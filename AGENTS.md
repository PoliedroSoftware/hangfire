# AGENTS.md

## Project

.NET 10 ASP.NET Core Hangfire service for electronic invoice automation (Poliedro). Single-project solution (no test project).

## Commands

```bash
dotnet restore              # restore packages
dotnet build                # compile
dotnet run --project PoliedroHangFire   # run locally (ports 5254 / 7008)
```

No tests, linter, formatter, or typecheck commands exist.

## Architecture (single `.csproj`)

| Layer | Path | Role |
|---|---|---|
| Domain | `Domain/ClientBilling/Entities/` | Entity models (`ClientBilling`) |
| Application | `Application/*/Interfaces/` | Service interfaces |
| Infrastructure | `Infrastructure/External/` | HTTP adapters to external billing APIs |
| Infrastructure | `Infrastructure/Observability/` | Prometheus metrics (`HangfireMetrics`) |
| WebApi | `WebApi/` | `Program.cs`, health checks, Hangfire dashboard filter |
| HangfireJobs | `HangfireJobs/ConfigJbos/` | Recurring job registration |

**Important**: `Infrastructure/Persistence/` and `Infrastructure/Resource/` are **excluded from compilation** (csproj `Remove` directives).

## Hangfire

- **Storage**: MySQL via `MySqlStorage` with `TablesPrefix = "Hangfire"`
- **Worker count**: 5
- **Connection string**: `MYSQL_CONNECTION` env var > `ConnectionStrings:HangfireConnection` in appsettings.json
- **Dashboard**: `/hangfire` — **no auth** (`DevDashboardAccessFilter` always returns `true`)
- **Job registration**: At startup, `ConfigJobs.RegisterJobsAsync` fetches clients from `External:ClientsUrl` and registers one recurring job per client: `facturacion-cliente-{id}` at `Cron.MinuteInterval(iterations)`.

**Connection string must include** `Pooling=True;Max Pool Size=200;Connection Lifetime=300;Connection Idle Timeout=60;` to avoid pool exhaustion. If using `MYSQL_CONNECTION` env var, add these parameters there too. For local Docker setup, also add `Connection Timeout=5;Default Command Timeout=60;`.

**Job flow**: Recurring job (`PendingInvoicesService.InvoicePendingAsync`) → checks pending invoices → enqueues `BackgroundJob.Enqueue<IInvoicesEmitterBIlling>` to emit.

## External APIs (config key `External:*`)

| Config | Purpose |
|---|---|
| `ClientsUrl` | GET list of clients |
| `PendingInvoicesUrl` | GET pending invoices (Bearer token auth) |
| `EmitInvoicesUrl` | POST to emit invoices |

All HTTP calls include header `X-Environment: production-billing`.

## Endpoints

| Route | Description |
|---|---|
| `/hangfire` | Hangfire dashboard (no auth) |
| `/health` | Detailed JSON health check |
| `/health/simple` | Simple health check (used by Docker `HEALTHCHECK`) |
| `/metrics` | Prometheus metrics (prometheus-net) |

## Prometheus metrics

Defined in `HangfireMetrics` (singleton, wired in DI). Exposes counters (`billing_jobs_executed_total`, `billing_emit_errors_total`, etc.), gauges, and histograms for job duration. HTTP metrics middleware (`UseHttpMetrics`) is enabled.

## CI/CD (GitHub Actions)

`.github/workflows/aws.yml` — triggers on PR merge to `main`, `release/*`, `releasecandidate/*`:

1. `dotnet restore` + `dotnet build --configuration Release`
2. `dotnet test --configuration Release --no-build`
3. SonarCloud scan
4. Docker build → push to ECR + Docker Hub
5. Deploy to AWS ECS (`update-service --force-new-deployment`)

## Docker

Multi-stage build (container port `8080`). `HEALTHCHECK` runs `curl -f http://localhost:8080/health/simple`.

### Docker Compose (local development)

`docker-compose.yml` at repo root runs MySQL 8.0 (`hangfire-mysql`) + Hangfire (`hangfire-app`). The `MYSQL_CONNECTION` env var points to the local MySQL container. Tables are auto-created by `PrepareSchemaIfNecessary=true`.

```bash
docker compose up -d --build
# App at http://localhost:8080
# MySQL at localhost:3307 (exposed for admin tools)
```

**Warning**: First-time startup requires MySQL to initialize (~30s). Hangfire waits via `depends_on: condition: service_healthy`. Check logs with `docker compose logs -f`.

### Known issue: remote MySQL connectivity

When using the remote MySQL (`150.136.181.118`), the Hangfire server may experience **command timeout + SocketException 10060** in components like `ExpirationManager`. This is a network-level connectivity issue (not wait_timeout or connection pool). If it persists, deploy with the dedicated MySQL container approach (`docker-compose.yml`).

## Namespace convention

`PoliedroHangFire.{Layer}.{Subdomain}.{...}` — no separate assemblies per layer.
