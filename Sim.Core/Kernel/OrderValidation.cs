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
        // The highest settlement id the validated world holds: colonization allocates
        // maxId + 1 (ColonizationSystem), so only ids ABOVE it can come into existence later.
        int maxSettlementId = -1;
        for (int s = 0; s < world.Settlements.Count; s++)
            if (world.Settlements[s].Id.Value > maxSettlementId) maxSettlementId = world.Settlements[s].Id.Value;

        // The highest polity id the validated roster holds: a revolt founds its new AI polity at the
        // roster maximum + 1 (RevoltSystem, D-048), so only ids ABOVE it can come into existence later.
        int maxPolityId = int.MinValue;
        for (int p = 0; p < world.Polities.Count; p++)
            if (world.Polities[p].Id.Value > maxPolityId) maxPolityId = world.Polities[p].Id.Value;

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
            //
            // M5 hardening H4 (2026-10-05) — an Empire FOUNDED MID-GAME. Every revolt founds a new
            // AI polity (R3 / D-048) and the UI's AI producer drives it from that turn, so a played
            // log carries orders from a polity the validated turn-0 world never registered — and
            // `sim replay` / `sim inspect` / `sim run --orders` of that log threw here (measured on
            // 9bb7423: canonical seed 42, the FoundedHarness labour orders, revolt at turn 58, the
            // new polity's first order stamped 58). Exactly the stream-V colony rule (`7100c77`),
            // for actors: the world-dependent checks — existence and control — are DEFERRED to
            // delivery, where every consumer applies them on PREV (ResearchSystem and AgeQuery read
            // the roster, RoadDevelopmentSystem.IsRosterPolity, ConstructionQuery / LabourActivities
            // the control relation, GovernanceSystem enacts only self-targeted edicts) and an order
            // failing them changes nothing. Deferred only when the actor could still be founded:
            // above every id the roster holds, delivered after the first step (Turn ≥ 1 — the turn-0
            // batch reads exactly this world). Every turn-0 actor is checked as before, and the
            // world-independent tax-authority check below still applies to every actor.
            bool futureActor = world.Polities.Count > 0 && record.Turn >= 1 && record.ActorId > maxPolityId;
            if (world.Polities.Count > 0 && !futureActor && !EmpireQuery.TryGetCommandSource(world, record.Actor, out _))
            {
                throw new OrderValidationException(
                    $"order[{i}] (turn {record.Turn}): {record.Kind} is issued by polity " +
                    $"{record.ActorId}, which is not a registered Empire in this world " +
                    $"({world.Polities.Count} registered). An order's actor is the issuing " +
                    "Empire's PolityId, never a player/AI marker.");
            }

            // ADR-033 D4 (ported from m5-full-build): an Empire sets ITS OWN tax policy and no
            // one else's. Authority comes from the order's issuing Empire (whose registration the
            // roster check above already established), never from the order's word for a target.
            // Whether the issuer CAN levy a tax yet (the research gate) is state-dependent and is
            // decided where the order is consumed — GovernanceSystem, on PREV.
            if (record.Kind == OrderKind.SetTaxRate)
            {
                if (record.TargetId != record.ActorId)
                {
                    throw new OrderValidationException(
                        $"order[{i}] (turn {record.Turn}): SetTaxRate targets polity " +
                        $"{record.TargetId} but was issued by polity {record.ActorId}. An Empire " +
                        "legislates its own taxes; it does not set another Empire's.");
                }
                continue;   // no settlement target to resolve
            }

            if (record.Kind is not (OrderKind.LaborAllocation or OrderKind.SectorAllocation
                or OrderKind.EnqueueConstruction)) continue;

            // A mid-game Empire (above) holds nothing in the turn-0 world: its control is decided at delivery.
            if (futureActor) continue;

            // M5-integration (stream V): a settlement FOUNDED MID-GAME is absent from the
            // validated (turn-0) world, yet the live step applies an order for it. Its
            // world-dependent checks — existence and control — are DEFERRED to delivery, where
            // the consumers apply the same predicates on PREV (LabourActivities.CanAllocate,
            // ConstructionQuery.IsProjectAvailable) and an order failing them changes nothing.
            // Deferred only when the id could still be founded: above every id the world holds,
            // in a world that has a settlement to found it from, delivered after the first step
            // (the Turn-0 batch reads exactly this world). Every turn-0 settlement is checked as before.
            int targetSettlement = record.Kind == OrderKind.SectorAllocation ? record.TargetId >> 3 : record.TargetId;
            if (record.Turn >= 1 && maxSettlementId >= 0 && targetSettlement > maxSettlementId) continue;

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

            // ADR-033 D2: an Empire allocates labour only where it rules — the SAME
            // predicate PathBuildSystem applies at consumption and the action surface
            // lists sectors by (LabourActivities.CanAllocate; guarded on a non-empty
            // Controls table exactly as the construction check above). Like that check,
            // this load-time pass sees only the turn-0 world: it is the fast, actionable
            // rejection of a bad log; the rule's implementation is the consumer's.
            if (record.Kind is OrderKind.LaborAllocation or OrderKind.SectorAllocation
                && !LabourActivities.CanAllocate(world, record.Actor, new SettlementId(settlementId)))
                throw new OrderValidationException(
                    $"order[{i}] (turn {record.Turn}): {record.Kind} targets settlement " +
                    $"{settlementId}, which polity {record.ActorId} does not control. An Empire may " +
                    "only allocate labour where it rules (D-037 control is authoritative).");
        }
    }
}
