using PoliedroHangFire.Application.InvoicesEmitterBilling.Interfaces;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace PoliedroHangFire.Infrastructure.External.Billing.Adapters.InvoicesEmitterBilling;
public class InvoicesEmitterBilling(HttpClient httpClient, IConfiguration config, PoliedroHangFire.Infrastructure.Observability.HangfireMetrics metrics) : IInvoicesEmitterBIlling
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task InvoicesEmitterServicesAsync(string jsonInvoices, int resolutionId)
    {
        var baseUrl = (config["External:EmitInvoicesUrl"]
            ?? throw new InvalidOperationException("External:EmitInvoicesUrl is required")).TrimEnd('/');

        var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/{resolutionId}");
        request.Headers.Add("X-Environment", "production-billing");
        request.Content = new StringContent($"{{\"data\":{jsonInvoices}}}", Encoding.UTF8, "application/json");

        metrics.BillingEmitRequestsTotal.Inc();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            using var response = await httpClient.SendAsync(request, cts.Token);

            var responseContent = await response.Content.ReadAsStringAsync(cts.Token);

            Console.WriteLine($"[EMISION RESULTADO] Resolución: {resolutionId} StatusCode: {response.StatusCode}");
            Console.WriteLine($"[EMISION RESULTADO] Body: {responseContent}");

            response.EnsureSuccessStatusCode();

            var results = JsonSerializer.Deserialize<List<EmissionResult>>(responseContent, JsonOptions);
            if (results is null || results.Any(r => !r.Status))
                throw new InvalidOperationException(
                    $"Billing no confirmó la emisión completa para la resolución {resolutionId}.");
        }
        catch (Exception ex)
        {
            metrics.BillingEmitErrorsTotal.Inc();
            Console.WriteLine($"[ERROR] Error al emitir facturas: {ex.Message}");
            throw;
        }
        finally
        {
            sw.Stop();
            metrics.BillingEmitDurationSeconds.Observe(sw.Elapsed.TotalSeconds);
        }
    }

    private record EmissionResult(bool Status);
}
