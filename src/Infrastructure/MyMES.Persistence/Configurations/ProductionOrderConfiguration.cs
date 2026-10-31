using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyMES.Domain.Production;
using MyMES.Domain.Products;

namespace MyMES.Persistence.Configurations;

internal sealed class ProductionOrderConfiguration : IEntityTypeConfiguration<ProductionOrder>
{
    public void Configure(EntityTypeBuilder<ProductionOrder> builder)
    {
        builder.ToTable("ProductionOrders", MyMesDbContext.MesSchema);

        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();

        builder.Property(o => o.OrderNumber)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(o => o.ProductCode)
            .HasMaxLength(64)
            .IsRequired();

        // Quantities are decimal, not float: four decimals cover piece counts and weights alike
        // without the rounding surprises binary floating point would bring to a stock ledger.
        builder.Property(o => o.PlannedQuantity)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(o => o.ProducedQuantity)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(o => o.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(o => o.PlannedStartUtc).IsRequired();
        builder.Property(o => o.PlannedEndUtc).IsRequired();
        builder.Property(o => o.CreatedAtUtc).IsRequired();
        builder.Property(o => o.CreatedBy).HasMaxLength(256);
        builder.Property(o => o.UpdatedBy).HasMaxLength(256);

        builder.HasIndex(o => o.OrderNumber)
            .IsUnique()
            .HasDatabaseName("IX_ProductionOrders_OrderNumber");

        // The work list is "open orders by planned start", so index that pair together.
        builder.HasIndex(o => new { o.Status, o.PlannedStartUtc })
            .HasDatabaseName("IX_ProductionOrders_Status_PlannedStartUtc");

        builder.HasIndex(o => o.ProductCode)
            .HasDatabaseName("IX_ProductionOrders_ProductCode");

        // Restrict, not cascade: a revision that an order was built against must never disappear,
        // or the as-built record loses the very thing it exists to prove.
        builder.HasOne<ProductVersion>()
            .WithMany()
            .HasForeignKey(o => o.ProductVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Ignore(o => o.DomainEvents);
        builder.Ignore(o => o.CompletionPercentage);
        builder.Ignore(o => o.IsClosed);
    }
}
