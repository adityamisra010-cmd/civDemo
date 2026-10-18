using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;

namespace Sim.Core.Observability;

/// <summary>
/// D-055 / ADR-040 (batch 4, item 3) — THE SPLIT REPORT'S OBSERVER (`sim run --split-report PATH`). A READ-ONLY
/// <see cref="ITurnObserver"/>: it copies the stocks the executor's read-only window shows after each phase, and after the
/// step writes one <see cref="SplitReportRow"/> per roster Empire whose building/roads split applies (the switch on, and the
/// Empire controls a place) — the D-054 WS2 split and the D-055 holds as ConstructionSystem computed them, next to what the
/// construction and road development phases ACTUALLY drew and published.
/// <list type="bullet">
/// <item>THE RULE'S FIGURES come from the very State functions ConstructionSystem's step calls, on the same inputs: PREV,
///   the step's batch, the stocks just before the <c>construction</c> phase, and the step's dt (NEXT's clock):
///   <see cref="ConstructionQuery.AllocationsWith"/> (step 2's split rows), <see cref="ConstructionQuery.PendingBook"/> (the
///   intake's queue and progress, steps 3–4), <see cref="ConstructionQuery.StepAllocations"/> and
///   <see cref="ConstructionQuery.GrantsLeft"/> (step 5), <see cref="ConstructionQuery.HoldsWalk"/> (step 6 — every head's
///   draws, set-asides, cause and holder) and, per Empire, <see cref="ConstructionQuery.AllocationFor"/> (S, the shares, D_b,
///   D_r, the grant and the forecast).</item>
/// <item>THE ACTUALS are measured, not computed: building work's draws and road works' draws are the Empire purse's fall
///   across the <c>construction</c> and <c>roaddevelopment</c> phases
///   (<see cref="ConstructionQuery.EmpirePursePlaces"/> of <see cref="RoadDevelopmentQuery.PayingSettlements(IReadOnlyWorldState, PolityId)"/>:
///   the split's own purse), and its set-asides are the ConstructionHolds rows of its purse as construction published
///   them.</item>
/// </list>
/// It REFUSES a world that was not founded (no terrain: the M0 toy world has no Empire to split for), a configuration
/// with the split off, and one with the D-055 hold rule off (`construction.reservations`; fixer round, finding
/// T3-L2-T3-V2-F2: without set-asides a head held back by another good draws less of this one, so build + holds = G_b —
/// <see cref="SplitReportCheck"/>'s P2 — is not a property of such a run; every report is a D-055 report), and it THROWS if a step completes without its having seen both the <c>construction</c> and the
/// <c>roaddevelopment</c> phase exactly once — a renamed or missing phase would otherwise zero the actuals silently. It never
/// writes world state (its window is <see cref="IReadOnlyWorldState"/>), so a run's hashes and saves are identical with and
/// without it. Arrays and tables walked in index order; no dictionary; checked long arithmetic in the purse sums.
/// </summary>
public sealed class SplitProbe : ITurnObserver
{
    /// <summary>The pipeline name of ConstructionSystem's phase (pipeline.json).</summary>
    public const string ConstructionPhase = Systems.Construction.ConstructionSystem.Name;

    /// <summary>The pipeline name of RoadDevelopmentSystem's phase (pipeline.json).</summary>
    public const string RoadPhase = Systems.Roads.RoadDevelopmentSystem.Name;

    private readonly Stream _output;
    private readonly SimConfig _cfg;
    private IReadOnlyWorldState? _prev;
    private OrderRecord[] _orders = [];
    private Table<GoodStockRow>? _last, _beforeBuild, _afterBuild, _beforeRoad, _afterRoad;
    private Table<ConstructionHoldRow>? _holds;

    /// <summary>The rows written so far.</summary>
    public long RowsWritten { get; private set; }

    /// <param name="output">The report stream (the caller owns it).</param>
    /// <param name="cfg">The simulation configuration the executor's systems were built from.</param>
    public SplitProbe(Stream output, SimConfig cfg)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(cfg);
        if (!ResourceAllocation.Enabled(cfg) || cfg.Goods is null)
            throw new InvalidOperationException(
                "split report: the building/roads split is off in this configuration (construction.allocationRatios), so " +
                "there is no split to report.");
        if (!Reservations.Enabled(cfg))
            throw new InvalidOperationException(
                "split report: the D-055 hold rule is off in this configuration (construction.reservations), so building " +
                "work's set-asides are not the rule's and split-check's P2 (build + holds = G_b) would not hold; the report " +
                "is of D-055 runs only.");
        _output = output;
        _cfg = cfg;
    }

    /// <summary>One observed step: <see cref="Begin"/>, <paramref name="executor"/>'s
    /// <see cref="TurnExecutor.Step(WorldState, ITurnObserver?)"/> with this observer, <see cref="End"/>.
    /// <paramref name="batch"/> must be the batch the executor delivers this step (its order log's batch for PREV's
    /// turn).</summary>
    public WorldState Step(TurnExecutor executor, WorldState prev, OrderBatch batch)
    {
        ArgumentNullException.ThrowIfNull(executor);
        Begin(prev, batch);
        WorldState next = executor.Step(prev, this);
        End(next);
        return next;
    }

    /// <summary>Starts observing the step from <paramref name="prev"/>, which delivers <paramref name="batch"/>. Refuses a
    /// world that was not founded.</summary>
    public void Begin(IReadOnlyWorldState prev, OrderBatch batch)
    {
        ArgumentNullException.ThrowIfNull(prev);
        if (prev.Terrain is null)
            throw new InvalidOperationException(
                "split report: the world was not founded (it has no terrain — the M0 toy world), so it has no Empire whose " +
                "building/roads split could be reported. Run a founded world (--founded, or a founded save).");
        _prev = prev;
        _orders = RoadDevelopmentQuery.Orders(batch);
        _last = _beforeBuild = _afterBuild = _beforeRoad = _afterRoad = null;
        _holds = null;
    }

    /// <summary>Timing is not read.</summary>
    public void OnPhase(string phase, long elapsedTimestampTicks, long allocatedBytes) { }

    /// <summary>Copies the window's stocks after every phase; around <see cref="ConstructionPhase"/> and
    /// <see cref="RoadPhase"/> it keeps the copies before and after, and the holds construction published.</summary>
    public void OnPhaseState(string phase, IReadOnlyWorldState next)
    {
        if (_prev is null) return;   // not begun: an executor stepping with this observer outside Step/Begin
        Table<GoodStockRow> now = CopyStocks(next.GoodStocks);
        if (phase == ConstructionPhase)
        {
            if (_afterBuild is not null)
                throw new InvalidOperationException($"split report: the pipeline runs a '{ConstructionPhase}' phase twice.");
            _beforeBuild = _last;
            _afterBuild = now;
            _holds = CopyHolds(next.ConstructionHolds);
        }
        else if (phase == RoadPhase)
        {
            if (_afterRoad is not null)
                throw new InvalidOperationException($"split report: the pipeline runs a '{RoadPhase}' phase twice.");
            _beforeRoad = _last;
            _afterRoad = now;
        }
        _last = now;
    }

    /// <summary>Ends the step that produced <paramref name="next"/>: writes its rows. Throws when the step ran no
    /// <see cref="ConstructionPhase"/> or no <see cref="RoadPhase"/>.</summary>
    public void End(IReadOnlyWorldState next)
    {
        ArgumentNullException.ThrowIfNull(next);
        if (_prev is not { } prev)
            throw new InvalidOperationException("split report: End without Begin.");
        if (_beforeBuild is null || _afterBuild is null || _holds is null)
            throw new InvalidOperationException(
                $"split report: the step from turn {prev.Clock.Turn} ran no '{ConstructionPhase}' phase, so building work's " +
                "draws and set-asides could not be measured (a renamed or missing pipeline phase).");
        if (_beforeRoad is null || _afterRoad is null)
            throw new InvalidOperationException(
                $"split report: the step from turn {prev.Clock.Turn} ran no '{RoadPhase}' phase, so road works' draws could " +
                "not be measured (a renamed or missing pipeline phase).");
        SplitReportRow[] rows = Rows(prev, _cfg, _orders, next.Clock.DtYears, _beforeBuild, _afterBuild, _holds,
            _beforeRoad, _afterRoad);
        for (int i = 0; i < rows.Length; i++) SplitReport.Write(_output, rows[i]);
        RowsWritten += rows.Length;
        _prev = null;
    }

    /// <summary>
    /// THE ROWS OF ONE STEP (pure; see the class header): one per roster Empire of <paramref name="prev"/> whose split
    /// applies (<see cref="ConstructionQuery.AllocationFor"/> not null), in roster order. <paramref name="orders"/> is the
    /// step's batch, <paramref name="dtYears"/> its dt; the stock tables are the window's copies before and after the
    /// construction and road development phases, and <paramref name="holds"/> the ConstructionHolds construction published.
    /// </summary>
    public static SplitReportRow[] Rows(
        IReadOnlyWorldState prev, SimConfig cfg, IReadOnlyList<OrderRecord> orders, double dtYears,
        IReadOnlyTable<GoodStockRow> beforeBuild, IReadOnlyTable<GoodStockRow> afterBuild,
        IReadOnlyTable<ConstructionHoldRow> holds, IReadOnlyTable<GoodStockRow> beforeRoad,
        IReadOnlyTable<GoodStockRow> afterRoad)
    {
        GoodsConfig goods = cfg.Goods ?? throw new InvalidOperationException("split report: no goods content.");
        // ConstructionSystem's steps 2–6 on the same inputs: the split rows the batch leaves, the post-intake book, each
        // Empire's grants, and THE holds walk.
        Table<ResourceAllocationRow> allocations = ConstructionQuery.AllocationsWith(prev, cfg, prev.ResourceAllocations, orders);
        IReadOnlyTable<ConstructionQueueRow> queue =
            ConstructionQuery.PendingBook(prev, cfg, orders, out IReadOnlyTable<ConstructionProgressRow> progress);
        long[]?[] grants = ConstructionQuery.GrantsLeft(
            ConstructionQuery.StepAllocations(prev, cfg, queue, progress, beforeBuild, allocations, orders, dtYears));
        HoldsWalkResult walk = ConstructionQuery.HoldsWalk(prev, cfg, queue, progress, beforeBuild, dtYears, grants);

        var rows = new List<SplitReportRow>();
        for (int k = 0; k < prev.Polities.Count; k++)
        {
            PolityId polity = prev.Polities[k].Id;
            int b = ResourceAllocation.EffectivePercent(allocations, polity);
            OrderRecord? road = RoadDevelopmentQuery.DeliveredOrderFor(prev, cfg, orders, polity);
            EmpireAllocation? split = ConstructionQuery.AllocationFor(
                prev, cfg, polity, queue, progress, beforeBuild, b, road, dtYears);
            if (split is null) continue;

            SettlementId[] purse = ConstructionQuery.EmpirePursePlaces(
                prev, cfg, polity, RoadDevelopmentQuery.PayingSettlements(prev, polity));
            int key = Reservations.PolityPurse(polity);
            int walked = walk.IndexOfPurse(key);

            var figures = new SplitGoodFigures[split.Goods.Length];
            for (int i = 0; i < split.Goods.Length; i++)
            {
                int g = split.Goods[i];
                AllocationSplit s = split.Splits[i];
                var id = new GoodId(g);
                long built = checked(EmpireStockpile.Available(beforeBuild, purse, id) - EmpireStockpile.Available(afterBuild, purse, id));
                long paved = checked(EmpireStockpile.Available(beforeRoad, purse, id) - EmpireStockpile.Available(afterRoad, purse, id));
                long? start = walked >= 0 && g < walk.Start[walked].Length ? walk.Start[walked][g] : null;
                figures[i] = new SplitGoodFigures(
                    goods.ById(g).Name, g, s.Supply, start, s.BuildingShare, s.RoadShare, s.BuildingDemand, s.RoadDemand,
                    s.BuildingCap, s.BuildingGrant, s.HoldsBack, split.BuildingDrawOf(g), built, split.BuildingHoldOf(g),
                    HeldIn(holds, key, g), split.RoadChargeOf(g), paved);
            }

            var heads = new List<SplitHeadFigures>();
            for (int h = 0; h < walk.Heads.Length; h++)
                if (walk.Heads[h].Purse == key) heads.Add(Head(walk.Heads[h], goods, progress));

            rows.Add(new SplitReportRow(prev.Clock.Turn, polity.Value, b, road?.Amount, split.RoadPaysNothingFromItsShare,
                figures, [.. heads]));
        }
        return [.. rows];
    }

    /// <summary>One walked head's figures: its project's input goods, ascending id, each with its need at the step's start,
    /// its budget, draw and set-aside — and, for its binding good only, the walk's cause, holder and set-aside ahead.</summary>
    private static SplitHeadFigures Head(HoldsWalkHead head, GoodsConfig goods, IReadOnlyTable<ConstructionProgressRow> progress)
    {
        HeadResolution r = head.Resolution;
        var ids = new List<int>();
        if (r.Project is { } project)
            for (int i = 0; i < project.Inputs.Length; i++)
            {
                int g = goods.IdOf(project.Inputs[i].Good);
                if (g >= 0 && !ids.Contains(g)) ids.Add(g);
            }
        ids.Sort();
        var figures = new SplitHeadGood[ids.Count];
        for (int j = 0; j < figures.Length; j++)
        {
            int g = ids[j];
            long need = 0;
            for (int i = 0; i < r.Project!.Inputs.Length; i++)
                if (goods.IdOf(r.Project.Inputs[i].Good) == g)
                    need = checked(need + ConstructionQuery.Remaining(r.Project.Inputs[i].Qty, r.ProgressBefore));
            long budget = g < head.ResolvedAgainst.Length ? head.ResolvedAgainst[g] : long.MaxValue;
            bool binding = g == r.LimitGood;
            figures[j] = new SplitHeadGood(
                goods.ById(g).Name, g, need, budget == long.MaxValue ? null : budget, head.DrawOf(g), head.HoldOf(g),
                binding ? head.Cause.ToString() : nameof(MaterialCause.None),
                binding && head.Holder is { } holder ? new SplitHeadRef(holder.Settlement.Value, holder.Slot, holder.ProjectId) : null,
                binding ? head.SetAsideAhead : 0);
        }
        return new SplitHeadFigures(
            head.Entry.Settlement.Value, head.Entry.Slot, head.Entry.ProjectId,
            ConstructionQuery.EnqueuedTurnOf(progress, head.Entry), r.ProgressBefore, r.ProgressAfter,
            head.Release.ToString(), r.Limit.ToString(), figures);
    }

    private static long HeldIn(IReadOnlyTable<ConstructionHoldRow> holds, int purse, int good)
    {
        long units = 0;
        for (int i = 0; i < holds.Count; i++)
            if (holds[i].Purse == purse && holds[i].Good == good) units = checked(units + holds[i].Units);
        return units;
    }

    private static Table<GoodStockRow> CopyStocks(IReadOnlyTable<GoodStockRow> rows)
    {
        var t = new Table<GoodStockRow>(rows.Count);
        for (int i = 0; i < rows.Count; i++) t.Add(rows[i]);
        return t;
    }

    private static Table<ConstructionHoldRow> CopyHolds(IReadOnlyTable<ConstructionHoldRow> rows)
    {
        var t = new Table<ConstructionHoldRow>(rows.Count);
        for (int i = 0; i < rows.Count; i++) t.Add(rows[i]);
        return t;
    }
}
