using System.Runtime.InteropServices;
using Eshop.Operations.Desktop.Api;
using Eshop.Operations.Desktop.Configuration;
using Microsoft.Extensions.Options;

namespace Eshop.Operations.Desktop.ViewModels;

public sealed class DiagnosticsViewModel
{
    public DiagnosticsViewModel(
        IOptions<DesktopOptions> desktopOptions,
        IOptions<ApiGatewayOptions> apiGatewayOptions)
    {
        ArgumentNullException.ThrowIfNull(desktopOptions);
        ArgumentNullException.ThrowIfNull(apiGatewayOptions);

        DesktopOptions desktop =
            desktopOptions.Value;

        ApiGatewayOptions apiGateway =
            apiGatewayOptions.Value;

        EnvironmentName =
            desktop.EnvironmentName;

        ApiGatewayBaseAddress =
            apiGateway.BaseAddress;

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

    public int ApiGatewayTimeoutSeconds { get; }

    public string ApplicationVersion { get; }

    public string RuntimeDescription { get; }

    public string OperatingSystemDescription { get; }

    public string ProcessArchitecture { get; }
}
