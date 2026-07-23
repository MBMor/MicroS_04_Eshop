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

    private FakeDownstreamServer(
        WebApplication application,
        Uri baseAddress)
    {
        _application = application;
        BaseAddress = baseAddress;
    }

    public Uri BaseAddress { get; }

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

        application.Map(
            "/{**path}",
            (HttpRequest request) =>
                Results.Ok(
                    new ForwardedResponse(
                        request.Method,
                        request.Path.Value
                        ?? string.Empty)));

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
            baseAddress);
    }

    public async ValueTask DisposeAsync()
    {
        await _application.StopAsync();
        await _application.DisposeAsync();
    }
}

internal sealed record ForwardedResponse(
    string Method,
    string Path);
