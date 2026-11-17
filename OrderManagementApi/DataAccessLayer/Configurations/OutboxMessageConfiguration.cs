using DataAccessLayer.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DataAccessLayer.Configurations;

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");
        builder.HasKey(message => message.Id);

        builder.Property(message => message.Type).IsRequired().HasMaxLength(200);
        builder.Property(message => message.Payload).IsRequired();
        builder.Property(message => message.CreatedAt).IsRequired();
        builder.Property(message => message.Attempts).IsRequired();
        builder.Property(message => message.LastError).HasMaxLength(2000);

        builder.HasIndex(message => new { message.PublishedAt, message.NextAttemptAt, message.CreatedAt })
            .HasDatabaseName("IX_OutboxMessages_PublishedAt_NextAttemptAt_CreatedAt");
    }
}
