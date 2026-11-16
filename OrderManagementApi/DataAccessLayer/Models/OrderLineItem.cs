namespace DataAccessLayer.Models;

public class OrderLineItem
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Order? Order { get; set; }

    public string ProductSku { get; set; } = string.Empty;
    public int Quantity { get; set; }          // > 0
    public decimal UnitPrice { get; set; }     // >= 0, in the order's currency

    public decimal LineTotal => Quantity * UnitPrice;   // not mapped
}
