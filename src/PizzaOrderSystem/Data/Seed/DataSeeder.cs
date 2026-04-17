using Microsoft.EntityFrameworkCore;
using PizzaOrderSystem.Models;

namespace PizzaOrderSystem.Data.Seed;

public static class DataSeeder
{
    public static async Task SeedAsync(PizzaDbContext db)
    {
        if (await db.Pizzas.AnyAsync()) return; // already seeded

        // --- Menu ---
        var margherita = new Pizza { Id = Guid.Parse("00000000-0000-0000-0001-000000000001"), Name = "Margherita", Description = "Classic tomato sauce, mozzarella, fresh basil", BasePrice = 10.99m };
        var pepperoni  = new Pizza { Id = Guid.Parse("00000000-0000-0000-0001-000000000002"), Name = "Pepperoni", Description = "Tomato sauce, mozzarella, spicy pepperoni", BasePrice = 12.99m };
        var bbqChicken = new Pizza { Id = Guid.Parse("00000000-0000-0000-0001-000000000003"), Name = "BBQ Chicken", Description = "BBQ sauce, grilled chicken, red onion, mozzarella", BasePrice = 13.99m };
        var veggie     = new Pizza { Id = Guid.Parse("00000000-0000-0000-0001-000000000004"), Name = "Veggie Supreme", Description = "Tomato sauce, mozzarella, bell peppers, mushrooms, olives, onions", BasePrice = 11.99m };
        var hawaiian   = new Pizza { Id = Guid.Parse("00000000-0000-0000-0001-000000000005"), Name = "Hawaiian", Description = "Tomato sauce, mozzarella, ham, pineapple", BasePrice = 12.49m };

        db.Pizzas.AddRange(margherita, pepperoni, bbqChicken, veggie, hawaiian);

        // --- Customers ---
        var alice = new Customer
        {
            Id    = Guid.Parse("00000000-0000-0000-0002-000000000001"),
            Name  = "Alice Johnson",
            Phone = "555-1001",
            Email = "alice@example.com"
        };
        var bob = new Customer
        {
            Id    = Guid.Parse("00000000-0000-0000-0002-000000000002"),
            Name  = "Bob Smith",
            Phone = "555-1002",
            Email = "bob@example.com"
        };
        db.Customers.AddRange(alice, bob);

        await db.SaveChangesAsync();

        // --- Past orders ---
        // Alice's last order: 1 Large Pepperoni + 1 Small Margherita
        var aliceOrder = new Order
        {
            Id          = Guid.Parse("00000000-0000-0000-0003-000000000001"),
            CustomerId  = alice.Id,
            Status      = OrderStatus.Delivered,
            TotalAmount = 23.48m,
            CreatedAt   = DateTime.UtcNow.AddDays(-7),
            Notes       = "Extra napkins please"
        };
        aliceOrder.Items.Add(new OrderItem
        {
            PizzaId        = pepperoni.Id,
            Size           = PizzaSize.Large,
            Quantity       = 1,
            UnitPrice      = 15.99m,
            Customizations = new() { { "crust", "thin" } }
        });
        aliceOrder.Items.Add(new OrderItem
        {
            PizzaId   = margherita.Id,
            Size      = PizzaSize.Small,
            Quantity  = 1,
            UnitPrice = 7.49m
        });

        // Bob's last order: 1 ExtraLarge BBQ Chicken
        var bobOrder = new Order
        {
            Id          = Guid.Parse("00000000-0000-0000-0003-000000000002"),
            CustomerId  = bob.Id,
            Status      = OrderStatus.Delivered,
            TotalAmount = 17.99m,
            CreatedAt   = DateTime.UtcNow.AddDays(-3),
        };
        bobOrder.Items.Add(new OrderItem
        {
            PizzaId        = bbqChicken.Id,
            Size           = PizzaSize.ExtraLarge,
            Quantity       = 1,
            UnitPrice      = 17.99m,
            Customizations = new() { { "extra_cheese", "yes" } }
        });

        db.Orders.AddRange(aliceOrder, bobOrder);
        await db.SaveChangesAsync();

        Console.WriteLine("[DB] Seed data inserted successfully.");
    }
}
