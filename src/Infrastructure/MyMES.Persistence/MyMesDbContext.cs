using System.Reflection;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MyMES.Abstractions.Security;
using MyMES.Abstractions.Time;
using MyMES.Application.Abstractions;
using MyMES.Domain.Common;
using MyMES.Domain.Integrations;
using MyMES.Domain.Machines;
using MyMES.Domain.Personnel;
using MyMES.Domain.Production;
using MyMES.Domain.Products;
using MyMES.EventBus;
using MyMES.Identity;

namespace MyMES.Persistence;

/// <summary>
/// The single database context: MES tables and the ASP.NET Core Identity schema.
/// </summary>
/// <remarks>
/// <para>
/// One context, two schemas. <c>mes</c> holds the business tables and <c>identity</c> holds the
/// authentication tables. Keeping them in one context means one migration history and one
/// transaction when a business change and a user change have to happen together.
/// </para>
/// <para>
/// Splitting per module into separate contexts is the natural next step if a module ever has to
/// move to its own database; the schema separation here is what makes that possible later.
/// </para>
/// </remarks>
public sealed class MyMesDbContext(
    DbContextOptions<MyMesDbContext> options,
    IDateTimeProvider clock,
    ICurrentUser currentUser,
    IEventBus eventBus)
    : IdentityDbContext<ApplicationUser, ApplicationRole, string>(options), IApplicationDbContext
{
    /// <summary>Schema holding the MES business tables.</summary>
    public const string MesSchema = "mes";

    /// <summary>Schema holding the ASP.NET Core Identity tables.</summary>
    public const string IdentitySchema = "identity";

    public DbSet<Machine> Machines => Set<Machine>();

    public DbSet<ProductionOrder> ProductionOrders => Set<ProductionOrder>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<ProductVersion> ProductVersions => Set<ProductVersion>();

    public DbSet<Employee> Employees => Set<Employee>();

    public DbSet<IncomingErpMessage> IncomingErpMessages => Set<IncomingErpMessage>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasDefaultSchema(MesSchema);
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }

    /// <summary>
    /// Stamps audit columns, saves, then publishes the domain events raised during the change.
    /// </summary>
    /// <remarks>
    /// Events are published <em>after</em> the commit so a handler never reacts to work that is
    /// then rolled back. The trade-off is that a handler failing after the commit cannot undo it;
    /// handlers log and continue, and anything that must not be lost needs an outbox.
    /// </remarks>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampAuditColumns();

        // Collected before saving: EF clears the change tracker's view of them afterwards.
        var entitiesWithEvents = ChangeTracker
            .Entries<Entity>()
            .Select(e => e.Entity)
            .Where(e => e.DomainEvents.Count > 0)
            .ToList();

        var domainEvents = entitiesWithEvents.SelectMany(e => e.DomainEvents).ToList();

        var affected = await base.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        foreach (var entity in entitiesWithEvents)
        {
            entity.ClearDomainEvents();
        }

        foreach (var domainEvent in domainEvents)
        {
            await eventBus.PublishAsync(domainEvent, cancellationToken).ConfigureAwait(false);
        }

        return affected;
    }

    private void StampAuditColumns()
    {
        var now = clock.UtcNow;
        var actor = currentUser.UserName ?? currentUser.UserId;

        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAtUtc = now;
                    entry.Entity.CreatedBy = actor;
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAtUtc = now;
                    entry.Entity.UpdatedBy = actor;
                    // Creation details are immutable once written.
                    entry.Property(e => e.CreatedAtUtc).IsModified = false;
                    entry.Property(e => e.CreatedBy).IsModified = false;
                    break;
            }
        }
    }
}
