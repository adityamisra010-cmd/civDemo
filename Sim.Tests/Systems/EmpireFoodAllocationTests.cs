using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.Observability;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Consumption;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

/// <summary>
/// D-053 §2 — INTRA-POLITY GRAIN RELIEF IN A SHORTAGE (sim.json consumption.empireFoodAllocation; a partial step
/// toward D-051 §6's central stockpile, not that stockpile).
///
/// Two layers are tested separately, because they answer different questions:
///  - THE RULE, <see cref="FoodRelief.Allocate"/>: a pure function of the claims. Hand-computed water-filling
///    cases, equal and distinct urgency, the composite tie-break (fraction exact → PREV severity → id),
///    zero food, adequate food, never more than available or than needed, and independence of input order.
///  - THE SYSTEM, <see cref="ConsumptionSystem"/>: who is a donor, who is a claimant (the EMPTY-grain-row rule),
///    nearest-first draw (ADR-032's order) with tie-dense cases whose tie-break decides who keeps grain,
///    recipients executed in ascending id whatever the row order, exact ledger conservation, stateless,
///    cross-empire and contested-control isolation, the switch-off control (byte-identical to the pre-relief
///    step), replay determinism, and full-pipeline turns: a settlement whose own harvest fails is fed by its
///    siblings and nobody starves — and, on the SHIPPED config, the regimes relief does NOT remove: abandonment
///    FAMINE once the polity has no spare grain, and a starving settlement in a polity short overall that gets
///    exactly its water-filled share and still starves.
///
/// The system rigs use one adult cohort of class 1 (needs.json: grain 0.9 + livestock 0.06 + fish 0.04 = 1.0
/// person-year-equivalent per person-year), dt = 1 year and only a grain stock: every unit of the requirement
/// substitutes onto grain, so a settlement of n adults requires exactly R = n units (asserted, not assumed).
/// Spoilage and the granary bound are switched off in the rig config unless a test is ABOUT their order, so
/// every grant is an exact integer read straight off the stores.
/// </summary>
public class EmpireFoodAllocationTests
{
    // ================================================================== THE RULE (pure)

    private static FoodClaim C(int id, long required, long unmet, int severity = 0) => new(id, required, unmet, severity);

    private static long[] Grants(long supply, params FoodClaim[] claims) => FoodRelief.Allocate(claims, supply);

    [Fact]
    public void Rule_AdequateSupply_EveryClaimMetInFull_NeverMore()
    {
        FoodClaim[] claims = [C(1, 100, 80), C(2, 200, 100), C(3, 50, 10)];
        Assert.Equal([80L, 100L, 10L], Grants(190, claims));          // exactly enough
        Assert.Equal([80L, 100L, 10L], Grants(1_000_000, claims));    // far more than enough: need caps it
    }

    [Fact]
    public void Rule_ZeroFood_NothingAllocated()
    {
        Assert.Equal([0L, 0L, 0L], Grants(0, C(1, 100, 80), C(2, 200, 100), C(3, 50, 10)));
        Assert.Empty(Grants(500));
    }

    /// <summary>
    /// HAND-COMPUTED water-filling. Fractions f = 0.8, 0.5, 0.2; supply 70.
    /// A alone: lowering A from 0.8 to 0.5 costs 30, leaving 40 — not enough to take A and B together from 0.5
    /// to C's 0.2 (that costs 0.3 × 300 = 90). So the level is L = (U − A)/R = (80 + 100 − 70)/(100 + 200) = 110/300:
    /// A gets 80 − 100·110/300 = 43.33 → 43, B gets 100 − 200·110/300 = 26.67 → 26, C nothing (0.2 &lt; L).
    /// 69 given, ONE leftover unit. A and B now both lack exactly 0.37 of their requirement (37/100 = 74/200 —
    /// an EXACT tie, decided by cross-multiplication, 37·200 = 74·100), so PREV severity decides, then id.
    /// </summary>
    [Fact]
    public void Rule_EmpireWideShortage_WaterFilling_HandComputed_ExactTieGoesToSeverityThenId()
    {
        // Equal severity: the leftover goes to the lower id (A).
        Assert.Equal([44L, 26L, 0L], Grants(70, C(1, 100, 80), C(2, 200, 100), C(3, 50, 10)));
        // B worse-classified last turn (Severe = 2): the leftover goes to B.
        Assert.Equal([43L, 27L, 0L], Grants(70, C(1, 100, 80), C(2, 200, 100, severity: 2), C(3, 50, 10)));
        // Ids swapped, severities equal: the leftover follows the id, not the position.
        Assert.Equal([43L, 27L, 0L], Grants(70, C(9, 100, 80), C(4, 200, 100), C(3, 50, 10)));
    }

    [Fact]
    public void Rule_EqualUrgency_EqualFill_LeftoverUnitsByAscendingId()
    {
        // Three identical claims (R 10, u 10), 14 units: the level is 16/30, each gets floor(4.67) = 4 and the
        // two leftover units go to the two lowest ids, whatever order the claims arrive in.
        Assert.Equal([5L, 5L, 4L], Grants(14, C(1, 10, 10), C(2, 10, 10), C(3, 10, 10)));
        Assert.Equal([4L, 5L, 5L], Grants(14, C(3, 10, 10), C(1, 10, 10), C(2, 10, 10)));
        // Same unmet FRACTION at different sizes is equal urgency too: each ends at the same fraction.
        long[] g = Grants(35, C(1, 10, 5), C(2, 20, 10), C(3, 40, 20));   // f = 0.5 each; supply = Σ u = 35
        Assert.Equal([5L, 10L, 20L], g);                                  // → everyone fully fed
        g = Grants(14, C(1, 10, 5), C(2, 20, 10), C(3, 40, 20));          // level (35 − 14)/70 = 0.3 exactly
        Assert.Equal([2L, 4L, 8L], g);                                    // every claim left at exactly 0.3
    }

    [Fact]
    public void Rule_DistinctUrgency_WorstServedFirst_ThenTogether()
    {
        // f = 0.9 vs 0.5. 30 units only bring A down to 0.6 — still worse than B — so B gets nothing.
        Assert.Equal([30L, 0L], Grants(30, C(1, 100, 90), C(2, 100, 50)));
        // 40 bring A exactly to B's 0.5; B still nothing.
        Assert.Equal([40L, 0L], Grants(40, C(1, 100, 90), C(2, 100, 50)));
        // 60: A to 0.5 costs 40, the other 20 go 10/10 → both at 0.4.
        Assert.Equal([50L, 10L], Grants(60, C(1, 100, 90), C(2, 100, 50)));
        // A small, desperate village beats a large, mildly short city: f 0.8 (R 10) vs f 0.1 (R 1000).
        // Proportional-to-demand would give the city 100 of the 108 units; worst-first gives the village all 7
        // it can use to reach 0.1, then shares.
        long[] g = Grants(8, C(1, 10, 8), C(2, 1000, 100));
        Assert.Equal(8L, g[0] + g[1]);
        Assert.True(g[0] >= 7, $"the village got {g[0]} — the worst-off was not served first");
    }

    /// <summary>TIE-DENSE: many claims at one exact fraction (R differing), severities and ids interleaved; the
    /// leftover units walk (severity DESC, id ASC) exactly, and the fraction comparison is exact — two fractions
    /// a double cannot tell apart are still ordered correctly.</summary>
    [Fact]
    public void Rule_TieDense_CompositeKey_SeverityThenId_AndExactFractionComparison()
    {
        // Six claims at f = 1/2 exactly: R 2,4,6,8,10,12 → u 1..6. Supply 6: level (21 − 6)/42 = 15/42.
        // floor shares: u − R·15/42 = R/2 − R·15/42 = R·6/42 = R/7 → 0,0,0,1,1,1 (sum 3); 3 leftover units.
        // Remaining fractions then: 1/2,1/2,1/2 for the first three, (u−1)/R for the rest = 3/8, 4/10, 5/12.
        // So the three leftovers go to the three claims still at 1/2 — ALL of them, in (severity, id) order.
        FoodClaim[] claims =
        [
            C(30, 2, 1, severity: 0), C(10, 4, 2, severity: 3), C(20, 6, 3, severity: 0),
            C(40, 8, 4, severity: 1), C(50, 10, 5, severity: 0), C(60, 12, 6, severity: 2),
        ];
        Assert.Equal([1L, 1L, 1L, 1L, 1L, 1L], Grants(6, claims));
        // Supply 4: level (21 − 4)/42 = 17/42, floor shares R·4/42 → 0,0,0,0,0,1 (only R = 12 reaches 1.14).
        // 3 leftovers among the five claims still at exactly 1/2: severity 3 (id 10) first, then severity 1
        // (id 40), then severity 0 by id — 20 before 30 before 50 — so id 20 takes the third.
        long[] g = Grants(4, claims);
        Assert.Equal(4L, g[0] + g[1] + g[2] + g[3] + g[4] + g[5]);
        Assert.Equal([0L, 1L, 1L, 1L, 0L, 1L], g);

        // EXACT comparison: (10^18 + 1) / (3·10^18) is strictly larger than 1/3, but both are the same double.
        var big = C(7, 3_000_000_000_000_000_000L, 1_000_000_000_000_000_001L);
        var small = C(1, 3, 1);
        Assert.Equal((double)big.Unmet / big.Required, (double)small.Unmet / small.Required);
        Assert.True(FoodRelief.Compare(big, big.Unmet, small, small.Unmet) < 0,
            "the larger exact fraction must rank first even with the higher id");
    }

    /// <summary>Property sweep (deterministic SplitMix64 generator, the kernel's own): Σ grants = min(supply, Σ unmet),
    /// 0 ≤ grant ≤ unmet, max-min within one unit at each end, and independence of the claims' order.</summary>
    [Fact]
    public void Rule_NeverExceedsSupplyOrNeed_MaxMinWithinOneUnit_PermutationIndependent()
    {
        ulong rng = 0xF00D_5EEDUL;
        int checkedCases = 0;
        for (int trial = 0; trial < 2_000; trial++)
        {
            int n = 1 + (int)(SplitMix64.Next(ref rng) % 9);
            var claims = new FoodClaim[n];
            long totalUnmet = 0;
            for (int i = 0; i < n; i++)
            {
                long r = 1 + (long)(SplitMix64.Next(ref rng) % (trial % 3 == 0 ? 12UL : 5_000UL));
                long u = 1 + (long)(SplitMix64.Next(ref rng) % (ulong)r);
                int sev = (int)(SplitMix64.Next(ref rng) % 4);
                claims[i] = new FoodClaim(100 + i * 7 % 13 + i * 13, r, u, sev);
                totalUnmet += u;
            }
            long supply = (long)(SplitMix64.Next(ref rng) % (ulong)(totalUnmet * 2 + 1));
            long[] g = FoodRelief.Allocate(claims, supply);

            long given = 0;
            for (int i = 0; i < n; i++)
            {
                Assert.InRange(g[i], 0L, claims[i].Unmet);
                given += g[i];
            }
            Assert.Equal(Math.Min(supply, totalUnmet), given);

            // Max-min within one unit at each end: if i received anything, no j can be left worse than i by more
            // than one unit of each: f'_j − f'_i < 1/R_i + 1/R_j  ⇔  (u_j−a_j)R_i − (u_i−a_i)R_j < R_i + R_j.
            for (int i = 0; i < n; i++)
            {
                if (g[i] == 0) continue;
                for (int j = 0; j < n; j++)
                {
                    Int128 lhs = (Int128)(claims[j].Unmet - g[j]) * claims[i].Required
                                 - (Int128)(claims[i].Unmet - g[i]) * claims[j].Required;
                    Assert.True(lhs < claims[i].Required + claims[j].Required,
                        $"trial {trial}: claim {claims[i].Settlement} was served while {claims[j].Settlement} was left more than a unit worse");
                }
            }

            // Permutation independence: reverse and rotate; each settlement's grant is unchanged.
            var reversed = new FoodClaim[n];
            for (int i = 0; i < n; i++) reversed[i] = claims[n - 1 - i];
            long[] gr = FoodRelief.Allocate(reversed, supply);
            var rotated = new FoodClaim[n];
            for (int i = 0; i < n; i++) rotated[i] = claims[(i + trial) % n];
            long[] go = FoodRelief.Allocate(rotated, supply);
            for (int i = 0; i < n; i++)
            {
                Assert.Equal(g[n - 1 - i], gr[i]);
                Assert.Equal(g[(i + trial) % n], go[i]);
            }
            checkedCases++;
        }
        Assert.Equal(2_000, checkedCases);
    }

    [Fact]
    public void Rule_RefusesMalformedClaims()
    {
        Assert.Throws<ArgumentException>(() => Grants(10, C(1, 0, 0)));
        Assert.Throws<ArgumentException>(() => Grants(10, C(1, 10, 11)));
        Assert.Throws<ArgumentException>(() => Grants(10, C(1, 10, 0)));
        Assert.Throws<ArgumentException>(() => Grants(10, C(1, 10, 5), C(1, 20, 5)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Grants(-1, C(1, 10, 5)));
    }

    // ================================================================== THE SYSTEM

    private static readonly SimConfig Shipped = TestConfigs.Sim();

    /// <summary>The shipped config with spoilage and the granary bound off, so grants read exactly.</summary>
    private static SimConfig Exact(bool on = true)
    {
        SimConfig cfg = Shipped with
        {
            Consumption = Shipped.Consumption with { GrainSpoilagePerYear = 0.0, GranaryYearsOfDemand = 0.0 },
        };
        return on ? cfg : TestConfigs.PreEmpireFoodAllocation(cfg);
    }

    private static EraTable FlatEra() => EraTableLoader.Load(
        """{ "bands": [ { "name": "flat", "startYear": 0, "endYear": 100000, "dtYears": 1 } ] }""");

    private static WorldState Step(SimConfig cfg, WorldState w) =>
        new TurnExecutor(FlatEra(), [SystemCatalog.Consumption(cfg)]).Step(w);

    private readonly record struct Site(int Id, long Adults, long Grain, int Polity);

    /// <summary>Settlements of <c>Adults</c> class-1 adults holding <c>Grain</c> and nothing else (a row for every
    /// good, at 0), controlled by <c>Polity</c> (0 = no controller: stateless).</summary>
    private static WorldState Rig(params Site[] sites)
    {
        var w = new WorldState(7);
        var ledger = new Ledger(w.LedgerFlows);
        foreach (Site s in sites)
        {
            var id = new SettlementId(s.Id);
            w.Settlements.Add(new SettlementRow(id, SiteCell: 0, FoundedTurn: 0));
            for (int c = 0; c < Cohorts.Count; c++)
            {
                int row = w.Buckets.Add(new BucketRow(id, new CultureId(1), new ReligionId(1), new ClassId(1),
                    c, Conserved.Zero, 0.0, 0.0, 0.0, 0.0));
                if (c == 5 && s.Adults > 0)
                    ledger.Flow(ref w.Buckets.Ref(row).Count, ConservedQuantityIds.Population,
                        ReasonIds.InitialEndowment, s.Adults, FlowDirection.Source, OverdrawPolicy.Throw);
            }
            foreach (GoodEntry g in Shipped.Goods!.Goods)
            {
                int row = w.GoodStocks.Add(new GoodStockRow(id, new GoodId(g.Id), Conserved.Zero, 0.0, 0.0));
                if (g.Id == Shipped.Goods.GrainId && s.Grain > 0)
                    ledger.Flow(ref w.GoodStocks.Ref(row).Amount, ConservedQuantityIds.OfGood(new GoodId(g.Id)),
                        ReasonIds.InitialEndowment, s.Grain, FlowDirection.Source, OverdrawPolicy.Throw);
            }
            if (s.Polity > 0) w.Controls.Add(new ControlRow(new PolityId(s.Polity), id, 1.0));
        }
        return w;
    }

    private static int Grain => Shipped.Goods!.GrainId;

    private static GoodStockRow GrainRow(IReadOnlyWorldState w, int settlement)
    {
        int i = GoodStockIndex.IndexOf(w.GoodStocks, new SettlementId(settlement), new GoodId(Grain));
        Assert.True(i >= 0, $"settlement {settlement} has no grain row");
        return w.GoodStocks[i];
    }

    private static ConsumptionDeficitRow Deficit(IReadOnlyWorldState w, int settlement)
    {
        for (int i = 0; i < w.ConsumptionDeficits.Count; i++)
            if (w.ConsumptionDeficits[i].Settlement.Value == settlement) return w.ConsumptionDeficits[i];
        throw new InvalidOperationException($"no deficit row for {settlement}");
    }

    private static long TotalGrain(IReadOnlyWorldState w) => FoodAudit.GrainStock(w, Grain);

    private static long Sunk(IReadOnlyWorldState w, ReasonId reason, ConservedQuantityId q)
    {
        for (int i = 0; i < w.LedgerFlows.Count; i++)
            if (w.LedgerFlows[i].Quantity == q && w.LedgerFlows[i].Reason == reason) return w.LedgerFlows[i].TotalSunk;
        return 0;
    }

    private static void AssertConservedExactly(WorldState before, WorldState after)
    {
        Assert.True(ConservationAuditor.IsConserved(after, out string report), report);
        ConservedQuantityId q = ConservedQuantityIds.OfGood(new GoodId(Grain));
        long sunk = (Sunk(after, ReasonIds.Eaten, q) - Sunk(before, ReasonIds.Eaten, q))
                    + (Sunk(after, ReasonIds.Spoilage, q) - Sunk(before, ReasonIds.Spoilage, q))
                    + (Sunk(after, ReasonIds.GranaryOverflow, q) - Sunk(before, ReasonIds.GranaryOverflow, q));
        Assert.Equal(TotalGrain(before) - sunk, TotalGrain(after));
    }

    [Fact]
    public void System_RigRequirementIsExactlyOneUnitPerAdultYear()
    {
        WorldState next = Step(Exact(), Rig(new Site(0, 100, 1_000, 1), new Site(1, 37, 1_000, 1)));
        Assert.Equal(100L, Deficit(next, 0).DemandUnits);
        Assert.Equal(37L, Deficit(next, 1).DemandUnits);
        Assert.Equal(100L, GrainRow(next, 0).LastConsumptionEatenUnits);
    }

    [Fact]
    public void System_AdequateSupply_NoTransfer_ByteIdenticalToSwitchOff()
    {
        WorldState w = Rig(new Site(0, 100, 150, 1), new Site(1, 200, 900, 1), new Site(2, 50, 51, 1));
        foreach (SimConfig on in new[] { Exact(), Shipped })
        {
            WorldState a = Step(on, w), b = Step(TestConfigs.PreEmpireFoodAllocation(on), w);
            Assert.Equal(WorldHash.ComputeHex(b), WorldHash.ComputeHex(a));
            Assert.Equal(0.0, Deficit(a, 0).DeficitRatio);
        }
    }

    [Fact]
    public void System_LocalShortage_FullyCoveredBySibling_ExactUnits_DeficitZero_Conserved()
    {
        WorldState w = Rig(new Site(0, 100, 30, 1), new Site(1, 200, 1_000, 1));
        WorldState off = Step(Exact(on: false), w);
        WorldState on = Step(Exact(), w);

        // Switch off: settlement 0 eats its 30 and is 70% short while 800 sit next door.
        Assert.Equal(0.7, Deficit(off, 0).DeficitRatio);
        Assert.Equal(800L, GrainRow(off, 1).Amount.Value);

        // Switch on: exactly the 70 it lacks move from 1 and are eaten at 0.
        Assert.Equal(0.0, Deficit(on, 0).DeficitRatio);
        Assert.Equal(100L, GrainRow(on, 0).LastConsumptionEatenUnits);   // 30 own + 70 received
        Assert.Equal(0L, GrainRow(on, 0).Amount.Value);
        Assert.Equal(730L, GrainRow(on, 1).Amount.Value);                // 1000 − 200 own − 70 given
        Assert.Equal(200L, GrainRow(on, 1).LastConsumptionEatenUnits);   // the donor's own eating is unchanged
        Assert.Equal(GrainRow(off, 0).LastConsumptionDemandUnits, GrainRow(on, 0).LastConsumptionDemandUnits);
        Assert.Equal(Deficit(off, 0).DemandUnits, Deficit(on, 0).DemandUnits);
        AssertConservedExactly(w, on);
        Assert.Equal(TotalGrain(off), TotalGrain(on) + 70);             // the 70 were EATEN, not lost
    }

    [Fact]
    public void System_ZeroSpareFood_NoTransfer_DeficitsUnchanged_ByteIdentical()
    {
        // Everyone short: nobody has grain left after their own eating, so there is nothing to share.
        WorldState w = Rig(new Site(0, 100, 30, 1), new Site(1, 200, 0, 1), new Site(2, 50, 50, 1));
        WorldState on = Step(Exact(), w), off = Step(Exact(on: false), w);
        Assert.Equal(WorldHash.ComputeHex(off), WorldHash.ComputeHex(on));
        Assert.Equal(0.7, Deficit(on, 0).DeficitRatio);
        Assert.Equal(1.0, Deficit(on, 1).DeficitRatio);
        Assert.Equal(0.0, Deficit(on, 2).DeficitRatio);
    }

    /// <summary>Empire-wide shortage: three hungry settlements (u = 80/100, 100/200, 10/50 — the hand-computed rule
    /// case) and one donor holding exactly 70 spare. Grants 44 / 26 / 0, drawn and eaten exactly.</summary>
    [Fact]
    public void System_EmpireWideShortage_WaterFilling_ExactGrants_AndDeficits()
    {
        WorldState w = Rig(
            new Site(0, 100, 20, 1), new Site(1, 200, 100, 1), new Site(2, 50, 40, 1), new Site(3, 10, 80, 1));
        WorldState on = Step(Exact(), w);
        Assert.Equal(20L + 44, GrainRow(on, 0).LastConsumptionEatenUnits);
        Assert.Equal(100L + 26, GrainRow(on, 1).LastConsumptionEatenUnits);
        Assert.Equal(40L, GrainRow(on, 2).LastConsumptionEatenUnits);
        Assert.Equal(0L, GrainRow(on, 3).Amount.Value);                 // the donor gave all 70 it had spare
        Assert.Equal((100.0 - 64.0) / 100.0, Deficit(on, 0).DeficitRatio);
        Assert.Equal((200.0 - 126.0) / 200.0, Deficit(on, 1).DeficitRatio);
        Assert.Equal((50.0 - 40.0) / 50.0, Deficit(on, 2).DeficitRatio);
        Assert.Equal(0.0, Deficit(on, 3).DeficitRatio);
        AssertConservedExactly(w, on);

        // PREV severity breaks the exact tie: settlement 1 was SEVERE last turn → the leftover unit is its.
        WorldState severe = w.Clone();
        severe.ConsumptionDeficits.Add(new ConsumptionDeficitRow(new SettlementId(1), 0.9, 200));
        Assert.Equal(FoodStateKind.Severe, FoodState.Of(severe, new SettlementId(1), Shipped, out _));
        WorldState s2 = Step(Exact(), severe);
        Assert.Equal(20L + 43, GrainRow(s2, 0).LastConsumptionEatenUnits);
        Assert.Equal(100L + 27, GrainRow(s2, 1).LastConsumptionEatenUnits);
    }

    [Fact]
    public void System_EqualUrgency_EqualFill_LeftoverById_IndependentOfRowOrder()
    {
        WorldState w = Rig(new Site(5, 10, 0, 1), new Site(2, 10, 0, 1), new Site(9, 10, 0, 1), new Site(4, 7, 21, 1));
        WorldState on = Step(Exact(), w);   // spare 14 → 5/5/4 by id: 2 and 5 get 5, 9 gets 4
        Assert.Equal(5L, GrainRow(on, 2).LastConsumptionEatenUnits);
        Assert.Equal(5L, GrainRow(on, 5).LastConsumptionEatenUnits);
        Assert.Equal(4L, GrainRow(on, 9).LastConsumptionEatenUnits);

        // The same settlements in another row order: every settlement's outcome is identical.
        WorldState shuffled = Rig(new Site(9, 10, 0, 1), new Site(4, 7, 21, 1), new Site(5, 10, 0, 1), new Site(2, 10, 0, 1));
        WorldState on2 = Step(Exact(), shuffled);
        foreach (int id in new[] { 2, 4, 5, 9 })
        {
            Assert.Equal(GrainRow(on, id).LastConsumptionEatenUnits, GrainRow(on2, id).LastConsumptionEatenUnits);
            Assert.Equal(GrainRow(on, id).Amount.Value, GrainRow(on2, id).Amount.Value);
            Assert.Equal(Deficit(on, id).DeficitRatio, Deficit(on2, id).DeficitRatio);
        }
    }

    [Fact]
    public void System_DistinctUrgency_WorstServedFirst()
    {
        // 0 lacks 90% (R 100, 10 own), 1 lacks 50% (R 100, 50 own); the donor has 30 spare → all 30 to 0.
        WorldState w = Rig(new Site(0, 100, 10, 1), new Site(1, 100, 50, 1), new Site(2, 10, 40, 1));
        WorldState on = Step(Exact(), w);
        Assert.Equal(40L, GrainRow(on, 0).LastConsumptionEatenUnits);
        Assert.Equal(50L, GrainRow(on, 1).LastConsumptionEatenUnits);
        Assert.Equal(0.6, Deficit(on, 0).DeficitRatio, 15);
        Assert.Equal(0.5, Deficit(on, 1).DeficitRatio);
    }

    private static readonly Lazy<TerrainSet> SharedTerrain =
        new(() => WorldFounding.Found(TestConfigs.DevWorldgen(), Shipped, 42).Terrain!);

    private static WorldState Place(WorldState w, params (int X, int Y)[] at)
    {
        TerrainSet t = SharedTerrain.Value;
        w.Terrain = t;
        for (int s = 0; s < at.Length; s++)
            w.Settlements[s] = w.Settlements[s] with { SiteCell = t.Index(at[s].X, at[s].Y) };
        return w;
    }

    /// <summary>ADR-032's draw order, nearest-first to the RECIPIENT, ties by id — tie-dense: donors 2 and 3 sit
    /// 1 px from the recipient (a tie, id 2 first), 1 at 2 px, 4 far away. The recipient lacks 100; 2 and 3 have 30
    /// spare each, 1 has plenty: 30 + 30 from the tied pair, 40 from 1, nothing from the far one.</summary>
    [Fact]
    public void System_DrawOrder_NearestFirstToTheRecipient_TiesById_TieDense_RowOrderFree()
    {
        static WorldState Build(bool reverseRows)
        {
            Site[] sites =
            [
                new(0, 100, 0, 1), new(1, 10, 1_010, 1), new(2, 10, 40, 1), new(3, 10, 40, 1), new(4, 10, 1_010, 1),
            ];
            (int, int)[] at = [(100, 100), (102, 100), (100, 101), (101, 100), (140, 140)];
            if (reverseRows) { Array.Reverse(sites); Array.Reverse(at); }
            return Place(Rig(sites), at);
        }

        foreach (bool reverse in new[] { false, true })
        {
            WorldState w = Build(reverse);
            WorldState on = Step(Exact(), w);
            Assert.Equal(0.0, Deficit(on, 0).DeficitRatio);
            Assert.Equal(100L, GrainRow(on, 0).LastConsumptionEatenUnits);
            Assert.Equal(0L, GrainRow(on, 2).Amount.Value);              // nearest (tie, lower id): all 30 spare
            Assert.Equal(0L, GrainRow(on, 3).Amount.Value);              // next: all 30
            Assert.Equal(1_000L - 40, GrainRow(on, 1).Amount.Value);     // then 40 from the next nearest
            Assert.Equal(1_000L, GrainRow(on, 4).Amount.Value);          // the far donor is not touched
            AssertConservedExactly(w, on);
        }
    }

    /// <summary>THE DONOR ID TIE-BREAK, WITH TEETH: donors 2 and 3 tie at 1 px from the recipient and hold 30 spare
    /// each, and the recipient lacks only 40 — LESS than the tied pair's 60 — so the tie-break alone decides who is left
    /// holding grain: id 2 (the lower) gives all 30, id 3 the remaining 10 and keeps 20. The farther donors are not
    /// touched. Both row orders.</summary>
    [Fact]
    public void System_DrawOrder_TiedDonors_TheLowerIdGivesFirst_TieDense_RowOrderFree()
    {
        static WorldState Build(bool reverseRows)
        {
            Site[] sites =
            [
                new(0, 40, 0, 1), new(1, 10, 1_010, 1), new(2, 10, 40, 1), new(3, 10, 40, 1), new(4, 10, 1_010, 1),
            ];
            (int, int)[] at = [(100, 100), (102, 100), (100, 101), (101, 100), (140, 140)];
            if (reverseRows) { Array.Reverse(sites); Array.Reverse(at); }
            return Place(Rig(sites), at);
        }

        foreach (bool reverse in new[] { false, true })
        {
            WorldState w = Build(reverse);
            WorldState on = Step(Exact(), w);
            Assert.Equal(40L, Deficit(on, 0).DemandUnits);                // the premise: it lacks exactly 40
            Assert.Equal(0.0, Deficit(on, 0).DeficitRatio);
            Assert.Equal(40L, GrainRow(on, 0).LastConsumptionEatenUnits);
            Assert.Equal(0L, GrainRow(on, 2).Amount.Value);               // tie, lower id: drained first (30)
            Assert.Equal(20L, GrainRow(on, 3).Amount.Value);              // tie, higher id: gives the last 10
            Assert.Equal(1_000L, GrainRow(on, 1).Amount.Value);           // farther: untouched
            Assert.Equal(1_000L, GrainRow(on, 4).Amount.Value);
            AssertConservedExactly(w, on);
        }
    }

    /// <summary>RECIPIENTS EXECUTE IN ASCENDING ID, WHATEVER THE ROW ORDER. Recipients 3 and 7 (each lacking 40) share
    /// their nearest donor A (id 5, 50 spare, 25 px² from both) and have different second donors — B (id 1) nearer 3,
    /// C (id 9) nearer 7 (36 px² vs 136). Supply is ample, so both are granted 40; then id 3 draws first, all 40 from
    /// A, and id 7 finds 10 left at A and takes 30 from C: A 0, B 200, C 170 in EVERY row order. Executing in row
    /// order instead lets 7 drain A first wherever its row comes first, leaving B 170, C 200.</summary>
    [Fact]
    public void System_RecipientsExecuteInAscendingId_IndependentOfRowOrder_SharedNearestDonor()
    {
        (Site Site, (int, int) At) r3 = (new(3, 40, 0, 1), (100, 100)), r7 = (new(7, 40, 0, 1), (110, 100));
        (Site Site, (int, int) At) a = (new(5, 10, 60, 1), (105, 100));
        (Site Site, (int, int) At) b = (new(1, 10, 210, 1), (100, 106)), c = (new(9, 10, 210, 1), (110, 106));
        (Site Site, (int, int) At)[][] rowOrders =
        [
            [r3, r7, a, b, c],
            [r7, r3, c, a, b],
            [c, b, a, r7, r3],
            [a, r7, b, r3, c],
        ];
        foreach ((Site Site, (int, int) At)[] rows in rowOrders)
        {
            var sites = new Site[rows.Length];
            var at = new (int, int)[rows.Length];
            for (int i = 0; i < rows.Length; i++) { sites[i] = rows[i].Site; at[i] = rows[i].At; }
            WorldState w = Place(Rig(sites), at);
            WorldState on = Step(Exact(), w);
            Assert.Equal(0.0, Deficit(on, 3).DeficitRatio);
            Assert.Equal(0.0, Deficit(on, 7).DeficitRatio);
            Assert.Equal(40L, GrainRow(on, 3).LastConsumptionEatenUnits);
            Assert.Equal(40L, GrainRow(on, 7).LastConsumptionEatenUnits);
            Assert.Equal(0L, GrainRow(on, 5).Amount.Value);
            Assert.Equal(200L, GrainRow(on, 1).Amount.Value);
            Assert.Equal(170L, GrainRow(on, 9).Amount.Value);
            AssertConservedExactly(w, on);
        }
    }

    /// <summary>THE EMPTY-GRAIN-ROW CLAIMANT RULE. Settlement 1 (10 adults, 100 grain) carries a substitution credit of
    /// −0.5 in its staple ConsumeRemainder (the R4 remainder semantics), so it asks for floor(10 − 0.5) = 9 and eats 9:
    /// ONE UNIT SHORT BESIDE A STOCKED GRANARY (deficit 0.1, 91 still held) — the shape the floating-point remainder edge
    /// produces on the founded golden. It is a DONOR, never a claimant: (a) with no other claimant nothing moves (ON is
    /// byte-identical to OFF, its 0.1 stands, the rich sibling is untouched); (b) beside a starving claimant and no other
    /// donor, its 91 feed that claimant and its own one-unit shortfall is not relieved.</summary>
    [Fact]
    public void System_OneUnitShortBesideAStockedGranary_IsADonor_NotAClaimant()
    {
        static WorldState WithStapleCredit(WorldState w, int settlement, double credit)
        {
            int i = GoodStockIndex.IndexOf(w.GoodStocks, new SettlementId(settlement), new GoodId(Grain));
            w.GoodStocks.Ref(i).ConsumeRemainder = credit;
            return w;
        }

        // (a) the one-unit-short settlement and a rich sibling: no claimant at all.
        WorldState w = WithStapleCredit(Rig(new Site(0, 10, 1_000, 1), new Site(1, 10, 100, 1)), 1, -0.5);
        WorldState off = Step(Exact(on: false), w), on = Step(Exact(), w);
        Assert.Equal(10L, Deficit(off, 1).DemandUnits);                   // the premise, measured on the control:
        Assert.Equal(9L, GrainRow(off, 1).LastConsumptionEatenUnits);     // one unit short ...
        Assert.Equal(91L, GrainRow(off, 1).Amount.Value);                 // ... beside a stocked granary
        Assert.Equal(0.1, Deficit(off, 1).DeficitRatio, 15);
        Assert.Equal(WorldHash.ComputeHex(off), WorldHash.ComputeHex(on));
        Assert.Equal(0.1, Deficit(on, 1).DeficitRatio, 15);
        Assert.Equal(9L, GrainRow(on, 1).LastConsumptionEatenUnits);
        Assert.Equal(990L, GrainRow(on, 0).Amount.Value);

        // (b) the same settlement beside a starving claimant (100 adults, nothing) and no other donor.
        WorldState w2 = WithStapleCredit(Rig(new Site(1, 10, 100, 1), new Site(2, 100, 0, 1)), 1, -0.5);
        WorldState on2 = Step(Exact(), w2);
        Assert.Equal(91L, GrainRow(on2, 2).LastConsumptionEatenUnits);    // all 91 of the donor's grain
        Assert.Equal(0L, GrainRow(on2, 1).Amount.Value);
        Assert.Equal(9L, GrainRow(on2, 1).LastConsumptionEatenUnits);     // its own one-unit shortfall stands
        Assert.Equal(0.1, Deficit(on2, 1).DeficitRatio, 15);
        Assert.Equal(0.09, Deficit(on2, 2).DeficitRatio, 15);
        AssertConservedExactly(w2, on2);
    }

    /// <summary>CONTESTED CONTROL. Place 1 carries TWO Controls rows, polity 1's and polity 2's (legal in a hand-built
    /// or deserialized world — EmpireQuery). Its controller is the LOWEST polity id, 1, so its spare is polity 1's alone.
    /// Polity 2's starving settlement 2 has polity 2's own donor 3 (100 spare), and place 1 — which polity 2 holds a row
    /// for — comes FIRST in its terrain-less draw list (ascending id). No grain may cross from place 1 to polity 2:
    /// place 1 feeds only polity 1's settlement 0 and closes at 900; settlement 2 is fed from 3. Both Controls row orders.</summary>
    [Fact]
    public void System_ContestedControl_NoDrawCrossesToTheNonControllingPolity()
    {
        foreach (bool polity2RowFirst in new[] { false, true })
        {
            WorldState w = Rig(new Site(0, 100, 0, 1), new Site(1, 10, 1_010, 1), new Site(2, 100, 0, 2), new Site(3, 10, 110, 2));
            var contested = new ControlRow(new PolityId(2), new SettlementId(1), 1.0);
            if (polity2RowFirst)
            {
                var rows = new List<ControlRow> { contested };
                for (int i = 0; i < w.Controls.Count; i++) rows.Add(w.Controls[i]);
                w.Controls.Clear();
                foreach (ControlRow r in rows) w.Controls.Add(r);
            }
            else w.Controls.Add(contested);
            Assert.True(EmpireQuery.TryGetController(w, new SettlementId(1), out PolityId owner) && owner.Value == 1);
            Assert.Contains(new SettlementId(1), RoadDevelopmentQuery.PayingSettlements(w, new PolityId(2), new SettlementId(2), new SettlementId(2)));

            WorldState on = Step(Exact(), w);
            Assert.Equal(0.0, Deficit(on, 0).DeficitRatio);
            Assert.Equal(900L, GrainRow(on, 1).Amount.Value);            // 1000 spare − 100 to polity 1's settlement 0
            Assert.Equal(0.0, Deficit(on, 2).DeficitRatio);
            Assert.Equal(0L, GrainRow(on, 3).Amount.Value);              // polity 2 fed 2 from its own donor
            AssertConservedExactly(w, on);
        }
    }

    [Fact]
    public void System_StatelessSettlement_NeitherGivesNorReceives()
    {
        // 0 is stateless and starving beside a rich controlled 1; 2 is stateless and rich beside a starving
        // controlled 3. A settlement no polity controls is its own store (D-053 §2).
        WorldState w = Rig(new Site(0, 100, 0, 0), new Site(1, 10, 1_000, 1), new Site(2, 10, 1_000, 0), new Site(3, 100, 0, 1));
        WorldState on = Step(Exact(), w);
        Assert.Equal(1.0, Deficit(on, 0).DeficitRatio);
        Assert.Equal(990L, GrainRow(on, 2).Amount.Value);
        Assert.Equal(0.0, Deficit(on, 3).DeficitRatio);                  // fed by 1, its own polity
        Assert.Equal(990L - 100, GrainRow(on, 1).Amount.Value);
        // A world with no Controls at all: nothing pools (unlike the road purse's toy rule).
        WorldState toy = Rig(new Site(0, 100, 0, 0), new Site(1, 10, 1_000, 0));
        Assert.Equal(WorldHash.ComputeHex(Step(Exact(on: false), toy)), WorldHash.ComputeHex(Step(Exact(), toy)));
    }

    [Fact]
    public void System_CrossEmpireIsolation_NoFoodCrossesAPolityBoundary()
    {
        WorldState w = Rig(new Site(0, 100, 0, 1), new Site(1, 10, 1_000, 2), new Site(2, 100, 0, 2), new Site(3, 10, 30, 1));
        WorldState on = Step(Exact(), w);
        Assert.Equal(0.8, Deficit(on, 0).DeficitRatio, 15);               // polity 1 had only 20 spare (at 3)
        Assert.Equal(0L, GrainRow(on, 3).Amount.Value);
        Assert.Equal(0.0, Deficit(on, 2).DeficitRatio);                  // polity 2 fed 2 from 1
        Assert.Equal(990L - 100, GrainRow(on, 1).Amount.Value);
        AssertConservedExactly(w, on);
    }

    [Fact]
    public void System_ReliefBeforeBounding_SpoilageAndCapacityApplyToThePostReliefStore()
    {
        // Shipped spoilage (0.08/yr) and granary bound (1.5 years of the settlement's own grain demand): the donor's
        // overflow is computed on what is LEFT after it fed its sibling — grain a hungry sibling could eat is not
        // first destroyed as overflow.
        WorldState w = Rig(new Site(0, 100, 0, 1), new Site(1, 100, 1_000, 1));
        WorldState on = Step(Shipped, w), off = Step(TestConfigs.PreEmpireFoodAllocation(Shipped), w);
        // Off: 1 eats 100, keeps 900, spoils floor(900 × (1 − e^−0.08)) = 69, caps at 150 → 681 overflow.
        Assert.Equal(150L, GrainRow(off, 1).Amount.Value);
        // On: 1 eats 100, gives 100, keeps 800, spoils floor(800 × (1 − e^−0.08)) = 61, caps at 150.
        Assert.Equal(150L, GrainRow(on, 1).Amount.Value);
        Assert.Equal(0.0, Deficit(on, 0).DeficitRatio);
        ConservedQuantityId q = ConservedQuantityIds.OfGood(new GoodId(Grain));
        Assert.Equal(Sunk(off, ReasonIds.Spoilage, q) - 8, Sunk(on, ReasonIds.Spoilage, q));
        Assert.Equal(Sunk(off, ReasonIds.GranaryOverflow, q) - 92, Sunk(on, ReasonIds.GranaryOverflow, q));
        AssertConservedExactly(w, on);
    }

    [Fact]
    public void System_DeterministicReplay_TwoRunsIdentical_OverManyTurns()
    {
        // A shrinking empire: no production in this pipeline, so stores run down and relief happens every turn
        // until the food is gone. Two independent runs must agree byte for byte at every turn.
        // Run under the exact rig config (stores last, relief on several turns) and under the shipped one (spoilage
        // and the granary bound in play).
        static WorldState Start() => Rig(
            new Site(0, 100, 0, 1), new Site(1, 60, 900, 1), new Site(2, 80, 40, 1), new Site(3, 50, 300, 2), new Site(4, 40, 0, 2));
        foreach ((SimConfig cfg, int minRelief) in new[] { (Exact(), 3), (Shipped, 1) })
        {
            WorldState a = Start(), b = Start();
            int reliefTurns = 0;
            for (int t = 0; t < 12; t++)
            {
                WorldState na = Step(cfg, a), nb = Step(cfg, b);
                Assert.Equal(WorldHash.ComputeHex(na), WorldHash.ComputeHex(nb));
                if (WorldHash.ComputeHex(na) != WorldHash.ComputeHex(Step(TestConfigs.PreEmpireFoodAllocation(cfg), a))) reliefTurns++;
                AssertConservedExactly(a, na);
                a = na; b = nb;
            }
            Assert.True(reliefTurns >= minRelief, $"relief happened on only {reliefTurns} turns — the replay check is vacuous");
        }
    }

    [Fact]
    public void Switch_ShipsOn_AndTheControlTurnsItOff()
    {
        Assert.True(Shipped.Consumption.EmpireFoodAllocation);
        Assert.False(TestConfigs.PreEmpireFoodAllocation(Shipped).Consumption.EmpireFoodAllocation);
    }

    // ================================================================== THE TURN (full pipeline)

    internal static TurnExecutor Pipeline(SimConfig cfg, WorldgenConfig wg, OrderLog? orders = null)
    {
        using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
        using var pipeStream = Sim.Data.DataFiles.OpenPipeline();
        return new TurnExecutor(EraTableLoader.Load(eraStream), PipelineLoader.Load(pipeStream, SystemCatalog.All(cfg, wg)), orders);
    }

    private static long StarvedTotal(IReadOnlyWorldState w) => Sunk(w, ReasonIds.Starvation, ConservedQuantityIds.Population);

    /// <summary>The canonical founded world (seed 42) with settlement 0's food labour abandoned (Farming 0,
    /// Herding 0: its harvest is zero) and its grain, livestock and fish stores emptied through the Ledger.</summary>
    internal static WorldState AbandonedSettlementRig(SimConfig cfg, out SettlementId s0)
    {
        WorldState start = WorldFounding.Found(TestConfigs.Worldgen(), cfg, 42);
        Assert.Equal(12, start.Settlements.Count);
        s0 = start.Settlements[0].Id;
        Assert.True(EmpireQuery.TryGetController(start, s0, out PolityId owner) && owner.Value == 1);
        Abandon(start, cfg, s0);
        return start;
    }

    /// <summary>Settlement <paramref name="s"/>'s food labour abandoned (a SectorAllocation row Farming 0, Herding 0,
    /// in force from PREV) and its grain, livestock and fish stores emptied through the Ledger.</summary>
    private static void Abandon(WorldState start, SimConfig cfg, SettlementId s)
    {
        start.SectorAllocations.Add(new SectorAllocationRow(s, Farming: 0.0, Herding: 0.0, Extraction: 0.5, Crafting: 0.3, Construction: 0.2));
        var ledger = new Ledger(start.LedgerFlows);
        foreach (string food in new[] { "grain", "livestock", "fish" })
        {
            var good = new GoodId(cfg.Goods!.IdOf(food));
            int i = GoodStockIndex.IndexOf(start.GoodStocks, s, good);
            long held = start.GoodStocks[i].Amount.Value;
            if (held > 0)
                ledger.Flow(ref start.GoodStocks.Ref(i).Amount, ConservedQuantityIds.OfGood(good), ReasonIds.Spoilage,
                    held, FlowDirection.Sink, OverdrawPolicy.Throw);
        }
        Assert.True(FoodState.IsAbandoned(start, s));
    }

    /// <summary>
    /// THE TURN-LEVEL INTEGRATION: the canonical founded world (one Empire, twelve settlements). Settlement 0's
    /// food labour is abandoned (Farming 0, Herding 0 — its harvest is zero) and its food stores are empty. Switch
    /// off: it eats nothing, classifies FAMINE (abandonment) and its people starve the next turn. Switch on: its
    /// siblings' spare grain feeds it in full that same turn, its deficit is 0, it classifies NORMAL, and nobody in
    /// the world starves. No new demographic rule is involved: starvation reads the deficit ratio as it always has.
    /// </summary>
    [Fact]
    public void Turn_HarvestFailsAtOneSettlement_FedBySiblings_NobodyStarves()
    {
        WorldgenConfig wg = TestConfigs.Worldgen();
        SimConfig on = Shipped, off = TestConfigs.PreEmpireFoodAllocation(Shipped);
        WorldState start = AbandonedSettlementRig(on, out SettlementId s0);

        TurnExecutor exOn = Pipeline(on, wg), exOff = Pipeline(off, wg);
        WorldState a1 = exOn.Step(start), b1 = exOff.Step(start);

        // OFF: the failure is a famine.
        double dOff = FoodState.DeficitRatio(b1, s0);
        Assert.True(dOff > 0.9, $"switch-off deficit {dOff} — the rig did not fail the harvest");
        Assert.Equal(FoodStateKind.Famine, FoodState.Of(b1, s0, off, out FamineReason why));
        Assert.Equal(FamineReason.Abandonment, why);

        // ON: fed in full by its siblings the same turn.
        Assert.Equal(0.0, FoodState.DeficitRatio(a1, s0));
        Assert.Equal(FoodStateKind.Normal, FoodState.Of(a1, s0, on, out _));
        long required = Deficit(a1, s0.Value).DemandUnits;
        Assert.True(required > 0);
        GoodStockRow g0 = GrainRow(a1, s0.Value);
        Assert.Equal(required, g0.LastConsumptionEatenUnits);            // everything it ate came from its siblings
        Assert.Equal(0L, g0.Amount.Value);
        Assert.True(ConservationAuditor.IsConserved(a1, out string report), report);
        Assert.Equal(TotalGrain(b1) - required, TotalGrain(a1) - (Sunk(b1, ReasonIds.Spoilage, ConservedQuantityIds.OfGood(new GoodId(Grain)))
            - Sunk(a1, ReasonIds.Spoilage, ConservedQuantityIds.OfGood(new GoodId(Grain))))
            - (Sunk(b1, ReasonIds.GranaryOverflow, ConservedQuantityIds.OfGood(new GoodId(Grain)))
            - Sunk(a1, ReasonIds.GranaryOverflow, ConservedQuantityIds.OfGood(new GoodId(Grain)))));

        // The next turn the demographic kernel spends the deficit: starvation off, none on.
        WorldState a2 = exOn.Step(a1), b2 = exOff.Step(b1);
        Assert.True(StarvedTotal(b2) - StarvedTotal(b1) > 0, "switch off: the famine starved nobody — rig vacuous");
        Assert.Equal(0L, StarvedTotal(a2) - StarvedTotal(start));
        Assert.True(ConservationAuditor.IsConserved(a2, out string report2), report2);
    }

    // ================================================================== WHAT RELIEF DOES NOT REMOVE (shipped config)
    // D-053 §2 moves grain only while the polity HAS spare grain. The per-settlement regimes the lost-premise rigs
    // measure (CR-015 abandonment famine, starvation) now run on TestConfigs.PreEmpireFoodAllocation; these two prove
    // on the SHIPPED config that both regimes stay reachable when the polity has none to spare.

    /// <summary>
    /// (a) ABANDONMENT FAMINE IS STILL REACHABLE IN PLAY (F-138 / CR-015 item 1(B), with hazardPerYear 0.0 its only
    /// shipped cause). The SHIPPED config and the order S_Abandonment_TriggersFamine issues — LaborAllocation 0 % at
    /// turn 1 — given to EVERY settlement of the polity: soon there is no spare grain anywhere in it, and settlement 0
    /// classifies FAMINE/Abandonment with every grain row of its polity at zero (relief ran dry; it did not skip the
    /// settlement), its effective deficit is the whole nominal one, and people starve the next turn. The twin with
    /// the order at settlement 0 ALONE never reaches FAMINE there in the same 12 turns: its siblings feed it — the
    /// regime moved, it did not vanish.
    /// </summary>
    [Fact]
    public void Shipped_AbandonmentFamine_IsReachable_WhenThePolityHasNoSpareGrain()
    {
        SimConfig cfg = Shipped;
        Assert.True(cfg.Consumption.EmpireFoodAllocation);
        WorldgenConfig wg = TestConfigs.Worldgen();
        WorldState founded = WorldFounding.Found(wg, cfg, 42);
        var everyone = new OrderLog();
        var onlyZero = new OrderLog();
        for (int s = 0; s < founded.Settlements.Count; s++)
        {
            SettlementId id = founded.Settlements[s].Id;
            Assert.True(EmpireQuery.TryGetController(founded, id, out PolityId p) && p.Value == 1);
            everyone.Append(new OrderRecord(1, ActorId: 1, OrderKind.LaborAllocation, TargetId: id.Value, Amount: 0.0));
        }
        onlyZero.Append(new OrderRecord(1, ActorId: 1, OrderKind.LaborAllocation, TargetId: 0, Amount: 0.0));
        TurnExecutor all = Pipeline(cfg, wg, everyone), one = Pipeline(cfg, wg, onlyZero);
        WorldState a = founded, b = WorldFounding.Found(wg, cfg, 42);
        var s0 = new SettlementId(0);
        int famineTurn = -1, twinFamineTurns = 0;
        for (int t = 1; t <= 12; t++)
        {
            a = all.Step(a);
            b = one.Step(b);
            if (FoodState.Of(b, s0, cfg, out _) == FoodStateKind.Famine) twinFamineTurns++;
            if (famineTurn > 0) continue;
            if (FoodState.Of(a, s0, cfg, out FamineReason why) != FoodStateKind.Famine) continue;
            famineTurn = t;
            Assert.True(why is FamineReason.Abandonment or FamineReason.Both, $"turn {t}: FAMINE for reason {why}");
            for (int s = 0; s < a.Settlements.Count; s++)
                if (EmpireQuery.TryGetController(a, a.Settlements[s].Id, out PolityId p) && p.Value == 1)
                    Assert.Equal(0L, GrainRow(a, a.Settlements[s].Id.Value).Amount.Value);   // no spare grain left
            double d = FoodState.DeficitRatio(a, s0);
            Assert.True(d > 0.0);
            Assert.Equal(d, FoodState.EffectiveDeficit(d, FoodStateKind.Famine, cfg));
            WorldState after = all.Step(a);
            Assert.True(StarvedTotal(after) > StarvedTotal(a), $"turn {t + 1}: the famine starved nobody");
        }
        Assert.True(famineTurn > 0, "every settlement abandoned, yet settlement 0 never classified FAMINE in 12 turns");
        Assert.Equal(0, twinFamineTurns);
    }

    /// <summary>
    /// (b) A STARVING SETTLEMENT IN A POLITY SHORT OVERALL GETS ONLY ITS WATER-FILLED SHARE AND STILL STARVES (shipped
    /// config, full pipeline, one step). Every settlement but settlement 1 has empty food stores; settlement 1 keeps
    /// farming and its stores (the donor); settlement 2 keeps a SMALL farming share, so it harvests part of what it needs
    /// and eats it before relief (a claim at a LOWER unmet fraction, 0 &lt; f &lt; 1); the other ten are abandoned (u = R,
    /// f = 1). The CONTROL step (switch off, same world) measures each settlement's own eating, so u = R − own eating.
    /// With the switch on every unit settlement 1 could spare is spent (it closes EMPTY) on less than the claimants lack,
    /// and each claimant's grant is EXACTLY FoodRelief.Allocate(claims, Σ grants) — the water-filled share, nothing more.
    /// Because the claims sit at DIFFERENT unmet fractions, the test also tells water-filling from a proportional
    /// division: the partly-fed settlement 2 gets strictly less than its proportional share (the worst-off are brought
    /// down first). Settlement 0 is left with a positive deficit, classifies FAMINE/Abandonment and the next step starves.
    /// </summary>
    [Fact]
    public void Shipped_ShortPolity_AStarvingSettlementGetsItsWaterFilledShare_AndStillStarves()
    {
        SimConfig on = Shipped, off = TestConfigs.PreEmpireFoodAllocation(Shipped);
        WorldgenConfig wg = TestConfigs.Worldgen();
        WorldState start = WorldFounding.Found(wg, on, 42);
        var donor = new SettlementId(1);
        var partly = new SettlementId(2);
        for (int s = 0; s < start.Settlements.Count; s++)
            if (start.Settlements[s].Id != donor) Abandon(start, on, start.Settlements[s].Id);
        // Settlement 2 farms again on a small share: a partial own harvest (its stores stay empty).
        for (int i = 0; i < start.SectorAllocations.Count; i++)
            if (start.SectorAllocations[i].Settlement == partly)
                start.SectorAllocations[i] = new SectorAllocationRow(partly, Farming: 0.05, Herding: 0.0, Extraction: 0.5, Crafting: 0.25, Construction: 0.2);

        TurnExecutor exOn = Pipeline(on, wg), exOff = Pipeline(off, wg);
        WorldState a1 = exOn.Step(start), b1 = exOff.Step(start);

        var claims = new List<FoodClaim>();
        var observed = new List<long>();
        long granted = 0, unmet = 0;
        int partlyAt = -1;
        for (int s = 0; s < start.Settlements.Count; s++)
        {
            int id = start.Settlements[s].Id.Value;
            if (id == donor.Value) continue;
            long required = Deficit(b1, id).DemandUnits;
            Assert.Equal(required, Deficit(a1, id).DemandUnits);
            long own = FoodEaten(b1, id);                                        // the control: what it ate of its own
            Assert.Equal(0L, GrainRow(b1, id).Amount.Value);                     // ...and its store closed empty: a claimant
            if (id == partly.Value)
            {
                Assert.True(own > 0 && own < required, $"settlement 2 ate {own} of {required} of its own — the rig needs a PARTIAL harvest");
                partlyAt = claims.Count;
            }
            else
            {
                Assert.Equal(0L, own);
                Assert.Equal(1.0, Deficit(b1, id).DeficitRatio);
            }
            long grant = FoodEaten(a1, id) - own;                                // own eating is the same either way; the rest is relief
            claims.Add(new FoodClaim(id, required, required - own, (int)FoodState.Of(start, new SettlementId(id), on, out _)));
            observed.Add(grant);
            granted += grant;
            unmet += required - own;
        }
        Assert.True(granted > 0, "the donor spared nothing — the rig is vacuous");
        Assert.True(granted < unmet, $"granted {granted} of {unmet}: the polity was not short overall");
        Assert.Equal(0L, GrainRow(a1, donor.Value).Amount.Value);              // every spare unit spent
        Assert.True(GrainRow(b1, donor.Value).Amount.Value > 0, "control: the donor kept nothing to spare");
        long[] expected = FoodRelief.Allocate(claims.ToArray(), granted);
        for (int i = 0; i < claims.Count; i++)
            Assert.True(expected[i] == observed[i],
                $"settlement {claims[i].Settlement}: granted {observed[i]}, its water-filled share is {expected[i]}");

        // Water-filling, not proportional division: the partly-fed claimant (the LOWEST unmet fraction) gets strictly less
        // than the floor of its proportional share granted × u / Σu — the fully unmet claimants are served down to it first.
        long proportional = (long)(System.Numerics.BigInteger.Multiply(granted, claims[partlyAt].Unmet) / unmet);
        Assert.True(observed[partlyAt] < proportional,
            $"settlement 2 (f = {claims[partlyAt].Unmet}/{claims[partlyAt].Required}) got {observed[partlyAt]}, proportional {proportional}: the rig does not tell the two apart");

        var s0 = new SettlementId(0);
        double d0 = FoodState.DeficitRatio(a1, s0);
        Assert.True(d0 > 0.0 && d0 < 1.0, $"settlement 0's deficit {d0}: it should be relieved in part, not in full");
        Assert.Equal(FoodStateKind.Famine, FoodState.Of(a1, s0, on, out FamineReason why));
        Assert.Equal(FamineReason.Abandonment, why);
        WorldState a2 = exOn.Step(a1);
        Assert.True(StarvedTotal(a2) > StarvedTotal(a1), "the short polity's famine starved nobody");
        Assert.True(ConservationAuditor.IsConserved(a2, out string report), report);
    }

    /// <summary>Food eaten this step, Σ over the food goods' LastConsumptionEatenUnits (the deficit ratio's numerator).</summary>
    private static long FoodEaten(IReadOnlyWorldState w, int settlement)
    {
        long eaten = 0;
        foreach (string food in new[] { "grain", "livestock", "fish" })
        {
            int i = GoodStockIndex.IndexOf(w.GoodStocks, new SettlementId(settlement), new GoodId(Shipped.Goods!.IdOf(food)));
            if (i >= 0) eaten += w.GoodStocks[i].LastConsumptionEatenUnits;
        }
        return eaten;
    }

    // ================================================================== THE SWITCH-OFF CONTROL
    // The pre-relief (de86ace) pins as LITERALS, so a later re-pin of the live constants cannot silently re-aim the control.
    // On the combined D-053 tree the relief switch alone no longer returns de86ace (the purse and the granary effects stay
    // on), so the switch-off arm is the BATCH off (TestConfigs.PreD053): each world must return its de86ace hash byte for
    // byte. Which layer moved a pin is the PER-LAYER proof below: the combined world against the combined world with ONLY
    // relief off, first differing exactly at the turn relief first moves grain.

    // D-054 (schema v33): these literals are v32 measurements, so every comparison goes through
    // SchemaProjection.V32 (the two empty v33 tables dropped; asserted empty).
    private const string De86aceFoundedGolden = "937577ffb0e73c2b051d8d7832cfb5028c2154d01c9afef4d976b71ccb5e7f54";
    private const string De86aceDrivenGolden = "7bcdc03ff5bf82ff22a527e9d1a28b614d216ddcee0265aacc98b44280a92033";
    private const string De86aceAiWorldTurn300 = "aa7a87a98370c24815d478877a3f4f862838fde51a33fd5d0841157ca73066a7";

    /// <summary>The canonical founded golden (seed 42, 300 turns). MEASURED: the shipped (ON) world returns this
    /// hash too — its only deficit settlement-turns (6 of 3,600) are one-unit remainder-convention shortfalls beside
    /// a stocked granary, i.e. donors, not claimants, so no grain moves (SnapshotTests' pin stays green).</summary>
    [Fact]
    public void Switch_Off_FoundedGolden_ByteIdenticalToPreRelief()
    {
        Assert.Equal(De86aceFoundedGolden,
            SchemaProjection.V32(Sim.Tests.Kernel.SnapshotTests.RunFoundedGolden(TestConfigs.PreD053(TestConfigs.Sim()), TestConfigs.PreD054Worldgen())));
        Assert.Equal(De86aceFoundedGolden,
            SchemaProjection.V32(Sim.Tests.Kernel.SnapshotTests.RunFoundedGolden(
                TestConfigs.PreEmpireFoodAllocation(TestConfigs.PreD054Sim(TestConfigs.Sim())), TestConfigs.PreD054Worldgen())));
    }

    /// <summary>The driven golden (seed 42, 300 turns, the driven order batch) — a world where relief DOES happen
    /// (StoreLossTests: relief settlement-turns from turn 5), so the shipped world moves; the batch-off control returns
    /// it. The per-layer proof is <see cref="PerLayer_Relief_TheDrivenWorldDivergesExactlyAtTheFirstRelief"/>.</summary>
    [Fact]
    public void Switch_Off_DrivenGolden_ByteIdenticalToPreRelief_AndOnItMoves()
    {
        // D-054 (batch 3): the PreD054 PAIR — founded flat (PreD054Worldgen), the sim half stripped (PreD053 strips
        // PreD054Sim), compared through the v32 projection.
        (WorldState off, _) = Sim.Tests.Kernel.DrivenGoldenTests.RunDriven(300, TestConfigs.PreD053(TestConfigs.Sim()), TestConfigs.PreD054Worldgen());
        Assert.Equal(De86aceDrivenGolden, SchemaProjection.V32(off));
        (WorldState on, _) = Sim.Tests.Kernel.DrivenGoldenTests.RunDriven(300, TestConfigs.PreDiscovery(TestConfigs.Sim()), TestConfigs.PreD054Worldgen());   // M6 EX: discovery off (R4)
        Assert.NotEqual(De86aceDrivenGolden, SchemaProjection.V32(on));
    }

    /// <summary>Whether intra-polity relief moved grain in the step <paramref name="prev"/> → <paramref name="next"/>,
    /// read off the grain books alone (never the switch): some settlement's grain residual opening + harvest − eaten −
    /// closing is NEGATIVE — it ate grain it neither held nor harvested. On the driven world (no raid, no founding) relief
    /// is the only inbound transfer (StoreLossTests).</summary>
    private static bool ReliefMovedGrain(IReadOnlyWorldState prev, IReadOnlyWorldState next)
    {
        for (int s = 0; s < next.Settlements.Count; s++)
        {
            int p = GoodStockIndex.IndexOf(prev.GoodStocks, next.Settlements[s].Id, new GoodId(Grain));
            int n = GoodStockIndex.IndexOf(next.GoodStocks, next.Settlements[s].Id, new GoodId(Grain));
            if (p < 0 || n < 0) continue;
            GoodStockRow r = next.GoodStocks[n];
            if (prev.GoodStocks[p].Amount.Value + r.LastProducedUnits - r.LastConsumptionEatenUnits - r.Amount.Value < 0) return true;
        }
        return false;
    }

    /// <summary>
    /// THE PER-LAYER PROOF (turn-exact): the driven world on the combined tree (the shipped batch) against the same world with
    /// ONLY relief off. They are byte-identical until the first step in which relief moves grain (read off the grain books
    /// of the combined run), and differ from that very step on. MEASURED on this tree: turn 5, and the relief-off run
    /// returns de86ace's driven golden — relief is the whole of the driven golden's movement (the purse and the granary
    /// effects leave this world unmoved: it builds no granary).
    /// </summary>
    [Fact]
    public void PerLayer_Relief_TheDrivenWorldDivergesExactlyAtTheFirstRelief()
    {
        // D-054: the combined BATCH-2 tree — the PreD054 PAIR: WS1/WS2 off (TestConfigs.PreD054Sim) and founded flat
        // (TestConfigs.PreD054Worldgen) — so the relief-off world is a PreD054 world and its de86ace literal compares
        // through the v32 projection.
        SimConfig on = TestConfigs.PreD054Sim(TestConfigs.Sim()), off = TestConfigs.PreEmpireFoodAllocation(on);
        WorldState w0 = WorldFounding.Found(TestConfigs.PreD054Worldgen(), on, 42);
        OrderLog orders = Sim.Tests.Kernel.DrivenGoldenTests.DrivingOrders(w0.Settlements.Count);
        OrderValidation.ValidateAgainstWorld(orders, w0);
        TurnExecutor exOn = Pipeline(on, TestConfigs.PreD054Worldgen(), orders), exOff = Pipeline(off, TestConfigs.PreD054Worldgen(), orders);
        WorldState a = w0, b = w0.Clone();
        long firstRelief = -1, firstDifferent = -1;
        for (int t = 1; t <= 300; t++)
        {
            WorldState prev = a;
            a = exOn.Step(prev);
            b = exOff.Step(b);
            if (firstRelief < 0 && ReliefMovedGrain(prev, a)) firstRelief = a.Clock.Turn;
            if (firstDifferent < 0 && WorldHash.ComputeHex(a) != WorldHash.ComputeHex(b)) firstDifferent = a.Clock.Turn;
        }
        Assert.True(firstRelief > 0, "relief never moved grain on the driven world — the proof is vacuous");
        Assert.Equal(firstRelief, firstDifferent);
        Assert.Equal(De86aceDrivenGolden, SchemaProjection.V32(b));      // relief off, the purse and granaries on: de86ace's
    }

    /// <summary>The AI world: `sim run --founded --seed 42 --turns 300 --ai-empires 1` (the AiFoundingAttributionTests
    /// harness). Two Empires, so relief is per polity; the batch-off control returns the de86ace hash.</summary>
    [Fact]
    public void Switch_Off_AiWorld_ByteIdenticalToPreRelief()
    {
        SimConfig off = TestConfigs.PreD053(TestConfigs.Sim());
        WorldgenConfig wg = TestConfigs.PreD054Worldgen() with { AiEmpires = 1 };
        var log = new OrderLog();
        TurnExecutor exec;
        using (var eraStream = Sim.Data.DataFiles.OpenEraPacing())
        using (var pipeStream = Sim.Data.DataFiles.OpenPipeline())
            exec = new TurnExecutor(EraTableLoader.Load(eraStream),
                PipelineLoader.Load(pipeStream, SystemCatalog.All(off, TestConfigs.PreD054Worldgen())), log);
        WorldState w = WorldFounding.Found(wg, off, 42);
        Assert.Equal(2, w.Polities.Count);
        for (int t = 1; t <= 300; t++)
        {
            AiOrders.Append(log, w, off, TestConfigs.PreD054Worldgen());
            w = exec.Step(w);
        }
        Assert.Equal(De86aceAiWorldTurn300, SchemaProjection.V32(w));
    }
}
