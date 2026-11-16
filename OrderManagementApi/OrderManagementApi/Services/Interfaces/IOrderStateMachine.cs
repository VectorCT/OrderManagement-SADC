using DataAccessLayer.Models;

namespace OrderManagementApi.Services.Interfaces;

public interface IOrderStateMachine
{
    bool CanTransition(OrderStatus from, OrderStatus to);
}
