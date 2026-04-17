using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Agents.AI.Workflows;
using PizzaOrderSystem.Agents;
using PizzaOrderSystem.Data;
using PizzaOrderSystem.Data.Seed;
using PizzaOrderSystem.Tools;
using PizzaOrderSystem.Workflow;

// ── Configuration ──────────────────────────────────────────────────────────────
var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false)
    .AddEnvironmentVariables()
    .Build();

var ollamaEndpoint = config["Ollama:Endpoint"] ?? "http://localhost:11434";
var ollamaModel    = config["Ollama:ModelName"] ?? "llama3";
var connString     = config.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("DefaultConnection not configured.");

// ── DI Container ───────────────────────────────────────────────────────────────
var services = new ServiceCollection();

services.AddDbContext<PizzaDbContext>(opt =>
    opt.UseNpgsql(connString));

services.AddScoped<CustomerTools>();
services.AddScoped<OrderHistoryTools>();
services.AddScoped<MenuTools>();
services.AddScoped<OrderTools>();
services.AddScoped<OrderSubmissionTools>();

services.AddSingleton<AgentFactory>(_ => new AgentFactory(ollamaEndpoint, ollamaModel));

var sp = services.BuildServiceProvider();

// ── Database: migrate + seed ────────────────────────────────────────────────────
await using (var scope = sp.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PizzaDbContext>();
    try
    {
        await db.Database.MigrateAsync();
        await DataSeeder.SeedAsync(db);
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[DB] Migration/seed failed: {ex.Message}");
        Console.WriteLine("Continuing without DB migration (database may not be available).");
        Console.ResetColor();
    }
}

// ── Welcome Banner ─────────────────────────────────────────────────────────────
Console.Clear();
Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine("╔══════════════════════════════════════════════════════════╗");
Console.WriteLine("║         🍕  Pizza Order System — Multi-Agent AI          ║");
Console.WriteLine("║         Powered by Microsoft Agent Framework + Ollama    ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════╝");
Console.ResetColor();
Console.WriteLine($"\nConnected to Ollama: {ollamaEndpoint}  Model: {ollamaModel}");
Console.WriteLine("\nType your order request and press Enter.");
Console.WriteLine("Examples:");
Console.WriteLine("  • \"I'd like to order a large pepperoni pizza\"");
Console.WriteLine("  • \"Order me the same pizza as last time but bigger\"");
Console.WriteLine("  • \"What pizzas do you have?\"");
Console.WriteLine("  • \"quit\" or \"exit\" to quit\n");

// ── Main chat loop ─────────────────────────────────────────────────────────────
while (true)
{
    Console.ForegroundColor = ConsoleColor.White;
    Console.Write("You: ");
    Console.ResetColor();

    var userInput = Console.ReadLine()?.Trim() ?? string.Empty;
    if (string.IsNullOrEmpty(userInput)) continue;
    if (userInput.Equals("quit", StringComparison.OrdinalIgnoreCase) ||
        userInput.Equals("exit", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine("\nGoodbye! 🍕");
        break;
    }

    Console.WriteLine();

    // ── Build workflow with a fresh DI scope (fresh DB context) ───────────────
    await using var scope = sp.CreateAsyncScope();
    var scopedSp = scope.ServiceProvider;

    var tools = new ToolsBundle(
        scopedSp.GetRequiredService<CustomerTools>(),
        scopedSp.GetRequiredService<OrderHistoryTools>(),
        scopedSp.GetRequiredService<MenuTools>(),
        scopedSp.GetRequiredService<OrderTools>(),
        scopedSp.GetRequiredService<OrderSubmissionTools>());

    var agentFactory = scopedSp.GetRequiredService<AgentFactory>();
    var workflow = PizzaOrderWorkflow.Build(agentFactory, tools);

    // ── Run the workflow ───────────────────────────────────────────────────────
    try
    {
        await using StreamingRun run = await InProcessExecution.RunStreamingAsync(
            workflow, new ChatMessage(ChatRole.User, userInput));

        await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

        await foreach (var evt in run.WatchStreamAsync())
        {
            switch (evt)
            {
                case AgentResponseUpdateEvent:
                    // Intermediate agent tokens — suppressed by default; enable for verbose mode
                    break;

                case WorkflowOutputEvent:
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine("\n─── Workflow completed ───────────────────────────────────\n");
                    Console.ResetColor();
                    break;

                case WorkflowErrorEvent err:
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"\n[Error] {err.Exception?.Message ?? "Unknown workflow error."}");
                    if (err.Exception?.InnerException is not null)
                        Console.WriteLine($"  Inner: {err.Exception.InnerException.Message}");
                    Console.ResetColor();
                    break;

                case ExecutorFailedEvent fail:
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"\n[Error] Executor '{fail.ExecutorId}' failed: {fail.Data}");
                    Console.ResetColor();
                    break;
            }
        }
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"\n[Fatal] {ex.Message}");
        if (ex.InnerException is not null)
            Console.WriteLine($"  Inner: {ex.InnerException.Message}");
        Console.ResetColor();
    }

    Console.WriteLine();
}
