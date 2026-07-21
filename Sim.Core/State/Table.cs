namespace Sim.Core.State;

/// <summary>
/// Read-only view of a table (kernel contract §3.1). Exposes no mutation surface:
/// the indexer is get-only and returns a copy of the row (rows are structs), so
/// callers holding this interface cannot alter table state — mutation attempts do
/// not compile. Proven by the Sim.Tests.ReadOnlyViolation compile-time gate.
/// </summary>
public interface IReadOnlyTable<T> where T : unmanaged
{
    int Count { get; }
    T this[int index] { get; }
}

/// <summary>
/// Table base pattern (kernel contract §3.1): world state is plain data — a set of
/// tables of structs, each owned by exactly one system. Rows are constrained to
/// <c>unmanaged</c> so a table holds pure value data (no object references): an
/// array copy is therefore a complete deep copy (§3.2 double buffering), and rows
/// serialize canonically as raw bytes (§3.8, lands in T0.7). Iteration is by index
/// in insertion order — deterministic by construction (law 5).
/// </summary>
public sealed class Table<T> : IReadOnlyTable<T> where T : unmanaged
{
    private T[] _rows;
    private int _count;

    public Table(int capacity = 0)
    {
        if (capacity < 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        _rows = capacity == 0 ? Array.Empty<T>() : new T[capacity];
        _count = 0;
    }

    public int Count => _count;

    public T this[int index]
    {
        get
        {
            if ((uint)index >= (uint)_count) throw new ArgumentOutOfRangeException(nameof(index));
            return _rows[index];
        }
        set
        {
            if ((uint)index >= (uint)_count) throw new ArgumentOutOfRangeException(nameof(index));
            _rows[index] = value;
        }
    }

    /// <summary>Writable reference to a row, for the owning system's hot paths.</summary>
    public ref T Ref(int index)
    {
        if ((uint)index >= (uint)_count) throw new ArgumentOutOfRangeException(nameof(index));
        return ref _rows[index];
    }

    /// <summary>Appends a row and returns its index (stable for the table's lifetime).</summary>
    public int Add(in T row)
    {
        if (_count == _rows.Length)
        {
            int newCapacity = _rows.Length == 0 ? 4 : _rows.Length * 2;
            Array.Resize(ref _rows, newCapacity);
        }
        _rows[_count] = row;
        return _count++;
    }

    /// <summary>
    /// Drops all rows (capacity retained). For DERIVED-state tables only — e.g.
    /// CatchmentSystem rebuilding its own tables on a network-revision change
    /// (D-016). BOUNDARY (director ruling, T1.5): calling this on a table that
    /// carries conserved stocks (PopBands, FoodStores, Biomass, Goods, …) is a
    /// law-1 violation — the stocks would vanish without a Ledger sink and the
    /// per-turn conservation audit fails the same turn. Derived state only.
    /// </summary>
    public void Clear() => _count = 0;

    /// <summary>
    /// Full deep copy (§3.2: at turn start the kernel clones Prev → Next; simplicity
    /// beats cleverness at M0 scale). Rows are unmanaged, so the array copy shares
    /// nothing with the source.
    /// </summary>
    public Table<T> Clone()
    {
        var copy = new Table<T>(_count);
        Array.Copy(_rows, copy._rows, _count);
        copy._count = _count;
        return copy;
    }
}
