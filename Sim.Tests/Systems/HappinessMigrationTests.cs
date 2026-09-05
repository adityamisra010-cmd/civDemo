using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

/// <summary>
/// T4.13 — HAPPINESS AS A WEAK MIGRATION INPUT, and the emergent loop.
///
/// Three properties, and the order matters because each one guards against a
/// different way of getting this wrong:
///
///  1. IT DOES SOMETHING. A zero-weight arm against a live-weight arm on the
///     same world — otherwise the term could be inert and every other test here
///     would still pass.
///  2. IT IS WEAK. Happiness must shade a choice between comparable
///     destinations and must never override a material one.
///  3. IT IS NOT A BONUS. Nothing anywhere adds happiness because migration
///     happened; the only way happiness moves is for a CONDITION to move. The
///     famine test is the sharp end of that: people leaving a starving
///     settlement must not make it not-starving by the act of leaving.
/// </summary>
public class HappinessMigrationTests
{
    private static EraTable FlatEra(double dtYears = 10.0) => EraTableLoader.Load(
        $$"""{ "bands": [ { "name": "flat", "startYear": 0, "endYear": 100000, "dtYears": {{dtYears}} } ] }""");

    private static TurnExecutor MigrationOnly(SimConfig cfg) =>
        new(FlatEra(), [SystemCatalog.Migration(cfg)]);

    private static long[] AdultsHeavy(long perCohort)
    {
        var counts = new long[Cohorts.Count];
        for (int c = 0; c < Cohorts.Count; c++) counts[c] = perCohort;
        return counts;
    }

    /// <summary>
    /// Three settlements: a crowded SOURCE (0) and two destinations (1, 2) that
    /// are materially identical — same land, same food, same distance — and
    /// differ ONLY in housing, and therefore only in happiness.
    /// </summary>
    private static WorldState TwoRivalDestinations(long dwellings1, long dwellings2)
    {
        var w = new WorldState(7);
        var ledger = new Ledger(w.LedgerFlows);

        for (int s = 0; s < 3; s++)
        {
            var id = new SettlementId(s);
            w.Settlements.Add(new SettlementRow(id, s, 0));

            long[] counts = AdultsHeavy(s == 0 ? 2_000_000 : 100);
            for (int c = 0; c < Cohorts.Count; c++)
            {
                int row = w.Buckets.Add(new BucketRow(
                    id, new CultureId(0), new ReligionId(0), new ClassId(0), c,
                    Conserved.Zero, 0.0, 0.0, 0.0, 0.0));
                ledger.Flow(ref w.Buckets.Ref(row).Count, ConservedQuantityIds.Population,
                    ReasonIds.InitialEndowment, counts[c], FlowDirection.Source, OverdrawPolicy.Throw);
            }

            int stock = w.GoodStocks.Add(new GoodStockRow(id, new GoodId(1), Conserved.Zero, 0.0, 0.0));
            ledger.Flow(ref w.GoodStocks.Ref(stock).Amount, ConservedQuantityIds.OfGood(new GoodId(1)),
                ReasonIds.InitialEndowment, 50_000, FlowDirection.Source, OverdrawPolicy.Throw);

            // Identical land for both destinations: happiness is the only
            // difference the system can see.
            w.CatchmentSummaries.Add(new CatchmentSummaryRow(id, 1, s == 0 ? 10.0 : 400.0, 0, 0));
            w.ConsumptionDeficits.Add(new ConsumptionDeficitRow(id, 0.0, 1000));

            long dw = s switch { 1 => dwellings1, 2 => dwellings2, _ => 0 };
            int h = w.Housing.Add(new HousingRow(id, Conserved.Zero, 0.0, 0.0, 0.0, 0.0));
            if (dw > 0)
            {
                ledger.Flow(ref w.Housing.Ref(h).Dwellings, ConservedQuantityIds.Dwellings,
                    ReasonIds.InitialEndowment, dw, FlowDirection.Source, OverdrawPolicy.Throw);
            }
        }

        // Equal distance to both destinations.
        w.SettlementDistances.Add(new SettlementDistanceRow(new SettlementId(0), new SettlementId(1), 20.0));
        w.SettlementDistances.Add(new SettlementDistanceRow(new SettlementId(0), new SettlementId(2), 20.0));
        return w;
    }

    private static long InflowTo(WorldState w, int settlement)
    {
        long n = 0;
        for (int i = 0; i < w.MigrationFlows.Count; i++)
            if (w.MigrationFlows[i].Settlement.Value == settlement) n += w.MigrationFlows[i].Inflow;
        return n;
    }

    // The two destinations are NOT interchangeable by construction: settlement
    // order affects integer flooring and remainder banking, and measurement
    // showed a standing 9-vs-16 split between them at ZERO happiness weight.
    // Comparing them directly would therefore attribute a positional artefact
    // to happiness. Both tests below SWAP which destination is housed and read
    // the difference the swap makes, which cancels position exactly.

    private static (long Housed, long Unhoused) BySwap(SimConfig cfg)
    {
        WorldState a = MigrationOnly(cfg).Step(TwoRivalDestinations(dwellings1: 100, dwellings2: 0));
        WorldState b = MigrationOnly(cfg).Step(TwoRivalDestinations(dwellings1: 0, dwellings2: 100));

        // In arm A the housed destination is 1; in arm B it is 2. Summing across
        // the two arms gives each ROLE the same positional exposure.
        long housed = InflowTo(a, 1) + InflowTo(b, 2);
        long unhoused = InflowTo(a, 2) + InflowTo(b, 1);
        return (housed, unhoused);
    }

    [Fact]
    public void BetweenMateriallyIdenticalDestinations_TheHappierOneReceivesMore()
    {
        (long housed, long unhoused) = BySwap(TestConfigs.Sim());

        Assert.True(housed > unhoused,
            $"happiness had no effect on destination choice: housed {housed} vs unhoused {unhoused}");
    }

    [Fact]
    public void WithTheWeightAtZero_HousingMakesNoDifferenceAtAll()
    {
        // THE ANTI-VACUITY ARM. With the term disabled the two ROLES must be
        // exactly indistinguishable — which also proves the swap harness itself
        // is not manufacturing the asymmetry the test above reports.
        SimConfig cfg = TestConfigs.Sim();
        SimConfig off = cfg with
        { Migration = cfg.Migration with { AttractivenessHappinessWeight = 0.0 } };

        (long housed, long unhoused) = BySwap(off);

        Assert.Equal(housed, unhoused);
    }

    [Fact]
    public void HappinessCannotOverrideAMaterialDifference()
    {
        // The WEAKNESS property. Destination 2 is miserable but has far more
        // land; destination 1 is content and land-poor. Material opportunity
        // must still win — a 15% modulation cannot reorder a 4x land gap.
        SimConfig cfg = TestConfigs.Sim();
        WorldState w = TwoRivalDestinations(dwellings1: 100, dwellings2: 0);
        for (int i = 0; i < w.CatchmentSummaries.Count; i++)
        {
            if (w.CatchmentSummaries[i].Settlement.Value == 1)
                w.CatchmentSummaries[i] = w.CatchmentSummaries[i] with { EffectiveArableKm2 = 200.0 };
            if (w.CatchmentSummaries[i].Settlement.Value == 2)
                w.CatchmentSummaries[i] = w.CatchmentSummaries[i] with { EffectiveArableKm2 = 800.0 };
        }

        w = MigrationOnly(cfg).Step(w);

        Assert.True(InflowTo(w, 2) > InflowTo(w, 1),
            "a weak happiness term overrode a 4x land difference — it is not weak");
    }

    [Fact]
    public void AFamineDestinationIsREFUSEDWhateverItsHappiness()
    {
        // The ruling's hard requirement: severe famine is not cured by moving
        // people into it. The absolute food gate and the deficit repulsion sit
        // OUTSIDE the happiness factor, so no happiness value can reopen them.
        SimConfig cfg = TestConfigs.Sim();
        WorldState w = TwoRivalDestinations(dwellings1: 100, dwellings2: 100);

        // Destination 1 is in total famine but perfectly housed — the case that
        // would be dangerous if happiness were additive rather than a modulator.
        for (int i = 0; i < w.ConsumptionDeficits.Count; i++)
            if (w.ConsumptionDeficits[i].Settlement.Value == 1)
                w.ConsumptionDeficits[i] = new ConsumptionDeficitRow(new SettlementId(1), 1.0, 1000);

        w = MigrationOnly(cfg).Step(w);

        Assert.Equal(0, InflowTo(w, 1));
        Assert.True(InflowTo(w, 2) > 0, "the healthy destination received nobody — rig is vacuous");
    }

    [Fact]
    public void MigratingDoesNotItselfRaiseHappiness_OnlyChangedConditionsDo()
    {
        // The prohibition, asserted directly. Take a world, run migration, and
        // compare the destination's happiness computed on the SAME conditions
        // before and after. People arriving must not be worth a single point on
        // their own; if anything, arrivals crowd the housing and LOWER it.
        SimConfig cfg = TestConfigs.Sim();
        WorldState before = TwoRivalDestinations(dwellings1: 100, dwellings2: 100);
        double happyBefore = SettlementHappiness.Of(before, new SettlementId(1), cfg);

        WorldState after = MigrationOnly(cfg).Step(before);
        double happyAfter = SettlementHappiness.Of(after, new SettlementId(1), cfg);

        Assert.True(InflowTo(after, 1) > 0, "nobody moved — the test would be vacuous");
        Assert.True(happyAfter <= happyBefore,
            $"happiness ROSE ({happyBefore} -> {happyAfter}) on arrivals alone, which is the "
            + "granted-bonus shape the ruling forbids; only improved conditions may raise it.");
    }
}
