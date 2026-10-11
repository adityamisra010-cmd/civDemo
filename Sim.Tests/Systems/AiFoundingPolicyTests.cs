using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Colonization;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;
using Xunit;

namespace Sim.Tests.Systems;

/// <summary>
/// M5 S1c (ADR-036 §8; D-051 §5: "The AI uses the same order and the same predicates") — THE AI FOUNDING POLICY.
/// The AI issues FoundSettlement through the player's constructor (FoundingQuery.FoundOrder), only for sites the
/// player's predicate (FoundingQuery.Check) answers Legal for, ranked by the unjittered siting score with the composite
/// key (score DESC, cell ASC). These tests pin the ranking (tie-dense, exhaustive), the ranking against an INDEPENDENT
/// oracle that walks the cells in key order and asks THE predicate, the player/AI record and world equality, the batch
/// spacing across polities, and every trigger arm.
///
/// Terrain rigs are the dev preset (256², 4 settlements, seed 42) with one AI Empire, after one orderless production
/// step (turn 1: PREV carries catchments). <see cref="Lax"/> opens the interval and the size threshold so the rig's
/// small settlements can found; the shipped thresholds are exercised by the toy-world gate tests and the integration
/// runs.
/// </summary>
public class AiFoundingPolicyTests
{
    private static readonly PolityId Player = new(1);
    private static readonly PolityId Rival = new(2);
    private static readonly WorldgenConfig Wg = TestConfigs.Worldgen();
    private static SimConfig Cfg => FoundingRigs.Cfg();

    /// <summary>The canonical config with the AI founding thresholds opened (interval 0 years, any source size).</summary>
    private static SimConfig Lax => With(new AiFoundingConfig(Enabled: true, MinSourcePopulation: 1, MinYearsBetweenFoundings: 0.0));

    private static SimConfig With(AiFoundingConfig? ai)
    {
        SimConfig cfg = Cfg;
        return cfg with { Colonization = cfg.Colonization! with { AiFounding = ai } };
    }

    private static readonly Lazy<WorldState> DuoTurn1 = new(
        () => FoundingRigs.Pipeline(FoundingRigs.Cfg(), null).Step(FoundingRigs.Dev(42UL, aiEmpires: 1)),
        LazyThreadSafetyMode.ExecutionAndPublication);

    private static WorldState Duo1() => DuoTurn1.Value.Clone();

    private static OrderLog Log(params OrderRecord[] orders)
    {
        var log = new OrderLog();
        foreach (OrderRecord o in orders) log.Append(o);
        return log;
    }

    /// <summary>The independent oracle for "restricted to cells FoundingQuery admits, ranked (score DESC, cell ASC)":
    /// every cell with a positive score, sorted by that key with LINQ (a stable sort on (−score, cell)), asked of THE
    /// predicate in turn; the first Legal one. Shares no code with the policy's scan but the predicate.</summary>
    private static int OracleSite(IReadOnlyWorldState w, SimConfig cfg, SettlementSiting.FrontierSiting f, SettlementId source)
    {
        int[] keyed = Enumerable.Range(0, f.Scores.Length).Where(c => f.Scores[c] > 0.0)
            .OrderByDescending(c => f.Scores[c]).ThenBy(c => c).ToArray();
        foreach (int cell in keyed)
            if (FoundingQuery.Check(w, cfg, f, Rival, source, cell) == FoundingVerdict.Legal) return cell;
        return -1;
    }

    // ================================================================== THE RANKING (tie-dense)

    [Fact]
    public void BestSite_IsTheCompositeKeyArgmax_ExhaustivelyOverTieDenseScoresAndEveryAdmissionMask()
    {
        // CLAUDE.md: an argmax over doubles uses (score, id) with a stable integer tie-break and ships a tie-dense
        // test. Every length-5 vector over {0, 0.5, 1, NaN} (ties everywhere) × every admission mask: the pick is the
        // admitted, non-NaN cell of greatest score, the LOWEST such cell among equals; −1 when none is admitted.
        double[] values = [0.0, 0.5, 1.0, double.NaN];
        int checkedCases = 0;
        var scores = new double[5];
        for (int code = 0; code < 1024; code++)
        {
            for (int i = 0, c = code; i < 5; i++, c /= 4) scores[i] = values[c % 4];
            for (int mask = 0; mask < 32; mask++)
            {
                int expected = -1;
                for (int i = 0; i < 5; i++)
                {
                    if ((mask & (1 << i)) == 0 || double.IsNaN(scores[i])) continue;
                    if (expected < 0 || scores[i] > scores[expected]) expected = i;   // ascending i: ties keep the lower
                }
                int m = mask;
                Assert.Equal(expected, AiFoundingPolicy.BestSite(scores, cell => (m & (1 << cell)) != 0));
                checkedCases++;
            }
        }
        Assert.Equal(1024 * 32, checkedCases);
    }

    [Fact]
    public void BestSite_AllScoresEqual_TakesTheLowestAdmittedCell_AndAnInadmissibleImproverNeverRaisesTheBar()
    {
        double[] flat = Enumerable.Repeat(0.7, 64).ToArray();
        Assert.Equal(5, AiFoundingPolicy.BestSite(flat, c => c is 5 or 17 or 40));
        Assert.Equal(63, AiFoundingPolicy.BestSite(flat, c => c == 63));
        Assert.Equal(-1, AiFoundingPolicy.BestSite(flat, _ => false));

        // Cell 1 scores highest but is refused: the bar stays where the admitted cells put it, so cell 2 (0.5) is
        // taken over cell 3 (0.5) — the tie goes to the lower cell — and cell 0 (0.2) loses to both.
        Assert.Equal(2, AiFoundingPolicy.BestSite([0.2, 0.9, 0.5, 0.5], c => c != 1));

        // Only strict improvers are asked about (the scan's cost property): with every cell equal and the first
        // admitted, the predicate is asked exactly once.
        int asked = 0;
        Assert.Equal(0, AiFoundingPolicy.BestSite(flat, _ => { asked++; return true; }));
        Assert.Equal(1, asked);
    }

    [Fact]
    public void OnFlattenedScores_TheAiPicksTheLowestCellThePredicateAdmits()
    {
        // The policy-level tie-dense case: every frontier candidate is given the SAME score, so the whole ranking is
        // decided by the integer tie-break. The pick must be the lowest cell THE predicate answers Legal for.
        WorldState w = Duo1();
        SettlementSiting.FrontierSiting real = FoundingRigs.Frontier(w);
        var scores = (double[])real.Scores.Clone();
        double level = real.Floor;                                    // the floor itself is a candidate score
        Assert.True(level > 0.0);
        int candidates = 0;
        for (int c = 0; c < scores.Length; c++)
            if (SettlementSiting.IsFrontierCandidate(real, w.Terrain!, c)) { scores[c] = level; candidates++; }
        var flat = new SettlementSiting.FrontierSiting
        {
            Lattice = real.Lattice, Scores = scores, Floor = real.Floor,
            MinSpacingCostUnits = real.MinSpacingCostUnits, Size = real.Size,
        };
        int flatCandidates = 0;
        for (int c = 0; c < scores.Length; c++) if (SettlementSiting.IsFrontierCandidate(flat, w.Terrain!, c)) flatCandidates++;
        Assert.Equal(candidates, flatCandidates);                     // the same cells, every one now tied

        SettlementId source = AiFoundingPolicy.RankedSources(w, Lax, Lax.Colonization!.AiFounding!, Rival)[0];
        int pick = AiFoundingPolicy.BestSiteFor(w, flat, w.Terrain!, new AiFoundingPolicy.Sites(w, flat), source);
        int lowest = -1;
        for (int c = 0; c < scores.Length && lowest < 0; c++)
            if (FoundingQuery.Check(w, Lax, flat, Rival, source, c) == FoundingVerdict.Legal) lowest = c;
        Assert.True(lowest >= 0, "no legal cell on the flattened frontier — the rig is void");
        Assert.Equal(lowest, pick);
    }

    [Fact]
    public void TheAiOrder_IsTheFirstCellInScoreDescCellAscOrder_ThatFoundingQueryAdmits_FromTheFirstRankedSource()
    {
        WorldState w = Duo1();
        OrderRecord order = AiFoundingPolicy.Decide(w, Lax, Wg, Rival) ?? throw new InvalidOperationException("the AI issued no order — the rig is void");
        SettlementId source = AiFoundingPolicy.RankedSources(w, Lax, Lax.Colonization!.AiFounding!, Rival)[0];
        Assert.Equal(source.Value, order.TargetId);
        Assert.Equal(OracleSite(w, Lax, FoundingRigs.Frontier(w), source), FoundingQuery.SiteCellOf(order));
        Assert.Equal((w.Clock.Turn, Rival.Value, OrderKind.FoundSettlement), (order.Turn, order.ActorId, order.Kind));
    }

    [Theory]
    [InlineData("Occupied")]
    [InlineData("TooClose")]
    [InlineData("Claimed")]
    [InlineData("Unreachable")]
    public void ACellThePredicateRefuses_IsNeverPicked_EvenWhenItOutranksEveryOther(string arm)
    {
        // Each per-cell arm the scan mirrors, made to BIND: a cell THE predicate refuses for that arm is given the
        // highest score on the map. The AI's pick must pass it over for exactly the oracle's cell (the first cell in key
        // order the predicate admits) — so the scan's masks are the predicate's arms, not a looser reading of them.
        WorldState w = Duo1();
        WorldgenConfig wg = arm == "Claimed"
            ? Wg with { Siting = Wg.Siting with { MinSpacingKm = 10.0 } }   // S1b's rig: catchments outgrow spacing
            : Wg;
        SettlementSiting.FrontierSiting real = FoundingRigs.Frontier(w, Lax, wg);
        SettlementId src = AiFoundingPolicy.RankedSources(w, Lax, Lax.Colonization!.AiFounding!, Rival)[0];
        int refused = arm switch
        {
            "Occupied" => w.Settlements[w.Settlements.Count - 1].Id == src ? w.Settlements[0].SiteCell : w.Settlements[w.Settlements.Count - 1].SiteCell,
            "Unreachable" => FoundingRigs.UnreachableCandidate(w, real, src),
            "TooClose" => FoundingRigs.FirstCell(w, Lax, real, Rival, src, FoundingVerdict.TooClose, 0, 40),
            _ => FoundingRigs.FirstCell(w, Lax, real, Rival, src, FoundingVerdict.Claimed, 0, 80),
        };
        Assert.True(refused >= 0, $"no {arm} cell — the rig is void");
        Assert.Equal(Enum.Parse<FoundingVerdict>(arm), FoundingQuery.Check(w, Lax, real, Rival, src, refused));

        var scores = (double[])real.Scores.Clone();
        scores[refused] = scores.Max() + 1.0;
        var boosted = new SettlementSiting.FrontierSiting
        {
            Lattice = real.Lattice, Scores = scores, Floor = real.Floor,
            MinSpacingCostUnits = real.MinSpacingCostUnits, Size = real.Size,
        };
        Assert.Equal(Enum.Parse<FoundingVerdict>(arm), FoundingQuery.Check(w, Lax, boosted, Rival, src, refused));

        int pick = AiFoundingPolicy.BestSiteFor(w, boosted, w.Terrain!, new AiFoundingPolicy.Sites(w, boosted), src);
        Assert.NotEqual(refused, pick);
        Assert.Equal(OracleSite(w, Lax, boosted, src), pick);
        Assert.Equal(FoundingVerdict.Legal, FoundingQuery.Check(w, Lax, boosted, Rival, src, pick));
    }

    // ================================================================== SAME ORDER, SAME PREDICATE, SAME RESULT

    [Fact]
    public void AiAndPlayer_SameFactory_SamePredicate_SameRecord_SameWorld()
    {
        // The RoadDevelopmentTests AiAndPlayer_* pattern: the same world twice, polity 2 AI-commanded in one and
        // player-commanded in the other. The AI's order equals the one the player's constructor builds for the same
        // source and site; THE predicate answers the same for both; and stepping each world with its order gives the
        // same world — compared by hash after the command source is set back, so every table is covered.
        WorldState forAi = Duo1();
        WorldState forPlayer = Duo1();
        for (int i = 0; i < forPlayer.Polities.Count; i++)
            if (forPlayer.Polities[i].Id == Rival) forPlayer.Polities[i] = new PolityRow(Rival, CommandSource.Player);

        Assert.Null(AiFoundingPolicy.Decide(forPlayer, Lax, Wg, Rival));     // a player-commanded polity is never touched
        OrderRecord ai = AiFoundingPolicy.Decide(forAi, Lax, Wg, Rival) ?? throw new InvalidOperationException("no AI order — the rig is void");
        var source = new SettlementId(ai.TargetId);
        int site = FoundingQuery.SiteCellOf(ai);
        OrderRecord player = FoundingQuery.FoundOrder(forPlayer, Rival, source, site);
        Assert.Equal(ai, player);
        Assert.Equal(FoundingVerdict.Legal, FoundingQuery.Check(forAi, Lax, FoundingRigs.Frontier(forAi), Rival, source, site));
        Assert.Equal(FoundingVerdict.Legal, FoundingQuery.Check(forPlayer, Lax, FoundingRigs.Frontier(forPlayer), Rival, source, site));

        WorldState a1 = FoundingRigs.Pipeline(Lax, Log(ai)).Step(forAi);
        WorldState p1 = FoundingRigs.Pipeline(Lax, Log(player)).Step(forPlayer);
        Assert.Equal(forAi.Settlements.Count + 1, a1.Settlements.Count);
        Assert.Equal(a1.Settlements.Rows(), p1.Settlements.Rows());
        for (int i = 0; i < p1.Polities.Count; i++)
            if (p1.Polities[i].Id == Rival) p1.Polities[i] = new PolityRow(Rival, CommandSource.Ai);
        Assert.Equal(WorldHash.ComputeHex(a1), WorldHash.ComputeHex(p1));
    }

    [Fact]
    public void OverAPlayedRun_EveryAiFoundingOrderIsLegalOnTheWorldItWasIssuedFrom_AndFoundsNextTurn_AndTheLedgerBalances()
    {
        // The AI producer drives the dev world for 12 turns with the thresholds opened, so it founds whenever it can.
        // Every Found Settlement order it emits is Legal under THE predicate on the world it saw, founds a settlement
        // in the next state at exactly the ordered cell, and the conservation audit balances every turn.
        SimConfig cfg = Lax;
        var log = new OrderLog();
        TurnExecutor exec = FoundingRigs.Pipeline(cfg, log);
        WorldState w = Duo1();
        int founded = 0;
        for (int t = 0; t < 12; t++)
        {
            int before = log.Count;
            AiOrders.Append(log, w, cfg, Wg);
            var issued = new List<OrderRecord>();
            for (int i = before; i < log.Count; i++) if (log[i].Kind == OrderKind.FoundSettlement) issued.Add(log[i]);
            SettlementSiting.FrontierSiting f = FoundingRigs.Frontier(w);
            foreach (OrderRecord o in issued)
                Assert.Equal(FoundingVerdict.Legal,
                    FoundingQuery.Check(w, cfg, f, o.Actor, new SettlementId(o.TargetId), FoundingQuery.SiteCellOf(o)));

            WorldState next = exec.Step(w);
            foreach (OrderRecord o in issued)
            {
                bool there = false;
                for (int i = 0; i < next.Settlements.Count; i++)
                    if (next.Settlements[i].SiteCell == FoundingQuery.SiteCellOf(o) && next.Settlements[i].FoundedTurn == w.Clock.Turn + 1)
                        there = true;
                Assert.True(there, $"the AI order stamped {o.Turn} founded nothing at cell {FoundingQuery.SiteCellOf(o)}");
                founded++;
            }
            Assert.True(ConservationAuditor.IsConserved(next, cfg.Goods, out string report), report);
            w = next;
        }
        Assert.True(founded >= 3, $"the rig founded only {founded} times — too few to mean anything");
    }

    [Fact]
    public void TwoAiPolitiesInOneBatch_SpaceTheirSitesAgainstEachOther_AndBothFound()
    {
        // Both AI Empires rank the same global score raster, so without the shared batch state the second would pick
        // the first's site (or its exclusion) and be refused at delivery. With it, the second polity's order is
        // already spaced against the first's, and the system founds both in one step.
        WorldState w = FoundingRigs.Pipeline(Cfg, null).Step(FoundingRigs.Dev(42UL, aiEmpires: 2));
        OrderRecord[] found = AiOrders.For(w, Lax, Wg).Where(o => o.Kind == OrderKind.FoundSettlement).ToArray();
        Assert.Equal(2, found.Length);
        Assert.NotEqual(found[0].ActorId, found[1].ActorId);

        // Independently: the second order is the one the second polity would pick once the first's site stands.
        var sites = new AiFoundingPolicy.Sites(w, Lax, Wg);
        Assert.Equal(found[0], AiFoundingPolicy.Decide(w, Lax, found[0].Actor, sites));
        Assert.Equal(found[1], AiFoundingPolicy.Decide(w, Lax, found[1].Actor, sites));
        OrderRecord alone = AiFoundingPolicy.Decide(w, Lax, Wg, found[1].Actor)!.Value;   // no knowledge of the first
        Assert.NotEqual(alone.Amount, found[1].Amount);                                  // the batch state mattered here

        WorldState next = FoundingRigs.Pipeline(Lax, Log(found)).Step(w);
        Assert.Equal(w.Settlements.Count + 2, next.Settlements.Count);
    }

    // ================================================================== THE TRIGGER ARMS (toy worlds)

    /// <summary>A terrain-less toy world: polity 1 the player, polity 2 AI; settlement i controlled by
    /// controllers[i], one bucket of <paramref name="adults"/> each, and a grain store at every settlement.</summary>
    private static WorldState Toy(int[] controllers, long adults, long grain = 1_000)
    {
        WorldState w = ResearchRigs.World([1, 2], controllers, adults);
        for (int s = 0; s < controllers.Length; s++) ResearchRigs.Stock(w, s, Cfg.Goods!.GrainId, grain);
        return w;
    }

    /// <summary>Sources <paramref name="n"/> more people into the settlement's first bucket, through the Ledger.</summary>
    private static void AddPeople(WorldState w, SettlementId s, long n)
    {
        for (int i = 0; i < w.Buckets.Count; i++)
        {
            if (w.Buckets[i].Settlement != s) continue;
            new Ledger(w.LedgerFlows).Flow(ref w.Buckets.Ref(i).Count, ConservedQuantityIds.Population,
                ReasonIds.InitialEndowment, n, FlowDirection.Source, OverdrawPolicy.Throw);
            return;
        }
        throw new InvalidOperationException($"settlement {s.Value} has no bucket");
    }

    private static SettlementId[] Ranked(IReadOnlyWorldState w, AiFoundingConfig ai) =>
        AiFoundingPolicy.RankedSources(w, With(ai), ai, Rival);

    [Fact]
    public void Sources_AreRankedPopulationDescThenIdAsc_TieDense()
    {
        var ai = new AiFoundingConfig(true, 100, 0.0);
        // Every AI settlement equal: the whole order is the integer tie-break, ascending id; the player's is absent.
        WorldState tied = Toy([2, 1, 2, 2, 2], 500);
        Assert.Equal([0, 2, 3, 4], Ranked(tied, ai).Select(s => s.Value));

        // One larger settlement leads; the rest keep ascending id among themselves.
        AddPeople(tied, new SettlementId(3), 7);
        Assert.Equal([3, 0, 2, 4], Ranked(tied, ai).Select(s => s.Value));
    }

    [Fact]
    public void Sources_BelowTheSizeThreshold_Starving_OrUnableToOutfitAParty_AreNotSources()
    {
        // Size: exactly the threshold qualifies, one below does not.
        Assert.Equal([0], Ranked(Toy([2], 500), new AiFoundingConfig(true, 500, 0.0)).Select(s => s.Value));
        Assert.Empty(Ranked(Toy([2], 499), new AiFoundingConfig(true, 500, 0.0)));

        var ai = new AiFoundingConfig(true, 100, 0.0);
        // Fed: Stress (0 < d <= a) is a source; Severe (d > a) is not — nobody starving sends colonists.
        double a = FoodState.SevereThreshold(Cfg);
        WorldState stress = Toy([2], 500);
        stress.ConsumptionDeficits.Add(new ConsumptionDeficitRow(new SettlementId(0), a));
        Assert.Equal(FoodStateKind.Stress, FoodState.Of(stress, new SettlementId(0), Cfg, out _));
        Assert.Single(Ranked(stress, ai));
        WorldState severe = Toy([2], 500);
        severe.ConsumptionDeficits.Add(new ConsumptionDeficitRow(new SettlementId(0), Math.Min(1.0, a + 0.05)));
        Assert.Equal(FoodStateKind.Severe, FoodState.Of(severe, new SettlementId(0), Cfg, out _));
        Assert.Empty(Ranked(severe, ai));

        // FoundingQuery's own food arm: no grain, no provisions, no source.
        Assert.Empty(Ranked(Toy([2], 500, grain: 0), ai));
    }

    [Fact]
    public void TheInterval_IsALowerBoundInYears_OverThePolitysNewestSettlement()
    {
        var ai = new AiFoundingConfig(true, 1, 50.0);
        WorldState w = Toy([2, 2], 500);
        w.Settlements[1] = w.Settlements[1] with { FoundedTurn = 5 };   // the polity's newest settlement

        w.Clock = new SimClock(Turn: 9, SimDays: 0, DtDays: 10 * SimClock.YearDays);    // 4 turns x 10 y = 40 < 50
        Assert.False(AiFoundingPolicy.IsDue(w, ai, Rival));
        w.Clock = new SimClock(Turn: 10, SimDays: 0, DtDays: 10 * SimClock.YearDays);   // 5 x 10 = 50: due
        Assert.True(AiFoundingPolicy.IsDue(w, ai, Rival));
        // Turns shorten (era table): 5 turns at 5 y read as 25 y even if the earlier ones were longer — never early.
        w.Clock = new SimClock(Turn: 10, SimDays: 0, DtDays: 5 * SimClock.YearDays);
        Assert.False(AiFoundingPolicy.IsDue(w, ai, Rival));

        // A settlement the polity does not control is not its founding; a polity that controls nothing is never due.
        w.Controls[1] = w.Controls[1] with { Polity = Player };
        w.Clock = new SimClock(Turn: 6, SimDays: 0, DtDays: 10 * SimClock.YearDays);   // newest own is turn 0: 60 y
        Assert.True(AiFoundingPolicy.IsDue(w, ai, Rival));
        Assert.False(AiFoundingPolicy.IsDue(w, ai, new PolityId(9)));
    }

    [Fact]
    public void TheSwitch_OffOrAbsent_IssuesNothing_AndOnItIssuesOnTheSameWorld()
    {
        WorldState w = Duo1();
        Assert.NotNull(AiFoundingPolicy.Decide(w, Lax, Wg, Rival));
        Assert.Null(AiFoundingPolicy.Decide(w, With(new AiFoundingConfig(false, 1, 0.0)), Wg, Rival));
        Assert.Null(AiFoundingPolicy.Decide(w, With(null), Wg, Rival));
        Assert.DoesNotContain(AiOrders.For(w, With(new AiFoundingConfig(false, 1, 0.0)), Wg), o => o.Kind == OrderKind.FoundSettlement);
        // The player polity is never decided for, whatever the config.
        Assert.Null(AiFoundingPolicy.Decide(w, Lax, Wg, Player));
    }

    [Fact]
    public void TheShippedContent_CarriesTheSwitchOn_AndItsDerivedThresholds()
    {
        AiFoundingConfig ai = Cfg.Colonization!.AiFounding!;
        Assert.True(ai.Enabled);
        // minSourcePopulation is DERIVED: the turn-zero founding size / (1 - orderPartyFraction) = 400 / 0.8.
        long foundingSize = Cfg.Founding.CohortCounts.Sum();
        Assert.Equal(foundingSize, (long)Math.Round(ai.MinSourcePopulation * (1.0 - Cfg.Colonization.OrderPartyFraction)));
        Assert.Equal(350.0, ai.MinYearsBetweenFoundings);
    }

    [Fact]
    public void TheConfigLoader_RefusesANonsensicalAiFoundingSection()
    {
        string json = TestConfigs.SimJson();
        Assert.Throws<SimConfigException>(() => Load(json.Replace("\"minSourcePopulation\": 500", "\"minSourcePopulation\": 0")));
        Assert.Throws<SimConfigException>(() => Load(json.Replace("\"minYearsBetweenFoundings\": 350", "\"minYearsBetweenFoundings\": -1")));
        Assert.NotNull(Load(json).Colonization!.AiFounding);

        static SimConfig Load(string sim) => SimConfigLoader.Load(sim);
    }
}
