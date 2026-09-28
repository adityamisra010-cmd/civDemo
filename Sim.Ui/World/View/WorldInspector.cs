using System.Globalization;
using Sim.Ui.World.Content;

namespace Sim.Ui.World.View;

/// <summary>Where a detail line's value came from.</summary>
public enum DetailTag
{
    /// <summary>Carried through from the source's report, unchanged.</summary>
    Reported = 0,
    /// <summary>A visual choice of the view (a stage, a slot, a cluster) — not a fact about the world.</summary>
    View = 1,
    /// <summary>The source does not report it.</summary>
    NotReported = 2,
}

public readonly record struct DetailLine(string Label, string Value, DetailTag Tag);

/// <summary>What the details panel shows for one entity.</summary>
public sealed record InspectorDetails(string Title, string Kind, string Provenance, IReadOnlyList<DetailLine> Lines, string Note);

/// <summary>
/// THE INSPECTOR — a pure function from the view and an id to the lines the details panel
/// prints. It reads REPORT fields and the view's own visual choices, tags each line with
/// which it is, and prints world coordinates only (never screen pixels, never a count the
/// level of detail aggregated). There is deliberately no line for strength, speed, morale or
/// any other resolution quantity: the view has none to show (task Part 12; DD-10, DD-06).
/// </summary>
public static class WorldInspector
{
    public static InspectorDetails? Details(WorldView view, WorldMorphology morph, WorldEntityId id)
    {
        string provenance = view.IsPlaceholder
            ? "DEMO / PLACEHOLDER - authored demonstration content, not simulation output"
            : "LIVE simulation - read-only";
        // A hidden entity is not seen: its details reveal nothing but that (a stale selection of
        // one that became hidden must not print its state).
        if (VisibilityOf(view, id) == ReportedVisibility.Hidden)
            return new InspectorDetails("Not visible", id.Kind.ToString(), provenance,
                [new DetailLine("Visibility", "hidden", DetailTag.Reported)], "");
        switch (id.Kind)
        {
            case WorldEntityKind.Settlement when view.FindSettlement(id) is SettlementView s:
            {
                var lines = new List<DetailLine>();
                Polity(lines, view, s.Report.PolityKey, s.Report.Relation);
                Input(lines, "Population", s.Report.Input("population"));
                if (s.Report.Input("sizeTier") is double tier) lines.Add(new("Size tier (simulation)", Morphology.Num(tier), DetailTag.Reported));
                if (s.Report.Input("dwellings") is double dw) lines.Add(new("Dwellings", Morphology.Num(dw), DetailTag.Reported));
                lines.Add(new("Visual stage", s.Stage.Name, DetailTag.View));
                lines.Add(new("Stage from", s.Stage.Explanation, DetailTag.View));
                int structures = 0; long total = 0;
                foreach (StructureView st in view.Structures)
                    if (st.Report.SettlementKey == s.Key && st.Drawable) { structures++; total += Math.Max(1, st.Report.Multiplicity); }
                lines.Add(new("Structures reported", structures == 0 ? "none" : $"{Morphology.Num(total)} in {structures} report(s)", DetailTag.Reported));
                Position(lines, s.Report.Position);
                Visibility(lines, s.Report.Visibility);
                return new InspectorDetails(s.Report.DisplayName, "Settlement", provenance, lines, s.Report.Note);
            }
            case WorldEntityKind.Structure when view.FindStructure(id) is StructureView st:
            {
                var lines = new List<DetailLine>
                {
                    new("Type", st.TypeKnown ? st.Type.Name : $"{st.Report.VisualType} (drawn as {st.Type.Name})", DetailTag.Reported),
                    new("Settlement", view.Settlement(st.Report.SettlementKey)?.Report.DisplayName ?? st.Report.SettlementKey, DetailTag.Reported),
                };
                Polity(lines, view, st.Report.PolityKey, st.Report.Relation);
                lines.Add(new("Visual stage", $"{st.Stage.Index + 1} of {st.Stage.Count} - {st.Stage.Name}", DetailTag.View));
                lines.Add(new("Stage from", st.Stage.Explanation, DetailTag.View));
                lines.Add(st.StateName is string state
                    ? new DetailLine("State", state, DetailTag.Reported)
                    : new DetailLine("State", "not reported", DetailTag.NotReported));
                lines.Add(st.Specialization is SpecializationMark sp
                    ? new DetailLine("Specialization", sp.Name, DetailTag.Reported)
                    : new DetailLine("Specialization", "not reported", DetailTag.NotReported));
                if (st.Report.Multiplicity != 1) lines.Add(new("Count in this report", Morphology.Num(st.Report.Multiplicity), DetailTag.Reported));
                foreach (ReportedInput i in st.Report.Inputs) lines.Add(new(i.Name, Morphology.Num(i.Value), DetailTag.Reported));
                foreach (ReportedLabel l in st.Report.Labels) lines.Add(new(l.Name, l.Value, DetailTag.Reported));
                lines.Add(st.Report.Established is long est
                    ? new DetailLine("First reported", view.IsPlaceholder ? $"demo step index {est}" : Morphology.Num(est), DetailTag.Reported)
                    : new DetailLine("First reported", "not reported", DetailTag.NotReported));
                if (st.ClusterKey is not null)
                    lines.Add(new("Drawn in", "a cluster token (provisional aggregation, D-038 H8)", DetailTag.View));
                lines.Add(new("Location", "a capability of its settlement - no map position of its own (D-038 H2)", DetailTag.View));
                Visibility(lines, st.Report.Visibility);
                return new InspectorDetails(st.Report.DisplayName, "Structure", provenance, lines, st.Report.Note);
            }
            case WorldEntityKind.Agent when view.FindAgent(id) is AgentView a:
            {
                var lines = new List<DetailLine> { new("Type", a.TypeKnown ? a.Type.Name : $"{a.Report.DisplayType} (drawn as {a.Type.Name})", DetailTag.Reported) };
                if (a.Report.Count is long n)
                    lines.Add(new(Title(a.CountNoun ?? "count"), n.ToString("#,0", CultureInfo.InvariantCulture), DetailTag.Reported));
                if (a.Report.Members.Count > 0) lines.Add(new("Members", string.Join(", ", a.Report.Members), DetailTag.Reported));
                Polity(lines, view, a.Report.PolityKey, a.Report.Relation);
                switch (a.Anchor)
                {
                    case AgentAnchor.Position: Position(lines, a.World); break;
                    case AgentAnchor.Graph:
                        lines.Add(new("Position", $"on {view.FindEdge(new WorldEntityId(WorldEntityKind.InfraEdge, a.Report.GraphLocation!.Value.EdgeKey))?.Report.DisplayName ?? a.Report.GraphLocation!.Value.EdgeKey} at {Morphology.Num(Math.Round(a.Report.GraphLocation!.Value.Fraction * 100))}%", DetailTag.Reported));
                        Position(lines, a.World, "Map point (projected)", DetailTag.View);
                        break;
                    case AgentAnchor.Settlement:
                        lines.Add(new("Position", $"at {view.Settlement(a.Report.AttachedSettlementKey!)?.Report.DisplayName ?? a.Report.AttachedSettlementKey} (no map position of its own)", DetailTag.Reported));
                        break;
                    default:
                        lines.Add(new("Position", "not resolvable - not drawn", DetailTag.NotReported));
                        break;
                }
                if (a.HeadingDeg is double h)
                {
                    double shown = Math.Round(h, 1);
                    if (shown >= 360.0) shown = 0.0;   // 359.96 is shown as 0, never as 360
                    lines.Add(new("Heading", Morphology.Num(shown) + " deg (display only)", DetailTag.Reported));
                }
                Visibility(lines, a.Report.Visibility);
                return new InspectorDetails(a.Report.DisplayName, a.Type.Category == "military" ? "Military formation" : a.Type.Category == "group" ? "Group" : "Person",
                    provenance, lines, a.Report.Note);
            }
            case WorldEntityKind.Resource when view.FindResource(id) is ResourceView r:
            {
                var lines = new List<DetailLine>
                {
                    new("Resource", r.Type?.Name ?? r.Report.ResourceType, DetailTag.Reported),
                    new("Settlement", view.Settlement(r.Report.SettlementKey)?.Report.DisplayName ?? r.Report.SettlementKey, DetailTag.Reported),
                };
                foreach (ReportedInput i in r.Report.Inputs) lines.Add(new(i.Name, Morphology.Num(Math.Round(i.Value, 3)), DetailTag.Reported));
                Visibility(lines, r.Report.Visibility);
                return new InspectorDetails(r.Report.DisplayName, "Resource", provenance, lines, r.Report.Note);
            }
            case WorldEntityKind.InfraNode when view.FindNode(id) is NodeView nv:
            {
                var lines = new List<DetailLine> { new("Node type", nv.Type?.Name ?? nv.Report.NodeType, DetailTag.Reported) };
                if (nv.Report.SettlementKey is string sk) lines.Add(new("Settlement", view.Settlement(sk)?.Report.DisplayName ?? sk, DetailTag.Reported));
                Position(lines, nv.Report.Position);
                Visibility(lines, nv.Report.Visibility);
                return new InspectorDetails(nv.Report.DisplayName, "Infrastructure node", provenance, lines, nv.Report.Note);
            }
            case WorldEntityKind.InfraEdge when view.FindEdge(id) is EdgeView e:
            {
                var lines = new List<DetailLine>
                {
                    new("Mode", e.TypeKnown ? e.Type.Name : e.Report.VisualType, DetailTag.Reported),
                    new("Visual stage", e.StageChoice.Name, DetailTag.View),
                    new("Stage from", e.StageChoice.Explanation, DetailTag.View),
                };
                foreach (ReportedInput i in e.Report.Inputs) lines.Add(new(i.Name, Morphology.Num(i.Value), DetailTag.Reported));
                Visibility(lines, e.Report.Visibility);
                return new InspectorDetails(e.Report.DisplayName, "Infrastructure edge", provenance, lines, e.Report.Note);
            }
        }
        return null;
    }

    private static ReportedVisibility? VisibilityOf(WorldView view, WorldEntityId id) => id.Kind switch
    {
        WorldEntityKind.Settlement => view.FindSettlement(id)?.Report.Visibility,
        WorldEntityKind.Structure => view.FindStructure(id) is StructureView s
            ? view.Settlement(s.Report.SettlementKey)?.Report.Visibility == ReportedVisibility.Hidden ? ReportedVisibility.Hidden : s.Report.Visibility
            : null,
        WorldEntityKind.InfraNode => view.FindNode(id)?.Report.Visibility,
        WorldEntityKind.InfraEdge => view.FindEdge(id)?.Report.Visibility,
        WorldEntityKind.Resource => view.FindResource(id) is ResourceView r
            ? view.Settlement(r.Report.SettlementKey)?.Report.Visibility == ReportedVisibility.Hidden ? ReportedVisibility.Hidden : r.Report.Visibility
            : null,
        _ => view.FindAgent(id)?.Report.Visibility,
    };

    private static string Title(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    private static void Input(List<DetailLine> lines, string label, double? v) =>
        lines.Add(v is double x ? new DetailLine(label, Morphology.Num(x), DetailTag.Reported) : new DetailLine(label, "not reported", DetailTag.NotReported));

    private static void Polity(List<DetailLine> lines, WorldView view, string? key, PolityRelation relation)
    {
        string label = relation switch
        {
            PolityRelation.Controller => "Controller",
            PolityRelation.Allegiance => "Allegiance",
            PolityRelation.Owner => "Owner",
            _ => "Polity",
        };
        if (key is null) { lines.Add(new(label, "not reported", DetailTag.NotReported)); return; }
        lines.Add(new(label, view.Polity(key)?.DisplayName ?? key, DetailTag.Reported));
    }

    private static void Position(List<DetailLine> lines, WorldPoint p, string label = "Position", DetailTag tag = DetailTag.Reported) =>
        lines.Add(new(label, $"({p.X.ToString("0.00", CultureInfo.InvariantCulture)}, {p.Y.ToString("0.00", CultureInfo.InvariantCulture)}) world units", tag));

    private static void Visibility(List<DetailLine> lines, ReportedVisibility v) =>
        lines.Add(v switch
        {
            ReportedVisibility.NotModelled => new DetailLine("Visibility", "not modelled: omniscient view", DetailTag.NotReported),
            ReportedVisibility.Remembered => new DetailLine("Visibility", "remembered (last known)", DetailTag.Reported),
            ReportedVisibility.Hidden => new DetailLine("Visibility", "hidden", DetailTag.Reported),
            _ => new DetailLine("Visibility", "visible", DetailTag.Reported),
        });
}
