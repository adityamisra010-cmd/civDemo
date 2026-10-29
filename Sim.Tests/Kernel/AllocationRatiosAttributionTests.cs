using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Kernel;

/// <summary>
/// D-054 WS2 (2026-10-14) — THE ATTRIBUTION CONTROL for the building/roads material split (sim.json
/// construction.allocationRatios; TestConfigs.PreAllocationRatios). The split acts only through an Empire's
/// ResourceAllocationRow, written only by a SetResourceAllocation order (kind 12), and an Empire with no row is at b = 100,
/// which ConstructionSystem skips (G_b = min(D_b, S) cannot bind). The AI never issues kind 12. So a world in which no
/// Empire sets a split is byte-identical with the switch on and off on EVERY turn — including turns on which the AI orders
/// road works and builds, the two uses the split governs — and no golden moves for WS2. (What moves the batch-2 pins is
/// WS1 and the v33 layout: IncrementalConstructionAttributionTests.)
/// </summary>
public class AllocationRatiosAttributionTests
{
    private static TurnExecutor Production(SimConfig cfg, OrderLog orders, WorldgenConfig wg)
    {
        using var era = Sim.Data.DataFiles.OpenEraPacing();
        using var pipe = Sim.Data.DataFiles.OpenPipeline();
        return new TurnExecutor(EraTableLoader.Load(era), PipelineLoader.Load(pipe, SystemCatalog.All(cfg, wg)), orders);
    }

    [Fact]
    public void WithNoSplitOrder_TheAiWorldIsByteIdenticalWithTheSwitchOnAndOff_OnEveryTurn()
    {
        // ADR-055/056 (the R4 rule): PreAllocationRatios is an older twin — it strips the newer military layer and switches the
        // economy packets off — so the ON arm is the military-off, pre-packet world too.
        SimConfig on = TestConfigs.PreEconomyTrack(TestConfigs.PreMilitary(TestConfigs.Sim())), off = TestConfigs.PreAllocationRatios(on);
        Assert.True(ResourceAllocation.Enabled(on));
        Assert.False(ResourceAllocation.Enabled(off));
        WorldgenConfig wg = TestConfigs.DevWorldgen() with { AiEmpires = 1 };
        var onLog = new OrderLog();
        var offLog = new OrderLog();
        TurnExecutor exOn = Production(on, onLog, wg), exOff = Production(off, offLog, wg);
        WorldState a = WorldFounding.Found(wg, on, 42), b = WorldFounding.Found(wg, off, 42);
        // ADR-046 §6 (2026-10-23, MEASURED): the supporting-milestone goal moves the dev world's first road order 284 -> 308,
        // past the old 300-turn horizon of this premise; 330 turns keep "the AI orders road works" true.
        for (int t = 1; t <= 330; t++)
        {
            AiOrders.Append(onLog, a, on, wg);
            AiOrders.Append(offLog, b, off, wg);
            a = exOn.Step(a);
            b = exOff.Step(b);
            Assert.Equal(0, a.ResourceAllocations.Count);
            Assert.Equal(WorldHash.ComputeHex(b), WorldHash.ComputeHex(a));
        }
        int roads = 0, builds = 0;
        Assert.Equal(offLog.Count, onLog.Count);
        for (int i = 0; i < onLog.Count; i++)
        {
            Assert.Equal(offLog[i], onLog[i]);
            Assert.NotEqual(OrderKind.SetResourceAllocation, onLog[i].Kind);   // the AI never sets a split
            if (onLog[i].Kind == OrderKind.DevelopRoads) roads++;
            if (onLog[i].Kind == OrderKind.EnqueueConstruction) builds++;
        }
        Assert.True(roads > 0 && builds > 0, $"anti-vacuity: the AI must order road works ({roads}) and building ({builds})");
        Assert.True(a.RoadDevelopments.Count > 0, "anti-vacuity: some road works were paid");
    }
}
