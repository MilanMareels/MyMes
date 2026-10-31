using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyMES.Identity;

namespace MyMES.Persistence.Configurations;

/// <summary>
/// Gives the Identity tables explicit names in their own schema.
/// </summary>
/// <remarks>
/// The framework defaults (<c>AspNetUsers</c> and friends) leak the framework's name into the
/// database. Naming them here keeps the schema readable and groups them away from the MES tables.
/// </remarks>
internal sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.ToTable("Users", MyMesDbContext.IdentitySchema);

        builder.Property(u => u.DisplayName).HasMaxLength(256);
        builder.Property(u => u.IsActive).IsRequired();
        builder.Property(u => u.CreatedAtUtc).IsRequired();

        builder.HasIndex(u => u.EmployeeId)
            .HasDatabaseName("IX_Users_EmployeeId")
            .HasFilter("[EmployeeId] IS NOT NULL");
    }
}

internal sealed class ApplicationRoleConfiguration : IEntityTypeConfiguration<ApplicationRole>
{
    public void Configure(EntityTypeBuilder<ApplicationRole> builder)
    {
        builder.ToTable("Roles", MyMesDbContext.IdentitySchema);
        builder.Property(r => r.Description).HasMaxLength(512);
    }
}

internal sealed class IdentityUserRoleConfiguration : IEntityTypeConfiguration<IdentityUserRole<string>>
{
    public void Configure(EntityTypeBuilder<IdentityUserRole<string>> builder) =>
        builder.ToTable("UserRoles", MyMesDbContext.IdentitySchema);
}

internal sealed class IdentityUserClaimConfiguration : IEntityTypeConfiguration<IdentityUserClaim<string>>
{
    public void Configure(EntityTypeBuilder<IdentityUserClaim<string>> builder) =>
        builder.ToTable("UserClaims", MyMesDbContext.IdentitySchema);
}

internal sealed class IdentityUserLoginConfiguration : IEntityTypeConfiguration<IdentityUserLogin<string>>
{
    public void Configure(EntityTypeBuilder<IdentityUserLogin<string>> builder) =>
        builder.ToTable("UserLogins", MyMesDbContext.IdentitySchema);
}

internal sealed class IdentityUserTokenConfiguration : IEntityTypeConfiguration<IdentityUserToken<string>>
{
    public void Configure(EntityTypeBuilder<IdentityUserToken<string>> builder) =>
        builder.ToTable("UserTokens", MyMesDbContext.IdentitySchema);
}

internal sealed class IdentityRoleClaimConfiguration : IEntityTypeConfiguration<IdentityRoleClaim<string>>
{
    public void Configure(EntityTypeBuilder<IdentityRoleClaim<string>> builder) =>
        builder.ToTable("RoleClaims", MyMesDbContext.IdentitySchema);
}
