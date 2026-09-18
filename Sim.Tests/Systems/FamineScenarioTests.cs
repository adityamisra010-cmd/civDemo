using System.Globalization;
using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Disaster;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

/// <summary>
/// T4.21-4 — THE SCENARIO BATTERY (spec §6.5; CR-015 G6(a), N7, N9; ADR-024).
///
/// The full-pipeline arm of "FAMINE IS EXCEPTIONAL". The unit arms live
/// elsewhere: <see cref="FoodStateTests"/> classifies hand rows,
/// <see cref="DisasterSystemTests"/> pins the onset process and the strip
/// control, <see cref="DemographyRetuneTests"/> pins the demographic response.
/// What is NOT provable from any of those is the PROPOSITION the director's
/// mandate actually makes, which is about whole worlds: that ordinary weather
/// never produces famine, that famine-class disasters produce it at the rate
/// the hazard says, that the FOOD BALANCE and not the event decides, and that
/// every famine has a named cause.
///
/// EVERY NUMBER IN THIS FILE WAS MEASURED ON THIS TREE by the packet that wrote
/// it (ADR-015 §6); the 20-seed canonical evidence it is drawn from is
/// docs/t4.21-4-record.md §2 with the raw rows in docs/t4.21-evidence/t4.21-4/.
///
/// COST CONTROL, stated rather than hidden. The two 20-seed sweeps run on the
/// DEV preset (256², N = 4), not the canonical 1024² world: the onset process
/// reads only λ and dt and is preset-independent BY CONSTRUCTION
/// (DisasterSystem.Step touches no terrain, no population and no food), so the
/// frequency claim is preset-free, while a canonical 20-seed sweep costs ~11
/// minutes of suite time. The canonical sweep WAS run — it is the packet's
/// recorded evidence (6,211 onsets over 66,000 settlement-turns = 0.941061 per
/// settlement-century) — and the single-seed canonical arm below re-measures it
/// in the suite at seed 42.
///
/// T4.21-7 — WHAT λ THIS BATTERY RUNS AT, AND WHY IT IS NOT THE SHIPPED ONE.
/// The famine-class disaster ships COMPLETE, TESTED AND INERT: sim.json
/// disaster.hazardPerYear is 0.0, and the RATE is the director's ruling on
/// docs/adr/cr-016-armed-disaster-fallout.md (T4.21-4 armed it at the derived
/// 0.01, MEASURED the fallout — a world negative by construction and a broken
/// PERMANENT dt-invariance detonator — and escalated it). Every arm here that
/// needs a live disaster therefore sets λ IN-RIG through <c>ArmedRig()</c> at
/// the derived 0.01, so the MECHANISM stays proven and none of these arms goes
/// vacuous at the shipping value; the arms that need λ = 0 (S_WeatherOnly,
/// S_EraGate, the sector rigs) set that in-rig too, and have done since
/// T4.21-4. The one arm that deliberately runs the SHIPPED config is
/// S_Abandonment_TriggersFamine — mandate item 1(B), the cause that is
/// reachable in play today.
/// </summary>
public class FamineScenarioTests
{
    private const ulong CanonicalSeed = 42;

    private static SimConfig WithHazard(SimConfig cfg, double lambda) =>
        cfg with { Disaster = cfg.Disaster with { HazardPerYear = lambda } };

    /// <summary>
    /// T4.21-7 — THE ARMED RIG, AND WHY IT IS A RIG AND NOT THE SHIPPED CONFIG.
    /// The famine-class disaster ships COMPLETE AND TESTED BUT INERT: sim.json
    /// disaster.hazardPerYear is 0.0 and the RATE is the director's ruling on
    /// docs/adr/cr-016-armed-disaster-fallout.md. The derived rate is 0.01 and
    /// T4.21-4 measured the whole world at it; this battery keeps MEASURING at
    /// that rate — because the mandate's proposition is about a world with the
    /// disaster in it — and does so by setting λ IN-RIG rather than by leaning
    /// on the shipped value. Every arm that needs a live disaster uses this, so
    /// a re-arming (or a different ruled rate) changes the shipped world without
    /// silently changing what this battery measures, and disarming leaves none
    /// of these arms vacuous. The SHIPPED value is asserted here, where a silent
    /// re-arming would matter.
    /// </summary>
    private const double ArmedLambda = 0.01;

    private static SimConfig ArmedRig()
    {
        SimConfig shipped = TestConfigs.Sim();
        Assert.Equal(0.0, shipped.Disaster.HazardPerYear);   // CR-016: inert, one data edit from live
        return WithHazard(shipped, ArmedLambda);
    }

    private static SimConfig WeatherOff(SimConfig cfg) =>
        cfg with { HarvestVariance = cfg.HarvestVariance with { SigmaLogYield = 0.0 } };

    private static TurnExecutor Executor(SimConfig cfg, WorldgenConfig wg, OrderLog? orders = null)
    {
        using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
        using var pipeStream = Sim.Data.DataFiles.OpenPipeline();
        return new TurnExecutor(EraTableLoader.Load(eraStream),
            PipelineLoader.Load(pipeStream, SystemCatalog.All(cfg, wg)), orders);
    }

    private static string Inv(double v) => v.ToString("G9", CultureInfo.InvariantCulture);

    /// <summary>What a run of the world produced, classified on the world the
    /// kernel READ each turn (PREV) — exactly the read every consumer makes.</summary>
    private sealed class Census
    {
        public int Famine, Severe, Stress, Normal;
        public int FamineNone, FamineDisaster, FamineAbandonment, FamineBoth;
        public int Onsets;
        public double SettlementYears;
        public long Starvation;
        /// <summary>Starvation on the step after a PREV world whose HIGHEST class
        /// was at most STRESS — V2's empirical arm. Must be 0.</summary>
        public long StarvationBelowSevere;
        public int TurnsBelowSevere;
        /// <summary>max d_eff over every STRESS settlement-turn — V2 by
        /// construction. Must be exactly 0.0.</summary>
        public double MaxStressEffectiveDeficit;
        public long FinalPopulation;

        /// <summary>T4.21-6 (spec §6.5 row 1, §7) — THE PER-SETTLEMENT ρ SERIES,
        /// accumulated so it can be REPORTED. ρ_t is the foundations audit's own
        /// definition (docs/t4.21-1-foundations-audit.md §2): grain
        /// LastProducedUnits ÷ ConsumptionDeficitRow.DemandUnits, counted only on
        /// turns where both are positive (the founding turn harvests nothing).
        /// Indexed BY SETTLEMENT ID, in arrays, so the order is the table's and
        /// not a dictionary's (law 5). Reported, never asserted: ρ_ship = 1.3 is
        /// the whole declared input of §3.3's derivation, so the one number the
        /// director needs beside it is where the shipped world's ρ actually
        /// sits.</summary>
        public readonly List<double> RhoSum = [];
        public readonly List<int> RhoCount = [];

        public void AddRho(int settlementId, double rho)
        {
            while (RhoSum.Count <= settlementId) { RhoSum.Add(0.0); RhoCount.Add(0); }
            RhoSum[settlementId] += rho;
            RhoCount[settlementId]++;
        }

        /// <summary>Each settlement's TIME-MEAN ρ, in ascending value order with
        /// the settlement id as a stable integer tie-break (CLAUDE.md: any
        /// ordering over doubles ships a composite key).</summary>
        public (int Id, double Mean)[] RhoMeans()
        {
            var list = new List<(int Id, double Mean)>();
            for (int id = 0; id < RhoSum.Count; id++)
                if (RhoCount[id] > 0) list.Add((id, RhoSum[id] / RhoCount[id]));
            (int Id, double Mean)[] a = [.. list];
            Array.Sort(a, static (x, y) => x.Mean != y.Mean ? x.Mean.CompareTo(y.Mean) : x.Id.CompareTo(y.Id));
            return a;
        }

        public int SettlementTurns => Famine + Severe + Stress + Normal;
        public double OnsetsPerSettlementCentury => Onsets / (SettlementYears / 100.0);
    }

    private static Census Run(SimConfig cfg, WorldgenConfig wg, ulong seed, int turns,
        OrderLog? orders = null, int? settlements = null)
    {
        TurnExecutor exec = Executor(cfg, wg, orders);
        WorldState world = WorldFounding.Found(wg, cfg, seed, settlements);
        var c = new Census();
        long prevStarvCum = 0;
        for (int t = 1; t <= turns; t++)
        {
            WorldState next = exec.Step(world);

            for (int r = 0; r < next.Disasters.Count; r++)
            {
                DisasterRow row = next.Disasters[r];
                if (row.Kind != DisasterSystem.KindCropFailure) continue;
                bool wasActive = false;
                for (int q = 0; q < world.Disasters.Count; q++)
                    if (world.Disasters[q].Settlement == row.Settlement)
                    { wasActive = world.Disasters[q].RemainingYears > 0.0; break; }
                if (!wasActive) c.Onsets++;
            }

            long cum = 0;
            for (int i = 0; i < next.LedgerFlows.Count; i++)
                if (next.LedgerFlows[i].Quantity == ConservedQuantityIds.Population
                    && next.LedgerFlows[i].Reason == ReasonIds.Starvation)
                    cum = next.LedgerFlows[i].TotalSunk;
            long starvedThisStep = cum - prevStarvCum;
            prevStarvCum = cum;
            c.Starvation += starvedThisStep;

            var grain = new GoodId(cfg.Goods!.GrainId);
            FoodStateKind worst = FoodStateKind.Normal;
            for (int i = 0; i < world.Settlements.Count; i++)
            {
                SettlementId sid = world.Settlements[i].Id;

                // ρ_t for the reported series — the foundations audit's definition.
                int gi = GoodStockIndex.IndexOf(world.GoodStocks, sid, grain);
                long produced = gi >= 0 ? world.GoodStocks[gi].LastProducedUnits : 0;
                long demand = 0;
                for (int q = 0; q < world.ConsumptionDeficits.Count; q++)
                    if (world.ConsumptionDeficits[q].Settlement == sid)
                    { demand = world.ConsumptionDeficits[q].DemandUnits; break; }
                if (produced > 0 && demand > 0) c.AddRho(sid.Value, (double)produced / demand);

                FoodStateKind k = FoodState.Of(world, sid, cfg, out FamineReason reason);
                if (k > worst) worst = k;
                switch (k)
                {
                    case FoodStateKind.Famine:
                        c.Famine++;
                        if (reason == FamineReason.None) c.FamineNone++;
                        else if (reason == FamineReason.Disaster) c.FamineDisaster++;
                        else if (reason == FamineReason.Abandonment) c.FamineAbandonment++;
                        else c.FamineBoth++;
                        break;
                    case FoodStateKind.Severe: c.Severe++; break;
                    case FoodStateKind.Stress:
                        c.Stress++;
                        c.MaxStressEffectiveDeficit = Math.Max(c.MaxStressEffectiveDeficit,
                            FoodState.EffectiveDeficit(FoodState.DeficitRatio(world, sid), k, cfg));
                        break;
                    default: c.Normal++; break;
                }
            }
            if (worst <= FoodStateKind.Stress)
            {
                c.TurnsBelowSevere++;
                c.StarvationBelowSevere += starvedThisStep;
            }

            c.SettlementYears += world.Settlements.Count * next.Clock.DtYears;
            world = next;
        }
        for (int i = 0; i < world.Buckets.Count; i++) c.FinalPopulation += world.Buckets[i].Count.Value;
        return c;
    }

    // ======================================================================
    // §6.5 — no famine without a cause
    // ======================================================================

    [Fact]
    public void S_Seed42_NoFamineWithoutCause()
    {
        // The canonical founded world, 300 turns, λ = 0.01 SET IN-RIG (T4.21-7:
        // the shipped value is 0.0 — the mechanism ships inert and the rate is
        // the director's, CR-016 — so this arm supplies its own hazard rather
        // than inheriting one). No orders: every famine here is the disaster's,
        // and every one of them must carry a reason. V1 is true BY CONSTRUCTION
        // (FoodState.Of assigns the reason inside the `struck || abandoned`
        // branch) — it is measured anyway, because "by construction" is a claim
        // about code and this is a claim about a world.
        SimConfig cfg = ArmedRig();
        Census c = Run(cfg, TestConfigs.Worldgen(), CanonicalSeed, 300);

        Assert.Equal(0, c.FamineNone);
        Assert.True(c.Famine > 0,
            "no FAMINE settlement-turn in 300 canonical turns — the battery is vacuous, "
            + "either because the rig's hazard stopped arming the disaster or because no strike "
            + "ever beat a granary");

        // V2, the by-construction arm: STRESS is the band adaptation absorbs, so
        // the effective deficit — starvation mortality's ONE input outside
        // FAMINE — is exactly zero on every STRESS settlement-turn.
        Assert.Equal(0.0, c.MaxStressEffectiveDeficit);
        // V2, the empirical arm: no turn whose PREV world held nobody above
        // STRESS killed anybody by starvation.
        Assert.Equal(0, c.StarvationBelowSevere);
        Assert.True(c.TurnsBelowSevere > 100,
            $"only {c.TurnsBelowSevere} turns with nothing above STRESS — the V2 arm is thin");

        // REPORTED, not asserted (spec §6.5): the shape of the armed world —
        // the world AT λ = 0.01, which is NOT the shipping value (CR-016).
        Console.WriteLine(
            $"S_Seed42_NoFamineWithoutCause: {c.SettlementTurns} settlement-turns — FAMINE {c.Famine} "
            + $"(disaster {c.FamineDisaster}, abandonment {c.FamineAbandonment}, both {c.FamineBoth}, "
            + $"NONE {c.FamineNone}), SEVERE {c.Severe}, STRESS {c.Stress}, NORMAL {c.Normal}; "
            + $"{c.Onsets} onsets over {Inv(c.SettlementYears)} settlement-years = "
            + $"{Inv(c.OnsetsPerSettlementCentury)}/settlement-century; starvation {c.Starvation}; "
            + $"final population {c.FinalPopulation}.");

        // REPORTED, not asserted (spec §6.5 row 1, §7): THE PER-SETTLEMENT ρ
        // SERIES. ρ_ship = 1.3 is the whole declared input of the §3.3
        // derivation, so this is the number the director checks that derivation
        // against — and until T4.21-6 it was the one number this test did not
        // print. The distribution over the full 300 turns, per settlement and
        // in full, is docs/t4.21-1-foundations-audit.md §4; what is printed here
        // is the same quantity on THIS run, so the two are comparable.
        (int Id, double Mean)[] rho = c.RhoMeans();
        Assert.True(rho.Length > 0, "no settlement produced a ρ series — the report is empty");
        var sb = new System.Text.StringBuilder();
        sb.Append(CultureInfo.InvariantCulture,
            $"S_Seed42_NoFamineWithoutCause ρ (grain produced / demand, time-mean per settlement, "
            + $"{rho.Length} settlements): min {Inv(rho[0].Mean)} (s{rho[0].Id}), median "
            + $"{Inv(rho[rho.Length / 2].Mean)}, max {Inv(rho[^1].Mean)} (s{rho[^1].Id}); "
            + $"ρ_ship = 1.3. Full series:");
        for (int i = 0; i < rho.Length; i++)
            sb.Append(CultureInfo.InvariantCulture, $" s{rho[i].Id}={Inv(rho[i].Mean)}");
        Console.WriteLine(sb.ToString());
    }

    // ======================================================================
    // §6.5 — the frequency matches the hazard
    // ======================================================================

    [Fact]
    public void S_TwentySeeds_FamineFrequencyMatchesHazard()
    {
        // THE BAND IS COMPUTED, NOT PINNED. The onset process is a Bernoulli
        // draw per settlement-turn at p = 1 − e^{−λ·dt} (DisasterSystem.Step,
        // the exact integration of a per-year hazard), so the expected onset
        // count over N settlement-turns is N·p and the 99 % binomial band is
        // N·p ± 2.5758·sqrt(N·p(1−p)). Both are computed below from the
        // measured N, so no literal can rot.
        //
        // AGAINST λ·100 = 1.0: the once-per-turn booking truncation is exactly
        // p/(λ·dt) = (1 − e^{−λdt})/(λdt) = 0.9516258 at λ = 0.01, dt = 10 —
        // the ≤ 5 % bias the disaster._doc states and CR-015 §3.3 accepts. The
        // band is therefore centred on the TRUNCATION-CORRECTED expectation and
        // the raw λ·100 comparison is reported.
        //
        // T4.21-7: λ = 0.01 is SET IN-RIG. It is the DERIVED rate and the one
        // the biases are stated at; it is not the shipped value (0.0 — the
        // mechanism ships inert, CR-016). The band is computed from the rig's
        // own λ, so this test measures the onset process at whatever rate the
        // rig arms, and would measure a newly ruled rate by changing one
        // constant here.
        SimConfig cfg = ArmedRig();
        WorldgenConfig wg = TestConfigs.DevWorldgen();
        int onsets = 0;
        double settlementYears = 0.0;
        int famine = 0, famineNone = 0;
        for (ulong seed = 1; seed <= 20; seed++)
        {
            Census c = Run(cfg, wg, seed, 300);
            onsets += c.Onsets;
            settlementYears += c.SettlementYears;
            famine += c.Famine;
            famineNone += c.FamineNone;
        }
        Assert.Equal(0, famineNone);

        const double dt = 10.0;                 // the Neolithic band covers turns 1–250 and dominates
        double p = 1.0 - Math.Exp(-cfg.Disaster.HazardPerYear * dt);
        double trials = settlementYears / dt;   // settlement-TURNS
        double expected = trials * p;
        double sd = Math.Sqrt(trials * p * (1.0 - p));
        const double z99 = 2.5758293035489004;
        double lo = expected - z99 * sd, hi = expected + z99 * sd;

        Console.WriteLine(
            $"S_TwentySeeds_FamineFrequencyMatchesHazard (dev preset): {onsets} onsets over "
            + $"{Inv(settlementYears)} settlement-years ({Inv(trials)} settlement-turns) = "
            + $"{Inv(onsets / (settlementYears / 100.0))}/settlement-century; truncation-corrected "
            + $"expectation {Inv(expected / (settlementYears / 100.0))}, binomial 99 % band "
            + $"[{Inv(lo / (settlementYears / 100.0))}, {Inv(hi / (settlementYears / 100.0))}]; "
            + $"truncation factor {Inv(p / (cfg.Disaster.HazardPerYear * dt))} against λ·100 = "
            + $"{Inv(cfg.Disaster.HazardPerYear * 100.0)}; FAMINE settlement-turns {famine}.");

        Assert.True(onsets >= lo && onsets <= hi,
            $"{onsets} onsets outside the binomial 99 % band [{lo:F1}, {hi:F1}] around {expected:F1} "
            + $"— the onset rate no longer matches λ = {Inv(cfg.Disaster.HazardPerYear)} at dt {dt}");
        Assert.True(famine > 0, "no FAMINE settlement-turn across 20 seeds — the sweep is vacuous");
    }

    // ======================================================================
    // §6.5 — ordinary weather is NEVER famine (and still bites)
    // ======================================================================

    [Fact]
    public void S_WeatherOnly_NeverFamine()
    {
        // THE MANDATE'S PROPOSITION, measured directly: with λ = 0 and no
        // orders there is no cause in the world, so there can be no FAMINE —
        // however bad the weather gets. The NON-VACUITY GUARD is the other half
        // and is the part that makes it a claim rather than a tautology:
        // ordinary weather must still produce SHORTAGE (STRESS/SEVERE
        // settlement-turns), i.e. the food balance is live and the zero famine
        // count is not "nothing ever went wrong".
        SimConfig cfg = WithHazard(TestConfigs.Sim(), 0.0);
        WorldgenConfig wg = TestConfigs.DevWorldgen();
        int famine = 0, severe = 0, stress = 0;
        long starvation = 0, starvationBelowSevere = 0;
        for (ulong seed = 1; seed <= 20; seed++)
        {
            Census c = Run(cfg, wg, seed, 300);
            famine += c.Famine; severe += c.Severe; stress += c.Stress;
            starvation += c.Starvation; starvationBelowSevere += c.StarvationBelowSevere;
            Assert.Equal(0, c.Onsets);
            Assert.Equal(0.0, c.MaxStressEffectiveDeficit);
        }
        Console.WriteLine(
            $"S_WeatherOnly_NeverFamine (dev preset, λ = 0): FAMINE {famine}, SEVERE {severe}, "
            + $"STRESS {stress}, starvation {starvation} (below-SEVERE turns: {starvationBelowSevere}).");

        Assert.Equal(0, famine);
        Assert.True(stress + severe > 0,
            "λ = 0 produced no STRESS and no SEVERE settlement-turn either — the 'weather is never "
            + "famine' claim is vacuous here, because weather produced no shortage at all");
        Assert.Equal(0, starvationBelowSevere);
    }

    // ======================================================================
    // §6.5 — the disaster triggers famine, and the BALANCE decides
    // ======================================================================

    [Fact]
    public void S_Disaster_TriggersFamine()
    {
        // ONE forced strike, THREE settlements. λ is forced so the strike is
        // certain (a config twin — the same instrument F_DisasterTiming_TurnExact
        // uses); it then lands on every rig identically, and the outcome differs
        // because the BALANCE differs. FAMINE iff d > 0.
        //
        // WHICH VARIABLE DECIDES, ISOLATED (T4.21-6). The first two arms differ
        // in BOTH ρ and store (1.3 / 0.1 y against 2.0 / 1.5 y), so on their own
        // they cannot tell "a rich granary absorbs it" from "the surplus ratio
        // absorbs it". The THIRD arm holds the store at the THIN value and moves
        // only ρ, and it is the arm that settles it: ρ = 2.0 behind a 0.1-year
        // store absorbs the same strike exactly as the full granary does. The
        // SURPLUS RATIO is the discriminator at dt 10 — grain spoilage at
        // 0.08/yr eats most of a decade-scale store before the strike lands, so
        // the store barely moves d. "The balance decides" is what this rig
        // shows; "a rich granary absorbs it" is not, and is not claimed.
        //
        // Asserted on the DEMOGRAPHIC consequence too, not on the label alone.
        SimConfig cfg = WithHazard(TestConfigs.Sim(), 1e6);
        var thinExec = FullBalanceExecutor(cfg);
        var fatExec = FullBalanceExecutor(cfg);
        var richThinStoreExec = FullBalanceExecutor(cfg);
        WorldState thin = FoodStateTests.BalanceRig(cfg, rho: 1.3, storeYears: 0.1);
        WorldState fat = FoodStateTests.BalanceRig(cfg, rho: 2.0, storeYears: 1.5);
        // The ISOLATING arm: the fat rig's ρ behind the THIN rig's store.
        WorldState richThinStore = FoodStateTests.BalanceRig(cfg, rho: 2.0, storeYears: 0.1);

        // TIMING. The strike is drawn at turn 1 and APPLIED to the harvest of
        // step 2, so the state after turn 2 is the one that classifies, and
        // Demographics — which reads PREV — spends it at step 3. The rigs are
        // therefore read at turn 2 and their starvation collected at turn 3;
        // going further would compare two worlds whose POPULATIONS have
        // diverged, and the fat rig's own growth (it is fed) would open a small
        // deficit that has nothing to do with the strike.
        thin = thinExec.Step(thin); fat = fatExec.Step(fat);            // turn 1: the draw
        richThinStore = richThinStoreExec.Step(richThinStore);
        thin = thinExec.Step(thin); fat = fatExec.Step(fat);            // turn 2: the strike lands
        richThinStore = richThinStoreExec.Step(richThinStore);
        var rig0 = new SettlementId(0);
        FoodStateKind thinKind = FoodState.Of(thin, rig0, cfg, out FamineReason thinReason);
        FoodStateKind fatKind = FoodState.Of(fat, rig0, cfg, out FamineReason fatReason);
        FoodStateKind richKind = FoodState.Of(richThinStore, rig0, cfg, out FamineReason richReason);
        double thinD = FoodState.DeficitRatio(thin, rig0);
        double fatD = FoodState.DeficitRatio(fat, rig0);
        double richD = FoodState.DeficitRatio(richThinStore, rig0);
        thin = thinExec.Step(thin); fat = fatExec.Step(fat);            // turn 3: the kernel spends it
        richThinStore = richThinStoreExec.Step(richThinStore);
        long thinStarved = StarvedTotalOf(thin), fatStarved = StarvedTotalOf(fat);
        long richStarved = StarvedTotalOf(richThinStore);

        Assert.True(thin.Disasters.Count == 1 && thin.Disasters[0].AppliedMultiplier < 1.0,
            "no strike was applied to the thin rig — the forced-λ twin is vacuous");
        Assert.True(fat.Disasters.Count == 1 && fat.Disasters[0].AppliedMultiplier < 1.0,
            "no strike was applied to the fat rig — the two arms did not receive the same event");
        Assert.True(richThinStore.Disasters.Count == 1 && richThinStore.Disasters[0].AppliedMultiplier < 1.0,
            "no strike was applied to the isolating rig");
        // THE SAME EVENT, BIT FOR BIT, ON ALL THREE. Without this the comparison
        // could be reading three different strikes.
        Assert.Equal(thin.Disasters[0].AppliedMultiplier, fat.Disasters[0].AppliedMultiplier);
        Assert.Equal(thin.Disasters[0].AppliedMultiplier, richThinStore.Disasters[0].AppliedMultiplier);

        Assert.True(thinD > 0.0, "the thin store absorbed the strike — rig vacuous");
        Assert.Equal(FoodStateKind.Famine, thinKind);
        Assert.Equal(FamineReason.Disaster, thinReason);

        Assert.Equal(0.0, fatD);
        Assert.Equal(FoodStateKind.Normal, fatKind);
        Assert.Equal(FamineReason.None, fatReason);

        // The consequence, not just the label: the same event kills on one side
        // of the balance and not on the other.
        Assert.True(thinStarved > 0, "FAMINE at ρ = 1.3 killed nobody — the exceptional channel is dead");
        Assert.Equal(0, fatStarved);

        // THE ISOLATING ARM. ρ = 2.0 behind the THIN rig's 0.1-year store
        // absorbs the same strike: the discriminator is the surplus ratio, not
        // the granary. If this ever fails, the attribution in the comment above
        // is wrong and the prose must change with it.
        Assert.Equal(0.0, richD);
        Assert.Equal(FoodStateKind.Normal, richKind);
        Assert.Equal(FamineReason.None, richReason);
        Assert.Equal(0, richStarved);

        Console.WriteLine(
            $"S_Disaster_TriggersFamine: same strike (applied multiplier "
            + $"{Inv(thin.Disasters[0].AppliedMultiplier)} on all three arms) — "
            + $"ρ = 1.3 / 0.1 y store -> {thinKind}/{thinReason}, d = {Inv(thinD)}, starved {thinStarved}; "
            + $"ρ = 2.0 / 1.5 y store -> {fatKind}/{fatReason}, d = {Inv(fatD)}, starved {fatStarved}; "
            + $"ρ = 2.0 / 0.1 y store -> {richKind}/{richReason}, d = {Inv(richD)}, starved {richStarved} "
            + "— the SURPLUS RATIO is the discriminator, the store is not.");
    }

    // ======================================================================
    // §6.5 — abandonment triggers famine (the lifted FamineAtOneOfTwelve, re-aimed)
    // ======================================================================

    [Fact]
    public void S_Abandonment_TriggersFamine()
    {
        // The FamineAtOneOfTwelve rig at the SCENARIO level: the canonical N = 12
        // founded world, settlement 0 ordered to 0 % farm at turn 1 (the legacy
        // LaborAllocation order, which writes Farming 0 / Herding 0 /
        // Construction 1.0). Under CR-015 that row IS abandonment, so the
        // settlement classifies FAMINE/Abandonment the same turn its deficit
        // appears, and its starvation reads the WHOLE deficit. The ORDERING
        // claim (exit before death) belongs to
        // MigrationTests.FamineAtOneOfTwelve_ExitCrossesTheFractionBeforeDeathDoes,
        // which this test deliberately does not duplicate; what is asserted
        // here is the CAUSE, the TIMING and the fact that the twin without the
        // order never reaches FAMINE at that settlement.
        //
        // THIS ARM USES THE SHIPPED CONFIG DELIBERATELY, and is the reason
        // mandate item 1(B) is still reachable in play today: with the
        // famine-class disaster shipping INERT (hazardPerYear 0.0, CR-016) the
        // ONLY cause in the shipped world is deliberate abandonment, and this
        // test proves that cause on the SHIPPED config, not on a rig.
        SimConfig cfg = TestConfigs.Sim();
        var orders = new OrderLog();
        orders.Append(new OrderRecord(1, ActorId: 1, OrderKind.LaborAllocation, TargetId: 0, Amount: 0.0));
        TurnExecutor ordered = Executor(cfg, TestConfigs.Worldgen(), orders);
        TurnExecutor twin = Executor(cfg, TestConfigs.Worldgen());
        WorldState a = WorldFounding.Found(TestConfigs.Worldgen(), cfg, CanonicalSeed);
        WorldState b = WorldFounding.Found(TestConfigs.Worldgen(), cfg, CanonicalSeed);

        var s0 = new SettlementId(0);
        int firstFamine = -1, twinFamineAtS0 = 0;
        for (int t = 1; t <= 12; t++)
        {
            a = ordered.Step(a);
            b = twin.Step(b);
            FoodStateKind ka = FoodState.Of(a, s0, cfg, out FamineReason ra);
            if (ka == FoodStateKind.Famine)
            {
                if (firstFamine < 0) firstFamine = t;
                // Abandonment is the cause; a disaster may coincide (Both), and
                // that is the only other reason this rig can produce.
                Assert.True(ra is FamineReason.Abandonment or FamineReason.Both,
                    $"turn {t}: FAMINE at the abandoned settlement carried reason {ra}");
            }
            if (FoodState.Of(b, s0, cfg, out _) == FoodStateKind.Famine) twinFamineAtS0++;
        }

        Assert.True(firstFamine > 0,
            "the 0 %-farm order never produced FAMINE at settlement 0 in 12 turns");
        Assert.True(Sim.Core.State.FoodState.IsAbandoned(a, s0),
            "the abandonment row is not in force — the order did not take");
        Assert.True(twinFamineAtS0 == 0,
            $"the ORDERLESS twin reached FAMINE at settlement 0 on {twinFamineAtS0} turns — the "
            + "attribution to abandonment is not clean on this seed");
        Console.WriteLine($"S_Abandonment_TriggersFamine: first FAMINE/Abandonment at turn {firstFamine}; "
            + $"orderless twin: {twinFamineAtS0} FAMINE turns at settlement 0.");
    }

    private static long StarvedTotalOf(WorldState w)
    {
        for (int i = 0; i < w.LedgerFlows.Count; i++)
            if (w.LedgerFlows[i].Quantity == ConservedQuantityIds.Population
                && w.LedgerFlows[i].Reason == ReasonIds.Starvation)
                return w.LedgerFlows[i].TotalSunk;
        return 0;
    }

    // ======================================================================
    // §6.5 — the two SINGLE-SECTOR pipeline cases (CR-015 G6(a))
    // ======================================================================

    /// <summary>Percent-valued SectorAllocation orders for one settlement, in
    /// the 28-byte OrderRecord form (TargetId = settlement × 8 + sector).</summary>
    private static OrderLog SectorOrders(int turn, int settlement, params double[] percents)
    {
        var log = new OrderLog();
        for (int sector = 0; sector < percents.Length; sector++)
            log.Append(new OrderRecord(turn, ActorId: 1, OrderKind.SectorAllocation,
                TargetId: settlement * 8 + sector, Amount: percents[sector]));
        return log;
    }

    private sealed record SectorRun(
        FoodStateKind[] Kinds, FamineReason[] Reasons, double[] Deficits, long[] Starved, long[] Pops);

    /// <summary>N = 2 founded dev world, weather OFF, λ = 0 (the CAUSE under
    /// test is the sector row, and a random famine-class strike would supply a
    /// second one — the same rig control DemographyRetuneTests.FamineRigFed
    /// carries and for the same reason), settlement 0 ordered at turn 1.</summary>
    private static SectorRun RunSectorRig(double[] percents, int turns)
    {
        SimConfig cfg = WeatherOff(WithHazard(TestConfigs.Sim(), 0.0));
        WorldgenConfig wg = TestConfigs.DevWorldgen();
        TurnExecutor exec = Executor(cfg, wg, SectorOrders(1, 0, percents));
        WorldState world = WorldFounding.Found(wg, cfg, CanonicalSeed, settlementsOverride: 2);
        var kinds = new FoodStateKind[turns];
        var reasons = new FamineReason[turns];
        var deficits = new double[turns];
        var starved = new long[turns];
        var pops = new long[turns];
        var s0 = new SettlementId(0);
        for (int t = 1; t <= turns; t++)
        {
            world = exec.Step(world);
            kinds[t - 1] = FoodState.Of(world, s0, cfg, out FamineReason r);
            reasons[t - 1] = r;
            deficits[t - 1] = FoodState.DeficitRatio(world, s0);
            starved[t - 1] = StarvedTotalOf(world);
            long p = 0;
            for (int i = 0; i < world.Buckets.Count; i++)
                if (world.Buckets[i].Settlement == s0) p += world.Buckets[i].Count.Value;
            pops[t - 1] = p;
        }
        return new SectorRun(kinds, reasons, deficits, starved, pops);
    }

    [Fact]
    public void S_Pastoralist_Pipeline()
    {
        // CR-015 G6(a), RULED: "pastoralists starve by basket design (D-035 as
        // ratified); NOT a STOP — the famine semantics do not REQUIRE a
        // substitution change; they EXPOSE a pre-existing basket fact."
        //
        // THIS TEST PINS THE MEASURED OUTCOME AND CHANGES NOTHING. The basket is
        // not touched, the substitution is not touched, no threshold is moved.
        // A settlement that herds and does not farm has no staple; D-035's
        // substitution runs ONE way (unmet non-staple demand falls back on the
        // staple, never the reverse), so its grain line goes unmet whatever its
        // livestock surplus. The ladder's verdict on that is SEVERE, not
        // FAMINE: there is no famine-class CAUSE — nobody struck it and it did
        // not abandon food labour (it herds). It therefore starves on the
        // ADAPTED deficit d_eff = (d − a)/(1 − a), which is the whole content
        // of G6(a)'s "under a non-famine label".
        //
        // The queue's substitution CR (M5) is what would change this. Until it
        // is ruled, this test exists so the outcome cannot drift unobserved.
        SectorRun run = RunSectorRig([0.0, 60.0, 10.0, 10.0, 20.0], turns: 30);

        int severe = 0, famine = 0, stress = 0, normal = 0;
        double worstDeficit = 0.0;
        for (int t = 0; t < run.Kinds.Length; t++)
        {
            switch (run.Kinds[t])
            {
                case FoodStateKind.Famine: famine++; break;
                case FoodStateKind.Severe: severe++; break;
                case FoodStateKind.Stress: stress++; break;
                default: normal++; break;
            }
            worstDeficit = Math.Max(worstDeficit, run.Deficits[t]);
        }
        Console.WriteLine(
            $"S_Pastoralist_Pipeline (farming 0 / herding 0.6): FAMINE {famine}, SEVERE {severe}, "
            + $"STRESS {stress}, NORMAL {normal} over 30 turns; worst d {Inv(worstDeficit)}; "
            + $"starvation {run.Starved[^1]}; settlement 0 population {run.Pops[0]} -> {run.Pops[^1]}.");

        // THE RULING, PINNED: never FAMINE (no cause), and the shortage is real.
        Assert.Equal(0, famine);
        Assert.True(severe > 0,
            "the pastoralist rig never reached SEVERE — G6(a)'s premise (a pure pastoralist is short "
            + "by basket design) is no longer true on this tree, which is a change to D-035's "
            + "substitution or to the basket and needs the M5 CR, not a test edit");
        Assert.True(worstDeficit > TestConfigs.Sim().FoodState.AdaptationAbsorbableShortfall,
            $"worst deficit {Inv(worstDeficit)} is inside the absorbable band — the rig is not the "
            + "G6(a) case");
        Assert.True(run.Starved[^1] > 0,
            "the pastoralist rig starved nobody — G6(a) pins that it DOES, on the adapted deficit");
        // And it starves on the ADAPTED deficit, not the whole one: that is the
        // only difference the label makes, and it is the one G6(a) rules on.
        for (int t = 0; t < run.Kinds.Length; t++)
            if (run.Kinds[t] == FoodStateKind.Severe)
                Assert.Equal(
                    (run.Deficits[t] - TestConfigs.Sim().FoodState.AdaptationAbsorbableShortfall)
                        / (1.0 - TestConfigs.Sim().FoodState.AdaptationAbsorbableShortfall),
                    FoodState.EffectiveDeficit(run.Deficits[t], run.Kinds[t], TestConfigs.Sim()));
    }

    [Fact]
    public void S_FarmerOnly_Pipeline()
    {
        // The mirror case, and the one that shows the asymmetry is the BASKET's
        // and not the ladder's: a settlement that farms and does not herd has
        // its staple, and D-035's one-directional substitution carries the
        // unmet livestock/fish lines INTO grain. It is therefore never FAMINE
        // and — with the store behind it — need not even be short.
        SectorRun run = RunSectorRig([70.0, 0.0, 10.0, 10.0, 10.0], turns: 30);

        int famine = 0, severe = 0, stress = 0;
        double worstDeficit = 0.0;
        for (int t = 0; t < run.Kinds.Length; t++)
        {
            if (run.Kinds[t] == FoodStateKind.Famine) famine++;
            else if (run.Kinds[t] == FoodStateKind.Severe) severe++;
            else if (run.Kinds[t] == FoodStateKind.Stress) stress++;
            worstDeficit = Math.Max(worstDeficit, run.Deficits[t]);
        }
        Console.WriteLine(
            $"S_FarmerOnly_Pipeline (farming 0.7 / herding 0): FAMINE {famine}, SEVERE {severe}, "
            + $"STRESS {stress} over 30 turns; worst d {Inv(worstDeficit)}; "
            + $"starvation {run.Starved[^1]}; settlement 0 population {run.Pops[0]} -> {run.Pops[^1]}.");

        Assert.Equal(0, famine);
        Assert.Equal(0, severe);
        Assert.Equal(0.0, worstDeficit);
        Assert.Equal(0, run.Starved[^1]);
    }

    // ======================================================================
    // §6.5 — the era gate does not manufacture famine
    // ======================================================================

    [Fact]
    public void S_EraGate_FoodStateContinuous()
    {
        // The canonical era table steps dt 10 -> 5 at turn 250. The claim: no
        // FAMINE onset is attributable to the dt change ALONE. The λ = 0 twin
        // isolates exactly that — with no cause in the world the ladder cannot
        // reach FAMINE on either side of the gate, however the dt moves, so a
        // FAMINE settlement-turn here would be the dt change manufacturing a
        // cause out of nothing.
        //
        // THE dt STEP IS ASSERTED, NOT NARRATED (era-pacing.json is TUNE data):
        // if the gate moves, this test's window stops straddling it and must
        // fail by name rather than pass vacuously.
        //
        // WHAT THIS DOES NOT CLAIM. With λ > 0 the CLASSIFICATION of a strike
        // straddling the boundary IS dt-dependent — a 5-year failure costs the
        // same total food at either dt but concentrates it into a much deeper
        // per-turn deficit at dt 5 (multiplier 1 − s against 1 − s/2). That is
        // the inherited F4/G8 artefact, pinned as a DIFFERENCE by
        // DisasterSystemTests.D_Classification_DtDifference_Pinned and NEVER
        // asserted equal. See docs/t4.21-4-record.md §5 for what arming made of
        // it.
        //
        // AND WHAT ITS GREEN IS WORTH, MEASURED (T4.21-6). The window is
        // COMPLETELY QUIET: on this tree it reports "FAMINE 0 before / 0 after;
        // STRESS+SEVERE 0 / 0" — there is no food-state activity of any kind on
        // either side of the gate. With λ = 0 and no orders FAMINE is unreachable
        // BY CONSTRUCTION (FoodState.Of needs `struck || abandoned`, and neither
        // exists here), so the two FAMINE asserts below cannot fail for any
        // dt-related reason — only for a regression in the CAUSE GATE itself,
        // which is a real property but not the one the test's name suggests. The
        // §6.5 row it implements is satisfied TRIVIALLY. Do not read this green
        // as dt coverage: the dt-dependence of a straddling strike is real, is
        // pinned as a DIFFERENCE by D_Classification_DtDifference_Pinned, and is
        // escalated in CR-016 §2.1. Giving this test teeth means running the
        // ARMED arm beside the λ = 0 one and asserting that pinned difference,
        // which belongs with whatever CR-016 option the director takes.
        SimConfig cfg = WithHazard(TestConfigs.Sim(), 0.0);
        TurnExecutor exec = Executor(cfg, TestConfigs.Worldgen());
        WorldState world = WorldFounding.Found(TestConfigs.Worldgen(), cfg, CanonicalSeed);
        int famineBefore = 0, famineAfter = 0, stressBefore = 0, stressAfter = 0;
        double dtBefore = 0.0, dtAfter = 0.0;
        for (int t = 1; t <= 270; t++)
        {
            WorldState prev = world;
            world = exec.Step(world);
            if (t < 240) continue;
            bool after = t > 250;
            if (t == 250) dtBefore = world.Clock.DtYears;
            if (t == 251) dtAfter = world.Clock.DtYears;
            for (int i = 0; i < prev.Settlements.Count; i++)
            {
                FoodStateKind k = FoodState.Of(prev, prev.Settlements[i].Id, cfg, out _);
                if (k == FoodStateKind.Famine) { if (after) famineAfter++; else famineBefore++; }
                else if (k >= FoodStateKind.Stress) { if (after) stressAfter++; else stressBefore++; }
            }
        }
        Assert.Equal(10.0, dtBefore);
        Assert.Equal(5.0, dtAfter);   // the gate is inside the window — not vacuous
        Console.WriteLine(
            $"S_EraGate_FoodStateContinuous (λ = 0): dt {dtBefore} -> {dtAfter} at turn 250; FAMINE "
            + $"{famineBefore} before / {famineAfter} after; STRESS+SEVERE {stressBefore} / {stressAfter}.");
        Assert.Equal(0, famineBefore);
        Assert.Equal(0, famineAfter);
    }

    // ======================================================================
    // §6.5 — determinism WITH DISASTERS LIVE
    // ======================================================================

    /// <summary>The canonical founded world under the SHIPPED (armed) config,
    /// 120 turns — long enough that the measured world contains famine, short
    /// enough to keep three legs inside the suite budget. Each leg asserts a
    /// disaster actually fired, so none of them can twin two quiet worlds.</summary>
    private const int DeterminismTurns = 120;

    private static (WorldState World, int Onsets, int Famine) StepCounting(
        TurnExecutor exec, WorldState world, int from, int to, SimConfig cfg, List<string>? hashes = null)
    {
        int onsets = 0, famine = 0;
        for (int t = from; t <= to; t++)
        {
            WorldState next = exec.Step(world);
            for (int r = 0; r < next.Disasters.Count; r++)
            {
                if (next.Disasters[r].Kind != DisasterSystem.KindCropFailure) continue;
                bool wasActive = false;
                for (int q = 0; q < world.Disasters.Count; q++)
                    if (world.Disasters[q].Settlement == next.Disasters[r].Settlement)
                    { wasActive = world.Disasters[q].RemainingYears > 0.0; break; }
                if (!wasActive) onsets++;
            }
            for (int i = 0; i < world.Settlements.Count; i++)
                if (FoodState.Of(world, world.Settlements[i].Id, cfg, out _) == FoodStateKind.Famine)
                    famine++;
            world = next;
            hashes?.Add(WorldHash.ComputeHex(world));
        }
        return (world, onsets, famine);
    }

    [Fact]
    public void S_Determinism_TwinIdentical_WithDisastersLive()
    {
        // λ = 0.01 SET IN-RIG (T4.21-7). The shipped hazard is 0.0 (CR-016), and a
        // determinism leg run on an inert disaster proves nothing about the
        // disaster — the non-vacuity guard below says so out loud.
        SimConfig cfg = ArmedRig();
        var hashesA = new List<string>();
        var hashesB = new List<string>();
        (_, int onsets, int famine) = StepCounting(Executor(cfg, TestConfigs.Worldgen()),
            WorldFounding.Found(TestConfigs.Worldgen(), cfg, CanonicalSeed), 1, DeterminismTurns, cfg, hashesA);
        StepCounting(Executor(cfg, TestConfigs.Worldgen()),
            WorldFounding.Found(TestConfigs.Worldgen(), cfg, CanonicalSeed), 1, DeterminismTurns, cfg, hashesB);
        Assert.True(onsets > 0 && famine > 0,
            $"{onsets} onsets / {famine} FAMINE settlement-turns — the twin is comparing two worlds "
            + "with no disaster in them, which proves nothing about the disaster's determinism");
        Assert.Equal(hashesA, hashesB);
        Console.WriteLine($"S_Determinism twin: {onsets} onsets, {famine} FAMINE settlement-turns "
            + $"across {DeterminismTurns} turns, hash-identical every turn.");
    }

    [Fact]
    public void S_Determinism_ReplayReproducesRun_WithDisastersLive()
    {
        // λ = 0.01 SET IN-RIG (T4.21-7). The shipped hazard is 0.0 (CR-016), and a
        // determinism leg run on an inert disaster proves nothing about the
        // disaster — the non-vacuity guard below says so out loud.
        SimConfig cfg = ArmedRig();
        static OrderLog Log()
        {
            var log = new OrderLog();
            log.Append(new OrderRecord(20, ActorId: 1, OrderKind.LaborAllocation, 0, 30.0));
            log.Append(new OrderRecord(60, ActorId: 1, OrderKind.LaborAllocation, 0, 80.0));
            return log;
        }
        var hashes = new List<string>();
        (_, int onsets, int famine) = StepCounting(Executor(cfg, TestConfigs.Worldgen(), Log()),
            WorldFounding.Found(TestConfigs.Worldgen(), cfg, CanonicalSeed), 1, DeterminismTurns, cfg, hashes);
        Assert.True(onsets > 0 && famine > 0, $"{onsets} onsets / {famine} FAMINE turns — replay leg vacuous");

        using var buffer = new MemoryStream();
        Log().Save(buffer);
        buffer.Position = 0;
        OrderLog reloaded = OrderLog.Load(buffer);
        var replayed = new List<string>();
        StepCounting(Executor(cfg, TestConfigs.Worldgen(), reloaded),
            WorldFounding.Found(TestConfigs.Worldgen(), cfg, CanonicalSeed), 1, DeterminismTurns, cfg, replayed);
        Assert.Equal(hashes, replayed);
    }

    [Fact]
    public void S_Determinism_SaveLoadContinue_WithAnActiveDisasterRow()
    {
        // The DisasterRow carries RemainingYears BY DIMENSION, so it is the one
        // piece of famine-adjacent state that is SERIALIZED. This leg saves at a
        // turn on which the table is non-empty and continues hash-identically —
        // and asserts the table was non-empty at the save, so it cannot pass by
        // snapshotting a world with no disaster in it. λ = 0.01 SET IN-RIG
        // (T4.21-7): the shipped hazard is 0.0 under CR-016, so this leg arms
        // its own, and the saveAt guard below fails loudly if it ever stops.
        SimConfig cfg = ArmedRig();
        TurnExecutor exec = Executor(cfg, TestConfigs.Worldgen());
        WorldState world = WorldFounding.Found(TestConfigs.Worldgen(), cfg, CanonicalSeed);
        int saveAt = -1;
        for (int t = 1; t <= DeterminismTurns; t++)
        {
            world = exec.Step(world);
            if (world.Disasters.Count > 0) { saveAt = t; break; }
        }
        Assert.True(saveAt > 0, "no disaster row appeared in 120 canonical turns — save/load leg vacuous");

        using var buffer = new MemoryStream();
        Snapshot.Save(world, buffer);
        buffer.Position = 0;
        TerrainSet regenerated = Sim.Core.Worldgen.Worldgen.Generate(TestConfigs.Worldgen(), CanonicalSeed);
        WorldState loaded = Snapshot.Load(buffer, regenerated);
        Assert.Equal(WorldHash.ComputeHex(world), WorldHash.ComputeHex(loaded));
        Assert.Equal(world.Disasters.Count, loaded.Disasters.Count);

        TurnExecutor execLoaded = Executor(cfg, TestConfigs.Worldgen());
        for (int t = saveAt + 1; t <= DeterminismTurns; t++)
        {
            world = exec.Step(world);
            loaded = execLoaded.Step(loaded);
            Assert.Equal(WorldHash.ComputeHex(world), WorldHash.ComputeHex(loaded));
        }
        Console.WriteLine($"S_Determinism save/load-continue: saved at turn {saveAt} with "
            + $"{loaded.Disasters.Count} disaster row(s) live, continued to {DeterminismTurns} hash-identical.");
    }

    private static TurnExecutor FullBalanceExecutor(SimConfig cfg) =>
        new(EraTableLoader.Load(
                """{ "bands": [ { "name": "flat", "startYear": 0, "endYear": 100000, "dtYears": 10.0 } ] }"""),
            [SystemCatalog.Disaster(cfg), SystemCatalog.Production(cfg),
             SystemCatalog.Consumption(cfg), SystemCatalog.Demographics(cfg),
             SystemCatalog.PathBuild(cfg)]);
}
