using Eshop.Operations.Desktop.Api;
using Eshop.Operations.Desktop.Configuration;
using Eshop.Operations.Desktop.Services;
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

        IOptions<ObservabilityOptions> observabilityOptions =
            Options.Create(
                new ObservabilityOptions
                {
                    DashboardUrl =
                        "https://aspire.example.test/"
                });

        var launcher =
            new StubExternalUriLauncher();

        var viewModel =
            new DiagnosticsViewModel(
                desktopOptions,
                apiGatewayOptions,
                observabilityOptions,
                launcher);

        Assert.Equal(
            "QA",
            viewModel.EnvironmentName);

        Assert.Equal(
            "https://gateway.example.test/",
            viewModel.ApiGatewayBaseAddress);

        Assert.Equal(
            "https://aspire.example.test/",
            viewModel.AspireDashboardUrl);

        Assert.True(
            viewModel.IsAspireDashboardConfigured);

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

    [Fact]
    public void OpenAspireDashboardCommandUsesConfiguredUri()
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

        IOptions<ObservabilityOptions> observabilityOptions =
            Options.Create(
                new ObservabilityOptions
                {
                    DashboardUrl =
                        "https://aspire.example.test/"
                });

        var launcher =
            new StubExternalUriLauncher();

        var viewModel =
            new DiagnosticsViewModel(
                desktopOptions,
                apiGatewayOptions,
                observabilityOptions,
                launcher);

        viewModel
            .OpenAspireDashboardCommand
            .Execute(null);

        Assert.Equal(
            new Uri(
                "https://aspire.example.test/"),
            launcher.OpenedUri);
    }

    [Fact]
    public void OpenAspireDashboardCommandIsDisabledWithoutConfiguredUrl()
    {
        IOptions<DesktopOptions> desktopOptions =
            Options.Create(
                new DesktopOptions
                {
                    EnvironmentName = "Local"
                });

        IOptions<ApiGatewayOptions> apiGatewayOptions =
            Options.Create(
                new ApiGatewayOptions
                {
                    BaseAddress =
                        "http://localhost:5080/",
                    TimeoutSeconds = 15
                });

        IOptions<ObservabilityOptions> observabilityOptions =
            Options.Create(
                new ObservabilityOptions());

        var launcher =
            new StubExternalUriLauncher();

        var viewModel =
            new DiagnosticsViewModel(
                desktopOptions,
                apiGatewayOptions,
                observabilityOptions,
                launcher);

        Assert.False(
            viewModel
                .OpenAspireDashboardCommand
                .CanExecute(null));

        Assert.False(
            viewModel.IsAspireDashboardConfigured);
    }

    private sealed class StubExternalUriLauncher
        : IExternalUriLauncher
    {
        public Uri? OpenedUri
        {
            get;
            private set;
        }

        public void Open(
            Uri uri)
        {
            OpenedUri =
                uri;
        }
    }
}
