using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Kernel;

/// <summary>
/// M5 hardening H4 (2026-10-05) — orders from an Empire FOUNDED MID-GAME pass the up-front validation every replay
/// path runs (`sim run --orders`, `sim replay`, `sim inspect`) against the TURN-0 world.
///
/// Every revolt founds a new AI polity at the roster maximum + 1 (RevoltSystem, D-048) and the UI's AI producer drives
/// it from that turn, so a played log carries its orders. Before this fix the turn-0 roster check threw on them
/// ("polity 2 … is not a registered Empire"), so a playtest session with a revolt could not be replayed or inspected.
/// The stream-V colony rule (`7100c77`, ColonyOrderValidationTests) is applied to ACTORS: an actor id ABOVE every id the
/// turn-0 roster holds, delivered after the first step, has its world-dependent checks (existence, control) deferred to
/// delivery, where the consumers apply them on PREV. These pins are semantic; the end-to-end replay of a real revolt
/// log is CliRevoltPolityProducerTests.
/// </summary>
public sealed class RevoltPolityOrderValidationTests
{
    private static (WorldState World, PolityId Player) Dev(int aiEmpires = 0) => GovernanceRigs.Founded(aiEmpires: aiEmpires);

    private static int MaxPolity(IReadOnlyWorldState w)
    {
        int max = int.MinValue;
        for (int i = 0; i < w.Polities.Count; i++) max = Math.Max(max, w.Polities[i].Id.Value);
        return max;
    }

    [Fact]
    public void AnActorAboveTheTurn0Roster_DeliveredAfterTheFirstStep_IsDeferred_EvenForASettlementItDoesNotYetRule()
    {
        (WorldState w, PolityId player) = Dev();
        var future = new PolityId(MaxPolity(w) + 1);
        SettlementId playersPlace = w.Settlements[0].Id;
        Assert.True(EmpireQuery.ControlsSettlement(w, player, playersPlace));

        // What a revolt-founded polity's AI issues for the place it took: a research target, a granary there, an Age
        // advance, a road development and its own tax policy — every order kind AiOrders produces.
        var log = new OrderLog();
        log.Append(OrderRecord.From(58, future, OrderKind.SetResearchTarget, 2, 0.0));
        log.Append(OrderRecord.From(58, future, OrderKind.EnqueueConstruction, playersPlace.Value, 1.0));
        log.Append(OrderRecord.From(59, future, OrderKind.AdvanceAge, 2, 1.0));
        log.Append(OrderRecord.From(60, future, OrderKind.DevelopRoads, 0, 50.0));
        log.Append(OrderRecord.From(61, future, OrderKind.SetTaxRate, future.Value, 5.0));
        log.Append(OrderRecord.From(62, future, OrderKind.SectorAllocation, playersPlace.Value * 8 + Sectors.Farming, 40.0));
        OrderValidation.ValidateAgainstWorld(log, w);   // must not throw
    }

    [Fact]
    public void TheSameActorOnTurn0_IsStillRejected_TheFirstBatchReadsExactlyTheValidatedWorld()
    {
        (WorldState w, _) = Dev();
        var future = new PolityId(MaxPolity(w) + 1);
        var log = new OrderLog();
        log.Append(OrderRecord.From(0, future, OrderKind.SetResearchTarget, 2, 0.0));
        OrderValidationException ex = Assert.Throws<OrderValidationException>(() => OrderValidation.ValidateAgainstWorld(log, w));
        Assert.Contains("not a registered Empire", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ARegisteredActorIsCheckedAsBefore_ItMayNotBuildWhereItDoesNotRule()
    {
        (WorldState w, PolityId player) = Dev(aiEmpires: 1);
        SettlementId rivals = default;
        for (int s = 0; s < w.Settlements.Count; s++)
            if (!EmpireQuery.ControlsSettlement(w, player, w.Settlements[s].Id)) { rivals = w.Settlements[s].Id; break; }
        var log = new OrderLog();
        log.Append(OrderRecord.From(5, player, OrderKind.EnqueueConstruction, rivals.Value, 1.0));
        OrderValidationException ex = Assert.Throws<OrderValidationException>(() => OrderValidation.ValidateAgainstWorld(log, w));
        Assert.Contains("does not control", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AFutureActorStillLegislatesOnlyItsOwnTax_TheWorldIndependentCheckIsNotDeferred()
    {
        (WorldState w, PolityId player) = Dev();
        var future = new PolityId(MaxPolity(w) + 1);
        var log = new OrderLog();
        log.Append(OrderRecord.From(10, future, OrderKind.SetTaxRate, player.Value, 40.0));
        OrderValidationException ex = Assert.Throws<OrderValidationException>(() => OrderValidation.ValidateAgainstWorld(log, w));
        Assert.Contains("legislates its own taxes", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>The deferral is safe only because delivery refuses what validation no longer catches: orders from an
    /// actor that NEVER comes into existence (no revolt) change nothing — the world is hash-identical, turn by turn,
    /// to the same run without them.</summary>
    [Fact]
    public void OrdersFromAnActorThatNeverExists_ChangeNothingAtDelivery()
    {
        SimConfig cfg = TestConfigs.Sim();
        (WorldState start, _) = Dev();
        var ghost = new PolityId(MaxPolity(start) + 1);
        var forged = new OrderLog();
        forged.Append(OrderRecord.From(1, ghost, OrderKind.SetResearchTarget, 2, 0.0));
        forged.Append(OrderRecord.From(1, ghost, OrderKind.EnqueueConstruction, start.Settlements[0].Id.Value, 1.0));
        forged.Append(OrderRecord.From(2, ghost, OrderKind.SectorAllocation, start.Settlements[0].Id.Value * 8 + Sectors.Farming, 5.0));
        forged.Append(OrderRecord.From(2, ghost, OrderKind.DevelopRoads, 0, 100.0));
        forged.Append(OrderRecord.From(3, ghost, OrderKind.SetTaxRate, ghost.Value, 100.0));
        forged.Append(OrderRecord.From(3, ghost, OrderKind.AdvanceAge, 2, 1.0));
        OrderValidation.ValidateAgainstWorld(forged, start);   // accepted up front (the id could still be founded)

        TurnExecutor Executor(OrderLog log)
        {
            using var era = Sim.Data.DataFiles.OpenEraPacing();
            using var pipe = Sim.Data.DataFiles.OpenPipeline();
            return new TurnExecutor(EraTableLoader.Load(era),
                PipelineLoader.Load(pipe, SystemCatalog.All(cfg, TestConfigs.DevWorldgen())), log);
        }
        TurnExecutor withGhost = Executor(forged), clean = Executor(new OrderLog());
        WorldState a = Dev().World, b = Dev().World;
        for (int t = 1; t <= 6; t++)
        {
            a = withGhost.Step(a);
            b = clean.Step(b);
            Assert.Equal(WorldHash.ComputeHex(b), WorldHash.ComputeHex(a));
        }
        Assert.False(EmpireQuery.TryGetCommandSource(a, ghost, out _));   // non-vacuous: it never existed
    }
}
