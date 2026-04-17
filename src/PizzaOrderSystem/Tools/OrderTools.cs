using Microsoft.EntityFrameworkCore;
using PizzaOrderSystem.Data;
using PizzaOrderSystem.Models;
using System.Text.Json;

namespace PizzaOrderSystem.Tools;

/// <summary>Tools for building, modifying, and pricing a pending order.</summary>
public class OrderTools(PizzaDbContext db)
{
    private static readonly Dictionary<PizzaSize, decimal> SizePriceMultiplier = new()
    {
        [PizzaSize.Small]      = 0.75m,
        [PizzaSize.Medium]     = 0.90m,
        [PizzaSize.Large]      = 1.00m,
        [PizzaSize.ExtraLarge] = 1.25m,
    };

    /// <summary>
    /// Calculate the total price for a list of order items.
    /// Items is a JSON array of objects: [{pizzaId, size, quantity}].
    /// Returns a breakdown with per-item prices and grand total.
    /// </summary>
    public async Task<string> CalculateOrderPrice(string itemsJson)
    {
        List<OrderItemRequest>? items;
        try { items = JsonSerializer.Deserialize<List<OrderItemRequest>>(itemsJson); }
        catch { return "INVALID_JSON: Could not parse items array."; }

        if (items is null || items.Count == 0) return "EMPTY_ORDER: No items provided.";

        var lines = new List<string>();
        decimal total = 0;

        foreach (var item in items)
        {
            if (!Guid.TryParse(item.PizzaId, out var pizzaGuid))
            { lines.Add($"INVALID_ID: {item.PizzaId}"); continue; }

            if (!Enum.TryParse<PizzaSize>(item.Size, true, out var size))
            { lines.Add($"INVALID_SIZE: {item.Size}"); continue; }

            var pizza = await db.Pizzas.FindAsync(pizzaGuid);
            if (pizza is null) { lines.Add($"NOT_FOUND: {item.PizzaId}"); continue; }

            var unitPrice = pizza.BasePrice * SizePriceMultiplier[size];
            var lineTotal = unitPrice * item.Quantity;
            total += lineTotal;
            lines.Add($"  {pizza.Name} ({size}) x{item.Quantity} = ${lineTotal:F2}");
        }

        lines.Add($"TOTAL: ${total:F2}");
        return string.Join("\n", lines);
    }

    /// <summary>
    /// Validate that all pizza IDs in a pending order exist and are available.
    /// Items is a JSON array of pizzaId strings.
    /// Returns VALID or a list of problems.
    /// </summary>
    public async Task<string> ValidateOrder(string pizzaIdsJson)
    {
        List<string>? ids;
        try { ids = JsonSerializer.Deserialize<List<string>>(pizzaIdsJson); }
        catch { return "INVALID_JSON"; }

        if (ids is null || ids.Count == 0) return "EMPTY: No items to validate.";

        var problems = new List<string>();
        foreach (var idStr in ids)
        {
            if (!Guid.TryParse(idStr, out var guid)) { problems.Add($"INVALID_ID: {idStr}"); continue; }
            var pizza = await db.Pizzas.FindAsync(guid);
            if (pizza is null) problems.Add($"NOT_FOUND: {idStr}");
            else if (!pizza.IsAvailable) problems.Add($"UNAVAILABLE: {pizza.Name}");
        }

        return problems.Count == 0 ? "VALID" : string.Join("\n", problems);
    }
}

public record OrderItemRequest(string PizzaId, string Size, int Quantity);
