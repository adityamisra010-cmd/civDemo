using System.Globalization;
using Sim.Ui.Art.Glyphs;
using Sim.Ui.World.Content;

namespace Sim.Ui.World.View;

/// <summary>
/// THE VIEW BUILDER — a pure function from one bundle's snapshots and the visual morphology
/// to a <see cref="WorldView"/>. It reads reports and content; it writes nothing, mints no
/// entity, and infers no existence: every settlement, structure, edge, resource and agent in
/// the view is one the source reported, and every aggregate is a count OF reports.
///
/// Order of operations (fixed, docs §6): stage → composition slots over ALL of a settlement's
/// reports whatever their visibility → visibility filter → (in the scene) level of detail and
/// viewport. Later steps only filter; none re-places, so no filter can move a building.
/// </summary>
public static class WorldViewBuilder
{
    public static WorldView Build(WorldSources sources, WorldMorphology morph)
    {
        IReadOnlyList<SettlementReport> settlementReports = sources.Settlements.Current.Items;
        IReadOnlyList<PolityReport> polities = sources.Polities.Current.Items;
        IReadOnlyList<StructureReport> structureReports = sources.Structures.Current.Items;
        IReadOnlyList<InfraNodeReport> nodeReports = sources.Infrastructure.Nodes.Items;
        IReadOnlyList<InfraEdgeReport> edgeReports = sources.Infrastructure.Edges.Items;
        IReadOnlyList<ResourceReport> resourceReports = sources.Resources.Current.Items;
        IReadOnlyList<AgentReport> agentReports = sources.Agents.Current.Items;
        var notes = new List<string>();

        var polityByKey = new Dictionary<string, PolityReport>(StringComparer.Ordinal);
        foreach (PolityReport p in polities) polityByKey[p.Key] = p;
        string? Ink(string? key) => key is not null && polityByKey.TryGetValue(key, out PolityReport? p) ? morph.PolityInk(p.InkSeed) : null;
        string? PolityName(string? key) => key is not null && polityByKey.TryGetValue(key, out PolityReport? p) ? p.DisplayName : null;

        // --- structures, grouped by settlement (lookup only; every iteration below is over sorted lists)
        var bySettlement = new Dictionary<string, List<StructureReport>>(StringComparer.Ordinal);
        foreach (StructureReport r in structureReports)
        {
            if (!bySettlement.TryGetValue(r.SettlementKey, out List<StructureReport>? list))
                bySettlement[r.SettlementKey] = list = [];
            list.Add(r);
        }
        var settlementKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (SettlementReport s in settlementReports) settlementKeys.Add(s.Key);

        var structureViews = new Dictionary<string, StructureView>(StringComparer.Ordinal);
        var clusters = new List<ClusterView>();
        var settlements = new List<SettlementView>(settlementReports.Count);

        foreach (SettlementReport s in settlementReports)
        {
            StageChoice stage = Morphology.Settlement(s, morph.Settlements);
            List<StructureReport> own = bySettlement.TryGetValue(s.Key, out List<StructureReport>? l) ? l : [];

            // Group by visual type; types in ordinal order, reports in composition priority.
            var typeIds = new List<string>();
            var byType = new Dictionary<string, List<StructureReport>>(StringComparer.Ordinal);
            foreach (StructureReport r in own)
            {
                string t = ResolveType(r.VisualType, morph).Id;
                if (!byType.TryGetValue(t, out List<StructureReport>? tl)) { byType[t] = tl = []; typeIds.Add(t); }
                tl.Add(r);
            }
            typeIds.Sort(StringComparer.Ordinal);

            var claims = new List<Composition.Claim>();
            var claimOwner = new List<(StructureReport? Report, string? ClusterKey, VisualType Type, List<StructureReport>? Members)>();
            foreach (string t in typeIds)
            {
                List<StructureReport> reports = byType[t];
                reports.Sort(ComparePriority);
                VisualType type = morph.Visual(t)!;
                int individual = Math.Min(reports.Count, morph.Aggregation.IndividualUpTo);
                for (int i = 0; i < individual; i++)
                {
                    claims.Add(new Composition.Claim(reports[i].Key, reports[i].Established, reports[i].Key));
                    claimOwner.Add((reports[i], null, type, null));
                }
                if (reports.Count > individual)
                {
                    StructureReport first = reports[individual];
                    string clusterKey = s.Key + "/cluster:" + t;
                    claims.Add(new Composition.Claim("cluster:" + t, first.Established, first.Key));
                    claimOwner.Add((null, clusterKey, type, reports.GetRange(individual, reports.Count - individual)));
                }
            }

            LotGeometry[] slots = Composition.AssignSlots(s.Key, claims, morph.Layout);
            var occupied = new List<LotGeometry>(slots.Length);
            for (int c = 0; c < claims.Count; c++)
            {
                (StructureReport? report, string? clusterKey, VisualType type, List<StructureReport>? members) = claimOwner[c];
                if (report is not null)
                {
                    structureViews[report.Key] = StructureViewOf(report, morph, slots[c], null, Ink(report.PolityKey), true);
                    if (report.Visibility != ReportedVisibility.Hidden) occupied.Add(slots[c]);
                }
                else
                {
                    var ids = new List<WorldEntityId>();
                    long total = 0;
                    bool anyShown = false;
                    foreach (StructureReport m in members!)
                    {
                        structureViews[m.Key] = StructureViewOf(m, morph, null, clusterKey, Ink(m.PolityKey), true);
                        if (m.Visibility == ReportedVisibility.Hidden) continue;
                        ids.Add(new WorldEntityId(WorldEntityKind.Structure, m.Key));
                        total += Math.Max(1, m.Multiplicity);
                        anyShown = true;
                    }
                    if (anyShown)
                    {
                        clusters.Add(new ClusterView(clusterKey!, s.Key, type, slots[c], ids, total,
                            new GlyphSpec(type.Glyph.Base, GlyphState.Complete, SizeClass.Px32, type.Glyph.Mark, Placement: Placement.Map)));
                        occupied.Add(slots[c]);
                    }
                }
            }

            int blocks = morph.Settlements.Stages[stage.Index].Blocks;
            LotGeometry[] visibleBlocks = Composition.VisibleBlocks(s.Key, blocks, occupied, morph.Layout);
            settlements.Add(new SettlementView(new WorldEntityId(WorldEntityKind.Settlement, s.Key), s, Ink(s.PolityKey),
                PolityName(s.PolityKey), stage, visibleBlocks, s.Visibility != ReportedVisibility.Hidden));
        }

        // Structures whose settlement was not reported: kept (the report exists) but not drawable.
        var structures = new List<StructureView>(structureReports.Count);
        int orphans = 0;
        foreach (StructureReport r in structureReports)
        {
            if (structureViews.TryGetValue(r.Key, out StructureView? v)) { structures.Add(v); continue; }
            orphans++;
            structures.Add(StructureViewOf(r, morph, null, null, Ink(r.PolityKey), false));
        }
        if (orphans > 0) notes.Add($"{orphans} structure report(s) name a settlement that is not reported: not drawn");

        // --- infrastructure graph
        var nodes = new List<NodeView>(nodeReports.Count);
        var nodePos = new Dictionary<string, (WorldPoint P, ReportedVisibility V)>(StringComparer.Ordinal);
        foreach (InfraNodeReport n in nodeReports)
        {
            nodes.Add(new NodeView(new WorldEntityId(WorldEntityKind.InfraNode, n.Key), n, morph.Node(n.NodeType),
                n.Visibility != ReportedVisibility.Hidden));
            nodePos[n.Key] = (n.Position, n.Visibility);
        }
        var edges = new List<EdgeView>(edgeReports.Count);
        var edgeEnds = new Dictionary<string, (WorldPoint A, WorldPoint B, bool Ok)>(StringComparer.Ordinal);
        int dangling = 0;
        foreach (InfraEdgeReport e in edgeReports)
        {
            InfraType? known = morph.Infra(e.VisualType);
            InfraType type = known ?? morph.InfraTypes[0];
            (InfraStage st, StageChoice choice) = Morphology.Edge(e, type);
            bool ok = nodePos.TryGetValue(e.NodeA, out var a) & nodePos.TryGetValue(e.NodeB, out var b);
            if (!ok) dangling++;
            edges.Add(new EdgeView(new WorldEntityId(WorldEntityKind.InfraEdge, e.Key), e, type, known is not null, st, choice,
                a.P, b.P, ok && e.Visibility != ReportedVisibility.Hidden));
            edgeEnds[e.Key] = (a.P, b.P, ok);
        }
        if (dangling > 0) notes.Add($"{dangling} edge report(s) name a node that is not reported: not drawn");

        // --- resources, ordered within their settlement by key
        var resources = new List<ResourceView>(resourceReports.Count);
        var resourceCount = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (ResourceReport r in resourceReports)
        {
            int idx = resourceCount.TryGetValue(r.SettlementKey, out int c) ? c : 0;
            resourceCount[r.SettlementKey] = idx + 1;
            bool drawable = settlementKeys.Contains(r.SettlementKey) && r.Visibility != ReportedVisibility.Hidden;
            resources.Add(new ResourceView(new WorldEntityId(WorldEntityKind.Resource, r.Key), r, morph.Resource(r.ResourceType), idx, drawable));
        }

        // --- agents: one token each, anchored as reported
        var agents = new List<AgentView>(agentReports.Count);
        var attachCount = new Dictionary<string, int>(StringComparer.Ordinal);
        var settlementPos = new Dictionary<string, WorldPoint>(StringComparer.Ordinal);
        foreach (SettlementReport s in settlementReports) settlementPos[s.Key] = s.Position;
        int unresolved = 0;
        foreach (AgentReport r in agentReports)
        {
            AgentType? known = morph.Agent(r.DisplayType);
            AgentType type = known ?? morph.Agent(WorldViewConstants.FallbackAgentType)!;
            AgentAnchor anchor = AgentAnchor.Unresolved;
            WorldPoint world = default;
            int attach = 0;
            if (r.Position is WorldPoint p && double.IsFinite(p.X) && double.IsFinite(p.Y)) { anchor = AgentAnchor.Position; world = p; }
            else if (r.GraphLocation is AgentGraphLocation g && edgeEnds.TryGetValue(g.EdgeKey, out var ends) && ends.Ok)
            {
                double f = double.IsFinite(g.Fraction) ? Math.Clamp(g.Fraction, 0, 1) : 0;
                anchor = AgentAnchor.Graph;
                world = new WorldPoint(ends.A.X + (ends.B.X - ends.A.X) * f, ends.A.Y + (ends.B.Y - ends.A.Y) * f);
            }
            else if (r.AttachedSettlementKey is string sk && settlementPos.TryGetValue(sk, out WorldPoint sp))
            {
                anchor = AgentAnchor.Settlement;
                world = sp;
                attach = attachCount.TryGetValue(sk, out int c) ? c : 0;
                attachCount[sk] = attach + 1;
            }
            if (anchor == AgentAnchor.Unresolved) unresolved++;
            string? countLabel = r.Count is long n ? n.ToString("#,0", CultureInfo.InvariantCulture) : null;
            double? heading = r.HeadingDeg is double h && double.IsFinite(h) ? NormalizeHeading(h) : null;
            agents.Add(new AgentView(new WorldEntityId(WorldEntityKind.Agent, r.Key), r, type, known is not null, Ink(r.PolityKey),
                anchor, world, attach, countLabel, r.CountNoun ?? type.CountNoun, heading,
                anchor != AgentAnchor.Unresolved && r.Visibility != ReportedVisibility.Hidden));
        }
        if (unresolved > 0) notes.Add($"{unresolved} agent report(s) have no resolvable location: not drawn (never placed at a default)");

        return new WorldView(sources.Label, sources.IsPlaceholder, sources.Observer, polities, settlements, structures, clusters,
            nodes, edges, resources, agents, Summary(structures, morph), notes);
    }

    /// <summary>A heading in [0, 360): 270, -90 and 630 are the same heading and draw identically.</summary>
    public static double NormalizeHeading(double deg)
    {
        double h = deg % 360.0;
        if (h < 0) h += 360.0;
        return h >= 360.0 ? 0.0 : h;
    }

    public static VisualType ResolveType(string reported, WorldMorphology morph) =>
        morph.Visual(reported)
        ?? (morph.LiveVisualType(reported) is string mapped ? morph.Visual(mapped) : null)
        ?? morph.Visual(WorldViewConstants.FallbackVisualType)!;

    /// <summary>Composition priority: established ascending (unreported last), then key ordinal.</summary>
    public static int ComparePriority(StructureReport a, StructureReport b)
    {
        int c = (a.Established ?? long.MaxValue).CompareTo(b.Established ?? long.MaxValue);
        return c != 0 ? c : string.CompareOrdinal(a.Key, b.Key);
    }

    private static StructureView StructureViewOf(StructureReport r, WorldMorphology morph, LotGeometry? slot, string? clusterKey,
        string? ink, bool settlementKnown)
    {
        VisualType type = ResolveType(r.VisualType, morph);
        bool known = morph.Visual(r.VisualType) is not null
            || (morph.LiveVisualType(r.VisualType) is string mapped && morph.Visual(mapped) is not null);
        StageChoice stage = Morphology.Structure(r, type);
        StateStyle? state = r.State is string sid ? morph.State(sid) : null;
        SpecializationMark? spec = r.Specialization is string spid ? morph.Specialization(spid) : null;
        var icon = new GlyphSpec(type.Glyph.Base, state?.GlyphState ?? GlyphState.Complete, SizeClass.Px32,
            spec?.Mark ?? type.Glyph.Mark, Placement: Placement.Map, Stage: Math.Min(stage.Index + 1, GlyphSpec.MaxStage));
        return new StructureView(new WorldEntityId(WorldEntityKind.Structure, r.Key), r, type, known, stage, icon, state?.Name, spec,
            ink, slot, clusterKey, settlementKnown && r.Visibility != ReportedVisibility.Hidden);
    }

    /// <summary>The civilization summary: per visual type, the SUM of reported multiplicities of
    /// drawable structures, and in how many settlements. Content order, then unknown ids ordinal.</summary>
    private static List<SummaryLine> Summary(IReadOnlyList<StructureView> structures, WorldMorphology morph)
    {
        var lines = new List<SummaryLine>();
        foreach (VisualType t in morph.VisualTypes)
        {
            long total = 0;
            var places = new HashSet<string>(StringComparer.Ordinal);
            foreach (StructureView s in structures)
            {
                if (!s.Drawable || !ReferenceEquals(s.Type, t)) continue;
                total += Math.Max(1, s.Report.Multiplicity);
                places.Add(s.Report.SettlementKey);
            }
            if (total > 0) lines.Add(new SummaryLine(t.Id, t.Name, total, places.Count));
        }
        return lines;
    }
}
