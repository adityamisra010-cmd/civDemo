namespace Proto.M7.Events;

/// <summary>
/// Explicit binary min-heap over <see cref="EventKey"/> (spec §2.3: do not rely on PriorityQueue tie semantics).
/// Keys are unique (Seq), so the extraction sequence is the sorted key sequence regardless of insertion order.
/// </summary>
public sealed class EventHeap
{
    private ScheduledEvent[] _items;
    private int _count;

    public EventHeap(int capacity = 64) => _items = new ScheduledEvent[Math.Max(4, capacity)];

    public int Count => _count;

    public void Push(in ScheduledEvent ev)
    {
        if (_count == _items.Length) Array.Resize(ref _items, _items.Length * 2);
        int i = _count++;
        while (i > 0)
        {
            int parent = (i - 1) >> 1;
            if (_items[parent].Key.CompareTo(ev.Key) <= 0) break;
            _items[i] = _items[parent];
            i = parent;
        }
        _items[i] = ev;
    }

    public bool TryPeek(out ScheduledEvent ev)
    {
        if (_count == 0) { ev = default; return false; }
        ev = _items[0];
        return true;
    }

    public ScheduledEvent Pop()
    {
        if (_count == 0) throw new InvalidOperationException("heap is empty");
        ScheduledEvent top = _items[0];
        _count--;
        if (_count > 0)
        {
            ScheduledEvent last = _items[_count];
            int i = 0;
            while (true)
            {
                int l = 2 * i + 1;
                if (l >= _count) break;
                int r = l + 1;
                int m = (r < _count && _items[r].Key.CompareTo(_items[l].Key) < 0) ? r : l;
                if (last.Key.CompareTo(_items[m].Key) <= 0) break;
                _items[i] = _items[m];
                i = m;
            }
            _items[i] = last;
        }
        _items[_count] = default;
        return top;
    }
}
