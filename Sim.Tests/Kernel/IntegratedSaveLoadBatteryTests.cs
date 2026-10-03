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
/// development, a tax rate and an Age advance (eligibility rig-forced). Rigs touch turn 0 only and move
/// conserved stocks through the Ledger.
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
    private static readonly SimConfig Cfg = UniversityRigs.Cfg(adultsPerUniversity: 100);
    private static readonly ResearchContent Research = TestConfigs.Research();
    private static readonly AgeContent Ages = TestConfigs.Ages();
    private static readonly PolityId Player = new(1);
    private static readonly PolityId Rival = new(2);
    private const int Horizon = 40;
    private const long RoadTurn = 1, RoadAgainTurn = 12, AgeTurn = 3, TaxTurn = 5, ClearTurn = 9, RetargetTurn = 10;

    private static int UniversityProject => InstitutionContent.ProjectOfType(Cfg, 1)!.Id;
    private const int Granary = 1;
    private const long TimberLeft = 20;

    private static TurnExecutor Production(OrderLog orders)
    {
        using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
        using var pipeStream = Sim.Data.DataFiles.OpenPipeline();
        return new TurnExecutor(EraTableLoader.Load(eraStream),
            PipelineLoader.Load(pipeStream, SystemCatalog.All(Cfg, TestConfigs.Worldgen())), orders);
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
    /// military milestone), and the capital holds the university's materials.</summary>
    private static WorldState Start()
    {
        WorldState w = WorldFounding.Found(TestConfigs.DevWorldgen() with { AiEmpires = 1 }, Cfg, 42);
        var known = new List<string>(WithAncestors("track_road"));
        known.AddRange(WithAncestors(UniversityRigs.Knowledge));
        known.AddRange(WithAncestors(GovernanceRigs.TaxationNode, "cereal_cultivation", "pottery_open_fired"));
        UniversityRigs.Grant(w, Research, Player, known.Distinct().ToArray());
        UniversityRigs.Materials(w, Cfg, Capital(w));
        // Road purse rig: the player's timber is cut to a little, through the Ledger, so the turn-0 road
        // order can afford only PART of its first route (the order ends there; the second order continues it).
        var timber = new GoodId(Cfg.Goods!.IdOf("timber"));
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
    private static void PlayerOrders(OrderLog log, IReadOnlyWorldState w)
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
        if (t == AgeTurn) log.Append(AgeQuery.AdvanceOrder(w, Player, 2, Ages.Surges[0].Key));
        if (t == TaxTurn) log.Append(Governance.TaxOrder(t, Player, 10.0));
        if (t == ClearTurn) log.Append(ResearchQuery.ClearTargetOrder(w, Player));
        if (t == RetargetTurn)
        {
            int current = -1;
            for (int i = 0; i < w.ResearchProgress.Count; i++)
                if (w.ResearchProgress[i].Polity == Player) { current = w.ResearchProgress[i].Node.Value; break; }
            if (FirstTarget(w, current) is { } o) log.Append(o);
        }
    }

    private static void OneTurnOfOrders(OrderLog log, IReadOnlyWorldState w)
    {
        PlayerOrders(log, w);
        AiOrders.Append(log, w, Cfg);
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

    internal static readonly Lazy<Uninterrupted> Run = new(() =>
    {
        var log = new OrderLog();
        TurnExecutor exec = Production(log);
        WorldState w = Start();
        var worlds = new List<WorldState> { w.Clone() };
        var hashes = new List<string> { WorldHash.ComputeHex(w) };
        for (int t = 0; t < Horizon; t++)
        {
            OneTurnOfOrders(log, w);
            w = exec.Step(w);
            worlds.Add(w.Clone());
            hashes.Add(WorldHash.ComputeHex(w));
        }
        return new Uninterrupted(worlds, hashes, log);
    }, LazyThreadSafetyMode.ExecutionAndPublication);

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

        TurnExecutor exec = Production(log);
        WorldState w = loaded;
        for (int t = k; t < Horizon; t++)
        {
            if (issued && t == k) AiOrders.Append(log, w, Cfg);
            else OneTurnOfOrders(log, w);
            w = exec.Step(w);
            Assert.True(run.Hashes[t + 1] == WorldHash.ComputeHex(w), $"{label} (saved at {k}): diverged at turn {t + 1}");
        }
        Assert.Equal(run.Log.Count, log.Count);
        for (int i = 0; i < log.Count; i++) Assert.Equal(run.Log[i], log[i]);
    }

    private static int First(Func<IReadOnlyWorldState, IReadOnlyWorldState, bool> at, string what)
    {
        List<WorldState> ws = Run.Value.Worlds;
        for (int k = 1; k < ws.Count - 1; k++) if (at(ws[k - 1], ws[k])) return k;
        Assert.Fail($"the run never reached the save point '{what}' within {Horizon} turns");
        return -1;
    }

    [Fact]
    public void TheScriptedRun_IssuesEveryOrderKind_AndReachesEverySavePoint()
    {
        Uninterrupted run = Run.Value;
        var kinds = new HashSet<OrderKind>();
        for (int i = 0; i < run.Log.Count; i++) if (run.Log[i].ActorId == Player.Value) kinds.Add(run.Log[i].Kind);
        foreach (OrderKind k in new[] { OrderKind.LaborAllocation, OrderKind.SectorAllocation, OrderKind.EnqueueConstruction,
                     OrderKind.SetTaxRate, OrderKind.SetResearchTarget, OrderKind.AdvanceAge, OrderKind.DevelopRoads })
            Assert.Contains(k, kinds);
        Assert.Contains(Enumerable.Range(0, run.Log.Count).Select(i => run.Log[i]),
            o => o.ActorId == Player.Value && o.Kind == OrderKind.SetResearchTarget && o.TargetId == -1);
        Assert.Contains(Enumerable.Range(0, run.Log.Count).Select(i => run.Log[i]), o => o.ActorId == Rival.Value);

        // The order-delivery semantics the save points straddle, turn-exact.
        Assert.Equal(1, AgeQuery.CurrentAge(run.Worlds[(int)AgeTurn], Ages, Player));
        Assert.Equal(2, AgeQuery.CurrentAge(run.Worlds[(int)AgeTurn + 1], Ages, Player));
        Assert.Equal(0.0, TaxRate(run.Worlds[(int)TaxTurn]));
        Assert.True(TaxRate(run.Worlds[(int)TaxTurn + 1]) > 0.0);
        Assert.Equal(0.10, TaxRate(run.Worlds[(int)TaxTurn + 1]), 12);

        // The save points, MEASURED on this tree (dev world, seed 42): partial research progress from turn 1;
        // the player's first completion at 28; the turn-1 road order affords 62% of its first route (turn 2);
        // the university (queued behind the granary on the timber-cut capital) is first mid-maturation at 32.
        Assert.Equal((1, 28, 2, 32), (
            First((_, w) => PartialProgress(w) > 0.0, "mid-research"),
            First((p, w) => Completed(w) > Completed(p), "after a completion"),
            First((_, w) => PartialRoad(w), "partial road"),
            First((_, w) => Maturity(w) is > 0.0 and < 1.0, "university maturing")));
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
}
