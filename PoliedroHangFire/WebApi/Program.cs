using Hangfire;
using Hangfire.Dashboard;
using Hangfire.MySql;
using PoliedroHangFire.Application.ClientBilling.Interfaces;
using PoliedroHangFire.Application.InvoicesEmitterBilling.Interfaces;
using PoliedroHangFire.Application.PendingInvoicesBilling.Interfaces;
using PoliedroHangFire.HangfireJobs.ConfigJbos.Billing;
using PoliedroHangFire.Infrastructure.External.Billing.Adapters.ClientBilling;
using PoliedroHangFire.Infrastructure.External.Billing.Adapters.InvoicesEmitterBilling;
using PoliedroHangFire.Infrastructure.External.Billing.Adapters.PendingInvoicesBilling;
using PoliedroHangFire.WebApi.HealthChecks;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddHttpClient<IClientService, ClientService>();
builder.Services.AddHttpClient<IPendingInvoicesBilling, PendingInvoicesService>();
builder.Services.AddHttpClient<IInvoicesEmitterBIlling, InvoicesEmitterBilling>();

// Observability: prometheus-net metrics registration (centralized HangfireMetrics)
builder.Services.AddSingleton<PoliedroHangFire.Infrastructure.Observability.HangfireMetrics>();

// Health checks: servicio externo + Hangfire
builder.Services.AddHealthChecks()
    .AddTypeActivatedCheck<ExternalServiceHealthCheck>(
        "client-service",
        args: new object[] { builder.Configuration["External:ClientsUrl"]!, "Client Service" })
    .AddCheck<HangfireHealthCheck>("hangfire");

// Registrar HttpClient para health checks
builder.Services.AddHttpClient();

builder.Services.AddHangfire(config => {
    var connectionString = Environment.GetEnvironmentVariable("MYSQL_CONNECTION") ?? builder.Configuration.GetConnectionString("HangfireConnection");
    Console.WriteLine($"[INFO] ConnectionString usada: {connectionString}");
    config.UseSimpleAssemblyNameTypeSerializer()
          .UseRecommendedSerializerSettings()
          .UseStorage(new MySqlStorage(connectionString, new MySqlStorageOptions
          {
              TablesPrefix = "Hangfire",
              PrepareSchemaIfNecessary = true


          }));
});
builder.Services.AddHangfireServer(options => options.WorkerCount = 5);

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

// Health check simple (para Docker)
app.MapHealthChecks("/health/simple");

app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
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
        await ConfigJobs.RegisterJobsAsync(scope.ServiceProvider);
        Console.WriteLine("[INFO] Jobs registrados exitosamente durante el startup");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"[WARNING] No se pudieron registrar los jobs durante el startup: {ex.Message}");
    Console.WriteLine("[INFO] La aplicación continuará ejecutándose. Verifique la conectividad con el servicio externo.");
}

app.Run();

