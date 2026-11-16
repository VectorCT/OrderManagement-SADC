using DataAccessLayer.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DataAccessLayer.Configurations;

public class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("IdempotencyRecords");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.Key).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Scope).IsRequired().HasMaxLength(200);
        builder.Property(x => x.RequestHash).IsRequired().HasMaxLength(128);
        builder.Property(x => x.ResponseBody).HasColumnType("nvarchar(max)");

        builder.HasIndex(x => new { x.Key, x.Scope })
            .IsUnique()
            .HasDatabaseName("UX_IdempotencyRecords_Key_Scope");
    }
}
