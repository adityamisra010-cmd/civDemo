using Sim.Core.Kernel;
using Sim.Core.State;
using Xunit;

namespace Sim.Ui.Tests;

// T1.9 adversarial hardening: these tests drive the REAL UI session code —
// UiSession.Start (UiFounding + the UI's executor recipe), EmitLaborOrder (the
// slider's actual stamping), EndTurn, Save — and replay the produced log
// headlessly. Order-timing drift (Turn+1 stamping), pipeline/era drift in the
// UI executor, and stamped-filename drift all break HERE now, not in a played
// session.
public class UiSessionReplayTests
{
    [Fact]
    public void UiSession_PlayedAndSaved_ReplaysHashForHash()
    {
        // Play 20 turns through the UI seam, emitting orders the way the HUD does.
        var session = Sim.Ui.UiSession.Start(42);
        var hashes = new List<string>(20);
        for (int t = 1; t <= 20; t++)
        {
            if (t == 3) session.EmitLaborOrder(55);
            if (t == 11) session.EmitLaborOrder(20);
            session.EndTurn();
            hashes.Add(WorldHash.ComputeHex(session.World));

            // THE DELIVERY SEMANTIC, pinned (adversarial pass follow-up: a
            // Turn+1 stamping mutant survived the pure replay comparison
            // because a shifted stamp shifts live and replay IDENTICALLY —
            // replay fidelity alone cannot see it. What it breaks is the HUD's
            // promise that a released slider applies on the very next End
            // Turn; that promise is asserted here, turn-exactly).
            if (t == 3) Assert.Equal(0.55, session.World.LaborAllocations[0].FarmShare);
            if (t == 11) Assert.Equal(0.20, session.World.LaborAllocations[0].FarmShare);
        }

        string logPath = Path.Combine(Path.GetTempPath(), $"orders-ui-replay-{Guid.NewGuid():N}.bin");
        session.Save(logPath);
        try
        {
            OrderLog loaded;
            using (var stream = File.OpenRead(logPath)) loaded = OrderLog.Load(stream);
            Assert.Equal(2, loaded.Count); // both slider releases, nothing else

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
            Assert.Equal(0.20, world.LaborAllocations[0].FarmShare);
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
        (ulong seed, int? size) = Sim.Ui.UiArgs.Parse([]);
        Assert.Equal(42UL, seed);
        Assert.Null(size);

        (seed, size) = Sim.Ui.UiArgs.Parse(["--seed", "7", "--size", "256"]);
        Assert.Equal(7UL, seed);
        Assert.Equal(256, size);
    }

    [Fact]
    public void UiFounding_SizeOverrideBranch_EqualsCanonicalAtThatSize()
    {
        WorldState ui = Sim.Ui.UiFounding.Found(42, sizeOverridePx: 256);
        Sim.Core.Worldgen.WorldgenConfig wg;
        using (var s = global::Sim.Data.DataFiles.OpenWorldgen())
            wg = Sim.Core.Worldgen.WorldgenConfigLoader.Load(s);
        Sim.Core.Systems.SimConfig sim;
        using (var s = global::Sim.Data.DataFiles.OpenSim())
            sim = Sim.Core.Systems.SimConfigLoader.Load(s);
        WorldState canonical = Sim.Core.Worldgen.WorldFounding.Found(
            wg with { SizePx = 256 }, sim, 42);
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
        Assert.Equal("civ-sim M1 (dev, local)", Sim.Ui.BuildInfo.Describe());
        Assert.Equal("dev", Sim.Ui.BuildInfo.Sha);
        Assert.Equal("local", Sim.Ui.BuildInfo.Date);
    }
}
