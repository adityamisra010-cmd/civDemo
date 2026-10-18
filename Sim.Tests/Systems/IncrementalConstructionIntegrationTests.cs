using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Construction;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;
using static Sim.Tests.Systems.IncrementalConstructionTests;

namespace Sim.Tests.Systems;

/// <summary>
/// WS1 (D-054) — INCREMENTAL CONSTRUCTION WITH THE REST OF THE WORLD: save/load continuity, replay determinism, the claims
/// (remaining needs), the AI (structural-stall cancel, its ordering guard, the university check), the one resolver and the
/// UI's estimate agreeing, and the batch-2 systems — a part-built granary is not a granary (no farm or famine factor, no Age
/// II milestone) until the step it completes; stone PRODUCTION earlier in the same step and stone TRADE earlier in the same
/// step both reach a waiting head in that step.
///
/// THE STEP (pipeline.json): catchment, harvestweather, disaster, PRODUCTION (4), appropriation, consumption, price, TRADE
/// (8), housing, CONSTRUCTION (10), …, institutions — all on the step's NEXT GoodStocks, so what production and trade put in
/// the purse earlier in the step is what construction resolves on.
/// </summary>
public class IncrementalConstructionIntegrationTests
{
    // ================================================================== persistence and determinism

    [Fact]
    public void SaveMidProgress_LoadAndContinue_EveryTurnHashEqualsTheUninterruptedRun()
    {
        WorldState w = Solo();
        AllBuilders(w, S0);
        double c = C(w, S0, 10.0);
        SimConfig cfg = WithWorks(Cfg, 4.0 * c);
        Set(w, S0, Timber, 100);
        Set(w, S0, Stone, 100);
        TurnExecutor exec = ConstructionOnly(cfg, Log(Enqueue(0, Player, S0, Works), Enqueue(1, Player, S0)));
        WorldState t1 = exec.Step(w);
        Assert.Equal(250_000, P(t1, S0));

        var straight = new List<string>();
        WorldState a = t1;
        for (int k = 0; k < 5; k++) { a = exec.Step(a); straight.Add(WorldHash.ComputeHex(a)); }

        using var ms = new MemoryStream();
        Snapshot.Save(t1, ms);
        ms.Position = 0;
        WorldState b = Snapshot.Load(ms, t1.Terrain);
        Assert.Equal(WorldHash.ComputeHex(t1), WorldHash.ComputeHex(b));
        Assert.Equal(t1.ConstructionProgress[0], b.ConstructionProgress[0]);
        for (int k = 0; k < 5; k++)
        {
            b = exec.Step(b);
            Assert.Equal(straight[k], WorldHash.ComputeHex(b));
        }
        Assert.Equal(1, Built(b, S0, Works));
        Assert.Equal(1, Built(b, S0));
    }

    [Fact]
    public void Replay_TheFullPipelineWithEnqueuesAndCancels_IsHashIdentical_AndSurvivesTheLogOnDisk()
    {
        WorldState w0 = WorldFounding.Found(TestConfigs.DevWorldgen(), Cfg, 42);
        OrderLog log = Log(Enqueue(0, Player, S0), Enqueue(0, Player, S1), Enqueue(1, Player, S1), Cancel(2, Player, S1, 1), Enqueue(3, Player, S2));
        using var ms = new MemoryStream();
        log.Save(ms);
        ms.Position = 0;
        OrderLog loaded = OrderLog.Load(ms);
        TurnExecutor a = UniversityRigs.Production(Cfg, log), b = UniversityRigs.Production(Cfg, loaded);
        WorldState x = w0, y = w0;
        for (int t = 0; t < 8; t++)
        {
            x = a.Step(x);
            y = b.Step(y);
            Assert.Equal(WorldHash.ComputeHex(x), WorldHash.ComputeHex(y));
        }
    }

    // ================================================================== claims

    [Fact]
    public void Claims_AreEachEntrysRemainingNeeds_NotItsWholeCost()
    {
        WorldState w = Solo();
        w.ConstructionQueue.Add(new ConstructionQueueRow(S0, 0, Granary));
        w.ConstructionProgress.Add(new ConstructionProgressRow(S0, 0, Granary, 0, 500_000));
        w.ConstructionQueue.Add(new ConstructionQueueRow(S1, 0, Granary));     // no progress row: p = 0
        SettlementId[] purse = ConstructionQuery.Holders(w, Cfg, S0);
        Assert.Equal(20 + 40, ConstructionQuery.Claimed(w.ConstructionQueue, w.ConstructionProgress, Cfg.Goods!, purse, new GoodId(Timber)));
        Assert.Equal(10 + 20, ConstructionQuery.Claimed(w.ConstructionQueue, w.ConstructionProgress, Cfg.Goods!, purse, new GoodId(Stone)));
        Assert.Equal(80, ConstructionQuery.Claimed(w.ConstructionQueue, Cfg.Goods!, purse, new GoodId(Timber)));   // the progress-less reading
        Set(w, S2, Timber, 70);
        Assert.Equal(10, ConstructionQuery.Unclaimed(w.GoodStocks, w.ConstructionQueue, w.ConstructionProgress, Cfg.Goods!, purse, new GoodId(Timber)));
    }

    // ================================================================== the AI

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
        Assert.Equal(0, BandViews.Adults(w.Buckets, s));
    }

    [Fact]
    public void TheAi_CancelsItsOwnStructurallyStalledHead_AndItsOtherSettlementStillOrdersAndBuilds()
    {
        // Duo: the AI holds S1 and S3. S1's granary is part-built when S1 loses every working adult.
        WorldState w = Duo();
        w.ConstructionQueue.Add(new ConstructionQueueRow(S1, 0, Granary));
        w.ConstructionProgress.Add(new ConstructionProgressRow(S1, 0, Granary, 0, 300_000));
        NoAdults(w, S1);
        Set(w, S3, Timber, 200);
        Set(w, S3, Stone, 200);
        AllBuilders(w, S3);
        HeadResolution stalled = ConstructionQuery.StructuralStall(w, Cfg, S1, 10.0)!;
        Assert.Equal((HeadLimitKind.Labour, true), (stalled.Limit, stalled.NoLabourAdults));

        OrderRecord[] orders = AiConstructionPolicy.Decide(w, Cfg, Rival, 10.0);
        Assert.Contains(ConstructionQuery.CancelOrder(w, Rival, S1, 0), orders);
        Assert.Contains(ConstructionQuery.EnqueueOrder(w, Rival, S3, Granary), orders);
        Assert.DoesNotContain(orders, o => o.Kind == OrderKind.EnqueueConstruction && o.TargetId == S1.Value);

        WorldState next = ConstructionOnly(Cfg, Log(orders)).Step(w);
        Assert.Empty(ConstructionQuery.Queue(next, S1));
        Assert.Equal(1, Built(next, S3));
        // The switch off: the batch-2 AI — no cancel (it never stalls structurally under M4-D's model of the queue).
        Assert.DoesNotContain(AiConstructionPolicy.Decide(w, TestConfigs.PreIncrementalConstruction(Cfg), Rival, 10.0),
            o => o.Kind == OrderKind.CancelConstruction);
    }

    [Fact]
    public void TheAi_OrdersNothingIntoASettlementWithNoLabourAdults_WhichWouldStallAtOnce()
    {
        WorldState w = Duo();
        Set(w, S3, Timber, 200);
        Set(w, S3, Stone, 200);
        NoAdults(w, S1);
        OrderRecord[] orders = AiConstructionPolicy.Decide(w, Cfg, Rival, 10.0);
        Assert.DoesNotContain(orders, o => o.TargetId == S1.Value);
        Assert.Contains(orders, o => o.TargetId == S3.Value && o.Kind == OrderKind.EnqueueConstruction);
        Assert.Contains(AiConstructionPolicy.Decide(w, TestConfigs.PreIncrementalConstruction(Cfg), Rival, 10.0),
            o => o.TargetId == S1.Value);                                   // batch 2 ordered there
    }

    /// <summary>
    /// C1 review F4c: a Labour wait is STRUCTURAL only when the settlement has no labour adults at all. A settlement whose
    /// adults have no construction share waits for its owner to give Building a share — the AI keeps that head and what went
    /// into it; the same head with no labour adults is structurally stalled and the AI cancels it.
    /// </summary>
    [Fact]
    public void TheAi_KeepsAHeadThatWaitsOnlyForAConstructionShare_ItCancelsOnlyWhenNoAdultCouldBuild()
    {
        WorldState w = Duo();
        w.ConstructionQueue.Add(new ConstructionQueueRow(S1, 0, Granary));
        w.ConstructionProgress.Add(new ConstructionProgressRow(S1, 0, Granary, 0, 300_000));
        NoBuilders(w, S1);
        Assert.True(InstitutionStaffing.LabourAdults(w, Cfg, S1) > 0.0, "anti-vacuity: S1 has labour adults");
        HeadResolution r = ConstructionQuery.ResolveHead(w, Cfg, S1, w.ConstructionQueue, w.ConstructionProgress, w.GoodStocks, 10.0, null)!;
        Assert.Equal((HeadLimitKind.Labour, false, false), (r.Limit, r.NoLabourAdults, r.StructurallyStalled));
        Assert.Null(ConstructionQuery.StructuralStall(w, Cfg, S1, 10.0));
        Assert.DoesNotContain(AiConstructionPolicy.Decide(w, Cfg, Rival, 10.0), o => o.Kind == OrderKind.CancelConstruction);

        NoAdults(w, S1);
        Assert.True(ConstructionQuery.StructuralStall(w, Cfg, S1, 10.0) is { Limit: HeadLimitKind.Labour, NoLabourAdults: true });
        Assert.Contains(ConstructionQuery.CancelOrder(w, Rival, S1, 0), AiConstructionPolicy.Decide(w, Cfg, Rival, 10.0));
    }

    /// <summary>
    /// C1 review F4b: the AI's RESERVED PURSE reserves each queued entry's REMAINING needs — what has gone in is sunk, not
    /// counted twice. S1's granary is half in (20 timber and 10 stone still to go) and the AI's purse (S3 and S1) holds 60
    /// timber and 30 stone: that covers a new granary at S3 only once S1's delivered half is discounted (60 − 20 = 40,
    /// 30 − 10 = 20). With nothing in yet, S1's entry claims its whole 40 and 20, leaving 20 and 10: no order.
    /// </summary>
    [Fact]
    public void TheAisReservedPurse_ReservesEachQueuedEntrysRemainingNeeds_NotItsWholeCost()
    {
        WorldState w = Duo();
        w.ConstructionQueue.Add(new ConstructionQueueRow(S1, 0, Granary));
        w.ConstructionProgress.Add(new ConstructionProgressRow(S1, 0, Granary, 0, 500_000));
        Set(w, S3, Timber, 60);
        Set(w, S3, Stone, 30);
        Assert.Contains(ConstructionQuery.EnqueueOrder(w, Rival, S3, Granary), AiConstructionPolicy.Decide(w, Cfg, Rival, 10.0));

        w.ConstructionProgress[0] = w.ConstructionProgress[0] with { Progress = 0 };
        Assert.DoesNotContain(AiConstructionPolicy.Decide(w, Cfg, Rival, 10.0),
            o => o.Kind == OrderKind.EnqueueConstruction && o.TargetId == S3.Value);
        // One unit short of the remaining-needs reading at half built: no order either (the bound is exact).
        w.ConstructionProgress[0] = w.ConstructionProgress[0] with { Progress = 500_000 };
        Set(w, S3, Stone, 29);
        Assert.DoesNotContain(AiConstructionPolicy.Decide(w, Cfg, Rival, 10.0),
            o => o.Kind == OrderKind.EnqueueConstruction && o.TargetId == S3.Value);
    }

    /// <summary>
    /// The AI's structural-stall cancel for a VIABILITY wait, AS D-054 SPECIFIES IT (WS1 "Abandoned": Labour with no adults,
    /// Viability, Unobtainable, UnknownProject). Pinned so the open Director question raised at the C1 review (F2: should a
    /// food-surplus lapse, which moves with each harvest, count as structural when the AI forfeits a part-built university
    /// over it?) changes a test rather than slipping through: a university 60 % built at a host whose published food surplus
    /// falls below the founding threshold is cancelled by the AI (forfeiting what went in); with the surplus above the
    /// threshold it is kept; a market shortfall (too few adults) is cancelled too.
    /// </summary>
    [Fact]
    public void TheAi_CancelsAUniversityHeadWhoseHostCannotFoundIt_AsSpecified_FoodSurplusAndMarketAlike()
    {
        SimConfig cfg = UniversityRigs.Cfg(adultsPerUniversity: 100);
        WorldState w = WorldFounding.Found(TestConfigs.DevWorldgen() with { AiEmpires = 1 }, cfg, 42);
        UniversityRigs.Grant(w, TestConfigs.Research(), Rival);
        SettlementId s = LabourActivities.ControlledSettlements(w, Rival)[0];
        int university = InstitutionContent.ProjectOfType(cfg, 1)!.Id;
        w.ConstructionQueue.Add(new ConstructionQueueRow(s, 0, university));
        w.ConstructionProgress.Add(new ConstructionProgressRow(s, 0, university, 0, 600_000));
        Share(w, s, 0.5);
        double founding = cfg.Institutions!.Universities.FoundingFoodSurplusRatio;

        UniversityRigs.Surplus(w, s, founding + 0.5);
        Assert.True(InstitutionViability.ToFound(w, cfg, s).Viable);
        Assert.DoesNotContain(AiConstructionPolicy.Decide(w, cfg, Rival, 10.0), o => o.Kind == OrderKind.CancelConstruction);

        UniversityRigs.Surplus(w, s, founding - 0.1);
        Assert.Equal(ViabilityFailure.FoodSurplus, InstitutionViability.ToFound(w, cfg, s).Failure);
        Assert.Equal(HeadLimitKind.Viability, ConstructionQuery.StructuralStall(w, cfg, s, 10.0)!.Limit);
        Assert.Contains(ConstructionQuery.CancelOrder(w, Rival, s, 0), AiConstructionPolicy.Decide(w, cfg, Rival, 10.0));

        UniversityRigs.Surplus(w, s, founding + 0.5);
        SimConfig crowded = UniversityRigs.Cfg(adultsPerUniversity: 1_000_000);
        Assert.Equal(ViabilityFailure.Market, InstitutionViability.ToFound(w, crowded, s).Failure);
        Assert.Contains(ConstructionQuery.CancelOrder(w, Rival, s, 0), AiConstructionPolicy.Decide(w, crowded, Rival, 10.0));
    }

    /// <summary>
    /// C1 review F4a: labour is a PACE, not a gate (WS1 UI: "Blocker drops the per-turn labour clause"). A project needing four
    /// turns of this settlement's builders, with its materials held, has NO blocker under incremental construction; the
    /// switch-off twin (M4-D's gate) keeps the per-turn labour clause. A material shortfall is still named — against the
    /// REMAINING need at the entry's progress — and still with no labour clause.
    /// </summary>
    [Fact]
    public void TheBlocker_HasNoLabourClause_WithIncrementalConstruction_TheSwitchOffTwinKeepsIt()
    {
        WorldState w = Solo();
        Share(w, S0, 0.5);
        double c = C(w, S0, 10.0);
        Assert.True(c > 0.0, "anti-vacuity: S0 has builders");
        SimConfig cfg = WithWorks(Cfg, labor: c * 4);
        ConstructionProjectEntry works = cfg.Goods!.ProjectById(Works)!;
        Set(w, S0, Timber, 40);
        Set(w, S0, Stone, 20);
        Assert.Null(ConstructionQuery.Blocker(w, cfg, S0, works, 10.0));
        string? off = ConstructionQuery.Blocker(w, TestConfigs.PreIncrementalConstruction(cfg), S0, works, 10.0);
        Assert.NotNull(off);
        Assert.Contains("adult-years of construction labour", off);

        Set(w, S0, Stone, 5);
        Assert.Equal("needs 10 stone (empire has 5)", ConstructionQuery.Blocker(w, cfg, S0, works, 10.0, progress: 500_000));
    }

    // ================================================================== one resolver: the system and the UI's estimate

    [Fact]
    public void HeadStatus_EqualsTheRealisedProgressAndDraws_WhereNothingElseMovesTheGoods_AndNamesWhoTookFirst()
    {
        // Construction only: nothing between turn start and construction changes timber or stone, so the estimate must
        // equal the step. S1 (ordered at turn 0) and S0 (ordered at turn 1) share a purse of 25 stone.
        WorldState w = Solo();
        AllBuilders(w, S0);
        AllBuilders(w, S1);
        Set(w, S2, Timber, 200);
        OrderLog log = Log(Enqueue(0, Player, S1), Enqueue(1, Player, S0));
        TurnExecutor exec = ConstructionOnly(Cfg, log);
        WorldState t1 = exec.Step(w);
        Set(t1, S3, Stone, 25);
        OrderRecord[] pending = [Enqueue(1, Player, S0)];

        HeadStatusReport s1 = ConstructionQuery.HeadStatus(t1, Cfg, S1, pending, 10.0)!;
        HeadStatusReport s0 = ConstructionQuery.HeadStatus(t1, Cfg, S0, pending, 10.0)!;
        WorldState t2 = exec.Step(t1);

        Assert.True(s1.Resolution.Completes);
        Assert.Equal(1, Built(t2, S1));
        Assert.Equal(s0.Resolution.ProgressAfter, P(t2, S0));
        Assert.Equal(250_000, s0.Resolution.ProgressAfter);
        Assert.Equal([10L, 5L], s0.Resolution.Draws);
        Assert.Equal(200 - 40 - 10, Of(t2, S2, Timber));
        Assert.Equal(0, s1.PlaceInOrder);
        Assert.Equal(1, s0.PlaceInOrder);
        Assert.Equal([new QueueEntryRef(S1, 0, Granary), new QueueEntryRef(S0, 0, Granary)], s0.Order);

        // A purse of exactly 20: S1 takes it all; S0 waits — and the estimate says who took it first.
        WorldState u1 = exec.Step(w);
        Set(u1, S3, Stone, 20);
        HeadStatusReport waiting = ConstructionQuery.HeadStatus(u1, Cfg, S0, pending, 10.0)!;
        Assert.False(waiting.Resolution.Progresses);
        Assert.Equal((HeadLimitKind.Materials, Stone, MaterialCause.TakenByEarlier), (waiting.Resolution.Limit, waiting.Resolution.LimitGood, waiting.Cause));
        Assert.Equal([new QueueEntryRef(S1, 0, Granary)], waiting.TakenBy);
        Assert.Equal(20, waiting.HeldAtStart);
        Assert.Equal(0, P(exec.Step(u1), S0));
        // No stone at all: the empire has none.
        HeadStatusReport none = ConstructionQuery.HeadStatus(exec.Step(w), Cfg, S0, pending, 10.0)!;
        Assert.Equal(MaterialCause.EmpireHasNone, none.Cause);
    }

    // ================================================================== the batch-2 systems

    [Fact]
    public void APartBuiltGranary_IsNotAGranary_NoFactorNoMilestone_UntilTheStepItCompletes_TurnExact()
    {
        WorldState w = Solo();
        AllBuilders(w, S0);
        Set(w, S0, Timber, 100);
        Set(w, S0, Stone, 10);
        TurnExecutor exec = ConstructionOnly(Cfg, Log(Enqueue(0, Player, S0)));
        WorldState t1 = exec.Step(w);
        Assert.Equal(500_000, P(t1, S0));
        GranariesConfig g = GranaryEffects.Active(Cfg)!;
        Assert.Equal(0, GranaryEffects.LocalGranaries(t1, Cfg, S0));
        Assert.Equal(0, GranaryEffects.EmpireGranaries(t1, Cfg, S0));
        Assert.Equal(1.0, GranaryEffects.FarmYieldFactor(g, GranaryEffects.LocalGranaries(t1, Cfg, S0)));
        Assert.Equal(1.0, GranaryEffects.FamineIntensityFactor(g, GranaryEffects.EmpireGranaries(t1, Cfg, S0)));
        Sim.Core.Systems.Ages.AgeMilestone granaryMilestone = Milestone("a2_granary");
        Assert.False(AgeQuery.Milestone(t1, Player, granaryMilestone).Met);

        Set(t1, S0, Stone, 10);
        WorldState t2 = exec.Step(t1);                                          // completes in step 1 → 2
        Assert.Equal(1, Built(t2, S0));
        Assert.Equal(1, GranaryEffects.LocalGranaries(t2, Cfg, S0));
        Assert.True(GranaryEffects.FarmYieldFactor(g, 1) > 1.0 && GranaryEffects.FamineIntensityFactor(g, 1) < 1.0);
        Assert.True(AgeQuery.Milestone(t2, Player, granaryMilestone).Met);
        // The factors act from the NEXT production step: ProductionSystem and DemographicsSystem read the count from PREV,
        // which is state 1 (no granary) in the completing step and state 2 (one) from the step after.
        Assert.Equal(0, GranaryEffects.LocalGranaries(t1, Cfg, S0));
    }

    /// <summary>
    /// C1 review F10 — the farm factor's TIMING through ProductionSystem itself, turn-exact. Production (pipeline position 4)
    /// and Construction (10) on one executor; S0 cultivates (root_crop known, so the granary factor applies to its grain) with
    /// half its labour farming and half building. Its granary is half built in step 0 → 1 (10 of 20 stone) and, with the other
    /// ten stone given at turn 1, completes in step 1 → 2. A CONTROL run never gets the other ten (its head stays part-built).
    /// Step 1 → 2 harvests IDENTICALLY in both (production reads PREV — state 1, where neither has a granary, and a part-built
    /// one is no granary); step 2 → 3 harvests MORE where the granary stands, by exactly the one-granary factor. The other
    /// settlements only farm, so no stone is quarried into the purse during the run.
    /// </summary>
    [Fact]
    public void ACompletedGranary_FirstRaisesTheHarvestInTheProductionStepAfterItsCompletionStep_TurnExact()
    {
        WorldState w = Solo();
        UniversityRigs.Grant(w, TestConfigs.Research(), Player, "root_crop");
        Assert.False(LabourActivities.HarvestsWildFood(w, Cfg, S0), "anti-vacuity: S0 cultivates");
        Share(w, S0, 0.5);
        foreach (SettlementId other in new[] { S1, S2, S3 }) NoBuilders(w, other);   // all farming: no sibling quarries stone into the purse
        Set(w, S0, Timber, 100);
        Set(w, S0, Stone, 10);
        var pipeline = new SystemRegistration[] { SystemCatalog.Production(Cfg), SystemCatalog.Construction(Cfg) };
        TurnExecutor exec = new(ResearchRigs.FlatEra(10.0), pipeline, Log(Enqueue(0, Player, S0)));
        WorldState t1 = exec.Step(w);
        Assert.Equal(500_000, P(t1, S0));

        WorldState control1 = t1.Clone();
        Set(t1, S0, Stone, 10);
        WorldState t2 = exec.Step(t1), c2 = exec.Step(control1);
        Assert.Equal((1L, 0L), (Built(t2, S0), Built(c2, S0)));                 // completes in step 1 → 2 (state 2)
        Assert.Equal(500_000, P(c2, S0));                                        // the control's head still waits
        GoodStockRow Grain(IReadOnlyWorldState x) => x.GoodStocks[GoodStockIndex.IndexOf(x.GoodStocks, S0, new GoodId(Cfg.Goods!.GrainId))];
        Assert.True(Grain(c2).LastProducedUnits > 100, $"anti-vacuity: S0 harvests ({Grain(c2).LastProducedUnits})");
        Assert.Equal(Grain(c2), Grain(t2));                                      // step 1 → 2: no factor yet

        WorldState t3 = exec.Step(t2), c3 = exec.Step(c2);
        Assert.True(Grain(t3).LastProducedUnits > Grain(c3).LastProducedUnits);    // step 2 → 3: the factor acts
        double rateOn = Grain(t3).Amount.Value - Grain(t2).Amount.Value + Grain(t3).ProduceRemainder - Grain(t2).ProduceRemainder;
        double rateOff = Grain(c3).Amount.Value - Grain(c2).Amount.Value + Grain(c3).ProduceRemainder - Grain(c2).ProduceRemainder;
        Assert.Equal(GranaryEffects.FarmYieldFactor(GranaryEffects.Active(Cfg)!, 1), rateOn / rateOff, 12);
    }

    private static Sim.Core.Systems.Ages.AgeMilestone Milestone(string id)
    {
        foreach (Sim.Core.Systems.Ages.AgeDefinition a in TestConfigs.Ages().Ages)
            if (a.Entry is { } e)
            {
                foreach (Sim.Core.Systems.Ages.AgeMilestone m in e.Core) if (m.Id == id) return m;
                foreach (Sim.Core.Systems.Ages.AgeMilestone m in e.Supporting) if (m.Id == id) return m;
            }
        throw new InvalidOperationException("no milestone " + id);
    }

    [Fact]
    public void StoneProducedEarlierInTheSameStep_ReachesAWaitingHead_InThatStep()
    {
        // Turn sequence: state t holds NO stone in the purse; step t → t+1 runs production (pipeline position 4) and then
        // construction (10) on the same NEXT stocks; the head (ordered at t) advances in that step on the new stone.
        WorldState w = Solo();
        SettlementId quarry = default;
        bool found = false;
        for (int i = 0; i < w.Deposits.Count && !found; i++)
            if (w.Deposits[i].Good.Value == Stone && w.Deposits[i].Abundance > 0.0) { quarry = w.Deposits[i].Settlement; found = true; }
        Assert.True(found, "anti-vacuity: the dev world has a stone deposit");
        var row = new SectorAllocationRow(quarry, Farming: 0.0, Herding: 0.0, Extraction: 0.5, Crafting: 0.0, Construction: 0.5);
        bool had = false;
        for (int i = 0; i < w.SectorAllocations.Count; i++)
            if (w.SectorAllocations[i].Settlement == quarry) { w.SectorAllocations[i] = row; had = true; }
        if (!had) w.SectorAllocations.Add(row);
        Set(w, quarry, Timber, 100);
        Assert.Equal(0, TotalOf(w, Stone));
        var pipeline = new SystemRegistration[] { SystemCatalog.Production(Cfg), SystemCatalog.Construction(Cfg) };
        WorldState t1 = new TurnExecutor(ResearchRigs.FlatEra(10.0), pipeline, Log(Enqueue(0, Player, quarry))).Step(w);
        long produced = TotalOf(t1, Stone) + (Sunk(t1, Stone) - Sunk(w, Stone));
        Assert.True(produced > 0, "anti-vacuity: production made stone in the step");
        long p = P(t1, quarry) + (Built(t1, quarry) > 0 ? N : 0);
        Assert.True(p > 0, "the head advanced on stone that did not exist at turn start");
        Assert.Equal(ConstructionQuery.Delivered(20, Math.Min(p, N)), Sunk(t1, Stone) - Sunk(w, Stone));
    }

    [Fact]
    public void StoneTradedEarlierInTheSameStep_ReachesAWaitingHead_InThatStep()
    {
        // Turn sequence: state t — the builder S0 (stateless: its own purse) holds NO stone, its neighbour S1 holds 10,000
        // at a far lower price (PREV prices, the trade step's input); step t → t+1 runs trade (pipeline position 8) and then
        // construction (10) on the same NEXT stocks: the head, ordered at t, advances on the stone trade brought in.
        WorldState w = TradeArbitrageTests.TradeWorld(Cfg, 2,
            stocks: [(0, "timber", 100, 0), (0, "stone", 0, 500), (1, "stone", 10_000, 0)],
            prices: [(0, "stone", 50.0), (1, "stone", 1.0)],
            edges: [(0, 1, 1.0)]);
        int adult = w.Buckets.Add(new BucketRow(S0, new CultureId(0), new ReligionId(0), new ClassId(0), Cohorts.FirstAdult,
            Conserved.Zero, 0.0, 0.0, 0.0, 0.0));
        new Ledger(w.LedgerFlows).Flow(ref w.Buckets.Ref(adult).Count, ConservedQuantityIds.Population, ReasonIds.InitialEndowment,
            100, FlowDirection.Source, OverdrawPolicy.Throw);
        AllBuilders(w, S0);
        Assert.Equal(0, Of(w, S0, Stone));
        var pipeline = new SystemRegistration[] { SystemCatalog.TradeArbitrage(Cfg), SystemCatalog.Construction(Cfg) };
        WorldState t1 = new TurnExecutor(ResearchRigs.FlatEra(10.0), pipeline, Log(Enqueue(0, Player, S0))).Step(w);
        long tradedIn = 0;
        for (int i = 0; i < t1.TradeFlows.Count; i++)
            if (t1.TradeFlows[i].Good.Value == Stone && t1.TradeFlows[i].To == S0) tradedIn += t1.TradeFlows[i].Quantity;
        Assert.True(tradedIn > 0, "anti-vacuity: trade brought stone into the empty purse");
        long p = Built(t1, S0) > 0 ? N : P(t1, S0);
        Assert.True(p > 0, "the head advanced on stone that arrived by trade in the same step");
        Assert.Equal(ConstructionQuery.Delivered(20, p), tradedIn - Of(t1, S0, Stone));   // what it drew of what came in
    }
}
