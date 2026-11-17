using DataAccessLayer.Contexts;
using DataAccessLayer.Models;
using Microsoft.EntityFrameworkCore;
using OrderManagement.Messaging;
using OrderManagementApi.Contracts;
using OrderManagementApi.Services.Interfaces;

namespace OrderManagementApi.Services;

public sealed class MessagingDiagnosticsService(
    AppDbContext dbContext,
    IRabbitMqQueueInspector queueInspector) : IMessagingDiagnosticsService
{
    private const int RecentMessageLimit = 50;

    public async Task<MessagingDiagnosticsResponse> GetAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var since = now.AddHours(-24);

        var orderStatusCounts = await dbContext.Orders
            .AsNoTracking()
            .GroupBy(order => order.Status)
            .Select(group => new { Status = group.Key, Count = group.LongCount() })
            .ToListAsync(cancellationToken);

        var ordersCreatedLast24Hours = await dbContext.Orders
            .AsNoTracking()
            .LongCountAsync(order => order.CreatedAt >= since, cancellationToken);

        var pendingOutboxCount = await dbContext.OutboxMessages
            .AsNoTracking()
            .LongCountAsync(message =>
                message.PublishedAt == null && message.NextAttemptAt == null,
                cancellationToken);
        var retryScheduledOutboxCount = await dbContext.OutboxMessages
            .AsNoTracking()
            .LongCountAsync(message =>
                message.PublishedAt == null && message.NextAttemptAt != null,
                cancellationToken);
        var publishedOutboxCount = await dbContext.OutboxMessages
            .AsNoTracking()
            .LongCountAsync(message => message.PublishedAt != null, cancellationToken);

        var recentOutboxMessages = await dbContext.OutboxMessages
            .AsNoTracking()
            .OrderByDescending(message => message.CreatedAt)
            .Take(RecentMessageLimit)
            .Select(message => new OutboxMessageResponse(
                message.Id,
                message.Type,
                message.Payload,
                ToDateTimeOffset(message.CreatedAt),
                message.PublishedAt.HasValue ? ToDateTimeOffset(message.PublishedAt.Value) : null,
                message.NextAttemptAt.HasValue ? ToDateTimeOffset(message.NextAttemptAt.Value) : null,
                message.Attempts,
                message.LastError))
            .ToListAsync(cancellationToken);

        var processedMessageCount = await dbContext.ProcessedMessages
            .AsNoTracking()
            .LongCountAsync(cancellationToken);
        var recentProcessedMessages = await dbContext.ProcessedMessages
            .AsNoTracking()
            .OrderByDescending(message => message.ProcessedAt)
            .Take(RecentMessageLimit)
            .Select(message => new ProcessedMessageResponse(
                message.MessageId,
                message.Consumer,
                ToDateTimeOffset(message.ProcessedAt)))
            .ToListAsync(cancellationToken);

        var rabbitMqStatus = await queueInspector.GetStatusAsync(cancellationToken);

        return new MessagingDiagnosticsResponse(
            ToDateTimeOffset(now),
            ordersCreatedLast24Hours,
            orderStatusCounts
                .OrderBy(item => item.Status)
                .Select(item => new OrderStatusCountResponse(item.Status.ToString(), item.Count))
                .ToList(),
            new OutboxSummaryResponse(
                pendingOutboxCount,
                retryScheduledOutboxCount,
                publishedOutboxCount,
                recentOutboxMessages),
            processedMessageCount,
            recentProcessedMessages,
            new RabbitMqDiagnosticsResponse(
                rabbitMqStatus.IsReachable,
                rabbitMqStatus.Error,
                rabbitMqStatus.Queues
                    .Select(queue => new RabbitMqQueueResponse(
                        queue.Name,
                        queue.Exists,
                        queue.ReadyMessages,
                        queue.Consumers))
                    .ToList()));
    }

    private static DateTimeOffset ToDateTimeOffset(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
