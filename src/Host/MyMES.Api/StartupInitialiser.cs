using MyMES.Identity;
using MyMES.Persistence;

namespace MyMES.Api;

/// <summary>
/// Brings the database and the identity data up to date before the API serves its first request.
/// </summary>
/// <remarks>
/// <para>
/// A hosted service rather than inline code after <c>app.Build()</c>. The host runs this as part
/// of start-up, so <c>dotnet run</c>, the container and <c>WebApplicationFactory</c> in the test
/// suite all behave identically. Code placed between <c>Build()</c> and <c>Run()</c> does not get
/// that guarantee, which is exactly the kind of difference that makes tests lie.
/// </para>
/// <para>
/// A failure here stops the host. That is intended: an API serving requests against a schema it
/// could not verify is worse than one that refuses to start and says why.
/// </para>
/// </remarks>
public sealed class StartupInitialiser(
    IServiceProvider services,
    ILogger<StartupInitialiser> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();

        var initialiser = scope.ServiceProvider.GetRequiredService<IDatabaseInitialiser>();
        await initialiser.InitialiseAsync(cancellationToken).ConfigureAwait(false);

        // Seeding needs the schema to exist. With AutoMigrate off and migrations pending this
        // fails loudly rather than half-seeding a stale database.
        var seeder = scope.ServiceProvider.GetRequiredService<IIdentitySeeder>();
        await seeder.SeedAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Start-up initialisation complete.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
