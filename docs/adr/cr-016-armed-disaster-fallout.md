# CR-016 — ARMING THE FAMINE-CLASS DISASTER BREAKS THREE FROZEN GATES

**Status: OPEN — ESCALATED TO THE DIRECTOR. Nothing is fixed here.**
**2026-09-18, T4.21-7: the orchestrator's decision is recorded at the FOOT of this file — the
disaster mechanism SHIPS COMPLETE AND TESTED BUT INERT (`hazardPerYear` back to 0.0) and the RATE
is the director's ruling. That settles the TREE, not this CR. §1–§5 below are unedited and remain
the measured evidence; the measurements in them were taken AT λ = 0.01, which is NOT the shipping
value.**
Raised by T4.21-4 (`t4.21-4-arm`, cut from `claude/civdemo-work-b1z2y4` @ `8f7f9da`) under
CLAUDE.md's STOP rule: *"If implementation reveals a genuine conflict between frozen items, STOP and
write `docs/adr/cr-NNN.md`."* Every number below was MEASURED by the agent raising this CR on the
packet tree (ADR-015 §6); the raw rows are `docs/t4.21-evidence/t4.21-4/*.csv` and the narrative
record is `docs/t4.21-4-record.md`.

**This is not a request to change the ruling.** CR-015 is RULED, the λ = 0.01 derivation is the
`disaster._doc`'s and is dimensional rather than fitted, and T4.21-4 executed the arming exactly as
specified. What is escalated is that the ruled change collides with gates that are themselves frozen,
and the packet's own governance (RULE A: *corridor bands are not touched; if you believe one should
move, that is a finding for the director, not an edit*) forbids the agent from resolving the
collision.

---

## 1. THE FROZEN ITEMS IN CONFLICT

| # | frozen item | where |
| --- | --- | --- |
| A | **CR-015 §3.3 + the mandate**: famine is exceptional, caused by a famine-class disaster at λ = 0.01/settlement/year, severity 0.75–1.0 over 5 years, sized by derivation to exhaust a full effective buffer alone at dt 10. RULED. | `cr-015-famine-is-exceptional.md` §3.3; `sim.json` `disaster._doc` |
| B | **CR-001's permanent dt-continuity detonator**: *"This test exists forever."* Windowed growth either side of the Neolithic→Bronze dt flip must agree within 0.1 per 1000 yr. | `DemographyRetuneTests.EraBoundaryContinuity_NeolithicToBronze_PermanentDetonator`; `CalibrationBatteryTests.Canonical_EraBoundaryContinuity_PermanentBatteryMember` |
| C | **`corridors.json` canonical bands**, held immovable by CR-003 §5.4, m4-exit-inventory §12 and CR-015 N7, and by this packet's RULE A. | `corridors.json`; `CalibrationBatteryTests` |
| D | **G8 / §2 F4, ACCEPTED AS INHERITED**: the famine classification is dt-dependent because the food balance pools a whole turn's surplus; pinned as a DIFFERENCE, never asserted equal. | spec §10 G8, §2 F4; `D_Classification_DtDifference_Pinned` |

A and B collide. A and C collide. D is the MECHANISM by which A collides with B, and it was accepted
as an artefact at a time when λ = 0 made it unobservable.

---

## 2. EVIDENCE

### 2.1 The dt artefact is no longer inert — it is the dominant term at the era gate

A 5-year failure at severity `s` costs the same total food at every dt (the system is dt-exact and
`D_DtExact_FiveYearFailure` pins it). What is NOT dt-invariant is the DEFICIT it produces, because
the turn multiplier is `1 − s·min(D, dt)/dt`:

| dt | multiplier at s = 0.875 | a ρ = 2 settlement's struck-turn output | its deficit |
| --- | --- | --- | --- |
| 10 | 0.5625 | 1.125 × demand | **0** (absorbed) |
| 5 | 0.125 | 0.25 × demand | **0.75** before stores |

So the SAME event is absorbed at dt 10 and catastrophic at dt 5 — and the canonical era table steps
dt 10 → 5 at turn 250. Starvation mortality is `1 − e^{−0.12·d·dt}`, so the deficit's depth, not the
food lost, sets the deaths.

**Measured, canonical founded, seed 42, the CR-001 detonator's own windows:**

| window | growth |
| --- | --- |
| Neolithic, last 1000 sim-years (dt 10) | **+0.4352** per 1000 yr |
| Bronze, first 1000 sim-years (dt 5) | **−1.8220** per 1000 yr |
| discontinuity | **2.5259** against the 0.1 bar |

The pre-CR-001 kernel broke here by 4.1 (+0.7 flipping to −3.4) and the world died by year +2250.
The armed world's break is 2.53 and the sign is the same: the Bronze era is a dying era.

### 2.2 The canonical fed corridor

`canonical.fedGrowthPerYear` measured **0.000407442** at seed 1 against the band **[0.0005, 0.001]**.
Below the floor. Seed 2 fails on the density quarantine's lower edge (0.00355132 against a window of
[0.368574, 0.742110]) — an order of magnitude below, because the population that divides the arable
is gone.

### 2.3 The size of the change, canonical, at 300 turns

Final population, armed ÷ λ = 0, same seed, 20 seeds: median **0.26**. Arming removes roughly three
quarters of the world's turn-300 population. Total across the 20 seeds: 211,585 armed against
760,965 unarmed.

Why: 9.5 % of settlement-decades take an onset; a struck FAMINE turn's mortality is
`1 − e^{−0.12·d·10}` (38 % at d = 0.4), while the fed demographic clock grows at +0.076 %/yr =
0.76 %/decade. `0.905 × 0.0076 + 0.095 × (−0.38) ≈ −2.9 %` per decade — **net negative by
construction**, before any migration or fertility effect. The hazard and the mortality kernel were
derived independently and their product was never checked.

### 2.4 The dev world is extinguished

Dev preset (256², N = 4), 1000 turns:

| seed | arm | starvation | crashes (0.20) | peak | final |
| --- | --- | --- | --- | --- | --- |
| 42 | λ = 0 | 0 | 0 | 93,910 | 93,910 |
| 42 | λ = 0.01 | 5,983 | 2 | 4,722 | **38** |
| 7 | λ = 0 | 0 | 0 | 123,600 | 123,600 |
| 7 | λ = 0.01 | 6,414 | 1 | 4,165 | **62** |

And the CANONICAL 1024² DRIVEN world — the one the goldens and the observability suite run on — goes
the same way over 300 turns: **6,373 → 147 people** at turn 300 (`ObservedWorlds.Driven300`,
re-measured). The founded world holds at 10,974 against 40,539, so the collapse is not universal at
300 turns; it is the LONGER horizons and the DRIVEN world that die.

### 2.5 The dev migration corridor reverses direction

`dev.migrationGrossPerDecade`, dev preset, 1000 turns: seed 42 **8.34E-05 → 0.0148**, seed 7
**1.02E-04 → 0.0158** (λ = 0 twin → armed, same runs). The T3.4c quarantine recorded a world 20–60 %
BELOW the floor 0.001; the armed world sits **1.5× above the CEILING 0.01**. The quarantine's upward
tooth was written on the assumption that the only way up was back into the band, and T4.21-4 had to
split it so it would stop reporting "RESOLVED — back inside the corridor" for a world that is
nowhere near the corridor. No band moved.

---

## 3. WHAT IS *NOT* IN CONFLICT (so nobody reads more into this than is here)

- The onset process is CORRECT and measured: 6,211 onsets over 66,000 canonical settlement-turns =
  0.941061 per settlement-century, inside the binomial 99 % band [0.922204, 0.981047] around the
  truncation-corrected 0.9516258, at z = −0.925.
- The ladder is CORRECT: 0 of 1,540 FAMINE settlement-turns carry `FamineReason.None`; 0 starvation
  deaths on every turn with nothing above STRESS; λ = 0 produces ZERO famine while still producing
  352 STRESS and 1 SEVERE settlement-turns.
- Determinism is intact with disasters live: twin, replay-from-log and save/load-continue with an
  active `DisasterRow` are all hash-identical every turn, each with a guard that a disaster fired.
- The arming is BIT-ATTRIBUTABLE: every layout control returns its constant byte for byte on the
  λ = 0 twin, so the tree minus the arming is bit-identical to `8f7f9da`.

The mechanism does what CR-015 says. The quarrel is with its MAGNITUDE against the demographic
kernel and with the dt artefact G8 accepted while it was invisible.

---

## 4. OPTIONS (≤ 3, minimal, none taken)

1. **Rule the fallout ACCEPTED and re-read the gates.** Declare that the armed world is the world,
   re-derive `canonical.fedGrowthPerYear` and the Malthus teeth against it, and amend CR-001's
   detonator to compare LIKE WITH LIKE across the gate (e.g. a λ = 0 twin, or famine-excluded
   windows). *Blast radius:* two corridor bands, the CR-001 detonator's definition, the CR-003
   Malthus teeth. *Cost:* the project loses a gate that has caught real dt defects twice, or gains a
   twin-based version of it that no longer watches the shipped world.
2. **Re-derive λ against the demographic kernel, not against the historical reference class alone.**
   The reference class gives 1/50–1/150 per settlement-year for a REGION's famines; §2.3 shows that
   at this mortality kernel the same number is a net-extinction driver. Either λ or
   `starvationMortalityMaxPerYear` is denominated against a different reference than the other.
   *Blast radius:* one TUNE value; all goldens move again; the derivation in `disaster._doc` needs a
   second input (the recovery rate the event must not outrun). *Cost:* re-opens a ruled derivation.
3. **Close G8 instead of accepting it** — make the classification dt-invariant by giving the food
   balance a per-year sub-step (already queued for M5) so a 5-year failure produces the same deficit
   PATH at dt 10 and dt 5. *Blast radius:* the food balance, i.e. the largest of the three.
   *Cost:* an M5-sized packet; but it is the only option that removes the CAUSE of §2.1 rather than
   re-reading the gate that detects it.

**Recommendation: 3 for the cause, 2 for the magnitude, and NEITHER without the director.** Option 1
is the one the agent can see being taken by default and is the one it recommends against: the
detonator in §2.1 is not wrong, it is working.

---

## 5. WHAT T4.21-4 LEFT RED, DELIBERATELY

| test | why | classification |
| --- | --- | --- |
| `CalibrationBatteryTests.Canonical_FedCorridors_AllInBand(1)` | `fedGrowthPerYear` below the band (§2.2) | NEW, escalated — RULE A forbids moving the band |
| `CalibrationBatteryTests.Canonical_FedCorridors_AllInBand(2)` | density below the recorded quarantine window (§2.2) | NEW, escalated |
| `CalibrationBatteryTests.Canonical_EraBoundaryContinuity_PermanentBatteryMember` | the CR-001 detonator (§2.1) | NEW, escalated |
| `DemographyRetuneTests.EraBoundaryContinuity_NeolithicToBronze_PermanentDetonator` | the same, on its own rig | NEW, escalated |
| `CalibrationBatteryTests.Dev_MalthusCorridors_AllInBand(42)` and `(7)` | CR-003's `starvedTotal == 0`, `crashes == 0` and `peak == final` teeth (§2.4). CR-015's dispositions authorise RE-READING the `starvedTotal == 0` tooth, but re-aiming all three onto a cause-attributed form is a change to the CR-003 quarantine's substance, which is the director's | NEW, escalated; seed 7 was ALREADY red on `8f7f9da` for an unrelated reason (the migration drift tooth), which T4.21-4 re-pinned |

Nothing else in the suite is red. The packet's own governance is in
`docs/t4.21-4-record.md` §4–§5.

---

## ORCHESTRATOR DECISION (2026-09-18) — the mechanism ships inert; the rate is the director's

**Nothing above this line is edited.** §1–§5 are the T4.21-4 measurement and stand as the evidence
for this CR. This section records what was DONE about them, by T4.21-7 (the disarm-and-settle lane,
working directly on `claude/civdemo-work-b1z2y4` @ `ee27c17`), and what the director is being asked
to rule. **This decision does not resolve this CR.** It puts the mechanism in the only state that is
honest while the CR is open, and it is reversible by one data edit.

### D.1 The decision

**THE DISASTER MECHANISM SHIPS COMPLETE AND TESTED BUT INERT: `hazardPerYear` returns to 0.0, and
the RATE becomes the director's ruling on CR-016.** The reasoning, recorded verbatim as it was
given:

1. the mandate's own rule — "IF a proposed change fixes one pathology by creating another THEN
   reject it" — and a world that dies by construction, plus a broken PERMANENT dt-invariance
   detonator, is a worse pathology than the one T4.21 fixed;
2. choosing a lower rate that makes the world survive would be tuning-to-outcome, which CR-015 §6.5
   forbids ("no constant moved ... no Libur fit") — the honest act is to decline to ship an
   unvalidated calibration, not to invent a validated-looking one;
3. the mechanism is PROVEN by forced-strike rigs, the ladder tests and the determinism legs, so
   mandate item 1(A) (a famine-class natural disaster) is IMPLEMENTED and reachable — only its RATE
   is unset;
4. mandate item 1(B) (deliberate abandonment) is live and unaffected, so famine remains reachable in
   play today;
5. the collision's root cause is the inherited G8/F4 artefact the director already acknowledged as
   inherited — its consequence was simply invisible while lambda = 0, and CR-016's option 3 (the
   queued per-year food-balance sub-step) is the real fix, which is M5-scale.

### D.2 The exact shipping state

| item | shipped | note |
| --- | --- | --- |
| `sim.json` `disaster.hazardPerYear` | **0.0** | was 0.01 from T4.21-4's arming commit `f44d5cc` |
| `disaster.durationYears` | **5.0** | UNCHANGED |
| `disaster.severityMin` / `severityMax` | **0.75** / **1.0** | UNCHANGED — the BAND is not what this CR disputes |
| the `disaster._doc` derivation and reference class | UNCHANGED | the `_doc` now states the sequence (shipped at 0 → armed at 0.01 and measured → returned to 0 pending this ruling) instead of reading as a plain "ARMED" |
| `corridors.json` | UNCHANGED, byte-identical to `8f7f9da` | no band moved at any point in T4.21-4 or T4.21-7 |
| `DisasterSystem`, `DisasterRow` (v25), `FoodState.struck`, the chronicle/telemetry/forensic surface | SHIPPED AND TESTED | code, schema and observability are exactly what T4.21-1 / -4 / -5 built |

**The mechanism is still PROVEN, by live tests that arm λ in their own rigs** (T4.21-7 step 3 — a
claim exercised only by a dead test is a finding, and the fix is to give the test its own hazard,
never to re-arm the shipped value). Named:

- *a disaster CAN cause famine* — `FamineScenarioTests.S_Disaster_TriggersFamine` (forced strike,
  λ = 1e6 in-rig), `S_Seed42_NoFamineWithoutCause` and
  `S_TwentySeeds_FamineFrequencyMatchesHazard` (λ = 0.01 in-rig via `ArmedRig()`),
  `DisasterSystemTests.D_HazardInfinite_EveryoneStruck`, `D_Classification_DtDifference_Pinned`,
  `FoodStateTests.F_Stockpile_DecidesFamine` / `F_DisasterTiming_TurnExact`.
- *an ordinary bad harvest CANNOT* — `FamineScenarioTests.S_WeatherOnly_NeverFamine` (λ = 0 in-rig,
  with its own non-vacuity guard that the weather still BITES).
- *abandonment CAN* — `FamineScenarioTests.S_Abandonment_TriggersFamine`, on the **shipped** config,
  deliberately: it is the cause reachable in play today (reason 4 above).
- *λ = 0 yields zero famine while still yielding STRESS* — `S_WeatherOnly_NeverFamine`, and
  `DisasterSystemTests.D_HazardZero_StripControl` for the byte-level strip identity.
- *the onset process matches its hazard, and the ladder holds* —
  `S_TwentySeeds_FamineFrequencyMatchesHazard` (binomial band computed from the rig's own λ),
  `D_OnsetProcess_BiasesWithinStatement` (λ = 0.01 in-rig).
- *determinism with disasters live* — `S_Determinism_TwinIdentical_WithDisastersLive`,
  `S_Determinism_ReplayReproducesRun_WithDisastersLive`,
  `S_Determinism_SaveLoadContinue_WithAnActiveDisasterRow`, all three λ = 0.01 in-rig, each with a
  guard that a disaster actually fired.
- *the forensic/chronicle surface reports a real strike* —
  `InspectionTests.T421_TheFamineDisasterAbandonmentAndRefusalLines_MatchTheRecordBothWays`
  (λ = 0.01 in-rig; its two vacuity guards are what would otherwise have gone quietly dead).

### D.3 What is reversible with ONE data edit

`Sim.Data/content/sim.json` → `disaster.hazardPerYear`. Nothing else. That arming is a DATA change
and no code path of its own, which is itself pinned by `SimConfigTests.DisasterHazard_Armed_Loads`
(the derived 0.01 still loads through the shipped loader today) and by
`SimConfigTests.ShippedDisaster_IsInert_AndTheBandIsTheDerivedOne` (the shipped 0.0 and the
untouched derived band, asserted together).

What the edit costs, measured, so the director can price it: it moves all four behavioural world
goldens and `ci.yml`'s `FOUNDED_GOLDEN` back to T4.21-4's armed values, re-pins the dev-migration
recorded envelope a third time, re-resolves the two CR-003 quarantines that T4.21-7 re-instated, and
re-opens the six reds of §5. Every one of those is recorded here and in `docs/t4.21-4-record.md`
with its old and new value, so the round trip is bookkeeping, not re-derivation.

**And it re-aims six `SimConfigTests`, which this cost list previously did not mention (T4.21-8
finding 2, measured on a worktree pinned to `8b59bab` with `hazardPerYear` = 0.01 and the T4.21-8
anchor fix applied: 6 failed / 31 passed of 37, against 37 / 37 on the shipped tree).** "Nothing
else" above is true of the DATA — one key, one value. It was never true of the tests that
*substitute into* that key, and the omission mattered:

| test | why the one data edit re-aims it |
| --- | --- |
| `ShippedDisaster_IsInert_AndTheBandIsTheDerivedOne` | **BY DESIGN — this is the tripwire.** It asserts the shipped 0.0 and the untouched derived band together; ruling a rate is exactly what must trip it. |
| `DisasterHazard_Armed_Loads` | its substitution anchors on the shipped literal, so on a re-armed tree there is nothing left to substitute |
| `HazardAnchor_IsValueExact_NotAPrefix` | same anchor (added by T4.21-8, below) |
| `DisasterHazard_Negative_RefusesLoad` (×2 cases) | same anchor |
| `DisasterSection_Missing_RefusesLoad` | same anchor |

These five were ALWAYS re-aimed by the edit. Before T4.21-8 two of them merely hid it: the anchor
was the comma-less `"hazardPerYear": 0.0`, which is a **PREFIX** of `"hazardPerYear": 0.01`, so on a
re-armed tree the substitution matched inside the armed value and produced `0.011`. Measured on the
armed tree before the fix: `DisasterHazard_Armed_Loads` failed with `Expected: 0.01 / Actual:
0.010999999999999999` — the test that exists to prove *the derived 0.01 still loads* was asserting
against 0.011; and `DisasterHazard_Negative_RefusesLoad` PASSED, but on `-0.011` / `NaN1`, not on
the values it names. Nothing was silently wrong on the SHIPPED tree, but the trap was live: the
tempting repair is the expected VALUE, and taking it makes the test stop testing what it names
forever.

T4.21-8 anchors all of them on `SimConfigTests.HazardAnchor` = `"hazardPerYear": 0.0,` **with its
trailing comma**, so the prefix collision cannot arise at whichever value ships, and routes every
one through `AssertAnchorMatched`, which fails with the remedy in the message: *move the SEARCH
STRINGS, never the expected VALUE.* Whoever executes the director's ruling therefore gets six loud,
self-describing reds and no silent substitution. `HazardAnchor_IsValueExact_NotAPrefix` is the guard
that makes the defect visible on the SHIPPED tree, where it otherwise cannot be seen: it applies the
substitution to its own output, which a value-exact anchor leaves unchanged and a prefix anchor
compounds. Teeth measured — reverting `HazardAnchor` to the comma-less literal fails it, and it
alone, with `Actual: ···"hazardPerYear": 0.011···`.

### D.4 What the director is asked to rule

The three options in §4 are unchanged and none of them has been taken. Restated as the question:

1. **Rule the fallout ACCEPTED and re-read the gates** — declare the armed world the world, re-derive
   `canonical.fedGrowthPerYear` and the Malthus teeth against it, and amend CR-001's detonator to
   compare like with like. This is the option §4 recommends AGAINST, and T4.21-7 did not take it:
   the detonator in §2.1 is not wrong, it is working.
2. **Re-derive λ against the demographic kernel, not against the historical reference class alone** —
   λ and `starvationMortalityMaxPerYear` are denominated against different references and their
   product was never checked. This is the option that sets the RATE, and it is the one this decision
   defers to the director rather than guessing: any rate chosen here to make the world survive would
   be the Libur fit CR-015 §6.5 forbids.
3. **Close G8 instead of accepting it** — give the food balance a per-year sub-step so a 5-year
   failure produces the same deficit PATH at dt 10 and dt 5. **THIS IS THE REAL FIX**: it removes the
   CAUSE of §2.1 rather than re-reading the gate that detects it, and it is the only option that does.
   It is **M5-SCALE** — the food balance is the largest of the three blast radii — and it is already
   queued as G8(c) in `docs/queue.md` ("per-year food-balance sub-step"). Options 2 and 3 are not
   exclusive: 3 makes the deficit depth dt-invariant, after which 2's derivation has a stable target.

**Recommendation, unchanged from §4: 3 for the cause, 2 for the magnitude, and NEITHER without the
director.** Until then the mechanism ships inert and the rate is unset — stated, not hidden.

### D.5 The tree this decision leaves behind, MEASURED

By T4.21-7 on `claude/civdemo-work-b1z2y4`, sequential, in BOTH configurations:

```
scripts/check-banned-constructs.sh          exit 0
scripts/check-read-isolation.sh             exit 0
scripts/check-readonly-proof.sh             exit 0
dotnet build                                0 warnings, 0 errors
dotnet build -c Release                     0 warnings, 0 errors
dotnet test              Sim.Tests      884 passed / 0 failed / 4 skipped   (30 m 31 s)
                         Sim.Ui.Tests   296 passed / 0 failed / 0 skipped   (1 m 35 s)
dotnet test -c Release   Sim.Tests      884 passed / 0 failed / 4 skipped   ( 8 m  6 s)
                         Sim.Ui.Tests   296 passed / 0 failed / 0 skipped   (    33 s)
```

Release is the configuration the earlier T4.21 records and CI measure in, so it is reported
alongside Debug rather than instead of it; the two agree test for test.

**THE RED SET IS EMPTY, and that is the check on §5 rather than an assumption.** Every one of the
six reds §5 left deliberately — `Canonical_FedCorridors_AllInBand(1)` and `(2)`,
`Canonical_EraBoundaryContinuity_PermanentBatteryMember`,
`DemographyRetuneTests.EraBoundaryContinuity_NeolithicToBronze_PermanentDetonator`, and
`Dev_MalthusCorridors_AllInBand(42)` and `(7)` — passes at λ = 0. None needed chasing, which is
itself the evidence that the arming was their sole cause: §2's collision is a property of the RATE
against the mortality kernel and the G8/F4 dt artefact, not of anything else T4.21 built. The one
red INHERITED from `8f7f9da` (seed 7's migration drift tooth) is resolved deliberately by re-pinning
the recorded envelope to the measured λ = 0 values, with the cause named, like a golden. The 4 skips
are the four manual measurement rigs that have always been skipped; CR-015 N9's two lifts are not
re-skipped.

**What the disarming costs, stated rather than hidden** — each recorded at its own site with BOTH
readings and with this CR named as what decides it, and each reversed by the same one data edit:
two `Cr003Quarantine` guards return to quarantined (measured at λ = 0: 0 starvation over the 900-turn
dev reconciliation rig; 0 famine lines in 20 chronicle events); `WorldReconciliationTests`' founded
starvation and driven dwelling decay return to ABSENCE pins (zero on all 300 turns, measured);
`MigrationTests.MagnitudeCorridor_FedPhaseDrift_WithTeeth`'s upward rate-lever tooth is quarantined
in place. `docs/queue.md` carries all of them as open items.

### D.6 A correction to how §2.1's table reads — TWO RIGS, not one (T4.21-8, re-measured)

**§1–§5 are frozen as T4.21-4's evidence and are NOT edited by this paragraph.** What is corrected
here is the *reading* of §2.1's table, and the NEW prose T4.21-7 wrote from it (the shipped
`sim.json` `disaster._doc`), which carried "2.5259 per 1000 yr" forward beside the +0.4352 / −1.8220
pair as though all three came from one run. They do not. Under ADR-015 §6 a number in new prose must
have been measured by the agent writing it, so both rigs were re-measured, on a worktree pinned to
`8b59bab` with `disaster.hazardPerYear` = 0.01 and nothing else changed:

| rig | run | Neolithic (dt 10) | Bronze (dt 5) | discontinuity | bar |
| --- | --- | --- | --- | --- | --- |
| `CalibrationBatteryTests.Canonical_EraBoundaryContinuity_PermanentBatteryMember` | `RunCanonical(seed **1**, 650 turns)`, windows 1600–2500 / 2500–3400 | **+0.4352** | **−1.8220** | **2.2572** (2.257227 measured directly, not by addition) | 0.1 |
| `DemographyRetuneTests.EraBoundaryContinuity_NeolithicToBronze_PermanentDetonator` | `ProductionExecutor`, `Founded(cfg)`, turns 1..450, 1000-yr windows either side of turn 250 | **+0.7616** | **−1.7643** | **2.5259** | 0.1 |

So §2.1's table pairs the BATTERY rig's two component readings with the DEMOGRAPHYRETUNE rig's
difference. Its heading also says "seed 42"; the battery rig those components come from runs at
**seed 1**. Both figures are real, both were re-measured above, and both are ~23× and ~25× the 0.1
bar — **no conclusion in this CR changes, and the decision in D.1 is untouched.** Re-measurable in
one command on a tree with `hazardPerYear` = 0.01:

```
dotnet test -c Release --filter "FullyQualifiedName~Canonical_EraBoundaryContinuity_PermanentBatteryMember|FullyQualifiedName~EraBoundaryContinuity_NeolithicToBronze_PermanentDetonator"
```

`sim.json`'s `disaster._doc` now carries both rigs' figures with their rigs named, rather than one
number attributed to the other's windows.

**The same pairing propagated into four other T4.21-4-era records, and they are DELIBERATELY LEFT
ALONE** — `gov-4` §6: *never rewrite a frozen document because a later decision supersedes part of
it; record the ambiguity and leave the document alone.* Every one of them is a measurement record of
what the world does at λ = 0.01, and rewriting them would destroy the provenance the freeze exists
to protect. **This paragraph IS that record — it is the single correction all four should be read
through:**

| site | what it says | status |
| --- | --- | --- |
| `docs/t4.21-4-record.md` §5 | "discontinuity **2.5259** per 1000 yr … : +0.4352 before, **−1.8220** after" | the conflated pair — FROZEN record, `docs/t4.21-4-record.md`'s own header says "nothing in this file was rewritten"; read via this table |
| `docs/t4.21-4-record.md` §5 red table | `Canonical_EraBoundaryContinuity_PermanentBatteryMember` — "the CR-001 detonator, 2.5259 per 1000 yr" | attributes the DemographyRetune rig's figure to the BATTERY test by name; that test's own discontinuity is **2.2572**. FROZEN; read via this table |
| `docs/queue.md` entry T4.21-4 | "breaks … by 2.5259 per 1000 yr (+0.4352 before, −1.8220 after)" | the conflated pair; T4.21-4's escalation entry, left as written |
| `docs/adr/adr-024…` §disaster | "breaks at the era gate by 2.5259 per 1000 yr (+0.4352 before, −1.8220 after)" | the conflated pair, left as written |

Two further sites give **2.5259 alone**, with no seed and no window attached — `docs/adr/cr-015…`
and `docs/current-state.md`. Those are **CORRECT as written** for the DemographyRetune rig and need
nothing; they are listed so a reader checking this table does not think they were missed.

**Only NEW prose was corrected**, which is precisely what ADR-015 §6 governs: the shipped
`sim.json` `disaster._doc` (T4.21-7-authored) and this section (T4.21-8-authored). No number in any
frozen record moved, and the one that mattered — *is the detonator broken?* — is answered the same
way by both rigs and by every one of these six sites: **yes, by ~23–25× the bar.**
