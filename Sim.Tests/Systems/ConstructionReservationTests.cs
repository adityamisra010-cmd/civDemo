using System.Buffers.Binary;
using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Construction;
using Sim.Core.Systems.Research;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;
using static Sim.Tests.Systems.IncrementalConstructionTests;

namespace Sim.Tests.Systems;

/// <summary>
/// D-055 (2026-10-16; the Director's batch-4 instruction, item 1: projects must not consume resources reserved by other
/// projects — an older, partially built project must not lose the materials it still needs to a younger one while it cannot
/// advance; this resolves D-054 Q17) — MATERIAL RESERVATIONS ACROSS TURNS ("holds"). The batch-4 design §1.8, tests #1–#25.
///
/// THE RULE (ConstructionQuery.HoldsWalk, one pure function for the step, HeadStatus and the split's forecast): every queue
/// HEAD that is not RELEASED holds its remaining need, per purse key and good, in the building order. Per purse and good the
/// budget starts at S (or the building grant G_b under a split); each head is resolved against the budget, the budget falls
/// by its draw, then the head holds min(need after the draw, budget) and the budget falls by the hold. Released (an INVALID
/// reservation): (a) unknown project; (b) no labour adults; (c) an institution with no institutions configuration.
///
/// THE RIG (IncrementalConstructionTests'): the founded dev world (seed 42, four settlements, all the player's unless stated;
/// the PLAYER's purse key is its polity id 1), every timber and stone row at exactly 0 unless set, housing's draw 0, turn
/// length 10 years, ConstructionSystem alone unless a test names road development. Every figure below is computed by hand
/// in the comment beside it; progress values are chosen off the 1,000 grid (a granary's 40 timber gives multiples of
/// 25,000, so most younger heads build a test project of 41 timber and 23 stone). Each test carries a PREMISE guard — the
/// PreReservations twin (TestConfigs.PreReservations: the batch-3 rule) reproduces the defect or the old value the test
/// moves — and is named in the step-2 mutant table (t1a-out/notes-2.md) with the mutant that kills it.
/// </summary>
public class ConstructionReservationTests
{
    private static readonly SimConfig Pre = TestConfigs.PreReservations(Cfg);
    private static readonly ResearchContent Research = TestConfigs.Research();
    private static readonly int PlayerPurse = Reservations.PolityPurse(Player);
    private static readonly int RivalPurse = Reservations.PolityPurse(Rival);
    private const int Palisade = 92;          // test-only: timber 31, labour 1
    private const int Cairn = 93;             // test-only: stone 20, labour 1
    private const int Workshop = 2;           // goods.json: 60 timber, 40 stone, 10 tools
    private const int University = 11;        // goods.json: 1,800 timber, 1,200 stone, 240 adult-years; founds an institution
    private static readonly int Tools = Cfg.Goods!.IdOf("tools");

    /// <summary>The shipped config plus the test projects: Works (id 90: timber 41, stone 23, labour 1 — completes in a
    /// step when its materials allow), Palisade (timber 31) and Cairn (stone 20).</summary>
    private static readonly SimConfig Shipped = With(WithWorks(Cfg, 1.0, Works, timber: 41, stone: 23));

    private static SimConfig With(SimConfig cfg)
    {
        GoodsConfig goods = cfg.Goods!;
        return cfg with
        {
            Goods = goods with
            {
                Projects = [.. goods.Projects!,
                    new ConstructionProjectEntry(Palisade, "palisade", [new ProjectInput("timber", 31)], 1.0),
                    new ConstructionProjectEntry(Cairn, "cairn", [new ProjectInput("stone", 20)], 1.0)],
            },
        };
    }

    private static readonly SimConfig ShippedPre = TestConfigs.PreReservations(Shipped);

    // ================================================================== helpers

    /// <summary>Appends a queue entry at <paramref name="s"/>'s next slot with its progress row (EnqueuedTurn, p).</summary>
    private static void Head(WorldState w, SettlementId s, long enqueued, int project, long progress = 0)
    {
        int slot = ConstructionQuery.NextSlot(w.ConstructionQueue, s);
        w.ConstructionQueue.Add(new ConstructionQueueRow(s, slot, project));
        w.ConstructionProgress.Add(new ConstructionProgressRow(s, slot, project, enqueued, progress));
    }

    private static ConstructionHoldRow[] Holds(IReadOnlyWorldState w)
    {
        var rows = new ConstructionHoldRow[w.ConstructionHolds.Count];
        for (int i = 0; i < rows.Length; i++) rows[i] = w.ConstructionHolds[i];
        return rows;
    }

    private static ConstructionHoldRow Row(int purse, int good, long units) => new(purse, good, units);

    /// <summary>THE walk on <paramref name="w"/> as a construction-only step with no orders sees it (no split).</summary>
    private static HoldsWalkResult Walk(IReadOnlyWorldState w, SimConfig cfg) =>
        ConstructionQuery.HoldsWalk(w, cfg, w.ConstructionQueue, w.ConstructionProgress, w.GoodStocks, 10.0, []);

    private static HeadStatusReport Status(IReadOnlyWorldState w, SimConfig cfg, SettlementId s, IReadOnlyList<OrderRecord>? orders = null) =>
        ConstructionQuery.HeadStatus(w, cfg, s, orders, 10.0)!;

    private static WorldState Step(WorldState w, SimConfig cfg, params OrderRecord[] orders) =>
        ConstructionOnly(cfg, orders.Length == 0 ? null : Log(orders)).Step(w);

    private static QueueEntryRef Ref(SettlementId s, int project, int slot = 0) => new(s, slot, project);

    /// <summary>Removes every adult of <paramref name="s"/> through the Ledger (a depopulated settlement: Sim.Core never
    /// removes a settlement, so a destroyed or abandoned one is this).</summary>
    private static void NoAdults(WorldState w, SettlementId s)
    {
        var ledger = new Ledger(w.LedgerFlows);
        for (int i = 0; i < w.Buckets.Count; i++)
        {
            BucketRow b = w.Buckets[i];
            if (b.Settlement != s || b.CohortIdx < Cohorts.FirstAdult || b.CohortIdx >= Cohorts.FirstElder || b.Count.Value == 0) continue;
            ledger.Flow(ref w.Buckets.Ref(i).Count, ConservedQuantityIds.Population, ReasonIds.InitialEndowment, b.Count.Value,
                FlowDirection.Sink, OverdrawPolicy.Throw);
        }
    }

    // ================================================================== #1–#3: the Q17 shape and the labour pace

    /// <summary>#1 Q17 at p &gt; 0: an older part-built head with no builders keeps its remaining need; the younger head waits,
    /// HeldForEarlier naming it; next step, builders back, the older head completes from exactly those units.</summary>
    [Fact]
    public void Q17_AnOlderPartBuiltHeadWithoutBuilders_KeepsItsNeed_TheYoungerWaitsNamingIt_ThenTheOlderCompletes()
    {
        WorldState w = Solo();
        Head(w, S0, 0, Granary, 437_500);                 // delivered ceil(17.5) = 18 timber, ceil(8.75) = 9 stone
        NoBuilders(w, S0);
        Head(w, S1, 5, Granary);
        AllBuilders(w, S1);
        Set(w, S3, Timber, 22);                           // exactly S0's remaining need: 40 − 18, 20 − 9
        Set(w, S3, Stone, 11);
        Assert.Equal((22L, 11L), (ConstructionQuery.Remaining(40, 437_500), ConstructionQuery.Remaining(20, 437_500)));

        // PREMISE (the batch-3 defect, D-054 Q17): the younger head takes all of it — floor(22N/40) = floor(11N/20) = 550,000.
        WorldState pre = Step(w, Pre);
        Assert.Equal((437_500L, 550_000L, 0L, 0L), (P(pre, S0), P(pre, S1), TotalOf(pre, Timber), TotalOf(pre, Stone)));

        WorldState t1 = Step(w, Cfg);
        Assert.Equal((437_500L, 0L, 22L, 11L), (P(t1, S0), P(t1, S1), TotalOf(t1, Timber), TotalOf(t1, Stone)));
        Assert.Equal([Row(PlayerPurse, Timber, 22), Row(PlayerPurse, Stone, 11)], Holds(t1));

        HeadStatusReport younger = Status(w, Cfg, S1);
        Assert.Equal((HeadLimitKind.Materials, MaterialCause.HeldForEarlier, Timber), (younger.Resolution.Limit, younger.Cause, younger.Resolution.LimitGood));
        Assert.Equal(Ref(S0, Granary), younger.Holder);
        Assert.Equal(22, younger.SetAsideAhead);
        HeadStatusReport older = Status(w, Cfg, S0);
        Assert.Equal((HeadLimitKind.Labour, false, HoldRelease.None), (older.Resolution.Limit, older.Resolution.NoLabourAdults, older.Release));
        Assert.Equal(22, older.SetAside![Timber]);

        AllBuilders(t1, S0);
        WorldState t2 = Step(t1, Cfg);
        Assert.Equal((1L, 0L, 0L, 0L), (Built(t2, S0), P(t2, S1), TotalOf(t2, Timber), TotalOf(t2, Stone)));
        Assert.Empty(Holds(t2));                          // S1 is the only head left and its purse is empty
        AssertConserved(t2);
    }

    /// <summary>#2 Q17 at p = 0: an older head that has not started still holds its whole cost.</summary>
    [Fact]
    public void Q17_AnOlderHeadAtProgressZero_HoldsItsWholeCost_TheYoungerGetsOnlyTheSurplus()
    {
        WorldState w = Solo();
        Head(w, S1, 0, Granary);
        NoBuilders(w, S1);
        Head(w, S0, 1, Works);                            // 41 timber, 23 stone
        AllBuilders(w, S0);
        Set(w, S2, Timber, 47);
        Set(w, S2, Stone, 27);

        // PREMISE: batch 3 lets the younger head complete from the older one's materials.
        WorldState pre = Step(w, ShippedPre);
        Assert.Equal((0L, 1L), (P(pre, S1), Built(pre, S0, Works)));

        // S1 holds 40 / 20; S0 gets 7 / 7: p' = min(floor(7N/41) = 170,731, floor(7N/23) = 304,347) = 170,731, drawing
        // ceil(41 × .170731) = 7 timber and ceil(23 × .170731) = 4 stone; it then holds min(19, 7 − 4) = 3 stone.
        WorldState t1 = Step(w, Shipped);
        Assert.Equal((0L, 170_731L), (P(t1, S1), P(t1, S0)));
        Assert.Equal((40L, 23L), (TotalOf(t1, Timber), TotalOf(t1, Stone)));   // 47 − 7, 27 − 4
        Assert.Equal([Row(PlayerPurse, Timber, 40), Row(PlayerPurse, Stone, 23)], Holds(t1));
        AssertConserved(t1);
    }

    /// <summary>#3 An older head progressing at its LABOUR PACE keeps the rest of its need (recon P3): it draws its third
    /// and holds the other two thirds; a younger head with builders to spare gets only the surplus beyond them.</summary>
    [Fact]
    public void AnOlderHeadAtALabourPace_KeepsTheRestOfItsNeed_TheYoungerGetsOnlyTheSurplus()
    {
        WorldState w = Solo();
        AllBuilders(w, S3);
        AllBuilders(w, S0);
        SimConfig cfg = WithWorks(Cfg, 3.0 * C(w, S3, 10.0));              // C / L = 1/3: 333,333 a step
        Head(w, S3, 0, Works);                                              // timber 40, stone 20
        Head(w, S0, 1, Granary);
        Set(w, S1, Timber, 200);
        Set(w, S1, Stone, 27);

        // PREMISE: batch 3 — S3 draws 14 / 7, S0 takes the other 20 stone and completes.
        WorldState pre = Step(w, TestConfigs.PreReservations(cfg));
        Assert.Equal((333_333L, 1L, 0L), (P(pre, S3), Built(pre, S0), TotalOf(pre, Stone)));

        // S3: p' = 333,333, draws ceil(13.33) = 14 timber and ceil(6.67) = 7 stone; holds its remaining 26 / 13.
        // S0: stone budget 27 − 7 − 13 = 7 → p' = floor(7N/20) = 350,000, drawing ceil(14) = 14 timber and 7 stone; it then
        // holds min(26, 200 − 14 − 26 − 14) = 26 timber and min(13, 0) = 0 stone.
        WorldState t1 = Step(w, cfg);
        Assert.Equal((333_333L, 350_000L), (P(t1, S3), P(t1, S0)));
        Assert.Equal((172L, 13L), (TotalOf(t1, Timber), TotalOf(t1, Stone)));
        Assert.Equal([Row(PlayerPurse, Timber, 52), Row(PlayerPurse, Stone, 13)], Holds(t1));
        HoldsWalkHead s3 = Walk(w, cfg).HeadOf(S3)!;
        Assert.Equal((14L, 7L, 26L, 13L), (s3.DrawOf(Timber), s3.DrawOf(Stone), s3.HoldOf(Timber), s3.HoldOf(Stone)));

        // Next step: S3 → 666,666 draws 13 timber and 7 stone; the 6 stone left is exactly its remaining need, so S0 waits.
        WorldState t2 = Step(t1, cfg);
        Assert.Equal((666_666L, 350_000L, 6L), (P(t2, S3), P(t2, S0), TotalOf(t2, Stone)));
        Assert.Equal([Row(PlayerPurse, Timber, 39), Row(PlayerPurse, Stone, 6)], Holds(t2));   // 13 + 26 timber; S3's 6 stone
        AssertConserved(t2);
    }

    // ================================================================== #4–#6: missing materials, location, long waits

    /// <summary>#4 Missing materials (R1-TEST-GAPS a): an older head that is short of stone holds its timber against a
    /// younger timber-only head.</summary>
    [Fact]
    public void AnOlderHeadShortOfOneInput_HoldsItsOtherInputs_AgainstAYoungerHeadThatNeedsOnlyThose()
    {
        WorldState w = Solo();
        AllBuilders(w, S1);
        AllBuilders(w, S0);
        Head(w, S1, 0, Granary);                          // builders, but no stone anywhere
        Head(w, S0, 1, Palisade);                         // timber 31 only
        Set(w, S2, Timber, 55);

        WorldState pre = Step(w, ShippedPre);
        Assert.Equal((0L, 1L), (P(pre, S1), Built(pre, S0, Palisade)));           // PREMISE: batch 3 builds the palisade

        // S1 cannot move (stone 0) and holds its 40 timber; S0 gets 15: p' = floor(15N/31) = 483,870.
        WorldState t1 = Step(w, Shipped);
        Assert.Equal((0L, 483_870L, 40L), (P(t1, S1), P(t1, S0), TotalOf(t1, Timber)));
        Assert.Equal([Row(PlayerPurse, Timber, 40)], Holds(t1));
        AssertCause(w, Shipped, S1, MaterialCause.EmpireHasNone, null, Stone);
        AssertCause(w, Shipped, S0, MaterialCause.HeldForEarlier, Ref(S1, Granary), Timber);
    }

    private static void AssertCause(IReadOnlyWorldState w, SimConfig cfg, SettlementId s, MaterialCause cause, QueueEntryRef? holder, int good)
    {
        HoldsWalkHead head = Walk(w, cfg).HeadOf(s)!;
        Assert.Equal((cause, holder, good), (head.Cause, head.Holder, head.Resolution.LimitGood));
        HeadStatusReport status = Status(w, cfg, s);
        Assert.Equal((cause, holder), (status.Cause, status.Holder));
    }

    /// <summary>#5 Location (R1-TEST-GAPS b): the held units sit in the YOUNGER head's own settlement — the draw order takes
    /// the builder's own stock first — and it still cannot draw them.</summary>
    [Fact]
    public void TheHeldUnitsSitInTheYoungerHeadsOwnSettlement_AndItStillCannotDrawThem()
    {
        WorldState w = Solo();
        Head(w, S1, 0, Granary, 262_500);                 // delivered ceil(10.5) = 11 timber, ceil(5.25) = 6 stone
        NoBuilders(w, S1);
        Head(w, S0, 1, Granary);
        AllBuilders(w, S0);
        Set(w, S0, Timber, 29);                           // S1's remaining need, held at S0 itself
        Set(w, S0, Stone, 14);

        WorldState pre = Step(w, Pre);                    // PREMISE: batch 3 — S0 builds from its own stock (700,000)
        Assert.Equal((700_000L, 1L, 0L), (P(pre, S0), Of(pre, S0, Timber), Of(pre, S0, Stone)));   // 29 − 28, 14 − 14

        WorldState t1 = Step(w, Cfg);
        Assert.Equal((262_500L, 0L), (P(t1, S1), P(t1, S0)));
        Assert.Equal((29L, 14L), (Of(t1, S0, Timber), Of(t1, S0, Stone)));       // untouched, in S0's own rows
        Assert.Equal([Row(PlayerPurse, Timber, 29), Row(PlayerPurse, Stone, 14)], Holds(t1));
        Assert.Equal(MaterialCause.HeldForEarlier, Status(w, Cfg, S0).Cause);
    }

    /// <summary>#6 (L2-09a) A VALID long wait keeps holding: an older university whose host cannot found it (a market or
    /// food-surplus viability failure — transient) and an older workshop whose crafted input cannot be made yet (research
    /// can open a recipe) each hold; the younger granary with no surplus waits, and its status names the holder, whose own
    /// status gives its reason; neither holder is released (the player's remedy is to cancel).</summary>
    [Fact]
    public void AViabilityWaitAndAnUnobtainableWait_KeepHolding_TheYoungerWaitsNamingTheHolder()
    {
        // (a) Viability.
        WorldState w = Solo();
        AllBuilders(w, S0);
        AllBuilders(w, S1);
        Head(w, S0, 0, University, 123_457);              // delivered ceil(222.22) = 223 timber, ceil(148.15) = 149 stone
        Head(w, S1, 1, Granary);
        Set(w, S2, Timber, 100);
        Set(w, S2, Stone, 50);
        UniversityViability v = InstitutionViability.ToFound(w, Cfg, S0);
        Assert.False(v.Viable);                                                     // PREMISE: the host cannot found it now
        Assert.NotEqual(ViabilityFailure.NoConfig, v.Failure);                      // ... for a transient reason
        Assert.True(InstitutionStaffing.LabourAdults(w, Cfg, S0) > 0.0);
        Assert.Equal(1L, Built(Step(w, Pre), S1));                                  // PREMISE: batch 3 builds the granary

        WorldState t1 = Step(w, Cfg);
        Assert.Equal((123_457L, 0L, 0L), (P(t1, S0), P(t1, S1), Built(t1, S1)));
        Assert.Equal([Row(PlayerPurse, Timber, 100), Row(PlayerPurse, Stone, 50)], Holds(t1));
        HeadStatusReport holder = Status(w, Cfg, S0), waiting = Status(w, Cfg, S1);
        Assert.Equal((HeadLimitKind.Viability, HoldRelease.None), (holder.Resolution.Limit, holder.Release));
        Assert.Equal((MaterialCause.HeldForEarlier, Ref(S0, University)), (waiting.Cause, waiting.Holder));

        // (b) Unobtainable: the workshop needs 10 tools; the purse holds none and no recipe here makes them.
        WorldState u = Solo();
        AllBuilders(u, S0);
        AllBuilders(u, S1);
        Head(u, S0, 0, Workshop);
        Head(u, S1, 1, Granary);
        Set(u, S2, Timber, 50);
        Set(u, S2, Stone, 30);
        Assert.Equal(0, TotalOf(u, Tools));
        Assert.Equal(HeadLimitKind.Unobtainable, ConstructionQuery.StructuralStall(u, Cfg, S0, 10.0)!.Limit);   // PREMISE
        Assert.Equal(1L, Built(Step(u, Pre), S1));

        WorldState u1 = Step(u, Cfg);
        Assert.Equal((0L, 0L), (P(u1, S0), Built(u1, S1)));
        Assert.Equal([Row(PlayerPurse, Timber, 50), Row(PlayerPurse, Stone, 30)], Holds(u1));
        HeadStatusReport ws = Status(u, Cfg, S0);
        Assert.Equal((HeadLimitKind.Unobtainable, HoldRelease.None), (ws.Resolution.Limit, ws.Release));
        Assert.Equal(MaterialCause.HeldForEarlier, Status(u, Cfg, S1).Cause);
        Assert.Equal(Ref(S0, Workshop), Status(u, Cfg, S1).Holder);
    }

    // ================================================================== #7: release — the invalid reservations

    /// <summary>#7 RELEASE (L2-02, R1-INVALID): (a) a project the content no longer defines, (b) a depopulated settlement's
    /// part-built university — the review's measured case: ResolveHead reports Viability (it checks viability before labour,
    /// so NoLabourAdults is false), yet the head can never progress — and (c) an institution with no institutions
    /// configuration hold nothing: the younger granary builds in that very step.</summary>
    [Theory]
    [InlineData("a")]
    [InlineData("b")]
    [InlineData("c")]
    public void AnInvalidReservation_IsReleased_TheYoungerHeadBuildsInThatStep(string which)
    {
        WorldState w = Solo();
        SimConfig cfg = Cfg;
        AllBuilders(w, S0);
        AllBuilders(w, S1);
        switch (which)
        {
            case "a":
                Head(w, S0, 0, 999, 0);                                             // no such project
                break;
            case "b":
                Head(w, S0, 0, University, 123_457);
                NoAdults(w, S0);
                HeadResolution r = ConstructionQuery.ResolveHead(w, cfg, S0, w.ConstructionQueue, w.ConstructionProgress, w.GoodStocks, 10.0, null)!;
                Assert.Equal((HeadLimitKind.Viability, false), (r.Limit, r.NoLabourAdults));   // PREMISE: the resolver's order
                Assert.Equal(0.0, InstitutionStaffing.LabourAdults(w, cfg, S0));
                break;
            default:
                cfg = Cfg with { Institutions = null };
                Head(w, S0, 0, University, 123_457);
                Assert.Equal(ViabilityFailure.NoConfig, InstitutionViability.ToFound(w, cfg, S0).Failure);   // PREMISE
                break;
        }
        Head(w, S1, 1, Granary);
        Set(w, S2, Timber, 40);
        Set(w, S2, Stone, 20);

        WorldState t1 = Step(w, cfg);
        Assert.Equal(1L, Built(t1, S1));
        Assert.Equal((0L, 0L), (TotalOf(t1, Timber), TotalOf(t1, Stone)));
        Assert.Empty(Holds(t1));
        HoldsWalkHead head = Walk(w, cfg).HeadOf(S0)!;
        Assert.Equal(which switch { "a" => HoldRelease.UnknownProject, "b" => HoldRelease.NoLabourAdults, _ => HoldRelease.NoInstitutionsConfig },
            head.Release);
        Assert.True(head.Released);
        Assert.Equal(ConstructionQuery.ReleaseOf(w, cfg, w.ConstructionQueue[0]), head.Release);
        AssertConserved(t1);
    }

    // ================================================================== #8–#11: cancel, completion, re-enqueue, revolt

    /// <summary>#8 A CANCEL of a part-built head (kind 11, validated at delivery, applied before the walk) releases its hold in
    /// the delivery step: a later head uses the units in that very step; what the cancelled head had delivered is forfeited
    /// (no refund); no ghost row remains.</summary>
    [Fact]
    public void ACancelledPartBuiltHead_ReleasesItsHoldInTheDeliveryStep_NoRefund_NoGhostRow()
    {
        WorldState w = Solo();
        SimConfig cfg = WithWorks(Cfg, 1.0);                                  // Works: timber 40, stone 20
        Head(w, S3, 0, Works, 333_333);                                       // delivered 14 / 7: remaining 26 / 13
        NoBuilders(w, S3);
        Head(w, S0, 1, Granary);
        AllBuilders(w, S0);
        Set(w, S1, Timber, 40);
        Set(w, S1, Stone, 20);
        long sunkT = Sunk(w, Timber), sunkS = Sunk(w, Stone);

        // No cancel: S3 holds 26 / 13; S0 gets 14 / 7 — p' = floor(14N/40) = floor(7N/20) = 350,000.
        WorldState kept = Step(w, cfg);
        Assert.Equal((333_333L, 350_000L), (P(kept, S3), P(kept, S0)));
        Assert.Equal([Row(PlayerPurse, Timber, 26), Row(PlayerPurse, Stone, 13)], Holds(kept));   // S0's draw leaves 0 to hold

        // The cancel stamped turn 0, delivered in step 0 → 1: S3 leaves before the walk; S0 takes the whole purse.
        WorldState t1 = Step(w, cfg, Cancel(0, Player, S3, 0));
        Assert.Empty(ConstructionQuery.Queue(t1, S3));
        Assert.Equal(1L, Built(t1, S0));
        Assert.Equal((0L, 0L), (TotalOf(t1, Timber), TotalOf(t1, Stone)));    // S3's 14 / 7 never come back
        Assert.Equal((40L, 20L), (Sunk(t1, Timber) - sunkT, Sunk(t1, Stone) - sunkS));
        Assert.Empty(Holds(t1));
        Assert.Equal(0, t1.ConstructionProgress.Count);
        AssertConserved(t1);
    }

    /// <summary>#9 COMPLETION: a head that completes leaves need 0 after its final draw, so the leftover goes to later heads in
    /// the same walk; the slot it promotes is not a head when the walk starts and holds from the NEXT step.</summary>
    [Fact]
    public void ACompletion_FreesTheLeftoverInTheSameWalk_AndThePromotedSlotHoldsFromTheNextStep()
    {
        WorldState w = Solo();
        AllBuilders(w, S1);
        AllBuilders(w, S0);
        Head(w, S1, 0, Granary, 750_000);                 // remaining 10 timber, 5 stone
        Head(w, S1, 0, Granary);                          // slot 1, the same EnqueuedTurn: promoted when slot 0 completes
        Head(w, S0, 1, Works);                            // 41 timber, 23 stone
        Set(w, S2, Timber, 35);
        Set(w, S2, Stone, 17);

        // S1 slot 0 completes (10 / 5); S0 gets 25 / 12: p' = min(floor(25N/41) = 609,756, floor(12N/23) = 521,739) = 521,739,
        // drawing ceil(21.39) = 22 timber and 12 stone; it then holds min(19, 3) = 3 timber. Slot 1 holds nothing this step.
        WorldState t1 = Step(w, Shipped);
        Assert.Equal(1L, Built(t1, S1));
        Assert.Equal(new ConstructionQueueRow(S1, 1, Granary), Assert.Single(ConstructionQuery.Queue(t1, S1)));
        Assert.Equal(521_739L, P(t1, S0));
        Assert.Equal((3L, 0L), (TotalOf(t1, Timber), TotalOf(t1, Stone)));
        Assert.Equal([Row(PlayerPurse, Timber, 3)], Holds(t1));
        Assert.Equal(0, Walk(w, Shipped).HeadOf(S1)!.Entry.Slot);       // slot 1 was not a head when the walk started

        // Next step the promoted slot (EnqueuedTurn 0) is a head ahead of S0: without builders it holds what arrives.
        NoBuilders(t1, S1);
        Set(t1, S2, Timber, 19);
        Set(t1, S2, Stone, 8);
        WorldState pre = Step(t1, ShippedPre);            // PREMISE: batch 3 — S0 takes the 19 / 8 (p' = floor(20N/23) = 869,565)
        Assert.Equal(869_565L, P(pre, S0));
        WorldState t2 = Step(t1, Shipped);
        Assert.Equal((0L, 521_739L), (P(t2, S1), P(t2, S0)));
        Assert.Equal([Row(PlayerPurse, Timber, 19), Row(PlayerPurse, Stone, 8)], Holds(t2));
        AssertConserved(t2);
    }

    /// <summary>#10 RE-ENQUEUE in one batch (a cancel and an enqueue of the same project): the new entry's key is the STAMP
    /// turn t (PREV's turn of the delivering step), it starts at p = 0, and it sits behind every older head. Turn-exact.</summary>
    [Fact]
    public void AReEnqueue_IsANewEntryKeyedAtItsStampTurn_BehindEveryOlderHead_TurnExact()
    {
        WorldState w = Solo();
        Head(w, S1, 0, Granary, 437_500);
        NoBuilders(w, S1);
        Head(w, S0, 2, Granary);
        AllBuilders(w, S0);
        TurnExecutor exec = ConstructionOnly(Cfg, Log(Cancel(5, Player, S1, 0), Enqueue(5, Player, S1)));
        WorldState t5 = exec.Run(w, 5);
        Assert.Equal((5L, 437_500L), (t5.Clock.Turn, P(t5, S1)));
        Set(t5, S2, Timber, 60);
        Set(t5, S2, Stone, 30);

        WorldState t6 = exec.Step(t5);
        Assert.Equal(new ConstructionProgressRow(S1, 0, Granary, 5, 0), ProgressRow(t6, S1));   // stamp turn 5, never 6
        Assert.Equal(1L, Built(t6, S0));                                                        // S0 (turn 2) went first
        Assert.Equal([Row(PlayerPurse, Timber, 20), Row(PlayerPurse, Stone, 10)], Holds(t6));  // the new entry holds the rest

        // The same state with the old entry kept: it (turn 0) holds 22 / 11 first, so S0 gets 38 / 19 — p' = 950,000.
        WorldState kept = Step(t5, Cfg);
        Assert.Equal((437_500L, 950_000L), (P(kept, S1), P(kept, S0)));
    }

    /// <summary>
    /// THE PRIORITY OF A PROMOTED SLOT (T1a round 2, 2026-10-16 — documented, not changed): priority is the ORDER'S AGE,
    /// (EnqueuedTurn, SettlementId, Slot), never building progress, and a promoted slot takes its own order's place in the
    /// building order. A part-built head A at S1 (ordered turn 3, p = 437,500, no builders) sets aside its remaining 22 timber
    /// and 11 stone — the whole purse — in step 0 → 1, behind S0's head (turn 0). S0's head then leaves the queue: it
    /// COMPLETES in step 0 → 1 (on 10 / 5; its next entry is promoted for step 1 → 2), or it is CANCELLED in step 1 → 2 (its
    /// next entry is promoted in that same step: the cancel precedes the walk; slot 0 sat at p = 999,999 with every unit
    /// delivered — ceil(40 × 0.999999) = 40 — so it needed and held nothing). In step 1 → 2 the promoted entry is resolved in
    /// its own order's place:
    /// <list type="bullet">
    /// <item>ordered at turn 1, EARLIER than A: it outranks the part-built head and draws the 22 / 11 A set aside the step
    ///   before — p' = floor(22N/40) = floor(11N/20) = 550,000 — and A, next, finds nothing to set aside;</item>
    /// <item>ordered at turn 5, LATER than A: it stays behind it; A keeps its 22 / 11 and the promoted entry waits,
    ///   HeldForEarlier naming A.</item>
    /// </list>
    /// Turn-exact. PREMISE: batch 3 (PreReservations) builds the promoted entry to 550,000 in both orders (A, without builders,
    /// holds nothing there).
    /// </summary>
    [Theory]
    [InlineData(false, 1L)]
    [InlineData(false, 5L)]
    [InlineData(true, 1L)]
    [InlineData(true, 5L)]
    public void APromotedSlot_TakesItsOwnOrdersPlace_OrderedEarlierItOutranksAPartBuiltHead_OrderedLaterItStaysBehind(
        bool cancelled, long promotedTurn)
    {
        WorldState w = Solo();
        Head(w, S1, 3, Granary, 437_500);                 // A: remaining 22 timber, 11 stone
        NoBuilders(w, S1);
        Head(w, S0, 0, Granary, cancelled ? 999_999 : 750_000);   // slot 0: needs nothing / completes on its remaining 10 / 5
        if (cancelled) NoBuilders(w, S0); else AllBuilders(w, S0);
        Head(w, S0, promotedTurn, Granary);               // slot 1: the entry promoted when slot 0 leaves
        Set(w, S2, Timber, cancelled ? 22 : 32);
        Set(w, S2, Stone, cancelled ? 11 : 16);

        // Step 0 → 1: S0's slot 0 first (turn 0), then A sets aside the 22 / 11 that are left.
        WorldState t1 = Step(w, Cfg);
        Assert.Equal((cancelled ? 0L : 1L, cancelled ? 0 : 1), (Built(t1, S0), ConstructionQuery.Queue(t1, S0)[0].Slot));
        Assert.Equal((437_500L, 22L, 11L), (P(t1, S1), TotalOf(t1, Timber), TotalOf(t1, Stone)));
        Assert.Equal([Row(PlayerPurse, Timber, 22), Row(PlayerPurse, Stone, 11)], Holds(t1));

        AllBuilders(t1, S0);
        OrderRecord[] orders = cancelled ? [Cancel(1, Player, S0, 0)] : [];
        WorldState pre = Step(t1, Pre, orders);           // PREMISE: batch 3 — the promoted entry takes the 22 / 11 either way
        Assert.Equal(550_000L, P(pre, S0));

        WorldState t2 = Step(t1, Cfg, orders);
        Assert.Equal(new ConstructionQueueRow(S0, 1, Granary), Assert.Single(ConstructionQuery.Queue(t2, S0)));
        HeadStatusReport promoted = Status(t1, Cfg, S0, orders), a = Status(t1, Cfg, S1, orders);
        if (promotedTurn < 3)
        {
            Assert.Equal((550_000L, 437_500L, 0L, 0L), (P(t2, S0), P(t2, S1), TotalOf(t2, Timber), TotalOf(t2, Stone)));
            Assert.Empty(Holds(t2));                      // A's set-aside went to the older order; nothing is left to hold
            Assert.Equal((550_000L, 0L, 0L), (promoted.Resolution.ProgressAfter, a.SetAside![Timber], a.SetAside[Stone]));
        }
        else
        {
            Assert.Equal((0L, 437_500L, 22L, 11L), (P(t2, S0), P(t2, S1), TotalOf(t2, Timber), TotalOf(t2, Stone)));
            Assert.Equal([Row(PlayerPurse, Timber, 22), Row(PlayerPurse, Stone, 11)], Holds(t2));
            Assert.Equal((HeadLimitKind.Materials, MaterialCause.HeldForEarlier, Ref(S1, Granary)),
                (promoted.Resolution.Limit, promoted.Cause, promoted.Holder));
            Assert.Equal((22L, 11L), (a.SetAside![Timber], a.SetAside[Stone]));
        }
        AssertConserved(t2);
    }

    /// <summary>#11 REVOLT (R1-TEST-GAPS c): a player's held head carried into an AI polity. The purse key is read on PREV,
    /// so from the step after the change the hold counts against the NEW controller's purse, keeping its order key, and the
    /// old purse's later heads use the units; the AI does not cancel a head merely because it is held back by a hold.</summary>
    [Fact]
    public void ARevolt_MovesTheHoldToTheNewControllersPurseNextStep_AndTheAiDoesNotCancelAHeldHead()
    {
        WorldState w = Duo();                              // S0, S2 the player's; S1, S3 the AI's
        Head(w, S0, 0, Granary, 437_500);                 // remaining 22 / 11
        NoBuilders(w, S0);
        Head(w, S2, 1, Granary);
        AllBuilders(w, S2);
        Head(w, S3, 1, Granary);
        AllBuilders(w, S3);
        Set(w, S2, Timber, 22);
        Set(w, S2, Stone, 11);

        WorldState t1 = Step(w, Cfg);                     // the player's purse: S0 holds 22 / 11, S2 waits
        Assert.Equal((0L, 0L), (P(t1, S2), P(t1, S3)));
        Assert.Equal([Row(PlayerPurse, Timber, 22), Row(PlayerPurse, Stone, 11)], Holds(t1));

        // The revolt: S0 passes to the AI (its control row rewritten, as RevoltSystem leaves it). The AI holds exactly 22 / 11.
        for (int i = 0; i < t1.Controls.Count; i++)
            if (t1.Controls[i].Place == S0) t1.Controls[i] = t1.Controls[i] with { Polity = Rival };
        Assert.True(EmpireQuery.ControlsSettlement(t1, Rival, S0));
        Set(t1, S1, Timber, 22);
        Set(t1, S1, Stone, 11);

        WorldState t2 = Step(t1, Cfg);
        // The player's purse is free: S2 takes 22 / 11 (p' = 550,000). The AI's: S0 (turn 0) holds its 22 / 11 first; S3 waits.
        Assert.Equal((550_000L, 0L, 437_500L), (P(t2, S2), P(t2, S3), P(t2, S0)));
        Assert.Equal([Row(RivalPurse, Timber, 22), Row(RivalPurse, Stone, 11)], Holds(t2));   // S2's 18 / 9 more: nothing left
        HeadStatusReport s3 = Status(t1, Cfg, S3);
        Assert.Equal((MaterialCause.HeldForEarlier, Ref(S0, Granary)), (s3.Cause, s3.Holder));

        // The AI decides on t2: its heads at S0 (no builders, adults present: a valid wait) and S3 (held back by S0's hold)
        // are not structurally stalled, so it cancels neither.
        OrderRecord[] ai = AiConstructionPolicy.Decide(t2, Cfg, Rival, 10.0);
        Assert.DoesNotContain(ai, o => o.Kind == OrderKind.CancelConstruction);
        Assert.Null(ConstructionQuery.StructuralStall(t2, Cfg, S3, 10.0));
        Assert.Null(ConstructionQuery.StructuralStall(t2, Cfg, S0, 10.0));
    }

    // ================================================================== #12–#15: purses

    /// <summary>#12 PURSE OFF (TestConfigs.PreEmpireConstructionStockpile; reservations stay on — R1-PURSE-KEY, L2-03): every
    /// purse is a single settlement's (key −1 − id), so a waiting head holds only its OWN stock and a younger head elsewhere
    /// builds from its own; TheCentralPurse's switch-off arm (EmpireConstructionStockpileTests) is unchanged.</summary>
    [Fact]
    public void PurseOff_HoldsAreKeyedPerSettlement_AWaitingHeadHoldsOnlyItsOwnStock()
    {
        SimConfig off = TestConfigs.PreEmpireConstructionStockpile(Shipped);
        Assert.True(Reservations.Enabled(off));
        WorldState w = Solo();
        Head(w, S1, 0, Granary);
        NoBuilders(w, S1);
        Set(w, S1, Timber, 40);
        Set(w, S1, Stone, 20);
        Head(w, S2, 1, Works);
        AllBuilders(w, S2);
        Set(w, S2, Timber, 30);
        Set(w, S2, Stone, 15);
        Head(w, S0, 2, Granary);
        AllBuilders(w, S0);

        // S2 from its own 30 / 15: p' = min(floor(30N/41) = 731,707, floor(15N/23) = 652,173) = 652,173, drawing
        // ceil(26.74) = 27 timber and 15 stone, then holding min(14, 3) = 3 timber of its own.
        WorldState t1 = Step(w, off);
        Assert.Equal((0L, 652_173L, 0L), (P(t1, S1), P(t1, S2), P(t1, S0)));
        Assert.Equal((40L, 20L, 3L, 0L), (Of(t1, S1, Timber), Of(t1, S1, Stone), Of(t1, S2, Timber), Of(t1, S2, Stone)));
        Assert.Equal([Row(Reservations.SettlementPurse(S2), Timber, 3),
                      Row(Reservations.SettlementPurse(S1), Timber, 40), Row(Reservations.SettlementPurse(S1), Stone, 20)], Holds(t1));
        Assert.Equal((-3, -2), (Reservations.SettlementPurse(S2), Reservations.SettlementPurse(S1)));
        // PREMISE (the purse-off rule): batch 3 draws the same — S1's stock never reached S2 — and publishes nothing.
        WorldState pre = Step(w, TestConfigs.PreReservations(off));
        Assert.Equal((652_173L, 0), (P(pre, S2), pre.ConstructionHolds.Count));
    }

    /// <summary>#13 A CONTROLS-LESS world (R1-TEST-GAPS e, L2-08): construction keys every settlement to its own purse and
    /// publishes those keys; road works pay from EVERY settlement there, so they subtract the payers' own holds and a donor
    /// gives at most its stock less its own set-aside.</summary>
    [Fact]
    public void AControlsLessWorld_PublishesSingleSettlementHolds_AndRoadWorksRespectThemDonorByDonor()
    {
        WorldState w = RoadRig(timber: 0);
        w.Controls.Clear();
        Head(w, S0, 0, Granary, 437_500);                 // remaining 22 / 11
        NoBuilders(w, S0);
        Set(w, S0, Timber, 30);
        Set(w, S0, Stone, 11);
        Set(w, S1, Timber, 40);
        long have = 30 + 40 - 22;                         // the payers' timber less S0's own set-aside

        WorldState pre = BuildAndRoads(ShippedPre, Log(Roads(0))).Step(w);   // PREMISE: batch 3 takes S0's own 30
        Assert.Equal((Road, 0L), (RoadUnits(pre), Of(pre, S0, Timber)));

        WorldState t1 = BuildAndRoads(Shipped, Log(Roads(0))).Step(w);
        Assert.Equal([Row(Reservations.SettlementPurse(S0), Timber, 22), Row(Reservations.SettlementPurse(S0), Stone, 11)], Holds(t1));
        Assert.Equal(RoadCharge(have, Road), RoadUnits(t1));                 // 48 of 60
        Assert.Equal(48L, RoadUnits(t1));
        Assert.Equal((22L, 0L), (Of(t1, S0, Timber), Of(t1, S1, Timber)));  // S0 gave 30 − 22 = 8, S1 its 40
        AssertConserved(t1);

        AllBuilders(t1, S0);                                                 // builders back: S0 completes from its own
        WorldState t2 = ConstructionOnly(Shipped, null).Step(t1);
        Assert.Equal(1L, Built(t2, S0));
    }

    /// <summary>#14 COMPETING heads in three settlements of one Empire plus a fourth Empire: the purses are independent — the
    /// player's waiting head binds only the player's purse, and the rival's head builds from its own.</summary>
    [Fact]
    public void ThreeSettlementsOfOneEmpireAndAFourthEmpire_ThePursesAreIndependent()
    {
        WorldState w = Duo();
        for (int i = 0; i < w.Controls.Count; i++)
            if (w.Controls[i].Place == S1) w.Controls[i] = w.Controls[i] with { Polity = Player };
        Assert.Equal((3, 1), (LabourActivities.ControlledSettlements(w, Player).Length, LabourActivities.ControlledSettlements(w, Rival).Length));
        Head(w, S1, 0, Granary, 437_500);                 // the player's, oldest, no builders: holds 22 / 11
        NoBuilders(w, S1);
        Head(w, S3, 0, Granary);                          // the rival's, as old
        AllBuilders(w, S3);
        Head(w, S0, 1, Works);
        AllBuilders(w, S0);
        Head(w, S2, 1, Granary);
        AllBuilders(w, S2);
        Set(w, S2, Timber, 50);
        Set(w, S0, Stone, 30);
        Set(w, S3, Timber, 40);
        Set(w, S3, Stone, 20);

        WorldState pre = Step(w, ShippedPre);             // PREMISE: batch 3 — S0 completes, S2 gets 9 / 7: 225,000
        Assert.Equal((1L, 225_000L), (Built(pre, S0, Works), P(pre, S2)));

        // The player: S1 holds 22 / 11 of 50 / 30; S0 gets 28 / 19: p' = min(floor(28N/41) = 682,926, floor(19N/23)) =
        // 682,926, drawing 28 timber and ceil(15.71) = 16 stone, then holding 3 stone; S2 gets nothing. The rival: S3 builds.
        WorldState t1 = Step(w, Shipped);
        Assert.Equal((437_500L, 682_926L, 0L, 1L), (P(t1, S1), P(t1, S0), P(t1, S2), Built(t1, S3)));
        Assert.Equal([Row(PlayerPurse, Timber, 22), Row(PlayerPurse, Stone, 14)], Holds(t1));
        Assert.Equal((0L, 0L), (Of(t1, S3, Timber), Of(t1, S3, Stone)));
        AssertConserved(t1);
    }

    /// <summary>#15 A CROSS-GOOD CHAIN A → B → C (L2-09b): A waits holding stone; B is blocked by A's stone hold and so holds
    /// its timber; C, which needs timber only, is held back by B — each label names its own holder.</summary>
    [Fact]
    public void ACrossGoodChain_EachHeadIsHeldBackByTheOneBeforeIt_AndNamesIt()
    {
        WorldState w = Solo();
        AllBuilders(w, S2);
        AllBuilders(w, S0);
        SimConfig cfg = With(WithWorks(Cfg, 4.0 * C(w, S2, 10.0)));         // B: timber 40, stone 20 at a quarter pace
        Head(w, S1, 0, Cairn);                            // A: stone 20, no builders
        NoBuilders(w, S1);
        Head(w, S2, 1, Works);                            // B
        Head(w, S0, 2, Palisade);                         // C: timber 31
        Set(w, S3, Stone, 20);
        Set(w, S3, Timber, 70);

        WorldState pre = Step(w, TestConfigs.PreReservations(cfg));   // PREMISE: batch 3 — B draws 10 / 5, C completes on 60
        Assert.Equal((250_000L, 1L), (P(pre, S2), Built(pre, S0, Palisade)));

        WorldState t1 = Step(w, cfg);
        Assert.Equal((0L, 0L, 967_741L), (P(t1, S1), P(t1, S2), P(t1, S0)));   // C: floor(30N/31)
        Assert.Equal([Row(PlayerPurse, Timber, 40), Row(PlayerPurse, Stone, 20)], Holds(t1));
        HoldsWalkResult walk = Walk(w, cfg);
        HoldsWalkHead b = walk.HeadOf(S2)!, c = walk.HeadOf(S0)!;
        Assert.Equal((MaterialCause.HeldForEarlier, Ref(S1, Cairn), Stone, 20L), (b.Cause, b.Holder, b.Resolution.LimitGood, b.SetAsideAhead));
        Assert.Equal((MaterialCause.HeldForEarlier, Ref(S2, Works), Timber, 40L), (c.Cause, c.Holder, c.Resolution.LimitGood, c.SetAsideAhead));
        Assert.Equal((0L, 40L, 20L), (b.HoldOf(Stone), b.HoldOf(Timber), walk.HeadOf(S1)!.HoldOf(Stone)));
    }

    // ================================================================== #16: the building/roads split

    /// <summary>The ResourceAllocationTests rig: ONE candidate route S0–S1 whose dirt-path row is 60 km (the trackway costs
    /// exactly 60 timber), every settlement's labour on building, <paramref name="timber"/> at S2 and 1,000 stone at S3.</summary>
    private static WorldState RoadRig(long timber, long stone = 1000)
    {
        WorldState w = Solo();
        w.TransportEdges.Clear();
        w.TransportEdges.Add(new TransportEdgeRow(1, S0, S1, EdgeTypes.DirtPath, TransportModes.Road, TransportEdgeStates.Complete,
            100_000, Road, 0, 0, 0, EdgeTypes.DirtPath, 0.0, 1.0));
        w.SettlementDistances.Clear();
        w.SettlementDistances.Add(new SettlementDistanceRow(S0, S1, 100.0));
        w.SettlementDistances.Add(new SettlementDistanceRow(S1, S0, 100.0));
        w.TradeFlows.Clear();
        w.ResearchCompleted.Add(new ResearchCompletedRow(Player, Research.Nodes[Research.IndexOfId("track_road")].Key));
        for (int s = 0; s < w.Settlements.Count; s++) AllBuilders(w, w.Settlements[s].Id);
        Set(w, S2, Timber, timber);
        Set(w, S3, Stone, stone);
        return w;
    }

    private const long Road = 60;

    private static OrderRecord Split(long turn, int buildings) =>
        new(turn, Player.Value, OrderKind.SetResourceAllocation, Player.Value, buildings);

    private static OrderRecord Roads(long turn) => OrderRecord.From(turn, Player, OrderKind.DevelopRoads, 0, 100.0);

    private static TurnExecutor BuildAndRoads(SimConfig cfg, OrderLog? log) =>
        new(ResearchRigs.FlatEra(10.0), [SystemCatalog.Construction(cfg), SystemCatalog.RoadDevelopment(cfg)], log);

    /// <summary>ADR-032's single-good charge, restated independently (the ResourceAllocationTests oracle).</summary>
    private static long RoadCharge(long have, long units) => have >= units ? units : (long)Math.Floor(units * ((double)have / units));

    private static long RoadUnits(IReadOnlyWorldState w)
    {
        long u = 0;
        for (int i = 0; i < w.RoadDevelopments.Count; i++) u += w.RoadDevelopments[i].MaterialUnits;
        return u;
    }

    /// <summary>The split rig with a WAITING older head (S0's granary at 437,500, no builders: it holds 22 timber, 11 stone)
    /// and a younger Works at S1 (41 / 23), 100 timber and the 60-timber road order.</summary>
    private static WorldState SplitRig(long timber = 100)
    {
        WorldState w = RoadRig(timber);
        Head(w, S0, 0, Granary, 437_500);
        NoBuilders(w, S0);
        Head(w, S1, 1, Works);
        return w;
    }

    /// <summary>#16 THE SPLIT (§1.3): at b = 100 road works get only what building work neither draws nor sets aside; at
    /// b = 50 and b = 0 the holds spend the grant G_b and road works find at least min(D_r, E_r); every figure by hand.</summary>
    [Theory]
    // b, S1's p' (or N: built), road units, building's draws, building's holds (timber)
    // b = 100: S0 holds 22, S1 completes on 41; road 100 − 41 − 22 = 37.
    [InlineData(100, N, 37L, 41L, 22L)]
    // b = 50: D_b = 22 + 41 = 63; E_b = E_r = 50 < D_r = 60 → G_b = 50; S0 holds 22, S1 gets 28 → floor(28N/41) = 682,926.
    [InlineData(50, 682_926L, 50L, 28L, 22L)]
    // b = 0: cap = 0 + (100 − 60) = 40 → G_b = 40; S0 holds 22, S1 gets 18 → floor(18N/41) = 439,024; the road its 60.
    [InlineData(0, 439_024L, 60L, 18L, 22L)]
    public void TheSplit_HoldsSpendTheGrant_RoadWorksGetWhatIsNeitherDrawnNorSetAside(int b, long p1, long road, long drawn, long held)
    {
        WorldState w = SplitRig();
        OrderRecord[] orders = b == ResourceAllocation.DefaultBuildingsPercent ? [Roads(0)] : [Split(0, b), Roads(0)];
        WorldState next = BuildAndRoads(Shipped, Log(orders)).Step(w);
        Assert.Equal(p1 == N ? 0L : p1, P(next, S1));
        Assert.Equal(p1 == N ? 1L : 0L, Built(next, S1, Works));
        Assert.Equal(road, RoadUnits(next));
        Assert.Equal(RoadCharge(100 - drawn - held, Road), RoadUnits(next));   // ADR-032's charge on what is left to it
        Assert.Equal(100 - drawn - road, TotalOf(next, Timber));
        Assert.Equal(held, TotalOf(next, Timber));                         // exactly the set-aside is left
        Assert.Equal(437_500L, P(next, S0));
        Assert.Equal(held, Holds(next)[0].Units);
        AssertConserved(next);
        if (b != ResourceAllocation.DefaultBuildingsPercent)
        {
            AllocationSplit x = ConstructionQuery.StepAllocations(w, Shipped, w.ConstructionQueue, w.ConstructionProgress, w.GoodStocks,
                ConstructionQuery.AllocationsWith(w, Shipped, w.ResourceAllocations, orders), orders, 10.0)![0]!.SplitOf(Timber)!.Value;
            Assert.Equal((100L, 63L, Road), (x.Supply, x.BuildingDemand, x.RoadDemand));          // D_b = claims; D_r on S
            Assert.Equal(drawn + held, x.BuildingGrant);                                          // the holds spend the grant
            Assert.True(100 - drawn - held >= Math.Min(x.RoadDemand, x.RoadShare), "road works find min(D_r, E_r)");
        }

        // PREMISE (the batch-3 rule): road works took the waiting head's need. b = 100 and b = 50: D_b = 41 (no hold) fits
        // E_b, S1 completes and the road is charged on the 59 left; b = 0: G_b = 40 and the road takes its 60. Either way the
        // timber S0 still needs is gone.
        WorldState pre = BuildAndRoads(ShippedPre, Log(orders)).Step(w);
        long preRoad = b == 0 ? Road : RoadCharge(59, Road);
        Assert.Equal(preRoad, RoadUnits(pre));
        Assert.Equal(b == 0 ? 0L : 59 - preRoad, TotalOf(pre, Timber));
        Assert.True(TotalOf(pre, Timber) < 22, "batch 3 leaves the waiting head less than its 22");
    }

    /// <summary>#16 (cont.) A head's need beyond the grant is NOT held: at b = 0 with 30 timber, G_b = 0, so the waiting head
    /// holds no timber and road works take all 30 — as the player's split says; its stone (inside the grant) stays held.</summary>
    [Fact]
    public void TheSplit_ANeedBeyondTheGrantIsNotHeld_RoadWorksMayUseIt()
    {
        WorldState w = SplitRig(timber: 30);
        OrderRecord[] orders = [Split(0, 0), Roads(0)];
        WorldState next = BuildAndRoads(Shipped, Log(orders)).Step(w);
        // D_b timber = 22 + 8 = 30, D_r = 60, E_r = 30 → cap 0 → G_b = 0. Stone: D_b = 11 + 5 + 18 = 34 = G_b.
        Assert.Equal((0L, 30L, 0L), (P(next, S1), RoadUnits(next), TotalOf(next, Timber)));
        Assert.Equal([Row(PlayerPurse, Stone, 34)], Holds(next));
        HoldsWalkResult walk = ConstructionQuery.HoldsWalk(w, Shipped, w.ConstructionQueue, w.ConstructionProgress, w.GoodStocks, 10.0,
            ConstructionQuery.GrantsLeft(ConstructionQuery.StepAllocations(w, Shipped, w.ConstructionQueue, w.ConstructionProgress,
                w.GoodStocks, ConstructionQuery.AllocationsWith(w, Shipped, w.ResourceAllocations, orders), orders, 10.0)));
        Assert.Equal((0L, 11L), (walk.HeadOf(S0)!.HoldOf(Timber), walk.HeadOf(S0)!.HoldOf(Stone)));
    }

    /// <summary>#16 (cont.) THE STARVATION GUARD WITH HOLDS: the forecast road charge runs on S − drawn − held. Trackway 1
    /// timber + 0.025 tools a km (60 timber, 2 tools); two younger granaries and the waiting head; b = 75. The forecast
    /// leaves road works 25 timber and 2 tools, which ADR-032's min-ratio floors to nothing (2 × 25/60 &lt; 1 tool), so the
    /// guard withholds nothing: building work takes its whole claim (78 drawn + 22 held) and the road nothing.</summary>
    [Fact]
    public void TheStarvationGuard_ReadsTheForecastOnWhatIsNeitherDrawnNorSetAside()
    {
        RoadClassConfig[] classes = [.. Shipped.Roads!.Classes];
        for (int i = 0; i < classes.Length; i++)
            if (classes[i].EdgeType == EdgeTypes.Trackway)
                classes[i] = classes[i] with { MaterialsPerKm = [new RoadMaterialConfig("timber", 1.0), new RoadMaterialConfig("tools", 0.025)] };
        SimConfig cfg = Shipped with { Roads = Shipped.Roads with { Classes = classes } };
        WorldState w = RoadRig(100);
        Set(w, S3, Tools, 2);
        Head(w, S0, 0, Granary, 437_500);
        NoBuilders(w, S0);
        Head(w, S1, 1, Granary);
        Head(w, S2, 1, Granary);
        OrderRecord[] orders = [Split(0, 75), Roads(0)];

        AllocationView view = ResourceAllocationQuery.ForEmpire(w, cfg, Player, orders, 10.0)!;
        Assert.True(view.Split.RoadPaysNothingFromItsShare);
        Assert.Equal((78L, 22L, 0L), (view.Split.BuildingDrawOf(Timber), view.Split.BuildingHoldOf(Timber), view.Split.RoadChargeOf(Timber)));
        WorldState next = BuildAndRoads(cfg, Log(orders)).Step(w);
        Assert.Equal((1L, 950_000L, 0L, 22L), (Built(next, S1), P(next, S2), RoadUnits(next), TotalOf(next, Timber)));
        Assert.Equal(2L, TotalOf(next, Tools));
        AssertConserved(next);
    }

    /// <summary>#16 (cont.) An EXPLICIT b = 100 row with a waiting head: the default and the row draw and set aside alike —
    /// the two worlds differ only by the row.</summary>
    [Fact]
    public void AnExplicitHundred_WithAWaitingHead_DiffersFromTheDefaultOnlyByItsRow()
    {
        WorldState def = BuildAndRoads(Shipped, Log(Roads(0))).Step(SplitRig());
        WorldState hundred = BuildAndRoads(Shipped, Log(Split(0, 100), Roads(0))).Step(SplitRig());
        Assert.Equal([Row(PlayerPurse, Timber, 22), Row(PlayerPurse, Stone, 11)], Holds(def));
        Assert.Equal(new ResourceAllocationRow(Player, 100), Assert.Single(hundred.ResourceAllocations.Rows()));
        hundred.ResourceAllocations.Clear();
        Assert.Equal(WorldHash.ComputeHex(def), WorldHash.ComputeHex(hundred));
    }

    /// <summary>#16 (cont.) THE REVIEW'S COUNTEREXAMPLE (R1-HOLD-FORMULA, L2-01), pinned: one head, a purse of 10 stone, a
    /// project of 30 stone at a third of its labour a step. It draws Delivered(30, 333,333) = 10 — every unit — and needs
    /// 20 more; the draw is subtracted BEFORE the hold, so it holds min(20, 10 − 10) = 0. (Capping the hold before the draw
    /// would set aside 10 stone that do not exist.)</summary>
    [Fact]
    public void TheReviewCounterexample_Purse10Cost30LabourOneThird_HoldsNothingItCannotSee()
    {
        WorldState w = Solo();
        AllBuilders(w, S0);
        const int Quarry = 94;
        SimConfig cfg = Cfg with
        {
            Goods = Cfg.Goods! with
            {
                Projects = [.. Cfg.Goods.Projects!, new ConstructionProjectEntry(Quarry, "quarry", [new ProjectInput("stone", 30)], 3.0 * C(w, S0, 10.0))],
            },
        };
        Head(w, S0, 0, Quarry);
        Set(w, S2, Stone, 10);

        HoldsWalkResult walk = Walk(w, cfg);
        HoldsWalkHead head = walk.HeadOf(S0)!;
        Assert.Equal((333_333L, 10L, 0L), (head.Resolution.ProgressAfter, head.DrawOf(Stone), head.HoldOf(Stone)));
        Assert.Equal((10L, 10L, 0L), (walk.Start[0][Stone], walk.Drawn[0][Stone], walk.Held[0][Stone]));
        WorldState t1 = Step(w, cfg);
        Assert.Equal((333_333L, 0L), (P(t1, S0), TotalOf(t1, Stone)));
        Assert.Empty(Holds(t1));
    }

    /// <summary>The walk under <paramref name="orders"/> exactly as the step sees them (the split rows upserted, the road
    /// order delivered): the grants of StepAllocations, then HoldsWalk.</summary>
    private static HoldsWalkResult WalkUnder(IReadOnlyWorldState w, SimConfig cfg, OrderRecord[] orders) =>
        ConstructionQuery.HoldsWalk(w, cfg, w.ConstructionQueue, w.ConstructionProgress, w.GoodStocks, 10.0,
            ConstructionQuery.GrantsLeft(ConstructionQuery.StepAllocations(w, cfg, w.ConstructionQueue, w.ConstructionProgress,
                w.GoodStocks, ConstructionQuery.AllocationsWith(w, cfg, w.ResourceAllocations, orders), orders, 10.0)));

    /// <summary>A head's label as a tuple: (limit, cause, binding good, holder, units of it set aside ahead, p').</summary>
    private static (HeadLimitKind, MaterialCause, int, QueueEntryRef?, long, long) Label(HoldsWalkHead h) =>
        (h.Resolution.Limit, h.Cause, h.Resolution.LimitGood, h.Holder, h.SetAsideAhead, h.Resolution.ProgressAfter);

    private static (HeadLimitKind, MaterialCause, int, QueueEntryRef?, long, long) Label(HeadStatusReport s) =>
        (s.Resolution.Limit, s.Cause, s.Resolution.LimitGood, s.Holder, s.SetAsideAhead, s.Resolution.ProgressAfter);

    /// <summary>
    /// #16 (cont.) THE LABELS UNDER THE SPLIT (§1.3 [L2-07]: "each label is pinned at b = 100, 50 and 0"; fix round,
    /// findings T1a-L1-T1a-V1-F2 and T1a-L3-T1a-L3-F1). SplitRig: S0's granary waits (no builders) and sets aside 22 timber;
    /// the younger Works at S1 gets what is left. Wherever S1 would get further IGNORING S0's set-aside it is HeldForEarlier
    /// naming S0's granary, with 22 set aside ahead — under a split too, where the grant also binds: the precedence puts
    /// HeldForEarlier before HeldForRoads. Each row is a LITERAL (the walk and HeadStatus are one function, so neither is
    /// compared with the other), and the step's own P(S1) agrees.
    /// <list type="bullet">
    /// <item>b = 100 (an explicit row), 50 timber: S1 gets 50 − 22 = 28 → floor(28N/41) = 682,926; ignoring the hold it would
    ///   get all 41 and complete.</item>
    /// <item>b = 50, 100 timber: G_b = 50 (D_b = 63; E_b = E_r = 50 &lt; D_r = 60); S1 gets 50 − 22 = 28 → 682,926; ignoring
    ///   the hold, A0 = min(100, 50) = 50 ≥ 41: complete.</item>
    /// <item>b = 0, 100 timber: G_b = 0 + (100 − 60) = 40; S1 gets 40 − 22 = 18 → floor(18N/41) = 439,024; ignoring the hold,
    ///   A0 = 40 → floor(40N/41) = 975,609 — the grant binds here too (40 &lt; 100 physically), and the label is still
    ///   HeldForEarlier.</item>
    /// </list>
    /// </summary>
    [Theory]
    [InlineData(100, 50L, 682_926L, -1L)]
    [InlineData(50, 100L, 682_926L, 50L)]
    [InlineData(0, 100L, 439_024L, 40L)]
    public void TheSplit_TheYoungerHeadBlockedByAnEarlierSetAside_IsHeldForEarlier_AtEveryShare(int b, long timber, long p1, long grant)
    {
        WorldState w = SplitRig(timber);
        OrderRecord[] orders = [Split(0, b), Roads(0)];
        HoldsWalkResult walk = WalkUnder(w, Shipped, orders);
        Assert.Equal(22L, walk.HeadOf(S0)!.HoldOf(Timber));                          // PREMISE: the earlier set-aside
        if (grant >= 0)
        {
            AllocationSplit x = ConstructionQuery.StepAllocations(w, Shipped, w.ConstructionQueue, w.ConstructionProgress, w.GoodStocks,
                ConstructionQuery.AllocationsWith(w, Shipped, w.ResourceAllocations, orders), orders, 10.0)![0]!.SplitOf(Timber)!.Value;
            Assert.Equal(grant, x.BuildingGrant);                                    // PREMISE: the grant binds below the 100
        }
        var expected = (HeadLimitKind.Materials, MaterialCause.HeldForEarlier, Timber, (QueueEntryRef?)Ref(S0, Granary), 22L, p1);
        Assert.Equal(expected, Label(walk.HeadOf(S1)!));
        Assert.Equal(expected, Label(Status(w, Shipped, S1, orders)));
        Assert.Equal(p1, P(BuildAndRoads(Shipped, Log(orders)).Step(w), S1));
    }

    /// <summary>
    /// #16 (cont.) The HeldForRoads twin (fix round, T1a-L1-T1a-V1-F2 / T1a-L3-T1a-L3-F1): the same rig with builders at S0,
    /// whose granary completes on its 22 / 11 and sets nothing aside, so no EARLIER set-aside blocks S1 — at b = 50 and 0 the
    /// grant alone binds (S1 gets G_b − 22: 28 → 682,926 and 18 → 439,024): HeldForRoads with no holder; at b = 100 S1
    /// completes and carries no label.
    /// </summary>
    [Theory]
    [InlineData(100, 0L, HeadLimitKind.None, MaterialCause.None)]
    [InlineData(50, 682_926L, HeadLimitKind.Materials, MaterialCause.HeldForRoads)]
    [InlineData(0, 439_024L, HeadLimitKind.Materials, MaterialCause.HeldForRoads)]
    public void TheSplit_WithNoEarlierSetAside_TheGrantAloneBinding_IsHeldForRoads(int b, long p1, HeadLimitKind limit, MaterialCause cause)
    {
        WorldState w = SplitRig();
        AllBuilders(w, S0);
        OrderRecord[] orders = [Split(0, b), Roads(0)];
        HoldsWalkResult walk = WalkUnder(w, Shipped, orders);
        HoldsWalkHead older = walk.HeadOf(S0)!;
        Assert.True(older.Resolution.Completes);                                     // PREMISE: no earlier set-aside
        Assert.Equal((0L, 0L), (older.HoldOf(Timber), older.HoldOf(Stone)));
        HoldsWalkHead s1 = walk.HeadOf(S1)!;
        HeadStatusReport status = Status(w, Shipped, S1, orders);
        long after = p1 == 0 ? N : p1;
        int good = limit == HeadLimitKind.None ? -1 : Timber;
        var expected = (limit, cause, good, (QueueEntryRef?)null, 0L, after);
        Assert.Equal(expected, Label(s1));
        Assert.Equal(expected, Label(status));
        WorldState next = BuildAndRoads(Shipped, Log(orders)).Step(w);
        Assert.Equal((p1, p1 == 0 ? 1L : 0L), (P(next, S1), Built(next, S1, Works)));
    }

    /// <summary>
    /// FIX ROUND, finding T1a-L3-T1a-L3-F2: "the earliest holder of the binding good" (§1.3) and SetAsideAhead ("units of
    /// the binding good set aside by EARLIER heads of its purse", HeadStatusReport) with TWO earlier holders. The building
    /// order: a Cairn at S3 (turn 0; stone only, no builders) sets aside 20 stone; S0's and S1's granaries (turns 1, 2; no
    /// builders) set aside 40 timber and 20 stone each; S2's granary (turn 3, builders) gets the 20 timber left of 100:
    /// p' = 500,000, timber-bound. Its holder is S0 — the earliest holder of TIMBER (not the Cairn, which is earlier but
    /// holds no timber; not S1, the latest) — and 80 timber is set aside ahead of it (S0's 40 + S1's 40; the Cairn's 0).
    /// </summary>
    [Fact]
    public void TwoEarlierHoldersOfTheBindingGood_TheEarliestIsNamed_AndBothSetAsidesAreCountedAhead()
    {
        WorldState w = Solo();
        Head(w, S3, 0, Cairn);
        NoBuilders(w, S3);
        Head(w, S0, 1, Granary);
        NoBuilders(w, S0);
        Head(w, S1, 2, Granary);
        NoBuilders(w, S1);
        Head(w, S2, 3, Granary);
        AllBuilders(w, S2);
        Set(w, S3, Timber, 100);
        Set(w, S3, Stone, 80);

        HoldsWalkResult walk = Walk(w, Shipped);
        HoldsWalkHead cairn = walk.HeadOf(S3)!, a = walk.HeadOf(S0)!, b = walk.HeadOf(S1)!;
        Assert.Equal((0L, 20L), (cairn.HoldOf(Timber), cairn.HoldOf(Stone)));      // PREMISES: who sets what aside
        Assert.Equal((40L, 20L, 40L, 20L), (a.HoldOf(Timber), a.HoldOf(Stone), b.HoldOf(Timber), b.HoldOf(Stone)));
        var expected = (HeadLimitKind.Materials, MaterialCause.HeldForEarlier, Timber, (QueueEntryRef?)Ref(S0, Granary), 80L, 500_000L);
        Assert.Equal(expected, Label(walk.HeadOf(S2)!));
        Assert.Equal(expected, Label(Status(w, Shipped, S2)));
        Assert.Equal(500_000L, P(Step(w, Shipped), S2));
    }

    /// <summary>
    /// FIX ROUND, finding T1a-L3-T1a-L3-F3: the named holder is of the waiting head's OWN purse (§1.1: holds are per purse
    /// key; §1.6 P7: hold notes read the viewer's own purse only) — never a FOREIGN Empire's head, even one EARLIER in the
    /// building order that sets the same good aside. The rival's S1 granary (turn 0, no builders) sets aside the rival's 40
    /// timber and 20 stone; the player's S0 granary (turn 1, no builders) sets aside 40 / 20 of the player's 50 / 25; the
    /// player's Works at S2 (turn 2, builders) gets 10 / 5: p' = min(floor(10N/41), floor(5N/23)) = 217,391, stone-bound.
    /// It names S0's granary, and only its own purse's 20 stone is set aside ahead of it (not the rival's 20 too).
    /// </summary>
    [Fact]
    public void TheNamedHolder_IsOfTheWaitingHeadsOwnPurse_NeverAnEarlierForeignHead()
    {
        WorldState w = Duo();
        Head(w, S1, 0, Granary);
        NoBuilders(w, S1);
        Head(w, S0, 1, Granary);
        NoBuilders(w, S0);
        Head(w, S2, 2, Works);
        AllBuilders(w, S2);
        Set(w, S3, Timber, 40);
        Set(w, S3, Stone, 20);
        Set(w, S2, Timber, 50);
        Set(w, S2, Stone, 25);

        HoldsWalkResult walk = Walk(w, Shipped);
        Assert.Equal([S1, S0, S2], new[] { walk.Heads[0].Entry.Settlement, walk.Heads[1].Entry.Settlement, walk.Heads[2].Entry.Settlement });
        Assert.Equal((RivalPurse, PlayerPurse, PlayerPurse), (walk.Heads[0].Purse, walk.Heads[1].Purse, walk.Heads[2].Purse));
        Assert.Equal((20L, 20L), (walk.HeadOf(S1)!.HoldOf(Stone), walk.HeadOf(S0)!.HoldOf(Stone)));   // PREMISE: both hold stone
        var expected = (HeadLimitKind.Materials, MaterialCause.HeldForEarlier, Stone, (QueueEntryRef?)Ref(S0, Granary), 20L, 217_391L);
        Assert.Equal(expected, Label(walk.HeadOf(S2)!));
        Assert.Equal(expected, Label(Status(w, Shipped, S2)));
    }

    /// <summary>
    /// FIX ROUND, finding T1a-L3-T1a-L3-F4: §1.3 — "Unobtainable only when the physical holding left, ignoring holds and the
    /// grant, is short of the next unit and the recipe is unreachable". An older workshop (turn 3, no builders) sets aside
    /// the purse's only 10 tools, which sit in the YOUNGER workshop's own settlement (S1); the younger (turn 7, builders)
    /// cannot move. Its input exists, so it is Materials, HeldForEarlier naming the older workshop — never Unobtainable —
    /// at b = 100, 50 and 0 (the road order's trackway costs timber only, so the split never reaches the tools). The
    /// control twin with NO tools anywhere stays Unobtainable (the relabel must not always fire).
    /// </summary>
    [Theory]
    [InlineData(100, 10L)]
    [InlineData(50, 10L)]
    [InlineData(0, 10L)]
    [InlineData(100, 0L)]
    [InlineData(0, 0L)]
    public void AHeadBlockedOnlyBySetAsideTools_IsMaterialsHeldForEarlier_NotUnobtainable(int b, long tools)
    {
        WorldState w = RoadRig(timber: 300, stone: 200);
        Head(w, S0, 3, Workshop);
        NoBuilders(w, S0);
        Head(w, S1, 7, Workshop);
        Set(w, S1, Tools, tools);
        OrderRecord[] orders = [Split(0, b), Roads(0)];
        HoldsWalkResult walk = WalkUnder(w, Shipped, orders);
        Assert.Equal((tools, tools), (TotalOf(w, Tools), walk.HeadOf(S0)!.HoldOf(Tools)));   // PREMISE: the older holds them all
        HeadStatusReport status = Status(w, Shipped, S1, orders);
        if (tools > 0)
        {
            Assert.Null(ConstructionQuery.StructuralStall(w, Shipped, S1, 10.0));             // PREMISE: not structural
            var expected = (HeadLimitKind.Materials, MaterialCause.HeldForEarlier, Tools, (QueueEntryRef?)Ref(S0, Workshop), 10L, 0L);
            Assert.Equal(expected, Label(walk.HeadOf(S1)!));
            Assert.Equal(expected, Label(status));
            Assert.False(status.Resolution.StructurallyStalled);
        }
        else
        {
            var expected = (HeadLimitKind.Unobtainable, MaterialCause.EmpireHasNone, Tools, (QueueEntryRef?)null, 0L, 0L);
            Assert.Equal(expected, Label(walk.HeadOf(S1)!));
            Assert.Equal(expected, Label(status));
        }
    }

    /// <summary>
    /// FIX ROUND, finding T1a-L1-T1a-V1-F3 (nit): the converse of the relabel. A younger workshop that could not move even
    /// with ALL the purse physically holds — no tools anywhere and no recipe here makes them — is Unobtainable for want of
    /// tools, as batch 3 and the AI's StructuralStall say, also when an earlier palisade's set-aside of every unit of timber
    /// makes timber the binding good against the budget (before the fix it read "Materials / Short of timber"). The rule is
    /// §1.3's both ways: Unobtainable exactly when the physical holding left cannot move the head and the recipe is
    /// unreachable. At b = 100, 50 and 0 (31 timber, 40 stone; the 60-timber road order, D_r = 31): the palisade sets aside
    /// all 31 timber at b = 100, the whole grant G_b = floor(31 × 50 / 100) = 15 at b = 50, and nothing at b = 0, where
    /// G_b = 0 + max(0, 31 − 31) = 0 and the grant alone binds (before the fix: "held for road works"). Batch 3
    /// (PreReservations, whose labels are its own) agrees at b = 100; under a split it read the grant-bound timber instead
    /// (Materials, timber) — with the hold rule on, §1.3 decides from what is physically there, as the AI's StructuralStall
    /// does.
    /// </summary>
    [Theory]
    [InlineData(100, 31L)]
    [InlineData(50, 15L)]
    [InlineData(0, 0L)]
    public void AStructurallyUnobtainableHead_KeepsItsUnobtainableLabel_WhenAnEarlierSetAsideOrTheGrantAlsoBinds(int b, long held)
    {
        WorldState w = RoadRig(timber: 31, stone: 40);
        Head(w, S0, 0, Palisade);
        NoBuilders(w, S0);
        Head(w, S1, 1, Workshop);
        OrderRecord[] orders = [Split(0, b), Roads(0)];
        Assert.Equal(0L, TotalOf(w, Tools));
        Assert.Equal(HeadLimitKind.Unobtainable, ConstructionQuery.StructuralStall(w, Shipped, S1, 10.0)!.Limit);   // PREMISE
        HeadStatusReport pre = Status(w, ShippedPre, S1, orders);
        Assert.Equal(b == 100 ? (HeadLimitKind.Unobtainable, Tools) : (HeadLimitKind.Materials, Timber),
            (pre.Resolution.Limit, pre.Resolution.LimitGood));                                                         // batch 3
        HoldsWalkResult walk = WalkUnder(w, Shipped, orders);
        Assert.Equal(held, walk.HeadOf(S0)!.HoldOf(Timber));                         // PREMISE: what binds the timber
        var expected = (HeadLimitKind.Unobtainable, MaterialCause.EmpireHasNone, Tools, (QueueEntryRef?)null, 0L, 0L);
        Assert.Equal(expected, Label(walk.HeadOf(S1)!));
        HeadStatusReport status = Status(w, Shipped, S1, orders);
        Assert.Equal(expected, Label(status));
        Assert.True(status.Resolution.StructurallyStalled);
    }

    /// <summary>
    /// FIX ROUND, finding T1a-L1-T1a-V1-F4 (nit) — the §1.2 release-table row "the Empire becoming extinct: its settlements'
    /// heads key to their new controller, or to their own purse when uncontrolled (Holders on PREV)". The rival's S1 granary
    /// waits at 437,500 (no builders; remaining 22 / 11), S3's granary has builders; S1 holds 10 timber, S3 40 / 20.
    /// (a) The rival's ROSTER row is gone but its control rows remain: the key is still the rival's id (control, not the
    /// roster, decides): S1 sets aside 22 / 11 of the purse's 50 / 20, S3 gets 28 / 9 → min(700,000, 450,000) = 450,000,
    /// drawing 18 / 9, then sets aside min(22, 50 − 22 − 18) = 10 timber: rows (2, timber, 32), (2, stone, 11).
    /// (b) Its control rows are gone too: S1 and S3 are stateless, each its own purse — S3 completes from its own 40 / 20 and
    /// S1 sets aside only its own 10 timber (purse −2). PREMISE: batch 3 builds S3 in both (no set-aside).
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnExtinctEmpire_ItsHeadsKeyByTheControlRowsOnPrev(bool controlRowsGone)
    {
        WorldState w = Duo();
        Head(w, S1, 0, Granary, 437_500);
        NoBuilders(w, S1);
        Head(w, S3, 1, Granary);
        AllBuilders(w, S3);
        Set(w, S1, Timber, 10);
        Set(w, S3, Timber, 40);
        Set(w, S3, Stone, 20);
        var polities = new List<PolityRow>();
        for (int i = 0; i < w.Polities.Count; i++) if (w.Polities[i].Id != Rival) polities.Add(w.Polities[i]);
        w.Polities.Clear();
        foreach (PolityRow r in polities) w.Polities.Add(r);
        if (controlRowsGone)
        {
            var controls = new List<ControlRow>();
            for (int i = 0; i < w.Controls.Count; i++) if (w.Controls[i].Polity != Rival) controls.Add(w.Controls[i]);
            w.Controls.Clear();
            foreach (ControlRow r in controls) w.Controls.Add(r);
        }
        Assert.Equal(1L, Built(Step(w, Pre), S3));                                    // PREMISE: batch 3 builds S3

        int key = controlRowsGone ? Reservations.SettlementPurse(S1) : RivalPurse;
        Assert.Equal(key, ConstructionQuery.PurseKey(w, Cfg, S1));
        WorldState t1 = Step(w, Cfg);
        if (controlRowsGone)
        {
            Assert.Equal(1L, Built(t1, S3));
            Assert.Equal([Row(Reservations.SettlementPurse(S1), Timber, 10)], Holds(t1));
        }
        else
        {
            Assert.Equal((450_000L, 0L), (P(t1, S3), Built(t1, S3)));
            Assert.Equal([Row(RivalPurse, Timber, 32), Row(RivalPurse, Stone, 11)], Holds(t1));
        }
        Assert.Equal(437_500L, P(t1, S1));
        AssertConserved(t1);
    }

    // ================================================================== #17–#19: no fictitious stock, no deadlock, tie-dense

    /// <summary>#17 NO FICTITIOUS STOCK: a consumer between steps lowers a held good below last step's hold; the next walk
    /// recomputes from what exists — the hold never exceeds the holding — and the oldest head is never blocked.</summary>
    [Fact]
    public void AConsumerLowersAHeldGood_TheNextHoldNeverExceedsTheHolding_AndTheOldestHeadIsNeverBlocked()
    {
        WorldState w = Solo();
        Head(w, S0, 0, Granary, 437_500);
        NoBuilders(w, S0);
        Head(w, S1, 1, Granary);
        AllBuilders(w, S1);
        Set(w, S3, Timber, 22);
        Set(w, S3, Stone, 11);
        WorldState t1 = Step(w, Cfg);
        Assert.Equal([Row(PlayerPurse, Timber, 22), Row(PlayerPurse, Stone, 11)], Holds(t1));

        Set(t1, S3, Stone, 6);                            // a consumer took 5 of the held stone (holds bind no consumer)
        WorldState t2 = Step(t1, Cfg);
        Assert.Equal([Row(PlayerPurse, Timber, 22), Row(PlayerPurse, Stone, 6)], Holds(t2));
        Assert.Equal(0L, P(t2, S1));

        AllBuilders(t2, S0);                              // the oldest head: 18 + 22 = 40 timber, 9 + 6 = 15 stone
        WorldState t3 = Step(t2, Cfg);
        Assert.Equal((750_000L, 0L), (P(t3, S0), P(t3, S1)));                 // floor(15N/20); draws 12 timber, 6 stone
        Assert.Equal((10L, 0L), (TotalOf(t3, Timber), TotalOf(t3, Stone)));
        Assert.Equal([Row(PlayerPurse, Timber, 10)], Holds(t3));             // its remaining 10 timber; 5 stone not there
        AssertConserved(t3);
    }

    /// <summary>#18 DEADLOCK FREEDOM: an older head that never gets builders does not freeze the purse — the younger head
    /// progresses on the surplus beyond the hold and completes; with no surplus it waits, its status naming the holder,
    /// whose own status gives the remedy (no builders, adults present: give Building a share, or cancel).</summary>
    [Fact]
    public void AnOlderHeadThatNeverGetsBuilders_DoesNotFreezeThePurse_TheYoungerProgressesOnTheSurplus()
    {
        WorldState w = Solo();
        Head(w, S0, 0, Granary, 437_500);
        NoBuilders(w, S0);
        Head(w, S1, 1, Works);
        AllBuilders(w, S1);
        Set(w, S3, Timber, 22);
        Set(w, S3, Stone, 11);

        WorldState t1 = Step(w, Shipped);
        Assert.Equal(0L, P(t1, S1));
        HeadStatusReport waiting = Status(w, Shipped, S1), holder = Status(w, Shipped, S0);
        Assert.Equal((MaterialCause.HeldForEarlier, Ref(S0, Granary)), (waiting.Cause, waiting.Holder));
        Assert.Equal((HeadLimitKind.Labour, false, HoldRelease.None), (holder.Resolution.Limit, holder.Resolution.NoLabourAdults, holder.Release));

        Set(t1, S3, Timber, 22 + 30);                     // a surplus beyond the hold arrives
        Set(t1, S3, Stone, 11 + 20);
        WorldState t2 = Step(t1, Shipped);                // S1: min(floor(30N/41), floor(20N/23)) = 731,707; 30 timber, 17 stone
        Assert.Equal(731_707L, P(t2, S1));
        Assert.Equal([Row(PlayerPurse, Timber, 22), Row(PlayerPurse, Stone, 11 + 3)], Holds(t2));

        Set(t2, S3, Timber, 22 + 11);
        Set(t2, S3, Stone, 14 + 3);
        WorldState t3 = Step(t2, Shipped);
        Assert.Equal((1L, 437_500L), (Built(t3, S1, Works), P(t3, S0)));
        Assert.Equal([Row(PlayerPurse, Timber, 22), Row(PlayerPurse, Stone, 11)], Holds(t3));
        AssertConserved(t3);
    }

    /// <summary>#19 TIE-DENSE (CLAUDE.md: a composite key ships a tie-dense test): 20 heads tied on EnqueuedTurn across TWO
    /// purses whose settlements interleave in the building order (the founded 20-settlement dev world with one AI Empire,
    /// round-robin control), rows inserted out of id order and each head's slot running against its id, no builders
    /// anywhere: each purse's stone is set aside head by head in ascending settlement id — more than 16 ties, so List.Sort's
    /// introsort and the integer tie-break alone order them.</summary>
    [Fact]
    public void TwentyTiedHeadsAcrossTwoInterleavedPurses_SetAsideInAscendingSettlementId()
    {
        const int n = 20;
        WorldState w = WorldFounding.Found(TestConfigs.DevWorldgen() with { AiEmpires = 1 }, Cfg, 42, settlementsOverride: n);
        var purse = new int[n];
        int lastPlayer = -1, lastRival = -1;
        for (int s = 0; s < n; s++)
        {
            var id = new SettlementId(s);
            Assert.Equal(s, w.Settlements[s].Id.Value);
            Set(w, id, Stone, 0);
            Set(w, id, Timber, 0);
            NoBuilders(w, id);
            Assert.True(EmpireQuery.TryGetController(w, id, out PolityId c));
            purse[s] = c.Value;
            if (c == Player) lastPlayer = s; else lastRival = s;
        }
        Assert.True(purse[0] != purse[1] && purse[1] != purse[2], "the two purses interleave in id order");
        for (int k = 0; k < n; k++)
        {
            int s = (k * 7) % n;
            int head = n - 1 - s;
            w.ConstructionQueue.Add(new ConstructionQueueRow(new SettlementId(s), head + 1, Granary));
            w.ConstructionQueue.Add(new ConstructionQueueRow(new SettlementId(s), head, Granary));
            w.ConstructionProgress.Add(new ConstructionProgressRow(new SettlementId(s), head + 1, Granary, 4, 0));
            w.ConstructionProgress.Add(new ConstructionProgressRow(new SettlementId(s), head, Granary, 3, 0));
        }
        Set(w, new SettlementId(lastPlayer), Stone, 87);  // 4 granaries and 7 over
        Set(w, new SettlementId(lastPlayer), Timber, 400);
        Set(w, new SettlementId(lastRival), Stone, 53);   // 2 granaries and 13 over

        HoldsWalkResult walk = Walk(w, Cfg);
        Assert.Equal(n, walk.Heads.Length);
        long leftP = 87, leftR = 53, timberP = 400;
        for (int s = 0; s < n; s++)
        {
            HoldsWalkHead h = walk.HeadOf(new SettlementId(s))!;
            Assert.False(h.Released);
            Assert.Equal(n - 1 - s, h.Entry.Slot);
            ref long left = ref (purse[s] == Player.Value ? ref leftP : ref leftR);
            long stone = Math.Min(20, left);
            left -= stone;
            long timber = purse[s] == Player.Value ? Math.Min(40, timberP) : 0;
            if (purse[s] == Player.Value) timberP -= timber;
            Assert.True((stone, timber) == (h.HoldOf(Stone), h.HoldOf(Timber)), $"settlement {s}: held ({h.HoldOf(Stone)}, {h.HoldOf(Timber)}), expected ({stone}, {timber})");
            Assert.Equal(s, walk.Heads[s].Entry.Settlement.Value);                    // the walk itself is in id order
        }
        Assert.Equal((0L, 0L), (leftP, leftR));                                            // every unit of stone set aside
        WorldState t1 = Step(w, Cfg);
        Assert.Equal([Row(PlayerPurse, Timber, 400 - timberP), Row(PlayerPurse, Stone, 87), Row(RivalPurse, Stone, 53)], Holds(t1));
    }

    // ================================================================== #20: the property test

    private static SimConfig TrackwayCosting(SimConfig cfg, params RoadMaterialConfig[] materials)
    {
        RoadClassConfig[] classes = [.. cfg.Roads!.Classes];
        for (int i = 0; i < classes.Length; i++)
            if (classes[i].EdgeType == EdgeTypes.Trackway) classes[i] = classes[i] with { MaterialsPerKm = materials };
        return cfg with { Roads = cfg.Roads with { Classes = classes } };
    }

    /// <summary>A deterministic xorshift (the platform RNG is a banned construct).</summary>
    private sealed class XorShift(ulong seed)
    {
        private ulong _s = seed;
        public ulong Next() { _s ^= _s << 13; _s ^= _s >> 7; _s ^= _s << 17; return _s; }
        public long Below(long n) => (long)(Next() % (ulong)n);
        public int Int(int n) => (int)(Next() % (ulong)n);
    }

    /// <summary>
    /// #20 THE PROPERTY TEST (§1.3; L2-05, L2-15): two Empires (S0, S2 the player's; S1, S3 the rival's) on DISJOINT routes
    /// (so each road order's plan is the same on PREV and NEXT), random splits and road orders, a head at every settlement
    /// (a granary or a 30-timber/10-stone test project at a random progress, a third of them WITHOUT builders — so heads
    /// wait and hold) and a second entry behind it, random stocks, four steps a trial. Every step, for every Empire and every
    /// good: conservation and no negative stock; holds ≤ the holding after building work's draws; with a split, D_r is
    /// SimulateCharges on S (unchanged), D_b is what the no-split step draws AND sets aside, draws + holds ≤ G_b, road works
    /// find S − d − h ≥ min(D_r, E_r), and the forecast draws, holds and road charge are the step's own; the oldest
    /// unreleased head of every purse was resolved against its whole starting budget.
    /// </summary>
    [Fact]
    public void ThePropertyTest_TwoEmpires_RandomSplitsHeadsWaitsAndRoads_EveryInvariantEveryStep()
    {
        SimConfig cfg = WithWorks(TrackwayCosting(Cfg, new RoadMaterialConfig("timber", 1.0), new RoadMaterialConfig("tools", 0.05)),
            labor: 0.5, timber: 30, stone: 10);
        SimConfig noSplit = TestConfigs.PreAllocationRatios(cfg);
        var rng = new XorShift(4055);
        int[] goods = [Timber, Stone, Tools];
        int heldUnderSplit = 0, waitingHolds = 0, bound = 0, guarded = 0, roadPaid = 0;
        for (int trial = 0; trial < 50; trial++)
        {
            WorldState w = Duo();
            w.TransportEdges.Clear();
            w.SettlementDistances.Clear();
            w.TradeFlows.Clear();
            foreach (PolityId p in new[] { Player, Rival })
                w.ResearchCompleted.Add(new ResearchCompletedRow(p, Research.Nodes[Research.IndexOfId("track_road")].Key));
            foreach ((int id, SettlementId a, SettlementId b, long km) in new[] { (1, S0, S2, 50L), (2, S1, S3, 40L) })
            {
                w.TransportEdges.Add(new TransportEdgeRow(id, a, b, EdgeTypes.DirtPath, TransportModes.Road, TransportEdgeStates.Complete,
                    100_000, km, 0, 0, 0, EdgeTypes.DirtPath, 0.0, 1.0));
                w.SettlementDistances.Add(new SettlementDistanceRow(a, b, 100.0));
                w.SettlementDistances.Add(new SettlementDistanceRow(b, a, 100.0));
            }
            foreach (SettlementId s in new[] { S0, S1, S2, S3 })
            {
                if (rng.Int(3) == 0) NoBuilders(w, s); else AllBuilders(w, s);
                Head(w, s, rng.Int(3), rng.Int(2) == 0 ? Granary : Works, rng.Int(2) == 0 ? 0 : 1 + rng.Below(N - 1));
                Head(w, s, 5, Granary);
            }
            for (int turn = 0; turn < 4; turn++)
            {
                foreach (SettlementId s in new[] { S0, S1, S2, S3 })
                {
                    Set(w, s, Timber, rng.Below(60));
                    Set(w, s, Stone, rng.Below(40));
                    Set(w, s, Tools, rng.Below(4));
                }
                long t = w.Clock.Turn;
                var batch = new List<OrderRecord>();
                foreach (PolityId p in new[] { Player, Rival })
                {
                    if (rng.Int(3) != 0) batch.Add(new OrderRecord(t, p.Value, OrderKind.SetResourceAllocation, p.Value, rng.Int(4) == 0 ? 100 : rng.Int(101)));
                    if (rng.Int(4) != 0) batch.Add(OrderRecord.From(t, p, OrderKind.DevelopRoads, 0, 100.0));
                }
                OrderLog log = Log([.. batch]);
                Table<ResourceAllocationRow> rows = ConstructionQuery.AllocationsWith(w, cfg, w.ResourceAllocations, batch);
                EmpireAllocation?[] step = ConstructionQuery.StepAllocations(w, cfg, w.ConstructionQueue, w.ConstructionProgress,
                    w.GoodStocks, rows, batch, 10.0)!;
                HoldsWalkResult walk = ConstructionQuery.HoldsWalk(w, cfg, w.ConstructionQueue, w.ConstructionProgress, w.GoodStocks, 10.0,
                    ConstructionQuery.GrantsLeft(step));
                WorldState built = ConstructionOnly(cfg, log).Step(w);
                WorldState builtOff = ConstructionOnly(noSplit, log).Step(w);
                WorldState next = new TurnExecutor(ResearchRigs.FlatEra(10.0), [SystemCatalog.Construction(cfg), SystemCatalog.RoadDevelopment(cfg)], log).Step(w);
                AssertConserved(next);
                Assert.Equal(walk.Rows(), Holds(built));

                // The oldest unreleased head of each purse saw the whole starting budget.
                for (int k = 0; k < walk.Purses.Length; k++)
                    for (int h = 0; h < walk.Heads.Length; h++)
                    {
                        if (walk.Heads[h].Purse != walk.Purses[k] || walk.Heads[h].Released) continue;
                        Assert.Equal(walk.Start[k], walk.Heads[h].ResolvedAgainst);
                        break;
                    }

                for (int k = 0; k < w.Polities.Count; k++)
                {
                    PolityId p = w.Polities[k].Id;
                    SettlementId[] purse = RoadDevelopmentQuery.PayingSettlements(w, p);
                    long Purse(IReadOnlyWorldState x, int g) => EmpireStockpile.Available(x.GoodStocks, purse, new GoodId(g));
                    long HeldIn(IReadOnlyWorldState x, int g)
                    {
                        long sum = 0;
                        for (int i = 0; i < x.ConstructionHolds.Count; i++)
                            if (x.ConstructionHolds[i].Purse == p.Value && x.ConstructionHolds[i].Good == g) sum += x.ConstructionHolds[i].Units;
                        return sum;
                    }
                    var supply = new long[Cfg.Goods!.Goods.Length + 1];
                    for (int g = 0; g < supply.Length; g++) supply[g] = Purse(w, g);
                    OrderRecord? road = RoadDevelopmentQuery.DeliveredOrderFor(w, cfg, batch, p);
                    long[] dr = road is { } o ? RoadDevelopmentQuery.SimulateCharges(w, cfg, p, o.Amount, supply) : new long[supply.Length];
                    foreach (int g in goods)
                    {
                        long s0 = Purse(w, g), drawn = s0 - Purse(built, g), held = HeldIn(built, g), roadDraw = Purse(built, g) - Purse(next, g);
                        Assert.True(Purse(next, g) >= 0, "no stock goes negative");
                        Assert.True(held <= Purse(built, g), $"holds {held} ≤ the holding after draws {Purse(built, g)}");
                        if (held > 0 && step[k] is null) waitingHolds++;
                        if (step[k] is not { } a) continue;
                        AllocationSplit x = a.SplitOf(g)!.Value;
                        // D_r on S, unchanged — except where the starvation guard fired, which takes the road's demand as 0.
                        Assert.Equal((s0, a.RoadPaysNothingFromItsShare ? 0 : dr[g]), (x.Supply, x.RoadDemand));
                        Assert.Equal(s0 - Purse(builtOff, g) + HeldIn(builtOff, g), x.BuildingDemand);        // D_b = claims
                        Assert.True(drawn + held <= x.BuildingGrant, $"draws {drawn} + holds {held} ≤ G_b {x.BuildingGrant}");
                        Assert.True(s0 - drawn - held >= Math.Min(x.RoadDemand, x.RoadShare), "road works find min(D_r, E_r)");
                        Assert.Equal((drawn, held, roadDraw), (a.BuildingDrawOf(g), a.BuildingHoldOf(g), a.RoadChargeOf(g)));
                        if (held > 0) heldUnderSplit++;
                        if (x.HoldsBack) bound++;
                        if (roadDraw > 0) roadPaid++;
                    }
                    if (step[k] is { RoadPaysNothingFromItsShare: true }) guarded++;
                }
                w = next;
            }
        }
        Assert.True(heldUnderSplit > 20, $"anti-vacuity: holds under a split {heldUnderSplit}");
        Assert.True(waitingHolds > 20, $"anti-vacuity: holds with no split {waitingHolds}");
        Assert.True(bound > 20, $"anti-vacuity: the split held materials back {bound} times");
        Assert.True(guarded > 3, $"anti-vacuity: the guard fired {guarded} times");
        Assert.True(roadPaid > 20, $"anti-vacuity: road works were paid {roadPaid} times");
    }

    // ================================================================== #21–#23: one walk, the edge, the colonised good

    /// <summary>#21 HoldsWalk ≡ THE STEP (L2-10): on a construction-only rig, at b = 100 and b = 50 (a split and a road
    /// order delivered, road development not run), THE walk on PREV gives the step's draws, progress and published table
    /// unit for unit — and HeadStatus (the UI's estimate, the same walk) agrees head by head.</summary>
    [Theory]
    [InlineData(100)]
    [InlineData(50)]
    public void HoldsWalkOnPrev_EqualsTheStep_AndHeadStatusAgrees_HeadByHead(int b)
    {
        WorldState w = SplitRig();
        Head(w, S2, 1, Granary);                          // a third head behind them
        OrderRecord[] orders = b == 100 ? [Roads(0)] : [Split(0, b), Roads(0)];
        HoldsWalkResult walk = ConstructionQuery.HoldsWalk(w, Shipped, w.ConstructionQueue, w.ConstructionProgress, w.GoodStocks, 10.0,
            ConstructionQuery.GrantsLeft(ConstructionQuery.StepAllocations(w, Shipped, w.ConstructionQueue, w.ConstructionProgress,
                w.GoodStocks, ConstructionQuery.AllocationsWith(w, Shipped, w.ResourceAllocations, orders), orders, 10.0)));
        WorldState next = ConstructionOnly(Shipped, Log(orders)).Step(w);

        Assert.Equal(walk.Rows(), Holds(next));
        Assert.NotEmpty(Holds(next));
        foreach (int g in new[] { Timber, Stone })
            Assert.Equal(walk.Drawn[0][g], TotalOf(w, g) - TotalOf(next, g));
        foreach (HoldsWalkHead h in walk.Heads)
        {
            SettlementId s = h.Entry.Settlement;
            HeadResolution r = h.Resolution;
            Assert.Equal(r.Completes ? 0L : r.ProgressAfter, P(next, s));
            HeadStatusReport status = Status(w, Shipped, s, orders);
            Assert.Equal((r.ProgressAfter, r.Limit, r.LimitGood, h.Cause, h.Holder), (status.Resolution.ProgressAfter,
                status.Resolution.Limit, status.Resolution.LimitGood, status.Cause, status.Holder));
            Assert.Equal(r.Draws, status.Resolution.Draws);
            Assert.Equal(h.Holds, status.SetAside);
        }
        // b = 50 binds: S1 gets 28 of its 41 timber (682,926) — the walk, the step and the estimate all say so.
        Assert.Equal(b == 100 ? 0L : 682_926L, P(next, S1));
    }

    /// <summary>#22 ORDER IS DATA (L3-07; re-rigged in T1a round 2, 2026-10-16: the kernel is frozen, so there is no kernel edge
    /// — road development is handed construction's NEXT ConstructionHolds read-only (SystemCatalog) and reads it AS IT STANDS
    /// when it runs). t1 is the split rig (63 timber) after one CONSTRUCTION-ONLY step — no road development in it, so the
    /// premise cannot depend on what road works read: S1's works completes on 41 timber and S0's waiting granary sets aside
    /// the remaining 22 timber and 11 stone; the purse holds exactly 22 timber, all of it at S2, and the route is untouched
    /// (60 timber). NEXT then carries into the next step (the kernel's PREV → NEXT copy) one of four tables: t1's own
    /// published rows (22 timber), a LOADED SAVE's rows (10 timber in purse 1, through Snapshot.Save/Load), a loaded save's
    /// SINGLE-SETTLEMENT row (10 of S2's timber in S2's own purse, key −1 − 2: the purse kind that also floors its donor) or
    /// none. A road order is then delivered:
    /// <list type="bullet">
    /// <item>THE SHIPPED ORDER (construction, then roaddevelopment): construction clears the table and publishes THIS step's
    ///   set-aside (22), so road works get 22 − 22 = 0, whatever was carried in;</item>
    /// <item>A NON-SHIPPED ORDER (roaddevelopment before construction, or without it): road development reads the carried
    ///   table — 22 − 22 = 0, 22 − 10 = 12 (either loaded row; S2, floored at 10, gives 12), 22 − 0 = 22 units — and never
    ///   writes it (alone, NEXT keeps the carried rows);</item>
    /// <item>the hold rule OFF (PreReservations): the table is ignored in any order, for BOTH purse kinds — 22 units with
    ///   either loaded save's rows (T1a round 2 fix, 2026-10-17, finding T1a-r2-L1-T1a-r2-V1-F4: a rule-off gate covering
    ///   Empire-purse rows only, single-settlement rows still subtracted and flooring S2, charged 12 here — MEASURED, mutant
    ///   N5 — and passed every other row).</item>
    /// </list></summary>
    [Theory]
    [InlineData("published", 0L)]
    [InlineData("loaded", 12L)]
    [InlineData("single", 12L)]
    [InlineData("none", 22L)]
    public void TheEdgeOrderIsData_TheShippedOrderReadsThisStepsHolds_AnotherOrderReadsTheTableAsItStands(string carried, long otherOrder)
    {
        WorldState t1 = ConstructionOnly(Shipped, null).Step(SplitRig(timber: 63));   // S1 completes on 41; S0 sets aside 22 / 11
        Assert.Equal([Row(PlayerPurse, Timber, 22), Row(PlayerPurse, Stone, 11)], Holds(t1));
        Assert.Equal((22L, 22L, 1L, 0), (TotalOf(t1, Timber), Of(t1, S2, Timber), Built(t1, S1, Works), t1.RoadDevelopments.Count));

        WorldState prev = t1.Clone();
        if (carried != "published") prev.ConstructionHolds.Clear();
        ConstructionHoldRow single = Row(Reservations.SettlementPurse(S2), Timber, 10);
        if (carried is "loaded" or "single")
        {
            prev.ConstructionHolds.Add(carried == "loaded" ? Row(PlayerPurse, Timber, 10) : single);
            prev = SaveLoad(prev);
        }
        ConstructionHoldRow[] expected = carried switch
        {
            "published" => [Row(PlayerPurse, Timber, 22), Row(PlayerPurse, Stone, 11)],
            "loaded" => [Row(PlayerPurse, Timber, 10)],
            "single" => [single],
            _ => [],
        };
        Assert.Equal(expected, Holds(prev));                                       // what NEXT will carry in

        const string B = ConstructionSystem.Name;                                  // the pipelines, as pipeline.json names
        const string R = Sim.Core.Systems.Roads.RoadDevelopmentSystem.Name;          // them, built from each arm's config
        long Charged(SimConfig cfg, string[] order, out WorldState next)
        {
            var pipeline = new SystemRegistration[order.Length];
            for (int i = 0; i < order.Length; i++)
                pipeline[i] = order[i] == B ? SystemCatalog.Construction(cfg) : SystemCatalog.RoadDevelopment(cfg);
            next = new TurnExecutor(ResearchRigs.FlatEra(10.0), pipeline, Log(Roads(1))).Step(prev);
            long u = 0;
            for (int i = 0; i < next.RoadDevelopments.Count; i++)
                if (next.RoadDevelopments[i].Turn == 1) u += next.RoadDevelopments[i].MaterialUnits;
            return u;
        }

        // The shipped order: this step's set-aside, recomputed by construction from what exists (22 of 22).
        Assert.Equal(0L, Charged(Shipped, [B, R], out WorldState shippedNext));
        Assert.Equal([Row(PlayerPurse, Timber, 22), Row(PlayerPurse, Stone, 11)], Holds(shippedNext));

        // Another order: the table as it stands when road development runs — the carried rows.
        Assert.Equal(otherOrder, Charged(Shipped, [R, B], out WorldState reversedNext));
        Assert.Equal(22L - otherOrder, TotalOf(reversedNext, Timber));
        Assert.Equal(otherOrder, Charged(Shipped, [R], out WorldState aloneNext));
        Assert.Equal(Holds(prev), Holds(aloneNext));                               // read, never written
        AssertConserved(reversedNext);

        // The rule off: the table is ignored, in the shipped order and in any other.
        Assert.Equal(22L, Charged(ShippedPre, [R], out _));
        Assert.Equal(22L, Charged(ShippedPre, [R, B], out _));
        Assert.Equal(22L, Charged(ShippedPre, [B, R], out _));
    }

    /// <summary>A GENUINE schema-v33 file of <paramref name="w"/>'s state, loaded. Snapshot's v33 layout is the v34 one with
    /// the canonical stream ending after section 60, so: the v34 bytes of <paramref name="w"/> with its ConstructionHolds
    /// emptied, the header's version set to 33, and section 61's (empty) int32 row count — the last field before the int32
    /// topology trailer — removed.</summary>
    private static WorldState LoadAsV33(WorldState w)
    {
        WorldState empty = w.Clone();
        empty.ConstructionHolds.Clear();
        byte[] v34;
        using (var ms = new MemoryStream()) { Snapshot.Save(empty, ms); v34 = ms.ToArray(); }
        Assert.Equal(34, BinaryPrimitives.ReadInt32LittleEndian(v34.AsSpan(8)));                  // the header's version
        Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(v34.AsSpan(v34.Length - 8)));     // section 61: 0 rows
        var v33 = new byte[v34.Length - 4];
        v34.AsSpan(0, v34.Length - 8).CopyTo(v33);
        v34.AsSpan(v34.Length - 4).CopyTo(v33.AsSpan(v34.Length - 8));                           // the topology trailer
        BinaryPrimitives.WriteInt32LittleEndian(v33.AsSpan(8), 33);
        return Snapshot.Load(new MemoryStream(v33), w.Terrain);
    }

    /// <summary>
    /// #22b THE v33 → v34 MIGRATION IS EXACT IN THE SHIPPED ORDER — and only there (T1a round 2 fix, 2026-10-17; finding
    /// T1a-r2-L1-T1a-r2-V1-F3: the claim that a v33 load's EMPTY ConstructionHolds table is exact, now qualified so in
    /// Snapshot, CanonicalSchema, Reservations, ADR-005 and sim.json). A v33 file has no section 61. From #22's t1 (purse 1
    /// sets aside 22 timber and 11 stone; the purse holds 22 timber), a GENUINE v33 file of the same state (<see
    /// cref="LoadAsV33"/>: it loads with the table empty, and its v33 hash is t1's) and a v34 save (both rows kept) are each
    /// stepped with a road order:
    /// <list type="bullet">
    /// <item>THE SHIPPED ORDER (construction, then roaddevelopment — pinned by ConstructionHoldsSchemaV34Tests.
    ///   TheShippedPipeline_RunsConstructionBeforeRoadDevelopment): construction rebuilds the table before road development
    ///   reads it, so both arms charge 0 and reach the SAME next world (equal hashes) — the migration is exact;</item>
    /// <item>ROAD DEVELOPMENT FIRST (reversed, or without construction — SystemCatalog.RoadDevelopment's ownership
    ///   paragraph): it reads the table as loaded — the v34 save's rows (0 units) but the migrated world's empty table (22
    ///   units) — so the two continue differently.</item>
    /// </list>
    /// A road development handed PREV's table instead of NEXT's (mutant A1, the round's R2-A1) breaks the shipped half: the
    /// v34 arm reads its loaded rows and charges 0, the migrated arm charges 22.
    /// </summary>
    [Fact]
    public void TheV33Migration_IsExactInTheShippedOrder_ARoadFirstOrderReadsTheMigratedWorldsEmptyTable()
    {
        WorldState t1 = ConstructionOnly(Shipped, null).Step(SplitRig(timber: 63));   // S1 completes on 41; S0 sets aside 22 / 11
        ConstructionHoldRow[] published = [Row(PlayerPurse, Timber, 22), Row(PlayerPurse, Stone, 11)];
        Assert.Equal(published, Holds(t1));
        Assert.Equal((22L, 0), (TotalOf(t1, Timber), t1.RoadDevelopments.Count));
        WorldState v34 = SaveLoad(t1), v33 = LoadAsV33(t1);
        Assert.Equal(published, Holds(v34));
        Assert.Empty(Holds(v33));                                                  // the migration: no section 61, no rows
        Assert.Equal(SchemaProjection.HashAtSchemaV33(t1, out _), SchemaProjection.HashAtSchemaV33(v33, out _));

        const string B = ConstructionSystem.Name;
        const string R = Sim.Core.Systems.Roads.RoadDevelopmentSystem.Name;
        (long Units, string Hash) Run(WorldState prev, string[] order)
        {
            var pipeline = new SystemRegistration[order.Length];
            for (int i = 0; i < order.Length; i++)
                pipeline[i] = order[i] == B ? SystemCatalog.Construction(Shipped) : SystemCatalog.RoadDevelopment(Shipped);
            WorldState next = new TurnExecutor(ResearchRigs.FlatEra(10.0), pipeline, Log(Roads(1))).Step(prev);
            AssertConserved(next);
            return (RoadUnits(next), WorldHash.ComputeHex(next));
        }

        // The shipped order: construction rebuilds the table first — exact.
        (long Units, string Hash) fromV34 = Run(v34, [B, R]), fromV33 = Run(v33, [B, R]);
        Assert.Equal((0L, 0L), (fromV34.Units, fromV33.Units));
        Assert.Equal(fromV34.Hash, fromV33.Hash);

        // Road development first: the table is read as loaded, before construction rebuilds it.
        foreach (string[] order in new[] { new[] { R, B }, new[] { R } })
        {
            fromV34 = Run(v34, order);
            fromV33 = Run(v33, order);
            Assert.Equal((0L, 22L), (fromV34.Units, fromV33.Units));
            Assert.NotEqual(fromV34.Hash, fromV33.Hash);
        }
    }

    /// <summary>#23 Colonisation, the one system between construction and road development, moves only grain: no project
    /// input and no road-class material is a good it moves, so road works see exactly the stocks construction left.</summary>
    [Fact]
    public void NoColonisedGood_IsAConstructionOrRoadInput()
    {
        GoodsConfig goods = Cfg.Goods!;
        int grain = goods.GrainId;
        foreach (ConstructionProjectEntry p in goods.Projects!)
            foreach (ProjectInput i in p.Inputs) Assert.NotEqual(grain, goods.IdOf(i.Good));
        foreach (RoadClassConfig c in Cfg.Roads!.Classes)
            foreach (RoadMaterialConfig m in c.MaterialsPerKm) Assert.NotEqual(grain, goods.IdOf(m.Good));
        string[] pipeline;
        using (var stream = Sim.Data.DataFiles.OpenPipeline())
        {
            var doc = System.Text.Json.Nodes.JsonNode.Parse(stream)!["pipeline"]!.AsArray();
            pipeline = new string[doc.Count];
            for (int i = 0; i < pipeline.Length; i++) pipeline[i] = (string)doc[i]!;
        }
        int c0 = Array.IndexOf(pipeline, ConstructionSystem.Name), r0 = Array.IndexOf(pipeline, Sim.Core.Systems.Roads.RoadDevelopmentSystem.Name);
        Assert.True(c0 >= 0 && c0 < r0);
        Assert.Contains(Sim.Core.Systems.Colonization.ColonizationSystem.Name, pipeline[(c0 + 1)..r0]);
    }

    // ================================================================== fix round (2026-10-16): the review's findings

    /// <summary>The Duo world (S0, S2 the player's; S1, S3 the rival's) with a SECOND control row for S0, the rival's.
    /// EmpireQuery documents that a hand-built or deserialized world may legally carry two rows for one place (D-037's
    /// cardinality is unenforced) and Snapshot.Load accepts it. S0's purse key is its LOWEST controller's, the player's (1);
    /// EmpireStockpile.Holders nonetheless lists S0 for the rival's heads too (every place the rival has a row for).</summary>
    private static WorldState DoubleControl()
    {
        WorldState w = Duo();
        w.Controls.Add(new ControlRow(Rival, S0, 1.0));
        return w;
    }

    private static WorldState SaveLoad(WorldState w)
    {
        using var ms = new MemoryStream();
        Snapshot.Save(w, ms);
        return Snapshot.Load(new MemoryStream(ms.ToArray()), w.Terrain);
    }

    /// <summary>
    /// FIX ROUND, finding T1a-L1-T1a-V1-F1 (two control rows for one place): the purses' holder sets are DISJOINT
    /// (ConstructionQuery.PurseHolders — Holders less every place whose own purse key differs), so the walk never counts one
    /// unit in two purses. The player's older head at S2 waits (no builders) and sets aside S0's 40 timber and 20 stone in
    /// purse 1; the rival's younger head at S1 has builders but its purse (S1, S3) holds nothing, so it waits for want of
    /// materials ("the empire has none") and sets nothing aside. Before the fix the rival's budget ALSO counted S0, so it
    /// drew the units purse 1 had set aside, invariant (i) threw "purse 1 sets aside 40 of good 4 but holds 0 after
    /// construction's draws", and every later step threw the same. Also through Snapshot.Save/Load (P1b), which accepts the
    /// two rows. PREMISE: batch 3 (PreReservations) steps the same world and the rival builds from S0's units.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TwoControlRowsForOnePlace_ThePursesAreDisjoint_TheWaitingOlderHeadHoldsItsUnits_TheStepNeverThrows(bool throughSaveLoad)
    {
        WorldState w = DoubleControl();
        Head(w, S2, 0, Granary);
        NoBuilders(w, S2);
        Head(w, S1, 1, Granary);
        AllBuilders(w, S1);
        Set(w, S0, Timber, 40);
        Set(w, S0, Stone, 20);
        if (throughSaveLoad) w = SaveLoad(w);
        int rows = 0;
        for (int i = 0; i < w.Controls.Count; i++) if (w.Controls[i].Place == S0) rows++;
        Assert.Equal(2, rows);                                                        // the load kept both rows

        // PREMISES: the keys are the lowest controller's; the rival's holder set (batch 3's) lists S0; batch 3 builds S1.
        Assert.Equal((PlayerPurse, RivalPurse, PlayerPurse), (ConstructionQuery.PurseKey(w, Cfg, S0),
            ConstructionQuery.PurseKey(w, Cfg, S1), ConstructionQuery.PurseKey(w, Cfg, S2)));
        Assert.Contains(S0, ConstructionQuery.Holders(w, Cfg, S1));
        Assert.Equal(1L, Built(Step(w, Pre), S1));

        // The purse holder sets: disjoint, each exactly the places whose key is that purse's.
        Assert.Equal([S2, S0], ConstructionQuery.PurseHolders(w, Cfg, S2));
        Assert.Equal([S1, S3], ConstructionQuery.PurseHolders(w, Cfg, S1));
        Assert.Equal(ConstructionQuery.Holders(w, Pre, S1), ConstructionQuery.PurseHolders(w, Pre, S1));   // rule off: Holders

        HoldsWalkResult walk = Walk(w, Cfg);
        WorldState t1 = Step(w, Cfg);                                                 // no throw
        Assert.Null(ConstructionQuery.HoldsInvariantViolation(walk, Cfg.Goods!, t1.GoodStocks, t1.ConstructionHolds));
        Assert.Equal((0L, 0L, 0L, 0L), (P(t1, S1), Built(t1, S1), P(t1, S2), Built(t1, S2)));
        Assert.Equal((40L, 20L), (Of(t1, S0, Timber), Of(t1, S0, Stone)));
        Assert.Equal([Row(PlayerPurse, Timber, 40), Row(PlayerPurse, Stone, 20)], Holds(t1));
        HoldsWalkHead rival = walk.HeadOf(S1)!;
        Assert.Equal((0L, 0L, 0L, 0L), (rival.DrawOf(Timber), rival.DrawOf(Stone), rival.HoldOf(Timber), rival.HoldOf(Stone)));
        Assert.Equal((HeadLimitKind.Materials, MaterialCause.EmpireHasNone), (rival.Resolution.Limit, rival.Cause));
        AssertConserved(t1);

        WorldState t2 = Step(t1, Cfg);                                                // and the next step too
        Assert.Equal([Row(PlayerPurse, Timber, 40), Row(PlayerPurse, Stone, 20)], Holds(t2));
    }

    /// <summary>
    /// FIX ROUND, T1a-L1-T1a-V1-F1 (cont., the verdict's A3): with builders at BOTH heads no head waits. The player's older
    /// head draws S0's 40 / 20 and completes; the rival's purse (S1, S3) holds nothing, so it sets nothing aside. Before the
    /// fix the rival's budget still counted S0's units after they were drawn and it set aside 40 timber that no longer
    /// existed (invariant (i): "purse 2 sets aside 40 of good 4 but holds 0"). The step is now batch 3's world exactly —
    /// nothing was ever set aside here.
    /// </summary>
    [Fact]
    public void TwoControlRowsForOnePlace_TheOlderHeadDraws_TheOtherPurseSetsAsideNothingThatIsGone()
    {
        WorldState w = DoubleControl();
        Head(w, S2, 0, Granary);
        AllBuilders(w, S2);
        Head(w, S1, 1, Granary);
        AllBuilders(w, S1);
        Set(w, S0, Timber, 40);
        Set(w, S0, Stone, 20);

        WorldState t1 = Step(w, Cfg);
        Assert.Equal((1L, 0L, 0L), (Built(t1, S2), Built(t1, S1), P(t1, S1)));
        Assert.Equal((0L, 0L), (Of(t1, S0, Timber), Of(t1, S0, Stone)));
        Assert.Empty(Holds(t1));
        WorldState pre = Step(w, Pre);
        Assert.True(WorldStates.StateEquals(pre, t1), "nothing is set aside, so the step is batch 3's");
        Assert.Equal(WorldHash.ComputeHex(pre), WorldHash.ComputeHex(t1));
    }

    /// <summary>
    /// FIX ROUND, T1a-L1-T1a-V1-F1 (cont.): the rival's head DRAWS from its own purse — its draw order (batch 3's Holders)
    /// reaches S0 BEFORE S3, so a walk that filtered only the budget but drew through Holders would take the units the
    /// player's waiting head sets aside at S0. ConstructionSystem draws through PurseHolders too: the rival completes from
    /// S3's 40 / 20 and S0's units stay set aside, untouched.
    /// </summary>
    [Fact]
    public void TwoControlRowsForOnePlace_TheOtherPurseDrawsOnlyItsOwnPlaces()
    {
        WorldState w = DoubleControl();
        Head(w, S2, 0, Granary);
        NoBuilders(w, S2);
        Head(w, S1, 1, Granary);
        AllBuilders(w, S1);
        Set(w, S0, Timber, 40);
        Set(w, S0, Stone, 20);
        Set(w, S3, Timber, 40);
        Set(w, S3, Stone, 20);
        SettlementId[] order = ConstructionQuery.Holders(w, Cfg, S1);
        Assert.True(Array.IndexOf(order, S0) < Array.IndexOf(order, S3), "premise: batch 3's draw order reaches S0 first");
        WorldState pre = Step(w, Pre);                                                // PREMISE: batch 3 takes S0's units
        Assert.Equal((1L, 0L, 40L), (Built(pre, S1), Of(pre, S0, Timber), Of(pre, S3, Timber)));

        WorldState t1 = Step(w, Cfg);
        Assert.Equal((1L, 0L), (Built(t1, S1), Built(t1, S2)));
        Assert.Equal((40L, 20L, 0L, 0L), (Of(t1, S0, Timber), Of(t1, S0, Stone), Of(t1, S3, Timber), Of(t1, S3, Stone)));
        Assert.Equal([Row(PlayerPurse, Timber, 40), Row(PlayerPurse, Stone, 20)], Holds(t1));
        AssertConserved(t1);
    }

    /// <summary>
    /// FIX ROUND, T1a-L1-T1a-V1-F1 (cont.): the rule's other consequence in a double-control world. S0's stock belongs to
    /// its lowest controller's purse ALONE, so an OLDER head of the other controller does not take it even with no hold
    /// anywhere: the rival's granary at S1 (turn 0, builders) completes from its own S3, and the player's younger granary at
    /// S2 (turn 1, builders) then completes from S0. Batch 3 (PreReservations) let the rival draw S0 first — its draw order
    /// reaches S0 before S3 — so the player's head found nothing (a recorded D-055 change, double-control worlds only).
    /// </summary>
    [Fact]
    public void TwoControlRowsForOnePlace_AnOlderHeadOfTheOtherController_CannotTakeThePlacesStock()
    {
        WorldState w = DoubleControl();
        Head(w, S1, 0, Granary);
        AllBuilders(w, S1);
        Head(w, S2, 1, Granary);
        AllBuilders(w, S2);
        Set(w, S0, Timber, 40);
        Set(w, S0, Stone, 20);
        Set(w, S3, Timber, 40);
        Set(w, S3, Stone, 20);
        SettlementId[] order = ConstructionQuery.Holders(w, Cfg, S1);
        Assert.True(Array.IndexOf(order, S0) < Array.IndexOf(order, S3), "premise: batch 3's draw order reaches S0 first");
        WorldState pre = Step(w, Pre);                                                // PREMISE: batch 3
        Assert.Equal((1L, 0L, 0L, 40L), (Built(pre, S1), Built(pre, S2), Of(pre, S0, Timber), Of(pre, S3, Timber)));

        WorldState t1 = Step(w, Cfg);
        Assert.Equal((1L, 1L), (Built(t1, S1), Built(t1, S2)));
        Assert.Equal((0L, 0L, 0L, 0L), (Of(t1, S0, Timber), Of(t1, S0, Stone), Of(t1, S3, Timber), Of(t1, S3, Stone)));
        Assert.Empty(Holds(t1));
        AssertConserved(t1);
    }

    /// <summary>
    /// FIX ROUND, T1a-L1-T1a-V1-F1 (cont., the verdict's A4): the FULL shipped pipeline (every system) steps the
    /// double-control world for three turns without throwing; every published set-aside fits in its purse's own places
    /// (S0 + S2 for the player, S1 + S3 for the rival) and the player's waiting head does set aside. Before the fix the
    /// first step threw "purse 1 sets aside 20 of good 5 but holds 3".
    /// </summary>
    [Fact]
    public void TwoControlRowsForOnePlace_TheFullPipelineStepsWithoutThrowing()
    {
        WorldState w = DoubleControl();
        Head(w, S2, 0, Granary);
        NoBuilders(w, S2);
        Head(w, S1, 1, Granary);
        AllBuilders(w, S1);
        Set(w, S0, Timber, 40);
        Set(w, S0, Stone, 20);
        Assert.Null(Record.Exception(() => FoundingRigs.Pipeline(Pre, null).Step(w)));   // PREMISE: batch 3 steps it
        bool held = false;
        for (int turn = 0; turn < 3; turn++)
        {
            w = FoundingRigs.Pipeline(Cfg, null).Step(w);
            for (int i = 0; i < w.ConstructionHolds.Count; i++)
            {
                ConstructionHoldRow row = w.ConstructionHolds[i];
                Assert.True(row.Purse == PlayerPurse || row.Purse == RivalPurse, $"purse {row.Purse}");
                (SettlementId a, SettlementId b) = row.Purse == PlayerPurse ? (S0, S2) : (S1, S3);
                Assert.True(row.Units <= Of(w, a, row.Good) + Of(w, b, row.Good), $"turn {turn}: {row} beyond its purse's places");
                held |= row.Purse == PlayerPurse;
            }
        }
        Assert.True(held, "anti-vacuity: the player's waiting head set something aside");
    }

    // ================================================================== T1a round 2 (2026-10-16): road works in a double-control world

    /// <summary>The double-control world (<see cref="DoubleControl"/>: S0 carries the player's row, its lowest, and the
    /// rival's) with ONE candidate route S1–S3 for the RIVAL (a 60 km dirt path: the trackway costs exactly 60 timber), the
    /// rival's road research, every settlement's labour on building, and the player's waiting granary at S2 (turn 0, no
    /// builders), which sets aside S0's 40 timber and 20 stone in purse 1.</summary>
    private static WorldState RivalRoadRig() => RivalRoadRig(S1, S3);

    /// <summary><see cref="RivalRoadRig()"/> with the rival's one candidate route <paramref name="a"/>–<paramref name="b"/>
    /// (the same 60 km dirt path) instead of S1–S3.</summary>
    private static WorldState RivalRoadRig(SettlementId a, SettlementId b)
    {
        WorldState w = DoubleControl();
        w.TransportEdges.Clear();
        w.TransportEdges.Add(new TransportEdgeRow(1, a, b, EdgeTypes.DirtPath, TransportModes.Road, TransportEdgeStates.Complete,
            100_000, Road, 0, 0, 0, EdgeTypes.DirtPath, 0.0, 1.0));
        w.SettlementDistances.Clear();
        w.SettlementDistances.Add(new SettlementDistanceRow(a, b, 100.0));
        w.SettlementDistances.Add(new SettlementDistanceRow(b, a, 100.0));
        w.TradeFlows.Clear();
        w.ResearchCompleted.Add(new ResearchCompletedRow(Rival, Research.Nodes[Research.IndexOfId("track_road")].Key));
        for (int s = 0; s < w.Settlements.Count; s++) AllBuilders(w, w.Settlements[s].Id);
        Head(w, S2, 0, Granary);
        NoBuilders(w, S2);
        Set(w, S0, Timber, 40);
        Set(w, S0, Stone, 20);
        return w;
    }

    private static OrderRecord RivalRoads(long turn) => OrderRecord.From(turn, Rival, OrderKind.DevelopRoads, 0, 100.0);

    private static TurnExecutor BuildAndRivalRoads(SimConfig cfg) =>
        new(ResearchRigs.FlatEra(10.0), [SystemCatalog.Construction(cfg), SystemCatalog.RoadDevelopment(cfg)], Log(RivalRoads(0)));

    /// <summary>
    /// T1a ROUND 2 (2026-10-16; the fix round's open item 1 — its probe t1a-out/fix/ZzRoadResidualProbe.cs.txt, turned into this
    /// test): THE SAME DISJOINT-HOLDER RULE FOR ROAD WORKS. The rival orders its road S1–S3 (60 timber) with
    /// <paramref name="atS3"/> timber of its own at S3. With the hold rule on its payers are its places less S0
    /// (ConstructionQuery.EmpirePursePlaces: S0 is purse 1's alone, as construction's PurseHolders already says), so its road
    /// works pay from S3 only — RoadCharge(atS3, 60) units — and S0 keeps every unit purse 1 set aside in that same step.
    /// MEASURED before the fix (v2, the probe): the rival's payers were [S1, S3, S0] and it spent S0's set-aside — 40 units
    /// with nothing at S3, 25 + 35 with 25 at S3 — while purse 1 published (1, timber, 40). With the rule OFF
    /// (PreReservations) road works are batch 3's, unchanged: the payers keep S0, which pays, and nothing is set aside.
    /// </summary>
    [Theory]
    [InlineData(true, 0L)]
    [InlineData(true, 25L)]
    [InlineData(false, 0L)]
    [InlineData(false, 25L)]
    public void TwoControlRowsForOnePlace_TheOtherControllersRoadWorks_CannotSpendTheLowestControllersSetAside(bool rule, long atS3)
    {
        WorldState w = RivalRoadRig();
        Set(w, S3, Timber, atS3);
        SimConfig cfg = rule ? Cfg : Pre;
        SettlementId[] payers = RoadDevelopmentQuery.PayingSettlements(w, Rival, S1, S3);
        Assert.Equal([S1, S3, S0], payers);                                           // PREMISE: S0 is a rival payer (last)
        Assert.Equal(rule ? [S1, S3] : payers, ConstructionQuery.EmpirePursePlaces(w, cfg, Rival, payers));
        Assert.Equal([S0, S2], ConstructionQuery.EmpirePursePlaces(w, cfg, Player, RoadDevelopmentQuery.PayingSettlements(w, Player)));

        WorldState t1 = BuildAndRivalRoads(cfg).Step(w);
        if (rule)
        {
            Assert.Equal([Row(PlayerPurse, Timber, 40), Row(PlayerPurse, Stone, 20)], Holds(t1));
            Assert.Equal(RoadCharge(atS3, Road), RoadUnits(t1));                      // 0, or 25 of 60: S3's own timber
            Assert.Equal(atS3, RoadUnits(t1));
            Assert.Equal((40L, 20L, 0L), (Of(t1, S0, Timber), Of(t1, S0, Stone), Of(t1, S3, Timber)));
        }
        else
        {
            Assert.Empty(Holds(t1));
            Assert.Equal(RoadCharge(40 + atS3, Road), RoadUnits(t1));                 // batch 3: 40, or 60 (25 + 35 of S0's)
            Assert.Equal((40 + atS3 - RoadUnits(t1), 0L), (Of(t1, S0, Timber), Of(t1, S3, Timber)));
        }
        AssertConserved(t1);
    }

    /// <summary>
    /// T1a ROUND 2 (cont.): in that world the rival's OWN purse is S1 + S3 (EmpirePursePlaces, not S0), so its road works
    /// still subtract what ITS heads set aside. The rival's waiting granary at S1 (turn 0, no builders) sets aside S3's 40
    /// timber and 20 stone in purse 2; the rival's road order then finds 40 − 40 = 0 timber and pays nothing, and S0's units
    /// (purse 1's) are untouched. Purse 2's membership must use the same rule: counted over every place the rival has a row
    /// for (S0 included) it would not lie within the payers [S1, S3], and its set-aside would go unsubtracted. PREMISE: batch 3
    /// (PreReservations) sets nothing aside and pays 60 — S3's 40, then 20 of S0's.
    /// </summary>
    [Fact]
    public void TwoControlRowsForOnePlace_TheOtherControllersRoadWorks_StillRespectTheirOwnPursesSetAside()
    {
        WorldState w = RivalRoadRig();
        Head(w, S1, 0, Granary);
        NoBuilders(w, S1);
        Set(w, S3, Timber, 40);
        Set(w, S3, Stone, 20);

        WorldState pre = BuildAndRivalRoads(Pre).Step(w);
        Assert.Equal((60L, 0L, 20L), (RoadUnits(pre), Of(pre, S3, Timber), Of(pre, S0, Timber)));

        WorldState t1 = BuildAndRivalRoads(Cfg).Step(w);
        Assert.Equal([Row(PlayerPurse, Timber, 40), Row(PlayerPurse, Stone, 20), Row(RivalPurse, Timber, 40), Row(RivalPurse, Stone, 20)],
            Holds(t1));
        Assert.Equal(0L, RoadUnits(t1));
        Assert.Equal((40L, 40L, 20L, 20L), (Of(t1, S0, Timber), Of(t1, S3, Timber), Of(t1, S0, Stone), Of(t1, S3, Stone)));
        AssertConserved(t1);
    }

    /// <summary>
    /// T1a ROUND 2 (cont.): THE SPLIT'S PURSE IS THE ROAD WORKS' PURSE. The building/roads split sums the Empire's holding S
    /// over the same places its road works pay from (ConstructionQuery.AllocationFor → EmpirePursePlaces), so D_r, the road
    /// guarantee min(D_r, E_r) and the road forecast count only what this Empire's road works can draw. The rival (split b = 50)
    /// has a granary at S1 with builders and 25 timber, 100 stone at S3; S0 (purse 1's alone) holds 40 timber the player's head
    /// sets aside; the rival orders its road S1–S3 (60 timber). Timber, by hand: S = 25 (S0 is not the rival's); D_b = 25 (the
    /// granary draws 25 of 40 with no split: p' = 625,000); D_r = 25; E_b = floor(25 × 50 / 100) = 12, E_r = 13; G_b = min(25,
    /// 12 + max(0, 13 − 25)) = 12; the guarantee min(25, 13) = 13. The step: the granary draws 12 (p' = floor(12N/40) =
    /// 300,000), and road works find 13 and are charged 13 — the forecast exactly. MEASURED with the road payers fixed but S
    /// still summed over every place the rival has a row for (r2/probe-split-before.log): S 65, G_b 25, guarantee 33, forecast
    /// 40, and road works paid 0 — the guarantee broken. PREMISE: batch 3 (PreReservations) counts S0 for the rival (S 65,
    /// road 33 = its forecast).
    /// </summary>
    [Fact]
    public void TwoControlRowsForOnePlace_UnderASplit_TheForecastAndTheRoadGuaranteeCountOnlyTheEmpiresOwnPlaces()
    {
        WorldState w = RivalRoadRig();
        Head(w, S1, 1, Granary);
        Set(w, S3, Timber, 25);
        Set(w, S3, Stone, 100);
        w.ResourceAllocations.Add(new ResourceAllocationRow(Rival, 50));
        int rival = -1;
        for (int i = 0; i < w.Polities.Count; i++) if (w.Polities[i].Id == Rival) rival = i;
        OrderRecord[] orders = [RivalRoads(0)];
        EmpireAllocation Forecast(SimConfig cfg) => ConstructionQuery.StepAllocations(w, cfg, w.ConstructionQueue, w.ConstructionProgress,
            w.GoodStocks, w.ResourceAllocations, orders, 10.0)![rival]!;

        EmpireAllocation pre = Forecast(Pre);                                        // PREMISE: batch 3 counts S0 for the rival
        WorldState preNext = BuildAndRivalRoads(Pre).Step(w);
        Assert.Equal((65L, 33L, 33L), (pre.SplitOf(Timber)!.Value.Supply, pre.RoadCharges[Timber], RoadUnits(preNext)));

        EmpireAllocation a = Forecast(Cfg);
        AllocationSplit t = a.SplitOf(Timber)!.Value;
        Assert.Equal((25L, 25L, 25L, 12L, 13L, 12L, 13L), (t.Supply, t.BuildingDemand, t.RoadDemand, t.BuildingShare, t.RoadShare,
            t.BuildingGrant, t.RoadGuarantee));
        Assert.Equal((12L, 13L), (a.BuildingDrawOf(Timber), a.RoadCharges[Timber]));

        WorldState t1 = BuildAndRivalRoads(Cfg).Step(w);
        Assert.Equal((300_000L, 13L, 0L, 40L), (P(t1, S1), RoadUnits(t1), Of(t1, S3, Timber), Of(t1, S0, Timber)));
        Assert.True(RoadUnits(t1) >= t.RoadGuarantee, "road works find at least min(D_r, E_r)");
        Assert.Equal(a.RoadCharges[Timber], RoadUnits(t1));                          // the forecast is the step
        AssertConserved(t1);
    }

    /// <summary>
    /// T1a ROUND 2 FIX (2026-10-17; finding T1a-r2-L1-T1a-r2-V1-F1): THE DONORS, NOT ONLY THE CHARGE. Road works COUNT and
    /// DRAW through the same filtered list (ConstructionQuery.EmpirePursePlaces): the payers whose holding the charge is
    /// computed on, and the donors the charged units leave, nearest-first. In the tests above the route is S1–S3, so the
    /// doubly-controlled S0 is the rival's LAST unfiltered payer and the charge — capped by S1 + S3's holding — is used up
    /// before a draw could reach it: a draw from the UNFILTERED list passed them all (MEASURED: mutant N3). Here the rival's
    /// route S0–S1 ENDS at S0, so S0 is its FIRST unfiltered donor ([S0, S1, S3]: S0 and S1 at distance 0, ties by id). S1 and
    /// S3 hold <paramref name="atS1"/> and <paramref name="atS3"/> timber (60 in all); the player's waiting granary at S2 sets
    /// aside S0's 40 timber and 20 stone in purse 1. Rule on: road works charge 60 — exactly S1 + S3's holding — and draw it
    /// from S1, then S3; S0 keeps all 40, and purse 1 publishes (1, timber, 40), (1, stone, 20). A draw from the unfiltered
    /// list would take S0's 40 first — (S0, S1, S3) = (0, 10, 30) with 30 / 30, (0, 0, 40) with 0 / 60 — while purse 1 still
    /// published 40 there. Rule off (PreReservations, batch 3): nothing is set aside, the donors keep S0, first, and it pays
    /// 40, then S1, then S3 — exactly those figures.
    /// </summary>
    [Theory]
    // rule, S1 and S3 timber before; S0, S1, S3 timber after (by hand above)
    [InlineData(true, 30L, 30L, 40L, 0L, 0L)]
    [InlineData(true, 0L, 60L, 40L, 0L, 0L)]
    [InlineData(false, 30L, 30L, 0L, 10L, 30L)]
    [InlineData(false, 0L, 60L, 0L, 0L, 40L)]
    public void TwoControlRowsForOnePlace_ARouteEndingAtThePlace_TheOtherControllersRoadWorksDrawOnlyFromTheirOwnPlaces(
        bool rule, long atS1, long atS3, long s0After, long s1After, long s3After)
    {
        WorldState w = RivalRoadRig(S0, S1);
        Set(w, S1, Timber, atS1);
        Set(w, S3, Timber, atS3);
        SimConfig cfg = rule ? Cfg : Pre;
        SettlementId[] payers = RoadDevelopmentQuery.PayingSettlements(w, Rival, S0, S1);
        Assert.Equal([S0, S1, S3], payers);                                           // PREMISE: S0 is the rival's FIRST donor
        Assert.Equal(rule ? [S1, S3] : payers, ConstructionQuery.EmpirePursePlaces(w, cfg, Rival, payers));

        WorldState t1 = BuildAndRivalRoads(cfg).Step(w);
        ConstructionHoldRow[] held = rule ? [Row(PlayerPurse, Timber, 40), Row(PlayerPurse, Stone, 20)] : [];
        Assert.Equal(held, Holds(t1));
        Assert.Equal(Road, RoadUnits(t1));                                            // 60 in every row: S1 + S3 hold exactly 60
        Assert.Equal((s0After, s1After, s3After), (Of(t1, S0, Timber), Of(t1, S1, Timber), Of(t1, S3, Timber)));
        Assert.Equal(20L, Of(t1, S0, Stone));                                         // the trackway costs no stone
        AssertConserved(t1);
    }

    /// <summary>
    /// T1a ROUND 2 FIX (2026-10-17; finding T1a-r2-L1-T1a-r2-V1-F2): THE EMPTY BOUNDARY — an Empire EVERY one of whose places
    /// is another Empire's purse. The Duo world (S0, S2 the player's; S1, S3 the rival's) with the PLAYER's control rows added
    /// on S1 and S3: every place the rival has a row for is keyed to purse 1 (its lowest controller, the player), so with the
    /// rule on EmpirePursePlaces leaves the rival NO payer, no donor and no split purse — never the unfiltered places
    /// (MEASURED: mutant N1, "an empty result falls back to the unfiltered places", passed every test above). The player's
    /// waiting granary at S2 (turn 0, no builders) sets aside S3's 40 timber and 20 stone in purse 1; the rival orders its road
    /// S1–S3 (60 timber) in each of two steps. Rule on: road works charge 0 in both steps, S3 keeps 40 / 20, and purse 1
    /// publishes (1, timber, 40), (1, stone, 20) after each; with a rival split row (b = 50) the rival's split is null — no
    /// purse to split, so no road forecast and no grant — and that step charges 0 too. Under N1 road works would charge 40
    /// (S3's 40, purse 1's published set-aside) and the split would forecast it. Rule off (PreReservations, batch 3): the
    /// payers are S1 and S3, the first step charges RoadCharge(40, 60) = 40 from S3 and the second nothing; the split counts
    /// S = 40 and forecasts those 40.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EveryPlaceOfAnEmpireIsAnotherEmpiresPurse_ItsRoadWorksAndItsSplitDrawNothing_TheSetAsideStands(bool rule)
    {
        WorldState w = Duo();
        w.Controls.Add(new ControlRow(Player, S1, 1.0));
        w.Controls.Add(new ControlRow(Player, S3, 1.0));
        w.TransportEdges.Clear();
        w.TransportEdges.Add(new TransportEdgeRow(1, S1, S3, EdgeTypes.DirtPath, TransportModes.Road, TransportEdgeStates.Complete,
            100_000, Road, 0, 0, 0, EdgeTypes.DirtPath, 0.0, 1.0));
        w.SettlementDistances.Clear();
        w.SettlementDistances.Add(new SettlementDistanceRow(S1, S3, 100.0));
        w.SettlementDistances.Add(new SettlementDistanceRow(S3, S1, 100.0));
        w.TradeFlows.Clear();
        w.ResearchCompleted.Add(new ResearchCompletedRow(Rival, Research.Nodes[Research.IndexOfId("track_road")].Key));
        for (int s = 0; s < w.Settlements.Count; s++) AllBuilders(w, w.Settlements[s].Id);
        Head(w, S2, 0, Granary);
        NoBuilders(w, S2);
        Set(w, S3, Timber, 40);
        Set(w, S3, Stone, 20);
        SimConfig cfg = rule ? Cfg : Pre;
        SettlementId[] payers = RoadDevelopmentQuery.PayingSettlements(w, Rival, S1, S3);
        Assert.Equal([S1, S3], payers);                                               // PREMISE: the rival's rows
        foreach (SettlementId s in payers) Assert.Equal(PlayerPurse, ConstructionQuery.PurseKey(w, Cfg, s));   // both purse 1's

        var exec = new TurnExecutor(ResearchRigs.FlatEra(10.0), [SystemCatalog.Construction(cfg), SystemCatalog.RoadDevelopment(cfg)],
            Log(RivalRoads(0), RivalRoads(1)));
        WorldState t1 = exec.Step(w), t2 = exec.Step(t1);
        long charged = rule ? 0L : RoadCharge(40, Road);                              // 0, or batch 3's 40 of S3's
        ConstructionHoldRow[] held = rule ? [Row(PlayerPurse, Timber, 40), Row(PlayerPurse, Stone, 20)] : [];
        foreach (WorldState t in new[] { t1, t2 })
        {
            Assert.Equal(charged, RoadUnits(t));                                      // the second step adds nothing
            Assert.Equal((40L - charged, 20L), (Of(t, S3, Timber), Of(t, S3, Stone)));
            Assert.Equal(held, Holds(t));
            AssertConserved(t);
        }

        // The split: a rival row at b = 50 and its road order. Rule on: no place of its own, no split.
        WorldState ws = w.Clone();
        ws.ResourceAllocations.Add(new ResourceAllocationRow(Rival, 50));
        int rival = -1;
        for (int i = 0; i < ws.Polities.Count; i++) if (ws.Polities[i].Id == Rival) rival = i;
        OrderRecord[] orders = [RivalRoads(0)];
        EmpireAllocation? split = ConstructionQuery.StepAllocations(ws, cfg, ws.ConstructionQueue, ws.ConstructionProgress,
            ws.GoodStocks, ws.ResourceAllocations, orders, 10.0)![rival];
        WorldState s1 = BuildAndRivalRoads(cfg).Step(ws);
        if (rule) Assert.Null(split);
        else Assert.Equal((40L, 40L), (split!.SplitOf(Timber)!.Value.Supply, split.RoadCharges[Timber]));
        Assert.Equal((charged, 40L - charged), (RoadUnits(s1), Of(s1, S3, Timber)));
        AssertConserved(s1);

        // Why: with the rule on the filter leaves the rival nothing (an empty list, never the unfiltered one).
        Assert.Equal(rule ? [] : payers, ConstructionQuery.EmpirePursePlaces(w, cfg, Rival, payers));
    }

    // ================================================================== the hold invariants name each breach

    /// <summary>The end-of-step hold invariants (ConstructionQuery.HoldsInvariantViolation; asserted by ConstructionSystem)
    /// pass on a real walk and name each breach of a tampered one: (i) a hold above the holding, (ii) draws + holds above the
    /// budget, (iii) a released head that holds, (v) a published table that differs, (vi) an oldest head resolved against
    /// less than the whole budget. ((iv), a negative stock, cannot be built: the Ledger refuses an overdraw.)</summary>
    [Fact]
    public void TheHoldInvariants_PassOnTheWalk_AndNameEachBreach()
    {
        WorldState w = Solo();
        Head(w, S0, 0, Granary, 437_500);
        NoBuilders(w, S0);
        Head(w, S1, 5, Granary);
        AllBuilders(w, S1);
        Set(w, S3, Timber, 22);
        Set(w, S3, Stone, 13);
        HoldsWalkResult walk = Walk(w, Cfg);
        WorldState next = Step(w, Cfg);
        GoodsConfig goods = Cfg.Goods!;
        Assert.Null(ConstructionQuery.HoldsInvariantViolation(walk, goods, next.GoodStocks, next.ConstructionHolds));

        long[][] Copy(long[][] a) { var c = new long[a.Length][]; for (int i = 0; i < a.Length; i++) c[i] = (long[])a[i].Clone(); return c; }
        long[][] held = Copy(walk.Held);
        held[0][Stone] = 14;                              // 2 + 12 > the 13 there
        Assert.Contains("no fictitious stock", ConstructionQuery.HoldsInvariantViolation(walk with { Held = held }, goods, next.GoodStocks, next.ConstructionHolds));
        long[][] drawn = Copy(walk.Drawn);
        drawn[0][Timber] = 1;
        Assert.Contains("no double counting", ConstructionQuery.HoldsInvariantViolation(walk with { Drawn = drawn }, goods, next.GoodStocks, next.ConstructionHolds));
        HoldsWalkHead[] heads = [.. walk.Heads];
        heads[1] = heads[1] with { Release = HoldRelease.NoLabourAdults, Holds = [.. heads[0].Holds] };
        Assert.Contains("released head", ConstructionQuery.HoldsInvariantViolation(walk with { Heads = heads }, goods, next.GoodStocks, next.ConstructionHolds));
        var fewer = new Table<ConstructionHoldRow>();
        fewer.Add(next.ConstructionHolds[0]);
        Assert.Contains("published table has 1 rows", ConstructionQuery.HoldsInvariantViolation(walk, goods, next.GoodStocks, fewer));
        var changed = new Table<ConstructionHoldRow>();
        changed.Add(next.ConstructionHolds[0] with { Units = 21 });
        changed.Add(next.ConstructionHolds[1]);
        Assert.Contains("published row 0", ConstructionQuery.HoldsInvariantViolation(walk, goods, next.GoodStocks, changed));
        HoldsWalkHead[] blocked = [.. walk.Heads];
        long[] less = [.. blocked[0].ResolvedAgainst];
        less[Stone] -= 1;
        blocked[0] = blocked[0] with { ResolvedAgainst = less };
        Assert.Contains("oldest head", ConstructionQuery.HoldsInvariantViolation(walk with { Heads = blocked }, goods, next.GoodStocks, next.ConstructionHolds));
    }
}
