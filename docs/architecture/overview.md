# Architecture overview

MyMES is a **modular monolith** with Clean Architecture layering. One process, one database, one
deployment unit, divided internally so that pieces can be pulled out later without a rewrite.

## Why a modular monolith

A MES is one transactional story. An order releases, a machine runs it, quantities are booked,
stock moves and an inspection is raised, and a plant manager expects those to agree with each
other. Splitting that across services at the start buys network calls, eventual consistency and
distributed debugging in exchange for independence nobody needs yet.

The monolith keeps the transaction, and the module boundaries keep the option. Every rule that a
service boundary would enforce physically is enforced here by a test instead:

- modules do not reference each other
- modules reach data through the application layer, never a `DbContext`
- the UI reaches data only over HTTP

When a module genuinely needs its own release cadence or its own database, the seam already
exists: give it its own `DbContext` on its own schema, move the module behind an HTTP contract,
and the event bus registration changes from in-process to a broker.

## The layers

### MyMES.Domain

Entities, value objects, enums, domain events and the invariants that protect them. **References
nothing** — not EF Core, not ASP.NET, not another MyMES project.

Business rules live here, not in services. `ProductionOrder` decides whether it may start;
`Product` decides that only one revision can be released at a time. A service that reached into
these objects to set a status would be the beginning of an anemic model, and the architecture
tests do not prevent that — code review does. The aggregates are deliberately shaped so the
wrong thing is hard to write: setters are private and every transition is a named method.

### MyMES.Contracts

The public wire format: DTOs and their own enums. No dependency on the domain.

The duplication is the point. `MachineStatus` and `MachineStatusDto` have the same members today,
and the day the domain gains an internal state that clients should not see, the contract does not
move. `MappingTests` iterates both enums in both directions, so the two cannot drift silently.

### MyMES.Application

Use cases, validation and mapping. Defines `IApplicationDbContext`, which exposes `DbSet<T>` and
`SaveChangesAsync`.

This is the one place Clean Architecture purity is traded away deliberately: the application layer
references EF Core. The alternative, a generic `IRepository<T>`, hides `Include`, projection and
`AsNoTracking` behind an abstraction that can only ever be a worse version of what EF already
offers. The boundary that actually protects the business rules is the domain's, and that one is
absolute.

Services return `Result` / `Result<T>` for expected failures (duplicate code, missing record,
illegal transition) and let genuine faults throw. The host maps the first to a status code and
the second to a 500.

### MyMES.Persistence

`MyMesDbContext`, one `IEntityTypeConfiguration<T>` per entity, and the migrations.

`SaveChangesAsync` does three things in order: stamp the audit columns, save, then publish the
domain events raised during the change. Publishing *after* the commit means a handler never
reacts to work that is then rolled back. The cost is that a handler failing afterwards cannot
undo the commit, which is why handlers log and continue rather than throw.

Two schemas: `mes` for business tables, `identity` for authentication. One migration history,
and a clean split to build on if a module ever leaves.

### MyMES.Identity

`ApplicationUser`, `ApplicationRole`, the role/permission table and the seeder.

Authorisation is permission-based. An endpoint says
`RequireAuthorization(MesPermissions.MachinesWrite)` and never names a role. Roles hold
permissions; a module or plugin can register a new permission at start-up and the host turns it
into a policy automatically. Changing who may do what is then configuration, not an edit to
endpoint code.

### Modules

Four: Machines, Production, Quality, Inventory. Each owns a slice of the API surface and
registers its own services and event handlers.

Quality and Inventory are the interesting ones for the architecture, even though they are small.
Neither references the Production module. Both react to a domain event raised by a production
order — a completed order raises a final inspection, a booking posts a stock receipt — which is
exactly the communication pattern the rest of the system is expected to follow.

### Hosts

- **MyMES.Api** — the only process that touches the database. Minimal APIs, JWT, Swagger,
  SignalR, health checks, module registration.
- **MyMES.Web** — Blazor Server. References `MyMES.Contracts` and nothing else from the core.
- **MyMES.MachineService** — a worker. Just another API client, with its own service account.

## Cross-cutting

### Errors

One shape everywhere: RFC 7807 `ProblemDetails`, with a machine-readable `code` extension that
clients branch on instead of parsing the message. `ResultExtensions.ToProblem` maps
`ErrorKind` to a status code, so a module's endpoint and a host's endpoint fail identically.

Outside Development the 500 detail is withheld: exception text leaks schema, paths and library
versions. The trace id stays, so a report can be correlated with the log.

### Logging

Serilog, structured. Messages use named placeholders, never interpolation, so the properties stay
queryable. The console template is pinned to the invariant culture; a log whose timestamps depend
on the server's locale is a log that cannot be compared across machines.

### Real-time

`IRealtimeNotifier` lives in `MyMES.Messaging` and knows nothing about SignalR. The API supplies
the SignalR implementation; the worker and the test suite get a no-op. That is the same "replace
a default implementation" seam extensions use.

### Time

Nothing reads `DateTime.UtcNow` directly except `SystemDateTimeProvider`. Everything else takes
`IDateTimeProvider`, which is what lets the domain tests pin a clock and assert on timestamps.

## Known gaps

- **No outbox.** Event publication is not transactional.
- **No distributed concurrency control.** Two operators booking the same order simultaneously is
  last-write-wins on quantities. A row version on `ProductionOrder` is the fix when it matters.
- **Permissions are cached in the token.** A revoked right takes effect when the token expires.
