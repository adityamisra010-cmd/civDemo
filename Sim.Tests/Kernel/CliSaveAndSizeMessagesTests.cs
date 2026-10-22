using System.Runtime.InteropServices;
using Sim.Cli;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Worldgen;
using Sim.Tests.State;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Kernel;

/// <summary>
/// Batch-3 integration (D-054, 2026-10-14) — WHAT THE CLI TELLS A USER ABOUT A SAVE IT MIGRATED AND A SIZE A CYLINDER
/// REFUSES, run as the real executable (TestUtil.CliProcess) so stdout and stderr are seen exactly as a script sees them.
///
/// (1) Verifier 2 F10(d): `sim hash` / `sim diff` of a schema-v32 save load it MIGRATED (terrain regenerated flat, the
/// two v33 tables added empty) and therefore print the v33 hash — MEASURED for the real v32 fixture: 3eb39203… where the
/// v32 build printed b000b2ee… — with nothing saying so. They now print one line on STDERR naming the projection
/// (Sim.Cli.SaveFiles.MigrationNote); stdout stays exactly the hash / the diff a script parses, and a current (v33) save
/// gets no note.
///
/// (2) Verifier 0 F9: a `--size` the cylinder cannot take is refused (exit 2, Cli.Guard's "error: …" on stderr) with
/// a remedy the CLI user can apply — the nearest sizes the stride rule takes — never "set topology.wrapX false"
/// (worldgen.json is an embedded resource). The CLI and the UI launchers print the SAME message: both report the
/// WorldgenConfigException Worldgen.Generate throws (WorldgenConfigLoader.ValidateTopology); Sim.Ui.Tests'
/// CylinderLaunchRefusalTests pins the UI side.
/// </summary>
public class CliSaveAndSizeMessagesTests
{
    private static readonly TimeSpan Limit = TimeSpan.FromMinutes(4);

    private static string FixturePath() => Path.Combine(AppContext.BaseDirectory, "Fixtures", "v32-seed42-turn40.civsnap");

    /// <summary>D-055 (2026-10-16) CAUSE BLOCK (X1 (f) and (g)): re-pinned for schema v34. Was: one note, for a v32 header,
    /// "note: x.civsnap is a schema-v32 save, migrated to v33 on load (D-054): its terrain regenerated flat … the two v33
    /// tables … The hash printed is the v33 hash …". Now the readable set is {32, 33, 34}, so BOTH older members get a note,
    /// each carrying X1 (g)'s exact text (code formatting dropped); the v32 note keeps the flat-terrain fact batch 3's
    /// verifier F10(d) asked for. Absent for the current version and for every version outside the set.</summary>
    [Fact]
    public void TheMigrationNote_IsOneLine_ForAV32OrV33Header_NamingTheProjection_AndAbsentForACurrentSave()
    {
        string? note = SaveFiles.MigrationNote("x.civsnap", 32);
        Assert.NotNull(note);
        Assert.DoesNotContain('\n', note);
        // ADR-043 (2026-10-22) RE-PIN of the texts: schema v35 appends CivicsPoints, so each older header names it too and
        // the hash printed is the v35 hash. Was (v34): "the three tables added since v32 (ConstructionProgress,
        // ResourceAllocations, ConstructionHolds) … the v34 hash …" and "v33: ConstructionHolds was added empty; … the v34 hash".
        Assert.Equal("note: x.civsnap: migrated from save schema v32: the four tables added since v32 (ConstructionProgress, "
            + "ResourceAllocations, ConstructionHolds, CivicsPoints) were added empty; the hash printed is the v35 hash (SchemaProjection.V32 "
            + "gives the v32 build's). Its terrain was regenerated flat (no v32 world was a cylinder) and its queued entries are "
            + "at progress 0, ordered as enqueued on turn -1.", note);
        string? v33 = SaveFiles.MigrationNote("x.civsnap", 33);
        Assert.Equal("note: x.civsnap: migrated from save schema v33: ConstructionHolds and CivicsPoints were added empty; the hash printed is "
            + "the v35 hash (SchemaProjection.V33 gives the v33 build's).", v33);
        Assert.Equal("note: x.civsnap: migrated from save schema v34: CivicsPoints was added empty (every Civics Points bank starts "
            + "at zero); the hash printed is the v35 hash (SchemaProjection.V34 gives the v34 build's).", SaveFiles.MigrationNote("x.civsnap", 34));
        Assert.Null(SaveFiles.MigrationNote("x.civsnap", CanonicalSchema.Version));
        foreach (int refused in new[] { 31, 36, 0, 27 })
            Assert.Null(SaveFiles.MigrationNote("x.civsnap", refused));                     // refused, not migrated
        Assert.Equal(35, CanonicalSchema.Version);   // the notes' texts name v34: a schema bump must revisit them (v35: ADR-043 CivicsPoints — 2026-10-22 cause: the schema bump alone)
    }

    [Fact]
    public void SimHash_OfAV32Save_PrintsTheV33HashAloneOnStdout_AndTheMigrationNoteOnStderr()
    {
        string fixture = FixturePath();
        WorldState loaded = SaveFiles.Load(fixture, -1, out string? note);
        Assert.NotNull(note);
        string v33 = WorldHash.ComputeHex(loaded);   // D-055: this build's (v34) hash; the local's name is batch 3's
        // What the note claims: the v32 build's hash is this world with the tables appended since v32 (two v33, one v34;
        // all empty) stripped.
        Assert.Equal(ConstructionSchemaV33Tests.V32FixtureHash, SchemaProjection.V32(loaded));
        Assert.NotEqual(ConstructionSchemaV33Tests.V32FixtureHash, v33);

        (int exit, string stdout, string stderr) = CliProcess.Run(Limit, "hash", fixture);

        Assert.Equal(0, exit);
        Assert.Equal(new[] { v33 }, CliProcess.Lines(stdout));                // stdout is exactly the hash
        Assert.Equal(new[] { note }, CliProcess.Lines(stderr));                // the note, once, on stderr only
    }

    [Fact]
    public void SimDiff_OfAV32Save_KeepsItsStdoutAndExitCode_AndNotesEachMigratedFileOnStderr()
    {
        string fixture = FixturePath();
        (int exit, string stdout, string stderr) = CliProcess.Run(Limit, "diff", fixture, fixture);

        Assert.Equal(0, exit);                                         // identical
        Assert.Contains("identical", stdout, StringComparison.Ordinal);
        Assert.DoesNotContain("note:", stdout, StringComparison.Ordinal);
        string note = SaveFiles.MigrationNote(fixture, CanonicalSchema.OldestReadableVersion)!;
        Assert.Equal(new[] { note, note }, CliProcess.Lines(stderr));         // A, then B
    }

    /// <summary>D-055 fix round (2026-10-16; finding T1a-L4-V4-N1): the usage text states the v34 migration rules — the readable
    /// set {32, 33, 34} (CanonicalSchema.IsReadable) migrates BOTH older schemas, each with its one stderr line: v32 with its
    /// terrain flat and the three tables added since v32 empty, v33 with ConstructionHolds empty (the old text named v32
    /// alone and "the two v33 tables").</summary>
    [Fact]
    public void TheUsage_SaysV32AndV33SavesAreMigrated_AndWhatEachGetsEmpty()
    {
        (int exit, string stdout, string stderr) = CliProcess.Run(Limit);
        Assert.Equal((1, ""), (exit, stdout));
        string usage = string.Join(" ", CliProcess.Lines(stderr).Select(l => l.Trim()));
        // ADR-043 (2026-10-22) RE-PIN of the usage text: v34 saves migrate too, and CivicsPoints is added empty to each.
        Assert.Contains("A schema-v32, -v33 or -v34 save is migrated on load and says so in one line on stderr; stdout is"
            + " unchanged in shape. v32: its terrain is regenerated flat and the four tables added since v32"
            + " (ConstructionProgress, ResourceAllocations, ConstructionHolds, CivicsPoints) are added empty. v33:"
            + " ConstructionHolds and CivicsPoints are added empty. v34: CivicsPoints is added empty.", usage);
        Assert.DoesNotContain("the two v33 tables", usage);
    }

    [Fact]
    public void SimHash_OfACurrentSave_PrintsNoNote()
    {
        string dir = Path.Combine(Path.GetTempPath(), "civsim-clinote-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            WorldState world = WorldFounding.Found(TestConfigs.DevWorldgen(), TestConfigs.Sim(), 42);
            string path = Path.Combine(dir, "v33.civsnap");
            using (FileStream f = File.Create(path)) Snapshot.Save(world, f);

            (int exit, string stdout, string stderr) = CliProcess.Run(Limit, "hash", path, "--size", "256");

            Assert.Equal(0, exit);
            Assert.Equal(new[] { WorldHash.ComputeHex(world) }, CliProcess.Lines(stdout));
            Assert.Equal("", stderr);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>The refusal Worldgen.Generate gives for <paramref name="size"/> under the shipped (wrapped) worldgen.json
    /// — the message the UI launchers report too.</summary>
    private static string ShippedRefusal(int size) => Assert.Throws<WorldgenConfigException>(
        () => Sim.Core.Worldgen.Worldgen.Generate(TestConfigs.Worldgen() with { SizePx = size }, 42)).Message;

    /// <summary>Final nits (verifier RC1-4, 2026-10-15): the size refusal's sentences after its first. Before, the second
    /// sentence was the imperative "Use a size that is a multiple of 4 px and at least 12 px — …", a remedy founding then
    /// refuses at 12 px (MEASURED: `sim run --founded --seed 42 --turns 1 --size 12` exits 2 with "error: settlement siting
    /// could only place 1 of 12 sites at minSpacingKm 95.2 — terrain too small or spacing too large."; at the default
    /// 12 settlements siting refused 12, 16, 20, 24, 32, 48 and 64 px on seeds 42, 1 and 7, and 96 px on seeds 42 and 1;
    /// the same command founded at 128 px on all three). Now the floor is the cylinder's rule.
    ///
    /// Final-round fix 3 (verifier RC1-4-FLOOR-NOT-NAMED, 2026-10-15): the founding sentence used to say "at the default
    /// settlement count a map far larger than 12 px is needed (settlement siting refuses a smaller one with its own
    /// message)" — no founding figure, although the records said it named one — and it was added to EVERY refusal,
    /// 250 px (nearest 248 and 252 px, both of which found) included. MEASURED before the fix: `sim run --founded --seed
    /// 42 --turns 1 --size 250` and `sim worldgen --seed 42 --size 250` (nothing founded) both ended with it. It now
    /// claims only that siting CAN refuse maps this small, names no figure, and is added only when a size the message
    /// names is below the size founding was measured to succeed from (124 px: re-measured, every size from 124 to 160 px
    /// founded on seeds 1–20 and 42; seed 42 was refused at 120 px).</summary>
    private const string CylinderRule = "The cylinder's own rule is a multiple of 4 px, at least 12 px — ";

    private const string FoundingSentence = "That rule is the cylinder's alone: founding also needs room to site every "
        + "settlement, and at the default settlement count, settlement siting can refuse maps this small, with its own "
        + "message.";

    /// <summary>
    /// The refusal names the nearest sizes THE CYLINDER TAKES, and only claims that. Fix round 1 (verifier VR-1,
    /// 2026-10-15): the message said "the nearest is 12 px" for 8 px (and "12 px or 16 px" for 13 px) and this theory
    /// called them "usable" without running them. MEASURED: `sim run --founded --seed 42 --turns 1 --size 12` exits 2
    /// with "error: settlement siting could only place 1 of 12 sites at minSpacingKm 95.2 — terrain too small or spacing
    /// too large." (16 px: 2 of 12) — the cylinder takes the size and founding refuses it for its own, named reason. The
    /// wording now scopes the claim to the cylinder ("the nearest sizes the cylinder takes are …"), and every size the
    /// message names is run with the same command: none is refused by the cylinder again; the realistic ones found and run
    /// (TheNamedRemedy_Works…), the small ones are refused by siting with siting's own message.
    ///
    /// Final-round fix 3 (verifier RC1-4-FLOOR-NOT-NAMED): <paramref name="foundingSentence"/> — whether the message
    /// carries <see cref="FoundingSentence"/> — is checked against what founding DOES at the named sizes on seed 42: the
    /// sentence is there exactly when siting refuses at least one of them, so its "can refuse maps this small" is shown,
    /// and a refusal without it names only sizes that found. 122 px (nearest 120 and 124 px) and 126 px (124 and 128 px)
    /// pin the measured threshold from both sides.
    /// </summary>
    [Theory]
    [InlineData(250, "the nearest sizes the cylinder takes are 248 px and 252 px.", new[] { 248, 252 }, false)]
    [InlineData(254, "the nearest sizes the cylinder takes are 252 px and 256 px.", new[] { 252, 256 }, false)]
    [InlineData(126, "the nearest sizes the cylinder takes are 124 px and 128 px.", new[] { 124, 128 }, false)]
    [InlineData(122, "the nearest sizes the cylinder takes are 120 px and 124 px.", new[] { 120, 124 }, true)]
    [InlineData(13, "the nearest sizes the cylinder takes are 12 px and 16 px.", new[] { 12, 16 }, true)]
    [InlineData(8, "the nearest size the cylinder takes is 12 px.", new[] { 12 }, true)]   // below the 3-node floor: only the floor
    public void SimRun_AtASizeTheCylinderRefuses_Exits2_WithTheSameRemedyTheUiShows_TheNearestSizesTheCylinderTakes(
        int size, string nearest, int[] named, bool foundingSentence)
    {
        string refusal = ShippedRefusal(size);
        // Final nits (verifier RC1-4): the floor is worded as the cylinder's own rule — no imperative that recommends a
        // size founding refuses. Final-round fix 3: the founding sentence closes the message only where it applies.
        Assert.DoesNotContain("Use a size", refusal, StringComparison.Ordinal);
        if (foundingSentence)
            Assert.EndsWith(CylinderRule + nearest + " " + FoundingSentence, refusal, StringComparison.Ordinal);
        else
        {
            Assert.EndsWith(CylinderRule + nearest, refusal, StringComparison.Ordinal);
            Assert.DoesNotContain("founding", refusal, StringComparison.Ordinal);
        }
        Assert.Contains("size " + size.ToString(System.Globalization.CultureInfo.InvariantCulture) + " px", refusal, StringComparison.Ordinal);
        Assert.DoesNotContain("set topology.wrapX", refusal, StringComparison.Ordinal);   // not the CLI user's to set

        (int exit, string stdout, string stderr) = CliProcess.Run(Limit,
            "run", "--founded", "--seed", "42", "--turns", "1", "--size", size.ToString(System.Globalization.CultureInfo.InvariantCulture));

        Assert.Equal(2, exit);
        Assert.Equal(new[] { "error: " + refusal }, CliProcess.Lines(stderr));
        Assert.DoesNotContain("run complete", stdout, StringComparison.Ordinal);

        // Every size the message names, run with the same command: the cylinder takes each one; siting founds it or
        // refuses it with its own message, and the founding sentence is there exactly when siting refuses one.
        bool sitingRefusedOne = false;
        foreach (int n in named)
        {
            WorldgenConfigLoader.ValidateTopology(TestConfigs.Worldgen() with { SizePx = n });   // does not throw
            (int e, _, string err) = CliProcess.Run(Limit,
                "run", "--founded", "--seed", "42", "--turns", "1", "--size", n.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Assert.DoesNotContain("this world is a cylinder", err, StringComparison.Ordinal);
            if (e == 0)
                Assert.Equal("", err);                                     // founds and runs
            else
            {
                Assert.Equal(2, e);
                Assert.StartsWith("error: settlement siting could only place ", err, StringComparison.Ordinal);   // siting's own refusal
                sitingRefusedOne = true;
            }
            // MEASURED on seed 42: every size from 124 px founds, and every named size below it is refused by siting.
            Assert.Equal(n >= 124, e == 0);
        }
        Assert.Equal(foundingSentence, sitingRefusedOne);
    }

    /// <summary>Final-round fix 3 (verifier RC1-4-FLOOR-NOT-NAMED, 2026-10-15): the size refusal is shared with a command
    /// that founds nothing — `sim worldgen --size` reaches the same <c>ValidateTopology</c>. At 250 px it printed the
    /// founding sentence before the fix (MEASURED: "… the nearest sizes the cylinder takes are 248 px and 252 px. That
    /// rule is the cylinder's alone: founding also needs room to site every settlement, and at the default settlement
    /// count a map far larger than 12 px is needed (settlement siting refuses a smaller one with its own message).",
    /// exit 2). Now it ends at the nearest sizes; at 18 px (nearest 16 and 20 px; `sim worldgen` takes no size below
    /// 16) it carries the sentence, which states a fact about founding at that size, not about the command.</summary>
    [Theory]
    [InlineData(250, "the nearest sizes the cylinder takes are 248 px and 252 px.", false)]
    [InlineData(18, "the nearest sizes the cylinder takes are 16 px and 20 px.", true)]
    public void SimWorldgen_AtASizeTheCylinderRefuses_Exits2_WithTheFoundingSentenceOnlyBelowTheMeasuredSize(
        int size, string nearest, bool foundingSentence)
    {
        string refusal = ShippedRefusal(size);
        Assert.EndsWith(CylinderRule + nearest + (foundingSentence ? " " + FoundingSentence : ""), refusal, StringComparison.Ordinal);
        (int exit, string stdout, string stderr) = CliProcess.Run(Limit,
            "worldgen", "--seed", "42", "--size", size.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(2, exit);
        Assert.Equal("", stdout);
        Assert.Equal(new[] { "error: " + refusal }, CliProcess.Lines(stderr));
    }

    /// <summary>
    /// Fix round 1 (verifier F2, 2026-10-15) — A FLAT SCHEMA-v33 SAVE. The loader regenerated every v33 save with the
    /// shipped worldgen (the cylinder) and never read the save's topology trailer, so a flat v33 save (a control build's,
    /// or any world founded with the topology switch off) was refused with "terrain mismatch: the provided TerrainSet's
    /// content hash does not equal the hash this save was made against — wrong seed or worldgen config." — MEASURED with
    /// this test before the fix. The trailer (Snapshot's int32 after the stream; bit 0 = wraps east-west) now names the
    /// topology the terrain is regenerated with, as the v32 rule already did for v32 files: the flat save loads flat, and
    /// `sim hash` prints its hash. A cylinder save still regenerates the cylinder (SimHash_OfACurrentSave_PrintsNoNote).
    /// </summary>
    [Fact]
    public void AFlatV33Save_IsRegeneratedWithTheTopologyItsTrailerNames_AndSimHashPrintsItsHash()
    {
        string dir = Path.Combine(Path.GetTempPath(), "civsim-flatv33-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            WorldState world = WorldFounding.Found(TestConfigs.PreCylinderWorldgen(TestConfigs.DevWorldgen()), TestConfigs.Sim(), 42);
            Assert.False(world.Terrain!.WrapX);
            string path = Path.Combine(dir, "flat-v33.civsnap");
            using (FileStream f = File.Create(path)) Snapshot.Save(world, f);
            byte[] bytes = File.ReadAllBytes(path);
            Assert.Equal(CanonicalSchema.Version, BitConverter.ToInt32(bytes, 8));
            Assert.Equal(0, BitConverter.ToInt32(bytes, bytes.Length - 4));             // the trailer: flat

            WorldState loaded = SaveFiles.Load(path, 256, out string? note);
            Assert.Null(note);                                                         // a current save: nothing migrated
            Assert.False(loaded.Terrain!.WrapX);
            Assert.Equal(WorldHash.ComputeHex(world), WorldHash.ComputeHex(loaded));

            (int exit, string stdout, string stderr) = CliProcess.Run(Limit, "hash", path, "--size", "256");
            Assert.Equal((0, ""), (exit, stderr));
            Assert.Equal(new[] { WorldHash.ComputeHex(world) }, CliProcess.Lines(stdout));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>A schema-v33 save of a founded 256-px seed-42 world (cylinder unless <paramref name="flat"/>) at
    /// <paramref name="path"/>, its topology trailer XORed with <paramref name="flipTrailer"/>; returns the world.</summary>
    private static WorldState SaveV33(string path, bool flat, int flipTrailer)
    {
        WorldgenConfig wg = flat ? TestConfigs.PreCylinderWorldgen(TestConfigs.DevWorldgen()) : TestConfigs.DevWorldgen();
        WorldState world = WorldFounding.Found(wg, TestConfigs.Sim(), 42);
        Assert.Equal(!flat, world.Terrain!.WrapX);
        using (var m = new MemoryStream())
        {
            Snapshot.Save(world, m);
            byte[] bytes = m.ToArray();
            int trailer = BitConverter.ToInt32(bytes, bytes.Length - 4);
            Assert.Equal(flat ? 0 : TopologyFlags.WrapX, trailer);
            BitConverter.TryWriteBytes(bytes.AsSpan(bytes.Length - 4), trailer ^ flipTrailer);
            File.WriteAllBytes(path, bytes);
        }
        return world;
    }

    /// <summary>
    /// Final nits (verifier RC1-2, 2026-10-15) — A SAVE WHOSE TOPOLOGY TRAILER BIT WAS FLIPPED IS REFUSED AS A TOPOLOGY
    /// MISMATCH, NAMING THE BIT. Since fix round 1 the loader regenerates the terrain the trailer names, so a cylinder
    /// save whose bit 0 was flipped 1→0 was regenerated FLAT and refused with the generic "error: terrain mismatch: the
    /// provided TerrainSet's content hash does not equal the hash this save was made against — wrong seed or worldgen
    /// config." — MEASURED with `sim hash` on this test's flipped copy before the fix (b3-final printed the actionable
    /// "topology mismatch: …" here, because it regenerated every v33 save as the shipped cylinder). Now, when the load
    /// fails under the trailer's topology, the loader re-checks the save under the OTHER topology with bit 0 restored, and
    /// only if the save then loads intact does it refuse with <see cref="SaveFiles.TrailerTopologyMismatch"/>. Nothing
    /// loads that was refused before: the flipped save is still refused, exit 2.
    ///
    /// Final-round fix 3 (verifier RC1-2-REMEDY-WORDING, 2026-10-15): the whole message is pinned, remedy included. It
    /// ended "… set bit 0 again, or save the world again, to load it." (MEASURED with `sim hash` on a flipped copy of a
    /// `sim run --founded --seed 42 --turns 2 --save-at 1` save), offering the `sim hash` / `sim diff` user — who holds
    /// only the file — a remedy that needs the session that made it. It now puts the remedy that acts on the file first
    /// and scopes the other to that session.
    /// </summary>
    [Theory]
    [InlineData(false,    // a cylinder save, bit 0 flipped 1 → 0
        "topology mismatch: the save's topology trailer (its last 4 bytes) has bit 0 (east-west wrap) CLEAR, naming a "
        + "FLAT world, but the save was made on an east-west WRAPPING world — with bit 0 set it loads intact against "
        + "that terrain. The trailer is corrupt, so the save is refused; set bit 0 again to load it, or save the world "
        + "again from the session that made it.")]
    [InlineData(true,     // a flat v33 save, bit 0 flipped 0 → 1
        "topology mismatch: the save's topology trailer (its last 4 bytes) has bit 0 (east-west wrap) SET, naming an "
        + "east-west WRAPPING world, but the save was made on a FLAT world — with bit 0 clear it loads intact against "
        + "that terrain. The trailer is corrupt, so the save is refused; clear bit 0 again to load it, or save the world "
        + "again from the session that made it.")]
    public void SimHash_OfASaveWhoseTrailerBitWasFlipped_IsRefusedAsATopologyMismatch_NamingTheBit(bool flat, string message)
    {
        string dir = Path.Combine(Path.GetTempPath(), "civsim-flipped-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string path = Path.Combine(dir, "flipped.civsnap");
            SaveV33(path, flat, flipTrailer: TopologyFlags.WrapX);
            int recorded = flat ? TopologyFlags.WrapX : 0;   // what the corrupt trailer now says
            string expected = SaveFiles.TrailerTopologyMismatch(recorded);
            Assert.StartsWith("topology mismatch: the save's topology trailer (its last 4 bytes) has bit 0 (east-west wrap) "
                + (flat ? "SET, naming an east-west WRAPPING world" : "CLEAR, naming a FLAT world")
                + ", but the save was made on " + (flat ? "a FLAT world" : "an east-west WRAPPING world"),
                expected, StringComparison.Ordinal);
            Assert.Contains("The trailer is corrupt", expected, StringComparison.Ordinal);
            Assert.Equal(message, expected);                                       // the whole text, remedy included

            var e = Assert.Throws<SnapshotFormatException>(() => SaveFiles.Load(path, 256));
            Assert.Equal(expected, e.Message);

            (int exit, string stdout, string stderr) = CliProcess.Run(Limit, "hash", path, "--size", "256");
            Assert.Equal(2, exit);
            Assert.Equal("", stdout);
            Assert.Equal(new[] { "error: " + expected }, CliProcess.Lines(stderr));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>The other side of RC1-2's rule: a save that does NOT load under the other topology either keeps the
    /// refusal it had — a wrong `--size` is still the terrain mismatch, a missing trailer still "save is truncated", unknown
    /// bits still name the bits — and an intact save still loads.</summary>
    [Fact]
    public void ASaveRefusedForAnotherReason_KeepsItsOwnRefusal_UnderTheTrailerRecheck()
    {
        string dir = Path.Combine(Path.GetTempPath(), "civsim-flipctl-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string intact = Path.Combine(dir, "intact.civsnap");
            WorldState world = SaveV33(intact, flat: false, flipTrailer: 0);
            Assert.Equal(WorldHash.ComputeHex(world), WorldHash.ComputeHex(SaveFiles.Load(intact, 256)));

            // Wrong size: neither topology regenerates the terrain the save was made against.
            Assert.StartsWith("terrain mismatch: ",
                Assert.Throws<SnapshotFormatException>(() => SaveFiles.Load(intact, 252)).Message, StringComparison.Ordinal);
            // A flipped trailer AND a wrong size: the other topology does not explain it either.
            string flipped = Path.Combine(dir, "flipped.civsnap");
            SaveV33(flipped, flat: false, flipTrailer: TopologyFlags.WrapX);
            Assert.StartsWith("terrain mismatch: ",
                Assert.Throws<SnapshotFormatException>(() => SaveFiles.Load(flipped, 252)).Message, StringComparison.Ordinal);

            // Truncated: the trailer is missing.
            string truncated = Path.Combine(dir, "truncated.civsnap");
            byte[] bytes = File.ReadAllBytes(intact);
            File.WriteAllBytes(truncated, bytes.AsSpan(0, bytes.Length - 4).ToArray());
            Assert.Equal("save is truncated: the schema-v33 topology trailer after the state stream is missing.",
                Assert.Throws<SnapshotFormatException>(() => SaveFiles.Load(truncated, 256)).Message);

            // Unknown bits.
            string unknown = Path.Combine(dir, "unknown.civsnap");
            SaveV33(unknown, flat: false, flipTrailer: 0x2);
            Assert.StartsWith("save carries unknown topology bits 0x00000003",
                Assert.Throws<SnapshotFormatException>(() => SaveFiles.Load(unknown, 256)).Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>
    /// Fix round 1 (verifier F1, 2026-10-15) — `sim inspect` OF A SESSION THE BATCH-2 (SCHEMA v32) BUILD RECORDED
    /// (Fixtures/v32-session-found: two FoundSettlement orders whose sites are legal only on the flat map it was played
    /// on). MEASURED before the fix: exit 2, "error: order (turn 5): FoundSettlement targets settlement 12, which does not
    /// exist when the order is delivered (turn 5, 12 settlement(s)). …" — inspect re-founded it on the shipped cylinder; an
    /// orderless batch-2 session printed "REPRODUCTION FAILED at turn 0", a determinism verdict on a schema difference.
    /// Now it is rebuilt flat (TopologyFlags.RecordedFlat, the rule UiSession.Resume uses) and, like Resume (S0), the
    /// trace's hashes are not compared across schemas — it says so. The replayed rows are the batch-2 build's own: the
    /// turn-8 row is the one that build printed for the same session (population 5,365, food 5,900, 14 settlements).
    ///
    /// Fix round 2 (verifier RC1-1, 2026-10-15) — THE LINES ABOVE THE VERDICT AGREE WITH IT. MEASURED on fix round 1's
    /// tree, above "trace not checked": "  platform  reference platform on both sides: a hash mismatch below is a
    /// determinism finding." (no hash is compared) and "  reproduce with: sim replay --founded --seed 42 --orders
    /// orders-7166834e1157f6bc.bin --turns 8" — a recipe that exited 2 on this build ("error: order (turn 5):
    /// FoundSettlement targets settlement 12, which does not exist when the order is delivered (turn 5, 12
    /// settlement(s)). …": it founds the cylinder) and exited 0 with hash 6157dfde… on the batch-2 build that recorded the
    /// session. Now only the plain platform line prints, and the recipe line names `sim inspect` and gives the replay
    /// recipe as the recording build's.
    /// </summary>
    [Fact]
    public void SimInspect_OfABatch2Session_RebuildsItOnTheFlatMapItWasPlayedOn_AndReproducesItInTheV32Form()
    {
        string src = Path.Combine(AppContext.BaseDirectory, "Fixtures", "v32-session-found");
        string dir = Path.Combine(Path.GetTempPath(), "civsim-b2inspect-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            foreach (string f in Directory.GetFiles(src)) File.Copy(f, Path.Combine(dir, Path.GetFileName(f)));
            string manifest = Path.Combine(dir, "session-7166834e1157f6bc.json");

            (int exit, string stdout, string stderr) = CliProcess.Run(Limit, "inspect", "--manifest", manifest);

            Assert.Equal((0, ""), (exit, stderr));
            Assert.DoesNotContain("REPRODUCTION FAILED", stdout, StringComparison.Ordinal);
            // D-057: the trace is checked in the v32 form, and this build reproduces the batch-2 build's game — its colony
            // (kind 9) and the order for it included — hash for hash through the last recorded turn. (Until D-057 the line
            // was NotChecked(32): no trace check.)
            Assert.Contains(SessionSchemaNotice.CheckedInRecordedForm(32, 8, -1, 8), CliProcess.Lines(stdout));
            Assert.Contains("the replay reproduces the recorded game through turn 8", stdout, StringComparison.Ordinal);
            Assert.Contains("on the flat map v32 used", SessionSchemaNotice.CheckedInRecordedForm(32, 8, -1, 8), StringComparison.Ordinal);
            Assert.DoesNotContain(SessionSchemaNotice.NotChecked(32), stdout, StringComparison.Ordinal);
            // The batch-2 build's own turn-8 row for this session (its `sim inspect` output, README).
            Assert.Contains("       8       80       5,365       +38         5,900        +216      14", CliProcess.Lines(stdout));

            // Fix round 2 (RC1-1): no line classifies a hash comparison that is not made — the platform block is its
            // plain provenance line alone (the fixture was played on ubuntu.24.04-x64, a reference platform).
            string[] lines = CliProcess.Lines(stdout);
            string platform = PlatformNotice.For("ubuntu.24.04-x64", RuntimeInformation.RuntimeIdentifier, hashesChecked: false);
            Assert.DoesNotContain('\n', platform);
            Assert.StartsWith("  platform  played on ubuntu.24.04-x64 (reference platform); inspecting on ", platform, StringComparison.Ordinal);
            Assert.Contains(platform, lines);
            Assert.DoesNotContain("a hash mismatch below is a determinism finding", stdout, StringComparison.Ordinal);
            Assert.DoesNotContain("NOTICE", stdout, StringComparison.Ordinal);
            // …and the recipe line is not this build's `sim replay` (which founds the cylinder): it names `sim inspect`,
            // and the replay recipe is given as the recording build's (dev, local — what the manifest records).
            Assert.DoesNotContain(lines, l => l.StartsWith("  reproduce with: sim replay", StringComparison.Ordinal));
            Assert.Contains("  reproduce with: sim inspect --manifest session-7166834e1157f6bc.json (it rebuilds a v32 session on the flat map v32 used)", lines);
            // D-055 (2026-10-16) CAUSE BLOCK: "(schema v33)" -> "(schema v34)" — the line names this build's schema; v34 is it now.
            // ADR-043 (2026-10-22) CAUSE BLOCK: "(schema v34)" -> "(schema v35)" — the line names this build's schema.
            Assert.Contains("          not with this build's sim replay: it founds the cylinder this build ships (schema v35), not that flat map.", lines);
            Assert.Contains("          The build that recorded the session (dev, local) replays it with: "
                + "sim replay --founded --seed 42 --orders orders-7166834e1157f6bc.bin --turns 8", lines);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>
    /// Fix round 2 (verifier RC1-1, 2026-10-15) — THE OTHER SIDE OF THE RULE: a session THIS build recorded keeps the
    /// hash-check platform lines and the `sim replay` recipe, and the recipe reproduces the session — the replay's
    /// final hash is the one the recording run printed. (Before fix round 2 nothing pinned either line for a current
    /// session; a rule that dropped them for every session would have passed.)
    /// </summary>
    [Fact]
    public void SimInspect_OfASessionThisBuildRecorded_KeepsTheHashCheckLines_AndPrintsARecipeThatReproducesIt()
    {
        string dir = Path.Combine(Path.GetTempPath(), "civsim-v33inspect-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            (int runExit, string runOut, string runErr) =
                CliProcess.Run(Limit, "run", "--founded", "--seed", "42", "--turns", "2", "--emit-session", dir);
            Assert.Equal((0, ""), (runExit, runErr));
            string runDone = Assert.Single(CliProcess.Lines(runOut), l => l.StartsWith("run complete: ", StringComparison.Ordinal));
            string hash = runDone[(runDone.LastIndexOf(' ') + 1)..];
            Assert.Equal(64, hash.Length);

            string manifestPath = Assert.Single(Directory.GetFiles(dir, "session-*.json"));
            SessionManifest manifest;
            using (FileStream f = File.OpenRead(manifestPath)) manifest = SessionManifest.Read(f, manifestPath);
            Assert.Equal(CanonicalSchema.Version, manifest.SchemaVersion);

            (int exit, string stdout, string stderr) = CliProcess.Run(Limit, "inspect", "--manifest", manifestPath);
            Assert.Equal((0, ""), (exit, stderr));
            Assert.Contains("reproduction VERIFIED: 3 turns, hash-for-hash.", stdout, StringComparison.Ordinal);
            // The whole platform block, hash-check lines included (on the reference platform: "… a hash mismatch below
            // is a determinism finding."; off it, the NOTICE paragraphs) — always more than the provenance line.
            string platform = PlatformNotice.For(manifest.Platform, RuntimeInformation.RuntimeIdentifier);
            Assert.Contains('\n', platform);
            Assert.Contains(platform, stdout, StringComparison.Ordinal);
            string recipe = manifest.ReplayCommand(2);
            Assert.Contains("  reproduce with: " + recipe, CliProcess.Lines(stdout));

            // Run the printed recipe as a user would from the session directory (its --orders names the log there).
            string[] args = recipe["sim ".Length..].Split(' ');
            int orders = Array.IndexOf(args, "--orders");
            args[orders + 1] = Path.Combine(dir, args[orders + 1]);
            (int replayExit, string replayOut, string replayErr) = CliProcess.Run(Limit, args);
            Assert.Equal((0, ""), (replayExit, replayErr));
            Assert.Contains("replay complete: seed 42, 2 turns, hash " + hash, CliProcess.Lines(replayOut));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>Fix round 2 (verifier RC1-1) — the recipe line as a pure function of the manifest: this build's
    /// `sim replay` recipe for this build's schema; for a schema before v33, `sim inspect` and the recording build's recipe,
    /// for any recorded size (a 250 px batch-2 session's recipe was refused by this build's cylinder size rule, MEASURED).
    /// D-055 (2026-10-16) CAUSE BLOCK (X1 (f)): was "…FromV33…" with a v33 manifest given this build's recipe. Under v34 a
    /// v33 session's hashes are no longer checked (SessionSchemaNotice.HashesChecked is schema equality), so its recipe is
    /// the recording build's (the newer-schema rule's two lines, with "recorded under v33"); this build's own recipe now
    /// belongs to a v34 manifest, and the flat-schema line names "(schema v34)".</summary>
    [Fact]
    public void TheReproduceLine_IsThisBuildsReplay_FromV34_AndNamesSimInspect_ForAFlatSchema()
    {
        // ADR-043 (2026-10-22) RE-PIN: this build is schema v35, so "this build's" manifest is v35 (was the literal v34) and
        // the older-schema lines name v35. The local keeps its historical name.
        var v34 = new SessionManifest(
            Seed: 7, SizePx: 252, Settlements: null, SchemaVersion: 35,
            BuildSha: "abc1234", BuildDate: "2026-10-15", StartedAt: "x",
            OrdersFile: "orders-a.bin", ChronicleFile: "chronicle-a.txt", TraceFile: "trace-a.csv",
            TelemetryFile: "telemetry-a.jsonl", Platform: SessionManifest.ReferencePlatform);
        Assert.Equal("  reproduce with: " + v34.ReplayCommand(5), SessionSchemaNotice.Reproduce(v34, "session-a.json", 5));
        Assert.StartsWith("  reproduce with: sim replay --founded --seed 7 --size 252 ", SessionSchemaNotice.Reproduce(v34, "session-a.json", 5), StringComparison.Ordinal);

        // A batch-3 (v33) session: a cylinder, but not this build's schema — the recording build's recipe.
        SessionManifest v33 = v34 with { SchemaVersion = 33 };
        string[] v33Lines = SessionSchemaNotice.Reproduce(v33, "session-a.json", 5).Split('\n');
        Assert.Equal(2, v33Lines.Length);
        Assert.Equal("  reproduce with: the build that recorded the session (abc1234, 2026-10-15): "
            + "sim replay --founded --seed 7 --size 252 --orders orders-a.bin --turns 5", v33Lines[0]);
        Assert.Equal("          not with this build's sim replay: this build is schema v35 and the session was recorded "
            + "under v33, so no replay here reproduces its hashes (D-008).", v33Lines[1]);

        SessionManifest v32 = v34 with { SchemaVersion = 32, SizePx = 250, BuildSha = "dev", BuildDate = "local" };
        string text = SessionSchemaNotice.Reproduce(v32, "session-a.json", 5);
        string[] lines = text.Split('\n');
        Assert.Equal(3, lines.Length);
        Assert.Equal("  reproduce with: sim inspect --manifest session-a.json (it rebuilds a v32 session on the flat map v32 used)", lines[0]);
        Assert.Equal("          not with this build's sim replay: it founds the cylinder this build ships (schema v35), not that flat map.", lines[1]);
        Assert.Equal("          The build that recorded the session (dev, local) replays it with: "
            + "sim replay --founded --seed 7 --size 250 --orders orders-a.bin --turns 5", lines[2]);
        Assert.StartsWith("  reproduce with: sim inspect --manifest session-a.json (it rebuilds a v31 session",
            SessionSchemaNotice.Reproduce(v32 with { SchemaVersion = 31 }, "session-a.json", 5), StringComparison.Ordinal);
    }

    /// <summary>
    /// Final nits (verifier RC2-1, 2026-10-15) — ONE CONDITION FOR THE RECIPE AND THE VERDICT. The recipe line was chosen
    /// by RecordedFlat while the verdict was chosen by schema equality, so a session recorded under a NEWER schema
    /// (v34) got this build's `sim replay` recipe above "trace not checked" — MEASURED on r2 with `sim inspect` of a v33
    /// session whose manifest was set to v34: "  reproduce with: sim replay --founded --seed 42 --orders
    /// orders-7d8bfccc29cce001.bin --turns 3", then "trace not checked: the session was recorded under save schema v34 …".
    /// Now <see cref="SessionSchemaNotice.HashesChecked"/> (schema equality) is the condition for the platform block, the
    /// recipe and the verdict; a newer-schema session's recipe is the recording build's, and this build's is disowned.
    /// </summary>
    [Fact]
    public void TheReproduceLine_ForANewerSchema_IsTheRecordingBuildsReplay_AndHashesAreCheckedOnlyUnderThisSchema()
    {
        Assert.False(SessionSchemaNotice.HashesChecked(CanonicalSchema.Version - 1));
        Assert.True(SessionSchemaNotice.HashesChecked(CanonicalSchema.Version));
        Assert.False(SessionSchemaNotice.HashesChecked(CanonicalSchema.Version + 1));   // not "not RecordedFlat"

        // D-055 (2026-10-16) CAUSE BLOCK (X1 (f)): the "newer schema" manifest was the literal SchemaVersion 34 — this build's
        // schema since v34, whose hashes ARE checked (the premise inverted). It is now Version + 1, never a literal, and the
        // expected texts are computed from the version.
        int newer = CanonicalSchema.Version + 1;
        string current = CanonicalSchema.Version.ToString(System.Globalization.CultureInfo.InvariantCulture);
        string later = newer.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var newerManifest = new SessionManifest(
            Seed: 7, SizePx: 252, Settlements: null, SchemaVersion: newer,
            BuildSha: "abc1234", BuildDate: "2026-11-01", StartedAt: "x",
            OrdersFile: "orders-a.bin", ChronicleFile: "chronicle-a.txt", TraceFile: "trace-a.csv",
            TelemetryFile: "telemetry-a.jsonl", Platform: SessionManifest.ReferencePlatform);
        string[] lines = SessionSchemaNotice.Reproduce(newerManifest, "session-a.json", 5).Split('\n');
        Assert.Equal(2, lines.Length);
        Assert.Equal("  reproduce with: the build that recorded the session (abc1234, 2026-11-01): "
            + "sim replay --founded --seed 7 --size 252 --orders orders-a.bin --turns 5", lines[0]);
        Assert.Equal("          not with this build's sim replay: this build is schema v" + current + " and the session was recorded "
            + "under v" + later + ", so no replay here reproduces its hashes (D-008).", lines[1]);
        Assert.DoesNotContain(lines, l => l.StartsWith("  reproduce with: sim replay", StringComparison.Ordinal));
    }

    /// <summary>A copy of the session <paramref name="srcDir"/> holds, its manifest's schemaVersion set to
    /// <paramref name="schema"/> (null: the files copied byte for byte); returns the copied manifest's path.</summary>
    private static string CopySessionAs(string srcDir, string dir, int? schema)
    {
        Directory.CreateDirectory(dir);
        foreach (string f in Directory.GetFiles(srcDir)) File.Copy(f, Path.Combine(dir, Path.GetFileName(f)));
        string path = Assert.Single(Directory.GetFiles(dir, "session-*.json"));
        if (schema is null) return path;
        SessionManifest m;
        using (FileStream f = File.OpenRead(path)) m = SessionManifest.Read(f, path);
        using (FileStream f = File.Create(path)) (m with { SchemaVersion = schema.Value }).Write(f);
        return path;
    }

    /// <summary>
    /// Final nits (verifier RC2-1) — `sim inspect` OF A SESSION A NEWER BUILD RECORDED. The same session this build
    /// recorded, inspected as itself and relabelled v34: as itself it is VERIFIED with this build's recipe; relabelled it
    /// is not checked, and every line above the verdict agrees — the plain platform line, the recording build's recipe,
    /// no `sim replay` recipe from this build. (Mutant MF, `hashesChecked = !RecordedFlat(schema)`, compares the v34 trace
    /// again and prints "reproduction VERIFIED".)
    /// </summary>
    [Fact]
    public void SimInspect_OfASessionANewerSchemaRecorded_OffersOnlyTheRecordingBuildsReplay_AndChecksNoHash()
    {
        string root = Path.Combine(Path.GetTempPath(), "civsim-v34inspect-" + Guid.NewGuid().ToString("N"));
        string rec = Path.Combine(root, "rec");
        Directory.CreateDirectory(rec);
        try
        {
            (int runExit, _, string runErr) = CliProcess.Run(Limit, "run", "--founded", "--seed", "42", "--turns", "3", "--emit-session", rec);
            Assert.Equal((0, ""), (runExit, runErr));
            string own = Assert.Single(Directory.GetFiles(rec, "session-*.json"));
            SessionManifest manifest;
            using (FileStream f = File.OpenRead(own)) manifest = SessionManifest.Read(f, own);

            // Premise: as recorded (v33) the trace is checked and reproduces.
            (int e0, string o0, string r0) = CliProcess.Run(Limit, "inspect", "--manifest", own);
            Assert.Equal((0, ""), (e0, r0));
            Assert.Contains("reproduction VERIFIED: 4 turns, hash-for-hash.", o0, StringComparison.Ordinal);
            Assert.Contains("  reproduce with: " + manifest.ReplayCommand(3), CliProcess.Lines(o0));

            string v34 = CopySessionAs(rec, Path.Combine(root, "v34"), CanonicalSchema.Version + 1);
            (int exit, string stdout, string stderr) = CliProcess.Run(Limit, "inspect", "--manifest", v34);
            Assert.Equal((0, ""), (exit, stderr));
            string[] lines = CliProcess.Lines(stdout);
            Assert.Contains(SessionSchemaNotice.NotChecked(CanonicalSchema.Version + 1), lines);
            Assert.DoesNotContain("reproduction VERIFIED", stdout, StringComparison.Ordinal);
            Assert.DoesNotContain("REPRODUCTION FAILED", stdout, StringComparison.Ordinal);
            Assert.Contains(PlatformNotice.For(manifest.Platform, RuntimeInformation.RuntimeIdentifier, hashesChecked: false), lines);
            Assert.DoesNotContain("a hash mismatch below is a determinism finding", stdout, StringComparison.Ordinal);
            Assert.DoesNotContain(lines, l => l.StartsWith("  reproduce with: sim replay", StringComparison.Ordinal));
            Assert.Contains("  reproduce with: the build that recorded the session (" + manifest.BuildSha + ", "
                + manifest.BuildDate + "): " + manifest.ReplayCommand(3), lines);
            // The recipe precedes the verdict, and agrees with it.
            Assert.True(Array.FindIndex(lines, l => l.StartsWith("  reproduce with: ", StringComparison.Ordinal))
                < Array.FindIndex(lines, l => l.StartsWith("trace not checked: ", StringComparison.Ordinal)));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    /// Final nits (verifier RC2-2, 2026-10-15) — AN EMPTY TRACE GETS NO RECIPE. MEASURED on r2, `sim inspect` of
    /// Fixtures/v32-session-found copied without its trace printed "  reproduce with: sim inspect --manifest
    /// session-7166834e1157f6bc.json (it rebuilds a v32 session on the flat map v32 used)" and the recording build's
    /// "… --turns 0" recipe, then "no turns recorded in trace-7166834e1157f6bc.csv — nothing to inspect." — a recipe
    /// pointing at the command that says there is nothing to inspect. Now an empty trace prints the plain platform line
    /// (no hash comparison follows), then the empty-trace line, and no recipe — for a pre-v33 session and for a session
    /// this build recorded alike.
    /// </summary>
    [Fact]
    public void SimInspect_OfAnEmptyTrace_SaysSo_AndOffersNoRecipe()
    {
        string root = Path.Combine(Path.GetTempPath(), "civsim-emptyinspect-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string src32 = Path.Combine(AppContext.BaseDirectory, "Fixtures", "v32-session-found");
            string b2 = CopySessionAs(src32, Path.Combine(root, "v32"), schema: null);   // the real batch-2 record
            File.Delete(Path.Combine(root, "v32", "trace-7166834e1157f6bc.csv"));

            string rec = Path.Combine(root, "v33");
            Directory.CreateDirectory(rec);
            (int runExit, _, string runErr) = CliProcess.Run(Limit, "run", "--founded", "--seed", "42", "--turns", "2", "--emit-session", rec);
            Assert.Equal((0, ""), (runExit, runErr));
            string own = Assert.Single(Directory.GetFiles(rec, "session-*.json"));
            File.Delete(Assert.Single(Directory.GetFiles(rec, "trace-*.csv")));

            foreach (string manifestPath in new[] { b2, own })
            {
                SessionManifest manifest;
                using (FileStream f = File.OpenRead(manifestPath)) manifest = SessionManifest.Read(f, manifestPath);
                (int exit, string stdout, string stderr) = CliProcess.Run(Limit, "inspect", "--manifest", manifestPath);
                Assert.Equal((0, ""), (exit, stderr));
                string[] lines = CliProcess.Lines(stdout);
                Assert.Equal("no turns recorded in " + manifest.TraceFile + " — nothing to inspect.", lines[^1]);
                Assert.DoesNotContain("reproduce with", stdout, StringComparison.Ordinal);
                Assert.DoesNotContain("replays it with", stdout, StringComparison.Ordinal);
                Assert.Equal(PlatformNotice.For(manifest.Platform, RuntimeInformation.RuntimeIdentifier, hashesChecked: false), lines[^2]);
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>The topology a recorded schema implies (D-054): every world recorded before v33 was flat; v33 and later
    /// record their topology (the snapshot trailer), and a session manifest written by this build is the shipped
    /// worldgen's. The CLI's .civsnap loader and the session rebuilds (UiSession.Resume, `sim inspect`) share the rule.</summary>
    [Fact]
    public void ARecordedSchemaBeforeV33_IsFlat_AndTheSaveLoaderRegeneratesWhatTheTrailerNames()
    {
        Assert.Equal(33, TopologyFlags.FirstCylinderSchema);
        Assert.True(TopologyFlags.RecordedFlat(31));
        Assert.True(TopologyFlags.RecordedFlat(32));
        Assert.False(TopologyFlags.RecordedFlat(33));
        Assert.False(TopologyFlags.RecordedFlat(34));

        WorldgenConfig shipped = TestConfigs.Worldgen();
        Assert.True(shipped.Topology is { WrapX: true });
        Assert.Null(SaveFiles.WorldgenFor(32, shipped, -1, topology: TopologyFlags.WrapX).Topology);   // v32 has no trailer: flat
        Assert.Equal(new TopologyConfig(false), SaveFiles.WorldgenFor(33, shipped, -1, topology: 0).Topology);
        Assert.Equal(shipped, SaveFiles.WorldgenFor(33, shipped, -1, topology: TopologyFlags.WrapX));
        Assert.Equal(new TopologyConfig(true), SaveFiles.WorldgenFor(33, TestConfigs.PreCylinderWorldgen(shipped), -1, topology: 1).Topology);
        Assert.Equal(shipped, SaveFiles.WorldgenFor(33, shipped, -1));                                // no trailer read: as shipped
    }

    [Fact]
    public void TheNamedRemedy_Works_SimRunAt248Px_FoundsAndRuns()
    {
        // The 250-px refusal's lower suggestion is a size the CLI then accepts (MEASURED: `run complete`, exit 0).
        (int exit, string stdout, string stderr) = CliProcess.Run(Limit, "run", "--founded", "--seed", "42", "--turns", "1", "--size", "248");
        Assert.Equal(0, exit);
        Assert.Contains("run complete: seed 42, 1 turns, hash ", stdout, StringComparison.Ordinal);
        Assert.Equal("", stderr);
    }
}
