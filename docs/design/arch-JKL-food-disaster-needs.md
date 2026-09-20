# ARCHITECTURE — OUTPUTS J, K, L: FOOD SYSTEM EVOLUTION (Part 12) · DISASTER RESILIENCE (Part 16) · EVOLVING HUMAN NEEDS (Part 17)

**DESIGN PASS. NOTHING IMPLEMENTED.** No production code, schema, golden, corridor, quarantine,
config value, data file or existing document was created, edited or deleted by this lane. The only
file this lane adds is this one, under `docs/design/`. Nothing here is ratified, certified or merged.

**Author authority: none. The DIRECTOR IS ChatGPT.** This lane does not rule. Where the design would
touch or contradict something RATIFIED, the conflict is surfaced with both sources and left
unresolved (§17). Where the repository records no decision, the question is put to the Director as a
question (§18), never as a proposal with a preferred answer attached.

## Tree pinned for every citation

- Branch `claude/civdemo-work-b1z2y4`, **`7fb84eb`** ("DESIGN LANE F/G/H"), working tree clean,
  `origin/claude/civdemo-work-b1z2y4` at the same commit. MEASURED (this lane): every `file:line`
  below was read at `7fb84eb`.
- **CONCURRENT SIBLING LANE, recorded against the pin.** A sibling lane of this same mandate
  committed `bcbeb46` ("DESIGN LANE I: climate / environment architecture") onto this branch while
  this lane was writing, so this document's own commit sits on `bcbeb46` rather than on `7fb84eb`.
  MEASURED (this lane): `bcbeb46` adds exactly one file, `docs/design/arch-I-climate-environment.md`,
  and `git diff --stat 7fb84eb bcbeb46 -- Sim.Core Sim.Data Sim.Tests Sim.Cli scripts CLAUDE.md
  docs/adr` is **empty**. Every `file:line` below therefore reads identically at `7fb84eb` and at
  `bcbeb46`. This lane did not read `arch-I-climate-environment.md`; where this document's Parts 12
  and 16 touch climate and terrain it cites `docs/design/recovered-decisions-climate-env-agri.md`
  (secondary) and the sources it verified directly.
- **GOV-4 applies to the recovery lanes as it applies to everything else.** This lane read
  `docs/design/recovered-decisions-food-disaster-needs.md` (F-rows),
  `docs/design/recovered-decisions-climate-env-agri.md` (E-rows), `docs/design/m4-closure-audit.md`
  and the sibling architecture lanes C / D / E / F-G-H **as secondary evidence**, and
  **re-verified against source, this pass**, every RATIFIED fact this design leans on. §1 is that
  verification. Where a recovery-lane row is cited without an independent source verification, it is
  labelled as secondary and says so.

**Label key** (mandate rule — every substantive claim carries exactly one):
**RATIFIED** (cite file:line) · **MEASURED** (cite record + tree) · **PROPOSED** (this phase's
design) · **INFERRED** (reasoned, not stated) · **DIRECTOR DECISION REQUIRED**.

**New terms introduced by this lane** (each PROPOSED, each defined once, at §2.2, §7.2 and §13.2):
**the five store terms** (T1…T5) · **the hazard/vulnerability split** · **the provision record**.
Everything else uses the repository's own vocabulary: the Spine, the D-020 predicate DSL, published
variables, the D-021 valves (Exit / Voice / Endurance), the D-035-C carrier test, corridor,
quarantine, latch, packet, law 1…7, CR-NNN / ADR-NNN / D-NNN.

---

## §0 WHAT THIS ARCHITECTURE IS FOR, AND WHAT IT DELIBERATELY DOES NOT DO

This document designs three things conceptually and builds none of them: how technology could change
the **mechanism** by which harvested food becomes eaten food (Part 12); how hazard could become
consequence through exposure, vulnerability, response and recovery without technology ever erasing a
disaster (Part 16); and how what a population *needs* could itself change as the civilization
develops, without a calendar ever creating a need (Part 17). Its organising commitment is that in
every one of the three, the object that moves is a **term inside a shipped resolution equation with
a named physical carrier** — never a bonus, never a grant, never a flag that a date sets. That is
law 2 and law 4 taken as the design's shape rather than as a rule someone has to remember.

**What it deliberately does not do.** It does not propose a food system, a hazard system, a needs
system or any other system. It proposes no packet, no schema, no row type and no config key. It does
not resolve CR-016, G1, Q-01 or any other open item; where its subject matter sits on top of one, it
names the open item and stops. It does not choose between the backward-from-consequence derivation
the tree ships and the forward-from-catalogue chain the Director has stated — §8 shows they are
architecturally different and hands the choice over intact. It does not resurrect the universal
`CapabilitySystem` under another name: nothing here owns a domain or coordinates one, and §2.5 states
the conformance test it holds itself to. And it does not pretend that "more technology" makes this
world better — §5.3 records that the most physically honest version of the preservation arc makes
the shipped food economy **worse before better**, and that this is the finding, not a flaw in the
design.

---

## §1 GROUND — THE FACTS THIS DESIGN STANDS ON, EACH VERIFIED THIS PASS

Each row was read from the cited source at `7fb84eb` by this lane. Recovery-lane row ids are given
where one exists, so the two records can be cross-read.

| # | fact | source, verified this pass | label | recovery row |
|---|---|---|---|---|
| **G1** | **Law 2.** *"coefficients inside resolution equations are fine; free-floating permanent buffs are banned."* | `CLAUDE.md:17` | RATIFIED | F-02 |
| **G2** | **Law 4.** *"capability derives from computed state, never from dates or era labels."* | `CLAUDE.md:19` | RATIFIED | F-04 |
| **G3** | **Law 1 / law 6 / law 7.** Conservation only through `Ledger.Transfer`/`Ledger.Flow`, stocks `long`; systems never reference each other, only State and Kernel; rates/ratios `double`. | `CLAUDE.md:16`, `:21`, `:22` | RATIFIED | F-01, F-05, F-06 |
| **G4** | **D-021 paired feedback.** *"Every positive feedback loop in the design must ship with at least one negative feedback loop that strengthens with amplitude."* | `docs/d021-stability-doctrine.md:8` | RATIFIED | F-12 |
| **G5** | **The three valves.** *"grievance expresses through three competing channels — **Exit, Voice, or Endurance** — and the sim always keeps at least one channel open."* Exit: *"Openness of exits (free movement, available land, transport reach) governs the valve"*. Endurance: *"Faith access, community institutions, and habituation absorb grievance into acquiescence… Effective, never infinite."* | `docs/d021-stability-doctrine.md:24`, `:28`, `:29` | RATIFIED | F-16, F-17, F-18 |
| **G6** | **The D-035-C carrier test.** *"Name the physical carrier — a good, a purse, a building, a policy, a body, a season. If none exists, it is an invented modifier and is refused."* And: *"The seven paths are not a taxonomy to be extended by analogy; a coupling that does not fit one of them has not found an eighth path, it has failed the carrier test."* | `docs/d035-needs-aggregation.md:91-96` | RATIFIED | F-52 |
| **G7** | **The seven legal coupling paths**, with carriers: 1 shared satisfier (a good) · 2 shared budget/input (a purse, a labour pool) · 3 demand coupling (a body) · 4 common cause (a world variable) · 5 capacity feedback (production, demography) · 6 institutional trade-off (a policy, an institution) · 7 grievance-level buffering (an institution). Path 5 is *"**Through production and demography, never directly.**"* | `docs/d035-needs-aggregation.md:79-87` | RATIFIED | F-51 |
| **G8** | **The universal `CapabilitySystem` is REJECTED, and the reason is law 6.** *"Gameplay interdependence is allowed; direct code coupling is not. Systems communicate through World State and kernel contracts, never by calling sibling domain systems."* · *"Economy may depend conceptually on knowledge, government, military, transport and institutions — that never justifies a sibling call."* · *"Do not create a universal God system such as a `CapabilitySystem` that owns every capability or coordinates every domain."* | `docs/d042-empire-and-player-control-addendum.md:136-142` | RATIFIED | F-169 |
| **G9** | **What replaces it: the D-020 predicate DSL over published variables.** *"The existing D-020 predicate machinery is the foundation for future capability evaluation."* | `docs/d042-empire-and-player-control-addendum.md:146-147` | RATIFIED | F-171 |
| **G10** | **The seam ships, with two live consumers.** `goods.json`'s `requires` is *"an optional D-020 availability predicate ('requires') — a knowledge gate over published variables, never a calendar date (law 4)"*, and the live datum is `"requires": "artisan_share > 0.05"`. | `Sim.Data/content/goods.json:3`, `:156` (and `:175`) | RATIFIED | — |
| **G11** | **The capability decision record carries a CORRECTION NOTICE and its §4 recommendation is UNVERIFIED.** *"the adversarial pass returned SURVIVES_WITH_CONDITIONS, not a clean survival"*; *"§4's recommendation is UNVERIFIED and must not be built against until it is attacked."* | `docs/capability-architecture-decision.md:7-27`, `:29-37` | MEASURED | — |
| **G12** | **`BoundStore` applies spoilage then a granary capacity bound, GRAIN ONLY.** Spoilage `1 − exp(−rate·dt)`; capacity `granaryYearsOfDemand × annualGrainDemand`, enforced only when `capacity > 0 && over > 0`; both are Ledger sinks with distinct reasons. Scope comment: *"Grain only. B-2a's base layer is STORED GRAIN; every other good's bounding is enrichment and out of this packet's fence."* | `Sim.Core/Systems/Consumption/ConsumptionSystem.cs:198-199`, `:285-325` | RATIFIED | F-86, F-95 |
| **G13** | **The two derived store constants and their carriers.** `grainSpoilagePerYear` 0.08, reference class *"mud-brick or pit granary, no chemical protection, no controlled atmosphere… 5-10%/yr to moulds, germination, insects and rodents; the midpoint is taken, not tuned. Carrier: decay."* `granaryYearsOfDemand` 1.5, *"Carrier: a structure of finite size, which grows with the settlement because more households means more granaries."* | `Sim.Data/content/sim.json:43-45` | RATIFIED | F-97, F-98 |
| **G14** | **One-directional substitution.** *"Unmet non-staple food demand falls back on the staple (grain): a household short of fish eats more bread rather than starving while grain sits in the store. Monotony is then paid in the D-035-A variety term inside satisfaction rather than in calories."* | `Sim.Core/Systems/Consumption/ConsumptionSystem.cs:30-37` | RATIFIED | F-79 |
| **G15** | **The harvest equation, and where the two multipliers enter.** `landSide = arableKm2 × YieldPerArableKm2PerYear`; `laborSide = farmLabor × OutputPerFarmerPerYear × toolFactor`; `toolFactor = 1 + ToolYieldBonusMax × equipRatio`; `ratePerYear = min(landSide, laborSide)`; and `foodMultiplier = HarvestWeatherFor(prev,…) × DisasterFor(prev,…)`. | `Sim.Core/Systems/Production/ProductionSystem.cs:227-234`, `:163` | MEASURED | A SHIPPED CONTRACT read from code, not a ruling: no D-decision, ADR or CR states this equation. The nearest ruling, CR-003 §3, governs only WHERE weather enters (*"weather multiplies REALISED OUTPUT"*, `ProductionSystem.cs:236`), not the `min(landSide, laborSide)` form itself. |
| **G16** | **`DisasterSystem` is a production shock in weather's class: one kind, reads nothing.** *"It does not decide famine — FoodState does… It reads no population, no stores, no deficits and no weather. It multiplies exactly the two FOOD paths harvest weather multiplies (farming, herding/fishing)… touches no store (a 'granary loss' kind is queued), and carries a single kind."* `p = 1 − exp(−λ dt)`; severity uniform on `[s_min, s_max]`; `multiplier = 1 − severity·min(remaining,dt)/dt`. | `Sim.Core/Systems/Disaster/DisasterSystem.cs:9-80`, `:97-155` | RATIFIED | F-130 |
| **G17** | **λ is DERIVED AT 0.01 and SHIPPED AT 0.0 — the mechanism is complete, tested and INERT; the rate is the Director's.** `"hazardPerYear": 0.0`. The `_doc` states the whole sequence, the reference class (England 1300–1700 ≈ 1/70 y; France by région 1/50–1/100 y; band 1/50–1/150) and *"it is ONE DATA EDIT (0.0 -> 0.01, or whatever the director rules) away from live."* | `Sim.Data/content/sim.json:234-235` | **DIRECTOR DECISION REQUIRED** | F-136, E-100 |
| **G18** | **The severity band's lower edge is derived AGAINST AN EFFECTIVE BUFFER, not against a hazard catalogue.** *"a famine-class event must be able to exhaust the full EFFECTIVE buffer of a settlement at the shipped surplus ratio ALONE at the coarsest era dt: `s_min * D > (dt_max (rho_ship - 1) + G) / rho_ship`… Threshold 3.46 production-years; D = 5, s_min = 0.75 gives s*D in [3.75, 5.0]. Dimensional, not a corridor fit: no outcome is targeted, rho_ship enters only as the size of the buffer the event must beat."* | `Sim.Data/content/sim.json:234` | RATIFIED | F-134, E-103 |
| **G19** | **CR-016 is OPEN and escalated; arming λ = 0.01 breaks three frozen gates; none of its three options is taken.** *"Recommendation: 3 for the cause, 2 for the magnitude, and NEITHER without the director."* | `docs/adr/cr-016-armed-disaster-fallout.md:3-34`, `:130-152` | **OPEN — DIRECTOR DECISION REQUIRED** | F-147, F-150 |
| **G20** | **CR-004 is WITHDRAWN BY ITS OWN EVIDENCE.** *"The store holds ~1.6 years. It survives one bad year. The premise is met and there is no conflict between the two frozen items. E1's '15%' was arithmetically correct and analytically meaningless."* What survives as fact: *"the store is pinned at the granary ceiling on 4,800 of 4,800 measured turns, and ~55% of every harvest is destroyed by spoilage plus overflow at every dt."* | `docs/adr/cr-004-granary-turn-length-mismatch.md:3-34` | WITHDRAWN (measured facts RATIFIED) | F-106, F-108 |
| **G21** | **`StructureRow` is written and read by nobody.** MEASURED (this lane, `7fb84eb`): `grep -rn "Structures" --include=*.cs Sim.Core/` returns only `WorldState.cs:1032` (the read-only projection), `SystemCatalog.cs:198` (wiring), `CanonicalSchema.cs:572-575/:956/:1020` (serialization), `ConstructionSystem.cs:115` (the writer) and `PathBuildSystem.cs:355` (a pass-through property on a wrapper). **No resolution equation anywhere consumes the granary count.** The granary project ships as data: `timber 40 + stone 20 + laborRequired 2.0`. | this lane's grep; `Sim.Data/content/goods.json:178-195` | **MEASURED (this lane)** | corroborates arch-E F23 |
| **G22** | **Pottery has exactly one consumer and it is the Comfort basket.** MEASURED (this lane): `pottery` appears in `goods.json:91` (the good), `:104-117` (the `pottery-firing` recipe: clay 2.0 + timber 0.5 → 1) and in `needs.json:90/:120/:150` (three classes' Comfort lines). Nothing in storage reads it. | this lane's grep | **MEASURED (this lane)** | corroborates arch-E E6 |
| **G23** | **Livestock and fish have NO store bound, NO spoilage, NO overflow — and the repository records NO INTENT either way.** *"the three goods are materially equivalent Sustenance inventories in every line of the basket mathematics, and materially non-equivalent in storage, raiding, colonisation, founding and migration — and no document or comment in the repository ever decided that they should be."* `GoodEntry` has no perishability, shelf-life, capacity or storability field. | `docs/t4.20-food-semantics.md:57-89`, `:48-50` (secondary: F-84, F-85; the `sim.json:43`/`ConsumptionSystem.cs:198` scope statements verified directly) | **OPEN — DIRECTOR RULING WANTED** | F-84, F-85, F-116 |
| **G24** | **The world is STORAGE-LIMITED, never FOOD-LIMITED.** *"overflow positive on 4,800/4,800 turns; harvest exceeded eating on 4,760/4,800 (the 40 exceptions are each seed's founding turn)."* | `docs/food-anomaly-investigation.md:630` (secondary; consistent with G20, verified) | MEASURED | F-109 |
| **G25** | **Terrain rasters are IMMUTABLE after worldgen, excluded from `Clone()`, hash-folded; the reversal route is per-layer and Director-approved.** Six layers ship (`elevation`, `water`, `temperature`, `moisture`, `fertility`, `movementCost`) plus rivers. No soil, no vegetation, no land-use, no pollution layer. | `docs/adr/adr-008-static-terrain.md:8-15`, `:37-40`; `Sim.Core/Worldgen/TerrainSet.cs:20-27` (secondary: E-13, E-14, E-15) | RATIFIED | E-13/14/15 |
| **G26** | **Happiness is a DERIVED READING over exactly two factors, and the absences are stated rather than stubbed.** *"The director named food, water, housing, clothing and taxation 'where available'… FOOD and HOUSING are available and are used. WATER is not modelled at all… CLOTHING/comfort exists only as a basket-bound need computed inside the needs system, so taking it would cross the D-021 line above… TAXATION does not exist on this tree in any form (M5 owns it) and is not invented here. Adding a factor later is one entry in `Factors` and its weight — the extension seam is the array."* | `Sim.Core/State/SettlementHappiness.cs:40-49`; the enum at `:77-83` | RATIFIED | F-69, F-70 |
| **G27** | **Happiness deliberately does not read the needs tables, and what it may read is a DIRECTOR'S CALL.** *"reusing the needs aggregate instead is a real improvement, and it is a DIRECTOR'S CALL because it turns on D-021, not on engineering taste."* | `Sim.Core/State/SettlementHappiness.cs:27-38` | **DIRECTOR DECISION REQUIRED** | F-67, F-68 |
| **G28** | **The eight-need ladder is frozen; BOUND grows by milestone; an UNBOUND need contributes exactly nothing.** *"The eight-need ladder is frozen (D-018 3); BOUND grows by milestone… An UNBOUND need contributes EXACTLY nothing whatever its weight says (T2.6 zero-effect gate)."* Shipped: Sustenance, Shelter, Comfort bound; Safety, Health, Belonging/Faith, Dignity/Liberty, Prospects unbound. | `Sim.Data/content/needs.json:2`, `:5-53` | RATIFIED | F-24, F-33, F-34 |
| **G29** | **Comfort is explicitly an ERA LADDER OF GOODS.** *"consumer goods above basics — the era ladder of pots → textiles → furniture → radios → devices"*. | `docs/d018-classes-and-needs.md:40` | RATIFIED | F-26 |
| **G30** | **Rising expectations REPLACES era tables; the habituation ratchet and relative deprivation stand.** *"Salience of Comfort, Liberty, and Prospects scales with the bucket's **literacy, urbanization, and media exposure** — computed state (Law 5)."* · *"Expectation baselines drift toward recent consumption: yesterday's luxury is today's floor. Losing accustomed comfort generates more grievance than never having had it."* · *"Grievance also accrues from visible inequality… Gini becomes flammable only when seen."* And the D-035-B supersession note: *"Ratcheting expectations and the relative-deprivation term also stand."* | `docs/d018-classes-and-needs.md:48-50`, `:56` | RATIFIED | F-29, F-30, F-31 |
| **G31** | **The habituation ratchet is DEFERRED IN IMPLEMENTATION, with a stated condition and a stated home.** *"expectation is FIXED at 1.0 — the D-018 §4 habituation ratchet is deferred to the milestone that gives needs supply curves to habituate to."* The constant: `private const double Expectation = 1.0;`. | `Sim.Core/Systems/NeedsGrievance/NeedsGrievanceSystem.cs:63-64`, `:85-86` | MEASURED | F-30 |
| **G32** | **Grievance drives NO behaviour until M5, and a CI gate enforces the read isolation.** *"NeedSatisfactions and Grievances are referenced ONLY by this system, serialization, StateEquals, tests, and Sim.Ui — enforced by the CI read-isolation grep; grievance drives NO behavior until M5 ships the unrest valves."* | `Sim.Core/Systems/NeedsGrievance/NeedsGrievanceSystem.cs:74-77` | RATIFIED and ENFORCED | F-35 |
| **G33** | **The observability taxonomy is FIVE kinds plus GAP, not four.** *"Every field in every record is therefore one of exactly five things"* — **READ · SUMMED · DIFFERENCED · RESIDUAL · RECOMPUTED** — and *"If a quantity would need a formula the simulation does not expose, it is a **GAP**… rather than an observer-side copy of the formula that will drift."* The one rule: *"The logger observes. The simulation calculates. The UI reads. The player issues orders."* | `docs/observability-architecture.md:16-33` | RATIFIED | — |
| **G34** | **Food entries sum to exactly 1.0 per class by construction.** *"a class's food entries sum to 1.0 by construction, so the basket changes WHAT is eaten and never how much nutrition a person needs."* | `Sim.Data/content/needs.json:67` | RATIFIED | F-78 |
| **G35** | **The enrichment layer's six items are the DIRECTOR'S list; the staging — not the content — is the ruling; each must act by MODIFYING the base spoilage rate or capacity, never by adding an independent term.** Storage technology · moisture · vermin · hygiene/store maintenance · seed corn · alternative uses. | `docs/m4-blocking-material.md:87-101` (secondary: F-114; the `sim.json:43` carrier language it rests on verified directly) | **DEFERRED** (list ratified, staging unscheduled) | F-114 |
| **G36** | **B-2b, the sequencing rule.** *"WHEN A BASE QUANTITY IS WRONG, ADDING DETAIL ON TOP MAKES THE ERROR HARDER TO FIND, NOT EASIER."* | `docs/m4-blocking-material.md:103-109` (secondary: F-115) | RATIFIED | F-115 |
| **G37** | **A second disaster kind has a stated price: a per-row famine-class predicate plus a kind registry. A "granary loss" kind is NOT AUTHORIZED.** | `docs/adr/adr-024-food-state-effective-deficit-disaster-shock.md:160-164`; `docs/adr/cr-015-famine-is-exceptional.md:516` (secondary: F-131, E-110) | REJECTED / NOT AUTHORIZED | F-131 |
| **G38** | **No food good has ever been traded; grain is structurally barred as the numéraire; food trade is a certified M4 exclusion.** | `docs/t4.20-food-semantics.md:128`; `docs/milestones.md:331-332` (secondary: F-89) | OPEN (missing capability) | F-89 |

---

# PART J — FOOD SYSTEM EVOLUTION (Mandate Part 12)

## §2 THE SHIPPED FOOD FLOW, AND THE ONLY PLACES A TECHNOLOGY COULD ENTER IT

### 2.1 The flow as it actually runs

MEASURED (this lane, at `7fb84eb`, from `ProductionSystem.cs:163/:227-234` and
`ConsumptionSystem.cs:30-37/:180-206/:285-325`):

```
LAND / LABOUR
  landSide  = EffectiveArableKm2 × YieldPerArableKm2PerYear (26.0)
  laborSide = farmLabor × OutputPerFarmerPerYear (5.0) × toolFactor
  toolFactor = 1 + ToolYieldBonusMax × equipRatio          ← the one shipped technology term
  ratePerYear = min(landSide, laborSide)
        × HarvestWeatherFor(prev) × DisasterFor(prev)       ← the two shipped shock multipliers
    ↓  Ledger source, reason Harvest (3)
STORE  GoodStockRow.Amount  (long, conserved, per settlement per good)
    ↓
EATING  cohort-weighted demand per class per good; one-directional substitution of unmet
        non-staple demand INTO the staple; proportional rationing across classes
    ↓  Ledger sink, reason Eaten (4)
DEFICIT d = clamp((requiredUnits − foodObtained) / requiredUnits, 0, 1)   ← the food balance's signal
    ↓
BOUND STORE — applied AFTER eating, deliberately, GRAIN ONLY
  1. SPOILAGE   lost = held × (1 − exp(−0.08 × dt))      Ledger sink, reason Spoilage (14)
  2. CAPACITY   capacity = 1.5 × annualGrainDemand
                over = Amount − capacity; enforced iff capacity > 0 && over > 0
                                                          Ledger sink, reason GranaryOverflow (15)
```

Three properties of this flow decide everything in Part 12, and all three are already on the record:

- **The store is the binding constraint, not the harvest.** G24: overflow positive on 4,800/4,800
  measured turns; ~55% of every harvest destroyed by spoilage plus overflow at every dt (G20).
- **Capacity is denominated in years of the settlement's own demand**, so it is **procyclical**: a
  settlement that loses population loses granary capacity on the same turn, and the surplus that
  would have fed the survivors is destroyed as overflow. RATIFIED
  (`ConsumptionSystem.cs:245-248`); its counter-cyclical alternative is CR-004 Option C, stated and
  never ruled (secondary: F-112, Q-05).
- **Only grain participates.** Livestock and fish are an unbounded, imperishable inventory that only
  feeds people (G23), and the repository records no intent either way.

### 2.2 PROPOSED — the five store terms

**PROPOSED (this lane), defined once.** A food technology in this project can only change one of
five terms. Naming them is the whole point: it converts "a technology improves food" into "a
technology moves T3", which is checkable.

| term | what it is | where it lives today | its value today |
|---|---|---|---|
| **T1 — the spoilage rate** | the per-year decay constant inside `1 − exp(−rate·dt)` | `consumption.grainSpoilagePerYear` | 0.08, DERIVED against *"mud-brick or pit granary, no chemical protection, no controlled atmosphere"* (G13) |
| **T2 — the capacity coefficient** | years-of-own-annual-demand the store may hold | `consumption.granaryYearsOfDemand` | 1.5, DERIVED, carrier *"a structure of finite size"* (G13) |
| **T3 — the set of goods bounded** | which goods `BoundStore` is called for | `ConsumptionSystem.cs:198-206` — grain only, by an explicit scope statement | {grain} |
| **T4 — the substitution direction / the basket** | what counts as feedable nutrition, and which way shortfall flows | `ConsumptionSystem.cs:30-37` and `needs.json` food lines | one-directional into the staple; food entries sum to 1.0 per class (G34) |
| **T5 — the spatial reach of the store** | whose store can feed whom | trade; **for food, nothing** — no food good has ever traded (G38) | {this settlement} |

**PROPOSED, and it is the whole design principle of Part 12 in one line:** *a food technology is a
named carrier that moves T1, T2, T3, T4 or T5. It is never a coefficient on the harvest, never a
bonus on satisfaction, never a happiness term, and never a new independent additive term — the last
is G35's own rule, stated by the Director: enrichment acts "by MODIFYING the base spoilage rate or
capacity, never by adding an independent term (law 2)".*

### 2.3 Why this cannot be "a food bonus", stated against the law rather than against taste

RATIFIED. Law 2 permits *"coefficients inside resolution equations"* and bans *"free-floating
permanent buffs"* (G1). A term that multiplies the harvest and never comes back down is the banned
shape; a term that changes how fast a store decays is the permitted shape, because a civilization
that stops maintaining its granaries gets the decay back. **INFERRED (this lane):** the T1/T2 terms
are strictly better than a harvest coefficient for law-2 purposes, because they are *loss* terms —
they can only be *reduced*, never removed, and the loss resumes the moment the carrier is gone. A
preservation technology cannot become a permanent aura, because what it modifies is a rate that
continues to run.

### 2.4 The one shipped technology term, and why it is the template

MEASURED (`ProductionSystem.cs:227-234`, verified this pass): `toolFactor = 1 + 0.3 × equipRatio`,
`equipRatio = min(1, toolStock / (farmLabor × ToolsPerFarmerToEquip))`, with a `ToolWear` Ledger sink
consuming the stock. **INFERRED (this lane):** this is the only end-to-end technology chain the
simulation actually runs, and it has the four properties every food technology proposed below must
reproduce — it terminates in a coefficient inside a resolution equation; it saturates (one tool set
per farmer); it decays (tools wear out through a Ledger sink); and its carrier is a conserved good.
Nothing is permanent. Sibling lane arch-E records the same chain as its template
(`docs/design/arch-E-breakthrough-domains.md:589-629`, secondary evidence, consistent with this
lane's own read of `ProductionSystem.cs`).

### 2.5 The conformance test this lane holds itself to (the `CapabilitySystem` rejection)

**RATIFIED (G8/G9):** what was rejected is the **coordination**, not the concept of capability — a
God object would have to call siblings or be called by them, which is law 6 violated by
construction. **PROPOSED (this lane) — the test every proposal below passes or is marked as
failing:**

1. **No new system owns "food technology".** Each term T1…T5 stays owned by the system that already
   owns it: T1, T2, T3 and T4 by `ConsumptionSystem`; T5 by the trade systems.
2. **The gate is a D-020 predicate over published variables, read by the owning system itself** —
   exactly the shipped `requires` shape (G10), never a shared evaluator that answers for everyone.
3. **No predicate operand is another capability.** The grammar's operands are a variable name or a
   number and nothing else (arch-D §3.2, secondary; consistent with G10's shipped datum). A
   prerequisite is a conjunct over published variables, never an edge to another capability.

Any proposal below that cannot pass all three is marked as failing, not quietly rewritten.

---

## §3 THE PROGRESSION, TERM BY TERM, WITH CARRIERS

**PROPOSED throughout this section.** Each stage names the term it moves, its D-035-C carrier, what
already exists on the tree, and what is missing. The stages are **not a ladder with ordered
thresholds on one accumulator** — arch-D and the capability record both identify ordered thresholds
`K1 < K2 < K3` on a monotone accumulator as *"tree edges with the edges hidden in the numbers"*
(`docs/capability-architecture-decision.md:135-140` — **MEASURED** as to the text; the
identification itself is **INFERRED**, it is that record's reasoning rather than a ruling, and the
record is UNVERIFIED by its own header per G11). They are parallel conditions, each independently
true or false, in the shape the Spine calls *"no tree; domain lattice lite"*.

| stage | term moved | direction | D-035-C carrier | what exists at `7fb84eb` | what is missing |
|---|---|---|---|---|---|
| **basic storage** (pit, mud-brick granary) | **T1, T2** | the baseline itself | **a building** (the granary) | the constants, with this exact reference class (G13) | nothing — this is what ships |
| **improved granaries** (raised floors, sealed jars, ventilation, rodent-proofing) | **T1 ↓ and T2 ↑** | both, and they are physically distinct: sealing lowers decay, building more raises the ceiling | **a building** (counted granaries) **and a good** (pottery vessels) | **both carriers are already in the tree and both are inert**: `StructureRow` is written by `ConstructionSystem` and read by nobody (G21); `pottery` is stocked and consumed only by the Comfort basket (G22) | a reader. Nothing new needs to exist — the granary the player builds does not yet hold grain |
| **preservation** (drying, salting, smoking, fermenting, brining) | **T3 first, then T1 for the newly-bounded goods** | **enlarging the bounded set, which makes the food economy STRICTER before it makes it looser** | **a good** (salt — absent from the roster; or containers) and **a season** (drying weather) | nothing. `GoodEntry` has no perishability, shelf-life, capacity or storability field (G23) | the data layer cannot express the distinction at all. See §5.3 — this is the honest and uncomfortable stage |
| **advanced storage** (silo, controlled atmosphere, fumigation) | **T1 ↓↓, T2 ↑↑** | same two terms, further | **a building** | the terms exist; the reference class in `sim.json:43` explicitly names what the current value excludes (*"no chemical protection, no controlled atmosphere"*) — i.e. the constant's own doc names its own successor | a mechanism that varies the constant per settlement |
| **refrigeration** | **T1 for animal goods** | collapses the decay rate of exactly the goods preservation could only slow | **a building** + **energy** | **no energy substrate exists.** Sibling lane arch-E records energy as a domain with **NO SUBSTRATE**: timber enters recipes as an ordinary Leontief input, not as fuel (`docs/design/arch-E-breakthrough-domains.md:544`, secondary) | an energy accounting the project does not have |
| **cold chain** | **T5** | the store stops being a place and becomes a network | **a good in motion** and **a path** | the traversal lattice and trade exist; **no food good has ever traded, and grain is structurally barred as the numéraire** (G38) | food trade. This stage is unreachable until Q-03 is answered |

**Two observations that are the point of the table.**

1. **The progression is not one axis.** Improved granaries and preservation are *different terms*
   (T2/T1 versus T3), and refrigeration and cold chain are *different terms again* (T1 for a
   different good-set, versus T5). A civilization can plausibly have excellent granaries and no
   preservation, or preservation without transport. **PROPOSED:** that is the correct shape, and it
   is what makes the arc a lattice rather than a tech tree — there is no single "storage level".
2. **Two of the six stages need nothing new built; they need a row that already exists to be read.**
   MEASURED (G21, G22): the granary project ships, the structure row ships, the pottery good ships,
   and no resolution equation consults any of them. **This lane does not propose closing that gap**
   — see §5.1 for why, and §17 X-01 for the conflict it would walk into.

---

## §4 HOW A STAGE WOULD BE GATED, WITHOUT A CALENDAR AND WITHOUT A TREE

**PROPOSED.** The gate is the shipped `requires` shape (G10) generalised to a term rather than to a
recipe: the owning system reads a D-020 predicate over published variables, and the predicate's truth
selects **which value a term takes**, not whether a capability is "owned".

Two shapes are available and they are **not interchangeable**, and the choice rule is mechanical:
**does flipping move a conserved stock?** **PROPOSED.** The rule is **not ratified**. The capability
record carries the text (`docs/capability-architecture-decision.md:189-192` — MEASURED as to the
text's existence only), but that document's recommendations are UNVERIFIED by its own header (G11),
so the text cannot carry the rule. This lane therefore **adopts by reference the disposition
`arch-D` already gives it** — PROPOSED there too, with the derivation re-done rather than inherited:
*"the same rule appears in `docs/capability-architecture-decision.md:186-192`, but that document's
recommendations are UNVERIFIED by its own header at `:29-37`, so this lane re-derives it"*
(`docs/design/arch-D-technology-capability.md:419-427`). The rule stands on that reasoning, not on
ratification.

- **Pure derived, no state** — the recipe-gate shape. Correct **under that rule** for T1 and T2,
  because flipping them moves no people: the next turn's spoilage is simply computed at a different
  rate. **PROPOSED:** T1 and T2 take this shape, which means **a civilization that loses the carrier
  loses the term in the same turn** — the non-monotonicity the mandate demands, for free, with no
  "loss" mechanism to write.
- **A latch with hysteresis** — the class-emergence shape. Required **under that rule** when
  flipping moves a conserved stock, because an oscillating input would chatter people back and forth
  every turn. **PROPOSED:** no food-storage term needs this. **INFERRED:** T3 might, if enlarging
  the bounded set could destroy a large store on a single bad turn and restore it on the next; that
  oscillation destroys `long` units through the Ledger and is not reversible. Recorded as a hazard,
  not designed.

**The latch, read correctly.** RATIFIED, and this lane restates it because the capability record's
own CORRECTION NOTICE says the earlier description was wrong: the latch *"records **current
satisfaction under hysteresis**"*, not that a predicate has fired
(`docs/capability-architecture-decision.md:20-23`). **INFERRED (this lane):** that is the difference
between a storage technology that can be *lost* and a tech-tree node that cannot, and it is the
single most load-bearing correction in that document for Part 12.

**What the predicate may read.** Published variables. Today the registry publishes
`food_surplus_ratio`, `artisan_share`, `population`, `trade_volume`
(`docs/observability-architecture.md:56-60`, verified this pass). **PROPOSED, and marked as owing a
decision:** a storage-technology predicate would want something like a per-settlement measure of
built granaries or of stocked containers. Publishing a new variable is a schema-adjacent act and this
lane proposes none; it records that **the predicate substrate exists and the operands do not**, which
is the same shape sibling lane arch-E records for communications (`E14`, secondary).

---

## §5 WHAT THIS DESIGN REFUSES, AND THE ONE STAGE THAT IS HONESTLY BAD NEWS

### 5.1 Refused: anything that moves T2 without walking into the quarantine consciously

**INFERRED (this lane), from RATIFIED facts.** The world is storage-limited (G24) and ~55% of every
harvest is destroyed (G20). Therefore **any** increase in T2 converts destroyed harvest directly into
stored food, into fed people, into population. This is not a marginal effect: it is the binding
constraint. MEASURED (G20): the store is at the ceiling on 4,800 of 4,800 turns, so the ceiling *is*
the economy.

The Malthus corridors are **QUARANTINED** with their bands frozen, and CR-003's standing instruction
is explicit: *"Do not loosen it, fit yield to it, alter consumption or demographics to satisfy it,
fabricate starvation, or delete the quarantine"* (secondary: F-163, quoting
`docs/m4-exit-inventory.md:308-309`). **This lane therefore proposes no packet touching T2, and
records that the first storage-technology packet in this project's future is a corridor event before
it is a gameplay feature.** It is named here so that whoever writes it cannot be surprised by it.

### 5.2 Refused: the shapes ADR-023 already ruled out

RATIFIED (secondary: F-55, F-56, quoting `docs/adr/adr-023-food-variety-is-d035a-only.md:5-8`,
`:27-30`). ADR-023 names an unusually explicit anti-scope: *"a second diversity index; Simpson
diversity; Shannon diversity; a new happiness bonus; an additive post-aggregate happiness modifier;
a multiplicative replacement for the existing happiness architecture; a food-only deprivation guard;
a new migration term; a new demographic survival term; a new revolt term; any new food-variety state;
any new serialized diversity field."* **PROPOSED, stated as a self-binding:** nothing in Part 12
reaches satisfaction or happiness at all. Every proposal above moves a *quantity of food*, and the
consequence for satisfaction follows through the existing basket mathematics with no new term. A
preservation arc that made food "better" rather than *more available* would be re-proposing a ruled-
out mechanism under a new name.

### 5.3 THE UNCOMFORTABLE STAGE — preservation makes the food economy worse first

**INFERRED (this lane), from RATIFIED facts G23, G12, G14 and G24.** Preservation technology, read
physically, does not *add* a capability: it *reduces a loss that the model does not currently
charge*. Today livestock and fish are perishable foods modelled as an unbounded imperishable
inventory. MEASURED (secondary, F-88): grain does 88.1% of the feeding while holding 0.8% of the food
inventory at turn 300; livestock and fish hold 99.2% of stored food and are never bounded.

So a physically honest preservation arc has this shape:

1. **First**, T3 enlarges: livestock and fish become bounded — spoiling fast, storable barely at all.
   The 99.2% inventory largely evaporates. The food economy gets *much* tighter.
2. **Then**, preservation raises their shelf life back toward grain's, stage by stage, with carriers.

**Stated plainly: step 1 alone is a large negative shock to every settlement in every world, and it
is the step that makes step 2 mean anything.** Shipping step 2 without step 1 is the "free-floating
permanent buff" shape law 2 bans (G1) — it would be a bonus on an inventory that never had the
problem the technology solves.

**This lane does not propose either step.** It records four things and stops:

- Whether the three food goods are materially equivalent or materially different is **Q-01, the
  cluster's largest explicit gap, and a DIRECTOR RULING is what closes it** (G23).
- `GoodEntry` cannot express perishability at all today, so the answer has nowhere to live (G23).
- G36's sequencing rule binds here with full force: *"WHEN A BASE QUANTITY IS WRONG, ADDING DETAIL ON
  TOP MAKES THE ERROR HARDER TO FIND, NOT EASIER."* Whether the livestock/fish base quantity is
  "wrong" is precisely what Q-01 asks.
- The step-1 shock would move every golden and would meet the same quarantined corridors as §5.1.

**DIRECTOR DECISION REQUIRED** (§18, DQ-J3).

### 5.4 Seed corn — the only listed item that changes the SHAPE of a bad year

MEASURED (G35): seed corn is on the Director's enrichment list, whose staging is unscheduled —
*"Distinct from spoilage: a reservation, not a loss — and it makes a bad year compound into the next
one."*

**PROPOSED (this lane), stated because it is the one enrichment item that is not a T1/T2 tuning
question:** seed corn is a **sixth term** in the sense that it is a *claim on the harvest taken
before the store*, not a loss from the store. Carrier: **next year's sowing** — a good, reserved.
Its architectural interest is that it is the first food mechanism in the project with **memory**: a
settlement that eats its seed corn is poorer *next* turn in a way no current mechanism represents,
because today a bad year is fully absorbed by the store and the next harvest is drawn fresh from
`min(landSide, laborSide)` with no dependence on what was sown.

**And it is the one that bears directly on D-021.** Seed corn is a positive feedback with no brake:
hunger → eat the seed → smaller harvest → more hunger. G4 requires that any such loop ship with a
negative feedback that **strengthens with amplitude**, in the same milestone. **PROPOSED:** the
Exit valve is the candidate already in the tree — a settlement that cannot sow loses people to
migration, and openness of exits governs the valve (G5) — but this lane has **not** demonstrated that
the Exit drain strengthens with the amplitude of a seed-corn spiral, and does not assert it. Recorded
as an owed demonstration (§16, OWED-J4).

---

## §6 THE D-021 OBLIGATION THIS PART OWES

RATIFIED (G4): every positive feedback loop ships with a negative feedback loop that strengthens with
amplitude, **in the same milestone**.

| proposed loop | is it positive? | the brake this lane can name | strengthens with amplitude? |
|---|---|---|---|
| better storage → more food kept → more people → more granaries built → more capacity | **yes** | **T2's own denomination**: capacity is years of *own annual demand*, so more people raise demand and the ceiling chases it rather than out-running it. And the land side of `min(landSide, laborSide)` is fixed by `EffectiveArableKm2` | **PARTIALLY — INFERRED.** The land bound is a hard ceiling that binds harder as population rises, which is amplitude-strengthening. The T2 denomination is *neutral*, not braking. This lane does not claim the pair is discharged |
| preservation → animal food keeps → herding worth more → more herding → less farming | **yes** | one-directional substitution (G14) already refuses to let a herding economy feed itself on animal goods alone: unmet non-staple demand falls back on the **staple**, never the reverse | **YES — RATIFIED mechanism, INFERRED application.** The more a settlement leans off grain, the harder the substitution asymmetry bites. F-81's measured pastoralist case (*"a pure pastoralist alternates deficit 0.94 / 0.00 forever"*, secondary) is this brake at full strength |
| seed corn spiral | **yes** | Exit (migration) | **NOT DEMONSTRATED.** §5.4; owed (OWED-J4) |
| cold chain → food trade → regional specialisation → less local food security | **yes** | none this lane can name | **NO.** Owed (OWED-J5) |

---

# PART K — DISASTER RESILIENCE (Mandate Part 16)

## §7 HAZARD → EXPOSURE → VULNERABILITY → IMPACT → RESPONSE → RECOVERY, MAPPED ONTO WHAT SHIPS

### 7.1 The six stages against `7fb84eb`

| stage | what the shipped tree has | evidence | label |
|---|---|---|---|
| **HAZARD** | one kind (crop failure), one global per-year rate λ, exact integration `p = 1 − exp(−λ dt)`, severity uniform on `[0.75, 1.0]`, duration 5.0 y, `RemainingYears` persisted by dimension. **λ = 0.0: the mechanism is complete, tested and inert.** | G16, G17 | RATIFIED / **DIRECTOR DECISION REQUIRED** on the rate |
| **EXPOSURE** | **ABSENT.** `DisasterSystem` reads no terrain, no climate, no water, no latitude, no biome; every settlement carries the same hazard. Spatial correlation and a weather-conditioned hazard are explicitly DEFERRED | G16; secondary `docs/design/m4-closure-audit.md:571`; F-132, E-109 | MEASURED |
| **VULNERABILITY** | **PRESENT, and it is the granary plus the surplus ratio.** `B_eff = G + dt(ρ − 1)` — the granary *plus the decade's pooled surplus at coarse dt* — is the buffer an event must exceed | G18; secondary F-143 | RATIFIED |
| **IMPACT** | a multiplier on exactly the two food paths weather multiplies; it never touches stores, never reads population or deficits, and **cannot decide famine** | G16 | RATIFIED |
| **RESPONSE** | **ALMOST ABSENT.** Three things happen and none of them is a decision: one-directional substitution into the staple (G14); the store drains; and outside FAMINE, adaptation absorbs up to `a = 0.20` of the shortfall with no mortality. There is **no relief, no reallocation, no rationing policy, no import** | G14; `Sim.Data/content/sim.json:230` (verified this pass) | MEASURED |
| **RECOVERY** | demographic rebound (T2.7 *"deficit-recovery fertility bounce"*), migration inflow, and the headroom growth cap that makes population approach the feedable limit asymptotically and **never overshoot into deficit** | secondary F-153, F-154, F-161 | RATIFIED |

### 7.2 PROPOSED — the hazard / vulnerability split

**PROPOSED (this lane), defined once.** The shipped `DisasterSystem` has a property worth
protecting and generalising: **it reads nothing** (G16). It draws a physical event and writes a row.
Everything that turns the event into a consequence happens elsewhere — the multiplier is consumed by
`ProductionSystem.DisasterFor`, the balance is computed by `ConsumptionSystem`, the label by
`FoodState`, the mortality by `DemographicsSystem`.

**The split, stated as a rule:** *the hazard side answers "did an event of this physical magnitude
occur here"; the vulnerability side answers "how much of it became a loss". They are owned by
different systems, communicate only through tables, and a technology may only ever move the
vulnerability side — unless it physically prevents the event from reaching the settlement, in which
case it moves the exposure side, and the design must say which.*

**Why this matters and is not bookkeeping.** It is the mechanical form of the mandate's *"science and
technology change VULNERABILITY and RESPONSE; they must NOT simply erase disasters"*. Under the
split, a drainage ditch cannot erase a flood: the event still occurs, still has its severity and its
duration, still appears in the row, and still shows in the chronicle. What changes is how much of the
crop the same event destroys. **INFERRED:** this also keeps law 6 intact — the hazard system never
grows a reader of buildings, population or stores, and the vulnerability term lives with the system
that already owns the resolution equation.

**And it preserves the observability contract.** RATIFIED (G33): the logger observes, the simulation
calculates. Under the split, the event and the consequence are *separately observable* — the player
can see that a flood occurred **and** that the drainage held, which is the difference between a
glass box and a number that moved.

---

## §8 THE ARCHITECTURAL DIFFERENCE THIS LANE MUST SURFACE AND WILL NOT RESOLVE

**This is a required output, not a failure.**

**Source A — the shipped derivation reasons BACKWARD, from consequence to cause.** RATIFIED (G18,
`sim.json:234`): the severity band's lower edge is chosen so that *"a famine-class event must be able
to exhaust the full EFFECTIVE buffer of a settlement at the shipped surplus ratio ALONE at the
coarsest era dt"*. The event's **size is derived from the buffer it must beat** — an internal
economic quantity (`ρ_ship = 1.3`, `G = 1.5`, `dt_max = 10`). The document is honest about this and
calls it *"a DIMENSIONAL derivation… not a corridor fit"*.

**Source B — the Director-stated chain reasons FORWARD, from a hazard catalogue.** *"real-world
hazard frequency → geographic exposure → settlement exposure → event probability → its representation
in a 10-year turn"*, with the simulation determining consequences, vulnerability and recovery, and
*"must not invent the underlying frequency by tuning for acceptable gameplay"*
(`docs/design/m4-closure-audit.md:551-555` — **DIRECTOR-STATED, relayed, UNVERIFIED as a repository
document**, per that lane's own §0).

**Why this is architectural and not a tuning question. INFERRED (this lane):** under A, the
magnitude of a hazard is a function of the economy it strikes, so **changing the economy changes what
a flood is**. Under B, the magnitude of a hazard is a function of the world's physical hazard
catalogue, so **changing the economy changes only what a flood does**. A storage technology that
raises `B_eff` would, under A, mechanically require `s_min` to be re-derived upward — the event must
still be able to exhaust the buffer — which means *every improvement in storage makes hazards
physically larger*. That is a strange world, and it is the direct consequence of deriving cause from
consequence. Under B it does not arise.

**What each side already concedes.** The closure audit records that the chain half-fits: `s_max = 1.0`
and `D = 5.0` **are** catalogue-derived (Irish potato 1846 ≈ 25% of normal; 1601–03 near-total
locally; the 3–7 y band), and `s_min = 0.75` is **not**
(`docs/design/m4-closure-audit.md:573`, secondary; the underlying `sim.json:234` text verified
directly by this lane). So the shipped design is a *hybrid*: two of three constants come from the
catalogue, one comes from the buffer.

**This lane does not choose.** It records the pair, and adds one consequence neither source states:
**§7.2's hazard/vulnerability split is only well-defined under B.** Under A, "vulnerability" is
already inside the hazard's definition, so a vulnerability-reducing technology and a severity
re-derivation are the same edit. **INFERRED. DIRECTOR DECISION REQUIRED** (§18, DQ-K1). Surfaced as
conflict X-03.

---

## §9 THE DIRECTOR'S WORKED EXAMPLE, WORKED THROUGH THE SHIPPED CODE

The example: *flood → crop loss; drainage → reduced crop loss; levees → reduced exposure;
forecasting → improved response; transport/storage → reduced food-system consequences; institutions
→ improved recovery — SAME physical event, DIFFERENT civilizational consequence.*

**PROPOSED throughout, with each link's status against `7fb84eb` stated.**

### 9.1 flood → crop loss

**Where it lands:** a `DisasterRow` with `Kind = 2`, multiplying the two food paths exactly as crop
failure does. **Carrier: a season** (the flood event itself, as the weather/hazard class).

**Status: BLOCKED with a stated price.** RATIFIED (G37): the shipped registry carries exactly one
kind, and a second kind with sub-class severities *"must add a per-row predicate
(`Severity·D·ρ_ship ≥ B_eff`) — queued with the kind registry"*. A "granary loss" kind is explicitly
**NOT AUTHORIZED**. **INFERRED:** a flood is the most natural granary-loss hazard there is — water
reaches the store, not only the field — so the very first kind a flood design wants is the one kind
the record forbids. Surfaced, not resolved (§18, DQ-K2).

### 9.2 drainage → reduced crop loss

**Term moved: VULNERABILITY.** The event's severity and duration are unchanged; the fraction of the
crop it destroys falls.

**Where it lands, under §7.2:** a coefficient in the consuming equation, i.e. where
`ProductionSystem` reads the disaster multiplier (`ProductionSystem.cs:163`, `:363-370`) — **not** in
`DisasterSystem`, which must keep reading nothing.

**Carrier: a building** (a drainage ditch / field-drain project), in the same shape as the shipped
granary project (`goods.json:178-195`, G21). **Status: the carrier shape exists; the reader does
not** — the same measured gap as §3's improved granaries: `StructureRow` is written and consumed by
nobody (G21).

**Law 2 check:** a coefficient inside a resolution equation is explicitly legal (G1), and this one
decays — drains silt up, the structure needs maintenance, and the housing system already ships the
maintenance-not-abstract-decay shape (`sim.json:207`, verified this pass). **PROPOSED:** any
vulnerability structure must carry maintenance for the same reason tools wear out (§2.4); a
permanent, maintenance-free drainage coefficient is the banned aura shape.

### 9.3 levees → reduced exposure

**Term moved: EXPOSURE, which does not exist.** MEASURED (G16, and the closure audit's row, §7.1):
λ is one global scalar; `DisasterSystem` reads no geography at all.

**What it would take, PROPOSED:** λ becomes a per-settlement rate — a catalogue base frequency
modified by the settlement's own geography (the terrain fields and the catchment the Spine already
carries) and then by built works. The closure audit proposes the same shape as design direction
(`docs/design/m4-closure-audit.md:578-580`, secondary, PROPOSED there too, not ratified).

**Carrier: a building** (the levee) **and a place** (the floodplain — `elevation`, `water`, `rivers`
and the hinterland means already exist and are sampled at founding, `WorldFounding.cs:325-352`,
secondary E-90).

**The wall, RATIFIED:** terrain rasters are immutable, uncloned and hash-folded (G25). A levee that
*changed the land* is forbidden; a levee that *changes a coefficient read against the land* is not.
**PROPOSED:** exposure must be a computed reading over immutable terrain plus mutable structures,
never a write to the terrain. That is the only shape ADR-008 leaves open without its per-layer
reversal.

**And the honest note:** reducing exposure is the one link in the Director's example that comes
closest to "erasing the disaster", because at the limit a levee makes `p → 0`. **PROPOSED fence:**
exposure works may reduce a settlement's hazard rate but must not be able to zero it, and the reason
is physical rather than balance-driven — a levee is overtopped by a large enough event, which is the
same statement as *"Effective, never infinite"* that D-021 makes about the Endurance valve (G5). This
lane proposes the fence and does not propose its form.

### 9.4 forecasting → improved response

**Term moved: RESPONSE, which is the weakest stage in the shipped tree (§7.1).**

**Status: NO SUBSTRATE, and the reason is structural rather than missing work.** MEASURED: the only
in-event responses that exist are substitution, the store draining and the `a = 0.20` adaptation
dead-zone — none of them is a *decision*. A forecast is only worth anything if something can act on
it, and the actors are the player (orders) and the AI (M5). RATIFIED (G33): *"The player issues
orders"* — the simulation does not decide for him.

**PROPOSED, as the only chain-conformant shape this lane can see:** forecasting is not a coefficient
at all. It is an **observability capability** — a D-020-gated readout that makes a hazard's
likelihood or an event's remaining duration *visible* to whoever issues orders, so the response is
the player's or the AI's, taken earlier. Under the observability taxonomy (G33) a hazard readout over
`DisasterRow` is **READ**, and an exposure readout would be **RECOMPUTED** from a public static. This
is the one place in Part 16 where the right answer is a reading rather than a mechanism, and it
follows the `SettlementHappiness` / `FoodState` precedent: a pure static over `IReadOnlyWorldState`,
crossing no isolation boundary (G26 and `FoodState.cs:22-26`, verified this pass).

**Carrier owed:** what makes a civilization *better* at forecasting, physically, is not nameable by
this lane without inventing knowledge state. It belongs to sibling lane arch-C's registers, not here.
Recorded as OWED-K1.

### 9.5 transport / storage → reduced food-system consequences

**Term moved: T5 (§2.2) and T2.** This is the direct bridge between Part 12 and Part 16: a region
that can move grain to a struck settlement converts a local catastrophe into a regional cost.

**Status: BLOCKED, and measured.** MEASURED (G38): no food good has ever traded, grain is
structurally barred as the numéraire, and food trade is a certified M4 exclusion. **INFERRED:** this
is the single largest missing resilience mechanism in the project — historically, famine relief is
overwhelmingly a transport and market phenomenon, and the simulation has no channel for it at all.
D-035-A's own stated purpose is *"a settlement that can feed itself entirely on grain still has a
reason to import fish"* (secondary F-41), and its purpose is unrealised for the same reason.

**And it interacts with D-021's Exit valve.** RATIFIED (G5): *"Openness of exits (free movement,
available land, transport reach) governs the valve."* **INFERRED:** with no food trade, the *only*
open valve under famine is Exit — people move, because grain cannot. Whether that is the intended
architecture or an artefact of a missing capability is not something this lane can determine from the
record. §18, DQ-K4.

### 9.6 institutions → improved recovery

**Term moved: RECOVERY.** **Status: the recovery mechanisms exist; the institutions do not.**
RATIFIED (secondary F-153, F-161): the headroom growth cap and the deficit-recovery fertility bounce
are shipped, and they are the recovery. Institutions land at M5 and later; D-021's landing schedule
puts the Endurance valve (faith, community, habituation) and organisation decay at **M8**
(`docs/d021-stability-doctrine.md:47-50`, secondary F-22).

**PROPOSED, and it is the only conformant route this lane can name:** institutional recovery enters
through **D-035-C path 6, INSTITUTIONAL TRADE-OFF** (*"one institution raises one need and lowers
another"*, carrier: a policy, an institution — G7) or **path 7, GRIEVANCE-LEVEL BUFFERING** (the
Endurance valve, *"Explicitly legal"*, operating **after** aggregation without changing satisfaction
— G7). A relief institution that simply made recovery faster with no cost and no carrier fails the
carrier test (G6). Under path 6, relief is *paid for* — a granary reserve is stored rather than
consumed, a labour draft builds rather than farms — and that is the mechanism the Director's link
needs.

**A fence this lane proposes explicitly:** institutional recovery must never be modelled as a
reduction in grievance decay time or as a direct happiness term. RATIFIED (F-21, secondary, quoting
`d021:54`): *"Anno avoids doom loops by deleting memory; we keep memory and pair it with history's
real release valves."* Forgiving the population is the forbidden shape; feeding them is the
permitted one.

---

## §10 WHAT FITS THE DIRECTOR'S CHAIN AND WHAT DOES NOT — CONSOLIDATED

MEASURED where marked; the fits/gaps table below is this lane's own reading of `7fb84eb`, and it
agrees with `docs/design/m4-closure-audit.md:557-573` (secondary), which this lane read after forming
it and now cites as corroboration rather than as source.

| chain step | fits? | the precise reason |
|---|---|---|
| real-world hazard frequency | **FITS** | λ's reference class is explicit real-world frequency with named sources and a band (G17) |
| geographic exposure | **DOES NOT FIT** | λ is one global scalar; the system reads no geography (G16). Spatial correlation is DEFERRED (E-109/F-132) |
| settlement exposure | **DOES NOT FIT ON THE HAZARD SIDE** | the only settlement-specific quantity, `ρ_ship`, enters the **severity** derivation, not the frequency (G18). The reference class's own downward adjustment — *"a single settlement's hinterland sees fewer than a région"* — is prose where the chain wants a computed term |
| event probability | **FITS, and is dt-exact** | `p = 1 − exp(−λ dt)`, exact integration of a per-year hazard (G16), law 3 satisfied |
| 10-year-turn representation | **FITS, with biases stated rather than hidden** | no new onset while active + once-per-turn booking: ≈4.7% of onsets truncated at dt 10, ≈4.8% rate bias at dt 0.5, both bounded by a test (G16, `sim.json:234`) |
| severity/duration from a catalogue | **HALF-FITS** | `s_max` and `D` are catalogue-derived; `s_min` is derived against `B_eff`, an internal economic quantity (§8) |
| simulation determines consequence, vulnerability, recovery | **FITS, and is the design's strongest conformance** | the system multiplies food output and reads nothing else; the ladder decides FAMINE from the balance; recovery is the ordinary demographic and migration response (G16) |
| frequency not invented by tuning for acceptable gameplay | **FITS, in the repository's own words** | *"choosing a lower rate that made the world survive would be tuning-to-outcome"* — which is why λ ships at 0.0 rather than at a comfortable value (G17, G19) |

**One tension this lane must surface (it is not this lane's to resolve, and the closure audit
surfaced it first — `m4-closure-audit.md:589-599`, secondary).** CR-016 option 2 is *"re-derive λ
against the demographic kernel, not against the historical reference class alone"* (G19). Read
against the Director's chain, that instruction points the wrong way: re-deriving the **hazard
frequency** against the **mortality kernel** derives frequency from a desired demographic outcome,
which is the shape the chain forbids. Under the chain, the term that should move is on the
**vulnerability or recovery** side. CR-016 itself names both terms and then proposes moving λ.
**DIRECTOR DECISION REQUIRED** (§18, DQ-K3). Surfaced as X-04.

---

## §11 THE D-021 OBLIGATION THIS PART OWES

| proposed loop | positive? | brake | strengthens with amplitude? |
|---|---|---|---|
| disaster → deaths + flight → smaller settlement → smaller granary (T2 is procyclical) → less buffer → worse next disaster | **yes, and it is already shipped** | the headroom growth cap makes population approach the feedable limit asymptotically and **never overshoot into deficit**, and *"the cap never removes a person"* (secondary F-153); the Exit valve drains along the real network, and `ω` is *"literally 'openness of exits'"* (secondary F-17) | **YES for the Exit arm — RATIFIED** (CR-015 N4 discharges the paired-feedback rule for the famine→flight loop explicitly, secondary F-12). **The procyclical-granary arm is NOT discharged** — it is CR-004 Option C, unruled (Q-05) |
| exposure works → fewer strikes → larger settlements in floodplains → larger loss when overtopped | **yes** | none this lane can name, and it is the classic levee effect | **NO.** Owed (OWED-K2). §9.3's "never zero" fence is a bound, not a brake |
| institutional recovery → faster rebound → higher population → more exposure | **yes** | path 6's trade-off cost (§9.6) | **NOT DEMONSTRATED.** Owed (OWED-K3) |

**And one standing constraint that binds this whole Part.** RATIFIED (secondary F-14, quoting
`d021:13`): *"forced-collapse runs must resolve into functioning successor configurations… 'Beyond
repair' is definitionally impossible: repair may take a century; an unplayable black hole may never
exist."* MEASURED (G19): at λ = 0.01 the dev world went 93,910 → 38 over 1000 turns. **INFERRED:**
any resilience design that adds hazard without adding recovery walks into that doctrine, and CR-016
is the open record of exactly that collision.

---

# PART L — EVOLVING HUMAN NEEDS (Mandate Part 17)

## §12 THE "HAPPINESS IS A FOUNDATION" DECISION — WHAT THE TREE ACTUALLY RECORDS

The mandate asks this lane to **preserve** the Director's earlier decision that M4 Happiness is a
FOUNDATION, not the permanent definition of all human needs, and to **find its actual source and cite
it**.

**MEASURED (this lane, at `7fb84eb`):** the literal sentence does not exist on this tree. This lane
re-ran the search independently: case-insensitive greps for `foundation`, `permanent definition`,
`provisional`, `placeholder` over `docs/`, `docs/adr/` and `Sim.Core/State/SettlementHappiness.cs`
return no such statement about happiness. The sibling recovery lane recorded the same absence as
conflict **C-07** (`docs/design/recovered-decisions-food-disaster-needs.md:372`, secondary; this
lane's own greps agree).

**What the tree DOES record — four lines, each verified this pass, and this lane treats them as the
decision's locus:**

1. **`Sim.Core/State/SettlementHappiness.cs:40-49`** (G26) — *"The director named food, water,
   housing, clothing and taxation 'where available'."* **Five factors named; two implemented; three
   absent with a stated reason each, rather than stubbed at 1.0** *"which would silently claim every
   settlement is well watered."*
2. **`SettlementHappiness.cs:48-49`** (G26) — *"Adding a factor later is one entry in `Factors` and
   its weight — the extension seam is the array."* Growth is declared, additive and named.
3. **`SettlementHappiness.cs:36-38`** (G27) — *"reusing the needs aggregate instead is a real
   improvement, and it is a DIRECTOR'S CALL because it turns on D-021, not on engineering taste."*
4. **`Sim.Data/content/needs.json:2`** (G28) — *"The eight-need ladder is frozen (D-018 3); **BOUND
   grows by milestone**."* The ratified definition of human needs is the **eight-need ladder**, not
   the two happiness factors, and happiness is *"deliberately not the same number as the needs
   aggregate"* (`Sim.Core/Observability/Explain/HappinessExplanation.cs:40-41`, secondary F-64/F-77).

**INFERRED (this lane):** read together, these four lines say exactly what the mandate says the
Director decided — happiness as shipped is a two-factor foundation with a declared extension seam,
and the permanent definition of human needs lives in D-018's frozen eight-need ladder whose BOUND set
grows. **This lane preserves that reading and builds on it.** Whether the decision the mandate refers
to is these four lines or a separate unfiled ruling is **not determinable from the tree**, and the
governing precedent is D-035's own provenance note — *an uncited ruling is refused and queried, not
reconstructed* (`docs/d035-needs-aggregation.md:11-14`, secondary F-23/§5.1). **DIRECTOR DECISION
REQUIRED** (§18, DQ-L1). Surfaced as X-05.

---

## §13 LUXURY → NORMAL EXPECTATION → BASIC NEED, AS COMPUTED STATE

### 13.1 The hard constraint, restated as a design shape

RATIFIED (G2): *"capability derives from computed state, never from dates or era labels."* **A
calendar date alone must not create a need.** RATIFIED (G30): D-018 §4 already answers this in the
project's own words — *"No era weight tables. 'Modern people want liberty' is not a calendar fact —
it is a computed one"* (`docs/d018-classes-and-needs.md:11`, verified this pass).

**PROPOSED (this lane), and it is the central claim of Part L:** *the repository does not need a new
mechanism for evolving needs. It needs three ratified-but-unimplemented D-018 §4 mechanisms, and the
design work is stating what computed state each one reads.*

| the three ratified mechanisms (G30) | what evolves | status on this tree |
|---|---|---|
| **rising expectations** | the **salience** (weight) of Comfort, Liberty and Prospects, scaling with *"literacy, urbanization, and media exposure — computed state (Law 5)"* | **RATIFIED, NOT IMPLEMENTED ANYWHERE** (secondary F-29, Q-18) |
| **the habituation ratchet** | the **expectation baseline**: *"yesterday's luxury is today's floor"* | **RATIFIED, DEFERRED IN IMPLEMENTATION** with a stated condition — `expectation` is a `const double = 1.0` (G31) |
| **the era ladder of Comfort satisfiers** | the **satisfier set**: *"pots → textiles → furniture → radios → devices"* | **RATIFIED** (G29); the ladder's first rung (pottery, cloth) ships in the baskets (G22) |

A fourth, **relative deprivation** (*"Gini becomes flammable only when seen"*, G30), is ratified and
unimplemented; it changes *who* compares to *whom* rather than *what* is needed, so this lane names it
and does not design it.

### 13.2 PROPOSED — the provision record, and the three candidate state sources

The habituation ratchet needs something to habituate **to**, and the code's own deferral names that
as the condition: *"deferred to the milestone that gives needs supply curves to habituate to"*
(G31). So the design question the mandate poses — *adoption fraction? sustained availability over a
period? the share of a class that has had it? a generation turnover?* — is precisely the question of
what the baseline drifts toward.

**PROPOSED term, defined once: the provision record** — whatever computed quantity the expectation
baseline drifts toward. This lane does not choose which; it states the four candidates with what each
implies mechanically, because they are **not** interchangeable and the choice has consequences the
Director should see.

| candidate | the state it needs | what it makes true | what it makes false |
|---|---|---|---|
| **A — instantaneous availability** (this turn's satisfaction) | none: `NeedSatisfactionRow` already ships | zero new state; pure derived | **no ratchet at all** — expectation would track supply exactly, and *"losing accustomed comfort"* would generate no more grievance than never having had it. **This is not the ratified mechanism** (G30) |
| **B — a smoothed record of recent satisfaction** | one serialized `double` per (settlement, class, need), the `SmoothedAttractivenessRow` shape that already ships | an exponential drift with a per-year rate, dt-integrated (law 3); a decade of plenty raises the floor, a decade of want lowers it slowly | needs a new serialized row type, which under the project's own rule ships a POPULATED-table test with exact `ExpectedLength`, bit-exact round-trip and hash equality (`CLAUDE.md`, T1.1/T1.3 precedent) |
| **C — the share of a class that has had it, sustained** | an adoption fraction over buckets | expectation becomes **social** rather than personal — the mandate's *"cultural adoption"* term. A luxury becomes an expectation when most of your class has one | needs a per-class adoption quantity nothing publishes today |
| **D — generation turnover** | **nothing new**: `turnoverRate = (PREV births + deaths) / PREV population` is **already computed and already used**, for grievance decay (`NeedsGrievanceSystem.cs:66-72`, verified this pass) | expectation ratchets **at the speed of cohort replacement** — children raised with pottery expect pottery; their parents remember doing without. This is the most historically defensible of the four and the cheapest in new state | the ratchet becomes slow in a stable population and fast in a churning one, which is a strong and possibly unwanted coupling |

**PROPOSED, stated as an observation rather than a recommendation:** candidate D is the only one that
requires no new state at all and reuses a quantity the needs system already reads for exactly the
adjacent purpose (generational decay of grievance, D-021 valve 8 — G5's *"Memory is long… but not
immortal"*). **DIRECTOR DECISION REQUIRED** (§18, DQ-L2). This lane does not choose, because the
choice determines whether a new serialized row exists, and that is a schema question.

**The law-4 check, which is the whole point:** all four candidates are functions of computed state —
satisfaction rows, bucket counts, births and deaths. **None of them can be reached by a date.** A
civilization that never produces pottery never habituates to pottery, at any year, in any era. That
is the mandate's constraint satisfied structurally rather than by a rule.

### 13.3 The three-stage transition, in the ratified structure

**PROPOSED.** *Luxury → normal expectation → basic need* is not three states of a need. It is three
different objects moving, in this order:

| stage | what is actually true | the state that says so | law-4 clean? |
|---|---|---|---|
| **luxury** | the good exists, is produced somewhere, and is **not in this class's basket** — or is in it at a small weight | `needs.json` basket lines per class; the good's stock | yes — production is computed |
| **normal expectation** | the good is in the basket, is reliably supplied, and **the provision record has risen**, so the expectation baseline for that need approaches 1.0 | the provision record (§13.2) | yes |
| **basic need** | **the ratchet has bitten**: losing the good now generates more grievance than never having had it, because `(expectation − S)⁺` is measured against a raised baseline. And CES with σ < 1 means the shortfall **cannot be bought off** by surplus elsewhere | the expectation baseline + D-035-B's non-compensatory aggregation, which already ships at σ = 0.5 | yes |

**The key structural point, RATIFIED rather than proposed:** *the transition to "basic" does not
require a new need, a new tier, or a change to the frozen ladder.* D-035-B's CES with σ < 1 already
makes every bound need non-compensatory — *"a need at zero cannot be bought off by surplus
elsewhere"* (secondary F-46, quoting `d035:42-53`; the shipped σ = 0.5 verified at
`Sim.Data/content/needs.json:58`). What "becoming basic" means mechanically is **the expectation
baseline rising**, and nothing else.

**And the ratchet is where the asymmetry lives.** RATIFIED (G30): *"Losing accustomed comfort
generates more grievance than never having had it."* **INFERRED (this lane):** that asymmetry is the
entire reason the baseline must be state and not a data constant — a symmetric expectation would make
gaining and losing a satisfier equal and opposite, which is exactly what the Director's sentence
denies.

### 13.4 What makes a genuinely NEW need emerge

The mandate asks how new needs emerge from technology + production + institutions + urbanization +
cultural adoption. **MEASURED (G28), and it constrains the answer hard:** the ladder is frozen at
eight, *"BOUND grows by milestone"*, and *"An UNBOUND need contributes EXACTLY nothing whatever its
weight says."*

**MEASURED (this lane, `needs.json:5-53`):** three of eight are bound (Sustenance, Shelter, Comfort);
five are unbound (Safety, Health, Belonging/Faith, Dignity/Liberty, Prospects). So **five ratified
needs are already waiting, inert by construction, and binding one is a milestone act on data, not a
runtime event.**

**PROPOSED, and it is the honest shape:**

- **A "new need" in this architecture is a need that BECOMES BOUND** — i.e. the simulation begins to
  compute a supply for it. Health becomes bound when a health supply exists; Safety when a policing
  or defence supply exists. That is a milestone act.
- **A "new need" is NOT a ninth row.** A ninth need is a D-decision change (G28, secondary F-24).
- **What genuinely evolves at runtime is the SATISFIER SET, not the need.** D-018 already says so for
  Comfort: *"the era ladder of pots → textiles → furniture → radios → devices"* (G29). A civilization
  that learns to weave does not acquire a new need; the set of things that satisfy Comfort grows, the
  basket composition shifts, and the D-035-A variety term inside the satisfaction equation registers
  the change **as satisfaction, never as a bonus** (secondary F-39/F-40, quoting `d035:20-30`).

**The architectural question this exposes, and it is real. DIRECTOR DECISION REQUIRED (DQ-L3):**
basket composition is **data today** — per class, TUNE, with food entries summing to exactly 1.0 by
construction (G34). For the satisfier set to evolve at runtime, some part of the basket must become
computed state. That is a genuine architectural change, it must preserve G34's sum-to-1.0 invariant
(otherwise a class can be made hungrier by learning to weave, which is exactly what G34 exists to
prevent), and this lane proposes no form for it.

---

## §14 HOW AN EVOLVING NEED ENTERS THE D-018 / D-035 STRUCTURE WITHOUT BREAKING IT

**PROPOSED.** Four entry points exist, and exactly one of them is the right one for each kind of
change. Getting this wrong is how a "rising expectations" feature becomes a free-floating modifier.

| the change | its legal entry point | why the others are wrong |
|---|---|---|
| **a satisfier joins a need** | a basket line (D-035-C **path 1, SHARED SATISFIER**, carrier: a good — G7) | it is not a weight change: a new satisfier changes *what* satisfies, not *how much* the need matters |
| **a need becomes more salient as the society develops** | the **Tier B class signature weights**, driven by rising expectations over published variables (G30) — *"Shipped as data, all TUNE"* (`d018:47`) | not a happiness factor (G26's array is a different object with a different scope note); not a new need |
| **the same supply now satisfies less** | the **expectation baseline** in `(expectation − S)⁺` (G31) | **not** a change to `S`. Moving satisfaction to represent an expectation change would make the two inseparable and would silently change what the CES aggregate means |
| **a need's deprivation becomes unbearable rather than merely bad** | it already is: **D-035-B's σ < 1** makes every bound need non-compensatory, and **Tier A** gate needs override signature weights below a floor, superlinearly (`d018:46`, retained by D-035-B, secondary F-27) | a new guard, floor or threshold for one need is F-60's rejected *"food-only deprivation guard"* shape — *"there is no food-only deprivation predicate in the architecture"* |

**Three fences this lane proposes on itself, each traceable to a ratified rejection:**

1. **Nothing in Part L reaches happiness.** MEASURED (G27): what happiness may read is a **DIRECTOR'S
   CALL**, and the read-isolation gate makes a single reference from `SettlementHappiness.cs` to the
   needs tables a CI failure (G32, and `SettlementHappiness.cs` is **not** on the gate's allowlist —
   secondary F-67). An evolving-needs design that moved happiness would be activating D-021 as a side
   effect, which the standing fence forbids by name (secondary F-75: *"Do not activate D-021 as a
   side effect of M5 startup."*).
2. **No second channel for anything D-035-A already carries.** RATIFIED (secondary F-55): *"Any
   future change to the salience of food variety must calibrate D-035-A rather than introduce another
   channel."*
3. **Rising expectations changes weights, never the aggregation.** RATIFIED (secondary F-47): the
   D-035-B acceptance test — *"With one need pinned at 1.0 and another at 0.0, aggregate grievance
   must remain above a stated floor for ALL values of the first"* — is inherited verbatim by any
   replacement aggregation. A weight-driven design never touches it; an aggregation-driven one must
   re-prove it RED against a weighted sum.

---

## §15 NOT IMPOSING MODERN EXPECTATIONS ON ANCIENT CIVILIZATIONS

The mandate asks specifically how an evolving need enters without imposing modern expectations on
ancient civilizations. **PROPOSED — four structural guarantees, each resting on something ratified:**

1. **An UNBOUND need contributes exactly nothing, whatever its weight says** (G28, T2.6 zero-effect
   gate). A Neolithic settlement has no Prospects grievance because nothing computes a Prospects
   supply — not because a rule says "too early".
2. **The expectation baseline starts where the supply starts.** Under any §13.2 candidate, a
   population that has never had pottery has a baseline that has never risen. The ratchet is
   *history-dependent by construction*, which is the opposite of a modern default.
3. **The satisfier set is bounded by what the civilization can make.** A basket line for a good
   nobody produces yields zero supply, and one-directional substitution (G14) is defined only into
   the staple. There is no mechanism by which an unmakeable good creates unmet demand.
4. **Salience reads the society's own computed state.** Rising expectations scales with *"literacy,
   urbanization, and media exposure"* (G30) — three quantities that are near zero in an early world
   and cannot be reached by a date. **MEASURED caveat (this lane):** none of the three is published
   as a variable today; the published registry is `food_surplus_ratio`, `artisan_share`,
   `population`, `trade_volume` (`docs/observability-architecture.md:56-60`). So the ratified
   mechanism has **no operands**. Recorded as OWED-L1 and DQ-L4.

**And the honest counter-consideration this lane must state rather than bury.** RATIFIED (G30):
D-018 names the *intended* consequence — *"development raises unrest potential before satisfying it;
revolutions cluster in modernizing societies, not stagnant ones"*. **INFERRED:** rising expectations
is therefore, by the Director's own design, a **positive feedback that makes development
destabilising**. Under D-021's paired-feedback rule (G4) it owes a negative feedback that strengthens
with amplitude, **in the same milestone**. The natural candidate is the **Endurance** valve — *"Faith
access, community institutions, and habituation absorb grievance into acquiescence… Effective, never
infinite"* (G5) — and D-021's landing schedule puts Endurance at **M8** while rising expectations
would plausibly want to land with the unrest valves at M5 (secondary F-22, quoting `d021:47-50`).
**That is a scheduling collision between two ratified items and this lane does not resolve it.**
Surfaced as X-06; DQ-L5.

---

## §16 CARRIERS OWED

Every proposed cross-system edge in this document whose D-035-C physical carrier this lane could
**not** name. Per G6, an edge without a carrier *"has not found an eighth path, it has failed the
carrier test"* — these are recorded as failures, not as pending work.

| id | the proposed edge | why no carrier | where it appears |
|---|---|---|---|
| **OWED-J1** | **a technology that lowers the spoilage rate T1 for *grain* beyond sealed containers** — e.g. "better drying" | drying is a **season** and a **place** (sun, humidity), and the moisture field exists (G25) — but the edge from a moisture *reading* to a *settlement-specific* spoilage rate has no physical intermediate this lane can name. G35 lists MOISTURE as *"Nearly free — the carrier already exists"*; this lane could not reproduce that claim, because the carrier for *exposure to* moisture in a store is a building, and G21 shows the building is unread | §3 |
| **OWED-J2** | **vermin and hygiene as spoilage drivers** (two of G35's six items) | G35 names their carriers — settlement density, the labour pool — and both quantities exist. What this lane cannot name is the **physical intermediate** between a labour allocation and a store's decay rate: store maintenance is not a sector, not a project and not a recipe on this tree | §3, §5 |
| **OWED-J3** | **preservation as a good** (salt, brine, smoke) | **salt does not exist in the goods roster.** MEASURED (this lane): `goods.json` carries 14 goods and salt is not among them. A preservation arc whose carrier is a good needs that good to exist first | §3, §5.3 |
| **OWED-J4** | **the seed-corn spiral's D-021 brake** | the Exit valve is nameable but this lane has **not** demonstrated it strengthens with the amplitude of a seed-corn spiral, and asserting it without a demonstration is exactly what G4 forbids | §5.4, §6 |
| **OWED-J5** | **the cold-chain / food-trade loop's brake** (regional specialisation reducing local food security) | none nameable. Historically the brake is price and storage; the sim has no food price channel because grain is the numéraire (G38) | §6 |
| **OWED-K1** | **what makes a civilization better at forecasting** | it is knowledge state, which belongs to sibling lane arch-C's registers. Naming a carrier here would be inventing knowledge state in a food/disaster lane | §9.4 |
| **OWED-K2** | **the levee-effect brake** (exposure works → denser floodplain settlement → larger loss when overtopped) | none nameable. This is a genuine open resilience problem, not a gap in this lane's search | §9.3, §11 |
| **OWED-K3** | **institutional recovery's brake** | path 6's trade-off is the *cost*, not the *brake*; this lane did not demonstrate amplitude-strengthening | §9.6, §11 |
| **OWED-K4** | **geographic exposure → per-settlement λ** | the *terrain* carrier is nameable (a place: floodplain, coast, arid margin — G25's fields). What is **not** nameable is the carrier for the step the reference class itself performs in prose: *"a single settlement's hinterland sees fewer than a région"* (G17). That is a **denominator change**, and a denominator is not a carrier | §9.3, §10 |
| **OWED-L1** | **rising expectations' operands** — literacy, urbanization, media exposure | none of the three is published, and two of them (literacy, media) have no substrate anywhere on this tree. The carrier for *urbanization* arguably exists (`SizeTier`, housing density) and this lane does not assert it, having not verified a publisher | §15 |
| **OWED-L2** | **the satisfier set becoming computed state** (§13.4) | the carrier for "this class now buys pottery" is **a purse** (D-035-C path 2) — and money does not exist on this tree; GOV-2 §1a defers it past M5 (`docs/capability-architecture-decision.md:64-73`, verified this pass). Without a purse, a computed basket has no mechanism for *why* composition shifts | §13.4 |

---

## §17 CONFLICTS SURFACED

Both sources recorded; nothing resolved; no side taken.

**X-01 — A storage technology is a corridor event, and the corridors are quarantined.**
*Source A:* the storage-technology arc is the Director's own list item and is flagged *"Most valuable
of the list, and the strongest candidate to follow the base layer: resilience becomes a real
investment decision and a genuine technology arc across eras"* (`docs/m4-blocking-material.md:87-101`,
secondary F-114).
*Source B:* the Malthus corridors are QUARANTINED with bands held immovable, and the standing
instruction is *"Do not loosen it, fit yield to it, alter consumption or demographics to satisfy it,
fabricate starvation, or delete the quarantine"* (secondary F-163, quoting
`docs/m4-exit-inventory.md:308-309` and `cr-003:292-298`).
*This lane's addition, MEASURED:* the world is storage-limited on 4,800/4,800 turns (G24) and ~55% of
every harvest is destroyed (G20), so **any** change to T2 moves the demographic outcome directly. The
most valuable item on the enrichment list is therefore also the one that cannot be shipped without
meeting the quarantine. **Not reconciled.**

**X-02 — The granary's carrier is named in ratified text and is not read by any equation.**
*Source A:* `Sim.Data/content/sim.json:43` states the carrier for `granaryYearsOfDemand`: *"a
structure of finite size, which grows with the settlement because more households means more
granaries."* RATIFIED, verified this pass.
*Source B:* MEASURED (this lane, G21): `StructureRow` — written by `ConstructionSystem` when a
granary project completes — is read by **no resolution equation anywhere in `Sim.Core`**. The
constant scales with population instead of with granaries.
*Note:* this is not asserted as a defect. The `_doc` describes the constant's **derivation** (why 1.5
is the right number), and a derivation may legitimately name a physical referent it does not read.
**Recorded because a storage-technology design will meet it on day one**, and because sibling lane
arch-E records the same finding independently (`arch-E…:653-658`, secondary). **Not reconciled.**

**X-03 — The severity derivation reasons backward; the Director's chain reasons forward.**
*Source A:* `Sim.Data/content/sim.json:234` (RATIFIED, verified this pass): `s_min` is derived so the
event can exhaust `B_eff`; *"Dimensional, not a corridor fit."*
*Source B:* the Director-stated chain — *"real-world hazard frequency → geographic exposure →
settlement exposure → event probability → its representation in a 10-year turn"*, with *"must not
invent the underlying frequency by tuning for acceptable gameplay"*
(`docs/design/m4-closure-audit.md:551-555`, **DIRECTOR-STATED, relayed, UNVERIFIED as a repository
document**).
*This lane's addition, INFERRED:* under A, improving the economy mechanically requires hazards to
grow, because the event must still exhaust a larger buffer; §7.2's hazard/vulnerability split is
well-defined only under B. **Not reconciled. This is an architectural difference, not a tuning one.**

**X-04 — CR-016 option 2 points at the frequency; the chain says the frequency is the one thing that
must not be moved for outcome.**
*Source A:* `docs/adr/cr-016-armed-disaster-fallout.md:130-152` (OPEN): option 2 is *"re-derive λ
against the demographic kernel, not against the historical reference class alone"*; the CR itself
notes *"Either λ or `starvationMortalityMaxPerYear` is denominated against a different reference than
the other."*
*Source B:* the chain's *"must not invent the underlying frequency by tuning for acceptable
gameplay"* (as above), and CR-015 §6.5's own *"No Libur fit"* (secondary F-151).
*Note:* first surfaced by `docs/design/m4-closure-audit.md:589-599` (secondary); recorded here because
it lands squarely on Part 16's design and because a resilience architecture that assumed either
reading would be building on an unsettled foundation. **Not reconciled.**

**X-05 — The mandate's "Happiness is a FOUNDATION" decision has no locus in the tree.**
*Source A:* the 2026-09-19 mandate, PART 17 (this lane's task text), which instructs that the decision
be **preserved** and its source **cited**.
*Source B:* MEASURED absence across `docs/`, `docs/adr/` and `Sim.Core/State/SettlementHappiness.cs`
(this lane's own greps, agreeing with recovery-lane C-07).
*Disposition:* §12 records the four citable lines that are the nearest thing to it and **preserves the
reading**, per the D-035 precedent that an uncited ruling is **queried, not reconstructed**. **Not
reconciled.**

**X-06 — Rising expectations is a positive feedback whose ratified brake lands three milestones
later.**
*Source A:* D-021's paired-feedback rule — *"Every positive feedback loop in the design must ship
with at least one negative feedback loop that strengthens with amplitude"* (G4, `d021:8`), and the
landing schedule putting the **Endurance** valve at **M8** (secondary F-22, `d021:47-50`).
*Source B:* D-018 §4's rising expectations, whose ratified and *desired* consequence is *"development
raises unrest potential before satisfying it"* (G30, `d018:48`).
*This lane's addition, INFERRED:* whichever milestone implements rising expectations owes a brake in
the **same** milestone, and the ratified brake is scheduled for M8. **Not reconciled.**

**X-07 — The observability taxonomy the mandate names has four kinds; the shipped one has five plus
GAP.**
*Source A:* this lane's task text: *"THE OBSERVABILITY TAXONOMY (RATIFIED, docs/observability-
architecture.md §0): READ / RECOMPUTED / DERIVED / GAP."*
*Source B:* MEASURED (this lane, `docs/observability-architecture.md:16-33`): *"exactly five
things"* — **READ · SUMMED · DIFFERENCED · RESIDUAL · RECOMPUTED** — plus **GAP** for anything needing
a formula the simulation does not expose. **"DERIVED" is not one of the five**; it is the tree's word
for a *reading* (`FoodState`, `SettlementHappiness`), which is a different concept.
*Why it matters here:* §9.4 places a forecasting readout in this taxonomy, and placing it under a
four-kind reading would mis-state which rule it must satisfy. Recorded as a factual correction, not a
disagreement. **Not reconciled** (this lane cannot amend a mandate).

**X-08 — Preservation, read physically, first makes the model stricter; the record has no decision
about whether the model wants that.**
*Source A:* G23 — livestock and fish are unbounded and imperishable, and *"no document or comment in
the repository ever decided that they should be"*; `GoodEntry` cannot express perishability.
*Source B:* the mandate's Part 12 progression, which places **preservation** between improved
granaries and advanced storage — a sequence that presupposes perishable animal goods.
*Disposition:* §5.3. **Not reconciled**, and it is Q-01 under another name.

---

## §18 DIRECTOR QUESTIONS

Stated as questions. No preferred answer is attached to any of them. Questions already carried by the
recovery lane are cross-referenced rather than restated as new.

**Part 12 — food system evolution**

- **DQ-J1.** Should the granary's carrier be made real — i.e. should the capacity coefficient T2 read
  the `StructureRow` count the player already builds — given that doing so moves the binding
  constraint of the whole food economy and meets the quarantined Malthus corridors? *(X-01, X-02;
  related to recovery-lane Q-06, which asks which enrichment item follows the base layer.)*
- **DQ-J2.** Which of the five store terms (T1…T5) is the intended home of "storage technology"? The
  enrichment list names *a spoilage multiplier* and *a built structure* together (G35); T1 and T2 are
  physically different technologies and this lane found no ruling that they move together.
- **DQ-J3.** Does the project want preservation at all, given that a physically honest preservation
  arc requires first making livestock and fish perishable — a large negative shock to every
  settlement in every world? *(§5.3, X-08; this is recovery-lane Q-01/Q-02 seen from the technology
  side. A ruling on Q-01 answers this; a ruling on this does not answer Q-01.)*
- **DQ-J4.** Is seed corn — *"a reservation, not a loss… it makes a bad year compound into the next
  one"* — a mechanism the project wants, given it would be the first food mechanism with memory across
  turns? *(recovery-lane Q-07, restated here because §5.4 finds it is also the first food loop needing
  its own D-021 brake.)*
- **DQ-J5.** May a food good ever be traded? Every Part 12 stage beyond advanced storage, and the
  whole of §9.5's resilience link, is unreachable while the answer is no. *(recovery-lane Q-03.)*

**Part 16 — disaster resilience**

- **DQ-K1.** Does the disaster's magnitude derive from a hazard catalogue, or from the effective
  buffer it must exhaust? *(X-03. The answer determines whether §7.2's hazard/vulnerability split is
  even well-defined, and whether improving storage makes hazards larger.)*
- **DQ-K2.** May there be a second disaster kind, and may a kind touch **stores** rather than
  production? A flood is the natural first kind and a granary loss is the natural first store-touching
  effect; the "granary loss" kind is currently NOT AUTHORIZED, with its price stated (G37).
  *(recovery-lane Q-11.)*
- **DQ-K3.** Is CR-016 option 2 re-pointed at the mortality kernel, kept as written, or dropped in
  favour of option 3 alone? *(X-04; first put by the closure audit. Recorded again because the whole of
  Part 16 sits on top of it.)*
- **DQ-K4.** Is "Exit is the only open valve under famine" the intended architecture, or an artefact
  of the missing food-trade capability? *(§9.5. D-021 requires at least one channel always open; with
  no food trade and no institutions, Exit is the only one, and that is a structural fact rather than a
  design choice anyone made.)*
- **DQ-K5.** May a technology reduce a settlement's **exposure** (its hazard rate), as opposed to its
  vulnerability — and if so, what bounds it away from zero? *(§9.3. The question is whether "science
  and technology must not simply erase disasters" is a statement about severity only, or about
  frequency too.)*

**Part 17 — evolving human needs**

- **DQ-L1.** Is the "M4 Happiness is a FOUNDATION, not the permanent definition of human needs"
  decision (a) the four citable lines §12 records, or (b) a separate ruling made outside the
  repository and never filed? *(X-05. Per the D-035 precedent this lane queries rather than
  reconstructs.)*
- **DQ-L2.** What does the expectation baseline drift toward — instantaneous satisfaction, a smoothed
  record, a class adoption fraction, or generation turnover? *(§13.2. The answer determines whether a
  new serialized row type exists, which is a schema question and therefore not this lane's.)*
- **DQ-L3.** Must basket composition become computed state for the satisfier set to evolve, and if so
  what preserves the ratified invariant that a class's food entries sum to exactly 1.0 by
  construction? *(§13.4, OWED-L2.)*
- **DQ-L4.** Which milestone publishes literacy, urbanization and media exposure — the three operands
  D-018 §4's rising expectations is ratified to read, and which do not exist? *(§15, OWED-L1;
  recovery-lane Q-18 asks the adjacent question of which milestone gives needs the supply curves
  habituation needs.)*
- **DQ-L5.** If rising expectations lands before M8, what discharges its D-021 paired-feedback
  obligation, given the ratified brake (the Endurance valve) is scheduled for M8? *(X-06.)*
- **DQ-L6.** May happiness read the needs aggregate? *(recovery-lane Q-17, restated because §14's
  first fence — that nothing in Part L reaches happiness — is provisional on the answer being "no",
  and every evolving-needs mechanism designed here is invisible to the player while it stays "no".)*

---

## §19 CAVEATS ON THIS DOCUMENT

1. **Everything in §§2–15 marked PROPOSED is this lane's design and carries no authority.** Most of
   this document is PROPOSED. That is expected and is stated honestly rather than dressed as
   recommendation.
2. **This lane verified, against source at `7fb84eb`, every RATIFIED fact it leans on** — the G-rows of
   §1 are that verification, and each names the file and lines read. Facts taken from the recovery
   lanes or from sibling architecture lanes without an independent source read are labelled
   **secondary** at the point of use.
3. **Two load-bearing sources are themselves qualified, and this lane did not treat them as clean.**
   `docs/capability-architecture-decision.md` carries a CORRECTION NOTICE falsifying two of its own
   claims and its adversarial pass returned **SURVIVES_WITH_CONDITIONS** (G11); its §4 recommendation
   is treated as **UNVERIFIED** wherever cited, and §4 of this document uses only the latch
   *correction* and the flip-cost storage rule, not the model recommendation. The Director-stated
   hazard chain (§8, §10) is **relayed and UNVERIFIED as a repository document** by the lane that
   recorded it.
4. **No measurement was run by this lane.** The MEASURED rows here are (a) greps and file reads
   performed this pass, explicitly attributed, or (b) figures quoted from the record that measured
   them, with that record named. No simulation was built, run or benchmarked; no test was executed.
   G20, G24 and the λ = 0.01 fallout figures describe worlds measured by other packets, and the
   λ = 0.01 figures **do not describe the shipped world**, which ships at λ = 0.0.
5. **No status was upgraded, downgraded or reconciled.** Where the code behaves as though a matter
   were settled and a document says it awaits a ruling, this document says OPEN.
6. **This lane did not read every relevant document in full.** It read the food/disaster/needs and
   climate/environment recovery lanes, the closure audit, the capability decision record, the sibling
   architecture lanes C/D/E/F-G-H in the sections bearing on food, disaster and needs, and the source
   files cited in §1. It did **not** read `cr-015`, `adr-024`, `adr-026`, `cr-003` or
   `m4-exit-inventory.md` end to end; claims drawn from them are cited through the recovery lane and
   labelled secondary. A reader relying on any such claim should verify it at source.
7. **Scope discipline.** No M5 code, no schema change, no production implementation, no merge, and no
   edit to any existing document. The three parts were designed together because they share the food
   balance, the store and the needs structure; they are not proposed as one system, and §2.5 states
   the conformance test against the `CapabilitySystem` rejection that this lane holds itself to.
