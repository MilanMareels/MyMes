using System.Reflection;
using NetArchTest.Rules;
using Shouldly;

namespace MyMES.ArchitectureTests;

/// <summary>
/// Executable architecture rules.
/// </summary>
/// <remarks>
/// <para>
/// The dependency diagram in <c>docs/architecture/dependency-rules.md</c> is prose, and prose
/// does not fail a build. These tests are the same rules in a form that does, which is what keeps
/// the boundaries real two years from now.
/// </para>
/// <para>
/// Each test names what it protects rather than just asserting: a failure here should tell the
/// next developer why the rule exists, not only that they broke it.
/// </para>
/// </remarks>
public sealed class DependencyRuleTests
{
    private static readonly Assembly Domain = typeof(MyMES.Domain.Machines.Machine).Assembly;
    private static readonly Assembly Application = typeof(MyMES.Application.DependencyInjection).Assembly;
    private static readonly Assembly Contracts = typeof(MyMES.Contracts.Machines.MachineDto).Assembly;
    private static readonly Assembly Persistence = typeof(MyMES.Persistence.MyMesDbContext).Assembly;
    private static readonly Assembly Web = typeof(MyMES.Web.Services.MyMesApiClient).Assembly;

    private static readonly Assembly MachinesModule = typeof(MyMES.Modules.Machines.MachinesModule).Assembly;
    private static readonly Assembly ProductionModule = typeof(MyMES.Modules.Production.ProductionModule).Assembly;
    private static readonly Assembly QualityModule = typeof(MyMES.Modules.Quality.QualityModule).Assembly;
    private static readonly Assembly InventoryModule = typeof(MyMES.Modules.Inventory.InventoryModule).Assembly;

    [Fact]
    public void Domain_does_not_depend_on_any_framework()
    {
        // The business rules have to be testable and portable. The moment the domain knows about
        // EF Core or ASP.NET, changing either becomes a change to the rules.
        var result = Types.InAssembly(Domain)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore",
                "Microsoft.Extensions.DependencyInjection",
                "Microsoft.Extensions.Configuration",
                "Microsoft.Extensions.Logging")
            .GetResult();

        Fail(result, "Domain must stay free of infrastructure and framework references.");
    }

    [Fact]
    public void Domain_does_not_depend_on_any_other_MyMES_project()
    {
        var result = Types.InAssembly(Domain)
            .ShouldNot()
            .HaveDependencyOnAny(
                "MyMES.Application",
                "MyMES.Contracts",
                "MyMES.Persistence",
                "MyMES.Infrastructure",
                "MyMES.Identity",
                "MyMES.Messaging",
                "MyMES.PluginSystem")
            .GetResult();

        Fail(result, "Domain sits at the centre: nothing in MyMES may be referenced from it.");
    }

    [Fact]
    public void Contracts_do_not_depend_on_the_domain()
    {
        // The wire contract must be able to stay stable while the domain evolves. Sharing types
        // would make every domain rename a breaking API change.
        var result = Types.InAssembly(Contracts)
            .ShouldNot()
            .HaveDependencyOnAny("MyMES.Domain", "MyMES.Application", "MyMES.Persistence")
            .GetResult();

        Fail(result, "Contracts are the public surface and must not leak domain types.");
    }

    [Fact]
    public void Application_does_not_depend_on_infrastructure_or_on_a_host()
    {
        var result = Types.InAssembly(Application)
            .ShouldNot()
            .HaveDependencyOnAny(
                "MyMES.Persistence",
                "MyMES.Infrastructure",
                "MyMES.Identity",
                "MyMES.Api",
                "MyMES.Web",
                "Microsoft.AspNetCore")
            .GetResult();

        Fail(result, "Application defines the use cases; infrastructure implements them, never the other way round.");
    }

    [Fact]
    public void The_Blazor_application_cannot_reach_the_database()
    {
        // The UI talks to the API and nothing else. Letting it open a DbContext would put
        // business rules and authorisation behind a screen instead of behind the API.
        var result = Types.InAssembly(Web)
            .ShouldNot()
            .HaveDependencyOnAny(
                "MyMES.Persistence",
                "MyMES.Domain",
                "MyMES.Application",
                "Microsoft.EntityFrameworkCore")
            .GetResult();

        Fail(result, "The Blazor application must reach data only through the API.");
    }

    [Theory]
    [MemberData(nameof(ModuleAssemblies))]
    public void A_module_does_not_depend_on_infrastructure(string name)
    {
        var result = Types.InAssembly(AssemblyFor(name))
            .ShouldNot()
            .HaveDependencyOnAny("MyMES.Persistence", "MyMES.Infrastructure")
            .GetResult();

        Fail(result, $"{name} must use the application layer's abstractions, not a concrete database.");
    }

    [Fact]
    public void Modules_do_not_depend_on_each_other()
    {
        // Modules communicate through contracts and domain events. A direct reference is what
        // turns a modular monolith back into a tangle.
        AssertNoDependencyOn(MachinesModule, "MyMES.Modules.Production", "MyMES.Modules.Quality", "MyMES.Modules.Inventory");
        AssertNoDependencyOn(ProductionModule, "MyMES.Modules.Machines", "MyMES.Modules.Quality", "MyMES.Modules.Inventory");
        AssertNoDependencyOn(QualityModule, "MyMES.Modules.Machines", "MyMES.Modules.Production", "MyMES.Modules.Inventory");
        AssertNoDependencyOn(InventoryModule, "MyMES.Modules.Machines", "MyMES.Modules.Production", "MyMES.Modules.Quality");
    }

    [Fact]
    public void Persistence_does_not_depend_on_a_host()
    {
        var result = Types.InAssembly(Persistence)
            .ShouldNot()
            .HaveDependencyOnAny("MyMES.Api", "MyMES.Web", "MyMES.MachineService")
            .GetResult();

        Fail(result, "Persistence is used by the hosts and must not know about them.");
    }

    public static TheoryData<string> ModuleAssemblies() =>
    [
        "MyMES.Modules.Machines",
        "MyMES.Modules.Production",
        "MyMES.Modules.Quality",
        "MyMES.Modules.Inventory"
    ];

    private static Assembly AssemblyFor(string name) => name switch
    {
        "MyMES.Modules.Machines" => MachinesModule,
        "MyMES.Modules.Production" => ProductionModule,
        "MyMES.Modules.Quality" => QualityModule,
        "MyMES.Modules.Inventory" => InventoryModule,
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown module assembly.")
    };

    private static void AssertNoDependencyOn(Assembly assembly, params string[] forbidden)
    {
        var result = Types.InAssembly(assembly).ShouldNot().HaveDependencyOnAny(forbidden).GetResult();
        Fail(result, $"{assembly.GetName().Name} must not reference another module directly.");
    }

    /// <summary>Turns a rule violation into a message that names the offending types.</summary>
    private static void Fail(TestResult result, string because)
    {
        var offenders = result.FailingTypeNames ?? [];

        result.IsSuccessful.ShouldBeTrue(
            $"{because}{Environment.NewLine}Offending types:{Environment.NewLine}  "
            + string.Join($"{Environment.NewLine}  ", offenders));
    }
}
