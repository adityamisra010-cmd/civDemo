using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.Systems;
using Sim.Core.State;
using Sim.Core.Systems.Research;
using Sim.Tests.TestUtil;
using static Sim.Tests.TestUtil.ResearchRigs;

namespace Sim.Tests.Systems;

/// <summary>
/// ADR-054 — THE DIRECTOR'S BINDING RULE (2026-10-27): "Every Research Point and Civics Point calculation is per turn and
/// based on average population", and the BASE production rate of RP and CP (no university, building, buff, penalty or
/// espionage) is the same, since both are population dependent.
/// <list type="bullet">
/// <item><b>One base.</b> RP and per-turn CP are the same function (ResearchQuery.BaseKnowledgePointsPerTurn =
///   rpPerTurn.coefficient × P^rpPerTurn.exponent) of the same input (ResearchQuery.KnowledgePopulation): base RP = base CP
///   exactly, at every turn length (dtYears 10, 5, 3, 2, 1, 0.5) and every population (100 … 1 000 000).</item>
/// <item><b>Per turn.</b> CP accrual is identical across dtYears at equal population (the ADR-030 §5 pattern extended).</item>
/// <item><b>Average population</b> (stated assumption, ADR-054 §3): the trapezoid mean of the holder's population at the
///   start of its previous research step and at the start of this one; first step = the current population. A population
///   jump shows HALF its effect on the next step and its FULL effect on the step after (turn-exact).</item>
/// </list>
/// The rig: ResearchRigs.Standard (RP = 10 × P^0.5 per turn) with tuning.civicsPoints {perTurn} and
/// tuning.populationAverageTurns 2 — so 10 000 people make exactly 1 000 RP and 1 000 000 milli-CP a turn.
/// </summary>
public class PerTurnCivicsAndAveragePopulationTests
{
    private static readonly PolityId P1 = new(Player);

    private static ResearchContent RigContent(bool perTurn = true, int? average = 2, double? cityStatePace = null)
    {
        Spec spec = Standard();
        if (perTurn) spec.CivicsPerTurn = true;
        else spec.CivicsPoints = (true, 1.0, 0.5);   // the ADR-043 §3 twin: 100 CP a sim-year at 10 000 people
        spec.PopulationAverageTurns = average;
        spec.CityStatePace = cityStatePace;
        return spec.Load();
    }

    private static readonly Lazy<ResearchContent> Rig = new(() => RigContent());

    /// <summary>Adds <paramref name="people"/> to settlement <paramref name="settlement"/>'s first bucket (a ledger Source flow).</summary>
    private static void AddPeople(WorldState w, int settlement, long people)
    {
        for (int b = 0; b < w.Buckets.Count; b++)
        {
            if (w.Buckets[b].Settlement.Value != settlement) continue;
            new Ledger(w.LedgerFlows).Flow(ref w.Buckets.Ref(b).Count, ConservedQuantityIds.Population,
                ReasonIds.InitialEndowment, people, FlowDirection.Source, OverdrawPolicy.Throw);
            return;
        }
        throw new InvalidOperationException("no bucket in settlement " + settlement);
    }

    /// <summary>A settlement with <paramref name="people"/> and NO control row: a city state (R2a), its own knowledge holder.</summary>
    private static void AddCityState(WorldState w, int settlement, long people)
    {
        var id = new SettlementId(settlement);
        w.Settlements.Add(new SettlementRow(id, SiteCell: settlement, FoundedTurn: 0));
        int row = w.Buckets.Add(new BucketRow(id, new CultureId(1), new ReligionId(1), new ClassId(1),
            cohortIdx: 5, Conserved.Zero, 0.0, 0.0, 0.0, 0.0));
        new Ledger(w.LedgerFlows).Flow(ref w.Buckets.Ref(row).Count, ConservedQuantityIds.Population,
            ReasonIds.InitialEndowment, people, FlowDirection.Source, OverdrawPolicy.Throw);
    }

    private static long Generated(IReadOnlyWorldState w, PolityId holder) => ResearchQuery.CivicsPointsAccount(w, holder).GeneratedMilli;

    /// <summary>A research-only executor on one flat era band whose edges land on a turn boundary at every tested dt
    /// (60 000 years: a whole number of 10-, 5-, 3-, 2-, 1- and 0.5-year turns; ResearchRigs.FlatEra's 100 000 is not a
    /// multiple of 3).</summary>
    private static TurnExecutor Executor(ResearchContent content, OrderLog? orders = null, double dtYears = 10.0) =>
        new(EraTableLoader.Load("{ \"bands\": [ { \"name\": \"flat\", \"startYear\": 0, \"endYear\": 60000, \"dtYears\": "
                + dtYears.ToString(System.Globalization.CultureInfo.InvariantCulture) + " } ] }"),
            [SystemCatalog.Research(TestConfigs.Sim() with { Research = content })], orders);

    // ------------------------------------------------------------------ one base: base RP = base CP

    public static TheoryData<double, double> DtAndPopulation()
    {
        var data = new TheoryData<double, double>();
        foreach (double dt in new[] { 10.0, 5.0, 3.0, 2.0, 1.0, 0.5 })
            foreach (double p in new[] { 100.0, 1_000.0, 10_000.0, 1_000_000.0 })
                data.Add(dt, p);
        return data;
    }

    [Theory]
    [MemberData(nameof(DtAndPopulation))]
    public void ShippedContent_BaseRpEqualsBaseCp_ExactlyAtEveryTurnLengthAndPopulation(double dtYears, double population)
    {
        ResearchContent c = TestConfigs.Research();
        Assert.True(c.Tuning.CivicsPoints is { Enabled: true, PerTurn: true });
        Assert.Equal(2, c.Tuning.PopulationAverageTurns);
        double baseRp = ResearchQuery.ResearchPoints(c.Tuning, population);
        // ONE function: base CP per turn IS the base RP per turn (==, no epsilon), and neither reads dt.
        Assert.Equal(ResearchQuery.BaseKnowledgePointsPerTurn(c.Tuning, population), baseRp);
        Assert.Equal(baseRp, ResearchQuery.CivicsPointsPerTurn(c.Tuning, population));
        Assert.Equal(0.0, ResearchQuery.CivicsPointsPerYear(c.Tuning, population));   // no per-year rate exists any more

        // A real holder of exactly that population, no completed research (no benefit), first step (no stored population):
        // the pool is the base, and the step's CP accrual is the base in milli-CP — the same number at every dtYears.
        WorldState w = World([Player], [Player], (long)population);
        Assert.Equal(population, ResearchQuery.KnowledgePopulation(w, c, P1));
        double pool = ResearchQuery.ResearchPointPool(w, c, P1);
        Assert.Equal(baseRp, pool);
        long cpMilli = ResearchQuery.CivicsPointsMilli(w, c, P1, dtYears);
        Assert.Equal((long)Math.Floor(1000.0 * 1.0 * baseRp), cpMilli);
        Assert.Equal(ResearchQuery.CivicsPointsMilli(w, c, P1, 10.0), cpMilli);
        Assert.True(cpMilli > 0L);
    }

    [Fact]
    public void ShippedContent_TheAnchorsHoldForBothLanes_100Gives2_And1000Gives10()
    {
        ResearchTuning t = TestConfigs.Research().Tuning;
        foreach ((double p, double rp) in new[] { (100.0, 2.0), (1_000.0, 10.0) })
        {
            Assert.InRange(ResearchQuery.ResearchPoints(t, p), rp * 0.999, rp * 1.001);   // the calibration anchors (ADR-030)
            Assert.Equal(ResearchQuery.ResearchPoints(t, p), ResearchQuery.CivicsPointsPerTurn(t, p));
        }
    }

    [Fact]
    public void ShippedContent_CityStateHolder_RpAndCpCarryTheSamePaceOnTheSameBase()
    {
        ResearchContent c = TestConfigs.Research();
        WorldState w = World([Player], [Player], 5_000);
        AddCityState(w, 1, 2_000);
        PolityId local = SettlementKnowledge.LocalHolder(new SettlementId(1));
        double pace = c.Tuning.CityStatePaceFraction;
        Assert.True(pace > 0.0);
        double baseCp = ResearchQuery.BaseKnowledgePointsPerTurn(c.Tuning, 2_000);
        Assert.Equal(pace * baseCp, ResearchQuery.CityStateResearchPoints(w, c, new SettlementId(1)));
        foreach (double dt in new[] { 10.0, 5.0, 3.0, 2.0, 1.0, 0.5 })
            Assert.Equal((long)Math.Floor(1000.0 * pace * baseCp), ResearchQuery.CivicsPointsMilli(w, c, local, dt));
    }

    // ------------------------------------------------------------------ per turn: the ADR-030 §5 pattern extended to CP

    [Theory]
    [InlineData(10.0)]
    [InlineData(5.0)]
    [InlineData(3.0)]
    [InlineData(2.0)]
    [InlineData(1.0)]
    [InlineData(0.5)]
    public void CpAccrual_IsPerTurn_IdenticalAcrossDtYears_AndEqualsTheRpOfTheSameStep(double dtYears)
    {
        var orders = new OrderLog();
        orders.Append(Target(0, 2));   // b — Technology; the civics lane holds no target, so the CP is banked whole
        WorldState w = Executor(Rig.Value, orders, dtYears).Step(WithCompleted(PlayerWorld(), 1));
        Assert.Equal(1000.0, Progress(w, 2));                    // RP per turn (ADR-030)
        Assert.Equal(1_000_000L, Generated(w, P1));              // CP per turn (ADR-054): the same 1 000, at every dt
        Assert.Equal(new CivicsPointsRow(P1, 1_000_000L, 1_000_000L, 0L), ResearchQuery.CivicsPointsAccount(w, P1));
    }

    [Theory]
    [InlineData(10.0, 1_000_000L)]
    [InlineData(5.0, 500_000L)]
    [InlineData(1.0, 100_000L)]
    [InlineData(0.5, 50_000L)]
    public void TheTwin_PerTurnAbsent_IsAdr043sPerSimYearAccrual_StillReachable(double dtYears, long expectedMilli)
    {
        var orders = new OrderLog();
        orders.Append(Target(0, 2));
        WorldState w = Executor(RigContent(perTurn: false, average: null), orders, dtYears).Step(WithCompleted(PlayerWorld(), 1));
        Assert.Equal(expectedMilli, Generated(w, P1));           // 100 CP a sim-year × dt (ADR-043 §3)
        Assert.Equal(1000.0, Progress(w, 2));
        Assert.Equal(0, w.ResearchPopulations.Count);            // the average switch off: no row is ever written
    }

    // ------------------------------------------------------------------ average population: turn-exact

    [Fact]
    public void APopulationJump_ShowsHalfItsEffectNextStep_AndItsFullEffectTheStepAfter_InBothLanes()
    {
        var orders = new OrderLog();
        orders.Append(Target(0, 2));
        TurnExecutor ex = Executor(Rig.Value, orders, dtYears: 0.5);
        WorldState w0 = WithCompleted(World([Player], [Player], 100), 1);
        Assert.Equal(0, w0.ResearchPopulations.Count);

        // Step 1 — FIRST STEP: no stored population, the current one (100) → 100 RP and 100 000 milli-CP.
        WorldState w1 = ex.Step(w0);
        Assert.Equal(100.0, Progress(w1, 2));
        Assert.Equal(100_000L, Generated(w1, P1));
        Assert.Equal([new ResearchPopulationRow(P1, 100L)], Rows(w1));

        // Between the steps the population jumps to 400.
        WorldState jumped = w1.Clone();
        AddPeople(jumped, 0, 300);
        Assert.Equal(250.0, ResearchQuery.KnowledgePopulation(jumped, Rig.Value, P1));   // (100 + 400) / 2
        Assert.Equal(400L, ResearchQuery.Population(jumped, P1));

        // Step 2 — HALF the jump: the average 250 → 10 × 250^0.5 RP, and the same in CP.
        WorldState w2 = ex.Step(jumped);
        double half = 10.0 * Math.Pow(250.0, 0.5);
        Assert.Equal(100.0 + half, Progress(w2, 2));
        Assert.Equal(100_000L + (long)Math.Floor(1000.0 * half), Generated(w2, P1));
        Assert.Equal([new ResearchPopulationRow(P1, 400L)], Rows(w2));

        // Step 3 — the FULL jump: (400 + 400) / 2 → 200 RP and 200 000 milli-CP.
        WorldState w3 = ex.Step(w2);
        Assert.Equal(100.0 + half + 200.0, Progress(w3, 2));
        Assert.Equal(100_000L + (long)Math.Floor(1000.0 * half) + 200_000L, Generated(w3, P1));
    }

    [Fact]
    public void WithTheAverageOff_TheJumpShowsInFullAtOnce_AndNoRowIsWritten()
    {
        var orders = new OrderLog();
        orders.Append(Target(0, 2));
        TurnExecutor ex = Executor(RigContent(average: null), orders);
        WorldState w1 = ex.Step(WithCompleted(World([Player], [Player], 100), 1));
        WorldState jumped = w1.Clone();
        AddPeople(jumped, 0, 300);
        Assert.Equal(400.0, ResearchQuery.KnowledgePopulation(jumped, RigContent(average: null), P1));
        WorldState w2 = ex.Step(jumped);
        Assert.Equal(100.0 + 200.0, Progress(w2, 2));
        Assert.Equal(300_000L, Generated(w2, P1));
        Assert.Equal(0, w2.ResearchPopulations.Count);
    }

    [Fact]
    public void TheTableIsOneRowPerHolder_StrictlyAscending_WithCityStatesUnderTheirNegativeKeys()
    {
        ResearchContent c = RigContent(cityStatePace: 0.25);
        WorldState w = World([3, Player], [Player, 3], 10_000);   // roster order 3, 1: the table sorts by key, not roster
        AddCityState(w, 2, 2_500);
        AddCityState(w, 3, 900);
        WorldState next = Executor(c).Step(w);
        Assert.Equal([
            new ResearchPopulationRow(SettlementKnowledge.LocalHolder(new SettlementId(3)), 900L),
            new ResearchPopulationRow(SettlementKnowledge.LocalHolder(new SettlementId(2)), 2_500L),
            new ResearchPopulationRow(P1, 10_000L),
            new ResearchPopulationRow(new PolityId(3), 10_000L),
        ], Rows(next));
        Assert.Null(ResearchQuery.PopulationsInvariantViolation(next.ResearchPopulations));
    }

    [Fact]
    public void ACityState_AveragesItsOwnPopulation_AndCarriesItsPaceIntoBothLanes()
    {
        ResearchContent c = RigContent(cityStatePace: 0.25);
        PolityId local = SettlementKnowledge.LocalHolder(new SettlementId(1));
        WorldState w = World([Player], [Player], 10_000);
        AddCityState(w, 1, 10_000);
        TurnExecutor ex = Executor(c, dtYears: 3.0);
        WorldState w1 = ex.Step(w);
        Assert.Equal(250_000L, Generated(w1, local));                          // 0.25 × 1 000 CP, first step
        WorldState jumped = w1.Clone();
        AddPeople(jumped, 1, 30_000);                                        // the city state grows to 40 000
        Assert.Equal(25_000.0, ResearchQuery.KnowledgePopulation(jumped, c, local));
        Assert.Equal(0.25 * (10.0 * Math.Pow(25_000.0, 0.5)), ResearchQuery.CityStateResearchPoints(jumped, c, new SettlementId(1)));
        WorldState w2 = ex.Step(jumped);
        Assert.Equal(250_000L + (long)Math.Floor(1000.0 * 0.25 * (10.0 * Math.Pow(25_000.0, 0.5))), Generated(w2, local));
        WorldState w3 = ex.Step(w2);
        Assert.Equal(Generated(w2, local) + 500_000L, Generated(w3, local));   // 0.25 × 2 000 CP: the full jump
        Assert.Equal(1_000_000L * 3, Generated(w3, P1));                        // the Empire's 10 000 never moved
    }

    [Fact]
    public void ControlChange_AnnexationAveragesTheEmpireAcrossIt_AndARelease_StartsTheCityStateAfresh()
    {
        ResearchContent c = RigContent(cityStatePace: 0.25);
        PolityId local = SettlementKnowledge.LocalHolder(new SettlementId(1));
        WorldState w = World([Player], [Player], 10_000);
        AddCityState(w, 1, 30_000);
        TurnExecutor ex = Executor(c);
        WorldState w1 = ex.Step(w);
        Assert.Equal(2, w1.ResearchPopulations.Count);

        // Annexation: the Empire's population goes 10 000 → 40 000 across the turn; the city state stops being a holder.
        WorldState annexed = w1.Clone();
        annexed.Controls.Add(new ControlRow(P1, new SettlementId(1), 1.0));
        Assert.Equal(25_000.0, ResearchQuery.KnowledgePopulation(annexed, c, P1));
        WorldState w2 = ex.Step(annexed);
        Assert.Equal(1_000_000L + (long)Math.Floor(1000.0 * 1.0 * (10.0 * Math.Pow(25_000.0, 0.5))), Generated(w2, P1));
        Assert.Equal([new ResearchPopulationRow(P1, 40_000L)], Rows(w2));     // the dormant local holder's row is gone

        // Release: the settlement is uncontrolled again — the city state has no stored row, so its first step reads its
        // current population (30 000), while the Empire averages 40 000 → 10 000.
        WorldState released = w2.Clone();
        var keep = new List<ControlRow>();
        for (int i = 0; i < released.Controls.Count; i++) if (released.Controls[i].Place.Value != 1) keep.Add(released.Controls[i]);
        released.Controls.Clear();
        foreach (ControlRow r in keep) released.Controls.Add(r);
        Assert.Equal(30_000.0, ResearchQuery.KnowledgePopulation(released, c, local));
        Assert.Equal(25_000.0, ResearchQuery.KnowledgePopulation(released, c, P1));
        WorldState w3 = ex.Step(released);
        Assert.Equal(Generated(w1, local) + (long)Math.Floor(1000.0 * 0.25 * (10.0 * Math.Pow(30_000.0, 0.5))), Generated(w3, local));
    }

    // ------------------------------------------------------------------ save/load and replay

    [Fact]
    public void SaveLoad_MidRun_BetweenAJumpAndItsSecondStep_ContinuesBitIdentically_AndReplayIsEqual()
    {
        WorldState Run(bool saveLoad)
        {
            var orders = new OrderLog();
            orders.Append(Target(0, 2));
            orders.Append(Target(0, 1001));
            TurnExecutor ex = Executor(Rig.Value, orders, dtYears: 2.0);
            WorldState w = WithCompleted(PlayerWorld(), 1);
            for (int t = 0; t < 6; t++)
            {
                if (t == 3) AddPeople(w, 0, 6_000);   // a jump between turns 3 and 4
                if (saveLoad && t == 4)
                {
                    using var ms = new MemoryStream();
                    Snapshot.Save(w, ms);
                    w = Snapshot.Load(new MemoryStream(ms.ToArray()));
                    Assert.Equal([new ResearchPopulationRow(P1, 16_000L)], Rows(w));   // the stored half of the trapezoid survives
                }
                w = ex.Step(w);
            }
            return w;
        }
        WorldState a = Run(saveLoad: false), b = Run(saveLoad: true), c = Run(saveLoad: false);
        Assert.True(WorldStates.StateEquals(a, b), "save/load mid-run drifted");
        Assert.Equal(WorldHash.ComputeHex(a), WorldHash.ComputeHex(b));
        Assert.Equal(WorldHash.ComputeHex(a), WorldHash.ComputeHex(c));   // replay
        Assert.True(a.ResearchPopulations.Count == 1);
    }

    // ------------------------------------------------------------------ the loader

    [Fact]
    public void Loader_PerTurnRefusesASecondCoefficient_AndTheWindowIsOneOrTwo()
    {
        Spec s = Standard();
        s.CivicsPerTurn = true;
        var doc = System.Text.Json.Nodes.JsonNode.Parse(s.Json())!.AsObject();
        doc["tuning"]!["civicsPoints"]!["coefficientPerYear"] = 0.008;
        var refused = Assert.Throws<ResearchContentException>(() => ResearchContentLoader.Load(doc.ToJsonString(), TestConfigs.Sim().Goods));
        Assert.Contains("must be absent", refused.Message);

        foreach (int bad in new[] { 0, 3, -1 })
        {
            Spec t = Standard();
            t.PopulationAverageTurns = bad;
            Assert.Contains("populationAverageTurns", Assert.Throws<ResearchContentException>(() => t.Load()).Message);
        }
        Assert.Equal(1, Standard().Load().Tuning.PopulationAverageTurns);   // absent = 1 = off
        Spec perYear = Standard();
        perYear.CivicsPoints = (true, 1.0, 0.5);
        Assert.False(perYear.Load().Tuning.CivicsPoints!.PerTurn);
    }

    private static ResearchPopulationRow[] Rows(IReadOnlyWorldState w)
    {
        var rows = new ResearchPopulationRow[w.ResearchPopulations.Count];
        for (int i = 0; i < rows.Length; i++) rows[i] = w.ResearchPopulations[i];
        return rows;
    }
}
