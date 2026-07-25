using Microsoft.Extensions.Diagnostics.HealthChecks;
using MySql.Data.MySqlClient;

namespace PoliedroHangFire.WebApi.HealthChecks;

public class HangfireHealthCheck : IHealthCheck
{
    private readonly string _connectionString;

    public HangfireHealthCheck(IConfiguration configuration)
    {
        _connectionString = Environment.GetEnvironmentVariable("MYSQL_CONNECTION")
            ?? configuration.GetConnectionString("HangfireConnection")
            ?? throw new InvalidOperationException("MySQL connection string not found");
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            using var command = new MySqlCommand(
                "SELECT COUNT(*) FROM HangfireServer WHERE LastHeartbeat >= DATE_SUB(NOW(), INTERVAL 2 MINUTE)",
                connection);
            var activeServers = (long)(await command.ExecuteScalarAsync(cancellationToken))!;

            if (activeServers > 0)
            {
                return HealthCheckResult.Healthy($"Hangfire server active. Servers: {activeServers}");
            }

            return HealthCheckResult.Unhealthy("No active Hangfire servers. Last heartbeat > 2 min.");
        }
        catch (MySqlException ex)
        {
            return HealthCheckResult.Unhealthy("MySQL connection failed for Hangfire", ex);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Hangfire health check failed", ex);
        }
    }
}
