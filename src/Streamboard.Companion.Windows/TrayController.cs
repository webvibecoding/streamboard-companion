using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Streamboard.Companion.Core.Api;
using Streamboard.Companion.Core.Detection;
using Streamboard.Companion.Core.Tracking;

namespace Streamboard.Companion.Windows;

internal sealed class TrayController : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _statusItem;
    private readonly ToolStripMenuItem _pauseItem;
    private readonly ToolStripMenuItem _mappingItem;
    private readonly TrackingController _trackingControl;
    private readonly Func<Task> _signOut;
    private readonly Func<Task> _connect;
    private readonly Func<Task> _checkUpdates;
    private readonly Action _openStatus;
    private readonly Action _openMapping;
    private readonly Action _exit;
    private Action? _balloonAction;
    private bool _disposed;

    public TrayController(
        TrackingController trackingControl,
        Func<Task> connect,
        Func<Task> signOut,
        Func<Task> checkUpdates,
        Action openStatus,
        Action openMapping,
        Action exit)
    {
        _trackingControl = trackingControl;
        _connect = connect;
        _signOut = signOut;
        _checkUpdates = checkUpdates;
        _openStatus = openStatus;
        _openMapping = openMapping;
        _exit = exit;

        _statusItem = new ToolStripMenuItem("Запуск…") { Enabled = false };
        _pauseItem = new ToolStripMenuItem("Приостановить отслеживание") { CheckOnClick = true };
        _pauseItem.CheckedChanged += PauseChanged;
        _mappingItem = new ToolStripMenuItem("Выбрать категорию…") { Enabled = false };
        _mappingItem.Click += (_, _) => _openMapping();

        var detailsItem = new ToolStripMenuItem("Открыть состояние");
        detailsItem.Click += (_, _) => _openStatus();
        var connectItem = new ToolStripMenuItem("Подключить Streamboard");
        connectItem.Click += async (_, _) => await ConnectAsync().ConfigureAwait(true);
        var dashboardItem = new ToolStripMenuItem("Открыть Streamboard");
        dashboardItem.Click += (_, _) => OpenUri(App.ProductionBaseUri);
        var updateItem = new ToolStripMenuItem("Проверить обновления");
        updateItem.Click += async (_, _) => await CheckUpdatesAsync().ConfigureAwait(true);
        var signOutItem = new ToolStripMenuItem("Отключить устройство");
        signOutItem.Click += async (_, _) => await SignOutAsync().ConfigureAwait(true);
        var exitItem = new ToolStripMenuItem("Выход");
        exitItem.Click += (_, _) => _exit();

        var menu = new ContextMenuStrip();
        menu.Items.AddRange(
        [
            _statusItem,
            new ToolStripSeparator(),
            detailsItem,
            connectItem,
            _pauseItem,
            _mappingItem,
            new ToolStripSeparator(),
            dashboardItem,
            updateItem,
            signOutItem,
            exitItem,
        ]);

        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "Streamboard Companion",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _notifyIcon.DoubleClick += (_, _) => _openStatus();
        _notifyIcon.BalloonTipClicked += (_, _) => _balloonAction?.Invoke();
    }

    public void SetStatus(CompanionStatus status)
    {
        if (_disposed)
        {
            return;
        }

        _statusItem.Text = status.Message;
        _notifyIcon.Text = TruncateTooltip($"Streamboard · {status.Message}");
        _mappingItem.Enabled = status.Game is not null;
        _pauseItem.CheckedChanged -= PauseChanged;
        _pauseItem.Checked = status.State == CompanionRunState.Paused;
        _pauseItem.Text = status.State == CompanionRunState.Paused
            ? "Возобновить отслеживание"
            : "Приостановить отслеживание";
        _pauseItem.CheckedChanged += PauseChanged;
    }

    public void ShowMappingRequired(ResolvedGame game)
    {
        if (_disposed)
        {
            return;
        }

        _mappingItem.Enabled = true;
        _balloonAction = _openMapping;
        _notifyIcon.ShowBalloonTip(
            7000,
            "Нужно выбрать категорию",
            $"Для «{game.DisplayName}» требуется ручное сопоставление.",
            ToolTipIcon.Info);
    }

    public void ShowPairing(PairingResponse pairing)
    {
        if (_disposed)
        {
            return;
        }

        _statusItem.Text = $"Подключение устройства · код {pairing.UserCode}";
        try
        {
            System.Windows.Clipboard.SetText(pairing.UserCode);
        }
        catch (Exception)
        {
            // Clipboard can be temporarily locked by another process.
        }

        _balloonAction = null;
        _notifyIcon.ShowBalloonTip(
            10000,
            "Подключите Streamboard Companion",
            $"Код {pairing.UserCode} скопирован. Завершите подключение в браузере.",
            ToolTipIcon.Info);
    }

    public void ShowUpdateAvailable(Uri releaseUri, string version)
    {
        _balloonAction = () => OpenUri(releaseUri);
        _notifyIcon.ShowBalloonTip(
            10000,
            "Доступно обновление",
            $"Streamboard Companion {version}. Нажмите, чтобы открыть GitHub Releases.",
            ToolTipIcon.Info);
    }

    public void ShowInformation(string message)
    {
        if (!_disposed)
        {
            _notifyIcon.ShowBalloonTip(5000, "Streamboard Companion", message, ToolTipIcon.Info);
        }
    }

    public void ShowError(string message)
    {
        if (!_disposed)
        {
            _notifyIcon.ShowBalloonTip(7000, "Streamboard Companion", message, ToolTipIcon.Error);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }

    private void PauseChanged(object? sender, EventArgs eventArgs)
    {
        _trackingControl.SetPaused(_pauseItem.Checked);
        _pauseItem.Text = _pauseItem.Checked
            ? "Возобновить отслеживание"
            : "Приостановить отслеживание";
    }

    private async Task SignOutAsync()
    {
        try
        {
            await _signOut().ConfigureAwait(true);
            ShowInformation("Устройство отключено. При следующей синхронизации потребуется подключение.");
        }
        catch (Exception)
        {
            ShowError("Не удалось отключить устройство.");
        }
    }

    private async Task ConnectAsync()
    {
        try
        {
            await _connect().ConfigureAwait(true);
            ShowInformation("Устройство подключено к Streamboard.");
        }
        catch (Exception)
        {
            ShowError("Не удалось подключить устройство.");
        }
    }

    private async Task CheckUpdatesAsync()
    {
        try
        {
            await _checkUpdates().ConfigureAwait(true);
        }
        catch (Exception)
        {
            ShowError("Не удалось проверить обновления.");
        }
    }

    private static void OpenUri(Uri uri)
    {
        if (uri.Scheme == Uri.UriSchemeHttps)
        {
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        }
    }

    private static string TruncateTooltip(string value) =>
        value.Length <= 63 ? value : value[..60] + "…";
}
