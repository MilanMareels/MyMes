# Database migrations

EF Core migrations, through the .NET CLI. The migration files are committed to the repository and
are the only source of truth for the schema.

## Setup

The EF tool is pinned in the tool manifest, so everyone gets the same version:

```bash
dotnet tool restore
```

## The two projects

Every command names two projects, and the distinction matters:

| Flag | Project | Why |
|---|---|---|
| `--project` | `src/Infrastructure/MyMES.Persistence` | Where the `DbContext` and the migrations live |
| `--startup-project` | `src/Host/MyMES.Api` | Where the configuration and the service registrations live |

## Creating a migration

```bash
dotnet ef migrations add AddMaintenanceSchedule --project src/Infrastructure/MyMES.Persistence --startup-project src/Host/MyMES.Api
```

The files land in `src/Infrastructure/MyMES.Persistence/Migrations`. Read the generated `Up` and
`Down` before committing. EF is good, not clairvoyant: a rename usually comes out as a drop and
an add, which silently throws the data away.

A database connection is **not** required to create a migration.
`DesignTimeDbContextFactory` builds the context without starting the API, so this works on a
laptop with nothing running.

## Applying a migration

Locally:

```bash
dotnet ef database update --project src/Infrastructure/MyMES.Persistence --startup-project src/Host/MyMES.Api
```

Against a different server, without editing any file:

```bash
MYMES_MIGRATIONS_CONNECTION="Server=staging;Database=MyMES;User Id=deploy;Password=..." dotnet ef database update --project src/Infrastructure/MyMES.Persistence --startup-project src/Host/MyMES.Api
```

On Windows PowerShell:

```powershell
$env:MYMES_MIGRATIONS_CONNECTION = "Server=staging;Database=MyMES;User Id=deploy;Password=..."; dotnet ef database update --project src/Infrastructure/MyMES.Persistence --startup-project src/Host/MyMES.Api
```

## Producing a script instead

For any environment where a person has to approve the change:

```bash
dotnet ef migrations script --idempotent --project src/Infrastructure/MyMES.Persistence --startup-project src/Host/MyMES.Api --output migrate.sql
```

`--idempotent` guards every step with a check against `__EFMigrationsHistory`, so the script is
safe to run against a database at any migration level.

## Undoing

Remove the last migration, if it has **not** been applied or shared:

```bash
dotnet ef migrations remove --project src/Infrastructure/MyMES.Persistence --startup-project src/Host/MyMES.Api
```

Roll a database back to an earlier migration:

```bash
dotnet ef database update <PreviousMigrationName> --project src/Infrastructure/MyMES.Persistence --startup-project src/Host/MyMES.Api
```

Never delete a migration that has been applied anywhere but your own machine. Write a new one
that corrects it.

## AutoMigrate

`Database:AutoMigrate` applies pending migrations during start-up.

| Environment | Setting | Reason |
|---|---|---|
| Development | `true` | One command and the app runs |
| Docker compose | `true` | The stack is meant to come up with `docker compose up` |
| Staging, production | `false` | Migrations belong in a controlled release step against a backed-up database, not in whichever instance happens to start first |

With it off, `DatabaseInitialiser` still checks and logs a warning naming the pending migrations,
rather than starting silently against a schema it does not match.

## Rules

- **Never `EnsureCreated()`.** It builds a schema with no migration history, after which no
  migration can ever be applied to that database. The code does not call it anywhere.
- **Never drop or recreate automatically.** Nothing in the codebase deletes a database.
- **Migrations are committed.** They are the schema's history and its only reliable record.
- **One migration per logical change.** A migration called `Fixes` helps nobody in a year.
- **Review the generated SQL for anything destructive.** A dropped column is permanent.

## Current schema

The initial migration creates both schemas:

| Schema | Tables |
|---|---|
| `mes` | `Machines`, `Products`, `ProductVersions`, `ProductionOrders`, `Employees`, `__EFMigrationsHistory` |
| `identity` | `Users`, `Roles`, `UserRoles`, `UserClaims`, `UserLogins`, `UserTokens`, `RoleClaims` |

Verified against SQL Server, not only generated:

- quantities are `decimal(18,4)`, never floating point
- `IX_ProductVersions_ProductId_VersionNumber` is unique: a revision label is unique within its
  product
- `Products → ProductVersions` cascades: revisions belong to the product
- `ProductionOrders → ProductVersions` is `NO ACTION`: a revision an order was built against must
  never disappear, or the as-built record loses the thing it exists to prove
