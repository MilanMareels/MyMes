using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyMES.Domain.Products;

namespace MyMES.Infrastructure.Configurations;

internal sealed class BillOfMaterialItemConfiguration : IEntityTypeConfiguration<BillOfMaterialItem>
{
    public void Configure(EntityTypeBuilder<BillOfMaterialItem> builder)
    {
        builder.ToTable("BillOfMaterialItems", MyMesDbContext.MesSchema);

        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).ValueGeneratedNever();

        builder.Property(b => b.ProductVersionId).IsRequired();
        builder.Property(b => b.ComponentProductId).IsRequired();

        builder.Property(b => b.Quantity)
            .HasPrecision(18, 4) // Geschikt voor gewichten/fracties
            .IsRequired();

        // Relatie naar het component Product dat verbruikt wordt.
        // Restrict voorkomt dat het verwijderen van een basis-product per ongeluk historische BOM's verwijdert.
        builder.HasOne(b => b.ComponentProduct)
            .WithMany()
            .HasForeignKey(b => b.ComponentProductId)
            .OnDelete(DeleteBehavior.Restrict);

        // Optionele relatie naar de RoutingStep
        builder.HasOne(b => b.RoutingStep)
            .WithMany(r => r.BomItems)
            .HasForeignKey(b => b.RoutingStepId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Ignore(b => b.DomainEvents);
    }
}