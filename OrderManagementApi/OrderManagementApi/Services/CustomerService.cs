using CSharpFunctionalExtensions;
using DataAccessLayer.Contexts;
using DataAccessLayer.Models;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using OrderManagementApi.Extensions;
using OrderManagementApi.Services.Interfaces;
using OrderManagementApi.Services.Models;
using OrderManagementApi.Services.Validation;

namespace OrderManagementApi.Services;

public sealed class CustomerService(
    AppDbContext dbContext,
    IValidator<CreateCustomerRequest> validator) : ICustomerService
{
    private readonly AppDbContext _dbContext = dbContext;
    private readonly IValidator<CreateCustomerRequest> _validator = validator;

    public async Task<Result<CustomerDto>> CreateAsync(CreateCustomerRequest request, CancellationToken ct = default)
    {
        var validationResult = await _validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
        {
            return Result.Failure<CustomerDto>(validationResult.ToErrorMessage());
        }

        var countryCode = request.CountryCode.Trim();
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
