using System.Text;
using System.Text.Json;
using CSharpFunctionalExtensions;
using DataAccessLayer.Contexts;
using DataAccessLayer.Models;
using Microsoft.EntityFrameworkCore;
using OrderManagementApi.Services.Interfaces;
using OrderManagementApi.Services.Models;
using OrderManagement.Messaging.Events;

namespace OrderManagementApi.Services;

public sealed class OrderService(
    AppDbContext dbContext,
    ISadcCurrencyPolicy currencyPolicy,
    IOrderStateMachine stateMachine) : IOrderService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
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

        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            Status = OrderStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            CurrencyCode = currencyCode.ToUpperInvariant(),
            Notes = null,
            UpdatedAt = null
        };

        foreach (var item in request.LineItems)
        {
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
        var createdEvent = new OrderCreatedEvent(Guid.NewGuid(), order.Id, order.CreatedAt);
        _dbContext.OutboxMessages.Add(new OutboxMessage
        {
            Id = createdEvent.MessageId,
            Type = nameof(OrderCreatedEvent),
            Payload = JsonSerializer.Serialize(createdEvent, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            CreatedAt = order.CreatedAt
        });
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
        var scope = $"PUT:/orders/{id}/status";
        var payload = targetStatus.ToString();
        var requestHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
        var existingRecord = await _dbContext.IdempotencyRecords
            .AsNoTracking()
            .SingleOrDefaultAsync(record => record.Key == idempotencyKey && record.Scope == scope, ct);

        if (existingRecord is not null)
        {
            if (!string.Equals(existingRecord.RequestHash, requestHash, StringComparison.Ordinal))
            {
                return Result.Failure<OrderDto>("Idempotency key was already used with a different request.");
            }

            if (string.IsNullOrWhiteSpace(existingRecord.ResponseBody))
            {
                return Result.Failure<OrderDto>("Stored idempotency response could not be replayed.");
            }

            try
            {
                var replay = JsonSerializer.Deserialize<OrderDto>(existingRecord.ResponseBody, JsonOptions);
                return replay is null
                    ? Result.Failure<OrderDto>("Stored idempotency response could not be replayed.")
                    : Result.Success(replay);
            }
            catch (JsonException)
            {
                return Result.Failure<OrderDto>("Stored idempotency response could not be replayed.");
            }
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

        var response = ToDto(order);

        var record = new IdempotencyRecord
        {
            Id = Guid.NewGuid(),
            Key = idempotencyKey,
            Scope = scope,
            RequestHash = requestHash,
            ResponseStatusCode = 200,
            ResponseBody = JsonSerializer.Serialize(response, JsonOptions),
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.IdempotencyRecords.Add(record);
        await _dbContext.SaveChangesAsync(ct);

        return Result.Success(response);
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
