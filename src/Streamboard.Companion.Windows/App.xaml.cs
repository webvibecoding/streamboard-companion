using System.Net.Http.Headers;
using System.Net.Http;
using System.Windows;
using Microsoft.Win32;
using Streamboard.Companion.Core.Api;
using Streamboard.Companion.Core.Auth;
using Streamboard.Companion.Core.Detection;
using Streamboard.Companion.Core.Tracking;

namespace Streamboard.Companion.Windows;

public partial class App : System.Windows.Application
{
    private static readonly Uri ProductionBaseUri = new("https://streamboard.sip1ay.ru/");

    private readonly CancellationTokenSource _shutdown = new();
    private SingleInstanceGuard? _singleInstance;
    private HttpClient? _apiHttpClient;
    private HttpClient? _updateHttpClient;
    private DeviceTokenManager? _tokens;
    private IStreamboardApiClient? _api;
    private IExecutableMappingStore? _executableMappings;
    private TrackingController? _trackingControl;
    private StatusWindow? _statusWindow;
    private TrayController? _tray;
    private ReleaseUpdateChecker? _updates;
    private LocalLog? _log;
    private Task? _trackingTask;
    private bool _exiting;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (!SingleInstanceGuard.TryAcquire(out _singleInstance))
        {
            System.Windows.MessageBox.Show(
                "Streamboard Companion уже запущен.",
                "Streamboard Companion",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown();
            return;
        }

        var dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Streamboard Companion");
        _log = new LocalLog(Path.Combine(dataDirectory, "logs", "companion.log"));
        _log.Write("application_started");

        DispatcherUnhandledException += (_, args) =>
        {
            _log.Write("dispatcher_error", args.Exception);
            _tray?.ShowError("Произошла внутренняя ошибка. Подробности сохранены в локальном журнале.");
            args.Handled = true;
        };

        _trackingControl = new TrackingController();
        _executableMappings = new JsonExecutableMappingStore(
            Path.Combine(dataDirectory, "executable-mappings.json"));
        _statusWindow = new StatusWindow(_trackingControl, OpenMappingWindow);

        _apiHttpClient = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
        {
            BaseAddress = ProductionBaseUri,
            Timeout = TimeSpan.FromSeconds(20),
        };
        _apiHttpClient.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("Streamboard.Companion", "1.0.0"));
        _api = new StreamboardApiClient(_apiHttpClient);

        _updateHttpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        _updates = new ReleaseUpdateChecker(
            _updateHttpClient,
            Path.Combine(dataDirectory, "update-state.json"));

        _tray = new TrayController(
            _trackingControl,
            ConnectAsync,
            SignOutAsync,
            () => CheckUpdatesAsync(force: true),
            () => _statusWindow.ShowFromTray(),
            OpenMappingWindow,
            ExitApplication);

        var pairingInteraction = new BrowserPairingInteraction(Dispatcher, _tray);
        _tokens = new DeviceTokenManager(
            _api,
            new WindowsCredentialStore(),
            pairingInteraction,
            Environment.MachineName);

        var resolver = new GameResolver(
            InstalledGameCatalogFactory.Create(),
            _executableMappings);
        var publisher = new AuthenticatedGameEventPublisher(_api, _tokens);
        var statusSink = new WindowsStatusSink(Dispatcher, _tray, _statusWindow);
        var coordinator = new GameTrackingCoordinator(
            new WindowsForegroundProcessProvider(),
            resolver,
            publisher,
            statusSink,
            _trackingControl);

        SystemEvents.PowerModeChanged += PowerModeChanged;
        _trackingTask = Task.Run(() => coordinator.RunAsync(_shutdown.Token));
        _ = ObserveTrackingTaskAsync(_trackingTask);
        _ = CheckUpdatesAsync(force: false);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _exiting = true;
        SystemEvents.PowerModeChanged -= PowerModeChanged;
        _shutdown.Cancel();
        _statusWindow?.CloseForExit();
        _tray?.Dispose();
        _apiHttpClient?.Dispose();
        _updateHttpClient?.Dispose();
        _singleInstance?.Dispose();
        _shutdown.Dispose();
        _log?.Write("application_stopped");
        base.OnExit(e);
    }

    private async Task ObserveTrackingTaskAsync(Task trackingTask)
    {
        try
        {
            await trackingTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
        {
            // Normal application shutdown.
        }
        catch (Exception exception)
        {
            _log?.Write("tracking_stopped", exception);
            await Dispatcher.InvokeAsync(
                () => _tray?.ShowError("Отслеживание остановлено из-за внутренней ошибки."));
        }
    }

    private async Task SignOutAsync()
    {
        if (_tokens is not null)
        {
            await _tokens.SignOutAsync(_shutdown.Token).ConfigureAwait(true);
        }
    }

    private async Task ConnectAsync()
    {
        if (_tokens is not null)
        {
            await _tokens.GetAccessTokenAsync(_shutdown.Token, allowPairing: true).ConfigureAwait(true);
        }
    }

    private async Task CheckUpdatesAsync(bool force)
    {
        if (_updates is null || _tray is null)
        {
            return;
        }

        try
        {
            var result = await _updates.CheckAsync(force, _shutdown.Token).ConfigureAwait(true);
            if (result.UpdateAvailable && result.ReleaseUri is not null)
            {
                _tray.ShowUpdateAvailable(result.ReleaseUri, result.Version ?? "новая версия");
            }
            else if (force && result.Checked)
            {
                _tray.ShowInformation("Установлена актуальная версия.");
            }
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
        {
            // Shutdown cancels background update checks.
        }
        catch (Exception exception)
        {
            _log?.Write("update_check_failed", exception);
            if (force)
            {
                throw;
            }
        }
    }

    private void OpenMappingWindow()
    {
        if (_api is null || _tokens is null || _executableMappings is null || _statusWindow is null)
        {
            return;
        }

        var game = _statusWindow.CurrentGame;
        if (game is null)
        {
            _tray?.ShowInformation("Сначала переключитесь на игру и дождитесь её определения.");
            return;
        }

        var window = new CategoryMappingWindow(
            game, _api, _tokens, _executableMappings, _statusWindow.GetResultsFor(game.Key))
        {
            Owner = _statusWindow.IsVisible ? _statusWindow : null,
        };
        if (window.ShowDialog() == true)
        {
            _tray?.ShowInformation("Сопоставление сохранено. Категория проверится повторно.");
            _trackingControl?.NotifySystemResume();
        }
    }

    private void PowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume)
        {
            _trackingControl?.NotifySystemResume();
            _log?.Write("system_resumed");
        }
    }

    private void ExitApplication()
    {
        if (_exiting)
        {
            return;
        }

        _exiting = true;
        Shutdown();
    }
}
