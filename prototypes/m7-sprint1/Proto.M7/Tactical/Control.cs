using Proto.M7.Spatial;

namespace Proto.M7.Tactical;

/// <summary>A formation order as submitted by a controller (player script or Enemy AI) for the current pulse.</summary>
/// <param name="StepOffset">Internal step of the pulse at which the order is delivered (0 = pulse start).</param>
/// <param name="TargetFormationId">Attack / Reengage target (enemy formation id), else -1.</param>
/// <param name="Pos">MoveTo destination (mm), else ignored.</param>
public readonly record struct OrderRequest(long FormationId, OrderKind Kind, int StepOffset = 0, long TargetFormationId = -1, Vec2L Pos = default);

/// <summary>Why an order was invalid before the budget was consulted (None = valid). Stable codes.</summary>
public enum OrderInvalid : byte
{
    None = 0,
    UnknownFormation = 1,
    NotOwnFormation = 2,
    NotOnField = 3,
    BadTarget = 4,
    NoPath = 5,
    NothingToReengage = 6,
    BadStep = 7,
}

/// <summary>
/// One submitted order and its outcome — the prototype-local order log (NOT the kernel OrderRecord). Every order is
/// recorded, including refused ones (spec §4: "refused orders are recorded (not silently dropped)"). Replaying the
/// submitted requests per pulse reproduces the battle (ReplayTests).
/// </summary>
public readonly record struct OrderLogEntry(
    long Index, long Pulse, int Side, OrderRequest Request, long AtMs, OrderInvalid Invalid, BudgetRefusal Budget)
{
    public bool Accepted => Invalid == OrderInvalid.None && Budget == BudgetRefusal.None;
}

public readonly record struct OwnFormationView(
    long Id, Posture Posture, OrderKind Directive, long DirectiveTarget, Vec2L DirectivePos,
    long Morale, int Start, int Active, int Routing, Vec2L Anchor, Vec2L Centroid, int BudgetUsed, int BudgetN);

/// <summary>An enemy entity the side has detected at least once (tracked now, or remembered at its last-seen point).</summary>
public readonly record struct KnownEnemy(long EntityId, long FormationId, Vec2L LastSeen, long LastSeenMs, bool Tracked);

/// <summary>
/// Everything a controller may see: its own formations (full information) and the side's KNOWLEDGE of the enemy (detected
/// entities only). It holds no reference to the battle, so a controller cannot read undetected enemies (knowledge-only AI).
/// </summary>
public sealed class SideView
{
    public SideView(int side, long pulse, long nowMs, long cellSizeMm, OwnFormationView[] own, KnownEnemy[] known, Vec2L[] objectives)
    {
        Side = side; Pulse = pulse; NowMs = nowMs; CellSizeMm = cellSizeMm; Own = own; Known = known; Objectives = objectives;
    }
    public int Side { get; }
    public long Pulse { get; }
    public long NowMs { get; }
    public long CellSizeMm { get; }
    /// <summary>Own formations still on the field, ascending id.</summary>
    public IReadOnlyList<OwnFormationView> Own { get; }
    /// <summary>Known enemy entities, ascending entity id.</summary>
    public IReadOnlyList<KnownEnemy> Known { get; }
    public IReadOnlyList<Vec2L> Objectives { get; }
}

/// <summary>Decides one side's formation orders at each pulse start.</summary>
public interface IFormationController
{
    void Decide(SideView view, List<OrderRequest> orders);
}

/// <summary>A controller that issues nothing (standing directives persist).</summary>
public sealed class PassiveController : IFormationController
{
    public static readonly PassiveController Instance = new();
    public void Decide(SideView view, List<OrderRequest> orders) { }
}

/// <summary>Scripted orders per pulse (player script, test script, or a replay of a recorded order log).</summary>
public sealed class ScriptedController : IFormationController
{
    private readonly List<(long Pulse, OrderRequest Order)> _orders = new();

    public ScriptedController Add(long pulse, OrderRequest order) { _orders.Add((pulse, order)); return this; }

    public void Decide(SideView view, List<OrderRequest> orders)
    {
        foreach (var (p, o) in _orders) if (p == view.Pulse) orders.Add(o);
    }

    /// <summary>Replay: re-submit exactly the requests one side submitted, pulse by pulse, in log order.</summary>
    public static ScriptedController FromLog(IReadOnlyList<OrderLogEntry> log, int side)
    {
        var c = new ScriptedController();
        foreach (var e in log) if (e.Side == side) c.Add(e.Pulse, e.Request);
        return c;
    }
}

public enum AiReason : byte
{
    NoContact = 0,
    Advance = 1,
    Engage = 2,
    AmbushWaiting = 3,
    LowMorale = 4,
    Withdrawing = 5,
    AlreadyEngaged = 6,
}

/// <summary>An inspectable AI decision (m7-spec.md:389 "Enemy AI makes inspectable decisions based on its knowledge").</summary>
public readonly record struct AiDecision(long Pulse, long FormationId, AiReason Reason, bool Issued, OrderRequest Order);

/// <summary>
/// Simple knowledge-only Enemy AI (spec §4). Input: a <see cref="SideView"/> (own formations + the side's knowledge).
/// Per own formation, ascending id:
///   1. morale &lt; RetreatMorale and not already retreating =&gt; Retreat;
///   2. already retreating =&gt; nothing;
///   3. a known enemy exists =&gt; target formation = the formation of the known enemy entity nearest to the formation
///      centroid by composite key (distance², formationId, entityId) (CLAUDE.md stable integer tie-break);
///      Ambush posture and that enemy farther than AmbushTriggerCells =&gt; Hold (once); else Attack(target) unless already so;
///   4. no known enemy and the side has objectives =&gt; MoveTo nearest objective, key (distance², index), unless already so;
///   5. otherwise nothing (standing directive).
/// Every decision is appended to <see cref="Decisions"/>. All thresholds are PLACEHOLDER inputs.
/// </summary>
public sealed class KnowledgeAi : IFormationController
{
    /// <summary>PLACEHOLDER (invented).</summary>
    public long RetreatMorale { get; init; } = 450;
    /// <summary>PLACEHOLDER (invented).</summary>
    public long AmbushTriggerCells { get; init; } = 3;

    private readonly List<AiDecision> _decisions = new();
    public IReadOnlyList<AiDecision> Decisions => _decisions;

    public void Decide(SideView view, List<OrderRequest> orders)
    {
        foreach (var f in view.Own)
        {
            if (f.Active == 0) continue;
            OrderRequest o;
            if (f.Directive != OrderKind.Retreat && f.Morale < RetreatMorale)
            {
                o = new OrderRequest(f.Id, OrderKind.Retreat);
                Emit(view, f, AiReason.LowMorale, o, orders);
                continue;
            }
            if (f.Directive == OrderKind.Retreat) { Note(view, f, AiReason.Withdrawing); continue; }

            bool found = false;
            Int128 bestD = 0; long bestForm = 0, bestEnt = 0;
            foreach (var k in view.Known)
            {
                Int128 d = Vec2L.DistanceSq(f.Centroid, k.LastSeen);
                if (!found || d < bestD || (d == bestD && (k.FormationId < bestForm || (k.FormationId == bestForm && k.EntityId < bestEnt))))
                {
                    found = true; bestD = d; bestForm = k.FormationId; bestEnt = k.EntityId;
                }
            }
            if (found)
            {
                Int128 trig = (Int128)AmbushTriggerCells * view.CellSizeMm;
                if (f.Posture == Posture.Ambush && f.Directive == OrderKind.Hold && bestD > trig * trig)
                {
                    Note(view, f, AiReason.AmbushWaiting);
                    continue;
                }
                if (f.Directive == OrderKind.Attack && f.DirectiveTarget == bestForm) { Note(view, f, AiReason.AlreadyEngaged); continue; }
                Emit(view, f, AiReason.Engage, new OrderRequest(f.Id, OrderKind.Attack, 0, bestForm), orders);
                continue;
            }
            if (view.Objectives.Count > 0 && f.Posture != Posture.Ambush)
            {
                int bi = 0; Int128 bd = Vec2L.DistanceSq(f.Centroid, view.Objectives[0]);
                for (int i = 1; i < view.Objectives.Count; i++)
                {
                    Int128 d = Vec2L.DistanceSq(f.Centroid, view.Objectives[i]);
                    if (d < bd) { bd = d; bi = i; }       // strict: ties keep the lower index
                }
                Vec2L obj = view.Objectives[bi];
                if (f.Directive == OrderKind.MoveTo && f.DirectivePos == obj) { Note(view, f, AiReason.Advance); continue; }
                Emit(view, f, AiReason.Advance, new OrderRequest(f.Id, OrderKind.MoveTo, 0, -1, obj), orders);
                continue;
            }
            Note(view, f, AiReason.NoContact);
        }
    }

    private void Emit(SideView v, in OwnFormationView f, AiReason r, OrderRequest o, List<OrderRequest> orders)
    {
        orders.Add(o);
        _decisions.Add(new AiDecision(v.Pulse, f.Id, r, true, o));
    }

    private void Note(SideView v, in OwnFormationView f, AiReason r)
        => _decisions.Add(new AiDecision(v.Pulse, f.Id, r, false, default));
}
