using Hangfire;
using PoliedroHangFire.Application.ClientBilling.Interfaces;
using PoliedroHangFire.Application.PendingInvoicesBilling.Interfaces;

namespace PoliedroHangFire.HangfireJobs.ConfigJbos.Billing
{
    public class ConfigJobs
    {
        public static async Task RegisterJobsAsync(IServiceProvider services)
        {
            var recurringJobs = services.GetRequiredService<IRecurringJobManager>();
            var clientService = services.GetRequiredService<IClientService>();
            var job = services.GetRequiredService<IPendingInvoicesBilling>();

            var clientes = await clientService.GetClientBillingsAsync();

            foreach (var cliente in clientes)
            {
                if (!cliente.Active || cliente.Iterations <= 0) continue;

                var jobId = $"facturacion-cliente-{cliente.ClientBillingElectronicId}";
                var interval = Cron.MinuteInterval(cliente.Iterations);

                recurringJobs.AddOrUpdate(
                    jobId,
                    () => job.InvoicePendingAsync(
                        cliente.ClientBillingElectronicId,
                        cliente.ApiKey,
                        cliente.ResolutionType,
                        cliente.Name
                    ),
                    interval
                );
            }
        }
    }
}
