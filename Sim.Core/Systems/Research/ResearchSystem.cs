using Sim.Core.Kernel;
using Sim.Core.State;

namespace Sim.Core.Systems.Research;

/// <summary>The research system's owned tables (ADR-003 ownership by construction).
/// ResearchCostModifiers and ResearchExposures are deliberately NOT here: they are input
/// contracts research only reads.</summary>
public sealed record ResearchTables(
    Table<ResearchTargetRow> Targets,
    Table<ResearchProgressRow> Progress,
    Table<ResearchCompletedRow> Completed,
    Table<ResearchEurekaRow> Eurekas,
    Table<ResearchCreditRow> Credits,
    Table<CivicsPointsRow> CivicsPoints,
    Table<ResearchLevelRow> Levels);

/// <summary>
/// ADR-029 — THE RESEARCH ENGINE (D-044; addendum A; ADR-030; ADR-043).
///
/// ADR-043 (D-062, 2026-10-22) — TWO LANES. With research.json tuning.civicsPoints.enabled (the shipped content), a
/// polity holds up to TWO targets, one per tree (the lane is the node's tree): Research Points (RP, per turn, ADR-030)
/// fund only the Technology target, and Civics Points (CP, per sim-year × dtYears, banked in the CivicsPoints table)
/// fund only the Civics target. A civic order no longer displaces the technology target. Order kind 6 keeps its shape:
/// the node key selects the lane, −1 clears the Technology lane and −2 the Civics lane. The fixed order inside a step
/// is: directives → credits → RP throughput → CP accrual → CP spend → completions → persist. With civicsPoints off
/// (or absent) the engine is the v34 ONE-POOL model below exactly — one target in either tree, funded by RP, and no
/// CivicsPoints row — the attribution twin.
///
/// THE ONE-POOL MODEL (v34): one shared Research Point
/// (RP) pool per polity drives both trees: Technology (the Main tree plus five subtrees)
/// and Civics. Each step, for every polity in <c>Prev.Polities</c> table order:
///
/// <list type="number">
/// <item><b>Directive.</b> SetResearchTarget orders issued by this polity are applied
///   in log order: -1 clears the target, and an AVAILABLE node's key sets it. An
///   order naming anything else changes nothing (the ConstructionSystem
///   consumption-time precedent). The last valid order wins. An order stamped turn
///   t retargets this very step, whose RP goes to the new target.</item>
/// <item><b>Acceleration credit.</b> For every AVAILABLE node, in index order: each
///   unfired Eureka whose condition holds on PREV fires ONCE and credits weight ×
///   BaseCost; then the foreign exposure offered (input seam) and not yet credited.
///   Both draw on ONE pool per node — at most accelerationCreditCeilingFraction ×
///   BaseCost (0.40) across sources — and each credit is also capped at the remaining
///   EffectiveCost. Nothing overflows to any other node. Credits land whatever the
///   target is (D-044 R10), and each source's amount is recorded (provenance).</item>
/// <item><b>Throughput.</b> The polity's RP for the turn goes to the active target,
///   capped at its remaining EffectiveCost. RP is PER TURN and is NOT multiplied by
///   dtYears (ADR-030 §2; the only per-turn site). RP that reaches no node — no target,
///   or the part past completion — is not stored anywhere: there is no general bank
///   (D-044 R20-D). Partial progress on every other node is left untouched (D-044 R9).</item>
/// <item><b>Completion.</b> Every available node whose progress has reached its
///   EffectiveCost completes, in index order — including a node its credits alone carried
///   there (credit-only completion is intended; no minimum RP spend). It is appended to the
///   completed-knowledge relation, its progress row is removed, and it stops being the
///   target if it was one. Completion is immediate and idempotent (D-044 R11). A completed
///   node is never available again, so it is never added twice. The node's dependents
///   become available from the NEXT step, because availability is always read from PREV.</item>
/// </list>
///
/// ADR-049 (D-063 R4, CR-024) — REPEATABLE LEVELS. With research.json tuning.repeatableLevels on, a node whose repeatable
/// descriptor carries <c>"levels": true</c> (<see cref="ResearchQuery.IsLevelled"/>) is completed REPEATEDLY: completing it
/// raises the holder's level in ResearchLevels (the row is appended at level 1 and replaced in place after), and the node
/// stays AVAILABLE (until <see cref="ResearchQuery.MaxRepeatableLevel"/>). Only the FIRST level appends the
/// ResearchCompleted row — so prerequisites, the stage and every other completed-knowledge reader see the node once, and a
/// later level appends nothing and re-grants nothing (a levelled node unlocks no entity; its benefit is its operand
/// effects, read through <see cref="ResearchQuery.LevelContribution"/>). Each level is a completion like any other: its
/// progress row is removed (progress restarts at 0 for the next level) and it stops being the target. Level n is priced
/// from BaseCost × g^(n−1) (<see cref="ResearchQuery.EffectiveCost"/>). Eurekas and foreign exposure stay ONCE PER NODE:
/// each Eureka fires once and the one pool (ceiling × BaseCost, BaseCost of level 1) is shared across all levels, landing
/// on whichever level is in progress when a credit is paid — a later level is paid in RP/CP. With the switch off no level
/// row is ever written and a repeatable completes once (D-044 R11), byte for byte.
///
/// R2a CITY STATES (docs/city-state-progression.md): after the roster polities, every settlement no Empire
/// controls is a knowledge holder under its reserved local key (SettlementKnowledge.LocalHolder) and runs the
/// SAME steps with two differences only — it issues no orders (its target is the cheapest available node,
/// AiResearchPolicy.Cheapest) and its RP is ResearchQuery.CityStateResearchPoints (tuning.cityStatePaceFraction ×
/// the RP curve of its own population). A controlled settlement's local rows are dormant, never deleted.
///
/// Completion builds nothing (D-044 R14). It makes declared capabilities and entity
/// eligibility answerable through <see cref="ResearchQuery"/>, and the owning
/// systems realize them. Age is never read (D-044 R13, law 4).
///
/// STATELESS; no RNG, so it adds no RngStreams rows; no Ledger (research is none of
/// people, money or goods — nor is CP: its audit is the identity Balance == Generated − Spent, ADR-043 §5). Every rule it applies is a <see cref="ResearchQuery"/>
/// static, which is the same function the Glass Box calls. With no research content
/// attached (a toy world, or a config loaded without research.json) it is inert:
/// the Colonization / Construction precedent.
/// </summary>
public sealed class ResearchSystem(ResearchContent? content) : ISimSystem<ResearchTables>
{
    /// <summary>SystemId 24. 17 is held by the unmerged t4.13 branch, 22 by the unmerged
    /// m5-full-build GovernanceSystem, and 19 is left alone (ADR-029 §3).</summary>
    public static readonly SystemId WellKnownId = new(24);
    public const string Name = "research";

    public SystemId Id => WellKnownId;

    private readonly ResearchContent? _content = content;

    public void Step(SimContext<ResearchTables> ctx)
    {
        if (_content is not { } content) return;
        IReadOnlyWorldState prev = ctx.Prev;
        ResearchTables owned = ctx.Owned;
        int n = content.Nodes.Count;
        bool twoLanes = content.Tuning.TwoLanes;
        var progressRemovals = new List<int>();
        var targetRemovals = new List<int>();
        var processed = new List<int>();

        // R2a: the knowledge holders — the roster polities (table order), then every settlement no Empire
        // controls, under its reserved local key (settlement-table order; docs/city-state-progression.md).
        var holders = new List<PolityId>();
        for (int pi = 0; pi < prev.Polities.Count; pi++) holders.Add(prev.Polities[pi].Id);
        for (int si = 0; si < prev.Settlements.Count; si++)
            if (SettlementKnowledge.ResearchesLocally(prev, content, prev.Settlements[si].Id))
                holders.Add(SettlementKnowledge.LocalHolder(prev.Settlements[si].Id));

        for (int hi = 0; hi < holders.Count; hi++)
        {
            PolityId polity = holders[hi];
            if (processed.Contains(polity.Value)) continue; // a doubled roster row is one Empire
            processed.Add(polity.Value);
            bool local = SettlementKnowledge.IsLocalHolder(polity);

            bool[] completed = ResearchQuery.CompletedMask(prev, content, polity);
            bool[] available = ResearchQuery.AvailableMask(content, completed, ResearchQuery.LevelMask(prev, content, polity, completed));

            // 1. The directive — one target slot per LANE (ADR-043): slot 0 is the Technology lane, funded by RP;
            //    slot 1 the Civics lane, funded by CP. In the one-pool model (TwoLanes off — the v34 behaviour exactly)
            //    slot 0 holds the one target, whichever tree, and slot 1 is never used.
            var target = new[] { -1, -1 };
            if (!twoLanes)
            {
                if (ResearchQuery.TryGetTarget(prev, polity, out ResearchNodeId current))
                {
                    int index = content.IndexOf(current);
                    if (index >= 0 && available[index]) target[0] = index;
                }
            }
            else
            {
                for (int r = 0; r < prev.ResearchTargets.Count; r++)
                {
                    ResearchTargetRow row = prev.ResearchTargets[r];
                    if (row.Polity.Value != polity.Value) continue;
                    int index = content.IndexOf(row.Node);
                    if (index >= 0 && available[index] && target[LaneOf(content, index, twoLanes)] < 0)
                        target[LaneOf(content, index, twoLanes)] = index;
                }
            }
            // A city-state issues no orders: it chooses autonomously — keep a live target, else the cheapest
            // available node (AiResearchPolicy.Cheapest: composite key (EffectiveCost, key), tie-dense tested) — per
            // lane in the two-lane model, the lane's tree only.
            if (local)
            {
                if (!twoLanes)
                {
                    if (target[0] < 0) target[0] = AiResearchPolicy.Cheapest(prev, content, polity, available, null);
                }
                else
                {
                    if (target[0] < 0) target[0] = AiResearchPolicy.Cheapest(prev, content, polity, available, TreeMask(content, ResearchTree.Technology));
                    if (target[1] < 0) target[1] = AiResearchPolicy.Cheapest(prev, content, polity, available, TreeMask(content, ResearchTree.Civics));
                }
            }
            for (int o = 0; o < ctx.Orders.Count && !local; o++)
            {
                OrderRecord order = ctx.Orders[o];
                if (order.Kind != OrderKind.SetResearchTarget || order.ActorId != polity.Value) continue;
                if (order.TargetId == ResearchQuery.ClearTechnologyTargetId) { target[0] = -1; continue; }
                if (order.TargetId == ResearchQuery.ClearCivicsTargetId)
                {
                    // −2 clears the Civics lane; in the one-pool model it clears the one target only if that is a civic.
                    if (twoLanes) target[1] = -1;
                    else if (target[0] >= 0 && content.Nodes[target[0]].Tree == ResearchTree.Civics) target[0] = -1;
                    continue;
                }
                int index = content.IndexOf(new ResearchNodeId(order.TargetId));
                if (index >= 0 && available[index]) target[LaneOf(content, index, twoLanes)] = index;
            }

            // Working view of this polity's progress rows (Next starts as a copy of Prev).
            var rowOf = new int[n];
            Array.Fill(rowOf, -1);
            for (int r = 0; r < owned.Progress.Count; r++)
            {
                ResearchProgressRow row = owned.Progress[r];
                if (row.Polity.Value != polity.Value) continue;
                int index = content.IndexOf(row.Node);
                if (index >= 0) rowOf[index] = r;
            }

            // 2. Acceleration credit — Eureka then foreign exposure, one shared pool per node,
            //    independent of the target, capped, no overflow, provenance per source.
            for (int i = 0; i < n; i++)
            {
                if (!available[i]) continue;
                ResearchNode node = content.Nodes[i];
                double cost = ResearchQuery.EffectiveCost(prev, content, polity, i);
                double ceiling = content.Tuning.AccelerationCreditCeilingFraction * node.BaseCost;
                double eurekaCredited = ResearchQuery.CreditedBySource(prev, polity, node.Key, AccelerationSource.Eureka);
                double exposureCredited = ResearchQuery.CreditedBySource(prev, polity, node.Key, AccelerationSource.ForeignExposure);
                int fired = 0;
                for (int e = 0; e < node.Eurekas.Count; e++) if (ResearchQuery.EurekaFired(prev, polity, node.Key, e)) fired++;
                for (int e = 0; e < node.Eurekas.Count; e++)
                {
                    ResearchEureka eureka = node.Eurekas[e];
                    if (eureka.Condition is null) continue;
                    if (ResearchQuery.EurekaFired(prev, polity, node.Key, e)) continue;
                    if (!ResearchQuery.EurekaHolds(prev, content, polity, eureka.Condition, completed)) continue;
                    // The Eureka fires once, whatever the pool can still pay: a held condition
                    // never pays twice, and a full pool simply pays it nothing.
                    owned.Eurekas.Add(new ResearchEurekaRow(polity, node.Key, e));
                    fired++;
                    // The LAST Eureka of the node to fire pays the rest of the node's entitlement
                    // (total weight × BaseCost minus what Eurekas already credited), so a fully
                    // satisfied Eureka lands on EXACTLY that amount — 40 % of BaseCost for equal
                    // weights — however its parts rounded. It can never exceed the pool.
                    double owed = fired == node.Eurekas.Count
                        ? node.EurekaTotalWeight * node.BaseCost - eurekaCredited
                        : ResearchQuery.EurekaCredit(node, eureka);
                    double headroom = Math.Max(0.0, ceiling - eurekaCredited - exposureCredited);
                    double paid = Credit(owned, rowOf, polity, node, Math.Min(owed, headroom), cost);
                    eurekaCredited += paid;
                    Record(owned, polity, node.Key, AccelerationSource.Eureka, paid);
                }
                double offered = ResearchQuery.ExposureOffered(prev, polity, node.Key);
                if (offered > exposureCredited)
                {
                    double headroom = Math.Max(0.0, ceiling - eurekaCredited - exposureCredited);
                    double paid = Credit(owned, rowOf, polity, node, Math.Min(offered - exposureCredited, headroom), cost);
                    Record(owned, polity, node.Key, AccelerationSource.ForeignExposure, paid);
                }
            }

            // 3. RP throughput to the Technology target (in the one-pool model, the one target). ADR-030 §2: Research
            //    Points are generated and spent PER TURN — deliberately NOT multiplied by dtYears (the scoped exception
            //    to law 3; this is its one code site). RP that reaches no node is lost (no general bank, D-044 R20-D).
            if (target[0] >= 0)
            {
                double amount = local
                    ? ResearchQuery.CityStateResearchPoints(prev, content, SettlementKnowledge.SettlementOf(polity))
                    : ResearchQuery.ResearchPointPool(prev, content, polity);
                if (amount > 0.0)
                    Credit(owned, rowOf, polity, content.Nodes[target[0]], amount,
                        ResearchQuery.EffectiveCost(prev, content, polity, target[0]));
            }

            // 3b. ADR-043 — CP accrual, then CP spend (two-lane model only). Accrual is a per-SIM-YEAR rate integrated
            //    over dtYears (law 3; NOT the ADR-030 exception) in integer milli-CP; it is BANKED without cap or loss.
            //    The Civics target then draws min(Balance, remaining cost) from the bank. Audit identity, every step:
            //    Balance == Generated − Spent.
            if (twoLanes)
            {
                long generated = ResearchQuery.CivicsPointsMilli(prev, content, polity, ctx.DtYears);
                int accountRow = -1;
                for (int r = 0; r < owned.CivicsPoints.Count; r++)
                    if (owned.CivicsPoints[r].Polity.Value == polity.Value) { accountRow = r; break; }
                if (accountRow >= 0 || generated > 0)
                {
                    CivicsPointsRow account = accountRow >= 0 ? owned.CivicsPoints[accountRow] : new CivicsPointsRow(polity, 0L, 0L, 0L);
                    long balance = checked(account.BalanceMilli + generated);
                    long spent = target[1] >= 0
                        ? SpendCivics(owned, rowOf, polity, content.Nodes[target[1]], balance,
                            ResearchQuery.EffectiveCost(prev, content, polity, target[1]))
                        : 0L;
                    var updated = new CivicsPointsRow(polity, balance - spent,
                        checked(account.GeneratedMilli + generated), checked(account.SpentMilli + spent));
                    if (accountRow >= 0) owned.CivicsPoints[accountRow] = updated;
                    else InsertAscending(owned.CivicsPoints, updated);
                }
            }

            // 4. Completion — immediate, idempotent, in index order. ADR-049: a levelled repeatable gains a level; only its
            //    first level appends the completed row.
            for (int i = 0; i < n; i++)
            {
                if (rowOf[i] < 0 || !available[i]) continue;
                if (owned.Progress[rowOf[i]].Progress < ResearchQuery.EffectiveCost(prev, content, polity, i)) continue;
                ResearchNodeId key = content.Nodes[i].Key;
                if (ResearchQuery.IsLevelled(content, i))
                {
                    if (!completed[i]) owned.Completed.Add(new ResearchCompletedRow(polity, key));
                    SetLevel(owned.Levels, new ResearchLevelRow(polity, key, ResearchQuery.Level(prev, polity, key) + 1));
                }
                else owned.Completed.Add(new ResearchCompletedRow(polity, key));
                progressRemovals.Add(rowOf[i]);
                if (i == target[0]) target[0] = -1;
                if (i == target[1]) target[1] = -1;
            }

            // 5. Persist the directive: per lane one row, or none (in the one-pool model: one row, or none). A row is
            //    replaced in place, so the table's order depends only on when each lane was first set.
            var laneRow = new[] { -1, -1 };
            for (int r = 0; r < owned.Targets.Count; r++)
            {
                if (owned.Targets[r].Polity.Value != polity.Value) continue;
                int index = content.IndexOf(owned.Targets[r].Node);
                int lane = index >= 0 ? LaneOf(content, index, twoLanes) : 0;
                if (laneRow[lane] < 0) laneRow[lane] = r;
                else targetRemovals.Add(r);   // a second row on one lane: no rule writes it — dropped
            }
            for (int lane = 0; lane < 2; lane++)
            {
                if (target[lane] >= 0)
                {
                    var row = new ResearchTargetRow(polity, content.Nodes[target[lane]].Key);
                    if (laneRow[lane] >= 0) owned.Targets[laneRow[lane]] = row;
                    else owned.Targets.Add(row);
                }
                else if (laneRow[lane] >= 0)
                {
                    targetRemovals.Add(laneRow[lane]);
                }
            }
        }

        RemoveRows(owned.Progress, progressRemovals);
        RemoveRows(owned.Targets, targetRemovals);
    }

    /// <summary>Adds up to <paramref name="amount"/> RP of progress to a node, capped at its
    /// remaining EffectiveCost, and returns what was actually added. Reaching the cost sets
    /// progress to EXACTLY the cost, so the completion comparison can never miss by a rounding
    /// ulp. The surplus goes nowhere.</summary>
    private static double Credit(ResearchTables owned, int[] rowOf, PolityId polity, ResearchNode node, double amount, double cost)
    {
        int r = rowOf[node.Index];
        double have = r >= 0 ? owned.Progress[r].Progress : 0.0;
        double remaining = cost - have;
        if (!(remaining > 0.0) || !(amount > 0.0)) return 0.0;
        double next = amount >= remaining ? cost : have + amount;
        var row = new ResearchProgressRow(polity, node.Key, next);
        if (r >= 0) owned.Progress[r] = row;
        else rowOf[node.Index] = owned.Progress.Add(row);
        return next - have;
    }

    /// <summary>ADR-049: writes a holder's new level of a node — replacing its row in place, or appending the first one, so
    /// the table's order depends only on the order of first completions.</summary>
    private static void SetLevel(Table<ResearchLevelRow> levels, ResearchLevelRow row)
    {
        for (int r = 0; r < levels.Count; r++)
        {
            if (levels[r].Polity.Value != row.Polity.Value || levels[r].Node.Value != row.Node.Value) continue;
            levels[r] = row;
            return;
        }
        levels.Add(row);
    }

    /// <summary>ADR-043: the lane slot of a node — 1 (Civics, CP-funded) for a civic in the two-lane model, else 0.</summary>
    private static int LaneOf(ResearchContent content, int index, bool twoLanes) =>
        twoLanes && content.Nodes[index].Tree == ResearchTree.Civics ? 1 : 0;

    /// <summary>A mask over node indices: the nodes of one tree.</summary>
    private static bool[] TreeMask(ResearchContent content, ResearchTree tree)
    {
        var mask = new bool[content.Nodes.Count];
        for (int i = 0; i < mask.Length; i++) mask[i] = content.Nodes[i].Tree == tree;
        return mask;
    }

    /// <summary>
    /// ADR-043: spends banked Civics Points on a civic's progress — min(<paramref name="balanceMilli"/>, the remaining
    /// EffectiveCost in milli-CP, rounded UP) — and returns the milli-CP spent. Spending the full remainder sets progress
    /// to EXACTLY the cost (the <see cref="Credit"/> rule), so completion can never miss by an ulp; a partial spend adds
    /// spent / 1000 CP. Nothing is lost: what is not spent stays in the bank.
    /// </summary>
    private static long SpendCivics(ResearchTables owned, int[] rowOf, PolityId polity, ResearchNode node, long balanceMilli, double cost)
    {
        int r = rowOf[node.Index];
        double have = r >= 0 ? owned.Progress[r].Progress : 0.0;
        double remaining = cost - have;
        if (!(remaining > 0.0) || balanceMilli <= 0L) return 0L;
        long needMilli = (long)Math.Ceiling(remaining * 1000.0);
        long spent = Math.Min(balanceMilli, needMilli);
        double next = spent >= needMilli ? cost : Math.Min(cost, have + spent / 1000.0);
        var row = new ResearchProgressRow(polity, node.Key, next);
        if (r >= 0) owned.Progress[r] = row;
        else rowOf[node.Index] = owned.Progress.Add(row);
        return spent;
    }

    /// <summary>Inserts a CP account keeping the table in strictly ascending Polity order (ADR-043's invariant), shifting
    /// the later rows — never a swap, so the canonical stream depends only on the set of holders.</summary>
    private static void InsertAscending(Table<CivicsPointsRow> table, CivicsPointsRow row)
    {
        int at = table.Count;
        for (int i = 0; i < table.Count; i++)
            if (table[i].Polity.Value > row.Polity.Value) { at = i; break; }
        table.Add(row);
        for (int i = table.Count - 1; i > at; i--) table[i] = table[i - 1];
        table[at] = row;
    }

    /// <summary>Adds a credited amount to the node's provenance row for that source (created on
    /// the source's first positive credit; a zero credit leaves no row).</summary>
    private static void Record(ResearchTables owned, PolityId polity, ResearchNodeId node, AccelerationSource source, double amount)
    {
        if (!(amount > 0.0)) return;
        for (int r = 0; r < owned.Credits.Count; r++)
        {
            ResearchCreditRow row = owned.Credits[r];
            if (row.Polity.Value == polity.Value && row.Node.Value == node.Value && row.Source == (int)source)
            {
                owned.Credits[r] = row with { Amount = row.Amount + amount };
                return;
            }
        }
        owned.Credits.Add(new ResearchCreditRow(polity, node, (int)source, amount));
    }

    /// <summary>Removes rows by index while preserving the relative order of the rest,
    /// because a swap-with-last would make the canonical stream depend on removal
    /// history (the ConstructionSystem.RemoveAt precedent).</summary>
    private static void RemoveRows<T>(Table<T> table, List<int> indices) where T : unmanaged
    {
        if (indices.Count == 0) return;
        var drop = new bool[table.Count];
        foreach (int i in indices) drop[i] = true;
        var keep = new List<T>(table.Count);
        for (int i = 0; i < table.Count; i++) if (!drop[i]) keep.Add(table[i]);
        table.Clear();
        foreach (T row in keep) table.Add(row);
    }
}
