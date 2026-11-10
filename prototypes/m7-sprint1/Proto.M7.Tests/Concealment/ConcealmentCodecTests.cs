using System.Security.Cryptography;
using Proto.M7.War;

namespace Proto.M7.Tests.Concealment;

/// <summary>
/// [AP] The lane-AP serialized rows (CLAUDE.md: every new serialized row type ships a POPULATED-table test — the exact ExpectedLength,
/// a bit-exact round trip and hash equality; empty-table coverage proves nothing): <see cref="ConcealmentStateRow"/> (29 bytes),
/// <see cref="ConcealmentChange"/> (23 bytes) and <see cref="EngagementRecord"/> (46 bytes) under one versioned table.
/// </summary>
public class ConcealmentCodecTests
{
    /// <summary>A populated table from a REAL run (three agents: a hide, works, a move, an ambush attack and a camouflage give three
    /// state rows, four change rows and one engagement) plus extreme synthetic rows (every field at its type's bounds, every flag and
    /// cause combination; 11 / 8 / 2 rows in all): the encoding has
    /// exactly ExpectedLength bytes, decodes to the same rows, re-encodes bit for bit, and the SHA-256 of both encodings is equal.</summary>
    [Fact]
    public void PopulatedTable_RoundTripsBitExact_WithTheExactLength_AndEqualHashes()
    {
        long t0 = ConcealmentActionTests.W0 + Proto.M7.Tests.TestSupport.Day;
        var rig = ConcealmentActionTests.Build(new[] { (t0, 0), (t0 + ConcealmentActionTests.Hours, 1), (t0 + ConcealmentActionTests.PulseMs, 0) },
            new[] { ConcealmentActionTests.A(0, 0, 0, 22), ConcealmentActionTests.A(1, 0, 0, 12), ConcealmentActionTests.A(2, 1, 3, 40) });
        ConcealmentActionTests.To(rig, t0);
        ConcealmentActionTests.Act(rig, 0, WarKinds.ActHide, 0, 0);
        ConcealmentActionTests.Act(rig, 0, WarKinds.ActEntrench, 1, 0);
        ConcealmentActionTests.To(rig, t0 + ConcealmentActionTests.Hours);
        ConcealmentActionTests.Act(rig, 1, WarKinds.ActMove, 2, 23);
        ConcealmentActionTests.To(rig, t0 + ConcealmentActionTests.PulseMs);
        ConcealmentActionTests.Act(rig, 0, WarKinds.ActAttack, 0, 2);
        ConcealmentActionTests.Act(rig, 0, WarKinds.ActCamouflage, 1, 0);
        ConcealmentActionTests.Finish(rig, t0 + ConcealmentActionTests.PulseMs + Proto.M7.Tests.TestSupport.Day);

        var states = rig.B.StateRows().ToList();
        var changes = rig.B.ConcealmentChanges.ToList();
        var eng = rig.B.Engagements.ToList();
        Assert.Equal((3, 4, 1), (states.Count, changes.Count, eng.Count));
        Assert.Contains(states, x => x.Flags == (ConcealKind.Entrenched | ConcealKind.Camouflaged));
        Assert.True(eng[0].Ambush);
        // extreme synthetic rows: bounds and every flag / cause combination
        for (int f = 0; f < 8; f++) states.Add(new ConcealmentStateRow(int.MaxValue - f, (ConcealKind)f, long.MinValue, long.MaxValue, -1));
        foreach (ConcealEnd cause in Enum.GetValues(typeof(ConcealEnd)))
            changes.Add(new ConcealmentChange(int.MinValue, int.MaxValue, long.MaxValue, -7, ConcealKind.Hidden | ConcealKind.Entrenched, cause == ConcealEnd.None, cause));
        eng.Add(new EngagementRecord(int.MaxValue, long.MinValue, int.MinValue, int.MaxValue, 63, 0, false, (ConcealKind)7, long.MaxValue, long.MinValue));

        byte[] bytes = ConcealmentCodec.Encode(states, changes, eng);
        Assert.Equal(ConcealmentCodec.ExpectedLength(states.Count, changes.Count, eng.Count), bytes.Length);
        Assert.Equal(1 + 4 + 11 * 29 + 4 + 8 * 23 + 4 + 2 * 46, bytes.Length);                 // 11 states, 8 changes, 2 engagements, by hand
        var (s2, c2, e2) = ConcealmentCodec.Decode(bytes);
        Assert.Equal(states, s2);
        Assert.Equal(changes, c2);
        Assert.Equal(eng, e2);
        byte[] again = ConcealmentCodec.Encode(s2, c2, e2);
        Assert.Equal(bytes, again);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)), Convert.ToHexString(SHA256.HashData(again)));
        // the empty table is the header and three zero counts only
        Assert.Equal(13, ConcealmentCodec.Encode(Array.Empty<ConcealmentStateRow>(), Array.Empty<ConcealmentChange>(), Array.Empty<EngagementRecord>()).Length);
    }

    /// <summary>Malformed input is refused as a format error: wrong version, truncation, a trailing byte, a bool byte other than 0/1,
    /// and a count larger than the bytes present.</summary>
    [Fact]
    public void Decode_RefusesMalformedInput()
    {
        var states = new[] { new ConcealmentStateRow(1, ConcealKind.Hidden, 5, -1, -1) };
        var changes = new[] { new ConcealmentChange(0, 3, 5, 1, ConcealKind.Hidden, true, ConcealEnd.None) };
        var eng = new[] { new EngagementRecord(4, 9, 1, 2, 0, 1, true, ConcealKind.Hidden, 0, 22) };
        byte[] ok = ConcealmentCodec.Encode(states, changes, eng);
        Assert.Equal(states, ConcealmentCodec.Decode(ok).States);
        var badVersion = (byte[])ok.Clone(); badVersion[0] = 2;
        Assert.Throws<FormatException>(() => ConcealmentCodec.Decode(badVersion));
        Assert.Throws<FormatException>(() => ConcealmentCodec.Decode(ok[..^1]));
        Assert.Throws<FormatException>(() => ConcealmentCodec.Decode(ok.Concat(new byte[] { 0 }).ToArray()));
        var badBool = (byte[])ok.Clone(); badBool[1 + 4 + 29 + 4 + 4 + 4 + 8 + 4 + 1] = 2;   // the change row's Begin byte
        Assert.Throws<FormatException>(() => ConcealmentCodec.Decode(badBool));
        var hugeCount = (byte[])ok.Clone(); System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(hugeCount.AsSpan(1), int.MaxValue);
        Assert.Throws<FormatException>(() => ConcealmentCodec.Decode(hugeCount));
        Assert.Throws<FormatException>(() => ConcealmentCodec.Decode(Array.Empty<byte>()));
    }
}
