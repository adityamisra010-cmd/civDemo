using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Kernel;

/// <summary>
/// ADR-034 (F3, M5 hardening) — the DELIVERY half of the order-validation deferral. The up-front pass against the
/// turn-0 world defers an actor above the turn-0 roster (a polity a revolt may found, 9bcad5b) and a settlement target
/// above the turn-0 settlements (a colony, 7100c77), so a forged id such as actor 999 passes it. Every replay runner
/// (`sim run --orders`, `sim replay`, `sim inspect`, `sim research --orders`) now calls
/// <see cref="OrderValidation.ValidateAtDelivery"/> for each turn's batch against PREV before stepping: a deferred id
/// that does not exist when its order is delivered rejects the log with an explicit diagnostic. Revolt and colony
/// replays still pass (CliRevoltPolityProducerTests replays a real revolt log through `sim replay`).
/// </summary>
public sealed class OrderDeliveryValidationTests
{
    private static (WorldState World, PolityId Player) Dev(int aiEmpires = 0) => GovernanceRigs.Founded(aiEmpires: aiEmpires);

    private static int MaxPolity(IReadOnlyWorldState w)
    {
        int max = int.MinValue;
        for (int i = 0; i < w.Polities.Count; i++) max = Math.Max(max, w.Polities[i].Id.Value);
        return max;
    }

    private static int MaxSettlement(IReadOnlyWorldState w)
    {
        int max = -1;
        for (int i = 0; i < w.Settlements.Count; i++) max = Math.Max(max, w.Settlements[i].Id.Value);
        return max;
    }

    private static TurnExecutor Executor(OrderLog log)
    {
        SimConfig cfg = TestConfigs.Sim();
        using var era = Sim.Data.DataFiles.OpenEraPacing();
        using var pipe = Sim.Data.DataFiles.OpenPipeline();
        return new TurnExecutor(EraTableLoader.Load(era),
            PipelineLoader.Load(pipe, SystemCatalog.All(cfg, TestConfigs.DevWorldgen())), log);
    }

    [Fact]
    public void AForgedActor999_PassesTheUpFrontPass_ButIsRejectedWhenItsTurnIsDelivered()
    {
        (WorldState start, _) = Dev();
        var forged = new OrderLog();
        forged.Append(OrderRecord.From(2, new PolityId(999), OrderKind.SetResearchTarget, 2, 0.0));
        OrderValidation.ValidateAgainstWorld(forged, start);   // deferred: the id could still be founded

        TurnExecutor ex = Executor(forged);
        WorldState w = start;
        // Turns 0 and 1 carry nothing from it: delivery passes.
        for (int t = 0; t < 2; t++)
        {
            OrderValidation.ValidateAtDelivery(forged.BatchFor(w.Clock.Turn), start, w);
            w = ex.Step(w);
        }
        Assert.Equal(2, w.Clock.Turn);
        OrderValidationException e = Assert.Throws<OrderValidationException>(
            () => OrderValidation.ValidateAtDelivery(forged.BatchFor(w.Clock.Turn), start, w));
        Assert.Contains("polity 999", e.Message, StringComparison.Ordinal);
        Assert.Contains("not a registered Empire when the order is delivered (turn 2", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ADeferredActorThatExistsAtDelivery_IsAccepted()
    {
        // turn-0 world: the player alone; the delivery world additionally holds the next polity id (as a revolt
        // founds it at roster max + 1) — here an AI Empire registered at that id.
        (WorldState turnZero, _) = Dev();
        (WorldState prev, _) = Dev(aiEmpires: 1);
        int next = MaxPolity(turnZero) + 1;
        Assert.True(EmpireQuery.TryGetCommandSource(prev, new PolityId(next), out _));   // non-vacuous
        var log = new OrderLog();
        log.Append(OrderRecord.From(5, new PolityId(next), OrderKind.SetResearchTarget, 2, 0.0));
        OrderValidation.ValidateAtDelivery(log.BatchFor(5), turnZero, prev);   // must not throw
    }

    [Fact]
    public void ADeferredSettlementTargetThatDoesNotExistAtDelivery_IsRejected_AnExistingOneIsNot()
    {
        (WorldState w, PolityId player) = Dev();
        int ghost = MaxSettlement(w) + 1;
        var log = new OrderLog();
        log.Append(OrderRecord.From(3, player, OrderKind.LaborAllocation, ghost, 40.0));
        OrderValidation.ValidateAgainstWorld(log, w);   // deferred up front (a colony could take that id)
        OrderValidationException e = Assert.Throws<OrderValidationException>(
            () => OrderValidation.ValidateAtDelivery(log.BatchFor(3), w, w));
        Assert.Contains($"settlement {ghost}, which does not exist when the order is delivered", e.Message, StringComparison.Ordinal);

        // A turn-0 settlement is not a deferred target: the delivery check leaves it to the up-front pass.
        var ok = new OrderLog();
        ok.Append(OrderRecord.From(3, player, OrderKind.SectorAllocation, w.Settlements[0].Id.Value * 8 + Sectors.Farming, 40.0));
        OrderValidation.ValidateAtDelivery(ok.BatchFor(3), w, w);
    }

    [Fact]
    public void TheCli_RejectsAReplayOfAForgedActorLog_WithTheDeliveryDiagnostic()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"adr034-cli-{Environment.ProcessId}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var forged = new OrderLog();
            forged.Append(OrderRecord.From(1, new PolityId(999), OrderKind.SetResearchTarget, 2, 0.0));
            string path = Path.Combine(dir, "forged.bin");
            using (FileStream f = File.Create(path)) forged.Save(f);
            var entry = typeof(Sim.Cli.CliRecipes).Assembly.EntryPoint!;
            var err = new StringWriter();
            TextWriter old = Console.Error;
            Console.SetError(err);
            int exit;
            try { exit = (int)entry.Invoke(null, [new[] { "replay", "--founded", "--seed", "42", "--turns", "3", "--orders", path }])!; }
            finally { Console.SetError(old); }
            Assert.NotEqual(0, exit);
            Assert.Contains("polity 999", err.ToString(), StringComparison.Ordinal);
            Assert.Contains("ADR-034", err.ToString(), StringComparison.Ordinal);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}
