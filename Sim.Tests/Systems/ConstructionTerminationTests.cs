using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Construction;
using Sim.Tests.TestUtil;
using static Sim.Tests.Systems.IncrementalConstructionTests;

namespace Sim.Tests.Systems;

/// <summary>
/// WS1 (D-054, Director item 9) — HOW A QUEUE ENTRY ENDS: completion (exactly once), CANCELLATION (order kind 11: the
/// entry and its progress leave the queue before the step's enqueues and building work; NO REFUND — what went in is
/// forfeited), the CONTROLLER CHANGE convention (the queue stays with the settlement, the new controller's purse pays and
/// only the new controller may cancel), SLOT REUSE (a freed slot never hands forfeited progress to a new entry), and the
/// delivery-time validation of kind 11 (ConstructionQuery.CancelOrderValid — the load-time pass cannot see later control).
/// Rigs as IncrementalConstructionTests (construction only, the dev world, stocks set exactly).
/// </summary>
public class ConstructionTerminationTests
{
    private static WorldState PartBuilt(out TurnExecutor exec, params OrderRecord[] later)
    {
        // S0's granary, 3 stone of 20 in: p = 150,000 after the first step (state 1).
        WorldState w = Solo();
        AllBuilders(w, S0);
        Set(w, S0, Timber, 100);
        Set(w, S0, Stone, 3);
        var all = new List<OrderRecord> { Enqueue(0, Player, S0) };
        all.AddRange(later);
        exec = ConstructionOnly(Cfg, Log([.. all]));
        WorldState t1 = exec.Step(w);
        Assert.Equal(150_000, P(t1, S0));
        return t1;
    }

    // ================================================================== completion, exactly once

    [Fact]
    public void Completion_CountsTheStructureExactlyOnce_RemovesBothRows_AndNothingMoreHappensAfter()
    {
        WorldState w = Solo();
        AllBuilders(w, S0);
        Set(w, S0, Timber, 400);
        Set(w, S0, Stone, 400);
        TurnExecutor exec = ConstructionOnly(Cfg, Log(Enqueue(0, Player, S0)));
        WorldState t1 = exec.Step(w);
        Assert.Equal(1, Built(t1, S0));                                       // labour and stone both ample: one step
        Assert.Equal((0, 0), (t1.ConstructionQueue.Count, t1.ConstructionProgress.Count));
        Assert.Equal((360L, 380L), (Of(t1, S0, Timber), Of(t1, S0, Stone)));
        Assert.Equal(2.0, Labour(t1, S0));                                    // L × (N − 0) / N = 2 exactly
        WorldState t4 = exec.Run(t1, 3);
        Assert.Equal(1, Built(t4, S0));                                       // never counted again
        Assert.Equal((360L, 380L), (Of(t4, S0, Timber), Of(t4, S0, Stone)));
        Assert.Equal(0, t4.ConstructionLabor.Count);
        Assert.Equal(1, t4.Structures.Count);
    }

    [Fact]
    public void TurnExact_EnqueueAtT_FirstProgressInStepTToTPlus1_CompletionInTheStepItIsReached_CancelAtT_GoneInTPlus1()
    {
        // Enqueue stamped 1: state 1 has no entry; state 2 has it WITH progress (enqueued and advanced in the same step).
        WorldState w = Solo();
        AllBuilders(w, S0);
        Set(w, S0, Timber, 100);
        Set(w, S0, Stone, 10);
        TurnExecutor exec = ConstructionOnly(Cfg, Log(Enqueue(1, Player, S0), Cancel(3, Player, S0, 0)));
        WorldState t1 = exec.Step(w);
        Assert.Equal((0, 0), (t1.ConstructionQueue.Count, t1.ConstructionProgress.Count));
        WorldState t2 = exec.Step(t1);
        Assert.Equal(new ConstructionProgressRow(S0, 0, Granary, 1, 500_000), ProgressRow(t2, S0));   // 10 of 20 stone
        WorldState t3 = exec.Step(t2);
        Assert.Equal(500_000, P(t3, S0));                                     // waits for stone
        // Cancel stamped 3: delivered in step 3 → 4, before building work — state 4 holds no entry and nothing was drawn.
        Set(t3, S0, Stone, 20);
        WorldState t4 = exec.Step(t3);
        Assert.Equal((0, 0), (t4.ConstructionQueue.Count, t4.ConstructionProgress.Count));
        Assert.Equal(0, Built(t4, S0));
        Assert.Equal(20, Of(t4, S0, Stone));                                  // the cancelled entry took nothing in its last step
        Assert.Equal(80, Of(t4, S0, Timber));                                 // the 20 timber in (ceil(40 × .5)) are forfeited
    }

    [Fact]
    public void Completion_HappensInTheStepThatReachesN_TurnExact_WithTheLabourOfTheLastPiece()
    {
        WorldState w = Solo();
        AllBuilders(w, S0);
        Set(w, S0, Timber, 100);
        Set(w, S0, Stone, 15);
        TurnExecutor exec = ConstructionOnly(Cfg, Log(Enqueue(0, Player, S0)));
        WorldState t1 = exec.Step(w);
        Assert.Equal(750_000, P(t1, S0));
        Assert.Equal(0, Built(t1, S0));
        Set(t1, S0, Stone, 5);
        WorldState t2 = exec.Step(t1);
        Assert.Equal(1, Built(t2, S0));                                       // completes in the step that reaches N
        Assert.Equal(2.0 * (N - 750_000) / N, Labour(t2, S0));
    }

    // ================================================================== cancellation

    [Fact]
    public void Cancel_ForfeitsWhatWentIn_NoRefund_AndTheNextSlotBecomesHeadWithItsOwnProgress()
    {
        WorldState t1 = PartBuilt(out TurnExecutor exec, Enqueue(1, Player, S0, Granary), Cancel(2, Player, S0, 0));
        WorldState t2 = exec.Step(t1);                                        // slot 1 queued behind; the head waits (no stone)
        ConstructionQueueRow[] q = ConstructionQuery.Queue(t2, S0);
        Assert.Equal([0, 1], [q[0].Slot, q[1].Slot]);
        Assert.Equal(150_000, ConstructionQuery.ProgressOf(t2, q[0]));
        Assert.Equal(0, ConstructionQuery.ProgressOf(t2, q[1]));
        long timber = Of(t2, S0, Timber), sunkT = Sunk(t2, Timber), sunkS = Sunk(t2, Stone);
        WorldState t3 = exec.Step(t2);
        ConstructionQueueRow head = Assert.Single(ConstructionQuery.Queue(t3, S0));
        Assert.Equal(1, head.Slot);
        Assert.Equal(0, ConstructionQuery.ProgressOf(t3, head));              // its own progress, not the forfeited 150,000
        Assert.Equal(timber, Of(t3, S0, Timber));                             // no refund: no source flow, no stock back
        Assert.Equal((sunkT, sunkS), (Sunk(t3, Timber), Sunk(t3, Stone)));
        AssertConserved(t3);
    }

    [Fact]
    public void CancelTheHighestSlot_AndReEnqueueTheSameProjectInOneTurn_StartsAtZero_NeverInheritsForfeitedProgress()
    {
        WorldState t1 = PartBuilt(out TurnExecutor exec, Cancel(1, Player, S0, 0), Enqueue(1, Player, S0));
        WorldState t2 = exec.Step(t1);
        ConstructionQueueRow head = Assert.Single(ConstructionQuery.Queue(t2, S0));
        Assert.Equal((0, Granary), (head.Slot, head.ProjectId));               // the freed slot 0, the same project
        Assert.Equal(new ConstructionProgressRow(S0, 0, Granary, 1, 0), ProgressRow(t2, S0));   // a NEW entry at 0
    }

    [Fact]
    public void CancelSlotZero_AndEnqueueAnother_NothingInherited_NothingThrows()
    {
        WorldState w0 = Solo();
        SimConfig cfg = WithWorks(Cfg, 2.0);
        AllBuilders(w0, S0);
        Set(w0, S0, Timber, 100);
        Set(w0, S0, Stone, 3);
        TurnExecutor exec = ConstructionOnly(cfg, Log(Enqueue(0, Player, S0), Cancel(1, Player, S0, 0), Enqueue(1, Player, S0, Works)));
        WorldState t1 = exec.Step(w0);
        Assert.Equal(150_000, P(t1, S0));
        WorldState t2 = exec.Step(t1);
        ConstructionQueueRow head = Assert.Single(ConstructionQuery.Queue(t2, S0));
        Assert.Equal((0, Works), (head.Slot, head.ProjectId));
        Assert.Equal(new ConstructionProgressRow(S0, 0, Works, 1, 0), ProgressRow(t2, S0));
    }

    [Fact]
    public void CancelAndTwoEnqueues_InOneBatch_TakeSlotsZeroAndOne_EachFromZero()
    {
        WorldState t1 = PartBuilt(out TurnExecutor exec, Cancel(1, Player, S0, 0), Enqueue(1, Player, S0), Enqueue(1, Player, S0));
        WorldState t2 = exec.Step(t1);
        ConstructionQueueRow[] q = ConstructionQuery.Queue(t2, S0);
        Assert.Equal(2, q.Length);
        Assert.Equal([0, 1], [q[0].Slot, q[1].Slot]);
        Assert.Equal([new ConstructionProgressRow(S0, 0, Granary, 1, 0), new ConstructionProgressRow(S0, 1, Granary, 1, 0)],
            [t2.ConstructionProgress[0], t2.ConstructionProgress[1]]);
    }

    [Fact]
    public void ACancelOfASlotNotQueued_ChangesNothing_AndACancelOfAnEntryEnqueuedInTheSameBatch_DoesNotReachIt()
    {
        // Cancels run BEFORE enqueues: a cancel of slot 0 in the batch that enqueues slot 0 finds no slot 0.
        WorldState w = Solo();
        AllBuilders(w, S0);
        TurnExecutor exec = ConstructionOnly(Cfg, Log(Cancel(0, Player, S0, 0), Enqueue(0, Player, S0), Cancel(0, Player, S0, 7)));
        WorldState t1 = exec.Step(w);
        Assert.Single(ConstructionQuery.Queue(t1, S0));
        Assert.Equal(SchemaProjection.Serialize(ConstructionOnly(Cfg, Log(Enqueue(0, Player, S0))).Step(w)), SchemaProjection.Serialize(t1));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(2.5)]
    [InlineData(-1.0)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(2147483648.0)]
    public void AMalformedCancelAppendedInMemory_ChangesNothing_NeverFloorsToTheHead(double slot)
    {
        // OrderLog.Append never validates; the consumer re-checks the payload (a NaN saturating to 0 would cancel the head).
        WorldState t1 = PartBuilt(out _);
        WorldState control = ConstructionOnly(Cfg, Log()).Step(t1);
        WorldState malformed = ConstructionOnly(Cfg, Log(Cancel(1, Player, S0, slot))).Step(t1);
        Assert.Equal(150_000, P(malformed, S0));
        Assert.Equal(SchemaProjection.Serialize(control), SchemaProjection.Serialize(malformed));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(2.5)]
    [InlineData(-1.0)]
    public void OrderLogLoad_RejectsAMalformedCancelPayload(double slot)
    {
        var log = Log(Cancel(0, Player, S0, slot));
        using var ms = new MemoryStream();
        log.Save(ms);
        ms.Position = 0;
        SnapshotFormatException e = Assert.Throws<SnapshotFormatException>(() => OrderLog.Load(ms));
        Assert.Contains("CancelConstruction slot must be a non-negative whole number", e.Message);
        var neg = Log(new OrderRecord(0, 1, OrderKind.CancelConstruction, -1, 0.0));
        using var ms2 = new MemoryStream();
        neg.Save(ms2);
        ms2.Position = 0;
        Assert.Contains("CancelConstruction settlement id must be >= 0", Assert.Throws<SnapshotFormatException>(() => OrderLog.Load(ms2)).Message);
    }

    [Fact]
    public void ACancelOfAWellFormedKind11_RoundTripsThroughTheLog()
    {
        var log = Log(Cancel(3, Player, S2, 4.0), new OrderRecord(4, 1, OrderKind.SetResourceAllocation, 1, 70.0));
        using var ms = new MemoryStream();
        log.Save(ms);
        ms.Position = 0;
        OrderLog back = OrderLog.Load(ms);
        Assert.Equal(log[0], back[0]);
        Assert.Equal(log[1], back[1]);
    }

    // ================================================================== controller change

    [Fact]
    public void ARevolt_TheQueueAndProgressStayWithTheSettlement_TheNewPursePays_AndTheOldControllerCanNoLongerCancel()
    {
        // Duo: S0 and S2 the player's, S1 and S3 the AI's. S0's granary part-built from the player's purse; then S0 passes
        // to the AI (as RevoltSystem/control change writes the relation). The progress continues, paid from the AI's purse;
        // the player's cancel, delivered after the change, is ignored (delivery-time control on PREV).
        WorldState w = Duo();
        AllBuilders(w, S0);
        Set(w, S2, Timber, 100);                                              // the player's purse
        Set(w, S0, Stone, 0);
        Set(w, S2, Stone, 3);
        TurnExecutor exec = ConstructionOnly(Cfg, Log(Enqueue(0, Player, S0), Cancel(1, Player, S0, 0)));
        WorldState t1 = exec.Step(w);
        Assert.Equal(150_000, P(t1, S0));
        Assert.Equal((94L, 0L), (Of(t1, S2, Timber), Of(t1, S2, Stone)));
        // The control change (the relation alone — the queue belongs to the settlement).
        for (int i = 0; i < t1.Controls.Count; i++)
            if (t1.Controls[i].Place == S0) t1.Controls[i] = t1.Controls[i] with { Polity = Rival };
        Set(t1, S3, Timber, 100);                                             // the AI's purse
        Set(t1, S3, Stone, 20);
        Set(t1, S2, Stone, 50);                                               // the player still holds stone: never touched
        WorldState t2 = exec.Step(t1);
        Assert.Equal(1, Built(t2, S0));                                       // the player's cancel was ignored
        Assert.Equal((100L - 34, 20L - 17), (Of(t2, S3, Timber), Of(t2, S3, Stone)));   // the remaining 34 + 17 from the AI's purse
        Assert.Equal((94L, 50L), (Of(t2, S2, Timber), Of(t2, S2, Stone)));
    }

    [Fact]
    public void AfterAControllerChange_TheNewControllerMayCancel()
    {
        WorldState w = Duo();
        AllBuilders(w, S0);
        Set(w, S2, Timber, 100);
        Set(w, S2, Stone, 3);
        TurnExecutor exec = ConstructionOnly(Cfg, Log(Enqueue(0, Player, S0), Cancel(1, Rival, S0, 0)));
        WorldState t1 = exec.Step(w);
        for (int i = 0; i < t1.Controls.Count; i++)
            if (t1.Controls[i].Place == S0) t1.Controls[i] = t1.Controls[i] with { Polity = Rival };
        WorldState t2 = exec.Step(t1);
        Assert.Empty(ConstructionQuery.Queue(t2, S0));
        Assert.Equal(0, t2.ConstructionProgress.Count);
    }

    // ================================================================== slot reuse

    [Fact]
    public void SlotReuse_CompleteThenReEnqueueAtSlotZero_StartsAtZero()
    {
        WorldState w = Solo();
        AllBuilders(w, S0);
        Set(w, S0, Timber, 40);
        Set(w, S0, Stone, 20);
        TurnExecutor exec = ConstructionOnly(Cfg, Log(Enqueue(0, Player, S0), Enqueue(1, Player, S0)));
        WorldState t1 = exec.Step(w);
        Assert.Equal(1, Built(t1, S0));
        WorldState t2 = exec.Step(t1);                                        // slot 0 again; no materials left
        ConstructionQueueRow head = Assert.Single(ConstructionQuery.Queue(t2, S0));
        Assert.Equal(0, head.Slot);
        Assert.Equal(new ConstructionProgressRow(S0, 0, Granary, 1, 0), ProgressRow(t2, S0));
        Assert.Equal(1, Built(t2, S0));
    }

    [Fact]
    public void AStaleProgressRowNamingAnotherProject_IsABug_TheResolverThrows_AndLoadRefusesIt()
    {
        WorldState w = Solo();
        AllBuilders(w, S0);
        w.ConstructionQueue.Add(new ConstructionQueueRow(S0, 0, Granary));
        w.ConstructionProgress.Add(new ConstructionProgressRow(S0, 0, Works, 0, 10));
        Assert.Throws<InvalidOperationException>(() =>
            ConstructionQuery.ResolveHead(w, Cfg, S0, w.ConstructionQueue, w.ConstructionProgress, w.GoodStocks, 10.0, null, strict: true));
        // A query reads the mismatched row as p = 0.
        Assert.Equal(0, ConstructionQuery.ProgressOf(w, w.ConstructionQueue[0]));
        using var ms = new MemoryStream();
        Snapshot.Save(w, ms);
        ms.Position = 0;
        SnapshotFormatException e = Assert.Throws<SnapshotFormatException>(() => Snapshot.Load(ms, w.Terrain));
        Assert.Contains("has no matching queue entry", e.Message);
    }

    /// <summary>The step's end-of-step invariant is the same statement Snapshot.Load applies (ConstructionQuery
    /// .ProgressInvariantViolation): a progress of N left in a table is a state no rule produces — completion removes the
    /// entry in the step that reaches N — and the step throws rather than carry a head that can never complete.</summary>
    [Fact]
    public void AProgressOfNLeftInTheTable_IsABug_TheStepsInvariantThrows_NamingTheRange()
    {
        WorldState w = Solo();
        AllBuilders(w, S0);
        w.ConstructionQueue.Add(new ConstructionQueueRow(S0, 0, Granary));
        w.ConstructionProgress.Add(new ConstructionProgressRow(S0, 0, Granary, 0, N));
        InvalidOperationException e = Assert.Throws<InvalidOperationException>(() => ConstructionOnly(Cfg, Log()).Step(w));
        Assert.Contains("outside [0, 999999]", e.Message);
    }

    // ================================================================== cannot proceed: the two content-shaped stalls

    private static readonly int Tools = Cfg.Goods!.IdOf("tools");
    private const int Workshop = 2;          // goods.json: 60 timber, 40 stone, 10 tools, 8 adult-years
    private const int Unknown = 77;          // no goods.json project has this id

    /// <summary>
    /// Fix round 1 (verifier WS1-TERM, 2026-10-15) — A HEAD STALLED ON AN UNOBTAINABLE CRAFTED INPUT. MEASURED before this
    /// test: no test named HeadLimitKind.Unobtainable, and the mutant that reports every materials stall as plain Materials
    /// (ConstructionQuery.Incremental.cs, `kind = HeadLimitKind.Materials`) survived the construction-focused filter. The
    /// rule (D-054 "cannot proceed"): the head WAITS and keeps its progress, nothing is drawn, the stall is STRUCTURAL, and
    /// the AI cancels its own structurally stalled head so its queue and its remaining-needs claims do not deadlock (it
    /// orders only into idle queues). Duo world: the AI holds S1 and S3; S1's workshop is 30 % in; the purse holds the
    /// timber and stone but no tools, and nothing in the Empire can make tools at turn 0 (toolmaking needs bronze, an
    /// Age III chain). The control: one tool in the purse — short, not unobtainable — advances and is not cancelled.
    /// </summary>
    [Fact]
    public void AHeadStalledOnAnUnobtainableCraftedInput_WaitsKeepingItsProgress_IsStructural_AndTheAiCancelsIt()
    {
        WorldState w = Duo();
        w.ConstructionQueue.Add(new ConstructionQueueRow(S1, 0, Workshop));
        w.ConstructionProgress.Add(new ConstructionProgressRow(S1, 0, Workshop, 0, 300_000));
        AllBuilders(w, S1);
        Set(w, S1, Timber, 200);
        Set(w, S1, Stone, 200);
        Set(w, S1, Tools, 0);
        Set(w, S3, Tools, 0);

        HeadResolution r = ConstructionQuery.ResolveHead(w, Cfg, S1, w.ConstructionQueue, w.ConstructionProgress, w.GoodStocks, 10.0, null)!;
        Assert.Equal((HeadLimitKind.Unobtainable, Tools, 300_000L, 300_000L), (r.Limit, r.LimitGood, r.ProgressBefore, r.ProgressAfter));
        Assert.Equal([0L, 0L, 0L], r.Draws);
        Assert.True(r.StructurallyStalled);
        Assert.Same(Cfg.Goods!.ProjectById(Workshop), ConstructionQuery.StructuralStall(w, Cfg, S1, 10.0)!.Project);

        // The system: the head waits, keeps p, draws nothing.
        WorldState kept = ConstructionOnly(Cfg, Log()).Step(w);
        Assert.Equal(new ConstructionProgressRow(S1, 0, Workshop, 0, 300_000), ProgressRow(kept, S1));
        Assert.Equal((200L, 200L), (Of(kept, S1, Timber), Of(kept, S1, Stone)));
        AssertConserved(kept);

        // The AI cancels its own structurally stalled head; delivered, the entry and its progress leave (no refund: the
        // purse is what it was — the 30 % that went in stays sunk). Delivered alone: the AI also orders a granary into its
        // idle S3 queue (paid from the same purse), which is not this test's subject.
        OrderRecord[] orders = AiConstructionPolicy.Decide(w, Cfg, Rival, 10.0);
        Assert.Contains(ConstructionQuery.CancelOrder(w, Rival, S1, 0), orders);
        WorldState cancelled = ConstructionOnly(Cfg, Log(ConstructionQuery.CancelOrder(w, Rival, S1, 0))).Step(w);
        Assert.DoesNotContain(cancelled.ConstructionQueue.Rows(), q => q.Settlement == S1 && q.ProjectId == Workshop);
        Assert.DoesNotContain(cancelled.ConstructionProgress.Rows(), p => p.Settlement == S1);
        Assert.Equal((200L, 200L), (Of(cancelled, S1, Timber), Of(cancelled, S1, Stone)));
        AssertConserved(cancelled);

        // Control: one tool in the purse — the input is short, not unobtainable. The head advances (to the edge of its
        // fourth tool: p = 400,000) and the AI keeps it.
        Set(w, S3, Tools, 1);
        HeadResolution shortOne = ConstructionQuery.ResolveHead(w, Cfg, S1, w.ConstructionQueue, w.ConstructionProgress, w.GoodStocks, 10.0, null)!;
        Assert.Equal((HeadLimitKind.Materials, 400_000L, false), (shortOne.Limit, shortOne.ProgressAfter, shortOne.StructurallyStalled));
        Assert.DoesNotContain(AiConstructionPolicy.Decide(w, Cfg, Rival, 10.0), o => ConstructionQuery.IsCancel(o.Kind));   // D-056: either cancel kind
    }

    /// <summary>
    /// Fix round 1 (verifier WS1-TERM) — A HEAD WHOSE PROJECT LEFT THE CONTENT (goods.json no longer defines its id: data
    /// changed under a saved queue). MEASURED before this test: no test named HeadLimitKind.UnknownProject, and the mutant
    /// that reports it as a non-structural Labour wait survived. The rule: it waits and keeps its progress, draws nothing,
    /// is STRUCTURAL, and the AI cancels its own — never the player's (the player sees "no longer known" and may cancel).
    /// </summary>
    [Fact]
    public void AHeadWhoseProjectLeftTheContent_WaitsKeepingItsProgress_IsStructural_AndTheAiCancelsOnlyItsOwn()
    {
        WorldState w = Duo();
        Assert.Null(Cfg.Goods!.ProjectById(Unknown));
        w.ConstructionQueue.Add(new ConstructionQueueRow(S1, 0, Unknown));
        w.ConstructionProgress.Add(new ConstructionProgressRow(S1, 0, Unknown, 0, 250_000));
        w.ConstructionQueue.Add(new ConstructionQueueRow(S0, 0, Unknown));
        w.ConstructionProgress.Add(new ConstructionProgressRow(S0, 0, Unknown, 0, 250_000));
        AllBuilders(w, S1);
        AllBuilders(w, S0);
        Set(w, S1, Timber, 200);
        Set(w, S1, Stone, 200);

        HeadResolution r = ConstructionQuery.ResolveHead(w, Cfg, S1, w.ConstructionQueue, w.ConstructionProgress, w.GoodStocks, 10.0, null)!;
        Assert.Equal((HeadLimitKind.UnknownProject, (ConstructionProjectEntry?)null, 250_000L, 250_000L, 0.0),
            (r.Limit, r.Project, r.ProgressBefore, r.ProgressAfter, r.LaborUsed));
        Assert.True(r.StructurallyStalled);
        Assert.NotNull(ConstructionQuery.StructuralStall(w, Cfg, S1, 10.0));

        WorldState kept = ConstructionOnly(Cfg, Log()).Step(w);
        Assert.Equal(new ConstructionProgressRow(S1, 0, Unknown, 0, 250_000), ProgressRow(kept, S1));
        Assert.Equal(new ConstructionProgressRow(S0, 0, Unknown, 0, 250_000), ProgressRow(kept, S0));
        Assert.Equal((200L, 200L), (Of(kept, S1, Timber), Of(kept, S1, Stone)));
        Assert.Equal(0.0, Labour(kept, S1));
        AssertConserved(kept);

        OrderRecord[] orders = AiConstructionPolicy.Decide(w, Cfg, Rival, 10.0);
        Assert.Contains(ConstructionQuery.CancelOrder(w, Rival, S1, 0), orders);
        Assert.DoesNotContain(orders, o => ConstructionQuery.IsCancel(o.Kind) && o.TargetId == S0.Value);   // the player's
        WorldState cancelled = ConstructionOnly(Cfg, Log(orders)).Step(w);
        Assert.Empty(ConstructionQuery.Queue(cancelled, S1));
        Assert.DoesNotContain(cancelled.ConstructionProgress.Rows(), p => p.Settlement == S1);
        Assert.Equal(new ConstructionProgressRow(S0, 0, Unknown, 0, 250_000), ProgressRow(cancelled, S0));
        AssertConserved(cancelled);
    }

    // ================================================================== the roster arm of the cancel predicate

    /// <summary>
    /// C1 review F4d: with a roster but no control relation (Polities non-empty, Controls empty — the control arm is guarded
    /// on a non-empty relation and checks nothing), the ROSTER arm alone decides who may cancel: an id outside the roster
    /// changes nothing, byte for byte; the roster Empire's cancel removes the entry and its progress.
    /// </summary>
    [Fact]
    public void WithARosterButNoControlRelation_OnlyARosterEmpiresCancelIsHonoured()
    {
        WorldState t1 = PartBuilt(out _);
        t1.Controls.Clear();
        Assert.True(t1.Polities.Count > 0);
        var stranger = new PolityId(99);
        Assert.False(EmpireQuery.TryGetCommandSource(t1, stranger, out _));
        Assert.False(ConstructionQuery.CancelOrderValid(t1, Cancel(1, stranger, S0, 0)));
        Assert.True(ConstructionQuery.CancelOrderValid(t1, Cancel(1, Player, S0, 0)));

        WorldState control = ConstructionOnly(Cfg, Log()).Step(t1);
        WorldState foreign = ConstructionOnly(Cfg, Log(Cancel(1, stranger, S0, 0))).Step(t1);
        Assert.Single(ConstructionQuery.Queue(foreign, S0));
        Assert.Equal(SchemaProjection.Serialize(control), SchemaProjection.Serialize(foreign));
        WorldState own = ConstructionOnly(Cfg, Log(Cancel(1, Player, S0, 0))).Step(t1);
        Assert.Empty(ConstructionQuery.Queue(own, S0));
        Assert.Equal(0, own.ConstructionProgress.Count);
    }
}
