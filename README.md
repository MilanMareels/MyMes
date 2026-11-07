# MyMES

A Manufacturing Execution System built on .NET 10, layered the simple way:
**Controller → Service → Data access**.

This is a **foundation**: enough real functionality to prove the architecture works end to end,
and nothing speculative on top of it. Machines, production orders and product revisions are fully
implemented, together with JWT authentication and a Blazor front end.

---

## Technology

| | |
|---|---|
| Runtime | .NET 10, C# with nullable reference types |
| API | ASP.NET Core MVC controllers, Swagger/OpenAPI, SignalR, API versioning |
| Front end | Blazor Web App, Interactive Server rendering |
| Data | Entity Framework Core 10, Microsoft SQL Server 2022 |
| Identity | ASP.NET Core Identity, JWT bearer tokens |
| Logging | Serilog, structured |
| Containers | Docker, Docker Compose |

Package versions are managed centrally in `Directory.Packages.props`; no project carries a
version of its own.

---

## Architecture at a glance

```
  ┌──────────────┐     HTTP      ┌──────────────┐
  │  MyMES.Web   │ ────────────▶ │  MyMES.Api   │   MVC controllers
  │ Blazor Server│               └──────┬───────┘
  └──────────────┘                      ▼
                          ┌────────────────────────┐
                          │  MyMES.Application     │   services (business logic)
                          └───────────┬────────────┘
                                      ▼
                          ┌────────────────────────┐
                          │  MyMES.Domain          │   entities (plain POCOs)
                          └────────────────────────┘
                                      ▲
                          ┌───────────┴────────────┐
                          │  MyMES.Infrastructure  │   DbContext + repositories
                          └────────────────────────┘
```

Three boundaries matter more than the rest:

1. **The domain references nothing.** Not EF Core, not ASP.NET, not another MyMES project.
2. **The Blazor application cannot reach the database.** It references `MyMES.Application` for
   DTOs and talks HTTP. Authorisation is therefore enforced where it counts, in the API.
3. **Controllers know nothing about the database.** They call services; services use
   `IApplicationDbContext`; only Infrastructure knows how to persist.

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
│   │   ├── MyMES.Domain/           entities (POCOs), enums, domain events. No dependencies.
│   │   └── MyMES.Application/      services (business logic), DTOs, Result, validation, mapping
│   ├── BuildingBlocks/
│   │   └── MyMES.PluginSystem/     plugin discovery, replace/decorate/extend, Result→HTTP
│   ├── Infrastructure/
│   │   └── MyMES.Infrastructure/   DbContext, Identity, migrations, messaging, composition root
│   └── Host/
│       ├── MyMES.Api/              MVC controllers, JWT, Swagger, SignalR hub, health
│       └── MyMES.Web/              Blazor Server front end
│
├── extensions/
│   └── MyMES.Extensions.Sample    worked example of a third-party extension
├── plugins/                       drop-in location for packaged plugins
├── deployment/
│   ├── docker/                    one Dockerfile per host
│   ├── compose/
│   └── scripts/
└── docs/
```

**Six projects.** When you add a field you touch three files at most: the entity, the service and
the DTO — not a chain of layers.

---

## Prerequisites

- Docker Desktop (local) or Docker Engine + Compose (server). Needed for the Docker route **and**
  for the Visual Studio route, which reuses the Docker SQL Server as its database.
- The .NET 10 SDK — only needed to run or debug the app in Visual Studio.

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

## Running in Visual Studio (debugging)

Use this when you want breakpoints, hot reload and step-through debugging. The app runs in Visual
Studio, but the **database still runs in Docker** — reusing the same SQL Server keeps everything on
one connection string. LocalDB is not recommended: the .NET provider used here has trouble with it.

### 1. Start the SQL Server container

Only the database runs in Docker; the app runs in VS.

```bash
docker compose up -d sqlserver
```

Wait until `docker ps` shows `(healthy)` for `mymes-sqlserver`, then leave it running while you
debug.

### 2. Point the API at it with user-secrets

Use `127.0.0.1`, **not** `localhost` — on some machines `localhost` resolves to IPv6 while Docker
maps only IPv4, which makes the connection time out. Use the same `MSSQL_SA_PASSWORD` as in `.env`:

```bash
dotnet user-secrets --project src/MyMES.Api set "ConnectionStrings:MyMes" "Server=127.0.0.1,1433;Database=MyMES;User Id=sa;Password=YOUR_MSSQL_SA_PASSWORD;TrustServerCertificate=True;MultipleActiveResultSets=True"
```

> This user-secret **overrides** the `ConnectionStrings:MyMes` in `appsettings.Development.json`
> (which points at a Windows-authenticated local SQL Server). User-secrets are loaded last, so they
> always win.

### 3. Create the first administrator

No credentials are compiled into the source. Supply them once through user-secrets; the seeder
creates the account **only when no administrator exists yet**. If the database already has an
administrator (for example from a previous Docker run), these values are ignored and you sign in
with that existing account.

```bash
dotnet user-secrets --project src/MyMES.Api set "Identity:Bootstrap:AdministratorUserName" "admin"
```

```bash
dotnet user-secrets --project src/MyMES.Api set "Identity:Bootstrap:AdministratorEmail" "admin@example.com"
```

```bash
dotnet user-secrets --project src/MyMES.Api set "Identity:Bootstrap:AdministratorPassword" "Choose-A-Strong-One-1"
```

Also set a signing key, or the API mints a throwaway one per process in Development and every
token dies on restart:

```bash
dotnet user-secrets --project src/MyMES.Api set "Jwt:Key" "at-least-32-characters-of-random-data-here"
```

### 4. Start the two hosts

In Visual Studio: right-click the solution → **Configure Startup Projects…** → **Multiple startup
projects**, and set `MyMES.Api` and `MyMES.Web` to **Start** (API first). Then press **F5**.

Or from the command line:

```bash
dotnet run --project src/MyMES.Api
```

```bash
dotnet run --project src/MyMES.Web
```

The ports are the same as in the Docker quick start. The API applies migrations itself on start-up
(`Database:AutoMigrate` is `true` in Development).

---

## Database migrations

Migrations are managed with Visual Studio's **Package Manager Console** (Tools → NuGet Package
Manager → Package Manager Console). Set the two dropdowns to **Default project** =
`MyMES.Infrastructure` and **Startup project** = `MyMES.Api`, then:

Create one:

```powershell
Add-Migration <Name>
```

Apply it:

```powershell
Update-Database
```

Produce a script for a DBA to review instead of running it directly:

```powershell
Script-Migration -Idempotent -Output migrate.sql
```

`EnsureCreated` is never used: it builds a schema with no migration history, after which no
migration can ever be applied to that database. More in
[docs/development/database-migrations.md](docs/development/database-migrations.md).

---

## Authentication and authorisation

- The API issues JWT bearer tokens from `POST /api/v1/auth/login`.
- The Blazor application signs in against that endpoint and keeps the token in an encrypted
  authentication cookie; a delegating handler attaches it to every API call. The UI therefore
  always acts on behalf of the signed-in user.
- Endpoints authorise against a **permission**, never a role:
  `[Authorize(MesPermissions.MachinesWrite)]`. Roles hold permissions, and the mapping lives in
  `MesRoles.DefaultGrants`.
- Roles shipped: Administrator, Supervisor, Operator, Maintenance, Quality, Planner. They are
  seeded idempotently on every start-up.
- A fallback policy requires an authenticated user, so anonymous access must be opted into per
  endpoint.

---

## How to add a feature

1. Add the entity as a plain POCO in `MyMES.Domain`.
2. Add a service (and its interface) in `MyMES.Application`.
3. Register the service in `MyMES.Application`'s `DependencyInjection`.
4. Add a controller in `MyMES.Api/Controllers`.
5. Add a migration: `Add-Migration <Name>`.

That is the whole list. There is no module project, no separate contracts project and no separate
repository project to touch.

## How to replace a service implementation

```csharp
services.ReplaceService<IMachineService, CustomerMachineService>();
```

## How to wrap one without losing it

```csharp
services.DecorateService<IMachineService, AuditingMachineService>();
```

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
| Contracts duplicate the domain enums | The public API must be able to stay stable while the domain evolves. The mapping is explicit, so drift is caught early. |
| One DbContext, two schemas | `mes` and `identity`. One migration history, one transaction when a business change and a user change must happen together. |
| Over-production is accepted | The floor really does make extra pieces. Refusing the booking pushes operators into not reporting at all. |
| Permissions in the token | The API authorises from the token alone, with no database round trip per request. The cost is that a permission change takes effect when the token expires. |
| In-process event bus | Services stay decoupled without the operational cost of a broker. Publishing is not transactional; anything that must not be lost needs an outbox. |

---

## What is not here yet

- **Transactional outbox.** Domain events are published after commit and a failing handler is
  logged, not retried. Fine for the current handlers; required before anything money-adjacent.
- **Real machine protocols.** Machine drivers are a seam to be filled in later.
- **Quality and inventory as features.** The seams exist; the features themselves are not built.
- **Refresh tokens.** A token lives one hour and the user signs in again.
- **Dynamic plugin loading.** Plugins are discovered from referenced assemblies. Loading DLLs
  from `plugins/` at run time is a deployment decision, not a security boundary; see the plugin
  documentation.
