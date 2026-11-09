using System.Buffers.Binary;
using Proto.M7.Spatial;
using Proto.M7.War.Outcomes;

namespace Proto.M7.Sandbox;

/// <summary>[D] Canonical little-endian fixed-length encoding of the sandbox rows (populated-table tests: exact length, bit-exact
/// round trip, hash equality). Table = [version u8][count i32] rows.</summary>
public static class SandboxCodec
{
    public const byte Version = 1;
    public const int HeaderBytes = 1 + 4;
    public const int EditRowBytes = 4 + 1 + 4 + 4 + 4 + 8 + 8 + 8 + 8 + 1;        // 50
    public const int CasualtyRowBytes = 8 + 4 + 7 * 8;                            // 68

    public static int ExpectedLength(int rows, int rowBytes) => checked(HeaderBytes + rows * rowBytes);

    private static byte[] Header(int length, int count)
    {
        var b = new byte[length];
        b[0] = Version;
        BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(1), count);
        return b;
    }

    private static int Check(byte[] b, int rowBytes)
    {
        if (b.Length < HeaderBytes || b[0] != Version) throw new FormatException("sandbox table header");
        int n = BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(1));
        if (n < 0 || b.Length != ExpectedLength(n, rowBytes)) throw new FormatException("sandbox table length");
        return n;
    }

    public static byte[] EncodeEdits(IReadOnlyList<SandboxEditRow> rows)
    {
        var b = Header(ExpectedLength(rows.Count, EditRowBytes), rows.Count);
        int at = HeaderBytes;
        foreach (var r in rows)
        {
            BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(at), r.Index); at += 4;
            b[at++] = r.Kind;
            BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(at), r.Side); at += 4;
            BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(at), r.FamilyKey); at += 4;
            BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(at), r.IdentityKey); at += 4;
            foreach (long v in new[] { r.FormationId, r.A, r.B, r.C }) { BinaryPrimitives.WriteInt64LittleEndian(b.AsSpan(at), v); at += 8; }
            b[at++] = r.Refusal;
        }
        if (at != b.Length) throw new InvalidOperationException("encoded length mismatch");
        return b;
    }

    public static SandboxEditRow[] DecodeEdits(byte[] b)
    {
        int n = Check(b, EditRowBytes), at = HeaderBytes;
        var rows = new SandboxEditRow[n];
        for (int i = 0; i < n; i++)
        {
            int index = BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(at)); at += 4;
            byte kind = b[at++];
            int side = BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(at)); at += 4;
            int fam = BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(at)); at += 4;
            int ident = BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(at)); at += 4;
            var v = new long[4];
            for (int k = 0; k < 4; k++) { v[k] = BinaryPrimitives.ReadInt64LittleEndian(b.AsSpan(at)); at += 8; }
            byte refusal = b[at++];
            rows[i] = new SandboxEditRow(index, kind, side, fam, ident, v[0], v[1], v[2], v[3], refusal);
        }
        return rows;
    }

    public static byte[] EncodeCasualties(IReadOnlyList<SandboxCasualtyRow> rows)
    {
        var b = Header(ExpectedLength(rows.Count, CasualtyRowBytes), rows.Count);
        int at = HeaderBytes;
        foreach (var r in rows)
        {
            BinaryPrimitives.WriteInt64LittleEndian(b.AsSpan(at), r.FormationId); at += 8;
            BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(at), r.Side); at += 4;
            foreach (long v in new[] { r.Persons, r.Fit, r.Wounded, r.RoutedReturned, r.Deserted, r.Dead, r.Captured })
            { BinaryPrimitives.WriteInt64LittleEndian(b.AsSpan(at), v); at += 8; }
        }
        if (at != b.Length) throw new InvalidOperationException("encoded length mismatch");
        return b;
    }

    public static SandboxCasualtyRow[] DecodeCasualties(byte[] b)
    {
        int n = Check(b, CasualtyRowBytes), at = HeaderBytes;
        var rows = new SandboxCasualtyRow[n];
        for (int i = 0; i < n; i++)
        {
            long id = BinaryPrimitives.ReadInt64LittleEndian(b.AsSpan(at)); at += 8;
            int side = BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(at)); at += 4;
            var v = new long[7];
            for (int k = 0; k < 7; k++) { v[k] = BinaryPrimitives.ReadInt64LittleEndian(b.AsSpan(at)); at += 8; }
            rows[i] = new SandboxCasualtyRow(id, side, v[0], v[1], v[2], v[3], v[4], v[5], v[6]);
        }
        return rows;
    }
}

/// <summary>[D] Campaign Encounter snapshots: a VALUE copy of what an encounter needs from the campaign (read-only reads).</summary>
public static class SandboxEncounters
{
    /// <summary>Reads each named campaign formation's persons from lane W's outcome world (its ledger balance; nothing written) and
    /// copies the campaign's battle terrain for the encounter.</summary>
    public static CampaignEncounterSnapshot FromOutcomeWorld(WarOutcomeWorld campaign, TerrainGrid campaignTerrain,
        (int CampaignFormation, int Side, int FamilyKey, int IdentityKey, Vec2L Anchor)[] picks)
    {
        var forces = new (int, int, int, int, long, Vec2L)[picks.Length];
        for (int i = 0; i < picks.Length; i++)
        {
            var p = picks[i];
            long persons = campaign.Ledger.Balance(campaign.FormationAccount(p.CampaignFormation));
            forces[i] = (p.CampaignFormation, p.Side, p.FamilyKey, p.IdentityKey, persons, p.Anchor);
        }
        var copy = new TerrainGrid(campaignTerrain.Width, campaignTerrain.Height, campaignTerrain.CellSizeMm, TerrainClass.Open);
        for (int y = 0; y < campaignTerrain.Height; y++)
            for (int x = 0; x < campaignTerrain.Width; x++)
                copy.Set(x, y, campaignTerrain.ClassAt(x, y), campaignTerrain.HeightAt(x, y));
        return new CampaignEncounterSnapshot(copy, forces);
    }
}
