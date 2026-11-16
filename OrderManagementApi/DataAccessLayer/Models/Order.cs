namespace DataAccessLayer.Models;

public enum OrderStatus
{
    Pending = 0,
    Paid = 1,
    Fulfilled = 2,
    Cancelled = 3
}

public class Order
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>ISO 4217, upper case. Must be permitted for the customer's country (service layer).</summary>
    public string CurrencyCode { get; set; } = string.Empty;

    /// <summary>Always computed server-side from the line items. Never accepted from clients.</summary>
    public decimal TotalAmount { get; set; }

    public List<OrderLineItem> LineItems { get; set; } = new();

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();  // [M2] optimistic concurrency

    public string? Notes { get; set; }                             // [M3]
    public DateTime? UpdatedAt { get; set; }                       // [M3]

    /// <summary>Σ(Quantity × UnitPrice), rounded to 2 dp (banker's rounding to match decimal(18,2)).</summary>
    public void RecalculateTotal() =>
        TotalAmount = Math.Round(LineItems.Sum(li => li.Quantity * li.UnitPrice), 2, MidpointRounding.ToEven);
}
