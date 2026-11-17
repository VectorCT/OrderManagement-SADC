using System.Text.Json;
using DataAccessLayer.Contexts;
using DataAccessLayer.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OrderManagement.Messaging.Events;
using OrderManagementApi.Services;
using OrderManagementApi.Services.Models;

namespace OrderManagementApi.Tests;

public sealed class OrderServiceOutboxTests
{
    [Fact]
    public async Task CreateAsync_PersistsOrderAndOutboxEventTogether()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            Name = "Example Customer",
            Email = "customer@example.com",
            CountryCode = "ZA"
        };
        await using (var seedContext = new SqliteTestAppDbContext(options))
        {
            await seedContext.Database.EnsureCreatedAsync();
            seedContext.Customers.Add(customer);
            await seedContext.SaveChangesAsync();
        }

        await using var dbContext = new SqliteTestAppDbContext(options);
        var service = new OrderService(dbContext, new SadcCurrencyPolicy(), new OrderStateMachine());
        var result = await service.CreateAsync(new CreateOrderRequest(
            customer.Id,
            "ZAR",
            [new CreateOrderLineItemRequest("SKU-1", 2, 15.50m)]));

        Assert.True(result.IsSuccess);
        var order = await dbContext.Orders.SingleAsync();
        var outboxMessage = await dbContext.OutboxMessages.SingleAsync();
        var createdEvent = JsonSerializer.Deserialize<OrderCreatedEvent>(
            outboxMessage.Payload,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Equal(order.Id, result.Value.Id);
        Assert.Equal(nameof(OrderCreatedEvent), outboxMessage.Type);
        Assert.Equal(outboxMessage.Id, createdEvent!.MessageId);
        Assert.Equal(order.Id, createdEvent.OrderId);
        Assert.Null(outboxMessage.PublishedAt);
    }

    [Fact]
    public async Task CreateAsync_WhenOutboxInsertFails_RollsBackOrderInsert()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var dbContext = new SqliteTestAppDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            Name = "Example Customer",
            Email = "customer@example.com",
            CountryCode = "ZA"
        };
        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync();
        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE TRIGGER FailOutboxInsert BEFORE INSERT ON OutboxMessages BEGIN SELECT RAISE(ABORT, 'outbox unavailable'); END;");

        var service = new OrderService(dbContext, new SadcCurrencyPolicy(), new OrderStateMachine());

        await Assert.ThrowsAsync<DbUpdateException>(() => service.CreateAsync(new CreateOrderRequest(
            customer.Id,
            "ZAR",
            [new CreateOrderLineItemRequest("SKU-1", 1, 10m)])));

        Assert.Equal(0, await dbContext.Orders.CountAsync());
        Assert.Equal(0, await dbContext.OutboxMessages.CountAsync());
    }
}
