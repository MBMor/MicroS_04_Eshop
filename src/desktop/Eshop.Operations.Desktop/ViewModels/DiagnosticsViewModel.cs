using CommunityToolkit.Mvvm.Input;
using System.Runtime.InteropServices;
using Eshop.Operations.Desktop.Api;
using Eshop.Operations.Desktop.Configuration;
using Eshop.Operations.Desktop.Services;
using Microsoft.Extensions.Options;

namespace Eshop.Operations.Desktop.ViewModels;

public sealed partial class DiagnosticsViewModel
{
    public DiagnosticsViewModel(
        IOptions<DesktopOptions> desktopOptions,
        IOptions<ApiGatewayOptions> apiGatewayOptions,
        IOptions<ObservabilityOptions> observabilityOptions,
        IExternalUriLauncher externalUriLauncher)
    {
        ArgumentNullException.ThrowIfNull(desktopOptions);
        ArgumentNullException.ThrowIfNull(apiGatewayOptions);
        ArgumentNullException.ThrowIfNull(observabilityOptions);
        ArgumentNullException.ThrowIfNull(externalUriLauncher);

        _externalUriLauncher = externalUriLauncher;

        DesktopOptions desktop =
            desktopOptions.Value;

        ApiGatewayOptions apiGateway =
            apiGatewayOptions.Value;

        ObservabilityOptions observability =
            observabilityOptions.Value;

        EnvironmentName =
            desktop.EnvironmentName;

        ApiGatewayBaseAddress =
            apiGateway.BaseAddress;

        AspireDashboardUrl =
            observability.DashboardUrl.Trim();

        ApiGatewayTimeoutSeconds =
            apiGateway.TimeoutSeconds;

        ApplicationVersion =
            typeof(DiagnosticsViewModel)
                .Assembly
                .GetName()
                .Version?
                .ToString()
            ?? "Unknown";

        RuntimeDescription =
            RuntimeInformation.FrameworkDescription;

        OperatingSystemDescription =
            RuntimeInformation.OSDescription;

        ProcessArchitecture =
            RuntimeInformation.ProcessArchitecture.ToString();
    }

    public string EnvironmentName { get; }

    public string ApiGatewayBaseAddress { get; }

    public string AspireDashboardUrl { get; }

    public bool IsAspireDashboardConfigured =>
        !string.IsNullOrWhiteSpace(
            AspireDashboardUrl);

    public int ApiGatewayTimeoutSeconds { get; }

    public string ApplicationVersion { get; }

    public string RuntimeDescription { get; }

    public string OperatingSystemDescription { get; }

    public string ProcessArchitecture { get; }

    private readonly IExternalUriLauncher
        _externalUriLauncher;

    [RelayCommand(
        CanExecute = nameof(IsAspireDashboardConfigured))]
    private void OpenAspireDashboard()
    {
        if (!Uri.TryCreate(
                AspireDashboardUrl,
                UriKind.Absolute,
                out Uri? dashboardUri))
        {
            return;
        }

        _externalUriLauncher.Open(
            dashboardUri);
    }
}
