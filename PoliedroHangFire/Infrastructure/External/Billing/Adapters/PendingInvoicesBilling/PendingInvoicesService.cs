using Hangfire;
using PoliedroHangFire.Application.InvoicesEmitterBilling.Interfaces;
using PoliedroHangFire.Application.PendingInvoicesBilling.Interfaces;
using System.Text;
using System.Text.Json;

namespace PoliedroHangFire.Infrastructure.External.Billing.Adapters.PendingInvoicesBilling;

public class PendingInvoicesService(HttpClient httpClient, IConfiguration config) : IPendingInvoicesBilling
{
    public async Task InvoicePendingAsync(int clienteId, string token, bool resolutionType, string name)
    {
        httpClient.DefaultRequestHeaders.Add("X-Environment", "production-billing");
        var request = new HttpRequestMessage(HttpMethod.Get, config["External:PendingInvoicesUrl"]);

        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        request.Content = new StringContent(JsonSerializer.Serialize(new
        {
            ResolutionType = resolutionType,
            Name = name
        }), Encoding.UTF8, "application/json");


        var response = await httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();

        var invoices = JsonSerializer.Deserialize<List<object>>(json);

        if (invoices != null && invoices.Any())
        {
            Console.WriteLine($"[INFO] {name}: Se encontraron facturas pendientes, programando job de emisión...");

            // Programar el job de emisión como uno nuevo
            BackgroundJob.Enqueue<IInvoicesEmitterBIlling>(svc =>
                svc.InvoicesEmitterServicesAsync(json, token)
            );
        }
        else
        {
            Console.WriteLine($"[INFO] {name}: No hay facturas pendientes.");
        }


    }
}

