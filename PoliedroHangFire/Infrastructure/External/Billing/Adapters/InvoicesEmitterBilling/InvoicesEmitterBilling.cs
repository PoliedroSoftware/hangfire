using PoliedroHangFire.Application.InvoicesEmitterBilling.Interfaces;
using System.Net.Http;
using System.Text;

namespace PoliedroHangFire.Infrastructure.External.Billing.Adapters.InvoicesEmitterBilling;
public class InvoicesEmitterBilling(HttpClient httpClient, IConfiguration config, PoliedroHangFire.Infrastructure.Observability.HangfireMetrics metrics) : IInvoicesEmitterBIlling
{
    public async Task InvoicesEmitterServicesAsync(string jsonInvoices, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, config["External:EmitInvoicesUrl"]);
        request.Headers.Add("X-Environment", "production-billing");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        request.Content = new StringContent(jsonInvoices, Encoding.UTF8, "application/json");

        metrics.BillingEmitRequestsTotal.Inc();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            var response = await httpClient.SendAsync(request, cts.Token);

            var responseContent = await response.Content.ReadAsStringAsync();

            Console.WriteLine($"[EMISION RESULTADO] StatusCode: {response.StatusCode}");
            Console.WriteLine($"[EMISION RESULTADO] Body: {responseContent}");

            response.EnsureSuccessStatusCode();
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
}
