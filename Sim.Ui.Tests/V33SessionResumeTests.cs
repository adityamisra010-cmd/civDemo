using Sim.Core.Kernel;
using Sim.Core.State;
using Xunit;

namespace Sim.Ui.Tests;

/// <summary>
/// D-055 (2026-10-16; schema v34, the batch-4 design X1 "UI resume of a v33 session under v34", premise (iii)) — A REAL
/// SESSION THE BATCH-3 (SCHEMA v33) BUILD RECORDED, RESUMED BY THIS v34 BUILD. The fixture is that build's own record
/// (Sim.Tests/Fixtures/README-v33-fixture.md; its CLI stamped b3-final-v9-0f8f45c9): seed 42, 256 px, four settlements, one
/// AI Empire, six turns, the player's 30/70 split, granaries at s0 and s2 and s0's builders taken away — the Q17 shape.
/// It takes the schema-mismatch path (D-008): it is REPLAYED under this build's rules with the four-line notice and its
/// trace is NOT checked, so it is not refused for trace divergence. A v33 world is a cylinder, so it is rebuilt on the
/// shipped worldgen (TopologyFlags.RecordedFlat(33) is false) — and the resumed world is exactly a fresh replay of the
/// same seed and log by this build.
/// </summary>
public class V33SessionResumeTests
{
    private const string Stamp = "f1cc70bce302a9a0";

    private static string CopyFixture(out string dir)
    {
        string src = Path.Combine(AppContext.BaseDirectory, "Fixtures", "v33-session-q17");
        dir = Path.Combine(Path.GetTempPath(), "b3-session-resume-"
            + Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        foreach (string f in new[] { "session-" + Stamp + ".json", "orders-" + Stamp + ".bin", "trace-" + Stamp + ".csv" })
            File.Copy(Path.Combine(src, f), Path.Combine(dir, f));
        return Path.Combine(dir, "session-" + Stamp + ".json");
    }

    [Fact]
    public void ABatch3Session_ResumesUnderV34_WithTheFourLineNotice_OnTheCylinder_IntoTheFreshShippedReplay()
    {
        string manifest = CopyFixture(out string dir);
        try
        {
            SessionManifest m;
            using (FileStream f = File.OpenRead(manifest)) m = SessionManifest.Read(f, manifest);
            Assert.Equal(33, m.SchemaVersion);                                     // the fixture's premise
            Assert.Equal("b3-final-v9-0f8f45c9", m.BuildSha);

            var stderr = new StringWriter();
            string? dialog = null;
            UiSession? s = ResumeLaunch.Attempt(manifest, stderr, (title, message) => dialog = message, out string log, out int code);

            Assert.Equal("", stderr.ToString());
            Assert.Null(dialog);
            Assert.Equal(0, code);                                                 // not refused (no trace check)
            Assert.NotNull(s);
            Assert.Equal(Path.Combine(dir, "orders-" + Stamp + ".bin"), log);
            Assert.Equal(6, s!.World.Clock.Turn);                                  // the trace's last turn
            Assert.True(s.World.Terrain!.WrapX);                                   // the cylinder it was played on

            // The four-line notice, its second line the cylinder one.
            Assert.Equal(UiSession.SchemaMismatchNotice(33, CanonicalSchema.Version), s.ResumeNotice);
            Assert.Equal(4, s.ResumeNotice.Count);
            Assert.Equal("Recorded under save schema v33; this build is v35.", s.ResumeNotice[0]);   // ADR-043 (2026-10-22): v35 (was v34)
            Assert.Equal("Resumed by replaying its seed and orders (D-008).", s.ResumeNotice[1]);

            // The fresh shipped replay of the same seed and log, with no trace at all: the same world.
            OrderLog saved;
            using (FileStream f = File.OpenRead(log)) saved = OrderLog.Load(f);
            // M6 Track EX (2026-10-22): a pre-v35 session replays without the discovery layer (it was played without it;
            // DiscoveryQuery.RecordedWithoutDiscovery), so the fresh replay it equals is the shipped one with discovery off.
            UiSession fresh = UiSession.StartWith(TestFog.PreDiscovery, 42, sizeOverridePx: 256, settlementsOverride: 4, aiEmpiresOverride: 1);
            fresh.ReplayTo(saved, 6);
            Assert.True(fresh.World.Terrain!.WrapX);
            Assert.Equal(WorldHash.ComputeHex(fresh.World), WorldHash.ComputeHex(s.World));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
