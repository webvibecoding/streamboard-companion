using System.Diagnostics;
using System.Windows.Threading;
using Streamboard.Companion.Core.Api;
using Streamboard.Companion.Core.Auth;

namespace Streamboard.Companion.Windows;

internal sealed class BrowserPairingInteraction : IPairingInteraction
{
    private readonly Dispatcher _dispatcher;
    private readonly TrayController _tray;

    public BrowserPairingInteraction(Dispatcher dispatcher, TrayController tray)
    {
        _dispatcher = dispatcher;
        _tray = tray;
    }

    public async ValueTask PairingStartedAsync(
        PairingResponse pairing,
        CancellationToken cancellationToken = default)
    {
        await _dispatcher.InvokeAsync(
            () => _tray.ShowPairing(pairing),
            DispatcherPriority.Normal,
            cancellationToken);

        var target = pairing.VerificationUriComplete ?? pairing.VerificationUri;
        if (!Uri.TryCreate(target, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || !uri.IsDefaultPort
            || uri.IdnHost != App.ProductionBaseUri.IdnHost
            || uri.UserInfo.Length != 0)
        {
            throw new InvalidOperationException("pairing_verification_uri_invalid");
        }

        Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
    }
}

