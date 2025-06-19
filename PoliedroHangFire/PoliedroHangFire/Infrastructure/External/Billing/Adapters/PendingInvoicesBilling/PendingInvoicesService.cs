using PoliedroHangFire.Application.PendingInvoicesBilling.Interfaces;
using System.Text;
using System.Text.Json;

namespace PoliedroHangFire.Infrastructure.External.Billing.Adapters.PendingInvoicesBilling;

public class PendingInvoicesService(HttpClient httpClient) : IPendingInvoicesBilling
{
    private readonly HttpClient _httpClient = httpClient;

    public async Task InvoicePendingAsync(int clienteId, string token, bool resolutionType, string name)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"https://wc9oqtphb5.execute-api.us-east-2.amazonaws.com/billing/api/v1/invoicespendingwithdetails");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        request.Content = new StringContent(JsonSerializer.Serialize(new
        {
            ResolutionType = resolutionType,
            Name = name
        }), Encoding.UTF8, "application/json");
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }
}

