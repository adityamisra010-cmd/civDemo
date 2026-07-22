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
        Assert.Equal(6, pipeline.Length);
        Assert.Equal("catchment", pipeline[0].Name);
        Assert.Equal("farming", pipeline[1].Name);
        Assert.Equal("consumption", pipeline[2].Name);
        Assert.Equal("classmobility", pipeline[3].Name); // T2.2, spec §3 pipeline order
        Assert.Equal("demographics", pipeline[4].Name);
        Assert.Equal("pathbuild", pipeline[5].Name);
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
            "known systems: catchment, farming, consumption, classmobility, demographics, pathbuild, weather, growth, trade",
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
