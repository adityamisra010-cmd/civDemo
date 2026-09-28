using System.Text.Json;
using Sim.Ui.Art.Glyphs;
using Sim.Ui.Render;
using TreesJsonOptions = Sim.Ui.Trees.Json.TreesJsonOptions;
using Sim.Ui.World.Content.Json;
using Sim.Ui.World.View;

namespace Sim.Ui.World.Content;

/// <summary>One problem found in world content, with the file and the JSON path.</summary>
public sealed record WorldDiagnostic(string File, string Path, string Message)
{
    public override string ToString() => $"{File} {Path}: {Message}";
}

/// <summary>
/// THE STRICT LOADER for ui-content/world/morphology.json. Malformed content is a list of
/// diagnostics, never a crash and never a silently partial morphology: unknown members,
/// duplicate keys, nulls in lists, unknown glyph names, non-ascending thresholds, parts
/// outside their lot, overlapping composition slots, date- or turn-keyed stage inputs and
/// non-Latin-1 text are all rejected with a path.
/// </summary>
public static class WorldContentLoader
{
    public const string Schema = "civ-sim/world-morphology@1";
    public const string MorphologyFile = "morphology.json";
    public const string DemoFile = "demo-world.json";

    public static string DefaultDirectory => Path.Combine(AppContext.BaseDirectory, "ui-content", "world");

    /// <summary>Stage inputs that would key a visual stage to time rather than reported
    /// state (CLAUDE.md law 4; CR-001 closed time-keyed maturation) — rejected by name.</summary>
    public static readonly string[] ForbiddenStageInputs = ["established", "turn", "year", "date", "age", "era", "elapsed"];

    public static (WorldMorphology? Morphology, IReadOnlyList<WorldDiagnostic> Diagnostics) LoadMorphologyFile(string path)
    {
        if (!File.Exists(path)) return (null, [new WorldDiagnostic(MorphologyFile, "$", $"file not found: {path}")]);
        return LoadMorphology(File.ReadAllText(path));
    }

    public static (WorldMorphology? Morphology, IReadOnlyList<WorldDiagnostic> Diagnostics) LoadMorphology(string json)
    {
        var d = new Diags(MorphologyFile);
        MorphologyDto? dto = Parse<MorphologyDto>(json, d);
        if (dto is null) return (null, d.List);

        if (dto.Schema != Schema) d.Error("schema", $"expected \"{Schema}\", found \"{dto.Schema}\"");
        string notice = d.Text(dto.Notice, "notice");

        SpriteScale? sprite = null;
        if (dto.Sprite is null) d.Error("sprite", "missing");
        else
        {
            double wpu = d.Positive(dto.Sprite.WorldPerUnit, "sprite.worldPerUnit");
            double min = d.Positive(dto.Sprite.MinPxPerUnit, "sprite.minPxPerUnit");
            double max = d.Positive(dto.Sprite.MaxPxPerUnit, "sprite.maxPxPerUnit");
            if (min >= max) d.Error("sprite", "minPxPerUnit must be below maxPxPerUnit");
            sprite = new SpriteScale(wpu, min, max);
        }

        LodThresholds? lod = null;
        if (dto.Lod is null) d.Error("lod", "missing");
        else
        {
            double mid = d.Positive(dto.Lod.MidFromPxPerUnit, "lod.midFromPxPerUnit");
            double near = d.Positive(dto.Lod.NearFromPxPerUnit, "lod.nearFromPxPerUnit");
            if (mid >= near) d.Error("lod", "midFromPxPerUnit must be below nearFromPxPerUnit");
            lod = new LodThresholds(mid, near);
        }

        CompositionLayout? layout = dto.Layout is null ? null : Layout(dto.Layout, d);
        if (dto.Layout is null) d.Error("layout", "missing");

        SettlementStages? stages = null;
        if (dto.SettlementStages is null) d.Error("settlementStages", "missing");
        else stages = Stages(dto.SettlementStages, d);

        Aggregation? aggregation = null;
        if (dto.Aggregation is null) d.Error("aggregation", "missing");
        else
        {
            int n = dto.Aggregation.IndividualUpTo ?? 0;
            if (n < 1) d.Error("aggregation.individualUpTo", "must be at least 1");
            aggregation = new Aggregation(n, d.Text(dto.Aggregation.Provisional, "aggregation.provisional"));
        }

        var visuals = new List<VisualType>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < (dto.VisualTypes?.Count ?? 0); i++)
        {
            VisualTypeDto v = dto.VisualTypes![i];
            string p = $"visualTypes[{i}]";
            string id = d.Id(v.Id, p + ".id", ids);
            string name = d.Text(v.Name, p + ".name");
            string category = d.OneOf(v.Category, p + ".category", "institution", "building", "works");
            WorldGlyph glyph = d.Glyph(v.Glyph, p + ".glyph");
            StageRule rule = d.Rule(v.StageBy, p + ".stageBy");
            var mstages = new List<MorphStage>();
            double prev = double.NegativeInfinity;
            for (int s = 0; s < (v.Stages?.Count ?? 0); s++)
            {
                MorphStageDto st = v.Stages![s];
                string sp = $"{p}.stages[{s}]";
                double min = d.Finite(st.Min, sp + ".min");
                if (s == 0 && min != 0) d.Error(sp + ".min", "the first stage's min must be 0");
                if (s > 0 && !(min > prev)) d.Error(sp + ".min", "stage mins must be strictly ascending");
                prev = min;
                var parts = new List<MorphPart>();
                for (int k = 0; k < (st.Adds?.Count ?? 0); k++)
                    parts.Add(d.Part(st.Adds![k], $"{sp}.adds[{k}]"));
                if (parts.Count == 0) d.Error(sp + ".adds", "a stage must add at least one part (stages are cumulative)");
                mstages.Add(new MorphStage(d.Text(st.Name, sp + ".name"), min, parts));
            }
            if (mstages.Count == 0) d.Error(p + ".stages", "at least one stage is required");
            visuals.Add(new VisualType(id, name, category, glyph, rule, mstages, d.Text(v.Note, p + ".note")));
        }
        if (visuals.Count == 0) d.Error("visualTypes", "at least one visual type is required");

        var specs = new List<SpecializationMark>();
        ids.Clear();
        for (int i = 0; i < (dto.Specializations?.Count ?? 0); i++)
        {
            SpecializationDto s = dto.Specializations![i];
            string p = $"specializations[{i}]";
            specs.Add(new SpecializationMark(d.Id(s.Id, p + ".id", ids), d.Text(s.Name, p + ".name"), d.Enum<GlyphDomain>(s.Mark, p + ".mark")));
        }

        var states = new List<StateStyle>();
        ids.Clear();
        for (int i = 0; i < (dto.States?.Count ?? 0); i++)
        {
            StateStyleDto s = dto.States![i];
            string p = $"states[{i}]";
            states.Add(new StateStyle(d.Id(s.Id, p + ".id", ids), d.Text(s.Name, p + ".name"), d.Enum<GlyphState>(s.GlyphState, p + ".glyphState")));
        }

        var infra = new List<InfraType>();
        ids.Clear();
        for (int i = 0; i < (dto.InfrastructureTypes?.Count ?? 0); i++)
        {
            InfraTypeDto t = dto.InfrastructureTypes![i];
            string p = $"infrastructureTypes[{i}]";
            string id = d.Id(t.Id, p + ".id", ids);
            StageRule rule = d.Rule(t.StageBy, p + ".stageBy");
            var ist = new List<InfraStage>();
            double prev = double.NegativeInfinity;
            for (int s = 0; s < (t.Stages?.Count ?? 0); s++)
            {
                InfraStageDto st = t.Stages![s];
                string sp = $"{p}.stages[{s}]";
                double min = d.Finite(st.Min, sp + ".min");
                if (s == 0 && min != 0) d.Error(sp + ".min", "the first stage's min must be 0");
                if (s > 0 && !(min > prev)) d.Error(sp + ".min", "stage mins must be strictly ascending");
                prev = min;
                StrokeKind stroke = d.OneOf(st.Stroke, sp + ".stroke", "solid", "dashed", "dotted") switch
                {
                    "dashed" => StrokeKind.Dashed,
                    "dotted" => StrokeKind.Dotted,
                    _ => StrokeKind.Solid,
                };
                ist.Add(new InfraStage(d.Text(st.Name, sp + ".name"), min, d.Positive(st.WidthPx, sp + ".widthPx"), stroke,
                    d.Ink(st.Ink, sp + ".ink")));
            }
            if (ist.Count == 0) d.Error(p + ".stages", "at least one stage is required");
            infra.Add(new InfraType(id, d.Text(t.Name, p + ".name"), rule, ist));
        }

        var nodes = new List<NodeType>();
        ids.Clear();
        for (int i = 0; i < (dto.NodeTypes?.Count ?? 0); i++)
        {
            NodeTypeDto t = dto.NodeTypes![i];
            string p = $"nodeTypes[{i}]";
            nodes.Add(new NodeType(d.Id(t.Id, p + ".id", ids), d.Text(t.Name, p + ".name"),
                t.Glyph is null ? null : d.Glyph(t.Glyph, p + ".glyph")));
        }

        var resources = new List<ResourceType>();
        ids.Clear();
        for (int i = 0; i < (dto.ResourceTypes?.Count ?? 0); i++)
        {
            ResourceTypeDto t = dto.ResourceTypes![i];
            string p = $"resourceTypes[{i}]";
            resources.Add(new ResourceType(d.Id(t.Id, p + ".id", ids), d.Text(t.Name, p + ".name"), d.Enum<GlyphDomain>(t.Mark, p + ".mark")));
        }

        var agents = new List<AgentType>();
        ids.Clear();
        for (int i = 0; i < (dto.AgentTypes?.Count ?? 0); i++)
        {
            AgentTypeDto t = dto.AgentTypes![i];
            string p = $"agentTypes[{i}]";
            string id = d.Id(t.Id, p + ".id", ids);
            string cat = d.OneOf(t.Category, p + ".category", "military", "person", "group");
            WorldGlyph g = d.Glyph(t.Glyph, p + ".glyph");
            // The silhouette family follows the category, so the three read apart at a glance.
            GlyphBase want = cat switch { "military" => GlyphBase.Standard, "person" => GlyphBase.Emblem, _ => GlyphBase.Node };
            if (g.Base != want) d.Error(p + ".glyph.base", $"a {cat} agent is drawn on the {want} base");
            if (t.CountNoun is not null) d.Text(t.CountNoun, p + ".countNoun");
            agents.Add(new AgentType(id, d.Text(t.Name, p + ".name"), cat, g, t.CountNoun));
        }

        var bands = new List<SizeBand>();
        long prevBand = long.MinValue;
        for (int i = 0; i < (dto.AgentSizeBands?.Count ?? 0); i++)
        {
            SizeBandDto b = dto.AgentSizeBands![i];
            string p = $"agentSizeBands[{i}]";
            long min = b.Min ?? -1;
            if (i == 0 && min != 0) d.Error(p + ".min", "the first band's min must be 0");
            if (i > 0 && min <= prevBand) d.Error(p + ".min", "band mins must be strictly ascending");
            prevBand = min;
            bands.Add(new SizeBand(min, d.Positive(b.Scale, p + ".scale")));
        }

        var inks = new List<string>();
        for (int i = 0; i < (dto.PolityInks?.Count ?? 0); i++) inks.Add(d.Ink(dto.PolityInks![i], $"polityInks[{i}]"));
        if (inks.Count == 0) d.Error("polityInks", "at least one ink is required");

        var live = new List<LiveProjectMap>();
        ids.Clear();
        for (int i = 0; i < (dto.LiveProjects?.Count ?? 0); i++)
        {
            LiveProjectDto m = dto.LiveProjects![i];
            string p = $"liveProjects[{i}]";
            string project = d.Id(m.Project, p + ".project", ids);
            string vt = d.Text(m.VisualType, p + ".visualType");
            if (!visuals.Exists(v => v.Id == vt)) d.Error(p + ".visualType", $"unknown visual type '{vt}'");
            live.Add(new LiveProjectMap(project, vt));
        }
        if (!visuals.Exists(v => v.Id == WorldViewConstants.FallbackVisualType))
            d.Error("visualTypes", $"a '{WorldViewConstants.FallbackVisualType}' fallback visual type is required");
        if (!agents.Exists(a => a.Id == WorldViewConstants.FallbackAgentType))
            d.Error("agentTypes", $"a '{WorldViewConstants.FallbackAgentType}' fallback agent type is required");

        if (d.HasErrors || sprite is null || lod is null || layout is null || stages is null || aggregation is null)
            return (null, d.List);
        return (new WorldMorphology(notice, sprite, lod, layout, stages, aggregation, visuals, specs, states, infra, nodes,
            resources, agents, bands, inks, live), d.List);
    }

    private static CompositionLayout Layout(LayoutDto l, Diags d)
    {
        double plaza = d.Positive(l.PlazaRadius, "layout.plazaRadius");
        double slot = d.Positive(l.SlotSize, "layout.slotSize");
        double block = d.Positive(l.BlockSize, "layout.blockSize");
        List<LotRing> slots = Rings(l.SlotRings, "layout.slotRings", d);
        List<LotRing> blocks = Rings(l.BlockRings, "layout.blockRings", d);
        if (slots.Count == 0) d.Error("layout.slotRings", "at least one ring is required");
        if (blocks.Count == 0) d.Error("layout.blockRings", "at least one ring is required");
        var layout = new CompositionLayout(plaza, slot, slots, block, blocks);
        if (d.HasErrors) return layout;

        // The lattice is FIXED; check it once here so no stage can ever collide at draw time.
        LotGeometry[] lots = CompositionGeometry.SlotCentres(layout);
        for (int i = 0; i < lots.Length; i++)
        {
            if (!CompositionGeometry.BoxClearsCircle(lots[i].X, lots[i].Y, slot, plaza))
                d.Error("layout.slotRings", $"slot (ring {lots[i].Ring}, lot {lots[i].Index}) overlaps the plaza");
            for (int j = i + 1; j < lots.Length; j++)
                if (CompositionGeometry.BoxesOverlap(lots[i].X, lots[i].Y, slot, lots[j].X, lots[j].Y, slot))
                    d.Error("layout.slotRings", $"slots (ring {lots[i].Ring}, lot {lots[i].Index}) and (ring {lots[j].Ring}, lot {lots[j].Index}) overlap");
        }
        return layout;
    }

    private static List<LotRing> Rings(List<RingDto>? rings, string path, Diags d)
    {
        var list = new List<LotRing>();
        double prev = 0;
        for (int i = 0; i < (rings?.Count ?? 0); i++)
        {
            RingDto r = rings![i];
            string p = $"{path}[{i}]";
            double radius = d.Positive(r.Radius, p + ".radius");
            if (radius <= prev) d.Error(p + ".radius", "ring radii must be strictly ascending");
            prev = radius;
            int lots = r.Lots ?? 0;
            if (lots < 1) d.Error(p + ".lots", "must be at least 1");
            list.Add(new LotRing(radius, Math.Max(1, lots), d.Finite(r.OffsetDeg, p + ".offsetDeg")));
        }
        return list;
    }

    private static SettlementStages Stages(SettlementStagesDto s, Diags d)
    {
        var stages = new List<SettlementStage>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        int prevBlocks = -1;
        for (int i = 0; i < (s.Stages?.Count ?? 0); i++)
        {
            SettlementStageDto st = s.Stages![i];
            string p = $"settlementStages.stages[{i}]";
            int blocks = st.Blocks ?? -1;
            if (blocks < 0) d.Error(p + ".blocks", "missing or negative");
            if (blocks < prevBlocks) d.Error(p + ".blocks", "a larger stage may not show fewer blocks (the footprint grows)");
            prevBlocks = blocks;
            stages.Add(new SettlementStage(d.Id(st.Id, p + ".id", ids), d.Text(st.Name, p + ".name"), Math.Max(0, blocks)));
        }
        if (stages.Count == 0) d.Error("settlementStages.stages", "at least one stage is required");

        var drivers = new List<StageDriver>();
        ids.Clear();
        for (int i = 0; i < (s.Drivers?.Count ?? 0); i++)
        {
            DriverDto dr = s.Drivers![i];
            string p = $"settlementStages.drivers[{i}]";
            string input = d.Id(dr.Input, p + ".input", ids);
            d.StageInputAllowed(input, p + ".input");
            string prov = d.OneOf(dr.Provenance, p + ".provenance", "simulation", "demonstration");
            var th = dr.Thresholds ?? [];
            if (th.Count != stages.Count) d.Error(p + ".thresholds", $"needs one threshold per stage ({stages.Count})");
            for (int k = 0; k < th.Count; k++)
            {
                if (!double.IsFinite(th[k])) d.Error($"{p}.thresholds[{k}]", "must be finite");
                if (k == 0 && th[k] != 0) d.Error($"{p}.thresholds[0]", "the first threshold must be 0");
                if (k > 0 && !(th[k] > th[k - 1])) d.Error($"{p}.thresholds[{k}]", "thresholds must be strictly ascending");
            }
            drivers.Add(new StageDriver(input, prov, dr.NamedStages ?? false, th.ToArray(), d.Text(dr.Note, p + ".note")));
        }
        if (drivers.Count == 0) d.Error("settlementStages.drivers", "at least one driver is required");
        d.Text(s.Note, "settlementStages.note");
        return new SettlementStages(drivers, stages);
    }

    internal static T? Parse<T>(string json, Diags d) where T : class
    {
        try
        {
            foreach (string nul in TreesJsonOptions.NullListElements(json)) d.Error(nul, "null is not content");
            if (d.HasErrors) return null;
            T? dto = JsonSerializer.Deserialize<T>(json, TreesJsonOptions.Options);
            if (dto is null) d.Error("$", "empty document");
            return dto;
        }
        catch (JsonException ex)
        {
            d.Error(ex.Path ?? "$", $"invalid JSON at line {(ex.LineNumber ?? 0) + 1}: {ex.Message}");
            return null;
        }
    }

    /// <summary>Diagnostics plus the small typed readers every section uses.</summary>
    internal sealed class Diags(string file)
    {
        public List<WorldDiagnostic> List { get; } = [];
        public bool HasErrors => List.Count > 0;

        public void Error(string path, string message) => List.Add(new WorldDiagnostic(file, path, message));

        public string Text(string? s, string path)
        {
            if (string.IsNullOrWhiteSpace(s)) { Error(path, "missing or empty"); return ""; }
            if (!string.Equals(DrawList.Latin1(s), s, StringComparison.Ordinal))
                Error(path, "text must be Latin-1 (the game's font atlas draws nothing else)");
            return s;
        }

        public string Id(string? s, string path, HashSet<string> seen)
        {
            string id = Text(s, path);
            if (id.Length > 0 && !seen.Add(id)) Error(path, $"duplicate id '{id}'");
            return id;
        }

        public string OneOf(string? s, string path, params string[] allowed)
        {
            string v = Text(s, path);
            if (v.Length > 0 && Array.IndexOf(allowed, v) < 0) Error(path, $"'{v}' is not one of: {string.Join(", ", allowed)}");
            return v;
        }

        public double Finite(double? v, string path)
        {
            if (v is not double x || !double.IsFinite(x)) { Error(path, "missing or not a finite number"); return 0; }
            return x;
        }

        public double Positive(double? v, string path)
        {
            double x = Finite(v, path);
            if (v is double && x <= 0) Error(path, "must be positive");
            return x;
        }

        public T Enum<T>(string? s, string path) where T : struct, System.Enum
        {
            string v = Text(s, path);
            foreach (string name in System.Enum.GetNames<T>())
                if (string.Equals(name, v, StringComparison.Ordinal)) return System.Enum.Parse<T>(name);
            if (v.Length > 0) Error(path, $"unknown {typeof(T).Name} '{v}'");
            return default;
        }

        public WorldGlyph Glyph(GlyphDto? g, string path)
        {
            if (g is null) { Error(path, "missing"); return new WorldGlyph(GlyphBase.Hall, GlyphDomain.None); }
            return new WorldGlyph(Enum<GlyphBase>(g.Base, path + ".base"), Enum<GlyphDomain>(g.Mark, path + ".mark"));
        }

        public string Ink(string? s, string path) =>
            OneOf(s, path, "InkPrimary", "InkSoft", "Verdigris", "IronRed", "GoldLeaf", "River");

        public void StageInputAllowed(string input, string path)
        {
            foreach (string f in ForbiddenStageInputs)
                if (string.Equals(input, f, StringComparison.OrdinalIgnoreCase))
                    Error(path, $"'{input}' would key a visual stage to time, not to reported state (law 4)");
        }

        public StageRule Rule(StageRuleDto? r, string path)
        {
            if (r is null) { Error(path, "missing"); return new StageRule("multiplicity", null, []); }
            if ((r.Input is null) == (r.Label is null)) Error(path, "exactly one of 'input' or 'label' is required");
            if (r.Input is not null)
            {
                Text(r.Input, path + ".input");
                StageInputAllowed(r.Input, path + ".input");
                if (r.Ordinals is not null) Error(path + ".ordinals", "ordinals apply to a 'label' rule only");
                return new StageRule(r.Input, null, []);
            }
            Text(r.Label, path + ".label");
            if (r.Label is not null) StageInputAllowed(r.Label, path + ".label");
            if (r.Ordinals is null || r.Ordinals.Count == 0) { Error(path + ".ordinals", "a label rule needs an ordinal table"); return new StageRule(null, r.Label, []); }
            // Sorted by value (ordinal) so nothing downstream depends on dictionary order.
            var table = new List<(string, double)>();
            foreach (string key in r.Ordinals.Keys.OrderBy(k => k, StringComparer.Ordinal))
            {
                double score = r.Ordinals[key];
                if (!double.IsFinite(score) || score < 0) Error($"{path}.ordinals.{key}", "must be a finite non-negative number");
                Text(key, $"{path}.ordinals");
                table.Add((key, score));
            }
            return new StageRule(null, r.Label, table);
        }

        public MorphPart Part(PartDto p, string path)
        {
            string kind = OneOf(p.Kind, path + ".kind", WorldViewConstants.PartKinds);
            double x = Finite(p.X, path + ".x"), y = Finite(p.Y, path + ".y");
            double w = Positive(p.W, path + ".w"), h = Positive(p.H, path + ".h");
            // A part must stay inside its lot at every stage: no footprint overflow, ever.
            const double e = 1e-9;
            if (x - w / 2 < -0.5 - e || x + w / 2 > 0.5 + e || y - h / 2 < -0.5 - e || y + h / 2 > 0.5 + e)
                Error(path, "part leaves its lot (the lot is [-0.5, 0.5] on both axes)");
            return new MorphPart(kind, x, y, w, h);
        }
    }
}

/// <summary>Names the view relies on.</summary>
public static class WorldViewConstants
{
    /// <summary>The visual type a structure is drawn as when content does not know its type.</summary>
    public const string FallbackVisualType = "structure";
    /// <summary>The agent type an agent is drawn as when content does not know its display type.</summary>
    public const string FallbackAgentType = "person";
    /// <summary>The built-in numeric stage input: the report's own multiplicity.</summary>
    public const string MultiplicityInput = "multiplicity";

    public static readonly string[] PartKinds =
        ["hall", "wing", "block", "tower", "dome", "green", "cross", "sawtooth", "stack", "yard", "basin"];
}
