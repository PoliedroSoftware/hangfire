using Hangfire;
using PoliedroHangFire.Application.InvoicesEmitterBilling.Interfaces;
using PoliedroHangFire.Application.PendingInvoicesBilling.Interfaces;
using System.Net;
using System.Text.Json;
using PoliedroHangFire.Infrastructure.Observability;

namespace PoliedroHangFire.Infrastructure.External.Billing.Adapters.PendingInvoicesBilling;

public class PendingInvoicesService(HttpClient httpClient, IConfiguration config, HangfireMetrics metrics)
    : IPendingInvoicesBilling
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task InvoicePendingAsync(int resolutionId, string displayName)
    {
        metrics.BillingJobsExecutedTotal.Inc();
        var jobSw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            metrics.BillingClientsProcessedTotal.Inc();

            var baseUrl = (config["External:PendingInvoicesUrl"]
                ?? throw new InvalidOperationException("External:PendingInvoicesUrl is required")).TrimEnd('/');

            var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/{resolutionId}");
            request.Headers.Add("X-Environment", "production-billing");

            var clientSw = System.Diagnostics.Stopwatch.StartNew();
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            using var response = await httpClient.SendAsync(request, cts.Token);
            clientSw.Stop();
            metrics.BillingClientsDurationSeconds.Observe(clientSw.Elapsed.TotalSeconds);

            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.NoContent)
            {
                metrics.BillingPendingQueue.Set(0);
                Console.WriteLine($"[WARNING] {displayName} ({resolutionId}): la resolución no existe en billing.");
                return;
            }

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cts.Token);
                metrics.BillingPendingQueue.Set(0);
                Console.WriteLine($"[ERROR] {displayName} ({resolutionId}): billing respondió {(int)response.StatusCode}. {error}");
                return;
            }

            var json = await response.Content.ReadAsStringAsync(cts.Token);

            var pending = JsonSerializer.Deserialize<PendingInvoicesResponse>(json, JsonOptions);
            if (pending?.Data is not { ValueKind: JsonValueKind.Array } data)
                throw new InvalidOperationException($"Respuesta inválida de facturas pendientes para la resolución {resolutionId}.");

            var count = data.GetArrayLength();
            if (count == 0)
            {
                Console.WriteLine($"[INFO] {displayName}: No hay facturas pendientes.");
                metrics.BillingPendingQueue.Set(0);
                return;
            }

            Console.WriteLine($"[INFO] {displayName}: {count} facturas pendientes, programando emisión a la resolución {resolutionId}...");
            metrics.BillingPendingInvoicesTotal.Inc(count);
            metrics.BillingPendingQueue.Set(count);

            // Billing expects the pending payload as { "data": [...] } on POST /billing/{resolutionId}.
            var payload = data.GetRawText();
            BackgroundJob.Enqueue<IInvoicesEmitterBIlling>(svc =>
                svc.InvoicesEmitterServicesAsync(payload, resolutionId)
            );
        }
        finally
        {
            jobSw.Stop();
            metrics.BillingJobDurationSeconds.Observe(jobSw.Elapsed.TotalSeconds);
        }
    }

    private record PendingInvoicesResponse(bool Success, int Count, JsonElement Data);
}
