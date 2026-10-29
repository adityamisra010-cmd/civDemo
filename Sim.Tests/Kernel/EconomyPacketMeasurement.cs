using System.Globalization;
using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;
using Xunit;
using Xunit.Abstractions;

namespace Sim.Tests.Kernel;

/// <summary>
/// MEASUREMENT (ADR-056, the M7 economy packets) — each packet against its switch-off twin on the canonical AI world
/// (`sim run --founded --ai-empires 2`'s loop: AiOrders before every step), seeds 1, 2, 42 and 7. Two arms:
/// NATURAL (the world as it researches; through year ~0 no AI reaches a packet's gate, so the packet must move nothing
/// but layout) and GRANTED (the packet's gate closures completed for every holder at founding, in BOTH arms, so the
/// delta is the packet's content alone). Reports population, food, prices, trade and the packet's goods. Not a pin; the
/// numbers are recorded in docs/adr/adr-056-economy-packets.md. Skipped by default (minutes per seed).
/// </summary>
public class EconomyPacketMeasurement(ITestOutputHelper output)
{
    private static readonly ulong[] Seeds = [1, 2, 42, 7];

    [Theory(Skip = "measurement — run explicitly; results in docs/adr/adr-056-economy-packets.md")]
    [InlineData("ec1", 400, "")]
    [InlineData("ec1", 150, "iron_bloomery,steel_carburized,iron_farm_tools,coke,bessemer")]
    [InlineData("ec2", 150, "horse_domestication")]
    [InlineData("ec3", 150, "saltpetre_refining,dynamite")]
    public void PacketAgainstItsTwin(string packet, int turns, string grant)
    {
        SimConfig on = TestConfigs.Sim();
        SimConfig off = packet == "all" ? TestConfigs.PreEconomyTrack(on) : Sim.Core.State.EconomyKnowledge.WithGoods(on, on.Goods!.WithoutPacket(packet));
        WorldgenConfig wg = TestConfigs.Worldgen() with { AiEmpires = 2 };
        string[] nodes = grant.Length == 0 ? [] : grant.Split(',');
        foreach (ulong seed in Seeds)
        {
            (long Pop, long Harvest, long Trade, string Hash) a = Run(on, wg, seed, turns, nodes);
            (long Pop, long Harvest, long Trade, string Hash) b = Run(off, wg, seed, turns, nodes);
            output.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"{packet} seed {seed} turns {turns} grant [{grant}]: pop {a.Pop} vs {b.Pop}; harvest {a.Harvest} vs {b.Harvest}; " +
                $"trade {a.Trade} vs {b.Trade}; hash {a.Hash[..16]} vs {b.Hash[..16]}"));
        }
    }

    private static (long Pop, long Harvest, long Trade, string Hash) Run(SimConfig cfg, WorldgenConfig wg, ulong seed, int turns, string[] grant)
    {
        WorldState w = WorldFounding.Found(wg, cfg, seed);
        if (grant.Length > 0) EconomyRigs.Know(w, cfg, null, grant);
        var log = new OrderLog();
        using var era = Sim.Data.DataFiles.OpenEraPacing();
        using var pipe = Sim.Data.DataFiles.OpenPipeline();
        var exec = new TurnExecutor(EraTableLoader.Load(era), PipelineLoader.Load(pipe, SystemCatalog.All(cfg, wg)), log);
        long trade = 0;
        for (int t = 1; t <= turns; t++)
        {
            AiOrders.Append(log, w, cfg, wg);
            w = exec.Step(w);
            for (int i = 0; i < w.TradeFlows.Count; i++) trade += w.TradeFlows[i].Quantity;
        }
        long pop = 0;
        for (int i = 0; i < w.Buckets.Count; i++) pop += w.Buckets[i].Count.Value;
        return (pop, EconomyRigs.Flow(w, cfg, "grain", ReasonIds.Harvest, sunk: false), trade, WorldHash.ComputeHex(w));
    }
}
