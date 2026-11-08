using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyMES.Domain.Products;

namespace MyMES.Infrastructure.Configurations;

internal sealed class RoutingStepConfiguration : IEntityTypeConfiguration<RoutingStep>
{
    public void Configure(EntityTypeBuilder<RoutingStep> builder)
    {
        builder.ToTable("RoutingSteps", MyMesDbContext.MesSchema);

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.ProductVersionId).IsRequired();
        builder.Property(r => r.Sequence).IsRequired();

        builder.Property(r => r.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(r => r.Description)
            .HasMaxLength(1000);

        // Unieke sequence per revisie (bijv. stap 10, 20, 30...)
        builder.HasIndex(r => new { r.ProductVersionId, r.Sequence })
            .IsUnique()
            .HasDatabaseName("IX_RoutingSteps_ProductVersionId_Sequence");

        // 1-op-Veel: RoutingStep -> BomItems (Materialen gekoppeld aan deze stap)
        // Belangrijk: Restrict gebruiken om meerdere cascade-paden te voorkomen!
        builder.HasMany(r => r.BomItems)
            .WithOne(b => b.RoutingStep)
            .HasForeignKey(b => b.RoutingStepId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Ignore(r => r.DomainEvents);
    }
}