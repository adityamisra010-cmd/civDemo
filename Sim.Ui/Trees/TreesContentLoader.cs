using System.Text.Json;
using Sim.Ui.Art.Glyphs;
using Sim.Ui.Trees.Json;

namespace Sim.Ui.Trees;

/// <summary>The outcome of loading content: the resolved set, or null when any ERROR was
/// found, plus every diagnostic. The UI shows diagnostics instead of crashing, so a
/// Director editing JSON sees exactly what is wrong and where.</summary>
public sealed record ContentLoadResult(TreesContentSet? Content, IReadOnlyList<ContentDiagnostic> Diagnostics)
{
    public bool Ok => Content is not null;
    public IEnumerable<ContentDiagnostic> Errors => Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error);
}

/// <summary>
/// CONTENT → MODEL. Reads the four content files, validates every reference across them,
/// and resolves the inline relationship fields of a node (prerequisites, enables,
/// dependsOn, feedsInto, related*) into the one typed edge list — so the graph has ONE
/// edge representation however the Director chose to write a relationship.
///
/// It decides nothing about the simulation: no cost is computed, no state inferred, no
/// relationship made authoritative. What it enforces is the shape of the content.
/// </summary>
public static class TreesContentLoader
{
    public const string TreesFile = "the-trees.json";
    public const string AgesFile = "ages.json";
    public const string GalleryFile = "gallery.json";
    public const string AnimationsFile = "animations.json";
    public const string DemoStateFile = "demo-state.json";

    public const string TreesSchema = "civ-sim/the-trees@1";
    public const string AgesSchema = "civ-sim/ages@1";
    public const string GallerySchema = "civ-sim/gallery@1";
    public const string AnimationsSchema = "civ-sim/ui-animations@1";

    /// <summary>The palette tokens a relation kind may name as its ink.</summary>
    public static readonly string[] InkTokens = ["InkPrimary", "InkSoft", "Verdigris", "IronRed", "GoldLeaf", "River"];

    /// <summary>The inline node fields and the relation kind each one writes, with the
    /// direction the authored sentence reads in ("self" is the node carrying the field).</summary>
    public static readonly (string Field, string Kind, bool SelfIsFrom)[] InlineRelations =
    [
        ("prerequisites", "prerequisite", false),   // X is a prerequisite of self
        ("enables", "enables", true),               // self enables X
        ("dependsOn", "dependsOn", true),           // self depends on X
        ("feedsInto", "feeds", true),               // self feeds into X
        ("relatedInstitutions", "relatedTo", true),
        ("relatedInfrastructure", "relatedTo", true),
        ("relatedIndustry", "relatedTo", true),
        ("relatedMilitary", "relatedTo", true),
        ("relatedApplications", "relatedTo", true),
    ];

    /// <summary>The lens a related* list expects its targets to live in (checked as a warning).</summary>
    private static string? RelatedLens(string field) => field switch
    {
        "relatedInstitutions" => "INSTITUTIONS",
        "relatedInfrastructure" => "INFRASTRUCTURE",
        "relatedIndustry" => "INDUSTRY",
        "relatedMilitary" => "MILITARY",
        "relatedApplications" => "APPLICATIONS",
        _ => null,
    };

    /// <summary>The default content directory: ui-content/trees beside the executable.</summary>
    public static string DefaultDirectory => Path.Combine(AppContext.BaseDirectory, "ui-content", "trees");

    public static ContentLoadResult LoadDirectory(string directory)
    {
        var diags = new List<ContentDiagnostic>();
        string? Read(string file)
        {
            string path = Path.Combine(directory, file);
            if (!File.Exists(path))
            {
                diags.Add(new(DiagnosticSeverity.Error, file, "$", $"file not found in {directory}"));
                return null;
            }
            return File.ReadAllText(path);
        }
        string? trees = Read(TreesFile), ages = Read(AgesFile), gallery = Read(GalleryFile), anims = Read(AnimationsFile);
        if (trees is null || ages is null || gallery is null || anims is null) return new(null, diags);
        ContentLoadResult r = LoadFromJson(trees, ages, gallery, anims, directory);
        return new(r.Content, diags.Concat(r.Diagnostics).ToArray());
    }

    public static ContentLoadResult LoadFromJson(string treesJson, string agesJson, string galleryJson, string animationsJson,
        string sourceDescription = "(in memory)")
    {
        var d = new Diagnostics();
        TreesFileDto? treesDto = Parse<TreesFileDto>(treesJson, TreesFile, d);
        AgesFileDto? agesDto = Parse<AgesFileDto>(agesJson, AgesFile, d);
        GalleryFileDto? galleryDto = Parse<GalleryFileDto>(galleryJson, GalleryFile, d);
        AnimationsFileDto? animDto = Parse<AnimationsFileDto>(animationsJson, AnimationsFile, d);
        if (treesDto is null || agesDto is null || galleryDto is null || animDto is null) return new(null, d.All);

        AnimationDocument animations = ResolveAnimations(animDto, d);
        AgesDocument? ages = ResolveAges(agesDto, animations, d);
        TreesDocument? trees = ages is null ? null : ResolveTrees(treesDto, ages, d);
        GalleryDocument gallery = ResolveGallery(galleryDto, d);
        if (ages is not null && trees is not null) CrossCheckAges(ages, trees, d);

        if (d.HasErrors || trees is null || ages is null) return new(null, d.All);
        var warnings = d.All.Where(x => x.Severity == DiagnosticSeverity.Warning).ToArray();
        return new(new TreesContentSet(trees, ages, gallery, animations, warnings, sourceDescription), d.All);
    }

    // --- parsing ------------------------------------------------------------------------

    private static T? Parse<T>(string json, string file, Diagnostics d) where T : class
    {
        try
        {
            T? dto = JsonSerializer.Deserialize<T>(json, TreesJsonOptions.Options);
            if (dto is null) d.Error(file, "$", "the file is empty or null");
            return dto;
        }
        catch (JsonException ex)
        {
            d.Error(file, ex.Path ?? "$", $"invalid JSON at line {ex.LineNumber + 1}: {ex.Message}");
            return null;
        }
    }

    private sealed class Diagnostics
    {
        private readonly List<ContentDiagnostic> _all = [];
        public IReadOnlyList<ContentDiagnostic> All => _all;
        public bool HasErrors => _all.Any(x => x.Severity == DiagnosticSeverity.Error);
        public void Error(string file, string path, string msg) => _all.Add(new(DiagnosticSeverity.Error, file, path, msg));
        public void Warn(string file, string path, string msg) => _all.Add(new(DiagnosticSeverity.Warning, file, path, msg));
    }

    private static string Req(string? value, string file, string path, Diagnostics d)
    {
        if (string.IsNullOrWhiteSpace(value)) { d.Error(file, path, "required"); return ""; }
        return value;
    }

    private static TEnum ParseEnum<TEnum>(string? value, TEnum fallback, string file, string path, Diagnostics d, bool required = true)
        where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            if (required) d.Error(file, path, $"required ({typeof(TEnum).Name})");
            return fallback;
        }
        if (Enum.TryParse(value, ignoreCase: false, out TEnum parsed) && Enum.IsDefined(parsed)
            && !int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out _))
            return parsed;
        d.Error(file, path, $"'{value}' is not a {typeof(TEnum).Name} (one of: {string.Join(", ", Enum.GetNames<TEnum>())})");
        return fallback;
    }

    /// <summary>kebab-case content words → enum members ("research-progress" → ResearchProgress).</summary>
    private static TEnum ParseWord<TEnum>(string? value, string file, string path, Diagnostics d) where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value)) { d.Error(file, path, "required"); return default; }
        string pascal = string.Concat(value.Split('-', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => char.ToUpperInvariant(w[0]) + w[1..]));
        if (Enum.TryParse(pascal, ignoreCase: false, out TEnum parsed) && Enum.IsDefined(parsed)) return parsed;
        string words = string.Join(", ", Enum.GetNames<TEnum>().Select(Kebab));
        d.Error(file, path, $"'{value}' is not one of: {words}");
        return default;
    }

    internal static string Kebab(string pascal)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < pascal.Length; i++)
        {
            char c = pascal[i];
            if (char.IsUpper(c) && i > 0) sb.Append('-');
            sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }

    private static void Unique<T>(IEnumerable<T> items, Func<T, string> id, string file, string what, Diagnostics d)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        int i = 0;
        foreach (T item in items)
        {
            string key = id(item);
            if (key.Length > 0 && !seen.Add(key)) d.Error(file, $"{what}[{i}].id", $"duplicate id '{key}'");
            i++;
        }
    }

    // --- animations ---------------------------------------------------------------------

    private static readonly string[] SteadyPrefixes = ["node-state", "edge-kind", "building-status", "unit-state", "age"];
    private static readonly string[] TriggerPrefixes = ["node-enter", "node-stage-up", "building-stage-up", "unit-veterancy-up", "age-enter"];

    private static AnimationDocument ResolveAnimations(AnimationsFileDto dto, Diagnostics d)
    {
        const string F = AnimationsFile;
        if (dto.Schema != AnimationsSchema) d.Error(F, "schema", $"expected '{AnimationsSchema}', found '{dto.Schema}'");
        var list = new List<AnimationDef>();
        List<AnimationDto> src = dto.Animations ?? [];
        for (int i = 0; i < src.Count; i++)
        {
            AnimationDto a = src[i];
            string p = $"animations[{i}]";
            string id = Req(a.Id, F, p + ".id", d);
            AnimationKind kind = ParseWord<AnimationKind>(a.Kind, F, p + ".kind", d);
            if (a.AppliesTo is null == (a.Trigger is null))
                d.Error(F, p, "exactly one of appliesTo (a steady loop) or trigger (a one-shot) is required");
            if (a.AppliesTo is not null && !SteadyPrefixes.Any(x => a.AppliesTo.StartsWith(x + ":", StringComparison.Ordinal)))
                d.Error(F, p + ".appliesTo", $"'{a.AppliesTo}' must start with one of: {string.Join(", ", SteadyPrefixes.Select(x => x + ":"))}");
            if (a.Trigger is not null && !TriggerPrefixes.Any(x => a.Trigger == x || a.Trigger.StartsWith(x + ":", StringComparison.Ordinal)))
                d.Error(F, p + ".trigger", $"'{a.Trigger}' must be one of: {string.Join(", ", TriggerPrefixes)}");
            double period = a.PeriodSeconds ?? 0.0, duration = a.DurationSeconds ?? 0.0;
            if (kind != AnimationKind.None && a.AppliesTo is not null && !(period > 0.0))
                d.Error(F, p + ".periodSeconds", "a looping animation needs periodSeconds > 0");
            if (kind != AnimationKind.None && a.Trigger is not null && !(duration > 0.0))
                d.Error(F, p + ".durationSeconds", "a one-shot animation needs durationSeconds > 0");
            list.Add(new AnimationDef(id, kind, a.AppliesTo, a.Trigger, period, duration,
                System.Math.Clamp(a.Amplitude ?? 0.3, 0.0, 1.0), System.Math.Clamp(a.Dots ?? 3, 1, 8), a.Description ?? ""));
        }
        Unique(list, x => x.Id, F, "animations", d);
        return new AnimationDocument(dto.Notice ?? "", list);
    }

    // --- ages ----------------------------------------------------------------------------

    private static AgesDocument? ResolveAges(AgesFileDto dto, AnimationDocument animations, Diagnostics d)
    {
        const string F = AgesFile;
        if (dto.Schema != AgesSchema) d.Error(F, "schema", $"expected '{AgesSchema}', found '{dto.Schema}'");
        var cats = (dto.MilestoneCategories ?? []).Select((c, i) =>
            new MilestoneCategoryDef(Req(c.Id, F, $"milestoneCategories[{i}].id", d), c.Name ?? c.Id ?? "", c.Provisional ?? true)).ToArray();
        Unique(cats, c => c.Id, F, "milestoneCategories", d);
        var catIds = new HashSet<string>(cats.Select(c => c.Id), StringComparer.Ordinal);

        var ages = new List<AgeDef>();
        List<AgeDto> src = dto.Ages ?? [];
        if (src.Count == 0) d.Error(F, "ages", "at least one Age is required");
        for (int i = 0; i < src.Count; i++)
        {
            AgeDto a = src[i];
            string p = $"ages[{i}]";
            string id = Req(a.Id, F, p + ".id", d);
            if (a.Order is null) d.Error(F, p + ".order", "required");
            DateRangeDto dr = a.DateRange ?? new DateRangeDto();
            if (dr.StartYear is long s && dr.EndYear is long e && e < s) d.Error(F, p + ".dateRange", "endYear precedes startYear");
            AgeThemeDto th = a.VisualTheme ?? new AgeThemeDto();
            EraRegister register = ParseEnum(th.Register, EraRegister.Primitive, F, p + ".visualTheme.register", d, required: false);
            GlyphBase iconBase = ParseEnum(a.Icon?.Base, GlyphBase.Star, F, p + ".icon.base", d, required: false);
            GlyphDomain iconMark = ParseEnum(a.Icon?.Mark, GlyphDomain.Hourglass, F, p + ".icon.mark", d, required: false);
            string anim = a.TransitionEffect?.Animation ?? "";
            if (anim.Length > 0 && !animations.Animations.Any(x => x.Id == anim))
                d.Error(F, p + ".transitionEffect.animation", $"no animation '{anim}' in {AnimationsFile}");
            DeltaTDto dt = a.DeltaT ?? new DeltaTDto();
            if (dt.YearsPerTurn is double y && !(y > 0.0)) d.Error(F, p + ".deltaT.yearsPerTurn", "must be > 0 when given");
            CatchUpDto cu = a.CatchUp ?? new CatchUpDto();
            ages.Add(new AgeDef(id, a.DisplayName ?? id, a.Order ?? 0, a.ShortDescription ?? "",
                new DateRangeDef(dr.StartLabel ?? "TBD", dr.EndLabel ?? "TBD", dr.StartYear, dr.EndYear, dr.Note ?? ""),
                a.Milestones ?? [], new AgeThemeDef(register, th.Accent ?? "InkPrimary", th.Motif ?? ""),
                iconBase, iconMark, anim, a.TransitionEffect?.Note ?? "",
                new DeltaTPlaceholder(dt.Placeholder ?? true, dt.YearsPerTurn, dt.Note ?? ""),
                new CatchUpPlaceholder(cu.Placeholder ?? true, cu.PathwayBased ?? true, cu.Note ?? ""),
                a.HistoricalContext ?? "", a.Placeholder ?? true));
        }
        Unique(ages, a => a.Id, F, "ages", d);
        foreach (var g in ages.GroupBy(a => a.Order).Where(g => g.Count() > 1))
            d.Error(F, "ages", $"order {g.Key} is used by more than one Age ({string.Join(", ", g.Select(a => a.Id))}) — Ages are strictly ordered");

        var milestones = new List<MilestoneDef>();
        List<MilestoneDto> ms = dto.Milestones ?? [];
        for (int i = 0; i < ms.Count; i++)
        {
            MilestoneDto m = ms[i];
            string p = $"milestones[{i}]";
            string id = Req(m.Id, F, p + ".id", d);
            string cat = Req(m.Category, F, p + ".category", d);
            if (cat.Length > 0 && !catIds.Contains(cat)) d.Error(F, p + ".category", $"unknown category '{cat}'");
            milestones.Add(new MilestoneDef(id, m.Age ?? "", m.Name ?? id, m.Description ?? "", cat, m.Mandatory ?? true,
                m.Prerequisites ?? [], m.EvidenceNotes ?? "", m.HistoricalSource ?? "", m.NodeRefs ?? [], m.Placeholder ?? true));
        }
        Unique(milestones, m => m.Id, F, "milestones", d);
        if (d.HasErrors) return null;

        var doc = new AgesDocument(dto.Notice ?? "", dto.Placeholder ?? true, cats, ages, milestones);
        // Every Age's listed milestones exist and belong to it; every milestone is listed once.
        var listedBy = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 0; i < ages.Count; i++)
        {
            foreach (string mid in ages[i].Milestones)
            {
                if (!doc.TryMilestone(mid, out MilestoneDef m)) { d.Error(F, $"ages[{i}].milestones", $"unknown milestone '{mid}'"); continue; }
                if (m.Age.Length > 0 && m.Age != ages[i].Id)
                    d.Error(F, $"ages[{i}].milestones", $"milestone '{mid}' says it belongs to '{m.Age}'");
                if (!listedBy.TryAdd(mid, ages[i].Id))
                    d.Error(F, $"ages[{i}].milestones", $"milestone '{mid}' is already listed by '{listedBy[mid]}'");
            }
        }
        for (int i = 0; i < milestones.Count; i++)
        {
            MilestoneDef m = milestones[i];
            if (!listedBy.ContainsKey(m.Id)) d.Warn(F, $"milestones[{i}]", $"milestone '{m.Id}' is not listed by any Age");
            if (m.Age.Length > 0 && !doc.TryAge(m.Age, out _)) d.Error(F, $"milestones[{i}].age", $"unknown Age '{m.Age}'");
            foreach (string pre in m.Prerequisites)
                if (!doc.TryMilestone(pre, out _)) d.Error(F, $"milestones[{i}].prerequisites", $"unknown milestone '{pre}'");
        }
        string? cycle = FindCycle(milestones.Select(m => m.Id).ToArray(),
            id => doc.TryMilestone(id, out MilestoneDef m) ? m.Prerequisites.Where(x => doc.TryMilestone(x, out _)) : []);
        if (cycle is not null) d.Error(F, "milestones", $"milestone prerequisites form a cycle: {cycle}");
        return d.HasErrors ? null : doc;
    }

    // --- the trees ------------------------------------------------------------------------

    private static TreesDocument? ResolveTrees(TreesFileDto dto, AgesDocument ages, Diagnostics d)
    {
        const string F = TreesFile;
        if (dto.Schema != TreesSchema) d.Error(F, "schema", $"expected '{TreesSchema}', found '{dto.Schema}'");

        var lenses = (dto.Lenses ?? []).Select((l, i) => new LensDef(
            Req(l.Id, F, $"lenses[{i}].id", d), l.Name ?? l.Id ?? "", l.Order ?? i,
            ParseEnum(l.Mark, GlyphDomain.None, F, $"lenses[{i}].mark", d), l.ShortDescription ?? "")).ToArray();
        if (lenses.Length == 0) d.Error(F, "lenses", "at least one lens is required");
        Unique(lenses, l => l.Id, F, "lenses", d);

        var states = (dto.States ?? []).Select((s, i) =>
        {
            string p = $"states[{i}]";
            int stage = s.Stage ?? 0;
            if (stage < 0 || stage > GlyphSpec.MaxStage) d.Error(F, p + ".stage", $"must be 0..{GlyphSpec.MaxStage}");
            return new StateDef(Req(s.Id, F, p + ".id", d), s.Name ?? s.Id ?? "",
                ParseWord<StateTrack>(s.Track, F, p + ".track", d),
                ParseEnum(s.GlyphState, GlyphState.Locked, F, p + ".glyphState", d), stage,
                ParseWord<WashMode>(s.Wash ?? "none", F, p + ".wash", d),
                ParseWord<ResearchRole>(s.ResearchRole ?? "none", F, p + ".researchRole", d), s.Legend ?? "");
        }).ToArray();
        Unique(states, s => s.Id, F, "states", d);
        var stateIds = new HashSet<string>(states.Select(s => s.Id), StringComparer.Ordinal);

        var sets = (dto.StateSets ?? []).Select((s, i) =>
        {
            string p = $"stateSets[{i}]";
            List<string> list = s.States ?? [];
            if (list.Count == 0) d.Error(F, p + ".states", "a state set needs at least one state");
            foreach (string st in list) if (!stateIds.Contains(st)) d.Error(F, p + ".states", $"unknown state '{st}'");
            if (list.Distinct(StringComparer.Ordinal).Count() != list.Count) d.Error(F, p + ".states", "a state is listed twice");
            return new StateSetDef(Req(s.Id, F, p + ".id", d), s.Description ?? "", list);
        }).ToArray();
        Unique(sets, s => s.Id, F, "stateSets", d);
        var setIds = new HashSet<string>(sets.Select(s => s.Id), StringComparer.Ordinal);

        var types = (dto.NodeTypes ?? []).Select((t, i) =>
        {
            string p = $"nodeTypes[{i}]";
            string set = Req(t.StateSet, F, p + ".stateSet", d);
            if (set.Length > 0 && !setIds.Contains(set)) d.Error(F, p + ".stateSet", $"unknown state set '{set}'");
            return new NodeTypeDef(Req(t.Id, F, p + ".id", d), t.Name ?? t.Id ?? "",
                ParseEnum(t.Base, GlyphBase.Node, F, p + ".base", d), ParseEnum(t.Mark, GlyphDomain.None, F, p + ".mark", d, required: false),
                set, t.Description ?? "");
        }).ToArray();
        Unique(types, t => t.Id, F, "nodeTypes", d);

        var kinds = (dto.RelationKinds ?? []).Select((k, i) =>
        {
            string p = $"relationKinds[{i}]";
            string ink = k.Ink ?? "InkSoft";
            if (!InkTokens.Contains(ink)) d.Error(F, p + ".ink", $"'{ink}' is not a palette token ({string.Join(", ", InkTokens)})");
            RelationFlow flow = ParseWord<RelationFlow>(k.Flow, F, p + ".flow", d);
            bool layering = k.Layering ?? false;
            if (flow == RelationFlow.None && layering) d.Error(F, p + ".layering", "an undirected kind (flow: none) cannot order the layout");
            return new RelationKindDef(Req(k.Id, F, p + ".id", d), k.Name ?? k.Id ?? "", k.Verb ?? k.Id ?? "", flow, layering,
                ParseWord<StrokeStyle>(k.Stroke ?? "solid", F, p + ".stroke", d), ink, k.Arrow ?? true, k.Description ?? "");
        }).ToArray();
        Unique(kinds, k => k.Id, F, "relationKinds", d);
        if (d.HasErrors) return null;

        var lensIds = new HashSet<string>(lenses.Select(l => l.Id), StringComparer.Ordinal);
        var typeById = types.ToDictionary(t => t.Id, StringComparer.Ordinal);
        var kindIds = new HashSet<string>(kinds.Select(k => k.Id), StringComparer.Ordinal);

        // Nodes.
        var nodes = new List<NodeDef>();
        List<NodeDto> src = dto.Nodes ?? [];
        for (int i = 0; i < src.Count; i++)
        {
            NodeDto n = src[i];
            string p = $"nodes[{i}]";
            string id = Req(n.Id, F, p + ".id", d);
            string domain = Req(n.Domain, F, p + ".domain", d);
            if (domain.Length > 0 && !lensIds.Contains(domain)) d.Error(F, p + ".domain", $"unknown lens '{domain}'");
            List<string> alsoIn = n.AlsoIn ?? [];
            foreach (string l in alsoIn)
            {
                if (!lensIds.Contains(l)) d.Error(F, p + ".alsoIn", $"unknown lens '{l}'");
                if (l == domain) d.Warn(F, p + ".alsoIn", $"'{l}' is already the node's domain");
            }
            string typeId = Req(n.Type, F, p + ".type", d);
            typeById.TryGetValue(typeId, out NodeTypeDef? type);
            if (typeId.Length > 0 && type is null) d.Error(F, p + ".type", $"unknown node type '{typeId}'");
            GlyphBase baseShape = n.Icon?.Base is null ? type?.Base ?? GlyphBase.Node : ParseEnum(n.Icon.Base, GlyphBase.Node, F, p + ".icon.base", d);
            GlyphDomain mark = n.Icon?.Mark is null ? type?.Mark ?? GlyphDomain.None : ParseEnum(n.Icon.Mark, GlyphDomain.None, F, p + ".icon.mark", d);
            EraRegister era = ParseEnum(n.VisualStyle?.Era, EraRegister.Primitive, F, p + ".visualStyle.era", d, required: false);

            string? from = n.AgeRange?.From, to = n.AgeRange?.To;
            if (from is not null && !ages.TryAge(from, out _)) d.Error(F, p + ".ageRange.from", $"unknown Age '{from}'");
            if (to is not null && !ages.TryAge(to, out _)) d.Error(F, p + ".ageRange.to", $"unknown Age '{to}'");
            if (from is not null && to is not null && ages.TryAge(from, out AgeDef af) && ages.TryAge(to, out AgeDef at) && at.Order < af.Order)
                d.Error(F, p + ".ageRange", $"'{to}' precedes '{from}'");
            List<string> ageRefs = n.AgeRefs ?? [];
            foreach (string a in ageRefs) if (!ages.TryAge(a, out _)) d.Error(F, p + ".ageRefs", $"unknown Age '{a}'");

            ResearchCostDef? cost = null;
            if (n.ResearchCost is not null)
            {
                if (n.ResearchCost.Points is long pts && pts < 0) d.Error(F, p + ".researchCost.points", "must be ≥ 0");
                cost = new ResearchCostDef(n.ResearchCost.Points, n.ResearchCost.Note ?? "");
            }
            if (n.MilestoneRef is not null && !ages.TryMilestone(n.MilestoneRef, out _))
                d.Error(F, p + ".milestoneRef", $"unknown milestone '{n.MilestoneRef}'");
            if (n.Column is int c && c < 0) d.Error(F, p + ".column", "must be ≥ 0");

            nodes.Add(new NodeDef(id, domain, alsoIn, typeId, n.Name ?? id, n.ShortDescription ?? "", n.LongDescription ?? "",
                baseShape, mark, era, n.VisualStyle?.Note ?? "", from, to, ageRefs, cost, n.HistoricalReferences ?? [],
                n.DirectorNotes ?? "", n.MilestoneRef, n.Column, n.Placeholder ?? true, i));
        }
        Unique(nodes, n => n.Id, F, "nodes", d);
        if (d.HasErrors) return null;
        var nodeById = nodes.ToDictionary(n => n.Id, StringComparer.Ordinal);

        // Edges: the authored list, then each node's inline relationship fields.
        var edges = new List<EdgeDef>();
        var seenEdges = new HashSet<(string, string, string)>();
        void AddEdge(string from, string to, string kind, string note, string origin, string path)
        {
            bool ok = true;
            if (!nodeById.ContainsKey(from)) { d.Error(F, path, $"unknown node '{from}'"); ok = false; }
            if (!nodeById.ContainsKey(to)) { d.Error(F, path, $"unknown node '{to}'"); ok = false; }
            if (!kindIds.Contains(kind)) { d.Error(F, path, $"unknown relation kind '{kind}'"); ok = false; }
            if (ok && from == to) { d.Error(F, path, $"'{from}' relates to itself"); ok = false; }
            if (!ok) return;
            if (!seenEdges.Add((from, to, kind))) { d.Warn(F, path, $"duplicate relationship {from} {kind} {to} (kept once)"); return; }
            edges.Add(new EdgeDef(from, to, kind, note, origin));
        }
        List<EdgeDto> esrc = dto.Edges ?? [];
        for (int i = 0; i < esrc.Count; i++)
        {
            EdgeDto e = esrc[i];
            string p = $"edges[{i}]";
            AddEdge(Req(e.From, F, p + ".from", d), Req(e.To, F, p + ".to", d), Req(e.Kind, F, p + ".kind", d), e.Note ?? "", "edges", p);
        }
        for (int i = 0; i < src.Count; i++)
        {
            NodeDto n = src[i];
            foreach ((string field, string kind, bool selfIsFrom) in InlineRelations)
            {
                List<string>? list = InlineList(n, field);
                if (list is null) continue;
                for (int j = 0; j < list.Count; j++)
                {
                    string other = list[j];
                    string path = $"nodes[{i}].{field}[{j}]";
                    (string from, string to) = selfIsFrom ? (n.Id!, other) : (other, n.Id!);
                    AddEdge(from, to, kind, "", field, path);
                    string? lens = RelatedLens(field);
                    if (lens is not null && nodeById.TryGetValue(other, out NodeDef? target) && target.Domain != lens && !target.AlsoIn.Contains(lens))
                        d.Warn(F, path, $"'{other}' is listed as {field} but is not in the {lens} lens");
                }
            }
        }
        if (d.HasErrors) return null;

        var doc = new TreesDocument(dto.Notice ?? "", dto.Placeholder ?? true, lenses, states, sets, types, kinds, nodes, edges);

        // The layout needs the LAYERING edges to be acyclic (feedback belongs in a
        // non-layering kind such as improves or supports).
        var flowOut = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (EdgeDef e in edges)
        {
            RelationKindDef k = doc.Kind(e.Kind);
            if (!k.Layering) continue;
            (string a, string b) = k.Flow == RelationFlow.Reverse ? (e.To, e.From) : (e.From, e.To);
            if (!flowOut.TryGetValue(a, out var list)) flowOut[a] = list = [];
            list.Add(b);
        }
        string? cyc = FindCycle(nodes.Select(n => n.Id).ToArray(), id => flowOut.TryGetValue(id, out var l) ? l : []);
        if (cyc is not null)
            d.Error(F, "edges", $"layering relationships form a cycle: {cyc}. Express feedback with a non-layering kind (improves, supports).");

        // Every node's type must be able to show SOME state; lenses with no node are only a warning.
        foreach (LensDef l in lenses)
            if (!nodes.Any(n => n.Domain == l.Id || n.AlsoIn.Contains(l.Id)))
                d.Warn(F, "lenses", $"lens '{l.Id}' has no nodes yet");
        return d.HasErrors ? null : doc;
    }

    private static List<string>? InlineList(NodeDto n, string field) => field switch
    {
        "prerequisites" => n.Prerequisites,
        "enables" => n.Enables,
        "dependsOn" => n.DependsOn,
        "feedsInto" => n.FeedsInto,
        "relatedInstitutions" => n.RelatedInstitutions,
        "relatedInfrastructure" => n.RelatedInfrastructure,
        "relatedIndustry" => n.RelatedIndustry,
        "relatedMilitary" => n.RelatedMilitary,
        "relatedApplications" => n.RelatedApplications,
        _ => null,
    };

    private static void CrossCheckAges(AgesDocument ages, TreesDocument trees, Diagnostics d)
    {
        var nodeIds = new HashSet<string>(trees.Nodes.Select(n => n.Id), StringComparer.Ordinal);
        for (int i = 0; i < ages.Milestones.Count; i++)
            foreach (string r in ages.Milestones[i].NodeRefs)
                if (!nodeIds.Contains(r)) d.Error(AgesFile, $"milestones[{i}].nodeRefs", $"unknown node '{r}'");
    }

    // --- gallery ---------------------------------------------------------------------------

    private static GalleryDocument ResolveGallery(GalleryFileDto dto, Diagnostics d)
    {
        const string F = GalleryFile;
        if (dto.Schema != GallerySchema) d.Error(F, "schema", $"expected '{GallerySchema}', found '{dto.Schema}'");
        var stages = (dto.MaturityStages ?? []).Select((m, i) =>
        {
            int st = m.Stage ?? 0;
            if (st < 1 || st > GlyphSpec.MaxStage) d.Error(F, $"maturityStages[{i}].stage", $"must be 1..{GlyphSpec.MaxStage}");
            return new MaturityStageDef(Req(m.Id, F, $"maturityStages[{i}].id", d), m.Name ?? m.Id ?? "", st, m.Legend ?? "");
        }).ToArray();
        Unique(stages, s => s.Id, F, "maturityStages", d);
        StatusGlyphDef[] Status(List<StatusGlyphDto>? list, string what) => (list ?? []).Select((s, i) =>
            new StatusGlyphDef(Req(s.Id, F, $"{what}[{i}].id", d), s.Name ?? s.Id ?? "",
                ParseEnum(s.GlyphState, GlyphState.Available, F, $"{what}[{i}].glyphState", d), s.Legend ?? "")).ToArray();
        var ops = Status(dto.OperationalStatuses, "operationalStatuses");
        var unitStates = Status(dto.UnitStates, "unitStates");
        Unique(ops, s => s.Id, F, "operationalStatuses", d);
        Unique(unitStates, s => s.Id, F, "unitStates", d);
        var vets = (dto.VeterancyLevels ?? []).Select((v, i) =>
        {
            int ch = v.Chevrons ?? 0;
            if (ch < 0 || ch > 3) d.Error(F, $"veterancyLevels[{i}].chevrons", "must be 0..3");
            return new VeterancyLevelDef(Req(v.Id, F, $"veterancyLevels[{i}].id", d), v.Name ?? v.Id ?? "", ch);
        }).ToArray();
        Unique(vets, v => v.Id, F, "veterancyLevels", d);
        var buildings = (dto.Buildings ?? []).Select((b, i) => new BuildingDef(Req(b.Id, F, $"buildings[{i}].id", d), b.Name ?? b.Id ?? "",
            ParseEnum(b.Base, GlyphBase.Hall, F, $"buildings[{i}].base", d), ParseEnum(b.Mark, GlyphDomain.None, F, $"buildings[{i}].mark", d, required: false),
            ParseEnum(b.Era, EraRegister.Primitive, F, $"buildings[{i}].era", d, required: false), b.Category ?? "", b.Placeholder ?? true)).ToArray();
        var units = (dto.Units ?? []).Select((u, i) => new UnitDef(Req(u.Id, F, $"units[{i}].id", d), u.Name ?? u.Id ?? "",
            ParseEnum(u.Base, GlyphBase.Standard, F, $"units[{i}].base", d), ParseEnum(u.Mark, GlyphDomain.None, F, $"units[{i}].mark", d, required: false),
            ParseEnum(u.Era, EraRegister.Primitive, F, $"units[{i}].era", d, required: false), u.Role ?? "", u.Placeholder ?? true)).ToArray();
        Unique(buildings, b => b.Id, F, "buildings", d);
        Unique(units, u => u.Id, F, "units", d);
        return new GalleryDocument(dto.Notice ?? "", stages, ops, unitStates, vets, buildings, units);
    }

    // --- shared -------------------------------------------------------------------------------

    /// <summary>A cycle through <paramref name="next"/> as "a → b → a", or null. Iterative
    /// DFS in the given id order, so the reported cycle is deterministic.</summary>
    internal static string? FindCycle(IReadOnlyList<string> ids, Func<string, IEnumerable<string>> next)
    {
        var colour = new Dictionary<string, int>(StringComparer.Ordinal);   // 0 white, 1 grey, 2 black
        var parent = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string root in ids)
        {
            if (colour.GetValueOrDefault(root) != 0) continue;
            var stack = new Stack<(string Id, IEnumerator<string> It)>();
            colour[root] = 1;
            stack.Push((root, next(root).GetEnumerator()));
            while (stack.Count > 0)
            {
                (string id, IEnumerator<string> it) = stack.Peek();
                if (it.MoveNext())
                {
                    string n = it.Current;
                    int c = colour.GetValueOrDefault(n);
                    if (c == 1)
                    {
                        var path = new List<string> { n };
                        for (string at = id; at != n; at = parent[at]) path.Add(at);
                        path.Add(n);
                        path.Reverse();
                        return string.Join(" → ", path);
                    }
                    if (c == 0)
                    {
                        colour[n] = 1;
                        parent[n] = id;
                        stack.Push((n, next(n).GetEnumerator()));
                    }
                }
                else
                {
                    colour[id] = 2;
                    stack.Pop();
                }
            }
        }
        return null;
    }
}
