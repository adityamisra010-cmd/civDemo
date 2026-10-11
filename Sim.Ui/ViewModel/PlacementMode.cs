using System.Globalization;
using Sim.Core.Pathing;
using Sim.Core.State;

namespace Sim.Ui.ViewModel;

/// <summary>
/// M5 S1d (D-051 §5, ADR-036) — THE SETTLEMENT-PLACEMENT MODE, as UI state. Entered from the action surface's "Found a
/// settlement" (or F) for ONE source the query lists (<see cref="AvailableActionsQuery.Colonization"/>: its source-side
/// arms hold); the player then chooses a SITE on the map in two steps — a click SELECTS a cell (legal or not: an illegal
/// cell is selected with its reason, so the player sees why), and Confirm (or Enter) asks the session's ONE guarded
/// emitter, <see cref="UiSession.EmitFoundSettlement"/>, for the order. Escape (or Cancel) leaves the mode. UI STATE ONLY:
/// nothing here writes the world or the log; hovering, selecting and the camera change no hash and queue nothing.
///
/// <para><b>Every cell is judged by THE predicate</b>, through <see cref="FoundSettlementOrderFactory.Judge"/> — the same
/// function the emitter's guard asks, with the same batch of this turn's queued orders — so the verdict shown is the
/// verdict the Confirm meets and the one ColonizationSystem applies. A cell the player does not know (P7) is never judged:
/// it is not hoverable and not selectable (<see cref="Hover"/> and <see cref="Select"/> ignore it).</para>
///
/// <para><b>The player's information class (ADR-037 §7).</b> While the player sees the whole world
/// (<see cref="SiteAssessment.SeesAll"/>; production today) the reason and the route are the simulation's own. Otherwise
/// a refusal whose arm reads beyond what the player knows (the floor, occupancy, spacing, the claim, the route) is told as
/// "why depends on what you have not seen", and no route is drawn. Whether an order would be accepted is still the
/// predicate's answer — an order surface cannot hide that one bit; recorded for the fog packets (docs/queue.md).</para>
///
/// <para><b>Cost.</b> The batch's standing sites (a capped Dijkstra) are built once per (world, queued orders); a verdict is
/// computed when the pointer enters a new cell; the route and the docked card only for the selected cell, or for the
/// hovered one once the pointer has rested on it <see cref="CardRestFrames"/> frames.</para>
/// </summary>
public sealed class PlacementMode
{
    /// <summary>How many consecutive frames the pointer must rest on one cell before its route and its card are shown (the
    /// card assesses the place — tens of ms — so it is not rebuilt for every cell the pointer crosses).</summary>
    public const int CardRestFrames = 8;

    /// <summary>Whether the mode is on.</summary>
    public bool Active { get; private set; }

    /// <summary>The source settlement's id, or -1.</summary>
    public int Source { get; private set; } = -1;

    /// <summary>The source's name and the party line (the query's own detail), as the mode was entered.</summary>
    public string SourceName { get; private set; } = "";
    public string PartyLine { get; private set; } = "";

    /// <summary>The KNOWN cell under the pointer over open map, or -1.</summary>
    public int Hovered { get; private set; } = -1;

    /// <summary>The hovered cell's verdict (null with no hovered cell).</summary>
    public FoundingVerdict? HoveredVerdict { get; private set; }

    /// <summary>The selected cell (the first step), or -1.</summary>
    public int Selected { get; private set; } = -1;

    /// <summary>The selected cell's verdict (null with no selection).</summary>
    public FoundingVerdict? SelectedVerdict { get; private set; }

    /// <summary>Why the last Confirm appended nothing, or null.</summary>
    public string? Refusal { get; private set; }

    /// <summary>Whether Confirm would ask for an order: a selected cell the predicate answers Legal for.</summary>
    public bool CanConfirm => Active && Selected >= 0 && SelectedVerdict == FoundingVerdict.Legal;

    /// <summary>The cell whose route is drawn and whose place card is docked: the selection; else the hovered cell once the
    /// pointer has rested on it <see cref="CardRestFrames"/> frames; else -1.</summary>
    public int CardCell => !Active ? -1 : Selected >= 0 ? Selected : _rest >= CardRestFrames ? Hovered : -1;

    /// <summary>The route from the source to <see cref="CardCell"/> (FoundingQuery.RouteFrom — the route arm 7 judges), or
    /// null when there is none to show: no card cell, the player does not see the whole world, or no frontier.</summary>
    public RouteQuery.Route? Route { get; private set; }

    /// <summary>The cell <see cref="Route"/> was found for (-1: none).</summary>
    public int RouteCell { get; private set; } = -1;

    /// <summary>Whether the player sees the whole world (SiteAssessment.SeesAll of the session's knowledge), as last judged.</summary>
    public bool SeesAll { get; private set; } = true;

    private int _rest;
    private object? _judgedWorld;
    private object? _judgedKnowledge;
    private int _judgedQueued = -1;
    private FoundSettlementOrderFactory.Pending? _pending;

    /// <summary>Enters the mode from <paramref name="source"/> (named <paramref name="name"/>; <paramref name="partyLine"/> the
    /// query's detail). Any previous choice is forgotten.</summary>
    public void Begin(int source, string name, string partyLine)
    {
        Reset();
        Active = true;
        Source = source;
        SourceName = name;
        PartyLine = partyLine;
    }

    /// <summary>Leaves the mode (Escape, Cancel, End Turn, or after an order was appended).</summary>
    public void Cancel() => Reset();

    private void Reset()
    {
        Active = false;
        Source = -1;
        SourceName = "";
        PartyLine = "";
        Hovered = Selected = -1;
        HoveredVerdict = SelectedVerdict = null;
        Refusal = null;
        Route = null;
        RouteCell = -1;
        _rest = 0;
        _judgedWorld = null;
        _judgedKnowledge = null;
        _judgedQueued = -1;
        _pending = null;
    }

    /// <summary>THE judgement of <paramref name="cell"/> from the mode's source on the session's world, with the batch this
    /// turn's queued orders make (<see cref="FoundSettlementOrderFactory.Judge"/>): null for a cell the player does not
    /// know, a world without terrain, or the mode off.</summary>
    public FoundingVerdict? Judge(UiSession session, int cell)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (!Active || session.Sites is not { } site || !Fresh(session, site)) return null;
        return FoundSettlementOrderFactory.Judge(session.World, session.Config, site.Frontier, session.Knowledge.Core,
            UiPlayer.Empire, new Sim.Core.State.SettlementId(Source), cell, _pending!);
    }

    /// <summary>
    /// M5 S1d review — THE BATCH the mode judges against (<see cref="FoundSettlementOrderFactory.Pending.Batch"/>: this turn's
    /// queued founding orders replayed through THE predicate), for the place card it docks: the card is judged against these
    /// same arrays (SiteAssessment.Assess with a batch), so its verdict, its arms and its words are the mode's. Null while the
    /// mode is off or the world has no terrain.
    /// </summary>
    public SiteAssessment.Batch? CardBatch(UiSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (!Active || session.Sites is not { } site || !Fresh(session, site)) return null;
        return _pending!.Batch;
    }

    /// <summary>The words for <paramref name="verdict"/> at <paramref name="cell"/> (the hovered or selected place): as
    /// <see cref="ReasonText(FoundingVerdict, string, bool, bool)"/>, a refusal only an order queued this turn causes told
    /// as such (<see cref="SiteAssessment.Batch.RefusesByOrder"/> of the mode's batch — the place card's own test).</summary>
    public string Reason(FoundingVerdict verdict, int cell) =>
        ReasonText(verdict, SourceName, SeesAll, _pending is { } p && p.Batch.RefusesByOrder(verdict, cell));

    /// <summary>Rebuilds the batch's standing sites when the world, the knowledge or the queued orders changed (and
    /// re-judges the hovered and selected cells against them). True when the mode can judge.</summary>
    private bool Fresh(UiSession session, SiteAssessment.Prepared site)
    {
        IReadOnlyList<Sim.Core.Kernel.OrderRecord> queued = session.QueuedOrders();
        if (_pending is not null && ReferenceEquals(_judgedWorld, session.World) && ReferenceEquals(_judgedKnowledge, session.Knowledge)
            && _judgedQueued == queued.Count) return true;
        _pending = FoundSettlementOrderFactory.Standing(session.World, session.Config, site.Frontier, queued);
        _judgedWorld = session.World;
        _judgedKnowledge = session.Knowledge;
        _judgedQueued = queued.Count;
        SeesAll = SiteAssessment.SeesAll(session.Knowledge.Core);
        var src = new Sim.Core.State.SettlementId(Source);
        if (Hovered >= 0) HoveredVerdict = FoundSettlementOrderFactory.Judge(session.World, session.Config, site.Frontier, session.Knowledge.Core, UiPlayer.Empire, src, Hovered, _pending);
        if (Selected >= 0) SelectedVerdict = FoundSettlementOrderFactory.Judge(session.World, session.Config, site.Frontier, session.Knowledge.Core, UiPlayer.Empire, src, Selected, _pending);
        RouteCell = -1;   // re-found for the new world
        Route = null;
        return true;
    }

    /// <summary>One frame's pointer: <paramref name="cell"/> is the terrain cell under it over open map (or -1). A cell the
    /// player does not know is no hover. Judged when it changes; the rest counter runs while it stays.</summary>
    public void Hover(UiSession session, int cell)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (!Active) return;
        if (session.Sites is not { } site || !Fresh(session, site)) return;   // a stale batch re-judges the hovered cell
        if (cell >= 0 && cell == Hovered)
        {
            if (_rest < CardRestFrames) _rest++;   // the same place: judged already
        }
        else
        {
            FoundingVerdict? verdict = cell >= 0 ? Judge(session, cell) : null;
            if (verdict is null) cell = -1;        // undiscovered (or nothing): not hoverable
            if (cell != Hovered)
            {
                Hovered = cell;
                HoveredVerdict = verdict;
                _rest = 0;
            }
        }
        SyncRoute(session);
    }

    /// <summary>The first step: a click on <paramref name="cell"/> selects it — legal or not, so the reason is shown — unless
    /// the player does not know it (not selectable: nothing changes). Returns whether it was selected.</summary>
    public bool Select(UiSession session, int cell)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (!Active || cell < 0 || Judge(session, cell) is not { } verdict) return false;
        Selected = cell;
        SelectedVerdict = verdict;
        Refusal = null;
        SyncRoute(session);
        return true;
    }

    /// <summary>The second step: asks the session's ONE guarded emitter for the order from the source to the selected cell.
    /// True when an order was appended (the mode then ends); false with <see cref="Refusal"/> saying why otherwise.</summary>
    public bool Confirm(UiSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (!Active) return false;
        if (Selected < 0) { Refusal = "Choose a place on the map first."; return false; }
        _ = Judge(session, Selected);   // re-judges the selection when the world, the knowledge or the queue changed
        if (SelectedVerdict is null) { Refusal = "That place is not one you know."; return false; }
        if (SelectedVerdict is { } v && v != FoundingVerdict.Legal)
        {
            Refusal = "Not here: " + Reason(v, Selected) + ".";
            return false;
        }
        if (!session.EmitFoundSettlement(Source, Selected))
        {
            Refusal = "The order was not accepted: " + (Judge(session, Selected) is { } now && now != FoundingVerdict.Legal
                ? Reason(now, Selected) : "settlers from " + SourceName + " are already ordered out this turn") + ".";
            return false;
        }
        Cancel();
        return true;
    }

    /// <summary>Finds the route for <see cref="CardCell"/> when it changed (only while the player sees the whole world).</summary>
    private void SyncRoute(UiSession session)
    {
        int cell = CardCell;
        if (cell == RouteCell) return;
        RouteCell = cell;
        Route = cell >= 0 && SeesAll && session.Sites is { } site
            ? FoundingQuery.RouteFrom(session.World, site.Frontier, new Sim.Core.State.SettlementId(Source), cell) : null;
    }

    /// <summary>
    /// A verdict in the player's words — the place card's own line (<see cref="InfoQuery.FoundingLine"/>). Unless the player
    /// sees the whole world, a refusal decided by what lies beyond the player's knowledge (the floor over every cell,
    /// occupancy, spacing, the claim, the route) is told only as such (ADR-037 §7).
    /// </summary>
    public static string ReasonText(FoundingVerdict verdict, string from, bool seesAll) => ReasonText(verdict, from, seesAll, byOrder: false);

    /// <summary>M5 S1d review: as <see cref="ReasonText(FoundingVerdict, string, bool)"/>, but a refusal that only an order
    /// queued this turn causes (<paramref name="byOrder"/>) is told in its own words
    /// (<see cref="InfoQuery.FoundingLine(FoundingVerdict, string, bool)"/>): "too close to a settlement you ordered this
    /// turn", not "...a standing settlement". A place the player's own order takes is the player's to know, so that one is
    /// told under a partial view too; spacing reads the lattice, so it is not.</summary>
    public static string ReasonText(FoundingVerdict verdict, string from, bool seesAll, bool byOrder)
    {
        bool own = verdict is FoundingVerdict.Legal or FoundingVerdict.UnknownSource or FoundingVerdict.NotControlled
            or FoundingVerdict.OffMap or FoundingVerdict.Water or FoundingVerdict.NoParty or FoundingVerdict.NoProvisions
            || (byOrder && verdict == FoundingVerdict.Occupied);
        return seesAll || own ? InfoQuery.FoundingLine(verdict, from, byOrder)
            : "settlers from " + from + " could not found a settlement here; why depends on what you have not seen";
    }

    /// <summary>The route as one line ("12 steps, travel cost 34.5 (about 120 km of ideal ground)"), or null when there is
    /// none to show.</summary>
    public string? RouteLine(TraversalLattice lattice)
    {
        ArgumentNullException.ThrowIfNull(lattice);
        if (Route is not { } r) return null;
        if (!r.Found) return "No land route from " + SourceName;
        return "Route from " + SourceName + ": " + (r.Nodes.Length - 1).ToString(CultureInfo.InvariantCulture) + " steps, travel cost "
            + r.TotalCost.ToString("0.#", CultureInfo.InvariantCulture) + " (about "
            + LatticeGeometry.IdealGroundKmForCostUnits(lattice, r.TotalCost).ToString("#,0", CultureInfo.InvariantCulture) + " km of ideal ground)";
    }
}
