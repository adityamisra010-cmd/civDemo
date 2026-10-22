using System.Globalization;
using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Colonization;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Kernel;

/// <summary>
/// D-057 (2026-10-18) — THE IDENTITY PROOF (<see cref="ReplayProof"/>): a recorded order is delivered only when every
/// settlement and polity it names is shown to be the one it was given for. Pinned here:
/// <list type="bullet">
/// <item>the proof itself, on hand-built worlds: the addressed settlement of every kind; the bounds of the last matching
///   turn; an id founded after the first difference refused (and the diagnostic); a world that matches again proving its
///   ids; the founded roster proven by construction and a revolt-born polity only like a colony; an id the world does not
///   hold left to ADR-034's delivery check; no trace (nothing proven but the roster); a row missing from the trace (no
///   difference); the recorded schema's form first and every readable form after; kind 11 still held to its own turn;
///   a trace's repeated turn read by its first row;</item>
/// <item>the CLI, each path in its own process, on a real drift — the game Sim.Ui.Tests' IdentityDriftResumeTests records
///   (two of the player's colonies founded in the other order under another build's siting floor, so the recorded order
///   for colony 12 names the other colony here): `sim inspect` refuses it with nothing written, and reports the same game
///   recorded under this build's rule as reproduced in the v33 form; every bare-log runner refuses ANOTHER SCHEMA's session
///   log before writing anything, and replays this schema's as this build's own run.</item>
/// </list>
/// </summary>
public class IdentityDriftGuardTests
{
    // ================================================================== the proof, on hand-built worlds

    private static WorldState World(long turn, int[] cells, int polities = 3, int seed = 7)
    {
        var w = new WorldState((ulong)seed) { Clock = new SimClock(turn, turn * 3600, 3600) };
        for (int i = 0; i < cells.Length; i++) w.Settlements.Add(new SettlementRow(new SettlementId(i), cells[i], i < 3 ? 0 : turn));
        for (int p = 1; p <= polities; p++) w.Polities.Add(new PolityRow(new PolityId(p), p == 1 ? CommandSource.Player : CommandSource.Ai));
        return w;
    }

    private static SessionTrace.Row Row(WorldState w) => new(w.Clock.Turn, 0, 0, w.Settlements.Count, 0, WorldHash.ComputeHex(w));

    /// <summary>One turn's batch, as a log delivers it (every order stamped the same turn).</summary>
    private static OrderBatch Batch(params OrderRecord[] orders)
    {
        if (orders.Length == 0) return OrderBatch.Empty;
        var log = new OrderLog();
        foreach (OrderRecord o in orders) log.Append(o);
        return log.BatchFor(orders[0].Turn);
    }

    private static OrderRecord Labour(long turn, int settlement, int actor = 1) =>
        new(turn, actor, OrderKind.LaborAllocation, settlement, 50.0);

    private static ReplayProof Proof(IReadOnlyList<SessionTrace.Row>? trace, int schema = CanonicalSchema.Version) =>
        new(trace, schema, foundedRosterMax: 3, "session S", "build B, 2026-10-15, schema v33");

    [Fact]
    public void AddressedSettlement_IsTheTargetOfEverySettlementKind_KindThreeDecoded_AndNoneForTheRest()
    {
        Assert.Equal(5, ReplayProof.AddressedSettlement(new OrderRecord(1, 1, OrderKind.LaborAllocation, 5, 50)));
        Assert.Equal(5, ReplayProof.AddressedSettlement(new OrderRecord(1, 1, OrderKind.SectorAllocation, (5 << 3) | 4, 0)));
        Assert.Equal(5, ReplayProof.AddressedSettlement(new OrderRecord(1, 1, OrderKind.EnqueueConstruction, 5, 1)));
        Assert.Equal(5, ReplayProof.AddressedSettlement(new OrderRecord(1, 1, OrderKind.FoundSettlement, 5, 1234)));
        Assert.Equal(5, ReplayProof.AddressedSettlement(new OrderRecord(1, 1, OrderKind.CancelConstruction, 5, 0)));
        Assert.Equal(5, ReplayProof.AddressedSettlement(new OrderRecord(1, 1, OrderKind.CancelConstructionEntry, 5, 0)));
        foreach (OrderKind none in new[] { OrderKind.SetRainBias, OrderKind.SetTaxRate, OrderKind.SetResearchTarget,
                     OrderKind.AdvanceAge, OrderKind.DevelopRoads, OrderKind.SetResourceAllocation })
            Assert.Equal(-1, ReplayProof.AddressedSettlement(new OrderRecord(1, 1, none, 5, 0)));
    }

    /// <summary>Recorded: settlements 0–2 at turn 0, 3 founded at turn 2, 4 at turn 3 on cell 400. Replayed: the same
    /// through turn 2, then 4 founded on cell 999 — another colony with the recorded one's id.</summary>
    private static (WorldState[] Recorded, WorldState[] Replayed) Drift()
    {
        WorldState[] rec = [World(0, [10, 20, 30]), World(1, [10, 20, 30]), World(2, [10, 20, 30, 40]), World(3, [10, 20, 30, 40, 400])];
        WorldState[] rep = [rec[0], rec[1], rec[2], World(3, [10, 20, 30, 40, 999])];
        return (rec, rep);
    }

    [Fact]
    public void ProvenIds_AreThoseOfTheLastMatchingTurn_AndAColonyFoundedAfterTheFirstDifferenceIsRefused()
    {
        (WorldState[] rec, WorldState[] rep) = Drift();
        ReplayProof proof = Proof(rec.Select(Row).ToArray());
        for (long t = 0; t < 3; t++) proof.BeforeDelivery(Batch(Labour(t, 0)), rep[t]);
        Assert.Equal((2L, -1L, 3), (proof.ProvenTurn, proof.FirstDifference, proof.ProvenSettlementMax));
        proof.BeforeDelivery(Batch(Labour(3, 3), Labour(3, 0)), rep[3]);   // 3 was founded at turn 2: proven
        Assert.Equal((2L, 3L), (proof.ProvenTurn, proof.FirstDifference));
        var e = Assert.Throws<OrderValidationException>(() => proof.BeforeDelivery(Batch(Labour(3, 0), Labour(3, 4)), rep[3]));
        Assert.Equal(
            "session S: the orders delivered on turn 3 include LaborAllocation (issued by polity 1) for settlement 4, founded at " +
            "turn 3 in this replay. A settlement's id is the next free one when it is founded, so id 4 names the settlement the " +
            "order was given for only if this replay is the recorded game up to that founding. This replay matches the recorded " +
            "game (its saved trace) through turn 2 and differs from turn 3 on, so the order could land on a different settlement " +
            "and the session is refused rather than the order applied. Nothing was applied and no session file was changed. To " +
            "continue this session, open it with the build that recorded it (build B, 2026-10-15, schema v33); to play this " +
            "build, start a new game.", e.Message);
    }

    [Fact]
    public void AWorldThatMatchesAgain_ProvesTheIdsItHolds_AndOnlyThen()
    {
        (WorldState[] rec, WorldState[] rep) = Drift();
        WorldState rec4 = World(4, [10, 20, 30, 40, 999, 500]);
        ReplayProof proof = Proof([.. rec.Select(Row), Row(rec4)]);
        for (long t = 0; t < 4; t++) proof.BeforeDelivery(Batch(), rep[t]);
        Assert.Equal((2L, 3L), (proof.ProvenTurn, proof.FirstDifference));
        WorldState other4 = World(4, [10, 20, 30, 40, 999, 501]);
        Assert.Throws<OrderValidationException>(() => proof.BeforeDelivery(Batch(Labour(4, 5)), other4));
        proof.BeforeDelivery(Batch(Labour(4, 5)), rec4);   // this world IS the recorded turn 4: 4 and 5 are proven
        Assert.Equal((4L, 5), (proof.ProvenTurn, proof.ProvenSettlementMax));
    }

    [Fact]
    public void TheFoundedRoster_IsProvenByConstruction_ARevoltBornPolity_OnlyLikeAColony()
    {
        (WorldState[] rec, WorldState[] rep) = Drift();
        WorldState drifted = rep[3].Clone();
        drifted.Polities.Add(new PolityRow(new PolityId(4), CommandSource.Ai));
        ReplayProof proof = Proof(rec.Select(Row).ToArray());
        for (long t = 0; t < 3; t++) proof.BeforeDelivery(Batch(), rep[t]);
        var research = new OrderRecord(3, 3, OrderKind.SetResearchTarget, 17, 0);
        proof.BeforeDelivery(Batch(research, Labour(3, 0, actor: 2)), drifted);   // AI Empire 3 and 2: founded with the world
        var e = Assert.Throws<OrderValidationException>(() =>
            proof.BeforeDelivery(Batch(new OrderRecord(3, 4, OrderKind.SetResearchTarget, 17, 0)), drifted));
        Assert.Contains("include SetResearchTarget issued by polity 4, which a revolt founded (the founding gave polities 1 to 3)", e.Message);
        Assert.Contains("so the order could land on a different polity", e.Message);
    }

    [Fact]
    public void AnIdTheWorldDoesNotHold_IsLeftToTheDeliveryCheck_WhichRefusesIt()
    {
        (WorldState[] rec, WorldState[] rep) = Drift();
        ReplayProof proof = Proof(rec.Select(Row).ToArray());
        for (long t = 0; t < 3; t++) proof.BeforeDelivery(Batch(), rep[t]);
        OrderBatch ghost = Batch(Labour(3, 9), new OrderRecord(3, 8, OrderKind.SetResearchTarget, 17, 0));
        proof.BeforeDelivery(ghost, rep[3]);   // settlement 9 and polity 8 name nothing here: nothing to misapply to
        Assert.Throws<OrderValidationException>(() => OrderValidation.ValidateAtDelivery(ghost, rep[0], rep[3]));
    }

    [Fact]
    public void WithNoTrace_NothingButTheRosterIsProven()
    {
        WorldState w = World(0, [10, 20, 30]);
        ReplayProof proof = Proof(null);
        proof.BeforeDelivery(Batch(new OrderRecord(0, 1, OrderKind.SetTaxRate, 1, 10), new OrderRecord(0, 2, OrderKind.SetResearchTarget, 17, 0)), w);
        var e = Assert.Throws<OrderValidationException>(() => proof.BeforeDelivery(Batch(Labour(0, 0)), w));
        Assert.Contains("for settlement 0, founded at turn 0 in this replay", e.Message);
        Assert.Contains("The session has no trace to show that with, so the order could land on a different settlement", e.Message);
        Assert.Equal((-1L, -1L, -1), (proof.ProvenTurn, proof.FirstDifference, proof.ProvenSettlementMax));
    }

    [Fact]
    public void ARowMissingFromTheTrace_IsNoDifference_AndADifferentFoundingRefusesEveryTurnZeroSettlement()
    {
        (WorldState[] rec, WorldState[] rep) = Drift();
        ReplayProof holes = Proof([Row(rec[0]), Row(rec[2])]);
        for (long t = 0; t < 3; t++) holes.BeforeDelivery(Batch(Labour(t, 0)), rep[t]);
        Assert.Equal((2L, -1L, 3), (holes.ProvenTurn, holes.FirstDifference, holes.ProvenSettlementMax));

        ReplayProof refounded = Proof(rec.Select(Row).ToArray());
        WorldState otherFounding = World(0, [11, 20, 30]);
        var e = Assert.Throws<OrderValidationException>(() => refounded.BeforeDelivery(Batch(Labour(0, 1)), otherFounding));
        Assert.Contains("This replay differs from the recorded game from its founding on (turn 0 does not match the saved trace)", e.Message);
    }

    [Fact]
    public void TheRecordedSchemasFormIsTriedFirst_AndEveryReadableFormProves()
    {
        (WorldState[] rec, WorldState[] rep) = Drift();
        foreach ((int schema, int form) in new[] { (33, 33), (32, 32), (32, CanonicalSchema.Version), (CanonicalSchema.Version, 33) })
        {
            ReplayProof proof = Proof(rec.Select(w => Row(w) with { Hash = WorldHash.ComputeHexAsSchema(w, form) }).ToArray(), schema);
            for (long t = 0; t < 3; t++) proof.BeforeDelivery(Batch(Labour(t, 0)), rep[t]);
            Assert.Equal((2L, -1L), (proof.ProvenTurn, proof.FirstDifference));
        }
    }

    [Fact]
    public void AKind11Batch_StillNeedsTheWorldItIsDeliveredTo_EvenForAProvenSettlement()
    {
        (WorldState[] rec, WorldState[] rep) = Drift();
        ReplayProof proof = Proof(rec.Select(Row).ToArray());
        for (long t = 0; t < 3; t++) proof.BeforeDelivery(Batch(), rep[t]);
        var e = Assert.Throws<OrderValidationException>(() =>
            proof.BeforeDelivery(Batch(new OrderRecord(3, 1, OrderKind.CancelConstruction, 0, 0)), rep[3]));
        Assert.Contains("include 1 legacy slot-only cancel order", e.Message);
        Assert.Contains("does not reproduce that world at turn 3", e.Message);
        proof = Proof(rec.Select(Row).ToArray());
        proof.BeforeDelivery(Batch(new OrderRecord(0, 1, OrderKind.CancelConstruction, 0, 0)), rep[0]);   // the recorded world
    }

    [Fact]
    public void ATracesRepeatedTurn_IsReadByItsFirstRow_InEveryOrderTheRowsComeIn()
    {
        (WorldState[] rec, WorldState[] rep) = Drift();
        SessionTrace.Row bad = Row(rec[1]) with { Hash = new string('0', 64) };
        foreach (SessionTrace.Row[] rows in new[]
                 {
                     new[] { Row(rec[0]), Row(rec[1]), bad, Row(rec[2]) },
                     new[] { Row(rec[2]), Row(rec[1]), Row(rec[0]), bad },
                 })
        {
            ReplayProof proof = Proof(rows);
            for (long t = 0; t < 3; t++) proof.BeforeDelivery(Batch(Labour(t, 0)), rep[t]);
            Assert.Equal((2L, -1L), (proof.ProvenTurn, proof.FirstDifference));
        }
        ReplayProof badFirst = Proof([Row(rec[0]), bad, Row(rec[1]), Row(rec[2])]);
        for (long t = 0; t < 3; t++) badFirst.BeforeDelivery(Batch(), rep[t]);
        Assert.Equal((0L, 1L), (badFirst.ProvenTurn, badFirst.FirstDifference));
    }

    // ================================================================== the CLI, on a real drift

    private static readonly TimeSpan Limit = TimeSpan.FromMinutes(6);
    private static readonly PolityId Me = new(1);
    private static readonly int[] NoBuilders = [55, 15, 10, 12, 0];
    private const int OrderTurn = 5, Turns = 7;
    private const string Stamp = "20261018-120000";

    private sealed record Plan(SettlementId First, int FirstSite, SettlementId Second, int SecondSite);

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

    /// <summary>The game of IdentityDriftResumeTests, headless: turn 2 a colony from <c>First</c>, turn 3 one from
    /// <c>Second</c>, turn 5 the player sets settlement 12's sectors (no builders) — recorded with
    /// <paramref name="colonization"/> as ColonizationSystem's siting rule. Sites not given are this build's best for the
    /// source when the order is issued; each is Legal on this build's rule.
    /// M6 Track EX (2026-10-22): <paramref name="rules"/> — default the shipped config with the discovery layer OFF
    /// (TestConfigs.PreDiscovery): the sessions here are labelled v33, and a pre-v35 session is replayed without the discovery
    /// layer (DiscoveryQuery.RecordedWithoutDiscovery), as it was played. Under the shipped fog (rules with fog on) the
    /// player first explores around both sources at turns 0 and 1 (OrderKind 14), so the sites it founds on are KNOWN.</summary>
    private static (OrderLog Log, List<WorldState> Worlds, Plan Plan) Record(WorldgenConfig colonization, Plan? plan,
        SettlementId first = default, SettlementId second = default, SimConfig? rules = null)
    {
        SimConfig cfg = rules ?? TestConfigs.PreDiscovery(TestConfigs.Sim());
        WorldgenConfig wg = TestConfigs.Worldgen();
        WorldState w = Sim.Cli.HeadlessFounding.Found(42);
        var log = new OrderLog();
        TurnExecutor exec = CylinderAttributionRig.Pipeline(cfg, colonization, log);
        SettlementId a = plan?.First ?? first, b = plan?.Second ?? second;
        int siteA = -1, siteB = -1;
        var worlds = new List<WorldState> { w };
        for (int t = 0; t < Turns; t++)
        {
            SettlementSiting.FrontierSiting f = FoundingQuery.Frontier(w, cfg, wg)!;
            if (t <= 1 && DiscoveryQuery.FogOn(cfg))   // two expeditions each (radius 3 -> 9 nodes) before the foundings
                foreach (SettlementId s in a == b ? new[] { a } : new[] { a, b })
                    log.Append(new OrderRecord(t, Me.Value, OrderKind.Explore, s.Value, 0.0));
            if (t == 2 || t == 3)
            {
                SettlementId source = t == 2 ? a : b;
                int site = (t == 2 ? plan?.FirstSite : plan?.SecondSite)
                    ?? AiFoundingPolicy.BestSiteFor(w, cfg, f, w.Terrain!, new AiFoundingPolicy.Sites(w, f), Me, source);
                Assert.Equal(FoundingVerdict.Legal, FoundingQuery.Check(w, cfg, f, Me, source, site));
                log.Append(FoundingQuery.FoundOrder(w, Me, source, site));
                if (t == 2) siteA = site; else siteB = site;
            }
            if (t == OrderTurn)
                for (int s = 0; s < Sectors.Count; s++)
                    log.Append(new OrderRecord(t, Me.Value, OrderKind.SectorAllocation, (12 << 3) | s, NoBuilders[s]));
            w = exec.Step(w);
            worlds.Add(w);
        }
        return (log, worlds, new Plan(a, siteA, b, siteB));
    }

    private static (OrderLog Log, List<WorldState> Worlds, Plan Plan) ThisBuildsGame(SimConfig? rules = null)
    {
        WorldState w = Sim.Cli.HeadlessFounding.Found(42);
        SettlementId small = w.Settlements[0].Id, big = w.Settlements[0].Id;
        for (int i = 1; i < w.Settlements.Count; i++)
        {
            SettlementId s = w.Settlements[i].Id;
            if (Population(w, s) < Population(w, small)) small = s;
            if (Population(w, s) > Population(w, big)) big = s;
        }
        Plan natural = Record(TestConfigs.Worldgen(), null, small, big, rules).Plan;
        return Record(TestConfigs.Worldgen(), new Plan(small, natural.SecondSite, big, natural.FirstSite), rules: rules);
    }

    private static WorldgenConfig OtherBuild(Plan plan)
    {
        WorldgenConfig wg = TestConfigs.Worldgen();
        WorldState w = Sim.Cli.HeadlessFounding.Found(42);
        double[] scores = FoundingQuery.Frontier(w, TestConfigs.Sim(), wg)!.Scores;
        Assert.True(scores[plan.FirstSite] < scores[plan.SecondSite], "the rig needs two sites of different scores");
        var positive = new List<double>();
        foreach (double v in scores) if (v > 0.0) positive.Add(v);
        positive.Sort();
        int at = positive.IndexOf(scores[plan.SecondSite]);
        return wg with { Siting = wg.Siting with { ScoreFloorPercentile = (at + 0.5) / positive.Count } };
    }

    private static string TempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "d057-identity-cli-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>The session record as an older build left it: the log, the trace of <paramref name="worlds"/> hashed in
    /// <paramref name="schema"/>'s form, a manifest labelled <paramref name="schema"/>. Returns the manifest's path.</summary>
    private static string Session(string dir, OrderLog log, List<WorldState> worlds, int schema)
    {
        using (FileStream f = File.Create(Path.Combine(dir, "orders-" + Stamp + ".bin"))) log.Save(f);
        var lines = new List<string> { SessionTrace.Header };
        foreach (WorldState w in worlds)
        {
            string[] cols = SessionTrace.Line(w, TestConfigs.Sim().Goods!.GrainId).Split(',');
            cols[^1] = WorldHash.ComputeHexAsSchema(w, schema);
            lines.Add(string.Join(',', cols));
        }
        File.WriteAllText(Path.Combine(dir, "trace-" + Stamp + ".csv"), string.Join("\n", lines) + "\n");
        var manifest = new SessionManifest(42, null, null, schema, "older-build", "2026-10-15", "2026-10-18 12:00:00",
            "orders-" + Stamp + ".bin", "chronicle-" + Stamp + ".txt", "trace-" + Stamp + ".csv");
        string path = Path.Combine(dir, "session-" + Stamp + ".json");
        using (FileStream f = File.Create(path)) manifest.Write(f);
        return path;
    }

    private static (string[] Files, byte[][] Bytes) Contents(string dir)
    {
        string[] files = Directory.GetFileSystemEntries(dir).Order(StringComparer.Ordinal).ToArray();
        return (files, files.Select(f => File.Exists(f) ? File.ReadAllBytes(f) : []).ToArray());
    }

    private static void AssertUnchanged(string dir, (string[] Files, byte[][] Bytes) before)
    {
        (string[] files, byte[][] bytes) = Contents(dir);
        Assert.Equal(before.Files, files);
        for (int i = 0; i < files.Length; i++) Assert.Equal(before.Bytes[i], bytes[i]);
    }

    [Fact]
    public void Inspect_RefusesTheDriftedColonySession_AtTheOrderItCannotProve_WithNothingWritten()
    {
        Plan plan = ThisBuildsGame().Plan;
        (OrderLog log, List<WorldState> recorded, _) = Record(OtherBuild(plan), plan);
        Assert.Equal(-1, SettlementAt(recorded[Turns], plan.FirstSite));       // premise: as recorded, 12 is the second colony
        Assert.Equal(12, SettlementAt(recorded[Turns], plan.SecondSite));
        string dir = TempDir();
        try
        {
            string manifest = Session(dir, log, recorded, 33);
            var before = Contents(dir);
            string report = Path.Combine(dir, "r.jsonl"), telemetry = Path.Combine(dir, "t.jsonl");
            (int exit, _, string err) = CliProcess.Run(Limit, "inspect", "--manifest", manifest, "--report-jsonl", report, "--telemetry", telemetry);
            Assert.True(exit == 2, $"expected exit 2, got {exit}: {err}");
            Assert.Contains("the orders delivered on turn 5 include SectorAllocation (issued by polity 1) for settlement 12, founded at turn 3", err);
            Assert.Contains("This replay matches the recorded game (its saved trace) through turn 2 and differs from turn 3 on", err);
            Assert.Contains("Nothing was replayed past that batch and no report file was written", err);
            AssertUnchanged(dir, before);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Inspect_OfTheSameGameUnderThisBuildsRule_ReproducesItInTheV33Form()
    {
        (OrderLog log, List<WorldState> worlds, _) = ThisBuildsGame();
        string dir = TempDir();
        try
        {
            string manifest = Session(dir, log, worlds, 33);
            (int exit, string stdout, string err) = CliProcess.Run(Limit, "inspect", "--manifest", manifest);
            Assert.True(exit == 0, err);
            Assert.Contains(Sim.Cli.SessionSchemaNotice.CheckedInRecordedForm(33, Turns, -1, Turns), CliProcess.Lines(stdout));
            Assert.Contains("the replay reproduces the recorded game through turn 7", stdout);
            Assert.DoesNotContain("no replayed hash can equal a recorded one", stdout);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void EveryBareLogRunner_RefusesAnotherSchemasSessionLog_BeforeWritingAnything_AndReplaysThisSchemasAsItsOwnRun()
    {
        (OrderLog log, List<WorldState> worlds, _) = ThisBuildsGame();
        string dir = TempDir();
        try
        {
            string manifest = Session(dir, log, worlds, 33);
            string orders = Path.Combine(dir, "orders-" + Stamp + ".bin");
            string save2 = Path.Combine(dir, "s2.civsnap");
            Assert.Equal(0, CliProcess.Run(Limit, "run", "--founded", "--seed", "42", "--turns", "2", "--save-at", "2", "--save", save2).Exit);
            var before = Contents(dir);
            string save = Path.Combine(dir, "out.civsnap"), hashes = Path.Combine(dir, "h.log"), emitted = Path.Combine(dir, "emitted");
            string report = Path.Combine(dir, "r.jsonl"), trace = Path.Combine(dir, "t.csv");
            string[][] runners =
            [
                ["run", "--founded", "--seed", "42", "--turns", "7", "--orders", orders, "--save-at", "7", "--save", save, "--hash-log", hashes],
                ["run", "--founded", "--seed", "42", "--turns", "7", "--orders", orders, "--emit-session", emitted],
                ["replay", "--founded", "--seed", "42", "--turns", "7", "--orders", orders, "--hash-log", hashes, "--report-jsonl", report],
                ["replay", "--founded", "--seed", "42", "--turns", "7", "--orders", orders, "--emit-session", emitted],
                ["run", "--from-save", save2, "--turns", "5", "--orders", orders, "--save-at", "7", "--save", save],
                ["research", "--seed", "42", "--turns", "7", "--orders", orders, "--trace-population", trace],
            ];
            foreach (string[] runner in runners)
            {
                (int exit, _, string err) = CliProcess.Run(Limit, runner);
                Assert.True(exit == 2, $"{string.Join(' ', runner)}: expected exit 2, got {exit}: {err}");
                Assert.Contains("is the log of session session-" + Stamp + ".json, recorded under save schema v33 (this build is v"
                    + CanonicalSchema.Version.ToString(CultureInfo.InvariantCulture) + ")", err);
                Assert.Contains("Nothing was replayed and no file was written. Inspect the session with `sim inspect --manifest", err);
                AssertUnchanged(dir, before);
            }

            // The same game, recorded by THIS build (the shipped rules — M6 EX: with the discovery layer on, so it explores
            // first) and labelled this build's schema: its log is replayed as this build's run (the ci.yml pattern).
            (OrderLog own, List<WorldState> ownWorlds, _) = ThisBuildsGame(TestConfigs.Sim());
            File.Delete(manifest);
            Session(dir, own, ownWorlds, CanonicalSchema.Version);
            (int ok, string stdout, string okErr) = CliProcess.Run(Limit, "replay", "--founded", "--seed", "42", "--turns", "7", "--orders", orders);
            Assert.True(ok == 0, okErr);
            Assert.Contains("replay complete: seed 42, 7 turns, hash " + WorldHash.ComputeHex(ownWorlds[Turns]), stdout);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
