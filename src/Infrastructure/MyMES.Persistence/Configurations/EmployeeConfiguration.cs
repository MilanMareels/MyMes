using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyMES.Domain.Personnel;

namespace MyMES.Persistence.Configurations;

internal sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("Employees", MyMesDbContext.MesSchema);

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.EmployeeNumber)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(e => e.FirstName).HasMaxLength(128).IsRequired();
        builder.Property(e => e.LastName).HasMaxLength(128).IsRequired();
        builder.Property(e => e.IsActive).IsRequired();
        builder.Property(e => e.CreatedAtUtc).IsRequired();
        builder.Property(e => e.CreatedBy).HasMaxLength(256);
        builder.Property(e => e.UpdatedBy).HasMaxLength(256);

        // Nullable string key matching the Identity user id. No foreign key is declared, because
        // the domain must not take a dependency on the Identity schema; the link is by value and
        // is filtered on often enough to earn an index.
        builder.Property(e => e.IdentityUserId).HasMaxLength(450);

        builder.HasIndex(e => e.EmployeeNumber)
            .IsUnique()
            .HasDatabaseName("IX_Employees_EmployeeNumber");

        builder.HasIndex(e => e.IdentityUserId)
            .HasDatabaseName("IX_Employees_IdentityUserId")
            .HasFilter("[IdentityUserId] IS NOT NULL");

        builder.Ignore(e => e.DomainEvents);
        builder.Ignore(e => e.FullName);
    }
}
