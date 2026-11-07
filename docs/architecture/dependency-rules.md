# Dependency rules

The solution has six projects with one direction of dependency: inward. Each project may only
reference the projects below it in this list, never a project above it.

## The allowed graph

```
MyMES.Domain            → (nothing)

MyMES.Application       → Domain, EF Core
MyMES.PluginSystem      → Application, ASP.NET Core

MyMES.Infrastructure    → Domain, Application, PluginSystem, ASP.NET Core Identity, EF Core

MyMES.Api               → Application, Infrastructure, PluginSystem
MyMES.Web               → Application
```

## The rules, and why each one exists

### 1. The domain depends on nothing

Not EF Core, not ASP.NET, not DI, not another MyMES project.

Entities are plain POCOs. The business rules have to outlive the frameworks around them: the moment
`ProductionOrder` knows about `DbContext`, upgrading EF Core becomes a change to the rules.

### 2. The application layer holds the business logic

Services, DTOs, validation and mapping live here. It defines `IApplicationDbContext`, which
exposes `DbSet<T>` and `SaveChangesAsync`; Infrastructure implements it.

The application layer references EF Core as a library. That is a deliberate exception: EF already
is a unit of work with a queryable repository, and hiding it behind a generic repository only
removes capability.

### 3. The Blazor application cannot reach the database

`MyMES.Web` references `MyMES.Application` for DTOs. Nothing else.

This is the rule most worth defending. The moment the UI can open a `DbContext`, somebody will
write a query in a page, and the authorisation for that data now lives behind a screen instead of
behind the API. Keeping the UI on HTTP means every rule is enforced in exactly one place.

### 4. Controllers know nothing about the database

Controllers call services; services call `IApplicationDbContext`. A controller that reached for
the concrete `MyMesDbContext` would skip the business rules.

## Adding a reference

If a new reference would break a rule, the reference is usually not the answer:

| You want to... | Do this instead |
|---|---|
| Query the database from the UI | Add an endpoint and a DTO |
| Use a framework type in the domain | Keep the domain a plain POCO |
| Share a helper | Put it in `MyMES.Application` |

If a rule genuinely needs to change, update this document in the same commit and say why in the
message. A rule nobody can explain is worse than no rule.
