using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using PizzaOrderSystem.Tools;
using PizzaOrderSystem.Workflow;

namespace PizzaOrderSystem.Workflow.Nodes;

/// <summary>
/// HITL node: asks the user for their customer ID and validates it via CustomerTools.
/// Uses a RequestPort pattern: emits a prompt and waits for the external response.
/// </summary>
public class CustomerIdentityExecutor(CustomerTools customerTools)
    : Executor<PizzaOrderWorkflowState, PizzaOrderWorkflowState>("CustomerIdentityExecutor")
{
    private const string StateKey = PizzaOrderWorkflow.StateKey;

    public override async ValueTask<PizzaOrderWorkflowState> HandleAsync(
        PizzaOrderWorkflowState state, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        const int maxAttempts = 3;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("\n🔐 [CustomerIdentity]: ");
            Console.ResetColor();
            Console.Write("To retrieve your previous order, please enter your Customer ID or registered phone number: ");

            var rawId = Console.ReadLine()?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(rawId)) continue;

            state.RawCustomerId = rawId;
            var result = await customerTools.GetCustomerById(rawId);

            if (result.StartsWith("FOUND:"))
            {
                // Parse: "FOUND: Customer ID=<guid>, Name=<name>, ..."
                var idPart = result.Split(',').FirstOrDefault(p => p.Contains("ID="));
                var namePart = result.Split(',').FirstOrDefault(p => p.Contains("Name="));

                if (idPart is not null && Guid.TryParse(idPart.Split('=').LastOrDefault()?.Trim(), out var customerId))
                    state.CustomerId = customerId;

                state.CustomerName = namePart?.Split('=').LastOrDefault()?.Trim();

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"\n✅ Welcome back, {state.CustomerName}!");
                Console.ResetColor();

                await context.QueueStateUpdateAsync(StateKey, state, cancellationToken: cancellationToken);
                return state;
            }

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n❌ Customer not found. {(attempt < maxAttempts ? $"Please try again ({maxAttempts - attempt} attempt(s) remaining)." : "Maximum attempts reached.")}");
            Console.ResetColor();
        }

        // After 3 failed attempts, set a flag and fall through
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n⚠️  Could not verify identity. Switching to new order flow.");
        Console.ResetColor();

        state.Intent = OrderIntent.NewOrder;
        await context.QueueStateUpdateAsync(StateKey, state, cancellationToken: cancellationToken);
        return state;
    }
}
