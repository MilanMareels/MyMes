using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MyMES.Persistence;

namespace MyMES.IntegrationTests;

/// <summary>
/// Boots the real API in memory.
/// </summary>
/// <remarks>
/// <para>
/// Everything is the production configuration except the database, which is swapped for the
/// in-memory provider. That keeps the suite runnable on any machine and in CI without a SQL
/// Server, while still exercising the real pipeline: authentication, authorisation, module
/// registration, routing and the exception handler.
/// </para>
/// <para>
/// The trade-off is deliberate and worth naming: anything provider-specific (unique indexes,
/// cascade behaviour, decimal precision) is <em>not</em> covered here. Those are verified by
/// applying the migration to SQL Server, which is a separate step in the release pipeline.
/// </para>
/// </remarks>
public sealed class MyMesApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"mymes-tests-{Guid.CreateVersion7()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Development so the API mints an ephemeral signing key instead of demanding one.
        builder.UseEnvironment(Environments.Development);

        // Set as host settings, not through ConfigureAppConfiguration.
        //
        // With the minimal hosting model, Program.cs reads builder.Configuration while it is
        // still registering services. A ConfigureAppConfiguration delegate contributed by the
        // factory runs later than that, so values added there arrive too late to influence
        // anything decided at registration time. UseSetting lands in the configuration the
        // builder itself is created with, which is early enough.
        foreach (var (key, value) in TestSettings)
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureServices(services =>
        {
            // Drop every registration the SQL Server provider brought with it, then re-register
            // the context on the in-memory provider.
            var descriptors = services
                .Where(d =>
                    d.ServiceType == typeof(DbContextOptions<MyMesDbContext>)
                    || d.ServiceType == typeof(DbContextOptions)
                    || (d.ServiceType.FullName?.StartsWith(
                        "Microsoft.EntityFrameworkCore.", StringComparison.Ordinal) ?? false))
                .ToList();

            foreach (var descriptor in descriptors)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<MyMesDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName)
                    // The in-memory provider warns that it ignores transactions; the suite does
                    // not test transactional behaviour, so the warning is suppressed knowingly.
                    .ConfigureWarnings(w => w.Ignore(
                        Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning)));
        });
    }

    /// <summary>Password the seeded test administrator signs in with.</summary>
    public const string AdministratorPassword = "Integration-Tests-1";

    public const string AdministratorUserName = "test.admin";

    private static readonly Dictionary<string, string> TestSettings = new(StringComparer.Ordinal)
    {
        // Never connected to: the provider is replaced below. It only has to satisfy the
        // start-up check that a connection string is configured at all.
        ["ConnectionStrings:MyMes"] = "Server=unused;Database=unused;Trusted_Connection=True",
        ["Database:AutoMigrate"] = "false",
        ["Jwt:Key"] = "integration-tests-signing-key-not-a-secret-0123456789",
        ["Jwt:Issuer"] = "MyMES.Tests",
        ["Jwt:Audience"] = "MyMES.Tests",
        ["Extensions:Sample:Enabled"] = "true",
        ["Identity:Bootstrap:AdministratorUserName"] = AdministratorUserName,
        ["Identity:Bootstrap:AdministratorEmail"] = "test.admin@example.invalid",
        ["Identity:Bootstrap:AdministratorPassword"] = AdministratorPassword
    };
}

