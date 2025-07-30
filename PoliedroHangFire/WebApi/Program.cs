using Hangfire;
using Hangfire.Dashboard;
using Hangfire.MySql;
using PoliedroHangFire.Application.ClientBilling.Interfaces;
using PoliedroHangFire.Application.PendingInvoicesBilling.Interfaces;
using PoliedroHangFire.HangfireJobs.ConfigJbos.Billing;
using PoliedroHangFire.Infrastructure.External.Billing.Adapters.ClientBilling;
using PoliedroHangFire.Infrastructure.External.Billing.Adapters.PendingInvoicesBilling;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddHttpClient<IClientService, ClientService>();
builder.Services.AddTransient<IPendingInvoicesBilling, PendingInvoicesService>();

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

app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new IDashboardAuthorizationFilter[]
    {
        new PoliedroHangFire.WebApi.DevDashboardAccessFilter()
    }
});
using (var scope = app.Services.CreateScope())
{
    await ConfigJobs.RegisterJobsAsync(scope.ServiceProvider);
}

app.Run();

