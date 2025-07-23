using PoliedroHangFire.Application.ClientBilling.Interfaces;
using System.Text.Json;

namespace PoliedroHangFire.Infrastructure.External.Billing.Adapters.ClientBilling;

public class ClientService : IClientService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public ClientService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public async Task<List<Domain.ClientBilling.Entities.ClientBilling>> GetClientBillingsAsync()
    {
        var useMock = _configuration.GetValue<bool>("ClientBilling:UseMock");

        if (useMock)
        {
            return new List<Domain.ClientBilling.Entities.ClientBilling>(); // Datos simulados
        }

        _httpClient.DefaultRequestHeaders.Add("X-Environment", "staging-billing");

        var response = await _httpClient.GetAsync("https://wc9oqtphb5.execute-api.us-east-2.amazonaws.com/billing/api/v1/client");
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();

        return JsonSerializer.Deserialize<List<Domain.ClientBilling.Entities.ClientBilling>>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new();
    }
}
