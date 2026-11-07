# Architecture overview

MyMES is a layered application on .NET 10. One process, one database, one deployment unit, divided
into six projects so the dependencies always point inward.

## The layers

### MyMES.Domain

Entities (plain POCOs), enums and domain events. **References nothing** — not EF Core, not
ASP.NET, not another MyMES project.

Entities carry no business methods: `Product`, `Machine` and `ProductionOrder` are data objects
with `get; set;` properties. The business rules live in the services.

### MyMES.Application

Use cases, validation, DTOs and mapping. Defines `IApplicationDbContext`, which exposes `DbSet<T>`
and `SaveChangesAsync`.

This is the one place purity is traded away deliberately: the application layer references EF Core.
The alternative, a generic `IRepository<T>`, hides `Include`, projection and `AsNoTracking` behind
an abstraction that can only ever be a worse version of what EF already offers. The boundary that
actually protects the business rules is the domain's, and that one is absolute.

Services return `Result` / `Result<T>` for expected failures (duplicate code, missing record,
illegal transition) and let genuine faults throw. The controllers map the first to a status code
and the second to a 500.

### MyMES.Infrastructure

`MyMesDbContext` (with its entity configurations and migrations), ASP.NET Core Identity, the
realtime messaging defaults and the composition root that wires them together.

`SaveChangesAsync` stamps the audit columns, saves, then publishes the domain events raised during
the change. Publishing *after* the commit means a handler never reacts to work that is then rolled
back.

Two schemas: `mes` for business tables, `identity` for authentication. One migration history.

### MyMES.Api

ASP.NET Core MVC controllers, JWT, Swagger, SignalR, health checks and the composition root
(`Program.cs`).

Controllers are thin: they validate the route, call a service and translate the `Result` into an
HTTP response. They never touch the database.

### MyMES.Web

Blazor Server. References `MyMES.Application` for DTOs and talks HTTP to the API — it has no
database access at all.

### MyMES.PluginSystem

The extension system: plugin discovery, `ReplaceService`, `DecorateService`, `ExtendWith` and the
`Result → HTTP` mapping. Third-party code can replace or wrap a service without forking the
platform.

## Cross-cutting

### Errors

One shape everywhere: RFC 7807 `ProblemDetails`, with a machine-readable `code` extension that
clients branch on instead of parsing the message. `ApiControllerBase.ToOk` maps `ErrorKind` to a
status code, so every controller fails identically.

Outside Development the 500 detail is withheld: exception text leaks schema, paths and library
versions. The trace id stays, so a report can be correlated with the log.

### Logging

Serilog, structured. Messages use named placeholders, never interpolation, so the properties stay
queryable.

### Real-time

`IRealtimeNotifier` knows nothing about SignalR. The API supplies the SignalR implementation; other
hosts get a no-op.

### Time

Nothing reads `DateTime.UtcNow` directly except `SystemDateTimeProvider`. Everything else takes
`IDateTimeProvider`, so time can be pinned and asserted on.

## Known gaps

- **No outbox.** Event publication is not transactional.
- **No distributed concurrency control.** Two operators booking the same order simultaneously is
  last-write-wins on quantities. A row version on `ProductionOrder` is the fix when it matters.
- **Permissions are cached in the token.** A revoked right takes effect when the token expires.
