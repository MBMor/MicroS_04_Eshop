using Eshop.Operations.Desktop.Api;
using Eshop.Operations.Desktop.Configuration;
using Eshop.Operations.Desktop.ViewModels;
using Microsoft.Extensions.Options;
using Xunit;

namespace Eshop.Operations.Desktop.Tests.ViewModels;

public sealed class DiagnosticsViewModelTests
{
    [Fact]
    public void ConstructorExposesDesktopConfiguration()
    {
        IOptions<DesktopOptions> desktopOptions =
            Options.Create(
                new DesktopOptions
                {
                    EnvironmentName = "QA"
                });

        IOptions<ApiGatewayOptions> apiGatewayOptions =
            Options.Create(
                new ApiGatewayOptions
                {
                    BaseAddress =
                        "https://gateway.example.test/",
                    TimeoutSeconds = 30
                });

        var viewModel =
            new DiagnosticsViewModel(
                desktopOptions,
                apiGatewayOptions);

        Assert.Equal(
            "QA",
            viewModel.EnvironmentName);

        Assert.Equal(
            "https://gateway.example.test/",
            viewModel.ApiGatewayBaseAddress);

        Assert.Equal(
            30,
            viewModel.ApiGatewayTimeoutSeconds);

        Assert.False(
            string.IsNullOrWhiteSpace(
                viewModel.ApplicationVersion));

        Assert.False(
            string.IsNullOrWhiteSpace(
                viewModel.RuntimeDescription));

        Assert.False(
            string.IsNullOrWhiteSpace(
                viewModel.OperatingSystemDescription));

        Assert.False(
            string.IsNullOrWhiteSpace(
                viewModel.ProcessArchitecture));
    }
}
