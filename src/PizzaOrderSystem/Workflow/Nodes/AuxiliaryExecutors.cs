using Microsoft.Agents.AI.Workflows;
using PizzaOrderSystem.Workflow;

namespace PizzaOrderSystem.Workflow.Nodes;

/// <summary>
/// Emitted when the user explicitly cancels the order.
/// </summary>
[YieldsOutput(typeof(string))]
public class CancelExecutor : Executor<PizzaOrderWorkflowState>
{
    public CancelExecutor() : base("CancelExecutor") { }

    public override async ValueTask HandleAsync(
        PizzaOrderWorkflowState state, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n🚫 Order cancelled. Have a great day!");
        Console.ResetColor();

        await context.YieldOutputAsync("Order cancelled by user.", cancellationToken);
    }
}

/// <summary>
/// Handles the modification-loopback: extracts state from ConfirmationResult and re-routes
/// to OrderBuilderExecutor for re-building.
/// </summary>
public class ModificationBridgeExecutor : Executor<ConfirmationResult, PizzaOrderWorkflowState>
{
    public ModificationBridgeExecutor() : base("ModificationBridgeExecutor") { }

    private const string StateKey = PizzaOrderWorkflow.StateKey;

    public override async ValueTask<PizzaOrderWorkflowState> HandleAsync(
        ConfirmationResult result, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        var state = result.State;
        state.UserModificationRequest = result.ModificationRequest;
        state.Intent = OrderIntent.Reorder; // treat modification as a reorder of the pending order

        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine($"\n[Modification] Applying: \"{result.ModificationRequest}\"");
        Console.ResetColor();

        await context.QueueStateUpdateAsync(StateKey, state, cancellationToken: cancellationToken);
        return state;
    }
}

/// <summary>
/// Cancelled bridge: extracts state from ConfirmationResult and routes to CancelExecutor.
/// </summary>
public class CancelledBridgeExecutor : Executor<ConfirmationResult, PizzaOrderWorkflowState>
{
    public CancelledBridgeExecutor() : base("CancelledBridgeExecutor") { }

    public override ValueTask<PizzaOrderWorkflowState> HandleAsync(
        ConfirmationResult result, IWorkflowContext context, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(result.State);
}
