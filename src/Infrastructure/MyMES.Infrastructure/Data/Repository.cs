using Microsoft.EntityFrameworkCore;
using MyMES.Application.Data;

namespace MyMES.Persistence;

/// <summary>Generic EF Core repository. The only place a queryable is exposed.</summary>
public sealed class Repository<TEntity>(MyMesDbContext db) : IRepository<TEntity> where TEntity : class
{
    public IQueryable<TEntity> Query() => db.Set<TEntity>();

    public async Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await db.Set<TEntity>().FindAsync([id], cancellationToken).ConfigureAwait(false);

    public void Add(TEntity entity) => db.Set<TEntity>().Add(entity);

    public void Update(TEntity entity) => db.Set<TEntity>().Update(entity);

    public void Remove(TEntity entity) => db.Set<TEntity>().Remove(entity);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        db.SaveChangesAsync(cancellationToken);
}
