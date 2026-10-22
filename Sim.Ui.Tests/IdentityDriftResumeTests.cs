using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Colonization;
using Sim.Core.Worldgen;
using Sim.Ui.ViewModel;
using Xunit;

namespace Sim.Ui.Tests;

/// <summary>
/// D-057 (2026-10-18) — A RECORDED ORDER FOR A COLONY FOUNDED AFTER TURN 0, IN A REPLAY THAT DRIFTED: the UI resume half
/// (<see cref="UiSession.Resume"/> → <see cref="UiSession.ReplayTo"/> → <see cref="ReplayProof"/>). The CLI half is
/// Sim.Tests' IdentityDriftGuardTests; ReplayProof's unit pins are there too.
///
/// THE RIG. The session is RECORDED under another build's rule — a Found Settlement party fraction so small that the
/// least populous of the player's settlements (s-small) can spare no one while the most populous (s-big) can spare one
/// (the rule is a TUNE number, ADR-036; another build's value is what "another build" means here) — and labelled schema
/// v33 with its trace in the v33 form, as a v33 build writes it:
/// <list type="bullet">
/// <item>turn 2: the player orders a colony from s-small (site A) — refused in the recording (no party);</item>
/// <item>turn 3: a colony from s-big (site B) — founded: settlement 12, the first free id;</item>
/// <item>turn 5: the player sets settlement 12's sectors (no builders): the colony at B.</item>
/// </list>
/// This build's rule lets s-small's party go, so the replay founds A as settlement 12 at turn 3 and B as 13 at turn 4, and
/// the turn-5 order names A — a different colony, which the player also controls. Before D-057 the resume applied it there
/// without a word (pinned below as the hazard, through the bare replay the resume used to be). Now the resume is REFUSED
/// before the turn-5 batch, with the diagnostic, exit 2, the dialog once and every file byte-identical; and the same game
/// recorded under this build's rule (no drift) resumes, the order landing on the colony it was given for.
/// </summary>
public class IdentityDriftResumeTests
{
    private static readonly PolityId Me = UiPlayer.Empire;
    private static readonly int[] NoBuilders = [55, 15, 10, 12, 0];
    private const string StartedAt = "2026-10-18 12:00:00";
    private const int OrderTurn = 5, Turns = 7;

    private static TurnExecutor Production(WorldgenConfig colonization, OrderLog orders)
    {
        using var pipe = Sim.Data.DataFiles.OpenPipeline();
        return new TurnExecutor(UiSession.ProductionEra(),
            PipelineLoader.Load(pipe, SystemCatalog.All(TestFog.PreDiscovery, colonization)), orders);
    }

    private static long Population(IReadOnlyWorldState w, SettlementId s)
    {
        long n = 0;
        for (int i = 0; i < w.Buckets.Count; i++) if (w.Buckets[i].Settlement == s) n += w.Buckets[i].Count.Value;
        return n;
    }

    private static int SettlementAt(IReadOnlyWorldState w, int cell)
    {
        for (int i = 0; i < w.Settlements.Count; i++) if (w.Settlements[i].SiteCell == cell) return w.Settlements[i].Id.Value;
        return -1;
    }

    private static int CellOf(IReadOnlyWorldState w, int settlement)
    {
        for (int i = 0; i < w.Settlements.Count; i++) if (w.Settlements[i].Id.Value == settlement) return w.Settlements[i].SiteCell;
        return -1;
    }

    /// <summary>The settlements whose sectors an order set to <see cref="NoBuilders"/> (construction weight 0).</summary>
    private static int[] NoBuilderSettlements(IReadOnlyWorldState w)
    {
        var ids = new List<int>();
        for (int i = 0; i < w.SectorAllocations.Count; i++)
            if (w.SectorAllocations[i].Construction == 0.0 && w.SectorAllocations[i].Herding > 0.0)   // not the all-farming default
                ids.Add(w.SectorAllocations[i].Settlement.Value);
        ids.Sort();
        return [.. ids];
    }

    /// <summary>The two Found Settlement orders: from <c>First</c> to <c>FirstSite</c> at turn 2, from <c>Second</c> to
    /// <c>SecondSite</c> at turn 3.</summary>
    private sealed record Plan(SettlementId First, int FirstSite, SettlementId Second, int SecondSite);

    private sealed record Recording(OrderLog Log, List<WorldState> Worlds, Plan Plan);

    /// <summary>
    /// The game, recorded with <paramref name="colonization"/> as ColonizationSystem's siting rule (this build's, or another
    /// build's). With no <paramref name="plan"/>, the sites are chosen on this build's rule as each order is issued
    /// (AiFoundingPolicy's best site for the source), from <paramref name="first"/> then <paramref name="second"/>; either
    /// way the predicate answers Legal for each, on this build's rule, on the world it is issued in.
    /// </summary>
    private static Recording Record(WorldgenConfig colonization, Plan? plan, SettlementId first = default, SettlementId second = default)
    {
        SimConfig production = TestFog.PreDiscovery;
        WorldgenConfig wg = UiSession.ProductionWorldgen();
        WorldState w = UiFounding.Found(42);
        var log = new OrderLog();
        TurnExecutor exec = Production(colonization, log);
        SettlementId a = plan?.First ?? first, b = plan?.Second ?? second;
        int siteA = -1, siteB = -1;
        var worlds = new List<WorldState> { w };
        for (int t = 0; t < Turns; t++)
        {
            SettlementSiting.FrontierSiting f = FoundingQuery.Frontier(w, production, wg)!;
            if (t == 2 || t == 3)
            {
                SettlementId source = t == 2 ? a : b;
                int site = (t == 2 ? plan?.FirstSite : plan?.SecondSite)
                    ?? AiFoundingPolicy.BestSiteFor(w, production, f, w.Terrain!, new AiFoundingPolicy.Sites(w, f), Me, source);
                Assert.Equal(FoundingVerdict.Legal, FoundingQuery.Check(w, production, f, Me, source, site));
                log.Append(FoundingQuery.FoundOrder(w, Me, source, site));
                if (t == 2) siteA = site; else siteB = site;
            }
            if (t == OrderTurn) foreach (OrderRecord o in SectorOrderFactory.Create(t, Me, new SettlementId(12), NoBuilders)) log.Append(o);
            w = exec.Step(w);
            worlds.Add(w);
        }
        return new Recording(log, worlds, new Plan(a, siteA, b, siteB));
    }

    /// <summary>This build's game: the least and the most populous turn-0 settlements found a colony each, the
    /// lower-scored of the two sites first (so that a stricter floor refuses the first and keeps the second).</summary>
    private static Recording ThisBuildsGame()
    {
        WorldState w = UiFounding.Found(42);
        SettlementId small = w.Settlements[0].Id, big = w.Settlements[0].Id;
        for (int i = 1; i < w.Settlements.Count; i++)
        {
            SettlementId s = w.Settlements[i].Id;
            if (Population(w, s) < Population(w, small)) small = s;
            if (Population(w, s) > Population(w, big)) big = s;
        }
        double[] scores = FoundingQuery.Frontier(w, TestFog.PreDiscovery, UiSession.ProductionWorldgen())!.Scores;
        // The best site goes to the first order, so the natural game founds the higher-scored site first: the two sites
        // trade places (each was legal beside the other, so each is legal with the other taken).
        Plan natural = Record(UiSession.ProductionWorldgen(), null, small, big).Plan;
        Recording r = Record(UiSession.ProductionWorldgen(), new Plan(small, natural.SecondSite, big, natural.FirstSite));
        Assert.True(scores[r.Plan.FirstSite] < scores[r.Plan.SecondSite], "the rig needs two sites of different scores");
        return r;
    }

    /// <summary>Another build's siting rule (worldgen.json siting.scoreFloorPercentile, a TUNE number): the floor raised to
    /// the second site's score, so the first site — eligible under this build's floor — is a PoorSite there.</summary>
    private static WorldgenConfig OtherBuild(Plan plan)
    {
        WorldgenConfig wg = UiSession.ProductionWorldgen();
        WorldState w = UiFounding.Found(42);
        double[] scores = FoundingQuery.Frontier(w, TestFog.PreDiscovery, wg)!.Scores;
        var positive = new List<double>();
        foreach (double v in scores) if (v > 0.0) positive.Add(v);
        positive.Sort();
        int at = positive.IndexOf(scores[plan.SecondSite]);
        WorldgenConfig other = wg with { Siting = wg.Siting with { ScoreFloorPercentile = (at + 0.5) / positive.Count } };
        SettlementSiting.FrontierSiting f = FoundingQuery.Frontier(w, TestFog.PreDiscovery, other)!;
        Assert.False(SettlementSiting.IsFrontierCandidate(f, w.Terrain!, plan.FirstSite));
        Assert.True(SettlementSiting.IsFrontierCandidate(f, w.Terrain!, plan.SecondSite));
        return other;
    }

    private static string WriteSession(string dir, Recording r, int schema, Func<WorldState, string> hash)
    {
        string log = Path.Combine(dir, "orders-20261018-120000.bin");
        using (FileStream o = File.Create(log)) r.Log.Save(o);
        int grain = TestFog.PreDiscovery.Goods!.GrainId;
        var lines = new List<string> { SessionTrace.Header };
        foreach (WorldState w in r.Worlds)
        {
            string[] cols = SessionTrace.Line(w, grain).Split(',');
            cols[^1] = hash(w);
            lines.Add(string.Join(',', cols));
        }
        File.WriteAllText(UiSession.TracePath(log), string.Join("\n", lines) + "\n");
        File.WriteAllText(UiSession.ChroniclePath(log), "the chronicle as the older build wrote it\n");
        SessionManifest m = UiSession.StartWith(TestFog.PreDiscovery, 42).Manifest(StartedAt, log);
        using (FileStream o = File.Create(UiSession.ManifestPath(log))) (m with { SchemaVersion = schema, BuildSha = "older-build" }).Write(o);
        return log;
    }

    private static string TempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "d057-identity-ui-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public void ARecordedOrderForAColony_WhoseIdNamesAnotherColonyInTheReplay_IsRefusedBeforeItIsApplied_AndNoFileChanges()
    {
        // This build's game picks the plan; the same orders are then recorded under the other build's rule.
        Plan plan = ThisBuildsGame().Plan;
        Recording rec = Record(OtherBuild(plan), plan);

        // As recorded: the first colony never came (a PoorSite there); settlement 12 is the second, and the turn-5 order
        // set ITS sectors.
        Assert.Equal(-1, SettlementAt(rec.Worlds[Turns], plan.FirstSite));
        Assert.Equal(12, SettlementAt(rec.Worlds[Turns], plan.SecondSite));
        Assert.Equal(new[] { 12 }, NoBuilderSettlements(rec.Worlds[Turns]));
        Assert.True(EmpireQuery.ControlsSettlement(rec.Worlds[Turns], Me, new SettlementId(12)));

        // THE HAZARD (what the resume did before D-057 — a replay that checks no identity): this build founds the first
        // colony as 12 and the second as 13, and the order lands on the first, another colony the player controls.
        UiSession bare = UiSession.StartWith(TestFog.PreDiscovery, 42);
        bare.ReplayTo(rec.Log, Turns);
        Assert.Equal(12, SettlementAt(bare.World, plan.FirstSite));
        Assert.Equal(13, SettlementAt(bare.World, plan.SecondSite));
        Assert.Equal(new[] { 12 }, NoBuilderSettlements(bare.World));
        Assert.Equal(plan.FirstSite, CellOf(bare.World, 12));
        Assert.True(EmpireQuery.ControlsSettlement(bare.World, Me, new SettlementId(12)));

        string dir = TempDir();
        try
        {
            string log = WriteSession(dir, rec, 33, w => WorldHash.ComputeHexAsSchema(w, 33));
            string[] files = Directory.GetFiles(dir).Order(StringComparer.Ordinal).ToArray();
            Assert.Equal(4, files.Length);   // the manifest, the log, the trace and the chronicle
            byte[][] before = files.Select(File.ReadAllBytes).ToArray();

            var e = Assert.Throws<OrderValidationException>(() => UiSession.Resume(UiSession.ManifestPath(log), out _, TestFog.PreDiscovery));
            Assert.Contains("the orders delivered on turn 5 include SectorAllocation (issued by polity 1) for settlement 12, "
                + "founded at turn 3 in this replay", e.Message);
            Assert.Contains("This replay matches the recorded game (its saved trace) through turn 2 and differs from turn 3 on", e.Message);
            Assert.Contains("the session is refused rather than the order applied", e.Message);
            Assert.Contains("Nothing was applied and no session file was changed", e.Message);
            Assert.Contains("open it with the build that recorded it (build older-build, ", e.Message);

            // The launch: one report, exit 2, the dialog once — and every file as it was, no copy made.
            var stderr = new StringWriter();
            var shown = new List<string>();
            Assert.Null(ResumeLaunch.Attempt(UiSession.ManifestPath(log), stderr, (_, text) => shown.Add(text), out _, out int code));
            Assert.Equal(ResumeLaunch.OrderLogRejectedExitCode, code);
            Assert.StartsWith("error: saved session " + Path.GetFileName(UiSession.ManifestPath(log)) + ": the orders delivered on turn 5",
                stderr.ToString());
            Assert.Contains("The session files were not changed.", Assert.Single(shown));
            Assert.Equal(files, Directory.GetFiles(dir).Order(StringComparer.Ordinal).ToArray());
            for (int i = 0; i < files.Length; i++) Assert.Equal(before[i], File.ReadAllBytes(files[i]));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Theory]
    [InlineData(33)]
    [InlineData(CanonicalSchema.Version)]
    public void TheSameGameRecordedUnderThisBuildsRule_Resumes_AndTheOrderLandsOnTheColonyItWasGivenFor(int schema)
    {
        Recording rec = ThisBuildsGame();
        Assert.Equal(12, SettlementAt(rec.Worlds[Turns], rec.Plan.FirstSite));
        Assert.Equal(new[] { 12 }, NoBuilderSettlements(rec.Worlds[Turns]));
        string dir = TempDir();
        try
        {
            string log = WriteSession(dir, rec, schema, w => WorldHash.ComputeHexAsSchema(w, schema));
            UiSession resumed = UiSession.Resume(UiSession.ManifestPath(log), out _, TestFog.PreDiscovery);
            Assert.Equal((long)Turns, resumed.World.Clock.Turn);
            Assert.Equal(WorldHash.ComputeHex(rec.Worlds[Turns]), WorldHash.ComputeHex(resumed.World));
            Assert.Equal(rec.Plan.FirstSite, CellOf(resumed.World, NoBuilderSettlements(resumed.World).Single()));
            if (schema == CanonicalSchema.Version) Assert.Empty(resumed.ResumeNotice);
            else Assert.Equal(UiSession.SchemaMismatchNotice(schema, CanonicalSchema.Version), resumed.ResumeNotice);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void ASessionWithoutItsTrace_WhoseOrdersNameASettlement_IsRefused_AndTheRemedyNamesTheMissingFile()
    {
        Recording rec = ThisBuildsGame();
        string dir = TempDir();
        try
        {
            string log = WriteSession(dir, rec, CanonicalSchema.Version, WorldHash.ComputeHex);
            File.Delete(UiSession.TracePath(log));
            string[] files = Directory.GetFiles(dir).Order(StringComparer.Ordinal).ToArray();
            byte[][] before = files.Select(File.ReadAllBytes).ToArray();
            var e = Assert.Throws<OrderValidationException>(() => UiSession.Resume(UiSession.ManifestPath(log), out _, TestFog.PreDiscovery));
            Assert.Contains("the orders delivered on turn 2 include FoundSettlement (issued by polity 1) for settlement", e.Message);
            Assert.Contains("The session has no trace to show that with", e.Message);
            Assert.Contains("put its trace file (" + Path.GetFileName(UiSession.TracePath(log)) + ") back beside the manifest", e.Message);
            Assert.Equal(files, Directory.GetFiles(dir).Order(StringComparer.Ordinal).ToArray());
            for (int i = 0; i < files.Length; i++) Assert.Equal(before[i], File.ReadAllBytes(files[i]));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
