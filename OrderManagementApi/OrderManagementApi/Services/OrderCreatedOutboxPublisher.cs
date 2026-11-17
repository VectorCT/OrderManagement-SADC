using System.Text.Json;
using DataAccessLayer.Contexts;
using Microsoft.EntityFrameworkCore;
using OrderManagement.Messaging;
using OrderManagement.Messaging.Events;

namespace OrderManagementApi.Services;

public sealed class OrderCreatedOutboxPublisher(
    IServiceScopeFactory scopeFactory,
    IOrderEventPublisher eventPublisher,
    ILogger<OrderCreatedOutboxPublisher> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const int BatchSize = 20;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var publishedAny = await PublishBatchAsync(stoppingToken);
                if (!publishedAny)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to process the order-created outbox batch.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task<bool> PublishBatchAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTime.UtcNow;
        var messages = await dbContext.OutboxMessages
            .Where(message =>
                message.PublishedAt == null &&
                (message.NextAttemptAt == null || message.NextAttemptAt <= now))
            .OrderBy(message => message.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        foreach (var outboxMessage in messages)
        {
            try
            {
                if (outboxMessage.Type != nameof(OrderCreatedEvent))
                {
                    throw new InvalidDataException($"Unsupported outbox message type '{outboxMessage.Type}'.");
                }

                var createdEvent = JsonSerializer.Deserialize<OrderCreatedEvent>(outboxMessage.Payload, JsonOptions)
                    ?? throw new InvalidDataException("The order-created outbox payload was empty.");
                await eventPublisher.PublishOrderCreatedAsync(createdEvent, cancellationToken);

                outboxMessage.PublishedAt = DateTime.UtcNow;
                outboxMessage.NextAttemptAt = null;
                outboxMessage.LastError = null;
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                outboxMessage.Attempts++;
                var delaySeconds = Math.Min(300, Math.Pow(2, Math.Min(outboxMessage.Attempts - 1, 8)));
                outboxMessage.NextAttemptAt = DateTime.UtcNow.AddSeconds(delaySeconds);
                outboxMessage.LastError = exception.Message.Length <= 2000
                    ? exception.Message
                    : exception.Message[..2000];
                await dbContext.SaveChangesAsync(cancellationToken);
                logger.LogError(exception,
                    "Could not publish outbox message {MessageId}; retry {Attempt} is scheduled for {NextAttemptAt}.",
                    outboxMessage.Id, outboxMessage.Attempts, outboxMessage.NextAttemptAt);
            }
        }

        return messages.Count > 0;
    }
}
