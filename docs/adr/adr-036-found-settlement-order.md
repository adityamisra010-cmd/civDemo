# ADR-036 — THE FOUND SETTLEMENT ORDER (D-051 §5): ORDER KIND 9, ONE LEGALITY PREDICATE, THE EXISTING FOUNDING PATH

**Status:** PROPOSED — the LEGALITY TERMS in §2 and §3 are the defaults the Director was told the workstream would
use unless he objects; they await his confirmation. Written 2026-10-10 by packet S1b of the M5 map / exploration /
settlement workstream (branch `m5w-s1b-found-settlement-order`, cut from `m5w-integration` @ `55640aed`, LOCAL, not
pushed, not merged).

**Governing record:** D-051 §5 (`docs/d051-m5-polish-rulings.md`, RATIFIED for M5): *"The player picks one of their
settlements and an unclaimed, reachable site. A party of colonists leaves with provisions and founds a city that the
issuing civilization controls. People and goods move only through the Ledger; nothing is created. The order uses the
existing colonization founding path. Emergent founding (D-037 B1) continues unchanged. The order is legal only under
stated conditions: the source has enough people and food, and the site is unclaimed and reachable. The AI uses the
same order and the same predicates."* (its bullets joined, words unchanged). D-051 states none of the conditions; this
ADR states them.

**Touches the kernel contract:** `Sim.Core/Kernel/OrderLog.cs` (a new `OrderKind` value and its load-time range
case) and `Sim.Core/Kernel/OrderValidation.cs` (the new kind joins the ADR-034 settlement-target lists). The
`OrderRecord` shape `{Turn, ActorId, Kind, TargetId, Amount}` is unchanged; `OrderLog.IoVersion` stays 1; no schema
change (canonical schema stays v32); `TurnExecutor` is untouched. No golden hash moves (§6).

---

## 1. The order

`OrderKind.FoundSettlement = 9`. **TargetId = the SOURCE settlement's id. Amount = the SITE cell** (row-major terrain
cell index), a whole number carried exactly by the double (the `EnqueueConstruction` precedent). The issuer is the
order's actor (`OrderRecord.Actor`). Constructor: `FoundingQuery.FoundOrder(world, issuer, source, cell)`, stamped
`world.Clock.Turn` like every other order constructor.

The record cannot carry a party size (both payload fields are used), so the size is content (§3), not a choice the
order makes.

**Validation layers** (the critic correction to the S1b plan is followed: load sees no world):

| layer | checks |
|---|---|
| `OrderLog.ValidateRecord` (load, world-free) | source id ≥ 0; the cell is a non-negative WHOLE number ≤ int.MaxValue (NaN, ±∞, fractions rejected) |
| `OrderValidation.ValidateAgainstWorld` (up front, turn-0 world) | the cell is on the map (`< Size²`; terrain is static, so for every actor and turn, deferred or not); the source exists and the issuer controls it — **deferred (ADR-034)** when the source id is above the turn-0 maximum and the order is delivered after the first step (a colony founded mid-game, including one founded by an earlier Found Settlement order) |
| `OrderValidation.ValidateAtDelivery` (each batch, against PREV) | a deferred source must exist by delivery |
| `FoundingQuery.Check` at consumption (ColonizationSystem, on PREV) | the legality terms of §2–§3; a failing order changes nothing |

The up-front control check mirrors `EnqueueConstruction`'s (guarded on a non-empty `Controls` table). It cannot
reject a live-played log: a turn-0 Empire's control of a turn-0 settlement can only be LOST later (a revolt moves it
to a polity above the roster maximum, whose orders are deferred), never gained.

## 2. Legality — THE predicate (`Sim.Core/State/FoundingQuery.cs`)

`FoundingQuery.Check(world, cfg, frontier, issuer, source, cell)` returns a `FoundingVerdict`; `Legal` iff every arm
holds. It is pure (no state, no cache, no RNG, no writes) and is the ONE predicate for the order consumer
(ColonizationSystem), the UI placement mode (S1d, later) and the AI policy (S1c, later). The arms, in test order
(cheap first, the route search last; the first failing arm is reported):

1. **Source** — the source stands in the judged world (`UnknownSource`) and the issuer controls it
   (`EmpireQuery.ControlsSettlement`; `NotControlled`). D-051: "one of their settlements"; the colony then inherits
   the source's controller through the existing `Found`, so it is "a city the issuing civilization controls".
2. **Site** — a map cell (`OffMap`), land (`Water`), its UNJITTERED siting score > 0 and not below the 50th-percentile
   floor (`PoorSite`) — `SettlementSiting.IsFrontierCandidate`, S1a's extraction of the turn-zero / emergent rule,
   unchanged. **Default: a below-floor site is not legal** (the same floor emergent founding obeys).
3. **Not occupied, spacing-legal** — `SettlementSiting.IsUnobstructedFrontierCell`, unchanged (`Occupied`,
   `TooClose`; ADR-018's 95.2 km travel-cost floor on the raw lattice).
4. **Unclaimed** (`Claimed`) — **Default reading: the site's ORIGIN LATTICE NODE lies in no settlement's catchment
   (no `CatchmentNodes` row).** D-051 does not define the word and the simulation has no land-level claim state
   (`ClaimRow` is schema-only, no writer); the catchment partition is the only land assignment it holds. Spacing
   cannot serve (it is ruled a distinctness floor, not territory — T4.4 review §4). On the shipped constants a catchment
   (50 km budget, ×1.1 at the top size tier) sits inside its settlement's 95.2 km spacing exclusion until roads
   extend it (catchments use the road overlay, spacing the raw terrain): measured, no `Claimed` cell within 60 cells of
   settlement 0 on dev seeds 42 and 1 at turn 1. The arm is therefore exercised in the tests with a 10 km spacing rig
   (§6), and it binds in play where roads carry a catchment past the spacing floor (INFERRED, not measured). A colony founded in the same step or the previous
   one has no catchment row yet (CatchmentSystem recomputes on the next step); spacing covers it.
5. **Enough people** (`NoParty`) — the party (§3) is at least one person, every non-empty source bucket keeping at
   least one.
6. **Enough food** (`NoProvisions`) — **Default: the party's per-capita share of the SOURCE's OWN grain store is at
   least 1 grain** — `FoundingQuery.ProvisionsShare`, the existing B1 arithmetic `⌊store × party / population⌋`,
   moved verbatim out of `ColonizationSystem.ProvisionsFor` (which now delegates to it) so the arm and the transfer
   are one function. D-051 §6's per-civilization stockpile is ratified but deferred; when it lands, this arm and the
   transfer change meaning together. No years-of-food floor (that would be a new constant: plan option 2, not taken).
7. **Reachable** (`Unreachable`) — `Pathfinder.FindPath` finds a route from the source's origin node to the site's on
   the frontier lattice (the raw traversal lattice `TraversalLattice.Build(terrain, riverCostFactor)` CatchmentSystem
   also builds) with the judged world's network overlay (paths and ADR-032 roads). Binary connectivity: no travel
   budget, so no new constant. This is the arm emergent siting lacks (`ColonizationTests` pins that unreachable land
   passes spacing); it applies on the order path only.

**Known extent** (D-040 B1, "settlement operates only within known extent"): no discovered-extent state exists until
S2a, so every cell is known and the arm is inert; its place is between arms 2 and 3.

**Which world.** Every arm is judged on **PREV** — the state of the turn the order was stamped in, which is the world
the issuer saw (the precedent of `ConstructionQuery.IsProjectAvailable`, `Governance.CanLevyTax` and `AgeQuery`). The
one input that is the step's own is occupancy/spacing, which the explicit overload takes as arrays: an earlier Found
Settlement order of the SAME batch may already have founded next door.

## 3. The party and the provisions

**Party size — Default: a content TUNE fraction of the source's population.** `sim.json`
`colonization.orderPartyFraction = 0.2` (CHOSEN; reference class in its `_doc`: D-051 names Civilization VI settlers
as the style — one population point from early cities of roughly 3–6, i.e. one sixth to one third; at the turn-zero
founding size of 400 it sends 80). Validated in (0, 1). The section is OPTIONAL in `SimConfig` (defaulted tail):
absent, no party forms and the order is inert. **It is read only on the order path**: emergent B1 sizes its party from
migration's unplaced demand and never reads it (the T4.4 §8 standing prohibition on any B1 threshold or TUNE holds).

**Allocation** (`FoundingQuery.AllocateParty`, integer arithmetic only): N = the source's people, C = Σ(cᵢ − 1) over
its non-empty buckets; T = min(⌊0.2·N⌋, C); bucket i gives ⌊T·cᵢ/N⌋ (never more than cᵢ − 1, since T < N); the
T − Σ floors people still owed are taken one at a time from the source's buckets in ascending ROW order, skipping a
bucket at its last person, cycling until none is owed. The remainder rule is a stable integer order, so equal
buckets cannot tie; no ordering over doubles exists (tie-dense test, §6).

**Departure is live.** Legality is judged on PREV, but the people and grain that leave are the source's at the moment
of departure — mid-step, after this step's harvest, meals and migration. `FoundingQuery.Departure` applies the SAME
people and food arms to ColonizationSystem's live tables and sizes the party there; if, by then, the step has reduced
every source bucket to its last person or emptied the granary's per-capita share, the order founds nothing.
**Default: a PREV-legal order is refused at departure in that case** rather than founding a colony with no
provisions (D-051's "enough people and food" holds when people and grain move). This is the only way a PREV-legal
order can fail to found, and it is pinned (§6).

**What moves, and how.** Exactly what an emergent founding moves, through the same `Found`: the settlement row at
`maxId + 1` with `FoundedTurn = t + 1`; the inherited `ControlRow`; one `GoodStockRow` per good at 0; deposits
(`WorldFounding.AddDepositsForSite`); Class-A and grievance rows; the people key for key by `Ledger.Transfer`; the
provisions by `Ledger.Transfer` on the grain row; the ADR-012 attractiveness seed. Nothing is minted.

## 4. Resolution inside ColonizationSystem.Step

1. The step seeds occupancy and spacing from every standing settlement (unchanged).
2. **The batch's FoundSettlement orders, in log order, BEFORE the emergent loop.** For each: skip if its source has
   already founded this step (**the first LEGAL order per source per turn founds**; an illegal order does not spend
   the source's turn); judge it (§2); size the departing party (§3); `Found`; then grow spacing and occupancy exactly
   as an emergent acceptance does, so later orders and the emergent loop space themselves against the new colony.
3. The emergent B1 loop, unchanged, over the settlements that stood at the start of the step (a colony founded by an
   order is not a B1 source this step, as a B1 newborn is not).

**Why orders first:** a player's explicit choice of site, legal on the state it was issued from, must not be crowded
out by the frontier drift of the same step; and resolving it first is what keeps "offered" and "founded" the same
answer. Measured: with B1 demand on settlement 0 and an order for the very cell B1 picks, the order founds there
(`maxId + 1`) and B1 founds elsewhere (`maxId + 2`), outside the order colony's spacing.

**Instantaneous.** No settler unit and no movement system (D-043's MobileAgent has no state; movement is M7).
**Delivery:** an order stamped turn t founds in the step t → t+1: the SettlementRow first appears in the state of turn
t+1 with `FoundedTurn` t+1; its catchment first appears in t+2; GovernanceSystem's reach for it is 0.0 in t+1 and t+2
(no distance row in PREV) and real from t+3.

## 5. Unchanged

- **Emergent B1 founding**: no FoundSettlement order → `FoundOrdered` is never entered; `ProvisionsFor` delegates to
  the moved arithmetic unchanged. Every golden is byte-identical (§6).
- The OrderRecord shape and IoVersion; the canonical schema (v32); TurnExecutor; every other order kind.
- **Not in this packet**: the AvailableActionsQuery listing and the UI placement mode (S1d); the AI founding policy
  (S1c). The `baseline.settlement_founding` research.json text now says the order exists (the three generated
  research documents are regenerated for the new content hash only).

## 6. Evidence (measured on this branch; see the packet's commit messages for the runs)

- `Sim.Tests/Systems/FoundSettlementOrderTests.cs`: the turn-exact delivery pin (t+1 settlement / t+2 catchment /
  Strength 0.0 at t+1 and t+2 / reach from t+3) and a stamp-is-the-key pin; exact people/grain conservation key for
  key with `ConservationAuditor` on a POPULATED world (colonization alone and the full pipeline); one test per
  rejection arm (unknown source, uncontrolled ×2, off map, water, poor site, occupied, too close, claimed, unreachable,
  empty granary, party 0) each hash-equal to the orderless step; the live-departure refusal; offered ⇔ founded over
  every source's nearest legal site, a witness of every arm and a ring sweep; same-turn multiplicity (first legal per
  source wins in log order; an illegal first order does not spend the turn; two sources on one site; two sources of
  one issuer each founding in the same turn, ids in log order); orders before the emergent loop; the allocator
  (tie-dense remainder, unequal buckets with exact vectors for the proportional floor and the row-order remainder,
  keep-one, multi-pass cycling, exhaustive small sweep), and the dev-world conservation test bounds every source
  bucket's share to its proportional floor or one more.
- `Sim.Tests/Kernel/FoundSettlementOrderLogTests.cs`: kind 9 round-trips bit-exact; the load-validation theory
  (negative source, fractional / negative / NaN / ±∞ / > int.MaxValue cells); off-map rejected up front even for a
  deferred source; the turn-0 existence/control checks; the ADR-034 deferral and its delivery check; a played log
  that founds a colony and then founds FROM it, replayed and saved/loaded at every turn across issue and effect,
  hash for hash; exact schema length and a bit-exact snapshot round trip of the populated world.
- `IntegratedSaveLoadBatteryTests`: two Found Settlement orders at turns 35 and 37 (after every measured save point,
  so no earlier pin moves) with save points between issue and effect.
- ci.yml `determinism-xproc` gains a FOUND SETTLEMENT leg (two processes, turn-exact delivery against the orderless
  founded run, replay, inspect); `FoundSettlementCiLegTests` pins its script literals as legal and founding.
- Goldens unchanged: founded `937577ff…`, driven, first-reign, toy, attribution constants and the AI leg — no
  existing run issues kind 9.

**Mutant record** (ADR-015 §6–7; each mutant built and run against the filter `FoundSettlement|IntegratedSaveLoadBattery`,
clean baseline 11 s / 66 tests, bound 10× = 110 s; no mutant hung; every kill below is a semantic test):

| mutant | killed by (a semantic test) | the property is one the system ought to have because |
|---|---|---|
| `Found` stamps FoundedTurn t (not t+1) | the turn-exact delivery pin; the stamp-is-the-key pin; the played-log and CI-leg pins (13 tests) | a settlement exists from the state its founding step produces |
| `FoundOrder` stamps turn t+1 | the delivery pin, offered ⇔ founded, conservation, multiplicity, battery (24 tests) | an order issued from state t is delivered on the step from t (every order constructor's rule) |
| the reachability arm skipped | rejection arm `Unreachable` | D-051 §5: "a reachable site" |
| the provisions transfer omitted | the exact conservation test (colony grain = the share) | D-051 §5: the party "leaves with provisions"; the cascade brake needs food |
| the emergent loop before the order loop | orders-before-emergent (B1 would take the ordered cell) | §4: an order legal on its state must not be crowded out by same-step drift |
| allocator remainder from the highest row | the tie-dense allocator test | a stable, stated integer order (CLAUDE.md tie rule) |
| allocator without the proportional floor (`take = 0`: every person through the row-order remainder, a pure round robin) — review finding, re-measured on the 68-test filter (clean 10.7 s, bound 110 s) | the unequal-bucket allocator test (`[10, 30, 60]` with a foreign row at 0.2 gave `[7, 0, 7, 6]`, not `[2, 0, 6, 12]`); the dev-world conservation test's proportional-floor bound (source bucket row 0, 69 people, gave 8 against a floor of 13) | §3: the party is a proportional cross-section of the source — measured on dev seed 42 settlement 0 (458 people, party 91), the round robin took 5 of the 6 people aged 60–64 (shipped: 1) and 8 of the 69 aged 0–4 (shipped: 14) |
| keep-one dropped | allocator keep-one and the exhaustive sweep | §2.5: the source keeps a person in every non-empty bucket |
| the claimed arm dropped | rejection arm `Claimed` | §2.4 "unclaimed" (the Director's reading to confirm) |
| every legal order per source founds | first-legal-per-source-wins | §4: one founding per source per turn |
| one founding per ISSUER per turn (`foundedFrom` keyed on `ActorId`, not the source) — review finding, same 68-test filter and bound | two sources of one issuer founding in one batch (6 settlements expected, 5 founded) | §4: the key is the source, so a civilization with several cities founds from each in one step |
| departure not re-checked live | departure-is-live | §3: a party never leaves without provisions |
| off-map check `>` not `>=` | the off-map world check | cell Size² is off the map |
| ValidateAtDelivery ignores kind 9 | the ADR-034 deferral test; the played-log forged-source check | ADR-034: a deferred id must exist by delivery |
| load accepts a fractional cell | the load theory row `cell: 1.5` | a fractional cell is not a place |

## 7. For the Director

Confirm or amend the defaults: (a) "unclaimed" = origin node in no catchment (§2.4); (b) party = 0.2 of the source,
not chosen per order (§3); (c) "enough food" = per-capita share ≥ 1 grain of the source's own store, until D-051 §6
lands (§2.6); (d) below-floor sites illegal (§2.2); (e) instantaneous founding, no travelling settler (§4); (f) a
PREV-legal order refused at departure when the step itself empties the source's people or granary share (§3). Each is
one line of `FoundingQuery` or one content number; none changes emergent founding.
