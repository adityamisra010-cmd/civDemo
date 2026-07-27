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
        Assert.Equal(10, pipeline.Length);
        Assert.Equal("catchment", pipeline[0].Name);
        // T3.4b: weather is published BEFORE production reads it. Production
        // reads PREV either way (the §3.2 lag), so this is legibility rather
        // than results — pinned so a silent reorder fails a test.
        Assert.Equal("harvestweather", pipeline[1].Name);
        Assert.Equal("production", pipeline[2].Name);
        Assert.Equal("consumption", pipeline[3].Name);
        // T3.4 (D-033): price runs AFTER consumption, so a turn's demand and
        // production signals are complete before anything is priced. It reads
        // PREV regardless (the §3.2 one-turn lag), so the position is about
        // legibility rather than results — but it is pinned here so a silent
        // reorder is a failing test rather than a shrug.
        Assert.Equal("price", pipeline[4].Name);
        Assert.Equal("classmobility", pipeline[5].Name);  // T2.2, spec §3 pipeline order
        Assert.Equal("migration", pipeline[6].Name);      // T2.5, spec §3 pipeline order
        Assert.Equal("demographics", pipeline[7].Name);
        Assert.Equal("needsgrievance", pipeline[8].Name); // T2.6, spec §3 pipeline order
        Assert.Equal("pathbuild", pipeline[9].Name);
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
        Assert.Equal("trade", pipeline[2].Name);
    }

    [Fact]
    public void UnknownSystem_FailsNamingItAndListingKnown()
    {
        var e = LoadFails("""{ "pipeline": ["weather", "wether"] }""");
        Assert.Contains("pipeline[1] 'wether' is not a registered system", e.Message);
        Assert.Contains(
            "known systems: catchment, harvestweather, production, consumption, price, classmobility, migration, demographics, needsgrievance, pathbuild, weather, growth, trade",
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
}
