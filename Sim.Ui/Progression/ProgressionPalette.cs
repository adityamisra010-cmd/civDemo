using Sim.Core.Systems.Research;
using Sim.Ui.Theme;
using Rgba = Sim.Ui.Art.ParchmentPalette.Rgba;

namespace Sim.Ui.Progression;

/// <summary>
/// The progression screen's colour LOOKUPS. The night-ink palette that lived here (one fixed dark
/// field, gold and cyan accents) has converged on the era theme (ADR-033 D8): every colour the
/// screen paints is now an <see cref="EraTheme"/> token — the lane hues are
/// <see cref="SemanticTokens.Lanes"/>, the state treatments the semantic tokens — so the same
/// screen is drawn in the era of the player's Age. What remains here is the node-to-lane and
/// state-to-word mapping.
/// </summary>
public static class ProgressionPalette
{
    /// <summary>The lane hue of a node in <paramref name="theme"/>: its subtree, the trunk, or civics.</summary>
    public static Rgba BranchOf(EraTheme theme, ResearchContent content, ResearchNode node) =>
        node.Tree == ResearchTree.Civics ? theme.Semantic.Lanes.Civics
        : node.Branch < 0 ? theme.Semantic.Lanes.Main : theme.Semantic.Lanes.Of(content.Branches[node.Branch].Id);

    public static string StateLabel(NodeState s) => s switch
    {
        NodeState.Completed => "Completed",
        NodeState.CurrentTarget => "Researching",
        NodeState.Available => "Available",
        NodeState.Partial => "Partly researched",
        _ => "Locked",
    };
}
