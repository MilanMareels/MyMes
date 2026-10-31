# Dependency rules

Every rule here is asserted in `tests/MyMES.ArchitectureTests`. Prose does not fail a build;
these do.

## The allowed graph

```
MyMES.Domain            → (nothing)
MyMES.Contracts         → (nothing)
MyMES.Abstractions      → Microsoft.Extensions.* abstractions only
MyMES.EventBus          → Abstractions
MyMES.PluginSystem      → Abstractions, ASP.NET Core (shared framework)
MyMES.Localization      → Microsoft.Extensions.* abstractions only

MyMES.Application       → Domain, Contracts, Abstractions, EventBus, EF Core
MyMES.Identity          → Domain, Abstractions, ASP.NET Core Identity
MyMES.Persistence       → Domain, Application, Identity, Abstractions, EF Core
MyMES.Messaging         → Abstractions, Contracts, EventBus
MyMES.Infrastructure    → Application, Persistence, Identity, Messaging, EventBus, PluginSystem

MyMES.Modules.*         → Application, Domain, Contracts, Abstractions, EventBus, PluginSystem

MyMES.Api               → everything above
MyMES.Web               → Contracts, Abstractions, Localization
MyMES.MachineService    → Contracts, Abstractions, Messaging
```

## The rules, and why each one exists

### 1. The domain depends on nothing

Not EF Core, not ASP.NET, not DI, not another MyMES project.

The business rules have to outlive the frameworks around them. The moment `ProductionOrder` knows
about `DbContext`, upgrading EF Core becomes a change to the rules, and testing a rule means
standing up a database.

*Asserted by* `Domain_does_not_depend_on_any_framework`,
`Domain_does_not_depend_on_any_other_MyMES_project`.

### 2. Contracts do not depend on the domain

The wire format must be able to stay stable while the domain changes. Sharing types makes every
domain rename a breaking API change for every client.

The duplicated enums are the price, and `MappingTests` is what keeps them honest: adding a domain
member without a contract member fails the test run.

*Asserted by* `Contracts_do_not_depend_on_the_domain`.

### 3. The application layer does not depend on infrastructure

It defines `IApplicationDbContext`; persistence implements it. Dependencies point inward, always.

The application layer does reference EF Core as a library. That is a deliberate exception,
explained in the overview: EF already is a unit of work with a queryable repository, and hiding
it behind a generic repository only removes capability.

*Asserted by* `Application_does_not_depend_on_infrastructure_or_on_a_host`.

### 4. The Blazor application cannot reach the database

`MyMES.Web` references `MyMES.Contracts`, `MyMES.Abstractions` and `MyMES.Localization`. Nothing
else.

This is the rule most worth defending. The moment the UI can open a `DbContext`, somebody will
write a query in a page, and the authorisation for that data now lives behind a screen instead of
behind the API. Keeping the UI on HTTP means every rule is enforced in exactly one place.

*Asserted by* `The_Blazor_application_cannot_reach_the_database`.

### 5. A module does not depend on infrastructure

Modules use `IApplicationDbContext`, never `MyMesDbContext`. A module that reached for the
concrete context could not be moved to its own database later, and could not be unit-tested
without one now.

*Asserted by* `A_module_does_not_depend_on_infrastructure`.

### 6. Modules do not depend on each other

Not on another module's implementation, and not on another module's project at all.

Modules communicate three ways:

1. **Domain events.** Quality raises a final inspection when it sees
   `ProductionOrderCompletedEvent`. It has never heard of the Production module.
2. **Contracts.** A shared DTO is a published interface; depending on one is fine.
3. **Application services.** A module may call an application-layer service another module also
   uses, because that service belongs to the application layer, not to a module.

A direct project reference is what turns a modular monolith back into a tangle, one "just this
once" at a time.

*Asserted by* `Modules_do_not_depend_on_each_other`.

### 7. Persistence does not depend on a host

Infrastructure is consumed by hosts and must not know which host is consuming it.

*Asserted by* `Persistence_does_not_depend_on_a_host`.

## Adding a reference

If a new reference would break a rule, the reference is usually not the answer:

| You want to... | Do this instead |
|---|---|
| Use another module's data | Subscribe to its domain event, or move the shared concept into the application layer |
| Query the database from a module | Use `IApplicationDbContext` |
| Show data in the UI | Add an endpoint and a contract |
| Use a framework type in the domain | Define an abstraction in `MyMES.Abstractions` and implement it in infrastructure |
| Share a helper between modules | Put it in `MyMES.Abstractions` or a building block |

If a rule genuinely needs to change, change the test in the same commit and say why in the
message. A rule nobody can explain is worse than no rule; a rule that is quietly deleted is worse
than both.
