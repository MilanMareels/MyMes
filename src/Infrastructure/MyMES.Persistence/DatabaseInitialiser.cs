using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MyMES.Persistence;

/// <summary>Brings the database schema up to date at start-up, when that is allowed.</summary>
public interface IDatabaseInitialiser
{
    /// <summary>
    /// Applies pending migrations if <see cref="DatabaseOptions.AutoMigrate"/> is on; otherwise
    /// only reports whether the database is behind.
    /// </summary>
    Task InitialiseAsync(CancellationToken cancellationToken = default);
}

/// <inheritdoc cref="IDatabaseInitialiser"/>
public sealed class DatabaseInitialiser(
    MyMesDbContext db,
    IOptions<DatabaseOptions> options,
    ILogger<DatabaseInitialiser> logger) : IDatabaseInitialiser
{
    public async Task InitialiseAsync(CancellationToken cancellationToken = default)
    {
        // The in-memory provider used by the test suite has no migrations at all, and asking it
        // for them throws. Nothing to do there; the model is created from the context.
        if (!db.Database.IsRelational())
        {
            logger.LogInformation("Non-relational provider in use; skipping migrations.");
            return;
        }

        // Never EnsureCreated: it builds a schema with no migration history, after which no
        // migration can ever be applied to that database.
        var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false))
            .ToList();

        if (pending.Count == 0)
        {
            logger.LogInformation("Database schema is up to date.");
            return;
        }

        if (!options.Value.AutoMigrate)
        {
            logger.LogWarning(
                "{Count} migration(s) pending ({Migrations}) and AutoMigrate is off. "
                + "Apply them with 'dotnet ef database update' before using the application.",
                pending.Count,
                string.Join(", ", pending));
            return;
        }

        logger.LogInformation("Applying {Count} migration(s): {Migrations}", pending.Count, string.Join(", ", pending));
        await db.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Database schema updated.");
    }
}
