using Microsoft.EntityFrameworkCore;
using SampleApp.Models;

namespace SampleApp.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("Orders");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ProductCode).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Quantity).IsRequired();
            entity.Property(x => x.UnitPrice).HasPrecision(18, 2).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
        });
    }
}
