using System.Text;
using Sim.Core.Kernel;
using Sim.Core.State;

namespace Sim.Tests.Kernel;

/// <summary>
/// T4.17 — THE SESSION RECORD: the manifest that makes a played session
/// reproducible, and the trace that makes the reproduction CHECKABLE.
///
/// The defect these close is not subtle: a session's order log recorded what was
/// ordered but never which world it was ordered in, so the seed — the one input
/// that cannot be recovered afterwards — was written down nowhere. Every
/// analysis tool in the repo sat behind that missing number.
/// </summary>
public class SessionRecordTests
{
    private static SessionManifest Sample() => new(
        Seed: 42, SizePx: 256, Settlements: 4, SchemaVersion: CanonicalSchema.Version,
        BuildSha: "abc1234", BuildDate: "2026-09-09", StartedAt: "2026-09-09 12:00:00",
        OrdersFile: "orders-x.bin", ChronicleFile: "chronicle-x.txt", TraceFile: "trace-x.csv",
        TelemetryFile: "telemetry-x.jsonl",
        Platform: "win-x64");   // v2 (ADR-022): a NON-reference platform, so the round trip cannot pass on the default

    [Fact]
    public void ManifestRoundTripsEveryFieldItPromisesToCarry()
    {
        SessionManifest written = Sample();
        using var buffer = new MemoryStream();
        written.Write(buffer);
        buffer.Position = 0;

        SessionManifest read = SessionManifest.Read(buffer, "test");
        Assert.Equal(written, read);   // record equality — every field, not a chosen few
    }

    [Fact]
    public void AWorldWithNoOverridesRoundTripsThoseAsAbsentNotAsZero()
    {
        // A canonical game passes neither --size nor --settlements, and null is
        // NOT the same as 0: a size of zero is a world, absence is "the shipped
        // default". Writing null as 0 would silently replay a different world.
        SessionManifest written = Sample() with { SizePx = null, Settlements = null };
        using var buffer = new MemoryStream();
        written.Write(buffer);
        buffer.Position = 0;

        SessionManifest read = SessionManifest.Read(buffer, "test");
        Assert.Null(read.SizePx);
        Assert.Null(read.Settlements);
        Assert.DoesNotContain("--size", read.ReplayCommand(10));
        Assert.DoesNotContain("--settlements", read.ReplayCommand(10));
    }

    [Fact]
    public void TheWrittenFileCarriesTheV2TagAndThePlatformColumn()
    {
        // The tag is what a reader keys on; the column is the whole of v2.
        using var buffer = new MemoryStream();
        Sample().Write(buffer);
        string json = Encoding.UTF8.GetString(buffer.ToArray());

        Assert.Contains("\"schema\": \"session-manifest/v2\"", json);
        Assert.Contains("\"platform\": \"win-x64\"", json);
        Assert.Equal("session-manifest/v2", SessionManifest.Schema);
    }

    [Fact]
    public void AV1FileIsStillREADABLE_AndItsPlatformReadsAsNotRecorded_NeverAsAGuess()
    {
        // Every session the director played before ADR-022 has a v1 manifest,
        // and every one of them is still fully reproducible — the world is a
        // function of seed and order log, and the platform column is
        // provenance. Refusing v1 would lose real sessions to a column they
        // never had; inventing a platform for them would let `sim inspect`
        // classify a divergence on evidence that does not exist.
        const string v1 = """
            {
              "schema": "session-manifest/v1",
              "seed": 42,
              "sizePx": 256,
              "settlements": 4,
              "schemaVersion": 24,
              "buildSha": "abc1234",
              "buildDate": "2026-09-09",
              "startedAt": "2026-09-09 12:00:00",
              "ordersFile": "orders-x.bin",
              "chronicleFile": "chronicle-x.txt",
              "traceFile": "trace-x.csv",
              "telemetryFile": "telemetry-x.jsonl"
            }
            """;
        using var buffer = new MemoryStream(Encoding.UTF8.GetBytes(v1));
        SessionManifest read = SessionManifest.Read(buffer, "runs/old.json");

        Assert.Equal(42UL, read.Seed);
        Assert.Equal("orders-x.bin", read.OrdersFile);
        Assert.Equal(SessionManifest.PlatformNotRecorded, read.Platform);
        Assert.False(SessionManifest.IsPlatformRecorded(read.Platform));
        Assert.False(SessionManifest.IsReferencePlatform(read.Platform));
        // And a v1 session re-written by this build is a v2 file with the
        // not-recorded marker preserved — the reader must not launder it into
        // a platform on the way through.
        using var again = new MemoryStream();
        read.Write(again);
        again.Position = 0;
        Assert.Equal(SessionManifest.PlatformNotRecorded, SessionManifest.Read(again, "rewritten").Platform);
    }

    [Theory]
    [InlineData("linux-x64", true)]           // the CI runner (CR-013 §8.4, run 34419607514: "RID: linux-x64")
    [InlineData("ubuntu.24.04-x64", true)]    // the remote-session container (CR-013 §8.2) — measured equal to the runner
    [InlineData("ubuntu.22.04-x64", true)]    // the same distro RID family; the rule is the family, not one version
    [InlineData("win-x64", false)]            // the director's machine and windows-latest (CR-013 §2, §8.4)
    [InlineData("osx-arm64", false)]
    [InlineData("linux-arm64", false)]        // x64 is part of the reference, not only the OS
    [InlineData("linux-musl-x64", false)]     // musl libm is not glibc libm; unmeasured, therefore not reference
    [InlineData("debian.12-x64", false)]      // the RID graph would resolve it to linux-x64; nobody has measured it
    [InlineData("ubuntu.-x64", false)]        // a bare prefix+suffix is not a RID
    [InlineData("", false)]
    [InlineData(SessionManifest.PlatformNotRecorded, false)]
    public void TheReferencePlatformIsExactlyTheTwoMeasuredSpellings(string rid, bool reference)
    {
        // The set is grown by MEASUREMENT, not by RID inheritance: CR-013's
        // divergence lives in libm, about which the RID graph says nothing.
        Assert.Equal(reference, SessionManifest.IsReferencePlatform(rid));
        Assert.Equal("linux-x64", SessionManifest.ReferencePlatform);
    }

    [Fact]
    public void AFileWithoutTheSchemaTagIsREFUSEDAndNamed()
    {
        // A reader that silently accepts an unknown vintage would rebuild the
        // wrong world and report the difference as a finding about the sim.
        using var buffer = new MemoryStream(Encoding.UTF8.GetBytes("""{"seed":42}"""));
        var e = Assert.Throws<InvalidDataException>(
            () => SessionManifest.Read(buffer, "runs/broken.json"));

        Assert.Contains("runs/broken.json", e.Message);
        Assert.Contains(SessionManifest.Schema, e.Message);
    }

    [Fact]
    public void TheReplayCommandNamesTheRecordedWorldAndTheTurnsAsked()
    {
        string cmd = Sample().ReplayCommand(90);
        Assert.Contains("--seed 42", cmd);
        Assert.Contains("--size 256", cmd);
        Assert.Contains("--settlements 4", cmd);
        Assert.Contains("--orders orders-x.bin", cmd);
        Assert.Contains("--turns 90", cmd);
        Assert.Contains("--founded", cmd);   // the played world is the founded one
    }

    [Fact]
    public void ATraceLineCarriesTheTurnItsTotalsAndTheHashThatPinsThem()
    {
        WorldState world = SnapshotTests.CanonicalExecutor().Run(SnapshotTests.Genesis(42), 3);
        string line = SessionTrace.Line(world, grainGoodId: 1);

        IReadOnlyList<SessionTrace.Row> rows = SessionTrace.Parse([SessionTrace.Header, line], "test");
        SessionTrace.Row row = Assert.Single(rows);

        Assert.Equal(world.Clock.Turn, row.Turn);
        Assert.Equal(world.Clock.WorldDateYears, row.Year);
        Assert.Equal(world.Settlements.Count, row.Settlements);
        // The hash is the whole point of the line: it is what a replay is
        // checked against, so it must be the world's real hash, not a summary.
        Assert.Equal(WorldHash.ComputeHex(world), row.Hash);
    }

    [Fact]
    public void TheSameWorldTracesIdentically_SoADIFFERENCEMeansTheWORLDDiffered()
    {
        // The cross-check is only evidence if the line is a pure function of the
        // world. If it carried anything incidental, a "divergence" would be a
        // property of the reporter rather than of the simulation.
        WorldState a = SnapshotTests.CanonicalExecutor().Run(SnapshotTests.Genesis(42), 3);
        WorldState b = SnapshotTests.CanonicalExecutor().Run(SnapshotTests.Genesis(42), 3);
        Assert.Equal(SessionTrace.Line(a, 1), SessionTrace.Line(b, 1));

        WorldState later = SnapshotTests.CanonicalExecutor().Run(SnapshotTests.Genesis(42), 4);
        Assert.NotEqual(SessionTrace.Line(a, 1), SessionTrace.Line(later, 1));
    }

    [Fact]
    public void AMalformedTraceLineTHROWSWithItsLineNumberRatherThanBeingSkipped()
    {
        // A trace read with holes in it makes a divergence report quietly
        // incomplete — the worst failure mode for a tool whose only job is to
        // say WHERE two runs stopped agreeing.
        var e = Assert.Throws<InvalidDataException>(() => SessionTrace.Parse(
            [SessionTrace.Header, "0,0,100,4,500,abc", "1,10,oops"], "runs/t.csv"));

        Assert.Contains("runs/t.csv", e.Message);
        Assert.Contains("line 3", e.Message);
    }
}
