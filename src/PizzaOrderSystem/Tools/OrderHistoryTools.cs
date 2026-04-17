using Microsoft.EntityFrameworkCore;
using PizzaOrderSystem.Data;
using PizzaOrderSystem.Models;

namespace PizzaOrderSystem.Tools;

/// <summary>Tools for fetching a customer's order history from PostgreSQL.</summary>
public class OrderHistoryTools(PizzaDbContext db)
{
    /// <summary>Fetch the most recent order for a customer, including all items and pizza names.</summary>
    public async Task<string> GetLastOrderByCustomerId(string customerId)
    {
        if (!Guid.TryParse(customerId, out var guid))
            return "INVALID_ID: Customer ID is not a valid GUID.";

        var order = await db.Orders
            .Include(o => o.Items)
                .ThenInclude(i => i.Pizza)
            .Where(o => o.CustomerId == guid)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync();

        if (order is null)
            return "NO_ORDERS: This customer has no previous orders.";

        var itemLines = order.Items.Select(i =>
        {
            var customs = i.Customizations.Count > 0
                ? $" (customizations: {string.Join(", ", i.Customizations.Select(kv => $"{kv.Key}={kv.Value}"))})"
                : string.Empty;
            return $"  - PizzaId={i.Pizza.Id}, Name={i.Pizza.Name}, Size={i.Size}, Qty={i.Quantity}, UnitPrice={i.UnitPrice:F2}{customs}";
        });

        return $"LAST_ORDER: OrderId={order.Id}, Date={order.CreatedAt:yyyy-MM-dd}, " +
               $"Total=${order.TotalAmount:F2}, Notes={order.Notes}\n" +
               string.Join("\n", itemLines);
    }

    /// <summary>Fetch a specific order by its order ID.</summary>
    public async Task<string> GetOrderById(string orderId)
    {
        if (!Guid.TryParse(orderId, out var guid))
            return "INVALID_ID: Order ID is not a valid GUID.";

        var order = await db.Orders
            .Include(o => o.Items)
                .ThenInclude(i => i.Pizza)
            .FirstOrDefaultAsync(o => o.Id == guid);

        if (order is null)
            return $"NOT_FOUND: No order found with ID '{orderId}'.";

        var itemLines = order.Items.Select(i =>
            $"  - {i.Pizza.Name} x{i.Quantity} ({i.Size}) @ ${i.UnitPrice:F2}");

        return $"ORDER: Id={order.Id}, Status={order.Status}, Total=${order.TotalAmount:F2}\n" +
               string.Join("\n", itemLines);
    }
}
