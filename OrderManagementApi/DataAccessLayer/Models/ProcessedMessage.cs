namespace DataAccessLayer.Models;

/// <summary>Consumer-side de-duplication: one row per (message, consumer) already handled.</summary>
public class ProcessedMessage
{
    public Guid MessageId { get; set; }
    public string Consumer { get; set; } = string.Empty;   // e.g. "OrderCreatedConsumer"
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
}
