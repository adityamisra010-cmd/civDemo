namespace Sim.Ui.Trees.View;

/// <summary>A node's motion at one instant. <see cref="OneShotT"/> runs 0→1 while a
/// transition animation plays and is −1 otherwise.</summary>
public readonly record struct NodeMotion(
    double HaloAlpha, double HaloScale, AnimationKind OneShot, double OneShotT,
    bool NewlyCompleted, bool NewlyDiscovered, double PipFillT)
{
    public static readonly NodeMotion Still = new(0.0, 1.0, AnimationKind.None, -1.0, false, false, -1.0);
    public bool Moving => HaloAlpha > 0.0 || OneShotT >= 0.0 || PipFillT >= 0.0;
}

public readonly record struct AgeMotion(double TransitionSweepT, double EnteredFlashT);
public readonly record struct BuildingMotion(double SweepT, double PipFillT);
public readonly record struct UnitMotion(double PulseAlpha, double FlashT);

/// <summary>
/// THE ANIMATOR — state-driven, deterministic, never authoritative (task §13).
///
/// It OBSERVES snapshots (with the UI clock's time of observation) and remembers, per
/// thing, which state it was last seen in and when that state was first seen. Every
/// sample is then a pure function of (what was observed, when, the time asked about):
/// no RNG, no wall clock, no frame counter. The same observation sequence always yields
/// the same motion, which is what the tests pin.
///
/// It cannot be authoritative: it reads immutable snapshots, returns plain motion values,
/// and no screen ever derives a STATE from it — a node's glyph comes from its reported
/// state alone; only the halo, flash and dots around it come from here. The first
/// observation establishes the baseline and animates nothing, so opening the screen never
/// replays history.
/// </summary>
public sealed class StateAnimator
{
    /// <summary>How long a node keeps its "newly completed / newly discovered" marker.</summary>
    public const double NewlyWindowSeconds = 12.0;

    private sealed class Track
    {
        public string State = "";
        public int Stage;
        // For nodes: the role and ring of the state entered at EnteredAt, and of the one before.
        public ResearchRole Role, PrevRole;
        public Art.Glyphs.GlyphState Ring, PrevRing;
        public double FirstSeen;
        public double EnteredAt = double.NegativeInfinity;
        public double StageUpAt = double.NegativeInfinity;
    }

    private readonly AnimationDocument _defs;
    private readonly Dictionary<string, Track> _nodes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Track> _buildings = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Track> _units = new(StringComparer.Ordinal);
    private long _treesSeq = long.MinValue, _agesSeq = long.MinValue, _gallerySeq = long.MinValue;
    private string? _age;
    private double _ageEnteredAt = double.NegativeInfinity;
    private double _transitionSeenAt = double.NegativeInfinity;
    private bool _transitionPending;

    public StateAnimator(AnimationDocument definitions) => _defs = definitions;

    public AnimationDocument Definitions => _defs;

    /// <summary>Record what the sources report at UI time <paramref name="now"/> (seconds).
    /// Re-observing an unchanged snapshot (same sequence) is a no-op.</summary>
    public void Observe(TreesDocument trees, TreesStateSnapshot t, AgeStateSnapshot a, GallerySnapshot g,
        AgesDocument ages, GalleryDocument gallery, double now)
    {
        if (t.Sequence != _treesSeq)
        {
            bool baseline = _treesSeq == long.MinValue;
            _treesSeq = t.Sequence;
            foreach (NodeDef def in trees.Nodes)
            {
                (StateDef state, _, _) = TreesQueryState.Resolve(trees, def, t.Status(def.Id));
                bool known = _nodes.ContainsKey(def.Id);
                Track tr = Step(_nodes, def.Id, state.Id, state.Stage, baseline, now, out bool changed);
                if (!known)
                {
                    // First sight of this node: no previous state to compare with.
                    tr.Role = tr.PrevRole = state.ResearchRole;
                    tr.Ring = tr.PrevRing = state.GlyphState;
                }
                else if (changed)
                {
                    tr.PrevRole = tr.Role; tr.PrevRing = tr.Ring;
                    tr.Role = state.ResearchRole; tr.Ring = state.GlyphState;
                }
            }
        }
        if (a.Sequence != _agesSeq)
        {
            bool baseline = _agesSeq == long.MinValue;
            _agesSeq = a.Sequence;
            if (!baseline && _age is not null && a.CurrentAgeId != _age
                && ages.TryAge(a.CurrentAgeId, out AgeDef next) && ages.TryAge(_age, out AgeDef prev) && next.Order > prev.Order)
                _ageEnteredAt = now;
            // Forward only: a report of an earlier Age is not animated as an entry.
            if (_age is null || !ages.TryAge(_age, out AgeDef cur) || !ages.TryAge(a.CurrentAgeId, out AgeDef rep) || rep.Order >= cur.Order)
                _age = a.CurrentAgeId;
            bool pending = a.Transition?.Pending ?? false;
            if (pending && !_transitionPending) _transitionSeenAt = now;
            _transitionPending = pending;
        }
        if (g.Sequence != _gallerySeq)
        {
            bool baseline = _gallerySeq == long.MinValue;
            _gallerySeq = g.Sequence;
            foreach (BuildingStatus b in g.Buildings)
                Step(_buildings, b.BuildingId, b.OperationalStatus, gallery.Maturity(b.MaturityStage)?.Stage ?? 0, baseline, now, out _);
            foreach (UnitStatus u in g.Units)
                Step(_units, u.UnitId, u.State, gallery.Veterancy(u.VeterancyLevel)?.Chevrons ?? 0, baseline, now, out _);
        }
    }

    private static Track Step(Dictionary<string, Track> tracks, string id, string state, int stage, bool baseline, double now, out bool changed)
    {
        changed = false;
        if (!tracks.TryGetValue(id, out Track? tr))
        {
            tr = new Track { State = state, Stage = stage, FirstSeen = now, EnteredAt = baseline ? double.NegativeInfinity : now };
            tracks[id] = tr;
            return tr;
        }
        if (tr.State != state) { tr.State = state; tr.EnteredAt = now; changed = true; }
        if (stage > tr.Stage) tr.StageUpAt = now;
        tr.Stage = stage;
        return tr;
    }

    // --- sampling --------------------------------------------------------------------------

    public NodeMotion Node(string nodeId, double now)
    {
        if (!_nodes.TryGetValue(nodeId, out Track? tr)) return NodeMotion.Still;
        double halo = 0.0, scale = 1.0;
        AnimationDef? steady = _defs.Steady("node-state:" + tr.State);
        if (steady is not null && steady.Kind == AnimationKind.Pulse)
        {
            double anchor = double.IsNegativeInfinity(tr.EnteredAt) ? tr.FirstSeen : tr.EnteredAt;
            double phase = Frac((now - anchor) / steady.PeriodSeconds);
            halo = steady.Amplitude * (0.5 - 0.5 * System.Math.Cos(2.0 * System.Math.PI * phase));
            scale = 1.0 + 0.12 * halo / System.Math.Max(0.01, steady.Amplitude);
        }

        AnimationKind kind = AnimationKind.None;
        double t = -1.0;
        AnimationDef? enter = _defs.Transition("node-enter:" + tr.State);
        if (enter is not null) (kind, t) = OneShot(enter, tr.EnteredAt, now);
        double since = now - tr.EnteredAt;
        bool newly = since >= 0.0 && since < NewlyWindowSeconds;
        double pip = -1.0;
        AnimationDef? stageUp = _defs.Transition("node-stage-up");
        if (stageUp is not null) pip = OneShot(stageUp, tr.StageUpAt, now).T;
        // By ROLE, not by state id, so renaming a state in content keeps the markers:
        // newly completed = entered a completed-research state from one that was not;
        // newly discovered = left a locked ring for anything else.
        bool completed = tr.Role == ResearchRole.Completed && tr.PrevRole != ResearchRole.Completed;
        bool discovered = !completed && tr.PrevRing == Art.Glyphs.GlyphState.Locked && tr.Ring != Art.Glyphs.GlyphState.Locked;
        return new NodeMotion(halo, scale, kind, t, newly && completed, newly && discovered, pip);
    }

    /// <summary>Positions (0..1 along the edge) of the flow dots for a steady edge
    /// animation, or empty. Anchored to t = 0 of the UI clock, phase-shifted per edge.</summary>
    public double[] EdgeDots(string kindId, int edgeIndex, double now)
    {
        AnimationDef? def = _defs.Steady("edge-kind:" + kindId);
        if (def is null || def.Kind != AnimationKind.FlowDots) return [];
        var dots = new double[def.Dots];
        double baseT = Frac(now / def.PeriodSeconds + 0.137 * edgeIndex);
        for (int k = 0; k < dots.Length; k++) dots[k] = Frac(baseT + k / (double)dots.Length);
        return dots;
    }

    public AgeMotion Age(double now)
    {
        double sweep = -1.0;
        AnimationDef? tr = _defs.Steady("age:transition");
        if (_transitionPending && tr is not null)
            sweep = Frac((now - _transitionSeenAt) / tr.PeriodSeconds);
        double flash = -1.0;
        AnimationDef? enter = _defs.Transition("age-enter");
        if (enter is not null) flash = OneShot(enter, _ageEnteredAt, now).T;
        return new AgeMotion(sweep, flash);
    }

    public BuildingMotion Building(string buildingId, double now)
    {
        if (!_buildings.TryGetValue(buildingId, out Track? tr)) return new BuildingMotion(-1.0, -1.0);
        double sweep = -1.0;
        AnimationDef? steady = _defs.Steady("building-status:" + tr.State);
        if (steady is not null && steady.Kind == AnimationKind.Sweep)
        {
            double anchor = double.IsNegativeInfinity(tr.EnteredAt) ? tr.FirstSeen : tr.EnteredAt;
            sweep = Frac((now - anchor) / steady.PeriodSeconds);
        }
        AnimationDef? up = _defs.Transition("building-stage-up");
        return new BuildingMotion(sweep, up is null ? -1.0 : OneShot(up, tr.StageUpAt, now).T);
    }

    public UnitMotion Unit(string unitId, double now)
    {
        if (!_units.TryGetValue(unitId, out Track? tr)) return new UnitMotion(0.0, -1.0);
        double pulse = 0.0;
        AnimationDef? steady = _defs.Steady("unit-state:" + tr.State);
        if (steady is not null && steady.Kind == AnimationKind.Pulse)
        {
            double anchor = double.IsNegativeInfinity(tr.EnteredAt) ? tr.FirstSeen : tr.EnteredAt;
            pulse = steady.Amplitude * (0.5 - 0.5 * System.Math.Cos(2.0 * System.Math.PI * Frac((now - anchor) / steady.PeriodSeconds)));
        }
        AnimationDef? up = _defs.Transition("unit-veterancy-up");
        return new UnitMotion(pulse, up is null ? -1.0 : OneShot(up, tr.StageUpAt, now).T);
    }

    private static (AnimationKind Kind, double T) OneShot(AnimationDef def, double startedAt, double now)
    {
        if (double.IsNegativeInfinity(startedAt)) return (AnimationKind.None, -1.0);
        double t = (now - startedAt) / def.DurationSeconds;
        return t >= 0.0 && t < 1.0 ? (def.Kind, t) : (AnimationKind.None, -1.0);
    }

    private static double Frac(double x) => x - System.Math.Floor(x);
}

/// <summary>Resolving a REPORTED state against the node type's state set — shared by the
/// query and the animator so both see the same state.</summary>
public static class TreesQueryState
{
    /// <summary>The state to show: the reported one when it belongs to the type's set;
    /// otherwise the first state of the set (reported = false when nothing was reported,
    /// valid = false when what was reported is not allowed).</summary>
    public static (StateDef State, bool Reported, bool Valid) Resolve(TreesDocument c, NodeDef def, NodeStatus? status)
    {
        IReadOnlyList<string> allowed = c.StatesFor(def.Type);
        bool reported = status is not null;
        bool valid = reported && allowed.Contains(status!.StateId) && c.TryState(status.StateId, out _);
        return (c.State(valid ? status!.StateId : allowed[0]), reported, valid);
    }
}

/// <summary>
/// FORWARD-ONLY AGE, AS A DISPLAY INVARIANT. Ages never regress (task §3). If a source ever
/// reports an earlier Age than one already shown, the view keeps showing the later Age and
/// raises a visible warning rather than silently drawing history backward. The guard
/// decides nothing about the simulation; it only refuses to render an impossible report.
/// </summary>
public sealed class AgeForwardGuard
{
    public string? DisplayedAgeId { get; private set; }
    public bool RegressionReported { get; private set; }
    public string? LastReportedAgeId { get; private set; }

    public void Observe(AgeStateSnapshot s, AgesDocument ages)
    {
        LastReportedAgeId = s.CurrentAgeId;
        if (!ages.TryAge(s.CurrentAgeId, out AgeDef reported)) return;
        if (DisplayedAgeId is null || !ages.TryAge(DisplayedAgeId, out AgeDef shown) || reported.Order >= shown.Order)
        {
            DisplayedAgeId = reported.Id;
            RegressionReported = false;
        }
        else RegressionReported = true;
    }
}
