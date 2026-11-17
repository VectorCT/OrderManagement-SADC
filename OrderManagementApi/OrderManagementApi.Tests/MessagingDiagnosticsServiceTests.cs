using DataAccessLayer.Contexts;
using DataAccessLayer.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using OrderManagement.Messaging;
using OrderManagementApi.Services;

namespace OrderManagementApi.Tests;

public sealed class MessagingDiagnosticsServiceTests
{
    [Fact]
    public async Task GetAsync_ReturnsOrderOutboxProcessedAndQueueStatus()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var orderId = Guid.NewGuid();
        var messageId = Guid.NewGuid();

        await using (var seedContext = new AppDbContext(options))
        {
            seedContext.Orders.Add(new Order
            {
                Id = orderId,
                CustomerId = Guid.NewGuid(),
                Status = OrderStatus.Pending,
                CreatedAt = DateTime.UtcNow
            });
            seedContext.OutboxMessages.AddRange(
                new OutboxMessage { Id = messageId, Type = "OrderCreatedEvent", Payload = "{}" },
                new OutboxMessage
                {
                    Id = Guid.NewGuid(),
                    Type = "OrderCreatedEvent",
                    Payload = "{}",
                    Attempts = 1,
                    NextAttemptAt = DateTime.UtcNow.AddSeconds(5)
                },
                new OutboxMessage
                {
                    Id = Guid.NewGuid(),
                    Type = "OrderCreatedEvent",
                    Payload = "{}",
                    PublishedAt = DateTime.UtcNow
                });
            seedContext.ProcessedMessages.Add(new ProcessedMessage
            {
                MessageId = messageId,
                Consumer = "OrderCreatedConsumer",
                ProcessedAt = DateTime.UtcNow
            });
            await seedContext.SaveChangesAsync();
        }

        var rabbitStatus = new RabbitMqStatus(true, null,
        [
            new RabbitMqQueueStatus("orders.order-created", true, 2, 1),
            new RabbitMqQueueStatus("orders.order-created.dead-letter", true, 1, 0)
        ]);
        var inspector = new StubRabbitMqQueueInspector(rabbitStatus);
        await using var dbContext = new AppDbContext(options);
        var service = new MessagingDiagnosticsService(dbContext, inspector);

        var diagnostics = await service.GetAsync();

        Assert.Equal(1, diagnostics.OrdersCreatedLast24Hours);
        Assert.Contains(diagnostics.OrdersByStatus, status =>
            status.Status == nameof(OrderStatus.Pending) && status.Count == 1);
        Assert.Equal(1, diagnostics.Outbox.PendingCount);
        Assert.Equal(1, diagnostics.Outbox.RetryScheduledCount);
        Assert.Equal(1, diagnostics.Outbox.PublishedCount);
        Assert.Equal(3, diagnostics.Outbox.RecentMessages.Count);
        Assert.Equal(1, diagnostics.ProcessedMessageCount);
        Assert.Equal(messageId, Assert.Single(diagnostics.RecentProcessedMessages).MessageId);
        Assert.True(diagnostics.RabbitMq.IsReachable);
        Assert.Equal(1u, diagnostics.RabbitMq.Queues.Single(queue => queue.Name.EndsWith("dead-letter")).ReadyMessages);
    }

    private sealed class StubRabbitMqQueueInspector(RabbitMqStatus status) : IRabbitMqQueueInspector
    {
        public Task<RabbitMqStatus> GetStatusAsync(CancellationToken cancellationToken) =>
            Task.FromResult(status);
    }
}
