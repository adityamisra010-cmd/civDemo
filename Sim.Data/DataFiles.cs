using System.Reflection;

namespace Sim.Data;

/// <summary>
/// Access to the JSON content files shipped in this assembly as embedded
/// resources. Callers (CLI, tests, kernel bootstrap) get Streams; Sim.Core's
/// loaders stay filesystem-free. All values in these files are TUNE parameters
/// (D-006) — edit freely, the loaders validate on load.
/// </summary>
public static class DataFiles
{
    /// <summary>The era-pacing table (D-006), consumed by EraTableLoader.</summary>
    public static Stream OpenEraPacing() => Open("Sim.Data.era-pacing.json");

    /// <summary>The M1 PRODUCTION turn pipeline (§3.3), consumed by PipelineLoader.</summary>
    public static Stream OpenPipeline() => Open("Sim.Data.pipeline.json");

    /// <summary>
    /// The retired T0.x toy pipeline (m1 spec §3): the kernel-invariant tests —
    /// golden/replay/harness on terrain-less toy worlds — and the M0-shaped CLI
    /// keep exercising the kernel through these systems until T1.9 extends the
    /// harness to founded production worlds.
    /// </summary>
    public static Stream OpenPipelineToy() => Open("Sim.Data.pipeline.toy.json");

    /// <summary>Worldgen tuning (D-015/D-022), consumed by WorldgenConfigLoader.</summary>
    public static Stream OpenWorldgen() => Open("Sim.Data.worldgen.json");

    /// <summary>Population/food-loop tuning (T1.5), consumed by SimConfigLoader.</summary>
    public static Stream OpenSim() => Open("Sim.Data.sim.json");

    /// <summary>The D-018 needs registry + grievance tuning (T2.6), consumed by
    /// NeedsConfigLoader (attached to SimConfig via SimConfigLoader.Load's
    /// two-stream overload).</summary>
    public static Stream OpenNeeds() => Open("Sim.Data.needs.json");

    /// <summary>The D-031 goods roster + recipe book (T3.2), consumed by
    /// GoodsConfigLoader (attached to SimConfig via the three-stream Load).</summary>
    public static Stream OpenGoods() => Open("Sim.Data.goods.json");

    /// <summary>The T2.8 calibration corridors (TUNE bands) consumed by the
    /// calibration battery and any offline nightly analysis.</summary>
    public static Stream OpenCorridors() => Open("Sim.Data.corridors.json");

    /// <summary>The T2.9 chronicle data: name phonology, event thresholds,
    /// prose templates — consumed by ChronicleConfigLoader.</summary>
    public static Stream OpenChronicle() => Open("Sim.Data.chronicle.json");

    /// <summary>ADR-029 (D-044, addendum A): the research graph — Technology (Main tree +
    /// five subtrees) and Civics — AUTHORED content (the finalization proposal; audited by
    /// scripts/research-content-audit.py); consumed by ResearchContentLoader (attached to
    /// SimConfig via the four-stream Load).</summary>
    public static Stream OpenResearch() => Open("Sim.Data.research.json");

    /// <summary>D-063 (tranche 2): the research.json the tranche-2 content was imported into (d22f936) — the attribution
    /// twin's content, read by Sim.Tests' TestConfigs.PreTranche2 and the CLI's `--without-tranche2`. Not shipped play
    /// content: nothing loads it unless asked.</summary>
    public static Stream OpenResearchPreTranche2() => Open("Sim.Data.research-pre-tranche2.json");

    /// <summary>ADR-031 (D-047): the nine Ages, milestone facts and Age-surge emphases —
    /// PROVISIONAL content; consumed by AgeContentLoader (attached to SimConfig via the
    /// six-stream Load).</summary>
    public static Stream OpenAges() => Open("Sim.Data.ages.json");

    /// <summary>ADR-031 (D-047 Part 2): the twelve unit families and their per-Age
    /// identities — consumed by UnitFamilyContentLoader (the six-stream Load).</summary>
    public static Stream OpenUnitFamilies() => Open("Sim.Data.unit-families.json");

    /// <summary>M6 review (2026-10-23): the educational text of each research node — DISPLAY TEXT ONLY, generated from the
    /// knowledge-redesign corpus (docs/m6-plan/knowledge-redesign/tools/export_runtime_texts.py; provenance and evidence
    /// status stay in the corpus). Read by the UI's research views; never by the simulation.</summary>
    public static Stream OpenKnowledgeTexts() => Open("Sim.Data.knowledge-texts.json");

    /// <summary>M6.9 / M6.10 (D-E, D-F option 1; D-066 R2): the INERT unit-progression content — promotion trees and the
    /// dormant training / Action Capacity contract data — consumed by UnitProgressionContentLoader. No simulation system reads
    /// it (UnitProgressionInertTests); the unit card shows it as planned for M7.</summary>
    public static Stream OpenUnitProgression() => Open("Sim.Data.unit-progression.json");

    private static Stream Open(string logicalName) =>
        Assembly.GetExecutingAssembly().GetManifestResourceStream(logicalName)
        ?? throw new InvalidOperationException(
            $"embedded resource '{logicalName}' not found in Sim.Data — " +
            "check the csproj EmbeddedResource items.");
}
