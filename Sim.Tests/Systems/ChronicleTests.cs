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

    /// <summary>T4.21-5: the collector classifies famine through FoodState, so
    /// every rig hands it the shipped sim config — the same one the kernel runs.</summary>
    private static ChronicleCollector Collector(ChronicleConfig? cfg = null) =>
        new(cfg ?? Config(), TestConfigs.Sim());

    /// <summary>a — the absorbable shortfall. Below it a settlement is STRESS,
    /// above it SEVERE; neither is famine, whatever the depth.</summary>
    private static double Absorbable => TestConfigs.Sim().FoodState.AdaptationAbsorbableShortfall;

    private static void SetDeficit(WorldState w, int row, double d) =>
        w.ConsumptionDeficits[row] = w.ConsumptionDeficits[row] with { DeficitRatio = d };

    /// <summary>Strike the settlement: a DisasterRow whose AppliedMultiplier is
    /// below 1 IS "a famine-class multiplier was applied to the harvest that
    /// produced this world's food" (FoodState.IsStruck).</summary>
    private static void Strike(WorldState w, SettlementId id, double severity, double applied)
    {
        for (int i = 0; i < w.Disasters.Count; i++)
        {
            if (w.Disasters[i].Settlement != id) continue;
            w.Disasters[i] = w.Disasters[i] with { Severity = severity, AppliedMultiplier = applied };
            return;
        }
        w.Disasters.Add(new DisasterRow(id, 1, severity, 0.0, 1.0, applied));
    }

    private static void Lift(WorldState w, SettlementId id)
    {
        for (int i = 0; i < w.Disasters.Count; i++)
            if (w.Disasters[i].Settlement == id)
                w.Disasters[i] = w.Disasters[i] with { AppliedMultiplier = 1.0, Severity = 0.0 };
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
        var c = Collector();
        WorldState world = Rig(pop: 640, settlements: 3);
        c.Observe(world);
        List<ChronicleEvent> founds = OfType(c, ChronicleEventType.Founding);
        Assert.Equal(3, founds.Count);
        Assert.All(founds, e => Assert.Equal(640.0, e.Magnitude1));
        Assert.Equal(0, founds[0].Turn);
        c.Observe(world); // second sight: no re-founding
        Assert.Equal(3, OfType(c, ChronicleEventType.Founding).Count);
    }

    // --- famine: the CLASSIFICATION, two-sided (T4.21-5 / CR-015) ------------

    /// <summary>
    /// THE PACKET'S CENTRAL TWO-SIDED TEST. The annals say "famine" when
    /// FoodState says FAMINE, and at no other time. The negative arm is the one
    /// that matters: a deficit of 0.60 — four times the RETIRED 0.15 constant
    /// and three times the absorbable shortfall, a settlement genuinely starving
    /// — produces NO famine event, because nothing struck it and its fields are
    /// tilled. Under the pre-T4.21-5 collector every one of these turns was a
    /// famine line. It is a FOOD SHORTFALL instead, and that event fires.
    /// </summary>
    [Fact]
    public void Famine_FiresOnTheClassificationAndItsReason_NotOnAnOrdinaryBadHarvest()
    {
        ChronicleCollector c = Collector();
        WorldState world = Rig();
        var id = new SettlementId(0);
        c.Observe(world);

        // ORDINARY BAD HARVEST, at every depth the old constant would have
        // called famine: STRESS, then SEVERE. No strike, fields tilled.
        foreach (double d in new[] { Absorbable - 1e-9, 0.15, Absorbable + 1e-9, 0.60, 0.99 })
        {
            Advance(world);
            SetDeficit(world, 0, d);
            world.SettlementVitals[0] = world.SettlementVitals[0] with { Deaths = 40 };
            c.Observe(world);
        }
        Assert.Empty(OfType(c, ChronicleEventType.FamineOnset));
        Assert.Empty(OfType(c, ChronicleEventType.Disaster));
        // The shortfall IS recorded — the event is not lost, it is named correctly.
        ChronicleEvent shortfall = Assert.Single(OfType(c, ChronicleEventType.FoodShortfallOnset));
        Assert.Equal(Absorbable - 1e-9, shortfall.Magnitude1);

        // THE STRIKE. Same deficit, now with a famine-class multiplier applied
        // to the harvest that produced it: FAMINE, reason Disaster.
        Advance(world);
        Strike(world, id, severity: 0.8, applied: 0.25);
        SetDeficit(world, 0, 0.60);
        world.SettlementVitals[0] = world.SettlementVitals[0] with { Deaths = 40 };
        c.Observe(world);
        ChronicleEvent onset = Assert.Single(OfType(c, ChronicleEventType.FamineOnset));
        Assert.Equal(0.60, onset.Magnitude1);
        Assert.Equal((double)(int)FamineReason.Disaster, onset.Magnitude2);
        Assert.Equal(world.Clock.Turn, onset.Turn);
        Assert.Equal(0, onset.SettlementId);

        // A DEEP deficit does not END a famine, and a shallow one does not
        // start one: only the classification moves the latch. Still struck,
        // still famine, no second onset.
        Advance(world);
        SetDeficit(world, 0, 0.05);
        world.SettlementVitals[0] = world.SettlementVitals[0] with { Deaths = 25 };
        c.Observe(world);
        Assert.Single(OfType(c, ChronicleEventType.FamineOnset));
        Assert.Empty(OfType(c, ChronicleEventType.FamineEnd));

        // The strike lifts: the classification leaves Famine and the annals
        // close it, with the duration and the deaths summed over famine turns.
        Advance(world);
        Lift(world, id);
        SetDeficit(world, 0, 0.05);
        world.SettlementVitals[0] = world.SettlementVitals[0] with { Deaths = 10 };
        c.Observe(world);
        ChronicleEvent end = Assert.Single(OfType(c, ChronicleEventType.FamineEnd));
        Assert.Equal(20.0, end.Magnitude1);                 // 2 turns x dt 10
        Assert.Equal(40.0 + 25.0 + 10.0, end.Magnitude2);
    }

    /// <summary>The OTHER reason, so the reason field is not a constant: fields
    /// left untilled (Farming == 0 and Herding == 0 on the raw row in force)
    /// with a deficit is FAMINE by abandonment, with no disaster anywhere.</summary>
    [Fact]
    public void Famine_AbandonmentReason_IsRecordedDistinctlyFromDisaster()
    {
        ChronicleCollector c = Collector();
        WorldState world = Rig();
        var id = new SettlementId(0);
        c.Observe(world);

        Advance(world);
        world.SectorAllocations.Add(new SectorAllocationRow(id, 0.0, 0.0, 0.3, 0.3, 0.4));
        SetDeficit(world, 0, 0.10);   // BELOW the absorbable shortfall: STRESS, were it not abandoned
        c.Observe(world);

        ChronicleEvent onset = Assert.Single(OfType(c, ChronicleEventType.FamineOnset));
        Assert.Equal((double)(int)FamineReason.Abandonment, onset.Magnitude2);
        Assert.Empty(OfType(c, ChronicleEventType.Disaster));
    }

    // --- disaster: two-sided, and an EDGE not a per-turn repeat --------------

    [Fact]
    public void Disaster_FiresOnTheAppliedMultiplier_OncePerEvent_NotOnBadWeather()
    {
        ChronicleCollector c = Collector();
        WorldState world = Rig();
        var id = new SettlementId(0);
        c.Observe(world);

        // Bad weather is not a disaster: a harvest-weather row far below one,
        // and a deficit to go with it, emit nothing. The disaster event reads
        // the DisasterRow and only the DisasterRow.
        Advance(world);
        world.HarvestWeather.Add(new HarvestWeatherRow(id, -1.2, 0.30));
        SetDeficit(world, 0, 0.45);
        c.Observe(world);
        Assert.Empty(OfType(c, ChronicleEventType.Disaster));

        // A row whose AppliedMultiplier is exactly 1.0 is a row, not a strike:
        // the predicate is < 1, and presence alone must not fire it.
        Advance(world);
        Strike(world, id, severity: 0.0, applied: 1.0);
        c.Observe(world);
        Assert.Empty(OfType(c, ChronicleEventType.Disaster));

        // Below one: fires, carrying severity and the multiplier applied.
        Advance(world);
        Strike(world, id, severity: 0.7, applied: 0.65);
        c.Observe(world);
        ChronicleEvent e = Assert.Single(OfType(c, ChronicleEventType.Disaster));
        Assert.Equal(0.7, e.Magnitude1);
        Assert.Equal(0.65, e.Magnitude2);

        // Still struck next turn: ONE line per event, not one per turn.
        Advance(world);
        c.Observe(world);
        Assert.Single(OfType(c, ChronicleEventType.Disaster));

        // Lifted, then struck again: a NEW event is a new line.
        Advance(world);
        Lift(world, id);
        c.Observe(world);
        Advance(world);
        Strike(world, id, severity: 0.5, applied: 0.5);
        c.Observe(world);
        Assert.Equal(2, OfType(c, ChronicleEventType.Disaster).Count);
    }

    // --- food shortfall: two-sided on the sign of the deficit -----------------

    [Fact]
    public void FoodShortfall_TwoSided_OnTheDeficitCrossingZero()
    {
        ChronicleCollector c = Collector();
        WorldState world = Rig();
        c.Observe(world);

        // Exactly zero is not a shortfall (the crossing is > 0, not >= 0).
        Advance(world);
        SetDeficit(world, 0, 0.0);
        c.Observe(world);
        Assert.Empty(OfType(c, ChronicleEventType.FoodShortfallOnset));

        // The smallest positive deficit is one — no band, no threshold.
        Advance(world);
        SetDeficit(world, 0, double.Epsilon);
        c.Observe(world);
        ChronicleEvent onset = Assert.Single(OfType(c, ChronicleEventType.FoodShortfallOnset));
        Assert.Equal(double.Epsilon, onset.Magnitude1);

        // Still short: no repeat.
        Advance(world);
        SetDeficit(world, 0, 0.3);
        c.Observe(world);
        Assert.Single(OfType(c, ChronicleEventType.FoodShortfallOnset));
        Assert.Empty(OfType(c, ChronicleEventType.FoodShortfallEnd));

        // Back to zero: the end fires with the duration in sim-years.
        Advance(world);
        SetDeficit(world, 0, 0.0);
        c.Observe(world);
        ChronicleEvent end = Assert.Single(OfType(c, ChronicleEventType.FoodShortfallEnd));
        Assert.Equal(20.0, end.Magnitude1);   // 2 turns x dt 10
    }

    /// <summary>The retired constants are GONE from the type, not merely
    /// unread: a threshold nobody reads is a threshold somebody re-reads.</summary>
    [Fact]
    public void TheChronicleOwnsNoFamineThreshold_AnyMore()
    {
        Type t = typeof(ChronicleThresholds);
        Assert.Null(t.GetProperty("FamineOnsetDeficit"));
        Assert.Null(t.GetProperty("FamineEndDeficit"));
        Assert.NotNull(t.GetProperty("MigrationSurgeFraction"));

        string json;
        using (var stream = Sim.Data.DataFiles.OpenChronicle())
        using (var reader = new StreamReader(stream))
            json = reader.ReadToEnd();
        Assert.DoesNotContain("\"famineOnsetDeficit\"", json);
        Assert.DoesNotContain("\"famineEndDeficit\"", json);
    }

    // --- extinction ----------------------------------------------------------

    [Fact]
    public void Extinction_FiresOnceWithLastPopulation_Latched()
    {
        var c = Collector();
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
        var c = Collector();
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
        var c = Collector(cfg);
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

        var onset = new ChronicleEvent(
            ChronicleEventType.FamineOnset, 10, 100.0, 0, 0.42, (double)(int)FamineReason.Disaster);
        string line = ChronicleProse.Render(onset, cfg, names);
        Assert.Contains(name, line);
        Assert.Contains("famine", line);
        Assert.Contains("42", line); // the deficit magnitude, as a percentage
        // T4.21-5: {reason} binds the RECORDED FamineReason ordinal, and the two
        // reasons read differently — a constant string would pass neither arm.
        Assert.Contains("ruined harvest", line);
        string abandoned = ChronicleProse.Render(
            onset with { Magnitude2 = (double)(int)FamineReason.Abandonment }, cfg, names);
        Assert.Contains("untilled", abandoned);
        Assert.DoesNotContain("ruined harvest", abandoned);

        // The three T4.21-5 events render from their own recorded magnitudes.
        string disaster = ChronicleProse.Render(
            new ChronicleEvent(ChronicleEventType.Disaster, 11, 110.0, 0, 0.60, 0.25), cfg, names);
        Assert.Contains(name, disaster);
        Assert.Contains("75", disaster);   // 1 - 0.25 applied, as a percentage
        Assert.Contains("60", disaster);   // severity, as a percentage
        string shortfall = ChronicleProse.Render(
            new ChronicleEvent(ChronicleEventType.FoodShortfallOnset, 12, 120.0, 0, 0.07, 0.0), cfg, names);
        Assert.Contains("7", shortfall);
        Assert.DoesNotContain("famine", shortfall);
        string recovered = ChronicleProse.Render(
            new ChronicleEvent(ChronicleEventType.FoodShortfallEnd, 13, 130.0, 0, 40.0, 0.0), cfg, names);
        Assert.Contains("40", recovered);

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
            var collector = new ChronicleCollector(cfg, sim);
            collector.Observe(world);
            // 900 turns crosses the first Malthus crash (~t820 on the T3.1
            // refreshed worldgen — moist river valleys raised capacity and
            // stretched the growth arc; was ~t590): famine,
            // surge, and possibly extinction events exist — the twins compare
            // a POPULATED chronicle, not two empty lists.
            for (int t = 1; t <= 900; t++) { world = exec.Step(world); collector.Observe(world); }
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
        // CR-003: no crash, so no famine line. Twin-identity above (the point
        // of this test) is unaffected and still asserted on 5+ events.
        Sim.Tests.TestUtil.Cr003Quarantine.FamineGuardStillDisarmed(
            anyFamine, "a famine line across the first Malthus crash");
    }
}
