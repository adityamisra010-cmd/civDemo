using System.Text.Json;
using Sim.Ui.Trees.Json;

namespace Sim.Ui.Trees;

/// <summary>
/// PLACEHOLDER STATE SOURCE — stands in for simulation state that does not exist yet.
///
/// Reads ui-content/trees/demo-state.json: a short script of STEPS, each listing only what
/// changed from the step before, so the UI can show state changes (and the animations
/// they trigger) before any system produces them. Stepping the script is a UI-only
/// preview control: it changes which placeholder snapshot is shown and nothing else. It
/// is not an order, never reaches the simulation, and is labelled PLACEHOLDER wherever
/// its values are drawn.
///
/// It enforces the invariants the real state will have to meet, so the UI is built
/// against them: every node state belongs to the node type's state set, fractions lie in
/// [0,1], and the Age never moves backward from one step to the next.
/// </summary>
public sealed class DemoStateSource : ITreesStateSource, IAgeStateSource, IGalleryStateSource
{
    public const string Schema = "civ-sim/trees-demo-state@1";
    public const string Label = "PLACEHOLDER · demo-state.json";

    private readonly TreesStateSnapshot[] _trees;
    private readonly AgeStateSnapshot[] _ages;
    private readonly GallerySnapshot[] _gallery;
    private readonly string[] _labels;

    private DemoStateSource(TreesStateSnapshot[] trees, AgeStateSnapshot[] ages, GallerySnapshot[] gallery, string[] labels)
    {
        _trees = trees; _ages = ages; _gallery = gallery; _labels = labels;
    }

    public int StepCount => _labels.Length;
    public int Step { get; private set; }
    public string StepLabel => _labels[Step];

    TreesStateSnapshot ITreesStateSource.Current => _trees[Step];
    AgeStateSnapshot IAgeStateSource.Current => _ages[Step];
    GallerySnapshot IGalleryStateSource.Current => _gallery[Step];

    public TreesStateSnapshot Trees => _trees[Step];
    public AgeStateSnapshot Ages => _ages[Step];
    public GallerySnapshot Gallery => _gallery[Step];

    /// <summary>Show step <paramref name="step"/> (clamped). A preview control only.</summary>
    public void SetStep(int step) => Step = System.Math.Clamp(step, 0, StepCount - 1);
    public void Next() => SetStep(Step + 1 >= StepCount ? 0 : Step + 1);
    public void Previous() => SetStep(Step == 0 ? StepCount - 1 : Step - 1);

    public static (DemoStateSource? Source, IReadOnlyList<ContentDiagnostic> Diagnostics) LoadFile(string path, TreesContentSet content)
    {
        if (!File.Exists(path))
            return (null, [new(DiagnosticSeverity.Error, TreesContentLoader.DemoStateFile, "$", $"file not found: {path}")]);
        return Load(File.ReadAllText(path), content);
    }

    public static (DemoStateSource? Source, IReadOnlyList<ContentDiagnostic> Diagnostics) Load(string json, TreesContentSet content)
    {
        const string F = TreesContentLoader.DemoStateFile;
        var diags = new List<ContentDiagnostic>();
        void Error(string path, string msg) => diags.Add(new(DiagnosticSeverity.Error, F, path, msg));
        void Warn(string path, string msg) => diags.Add(new(DiagnosticSeverity.Warning, F, path, msg));

        DemoStateFileDto? dto;
        try { dto = JsonSerializer.Deserialize<DemoStateFileDto>(json, TreesJsonOptions.Options); }
        catch (JsonException ex) { Error(ex.Path ?? "$", $"invalid JSON at line {ex.LineNumber + 1}: {ex.Message}"); return (null, diags); }
        if (dto is null) { Error("$", "empty"); return (null, diags); }
        if (dto.Schema != Schema) Error("schema", $"expected '{Schema}', found '{dto.Schema}'");
        List<DemoStepDto> steps = dto.Steps ?? [];
        if (steps.Count == 0) { Error("steps", "at least one step is required"); return (null, diags); }

        TreesDocument trees = content.Trees;
        AgesDocument ages = content.Ages;
        GalleryDocument gallery = content.Gallery;
        var nodeIds = new HashSet<string>(trees.Nodes.Select(n => n.Id), StringComparer.Ordinal);

        static bool Fraction(double? v) => v is null || (v >= 0.0 && v <= 1.0);

        // Running (merged) state.
        var nodes = new Dictionary<string, NodeStatus>(StringComparer.Ordinal);
        var research = new ResearchHeader(null, null, "");
        string? currentAge = null; double? ageProgress = null; AgeTransitionStatus? transition = null;
        var milestones = new Dictionary<string, MilestoneStatus>(StringComparer.Ordinal);
        var buildings = new List<BuildingStatus>();
        var units = new List<UnitStatus>();
        int? diffusion = null;

        var treesOut = new TreesStateSnapshot[steps.Count];
        var agesOut = new AgeStateSnapshot[steps.Count];
        var galleryOut = new GallerySnapshot[steps.Count];
        var labels = new string[steps.Count];
        string civ = dto.Civilization?.Name ?? "Civilization (placeholder)";

        for (int s = 0; s < steps.Count; s++)
        {
            DemoStepDto step = steps[s];
            string p = $"steps[{s}]";
            labels[s] = step.Label ?? $"Step {s}";

            if (step.Research is not null)
            {
                string? target = step.Research.CurrentTarget ?? research.CurrentTarget;
                if (target is not null && !nodeIds.Contains(target)) Error(p + ".research.currentTarget", $"unknown node '{target}'");
                if (step.Research.PointsPerTurn is long ppt && ppt < 0) Error(p + ".research.pointsPerTurn", "must be ≥ 0");
                research = new ResearchHeader(step.Research.PointsPerTurn ?? research.PointsPerTurn, target,
                    step.Research.Note ?? research.Note);
            }

            List<DemoNodeDto> ns = step.Nodes ?? [];
            for (int i = 0; i < ns.Count; i++)
            {
                DemoNodeDto n = ns[i];
                string np = $"{p}.nodes[{i}]";
                if (n.Id is null || !nodeIds.Contains(n.Id)) { Error(np + ".id", $"unknown node '{n.Id}'"); continue; }
                NodeDef def = trees.Nodes.First(x => x.Id == n.Id);
                IReadOnlyList<string> allowed = trees.StatesFor(def.Type);
                if (n.State is null || !allowed.Contains(n.State))
                {
                    Error(np + ".state", $"'{n.State}' is not in the '{def.Type}' state set ({string.Join(", ", allowed)})");
                    continue;
                }
                if (!Fraction(n.ResearchProgress)) Error(np + ".researchProgress", "must be in [0,1]");
                if (!Fraction(n.RealizationProgress)) Error(np + ".realizationProgress", "must be in [0,1]");
                if (n.ResearchPoints is long rp && rp < 0) Error(np + ".researchPoints", "must be ≥ 0");
                nodes[n.Id] = new NodeStatus(n.Id, n.State, n.ResearchProgress, n.ResearchPoints, n.RealizationProgress, n.Note ?? "");
            }

            if (step.Ages is not null)
            {
                string? cur = step.Ages.Current;
                if (cur is null || !ages.TryAge(cur, out AgeDef curAge)) { Error(p + ".ages.current", $"unknown Age '{cur}'"); }
                else
                {
                    if (currentAge is not null && curAge.Order < ages.Age(currentAge).Order)
                        Error(p + ".ages.current", $"'{cur}' precedes '{currentAge}' — Ages move forward only");
                    currentAge = cur;
                }
                if (!Fraction(step.Ages.Progress)) Error(p + ".ages.progress", "must be in [0,1]");
                ageProgress = step.Ages.Progress;
                transition = null;
                if (step.Ages.Transition is DemoTransitionDto t)
                {
                    if (t.To is not null && ages.TryAge(t.To, out AgeDef to) && currentAge is not null && to.Order <= ages.Age(currentAge).Order)
                        Error(p + ".ages.transition.to", $"'{t.To}' is not after the current Age — Ages move forward only");
                    if (t.To is not null && !ages.TryAge(t.To, out _)) Error(p + ".ages.transition.to", $"unknown Age '{t.To}'");
                    transition = new AgeTransitionStatus(t.Pending ?? false, t.To, t.Note ?? "");
                }
            }
            if (currentAge is null) { Error(p + ".ages", "the first step must report the current Age"); currentAge = ages.InOrder()[0].Id; }

            List<DemoMilestoneDto> ms = step.Milestones ?? [];
            for (int i = 0; i < ms.Count; i++)
            {
                DemoMilestoneDto m = ms[i];
                string mp = $"{p}.milestones[{i}]";
                if (m.Id is null || !ages.TryMilestone(m.Id, out _)) { Error(mp + ".id", $"unknown milestone '{m.Id}'"); continue; }
                MilestoneCompletion completion = m.Completion switch
                {
                    "not-started" => MilestoneCompletion.NotStarted,
                    "partial" => MilestoneCompletion.Partial,
                    "complete" => MilestoneCompletion.Complete,
                    _ => (MilestoneCompletion)(-1),
                };
                if ((int)completion < 0) { Error(mp + ".completion", $"'{m.Completion}' is not one of: not-started, partial, complete"); continue; }
                if (!Fraction(m.Progress)) Error(mp + ".progress", "must be in [0,1]");
                milestones[m.Id] = new MilestoneStatus(m.Id, completion, m.Progress, m.Evidence ?? "");
            }

            List<DemoBuildingDto> bs = step.Buildings ?? [];
            for (int i = 0; i < bs.Count; i++)
            {
                DemoBuildingDto b = bs[i];
                string bp = $"{p}.buildings[{i}]";
                if (b.Building is null || !gallery.Buildings.Any(x => x.Id == b.Building)) { Error(bp + ".building", $"unknown building '{b.Building}'"); continue; }
                if (b.Maturity is not null && gallery.Maturity(b.Maturity) is null) Error(bp + ".maturity", $"unknown maturity stage '{b.Maturity}'");
                if (b.Status is null || gallery.OperationalStatus(b.Status) is null) Error(bp + ".status", $"unknown operational status '{b.Status}'");
                if (b.AgeBuilt is not null && !ages.TryAge(b.AgeBuilt, out _)) Error(bp + ".ageBuilt", $"unknown Age '{b.AgeBuilt}'");
                if (!Fraction(b.MaturityProgress) || !Fraction(b.ConstructionProgress)) Error(bp, "progress fields must be in [0,1]");
                var status = new BuildingStatus(b.Building, b.Maturity, b.MaturityProgress ?? 0.0, b.ConstructionProgress ?? 0.0,
                    b.Personnel?.Current, b.Personnel?.Capacity, b.Capacity ?? "", b.Specialization, b.AgeBuilt, b.Status ?? "operational");
                int at = buildings.FindIndex(x => x.BuildingId == b.Building);
                if (at >= 0) buildings[at] = status; else buildings.Add(status);
            }

            List<DemoUnitDto> us = step.Units ?? [];
            for (int i = 0; i < us.Count; i++)
            {
                DemoUnitDto u = us[i];
                string up = $"{p}.units[{i}]";
                if (u.Unit is null || !gallery.Units.Any(x => x.Id == u.Unit)) { Error(up + ".unit", $"unknown unit '{u.Unit}'"); continue; }
                if (u.State is null || gallery.UnitState(u.State) is null) Error(up + ".state", $"unknown unit state '{u.State}'");
                if (u.Veterancy is null || gallery.Veterancy(u.Veterancy) is null) Error(up + ".veterancy", $"unknown veterancy level '{u.Veterancy}'");
                if (!Fraction(u.Strength) || !Fraction(u.ExperienceProgress) || !Fraction(u.Cohesion)) Error(up, "fractions must be in [0,1]");
                if (u.Doctrine is not null && !nodeIds.Contains(u.Doctrine)) Warn(up + ".doctrine", $"'{u.Doctrine}' is not a node id");
                var status = new UnitStatus(u.Unit, u.State ?? "ready", u.Strength ?? 1.0, u.Experience, u.Veterancy ?? "recruit",
                    u.ExperienceProgress ?? 0.0, u.Training ?? "", u.Recovery ?? "", u.Doctrine, u.Cohesion ?? 0.0);
                int at = units.FindIndex(x => x.UnitId == u.Unit);
                if (at >= 0) units[at] = status; else units.Add(status);
            }

            if (step.Diffusion?.Stage is int ds)
            {
                if (ds < 0 || ds > 4) Error(p + ".diffusion.stage", "must be 0..4");
                diffusion = ds;
            }

            long seq = s + 1;
            treesOut[s] = new TreesStateSnapshot(seq, Label, true, civ, research, nodes.Values);
            agesOut[s] = new AgeStateSnapshot(seq, Label, true, currentAge, ageProgress, transition, milestones.Values);
            galleryOut[s] = new GallerySnapshot(seq, Label, true, buildings.ToArray(), units.ToArray(), diffusion);
        }

        // Nodes the script never mentions render in the first state of their set; say so.
        foreach (NodeDef n in trees.Nodes)
            if (treesOut[0].Status(n.Id) is null)
                Warn("steps[0].nodes", $"node '{n.Id}' has no state in the first step; it is shown as '{trees.StatesFor(n.Type)[0]}'");

        if (diags.Any(x => x.Severity == DiagnosticSeverity.Error)) return (null, diags);
        return (new DemoStateSource(treesOut, agesOut, galleryOut, labels), diags);
    }
}

/// <summary>A source with nothing to report: every node shows the first state of its set,
/// the first Age is current, and the gallery is empty. Used when no state file exists, so
/// content can be edited and previewed on its own.</summary>
public sealed class NoStateSource : ITreesStateSource, IAgeStateSource, IGalleryStateSource
{
    public const string Label = "NO STATE SOURCE · content preview";
    private readonly TreesStateSnapshot _trees;
    private readonly AgeStateSnapshot _ages;
    private readonly GallerySnapshot _gallery;

    public NoStateSource(TreesContentSet content)
    {
        _trees = new TreesStateSnapshot(0, Label, true, "—", new ResearchHeader(null, null, ""), []);
        _ages = new AgeStateSnapshot(0, Label, true, content.Ages.InOrder()[0].Id, null, null, []);
        _gallery = new GallerySnapshot(0, Label, true, [], [], null);
    }

    public TreesStateSnapshot Current => _trees;
    AgeStateSnapshot IAgeStateSource.Current => _ages;
    GallerySnapshot IGalleryStateSource.Current => _gallery;
}
