using Sim.Core.State;

namespace Sim.Tests.State;

// Additive unit tests for the table base pattern (never a substitute for the
// packet acceptance tests — CLAUDE.md workflow rules).
public class TableTests
{
    // Test-local row type: mutable and unmanaged, so copy semantics are observable.
    private struct Counter
    {
        public long Value;
    }

    [Fact]
    public void Add_ReturnsSequentialStableIndices()
    {
        var table = new Table<Counter>();
        Assert.Equal(0, table.Add(new Counter { Value = 10 }));
        Assert.Equal(1, table.Add(new Counter { Value = 20 }));
        Assert.Equal(2, table.Count);
        Assert.Equal(10, table[0].Value);
        Assert.Equal(20, table[1].Value);
    }

    [Fact]
    public void IndexerAndRef_OutOfRange_Throw()
    {
        var table = new Table<Counter>();
        table.Add(new Counter { Value = 1 });
        Assert.Throws<ArgumentOutOfRangeException>(() => table[1]);
        Assert.Throws<ArgumentOutOfRangeException>(() => table[-1]);
        Assert.Throws<ArgumentOutOfRangeException>(() => table[1] = default);
        Assert.Throws<ArgumentOutOfRangeException>(() => table.Ref(1));
    }

    [Fact]
    public void Ref_MutatesRowInPlace()
    {
        var table = new Table<Counter>();
        table.Add(new Counter { Value = 5 });
        table.Ref(0).Value = 6;
        Assert.Equal(6, table[0].Value);
    }

    [Fact]
    public void Clone_PreservesRowsAndCount_AndIsolatesBuffers()
    {
        var table = new Table<Counter>();
        table.Add(new Counter { Value = 1 });
        table.Add(new Counter { Value = 2 });

        var clone = table.Clone();
        Assert.Equal(2, clone.Count);
        Assert.Equal(1, clone[0].Value);
        Assert.Equal(2, clone[1].Value);

        clone.Ref(0).Value = 100;
        table.Ref(1).Value = 200;
        Assert.Equal(1, table[0].Value);
        Assert.Equal(2, clone[1].Value);
    }

    [Fact]
    public void ReadOnlyView_IndexerReturnsCopy_TableUnaffected()
    {
        var table = new Table<Counter>();
        table.Add(new Counter { Value = 7 });
        IReadOnlyTable<Counter> view = table;

        var row = view[0];
        row.Value = 999; // mutates the local copy only

        Assert.Equal(7, table[0].Value);
        Assert.Equal(1, view.Count);
    }
}
