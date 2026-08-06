using Sim.Tests.TestUtil;

namespace Sim.Tests.Conventions;

/// <summary>
/// CONV-1 — TERM NAMESPACING BY DOMAIN (docs/conv-1-term-namespacing.md).
///
/// WHY A TEST AT ALL. A convention nobody verifies decays exactly like a stale
/// document, and this project has recorded THREE instances of that shape:
/// CLAUDE.md's merge-loop line false for eleven merges, ADR-015 §7.7 carrying a
/// refuted mechanism, and the Spine's system inventory stale by one milestone
/// from M6 onward. All three survived BECAUSE they sat in documents people read
/// to find out what is true. So the two RULED terms get a guard.
///
/// WHAT THIS CANNOT DO — stated plainly, on the T3.11 4a precedent ("no test can
/// enforce that an agent enumerated two projects; what it enforces is that the
/// rule stays performable"):
///   - it CANNOT enforce English usage. A new art document, a commit message or
///     a code comment may say "grain" and nothing here will see it;
///   - it CANNOT police the historical record, and must not: d038-visual-target
///     and the art gate records keep their original phrasing BY RULING (S8 §5 —
///     closed records describe what was said at the time);
///   - it does NOT check the two PROPOSED terms (`stock`, `source`). Enforcing
///     an unruled convention would bind a decision the director has not made.
///
/// WHAT IT DOES enforce: the one file this convention renamed STAYS renamed, and
/// the trade/toytrade split that already cost a packet cannot silently revert.
/// </summary>
public class Conv1TermNamespacingTests
{
    [Fact]
    public void Grain_IsSimOwned_TheStyleBibleDoesNotUseIt()
    {
        // ANCHORING (T3.11 4a precedent: that red proof caught its own guard
        // passing on an unrelated "run:" substring elsewhere in the file). The
        // scope here is ONE named file, so there is nowhere else a match could
        // hide — but the file legitimately contains the word inside CONV-1's own
        // rename note, which must quote the old wording to be useful. That note
        // is a markdown BLOCKQUOTE, so blockquote lines are excluded and every
        // other line is checked. A future violation would be ordinary prose and
        // would land on a non-'>' line.
        string path = Path.Combine(RepoPaths.Root(), "docs", "style-bible-parchment.md");
        string[] lines = File.ReadAllLines(path);

        var offenders = new List<string>();
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].TrimStart().StartsWith('>')) continue;   // the rename note
            if (lines[i].Contains("grain", StringComparison.OrdinalIgnoreCase))
                offenders.Add($"  line {i + 1}: {lines[i].Trim()}");
        }

        Assert.True(offenders.Count == 0,
            "CONV-1: 'grain' is namespaced to the SIM domain (goods.json id 1, the numeraire, " +
            "serialized). The paper texture is 'paper fibre' / 'fibre'. Offending lines in " +
            $"docs/style-bible-parchment.md:\n{string.Join("\n", offenders)}");

        // NOT VACUOUS: the file must actually still describe the texture it was
        // renamed FROM, or this guard would pass just as happily over a file
        // that had lost the section entirely.
        string all = string.Join("\n", lines);
        Assert.Contains("fibre", all, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Trade_IsSimOwned_TheToyKeepsTheQualifiedName()
    {
        // The collision that ALREADY BIT: pipeline presets are DATA, so a preset
        // naming "trade" bound ambiguously once a second system claimed the word
        // (T3.6, director decision 3 — the toy was renamed and PipelineLoader
        // gained a load guard). This asserts against the Name CONSTANTS, not
        // against file text, so no comment or doc mentioning either word can
        // satisfy it.
        Assert.Equal("trade", Sim.Core.Systems.Trade.TradeArbitrageSystem.Name);
        Assert.Equal("toytrade", Sim.Core.Systems.Trade.TradeSystem.Name);
        Assert.NotEqual(
            Sim.Core.Systems.Trade.TradeArbitrageSystem.Name,
            Sim.Core.Systems.Trade.TradeSystem.Name);
    }
}
