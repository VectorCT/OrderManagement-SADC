namespace DataAccessLayer.Models;

/// <summary>Stores the outcome of an idempotent request (e.g. PUT /orders/{id}/status) so retries replay it.</summary>
public class IdempotencyRecord
{
    public Guid Id { get; set; }
    public string Key { get; set; } = string.Empty;        // Idempotency-Key header value
    public string Scope { get; set; } = string.Empty;      // e.g. "PUT:/api/orders/{id}/status"
    public string RequestHash { get; set; } = string.Empty; // SHA-256 of the request payload
    public int ResponseStatusCode { get; set; }
    public string? ResponseBody { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
