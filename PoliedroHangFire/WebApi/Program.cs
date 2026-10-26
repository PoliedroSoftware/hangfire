using Hangfire;
using Hangfire.Dashboard;
using Hangfire.MySql;
using PoliedroHangFire.Application.DianResolution.Interfaces;
using PoliedroHangFire.Application.InvoicesEmitterBilling.Interfaces;
using PoliedroHangFire.Application.PendingInvoicesBilling.Interfaces;
using PoliedroHangFire.HangfireJobs.ConfigJbos.Billing;
using PoliedroHangFire.Infrastructure.External.Billing.Adapters.DianResolutionBilling;
using PoliedroHangFire.Infrastructure.External.Billing.Adapters.InvoicesEmitterBilling;
using PoliedroHangFire.Infrastructure.External.Billing.Adapters.PendingInvoicesBilling;
using PoliedroHangFire.WebApi.HealthChecks;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient<IDianResolutionService, DianResolutionService>();
builder.Services.AddHttpClient<IPendingInvoicesBilling, PendingInvoicesService>();
builder.Services.AddHttpClient<IInvoicesEmitterBIlling, InvoicesEmitterBilling>();

// Observability: prometheus-net metrics registration (centralized HangfireMetrics)
builder.Services.AddSingleton<PoliedroHangFire.Infrastructure.Observability.HangfireMetrics>();

// Health checks: servicio externo + Hangfire
builder.Services.AddHealthChecks()
    .AddTypeActivatedCheck<ExternalServiceHealthCheck>(
        "dian-resolution",
        null,
        new[] { "external" },
        new object[] { builder.Configuration["External:DianresolutionUrl"]!, "Dian Resolution Service" })
    .AddCheck<HangfireHealthCheck>("hangfire", tags: new[] { "hangfire" });

// Registrar HttpClient para health checks
builder.Services.AddHttpClient();

builder.Services.AddHangfire(config => {
    var connectionString = Environment.GetEnvironmentVariable("MYSQL_CONNECTION") ?? builder.Configuration.GetConnectionString("HangfireConnection");
    var tablesPrefix = PoliedroHangFire.WebApi.HangfireStorageConfiguration.TablesPrefix(builder.Configuration);
    config.UseSimpleAssemblyNameTypeSerializer()
          .UseRecommendedSerializerSettings()
          .UseStorage(new MySqlStorage(connectionString, new MySqlStorageOptions
          {
              TablesPrefix = tablesPrefix,
              PrepareSchemaIfNecessary = true


          }));
});
builder.Services.AddHangfireServer(options => options.WorkerCount = builder.Configuration.GetValue("Hangfire:WorkerCount", 5));

builder.Services.AddAntiforgery();

var app = builder.Build();

// Configurar Health Check endpoints
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var response = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(x => new
            {
                name = x.Key,
                status = x.Value.Status.ToString(),
                description = x.Value.Description,
                duration = x.Value.Duration.TotalMilliseconds
            }),
            totalDuration = report.TotalDuration.TotalMilliseconds
        };
        await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response));
    }
});

// Health check simple (para Docker / liveness): solo dependencias propias, sin servicios externos
app.MapHealthChecks("/health/simple", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("hangfire")
});

app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    IsReadOnlyFunc = _ => builder.Configuration.GetValue("Hangfire:DashboardReadOnly", true),
    Authorization = new IDashboardAuthorizationFilter[]
    {
        new PoliedroHangFire.WebApi.DevDashboardAccessFilter()
    }
});

// Prometheus-net HTTP metrics middleware and /metrics endpoint
// Expose standard HTTP metrics (requests, latency, status, endpoint) and the /metrics endpoint for Prometheus to scrape.
app.UseHttpMetrics();
app.MapMetrics("/metrics");

// Intentar registrar jobs de forma segura
try
{
    using (var scope = app.Services.CreateScope())
    {
        var registered = await ConfigJobs.RegisterJobsAsync(scope.ServiceProvider);
        Console.WriteLine(registered
            ? "[INFO] Jobs registrados exitosamente durante el startup"
            : "[WARNING] No se pudieron registrar los jobs: la API de resoluciones DIAN no respondió. Se conservan los jobs existentes y la aplicación continuará ejecutándose.");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"[WARNING] No se pudieron registrar los jobs durante el startup: {ex.Message}");
    Console.WriteLine("[INFO] La aplicación continuará ejecutándose. Verifique la conectividad con el servicio externo.");
}

app.Run();

