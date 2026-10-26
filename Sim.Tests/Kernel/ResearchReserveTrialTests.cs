using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Research;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Kernel;

/// <summary>
/// ADR-051 — THE TRIAL CONFIGURATION on the real pipeline (Director's ruling 2026-10-26: route overflow and the three-turn
/// reserve enabled together, in the trial configuration only): the shipped research.json with tuning.researchOverflow and
/// tuning.researchReserve {3, 1}, the founded seed-42 world with one AI empire, 120 turns. The player issues no order, so its
/// technology lane funds nothing and its reserve saturates at three turns of its pool; the AI's orders are recorded. The run
/// replays from its order log to the same hash at every turn, and a save/load at turn 60 (a non-zero reserve) continues
/// identically.
/// </summary>
public class ResearchReserveTrialTests
{
    private const int Turns = 120;

    internal static SimConfig Trial(SimConfig cfg)
    {
        System.Text.Json.Nodes.JsonObject doc;
        using (var s = Sim.Data.DataFiles.OpenResearch()) doc = System.Text.Json.Nodes.JsonNode.Parse(s)!.AsObject();
        doc["tuning"]!.AsObject()["researchOverflow"] = true;
        doc["tuning"]!.AsObject()["researchReserve"] = new System.Text.Json.Nodes.JsonObject { ["capTurns"] = 3.0, ["drawTurns"] = 1.0 };
        using var goods = Sim.Data.DataFiles.OpenGoods();
        return cfg with { Research = ResearchContentLoader.Load(doc.ToJsonString(), GoodsConfigLoader.Load(goods)) };
    }

    private static TurnExecutor Exec(SimConfig cfg, OrderLog log)
    {
        using var era = Sim.Data.DataFiles.OpenEraPacing();
        using var pipe = Sim.Data.DataFiles.OpenPipeline();
        return new TurnExecutor(EraTableLoader.Load(era), PipelineLoader.Load(pipe, SystemCatalog.All(cfg, TestConfigs.Worldgen())), log);
    }

    [Fact]
    public void TrialConfiguration_ReplaysFromItsOrderLog_AndSaveLoadMidRunContinuesIdentically()
    {
        SimConfig cfg = Trial(TestConfigs.Sim());
        ResearchContent research = cfg.Research!;
        Assert.True(research.Tuning.ResearchOverflow);
        Assert.NotNull(research.Tuning.Reserve);
        WorldgenConfig wg = TestConfigs.Worldgen() with { AiEmpires = 1 };
        var log = new OrderLog();
        TurnExecutor live = Exec(cfg, log);
        WorldState genesis = WorldFounding.Found(wg, cfg, 42);
        WorldState w = genesis;
        var hashes = new string[Turns + 1];
        WorldState? atSixty = null;
        PolityId player = UiPlayerOf(genesis);
        for (int t = 1; t <= Turns; t++)
        {
            AiOrders.Append(log, w, cfg, TestConfigs.Worldgen());
            WorldState prev = w;
            w = live.Step(w);
            hashes[t] = WorldHash.ComputeHex(w);
            Assert.Null(ResearchQuery.ReservesInvariantViolation(w.ResearchReserves));
            Assert.Null(ResearchQuery.CivicsPointsInvariantViolation(w.CivicsPoints));
            // The idle player: each step deposits ReserveDepositMilli of PREV (its pool, up to its cap) — never more.
            Assert.Equal(ResearchQuery.ReserveMilli(prev, player) + ResearchQuery.ReserveDepositMilli(prev, research, player),
                ResearchQuery.ReserveMilli(w, player));
            Assert.True(ResearchQuery.ReserveMilli(w, player) <= Math.Max(ResearchQuery.ReserveMilli(prev, player),
                ResearchQuery.ReserveCapMilli(prev, research, player)), "the reserve passed its cap");
            if (t == 60) atSixty = w;
        }
        long reserve = ResearchQuery.ReserveMilli(w, player);
        Assert.True(reserve > 0L, "the idle player deposited nothing");
        Assert.True(ResearchQuery.ReserveMilli(atSixty!, player) > 0L);

        // Replay: a fresh executor from the same genesis and the recorded log (no new orders) — equal at every turn.
        TurnExecutor replay = Exec(cfg, log);
        WorldState r = WorldFounding.Found(wg, cfg, 42);
        for (int t = 1; t <= Turns; t++)
        {
            r = replay.Step(r);
            Assert.Equal(hashes[t], WorldHash.ComputeHex(r));
        }

        // Save/load at turn 60 with a non-zero reserve; the loaded world continues to the same turn-120 hash.
        using var ms = new MemoryStream();
        Snapshot.Save(atSixty!, ms);
        WorldState loaded = Snapshot.Load(new MemoryStream(ms.ToArray()), atSixty!.Terrain);
        Assert.True(WorldStates.StateEquals(atSixty!, loaded));
        TurnExecutor resumed = Exec(cfg, log);
        for (int t = 61; t <= Turns; t++) loaded = resumed.Step(loaded);
        Assert.Equal(hashes[Turns], WorldHash.ComputeHex(loaded));
    }

    private static PolityId UiPlayerOf(WorldState w)
    {
        for (int i = 0; i < w.Polities.Count; i++)
            if (w.Polities[i].Source == CommandSource.Player) return w.Polities[i].Id;
        throw new InvalidOperationException("no player polity");
    }
}
