using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace PoliedroHangFire.WebApi.HealthChecks;

public class ExternalServiceHealthCheck : IHealthCheck
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _url;
    private readonly string _serviceName;

    public ExternalServiceHealthCheck(IHttpClientFactory httpClientFactory, string url, string serviceName)
    {
        _httpClientFactory = httpClientFactory;
        _url = url;
        _serviceName = serviceName;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(10);
            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Add("X-Environment", "production-billing");
            
            using var response = await httpClient.GetAsync(_url, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                return HealthCheckResult.Healthy($"{_serviceName} is available. Status: {response.StatusCode}");
            }
            else
            {
                return HealthCheckResult.Degraded($"{_serviceName} returned {response.StatusCode}");
            }
        }
        catch (HttpRequestException ex)
        {
            return HealthCheckResult.Unhealthy($"{_serviceName} is unavailable", ex);
        }
        catch (TaskCanceledException ex)
        {
            return HealthCheckResult.Unhealthy($"{_serviceName} timeout", ex);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"{_serviceName} check failed", ex);
        }
    }
}
