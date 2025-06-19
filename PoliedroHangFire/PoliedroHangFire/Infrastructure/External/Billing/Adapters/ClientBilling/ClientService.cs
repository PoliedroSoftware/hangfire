using PoliedroHangFire.Application.ClientBilling.Interfaces;
using System.Text.Json;

namespace PoliedroHangFire.Infrastructure.External.Billing.Adapters.ClientBilling;
public class ClientService(HttpClient httpClient) : IClientService
{
    private readonly HttpClient _httpClient = httpClient;

    public async Task<List<Domain.ClientBilling.Entities.ClientBilling>> GetClientBillingsAsync()
    {
        var response = await _httpClient.GetAsync("https://wc9oqtphb5.execute-api.us-east-2.amazonaws.com/billing/api/v1/client");
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();

        return JsonSerializer.Deserialize<List<Domain.ClientBilling.Entities.ClientBilling>>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }

}
