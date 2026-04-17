using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using PizzaOrderSystem.Tools;
using PizzaOrderSystem.Workflow;

namespace PizzaOrderSystem.Workflow.Nodes;

/// <summary>
/// Fetches the customer's last order from PostgreSQL using OrderHistoryTools.
/// Stores the raw order string in state for the OrderBuilderExecutor.
/// </summary>
public class OrderHistoryExecutor(AIAgent agent, OrderHistoryTools historyTools)
    : Executor<PizzaOrderWorkflowState, PizzaOrderWorkflowState>("OrderHistoryExecutor")
{
    private const string StateKey = PizzaOrderWorkflow.StateKey;

    public override async ValueTask<PizzaOrderWorkflowState> HandleAsync(
        PizzaOrderWorkflowState state, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        if (state.CustomerId is null)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("[OrderHistory] No customer ID found, skipping history lookup.");
            Console.ResetColor();
            await context.QueueStateUpdateAsync(StateKey, state, cancellationToken: cancellationToken);
            return state;
        }

        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine("[OrderHistory] Fetching your last order...");
        Console.ResetColor();

        var rawHistory = await historyTools.GetLastOrderByCustomerId(state.CustomerId.Value.ToString());
        state.LastOrderRaw = rawHistory;

        if (rawHistory.StartsWith("NO_ORDERS:"))
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("📋 You have no previous orders. Let's place a new one!");
            Console.ResetColor();
        }
        else
        {
            // Use the OrderHistoryAgent to format and display the history nicely
            var session = await agent.CreateSessionAsync(cancellationToken: cancellationToken);
            var prompt = $"Here is the raw last order data for customer {state.CustomerName}:\n\n{rawHistory}\n\nPlease present this order clearly to the customer.";
            var response = await agent.RunAsync(prompt, session, cancellationToken: cancellationToken);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("\n📦 [OrderHistory]: ");
            Console.ResetColor();
            Console.WriteLine(response.Text);
        }

        await context.QueueStateUpdateAsync(StateKey, state, cancellationToken: cancellationToken);
        return state;
    }
}
