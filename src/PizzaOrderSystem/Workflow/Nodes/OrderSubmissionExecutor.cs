using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using PizzaOrderSystem.Tools;
using PizzaOrderSystem.Workflow;
using System.Text.Json;

namespace PizzaOrderSystem.Workflow.Nodes;

/// <summary>
/// Saves the confirmed order to PostgreSQL and presents a success message.
/// </summary>
[YieldsOutput(typeof(string))]
public class OrderSubmissionExecutor(AIAgent agent, OrderSubmissionTools submissionTools)
    : Executor<ConfirmationResult>("OrderSubmissionExecutor")
{
    private const string StateKey = PizzaOrderWorkflow.StateKey;

    public override async ValueTask HandleAsync(
        ConfirmationResult result, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        var state = result.State;

        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine("\n[OrderSubmission] Saving your order...");
        Console.ResetColor();

        var order = state.PendingOrder!;
        var itemsJson = JsonSerializer.Serialize(order.Items);

        var saveResult = await submissionTools.SaveOrder(
            state.CustomerId?.ToString() ?? Guid.Empty.ToString(),
            itemsJson,
            order.SpecialInstructions);

        string confirmationNumber = string.Empty;
        if (saveResult.StartsWith("ORDER_SAVED:"))
        {
            var orderId = saveResult.Split("OrderId=").LastOrDefault()?.Split(',').FirstOrDefault()?.Trim();
            if (orderId is not null)
            {
                var confResult = submissionTools.GenerateConfirmationNumber(orderId);
                confirmationNumber = confResult.Replace("CONFIRMATION: ", string.Empty);
                state.ConfirmedOrderId = orderId;
                state.ConfirmationNumber = confirmationNumber;
            }
        }

        // Use the agent to compose the final success message
        var session = await agent.CreateSessionAsync(cancellationToken: cancellationToken);
        var prompt =
            $"Customer: {state.CustomerName ?? "Guest"}\n" +
            $"Save result: {saveResult}\n" +
            $"Confirmation number: {confirmationNumber}\n\n" +
            "Please compose a friendly order confirmation message.";

        var response = await agent.RunAsync(prompt, session, cancellationToken: cancellationToken);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("\n✅ [OrderSubmission]: ");
        Console.ResetColor();
        Console.WriteLine(response.Text);

        await context.QueueStateUpdateAsync(StateKey, state, cancellationToken: cancellationToken);
        await context.YieldOutputAsync(response.Text ?? "Order placed successfully!", cancellationToken);
    }
}
