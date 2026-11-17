namespace DataAccessLayer.Models;

public class OutboxMessage
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PublishedAt { get; set; }
    public DateTime? NextAttemptAt { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
}
