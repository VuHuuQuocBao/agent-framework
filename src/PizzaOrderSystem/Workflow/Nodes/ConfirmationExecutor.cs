using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using PizzaOrderSystem.Workflow;

namespace PizzaOrderSystem.Workflow.Nodes;

/// <summary>
/// Result of the confirmation step.
/// </summary>
public enum ConfirmationDecision { Confirmed, Cancelled, Modify }

public record ConfirmationResult(ConfirmationDecision Decision, string? ModificationRequest, PizzaOrderWorkflowState State);

/// <summary>
/// HITL executor: presents the order summary to the user and asks for confirmation.
/// Loops until the user confirms, cancels, or requests modification.
/// On modification, updates UserModificationRequest and returns Modify decision.
/// </summary>
[YieldsOutput(typeof(string))]
public class ConfirmationExecutor(AIAgent confirmationAgent)
    : Executor<PizzaOrderWorkflowState, ConfirmationResult>("ConfirmationExecutor")
{
    private const string StateKey = PizzaOrderWorkflow.StateKey;

    public override async ValueTask<ConfirmationResult> HandleAsync(
        PizzaOrderWorkflowState state, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        if (state.PendingOrder is null || state.PendingOrder.Items.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("\n[Confirmation] No order to confirm. Cancelling.");
            Console.ResetColor();
            await context.QueueStateUpdateAsync(StateKey, state, cancellationToken: cancellationToken);
            return new ConfirmationResult(ConfirmationDecision.Cancelled, null, state);
        }

        var session = await confirmationAgent.CreateSessionAsync(cancellationToken: cancellationToken);
        var orderSummary = state.PendingOrder.FormatSummary();
        var prompt =
            $"Customer: {state.CustomerName ?? "Guest"}\n\n" +
            $"Pending order:\n{orderSummary}\n\n" +
            "Please present the order summary and ask for confirmation.";

        var agentMsg = await confirmationAgent.RunAsync(prompt, session, cancellationToken: cancellationToken);

        Console.ForegroundColor = ConsoleColor.Blue;
        Console.Write("\n📋 [Confirmation]: ");
        Console.ResetColor();
        Console.WriteLine(agentMsg.Text);

        Console.Write("\nYour response: ");
        var userInput = Console.ReadLine()?.Trim().ToLowerInvariant() ?? string.Empty;
        state.AddMessage("user", userInput);

        if (userInput is "yes" or "y" or "confirm" or "ok" or "sure" or "yep" or "yeah")
        {
            state.IsConfirmed = true;
            await context.QueueStateUpdateAsync(StateKey, state, cancellationToken: cancellationToken);
            return new ConfirmationResult(ConfirmationDecision.Confirmed, null, state);
        }

        if (userInput is "no" or "n" or "cancel" or "stop" or "quit" or "exit")
        {
            state.IsCancelled = true;
            await context.QueueStateUpdateAsync(StateKey, state, cancellationToken: cancellationToken);
            return new ConfirmationResult(ConfirmationDecision.Cancelled, null, state);
        }

        // Treat everything else as a modification request
        state.UserModificationRequest = userInput;
        await context.QueueStateUpdateAsync(StateKey, state, cancellationToken: cancellationToken);
        return new ConfirmationResult(ConfirmationDecision.Modify, userInput, state);
    }
}
