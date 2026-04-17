namespace PizzaOrderSystem.Models;

/// <summary>In-memory order being constructed during the workflow session.</summary>
public class PendingOrder
{
    public Guid CustomerId { get; set; }
    public List<PendingOrderItem> Items { get; set; } = new();
    public string SpecialInstructions { get; set; } = string.Empty;
    public decimal TotalAmount => Items.Sum(i => i.UnitPrice * i.Quantity);

    public string FormatSummary()
    {
        var lines = Items.Select(i =>
            $"  • {i.Quantity}× {i.PizzaName} — {i.Size} — ${i.UnitPrice:F2}" +
            (i.Customizations.Count > 0
                ? $" [{string.Join(", ", i.Customizations.Select(kv => $"{kv.Key}: {kv.Value}"))}]"
                : string.Empty));

        return string.Join("\n", lines) + $"\n  Total: ${TotalAmount:F2}";
    }
}

public class PendingOrderItem
{
    public Guid PizzaId { get; set; }
    public string PizzaName { get; set; } = string.Empty;
    public PizzaSize Size { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public Dictionary<string, string> Customizations { get; set; } = new();
}
