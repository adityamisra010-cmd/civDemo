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

## 8. The AI founding policy (M5 S1c, appended 2026-10-11)

**Written by packet S1c** (branch `m5w-s1c-ai-founding-policy`, cut from `m5w-s1b-found-settlement-order` @ `3d8aacc`,
LOCAL, not pushed, not merged). D-051 §5: *"The AI uses the same order and the same predicates."* No ratified AI
expansion policy exists (`docs/m5-ai-constitution.md` is a planning document), so this is the smallest conservative
rule, every threshold content TUNE, **flagged for the Director**.

**The same order, the same predicate.** `Sim.Core/Systems/Colonization/AiFoundingPolicy.cs` (the RoadDevelopmentPolicy /
AiConstructionPolicy shape: not a system, writes no state) returns `FoundingQuery.FoundOrder(world, polity, source,
cell)` — the player's constructor — for the argmax over the cells `FoundingQuery.Check` (the explicit overload, with
the batch's standing sites) answers `Legal` for on the world the order will be judged on: THE PREDICATE DECIDES, and a
refused pick falls through to the same source's next-best Legal cell (§8.2). There is no AI-only legality, verb or
write path; ColonizationSystem founds or refuses the AI's order exactly as the player's. `AiOrders.For` calls it per AI
polity after construction; `AiOrders.For`/`Append` gain a `WorldgenConfig` parameter (the siting config the pipeline's
ColonizationSystem is built from — CLI `CliRecipes.Worldgen()`, UI `UiSession.ProductionWorldgen()`, tests
`TestConfigs.Worldgen()`), so the AI judges with the system's frontier.

**The rule** (sim.json `colonization.aiFounding`; every value TUNE with its reference class in `_doc`):

| arm | rule | value |
|---|---|---|
| switch | `enabled`; absent section = inert | `true` |
| interval | the polity's NEWEST settlement (largest `FoundedTurn` among those it controls, by any path) is at least this old, measured as the LOWER bound (turns elapsed) × (current turn length) — the era table only shortens turns, so it never opens early | `minYearsBetweenFoundings` **350** years |
| source | controlled; at least this many people; fed (`FoodState` Normal or Stress — nobody starving); FoundingQuery's own people and food arms hold; tried in the composite key (population DESC, settlement id ASC) | `minSourcePopulation` **500** (DERIVED: the turn-zero founding size 400 ÷ (1 − orderPartyFraction 0.2)) |
| site | the argmax of the UNJITTERED siting score (`SettlementSiting.CandidateScores`, the frontier raster) by the composite key (score DESC, cell ASC) over the cells `FoundingQuery.Check` answers Legal for from that source. A PRE-FILTER mirrors four per-cell arms as necessary conditions, to spare the route search: candidate, unobstructed by the batch's standing sites, unclaimed, and in the source's connected component (one uncapped `Pathfinder.Isochrone` flood: the edge set and relaxation `FindPath` expands, so flooded ⇔ FindPath finds it). Its argmax is put to the predicate; on a refusal the scan is repeated with the predicate in the admission, so the SAME source takes its next-best Legal cell (§8.2) | — |

At most one order per polity per turn; the first source in key order with a legal site founds. **Batch spacing:**
`AiFoundingPolicy.Sites` carries the occupied cells and the spacing exclusion across the polities of one producer call,
grown with each order issued exactly as `ColonizationSystem.FoundOrdered` grows them with each founding, so a later AI
polity never picks a site an earlier one's colony will exclude. Player orders that precede the AI's in a batch are not
read by the AI; one that makes an AI order `TooClose` at delivery is refused by the system like any other.

**The interval value was MEASURED before it was kept.** The first value, 50 years (two human generations, the
village-fission cadence), bound at every opportunity on seed 42: 73 AI foundings in 600 turns, 84 settlements, a 7.8×
slower run — not conservative. 350 years is anchored to the Age ladder's own expansion milestones (a4_expansion 14,
a6_towns 18): a polity holding 6 of the canonical 12 at founding reaches 14 at this pace by about the historical Iron Age
(8 foundings in the 2,800 years from 4000 BCE to ~1200 BCE). INFERRED anchor; the Director may rule a different pace.

**KNOWN INFORMATION-ACCESS LEAK (recorded, not fixed).** The AI ranks every cell of the map and floods its whole
connected component: no known-extent state exists until the fog-of-war packets (S2a/S2b), which await the Director's fog
ruling, and FoundingQuery's known-extent arm is inert (§2), so the predicate admits what the AI reads. When the arm
lands in `FoundingQuery`, the AI's PICK narrows to the known extent through the predicate with no change to the policy:
a refused pick falls through to the same source's next-best Legal cell (pinned with an emulated arm, §8.2). Its READ
does not narrow: the scan still walks every cell's score, the flood still crosses the whole component, and once the
pre-filter's pick is unknown the rescan asks the predicate about every unknown improver. **S2b must therefore extend
the policy's pre-filter with the extent** (one more mirrored arm, which also restores one predicate call per source)
**or refactor the scan to delegate its per-cell arms to `FoundingQuery`**; until then the leak is in what the AI reads,
not in what it founds. (As first written at `a2c6e78` this paragraph said the extent would narrow the admissible set
"through the predicate" with "no second rule". That was false — a refusal skipped the whole source — §8.2.) MEASURED consequence
(below): the score-only ranking over the whole component sends AI colonies a median 953 km (217–2,392 km) from their
source, 44 % beyond 1,000 km; the known-extent arm is the ratified restriction (D-040 B1). A travel budget would be a new
TUNE constant — not added; a Director option.

**Unchanged:** emergent B1 founding (the policy issues orders; it never touches `ColonizationSystem`); the
OrderRecord shape and IoVersion; the schema (v32); every world with no AI polity (the founded golden `937577ff…`, two
CLI processes on this tree). With `enabled = false` the AI world is the pre-S1c world byte for byte.

### 8.1 Evidence (this branch, Release)

- **Attribution control** (`AiFoundingAttributionTests`): the canonical seed-42 world with one AI Empire, 300 turns,
  switch OFF (`TestConfigs.PreAiFounding`) returns `a416ce4b…` — the turn-300 hash `sim run --founded --seed 42 --turns
  300 --ai-empires 1` produces on the base tree 3d8aacc — BYTE FOR BYTE, and issues no kind-9 order; switch ON is
  hash-identical through state 35, the first AI order is stamped 35 and state 36 is the first that differs; every order
  before it is the same order in both logs; ON turn 300 = `aa7a87a9…` (in-test harness and CLI agree).
- **Re-pinned with cause** (`AiEmpireIntegrationTests`, old → new, one cause, the AI's colonies; the control
  `WithAiFoundingOff_EveryAiPinReturnsItsPreS1cValue` returns every old value with only the switch off): Age 2 advance
  232/233 → 221/222; first road 140/141 → 134/135; first levy 451/452 → 423/424 with A3 451 → 423; the H2 stripped-Age
  control 357/358 → 337/338. New pins: kind-9 orders at 35, 71, 107, 143, 179, 215, 286, 357 by the first levy, the AI
  holding 14 settlements. The research targets (0, 26) and the first granary (9) precede the first founding and are
  unchanged.
- **ci.yml** `determinism-xproc` AI leg: the kind check covers 3..9; its re-derived values (two CLI processes) are in the
  leg's comment.
- **Policy tests** (`AiFoundingPolicyTests`): the composite-key argmax exhaustively over every tie-dense length-5 score
  vector over {0, 0.5, 1, NaN} × every admission mask (32,768 cases); a FLATTENED frontier (every candidate tied) picks
  the lowest cell THE predicate admits; the AI's order equals an INDEPENDENT oracle (every positive cell sorted by
  (score DESC, cell ASC) with LINQ, asked of `FoundingQuery.Check` in turn); each per-cell arm (Occupied, TooClose,
  Claimed with S1b's 10 km rig, Unreachable) made to bind on the TOP-ranked cell is passed over for the oracle's cell;
  player/AI parity (same world twice, polity 2 AI vs player: the AI's record equals the player constructor's, the predicate
  answers alike, and the stepped worlds hash-equal once the command source is set back); a played 12-turn run in which
  every AI order is Legal on its world, founds at its cell in the next state, and the conservation audit balances every
  turn; two AI polities in one batch spaced against each other and both founding; the toy-world trigger arms (size
  threshold edge, Stress in / Severe out, no grain out, (population DESC, id ASC) tie-dense, the interval's lower bound
  and control filter, the switch off/absent, the shipped content's derived values, the loader's refusals).
- **Measured across seeds** (`AiFoundingMeasurement`, SIM_MEASURE=1; canonical 1024², one AI Empire, 600 turns; seeds
  42, 1, 2, 3, 7): **7 AI foundings by turn 300 and 11 by 600 on every seed**, every one founded (none refused at
  delivery), stamped 35, 71, 107, 143, 179, 215, 286, 357, 428, 499, 570 — the interval binds every time; the
  **conservation audit balanced on all 3,000 ON turns**; a **replay** of each ON log with a fresh executor and no
  producer is **hash-identical** every turn. The AI holds 13 settlements at 300 and 17 at 600 (OFF: 6 / 6).
  **Age milestones:** `a4_expansion` (≥ 14) holds for the AI from turn **358 on every seed** (OFF: never in 600);
  `a6_towns` (≥ 18) is **not reached by 600** on any seed (17). Age entries move EARLIER: A2 by 11–19 turns (seed 42:
  233 → 222; 1: 237 → 225; 2: 233 → 218; 3: 251 → 233; 7: 248 → 229) and A3 by 28–54 turns (451 → 423, 462 → 433,
  452 → 418, 489 → 440, 485 → 431); A4 is reached by no run in 600 turns. Wall time per 600-turn run ×1.6–2.3 (the
  world holds 23 settlements instead of 12).

### 8.2 Review finding: a refusal skipped the SOURCE, not the cell (fixed on this branch)

**The finding** (major, confirmed by an independent verdict). As written at `a2c6e78`, `BestSiteFor` re-implemented
the predicate's per-cell arms (candidate, unobstructed, reachable, unclaimed) and `FoundingQuery.Check` was only a final
guard on its pick. When the guard refused, `Decide` moved on to the next SOURCE, not to the next-best cell. Any arm the
predicate gains that the scan does not copy (the planned S2b known-extent arm) would therefore stop the AI founding,
and silently: no illegal order would be issued and no legality test would fail. No test pinned the guard (mutant M9,
"guard removed", was recorded as equivalent), and the §8 claim of "through the predicate, no second rule" was false.
The reviewer measured it on seed 42 (one AI Empire, turn 35) with an emulated arm that refused cells with x ≥ 360:
`Decide` returned NONE, although source 1 had a Legal cell, 463126.

**The fix** (`AiFoundingPolicy.BestSiteFor`). The predicate decides. The pre-filter's argmax is put to the predicate:
- **Legal:** it is the answer. The argmax of a superset that lies in the subset is the subset's argmax. Today that is
  every pick, because the pre-filter is exactly the predicate's per-cell arms, so the predicate runs once per source,
  as before.
- **Refused:** the scan is repeated with the predicate in the admission, so the SAME source takes its next-best Legal
  cell. The next source is tried only when none of this source's cells is Legal.

`AiFoundingPolicy.SiteCheck` and `Decide(…, SiteCheck)` are the test seam. Production passes `FoundingQuery.Check` and
nothing else. A single scan that asked the predicate about every improver was measured and rejected: on the reviewer's
world it asked the predicate 82 times (every answer Legal), and `Decide` took 231–254 ms against 52–65 ms at `a2c6e78`,
for the same result.

**Measured** (Release, this tree; the reviewer's world, canonical 1024², seed 42, one AI Empire, stepped to turn 35 with
the AI producer; a throwaway probe, not committed):
- **Unrestricted:** source 1, cell 634216 (x 360), as at `a2c6e78`. The predicate is asked once and answers Legal.
  `Decide` takes 55–85 ms.
- **x ≥ 360 unknown (emulated):** source 1 founds at cell 463126, the reviewer's oracle cell (`a2c6e78`: NONE). The
  predicate is asked 2,004 times, 1,952 of them refused by the arm, in 230 ms. This is the READ cost S2b must bound (§8).
- **Behaviour today is unchanged:** full `Sim.Tests` 1662 passed, 2 failed, 13 skipped. The 2 failures are the
  inherited CR-020 reds `Dev_MalthusCorridors_AllInBand` seeds 42 and 7. Every AI pin, the attribution constants
  (`a416ce4b…` OFF, `aa7a87a9…` ON) and the founded golden pass unmoved.
  The ci.yml AI leg was run exactly as CI runs it, as two CLI processes from this tree. Hash logs and run logs are
  byte-identical, with last hash `7f374638…`, the value `65a533f` recorded. The run log holds 296 orders, kinds 3..9,
  and 11 kind-9 orders stamped 35, 71, 107, 143, 179, 215, 286, 357, 428, 499 and 570. `sim replay` is identical and
  `sim inspect --turn 600` reports "reproduction VERIFIED".

**Tests.** `AnArmThePreFilterDoesNotMirror_StillBinds_AndTheSameSourceFoundsAtTheOraclesNextBestCell` runs on the dev
256² rig and checks four things:
- The emulated arm binds on the unrestricted pick, and the first-ranked source founds at exactly the oracle's cell. The
  oracle walks (score DESC, cell ASC) with LINQ and asks the extended predicate.
- An arm that refuses every cell of the first source moves the AI to the second source, at that source's oracle cell.
- The seam's production value is the predicate itself.
- Unrestricted, the predicate is asked once.

The bind theory now also asserts that the predicate is asked exactly once, about the pick, and never about the refused
cell. Its TooClose case now takes the first TooClose cell OUTSIDE every catchment. The nearest TooClose cells sit inside
the source's catchment, where the claimed arm hid the spacing arm: dropping the unobstructed arm from the pre-filter
survived on the old rig.

**Mutant record** (filter `AiFoundingPolicyTests`: clean 8 s, 18 tests; bound 15× = 120 s; each mutant took 8–9 s and
none hung):

| mutant | killed by (a semantic test) | the property is one the system ought to have because |
|---|---|---|
| M9 again: the predicate removed (the pre-filter's pick returned unchecked) | `AnArm…` (it ordered the refused cell 36463, not the oracle's 42559); the bind theory's call record also fails in all 4 cases | D-051 §5: the AI uses the same predicate and never orders what it refuses |
| a refusal skips the source (the `a2c6e78` shape) | `AnArm…` (no order at all) | an arm that refuses one cell must not stop a source with other Legal cells |
| the rescan without the predicate | `AnArm…` (the refused cell again) | as M9 |
| only the first source tried | `AnArm…` (null, instead of the second source's oracle order) | the first source in key order with a legal site founds |
| pre-filter reach arm dropped | bind theory `Unreachable` (the predicate was asked about the unreachable cell) | the pre-filter spares the route search. This is now a COST mutant: the predicate refuses what it lets through, so the pick is unchanged |
| pre-filter claimed arm dropped | bind theory `Claimed` | as above |
| pre-filter unobstructed arm dropped | bind theory `TooClose` (killed only on the new rig) | as above |

**For the Director:** (g) the AI expansion rule and its two values (350 years, 500 people) — or a ratified pace;
(h) the known leak and its distance consequence until the extent lands (or a travel budget now), and whether S2b
restricts the AI's READ (the pre-filter) as well as its PICK (the predicate, §8.2); (i) whether a4_expansion
being met at turn 358 on every seed, ahead of the AI's other A4 milestones, is the intended reading of that milestone.

### 8.3 The site scan reads through the knowledge seam (M5 P7, appended 2026-10-11)

**Written by packet P7** (branch `m5w-p7-player-knowledge-seam`, cut from `m5w-integration` @ `b49ae203`, LOCAL, not
pushed, not merged; ADR-037). The Director's P7 addition — *"the AI cannot use information it has not legitimately
discovered"* — answers half of item (h) above for the READ: the pre-filter's FIRST arm is now
`PolityKnowledge.KnowsCell` for the issuer (ADR-037's one information-class seam), so a cell outside the issuer's known
extent is never admitted and never raises the ranking's bar, so an UNKNOWN cell is never picked, whatever it scores.
The pick is NOT a function of known terrain only (corrected after review; this paragraph first claimed it was): a
known cell's score is the frontier raster's, which integrates terrain under unknown nodes (below). `PolityKnowledge.Of`
is all-known until S2a stores an extent, so the arm admits every cell and every order is byte-identical (MEASURED: the ci.yml AI leg's 600-turn hash log and run log equal the base binary's,
byte for byte). `Decide` and `BestSiteFor` gain overloads taking the knowledge (the production entries pass
`PolityKnowledge.Of`); `IsDue` now tests control before reading a settlement's founding turn.

What stays as §8 recorded, for S2b: the reachability flood still crosses the issuer's whole connected component, and the
standing-site occupancy, spacing and claimed masks — the predicate's own rule arms — are read over every settlement.
Neither is a ranking over unknown cells: an unknown cell is refused before them.

**A third channel, recorded after review: the VALUATION of a known cell.** `SettlementSiting.CandidateScores` is region
fertility (a box sum over ±3 px, reaching the neighbouring 4-px nodes) × water access (a whole-map BFS, cutoff 24 px),
and the candidate floor is the median of every positive-scoring cell — so terrain under unknown nodes moves known
cells' scores, the floor, and the pick between KNOWN cells. MEASURED (dev rig, one AI, after one step): hiding only the
eight nodes around the free pick and zeroing their fertility moves the pick 36463 → 42559, both known; with half the
map unknown, unknown fertility alone moves 472 / 468 known cells' scores, flips the candidate arm for 433 / 2,883 known
cells and moves the floor 18.81 → 19.56 / 26.54 (fertility 0 / 1). The READ half of item (h) is therefore only partly
answered: the AI ranks known cells, but values them with terrain it has not discovered. What a polity's valuation of a
partly undiscovered neighbourhood is belongs to S2b with the rest of (h); ADR-037 §6 records it, and the invariance
tooth is quarantined (not skipped) in `PolityKnowledgeTests`.

**Tests** (Sim.Tests `PolityKnowledgeTests`): a stub hiding the unrestricted pick's node moves the order to the oracle's
best KNOWN legal cell; with the rival knowing half the map, raising every unknown cell's own score above every known one
leaves the pick unchanged (control: all-known, the same raise moves it to an unknown cell); and whatever the terrain
under the unknown half (fertility 0 or 1), the pick is a known cell and the oracle's best known legal cell (control:
all-known, fertility 1 moves it to an unknown cell). The valuation channel's quarantine asserts it still open. Mutant
(the `KnowsCell` arm dropped): killed by all three (ADR-037 / `docs/m5-playability-gate.md` §3.6 kill record).
