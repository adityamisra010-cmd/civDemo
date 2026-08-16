# CR-004 — MALTHUSIAN FEEDBACK ARCHITECTURE: KEEP / SIMPLIFY / REPLACE

**Status: DRAFT, awaiting director ruling. No code changed.** Written per CLAUDE.md governance: the
director's population-architecture direction (this session) asks me to evaluate simplifying or
replacing the demographic engine, but Demographics/Consumption are the exact surface T4.2 certified
and every packet this session has explicitly called **frozen**. A SIMPLIFY or REPLACE ruling would
authorize touching a frozen contract — CLAUDE.md requires that go through a CR with director
sign-off before implementation, not be coded inline off a design direction. This is that CR.

## 1. FROZEN ITEMS IN TENSION

- "T4.2 remains frozen" (stated explicitly in every T4.10 packet this session).
- CLAUDE.md governance: "The kernel is frozen after M0 acceptance without director sign-off,"
  and "if implementation reveals a genuine conflict between frozen items, STOP and write
  `docs/adr/cr-NNN.md`."
- The population-architecture direction asks to evaluate replacing/simplifying exactly the
  Demographics/Consumption/Housing/Migration coupling T4.2 built on and CR-003 already ruled on.

Nothing here is a defect in T4.2 — the bisection evidence below shows T4.2's mechanism behaves
exactly as designed. The tension is procedural: a KEEP/SIMPLIFY/REPLACE decision is an
architecture-level ruling, and CLAUDE.md's workflow puts that decision here, not in an inline code
edit.

## 2. EVIDENCE ASSEMBLED THIS SESSION

### 2a. Corridor bisection (density vs. migration have DIFFERENT causes — see prior hand-back)

- **Density drift is 77.8% T4.1-family (siting/endowment), 22.2% T4.2** (small, already
  self-measured by T4.2 as 0.9-2.9% population/flat arable).
- **Migration's collapse is 100% T4.2**, via a direct, intended mechanical path:
  `MigrationSystem.cs:138-142` reads `R_i` from `prev.GoodStocks[grain].Amount` — the exact stock
  field `ConsumptionSystem.BoundStore` (T4.2) drains via Spoilage/GranaryOverflow
  (`ConsumptionSystem.cs:233-293`). T4.1 alone moved migration in the *healthy* direction
  (mean 0.001533→0.001827, floor-breaches 1/20→0/20); T4.2 alone then collapsed it
  (0.001827→0.000352, floor-breaches 0/20→20/20).

**This is a calibration/coupling consequence of a certified mechanism working as designed** — not
evidence the demographic architecture itself is unstable or pathological. Migration's corridor needs
a coupling-aware recalibration decision (its own, narrower question), not an architecture rewrite.

### 2b. Malthusian feedback audit (full parallel investigation, this session, all read-only)

**Track A/D (food→fertility/mortality):** `ConsumptionSystem.cs:178-190` computes a deficit ratio
clamped [0,1]; `DemographicsSystem.cs:136,177` (fertility suppression, linear in deficit, with a
rebound-reservoir that *refunds* suppressed births later) and `DemographicsSystem.cs:273-278`
(starvation mortality, `MaxPerYear × deficit × cohortMultiplier`, also linear) both read **last
turn's** deficit (one-turn lag, by design). Both responses are linear in the deficit ratio — real,
active, correctly wired, but with **no compounding/duration/density term** beyond what the deficit
ratio itself carries turn to turn.

**Track B (labour→production→food):** A genuine hard ceiling exists —
`ratePerYear = min(arableKm2 × YieldPerArableKm2PerYear, farmLabor × OutputPerFarmerPerYear × toolFactor)`
(`ProductionSystem.cs:209-211`), Leontief, land-capped independent of population past that point.
**But CR-003 already measured this ceiling is selected in 0 of 1,630 turns** at every shipped yield
value (`cr-003.md:96-99`) — the ceiling exists in code and is completely inert in play.

**Track C (carrying-capacity inventory):** a real closed loop exists — land → harvest → grain
bounding (T4.2) → deficit → mortality/fertility — fully endogenous and active in the MalthusLite
scenario. But it is narrow: no disease/health system, no institutions/governance limits, and
**critically, no colonization/daughter-settlement mechanism** — settlement count is fixed forever at
genesis (`Worldgen/WorldFounding.cs:39`, confirmed zero `Settlements.Add` calls anywhere in
`Sim.Core/Systems`). Migration and starvation are the *only* two pressure valves; there is no
"found new land" valve.

**Track E (migration):** structurally proven **irrelevant** to the world-total Malthus metric —
migration is a pure Ledger.Transfer between existing settlements (`MigrationSystem.cs:333-335`,
conserved by construction), and `TotalPop` sums across every settlement (`PopulationTests.cs:35-39`)
— migration cannot add, remove, or trend world population. It can redistribute where a local crash
is visible, but cannot mask or explain the observed zero world-level crossings.

**Track F (dt/temporal resolution):** exponential-survival integration
(`DemographicsSystem.cs:204,207`) is dt-invariant and precludes a sharp within-step crash;
`TotalPop` is sampled every turn with no subsampling gap (`PopulationTests.cs:260-264`). **Cadence
does not explain the null result.**

**Track G (MalthusLite's own status):** the ≥2-crossings bar is an **inherited T2.7/M2-era
diagnostic threshold**, never stated as an architectural mandate — `Cr003Quarantine.cs:1-39` frames
it explicitly as detection machinery for a *contingent* phenomenon, and **CR-003's own ruling
(`cr-003.md:240-252`) states the current non-oscillating, pre-Malthusian output is CORRECT** given
the shipped constants: *"A FRONTIER SOCIETY IS NOT MALTHUSIAN... a pre-Malthusian world is the
CORRECT output of the corrected constants, not a defect in them."* CR-003 already named the missing
piece: colonization/land-clearance (Option 3, scheduled for M4, never yet built).

### 2c. The root cause, stated precisely

The demographic **mechanism** (deficit→fertility/mortality) is real, linear, correctly wired,
dt-robust, and not the pathology. The **ceiling** (land×yield Leontief cap) is real and correctly
wired but never binds, because **population growth is driven by an exogenous T2.7 demographic-vector
rate integrated over the era table** (~0.076%/yr fed growth), and that growth clock runs out (6,000
simulated years) long before the land ceiling — which requires yield ≤6.2 to ever bind, against a
shipped range of 15–152.7 — is ever approached. `cr-003.md:100-103`: *"The demographic clock runs
out before the land does."* This is **not** a coupling-instability problem (no oscillation,
collapse, or runaway was found anywhere in this audit) — it is the **opposite**: the system is
stably, monotonically pre-Malthusian because the exogenous growth-rate target is set far below what
the endogenous ceiling would allow.

## 3. OPTIONS

1. **KEEP.** The architecture is fundamentally sound: deficit→fertility/mortality is linear,
   dt-robust, correctly lagged, and migration is provably not confounding it. The land ceiling exists
   and works exactly as designed when it binds (it simply doesn't, at current constants). CR-003
   already ruled the correct fix is a **missing mechanism** (colonization/land clearance, or a lower
   yield calibration), not an architecture replacement. **This is the evidence-supported
   recommendation.**
2. **SIMPLIFY.** Not supported by this audit — no excessive/unstable coupling was found. Migration is
   already fully decoupled from world-total population (Track E). Housing is already *not* a direct
   mortality mechanism — this audit found **no Housing→Demographics link at all** (Housing feeds
   Grievance and settlement capacity/build-rate only; the director's stated design principle
   ("housing should not be a primary mortality mechanism") is **already how the code is built**, not
   a defect to fix).
3. **REPLACE.** Not supported. No pathological oscillation, runaway collapse, or artificial
   equilibrium was found anywhere in Tracks A–G. The observed "zero crossings" is a **calibration/
   missing-mechanism finding CR-003 already ruled on**, not evidence of architectural instability.
   Replacing the engine would discard a linear, dt-invariant, correctly-isolated mechanism to solve a
   problem this audit could not find.

## 4. RECOMMENDATION

**KEEP**, with two independent, narrow follow-ups — neither is an architecture change:

1. **Migration corridor** (this session's bisection): a coupling-aware recalibration decision — does
   `R_i` reading raw post-bounding grain stock (rather than e.g. a smoothed/pre-spoilage production
   signal) reflect the intended design, or should Migration's attractiveness term be re-derived now
   that T4.2 exists? This is a T4.6/Migration-scoped question, not a Demographics rewrite.
2. **MalthusLite** stays exactly as CR-003 left it (DEFERRED), per this session's ruling. CR-003's own
   prescribed path — colonization/land-clearance (Option 3) or a yield recalibration to ≤6.2 — is the
   ratified next step, already scheduled, not superseded by anything found in this audit.

**No blast radius from this recommendation**, because it changes nothing — that is the point of a
KEEP ruling: no code, no test, no config, no schema, no golden is touched by this CR.

---

## 5. DIRECTOR RULING

**RULING: KEEP.** The Malthusian architecture audit (§2 above) is accepted in full. Specifically
ratified as established fact, not merely this ADR's argument:

- Deficit → fertility/mortality is structurally sound.
- The mechanism is linear and dt-robust.
- Migration cannot affect the world-total population metric (proven by construction, Track E).
- The zero-crossings result is the already-ruled CR-003 finding, not a new defect.
- The missing pressure mechanism is colonization/land clearance (CR-003 Option 3), not a broken
  population engine.
- Housing is not used as a primary mortality mechanism in the current code (confirmed absent, not
  merely undesired) — the director's stated design principle is already how the system is built.

**NOT AUTHORIZED by this ruling:** any simplification or replacement of Demographics or Consumption.
T4.2 remains frozen and is not reopened by this ruling or by anything in this ADR. No demographic
architecture change of any kind is authorized.

---

**STATUS: RULED — KEEP.** CR-004 closed. No code changed; none is authorized by this ruling.
