# Streamboard Companion

Streamboard Companion is a Windows 11 tray application that detects the game in
the foreground and keeps the category of active Twitch and VK Play Live streams
in sync through [Streamboard](https://streamboard.sip1ay.pro).

## Version 1.0 scope

- Detects foreground games from Steam, Epic Games manifests, and executable metadata.
- Waits for a stable game for 10 seconds and rechecks active streams every 30 seconds.
- Updates only the category on connected, currently live Twitch and VK Play Live channels.
- Leaves titles, tags, other metadata, offline channels, and all other platforms unchanged.
- Reuses exact category matches; ambiguous games are shown for manual selection.
- Does not change the last category when a game closes or focus moves to a non-game app.
- Runs only when launched manually; it does not register itself for Windows startup.

## Installation

Download one of the assets from the latest GitHub Release:

- `Streamboard.Companion-<version>-win-x64-portable.zip` — extract and run
  `Streamboard.Companion.exe`.
- `Streamboard.Companion-Setup-<version>.exe` — per-user installer with Start Menu
  and a desktop shortcut selected by default.

The release binaries are not code-signed. Windows SmartScreen may display an
"Unknown publisher" warning. Verify the downloaded file against `SHA256SUMS.txt`
before running it.

## Pairing

1. Start the Companion and choose **Connect Streamboard**.
2. The app opens the Streamboard verification page in your default browser.
3. Sign in with Google if necessary and approve the displayed device code.
4. Return to the Companion. Twitch and VK Play Live remain connected only to the
   Streamboard server; their OAuth tokens are never copied to Windows.

The refresh credential is stored in Windows Credential Manager. You can revoke
the device from Streamboard or disconnect it from the tray application.

## Saved mappings

Open **Сохранённые сопоставления** from the tray or status window. Select the
Twitch or VK Play Live cell to change a category or fill in a missing one. The
game can be closed while you edit. Companion uses the saved category on the
next sync for an active game and a live stream.

## Privacy

The server receives a storefront game identifier, display name, executable file
name, and a one-way identifier for fallback executable mappings. Full executable
paths, window titles, process lists, and platform OAuth tokens are not uploaded.
See [PRIVACY.md](PRIVACY.md) for details.

## Development

Requirements:

- Windows 11
- .NET SDK 8

```powershell
dotnet restore Streamboard.Companion.sln
dotnet build Streamboard.Companion.sln -c Release
dotnet test Streamboard.Companion.sln -c Release
```

Release builds always connect to `https://streamboard.sip1ay.pro`. Tests inject a
mock HTTP transport and never require production credentials.

## Security

Please report vulnerabilities privately as described in [SECURITY.md](SECURITY.md).

## License

[MIT](LICENSE)
