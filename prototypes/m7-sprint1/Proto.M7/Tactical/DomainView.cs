using Proto.M7.Spatial;

namespace Proto.M7.Tactical;

/// <summary>An own vessel formation as its controller sees it (full information about own forces).</summary>
public readonly record struct OwnVesselFormationView(
    long Id, string ClassName, int Afloat, int Start, long Hull, long Crew, long Morale, Vec2L Centroid, int LeadHeading,
    OrderKind Directive, long DirectiveTarget, Vec2L DirectivePos, long Capacity, long Embarked, int BudgetUsed, int BudgetN);

/// <summary>An own air formation: how many airframes are up, at base (and ready), lost; fuel of the airborne ones.</summary>
public readonly record struct OwnAirFormationView(
    long Id, string ClassName, int Airborne, int Based, int Ready, int Lost, int SortiesLeft, long MinFuelMs, Vec2L Centroid,
    OrderKind Directive, long DirectiveTarget, Vec2L DirectivePos, int BudgetUsed, int BudgetN);

/// <summary>An own land formation with people aboard transports (it may have nobody left on the field).</summary>
public readonly record struct EmbarkedFormationView(long FormationId, long CarrierFormationId, int Aboard, int OnField);

/// <summary>An enemy vessel or aircraft the side has detected at least once (tracked now, or remembered).</summary>
public readonly record struct KnownDomainEnemy(long EntityId, long FormationId, Domain Domain, Vec2L LastSeen, long LastSeenMs, bool Tracked);

/// <summary>
/// The naval / air part of a <see cref="SideView"/>: own vessel and air formations (full information), own formations with
/// people aboard, and the side's KNOWLEDGE of enemy vessels and aircraft (detected ones only; fog-honest like
/// <see cref="SideView.Known"/>). Holds no reference to the battle.
/// </summary>
public sealed class DomainView
{
    public static readonly DomainView Empty = new(Array.Empty<OwnVesselFormationView>(), Array.Empty<OwnAirFormationView>(),
                                                  Array.Empty<EmbarkedFormationView>(), Array.Empty<KnownDomainEnemy>());

    public DomainView(OwnVesselFormationView[] vessels, OwnAirFormationView[] air, EmbarkedFormationView[] embarked, KnownDomainEnemy[] known)
    {
        Vessels = vessels; Air = air; Embarked = embarked; Known = known;
    }

    /// <summary>Own vessel formations with at least one hull afloat, ascending id.</summary>
    public IReadOnlyList<OwnVesselFormationView> Vessels { get; }
    /// <summary>Own air formations with at least one airframe not lost, ascending id.</summary>
    public IReadOnlyList<OwnAirFormationView> Air { get; }
    public IReadOnlyList<EmbarkedFormationView> Embarked { get; }
    /// <summary>Known enemy vessels and aircraft, ascending entity id.</summary>
    public IReadOnlyList<KnownDomainEnemy> Known { get; }
}

/// <summary>Runs several controllers in sequence on the same view (e.g. a land AI and a naval/air AI).</summary>
public sealed class CompositeController : IFormationController
{
    private readonly IFormationController[] _parts;
    public CompositeController(params IFormationController[] parts) { _parts = parts; }
    public void Decide(SideView view, List<OrderRequest> orders)
    {
        foreach (var p in _parts) p.Decide(view, orders);
    }
}

/// <summary>
/// Simple knowledge-only naval / air AI (inspectable; all thresholds PLACEHOLDER). Per own vessel formation, ascending id:
/// carriers of troops (Capacity &gt; 0) are left to the player/script; otherwise attack the formation of the known enemy VESSEL
/// nearest the formation centroid, key (distance², formationId, entityId), unless already attacking it. Per own air formation:
/// if it has an air-to-air weapon and an enemy aircraft is known, attack that aircraft's formation (same key); otherwise, if
/// it has a strike weapon and an enemy vessel is known, attack the nearest known vessel formation; otherwise nothing.
/// Decisions are recorded in <see cref="Decisions"/>.
/// </summary>
public sealed class DomainAi : IFormationController
{
    private readonly List<AiDecision> _decisions = new();
    public IReadOnlyList<AiDecision> Decisions => _decisions;
    /// <summary>Capability lookup: does this own air formation carry an air-to-air / strike weapon (supplied by the scenario).</summary>
    public Func<long, TargetDomains>? AirWeapons { get; init; }

    public void Decide(SideView view, List<OrderRequest> orders)
    {
        var d = view.Domain;
        foreach (var v in d.Vessels)
        {
            if (v.Capacity > 0 || v.Afloat == 0 || v.Directive == OrderKind.Retreat) continue;
            if (!Nearest(d, v.Centroid, Domain.Naval, out long form)) { Note(view, v.Id, AiReason.NoContact); continue; }
            if (v.Directive == OrderKind.Attack && v.DirectiveTarget == form) { Note(view, v.Id, AiReason.AlreadyEngaged); continue; }
            Emit(view, v.Id, AiReason.Engage, new OrderRequest(v.Id, OrderKind.Attack, 0, form), orders);
        }
        foreach (var a in d.Air)
        {
            var w = AirWeapons?.Invoke(a.Id) ?? TargetDomains.None;
            bool canLaunchOrFly = a.Airborne > 0 || a.Ready > 0;
            if (!canLaunchOrFly) { Note(view, a.Id, AiReason.NoContact); continue; }
            long form;
            if ((w & TargetDomains.Air) != 0 && Nearest(d, a.Centroid, Domain.Air, out form)) { }
            else if ((w & TargetDomains.Surface) != 0 && Nearest(d, a.Centroid, Domain.Naval, out form)) { }
            else { Note(view, a.Id, AiReason.NoContact); continue; }
            if (a.Directive == OrderKind.Attack && a.DirectiveTarget == form && a.Airborne > 0) { Note(view, a.Id, AiReason.AlreadyEngaged); continue; }
            Emit(view, a.Id, AiReason.Engage, new OrderRequest(a.Id, OrderKind.Attack, 0, form), orders);
        }
    }

    private static bool Nearest(DomainView d, Vec2L from, Domain dom, out long formation)
    {
        bool found = false; Int128 bd = 0; long bf = 0, be = 0;
        foreach (var k in d.Known)
        {
            if (k.Domain != dom) continue;
            Int128 dd = Vec2L.DistanceSq(from, k.LastSeen);
            if (!found || dd < bd || (dd == bd && (k.FormationId < bf || (k.FormationId == bf && k.EntityId < be))))
            { found = true; bd = dd; bf = k.FormationId; be = k.EntityId; }
        }
        formation = bf;
        return found;
    }

    private void Emit(SideView v, long formationId, AiReason r, OrderRequest o, List<OrderRequest> orders)
    {
        orders.Add(o);
        _decisions.Add(new AiDecision(v.Pulse, formationId, r, true, o));
    }

    private void Note(SideView v, long formationId, AiReason r) => _decisions.Add(new AiDecision(v.Pulse, formationId, r, false, default));
}
