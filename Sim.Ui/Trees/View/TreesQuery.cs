using Sim.Ui.Art.Glyphs;

namespace Sim.Ui.Trees.View;

/// <summary>How strongly a node or edge is drawn. Ordered: a later member always wins
/// when two rules disagree (a filtered-out node stays dimmed even inside the focused lens).</summary>
public enum Emphasis { Normal = 0, Context = 1, Dimmed = 2, Hidden = 3 }

/// <summary>
/// One node as the screen will show it: its content, its REPORTED state resolved against
/// its type's state set, and the interaction overlays (task §11: highlighted, hovered,
/// selected, disabled, prerequisite path). "Newly completed / newly discovered" come from
/// the animator, which alone knows when a state was first seen.
/// </summary>
public sealed record NodeView(
    int Index, NodeDef Def, NodeTypeDef Type, StateDef State, NodeStatus? Status,
    bool StateReported, bool StateValid, Emphasis Emphasis,
    bool Selected, bool Hovered, bool SearchMatch, bool InFocus, bool OnPath, bool Disabled);

public sealed record EdgeView(GraphEdge Edge, Emphasis Emphasis, bool OnPath, bool CrossLens);

public sealed record TreesView(
    IReadOnlyList<NodeView> Nodes, IReadOnlyList<EdgeView> Edges, int MatchCount, int VisibleCount,
    IReadOnlyList<string> ActiveFilters);

/// <summary>
/// THE QUERY — a pure function (graph, reported state, UI state) → what to emphasise.
/// Lens focus, the state / Age / type filters, search, and the selected node's focus mode
/// are all evaluated here, headless, so "filtering works" is a test over this function and
/// not over pixels.
/// </summary>
public static class TreesQuery
{
    public static TreesView Evaluate(TreeGraph g, TreesStateSnapshot snapshot, TreesUiState ui, AgesDocument ages)
    {
        TreesDocument c = g.Content;
        int n = g.Count;

        // Focus set from the selection.
        var focus = new bool[n];
        bool focusing = ui.Selected is int && ui.Focus != FocusMode.None;
        if (ui.Selected is int sel)
        {
            focus[sel] = true;
            if (ui.Focus is FocusMode.Prerequisites or FocusMode.Lineage) foreach (int i in g.Upstream(sel)) focus[i] = true;
            if (ui.Focus is FocusMode.Downstream or FocusMode.Lineage) foreach (int i in g.Downstream(sel)) focus[i] = true;
        }

        // The prerequisite path without a focus mode: the selected node's direct upstream.
        var directUp = new bool[n];
        if (ui.Selected is int s0)
            foreach (GraphEdge e in g.DirectUpstream(s0)) directUp[e.FlowFrom] = true;

        // Lens membership and the lens's immediate context.
        var inLens = new bool[n];
        var context = new bool[n];
        for (int i = 0; i < n; i++) inLens[i] = ui.Lens is null || g.InLens(i, ui.Lens);
        if (ui.Lens is not null)
            foreach (GraphEdge e in g.Edges)
            {
                if (inLens[e.From] && !inLens[e.To]) context[e.To] = true;
                if (inLens[e.To] && !inLens[e.From]) context[e.From] = true;
            }

        AgeDef? ageFilter = ui.AgeFilter is not null && ages.TryAge(ui.AgeFilter, out AgeDef af) ? af : null;
        string search = ui.Search.Trim();

        var nodes = new NodeView[n];
        int matches = 0, visible = 0;
        for (int i = 0; i < n; i++)
        {
            NodeDef def = g.Node(i);
            NodeTypeDef type = g.TypeOf(i);
            NodeStatus? status = snapshot.Status(def.Id);
            (StateDef state, bool reported, bool valid) = TreesQueryState.Resolve(c, def, status);

            bool passState = ui.StateFilter.Count == 0 || ui.StateFilter.Contains(state.Id);
            bool passType = ui.TypeFilter is null || def.Type == ui.TypeFilter;
            bool passAge = ageFilter is null || InAge(def, ageFilter, ages);
            bool passFilters = passState && passType && passAge;
            bool match = search.Length > 0 &&
                (def.Name.Contains(search, StringComparison.OrdinalIgnoreCase) || def.Id.Contains(search, StringComparison.OrdinalIgnoreCase));
            if (match) matches++;

            Emphasis em = Emphasis.Normal;
            if (!inLens[i]) em = context[i] ? Emphasis.Context : (ui.FilterMode == FilterMode.Hide ? Emphasis.Hidden : Emphasis.Dimmed);
            if (!passFilters) em = Max(em, ui.FilterMode == FilterMode.Hide ? Emphasis.Hidden : Emphasis.Dimmed);
            if (search.Length > 0 && !match) em = Max(em, Emphasis.Dimmed);
            if (focusing && !focus[i]) em = Max(em, Emphasis.Dimmed);
            bool selected = ui.Selected == i;
            if (selected && em > Emphasis.Normal) em = Emphasis.Normal;   // the selection is never lost from view
            if (em != Emphasis.Hidden) visible++;

            nodes[i] = new NodeView(i, def, type, state, status, reported, valid, em,
                selected, ui.Hovered == i, match, focusing && focus[i],
                focusing ? focus[i] && !selected : directUp[i], !passFilters || (reported && !valid));
        }

        var edges = new EdgeView[g.Edges.Count];
        for (int e = 0; e < edges.Length; e++)
        {
            GraphEdge edge = g.Edges[e];
            Emphasis em = Max(nodes[edge.From].Emphasis, nodes[edge.To].Emphasis);
            bool onPath;
            if (focusing) onPath = edge.Directed && edge.Kind.Layering && focus[edge.FlowFrom] && focus[edge.FlowTo];
            else onPath = ui.Selected is int s1 && edge.Directed && edge.Kind.Layering && edge.FlowTo == s1;
            if (onPath && em > Emphasis.Context) em = Emphasis.Context;
            edges[e] = new EdgeView(edge, em, onPath, g.IsCrossLens(edge));
        }

        var active = new List<string>();
        if (ui.Lens is not null) active.Add("lens: " + c.Lens(ui.Lens).Name);
        if (ui.StateFilter.Count > 0) active.Add("state: " + string.Join(", ", ui.StateFilter.Select(x => c.TryState(x, out StateDef sd) ? sd.Name : x)));
        if (ageFilter is not null) active.Add("age: " + ageFilter.DisplayName);
        if (ui.TypeFilter is not null) active.Add("type: " + c.NodeType(ui.TypeFilter).Name);
        if (search.Length > 0) active.Add($"search: \"{search}\" ({matches})");
        if (focusing) active.Add("focus: " + ui.Focus.ToString().ToLowerInvariant());
        return new TreesView(nodes, edges, matches, visible, active);
    }

    private static Emphasis Max(Emphasis a, Emphasis b) => (Emphasis)System.Math.Max((int)a, (int)b);

    /// <summary>Does a node's Age span cover <paramref name="age"/>? Uses ageRange (open
    /// ended when "to" is absent) and ageRefs. Content without Age data is outside every Age.</summary>
    public static bool InAge(NodeDef def, AgeDef age, AgesDocument ages)
    {
        if (def.AgeRefs.Contains(age.Id)) return true;
        if (def.AgeFrom is null) return false;
        int from = ages.Age(def.AgeFrom).Order;
        int to = def.AgeTo is null ? int.MaxValue : ages.Age(def.AgeTo).Order;
        return age.Order >= from && age.Order <= to;
    }
}

/// <summary>
/// STATE → GLYPH. The binding the task calls "base node shape + domain glyph + progression
/// state + research state + maturity": the node type's base, the node's mark, the state's
/// ring, the wash chosen by the state's wash mode, the state's stage pips. Fractions are
/// quantised to 1/32 so a texture cache stays bounded however progress moves.
/// </summary>
public static class NodeVisuals
{
    public static GlyphSpec Glyph(NodeView v, SizeClass size) =>
        Glyph(v.Def, v.State, v.Status, size);

    public static GlyphSpec Glyph(NodeDef def, StateDef state, NodeStatus? status, SizeClass size)
    {
        double research = Quantise(status?.ResearchProgress ?? 0.0);
        double progress = Quantise(status?.RealizationProgress ?? 0.0);
        double wash = state.Wash switch
        {
            WashMode.None => 0.0,
            WashMode.Full => 1.0,
            WashMode.ResearchProgress => research,
            WashMode.RealizationProgress => progress,
            WashMode.MilestoneProgress => progress,
            _ => 0.0,
        };
        double arc = state.GlyphState is GlyphState.InProgress or GlyphState.Stalled
            ? (state.Track == StateTrack.Research ? research : progress)
            : 0.0;
        return new GlyphSpec(def.Base, state.GlyphState, size, Domain: def.Mark, Era: def.Era,
            Maturity: wash, Progress: arc, Stage: state.Stage);
    }

    /// <summary>The legend's exemplar for a state (a generic node of the given base).</summary>
    public static GlyphSpec Legend(StateDef state, GlyphBase shape, SizeClass size) =>
        new(shape, state.GlyphState, size,
            Maturity: state.Wash switch { WashMode.None => 0.0, WashMode.Full => 1.0, _ => 0.5 },
            Progress: state.GlyphState is GlyphState.InProgress or GlyphState.Stalled ? 0.5 : 0.0,
            Stage: state.Stage);

    public static double Quantise(double f) =>
        double.IsNaN(f) ? 0.0 : System.Math.Round(System.Math.Clamp(f, 0.0, 1.0) * 32.0) / 32.0;

    /// <summary>The size class for a glyph drawn <paramref name="pixels"/> wide on screen:
    /// the largest class not bigger than it, so detail follows the LOD contract.</summary>
    public static SizeClass SizeFor(double pixels) =>
        pixels >= 44 ? SizeClass.Px48 : pixels >= 30 ? SizeClass.Px32 : pixels >= 21 ? SizeClass.Px24 : SizeClass.Px16;
}
