using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

/// <summary>
/// M5 polish, food audit P4 — ColonizationSystem's frontier cache is keyed on the terrain it was built from.
/// The cache is a pure function of static terrain, so ONE pipeline instance used for two worlds (a forecast
/// or a tool reusing a pipeline across seeds) must site the second world's colony on the second world's map.
/// Before the key, the frontier built from the first world's terrain was reused and the second world's colony
/// was mis-sited (measured by the audit: 3/3 reuses). Each world here is stepped to the turn BEFORE its first
/// colony is founded; the founding step through a pipeline that has already stepped the other world must equal
/// the founding step through a fresh pipeline, hash for hash.
/// </summary>
public class ColonizationFrontierKeyTests
{
    private static SystemRegistration[] Pipeline()
    {
        using var pipeStream = Sim.Data.DataFiles.OpenPipeline();
        return PipelineLoader.Load(pipeStream, SystemCatalog.All(TestConfigs.Sim(), TestConfigs.Worldgen()));
    }

    private static EraTable Era()
    {
        using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
        return EraTableLoader.Load(eraStream);
    }

    /// <summary>The world one step before its first colony: single-settlement start, stepped until the next
    /// step would add a settlement.</summary>
    private static WorldState EveOfFirstColony(ulong seed, int maxTurns)
    {
        var exec = new TurnExecutor(Era(), Pipeline());
        WorldState w = WorldFounding.Found(TestConfigs.Worldgen(), TestConfigs.Sim(), seed, 1);
        for (int t = 0; t < maxTurns; t++)
        {
            WorldState next = exec.Step(w);
            if (next.Settlements.Count > w.Settlements.Count) return w;
            w = next;
        }
        throw new InvalidOperationException($"seed {seed}: no colony in {maxTurns} turns — rig vacuous");
    }

    [Fact]
    public void OnePipelineSteppedOverTwoSeedsFoundingTurns_EqualsFreshPipelines()
    {
        WorldState a = EveOfFirstColony(2UL, 120);
        WorldState b = EveOfFirstColony(5UL, 120);
        Assert.NotSame(a.Terrain, b.Terrain);

        EraTable era = Era();
        WorldState freshA = new TurnExecutor(era, Pipeline()).Step(a);
        WorldState freshB = new TurnExecutor(era, Pipeline()).Step(b);
        Assert.Equal(a.Settlements.Count + 1, freshA.Settlements.Count);   // both steps found a colony
        Assert.Equal(b.Settlements.Count + 1, freshB.Settlements.Count);

        // One pipeline instance, both orders: A then B, and B then A.
        var shared = new TurnExecutor(era, Pipeline());
        WorldState sharedA = shared.Step(a);
        WorldState sharedB = shared.Step(b);
        Assert.Equal(WorldHash.ComputeHex(freshA), WorldHash.ComputeHex(sharedA));
        Assert.Equal(WorldHash.ComputeHex(freshB), WorldHash.ComputeHex(sharedB));
        Assert.Equal(freshB.Settlements[^1].SiteCell, sharedB.Settlements[^1].SiteCell);

        var shared2 = new TurnExecutor(era, Pipeline());
        Assert.Equal(WorldHash.ComputeHex(freshB), WorldHash.ComputeHex(shared2.Step(b)));
        Assert.Equal(WorldHash.ComputeHex(freshA), WorldHash.ComputeHex(shared2.Step(a)));
    }
}
