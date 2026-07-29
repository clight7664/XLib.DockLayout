namespace XLib.DockLayout.State;

internal sealed class DockLayoutCommandQueue : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _disposed;

    public async ValueTask<T> EnqueueAsync<T>(Func<CancellationToken, T> command, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return command(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask<T> EnqueueAsync<T>(Func<CancellationToken, ValueTask<T>> command, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await command(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return ValueTask.CompletedTask;
        }

        _disposed = true;
        _gate.Dispose();
        return ValueTask.CompletedTask;
    }
}
