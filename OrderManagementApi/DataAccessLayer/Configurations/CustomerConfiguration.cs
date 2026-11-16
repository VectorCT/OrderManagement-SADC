using DataAccessLayer.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DataAccessLayer.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedOnAdd();

        builder.Property(c => c.Name).IsRequired().HasMaxLength(200);
        builder.Property(c => c.Email).IsRequired().HasMaxLength(320);
        builder.Property(c => c.CountryCode).IsRequired().HasMaxLength(2).IsFixedLength().IsUnicode(false);
        builder.Property(c => c.PhoneNumber).HasMaxLength(30);
        builder.Property(c => c.CreatedAt).IsRequired();

        builder.HasIndex(c => c.Email)
            .IsUnique()
            .HasDatabaseName("UX_Customers_Email");

        builder.HasIndex(c => c.Name)
            .HasDatabaseName("IX_Customers_Name");
    }
}
