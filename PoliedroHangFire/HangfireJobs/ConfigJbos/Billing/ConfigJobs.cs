using Hangfire;
using Hangfire.Storage;
using PoliedroHangFire.Application.DianResolution.Interfaces;
using PoliedroHangFire.Application.PendingInvoicesBilling.Interfaces;
using DianResolutionEntity = PoliedroHangFire.Domain.DianResolution.Entities.DianResolution;

namespace PoliedroHangFire.HangfireJobs.ConfigJbos.Billing;

public class ConfigJobs
{
    private const string ResolutionJobPrefix = "facturacion-resolucion-";
    private const string LegacyClientJobPrefix = "facturacion-cliente-";
    private const string LegacyAutomationJobPrefix = "billing-v1-";

    /// <summary>
    /// Registers one recurring job per active DIAN resolution.
    /// Returns <c>false</c> when the resolution endpoint is unreachable; in that case only
    /// legacy jobs are purged and the existing resolution jobs are preserved.
    /// </summary>
    public static async Task<bool> RegisterJobsAsync(IServiceProvider services)
    {
        var recurringJobs = services.GetRequiredService<IRecurringJobManager>();
        var resolutionService = services.GetRequiredService<IDianResolutionService>();
        var job = services.GetRequiredService<IPendingInvoicesBilling>();
        var interval = Math.Clamp(
            services.GetRequiredService<IConfiguration>().GetValue("Hangfire:ResolutionIntervalMinutes", 5), 1, 59);

        // A failure here must not prevent the cleanup of jobs left by removed schedulers.
        List<DianResolutionEntity>? resolutions = null;
        try
        {
            resolutions = await resolutionService.GetActiveResolutionsAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WARNING] No se pudo obtener la lista de resoluciones DIAN: {ex.Message}");
        }

        RemoveStaleJobs(recurringJobs, resolutions);

        if (resolutions is null)
            return false;

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

        return true;
    }

    /// <summary>
    /// Drops schedules for resolutions that are gone and jobs left by removed schedulers.
    /// Legacy jobs are always removed; resolution jobs are only pruned when the resolution
    /// list is known (<paramref name="resolutions"/> not null), so an outage cannot wipe them.
    /// </summary>
    private static void RemoveStaleJobs(IRecurringJobManager recurringJobs, List<DianResolutionEntity>? resolutions)
    {
        var wanted = resolutions?
            .Select(r => ResolutionJobPrefix + r.ResolutionId)
            .ToHashSet(StringComparer.Ordinal);

        using var connection = JobStorage.Current.GetConnection();
        foreach (var existing in connection.GetRecurringJobs())
        {
            var isLegacyJob = existing.Id.StartsWith(LegacyClientJobPrefix, StringComparison.Ordinal)
                || existing.Id.StartsWith(LegacyAutomationJobPrefix, StringComparison.Ordinal);
            var isGoneResolutionJob = wanted is not null
                && existing.Id.StartsWith(ResolutionJobPrefix, StringComparison.Ordinal)
                && !wanted.Contains(existing.Id);

            if (isLegacyJob || isGoneResolutionJob)
                recurringJobs.RemoveIfExists(existing.Id);
        }
    }
}
