using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Research;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;
using static Sim.Tests.Systems.IncrementalConstructionTests;

namespace Sim.Tests.State;

/// <summary>
/// D-055 (2026-10-16; the Director's batch-4 instruction, item 1; batch-4 design §1.6, track T1b) — THE HOLD RULE AS THE
/// READ-ONLY QUERIES SAY IT: SetAsideQuery (THE walk, read with HeadStatus's own setup), the Build plate's blocker
/// (ConstructionQuery.Blocker with the set-asides ahead of a new order), the project card's Met lines and note
/// (InfoQuery.ProjectCard), the road blocker's payers (AvailableActionsQuery.Roads — T1a round 2's open item: the payers road
/// development itself draws on, ConstructionQuery.EmpirePursePlaces, and what construction set aside within them), the
/// viewer's own set-aside totals, and the split's default wording. Display only: no test here steps a rule T1a did not
/// write.
///
/// THE RIG (IncrementalConstructionTests'): the founded dev world, seed 42, four settlements, all the player's unless
/// stated, timber and stone at 0 unless set, housing's draw 0, turn length 10 years. Q17: S0's granary ordered at turn 0,
/// part-built at p = 437,500 (18 of 40 timber and 9 of 20 stone in: 22 and 11 to go), with no builders — a valid wait, so
/// it SETS ASIDE what it still needs; S1's younger granary (ordered at turn 5) has builders. Each test carries its
/// PreReservations control (TestConfigs.PreReservations: batch 3's reading, word for word).
/// </summary>
public partial class SetAsideReadingTests
{
    private static readonly SimConfig Pre = TestConfigs.PreReservations(Cfg);
    private static readonly ResearchContent Research = TestConfigs.Research();
    private static readonly int PlayerPurse = Reservations.PolityPurse(Player);
    private static readonly Func<int, string> SName = id => "S" + id;
    private static readonly ConstructionProjectEntry GranaryP = Cfg.Goods!.ProjectById(Granary)!;
    private const long Part = 437_500;
    private const double Dt = 10.0;

    private static void Head(WorldState w, SettlementId s, long enqueued, int project = Granary, long progress = 0, bool row = true)
    {
        int slot = ConstructionQuery.NextSlot(w.ConstructionQueue, s);
        w.ConstructionQueue.Add(new ConstructionQueueRow(s, slot, project));
        if (row) w.ConstructionProgress.Add(new ConstructionProgressRow(s, slot, project, enqueued, progress));
    }

    /// <summary>Q17: S0's part-built granary (turn 0, no builders) and S1's younger one (turn 5, builders); the purse's timber
    /// and stone at S3.</summary>
    private static WorldState Q17(long timber, long stone, bool younger = true)
    {
        WorldState w = Solo();
        Head(w, S0, 0, Granary, Part);
        NoBuilders(w, S0);
        if (younger)
        {
            Head(w, S1, 5);
            AllBuilders(w, S1);
        }
        Set(w, S3, Timber, timber);
        Set(w, S3, Stone, stone);
        return w;
    }

    private static ActionQueryContext Next(IReadOnlyList<OrderRecord>? pending = null) => new(pending, Dt);

    private static (string Label, bool? Met, string? Note) Line(InfoCard card, string label)
    {
        foreach (InfoLink l in card.Requirements) if (l.Label == label) return (l.Label, l.Met, l.Note);
        throw new Xunit.Sdk.XunitException("no requirement line '" + label + "' on " + card.Title);
    }

    private static ActionDescriptor? BuildDescriptor(
        IReadOnlyWorldState w, SimConfig cfg, SettlementId s, int project = Granary, IReadOnlyList<OrderRecord>? pending = null)
    {
        var list = new List<ActionDescriptor>();
        AvailableActionsQuery.Construction(w, cfg, Player, Next(pending), list);
        long id = ((long)s.Value << 32) | (uint)project;
        foreach (ActionDescriptor a in list) if (a.Domain == ActionDomain.Construction && a.Id == id) return a;
        return null;
    }

    // ================================================================== the reading is HeadStatus's walk

    /// <summary>
    /// SetAsideQuery.Read is THE walk HeadStatus estimates with — the pending book, the split each Empire's pending orders put
    /// in force — so every display that reads it agrees with every head's status. Pinned head by head (cause, holder, the
    /// set-aside ahead, the head's own set-aside, p') on the Q17 rig with no pending orders, with a pending enqueue at S2 (a
    /// new head behind both) and with the holder's cancel pending (its set-aside released in the same step: S1 is then the
    /// oldest head). Mutant: Read without the pending book (the world's own queue) — the pending enqueue's head and the
    /// pending cancel both disappear from the reading while HeadStatus still sees them.
    /// </summary>
    [Fact]
    public void TheReading_IsHeadStatusesOwnWalk_HeadByHead_WithAndWithoutPendingOrders()
    {
        WorldState w = Q17(timber: 50, stone: 25);
        OrderRecord[][] batches =
        [
            [],
            [Enqueue(0, Player, S2)],
            [Cancel(0, Player, S0, 0)],
        ];
        int heldForEarlier = 0;
        foreach (OrderRecord[] pending in batches)
        {
            SetAsideReading reading = SetAsideQuery.Read(w, Cfg, pending, Dt);
            int expectedHeads = pending.Length == 1 && pending[0].Kind == OrderKind.EnqueueConstruction ? 3
                : pending.Length == 1 ? 1 : 2;
            Assert.Equal(expectedHeads, reading.Walk.Heads.Length);
            foreach (HoldsWalkHead head in reading.Walk.Heads)
            {
                HeadStatusReport status = ConstructionQuery.HeadStatus(w, Cfg, head.Entry.Settlement, pending, Dt)!;
                Assert.Equal((status.Cause, status.Holder, status.SetAsideAhead, status.Resolution.ProgressAfter),
                    (head.Cause, head.Holder, head.SetAsideAhead, head.Resolution.ProgressAfter));
                Assert.Equal(head.Holds, status.SetAside);
                if (head.Cause == MaterialCause.HeldForEarlier) heldForEarlier++;
            }
        }
        Assert.True(heldForEarlier >= 2, "anti-vacuity: S1 (and S2's pending head) wait on S0's set-aside");
    }

    // ================================================================== the Build plate's blocker

    /// <summary>
    /// THE BUILD PLATE (a would-be NEW order) is judged against the purse less EVERY set-aside of it (every head is ahead of a
    /// new order) and says so. S0 alone sets aside 22 timber and 11 stone; the purse holds 60 timber and 31 stone. A new
    /// granary at S2 (40 timber, 20 stone): 60 − 22 = 38 &lt; 40 — "40 timber (empire has 60; 22 set aside for earlier
    /// projects)"; 31 − 11 = 20 covers the stone. The descriptor the query lists carries exactly that blocker. CONTROL (batch
    /// 3, PreReservations): nothing is set aside, 60 and 31 cover the granary — no blocker at all. Mutant: the set-aside not
    /// subtracted in the blocker (the reading above with the set-asides ignored).
    /// </summary>
    [Fact]
    public void ANewOrdersBlocker_CountsEverySetAsideOfItsPurse_AndNamesIt_BatchThreeSeesNone()
    {
        WorldState w = Q17(timber: 60, stone: 31, younger: false);
        SetAsideReading reading = SetAsideQuery.Read(w, Cfg, null, Dt);
        long[] held = SetAsideQuery.HeldIn(reading.Walk, PlayerPurse);
        Assert.Equal((22L, 11L), (SetAsideQuery.At(held, Timber), SetAsideQuery.At(held, Stone)));     // PREMISE

        const string expected = "needs 40 timber (empire has 60; 22 set aside for earlier projects)";
        Assert.Equal(expected, ConstructionQuery.Blocker(w, Cfg, S2, GranaryP, Dt, 0, held));
        Assert.Equal(expected, BuildDescriptor(w, Cfg, S2)!.Blocker);
        Assert.Null(ConstructionQuery.Blocker(w, Cfg, S2, GranaryP, Dt, 0, setAside: null));    // the old reading: no shortfall
        Assert.Null(BuildDescriptor(w, Pre, S2)!.Blocker);                                      // CONTROL: batch 3

        // All of it set aside (22 timber, 11 stone): both inputs short, each clause names its set-aside; batch 3 the bare form.
        WorldState all = Q17(timber: 22, stone: 11, younger: false);
        Assert.Equal("needs 40 timber (empire has 22; 22 set aside for earlier projects), 20 stone (empire has 11; 11 set aside for earlier projects)",
            BuildDescriptor(all, Cfg, S2)!.Blocker);
        Assert.Equal("needs 40 timber (empire has 22), 20 stone (empire has 11)", BuildDescriptor(all, Pre, S2)!.Blocker);
    }

    private static readonly Lazy<WorldState> UniversityDuoAt3 = new(() =>
        UniversityRigs.Production(UniversityRigs.Cfg()).Run(WorldFounding.Found(TestConfigs.DevWorldgen() with { AiEmpires = 1 }, UniversityRigs.Cfg(), 42), 3));

    /// <summary>
    /// THE FOUND PLATE (AvailableActionsQuery.Institutions — a would-be NEW order of a university) is judged as the Build plate
    /// is: against the purse less EVERY set-aside of it (T1b step 2: the step-1 change to the Institutions contributor had no
    /// test, so a mutant passing no set-aside there survived — measured, records/t1b.md). The university rig at turn 3 (one
    /// AI), the player's university knowledge granted; every player settlement at a 0.5 building share with a food surplus of
    /// 2.0, its timber and stone at 0; a part-built granary (turn 0, no builders) at the first of them sets aside 22 timber and
    /// 11 stone; 1810 timber and 1205 stone at the second, the host. The military university (1800 timber, 1200 stone):
    /// 1810 − 22 &lt; 1800 and 1205 − 11 &lt; 1200 — the plate waits, naming each set-aside, and carries exactly the Build
    /// plate's blocker. Without the set-asides the purse covers it (no blocker). CONTROL (PreReservations, batch 3): nothing is
    /// set aside — no blocker. Mutant: the Institutions contributor passes no set-aside.
    /// </summary>
    [Fact]
    public void ANewFoundingsBlocker_CountsEverySetAsideOfItsPurse_AsTheBuildPlateDoes_BatchThreeSeesNone()
    {
        SimConfig cfg = UniversityRigs.Cfg(adultsPerUniversity: 100), pre = TestConfigs.PreReservations(cfg);
        WorldState w = UniversityDuoAt3.Value.Clone();
        UniversityRigs.Grant(w, Research, Player);
        SettlementId[] mine = LabourActivities.ControlledSettlements(w, Player);
        Assert.True(mine.Length >= 2, "the rig needs two of the player's settlements");
        foreach (SettlementId s in mine)
        {
            UniversityRigs.Construction(w, s, 0.5);
            UniversityRigs.Surplus(w, s, 2.0);
            Set(w, s, Timber, 0);
            Set(w, s, Stone, 0);
        }
        SettlementId holder = mine[0], host = mine[1];
        Head(w, holder, 0, Granary, Part);
        NoBuilders(w, holder);
        Set(w, host, Timber, 1_810);
        Set(w, host, Stone, 1_205);
        double dt = w.Clock.DtYears;
        ConstructionProjectEntry military = InstitutionContent.ProjectOfType(cfg, 1)!;

        long[] held = SetAsideQuery.HeldIn(SetAsideQuery.Read(w, cfg, null, dt).Walk, PlayerPurse);
        Assert.Equal((22L, 11L), (SetAsideQuery.At(held, Timber), SetAsideQuery.At(held, Stone)));     // PREMISE: the holder sets aside
        Assert.Null(ConstructionQuery.Blocker(w, cfg, host, military, dt, 0, setAside: null));          // PREMISE: the purse alone covers it

        string? Plate(SimConfig c)
        {
            var list = new List<ActionDescriptor>();
            AvailableActionsQuery.Institutions(w, c, Player, new ActionQueryContext(null, dt), list);
            long id = ((long)host.Value << 32) | (uint)military.Id;
            foreach (ActionDescriptor a in list) if (a.Domain == ActionDomain.Institutions && a.Id == id) return a.Blocker;
            throw new Xunit.Sdk.XunitException("the military university is not listed at the host");
        }
        string? plate = Plate(cfg);
        Assert.Equal(ConstructionQuery.Blocker(w, cfg, host, military, dt, 0, held), plate);
        Assert.Equal("needs 1800 timber (empire has 1810; 22 set aside for earlier projects), 1200 stone (empire has 1205; 11 set aside for earlier projects)",
            plate);
        Assert.Null(Plate(pre));                                                                         // CONTROL: batch 3
    }

    /// <summary>
    /// P7: BlockerSeenBy reads the set-asides only on the viewer's OWN books. In the duo world (S1, S3 the rival's) the
    /// rival's set-aside passed for a rival settlement is ignored: the viewer reads that settlement's own stock, batch 3's
    /// words. Its own settlement reads them. Mutant: BlockerSeenBy passes the set-asides whatever the viewer rules.
    /// </summary>
    [Fact]
    public void BlockerSeenBy_ReadsTheSetAsides_OnTheViewersOwnBooksOnly()
    {
        WorldState w = Duo();
        Set(w, S1, Timber, 30);
        Set(w, S1, Stone, 30);
        Set(w, S2, Timber, 30);
        Set(w, S2, Stone, 30);
        long[] aside = new long[Math.Max(Timber, Stone) + 1];
        aside[Timber] = 25;
        Assert.Equal("needs 40 timber (has 30)", ConstructionQuery.BlockerSeenBy(w, Cfg, Player, S1, GranaryP, Dt, 0, aside));
        Assert.Equal("needs 40 timber (empire has 30; 25 set aside for earlier projects)",
            ConstructionQuery.BlockerSeenBy(w, Cfg, Player, S2, GranaryP, Dt, 0, aside));
    }

    // ================================================================== the project card

    /// <summary>
    /// THE PROJECT CARD of a QUEUED HEAD reads the purse less the EARLIER heads' draws and set-asides (its Met lines) and
    /// names the holder in the note — "empire has 22; 22 set aside for S0's Granary, ahead in the building order" — while its
    /// status says what it will wait for. Q17 with exactly S0's need in the purse: S1's granary is Met for neither input
    /// (22 − 22 &lt; 40), and its head waits HeldForEarlier naming S0. The holder's own card (first in the building order)
    /// reads the whole purse: 22 to go, 22 held — Met. CONTROL (PreReservations): batch 3's card, word for word — Met (22 is
    /// counted against nothing) and the bare note. Mutant: the hold term dropped from Met.
    /// </summary>
    [Fact]
    public void AQueuedHeadsCard_ReadsThePurseLessTheEarlierHeads_AndNamesTheHolder_TheHoldersOwnCardDoesNot()
    {
        WorldState w = Q17(timber: 22, stone: 11);
        HeadStatusReport s1 = ConstructionQuery.HeadStatus(w, Cfg, S1, null, Dt)!;
        Assert.Equal((MaterialCause.HeldForEarlier, (QueueEntryRef?)new QueueEntryRef(S0, 0, Granary)), (s1.Cause, s1.Holder));   // PREMISE

        InfoCard card = InfoQuery.Card(w, Cfg, Player, InfoSubject.OfProject(Granary, S1.Value), Next(), SName);
        Assert.Equal(("40 timber", (bool?)false, (string?)"empire has 22; 22 set aside for S0's Granary, ahead in the building order"),
            Line(card, "40 timber"));
        Assert.Equal(("20 stone", (bool?)false, (string?)"empire has 11; 11 set aside for S0's Granary, ahead in the building order"),
            Line(card, "20 stone"));
        Assert.EndsWith(" It will wait for 40 timber (empire has 22; 22 set aside for earlier projects), 20 stone (empire has 11; 11 set aside for earlier projects).",
            card.StatusLine, StringComparison.Ordinal);

        InfoCard holder = InfoQuery.Card(w, Cfg, Player, InfoSubject.OfProject(Granary, S0.Value), Next(), SName);
        Assert.Equal(("40 timber - 18 in, 22 to go", (bool?)true, (string?)"empire has 22"), Line(holder, "40 timber - 18 in, 22 to go"));
        Assert.Equal(("20 stone - 9 in, 11 to go", (bool?)true, (string?)"empire has 11"), Line(holder, "20 stone - 9 in, 11 to go"));

        InfoCard pre = InfoQuery.Card(w, Pre, Player, InfoSubject.OfProject(Granary, S1.Value), Next(), SName);
        Assert.Equal(("40 timber", (bool?)false, (string?)"empire has 22"), Line(pre, "40 timber"));   // 22 < 40: short either way
        Assert.DoesNotContain("set aside", pre.StatusLine ?? "", StringComparison.Ordinal);
    }

    /// <summary>
    /// THE CARD'S Met IS FALSE EXACTLY WHEN THE HEAD WAITS ON AN EARLIER SET-ASIDE (batch-4 design §1.8 UI, L4-01). The purse
    /// holds 50 timber and 25 stone — enough for S1's granary on its own (40, 20), not with S0's 22 and 11 set aside ahead
    /// (28, 14). With S0 holding: HeadStatus says HeldForEarlier, the card's timber and stone lines are not Met, and a new
    /// granary at S2 will wait, naming the set-aside. With S0's cancel pending (its set-aside released in the same step):
    /// none of the three. And under PreReservations (batch 3): none of the three. Mutant: the hold term dropped from the
    /// card's Met (S1's lines Met while its head is HeldForEarlier).
    /// </summary>
    [Theory]
    [InlineData("holds", true)]
    [InlineData("holder cancelled", false)]
    [InlineData("batch 3", false)]
    public void TheCardsMetIsFalse_AndANewOrderWaits_ExactlyWhenTheHeadIsHeldForEarlier(string arm, bool held)
    {
        WorldState w = Q17(timber: 50, stone: 25);
        SimConfig cfg = arm == "batch 3" ? Pre : Cfg;
        OrderRecord[] pending = arm == "holder cancelled" ? [Cancel(0, Player, S0, 0)] : [];
        HeadStatusReport s1 = ConstructionQuery.HeadStatus(w, cfg, S1, pending, Dt)!;
        Assert.Equal(held, s1.Cause == MaterialCause.HeldForEarlier);

        InfoCard card = InfoQuery.Card(w, cfg, Player, InfoSubject.OfProject(Granary, S1.Value), Next(pending), SName);
        Assert.Equal(!held, Line(card, "40 timber").Met);
        Assert.Equal(!held, Line(card, "20 stone").Met);
        string? plate = BuildDescriptor(w, cfg, S2, Granary, pending)!.Blocker;
        Assert.Equal(held, plate is not null);
        if (held)
        {
            Assert.Equal("needs 40 timber (empire has 50; 22 set aside for earlier projects), 20 stone (empire has 25; 11 set aside for earlier projects)", plate);
            Assert.Equal("empire has 50; 22 set aside for S0's Granary, ahead in the building order", Line(card, "40 timber").Note);
        }
        else Assert.DoesNotContain("set aside", Line(card, "40 timber").Note ?? "", StringComparison.Ordinal);
    }

    /// <summary>
    /// A WOULD-BE NEW ORDER'S CARD (the project not queued at the settlement) reads the purse less EVERY set-aside of it and
    /// names every holder: S2's granary card with 60 timber and 31 stone, S0's 22 and 11 set aside — timber not Met (38 &lt;
    /// 40), stone Met (20 = 20), each note naming S0's granary. CONTROL (PreReservations): both Met, bare notes. Mutant: the
    /// card reads only the claims for a raw good (batch 3's Met).
    /// </summary>
    [Fact]
    public void ANewOrdersCard_ReadsThePurseLessEverySetAside_AndNamesTheHolders()
    {
        WorldState w = Q17(timber: 60, stone: 31, younger: false);
        InfoCard card = InfoQuery.Card(w, Cfg, Player, InfoSubject.OfProject(Granary, S2.Value), Next(), SName);
        Assert.Equal(("40 timber", (bool?)false, (string?)"empire has 60; 22 set aside for S0's Granary, ahead in the building order"),
            Line(card, "40 timber"));
        Assert.Equal(("20 stone", (bool?)true, (string?)"empire has 31; 11 set aside for S0's Granary, ahead in the building order"),
            Line(card, "20 stone"));
        InfoCard pre = InfoQuery.Card(w, Pre, Player, InfoSubject.OfProject(Granary, S2.Value), Next(), SName);
        Assert.Equal(("40 timber", (bool?)true, (string?)"empire has 60"), Line(pre, "40 timber"));
        Assert.Equal(("20 stone", (bool?)true, (string?)"empire has 31"), Line(pre, "20 stone"));

        // Two holders (S0's granary and S1's, both waiting with no builders): both named, in the building order, the units summed.
        WorldState two = Q17(timber: 60, stone: 31, younger: false);
        Head(two, S1, 3);
        NoBuilders(two, S1);
        InfoCard both = InfoQuery.Card(two, Cfg, Player, InfoSubject.OfProject(Granary, S2.Value), Next(), SName);
        Assert.Equal("empire has 60; 60 set aside for S0's Granary and S1's Granary, ahead in the building order", Line(both, "40 timber").Note);
        Assert.Equal("empire has 31; 31 set aside for S0's Granary and S1's Granary, ahead in the building order", Line(both, "20 stone").Note);
    }

    // ================================================================== the road blocker (T1a round 2's open item)

    private const long Road = 60;

    /// <summary>T1a's double-control road rig, rebuilt here: the duo world (S0, S2 the player's; S1, S3 the rival's) with a
    /// second control row for S0 (the rival's — S0 is its LOWEST controller's, the player's, purse alone), ONE candidate route
    /// S1–S3 for the rival (a 60 km dirt path: the trackway costs 60 timber), the rival's road research, every settlement on
    /// building, and the player's waiting granary at S2 (turn 0, no builders) setting aside S0's 40 timber and 20 stone.</summary>
    private static WorldState RivalRoadRig()
    {
        WorldState w = Duo();
        w.Controls.Add(new ControlRow(Rival, S0, 1.0));
        w.TransportEdges.Clear();
        w.TransportEdges.Add(new TransportEdgeRow(1, S1, S3, EdgeTypes.DirtPath, TransportModes.Road, TransportEdgeStates.Complete,
            100_000, Road, 0, 0, 0, EdgeTypes.DirtPath, 0.0, 1.0));
        w.SettlementDistances.Clear();
        w.SettlementDistances.Add(new SettlementDistanceRow(S1, S3, 100.0));
        w.SettlementDistances.Add(new SettlementDistanceRow(S3, S1, 100.0));
        w.TradeFlows.Clear();
        w.ResearchCompleted.Add(new ResearchCompletedRow(Rival, Research.Nodes[Research.IndexOfId("track_road")].Key));
        for (int s = 0; s < w.Settlements.Count; s++) AllBuilders(w, w.Settlements[s].Id);
        Head(w, S2, 0);
        NoBuilders(w, S2);
        Set(w, S0, Timber, 40);
        Set(w, S0, Stone, 20);
        return w;
    }

    private static string? RoadBlocker(IReadOnlyWorldState w, SimConfig cfg, PolityId polity)
    {
        var list = new List<ActionDescriptor>();
        AvailableActionsQuery.Roads(w, cfg, polity, list);
        foreach (ActionDescriptor a in list) if (a.Domain == ActionDomain.Roads) return a.Blocker;
        throw new Xunit.Sdk.XunitException("road development is not listed");
    }

    /// <summary>
    /// THE ROAD BLOCKER SAYS WHAT THE STEP WILL DO (T1a round 2's open item; ConstructionReservationTests'
    /// TwoControlRowsForOnePlace_TheOtherControllersRoadWorks_CannotSpendTheLowestControllersSetAside pins the step). With the
    /// hold rule on the rival's road works pay from its own purse places only (S1, S3 — S0 is the player's purse's), so with
    /// <paramref name="atS3"/> = 0 they can pay nothing and the blocker says so; with 25 at S3 they pay 25 and there is no
    /// blocker. With the rule off (batch 3) S0 is a payer: its 40 timber pay, no blocker. MEASURED before this change (the
    /// listing still read PayingSettlements): no blocker in the (on, 0) row while the step paid 0. Mutant: the road blocker
    /// back on PayingSettlements.
    /// </summary>
    [Theory]
    [InlineData(true, 0L, "needs 60 timber for the first route")]
    [InlineData(true, 25L, null)]
    [InlineData(false, 0L, null)]
    public void TheRoadBlocker_CountsOnlyThePayersRoadDevelopmentDrawsOn(bool rule, long atS3, string? expected)
    {
        WorldState w = RivalRoadRig();
        Set(w, S3, Timber, atS3);
        SimConfig cfg = rule ? Cfg : Pre;
        Assert.Equal([S1, S3, S0], RoadDevelopmentQuery.PayingSettlements(w, Rival, S1, S3));       // PREMISE: S0 is a rival payer
        Assert.Equal(expected, RoadBlocker(w, cfg, Rival));
    }

    /// <summary>
    /// The road blocker also subtracts what construction SETS ASIDE within the payers (RoadDevelopmentQuery.SetAsideWithin; since
    /// the T1b fix round on the coming step's walk, SetAsideQuery.StepTable — with no pending order it re-derives what the
    /// published table holds): the rival's waiting granary at S1 (turn 0, no builders)
    /// sets aside S3's 40 timber in purse 2 (published by a construction step), so the rival's road works find 40 − 40 = 0
    /// and the blocker names the set-aside. CONTROL (batch 3, PreReservations): nothing is set aside, S3's 40 and S0's 40 pay
    /// — no blocker. Mutant: the set-aside not subtracted in the road blocker.
    /// </summary>
    [Fact]
    public void TheRoadBlocker_SubtractsWhatConstructionSetAsideWithinThePayers_AndSaysSo()
    {
        WorldState w = RivalRoadRig();
        Head(w, S1, 0);
        NoBuilders(w, S1);
        Set(w, S3, Timber, 40);
        Set(w, S3, Stone, 20);
        WorldState on = ConstructionOnly(Cfg, null).Step(w);
        Assert.Contains(new ConstructionHoldRow(Reservations.PolityPurse(Rival), Timber, 40), Rows(on.ConstructionHolds));   // PREMISE
        Assert.Equal("needs 60 timber for the first route (set aside for building projects: 40 timber)", RoadBlocker(on, Cfg, Rival));
        WorldState off = ConstructionOnly(Pre, null).Step(w);
        Assert.Empty(Rows(off.ConstructionHolds));
        Assert.Null(RoadBlocker(off, Pre, Rival));
    }

    private static T[] Rows<T>(IReadOnlyTable<T> table) where T : unmanaged
    {
        var rows = new T[table.Count];
        for (int i = 0; i < rows.Length; i++) rows[i] = table[i];
        return rows;
    }

    // ================================================================== the viewer's own totals; the split's default words

    /// <summary>
    /// THE EMPIRE SUMMARY'S DATA (SetAsideQuery.ViewerTotals): the viewer's OWN purse in the published table — its Empire purse
    /// and the single-settlement purses of the places it rules — summed per good, ascending good id; never another purse (P7);
    /// nothing with the rule off whatever the table holds. On the duo world after a construction step in which the player's
    /// granary at S2 sets aside S0's 40 timber and 20 stone and the rival's at S1 sets aside S3's 30 timber: the player reads
    /// (timber 40, stone 20), the rival (timber 30). A hand-added single-settlement row of the player's S2 is counted for the
    /// player only. Mutant: the purse filter dropped (every row summed).
    /// </summary>
    [Fact]
    public void TheViewersTotals_AreItsOwnPurseOnly_AndNothingWithTheRuleOff()
    {
        WorldState w = Duo();
        Head(w, S2, 0);
        NoBuilders(w, S2);
        Head(w, S1, 0);
        NoBuilders(w, S1);
        Set(w, S0, Timber, 40);
        Set(w, S0, Stone, 20);
        Set(w, S3, Timber, 30);
        WorldState t1 = ConstructionOnly(Cfg, null).Step(w);
        Assert.Equal([(Timber, 40L), (Stone, 20L)], SetAsideQuery.ViewerTotals(t1, Cfg, Player));
        Assert.Equal([(Timber, 30L)], SetAsideQuery.ViewerTotals(t1, Cfg, Rival));
        Assert.Empty(SetAsideQuery.ViewerTotals(t1, Pre, Player));

        WorldState hand = t1.Clone();
        var rows = new List<ConstructionHoldRow>(Rows(hand.ConstructionHolds)) { new(Reservations.SettlementPurse(S2), Timber, 5) };
        rows.Sort(Reservations.CompareKeys);
        hand.ConstructionHolds.Clear();
        foreach (ConstructionHoldRow r in rows) hand.ConstructionHolds.Add(r);
        Assert.Equal([(Timber, 45L), (Stone, 20L)], SetAsideQuery.ViewerTotals(hand, Cfg, Player));
        Assert.Equal([(Timber, 30L)], SetAsideQuery.ViewerTotals(hand, Cfg, Rival));
    }

    /// <summary>
    /// THE SPLIT'S DEFAULT WORDS (batch-4 design §1.6: "road works get only what building work uses or sets aside" — worded so
    /// it says the rule: what is LEFT after building work uses or sets aside what it needs): the split's card and the
    /// Materials descriptor say so with the hold rule on; batch 3's "… only what building work leaves" with it off.
    /// </summary>
    [Fact]
    public void TheSplitsDefaultWords_SayBuildingWorkUsesOrSetsAside_BatchThreeKeepsItsOwn()
    {
        WorldState w = Solo();
        InfoCard on = InfoQuery.Card(w, Cfg, Player, InfoSubject.MaterialsSplit, Next(), SName);
        InfoCard off = InfoQuery.Card(w, Pre, Player, InfoSubject.MaterialsSplit, Next(), SName);
        Assert.StartsWith("Building first (default): road works get only what is left after building work uses or sets aside what it needs.",
            on.StatusLine, StringComparison.Ordinal);
        Assert.StartsWith("Building first (default): road works get only what building work leaves.", off.StatusLine, StringComparison.Ordinal);

        w.ResearchCompleted.Add(new ResearchCompletedRow(Player, Research.Nodes[Research.IndexOfId("track_road")].Key));
        WorldState routed = RoadsKnown(w);
        string Detail(SimConfig cfg)
        {
            var list = new List<ActionDescriptor>();
            AvailableActionsQuery.Roads(routed, cfg, Player, list);
            foreach (ActionDescriptor a in list) if (a.Domain == ActionDomain.ResourceAllocation) return a.Detail!;
            throw new Xunit.Sdk.XunitException("the Materials descriptor is not listed");
        }
        Assert.Equal("building first (default): road works get only what is left after building work uses or sets aside what it needs", Detail(Cfg));
        Assert.Equal("building first (default): road works get only what building work leaves", Detail(Pre));
    }

    /// <summary>The solo world with one candidate route S0–S1 (a 60 km dirt path), so road development is listed.</summary>
    private static WorldState RoadsKnown(WorldState w)
    {
        WorldState r = w.Clone();
        r.TransportEdges.Clear();
        r.TransportEdges.Add(new TransportEdgeRow(1, S0, S1, EdgeTypes.DirtPath, TransportModes.Road, TransportEdgeStates.Complete,
            100_000, Road, 0, 0, 0, EdgeTypes.DirtPath, 0.0, 1.0));
        r.SettlementDistances.Clear();
        r.SettlementDistances.Add(new SettlementDistanceRow(S0, S1, 100.0));
        r.SettlementDistances.Add(new SettlementDistanceRow(S1, S0, 100.0));
        return r;
    }
}
