using PoliedroHangFire.Application.InvoicesEmitterBilling.Interfaces;
using PoliedroHangFire.Application.PendingInvoicesBilling.Interfaces;
using System.Text;
using System.Text.Json;

namespace PoliedroHangFire.Infrastructure.External.Billing.Adapters.PendingInvoicesBilling;

public class PendingInvoicesService(HttpClient httpClient, IInvoicesEmitterBIlling invoicesEmitterBIlling, IConfiguration config) : IPendingInvoicesBilling
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

       await invoicesEmitterBIlling.InvoicesEmitterServicesAsync(json, token);


    }
}

