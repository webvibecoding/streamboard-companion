namespace Streamboard.Companion.Core.Detection;

public sealed class ExecutableDenylist
{
    private static readonly string[] DefaultNames =
    [
        "applicationframehost.exe",
        "battle.net.exe",
        "chrome.exe",
        "cmd.exe",
        "code.exe",
        "conhost.exe",
        "devenv.exe",
        "discord.exe",
        "dwm.exe",
        "easyanticheat.exe",
        "easyanticheat_eos.exe",
        "epicgameslauncher.exe",
        "explorer.exe",
        "firefox.exe",
        "gamebar.exe",
        "gamingservices.exe",
        "msedge.exe",
        "notepad.exe",
        "notepad++.exe",
        "obs.exe",
        "obs64.exe",
        "powershell.exe",
        "pwsh.exe",
        "riotclientservices.exe",
        "searchhost.exe",
        "shellexperiencehost.exe",
        "steam.exe",
        "steamwebhelper.exe",
        "streamboard.companion.exe",
        "taskmgr.exe",
        "teams.exe",
        "upc.exe",
        "uplaywebcore.exe",
        "windowsterminal.exe",
        "winword.exe",
    ];

    private readonly HashSet<string> _names;

    public ExecutableDenylist(IEnumerable<string>? additionalNames = null)
    {
        _names = new HashSet<string>(DefaultNames, StringComparer.OrdinalIgnoreCase);
        if (additionalNames is null)
        {
            return;
        }

        foreach (var name in additionalNames)
        {
            var normalized = Path.GetFileName(name ?? string.Empty).Trim();
            if (normalized.Length > 0)
            {
                _names.Add(normalized);
            }
        }
    }

    public bool Contains(string executableName) =>
        _names.Contains(Path.GetFileName(executableName ?? string.Empty));
}

