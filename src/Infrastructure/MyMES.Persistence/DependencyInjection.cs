using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyMES.Application.Abstractions;

namespace MyMES.Persistence;

/// <summary>Options controlling how the database is reached and migrated.</summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>
    /// Applies pending migrations during start-up.
    /// </summary>
    /// <remarks>
    /// Convenient on a developer machine and in the Docker compose stack. Leave it off for a real
    /// deployment, where migrations belong in a controlled release step against a backed-up
    /// database rather than in whichever instance happens to start first.
    /// </remarks>
    public bool AutoMigrate { get; set; }

    /// <summary>How often to retry a transient SQL failure before giving up.</summary>
    public int MaxRetryCount { get; set; } = 5;

    /// <summary>Seconds a command may run before timing out.</summary>
    public int CommandTimeoutSeconds { get; set; } = 30;
}

public static class DependencyInjection
{
    /// <summary>Registers the database context and exposes it as <see cref="IApplicationDbContext"/>.</summary>
    /// <exception cref="InvalidOperationException">No connection string is configured.</exception>
    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("MyMes")
            ?? throw new InvalidOperationException(
                "Connection string 'MyMes' is not configured. Set ConnectionStrings__MyMes in the "
                + "environment, or use user-secrets during development.");

        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));
        var options = configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>()
            ?? new DatabaseOptions();

        services.AddDbContext<MyMesDbContext>(builder =>
            builder.UseSqlServer(connectionString, sql =>
            {
                // Migrations live next to the context, in this assembly.
                sql.MigrationsAssembly(typeof(MyMesDbContext).Assembly.FullName);

                // Keeps the history table out of dbo and beside the tables it describes.
                sql.MigrationsHistoryTable("__EFMigrationsHistory", MyMesDbContext.MesSchema);

                // SQL Server in a container restarts and fails over; transient faults are normal.
                sql.EnableRetryOnFailure(
                    options.MaxRetryCount,
                    TimeSpan.FromSeconds(10),
                    errorNumbersToAdd: null);

                sql.CommandTimeout(options.CommandTimeoutSeconds);
            }));

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<MyMesDbContext>());
        services.AddScoped<IDatabaseInitialiser, DatabaseInitialiser>();

        return services;
    }
}
