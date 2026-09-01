using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace ApiGateway.IntegrationTests.Infrastructure;

internal sealed class FakeDownstreamServer
    : IAsyncDisposable
{
    private const string TestingEnvironment = "Testing";

    private readonly WebApplication _application;

    private readonly RequestCounter _requestCounter;

    private FakeDownstreamServer(
        WebApplication application,
        Uri baseAddress,
        RequestCounter requestCounter)
    {
        _application = application;
        _requestCounter = requestCounter;
        BaseAddress = baseAddress;
    }

    public Uri BaseAddress { get; }

    public int RequestCount =>
        _requestCounter.Value;

    public void ResetRequestCount()
    {
        _requestCounter.Reset();
    }

    public static async Task<FakeDownstreamServer>
        StartAsync(
            CancellationToken cancellationToken = default)
    {
        WebApplicationBuilder builder =
            WebApplication.CreateBuilder(
                new WebApplicationOptions
                {
                    EnvironmentName = TestingEnvironment
                });

        builder.WebHost.ConfigureKestrel(
            kestrelOptions =>
            {
                kestrelOptions.Listen(
                    IPAddress.Loopback,
                    port: 0);
            });

        WebApplication application =
            builder.Build();

        RequestCounter requestCounter = new();

        application.Map(
            "/{**path}",
            (HttpRequest request) =>
            {
                if (string.Equals(
                        request.Path,
                        "/health",
                        StringComparison.Ordinal))
                {
                    return Results.Ok();
                }

                requestCounter.Increment();

                return
                Results.Ok(
                    new ForwardedResponse(
                        request.Method,
                        request.Path.Value
                        ?? string.Empty));
            });

        await application.StartAsync(
            cancellationToken);

        IServer server =
            application.Services
                .GetRequiredService<IServer>();

        IServerAddressesFeature addressesFeature =
            server.Features
                .Get<IServerAddressesFeature>()
            ?? throw new InvalidOperationException(
                "Kestrel did not expose its bound addresses.");

        string address =
            addressesFeature.Addresses.Single();

        Uri baseAddress = new(
            $"{address.TrimEnd('/')}/",
            UriKind.Absolute);

        return new FakeDownstreamServer(
            application,
            baseAddress,
            requestCounter);
    }

    public async ValueTask DisposeAsync()
    {
        await _application.StopAsync();
        await _application.DisposeAsync();
    }

    private sealed class RequestCounter
    {
        private int _value;

        public int Value =>
            Volatile.Read(ref _value);

        public void Increment()
        {
            Interlocked.Increment(ref _value);
        }

        public void Reset()
        {
            Interlocked.Exchange(
                ref _value,
                0);
        }
    }
}

internal sealed record ForwardedResponse(
    string Method,
    string Path);
