using DataAccessLayer.Contexts;
using DataAccessLayer.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrderManagement.Messaging.Events;

namespace OrderManagement.Worker;

public sealed class OrderCreatedHandler(
    AppDbContext dbContext,
    ILogger<OrderCreatedHandler> logger)
{
    private const string ConsumerName = "OrderCreatedConsumer";

    public async Task HandleAsync(OrderCreatedEvent message, CancellationToken cancellationToken)
    {
        if (message.MessageId == Guid.Empty || message.OrderId == Guid.Empty)
        {
            throw new InvalidDataException("OrderCreated requires non-empty message and order identifiers.");
        }

        if (await dbContext.ProcessedMessages.AnyAsync(
                processed => processed.MessageId == message.MessageId && processed.Consumer == ConsumerName,
                cancellationToken))
        {
            logger.LogInformation("Skipping already processed OrderCreated message {MessageId}.", message.MessageId);
            return;
        }

        var order = await dbContext.Orders
            .SingleOrDefaultAsync(candidate => candidate.Id == message.OrderId, cancellationToken)
            ?? throw new InvalidOperationException($"Order {message.OrderId} from the event was not found.");

        if (order.Status is OrderStatus.Pending or OrderStatus.Paid)
        {
            order.Status = OrderStatus.Fulfilled;
            order.UpdatedAt = DateTime.UtcNow;
        }

        dbContext.ProcessedMessages.Add(new ProcessedMessage
        {
            MessageId = message.MessageId,
            Consumer = ConsumerName,
            ProcessedAt = DateTime.UtcNow
        });

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.ChangeTracker.Clear();
            if (await dbContext.ProcessedMessages.AnyAsync(
                    processed => processed.MessageId == message.MessageId && processed.Consumer == ConsumerName,
                    cancellationToken))
            {
                logger.LogInformation("Skipping concurrently processed OrderCreated message {MessageId}.",
                    message.MessageId);
                return;
            }

            throw;
        }
        catch (DbUpdateException exception) when (IsDuplicateKey(exception))
        {
            logger.LogInformation("Skipping concurrently processed OrderCreated message {MessageId}.",
                message.MessageId);
        }
    }

    private static bool IsDuplicateKey(DbUpdateException exception) =>
        exception.GetBaseException() is SqlException { Number: 2601 or 2627 };
}
