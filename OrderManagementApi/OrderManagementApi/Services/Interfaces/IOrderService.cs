using CSharpFunctionalExtensions;
using DataAccessLayer.Models;
using OrderManagementApi.Services.Models;

namespace OrderManagementApi.Services.Interfaces;

public interface IOrderService
{
    Task<Result<OrderDto>> CreateAsync(CreateOrderRequest request, CancellationToken ct = default);
    Task<Result<OrderDto>> GetAsync(Guid id, CancellationToken ct = default);
    Task<Result<IReadOnlyList<OrderSummaryDto>>> ListAsync(Guid? customerId, OrderStatus? status, int page, int pageSize, string? sort, CancellationToken ct = default);
    Task<Result<OrderDto>> TransitionStatusAsync(Guid id, OrderStatus targetStatus, string idempotencyKey, CancellationToken ct = default);
}
