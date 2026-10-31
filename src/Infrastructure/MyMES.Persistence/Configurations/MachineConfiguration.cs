using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyMES.Domain.Machines;

namespace MyMES.Persistence.Configurations;

internal sealed class MachineConfiguration : IEntityTypeConfiguration<Machine>
{
    public void Configure(EntityTypeBuilder<Machine> builder)
    {
        builder.ToTable("Machines", MyMesDbContext.MesSchema);

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.Property(m => m.MachineCode)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(m => m.Name)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(m => m.Description)
            .HasMaxLength(512);

        // Stored as the numeric value: compact, and the name stays free to change in code.
        builder.Property(m => m.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(m => m.StatusChangedAtUtc).IsRequired();
        builder.Property(m => m.CreatedAtUtc).IsRequired();
        builder.Property(m => m.CreatedBy).HasMaxLength(256);
        builder.Property(m => m.UpdatedBy).HasMaxLength(256);

        builder.HasIndex(m => m.MachineCode)
            .IsUnique()
            .HasDatabaseName("IX_Machines_MachineCode");

        // The andon board filters on status constantly.
        builder.HasIndex(m => m.Status)
            .HasDatabaseName("IX_Machines_Status");

        builder.Ignore(m => m.DomainEvents);
    }
}
