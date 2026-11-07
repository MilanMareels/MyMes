namespace MyMES.Application.Data;

/// <summary>
/// Data access for one entity type. Services depend on this instead of on the DbContext, so the
/// repository is the only place that touches EF Core's queryable.
/// </summary>
public interface IRepository<TEntity> where TEntity : class
{
    /// <summary>Exposes the queryable for read queries. The service composes LINQ here.</summary>
    IQueryable<TEntity> Query();

    Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    void Add(TEntity entity);

    void Update(TEntity entity);

    void Remove(TEntity entity);

    /// <summary>Commits all tracked changes for this unit of work.</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
