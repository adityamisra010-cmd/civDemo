using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Disaster;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

/// <summary>
/// T4.21-1 (CR-015 §3.3, test plan §6.2) — the explicit famine-class production
/// shock. What is pinned: the onset process composes across dts up to its stated
/// biases; the loss GIVEN onset is dt-exact; the multiplier reaches exactly the
/// two food paths weather reaches and never extraction; λ = 0 differs from a
/// world without the system by the RNG stream rows alone; rows persist through
/// save/load; the classification the same physical event receives at dt 10 and
/// dt 0.5 DIFFERS, and that difference is pinned as the inherited F4 artefact,
/// never asserted equal.
/// </summary>
public class DisasterSystemTests
{
    private const ulong Seed = 42;
    private static readonly SettlementId S0 = new(0);

    private static EraTable FlatEra(double dtYears) => EraTableLoader.Load(
        $$"""{ "bands": [ { "name": "flat", "startYear": 0, "endYear": 100000, "dtYears": {{dtYears.ToString(System.Globalization.CultureInfo.InvariantCulture)}} } ] }""");

    private static SimConfig WithDisaster(SimConfig cfg, double hazard, double? duration = null, double? sMin = null, double? sMax = null) =>
        cfg with
        {
            Disaster = cfg.Disaster with
            {
                HazardPerYear = hazard,
                DurationYears = duration ?? cfg.Disaster.DurationYears,
                SeverityMin = sMin ?? cfg.Disaster.SeverityMin,
                SeverityMax = sMax ?? cfg.Disaster.SeverityMax,
            },
        };

    /// <summary>The canonical pipeline (disaster included) over the dev preset.</summary>
    private static TurnExecutor CanonicalExecutor(SimConfig cfg, OrderLog? orders = null)
    {
        using var era = Sim.Data.DataFiles.OpenEraPacing();
        using var pipe = Sim.Data.DataFiles.OpenPipeline();
        return new TurnExecutor(EraTableLoader.Load(era),
            PipelineLoader.Load(pipe, SystemCatalog.All(cfg, TestConfigs.DevWorldgen())), orders);
    }

    private static WorldState DevFounded(SimConfig cfg, ulong seed = Seed, int settlements = 4) =>
        WorldFounding.Found(TestConfigs.DevWorldgen(), cfg, seed, settlements);

    private static byte[] Bytes(WorldState w)
    {
        using var buffer = new MemoryStream();
        using (var writer = new BinaryWriter(buffer, System.Text.Encoding.UTF8, leaveOpen: true))
            CanonicalSchema.Write(w, writer);
        return buffer.ToArray();
    }

    private static DisasterRow? RowOf(WorldState w, SettlementId s)
    {
        for (int i = 0; i < w.Disasters.Count; i++)
            if (w.Disasters[i].Settlement == s) return w.Disasters[i];
        return null;
    }

    // ======================================================================
    // THE ATTRIBUTION CONTROL AND THE RNG CONTRACT
    // ======================================================================

    [Fact]
    public void D_HazardZero_StripControl()
    {
        // λ = 0 (the shipped value): the world with the system in its pipeline,
        // its disaster streams removed and its (empty) table cleared, is BYTE
        // FOR BYTE the world run with the system absent from the pipeline. The
        // only thing the packet adds to a canonical stream is stream rows.
        SimConfig cfg = TestConfigs.Sim();
        Assert.Equal(0.0, cfg.Disaster.HazardPerYear);

        WorldState with = CanonicalExecutor(cfg).Run(DevFounded(cfg), 30);

        using var pipe = Sim.Data.DataFiles.OpenPipeline();
        string json = new StreamReader(pipe).ReadToEnd().Replace("\"disaster\",", "");
        Assert.DoesNotContain("disaster", json);
        using var era = Sim.Data.DataFiles.OpenEraPacing();
        var withoutExec = new TurnExecutor(EraTableLoader.Load(era),
            PipelineLoader.Load(json, SystemCatalog.All(cfg, TestConfigs.DevWorldgen())));
        WorldState without = withoutExec.Run(DevFounded(cfg), 30);

        Assert.Equal(0, with.Disasters.Count);
        int removed = 0;
        var kept = new List<RngStreamRow>();
        for (int i = 0; i < with.RngStreams.Count; i++)
        {
            if (with.RngStreams[i].System == DisasterSystem.WellKnownId) { removed++; continue; }
            kept.Add(with.RngStreams[i]);
        }
        Assert.Equal(with.Settlements.Count, removed);   // one stream per settlement — the strip is not vacuous
        with.RngStreams.Clear();
        foreach (RngStreamRow r in kept) with.RngStreams.Add(r);

        Assert.Equal(Bytes(without), Bytes(with));
        Assert.Equal(WorldHash.ComputeHex(without), WorldHash.ComputeHex(with));
    }

    [Fact]
    public void D_RngConsumption_IsTwoDrawsPerSettlement_WhateverTheHazard()
    {
        // BOTH uniforms are drawn unconditionally: a λ = 0 turn and a λ = ∞
        // turn leave every stream in the SAME state. This is what makes the
        // control above exact and a TUNE of λ hash-attributable.
        SimConfig cfg = TestConfigs.Sim();
        WorldState a = new TurnExecutor(FlatEra(10.0), [SystemCatalog.Disaster(WithDisaster(cfg, 0.0))]).Step(Settlements(7, 5));
        WorldState b = new TurnExecutor(FlatEra(10.0), [SystemCatalog.Disaster(WithDisaster(cfg, 1e6))]).Step(Settlements(7, 5));
        Assert.Equal(0, a.Disasters.Count);
        Assert.Equal(5, b.Disasters.Count);
        Assert.Equal(5, a.RngStreams.Count);
        Assert.True(WorldStates.TableEquals(a.RngStreams, b.RngStreams));

        // And the streams really advanced: two NextDouble = four NextUInt32.
        WorldState untouched = Settlements(7, 5);
        var reg = new RngRegistry(untouched);
        for (int i = 0; i < 5; i++)
        {
            RngStream s = reg.Get(DisasterSystem.WellKnownId, new RegionId(i));
            s.NextDouble(); s.NextDouble();
        }
        Assert.True(WorldStates.TableEquals(untouched.RngStreams, a.RngStreams));
    }

    private static WorldState Settlements(ulong seed, int n)
    {
        var w = new WorldState(seed);
        for (int i = 0; i < n; i++) w.Settlements.Add(new SettlementRow(new SettlementId(i), SiteCell: i, FoundedTurn: 0));
        return w;
    }

    // ======================================================================
    // THE ROW
    // ======================================================================

    [Fact]
    public void D_HazardInfinite_EveryoneStruck()
    {
        // λ = 1e6 ⇒ P = 1 − e^{−1e7} = 1 exactly ⇒ every settlement onsets on
        // turn 1 with Multiplier = 1 − s·min(D, dt)/dt, Applied 1.0.
        SimConfig cfg = WithDisaster(TestConfigs.Sim(), 1e6);
        WorldState w = CanonicalExecutor(cfg).Step(DevFounded(cfg));
        Assert.Equal(w.Settlements.Count, w.Disasters.Count);
        double dt = w.Clock.DtDays / SimClock.YearDays;
        Assert.Equal(10.0, dt);
        for (int i = 0; i < w.Settlements.Count; i++)
        {
            DisasterRow? row = RowOf(w, w.Settlements[i].Id);
            Assert.NotNull(row);
            DisasterRow r = row.Value;
            Assert.Equal(DisasterSystem.KindCropFailure, r.Kind);
            Assert.InRange(r.Severity, cfg.Disaster.SeverityMin, cfg.Disaster.SeverityMax);
            Assert.Equal(1.0 - r.Severity * Math.Min(cfg.Disaster.DurationYears, dt) / dt, r.Multiplier);
            Assert.Equal(Math.Max(0.0, cfg.Disaster.DurationYears - dt), r.RemainingYears);
            Assert.Equal(1.0, r.AppliedMultiplier);
        }
        // Severities are DRAWN, not constant: the second uniform reaches the row.
        bool varied = false;
        for (int i = 1; i < w.Disasters.Count; i++) if (w.Disasters[i].Severity != w.Disasters[0].Severity) varied = true;
        Assert.True(varied, "every severity identical — the severity draw is not reaching the row");

        // A 25-year event at dt 10: RemainingYears 15, the whole turn active —
        // the row's formula 1 − s·overlap/dt with overlap = dt (s·10/10 is not
        // bit-identical to s, and the row carries the formula, not a shortcut).
        SimConfig longCfg = WithDisaster(TestConfigs.Sim(), 1e6, duration: 25.0);
        WorldState l = CanonicalExecutor(longCfg).Step(DevFounded(longCfg));
        for (int i = 0; i < l.Disasters.Count; i++)
        {
            Assert.Equal(15.0, l.Disasters[i].RemainingYears);
            Assert.Equal(1.0 - l.Disasters[i].Severity * 10.0 / 10.0, l.Disasters[i].Multiplier);
            Assert.InRange(l.Disasters[i].Multiplier, 0.0, 0.25 + 1e-12);
        }
    }

    [Fact]
    public void D_Rows_PersistWhileActive_CarryTheAppliedFactOneTurn_ThenVanish()
    {
        // The renewal: an active event continues untouched by the hazard (no new
        // onset while active), each turn's overlap reduces RemainingYears, the
        // last turn books the partial overlap, the turn after carries ONLY the
        // Applied fact (Kind 0), and then the table has no row for it.
        SimConfig cfg = TestConfigs.Sim();
        SimConfig strike = WithDisaster(cfg, 1e6, duration: 12.0, sMin: 0.8, sMax: 0.8);
        SimConfig quiet = WithDisaster(cfg, 0.0, duration: 12.0, sMin: 0.8, sMax: 0.8);
        WorldState w = Settlements(3, 1);

        w = new TurnExecutor(FlatEra(5.0), [SystemCatalog.Disaster(strike)]).Step(w);   // onset
        DisasterRow r1 = RowOf(w, S0)!.Value;
        Assert.Equal((1, 0.8, 7.0, 1.0 - 0.8, 1.0), (r1.Kind, r1.Severity, r1.RemainingYears, r1.Multiplier, r1.AppliedMultiplier));

        TurnExecutor q = new(FlatEra(5.0), [SystemCatalog.Disaster(quiet)]);
        w = q.Step(w);
        DisasterRow r2 = RowOf(w, S0)!.Value;
        Assert.Equal((1, 0.8, 2.0, 1.0 - 0.8, 1.0 - 0.8), (r2.Kind, r2.Severity, r2.RemainingYears, r2.Multiplier, r2.AppliedMultiplier));

        w = q.Step(w);   // 2 of 5 years active: multiplier 1 − 0.8 × 2/5
        DisasterRow r3 = RowOf(w, S0)!.Value;
        Assert.Equal(1, r3.Kind);
        Assert.Equal(0.0, r3.RemainingYears);
        Assert.Equal(1.0 - 0.8 * 2.0 / 5.0, r3.Multiplier);
        Assert.Equal(1.0 - 0.8, r3.AppliedMultiplier);

        w = q.Step(w);   // the carry: Applied only
        DisasterRow r4 = RowOf(w, S0)!.Value;
        Assert.Equal((0, 0.0, 0.0, 1.0), (r4.Kind, r4.Severity, r4.RemainingYears, r4.Multiplier));
        Assert.Equal(1.0 - 0.8 * 2.0 / 5.0, r4.AppliedMultiplier);

        w = q.Step(w);   // gone
        Assert.Null(RowOf(w, S0));
        Assert.Equal(0, w.Disasters.Count);

        // The strike executor on an ACTIVE row does not re-draw the severity or
        // the duration: renewal, not superposition.
        WorldState active = Settlements(3, 1);
        active = new TurnExecutor(FlatEra(5.0), [SystemCatalog.Disaster(strike)]).Step(active);
        active = new TurnExecutor(FlatEra(5.0), [SystemCatalog.Disaster(WithDisaster(cfg, 1e6, duration: 12.0, sMin: 0.1, sMax: 0.1))]).Step(active);
        DisasterRow still = RowOf(active, S0)!.Value;
        Assert.Equal(0.8, still.Severity);
        Assert.Equal(2.0, still.RemainingYears);
    }

    // ======================================================================
    // PRODUCTION
    // ======================================================================

    [Fact]
    public void D_AppliedOnce_FarmingAndHerding_NeverExtraction()
    {
        // KILLS M-PROD-EXTRACTION. Grain and livestock scale by the disaster
        // multiplier exactly once (composed with weather where a weather row
        // exists); ore does not see it at all — the T4.5 fence, inherited.
        SimConfig cfg = TestConfigs.Sim();
        var counts = new long[Cohorts.Count];
        counts[5] = 3000;

        (long Grain, long Livestock, long Clay) Produce(double? disaster, double? weather)
        {
            WorldState w = PopulationExactnessTests.BucketWorld(counts);
            w.CatchmentSummaries.Add(new CatchmentSummaryRow(S0, NodeCount: 1, EffectiveArableKm2: 5000.0, NetworkRevision: 0, LastRecomputeTurn: 0));
            w.SectorAllocations.Add(new SectorAllocationRow(S0, 0.4, 0.3, 0.3, 0.0, 0.0));
            w.Deposits.Add(new DepositRow(S0, new GoodId(cfg.Goods!.IdOf("livestock")), 1.0));
            w.Deposits.Add(new DepositRow(S0, new GoodId(cfg.Goods!.IdOf("clay")), 1.0));
            foreach (GoodEntry g in cfg.Goods!.Goods)
                w.GoodStocks.Add(new GoodStockRow(S0, new GoodId(g.Id), Conserved.Zero, 0.0, 0.0));
            if (weather is double m) w.HarvestWeather.Add(new HarvestWeatherRow(S0, Math.Log(m), m));
            if (disaster is double d) w.Disasters.Add(new DisasterRow(S0, 1, 1.0 - d, 0.0, d, 1.0));
            WorldState next = new TurnExecutor(FlatEra(10.0), [SystemCatalog.Production(cfg)]).Step(w);
            return (Stock(next, "grain"), Stock(next, "livestock"), Stock(next, "clay"));
        }
        long Stock(WorldState w, string good)
        {
            int id = cfg.Goods!.IdOf(good);
            for (int i = 0; i < w.GoodStocks.Count; i++)
                if (w.GoodStocks[i].Settlement == S0 && w.GoodStocks[i].Good.Value == id) return w.GoodStocks[i].Amount.Value;
            return 0;
        }

        (long g0, long l0, long c0) = Produce(null, null);
        Assert.True(g0 > 1000 && l0 > 1000 && c0 > 1000, "rig vacuous");

        (long g1, long l1, long c1) = Produce(0.5, null);
        Assert.InRange(g1, (long)Math.Floor(g0 * 0.5) - 1, (long)Math.Floor(g0 * 0.5) + 1);
        Assert.InRange(l1, (long)Math.Floor(l0 * 0.5) - 1, (long)Math.Floor(l0 * 0.5) + 1);
        Assert.Equal(c0, c1);

        (long g2, long l2, long c2) = Produce(0.5, 0.8);   // composed: 0.4, once
        Assert.InRange(g2, (long)Math.Floor(g0 * 0.4) - 1, (long)Math.Floor(g0 * 0.4) + 1);
        Assert.InRange(l2, (long)Math.Floor(l0 * 0.4) - 1, (long)Math.Floor(l0 * 0.4) + 1);
        Assert.Equal(c0, c2);

        // A row with Multiplier 1.0 (the Applied-only carry) changes nothing —
        // identical, not merely close: x × 1.0 == x.
        Assert.Equal(g0, Produce(1.0, null).Grain);
        Assert.Equal(l0, Produce(1.0, null).Livestock);
    }

    [Fact]
    public void D_DtExact_FiveYearFailure()
    {
        // The loss GIVEN onset is dt-exact: a 5-year failure at s = 1 on a
        // constant-yield land-bound rig costs exactly 5 years of food at dt 10
        // (one turn at multiplier 0.5) and at dt 0.5 (ten turns at 0), to the
        // unit. The rig seeds PREV with the row the onset turn writes and lets
        // the system carry it from there at λ = 0.
        SimConfig cfg = WithDisaster(TestConfigs.Sim(), 0.0, sMin: 1.0, sMax: 1.0);
        const double yieldPerYear = 1300.0;   // 50 km² × 26.0

        long ProducedOver(double dt, int turns, DisasterRow? seeded)
        {
            var counts = new long[Cohorts.Count];
            counts[5] = 1000;
            WorldState w = PopulationExactnessTests.BucketWorld(counts);
            w.CatchmentSummaries.Add(new CatchmentSummaryRow(S0, NodeCount: 1,
                EffectiveArableKm2: yieldPerYear / cfg.Farming.YieldPerArableKm2PerYear, NetworkRevision: 0, LastRecomputeTurn: 0));
            w.SectorAllocations.Add(new SectorAllocationRow(S0, 1.0, 0.0, 0.0, 0.0, 0.0));
            foreach (GoodEntry g in cfg.Goods!.Goods)
                w.GoodStocks.Add(new GoodStockRow(S0, new GoodId(g.Id), Conserved.Zero, 0.0, 0.0));
            if (seeded is DisasterRow r) w.Disasters.Add(r);
            var exec = new TurnExecutor(FlatEra(dt), [SystemCatalog.Disaster(cfg), SystemCatalog.Production(cfg)]);
            for (int t = 0; t < turns; t++) w = exec.Step(w);
            int grain = cfg.Goods!.GrainId;
            for (int i = 0; i < w.GoodStocks.Count; i++)
                if (w.GoodStocks[i].Good.Value == grain) return w.GoodStocks[i].Amount.Value;
            return 0;
        }

        long base10 = ProducedOver(10.0, 1, null);
        long base05 = ProducedOver(0.5, 20, null);
        Assert.Equal(13_000, base10);
        Assert.Equal(13_000, base05);

        long struck10 = ProducedOver(10.0, 1, new DisasterRow(S0, 1, 1.0, 0.0, 0.5, 1.0));
        long struck05 = ProducedOver(0.5, 20, new DisasterRow(S0, 1, 1.0, 4.5, 0.0, 1.0));
        Assert.Equal(base10 - struck10, base05 - struck05);
        Assert.Equal(5 * 1300, base10 - struck10);
    }

    // ======================================================================
    // THE dt DIFFERENCE — PINNED AS A DIFFERENCE, NEVER CLAIMED EQUAL
    // ======================================================================

    /// <summary>One settlement, ρ = 1.3 land-bound, Default sector mix, weather
    /// off (no weather system), grain store G = 1.5 y of demand, no deposits.
    /// The strike is forced at s = 1 on turn 1 and λ is 0 thereafter.</summary>
    private static (List<double> Deficit, List<FoodStateKind> Kind) RunDtRig(SimConfig cfg, double dt, int turns)
    {
        SimConfig strike = WithDisaster(cfg, 1e6, sMin: 1.0, sMax: 1.0);
        SimConfig quiet = WithDisaster(cfg, 0.0, sMin: 1.0, sMax: 1.0);
        var counts = new long[Cohorts.Count];
        counts[5] = 1000;
        WorldState w = PopulationExactnessTests.BucketWorld(counts);
        w.CatchmentSummaries.Add(new CatchmentSummaryRow(S0, NodeCount: 1,
            EffectiveArableKm2: 1.3 * 1000.0 / cfg.Farming.YieldPerArableKm2PerYear, NetworkRevision: 0, LastRecomputeTurn: 0));
        var ledger = new Ledger(w.LedgerFlows);
        foreach (GoodEntry g in cfg.Goods!.Goods)
        {
            int row = w.GoodStocks.Add(new GoodStockRow(S0, new GoodId(g.Id), Conserved.Zero, 0.0, 0.0));
            if (g.Id == cfg.Goods!.GrainId)
                ledger.Flow(ref w.GoodStocks.Ref(row).Amount, ConservedQuantityIds.OfGood(new GoodId(g.Id)),
                    ReasonIds.InitialEndowment, 1500, FlowDirection.Source, OverdrawPolicy.Throw);
        }
        SystemRegistration[] Pipeline(SimConfig c) =>
            [SystemCatalog.Disaster(c), SystemCatalog.Production(c), SystemCatalog.Consumption(c)];
        var deficits = new List<double>();
        var kinds = new List<FoodStateKind>();
        TurnExecutor first = new(FlatEra(dt), Pipeline(strike));
        TurnExecutor rest = new(FlatEra(dt), Pipeline(quiet));
        for (int t = 1; t <= turns; t++)
        {
            w = (t == 1 ? first : rest).Step(w);
            deficits.Add(FoodState.DeficitRatio(w, S0));
            kinds.Add(FoodState.Of(w, S0, cfg, out _));
        }
        return (deficits, kinds);
    }

    [Fact]
    public void D_Classification_DtDifference_Pinned()
    {
        // The SAME physical event — a 5-year total failure at ρ = 1.3 behind a
        // 1.5-year granary — classifies DIFFERENTLY at dt 10 and dt 0.5, because
        // the coarse turn pools a decade's surplus and spreads a small deficit
        // over ten years while the fine turn concentrates a total one over
        // three and a half (F4, the dt-vs-buffer artefact; CR-015 G8). Both are
        // pinned SEPARATELY; nothing here says they agree.
        //
        // Spoilage OFF isolates the artefact and reproduces the spec's stated
        // numbers exactly; the shipped-spoilage arm below pins what the shipped
        // store does with the same event.
        SimConfig cfg = TestConfigs.Sim();
        SimConfig noSpoil = cfg with { Consumption = cfg.Consumption with { GrainSpoilagePerYear = 0.0 } };

        (List<double> d10, List<FoodStateKind> k10) = RunDtRig(noSpoil, 10.0, 6);
        int famine10 = 0;
        for (int i = 0; i < k10.Count; i++) if (k10[i] == FoodStateKind.Famine) famine10++;
        Assert.Equal(1, famine10);
        Assert.Equal(FoodStateKind.Famine, k10[1]);            // the state after turn 2 — the applied turn
        Assert.InRange(d10[1], 0.19, 0.21);                     // d = ρ(sD − sD*)/10 = 1.3(5 − 3.46)/10 = 0.20
        Assert.Equal(0.0, d10[0]);
        for (int i = 2; i < d10.Count; i++) Assert.Equal(0.0, d10[i]);

        (List<double> d05, List<FoodStateKind> k05) = RunDtRig(noSpoil, 0.5, 16);
        // Applied window: states 2..11 (ten turns × 0.5 y). The store lasts
        // G/(1 − ρ(1 − s)) = 1.5 y = three turns at d = 0, then d = 1.0 for the
        // seven remaining active turns.
        int famine05 = 0, atOne = 0, atZeroInWindow = 0;
        for (int i = 1; i <= 10; i++)
        {
            if (k05[i] == FoodStateKind.Famine) famine05++;
            if (d05[i] == 1.0) atOne++;
            if (d05[i] == 0.0) atZeroInWindow++;
        }
        Assert.Equal(3, atZeroInWindow);
        Assert.Equal(7, atOne);
        Assert.Equal(7, famine05);
        for (int i = 1; i <= 3; i++) Assert.Equal(FoodStateKind.Normal, k05[i]);
        for (int i = 4; i <= 10; i++) Assert.Equal(FoodStateKind.Famine, k05[i]);
        Assert.Equal(0.0, d05[11]);                              // first restored harvest
        Assert.Equal(FoodStateKind.Normal, k05[11]);

        // THE DIFFERENCE, stated: one FAMINE turn at d ≈ 0.20 versus seven at
        // d = 1.0 — the decade absorbs 80% of what the half-year cannot.
        Assert.NotEqual(famine10, famine05);
        Assert.True(d10[1] < 0.25 && d05[10] == 1.0);

        // SHIPPED SPOILAGE (0.08/yr): the coarse arm is unchanged (people eat
        // before the store spoils and the whole store is eaten in the struck
        // decade); the fine arm loses part of a turn to spoilage — two turns at
        // d = 0, one partial, then the same seven at d = 1.0. Pinned as measured.
        (List<double> s10, List<FoodStateKind> sk10) = RunDtRig(cfg, 10.0, 6);
        Assert.Equal(FoodStateKind.Famine, sk10[1]);
        Assert.InRange(s10[1], 0.19, 0.21);
        (List<double> s05, List<FoodStateKind> sk05) = RunDtRig(cfg, 0.5, 16);
        int sAtOne = 0, sAtZero = 0, sPartial = 0, sFamine = 0;
        for (int i = 1; i <= 10; i++)
        {
            if (sk05[i] == FoodStateKind.Famine) sFamine++;
            if (s05[i] == 1.0) sAtOne++;
            else if (s05[i] == 0.0) sAtZero++;
            else sPartial++;
        }
        Assert.Equal(7, sAtOne);
        Assert.Equal(2, sAtZero);
        Assert.Equal(1, sPartial);
        Assert.Equal(8, sFamine);
    }

    // ======================================================================
    // THE ONSET PROCESS
    // ======================================================================

    [Fact]
    public void D_OnsetProcess_BiasesWithinStatement()
    {
        // 20 seeds × 300 turns, 300 settlements, disaster system alone, at the
        // ARMED hazard λ = 0.01 — the value the biases are STATED at. Onsets per
        // settlement-century: at dt 0.5 the 5-year refractory gives an effective
        // rate λ/(1 + λD) = 0.952 λ, within [0.90, 1.00] × λ·100; at dt 10 the
        // once-per-turn truncation gives P(≥1 onset in 10 y)/λ·10 = 0.952,
        // within [0.93, 1.00]. (The spec's "λ = 0.05 for power" sits OUTSIDE its
        // own bands by its own formulas — 0.80 and 0.79 — so the elevated rate
        // is not used; power comes from settlements instead.)
        SimConfig cfg = WithDisaster(TestConfigs.Sim(), 0.01);
        const int settlements = 300, turns = 300, seeds = 20;
        double D = cfg.Disaster.DurationYears;

        double OnsetsPerSettlementCentury(double dt)
        {
            long onsets = 0;
            double onsetRemaining = D - Math.Min(D, dt);   // RemainingYears the onset turn writes
            for (ulong seed = 1; seed <= seeds; seed++)
            {
                WorldState w = Settlements(seed, settlements);
                var exec = new TurnExecutor(FlatEra(dt), [SystemCatalog.Disaster(cfg)]);
                for (int t = 0; t < turns; t++)
                {
                    w = exec.Step(w);
                    for (int i = 0; i < w.Disasters.Count; i++)
                    {
                        DisasterRow r = w.Disasters[i];
                        if (r.Kind == DisasterSystem.KindCropFailure && r.RemainingYears == onsetRemaining) onsets++;
                    }
                }
            }
            double settlementYears = (double)seeds * settlements * turns * dt;
            return onsets / (settlementYears / 100.0);
        }

        double expected = cfg.Disaster.HazardPerYear * 100.0;   // 1 per settlement-century
        double fine = OnsetsPerSettlementCentury(0.5);
        double coarse = OnsetsPerSettlementCentury(10.0);
        Assert.InRange(fine / expected, 0.90, 1.00);
        Assert.InRange(coarse / expected, 0.93, 1.00);
        // Neither is unbiased (the statement is of a bias, not of exactness),
        // and the fine bias is the renewal's 1/(1 + λD) to within sampling.
        Assert.InRange(fine / expected, 0.93, 0.975);
        Assert.InRange(coarse / expected, 0.94, 0.965);
    }

    // ======================================================================
    // PERSISTENCE AND DETERMINISM
    // ======================================================================

    [Fact]
    public void D_PersistsAcrossSaveLoad()
    {
        // The FoundedHarness save/load-continue leg with an ACTIVE row in the
        // saved state: a 40-year event at λ = ∞ means every settlement carries
        // RemainingYears > 0 (10 y after three dt-10 turns) at the save, and the
        // continued and reloaded branches must hash equal every turn.
        SimConfig cfg = WithDisaster(TestConfigs.Sim(), 1e6, duration: 40.0);
        TurnExecutor exec = CanonicalExecutor(cfg);
        WorldState live = DevFounded(cfg);
        for (int t = 1; t <= 3; t++) live = exec.Step(live);
        Assert.Equal(live.Settlements.Count, live.Disasters.Count);
        for (int i = 0; i < live.Disasters.Count; i++) Assert.True(live.Disasters[i].RemainingYears > 0.0);

        using var buffer = new MemoryStream();
        Snapshot.Save(live, buffer);
        buffer.Position = 0;
        TerrainSet regenerated = Sim.Core.Worldgen.Worldgen.Generate(TestConfigs.DevWorldgen(), Seed);
        WorldState loaded = Snapshot.Load(buffer, regenerated);
        Assert.Equal(WorldHash.ComputeHex(live), WorldHash.ComputeHex(loaded));
        Assert.True(WorldStates.StateEquals(live, loaded));

        TurnExecutor execLoaded = CanonicalExecutor(cfg);
        for (int t = 4; t <= 8; t++)
        {
            live = exec.Step(live);
            loaded = execLoaded.Step(loaded);
            Assert.Equal(WorldHash.ComputeHex(live), WorldHash.ComputeHex(loaded));
        }
        Assert.True(live.Disasters.Count > 0);
    }

    [Fact]
    public void D_Replay_Deterministic()
    {
        // Live vs replay with λ > 0: the same seed, config and order log through
        // two independent executors hash equal at every turn, and rows exist.
        SimConfig cfg = WithDisaster(TestConfigs.Sim(), 0.5);
        static OrderLog Orders()
        {
            var log = new OrderLog();
            log.Append(new OrderRecord(3, ActorId: 1, OrderKind.LaborAllocation, 0, 40.0));
            return log;
        }
        TurnExecutor a = CanonicalExecutor(cfg, Orders());
        TurnExecutor b = CanonicalExecutor(cfg, Orders());
        WorldState wa = DevFounded(cfg), wb = DevFounded(cfg);
        int rowsSeen = 0;
        for (int t = 1; t <= 20; t++)
        {
            wa = a.Step(wa);
            wb = b.Step(wb);
            Assert.Equal(WorldHash.ComputeHex(wa), WorldHash.ComputeHex(wb));
            rowsSeen += wa.Disasters.Count;
        }
        Assert.True(rowsSeen > 0, "no disaster rows in 20 turns at λ = 0.5 — rig vacuous");
    }

    // ======================================================================
    // SCHEMA v25
    // ======================================================================

    [Fact]
    public void SchemaV25_PopulatedDisasterTable_LengthAndRoundTripExact()
    {
        // Constitution rule: every new serialized row type ships a POPULATED-
        // table test — exact ExpectedLength, bit-exact round trip, hash equality.
        // Three rows with distinct values per field, including the smallest
        // denormal (5e-324), a negative zero, and a non-dyadic double
        // (0.29720704310868246) that any normalisation would move.
        var world = new WorldState(11);
        world.Disasters.Add(new DisasterRow(new SettlementId(2), 1, 0.75, 4.5, 0.29720704310868246, 1.0));
        world.Disasters.Add(new DisasterRow(new SettlementId(1), 0, 0.0, 0.0, 1.0, 0.625));
        world.Disasters.Add(new DisasterRow(new SettlementId(2), 7, 5e-324, -0.0, 0.9560546875, 0.4375));

        Assert.Equal(25, CanonicalSchema.Version);

        using var ms = new MemoryStream();
        using (var writer = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
            CanonicalSchema.Write(world, writer);
        Assert.Equal(CanonicalSchema.ExpectedLength(world), ms.Length);
        // The table's own contribution: a 4-byte count and three 40-byte rows.
        var empty = new WorldState(11);
        Assert.Equal(3 * 40, CanonicalSchema.ExpectedLength(world) - CanonicalSchema.ExpectedLength(empty));

        ms.Position = 0;
        using var reader = new BinaryReader(ms);
        WorldState back = CanonicalSchema.Read(reader);
        Assert.True(WorldStates.StateEquals(world, back), "round-trip drifted");
        Assert.Equal(WorldHash.ComputeHex(world), WorldHash.ComputeHex(back));

        Assert.Equal(3, back.Disasters.Count);
        for (int i = 0; i < 3; i++)
        {
            DisasterRow a = world.Disasters[i], b = back.Disasters[i];
            Assert.Equal(a.Settlement, b.Settlement);
            Assert.Equal(a.Kind, b.Kind);
            Assert.Equal(BitConverter.DoubleToInt64Bits(a.Severity), BitConverter.DoubleToInt64Bits(b.Severity));
            Assert.Equal(BitConverter.DoubleToInt64Bits(a.RemainingYears), BitConverter.DoubleToInt64Bits(b.RemainingYears));
            Assert.Equal(BitConverter.DoubleToInt64Bits(a.Multiplier), BitConverter.DoubleToInt64Bits(b.Multiplier));
            Assert.Equal(BitConverter.DoubleToInt64Bits(a.AppliedMultiplier), BitConverter.DoubleToInt64Bits(b.AppliedMultiplier));
        }
        Assert.True(double.IsNegative(back.Disasters[2].RemainingYears));   // −0.0 survived
        Assert.Equal(5e-324, back.Disasters[2].Severity);

        // A dropped or reordered field is visible: a world differing in ONE
        // field of one row hashes differently.
        var twin = new WorldState(11);
        twin.Disasters.Add(world.Disasters[0]);
        twin.Disasters.Add(world.Disasters[1]);
        twin.Disasters.Add(world.Disasters[2] with { AppliedMultiplier = 0.4376 });
        Assert.NotEqual(WorldHash.ComputeHex(world), WorldHash.ComputeHex(twin));
        Assert.False(WorldStates.StateEquals(world, twin));
    }
}
