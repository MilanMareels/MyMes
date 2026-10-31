# Writing an extension

MyMES is built to be extended without forking it. A customer or integrator can add modules,
machine drivers, workflow steps and endpoints, and can replace or wrap services the platform
ships with.

`extensions/MyMES.Extensions.Sample` is a working example that builds and is covered by the test
suite. Start there.

---

## Read this first: what in-process plugins are not

A plugin loaded into the MyMES process **runs with the full permissions of the host**. It can
read every connection string, open sockets, read and write the file system, and call any API the
host can reach.

`AssemblyLoadContext` does not change that. It isolates *assembly versions* so two components can
use different builds of the same library. It is not a sandbox, it is not a permission boundary,
and .NET has had no in-process security sandbox since Code Access Security was removed. Anyone
who tells you otherwise is describing a feature that does not exist.

So:

- **Treat installing a plugin exactly like deploying code you wrote.** Same review, same supply
  chain scrutiny, same signing.
- **For genuinely untrusted extensions, use process or container isolation.** Run the extension
  as its own service with its own identity, talking to MyMES over HTTP with a service account that
  holds only the permissions it needs. That boundary is real because the operating system
  enforces it.
- **Give extension service accounts the minimum permissions.** The permission model works for
  machine-to-machine callers; the MachineService is the worked example.

---

## The three mechanisms

### Replace

Substitute the platform's implementation entirely. Use when the default is wrong for this
installation: a customer's own order-numbering scheme, a different way of allocating stock.

```csharp
services.ReplaceService<IProductionOrderService, AcmeProductionOrderService>();
```

The default is gone afterwards. You own every member of the interface.

### Decorate

Wrap the existing implementation so yours runs around it. Use to add behaviour while keeping the
platform's: auditing, caching, pushing a copy of every booking to an ERP.

```csharp
services.DecorateService<IMachineService, AuditingMachineService>();
```

The decorator takes the wrapped service as a constructor parameter:

```csharp
internal sealed class AuditingMachineService(IMachineService inner, ILogger<AuditingMachineService> logger)
    : IMachineService
{
    public async Task<Result<MachineDto>> ChangeStatusAsync(
        Guid id, ChangeMachineStatusRequest request, CancellationToken cancellationToken = default)
    {
        var result = await inner.ChangeStatusAsync(id, request, cancellationToken);
        if (result.IsSuccess)
        {
            logger.LogInformation("{MachineCode} moved to {Status}.",
                result.Value.MachineCode, result.Value.Status);
        }

        return result;
    }

    // Every other member must pass straight through. Swallowing one silently removes
    // platform behaviour, and nothing will tell you.
}
```

### Extend

Add functionality beside what exists, changing nothing. Use for anything the host resolves as a
collection: extra event handlers, extra machine drivers, extra endpoints.

```csharp
services.ExtendWith<IMachineDriver, ModbusDriver>();
services.AddEventHandler<ProductionOrderCompletedEvent, NotifyErpOnCompletion>();
```

---

## Writing the plugin

```csharp
public sealed class AcmePlugin : IMyMesPlugin, IEndpointModule
{
    private bool _enabled;

    public string Name => "Acme integration";
    public string Version => "1.2.0";
    public string CompatiblePlatformVersion => "0.1";

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        _enabled = configuration.GetValue<bool>("Extensions:Acme:Enabled");
        if (!_enabled)
        {
            return;
        }

        services.DecorateService<IMachineService, AcmeAuditingMachineService>();
        services.AddEventHandler<ProductionOrderCompletedEvent, PostToErp>();
        services.AddHostedService<ErpSyncWorker>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        if (!_enabled)
        {
            return;
        }

        endpoints.MapGet("/acme/sync-status", (IErpSync sync) => Results.Ok(sync.Status()))
            .WithTags("Acme")
            .RequireAuthorization(MesPermissions.ProductionRead);
    }
}
```

Points worth copying:

- **Gate on configuration.** An extension that is always on cannot be turned off when it
  misbehaves at three in the morning.
- **Authorise your endpoints.** The fallback policy requires an authenticated user, but an
  endpoint without an explicit permission is reachable by anyone signed in.
- **A plugin needs a parameterless constructor.** It runs before the container exists, so it
  cannot have dependencies injected. Anything it needs comes from the `IConfiguration` it is
  handed.

## Registering it

Reference the project from `MyMES.Api` and add its assembly to the `AddMyMesModules` call in
`Program.cs`.

Order is guaranteed: every built-in module runs first, then plugins. By the time your
`ConfigureServices` runs, every default you might want to replace or decorate is registered.
Decorating something that is not registered throws with an explanation rather than failing
silently.

## Adding permissions

Register the right, grant it to roles, and the host turns it into a policy automatically:

```csharp
var catalog = services.BuildServiceProvider().GetRequiredService<IPermissionCatalog>();
catalog.Register("acme.sync", MesRoles.Administrator, MesRoles.Supervisor);
```

Then use it: `RequireAuthorization("acme.sync")`.

## Adding a machine driver

Implement `IMachineDriver` in a project the MachineService references and register it. Drivers
are resolved as a collection, so the poll loop does not change.

```csharp
public sealed class ModbusDriver(ILogger<ModbusDriver> logger) : IMachineDriver
{
    public string Protocol => "modbus-tcp";
    public IReadOnlyCollection<string> MachineCodes => ["PRESS-01"];

    public async Task<MachineReading?> ReadAsync(string machineCode, CancellationToken cancellationToken)
    {
        try
        {
            // ... read the registers ...
        }
        catch (SocketException ex)
        {
            // Unreachable is an expected condition on a shop floor, not an exception the
            // poll loop should handle. Return null and it is reported as a comms failure.
            logger.LogDebug(ex, "{MachineCode} did not answer.", machineCode);
            return null;
        }
    }
}
```

**Never put safety logic in a driver.** Emergency stop, light curtains and interlocks belong to
the machine's own safety chain, which is independent of the MES by design and by regulation. The
MES observes and records; it does not make a machine safe.

## Compatibility

`CompatiblePlatformVersion` is compared on major and minor; patch releases stay compatible. A
mismatch logs a warning and loads the plugin anyway, because refusing to start a plant because an
integration targets 0.1 instead of 0.2 is the wrong default. Check the log after an upgrade.

Stable to build against:

- `MyMES.Contracts` — DTOs and wire enums
- `MyMES.Abstractions` — module contract, `Result`, `IDateTimeProvider`, `ICurrentUser`,
  permission constants
- `MyMES.PluginSystem` — plugin contract and the three mechanisms
- `MyMES.Application` service interfaces

Not stable, and not intended for extensions: `MyMES.Persistence` internals, entity configurations,
and anything marked `internal`.
