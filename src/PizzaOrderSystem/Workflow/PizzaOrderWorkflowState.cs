using PizzaOrderSystem.Models;

namespace PizzaOrderSystem.Workflow;

public enum OrderIntent
{
    Unknown,
    NewOrder,
    Reorder,
    MenuInquiry,
    CancelOrder
}

/// <summary>
/// Typed shared state that flows through every node in the pizza order workflow.
/// Each node reads from and writes to this object instead of re-parsing raw messages.
/// </summary>
public class PizzaOrderWorkflowState
{
    // --- Identity ---
    public string? RawCustomerId { get; set; }
    public Guid? CustomerId { get; set; }
    public string? CustomerName { get; set; }

    // --- Intent ---
    public OrderIntent Intent { get; set; } = OrderIntent.Unknown;
    public string? UserModificationRequest { get; set; } // e.g. "bigger size", "no onions"

    // --- History ---
    public string? LastOrderRaw { get; set; }  // raw string returned by OrderHistoryTools

    // --- Active order being built ---
    public PendingOrder? PendingOrder { get; set; }

    // --- Confirmation ---
    public bool IsConfirmed { get; set; }
    public bool IsCancelled { get; set; }

    // --- Final result ---
    public string? ConfirmedOrderId { get; set; }
    public string? ConfirmationNumber { get; set; }

    // --- Conversation context passed to agents ---
    public List<string> ConversationHistory { get; set; } = new();

    public void AddMessage(string role, string content)
        => ConversationHistory.Add($"[{role.ToUpper()}]: {content}");
}
