using Sim.Core.Kernel;
using Sim.Core.State;

namespace Sim.Core.Systems.Research;

/// <summary>The research system's owned tables (ADR-003 ownership by construction).
/// ResearchCostModifiers is deliberately NOT here: research only reads it.</summary>
public sealed record ResearchTables(
    Table<ResearchTargetRow> Targets,
    Table<ResearchProgressRow> Progress,
    Table<ResearchCompletedRow> Completed,
    Table<ResearchEurekaRow> Eurekas);

/// <summary>
/// ADR-029 — THE RESEARCH ENGINE (D-044). One shared Cognitive Load Point pool per
/// polity drives both trees: Technology (the Main tree plus five subtrees) and
/// Civics. Each step, for every polity in <c>Prev.Polities</c> table order:
///
/// <list type="number">
/// <item><b>Directive.</b> SetResearchTarget orders issued by this polity are applied
///   in log order: -1 clears the target, and an AVAILABLE node's key sets it. An
///   order naming anything else changes nothing (the ConstructionSystem
///   consumption-time precedent). The last valid order wins. An order stamped turn
///   t retargets this very step, whose CLP goes to the new target.</item>
/// <item><b>Eurekas.</b> For every AVAILABLE node, in index order, each uncredited
///   condition of each evaluable Eureka string (string order, then condition order)
///   whose condition holds on PREV is credited ONCE. It credits its OWN node —
///   whatever the target is — with eurekaFullCreditFraction × BaseCost × the
///   condition's share of the node's Eureka (shares sum to 1, so a fully satisfied
///   Eureka is exactly 40 % of BASE cost; Director ruling 2026-10-01 §5–§6), capped at
///   the node's remaining EFFECTIVE cost. There is no overflow to any other node
///   (D-044 R10).</item>
/// <item><b>Throughput.</b> ResearchPerYear × dtYears (law 3) goes to the active
///   target, capped at its remaining cost. CLP that reaches no node — no target, or
///   the part past completion — is not stored anywhere: there is no general bank
///   (D-044 R20-D). Partial progress on every other node is left untouched
///   (D-044 R9).</item>
/// <item><b>Completion.</b> Every available node whose progress has reached its
///   EffectiveCost completes, in index order. It is appended to the
///   completed-knowledge relation, its progress row is removed, and it stops being
///   the target if it was one. Completion is immediate and idempotent (D-044 R11).
///   A completed node is never available again, so it is never added twice. The
///   node's dependents become available from the NEXT step, because availability
///   is always read from PREV.</item>
/// </list>
///
/// Completion builds nothing (D-044 R14). It makes declared capabilities and entity
/// eligibility answerable through <see cref="ResearchQuery"/>, and the owning
/// systems realize them. Age is never read (D-044 R13, law 4).
///
/// STATELESS; no RNG, so it adds no RngStreams rows; no Ledger (research is none of
/// people, money or goods). Every rule it applies is a <see cref="ResearchQuery"/>
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
        var progressRemovals = new List<int>();
        var targetRemovals = new List<int>();
        var processed = new List<int>();

        for (int pi = 0; pi < prev.Polities.Count; pi++)
        {
            PolityId polity = prev.Polities[pi].Id;
            if (processed.Contains(polity.Value)) continue; // a doubled roster row is one Empire
            processed.Add(polity.Value);

            bool[] completed = ResearchQuery.CompletedMask(prev, content, polity);
            bool[] available = ResearchQuery.AvailableMask(content, completed);

            // 1. The directive.
            int target = -1;
            if (ResearchQuery.TryGetTarget(prev, polity, out ResearchNodeId current))
            {
                int index = content.IndexOf(current);
                if (index >= 0 && available[index]) target = index;
            }
            for (int o = 0; o < ctx.Orders.Count; o++)
            {
                OrderRecord order = ctx.Orders[o];
                if (order.Kind != OrderKind.SetResearchTarget || order.ActorId != polity.Value) continue;
                if (order.TargetId == -1) { target = -1; continue; }
                int index = content.IndexOf(new ResearchNodeId(order.TargetId));
                if (index >= 0 && available[index]) target = index;
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

            // 2. Eurekas — independent of the target, capped, no overflow.
            for (int i = 0; i < n; i++)
            {
                if (!available[i]) continue;
                ResearchNode node = content.Nodes[i];
                for (int e = 0; e < node.Eurekas.Count; e++)
                {
                    ResearchEureka eureka = node.Eurekas[e];
                    for (int c = 0; c < eureka.Conditions.Count; c++)
                    {
                        if (ResearchQuery.EurekaFired(prev, polity, node.Key, e, c)) continue;
                        if (!ResearchQuery.EurekaHolds(prev, content, polity, eureka.Conditions[c], completed)) continue;
                        owned.Eurekas.Add(new ResearchEurekaRow(polity, node.Key, e, c));
                        Credit(owned, rowOf, polity, node, ResearchQuery.EurekaConditionCredit(content, node, eureka),
                            ResearchQuery.EffectiveCost(prev, content, polity, i));
                    }
                }
            }

            // 3. Throughput to the one active target.
            if (target >= 0)
            {
                double amount = ResearchQuery.ResearchPerYear(prev, content, polity) * ctx.DtYears;
                if (amount > 0.0)
                    Credit(owned, rowOf, polity, content.Nodes[target], amount,
                        ResearchQuery.EffectiveCost(prev, content, polity, target));
            }

            // 4. Completion — immediate, idempotent, in index order.
            for (int i = 0; i < n; i++)
            {
                if (rowOf[i] < 0 || !available[i]) continue;
                if (owned.Progress[rowOf[i]].Progress < ResearchQuery.EffectiveCost(prev, content, polity, i)) continue;
                owned.Completed.Add(new ResearchCompletedRow(polity, content.Nodes[i].Key));
                progressRemovals.Add(rowOf[i]);
                if (i == target) target = -1;
            }

            // 5. Persist the directive: one row, or none.
            int targetRow = -1;
            for (int r = 0; r < owned.Targets.Count; r++)
                if (owned.Targets[r].Polity.Value == polity.Value) { targetRow = r; break; }
            if (target >= 0)
            {
                var row = new ResearchTargetRow(polity, content.Nodes[target].Key);
                if (targetRow >= 0) owned.Targets[targetRow] = row;
                else owned.Targets.Add(row);
            }
            else if (targetRow >= 0)
            {
                targetRemovals.Add(targetRow);
            }
        }

        RemoveRows(owned.Progress, progressRemovals);
        RemoveRows(owned.Targets, targetRemovals);
    }

    /// <summary>Adds up to <paramref name="amount"/> CLP to a node, capped at its
    /// remaining cost. Reaching the cost sets progress to EXACTLY the cost, so the
    /// completion comparison can never miss by a rounding ulp. The surplus goes
    /// nowhere.</summary>
    private static void Credit(ResearchTables owned, int[] rowOf, PolityId polity, ResearchNode node, double amount, double cost)
    {
        int r = rowOf[node.Index];
        double have = r >= 0 ? owned.Progress[r].Progress : 0.0;
        double remaining = cost - have;
        if (!(remaining > 0.0) || !(amount > 0.0)) return;
        double next = amount >= remaining ? cost : have + amount;
        var row = new ResearchProgressRow(polity, node.Key, next);
        if (r >= 0) owned.Progress[r] = row;
        else rowOf[node.Index] = owned.Progress.Add(row);
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
