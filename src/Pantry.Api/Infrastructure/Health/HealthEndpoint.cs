using System.Reflection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Pantry.Api.Infrastructure.Health;

public static class HealthEndpoint
{
    private static readonly string Version =
        typeof(HealthEndpoint).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "dev";

    public static IEndpointRouteBuilder MapHealthEndpoint(this IEndpointRouteBuilder api)
    {
        api.MapGet("/health", async (HealthCheckService healthChecks, CancellationToken cancellationToken) =>
        {
            var report = await healthChecks.CheckHealthAsync(cancellationToken);
            var healthy = report.Status == HealthStatus.Healthy;
            return Results.Json(
                new HealthResponse(healthy ? "ok" : "unhealthy", Version),
                statusCode: healthy ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable);
        });
        return api;
    }
}

public sealed record HealthResponse(string Status, string Version);
