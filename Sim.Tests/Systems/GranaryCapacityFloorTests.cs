using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

/// <summary>
/// THE GRANARY CEILING MUST NOT DESTROY A STORE BECAUSE IT FLOORED TO ZERO.
///
/// THE DEFECT. `BoundStore` sized the granary as
/// `WholeUnits(GranaryYearsOfDemand × annualGrainDemand)` and enforced it
/// whenever `annualGrainDemand > 0`. `WholeUnits` FLOORS — it is the D-004
/// converter for a FLOW, under a remainder convention that banks the fraction
/// somewhere. A capacity is a THRESHOLD with no remainder bank, so at
/// `1.5 × annualGrainDemand < 1` a genuinely positive capacity collapsed to 0,
/// `over` became the entire store, and every unit of grain the settlement held
/// was destroyed as `GranaryOverflow` in a single turn.
///
/// The tell was the asymmetry: a settlement at LITERALLY zero demand fails the
/// `annualGrainDemand > 0` guard and keeps its grain forever, while a settlement
/// down to its LAST PERSON was stripped bare. Nobody chose that rule.
///
/// THE INVARIANT PINNED HERE: the ceiling is enforced only when it is
/// representable as a positive whole number of units. Below that it is not
/// enforced — the same stance the code already took when there was no demand at
/// all to size a granary from.
///
/// THE ARITHMETIC, so these fixtures are readable rather than magic. Class 1's
/// Sustenance basket is grain 0.9 + livestock 0.06 + fish 0.04 = 1.0 per
/// person-year. These worlds carry a grain row only, so the livestock and fish
/// demand goes unmet and substitutes onto the staple: `annualGrainDemand` is
/// therefore exactly the cohort-weighted head count. Cohort 0 (a child) weighs
/// 0.6 and cohort 5 (an adult) weighs 1.0, so:
///   1 child  → capacity = floor(1.5 × 0.6) = floor(0.9) = 0   ← the defect
///   1 adult  → capacity = floor(1.5 × 1.0) = floor(1.5) = 1   ← enforced
/// </summary>
public sealed class GranaryCapacityFloorTests
{
    private static readonly GoodId Grain = new(1);

    private static EraTable FlatEra(double dtYears) => EraTableLoader.Load(
        $$"""{ "bands": [ { "name": "flat", "startYear": 0, "endYear": 100000, "dtYears": {{dtYears.ToString(System.Globalization.CultureInfo.InvariantCulture)}} } ] }""");

    /// <summary>One settlement, the given cohort counts, and `store` grain.
    /// Consumption alone — no production — so the store under test is the only
    /// thing moving and nothing can refill it mid-turn.</summary>
    private static WorldState World(long[] counts, long store)
    {
        WorldState world = PopulationExactnessTests.BucketWorld(counts);
        int row = world.GoodStocks.Add(new GoodStockRow(
            new SettlementId(0), Grain, Conserved.Zero, 0.0, 0.0));
        if (store > 0)
        {
            new Ledger(world.LedgerFlows).Flow(
                ref world.GoodStocks.Ref(row).Amount, ConservedQuantityIds.OfGood(Grain),
                ReasonIds.InitialEndowment, store, FlowDirection.Source, OverdrawPolicy.Throw);
        }
        return world;
    }

    private static long[] Cohort(int index, long count)
    {
        var counts = new long[Cohorts.Count];
        counts[index] = count;
        return counts;
    }

    private static WorldState StepConsumption(WorldState world, double dt)
    {
        SimConfig cfg = TestConfigs.Sim();
        var exec = new TurnExecutor(FlatEra(dt), [SystemCatalog.Consumption(cfg)]);
        return exec.Step(world);
    }

    private static long FlowSunk(WorldState w, ReasonId reason)
    {
        long total = 0;
        for (int i = 0; i < w.LedgerFlows.Count; i++)
        {
            LedgerFlowRow r = w.LedgerFlows[i];
            if (r.Quantity == ConservedQuantityIds.OfGood(Grain) && r.Reason == reason)
                total += r.TotalSunk;
        }
        return total;
    }

    private static long Stock(WorldState w)
    {
        for (int i = 0; i < w.GoodStocks.Count; i++)
            if (w.GoodStocks[i].Good == Grain) return w.GoodStocks[i].Amount.Value;
        return -1;
    }

    /// <summary>
    /// CASE 1 — POSITIVE CONTINUOUS CAPACITY BELOW ONE UNIT. This is the
    /// regression: one child gives a true capacity of 0.9 units, which floors to
    /// 0. Before the fix the whole surviving store was taken as overflow and the
    /// settlement ended the turn at exactly 0 grain. **This test fails against
    /// the old behaviour** — that is its entire purpose.
    /// </summary>
    [Fact]
    public void PositiveCapacityBelowOneUnit_DoesNotDestroyTheStore()
    {
        WorldState next = StepConsumption(World(Cohort(0, 1), store: 100), dt: 10.0);

        Assert.Equal(0, FlowSunk(next, ReasonIds.GranaryOverflow));
        Assert.True(Stock(next) > 0,
            $"a capacity of floor(1.5 × 0.6) = 0 destroyed the entire store; stock ended at {Stock(next)}");
    }

    /// <summary>
    /// CASE 2 — NO DEMAND AT ALL, so no basis to size a granary from. This is
    /// PRE-EXISTING behaviour and the fix must not disturb it: the outer guard
    /// (`annualGrainDemand > 0`) already skipped bounding here. Pinned so the two
    /// zero-ish cases are held together and can never drift apart again — it was
    /// their DISAGREEMENT that made the defect visible.
    /// </summary>
    [Fact]
    public void ZeroDemand_SkipsTheCeiling_Unchanged()
    {
        WorldState next = StepConsumption(World(new long[Cohorts.Count], store: 100), dt: 10.0);

        Assert.Equal(0, FlowSunk(next, ReasonIds.GranaryOverflow));
        Assert.True(Stock(next) > 0);
    }

    /// <summary>
    /// CASE 3 — A REPRESENTABLE CEILING IS STILL ENFORCED, EXACTLY. One adult
    /// gives capacity = floor(1.5 × 1.0) = 1, so a 100-unit store must be cut to
    /// exactly 1 and the difference must appear as `GranaryOverflow`. This is the
    /// test that stops the fix from becoming "never enforce the ceiling".
    /// </summary>
    [Fact]
    public void CapacityOfOneUnit_IsStillEnforced_AndTheStoreIsCutToIt()
    {
        WorldState next = StepConsumption(World(Cohort(5, 1), store: 100), dt: 10.0);

        Assert.True(FlowSunk(next, ReasonIds.GranaryOverflow) > 0,
            "a representable ceiling must still bind");
        Assert.Equal(1, Stock(next));
    }

    /// <summary>
    /// CASE 4 — ORDINARY SCALE IS UNTOUCHED. 350 people, the same shape the
    /// equilibrium tests use: capacity is in the hundreds, the ceiling binds, and
    /// the store lands on it exactly. If the fix perturbed anything at canonical
    /// scale this is where it would show, and it is why no golden moves.
    /// </summary>
    [Fact]
    public void CanonicalScale_CeilingBindsExactly_AndIsUnaffectedByTheFix()
    {
        var counts = new long[Cohorts.Count];
        counts[0] = 100; counts[5] = 200; counts[15] = 50;   // 350 people
        WorldState next = StepConsumption(World(counts, store: 1_000_000), dt: 10.0);

        // Weighted heads = 100×0.6 + 200×1.0 + 50×0.7 = 295 ⇒ annual grain
        // demand 295 (the non-staple 0.1 substitutes onto grain, as above) ⇒
        // capacity = floor(1.5 × 295) = 442. Hand-derived, not pasted from a run.
        Assert.Equal(442, Stock(next));
        Assert.True(FlowSunk(next, ReasonIds.GranaryOverflow) > 0);
    }

    /// <summary>
    /// CONSERVATION over every fixture above: whatever the ceiling does, the
    /// grain accounting must still close exactly. `long` arithmetic, no epsilon.
    /// </summary>
    [Theory]
    [InlineData(0, 1, 100)]      // capacity floors to 0
    [InlineData(5, 1, 100)]      // capacity 1
    [InlineData(5, 350, 1_000_000)]
    public void GrainAccountingCloses_WhateverTheCeilingDoes(int cohort, long count, long store)
    {
        WorldState before = World(Cohort(cohort, count), store);
        FoodAudit.FoodSnapshot start = FoodAudit.Snapshot(before, Grain.Value, "start");
        WorldState after = StepConsumption(before, dt: 10.0);
        FoodAudit.FoodSnapshot end = FoodAudit.Snapshot(after, Grain.Value, "end");

        FoodAudit.FoodTurnAccount account = FoodAudit.Account(start, end);
        Assert.True(account.Reconciles, account.Line());
    }
}
