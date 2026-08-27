using System.Windows;
using Eshop.Operations.Desktop.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Eshop.Operations.Desktop.ViewModels;

namespace Eshop.Operations.Desktop;

public partial class App : Application
{
    private IHost? _host;
    private ILogger<App>? _logger;
    private bool _shutdownStarted;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            _host = CreateHost(e.Args);
            _logger = _host.Services.GetRequiredService<ILogger<App>>();

            await _host.StartAsync();

            _logger.LogInformation("Eshop Operations Console started.");

            var mainWindow = _host.Services.GetRequiredService<MainWindow>();

            mainWindow.Closed += OnMainWindowClosed;

            MainWindow = mainWindow;
            mainWindow.Show();
        }
        catch (OptionsValidationException exception)
        {
            _logger?.LogCritical(
                exception,
                "Application configuration is invalid.");

            MessageBox.Show(
                string.Join(
                    Environment.NewLine,
                    exception.Failures),
                "Invalid application configuration",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            DisposeHost();
            Shutdown(-1);
        }
        catch (Exception exception)
        {
            _logger?.LogCritical(
                exception,
                "Application startup failed.");

            MessageBox.Show(
                "The application could not start. Check the application logs for details.",
                "Startup failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            DisposeHost();
            Shutdown(-1);
        }
    }

    private static IHost CreateHost(string[] args)
    {
        var settings = new HostApplicationBuilderSettings
        {
            Args = args,
            ContentRootPath = AppContext.BaseDirectory
        };

        var builder = Host.CreateApplicationBuilder(settings);

        builder.Services.AddSingleton<
            IValidateOptions<DesktopOptions>,
            DesktopOptionsValidator>();

        builder.Services
            .AddOptions<DesktopOptions>()
            .Bind(
                builder.Configuration.GetSection(
                    DesktopOptions.SectionName))
            .ValidateOnStart();

        builder.Services.AddSingleton<ShellViewModel>();
        builder.Services.AddSingleton<MainWindow>();

        return builder.Build();
    }

    private async void OnMainWindowClosed(
        object? sender,
        EventArgs e)
    {
        if (_shutdownStarted)
        {
            return;
        }

        _shutdownStarted = true;

        if (sender is MainWindow mainWindow)
        {
            mainWindow.Closed -= OnMainWindowClosed;
        }

        try
        {
            if (_host is not null)
            {
                _logger?.LogInformation(
                    "Eshop Operations Console is shutting down.");

                await _host.StopAsync(TimeSpan.FromSeconds(5));
            }
        }
        catch (Exception exception)
        {
            _logger?.LogError(
                exception,
                "An error occurred while stopping the application host.");
        }
        finally
        {
            DisposeHost();
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        DisposeHost();

        base.OnExit(e);
    }

    private void DisposeHost()
    {
        _host?.Dispose();
        _host = null;
    }
}
