using Sim.Core;
using Sim.Core.Kernel;

namespace Sim.Tests.Kernel;

// T0.5 acceptance: pipeline.json validation — unknown system id, duplicates,
// empty list all fail with actionable messages.
public class PipelineLoaderTests
{
    private static readonly SystemRegistration[] Available =
        SystemCatalog.All(TestUtil.TestConfigs.Sim());

    private static PipelineFormatException LoadFails(string json)
        => Assert.Throws<PipelineFormatException>(() => PipelineLoader.Load(json, Available));

    [Fact]
    public void CanonicalPipelineFile_LoadsInConfiguredOrder()
    {
        // The M2 production preset (m2 spec §3; classmobility added at T2.2).
        using var stream = Sim.Data.DataFiles.OpenPipeline();
        var pipeline = PipelineLoader.Load(stream, Available);
        Assert.Equal(17, pipeline.Length);   // T4.5 `appropriation`; T4.4 `colonization`; M4-D `construction`; T4.13 `revolt`; M5 `governance`
        Assert.Equal("catchment", pipeline[0].Name);
        // T3.4b: weather is published BEFORE production reads it. Production
        // reads PREV either way (the §3.2 lag), so this is legibility rather
        // than results — pinned so a silent reorder fails a test.
        Assert.Equal("harvestweather", pipeline[1].Name);
        Assert.Equal("production", pipeline[2].Name);
        // T4.5 (D-037 B3): appropriation sits BETWEEN production and consumption,
        // so grain taken by hungry herders is available to eat in the same turn
        // rather than a turn later. It reads PREV deficits like every other
        // cross-system signal (the §3.2 lag); only the relief is same-turn.
        Assert.Equal("appropriation", pipeline[3].Name);
        Assert.Equal("consumption", pipeline[4].Name);
        // T3.4 (D-033): price runs AFTER consumption, so a turn's demand and
        // production signals are complete before anything is priced. It reads
        // PREV regardless (the §3.2 one-turn lag), so the position is about
        // legibility rather than results — but it is pinned here so a silent
        // reorder is a failing test rather than a shrug.
        Assert.Equal("price", pipeline[5].Name);
        // T3.6 (D-034): trade runs AFTER price — it arbitrages the prices the
        // solver just published. It reads PREV either way (the §3.2 lag), so
        // position is legibility; pinned so a silent reorder fails a test.
        Assert.Equal("trade", pipeline[6].Name);
        // T3.8: housing runs after the goods economy settles the turn's flows.
        // It reads PREV either way (the §3.2 lag: maintenance draws Prev
        // stocks, pathbuild subtracts Prev housing labor next turn), so the
        // position is legibility — pinned so a silent reorder fails a test.
        Assert.Equal("housing", pipeline[7].Name);
        // M4-D: construction runs immediately AFTER housing. Both draw on the
        // same construction-sector pool, and construction subtracts housing's
        // published draw at the standard one-turn lag — a table read, never a
        // system reference. It sits after production/consumption/trade so the
        // materials it checks are the ones the settlement actually holds when
        // the turn's goods flows have settled.
        Assert.Equal("construction", pipeline[8].Name);
        Assert.Equal("classmobility", pipeline[9].Name);  // T2.2, spec §3 pipeline order
        Assert.Equal("migration", pipeline[10].Name);     // T2.5, spec §3 pipeline order
        // T4.4: colonization runs immediately AFTER migration and BEFORE
        // demographics. After, because it draws the founding party from LIVE
        // post-migration buckets and reading PREV would double-spend against
        // migration's overdraw scaler; before, because the party is ordinary
        // people who should age and breed this turn like everyone else.
        Assert.Equal("colonization", pipeline[11].Name);
        // T4.13: revolt runs immediately AFTER colonization. After, because a
        // settlement founded THIS turn has no Prev happiness reading and must
        // not be judged on one; and it must precede nothing in particular, since
        // it reads only Prev and writes only the control relation. Its position
        // is therefore about legibility rather than correctness — but it is
        // pinned here so a future reorder is a deliberate act, not a diff.
        Assert.Equal("revolt", pipeline[12].Name);
        // M5: governance runs immediately AFTER revolt. After, because a
        // settlement a polity LOST this turn must not have its administrative
        // reach recomputed as though the polity still held it; and after
        // pathbuild's network from the previous turn, since reach is a travel
        // cost from the capital. It reads Prev and writes only the tax policy
        // table and the control relation's strength.
        Assert.Equal("governance", pipeline[13].Name);
        Assert.Equal("demographics", pipeline[14].Name);
        Assert.Equal("needsgrievance", pipeline[15].Name); // T2.6, spec §3 pipeline order
        Assert.Equal("pathbuild", pipeline[16].Name);
    }

    [Fact]
    public void ToyPipelineFile_LoadsTheRetiredToys()
    {
        // The retired T0.x preset (m1 spec §3) — kernel-invariant tests run it.
        using var stream = Sim.Data.DataFiles.OpenPipelineToy();
        var pipeline = PipelineLoader.Load(stream, Available);
        Assert.Equal(3, pipeline.Length);
        Assert.Equal("weather", pipeline[0].Name);
        Assert.Equal("growth", pipeline[1].Name);
        Assert.Equal("toytrade", pipeline[2].Name);
    }

    [Fact]
    public void UnknownSystem_FailsNamingItAndListingKnown()
    {
        var e = LoadFails("""{ "pipeline": ["weather", "wether"] }""");
        Assert.Contains("pipeline[1] 'wether' is not a registered system", e.Message);
        Assert.Contains(
            "known systems: catchment, harvestweather, production, appropriation, consumption, price, trade, housing, construction, classmobility, migration, colonization, revolt, governance, demographics, needsgrievance, pathbuild, weather, growth, toytrade",
            e.Message);
    }

    [Fact]
    public void DuplicateSystem_FailsNamingBothPositions()
    {
        var e = LoadFails("""{ "pipeline": ["weather", "growth", "weather"] }""");
        Assert.Contains("pipeline[2] 'weather' is a duplicate (already listed at position 0)", e.Message);
    }

    [Fact]
    public void EmptyOrMissingPipeline_FailsActionably()
    {
        Assert.Contains("non-empty 'pipeline' array", LoadFails("""{ "pipeline": [] }""").Message);
        Assert.Contains("non-empty 'pipeline' array", LoadFails("""{ }""").Message);
    }

    [Fact]
    public void InvalidJson_FailsActionably()
    {
        Assert.Contains("not valid JSON", LoadFails("{ nope").Message);
    }

    [Fact]
    public void AmbiguousRoster_DuplicateName_RefusesBeforeAnyBinding()
    {
        // T3.6 (director decision 3b): a duplicate name in the AVAILABLE
        // roster binds presets silently to whichever registration wins the
        // scan — the config-fails-quietly class. The roster itself is refused.
        // Proven RED by deleting the ValidateRoster call in PipelineLoader.
        // The Load path is exercised with the same registration twice (the one
        // collision constructible from outside — the ctor is internal by
        // design); the id arm is attacked through the pure guard below.
        SystemRegistration[] dup = [Available[0], Available[1], Available[1]];
        var e = Assert.Throws<PipelineFormatException>(
            () => PipelineLoader.Load("""{ "pipeline": ["weather"] }""", dup));
        Assert.Contains("AMBIGUOUS", e.Message);
        Assert.Contains(Available[1].Name, e.Message);
    }

    [Fact]
    public void AmbiguousRoster_DuplicateId_RefusesNamingBothSystems()
    {
        var e = Assert.Throws<PipelineFormatException>(
            () => PipelineLoader.ValidateRoster(["alpha", "beta"], [7, 7]));
        Assert.Contains("AMBIGUOUS", e.Message);
        Assert.Contains("WellKnownId", e.Message);
        Assert.Contains("alpha", e.Message);
        Assert.Contains("beta", e.Message);
        // And a clean roster passes.
        PipelineLoader.ValidateRoster(["alpha", "beta"], [7, 8]);
    }

    [Fact]
    public void ShippedRoster_HasNoDuplicateNamesOrIds_TheDecision3dAudit()
    {
        // Director decision 3d: the audit, kept as a permanent pin rather
        // than a one-time grep — the registry PERMITTING duplicates was the
        // finding, and the roster guard closes the class; this asserts the
        // shipped roster never trips it.
        var names = new HashSet<string>();
        var ids = new HashSet<int>();
        foreach (SystemRegistration r in Available)
        {
            Assert.True(names.Add(r.Name), $"duplicate system name '{r.Name}' in SystemCatalog.All");
            Assert.True(ids.Add(r.Id.Value), $"duplicate WellKnownId {r.Id.Value} in SystemCatalog.All");
        }
    }
}
