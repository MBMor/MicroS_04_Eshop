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
            var logger = _host.Services.GetRequiredService<ILogger<App>>();
            _logger = logger;

            await _host.StartAsync();

            LogApplicationStarted(logger);

            var mainWindow = _host.Services.GetRequiredService<MainWindow>();

            mainWindow.Closed += OnMainWindowClosed;

            MainWindow = mainWindow;
            mainWindow.Show();
        }
        catch (OptionsValidationException exception)
        {
            if (_logger is { } logger)
            {
                LogInvalidConfiguration(logger, exception);
            }

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
            if (_logger is { } logger)
            {
                LogStartupFailed(logger, exception);
            }

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
                if (_logger is { } logger)
                {
                    LogApplicationStopping(logger);
                }

                await _host.StopAsync(TimeSpan.FromSeconds(5));
            }
        }
        catch (Exception exception)
        {
            if (_logger is { } logger)
            {
                LogHostStopFailed(logger, exception);
            }
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

    [LoggerMessage(
    EventId = 1000,
    Level = LogLevel.Information,
    Message = "Eshop Operations Console started.")]
    private static partial void LogApplicationStarted(ILogger logger);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Critical,
        Message = "Application configuration is invalid.")]
    private static partial void LogInvalidConfiguration(
        ILogger logger,
        Exception exception);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Critical,
        Message = "Application startup failed.")]
    private static partial void LogStartupFailed(
        ILogger logger,
        Exception exception);

    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Information,
        Message = "Eshop Operations Console is shutting down.")]
    private static partial void LogApplicationStopping(ILogger logger);

    [LoggerMessage(
        EventId = 1004,
        Level = LogLevel.Error,
        Message = "An error occurred while stopping the application host.")]
    private static partial void LogHostStopFailed(
        ILogger logger,
        Exception exception);
}
