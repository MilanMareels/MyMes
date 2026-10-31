using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using MyMES.Abstractions.Security;
using MyMES.Abstractions.Time;
using MyMES.EventBus;

namespace MyMES.Persistence;

/// <summary>
/// Builds a context for <c>dotnet ef</c> without starting the application.
/// </summary>
/// <remarks>
/// <para>
/// The EF tools can normally reach the host's service provider, but doing so starts the whole
/// API, which needs a reachable database and real configuration. Migrations should be
/// generatable on a laptop with nothing running, so the factory is explicit.
/// </para>
/// <para>
/// The connection string is read from <c>MYMES_MIGRATIONS_CONNECTION</c> when present and
/// otherwise falls back to local SQL Server. It is only ever used to read the provider's shape
/// while scaffolding, never to connect during <c>migrations add</c>.
/// </para>
/// </remarks>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<MyMesDbContext>
{
    private const string DefaultConnection =
        "Server=localhost;Database=MyMES;Trusted_Connection=True;TrustServerCertificate=True";

    public MyMesDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("MYMES_MIGRATIONS_CONNECTION")
            ?? DefaultConnection;

        var options = new DbContextOptionsBuilder<MyMesDbContext>()
            .UseSqlServer(connectionString, sql =>
            {
                sql.MigrationsAssembly(typeof(MyMesDbContext).Assembly.FullName);
                sql.MigrationsHistoryTable("__EFMigrationsHistory", MyMesDbContext.MesSchema);
            })
            .Options;

        return new MyMesDbContext(
            options,
            new DesignTimeDateTimeProvider(),
            new DesignTimeCurrentUser(),
            new DesignTimeEventBus());
    }

    private sealed class DesignTimeDateTimeProvider : IDateTimeProvider
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }

    private sealed class DesignTimeCurrentUser : ICurrentUser
    {
        public string? UserId => null;

        public string? UserName => null;

        public bool IsAuthenticated => false;

        public bool IsInRole(string role) => false;
    }

    /// <summary>Scaffolding never saves anything, so nothing is ever published.</summary>
    private sealed class DesignTimeEventBus : IEventBus
    {
        public Task PublishAsync<TEvent>(TEvent message, CancellationToken cancellationToken = default)
            where TEvent : class => Task.CompletedTask;

        public Task PublishAsync(object message, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
