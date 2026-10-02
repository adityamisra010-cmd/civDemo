# Universities as real institutions (ADR-033 D6) — design note

**Status:** design committed BEFORE code on `m5i-s3-institutions` (cut from `m5-integration` @ `5e7fa35`).
Every constant below is TUNE; each carries the reference class it was derived from, fixed here before any
measurement of its effect (CR-003 §5.1). Numbers marked MEASURED were measured by the agent writing this
note; historical figures are approximate and marked INFERRED.

**Binding inputs.** ADR-033 D6 (the decision); ADR-028 (RATIFIED: viability is hierarchical and
type-specific, saturation is thresholds plus diminishing returns, LOCKED / AVAILABLE / SATURATED are
derived, UNDER_CONSTRUCTION / ACTIVE / MATURE are stored; no maximum count in any form);
`civilization-progression-architecture.md` §8.5 (D-044 R5 / R20-H / R20-I: universities act on research
only through `BaseCost → EffectiveCost`; no currency, no flat RP, no queue; the formula is not ratified;
the first writer owes D-021's brake in the same milestone); ADR-029 §9 (the seam
`ResearchCostModifierRow(Polity, UniversityType, Factor)`, Factor in (0, 1], applied only to Technology
nodes of the matching subtree, floor 20 % of BaseCost); D-021 (the paired-feedback rule); the Director's
integration directive ("Universities are real institutions with specialties … diminishing returns;
maturity; saturation; local specialization; labor constraints; diffusion. Do not make universities instant
magic modifiers"; RESEARCH MECHANICS — DO NOT CHANGE).

---

## 1. The lifecycle, and where each state lives

```
knowledge (research)  ──►  AVAILABLE  ──order──►  UNDER_CONSTRUCTION  ──build──►  founded (ACTIVE, M = 0)
   LOCKED (derived)        (derived)              (ConstructionQueue row)          (Institutions row)
                                                                                       │ matures while viable
                                                                                       ▼
                       effects: research cost (polity) · health (settlement, diffusing)   MATURE (M ≥ 0.95)
                       feedback: staff withdrawn from labour → surplus → viability          SATURATED (derived)
```

| state | kind | source on this tree |
|---|---|---|
| LOCKED | derived | `ConstructionQuery.Availability(...) == NotKnowledgeEligible` — the project's entities are not knowledge-eligible |
| AVAILABLE | derived | `ConstructionQuery.IsProjectAvailable` — the existing predicate (knowledge + control); no new availability path |
| UNDER_CONSTRUCTION | stored | the `ConstructionQueue` row of a university project (and, for one turn, the completed `Structures` count not yet founded — §3) |
| ACTIVE | stored | an `Institutions` row with maturity below `matureAt` |
| MATURE | stored | the row's stored `Maturity` ≥ `matureAt` (the quantity is stored; the label is its reading) |
| DIMINISHING / SATURATED | derived | `InstitutionsQuery` from the polity's maturity-weighted sum X of a type: DIMINISHING once X > 0, SATURATED once S(X) ≥ `saturatedAt` — never a flag |

Other institution types (hospitals, schools, academies…) are OUT OF SCOPE; they follow this same lifecycle
later through the same table (its `Type` key space widens) and the same `founds` link.

## 2. State — schema v31

Two tables are appended after `TaxPolicies`:

| table | row | width | owner |
|---|---|---|---|
| `Institutions` | `InstitutionRow(int Id, PolityId Polity, SettlementId Settlement, int Type, long FoundedTurn, double Maturity)` | 32 | `InstitutionsSystem` (new, SystemId 28) |
| `ConstructionLabor` | `ConstructionLaborRow(SettlementId Settlement, double LastLaborUsed)` | 12 | `ConstructionSystem` (D10, §8) |

- `Id`: stable, assigned one past the highest id ever in the table; rows are never removed in this pass.
- `Polity`: the OWNER — the controller of the settlement when the institution was founded (0 = founded in
  a stateless settlement: it serves no polity's research but still heals locally). The research effect
  accrues to the owner. Control is not duplicated: a later revolt changes `Controls`, not this row
  (transfer of institutions on conquest or secession is later work — reported as an open question).
- `Type`: the research.json `universityTypes[].key` (1 military, 2 medical, 3 engineering, 4 natural
  science, 5 agricultural).
- `FoundedTurn`: the turn of the first state that contains the row.
- `Maturity` ∈ [0, 1]: THE stored quantity (ADR-028 §3). Staffing, viability, every effect and the
  saturation reading are derived from it and from published state; nothing else is stored.

Neither table carries people, money or goods, so the Ledger is untouched (law 1).

## 3. Founding through the existing construction queue

- Five projects in goods.json, one per specialty, ids 11–15 (10 + the type key; ids 3–10 stay free for
  buildings). Each carries the S2 link `"entity": "building.university"` and a new `"founds"` object
  `{ "entity": "inst.university", "universityType": "<type id>" }` — data, validated at the load where
  goods.json meets research.json (the entity must be an institution, the type must exist, one project per
  type).
- **Both entities gate (L4 decision).** `inst.university` requires `library AND legal_code_roman`;
  `building.university` requires `medicine_hippocratic AND (geometry_axiomatic OR algebra)`. The
  research stage that opens the five subtrees (D-044 R4) is, by its own `_doc`, the knowledge reading of
  BOTH (plus `building.library`), and §8.4.2 says the stage "exists specifically to support specialized
  universities". If only the institution gated, a specialized university could be founded before the
  subtree its cost factor acts on is even open (MEASURED: the closure of `inst.university` alone is 18
  nodes / 25,210 RP; the union with `building.university` is 29 nodes / 60,440 RP — exactly the stage's
  closure). A project builds a building and founds an institution, so the existing predicate checks the
  project's `entity` AND its `founds.entity` (`ConstructionQuery.IsKnowledgeEligible`); there is still one
  availability predicate and one caller pair.
- **Materials and labour (TUNE).** Reference class: the shipped workshop (60 timber, 40 stone, 8.0
  adult-years for a craft workshop of ≈ 50 m²) scaled by floor area to a collegiate complex of ≈ 1,500 m²
  (hall, library/chapel, lodgings round a quadrangle — INFERRED from high-medieval Oxford/Cambridge
  colleges): ×30 ⇒ **1,800 timber, 1,200 stone, 240 adult-years**. The workshop's 10 tools are its own
  equipment, not building material, so they do not scale. The project is built whole in one turn or waits
  (ConstructionSystem's gate) — a university is a project a village cannot marshal and a city can.
- **Viability is a resolution gate** beside materials and capacity: the queue head of a university
  project resolves only where the FOUNDING viability (§4) holds on PREV; otherwise it waits like a head
  short of stone. LOCKED/AVAILABLE stay the existing predicate.
- **Single writer, one-turn lag (pinned turn-exactly).** ConstructionSystem completes the project as it
  completes any project (materials through Ledger reason `ConstructionMaterials`, a `Structures` count).
  InstitutionsSystem, which reads only PREV, founds one `Institutions` row per completed university
  building not yet founded (`Structures` count of the project at the settlement minus the rows of that
  type there). Order stamped t → queued and built in the step t→t+1 (state t+1 shows the structure) →
  founded in the step t+1→t+2 (state t+2 shows the row, `FoundedTurn` = t+2, `Maturity` = 0) → first
  maturation in t+2→t+3. No instant effect.

## 4. Viability (ADR-028 §1: type-specific, hierarchical, from real state)

Every term reads PREV. For a settlement S hosting n university buildings (all types; `Structures`):

| term (ADR-028 §1.2 kind) | to FOUND the (n+1)-th | to SUSTAIN (keep maturing) | reference class |
|---|---|---|---|
| hierarchy | the content requirement (§3) | — | `inst.university` on `library`; `building.university` (D-044 R4 stage) |
| local capacity — market | adults(S) ≥ `adultsPerUniversity` × (n+1) | adults(S) ≥ `adultsPerUniversity` × n | §4.1 |
| local capacity — food | `food_surplus_ratio`(S) ≥ `foundingFoodSurplusRatio` (1.3) | ≥ `sustainingFoodSurplusRatio` (1.1) | §4.2 |
| marginal returns | the market term is per instance; the effect curve is concave (§6) | — | ADR-028 §2 |
| physical / site | none: a university needs no river, coast or deposit, and none is invented (ADR-028 §1.2) | — | — |

**4.1 `adultsPerUniversity` = 2,000 adults.** Reference class: the SMALLEST towns that hosted a
high-medieval studium generale — Cambridge c. 1300 ≈ 3,500 inhabitants (Oxford ≈ 6,000, Salamanca
≈ 5,000; INFERRED from standard urban histories) — times the founded world's adult share 0.563
(MEASURED, seed 42: 1,306 / 2,320 adults at turn 200, 3,282 / 5,826 at turn 400, settlement 0) ⇒ 1,970,
rounded to 2,000. It is a THRESHOLD PER INSTANCE scaled by population, not a maximum count: a city of
6,000 adults sustains three universities, a city of 60,000 thirty (ADR-028 §1.4/§2.3 forbid a fixed
cap; the per-instance market term is the "marginal returns" kind of §1.2 and makes absurd counts — the
ledger's "impossible university counts" — impossible by population, not by decree).

**4.2 The food thresholds are the content's own specialist thresholds.** sim.json `registries.classes`:
Artisans emerge on `food_surplus_ratio > 1.3` and recede below `1.1` ("the food exists to feed a
non-farmer", T3.1). Scholars are non-food specialists like artisans, so the same physical condition
applies, by identity, not choice. The asymmetric pair gives hysteresis without a stored latch: founding
needs 1.3, an established university keeps maturing until the surplus falls below 1.1.

When the sustaining test fails — hunger (the surplus collapses), population loss (adults fall below the
market) — maturity DECAYS (§5) instead of growing.

## 5. Maturation (dt-correct closed form)

```
viable:      M' = 1 − (1 − M) · exp(−dt / τ_grow)
not viable:  M' = M · exp(−dt / τ_decay)
```

Both are the exact solutions of the linear ODEs dM/dt = (1 − M)/τ_grow and dM/dt = −M/τ_decay over a turn
of dt years (law 3; the HousingSystem decay precedent). Two half-steps compose to one full step up to
floating rounding; M = 1 viable and M = 0 decaying are fixed points exactly. A new university starts at
0 and is founded with M = 0 (no instant effect).

- **τ_grow = 16 years.** Reference class: the founding-to-recognition interval of six early studia —
  Bologna (c. 1088 → Authentica Habita 1158: 70 y), Paris (c. 1150 masters' guild → 1200 royal charter:
  ≈ 48), Oxford (c. 1167 → 1214 legatine ordinance: 47), Cambridge (1209 → 1231 royal writ: 22),
  Montpellier (c. 1137 → 1220 statutes: 83), Salamanca (1218 → 1243 royal charter: 25) (INFERRED).
  Median 47.5 years, read as the time to the MATURE label 1 − e⁻³ ⇒ τ = 47.5 / 3 ≈ 16.
- **τ_decay = 21.7 years.** Reference class: the simulation's own adult mortality. An institution that
  cannot recruit loses its trained masters at the rate working adults leave the population; the mean of
  `demographics.mortalityPerYear` over the working cohorts 3–11 (0.024 … 0.083; sum 0.415) is 0.0461/yr,
  e-fold 21.7 years.
- **`matureAt` = 0.95** = 1 − e⁻³ rounded: the three-e-fold convention for "at its asymptote".

## 6. Effects

**6.1 Research cost (every specialty, ADR-029 seam; Engineering is the Director's named example).** For
each polity P and university type T, with X = Σ maturity over P's universities of type T:

```
factor(P, T) = 1 − maxResearchCostReduction × S(X),   S(X) = 1 − exp(−X)
```

InstitutionsSystem rebuilds `ResearchCostModifiers` every step from the maturities it has just
integrated — one row per (P, T) with X > 0, polities in roster order, types in content order — so the
table in any state is a pure function of that state's `Institutions` rows; ResearchSystem reads it from
PREV as it always has. The seam already restricts a factor to Technology nodes of T's subtree; Main-tree
and Civics nodes are untouched. Factor ∈ [2/3, 1) ⊂ (0, 1] — the 20 % floor and the 40 %-of-BaseCost
Eureka ceiling are untouched (the floor binds only when BaseCost × Π factor < 0.2 BaseCost, which no
university stack reaches).

- **Diminishing returns.** S is concave: the first mature university supplies S(1) = 0.632 of the
  maximum, the second only 0.233 more, the third 0.086 (marginal = e⁻ˣ(1 − e⁻¹)).
- **Saturation (derived reading).** SATURATED when S(X) ≥ `saturatedAt` = 0.95 (X ≥ ≈ 3 mature
  universities of the type: the next one would add < 5 % of what the first did); DIMINISHING when X > 0.
- **`maxResearchCostReduction` = 1/3.** Reference class: the ratified calibration's single stated
  assumption for "future research modifiers" — ×1.5 research throughput for a research-optimized
  civilization (D-045 Part D 3; `research-calibration-report-d045.md` §6) — read as the ceiling of one
  specialty's stack on its own branch: EffectiveCost/BaseCost = 1/1.5. It applies per branch, so the
  civilization-wide effect (trunk and Civics never discounted) is smaller than ×1.5.

**6.2 Health — medical universities (local specialization and diffusion).** ONE per-settlement seam,
`InstitutionEffects.MortalityMultiplier(prev, cfg, settlement)`, read by DemographicsSystem once per
settlement per turn and multiplied into every cohort's BASE mortality rate (never starvation: medicine
does not feed anyone):

```
coverage(S) = Σ_medical i  M_i · exp(−d(site_i, S) / healthDecayCostUnits)    (d = 0 at the site,
                                                                                SettlementDistances otherwise)
μ(S)        = 1 − maxMortalityReduction · (1 − exp(−coverage(S)))  ∈ [1 − maxMortalityReduction, 1]
```

- Bounded and saturating; strongest at the university's own settlement and decaying with travel cost
  over the road-aware network (D-040 C3); physical diffusion, no polity filter (medicine travels with
  practitioners, not with sovereignty).
- **`maxMortalityReduction` = 0.10.** Reference class: smallpox, ≈ 10 % of all deaths in 18th-century
  Europe, is the largest single cause a pre-modern medical establishment removed (variolation 1720s,
  vaccination 1796; INFERRED) — a medical establishment at its ceiling removes about that share of
  baseline mortality, no more.
- **`healthDecayCostUnits` = 25.0** by identity with `migration.dampingDecayCostUnits` and
  `governance.authorityDecayCostUnits` (both "how far does influence travel over this network"; ≈ 400 km
  of ideal ground); a second unrelated distance scale would assert a magnitude nobody measured.
- **`healthType`** = `"medical_university"` — data, no type id in code.
- A world without a medical university returns the literal 1.0 and `m × 1.0 == m` bit for bit: the
  demographic kernel executes the identical instruction values (§9).

## 7. The labour constraint and the D-021 brake

**7.1 Staffing.** A university employs scholars — real adults of its host withdrawn from productive
labour, never created or destroyed (they stay in their buckets, age, eat and die as everyone does):

```
staff(S)  = min(adults(S), Σ_i at S  staffShareAtMaturity · adultsPerUniversity · M_i)
labour(S) = adults(S) − staff(S)
```

ONE shared reader, `InstitutionStaffing.LabourAdults(prev, cfg, settlement)`, replaces the raw adult count
in EVERY sector pool: ProductionSystem (farming, herding/fishing, extraction, crafting) and the
construction pool's three consumers (HousingSystem, PathBuildSystem, `ConstructionQuery.CapacityAdultYears`)
— sector shares apply to the labour that remains, so a scholar is never also a farmer or a builder (the
same class of double count as D10). With no institution it returns `(double)adults` exactly.

- **Staffing is per INSTANCE.** Each university at maturity employs the share σ of the market it needs:
  σ × A_u = 0.05 × 2,000 = 100 adults, grown into as it matures. While the market term holds
  (n ≤ adults / A_u) a host's scholars therefore never exceed σ of its adults, however large it grows; a
  large city hosts MORE universities rather than larger ones.
- **`staffShareAtMaturity` = 0.05**, read as the scholar share of a host at its university market's
  capacity. Reference class: scholars as a share of the host town c. 1300 — Paris ≈ 4,000 / 220,000
  (1.8 %), Padua ≈ 800 / 30,000 (2.7 %), Montpellier ≈ 1,000 / 40,000 (2.5 %), Toulouse ≈ 1,000 / 35,000
  (2.9 %), Bologna ≈ 2,500 / 55,000 (4.5 %), Cambridge ≈ 700 / 3,500 (20 %), Oxford ≈ 1,500 / 6,500 (23 %)
  (INFERRED). Median 2.9 % of inhabitants ÷ adult share 0.563 ≈ 5 % of adults.
- **Measured and rejected: staff proportional to the HOST** (σ × M × adults(S) per university, this
  note's first draft). It compounds with the per-instance market — n ≤ adults / A_u instances, each
  employing σ of the host, withdraw up to σ × adults / A_u of it — so the share grows with population
  without bound. MEASURED on the seed-42 `aiEmpires = 1` run at turn 1000 with that formula: the rival's
  university hosts had 3–14 universities each and 14–48 % of their adults withdrawn (s5: 14 universities,
  Σ M = 9.6, 48 %). Per-instance staffing keeps the loop's force linear in its amplitude (§7.2) and bounds
  it by the market.

**7.2 Why this is a negative feedback loop that STRENGTHENS WITH AMPLITUDE.** The positive loop D-021
names (§8.5.2) is research → universities → cheaper research → more research and more institutions. Its
AMPLITUDE is the institutional investment: how many universities exist and how mature they are,
A = Σ M_i. The brake:

```
more universities / more maturity (A↑)
  → staff = σ · A_u · A_S withdrawn from every sector        (linear in A: the brake's force grows with amplitude)
  → herding/fishing, extraction, crafting and labour-bound farming output fall
  → food_surplus_ratio falls (and materials and construction capacity for the next university fall)
  → below 1.3 no further university can be founded there; below 1.1 maturity DECAYS
  → staff fall with maturity → output and surplus recover
```

The restoring force (labour withdrawn, output lost) is proportional to the amplitude, so it strengthens
with it, and it acts on the very variable the loop's growth depends on (viability) — D-021's paired-
feedback rule. Two damping terms sit beside it and are not offered as the brake: the effect is concave
and bounded (S(X) ≤ 1, factor ≥ 2/3), and the market term bounds the number of instances by population.
The test `InstitutionBrakeTests` MEASURES the brake on the production pipeline: withdrawn labour and lost
food grow monotonically with the number of mature universities, the surplus crosses the sustaining
threshold at a finite count, and past it maturity decays instead of growing (§12.2). Its magnitude: at
the canonical σ a host at its market's capacity loses at most 5 % of its adults in total, so the food term
binds where the surplus is within that margin of 1.1 and the market term binds first elsewhere; the rig
raises σ to show the shape, which does not depend on σ.

## 8. ADR-033 D10 — construction capacity is spent once (rides the v31 bump)

ConstructionSystem publishes the adult-years its resolved queue heads consumed this step in the
`ConstructionLabor` table (rebuilt every step: cleared, then one row per settlement that completed a
project; absent = 0, the TradeFlows/Disasters precedent), and PathBuild subtracts PREV's value exactly as
it subtracts `HousingRow.LastLaborUsed` — the §3.2 one-turn lag, a table read, never a system reference.
`ConstructionCapacityD10Tests` flips from pinning the double count to pinning the single spend. No world
without construction orders writes a row (every golden world), so the fix moves no golden beyond the v31
layout; AI runs (which do build) move, and are re-derived.

## 9. AI (same orders, same predicates)

- **Construction.** AiConstructionPolicy keeps S2's baseline rule for ordinary projects (granary,
  workshop) and gains ONE university decision per AI polity per turn, made after them: among the
  university types AVAILABLE to it, the specialty it holds FEWEST instances of, counted PROSPECTIVELY
  (founded rows + buildings completed in its settlements and not yet founded + projects of the type in
  their queues), then the lowest maturity-weighted sum X, then the lower type key (composite key
  (n, X, key): diminishing returns make the AI diversify). A type whose prospective count is already
  SATURATED (S(n) ≥ `saturatedAt`, n ≥ 3) is not founded again — the AI's reading of the derived
  SATURATED stage, not a cap on the world. (The first draft keyed on X alone; MEASURED on the long AI run
  it ordered each specialty three times running, because the founding lag and a new university's zero
  maturity left X at 0 — the prospective count is the fix.) The site is the controlled settlement with an
  empty queue where the founding viability holds, every material is present and the construction capacity
  covers the labour, choosing the largest spare market (adults − 2,000 × hosted; ties to the lower
  settlement id). Composite keys, tie-dense tests. The order is `ConstructionQuery.EnqueueOrder`, the
  player's constructor.
- **Research goals (ADR-033 B).** AiResearchPolicy's goal closure becomes the next Age's core closure ∪
  the prerequisite closures of the requirements of the capability-gated actions the AI itself uses: the
  next unknown road class (sim.json `roads.classes[].entity`), the taxation requirement
  (`governance.taxationRequires`) while it is unmet, and the university requirement (the university
  projects' `entity` + `founds.entity`) while it is unmet — institution atoms expanded to their own
  knowledge requirements. It picks the cheapest AVAILABLE node within that union (composite key
  (EffectiveCost, key)), else the cheapest anywhere (unchanged fallback). Measured against S2's baseline
  in the stream report.

## 10. Queries and the map

- `AvailableActionsQuery.Institutions` lists "Found a <type> University" per (settlement, type) exactly
  where `ConstructionQuery.IsProjectAvailable` holds (one predicate, two callers), with the founding
  viability, materials and capacity as its transient blocker; the Construction contributor stops listing
  university projects so nothing is listed twice.
- `InstitutionsQuery` (pure, read-only): instances (type, owner, site, founding turn, maturity, lifecycle,
  staff, sustaining viability and its reason), per-polity specialty readings (count, X, factor, saturation
  S(X), next-instance marginal, DIMINISHING/SATURATED), per-settlement health (coverage, multiplier), and
  the lifecycle of a (polity, settlement, type) slot (LOCKED … MATURE).
- `InstitutionMarkerSource.InstitutionsAt` (stream U3's seam) returns the settlement's institutions from
  the real table, one entry per type, ascending key, counted. The world lens no longer draws a
  university project's `Structures` count as a generic structure, so a university is drawn once.

## 11. What does not change

RP generation (0.08 × P^0.699), overflow, Eureka rules, the 20 % floor, the 40 % Eureka ceiling,
transport (frozen at `997824b`), unrest, literacy and education (no system exists; not invented), the UI
panels (streams U2/U4 render `InstitutionsQuery`). A world with no university is bit-identical in every
simulated quantity: the staff reader returns the raw adult count, the mortality seam returns 1.0, the
modifier table stays empty, and the D10 table stays empty unless a project is built. Only the stream
layout (two count prefixes) moves the goldens, attributed by `IntegratedPinAttributionTests`.

## 12. Measured results (this stream's tree, Release)

**12.1 Goldens — the v31 layout alone.** Each moved by exactly two empty four-byte count prefixes
(Institutions, ConstructionLabor): no pinned world issues an EnqueueConstruction order, so nothing is
built, no university is founded and no construction labour is published. `IntegratedPinAttributionTests`'
v31 controls strip the layer (and the ResearchCostModifiers its system rebuilds) and return each v30 pin
byte for byte with `removed == 0`.

| world | v30 | v31 | also derived by |
|---|---|---|---|
| synthetic, seed 42, 200 turns | `bbcac046…` | `0af7143fb69809fc58653ae99178ff11c8d020137b78443ac1bae46b21c8b269` | `sim run --seed 42 --turns 200 --hash-log` |
| founded, seed 42, 300 turns (`ci.yml` FOUNDED_GOLDEN) | `64820f83…` | `74306d6a574b6a680e454eb385f88e9df2d6a74c5c16fd9af64930c3cdc55c1c` | `sim run --founded --seed 42 --turns 300 --hash-log`, two processes, byte-identical logs |
| FirstReign, 40 turns | `3613dcc4…` | `481d37170d7f70f35950a358cbb831c2c78f668642cdd529f7dbfd806853ef87` | in-test harness |
| driven, seed 42, 300 turns | `638d7a09…` | `65d53a01ffe1b3e9065cd48100698ac909e3e5b44e1c96f0f32dd5d6c6dbd651` | in-test harness |

**12.2 The D-021 brake (`InstitutionBrakeTests`).** Dev world, seed 42, settlement 0 after three turns
(281 adults, published food surplus 1.3088); market `adults / 16` = 17, staff share raised to 0.8 in the
rig so each mature instance withdraws 13.6 adults (4.8 %); one production-pipeline step per k:

| k | staff | labour-bound output | food surplus ratio | maturity one step later |
|---|---|---|---|---|
| 0 | 0.0 | 1510 | 1.3088 | — |
| 1 | 13.6 | 1437 | 1.2453 | 1.0000 |
| 2 | 27.2 | 1365 | 1.1822 | 1.0000 |
| 3 | 40.8 | 1290 | 1.1187 | 1.0000 |
| 4 | 54.4 | 1218 | 1.0555 | 0.6308 |
| 7 | 95.2 | 999 | 0.8655 | 0.6308 |
| 14 | 190.4 | 487 | 0.4219 | 0.6308 |

The force is exactly linear in k, the output loss grows with k, the surplus falls monotonically, and at
k* = 4 the host can no longer sustain its scholars: maturity decays (0.6308 = e^(−10 / 21.7), one
10-year turn) instead of holding at 1. At the canonical σ = 0.05 a host at its market's capacity loses at
most 5 % of its adults (second test), so the food term binds only within that margin of 1.1.

**12.3 The AI on the canonical world (ADR-033 B; seed 42, `aiEmpires = 1`).** Measured by a probe on the
production pipeline and by `sim run --founded --seed 42 --turns 400 --ai-empires 1` (the same turn-400
hash). S2 is the tree this stream was cut from (core-only research goal).

| milestone | S2 | S3 |
|---|---|---|
| research targets | grinding_stone t0, cereal_cultivation t19 | grinding_stone t0, knapping_oldowan t19 |
| first granary ordered | t9 | t9 |
| first road (DevelopRoads order / RoadDevelopments row) | none in 400 turns | t246 / t247 |
| first levy (SetTaxRate order / positive rate) | none in 400 turns | t305 / t306 |
| first university (order / founded row) | none in 400 turns | none in 400; t722 / t724 in a 1,000-turn extension |
| Age 2 in force | t142 | t382 |
| Age 3 in force | t396 | t654 (extension) |
| Ages 4 / 5 in force | — | t744 / t965 (extension) |
| nodes completed by t400 | 17 | 22 |
| turn-400 hash | `a25d36ce…` | `e7e587c85173328d5625910299302f4e46fcb8d2482d842629e9ec9295e0d3d1` |

The union reaches the capability gates S2 never reached, and it DELAYS every Age: cheapest-first over the
union researches the many cheap ancestors of the road, tax and university closures before the core. An
offline closure computation over base costs (INFERRED: no Eurekas, RP rather than turns) gives the same
ordering — union: road 9,240 RP, tax 13,640, Age 2 20,530; S2: Age 2 3,890, tax 39,790, road 95,010 — and
for a goal-SEQUENTIAL variant (work the goal whose remaining closure is cheapest): road 3,850, Age 2 8,620,
Age 3 24,380. The packet specifies the union; the variant is recorded in `docs/queue.md`, not taken.

**12.4 Universities in the long run (same run, 1,000 turns).** The AI orders its first university at
t722 (military, settlement 1), founded at t724 (the one-turn lag), and founds fifteen — three of each
specialty, spread over five settlements — by t742; the saturation stop then holds. Staff at t1000:
≈ 1,430 adults (100 × Σ M, Σ M = 14.3) in hosts of 64,000–163,000 adults — 0.1–0.5 % of each host. The first draft (staff
proportional to the host, specialty keyed on X alone) measured 46 universities by t1000 — each specialty
ordered three turns running — with 14–48 % of the hosts' adults withdrawn (§7.1, §9).
