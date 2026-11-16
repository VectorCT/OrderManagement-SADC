namespace OrderManagementApi.Services.Models;

public sealed record CreateOrderLineItemRequest(string ProductSku, int Quantity, decimal UnitPrice);

public sealed record CreateOrderRequest(Guid CustomerId, string CurrencyCode, List<CreateOrderLineItemRequest> LineItems);

public sealed record OrderLineItemDto(
    Guid Id,
    Guid OrderId,
    string ProductSku,
    int Quantity,
    decimal UnitPrice);

public sealed record OrderSummaryDto(
    Guid Id,
    Guid CustomerId,
    string Status,
    DateTime CreatedAt,
    string CurrencyCode,
    decimal TotalAmount);

public sealed record OrderDto(
    Guid Id,
    Guid CustomerId,
    string Status,
    DateTime CreatedAt,
    string CurrencyCode,
    decimal TotalAmount,
    IReadOnlyList<OrderLineItemDto> LineItems,
    byte[] RowVersion,
    string? Notes,
    DateTime? UpdatedAt);
