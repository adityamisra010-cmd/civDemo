# M6 slice — implementation contract (coordinator's design; binding for the implementation agents)

Authority: the Director's final M6 decisions of 2026-10-22 (recorded by the coordinator as D-062). They supersede the
single-pool / no-reserve rulings (D-044 R2/R12/R20-D, ledger §26 items 2–4, D-I), the research-stage civic atom
(ADR-029 §8, D-046 F§1), "no second exploration system / discovery only by scouts" (D-040 B2, D-039 B1/B3, m6-spec
1.13) **to the extent of the minimal Explore/Prospect actions below**, and the v35/v36 schema reservations. Movement
and Action Points stay M7; civic adoption stays M8; no nuclear mechanics. You (an implementation agent) do NOT edit
governance records (D-records, registers, m6-spec, current-state, CLAUDE.md, milestones.md); you write only the ADR
named for your track plus code, content, tests and the CI/audit artifacts your change regenerates.

Repository laws still apply in full (CLAUDE.md): Ledger-only conservation, `long` conserved stocks, dt-correct rates,
no calendar gates, determinism bans (no System.Random, float, Dictionary iteration in sim logic, LINQ in hot paths,
culture-sensitive formatting), systems isolated (State + Kernel only), composite-key argmax with stable integer
tie-break + tie-dense test, POPULATED-table tests for every new row type (exact ExpectedLength, bit-exact round-trip,
hash equality), turn-exact pins for every new order delivery, golden moves re-pinned with a dated RE-PIN comment and
an attribution test (switch-off twin + `SchemaProjection` strip), banned-constructs and read-isolation scripts green.

## Schema v35 (one bump for the whole slice)

`CanonicalSchema.Version = 35`, readable set {32, 33, 34, 35}. New sections, appended in this exact order:

| Section | Row | Width | Owner | Track |
|---|---|---|---|---|
| 62 | `CivicsPointsRow(PolityId Polity, long BalanceMilli, long GeneratedMilli, long SpentMilli)` — at most one per polity, ascending | 28 | ResearchSystem | CP |
| 63 | `KnownExtentRow(PolityId Polity, int Word, ulong Bits)` — sparse 64-bit words of the 256×256 node lattice (node index n → word n>>6, bit n&63); only non-zero words; ascending (Polity, Word) | 16 | DiscoverySystem | EX |
| 64 | `ExplorationRow(PolityId Polity, SettlementId Settlement, int RadiusNodes)` — explored radius around an origin settlement; ascending (Polity, Settlement) | 12 | DiscoverySystem | EX |
| 65 | `SettlementSightingRow(PolityId Polity, SettlementId Settlement, long LastSeenTurn)` — last-seen memory of a settlement's existence; ascending | 16 | DiscoverySystem | EX |
| 66 | `SettlementSurveyRow(PolityId Polity, SettlementId Settlement, long Turn)` — the polity has prospected (surveyed) this settlement's hinterland; ascending | 16 | DiscoverySystem | EX |

Older streams (v32–v34) stop before section 62; the migration rule is "empty tables", and the DiscoverySystem's
first step after loading re-derives the seed extent (rule E2), so a migrated save becomes fogged with the founding seed
only. Each track adds only its own sections; the coordinator merges them into one v35.

## Track CP — research stage gate + Civics Points (ADR-043)

1. **Stage gate.** `research.json` `researchStage.requires` drops `legal_code_roman`:
   `medicine_hippocratic AND (geometry_axiomatic OR algebra) AND stamp_seal`. `inst.university` keeps
   `library AND legal_code_roman`. Update the stage `_doc` text; flip ResearchContentTests' stage assertion (stage holds
   without the civic; T14 still proves the university needs it).
2. **Two lanes.** RP (Research Points) funds only the Technology target; CP funds only the Civics target. Reuse
   `ResearchTargetRow`/`ResearchProgressRow`; a polity may hold one target per tree (lane = node's tree). Order kind 6
   keeps its shape; the node key selects the lane; TargetId −1 clears the Technology lane, a new −2 clears the Civics
   lane (OrderLog validation + docs). A civic order no longer displaces the tech target.
3. **CP generation (per sim-year, law 3).** Each step, for each polity: `cpMilli = floor(1000 × c × P^β × dtYears)`
   with P = the same `ResearchQuery.Population` RP uses, c = `tuning.civicsPoints.coefficientPerYear` (start 0.008),
   β = `tuning.civicsPoints.exponent` (start 0.699), loader range checks. City-states: × the same 0.25 factor RP uses.
   Rationale and tunables documented as PROPOSED values in ADR-043; other CP sources (institutions, specialists,
   embassies, spies, artists, scholars) are DEFERRED and listed.
4. **Bankable balance and spending.** `Balance += generated` each step; `GeneratedMilli += generated`. If a Civics
   target is held: spend `min(Balance, remainingCost×1000)` (integer milli) into its progress; `SpentMilli += spent`;
   `Balance −= spent`. Unspent CP stays banked (no loss, no cap). Completion as today (completed row, progress row
   removed). Identity pinned exactly in tests: `Balance == GeneratedMilli − SpentMilli` for every polity every turn.
   RP keeps today's semantics (overflow LOST, nothing banked).
5. **Ledger/accounting.** CP is not a conserved good (no Ledger.Flow); the accounting identity above is the audit,
   asserted every step in a test across seeds 1, 2, 42, 7. Order of operations inside ResearchSystem is fixed and
   documented: directives → credits → RP throughput → CP accrual → CP spend → completions → persist.
6. **Exchange RP↔CP: not implemented** (Director: only on the government-change turn; government change is M8). The
   ADR names the slot.
7. **AI.** `AiResearchPolicy` returns up to two orders, one per lane, each the argmin over its goal closures restricted
   to its tree (composite key + tie-dense test). The taxation goal moves to the civics lane. CLI auto driver updated.
8. **UI.** Research screen: Technology and Civics targets shown separately; CP balance and CP/turn shown on the civics
   tab; clear-target per lane. No adoption UI (M8).
9. **Switch for attribution:** `research.json tuning.civicsPoints.enabled` (or a SimConfig flag): off ⇒ the old
   one-pool behaviour (civics on RP, one target) so the attribution twin reproduces the v34 hash after stripping
   section 62.
10. Regenerate the three research `--check` docs (content audit, calibration report, unlock audit); update the
    calibration report text "one shared pool".

## Track EX — deposits, concealment, Prospect, Explore, fog, Actions menu (ADR-044)

**D1 Denominator and eligibility.** Deposits are the existing `DepositRow` per (settlement, good) written at founding
and colonization (no change to generation, quantities or placement). *Eligible* = rows with `Abundance > 0` whose good
is in `sim.json discovery.concealableGoods` = ["stone", "clay", "copper-ore", "tin-ore"] (geological deposits;
biological and food goods are surface-evident and never concealed). The share is measured over eligible rows.

**D2 Classification (derived, not stored).** `DepositConcealment.IsConcealed(world, settlement, good)`: for a
settlement with n eligible rows, conceal k = floor(n × s + u) rows, where s = `discovery.concealedShare` (0.25) and
u ∈ [0,1) = SplitMix64 hash of (seed, ConcealSalt, settlement); the concealed rows are the k lowest by composite key
(hash(seed, salt, settlement, good), goodId). Stateless (consumes no RNG stream). Tests: reproducibility, tie-dense,
realized share on seeds 1, 2, 42, 7 at turn 0 within [0.20, 0.30], deposit table byte-identical with concealment on/off.

**D3 Effect.** A concealed row is *undiscovered* for polity P until `SettlementSurveyRow(P, settlement)` exists. For
the settlement's current controller, an undiscovered concealed row reads as abundance 0 in every consumer:
`ProductionSystem.FromDeposits`, `LabourActivities.GoodsOf`/`DepositAbundance`, `ConstructionQuery.Producible`/`DepositOf`,
InfoQuery good/place cards and `Obtainable`, SiteAssessment (incl. `IfFoundedNext` — a not-yet-founded site's concealed
rows are unknown), ResourceOverlay ore markers. One shared predicate (State query) used by system, UI and AI.
Developer/Explain surfaces may read raw truth (labelled developer).

**D4 Prospect = order kind 15.** Actor polity; TargetId = a settlement the actor controls that it has not surveyed.
Cost: `discovery.prospectTools` tools (start 4) drawn through `EmpireStockpile.Draw` (own settlement first) and SUNK
via `Ledger.Flow` under new reason 17 `Prospecting`; if the empire cannot cover it the order is refused at delivery
and nothing is charged. A technology improves it: with the existing node that best represents ore recognition (choose
from current content, e.g. `copper_smelting`; justify in the ADR) the cost halves (integer). Effect (turn-exact:
stamped t → visible in t+1): write `SettlementSurveyRow(P, S, t)`; every concealed row there becomes discovered for P
(possibly none — the cost is paid regardless, so no free information; a settlement can be surveyed once per polity, so
no repeat exploit). Prospecting never creates, moves or resizes a deposit.

**D5 Fog model (minimal, M6).** Per polity and lattice node: UNKNOWN / VISIBLE / REMEMBERED.
- E1 Known extent (stored, monotone): `KnownExtentRow` bits never clear.
- E2 Seed: every step, DiscoverySystem ORs into each polity's extent the disc of radius `fog.seedRadiusNodes` (3 nodes
  ≈ 50 km, the model's own hinterland constant) around every settlement it controls (covers turn 0, colonies,
  revolt-born polities, conquest, migrated saves).
- E3 Explore (below) ORs larger discs.
- E4 VISIBLE (derived, never stored) = the node of each settlement the polity controls ∪ discs of radius
  `fog.formationSightNodes` (1) around the polity's formations. REMEMBERED = known ∧ ¬visible. UNKNOWN = ¬known.
- E5 Settlements: `SettlementSightingRow(P, S, turn)` is written/updated when S's node is VISIBLE to P or when an
  Explore reveals S's node. `KnowsSettlement` = own, or a sighting exists (presence as last seen; a settlement founded in
  remembered land stays unknown until seen again). Internals of rivals stay hidden as today (`ReadsInternals` own only).
- E6 Units: a rival formation is shown only while its node is VISIBLE (no ghost memory in M6: formations cannot move
  before M7, so a last-known marker equals the live position; ghost rows are DEFERRED to M7 with movement — documented).
  Neutral entities: never remembered.
- E7 `PolityKnowledge.Of` reads E1/E4/E5 (its declared seam); the UI's `PlayerKnowledge` mirrors it; AI reads only it.
  Difficulty grants no information. Maps are never shared.
- E8 Gating: a FoundSettlement order's target node must be KNOWN to the actor (validator, UI and AI share the
  predicate). Emergent colonization (people, not state) stays unchanged (recorded deviation). Trade/roads/claims: no
  new gate in this slice beyond what the seam already applies — list what is DEFERRED.
- Switch: `fog.enabled` (true). Off ⇒ `Of()` answers all-known exactly as today and DiscoverySystem writes nothing ⇒
  the attribution twin.

**D6 Explore = order kind 14.** Actor polity; TargetId = a settlement S the actor controls. The expedition widens the
explored radius around S: new radius = current (`ExplorationRow`, default `fog.seedRadiusNodes`) + `fog.exploreStepNodes`
(3), capped at `fog.exploreMaxRadiusNodes` (15); refused if already at the cap. Cost: `fog.exploreGrain` grain (start 60)
via `EmpireStockpile.Draw` from S first, SUNK under new reason 18 `Expedition` (travel provisions, D-039 B1's food;
people are not removed — the M7 mobile unit replaces this). Effect (stamped t → t+1): ExplorationRow updated; extent |=
disc(S node, new radius); sightings written for every settlement whose node lies in the newly revealed disc. Wrap-aware
(cylinder topology), integer offsets, precomputed disc tables; deterministic.

**D7 DiscoverySystem** (new SystemId 29; pipeline slot after Colonization): owns sections 63–66; applies E2 seeds,
Explore and Prospect orders (log order; one order per (polity, target) per turn, later duplicates refused), sightings.
Stock draws follow the same ownership/edge rules ConstructionSystem uses for `EmpireStockpile.Draw` — check and
mirror them (add the `ReadsThisStepFrom` edges the executor requires).

**D8 AI parity.** AiOrders gains two lanes: Prospect — the AI's own unsurveyed settlement with the most eligible rows,
one per turn, if affordable (composite key (−eligibleCount, settlementId)); Explore — when the founding policy finds no
known candidate site, explore from the controlled settlement with the smallest explored radius (key (radius,
settlementId)). Both read only `PolityKnowledge` and own stocks. Update ci.yml's AI kind set.

**D9 UI — separate Construction and Actions menus.** The POLICY action surface keeps its "Construction" block (Build
Granary etc.) and gains a separate, headed **"Actions"** menu listing, per controlled settlement, "Explore surroundings
(radius r → r+3 nodes, cost N grain)" and "Prospect (cost N tools)" with availability, cost and the blocker reason
(already surveyed / radius cap / not enough tools or grain). New `ActionDomain`, hit and command kinds,
`UiSession.EmitExplore`/`EmitProspect` guarded by the same predicates, PlayabilityGate rows, `ActionEmitterGuardTests`.
Map: UNKNOWN parchment as the seam already paints; REMEMBERED nodes drawn with a darker overlay; VISIBLE normal.
After a prospect, discovered concealed deposits appear in the place card and overlay.

**D10 Tests (minimum).** Classification reproducibility + tie-dense + share bounds; deposit table identical on/off;
Prospect: turn-exact pin, cost sunk exactly (ledger identity), refused when unaffordable (state hash equal to the
orderless step), second prospect refused, concealed row produces nothing before and produces after; Explore: turn-exact
pin, radius growth and cap, cost, extent monotone, wrap at the seam; fog: KnowsCell/Visible semantics, settlement
sighting memory (founded-after-last-seen stays hidden), founding refused on unknown node; metamorphic leak tests
(perturbing unknown land, undiscovered concealed deposits and unseen rival settlements leaves AI orders and UI
output identical, with positive controls); POPULATED-table tests for sections 63–66; save/load/replay hash equality;
v34 save migration; UI tests for the Actions menu (listing, costs, blockers, emit guards) and that Construction still
lists Build Granary.

## Track TREE — technology/civics content tranche (ADR-045)

Implement in `Sim.Data/content/research.json` the non-breaking part of the package's proposal
(`docs/m6-plan/knowledge-redesign/technology-tree-proposal.json`, `civics-tree-proposal.json`): new technology nodes
(keys 427+), rewires, re-scores, relocations to the trunk, renames and capability-string updates; **no key retirement,
no merge, no split that removes a consumer** (those need the remap seam, KR-2, deferred). Civics: keep the 7 legacy
civics; the 60 new civics are DEFERRED (they need regime/policy entity kinds and M8 adoption) unless the loader can take
them as knowledge-only civics without new entity kinds — decide from the loader rules and document. Nuclear chain:
knowledge nodes only (no mechanic). Do not touch `researchStage` or `tuning` (Track CP owns them). Every new node needs
a loader-valid record (primary university, Eureka rules, no orphan, chronology test D-047), a short in-game description
(not the educational corpus), and its prerequisites causal per the proposal. Update content tests, regenerate the three
`--check` docs, and record accepted / revised / deferred / rejected per proposal change in the ADR.
