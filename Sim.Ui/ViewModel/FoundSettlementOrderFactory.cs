using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Colonization;
using Sim.Core.Worldgen;

namespace Sim.Ui.ViewModel;

/// <summary>
/// M5 S1d (D-051 §5, ADR-036) — THE PLAYER'S FOUND SETTLEMENT ORDER. Pure. The order is exactly
/// <see cref="FoundingQuery.FoundOrder"/> — the constructor the AI's founding policy uses (S1c) — and it is built only
/// when THE predicate, <see cref="FoundingQuery.Check(IReadOnlyWorldState, SimConfig, SettlementSiting.FrontierSiting, ReadOnlySpan{double}, ReadOnlySpan{int}, PolityId, SettlementId, int)"/>,
/// answers <see cref="FoundingVerdict.Legal"/> for it on the world the coming step judges it on (the session's world:
/// PREV at delivery) with the standing sites that step's intake will hold when it reaches this order. So the UI never
/// queues an order ColonizationSystem would refuse (ADR-033 D2, "one predicate, two callers"; the ActionEmitterGuardTests
/// pattern), and the AI and the player are refused on the same rule.
///
/// <list type="bullet">
/// <item><b>The player's known extent (P7)</b>: a cell the player's <see cref="PolityKnowledge"/> does not know is never
///   judged and never ordered (<see cref="Judge"/> answers null) — the arm the AI's site scan puts first
///   (AiFoundingPolicy's pre-filter); the predicate's own known-extent arm is inert until S2a.</item>
/// <item><b>The batch</b> (<see cref="Pending"/>): the intake judges a turn's FoundSettlement orders in LOG ORDER, each
///   with the occupancy and spacing grown by every earlier one that founded, and founds only the FIRST legal order per
///   source per turn (ColonizationSystem.FoundOrdered). The orders already queued this turn are replayed through the same
///   predicate in the same order, growing the AI's own batch state (<see cref="AiFoundingPolicy.Sites"/> — reused, not
///   copied); a source that will already found this turn is refused a second order, and a new site is judged against the
///   colonies the queued orders will found. CONSERVATIVE in one respect only: departure is live (ADR-036 §3), so a queued
///   order that the step leaves without a party founds nothing; it is still counted here, which can only refuse an order
///   the step would have accepted, never queue one it refuses.</item>
/// </list>
/// Deterministic: log order, table order, integer keys; no RNG, no clock, no dictionary, no LINQ.
/// </summary>
public static class FoundSettlementOrderFactory
{
    /// <summary>The coming step's founding batch as far as this turn's queue already fills it: the standing sites a NEW
    /// order is judged against, and the sources that will already found.</summary>
    public sealed class Pending
    {
        internal Pending(AiFoundingPolicy.Sites sites, int[] founding, SiteAssessment.Batch batch)
        {
            Sites = sites;
            Founding = founding;
            Batch = batch;
        }

        /// <summary>The occupied sites and the spacing exclusion: the world's settlements, grown by each queued order the
        /// predicate accepts, in log order (AiFoundingPolicy.Sites.Accept — the system's own growth).</summary>
        public AiFoundingPolicy.Sites Sites { get; }

        /// <summary>The source settlement ids whose queued order the predicate accepts, in log order.</summary>
        public IReadOnlyList<int> Founding { get; }

        /// <summary>M5 S1d review — the same standing sites as a snapshot (<see cref="SiteAssessment.Batch"/>: the occupied
        /// cells and the exclusion <see cref="Sites"/> holds once the queue is replayed, and the standing settlements' own
        /// exclusion). <see cref="Judge"/> reads THESE arrays, and so does the place card the placement mode docks
        /// (SiteAssessment.Assess with this batch), so the mode's verdict and the card's are one judgement.</summary>
        public SiteAssessment.Batch Batch { get; }

        /// <summary>Whether a queued order from <paramref name="source"/> already founds this turn.</summary>
        public bool Founds(SettlementId source)
        {
            for (int i = 0; i < Founding.Count; i++) if (Founding[i] == source.Value) return true;
            return false;
        }
    }

    /// <summary>
    /// The batch <paramref name="queued"/> (the not-yet-stepped orders of the current turn, in log order) makes: for each
    /// FoundSettlement order, skipped when its source already founds, else judged by THE predicate with the sites grown
    /// so far; a legal one grows them. The ColonizationSystem.FoundOrdered loop on PREV, without the live departure.
    /// </summary>
    public static Pending Standing(
        IReadOnlyWorldState world, SimConfig cfg, SettlementSiting.FrontierSiting frontier, IReadOnlyList<OrderRecord> queued)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(queued);
        var sites = new AiFoundingPolicy.Sites(world, frontier);
        int standing = sites.Occupied.Length;                 // the world's settlements' sites, before any order grows them
        double[] standingSpacing = sites.Spacing.ToArray();   // copied: Accept grows the exclusion in place
        var founding = new List<int>();
        for (int i = 0; i < queued.Count; i++)
        {
            OrderRecord o = queued[i];
            if (o.Kind != OrderKind.FoundSettlement) continue;
            if (founding.Contains(o.TargetId)) continue;
            int site = FoundingQuery.SiteCellOf(o);
            if (FoundingQuery.Check(world, cfg, frontier, sites.Spacing, sites.Occupied, o.Actor, new SettlementId(o.TargetId), site)
                != FoundingVerdict.Legal) continue;
            sites.Accept(site);
            founding.Add(o.TargetId);
        }
        return new Pending(sites, [.. founding], new SiteAssessment.Batch(frontier, sites.Occupied, standing, sites.Spacing, standingSpacing));
    }

    /// <summary>
    /// THE JUDGEMENT the placement mode shows for one cell: null when <paramref name="knowledge"/> does not know it (never
    /// offered: not hoverable, not selectable); otherwise THE predicate's verdict for (<paramref name="issuer"/>,
    /// <paramref name="source"/>, <paramref name="cell"/>) on <paramref name="world"/> with <paramref name="pending"/>'s
    /// standing sites — the answer the intake will give.
    /// </summary>
    public static FoundingVerdict? Judge(
        IReadOnlyWorldState world, SimConfig cfg, SettlementSiting.FrontierSiting frontier, PolityKnowledge knowledge,
        PolityId issuer, SettlementId source, int cell, Pending pending)
    {
        ArgumentNullException.ThrowIfNull(knowledge);
        ArgumentNullException.ThrowIfNull(pending);
        if (knowledge.Polity.Value != issuer.Value) throw new ArgumentException("the knowledge must be the issuer's", nameof(knowledge));
        if (!knowledge.KnowsCell(cell)) return null;
        return FoundingQuery.Check(world, cfg, frontier, pending.Batch.Spacing, pending.Batch.Occupied, issuer, source, cell);
    }

    /// <summary>The FoundSettlement order for (<paramref name="issuer"/>, <paramref name="source"/>,
    /// <paramref name="cell"/>), stamped with the CURRENT turn (the next End Turn delivers it), or null when the simulation
    /// would not found it: the player does not know the cell, a queued order from the source already founds this turn, or
    /// the predicate refuses it against the batch.</summary>
    public static OrderRecord? Create(
        IReadOnlyWorldState world, SimConfig cfg, SettlementSiting.FrontierSiting frontier, PolityKnowledge knowledge,
        PolityId issuer, SettlementId source, int cell, IReadOnlyList<OrderRecord> queued)
    {
        Pending pending = Standing(world, cfg, frontier, queued);
        if (pending.Founds(source)) return null;
        if (Judge(world, cfg, frontier, knowledge, issuer, source, cell, pending) != FoundingVerdict.Legal) return null;
        return FoundingQuery.FoundOrder(world, issuer, source, cell);
    }
}
