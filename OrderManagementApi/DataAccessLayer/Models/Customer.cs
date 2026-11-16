namespace DataAccessLayer.Models;

public class Customer
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    /// <summary>ISO 3166-1 alpha-2, upper case (validated against the SADC list in the service layer).</summary>
    public string CountryCode { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? PhoneNumber { get; set; }                       // [M3]

    public List<Order> Orders { get; set; } = new();
}
