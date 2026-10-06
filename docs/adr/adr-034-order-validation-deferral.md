# ADR-034 — ORDER VALIDATION: DEFERRING CHECKS FOR IDS FOUNDED MID-GAME, AND THE DELIVERY CHECK THAT BOUNDS IT

**Status:** PROPOSED — awaits the Director's acceptance ruling. Written 2026-10-05 by the F3 fix stream of the M5
hardening pass (branch `m5h-f3-ui-docs-fixes`), after a verifier found that two kernel changes had landed without an
ADR.

> **2026-10-06: RATIFIED by the Director (D-050 ruling 1).**

**Touches the kernel contract:** `Sim.Core/Kernel/OrderValidation.cs` (frozen after M0 without Director sign-off).
No schema change, no new order kind, no change to `TurnExecutor.Step`, no golden hash moves. This record is the ADR
the constitution requires for a touched contract. It covers two earlier commits made without one, plus the
tightening this stream added.

---

## 1. Context

`OrderValidation.ValidateAgainstWorld` (T1.6) is the up-front, world-dependent check every replay runner performs
right after it builds the **turn-0** world: `sim run --orders`, `sim replay`, `sim inspect`, `sim research --orders`
and the UI's session tests. Its rejection must be actionable and happen before the run starts, never as a silent skip
partway through a turn. It checks that the issuing actor is a registered Empire, that a construction or labour target
exists, and that the actor controls it.

Two M5 mechanisms create ids **after** turn 0:

| mechanism | id allocated | commit that deferred the check |
|---|---|---|
| colonization (ColonizationSystem) | settlement `max settlement id + 1` | `7100c77` (M5 integration, stream V) |
| revolt (RevoltSystem, D-048) | polity `max polity id + 1` | `9bcad5b` (M5 hardening H4) |

A live session applies orders for these ids, because the UI and the AI producer (ADR-033 D5) issue them from the
state that holds them. Checked against the turn-0 world, the same log was rejected. Measured on `9bb7423`: canonical
seed 42 with the FoundedHarness labour orders revolts settlement 0 at turn 58, and `sim replay` of that run log
exited 2 with "polity 2 is not a registered Empire". So a played session with a colony or a revolt could not be
replayed or inspected.

## 2. The rule

### 2.1 Up-front deferral (7100c77, 9bcad5b — unchanged here)

`ValidateAgainstWorld` **defers** the world-dependent checks (existence and control) for an order when **all** of
the following hold:

- **Actor:** the roster is non-empty, `record.Turn >= 1`, and `record.ActorId` is above every polity id in the
  turn-0 roster.
- **Settlement target** (LaborAllocation, SectorAllocation decoded `>> 3`, EnqueueConstruction): `record.Turn >= 1`,
  the world has at least one settlement, and the target id is above every turn-0 settlement id.

Turn-0 orders are never deferred, because the first batch reads exactly the validated world. Every turn-0 id is
checked as before. The world-independent check that an Empire legislates only its own tax
(`SetTaxRate.TargetId == ActorId`) applies to every actor, deferred or not.

### 2.2 What this weakened

Before these commits, any actor or settlement id missing from the turn-0 world was rejected up front. Since then, a
**forged** id above the turn-0 maxima, such as actor 999 or settlement 999, passes `ValidateAgainstWorld`. Until this
ADR the only protection was at delivery. Each consumer applies its predicate on PREV: ResearchSystem and AgeQuery
read the roster, RoadDevelopmentSystem uses `IsRosterPolity`, ConstructionQuery and LabourActivities check the
control relation, and GovernanceSystem enacts only self-targeted edicts. An order that fails its predicate changes
nothing. `RevoltPolityOrderValidationTests.OrdersFromAnActorThatNeverExists_ChangeNothingAtDelivery` pins this: the
world hash is identical, turn by turn. The deferral was therefore **safe**, but a forged log was **accepted
silently**, and that broke the T1.6 principle that a bad log is rejected with an actionable message.

### 2.3 The tightening: a delivery-time check (this ADR, F3)

`OrderValidation.ValidateAtDelivery(OrderBatch batch, IReadOnlyWorldState turnZero, IReadOnlyWorldState prev)`
re-applies exactly the deferred checks at the moment the batch is delivered:

- A **deferred actor** must be a registered Empire in PREV.
- A **deferred settlement target** must exist in PREV.

Otherwise it throws `OrderValidationException`. The message names the order, the id, the delivery turn and the size
of PREV's roster or settlement table, and cites ADR-034. Orders that the up-front pass already checked are not checked
again. The function only observes: `Step` is unchanged and no hash moves.

Every replay runner calls it for each turn's batch, against the world it is about to step, before stepping:

- `sim run --orders`: the loaded log's batch.
- `sim replay` and `sim inspect`: the log's batch.
- `sim research --orders`.

**Why a live-played log always passes.** The UI and the AI producer stamp each order with the turn being played,
and they issue it from the very state the order is delivered to. A polity the AI drives is in that state's roster
(the roster never shrinks: no code removes a PolityRow). A settlement the player or AI targets exists in that state.
So a revolt or colony replay is unaffected. `CliRevoltPolityProducerTests` runs a real turn-58 revolt log through
`sim run` and `sim replay`, with every hash equal, and still passes with the check in place.

**Considered and not taken: a tighter up-front bound** (for example, a deferred actor id at most
`roster max + number of settlements`). Revolts can recur, because a revolt-founded polity's settlement can revolt
again, and colonies add settlements. So no bound computable from the turn-0 world is both sound and tight. The
delivery check is exact, and it costs one scan of the batch per turn.

## 3. Tests

| test | pins |
|---|---|
| `ColonyOrderValidationTests` (7100c77) | colony-target deferral; turn-0 targets still checked |
| `RevoltPolityOrderValidationTests` (9bcad5b) | actor deferral; turn-0 rejection; registered actors still control-checked; tax authority not deferred; a never-founded actor's orders change nothing (hash per turn) |
| `CliRevoltPolityProducerTests` (H4) | a real revolt log replays through the CLI with the delivery check in place |
| `OrderDeliveryValidationTests` (F3) | forged actor 999 passes the up-front pass and is rejected when its turn (2) is delivered, with a turn-exact message; a deferred actor that exists at delivery is accepted; a deferred settlement target that does not exist is rejected while a turn-0 target is not re-checked; `sim replay` of a forged-actor log exits non-zero with the ADR-034 diagnostic |

Mutant (measured): deleting the `ValidateAtDelivery` call from `sim replay` fails
`TheCli_RejectsAReplayOfAForgedActorLog_WithTheDeliveryDiagnostic` (1 of 4 F3 tests).

## 4. Blast radius

- `Sim.Core/Kernel/OrderValidation.cs`: one new public static method. The behaviour of `ValidateAgainstWorld` is
  unchanged by this ADR.
- `Sim.Cli/Program.cs` (run, replay, inspect) and `Sim.Cli/ResearchCli.cs`: one call per turn before `Step`.
- The UI's `--resume` replays its own session log through `UiSession` and does not call the new check (INFERRED to be
  safe for the same reason a live log passes, and outside the F3 scope). It can adopt the check later.
- No schema, golden, order-kind or system change.
