using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Research;
using Sim.Ui.Actions;
using Sim.Ui.ViewModel;
using Xunit;

namespace Sim.Ui.Tests;

/// <summary>
/// D-055 (2026-10-17; batch 4, track T1b, FIX ROUND — the adjudicated review findings T1b-L1-F1…F11 and T1b-L2-V2-F1…F6).
/// Each test pins one corrected behaviour on the rigs of <see cref="ConstructionHoldsUiTests"/> (the Q17 rig unless stated) with
/// its control, and names the semantic mutant it kills. Display only: no test here steps a rule T1a did not write, except the
/// step-level road-charge checks, which read what the step does to confirm what the surface says.
/// </summary>
public partial class ConstructionHoldsUiTests
{
    private static string Name(Rig r, SettlementId s) => r.S.Names.Name(s.Value);

    private static string Remedy(Rig r, SettlementId holder) =>
        " - it has no builders (give " + Label(r.S.World) + " a share in Labour at " + Name(r, holder) + ", or cancel it)";

    private static long RoadUnits(IReadOnlyWorldState w)
    {
        long units = 0;
        for (int i = 0; i < w.RoadDevelopments.Count; i++) if (w.RoadDevelopments[i].Polity == Me) units += w.RoadDevelopments[i].MaterialUnits;
        return units;
    }

    // ================================================================== F1: the holder's remedy follows its own limit

    /// <summary>
    /// FIX ROUND F1 — A HOLDER BUILT ONLY AS FAR AS ITS STONE ALLOWS is not offered more labour. A (ordered at turn 0, p =
    /// 437,500, EVERY adult building — there is no larger share to give) has 22 timber and no stone: it advances only to
    /// 450,000 (the units it already has the stone for) and sets aside the 22 timber; B (ordered at turn 1, p = 25,000 — its
    /// next units need timber only) waits on that set-aside. B's line names A's own limit and the only remedy that frees the
    /// timber: "… - it is being built only as far as its stone allows (cancel it to free them)" — A's own row says "as far as
    /// its stone allows". Before the fix B was told to give Building "a larger share in Labour" at A, which moves nothing.
    /// Mutant: the holder's progress checked before its limit (the labour remedy for every advancing holder).
    /// </summary>
    [Fact]
    public void AHolderBuiltOnlyAsFarAsItsStoneAllows_TheWaitingLineNamesTheStone_NotMoreLabour()
    {
        Rig r = Q17(holderTurn: 0, youngerTurn: 1, timber: 22, stone: 0, youngerProgress: 25_000, more: (w, holder) => Share(w, holder, 1.0));
        HeadStatusReport holder = Status(r.S, r.A);
        Assert.Equal((HeadLimitKind.Materials, Stone, Part, 450_000L), (holder.Resolution.Limit, holder.Resolution.LimitGood,
            holder.Resolution.ProgressBefore, holder.Resolution.ProgressAfter));                                   // PREMISE
        Assert.Equal("work goes on at End Turn, as far as its stone allows (an estimate)", HeadOf(Surface(r.S, r.A)).Status);
        Assert.Equal(22L, holder.SetAside![Timber]);
        Assert.Equal((MaterialCause.HeldForEarlier, (QueueEntryRef?)new QueueEntryRef(r.A, 0, Granary)), (Status(r.S, r.B).Cause, Status(r.S, r.B).Holder));

        Assert.Equal("waiting for timber: the empire's 22 are set aside for " + Name(r, r.A) + "'s Granary, ordered earlier (turn 1), ahead in the "
            + "building order - it is being built only as far as its stone allows (cancel it to free them)", HeadOf(Surface(r.S, r.B)).Status);
    }

    // ================================================================== F3: the label names what physically binds

    /// <summary>
    /// FIX ROUND F3 — A HEAD SHORT OF A GOOD NOBODY SETS ASIDE IS TOLD THAT GOOD. (a) A (no builders) sets aside all 22 timber
    /// and there is no stone at all: B could not move even with A's timber, so it is not held for A — "waiting for stone: the
    /// empire has none", exactly what the rule off and A's cancel pending say (it read "waiting for timber: not enough yet").
    /// (b) A builds at a labour pace, drawing 2 timber and the last stone and setting aside 20 timber: B is short of stone, which
    /// A takes first — "waiting for stone: A's Granary, ordered earlier, takes the empire's 1 first", exactly the rule-off line
    /// (it read "waiting for timber: …, takes the empire's 22 first", though A takes 2). Mutant: the label's good taken from the
    /// budget resolution (the one the set-aside made bind) instead of the counterfactual A0.
    /// </summary>
    [Fact]
    public void AHeadShortOfAGoodNobodySetsAside_IsToldThatGood_AsTheRuleOffAndTheHoldersCancelSay()
    {
        Rig a = Q17(holderTurn: 0, youngerTurn: 1, timber: 22, stone: 0);
        Assert.Equal((22L, 0L), (Status(a.S, a.A).SetAside![Timber], Status(a.S, a.A).SetAside![Stone]));         // PREMISE
        const string none = "waiting for stone: the empire has none";
        Assert.Equal((MaterialCause.EmpireHasNone, Stone), (Status(a.S, a.B).Cause, Status(a.S, a.B).Resolution.LimitGood));
        Assert.Equal(none, HeadOf(Surface(a.S, a.B)).Status);
        Assert.Equal(none, HeadOf(Surface(a.S, a.B, PreHolds)).Status);                                            // CONTROL: rule off
        Assert.True(a.S.EmitCancelConstruction(a.A.Value, 0));
        Assert.Equal(none, HeadOf(Surface(a.S, a.B)).Status);                                                      // CONTROL: A cancelled

        Rig b = Q17(holderTurn: 0, youngerTurn: 1, timber: 22, stone: 1, more: (w, holder) =>
        {
            for (int i = 0; i < w.Housing.Count; i++) if (w.Housing[i].Settlement == holder) w.Housing.Ref(i).LastLaborUsed = 0.0;
            Share(w, holder, 0.5 / (InstitutionStaffing.LabourAdults(w, Cfg, holder) * Dt(w)));
        });
        HeadStatusReport holder = Status(b.S, b.A);
        Assert.Equal((2L, 1L, 20L, 0L), (holder.Resolution.Draws[0], holder.Resolution.Draws[1], holder.SetAside![Timber], holder.SetAside[Stone]));
        string takes = "waiting for stone: " + Name(b, b.A) + "'s Granary, ordered earlier, takes the empire's 1 first";
        Assert.Equal((MaterialCause.TakenByEarlier, Stone, 1L), (Status(b.S, b.B).Cause, Status(b.S, b.B).Resolution.LimitGood, Status(b.S, b.B).HeldAtStart));
        Assert.Equal(takes, HeadOf(Surface(b.S, b.B)).Status);
        Assert.Equal(takes, HeadOf(Surface(b.S, b.B, PreHolds)).Status);                                           // CONTROL: rule off
    }

    // ================================================================== F2 and F6: the road blocker and the basic preview

    // D-063 (tranche 2, 2026-10-24): the two F2 tests below STEP the world, and the tranche-2 research content's realized
    // effects move what one End Turn leaves (MEASURED on the shipped content: the published set-aside 34 timber, not 33; with
    // nothing pending the enqueue rig's road paid 8 units, not 6). Their subject is the construction hold, not research, so they run on the pre-tranche-2 content
    // (TestTranche2) — the same rig, founding and steps — where their measured literals hold.
    private static readonly SimConfig T2Cfg = TestTranche2.PreTranche2;
    private static readonly SimConfig T2PreHolds = T2Cfg with { Construction = T2Cfg.Construction! with { Reservations = false } };

    private static readonly Lazy<WorldState> T2RoadWorld = new(() =>
    {
        WorldState w = UiFounding.Found(42, 256, 4);
        ResearchContent research = T2Cfg.Research!;
        foreach (string id in ActionSurfacePreview.WithAncestors(research, "track_road"))
            w.ResearchCompleted.Add(new ResearchCompletedRow(Me, research.Nodes[research.IndexOfId(id)].Key));
        UiSession s = UiSession.StartFromWith(T2Cfg, w, 42, 256, 4);
        s.EndTurn();
        return s.World.Clone();
    });

    /// <summary>The F2 rig: the trackway world, every settlement farming only (no production of timber or stone), a granary at
    /// A ordered last turn with no builders, 60 timber at B and 15 stone at A; one End Turn — the step publishes A's set-aside
    /// (33 timber, all that is left after the dwellings' upkeep, and 15 stone).</summary>
    private static (UiSession S, SettlementId A) PublishedHold()
    {
        WorldState w = T2RoadWorld.Value.Clone();
        SettlementId[] mine = Mine(w, Me);
        foreach (SettlementId s in mine) { Set(w, s, Timber, 0); Set(w, s, Stone, 0); Share(w, s, 0.0); }
        Queue(w, mine[0], w.Clock.Turn - 1);
        Set(w, mine[1], Timber, 60);
        Set(w, mine[0], Stone, 15);
        UiSession s0 = UiSession.StartFromWith(T2Cfg, w, 42, 256, 4);
        s0.EndTurn();
        return (s0, mine[0]);
    }

    private static long StepRoadUnits(WorldState from, Action<UiSession> orders)
    {
        UiSession s = UiSession.StartFromWith(T2Cfg, from.Clone(), 42, 256, 4);
        orders(s);
        long before = RoadUnits(s.World);
        Assert.True(s.EmitRoadOrder(100.0));
        s.EndTurn();
        return RoadUnits(s.World) - before;
    }

    /// <summary>
    /// FIX ROUND F2 — THE ROAD BLOCKER SAYS WHAT THE STEP WILL DO, A PENDING CANCEL INCLUDED. The step published A's set-aside
    /// (33 timber); the road order's first route needs 245 timber. With nothing pending the blocker names the set-aside and the
    /// step pays the road nothing. With A's cancel pending (its set-aside released in the same step — design §1.2) the blocker
    /// is gone, the basic preview's affordable fraction equals the split preview's (33 of the 1434 timber), and the step pays
    /// the road (6 units). Before the fix the blocker and the basic preview read the LAST step's table and kept the set-aside.
    /// CONTROL (the rule off): no blocker either way. Mutant: the blocker (and the basic preview) back on the published table.
    /// </summary>
    [Fact]
    public void TheRoadBlocker_ReadsTheStepsOwnWalk_APendingCancelOfTheHolderClearsIt_AndTheStepPays()
    {
        (UiSession s, SettlementId a) = PublishedHold();
        Assert.Equal([new ConstructionHoldRow(Me.Value, Timber, 33), new ConstructionHoldRow(Me.Value, Stone, 15)],
            Enumerable.Range(0, s.World.ConstructionHolds.Count).Select(i => s.World.ConstructionHolds[i]).ToArray());   // PREMISE
        WorldState published = s.World.Clone();
        Assert.Equal("needs 245 timber for the first route (set aside for building projects: 33 timber)", Surface(s, a).Roads!.Blocker);
        Assert.Null(Surface(s, a, T2PreHolds).Roads!.Blocker);
        Assert.Equal(0L, StepRoadUnits(published, _ => { }));                                                     // the step agrees

        int slot = s.World.ConstructionQueue[0].Slot;
        Assert.True(s.EmitCancelConstruction(a.Value, slot));
        ActionSurfaceModel cancelled = Surface(s, a);
        Assert.Null(cancelled.Roads!.Blocker);
        RoadPlanPreview p = ActionSurface.RoadPlan(s.World, T2Cfg, Me, 100.0, s.Names.Name, cancelled.Allocation, numerals: true);
        Assert.Equal(33.0 / 1434.0, p.Affordable, 12);
        Assert.Equal(p.AfterBuilding!.Value, p.Affordable, 12);
        Assert.Equal(6L, StepRoadUnits(published, x => Assert.True(x.EmitCancelConstruction(a.Value, slot))));     // the step agrees
    }

    /// <summary>
    /// FIX ROUND F2 — A PENDING ENQUEUE'S SET-ASIDE IS COUNTED. Nothing is queued or published; 33 timber is left after one
    /// End Turn. With no order pending there is no blocker and the step pays the road 6 units; with a granary ordered at A this
    /// turn (no builders: the step's walk sets aside what it finds) the blocker names the set-aside, the basic preview's
    /// affordable fraction is 0 as the split preview's is, and the step pays the road nothing. CONTROL (the rule off): no blocker.
    /// Mutant: the blocker (and the basic preview) back on the published table.
    /// </summary>
    [Fact]
    public void TheRoadBlocker_CountsAPendingEnqueuesSetAside_AndTheStepPaysNothing()
    {
        WorldState w = T2RoadWorld.Value.Clone();
        SettlementId[] mine = Mine(w, Me);
        foreach (SettlementId x in mine) { Set(w, x, Timber, 0); Set(w, x, Stone, 0); Share(w, x, 0.0); }
        Set(w, mine[1], Timber, 60);
        Set(w, mine[0], Stone, 15);
        UiSession pre = UiSession.StartFromWith(T2Cfg, w, 42, 256, 4);
        pre.EndTurn();
        WorldState start = pre.World.Clone();
        Assert.Equal(0, start.ConstructionHolds.Count);                                                           // PREMISE
        UiSession s = UiSession.StartFromWith(T2Cfg, start.Clone(), 42, 256, 4);
        Assert.Null(Surface(s, mine[0]).Roads!.Blocker);
        Assert.Equal(6L, StepRoadUnits(start, _ => { }));

        Assert.True(s.EmitConstructionOrder(mine[0].Value, Granary));
        ActionSurfaceModel ordered = Surface(s, mine[0]);
        Assert.Equal("needs 245 timber for the first route (set aside for building projects: 33 timber)", ordered.Roads!.Blocker);
        Assert.Null(Surface(s, mine[0], T2PreHolds).Roads!.Blocker);
        RoadPlanPreview p = ActionSurface.RoadPlan(s.World, T2Cfg, Me, 100.0, s.Names.Name, ordered.Allocation, numerals: true);
        Assert.Equal((0.0, 0.0), (p.Affordable, p.AfterBuilding!.Value));
        Assert.Equal(0L, StepRoadUnits(start, x => Assert.True(x.EmitConstructionOrder(mine[0].Value, Granary))));
    }

    /// <summary>
    /// FIX ROUND F6 (and L2-F6) — THE BASIC ROADS PREVIEW (no split: construction.allocationRatios off) SUBTRACTS WHAT THE STEP
    /// SETS ASIDE. A's waiting granary sets aside 22 timber; the purse holds the road's R timber plus 10: the preview's affordable
    /// fraction is (R + 10 − 22) / R; with the hold rule off (nothing set aside) it is all of it. The PAINTED Roads block (no
    /// Materials block without the split), at the screen's default draft of 10% (one route, R10 timber) with R10 + 10 timber in
    /// the purse: "(we can pay …)" while A sets aside 22; with A's cancel pending — released in the step — "(affordable)": the
    /// screen passes the Roads block's pending orders to the preview. Mutants: the set-aside not subtracted in the basic
    /// preview; the screen's preview read without the pending orders.
    /// </summary>
    [Fact]
    public void TheBasicRoadsPreview_WithoutTheSplit_SubtractsWhatTheStepSetsAside()
    {
        long road = RoadTimber(RoadWorld.Value);
        Rig r = Q17(holderTurn: 0, youngerTurn: 1, timber: road + 10, stone: 11, younger: false);
        SimConfig noSplit = Cfg with { Construction = Cfg.Construction! with { AllocationRatios = false } };
        SimConfig noSplitNoHolds = noSplit with { Construction = noSplit.Construction! with { Reservations = false } };
        Assert.False(ResourceAllocation.Enabled(noSplit));
        Assert.True(Reservations.Enabled(noSplit));                                                                // PREMISE
        Assert.Equal(22L, Status(r.S, r.A, noSplit).SetAside![Timber]);
        RoadPlanPreview p = ActionSurface.RoadPlan(r.S.World, noSplit, Me, 100.0, r.S.Names.Name);
        Assert.Null(p.LeftLine);                                                                                   // no split: the basic reading
        Assert.Equal((double)(road + 10 - 22) / road, p.Affordable, 12);
        Assert.Equal(1.0, ActionSurface.RoadPlan(r.S.World, noSplitNoHolds, Me, 100.0, r.S.Names.Name).Affordable, 12);

        long draft = 0;
        foreach (RoadDevelopmentStep step in RoadDevelopmentQuery.Plan(RoadWorld.Value, Cfg.Research!, Cfg.Roads!, Cfg.Goods!, Me, 10.0))
            foreach (RoadMaterialCost m in step.Cost) if (m.Good.Value == Timber) draft += m.Units;
        Rig painted = Q17(holderTurn: 0, youngerTurn: 1, timber: draft + 10, stone: 11, younger: false);
        Assert.Null(Surface(painted.S, painted.A, noSplit).Allocation);                                            // PREMISE: no Materials block
        string before = All(Paint(painted.S, painted.A, noSplit).Texts);
        Assert.Contains("Estimated cost on current stocks: " + Inv(draft) + " timber (we can pay ", before, StringComparison.Ordinal);
        Assert.True(painted.S.EmitCancelConstruction(painted.A.Value, 0));
        Assert.Contains("Estimated cost on current stocks: " + Inv(draft) + " timber (affordable)", All(Paint(painted.S, painted.A, noSplit).Texts), StringComparison.Ordinal);
    }

    // ================================================================== L2-F1: the roads line under a binding split

    /// <summary>
    /// FIX ROUND L2-F1 — THE ROADS HOLD LINE READS THE SPLIT FORECAST'S GRANTS. TheRoadsPreview_CountsWhatBuildingWorkSetsAside's
    /// rig (A's waiting granary, 22 timber to go; the purse the road's R timber plus 10) at b = 0: the split withholds, its
    /// forecast grants building work 10 timber, so the forecast's heads set aside 10 — the split's BuildingHolds — and the line
    /// says "10 timber is set aside for A's Granary; road works may not use it", agreeing with the left line (everything the
    /// order needs is left). (At b = 100 and 50 the grants do not bind: 22.) Mutant: the hold line walked without the grants.
    /// </summary>
    [Fact]
    public void TheRoadsHoldLine_UnderASplitThatCapsBuildingsGrant_SaysWhatTheForecastSetsAside()
    {
        long road = RoadTimber(RoadWorld.Value);
        Rig r = Q17(holderTurn: 0, youngerTurn: 1, timber: road + 10, stone: 11, younger: false);
        Assert.True(r.S.EmitAllocationOrder(0));
        AllocationBlock block0 = Surface(r.S, r.A).Allocation!;
        AllocationView view = ResourceAllocationQuery.ForEmpire(r.S.World, Cfg, Me, r.S.QueuedOrders(), Dt(r.S.World), 100.0)!;
        long[]? grants = SetAsideQuery.ForecastGrants(view.Split);
        Assert.NotNull(grants);                                                                                    // PREMISE: it binds
        Assert.Equal(10L, view.Split.BuildingHoldOf(Timber));
        HoldsWalkHead[] heads = SetAsideQuery.HeadsUnder(r.S.World, Cfg, Me, r.S.QueuedOrders(), Dt(r.S.World), grants);
        Assert.Equal(10L, heads.Sum(h => h.HoldOf(Timber)));
        RoadPlanPreview p = ActionSurface.RoadPlan(r.S.World, Cfg, Me, 100.0, r.S.Names.Name, block0, numerals: false);
        Assert.Equal("10 timber is set aside for " + Name(r, r.A) + "'s Granary; road works may not use it", p.SetAside);
        Assert.StartsWith("after what building work uses or sets aside, everything this order needs is left", p.LeftLine, StringComparison.Ordinal);
    }

    // ================================================================== F5 / L2-F2: the split clause on the holder's row

    /// <summary>
    /// FIX ROUND F5 / L2-F2 — THE HOLDER'S ROW UNDER A SPLIT BELOW 100 (design §1.6: "Under b &lt; 100 it also says: the split
    /// lets road works use the rest"), queued and in force. A's waiting granary sets aside 22 timber and 11 stone. At b = 100
    /// the row has no clause; with b = 50 ordered this turn it has it (the surface reads the split as it will stand at End
    /// Turn); the rule off has no row; after End Turn (b = 50 in force) it still has it; with b = 100 ordered back it has none.
    /// Where the split's grant binds (30 timber, a road order for all of it, b = 50) A sets aside only part of its timber need
    /// and the row says so with the clause. Mutants: the clause never emitted; the split read without the pending orders;
    /// the split compared with &lt;= 100.
    /// </summary>
    [Fact]
    public void TheHoldersRow_UnderASplitBelow100_SaysTheSplitLetsRoadWorksUseTheRest_QueuedAndInForce()
    {
        const string row = "sets aside 22 timber and 11 stone while it waits; projects ordered later wait for it";
        const string clause = "; the split lets road works use the rest";
        Rig r = Q17(holderTurn: 0, youngerTurn: 1, timber: 22, stone: 11, younger: false);
        Assert.Equal(row, HeadOf(Surface(r.S, r.A)).SetAside);                                                     // b = 100
        Assert.True(r.S.EmitAllocationOrder(50));
        Assert.Equal(100, ResourceAllocation.EffectivePercent(r.S.World, Me));                                    // PREMISE: only ordered
        Assert.Equal(row + clause, HeadOf(Surface(r.S, r.A)).SetAside);                                            // queued
        Assert.Null(HeadOf(Surface(r.S, r.A, PreHolds)).SetAside);                                                 // CONTROL: rule off
        r.S.EndTurn();
        Assert.Equal(50, ResourceAllocation.EffectivePercent(r.S.World, Me));
        Assert.Equal(row + clause, HeadOf(Surface(r.S, r.A)).SetAside);                                            // in force
        Assert.True(r.S.EmitAllocationOrder(100));
        Assert.Equal(row, HeadOf(Surface(r.S, r.A)).SetAside);                                                     // ordered back

        Rig capped = Q17(holderTurn: 0, youngerTurn: 1, timber: 30, stone: 11, younger: false);
        Assert.True(capped.S.EmitRoadOrder(100.0));
        Assert.True(capped.S.EmitAllocationOrder(50));
        HeadStatusReport a = Status(capped.S, capped.A);
        Assert.Equal((15L, 11L), (a.SetAside![Timber], a.SetAside[Stone]));                                        // PREMISE: the grant binds
        Assert.Equal("sets aside 15 timber and 11 stone while it waits; projects ordered later wait for it" + clause, HeadOf(Surface(capped.S, capped.A)).SetAside);
    }

    // ================================================================== L2-F6: the singular, and two holders on the roads line

    /// <summary>
    /// FIX ROUND L2-F6 — ONE UNIT IS "is": A (p = 975,000: 1 timber to go, its stone all in) sets aside the empire's one timber;
    /// B waits on it: "waiting for timber: the empire's 1 is set aside for A's Granary, …". Mutant: always "are".
    /// </summary>
    [Fact]
    public void OneUnitSetAside_IsSingular()
    {
        Rig r = Q17(holderTurn: 0, youngerTurn: 1, timber: 1, stone: 1, progress: 975_000);
        Assert.Equal((1L, 0L), (Status(r.S, r.A).SetAside![Timber], Status(r.S, r.A).SetAside![Stone]));           // PREMISE
        Assert.Equal("waiting for timber: the empire's 1 is set aside for " + Name(r, r.A) + "'s Granary, ordered earlier (turn 1), ahead in the "
            + "building order" + Remedy(r, r.A), HeadOf(Surface(r.S, r.B)).Status);
    }

    /// <summary>
    /// FIX ROUND L2-F6 — TWO HOLDERS ON THE ROADS LINE, IN THE BUILDING ORDER (HoldText.RoadsLine): A's granary (turn 0, 22
    /// timber to go) and C's (turn 3, not started: 40), both waiting with no builders; the purse holds the road's timber plus
    /// 100. "22 timber is set aside for A's Granary and 40 timber for C's Granary; road works may not use them". Mutant: the
    /// heads listed in reverse.
    /// </summary>
    [Fact]
    public void TheRoadsHoldLine_NamesEveryHolder_InTheBuildingOrder()
    {
        long road = RoadTimber(RoadWorld.Value);
        SettlementId c = default;
        Rig r = Q17(holderTurn: 0, youngerTurn: 1, timber: road + 100, stone: 31, younger: false, more: (w, holder) =>
        {
            c = Mine(w, Me)[2];
            Queue(w, c, 3);
            Share(w, c, 0.0);
        });
        RoadPlanPreview p = ActionSurface.RoadPlan(r.S.World, Cfg, Me, 100.0, r.S.Names.Name, Surface(r.S, r.A).Allocation, numerals: false);
        Assert.Equal("22 timber is set aside for " + Name(r, r.A) + "'s Granary and 40 timber for " + Name(r, c) + "'s Granary; road works may not use them",
            p.SetAside);
    }

    // ================================================================== F7: the earlier heads' draws on the waiting line

    /// <summary>
    /// FIX ROUND F7 — THE WAITING LINE ACCOUNTS FOR THE EARLIER HEADS' DRAWS. E1 (turn 0, p = 650,000, builders) completes and
    /// draws 7 of the empire's 12 stone; E2 (turn 1, not started, no builders) sets aside the other 5; B (turn 2, p = 700,000,
    /// 6 stone to go) waits on E2: "waiting for stone: 5 of the empire's 12 are set aside for E2's Granary, ordered earlier
    /// (turn 2), ahead in the building order, and 7 are used this turn by projects ahead of it - …" — the figures add up.
    /// Mutant: the earlier draws not passed to the line.
    /// </summary>
    [Fact]
    public void TheWaitingLine_SaysWhatTheEarlierHeadsUseThisTurn()
    {
        WorldState w = RoadWorld.Value.Clone();
        SettlementId[] m = Mine(w, Me);
        foreach (SettlementId s in m) { Set(w, s, Timber, 0); Set(w, s, Stone, 0); }
        Queue(w, m[0], 0, 650_000); Share(w, m[0], 1.0);
        Queue(w, m[1], 1); Share(w, m[1], 0.0);
        Queue(w, m[2], 2, 700_000); Share(w, m[2], 1.0);
        Set(w, m[0], Timber, 500); Set(w, m[0], Stone, 12);
        var r = new Rig(UiSession.StartFrom(w, 42, 256, 4), m[1], m[2]);
        HeadStatusReport b = Status(r.S, m[2]);
        Assert.Equal((MaterialCause.HeldForEarlier, 12L, 5L), (b.Cause, b.HeldAtStart, b.SetAsideAhead));        // PREMISE
        Assert.Equal(7L, Status(r.S, m[0]).Resolution.Draws[1]);
        Assert.Equal("waiting for stone: 5 of the empire's 12 are set aside for " + Name(r, m[1]) + "'s Granary, ordered earlier (turn 2), ahead in "
            + "the building order, and 7 are used this turn by projects ahead of it" + Remedy(r, m[1]), HeadOf(Surface(r.S, m[2])).Status);
    }

    // ================================================================== F10, F11

    /// <summary>
    /// FIX ROUND F10 — TWO MIGRATED ENTRIES (no progress rows: both keys −1, their order turns unknown) are not "ordered the same
    /// turn": the holder is first in the building order by its settlement id, and the line says only that. Mutant: the −1 tie
    /// read as a same-turn tie.
    /// </summary>
    [Fact]
    public void TwoMigratedEntries_AreFirstInTheBuildingOrder_NotOrderedTheSameTurn()
    {
        WorldState w = RoadWorld.Value.Clone();
        SettlementId[] m = Mine(w, Me);
        foreach (SettlementId s in m) { Set(w, s, Timber, 0); Set(w, s, Stone, 0); }
        Queue(w, m[0], 0, row: false); Share(w, m[0], 0.0);
        Queue(w, m[1], 0, row: false); Share(w, m[1], 1.0);
        Set(w, m[0], Timber, 40); Set(w, m[0], Stone, 20);
        var r = new Rig(UiSession.StartFrom(w, 42, 256, 4), m[0], m[1]);
        Assert.Equal(0, r.S.World.ConstructionProgress.Count);                                                    // PREMISE: both −1
        Assert.Equal("waiting for timber: the empire's 40 are set aside for " + Name(r, m[0]) + "'s Granary, first in the building order"
            + Remedy(r, m[0]), HeadOf(Surface(r.S, m[1])).Status);
    }

    /// <summary>
    /// FIX ROUND F11 — A NEW ORDER THAT WILL WAIT BEHIND A SET-ASIDE is not told it starts. A (turn 0) waits with no builders and
    /// sets aside 22 timber and 11 stone; a granary ordered this turn at B (every adult building) is first in line there, but
    /// its status is HeldForEarlier: "Granary ordered - it is first in line, but at End Turn it waits for timber set aside for
    /// earlier projects; later, cancelling loses what went in". CONTROL (the rule off): it starts — batch 3's words. Mutant:
    /// the new line dropped (batch 3's "it starts" for a held head).
    /// </summary>
    [Fact]
    public void ANewOrderHeldBehindASetAside_IsToldItWaits_NotThatItStarts()
    {
        Rig r = Q17(holderTurn: 0, youngerTurn: 1, timber: 22, stone: 11, younger: false, more: (w, _) => Share(w, Mine(w, Me)[1], 1.0));
        Assert.True(r.S.EmitConstructionOrder(r.B.Value, Granary));
        Assert.Equal(MaterialCause.HeldForEarlier, Status(r.S, r.B).Cause);                                        // PREMISE
        Assert.Equal("Granary ordered - it is first in line, but at End Turn it waits for timber set aside for earlier projects; later, cancelling loses what went in",
            Surface(r.S, r.B).Construction!.Ordered[0]);
        Assert.StartsWith("Granary ordered - it is first in line, so at End Turn it starts and may use up to ",
            Surface(r.S, r.B, PreHolds).Construction!.Ordered[0], StringComparison.Ordinal);
    }

    // ================================================================== F8: a doubly-controlled place (P7)

    /// <summary>
    /// FIX ROUND F8 — THE INFORMATION CLASS IN A WORLD WITH TWO CONTROL ROWS FOR ONE PLACE (P7; design §1.6 Visibility: hold notes
    /// read the viewer's OWN purse). The one-AI world with the LOWER-id Empire's first place also controlled by the higher one: the
    /// place's stock is the lower Empire's purse's alone. A waiting granary at the lower Empire's second place — which the higher
    /// one does not rule — sets aside 22 timber and 11 stone. The higher controller's card for a granary at the shared place names
    /// no set-aside and no foreign queue entry; the lower controller's own card at the same place names its holder. Mutant: the
    /// card's gate back on "rules the place" alone.
    /// </summary>
    [Fact]
    public void ADoublyControlledPlace_TheHigherControllersCard_NamesNoForeignHead_TheOwnersCardDoes()
    {
        WorldState w = UiFounding.Found(42, 256, 4, aiEmpiresOverride: 1);
        PolityId ai = default;
        for (int i = 0; i < w.Polities.Count; i++) if (w.Polities[i].Id.Value != Me.Value) ai = w.Polities[i].Id;
        (PolityId low, PolityId high) = Me.Value < ai.Value ? (Me, ai) : (ai, Me);
        SettlementId[] lows = Mine(w, low);
        SettlementId shared = lows[0], other = lows[1];
        w.Controls.Add(new ControlRow(high, shared, 1.0));
        for (int s = 0; s < w.Settlements.Count; s++) { Set(w, w.Settlements[s].Id, Timber, 0); Set(w, w.Settlements[s].Id, Stone, 0); }
        w.ConstructionQueue.Clear();
        w.ConstructionProgress.Clear();
        Queue(w, other, 0, Part);
        Share(w, other, 0.0);
        Set(w, other, Timber, 22);
        Set(w, other, Stone, 11);
        Assert.True(EmpireQuery.ControlsSettlement(w, high, shared));
        Assert.False(EmpireQuery.ControlsSettlement(w, high, other));                                             // PREMISE
        double dt = ActionQueryContext.ForNextStep(w, Era).NextDtYears!.Value;
        Func<int, string> name = id => "S" + Inv(id);
        InfoCard CardOf(PolityId viewer) =>
            InfoQuery.Card(w, Cfg, viewer, InfoSubject.OfProject(Granary, shared.Value), new ActionQueryContext(null, dt), name);
        string foreign = name(other.Value) + "'s Granary";

        InfoCard seen = CardOf(high);
        string said = seen.StatusLine + " " + string.Join(" ", seen.Requirements.Select(l => l.Note));
        Assert.DoesNotContain("set aside", said, StringComparison.Ordinal);
        Assert.DoesNotContain(foreign, said, StringComparison.Ordinal);
        Assert.Equal("empire has 22; 22 set aside for " + foreign + ", ahead in the building order",
            CardOf(low).Requirements.First(l => l.Label == "40 timber").Note);                                     // CONTROL: the owner
    }

    // ================================================================== F9: a claim nothing sets aside keeps its parenthetical

    /// <summary>
    /// FIX ROUND F9 — A CLAIM THE HOLD RULE DOES NOT SET ASIDE KEEPS SAYING SO. A's queue: a granary (the head, waiting with no
    /// builders) and behind it a workshop claiming 10 tools — a non-head entry: it sets nothing aside. A would-be workshop's card
    /// at C: "empire has 25; 10 counted toward projects already queued at A (this does not set it aside: …)" — batch 3's words,
    /// still true. When the workshop is the head and the purse has only 6 tools it sets aside 6 of its 10: "(4 of it is not set
    /// aside: …)", then names the 6. Mutants: the parenthetical dropped whenever the rule is on; the partial clause dropped.
    /// </summary>
    [Fact]
    public void AClaimNothingSetsAside_KeepsItsParenthetical_APartialSetAsideSaysHowMuchIsNot()
    {
        const int Workshop = 2;
        const string notSetAside = " (this does not set it aside: earlier projects in the building order, and road works under the split, can use it first)";
        InfoLink ToolsLine(UiSession s, SettlementId at, SimConfig cfg) =>
            InfoQuery.Card(s.World, cfg, Me, InfoSubject.OfProject(Workshop, at.Value), ActionQueryContext.ForNextStep(s.World, Era, s.QueuedOrders()), s.Names.Name)
                .Requirements.First(l => l.Label.Contains("tools", StringComparison.Ordinal));

        SettlementId c = default;
        Rig behind = Q17(holderTurn: 0, youngerTurn: 1, timber: 500, stone: 500, younger: false, more: (w, holder) =>
        {
            foreach (SettlementId s in Mine(w, Me)) Set(w, s, Tools, 0);
            Queue(w, holder, 0, project: Workshop);
            Set(w, holder, Tools, 25);
            c = Mine(w, Me)[2];
        });
        string counted = "empire has 25; 10 counted toward projects already queued at " + Name(behind, behind.A);
        Assert.Equal(0L, SetAsideQuery.At(SetAsideQuery.HeldIn(SetAsideQuery.Read(behind.S.World, Cfg, null, Dt(behind.S.World)).Walk, Me.Value), Tools));   // PREMISE
        Assert.Equal(counted + notSetAside, ToolsLine(behind.S, c, Cfg).Note);
        Assert.Equal(counted + notSetAside, ToolsLine(behind.S, c, PreHolds).Note);                                 // CONTROL: batch 3

        Rig head = Q17(holderTurn: 0, youngerTurn: 1, timber: 500, stone: 500, younger: false, more: (w, holder) =>
        {
            w.ConstructionQueue.Clear();
            w.ConstructionProgress.Clear();
            foreach (SettlementId s in Mine(w, Me)) Set(w, s, Tools, 0);
            Queue(w, holder, 0, project: Workshop);
            Set(w, holder, Tools, 6);
        });
        Assert.Equal(6L, Status(head.S, head.A).SetAside![Tools]);                                                 // PREMISE
        Assert.Equal("empire has 6; 10 counted toward projects already queued at " + Name(head, head.A)
            + " (4 of it is not set aside: earlier projects in the building order, and road works under the split, can use it first); 6 set aside for "
            + Name(head, head.A) + "'s Workshop, ahead in the building order", ToolsLine(head.S, c, Cfg).Note);
    }
}
