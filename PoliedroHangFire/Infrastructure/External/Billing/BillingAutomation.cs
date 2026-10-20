using System.Net.Http.Json;
using System.Text.Json;
using Hangfire;
using Hangfire.Storage;

namespace PoliedroHangFire.Infrastructure.External.Billing;

/// <summary>Billing v1 adapter. Jobs persist resolution IDs, never customer data or Plemsi keys.</summary>
public sealed class BillingAutomation(HttpClient http, IConfiguration configuration,
    IRecurringJobManager jobs, ILogger<BillingAutomation> logger)
{
    private HttpRequestMessage Request(HttpMethod method, string path)
    {
        var baseUrl = configuration["Billing:BaseUrl"]?.TrimEnd('/')
            ?? throw new InvalidOperationException("Billing:BaseUrl is required");
        var request = new HttpRequestMessage(method, baseUrl + path);
        request.Headers.Add("X-Billing-Admin-Key", configuration["Billing:AdminKey"]);
        return request;
    }

    [AutomaticRetry(Attempts = 0)]
    [DisableConcurrentExecution(120)]
    public async Task ReconcileAsync()
    {
        using var request = Request(HttpMethod.Get, "/api/v1/admin/automation/resolutions");
        using var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var candidates = await response.Content.ReadFromJsonAsync<List<Candidate>>() ?? [];
        var interval = Math.Clamp(configuration.GetValue("Billing:IntervalMinutes", 5), 1, 59);
        var wanted = candidates.Select(c => "billing-v1-resolution-" + c.ResolutionId).ToHashSet();
        using var connection = JobStorage.Current.GetConnection();
        foreach (var old in connection.GetRecurringJobs().Where(j => j.Id.StartsWith("billing-v1-resolution-", StringComparison.Ordinal)))
            if (!wanted.Contains(old.Id)) jobs.RemoveIfExists(old.Id);
        foreach (var candidate in candidates)
        {
            var id = candidate.ResolutionId;
            jobs.AddOrUpdate<BillingAutomation>("billing-v1-resolution-" + id,
                job => job.ProcessResolutionAsync(id), $"*/{interval} * * * *");
        }
        logger.LogInformation("Billing schedule reconciled: {Count} active resolutions. Each execution rechecks client and license.", candidates.Count);
    }

    [AutomaticRetry(Attempts = 0)]
    [DisableConcurrentExecution(600)]
    public async Task ProcessResolutionAsync(int resolutionId)
    {
        if (!configuration.GetValue("Billing:EmissionEnabled", false))
        {
            logger.LogInformation("Emission disabled for this installation; resolution {Id} skipped.", resolutionId);
            return;
        }
        using var eligibilityRequest = Request(HttpMethod.Get, $"/api/v1/admin/automation/resolutions/{resolutionId}/eligibility");
        using var eligibilityResponse = await http.SendAsync(eligibilityRequest);
        eligibilityResponse.EnsureSuccessStatusCode();
        var eligibility = await eligibilityResponse.Content.ReadFromJsonAsync<Eligibility>();
        if (eligibility?.Allowed != true)
        {
            logger.LogInformation("Resolution {Id} skipped: client or Odoo license inactive.", resolutionId);
            return;
        }
        using var pendingRequest = Request(HttpMethod.Get, $"/api/v1/invoicespendingwithdetails/{resolutionId}");
        using var pendingResponse = await http.SendAsync(pendingRequest);
        pendingResponse.EnsureSuccessStatusCode();
        var pending = await pendingResponse.Content.ReadFromJsonAsync<Pending>();
        if (pending?.Success != true || pending.Data.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("Invalid pending-invoices response");
        if (pending.Data.GetArrayLength() == 0) return;
        using var emitRequest = Request(HttpMethod.Post, $"/api/v1/billing/{resolutionId}");
        emitRequest.Content = JsonContent.Create(new { data = pending.Data });
        using var emitResponse = await http.SendAsync(emitRequest);
        emitResponse.EnsureSuccessStatusCode();
        var results = await emitResponse.Content.ReadFromJsonAsync<List<EmissionResult>>();
        if (results is null || results.Count != pending.Data.GetArrayLength() || results.Any(r => !r.Status))
            throw new InvalidOperationException("Billing reported incomplete emission; inspect provider/local persistence before retrying");
        logger.LogInformation("Resolution {Id}: {Count} invoices processed.", resolutionId, results.Count);
    }

    public record Candidate(int ResolutionId);
    public record Eligibility(bool Allowed);
    public record Pending(bool Success, JsonElement Data);
    public record EmissionResult(bool Status);
}
