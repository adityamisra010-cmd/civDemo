# MIGRATION SIMPLIFICATION — ARCHITECTURE IMPACT AUDIT

**Branch `migration-simplification-audit`, cut from `main` `8586863`. Investigation only.**
No production code, data, test, golden, corridor, band or quarantine was touched. This document
is the only file added.

**This audit was conducted by me alone.** No independent agent reviewed it. Nothing in it is an
independent verdict, and no finding here is certified.

---

## §0 THE HEADLINE, STATED FIRST

**The proposed model is NOT architecturally compatible as stated.** It is refused by one ratified
sentence, and the refusal is not a technicality:

> **`docs/m2-spec.md` §3, line 25 — the migration contract, ratified:**
> *"Migration is Ledger transfers of people by cohort (young-adult-weighted profile), **driven by
> food-per-capita and land-per-capita differentials**, damped by network travel cost between
> settlements, **amplified by famine deficit** (Exit valve). No teleporting: flows only between
> settlements with finite travel cost."*

The tree does not say food stress *causes* migration. It says differentials cause migration and
famine **amplifies** it. The proposal inverts that: it makes food stress the sole cause and demotes
the differential to nothing. That is an amendment to a ratified contract, not a simplification of an
implementation.

**But the audit's more useful finding is that the proposal is right about three things and wrong
about one, and the three it is right about cost almost nothing to get.** Specifically:

- **Already true, no change needed.** Housing, poverty, grievance, prices and infrastructure do
  **not** influence migration today. `MigrationSystem` reads none of them (verified: zero hits for
  Housing/Grievance/Price/Wage/Dwelling in the file). The director's behavioural principle on this
  point is already how the code is built.
- **Already true, no change needed.** "No viable destination ⇒ no migration ⇒ people remain subject
  to demographic consequences" is exactly ADR-012, already shipped and pinned.
- **Available, cheap, and genuinely missing.** Nearest-first destination preference and border
  gating are *not* in the tree. Distance data for the first is already present with no schema
  change. Borders are schema-only and inert.
- **Refused.** Deleting attractiveness/gap as the driver contradicts m2-spec §3 **and** CR-003's
  ratified emergence requirement.

**The T4.4 answer is the practical one: simplifying migration is NOT required to fix T4.4, and
T4.4's defect is not caused by migration's complexity.** See §4.

---

# PART 1 — THE AUTHORITIES, AS THEY ACTUALLY READ

Read from the tree at `8586863`. Where the prompt's terminology differs from the tree, the tree is
recorded.

### 1.1 Terminology corrections (the prompt asked for these)

| prompt says | the tree says |
|---|---|
| "T2.8 migration contract" | **T2.8 in `m2-spec.md` §4 is "Autoplay + calibration battery v1".** There is no T2.8 migration document. The migration stabilization was a **director ruling made during T2.8**, recorded only in `MigrationSystem`'s doc comment and cited afterwards by `m3-spec.md` D-033 (*"the same discipline that stabilized migration at T2.8"*) and D-034. Its authority is real but **derivative** — it survives as a ratified *precedent* in two D-decisions, not as a contract of its own. |
| "T2.13 migration contract" | **T2.13 = ADR-012** (`docs/adr/adr-012-destination-viability.md`, status accepted, director packet). That IS a contract. |
| "CR-004" | **Does not exist on `main`.** It exists only as an unmerged DRAFT (`f7a3345`, `docs/adr/cr-004-malthus-architecture.md`), explicitly *"Status: DRAFT, awaiting director ruling."* It is evidence, not authority. Its content is directly on point and is used below, labelled as draft. |
| "D-018 §5 mobility" | D-018 §5's "mobility flows" are **CLASS** mobility (Peasant→Laborer…), not geographic migration. Only **D-021 Part 3** extends that language to *"intra- and inter-settlement job movement"*. |

### 1.2 What each authority actually commits

**`m2-spec.md` §3:25** — quoted in §0. The controlling contract. Also §4 T2.5's acceptance:
*"richer settlement gains migrants from poorer (direction test) … no flow between unreachable
pairs; magnitudes a few %/decade."* **"Richer gains from poorer" is a ratified acceptance
criterion and it is a pure differential/pull statement.**

**D-021 (`d021-stability-doctrine.md`)** — Part 2 valve 3: *"Angry people leave before they revolt
twice: out-migration **to frontier, other settlements, or abroad** drains the aggrieved bucket
along the real network. **Openness of exits (free movement, available land, transport reach)
governs the valve**."* Two things follow: (a) the **frontier is a ratified exit target**, which is
the seed D-037 B1 later grows; (b) **"free movement"** is named as a governing condition — the
closest the tree comes to authorizing border gating, and it is stated as a *policy choice that
redirects pressure into Voice*, i.e. it belongs to a later unrest/policy milestone.
Part 3: *"wage differentials pull migration along the real network within catchments and between
cities"* — a ratified **pull** driver, scheduled M5/M8 with labour markets.

**ADR-012 (T2.13)** — `viability(j) = (store>0 OR lastHarvest>0) ? max(0, 1 − Repulsion × deficit) : 0`,
multiplying **both** channels. Its D-021 preservation clause is the sentence the proposal needs and
already has: *"When every reachable destination is itself non-viable, flight goes to zero: there is
no exodus without a destination; people die at home instead of circulating between ruins."*

**D-037 B1** — *"Migration currently runs settlement-to-settlement, and ADR-012 rules that with no
viable destination people die at home. **Extend it**: groups may depart into UNCLAIMED land and
found new settlements."* "Extend **it**" = extend **migration**. The hinge is named, and the
condition named at that hinge is **ADR-012's no-viable-destination**, not consumption deficit.

**D-037 Part A / T4.3** — claim, control, recognition. `ClaimRow(Polity, Place, Strength)` and
`ControlRow(Polity, Place, Strength)` exist in `WorldState.cs:548,567`. Their own doc comments say
*"SCHEMA ONLY at T4.3: no system computes, writes, or decays this field yet"*. Verified: the only
writers anywhere are `CanonicalSchema.cs:808,816` — **deserialization**. Borders are inert.

**CR-003 §5.1 (ratified director ruling)** — *"**The Malthusian trap must EMERGE when land fills.
It must never be hardwired**."* §5.4 requires *"the migration attractiveness weights re-DERIVED
(§2.6) rather than rescaled to old behaviour"*, and §5.2(a) defines T4.4's purpose as *"how the
frontier eventually closes and Malthusian pressure legitimately emerges."*

**`sim.json` `_docLandWeight` (ratified data derivation, T3.4b under CR-003 §2.6)** — the single
most important document for this audit, because it states the design intent of the very term the
proposal deletes:

> *"in a frontier world the driver of movement is **LAND OPPORTUNITY**, because everyone is fed and
> only room to farm distinguishes one site from another; when land fills and stores thin, **HUNGER
> must take over** as the driver. The weights are therefore set so the two terms contribute
> COMPARABLY at FED conditions … **The driver SWITCHES WITH THE REGIME rather than being hardwired,
> which is the emergence property CR-003 requires.**"*

**The proposed model hardwires the driver to hunger.** That is the exact thing this derivation and
CR-003 §5.1 forbid. This is the audit's central conflict.

**`m4-spec.md`:273 — T4.12, "the migration-weight packet"**, adversarial-mandatory, dependencies
none, *"T3.4c ruling 2, still unhomed: design point missed 2.3×–8.1×, metric unstable in N and
seed."* **A migration re-derivation packet already exists and is already scheduled.**

---

# PART 2 — INVENTORY AND CLASSIFICATION OF EVERY MIGRATION MECHANISM

All line references are `Sim.Core/Systems/Migration/MigrationSystem.cs` at `8586863` (359 lines).

| # | mechanism | code | class | authority / reason |
|---|---|---|---|---|
| 1 | Ledger.Transfer of people, full bucket key preserved | 333-335, 318-322 | **A** | m2-spec §3:25 "Ledger transfers of people by cohort"; CLAUDE.md law 1 |
| 2 | Cohort profile (young-adult-weighted) | 240, 264, 298 | **A** | m2-spec §3:25 "by cohort (young-adult-weighted profile)" |
| 3 | Per-year base rate × dtYears | 240, 264-265, 298-299 | **A** | law 3; D-018 §5 "a few % per sim-year" |
| 4 | **Food-per-capita attractiveness term** | 154 | **A** | m2-spec §3:25 "driven by food-per-capita … differentials" |
| 5 | **Land-per-capita attractiveness term** | 146-154 | **A** | m2-spec §3:25 "…and land-per-capita differentials"; `_docLandWeight` derivation; CR-003 §5.1 emergence |
| 6 | Pairwise **gap** `max(0, S_j − S_i)` | 231, 272, 287 | **A** | m2-spec §3:25 "**differentials**"; T2.5 accept "richer gains from poorer" |
| 7 | **Travel-cost damping** `exp(−cost/decay)` | 199-207 | **A** | m2-spec §3:25 "damped by network travel cost". Note this is ratified as a **continuous damping**, not as a feasibility filter |
| 8 | Unreachable pair ⇒ +∞ ⇒ damping 0 | 26-27, 206 | **A** | m2-spec §3:25 "No teleporting: flows only between settlements with finite travel cost"; T2.5 accept |
| 9 | **Famine flight** `FamineFlightFactor × deficit_i`, gap-independent, uncapped by the gap cap | 273, 288 | **A** | m2-spec §3:25 "amplified by famine deficit (Exit valve)"; D-021 valve 3; ADR-012 §"D-021 Exit-valve preservation" |
| 10 | **Destination deficit repulsion** `max(0, 1 − k·deficit_j)` | 170-174 | **A** | ADR-012 Decision, verbatim |
| 11 | **Absolute food gate** (no store AND no harvest ⇒ 0) | 142, 172-174 | **A** | ADR-012 Decision, verbatim ("the director's candidate fix (1), taken as a hard gate") |
| 12 | Viability multiplies **both** channels | 242, 271, 286 | **A** | ADR-012: *"Every pairwise migration flow (BOTH channels…)"* |
| 13 | **Gap-closing cap** `f × m*`, `m* = (R_j P_i − R_i P_j)/(R_i+R_j)` | 246-251 | **B** | Not in a spec sentence. Required indirectly by **D-021's paired-feedback rule** (*"every positive feedback loop must ship with a negative feedback loop that strengthens with amplitude"*) and ratified **by citation** in `m3-spec.md` D-033 and D-034 as the pattern they copy. Removing it removes the precedent two D-decisions rest on. |
| 14 | **EMA attractiveness smoothing** (`WindowYears`, first-sighting init `S = A`) | 176-194 | **B** | Not in any spec sentence. It is the T2.8 director ruling's remedy for a measured attractor (`queue.md`:65 — *"persistent two-turn population slosh (~95% of a settlement shuttling)"*). **Required by the pathology, not by a document.** If the pathology is removed by other means, the EMA's warrant goes with it — see §5. |
| 15 | Proportional **overdraw scaling** | 258-277, 301-302 | **B** | Law 1: a bucket may not go negative and destinations must not be first-come-first-served. `Overdraw_ScalesProportionally_NoFirstDestinationGrab` pins it. Any model with >1 destination needs *some* such rule; proportional is the implementation choice. |
| 16 | Persistent **MigrationRemainder** per bucket | 296, 305-307, 328 | **B** | Law 1 + `long` stocks: fractional people cannot move; without banking, `floor()` biases every small flow to zero permanently. Schema field (`BucketRow`, v9). Architecturally required in *some* form by integer conservation. |
| 17 | `ClampToAvailable` backstop | 335 | **B** | Law 1; documented ULP-association backstop (222-224) |
| 18 | Ascending (source, dest, bucket-key) execution order | 281-291 | **B** | Law 5 determinism |
| 19 | `settlementIndex` array id→row, no dictionaries | 126-127 | **B** | Law 5 |
| 20 | Chronicle `MigrationFlowRow` (Inflow/Outflow only) | 113-116, 340-341 | **A** | m2-spec §4 T2.5 "chronicle hooks for surges"; T2.9 surge detection |
| 21 | Destination-key matching + refund when no matching bucket exists | 315-330 | **C** | An implementation consequence of hand-built worlds having heterogeneous bucket layouts. Two review findings are fossilised here (315-321, 326-329). Not architecturally required — a founding invariant would remove the need. |
| 22 | `k`-index shortcut before the linear `FindBucket` | 315, 322 | **C** | Pure optimisation; the guarded fallback is the real path |
| 23 | Recomputing the same products in the desire pass and the transfer pass | 219-224, 255-299 | **C/D** | Acknowledged in-code as a *"pre-T2.13 pattern"* with ULP divergence between the two sites. Compatibility scaffolding; a single-pass formulation is behaviourally intended to be identical |
| 24 | `n < 2` early return | 195 | **C** | Guard |
| 25 | Crowding-saturation term | 74-78 | **E** | **Considered and DECLINED** in the doc comment, on law 2 grounds. Recorded so a future agent does not re-propose it. |
| 26 | Destination **capacity** logic | — | **absent** | There is none. Housing capacity is not read. |
| 27 | **Border / claim / control** gating | — | **absent** | There is none. §1.2 confirms Claims/Controls are inert. |
| 28 | Nearest-destination preference / search ordering | — | **absent** | There is none. Every pair is evaluated; distance enters only as continuous damping. |

### 2.1 The classification result, in one line
**Of 25 implemented mechanisms, 12 are class A (explicitly ratified), 7 are class B (required
indirectly), 5 are class C/D (implementation), 1 is class E (declared dead).** There is **no class F**
in the implementation — nothing currently in the code is unclear as to whether it is required. The
unclear questions are all about the *proposal*, and they are in §12.4.

**The "layered machinery" in the prompt is 12 ratified mechanisms and 7 conservation/determinism
obligations, not accumulated cruft.** The genuinely removable complexity (§12.3) is small: items
21–24. That is the honest answer to the prompt's framing question about whether the implementation
is more complicated than the intended rule. **It is not. It is almost exactly as complicated as the
ratified rule, plus about 40 lines of scaffolding.**

---

# PART 3 — THE PROPOSED MODEL, TESTED CLAUSE BY CLAUSE

| proposed clause | verdict | evidence |
|---|---|---|
| **"Migration pressure arises from inability to sustain population, principally food deficit"** | **CONFLICT** | m2-spec §3:25 makes famine an **amplifier** of a differential-driven flow. `_docLandWeight` states the fed-regime driver is **land opportunity**. CR-003 §5.1 requires the driver to switch with the regime and *never be hardwired*. |
| **"No food deficit ⇒ no migration"** | **CONFLICT, and it deletes a ratified acceptance criterion** | T2.5's *"richer settlement gains migrants from poorer (direction test)"* is unsatisfiable in a fed world under this rule. `MigrationTests.Direction_NetFlowRunsPoorToRich` is that criterion's pin. |
| **Destination viable only if reachable** | **ALREADY TRUE** | `damping=0` for unreachable pairs (206), pinned by `UnreachablePair_ZeroFlow_AtTheTableLevel` |
| **Destination viable only if it has food** | **ALREADY TRUE, verbatim** | ADR-012 both gates (170-174) |
| **Destination viable only if borders permit** | **NOT EXPRESSIBLE TODAY** | §1.2. Requires a new mechanism **and** polity/claim data that nothing writes. **Ruling required.** |
| **"any explicitly ratified capacity constraint"** | **NONE EXISTS** | item 26. Nothing to honour; nothing to remove. |
| **"Prefer nearby viable destinations"** | **NOT RATIFIED — and this is the audit's one clean gap** | Grep for "nearest" across all `docs/` returns **zero** hits about migration. The only ratified statement about distance is *"damped by network travel cost"*, which is a **continuous weight**, not an ordering. **Nearest-first is a NEW rule requiring a ruling.** |
| **"Distance is primarily a search/order/feasibility constraint, not a continuous attractiveness modifier"** | **CONFLICT** | m2-spec §3:25 ratifies it as damping. Also: the migration corridor's own derivation (`corridors.json`) *defines the measured quantity* as **"LONG-DISTANCE PERMANENT INTER-SETTLEMENT RELOCATION between sites spaced ≥480 km apart."* A nearest-first rule would systematically move flow to short hops and change what that corridor measures. |
| **"No viable destination ⇒ migration does not fabricate a destination; people remain subject to existing demographic consequences"** | **ALREADY TRUE, verbatim ratified** | ADR-012 §"D-021 Exit-valve preservation": *"there is no exodus without a destination; people die at home instead of circulating between ruins."* Pinned by `CollapseStabilityTests`. |
| **"Colonization may subsequently act"** | **COMPATIBLE — this is exactly D-037 B1's sentence** | D-037 B1 names ADR-012's no-viable-destination rule as the thing being extended. **The proposal's final clause is the most tree-faithful part of it.** |

---

# PART 4 — THE T4.4 CONNECTION

*(T4.4's implementation lives on branch `t4.4-colonization` `ae1ebbd`, not on `main`. Findings
about it are from `docs/t4.4-review-record.md` §5, which is my own measurement, not independent.)*

**The single most important finding in this audit:**

> **Simplifying migration is NOT necessary to fix T4.4, and T4.4's defect was never caused by
> migration's complexity.** T4.4 broke because it used the **wrong trigger** — `ConsumptionDeficitRow`,
> which cannot distinguish *"cannot feed the people it has"* from *"was founded three turns ago and
> is not producing yet"* — and because `DeficitRatio` is scale-free, so emigration can never clear
> it. Measured: 12 → 178 settlements by turn 77 with population falling 4330 → 3192; at turn 40,
> **7 of 7** deficit settlements were ones colonization itself had founded.

**Q1 — can migration expose "people seeking an existing destination but with none available"
without violating its contract?** **Yes, and it requires no contract change at all.** The quantity
is already computed inside `Step`: it is the per-bucket desire that survives the source's own
push terms but finds every destination's `damping × viability` product zero. Today it is simply
**not written down** — line 271's `total` accumulates only what *is* placeable. Exposing it is an
additive chronicle-style output, exactly as `MigrationFlowRow` already is. It changes no equation,
moves no flow, and touches neither the T2.8 cap nor ADR-012.
**It does need a new serialized row (or two fields on `MigrationFlowRow`), so it is a schema change
with a POPULATED-table test — a real cost, but a mechanical one.**

**Q2 — is that quantity necessary?** **Not strictly, but it is the only formulation that is
self-limiting by a mechanism rather than by damping.** The alternative T4.4 already probed —
making the clearing cost binding — halves the rate and does not converge (measured: 123 → 73
settlements at turn 60, still ~1.2 foundings/turn with no saturation). The reason the failed-demand
formulation converges is structural: **placing the party discharges the demand**, so the quantity
is consumed by its own satisfaction. A ratio cannot be.

**Q3 — does D-037 B1 REQUIRE colonization to be driven by failed migration, or merely permit it at
the same hinge?** **It requires it, as strongly as prose can.** B1's two sentences are:
*"ADR-012 rules that with no viable destination people die at home. **Extend it**: groups may depart
into UNCLAIMED land…"* The antecedent of "it" is migration, and the *only* condition B1 names is
ADR-012's no-viable-destination. B1 never mentions consumption deficit. **T4.4's deficit trigger was
therefore not derived from B1 — it was borrowed from T4.5/D-037 B3, whose subject is raiding.**
That is the root error, and this audit makes it explicit.

**Q4 — can `food stress → attempt existing destinations → none viable → colonization` be built
without a second migration clock, destination simulation, damping system, or virtual settlement?**
**Yes, and that is the strongest structural argument in this audit.** Colonization already runs
*after* migration in the pipeline (`t4.4-colonization` inserts it at index 10, immediately after
`migration`). A system that reads a quantity migration has already computed invents nothing: no
second clock (it runs on migration's own turn), no destination simulation (it reads a scalar, not a
set), no damping (it never scores a destination), no virtual settlement (the exact thing T4.4's
design pass rejected). **This is strictly LESS machinery than T4.4 currently has.**

**Q5 — genuinely self-limiting?** **Yes, by three independent brakes**, and this is why it differs
in kind from the ratio trigger: (a) the demand is *consumed* when placed; (b) each founding creates
a new viable destination, so next turn's identical stress is *placeable* and produces no founding;
(c) the spacing floor and the finite frontier bound the site supply. Brake (b) is the important one
— **it is a negative feedback that strengthens with amplitude, which is precisely D-021's
paired-feedback rule.** The deficit-ratio trigger has no such brake, which is why it ran away.

**Q6 — could a newly founded settlement immediately trigger another founding?** **No, and the
architectural condition that prevents it is ADR-012's own absolute food gate.** A newborn settlement
with carried provisions has `store > 0`, so its **viability is non-zero** — it is a *viable
destination*. Under the failed-demand model, stressed neighbours can now be **placed into it**,
which means their demand is discharged and no founding occurs. Under the deficit-ratio model the
same newborn instead *emits* a founding, because its own deficit is read as a founding trigger.
**The two models have opposite signs on exactly the settlement that broke T4.4.** That is the
cleanest single result of this audit.

Two caveats I will not paper over: (i) if the newborn's provisions run out and its harvest has not
started, `anyFood` goes false, viability goes to zero, and it stops being a destination — it does
not *emit* a founding, but it stops absorbing one; (ii) an unreachable frontier settlement has
`damping = 0` from everywhere, so it is never a destination for anyone. Neither reintroduces the
cascade, but both mean the frontier's closure rate depends on reachability, which is a measurement
question for the implementing packet, not an architectural one.

---

# PART 5 — THE ADR-012 PATHOLOGY

**Verdict: the proposed simplification would make the resurrection pathology STRUCTURALLY
IMPOSSIBLE rather than merely less likely — and this is the proposal's single strongest argument.**

The pathology has two limbs (ADR-012 Context):

1. **Starvation magnetism** — *"attractiveness is per-capita, so a settlement emptied by famine —
   zero food, full catchment — read as the world's STRONGEST magnet."* **This limb exists only
   because attractiveness exists.** Delete the attractiveness/gap channel and there is no magnet to
   invert. The limb does not need to be guarded; it cannot be stated.
2. **The resurrection cycle** — *"when the last inhabitant died, demand hit zero and the deficit
   signal RESET to 0.00."* This limb is about the **deficit signal**, not attractiveness, and it
   would **survive** the simplification: a dead settlement still reads deficit 0. **But ADR-012's
   absolute food gate (`store>0 OR lastHarvest>0`) kills it independently of attractiveness**, and
   the proposal keeps that gate. So limb 2 stays guarded by the guard that already guards it.

**Therefore: removing attractiveness from the migration decision makes the ADR-012 *attractiveness*
pathology irrelevant to migration, and leaves the *deficit-reset* pathology guarded by the food gate
that already handles it.**

**Consequence for the EMA (item 14, class B).** The EMA's entire documented warrant is damping a
one-turn attractiveness spike (*"a one-turn emptying can no longer mint a one-turn magnet"*). **With
no attractiveness in the decision, the EMA has nothing to smooth and its warrant is discharged.**
Per the prompt's instruction I am not proposing an EMA workaround: I am reporting that the EMA
becomes *unmotivated*, not that it becomes wrong. **This also disposes of T4.4's most awkward line:**
T4.4 seeds a new settlement's EMA from its founding source purely to stop an unseeded frontier
settlement arriving pre-converged on its own inflated signal. Under the simplified model **that
whole guard disappears along with the hazard it guards.**

I record the counter-argument honestly: the T2.8 ping-pong attractor (`queue.md`:65) was a *measured*
pathology in a *real* run, and the EMA plus the gap cap are what killed it. **A future packet must
prove the simplified model does not reintroduce a two-turn slosh through a different door — the
famine-flight channel is destination-blind except for viability and is explicitly uncapped, so a
cluster of mutually-starving settlements is where I would look first.** `MigrationStabilityTests`
and `MigrationConcentrationTests` already contain the detectors to test that with.

---

# PART 6 — MIGRATION AS GEOGRAPHICAL BEHAVIOUR

| question | answer from the tree |
|---|---|
| Does `MigrationFlowRow` contain a destination? | **No.** `MigrationFlowRow(SettlementId Settlement, long Inflow, long Outflow)` (`WorldState.cs:380`). It is a per-settlement **aggregate**, not an edge. |
| Where is destination identity represented? | **Nowhere in state.** It exists only transiently as the `dst` loop index inside `Step`. This is the same fact that killed the ADR-018 spacing hypothesis in an earlier session: no distance and no destination exist anywhere in the persisted migration chain. |
| Is migration fundamentally pairwise? | **Yes, in computation** (a full `n × n` sweep, lines 226-253 and 281-345), **no, in state.** Nothing downstream can tell where anyone went. |
| Can distance be evaluated without new schema? | **Yes.** `SettlementDistanceRow(From, To, TravelCost)` (`WorldState.cs:372`) is a full pairwise table written by `CatchmentSystem`. **Nearest-first ordering needs no schema change whatsoever.** |
| Are borders/claims/control available? | **Structurally yes, behaviourally no.** `ClaimRow`/`ControlRow` exist and serialize, but nothing writes them and no polity assignment exists. Any border rule would gate on a table that is empty in every world the sim can currently produce — i.e. it would be **vacuous by construction**, the exact defect ADR-015 §7.4 names (*"a guard whose red has never been seen is not a guard"*). |
| Does D-037's territory model provide enough to determine access? | **The MODEL does; the DATA does not.** D-037 A3's claim/control/recognition triple is expressive enough. T4.3 shipped the schema and explicitly deferred every mechanism. **Border-gated migration is blocked on a polity-assignment mechanism that no milestone has yet delivered.** |
| Would "nearest" reuse existing travel-time infrastructure? | **Yes** — `TravelCost` is already the lattice cost `dampingDecayCostUnits` is denominated in (`sim.json` `_docDamping`: *"the same units as SettlementDistanceRow.TravelCost"*). |
| Compatible with deterministic ordering? | **Yes, but only with care.** `TravelCost` is a `double`, so a nearest-first sort is an ordering over doubles and CLAUDE.md requires *"a composite key with a stable integer tie-break (score, id) — and ships a tie-dense test proving it."* Sort by `(TravelCost, destination id)`. |
| What if several destinations are equally near? | **Not answerable from the tree — this is a genuine design question.** Ties are not rare artifacts here: a lattice-derived cost over a symmetric map produces exact ties readily. Three defensible answers (first by id; split proportionally; split evenly) give different behaviour, and **none is ratified**. See §12.4 R3. |

**On the intuitive hierarchy (internal → accessible foreign → farther → none):** levels 1 and 2 are
**indistinguishable in the current tree** because no settlement belongs to a polity. The hierarchy
collapses to "nearer → farther → none", which the current continuous damping already produces
*statistically* (nearer destinations get exponentially more flow) but not *lexicographically* (a
distant destination with a large gap can outdraw a near one with a small gap). **That difference —
statistical preference vs strict ordering — is the real behavioural content of the proposal's
"nearest" clause, and it is what needs a ruling.**

---

# PART 7 — CORRIDOR AND CALIBRATION IMPACT

**No band, window, quarantine or corridor was touched.** Architecture and calibration are kept
separate below, as instructed.

### 7.1 The corridor's definition is written in the machinery the proposal deletes
`corridors.json` `canonical.migrationGrossPerDecade`, band `[0.001, 0.01]`, quarantine **active**,
owner *"M4 CR-002 packet"*. Its ratified note defines the measured quantity as:

> *"LONG-DISTANCE PERMANENT INTER-SETTLEMENT RELOCATION between sites spaced **≥480 km apart** …
> It is NOT a migration propensity: baseRatePerYear 0.03/0.018/0.012 gives 0.43/0.41/0.42 %/decade,
> barely moving and non-monotonically, **because the T2.8 gap-closing cap binds and
> m\* = (R_j·P_i − R_i·P_j)/(R_i+R_j) depends only on resources and population — the rate cancels
> out entirely.**"*

**The corridor's own derivation is stated in terms of `R`, `m*`, and the gap cap.** Delete them and
the sentence defining what the number means no longer parses. **This is the single largest
calibration consequence: the corridor would have to be re-derived from its reference class, not
re-measured.**

### 7.2 What becomes invalid, what survives

| measurement | status under the simplification |
|---|---|
| `migrationGrossPerDecade` **band derivation** | **INVALID as reasoning** (its "rate cancels out" argument is cap algebra). The **reference class** (LBK Neolithic spread, Bantu expansion, Russian/Siberian frontier, North American interior) and the two bounds — *"below 0.1 %/decade a 26× land-quality differential is never exploited"*, *"above 1.0 %/decade subsistence societies cannot sustain that movement"* — **survive**, because they are historical arguments, not model algebra. **The band edges could plausibly be re-derived to the same numbers by a different route.** |
| the **≥480 km** framing | **INVALID.** Nearest-first moves flow to short hops by construction. |
| its **quarantine** (window `[0.0009, 0.01]`, owner CR-002) | **Untouched, and must stay so.** The quarantine's stated question — *"whether an absolute floor is the right shape"* — is **orthogonal** to this proposal and survives it intact. |
| density corridors | **Unaffected by migration architecture.** CR-004 draft Track E proves migration is *"structurally irrelevant to the world-total Malthus metric"* — pure `Ledger.Transfer` between existing settlements; it cannot change world population. |
| Malthus corridors | **Unaffected by this proposal; blocked on T4.4** either way (CR-003 §5.2(a)). |
| ADR-018 spacing | **Unaffected.** Measured in an earlier session and recorded: `MigrationFlowRow` carries no distance, 44/66 pairs remain ≥480 km, and median damping *rose* 0.1098 → 0.1985 — the wrong sign for the spacing hypothesis. |
| T3.4b/T3.4c weight derivations | **SUPERSEDED WHOLESALE** if the land term goes. T4.12 is their existing home. |
| migration goldens | Three behavioural goldens plus `ci.yml`'s `FOUNDED_GOLDEN` move. Ordinary OLD → NEW → CAUSE work. |

### 7.3 Is today's low migration a genuine model result or a granary-cap artefact?
**It is a T4.2 artefact, and this is measured twice, independently, and agrees both times.**

- **CR-004 draft §2a:** *"Migration's collapse is **100% T4.2**"* — T4.1 alone moved migration in the
  healthy direction (0.001533 → 0.001827, floor breaches 1/20 → 0/20); **T4.2 alone collapsed it
  (0.001827 → 0.000352, breaches 0/20 → 20/20)** via `R_i` reading the exact grain stock
  `ConsumptionSystem.BoundStore` drains through Spoilage/GranaryOverflow.
- **My own single-variable control in an earlier session** (config-only, populations within 2%):
  cumulative gross **5,124 vs 34,416 (6.7×)**; turn-650 dispersion **5.14e-4 vs 4.63e-2 (90×)**.
  Mechanism: **the granary cap is denominated in *years of demand*, i.e. already per-capita
  normalised, so it strips the food term of inter-settlement dispersion.**

**This matters enormously for the proposal and cuts against it.** The proposal's premise is that
migration should be driven by food stress. **T4.2 has already made the food term nearly
dispersion-free.** A model driven *only* by food would inherit that flattening with no second term
to carry the signal — and `corridors.json` records that at σ=0 the shipped world **already reads 33%
below the floor**. **A food-only model is at material risk of producing *less* migration than today,
not more.** Whether that is acceptable is a calibration question the implementing packet must
measure before the architecture is committed.

### 7.4 The land term: a measured tension I am reporting, not resolving
The same corridor note records a factorial (canonical seed 1, 650 turns):

> *"the number is NOT set by land heterogeneity — that attribution was refuted by factorial …
> σ swept 0→0.5 moves the corridor **3.70×** … while **the land term's entire main effect is 2.0%
> and deleting it outright moves the value −0.8%**."*

**So the land term — which m2-spec §3:25 ratifies, `_docLandWeight` derives at length, and CR-003
§5.1 ties to the emergence property — is empirically near-inert at canonical settings today,** and
T3.4c measured its design point missed by **2.3×–8.1×** in every configuration including the rig it
was derived on. **This is a genuine, already-documented tension between a ratified intent and a
measured effect. It is T4.12's subject.** It does not authorize deleting the term — CR-003's
emergence requirement is about what must happen *when land fills*, and land has not filled, precisely
because T4.4 does not exist yet. **The term may be inert today and load-bearing later.** That is
exactly what "the driver switches with the regime" predicts.

### 7.5 More or less meaningful?
**Less meaningful in the short run, potentially more in the long run.** Short run: the corridor's
definitional sentence stops parsing and the ≥480 km framing dies. Long run: a corridor over a
causally legible mechanism is easier to defend against its reference class than one whose value is
set by a cap-binding interaction nobody can explain without algebra. **But it must be RE-DERIVED,
and re-derivation is a CR-002/T4.12-owned act, not a side effect of an architecture packet.**

---

# PART 8 — TEST IMPACT (no test was edited)

**26 tests across 4 dedicated files, plus dependants.**

### `MigrationTests.cs` (12)
| test | class |
|---|---|
| `Direction_NetFlowRunsPoorToRich` | **ARCHITECTURE — must remain** (m2-spec T2.5 accept, verbatim). **Under the proposal it becomes unsatisfiable in a fed world. This test is where the conflict bites hardest.** |
| `UnreachablePair_ZeroFlow_AtTheTableLevel` | **ARCHITECTURE — must remain unchanged** (m2-spec §3:25 no-teleport) |
| `MigrantCohorts_YoungAdultPeaked_VsSourceDistribution` | **ARCHITECTURE — must remain** (m2-spec §3:25 cohort profile) |
| `FamineAtOneOfTwelve_ExitCrossesTheFractionBeforeDeathDoes` | **ARCHITECTURE — must remain** (T2.5 accept "Exit-before-death"; D-021 valve 3) |
| `Migrants_KeepTheirFullBucketKey_ClassesTravelSeparately` | **ARCHITECTURE — must remain** (law 1 + D-026 key) |
| `Chronicle_RecordsDeliveredFlow_NotRequested_WhenClampBinds` | **ARCHITECTURE — must remain** (law 1 honesty) |
| `Overdraw_ScalesProportionally_NoFirstDestinationGrab` | **must be REWRITTEN** — the property (no first-destination grab) is architectural; the proportional *formula* is implementation |
| `FamineFlight_FiresWithZeroGap_AndScalesWithTheFactor` | **must be REWRITTEN** — "with zero gap" presupposes a gap channel. The property (flight is source-driven and gap-independent) is D-021/ADR-012 architecture and must survive in a new form |
| `Damping_NearerDestinationReceivesMore_InTheExpRatio` | **must be REWRITTEN** — "nearer receives more" is architecture; **"in the exp ratio" is the exact clause the proposal changes** |
| `Direction_EqualAttractiveness_ZeroGrossFlow` | **OBSOLETE** under the proposal (no attractiveness) |
| `MagnitudeCorridor_FedPhaseDrift_WithTeeth` | **must be REWRITTEN** — a fed-phase corridor is meaningless when the model forbids fed-phase migration |
| `Distances_ComputedWithCatchments_EventSkipStillBinds` | **must remain** (coupling to D-016 recompute gate) |

### `MigrationStabilityTests.cs` (6)
`Smoothing_FilterUpdate_ExactRecurrence_AndDesireReadsSmoothed` and `Smoothing_DtHalving_FirstOrderConvergence`
— **OBSOLETE** (they test the EMA, item 14, whose warrant §5 shows is discharged).
`GapCap_PairGrossFlow_NeverExceedsFractionOfEqualizing` — **OBSOLETE as written**; the *property*
(no overshoot) is D-021 paired-feedback architecture and needs a replacement pin in whatever form
the new model bounds flow.
`Detector_Teeth_SyntheticPingPongCaught_SmoothDriftPasses`, `BifurcationConfig_NoTwoTurnOscillation_AnySettlement`,
`CanonicalAutoplay_OccupancyConcentration_Bounded` — **MUST REMAIN, UNCHANGED, AND ARE THE MOST
IMPORTANT TESTS IN THIS AUDIT.** They are model-agnostic pathology detectors. They are how a future
packet proves §5's caveat — that the simplified model does not reintroduce oscillation by another
door.

### `MigrationConcentrationTests.cs` (4)
All four (`ConcentrationDetector_FiresOnTheRecordedPreFixVector`, `MagnitudeBound_PerSimYear_StillFiresOnTheRecordedPathology`,
`NoSingleSettlementAbsorbsWorldMigration_OverAMillennium`, `PersistentExportFromMarginalLand_Survives_AndConverges`)
— **MUST REMAIN.** Outcome-level, mechanism-agnostic. `PersistentExportFromMarginalLand_Survives`
deserves special attention: **marginal land exporting people persistently is a fed-world
differential behaviour, and a food-stress-only model may not reproduce it.**

### `CollapseStabilityTests.cs` (4)
All four — **MUST REMAIN UNCHANGED.** These are ADR-012's permanent regression battery, each
verified to fail on pre-fix code. The proposal keeps ADR-012's gates, so these should pass
unmodified. **If they do not, the proposal has broken ADR-012 and that is a blocking result.**

### Missing coverage a future packet must add
1. **No test pins "nearest is preferred over farther-but-better"** — it cannot exist today.
2. **No tie-density test for equal-distance destinations** — CLAUDE.md mandates one for any ordering over doubles.
3. **No test that unplaced departure demand is conserved** (nothing computes it).
4. **No border-gating test** — and none can be non-vacuous until something writes `ControlRow`.
5. **No pin on the regime switch itself** — no test asserts that the driver moves from land to hunger as stores thin. That is `_docLandWeight`'s central claim and it is **unpinned in either model.**

---

# PART 9 — MODEL COMPARISON

**MODEL A (current, from source).** Per (source bucket × destination): a per-year rate × cohort
weight × PREV count × `exp(−travelCost/25)` × `viability(dst)` × (`gapScale` × `max(0, S_dst − S_src)`
+ `8.0 × deficit_src`), where `S` is a 20-year EMA over `A = (0.02·grain + 0.078125·arableKm²)/max(pop,1)`,
`gapScale` caps the pair's gap desire at `0.25 × m*`, desires are proportionally scaled to the
bucket's PREV count, and transfers execute in ascending (src, dst, bucket-key) order through a
persistent sub-person remainder.

**MODEL B (proposed).** Food stress → nearest viable destination → migrate; no viable destination →
no migration; colonization handles unserved frontier pressure.

| dimension | A | B |
|---|---|---|
| **architectural compliance** | **Compliant by construction** — every A-class item traces to m2-spec §3:25, D-021 or ADR-012 | **NON-COMPLIANT as stated** — contradicts m2-spec §3:25 (differentials, damping), T2.5's direction criterion, and CR-003 §5.1's emergence requirement |
| **causal clarity** | **Poor.** Five multiplied terms; nobody can explain an observed flow without algebra; the corridor note needs a paragraph of `m*` to say what the number means | **Excellent.** One cause, one ordered search, one outcome. This is the proposal's real merit and it should not be dismissed |
| **emergent behaviour** | Regime-switching driver (land→hunger) **by design**; measured near-inert on the land side today (§7.4) | **Single-regime by construction.** Frontier land-seeking migration **cannot emerge** — it must be added back as a second term, at which point B converges toward A |
| **determinism** | Solved. No RNG, array scans, pinned ascending order | **A new obligation**: nearest-first is an ordering over `double` travel costs, requiring a composite `(cost, id)` key and a tie-dense test |
| **calibration burden** | High but **already paid**; band derived, quarantined, owner assigned | **Re-derivation required from the reference class** (§7.1). Non-trivial, and at risk of reading *lower* than today (§7.3) |
| **pathological loops** | Two known, both **fixed and pinned** (T2.8 slosh, ADR-012 resurrection) | **Kills the attractiveness pathology at the root** (§5). **Unproven** against the slosh via the uncapped famine-flight channel |
| **interaction with colonization** | Deficit-ratio trigger **runs away** (measured, §4) | **Self-limiting by three brakes**, and reverses the sign on the newborn settlement that broke T4.4 (§4 Q6). **B's decisive advantage** |
| **interaction with borders** | None | None **today** — B's border clause is unbuildable until a polity mechanism exists (§6) |
| **computational complexity** | `O(n²)` sweep + `O(n²)` cap pass; the `new int[maxId+1]` and `n×n` matrices are what made T4.4's runaway non-terminating | `O(n log n)` per source if nearest-first short-circuits; **only if** it stops at the first viable destination |
| **explainability** | Weak | Strong |
| **future extensibility** | Wage differentials (D-021 Part 3, M5/M8) **drop straight into the existing gap channel** | Wage-differential pull **has no home in B** — it is a pull, and B has deleted the pull channel. **B is less extensible toward D-021 Part 3's own commitment.** |

**The honest summary:** B wins decisively on causal clarity, on the ADR-012 pathology, and on
colonization. A wins decisively on ratified compliance, on regime emergence, and on extensibility
toward the pull-driven mobility D-021 Part 3 already commits to. **The two are not rankable without
a director decision about which of those the project values, because the tree currently commits to
both.**

---

# PART 10 — REQUIRED FINDINGS

**F1 — What migration actually represents.** Ledger-conserved relocation of whole people between
existing settlements, by cohort, keeping their full bucket key, along a finite-travel-cost network,
serving simultaneously as (a) a spatial-equilibration mechanism and (b) **D-021's Exit valve**. It
is *not* a world-population mechanism — CR-004 draft Track E proves it structurally cannot change
total population.

**F2 — What causes migration per ratified architecture.** **Two causes, not one.** m2-spec §3:25:
food-per-capita and land-per-capita **differentials** (the standing driver), **amplified** by famine
deficit (the surge). D-021 Part 3 adds a third, scheduled for M5/M8: **wage differentials**. The
proposal's "food stress is the cause" is a *subset* of the ratified position, not a restatement of it.

**F3 — Is attractiveness architectural or implementation-level?** **ARCHITECTURAL.** Its two terms
are named in m2-spec §3:25; its weights are a ratified derivation under CR-003 §2.6; CR-003 §5.1
ties the land term to the emergence property. **Its INTERNAL FORM (per-capita `R/P`, the specific
weights, `m*`) is implementation and is already known-defective** — design point missed 2.3×–8.1×
(T3.4c), land term's main effect 2.0% (corridor factorial). **The concept is ratified; the
formulation is open and already has a packet, T4.12.**

**F4 — Is EMA architectural or implementation-level?** **IMPLEMENTATION-LEVEL (class B).** It
appears in no ratified document. It is a T2.8 director-ruling remedy for one measured attractor. Its
warrant is entirely derivative of attractiveness: **remove attractiveness and the EMA is
unmotivated.** It is nonetheless persistent world state with a serialized row, so removing it is a
schema change.

**F5 — Is distance a ranking signal, a feasibility constraint, or both?** **Ratified as BOTH, but
in one combined continuous form.** m2-spec §3:25 gives feasibility (*"no teleporting: flows only
between settlements with finite travel cost"* — the +∞/`exp(−∞)=0` construction) and weighting
(*"damped by network travel cost"*). **What is NOT ratified is distance as a strict ORDERING.**
Nearest-first is a genuinely new rule. **This is the cleanest single ruling this audit needs.**

**F6 — Are borders already expressible?** **Schema yes, behaviour no.** `ClaimRow`/`ControlRow`
exist and serialize; the only writer in the entire tree is `CanonicalSchema` deserialization; T4.3
explicitly deferred every mechanism; D-037 A3's cardinality is documented but unenforced. **A border
gate written today would be vacuous in every producible world** (ADR-015 §7.4). Blocked on a
polity-assignment mechanism no milestone has delivered.

**F7 — Is destination viability already defined?** **YES, verbatim, and the proposal can adopt it
unchanged.** ADR-012: reachability via damping, food via the deficit gate and the absolute food
gate. The proposal adds two conditions ADR-012 lacks: borders (F6, unbuildable) and capacity
(nothing ratified exists).

**F8 — Does migration need a destination identity?** **Not for the current model; YES for the
proposed one, and YES for T4.4's clean fix.** `MigrationFlowRow` is a per-settlement aggregate with
no destination. Nearest-first needs destination identity only transiently (already available), but
**exposing unplaced departure demand needs a new persisted quantity** — a real schema change with a
POPULATED-table test.

**F9 — Can colonization cleanly consume failed-destination pressure?** **YES — cleanly, additively,
and with less machinery than T4.4 currently has.** The quantity is already computed inside
`MigrationSystem.Step` and merely discarded. Consuming it requires no change to the T2.8 cap, no
change to ADR-012, no second clock, no virtual settlement. **This is available under EITHER model
and does not depend on the simplification.** (§4 Q1–Q6)

**F10 — Does the simplified model violate any ratified document?** **YES — three, specifically:**
1. **`m2-spec.md` §3:25** — deletes "driven by food-per-capita and land-per-capita differentials" and reframes "damped by network travel cost" as an ordering.
2. **`m2-spec.md` §4 T2.5 acceptance** — "richer settlement gains migrants from poorer" becomes unsatisfiable in a fed world.
3. **CR-003 §5.1 + `sim.json` `_docLandWeight`** — hardwires the driver to hunger, against *"the driver SWITCHES WITH THE REGIME rather than being hardwired"* and *"the Malthusian trap must EMERGE when land fills. It must never be hardwired."*
It violates **no** determinism law, **no** conservation law, and **not** ADR-012 (which it preserves
intact and in fact honours more cleanly).

**F11 — Rules that must remain untouched.** ADR-012's two gates and their application to **both**
channels; D-021 valve 3's source-driven, gap-independent, uncapped Exit valve; law 1 conservation via
`Ledger.Transfer`; the full bucket key surviving relocation; no-teleport; `long` stocks with banked
sub-person remainders; law 5 determinism including a composite tie-break for any new ordering;
CR-003 §5.4's standing constraints; **every corridor band, window and quarantine**.

**F12 — What can be deleted/replaced with NO ruling.** Only the class C/D items: the `k`-index
shortcut (22), the duplicated desire/transfer product computation and its acknowledged ULP
divergence (23), the `n < 2` guard (24), and — if a founding invariant guarantees identical bucket
layouts — the destination-key fallback and refund path (21). **Roughly 40 lines. Nothing behavioural.**

**F13 — Genuine director decisions.** R1–R5 in §12.4.

**F14 — Should T4.4 resume after migration simplification?** **T4.4 should resume, but it is NOT
blocked on the simplification.** The failed-demand hinge (F9) is available under the current model
and fixes the measured cascade. **Sequencing recommendation: fix T4.4's trigger first, under the
current migration model.** Rationale: (a) T4.4's defect is a wrong trigger, not migration's
complexity; (b) CR-003 §5.2(a) makes T4.4 the gate on the quarantined Malthus corridors, so it is
the higher-value unblock; (c) **a migration redesign should be measured in a world where the frontier
can actually close** — §7.4 shows the land term looks inert *precisely because* land has never
filled, which is T4.4's absence. **Re-deriving migration before T4.4 exists risks deleting a term
for being inert in the only regime that could not exercise it.**

**F15 — Dedicated packet or T4.4 patch?** **A dedicated packet, unambiguously — and one already
exists.** `m4-spec.md`:273 **T4.12, "the migration-weight packet"**, adversarial-mandatory, no
dependencies. This work is **larger** than T4.12 as currently scoped (T4.12 owns the *weights*; this
owns the *causal structure*), so T4.12 would need to be widened by ruling or a new packet cut
alongside it. **It must never be folded into T4.4** — that would repeat the T3.3 precedent of a
shipped regression built on an unratified finding.

---

# PART 12 — VERDICT AND BOUNDARY

*(Numbered per the packet's eight required outputs.)*

### 12.1 VERDICT
**NOT COMPATIBLE AS STATED. COMPATIBLE IN THREE OF ITS FOUR CLAUSES.**

The proposal's destination-viability clause, its no-viable-destination clause, and its
colonization-handoff clause are **already ratified and already built** — ADR-012 and D-037 B1 say
them almost verbatim. Its **source-trigger clause is refused** by m2-spec §3:25, T2.5's acceptance
criterion, and CR-003 §5.1's emergence requirement. Its **nearest-first clause is unratified** —
neither refused nor permitted; it is simply new.

**The prompt's framing question — "is the migration model unnecessarily complicated?" — answers
NO on the evidence.** 12 of 25 mechanisms are explicitly ratified, 7 more are conservation or
determinism obligations, 1 is a declared-dead note, and only 5 are removable implementation
scaffolding. **The complexity is the ratified rule's complexity, not an implementation's.** What is
genuinely wrong is narrower and already documented: the attractiveness *formulation* (design point
missed 2.3×–8.1×, land term's measured effect 2.0%) and its *input* (T4.2's granary cap having
stripped the food term of dispersion — 100% attribution, twice measured).

### 12.2 KEEP (must remain)
ADR-012's deficit gate, absolute food gate, and their application to both channels · D-021's
source-driven, gap-independent, uncapped Exit valve · no-teleport / finite-travel-cost-only flow ·
`Ledger.Transfer` conservation with the full bucket key preserved · cohort profile · per-sim-year
rates integrated with `dtYears` · banked sub-person remainders · deterministic ascending execution ·
**all three stability/pathology detector families unchanged** (`MigrationStabilityTests` oscillation
detectors, `MigrationConcentrationTests`, `CollapseStabilityTests`) · every corridor band, window and
quarantine.

### 12.3 REMOVE (no ruling needed)
Items 21–24 only: the `k`-index shortcut; the duplicated desire/transfer product computation with
its ULP divergence; the `n < 2` guard; and the destination-key fallback/refund path **if** a founding
invariant is proven. **~40 lines, zero behavioural change.** Everything else requires a ruling.

**Conditionally removable, but only as a consequence of a ruling on R1:** the EMA (14) and the
gap-closing cap (13). Neither may be removed on its own merits — the EMA's warrant is discharged
only if attractiveness goes (F4), and the cap is cited as precedent by m3-spec D-033/D-034.

### 12.4 RULING REQUIRED
- **R1 — the source trigger.** Does migration remain differential-driven with famine as amplifier
  (m2-spec §3:25, status quo), or become food-stress-only? **This is the whole decision.** Adopting
  the proposal requires amending m2-spec §3:25 **and** disposing of CR-003 §5.1's emergence
  requirement, which means **a CR, not a packet.** My recommendation: **do not adopt as stated.**
  If the goal is causal legibility, the achievable version is *"one driver whose regime switches"* —
  which is what `_docLandWeight` already claims and what T4.12 exists to make true.
- **R2 — distance as ordering.** Is nearest-first a new ratified rule, replacing or supplementing
  `exp(−cost/decay)`? **Unratified either way; nothing in the tree forbids it.** Note it directly
  contradicts the migration corridor's ≥480 km framing (§7.1).
- **R3 — equal-distance ties.** First-by-id, proportional split, or even split? Not answerable from
  the tree; ties are common on a lattice-derived cost.
- **R4 — border gating.** Ruled in principle by D-021 (*"openness of exits… free movement"*) and
  D-037 A3, but **unbuildable** until something writes `ControlRow`. Ruling needed on whether a
  migration packet may deliver the polity-assignment mechanism, or must wait.
- **R5 — T4.12's scope.** Widen the existing migration-weight packet to own causal structure, or cut
  a new packet beside it?

**Not a ruling, but a required sequencing decision:** whether T4.4 proceeds first (F14 recommends
yes).

### 12.5 T4.4 CONSEQUENCE
**T4.4 should be redesigned around its TRIGGER, not around the simplified migration model.** The
measured defect is that `ConsumptionDeficitRow` selects newborn settlements rather than overpopulated
ones, and that a scale-free ratio cannot be cleared by emigration. **D-037 B1 never authorized that
trigger** — it names ADR-012's no-viable-destination condition, and B1's *"extend it"* refers to
migration (§4 Q3). The fix is available **under the current migration model** and is strictly less
machinery than T4.4 ships today. **T4.4 does not need to wait for a migration redesign, and should
not.**

### 12.6 RECOMMENDED ARCHITECTURE (causal flow, for a future packet)
Stated as what the tree already supports plus the one additive quantity — **not as an endorsement of
R1's simplification**, which I recommend against:

```
per source settlement, per bucket:
  standing desire   = rate × cohort × count × dtYears × (differential term)   [m2-spec §3:25 — KEEP]
  surge desire      = rate × cohort × count × dtYears × FamineFlight × deficit_src  [D-021 valve 3 — KEEP]
        ↓
  for each destination, ordered by (TravelCost, destinationId):              [R2 — NEW, needs ruling]
      viable?  reachable (finite cost) ∧ (store>0 ∨ lastHarvest>0)
               ∧ max(0, 1 − Repulsion × deficit_dst) > 0                     [ADR-012 — KEEP VERBATIM]
               ∧ borders permit                                              [R4 — NOT BUILDABLE YET]
        ↓
  place what can be placed  → Ledger.Transfer, full bucket key, ascending order, banked remainder
  record what could NOT be placed → UNPLACED DEPARTURE DEMAND                [NEW, additive, schema]
        ↓
COLONIZATION (next system, same turn) consumes unplaced demand:
  party = unplaced demand (already whole people, already conserved)
  site  = frontier siting under the ADR-018 spacing floor + explicit SiteCell distinctness
  ↓ placing the party DISCHARGES the demand — self-limiting by construction
  ↓ the new settlement is itself a viable destination next turn — the cascade brake (§4 Q6)
```

The only genuinely new object is **unplaced departure demand**. Everything else is already in the
tree.

### 12.7 IMPLEMENTATION BOUNDARY
**A future packet MAY, without another director conversation:** delete items 21–24; expose unplaced
departure demand as an additive quantity (new row + POPULATED-table test + ADR, since it touches the
serialized contract); change T4.4's trigger to consume it; re-derive goldens with OLD → NEW → CAUSE;
add the missing tests in §8.

**A future packet MAY NOT, without a ruling:** delete or replace the attractiveness terms, the gap
channel, the EMA, or the gap-closing cap (R1) · make distance an ordering (R2) · choose a tie rule
(R3) · add border gating or write `ControlRow` (R4) · move any band, window or quarantine · amend
m2-spec §3:25, T2.5's acceptance criteria, CR-003, ADR-012, or D-021 · re-scope T4.12 (R5).

### 12.8 MIGRATION / COLONIZATION HANDOFF — the conceptual contract
> **Migration owns the question "can these people be placed in a settlement that already exists?"
> and answers it completely. Colonization owns only the residue: the people migration wanted to move
> and could not place. Migration never fabricates a destination; colonization never re-decides a
> destination migration already found.**

Four properties follow, and each is testable:
1. **Conservation.** Unplaced demand is measured in whole people from the same buckets, so the
   handoff is a `Ledger.Transfer`, never a source.
2. **No double-spend.** Colonization runs after migration and draws from **live post-migration
   counts** — whoever left is already gone.
3. **Self-limitation.** Placing a party discharges the demand. **A ratio cannot be discharged; a
   count can.** This is the entire difference from T4.4's current trigger.
4. **The cascade brake.** A newly founded settlement holding provisions is a **viable destination**
   under ADR-012's own gate, so next turn the same stress is *placed into it* rather than founding
   again. Under the current trigger the same settlement *emits* a founding. **Opposite signs on the
   settlement that broke T4.4.**

---

## §13 WHAT THIS AUDIT DID NOT DO
No code, data, test, golden, corridor, band or quarantine was modified — `git status` shows this one
new file. No independent agent reviewed any finding here. No measurement was re-run for this audit;
the quantitative claims are cited to their sources (`corridors.json` notes, CR-004 draft `f7a3345`,
`docs/t3.4c-remeasurement.md`, `docs/t4.4-review-record.md`) and the two I produced myself in earlier
sessions are labelled as mine. **The T4.2 attribution (§7.3) is the one claim resting on two
independent measurements that agree; every other quantitative claim rests on a single source.**
