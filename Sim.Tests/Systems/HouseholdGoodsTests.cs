using System;
using System.Collections.Generic;
using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Tests.TestUtil;
using Xunit;

namespace Sim.Tests.Systems;

/// <summary>
/// T4.13 — COMFORT AS A STOCK. The whole point of a stock is that its behaviour
/// CHANGES as the stock accumulates, so these tests run trajectories rather than
/// single steps: fill from empty, hold at the standard, deplete under neglect,
/// and dilute under population growth.
/// </summary>
public class HouseholdGoodsTests
{
    private static readonly SettlementId S0 = new(0);

    private static EraTable FlatEra(double dtYears) => EraTableLoader.Load(
        $$"""{ "bands": [ { "name": "flat", "startYear": 0, "endYear": 100000, "dtYears": {{dtYears.ToString(System.Globalization.CultureInfo.InvariantCulture)}} } ] }""");

    /// <summary>One settlement, `heads` peasants, and `materials` units of each
    /// household-goods material in stock. No production, no consumption — just the
    /// household-goods system, so every movement is attributable to it.</summary>
    private static WorldState Rig(SimConfig cfg, long heads, long materials, long startingUnits = 0)
    {
        var w = new WorldState(7);
        w.Settlements.Add(new SettlementRow(S0, 0, 0));
        var ledger = new Ledger(w.LedgerFlows);
        int b = w.Buckets.Add(new BucketRow(
            S0, new CultureId(1), new ReligionId(1), new ClassId(1), 5,
            Conserved.Zero, 0.0, 0.0, 0.0, 0.0));
        ledger.Flow(ref w.Buckets.Ref(b).Count, ConservedQuantityIds.Population,
            ReasonIds.InitialEndowment, heads, FlowDirection.Source, OverdrawPolicy.Throw);

        foreach (GoodEntry g in cfg.Goods!.Goods)
        {
            int row = w.GoodStocks.Add(new GoodStockRow(S0, new GoodId(g.Id), Conserved.Zero, 0.0, 0.0));
            if (materials > 0)
                ledger.Flow(ref w.GoodStocks.Ref(row).Amount,
                    ConservedQuantityIds.OfGood(new GoodId(g.Id)), ReasonIds.InitialEndowment,
                    materials, FlowDirection.Source, OverdrawPolicy.Throw);
        }
        if (startingUnits > 0)
        {
            int hg = w.HouseholdGoods.Add(new HouseholdGoodsRow(S0, Conserved.Zero, 0.0, 0.0));
            ledger.Flow(ref w.HouseholdGoods.Ref(hg).Units, ConservedQuantityIds.HouseholdGoods,
                ReasonIds.InitialEndowment, startingUnits, FlowDirection.Source, OverdrawPolicy.Throw);
        }
        return w;
    }

    private static long Units(WorldState w)
    {
        for (int i = 0; i < w.HouseholdGoods.Count; i++)
            if (w.HouseholdGoods[i].Settlement == S0) return w.HouseholdGoods[i].Units.Value;
        return 0;
    }

    private static double Requirement(SimConfig cfg, long heads) =>
        heads * cfg.Needs!.HouseholdGoods!.StandardPerPerson(1);

    [Fact]
    public void FillsFromEmptyTowardTheStandard_ThenSTOPS_NeverRatchetsAbove()
    {
        // THE ACCEPTANCE PROPERTY (m4-spec P4): the stock does not saturate at 1.0
        // forever. It fills to the requirement and then holds — it does not keep
        // climbing just because materials remain.
        SimConfig cfg = TestConfigs.Sim();
        WorldState w = Rig(cfg, heads: 1000, materials: 1_000_000);
        var exec = new TurnExecutor(FlatEra(10.0), [SystemCatalog.HouseholdGoods(cfg)]);

        double req = Requirement(cfg, 1000);
        var trajectory = new List<long>();
        for (int t = 0; t < 20; t++) { w = exec.Step(w); trajectory.Add(Units(w)); }

        Assert.True(trajectory[0] > 0, "nothing was crafted on the first turn");
        // It reaches the requirement...
        Assert.True(trajectory[^1] >= (long)(req * 0.99),
            $"never reached the standard: {trajectory[^1]} against requirement {req:F1}");
        // ...and never exceeds it by more than the one-unit rounding boundary,
        // even with a million units of material sitting there for twenty turns.
        foreach (long u in trajectory)
            Assert.True(u <= (long)Math.Ceiling(req) + 1,
                $"stock ratcheted above the requirement: {u} > {req:F1}");
    }

    [Fact]
    public void WithNoMaterials_TheStockDEPLETES_AndTheDecayIsBoundedByWhatIsInUse()
    {
        // Neglect: a settlement with goods but no materials to replace them.
        // The stock must fall monotonically and never below zero.
        SimConfig cfg = TestConfigs.Sim();
        WorldState w = Rig(cfg, heads: 1000, materials: 0, startingUnits: 5000);
        var exec = new TurnExecutor(FlatEra(10.0), [SystemCatalog.HouseholdGoods(cfg)]);

        long prev = Units(w);
        var seen = new List<long> { prev };
        for (int t = 0; t < 15; t++)
        {
            w = exec.Step(w);
            long now = Units(w);
            Assert.True(now <= prev, $"stock rose with no materials: {prev} -> {now}");
            Assert.True(now >= 0, "stock went negative");
            prev = now; seen.Add(now);
        }
        Assert.True(seen[^1] < seen[0], "fifteen turns of neglect cost nothing");
    }

    [Fact]
    public void IdleSurplusDoesNotEvaporate_WearIsOnUSE_NotOnTheWholeStock()
    {
        // THE LOAD-BEARING ASYMMETRY versus housing. A settlement holding far more
        // than it can use must lose only what its people actually use — a
        // stock × rate decay would bleed the surplus too. Measured against the
        // arithmetic: with requirement R and stock >> R, one turn's loss is
        // R × wornFraction, NOT stock × wornFraction.
        SimConfig cfg = TestConfigs.Sim();
        long heads = 100;
        double req = Requirement(cfg, heads);
        long surplus = (long)(req * 50);
        WorldState w = Rig(cfg, heads, materials: 0, startingUnits: surplus);
        var exec = new TurnExecutor(FlatEra(10.0), [SystemCatalog.HouseholdGoods(cfg)]);

        long before = Units(w);
        w = exec.Step(w);
        long lost = before - Units(w);

        // THE EXPECTED QUANTITY WAS WRONG IN THIS TEST'S FIRST VERSION, and
        // independent review caught it: it asserted req × wornFraction(dt), which
        // is the e-fold of a STOCK-proportional decay. Deep in the surplus regime
        // the in-use set is CONSTANT at `req`, so the goods wear at a constant
        // req/L per year and a 10-year turn gets through 10/5 = two full
        // turnovers of it. The old expectation capped the loss below ONE
        // turnover, i.e. it pinned the integration error as intended behaviour
        // (ADR-015 §7.2 — teeth are not aim). The property this test exists for
        // is unchanged and still asserted: wear is on USE, not on the whole stock.
        double life = cfg.Needs!.HouseholdGoods!.ServiceLifeYears;
        double onUse = req * 10.0 / life;                       // constant-rate regime
        double onWholeStock = before * cfg.Needs!.HouseholdGoods!.WornFraction(10.0);
        Assert.True(Math.Abs(lost - onUse) <= 1.0,
            $"loss {lost} is not the in-use quantity {onUse:F2}");
        Assert.True(lost < onWholeStock / 10.0,
            $"loss {lost} looks like whole-stock decay ({onWholeStock:F0}) — the inUse term is gone");
    }

    [Fact]
    public void PopulationGROWTH_DilutesComfort_WithoutLosingASinglePot()
    {
        // m4-spec P4, the sharp edge of it: the equilibrium tracks POPULATION, so a
        // growing settlement's satisfaction falls even though its stock does not.
        SimConfig cfg = TestConfigs.Sim();
        HouseholdGoodsConfig hg = cfg.Needs!.HouseholdGoods!;
        long stock = (long)Math.Ceiling(Requirement(cfg, 1000));

        double satAt1000 = Math.Min(1.0, stock / Requirement(cfg, 1000));
        double satAt4000 = Math.Min(1.0, stock / Requirement(cfg, 4000));

        Assert.Equal(1.0, satAt1000, 6);
        Assert.True(satAt4000 < 0.30,
            $"quadrupling the population barely moved satisfaction ({satAt4000:F3}) — "
            + "the stock is not population-denominated");
        Assert.True(hg.StandardPerPerson(2) > hg.StandardPerPerson(1),
            "artisans must need more household goods than peasants — the ratified basket says so");
    }

    [Fact]
    public void CraftingIsBoundedByMATERIALS_AndConservesThemExactly()
    {
        // Conservation (law 1) across TWO quantities: materials leave their good
        // stocks and units enter the household stock; nothing is minted and
        // nothing vanishes. Exact equality, no epsilon.
        SimConfig cfg = TestConfigs.Sim();
        WorldState w = Rig(cfg, heads: 1000, materials: 40);
        var exec = new TurnExecutor(FlatEra(10.0), [SystemCatalog.HouseholdGoods(cfg)]);

        w = exec.Step(w);

        Assert.True(ConservationAuditor.IsConserved(w, out string report), report);
        // Materials were the binding constraint, so the stock cannot have reached
        // the requirement.
        Assert.True(Units(w) < Requirement(cfg, 1000),
            "crafted past what the materials could pay for");
        Assert.True(Units(w) > 0, "crafted nothing despite having materials");
    }

    [Fact]
    public void Deterministic_AcrossIdenticalRuns_BitExact()
    {
        SimConfig cfg = TestConfigs.Sim();
        var exec = new TurnExecutor(FlatEra(10.0), [SystemCatalog.HouseholdGoods(cfg)]);
        WorldState a = Rig(cfg, 1000, 5000);
        WorldState b = Rig(cfg, 1000, 5000);
        for (int t = 0; t < 8; t++) { a = exec.Step(a); b = exec.Step(b); }
        Assert.Equal(WorldHash.ComputeHex(a), WorldHash.ComputeHex(b));
    }

    [Fact]
    public void TheStandardIsDERIVED_AnnualWearAtTheStandardEqualsTheRatifiedBasketDraw()
    {
        // The derivation the packet rests on, asserted rather than asserted-about:
        // at the standard holding, a year's wear per person equals exactly what the
        // FORMER Comfort basket drew per person per year — for ANY service life.
        // That is why the one new constant does not move steady-state consumption.
        HouseholdGoodsConfig hg = TestConfigs.Sim().Needs!.HouseholdGoods!;
        foreach (HouseholdGoodsClass pc in hg.PerClass)
        {
            double basketAnnual = 0.0;
            foreach (HouseholdGoodsMaterial m in pc.Materials) basketAnnual += m.PerPersonYear;
            double wearAtStandard = hg.StandardPerPerson(pc.Class) * hg.WornFraction(1.0);
            Assert.Equal(basketAnnual, wearAtStandard, 12);
        }
    }

    // --- REGRESSION PINS for the three defects independent review found -------

    [Fact]
    public void MaterialsDrawnEqualsUnitsMade_EXACTLY_NoMintFromRoundingSplit()
    {
        // F1, the critical one: `made` units were sourced under one flow while each
        // material was drawn under its OWN floor, and Σ floor(made × mixⱼ) is
        // strictly less than `made` whenever a share is fractional. The auditor is
        // structurally blind to it because the source flow is individually legal.
        // The invariant is asserted directly: materials out == units in, exactly.
        SimConfig cfg = TestConfigs.Sim();
        WorldState w = Rig(cfg, heads: 1000, materials: 1_000_000);
        var exec = new TurnExecutor(FlatEra(10.0), [SystemCatalog.HouseholdGoods(cfg)]);

        long materialsBefore = TotalMaterials(w, cfg);
        w = exec.Step(w);
        long drawn = materialsBefore - TotalMaterials(w, cfg);
        Assert.Equal(drawn, Units(w));
    }

    [Fact]
    public void OneUnitOfEachMaterial_CannotMintForever_PerpetualMotionPin()
    {
        // F1's worst case, measured before the fix: a settlement holding exactly
        // 1 pottery and 1 cloth crafted a unit EVERY turn while its material
        // stocks never moved. Both floors were 0 while affordability said 1.
        SimConfig cfg = TestConfigs.Sim();
        WorldState w = Rig(cfg, heads: 1000, materials: 1);
        var exec = new TurnExecutor(FlatEra(10.0), [SystemCatalog.HouseholdGoods(cfg)]);

        long materialsBefore = TotalMaterials(w, cfg);
        long cumulativeMade = 0, prevUnits = 0;
        for (int t = 0; t < 12; t++)
        {
            w = exec.Step(w);
            long u = Units(w);
            if (u > prevUnits) cumulativeMade += u - prevUnits;
            prevUnits = u;
        }
        long materialsSpent = materialsBefore - TotalMaterials(w, cfg);
        Assert.True(cumulativeMade <= materialsSpent,
            $"minted {cumulativeMade} units from {materialsSpent} materials — perpetual motion");
        Assert.True(ConservationAuditor.IsConserved(w, out string report), report);
    }

    [Fact]
    public void WearIsDtINVARIANT_OneBigTurnMatchesManySmallOnes_InTheSurplusRegime()
    {
        // F2: 1 − exp(−dt/L) is the closed form for decay proportional to the
        // STOCK, but above the requirement the rate is CONSTANT at requirement/L.
        // Measured before the fix: one dt=10 turn lost 2615 where ten dt=1 turns
        // lost 920. Era pacing steps dt 10→5→3→2→1→0.5, so this is not academic.
        SimConfig cfg = TestConfigs.Sim();
        long heads = 10_000;
        double req = Requirement(cfg, heads);
        long start = (long)(req * 1.8);

        WorldState big = Rig(cfg, heads, materials: 0, startingUnits: start);
        big = new TurnExecutor(FlatEra(10.0), [SystemCatalog.HouseholdGoods(cfg)]).Step(big);

        WorldState small = Rig(cfg, heads, materials: 0, startingUnits: start);
        var fine = new TurnExecutor(FlatEra(1.0), [SystemCatalog.HouseholdGoods(cfg)]);
        for (int t = 0; t < 10; t++) small = fine.Step(small);

        long a = Units(big), b = Units(small);
        Assert.True(Math.Abs(a - b) <= Math.Max(2, b / 100),
            $"wear is not dt-invariant: dt=10 leaves {a}, ten dt=1 turns leave {b}");
    }

    [Fact]
    public void AnArtisanOnlySettlement_ReachesItsStandard_NoSubUnitStall()
    {
        // F3: the sub-unit residue was banked for ONE class only, so a settlement
        // with no members of that class dropped its residue every turn and stalled
        // one unit short of its requirement forever — measured stalling at 1
        // against a requirement of 1.4343 with a million units of material.
        SimConfig cfg = TestConfigs.Sim();
        var w = new WorldState(7);
        w.Settlements.Add(new SettlementRow(S0, 0, 0));
        var ledger = new Ledger(w.LedgerFlows);
        int b = w.Buckets.Add(new BucketRow(
            S0, new CultureId(1), new ReligionId(1), new ClassId(2), 5,
            Conserved.Zero, 0.0, 0.0, 0.0, 0.0));
        ledger.Flow(ref w.Buckets.Ref(b).Count, ConservedQuantityIds.Population,
            ReasonIds.InitialEndowment, 2, FlowDirection.Source, OverdrawPolicy.Throw);
        foreach (GoodEntry g in cfg.Goods!.Goods)
        {
            int row = w.GoodStocks.Add(new GoodStockRow(S0, new GoodId(g.Id), Conserved.Zero, 0.0, 0.0));
            ledger.Flow(ref w.GoodStocks.Ref(row).Amount,
                ConservedQuantityIds.OfGood(new GoodId(g.Id)), ReasonIds.InitialEndowment,
                1_000_000, FlowDirection.Source, OverdrawPolicy.Throw);
        }

        double req = 2 * cfg.Needs!.HouseholdGoods!.StandardPerPerson(2);
        var exec = new TurnExecutor(FlatEra(1.0), [SystemCatalog.HouseholdGoods(cfg)]);
        for (int t = 0; t < 30; t++) w = exec.Step(w);

        Assert.True(Units(w) >= (long)Math.Floor(req),
            $"artisan-only settlement stalled at {Units(w)} against requirement {req:F4} "
            + "with unlimited materials — the sub-unit residue is being dropped");
    }

    private static long TotalMaterials(WorldState w, SimConfig cfg)
    {
        var wanted = new List<int>();
        foreach (HouseholdGoodsClass pc in cfg.Needs!.HouseholdGoods!.PerClass)
            foreach (HouseholdGoodsMaterial m in pc.Materials)
            {
                int id = cfg.Goods!.IdOf(m.Good);
                if (!wanted.Contains(id)) wanted.Add(id);
            }
        long total = 0;
        for (int i = 0; i < w.GoodStocks.Count; i++)
            if (wanted.Contains(w.GoodStocks[i].Good.Value)) total += w.GoodStocks[i].Amount.Value;
        return total;
    }
}
