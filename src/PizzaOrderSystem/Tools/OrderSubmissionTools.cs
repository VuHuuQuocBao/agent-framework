using PizzaOrderSystem.Data;
using PizzaOrderSystem.Models;

namespace PizzaOrderSystem.Tools;

/// <summary>Tools for persisting a confirmed order to PostgreSQL.</summary>
public class OrderSubmissionTools(PizzaDbContext db)
{
    /// <summary>
    /// Persist a confirmed PendingOrder to the database and return the order ID.
    /// customerId: the customer's UUID string.
    /// itemsJson: JSON array of {pizzaId, size, quantity, unitPrice, customizations}.
    /// specialInstructions: optional free-text notes.
    /// </summary>
    public async Task<string> SaveOrder(
        string customerId,
        string itemsJson,
        string specialInstructions = "")
    {
        if (!Guid.TryParse(customerId, out var custGuid))
            return "INVALID_CUSTOMER_ID";

        List<PendingOrderItem>? items;
        try { items = System.Text.Json.JsonSerializer.Deserialize<List<PendingOrderItem>>(itemsJson); }
        catch { return "INVALID_JSON"; }

        if (items is null || items.Count == 0)
            return "EMPTY_ORDER";

        var order = new Order
        {
            CustomerId = custGuid,
            Status     = OrderStatus.Confirmed,
            Notes      = specialInstructions,
            CreatedAt  = DateTime.UtcNow,
        };

        foreach (var item in items)
        {
            order.Items.Add(new OrderItem
            {
                PizzaId        = item.PizzaId,
                Size           = item.Size,
                Quantity       = item.Quantity,
                UnitPrice      = item.UnitPrice,
                Customizations = item.Customizations,
            });
        }

        order.TotalAmount = order.Items.Sum(i => i.UnitPrice * i.Quantity);

        db.Orders.Add(order);
        await db.SaveChangesAsync();

        return $"ORDER_SAVED: OrderId={order.Id}, Total=${order.TotalAmount:F2}";
    }

    /// <summary>Generate a human-readable confirmation number from an order ID.</summary>
    public string GenerateConfirmationNumber(string orderId)
    {
        if (!Guid.TryParse(orderId, out var guid))
            return "INVALID_ID";

        var shortCode = guid.ToString("N")[..8].ToUpper();
        return $"CONFIRMATION: #{shortCode}";
    }
}
