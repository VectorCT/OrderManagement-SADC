using CSharpFunctionalExtensions;
using DataAccessLayer.Contexts;
using DataAccessLayer.Models;
using Microsoft.EntityFrameworkCore;
using OrderManagementApi.Extensions;
using OrderManagementApi.Services.Interfaces;
using OrderManagementApi.Services.Models;

namespace OrderManagementApi.Services;

public sealed class CustomerService(AppDbContext dbContext) : ICustomerService
{
    private readonly AppDbContext _dbContext = dbContext;

    public async Task<Result<CustomerDto>> CreateAsync(CreateCustomerRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Result.Failure<CustomerDto>("Customer name is required.");
        }

        var countryCode = request.CountryCode.Trim();
        if (string.IsNullOrWhiteSpace(countryCode) || countryCode.Length != 2)
        {
            return Result.Failure<CustomerDto>("Country code must be a valid ISO 3166-1 alpha-2 code.");
        }

        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Email = request.Email.Trim(),
            CountryCode = countryCode.ToUpperInvariant(),
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Customers.Add(customer);
        await _dbContext.SaveChangesAsync(ct);

        return Result.Success(ToDto(customer));
    }

    public async Task<Result<CustomerDto>> GetAsync(Guid id, CancellationToken ct = default)
    {
        var customer = await _dbContext.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        return customer is null
            ? Result.Failure<CustomerDto>("Customer was not found.")
            : Result.Success(ToDto(customer));
    }

    public async Task<Result<IReadOnlyList<CustomerDto>>> SearchAsync(string? search, int page, int pageSize, CancellationToken ct = default)
    {
        var normalizedPage = page < 1 ? 1 : page;
        var normalizedPageSize = pageSize < 1 ? 20 : Math.Min(pageSize, 100);

        var query = _dbContext.Customers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var filter = search.Trim();
            query = query.Where(c => c.Name.Contains(filter) || c.Email.Contains(filter));
        }

        var items = await query
            .OrderBy(c => c.Name)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .Select(c => new CustomerDto(c.Id, c.Name, c.Email, c.CountryCode, c.CreatedAt))
            .ToReadOnlyListAsync();

        return Result.Success(items);
    }

    private static CustomerDto ToDto(Customer customer) =>
        new(
            customer.Id,
            customer.Name,
            customer.Email,
            customer.CountryCode,
            customer.CreatedAt
        );
}
