using System.Collections.Concurrent;

namespace WechatAIClient.Services;

/// <summary>Ref-counted keyed gate to avoid dispose races on concurrent media fetches.</summary>
public sealed class KeyedSemaphoreGate
{
    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    public async Task<IDisposable> AcquireAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var entry = _entries.AddOrUpdate(
            key,
            static _ => new Entry(),
            static (_, existing) =>
            {
                Interlocked.Increment(ref existing.RefCount);
                return existing;
            });

        try
        {
            await entry.Semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            ReleaseRef(key, entry, waited: false);
            throw;
        }

        return new Releaser(this, key, entry);
    }

    private void ReleaseRef(string key, Entry entry, bool waited)
    {
        if (waited)
        {
            entry.Semaphore.Release();
        }

        if (Interlocked.Decrement(ref entry.RefCount) == 0)
        {
            if (_entries.TryRemove(key, out var removed) && ReferenceEquals(removed, entry))
            {
                // Do not Dispose — waiters may still be racing; GC collects after last use.
            }
        }
    }

    private sealed class Entry
    {
        public readonly SemaphoreSlim Semaphore = new(1, 1);
        public int RefCount = 1;
    }

    private sealed class Releaser : IDisposable
    {
        private readonly KeyedSemaphoreGate _owner;
        private readonly string _key;
        private readonly Entry _entry;
        private int _disposed;

        public Releaser(KeyedSemaphoreGate owner, string key, Entry entry)
        {
            _owner = owner;
            _key = key;
            _entry = entry;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            _owner.ReleaseRef(_key, _entry, waited: true);
        }
    }
}
