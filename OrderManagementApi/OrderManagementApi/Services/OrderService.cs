using System.Text;
using CSharpFunctionalExtensions;
using DataAccessLayer.Contexts;
using DataAccessLayer.Models;
using Microsoft.EntityFrameworkCore;
using OrderManagementApi.Services.Interfaces;
using OrderManagementApi.Services.Models;

namespace OrderManagementApi.Services;

public sealed class OrderService(AppDbContext dbContext, ISadcCurrencyPolicy currencyPolicy, IOrderStateMachine stateMachine) : IOrderService
{
    private readonly AppDbContext _dbContext = dbContext;
    private readonly ISadcCurrencyPolicy _currencyPolicy = currencyPolicy;
    private readonly IOrderStateMachine _stateMachine = stateMachine;

    public async Task<Result<OrderDto>> CreateAsync(CreateOrderRequest request, CancellationToken ct = default)
    {
        var customer = await _dbContext.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == request.CustomerId, ct);
        if (customer is null)
        {
            return Result.Failure<OrderDto>("Customer was not found.");
        }

        var currencyCode = request.CurrencyCode.Trim();
        if (!_currencyPolicy.IsCurrencyAllowed(customer.CountryCode, currencyCode))
        {
            return Result.Failure<OrderDto>("Currency is not valid for the customer's country.");
        }

        if (request.LineItems is null || request.LineItems.Count == 0)
        {
            return Result.Failure<OrderDto>("Order must contain at least one line item.");
        }

        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            Customer = customer,
            Status = OrderStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            CurrencyCode = currencyCode.ToUpperInvariant(),
            Notes = null,
            UpdatedAt = null
        };

        foreach (var item in request.LineItems)
        {
            if (item.Quantity <= 0)
            {
                return Result.Failure<OrderDto>("Line item quantity must be greater than zero.");
            }

            if (item.UnitPrice < 0)
            {
                return Result.Failure<OrderDto>("Line item unit price cannot be negative.");
            }

            order.LineItems.Add(new OrderLineItem
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                ProductSku = item.ProductSku.Trim(),
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice
            });
        }

        order.RecalculateTotal();

        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync(ct);

        return Result.Success(ToDto(order));
    }

    public async Task<Result<OrderDto>> GetAsync(Guid id, CancellationToken ct = default)
    {
        var order = await _dbContext.Orders
            .AsNoTracking()
            .Include(o => o.LineItems)
            .FirstOrDefaultAsync(o => o.Id == id, ct);

        return order is null
            ? Result.Failure<OrderDto>("Order was not found.")
            : Result.Success(ToDto(order));
    }

    public async Task<Result<IReadOnlyList<OrderSummaryDto>>> ListAsync(Guid? customerId, OrderStatus? status, int page, int pageSize, string? sort, CancellationToken ct = default)
    {
        var normalizedPage = page < 1 ? 1 : page;
        var normalizedPageSize = pageSize < 1 ? 20 : Math.Min(pageSize, 100);

        var query = _dbContext.Orders.AsNoTracking().AsQueryable();

        if (customerId.HasValue)
        {
            query = query.Where(o => o.CustomerId == customerId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(o => o.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(sort) && sort.Equals("total", StringComparison.OrdinalIgnoreCase))
        {
            query = query.OrderByDescending(o => o.TotalAmount);
        }
        else
        {
            query = query.OrderByDescending(o => o.CreatedAt);
        }

        var items = await query
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .Select(o => new OrderSummaryDto(o.Id, o.CustomerId, o.Status.ToString(), o.CreatedAt, o.CurrencyCode, o.TotalAmount))
            .ToListAsync(ct);

        return Result.Success<IReadOnlyList<OrderSummaryDto>>(items);
    }

    public async Task<Result<OrderDto>> TransitionStatusAsync(Guid id, OrderStatus targetStatus, string idempotencyKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return Result.Failure<OrderDto>("Idempotency key is required.");
        }

        var order = await _dbContext.Orders
            .Include(o => o.LineItems)
            .FirstOrDefaultAsync(o => o.Id == id, ct);

        if (order is null)
        {
            return Result.Failure<OrderDto>("Order was not found.");
        }

        if (!_stateMachine.CanTransition(order.Status, targetStatus))
        {
            return Result.Failure<OrderDto>($"Order status cannot transition from {order.Status} to {targetStatus}.");
        }

        order.Status = targetStatus;
        order.UpdatedAt = DateTime.UtcNow;

        var payload = idempotencyKey + ":" + targetStatus;
        var requestHash = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(payload));

        var record = new IdempotencyRecord
        {
            Id = Guid.NewGuid(),
            Key = idempotencyKey,
            Scope = $"PUT:/orders/{id}/status",
            RequestHash = Convert.ToHexString(requestHash),
            ResponseStatusCode = 200,
            ResponseBody = targetStatus.ToString(),
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.IdempotencyRecords.Add(record);
        await _dbContext.SaveChangesAsync(ct);

        return Result.Success(ToDto(order));
    }

    private static OrderDto ToDto(Order order)
    {
        var items = order.LineItems
            .Select(li => new OrderLineItemDto(li.Id, li.OrderId, li.ProductSku, li.Quantity, li.UnitPrice))
            .ToList();

        return new OrderDto(
            order.Id,
            order.CustomerId,
            order.Status.ToString(),
            order.CreatedAt,
            order.CurrencyCode,
            order.TotalAmount,
            items,
            order.RowVersion,
            order.Notes,
            order.UpdatedAt);
    }
}
