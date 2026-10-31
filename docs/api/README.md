# The API

Base address `/api/v1`. Swagger is at `/swagger` in Development, and elsewhere when
`Swagger:Enabled` is true.

## Authentication

```
POST /api/v1/auth/login
{ "userName": "admin", "password": "..." }
```

Returns a bearer token and the caller's roles. Send it on every other call:

```
Authorization: Bearer eyJhbGciOi...
```

Tokens last an hour by default (`Jwt:ExpiryMinutes`). There is no refresh token: the user signs
in again.

SignalR cannot set headers on a WebSocket handshake, so the hub accepts the token as
`?access_token=` — on `/hubs/*` paths only.

## Versioning

The version is in the path. A URL that names its version is self-describing in logs, in a proxy
and in Swagger. Version 2 becomes a sibling group mapped from the same modules, so both can be
served at once during a migration.

## Errors

Every failure is RFC 7807 `ProblemDetails` with a machine-readable `code`:

```json
{
  "title": "Conflict",
  "detail": "Machine code CNC-01 is already registered.",
  "status": 409,
  "code": "machine.code_in_use",
  "traceId": "00-a1b2...",
  "instance": "/api/v1/machines"
}
```

Branch on `code`, not on `detail`. The message is for a person and may be reworded or translated;
the code will not change.

| Status | Meaning |
|---|---|
| 400 | Input failed validation |
| 401 | No token, or an invalid one |
| 403 | Signed in, but the permission is missing |
| 404 | No such resource |
| 409 | The operation conflicts with the current state (duplicate key, illegal transition) |
| 423 | Account locked after repeated failed sign-ins |
| 500 | Unexpected. Detail is withheld outside Development; quote the `traceId` |

## Endpoints

### Machines — `MesPermissions.Machines*`

| | |
|---|---|
| `GET /machines` | Paged, filter by `status` and `search` |
| `GET /machines/{id}` | |
| `POST /machines` | |
| `PUT /machines/{id}` | Name and description; the code is immutable |
| `POST /machines/{id}/status` | Separate right: operators change state without editing the register |

### Production orders — `MesPermissions.Production*`

| | |
|---|---|
| `GET /production-orders` | Paged, running orders first |
| `GET /production-orders/{id}` | |
| `POST /production-orders` | Creates as draft |
| `POST /production-orders/{id}/release` | |
| `POST /production-orders/{id}/start` | Also resumes after a hold |
| `POST /production-orders/{id}/hold` | |
| `POST /production-orders/{id}/report` | Books produced quantity |
| `POST /production-orders/{id}/complete` | |
| `POST /production-orders/{id}/cancel` | |

Transitions are separate endpoints, not a `PATCH` on status: they are distinct actions with
distinct rights, and the verb in the URL documents that.

### Products and revisions — `MesPermissions.Products*`

| | |
|---|---|
| `GET /products` | Paged summaries |
| `GET /products/{id}` | Full revision history |
| `GET /products/by-code/{productCode}` | |
| `POST /products` | Optionally with a first draft revision |
| `PUT /products/{id}` | |
| `POST /products/{id}/versions` | Adds a draft |
| `POST /products/{id}/versions/{versionId}/release` | Retires the previously released revision |
| `POST /products/{id}/versions/{versionId}/obsolete` | |
| `POST /products/{id}/deactivate` | |

Releasing needs `products.release`, a different right from `products.write`: drafting a revision
and approving it for production are different responsibilities, and in a regulated plant they are
different people.

### Users — `MesPermissions.Users*`

| | |
|---|---|
| `GET /users` | |
| `GET /users/roles` | |
| `POST /users` | Optionally creates the employee record too |
| `PUT /users/{id}/roles` | Replaces role membership |

### Other

| | |
|---|---|
| `GET /dashboard/summary` | KPI figures; any signed-in user |
| `GET /platform` | Which modules and plugins this instance loaded |
| `GET /quality/pending-inspections` | |
| `GET /inventory/movements` | |

### Outside the versioned group

| | |
|---|---|
| `GET /health/live` | Liveness. Anonymous, and deliberately does not check the database: a failing dependency should not make an orchestrator kill a healthy process |
| `GET /health/ready` | Readiness, including SQL Server. Anonymous |
| `/hubs/shop-floor` | SignalR. Authenticated |

## Real-time

Client methods on the hub:

| Event | Payload |
|---|---|
| `MachineChanged` | `MachineDto` |
| `ProductionOrderChanged` | `ProductionOrderDto` |
| `Telemetry` | `MachineTelemetryReceived` |

`SubscribeToMachine(machineCode)` narrows the feed to one machine, for a terminal mounted on it.
Telemetry is high volume and goes only to a machine's own subscribers.

## Paging

List endpoints take `page` and `pageSize` and return:

```json
{ "items": [], "page": 1, "pageSize": 25, "totalCount": 0, "totalPages": 0,
  "hasNextPage": false, "hasPreviousPage": false }
```

`pageSize` is clamped to 200 server-side, so a client cannot ask for the whole table.
