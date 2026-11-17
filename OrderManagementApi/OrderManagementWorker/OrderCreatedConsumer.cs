using System.Text;
using System.Text.Json;
using DataAccessLayer.Contexts;
using Microsoft.EntityFrameworkCore;
using OrderManagement.Messaging;
using OrderManagement.Messaging.Events;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace OrderManagement.Worker;

public sealed class OrderCreatedConsumer(
    RabbitMqOptions options,
    IServiceScopeFactory scopeFactory,
    ILogger<OrderCreatedConsumer> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const int MaximumRetries = 3;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var connectionFactory = RabbitMqConnectionFactory.Create(options);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var connection = await connectionFactory.CreateConnectionAsync(stoppingToken);
                await using var channel = await connection.CreateChannelAsync(
                    new CreateChannelOptions(
                        publisherConfirmationsEnabled: true,
                        publisherConfirmationTrackingEnabled: true,
                        consumerDispatchConcurrency: 1),
                    stoppingToken);
                await RabbitMqTopology.DeclareAsync(channel, stoppingToken);
                await channel.BasicQosAsync(0, 1, global: false, stoppingToken);

                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += (_, delivery) =>
                    ProcessDeliveryAsync(channel, delivery, stoppingToken);

                var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                consumer.ShutdownAsync += (_, _) =>
                {
                    stopped.TrySetResult();
                    return Task.CompletedTask;
                };
                connection.ConnectionShutdownAsync += (_, _) =>
                {
                    stopped.TrySetResult();
                    return Task.CompletedTask;
                };

                await channel.BasicConsumeAsync(
                    RabbitMqTopology.OrderCreatedQueue,
                    autoAck: false,
                    consumer,
                    stoppingToken);

                logger.LogInformation("Consuming OrderCreated messages from {Queue}.",
                    RabbitMqTopology.OrderCreatedQueue);
                await stopped.Task.WaitAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "RabbitMQ consumer stopped; reconnecting in five seconds.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task ProcessDeliveryAsync(
        IChannel channel,
        BasicDeliverEventArgs delivery,
        CancellationToken cancellationToken)
    {
        var body = delivery.Body.ToArray();
        try
        {
            var message = JsonSerializer.Deserialize<OrderCreatedEvent>(body, JsonOptions)
                ?? throw new InvalidDataException("The OrderCreated message body was empty.");

            await using var scope = scopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<OrderCreatedHandler>();
            await handler.HandleAsync(message, cancellationToken);
            await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            var retryCount = ReadRetryCount(delivery.BasicProperties.Headers);
            if (retryCount >= MaximumRetries)
            {
                logger.LogError(exception,
                    "OrderCreated message {MessageId} exhausted {RetryCount} retries and will be dead-lettered.",
                    delivery.BasicProperties.MessageId, retryCount);
                await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: false, cancellationToken);
                return;
            }

            try
            {
                await PublishRetryAsync(channel, delivery, body, retryCount + 1, cancellationToken);
                await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, cancellationToken);
                logger.LogWarning(exception,
                    "OrderCreated message {MessageId} failed; retry {RetryCount} was scheduled.",
                    delivery.BasicProperties.MessageId, retryCount + 1);
            }
            catch (Exception publishException) when (publishException is not OperationCanceledException)
            {
                logger.LogError(publishException,
                    "Could not enqueue retry for message {MessageId}; returning the original delivery to the queue.",
                    delivery.BasicProperties.MessageId);
                await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: true, cancellationToken);
            }
        }
    }

    private static async Task PublishRetryAsync(
        IChannel channel,
        BasicDeliverEventArgs delivery,
        byte[] body,
        int retryCount,
        CancellationToken cancellationToken)
    {
        var retryQueue = RabbitMqTopology.RetryQueues[retryCount - 1];
        var properties = new BasicProperties
        {
            ContentType = delivery.BasicProperties.ContentType,
            DeliveryMode = DeliveryModes.Persistent,
            MessageId = delivery.BasicProperties.MessageId,
            Type = delivery.BasicProperties.Type,
            Headers = new Dictionary<string, object?>
            {
                ["x-retry-count"] = retryCount
            }
        };

        await channel.BasicPublishAsync(
            RabbitMqTopology.RetryExchange,
            retryQueue.RoutingKey,
            mandatory: true,
            basicProperties: properties,
            body,
            cancellationToken);
    }

    private static int ReadRetryCount(IDictionary<string, object?>? headers)
    {
        if (headers is null || !headers.TryGetValue("x-retry-count", out var value))
        {
            return 0;
        }

        return value switch
        {
            int count => count,
            long count when count is >= 0 and <= int.MaxValue => (int)count,
            byte[] bytes when int.TryParse(Encoding.UTF8.GetString(bytes), out var count) => count,
            _ => 0
        };
    }
}
