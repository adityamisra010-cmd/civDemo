using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;

namespace Sim.Tests.Kernel;

/// <summary>
/// M5 hardening H4 (2026-10-05) — `sim run` DRIVES THE AI POLITY A REVOLT FOUNDS, AS THE UI DOES.
///
/// THE DEFECT (measured on 9bb7423). ADR-033 D5 runs the AI order producer "headless as in the UI". The UI calls
/// <see cref="AiOrders.Append"/> on EVERY end-turn (UiSession.EndTurn), so any AI polity in the world acts —
/// including the one each revolt founds mid-game (R3 / D-048). `sim run` decided ONCE, at founding, whether to run
/// the producer (`AiOrders.HasAiPolity(world)` on the turn-0 world), a gate written at D5 before revolts created
/// polities. So without --ai-empires the CLI never drove a revolted settlement's polity: canonical seed 42 with the
/// FoundedHarness labour orders revolts settlement 0 at turn 58, and the CLI's run log then held the player's six
/// orders and nothing else, while a UI session with the same clicks gives the new polity a research target at once.
/// Same seed, same player orders, different world.
///
/// THE PIN. The CLI is invoked in-process through its real entry point with real arguments; its emitted run log must
/// carry orders from the revolt-founded polity, its per-turn hashes must equal an in-process replica of the UI's
/// end-turn loop (player orders of the turn, then AiOrders.Append, then step), and `sim replay` of the emitted run log
/// must reproduce every hash.
/// </summary>
public class CliRevoltPolityProducerTests
{
    private const ulong Seed = 42;
    // The FoundedHarness revolt turn (FoundedHarnessTests.RevoltTurn: 58 at H4; ADR-035 moved it 58 -> 63 -> 97 -> 39),
    // plus a few turns of the new polity's own orders.
    private const int Turns = FoundedHarnessTests.RevoltTurn + 6;

    /// <summary>FoundedHarnessTests.SessionLog: labour swings on settlement 0, which starve it unfed and unhoused into
    /// revolt at FoundedHarnessTests.RevoltTurn on this world.</summary>
    private static OrderLog PlayerLog()
    {
        var log = new OrderLog();
        double[] pcts = FoundedHarnessTests.SessionPcts;   // ADR-035 §7: the re-rigged swing set, shared
        for (int i = 0; i < pcts.Length; i++)
            log.Append(new OrderRecord(3 + i * 30, ActorId: 1, OrderKind.LaborAllocation, 0, pcts[i]));
        return log;
    }

    private static int Cli(params string[] args)
    {
        var entry = typeof(Sim.Cli.CliRecipes).Assembly.EntryPoint
            ?? throw new InvalidOperationException("Sim.Cli has no entry point");
        return (int)entry.Invoke(null, [args])!;
    }

    private static SimConfig CliConfig()
    {
        using var sim = Sim.Data.DataFiles.OpenSim();
        using var needs = Sim.Data.DataFiles.OpenNeeds();
        using var goods = Sim.Data.DataFiles.OpenGoods();
        using var research = Sim.Data.DataFiles.OpenResearch();
        using var ages = Sim.Data.DataFiles.OpenAges();
        using var families = Sim.Data.DataFiles.OpenUnitFamilies();
        return SimConfigLoader.Load(sim, needs, goods, research, ages, families);
    }

    [Fact]
    public void SimRun_WithoutAiEmpires_DrivesTheRevoltFoundedPolity_ExactlyAsTheUiLoop_AndTheRunLogReplays()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"h4-cli-revolt-{Environment.ProcessId}");
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        Directory.CreateDirectory(dir);
        string ordersPath = Path.Combine(dir, "player.bin");
        using (FileStream f = File.Create(ordersPath)) PlayerLog().Save(f);
        string runHashes = Path.Combine(dir, "run.log"), replayHashes = Path.Combine(dir, "replay.log");
        string session = Path.Combine(dir, "session");

        Assert.Equal(0, Cli("run", "--founded", "--seed", "42", "--turns", Turns.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--orders", ordersPath, "--hash-log", runHashes, "--emit-session", session));
        string runLogPath = Directory.GetFiles(session, "orders-*.bin").Single();
        OrderLog runLog;
        using (FileStream f = File.OpenRead(runLogPath)) runLog = OrderLog.Load(f);

        // 1. SEMANTIC: the revolt-founded polity (not the player, polity 1) issued orders, from the revolt on.
        int foreign = 0, firstForeignTurn = -1;
        for (int i = 0; i < runLog.Count; i++)
            if (runLog[i].ActorId != 1) { foreign++; if (firstForeignTurn < 0) firstForeignTurn = (int)runLog[i].Turn; }
        Assert.True(foreign > 0, "the CLI run log carries no order from the polity the revolt founded — the producer never ran");
        // ADR-035 RE-PIN (2026-10-07; MEASURED): 58 -> 63 (P-F1) -> 97 (P-F0) -> 39 (P-F2, re-rigged swing set), read
        // from the FoundedHarness pin.
        Assert.Equal(FoundedHarnessTests.RevoltTurn, firstForeignTurn);   // the new polity's first order is stamped on the revolt turn

        // 2. EQUIVALENCE: the UI's end-turn loop, replicated in-process (the player's orders of the turn, then the
        //    AI producer on the same world, then one step) gives the same world on every turn.
        SimConfig cfg = CliConfig();
        OrderLog player = PlayerLog();
        var uiLog = new OrderLog();
        var executor = new TurnExecutor(Sim.Cli.CliRecipes.Era(), Sim.Cli.CliRecipes.ProductionPipeline(), uiLog);
        WorldState world = Sim.Cli.HeadlessFounding.Found(Seed);
        string[] cliHashes = File.ReadAllLines(runHashes);
        Assert.Equal(Turns, cliHashes.Length);
        bool revolted = false;
        for (int t = 1; t <= Turns; t++)
        {
            OrderBatch batch = player.BatchFor(world.Clock.Turn);
            for (int b = 0; b < batch.Count; b++) uiLog.Append(batch[b]);
            AiOrders.Append(uiLog, world, cfg);
            world = executor.Step(world);
            Assert.Equal(WorldHash.ComputeHex(world), cliHashes[t - 1]);
            if (EmpireQuery.TryGetController(world, new SettlementId(0), out PolityId c) && c.Value != 1) revolted = true;
        }
        Assert.True(revolted, "settlement 0 never revolted — the rig no longer reaches a mid-run AI polity (vacuous)");

        // 3. REPLAY: the emitted run log reproduces every hash with no producer (sim replay never produces orders).
        Assert.Equal(0, Cli("replay", "--founded", "--seed", "42", "--turns", Turns.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--orders", runLogPath, "--hash-log", replayHashes));
        Assert.Equal(cliHashes, File.ReadAllLines(replayHashes));

        Directory.Delete(dir, recursive: true);
    }
}
