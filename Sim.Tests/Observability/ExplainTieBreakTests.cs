using Sim.Core.Kernel;
using Sim.Core.Observability.Explain;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Observability;

/// <summary>
/// T4.19 lane A2 — the ordering rules, each proven on a TIE-DENSE hand world
/// where the wrong rule (table order, a double-only scan) gives a different
/// answer from the right one.
///
/// THE TIE IS BIT-EXACT BY CONSTRUCTION, not by luck: four bound needs of equal
/// weight → normalised weights 0.25 (exact); satisfactions 0.5 at σ = 0.5
/// (ρ = −1) → s^ρ = 2 (exact); every term 0.25·2 = 0.5 and 0.25·1 = 0.25 is a
/// dyadic rational, so the CES sums are exact in every association and the
/// four marginal lifts are IDENTICAL doubles. The one arithmetic assumption —
/// Math.Pow(0.5, −1) == 2 exactly — is asserted first, so a platform where it
/// fails reports that rather than a spurious tie-break result.
/// </summary>
public class ExplainTieBreakTests
{
    private static readonly SettlementId S0 = new(0);
    private static readonly ClassId C1 = new(1);

    /// <summary>The shipped config with a FOUR-need bound registry of equal
    /// weights (ids 1..4 bound: three Tier-A gates and Health) — built directly,
    /// bypassing the loader's basket checks, because the explanation reads
    /// satisfaction ROWS and needs no basket to rank them.</summary>
    private static SimConfig EqualWeightConfig()
    {
        SimConfig shipped = TestConfigs.Sim();
        NeedsConfig n = shipped.Needs!;
        NeedEntry[] needs =
        [
            new(1, "Sustenance", true, 1.0),
            new(2, "Shelter", true, 1.0, 0.0, "housingStock"),
            new(3, "Safety", true, 1.0),
            new(4, "Health", true, 1.0),
            new(5, "Belonging/Faith", false, 0.5),
            new(6, "Comfort", false, 0.3),
        ];
        return shipped with { Needs = new NeedsConfig(needs, n.Grievance, n.Aggregation, n.Baskets, n.VarietyStandard) };
    }

    /// <summary>One settlement, one 1000-person class-1 bucket, a grievance row,
    /// and satisfaction rows for the given need ids IN THE ORDER GIVEN.</summary>
    private static WorldState World(int[] needIdsInInsertionOrder, Func<int, double> satisfaction)
    {
        var w = new WorldState(7);
        var ledger = new Ledger(w.LedgerFlows);
        w.Settlements.Add(new SettlementRow(S0, SiteCell: 0, FoundedTurn: 0));
        int row = w.Buckets.Add(new BucketRow(S0, new CultureId(1), new ReligionId(1), C1,
            cohortIdx: 5, Conserved.Zero, 0.0, 0.0, 0.0, 0.0));
        ledger.Flow(ref w.Buckets.Ref(row).Count, ConservedQuantityIds.Population,
            ReasonIds.InitialEndowment, 1000, FlowDirection.Source, OverdrawPolicy.Throw);
        w.Grievances.Add(new GrievanceRow(S0, C1, 0.25));
        foreach (int id in needIdsInInsertionOrder)
            w.NeedSatisfactions.Add(new NeedSatisfactionRow(S0, C1, id, satisfaction(id)));
        return w;
    }

    [Fact]
    public void TieDense_EveryBoundNeedEquallyUnmet_LowestIdWins()
    {
        Assert.Equal(2.0, Math.Pow(0.5, -1.0));   // the exactness precondition, stated
        SimConfig cfg = EqualWeightConfig();
        Assert.Equal(0.5, cfg.Needs!.Aggregation.Sigma);   // ρ = −1: s^ρ = 1/s
        Assert.Equal(0.5, cfg.Needs.Aggregation.TierAFloor); // s = 0.5 is AT the floor: severity 0, no gate reweighting

        WorldState w = World([1, 2, 3, 4], _ => 0.5);
        GrievanceExplanation g = GrievanceExplanation.For(w, w, cfg, S0, C1);

        double[] lifts = new double[4];
        for (int i = 0; i < 4; i++)
        {
            NeedComponent c = ExplainGrievanceTests.Component(g, i + 1);
            Assert.True(c.Bound);
            lifts[i] = c.MarginalLift;
        }
        // Bit-identical lifts: the tie is real, dense, and exact.
        Assert.Equal(lifts[0], lifts[1]);
        Assert.Equal(lifts[0], lifts[2]);
        Assert.Equal(lifts[0], lifts[3]);
        Assert.True(lifts[0] > 0.0, "vacuous: a tie at zero lift");

        Assert.Equal(1, g.PrimaryNeedId);   // lowest id wins the four-way tie
        Assert.Equal(0.5, g.Aggregate);     // S = 1/(4 × 0.25 × 2) = 0.5, exactly
    }

    [Fact]
    public void DescendingTableOrder_SamePrimarySameAggregate_AsAscending()
    {
        SimConfig cfg = EqualWeightConfig();

        // (a) the tie world, rows inserted 4,3,2,1: table order would pick 4.
        WorldState asc = World([1, 2, 3, 4], _ => 0.5);
        WorldState desc = World([4, 3, 2, 1], _ => 0.5);
        GrievanceExplanation ga = GrievanceExplanation.For(asc, asc, cfg, S0, C1);
        GrievanceExplanation gd = GrievanceExplanation.For(desc, desc, cfg, S0, C1);
        Assert.Equal(1, gd.PrimaryNeedId);
        Assert.Equal(ga.PrimaryNeedId, gd.PrimaryNeedId);
        Assert.Equal(ga.Aggregate, gd.Aggregate);
        Assert.Equal(ga.Recomputed, gd.Recomputed);
        for (int i = 0; i < ga.Needs.Length; i++)
        {
            Assert.Equal(ga.Needs[i].NeedId, gd.Needs[i].NeedId);        // registry order regardless of table order
            Assert.Equal(ga.Needs[i].Satisfaction, gd.Needs[i].Satisfaction);
            Assert.Equal(ga.Needs[i].MarginalLift, gd.Needs[i].MarginalLift);
        }

        // (b) a distinct worst need in the MIDDLE of the registry (id 3 at 0.25,
        // the rest at 0.5): the primary is 3 whichever way the rows were inserted,
        // and a strictly-greater scan in DESCENDING table order would also give 3
        // only by luck — so the assertion that matters is that (a) and (b) agree
        // with the registry-keyed rule, not with either insertion order.
        static double Worst3(int id) => id == 3 ? 0.25 : 0.5;
        GrievanceExplanation a3 = GrievanceExplanation.For(World([1, 2, 3, 4], Worst3), World([1, 2, 3, 4], Worst3), cfg, S0, C1);
        GrievanceExplanation d3 = GrievanceExplanation.For(World([4, 3, 2, 1], Worst3), World([4, 3, 2, 1], Worst3), cfg, S0, C1);
        Assert.Equal(3, a3.PrimaryNeedId);
        Assert.Equal(3, d3.PrimaryNeedId);
        Assert.Equal(a3.Aggregate, d3.Aggregate);
        Assert.True(ExplainGrievanceTests.Component(a3, 3).MarginalLift
                    > ExplainGrievanceTests.Component(a3, 1).MarginalLift, "vacuous: need 3 is not the strict argmax");
    }

    [Fact]
    public void MigrationDestinations_SortedValueDescIdAsc_TieDense()
    {
        SimConfig cfg = TestConfigs.Sim();
        var w = new WorldState(3);
        // Five settlements inserted 4,0,3,1,2 with smoothed values that tie in
        // pairs: (4: 0.2) (0: 0.5) (3: 0.5) (1: 0.2) (2: 0.9). Sorted from the
        // point of view of settlement 2: [0, 3] at 0.5 (id ASC breaks the tie),
        // then [1, 4] at 0.2.
        int[] ids = [4, 0, 3, 1, 2];
        double[] values = [0.2, 0.5, 0.5, 0.2, 0.9];
        for (int i = 0; i < ids.Length; i++)
        {
            w.Settlements.Add(new SettlementRow(new SettlementId(ids[i]), SiteCell: i, FoundedTurn: 0));
            w.SmoothedAttractiveness.Add(new SmoothedAttractivenessRow(new SettlementId(ids[i]), values[i]));
        }
        MigrationExplanation m = MigrationExplanation.For(w, w, cfg, new SettlementId(2));

        Assert.Equal(0.9, m.Pull);
        Assert.Equal(4, m.Others.Length);
        Assert.Equal([0, 3, 1, 4], m.Others.Select(o => o.Id.Value).ToArray());
        Assert.Equal([0.5, 0.5, 0.2, 0.2], m.Others.Select(o => o.SmoothedAttractiveness).ToArray());
    }
}
