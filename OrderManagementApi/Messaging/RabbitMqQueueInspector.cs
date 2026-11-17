using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace OrderManagement.Messaging;

public sealed class RabbitMqQueueInspector(
    RabbitMqOptions options,
    ILogger<RabbitMqQueueInspector> logger) : IRabbitMqQueueInspector
{
    public async Task<RabbitMqStatus> GetStatusAsync(CancellationToken cancellationToken)
    {
        IConnection connection;
        try
        {
            connection = await RabbitMqConnectionFactory.Create(options).CreateConnectionAsync(cancellationToken);
        }
        catch (BrokerUnreachableException exception)
        {
            logger.LogWarning(exception, "RabbitMQ is unreachable while collecting messaging diagnostics.");
            return new RabbitMqStatus(false, "RabbitMQ is unreachable.", []);
        }

        await using (connection)
        {
            string[] queueNames =
            [
                RabbitMqTopology.OrderCreatedQueue,
                .. RabbitMqTopology.RetryQueues.Select(queue => queue.QueueName),
                RabbitMqTopology.DeadLetterQueue
            ];
            var queues = new List<RabbitMqQueueStatus>(queueNames.Length);

            foreach (var queueName in queueNames)
            {
                await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
                try
                {
                    var declaration = await channel.QueueDeclarePassiveAsync(queueName, cancellationToken);
                    queues.Add(new RabbitMqQueueStatus(
                        queueName,
                        true,
                        declaration.MessageCount,
                        declaration.ConsumerCount));
                }
                catch (OperationInterruptedException exception) when (exception.ShutdownReason?.ReplyCode == 404)
                {
                    queues.Add(new RabbitMqQueueStatus(queueName, false, null, null));
                }
            }

            return new RabbitMqStatus(true, null, queues);
        }
    }
}
