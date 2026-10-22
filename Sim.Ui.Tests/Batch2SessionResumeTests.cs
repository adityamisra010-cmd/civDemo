using System.Security.Cryptography;
using Sim.Core.Kernel;
using Sim.Core.State;
using Xunit;

namespace Sim.Ui.Tests;

/// <summary>
/// Fix round 1 (D-054, verifier F1, 2026-10-15) — A SESSION THE BATCH-2 (SCHEMA v32) BUILD RECORDED, RESUMED BY THIS
/// BUILD. The fixture is that build's own record (Sim.Tests/Fixtures/v32-session-found/README.md): seed 42, two
/// FoundSettlement orders whose sites are legal only on the FLAT map it was played on. Every world recorded under a
/// schema before v33 was flat (the cylinder, WS3, arrived with v33), but Resume re-founded every session with the
/// shipped worldgen, so this one was rebuilt on the cylinder. MEASURED before the fix, through the launcher seam
/// (ResumeLaunch.Attempt): exit 2, no session, and on stderr "error: order (turn 5): FoundSettlement targets settlement
/// 12, which does not exist when the order is delivered (turn 5, 12 settlement(s)). …". An orderless batch-2 session
/// resumed onto a different map, and the notice said nothing about it.
/// Now Resume founds a pre-v33 session flat (TopologyFlags.RecordedFlat — the CLI's .civsnap rule, one rule). The notice
/// says so in its own line. The resumed world is the very world the batch-2 build played: its v32 projection equals the
/// trace's turn-8 hash.
/// </summary>
public class Batch2SessionResumeTests
{
    private const string Stamp = "7166834e1157f6bc";

    /// <summary>The batch-2 build's own turn-8 hash for this session (the trace's last row; README).</summary>
    private const string Turn8V32Hash = "6157dfde1b3c9dc9806fca088d2042831bcc3f344692e5076039947d7fe756c4";

    /// <summary>Copies the fixture to a fresh directory (a resumed session continues into its own files) and returns the
    /// copied manifest's path.</summary>
    private static string CopyFixture(out string dir)
    {
        string src = Path.Combine(AppContext.BaseDirectory, "Fixtures", "v32-session-found");
        dir = Path.Combine(Path.GetTempPath(), "b2-session-resume-"
            + Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        foreach (string f in new[] { "session-" + Stamp + ".json", "orders-" + Stamp + ".bin", "trace-" + Stamp + ".csv" })
            File.Copy(Path.Combine(src, f), Path.Combine(dir, f));
        return Path.Combine(dir, "session-" + Stamp + ".json");
    }

    /// <summary>THE v32 PROJECTION (Sim.Tests' SchemaProjection.V32, restated: this project does not reference Sim.Tests):
    /// v33 appended two tables after section 58, so a world with both empty serializes to the v32 stream plus two empty
    /// count prefixes (8 zero bytes); its v32 hash is the SHA-256 of the stream without them.
    /// D-055 (2026-10-16) CAUSE BLOCK (X1 (d)): v34 appends a third table, ConstructionHolds (section 61), so the
    /// projection asserts it empty too and drops 12 bytes (8 + 4) — the layout alone; nothing else changed.</summary>
    /// ADR-043 (2026-10-22) CAUSE BLOCK: v35 appends CivicsPoints (section 62). The resume replays under this build's rules,
    /// in which every Empire accrues Civics Points from turn 1 (an account row each) — on this 8-turn world no civic is yet
    /// available, so the accounts are Track CP's whole footprint (Sim.Tests' CivicsPointsAttributionTests.
    /// TheCpLayer_ActsFromTheFirstStep_TheTwinIsTheV34WorldAtTurnZero). The projection clears them and drops 16 bytes.
    private static string HashAtSchemaV32(WorldState world)
    {
        Assert.Equal((0, 0, 0), (world.ConstructionProgress.Count, world.ResourceAllocations.Count, world.ConstructionHolds.Count));
        WorldState c = world.Clone();
        Assert.True(c.CivicsPoints.Count <= c.Polities.Count, "one CP account per Empire at most");
        c.CivicsPoints.Clear();
        using var buffer = new MemoryStream();
        using (var writer = new BinaryWriter(buffer, System.Text.Encoding.UTF8, leaveOpen: true)) CanonicalSchema.Write(c, writer);
        byte[] full = buffer.ToArray();
        for (int i = full.Length - 16; i < full.Length; i++) Assert.Equal(0, full[i]);
        return Convert.ToHexStringLower(SHA256.HashData(full.AsSpan(0, full.Length - 16)));
    }

    [Fact]
    public void ABatch2SessionWithFoundSettlementOrders_ResumesOnTheFlatMapItWasPlayedOn_IntoTheWorldItsTraceRecorded()
    {
        string manifest = CopyFixture(out string dir);
        try
        {
            using (FileStream m = File.OpenRead(manifest))
                Assert.Equal(32, SessionManifest.Read(m, manifest).SchemaVersion);   // the fixture's premise

            var stderr = new StringWriter();
            string? dialog = null;
            UiSession? s = ResumeLaunch.Attempt(manifest, stderr, (title, message) => dialog = message, out string log, out int code);

            Assert.Equal("", stderr.ToString());
            Assert.Null(dialog);
            Assert.Equal(0, code);
            Assert.NotNull(s);
            Assert.Equal(Path.Combine(dir, "orders-" + Stamp + ".bin"), log);
            Assert.Equal(8, s!.World.Clock.Turn);
            Assert.False(s.World.Terrain!.WrapX);                                  // the flat map it was played on
            Assert.Equal(14, s.World.Settlements.Count);                           // both colonies founded (trace: 14)
            Assert.Equal(Turn8V32Hash, HashAtSchemaV32(s.World));                  // the very world the batch-2 build played

            // The notice names the topology in its own line (four lines: the toast shows each whole).
            Assert.Equal(UiSession.SchemaMismatchNotice(32, CanonicalSchema.Version), s.ResumeNotice);
            Assert.Equal(4, s.ResumeNotice.Count);
            Assert.Equal("Replayed from its seed and orders on the flat map v32 used (D-008).", s.ResumeNotice[1]);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>
    /// The same rule at a size the cylinder refuses: a session the batch-2 build recorded at 250 px (a flat world takes any
    /// size; the cylinder needs one the 4-px stride divides). Before the fix its resume was refused through
    /// WorldgenConfigException (exit 2: "this world is a cylinder …"); now it is rebuilt flat and continues as recorded. The
    /// session is recorded here flat and its manifest stamped v32 (ChronicleAndResumeTests.RecordSchema) — what the batch-2
    /// build wrote — and its trace's hashes replaced, as a v32 trace's cannot equal this build's.
    /// </summary>
    [Fact]
    public void ABatch2SessionAt250Px_ResumesFlat_InsteadOfBeingRefusedByTheCylinder()
    {
        string dir = Path.Combine(Path.GetTempPath(), "b2-session-250-"
            + Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string path = Path.Combine(dir, "orders-20261010-120000-s250-n4.bin");
            UiSession live = UiSession.Start(42, sizeOverridePx: 250, settlementsOverride: 4, wrapXOverride: false);
            live.ExportManifest("2026-10-10 12:00:00", path);
            for (int t = 0; t < 3; t++) live.EndTurn();
            live.Save(path);
            live.ExportTrace(UiSession.TracePath(path));
            string manifest = UiSession.ManifestPath(path);
            ChronicleAndResumeTests.RecordSchema(manifest, 32);
            ChronicleAndResumeTests.RecordTraceUnderAnotherSchema(UiSession.TracePath(path));

            var stderr = new StringWriter();
            UiSession? s = ResumeLaunch.Attempt(manifest, stderr, null, out _, out int code);

            Assert.Equal((0, ""), (code, stderr.ToString()));
            Assert.False(s!.World.Terrain!.WrapX);
            Assert.Equal(3, s.World.Clock.Turn);
            Assert.Equal(WorldHash.ComputeHex(live.World), WorldHash.ComputeHex(s.World));
            Assert.Equal(UiSession.SchemaMismatchNotice(32, CanonicalSchema.Version), s.ResumeNotice);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>The premise, measured: the same log replayed on the shipped cylinder is refused at the second order's
    /// delivery — the colony the first order founds is not founded there, because its site is not legal on the cylinder.
    /// So the flat rebuild above is what makes the session resumable at all, not a coincidence of the replay.</summary>
    [Fact]
    public void TheSameLogOnTheShippedCylinder_IsRefusedAtTheSecondFoundSettlement()
    {
        _ = CopyFixture(out string dir);
        try
        {
            OrderLog saved;
            using (FileStream o = File.OpenRead(Path.Combine(dir, "orders-" + Stamp + ".bin"))) saved = OrderLog.Load(o);
            UiSession cylinder = UiSession.Start(42);
            Assert.True(cylinder.World.Terrain!.WrapX);
            OrderValidationException refused = Assert.Throws<OrderValidationException>(() => cylinder.ReplayTo(saved, 8));
            Assert.StartsWith("order (turn 5): FoundSettlement targets settlement 12, which does not exist when the order is delivered",
                refused.Message, StringComparison.Ordinal);

            UiSession flat = UiSession.Start(42, wrapXOverride: false);
            flat.ReplayTo(saved, 8);
            Assert.Equal(Turn8V32Hash, HashAtSchemaV32(flat.World));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
