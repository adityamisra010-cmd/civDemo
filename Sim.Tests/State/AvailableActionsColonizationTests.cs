using System.Collections.Immutable;
using System.Globalization;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Tests.TestUtil;

namespace Sim.Tests.State;

/// <summary>
/// M5 S1d (D-051 §5, ADR-036) — THE FOUND SETTLEMENT ACTION in <see cref="AvailableActionsQuery"/>: one Order descriptor
/// per settlement the issuer controls for which the SOURCE-side arms of THE predicate hold (control, enough people, enough
/// food — <see cref="FoundingQuery.Departure"/>, the function ColonizationSystem applies), with the party and its
/// provisions as FoundingQuery computes them; never for a settlement another polity rules, never for a source that cannot
/// spare a party or provision one, never twice in a turn for one source, never for a world without terrain. Each refusal
/// has a control that lists the same source without its cause.
/// </summary>
public class AvailableActionsColonizationTests
{
    // M6 EX (2026-10-22): the founding rigs' config (FoundingRigs.Cfg — the discovery layer off, the R4 rule): the S1d
    // founding surface is an older layer premised on the all-known view (a rival's settlement is known, so "not yours").
    private static readonly SimConfig Cfg = FoundingRigs.Cfg();
    private static readonly PolityId Player = new(1);
    private static readonly PolityId Rival = new(2);

    private static ActionDescriptor[] Founding(IReadOnlyWorldState w, SimConfig cfg, PolityId p, ActionQueryContext? ctx = null) =>
        AvailableActionsQuery.For(w, cfg, p, ctx).Where(a => a.Domain == ActionDomain.Colonization).ToArray();

    [Fact]
    public void EveryControlledSourceWhoseSourceArmsHold_IsListedOnce_WithTheParty_AndTheProvisions()
    {
        WorldState w = FoundingRigs.Dev(42UL, aiEmpires: 1);
        var grain = new GoodId(Cfg.Goods!.GrainId);
        string grainName = Cfg.Goods.ById(Cfg.Goods.GrainId).Name;
        foreach (PolityId p in new[] { Player, Rival })
        {
            ActionDescriptor[] listed = Founding(w, Cfg, p);
            var expected = new List<int>();
            for (int i = 0; i < w.Settlements.Count; i++)
            {
                SettlementId s = w.Settlements[i].Id;
                if (EmpireQuery.ControlsSettlement(w, p, s)
                    && FoundingQuery.Departure(w.Buckets, w.GoodStocks, Cfg, s, null, out _) == FoundingVerdict.Legal) expected.Add(s.Value);
            }
            Assert.NotEmpty(expected);
            Assert.Equal(expected, listed.Select(a => (int)a.Id));
            foreach (ActionDescriptor a in listed)
            {
                var s = new SettlementId((int)a.Id);
                long party = FoundingQuery.AllocateParty(w.Buckets, s, FoundingQuery.PartyFraction(Cfg), null);
                long provisions = FoundingQuery.ProvisionsShare(w.GoodStocks, w.Buckets, s, grain, party);
                Assert.True(party >= 1 && provisions >= 1);
                Assert.Equal((ActionKind.Order, (OrderKind?)OrderKind.FoundSettlement), (a.Kind, a.Order));
                Assert.Equal("colonization.found." + a.Id.ToString(CultureInfo.InvariantCulture), a.Key);
                ActionTarget t = Assert.Single(a.Targets);
                Assert.Equal((ActionTargetKind.Settlement, a.Id), (t.Kind, t.Id));
                Assert.Null(a.Blocker);
                Assert.Equal(party.ToString(CultureInfo.InvariantCulture) + " people and " + provisions.ToString(CultureInfo.InvariantCulture)
                    + " " + grainName + " would set out", a.Detail);
                Assert.True(EmpireQuery.ControlsSettlement(w, p, s));   // never another polity's settlement
            }
        }
    }

    [Fact]
    public void ASourceThatCannotProvisionAParty_IsNotListed_TheOthersAre()
    {
        WorldState w = FoundingRigs.Dev(42UL);
        SettlementId src = w.Settlements[0].Id;
        Assert.Contains(Founding(w, Cfg, Player), a => a.Id == src.Value);   // control
        FoundingRigs.SinkGood(w, src, new GoodId(Cfg.Goods!.GrainId), FoundingRigs.Grain(w, src));
        Assert.Equal(FoundingVerdict.NoProvisions, FoundingQuery.Departure(w.Buckets, w.GoodStocks, Cfg, src, null, out _));
        ActionDescriptor[] listed = Founding(w, Cfg, Player);
        Assert.DoesNotContain(listed, a => a.Id == src.Value);
        Assert.NotEmpty(listed);
    }

    [Fact]
    public void WhenNoPartyCanForm_NothingIsListed_AndWithoutTerrainNothingIsListed()
    {
        WorldState w = FoundingRigs.Dev(42UL);
        Assert.NotEmpty(Founding(w, Cfg, Player));   // control
        SimConfig tiny = Cfg with { Colonization = new ColonizationConfig(1e-6) };   // the fraction floors to nobody
        Assert.Empty(Founding(w, tiny, Player));
        SimConfig none = Cfg with { Colonization = null };                           // the section absent: the order is inert
        Assert.Empty(Founding(w, none, Player));
        WorldState flat = w.Clone();
        flat.Terrain = null;
        Assert.Empty(Founding(flat, Cfg, Player));
    }

    [Fact]
    public void ASourceWithAFoundingAlreadyQueuedThisTurn_IsNotListedAgain_AnotherIssuersOrderDoesNotHideIt()
    {
        WorldState w = FoundingRigs.Dev(42UL, aiEmpires: 1);
        SettlementId src = Founding(w, Cfg, Player).Select(a => new SettlementId((int)a.Id)).First();
        OrderRecord mine = FoundingQuery.FoundOrder(w, Player, src, 12345);
        OrderRecord theirs = FoundingQuery.FoundOrder(w, Rival, src, 12345);
        OrderRecord otherKind = OrderRecord.From(w.Clock.Turn, Player, OrderKind.EnqueueConstruction, src.Value, 1);
        Assert.DoesNotContain(Founding(w, Cfg, Player, new ActionQueryContext([mine])), a => a.Id == src.Value);
        Assert.Contains(Founding(w, Cfg, Player, new ActionQueryContext([theirs, otherKind])), a => a.Id == src.Value);
        Assert.True(AvailableActionsQuery.FoundingQueued([mine], Player, src));
        Assert.False(AvailableActionsQuery.FoundingQueued([theirs, otherKind], Player, src));
    }

    [Fact]
    public void TheDescriptors_AreInTheQuerysOrder_DomainThenId_IndependentOfTableOrder()
    {
        WorldState w = FoundingRigs.Dev(42UL);
        ImmutableArray<ActionDescriptor> all = AvailableActionsQuery.For(w, Cfg, Player);
        ActionDescriptor[] founding = all.Where(a => a.Domain == ActionDomain.Colonization).ToArray();
        Assert.True(founding.Length > 1);
        for (int i = 1; i < founding.Length; i++) Assert.True(founding[i - 1].Id < founding[i].Id);
        Assert.True(AvailableActionsQuery.Same(all, AvailableActionsQuery.Order(all.Reverse().ToArray())));
    }
    [Fact]
    public void TheFoundingCard_CarriesTheListedDescriptor_AndOtherwiseSaysWhichSourceArmFails()
    {
        WorldState w = FoundingRigs.Dev(42UL, aiEmpires: 1);
        ImmutableArray<ActionDescriptor> actions = AvailableActionsQuery.For(w, Cfg, Player);
        var mine = new SettlementId((int)actions.First(a => a.Domain == ActionDomain.Colonization).Id);
        InfoCard card = InfoQuery.Card(w, Cfg, Player, InfoSubject.OfFounding(mine.Value), actions: actions);
        Assert.Equal(InfoStatus.Available, card.Status);
        Assert.True(actions.Single(a => a.Domain == ActionDomain.Colonization && a.Id == mine.Value).Equals(card.Action));
        Assert.Equal(card.Action!.Detail, card.Summary);

        // Another polity's settlement: not the player's to send settlers from (no descriptor).
        SettlementId theirs = default;
        for (int i = 0; i < w.Settlements.Count; i++) if (EmpireQuery.ControlsSettlement(w, Rival, w.Settlements[i].Id)) { theirs = w.Settlements[i].Id; break; }
        InfoCard foreign = InfoQuery.Card(w, Cfg, Player, InfoSubject.OfFounding(theirs.Value));
        Assert.Equal(InfoStatus.Locked, foreign.Status);
        Assert.Null(foreign.Action);
        Assert.Contains("is not yours", foreign.StatusLine);

        // Queued this turn: already ordered (no descriptor; the query does not list it).
        var queued = new ActionQueryContext([FoundingQuery.FoundOrder(w, Player, mine, 0)]);
        InfoCard ordered = InfoQuery.Card(w, Cfg, Player, InfoSubject.OfFounding(mine.Value), queued);
        Assert.Null(ordered.Action);
        Assert.StartsWith("Already ordered this turn", ordered.StatusLine);

        // An empty granary: cannot provision a party.
        FoundingRigs.SinkGood(w, mine, new GoodId(Cfg.Goods!.GrainId), FoundingRigs.Grain(w, mine));
        InfoCard empty = InfoQuery.Card(w, Cfg, Player, InfoSubject.OfFounding(mine.Value));
        Assert.Equal(InfoStatus.Locked, empty.Status);
        Assert.Contains("cannot provision a party", empty.StatusLine);

        // A settlement the player does not know names nothing.
        PolityKnowledge hides = PolityKnowledge.Restricted(w, Player, Mask(w, theirs, false), Mask(w, theirs, false));
        Assert.Equal(InfoStatus.Unknown, InfoQuery.Card(w, Cfg, Player, InfoSubject.OfFounding(theirs.Value), knowledge: hides).Status);
        Assert.Equal(InfoStatus.Unknown, InfoQuery.Card(w, Cfg, Player, InfoSubject.OfFounding(99999)).Status);
    }

    /// <summary>A known-node mask with every node known except the one holding <paramref name="s"/>'s site.</summary>
    private static bool[] Mask(IReadOnlyWorldState w, SettlementId s, bool hidden)
    {
        PolityKnowledge all = PolityKnowledge.Of(w, Player, null);
        var known = new bool[all.LatticeSize * all.LatticeSize];
        for (int n = 0; n < known.Length; n++) known[n] = true;
        known[all.NodeOfCell(FoundingRigs.SiteCellOf(w, s))] = hidden;
        return known;
    }
}
