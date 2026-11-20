using Microsoft.Extensions.Diagnostics.HealthChecks;
using StudentWebPortal.Data;

namespace StudentWebPortal.Health;

/// <summary>Reports whether the API can reach its PostgreSQL database. Exposed at /health.</summary>
public sealed class DatabaseHealthCheck(StudentWebPortalContext context) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext healthContext,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await context.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy("Database reachable.")
                : HealthCheckResult.Unhealthy("Cannot connect to the database.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database health check failed.", ex);
        }
    }
}
