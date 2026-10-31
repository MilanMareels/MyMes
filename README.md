# MyMES

A Manufacturing Execution System built as a modular monolith on .NET 10, with Clean Architecture
boundaries that are enforced by tests rather than by convention.

This is a **foundation**: enough real functionality to prove the architecture works end to end,
and nothing speculative on top of it. Machines, production orders and product revisions are fully
implemented; quality and inventory are working module scaffolding that demonstrates how a module
reacts to the rest of the system without depending on it.

---

## Technology

| | |
|---|---|
| Runtime | .NET 10, C# with nullable reference types |
| API | ASP.NET Core minimal APIs, Swagger/OpenAPI, SignalR, API versioning |
| Front end | Blazor Web App, Interactive Server rendering |
| Data | Entity Framework Core 10, Microsoft SQL Server 2022 |
| Identity | ASP.NET Core Identity, JWT bearer tokens |
| Logging | Serilog, structured |
| Tests | xUnit, Shouldly, NetArchTest |
| Containers | Docker, Docker Compose |

Package versions are managed centrally in `Directory.Packages.props`; no project carries a
version of its own.

---

## Architecture at a glance

```
          ┌──────────────┐     HTTP      ┌──────────────┐
          │  MyMES.Web   │ ────────────▶ │  MyMES.Api   │
          │ Blazor Server│               │ minimal APIs │
          └──────────────┘               └──────┬───────┘
                                                │
          ┌──────────────────┐   HTTP           │
          │MyMES.MachineService│ ──────────────▶│
          │   worker service   │                │
          └──────────────────┘                  ▼
                                   ┌────────────────────────┐
                                   │  Modules (4)           │
                                   │  Machines / Production │
                                   │  Quality / Inventory   │
                                   └───────────┬────────────┘
                                               ▼
                                   ┌────────────────────────┐
                                   │  MyMES.Application     │ use cases
                                   └───────────┬────────────┘
                                               ▼
                                   ┌────────────────────────┐
                                   │  MyMES.Domain          │ no framework deps
                                   └────────────────────────┘
                                               ▲
                                   ┌───────────┴────────────┐
                                   │  MyMES.Persistence     │ EF Core, SQL Server
                                   │  MyMES.Identity        │
                                   └────────────────────────┘
```

Three boundaries matter more than the rest, and all three are asserted in
`tests/MyMES.ArchitectureTests`:

1. **The domain references nothing.** Not EF Core, not ASP.NET, not another MyMES project.
2. **The Blazor application cannot reach the database.** It references `MyMES.Contracts` and
   talks HTTP. Authorisation is therefore enforced where it counts, in the API.
3. **Modules do not reference each other.** They communicate through contracts and domain events.

See [docs/architecture/overview.md](docs/architecture/overview.md) and
[docs/architecture/dependency-rules.md](docs/architecture/dependency-rules.md).

---

## Folder structure

```
MyMES/
├── MyMES.sln
├── Directory.Build.props          shared build settings
├── Directory.Packages.props       central package versions
├── global.json                    pinned SDK
├── docker-compose.yml
├── .env.example                   placeholders only; copy to .env
│
├── src/
│   ├── Core/
│   │   ├── MyMES.Abstractions     module contract, clock, current user, Result, permissions
│   │   ├── MyMES.Domain           entities, invariants, domain events. No dependencies.
│   │   ├── MyMES.Contracts        DTOs and wire enums shared by API and UI
│   │   └── MyMES.Application      use cases, validation, IApplicationDbContext, mapping
│   ├── Infrastructure/
│   │   ├── MyMES.Identity         ApplicationUser, roles, permission catalogue, seeding
│   │   ├── MyMES.Persistence      DbContext, entity configurations, migrations
│   │   ├── MyMES.Infrastructure   composition root: clock, current user, database, messaging
│   │   └── MyMES.Messaging        integration events, IRealtimeNotifier
│   ├── Host/
│   │   ├── MyMES.Api              minimal APIs, JWT, Swagger, SignalR hub, health
│   │   ├── MyMES.Web              Blazor Server front end
│   │   └── MyMES.MachineService   worker polling machine drivers
│   ├── Modules/
│   │   ├── MyMES.Modules.Machines      /machines
│   │   ├── MyMES.Modules.Production    /production-orders, /products
│   │   ├── MyMES.Modules.Quality       reacts to completed orders
│   │   └── MyMES.Modules.Inventory     reacts to production bookings
│   └── BuildingBlocks/
│       ├── MyMES.EventBus         in-process publish/subscribe
│       ├── MyMES.PluginSystem     module discovery, replace/decorate/extend, Result→HTTP
│       └── MyMES.Localization     UI string catalogue
│
├── extensions/
│   └── MyMES.Extensions.Sample    worked example of a third-party extension
├── plugins/                       drop-in location for packaged plugins
├── tests/
│   ├── MyMES.UnitTests            domain rules, use cases, mapping, extensibility
│   ├── MyMES.IntegrationTests     the real API over HTTP
│   └── MyMES.ArchitectureTests    the dependency rules, as assertions
├── deployment/
│   ├── docker/                    one Dockerfile per host
│   ├── compose/
│   └── scripts/
└── docs/
```

---

## Prerequisites

- Docker Desktop (local) or Docker Engine + Compose (server)
- The .NET 10 SDK is only needed for the non-Docker debugging route below

---

## Quick start (Docker)

This is the recommended way to run MyMES — locally, in development and in production. The same
`docker-compose.yml` is used everywhere; only the values in `.env` differ.

```bash
cp .env.example .env
```

Fill in the secrets in `.env`, then:

```bash
docker compose up -d --build
```

| | |
|---|---|
| Front end | http://localhost:5200 |
| API + Swagger | http://localhost:5100/swagger |
| SignalR hub | http://localhost:5100/hubs/shop-floor |
| Health | `/health/live`, `/health/ready` |

Required values in `.env`:

- `MSSQL_SA_PASSWORD` — a strong password; SQL Server rejects weak ones.
- `JWT_SIGNING_KEY` — at least 32 characters. Generate one with `openssl rand -base64 48`.
- `MYMES_ADMIN_USERNAME`, `MYMES_ADMIN_EMAIL`, `MYMES_ADMIN_PASSWORD` — the first administrator,
  created once on the first start-up.

Other operations:

```bash
docker compose logs -f api
```

```bash
docker compose down
```

```bash
docker compose down -v
```

`down -v` also removes the SQL Server volume, which deletes the database.

Notes on the compose file:

- SQL Server is published on `127.0.0.1:1433` only, so it is not exposed to the network.
- The API waits for SQL Server's health check to report that it accepts queries, not merely that
  the process started.
- `Database__AutoMigrate` is `true` in the stack so one command brings everything up. Turn it off
  for a real deployment and migrate as a controlled release step.
- In `Production` the web app marks its login cookie `Secure`, so the browser only sends it over
  HTTPS. To reach the app over plain HTTP (for example a home server), set
  `ASPNETCORE_ENVIRONMENT=Development` in `.env`; for anything public, put an HTTPS reverse proxy
  in front instead.
- No password appears in `docker-compose.yml`; everything comes from `.env`, which is git-ignored.

### Development mode (optional)

For verbose logging and Swagger while developing, apply the dev overlay on top of the base file:

```bash
docker compose -f docker-compose.yml -f deployment/compose/docker-compose.dev.yml up -d --build
```

The overlay only adds developer-friendly settings (Debug log level, Swagger on); the services and
the database are the same.

---

## Running without Docker (Visual Studio debugging only)

Use this route only when you want breakpoints, hot reload and step-through debugging in Visual
Studio. You then manage the database yourself — a local SQL Server, or LocalDB. In Development the
database migrates itself on start-up (`Database:AutoMigrate` is `true`), so no manual migration is
needed.

### 1. Configure the database

`src/Host/MyMES.Api/appsettings.Development.json` points at a local SQL Server with Windows
authentication. Override it with user-secrets if yours differs. On a machine with only Visual
Studio's **LocalDB** (no full SQL Server):

```bash
dotnet user-secrets --project src/Host/MyMES.Api set "ConnectionStrings:MyMes" "Server=(localdb)\MSSQLLocalDB;Database=MyMES;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True"
```

### 2. Create the first administrator

No credentials are compiled into the source. Supply them once through user-secrets; the seeder
creates the account only when no administrator exists yet, and logs a warning if nothing is
configured.

```bash
dotnet user-secrets --project src/Host/MyMES.Api set "Identity:Bootstrap:AdministratorUserName" "admin"
```

```bash
dotnet user-secrets --project src/Host/MyMES.Api set "Identity:Bootstrap:AdministratorEmail" "admin@example.com"
```

```bash
dotnet user-secrets --project src/Host/MyMES.Api set "Identity:Bootstrap:AdministratorPassword" "Choose-A-Strong-One-1"
```

Also set a signing key, or the API mints a throwaway one per process in Development and every
token dies on restart:

```bash
dotnet user-secrets --project src/Host/MyMES.Api set "Jwt:Key" "at-least-32-characters-of-random-data-here"
```

### 3. Start the three hosts

```bash
dotnet run --project src/Host/MyMES.Api
```

```bash
dotnet run --project src/Host/MyMES.Web
```

```bash
dotnet run --project src/Host/MyMES.MachineService
```

The ports are the same as in the Docker quick start above.

---

## Database migrations

Create one:

```bash
dotnet ef migrations add <Name> --project src/Infrastructure/MyMES.Persistence --startup-project src/Host/MyMES.Api
```

Apply it:

```bash
dotnet ef database update --project src/Infrastructure/MyMES.Persistence --startup-project src/Host/MyMES.Api
```

Produce a script for a DBA to review instead of running it directly:

```bash
dotnet ef migrations script --idempotent --project src/Infrastructure/MyMES.Persistence --startup-project src/Host/MyMES.Api --output migrate.sql
```

`EnsureCreated` is never used: it builds a schema with no migration history, after which no
migration can ever be applied to that database. More in
[docs/development/database-migrations.md](docs/development/database-migrations.md).

---

## Tests

```bash
dotnet test
```

Three suites, all runnable without a database:

| Suite | What it covers |
|---|---|
| `MyMES.UnitTests` | Domain invariants and state machines, use cases on an in-memory context, exhaustive enum mapping, the three extension mechanisms |
| `MyMES.IntegrationTests` | The real API over HTTP: sign-in, authorisation, module routing, the `ProblemDetails` contract |
| `MyMES.ArchitectureTests` | The dependency rules above, as assertions that fail the build |

---

## Authentication and authorisation

- The API issues JWT bearer tokens from `POST /api/v1/auth/login`.
- The Blazor application signs in against that endpoint and keeps the token in an encrypted
  authentication cookie; a delegating handler attaches it to every API call. The UI therefore
  always acts on behalf of the signed-in user.
- Endpoints authorise against a **permission**, never a role:
  `RequireAuthorization(MesPermissions.MachinesWrite)`. Roles hold permissions, and the mapping
  lives in `MesRoles.DefaultGrants`.
- Roles shipped: Administrator, Supervisor, Operator, Maintenance, Quality, Planner. They are
  seeded idempotently on every start-up.
- A fallback policy requires an authenticated user, so anonymous access must be opted into per
  endpoint.

---

## How to add a new module

1. Create `src/Modules/MyMES.Modules.<Name>` referencing `MyMES.Application`, `MyMES.Domain`,
   `MyMES.Contracts`, `MyMES.Abstractions`, `MyMES.EventBus` and `MyMES.PluginSystem`.
   Add `<FrameworkReference Include="Microsoft.AspNetCore.App" />` if it maps endpoints.
2. Implement `IMyMesModule`, and `IEndpointModule` if it has an API surface:

   ```csharp
   public sealed class MaintenanceModule : IMyMesModule, IEndpointModule
   {
       public string Name => "Maintenance";
       public int Order => 50;

       public void ConfigureServices(IServiceCollection services, IConfiguration configuration) =>
           services.AddEventHandler<MachineStatusChangedEvent, ScheduleServiceOnFault>();

       public void MapEndpoints(IEndpointRouteBuilder endpoints) =>
           endpoints.MapGroup("/maintenance").WithTags("Maintenance");
   }
   ```

3. Add the project to `MyMES.Api` and list its assembly in the `AddMyMesModules` call.
4. The architecture tests will hold you to the rules. That is the point.

## How to replace a service implementation

```csharp
services.ReplaceService<IMachineService, CustomerMachineService>();
```

## How to wrap one without losing it

```csharp
services.DecorateService<IMachineService, AuditingMachineService>();
```

## How to add a machine driver

Implement `IMachineDriver` in `MyMES.MachineService` and register it. Drivers are resolved as a
collection, so adding Modbus TCP or OPC UA is one registration and no change to the poll loop.
See `SimulatedMachineDriver` for the shape, including why an unreachable machine returns `null`
rather than throwing.

## How to create an extension

`extensions/MyMES.Extensions.Sample` is a working example covering all three mechanisms. Start
from [docs/plugins/getting-started.md](docs/plugins/getting-started.md), which also covers what
in-process plugins can and cannot be trusted with.

---

## Deliberate decisions

| Decision | Reasoning |
|---|---|
| `Guid.CreateVersion7()` for keys | Globally unique like any GUID, but time-ordered, so rows append to the clustered index instead of fragmenting it. |
| Application layer references EF Core | `IApplicationDbContext` exposes `DbSet<T>`. EF Core already is a unit of work with a queryable repository; wrapping it in a generic repository removes capability and adds a layer. The **domain** stays framework-free, which is the boundary that protects the rules. |
| Contracts duplicate the domain enums | The public API must be able to stay stable while the domain evolves. The mapping is explicit and exhaustively tested, so drift fails the build. |
| One DbContext, two schemas | `mes` and `identity`. One migration history, one transaction when a business change and a user change must happen together. Per-module contexts are the next step if a module ever moves to its own database. |
| Over-production is accepted | The floor really does make extra pieces. Refusing the booking pushes operators into not reporting at all. |
| Permissions in the token | The API authorises from the token alone, with no database round trip per request. The cost is that a permission change takes effect when the token expires. |
| In-process event bus | The right shape for a modular monolith: modules stay decoupled without the operational cost of a broker. Publishing is not transactional; anything that must not be lost needs an outbox. |

---

## What is not here yet

- **Transactional outbox.** Domain events are published after commit and a failing handler is
  logged, not retried. Fine for the current handlers; required before anything money-adjacent.
- **Per-module schemas.** All entities live in `MyMES.Domain` and all configurations in
  `MyMES.Persistence`. The schema separation makes the split possible later.
- **Real machine protocols.** Only the simulated driver exists; `IMachineDriver` is the seam.
- **Quality and inventory as features.** They are working module scaffolding, not finished
  modules: an in-memory inspection backlog and stock ledger that demonstrate cross-module
  reaction through events.
- **Refresh tokens.** A token lives one hour and the user signs in again.
- **Dynamic plugin loading.** Plugins are discovered from referenced assemblies. Loading DLLs
  from `plugins/` at run time is a deployment decision, not a security boundary; see the plugin
  documentation.
