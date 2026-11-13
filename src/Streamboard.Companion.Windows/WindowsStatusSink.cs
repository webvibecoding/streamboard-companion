using System.Windows.Threading;
using Streamboard.Companion.Core.Detection;
using Streamboard.Companion.Core.Tracking;

namespace Streamboard.Companion.Windows;

internal sealed class WindowsStatusSink : ICompanionStatusSink
{
    private readonly Dispatcher _dispatcher;
    private readonly TrayController _tray;
    private readonly StatusWindow _window;

    public WindowsStatusSink(Dispatcher dispatcher, TrayController tray, StatusWindow window)
    {
        _dispatcher = dispatcher;
        _tray = tray;
        _window = window;
    }

    public async ValueTask UpdateStatusAsync(
        CompanionStatus status,
        CancellationToken cancellationToken = default)
    {
        await _dispatcher.InvokeAsync(
            () =>
            {
                _tray.SetStatus(status);
                _window.UpdateStatus(status);
            },
            DispatcherPriority.Normal,
            cancellationToken);
    }

    public async ValueTask MappingRequiredAsync(
        ResolvedGame unresolvedGame,
        CancellationToken cancellationToken = default)
    {
        await _dispatcher.InvokeAsync(
            () => _tray.ShowMappingRequired(unresolvedGame),
            DispatcherPriority.Normal,
            cancellationToken);
    }
}

