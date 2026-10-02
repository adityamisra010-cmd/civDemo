using Sim.Core.Kernel;
using Sim.Core.Observability.Forensic;

namespace Sim.Tests.Kernel;

/// <summary>
/// ADR-033 D5 — the AI-empire override is part of a session's world identity (a session played against AI
/// Empires replays only into a world founded with them), and the forensic record tells the truth about AI
/// decisions now that AiOrders produces them.
/// </summary>
public class AiSessionRecordTests
{
    private static SessionManifest Sample(int? ai) => new(
        42, 256, 4, CanonicalSchema.Version, "sha", "date", "started",
        "orders-x.bin", "chronicle-x.txt", "trace-x.csv", "telemetry-x.jsonl", "linux-x64", "forensic-x.jsonl", ai);

    private static string Json(SessionManifest m)
    {
        using var buffer = new MemoryStream();
        m.Write(buffer);
        return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static SessionManifest RoundTrip(SessionManifest m)
    {
        using var buffer = new MemoryStream();
        m.Write(buffer);
        buffer.Position = 0;
        return SessionManifest.Read(buffer, "test");
    }

    [Fact]
    public void TheAiEmpireOverride_RoundTrips_AndTheReplayCommandCarriesIt()
    {
        SessionManifest m = Sample(3);
        Assert.Equal(m, RoundTrip(m));
        Assert.Equal(3, RoundTrip(m).AiEmpires);
        Assert.Contains("\"aiEmpires\": 3", Json(m));
        Assert.Contains("--ai-empires 3 ", m.ReplayCommand(10) + " ");
        Assert.Equal("session-manifest/v2", SessionManifest.Schema);   // an additive key, not a new vintage
    }

    [Fact]
    public void WithoutAnOverride_TheManifestIsWhatItWasBefore_NoKeyNoFlag_AndReadsBackNull()
    {
        SessionManifest m = Sample(null);
        Assert.DoesNotContain("aiEmpires", Json(m));
        Assert.DoesNotContain("--ai-empires", m.ReplayCommand(10));
        Assert.Null(RoundTrip(m).AiEmpires);
        Assert.Equal(m, RoundTrip(m));
    }

    [Fact]
    public void TheForensicRecord_StatesTheTruthAboutAiDecisions_WithoutMovingItsSchemaTokens()
    {
        ForensicRunRecord solo = Build(0), duo = Build(1);
        Assert.Contains("no AI actor", solo.AiNote);
        Assert.Contains("AiOrders", duo.AiNote);
        Assert.Contains("order log", duo.AiNote);
        Assert.DoesNotContain("no system emits an order", duo.AiNote, StringComparison.OrdinalIgnoreCase);
        ForensicLimitation ai = Assert.Single(duo.Limitations, l => l.Token == ForensicSchema.LimitNoAi);
        Assert.Equal("no-ai-at-m4", ForensicSchema.LimitNoAi);   // the token readers match on is unchanged
        Assert.Contains("AiOrders", ai.Why);
        Assert.DoesNotContain("No system in this build emits an order", ai.Why, StringComparison.Ordinal);
    }

    private static ForensicRunRecord Build(int ai) => ForensicSession.BuildRun(
        42, 256, 4, founded: true,
        contentAssembly: typeof(Sim.Data.DataFiles).Assembly,
        orders: new OrderLog(),
        era: Sim.Cli.CliRecipes.Era(),
        pipeline: Sim.Cli.CliRecipes.ProductionPipeline(),
        aiEmpiresConfigured: ai,
        terrainContentHash: null,
        buildSha: "abc", buildDate: "2026-10-02", platform: "linux-x64", startedAt: null);
}
