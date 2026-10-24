using Sim.Core.State;
using Sim.Core.Systems.ClassMobility;

namespace Sim.Core.Systems.Research;

/// <summary>ADR-048: why a node is on a destination's route (tranche-2 contract §3). <see cref="Sweep"/> carries its cohort
/// Age in <see cref="RouteBlocker.SweepAge"/>; a sweep node is never labelled a prerequisite.</summary>
public enum RouteReason
{
    /// <summary>An authored prerequisite ancestor of the destination (or, for a recursive node, its subtree's finite set).</summary>
    Prerequisite = 1,
    /// <summary>Required by the Age sweep: a cohort node of Age k (or an ancestor of one) that must complete first.</summary>
    Sweep = 2,
    /// <summary>Required to reach the research stage that opens the subtrees.</summary>
    Stage = 3,
    /// <summary>A node of the OTHER lane while that lane works a destination of its own: this lane cannot fund it.</summary>
    OtherLane = 4,
}

/// <summary>ADR-048: one outstanding node of a route. <see cref="Reason"/> is what the UI shows (<see cref="Cause"/>, or
/// <see cref="RouteReason.OtherLane"/> when the other lane is busy); <see cref="Cause"/> is the structural reason;
/// <see cref="SweepAge"/> the cohort Age k when the cause is the sweep (else 0); <see cref="Eligible"/> whether it is
/// available now.</summary>
public readonly record struct RouteBlocker(int Index, ResearchNodeId Key, RouteReason Reason, RouteReason Cause, int SweepAge, bool Eligible);

/// <summary>ADR-048: a planned route — the minimum-total-remaining-cost SET of incomplete nodes (ascending dense index,
/// the destination excluded) whose completion makes <see cref="Destination"/> eligible, each with its structural reason.
/// <see cref="RemainingCost"/> = Σ remaining EffectiveCost over the route and the destination (ascending index order).</summary>
public sealed record ResearchRoutePlan(
    int Destination, int[] Route, RouteReason[] Cause, int[] SweepAge, double RemainingCost, bool Exact, long Combinations);

/// <summary>ADR-048: what one lane funds this step — its destination (-1: none), the CURRENT node it funds (-1: none), the
/// plan when the destination is not available yet, and whether it funds the other lane's route (free-lane assist).</summary>
public sealed record LaneFunding(int Lane, int Destination, int Current, ResearchRoutePlan? Plan, bool Assisting);

/// <summary>ADR-048: <see cref="ResearchQuery.Route"/>'s answer for one lane.</summary>
public sealed record ResearchRoute(
    ResearchTree Lane, ResearchNodeId? Destination, ResearchNodeId? Current, ResearchNodeId? Next,
    IReadOnlyList<ResearchNodeId> Ordered, IReadOnlyList<RouteBlocker> Blockers, double RemainingCost,
    bool DestinationAvailable, bool Feasible, bool Assisting, ResearchNodeId? AssistedDestination, bool Exact, long Combinations)
{
    /// <summary>The blockers of one reason (and, for <see cref="RouteReason.Sweep"/>, one cohort Age when
    /// <paramref name="sweepAge"/> &gt; 0), in route order.</summary>
    public List<RouteBlocker> BlockersOf(RouteReason reason, int sweepAge = 0)
    {
        var result = new List<RouteBlocker>();
        foreach (RouteBlocker b in Blockers)
            if (b.Reason == reason && (sweepAge == 0 || b.SweepAge == sweepAge)) result.Add(b);
        return result;
    }

    public static ResearchRoute None(ResearchTree lane) =>
        new(lane, null, null, null, [], [], 0.0, false, true, false, null, true, 1);
}

/// <summary>
/// ADR-048 — THE DESTINATION ROUTE PLANNER (tranche-2 contract §3). Pure and deterministic: it reads the content, the
/// polity's completed mask and its remaining-cost vector (<see cref="ResearchQuery.RemainingCosts"/>), never a clock, an
/// RNG, a dictionary or LINQ.
///
/// <para><b>The route</b> of a destination D is the minimum-total-remaining-EffectiveCost SET of incomplete nodes whose
/// completion makes D eligible (<see cref="ResearchQuery.IsAvailable(ResearchContent, int, bool[], bool)"/>). A node's
/// requirements are its authored prerequisite expression (AND / OR), the stage gate when it is in a subtree and the stage
/// is not reached, a recursive node's finite subtree, and — with tuning.ageSweep — every outstanding cohort node of Age
/// a − 2 (both trees); each required node brings its own requirements (cross-tree prerequisites included). A node shared
/// by several requirements is counted ONCE (the route is a set).</para>
///
/// <para><b>Exact, then greedy.</b> The OR groups (an n-ary OR not already satisfied) reachable from D over every
/// alternative are numbered in breadth-first discovery order. When the product of their alternative counts is at most
/// <see cref="ExactCombinationLimit"/> (4,096) every combination is enumerated (lexicographic, the first group most
/// significant, each group's alternatives in listed order) and the cheapest closure wins — composite key (total remaining
/// cost, the ascending key sequence compared lexicographically, enumeration position). Otherwise the route is greedy:
/// each OR takes the alternative whose own closure is cheapest, ties to the alternative listed first — (closure cost,
/// listed position). Work is bounded by 4,096 closures of at most N nodes each (N = content nodes).</para>
///
/// <para><b>Funding</b> (<see cref="Fund"/>): an available destination is funded itself; otherwise the lane funds its
/// CURRENT node — the eligible route node of its own lane with the smallest composite key (remaining EffectiveCost, key).
/// A route node of the other lane is funded by that lane only while it has no destination of its own (free-lane assist);
/// otherwise it is an otherLane blocker.</para>
/// </summary>
public static class ResearchRoutePlanner
{
    /// <summary>The exact search's bound on OR combinations (contract §3).</summary>
    public const long ExactCombinationLimit = 4096;

    /// <summary>ADR-043: the lane slot of a node — 1 (Civics, CP-funded) for a civic in the two-lane model, else 0.</summary>
    public static int LaneOf(ResearchContent content, int index, bool twoLanes) =>
        twoLanes && content.Nodes[index].Tree == ResearchTree.Civics ? 1 : 0;

    // ------------------------------------------------------------------ funding (ResearchSystem and the query)

    /// <summary>
    /// What each lane funds this step: per lane slot (one slot in the one-pool model), the destination itself when it is
    /// available, else the CURRENT node of its route (<see cref="PickCurrent"/>); then a lane with no destination funds the
    /// other lane's route (free-lane assist). ResearchSystem and <see cref="ResearchQuery.Route"/> both call this, so the
    /// screen's "current research" is by construction what the step funds.
    /// </summary>
    public static LaneFunding[] Fund(ResearchContent content, bool[] completed, bool[] available, double[] remaining,
        int[] destinations, bool twoLanes)
    {
        int lanes = twoLanes ? 2 : 1;
        var plans = new ResearchRoutePlan?[2];
        var current = new[] { -1, -1 };
        var assisting = new bool[2];
        for (int lane = 0; lane < lanes; lane++)
        {
            int d = destinations[lane];
            if (d < 0) continue;
            if (available[d]) { current[lane] = d; continue; }
            plans[lane] = Plan(content, completed, remaining, d);
            current[lane] = PickCurrent(content, plans[lane]!.Route, available, remaining, lane, twoLanes, -1);
        }
        if (twoLanes)
            for (int lane = 0; lane < 2; lane++)
            {
                if (destinations[lane] >= 0 || plans[1 - lane] is not { } other) continue;
                current[lane] = PickCurrent(content, other.Route, available, remaining, lane, twoLanes, -1);
                assisting[lane] = current[lane] >= 0;
            }
        var result = new LaneFunding[lanes];
        for (int lane = 0; lane < lanes; lane++)
            result[lane] = new LaneFunding(lane, destinations[lane], current[lane], plans[lane], assisting[lane]);
        return result;
    }

    /// <summary>The eligible route node of <paramref name="lane"/> with the smallest composite key (remaining EffectiveCost,
    /// key), skipping <paramref name="except"/>; -1 when none. An ascending-index (= ascending-key) scan replacing only on a
    /// strictly lower cost, so bit-equal costs go to the lower key.</summary>
    public static int PickCurrent(ResearchContent content, int[] route, bool[] available, double[] remaining, int lane,
        bool twoLanes, int except)
    {
        int best = -1;
        for (int k = 0; k < route.Length; k++)
        {
            int i = route[k];
            if (i == except || !available[i] || LaneOf(content, i, twoLanes) != lane) continue;
            if (best < 0 || remaining[i] < remaining[best]) best = i;
        }
        return best;
    }

    /// <summary>
    /// The full report of one lane (<see cref="ResearchQuery.Route"/>): <see cref="Fund"/>'s choice, the NEXT node (what
    /// the lane would fund once its current node is complete), the ordered route (the research order the planner's rule
    /// produces when every route node is funded as it becomes eligible), the blockers with their reasons and the
    /// remaining cost. <paramref name="lane"/> selects the slot (Civics → 1 in the two-lane model, else 0).
    /// </summary>
    public static ResearchRoute Describe(ResearchContent content, bool[] completed, bool[] available, double[] remaining,
        int[] destinations, bool twoLanes, ResearchTree lane)
    {
        int slot = twoLanes && lane == ResearchTree.Civics ? 1 : 0;
        LaneFunding[] funding = Fund(content, completed, available, remaining, destinations, twoLanes);
        LaneFunding f = funding[slot];
        ResearchRoutePlan? plan = f.Plan ?? (f.Assisting ? funding[1 - slot].Plan : null);
        int destination = f.Assisting ? funding[1 - slot].Destination : f.Destination;
        if (destination < 0) return ResearchRoute.None(lane);

        ResearchNodeId? Key(int i) => i >= 0 ? content.Nodes[i].Key : null;
        if (plan is null)   // the destination is available: it is funded itself
            return new ResearchRoute(lane, Key(destination), Key(f.Current), null, [], [], remaining[destination],
                true, true, false, null, true, 1);

        // NEXT: what this lane funds once its current node is complete (the route minus that node).
        int next = -1;
        if (f.Current >= 0)
        {
            bool[] done = (bool[])completed.Clone();
            done[f.Current] = true;
            bool[] after = ResearchQuery.AvailableMask(content, done);
            if (!f.Assisting && after[destination]) next = destination;
            else next = PickCurrent(content, plan.Route, after, remaining, slot, twoLanes, f.Current);
        }

        (int[] ordered, bool feasible) = Order(content, completed, remaining, plan);
        var blockers = new List<RouteBlocker>(plan.Route.Length);
        var listed = new bool[content.Nodes.Count];
        int otherDestination = twoLanes ? destinations[1 - slot] : -1;
        void Add(int i)
        {
            int k = Array.BinarySearch(plan.Route, i);
            RouteReason cause = plan.Cause[k];
            bool otherLane = twoLanes && LaneOf(content, i, twoLanes) != slot && otherDestination >= 0 && !f.Assisting;
            blockers.Add(new RouteBlocker(i, content.Nodes[i].Key, otherLane ? RouteReason.OtherLane : cause, cause,
                plan.SweepAge[k], available[i]));
            listed[i] = true;
        }
        foreach (int i in ordered) Add(i);
        foreach (int i in plan.Route) if (!listed[i]) Add(i);   // an infeasible route's stranded nodes, index order
        var orderedKeys = new ResearchNodeId[ordered.Length];
        for (int k = 0; k < ordered.Length; k++) orderedKeys[k] = content.Nodes[ordered[k]].Key;
        return new ResearchRoute(lane, Key(destination), Key(f.Current), Key(next), orderedKeys, blockers, plan.RemainingCost,
            false, feasible, f.Assisting, f.Assisting ? Key(destination) : null, plan.Exact, plan.Combinations);
    }

    /// <summary>
    /// The research ORDER of a route: from the completed mask, repeatedly the eligible route node with the smallest
    /// composite key (remaining EffectiveCost, key) — the funding rule with both lanes pooled — until none is eligible; the
    /// route is FEASIBLE when the destination is then eligible (a cycle or an unreachable gate leaves it infeasible, which
    /// the shipped graph never does but the planner defends against).
    /// </summary>
    public static (int[] Ordered, bool Feasible) Order(ResearchContent content, bool[] completed, double[] remaining, ResearchRoutePlan plan)
    {
        bool[] done = (bool[])completed.Clone();
        var ordered = new List<int>(plan.Route.Length);
        while (true)
        {
            bool stage = ResearchQuery.StageReached(content, done);
            bool[]? swept = content.Tuning.AgeSweep ? ResearchQuery.SweptAges(content, done) : null;
            int best = -1;
            foreach (int i in plan.Route)
            {
                if (done[i] || !ResearchQuery.IsAvailable(content, i, done, stage, swept)) continue;
                if (best < 0 || remaining[i] < remaining[best]) best = i;
            }
            if (best < 0) break;
            done[best] = true;
            ordered.Add(best);
        }
        bool feasible = ResearchQuery.IsAvailable(content, plan.Destination, done, ResearchQuery.StageReached(content, done));
        return ([.. ordered], feasible);
    }

    // ------------------------------------------------------------------ the plan

    /// <summary>The route of <paramref name="destination"/> (an incomplete node) — see the class summary.</summary>
    public static ResearchRoutePlan Plan(ResearchContent content, bool[] completed, double[] remaining, int destination)
    {
        if (destination < 0 || destination >= content.Nodes.Count) throw new ArgumentOutOfRangeException(nameof(destination));
        var p = new Planner(content, completed, remaining, destination);
        return p.Run();
    }

    // ---- requirement expressions (a plan-local mirror of the Predicate, n-ary AND / OR) ----

    private abstract class Req
    {
        public sbyte Sat;        // 0 unknown, 1 satisfied by completed, -1 not
        public bool Explored;
    }

    private sealed class AtomReq(int node) : Req { public readonly int Node = node; }
    private sealed class AllReq(Req[] parts) : Req { public readonly Req[] Parts = parts; }
    private sealed class AnyReq(Req[] alts) : Req { public readonly Req[] Alts = alts; public int Group = -1; public int[]? Greedy; }
    private sealed class TrueReq : Req { public static readonly TrueReq Instance = new(); }
    private sealed class NeverReq : Req { }

    private readonly record struct Component(Req Req, RouteReason Cause, int SweepAge);

    private sealed class Planner
    {
        private static readonly int[] Empty = [];
        private static readonly int[] Impossible = [-1];

        private readonly ResearchContent _c;
        private readonly bool[] _done;
        private readonly double[] _rem;
        private readonly int _dest;
        private readonly int _n;
        private readonly Component[]?[] _components;
        private readonly bool _stageReached;
        private readonly bool[] _swept;
        private Req? _stage;

        public Planner(ResearchContent content, bool[] completed, double[] remaining, int destination)
        {
            _c = content;
            _done = completed;
            _rem = remaining;
            _dest = destination;
            _n = content.Nodes.Count;
            _components = new Component[]?[_n];
            _stageReached = ResearchQuery.StageReached(content, completed);
            _swept = ResearchQuery.SweptAges(content, completed);
        }

        public ResearchRoutePlan Run()
        {
            // 1. Union exploration: every node reachable over EVERY alternative, numbering the unsatisfied OR groups.
            var groups = new List<AnyReq>();
            var seen = new bool[_n];
            var queue = new Queue<int>();
            seen[_dest] = true;
            queue.Enqueue(_dest);
            while (queue.Count > 0)
            {
                int x = queue.Dequeue();
                foreach (Component comp in Components(x)) Explore(comp.Req, groups, seen, queue);
            }
            long combinations = 1;
            foreach (AnyReq g in groups)
            {
                combinations *= g.Alts.Length;
                if (combinations > ExactCombinationLimit) break;
            }

            int[] route;
            bool exact = combinations <= ExactCombinationLimit;
            if (exact)
            {
                var choice = new int[groups.Count];
                int[]? best = null;
                double bestCost = 0.0;
                var inSet = new bool[_n];
                while (true)
                {
                    int[]? set = Closure(choice, inSet);
                    if (set is not null)
                    {
                        double cost = Cost(set);
                        if (best is null || cost < bestCost || (cost == bestCost && LexLess(set, best)))
                        {
                            best = set;
                            bestCost = cost;
                        }
                    }
                    int g = groups.Count - 1;   // odometer: the LAST group varies fastest
                    for (; g >= 0; g--)
                    {
                        if (++choice[g] < groups[g].Alts.Length) break;
                        choice[g] = 0;
                    }
                    if (g < 0) break;
                }
                route = best ?? GreedyRoute();   // every combination impossible: the greedy route reports the blockers
            }
            else
            {
                route = GreedyRoute();
            }

            (RouteReason[] cause, int[] sweepAge) = Label(route);
            return new ResearchRoutePlan(_dest, route, cause, sweepAge, Cost(route) + _rem[_dest], exact,
                Math.Min(combinations, ExactCombinationLimit + 1));
        }

        // ---- requirements of one node ----

        private Component[] Components(int x)
        {
            if (_components[x] is { } memo) return memo;
            ResearchNode node = _c.Nodes[x];
            var list = new List<Component>(4);
            if (node.Prerequisite is { } pre) list.Add(new Component(Build(pre), RouteReason.Prerequisite, 0));
            if (node.IsRecursive && (node.Branch >= 0 || node.Tree == ResearchTree.Civics))
            {
                // The exhaustion rule (ResearchQuery.IsAvailable): a subtree repeatable waits for its subtree's finite set,
                // a civics repeatable for every finite civic of Age A9 (ADR-049), a trunk repeatable for nothing more.
                var parts = new List<Req>();
                IReadOnlyList<int> exhaust = node.Branch >= 0 ? _c.FiniteNodesBySubtree[node.Branch] : _c.FiniteFinalAgeCivics;
                foreach (int i in exhaust) if (!_done[i]) parts.Add(new AtomReq(i));
                if (parts.Count > 0) list.Add(new Component(new AllReq([.. parts]), RouteReason.Prerequisite, 0));
            }
            if (node.Branch >= 0 && !_stageReached)
                list.Add(new Component(_stage ??= Build(_c.Stage), RouteReason.Stage, 0));
            int cohort = node.AgeNumber - 2;
            if (_c.Tuning.AgeSweep && cohort >= 1 && !_swept[cohort])
            {
                var parts = new List<Req>();
                for (int i = 0; i < _n; i++)
                {
                    ResearchNode m = _c.Nodes[i];
                    if (!_done[i] && m.AgeNumber == cohort && ResearchQuery.IsSweepCohortMember(m)) parts.Add(new AtomReq(i));
                }
                list.Add(new Component(new AllReq([.. parts]), RouteReason.Sweep, cohort));
            }
            Component[] result = [.. list];
            _components[x] = result;
            return result;
        }

        private Req Build(Predicate p) => p.Fold<Req>(
            a => a >= 0 && a < _n ? new AtomReq(a) : new NeverReq(),
            (l, r) => new AllReq(Flatten<AllReq>(l, r, x => x.Parts)),
            (l, r) => new AnyReq(Flatten<AnyReq>(l, r, x => x.Alts)),
            TrueReq.Instance);

        private static Req[] Flatten<T>(Req l, Req r, Func<T, Req[]> parts) where T : Req
        {
            Req[] left = l is T lt ? parts(lt) : [l];
            Req[] right = r is T rt ? parts(rt) : [r];
            var all = new Req[left.Length + right.Length];
            left.CopyTo(all, 0);
            right.CopyTo(all, left.Length);
            return all;
        }

        private bool Satisfied(Req r)
        {
            if (r.Sat != 0) return r.Sat > 0;
            bool ok;
            switch (r)
            {
                case AtomReq a: ok = _done[a.Node]; break;
                case AllReq all:
                    ok = true;
                    foreach (Req part in all.Parts) if (!Satisfied(part)) { ok = false; break; }
                    break;
                case AnyReq any:
                    ok = false;
                    foreach (Req alt in any.Alts) if (Satisfied(alt)) { ok = true; break; }
                    break;
                case TrueReq: ok = true; break;
                default: ok = false; break;   // NeverReq
            }
            r.Sat = ok ? (sbyte)1 : (sbyte)-1;
            return ok;
        }

        private void Explore(Req r, List<AnyReq> groups, bool[] seen, Queue<int> queue)
        {
            if (r.Explored || Satisfied(r)) return;
            r.Explored = true;
            switch (r)
            {
                case AtomReq a:
                    if (!seen[a.Node]) { seen[a.Node] = true; queue.Enqueue(a.Node); }
                    break;
                case AllReq all:
                    foreach (Req part in all.Parts) Explore(part, groups, seen, queue);
                    break;
                case AnyReq any:
                    any.Group = groups.Count;
                    groups.Add(any);
                    foreach (Req alt in any.Alts) Explore(alt, groups, seen, queue);
                    break;
            }
        }

        // ---- exact: the closure under one OR assignment ----

        /// <summary>The route (ascending, the destination excluded) under <paramref name="choice"/>, or null when it
        /// meets an impossible atom or loops back to the destination.</summary>
        private int[]? Closure(int[] choice, bool[] inSet)
        {
            var members = new List<int>();
            var stack = new Stack<int>();
            bool impossible = false;
            stack.Push(_dest);
            while (stack.Count > 0 && !impossible)
            {
                int x = stack.Pop();
                foreach (Component comp in Components(x)) Resolve(comp.Req, choice, inSet, members, stack, ref impossible);
            }
            foreach (int m in members) inSet[m] = false;
            if (impossible) return null;
            int[] set = [.. members];
            Array.Sort(set);
            return set;
        }

        private void Resolve(Req r, int[] choice, bool[] inSet, List<int> members, Stack<int> stack, ref bool impossible)
        {
            if (Satisfied(r)) return;
            switch (r)
            {
                case AtomReq a:
                    if (a.Node == _dest) { impossible = true; return; }
                    if (!inSet[a.Node]) { inSet[a.Node] = true; members.Add(a.Node); stack.Push(a.Node); }
                    return;
                case AllReq all:
                    foreach (Req part in all.Parts) Resolve(part, choice, inSet, members, stack, ref impossible);
                    return;
                case AnyReq any:
                    Resolve(any.Alts[any.Group >= 0 ? choice[any.Group] : 0], choice, inSet, members, stack, ref impossible);
                    return;
                case NeverReq:
                    impossible = true;
                    return;
            }
        }

        // ---- greedy: each OR takes its cheapest own closure ----

        private int[]?[]? _closure;
        private bool[]? _onStack;

        private int[] GreedyRoute()
        {
            _closure = new int[]?[_n];
            _onStack = new bool[_n];
            int[] all = NodeClosure(_dest);
            if (ReferenceEquals(all, Impossible)) all = Empty;
            var route = new List<int>(all.Length);
            foreach (int i in all) if (i != _dest) route.Add(i);
            return [.. route];
        }

        private int[] NodeClosure(int x)
        {
            if (_done[x]) return Empty;
            if (_closure![x] is { } memo) return memo;
            if (_onStack![x]) return Empty;   // a cycle contributes nothing here; Order reports the route infeasible
            _onStack[x] = true;
            int[] acc = [x];
            foreach (Component comp in Components(x))
            {
                int[] part = Greedy(comp.Req);
                if (ReferenceEquals(part, Impossible)) continue;   // kept as far as it goes; infeasible by Order
                acc = Union(acc, part);
            }
            _onStack[x] = false;
            _closure[x] = acc;
            return acc;
        }

        private int[] Greedy(Req r)
        {
            if (Satisfied(r)) return Empty;
            switch (r)
            {
                case AtomReq a: return NodeClosure(a.Node);
                case AllReq all:
                {
                    int[] acc = Empty;
                    foreach (Req part in all.Parts)
                    {
                        int[] p = Greedy(part);
                        if (ReferenceEquals(p, Impossible)) return Impossible;
                        acc = Union(acc, p);
                    }
                    return acc;
                }
                case AnyReq any:
                {
                    if (any.Greedy is { } memo) return memo;
                    int[] best = Impossible;
                    double bestCost = 0.0;
                    foreach (Req alt in any.Alts)   // listed order: a strictly lower closure cost replaces
                    {
                        int[] p = Greedy(alt);
                        if (ReferenceEquals(p, Impossible)) continue;
                        double cost = Cost(p);
                        if (ReferenceEquals(best, Impossible) || cost < bestCost) { best = p; bestCost = cost; }
                    }
                    any.Greedy = best;
                    return best;
                }
                case TrueReq: return Empty;
                default: return Impossible;
            }
        }

        // ---- labels ----

        /// <summary>Each route node's structural reason: PREREQUISITE when it is reached from the destination over authored
        /// prerequisite (and recursive-subtree) edges inside the route; else STAGE when the stage edges reach it too; else
        /// SWEEP(k), k the smallest cohort Age whose sweep (or an ancestor of whose cohort node) brings it in.</summary>
        private (RouteReason[] Cause, int[] SweepAge) Label(int[] route)
        {
            var inRoute = new bool[_n];
            foreach (int i in route) inRoute[i] = true;
            var label = new RouteReason[_n];
            var age = new int[_n];

            // A: prerequisite edges only.  B: prerequisite and stage edges.
            Reach([_dest], inRoute, label, age, stage: false, RouteReason.Prerequisite, 0);
            Reach([_dest], inRoute, label, age, stage: true, RouteReason.Stage, 0);
            // C: sweep edges from every node already on the route (or the destination), cohort Age ascending, to a fixpoint.
            bool changed = true;
            while (changed)
            {
                changed = false;
                for (int k = 1; k <= 7; k++)
                {
                    var seeds = new List<int>();
                    for (int y = 0; y < _n; y++)
                    {
                        if (y != _dest && (!inRoute[y] || label[y] == 0)) continue;
                        foreach (Component comp in Components(y))
                        {
                            if (comp.Cause != RouteReason.Sweep || comp.SweepAge != k) continue;
                            foreach (int a in Atoms(comp.Req))
                                if (inRoute[a] && label[a] == 0) { label[a] = RouteReason.Sweep; age[a] = k; seeds.Add(a); changed = true; }
                        }
                    }
                    if (seeds.Count > 0) Reach([.. seeds], inRoute, label, age, stage: true, RouteReason.Sweep, k);
                }
            }
            var cause = new RouteReason[route.Length];
            var sweepAge = new int[route.Length];
            for (int k = 0; k < route.Length; k++)
            {
                int i = route[k];
                cause[k] = label[i] == 0 ? RouteReason.Prerequisite : label[i];   // defensive: unreached nodes
                sweepAge[k] = cause[k] == RouteReason.Sweep ? age[i] : 0;
            }
            return (cause, sweepAge);
        }

        /// <summary>Labels every unlabelled route node reachable from <paramref name="seeds"/> over prerequisite (and, with
        /// <paramref name="stage"/>, stage) edges inside the route — through labelled nodes too — with
        /// <paramref name="reason"/>.</summary>
        private void Reach(int[] seeds, bool[] inRoute, RouteReason[] label, int[] age, bool stage, RouteReason reason, int sweepAge)
        {
            var visited = new bool[_n];
            var queue = new Queue<int>();
            foreach (int s in seeds) { visited[s] = true; queue.Enqueue(s); }
            while (queue.Count > 0)
            {
                int x = queue.Dequeue();
                foreach (Component comp in Components(x))
                {
                    if (comp.Cause == RouteReason.Sweep || (comp.Cause == RouteReason.Stage && !stage)) continue;
                    foreach (int a in Atoms(comp.Req))
                    {
                        if (!inRoute[a] || visited[a]) continue;
                        visited[a] = true;
                        if (label[a] == 0) { label[a] = reason; age[a] = sweepAge; }
                        queue.Enqueue(a);
                    }
                }
            }
        }

        private static List<int> Atoms(Req r)
        {
            var list = new List<int>();
            Collect(r, list);
            return list;

            static void Collect(Req r, List<int> into)
            {
                switch (r)
                {
                    case AtomReq a: into.Add(a.Node); break;
                    case AllReq all: foreach (Req p in all.Parts) Collect(p, into); break;
                    case AnyReq any: foreach (Req p in any.Alts) Collect(p, into); break;
                }
            }
        }

        // ---- set helpers ----

        /// <summary>Σ remaining EffectiveCost over an ascending set, in ascending order (bit-reproducible).</summary>
        private double Cost(int[] set)
        {
            double sum = 0.0;
            foreach (int i in set) sum += _rem[i];
            return sum;
        }

        private static bool LexLess(int[] a, int[] b)
        {
            int k = 0;
            for (; k < a.Length && k < b.Length; k++)
                if (a[k] != b[k]) return a[k] < b[k];
            return a.Length < b.Length;
        }

        private static int[] Union(int[] a, int[] b)
        {
            if (a.Length == 0) return b;
            if (b.Length == 0) return a;
            var merged = new int[a.Length + b.Length];
            int i = 0, j = 0, n = 0;
            while (i < a.Length || j < b.Length)
            {
                int next = j >= b.Length || (i < a.Length && a[i] <= b[j]) ? a[i] : b[j];
                if (i < a.Length && a[i] == next) i++;
                if (j < b.Length && b[j] == next) j++;
                merged[n++] = next;
            }
            return n == merged.Length ? merged : merged[..n];
        }
    }
}
