# CR-016 — ARMING THE FAMINE-CLASS DISASTER BREAKS THREE FROZEN GATES

**Status: OPEN — ESCALATED TO THE DIRECTOR. Nothing is fixed here.**
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
