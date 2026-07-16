using PoliedroHangFire.Application.InvoicesEmitterBilling.Interfaces;
using System.Net.Http;
using System.Text;

namespace PoliedroHangFire.Infrastructure.External.Billing.Adapters.InvoicesEmitterBilling;
public class InvoicesEmitterBilling(HttpClient httpClient, IConfiguration config, PoliedroHangFire.Infrastructure.Observability.HangfireMetrics metrics) : IInvoicesEmitterBIlling
{
    public async Task InvoicesEmitterServicesAsync(string jsonInvoices, string token)
    {
        httpClient.DefaultRequestHeaders.Clear();
        httpClient.DefaultRequestHeaders.Add("X-Environment", "production-billing");
        var request = new HttpRequestMessage(HttpMethod.Post, config["External:EmitInvoicesUrl"]);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        request.Content = new StringContent(jsonInvoices, Encoding.UTF8, "application/json");

        // Count request and measure duration
        metrics.BillingEmitRequestsTotal.Inc();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var response = await httpClient.SendAsync(request);

            var responseContent = await response.Content.ReadAsStringAsync();

            Console.WriteLine($"[EMISION RESULTADO] StatusCode: {response.StatusCode}");
            Console.WriteLine($"[EMISION RESULTADO] Body: {responseContent}");

            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException ex)
        {
            // Increment error counter for failed emit requests
            metrics.BillingEmitErrorsTotal.Inc();

            Console.WriteLine("[ERROR] HttpRequestException al emitir facturas:");
            Console.WriteLine($"[ERROR] {ex.Message}");
        }
        catch (Exception ex)
        {
            // Increment error counter for unexpected failures
            metrics.BillingEmitErrorsTotal.Inc();

            Console.WriteLine("[ERROR] Excepción general al emitir facturas:");
            Console.WriteLine($"[ERROR] {ex.Message}");
        }
        finally
        {
            sw.Stop();
            metrics.BillingEmitDurationSeconds.Observe(sw.Elapsed.TotalSeconds);
        }
    }
}
