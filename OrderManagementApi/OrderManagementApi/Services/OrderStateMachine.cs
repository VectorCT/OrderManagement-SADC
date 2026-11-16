using DataAccessLayer.Models;
using OrderManagementApi.Services.Interfaces;

namespace OrderManagementApi.Services;

public sealed class OrderStateMachine : IOrderStateMachine
{
    private static readonly Dictionary<OrderStatus, HashSet<OrderStatus>> ValidTransitions = new()
    {
        [OrderStatus.Pending] = new() { OrderStatus.Paid, OrderStatus.Cancelled },
        [OrderStatus.Paid] = new() { OrderStatus.Fulfilled, OrderStatus.Cancelled },
        [OrderStatus.Fulfilled] = new(),
        [OrderStatus.Cancelled] = new()
    };

    public bool CanTransition(OrderStatus from, OrderStatus to)
    {
        if (!ValidTransitions.TryGetValue(from, out var allowed))
        {
            return false;
        }

        return allowed.Contains(to);
    }
}
