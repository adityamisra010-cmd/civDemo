using System.Globalization;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Ui.ViewModel;
using Xunit;

namespace Sim.Ui.Tests;

// T3.9a item 3 acceptance: the per-class needs panel — real values for the
// three bound needs, honest labels for the other five, and PER-CLASS rows so
// the D-018 claim (classes want measurably different things) is visible.
public class NeedsPanelModelTests
{
    private static NeedsConfig Needs()
    {
        using var stream = global::Sim.Data.DataFiles.OpenNeeds();
        return NeedsConfigLoader.Load(stream);
    }

    [Fact]
    public void Blocks_FoundedAndStepped_BoundNeedsMirrorTheTable_UnboundHonest()
    {
        SimConfig cfg = MarketPanelModelTests.SimCfg();
        WorldState world = MarketPanelModelTests.SteppedWorld(cfg, 3);
        int settlement = world.Settlements[0].Id.Value;
        NeedsConfig needs = Needs();

        IReadOnlyList<NeedsClassBlock> blocks =
            NeedsPanelModel.Blocks(world, settlement, needs, cfg.Registries.Classes);
        Assert.Equal(cfg.Registries.Classes.Length, blocks.Count);

        foreach (NeedsClassBlock block in blocks)
        {
            ClassEntry cls = cfg.Registries.Classes.Single(c => c.Name == block.ClassName);
            if (!block.Present)
            {
                Assert.Empty(block.NeedLines);
                Assert.Contains("none present", block.HeaderLine);
                continue;
            }
            Assert.Equal(needs.Needs.Length, block.NeedLines.Count); // all eight, always
            for (int n = 0; n < needs.Needs.Length; n++)
            {
                NeedEntry need = needs.Needs[n];
                string line = block.NeedLines[n];
                if (!need.Bound)
                {
                    Assert.Equal($"  {need.Name}: not yet simulated", line);
                    continue;
                }
                // Bound: the line mirrors the published row exactly — a number
                // iff a NeedSatisfactionRow exists for (settlement, class, need).
                double? value = null;
                for (int i = 0; i < world.NeedSatisfactions.Count; i++)
                    if (world.NeedSatisfactions[i].Settlement.Value == settlement
                        && world.NeedSatisfactions[i].Class.Value == cls.Id
                        && world.NeedSatisfactions[i].NeedId == need.Id)
                    { value = world.NeedSatisfactions[i].Value; break; }
                Assert.Equal(value is { } v
                    ? string.Create(CultureInfo.InvariantCulture, $"  {need.Name}: {v:F2}")
                    : $"  {need.Name}: not yet measured", line);
            }
        }

        // The gate's substance: the peasant block shows a REAL number for
        // Sustenance (a bound need with a nonempty peasant basket), not a label.
        NeedsClassBlock peasants = blocks.Single(b => b.ClassName == "Peasants");
        Assert.True(peasants.Present);
        Assert.Matches(@"^  Sustenance: \d\.\d\d$", peasants.NeedLines[0]);
    }

    [Fact]
    public void Blocks_ClassesWithDifferentSatisfactions_ShowDifferentNumbers()
    {
        // Hand-built: peasants at 0.90, artisans at 0.40 on the same need —
        // the panel must show the divergence, per class, verbatim.
        SimConfig cfg = MarketPanelModelTests.SimCfg();
        NeedsConfig needs = Needs();
        var world = new WorldState(7) { Clock = new SimClock(2, 7200, 3600) };
        var sid = new SettlementId(0);
        world.Settlements.Add(new SettlementRow(sid, SiteCell: 0, FoundedTurn: 0));
        var ledger = new Ledger(world.LedgerFlows);
        foreach (int classId in new[] { 1, 2 })
        {
            int row = world.Buckets.Add(new BucketRow(
                sid, new CultureId(1), new ReligionId(1), new ClassId(classId),
                5, Conserved.Zero, 0.0, 0.0, 0.0, 0.0));
            ledger.Flow(ref world.Buckets.Ref(row).Count, ConservedQuantityIds.Population,
                ReasonIds.InitialEndowment, 100, FlowDirection.Source, OverdrawPolicy.Throw);
        }
        world.NeedSatisfactions.Add(new NeedSatisfactionRow(sid, new ClassId(1), 1, 0.90));
        world.NeedSatisfactions.Add(new NeedSatisfactionRow(sid, new ClassId(2), 1, 0.40));

        IReadOnlyList<NeedsClassBlock> blocks =
            NeedsPanelModel.Blocks(world, 0, needs, cfg.Registries.Classes);
        Assert.Equal("  Sustenance: 0.90",
            blocks.Single(b => b.ClassName == "Peasants").NeedLines[0]);
        Assert.Equal("  Sustenance: 0.40",
            blocks.Single(b => b.ClassName == "Artisans").NeedLines[0]);
    }

    [Fact]
    public void Blocks_EmptyClass_ShowsNonePresent_NeverNumbers()
    {
        // §7.4 guard: a class with zero population gets no satisfaction rows
        // from the sim, and a satisfaction for nobody is per-capita-
        // meaningless (T2.13). Proven red by removing the class-pop guard —
        // see commit message.
        SimConfig cfg = MarketPanelModelTests.SimCfg();
        NeedsConfig needs = Needs();
        var world = new WorldState(7) { Clock = new SimClock(2, 7200, 3600) };
        var sid = new SettlementId(0);
        world.Settlements.Add(new SettlementRow(sid, SiteCell: 0, FoundedTurn: 0));
        var ledger = new Ledger(world.LedgerFlows);
        int row = world.Buckets.Add(new BucketRow(
            sid, new CultureId(1), new ReligionId(1), new ClassId(1),
            5, Conserved.Zero, 0.0, 0.0, 0.0, 0.0));
        ledger.Flow(ref world.Buckets.Ref(row).Count, ConservedQuantityIds.Population,
            ReasonIds.InitialEndowment, 50, FlowDirection.Source, OverdrawPolicy.Throw);
        // A STALE artisan row with zero artisans (the exact T2.13-shaped hazard):
        world.NeedSatisfactions.Add(new NeedSatisfactionRow(sid, new ClassId(2), 1, 0.99));

        IReadOnlyList<NeedsClassBlock> blocks =
            NeedsPanelModel.Blocks(world, 0, needs, cfg.Registries.Classes);
        NeedsClassBlock artisans = blocks.Single(b => b.ClassName == "Artisans");
        Assert.False(artisans.Present);
        Assert.Empty(artisans.NeedLines);
        Assert.Equal("Artisans — none present", artisans.HeaderLine);
    }

    [Fact]
    public void Blocks_BeforeFirstTurn_BoundNeedsReadNotYetMeasured()
    {
        SimConfig cfg = MarketPanelModelTests.SimCfg();
        WorldState world = WorldFounding.Found(MarketPanelModelTests.DevCfg(), cfg, 42);
        Assert.Equal(0, world.NeedSatisfactions.Count);
        IReadOnlyList<NeedsClassBlock> blocks = NeedsPanelModel.Blocks(
            world, world.Settlements[0].Id.Value, Needs(), cfg.Registries.Classes);
        NeedsClassBlock peasants = blocks.Single(b => b.ClassName == "Peasants");
        Assert.True(peasants.Present);
        Assert.Equal("  Sustenance: not yet measured", peasants.NeedLines[0]);
    }
}
