using System.Buffers.Binary;
using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;

namespace Sim.Tests.State;

/// <summary>
/// D-056 (2026-10-18; the Director's mandate §5: "reject corrupt data … tests for … malformed input") — A SAVE THAT NO
/// BUILD WROTE IS REFUSED AS ONE. Snapshot.Load already refused a bad magic, an unreadable version, a broken invariant, a
/// header that disagrees with its stream and a missing trailer; three corruptions escaped it — a stream cut inside its
/// tables (a bare EndOfStreamException), a negative or impossible row count (accepted, misaligning every later field, or
/// growing a table toward the end of the file), and bytes after the last field (ignored). Each is now a
/// SnapshotFormatException with a diagnostic, and a well-formed save still loads bit-exactly.
/// </summary>
public class SnapshotCorruptInputTests
{
    /// <summary>A terrain-less world with rows in the queue, progress, split and hold tables (the v33/v34 sections at the
    /// end of the stream) and in tables early in it.</summary>
    private static WorldState Populated()
    {
        var w = new WorldState(34);
        w.Regions.Add(new RegionRow(new RegionId(0)));
        w.Regions.Add(new RegionRow(new RegionId(1)));
        w.ConstructionQueue.Add(new ConstructionQueueRow(new SettlementId(0), 0, 1));
        w.ConstructionProgress.Add(new ConstructionProgressRow(new SettlementId(0), 0, 1, 2, 400_000));
        w.ResourceAllocations.Add(new ResourceAllocationRow(new PolityId(1), 30));
        w.ConstructionHolds.Add(new ConstructionHoldRow(1, 4, 6));
        w.ConstructionHolds.Add(new ConstructionHoldRow(1, 5, 11));
        return w;
    }

    private static byte[] Save(WorldState w)
    {
        using var ms = new MemoryStream();
        Snapshot.Save(w, ms);
        return ms.ToArray();
    }

    /// <summary>Snapshot header: magic 8 | version 4 | seed 8 | turn 8; then the stream: seed 8 | clock 24 | terrain flag 1
    /// (false) | the region count.</summary>
    private const int RegionCountOffset = 8 + 4 + 8 + 8 + 8 + 24 + 1;

    [Fact]
    public void AWellFormedSave_StillLoadsBitExactly()
    {
        WorldState w = Populated();
        byte[] bytes = Save(w);
        WorldState back = Snapshot.Load(new MemoryStream(bytes));
        Assert.Equal(WorldHash.ComputeHex(w), WorldHash.ComputeHex(back));
        Assert.Equal(bytes, Save(back));
        Assert.Equal(2, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(RegionCountOffset)));
    }

    [Fact]
    public void EveryTruncation_IsRefusedAsACorruptSave_NeverABareReaderException()
    {
        byte[] bytes = Save(Populated());
        for (int length = 0; length < bytes.Length; length++)
        {
            var e = Record.Exception(() => Snapshot.Load(new MemoryStream(bytes, 0, length)));
            Assert.True(e is SnapshotFormatException, $"a save cut at {length} of {bytes.Length} bytes threw {e?.GetType().Name ?? "nothing"}: {e?.Message}");
        }
        string cutInATable = Assert.Throws<SnapshotFormatException>(() => Snapshot.Load(new MemoryStream(bytes, 0, bytes.Length - 20))).Message;
        Assert.Contains("save is truncated or corrupt: its state stream cannot be read to the end (it ends inside a table)", cutInATable);
        Assert.Contains("topology trailer", Assert.Throws<SnapshotFormatException>(() => Snapshot.Load(new MemoryStream(bytes, 0, bytes.Length - 4))).Message);
    }

    [Theory]
    [InlineData(-1, "a table's row count is negative (-1)")]
    [InlineData(int.MinValue, "a table's row count is negative (-2147483648)")]
    [InlineData(int.MaxValue, "a table claims 2147483647 rows but only")]
    public void ARowCountNoWriterEmits_IsRefused(int count, string expected)
    {
        byte[] bytes = Save(Populated());
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(RegionCountOffset), count);
        string message = Assert.Throws<SnapshotFormatException>(() => Snapshot.Load(new MemoryStream(bytes))).Message;
        Assert.Contains("save is truncated or corrupt", message);
        Assert.Contains(expected, message);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    public void BytesAfterTheLastField_AreRefused(int extra)
    {
        byte[] bytes = Save(Populated());
        var longer = new byte[bytes.Length + extra];
        bytes.CopyTo(longer, 0);
        Assert.Contains("it carries bytes after its last field (the topology trailer)",
            Assert.Throws<SnapshotFormatException>(() => Snapshot.Load(new MemoryStream(longer))).Message);
    }
}
