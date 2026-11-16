using DataAccessLayer.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DataAccessLayer.Configurations;

public class OrderLineItemConfiguration : IEntityTypeConfiguration<OrderLineItem>
{
    public void Configure(EntityTypeBuilder<OrderLineItem> builder)
    {
        builder.ToTable("OrderLineItems");

        builder.HasKey(li => li.Id);
        builder.Property(li => li.Id).ValueGeneratedOnAdd();

        builder.Property(li => li.ProductSku).IsRequired().HasMaxLength(64);
        builder.Property(li => li.UnitPrice).HasPrecision(18, 2);
        builder.Ignore(li => li.LineTotal);

        builder.HasOne(li => li.Order)
            .WithMany(o => o.LineItems)
            .HasForeignKey(li => li.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(li => li.OrderId)
            .HasDatabaseName("IX_OrderLineItems_OrderId")
            .IncludeProperties(li => new { li.ProductSku, li.Quantity, li.UnitPrice });
    }
}
