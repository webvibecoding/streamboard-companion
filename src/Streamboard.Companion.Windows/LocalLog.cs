namespace Streamboard.Companion.Windows;

internal sealed class LocalLog
{
    private const long MaxBytes = 512 * 1024;
    private readonly string _path;
    private readonly object _gate = new();

    public LocalLog(string path)
    {
        _path = path;
    }

    public void Write(string eventName, Exception? exception = null)
    {
        var safeEvent = new string((eventName ?? "event")
            .Where(static character => char.IsLetterOrDigit(character) || character is '_' or '-')
            .Take(80)
            .ToArray());
        var line = $"{DateTimeOffset.UtcNow:O} {safeEvent}";
        if (exception is not null)
        {
            line += $" exception={exception.GetType().Name}";
        }

        lock (_gate)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                RotateIfNeeded();
                File.AppendAllText(_path, line + Environment.NewLine);
            }
            catch (Exception logException) when (
                logException is IOException or UnauthorizedAccessException)
            {
                // Logging never interrupts tracking and never serializes request data.
            }
        }
    }

    private void RotateIfNeeded()
    {
        if (!File.Exists(_path) || new FileInfo(_path).Length < MaxBytes)
        {
            return;
        }

        var oldest = _path + ".3";
        if (File.Exists(oldest))
        {
            File.Delete(oldest);
        }

        for (var index = 2; index >= 1; index--)
        {
            var source = _path + $".{index}";
            if (File.Exists(source))
            {
                File.Move(source, _path + $".{index + 1}");
            }
        }

        File.Move(_path, _path + ".1");
    }
}
