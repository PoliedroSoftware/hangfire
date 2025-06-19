using Hangfire;
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
    var connectionString = builder.Configuration.GetConnectionString("HangfireConnection");
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

app.UseHangfireDashboard("/hangfire");

using (var scope = app.Services.CreateScope())
{
    await ConfigJobs.RegisterJobsAsync(scope.ServiceProvider);
}

app.Run();

