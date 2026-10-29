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
        // T4.21-6 — THE SEVERITY CLAUSE IS GONE FROM THE SHIPPED TEMPLATE, and
        // this is the assertion that says so rather than leaving a reader to
        // wonder. The Disaster event fires on the rising edge of
        // FoodState.IsStruck; at canonical dt 10 with durationYears 5 the struck
        // row is the TAIL row, whose Severity is 0 by construction, so the clause
        // rendered "the blight has 0 in a hundred of the fields still" on
        // essentially every canonical-era disaster (measured on the armed
        // canonical session: 55 of 60 rising edges carry severity 0). The loss
        // figure, which comes from AppliedMultiplier, carries the magnitude.
        Assert.DoesNotContain("in a hundred of the fields", disaster, StringComparison.Ordinal);
        // The same event as the canonical case actually records it: severity 0.
        string tail = ChronicleProse.Render(
            new ChronicleEvent(ChronicleEventType.Disaster, 11, 110.0, 0, 0.0, 0.30), cfg, names);
        Assert.Contains("70", tail);       // 1 - 0.30 applied — the magnitude survives
        Assert.DoesNotContain("0 in a hundred", tail, StringComparison.Ordinal);
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

    // --- M5 playtest (Director, 2026-10-19): the rounded-zero share and the
    // count of one. Both lines are from the Director's real seed-42 session;
    // the magnitudes below are the ones measured on that world (seed 42, one
    // AI Empire, canonical size: Pishemul's shortfall onset at turn 6 carried a
    // deficit ratio of 0.0003673769287288758; five settlements' first artisans
    // were a single adult). The EVENTS were right — CR-015 makes any deficit
    // above zero a shortfall, and one adult artisan is a first artisan — the
    // PROSE was wrong: Whole() rounded 0.037 to "0", and the template was
    // plural-only.

    private const double PishemulDeficit = 0.0003673769287288758;

    [Fact]
    public void Prose_ShortfallBelowHalfAPercent_NeverReadsZero_FromTheCollector()
    {
        ChronicleConfig cfg = Config();
        ChronicleCollector c = Collector(cfg);
        WorldState world = Rig();
        NameRegistry names = NameRegistry.Build(cfg, 42, world);
        c.Observe(world);
        Advance(world);
        SetDeficit(world, 0, PishemulDeficit);
        c.Observe(world);
        ChronicleEvent onset = Assert.Single(OfType(c, ChronicleEventType.FoodShortfallOnset));
        Assert.Equal(PishemulDeficit, onset.Magnitude1); // the event set is unchanged: it still fires

        string line = ChronicleProse.Render(onset, cfg, names);
        Assert.Equal("In 10, the stores at " + names.Name(0)
            + " ran short: fewer than 1 of every hundred measures of need went unmet.", line);
        Assert.DoesNotContain(" 0 of every hundred", line, StringComparison.Ordinal);
    }

    [Fact]
    public void Prose_EveryShare_RoundingNeverCrossesZeroOrTheWhole()
    {
        ChronicleConfig cfg = Config();
        WorldState world = Rig();
        NameRegistry names = NameRegistry.Build(cfg, 42, world);
        string R(ChronicleEventType t, double m1, double m2) =>
            ChronicleProse.Render(new ChronicleEvent(t, 1, 10.0, 0, m1, m2), cfg, names);

        // Every {...Pct} placeholder the shipped templates use.
        Assert.Contains(": fewer than 1 of every hundred",
            R(ChronicleEventType.FamineOnset, PishemulDeficit, (double)(int)FamineReason.Disaster), StringComparison.Ordinal);
        Assert.Contains("- fewer than 1 of every hundred",
            R(ChronicleEventType.Disaster, 0.0, 0.996), StringComparison.Ordinal);       // loss 0.4%
        Assert.Contains("- more than 99 of every hundred",
            R(ChronicleEventType.Disaster, 0.0, 0.004), StringComparison.Ordinal);       // loss 99.6%
        Assert.Contains("- 100 of every hundred",
            R(ChronicleEventType.Disaster, 0.0, 0.0), StringComparison.Ordinal);         // total loss is total
        Assert.Contains("- fewer than 1 of every hundred who lived there",
            R(ChronicleEventType.MigrationSurge, 3.0, 0.004), StringComparison.Ordinal); // a re-tuned threshold
        Assert.Contains(": more than 99 of every hundred",
            R(ChronicleEventType.FoodShortfallOnset, 0.996, 0.0), StringComparison.Ordinal);
        // Boundaries: just under half a percent rounds to 0 and reads "fewer
        // than 1"; just over reads 1; the ordinary case is unchanged.
        Assert.Contains(": fewer than 1 of every", R(ChronicleEventType.FoodShortfallOnset, 0.0049, 0.0), StringComparison.Ordinal);
        Assert.Contains(": 1 of every", R(ChronicleEventType.FoodShortfallOnset, 0.006, 0.0), StringComparison.Ordinal);
        Assert.Contains(": 7 of every", R(ChronicleEventType.FoodShortfallOnset, 0.07, 0.0), StringComparison.Ordinal);
        // A share that is genuinely zero still says zero (a famine with no
        // nominal deficit — abandonment — is not "fewer than 1").
        Assert.Contains(": 0 of every", R(ChronicleEventType.FamineOnset, 0.0, (double)(int)FamineReason.Abandonment), StringComparison.Ordinal);
    }

    [Fact]
    public void Prose_FirstArtisans_ASingleAdult_ReadsSingular_FromTheCollector()
    {
        ChronicleConfig cfg = Config();
        var c = Collector(cfg);
        WorldState world = Rig();
        NameRegistry names = NameRegistry.Build(cfg, 42, world);
        c.Observe(world);
        int adultRow = world.Buckets.Add(new BucketRow(
            new SettlementId(0), new CultureId(1), new ReligionId(1), new ClassId(2),
            6, Conserved.Zero, 0.0, 0.0, 0.0, 0.0));
        new Ledger(world.LedgerFlows).Flow(ref world.Buckets.Ref(adultRow).Count, ConservedQuantityIds.Population,
            ReasonIds.InitialEndowment, 1, FlowDirection.Source, OverdrawPolicy.Throw);
        Advance(world);
        c.Observe(world);
        ChronicleEvent e = Assert.Single(OfType(c, ChronicleEventType.FirstArtisans));
        Assert.Equal(1.0, e.Magnitude1);

        Assert.Equal("In 10, the first artisan set up a workshop at " + names.Name(0) + ", 1 master.",
            ChronicleProse.Render(e, cfg, names));
        Assert.Equal("In 10, the first artisans set up their workshops at " + names.Name(0) + ", 2 masters and hands.",
            ChronicleProse.Render(e with { Magnitude1 = 2.0 }, cfg, names));
    }

    [Fact]
    public void Prose_EveryCountTemplate_IsGrammaticalAtOne_AndUnchangedAtMany()
    {
        ChronicleConfig cfg = Config();
        WorldState world = Rig();
        NameRegistry names = NameRegistry.Build(cfg, 42, world);
        string n = names.Name(0);
        string R(ChronicleEventType t, double m1, double m2) =>
            ChronicleProse.Render(new ChronicleEvent(t, 1, 10.0, 0, m1, m2), cfg, names);

        Assert.Equal($"In the year 10, {n} was founded, 1 soul strong.", R(ChronicleEventType.Founding, 1, 0));
        Assert.Equal($"In the year 10, {n} was founded, 40 souls strong.", R(ChronicleEventType.Founding, 40, 0));
        Assert.Equal($"In 10, the famine at {n} lifted after 1 year; 1 was dead of it.", R(ChronicleEventType.FamineEnd, 1, 1));
        Assert.Equal($"In 10, the famine at {n} lifted after 30 years; 217 were dead of it.", R(ChronicleEventType.FamineEnd, 30, 217));
        Assert.Equal($"In 10, the famine at {n} lifted after 10 years; 0 were dead of it.", R(ChronicleEventType.FamineEnd, 10, 0));
        Assert.Equal($"In 10, {n} fell silent - the last 1 soul perished, and no smoke rose again.", R(ChronicleEventType.Extinction, 1, 0));
        Assert.Equal($"In 10, {n} fell silent - the last 300 souls perished, and no smoke rose again.", R(ChronicleEventType.Extinction, 300, 0));
        Assert.Equal($"In 10, 1 soul quit {n} in a single season - 5 of every hundred who lived there.", R(ChronicleEventType.MigrationSurge, 1, 0.05));
        Assert.Equal($"In 10, 42 souls quit {n} in a single season - 6 of every hundred who lived there.", R(ChronicleEventType.MigrationSurge, 42, 0.06));
        Assert.Equal($"In 10, {n} ate its fill again after 1 lean year.", R(ChronicleEventType.FoodShortfallEnd, 1, 0));
        Assert.Equal($"In 10, {n} ate its fill again after 40 lean years.", R(ChronicleEventType.FoodShortfallEnd, 40, 0));
        // Singular follows the PRINTED number: 0.6 prints "1".
        Assert.Contains(", 1 master.", R(ChronicleEventType.FirstArtisans, 0.6, 0), StringComparison.Ordinal);
    }

    [Fact]
    public void Prose_MalformedCountSelector_Throws()
    {
        ChronicleConfig cfg = Config();
        WorldState world = Rig();
        NameRegistry names = NameRegistry.Build(cfg, 42, world);
        var e = new ChronicleEvent(ChronicleEventType.FirstArtisans, 1, 10.0, 0, 1.0, 0.0);
        foreach (string bad in new[] { "{count} {count:master}.", "{count} {count:a|b|c}.", "{count} {count:a|b" })
        {
            var c2 = cfg with { Templates = cfg.Templates with { FirstArtisans = bad } };
            Assert.Throws<InvalidDataException>(() => ChronicleProse.Render(e, c2, names));
        }
        // A selector for a count the event never recorded is an unbound placeholder.
        var c3 = cfg with { Templates = cfg.Templates with { FoodShortfallOnset = "{name} {count:a|b}" } };
        Assert.Throws<InvalidDataException>(() => ChronicleProse.Render(
            new ChronicleEvent(ChronicleEventType.FoodShortfallOnset, 1, 10.0, 0, 0.1, 0.0), c3, names));
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
        // CR-003 QUARANTINE — RE-INSTATED BY T4.21-7, AND WHY, WITH BOTH
        // MEASUREMENTS. The original quarantine recorded that the
        // pre-Malthusian dev world never crashed, so the annals carried no
        // famine line and the twin compared a chronicle without one.
        //
        //   ARMED (λ = 0.01, T4.21-4, measured by that agent): RESOLVED — this
        //   exact rig rendered famine prose (docs/t4.21-4-record.md §2.7,
        //   §4.3). The quarantine call was deleted and the guard restored,
        //   correctly, FOR THAT TREE.
        //
        //   DISARMED (λ = 0, the value that SHIPS, re-measured on THIS tree by
        //   the agent writing this line, on THIS EXACT RIG — dev preset, seed
        //   42, 900 turns): NOT RESOLVED — 20 chronicle events, 0 of them a
        //   famine line. The restored guard above would fail.
        //
        // Twin-identity above — the point of this test — is unaffected and is
        // still asserted on 20 events. Which reading is right is CR-016's to
        // decide: the disaster mechanism ships COMPLETE AND TESTED BUT INERT
        // (sim.json disaster.hazardPerYear = 0.0) and its RATE is the
        // director's ruling (docs/adr/cr-016-armed-disaster-fallout.md). One
        // data edit makes this quarantine fire again, and it must then be
        // deleted and the guard restored.
        Sim.Tests.TestUtil.Cr003Quarantine.FamineGuardStillDisarmed(
            anyFamine, "a famine line across the first Malthus crash");
    }
    // --- the coded session record (ADR-059 amendment 2026-10-29) -------------------------------------------------

    /// <summary>
    /// THE CODED LINE OF EVERY CORE EVENT KIND, exactly, beside its prose: the coded record (chronicle-*.txt) is built at
    /// the source from the event's values — the same year, the same rounded counts and the same share bounds the prose
    /// prints (fewer than 1 → &lt;1, more than 99 → &gt;99) — and names the settlement by id (ST0), never by name. Each
    /// kind is pinned field for field, and every number the prose prints is the number the code carries.
    /// </summary>
    [Fact]
    public void Code_EveryEventKind_IsItsCodedLine_WithTheProsesFigures()
    {
        ChronicleConfig cfg = Config();
        WorldState world = Rig();
        NameRegistry names = NameRegistry.Build(cfg, 42, world);
        string name = names.Name(0);
        (ChronicleEvent E, string Code, string[] Figures)[] cases =
        [
            (new(ChronicleEventType.Founding, 0, 0.0, 0, 438.0, 0.0), "Y0 FN ST0 SL438", ["438"]),
            (new(ChronicleEventType.FamineOnset, 10, 100.4, 0, 0.42, (double)(int)FamineReason.Disaster), "Y100 FA ST0 PC42 RS1", ["42"]),
            (new(ChronicleEventType.FamineOnset, 10, 100.0, 0, 0.004, (double)(int)FamineReason.Both), "Y100 FA ST0 PC<1 RS3", ["fewer than 1"]),
            (new(ChronicleEventType.FamineEnd, 12, 120.0, 0, 20.0, 37.6), "Y120 FE ST0 YR20 DD38", ["20", "38"]),
            (new(ChronicleEventType.Extinction, 13, 130.0, 0, 1.0, 0.0), "Y130 XT ST0 SL1", ["1"]),
            (new(ChronicleEventType.FirstArtisans, 7, 70.0, 0, 26.0, 0.0), "Y70 AR ST0 AT26", ["26"]),
            (new(ChronicleEventType.FirstArtisans, 7, 70.0, 0, 1.0, 0.0), "Y70 AR ST0 AT1", ["1"]),
            (new(ChronicleEventType.MigrationSurge, 2, 20.0, 0, 22.0, 0.07), "Y20 MG ST0 SL22 PC7", ["22", "7"]),
            (new(ChronicleEventType.Disaster, 11, 110.0, 0, 0.60, 0.25), "Y110 DS ST0 PC75", ["75"]),
            (new(ChronicleEventType.Disaster, 11, 110.0, 0, 0.0, 0.002), "Y110 DS ST0 PC>99", ["more than 99"]),
            (new(ChronicleEventType.FoodShortfallOnset, 3, 30.0, 0, 0.000367, 0.0), "Y30 SO ST0 PC<1", ["fewer than 1"]),
            (new(ChronicleEventType.FoodShortfallEnd, 5, 50.0, 0, 10.0, 0.0), "Y50 SE ST0 YR10", ["10"]),
        ];
        var kinds = new HashSet<ChronicleEventType>();
        foreach ((ChronicleEvent e, string code, string[] figures) in cases)
        {
            Assert.Equal(code, ChronicleCode.Render(e));
            string prose = ChronicleProse.Render(e, cfg, names);
            Assert.Contains(name, prose, StringComparison.Ordinal);
            Assert.DoesNotContain(name, code, StringComparison.Ordinal);   // by id, never by name
            Assert.Contains(code.Split(' ')[0][1..], prose, StringComparison.Ordinal);   // the same year
            foreach (string f in figures) Assert.Contains(f, prose, StringComparison.Ordinal);
            kinds.Add(e.Type);
        }
        Assert.Equal(Enum.GetValues<ChronicleEventType>().Length, kinds.Count);   // every kind is pinned
    }

    /// <summary>The coded record's file: the format tag, then the name lines in ORDINAL order of their code (a dense
    /// tie set: ST1, ST10, ST2 … and a second note of a code keeps the first name), then the event lines in event order.</summary>
    [Fact]
    public void Code_TheRecordFile_IsTagNamesInOrdinalOrderThenEvents()
    {
        var r = new ChronicleCode.Record();
        foreach (int id in new[] { 2, 10, 1, 21, 3, 12 })
        {
            r.Add("Y0 FN " + r.Note(ChronicleCode.Settlement(id), "N" + id.ToString(System.Globalization.CultureInfo.InvariantCulture)) + " SL1");
        }
        r.Note("ST2", "other");          // first note wins
        r.Note("AG1", "First");
        Assert.Equal(
            ChronicleCode.FormatTag + "\nAG1=First\nST1=N1\nST10=N10\nST12=N12\nST2=N2\nST21=N21\nST3=N3\n"
            + "Y0 FN ST2 SL1\nY0 FN ST10 SL1\nY0 FN ST1 SL1\nY0 FN ST21 SL1\nY0 FN ST3 SL1\nY0 FN ST12 SL1\n",
            r.FileText());
        Assert.True(ChronicleCode.TryParseNameLine("ST12=Bigen", out string c, out string n) && c == "ST12" && n == "Bigen");
        Assert.True(ChronicleCode.TryParseNameLine("AG-1=x=y", out c, out n) && c == "AG-1" && n == "x=y");
        Assert.False(ChronicleCode.TryParseNameLine("Y0 FN ST12 SL1", out _, out _));
        Assert.False(ChronicleCode.TryParseNameLine("ABCD1=x", out _, out _));
        Assert.False(ChronicleCode.TryParseNameLine("ST=x", out _, out _));
    }
}
