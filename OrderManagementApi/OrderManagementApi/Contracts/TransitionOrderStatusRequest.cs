using DataAccessLayer.Models;

namespace OrderManagementApi.Contracts;

public sealed record TransitionOrderStatusRequest(OrderStatus Status);