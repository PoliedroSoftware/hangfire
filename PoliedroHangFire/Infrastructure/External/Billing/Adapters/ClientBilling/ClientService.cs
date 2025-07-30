using PoliedroHangFire.Application.ClientBilling.Interfaces;
using System.Text.Json;

namespace PoliedroHangFire.Infrastructure.External.Billing.Adapters.ClientBilling;

public class ClientService(HttpClient httpClient, IConfiguration config) : IClientService
{
    public async Task<List<Domain.ClientBilling.Entities.ClientBilling>> GetClientBillingsAsync()
    {
        httpClient.DefaultRequestHeaders.Add("X-Environment", "production-billing");
        var response = await httpClient.GetAsync(config["External:ClientsUrl"]);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<Domain.ClientBilling.Entities.ClientBilling>>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? [];
    }
}
