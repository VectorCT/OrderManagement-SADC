using DataAccessLayer.Contexts;
using DataAccessLayer.Models;
using Microsoft.EntityFrameworkCore;

namespace OrderManagementApi.Tests;

internal sealed class SqliteTestAppDbContext(DbContextOptions<AppDbContext> options)
    : AppDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        // SQLite substitutes SQL Server-specific response storage and rowversion behavior.
        builder.Entity<IdempotencyRecord>()
            .Property(record => record.ResponseBody)
            .HasColumnType("TEXT");
        builder.Entity<Order>()
            .Property(order => order.RowVersion)
            .ValueGeneratedNever()
            .IsConcurrencyToken(false);
    }
}
