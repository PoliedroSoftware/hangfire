# AGENTS.md

## Project identity

- .NET 10 ASP.NET Core Web App for Poliedro billing automation via Hangfire.
- Single project at `PoliedroHangFire/`, solution file at `PoliedroHangFire.sln`.

## Build, test, run

```bash
dotnet restore
dotnet build
dotnet test
```

No test projects exist yet — CI runs `dotnet test` expecting `[Fact]`/`[Test]` attributes somewhere.

Run locally: `dotnet run --project PoliedroHangFire` (listens on `http://localhost:5254` and `https://localhost:7008`).

## Excluded from compilation

The `.csproj` explicitly removes these folders from compile, content, and resource inclusion:
- `Infrastructure\Persistence\**`
- `Infrastructure\Resource\**`

Do NOT place code in those directories.

## Hangfire configuration

- **Storage**: `MySqlStorage` with table prefix `Hangfire`.
- **Connection string override**: Set `MYSQL_CONNECTION` env var to override `ConnectionStrings:HangfireConnection`. The start-up reads the env var first, falls back to `appsettings.json`.
- **Server**: `AddHangfireServer(options => options.WorkerCount = 5)`.
- **Dashboard**: mounted at `/hangfire`. No auth (the `DevDashboardAccessFilter` always returns `true`).
- `Hangfire.MemoryStorage` is in the dependencies but NOT used at runtime — it's only referenced in the package list.

## Job registration flow (critical)

1. On startup, `ConfigJobs.RegisterJobsAsync` is called.
2. It calls `IClientService.GetClientBillingsAsync()` to fetch clients from the external billing API.
3. For each client, a **recurring Hangfire job** is registered with id `facturacion-cliente-{ClientBillingElectronicId}` running at `Cron.MinuteInterval(cliente.Iterations)` minutes.
4. Each recurring job calls `IPendingInvoicesBilling.InvoicePendingAsync(...)`.
5. If pending invoices are found, it enqueues a fire-and-forget job via `BackgroundJob.Enqueue<IInvoicesEmitterBIlling>(...)`.

**Important**: Jobs are not static — they are created dynamically from API data. If `GetClientBillingsAsync` fails, jobs are NOT registered but the app continues running (catch in Program.cs).

## Health checks

- `/health` — detailed JSON with per-check status, handled by custom response writer (Program.cs:49).
- `/health/simple` — raw minimal response, used by Docker HEALTHCHECK.
- Both require `AddHealthChecks()` and `MapHealthChecks()`, which are wired in Program.cs.

## Docker

- **Build context**: repository root (not `PoliedroHangFire/`).
- **Target framework**: `net10.0`, base image `mcr.microsoft.com/dotnet/aspnet:10.0`.
- **Exposed ports**: 8080, 8081.
- **Healthcheck**: `curl -f http://localhost:8080/health/simple`.

## CI/CD (GitHub Actions)

- **Workflow**: `.github/workflows/aws.yml`.
- **Triggers**: push to `Feature/test-github-actions`; PRs (opened, sync, reopen, close) to `main`, `release/*`, `releasecandidate/*`.
- **Jobs**: `dotnet` (restore, build, test) → `sonar` (SonarCloud) → on merge: `docker` (push to AWS ECR), `docker-hub` (push to Docker Hub), `deploy` (AWS ECS force-new-deployment).
- Docker and deploy only run when `github.event.pull_request.merged == true`.

## External API dependencies

All external URLs are configured in `appsettings.json` under `External`:
- `ClientsUrl` — fetch client list
- `PendingInvoicesUrl` — get pending invoices per client
- `EmitInvoicesUrl` — post invoices for emission

All HTTP clients send header `X-Environment: production-billing`.

## Project structure conventions

- `Domain/` — entities (e.g., `ClientBilling`)
- `Application/{Feature}/Interfaces/` — service interfaces
- `Application/{Feature}/DTOs/` — empty, reserved
- `Application/{Feature}/Services/` — empty, reserved
- `Infrastructure/External/Billing/Adapters/{Feature}/` — service implementations
- `HangfireJobs/ConfigJbos/Billing/` — job scheduling
- `WebApi/` — Program.cs, filters, health checks

## Security notes

- `appsettings.json` contains a hardcoded MySQL password. Do not log this file or expose it.
- The Hangfire dashboard has no auth (open to anyone who reaches the endpoint).
