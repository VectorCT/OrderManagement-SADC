using DataAccessLayer.Contexts;
using DataAccessLayer.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OrderManagementApi.Services.Interfaces;

namespace OrderManagementApi.Services;

public sealed class ApplicationDataSeeder(
    AppDbContext dbContext,
    RoleManager<IdentityRole> roleManager,
    UserManager<ApplicationUser> userManager,
    ISadcCurrencyPolicy currencyPolicy,
    ILogger<ApplicationDataSeeder> logger)
{
    private const int SeedOrderCount = 1024;
    private static readonly string[] SeedRoleNames = ["Admin", "Orders.Read", "Orders.Write", "Orders.Admin"];
    private static readonly CountrySeed[] Countries =
    [
        new("AO", "AOA", "Angola"),
        new("BW", "BWP", "Botswana"),
        new("KM", "KMF", "Comoros"),
        new("CD", "CDF", "DR Congo"),
        new("SZ", "SZL", "Eswatini"),
        new("LS", "LSL", "Lesotho"),
        new("MG", "MGA", "Madagascar"),
        new("MW", "MWK", "Malawi"),
        new("MU", "MUR", "Mauritius"),
        new("MZ", "MZN", "Mozambique"),
        new("NA", "NAD", "Namibia"),
        new("SC", "SCR", "Seychelles"),
        new("ZA", "ZAR", "South Africa"),
        new("TZ", "TZS", "Tanzania"),
        new("ZM", "ZMW", "Zambia"),
        new("ZW", "ZWL", "Zimbabwe")
    ];

    public async Task SeedAsync(string adminEmail, bool seedOrders, CancellationToken cancellationToken)
    {
        await SeedRolesAsync(cancellationToken);
        await AssignAdminRoleAsync(adminEmail, cancellationToken);
        if (seedOrders)
        {
            await SeedOrdersAsync(cancellationToken);
        }
    }

    private async Task SeedRolesAsync(CancellationToken cancellationToken)
    {
        foreach (var roleName in SeedRoleNames)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await roleManager.RoleExistsAsync(roleName))
            {
                continue;
            }

            var result = await roleManager.CreateAsync(new IdentityRole(roleName));
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Unable to seed role '{roleName}': {string.Join("; ", result.Errors.Select(error => error.Description))}");
            }
        }
    }

    private async Task AssignAdminRoleAsync(string adminEmail, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        if (adminUser is null)
        {
            logger.LogWarning(
                "The configured admin account {AdminEmail} does not exist. Register the account, then restart with data seeding enabled.",
                adminEmail);
            return;
        }

        if (await userManager.IsInRoleAsync(adminUser, "Admin"))
        {
            return;
        }

        var result = await userManager.AddToRoleAsync(adminUser, "Admin");
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Unable to assign the Admin role to '{adminEmail}': {string.Join("; ", result.Errors.Select(error => error.Description))}");
        }
    }

    private async Task SeedOrdersAsync(CancellationToken cancellationToken)
    {
        var customers = Countries.Select((country, index) => new Customer
        {
            Id = SeedGuid(1, index + 1),
            Name = $"{country.Name} Demo Customer",
            Email = $"seed-{country.Code.ToLowerInvariant()}@example.sadc",
            CountryCode = country.Code,
            CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(index)
        }).ToArray();
        var customerIds = customers.Select(customer => customer.Id).ToArray();
        var knownCustomerIds = (await dbContext.Customers
            .Where(customer => customerIds.Contains(customer.Id))
            .Select(customer => customer.Id)
            .ToListAsync(cancellationToken))
            .ToHashSet();
        dbContext.Customers.AddRange(customers.Where(customer => !knownCustomerIds.Contains(customer.Id)));
        await dbContext.SaveChangesAsync(cancellationToken);

        var seededOrderIds = Enumerable.Range(1, SeedOrderCount)
            .Select(sequence => SeedGuid(2, sequence))
            .ToArray();
        var knownOrderIds = (await dbContext.Orders
            .Where(order => seededOrderIds.Contains(order.Id))
            .Select(order => order.Id)
            .ToListAsync(cancellationToken))
            .ToHashSet();
        var customerByCountry = await dbContext.Customers
            .Where(customer => customerIds.Contains(customer.Id))
            .ToDictionaryAsync(customer => customer.CountryCode, cancellationToken);
        var orders = new List<Order>(SeedOrderCount - knownOrderIds.Count);
        for (var index = 0; index < SeedOrderCount; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var orderId = SeedGuid(2, index + 1);
            if (knownOrderIds.Contains(orderId))
            {
                continue;
            }

            var country = Countries[index % Countries.Length];
            var customer = customerByCountry[country.Code];
            var currency = country.Code == "ZW" && (index / Countries.Length) % 2 == 1
                ? "USD"
                : country.Currency;
            if (!currencyPolicy.IsCurrencyAllowed(country.Code, currency))
            {
                throw new InvalidOperationException(
                    $"Seed currency '{currency}' is not allowed for SADC country '{country.Code}'.");
            }

            var order = new Order
            {
                Id = orderId,
                CustomerId = customer.Id,
                Status = (OrderStatus)(index % Enum.GetValues<OrderStatus>().Length),
                CurrencyCode = currency,
                CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddHours(index * 3),
                Notes = "Deterministic SADC demo order"
            };
            var itemCount = 1 + index % 3;
            for (var itemIndex = 0; itemIndex < itemCount; itemIndex++)
            {
                order.LineItems.Add(new OrderLineItem
                {
                    Id = SeedGuid(3 + itemIndex, index + 1),
                    ProductSku = $"SADC-{(index % 25) + 1:000}",
                    Quantity = 1 + (index + itemIndex) % 5,
                    UnitPrice = 10m + (index % 500) + itemIndex * 2.5m
                });
            }

            order.RecalculateTotal();
            orders.Add(order);
        }

        if (orders.Count > 0)
        {
            dbContext.Orders.AddRange(orders);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation(
            "SADC sample data is ready: {OrderCount} deterministic orders across {CountryCount} countries.",
            SeedOrderCount,
            Countries.Length);
    }

    private static Guid SeedGuid(int prefix, int sequence) =>
        Guid.Parse($"0000000{prefix}-0000-0000-0000-{sequence:000000000000}");

    private sealed record CountrySeed(string Code, string Currency, string Name);
}
