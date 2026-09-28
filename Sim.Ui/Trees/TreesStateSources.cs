namespace Sim.Ui.Trees;

// THE STATE BOUNDARY (docs/architecture/the-trees-ui.md §7).
//
// The UI is a READ-ONLY OBSERVER. It learns a node's state, a research total, an Age's
// progress or a unit's veterancy only through these interfaces, and it has no way to
// write any of them back: every type here is immutable and no interface has a setter or
// a command.
//
// The simulation does not carry any of this state yet (pre-m5-repository-audit.md §1:
// nothing in Sim.Core names a research domain, an Age milestone, building maturity or
// veterancy). So today the only implementation is PLACEHOLDER — DemoStateSource, reading
// ui-content/trees/demo-state.json. When the simulation grows the real state, an
// adapter over IReadOnlyWorldState / the observation records implements these same
// interfaces and nothing above this line changes. Deciding WHAT that state is belongs to
// the Director and the milestone specs, not to this file.

/// <summary>A node's state as reported by a source. Progress fields are fractions in
/// [0,1] or null (unknown / not applicable); the UI never derives them.</summary>
public sealed record NodeStatus(
    string NodeId, string StateId, double? ResearchProgress, long? ResearchPoints, double? RealizationProgress, string Note);

/// <summary>The research line of the header. Points are REPORTED, never computed here
/// (no research formula exists yet — task §5).</summary>
public sealed record ResearchHeader(long? PointsPerTurn, string? CurrentTarget, string Note);

public sealed class TreesStateSnapshot
{
    private readonly Dictionary<string, NodeStatus> _byId;

    public TreesStateSnapshot(long sequence, string sourceLabel, bool isPlaceholder, string civilizationName,
        ResearchHeader research, IEnumerable<NodeStatus> nodes)
    {
        Sequence = sequence;
        SourceLabel = sourceLabel;
        IsPlaceholder = isPlaceholder;
        CivilizationName = civilizationName;
        Research = research;
        Nodes = nodes.OrderBy(x => x.NodeId, StringComparer.Ordinal).ToArray();
        _byId = Nodes.ToDictionary(x => x.NodeId, StringComparer.Ordinal);
    }

    /// <summary>Changes whenever the reported state changes; equal sequences mean equal state.</summary>
    public long Sequence { get; }
    public string SourceLabel { get; }
    public bool IsPlaceholder { get; }
    public string CivilizationName { get; }
    public ResearchHeader Research { get; }

    /// <summary>Every reported node status, sorted by node id (ordinal).</summary>
    public IReadOnlyList<NodeStatus> Nodes { get; }

    public NodeStatus? Status(string nodeId) => _byId.GetValueOrDefault(nodeId);
}

public interface ITreesStateSource
{
    TreesStateSnapshot Current { get; }
}

/// <summary>Where an Age stands relative to the civilization's current Age. Ages move
/// forward only; a Passed Age is never Current again.</summary>
public enum AgeStanding { Passed, Current, Future }

public sealed record AgeTransitionStatus(bool Pending, string? ToAgeId, string Note);

public sealed record MilestoneStatus(string MilestoneId, MilestoneCompletion Completion, double? Progress, string Evidence);

public sealed class AgeStateSnapshot
{
    private readonly Dictionary<string, MilestoneStatus> _byId;

    public AgeStateSnapshot(long sequence, string sourceLabel, bool isPlaceholder, string currentAgeId,
        double? currentProgress, AgeTransitionStatus? transition, IEnumerable<MilestoneStatus> milestones)
    {
        Sequence = sequence;
        SourceLabel = sourceLabel;
        IsPlaceholder = isPlaceholder;
        CurrentAgeId = currentAgeId;
        CurrentProgress = currentProgress;
        Transition = transition;
        Milestones = milestones.OrderBy(m => m.MilestoneId, StringComparer.Ordinal).ToArray();
        _byId = Milestones.ToDictionary(m => m.MilestoneId, StringComparer.Ordinal);
    }

    public long Sequence { get; }
    public string SourceLabel { get; }
    public bool IsPlaceholder { get; }
    public string CurrentAgeId { get; }

    /// <summary>The current Age's progress in [0,1] as REPORTED by the source, or null.
    /// The UI does not compute Age progress: the completion rule is a Director decision.</summary>
    public double? CurrentProgress { get; }
    public AgeTransitionStatus? Transition { get; }
    public IReadOnlyList<MilestoneStatus> Milestones { get; }

    /// <summary>A milestone's status; an unreported milestone reads as not started.</summary>
    public MilestoneStatus Milestone(string id) =>
        _byId.GetValueOrDefault(id) ?? new MilestoneStatus(id, MilestoneCompletion.NotStarted, null, "");
}

public interface IAgeStateSource
{
    AgeStateSnapshot Current { get; }
}

/// <summary>A building placeholder's display state (task §14, §17). Personnel figures are
/// DISPLAY placeholders, not the simulation's conserved population.</summary>
public sealed record BuildingStatus(
    string BuildingId, string? MaturityStage, double MaturityProgress, double ConstructionProgress,
    long? PersonnelCurrent, long? PersonnelCapacity, string Capacity, string? Specialization,
    string? AgeBuilt, string OperationalStatus);

/// <summary>A unit placeholder's display state (task §15, §16). No combat statistic,
/// no formula and no multiplier: every field is shown as reported.</summary>
public sealed record UnitStatus(
    string UnitId, string State, double Strength, long? Experience, string VeterancyLevel,
    double ExperienceProgress, string Training, string Recovery, string? Doctrine, double Cohesion);

public sealed class GallerySnapshot
{
    public GallerySnapshot(long sequence, string sourceLabel, bool isPlaceholder,
        IEnumerable<BuildingStatus> buildings, IEnumerable<UnitStatus> units, int? diffusionStage)
    {
        Sequence = sequence;
        SourceLabel = sourceLabel;
        IsPlaceholder = isPlaceholder;
        Buildings = buildings.ToArray();
        Units = units.ToArray();
        DiffusionStage = diffusionStage;
    }

    public long Sequence { get; }
    public string SourceLabel { get; }
    public bool IsPlaceholder { get; }
    public IReadOnlyList<BuildingStatus> Buildings { get; }
    public IReadOnlyList<UnitStatus> Units { get; }

    /// <summary>Which station (0..4) of the sample diffusion pathway to show, or null.</summary>
    public int? DiffusionStage { get; }

    public BuildingStatus? Building(string id) => Buildings.FirstOrDefault(b => b.BuildingId == id);
    public UnitStatus? Unit(string id) => Units.FirstOrDefault(u => u.UnitId == id);
}

public interface IGalleryStateSource
{
    GallerySnapshot Current { get; }
}

/// <summary>What the live session can honestly say today: the turn, the world date and
/// the simulation's own Δt. Built by the host from the session's clock (read-only); the
/// Trees namespace never sees a simulation type.</summary>
public sealed record SessionContext(long Turn, double WorldYear, double DtYears);
