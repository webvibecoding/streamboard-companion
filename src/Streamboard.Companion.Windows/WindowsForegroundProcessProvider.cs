using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Streamboard.Companion.Core.Detection;

namespace Streamboard.Companion.Windows;

internal sealed class WindowsForegroundProcessProvider : IForegroundProcessProvider
{
    private const uint ProcessQueryLimitedInformation = 0x1000;
    private readonly TimeProvider _timeProvider;
    private readonly int _ownProcessId = Environment.ProcessId;

    public WindowsForegroundProcessProvider(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public ValueTask<ForegroundProcessSnapshot?> GetForegroundProcessAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var window = GetForegroundWindow();
        if (window == IntPtr.Zero)
        {
            return ValueTask.FromResult<ForegroundProcessSnapshot?>(null);
        }

        _ = GetWindowThreadProcessId(window, out var processIdValue);
        var processId = unchecked((int)processIdValue);
        if (processId <= 0 || processId == _ownProcessId)
        {
            return ValueTask.FromResult<ForegroundProcessSnapshot?>(null);
        }

        try
        {
            using var process = Process.GetProcessById(processId);
            var name = process.ProcessName;
            if (!name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                name += ".exe";
            }

            var path = TryQueryImagePath(processId) ?? TryReadMainModule(process);
            var title = ReadWindowTitle(window);
            return ValueTask.FromResult<ForegroundProcessSnapshot?>(new(
                processId,
                name,
                path,
                title,
                _timeProvider.GetUtcNow()));
        }
        catch (Exception exception) when (
            exception is ArgumentException
                or InvalidOperationException
                or Win32Exception
                or NotSupportedException)
        {
            return ValueTask.FromResult<ForegroundProcessSnapshot?>(null);
        }
    }

    private static string? TryQueryImagePath(int processId)
    {
        var processHandle = OpenProcess(ProcessQueryLimitedInformation, false, processId);
        if (processHandle == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var capacity = 32768u;
            var buffer = new StringBuilder((int)capacity);
            return QueryFullProcessImageName(processHandle, 0, buffer, ref capacity)
                ? buffer.ToString()
                : null;
        }
        finally
        {
            CloseHandle(processHandle);
        }
    }

    private static string? TryReadMainModule(Process process)
    {
        try
        {
            return process.MainModule?.FileName;
        }
        catch (Exception exception) when (
            exception is Win32Exception or InvalidOperationException or NotSupportedException)
        {
            return null;
        }
    }

    private static string? ReadWindowTitle(IntPtr window)
    {
        var length = GetWindowTextLength(window);
        if (length <= 0)
        {
            return null;
        }

        var buffer = new StringBuilder(length + 1);
        return GetWindowText(window, buffer, buffer.Capacity) > 0 ? buffer.ToString() : null;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetWindowText(IntPtr window, StringBuilder text, int maxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetWindowTextLength(IntPtr window);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint access, bool inheritHandle, int processId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageName(
        IntPtr process,
        int flags,
        StringBuilder executableName,
        ref uint size);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
}

