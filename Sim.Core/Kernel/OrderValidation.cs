using Sim.Core.State;

namespace Sim.Core.Kernel;

/// <summary>Raised when an order log is inconsistent with the world it targets.</summary>
public sealed class OrderValidationException(string message) : Exception(message);

/// <summary>
/// World-dependent order validation (T1.6): payload RANGES are checked at
/// OrderLog.Load; target EXISTENCE needs the world and is checked here, before
/// turn 1 (the CLI calls it right after world construction). Rejection is
/// actionable and up-front — never a silent mid-turn skip.
/// </summary>
public static class OrderValidation
{
    public static void ValidateAgainstWorld(OrderLog orders, IReadOnlyWorldState world)
    {
        for (int i = 0; i < orders.Count; i++)
        {
            OrderRecord record = orders[i];

            // M4-B: the issuing strategic actor must be a REAL Empire. Actor
            // existence is world-dependent, so this is its layer — the load-time
            // pass sees no world and cannot ask (§5's boundary, kept).
            //
            // Guarded on a non-empty roster ON PURPOSE, and this is a limitation
            // worth stating rather than hiding: no system populates Polities yet,
            // so every canonical world today has an EMPTY roster and this check
            // does not fire. Enforcing unconditionally would reject every existing
            // order log — including the replay fixtures — for naming an Empire the
            // world never had the chance to register. The seam is in place and
            // becomes live the moment worldgen seeds a roster.
            if (world.Polities.Count > 0 && !EmpireQuery.TryGetCommandSource(world, record.Actor, out _))
            {
                throw new OrderValidationException(
                    $"order[{i}] (turn {record.Turn}): {record.Kind} is issued by polity " +
                    $"{record.ActorId}, which is not a registered Empire in this world " +
                    $"({world.Polities.Count} registered). An order's actor is the issuing " +
                    "Empire's PolityId, never a player/AI marker.");
            }

            if (record.Kind is not (OrderKind.LaborAllocation or OrderKind.SectorAllocation
                or OrderKind.EnqueueConstruction)) continue;

            // M4-D §12: an Empire may only build where it rules. The answer comes
            // from the D-037 control relation, never from the actor id taken on
            // trust — this is the caller EmpireQuery.ControlsSettlement was
            // written for. Guarded on a non-empty Controls table for the same
            // reason the actor check is guarded on a non-empty roster: a world
            // with no control relation has nothing to check against, and hand-built
            // test worlds are legitimately in that state.
            if (record.Kind == OrderKind.EnqueueConstruction && world.Controls.Count > 0
                && !EmpireQuery.ControlsSettlement(world, record.Actor, new SettlementId(record.TargetId)))
            {
                throw new OrderValidationException(
                    $"order[{i}] (turn {record.Turn}): EnqueueConstruction targets settlement " +
                    $"{record.TargetId}, which polity {record.ActorId} does not control. An Empire may " +
                    "only build where it rules (D-037 control is authoritative).");
            }

            // T3.3: SectorAllocation packs (settlement × 8 + sector) into
            // TargetId — decode before the existence check (sector range is
            // already load-validated).
            int settlementId = record.Kind == OrderKind.SectorAllocation
                ? record.TargetId >> 3 : record.TargetId;

            bool found = false;
            for (int s = 0; s < world.Settlements.Count; s++)
            {
                if (world.Settlements[s].Id.Value == settlementId) { found = true; break; }
            }
            if (!found)
                throw new OrderValidationException(
                    $"order[{i}] (turn {record.Turn}): {record.Kind} targets settlement " +
                    $"{settlementId}, which does not exist in this world " +
                    $"({world.Settlements.Count} settlement(s)). Toy worlds have none — " +
                    "labor orders need a founded world.");
        }
    }
}
