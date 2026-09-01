using System.Net;

using Eshop.Operations.Desktop.Api;
using Eshop.Operations.Desktop.Api.OperationalHealth;
using Eshop.Operations.Desktop.ViewModels;

using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace Eshop.Operations.Desktop.Tests.ViewModels;

public sealed class OperationalHealthViewModelTests
{
    [Fact]
    public async Task RefreshHealthMapsHealthyResponse()
    {
        OperationalServiceHealthDto[] services =
        [
            new("Catalog", "Healthy", 12, null, 200),
            new("Basket", "Healthy", 8, null, 200),
            new("Orders", "Healthy", 15, null, 200),
            new("Inventory", "Healthy", 10, null, 200),
            new("Payments", "Healthy", 11, null, 200),
            new("Notifications", "Healthy", 9, null, 200)
        ];

        OperationalHealthViewModel viewModel =
            CreateViewModel(
                new OperationalHealthResponseDto(
                    "Healthy",
                    DateTimeOffset.UtcNow,
                    services));

        await viewModel.RefreshHealthCommand.ExecuteAsync(null);

        Assert.Equal(
            "Healthy",
            viewModel.OverallStatus);
        Assert.True(
            viewModel.HasLoaded);
        Assert.Equal(
            6,
            viewModel.Services.Count);
        Assert.Null(
            viewModel.ErrorMessage);
        Assert.False(
            viewModel.HasError);
    }

    [Fact]
    public async Task RefreshHealthKeepsRowsForDegradedResponse()
    {
        OperationalHealthViewModel viewModel =
            CreateViewModel(
                new OperationalHealthResponseDto(
                    "Degraded",
                    DateTimeOffset.UtcNow,
                    [
                        new("Orders", "Healthy", 12, null, 200),
                        new("Payments", "Unavailable", 2001, "Timeout", null)
                    ]));

        await viewModel.RefreshHealthCommand.ExecuteAsync(null);

        Assert.Equal(
            "Degraded",
            viewModel.OverallStatus);
        Assert.True(
            viewModel.HasLoaded);
        Assert.Equal(
            2,
            viewModel.Services.Count);
        Assert.Equal(
            "Unavailable",
            viewModel.Services[1].Status);
        Assert.Null(
            viewModel.ErrorMessage);
    }

    [Fact]
    public async Task RefreshHealthShowsForbiddenMessage()
    {
        OperationalHealthViewModel viewModel =
            CreateViewModel(
                new ApiRequestException(
                    HttpStatusCode.Forbidden,
                    null));

        await viewModel.RefreshHealthCommand.ExecuteAsync(null);

        Assert.Equal(
            "Support or admin access is required to check operational health.",
            viewModel.ErrorMessage);
        Assert.Equal(
            "Operational health check failed.",
            viewModel.StatusText);
    }

    [Fact]
    public async Task RefreshHealthShowsGatewayUnavailableMessage()
    {
        OperationalHealthViewModel viewModel =
            CreateViewModel(
                new HttpRequestException(
                    "Gateway unavailable."));

        await viewModel.RefreshHealthCommand.ExecuteAsync(null);

        Assert.Equal(
            "The API Gateway could not be reached.",
            viewModel.ErrorMessage);
        Assert.True(
            viewModel.HasError);
    }

    private static OperationalHealthViewModel CreateViewModel(
        OperationalHealthResponseDto response)
    {
        return new OperationalHealthViewModel(
            new StubOperationalHealthApiClient(
                _ => Task.FromResult(response)),
            NullLogger<OperationalHealthViewModel>.Instance);
    }

    private static OperationalHealthViewModel CreateViewModel(
        Exception exception)
    {
        return new OperationalHealthViewModel(
            new StubOperationalHealthApiClient(
                _ => Task.FromException<OperationalHealthResponseDto>(
                    exception)),
            NullLogger<OperationalHealthViewModel>.Instance);
    }

    private sealed class StubOperationalHealthApiClient(
        Func<CancellationToken, Task<OperationalHealthResponseDto>> getHealth)
        : IOperationalHealthApiClient
    {
        public Task<OperationalHealthResponseDto>
            GetOperationalHealthAsync(
                CancellationToken cancellationToken)
        {
            return getHealth(
                cancellationToken);
        }
    }
}
