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
public class IntegratedSaveLoadBatteryTests
{
    /// <summary>Batch 3 (D-054, integration R1): the battery's configuration — the SimConfig, the dev worldgen the world is
    /// founded with, and the production worldgen the pipeline and the AI are built with. <see cref="Shipped"/> is THE rig;
    /// the save-point attribution control (<see cref="TheSavePoints_MovedForTheCylinderAndIncrementalConstruction_EachLayerAloneMovesItsOwnPin"/>)
    /// records the same script with one batch-3 layer switched off.</summary>
    internal sealed record Rig(SimConfig Cfg, WorldgenConfig Dev, WorldgenConfig Production);

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
        // Road purse rig: the player's timber is cut to a little, through the Ledger, so the turn-0 road
        // order can afford only PART of its first route (the order ends there; the second order continues it).
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
        Assert.Equal((1, 26, 2, 26), SavePoints(run.Worlds));
    }

    /// <summary>
    /// BATCH 3 / D-054 ATTRIBUTION CONTROL (integration R1, 2026-10-14) for the save-point pin of
    /// <see cref="TheScriptedRun_IssuesEveryOrderKind_AndReachesEverySavePoint"/>, which moved (1, 27, 2, 34) -> (1, 26, 2, 26)
    /// for two layers. The SAME script recorded with one layer off at a time: the cylinder alone moves the first completion
    /// (27 -> 26), WS1 incremental construction alone moves the university's first mid-maturation (34 -> 26), both off is the
    /// batch-2 tuple, and WS2 (allocation ratios) moves no save point.
    /// </summary>
    [Fact]
    public void TheSavePoints_MovedForTheCylinderAndIncrementalConstruction_EachLayerAloneMovesItsOwnPin()
    {
        Rig flat = Shipped with { Dev = TestConfigs.PreCylinderWorldgen(Shipped.Dev), Production = TestConfigs.PreCylinderWorldgen(Shipped.Production) };
        Rig ws1Off = Shipped with { Cfg = TestConfigs.PreIncrementalConstruction(Shipped.Cfg) };
        Rig ws2Off = Shipped with { Cfg = TestConfigs.PreAllocationRatios(Shipped.Cfg) };
        Rig bothOff = flat with { Cfg = TestConfigs.PreIncrementalConstruction(Shipped.Cfg) };
        Assert.False(Record(flat).Worlds[0].Terrain!.WrapX);
        Assert.Equal((1, 26, 2, 34), SavePoints(Record(ws1Off).Worlds));     // the cylinder alone: completion 27 -> 26
        Assert.Equal((1, 27, 2, 26), SavePoints(Record(flat).Worlds));       // WS1 alone: university maturing 34 -> 26
        Assert.Equal((1, 27, 2, 34), SavePoints(Record(bothOff).Worlds));    // the batch-2 tuple
        Assert.Equal((1, 26, 2, 26), SavePoints(Record(ws2Off).Worlds));     // WS2 moves no save point
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
    private static int[] OtherSaveTurns()
    {
        (int research, int completion, int road, int maturing) = SavePoints(Run.Value.Worlds);
        return [1, research, completion, road, maturing,
            (int)AgeTurn, (int)AgeTurn + 1, (int)TaxTurn, (int)TaxTurn + 1,
            (int)FoundTurn, (int)FoundTurn + 1, (int)FromColonyTurn, (int)FromColonyTurn + 1,
            (int)CancelTurn, (int)CancelTurn + 1, (int)AllocationTurn, (int)AllocationTurn + 1];
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
    /// </summary>
    [Fact]
    public void SaveLoad_MidPartBuiltConstructionHead()
    {
        int[] others = OtherSaveTurns();
        List<WorldState> worlds = Run.Value.Worlds;
        bool NewProgressTable(IReadOnlyWorldState w)
        {
            foreach (int o in others) if (SameProgressTable(worlds[o], w)) return false;
            return true;
        }
        int k = First((_, w) => PartBuiltHead(w) && NewProgressTable(w),
            "a part-built construction head whose progress table no other save point saves");
        Assert.Equal(8, k);
        Assert.True(PartBuiltHead(worlds[k]));
        Assert.DoesNotContain(k, others);
        Assert.Equal(231_666L, Assert.Single(Sim.Tests.Systems.TableRows.Rows(worlds[k].ConstructionProgress)).Progress);
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
}
