# Database migrations

EF Core migrations are managed with Visual Studio's **Package Manager Console** (Tools → NuGet
Package Manager → Package Manager Console). The migration files are committed to the repository and
are the only source of truth for the schema.

Set the two dropdowns in the console before running any command:

| Dropdown | Project | Why |
|---|---|---|
| Default project | `MyMES.Persistence` | Where the `DbContext` and the migrations live |
| Startup project | `MyMES.Api` | Where the configuration and the service registrations live |

## Creating a migration

```powershell
Add-Migration AddMaintenanceSchedule
```

The files land in `src/Infrastructure/MyMES.Persistence/Migrations`. Read the generated `Up` and
`Down` before committing. EF is good, not clairvoyant: a rename usually comes out as a drop and
an add, which silently throws the data away.

A database connection is **not** required to create a migration.
`DesignTimeDbContextFactory` builds the context without starting the API, so this works on a
laptop with nothing running.

## Applying a migration

Locally:

```powershell
Update-Database
```

In Development this is usually unnecessary: the API applies pending migrations itself on start-up
(`Database:AutoMigrate` is `true`).

Against a different server, without editing any file, set the connection string first:

```powershell
$env:MYMES_MIGRATIONS_CONNECTION = "Server=staging;Database=MyMES;User Id=deploy;Password=..."
Update-Database
```

`DesignTimeDbContextFactory` resolves the connection string in this order: the
`MYMES_MIGRATIONS_CONNECTION` environment variable, then the API's user-secrets, then a local SQL
Server fallback. So on a developer machine `Update-Database` targets the same database the running
app uses, with no extra setup.

## Producing a script instead

For any environment where a person has to approve the change:

```powershell
Script-Migration -Idempotent -Output migrate.sql
```

`-Idempotent` guards every step with a check against `__EFMigrationsHistory`, so the script is
safe to run against a database at any migration level.

## Undoing

Remove the last migration, if it has **not** been applied or shared:

```powershell
Remove-Migration
```

Roll a database back to an earlier migration:

```powershell
Update-Database <PreviousMigrationName>
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
| `mes` | `Machines`, `Products`, `ProductVersions`, `ProductionOrders`, `Employees`, `IncomingErpMessages`, `__EFMigrationsHistory` |
| `identity` | `Users`, `Roles`, `UserRoles`, `UserClaims`, `UserLogins`, `UserTokens`, `RoleClaims` |

Verified against SQL Server, not only generated:

- quantities are `decimal(18,4)`, never floating point
- `IX_ProductVersions_ProductId_VersionNumber` is unique: a revision label is unique within its
  product
- `Products → ProductVersions` cascades: revisions belong to the product
- `ProductionOrders → ProductVersions` is `NO ACTION`: a revision an order was built against must
  never disappear, or the as-built record loses the thing it exists to prove
