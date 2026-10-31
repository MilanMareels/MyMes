using Microsoft.EntityFrameworkCore;
using MyMES.Domain.Integrations;
using MyMES.Domain.Machines;
using MyMES.Domain.Personnel;
using MyMES.Domain.Production;
using MyMES.Domain.Products;

namespace MyMES.Application.Abstractions;

/// <summary>
/// The database as the application layer sees it.
/// </summary>
/// <remarks>
/// <para>
/// This exposes EF Core's <see cref="DbSet{TEntity}"/> rather than hand-rolled repositories. EF
/// Core already is a unit of work with a queryable repository per aggregate, and wrapping it in a
/// generic <c>IRepository&lt;T&gt;</c> would add a layer that only removes capability.
/// </para>
/// <para>
/// The trade-off is that the application layer references EF Core. The <em>domain</em> layer does
/// not, which is the boundary that actually protects the business rules, and the architecture
/// tests enforce it.
/// </para>
/// </remarks>
public interface IApplicationDbContext
{
    DbSet<Machine> Machines { get; }

    DbSet<ProductionOrder> ProductionOrders { get; }

    DbSet<Product> Products { get; }

    DbSet<ProductVersion> ProductVersions { get; }

    DbSet<Employee> Employees { get; }

    DbSet<IncomingErpMessage> IncomingErpMessages { get; }

    /// <summary>
    /// Commits the tracked changes. The implementation also stamps audit columns and hands any
    /// domain events raised during the change to the event bus.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
