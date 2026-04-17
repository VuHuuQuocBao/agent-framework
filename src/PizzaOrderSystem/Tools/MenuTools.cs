using Microsoft.EntityFrameworkCore;
using PizzaOrderSystem.Data;
using PizzaOrderSystem.Models;

namespace PizzaOrderSystem.Tools;

/// <summary>Tools for retrieving menu information and resolving size modifications.</summary>
public class MenuTools(PizzaDbContext db)
{
    private static readonly Dictionary<PizzaSize, decimal> SizePriceMultiplier = new()
    {
        [PizzaSize.Small]      = 0.75m,
        [PizzaSize.Medium]     = 0.90m,
        [PizzaSize.Large]      = 1.00m,
        [PizzaSize.ExtraLarge] = 1.25m,
    };

    /// <summary>Get all available pizzas with base prices and size pricing.</summary>
    public async Task<string> GetMenuItems()
    {
        var pizzas = await db.Pizzas.Where(p => p.IsAvailable).ToListAsync();
        if (!pizzas.Any()) return "MENU_EMPTY: No items available.";

        var lines = pizzas.Select(p =>
        {
            var prices = Enum.GetValues<PizzaSize>()
                .Select(s => $"{s}=${p.BasePrice * SizePriceMultiplier[s]:F2}");
            return $"  [{p.Id}] {p.Name} — {p.Description}\n    Prices: {string.Join(", ", prices)}";
        });

        return "MENU:\n" + string.Join("\n", lines);
    }

    /// <summary>Get the price for a specific pizza at a given size.</summary>
    public async Task<string> GetPizzaPrice(string pizzaId, string size)
    {
        if (!Guid.TryParse(pizzaId, out var guid))
            return "INVALID_ID";
        if (!Enum.TryParse<PizzaSize>(size, true, out var pizzaSize))
            return $"INVALID_SIZE: Valid sizes are {string.Join(", ", Enum.GetNames<PizzaSize>())}";

        var pizza = await db.Pizzas.FindAsync(guid);
        if (pizza is null) return "NOT_FOUND";

        var price = pizza.BasePrice * SizePriceMultiplier[pizzaSize];
        return $"PRICE: {pizza.Name} ({pizzaSize}) = ${price:F2}";
    }

    /// <summary>
    /// Returns the next larger pizza size. If already ExtraLarge, returns ExtraLarge (maximum).
    /// Use this when the customer asks for "bigger", "larger", or "upsize".
    /// </summary>
    public string GetNextSize(string currentSize)
    {
        if (!Enum.TryParse<PizzaSize>(currentSize, true, out var size))
            return $"INVALID_SIZE: Valid sizes are {string.Join(", ", Enum.GetNames<PizzaSize>())}";

        var next = size switch
        {
            PizzaSize.Small      => PizzaSize.Medium,
            PizzaSize.Medium     => PizzaSize.Large,
            PizzaSize.Large      => PizzaSize.ExtraLarge,
            PizzaSize.ExtraLarge => PizzaSize.ExtraLarge,
            _                    => size
        };

        var changed = next != size;
        return changed
            ? $"NEXT_SIZE: {size} → {next}"
            : $"ALREADY_MAX: {size} is already the largest size.";
    }

    /// <summary>
    /// Returns the next smaller pizza size. If already Small, returns Small (minimum).
    /// Use this when the customer asks for "smaller" or "downsize".
    /// </summary>
    public string GetPreviousSize(string currentSize)
    {
        if (!Enum.TryParse<PizzaSize>(currentSize, true, out var size))
            return $"INVALID_SIZE: Valid sizes are {string.Join(", ", Enum.GetNames<PizzaSize>())}";

        var prev = size switch
        {
            PizzaSize.ExtraLarge => PizzaSize.Large,
            PizzaSize.Large      => PizzaSize.Medium,
            PizzaSize.Medium     => PizzaSize.Small,
            PizzaSize.Small      => PizzaSize.Small,
            _                    => size
        };

        var changed = prev != size;
        return changed
            ? $"PREV_SIZE: {size} → {prev}"
            : $"ALREADY_MIN: {size} is already the smallest size.";
    }

    /// <summary>List all available pizza sizes with price multipliers.</summary>
    public string GetPizzaSizes()
    {
        var lines = SizePriceMultiplier.Select(kv => $"  {kv.Key}: ×{kv.Value:F2} base price");
        return "SIZES:\n" + string.Join("\n", lines);
    }
}
