namespace RagPipeline.BuildingBlocks.Bootstrap;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

public sealed class ProofAttemptLimiter : IProofAttemptLimiter
{
    private readonly int _maxFailuresPerSource;
    private readonly int _maxGlobalFailures;
    private readonly TimeSpan _window;
    private readonly TimeProvider _timeProvider;

    private readonly object _syncLock = new();
    private readonly List<DateTimeOffset> _globalFailures = new();
    private readonly ConcurrentDictionary<string, List<DateTimeOffset>> _sourceFailures = new(StringComparer.OrdinalIgnoreCase);

    public ProofAttemptLimiter(
        int maxFailuresPerSource = 5,
        int maxGlobalFailures = 20,
        TimeSpan? window = null,
        TimeProvider? timeProvider = null)
    {
        _maxFailuresPerSource = maxFailuresPerSource;
        _maxGlobalFailures = maxGlobalFailures;
        _window = window ?? TimeSpan.FromMinutes(15);
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public bool IsBlocked(string? sourceKey)
    {
        var now = _timeProvider.GetUtcNow();
        var cutoff = now - _window;

        lock (_syncLock)
        {
            _globalFailures.RemoveAll(t => t < cutoff);
            if (_globalFailures.Count >= _maxGlobalFailures)
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(sourceKey))
            {
                if (_sourceFailures.TryGetValue(sourceKey, out var sourceList))
                {
                    sourceList.RemoveAll(t => t < cutoff);
                    if (sourceList.Count >= _maxFailuresPerSource)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }

    public void RecordFailure(string? sourceKey)
    {
        var now = _timeProvider.GetUtcNow();
        var cutoff = now - _window;

        lock (_syncLock)
        {
            _globalFailures.RemoveAll(t => t < cutoff);
            _globalFailures.Add(now);

            if (!string.IsNullOrWhiteSpace(sourceKey))
            {
                var sourceList = _sourceFailures.GetOrAdd(sourceKey, _ => new List<DateTimeOffset>());
                sourceList.RemoveAll(t => t < cutoff);
                sourceList.Add(now);
            }
        }
    }

    public void RecordSuccess(string? sourceKey)
    {
        // Success on one source does NOT clear global failures or other sources' failures.
        if (!string.IsNullOrWhiteSpace(sourceKey))
        {
            lock (_syncLock)
            {
                if (_sourceFailures.TryGetValue(sourceKey, out var sourceList))
                {
                    sourceList.Clear();
                }
            }
        }
    }

    public void Reset()
    {
        lock (_syncLock)
        {
            _globalFailures.Clear();
            _sourceFailures.Clear();
        }
    }
}
