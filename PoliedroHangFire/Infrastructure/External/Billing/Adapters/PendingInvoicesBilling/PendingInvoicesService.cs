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
        // Mark job start and measure overall job duration
        metrics.BillingJobsExecutedTotal.Inc();
        var jobSw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            // Each invocation corresponds to a client being processed by the orchestrator
            metrics.BillingClientsProcessedTotal.Inc();

            httpClient.DefaultRequestHeaders.Add("X-Environment", "production-billing");
            var request = new HttpRequestMessage(HttpMethod.Get, config["External:PendingInvoicesUrl"]);

            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            request.Content = new StringContent(JsonSerializer.Serialize(new
            {
                ResolutionType = resolutionType,
                Name = name
            }), Encoding.UTF8, "application/json");

            // Measure time spent consulting pending invoices for this client
            var clientSw = System.Diagnostics.Stopwatch.StartNew();
            var response = await httpClient.SendAsync(request);
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

