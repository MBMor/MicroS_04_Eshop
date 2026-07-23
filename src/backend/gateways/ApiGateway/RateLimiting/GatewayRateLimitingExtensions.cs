using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Eshop.Security.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ApiGateway.RateLimiting;

public static class GatewayRateLimitingExtensions
{
    public static IServiceCollection AddGatewayRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        IConfigurationSection section =
            configuration.GetRequiredSection(
                GatewayRateLimitingOptions.SectionName);

        GatewayRateLimitingOptions settings =
            section.Get<GatewayRateLimitingOptions>()
            ?? throw new InvalidOperationException(
                $"Configuration section " +
                $"'{GatewayRateLimitingOptions.SectionName}' " +
                "is invalid.");

        ValidateSettings(settings);

        services.Configure<GatewayRateLimitingOptions>(
            section);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode =
                StatusCodes.Status429TooManyRequests;

            options.OnRejected =
                HandleRejectedRequestAsync;

            options.AddPolicy<string>(
                GatewayRateLimitPolicies.PublicRead,
                httpContext =>
                    CreateFixedWindowPartition(
                        partitionKey:
                            $"{GatewayRateLimitPolicies.PublicRead}:" +
                            ResolveRemoteAddress(httpContext),
                        settings.PublicRead));

            options.AddPolicy<string>(
                GatewayRateLimitPolicies.CustomerApi,
                httpContext =>
                    CreateFixedWindowPartition(
                        partitionKey:
                            $"{GatewayRateLimitPolicies.CustomerApi}:" +
                            ResolveAuthenticatedSubject(
                                httpContext),
                        settings.CustomerApi));

            options.AddPolicy<string>(
                GatewayRateLimitPolicies.Checkout,
                httpContext =>
                    CreateFixedWindowPartition(
                        partitionKey:
                            $"{GatewayRateLimitPolicies.Checkout}:" +
                            ResolveAuthenticatedSubject(
                                httpContext),
                        settings.Checkout));

            options.AddPolicy<string>(
                GatewayRateLimitPolicies.Operational,
                httpContext =>
                    CreateFixedWindowPartition(
                        partitionKey:
                            $"{GatewayRateLimitPolicies.Operational}:" +
                            ResolveAuthenticatedSubject(
                                httpContext),
                        settings.Operational));
        });

        return services;
    }

    private static RateLimitPartition<string>
        CreateFixedWindowPartition(
            string partitionKey,
            FixedWindowRateLimitOptions settings)
    {
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = settings.PermitLimit,
                Window = TimeSpan.FromSeconds(
                    settings.WindowSeconds),
                QueueLimit = 0,
                QueueProcessingOrder =
                    QueueProcessingOrder.OldestFirst
            });
    }

    private static string ResolveAuthenticatedSubject(
        HttpContext httpContext)
    {
        string? subject = httpContext.User.FindFirstValue(
            EshopClaimNames.Subject);

        if (!string.IsNullOrWhiteSpace(subject))
        {
            return subject;
        }

        return $"anonymous:{ResolveRemoteAddress(httpContext)}";
    }

    private static string ResolveRemoteAddress(
        HttpContext httpContext)
    {
        return httpContext.Connection.RemoteIpAddress
                   ?.ToString()
               ?? "unknown";
    }

    private static async ValueTask
        HandleRejectedRequestAsync(
            OnRejectedContext context,
            CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (context.Lease.TryGetMetadata(
                MetadataName.RetryAfter,
                out TimeSpan retryAfter))
        {
            int retryAfterSeconds = Math.Max(
                1,
                (int)Math.Ceiling(
                    retryAfter.TotalSeconds));

            context.HttpContext.Response.Headers[
                "Retry-After"] =
                retryAfterSeconds.ToString(
                    CultureInfo.InvariantCulture);
        }

        await Results.Problem(
                statusCode:
                    StatusCodes.Status429TooManyRequests,
                title: "Too Many Requests",
                detail:
                    "The request rate limit was exceeded. " +
                    "Retry after the indicated interval.")
            .ExecuteAsync(context.HttpContext);
    }

    private static void ValidateSettings(
        GatewayRateLimitingOptions settings)
    {
        ValidatePolicy(
            nameof(settings.PublicRead),
            settings.PublicRead);

        ValidatePolicy(
            nameof(settings.CustomerApi),
            settings.CustomerApi);

        ValidatePolicy(
            nameof(settings.Checkout),
            settings.Checkout);

        ValidatePolicy(
            nameof(settings.Operational),
            settings.Operational);
    }

    private static void ValidatePolicy(
        string policyName,
        FixedWindowRateLimitOptions settings)
    {
        if (settings.PermitLimit <= 0)
        {
            throw new InvalidOperationException(
                $"RateLimiting:{policyName}:PermitLimit " +
                "must be greater than zero.");
        }

        if (settings.WindowSeconds <= 0)
        {
            throw new InvalidOperationException(
                $"RateLimiting:{policyName}:WindowSeconds " +
                "must be greater than zero.");
        }
    }
}
