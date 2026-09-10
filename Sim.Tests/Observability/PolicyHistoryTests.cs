using Sim.Core.Kernel;
using Sim.Core.Observability;
using Sim.Core.State;
using Sim.Tests.Kernel;

namespace Sim.Tests.Observability;

/// <summary>
/// §4 — THE POLICY HISTORY: a change is detected on the SectorAllocationRow and
/// attributed to the order (by log index) that set it; a state is recorded for
/// every settlement on every turn, changed or not.
///
/// TURN-EXACT PINS (T1.9 precedent: replay equality cannot see stamping drift).
/// DrivenGoldenTests' orders carry Turn 2, so they are delivered to the step
/// 2 → 3 (TurnExecutor.Step: BatchFor(prev.Clock.Turn)); the row is first
/// visible on turn-3 state, so the change is recorded at Turn 3; the shares IN
/// FORCE during the 2 → 3 step were still the default (Production reads prev),
/// and the new mix is in force from the 3 → 4 step. Each of those is pinned.
/// </summary>
public class PolicyHistoryTests
{
    private static readonly double[][] Mixes =
    [
        [0.70, 0.10, 0.05, 0.05, 0.10],
        [0.30, 0.10, 0.45, 0.05, 0.10],
        [0.30, 0.10, 0.10, 0.40, 0.10],
    ];

    [Fact]
    public void Driven_RecordsEveryChangeWithOldNewActorAndOrderIndex_OnTurn3Only()
    {
        ObservationLog log = ObservedWorlds.Driven300.Value.Log;
        SectorAllocationRow dflt = Sectors.Default(new SettlementId(0));

        // 12 settlements × 5 sectors = 60 orders; group C's extraction (0.10)
        // equals the default, so 4 of the 60 change nothing — 56 changes.
        Assert.Equal(56, log.PolicyChanges.Count);
        foreach (PolicyChange c in log.PolicyChanges)
        {
            Assert.Equal(3, c.Turn);
            Assert.Equal(30.0, c.Year);
            Assert.Equal(1, c.Actor);
            Assert.Equal(c.Settlement * 5 + c.Sector, c.OrderIndex);   // ascending settlement, then sector
            Assert.Equal(Sectors.Raw(dflt, c.Sector), c.OldWeight);
            Assert.Equal(Mixes[c.Settlement % 3][c.Sector] , c.NewWeight);
            Assert.NotEqual(c.OldWeight, c.NewWeight);
        }
        PolicyChange first = log.PolicyChanges[0];
        Assert.Equal(0, first.Settlement);
        Assert.Equal(Sectors.Farming, first.Sector);
        Assert.Equal(0.55, first.OldWeight);
        Assert.Equal(0.70, first.NewWeight);
        Assert.Equal(0, first.OrderIndex);
        // Group C (2, 5, 8, 11): no extraction change recorded.
        foreach (PolicyChange c in log.PolicyChanges)
            Assert.False(c.Settlement % 3 == 2 && c.Sector == Sectors.Extraction, "a no-op order was recorded as a change");
    }

    [Fact]
    public void Driven_RecordsAStateForEverySettlementEveryTurn_ChangedOrNot()
    {
        ObservationLog log = ObservedWorlds.Driven300.Value.Log;
        Assert.Equal(12 * 300, log.PolicyStates.Count);
        for (int i = 0; i < log.PolicyStates.Count; i++)
        {
            PolicyState st = log.PolicyStates[i];
            Assert.Equal(i / 12 + 1, st.Turn);
            Assert.Equal(i % 12, st.Settlement);
            var row = new SectorAllocationRow(new SettlementId(st.Settlement),
                st.Declared[0], st.Declared[1], st.Declared[2], st.Declared[3], st.Declared[4]);
            for (int sec = 0; sec < Sectors.Count; sec++)
                Assert.Equal(Sectors.Share(row, sec), st.Effective[sec]);   // recomputed through the public accessor
            double[] expected = st.Turn <= 2
                ? [0.55, 0.15, 0.10, 0.12, 0.08]
                : Mixes[st.Settlement % 3];
            Assert.Equal(expected, st.Declared);
        }

        // THE ONE-TURN LAG, PINNED on the turn record's in-force shares.
        double[] defaultShares = Shares(Sectors.Default(new SettlementId(0)));
        foreach (PolicyInForce p in log.At(3)!.Turn.Policy) Assert.Equal(defaultShares, p.Shares);
        foreach (PolicyInForce p in log.At(4)!.Turn.Policy)
        {
            double[] m = Mixes[p.Settlement % 3];
            Assert.Equal(Shares(new SectorAllocationRow(new SettlementId(p.Settlement), m[0], m[1], m[2], m[3], m[4])), p.Shares);
        }
        // And the same on the settlement record: in-force is prev, declared is next.
        SettlementRecord s0 = log.Settlement(3, 0)!;
        Assert.Equal(defaultShares, s0.Economy.SectorShares);
        Assert.Equal(Mixes[0], s0.Policy.DeclaredWeights);
        Assert.True(s0.Policy.DeclaredRowPresent);
        Assert.False(s0.Economy.SectorRowPresent);   // prev (turn 2) had no row yet

        // The orders applied on the 2 -> 3 step, with their log indices.
        OrderApplied[] applied = log.At(3)!.Turn.Orders;
        Assert.Equal(60, applied.Length);
        for (int i = 0; i < applied.Length; i++)
        {
            Assert.Equal(i, applied[i].Index);
            Assert.Equal(2, applied[i].Turn);
            Assert.Equal(i / 5, applied[i].Settlement);
            Assert.Equal(i % 5, applied[i].Sector);
        }
        Assert.Empty(log.At(2)!.Turn.Orders);
        Assert.Empty(log.At(4)!.Turn.Orders);
        Assert.Equal(5, log.Settlement(3, 7)!.Orders.Length);
        Assert.Equal(35, log.Settlement(3, 7)!.Orders[0].Index);
    }

    [Fact]
    public void Founded_NeverOrdered_RecordsDefaultStateEveryTurnAndNoChange()
    {
        ObservationLog log = ObservedWorlds.Founded300.Value.Log;
        Assert.Empty(log.PolicyChanges);
        Assert.Equal(12 * 300, log.PolicyStates.Count);
        foreach (PolicyState st in log.PolicyStates)
        {
            Assert.Equal([0.55, 0.15, 0.10, 0.12, 0.08], st.Declared);   // Sectors.Default, WorldState.cs
        }
        foreach (TurnObservation o in log.Observations)
            foreach (SettlementRecord r in o.Settlements)
                Assert.False(r.Policy.DeclaredRowPresent);
    }

    [Fact]
    public void ALegacyLaborOrder_ChangesEverySector_AllAttributedToThatOneOrder_LastOrderWins()
    {
        // LaborAllocation maps farm% onto farming and the remainder onto
        // construction with the other three zeroed (PathBuildSystem.cs:80-88) —
        // one order, five changes, one index. Two orders for the same target on
        // the same turn: the second Upsert stands, so attribution names it.
        var orders = new OrderLog();
        orders.Append(new OrderRecord(Turn: 1, ActorId: 1, OrderKind.LaborAllocation, TargetId: 0, Amount: 90.0));
        orders.Append(new OrderRecord(Turn: 1, ActorId: 1, OrderKind.LaborAllocation, TargetId: 0, Amount: 40.0));
        ObservationLog log = ObservedWorlds.Observed(ObservedWorlds.Founded(), orders, 3).Log;

        Assert.Equal(5, log.PolicyChanges.Count);
        double[] expectedNew = [0.4, 0.0, 0.0, 0.0, 1.0 - 0.4];
        for (int i = 0; i < 5; i++)
        {
            PolicyChange c = log.PolicyChanges[i];
            Assert.Equal(2, c.Turn);
            Assert.Equal(0, c.Settlement);
            Assert.Equal(i, c.Sector);
            Assert.Equal(Sectors.Raw(Sectors.Default(new SettlementId(0)), i), c.OldWeight);
            Assert.Equal(expectedNew[i], c.NewWeight);
            Assert.Equal(1, c.Actor);
            Assert.Equal(1, c.OrderIndex);   // the LAST of the two, not the first
        }
        Assert.Equal(2, log.At(2)!.Turn.Orders.Length);
        Assert.Equal(2, log.Settlement(2, 0)!.Orders.Length);
        Assert.Empty(log.Settlement(2, 1)!.Orders);
    }

    private static double[] Shares(in SectorAllocationRow row)
    {
        var s = new double[Sectors.Count];
        for (int i = 0; i < Sectors.Count; i++) s[i] = Sectors.Share(row, i);
        return s;
    }
}
