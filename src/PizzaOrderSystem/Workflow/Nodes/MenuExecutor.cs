using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using PizzaOrderSystem.Tools;
using PizzaOrderSystem.Workflow;

namespace PizzaOrderSystem.Workflow.Nodes;

/// <summary>
/// Handles the menu inquiry path.
/// Runs MenuAgent and yields output.
/// </summary>
[YieldsOutput(typeof(string))]
public class MenuExecutor(AIAgent agent) : Executor<PizzaOrderWorkflowState>("MenuExecutor")
{
    public override async ValueTask HandleAsync(
        PizzaOrderWorkflowState state, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("\n🍕 [MenuAgent]: ");
        Console.ResetColor();

        var session = await agent.CreateSessionAsync(cancellationToken: cancellationToken);
        var prompt = "The customer wants to see the menu. Please retrieve and display all available pizzas with their prices.";

        var response = await agent.RunAsync(prompt, session, cancellationToken: cancellationToken);
        Console.WriteLine(response.Text);

        await context.YieldOutputAsync(response.Text ?? string.Empty, cancellationToken);
    }
}
