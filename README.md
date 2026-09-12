# NetWatch — Network & Service Availability Monitoring

**Live demo:** https://netwatch-rbmr.onrender.com — free tier, so the first visit
after idle takes ~30s to wake. Ask for credentials, or run it locally in two commands.

A self-hosted monitoring system: register devices (routers, servers, websites), attach
probes (ICMP ping, TCP connect, HTTP), and watch live health, latency charts, uptime
statistics and incident history on a real-time dashboard — in English or Arabic.

**Stack:** ASP.NET Core 9 Web API · EF Core 9 (SQLite / SQL Server) · ASP.NET Core
Identity + JWT · SignalR · Angular 19 · xUnit (114 tests) · Karma (19 tests) · Docker ·
GitHub Actions

---

## Why this project

Most monitoring demos poll a URL and print a status code. NetWatch is built the way a
production tool is built, and every non-obvious decision is documented in the code:

- **A debounced state machine, not a naive check.** One dropped packet must not page
  anyone. A probe goes *Down* only after N consecutive failures and recovers only after
  M consecutive successes. The state machine lives on the domain entity with zero I/O,
  so it is tested exhaustively without a network or a clock.
- **Incidents are outages, not failed checks.** One record spans an outage from first
  failure to auto-resolution — with severity escalation (degraded → down), operator
  acknowledgement, and a running failure count.
- **A real scheduler.** A `BackgroundService` on a `PeriodicTimer` (no drift), a DI
  scope per check (`DbContext` is not thread-safe), a `SemaphoreSlim` cap on concurrent
  probes, and a thundering-herd limit per tick. A retention job purges old results in
  bounded batches.
- **Provider-agnostic data layer.** No raw SQL, no provider-specific types. SQLite for
  development and CI, SQL Server for production — switched by configuration, with
  separate migration assemblies because migrations carry engine-specific SQL. CI fails
  if the model drifts from either snapshot.
- **Security that would survive a review.** Refresh tokens stored as SHA-256 hashes and
  rotated on every use; replaying a retired token revokes the whole chain. Login
  responses identical for unknown email vs wrong password. Identity lockout after 5
  attempts. No secret of any kind in the repository — Development generates an ephemeral
  signing key, everything else refuses to boot without one.
- **Live by push, not poll.** SignalR broadcasts every check result and incident event.
  The Angular dashboard patches its state in place; the connection indicator is visible,
  so a stale dashboard says so.
- **Arabic as a first-class language.** Runtime EN/AR switching, `dir` set on the
  document element, CSS logical properties everywhere (no RTL stylesheet fork), and
  hostnames/URLs pinned LTR inside RTL text. A test enforces translation parity.

## Architecture

```
┌────────────────────┐     REST + SignalR      ┌─────────────────────────────┐
│  Angular 19 SPA    │ ◄─────────────────────► │  NetWatch.Api               │
│  (signals, lazy    │                         │  controllers · JWT · hub    │
│   routes, RTL)     │                         ├─────────────────────────────┤
└────────────────────┘                         │  NetWatch.Application       │
                                               │  services · DTOs · rules    │
        probes                                 ├─────────────────────────────┤
   ICMP / TCP / HTTP  ◄────────────────────────│  NetWatch.Infrastructure    │
        ▲                                      │  EF Core · Identity ·       │
        │                                      │  scheduler · probe engine   │
   monitored network                           ├─────────────────────────────┤
                                               │  NetWatch.Domain            │
                                               │  entities · state machine   │
                                               └─────────────────────────────┘
```

Dependencies point inward only. The domain has no references; the probe state machine
and incident lifecycle are pure and fully unit-tested.

## Run it locally

Prerequisites: .NET 9 SDK, Node 22+.

```bash
# API (terminal 1) — SQLite, migrations, seed data, ephemeral JWT key: zero setup
cd src/NetWatch.Api
dotnet run --urls http://localhost:5080
```

```bash
# Client (terminal 2)
cd client/netwatch-web
npm install
npm start
```

Open http://localhost:4200 and sign in with the development seed account
`admin@netwatch.local` / `NetWatch#Dev1` (Development only — production refuses to seed
an admin without an explicit password). Swagger lives at http://localhost:5080/swagger.

Three sample devices are seeded so the dashboard shows live data immediately.

### Tests

```bash
dotnet test                                    # 114 backend tests
cd client/netwatch-web && npm test             # 19 client tests
```

## Caching

The dashboard summary is the most expensive read in the application — four repository
round trips, one an aggregate over the whole probe-result table — and every open
dashboard asks for it on an interval.

It is also the one read where a stale answer is a **correctness** bug: a monitoring wall
showing "all up" thirty seconds into an outage is worse than no wall. So the entry is not
left to a TTL. `CacheInvalidatingMonitoringNotifier` retires it the moment a probe changes
state or an incident opens or resolves; the TTL is only a backstop for a lost
invalidation. Checks that change nothing — the overwhelming majority, since every probe
reports on every tick — deliberately leave the cache alone.

Invalidation rotates a version token folded into the cache key rather than deleting keys.
`IDistributedCache` has no wildcard delete, and scanning Redis for matching keys is the
operation the Redis docs warn against in production; rotating is one O(1) write no matter
how many windows are cached.

Redis is opt-in by connection string:

```bash
# Without this, an in-process distributed cache is registered instead — a fresh clone
# and the test host need no broker installed.
ConnectionStrings__Redis="localhost:6379"
```

`docker-compose.yml` and the Azure template both wire it up. A single-instance deployment
is served correctly by the in-process fallback; it only stops being adequate once a
second replica exists, at which point each would cache separately.

## Deploy to Azure

`infra/main.bicep` provisions App Service (Linux container), Azure Cache for Redis and
Azure SQL, and wires the connection strings into the app settings. The
`Deploy to Azure` workflow runs the tests, pushes the image to GHCR and applies the
template.

```bash
az group create --name netwatch-rg --location westeurope
az deployment group create   --resource-group netwatch-rg   --template-file infra/main.bicep   --parameters appName=<globally-unique-name>                sqlAdminLogin=<login>                sqlAdminPassword=<password>                jwtSigningKey=<base64-key>
```

The workflow authenticates with OIDC, so no publish profile or service-principal secret
is stored in the repository. It needs `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`,
`AZURE_SUBSCRIPTION_ID`, `SQL_ADMIN_LOGIN`, `SQL_ADMIN_PASSWORD` and `JWT_SIGNING_KEY`
as repository secrets, and deployment is `workflow_dispatch` only — a monitoring system
that redeploys itself on every push to main goes blind during each rollout.

The App Service plan is B1 rather than the free tier because the monitoring
`BackgroundService` needs `alwaysOn`: on a tier that idles the worker out, checks stop
running whenever nobody has the dashboard open.

## Run it with Docker

```bash
cp .env.example .env    # fill in JWT_SIGNING_KEY, MSSQL_SA_PASSWORD, ADMIN_PASSWORD
docker compose up --build
```

One container serves both the API and the built SPA from a single origin (no CORS in
production); a second runs SQL Server with a health-gated startup. ICMP probes need
`NET_RAW` (granted in the compose file) — TCP/HTTP probes work unprivileged.

## Roles

| Role | Can |
|---|---|
| **Viewer** | See dashboards, history, incidents |
| **Operator** | + add/edit devices and probes, acknowledge incidents, run checks |
| **Admin** | + delete devices, register users |

Enforced server-side on every endpoint; the client merely hides what the API would
reject anyway.

## Design decisions worth reading in the code

| Decision | Where |
|---|---|
| Debounce thresholds & state transitions | `Domain/Entities/Probe.cs` |
| Failure-mode classification (refused ≠ timeout ≠ DNS) | `Infrastructure/Probing/*Executor.cs` |
| No-drift scheduling, scope-per-check, bounded concurrency | `Infrastructure/Monitoring/MonitoringSchedulerService.cs` |
| Refresh-token rotation & theft response | `Infrastructure/Identity/AuthService.cs` |
| Refresh-stampede guard on the client | `client/.../auth.interceptor.ts` |
| Per-provider migrations & drift check in CI | `NetWatch.Migrations.*` + `.github/workflows/ci.yml` |
| p95 vs average, and why a dashboard needs both | `Application/Monitoring/LatencyStatistics.cs` |
| Event-driven cache invalidation, and why TTL alone is wrong here | `Application/Dashboard/CachedDashboardService.cs` |
| Cache failures degrade latency, never availability | `Infrastructure/Caching/DistributedCacheService.cs` |
| RTL via logical properties, LTR-pinned hostnames | `client/.../styles.scss`, `i18n.service.ts` |

## Known trade-offs

- Tokens are kept in `localStorage` so the SPA survives a refresh; the same-origin
  production deployment could move to HttpOnly cookies + CSRF protection instead.
- The SignalR hub broadcasts to all authenticated clients; per-group filtering (e.g. by
  site) would be the next step at larger scale.
- Percentiles are computed over the charted sample, not the full window — documented on
  the DTO, deliberate for provider portability.

## License

MIT
