using PoliedroHangFire.Application.DianResolution.Interfaces;
using System.Net;
using System.Text.Json;
using DianResolutionEntity = PoliedroHangFire.Domain.DianResolution.Entities.DianResolution;

namespace PoliedroHangFire.Infrastructure.External.Billing.Adapters.DianResolutionBilling;

public class DianResolutionService(HttpClient httpClient, IConfiguration config) : IDianResolutionService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<List<DianResolutionEntity>> GetActiveResolutionsAsync()
    {
        var url = config["External:DianresolutionUrl"]
            ?? throw new InvalidOperationException("External:DianresolutionUrl is required");

        using var response = await httpClient.GetAsync(url);
        if (response.StatusCode == HttpStatusCode.NoContent) return [];
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(content)) return [];

        var resolutions = JsonSerializer.Deserialize<List<DianResolutionEntity>>(content, JsonOptions) ?? [];

        // The endpoint already returns only active resolutions; this guards against partial payloads.
        return resolutions.Where(r => r.Active != false).ToList();
    }
}
