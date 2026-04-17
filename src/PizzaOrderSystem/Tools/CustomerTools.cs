using Microsoft.EntityFrameworkCore;
using PizzaOrderSystem.Data;
using PizzaOrderSystem.Models;

namespace PizzaOrderSystem.Tools;

/// <summary>Tools for looking up customer records from PostgreSQL.</summary>
public class CustomerTools(PizzaDbContext db)
{
    /// <summary>Look up a customer by their UUID or phone number and return their profile.</summary>
    public async Task<string> GetCustomerById(string customerId)
    {
        Customer? customer = null;

        if (Guid.TryParse(customerId, out var guid))
            customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == guid);

        if (customer is null)
            customer = await db.Customers.FirstOrDefaultAsync(c => c.Phone == customerId.Trim());

        if (customer is null)
            return $"NOT_FOUND: No customer found with ID or phone '{customerId}'.";

        return $"FOUND: Customer ID={customer.Id}, Name={customer.Name}, Phone={customer.Phone}, Email={customer.Email}";
    }
}
