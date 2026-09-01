using System.Text.Json;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace Eshop.HealthChecks;

public static class EshopHealthChecksExtensions
{
    private const string ReadinessTag = "ready";

    private static readonly JsonSerializerOptions
        HealthJsonOptions =
            new(JsonSerializerDefaults.Web);

    public static IHealthChecksBuilder
        AddEshopPostgreSqlReadinessCheck(
            this IHealthChecksBuilder builder,
            string connectionString,
            string name = "postgresql")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            connectionString);

        NpgsqlConnectionStringBuilder connectionOptions =
            new(connectionString)
            {
                Timeout = 2,
                CommandTimeout = 2
            };

        return builder.AddEshopReadinessCheck(
            name,
            async (_, cancellationToken) =>
            {
                await using NpgsqlConnection connection =
                    new(connectionOptions.ConnectionString);

                await connection.OpenAsync(
                    cancellationToken);

                await using NpgsqlCommand command =
                    new("SELECT 1", connection);

                await command.ExecuteScalarAsync(
                    cancellationToken);
            });
    }

    public static IHealthChecksBuilder AddEshopReadinessCheck(
        this IHealthChecksBuilder builder,
        string name,
        Func<IServiceProvider, CancellationToken, Task> probe,
        TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(probe);

        return builder.Add(
            new HealthCheckRegistration(
                name,
                serviceProvider =>
                    new DelegateHealthCheck(
                        serviceProvider,
                        probe),
                HealthStatus.Unhealthy,
                [ReadinessTag],
                timeout ?? TimeSpan.FromSeconds(5)));
    }

    public static IEndpointRouteBuilder MapEshopHealthChecks(
        this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints
            .MapHealthChecks(
                "/live",
                new HealthCheckOptions
                {
                    Predicate = static _ => false
                })
            .AllowAnonymous();

        endpoints
            .MapHealthChecks(
                "/ready",
                CreateReadinessOptions())
            .AllowAnonymous();

        endpoints
            .MapHealthChecks(
                "/health",
                CreateReadinessOptions())
            .AllowAnonymous();

        return endpoints;
    }

    private static HealthCheckOptions
        CreateReadinessOptions()
    {
        return new HealthCheckOptions
        {
            Predicate = static registration =>
                registration.Tags.Contains(
                    ReadinessTag),

            ResponseWriter =
                WriteReadinessResponseAsync
        };
    }

    private static async Task
        WriteReadinessResponseAsync(
            HttpContext context,
            HealthReport report)
    {
        EshopHealthCheckResponse[] checks =
            report.Entries
                .OrderBy(
                    entry => entry.Key,
                    StringComparer.Ordinal)
                .Select(
                    entry =>
                        new EshopHealthCheckResponse(
                            entry.Key,
                            entry.Value.Status.ToString()))
                .ToArray();

        var response =
            new EshopHealthResponse(
                report.Status.ToString(),
                checks);

        context.Response.ContentType =
            "application/json; charset=utf-8";

        await JsonSerializer.SerializeAsync(
            context.Response.Body,
            response,
            HealthJsonOptions,
            context.RequestAborted);
    }

    private sealed class DelegateHealthCheck(
        IServiceProvider serviceProvider,
        Func<IServiceProvider, CancellationToken, Task> probe)
        : IHealthCheck
    {
        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await probe(
                    serviceProvider,
                    cancellationToken);

                return HealthCheckResult.Healthy();
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                return new HealthCheckResult(
                    context.Registration.FailureStatus,
                    "Mandatory dependency is unavailable.",
                    exception);
            }
        }
    }
}
