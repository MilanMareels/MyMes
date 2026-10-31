using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MyMES.Abstractions.Results;
using MyMES.Abstractions.Time;
using MyMES.Application.Abstractions;
using MyMES.Application.Machines;
using MyMES.Contracts.Common;
using MyMES.Contracts.Machines;
using MyMES.Domain.Machines;
using MyMES.Domain.Personnel;
using MyMES.Domain.Production;
using MyMES.Domain.Products;
using Shouldly;

namespace MyMES.UnitTests.Application;

/// <summary>
/// The machine use cases against an in-memory context.
/// </summary>
/// <remarks>
/// No SQL Server here on purpose: these tests are about the service's own decisions (conflict
/// detection, not-found handling, mapping), not about the database. Provider-specific behaviour
/// such as unique indexes belongs in the integration suite.
/// </remarks>
public sealed class MachineServiceTests : IDisposable
{
    private readonly TestDbContext _db;
    private readonly MachineService _service;

    public MachineServiceTests()
    {
        _db = new TestDbContext();
        _service = new MachineService(
            _db,
            new FixedClock(new DateTime(2026, 10, 31, 8, 0, 0, DateTimeKind.Utc)),
            NullLogger<MachineService>.Instance);
    }

    [Fact]
    public async Task Creating_a_machine_returns_the_mapped_contract()
    {
        var result = await _service.CreateAsync(new CreateMachineRequest
        {
            MachineCode = "cnc-01",
            Name = "CNC Lathe 1",
            Description = "Main lathe"
        });

        result.IsSuccess.ShouldBeTrue();
        result.Value.MachineCode.ShouldBe("CNC-01");
        result.Value.Status.ShouldBe(MachineStatusDto.Idle);
    }

    [Fact]
    public async Task A_duplicate_code_is_reported_as_a_conflict()
    {
        await _service.CreateAsync(new CreateMachineRequest { MachineCode = "CNC-01", Name = "First" });

        var result = await _service.CreateAsync(new CreateMachineRequest { MachineCode = "cnc-01", Name = "Second" });

        result.IsFailure.ShouldBeTrue();
        result.Error!.Value.Kind.ShouldBe(ErrorKind.Conflict);
        result.Error!.Value.Code.ShouldBe("machine.code_in_use");
    }

    [Fact]
    public async Task An_unknown_machine_is_reported_as_not_found()
    {
        var result = await _service.GetByIdAsync(Guid.CreateVersion7());

        result.IsFailure.ShouldBeTrue();
        result.Error!.Value.Kind.ShouldBe(ErrorKind.NotFound);
    }

    [Fact]
    public async Task Changing_the_status_of_a_decommissioned_machine_is_a_conflict()
    {
        var created = await _service.CreateAsync(new CreateMachineRequest { MachineCode = "CNC-01", Name = "Lathe" });
        var machine = await _db.Machines.SingleAsync(m => m.Id == created.Value.Id);
        machine.Decommission(DateTime.UtcNow);
        await _db.SaveChangesAsync();

        var result = await _service.ChangeStatusAsync(
            created.Value.Id,
            new ChangeMachineStatusRequest { Status = MachineStatusDto.Running });

        result.IsFailure.ShouldBeTrue();
        result.Error!.Value.Kind.ShouldBe(ErrorKind.Conflict);
    }

    [Fact]
    public async Task Listing_filters_by_status_and_pages_the_result()
    {
        for (var i = 1; i <= 5; i++)
        {
            await _service.CreateAsync(new CreateMachineRequest { MachineCode = $"CNC-0{i}", Name = $"Lathe {i}" });
        }

        var running = await _service.GetByIdAsync((await _service.GetAsync(new PageRequest())).Items[0].Id);
        await _service.ChangeStatusAsync(
            running.Value.Id,
            new ChangeMachineStatusRequest { Status = MachineStatusDto.Running });

        var page = await _service.GetAsync(
            new PageRequest { Page = 1, PageSize = 2 },
            MachineStatusDto.Idle);

        page.TotalCount.ShouldBe(4);
        page.Items.Count.ShouldBe(2);
        page.HasNextPage.ShouldBeTrue();
    }

    public void Dispose() => _db.Dispose();

    private sealed class FixedClock(DateTime utcNow) : IDateTimeProvider
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    /// <summary>
    /// A minimal context implementing the application's database contract.
    /// </summary>
    /// <remarks>
    /// Deliberately not <c>MyMesDbContext</c>: that one carries the Identity schema and the audit
    /// and event-dispatch behaviour, none of which these tests exercise. Depending on it here
    /// would make a unit test fail for reasons that have nothing to do with the unit.
    /// </remarks>
    private sealed class TestDbContext : DbContext, IApplicationDbContext
    {
        public TestDbContext()
            : base(new DbContextOptionsBuilder<TestDbContext>()
                .UseInMemoryDatabase($"machines-{Guid.CreateVersion7()}")
                .Options)
        {
        }

        public DbSet<Machine> Machines => Set<Machine>();

        public DbSet<ProductionOrder> ProductionOrders => Set<ProductionOrder>();

        public DbSet<Product> Products => Set<Product>();

        public DbSet<ProductVersion> ProductVersions => Set<ProductVersion>();

        public DbSet<Employee> Employees => Set<Employee>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Machine>(e =>
            {
                e.HasKey(m => m.Id);
                e.Ignore(m => m.DomainEvents);
            });

            modelBuilder.Entity<ProductionOrder>(e =>
            {
                e.HasKey(o => o.Id);
                e.Ignore(o => o.DomainEvents);
                e.Ignore(o => o.CompletionPercentage);
                e.Ignore(o => o.IsClosed);
            });

            modelBuilder.Entity<Product>(e =>
            {
                e.HasKey(p => p.Id);
                e.Ignore(p => p.DomainEvents);
                e.Ignore(p => p.ReleasedVersion);
            });

            modelBuilder.Entity<ProductVersion>(e =>
            {
                e.HasKey(v => v.Id);
                e.Ignore(v => v.DomainEvents);
            });

            modelBuilder.Entity<Employee>(e =>
            {
                e.HasKey(x => x.Id);
                e.Ignore(x => x.DomainEvents);
                e.Ignore(x => x.FullName);
            });
        }
    }
}
