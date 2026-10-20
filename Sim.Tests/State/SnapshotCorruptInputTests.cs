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

    // ================================================================== D-057: malformed fields

    /// <summary>The offset of the one byte where <paramref name="a"/> and <paramref name="b"/> (equal length) differ.</summary>
    private static int OnlyDifference(byte[] a, byte[] b)
    {
        Assert.Equal(a.Length, b.Length);
        int at = -1;
        for (int i = 0; i < a.Length; i++)
            if (a[i] != b[i]) { Assert.Equal(-1, at); at = i; }
        Assert.True(at >= 0, "the two saves are identical");
        return at;
    }

    private static WorldState WithAgeEligibility(bool eligible)
    {
        WorldState w = Populated();
        w.AgeEligibility.Add(new AgeEligibilityRow(new PolityId(1), 2, 5, 1, 1, 2, 2, 3, 2, eligible));
        return w;
    }

    private static WorldState WithPolity(CommandSource source)
    {
        WorldState w = Populated();
        w.Polities.Add(new PolityRow(new PolityId(1), source));
        return w;
    }

    /// <summary>D-057: a boolean field holds the byte a writer writes — 0 or 1. BinaryReader.ReadBoolean took any non-zero
    /// byte as true, so a save with a 2 there loaded, and re-saved as another file. The terrain flag (the stream's first
    /// boolean) and a row's boolean (AgeEligibility.Eligible) are refused for every byte 2..255, and 0 and 1 load
    /// bit-exactly.</summary>
    [Fact]
    public void ABooleanFieldHoldingAnyByteButZeroOrOne_IsRefused()
    {
        byte[] no = Save(WithAgeEligibility(false)), yes = Save(WithAgeEligibility(true));
        int row = OnlyDifference(no, yes);
        Assert.Equal((0, 1), (no[row], yes[row]));
        foreach (byte[] ok in new[] { no, yes }) Assert.Equal(ok, Save(Snapshot.Load(new MemoryStream(ok))));
        const int terrainFlag = RegionCountOffset - 1;
        Assert.Equal(0, no[terrainFlag]);
        foreach (int at in new[] { terrainFlag, row })
            for (int b = 2; b <= 255; b++)
            {
                byte[] bad = (byte[])no.Clone();
                bad[at] = (byte)b;
                string message = Assert.Throws<SnapshotFormatException>(() => Snapshot.Load(new MemoryStream(bad))).Message;
                Assert.Contains("save is truncated or corrupt", message);
                Assert.Contains($"a boolean field holds the byte {b}; a writer writes only 0 or 1", message);
            }
    }

    /// <summary>D-057: a polity's command source is one CommandSource defines (AI 0, player 1); any other value was cast and
    /// loaded as a polity no one commands.</summary>
    [Theory]
    [InlineData(2)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public void APolityCommandSourceNoEnumValueNames_IsRefused(int value)
    {
        byte[] ai = Save(WithPolity(CommandSource.Ai)), player = Save(WithPolity(CommandSource.Player));
        int at = OnlyDifference(ai, player);   // the low byte of the source's int32
        foreach (byte[] ok in new[] { ai, player }) Assert.Equal(ok, Save(Snapshot.Load(new MemoryStream(ok))));
        byte[] bad = (byte[])ai.Clone();
        BinaryPrimitives.WriteInt32LittleEndian(bad.AsSpan(at), value);
        string message = Assert.Throws<SnapshotFormatException>(() => Snapshot.Load(new MemoryStream(bad))).Message;
        Assert.Contains($"a polity's command source is {value}; only 0 (AI) and 1 (player) exist", message);
    }
}
