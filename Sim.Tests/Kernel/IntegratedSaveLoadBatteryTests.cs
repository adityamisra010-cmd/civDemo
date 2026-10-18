using System.Collections.Immutable;
using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Ages;
using Sim.Core.Systems.Research;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Kernel;

/// <summary>
/// M5-integration stream V, item 2 — THE INTEGRATED SAVE/LOAD BATTERY. One founded world (dev worldgen,
/// seed 42, aiEmpires = 1) on the production pipeline, run exactly as UiSession.EndTurn runs it: before each
/// step the player's orders for that turn (built by the same order constructors the UI calls, from the world
/// the player sees) and then the AI's orders (AiOrders.Append) go into ONE order log the executor delivers
/// from. The player issues every order kind the M5 surface has: labour (legacy + sector), research target
/// set / clear / re-set, construction (a granary and a university, the knowledge rig-forced), road
/// development, a tax rate, an Age advance (eligibility rig-forced), — M5 S1b, ADR-036 — two Found
/// Settlement orders: one from the player's second holding, then one FROM the colony it founded (the ADR-034 case), and —
/// D-054 — a CancelConstruction of a committed queue entry (WS1) and a SetResourceAllocation order (kind 12, WS2: the
/// player's building/roads split, upserted the step it is delivered), all late in the run so every earlier save point (and
/// its measured turn) is untouched.
/// Rigs touch turn 0 only and move conserved stocks through the Ledger.
///
/// The uninterrupted run records every turn's hash. At each SAVE POINT (turn 1; mid-research; right after a
/// completion; mid partial road modernization; mid university maturation; between the tax order's issue and
/// its effect; across the Age transition) the world AND the order log are serialized and loaded back, the
/// action surface (AvailableActionsQuery, both Empires) must be equal before save / after load, and the loaded
/// session is continued with a FRESH executor, re-running the player script and the AI producer on the loaded
/// state — every subsequent turn's hash must equal the uninterrupted run's, and so must the final log.
/// Every save point is located by measuring the uninterrupted run (non-vacuity is asserted).
/// </summary>
[Trait("suite", "determinism")]
public partial class IntegratedSaveLoadBatteryTests
{
    /// <summary>Batch 3 (D-054, integration R1): the battery's configuration — the SimConfig, the dev worldgen the world is
    /// founded with, and the production worldgen the pipeline and the AI are built with. <see cref="Shipped"/> is THE rig;
    /// the save-point attribution control (<see cref="TheSavePoints_MovedForTheCylinderAndIncrementalConstruction_EachLayerAloneMovesItsOwnPin"/>)
    /// records the same script with one batch-3 layer switched off.
    /// D-055 (2026-10-16): <c>Extra</c> — orders a variant adds to the player's script each turn (null for every pinned run;
    /// the battery variant TheBatteryVariant_AYoungerGranaryWaitsBehindTheUniversitysSetAside_… passes one).</summary>
    internal sealed record Rig(SimConfig Cfg, WorldgenConfig Dev, WorldgenConfig Production, Func<IReadOnlyWorldState, OrderRecord[]>? Extra = null);

    private static readonly Rig Shipped = new(UniversityRigs.Cfg(adultsPerUniversity: 100), TestConfigs.DevWorldgen(), TestConfigs.Worldgen());
    private static readonly SimConfig Cfg = Shipped.Cfg;
    private static readonly ResearchContent Research = TestConfigs.Research();
    private static readonly AgeContent Ages = TestConfigs.Ages();
    private static readonly PolityId Player = new(1);
    private static readonly PolityId Rival = new(2);
    private const int Horizon = 40;
    private const long RoadTurn = 1, RoadAgainTurn = 12, AgeTurn = 3, TaxTurn = 5, ClearTurn = 9, RetargetTurn = 10;

    /// <summary>M5 S1b (ADR-036): the Found Settlement turns — after the last measured save point (34), so the
    /// orders change only worlds 36.. and the existing pins below cannot move.</summary>
    private const long FoundTurn = 35, FromColonyTurn = 37;

    /// <summary>D-054 WS1 (C1 review F3): the construction-termination turns — after the last measured save point, like the
    /// Found Settlement turns, so they change only worlds 37.. and no measured pin below moves. BuildTurn: three granaries
    /// at the player's other holding (they queue behind whatever it holds). CancelTurn: a CancelConstruction of that
    /// holding's LAST entry — a non-head entry at delivery, since a step completes at most the head. AllocationTurn: a
    /// SetResourceAllocation (kind 12) for the player's own Empire (WS2: state AllocationTurn + 1 holds its row). MEASURED on this
    /// tree (seed 42, by the agent writing this line): the holding is settlement 2; its slot 0 completes in step 36 → 37, so
    /// world 37 holds slots 1 (head) and 2 at p = 0; the turn-37 order cancels slot 2 and slot 1 completes in step 37 → 38.
    /// With TestConfigs.PreIncrementalConstruction the measured save points below are still (1, 27, 2, 34): these orders
    /// move no pin. Batch 3 / D-054 (integration R1, 2026-10-14; RE-MEASURED on the shipped cylinder): the same sequence —
    /// settlement 2, slot 0 built in step 36 → 37, world 37 holding slots 1 (head) and 2 at p = 0, slot 2 cancelled and slot 1
    /// built in step 37 → 38 (world 38: queue empty, two granaries); the save points are those of
    /// TheSavePoints_MovedForTheCylinderAndIncrementalConstruction_EachLayerAloneMovesItsOwnPin, all before turn 35, so
    /// these orders still move no pin.</summary>
    private const long BuildTurn = 36, CancelTurn = 37, AllocationTurn = 38;
    private const double AllocationPercent = 30.0;

    /// <summary>H2: the Age the player is rigged into at turn 0 — one below the tax edict's Age, so the scripted
    /// AdvanceAge enters the Age that makes the turn-5 tax order legal.</summary>
    private static int EntryAge => GovernanceRigs.TaxAge - 1;

    private static int UniversityProject => InstitutionContent.ProjectOfType(Cfg, 1)!.Id;
    private const int Granary = 1;
    private const long TimberLeft = 20;

    private static TurnExecutor Production(OrderLog orders, Rig rig)
    {
        using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
        using var pipeStream = Sim.Data.DataFiles.OpenPipeline();
        return new TurnExecutor(EraTableLoader.Load(eraStream),
            PipelineLoader.Load(pipeStream, SystemCatalog.All(rig.Cfg, rig.Production)), orders);
    }

    private static string[] WithAncestors(params string[] nodeIds)
    {
        var seen = new bool[Research.Nodes.Count];
        var stack = new Stack<int>();
        foreach (string id in nodeIds) stack.Push(Research.IndexOfId(id));
        while (stack.Count > 0)
        {
            int i = stack.Pop();
            if (seen[i]) continue;
            seen[i] = true;
            foreach (int p in Research.Nodes[i].PrerequisiteNodes) stack.Push(p);
        }
        var ids = new List<string>();
        for (int i = 0; i < seen.Length; i++) if (seen[i]) ids.Add(Research.Nodes[i].Id);
        return ids.ToArray();
    }

    /// <summary>The founded world plus the turn-0 rigs: the player knows the track road's closure, both
    /// university entities, the tax gate and the A2 entry (cereal + pottery; the founding warband is the
    /// military milestone), and the capital holds the university's materials.
    /// H2 (2026-10-05, Director §7: the tax edict is operational only from A3): the player starts IN A2 (an Age
    /// row, the rig standing in for the earlier AdvanceAge) and knows the A3 entry — core arsenical bronze (no
    /// recipe), supporting writing + law code (institutional) and the solid wheel (technological): 3 supporting
    /// over 2 categories — so the scripted AdvanceAge enters A3 and the later tax order is legal.</summary>
    private static WorldState Start(Rig rig)
    {
        WorldState w = WorldFounding.Found(rig.Dev with { AiEmpires = 1 }, rig.Cfg, 42);
        var known = new List<string>(WithAncestors("track_road"));
        known.AddRange(WithAncestors(UniversityRigs.Knowledge));
        known.AddRange(WithAncestors(GovernanceRigs.TaxationNode, "cereal_cultivation", "pottery_open_fired"));
        known.AddRange(WithAncestors("arsenical_bronze", "wheel_solid", "law_code"));
        UniversityRigs.Grant(w, Research, Player, known.Distinct().ToArray());
        GovernanceRigs.EnterAge(w, Player, EntryAge);
        UniversityRigs.Materials(w, rig.Cfg, Capital(w));
        // Road purse rig: the player's timber is cut to a little, through the Ledger, so the first road order that pays can
        // afford only PART of its first route (the order ends there). A later order pays only once a holding is free:
        // with the hold rule off (TestConfigs.PreReservations, batch 3) the turn-1 order part-builds route 1 (0 -> 0.755),
        // the turn-12 order pays nothing, and the order once the university stands (turn 23) completes it; with WS1 off
        // (PreIncrementalConstruction) the turn-12 order completes it.
        // D-055 (2026-10-16): with the hold rule on, the capital's university sets that timber aside from world 2 on, so
        // the turn-1 and turn-12 orders pay nothing and the script's road order once the university stands (PlayerOrders,
        // turn 22) is the one that part-builds the first route (0 -> 0.62); no later order continues it within the 40 turns,
        // so the route stays part-built in the shipped run. (Fix round 2026-10-16, finding T1a-L3-T1a-L3-F6: the old "the
        // second order continues it" held in no run — MEASURED per arm, t1a-out/fix/battery/probe-battery-clean.txt.)
        var timber = new GoodId(rig.Cfg.Goods!.IdOf("timber"));
        for (int i = 0; i < w.GoodStocks.Count; i++)
        {
            GoodStockRow row = w.GoodStocks[i];
            if (row.Good != timber || !EmpireQuery.ControlsSettlement(w, Player, row.Settlement)) continue;
            long excess = row.Amount.Value - TimberLeft;
            if (excess <= 0) continue;
            new Ledger(w.LedgerFlows).Flow(ref w.GoodStocks.Ref(i).Amount, ConservedQuantityIds.OfGood(timber),
                ReasonIds.InitialEndowment, excess, FlowDirection.Sink, OverdrawPolicy.Throw);
        }
        return w;
    }

    private static SettlementId Capital(IReadOnlyWorldState w) =>
        EmpireQuery.TryGetCapital(w, Player, out SettlementId s) ? s : throw new InvalidOperationException("no capital");

    private static SettlementId OtherHolding(IReadOnlyWorldState w)
    {
        SettlementId cap = Capital(w);
        for (int i = 0; i < w.Settlements.Count; i++)
            if (w.Settlements[i].Id != cap && EmpireQuery.ControlsSettlement(w, Player, w.Settlements[i].Id))
                return w.Settlements[i].Id;
        return cap;
    }

    /// <summary>The first research node AVAILABLE to the player now, other than <paramref name="skip"/>
    /// (research.json order — a stable integer order, no score involved).</summary>
    private static OrderRecord? FirstTarget(IReadOnlyWorldState w, int skip)
    {
        for (int i = 0; i < Research.Nodes.Count; i++)
        {
            if (Research.Nodes[i].Key.Value == skip) continue;
            if (ResearchQuery.TargetOrder(w, Research, Player, Research.Nodes[i].Key) is { } o) return o;
        }
        return null;
    }

    /// <summary>The player's script: a pure function of (turn, the world the player sees).</summary>
    private static void PlayerOrders(OrderLog log, IReadOnlyWorldState w, Rig rig)
    {
        long t = w.Clock.Turn;
        if (t == 0)
        {
            SettlementId cap = Capital(w);
            OrderRecord target = FirstTarget(w, -1) ?? throw new InvalidOperationException("nothing to research");
            log.Append(target);
            log.Append(OrderRecord.From(t, Player, OrderKind.SectorAllocation, (cap.Value << 3) | Sectors.Construction, 60.0));
            log.Append(OrderRecord.From(t, Player, OrderKind.LaborAllocation, OtherHolding(w).Value, 70.0));
            log.Append(ConstructionQuery.EnqueueOrder(w, Player, cap, Granary));
            log.Append(ConstructionQuery.EnqueueOrder(w, Player, cap, UniversityProject));
        }
        if (t == RoadTurn || t == RoadAgainTurn) log.Append(RoadDevelopmentQuery.DevelopOrder(w, Player, 100.0));
        // D-055 (2026-10-16): THE ROAD ORDER ONCE THE UNIVERSITY STANDS — after RoadAgainTurn, the first turn the capital's
        // queue is empty (its university is built) and no road development the player decided after RoadAgainTurn exists,
        // the player orders the roads again (re-issued each turn until one pays). Why: under the hold rule (sim.json
        // construction.reservations) the capital's university, a valid long wait, SETS ASIDE every unit of the player's
        // timber from world 2 to world 21 (its need is 1,800), so the turn-1 and turn-12 orders pay nothing and the battery
        // would never reach its "partial road" save point. MEASURED (2026-10-16, by the agent writing this line): shipped,
        // the order is issued at turn 22 and part-builds route 1 (152 units, 0 -> 0.62) in world 23; in EVERY arm with the
        // rule off (TestConfigs.PreReservations, PreIncrementalConstruction, both with the flat world) it fires only after
        // that arm's university stands (turn 23 or 32) and moves no measured save point (the attribution control below).
        if (t > RoadAgainTurn && ConstructionQuery.Queue(w, Capital(w)).Length == 0 && !RoadDecidedAfter(w, RoadAgainTurn))
            log.Append(RoadDevelopmentQuery.DevelopOrder(w, Player, 100.0));
        if (t == AgeTurn) log.Append(AgeQuery.AdvanceOrder(w, Player, EntryAge + 1, Ages.Surges[0].Key));
        if (t == TaxTurn) log.Append(Governance.TaxOrder(t, Player, 10.0));
        if (t == ClearTurn) log.Append(ResearchQuery.ClearTargetOrder(w, Player));
        if (t == RetargetTurn)
        {
            int current = -1;
            for (int i = 0; i < w.ResearchProgress.Count; i++)
                if (w.ResearchProgress[i].Polity == Player) { current = w.ResearchProgress[i].Node.Value; break; }
            if (FirstTarget(w, current) is { } o) log.Append(o);
        }
        // The other holding, not the capital: at turn 35 the capital's granary is empty (measured: 0 grain, every
        // otherwise-legal site NoProvisions), so it may not send a party; settlement 2 holds 647 grain.
        // (Batch 3 / D-054, integration R1: on the shipped cylinder the capital holds 47 grain and settlement 2 holds 719 at
        // turn 35, MEASURED; the order still comes from settlement 2, the script's choice.)
        if (t == FoundTurn) log.Append(FoundFrom(w, OtherHolding(w), rig));
        if (t == FromColonyTurn) log.Append(FoundFrom(w, ColonyOf(w), rig));
        if (t == BuildTurn)
            for (int i = 0; i < 3; i++) log.Append(ConstructionQuery.EnqueueOrder(w, Player, OtherHolding(w), Granary));
        if (t == CancelTurn && ConstructionQuery.Queue(w, OtherHolding(w)) is { Length: >= 2 } queued)
            log.Append(ConstructionQuery.CancelOrder(w, Player, OtherHolding(w), queued[^1].Slot));
        if (t == AllocationTurn)
            log.Append(OrderRecord.From(t, Player, OrderKind.SetResourceAllocation, Player.Value, AllocationPercent));
    }

    /// <summary>D-055: some road development the player decided after <paramref name="turn"/> is in the world's log
    /// (RoadDevelopments is append-only: one row per route an order paid for).</summary>
    private static bool RoadDecidedAfter(IReadOnlyWorldState w, long turn)
    {
        for (int i = 0; i < w.RoadDevelopments.Count; i++)
            if (w.RoadDevelopments[i].Polity == Player && w.RoadDevelopments[i].Turn > turn) return true;
        return false;
    }

    /// <summary>A Found Settlement order from <paramref name="source"/> to its nearest LEGAL site (FoundingRigs'
    /// stable ring scan over THE predicate, FoundingQuery.Check) — a pure function of the world.</summary>
    private static OrderRecord FoundFrom(IReadOnlyWorldState w, SettlementId source, Rig rig)
    {
        int site = FoundingRigs.NearestLegalSite(w, rig.Cfg, FoundingRigs.Frontier(w, rig.Cfg, rig.Production), Player, source);
        if (site < 0) throw new InvalidOperationException($"turn {w.Clock.Turn}: no legal site from settlement {source.Value}");
        return FoundingQuery.FoundOrder(w, Player, source, site);
    }

    /// <summary>The colony the FoundTurn order founded: the player's settlement founded in state FoundTurn + 1.</summary>
    private static SettlementId ColonyOf(IReadOnlyWorldState w)
    {
        for (int i = 0; i < w.Settlements.Count; i++)
            if (w.Settlements[i].FoundedTurn == FoundTurn + 1 && EmpireQuery.ControlsSettlement(w, Player, w.Settlements[i].Id))
                return w.Settlements[i].Id;
        throw new InvalidOperationException("the FoundTurn order founded no colony");
    }

    private static void OneTurnOfOrders(OrderLog log, IReadOnlyWorldState w, Rig rig)
    {
        PlayerOrders(log, w, rig);
        if (rig.Extra is { } extra)
            foreach (OrderRecord o in extra(w)) log.Append(o);
        AiOrders.Append(log, w, rig.Cfg, rig.Production);
    }

    private static ImmutableArray<ActionDescriptor>[] Surface(IReadOnlyWorldState w) =>
        [AvailableActionsQuery.For(w, Cfg, Player), AvailableActionsQuery.For(w, Cfg, Rival)];

    private static double PartialProgress(IReadOnlyWorldState w)
    {
        for (int i = 0; i < w.ResearchProgress.Count; i++)
            if (w.ResearchProgress[i].Polity == Player && w.ResearchProgress[i].Progress > 0.0) return w.ResearchProgress[i].Progress;
        return 0.0;
    }

    private static int Completed(IReadOnlyWorldState w)
    {
        int n = 0;
        for (int i = 0; i < w.ResearchCompleted.Count; i++) if (w.ResearchCompleted[i].Polity == Player) n++;
        return n;
    }

    private static bool PartialRoad(IReadOnlyWorldState w)
    {
        for (int i = 0; i < w.RoadDevelopments.Count; i++)
        {
            RoadDevelopmentRow r = w.RoadDevelopments[i];
            if (r.Polity == Player && r.ProgressAfter > 0.0 && r.ProgressAfter < 1.0) return true;
        }
        return false;
    }

    /// <summary>D-054 WS1: some construction head has part of its work in (0 &lt; p &lt; N) — a state no batch-2 save held.</summary>
    private static bool PartBuiltHead(IReadOnlyWorldState w)
    {
        for (int i = 0; i < w.ConstructionProgress.Count; i++)
            if (w.ConstructionProgress[i].Progress > 0 && w.ConstructionProgress[i].Progress < ConstructionQuery.ProgressUnits) return true;
        return false;
    }

    /// <summary>Final round fix 1 (verifier RC1-3-SAMESTATE): the two worlds hold the same ConstructionProgress table,
    /// row for row (<see cref="ConstructionProgressRow"/> is a record struct: value equality on all five fields).</summary>
    private static bool SameProgressTable(IReadOnlyWorldState a, IReadOnlyWorldState b)
    {
        if (a.ConstructionProgress.Count != b.ConstructionProgress.Count) return false;
        for (int i = 0; i < a.ConstructionProgress.Count; i++)
            if (a.ConstructionProgress[i] != b.ConstructionProgress[i]) return false;
        return true;
    }

    private static double Maturity(IReadOnlyWorldState w)
    {
        for (int i = 0; i < w.Institutions.Count; i++) if (w.Institutions[i].Polity == Player) return w.Institutions[i].Maturity;
        return -1.0;
    }

    private static double TaxRate(IReadOnlyWorldState w)
    {
        for (int i = 0; i < w.TaxPolicies.Count; i++) if (w.TaxPolicies[i].Polity == Player) return w.TaxPolicies[i].Rate;
        return 0.0;
    }

    internal sealed record Uninterrupted(List<WorldState> Worlds, List<string> Hashes, OrderLog Log);

    internal static readonly Lazy<Uninterrupted> Run = new(() => Record(Shipped), LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>The uninterrupted scripted run on <paramref name="rig"/>: every turn's world and hash, and the log.</summary>
    internal static Uninterrupted Record(Rig rig)
    {
        var log = new OrderLog();
        TurnExecutor exec = Production(log, rig);
        WorldState w = Start(rig);
        var worlds = new List<WorldState> { w.Clone() };
        var hashes = new List<string> { WorldHash.ComputeHex(w) };
        for (int t = 0; t < Horizon; t++)
        {
            OneTurnOfOrders(log, w, rig);
            w = exec.Step(w);
            worlds.Add(w.Clone());
            hashes.Add(WorldHash.ComputeHex(w));
        }
        return new Uninterrupted(worlds, hashes, log);
    }

    private static OrderLog Copy(OrderLog log)
    {
        using var ms = new MemoryStream();
        log.Save(ms);
        ms.Position = 0;
        return OrderLog.Load(ms);
    }

    /// <summary>Saves world k (and the log as written by then), loads both, asserts the surface, continues
    /// to the horizon re-running the player script and the AI, and asserts every hash and the final log.</summary>
    /// <param name="issued">When true the save is taken AFTER the player issued turn k's orders and before
    /// End Turn (the log holds them; the AI's turn-k orders are produced at End Turn, after the load).</param>
    private static void SaveLoadContinue(int k, string label, bool issued = false)
    {
        Uninterrupted run = Run.Value;
        WorldState atK = run.Worlds[k];

        // The log as it stood at turn k: every order stamped before k, plus (issued) the player's turn-k orders.
        var partial = new OrderLog();
        for (int i = 0; i < run.Log.Count; i++)
            if (run.Log[i].Turn < k || (issued && run.Log[i].Turn == k && run.Log[i].ActorId == Player.Value))
                partial.Append(run.Log[i]);

        using var ms = new MemoryStream();
        Snapshot.Save(atK, ms);
        ms.Position = 0;
        WorldState loaded = Snapshot.Load(ms, atK.Terrain);
        OrderLog log = Copy(partial);
        Assert.Equal(run.Hashes[k], WorldHash.ComputeHex(loaded));

        ImmutableArray<ActionDescriptor>[] before = Surface(atK), after = Surface(loaded);
        for (int p = 0; p < before.Length; p++)
        {
            Assert.Equal(before[p].AsEnumerable(), after[p].AsEnumerable());
            Assert.True(AvailableActionsQuery.Same(before[p], after[p]), $"{label}: action surface differs after load");
        }

        TurnExecutor exec = Production(log, Shipped);
        WorldState w = loaded;
        for (int t = k; t < Horizon; t++)
        {
            if (issued && t == k) AiOrders.Append(log, w, Shipped.Cfg, Shipped.Production);
            else OneTurnOfOrders(log, w, Shipped);
            w = exec.Step(w);
            Assert.True(run.Hashes[t + 1] == WorldHash.ComputeHex(w), $"{label} (saved at {k}): diverged at turn {t + 1}");
        }
        Assert.Equal(run.Log.Count, log.Count);
        for (int i = 0; i < log.Count; i++) Assert.Equal(run.Log[i], log[i]);
    }

    private static int First(Func<IReadOnlyWorldState, IReadOnlyWorldState, bool> at, string what) => First(Run.Value.Worlds, at, what);

    private static int First(List<WorldState> ws, Func<IReadOnlyWorldState, IReadOnlyWorldState, bool> at, string what)
    {
        for (int k = 1; k < ws.Count - 1; k++) if (at(ws[k - 1], ws[k])) return k;
        Assert.Fail($"the run never reached the save point '{what}' within {Horizon} turns");
        return -1;
    }

    /// <summary>The four measured save points of a recorded run: mid-research, right after a completion, a partial road,
    /// the university mid-maturation (each the first world in which it holds).</summary>
    internal static (int MidResearch, int AfterCompletion, int PartialRoad, int UniversityMaturing) SavePoints(List<WorldState> ws) => (
        First(ws, (_, w) => PartialProgress(w) > 0.0, "mid-research"),
        First(ws, (p, w) => Completed(w) > Completed(p), "after a completion"),
        First(ws, (_, w) => PartialRoad(w), "partial road"),
        First(ws, (_, w) => Maturity(w) is > 0.0 and < 1.0, "university maturing"));

    [Fact]
    public void TheScriptedRun_IssuesEveryOrderKind_AndReachesEverySavePoint()
    {
        Uninterrupted run = Run.Value;
        var kinds = new HashSet<OrderKind>();
        for (int i = 0; i < run.Log.Count; i++) if (run.Log[i].ActorId == Player.Value) kinds.Add(run.Log[i].Kind);
        foreach (OrderKind k in new[] { OrderKind.LaborAllocation, OrderKind.SectorAllocation, OrderKind.EnqueueConstruction,
                     OrderKind.SetTaxRate, OrderKind.SetResearchTarget, OrderKind.AdvanceAge, OrderKind.DevelopRoads,
                     OrderKind.FoundSettlement, OrderKind.CancelConstruction, OrderKind.SetResourceAllocation })
            Assert.Contains(k, kinds);
        Assert.Contains(Enumerable.Range(0, run.Log.Count).Select(i => run.Log[i]),
            o => o.ActorId == Player.Value && o.Kind == OrderKind.SetResearchTarget && o.TargetId == -1);
        Assert.Contains(Enumerable.Range(0, run.Log.Count).Select(i => run.Log[i]), o => o.ActorId == Rival.Value);

        // The order-delivery semantics the save points straddle, turn-exact.
        Assert.Equal(EntryAge, AgeQuery.CurrentAge(run.Worlds[(int)AgeTurn], Ages, Player));
        Assert.Equal(EntryAge + 1, AgeQuery.CurrentAge(run.Worlds[(int)AgeTurn + 1], Ages, Player));
        Assert.True(Governance.CanLevyTax(run.Worlds[(int)TaxTurn], Cfg, Player));   // H2: the Age gate is open
        Assert.Equal(0.0, TaxRate(run.Worlds[(int)TaxTurn]));
        Assert.True(TaxRate(run.Worlds[(int)TaxTurn + 1]) > 0.0);
        Assert.Equal(0.10, TaxRate(run.Worlds[(int)TaxTurn + 1]), 12);

        // M5 S1b (ADR-036), turn-exact: the FoundTurn order founds in state FoundTurn + 1; the order FROM that colony
        // (a source above the turn-0 maximum — ADR-034) founds in state FromColonyTurn + 1. The up-front validation
        // every replay path runs accepts the whole log against turn 0.
        int Count(IReadOnlyWorldState w) => w.Settlements.Count;
        Assert.Equal(Count(run.Worlds[(int)FoundTurn]) + 1, Count(run.Worlds[(int)FoundTurn + 1]));
        SettlementId colony = ColonyOf(run.Worlds[(int)FromColonyTurn]);
        Assert.True(colony.Value > FoundingRigs.MaxSettlementId(run.Worlds[0]));
        Assert.Equal(Count(run.Worlds[(int)FromColonyTurn]) + 1, Count(run.Worlds[(int)FromColonyTurn + 1]));
        Assert.Contains(Enumerable.Range(0, run.Log.Count).Select(i => run.Log[i]),
            o => o.Kind == OrderKind.FoundSettlement && o.TargetId == colony.Value && o.Turn == FromColonyTurn);
        OrderValidation.ValidateAgainstWorld(run.Log, run.Worlds[0]);

        // The save points, MEASURED on this tree (dev world, seed 42): partial research progress from turn 1;
        // the player's first completion at 28; the turn-1 road order affords 62% of its first route (turn 2);
        // the university (queued behind the granary on the timber-cut capital) is first mid-maturation at 32.
        // R2a RE-AIM (2026-10-03, MEASURED by the agent writing this line): university mid-maturation 32 -> 36.
        // Cause: the trade gate (no settlement here knows Trade, so the inter-settlement flows that supplied the
        // capital's build stop); with ONLY sim.json trade.entity removed the run returns 32.
        // D-051 1(b) RE-PIN (2026-10-10, time-averaged decade weather; MEASURED by the agent writing this line): first
        // completion 28 -> 27, university mid-maturation 36 -> 34; mid-research 1 and partial road 2 unchanged. Every save
        // point is still reached.
        // BATCH 3 / D-054 RE-PIN (integration R1, 2026-10-14; VALUE, MEASURED on this tree by the agent writing this line):
        // (1, 27, 2, 34) -> (1, 26, 2, 26) — the first completion 27 -> 26 and the university first mid-maturation 34 -> 26;
        // mid-research 1 and partial road 2 unchanged. Every save point is still reached (the two coincide at world 26:
        // the completion's save and the maturation's save are both taken there, and each continues hash-identically).
        // CAUSE: TWO LAYERS, each moving its own save point. (1) The CYLINDER (worldgen.json topology.wrapX — the dev world
        // regenerates x-periodic) moves the player's research curve: completion 27 -> 26. (2) WS1, INCREMENTAL CONSTRUCTION
        // (sim.json construction.incremental): the university, queued behind the granary on the timber-cut capital, is
        // built part by part as its materials arrive instead of waiting for its whole cost, so it stands — and matures —
        // eight turns earlier: 34 -> 26. ATTRIBUTION (MEASURED, TheSavePoints_MovedForTheCylinderAndIncrementalConstruction_EachLayerAloneMovesItsOwnPin):
        // WS1 off alone (TestConfigs.PreIncrementalConstruction, the cylinder on) -> (1, 26, 2, 34); the cylinder off alone
        // (TestConfigs.PreCylinderWorldgen, WS1 on) -> (1, 27, 2, 26); both off -> (1, 27, 2, 34) = OLD; WS2 off alone
        // (TestConfigs.PreAllocationRatios) -> the NEW values (the kind-12 order is at turn 38, after every save point).
        // D-055 RE-PIN (2026-10-16; the Director's batch-4 instruction, item 1 — projects must not consume resources reserved
        // by other projects, and road works may use only what building work neither draws nor sets aside; batch-4 design
        // §1.5 and §1.7, a behaviour candidate; VALUE, MEASURED on this tree by the agent writing this line):
        // (1, 26, 2, 26) -> (1, 26, 23, 24) — the partial road 2 -> 23 and the university first mid-maturation 26 -> 24;
        // mid-research 1 and the first completion 26 unchanged. CAUSE: the hold rule (sim.json construction.reservations).
        // From world 2 the capital's university — queued at turn 0, the head from world 1, a VALID long wait (it waits on
        // viability in 8 steps and on materials in the rest) — sets aside its remaining need, which exceeds the player's
        // whole timber purse in every world to 21 (world 2: all 185 of 185). So the turn-1 road order, which batch 3 paid 185
        // units (0 -> 0.755 of route 1, world 2), pays nothing, and neither does the turn-12 order: road works may use only
        // what building work neither draws nor sets aside. The university keeps those 185 units, completes in step 21 -> 22
        // (batch 3: 22 -> 23) and matures from world 24 (batch 3: 26). The script's road order once the university stands
        // (turn 22, above) is the first that pays: route 1, 152 units, 0 -> 0.62, world 23. ATTRIBUTION (MEASURED): under
        // TestConfigs.PreReservations the tuple is (1, 26, 2, 26), batch 3's
        // (TheSavePoints_UnderPreReservations_AreBatch3s_AndNoWorldHoldsARow), and the reservations arm of
        // TheSavePoints_MovedForTheCylinderAndIncrementalConstruction_EachLayerAloneMovesItsOwnPin shows the layer alone moves
        // the two save points.
        Assert.Equal((1, 26, 23, 24), SavePoints(run.Worlds));

        // D-055, turn-exact on the run itself: the university's set-aside binds the road orders. World 2 (the turn-1 order's
        // effect) holds every unit of the player's timber set aside and no road row; no road row exists before world 23;
        // world 23 holds exactly the turn-22 order's row.
        var timber = new GoodId(Cfg.Goods!.IdOf("timber"));
        long purseTimber = EmpireStockpile.Available(run.Worlds[2].GoodStocks, ConstructionQuery.Holders(run.Worlds[2], Cfg, Capital(run.Worlds[2])), timber);
        Assert.Equal(185, purseTimber);
        Assert.Contains(new ConstructionHoldRow(Reservations.PolityPurse(Player), timber.Value, purseTimber), Sim.Tests.Systems.TableRows.Rows(run.Worlds[2].ConstructionHolds));
        for (int k = 0; k <= 22; k++) Assert.Equal(0, run.Worlds[k].RoadDevelopments.Count);
        RoadDevelopmentRow road = Assert.Single(Sim.Tests.Systems.TableRows.Rows(run.Worlds[23].RoadDevelopments));
        Assert.Equal((22L, Player, 152L, 0.0), (road.Turn, road.Polity, road.MaterialUnits, road.ProgressBefore));
        Assert.True(road.ProgressAfter is > 0.0 and < 1.0, $"the turn-22 order part-builds its route ({road.ProgressAfter})");
        OrderRecord[] roadOrders = Enumerable.Range(0, run.Log.Count).Select(i => run.Log[i])
            .Where(o => o.ActorId == Player.Value && o.Kind == OrderKind.DevelopRoads).ToArray();
        Assert.Equal([RoadTurn, RoadAgainTurn, 22L], roadOrders.Select(o => o.Turn).ToArray());
    }

    /// <summary>
    /// BATCH 3 / D-054 ATTRIBUTION CONTROL (integration R1, 2026-10-14) for the save-point pin of
    /// <see cref="TheScriptedRun_IssuesEveryOrderKind_AndReachesEverySavePoint"/>, which moved (1, 27, 2, 34) -> (1, 26, 2, 26)
    /// for two layers. The SAME script recorded with one layer off at a time: the cylinder alone moves the first completion
    /// (27 -> 26), WS1 incremental construction alone moves the university's first mid-maturation (34 -> 26), both off is the
    /// batch-2 tuple, and WS2 (allocation ratios) moves no save point.
    ///
    /// D-055 (2026-10-16; batch-4 design §1.7: "add a reservations arm"; MEASURED on this tree by the agent writing this
    /// line) — THE THIRD LAYER, RESERVATIONS (sim.json construction.reservations), moved the shipped tuple (1, 26, 2, 26) ->
    /// (1, 26, 23, 24). The R4 chain strips it with WS1 (TestConfigs.PreIncrementalConstruction switches reservations off
    /// too), so the WS1-off and both-off arms are batch 3's, unchanged; the arms that keep WS1 on moved with it. The arms:
    /// WS1 off (and with it reservations; the cylinder on) (1, 26, 2, 34), unchanged; the cylinder off alone (WS1 and
    /// reservations on) (1, 27, 2, 26) -> (1, 27, 23, 24); both off (1, 27, 2, 34), unchanged — the batch-2 tuple; WS2 off
    /// alone (1, 26, 2, 26) -> (1, 26, 23, 24), the shipped tuple again — WS2 still moves no save point. NEW ARMS:
    /// reservations off alone (TestConfigs.PreReservations) -> (1, 26, 2, 26), batch 3's shipped tuple — the layer alone moves
    /// the partial road 2 -> 23 and the university's maturation 26 -> 24; reservations off on the flat world -> (1, 27, 2, 26),
    /// batch 3's cylinder-off arm — the cylinder still moves only the completion, 27 -> 26.
    /// </summary>
    [Fact]
    public void TheSavePoints_MovedForTheCylinderAndIncrementalConstruction_EachLayerAloneMovesItsOwnPin()
    {
        Rig flat = Shipped with { Dev = TestConfigs.PreCylinderWorldgen(Shipped.Dev), Production = TestConfigs.PreCylinderWorldgen(Shipped.Production) };
        Rig ws1Off = Shipped with { Cfg = TestConfigs.PreIncrementalConstruction(Shipped.Cfg) };
        Rig ws2Off = Shipped with { Cfg = TestConfigs.PreAllocationRatios(Shipped.Cfg) };
        Rig bothOff = flat with { Cfg = TestConfigs.PreIncrementalConstruction(Shipped.Cfg) };
        Rig reservationsOff = Shipped with { Cfg = TestConfigs.PreReservations(Shipped.Cfg) };
        Rig flatReservationsOff = flat with { Cfg = TestConfigs.PreReservations(Shipped.Cfg) };
        Assert.False(Record(flat).Worlds[0].Terrain!.WrapX);
        Assert.False(Reservations.Enabled(ws1Off.Cfg));                       // the R4 chain strips the D-055 layer with WS1
        Assert.True(Reservations.Enabled(ws2Off.Cfg));                        // the single-layer helper keeps it (X2)
        Assert.Equal((1, 26, 2, 34), SavePoints(Record(ws1Off).Worlds));     // the cylinder alone: completion 27 -> 26
        Assert.Equal((1, 27, 23, 24), SavePoints(Record(flat).Worlds));      // WS1 + reservations: maturing 34 -> 24, road 2 -> 23
        Assert.Equal((1, 27, 2, 34), SavePoints(Record(bothOff).Worlds));    // the batch-2 tuple
        Assert.Equal((1, 26, 23, 24), SavePoints(Record(ws2Off).Worlds));    // WS2 moves no save point
        Assert.Equal((1, 26, 2, 26), SavePoints(Record(reservationsOff).Worlds));      // D-055 alone: road 2 -> 23, maturing 26 -> 24
        Assert.Equal((1, 27, 2, 26), SavePoints(Record(flatReservationsOff).Worlds));  // batch 3's cylinder-off arm
    }

    /// <summary>
    /// D-054 WS1 (C1 review F3), turn-exact: the CancelTurn order names the other holding's LAST entry, which state CancelTurn
    /// holds as a non-head entry; state CancelTurn + 1 holds neither it nor its progress row, and every entry between the head
    /// and it is still queued. The kind-12 order is in the log as the player issued it (the up-front validation the battery
    /// runs accepts it; TheScriptedAllocation_… pins its effect).
    /// </summary>
    [Fact]
    public void TheScriptedCancel_RemovesTheNonHeadEntryInTheNextState_TurnExact_AndTheAllocationOrderIsLogged()
    {
        Uninterrupted run = Run.Value;
        WorldState at = run.Worlds[(int)CancelTurn], after = run.Worlds[(int)CancelTurn + 1];
        SettlementId other = OtherHolding(at);
        OrderRecord[] cancels = Enumerable.Range(0, run.Log.Count).Select(i => run.Log[i])
            .Where(o => o.ActorId == Player.Value && o.Kind == OrderKind.CancelConstruction).ToArray();
        OrderRecord cancel = Assert.Single(cancels);
        Assert.Equal((CancelTurn, other.Value), (cancel.Turn, cancel.TargetId));

        ConstructionQueueRow[] before = ConstructionQuery.Queue(at, other);
        Assert.True(before.Length >= 2, "anti-vacuity: the holding has a non-head entry to cancel at CancelTurn");
        ConstructionQueueRow cancelled = before[^1];
        Assert.Equal(cancelled.Slot, (int)cancel.Amount);
        Assert.NotEqual(before[0].Slot, cancelled.Slot);
        Assert.True(ConstructionQuery.ProgressIndex(at.ConstructionProgress, cancelled) >= 0, "the entry has its progress row");

        ConstructionQueueRow[] next = ConstructionQuery.Queue(after, other);
        Assert.DoesNotContain(cancelled, next);
        Assert.Equal(-1, ConstructionQuery.ProgressIndex(after.ConstructionProgress, cancelled));
        for (int i = 1; i < before.Length - 1; i++) Assert.Contains(before[i], next);
        Assert.Equal(before.Length - 1 - (ConstructionQuery.Built(after, other, Granary) - ConstructionQuery.Built(at, other, Granary)), next.Length);

        Assert.Contains(Enumerable.Range(0, run.Log.Count).Select(i => run.Log[i]),
            o => o == OrderRecord.From(AllocationTurn, Player, OrderKind.SetResourceAllocation, Player.Value, AllocationPercent));
    }

    /// <summary>
    /// D-054 WS2, turn-exact: the AllocationTurn order (the player's own split, building 30 shares in 100) is in no state up
    /// to AllocationTurn and is the player's ResourceAllocationRow from state AllocationTurn + 1 on — the step that delivers
    /// it applies it (ConstructionSystem's step 2) — and no other Empire has a row (the AI never sets one).
    /// </summary>
    [Fact]
    public void TheScriptedAllocation_IsThePlayersRowFromTheNextState_TurnExact()
    {
        Uninterrupted run = Run.Value;
        for (int t = 0; t <= (int)AllocationTurn; t++) Assert.Equal(0, run.Worlds[t].ResourceAllocations.Count);
        for (int t = (int)AllocationTurn + 1; t < run.Worlds.Count; t++)
            Assert.Equal(new ResourceAllocationRow(Player, (int)AllocationPercent), Assert.Single(Sim.Tests.Systems.TableRows.Rows(run.Worlds[t].ResourceAllocations)));
        Assert.True(run.Worlds.Count > (int)AllocationTurn + 1, "anti-vacuity: the run reaches the state after the order");
    }

    [Fact]
    public void SaveLoad_BetweenCancelConstructionIssueAndEffect_AndAcrossTheAllocationOrder()
    {
        SaveLoadContinue((int)CancelTurn, "cancel before issue");
        SaveLoadContinue((int)CancelTurn, "cancel issued, not yet delivered", issued: true);
        SaveLoadContinue((int)CancelTurn + 1, "cancel delivered");
        SaveLoadContinue((int)AllocationTurn, "allocation order issued, not yet delivered", issued: true);
        SaveLoadContinue((int)AllocationTurn + 1, "allocation delivered: its row carried by the save");   // D-054 WS2
    }

    [Fact] public void SaveLoad_AtTurnOne() => SaveLoadContinue(1, "turn 1");

    [Fact]
    public void SaveLoad_MidResearch_PartialProgress() =>
        SaveLoadContinue(First((_, w) => PartialProgress(w) > 0.0, "mid-research"), "mid-research");

    [Fact]
    public void SaveLoad_AfterACompletion() =>
        SaveLoadContinue(First((p, w) => Completed(w) > Completed(p), "after a completion"), "after a completion");

    [Fact]
    public void SaveLoad_MidPartialRoadModernization() =>
        SaveLoadContinue(First((_, w) => PartialRoad(w), "partial road"), "partial road");

    /// <summary>Every world another Save* test of this battery saves (with or without the turn's orders issued): turn 1,
    /// the four measured save points, and both sides of the Age, tax, Found Settlement, cancel and allocation orders.
    /// HAND-KEPT: a save test added to this battery later must add the worlds it saves here, or the part-built save point
    /// below may pick a construction state that test already saves.</summary>
    private static int[] OtherSaveTurns() => OtherSaveTurns(Run.Value.Worlds);

    /// <summary><see cref="OtherSaveTurns()"/> for any recorded run of the script (D-055: the PreReservations twin of the
    /// part-built save point measures its own run).</summary>
    private static int[] OtherSaveTurns(List<WorldState> worlds)
    {
        (int research, int completion, int road, int maturing) = SavePoints(worlds);
        return [1, research, completion, road, maturing,
            (int)AgeTurn, (int)AgeTurn + 1, (int)TaxTurn, (int)TaxTurn + 1,
            (int)FoundTurn, (int)FoundTurn + 1, (int)FromColonyTurn, (int)FromColonyTurn + 1,
            (int)CancelTurn, (int)CancelTurn + 1, (int)AllocationTurn, (int)AllocationTurn + 1];
    }

    /// <summary>The part-built save point of a recorded run: the first world holding a part-built head whose
    /// ConstructionProgress table differs from the table of EVERY other save point's world (see below).</summary>
    private static int PartBuiltSavePoint(List<WorldState> worlds)
    {
        int[] others = OtherSaveTurns(worlds);
        bool NewProgressTable(IReadOnlyWorldState w)
        {
            foreach (int o in others) if (SameProgressTable(worlds[o], w)) return false;
            return true;
        }
        return First(worlds, (_, w) => PartBuiltHead(w) && NewProgressTable(w),
            "a part-built construction head whose progress table no other save point saves");
    }

    /// <summary>
    /// Fix round 1 (verifier F4, 2026-10-15): the battery's "after a completion" and "university maturing" save points now
    /// fall on the same world (26), so it saved one fewer distinct state — and none of its save points targeted the state
    /// batch 3 added: a construction head part-built (0 &lt; p &lt; N; D-054 WS1). This save point is a world of the
    /// scripted run holding one, saved, loaded and continued hash-for-hash to the horizon with every order re-issued.
    ///
    /// Final nits (verifier RC1-3, 2026-10-15): fix round 1 took the FIRST such world, world 3 — the world the Age save
    /// (AgeTurn = 3, "age before decision") already saved, loaded and continued identically, so the test added no saved
    /// state. The predicate is now the first part-built-head world that NO OTHER save point of this battery uses
    /// (<see cref="OtherSaveTurns"/>: 1, 2, 3, 4, 5, 6, 26, 35-39). MEASURED on this tree: the capital's military
    /// university (slot 1) is part-built in worlds 3 to 22 (p = 32,777 at world 3; 145,000 at worlds 6 and 7; 958,333 at
    /// world 22) and completes in step 22 → 23; the first of those no other save uses is world 7 — p = 145,000 of
    /// 1,000,000, unchanged since world 6 (the head made no progress in step 6 → 7) and 231,666 at world 8.
    ///
    /// Final round fix 1 (verifier RC1-3-SAMESTATE, 2026-10-15): world 7 is a distinct WORLD, but its ConstructionProgress
    /// table is world 6's row for row (one row: settlement 0, slot 1, project 11, enqueued turn 0, p = 145,000), and world 6
    /// is TaxTurn + 1, which SaveLoad_BetweenTaxOrderIssueAndEffect already saves ("tax in force"). So the construction
    /// state this test exists for was already saved, loaded and continued. The mutant "progress rounded down to 1,000 on
    /// load" passed this test alone (145,000 is a multiple of 1,000). The predicate is now the first part-built-head world
    /// whose ConstructionProgress table differs from the table of EVERY other save point's world. MEASURED on this tree
    /// (a probe dump of every world's table, removed after): worlds 6 and 7 hold p = 145,000; worlds 8 to 10 hold 231,666;
    /// so the save point is world 8 (p = 231,666), whose table no other save world holds. The mutant above now fails here.
    ///
    /// D-055 RE-PIN (2026-10-16; the Director's batch-4 instruction, item 1; batch-4 design §1.7, the battery; VALUE,
    /// MEASURED on this tree by the agent writing this line): the save point stays world 8, its progress 231,666 ->
    /// 306,111. CAUSE: the hold rule (sim.json construction.reservations) — the university keeps the 185 timber the turn-1
    /// road order took in batch 3 (set aside from world 2, see TheScriptedRun_…), so it is part-built from world 3 at
    /// 107,777 (batch 3: 32,777) and every later table moves: worlds 3 and 4 (AgeTurn, AgeTurn + 1) hold 107,777, world 5
    /// (TaxTurn) 182,777, worlds 6 and 7 220,000 (6 is TaxTurn + 1, so world 7's table is again already saved), world 8
    /// 306,111 — the first table no other save world (now 1, 3, 4, 5, 6, 23, 24, 26, 35–39) holds. ATTRIBUTION: under
    /// TestConfigs.PreReservations the same predicate picks world 8 at 231,666, batch 3's
    /// (TheSavePoints_UnderPreReservations_AreBatch3s_AndNoWorldHoldsARow).
    /// </summary>
    [Fact]
    public void SaveLoad_MidPartBuiltConstructionHead()
    {
        int[] others = OtherSaveTurns();
        List<WorldState> worlds = Run.Value.Worlds;
        int k = PartBuiltSavePoint(worlds);
        Assert.Equal(8, k);
        Assert.True(PartBuiltHead(worlds[k]));
        Assert.DoesNotContain(k, others);
        Assert.Equal(306_111L, Assert.Single(Sim.Tests.Systems.TableRows.Rows(worlds[k].ConstructionProgress)).Progress);
        foreach (int o in others) Assert.False(SameProgressTable(worlds[o], worlds[k]), $"world {o} already holds world {k}'s progress table");
        Assert.True(SameProgressTable(worlds[6], worlds[7]), "anti-vacuity: world 7's table is world 6's (the reason world 7 no longer qualifies)");
        SaveLoadContinue(k, "part-built head");
    }

    [Fact]
    public void SaveLoad_MidUniversityMaturation() =>
        SaveLoadContinue(First((_, w) => Maturity(w) is > 0.0 and < 1.0, "university maturing"), "university maturing");

    [Fact]
    public void SaveLoad_BetweenTaxOrderIssueAndEffect()
    {
        // World TaxTurn is the state the order is issued from (it is logged after the load and delivered on the
        // step from TaxTurn); the effect is first visible at TaxTurn + 1 (pinned above). Saving at TaxTurn
        // straddles issue and effect; saving at TaxTurn + 1 straddles the first collection.
        SaveLoadContinue((int)TaxTurn, "tax before issue");
        SaveLoadContinue((int)TaxTurn, "tax issued, not yet in effect", issued: true);
        SaveLoadContinue((int)TaxTurn + 1, "tax in force");
    }

    [Fact]
    public void SaveLoad_AcrossTheAgeTransition()
    {
        SaveLoadContinue((int)AgeTurn, "age before decision");
        SaveLoadContinue((int)AgeTurn, "age decided, not yet entered", issued: true);
        SaveLoadContinue((int)AgeTurn + 1, "age entered");
    }

    [Fact]
    public void SaveLoad_BetweenFoundSettlementIssueAndEffect()
    {
        // M5 S1b (ADR-036). FoundTurn: before the order and issued-not-delivered; FoundTurn + 1: the colony stands
        // with no catchment yet (CatchmentSystem picks it up on the next step); FromColonyTurn: the order FROM the
        // colony issued, not yet delivered; FromColonyTurn + 1: the second colony stands.
        SaveLoadContinue((int)FoundTurn, "found before issue");
        SaveLoadContinue((int)FoundTurn, "found issued, not yet delivered", issued: true);
        SaveLoadContinue((int)FoundTurn + 1, "colony founded, catchment pending");
        SaveLoadContinue((int)FromColonyTurn, "found from the colony issued, not yet delivered", issued: true);
        SaveLoadContinue((int)FromColonyTurn + 1, "second colony founded");
    }

    /// <summary>
    /// D-055 (2026-10-16; the batch-4 design §1.8 #24, review L2-09c) — THE BATTERY VARIANT: the scripted run plus ONE
    /// younger player granary, ordered at turn 1 at the player's other holding, behind the capital's university (slot 1,
    /// the head from world 1, EnqueuedTurn 0). The university is a VALID long wait — it advances at its builders' pace and
    /// waits on materials and viability — so it holds its remaining need of the shared purse every step (1,800 timber and
    /// 1,200 stone; it never holds less than the purse has of either), and the younger granary waits behind it. MEASURED on
    /// this tree (2026-10-16, by the agent writing this line): the granary's entry stands at p = 0 in worlds 2 to 21 — TWENTY
    /// steps — and completes in step 21 → 22, the step the university completes and its leftovers reach the next head in the
    /// same walk. With TestConfigs.PreReservations (the batch-3 rule) the granary is built in step 1 → 2: no wait at all.
    /// The head-of-line wait is the documented cost of the Director's rule (D-055 limitations; m5-playtest-known-issues).
    /// </summary>
    [Fact]
    public void TheBatteryVariant_AYoungerGranaryWaitsBehindTheUniversitysSetAside_TwentySteps_AndUnderPreReservationsBuildsAtOnce()
    {
        static OrderRecord[] Younger(IReadOnlyWorldState w) =>
            w.Clock.Turn == 1 ? [ConstructionQuery.EnqueueOrder(w, Player, OtherHolding(w), Granary)] : [];
        Uninterrupted run = Record(Shipped with { Extra = Younger });
        Uninterrupted pre = Record(Shipped with { Cfg = TestConfigs.PreReservations(Shipped.Cfg), Extra = Younger });
        SettlementId other = OtherHolding(run.Worlds[0]);
        var entry = new ConstructionQueueRow(other, 0, Granary);
        var universityEntry = new ConstructionQueueRow(Capital(run.Worlds[0]), 1, UniversityProject);

        // PREMISE: the batch-3 rule builds the younger granary at once.
        Assert.Equal(1L, ConstructionQuery.Built(pre.Worlds[2], other, Granary));
        Assert.Equal(0, ConstructionQuery.Built(pre.Worlds[1], other, Granary));

        for (int k = 2; k <= 21; k++)
        {
            WorldState w = run.Worlds[k];
            Assert.True(ConstructionQuery.ProgressIndex(w.ConstructionProgress, entry) >= 0, $"world {k}: the granary is queued");
            Assert.Equal(0L, ConstructionQuery.ProgressOf(w, entry));                                   // it waits ...
            Assert.Equal(0L, ConstructionQuery.Built(w, other, Granary));
            Assert.True(ConstructionQuery.ProgressIndex(w.ConstructionProgress, universityEntry) >= 0, $"world {k}: the university is the older head");
            Assert.True(w.ConstructionHolds.Count > 0, $"world {k}: the university sets materials aside");   // ... behind a hold
        }
        WorldState built = run.Worlds[22];
        Assert.Equal(1L, ConstructionQuery.Built(built, other, Granary));                               // twenty steps later
        Assert.Equal(1L, ConstructionQuery.Built(built, Capital(built), UniversityProject));            // in the university's step
        Assert.Equal(0, built.ConstructionHolds.Count);
    }

    /// <summary>
    /// D-055 (2026-10-16; §1.8 #25, X2) — THE SWITCH OFF IS BATCH 3 ON THE BATTERY: the scripted run under
    /// TestConfigs.PreReservations publishes no hold row in any world and reaches batch 3's measured save points
    /// (1, 26, 2, 26) — the tuple TheScriptedRun_IssuesEveryOrderKind_AndReachesEverySavePoint pinned before D-055.
    /// Step 3 (2026-10-16, MEASURED by the agent writing this line): the same holds with the script's D-055 road order once
    /// the university stands (here issued at turn 23, after the batch-3 university completes in step 22 -> 23): the turn-1
    /// order pays batch 3's 185 units (0 -> 0.755) in world 2, and the part-built save point is batch 3's world 8 at
    /// 231,666 — the old values of the three battery pins D-055 moved.
    /// </summary>
    [Fact]
    public void TheSavePoints_UnderPreReservations_AreBatch3s_AndNoWorldHoldsARow()
    {
        Uninterrupted pre = Record(Shipped with { Cfg = TestConfigs.PreReservations(Shipped.Cfg) });
        Assert.False(Reservations.Enabled(TestConfigs.PreReservations(Shipped.Cfg)));
        Assert.True(Reservations.Enabled(Shipped.Cfg));
        foreach (WorldState w in pre.Worlds) Assert.Equal(0, w.ConstructionHolds.Count);
        Assert.Equal((1, 26, 2, 26), SavePoints(pre.Worlds));
        RoadDevelopmentRow first = Assert.Single(Sim.Tests.Systems.TableRows.Rows(pre.Worlds[2].RoadDevelopments));
        Assert.Equal((RoadTurn, 185L, 0.0), (first.Turn, first.MaterialUnits, first.ProgressBefore));
        Assert.True(first.ProgressAfter is > 0.75 and < 0.76, $"batch 3's turn-1 road: {first.ProgressAfter}");
        int k = PartBuiltSavePoint(pre.Worlds);
        Assert.Equal((8, 231_666L), (k, Assert.Single(Sim.Tests.Systems.TableRows.Rows(pre.Worlds[k].ConstructionProgress)).Progress));
        Assert.Contains(Enumerable.Range(0, pre.Log.Count).Select(i => pre.Log[i]),
            o => o.ActorId == Player.Value && o.Kind == OrderKind.DevelopRoads && o.Turn == 23);
    }
}
