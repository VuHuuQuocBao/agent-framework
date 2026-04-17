using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PizzaOrderSystem.Data;

/// <summary>
/// Used by EF Core tools (dotnet-ef migrations) to create a DbContext at design time
/// without running the full application startup.
/// </summary>
public class PizzaDbContextDesignTimeFactory : IDesignTimeDbContextFactory<PizzaDbContext>
{
    public PizzaDbContext CreateDbContext(string[] args)
    {
        var opts = new DbContextOptionsBuilder<PizzaDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=pizza_orders;Username=postgres;Password=postgres")
            .Options;

        return new PizzaDbContext(opts);
    }
}
