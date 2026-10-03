using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.Observability;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Kernel;

/// <summary>
/// M5-integration stream V, item 1 — orders for a settlement FOUNDED MID-GAME.
///
/// OrderValidation.ValidateAgainstWorld is the up-front pass every replay path runs
/// (`sim run --orders`, `sim replay`, `sim inspect`) against the TURN-0 world. A colony
/// does not exist there, so before this fix a labour or construction order the live
/// step applied to a colony made the replay of the very same log throw. Reproduced
/// here with the stranded-source rig (ObservedWorlds.FoundingRun): settlement 12 is
/// founded on the step from turn 1 to turn 2.
///
/// The fix defers the world-dependent checks — existence AND control — for a settlement
/// that is ABSENT from the validated world, carries an id ABOVE every id it holds (the
/// only ids colonization can allocate: ColonizationSystem takes maxId + 1), and is
/// targeted by an order delivered after the first step (Turn ≥ 1 — the batch for turn t
/// is delivered on the step from t, so a Turn-0 order sees exactly the validated world).
/// The consumers already apply the same predicates on PREV at delivery
/// (LabourActivities.CanAllocate, ConstructionQuery.IsProjectAvailable), so a deferred
/// order for a settlement that never appears changes nothing. Every settlement that
/// exists at turn 0 is validated exactly as before.
/// </summary>
public sealed class ColonyOrderValidationTests
{
    private const int Colony = 12;
    private const int Granary = 1;

    /// <summary>The ObservedWorlds.FoundingRun rig, unstepped: warmed to turn 1, every
    /// settlement but 0 stranded, 0 in deficit. The next step founds settlement 12.</summary>
    private static WorldState Rig()
    {
        SimConfig cfg = TestConfigs.Sim();
        WorldState w = ObservedWorlds.Founded(1UL);
        using (var eraStream = Sim.Data.DataFiles.OpenEraPacing())
            w = new TurnExecutor(EraTableLoader.Load(eraStream), [SystemCatalog.Catchment(cfg)]).Step(w);
        SettlementId src = w.Settlements[0].Id;
        for (int s = 0; s < w.Settlements.Count; s++)
        {
            if (w.Settlements[s].Id == src) continue;
            for (int i = 0; i < w.GoodStocks.Count; i++)
                if (w.GoodStocks[i].Settlement == w.Settlements[s].Id)
                    w.GoodStocks[i] = w.GoodStocks[i] with { Amount = Conserved.Zero, LastProducedUnits = 0 };
        }
        w.ConsumptionDeficits.Add(new ConsumptionDeficitRow(src, 0.40, 1000));
        return w;
    }

    private static PolityId RulerOf(IReadOnlyWorldState w, int settlement)
    {
        for (int i = 0; i < w.Controls.Count; i++)
            if (w.Controls[i].Place.Value == settlement) return w.Controls[i].Polity;
        throw new InvalidOperationException($"no controller for settlement {settlement}");
    }

    private static int MaxId(IReadOnlyWorldState w)
    {
        int max = -1;
        for (int i = 0; i < w.Settlements.Count; i++) max = Math.Max(max, w.Settlements[i].Id.Value);
        return max;
    }

    private static bool Exists(IReadOnlyWorldState w, int id)
    {
        for (int i = 0; i < w.Settlements.Count; i++) if (w.Settlements[i].Id.Value == id) return true;
        return false;
    }

    /// <summary>Orders for the colony delivered on the step from turn 2 (the colony
    /// exists in that step's PREV): a labour mix, one sector weight, and a granary.</summary>
    private static OrderLog ColonyOrders(PolityId ruler)
    {
        var log = new OrderLog();
        log.Append(OrderRecord.From(2, ruler, OrderKind.LaborAllocation, Colony, 30.0));
        log.Append(OrderRecord.From(2, ruler, OrderKind.SectorAllocation, (Colony << 3) | 2, 40.0));
        log.Append(OrderRecord.From(2, ruler, OrderKind.EnqueueConstruction, Colony, Granary));
        return log;
    }

    [Fact]
    public void ColonyOrders_AppliedLive_AndTheValidatorAcceptsTheSameLog()
    {
        WorldState start = Rig();
        Assert.Equal(11, MaxId(start));
        Assert.False(Exists(start, Colony));

        // The colony is ruled by its parent's ruler (ColonizationSystem's ControlRow).
        WorldState probe = ObservedWorlds.Executor(TestConfigs.Sim(), null).Step(Rig());
        Assert.True(Exists(probe, Colony), "the rig founded nothing — the reproduction is void");
        PolityId ruler = RulerOf(probe, Colony);
        OrderLog orders = ColonyOrders(ruler);

        // THE REPORTED HAZARD: this used to throw ("does not control" for the
        // construction order, checked first) although the live step applies the log.
        OrderValidation.ValidateAgainstWorld(orders, start);

        // Live: turn 1 -> 2 founds the colony, turn 2 -> 3 delivers the orders.
        TurnExecutor exec = ObservedWorlds.Executor(TestConfigs.Sim(), orders);
        WorldState w = exec.Step(start);
        Assert.Equal(2, w.Clock.Turn);
        Assert.True(Exists(w, Colony));
        for (int i = 0; i < w.SectorAllocations.Count; i++)
            Assert.False(w.SectorAllocations[i].Settlement.Value == Colony && w.SectorAllocations[i].Farming == 0.30);
        w = exec.Step(w);

        bool allocated = false;
        for (int i = 0; i < w.SectorAllocations.Count; i++)
        {
            SectorAllocationRow r = w.SectorAllocations[i];
            if (r.Settlement.Value != Colony) continue;
            allocated = true;
            Assert.Equal(0.30, r.Farming);       // the LaborAllocation, then...
            Assert.Equal(0.40, r.Extraction);    // ...sector 2 overwritten by the SectorAllocation
        }
        Assert.True(allocated, "the colony's labour orders were not applied live");

        bool queuedOrBuilt = false;
        for (int i = 0; i < w.ConstructionQueue.Count; i++)
            if (w.ConstructionQueue[i].Settlement.Value == Colony) queuedOrBuilt = true;
        for (int i = 0; i < w.ConstructionLabor.Count; i++)
            if (w.ConstructionLabor[i].Settlement.Value == Colony) queuedOrBuilt = true;
        Assert.True(queuedOrBuilt, "the colony's granary order was not applied live");
    }

    [Fact]
    public void TurnZeroSettlements_StillValidatedExactly()
    {
        WorldState start = Rig();
        PolityId ruler = RulerOf(start, 0);

        // A missing id BELOW the turn-0 maximum can never be founded: rejected.
        const int gone = 5;
        var kept = new List<SettlementRow>();
        for (int i = 0; i < start.Settlements.Count; i++)
            if (start.Settlements[i].Id.Value != gone) kept.Add(start.Settlements[i]);
        start.Settlements.Clear();
        foreach (SettlementRow row in kept) start.Settlements.Add(row);
        var ghost = new OrderLog();
        ghost.Append(OrderRecord.From(3, ruler, OrderKind.LaborAllocation, gone, 50.0));
        Assert.Contains("does not exist", Assert.Throws<OrderValidationException>(
            () => OrderValidation.ValidateAgainstWorld(ghost, start)).Message);

        // A settlement that EXISTS at turn 0 but another Empire rules: rejected, on any turn.
        var rival = new PolityId(ruler.Value + 100);
        start.Polities.Add(new PolityRow(rival, CommandSource.Ai));
        foreach (OrderKind kind in new[] { OrderKind.LaborAllocation, OrderKind.EnqueueConstruction })
        {
            var trespass = new OrderLog();
            trespass.Append(OrderRecord.From(7, rival, kind, 0,
                kind == OrderKind.EnqueueConstruction ? Granary : 50.0));
            Assert.Contains("does not control", Assert.Throws<OrderValidationException>(
                () => OrderValidation.ValidateAgainstWorld(trespass, start)).Message);
        }

        // A future id on the FIRST step (Turn 0) sees exactly the validated world: rejected.
        var early = new OrderLog();
        early.Append(OrderRecord.From(0, ruler, OrderKind.LaborAllocation, Colony, 50.0));
        Assert.Throws<OrderValidationException>(() => OrderValidation.ValidateAgainstWorld(early, start));

        // A world with no settlements can never found one: still rejected on any turn.
        var toy = new WorldState(1);
        var toyLog = new OrderLog();
        toyLog.Append(new OrderRecord(5, ActorId: 1, OrderKind.LaborAllocation, TargetId: 0, Amount: 50.0));
        Assert.Throws<OrderValidationException>(() => OrderValidation.ValidateAgainstWorld(toyLog, toy));
    }

    [Fact]
    public void DeferredOrder_ForASettlementThatNeverAppears_ChangesNothingAtDelivery()
    {
        // The deferral's other half: an order for an id no step ever founds is refused
        // by the consumer's own predicate on PREV and the world is identical to the
        // order-free run (hash-equal every turn).
        WorldState start = Rig();
        PolityId ruler = RulerOf(start, 0);
        var log = new OrderLog();
        log.Append(OrderRecord.From(2, ruler, OrderKind.LaborAllocation, 40, 30.0));
        log.Append(OrderRecord.From(2, ruler, OrderKind.EnqueueConstruction, 40, Granary));
        OrderValidation.ValidateAgainstWorld(log, start);

        TurnExecutor withOrders = ObservedWorlds.Executor(TestConfigs.Sim(), log);
        TurnExecutor without = ObservedWorlds.Executor(TestConfigs.Sim(), null);
        WorldState a = start, b = Rig();
        for (int t = 0; t < 3; t++)
        {
            a = withOrders.Step(a);
            b = without.Step(b);
            Assert.Equal(WorldHash.ComputeHex(b), WorldHash.ComputeHex(a));
        }
    }
}
