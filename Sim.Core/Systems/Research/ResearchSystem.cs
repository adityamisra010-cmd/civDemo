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
    Table<ResearchCreditRow> Credits);

/// <summary>
/// ADR-029 — THE RESEARCH ENGINE (D-044; addendum A; ADR-030). One shared Research Point
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

            // 3. Throughput to the one active target. ADR-030 §2: Research Points are
            //    generated and spent PER TURN — deliberately NOT multiplied by dtYears (the
            //    scoped exception to law 3; this is its one code site).
            if (target >= 0)
            {
                double amount = ResearchQuery.ResearchPointPool(prev, content, polity);
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
