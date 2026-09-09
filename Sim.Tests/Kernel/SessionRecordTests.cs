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
        TelemetryFile: "telemetry-x.jsonl");

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
