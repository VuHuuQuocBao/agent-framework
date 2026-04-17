using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using PizzaOrderSystem.Models;
using PizzaOrderSystem.Workflow;
using System.Text.Json;

namespace PizzaOrderSystem.Workflow.Nodes;

/// <summary>
/// Builds or modifies a PendingOrder using the OrderBuilderAgent.
/// For reorders, the agent receives the last order + modification instructions.
/// For new orders, the agent receives the user's request and the menu.
/// </summary>
public class OrderBuilderExecutor(AIAgent agent)
    : Executor<PizzaOrderWorkflowState, PizzaOrderWorkflowState>("OrderBuilderExecutor")
{
    private const string StateKey = PizzaOrderWorkflow.StateKey;

    public override async ValueTask<PizzaOrderWorkflowState> HandleAsync(
        PizzaOrderWorkflowState state, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine("\n[OrderBuilder] Building your order...");
        Console.ResetColor();

        var session = await agent.CreateSessionAsync(cancellationToken: cancellationToken);

        string prompt;

        if (state.Intent == OrderIntent.Reorder && !string.IsNullOrEmpty(state.LastOrderRaw)
            && !state.LastOrderRaw.StartsWith("NO_ORDERS:"))
        {
            var modification = state.UserModificationRequest ?? "same as before";
            prompt =
                $"Customer: {state.CustomerName} (ID: {state.CustomerId})\n\n" +
                $"Last order data:\n{state.LastOrderRaw}\n\n" +
                $"Modification requested: \"{modification}\"\n\n" +
                "Please build the modified order and output the ORDER_READY block.";
        }
        else
        {
            // New order — build from scratch based on conversation history
            var history = string.Join("\n", state.ConversationHistory.TakeLast(10));
            prompt =
                $"Customer conversation so far:\n{history}\n\n" +
                $"Customer ID: {state.CustomerId?.ToString() ?? "guest"}\n\n" +
                "Please build the order from the customer's request and output the ORDER_READY block.";
        }

        var response = await agent.RunAsync(prompt, session, cancellationToken: cancellationToken);
        var responseText = response.Text ?? string.Empty;

        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.Write("\n[OrderBuilder]: ");
        Console.ResetColor();
        Console.WriteLine(responseText);

        // Parse the ORDER_READY JSON block
        state.PendingOrder = ParsePendingOrder(responseText, state.CustomerId);

        await context.QueueStateUpdateAsync(StateKey, state, cancellationToken: cancellationToken);
        return state;
    }

    private static PendingOrder? ParsePendingOrder(string agentResponse, Guid? customerId)
    {
        var marker = "ORDER_READY";
        var idx = agentResponse.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;

        var jsonStart = agentResponse.IndexOf('{', idx);
        if (jsonStart < 0) return null;

        try
        {
            var jsonStr = agentResponse[jsonStart..];
            // Trim to the matching brace
            int depth = 0, end = -1;
            for (int i = 0; i < jsonStr.Length; i++)
            {
                if (jsonStr[i] == '{') depth++;
                else if (jsonStr[i] == '}') { depth--; if (depth == 0) { end = i; break; } }
            }
            if (end < 0) return null;
            jsonStr = jsonStr[..(end + 1)];

            var doc = JsonDocument.Parse(jsonStr);
            var root = doc.RootElement;

            var order = new PendingOrder
            {
                CustomerId = customerId ?? Guid.Empty,
                SpecialInstructions = root.TryGetProperty("specialInstructions", out var si) ? si.GetString() ?? "" : "",
            };

            if (root.TryGetProperty("items", out var itemsEl))
            {
                foreach (var item in itemsEl.EnumerateArray())
                {
                    var pendingItem = new PendingOrderItem
                    {
                        PizzaId   = item.TryGetProperty("pizzaId", out var pid) && Guid.TryParse(pid.GetString(), out var g) ? g : Guid.Empty,
                        PizzaName = item.TryGetProperty("pizzaName", out var pn) ? pn.GetString() ?? "" : "",
                        Quantity  = item.TryGetProperty("quantity", out var q) ? q.GetInt32() : 1,
                        UnitPrice = item.TryGetProperty("unitPrice", out var up) ? (decimal)up.GetDouble() : 0,
                    };

                    if (item.TryGetProperty("size", out var sizeEl) &&
                        Enum.TryParse<PizzaSize>(sizeEl.GetString(), true, out var size))
                        pendingItem.Size = size;

                    if (item.TryGetProperty("customizations", out var custEl))
                    {
                        foreach (var prop in custEl.EnumerateObject())
                            pendingItem.Customizations[prop.Name] = prop.Value.GetString() ?? "";
                    }

                    order.Items.Add(pendingItem);
                }
            }

            return order;
        }
        catch
        {
            return null;
        }
    }
}
