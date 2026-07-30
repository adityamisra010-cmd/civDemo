using Sim.Core.Systems;

namespace Sim.Tests.Systems;

/// <summary>
/// T3.8 — the needs-source load guards, RE-PROVEN after modification
/// (director's pre-handback check; recorded in the review record as a §7.4
/// instance in a new shape: not a missing guard but a MODIFIED one whose
/// prior red proof no longer automatically holds. The T3.5b guard "a bound
/// need with no satisfier refuses load" was taught the housingStock source
/// to clear the 229-failure wall — teaching a guard a new case can lose its
/// teeth on the old one, so BOTH properties get permanent tests and fresh
/// measured reds).
/// </summary>
public class HousingGuardTests
{
    private static string CanonicalNeeds()
    {
        using var stream = global::Sim.Data.DataFiles.OpenNeeds();
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    [Fact]
    public void BoundNeed_WithNoSatisfierOfAnyKind_StillRefusesLoad()
    {
        // THE ORIGINAL T3.5b PROPERTY, re-proven post-modification: strip
        // Shelter's source declaration (its basket lines are already gone at
        // T3.8) — a bound need with NO satisfier of any kind must refuse.
        string json = CanonicalNeeds().Replace("      \"source\": \"housingStock\",\n", "");
        var e = Assert.Throws<NeedsConfigException>(() => NeedsConfigLoader.Load(json));
        Assert.Contains("Shelter", e.Message);
        Assert.Contains("no basket entry serves it", e.Message);
        Assert.Contains("housingStock", e.Message); // the message points at the fix
    }

    [Fact]
    public void SourceTypo_RefusesLoad_NeverSilentlyFallsBackToBasket()
    {
        // The NEW case's own red: a misspelled source must not quietly become
        // "basket" (which, with no basket lines, would then be the original
        // defect wearing a typo).
        string json = CanonicalNeeds().Replace("\"source\": \"housingStock\"", "\"source\": \"housingStok\"");
        var e = Assert.Throws<NeedsConfigException>(() => NeedsConfigLoader.Load(json));
        Assert.Contains("source must be", e.Message);
        Assert.Contains("housingStok", e.Message);
    }

    [Fact]
    public void HousingSourcedNeed_WithBasketLines_RefusesLoad_TheAmbiguityGuard()
    {
        // Double-sourcing: give housing-sourced Shelter a basket line back.
        string json = CanonicalNeeds().Replace(
            "\"entries\": [",
            "\"entries\": [\n      { \"class\": 1, \"need\": 2, \"good\": \"timber\", \"perPersonYear\": 0.03 },");
        var e = Assert.Throws<NeedsConfigException>(() => NeedsConfigLoader.Load(json));
        Assert.Contains("two satisfaction sources", e.Message);
        Assert.Contains("Shelter", e.Message);
    }
}
