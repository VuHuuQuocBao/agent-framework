using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using PizzaOrderSystem.Workflow;

namespace PizzaOrderSystem.Workflow.Nodes;

/// <summary>
/// Classifies user intent by running the OrchestratorAgent.
/// Input: the user's initial ChatMessage.
/// Output: PizzaOrderWorkflowState with Intent populated.
/// </summary>
[YieldsOutput(typeof(string))]
public class OrchestratorExecutor(AIAgent agent) : Executor<ChatMessage, PizzaOrderWorkflowState>("OrchestratorExecutor")
{
    private const string StateKey = PizzaOrderWorkflow.StateKey;

    public override async ValueTask<PizzaOrderWorkflowState> HandleAsync(
        ChatMessage message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        var state = new PizzaOrderWorkflowState();
        state.AddMessage("user", message.Text ?? string.Empty);

        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine("[Orchestrator] Classifying intent...");
        Console.ResetColor();

        var response = await agent.RunAsync(message.Text ?? string.Empty, cancellationToken: cancellationToken);
        var intentToken = response.Text?.Trim().ToUpperInvariant() ?? string.Empty;

        state.Intent = intentToken switch
        {
            "REORDER"       => OrderIntent.Reorder,
            "MENU_INQUIRY"  => OrderIntent.MenuInquiry,
            "CANCEL_ORDER"  => OrderIntent.CancelOrder,
            _               => OrderIntent.NewOrder,
        };

        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine($"[Orchestrator] Intent detected: {state.Intent}");
        Console.ResetColor();

        await context.QueueStateUpdateAsync(StateKey, state, cancellationToken: cancellationToken);
        return state;
    }
}
