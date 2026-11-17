using System.Text.Json;
using Microsoft.Extensions.Logging;
using OrderManagement.Messaging.Events;
using RabbitMQ.Client;

namespace OrderManagement.Messaging;

public sealed class RabbitMqOrderEventPublisher(
    RabbitMqOptions options,
    ILogger<RabbitMqOrderEventPublisher> logger) : IOrderEventPublisher, IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim _publishLock = new(1, 1);
    private readonly ConnectionFactory _connectionFactory = RabbitMqConnectionFactory.Create(options);

    private IConnection? _connection;
    private IChannel? _channel;

    public async Task PublishOrderCreatedAsync(OrderCreatedEvent message, CancellationToken cancellationToken)
    {
        await _publishLock.WaitAsync(cancellationToken);
        try
        {
            await EnsureChannelAsync(cancellationToken);
            var body = JsonSerializer.SerializeToUtf8Bytes(message, JsonOptions);
            var properties = new BasicProperties
            {
                ContentType = "application/json",
                DeliveryMode = DeliveryModes.Persistent,
                MessageId = message.MessageId.ToString(),
                Type = nameof(OrderCreatedEvent)
            };

            await _channel!.BasicPublishAsync(
                RabbitMqTopology.EventsExchange,
                RabbitMqTopology.OrderCreatedRoutingKey,
                mandatory: true,
                basicProperties: properties,
                body,
                cancellationToken);

            logger.LogInformation("Published OrderCreated message {MessageId} for order {OrderId}.",
                message.MessageId, message.OrderId);
        }
        catch
        {
            await ResetConnectionAsync();
            throw;
        }
        finally
        {
            _publishLock.Release();
        }
    }

    private async Task EnsureChannelAsync(CancellationToken cancellationToken)
    {
        if (_channel?.IsOpen == true && _connection?.IsOpen == true)
        {
            return;
        }

        await ResetConnectionAsync();
        _connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        _channel = await _connection.CreateChannelAsync(
            new CreateChannelOptions(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true),
            cancellationToken);
        await RabbitMqTopology.DeclareAsync(_channel, cancellationToken);
    }

    private async Task ResetConnectionAsync()
    {
        if (_channel is not null)
        {
            await _channel.DisposeAsync();
            _channel = null;
        }

        if (_connection is not null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _publishLock.WaitAsync();
        try
        {
            await ResetConnectionAsync();
        }
        finally
        {
            _publishLock.Release();
            _publishLock.Dispose();
        }
    }
}
