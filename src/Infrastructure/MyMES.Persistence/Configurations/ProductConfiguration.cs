using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyMES.Domain.Products;

namespace MyMES.Persistence.Configurations;

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products", MyMesDbContext.MesSchema);

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.ProductCode)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(p => p.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(p => p.Description)
            .HasMaxLength(1000);

        builder.Property(p => p.UnitOfMeasure)
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(p => p.IsActive).IsRequired();
        builder.Property(p => p.CreatedAtUtc).IsRequired();
        builder.Property(p => p.CreatedBy).HasMaxLength(256);
        builder.Property(p => p.UpdatedBy).HasMaxLength(256);

        builder.HasIndex(p => p.ProductCode)
            .IsUnique()
            .HasDatabaseName("IX_Products_ProductCode");

        // Revisions belong to the product: deleting the product takes them with it, and the
        // backing field is used so the aggregate keeps control of its own collection.
        builder.HasMany(p => p.Versions)
            .WithOne(v => v.Product!)
            .HasForeignKey(v => v.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata
            .FindNavigation(nameof(Product.Versions))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(p => p.DomainEvents);
        builder.Ignore(p => p.ReleasedVersion);
    }
}

internal sealed class ProductVersionConfiguration : IEntityTypeConfiguration<ProductVersion>
{
    public void Configure(EntityTypeBuilder<ProductVersion> builder)
    {
        builder.ToTable("ProductVersions", MyMesDbContext.MesSchema);

        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).ValueGeneratedNever();

        builder.Property(v => v.ProductId).IsRequired();

        builder.Property(v => v.VersionNumber)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(v => v.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(v => v.Notes).HasMaxLength(2000);
        builder.Property(v => v.CreatedAtUtc).IsRequired();

        // A revision label is unique within its product, not across the catalogue.
        builder.HasIndex(v => new { v.ProductId, v.VersionNumber })
            .IsUnique()
            .HasDatabaseName("IX_ProductVersions_ProductId_VersionNumber");

        // Production orders look up the released revision of a product constantly.
        builder.HasIndex(v => new { v.ProductId, v.Status })
            .HasDatabaseName("IX_ProductVersions_ProductId_Status");

        builder.Ignore(v => v.DomainEvents);
    }
}
