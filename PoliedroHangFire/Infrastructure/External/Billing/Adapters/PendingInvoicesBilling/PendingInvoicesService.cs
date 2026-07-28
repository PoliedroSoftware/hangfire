using Hangfire;
using PoliedroHangFire.Application.InvoicesEmitterBilling.Interfaces;
using PoliedroHangFire.Application.PendingInvoicesBilling.Interfaces;
using System.Text;
using System.Text.Json;
using PoliedroHangFire.Infrastructure.Observability;

namespace PoliedroHangFire.Infrastructure.External.Billing.Adapters.PendingInvoicesBilling;

public class PendingInvoicesService(HttpClient httpClient, IConfiguration config, HangfireMetrics metrics) : IPendingInvoicesBilling
{
    public async Task InvoicePendingAsync(int clienteId, string token, bool resolutionType, string name)
    {
        metrics.BillingJobsExecutedTotal.Inc();
        var jobSw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            metrics.BillingClientsProcessedTotal.Inc();

            var request = new HttpRequestMessage(HttpMethod.Get, config["External:PendingInvoicesUrl"]);
            request.Headers.Add("X-Environment", "production-billing");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            request.Content = new StringContent(JsonSerializer.Serialize(new
            {
                ResolutionType = resolutionType,
                Name = name
            }), Encoding.UTF8, "application/json");

            var clientSw = System.Diagnostics.Stopwatch.StartNew();
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            var response = await httpClient.SendAsync(request, cts.Token);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            clientSw.Stop();
            metrics.BillingClientsDurationSeconds.Observe(clientSw.Elapsed.TotalSeconds);

            var invoices = JsonSerializer.Deserialize<List<object>>(json);

            if (invoices != null && invoices.Any())
            {
                Console.WriteLine($"[INFO] {name}: Se encontraron facturas pendientes, programando job de emisión...");

                // Update pending invoices metrics
                metrics.BillingPendingInvoicesTotal.Inc(invoices.Count);
                metrics.BillingPendingQueue.Set(invoices.Count);

                // Programar el job de emisión como uno nuevo
                BackgroundJob.Enqueue<IInvoicesEmitterBIlling>(svc =>
                    svc.InvoicesEmitterServicesAsync(json, token)
                );
            }
            else
            {
                Console.WriteLine($"[INFO] {name}: No hay facturas pendientes.");

                // No pending invoices found — ensure queue gauge reflects zero for this execution
                metrics.BillingPendingQueue.Set(0);
            }
        }
        finally
        {
            jobSw.Stop();
            metrics.BillingJobDurationSeconds.Observe(jobSw.Elapsed.TotalSeconds);
        }
    }
}

