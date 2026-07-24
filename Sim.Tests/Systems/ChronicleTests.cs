using Sim.Core;
using Sim.Core.Chronicle;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

// T2.9 acceptance: names deterministic with a stated collision policy; every
// event type fires at its DOCUMENTED threshold (two-sided rigs: just-below
// silent, at-threshold fires) carrying turn/year/settlement/magnitudes; prose
// invents no facts (unknown placeholder throws); annals twin-identical across
// identical runs.
public class ChronicleTests
{
    private static ChronicleConfig Config()
    {
        using var stream = Sim.Data.DataFiles.OpenChronicle();
        return ChronicleConfigLoader.Load(stream);
    }

    // --- rig: a hand world the collector reads directly ----------------------

    private static WorldState Rig(long pop = 1000, int settlements = 1)
    {
        var world = new WorldState(7);
        var ledger = new Ledger(world.LedgerFlows);
        for (int s = 0; s < settlements; s++)
        {
            var id = new SettlementId(s);
            world.Settlements.Add(new SettlementRow(id, SiteCell: s, FoundedTurn: 0));
            int row = world.Buckets.Add(new BucketRow(
                id, new CultureId(1), new ReligionId(1), new ClassId(1),
                5, Conserved.Zero, 0.0, 0.0, 0.0, 0.0)); // one adult cohort
            if (pop > 0)
                ledger.Flow(ref world.Buckets.Ref(row).Count, ConservedQuantityIds.Population,
                    ReasonIds.InitialEndowment, pop, FlowDirection.Source, OverdrawPolicy.Throw);
            world.ConsumptionDeficits.Add(new ConsumptionDeficitRow(id, 0.0, 0));
            world.MigrationFlows.Add(new MigrationFlowRow(id, 0, 0));
            world.SettlementVitals.Add(new SettlementVitalsRow(id, 0, 0, 10.0));
        }
        return world;
    }

    private static void Advance(WorldState world) =>
        world.Clock = new SimClock(world.Clock.Turn + 1,
            world.Clock.SimDays + 3600, DtDays: 3600); // dt 10 years

    private static List<ChronicleEvent> OfType(
        ChronicleCollector c, ChronicleEventType type)
    {
        var list = new List<ChronicleEvent>();
        foreach (ChronicleEvent e in c.Events) if (e.Type == type) list.Add(e);
        return list;
    }

    // --- names ---------------------------------------------------------------

    [Fact]
    public void Names_DeterministicFromSeedAndId_DistinctAcrossIds()
    {
        ChronicleConfig cfg = Config();
        WorldState world = Rig(settlements: 12);
        NameRegistry a = NameRegistry.Build(cfg, worldSeed: 42, world);
        NameRegistry b = NameRegistry.Build(cfg, worldSeed: 42, world);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int id = 0; id < 12; id++)
        {
            Assert.Equal(a.Name(id), b.Name(id)); // same seed => same names
            Assert.True(seen.Add(a.Name(id)), $"duplicate name '{a.Name(id)}'");
            Assert.True(a.Name(id).Length >= 2 && char.IsUpper(a.Name(id)[0]),
                $"'{a.Name(id)}' is not a capitalized name");
        }
        // A different seed reshuffles (not necessarily every name — assert
        // the REGISTRY differs somewhere, the honest claim).
        NameRegistry other = NameRegistry.Build(cfg, worldSeed: 43, world);
        bool anyDiffers = false;
        for (int id = 0; id < 12; id++) if (other.Name(id) != a.Name(id)) anyDiffers = true;
        Assert.True(anyDiffers, "seed 43 produced the identical registry — generator ignores the seed?");
    }

    [Fact]
    public void Names_CollisionPolicy_RegeneratesWithSalt_ThrowsWhenSpaceExhausted()
    {
        // A one-name phonology: the first settlement takes the only possible
        // name; the second must exhaust every salt and THROW (the stated
        // policy is regenerate-with-salt, never suffix — a silent "-II" would
        // fake a dynastic fact no mechanism recorded).
        var tiny = new ChronicleConfig(
            new PhonologyConfig(["k"], ["a"], [""], 1, 1),
            Config().Thresholds, Config().Templates);
        WorldState one = Rig(settlements: 1);
        Assert.Equal("Ka", NameRegistry.Build(tiny, 42, one).Name(0));
        WorldState two = Rig(settlements: 2);
        Assert.Throws<InvalidOperationException>(() => NameRegistry.Build(tiny, 42, two));

        // A two-name phonology seats two settlements via salt regeneration —
        // both names used, deterministically, whatever order the salts land.
        var duo = new ChronicleConfig(
            new PhonologyConfig(["k"], ["a", "o"], [""], 1, 1),
            Config().Thresholds, Config().Templates);
        NameRegistry r = NameRegistry.Build(duo, 42, two);
        Assert.NotEqual(r.Name(0), r.Name(1));
        Assert.Contains(r.Name(0), new[] { "Ka", "Ko" });
        Assert.Contains(r.Name(1), new[] { "Ka", "Ko" });
    }

    // --- founding ------------------------------------------------------------

    [Fact]
    public void Founding_FiresOnFirstSight_WithPopulationMagnitude()
    {
        var c = new ChronicleCollector(Config());
        WorldState world = Rig(pop: 640, settlements: 3);
        c.Observe(world);
        List<ChronicleEvent> founds = OfType(c, ChronicleEventType.Founding);
        Assert.Equal(3, founds.Count);
        Assert.All(founds, e => Assert.Equal(640.0, e.Magnitude1));
        Assert.Equal(0, founds[0].Turn);
        c.Observe(world); // second sight: no re-founding
        Assert.Equal(3, OfType(c, ChronicleEventType.Founding).Count);
    }

    // --- famine onset/end: two-sided at the documented thresholds ------------

    [Fact]
    public void FamineOnset_TwoSided_AtDocumentedThreshold_HysteresisOnEnd()
    {
        ChronicleConfig cfg = Config();
        double onset = cfg.Thresholds.FamineOnsetDeficit;
        var c = new ChronicleCollector(cfg);
        WorldState world = Rig();
        c.Observe(world);

        // Just below: silent.
        Advance(world);
        world.ConsumptionDeficits[0] = world.ConsumptionDeficits[0] with { DeficitRatio = onset - 1e-9 };
        c.Observe(world);
        Assert.Empty(OfType(c, ChronicleEventType.FamineOnset));

        // At threshold: fires, magnitude = the triggering deficit.
        Advance(world);
        world.ConsumptionDeficits[0] = world.ConsumptionDeficits[0] with { DeficitRatio = onset };
        world.SettlementVitals[0] = world.SettlementVitals[0] with { Deaths = 40 };
        c.Observe(world);
        ChronicleEvent onsetEvent = Assert.Single(OfType(c, ChronicleEventType.FamineOnset));
        Assert.Equal(onset, onsetEvent.Magnitude1);
        Assert.Equal(world.Clock.Turn, onsetEvent.Turn);
        Assert.Equal(0, onsetEvent.SettlementId);

        // Deficit eases BELOW onset but ABOVE end: hysteresis holds the latch
        // (no end event, no second onset).
        Advance(world);
        world.ConsumptionDeficits[0] = world.ConsumptionDeficits[0] with { DeficitRatio = 0.05 };
        world.SettlementVitals[0] = world.SettlementVitals[0] with { Deaths = 25 };
        c.Observe(world);
        Assert.Empty(OfType(c, ChronicleEventType.FamineEnd));
        Assert.Single(OfType(c, ChronicleEventType.FamineOnset));

        // Deficit returns to the end threshold: fires with duration years and
        // the deaths summed over the famine turns (40 at onset + 25 + 10).
        Advance(world);
        world.ConsumptionDeficits[0] = world.ConsumptionDeficits[0] with
        { DeficitRatio = cfg.Thresholds.FamineEndDeficit };
        world.SettlementVitals[0] = world.SettlementVitals[0] with { Deaths = 10 };
        c.Observe(world);
        ChronicleEvent end = Assert.Single(OfType(c, ChronicleEventType.FamineEnd));
        Assert.Equal(20.0, end.Magnitude1); // onset year -> end year = 2 turns x dt 10
        Assert.Equal(40.0 + 25.0 + 10.0, end.Magnitude2);
    }

    // --- extinction ----------------------------------------------------------

    [Fact]
    public void Extinction_FiresOnceWithLastPopulation_Latched()
    {
        var c = new ChronicleCollector(Config());
        WorldState world = Rig(pop: 300);
        c.Observe(world);

        Advance(world);
        var ledger = new Ledger(world.LedgerFlows);
        ledger.Flow(ref world.Buckets.Ref(0).Count, ConservedQuantityIds.Population,
            ReasonIds.Starvation, 300, FlowDirection.Sink, OverdrawPolicy.Throw);
        c.Observe(world);
        ChronicleEvent e = Assert.Single(OfType(c, ChronicleEventType.Extinction));
        Assert.Equal(300.0, e.Magnitude1); // the souls that perished

        Advance(world);
        c.Observe(world); // still empty: no repeat
        Assert.Single(OfType(c, ChronicleEventType.Extinction));
    }

    // --- first artisans ------------------------------------------------------

    [Fact]
    public void FirstArtisans_FiresOnFirstAdultArtisans_NotOnChildren()
    {
        var c = new ChronicleCollector(Config());
        WorldState world = Rig();
        var id = new SettlementId(0);
        // Artisan CHILDREN first — must not fire (the event is workshops, not births).
        int childRow = world.Buckets.Add(new BucketRow(
            id, new CultureId(1), new ReligionId(1), new ClassId(2),
            1, Conserved.Zero, 0.0, 0.0, 0.0, 0.0));
        var ledger = new Ledger(world.LedgerFlows);
        ledger.Flow(ref world.Buckets.Ref(childRow).Count, ConservedQuantityIds.Population,
            ReasonIds.InitialEndowment, 50, FlowDirection.Source, OverdrawPolicy.Throw);
        c.Observe(world);
        Advance(world);
        c.Observe(world);
        Assert.Empty(OfType(c, ChronicleEventType.FirstArtisans));

        // Adults appear: fires once with the count.
        int adultRow = world.Buckets.Add(new BucketRow(
            id, new CultureId(1), new ReligionId(1), new ClassId(2),
            6, Conserved.Zero, 0.0, 0.0, 0.0, 0.0));
        ledger.Flow(ref world.Buckets.Ref(adultRow).Count, ConservedQuantityIds.Population,
            ReasonIds.InitialEndowment, 35, FlowDirection.Source, OverdrawPolicy.Throw);
        Advance(world);
        c.Observe(world);
        ChronicleEvent e = Assert.Single(OfType(c, ChronicleEventType.FirstArtisans));
        Assert.Equal(35.0, e.Magnitude1);
        Advance(world);
        c.Observe(world);
        Assert.Single(OfType(c, ChronicleEventType.FirstArtisans)); // latched
    }

    // --- migration surge: two-sided ------------------------------------------

    [Fact]
    public void MigrationSurge_TwoSided_AtDocumentedFraction_OfStartOfTurnPopulation()
    {
        ChronicleConfig cfg = Config();
        var c = new ChronicleCollector(cfg);
        WorldState world = Rig(pop: 1000);
        c.Observe(world);

        // Just below the fraction: silent (surge = outflow / START-of-turn pop).
        long below = (long)(cfg.Thresholds.MigrationSurgeFraction * 1000) - 1;
        Advance(world);
        world.MigrationFlows[0] = world.MigrationFlows[0] with { Outflow = below };
        c.Observe(world);
        Assert.Empty(OfType(c, ChronicleEventType.MigrationSurge));

        // At the fraction: fires with count and share.
        long at = (long)(cfg.Thresholds.MigrationSurgeFraction * 1000);
        Advance(world);
        world.MigrationFlows[0] = world.MigrationFlows[0] with { Outflow = at };
        c.Observe(world);
        ChronicleEvent e = Assert.Single(OfType(c, ChronicleEventType.MigrationSurge));
        Assert.Equal(at, e.Magnitude1);
        Assert.Equal(at / 1000.0, e.Magnitude2);
    }

    // --- prose ---------------------------------------------------------------

    [Fact]
    public void Prose_FamineReadsAsAStory_NamingItsSettlement()
    {
        ChronicleConfig cfg = Config();
        WorldState world = Rig();
        NameRegistry names = NameRegistry.Build(cfg, 42, world);
        string name = names.Name(0);

        var onset = new ChronicleEvent(ChronicleEventType.FamineOnset, 10, 100.0, 0, 0.42, 0.0);
        string line = ChronicleProse.Render(onset, cfg, names);
        Assert.Contains(name, line);
        Assert.Contains("famine", line);
        Assert.Contains("42", line); // the deficit magnitude, as a percentage

        var end = new ChronicleEvent(ChronicleEventType.FamineEnd, 13, 130.0, 0, 30.0, 217.0);
        string endLine = ChronicleProse.Render(end, cfg, names);
        Assert.Contains(name, endLine);
        Assert.Contains("30", endLine);   // duration years
        Assert.Contains("217", endLine);  // the dead
    }

    [Fact]
    public void Prose_UnknownPlaceholder_Throws_NoInventedFacts()
    {
        ChronicleConfig cfg = Config();
        var bad = cfg with
        {
            Templates = cfg.Templates with
            { FamineOnset = "In {year}, {name} lost {cattleCount} head of cattle." },
        };
        WorldState world = Rig();
        NameRegistry names = NameRegistry.Build(cfg, 42, world);
        var e = new ChronicleEvent(ChronicleEventType.FamineOnset, 1, 10.0, 0, 0.2, 0.0);
        Assert.Throws<InvalidDataException>(() => ChronicleProse.Render(e, bad, names));
    }

    // --- twin-identical annals over a real autoplay ---------------------------

    [Fact]
    public void Annals_TwinIdentical_AcrossIdenticalRuns()
    {
        ChronicleConfig cfg = Config();
        List<string> RunOnce()
        {
            SimConfig sim = TestConfigs.Sim();
            using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
            using var pipeStream = Sim.Data.DataFiles.OpenPipeline();
            var exec = new TurnExecutor(EraTableLoader.Load(eraStream),
                PipelineLoader.Load(pipeStream, SystemCatalog.All(sim)));
            WorldState world = WorldFounding.Found(TestConfigs.DevWorldgen(), sim, 42, null);
            NameRegistry names = NameRegistry.Build(cfg, world.Seed, world);
            var collector = new ChronicleCollector(cfg);
            collector.Observe(world);
            // 700 turns crosses the first Malthus crash (~t590): famine,
            // surge, and possibly extinction events exist — the twins compare
            // a POPULATED chronicle, not two empty lists.
            for (int t = 1; t <= 700; t++) { world = exec.Step(world); collector.Observe(world); }
            var lines = new List<string>(collector.Events.Count);
            foreach (ChronicleEvent e in collector.Events)
                lines.Add(ChronicleProse.Render(e, cfg, names));
            return lines;
        }
        List<string> a = RunOnce(), b = RunOnce();
        Assert.True(a.Count > 4, $"only {a.Count} events across the Malthus horizon — twin rig vacuous");
        Assert.Equal(a, b);
        bool anyFamine = false;
        foreach (string line in a) if (line.Contains("famine")) anyFamine = true;
        Assert.True(anyFamine, "no famine line across the first Malthus crash — detection vacuous");
    }
}
