Purpose
This file gives Copilot sessions concise, actionable instructions for working on the PoliedroHangFire repository: how to build/run/test, the high-level architecture, and repository-specific conventions Copilot should preserve when suggesting changes.

Build / Run / Test / Lint
- Restore dependencies: dotnet restore
- Build (Release): dotnet build ./PoliedroHangFire/PoliedroHangFire.csproj -c Release
- Publish (for Docker or release): dotnet publish ./PoliedroHangFire/PoliedroHangFire.csproj -c Release -o ./publish
- Run locally (development): cd PoliedroHangFire && dotnet run
- Docker build & run (image exposes 8080): docker build -t poliedrohangfire . && docker run -e MYSQL_CONNECTION="<conn>" -p 8080:8080 poliedrohangfire
- Health endpoints: GET /health (JSON detailed) and GET /health/simple (basic)

Tests
- CI uses: dotnet test --configuration Release --no-build
- Run full test suite locally: dotnet test
- Run a single test: dotnet test --filter "FullyQualifiedName~MyNamespace.MyTestClass.MyTestMethod" (adjust FullyQualifiedName to match your test)

Linting / Static analysis
- No repository-local linter config found. CI performs SonarCloud scan (see .github/workflows/aws.yml). Avoid adding unrelated global changes without CI updates.

High-level architecture (big picture)
- Single ASP.NET Core WebApi application (PoliedroHangFire) that hosts a Hangfire Server and Hangfire Dashboard.
- Storage: Hangfire uses MySQL (MySqlStorage) with TablesPrefix = "Hangfire". Connection string is read from env MYSQL_CONNECTION or configuration key ConnectionStrings:HangfireConnection.
- Startup (Program.cs):
  - Registers typed HttpClients for external adapters.
  - Registers health checks (ExternalServiceHealthCheck) for critical external services.
  - Configures Hangfire and starts the Hangfire server (WorkerCount = 5).
  - Attempts to register recurring jobs at startup using ConfigJobs.RegisterJobsAsync.
- Jobs flow:
  1. ConfigJobs obtains client list from IClientService.
  2. For each client it creates a recurring job named facturacion-cliente-{ClientBillingElectronicId} with Cron.MinuteInterval(cliente.Iterations) that calls IPendingInvoicesBilling.InvoicePendingAsync(...).
  3. IPendingInvoicesBilling implementation queries external pending-invoices endpoint; if invoices exist it enqueues a BackgroundJob to IInvoicesEmitterBIlling.InvoicesEmitterServicesAsync to emit invoices.

Key files to inspect for behavior and changes
- Program.cs (startup, DI, Hangfire config)
- HangfireJobs/ConfigJbos/Billing/ConfigJobs.cs (dynamic recurring job registration)
- Infrastructure/External/Billing/Adapters/* (ClientService, PendingInvoicesService, InvoicesEmitterBilling) — external integrations
- WebApi/HealthChecks/ExternalServiceHealthCheck.cs
- Dockerfile and .github/workflows/aws.yml (CI pipeline & containerization)

Key conventions and repository-specific notes (preserve these)
- Dependency injection: adapters are registered as typed HttpClient services (e.g., builder.Services.AddHttpClient<IClientService, ClientService>()). Keep method signatures and DI registrations intact when refactoring.
- Config keys used by code: External:ClientsUrl, External:PendingInvoicesUrl, External:EmitInvoicesUrl. MYSQL_CONNECTION env var overrides the Hangfire connection string.
- Job naming: recurring jobs use "facturacion-cliente-{ClientBillingElectronicId}". Do not change this format without updating any callers/monitoring tooling.
- Cron/interval semantics: Iterations property on ClientBilling is interpreted as minutes and passed to Cron.MinuteInterval(...).
- Error handling: startup job registration is tolerant — failures are logged and the app continues. Respect existing try/catch behavior.
- Logging: code uses Console.WriteLine for operational messages; keep messages clear and concise if adding logs.
- Schema migration: MySqlStorageOptions.PrepareSchemaIfNecessary = true is relied on in startup — avoid removing it unless schema management is otherwise handled.

CI notes
- GitHub Actions workflow: .github/workflows/aws.yml
  - Uses dotnet 10.0 for restore/build/test
  - SonarCloud scan step is present (requires SONAR_TOKEN)
  - Docker image build and push steps included (AWS ECR and Docker Hub) and ECS deploy steps when PRs are merged

Files for Copilot to prefer when making suggestions
- Program.cs, ConfigJobs.cs, adapter classes under Infrastructure/External/Billing, the domain entity ClientBilling, and Dockerfile/.github/workflows/aws.yml.

When opening PRs or suggesting changes
- Preserve public interfaces (IPendingInvoicesBilling, IInvoicesEmitterBIlling, IClientService) unless proposing coordinated API changes across DI registration and all usages.
- Keep job registration id format and Cron semantics stable or clearly document migration steps.
- If changing configuration keys, update Dockerfile, README, and CI secrets references.

Relevant developer notes
- Target framework: .NET 10 (used in CI and Dockerfile)
- Health endpoints and Hangfire dashboard are exposed on the main web port; dashboard access is protected by DevDashboardAccessFilter.

Quick links
- Startup: PoliedroHangFire/WebApi/Program.cs
- Jobs: HangfireJobs/ConfigJbos/Billing/ConfigJobs.cs
- External adapters: PoliedroHangFire/Infrastructure/External/Billing/Adapters/
- CI: .github/workflows/aws.yml

Last updated: 2026-07-10

---
If you'd like, Copilot sessions can also be configured to use MCP servers (for example, to run Playwright tests or start a local containerized environment). Would you like to configure any MCP servers for this repo? (yes/no)