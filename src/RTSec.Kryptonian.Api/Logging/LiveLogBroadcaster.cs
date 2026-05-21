using RTSec.Kryptonian.Application.DTOs;

namespace RTSec.Kryptonian.Api.Logging;

/// <summary>
/// In-memory ring buffer of recent gateway log entries that backs the live-log
/// panel on the admin UI's Events page. Singleton; thread-safe; lossy when the
/// buffer fills (oldest entries are dropped).
/// </summary>
public sealed class LiveLogBroadcaster
{
    private readonly int _capacity;
    private readonly LinkedList<LiveLogEntryDto> _entries = new();
    private readonly object _lock = new();
    private long _nextSeq = 1;

    public LiveLogBroadcaster(int capacity = 500)
    {
        if (capacity < 16) throw new ArgumentOutOfRangeException(nameof(capacity));
        _capacity = capacity;
    }

    public void Add(LiveLogEntryDto entry)
    {
        lock (_lock)
        {
            entry.Seq = _nextSeq++;
            _entries.AddLast(entry);
            while (_entries.Count > _capacity)
            {
                _entries.RemoveFirst();
            }
        }
    }

    public LiveLogPageDto GetSince(long sinceSeq, int limit)
    {
        lock (_lock)
        {
            var page = new LiveLogPageDto { CurrentSeq = _nextSeq - 1 };
            foreach (var entry in _entries)
            {
                if (entry.Seq <= sinceSeq) continue;
                page.Entries.Add(entry);
                if (page.Entries.Count >= limit) break;
            }
            return page;
        }
    }
}
