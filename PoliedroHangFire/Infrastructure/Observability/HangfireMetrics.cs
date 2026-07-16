using Prometheus;

namespace PoliedroHangFire.Infrastructure.Observability;

public sealed class HangfireMetrics
{
    // Counters
    public readonly Counter BillingJobsExecutedTotal;
    public readonly Counter BillingClientsProcessedTotal;
    public readonly Counter BillingPendingInvoicesTotal;
    public readonly Counter BillingEmitRequestsTotal;
    public readonly Counter BillingEmitErrorsTotal;

    // Gauges
    public readonly Gauge BillingPendingQueue;

    // Histograms
    public readonly Histogram BillingJobDurationSeconds;
    public readonly Histogram BillingClientsDurationSeconds;
    public readonly Histogram BillingEmitDurationSeconds;

    public HangfireMetrics()
    {
        // Create metrics with stable names and helpful labels where applicable.
        BillingJobsExecutedTotal = Metrics.CreateCounter(
            "billing_jobs_executed_total",
            "Total number of billing jobs started by Hangfire"
        );

        BillingJobDurationSeconds = Metrics.CreateHistogram(
            "billing_job_duration_seconds",
            "Duration of billing job execution in seconds",
            new HistogramConfiguration
            {
                Buckets = Histogram.ExponentialBuckets(start: 0.1, factor: 2, count: 12)
            }
        );

        BillingClientsProcessedTotal = Metrics.CreateCounter(
            "billing_clients_processed_total",
            "Total number of clients processed by the billing orchestrator"
        );

        BillingClientsDurationSeconds = Metrics.CreateHistogram(
            "billing_clients_duration_seconds",
            "Time spent processing a single client in seconds",
            new HistogramConfiguration
            {
                Buckets = Histogram.ExponentialBuckets(start: 0.01, factor: 2, count: 12)
            }
        );

        BillingPendingInvoicesTotal = Metrics.CreateCounter(
            "billing_pending_invoices_total",
            "Total number of pending invoices discovered"
        );

        BillingPendingQueue = Metrics.CreateGauge(
            "billing_pending_queue",
            "Number of pending invoices found in the last execution"
        );

        BillingEmitRequestsTotal = Metrics.CreateCounter(
            "billing_emit_requests_total",
            "Total number of requests sent to Billing API to emit invoices"
        );

        BillingEmitErrorsTotal = Metrics.CreateCounter(
            "billing_emit_errors_total",
            "Total number of failed requests to Billing API"
        );

        BillingEmitDurationSeconds = Metrics.CreateHistogram(
            "billing_emit_duration_seconds",
            "Duration of calls to Billing API in seconds",
            new HistogramConfiguration
            {
                Buckets = Histogram.ExponentialBuckets(start: 0.01, factor: 2, count: 12)
            }
        );
    }
}