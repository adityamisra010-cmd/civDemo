using Sim.Core.Kernel;
using Sim.Core.State;
using Xunit;

namespace Sim.Ui.Tests;

// T1.9 adversarial hardening: these tests drive the REAL UI session code —
// UiSession.Start (UiFounding + the UI's executor recipe), EmitSectorOrders (the
// labour control's actual stamping), EndTurn, Save — and replay the produced log
// headlessly. Order-timing drift (Turn+1 stamping), pipeline/era drift in the
// UI executor, and stamped-filename drift all break HERE now, not in a played
// session. ADR-033 D9 / audit E38: the M1 single-slider path these tests used to
// drive (EmitLaborOrder, OrderKind 2) left Sim.Ui, so they drive the LIVE labour
// path — the five-sector batch the action surface emits — and the replay guarantee
// still covers what a player can actually do.
public class UiSessionReplayTests
{
    [Fact]
    public void UiSession_PlayedAndSaved_ReplaysHashForHash()
    {
        // Play 20 turns through the UI seam, emitting orders the way the labour control does.
        var session = Sim.Ui.UiSession.Start(42);
        int capital = session.World.Settlements[0].Id.Value;
        var hashes = new List<string>(20);
        for (int t = 1; t <= 20; t++)
        {
            if (t == 3) Assert.True(session.EmitSectorOrders([40, 20, 15, 15, 10], capital));
            if (t == 11) Assert.True(session.EmitSectorOrders([20, 30, 20, 20, 10], capital));
            session.EndTurn();
            hashes.Add(WorldHash.ComputeHex(session.World));

            // THE DELIVERY SEMANTIC, pinned (adversarial pass follow-up: a
            // Turn+1 stamping mutant survived the pure replay comparison
            // because a shifted stamp shifts live and replay IDENTICALLY —
            // replay fidelity alone cannot see it. What it breaks is the
            // control's promise that an applied split takes effect on the very
            // next End Turn; that promise is asserted here, turn-exactly).
            if (t == 3) Assert.Equal((0.40, 0.20), (session.World.SectorAllocations[0].Farming, session.World.SectorAllocations[0].Herding));
            if (t == 11) Assert.Equal((0.20, 0.30), (session.World.SectorAllocations[0].Farming, session.World.SectorAllocations[0].Herding));
        }

        string logPath = Path.Combine(Path.GetTempPath(), $"orders-ui-replay-{Guid.NewGuid():N}.bin");
        session.Save(logPath);
        try
        {
            OrderLog loaded;
            using (var stream = File.OpenRead(logPath)) loaded = OrderLog.Load(stream);
            Assert.Equal(2 * Sectors.Count, loaded.Count); // both applied splits, nothing else
            for (int i = 0; i < loaded.Count; i++) Assert.Equal(OrderKind.SectorAllocation, loaded[i].Kind);

            // Headless replay through the SAME UI recipes (founding + executor).
            TurnExecutor exec = Sim.Ui.UiSession.BuildProductionExecutor(loaded);
            WorldState world = Sim.Ui.UiFounding.Found(42);
            OrderValidation.ValidateAgainstWorld(loaded, world);
            for (int t = 1; t <= 20; t++)
            {
                world = exec.Step(world);
                Assert.Equal(hashes[t - 1], WorldHash.ComputeHex(world));
            }
            // The orders really steered the sim (anti-vacuity).
            Assert.Equal(0.20, world.SectorAllocations[0].Farming);
            Assert.Equal(0.30, world.SectorAllocations[0].Herding);
        }
        finally
        {
            File.Delete(logPath);
        }
    }

    [Fact]
    public void SessionLogPath_StampedFlat_LexicographicIsChronological()
    {
        var early = new DateTime(2026, 7, 22, 9, 5, 0);
        var late = new DateTime(2026, 7, 22, 10, 0, 0);
        string a = Sim.Ui.UiSession.SessionLogPath(early);
        string b = Sim.Ui.UiSession.SessionLogPath(late);

        Assert.Equal(Path.Combine("runs", "orders-20260722-090500.bin"), a);
        Assert.True(string.CompareOrdinal(a, b) < 0, "lexicographic != chronological");

        // Non-canonical sizes are visible IN the name (replay needs --size PX).
        Assert.Equal(Path.Combine("runs", "orders-20260722-090500-s256.bin"),
            Sim.Ui.UiSession.SessionLogPath(early, sizeOverridePx: 256));
    }

    [Fact]
    public void UiArgs_Defaults_AreCanonical()
    {
        // Replay-fidelity surface: a silently-added default size override would
        // make every played session unreplayable at canonical size.
        (ulong seed, int? size, int? settlements, int? aiEmpires) = Sim.Ui.UiArgs.Parse([]);
        Assert.Equal(42UL, seed);
        Assert.Null(size);
        Assert.Null(settlements); // T2.3: canonical count unless explicitly overridden
        Assert.Null(aiEmpires);   // ADR-033 D5: worldgen.json's count (0) unless explicitly overridden

        (seed, size, settlements, aiEmpires) = Sim.Ui.UiArgs.Parse(
            ["--seed", "7", "--size", "256", "--settlements", "4", "--ai-empires", "2"]);
        Assert.Equal(7UL, seed);
        Assert.Equal(256, size);
        Assert.Equal(4, settlements);
        Assert.Equal(2, aiEmpires);

        // An explicit 0 is recorded as an override (the log name says -a0); a negative or non-numeric count is ignored.
        Assert.Equal(0, Sim.Ui.UiArgs.Parse(["--ai-empires", "0"]).AiEmpiresOverride);
        Assert.Null(Sim.Ui.UiArgs.Parse(["--ai-empires", "-1"]).AiEmpiresOverride);
        Assert.Null(Sim.Ui.UiArgs.Parse(["--ai-empires", "two"]).AiEmpiresOverride);
        Assert.Contains("--ai-empires N", Sim.Ui.UiArgs.Usage);
    }

    [Fact]
    public void TheAiEmpiresOption_FoundsThatManyAiEmpires_AndNamesTheLog()
    {
        (ulong seed, int? size, int? settlements, int? aiEmpires) = Sim.Ui.UiArgs.Parse(
            ["--size", "256", "--settlements", "4", "--ai-empires", "1"]);
        var session = Sim.Ui.UiSession.Start(seed, size, settlements, aiEmpires);
        int ai = 0;
        for (int i = 0; i < session.World.Polities.Count; i++)
            if (session.World.Polities[i].Source == CommandSource.Ai) ai++;
        Assert.Equal(1, ai);
        Assert.Equal(1, session.AiEmpiresOverride);
        Assert.EndsWith("-s256-n4-a1.bin", Sim.Ui.UiSession.SessionLogPath(new DateTime(2026, 10, 2, 12, 0, 0), size, settlements, aiEmpires));
    }

    [Fact]
    public void UiFounding_SizeOverrideBranch_EqualsCanonicalAtThatSize()
    {
        WorldState ui = Sim.Ui.UiFounding.Found(42, sizeOverridePx: 256, settlementsOverride: 4);
        Sim.Core.Worldgen.WorldgenConfig wg;
        using (var s = global::Sim.Data.DataFiles.OpenWorldgen())
            wg = Sim.Core.Worldgen.WorldgenConfigLoader.Load(s);
        // ADR-031: the canonical recipe is the CLI's six-stream load (CliRecipes), which founds
        // the warband formations; the UI now founds with the same recipe.
        Sim.Core.Systems.SimConfig sim;
        using (var s = global::Sim.Data.DataFiles.OpenSim())
        using (var n = global::Sim.Data.DataFiles.OpenNeeds())
        using (var g = global::Sim.Data.DataFiles.OpenGoods())
        using (var r = global::Sim.Data.DataFiles.OpenResearch())
        using (var a = global::Sim.Data.DataFiles.OpenAges())
        using (var f = global::Sim.Data.DataFiles.OpenUnitFamilies())
            sim = Sim.Core.Systems.SimConfigLoader.Load(s, n, g, r, a, f);
        WorldState canonical = Sim.Core.Worldgen.WorldFounding.Found(
            wg with { SizePx = 256 }, sim, 42, settlementsOverride: 4);
        Assert.Equal(WorldHash.ComputeHex(canonical), WorldHash.ComputeHex(ui));
    }
}

// T1.10: build identity — the string the director sees in title AND panel.
public class BuildInfoTests
{
    [Fact]
    public void Describe_LocalBuild_FallsBackToDevLocal()
    {
        // Test builds pass no -p:BuildSha/-p:BuildDate → the documented fallback.
        // MILESTONE LABEL: bumped M2 → M3 at T3.12 (the M3 exit artifact), M3 → M4 at the M4 exit paperwork (b40fad6), and M4 → M5 on
        // 2026-10-05 (M5 hardening, Director §13 — the M5 playtest builds still showed "civ-sim M4"). This
        // assert is deliberately EXACT rather than a prefix/contains match — the
        // director reads this string at every gate to know which build he is
        // holding, so a stale milestone label must fail the suite, not pass it
        // quietly. The label is the CURRENT milestone (docs/milestones.md, the roadmap rebase table): bump it
        // here and in BuildInfo.Milestone together whenever the current milestone changes.
        Assert.Equal("civ-sim M5 (dev, local)", Sim.Ui.BuildInfo.Describe());
        Assert.Equal("M5", Sim.Ui.BuildInfo.Milestone);
        Assert.Equal("dev", Sim.Ui.BuildInfo.Sha);
        Assert.Equal("local", Sim.Ui.BuildInfo.Date);
    }
}
