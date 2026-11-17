using System.Text.Json;
using DataAccessLayer.Contexts;
using DataAccessLayer.Models;
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
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            Name = "Example Customer",
            Email = "customer@example.com",
            CountryCode = "ZA"
        };
        await using (var seedContext = new AppDbContext(options))
        {
            seedContext.Customers.Add(customer);
            await seedContext.SaveChangesAsync();
        }

        await using var dbContext = new AppDbContext(options);
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
}
