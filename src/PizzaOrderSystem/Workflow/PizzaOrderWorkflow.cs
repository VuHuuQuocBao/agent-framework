using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using PizzaOrderSystem.Agents;
using PizzaOrderSystem.Tools;
using PizzaOrderSystem.Workflow.Nodes;
using AgentWorkflow = Microsoft.Agents.AI.Workflows.Workflow;

namespace PizzaOrderSystem.Workflow;

/// <summary>
/// Assembles the pizza ordering multi-agent workflow graph.
///
/// Graph topology:
///
///   [OrchestratorExecutor]
///        │
///        ├── MENU_INQUIRY  ────────────────────────────────► [MenuExecutor] ──► END
///        │
///        ├── CANCEL_ORDER  ────────────────────────────────► [CancelExecutor] ──► END
///        │
///        ├── REORDER ──► [CustomerIdentityExecutor]
///        │                       │
///        │               [OrderHistoryExecutor]
///        │                       │
///        │                       └──────────────────────────────────┐
///        │                                                           │
///        └── NEW_ORDER ──────────────────────────────► [OrderBuilderExecutor]
///                                                               │
///                                                   [ConfirmationExecutor]
///                                                        │   │    │
///                                            CONFIRMED ──┘   │    └─── MODIFY ─► [ModificationBridgeExecutor]
///                                                             │                          │
///                                                         CANCELLED              (loops back to OrderBuilderExecutor)
///                                                             │
///                                              [CancelledBridgeExecutor]
///                                                             │
///                                                   [CancelExecutor] ──► END
///
/// Confirmed path:
///   [ConfirmationExecutor] ─► CONFIRMED ─► [OrderSubmissionExecutor] ──► END
/// </summary>
public static class PizzaOrderWorkflow
{
    public const string StateKey = "pizza_order_state";

    public static AgentWorkflow Build(AgentFactory agentFactory, ToolsBundle tools)
    {
        // --- Create agents ---
        var orchestratorAgent  = agentFactory.CreateOrchestratorAgent();
        var identityAgent      = agentFactory.CreateCustomerIdentityAgent();
        var historyAgent       = agentFactory.CreateOrderHistoryAgent();
        var menuAgent          = agentFactory.CreateMenuAgent(tools.MenuTools);
        var builderAgent       = agentFactory.CreateOrderBuilderAgent(tools.MenuTools, tools.OrderTools);
        var confirmationAgent  = agentFactory.CreateConfirmationAgent();
        var submissionAgent    = agentFactory.CreateOrderSubmissionAgent(tools.SubmissionTools);

        // --- Create executors ---
        var orchestratorExec   = new OrchestratorExecutor(orchestratorAgent);
        var menuExec           = new MenuExecutor(menuAgent);
        var cancelExec         = new CancelExecutor();
        var customerIdExec     = new CustomerIdentityExecutor(tools.CustomerTools);
        var orderHistoryExec   = new OrderHistoryExecutor(historyAgent, tools.HistoryTools);
        var orderBuilderExec   = new OrderBuilderExecutor(builderAgent);
        var confirmationExec   = new ConfirmationExecutor(confirmationAgent);
        var submissionExec     = new OrderSubmissionExecutor(submissionAgent, tools.SubmissionTools);
        var modBridgeExec      = new ModificationBridgeExecutor();
        var cancelBridgeExec   = new CancelledBridgeExecutor();

        // --- Build the workflow graph ---
        var builder = new WorkflowBuilder(orchestratorExec);

        builder
            // Route from orchestrator based on intent
            .AddSwitch(orchestratorExec, sw => sw
                .AddCase<PizzaOrderWorkflowState>(
                    s => s?.Intent == OrderIntent.MenuInquiry,
                    menuExec)
                .AddCase<PizzaOrderWorkflowState>(
                    s => s?.Intent == OrderIntent.CancelOrder,
                    cancelExec)
                .AddCase<PizzaOrderWorkflowState>(
                    s => s?.Intent == OrderIntent.Reorder,
                    customerIdExec)
                .WithDefault(
                    orderBuilderExec))  // NEW_ORDER goes straight to builder

            // Reorder path: identity → history → builder
            .AddEdge(customerIdExec, orderHistoryExec)
            .AddEdge(orderHistoryExec, orderBuilderExec)

            // Both new order and reorder paths converge at confirmation
            .AddEdge(orderBuilderExec, confirmationExec)

            // Confirmation routing
            .AddSwitch(confirmationExec, sw => sw
                .AddCase<ConfirmationResult>(
                    r => r?.Decision == ConfirmationDecision.Confirmed,
                    submissionExec)
                .AddCase<ConfirmationResult>(
                    r => r?.Decision == ConfirmationDecision.Modify,
                    modBridgeExec)
                .WithDefault(
                    cancelBridgeExec))   // Cancelled

            // Modification loop: bridge → orderBuilder → confirmation (loop)
            .AddEdge(modBridgeExec, orderBuilderExec)

            // Cancellation bridge → cancel executor
            .AddEdge(cancelBridgeExec, cancelExec)

            // Define which executors yield the terminal output
            .WithOutputFrom(menuExec, cancelExec, submissionExec);

        return builder.Build();
    }
}

/// <summary>Bundles all tool instances for clean injection.</summary>
public record ToolsBundle(
    CustomerTools CustomerTools,
    OrderHistoryTools HistoryTools,
    MenuTools MenuTools,
    OrderTools OrderTools,
    OrderSubmissionTools SubmissionTools);
