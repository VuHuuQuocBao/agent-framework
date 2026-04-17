using System.Text.Json;

namespace PizzaOrderSystem.Models;

public class OrderItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public Guid PizzaId { get; set; }
    public PizzaSize Size { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }

    /// <summary>Flexible JSONB column for extras: crust type, extra toppings, etc.</summary>
    public Dictionary<string, string> Customizations { get; set; } = new();

    public Order Order { get; set; } = null!;
    public Pizza Pizza { get; set; } = null!;
}
