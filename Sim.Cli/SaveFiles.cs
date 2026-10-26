using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Worldgen;

namespace Sim.Cli;

/// <summary>
/// The CLI's one snapshot loader (`sim hash`, `sim diff`): loads a save, regenerating its terrain when the stream says
/// it had one. Terrain is not serialized (ADR-008); Snapshot.Load demands the regenerated TerrainSet and validates its
/// content hash against the one the save recorded, so a wrong size or worldgen config fails loudly rather than hashing
/// a different world. The seed, the version and the terrain flag are read from fixed header offsets BEFORE Load so that
/// a toy save (no terrain) is loaded plainly and a founded save gets exactly one worldgen.
///
/// D-054 / ADR-039 (batch-3 integration, 2026-10-14) — A SCHEMA-v32 SAVE IS FLAT. Every v32 world was a flat square
/// raster (the cylinder, WS3, arrived with v33), and a v32 file carries no topology trailer, so its terrain is
/// regenerated with the topology switch OFF (worldgen.json topology.wrapX dropped — the switch only Worldgen.Generate
/// reads); a v33 save is regenerated with the shipped worldgen and the topology its trailer records (fix round 1,
/// verifier F2). A v33 save that fails to load under its trailer's topology but loads intact with the trailer's wrap bit
/// flipped is refused as a corrupt trailer, naming the bit (<see cref="TrailerTopologyMismatch"/>; final nits, RC1-2).
///
/// D-055 (schema v34): a v34 save is regenerated exactly as a v33 one (the trailer names its topology); a v33 save loads
/// migrated (ConstructionHolds added empty) and says so on STDERR (<see cref="MigrationNote"/>).
/// </summary>
public static class SaveFiles
{
    /// <summary>Header layout (Snapshot.Save): magic 8 | version 4 | seed 8 | turn 8, then the canonical stream:
    /// seed 8 | clock 24 | terrain flag 1.</summary>
    private const int HeaderVersionOffset = 8;
    private const int HeaderSeedOffset = 8 + 4;
    private const int TerrainFlagOffset = 8 + 4 + 8 + 8 + 8 + 24;

    /// <summary>Loads the save at <paramref name="path"/>; <paramref name="sizePx"/> ≥ 0 overrides the map size the
    /// terrain is regenerated at (`--size`), −1 keeps the shipped one.</summary>
    public static WorldState Load(string path, long sizePx) => Load(path, sizePx, out _);

    /// <summary>As <see cref="Load(string, long)"/>; <paramref name="migrationNote"/> is the one-line
    /// <see cref="MigrationNote"/> for a save this build MIGRATED on load (a schema-v32 or v33 file), null for a current one.
    /// `sim hash` and `sim diff` print it to STDERR — never stdout, which stays exactly the hash / the diff.</summary>
    public static WorldState Load(string path, long sizePx, out string? migrationNote)
    {
        byte[] bytes = File.ReadAllBytes(path);
        migrationNote = bytes.Length >= HeaderVersionOffset + 4
            ? MigrationNote(path, BitConverter.ToInt32(bytes, HeaderVersionOffset)) : null;
        TerrainSet? terrain = null;
        int? trailer = null;
        Func<int, TerrainSet>? regenerate = null;
        if (bytes.Length > TerrainFlagOffset && bytes[TerrainFlagOffset] != 0)
        {
            if (!BitConverter.IsLittleEndian) throw new InvalidOperationException("big-endian host: header peek assumes little-endian saves");
            ulong seed = BitConverter.ToUInt64(bytes, HeaderSeedOffset);
            int version = BitConverter.ToInt32(bytes, HeaderVersionOffset);
            WorldgenConfig wgCfg;
            using (var wgStream = Sim.Data.DataFiles.OpenWorldgen())
                wgCfg = WorldgenConfigLoader.Load(wgStream);
            // Fix round 1 (verifier F2): a v33 file's last int32 is its topology trailer — the topology to regenerate. A
            // file whose last bytes are not a trailer (truncated, corrupt) is still refused by Snapshot.Load, which reads
            // and checks the trailer before it compares the terrain.
            trailer = TopologyFlags.RecordedFlat(version) ? null : BitConverter.ToInt32(bytes, bytes.Length - 4);
            regenerate = topology => Worldgen.Generate(WorldgenFor(version, wgCfg, sizePx, topology), seed);
            terrain = Worldgen.Generate(WorldgenFor(version, wgCfg, sizePx, trailer), seed);
        }
        using var stream = new MemoryStream(bytes, writable: false);
        try
        {
            return Snapshot.Load(stream, terrain);
        }
        catch (SnapshotFormatException) when (trailer is { } t && regenerate is not null && LoadsUnderTheOtherTopology(bytes, t, regenerate))
        {
            // Final nits (verifier RC1-2): the save is intact except for its trailer's wrap bit — say so, naming the bit.
            throw new SnapshotFormatException(TrailerTopologyMismatch(t));
        }
    }

    /// <summary>
    /// Final nits (verifier RC1-2, 2026-10-15) — WHY A v33 LOAD FAILED, WHEN THE TRAILER'S WRAP BIT IS THE ONLY FAULT.
    /// Since fix round 1 the terrain is regenerated with the topology the trailer names, so a cylinder save whose bit 0
    /// was flipped 1→0 was regenerated FLAT and refused with Snapshot.Load's generic "terrain mismatch: … wrong seed or
    /// worldgen config." (MEASURED with `sim hash`; before fix round 1, which regenerated every v33 save as the shipped
    /// cylinder, the same file got Snapshot.Load's "topology mismatch: …"). This re-checks a failed load: true only when
    /// <paramref name="bytes"/> with bit 0 of its <paramref name="trailer"/> flipped back LOADS INTACT against the terrain
    /// regenerated under that other topology — every other check Snapshot.Load makes passing. Any other failure (a wrong
    /// `--size` or seed, a truncated file, unknown bits, a size the other topology refuses) answers false, and the
    /// original refusal stands. Diagnostic only: the save is refused either way.
    /// </summary>
    internal static bool LoadsUnderTheOtherTopology(byte[] bytes, int trailer, Func<int, TerrainSet> regenerate)
    {
        if ((trailer & ~TopologyFlags.Known) != 0 || bytes.Length < 4) return false;   // unknown bits: Snapshot.Load's refusal
        int other = trailer ^ TopologyFlags.WrapX;
        byte[] repaired = (byte[])bytes.Clone();
        BitConverter.TryWriteBytes(repaired.AsSpan(repaired.Length - 4), other);
        try
        {
            using var s = new MemoryStream(repaired, writable: false);
            Snapshot.Load(s, regenerate(other));
            return true;
        }
        catch (Exception)
        {
            // Not explained by the other topology (or that topology cannot take this size): the first refusal stands.
            return false;
        }
    }

    /// <summary>Final nits (verifier RC1-2) — the refusal for a v33 save whose topology trailer names
    /// <paramref name="recorded"/> while the save loads intact only with bit 0 (<see cref="TopologyFlags.WrapX"/>) flipped:
    /// the trailer is corrupt. Names the bit, both topologies and the remedy. Final-round fix 3 (verifier
    /// RC1-2-REMEDY-WORDING, 2026-10-15): the remedy that acts on the refused file — the bit — comes first, and saving
    /// again is scoped to the session that made the save (was "… bit 0 again, or save the world again, to load it.", which
    /// a user holding only the file cannot do).</summary>
    public static string TrailerTopologyMismatch(int recorded)
    {
        bool wraps = (recorded & TopologyFlags.WrapX) != 0;
        return "topology mismatch: the save's topology trailer (its last 4 bytes) has bit 0 (east-west wrap) "
            + (wraps ? "SET, naming an east-west WRAPPING world" : "CLEAR, naming a FLAT world")
            + ", but the save was made on " + (wraps ? "a FLAT world" : "an east-west WRAPPING world")
            + " — with bit 0 " + (wraps ? "clear" : "set") + " it loads intact against that terrain. The trailer is "
            + "corrupt, so the save is refused; " + (wraps ? "clear" : "set") + " bit 0 again to load it, or save the world "
            + "again from the session that made it.";
    }

    /// <summary>
    /// D-054 (batch-3 integration, verifier F10(d)); D-055 (schema v34, X1 (g)) — WHAT THE CLI SAYS WHEN IT MIGRATED A
    /// SAVE: one line for a header <paramref name="version"/> this build loads MIGRATED — an older member of the readable
    /// set (CanonicalSchema.IsReadable: 32 or 33) — naming the projection the load applied (CanonicalSchema.Read's exact
    /// migration: the tables appended since that schema added EMPTY; for v32 also the terrain regenerated FLAT) and that
    /// the hash printed is therefore this build's (v34) hash, not the one the recording build printed for the same file
    /// (that is the v34 world with those empty tables stripped: the test suite's SchemaProjection.V32 / V33). Null for a
    /// current save, and for any version this build does not load (Snapshot.Load refuses it, and the CLI prints that
    /// refusal instead). After "note: &lt;path&gt;: ", the v33 text is X1 (g)'s verbatim (its code formatting dropped); the v32
    /// text is X1 (g)'s followed by one more sentence kept from batch 3's note (verifier F10(d), pinned by
    /// CliSaveAndSizeMessagesTests): the terrain regenerated flat and the queued entries at progress 0, ordered as enqueued on
    /// turn −1 — a recorded departure from X1 (g) (D-055 fix round, finding T1a-L2-V2-03).
    /// </summary>
    public static string? MigrationNote(string path, int version)
    {
        if (!CanonicalSchema.IsReadable(version) || version == CanonicalSchema.Version) return null;
        string current = CanonicalSchema.Version.ToString(System.Globalization.CultureInfo.InvariantCulture);
        // ADR-043 (v35): CivicsPoints joins every older version's list of tables added empty; ADR-044 (v35): so do the four
        // M6 EX discovery tables.
        const string v35Tables = " M6 discovery tables (KnownExtents, Explorations, SettlementSightings, SettlementSurveys)";
        // ADR-049 (v36): ResearchLevels joins every older version's list of tables added empty.
        const string v36Table = " ResearchLevels (no repeatable research level existed before v36) was added empty too.";
        // ADR-051 (v37): ResearchReserves joins every older version's list of tables added empty.
        const string v37Table = " ResearchReserves (no research reserve existed before v37) was added empty too.";
        return version switch
        {
            32 => "note: " + path + ": migrated from save schema v32: the four tables added since v32 (ConstructionProgress,"
                  + " ResourceAllocations, ConstructionHolds, CivicsPoints) and the four" + v35Tables + " were added empty; the"
                  + " hash printed is the v" + current + " hash (SchemaProjection.V32 gives the v32 build's). Its terrain was"
                  + " regenerated flat (no v32 world was a cylinder) and its queued entries are at progress 0, ordered as enqueued"
                  + " on turn -1." + v36Table + v37Table,
            33 => "note: " + path + ": migrated from save schema v33: ConstructionHolds, CivicsPoints and the four" + v35Tables
                  + " were added empty; the hash printed is the v" + current + " hash (SchemaProjection.V33 gives the v33 build's)."
                  + v36Table + v37Table,
            34 => "note: " + path + ": migrated from save schema v34: CivicsPoints (every Civics Points bank starts at zero) and"
                  + " the four" + v35Tables + " were added empty; the first step re-derives each polity's seed extent; the hash"
                  + " printed is the v" + current + " hash (SchemaProjection.V34 gives the v34 build's)." + v36Table + v37Table,
            35 => "note: " + path + ": migrated from save schema v35: ResearchLevels was added empty (no repeatable research level"
                  + " existed before v36; a repeatable completed under v35 reads level 1); the hash printed is the v" + current
                  + " hash (SchemaProjection.V35 gives the v35 build's)." + v37Table,
            _ => "note: " + path + ": migrated from save schema v36: ResearchReserves was added empty (no research reserve"
                  + " existed before v37); the hash printed is the v" + current + " hash (SchemaProjection.V36 gives the v36 build's).",
        };
    }

    /// <summary>The worldgen a save of header version <paramref name="version"/> is regenerated with: the shipped
    /// <paramref name="shipped"/> config (with the `--size` override), and its topology the one the save records — for a
    /// save from before v33 (<see cref="TopologyFlags.RecordedFlat"/>: schema v32, which has no trailer) the switch off, a
    /// v32 world is flat; for a v33 save the east-west wrap its <paramref name="topology"/> trailer names (bit 0,
    /// <see cref="TopologyFlags.WrapX"/>; fix round 1, verifier F2 — before, every v33 save was regenerated as shipped, so a
    /// flat v33 save was refused as a terrain mismatch). With no trailer given, the shipped topology.</summary>
    public static WorldgenConfig WorldgenFor(int version, WorldgenConfig shipped, long sizePx, int? topology = null)
    {
        WorldgenConfig cfg = sizePx >= 0 ? shipped with { SizePx = (int)sizePx } : shipped;
        if (TopologyFlags.RecordedFlat(version)) return cfg with { Topology = null };
        return topology is { } t ? cfg with { Topology = new TopologyConfig((t & TopologyFlags.WrapX) != 0) } : cfg;
    }
}
