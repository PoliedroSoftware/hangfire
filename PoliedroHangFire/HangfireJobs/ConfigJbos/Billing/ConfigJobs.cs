using Hangfire;
using Hangfire.Storage;
using PoliedroHangFire.Application.DianResolution.Interfaces;
using PoliedroHangFire.Application.PendingInvoicesBilling.Interfaces;

namespace PoliedroHangFire.HangfireJobs.ConfigJbos.Billing;

public class ConfigJobs
{
    private const string ResolutionJobPrefix = "facturacion-resolucion-";
    private const string LegacyClientJobPrefix = "facturacion-cliente-";
    private const string LegacyAutomationJobPrefix = "billing-v1-";

    public static async Task RegisterJobsAsync(IServiceProvider services)
    {
        var recurringJobs = services.GetRequiredService<IRecurringJobManager>();
        var resolutionService = services.GetRequiredService<IDianResolutionService>();
        var job = services.GetRequiredService<IPendingInvoicesBilling>();
        var interval = Math.Clamp(
            services.GetRequiredService<IConfiguration>().GetValue("Hangfire:ResolutionIntervalMinutes", 5), 1, 59);

        var resolutions = await resolutionService.GetActiveResolutionsAsync();
        var wanted = resolutions.Select(r => ResolutionJobPrefix + r.ResolutionId).ToHashSet(StringComparer.Ordinal);

        RemoveStaleJobs(recurringJobs, wanted);

        foreach (var resolution in resolutions)
        {
            var displayName = string.IsNullOrWhiteSpace(resolution.Description)
                ? $"Resolución {resolution.ResolutionId}"
                : resolution.Description.Trim();

            recurringJobs.AddOrUpdate(
                ResolutionJobPrefix + resolution.ResolutionId,
                () => job.InvoicePendingAsync(resolution.ResolutionId, displayName),
                Cron.MinuteInterval(interval)
            );
        }
    }

    /// <summary>Drops schedules for resolutions that are gone and jobs left by removed schedulers.</summary>
    private static void RemoveStaleJobs(IRecurringJobManager recurringJobs, HashSet<string> wanted)
    {
        using var connection = JobStorage.Current.GetConnection();
        foreach (var existing in connection.GetRecurringJobs())
        {
            var isLegacyJob = existing.Id.StartsWith(LegacyClientJobPrefix, StringComparison.Ordinal)
                || existing.Id.StartsWith(LegacyAutomationJobPrefix, StringComparison.Ordinal);
            var isGoneResolutionJob = existing.Id.StartsWith(ResolutionJobPrefix, StringComparison.Ordinal)
                && !wanted.Contains(existing.Id);

            if (isLegacyJob || isGoneResolutionJob)
                recurringJobs.RemoveIfExists(existing.Id);
        }
    }
}
