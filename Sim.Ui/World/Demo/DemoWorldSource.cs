using System.Globalization;
using Sim.Ui.World.Content;
using Sim.Ui.World.Content.Json;

namespace Sim.Ui.World.Demo;

/// <summary>One step of the demonstration timeline (a chosen snapshot, not a turn).</summary>
public sealed record DemoStep(string Id, string Name, string Caption);

/// <summary>
/// THE DEMO WORLD SOURCE — DEMONSTRATION / PLACEHOLDER DATA ONLY
/// (ui-content/world/demo-world.json, docs/architecture/world-visualization.md §7).
///
/// It exists so the view layer can be seen working before the simulation produces
/// institutions, armies, roles for people or cultural groups. It implements exactly the
/// same six read-only source interfaces as the live adapters, so replacing it by an
/// authoritative source is a change of provider, not of renderer. Every snapshot it returns
/// is marked <c>IsPlaceholder</c> and every report's note says DEMO. None of its values is
/// ever written into, or read by, the simulation.
///
/// Its only state is the chosen STEP — a preview control, like the Trees' DemoStateSource.
/// A report at step s is the merge of the entity's timeline entries with step ≤ s:
/// values merge forward, inputs and labels merge BY NAME (null deletes one), an entry marked
/// <c>removed</c> ends the entity from that step on, and <c>Established</c> is DERIVED as the
/// entity's first step (never authored), so a later arrival always composes after an
/// earlier one.
/// </summary>
public sealed class DemoWorldSource :
    ISettlementViewSource, IPolityViewSource, IStructureViewSource, IInfrastructureViewSource, IResourceViewSource, IMobileAgentViewSource
{
    public const string Label = "DEMO world (placeholder)";
    public const string Schema = "civ-sim/world-demo@1";
    public const string Observer = "omniscient (demonstration)";

    private readonly DemoWorld _world;
    private int _step;
    private long _sequence = 1;
    private StepReports? _cache;

    private DemoWorldSource(DemoWorld world, int step)
    {
        _world = world;
        _step = Math.Clamp(step, 0, world.Steps.Count - 1);
    }

    public string Notice => _world.Notice;
    public double Width => _world.Width;
    public double Height => _world.Height;
    /// <summary>The demo's paper: its extent and decorative water. A backdrop, not an entity.</summary>
    public Scene.WorldBackdrop Backdrop => new(_world.Width, _world.Height, _world.Water);
    public IReadOnlyList<DemoStep> Steps => _world.Steps;
    public int Step => _step;
    public int StepCount => _world.Steps.Count;
    public DemoStep CurrentStep => _world.Steps[_step];

    /// <summary>A bundle for a scene: all six sources are this one demo world.</summary>
    public WorldSources Sources() => new(Label, IsPlaceholder: true, Observer, this, this, this, this, this, this);

    public void SetStep(int step)
    {
        int s = Math.Clamp(step, 0, _world.Steps.Count - 1);
        if (s == _step) return;
        _step = s;
        _sequence++;
        _cache = null;
    }

    public void Next() => SetStep(_step + 1);
    public void Previous() => SetStep(_step - 1);

    Snapshot<SettlementReport> ISettlementViewSource.Current => Reports.Settlements;
    Snapshot<PolityReport> IPolityViewSource.Current => Reports.Polities;
    Snapshot<StructureReport> IStructureViewSource.Current => Reports.Structures;
    Snapshot<InfraNodeReport> IInfrastructureViewSource.Nodes => Reports.Nodes;
    Snapshot<InfraEdgeReport> IInfrastructureViewSource.Edges => Reports.Edges;
    Snapshot<ResourceReport> IResourceViewSource.Current => Reports.Resources;
    Snapshot<AgentReport> IMobileAgentViewSource.Current => Reports.Agents;

    private sealed record StepReports(
        Snapshot<SettlementReport> Settlements, Snapshot<PolityReport> Polities, Snapshot<StructureReport> Structures,
        Snapshot<InfraNodeReport> Nodes, Snapshot<InfraEdgeReport> Edges, Snapshot<ResourceReport> Resources,
        Snapshot<AgentReport> Agents);

    private StepReports Reports => _cache ??= Build();

    private const string DemoNote = "DEMO / PLACEHOLDER: authored demonstration content, not simulation output";

    private StepReports Build()
    {
        long seq = _sequence;
        int s = _step;
        var settlements = new List<SettlementReport>();
        foreach (DemoEntity e in _world.Settlements)
            if (e.At(s) is Merged m)
                settlements.Add(new SettlementReport(e.Key, e.Name, m.Polity, m.Polity is null ? PolityRelation.None : PolityRelation.Controller,
                    new WorldPoint(e.X, e.Y), m.Inputs, m.Visibility, Note(e)));
        var structures = new List<StructureReport>();
        foreach (DemoEntity e in _world.Structures)
            if (e.At(s) is Merged m)
                structures.Add(new StructureReport(e.Key, e.Name, e.Type!, e.Settlement!, m.Polity,
                    m.Polity is null ? PolityRelation.None : PolityRelation.Owner, m.FirstStep, m.Multiplicity ?? 1, m.Inputs, m.Labels,
                    m.Specialization, m.State, m.Visibility, Note(e)));
        var nodes = new List<InfraNodeReport>();
        foreach (DemoEntity e in _world.Nodes)
            if (e.At(s) is Merged m)
                nodes.Add(new InfraNodeReport(e.Key, e.Name, e.Type!, new WorldPoint(e.X, e.Y), e.Settlement, m.Visibility, Note(e)));
        var edges = new List<InfraEdgeReport>();
        foreach (DemoEntity e in _world.Edges)
            if (e.At(s) is Merged m)
                edges.Add(new InfraEdgeReport(e.Key, e.Name, e.Type!, e.A!, e.B!, m.Polity,
                    m.Polity is null ? PolityRelation.None : PolityRelation.Owner, m.Inputs, m.Visibility, Note(e)));
        var resources = new List<ResourceReport>();
        foreach (DemoEntity e in _world.Resources)
            if (e.At(s) is Merged m)
                resources.Add(new ResourceReport(e.Key, e.Name, e.Type!, e.Settlement!, m.Inputs, m.Visibility, Note(e)));
        var agents = new List<AgentReport>();
        foreach (DemoEntity e in _world.Agents)
            if (e.At(s) is Merged m)
            {
                WorldPoint? pos = m.X is double x && m.Y is double y ? new WorldPoint(x, y) : null;
                AgentGraphLocation? graph = m.Edge is string edge ? new AgentGraphLocation(edge, m.Fraction ?? 0) : null;
                agents.Add(new AgentReport(e.Key, e.Name, e.Type!, m.Polity, m.Polity is null ? PolityRelation.None : PolityRelation.Owner,
                    pos, graph, m.AtSettlement, m.Heading, m.Count, null, e.Members, m.Visibility, Note(e), m.FirstStep));
            }
        return new StepReports(
            new Snapshot<SettlementReport>(seq, Label, true, settlements, r => r.Key),
            new Snapshot<PolityReport>(seq, Label, true, _world.Polities, r => r.Key),
            new Snapshot<StructureReport>(seq, Label, true, structures, r => r.Key),
            new Snapshot<InfraNodeReport>(seq, Label, true, nodes, r => r.Key),
            new Snapshot<InfraEdgeReport>(seq, Label, true, edges, r => r.Key),
            new Snapshot<ResourceReport>(seq, Label, true, resources, r => r.Key),
            new Snapshot<AgentReport>(seq, Label, true, agents, r => r.Key));
    }

    private static string Note(DemoEntity e) => e.Note is null ? DemoNote : DemoNote + "; " + e.Note;

    // ------------------------------------------------------------------ loading

    public static (DemoWorldSource? Source, IReadOnlyList<WorldDiagnostic> Diagnostics) LoadFile(string path, WorldMorphology morphology, int step = 0)
    {
        if (!File.Exists(path)) return (null, [new WorldDiagnostic(WorldContentLoader.DemoFile, "$", $"file not found: {path}")]);
        return Load(File.ReadAllText(path), morphology, step);
    }

    public static (DemoWorldSource? Source, IReadOnlyList<WorldDiagnostic> Diagnostics) Load(string json, WorldMorphology morphology, int step = 0)
    {
        var d = new WorldContentLoader.Diags(WorldContentLoader.DemoFile);
        DemoWorldDto? dto = WorldContentLoader.Parse<DemoWorldDto>(json, d);
        if (dto is null) return (null, d.List);
        DemoWorld? world = DemoWorld.Validate(dto, morphology, d);
        return world is null || d.HasErrors ? (null, d.List) : (new DemoWorldSource(world, step), d.List);
    }
}

/// <summary>An entity's merged state at one step.</summary>
internal sealed record Merged(
    long FirstStep, IReadOnlyList<ReportedInput> Inputs, IReadOnlyList<ReportedLabel> Labels, long? Multiplicity,
    string? Specialization, string? State, ReportedVisibility Visibility, string? Polity,
    double? X, double? Y, string? Edge, double? Fraction, string? AtSettlement, double? Heading, long? Count);

/// <summary>One validated demo entity and its timeline (entries sorted by step).</summary>
internal sealed record DemoEntity(
    string Key, string Name, string? Type, string? Settlement, string? Polity, double X, double Y, string? A, string? B,
    IReadOnlyList<string> Members, string? Note, IReadOnlyList<DemoEntryDto> Entries)
{
    public Merged? At(int step)
    {
        if (Entries.Count == 0 || Entries[0].Step > step) return null;
        var inputs = new List<ReportedInput>();
        var labels = new List<ReportedLabel>();
        long? multiplicity = null;
        string? spec = null, state = null, polity = Polity, atSettlement = null, edge = null;
        double? x = null, y = null, fraction = null, heading = null;
        long? count = null;
        ReportedVisibility vis = ReportedVisibility.Visible;
        foreach (DemoEntryDto e in Entries)
        {
            if (e.Step > step) break;
            if (e.Removed == true) return null;
            if (e.Inputs is not null)
                foreach (string name in e.Inputs.Keys.OrderBy(k => k, StringComparer.Ordinal))
                {
                    inputs.RemoveAll(i => i.Name == name);
                    if (e.Inputs[name] is double v) inputs.Add(new ReportedInput(name, v));
                }
            if (e.Labels is not null)
                foreach (string name in e.Labels.Keys.OrderBy(k => k, StringComparer.Ordinal))
                {
                    labels.RemoveAll(l => l.Name == name);
                    if (e.Labels[name] is string v) labels.Add(new ReportedLabel(name, v));
                }
            multiplicity = e.Multiplicity ?? multiplicity;
            spec = e.Specialization ?? spec;
            state = e.State ?? state;
            polity = e.Polity ?? polity;
            heading = e.Heading ?? heading;
            count = e.Count ?? count;
            if (e.Visibility is string vv) vis = DemoWorld.VisibilityOf(vv);
            // A location entry REPLACES the previous location, whichever of the three kinds it is.
            if (e.X is not null || e.Edge is not null || e.Settlement is not null)
            {
                x = e.X; y = e.Y; edge = e.Edge; fraction = e.Fraction; atSettlement = e.Settlement;
            }
        }
        inputs.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
        labels.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
        return new Merged(Entries[0].Step!.Value, inputs, labels, multiplicity, spec, state, vis, polity, x, y, edge, fraction,
            atSettlement, heading, count);
    }
}

/// <summary>The validated demo document.</summary>
internal sealed record DemoWorld(
    string Notice, double Width, double Height, IReadOnlyList<Scene.WorldRect> Water, IReadOnlyList<DemoStep> Steps, IReadOnlyList<PolityReport> Polities,
    IReadOnlyList<DemoEntity> Settlements, IReadOnlyList<DemoEntity> Structures, IReadOnlyList<DemoEntity> Nodes,
    IReadOnlyList<DemoEntity> Edges, IReadOnlyList<DemoEntity> Resources, IReadOnlyList<DemoEntity> Agents)
{
    public static ReportedVisibility VisibilityOf(string v) => v switch
    {
        "remembered" => ReportedVisibility.Remembered,
        "hidden" => ReportedVisibility.Hidden,
        _ => ReportedVisibility.Visible,
    };

    /// <summary>Inputs the demo may not report: a progress fraction would depict partial
    /// construction, which M4-D rejects and DD-08 leaves open.</summary>
    public static readonly string[] ForbiddenDemoInputs = ["progress", "percent", "turnsRemaining"];

    private enum Kind { Settlement, Structure, Node, Edge, Resource, Agent }

    public static DemoWorld? Validate(DemoWorldDto dto, WorldMorphology morph, WorldContentLoader.Diags d)
    {
        if (dto.Schema != DemoWorldSource.Schema) d.Error("schema", $"expected \"{DemoWorldSource.Schema}\", found \"{dto.Schema}\"");
        if (dto.Placeholder != true) d.Error("placeholder", "the demo world must declare \"placeholder\": true");
        string notice = d.Text(dto.Notice, "notice");
        if (!notice.Contains("DEMO", StringComparison.Ordinal)) d.Error("notice", "the notice must say DEMO");
        double width = 0, height = 0;
        if (dto.World is null) d.Error("world", "missing");
        else { width = d.Positive(dto.World.Width, "world.width"); height = d.Positive(dto.World.Height, "world.height"); }

        var water = new List<Scene.WorldRect>();
        for (int i = 0; i < (dto.Water?.Count ?? 0); i++)
        {
            DemoWaterDto w = dto.Water![i];
            string p = $"water[{i}]";
            water.Add(new Scene.WorldRect(d.Finite(w.X, p + ".x"), d.Finite(w.Y, p + ".y"), d.Positive(w.W, p + ".w"), d.Positive(w.H, p + ".h")));
        }

        var steps = new List<DemoStep>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < (dto.Steps?.Count ?? 0); i++)
        {
            DemoStepDto st = dto.Steps![i];
            steps.Add(new DemoStep(d.Id(st.Id, $"steps[{i}].id", ids), d.Text(st.Name, $"steps[{i}].name"), d.Text(st.Caption, $"steps[{i}].caption")));
        }
        if (steps.Count == 0) d.Error("steps", "at least one step is required");

        var polities = new List<PolityReport>();
        var polityKeys = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < (dto.Polities?.Count ?? 0); i++)
        {
            DemoPolityDto p = dto.Polities![i];
            string key = d.Id(p.Key, $"polities[{i}].key", polityKeys);
            polities.Add(new PolityReport(key, d.Text(p.Name, $"polities[{i}].name"), p.InkSeed ?? 0,
                "DEMO / PLACEHOLDER polity: authored demonstration content"));
        }

        var settlementKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (DemoEntityDto e in dto.Settlements ?? []) if (e.Key is not null) settlementKeys.Add(e.Key);
        var nodeKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (DemoEntityDto e in dto.Nodes ?? []) if (e.Key is not null) nodeKeys.Add(e.Key);
        var edgeKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (DemoEntityDto e in dto.Edges ?? []) if (e.Key is not null) edgeKeys.Add(e.Key);

        List<DemoEntity> Entities(List<DemoEntityDto>? list, string section, Kind kind)
        {
            var result = new List<DemoEntity>();
            var keys = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < (list?.Count ?? 0); i++)
            {
                DemoEntityDto e = list![i];
                string p = $"{section}[{i}]";
                string key = d.Id(e.Key, p + ".key", keys);
                string name = d.Text(e.Name, p + ".name");
                if (e.Note is not null) d.Text(e.Note, p + ".note");
                if (e.Polity is not null && !polityKeys.Contains(e.Polity)) d.Error(p + ".polity", $"unknown polity '{e.Polity}'");

                string? type = e.Type;
                switch (kind)
                {
                    case Kind.Settlement:
                        if (type is not null) d.Error(p + ".type", "a settlement has no type");
                        break;
                    case Kind.Structure:
                        if (type is null || morph.Visual(type) is null) d.Error(p + ".type", $"unknown visual type '{type}'");
                        break;
                    case Kind.Node:
                        if (type is null || morph.Node(type) is null) d.Error(p + ".type", $"unknown node type '{type}'");
                        break;
                    case Kind.Edge:
                        if (type is null || morph.Infra(type) is null) d.Error(p + ".type", $"unknown infrastructure type '{type}'");
                        break;
                    case Kind.Resource:
                        if (type is null) d.Error(p + ".type", "missing");
                        break;
                    case Kind.Agent:
                        if (type is null || morph.Agent(type) is null) d.Error(p + ".type", $"unknown agent type '{type}'");
                        break;
                }

                bool needsSettlement = kind is Kind.Structure or Kind.Resource;
                if (needsSettlement && (e.Settlement is null || !settlementKeys.Contains(e.Settlement)))
                    d.Error(p + ".settlement", $"unknown settlement '{e.Settlement}'");
                if (!needsSettlement && kind != Kind.Node && e.Settlement is not null)
                    d.Error(p + ".settlement", "only structures, resources and nodes name a settlement here");
                if (kind == Kind.Node && e.Settlement is not null && !settlementKeys.Contains(e.Settlement))
                    d.Error(p + ".settlement", $"unknown settlement '{e.Settlement}'");

                bool placed = kind is Kind.Settlement or Kind.Node;
                if (placed)
                {
                    double x = d.Finite(e.X, p + ".x"), y = d.Finite(e.Y, p + ".y");
                    if (x < 0 || y < 0 || x > width || y > height) d.Error(p, "position lies outside the demo world");
                }
                else if (e.X is not null || e.Y is not null) d.Error(p, "x/y belong on a settlement or node (agents place per timeline entry)");

                if (kind == Kind.Edge)
                {
                    if (e.A is null || !nodeKeys.Contains(e.A)) d.Error(p + ".a", $"unknown node '{e.A}'");
                    if (e.B is null || !nodeKeys.Contains(e.B)) d.Error(p + ".b", $"unknown node '{e.B}'");
                    if (e.A is not null && e.A == e.B) d.Error(p, "an edge joins two different nodes");
                }
                else if (e.A is not null || e.B is not null) d.Error(p, "a/b belong on an edge");
                if (e.Members is not null && kind != Kind.Agent) d.Error(p + ".members", "only agents have members");
                var members = new List<string>();
                for (int m = 0; m < (e.Members?.Count ?? 0); m++) members.Add(d.Text(e.Members![m], $"{p}.members[{m}]"));

                var entries = (e.Timeline ?? []).ToList();
                if (entries.Count == 0) d.Error(p + ".timeline", "at least one entry is required");
                entries.Sort((a, b) => (a.Step ?? -1).CompareTo(b.Step ?? -1));
                for (int t = 0; t < entries.Count; t++)
                    Entry(entries[t], $"{p}.timeline(step {entries[t].Step})", kind, t == 0, t == entries.Count - 1,
                        t > 0 && entries[t].Step == entries[t - 1].Step);
                result.Add(new DemoEntity(key, name, type, e.Settlement, e.Polity, e.X ?? 0, e.Y ?? 0, e.A, e.B, members, e.Note, entries));
            }
            return result;
        }

        void Entry(DemoEntryDto t, string p, Kind kind, bool first, bool last, bool duplicate)
        {
            if (t.Step is not int s || s < 0 || s >= steps.Count) d.Error(p + ".step", $"must be a step index in 0..{steps.Count - 1}");
            if (duplicate) d.Error(p, "two entries for the same step");
            if (t.Removed == true)
            {
                if (first) d.Error(p, "an entity cannot be removed at its first entry");
                if (!last) d.Error(p, "nothing may follow a removal");
            }
            if (t.Visibility is not null) d.OneOf(t.Visibility, p + ".visibility", "visible", "remembered", "hidden");
            if (t.Polity is not null && !polityKeys.Contains(t.Polity)) d.Error(p + ".polity", $"unknown polity '{t.Polity}'");
            if (t.Inputs is not null)
                foreach (string name in t.Inputs.Keys.OrderBy(k => k, StringComparer.Ordinal))
                {
                    d.Text(name, p + ".inputs");
                    if (Array.Exists(ForbiddenDemoInputs, f => string.Equals(f, name, StringComparison.OrdinalIgnoreCase)))
                        d.Error($"{p}.inputs.{name}", "the demo may not report construction progress (M4-D has none; DD-08 is open)");
                    if (t.Inputs[name] is double v && !double.IsFinite(v)) d.Error($"{p}.inputs.{name}", "must be finite");
                }
            if (t.Labels is not null)
                foreach (string name in t.Labels.Keys.OrderBy(k => k, StringComparer.Ordinal))
                {
                    d.Text(name, p + ".labels");
                    if (t.Labels[name] is string v) d.Text(v, $"{p}.labels.{name}");
                }
            bool structure = kind == Kind.Structure;
            if (!structure && (t.Multiplicity is not null || t.Specialization is not null || t.State is not null || t.Labels is not null))
                d.Error(p, "multiplicity/specialization/state/labels belong on a structure");
            if (t.Multiplicity is long mm && mm < 1) d.Error(p + ".multiplicity", "must be at least 1");
            if (t.Specialization is not null && morph.Specialization(t.Specialization) is null) d.Error(p + ".specialization", $"unknown specialization '{t.Specialization}'");
            if (t.State is not null && morph.State(t.State) is null) d.Error(p + ".state", $"unknown state '{t.State}'");

            bool agent = kind == Kind.Agent;
            if (!agent && (t.X is not null || t.Y is not null || t.Edge is not null || t.Fraction is not null || t.Settlement is not null
                || t.Heading is not null || t.Count is not null))
                d.Error(p, "location/heading/count belong on an agent");
            if (agent)
            {
                int kinds = (t.X is not null || t.Y is not null ? 1 : 0) + (t.Edge is not null ? 1 : 0) + (t.Settlement is not null ? 1 : 0);
                if (kinds > 1) d.Error(p, "an agent is at a position, on an edge, or at a settlement - one of them");
                if (first && kinds == 0) d.Error(p, "an agent's first entry must say where it is");
                if ((t.X is null) != (t.Y is null)) d.Error(p, "x and y come together");
                if (t.X is double x && t.Y is double y && (!double.IsFinite(x) || !double.IsFinite(y) || x < 0 || y < 0 || x > width || y > height))
                    d.Error(p, "position lies outside the demo world");
                if (t.Edge is not null && !edgeKeys.Contains(t.Edge)) d.Error(p + ".edge", $"unknown edge '{t.Edge}'");
                if ((t.Edge is null) != (t.Fraction is null)) d.Error(p, "edge and fraction come together");
                if (t.Fraction is double f && !(f >= 0 && f <= 1)) d.Error(p + ".fraction", "must lie in [0, 1]");
                if (t.Settlement is not null && !settlementKeys.Contains(t.Settlement)) d.Error(p + ".settlement", $"unknown settlement '{t.Settlement}'");
                if (t.Heading is double h && !double.IsFinite(h)) d.Error(p + ".heading", "must be finite");
                if (t.Count is long c && c < 0) d.Error(p + ".count", "must not be negative");
            }
        }

        var settlements = Entities(dto.Settlements, "settlements", Kind.Settlement);
        var structures = Entities(dto.Structures, "structures", Kind.Structure);
        var nodes = Entities(dto.Nodes, "nodes", Kind.Node);
        var edges = Entities(dto.Edges, "edges", Kind.Edge);
        var resources = Entities(dto.Resources, "resources", Kind.Resource);
        var agents = Entities(dto.Agents, "agents", Kind.Agent);
        if (d.HasErrors) return null;
        return new DemoWorld(notice, width, height, water, steps, polities, settlements, structures, nodes, edges, resources, agents);
    }

    internal static string Format(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
}
