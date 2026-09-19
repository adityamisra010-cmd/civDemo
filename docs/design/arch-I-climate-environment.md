# ARCHITECTURE I — CLIMATE · ENVIRONMENT · POLLUTION

**DESIGN PHASE. NOTHING IMPLEMENTED.** No production code, schema, golden, corridor,
quarantine or ratified/frozen document was touched. No milestone spec was written or
amended. No packet was scoped. No ruling is made here.

**Author authority: none. The DIRECTOR IS ChatGPT.** Where two sources disagree, both are
named and neither side is taken (§14). Where the repository records no decision, the
question is stated rather than answered (§15).

**Tree pinned for every citation:** branch `claude/civdemo-work-b1z2y4` @ **`7fb84eb`**,
working tree clean. MEASURED (this lane): `git diff --stat 1276c15 7fb84eb -- Sim.Core
Sim.Data Sim.Tests Sim.Cli scripts` is **empty** — every commit between the
decision-recovery lane's pin and this one touches `docs/design/` only, so every `file:line`
this lane inherits from `docs/design/recovered-decisions-climate-env-agri.md` reads
identically here. Every RATIFIED fact leaned on below was nonetheless **re-read at its
source on this tree by this lane**, per the mandate's method; where a citation drifted it is
recorded, never silently corrected (GOV-4).

**Label key** (mandate labelling rule — every substantive claim carries exactly one):
**RATIFIED** (cite file:line) · **MEASURED** (cite record + tree) · **PROPOSED** (this
phase's design) · **INFERRED** (reasoned, not stated) · **DIRECTOR DECISION REQUIRED**.
**Most of what follows is PROPOSED. That is the honest state of this subject** — the
repository contains a static terrain field and a stochastic output multiplier and, between
them, nothing at all.

**Prior lanes read first, as the method requires:**
`docs/design/recovered-decisions-climate-env-agri.md` (E-01…E-119, C-01…C-06, T-01…T-14,
gaps 1–18) in full; `docs/design/recovered-decisions-food-disaster-needs.md`,
`docs/design/recovered-decisions-architecture-invariants.md`,
`docs/design/m4-closure-audit.md` §5.3–5.4; and the sibling design lanes
`docs/design/arch-C-knowledge.md`, `arch-D-technology-capability.md`,
`arch-E-breakthrough-domains.md` (whose F21/E11/E13 rows this lane's §6 and §7 extend rather
than re-decide). Where this design needs an object those lanes propose, it is named as
**theirs** and not re-specified here.

**PROPOSED terms introduced here, each defined once and used consistently:** the **CLIMATE
DRIVER** (§3.1) · a **CLIMATE REGION** (§3.3) · the **REALIZED TURN ANOMALY** (§3.4) · an
**EXTREME CLIMATE EPISODE** (§2) · **ENVIRONMENTAL QUALITY** (§7.4) · a **LOADING** (§7.2) ·
**ASSIMILATIVE CAPACITY** (§7.3) · the **FALLOW BUDGET** (§6.5). Every other term in this
document is the repository's own (T-01…T-14).

---

## §0 WHAT THIS ARCHITECTURE IS FOR, AND WHAT IT DELIBERATELY DOES NOT DO

This document designs the conceptual architecture for a **climate that exists as a thing
distinct from weather** — a bounded, reproducible, multi-year, regionally-structured signal
that drives the chain climate → rainfall → water → soil → agriculture → food — and for
**environmental degradation as a causal pathway rather than a happiness penalty**, from
recipe execution through air, water and soil loadings to agricultural, demographic and
settlement consequences, with the historical progression (manure, rotation, industrial
agriculture, synthetic fertilizer, pollution controls, water treatment) expressed as
*changes to causal relationships* and never as bonuses. Its one load-bearing structural move
is the separation of **normal climate variability** (a signal), **extreme climate episodes**
(a reading of that signal with no causal power of its own) and **true disasters** (drivers
with their own state, carrier and CAUSE identity) — a separation that is mechanical, not a
matter of magnitude, and that is grounded in the shipped `DisasterSystem`/`FoodState` split
rather than invented. What this document **deliberately does not do**: it does not rule G1
(§5 states precisely how the design relates to it and stops); it does not choose between the
two climate substrates it offers (§3.6); it does not decide whether a degradation stock is a
`long` on the Ledger or a rate-like `double` (§7.2, gap 14); it does not touch the derived
26.0 or propose a number for any constant; it does not resurrect a universal `CapabilitySystem`
under an environmental name (§9.1); it does not name a milestone, a packet or an owner; and
it does not pretend that the ADR-008 wall can be walked through by a design document (§6.4).

---

## §1 GROUND — THE FACTS THIS DESIGN STANDS ON, RE-VERIFIED THIS PASS

Each row was re-read at its source on `7fb84eb` by this lane. These are the only facts the
design leans on; everything else in it is PROPOSED.

| # | fact | label | source (re-read this pass) |
|---|---|---|---|
| **F1** | **The seven laws.** Conservation via `Ledger.Transfer`/`Ledger.Flow`, conserved stocks `long`, exact equality in tests · mechanisms over modifiers, free-floating permanent buffs banned · dt-correctness, every rate per-sim-year integrated with `dtYears` · no calendar gates · determinism, all randomness via `RngRegistry`, state in `WorldState` · isolation, systems never reference each other · types, stocks `long`, rates `double`. | RATIFIED | `CLAUDE.md:16-22` |
| **F2** | **The shipped weather model, in full.** `rho = exp(−dtYears / CorrelationTimeYears)`; `x_i(t) = rho·x_i(t−1) + Sigma·sqrt(1 − rho²)·e_i`; `mult_i = exp(x_i − Sigma²/2)`, per settlement, in log space. There is no seasonality, no climate state, no spatial field with its own dynamics, and no bound on the draw. | RATIFIED | `Sim.Core/Systems/Harvest/HarvestWeatherSystem.cs:30-34` |
| **F3** | **The `sqrt(1 − rho²)` factor holds the STATIONARY variance at `Sigma²` for every dt**, *"Without it a campaign that shrinks dt across eras would silently change how variable the weather is, which would be the era table altering the climate."* | RATIFIED | `HarvestWeatherSystem.cs:42-45` |
| **F4** | **MEAN EXACTLY ONE**, by the `−Sigma²/2` correction: *"Weather changes the VARIANCE of yield, never its expectation … without this correction merely switching weather on would raise mean yield by exp(Sigma²/2) and quietly re-open a constant the director derived."* | RATIFIED | `HarvestWeatherSystem.cs:57-64` |
| **F5** | **Spatial correlation runs through TRAVEL COST.** `e_i = sqrt(k)·regional_i + sqrt(1−k)·local_i`, regional being an exponential-distance-kernel smoothing over the `SettlementDistances` travel costs. Local draws are taken in table row order BEFORE any smoothing *"so the kernel cannot make the draw sequence depend on distances."* | RATIFIED | `HarvestWeatherSystem.cs:47-55`, `:66-71` |
| **F6** | **The multiplier is applied at exactly one site, to realised OUTPUT, after the Leontief `min()`:** `ratePerYear *= foodMultiplier;`. Applying it to the land side is forbidden — it would make it a statement about how much land the settlement has. | RATIFIED | `Sim.Core/Systems/Production/ProductionSystem.cs:249-253` |
| **F7** | **Terrain rasters are IMMUTABLE after worldgen**, excluded from the per-turn `Clone()`, content hash folded into every `WorldHash`. The reserved upgrade path *"would move the mutated layers into cloned, canonically-serialized state — a director-approved ADR at that milestone, reversing this exclusion only for the layers that gain writers."* | RATIFIED | `docs/adr/adr-008-static-terrain.md:8-15`, `:37-40`; `Sim.Core/Worldgen/TerrainSet.cs:5-13` |
| **F8** | **The shipped layers are `_elevation`, `_water`, `_temperature`, `_moisture`, `_fertility`, `_movementCost`, `_rivers`, `_riverPolylines`.** There is **no soil layer, no vegetation layer, no pollution layer, no land-use layer.** `_riverPolylines` are head→mouth cell indices, discharge-ranked. | RATIFIED | `TerrainSet.cs:20-27` |
| **F9** | **Moisture is a distance-to-water proxy, not a water balance:** `moisture[i] = 1.0 / (1.0 + waterDistance[i] / MoistureDecayPx)`. Fertility is `tempSuit × moisture`, river-boosted, clamped to [0,1], exactly 0 on water. Neither has a writer. | RATIFIED | `Sim.Core/Worldgen/Worldgen.cs:115`, `:122-129` |
| **F10** | **The era table:** dt 10 (Neolithic) → 5 → 3 → 2 → 1 → 0.5 (Modern, Information+). | RATIFIED | `Sim.Data/content/era-pacing.json:3-9` |
| **F11** | **dt-invariance BY CONSTRUCTION has a shipped precedent.** Demographics integrates *"the closed form of the constant-rate ODE composed over FIXED half-year micro-steps … every era dt (10, 5, 3, 2, 1, 0.5) is an exact multiple of the micro-step, so every dt executes the SAME kernel the same number of times per sim-year — the growth rate cannot depend on dt except through turn-boundary integer flooring."* | RATIFIED | `Sim.Core/Systems/Demographics/DemographicsSystem.cs:24-30` |
| **F12** | **The disaster is an explicit famine-class production shock in weather's class**, with an exactly-integrated per-year hazard `p = 1 − exp(−λ dt)`, a severity, and a `RemainingYears` that *"persists BY DIMENSION (an event longer than a late-era turn), not by observation of a regime"*. It reads no population, no stores, no deficits and no weather, and **it does not decide famine — `FoodState` does.** | RATIFIED | `Sim.Core/Systems/Disaster/DisasterSystem.cs:33-45`, `:50-58` |
| **F13** | **FAMINE requires `d > 0` AND an exceptional cause**; weather alone reaches at most SEVERE FOOD STRESS. *"neither the disaster row nor the abandonment row IS a famine; both are causes that the balance must confirm."* | RATIFIED | `docs/adr/adr-024-food-state-effective-deficit-disaster-shock.md:18-25`, `:40-41`, `:61-78` |
| **F14** | **The separation of bad weather from disaster is BY CAUSE at the decade scale and BY MAGNITUDE at the year scale**, and the decade-scale bands overlap under F1-unfixed weather. | RATIFIED | `adr-024:190-194` |
| **F15** | **Standing constraint in force: weather UNCLAMPED AND UNMODIFIED pending G1**; `sigmaLogYield` and `correlationTimeYears` are on the no-move list. | RATIFIED | `docs/adr/cr-015-famine-is-exceptional.md:573-576`, `:581` |
| **F16** | **G1 is escalated and OPEN, with three options, of which (c) is REFUSED.** (a) leave; (b) apply `g(T/τ) = 2(τ/T)[1 − (τ/T)(1 − e^{−T/τ})]` as `multiplier = exp(√g·x − g·σ²/2)` leaving *"σ, τ, the AR(1) state, the spatial blend and the mean-one property untouched"*; (c) apply `g` and re-derive σ — refused because *"σ is yearly and correct; re-deriving it is tuning."* Interim disposition: (a), weather ships byte-identical. | RATIFIED (as an open escalation) | `cr-015:3`, `:524-547`, `:557-569`; `docs/milestones.md:318-319` |
| **F17** | **`sigmaLogYield = 0.2936` is DERIVED and is a YEARLY figure** — `sqrt(ln(1 + CV²))` at CV = 0.30, reference class rain-fed cereal agriculture without irrigation or modern inputs. `correlationTimeYears = 3.0`, `spatialSharedFraction = 0.6` and `spatialRangeCostUnits = 40.0` are CHOSEN, never derived. | RATIFIED | `Sim.Core/Systems/SimConfig.cs:369-405`; `Sim.Data/content/sim.json:222-228` |
| **F18** | **The environment & climate system's Spine home is M9, and the only recorded statement of what it is FOR is** *"degradation stocks close loops"*. Global-equilibrium solving is permanently anti-scoped. | RATIFIED | `docs/civ-sim-architecture-v3-outline.md:89`, `:98` |
| **F19** | **The D-035-C carrier test.** Seven legal coupling paths; *"Name the physical carrier — a good, a purse, a building, a policy, a body, a season. If none exists, it is an invented modifier and is refused."* Path 4 is **COMMON CAUSE** — two consumers read the same upstream world variable, *"Correlation with no link between the needs"*, carrier: **a world variable**. | RATIFIED | `docs/d035-needs-aggregation.md:79-96` |
| **F20** | **D-021's paired-feedback rule.** *"Every positive feedback loop in the design must ship with at least one negative feedback loop that strengthens with amplitude."* The valves are **Exit, Voice, Endurance**; Exit is out-migration along the real network, and *"the sim always keeps at least one channel open."* Valves 1,2,3,6,7 land at M5; 4,5,8 at M8. | RATIFIED | `docs/d021-stability-doctrine.md:8`, `:24-33`, `:45-48` |
| **F21** | **`SettlementHappiness` is a DERIVED READING, never a stock**, not serialized, and it **deliberately does not read the needs or grievance rows** (D-021: grievance drives no BEHAVIOUR until M5; enforced by `scripts/check-read-isolation.sh`). It is computed from **PRIMARY signals using the SAME formulas**, aggregated **non-compensatorily** by `NeedsAggregation.Aggregate` (D-035-B CES, sigma < 1), over an explicit `Factor` enum whose header states *"Adding a factor later is one entry in Factors and its weight — the extension seam is the array."* WATER is *"absent rather than stubbed at 1.0, which would silently claim every settlement is well watered."* | RATIFIED | `Sim.Core/State/SettlementHappiness.cs:7-9`, `:28-37`, `:41-49`, `:51-57`, `:77-87`; `scripts/check-read-isolation.sh:1-45` |
| **F22** | **The observability taxonomy is FIVE kinds plus GAP** — READ · SUMMED · DIFFERENCED · RESIDUAL · RECOMPUTED — and *"The logger observes. The simulation calculates. The UI reads."* A quantity needing a formula the simulation does not expose is a **GAP**, never an observer-side copy. | RATIFIED | `docs/observability-architecture.md:16-31` |
| **F23** | **The capability seam ships and is the D-020 predicate DSL over published variables**, with two live consumers — class emergence and recipe availability. `goods.json`'s `requires` is *"an optional D-020 availability predicate ('requires') — a knowledge gate over published variables, never a calendar date (law 4)"*, shipping as `"requires": "artisan_share > 0.05"`. The published registry today is `food_surplus_ratio`, `artisan_share`, `population`, `trade_volume`. | RATIFIED | `Sim.Data/content/goods.json:3`, `:156`, `:175`; `Sim.Core/State/Variables.cs:36-37`, `:60`, `:90` |
| **F24** | **A universal `CapabilitySystem` God object is REJECTED**, and the recorded reason is **law 6, isolation**: it is rejected as an OWNERSHIP shape, not as an idea. The seam is conformant *"only as a shared predicate grammar consumed independently by each domain system"*, *"never as a system that answers for everyone."* | RATIFIED | `docs/d042-empire-and-player-control-addendum.md:141-142`, `:200`; `docs/capability-architecture-decision.md:165-175` |
| **F25** | **`docs/capability-architecture-decision.md` carries a CORRECTION NOTICE falsifying two of its own claims, and its §4 recommendation is UNVERIFIED** — the adversarial pass returned SURVIVES_WITH_CONDITIONS, not clean survival. | RATIFIED (as a verification status) | `docs/capability-architecture-decision.md:7-27`, `:29-38` |
| **F26** | **The five sectors are Farming(0), Herding(1), Extraction(2), Crafting(3), Construction(4).** There is no "industry" object; industrial activity in this model is **Extraction and Crafting sector labour and the recipe executions it performs**. | RATIFIED | `Sim.Core/State/WorldState.cs:320-324` |
| **F27** | **The shipped pipeline order is** `catchment · harvestweather · disaster · production · appropriation · consumption · price · trade · housing · construction · classmobility · migration · colonization · revolt · demographics · needsgrievance · pathbuild`. | RATIFIED | `Sim.Data/content/pipeline.json:2-20` |
| **F28** | **MEASURED, and the single most important fact about what weather currently does:** over 32 unrigged worlds, 1000 turns, the worst realised weather multiplier was **0.19** — an 81 % harvest failure — with starvation deaths 0, famine events 0 and max consumption deficit exactly 0.0000. *"Weather severity is not the binding constraint on famine in this world; store depth is."* | RATIFIED (as a recorded director-level error and its measurement) | `docs/adr/cr-003.md:303-336`; recovered at E-66 |
| **F29** | **`grep -rn -i "pollution"` over `docs/`, `Sim.Core/` and `Sim.Data/` returns zero hits outside this mandate's own design documents.** MEASURED again this pass at `7fb84eb`. There is no prior decision on pollution of any kind. | MEASURED (this lane) | — |
| **F30** | **The M1 anti-scope: *"no plate tectonics, no erosion, no climate simulation — upgrade path reserved."*** This is a closed D-022 decision, not an unbuilt backlog item. | RATIFIED | `docs/m1-walking-skeleton-spec.md:10` |

---

## §2 THE THREE-WAY SEPARATION — MECHANICAL, NOT MAGNITUDE

The mandate requires that NORMAL CLIMATE VARIABILITY, EXTREME CLIMATE EVENTS and TRUE
DISASTERS be separated by what they *are*, not by how big they are. The repository already
contains half of this separation and states it in exactly those terms: F14 records that bad
weather and disaster are told apart **by cause** at the decade scale, because at that scale
their magnitudes overlap. F13 is the mechanism — `FoodState` reads a CAUSE row, and weather
never writes one.

**PROPOSED — the separation completed, in three rows.** The distinguishing column is the
third one; magnitude appears nowhere in this table.

| | **NORMAL CLIMATE VARIABILITY** | **EXTREME CLIMATE EPISODE** | **TRUE DISASTER** |
|---|---|---|---|
| what it **is** | a **signal**: the continuous, bounded climate anomaly in force at a place and time | a **reading of that signal**: a named, bounded interval during which the signal sits outside a declared envelope | a **driver**: a discrete physical event with its own state |
| causal power | it is the cause — it multiplies realised output at F6's one site | **NONE.** Delete the episode object and the simulation behaves **identically**, because the episode *is* the climate, not an addition to it | its own. Delete it and the world changes: it can do things the climate cannot |
| state | the driver's own state (§3), one value per climate region per year | **no state at all** — RECOMPUTED from the driver (F22) | a serialized row with severity and `RemainingYears`, persisting BY DIMENSION (F12) |
| duration | none; it is defined at every instant | emergent — the interval during which the predicate holds | a physical duration, derived against a declared buffer |
| carrier (F19) | **a season** | none needed: it is a world variable read twice (D-035-C path 4, COMMON CAUSE) | its own — a flood's water, a fire's fuel, a murrain's herd |
| writes a **CAUSE** the `FoodState` ladder reads (F13)? | **no** | **no** | **yes** |
| worst outcome it can produce alone | SEVERE FOOD STRESS | SEVERE FOOD STRESS | FAMINE |
| randomness | in the stochastic substrate only (§3.6 variant B); none in variant A | none, ever | one hazard draw and one severity draw per settlement per turn (F12) |

**PROPOSED — the consequence, stated as a rule because it is the useful form:**

> **A drought is category 2. A flood is category 3.**
> A drought has no separate carrier: it is the water term of the climate being low for a
> while, and everything it does, the climate signal already does. A flood is a discrete
> physical event that destroys a store, a dwelling or a field; it needs a carrier, a row and
> a duration, and it is therefore a disaster in the shipped `DisasterSystem` sense.

**Why this matters and is not bookkeeping. INFERRED.** It answers F14's overlap without
touching either side of it. Under this separation a category-2 episode and a category-3
disaster may produce *the same decade multiplier* and remain distinguishable, because only
one of them writes a cause row — which is exactly the mechanism `adr-024:190-194` already
relies on, generalised from "weather vs disaster" to "climate vs disaster". It also explains
why the mandate's contrast (`3, 2.5, 3.2, 3.5, 3.1, 2.8, 3.4` and not `1, 6, 2, 6, 1`) is a
statement about category 1 only: the wild sequence is what you get when a single unbounded
draw is asked to carry all three jobs at once, which is what ships today.

**The honest objection, stated because it is real.** Category 2 has no causal power, so a
reader may ask what it is for. Three answers, all PROPOSED: (i) the **chronicle** — "the
seven ill years" is a name a player needs and the sim cannot currently produce; (ii) the
**D-020 predicate DSL** — an institutional or behavioural response may legitimately gate on
*"a dry episode is in force"* as a published variable, which is a knowledge gate over
computed state and not a calendar gate (F23, law 4); (iii) **falsifiability** — an episode
predicate with a declared envelope is a thing a test can count, and E-65's standard (*"a
constant that is selected in zero turns of the whole campaign is unfalsifiable by
construction"*) applies to episodes as much as to constants.

---

## §3 THE CLIMATE SUBSTRATE

### 3.1 What is missing, stated exactly

MEASURED (this lane, at `7fb84eb`): the tree contains a **static** temperature field and a
**static** moisture field, both written once at worldgen and immutable thereafter (F7, F9),
and a **stochastic per-settlement output multiplier** with no memory beyond one AR(1) state
(F2). RATIFIED absence: *"rainfall"* as a modelled quantity exists only in the retired M0 toy
`WeatherSystem`, which is not in the shipped pipeline (F27; recovered at E-24). There is no
object in the simulation whose value at year *t* differs from its value at year *t*+20 for
any reason other than a fresh random draw.

**PROPOSED — the missing object, named once.** The **CLIMATE DRIVER** is a bounded,
reproducible, dimensionless anomaly field `c(region, t)` defined **per sim-year**, with
long-run mean exactly zero, whose value is a property of the *world* and not of any
settlement's history. Everything else in Parts 9 and 10 is a consumer of it.

### 3.2 The ENSO analogue, investigated as the mandate asks — and what survives the atomic turn

ENSO is, conceptually, a small number of persistent regional states (warm / neutral / cool)
with multi-year residence times and teleconnections that shift rainfall in regions far from
the state's own. The mandate asks that it be investigated as an analogue, not copied.

**MEASURED, and it is the finding that decides the design: a literal ENSO does not survive
the era table.** Real ENSO events have residence times of roughly 9–24 months. The coarsest
shipped dt is **10 sim-years** (F10). A state whose residence time is shorter than the turn
is, at that turn length, indistinguishable from a fresh draw — which is precisely the defect
already recorded against the shipped `correlationTimeYears = 3.0`: at dt 10, `rho = 0.036`
and the AR(1) memory is inert, so *"the drought persistence the ruling required is invisible
at that era's resolution"* (E-38, from `SimConfig.cs:388-394`; re-read this pass).

**PROPOSED — the dimensional requirement this imposes, stated as a constraint on any design:**

> **For a persistent climate state to be OBSERVABLE at a given dt, its residence time must
> exceed that dt.** For the structure to be visible in the Neolithic band it must exceed
> **10 sim-years**, which means the conceptual analogue is not ENSO but the **multi-decadal**
> family — the PDO/AMO-scale regime, the Bond-event scale, the Little-Ice-Age scale.
> A 2-year state and a 40-year state are not the same design at different tunings; at dt 10
> the first is noise and the second is history.

**INFERRED:** this is the reason the mandate's two sequences look the way they do. `1, 6, 2,
6, 1` is a memoryless draw sampled coarsely. `3, 2.5, 3.2, 3.5, 3.1, 2.8, 3.4` is a bounded
signal whose correlation time is *longer than the sampling interval*. The difference is not
amplitude — it is the ratio of residence time to dt.

**What is deliberately NOT imported from ENSO.** No atmosphere, no ocean, no coupled
dynamics, no advection, no simulated physics of any kind. F30 anti-scopes climate simulation
at M1 worldgen fidelity as a closed D-022 decision with a reserved upgrade path; F18
anti-scopes global-equilibrium solving permanently (recorded at E-118 as being about markets,
so its extension to a whole-world climate solve is INFERRED, not ruled). **PROPOSED:** what
is imported is exactly three properties — *a small number of persistent states*, *multi-year
residence*, and *one state shifting several regions at once* — and nothing else.

### 3.3 Regional structure, and the unphysical coupling this would remove

**PROPOSED — a CLIMATE REGION is a geographic partition of the world raster, computed once at
worldgen from the immutable layers** (latitude band × elevation class × distance-to-water
class are already present in `_temperature`, `_elevation`, `_water`, `_moisture` — F8), with
each settlement assigned to exactly one region by its site. It needs **no new raster layer and
no ADR-008 reversal**: it is a partition *of* layers that already exist and are already
hash-folded (F7).

**This would replace a coupling that is physically wrong and is already recorded as
unremarked.** F5: the shipped spatial correlation runs over `SettlementDistances` **travel
costs**, so — as the recovery lane records at E-32 and E-40 — *"roads and rivers therefore
change who shares weather"* and `spatialRangeCostUnits` *"is not a physical length: improving
roads shrinks the weather field's effective footprint in map terms."* Weather does not follow
roads. **PROPOSED: climate correlation is geographic; it must not read the travel-cost
lattice.** This is a change to shipped, ratified T3.4b behaviour and is surfaced as a conflict
in §14 (**X4**), not smuggled in as an improvement.

**Teleconnection, and why it needs no new coupling path.** A teleconnection is one climate
state shifting the anomaly in several regions at once with different signs. **PROPOSED: this
is D-035-C path 4, COMMON CAUSE — *"two consumers read the same upstream world variable …
Correlation with no link between them"*, carrier: a world variable (F19).** If the driver is
**one** field sampled with a per-region gain and phase, rather than N independent per-region
processes with coupling coefficients between them, then a teleconnection is not an edge at
all: it is the same variable read in two places. **The design must therefore be built as one
driver with regional gains, not as N drivers with a coupling matrix** — the second shape
would need an eighth coupling path and would fail the carrier test.

### 3.4 The atomic turn: what other systems see

**PROPOSED — the REALIZED TURN ANOMALY.** The climate driver is defined per sim-year. What
`ProductionSystem` consumes at F6's one site is the **turn mean** of the per-year multiplier
over the turn's own years:

```
m(s)        the per-sim-year food multiplier at settlement s, derived from the driver (§6)
M(s, t, dt) = (1/dt) · ∫[t, t+dt) m(s, u) du         the REALIZED TURN ANOMALY
```

This is not a new idea in this repository; it is F11's shape applied to climate. Demographics
already integrates a per-year process over fixed half-year micro-steps and calls the property
by its name — *"dt-invariance BY CONSTRUCTION"*. Two implementation routes exist and the
choice belongs with §3.6's choice of substrate:

- **analytic** — if `m` is closed-form (variant A), the integral is closed-form and composes
  **exactly**: `∫[a,c] = ∫[a,b] + ∫[b,c]` in exact arithmetic up to floating-point rounding.
  This is the strongest available form of the dt property and it has a shipped test idiom to
  copy: `DisasterSystemTests.D_DtExact_FiveYearFailure` pins that a five-year failure costs
  the same food at dt 10 and at dt 0.5 (F12).
- **micro-stepped** — if `m` is stochastic (variant B), the integral is a fixed-micro-step sum.
  **Note the dimensional trap, MEASURED against F10:** a one-year micro-step does **not**
  divide the Modern band's dt = 0.5. The micro-step must therefore be **0.5 sim-years**, the
  same as Demographics' (F11), or the design inherits exactly the flooring artefact F11's
  comment warns about.

### 3.5 Bounded by construction, and mean-one by construction

Two properties the mandate requires and F4 makes non-negotiable.

**BOUNDED.** **PROPOSED:** the driver is a sum of a fixed, small number of bounded components,
so `|c| ≤ Σ|a_k|` identically — a bound that is a *property of the construction*, not a clamp
applied afterwards. This matters for a reason beyond taste: a clamp bolted onto an unbounded
process is a free-floating modifier and would run at law 2; a sum of bounded terms is a
mechanism whose range is a theorem.

**MEAN EXACTLY ONE (F4).** **PROPOSED, and this is the sharpest constraint in §3:** the
multiplier's long-run mean must be exactly 1 or the derived 26.0 reopens. Constructively:

- **variant A** — each component is a zero-mean bounded oscillation, so the driver's
  *asymptotic* time-average is exactly 0 and the multiplier's is exactly 1 by construction.
  **Honest limitation, stated rather than hidden:** over a *finite* 6,000-year campaign the
  realized mean is `1 + O(Σ|a_k|·P_k / T)` for component periods `P_k`, not exactly 1. This is
  the same standard the shipped model meets — `exp(x − σ²/2)` has expectation exactly 1 and a
  realized 6,000-year mean that does not — so it is not a regression, but it is not exact
  either, and a test must assert the *ensemble* property, not a realized one.
- **variant B** — the mode means must satisfy `Σ_m π_m · μ_m = 1` under the chain's stationary
  distribution `π`, which can be made **exact in data** by defining one mode's mean as the
  residual that closes the sum. This is a testable, exact, populated-table property.

Either way: **the mean-one property is data-checkable at load time**, which is strictly better
than the shipped model, where it is a consequence of a correction term whose realized effect
was found to be wrong by 1.3–4.3 % and needed T3.4c to fix (E-33).

### 3.6 The two candidate substrates, and the choice this lane does not make

**PROPOSED — VARIANT A: CLOSED-FORM QUASI-PERIODIC DRIVER.**
`c(region, t) = Σ_k a_k(region) · f(2π·t / P_k + φ_k(region))` over a small fixed bank of
components with incommensurable periods `P_k` spanning the multi-decadal to multi-centennial
range. Amplitudes and phases are fields assigned at worldgen from the world seed, giving
regional difference and teleconnection (§3.3) for free. The state is **nothing**: `c` is a
pure function of elapsed sim-years and immutable worldgen data, so it is a **RECOMPUTED**
quantity in the F22 sense and is **not serialized at all**.

| property the mandate asks for | how variant A supplies it |
| --- | --- |
| deterministic sequences | a pure function of (elapsed years, worldgen fields) — no draw, no state, no order dependence |
| bounded variability | `\|c\| ≤ Σ\|a_k\|` identically, a theorem not a clamp |
| multi-year structure | the component periods |
| regional differences | per-region amplitude and phase fields |
| persistent states | an EXTREME CLIMATE EPISODE (§2) is an interval of the driver, emergent from beating between components |
| realistic oscillations | incommensurable periods beat, so the sequence never repeats and never looks periodic |
| reproducibility | trivially — no RNG stream, no state, nothing to desynchronise |
| compatible with the 10-year atomic turn | the turn integral is analytic and composes **exactly** (§3.4) |

**Its two real costs, stated because they are not small.** (i) **Transcendentals.** E-42 records
libm's entry into the hashed determinism surface through weather's Box–Muller as **OPEN**, and
that *"this item does not get cheaper with time; it gets more expensive"*; a component bank
adds more. ADR-022's reference platform mitigates and does not close it. (ii) **Predictability.**
A climate that is a closed-form function of time is, in principle, forecastable by anything that
can read the function — which is a *design* question about what the player and the AI may know,
not a correctness question, and it belongs to the Director.

**PROPOSED — VARIANT B: MARKOV REGIME CHAIN.** A small number of discrete climate modes per
region, with transition hazards integrated exactly as `p = 1 − exp(−dt/τ_mode)` — the shipped
`DisasterSystem` idiom (F12) — and a bounded within-mode anomaly. Residence times are per-year
rates (law 3); modes and residence counters are serialized rows. Closer to the ENSO analogue as
stated, honest about randomness, and it inherits every shipped discipline for RNG keying and
draw order (F2, F5, F12). Its costs: it **adds serialized state** and therefore a schema
version and every golden; its turn integral is a micro-step sum rather than an identity; and
the mode-residence/dt relationship of §3.2 becomes a live tuning hazard rather than a
construction property.

**This lane does not choose between A and B.** Both satisfy the mandate's seven requirements;
they differ in what they cost and in what they concede. **DIRECTOR DECISION REQUIRED (Q1).**

### 3.7 The one thing both variants must avoid — and it is a law-4 question

**INFERRED, and surfaced because it is the sharpest trap in Part 9.** A closed-form climate
keyed to the **absolute calendar year** is a *scripted* climate: every campaign would enter its
Little Ice Age in the same century. That is the environmental form of exactly what law 4 bans
in the capability domain — *"capability derives from computed state, never from dates or era
labels"* (F1) — even though law 4's text is about capability and does not reach climate on its
face.

**PROPOSED — the escape, and it costs nothing:** the driver reads **elapsed sim-years since the
campaign epoch**, and its per-region phases `φ_k` are assigned **from the world seed at
worldgen**. Reproducibility is preserved (same seed → same climate, forever, live and on
replay — F1 law 5's standard); scripting is impossible, because no two seeds share a climate
history and nothing is keyed to a named era.

**Conflict flagged:** "elapsed sim-years since the campaign epoch" leans on a quantity whose
definition is itself under an open CR — `docs/adr/cr-006-continuous-time-and-campaign-epoch.md`
§2 is a conflict about the campaign epoch and is **OPEN** (`cr-006:111`). Surfaced at §14 **X6**.

---

## §4 dt AND THE T3.4b/T3.4c INVARIANT — THE ANSWER AT EVERY dt IN THE ERA TABLE

The mandate requires this design to say how it satisfies, at every dt in the era table, the
ratified concern that the weather STATE holds stationary variance because otherwise *"the era
table would alter the climate"* (F3) — or to mark it as a conflict. **It is both**, and the two
halves must be stated separately because they have different answers.

**The half that is satisfied, and satisfied more strongly than today.** The CLIMATE — the
per-sim-year process — is defined **once, per sim-year, with no reference to dt whatsoever**.
In variant A it is a function of elapsed years; in variant B it is a chain whose transition
hazards are exactly-integrated per-year rates (law 3, F12's idiom). At dt 10 and at dt 0.5 the
underlying annual sequence is **identical**, not merely identically distributed. The era table
therefore cannot alter the climate in the sense F3's comment names — it cannot alter it at all,
because the climate does not know what dt is. In variant A this is an *identity*; the shipped
`sqrt(1 − rho²)` achieves the same end for a stationary variance by a correction factor, and a
correction factor is a thing that can be deleted by a mutant (E-43 records that it currently
**has no semantic test at two different dt** and is **NOT cleared**). A construction cannot be.

**The half that is a conflict.** What `ProductionSystem` consumes is not the climate; it is the
**REALIZED TURN ANOMALY** (§3.4), and that quantity **does** vary with dt — a decade's mean
harvest multiplier is genuinely less variable than a single year's, by the aggregation law, and
under this design it is so by construction. The shipped design does the opposite: the published
turn multiplier carries the **annual** variance at every dt, which is dt-invariant and is
exactly what G1 says is wrong by a factor of `1/√g = 1.5` in σ at dt 10 (F16).

> **So: any deterministic climate design that integrates an annual process over the turn
> necessarily produces the law-3 answer to C-02, and a design that preserves the published
> multiplier's dt-invariance cannot integrate an annual process. The two cannot both be had.**
> C-02 records this as *"Two frozen items and a law disagree"* (`cr-015:549-555`), and S8 §3
> *"does not let a packet resolve it by choosing."* **This design does not choose; it
> localizes the choice.** The turn-aggregation rule of §3.4 is the single named place where
> G1's answer lives, and whichever way the Director rules G1, the ruling is a one-line change
> at that place and nowhere else. **DIRECTOR DECISION REQUIRED (Q2).**

**A property worth pinning either way, PROPOSED as a test obligation rather than a test:** the
dt-invariance claim must be measured at **two different dt**, not asserted by a comment. E-43 is
the standing example of what happens otherwise — a factor whose *entire stated purpose* has no
semantic test, with an owner (T3.4d) that MEASURED-ly does not exist as a packet anywhere in the
tree. Any climate design inherits that obligation, and it inherits it doubled, because it has an
annual invariant *and* a turn-aggregation rule to pin.

---

## §5 G1 — PRECISELY HOW A BOUNDED DETERMINISTIC CLIMATE RELATES TO THE SIGMA QUESTION

The mandate asks for precision here, so this section is narrow and makes no recommendation.

**What G1 is, restated from source (F16).** The shipped turn multiplier is `exp(x − σ²/2)`
where `x` is the **stationary annual** AR(1) state sampled once per turn. A turn's harvest is
physically the turn-mean of its years' multipliers, whose variance is `σ²·g(T/τ)` = 0.427·σ² at
dt 10. So the shipped decade multiplier is **1.5× too variable in σ**, and `P(decade multiplier
≤ 0.57)` runs **3.9 % as shipped versus 0.25 % corrected** (`cr-015:524-547`).

**The four paths, and where the fourth sits.** Options (a) leave, (b) apply `g`, (c) apply `g`
and re-derive σ — all three operate on the same stochastic, unbounded, single-scale process
(`cr-015:557-569`). The **fourth path** — the relayed direction recorded at E-119 and at
`docs/design/m4-closure-audit.md:679-694` as **DIRECTOR-STATED, relayed, UNVERIFIED as a
repository document** — *replaces* that process. It is **not on §6.4's list** and a sibling lane
already records that it *"would need to be put there — or put in its own CR — before anyone could
implement it."*

**How this design relates to the sigma question, in four precise statements.**

1. **The defect G1 names cannot arise under §3.4. INFERRED.** `g` is the ratio between the
   variance of a turn-mean and the variance of an annual draw. Under §3.4 the published value
   **is** the turn-mean, computed as such. There is no mis-scaled quantity left for option (b) to
   correct, because the correction and the value are the same object. This is the sense in which
   a bounded deterministic climate could make the sigma question **moot** — and that is an
   INFERRED property of the shape, **not a ruling**.
2. **"Moot" is not "ruled", and the difference is three separate Director acts.** (i) Whether the
   shipped weather is amended at all: F15's *"weather unclamped and unmodified (pending G1)"* is
   a constraint **in force**, and a bounded climate is a clamped signal by construction, so the
   design is presently **forbidden by a ruling in force** (§14 **X1**). (ii) What becomes of the
   goldens: they move under (b) and they move under the fourth path, differently. (iii) Whether
   G1 is *answered* or merely *bypassed* — a bypassed defect that is never ruled remains in the
   record, and `cr-015:3` reads **"Status: RULED … G1 open."**
3. **σ = 0.2936 survives this design unchanged, and option (c)'s refusal is the reason.**
   RATIFIED (F17): σ is `sqrt(ln(1 + CV²))` at a **yearly** CV of 0.30, and (c) was refused
   because *"σ is yearly and correct; re-deriving it is tuning."* A design whose driver is
   defined **per sim-year** has a natural home for exactly that number: σ is the **annual
   amplitude**, unchanged, and the turn value is its honest aggregate. **INFERRED: the fourth
   path is therefore the option most consistent with (c)'s refusal, not least** — it keeps σ
   yearly and stops asking a yearly number to describe a decade. This is an argument, offered
   for the Director to accept or reject; it is not a recommendation.
4. **Bounding is a SEPARATE question from variance, and it has its own blast radius.** Bounding
   removes the tail. MEASURED (F28): the shipped model's worst realised multiplier over 32 worlds
   was **0.19**. ADR-024 derived the disaster/weather separation at the year scale from precisely
   that tail — a 0.25 yearly multiplier sits at `z = −4.57`, *"once per ≈ 400,000 settlement-years;
   'not every extreme draw is a disaster' holds by construction at the YEAR scale"* (`adr-024:190-194`).
   Under a bounded climate that argument is **replaced by a stronger one** — weather cannot reach
   the disaster band *at all*, at any scale, because its range is a theorem (§3.5) — which
   strengthens ADR-024's separation and changes what E-103's `s_min` derivation is measured
   against. It also changes what CR-016 is arguing about, since CR-016's three broken gates are
   all downstream of how often a settlement meets a decade it cannot absorb (`cr-016:24-34`).
   **DIRECTOR DECISION REQUIRED (Q3).**

---

## §6 THE CAUSAL CHAIN — CLIMATE → RAINFALL → WATER → SOIL → AGRICULTURE → FOOD

Edge by edge, each with its carrier (F19), the state it needs, and what blocks it. Every row
is PROPOSED unless labelled otherwise.

### 6.1 The chain as a coupling map

| # | edge | mechanism | carrier (F19) | new state | blocked? |
|---|---|---|---|---|---|
| **A1** | CLIMATE DRIVER → **rainfall anomaly** | the driver's water component, read at the settlement's climate region | **a season** | none (RECOMPUTED) | no |
| **A2** | rainfall anomaly × static `_moisture` → **effective moisture** | the settlement's hinterland-mean `_moisture` (already read at founding, `WorldFounding.cs:325-352`) scaled by the anomaly | **a season**, acting on **the land** | none | no |
| **A3** | effective moisture → **soil-water availability** | a per-settlement rate-like `double` in [0,1], the root-zone balance | **water in the root zone** | one `double` per settlement | no — but see §6.2 |
| **A4** | soil-water availability → **the FALLOW BUDGET** (§6.5) | water stress lengthens the rest a field needs | **the fallow field, a season** | the fallow budget as state | **yes — §6.4** |
| **A5** | FALLOW BUDGET → **sown share** → **yield per fertility-weighted km²** | the sown share is a *term inside ADR-013's derivation of 26.0*, today baked in as arithmetic | **the field, the rotation** | — | **yes — §6.4** |
| **A6** | yield → **the land side of the Leontief `min()`** | `arableKm2 × yield` | **the land** | — | **yes — §6.4** |
| **A7** | REALIZED TURN ANOMALY → **realised food output** | the shipped site, unchanged in shape: `ratePerYear *= foodMultiplier` (F6) | **a season** | none | no |
| **A8** | climate water term → **river discharge proxy** | discharge scales the assimilative capacity of §7.3 and the dilution of water loadings | **the river** | one `double` per river reach | no |

### 6.2 What A1–A3 may and may not claim

The shipped `_moisture` field is a **distance-to-water proxy**, not a water balance: no
precipitation, no evapotranspiration, no runoff, no storage (F9; E-17). **PROPOSED, stated as a
limit rather than a feature:** A1–A3 add a *time-varying anomaly on top of a static suitability
proxy*. They do **not** make the world hydrologically modelled, and calling the result
"rainfall" would be reusing a name the retired toy already spent (E-24). The honest name is what
it is: a **water term of the climate driver**, multiplying an existing static field.

**PROPOSED — the denomination discipline this inherits.** ADR-013(a) made
`Sim.Core/Pathing/LatticeGeometry.cs` *"the only place where lattice units and physical units
meet"*, with names carrying units and a grep gate from which *"tests are not exempt"* (E-50,
E-51). Every quantity in this chain therefore ships with its unit in its name — a bare `moisture`
or `water` would be a review finding — and the three-layer enforcement proven RED on both fault
shapes is *"the standard for any structural gate"* (E-51).

### 6.3 Where water is NOT allowed to go

RATIFIED (E-88, E-89, and re-read this pass at `SettlementHappiness.cs:41-49`): the D-018 needs
ladder is **frozen at eight needs** and water is not among them; water as a human need *"is not
modelled anywhere"*, and `SettlementHappiness` records it as *"absent rather than stubbed at 1.0,
which would silently claim every settlement is well watered."* **PROPOSED: this chain adds
geographic water only (T-11 sense (a)) and does not add water as a need.** Adding water to the
ladder reopens a frozen D-decision and is not this lane's to propose. §8 states the one door that
is open.

### 6.4 THE WALL — and it is the central agronomic conflict of Parts 9 and 10

Edges A4, A5 and A6 all end at the same place: **something must change how good the land is.**
Two ratified items stand in the way, and they point in opposite directions.

- **F7/ADR-008.** `_fertility` is immutable, uncloned and hash-folded. A soil model that writes
  it needs the reserved upgrade path — *"a director-approved ADR at that milestone, reversing this
  exclusion only for the layers that gain writers."* Lane E records the same wall at its F21/E11
  and marks environmental management **BLOCKED**, noting *"The carrier is nameable; the substrate
  refuses it."*
- **F6 and the ruled meaning of the multiplier.** The CR-003 ruling fixes that the weather
  multiplier *"multiplies OUTPUT and never alters the derived 26.0, which stays a statement about
  land quality under normal conditions"* (E-26), and applying a multiplier to the land side is
  forbidden precisely because it would be a statement about land (F6).

**The conflict, stated without resolving it.** Soil degradation **is** a statement about land
quality. It is therefore the one environmental mechanism that F6's rule was never written for:
the rule separates *land quality (a constant)* from *weather (a coefficient on output)*, and soil
is neither — it is a **variable of the land**. Three shapes exist and each breaks something:

| shape | what it does | what it breaks |
|---|---|---|
| **(i) multiply the land side** by a soil-condition factor | the physically honest reading | reopens the ruled meaning of the derived 26.0 per settlement, which is the exact thing `cr-003:280-283` and F6 forbid for weather |
| **(ii) multiply output only** | legal today, one-line, no schema change | **mis-states what soil is.** A settlement that has exhausted its soil is not having a bad year; it is poorer land now. Output-only soil cannot make land permanently poorer, which is the whole phenomenon |
| **(iii) make the FALLOW BUDGET state** (§6.5) | soil acts by changing *how much of the land is sown this year*, not by changing what a sown hectare yields | requires opening ADR-013's derivation internals into state — a director act, because those shares are currently **arithmetic inside a derived constant**, not data |

**PROPOSED: (iii) is the shape that is a mechanism rather than a modifier**, and it is the only
one of the three whose historical progression (§9) writes itself. It is **not** proposed as the
answer; the choice among (i), (ii) and (iii) is **DIRECTOR DECISION REQUIRED (Q4)** because it
turns on the meaning of a derived constant the director ruled *"not provisional and not
negotiable"* (E-54).

### 6.5 The FALLOW BUDGET — PROPOSED, defined once

ADR-013's derivation of 26.0 contains a complete land budget inside each ideal km²: cropped share
at saturation **k = 0.28**, deducting channel/floodplain 12 ha, slope and terrace break 15,
pasture for the traction and manure herd 25, woodland 15, settlement 5 per 100 ha; medium fallow,
crop **2 years in 7 = 0.2857**, giving 8.00 sown ha per f = 1.0 km² (RATIFIED, re-read this pass
at `Sim.Data/content/sim.json:4`). **None of it is state** — gap 8 records exactly this: *"it is
arithmetic inside a constant. No decision records whether that budget should ever become a
modelled quantity."*

> **PROPOSED — the FALLOW BUDGET is that arithmetic promoted to a per-settlement modelled
> quantity: the share of the hinterland sown this year, and the rest the rotation requires.**
> Soil condition then acts by **lengthening or shortening the rest a field needs**, which is a
> mechanism with a carrier — *the fallow field, a season* — and which reduces to the shipped
> constant exactly when the budget sits at its derived values. **The identity arm matters and is
> the F6 idiom: a world whose fallow budget is at ADR-013's values must farm EXACTLY as it does
> today**, the same way *"a world with no weather system in its pipeline … farms exactly as it did
> before T3.4b"* (E-36). Without that arm the mechanism has no attribution control.

**INFERRED, and it is why this shape is worth the Director's attention:** it is the only one of
the three that makes *every* item in the mandate's historical progression a change to the same
causal relationship rather than a new coefficient — see §9.

### 6.6 What this chain does NOT reach, stated rather than stubbed

- **Livestock and fish.** RATIFIED (E-81): the three food goods reach the land by two different
  routes — grain through `EffectiveArableKm2`, livestock and fish through worldgen **deposit
  channels** keyed on `moisture` and `water`. A soil or fallow model touches **grain only**, and
  leaves two thirds of the food roster untouched. The pure pastoralist is already recorded as
  perpetually grain-short and *"owes nothing to the weather"* (E-107, ruled NOT a STOP).
- **Store depth.** F28 is unambiguous: *"Weather severity is not the binding constraint on famine
  in this world; store depth is."* **INFERRED: a richer climate does not by itself make anything
  hungrier**, and any claim that it would is a claim about stores, which belongs to B-2's family
  and not to this lane.
- **Anything sub-4 km.** D-015 fixes 4 km/px as the finest environmental resolution that exists
  (E-10). A river channel, a field, a terrace is not representable as a raster cell.

---

## §7 PART 11 — POLLUTION AND ENVIRONMENTAL QUALITY

**MEASURED (F29): the repository records no decision of any kind on pollution.** The Spine's one
sentence — *"degradation stocks close loops"* (F18) — is the entire prior art. Everything in §7
is PROPOSED, and gap 14 and gap 15 are restated as Director questions rather than answered.

The Director's ban is explicit: this must not reduce to *"industry = −5 Happiness"*. The
architecture below has no such edge anywhere in it; the only path from industry to how a
settlement feels runs through **four physical steps and a non-compensatory aggregation** (§8).

### 7.1 There is no "industry" object, and none is needed

RATIFIED (F26): the five sectors are Farming, Herding, Extraction, Crafting, Construction.
**PROPOSED: industrial activity is not a new object — it is recipe execution in the Extraction
and Crafting sectors**, which already ships, already has per-execution dimensional semantics
(*"inputs and laborPerOutput are PER EXECUTION of the recipe, and one execution yields output.qty
units"*, `goods.json:3`), and already has an availability predicate seam (F23). This matters: it
means the pollution source term is a **coefficient inside the production resolution equation**
(law 2 legal) attached to a thing that already exists, not a new sector, a new class, or a new
system that owns industry.

### 7.2 Sources — a LOADING, and the question of what kind of quantity it is

**PROPOSED — a LOADING is the quantity of degradation delivered to one environmental medium per
sim-year.** Three sources, three carriers:

| source | mechanism | carrier (F19) | notes |
|---|---|---|---|
| **recipe execution** | a per-execution emission coefficient per medium, data in `goods.json` beside `inputs` and `laborPerOutput` | **a good being made** | dimensional declaration required: per execution, not per output unit — the exact fault `goods.json:3` records T3.2 making and T3.3 correcting |
| **settlement density** | organic loading scaling with people per dwelling and with population | **a body** (D-035-C path 3) | the substrate exists: `HousingRow.Dwellings`, the buckets |
| **agricultural intensity** | nutrient runoff scaling with applied fertilizer | **the fertilizer good, applied to the land** | zero until the good exists; see §9 |

**THE QUESTION THIS LANE DOES NOT ANSWER (gap 14).** Is a degradation stock a **conserved `long`
moving only through `Ledger.Flow`** — which is what F18's own word "stocks" plus law 1 plus law 7
read together imply, and which would give it the world-level DIFFERENCED instrument for free
(F22) — or a **rate-like `double`**, following the shipped precedent for environmental state
(`WorldState.cs:11-15`: *"a rate-like double, not a conserved stock (law 7)"*)? The two have very
different costs and the choice is visible in every test that follows. **DIRECTOR DECISION
REQUIRED (Q5).** This lane notes only that **decay makes the conserved reading awkward** — a
pollutant that degrades is not conserved, so the `long` reading needs an explicit Ledger *sink*
reason for decay in the manner of Spoilage (14) and GranaryOverflow (15), which is a real and
available shape, not an objection.

### 7.3 Media, transport, and the one carrier that already ships

**PROPOSED — three media, because they behave differently, not because three is tidy:**

| medium | spatial behaviour | transport carrier | sink |
|---|---|---|---|
| **AIR** | local to the settlement; no transport | — (none needed) | decay, a per-sim-year rate integrated with `dtYears` (law 3) |
| **WATER** | **travels downstream** | **the river** — `TerrainSet._riverPolylines` are already *"head→mouth cell indices, discharge-ranked"* (F8), immutable and readable | decay **plus dilution by discharge**, which is the climate chain's A8 |
| **SOIL** | accumulates in the hinterland; does not travel | — | very slow decay, plus removal in the harvest |

**The water medium is the load-bearing one and it is the cleanest carrier in this document.** A
settlement's effluent reaching the next settlement downstream is routed along geometry that
**already exists, is already immutable, and is already folded into `WorldHash`** — so no raster is
mutated, no ADR-008 reversal is needed, and the coupling has a named physical carrier that a
player can point at. It also gives the model something it has never had: **a settlement can harm
its neighbour without meaning to, through a medium, with distance and direction mattering.**

**PROPOSED — ASSIMILATIVE CAPACITY** is the per-settlement, per-medium rate at which loadings are
absorbed without consequence: for water, a function of river discharge (A8) and therefore of the
climate; for soil, of hinterland area; for air, of nothing yet. **This is where climate and
pollution couple, and the coupling is physical rather than thematic: a dry decade concentrates
the same effluent into less water.** Carrier: **the river**.

### 7.4 ENVIRONMENTAL QUALITY is a DERIVED READING, never a stock

**PROPOSED, and modelled directly on the F21 precedent rather than invented:**

> **ENVIRONMENTAL QUALITY** is a per-settlement, per-medium sufficiency in [0,1], **recomputed
> from loadings and assimilative capacity every time it is asked**. It does not accumulate, it
> does not decay, nothing integrates it, and **it is not serialized**. Ask it twice about the same
> world and you get the same number.

The reasoning is F21's, verbatim in shape: *"A stock can be granted: something hands you +5 and
the population is happier with no change in what it eats or where it sleeps. A derived reading
cannot — the only way to move it is to move a condition."* Applied here: **the only way to make a
settlement's environment better is to reduce a loading or raise an assimilative capacity.** There
is no edge in this architecture that can hand a settlement +5 environmental quality, and that is
enforced by the *shape of the type*, not by a rule someone has to remember. It also satisfies the
observability rule that *"nothing new is serialized merely to be observable"*: quality is
**RECOMPUTED** (F22); only the loadings are READ.

### 7.5 Consequences — four edges, four carriers

| # | edge | mechanism | carrier (F19) | status |
|---|---|---|---|---|
| **B1** | soil loading → **agriculture** | enters the **FALLOW BUDGET** (§6.5) — degraded soil needs longer rest; salinisation and nutrient exhaustion are the same lever | **the land, the fallow field** | **blocked by §6.4's wall** |
| **B2** | water quality → **production inputs** | water-using recipes (tanning, brewing, dyeing) require more input per execution as quality falls — a **DEMAND COUPLING** in D-035-C path 3's sense: the physical state changes the quantity required | **a good** | legal today in shape; needs the recipes |
| **B3** | air and water quality → **mortality** | a mortality term the Demographics micro-steps read from a published row — never a sibling call (law 6) | **a body** | **NO SUBSTRATE.** There is no health system; Health & disease is M8 on the Spine. Lane E records the same finding for sanitation: *"no health or contamination quantity for it to bind"* |
| **B4** | environmental quality → **how the settlement is to live in** | a new **Factor** in `SettlementHappiness`, non-compensatory — see §8 | **a world variable** (D-035-C path 4) | legal today in shape; see §8 for the exact constraint |

---

## §8 THE POLLUTION → HAPPINESS EDGE: THE ONE SHAPE THAT IS AVAILABLE

The mandate asks what shape is available given that `SettlementHappiness` is a derived reading
forbidden by an enforced gate from reading needs and grievances. Reading the type and the gate at
source (F21) gives a precise and narrow answer.

**What the gate actually forbids.** `scripts/check-read-isolation.sh` bans sim-side reference to
the `Grievances` and `NeedSatisfactions` tables and their row types outside an allowlist, for the
D-021 reason that grievance drives no BEHAVIOUR until M5. Since happiness feeds migration — a
behaviour — sourcing happiness from those rows would make needs state drive behaviour early. The
type's own header says so and names the consequence: happiness is computed *"from the PRIMARY
signals the needs system itself reads, using the SAME formulas"*, and the duplication is
deliberate.

**PROPOSED — the available shape, in four clauses, each traced to the source:**

1. **A new entry in the `Factor` enum, not a term added to the score.** The header states the
   extension seam explicitly: *"Adding a factor later is one entry in Factors and its weight — the
   extension seam is the array."* The factor is `EnvironmentalQuality`.
2. **Computed from a PRIMARY row — the loadings — and never from a need satisfaction row.** This
   is what keeps the D-021 gate green. The loadings are written by the environment system and read
   by a pure static function, exactly as `FoodSufficiency` reads `ConsumptionDeficits` and
   `HousingSufficiency` reads the housing row.
3. **Non-compensatory by construction.** It enters `NeedsAggregation.Aggregate`, D-035-B's CES
   with sigma < 1, where *"a weighted sum would let a granary full of food buy off having nowhere
   to live"* and sigma = 0.5 makes the factors genuine **complements**. **This is the precise
   technical reason the edge cannot degenerate into "industry = −5 Happiness":** a subtractive
   penalty is compensatory — more food buys it off — and a CES complement is not. A well-fed,
   well-housed settlement drinking foul water is **not** "averagely fine".
4. **It cannot be granted, only earned or lost through a condition**, because both it and
   happiness are derived readings (§7.4, F21). There is no state anywhere on this path that
   anything can add to.

**And it opens the door the header left ajar, without touching the frozen ladder.** F21 records
WATER as absent from happiness *"because there is no water good and no water need"*, and E-89
records that the eight-need ladder is **frozen** so water may not enter as a need. **INFERRED: a
happiness FACTOR is not a need** — the type computes factors from primary signals precisely so it
need not go through the needs system — **so water quality can reach happiness as a factor without
reopening D-018.** That is an inference about what the two documents permit read together, not a
ruling, and it produces an asymmetry worth the Director's eye: happiness would then carry a factor
the needs ladder does not have. **DIRECTOR DECISION REQUIRED (Q6).**

---

## §9 THE HISTORICAL PROGRESSION AS CHANGES TO CAUSAL RELATIONSHIPS

The mandate's requirement is that each development be a change to a causal relationship and never
a bonus. Under §6.5's FALLOW BUDGET and §7.2's loading coefficients, every item on the Director's
list becomes a change to **one of exactly two relationships** — how much of the land is sown, and
how much loading an execution delivers — which is what makes the list a progression rather than a
pile of features.

| development | the relationship it changes | carrier (F19) | what it explicitly is NOT |
|---|---|---|---|
| **manure / compost** | shortens the rest a field needs → raises the sown share of the FALLOW BUDGET | **the herd, the dung** — and note the herd is **already in ADR-013's derivation**, as the 25 ha of *"pasture for the traction and manure herd"* per 100 | not "+X % yield". The manure must be produced by animals that occupy land that is then not sown — the trade-off is in the budget |
| **crop rotation / legumes** | changes the *shape* of the budget: a restoring crop occupies the fallow slot, so the land rests and produces at once | **the rotation, a season** | not a research node. It is a **practice**, whose availability is a D-020 `requires` predicate over published variables (F23) |
| **industrial agriculture** | raises the per-labour output coefficient **and** raises the soil-depletion and runoff coefficients — one development, two coefficients, opposite signs | **tools, draught, the field** | not a yield bonus. The externality is in the same change, not in a later balancing patch |
| **synthetic fertilizer** | removes the nutrient term from the soil-condition equation **and** adds a runoff loading to the water medium | **the fertilizer good** | **not a permanent buff.** It is an INPUT that must be produced and consumed *every year*: stop making it and the relationship reverts. This is precisely the "lose practical access while retaining knowledge" the mandate's core principle requires, and it falls out of law 1 rather than being arranged |
| **pollution controls** | lowers the per-execution emission coefficient at a labour-and-goods cost **per unit of output** | **an institution, the abatement labour** | not a damage-reduction modifier applied after the damage. The cost scales with what is protected |
| **water treatment** | raises the **assimilative capacity** of the water medium | **the treatment works (a building), an institution** | not a +quality bonus. A building that must be built, maintained and staffed, and that fails when it is not |

### 9.1 How these become available — and the God object that must not be rebuilt

**RATIFIED (F23, F24).** The capability seam **already ships**: the D-020 predicate DSL over
published variables, with recipe availability as a live consumer, described in shipped data as
*"a knowledge gate over published variables, never a calendar date (law 4)"*. A universal
`CapabilitySystem` is **REJECTED**, and the recorded reason is **law 6, isolation** — it is
rejected as an **ownership shape**, not as an idea; the seam is conformant *"only as a shared
predicate grammar consumed independently by each domain system"*, *"never as a system that answers
for everyone"* (`capability-architecture-decision.md:165-175`).

**PROPOSED — what that means for this lane, stated so it cannot be read the other way:** a climate
or environment system **publishes variables and is read through predicates**, and **never owns or
coordinates any other domain's response to the environment**. There is no `EnvironmentSystem` that
tells agriculture what to do, no central "environmental state" object that other systems consult
for rulings, and no environmental capability registry. An "environmental capability owner" would be
the rejected God object wearing a green coat, and §1's F24 is cited here specifically so that it
is not.

**Deferred to the sibling lanes, by name.** *What* knowledge is, how a practice is discovered, how
it is institutionalised, how it diffuses and how it is lost are the subjects of
`docs/design/arch-C-knowledge.md`, `arch-D-technology-capability.md` and
`arch-E-breakthrough-domains.md`. This lane assumes only their conclusion that no new abstraction
is required, and supplies the **environmental side** of the predicates: the published variables a
practice's `requires` clause would read. **Note F25: `capability-architecture-decision.md` carries
a correction notice and its §4 recommendation is UNVERIFIED** — so this lane leans on the shipped
`requires` field (data, F23), not on that document's recommendation.

---

## §10 THE D-021 OBLIGATION — POSITIVE LOOPS AND THEIR AMPLITUDE-STRENGTHENING BRAKES

F20 is binding: *"Every positive feedback loop in the design must ship with at least one negative
feedback loop that strengthens with amplitude"*, **in the same milestone**. This design creates
three positive loops. Each is stated plainly so it can be attacked, with its paired negative.

**Loop 1 — INTENSIFICATION.** more people → more farm labour → more intensive cultivation → more
food → more people.
**Paired negative, PROPOSED:** intensity shortens fallow below what the soil can bear, and the
soil-condition penalty **grows with the shortfall**, so the harder the land is pushed the faster it
degrades. Carrier: **the fallow field**. *Strengthens with amplitude:* yes, by construction — the
brake is a function of the very quantity the loop raises.

**Loop 2 — INDUSTRIALISATION.** crafting output → wealth → more crafting → more output.
**Two paired negatives, PROPOSED:** (i) loadings rise with output while assimilative capacity does
not, so environmental quality falls **faster than linearly** in output, and it falls on the same
settlement's own water, soil and people; (ii) **EXIT** — D-021 valve 3, *"Angry people leave before
they revolt twice"* — reaches this loop today, because environmental quality enters happiness (§8)
and happiness feeds migration. *Strengthens with amplitude:* yes for both; the worse it gets the
more leave, and leaving removes the labour that produced the output. **This is the one place the
design gets a ratified valve for free, and it is worth stating that it is Exit specifically:
Voice and Endurance are M5 and M8 respectively (F20), so a design landing before them must not
rely on either.**

**Loop 3 — MITIGATION.** controls reduce damage → more industry is viable → more industry → more
loading.
**Paired negative, PROPOSED:** controls cost labour and goods **per unit of output protected**, out
of the same pools everything else draws on (D-035-C path 2, SHARED BUDGET — carrier: *a purse, a
labour pool*), so the more output is protected the more the protection costs. *Strengthens with
amplitude:* yes.

**The honest gap. PROPOSED and flagged:** loop 1's brake is the one that lands inside §6.4's wall.
If the Director rules shape (ii) — soil acting on output only — then **loop 1's brake does not
exist**, because output-only soil cannot make land permanently poorer and intensification has
nothing to run into. **A design that creates loop 1 without shape (i) or (iii) ships an unpaired
positive loop, which D-021 forbids.** This is not an argument for (iii); it is a consequence the
Director should have in hand when ruling Q4.

---

## §11 OBSERVABILITY PLACEMENT

Per F22, and per the standing rule that **nothing new is serialized merely to be observable**.

| object | kind (F22) | note |
|---|---|---|
| the CLIMATE DRIVER `c(region, t)` | **RECOMPUTED** in variant A (a pure function of elapsed years and worldgen fields); **READ** in variant B (a serialized mode row) | the choice of substrate is also a choice about what the glass box costs |
| the REALIZED TURN ANOMALY | **READ** | it is the value `ProductionSystem` used; note E-49's open v25 item — *"The APPLIED weather multiplier is not on prev (the row holds the NEXT draw)"* — which any climate design inherits and which an attribution audit wants closed |
| an EXTREME CLIMATE EPISODE | **RECOMPUTED** | by definition (§2): it has no state |
| a LOADING, per settlement per medium | **READ** | the primary row |
| world-level loading sources and sinks | **DIFFERENCED** | only if Q5 rules the `long`/Ledger reading — the flow table's first difference is then the exact world-level source and sink by reason, to the unit |
| ENVIRONMENTAL QUALITY | **RECOMPUTED** | a public static function on stored state, in `SettlementHappiness`'s idiom — never a private observer-side re-implementation |
| the FALLOW BUDGET | **READ** | if it becomes state at all (§6.4) |
| pollution → mortality attribution | **GAP** | there is no health substrate (B3); the record must say GAP rather than carry an observer-side formula |

---

## §12 DIMENSIONAL DECLARATION OF EVERY PROPOSED QUANTITY

Stated because S8 §4.1(b) requires it of specs and because E-50/E-51 make unit-bearing names a
standing review rule. **All PROPOSED.** Nothing here is a number; naming a value is tuning and
tuning belongs to a packet, not to a design.

| quantity | unit | type (law 7) | per-year rate? | state? |
|---|---|---|---|---|
| climate driver `c` | dimensionless anomaly, mean 0 | `double` | no — a state, sampled | variant A: none. variant B: a mode row |
| per-year food multiplier `m` | dimensionless, long-run mean 1 | `double` | no | none |
| REALIZED TURN ANOMALY `M` | dimensionless, mean 1 | `double` | no | one row per settlement |
| mode residence time `τ_mode` | **sim-years** | `double` | it is a time constant; the hazard `1/τ` is per-year and integrates as `1 − exp(−dt/τ)` | TUNE data |
| component period `P_k` | **sim-years** | `double` | no | worldgen field |
| soil-water availability | dimensionless [0,1] | `double` | no | one per settlement |
| river discharge proxy | dimensionless index | `double` | no | one per reach |
| sown share (FALLOW BUDGET) | dimensionless [0,1] | `double` | no | one per settlement |
| LOADING, per medium | **units of loading per sim-year** delivered | `long` **or** `double` — **Q5** | **yes**, integrated with `dtYears` | stock or pressure — **Q5** |
| loading decay | **per sim-year** | `double` | **yes** | TUNE |
| ASSIMILATIVE CAPACITY | **units of loading per sim-year absorbed** | `double` | **yes** | RECOMPUTED |
| ENVIRONMENTAL QUALITY | dimensionless sufficiency [0,1] | `double` | no | **none — derived reading** |
| per-execution emission coefficient | **loading units per recipe EXECUTION** (never per output unit — `goods.json:3`) | `double` | no | TUNE data |

---

## §13 CARRIERS OWED

Every cross-system edge proposed above whose D-035-C physical carrier this lane could **not**
name, listed as owed rather than waved through. F19: *"If none exists, it is an invented modifier
and is refused."*

| # | edge | why the carrier is not nameable yet | what would supply one |
|---|---|---|---|
| **O1** | **AIR loading → anything at all** | air has no transport medium, no assimilative capacity term and no consumer in this design except B3, which has no substrate. It is presently a stock with a source and a sink and no downstream edge — which is a loading nobody can be harmed by | either a health substrate (B3) or a crop-damage pathway; until then air is **PROPOSED for omission**, not for inclusion at zero |
| **O2** | **pollution → mortality (B3)** | the carrier (*a body*) is nameable; the **SUBSTRATE** is not — there is no health, disease or contamination quantity anywhere on the tree, and Health & disease is M8 on the Spine (F18). Lane E reaches the same conclusion for sanitation | an M8 health quantity that mortality already reads |
| **O3** | **teleconnection, IF the design is built as N per-region processes with a coupling matrix** | a coefficient linking two regional processes with no medium between them is an invented modifier. §3.3 avoids this by building one driver with regional gains (D-035-C path 4) — **but the debt is real for any design that does not** | one field, not N processes; or a modelled medium, which F30 anti-scopes |
| **O4** | **climate → the DISASTER hazard λ** | the obvious edge (a dry decade raising the murrain hazard) is **already DEFERRED with its reason recorded**: *"λ is constant; a hazard that rises in a bad-weather decade (drought → murrain) is a coupling the mandate did not ask for"* (`queue.md:1392`) | a director act reopening it; the carrier would be the herd and the fodder, which is nameable — the deferral is scope, not carrier |
| **O5** | **soil condition → the derived 26.0 (A5/A6)** | the carrier (*the land, the fallow field*) is nameable. What is owed is not a carrier but a **RULING** on §6.4 — the mechanism has a carrier and the substrate refuses it (ADR-008) and the ruled meaning of 26.0 contests it | Q4, plus ADR-008's reserved per-layer upgrade path if shape (i) is ruled |
| **O6** | **"the climate" as a carrier for A1** | *a season* is on D-035-C's explicit list, so A1 passes the test on its face. Recorded here anyway because the test's list was written for household needs and a *season* there means a harvest cycle, not a multi-decadal regime — the reading is **INFERRED** | a director reading of whether D-035-C's carrier list extends to a climate regime |

---

## §14 CONFLICTS SURFACED

Where this design touches or contradicts something RATIFIED. **Both sources are named. Nothing is
reconciled.** This is a required output, not a failure.

| # | source A | source B | the conflict |
|---|---|---|---|
| **X1** | **F15 — the CR-015 ruling in force:** *"Weather unclamped and unmodified (pending G1)"*, and `sigmaLogYield` / `correlationTimeYears` on the no-move list (`cr-015:573-576`, `:581`). | **The mandate's own direction**, relayed at E-119 / `m4-closure-audit.md:679-694`: *a deterministic, BOUNDED climate signal*. | **A bounded signal is a clamped signal by construction.** The whole of §3 is presently forbidden by a ruling in force. Recorded by the recovery lane at E-48 and unresolved. **DIRECTOR DECISION REQUIRED.** |
| **X2** | **F3 — the ratified T3.4b/T3.4c invariant:** the weather state holds stationary variance at every dt, *"which would be the era table altering the climate"*; and `m3-spec.md:52-53`. | **F1 law 3 applied to the published turn value**, and G1's measurement that the decade multiplier is 1.5× too variable (`cr-015:524-547`). | **This is C-02, already recorded as *"Two frozen items and a law disagree"*.** §4 shows the two halves have different answers under any integrating design: the annual process becomes dt-free (stronger than F3), and the published turn value becomes dt-varying (the law-3 answer). **A design cannot have both, and S8 §3 does not let a packet choose.** |
| **X3** | **F6 / E-26 — the ruled separation:** *"land quality is a constant of the land; weather is a coefficient on realised output"*; applying a multiplier to the land side is forbidden; the derived 26.0 is *"not provisional and not negotiable"* (`cr-003:297-298`). | **The mandate's Part 11 progression** — manure → soil restoration, industrial agriculture → yield up, fertilizer → nutrient limitation down — every item of which is a claim about **land quality changing**. | **Soil is a VARIABLE of the land, and the ruled dichotomy has no slot for it.** §6.4 states the three shapes and what each breaks. **DIRECTOR DECISION REQUIRED (Q4).** |
| **X4** | **F5 — the shipped spatial blend** runs over `SettlementDistances` **travel costs**, ratified at T3.4b/T3.4c with the ruling's *"SPATIAL CORRELATION REQUIRED"* behind it (`m3-spec.md:52`). | **§3.3** — climate correlation must be **geographic**, because weather does not follow roads, and E-32/E-40 already record the coupling as unphysical (*"improving roads shrinks the weather field's effective footprint in map terms"*). | **Changing the correlation geometry changes shipped, ratified behaviour and every golden.** Note also E-44: `spatialSharedFraction` and `spatialRangeCostUnits` currently have **NO semantic test** — mutants forcing k = 1 and a constant kernel both pass — so the shipped spatial property is asserted by code and by no test. Recorded, not resolved. |
| **X5** | **F7 / ADR-008** — terrain rasters immutable, uncloned, hash-folded; the reserved route reverses the exclusion *"only for the layers that gain writers"*. | **Any soil, vegetation, land-use or soil-pollution layer** — and D-009/D-010 already name **soil** and **vegetation** as intended raster layers (E-08) while D-022 **anti-scopes erosion and climate simulation** (F30). | **Three ratified items point three ways** and E-14/gap 6 record that nothing reconciles them. §7.3 routes *water* pollution around the wall using the existing immutable river polylines; **soil pollution has no such route** and meets the wall directly. |
| **X6** | **§3.7** needs "elapsed sim-years since the campaign epoch" as the driver's time coordinate. | **`docs/adr/cr-006-continuous-time-and-campaign-epoch.md` §2 is OPEN** — a conflict about the campaign epoch itself (`cr-006:111`), alongside §1's open conflict about mid-turn resolution. | The quantity the design keys on is under an unruled CR. Minor, and recorded rather than worked around. |
| **X7** | **F22 / the mandate's summary of the observability taxonomy** as *"READ / RECOMPUTED / DERIVED / GAP"*. | **The ratified text**, re-read this pass: the taxonomy is **five** kinds — READ · SUMMED · DIFFERENCED · RESIDUAL · RECOMPUTED — plus GAP, and there is no kind called "DERIVED" (`docs/observability-architecture.md:20-31`). | Recorded because §11 places every proposed object into the **five-kind** taxonomy, and a reader working from the four-name summary would find one of them missing. Not a repository conflict — a correction to the brief this lane was given. |

---

## §15 DIRECTOR QUESTIONS

Stated as questions. No preferred answer is attached to any of them.

**On the climate substrate**

1. **Q1 — Which substrate?** A closed-form quasi-periodic driver with no state and an exact turn
   integral, or a Markov regime chain with serialized modes and micro-stepped aggregation (§3.6)?
   They differ in schema cost, in how much libm enters the determinism surface (E-42, OPEN), and
   in whether the climate is in principle forecastable.
2. **Q2 — What is the turn-aggregation rule?** §4 shows that the rule at §3.4 *is* G1's answer:
   integrate the annual process and the published turn value varies with dt (law 3's answer);
   preserve the published value's dt-invariance and the annual process cannot be integrated (F3's
   answer). Which?
3. **Q3 — Is weather to be bounded at all**, given that F15's *"unclamped and unmodified"* is in
   force, and given that bounding removes the tail against which ADR-024 derived the
   weather/disaster separation and changes what CR-016's three broken gates are arguing about?
4. **Q4 — What may change land quality?** §6.4's three shapes: multiply the land side (reopens the
   ruled meaning of 26.0), multiply output only (mis-states what soil is, and leaves D-021 loop 1
   unpaired — §10), or promote ADR-013's land budget to state (opens a derived constant's internals).
5. **Q5 — Is a degradation stock a conserved `long` on the Ledger, or a rate-like `double`?**
   Gap 14, restated: the Spine's own word is *"stocks"* and laws 1 and 7 read together point at the
   `long`; the shipped precedent for environmental state points at the `double`.
6. **Q6 — May happiness carry an environmental factor that the frozen needs ladder does not have?**
   §8 shows the factor route is open without reopening D-018, and that taking it makes happiness
   and the needs ladder asymmetric.

**On scope and placement**

7. **Q7 — Is there to be a CLIMATE at all, distinct from weather?** Gap 1, restated unchanged,
   because it remains the prior question to all six above and no document records a decision
   either way.
8. **Q8 — What timescale do the persistent regional states have?** §3.2 shows a literal ENSO
   (9–24 months) is invisible at dt 10 and that the observable analogue is multi-decadal. Is the
   intent multi-decadal regimes, or is the intent that the structure be invisible in the early
   eras and emerge as dt shrinks?
9. **Q9 — Is the M9 placement of environment & climate still the intent?** Gap 16: the only source
   is the pre-M0 Spine table (F18), and nothing since restates it — while the mandate's Part 11
   progression reaches from the neolithic manure heap to industrial abatement, which is not one
   milestone's worth of world.
10. **Q10 — Does the fourth path go onto CR-015 §6.4's list, or into its own CR?** A sibling lane
    records that it *"would need to be put there — or put in its own CR — before anyone could
    implement it"* (`m4-closure-audit.md:712-714`), and that choice determines whether G1 is
    answered or bypassed.
11. **Q11 — Does D-035-C's carrier list extend to a climate regime?** O6: *a season* is on the
    list, but the list was written for household needs. A multi-decadal regime is a season only by
    analogy, and D-035-C is explicit that *"the seven paths are not a taxonomy to be extended by
    analogy."*

---

## §16 CAVEATS ON THIS DOCUMENT

1. **Nothing here is a ruling, a packet, or an implementation.** No production code, schema,
   golden, corridor, quarantine or ratified document was touched, and no milestone spec was
   written or amended.
2. **GOV-4.** `docs/design/recovered-decisions-climate-env-agri.md`, `docs/design/m4-closure-audit.md`
   and the sibling `arch-*` lanes are **SECONDARY** evidence — prior agents' output from this same
   mandate. Every RATIFIED fact in §1 was re-read at its own source on `7fb84eb` by this lane before
   being leaned on. Where this document cites an **E-nnn** row it is citing the recovery lane's
   index, and the underlying `file:line` is given alongside.
3. **The relayed fourth path (E-119) is recorded, not endorsed**, and its own source marks it
   *"DIRECTOR-STATED (relayed; UNVERIFIED as a repository document)"*. This design is a response to
   a direction whose status as a repository decision is exactly that.
4. **`docs/capability-architecture-decision.md` carries a correction notice and its §4
   recommendation returned SURVIVES_WITH_CONDITIONS (F25).** §9.1 therefore leans on the shipped
   `requires` field in data and on D-042's ratified rejection, not on that document's recommendation.
5. **No constant is named anywhere in this document**, deliberately. E-53's standing methodology —
   *"DERIVE, DO NOT FIT. Out-of-band with a derived constant beats in-band with a fitted one"* — and
   E-55's standard — derive the reference class from the model's own definitions **before** importing
   a historical number — both bind every number this architecture would eventually need, and neither
   can be honoured inside a design pass.
