using Sim.Cli;
using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;

namespace Sim.Tests.Kernel;

/// <summary>
/// D-058 (2026-10-19) — THE ci.yml AI-EMPIRE LEG'S CONSTRUCTION PREMISE, PINNED. The leg (gen-sample-orders.py --founded-mix,
/// `sim run --founded --seed 42 --ai-empires 1`) orders granaries at settlement 0 at turn 10 and, at turn 11, a kind-13 cancel
/// of the LAST of them, meant to be a NON-HEAD entry when it is delivered. The leg itself only checks the kinds in the run log
/// and that replay and inspect agree, so when the Director's "build one at a time … 7 built, 1 under construction, 1 not
/// started" ruling let one step build all three granaries the batch-4 log ordered, the cancel silently named nothing (MEASURED:
/// with the three-granary log, world 12 with and without the cancel was the same hash). This test runs the CLI's own founding,
/// configuration, executor and AI producer in process on the generator's orders to turn 12 and pins the premise: at world 11
/// settlement 0 holds the cancel's target as a queued non-head entry with its identity, and world 12 no longer holds it.
/// </summary>
public class MixLegConstructionPremiseTests
{
    /// <summary>The generator's granary count at turn 10 (keep in step with scripts/gen-sample-orders.py --founded-mix).</summary>
    internal const int Granaries = MixGranaries;
    private const int MixGranaries = 12;

    [Fact]
    public void TheTurn11Cancel_NamesAQueuedNonHeadEntry_AndRemovesItInTheNextWorld()
    {
        string script = File.ReadAllText(Path.Combine(AllocationScenario.RepoRoot(), "scripts", "gen-sample-orders.py"));
        Assert.Contains($"MIX_GRANARIES = {Granaries}", script, StringComparison.Ordinal);

        var loaded = new OrderLog();
        foreach (OrderRecord r in Records()) loaded.Append(r);
        SimConfig cfg = AllocationScenario.CliConfig();
        WorldState w = HeadlessFounding.Found(42, aiEmpiresOverride: 1);
        var orders = new OrderLog();
        TurnExecutor exec = AllocationScenario.Executor(cfg, orders);
        var worlds = new List<WorldState> { w };
        for (int t = 1; t <= 12; t++)
        {
            OrderBatch batch = loaded.BatchFor(w.Clock.Turn);
            for (int b = 0; b < batch.Count; b++) orders.Append(batch[b]);
            AiOrders.Append(orders, w, cfg, CliRecipes.Worldgen());
            w = exec.Step(w);
            worlds.Add(w);
        }
        var s0 = new SettlementId(0);
        var target = new ConstructionQueueRow(s0, Granaries - 1, 1);
        ConstructionQueueRow[] q11 = ConstructionQuery.Queue(worlds[11], s0);
        Assert.True(q11.Length >= 2, $"anti-vacuity: settlement 0 holds {q11.Length} entries at world 11");
        Assert.Equal(target, q11[^1]);
        Assert.NotEqual(target, q11[0]);                                                      // a NON-HEAD entry
        Assert.Equal(10L, ConstructionQuery.EnqueuedTurnOf(worlds[11].ConstructionProgress, target));
        Assert.DoesNotContain(target, ConstructionQuery.Queue(worlds[12], s0));
        Assert.True(ConstructionQuery.Built(worlds[11], s0, 1) >= 2, "the step 10 -> 11 built more than one granary (D-058)");
    }

    /// <summary>The generator's --founded-mix records of turns 0..11 that matter to settlement 0's queue.</summary>
    private static IEnumerable<OrderRecord> Records()
    {
        yield return new OrderRecord(0, 1, OrderKind.SetResearchTarget, 2, 0.0);
        yield return new OrderRecord(0, 1, OrderKind.SectorAllocation, (0 << 3) | 4, 10.0);
        for (int i = 0; i < Granaries; i++) yield return new OrderRecord(10, 1, OrderKind.EnqueueConstruction, 0, 1.0);
        yield return new OrderRecord(11, 1, OrderKind.CancelConstructionEntry, 0,
            OrderLog.PackCancelTarget(new CancelTarget(Granaries - 1, 1, 10)));
    }
}
