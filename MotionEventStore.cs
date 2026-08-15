using Grimlok.Models;
using System.Collections.Concurrent;

namespace Grimlok.Services;

public sealed class MotionEventStore
{
    private const int MaximumEvents = 250;
    private readonly ConcurrentQueue<SecurityEvent> _queue = new();
    private readonly ConcurrentDictionary<Guid, SecurityEvent> _dictionary = new();

    public void AddEvent(SecurityEvent securityEvent)
    {
        _dictionary[securityEvent.Id] = securityEvent;
        _queue.Enqueue(securityEvent);

        while (_queue.Count > MaximumEvents)
        {
            if (!_queue.TryDequeue(out var removed))
            {
                continue;
            }
            _dictionary.TryRemove(removed.Id, out _);
        }
    }

    public void Add(SecurityEvent securityEvent) => AddEvent(securityEvent);

    public IReadOnlyList<SecurityEvent> GetRecentEvents(int limit = 50)
    {
        return [.. _queue.Reverse().Take(limit)];
    }

    public IReadOnlyList<SecurityEvent> GetRecent(int limit = 50) => GetRecentEvents(limit);

    public SecurityEvent? GetEventById(Guid id)
    {
        return _dictionary.TryGetValue(id, out var securityEvent) ? securityEvent : null;
    }
}
