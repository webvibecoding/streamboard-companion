namespace Streamboard.Companion.Windows;

internal sealed class SingleInstanceGuard : IDisposable
{
    private readonly Mutex _mutex;
    private bool _ownsMutex;

    private SingleInstanceGuard(Mutex mutex, bool ownsMutex)
    {
        _mutex = mutex;
        _ownsMutex = ownsMutex;
    }

    public static bool TryAcquire(out SingleInstanceGuard? guard)
    {
        var mutex = new Mutex(
            initiallyOwned: true,
            name: @"Local\Streamboard.Companion",
            createdNew: out var createdNew);
        guard = new SingleInstanceGuard(mutex, createdNew);
        return createdNew;
    }

    public void Dispose()
    {
        if (_ownsMutex)
        {
            _mutex.ReleaseMutex();
            _ownsMutex = false;
        }

        _mutex.Dispose();
    }
}

