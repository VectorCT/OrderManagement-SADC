using DataAccessLayer.Contexts;
using DataAccessLayer.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OrderManagementApi.Services;

namespace OrderManagementApi.Tests;

public sealed class OrderServiceIdempotencyTests
{
    [Fact]
    public async Task TransitionStatusAsync_ReplaysSuccessfulResponseForSameKeyAndRequest()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        var order = await SeedPendingOrder(options);
        await using var dbContext = new SqliteTestAppDbContext(options);
        var service = new OrderService(dbContext, new SadcCurrencyPolicy(), new OrderStateMachine());

        var firstResult = await service.TransitionStatusAsync(order.Id, OrderStatus.Paid, "payment-1");
        var replayResult = await service.TransitionStatusAsync(order.Id, OrderStatus.Paid, "payment-1");

        Assert.True(firstResult.IsSuccess);
        Assert.True(replayResult.IsSuccess);
        Assert.Equal(firstResult.Value.Id, replayResult.Value.Id);
        Assert.Equal(firstResult.Value.Status, replayResult.Value.Status);
        Assert.Equal(firstResult.Value.UpdatedAt, replayResult.Value.UpdatedAt);
        Assert.Equal(firstResult.Value.TotalAmount, replayResult.Value.TotalAmount);
        Assert.Equal(1, await dbContext.IdempotencyRecords.CountAsync());

        var persistedOrder = await dbContext.Orders.SingleAsync();
        Assert.Equal(OrderStatus.Paid, persistedOrder.Status);
    }

    [Fact]
    public async Task TransitionStatusAsync_RejectsSameKeyWithDifferentRequestWithoutChangingOrder()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        var order = await SeedPendingOrder(options);
        await using var dbContext = new SqliteTestAppDbContext(options);
        var service = new OrderService(dbContext, new SadcCurrencyPolicy(), new OrderStateMachine());

        var firstResult = await service.TransitionStatusAsync(order.Id, OrderStatus.Paid, "payment-2");
        var conflictResult = await service.TransitionStatusAsync(order.Id, OrderStatus.Cancelled, "payment-2");

        Assert.True(firstResult.IsSuccess);
        Assert.True(conflictResult.IsFailure);
        Assert.Contains("different request", conflictResult.Error);
        Assert.Equal(1, await dbContext.IdempotencyRecords.CountAsync());

        var persistedOrder = await dbContext.Orders.SingleAsync();
        Assert.Equal(OrderStatus.Paid, persistedOrder.Status);
    }

    private static async Task<Order> SeedPendingOrder(DbContextOptions<AppDbContext> options)
    {
        await using var context = new SqliteTestAppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            Name = "Example Customer",
            Email = "customer@example.com",
            CountryCode = "ZA"
        };
        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            Customer = customer,
            CurrencyCode = "ZAR",
            CreatedAt = DateTime.UtcNow,
            Status = OrderStatus.Pending
        };

        context.Orders.Add(order);
        await context.SaveChangesAsync();
        return order;
    }
}
