# H-TR — Terrain in the TacticalResolver, deployment thirds, inactivity vs concealment, no pulse ceiling

M7 Phase C, lane TR. Branch `lane/TR` in a private clone (`/dev/shm/m7c/TR`), cut from `fc5230c` and **rebased by merge onto
dev `7118ef0`** (T3 two-action windows + lane N naval / air / combined arms / snipers). Everything lives in
`prototypes/m7-sprint1` (D-079 Q5 open: nothing in Sim.Core / Sim.Data / Sim.Cli / Sim.Tests / Sim.Ui). AutoResolver untouched
(lane P). Of the files lane T3 edits, `War/WarTheaterWorld.cs`, `Events/Runner.cs`, `Events/EventScheduler.cs` and
`War/ActionWindows.cs` are untouched by this lane.

**Provenance.** The lane was paused by the Director twice and resumed. At this (second) resume the RAM clone still existed;
the mutant scratch copy (`TR-mut`) was a separate tree, and a residue check of the clone (each of the 32 first-run mutant
definitions: original text present exactly once, mutated text absent) was clean. The work-in-progress was committed as is
(`092460f`), dev `7118ef0` was merged (`35f8b06`, conflicts resolved keeping both sides), the full suite was run on the
merge commit **before** any further change (§5.1), and the lane-N interplay was then fixed and tested (`4052510`, §5). The
resumed session ran **throttled** on the user's instruction ("Throttle by 90%"): `nice -n 19`, `-m:1` builds, one full-suite
run per mutant on a prioritized subset, a small bench. Every number in this report was measured on the final tree unless it
is explicitly marked as from the pre-merge tree (first run).

Status words: **MEASURED** (run on this tree, output quoted), **DERIVED** (hand-derived and pinned by a test), **INFERRED**
(argued, not isolated), **PROPOSAL** (a Director-reserved or not-yet-ratified choice — decided by nobody here),
**PLACEHOLDER** (a magnitude with a cited direction but no ratified value).

---

## 0. Summary

| Brief item | State | Evidence |
|---|---|---|
| (1) Terrain in the resolution equations: movement cost, continuous LOS with elevation / forest / buildings, cover vs concealment, charge and missile effectiveness (slope, rivers, marsh, forest), formation cohesion | Implemented, default ON — **except: rivers and marsh do not touch MISSILE effectiveness** (melee only; limitation 24, pinned by a test, [r1] §12); applies to land formations including troops landed in an amphibious battle; vessel / land observers and domain gunners use the same terrain sight | 22 tests (10 spatial, 12 tactical) with exact derived values + 1 domain-sight test; identity on flat open ground (Slice D golden trace unchanged); amphibious battle: the forest's missile transmission alone moves it (measured attribution, §5.4) |
| (2) Deployment thirds, middle third refused before battle, deterministic refusal, 20 m asymmetry kept | Implemented (ground formations; vessels, aircraft and embarked troops are not ground deployments). [r1] An edit is judged on the edited formation only (the whole-deployment check deadlocked); two SHIPPED scenarios (parity Ambush, lane N Amphibious) violate the thirds (they run Unrestricted); the default `Meeting` still carries the F1-corrected mirrored geometry (kept as the control) — §12 | 10 + 1 tests + 3 round-1 tests; `MeetingAsymmetric` keeps 20,000 mm through the phase and into the battle |
| (3) Inactivity vs concealment / defence / waiting, exact predicate, 4-vs-5 boundary | Implemented as the default tactical rule; integrated with the naval / air modules. [r1] A hidden formation MARCHING on an order is no longer judged stationary; "concealed" includes terrain on the enemy's sight line; the mutual guard is OFF by default (both sides rout, D-079 S304) — §12 | 22 + 7 tests: each exemption with its teeth control and exact boundary, the 4/5 boundary as a turn-exact order pin with ±1 neighbours, delivery pin for the explicit Hold, replay, and the five lane-N interplay items |
| (4) No implicit pulse ceiling; long battles end only by battle-end conditions | [r1] Long battles end only by DIR5's ends ([r2, V8: quote corrected] DIR5 §2.6: "dead, retreated, routed or surrendered" — in the engine, elimination: a side without Active entities, its members dead, escaped under a Retreat order, routed (routed escapes) or surrendered) **or by the lane's own, UNRATIFIED Standoff end** (whole-field idle — PROPOSAL P-TR1), without which visible-idle or all-exempt battles never end. Audited; counters saturating; proven by long runs; bare point fire excluded from progress (it was an unbounded activity source after the merge) | 3 long-battle tests + the bombardment test; mutant table §8.4 (a removed Standoff end is a HANG finding) |
| Tactical bench cost | Measured | §6 |
| Rebase onto `7118ef0` | Done; merge commit measured before further work | §5.1: 538 / 6 / 544 on the merge commit (both configurations); final §8.1 |

Test counts (MEASURED, `dotnet test prototypes/m7-sprint1/M7Proto.slnx`): `7118ef0` alone **487 / 0 / 0** in Debug and in
Release; tree before verification round 1 **554 / 0 / 0** (Debug and Release); **after verification round 1 569 / 0 / 0
(Debug) and 569 / 0 / 0 (Release)** — §8.1, §12; **after verification round 2 581 / 0 / 0 (Debug) and 581 / 0 / 0 (Release)**
— §13. Statements corrected by round 1 are marked **[r1]**, by round 2 **[r2]**; §12 and §13 have the per-finding verdicts.

---

## 1. Terrain (DIR5 §2.7)

DIR5 §2.7: "Hybrid grid pathfinding and continuous line of sight/projectile calculations. Fixed-point integers for battle
positions and kinematics. Terrain, concealment, forest cover, elevation and line of sight must materially influence
combat." The Slice C hybrid (integer A* on the grid + exact supercover LOS/fire on continuous mm positions, Q32/long only)
is kept; lane TR adds the terrain physics on top of it.

### 1.1 Terrain classes

Three classes are **appended** (codes 0–7 unchanged, so every Slice C hash is unchanged): `Marsh = 8`, `Ford = 9`
(fordable river or stream; a deep river stays `Water`, impassable to formations), `Building = 10` (impassable to formations,
opaque up to a finite roof height — unlike `Wall`, which stays infinitely tall). `TerrainCostTable.ClassCount` 8 → 11.
Placeholder costs: Marsh 3000, Ford 3000, Building impassable. Concealment placeholders: Marsh 1, Ford 1, Building 0.

### 1.2 The terrain table (`Spatial/TerrainEffects.cs`) — every value is a coefficient inside an equation

| Class | Feature (layer) height | Opaque | Missile transmission (cover) | Footing | Order cap (cohesion) |
|---|---:|:-:|---:|---:|---:|
| Road, Open | 0 | – | 1 | 1 | 1 |
| Forest | 15,000 mm | – | 3/4 | 1 | 1/2 |
| Jungle | 25,000 mm | – | 1/2 | 1 | 1/4 |
| Hills | 0 | – | 1 | 1 | 1 |
| Mountain | 0 | – | 1 | 1 | 1/2 |
| Water, Wall | 0 | (Wall: Slice C, infinite) | 1 | 1 | 1 |
| Marsh | 0 | – | 1 | 1/2 | 1/2 |
| Ford | 0 | – | 1 | 1/2 | 1/2 |
| Building | 8,000 mm | yes | 1 (blocked first) | 1 | 1 |

Global: Naismith climb 25/3 (CITED); melee slope coefficient β = 1/2, missile elevation coefficient γ = 1/2, gradient
cap 1 (45°) (PLACEHOLDER magnitudes); cohesion floor c₀ = 1/2 (PLACEHOLDER). All other entries PLACEHOLDER.

Rationale (direction cited, magnitudes not):
- **Cover ≠ concealment.** US Army FM 3-21.8 (2007), cover and concealment: cover protects from the effects of fire,
  concealment from observation. So concealment (detection clarity, Slice C table) and missile transmission (interception)
  are separate coefficients. Forest canopy order of magnitude: mature temperate canopy 15–30 m, tropical 25–45 m (low ends used).
- **Footing.** Mud and water deny footing to both striker and defender: Agincourt 1415 (Keegan, *The Face of Battle*,
  1976, ch. 2); attacks across rivers (Granicus 334 BC, Arrian, *Anabasis* I.13–16).
- **Cohesion.** Polybius, *Histories* XVIII.31: the phalanx needs level, clear ground; ditches, clumps of trees, ridges
  and water courses break it.
- **Slope.** Vegetius, *Epitoma rei militaris* III.13: the higher ground favours both the charge and the missile.
- **Climb.** W. W. Naismith (1892): one hour per 5 km plus one hour per 600 m of ascent ⇒ 1 mm of ascent costs 25/3 mm of
  flat travel; descent free. Its transfer to formed troops is PLACEHOLDER.

**Identity guarantee (validated by `TerrainEffects.Validate`):** Road and Open are never opaque and have transmission,
footing and order cap 1 (and `ConcealmentTable` already forces their concealment to 1); on flat ground every gradient is
0. So on a flat Road/Open field every lane-TR coefficient is exactly One and every Slice D equation is reproduced bit for
bit. Every coefficient is validated **strictly positive** (termination: §4).

### 1.3 Equations

Integer/Q32 throughout; "floor" at every product; S = cell size (20,000 mm in the tactical scenarios).

**Movement — planner and exit field.** Each A* step a→b costs the Slice C step cost plus
`ClimbCost(h_b − h_a) = floor(max(0, Δh) · 1000 · 25 / 3)` (cost×mm units; descent free). The term is ≥ 0, so the
heuristic stays admissible and consistent. The exit (rout) field uses the same term in the direction of travel toward
the edge (`EdgeField`: relaxation n→c pays `ClimbStep(n, c)`, descent picks `ClimbStep(c, n)`).

**Movement — kinematics.** Per-step advance = `floor(v · Δt / (classCost + climbPerMm))` with
`climbPerMm = floor(Δh⁺ · 1000 · 25 / (3 · run))`, Δh between the current cell and the cell one cell-length ahead toward
the goal (the next waypoint for an anchor), `run` = their centre distance. Example (DERIVED, tested): 5 m terrace,
20 m cells: cost 1000 + 2083 → 843 mm per 2 s step instead of 2,600.

**Line of sight (continuous, `Spatial/TerrainSight.cs`).** Same exact supercover walk and line-height interpolation as
Slice C, z(n) = z₀ + floor((z₁ − z₀)·n/D). For an intermediate cell c with zMin = min(z(TIn), z(TOut)):
ground(c) > zMin ⇒ blocked (Height); `inside = zMin < ground(c) + FeatureHeight(c)`; inside ∧ Opaque ⇒ blocked
(**Structure**); inside ∧ not corner-side ⇒ Clarity ×= Concealment(c), Cover ×= Transmission(c). The target's own cell
always applies (a unit under canopy is under canopy from above too); the observer's own cell never does. Consequences
(tested): a line from a hill clears a forest unattenuated; the same line at eye level is attenuated cell by cell; a
building blocks below its roof and not above; under the Slice-C-equivalent table (every layer unbounded, nothing opaque)
the query equals `LineOfSight.Query` exactly (600 random segments, random heights and classes). Since the merge the same
query serves the naval observers of land targets and of vessels, the land observers of vessels, and the direct-fire LOS
of domain gunners (§5.3) — so it is the identity there too under the Slice-C-equivalent table.

**Detection** keeps the Slice D rule (one counter-RNG draw per side, target and instant; p = best clarity, times the
stationary factor when the target is stationary, has not fired and the line is concealed, times lane N's personal
concealment) — clarity now comes from `TerrainSight`.

**Melee** (blow of a on t):
```
A = EffMelee(F_a)          D = EffDefence(F_t)                       (Slice D/E stats)
g  = clamp((h(cell t) − h(cell a)) / |centre_t − centre_a|, −1, 1)   (0 in the same cell; > 0 = attacking uphill)
S  = 1 − β·g                                                         slope
Iₐ = 1 + χₐ · min(1, δₐ / δrefₐ)                                     charge impetus (χ = profile Impetus; δ = distance
                                                                      actually covered this step; δref = open-ground sprint
                                                                      per step = v · 3/2 · Δt)
Fₐ, Fₜ = Footing(class of own cell)
Kₓ = c₀ + (1 − c₀) · Cohesion(Fₓ),  Cohesion = mean OrderCap over the formation's Active members' cells
A' = A · (S·Iₐ·Fₐ·Kₐ)      D' = D · (Fₜ·Kₜ)
p  = clamp(A' / (A' + D'), HitMin, HitMax)
```
Terrain shapes the charge **by mechanism**: the run-up is slower in marsh/forest/uphill (class cost + Naismith), so δ and
the impetus shrink; no separate "charge in forest" modifier exists. No shipped profile has an impetus (χ = 0 default;
tests use a labelled TEST INPUT χ = 1/2).

**Missile** (shot of a that geometrically struck s — Slice C `DirectFire` unchanged):
```
g_s = clamp((h(cell a) − h(cell s)) / |…|, −1, 1)                    (> 0 = shooting downhill)
E = 1 + γ·g_s
T = Π Transmission over the non-corner layer cells the shot is inside, entered strictly before the hit, × Transmission of
    the struck entity's own cell; an Opaque cell on that stretch BLOCKS the shot (not a valid shot, no activity)
p = clamp(A·E / (A·E + D), HitMin, HitMax) · T
```
Lane N's domain weapons (naval guns, coastal / field / AA guns, bombs, rockets) keep their own hit equations: terrain
enters them only through sight (§5.3). Cover against domain fire is an open point (§10, P-TR11).

### 1.4 Tests (semantic, exact) and their mutants

| Mechanism | Test | Pinned value (DERIVED) | Mutant(s) |
|---|---|---|---|
| Identity on flat open | `HitChanceIsTheSliceDValueWhenEveryFactorIsOne` (all A, D ∈ [0,120]²); golden trace hash unchanged | bit-exact Slice D | – |
| Slice C equivalence | `SliceCEquivalentTableReproducesLineOfSightExactly` | 600/600 equal | – |
| Canopy layer + elevation | `ForestAttenuatesBelowTheCanopyAndNotAboveIt`, `CanopyBoundaryIsStrict` (14,999 vs 15,000 mm), `TargetUnderCanopyStaysConcealedFromAbove` | 1/8 below, 1 above | M1 |
| Buildings occlude | `BuildingBlocksBelowItsRoofAndNotAbove` (z at roof edge 8,842 vs 3,485 mm) | Structure / visible | M2 |
| Detection uses it | `DetectionSeesOverTheCanopyFromAHillAndNotThroughABuilding` | tracked at t=0 from a 30 m hill; never through the building | M26 |
| Domain sight uses it | `ABuildingBlocksTheSightBetweenAShipAndTroopsBehindIt` | neither sees the other behind an 8 m building; both seen under the Slice-C-equivalent table | N7, N8 |
| Fire path cover/structures | `FirePathCoverAndStructureBlocking`, `MissileElevationCoverAndStructuresAreWiredExactly` | (3/4)², (3/4)³, blocked; p·3/4; Zero behind a building | M3, M28 |
| Missile elevation | same | E = 5/4 and 3/4 at g = ±1/2 | M27 |
| Melee slope | `MeleeSlopeCoefficientIsWiredExactly` | S = 7/8, 9/8, cap 1/2 | M4 |
| Footing + cohesion | `FootingAndCohesionAreWiredExactly`, `CohesionIsTheMeanOrderCapOfTheMembers` | 3/8 in a ford; 5/8 mean cohesion | M5, M6 |
| Charge impetus | `ImpetusFactorEquation`, `ChargeImpetusShrinksWithTheTerrainSlowedRunUp` | δ = 3,900 open vs 1,300 marsh; p = 5/8 open | M7 |
| Climb in the planner | `NaismithClimbCostIsExact`, `PlannerPaysTheClimbAndDetoursWhenCheaper` (= independent Dijkstra), `MoveToRoutesAroundAMoundWhenClimbCosts` | 83,333,333 for 10 m | M8 |
| Climb in kinematics | `AnchorClimbsSlowerExactly` | 2,600 + 843 mm | M9 |
| Climb in the exit field | `ExitFieldPaysClimbTowardTheEdgeOnly` | +ClimbCost once, downhill free | M10 |
| Table validation | `EffectsTableValidationRefusesNonIdentityOpenGroundAndZeroCoefficients` | – | – |

### 1.5 Material influence (MEASURED on the final tree, `Proto.M7.Bench terrain`, seeds 1–8, 20 HI assault 20 HI holding on an explicit Hold)

| Ground | attacker killed | defender killed | net attacker loss | attacker held the field |
|---|---:|---:|---:|---:|
| flat (control) | 4 | 75 | −71 | 8/8 |
| defender on the crest of a 10 m terrace (g = 1/2 across the crest) | 54 | 69 | −15 | 7/8 |
| attacker must wade a 2-column ford (defender on the far bank) | 68 | 2 | +66 | 0/8 |

Identical to the pre-merge measurement (the merge changes nothing in land-only battles).

Pinned by `AttackingUphillCostsTheAttackerMaterially` and `AttackingAcrossAFordCostsTheAttackerMaterially` (thresholds
well inside the measured gaps). The ford effect is very large (footing 1/2 × cohesion 3/4 on the wader, and the bank
striking a footing-less defender): the magnitudes are PLACEHOLDER and need calibration, the direction is the cited one.
Observation (not lane TR): on the flat a converging KnowledgeAi attack beats a static Hold line by a wide margin — a Slice D
dynamic (many-on-one at the front rank), not caused by this lane.

Amphibious battle (lane N's scenario, MEASURED, §5.4): terrain reaches the troops landed from the transports — with the
lane-TR table the snipers firing from the forest belt deal 192 instead of 428 damage to the landing force (3 instead of 8
of the landed troops killed), and the move is caused by the forest's missile transmission alone.

---

## 2. Deployment thirds (DIR5 §2.14)

DIR5 §2.14: "Divide the battlefield into thirds along the deployment axis. Each army deploys within its own one-third. The
middle one-third is undeployable before battle. Players can edit deployments before battle begins. Once deployment ends
and battle starts, the entire battlefield becomes accessible. … The previously observed 20-metre difference in distance
to an exit must not be artificially corrected simply to equalize outcomes."

**Predicate (`Tactical/Deployment.cs`, exact, no division).** The two sides must exit through opposite edges
(otherwise `SidesNotOpposed`). Axis x (West/East, L = Width·S) or y (North/South, L = Height·S). A coordinate u is in the
low third iff 3u < L, the middle iff L ≤ 3u < 2L, the high third iff 3u ≥ 2L (half-open like the cells; exact for any L,
e.g. L = 940,000: 313,333 low, 313,334 middle). An entity disc (centre u, radius r) is deployable iff the **whole disc** is
in its side's own third (the third at its own exit edge): low side 3(u + r) < L, high side 3(u − r) ≥ 2L; otherwise
`MiddleThird` or, if it reaches the far third, `EnemyThird`. Plus passable cell and no disc overlap. [r1, V12] Two passes:
pass 1 checks every entity in ascending id (range, passability, zone) and reports the first failing entity; only if all
pass, pass 2 reports the first overlapping pair (i, j), i < j, by its lower id — so a zone failure of ANY entity is reported
before an overlap of lower ids (refusal code + entity id) — a pure function of the deployment (tie-dense test permutes
input order).

**Ground formations only (since the merge).** The thirds place GROUND formations: `IsGroundFormation(s) := s.Domain = Land ∧
s.EmbarkedOn = null`. Vessel and aircraft formations and land formations that start aboard a transport contribute no
placement; the phase refuses edits to them (`NotGroundFormation = 10`, appended) and leaves them unchanged on `Close`; the
battle's defence-in-depth re-check runs over the ground entities only, before lane N's `DomainInit`. Tested by
`DeploymentThirdsApplyToGroundFormationsOnly` (a vessel on a lake in the middle third is accepted; mutant N9 refuses it).
How (or whether) the thirds apply to naval and air deployment is not defined by DIR5 §2.14 — **open point P-TR10**.

**Phase.** `DeploymentPhase` holds the proposal, accepts or refuses each edit [r1, V2: by checking the EDITED formation —
pass 1 over its entities, pass 2 its overlaps with every other ground entity at its current position (`CheckEdit`); other
formations' validity does not enter, so an invalid formation never blocks the edits that repair the others] (a refused
edit changes nothing), logs every edit with its verdict, refuses `Close` while invalid, and on
`Close` yields the battle setup with **explicit entity positions exactly as deployed** and `Deployment = Thirds`; later
edits are `PhaseClosed`. The battle re-checks a `Thirds` setup at construction. The rule constrains start positions only:
after start every passable cell is reachable (tested: a deployed formation marches into the middle third and then into the
enemy third). A closed deployment with no edits reproduces the generated-slot battle event for event (same trace and
result hash).

`BattleSetup.Deployment` defaults to `Unrestricted` so the many unit-level mechanic probes that place forces in contact
keep working. **PROPOSAL:** the campaign / sandbox path to a battle must always go through `DeploymentPhase` (or set
`Thirds`); the default is not the rule.

**Asymmetry preserved.** Nothing is normalized anywhere. The pre-F1 Meeting geometry (anchors Cell(14,16) and
Cell(34,16)) is restored as its own scenario, `TacticalScenarios.MeetingAsymmetric`, next to the mirrored `Meeting` (kept
as the control). `TwentyMetreExitDifferenceIsPreserved` pins: both anchors legal; exit-line distances 290,000 and
269,999 mm (East line convention L − 1 − x) — the 20,000 mm difference survives the phase, the closed setup and the battle's
start positions; the engine's exit-path metric differs by exactly one open cell (20,000 × 1000); the mirrored control
differs by 0 (the line convention's 1 mm). Mutant M14 ("Close normalizes side 1 to the mirror of side 0") is killed by it.

**Per-side outcomes (MEASURED on the final tree, 40 seeds, 50v50 HI, KnowledgeAi both sides):**

| Scenario | west (side 0) lost | east (side 1) lost | undecided | killed W / E |
|---|---:|---:|---:|---:|
| mirrored `Meeting` (control) | 22 | 16 | 2 | 661 / 647 |
| `MeetingAsymmetric` (20 m kept) | 9 | 31 | 0 | 615 / 635 |

(22 vs 16: two-sided p ≈ 0.4; 9 vs 31: p ≈ 0.001. Identical to the pre-merge measurement.)

**D-079 D13 classification (delegated technical; PROPOSAL for the record).** The mirrored control shows no significant
engine bias at n = 40; the asymmetric geometry produces a strongly one-sided result (see the table; two-sided binomial
tests by normal approximation). The outcome difference is therefore caused by the start geometry, not by the engine — a
deployment asymmetry, which the thirds rule explicitly permits (each army may stand anywhere in its own third).
Classification: **realistic tactical asymmetry, kept**; the mirrored scenario stays the control for engine-bias checks,
and battery reporting should be per side. (Why 20 m matters so much — which side reaches engagement distance first and
fights in formation — is an inference I did not isolate.)

---

## 3. The inactivity rule (DIR5 §2.6)

DIR5 §2.6: "A hidden stationary unit that remains stationary for at least five pulses may be treated as routed or lost to
prevent indefinite battles. Do not automatically destroy every stationary hidden unit. Distinguish genuine inactivity from
active concealment, defence or deliberate waiting." D-079 R17: "routed or lost" names the permitted consequences, so the
Sprint 1 "escaped, withdrawn intact" is not used. D6 delegates the distinction, whether the rule applies to a unit,
routed vs lost, and the Ledger mapping.

### 3.1 The exact predicate (`Tactical/InactivityRule.cs`, `TacticalBattle.PulseEndHiddenStationary`, `TacticalBattle.HiddenStationaryDomain.cs`)

For each LAND formation f with at least one Active member that has not already routed, at the end of pulse p:

```
Hidden_p(f)      := no on-field member of f is TRACKED by the enemy side at the end of p (by any observer, of any domain)
Progress_p(f)    := some member had PRIMARY activity in p (dealt/received damage; attacked a valid target — a shot at a bare
                    point with no target entity is NOT one; credited with a first detection; a new running minimum, by >= 1
                    cell, of its distance to a tracked enemy / an own objective / its exit while retreating; D-tactical.md §4)
                    or some member changed state
                    OR [r1, V1; potential corrected in r2, V1] the formation's anchor set a new running minimum, by >= 1
                    cell, of its REMAINING PATH LENGTH to the GOAL CELL of the movement order it executes (MoveTo destination,
                    or the cell an Attack marches on): Rem = |Anchor − P[i]| + Σ_{k=i}^{n−2} |P[k+1] − P[k]| along the
                    planned waypoint path P from its next waypoint i (0 once the path is exhausted); one minimum per goal
                    cell, never reset (DIR5 model only). (r1 used |Anchor − centre(goal)|, which grows on a detour.)
Stationary_p(f)  := NOT Progress_p(f)
c_p(f)           := (Hidden_p(f) AND Stationary_p(f)) ? min(c_{p−1}(f) + 1, 5) : 0,   c_{−1} = 0
(a) ConcealedEnemyInReach := EXISTS Active member e and an enemy x within |x − e| <= W(e) + v_f · PulseMs/1000 such that e
                    is CONCEALED FROM x: its cell's Concealment < 1, or its personal concealment < 1 (lane N camouflage),
                    or [r1, V4] TerrainSight.Query(x -> e, eye heights) is not visible or has Clarity < 1 (a building, a
                    crest, vegetation on the line); x an Active land entity, or an on-field vessel / aircraft (horizontal
                    distance; for these only the cell / personal terms count)
                    (W = RangeCells·S if ranged, else 2r + reach = 1,200 mm; v_f = the formation's open-ground pace)
                    — "it could close and strike within one pulse"
(b) DefensiveHold := Directive(f) = Hold AND that Hold came from an ACCEPTED EXPLICIT Hold order (not the default
                    standing state)  OR  |Anchor(f) − o| <= S for an objective o of f's side
(c) AmbushWait    := Posture(f) = Ambush AND Directive(f) = Hold   (the deployment-time ambush order still in force)
ROUT_p(f)        := c_p(f) >= 5 AND NOT ((a) OR (b) OR (c))
```

- **Consequence: ROUTED.** Every Active member becomes Routing and leaves by the Slice D rout mechanics (escape at its exit
  edge as a routed escape, or surrender if cut off) — conserved, nothing deleted. Chosen over "lost" because it reuses the
  existing conserved path and invents no casualty mapping; "lost" (a Ledger loss) is the alternative for the Director.
- **"Stationary" is read as "goes nowhere":** a hidden formation marching back and forth sets no new minimum and is
  stationary in effect (`OscillatingHiddenFormationIsStationaryInEffect` measures 28–31 m of movement every pulse and
  still routs it — [r1] at the end of pulse 6, after its first approach to each of its two goals). This is stricter than "did
  not move" — reported. [r1, V1] Before round 1 a STRAIGHT purposeful march to a non-objective point was also "stationary"
  (routed at the end of pulse 4 while marching 78 m per pulse) — not disclosed then, fixed now. [r2, V1] After round 1 a
  march along a DETOUR (around a wall, along a river bank to a ford) was still "stationary" — the r1 potential, the straight
  distance to the goal, grows on such a path — and was routed at the end of pulse 4 at full pace; fixed in round 2 (§13).
- **Mutual guard — [r1, V3] now an OPTION, default OFF** (`TacticalRules.MutualStandoffGuard`; D-079 S304: "routed or lost,
  per unit; not a draw or intact escape by default"). OFF: genuinely inactive formations of both sides all rout and the
  battle ends `BothEliminated`. ON (the pre-round-1 default, PROPOSAL P-TR1): if the routs of one pulse end would leave
  NEITHER side an Active entity of any domain (land entities in formations that do not rout now, plus vessels and aircraft
  counted as for elimination), nobody routs and the battle ends as a **Standoff**.
- **Whole-field Standoff:** G = pulses in a row in which no formation of any domain made progress (saturating at 5); G ≥ 5
  with nobody routing this pulse ends the battle as `BattleEndReason.Standoff`: nobody is routed or removed, Active
  entities stay Active. [r1, V3] This end is NOT in DIR5 §2.6's list (dead / retreated / routed / surrendered) and reaches
  beyond DIR5's hidden-only rule (it also ends battles whose formations are all VISIBLE and idle): an UNRATIFIED lane-TR end
  condition (PROPOSAL P-TR1), shipped as the default only because without it such battles never end (measured hang, M21). Evaluation order at a pulse end: lane N bookkeeping → counters → G → ROUT (→ elimination check) →
  mutual guard → Standoff.
- **Vessels and aircraft are not judged** by this rule (they never rout by it); they take part through progress (G), the
  guard and exemption (a) — §5.2. Whether DIR5 §2.6 should rout a hidden idle fleet or flight is **open point P-TR9**.
- **Verdict log** (`InactivityVerdicts`, hashed into the state hash): the first judgement of each idle stretch, every
  change of a formation's exemption set, every rout — bounded by state changes, not by pulses.

### 3.2 Tests (`HiddenStationaryRuleTests`, 22 cases; lane-N interplay in §5.2)

| Case | Setup | Result (MEASURED = DERIVED) |
|---|---|---|
| genuine inactivity | A hidden in jungle on its default Hold; B holding explicitly far away | A routed at the end of pulse 4; SideEliminated, 5 pulses, 300,000 ms; 10 routed escapes, 0 killed, 0 "disengaged"; verdicts exactly `[A: 5, None, routed]`, `[B: 5, DefensiveHold, not routed]` |
| 4 vs 5, ±1 | A ordered to march to its objective at pulse k | k = 3, 4: counter 3/4 then reset, not routed (Standoff later); k = 5, 6: routed at the end of pulse 4, the order never delivered |
| (a) boundary | one A entity in a jungle cell, one B entity d mm away | d = 79,200: exempt, Standoff at 6 pulses; d = 79,201: routed; clause off at 79,200: routed |
| (b) explicit Hold | Hold ordered at pulse 0 | exempt; Hold then Rally: routed |
| (b) delivery pin | Hold ordered at pulse k, step offset o | k = 3 or 4 (o = 0), and k = 4 at its LAST step (o = 29, delivered at 298,000 ms, before the 300,000 ms pulse end): exempt at the end of pulse 4 (Standoff); k = 5: too late, routed at the end of pulse 4; a Hold is not progress (counter 5 either way) |
| (b) objective | anchor 20,000 / 20,001 mm from own objective | exempt / routed |
| (c) ambush | Ambush posture on the standing Hold | exempt; after a MoveTo: routed |
| mutual guard [r1] | both hidden, idle, unexempt | default (guard OFF): both routed, BothEliminated at 5 pulses, 10 + 10 routed escapes; guard ON: Standoff at 5 pulses, both intact, 2 verdicts not routed |
| visible idle | two formations in sight, passive | never judged; Standoff at 6 pulses (G = 5) |
| oscillation [r1] | marching back and forth in the jungle | counter 0, 0, 1, 2, 3, 4 at pulse starts 1–6; routed at the end of pulse 6 (7 pulses) |
| purposeful march [r1] | hidden MoveTo to a non-objective point (`VerificationRound1TRTests`) | counter 0 for the 8 marching pulses; routed at the end of pulse 12, five pulses after arrival |
| rout mechanics | A routs while its side keeps an exempt formation | A's 10 entities flee West through the jungle and escape (routed escapes); battle ends later as Standoff |
| replay | order-log replay of a battle with a rout verdict | trace, state and result hashes and the verdict log equal |
| pure predicate | `NextCounter`, `Routs` at 4/5, saturation, every exemption | – |

### 3.3 The legacy rule is retained, not deleted

`TacticalRules.Inactivity` defaults to `HiddenStationary` (DIR5). `LegacySideDisengage` (Sprint 1: a side whose every
formation is inactive for 5 pulses escapes intact; lane N extended it to vessels and aircraft) is kept **only** because the
AutoResolver (lane P, not touched) still implements it. Tests that pin that rule or compare both resolvers on it pin
`LegacySideDisengage` explicitly (§8.2). **Until lane P ports the predicate above to the AutoResolver, the resolvers differ
by construction on any battle that ends by inactivity.**

---

## 4. No implicit pulse ceiling (DIR5 §2.6, O-4)

There is no pulse or event cap in `Proto.M7` (as in Sprint 1). Audit of everything that grows with battle length:

| Item | Bound | Status |
|---|---|---|
| DIR5 idle counter `c`, field counter `G` | saturate at 5 | new; no overflow possible |
| Legacy idle counters (`int`, checked; land and lane N's vessel/air counter) | the legacy model ends every battle at 5 idle pulses; lane N's counter keeps counting under DIR5 (bookkeeping) but resets on activity | unchanged; `checked` would throw only after 2³¹ consecutive idle pulses of a fleet under DIR5, which G ends after 5 — unreachable |
| Verdict log | bounded by state changes (one entry per idle stretch / exemption change / rout) | new; pinned by `LongMarch…` (`Single` verdict over 200+ pulses) |
| Order log index cast to `int` | 2³¹ orders (≥ 10⁸ pulses at ≤ 2N orders per formation) | formal, unreachable; reported |
| Time in ms (`long`) | 9.2·10¹⁸ ms ≈ 2.9·10⁸ years | formal; reported |
| Entity generation (`int`, checked) | ≤ 3 state changes per entity (lane N: embark / land cycles add 2 per cycle — bounded only by the orders given) | not pulse-related |
| `EventTrace` (all entries kept in a `List`) | memory: MEASURED ≈ 152 entries/pulse for a 1v1 duel; ≈ 550/pulse for 110v110; ≈ 100 B/entry ⇒ ≈ 55 KB/pulse at 110v110 ⇒ 10⁴ pulses ≈ 0.5 GB; hard cap 2³¹ entries | **practical memory bound, not a rule.** Not changed (shared Slice B file). PROPOSAL P-TR7: stream the trace hash |
| Test harness guards | 2,000 pulses (default); 100 in `HiddenStationaryRuleTests`; 40–50 in the interplay tests; 20,000 / 2,000 / 20,000 in the three long tests | test-only, never a rule; `TheHarnessGuardIsNeverTheTerminator` (guard 20,000 vs 2,000: same battle) |

**Termination argument (extends D-tactical.md §4).** Every progress event consumes a bounded integer potential (HP, hull
and bounded heal / repair pools; crews; running minima; first detections; sorties; monotone state changes). Attacks almost
surely consume HP because every lane-TR coefficient is validated > 0, so p ≥ HitMin · T_min > 0 for every attack at a
target. **The one unbounded activity source the merge brought in — area fire at a bare POINT with unlimited ammunition — is
not progress** (§5.2 item 5): it counts only when its blast lands on someone. Once progress stops, the field counter G
reaches 5 within five pulses and the battle ends (Standoff), unless ROUT or elimination ends it first. So every battle ends
by a battle-end condition — elimination (DIR5 §2.6: "dead, retreated, routed or surrendered"; [r2, V8] quote corrected), or the lane's UNRATIFIED Standoff (P-TR1,
[r1]) — almost surely, with no ceiling. [r1, V1] The march-progress minima are one more bounded potential: one per goal
CELL (at most Width·Height of them), each monotone and falling by >= one cell per progress event, so even a controller that
issues new MoveTo orders for ever obtains finitely many march-progress events. [r1, V5] A shot blocked by a building is
not a valid shot and not progress (`ShotsBlockedByABuildingAreNotActivity`; counting it is a measured non-termination). Evidence (MEASURED): attrition duels at hit chance 1/2000 run hundreds of pulses and end by elimination
(`LongAttritionDuelEndsOnlyByElimination`: > 100 pulses, never Standoff, field counter 0 throughout; bench §6); a half-speed
march across an 8 km field lives > 150 pulses because it progresses every pulse, then ends five idle pulses after arrival
(`LongMarchLivesAsLongAsTheStateProgresses`); a warship bombarding an empty point for the whole battle no longer keeps it
alive (`ABombardmentOfAnEmptyPointDoesNotKeepTheBattleAlive`).

### 4.1 Findings: non-termination under mutation

- **M21** (whole-field Standoff end removed): in the first (pre-merge) mutant run it did not finish within its 400 s bound:
  **HANG — non-termination under this mutation** (ADR-015 §7.1). The Standoff end is load-bearing for termination, but the
  suite could not report it in time because the long tests then used a 10⁶-pulse harness guard. Fix (tests only): guards
  sized to 10–50× the measured battle; the march test asserts the end within 2N + 1 pulses of arrival. Re-run on the final
  tree: §8.4.
- **Merge finding (no mutation needed):** with lane N's vessel / aircraft activity fed into G (item 1 of §5.2) but bare point
  fire still counted as progress, `FormationInputOrderDoesNotMatter` and `VesselsNeverLeaveTheWaterInTheAmphibiousBattle`
  (both run the amphibious battle) tripped lane N's 400-pulse harness guard (MEASURED, second suite run after the merge). Cause: the warship
  bombarding the forest edge with unlimited ammunition is primary activity every reload. Fixed by item 5 of §5.2; mutant N6
  re-introduces it (§8.4).

---

## 5. Rebase on dev `7118ef0` and the lane N interplay

### 5.1 The merge (MEASURED)

`7118ef0` = T3 (two-action windows) + lane N (naval, air, transports, combined arms, snipers). Conflicts (4 files) were
resolved keeping both sides: `Proto.M7.Bench/Program.cs` (both dispatch lines), `BattleSetup.cs` (`Effects`, `Deployment`
and lane N's `MultiDomain`), `UnitProfiles.cs` (`Impetus` and lane N's `FireIntervalSteps`, `PersonalConcealment`),
`TacticalBattle.cs` (the entity fields `LastStepMm` and `NextRangedMs`; the constructor tail: thirds re-check over ground
entities → lane N's `DomainInit` → cohesion initialisation after it, so members that start aboard are not counted on the
ground). `Deployment.cs` gained the ground-formation scope (§2). Every lane N hook (28 `lane N` markers) is present in the
merged `TacticalBattle.cs`.

| Tree | Debug | Release |
|---|---|---|
| `7118ef0` alone (TR-base checkout) | 487 / 0 / 0 | 487 / 0 / 0 |
| merge commit `35f8b06` (before any further change) | 538 / 6 / 544 | 538 / 6 / 544 |

The 6 failures of the merge commit, all lane N tests meeting lane TR changes, and what was done:

| Failure | Cause (measured) | Resolution |
|---|---|---|
| `AirTests.LookDownClarityIsTheCanopyOfTheTargetsOwnCell` | an 8-entry concealment table (`ClassCount` 8 → 11) | table extended with the three new classes (none on that grid) |
| `DomainOrdersAndGoldenTests.SailLineBattleGoldenAndSeedSensitivity`, `CombinedArmsTests.NavalBattleDefaultsToTheTacticalResolverAndIsResolvedTactically` | naval-only battles ended as a Standoff after five pulses: no land formation, so G never saw any progress | item 1 below; both pass unchanged (the sail golden is lane N's value again) |
| `DomainOrdersAndGoldenTests.AmphibiousBattle…MatchesTheGolden` | terrain + DIR5 rule + state-hash encoding | re-pinned with measured attribution (§5.4) |
| `TransportTests.EmbarkDisembarkLoopIsNotActivitySoTheBattleStillEnds` | asserts the legacy end `BothDisengaged` | pinned to the legacy model; DIR5 twin added (§5.2) |
| `SniperTests.ConcealedSniperInTheOpenCountsAsActivelyHiding` | pins the legacy active-concealment counter | pinned to the legacy model; DIR5 twin added (§5.2) |

### 5.2 The DIR5 rule with vessels and aircraft (`Tactical/TacticalBattle.HiddenStationaryDomain.cs`)

| # | Rule | Why | Test (MEASURED) | Mutant |
|---|---|---|---|---|
| 1 | Vessel / aircraft progress (lane N's activity definition: a present member with primary activity, or a state change) feeds G | otherwise a naval-only battle ends as a Standoff while the ships fight (merge commit) | `ANavalOnlyBattleIsNotEndedAsAStandoffWhileTheShipsFight` (sail line battle: > 5 pulses, not Standoff) | N1 |
| 2 | Lane N's `DomainPulseEnd` still runs (vessel morale recovery; lane N's counter, which is in the state hash); its side verdict is discarded | the DIR5 path returned before lane N's hook: vessel morale stopped recovering | `VesselMoraleRecoversUnderDir5ExactlyAsUnderTheLegacyModel` (500 → 600 in both models, 5 pulses) | N2 |
| 3 | Mutual guard over every domain (§3.1) | a side that keeps a ship is not eliminated by its troops' rout | `TheMutualGuardFiresOnlyWhenTheRoutsWouldLeaveNeitherSideAnActiveEntity` (land-only: Standoff; with a side-1 transport: both riflemen rout, side 1 holds, 5 pulses) | N3 |
| 4 | Exemption (a): personal concealment counts as concealment (DIR5 says *active concealment*); enemy vessels / aircraft count as enemies in reach | lane N's camouflaged sniper waiting in the open is active concealment; a ship offshore is an enemy | `ACamouflagedSniperInTheOpenWithAnEnemyInReachIsExempt` (60 m: exempt; 200 m: routed), `AConcealedFormationWithAnEnemyVesselInReachIsExempt` (150 m: exempt; 900 m: routed; reach 378 m) | N5, N4 |
| 5 | Under DIR5 a shot at a bare point (area fire, no target entity) is not progress by itself — its blast is, when it lands on someone (`CreditSource`); implemented by restoring the shooter's Primary flag to its pre-shot value, so activity before or after the shot in the pulse still counts exactly; legacy model unchanged | unlimited-ammunition point fire is an unbounded activity source (merge finding, §4.1) | `ABombardmentOfAnEmptyPointDoesNotKeepTheBattleAlive` (≥ 40 point shots, Standoff after 6 pulses; aimed at the enemy: it hits and decides the battle) | N6 |
| – | DIR5 twins of the two legacy-pinned lane N tests | – | `EmbarkDisembarkLoopIsNotProgressUnderDir5` (Standoff after 6 pulses, no verdict, nobody harmed — each formation spends a pulse end aboard at least every other pulse, which resets its counter); the sniper twin above | N1 (also kills it) |

Item 5 changes when lane N's activity flag is set, so it is a change to a lane N mechanic — made only under the DIR5
model, only for bare point shots, and only because termination requires it. Item 3 changes the guard's definition in
mixed battles only (identical in land-only battles: the land tests and the Slice D golden are unchanged).

### 5.3 Terrain sight for the domain modules

The five `LineOfSight.Query` calls of lane N (`TacticalBattle.DomainCombat.cs`: vessel observer → land target, vessel
observer → vessel / aircraft, land observer → vessel / aircraft, and the direct-fire LOS of domain gunners at land and at
domain targets) now call `TerrainSight.Query` with the battle's terrain table — the sight rule "terrain requires": buildings
block below their roof, canopy attenuates only below its height. Aircraft observers keep lane N's look-down rule (clarity =
concealment of the target's own cell), which is what `TerrainSight` also applies to the target's cell. Under the
Slice-C-equivalent table the calls are identical to Slice C (tested on random segments, §1.4). Test:
`ABuildingBlocksTheSightBetweenAShipAndTroopsBehindIt`; mutants N7 (vessel observer) and N8 (land observer of a vessel).
The three other call sites (vessel observer → vessel, both gunner LOS) have no dedicated test (§9).

### 5.4 The moved amphibious golden — measured attribution

Lane N's amphibious battle (seed 42; scripted attacker, AI defender) under four configurations (MEASURED):

| Terrain table | Inactivity model | Trace hash | Result hash | Personnel hash | End |
|---|---|---|---|---|---|
| Slice-C-equivalent (OFF) | legacy | **5B72E757…9AAF = lane N** | **216C4A45…AE15 = lane N** | **E28B1BE9…C963 = lane N** | SideDisengaged, 15 pulses |
| placeholder, but missile transmission = 1 everywhere | legacy | **= lane N** | **= lane N** | = lane N | SideDisengaged, 15 pulses |
| placeholder (ON) | legacy | D5D47B43…5ABC | A587F3C2…29A7 | 801DBC88…13FD | SideDisengaged, 15 pulses |
| Slice-C-equivalent (OFF) | DIR5 | 481A9EBE…8749 | D49C3A34…2204 | 1EFBF25C…58C9 | Standoff, 19 pulses |
| placeholder (ON) — **the default, new golden** | DIR5 | 075371D6…D6C0 | 7A21CC07…8AAE | 6B252A73…7103 | Standoff, 19 pulses |

(The state hash differs in every lane-TR configuration because lane TR appended its own state to the state-hash encoding;
the trace, result and personnel hashes show the battle itself.)

Attribution: **(i) terrain** — of all lane-TR coefficients only the forest's missile transmission changes this battle: the
defending snipers (F23) fire through the forest belt at the landing force (F12): damage dealt 428 → 192, landed troops
killed 8 → 3 (every other formation's counts are unchanged); the domain-sight switch, elevation, layers, footing, cohesion,
slope and climb are identity here (each isolated variant reproduces the same hashes, MEASURED with a temporary harness);
**(ii) the DIR5 rule** — the holding AA guns (F22) and the defending fighter (F24) no longer escape intact by side
disengagement: the battle ends as a Standoff after 19 pulses with them Active (D-079 R17: escaped is not a permitted
inactivity consequence; nobody is judged inactive in this battle — the verdict log is empty); **(iii) the state-hash
encoding.** Pinned by `TheAmphibiousGoldenMovesOnlyThroughMissileTransmissionAndTheDir5Rule`, which keeps lane N's
constants as the control; the new constants are in `DomainOrdersAndGoldenTests` with this attribution. The sail line
battle golden did not move.

---

## 6. Tactical bench cost (MEASURED; host shared with other agents — single numbers vary by tens of percent)

Release build, `nice -n 19`, interleaved runs, host load average ≈ 2 during the runs (much quieter than the pre-merge
session). Totals in ms over 10 seeds per case; the outcome columns (mean pulses / mean events) are the check that the same
battles are being timed.

**Open field, `Proto.M7.Bench tactical 10` — `7118ef0` vs final lane TR:**

| Case (mean pulses / events, identical in both trees) | `7118ef0` r1 | lane TR r1 | `7118ef0` r2 | lane TR r2 |
|---|---:|---:|---:|---:|
| 50v50 heavy infantry (3.7 / 911) | 766 | 905 | 719 | 822 |
| 60v60 archers (3.0 / 857) | 680 | 750 | 664 | 707 |
| 50v60 HI vs archers (3.0 / 731) | 551 | 635 | 552 | 576 |
| 110v110 combined arms (4.0 / 2,210) | 2,747 | 2,825 | 2,489 | 2,571 |
| **sum** | **4,744** | **5,115** | **4,424** | **4,676** |

Same battles in both trees (every per-seed end, pulse count, event count and casualty split is identical — MEASURED,
log in the appendix). Lane TR costs **+5.7 % to +7.8 %** on the open field (two rounds; the noise between rounds of the same
tree is of the same order: 7.2 % for `7118ef0`, 9.4 % for lane TR). INFERRED cause: the per-pulse DIR5 bookkeeping (counters, exemption checks at the
threshold) and the per-step cohesion / gradient short-circuit checks; not profiled.

**Mixed-terrain map vs open field (`Proto.M7.Bench terrain`, final tree, 10 seeds):** 50v50 HI open 3.7 pulses / 911
events, median 23.4 ms/pulse; terrain 8.1 / 1,230, median 15.6 ms/pulse. 110v110 combined arms open 4.0 / 2,210, median
69.3 ms/pulse; terrain 4.5 / 3,344, median 74.1 ms/pulse. Per event the terrain map costs about the same as the open
field (total 3,131 ms / 33,440 events ≈ 0.094 ms vs 2,714 ms / 22,100 ≈ 0.123 ms); per battle it costs more because the
battles manoeuvre longer.

**Naval / amphibious (`Proto.M7.Bench domain 5`, `7118ef0` vs final lane TR):**

| Case | `7118ef0` pulses / events / total ms | lane TR pulses / events / total ms |
|---|---|---|
| galleys 5v5 | 18.8 / 2,763 / 452 | 18.8 / 2,763 / 471 |
| sail line 5v5 (wind) | 46.8 / 4,635 / 474 | 48.0 / 4,745 / 475 |
| turret 5v5 | 11.2 / 1,944 / 51 | 11.2 / 1,944 / 55 |
| amphibious combined arms | 13.0 / 1,817 / 624 | 16.2 / 2,184 / 792 |

Galleys and turret ships: same battles, cost within noise. Sail: different battles — attribution MEASURED (seeds 1–5, both
inactivity models on the final tree): under the legacy model seeds 1, 2 and 5 end with one fleet **disengaging intact**
after five idle pulses (34, 49, 39 pulses); under DIR5 vessels are not judged, so those battles end as a **Standoff** 1–4
pulses later with both fleets on the field (38, 50, 40); seeds 3 and 4 are identical (elimination). Amphibious: the seed-42
attribution of §5.4 (forest missile transmission + DIR5 Standoff); other seeds not isolated (INFERRED: same two causes).
Per event the amphibious battle costs 0.069 ms on `7118ef0` and 0.073 ms on lane TR (624 ms / 9,085 events vs 792 ms /
10,920 events, +5 %, inside the round-to-round noise), so the switch of the domain sight calls to `TerrainSight` has no
cost that this small bench can resolve. Per-seed outcomes (appendix): galleys and turret ships identical in every field;
amphibious seeds 1–4 end SideDisengaged → Standoff, seed 5 SideEliminated in both, and in every seed the landing force
loses fewer men (e.g. seed 1: 8 → 2 killed) — the same two causes as seed 42.

---

## 7. Cross-lane effects (for the integrator)

- **AutoResolver parity (lane P).** D-079 D11: "terrain influence in both resolvers together (a one-sided addition creates
  a parity gap by construction)". This lane adds terrain and the DIR5 inactivity rule to the TacticalResolver only (as
  briefed). The AutoResolver must receive both (the equations of §1.3 and the predicate of §3.1) before parity is measured
  on terrain or inactivity scenarios. The parity battery's golden **subset** (open-field scenarios) did not move.
- Parity scenarios with terrain, tactical side only (MEASURED on the final tree, `Proto.M7.Bench terrain`, appendix):

  | Scenario | terrain | A held | B held | undecided | mean pulses | killed A | killed B |
  |---|---|---:|---:|---:|---:|---:|---:|
  | ArchersVsInfantryForest | OFF | 20 | 0 | 0 | 5.0 | 0 | 392 |
  | ArchersVsInfantryForest | ON | 20 | 0 | 0 | 5.0 | 0 | 380 |
  | Ambush (jungle belt) | OFF | 7 | 11 | 2 | 5.9 | 353 | 315 |
  | Ambush (jungle belt) | ON | 7 | 11 | 2 | 5.9 | 353 | 315 |

  The three parity scenarios under both inactivity models (ArchersVsInfantryForest, Ambush, EqualInfantry, 20 battles
  each) give identical outcomes under DIR5 and legacy: they end by elimination before any idle stretch. Observation for lane
  P (not caused by this lane): in `EqualInfantry` side 0 held the field 4 times, side 1 12 times, undecided 4 (two-sided
  binomial p ≈ 0.08 on the 16 decided battles — not significant at n = 20, worth a larger battery).

  ArchersVsInfantryForest: the forest's missile transmission (3/4 per layer cell) intercepts arrows — fewer infantry
  killed, same winner. Ambush: identical battles. INFERRED (not isolated): both formations fight on the same flat jungle
  belt with melee only, so the cohesion factors of attacker and defender are equal and cancel exactly in
  A·K/(A·K + D·K) (integer floor of the same rational), there is no slope and no missile, and at eye height on flat ground
  every line through the jungle is inside its 25 m canopy, which is exactly the Slice C concealment rule. Terrain that is
  the same for both sides changes nothing by construction; it matters where it differs between them (§1.5).
  [r1, V8] The parity `Ambush` deploys its ambushers (side 1, entity 2000 first) in the MIDDLE third, which DIR5 §2.14
  forbids before battle; it runs because `BattleSetup.Deployment` defaults to Unrestricted. Its numbers above are therefore
  not results of a DIR5-legal deployment. The same holds for lane N's `Amphibious` (shore formations 20–23 in the middle
  third). Pinned by `ShippedScenariosAgainstTheThirdsRule`; PROPOSAL P-TR13; exemption (c) vs the thirds: limitation 31.
- **Lane N.** §5: the DIR5 rule takes vessels and aircraft into account without judging them; lane N's domain hit
  equations are unchanged; the amphibious golden moved with attribution; the sail golden did not. Lane N's report (H-N)
  describes the legacy inactivity behaviour of vessels and aircraft — under the default (DIR5) model their side no longer
  disengages; the battle ends by elimination, by a land rout, or as a Standoff.
- **T3 (two-action windows).** No interaction found: T3's files are untouched; T3's tests pass on the merged tree.
- `TerrainCostTable.ClassCount` 8 → 11: any new code that builds an 8-entry table must add Marsh, Ford, Building (lane N's
  one such test was extended).
- `KnowledgeAi` was not changed. An AI formation with no orders, no objective and no known enemy is genuinely idle under
  DIR5 and routs after five hidden pulses unless it is exempt; an AI that means to hold should order Hold (PROPOSAL P-TR8).

---

## 8. Validation

### 8.1 Test counts (MEASURED, `dotnet test prototypes/m7-sprint1/M7Proto.slnx`, the only solution whose projects changed)

| Tree | Debug | Release |
|---|---|---|
| `fc5230c` (pre-merge baseline, first run) | 369 / 0 / 0 | 369 / 0 / 0 |
| lane TR before the merge (first run) | 426 / 0 / 0 | 426 / 0 / 0 |
| `7118ef0` alone | 487 / 0 / 0 | 487 / 0 / 0 |
| merge commit `35f8b06` | 538 / 6 / 544 | 538 / 6 / 544 |
| lane TR tree before verification round 1 (`822f891`) | 554 / 0 / 0 | 554 / 0 / 0 |
| **after verification round 1 (§12)** | **569 / 0 / 0** | **569 / 0 / 0** |

Against `7118ef0`: +67 tests (57 lane-TR tests of §1–§4 + 9 interplay tests in `TerrainDomainInterplayTests` + 1 DIR5 twin
in `TransportTests`), 0 removed, 0 failing.

Serialization: no new serialized row type. `InactivityVerdict` and the new formation fields are appended to the battle's
STATE-HASH encoding only (the prototype has no state deserializer, so there is no round-trip to test); hash equality on a
POPULATED verdict log is pinned by `ReplayReproducesARoutVerdictBitForBit` (live vs order-log replay, with a rout verdict
present). `DeploymentLogEntry` is in-memory only (neither hashed nor serialized).

### 8.2 Moved tests and goldens (each attributed)

| Test | Change | Cause |
|---|---|---|
| `ReplayAndMetamorphicTests.GoldenStateHash` | 88F6322C… → 92EA64F3… | state-hash ENCODING extended with lane-TR state; the battle is unchanged: `GoldenTraceHash` E53AA685… did **not** move (also asserted by lane N's `LandOnlyBattleHasNoDomainSection…`) |
| `PathfinderTests.Cost_validator_rejects_violations`, `Forest_and_hills_position_is_not_constrained`, `Concealment_validator` | 8-entry tables → 11 entries | ClassCount 8 → 11 |
| `AirTests.LookDownClarityIsTheCanopyOfTheTargetsOwnCell` (lane N) | 8-entry concealment table → 11 entries | ClassCount 8 → 11 |
| `InactivityRuleTests` (2 setups) and `TT.HideRules` | pinned `Inactivity = LegacySideDisengage` | they test the superseded Sprint 1 rule, still used by the AutoResolver |
| `AutoResolverTests.MutualStandoffDisengagesAfterExactlyFivePulsesInBothResolvers`, `AutoResolverVerificationFixTests.IdleFormationRecoversHoldMorale…`, `FirstDetectionIsActivityInBothResolvers` | pinned legacy rule on the tactical side | resolver parity on the rule both still share |
| `SniperTests.ConcealedSniperInTheOpenCountsAsActivelyHiding`, `TransportTests.EmbarkDisembarkLoopIsNotActivitySoTheBattleStillEnds` (lane N) | pinned legacy rule; DIR5 twins added | they pin lane N's legacy active-concealment counter / the legacy `BothDisengaged` end |
| `VerificationFixDTests.ReacquiringALostEnemyIsNotNewActivity` | BothDisengaged → Standoff, 1 Active per side instead of 1 disengaged escape (pulse window 5–8 unchanged) | DIR5 rule; the property pinned (re-acquisition is not progress) is unchanged |
| `DomainOrdersAndGoldenTests.AmphibTrace/State/Personnel` (lane N) | 5B72E757… / 6E3963D9… / E28B1BE9… → 075371D6… / 736BB506… / 6B252A73… | §5.4 (missile transmission of the forest + DIR5 rule + encoding), pinned by the attribution test |

### 8.3 Determinism scan

`scripts/check-proto-determinism.sh`: **CLEAN** (64 files under `Proto.M7/`, tree `822f891`; [r1] 65 files after round 1, CLEAN).

### 8.4 Mutants

**Final tree** (prioritized subset, throttled; separate scratch clone of `4052510`; one full `Proto.M7.Tests` Release run per mutant;
bound = 170 s = 10 × the measured clean full-suite wall time of 14–17 s; workspace restored after each mutant, `git status` clean
afterwards). Every kill includes at least one semantic (non-golden) test.

| Mutant | What it breaks | Verdict | Suite time | Failing tests (semantic ones in bold) |
|---|---|---|---:|---|
| N1 | interplay 1: vessel/aircraft progress ignored by the field counter G | **KILLED** | 14s | **`CombinedArmsTests.NavalBattleDefaultsToTheTacticalResolverAndIsResolvedTactically`**, `DomainOrdersAndGoldenTests.SailLineBattleGoldenAndSeedSensitivity`, **`TerrainDomainInterplayTests.ANavalOnlyBattleIsNotEndedAsAStandoffWhileTheShipsFight`**, **`TransportTests.EmbarkDisembarkLoopIsNotProgressUnderDir5`** |
| N2 | interplay 2: lane N pulse-end bookkeeping skipped under DIR5 | **KILLED** | 15s | `DomainOrdersAndGoldenTests.AmphibiousBattleReplaysExactlyFromItsOrderLogAndMatchesTheGolden`, **`TerrainDomainInterplayTests.VesselMoraleRecoversUnderDir5ExactlyAsUnderTheLegacyModel`** |
| N3 | interplay 3: mutual guard over land formations only | **KILLED** | 17s | **`TerrainDomainInterplayTests.AConcealedFormationWithAnEnemyVesselInReachIsExempt`**, **`TerrainDomainInterplayTests.TheMutualGuardFiresOnlyWhenTheRoutsWouldLeaveNeitherSideAnActiveEntity`** |
| N4 | interplay 4: exemption (a) ignores enemy vessels/aircraft | **KILLED** | 14s | **`TerrainDomainInterplayTests.AConcealedFormationWithAnEnemyVesselInReachIsExempt`** |
| N5 | interplay 4: exemption (a) ignores personal concealment | **KILLED** | 17s | **`TerrainDomainInterplayTests.ACamouflagedSniperInTheOpenWithAnEnemyInReachIsExempt`** |
| N6 | interplay 5: a bare point shot counts as progress | **KILLED** | 17s | `DomainOrdersAndGoldenTests.AmphibiousBattleReplaysExactlyFromItsOrderLogAndMatchesTheGolden`, `DomainOrdersAndGoldenTests.FormationInputOrderDoesNotMatter`, `NavalTests.VesselsNeverLeaveTheWaterInTheAmphibiousBattle`, **`TerrainDomainInterplayTests.ABombardmentOfAnEmptyPointDoesNotKeepTheBattleAlive`**, `TerrainDomainInterplayTests.TheAmphibiousGoldenMovesOnlyThroughMissileTransmissionAndTheDir5Rule` |
| N7 | domain sight: vessel observer of land targets back to Slice C LOS | **KILLED** | 16s | **`TerrainDomainInterplayTests.ABuildingBlocksTheSightBetweenAShipAndTroopsBehindIt`** |
| N8 | domain sight: land observer of vessels back to Slice C LOS | **KILLED** | 15s | **`TerrainDomainInterplayTests.ABuildingBlocksTheSightBetweenAShipAndTroopsBehindIt`** |
| N9 | deployment: placements include vessel/aircraft/embarked formations | **KILLED** | 19s | **`TerrainDomainInterplayTests.DeploymentThirdsApplyToGroundFormationsOnly`** |
| M3 | missile: cover transmission ignored | **KILLED** | 15s | `DomainOrdersAndGoldenTests.AmphibiousBattleReplaysExactlyFromItsOrderLogAndMatchesTheGolden`, `TerrainDomainInterplayTests.TheAmphibiousGoldenMovesOnlyThroughMissileTransmissionAndTheDir5Rule`, **`TerrainTacticalTests.MissileElevationCoverAndStructuresAreWiredExactly`** |
| M8 | planner: climb cost dropped | **KILLED** | 14s | **`TerrainSightAndClimbTests.PlannerPaysTheClimbAndDetoursWhenCheaper`**, **`TerrainTacticalTests.MoveToRoutesAroundAMoundWhenClimbCosts`** |
| M13 | deployment: battle does not re-check a Thirds setup | **KILLED** | 16s | **`DeploymentThirdsTests.BattleRefusesAThirdsSetupWithAMiddleThirdEntity`** |
| M14 | deployment: Close normalizes side 1 to the mirror of side 0 (equalizing) | **KILLED** | 17s | **`TerrainDomainInterplayTests.DeploymentThirdsApplyToGroundFormationsOnly`**, **`DeploymentThirdsTests.TwentyMetreExitDifferenceIsPreserved`** |
| M15 | rule: threshold off by one (6 pulses) | **KILLED** | 15s | **`TerrainDomainInterplayTests.ACamouflagedSniperInTheOpenWithAnEnemyInReachIsExempt`**, **`TerrainDomainInterplayTests.AConcealedFormationWithAnEnemyVesselInReachIsExempt`**, **`TerrainDomainInterplayTests.TheMutualGuardFiresOnlyWhenTheRoutsWouldLeaveNeitherSideAnActiveEntity`**, **`HiddenStationaryRuleTests.AmbushWaitIsDeliberateWaiting`**, **`HiddenStationaryRuleTests.ConcealedWithAnEnemyInReachIsNotInactivity(d:`**, **`HiddenStationaryRuleTests.ExplicitHoldCountsFromTheInstantItIsDelivered(pulse:`**, … (14 in all) |
| M19 | rule: mutual guard removed (double rout) | **KILLED** | 20s | **`TerrainDomainInterplayTests.TheMutualGuardFiresOnlyWhenTheRoutsWouldLeaveNeitherSideAnActiveEntity`**, **`HiddenStationaryRuleTests.MutualHiddenIdlenessIsAStandoffNotADoubleRout`** |
| M21 | standoff: whole-field idle end removed | **KILLED** | 18s | `DomainOrdersAndGoldenTests.AmphibiousBattleReplaysExactlyFromItsOrderLogAndMatchesTheGolden`, `DomainOrdersAndGoldenTests.FormationInputOrderDoesNotMatter`, `NavalTests.VesselsNeverLeaveTheWaterInTheAmphibiousBattle`, **`TerrainDomainInterplayTests.ABombardmentOfAnEmptyPointDoesNotKeepTheBattleAlive`**, **`TerrainDomainInterplayTests.ACamouflagedSniperInTheOpenWithAnEnemyInReachIsExempt`**, **`TerrainDomainInterplayTests.AConcealedFormationWithAnEnemyVesselInReachIsExempt`**, … (19 in all) |
| M25 | an implicit 100-pulse ceiling | **KILLED** | 17s | **`NoPulseCeilingTests.LongAttritionDuelEndsOnlyByElimination`**, **`NoPulseCeilingTests.LongMarchLivesAsLongAsTheStateProgresses`** |
| M26 | detection: Slice C LOS (no canopy, no structures) | **KILLED** | 14s | **`TerrainTacticalTests.DetectionSeesOverTheCanopyFromAHillAndNotThroughABuilding`** |

**Pre-merge tree (first run, before the pause; evidence for that tree only):** 32 definitions, filtered suite (the five lane-TR
test classes), bound 400 s.

| Mutant | What it breaks | Verdict | Killing tests |
|---|---|---|---|
| M1 | sight: canopy height ignored (every line inside every layer) | KILLED | `BuildingBlocksBelowItsRoofAndNotAbove`, `CanopyBoundaryIsStrict`, `ForestAttenuatesBelowTheCanopyAndNotAboveIt`, `DetectionSeesOverTheCanopyFromAHillAndNotThroughABuilding` |
| M2 | sight: buildings not opaque | KILLED | `BuildingBlocksBelowItsRoofAndNotAbove` |
| M3 | missile: cover transmission ignored | KILLED | `MissileElevationCoverAndStructuresAreWiredExactly` |
| M4 | melee: slope sign flipped (downhill penalized) | KILLED | `MeleeSlopeCoefficientIsWiredExactly` |
| M5 | melee: attacker footing ignored | KILLED | `ChargeImpetusShrinksWithTheTerrainSlowedRunUp`, `FootingAndCohesionAreWiredExactly` |
| M6 | melee: cohesion factor ignored | KILLED | `ChargeImpetusShrinksWithTheTerrainSlowedRunUp`, `FootingAndCohesionAreWiredExactly` |
| M7 | charge: impetus ignores the run-up distance | KILLED | `ChargeImpetusShrinksWithTheTerrainSlowedRunUp`, `ImpetusFactorEquation` |
| M8 | planner: climb cost dropped | KILLED | `PlannerPaysTheClimbAndDetoursWhenCheaper`, `MoveToRoutesAroundAMoundWhenClimbCosts` |
| M9 | movement: anchor/entity climb dropped | KILLED | `AnchorClimbsSlowerExactly` |
| M10 | exit field: climb direction reversed | KILLED | `ExitFieldPaysClimbTowardTheEdgeOnly` |
| M11 | deployment: west/north side may deploy in the middle third | KILLED | `BattleRefusesAThirdsSetupWithAMiddleThirdEntity`, `CheckRefusesMiddleThirdImpassableOverlapAndUnopposedSides`, `CloseRequiresAValidDeploymentAndThenLocksIt`, `PhaseAcceptsEditsInsideTheOwnThirdAndRefusesTheMiddleThirdWithoutChangingAnything`, `RefusalIsIndependentOfInputOrder`, `WholeDiscMustBeInsideTheOwnThird` |
| M12 | deployment: centre-only check (disc may straddle) | KILLED | `WholeDiscMustBeInsideTheOwnThird` |
| M13 | deployment: battle does not re-check a Thirds setup | KILLED | `BattleRefusesAThirdsSetupWithAMiddleThirdEntity` |
| M14 | deployment: Close normalizes side 1 to the mirror of side 0 (equalizing) | KILLED | `TwentyMetreExitDifferenceIsPreserved` |
| M15 | rule: threshold off by one (6 pulses) | KILLED | `AmbushWaitIsDeliberateWaiting`, `ConcealedWithAnEnemyInReachIsNotInactivity(d:`, `ExplicitHoldIsDefenceNotInactivity`, `GenuinelyInactiveHiddenFormationRoutsAfterExactlyFivePulses`, `HoldingAnObjectiveIsDefence(offset:`, `OscillatingHiddenFormationIsStationaryInEffect`, `PredicateCounterAndThreshold`, `ProgressInTheFifthPulseSavesItAndTheSixthIsTooLate(orderPulse:`, `ReplayReproducesARoutVerdictBitForBit`, `RoutedFormationLeavesByTheRoutMechanics` |
| M15b | rule: counter double-counts (routs after 3 pulses) | KILLED | `AmbushWaitIsDeliberateWaiting`, `ConcealedWithAnEnemyInReachIsNotInactivity(d:`, `ExplicitHoldIsDefenceNotInactivity`, `GenuinelyInactiveHiddenFormationRoutsAfterExactlyFivePulses`, `HoldingAnObjectiveIsDefence(offset:`, `MutualHiddenIdlenessIsAStandoffNotADoubleRout`, `OscillatingHiddenFormationIsStationaryInEffect`, `PredicateCounterAndThreshold`, `ProgressInTheFifthPulseSavesItAndTheSixthIsTooLate(orderPulse:`, `RoutedFormationLeavesByTheRoutMechanics` |
| M16 | rule: exemption (a) ignored | KILLED | `ConcealedWithAnEnemyInReachIsNotInactivity(d:` |
| M16b | rule: engagement reach off by one mm | KILLED | `ConcealedWithAnEnemyInReachIsNotInactivity(d:` |
| M17 | rule: explicit Hold not an exemption | KILLED | `AfterStartTheWholeFieldIsAccessible`, `AmbushWaitIsDeliberateWaiting`, `ExplicitHoldIsDefenceNotInactivity`, `GenuinelyInactiveHiddenFormationRoutsAfterExactlyFivePulses`, `HoldingAnObjectiveIsDefence(offset:`, `OscillatingHiddenFormationIsStationaryInEffect`, `ProgressInTheFifthPulseSavesItAndTheSixthIsTooLate(orderPulse:`, `RoutedFormationLeavesByTheRoutMechanics`, `LongMarchLivesAsLongAsTheStateProgresses` |
| M17b | rule: the default standing Hold counts as defence | KILLED | `AmbushWaitIsDeliberateWaiting`, `ConcealedWithAnEnemyInReachIsNotInactivity(d:`, `GenuinelyInactiveHiddenFormationRoutsAfterExactlyFivePulses`, `HoldingAnObjectiveIsDefence(offset:`, `MutualHiddenIdlenessIsAStandoffNotADoubleRout`, `ProgressInTheFifthPulseSavesItAndTheSixthIsTooLate(orderPulse:`, `RoutedFormationLeavesByTheRoutMechanics` |
| M17c | rule: objective radius strict (off by one) | KILLED | `HoldingAnObjectiveIsDefence(offset:` |
| M18 | rule: exemption (c) ignored | KILLED | `AmbushWaitIsDeliberateWaiting` |
| M18b | rule: ambush exemption survives a movement order | KILLED | `AmbushWaitIsDeliberateWaiting` |
| M19 | rule: mutual guard removed (double rout) | KILLED | `MutualHiddenIdlenessIsAStandoffNotADoubleRout` |
| M20 | rule: visibility ignored (seen formations counted as hidden) | KILLED | `VisibleIdleFormationsEndInAStandoff` |
| M21 | standoff: whole-field idle end removed | HANG(>400s: non-termination under this mutation) | – |
| M22 | rule: progress ignored | KILLED | `AfterStartTheWholeFieldIsAccessible`, `ConcealedWithAnEnemyInReachIsNotInactivity(d:`, `ProgressInTheFifthPulseSavesItAndTheSixthIsTooLate(orderPulse:`, `LongMarchLivesAsLongAsTheStateProgresses` |
| M24 | log: one verdict per pulse (unbounded growth) | KILLED | `LongMarchLivesAsLongAsTheStateProgresses` |
| M25 | an implicit 100-pulse ceiling | KILLED | `LongAttritionDuelEndsOnlyByElimination`, `LongMarchLivesAsLongAsTheStateProgresses` |
| M26 | detection: Slice C LOS (no canopy, no structures) | KILLED | `DetectionSeesOverTheCanopyFromAHillAndNotThroughABuilding` |
| M27 | missile: elevation sign flipped | KILLED | `MissileElevationCoverAndStructuresAreWiredExactly` |
| M28 | fire path: buildings do not stop missiles | KILLED | `FirePathCoverAndStructureBlocking`, `MissileElevationCoverAndStructuresAreWiredExactly` |

**Second verify question (ADR-015 §7.2: is the asserted property one the system ought to have?).** M-mutant kills assert the
lane-TR mechanics of §1–§4 (cited directions, Director rules DIR5 §2.6/§2.14). N-mutant kills assert the interplay rules
of §5.2: items 1, 2 and 4 follow directly from DIR5 ("active concealment", no premature end of a fight) and from not
changing lane N's mechanics; items 3 (guard over every domain) and 5 (bare point fire is not progress) are this lane's
design choices — the tests pin them, but whether they are the right rules is part of PROPOSALS P-TR1 and P-TR9. Not
re-run on the final tree (throttle): M1, M2, M4–M7, M9–M12, M15b–M18b, M20, M22, M24, M27, M28 — each mutated line is
present unchanged, exactly once, in the final tree (verified), but the surrounding code now includes the merge, so their
first-run kills are evidence for the pre-merge tree only (limitation 23).

**Findings.** [r1] This claim was wrong: the round-1 verifier found four survivors on `822f891` (blocked shot = attack,
descent charged, no per-step cohesion, concealment condition dropped — TR-V5, V6, V7, V4); each now has a semantic test and
is re-run as R1-M8, R1-M9, R1-M10, R1-M6/M7 (§12.4, all killed). Originally: None open on the final tree: M21 now fails fast (18 s, 22 tests) instead of hanging; N6 reproduces the merge
finding of §4.1 (the amphibious battle runs into lane N's 400-pulse harness guard) and is killed semantically by
`ABombardmentOfAnEmptyPointDoesNotKeepTheBattleAlive`.

---

## 9. Limitations (honest list)

1. Every terrain magnitude except Naismith's climb ratio is PLACEHOLDER; the ford effect in particular is very strong.
2. Flat-topped cells: height changes are steps at cell boundaries; slope coefficients use cell-centre gradients, so two
   entities in the same cell never see a slope, and a duel across a boundary sees the full step.
3. Anchor kinematics read the climb of the segment ahead once per 2 s step (a waypoint crossed mid-step does not re-read
   it); entity kinematics use the cell one cell-length ahead.
4. Cohesion is terrain-only (mean order cap of the members' cells); geometric dispersion (members out of their slots) does
   not enter it yet.
5. Charge impetus exists as a mechanism but no shipped profile carries an impetus (no value was invented).
6. Elevation does not extend sight range (no horizon model); missile range is unchanged by elevation (only hit chance).
7. Fire-path cover multiplies the hit chance after the clamp, so p can fall below HitMin (still > 0).
8. Healing under Rally is not counted as progress (unchanged Slice D definition); a rallying formation hidden for five
   pulses is not exempt unless it also holds explicitly / an objective.
9. The DIR5 rule exists in the TacticalResolver only (AutoResolver: lane P).
10. The event trace keeps every entry in memory (§4) — a practical, not a rule, bound.
11. The 20 m asymmetry mechanism (why it shifts outcomes) is not isolated.
12. Cross-platform equality of the hashes is untested (O-17, as before).
13. Terrain shared by both sides cancels (the Ambush parity scenario is bit-identical with terrain ON and OFF); the parity
    battery is therefore a weak terrain probe — a terrain battery needs scenarios where the ground differs between the sides.
14. D-079 D13 was applied to the Meeting 20 m asymmetry only; the CombinedArms anchor asymmetry is not classified here.
15. The Standoff end (whole-field idle; and the mutual guard when switched ON) leaves both forces in place — a draw-like
    outcome that is NOT one of DIR5 §2.6's ends; shipped as the default terminator, unratified (P-TR1). [r1]
16. Bench timings are from a shared host; only same-run ratios and the identical-outcome checks are meaningful.
17. Vessels and aircraft are not judged by the DIR5 rule (P-TR9); a hidden, idle fleet never routs — the battle ends as a
    Standoff once nothing progresses.
18. Terrain enters lane N's domain weapons only through sight: no cover factor for shells or bombs landing in a forest or a
    village, no footing/cohesion for vessels (not applicable) and no elevation term for domain fire (P-TR11).
19. Of the five domain sight call sites switched to `TerrainSight`, two have a dedicated test (vessel → land target, land →
    vessel); vessel → vessel and the two gunner-LOS sites rely on the shared function's tests.
20. Exemption (a) measures an enemy vessel's or aircraft's distance horizontally (as lane N's searcher clause), so an aircraft
    overhead counts as "in reach" of a concealed rifleman whatever its altitude.
21. The thirds rule does not constrain vessels, aircraft or embarked troops (P-TR10).
22. The bare-point-fire rule (§5.2 item 5) also applies to land crew-served guns of lane N: a hidden battery firing at an
    empty point for five pulses is not making progress and routs unless exempt (e.g. an explicit Hold, or a Bombard order —
    which is not a Hold). Whether a standing bombardment order should be an exemption is part of P-TR9.
23. The first-run kill records of the pre-merge mutants (§8.4) are evidence for the pre-merge tree only; on the final tree a
    prioritized subset was re-run (throttle).
24. [r1, V9] Rivers (Ford) and Marsh do NOT affect MISSILE effectiveness: the missile equation has no footing or cohesion
    term and both classes have transmission 1; they act on melee only (footing 1/2 both ways, OrderCap 1/2 via cohesion).
    The brief listed "charge and missile effectiveness (uphill/downhill, rivers, marsh, forest)"; for missiles only slope
    and forest/vegetation are implemented. Pinned by `MarshAndFordDoNotChangeTheMissileHitChance`; PROPOSAL P-TR12.
25. [r1, V4] Exemption (a) against an enemy VESSEL or AIRCRAFT in reach still uses only the member's own cell and personal
    concealment (the terrain-on-the-sight-line term is computed for land enemies only, at eye height). [r2, V5] The
    requirement is now pinned: `HiddenInTheOpenFromAShortSightedEnemyVesselIsNotConcealment` (open: routed; forest: exempt).
26. [r1, V1] March progress is measured on the formation's ANCHOR (its commanded position): members held back (separation,
    slower ground) do not stop it; a formation whose anchor reaches its goal while members trail is idle from then on.
27. [r1, V1] A vessel / aircraft formation steaming or flying toward a MoveTo point is not march progress for the field
    counter G (only lane N's own activity definition counts there; unchanged).
28. [r1, V10] `TacticalScenarios.Meeting` — the default scenario of ~20 tests, the Slice D bench and lane P's parity tests —
    still has the F1-corrected mirrored geometry; it is now labelled the engine-bias CONTROL and the tactical bench also
    runs `MeetingAsymmetric`, but the default was not re-pointed (P-TR6).
29. [r1, V3] With the mutual guard OFF by default, AI-vs-AI battles in which both sides wait hidden without an explicit Hold
    now end `BothEliminated` (every such formation routed) instead of a Standoff; `KnowledgeAi` does not order Hold (P-TR8).
30. [r1, V1] Goal minima are kept for every distinct goal cell a formation ever marched to (bounded by Width·Height per
    formation; a few entries in every measured battle) and appended to the state hash only when present.
31. [r1, V8] Exemption (c) vs the thirds: an Ambush posture is given at deployment, so under the thirds rule a deployment-time
    ambush can only wait inside its own third; a mid-field ambush must march there after the start and then hold under an
    explicit Hold (exemption (b)). The shipped parity `Ambush` places its ambushers in the middle third (it runs
    Unrestricted), so its results (§7) are not results of a DIR5-legal deployment.
32. [r2, V1] The march potential is the remaining length of the PLANNED path. A new plan (a new order, or an Attack whose
    target cell moved) is measured against the same goal cell's old minimum, so a re-plan toward an already-approached goal
    along a longer route is no progress until it beats that minimum by a cell (the P-TR3 reading, kept). The termination
    bound is larger than r1's (still finite: at most Width·Height goal cells per formation, each starting at most at the
    longest A* path, Width·Height·S·√2 plus the anchor's offset, and falling by >= S per event) — DERIVED from the argument
    in `GoalMinima`, not measured as a worst case.
33. [r2, V6] Deployment ids: an edit's entity ids must be unused by every other entity of the battle (any formation, any
    domain, generated ids Id·1000 + slot included); otherwise ids are free (no reserved ranges). A setup that already contains a
    duplicate is refused by `Check` / `Close` (DuplicateId), not by the battle constructor's exception.
34. [r2, V7] PROCESS: this lane's round-1 cleanup ran `rm -f /dev/shm/m7c/*.txt` — a wildcard OUTSIDE its own clone in the
    shared lane directory — and lane E's timing / output files (`E-base-*.txt`, `E-times.txt`, `E-<tag>-*.txt`) are gone.
    Not restorable here (no copy exists; re-measuring is lane E's call). Lane E's Release baseline counts survive in
    `/dev/shm/m7c/E-notes.md` line 12 (369 passed, 58 s); the build-output tails and per-run times do not. Round 2 removed
    nothing outside the clone except its own named scratch files.

## 10. Proposals for the Director (not decided here)

- **P-TR1 Standoff end condition.** [r1, V3: the mutual guard is now an option, default OFF — S304; the whole-field
  Standoff is still the shipped default terminator and still UNRATIFIED; it also ends battles of VISIBLE idle formations,
  which DIR5's hidden-only rule does not reach.] DIR5 lists "dead, retreated, routed or surrendered". When every remaining formation is
  exempt (holding or waiting) or visible but idle, something must end the battle; this lane ends it as a Standoff (nobody
  routed or removed) after five whole-field idle pulses, plus the mutual guard. Alternatives: (a) exemptions expire after
  K pulses; (b) the campaign attacker who does not attack is treated as having retreated. Tension to rule on: D-079 S304
  records the inactivity consequence as "routed or lost, per unit; not a draw or intact escape by default". The Standoff is
  not the consequence for an inactive unit (that is ROUTED), but it is a draw-like battle end when nobody is inactive in the
  DIR5 sense or when the routs would eliminate both sides at once (mutual guard).
- **P-TR2 Routed vs lost.** Implemented: routed (Slice D rout path, conserved). Alternative: lost (a Ledger loss mapping).
- **P-TR3 "Stationary" = goes nowhere** (oscillation counts as stationary). [r1] A purposeful march toward the goal of a
  movement order is progress (one running minimum per goal cell); re-covering ground toward goals already approached is not.
  [r2] "Toward" is measured along the planned path (remaining path length), so detours count.
- **P-TR4 Engagement reach** for exemption (a): weapon reach + one pulse of open-ground movement.
- **P-TR5 Deployment default.** The campaign/sandbox path must always deploy through `DeploymentPhase` (Thirds).
- **P-TR6 D13.** Classify the 20 m Meeting asymmetry as realistic (deployment) asymmetry; keep the mirrored control;
  report per side. [r1, V10] And: re-point the DEFAULT measurement scenario (`Meeting`, used by ~20 tests, the Slice D bench and
  lane P's AutoResolver parity tests) to the uncorrected geometry, or rename it `MeetingMirroredControl` — an integrator /
  lane P change (it moves their pins), not made here.
- **P-TR7 Trace memory.** Stream the trace hash for long battles (changes the hash format).
- **P-TR8 AI intent.** `KnowledgeAi` should order Hold explicitly when it means to hold.
- **P-TR9 DIR5 for vessels, aircraft and gun batteries.** Should a hidden, idle fleet or flight rout after five pulses
  (lane N owns the vessel rout mechanics), and should a standing Bombard order exempt a battery whose shots hit nothing?
  Implemented here: vessels and aircraft are not judged; a Bombard is not an exemption.
- **P-TR10 Thirds at sea and in the air.** DIR5 §2.14 defines thirds for the battlefield; whether vessels and aircraft (and
  troops aboard) are held to them is undefined. Implemented here: they are not.
- **P-TR11 Terrain cover against domain fire.** Should a forest or a village reduce the effect of shells, bombs and rockets
  on troops in it (a transmission / blast-shielding coefficient in lane N's area-fire equation)? Not implemented.
- **P-TR12 [r1, V9] Rivers and marsh in the missile equation.** Candidate mechanism (not implemented, needs a cited
  magnitude): the shooter's footing and the formations' cohesion as coefficients of A (archers wading a ford or standing in
  a marsh shoot worse) — mirroring the melee factors; or none, if the Director reads the brief's list as covering charge and
  missile together.
- **P-TR13 [r1, V8] Shipped scenarios that break the thirds.** Re-anchor the parity `Ambush` ambushers (lane P) and the
  amphibious shore defence (lane N) inside their own thirds, or label both "Unrestricted probe" scenarios; either changes
  those lanes' pins, so it is not done here.
- **P-TR14 [r1, V1] Attack marches.** Implemented reading: an Attack order marching on the target's last KNOWN position is a
  movement order like MoveTo (its approach is progress); only a TRACKED enemy's approach counted before. [r2, V3] Now pinned
  by a semantic test, `AnAttackMarchOnALastKnownPositionIsProgress` (§13), so a ruling either way can be checked: under this
  reading the hidden attacker is not idle during its 10-pulse march; under the alternative (mutant VM2) it is judged
  stationary while marching (§13.4).

## 11. Files

New: `Proto.M7/Spatial/TerrainEffects.cs`, `Proto.M7/Spatial/TerrainSight.cs`, `Proto.M7/Tactical/TerrainCombat.cs`,
`Proto.M7/Tactical/Deployment.cs`, `Proto.M7/Tactical/InactivityRule.cs`,
`Proto.M7/Tactical/TacticalBattle.HiddenStationaryDomain.cs`, `Proto.M7.Bench/TerrainBench.cs`, the test files
`TerrainSightAndClimbTests`, `TerrainTacticalTests`, `DeploymentThirdsTests`, `HiddenStationaryRuleTests`,
`NoPulseCeilingTests`, `MultiDomain/TerrainDomainInterplayTests`, this report. Edited (delimited with `[TR]` /
`[TR x lane N]`): `Spatial/Terrain.cs`, `Spatial/LineOfSight.cs` (one enum value), `Spatial/GridPathfinder.cs` (optional
climb), `Tactical/EdgeField.cs` (optional climb), `Tactical/TacticalBattle.cs`, `Tactical/TacticalBattle.DomainCombat.cs`
(lane N's file: five sight calls and the two bare-point-fire lines), `Tactical/TacticalTypes.cs` (`Standoff`),
`Tactical/BattleSetup.cs`, `Tactical/UnitProfiles.cs` (`Impetus`), `Tactical/TacticalScenarios.cs` (`MeetingAsymmetric`),
`Proto.M7.Bench/Program.cs` (one line), and the tests of §8.2.
[r1] New: `Proto.M7/Tactical/TacticalBattle.Round1.cs`, `Proto.M7.Tests/Tactical/VerificationRound1TRTests.cs`. Edited:
`Tactical/Deployment.cs` (`CheckEdit`, doc), `Tactical/TacticalBattle.cs` (five one-line `[TR]` call sites and doc),
`Tactical/BattleSetup.cs` (`MutualStandoffGuard`), `Tactical/InactivityRule.cs` and `Tactical/TacticalTypes.cs` (doc),
`Tactical/TacticalBattle.DomainMove.cs` (lane N's file: one line, V13), `Tactical/TacticalScenarios.cs` (doc),
`Proto.M7.Bench/TacticalBench.cs` (one case), and the tests of §12.4.
[r2] New: `Proto.M7.Tests/Tactical/VerificationRound2TRTests.cs`. Edited: `Tactical/TacticalBattle.Round1.cs` (`GoalMinima`
potential), `Tactical/Deployment.cs` (`DuplicateId`), `Tactical/InactivityRule.cs` and
`Tactical/TacticalBattle.HiddenStationaryDomain.cs` (doc), the two golden constants of §13.3. No shared file
(`TacticalBattle.cs`, `WarTheaterWorld.cs`, `Runner.cs`, `EventScheduler.cs`) was touched in round 2.

## Appendix — bench output (final tree)

```
== tactical round 1 TR-base load 2.57
  50v50 heavy_infantry seed 1: SideEliminated side=0 holds=1 pulses=3 events=830 dropped=0 92.8 ms (30.95 ms/pulse, 0.01 M ev/s) F1:50=30+18+2 F2:50=37+13+0
  50v50 heavy_infantry seed 2: BothEliminated side=-1 holds=-1 pulses=9 events=1329 dropped=0 158.9 ms (17.66 ms/pulse, 0.01 M ev/s) F1:50=35+15+0 F2:50=35+15+0
  50v50 heavy_infantry seed 3: SideEliminated side=0 holds=1 pulses=4 events=821 dropped=0 101.2 ms (25.30 ms/pulse, 0.01 M ev/s) F1:50=29+18+3 F2:50=37+13+0
  50v50 heavy_infantry seed 4: SideEliminated side=1 holds=0 pulses=3 events=899 dropped=0 77.9 ms (25.96 ms/pulse, 0.01 M ev/s) F1:50=34+16+0 F2:50=31+18+1
  50v50 heavy_infantry seed 5: SideEliminated side=0 holds=1 pulses=3 events=791 dropped=0 73.5 ms (24.51 ms/pulse, 0.01 M ev/s) F1:50=32+18+0 F2:50=36+14+0
  50v50 heavy_infantry seed 6: SideEliminated side=0 holds=1 pulses=3 events=891 dropped=0 72.5 ms (24.17 ms/pulse, 0.01 M ev/s) F1:50=30+18+2 F2:50=34+16+0
  50v50 heavy_infantry seed 7: SideEliminated side=0 holds=1 pulses=3 events=871 dropped=0 49.8 ms (16.59 ms/pulse, 0.02 M ev/s) F1:50=31+18+1 F2:50=34+16+0
  50v50 heavy_infantry seed 8: SideEliminated side=0 holds=1 pulses=3 events=861 dropped=0 45.7 ms (15.22 ms/pulse, 0.02 M ev/s) F1:50=32+18+0 F2:50=35+15+0
  50v50 heavy_infantry seed 9: SideEliminated side=1 holds=0 pulses=3 events=870 dropped=0 48.3 ms (16.11 ms/pulse, 0.02 M ev/s) F1:50=37+13+0 F2:50=32+18+0
  50v50 heavy_infantry seed 10: BothEliminated side=-1 holds=-1 pulses=3 events=944 dropped=0 45.1 ms (15.05 ms/pulse, 0.02 M ev/s) F1:50=30+20+0 F2:50=32+18+0
50v50 heavy_infantry: mean pulses 3.7, mean events 911, total 766 ms, median ms/pulse 24.17, max ms/pulse 30.95
  60v60 archers seed 1: SideEliminated side=0 holds=1 pulses=3 events=743 dropped=0 53.6 ms (17.87 ms/pulse, 0.01 M ev/s) F1:60=38+22+0 F2:60=39+21+0
  60v60 archers seed 2: SideEliminated side=0 holds=1 pulses=3 events=841 dropped=0 54.2 ms (18.06 ms/pulse, 0.02 M ev/s) F1:60=37+23+0 F2:60=42+18+0
  60v60 archers seed 3: BothEliminated side=-1 holds=-1 pulses=3 events=964 dropped=0 58.3 ms (19.44 ms/pulse, 0.02 M ev/s) F1:60=37+23+0 F2:60=38+22+0
  60v60 archers seed 4: SideEliminated side=1 holds=0 pulses=3 events=981 dropped=0 68.1 ms (22.70 ms/pulse, 0.01 M ev/s) F1:60=39+21+0 F2:60=38+22+0
  60v60 archers seed 5: SideEliminated side=0 holds=1 pulses=3 events=892 dropped=0 82.8 ms (27.61 ms/pulse, 0.01 M ev/s) F1:60=35+25+0 F2:60=39+21+0
  60v60 archers seed 6: SideEliminated side=0 holds=1 pulses=3 events=838 dropped=0 72.1 ms (24.04 ms/pulse, 0.01 M ev/s) F1:60=37+23+0 F2:60=39+21+0
  60v60 archers seed 7: BothEliminated side=-1 holds=-1 pulses=3 events=909 dropped=0 77.7 ms (25.90 ms/pulse, 0.01 M ev/s) F1:60=38+22+0 F2:60=37+23+0
  60v60 archers seed 8: SideEliminated side=0 holds=1 pulses=3 events=820 dropped=0 62.9 ms (20.98 ms/pulse, 0.01 M ev/s) F1:60=38+22+0 F2:60=44+16+0
  60v60 archers seed 9: SideEliminated side=1 holds=0 pulses=3 events=818 dropped=0 73.2 ms (24.39 ms/pulse, 0.01 M ev/s) F1:60=40+20+0 F2:60=38+22+0
  60v60 archers seed 10: SideEliminated side=0 holds=1 pulses=3 events=764 dropped=0 76.9 ms (25.64 ms/pulse, 0.01 M ev/s) F1:60=36+24+0 F2:60=40+20+0
60v60 archers: mean pulses 3.0, mean events 857, total 680 ms, median ms/pulse 24.04, max ms/pulse 27.61
  50v60 heavy_infantry vs archers seed 1: SideEliminated side=0 holds=1 pulses=3 events=715 dropped=0 44.7 ms (14.91 ms/pulse, 0.02 M ev/s) F1:50=30+20+0 F2:60=60+0+0
  50v60 heavy_infantry vs archers seed 2: SideEliminated side=0 holds=1 pulses=3 events=830 dropped=0 44.9 ms (14.96 ms/pulse, 0.02 M ev/s) F1:50=30+20+0 F2:60=60+0+0
  50v60 heavy_infantry vs archers seed 3: SideEliminated side=0 holds=1 pulses=3 events=622 dropped=0 44.2 ms (14.72 ms/pulse, 0.01 M ev/s) F1:50=30+20+0 F2:60=60+0+0
  50v60 heavy_infantry vs archers seed 4: SideEliminated side=0 holds=1 pulses=3 events=730 dropped=0 46.6 ms (15.52 ms/pulse, 0.02 M ev/s) F1:50=30+20+0 F2:60=60+0+0
  50v60 heavy_infantry vs archers seed 5: SideEliminated side=0 holds=1 pulses=3 events=738 dropped=0 62.3 ms (20.77 ms/pulse, 0.01 M ev/s) F1:50=30+20+0 F2:60=60+0+0
  50v60 heavy_infantry vs archers seed 6: SideEliminated side=0 holds=1 pulses=3 events=624 dropped=0 64.6 ms (21.53 ms/pulse, 0.01 M ev/s) F1:50=30+20+0 F2:60=60+0+0
  50v60 heavy_infantry vs archers seed 7: SideEliminated side=0 holds=1 pulses=3 events=745 dropped=0 71.0 ms (23.65 ms/pulse, 0.01 M ev/s) F1:50=30+20+0 F2:60=60+0+0
  50v60 heavy_infantry vs archers seed 8: SideEliminated side=0 holds=1 pulses=3 events=849 dropped=0 57.2 ms (19.05 ms/pulse, 0.01 M ev/s) F1:50=30+20+0 F2:60=60+0+0
  50v60 heavy_infantry vs archers seed 9: SideEliminated side=0 holds=1 pulses=3 events=732 dropped=0 56.9 ms (18.96 ms/pulse, 0.01 M ev/s) F1:50=30+20+0 F2:60=60+0+0
  50v60 heavy_infantry vs archers seed 10: SideEliminated side=0 holds=1 pulses=3 events=726 dropped=0 58.5 ms (19.51 ms/pulse, 0.01 M ev/s) F1:50=30+20+0 F2:60=60+0+0
50v60 heavy_infantry vs archers: mean pulses 3.0, mean events 731, total 551 ms, median ms/pulse 19.05, max ms/pulse 23.65
  110v110 combined arms seed 1: SideEliminated side=1 holds=0 pulses=4 events=2618 dropped=0 299.4 ms (74.86 ms/pulse, 0.01 M ev/s) F1:50=24+26+0 F2:60=39+21+0 F3:50=26+23+1 F4:60=37+23+0
  110v110 combined arms seed 2: SideEliminated side=1 holds=0 pulses=4 events=2031 dropped=0 259.2 ms (64.79 ms/pulse, 0.01 M ev/s) F1:50=25+25+0 F2:60=42+18+0 F3:50=28+22+0 F4:60=38+22+0
  110v110 combined arms seed 3: SideEliminated side=0 holds=1 pulses=4 events=2199 dropped=0 264.7 ms (66.18 ms/pulse, 0.01 M ev/s) F1:50=25+25+0 F2:60=38+22+0 F3:50=21+25+4 F4:60=52+8+0
  110v110 combined arms seed 4: SideEliminated side=1 holds=0 pulses=4 events=2101 dropped=0 250.6 ms (62.64 ms/pulse, 0.01 M ev/s) F1:50=23+27+0 F2:60=40+20+0 F3:50=23+26+1 F4:60=37+23+0
  110v110 combined arms seed 5: SideEliminated side=1 holds=0 pulses=4 events=2220 dropped=0 286.3 ms (71.58 ms/pulse, 0.01 M ev/s) F1:50=25+25+0 F2:60=39+21+0 F3:50=27+23+0 F4:60=37+23+0
  110v110 combined arms seed 6: SideEliminated side=0 holds=1 pulses=4 events=1919 dropped=0 269.6 ms (67.41 ms/pulse, 0.01 M ev/s) F1:50=27+22+1 F2:60=37+23+0 F3:50=28+22+0 F4:60=43+17+0
  110v110 combined arms seed 7: SideEliminated side=1 holds=0 pulses=4 events=2129 dropped=0 293.6 ms (73.41 ms/pulse, 0.01 M ev/s) F1:50=26+24+0 F2:60=44+16+0 F3:50=22+25+3 F4:60=36+24+0
  110v110 combined arms seed 8: SideEliminated side=1 holds=0 pulses=4 events=1818 dropped=0 262.0 ms (65.50 ms/pulse, 0.01 M ev/s) F1:50=28+22+0 F2:60=48+12+0 F3:50=25+25+0 F4:60=38+22+0
  110v110 combined arms seed 9: SideEliminated side=1 holds=0 pulses=4 events=2119 dropped=0 288.1 ms (72.03 ms/pulse, 0.01 M ev/s) F1:50=25+25+0 F2:60=46+14+0 F3:50=22+23+5 F4:60=38+22+0
  110v110 combined arms seed 10: BothEliminated side=-1 holds=-1 pulses=4 events=2945 dropped=0 273.0 ms (68.26 ms/pulse, 0.01 M ev/s) F1:50=25+25+0 F2:60=38+22+0 F3:50=20+29+1 F4:60=38+22+0
110v110 combined arms: mean pulses 4.0, mean events 2210, total 2747 ms, median ms/pulse 68.26, max ms/pulse 74.86
wall 5.741789174 s
== tactical round 1 TR load 2.44
  50v50 heavy_infantry seed 1: SideEliminated side=0 holds=1 pulses=3 events=830 dropped=0 122.9 ms (40.98 ms/pulse, 0.01 M ev/s) F1:50=30+18+2 F2:50=37+13+0
  50v50 heavy_infantry seed 2: BothEliminated side=-1 holds=-1 pulses=9 events=1329 dropped=0 239.4 ms (26.61 ms/pulse, 0.01 M ev/s) F1:50=35+15+0 F2:50=35+15+0
  50v50 heavy_infantry seed 3: SideEliminated side=0 holds=1 pulses=4 events=821 dropped=0 85.5 ms (21.37 ms/pulse, 0.01 M ev/s) F1:50=29+18+3 F2:50=37+13+0
  50v50 heavy_infantry seed 4: SideEliminated side=1 holds=0 pulses=3 events=899 dropped=0 86.0 ms (28.68 ms/pulse, 0.01 M ev/s) F1:50=34+16+0 F2:50=31+18+1
  50v50 heavy_infantry seed 5: SideEliminated side=0 holds=1 pulses=3 events=791 dropped=0 62.8 ms (20.95 ms/pulse, 0.01 M ev/s) F1:50=32+18+0 F2:50=36+14+0
  50v50 heavy_infantry seed 6: SideEliminated side=0 holds=1 pulses=3 events=891 dropped=0 64.1 ms (21.36 ms/pulse, 0.01 M ev/s) F1:50=30+18+2 F2:50=34+16+0
  50v50 heavy_infantry seed 7: SideEliminated side=0 holds=1 pulses=3 events=871 dropped=0 58.9 ms (19.64 ms/pulse, 0.01 M ev/s) F1:50=31+18+1 F2:50=34+16+0
  50v50 heavy_infantry seed 8: SideEliminated side=0 holds=1 pulses=3 events=861 dropped=0 58.5 ms (19.51 ms/pulse, 0.01 M ev/s) F1:50=32+18+0 F2:50=35+15+0
  50v50 heavy_infantry seed 9: SideEliminated side=1 holds=0 pulses=3 events=870 dropped=0 61.0 ms (20.35 ms/pulse, 0.01 M ev/s) F1:50=37+13+0 F2:50=32+18+0
  50v50 heavy_infantry seed 10: BothEliminated side=-1 holds=-1 pulses=3 events=944 dropped=0 65.6 ms (21.88 ms/pulse, 0.01 M ev/s) F1:50=30+20+0 F2:50=32+18+0
50v50 heavy_infantry: mean pulses 3.7, mean events 911, total 905 ms, median ms/pulse 21.37, max ms/pulse 40.98
  60v60 archers seed 1: SideEliminated side=0 holds=1 pulses=3 events=743 dropped=0 74.2 ms (24.73 ms/pulse, 0.01 M ev/s) F1:60=38+22+0 F2:60=39+21+0
  60v60 archers seed 2: SideEliminated side=0 holds=1 pulses=3 events=841 dropped=0 78.0 ms (25.99 ms/pulse, 0.01 M ev/s) F1:60=37+23+0 F2:60=42+18+0
  60v60 archers seed 3: BothEliminated side=-1 holds=-1 pulses=3 events=964 dropped=0 85.7 ms (28.55 ms/pulse, 0.01 M ev/s) F1:60=37+23+0 F2:60=38+22+0
  60v60 archers seed 4: SideEliminated side=1 holds=0 pulses=3 events=981 dropped=0 74.3 ms (24.78 ms/pulse, 0.01 M ev/s) F1:60=39+21+0 F2:60=38+22+0
  60v60 archers seed 5: SideEliminated side=0 holds=1 pulses=3 events=892 dropped=0 75.8 ms (25.28 ms/pulse, 0.01 M ev/s) F1:60=35+25+0 F2:60=39+21+0
  60v60 archers seed 6: SideEliminated side=0 holds=1 pulses=3 events=838 dropped=0 74.5 ms (24.85 ms/pulse, 0.01 M ev/s) F1:60=37+23+0 F2:60=39+21+0
  60v60 archers seed 7: BothEliminated side=-1 holds=-1 pulses=3 events=909 dropped=0 82.1 ms (27.38 ms/pulse, 0.01 M ev/s) F1:60=38+22+0 F2:60=37+23+0
  60v60 archers seed 8: SideEliminated side=0 holds=1 pulses=3 events=820 dropped=0 68.0 ms (22.66 ms/pulse, 0.01 M ev/s) F1:60=38+22+0 F2:60=44+16+0
  60v60 archers seed 9: SideEliminated side=1 holds=0 pulses=3 events=818 dropped=0 68.6 ms (22.88 ms/pulse, 0.01 M ev/s) F1:60=40+20+0 F2:60=38+22+0
  60v60 archers seed 10: SideEliminated side=0 holds=1 pulses=3 events=764 dropped=0 68.7 ms (22.91 ms/pulse, 0.01 M ev/s) F1:60=36+24+0 F2:60=40+20+0
60v60 archers: mean pulses 3.0, mean events 857, total 750 ms, median ms/pulse 24.85, max ms/pulse 28.55
  50v60 heavy_infantry vs archers seed 1: SideEliminated side=0 holds=1 pulses=3 events=715 dropped=0 83.9 ms (27.97 ms/pulse, 0.01 M ev/s) F1:50=30+20+0 F2:60=60+0+0
  50v60 heavy_infantry vs archers seed 2: SideEliminated side=0 holds=1 pulses=3 events=830 dropped=0 68.9 ms (22.97 ms/pulse, 0.01 M ev/s) F1:50=30+20+0 F2:60=60+0+0
  50v60 heavy_infantry vs archers seed 3: SideEliminated side=0 holds=1 pulses=3 events=622 dropped=0 56.7 ms (18.91 ms/pulse, 0.01 M ev/s) F1:50=30+20+0 F2:60=60+0+0
  50v60 heavy_infantry vs archers seed 4: SideEliminated side=0 holds=1 pulses=3 events=730 dropped=0 58.4 ms (19.46 ms/pulse, 0.01 M ev/s) F1:50=30+20+0 F2:60=60+0+0
  50v60 heavy_infantry vs archers seed 5: SideEliminated side=0 holds=1 pulses=3 events=738 dropped=0 65.0 ms (21.65 ms/pulse, 0.01 M ev/s) F1:50=30+20+0 F2:60=60+0+0
  50v60 heavy_infantry vs archers seed 6: SideEliminated side=0 holds=1 pulses=3 events=624 dropped=0 66.2 ms (22.06 ms/pulse, 0.01 M ev/s) F1:50=30+20+0 F2:60=60+0+0
  50v60 heavy_infantry vs archers seed 7: SideEliminated side=0 holds=1 pulses=3 events=745 dropped=0 59.4 ms (19.79 ms/pulse, 0.01 M ev/s) F1:50=30+20+0 F2:60=60+0+0
  50v60 heavy_infantry vs archers seed 8: SideEliminated side=0 holds=1 pulses=3 events=849 dropped=0 57.0 ms (19.00 ms/pulse, 0.01 M ev/s) F1:50=30+20+0 F2:60=60+0+0
  50v60 heavy_infantry vs archers seed 9: SideEliminated side=0 holds=1 pulses=3 events=732 dropped=0 62.6 ms (20.88 ms/pulse, 0.01 M ev/s) F1:50=30+20+0 F2:60=60+0+0
  50v60 heavy_infantry vs archers seed 10: SideEliminated side=0 holds=1 pulses=3 events=726 dropped=0 57.3 ms (19.10 ms/pulse, 0.01 M ev/s) F1:50=30+20+0 F2:60=60+0+0
50v60 heavy_infantry vs archers: mean pulses 3.0, mean events 731, total 635 ms, median ms/pulse 20.88, max ms/pulse 27.97
  110v110 combined arms seed 1: SideEliminated side=1 holds=0 pulses=4 events=2618 dropped=0 300.7 ms (75.19 ms/pulse, 0.01 M ev/s) F1:50=24+26+0 F2:60=39+21+0 F3:50=26+23+1 F4:60=37+23+0
  110v110 combined arms seed 2: SideEliminated side=1 holds=0 pulses=4 events=2031 dropped=0 314.2 ms (78.55 ms/pulse, 0.01 M ev/s) F1:50=25+25+0 F2:60=42+18+0 F3:50=28+22+0 F4:60=38+22+0
  110v110 combined arms seed 3: SideEliminated side=0 holds=1 pulses=4 events=2199 dropped=0 258.0 ms (64.50 ms/pulse, 0.01 M ev/s) F1:50=25+25+0 F2:60=38+22+0 F3:50=21+25+4 F4:60=52+8+0
  110v110 combined arms seed 4: SideEliminated side=1 holds=0 pulses=4 events=2101 dropped=0 262.5 ms (65.62 ms/pulse, 0.01 M ev/s) F1:50=23+27+0 F2:60=40+20+0 F3:50=23+26+1 F4:60=37+23+0
  110v110 combined arms seed 5: SideEliminated side=1 holds=0 pulses=4 events=2220 dropped=0 262.3 ms (65.58 ms/pulse, 0.01 M ev/s) F1:50=25+25+0 F2:60=39+21+0 F3:50=27+23+0 F4:60=37+23+0
  110v110 combined arms seed 6: SideEliminated side=0 holds=1 pulses=4 events=1919 dropped=0 273.8 ms (68.44 ms/pulse, 0.01 M ev/s) F1:50=27+22+1 F2:60=37+23+0 F3:50=28+22+0 F4:60=43+17+0
  110v110 combined arms seed 7: SideEliminated side=1 holds=0 pulses=4 events=2129 dropped=0 255.4 ms (63.86 ms/pulse, 0.01 M ev/s) F1:50=26+24+0 F2:60=44+16+0 F3:50=22+25+3 F4:60=36+24+0
  110v110 combined arms seed 8: SideEliminated side=1 holds=0 pulses=4 events=1818 dropped=0 303.6 ms (75.91 ms/pulse, 0.01 M ev/s) F1:50=28+22+0 F2:60=48+12+0 F3:50=25+25+0 F4:60=38+22+0
  110v110 combined arms seed 9: SideEliminated side=1 holds=0 pulses=4 events=2119 dropped=0 304.0 ms (75.99 ms/pulse, 0.01 M ev/s) F1:50=25+25+0 F2:60=46+14+0 F3:50=22+23+5 F4:60=38+22+0
  110v110 combined arms seed 10: BothEliminated side=-1 holds=-1 pulses=4 events=2945 dropped=0 290.5 ms (72.63 ms/pulse, 0.01 M ev/s) F1:50=25+25+0 F2:60=38+22+0 F3:50=20+29+1 F4:60=38+22+0
110v110 combined arms: mean pulses 4.0, mean events 2210, total 2825 ms, median ms/pulse 72.63, max ms/pulse 78.55
wall 6.448202309 s
== tactical round 2 TR-base load 2.22
  50v50 heavy_infantry seed 1: SideEliminated side=0 holds=1 pulses=3 events=830 dropped=0 73.3 ms (24.44 ms/pulse, 0.01 M ev/s) F1:50=30+18+2 F2:50=37+13+0
  50v50 heavy_infantry seed 2: BothEliminated side=-1 holds=-1 pulses=9 events=1329 dropped=0 140.8 ms (15.64 ms/pulse, 0.01 M ev/s) F1:50=35+15+0 F2:50=35+15+0
  50v50 heavy_infantry seed 3: SideEliminated side=0 holds=1 pulses=4 events=821 dropped=0 72.7 ms (18.17 ms/pulse, 0.01 M ev/s) F1:50=29+18+3 F2:50=37+13+0
  50v50 heavy_infantry seed 4: SideEliminated side=1 holds=0 pulses=3 events=899 dropped=0 84.9 ms (28.28 ms/pulse, 0.01 M ev/s) F1:50=34+16+0 F2:50=31+18+1
  50v50 heavy_infantry seed 5: SideEliminated side=0 holds=1 pulses=3 events=791 dropped=0 60.4 ms (20.12 ms/pulse, 0.01 M ev/s) F1:50=32+18+0 F2:50=36+14+0
  50v50 heavy_infantry seed 6: SideEliminated side=0 holds=1 pulses=3 events=891 dropped=0 61.0 ms (20.33 ms/pulse, 0.01 M ev/s) F1:50=30+18+2 F2:50=34+16+0
  50v50 heavy_infantry seed 7: SideEliminated side=0 holds=1 pulses=3 events=871 dropped=0 55.1 ms (18.37 ms/pulse, 0.02 M ev/s) F1:50=31+18+1 F2:50=34+16+0
  50v50 heavy_infantry seed 8: SideEliminated side=0 holds=1 pulses=3 events=861 dropped=0 51.9 ms (17.29 ms/pulse, 0.02 M ev/s) F1:50=32+18+0 F2:50=35+15+0
  50v50 heavy_infantry seed 9: SideEliminated side=1 holds=0 pulses=3 events=870 dropped=0 55.7 ms (18.58 ms/pulse, 0.02 M ev/s) F1:50=37+13+0 F2:50=32+18+0
  50v50 heavy_infantry seed 10: BothEliminated side=-1 holds=-1 pulses=3 events=944 dropped=0 63.6 ms (21.21 ms/pulse, 0.01 M ev/s) F1:50=30+20+0 F2:50=32+18+0
50v50 heavy_infantry: mean pulses 3.7, mean events 911, total 719 ms, median ms/pulse 20.12, max ms/pulse 28.28
  60v60 archers seed 1: SideEliminated side=0 holds=1 pulses=3 events=743 dropped=0 66.3 ms (22.10 ms/pulse, 0.01 M ev/s) F1:60=38+22+0 F2:60=39+21+0
  60v60 archers seed 2: SideEliminated side=0 holds=1 pulses=3 events=841 dropped=0 72.7 ms (24.23 ms/pulse, 0.01 M ev/s) F1:60=37+23+0 F2:60=42+18+0
  60v60 archers seed 3: BothEliminated side=-1 holds=-1 pulses=3 events=964 dropped=0 69.0 ms (22.99 ms/pulse, 0.01 M ev/s) F1:60=37+23+0 F2:60=38+22+0
  60v60 archers seed 4: SideEliminated side=1 holds=0 pulses=3 events=981 dropped=0 67.5 ms (22.50 ms/pulse, 0.01 M ev/s) F1:60=39+21+0 F2:60=38+22+0
  60v60 archers seed 5: SideEliminated side=0 holds=1 pulses=3 events=892 dropped=0 65.5 ms (21.85 ms/pulse, 0.01 M ev/s) F1:60=35+25+0 F2:60=39+21+0
  60v60 archers seed 6: SideEliminated side=0 holds=1 pulses=3 events=838 dropped=0 66.0 ms (22.01 ms/pulse, 0.01 M ev/s) F1:60=37+23+0 F2:60=39+21+0
  60v60 archers seed 7: BothEliminated side=-1 holds=-1 pulses=3 events=909 dropped=0 65.5 ms (21.85 ms/pulse, 0.01 M ev/s) F1:60=38+22+0 F2:60=37+23+0
  60v60 archers seed 8: SideEliminated side=0 holds=1 pulses=3 events=820 dropped=0 65.2 ms (21.74 ms/pulse, 0.01 M ev/s) F1:60=38+22+0 F2:60=44+16+0
  60v60 archers seed 9: SideEliminated side=1 holds=0 pulses=3 events=818 dropped=0 64.2 ms (21.40 ms/pulse, 0.01 M ev/s) F1:60=40+20+0 F2:60=38+22+0
  60v60 archers seed 10: SideEliminated side=0 holds=1 pulses=3 events=764 dropped=0 62.3 ms (20.75 ms/pulse, 0.01 M ev/s) F1:60=36+24+0 F2:60=40+20+0
60v60 archers: mean pulses 3.0, mean events 857, total 664 ms, median ms/pulse 22.01, max ms/pulse 24.23
  50v60 heavy_infantry vs archers seed 1: SideEliminated side=0 holds=1 pulses=3 events=715 dropped=0 55.5 ms (18.51 ms/pulse, 0.01 M ev/s) F1:50=30+20+0 F2:60=60+0+0
  50v60 heavy_infantry vs archers seed 2: SideEliminated side=0 holds=1 pulses=3 events=830 dropped=0 55.7 ms (18.57 ms/pulse, 0.01 M ev/s) F1:50=30+20+0 F2:60=60+0+0
  50v60 heavy_infantry vs archers seed 3: SideEliminated side=0 holds=1 pulses=3 events=622 dropped=0 53.2 ms (17.72 ms/pulse, 0.01 M ev/s) F1:50=30+20+0 F2:60=60+0+0
  50v60 heavy_infantry vs archers seed 4: SideEliminated side=0 holds=1 pulses=3 events=730 dropped=0 52.9 ms (17.64 ms/pulse, 0.01 M ev/s) F1:50=30+20+0 F2:60=60+0+0
  50v60 heavy_infantry vs archers seed 5: SideEliminated side=0 holds=1 pulses=3 events=738 dropped=0 54.0 ms (18.02 ms/pulse, 0.01 M ev/s) F1:50=30+20+0 F2:60=60+0+0
  50v60 heavy_infantry vs archers seed 6: SideEliminated side=0 holds=1 pulses=3 events=624 dropped=0 60.5 ms (20.17 ms/pulse, 0.01 M ev/s) F1:50=30+20+0 F2:60=60+0+0
  50v60 heavy_infantry vs archers seed 7: SideEliminated side=0 holds=1 pulses=3 events=745 dropped=0 55.0 ms (18.33 ms/pulse, 0.01 M ev/s) F1:50=30+20+0 F2:60=60+0+0
  50v60 heavy_infantry vs archers seed 8: SideEliminated side=0 holds=1 pulses=3 events=849 dropped=0 55.8 ms (18.61 ms/pulse, 0.02 M ev/s) F1:50=30+20+0 F2:60=60+0+0
  50v60 heavy_infantry vs archers seed 9: SideEliminated side=0 holds=1 pulses=3 events=732 dropped=0 54.5 ms (18.18 ms/pulse, 0.01 M ev/s) F1:50=30+20+0 F2:60=60+0+0
  50v60 heavy_infantry vs archers seed 10: SideEliminated side=0 holds=1 pulses=3 events=726 dropped=0 54.9 ms (18.30 ms/pulse, 0.01 M ev/s) F1:50=30+20+0 F2:60=60+0+0
50v60 heavy_infantry vs archers: mean pulses 3.0, mean events 731, total 552 ms, median ms/pulse 18.33, max ms/pulse 20.17
  110v110 combined arms seed 1: SideEliminated side=1 holds=0 pulses=4 events=2618 dropped=0 258.6 ms (64.66 ms/pulse, 0.01 M ev/s) F1:50=24+26+0 F2:60=39+21+0 F3:50=26+23+1 F4:60=37+23+0
  110v110 combined arms seed 2: SideEliminated side=1 holds=0 pulses=4 events=2031 dropped=0 250.0 ms (62.49 ms/pulse, 0.01 M ev/s) F1:50=25+25+0 F2:60=42+18+0 F3:50=28+22+0 F4:60=38+22+0
  110v110 combined arms seed 3: SideEliminated side=0 holds=1 pulses=4 events=2199 dropped=0 234.6 ms (58.64 ms/pulse, 0.01 M ev/s) F1:50=25+25+0 F2:60=38+22+0 F3:50=21+25+4 F4:60=52+8+0
  110v110 combined arms seed 4: SideEliminated side=1 holds=0 pulses=4 events=2101 dropped=0 246.8 ms (61.70 ms/pulse, 0.01 M ev/s) F1:50=23+27+0 F2:60=40+20+0 F3:50=23+26+1 F4:60=37+23+0
  110v110 combined arms seed 5: SideEliminated side=1 holds=0 pulses=4 events=2220 dropped=0 253.5 ms (63.36 ms/pulse, 0.01 M ev/s) F1:50=25+25+0 F2:60=39+21+0 F3:50=27+23+0 F4:60=37+23+0
  110v110 combined arms seed 6: SideEliminated side=0 holds=1 pulses=4 events=1919 dropped=0 257.9 ms (64.49 ms/pulse, 0.01 M ev/s) F1:50=27+22+1 F2:60=37+23+0 F3:50=28+22+0 F4:60=43+17+0
  110v110 combined arms seed 7: SideEliminated side=1 holds=0 pulses=4 events=2129 dropped=0 242.5 ms (60.62 ms/pulse, 0.01 M ev/s) F1:50=26+24+0 F2:60=44+16+0 F3:50=22+25+3 F4:60=36+24+0
  110v110 combined arms seed 8: SideEliminated side=1 holds=0 pulses=4 events=1818 dropped=0 233.4 ms (58.35 ms/pulse, 0.01 M ev/s) F1:50=28+22+0 F2:60=48+12+0 F3:50=25+25+0 F4:60=38+22+0
  110v110 combined arms seed 9: SideEliminated side=1 holds=0 pulses=4 events=2119 dropped=0 242.8 ms (60.69 ms/pulse, 0.01 M ev/s) F1:50=25+25+0 F2:60=46+14+0 F3:50=22+23+5 F4:60=38+22+0
  110v110 combined arms seed 10: BothEliminated side=-1 holds=-1 pulses=4 events=2945 dropped=0 268.6 ms (67.16 ms/pulse, 0.01 M ev/s) F1:50=25+25+0 F2:60=38+22+0 F3:50=20+29+1 F4:60=38+22+0
110v110 combined arms: mean pulses 4.0, mean events 2210, total 2489 ms, median ms/pulse 62.49, max ms/pulse 67.16
wall 5.530773265 s
== tactical round 2 TR load 2.12
  50v50 heavy_infantry seed 1: SideEliminated side=0 holds=1 pulses=3 events=830 dropped=0 117.2 ms (39.08 ms/pulse, 0.01 M ev/s) F1:50=30+18+2 F2:50=37+13+0
  50v50 heavy_infantry seed 2: BothEliminated side=-1 holds=-1 pulses=9 events=1329 dropped=0 201.9 ms (22.43 ms/pulse, 0.01 M ev/s) F1:50=35+15+0 F2:50=35+15+0
  50v50 heavy_infantry seed 3: SideEliminated side=0 holds=1 pulses=4 events=821 dropped=0 71.5 ms (17.87 ms/pulse, 0.01 M ev/s) F1:50=29+18+3 F2:50=37+13+0
  50v50 heavy_infantry seed 4: SideEliminated side=1 holds=0 pulses=3 events=899 dropped=0 68.4 ms (22.79 ms/pulse, 0.01 M ev/s) F1:50=34+16+0 F2:50=31+18+1
  50v50 heavy_infantry seed 5: SideEliminated side=0 holds=1 pulses=3 events=791 dropped=0 63.6 ms (21.21 ms/pulse, 0.01 M ev/s) F1:50=32+18+0 F2:50=36+14+0
  50v50 heavy_infantry seed 6: SideEliminated side=0 holds=1 pulses=3 events=891 dropped=0 60.0 ms (19.99 ms/pulse, 0.01 M ev/s) F1:50=30+18+2 F2:50=34+16+0
  50v50 heavy_infantry seed 7: SideEliminated side=0 holds=1 pulses=3 events=871 dropped=0 58.5 ms (19.50 ms/pulse, 0.01 M ev/s) F1:50=31+18+1 F2:50=34+16+0
  50v50 heavy_infantry seed 8: SideEliminated side=0 holds=1 pulses=3 events=861 dropped=0 56.7 ms (18.90 ms/pulse, 0.02 M ev/s) F1:50=32+18+0 F2:50=35+15+0
  50v50 heavy_infantry seed 9: SideEliminated side=1 holds=0 pulses=3 events=870 dropped=0 65.1 ms (21.70 ms/pulse, 0.01 M ev/s) F1:50=37+13+0 F2:50=32+18+0
  50v50 heavy_infantry seed 10: BothEliminated side=-1 holds=-1 pulses=3 events=944 dropped=0 59.0 ms (19.66 ms/pulse, 0.02 M ev/s) F1:50=30+20+0 F2:50=32+18+0
50v50 heavy_infantry: mean pulses 3.7, mean events 911, total 822 ms, median ms/pulse 21.21, max ms/pulse 39.08
  60v60 archers seed 1: SideEliminated side=0 holds=1 pulses=3 events=743 dropped=0 67.3 ms (22.44 ms/pulse, 0.01 M ev/s) F1:60=38+22+0 F2:60=39+21+0
  60v60 archers seed 2: SideEliminated side=0 holds=1 pulses=3 events=841 dropped=0 68.7 ms (22.91 ms/pulse, 0.01 M ev/s) F1:60=37+23+0 F2:60=42+18+0
  60v60 archers seed 3: BothEliminated side=-1 holds=-1 pulses=3 events=964 dropped=0 85.5 ms (28.52 ms/pulse, 0.01 M ev/s) F1:60=37+23+0 F2:60=38+22+0
  60v60 archers seed 4: SideEliminated side=1 holds=0 pulses=3 events=981 dropped=0 72.8 ms (24.26 ms/pulse, 0.01 M ev/s) F1:60=39+21+0 F2:60=38+22+0
  60v60 archers seed 5: SideEliminated side=0 holds=1 pulses=3 events=892 dropped=0 69.7 ms (23.22 ms/pulse, 0.01 M ev/s) F1:60=35+25+0 F2:60=39+21+0
  60v60 archers seed 6: SideEliminated side=0 holds=1 pulses=3 events=838 dropped=0 69.6 ms (23.18 ms/pulse, 0.01 M ev/s) F1:60=37+23+0 F2:60=39+21+0
  60v60 archers seed 7: BothEliminated side=-1 holds=-1 pulses=3 events=909 dropped=0 69.5 ms (23.17 ms/pulse, 0.01 M ev/s) F1:60=38+22+0 F2:60=37+23+0
  60v60 archers seed 8: SideEliminated side=0 holds=1 pulses=3 events=820 dropped=0 68.3 ms (22.76 ms/pulse, 0.01 M ev/s) F1:60=38+22+0 F2:60=44+16+0
  60v60 archers seed 9: SideEliminated side=1 holds=0 pulses=3 events=818 dropped=0 68.1 ms (22.69 ms/pulse, 0.01 M ev/s) F1:60=40+20+0 F2:60=38+22+0
  60v60 archers seed 10: SideEliminated side=0 holds=1 pulses=3 events=764 dropped=0 67.7 ms (22.56 ms/pulse, 0.01 M ev/s) F1:60=36+24+0 F2:60=40+20+0
60v60 archers: mean pulses 3.0, mean events 857, total 707 ms, median ms/pulse 23.17, max ms/pulse 28.52
  50v60 heavy_infantry vs archers seed 1: SideEliminated side=0 holds=1 pulses=3 events=715 dropped=0 56.8 ms (18.93 ms/pulse, 0.01 M ev/s) F1:50=30+20+0 F2:60=60+0+0
  50v60 heavy_infantry vs archers seed 2: SideEliminated side=0 holds=1 pulses=3 events=830 dropped=0 62.6 ms (20.86 ms/pulse, 0.01 M ev/s) F1:50=30+20+0 F2:60=60+0+0
  50v60 heavy_infantry vs archers seed 3: SideEliminated side=0 holds=1 pulses=3 events=622 dropped=0 57.0 ms (18.99 ms/pulse, 0.01 M ev/s) F1:50=30+20+0 F2:60=60+0+0
  50v60 heavy_infantry vs archers seed 4: SideEliminated side=0 holds=1 pulses=3 events=730 dropped=0 56.7 ms (18.89 ms/pulse, 0.01 M ev/s) F1:50=30+20+0 F2:60=60+0+0
  50v60 heavy_infantry vs archers seed 5: SideEliminated side=0 holds=1 pulses=3 events=738 dropped=0 56.8 ms (18.94 ms/pulse, 0.01 M ev/s) F1:50=30+20+0 F2:60=60+0+0
  50v60 heavy_infantry vs archers seed 6: SideEliminated side=0 holds=1 pulses=3 events=624 dropped=0 61.0 ms (20.35 ms/pulse, 0.01 M ev/s) F1:50=30+20+0 F2:60=60+0+0
  50v60 heavy_infantry vs archers seed 7: SideEliminated side=0 holds=1 pulses=3 events=745 dropped=0 55.9 ms (18.62 ms/pulse, 0.01 M ev/s) F1:50=30+20+0 F2:60=60+0+0
  50v60 heavy_infantry vs archers seed 8: SideEliminated side=0 holds=1 pulses=3 events=849 dropped=0 56.8 ms (18.95 ms/pulse, 0.01 M ev/s) F1:50=30+20+0 F2:60=60+0+0
  50v60 heavy_infantry vs archers seed 9: SideEliminated side=0 holds=1 pulses=3 events=732 dropped=0 56.4 ms (18.79 ms/pulse, 0.01 M ev/s) F1:50=30+20+0 F2:60=60+0+0
  50v60 heavy_infantry vs archers seed 10: SideEliminated side=0 holds=1 pulses=3 events=726 dropped=0 56.4 ms (18.80 ms/pulse, 0.01 M ev/s) F1:50=30+20+0 F2:60=60+0+0
50v60 heavy_infantry vs archers: mean pulses 3.0, mean events 731, total 576 ms, median ms/pulse 18.94, max ms/pulse 20.86
  110v110 combined arms seed 1: SideEliminated side=1 holds=0 pulses=4 events=2618 dropped=0 277.6 ms (69.40 ms/pulse, 0.01 M ev/s) F1:50=24+26+0 F2:60=39+21+0 F3:50=26+23+1 F4:60=37+23+0
  110v110 combined arms seed 2: SideEliminated side=1 holds=0 pulses=4 events=2031 dropped=0 255.6 ms (63.91 ms/pulse, 0.01 M ev/s) F1:50=25+25+0 F2:60=42+18+0 F3:50=28+22+0 F4:60=38+22+0
  110v110 combined arms seed 3: SideEliminated side=0 holds=1 pulses=4 events=2199 dropped=0 247.5 ms (61.88 ms/pulse, 0.01 M ev/s) F1:50=25+25+0 F2:60=38+22+0 F3:50=21+25+4 F4:60=52+8+0
  110v110 combined arms seed 4: SideEliminated side=1 holds=0 pulses=4 events=2101 dropped=0 234.8 ms (58.69 ms/pulse, 0.01 M ev/s) F1:50=23+27+0 F2:60=40+20+0 F3:50=23+26+1 F4:60=37+23+0
  110v110 combined arms seed 5: SideEliminated side=1 holds=0 pulses=4 events=2220 dropped=0 250.4 ms (62.61 ms/pulse, 0.01 M ev/s) F1:50=25+25+0 F2:60=39+21+0 F3:50=27+23+0 F4:60=37+23+0
  110v110 combined arms seed 6: SideEliminated side=0 holds=1 pulses=4 events=1919 dropped=0 261.4 ms (65.34 ms/pulse, 0.01 M ev/s) F1:50=27+22+1 F2:60=37+23+0 F3:50=28+22+0 F4:60=43+17+0
  110v110 combined arms seed 7: SideEliminated side=1 holds=0 pulses=4 events=2129 dropped=0 256.7 ms (64.17 ms/pulse, 0.01 M ev/s) F1:50=26+24+0 F2:60=44+16+0 F3:50=22+25+3 F4:60=36+24+0
  110v110 combined arms seed 8: SideEliminated side=1 holds=0 pulses=4 events=1818 dropped=0 249.4 ms (62.35 ms/pulse, 0.01 M ev/s) F1:50=28+22+0 F2:60=48+12+0 F3:50=25+25+0 F4:60=38+22+0
  110v110 combined arms seed 9: SideEliminated side=1 holds=0 pulses=4 events=2119 dropped=0 261.0 ms (65.24 ms/pulse, 0.01 M ev/s) F1:50=25+25+0 F2:60=46+14+0 F3:50=22+23+5 F4:60=38+22+0
  110v110 combined arms seed 10: BothEliminated side=-1 holds=-1 pulses=4 events=2945 dropped=0 277.0 ms (69.26 ms/pulse, 0.01 M ev/s) F1:50=25+25+0 F2:60=38+22+0 F3:50=20+29+1 F4:60=38+22+0
110v110 combined arms: mean pulses 4.0, mean events 2210, total 2571 ms, median ms/pulse 64.17, max ms/pulse 69.40
wall 5.882958137 s
== domain TR-base load 2.03
Proto.M7.Bench domain: seeds=5 cpu=4 os=Ubuntu 24.04.5 LTS
  galleys 5v5 (oar, ram) seed 1: SideEliminated side=0 holds=1 pulses=19 events=2747 141.1 ms (7.42 ms/pulse) F1:5=4+1+0 F2:5=4+1+0 people 2000/2000
  galleys 5v5 (oar, ram) seed 2: SideEliminated side=0 holds=1 pulses=19 events=2748 142.3 ms (7.49 ms/pulse) F1:5=4+1+0 F2:5=4+1+0 people 2000/2000
  galleys 5v5 (oar, ram) seed 3: SideEliminated side=1 holds=0 pulses=18 events=2640 96.0 ms (5.34 ms/pulse) F1:5=4+1+0 F2:5=4+1+0 people 2000/2000
  galleys 5v5 (oar, ram) seed 4: SideEliminated side=0 holds=1 pulses=19 events=2835 38.7 ms (2.03 ms/pulse) F1:5=4+1+0 F2:5=4+1+0 people 2000/2000
  galleys 5v5 (oar, ram) seed 5: SideEliminated side=1 holds=0 pulses=19 events=2846 34.4 ms (1.81 ms/pulse) F1:5=4+1+0 F2:5=4+1+0 people 2000/2000
galleys 5v5 (oar, ram): mean pulses 18.8, mean events 2763, total 452 ms, median ms/pulse 5.34, max ms/pulse 7.49
  sail line 5v5 (broadside, wind) seed 1: SideDisengaged side=0 holds=1 pulses=34 events=3374 96.9 ms (2.85 ms/pulse) F1:5=5+0+0 F2:5=4+1+0 people 6000/6000
  sail line 5v5 (broadside, wind) seed 2: SideDisengaged side=1 holds=0 pulses=49 events=4834 125.5 ms (2.56 ms/pulse) F1:5=4+1+0 F2:5=4+1+0 people 6000/6000
  sail line 5v5 (broadside, wind) seed 3: SideEliminated side=1 holds=0 pulses=61 events=6042 122.4 ms (2.01 ms/pulse) F1:5=4+1+0 F2:5=4+1+0 people 6000/6000
  sail line 5v5 (broadside, wind) seed 4: SideEliminated side=1 holds=0 pulses=51 events=5049 76.0 ms (1.49 ms/pulse) F1:5=5+0+0 F2:5=3+2+0 people 6000/6000
  sail line 5v5 (broadside, wind) seed 5: SideDisengaged side=0 holds=1 pulses=39 events=3875 53.7 ms (1.38 ms/pulse) F1:5=4+1+0 F2:5=4+1+0 people 6000/6000
sail line 5v5 (broadside, wind): mean pulses 46.8, mean events 4635, total 474 ms, median ms/pulse 2.01, max ms/pulse 2.85
  turret 5v5 (engine, long range) seed 1: SideEliminated side=0 holds=1 pulses=10 events=1806 9.0 ms (0.90 ms/pulse) F1:5=3+2+0 F2:5=4+1+0 people 1500/1500
  turret 5v5 (engine, long range) seed 2: SideEliminated side=0 holds=1 pulses=10 events=1664 9.4 ms (0.94 ms/pulse) F1:5=3+2+0 F2:5=4+1+0 people 1500/1500
  turret 5v5 (engine, long range) seed 3: SideEliminated side=0 holds=1 pulses=11 events=1895 9.9 ms (0.90 ms/pulse) F1:5=3+2+0 F2:5=4+1+0 people 1500/1500
  turret 5v5 (engine, long range) seed 4: SideEliminated side=1 holds=0 pulses=10 events=1819 9.9 ms (0.99 ms/pulse) F1:5=4+1+0 F2:5=3+2+0 people 1500/1500
  turret 5v5 (engine, long range) seed 5: SideEliminated side=1 holds=0 pulses=15 events=2536 13.3 ms (0.89 ms/pulse) F1:5=4+1+0 F2:5=3+2+0 people 1500/1500
turret 5v5 (engine, long range): mean pulses 11.2, mean events 1944, total 51 ms, median ms/pulse 0.90, max ms/pulse 0.99
  amphibious combined arms seed 1: SideDisengaged side=1 holds=0 pulses=14 events=2055 192.2 ms (13.73 ms/pulse) F10:2=2+0+0 F11:2=2+0+0 F12:50=42+8+0 F13:1=1+0+0 F14:1=0+1+0 F20:50=6+44+0 F21:5=0+5+0 F22:5=5+0+0 F23:2=0+2+0 F24:1=1+0+0 people 516/516
  amphibious combined arms seed 2: SideDisengaged side=1 holds=0 pulses=14 events=1940 165.0 ms (11.79 ms/pulse) F10:2=2+0+0 F11:2=2+0+0 F12:50=41+9+0 F13:1=1+0+0 F14:1=1+0+0 F20:50=4+46+0 F21:5=0+5+0 F22:5=5+0+0 F23:2=0+2+0 F24:1=1+0+0 people 516/516
  amphibious combined arms seed 3: SideDisengaged side=1 holds=0 pulses=14 events=1909 96.3 ms (6.88 ms/pulse) F10:2=2+0+0 F11:2=2+0+0 F12:50=40+10+0 F13:1=1+0+0 F14:1=1+0+0 F20:50=2+48+0 F21:5=0+5+0 F22:5=5+0+0 F23:2=0+2+0 F24:1=1+0+0 people 516/516
  amphibious combined arms seed 4: SideDisengaged side=1 holds=0 pulses=14 events=1896 114.8 ms (8.20 ms/pulse) F10:2=2+0+0 F11:2=2+0+0 F12:50=50+0+0 F13:1=1+0+0 F14:1=1+0+0 F20:50=1+49+0 F21:5=0+5+0 F22:5=5+0+0 F23:2=0+2+0 F24:1=1+0+0 people 516/516
  amphibious combined arms seed 5: SideEliminated side=1 holds=0 pulses=9 events=1286 55.4 ms (6.16 ms/pulse) F10:2=2+0+0 F11:2=2+0+0 F12:50=41+9+0 F13:1=1+0+0 F14:1=1+0+0 F20:50=0+50+0 F21:5=0+5+0 F22:5=3+2+0 F23:2=1+1+0 F24:1=1+0+0 people 516/516
amphibious combined arms: mean pulses 13.0, mean events 1817, total 624 ms, median ms/pulse 8.20, max ms/pulse 13.73
== domain TR load 2.03
Proto.M7.Bench domain: seeds=5 cpu=4 os=Ubuntu 24.04.5 LTS
  galleys 5v5 (oar, ram) seed 1: SideEliminated side=0 holds=1 pulses=19 events=2747 179.6 ms (9.45 ms/pulse) F1:5=4+1+0 F2:5=4+1+0 people 2000/2000
  galleys 5v5 (oar, ram) seed 2: SideEliminated side=0 holds=1 pulses=19 events=2748 75.1 ms (3.95 ms/pulse) F1:5=4+1+0 F2:5=4+1+0 people 2000/2000
  galleys 5v5 (oar, ram) seed 3: SideEliminated side=1 holds=0 pulses=18 events=2640 70.8 ms (3.93 ms/pulse) F1:5=4+1+0 F2:5=4+1+0 people 2000/2000
  galleys 5v5 (oar, ram) seed 4: SideEliminated side=0 holds=1 pulses=19 events=2835 80.8 ms (4.25 ms/pulse) F1:5=4+1+0 F2:5=4+1+0 people 2000/2000
  galleys 5v5 (oar, ram) seed 5: SideEliminated side=1 holds=0 pulses=19 events=2846 65.0 ms (3.42 ms/pulse) F1:5=4+1+0 F2:5=4+1+0 people 2000/2000
galleys 5v5 (oar, ram): mean pulses 18.8, mean events 2763, total 471 ms, median ms/pulse 3.95, max ms/pulse 9.45
  sail line 5v5 (broadside, wind) seed 1: Standoff side=-1 holds=-1 pulses=38 events=3742 113.1 ms (2.98 ms/pulse) F1:5=5+0+0 F2:5=4+1+0 people 6000/6000
  sail line 5v5 (broadside, wind) seed 2: Standoff side=-1 holds=-1 pulses=50 events=4926 122.7 ms (2.45 ms/pulse) F1:5=4+1+0 F2:5=4+1+0 people 6000/6000
  sail line 5v5 (broadside, wind) seed 3: SideEliminated side=1 holds=0 pulses=61 events=6042 101.5 ms (1.66 ms/pulse) F1:5=4+1+0 F2:5=4+1+0 people 6000/6000
  sail line 5v5 (broadside, wind) seed 4: SideEliminated side=1 holds=0 pulses=51 events=5049 71.3 ms (1.40 ms/pulse) F1:5=5+0+0 F2:5=3+2+0 people 6000/6000
  sail line 5v5 (broadside, wind) seed 5: Standoff side=-1 holds=-1 pulses=40 events=3967 66.0 ms (1.65 ms/pulse) F1:5=4+1+0 F2:5=4+1+0 people 6000/6000
sail line 5v5 (broadside, wind): mean pulses 48.0, mean events 4745, total 475 ms, median ms/pulse 1.66, max ms/pulse 2.98
  turret 5v5 (engine, long range) seed 1: SideEliminated side=0 holds=1 pulses=10 events=1806 9.0 ms (0.90 ms/pulse) F1:5=3+2+0 F2:5=4+1+0 people 1500/1500
  turret 5v5 (engine, long range) seed 2: SideEliminated side=0 holds=1 pulses=10 events=1664 8.9 ms (0.89 ms/pulse) F1:5=3+2+0 F2:5=4+1+0 people 1500/1500
  turret 5v5 (engine, long range) seed 3: SideEliminated side=0 holds=1 pulses=11 events=1895 9.8 ms (0.89 ms/pulse) F1:5=3+2+0 F2:5=4+1+0 people 1500/1500
  turret 5v5 (engine, long range) seed 4: SideEliminated side=1 holds=0 pulses=10 events=1819 9.6 ms (0.96 ms/pulse) F1:5=4+1+0 F2:5=3+2+0 people 1500/1500
  turret 5v5 (engine, long range) seed 5: SideEliminated side=1 holds=0 pulses=15 events=2536 18.0 ms (1.20 ms/pulse) F1:5=4+1+0 F2:5=3+2+0 people 1500/1500
turret 5v5 (engine, long range): mean pulses 11.2, mean events 1944, total 55 ms, median ms/pulse 0.90, max ms/pulse 1.20
  amphibious combined arms seed 1: Standoff side=-1 holds=-1 pulses=18 events=2518 250.6 ms (13.92 ms/pulse) F10:2=2+0+0 F11:2=2+0+0 F12:50=48+2+0 F13:1=1+0+0 F14:1=0+1+0 F20:50=6+44+0 F21:5=0+5+0 F22:5=5+0+0 F23:2=0+2+0 F24:1=1+0+0 people 516/516
  amphibious combined arms seed 2: Standoff side=-1 holds=-1 pulses=18 events=2428 192.8 ms (10.71 ms/pulse) F10:2=2+0+0 F11:2=2+0+0 F12:50=47+3+0 F13:1=1+0+0 F14:1=1+0+0 F20:50=4+46+0 F21:5=0+5+0 F22:5=5+0+0 F23:2=0+2+0 F24:1=1+0+0 people 516/516
  amphibious combined arms seed 3: Standoff side=-1 holds=-1 pulses=18 events=2353 132.6 ms (7.37 ms/pulse) F10:2=2+0+0 F11:2=2+0+0 F12:50=46+4+0 F13:1=1+0+0 F14:1=1+0+0 F20:50=2+48+0 F21:5=0+5+0 F22:5=5+0+0 F23:2=0+2+0 F24:1=1+0+0 people 516/516
  amphibious combined arms seed 4: Standoff side=-1 holds=-1 pulses=18 events=2344 164.9 ms (9.16 ms/pulse) F10:2=2+0+0 F11:2=2+0+0 F12:50=50+0+0 F13:1=1+0+0 F14:1=1+0+0 F20:50=1+49+0 F21:5=0+5+0 F22:5=5+0+0 F23:2=0+2+0 F24:1=1+0+0 people 516/516
  amphibious combined arms seed 5: SideEliminated side=1 holds=0 pulses=9 events=1277 51.5 ms (5.72 ms/pulse) F10:2=2+0+0 F11:2=2+0+0 F12:50=45+5+0 F13:1=1+0+0 F14:1=1+0+0 F20:50=0+50+0 F21:5=0+5+0 F22:5=3+2+0 F23:2=1+1+0 F24:1=1+0+0 people 516/516
amphibious combined arms: mean pulses 16.2, mean events 2184, total 792 ms, median ms/pulse 9.16, max ms/pulse 13.92
== terrain TR load 1.95
Proto.M7.Bench terrain: seeds=10 cpu=4 os=Ubuntu 24.04.5 LTS
== cost: open field vs mixed terrain (KnowledgeAi both sides) ==
  50v50 HI open   : mean pulses 3.7, mean events 911, median ms/pulse 23.41 (min 15.32, max 30.20), total 805 ms, ends [BothEliminated 2, SideEliminated 8]
  50v50 HI terrain: mean pulses 8.1, mean events 1230, median ms/pulse 15.60 (min 13.14, max 21.71), total 1270 ms, ends [BothEliminated 1, SideEliminated 9]
  110v110 CA open   : mean pulses 4.0, mean events 2210, median ms/pulse 69.34 (min 61.47, max 74.94), total 2714 ms, ends [BothEliminated 1, SideEliminated 9]
  110v110 CA terrain: mean pulses 4.5, mean events 3344, median ms/pulse 74.06 (min 44.44, max 82.28), total 3131 ms, ends [BothEliminated 1, SideEliminated 9]
== influence: 20 HI assault 20 HI holding the crest / far bank (seeds 1..8) ==
  flat : attacker killed 4, defender killed 75, net attacker loss -71, attacker held the field 8/8
  hill : attacker killed 54, defender killed 69, net attacker loss -15, attacker held the field 7/8
  ford : attacker killed 68, defender killed 2, net attacker loss 66, attacker held the field 0/8
== asymmetry: per-side outcomes (50v50 HI, KnowledgeAi both) ==
  mirrored Meeting (control)       : 40 seeds — west (side 0) lost 22, east (side 1) lost 16, undecided 2; killed west 661, east 647
  MeetingAsymmetric (20 m kept)    : 40 seeds — west (side 0) lost 9, east (side 1) lost 31, undecided 0; killed west 615, east 635
== parity scenarios (tactical side only): DIR5 hidden-stationary rule vs the legacy side-disengage rule ==
  ArchersVsInfantryForest  LegacySideDisengage : side0 held 20, side1 held 0, undecided 0; mean pulses 5.0; killed 380; inactivity routs 0; ends [SideEliminated 20]
  ArchersVsInfantryForest  HiddenStationary    : side0 held 20, side1 held 0, undecided 0; mean pulses 5.0; killed 380; inactivity routs 0; ends [SideEliminated 20]
  Ambush                   LegacySideDisengage : side0 held 7, side1 held 11, undecided 2; mean pulses 5.9; killed 668; inactivity routs 0; ends [BothEliminated 2, SideEliminated 18]
  Ambush                   HiddenStationary    : side0 held 7, side1 held 11, undecided 2; mean pulses 5.9; killed 668; inactivity routs 0; ends [BothEliminated 2, SideEliminated 18]
  EqualInfantry            LegacySideDisengage : side0 held 4, side1 held 12, undecided 4; mean pulses 3.6; killed 656; inactivity routs 0; ends [BothEliminated 4, SideEliminated 16]
  EqualInfantry            HiddenStationary    : side0 held 4, side1 held 12, undecided 4; mean pulses 3.6; killed 656; inactivity routs 0; ends [BothEliminated 4, SideEliminated 16]
== parity scenarios (tactical side only): lane-TR terrain coefficients ON vs the Slice-C-equivalent table (OFF) ==
  ArchersVsInfantryForest  terrain OFF: A held 20, B held 0, undecided 0; mean pulses 5.0; killed A 0, B 392
  ArchersVsInfantryForest  terrain ON : A held 20, B held 0, undecided 0; mean pulses 5.0; killed A 0, B 380
  Ambush                   terrain OFF: A held 7, B held 11, undecided 2; mean pulses 5.9; killed A 353, B 315
  Ambush                   terrain ON : A held 7, B held 11, undecided 2; mean pulses 5.9; killed A 353, B 315
== long battle: attrition duel, hit chance 1/2000 ==
  seed 17: SideEliminated pulses=367 events=55697 traceEntries=55697 (151.8/pulse) 43 ms
  seed 18: SideEliminated pulses=185 events=28107 traceEntries=28107 (151.9/pulse) 14 ms
  seed 19: SideEliminated pulses=312 events=47332 traceEntries=47332 (151.7/pulse) 23 ms
wall 47.631323049 s
```

---

## 12. Verification round 1 (verify-TR-r1, 2026-11-05)

Input: the verifier's 13 findings TR-V1 … TR-V13 against `822f891` (probe file `VerifyProbeTR.cs`, not part of the tree).
Rule applied: a finding is acted on only after it was REPRODUCED here against the tree. Reproduction used the verifier's
probes copied (uncommitted) into this clone and built on `822f891` (V1, V2, V4–V7), new probes on the tree with the fix lines
toggled off (V11, V13: the toggle was reverted and the restored files checked), reading of the cited code (V3, V8–V10, V12)
plus a measuring test where a property could be measured (V8, V9). No finding was refuted. Every fix is minimal, every fix
or gap has a semantic test, and the test counts, scan, mutants and bench below were measured on the final tree (commit of
this section). Statements elsewhere in this report that round 1 changed are marked **[r1]**.

### 12.1 Per-finding verdicts

| ID | Sev. | Verdict | Reproduction (MEASURED on `822f891` unless noted) | Change | Semantic test(s) | Mutant |
|---|---|---|---|---|---|---|
| V1 | major | REPRODUCED | P7: hidden HI marching on MoveTo Cell(3,30), anchor y 30,000 → 342,000 mm (78 m/pulse), idle counter 1, 2, 3, 4, verdict {pulse 4, None, routed}, SideEliminated after 5 pulses, 10 routed escapes | march progress: per-goal-cell anchor minima (§12.2) — DIR5 model only | `AHiddenFormationMarchingOnAMoveToOrderIsNotStationary`, `MarchProgressIsTrackedUnderTheDir5ModelOnly`, `OscillatingHiddenFormationIsStationaryInEffect` (re-pinned) | R1-M1, R1-M2 |
| V2 | major | REPRODUCED | P5 (Amphibious(42)): each of the edits 20, 21, 22, 23 refused with ANOTHER formation's MiddleThird entity; Close refused; formation 20 alone at the new anchor: Accepted. P6 (parity Ambush): side 0 re-proposing its own legal anchor refused with MiddleThird 2000 (side 1's entity) | `DeploymentZones.CheckEdit`: an edit is judged on the edited formation (zones, then overlaps with every other ground entity at its current position); `Close` still needs the whole deployment | `TwoInvalidFormationsAreRepairedOneEditAtATime`, `AnEditIsJudgedOnItsOwnPlacementOnly`, `ShippedScenariosAgainstTheThirdsRule` | R1-M3, R1-M4 |
| V3 | major | REPRODUCED (code + the pinning tests; DIR5 §2.6 list; D-079 S304) | (i) mutual guard: `MutualHiddenIdlenessIsAStandoffNotADoubleRout` pinned a draw for genuinely inactive formations of both sides; (ii) `VisibleIdleFormationsEndInAStandoff` pins a Standoff of VISIBLE idle formations | (i) FIXED: the guard is now `TacticalRules.MutualStandoffGuard`, default OFF (S304) — both sides rout, BothEliminated. (ii) NOT changed: the whole-field Standoff stays the shipped terminator (without it such battles never end — M21 HANG) but is now labelled everywhere as an UNRATIFIED lane end (P-TR1); §0 / §4 claims corrected | `MutualHiddenIdlenessRoutsBothSidesUnlessTheGuardIsOn`, `TheMutualGuardFiresOnlyWhenTheRoutsWouldLeaveNeitherSideAnActiveEntity` (guard ON explicitly + default OFF case) | R1-M5 |
| V4 | major | REPRODUCED | P2: HI in the open behind a building column, enemy 40 m away (inside its 79,200 mm reach), never detected: verdict {pulse 4, None, routed}, SideEliminated. Verifier's mutant (concealment condition removed) survived 554/554 | exemption (a) "concealed FROM the enemy in reach": own cell, personal concealment, or terrain on that enemy's sight line (`TerrainSight` not visible or clarity < 1) | `HiddenBehindABuildingOrACrestWithAnEnemyInReachIsConcealedByTerrain` (building, 10 m crest), `HiddenInTheOpenFromAShortSightedEnemyIsNotConcealment` (teeth) | R1-M6, R1-M7 |
| V5 | minor | REPRODUCED (test gap; behaviour correct) | P1: archer behind a building firing at a tracked target: Standoff after 6 pulses; under the verifier's mutant (blocked shot = attack) the suite still passed 554/554 and P1 hit its 100-pulse guard | test only | `ShotsBlockedByABuildingAreNotActivity` (Fire events executed, 0 attacks, idle 0…5, Standoff at 6 pulses) | R1-M8 |
| V6 | minor | REPRODUCED (test gap) | P3: anchor 5,200 mm in two downhill steps; the verifier's "descent charged" mutant survived | test only | `AnAnchorDescendsAtTheFlatPaceExactly` | R1-M9 |
| V7 | minor | REPRODUCED (test gap) | P4: cohesion 2,147,483,648 (= 1/2) after three pulses into a forest; the verifier's "no per-step cohesion" mutant survived | test only | `CohesionIsRecomputedAsTheFormationMovesIntoAForest` | R1-M10 |
| V8 | minor | REPRODUCED | `DeploymentZones.Check` on every shipped setup: parity Ambush (both orientations) MiddleThird 2000; `Amphibious(42)` MiddleThird 20000; all other parity ids, `Meeting`, `MeetingAsymmetric`, `CombinedArms`: accepted | report (§7, limitation 31, P-TR13) + pin test; no scenario changed (lane P / lane N files and pins) | `ShippedScenariosAgainstTheThirdsRule` (also: the amphibious defence is repaired by four edits and the phase closes) | R1-M3 |
| V9 | minor | REPRODUCED | `TerrainCombat`: missile p = clamp(A·E/(A·E+D))·T, no footing / cohesion term; table: Marsh and Ford transmission 1. MEASURED: an archer's hit chance 75/120 with the target or the shooter in Open, Marsh or Ford | §0 row and limitation 24 corrected; PROPOSAL P-TR12 (no mechanism invented: no cited magnitude) | `MarshAndFordDoNotChangeTheMissileHitChance` (pins the limitation; Forest control 3/4) | – (pin of an absence) |
| V10 | minor | REPRODUCED | `TacticalScenarios.Meeting` = `MirrorColumn(14)` with the F1 equalizing rationale in its comment; `TacticalBench` measured only it | `Meeting` relabelled the mirrored ENGINE-BIAS CONTROL (DR32 supersedes the F1 rationale); `TacticalBench` also runs `MeetingAsymmetric`; the default NOT re-pointed (moves ~20 tests, the Slice D bench and lane P's AutoResolver parity pins) — P-TR6 extended, limitation 28 | existing `TwentyMetreExitDifferenceIsPreserved` | – |
| V11 | nit | REPRODUCED | probe on the tree with the fix toggled off: Hold (p0) → Embark (p1) → Disembark to a walled-off point (p3): landing without path → implicit Hold → verdict {pulse 10, DefensiveHold, not routed}; the same without the earlier Hold: {pulse 10, None, routed} | an accepted order on lane N's domain path clears `HoldExplicit` (one call site in `ApplyOrder`) | `AnAcceptedDomainOrderClearsTheExplicitHold` (with / without the earlier Hold: both routed, no exemption) | R1-M11 |
| V12 | nit | REPRODUCED | `DeploymentZones.Check`: the zone loop returns before the overlap loop; the doc said one ascending sweep | doc and report §2 corrected (the deterministic two-pass order is kept) | – (documentation) | – |
| V13 | nit | REPRODUCED | probe with the fix toggled off: 12 of 20 landed riflemen carried a stale LastStepMm of 900–3,900 mm into the landing step (no effect today: no profile has an impetus) | `LastStepMm = 0` at landing (lane N's file, one line); the run-up of IMPETUS entities is appended to the state hash when any exists | `ALandedEntityHasNoRunUp` | R1-M12 |

Refuted: none. Not done: V3 (ii) and V10's default re-pointing are Director / integrator choices (P-TR1, P-TR6); V9's
missing mechanism is a proposal (P-TR12).

### 12.2 What changed, as equations

**V1 — march progress (DIR5 model).** [r2: the potential d below was replaced by the remaining path length — §13.2; this
r1 form grows on detours, verify-TR-r2 V1.] For a formation f whose directive is MoveTo or Attack and whose path P is not empty, at
every step after the anchor moved: g = cell(P[last]) (the MoveTo destination, or the cell an Attack marches on),
d = |Anchor(f) − centre(g)|, and

    Track_f(g, d):  first measurement of g -> best_f(g) := d (no progress);  else if d <= best_f(g) − S -> best_f(g) := d, PROGRESS

The flag joins Progress_p(f) and the field counter G. Minima are per goal cell and never reset, so returning toward an
already-approached goal is no progress (P-TR3 kept: `OscillatingHiddenFormationIsStationaryInEffect` now routs at the end
of pulse 6 instead of 4, after its first approach to each of its two goals). Termination stays controller-independent: at most
Width·Height goal cells per formation, each a monotone integer potential falling by >= S per event. Reading (P-TR14): an
Attack's march on the target's last KNOWN position counts as a movement order. The legacy model tracks nothing (its goldens
and the parity battery subset are unchanged — MEASURED: the suite passes with the old parity hashes).

**V2 — edit check.** `CheckEdit(edited, others)`: PASS 1 over the edited entities in ascending id (range, passability, zone);
PASS 2 the pairs (a, b), a edited, b any other placement, in ascending (a, b), reported by a. `Propose` uses it; `Close`
still requires `Check` of the whole deployment.

**V3 — mutual guard.** `mutual = MutualStandoffGuard ∧ routCount > 0 ∧ NoSideKeepsAnActiveEntityAfter(rout)`; default
false. Default outcome for "both sides hidden, idle, unexempt": both routed, `BothEliminated`, every entity a routed escape.

**V4 — exemption (a).** EXISTS Active member e, Active land enemy x with |x − e| <= R(e) and Concealed(e, x), where
Concealed(e, x) := Concealment(cell e) < 1 ∨ Personal(e) < 1 ∨ ¬Visible(x→e) ∨ Clarity(x→e) < 1 (TerrainSight at eye
height); or an on-field enemy vessel / aircraft within R(e) and Concealment(cell e) < 1 ∨ Personal(e) < 1 (unchanged).

**V11 / V13** as in the table. **Encoding:** `AppendRound1State` appends, after lane N's domain state and only when present,
the goal minima (count, then formation id, goal cell, minimum) and the run-up of impetus entities (count, id, LastStepMm).

### 12.3 Moved goldens (each attributed, MEASURED)

| Golden | Old → new | Cause |
|---|---|---|
| `ReplayAndMetamorphicTests.GoldenStateHash` (CombinedArms 42) | 92EA64F3… → 81492070… | encoding only: with `AppendRound1State` disabled the old value is reproduced exactly; the trace hash E53AA685… did not move |
| `DomainOrdersAndGoldenTests.AmphibState` (lane N) | 736BB506… → 4F258FDC… | encoding only (same measurement); `AmphibTrace` 075371D6… and `AmphibPersonnel` 6B252A73… did not move |
| `HiddenStationaryRuleTests.OscillatingHiddenFormationIsStationaryInEffect` | 5 → 7 pulses | V1 (first approach to each goal is progress) |
| `HiddenStationaryRuleTests.MutualHiddenIdleness…` (renamed …RoutsBothSidesUnlessTheGuardIsOn) | Standoff → BothEliminated by default; Standoff with the guard ON | V3 |
| `TerrainDomainInterplayTests.TheMutualGuardFires…` | runs with the guard ON explicitly; default case added | V3 |

No other test moved; the parity battery golden subset did not move.

### 12.4 Validation (MEASURED on the final tree)

- **Builds:** `M7Proto.slnx` Debug and Release, `nice -n 19`, `-m:1`: 0 warnings, 0 errors. (Only this solution's projects
  changed; M7Econ / M7DetMath do not reference them.)
- **Tests:** Debug **569 / 0 / 0**, Release **569 / 0 / 0** (baseline `7118ef0` 487 / 0 / 0; `822f891` 554 / 0 / 0). +15 =
  the 15 cases of `VerificationRound1TRTests` (12 methods; two theories with two cases each); no test removed.
- **Determinism scan** (`scripts/check-proto-determinism.sh`): **CLEAN** (65 files under `Proto.M7/`).
- **Mutants** (one Release full-suite run each, bound 130 s = 10 × the clean Release suite of 13 s, files restored and
  checked after each; source hashes of all 115 `.cs` files equal before and after the run):

  | Mutant | What it breaks | Verdict | Suite | Killing tests (semantic in bold; golden-only kills not counted) |
  |---|---|---|---:|---|
  | R1-M1 | V1: march progress never set | **KILLED** | 17s | **`HiddenStationaryRuleTests.OscillatingHiddenFormationIsStationaryInEffect`**, **`VerificationRound1TRTests.AHiddenFormationMarchingOnAMoveToOrderIsNotStationary`** |
  | R1-M2 | V1: goal minima reset on every new goal (only the latest goal remembered) | **KILLED** | 17s | `DomainOrdersAndGoldenTests.AmphibiousBattleReplaysExactlyFromItsOrderLogAndMatchesTheGolden`, **`HiddenStationaryRuleTests.OscillatingHiddenFormationIsStationaryInEffect`**, `ReplayAndMetamorphicTests.ReplayFromRecordedOrdersReproducesTraceAndResult`, **`VerificationFixDTests.ReacquiringALostEnemyIsNotNewActivity`** |
  | R1-M3 | V2: an edit is judged on the whole deployment again (every formation treated as edited) | **KILLED** | 16s | **`VerificationRound1TRTests.AnEditIsJudgedOnItsOwnPlacementOnly`**, **`VerificationRound1TRTests.ShippedScenariosAgainstTheThirdsRule`**, **`VerificationRound1TRTests.TwoInvalidFormationsAreRepairedOneEditAtATime`** |
  | R1-M4 | V2: edit overlap check skipped | **KILLED** | 14s | **`VerificationRound1TRTests.AnEditIsJudgedOnItsOwnPlacementOnly`** |
  | R1-M5 | V3: mutual guard ON by default | **KILLED** | 16s | **`HiddenStationaryRuleTests.MutualHiddenIdlenessRoutsBothSidesUnlessTheGuardIsOn`** |
  | R1-M6 | V4: concealment condition of (a) dropped (any enemy in reach exempts) | **KILLED** | 16s | **`VerificationRound1TRTests.HiddenInTheOpenFromAShortSightedEnemyIsNotConcealment`** |
  | R1-M7 | V4: terrain on the sight line ignored (own cell / personal only — the pre-round-1 rule) | **KILLED** | 17s | **`VerificationRound1TRTests.HiddenBehindABuildingOrACrestWithAnEnemyInReachIsConcealedByTerrain (building cases`** |
  | R1-M8 | V5: a shot blocked by a building counts as an attack | **KILLED** | 18s | **`VerificationRound1TRTests.ShotsBlockedByABuildingAreNotActivity`** |
  | R1-M9 | V6: descent charged like ascent | **KILLED** | 16s | **`VerificationRound1TRTests.AnAnchorDescendsAtTheFlatPaceExactly`** |
  | R1-M10 | V7: cohesion not recomputed per step | **KILLED** | 17s | **`VerificationRound1TRTests.CohesionIsRecomputedAsTheFormationMovesIntoAForest`** |
  | R1-M11 | V11: accepted domain orders keep a stale explicit Hold | **KILLED** | 18s | **`VerificationRound1TRTests.AnAcceptedDomainOrderClearsTheExplicitHold (holdFirst cases`** |
  | R1-M12 | V13: landed entities keep their last run-up | **KILLED** | 16s | **`VerificationRound1TRTests.ALandedEntityHasNoRunUp`** |

  Second verify question (ADR-015 §7.2): R1-M1/M2 kills assert DIR5's "stationary" (a marching unit is not) and P-TR3's
  anti-oscillation reading; R1-M3/M4 assert DIR5 §2.14 "players can edit deployments" without letting an edit overlap;
  R1-M5 asserts S304's default; R1-M6/M7 assert DIR5's "distinguish … active concealment" (by terrain, incl. structures and
  crests, but not mere invisibility in the open); R1-M8 asserts termination (a blocked shot is no activity); R1-M9/M10 assert
  the cited Naismith rule and the cohesion mechanism; R1-M11/M12 assert that state cannot leak across lane N's paths.
- **Bench** (small, Release, `nice -n 19`, shared host — single numbers vary by tens of percent):

  | Case (mean pulses / events) | before round 1 (`822f891`, §6: r1 / r2 ms) | after round 1 (ms) |
  |---|---|---:|
  | 50v50 heavy infantry, mirrored control (3.7 / 911) | 905 / 822 | 912 |
  | 50v50 heavy infantry, 20 m asymmetric — new bench case [r1, V10] (5.7 / 1,029) | – | 768 |
  | 60v60 archers (3.0 / 857) | 750 / 707 | 889 |
  | 50v60 HI vs archers (3.0 / 731) | 635 / 576 | 597 |
  | 110v110 combined arms (4.0 / 2,210) | 2,825 / 2,571 | 2,745 |
  | **sum of the four old cases** | **5,115 / 4,676** | **5,143** |

  `Proto.M7.Bench tactical 10`, `terrain` (48 s wall) and `domain 5`, load average 1.7–2.7. **Same battles:** every one of the
  60 per-seed result lines of the old cases (40 tactical, 20 domain: end reason, side, pulses, events, dropped, per-formation
  survivors / killed / surrendered) is identical to the pre-round-1 appendix, and so is the outcome part of all 19 summary
  lines of the terrain bench (cost-case pulses / events / ends, influence: flat −71 / hill −15 / ford +66, asymmetry 22–16–2
  vs 9–31–0, parity under both models and both tables) — MEASURED (`comm` of the normalized logs: no new line). So no shipped bench battle had a hidden marcher, a both-sides-idle double
  rout, a terrain-occluded concealment or a deployment phase in it. Cost: the round-1 sum 5,143 ms is 0.5 % above the slower
  pre-round-1 run (5,115 / 4,676 ms; round-to-round noise ≈ 9 %) — no cost this small bench can resolve from the per-step
  march tracking (one cell lookup,
  one distance and one binary search per moving formation per step). Domain: galleys 470, sail 468, turret 55, amphibious
  667 ms (pre-round-1 471 / 475 / 55 / 792). Full log: `scratchpad/m7c/TR-work/r1-bench.txt` (session scratchpad).

## 13. Verification round 2 (verify-TR-r2, 2026-11-05)

Input: the verifier's 8 findings TR2-V1 … TR2-V8 against `3f71cc8` (probe file `VerifyR2ProbeTR.cs`, not part of the tree).
Rule applied: a finding is acted on only after it was REPRODUCED here against the tree. Reproduction used the verifier's probe
file copied (uncommitted, deleted afterwards) into this clone and run on `3f71cc8` (V1, V2, V6); the test gaps V2–V5 were
reproduced by running the verifier's mutants on the round-2 tree — each of VM1, VM3 and VM5 fails ONLY the new round-2 test
and VM2 only the new test plus two state-hash goldens, so none of the 569 pre-round-2 tests kills them semantically; V7 by
listing the shared directory and reading lane E's scripts and notes; V8 by reading the cited lines against DIR5 §2.6
(`director-decisions-2026-11-05.md` line 156). No finding was refuted. Every number below was measured on the final tree (the
commit of this section) unless marked as measured on `3f71cc8`. Statements changed by round 2 are marked **[r2]**.

### 13.1 Per-finding verdicts

| ID | Sev. | Verdict | Reproduction (MEASURED) | Change | Semantic test(s) | Mutant(s) |
|---|---|---|---|---|---|---|
| V1 | major | REPRODUCED | probe P3 on `3f71cc8`: one hidden HI ordered MoveTo Cell(8,10) from Cell(4,10); kind 0 wall at x = 6 (gap y 30–31), kind 1 river at x = 6 (Ford y 28): anchor and entity co-located, marching north 78 m per pulse, straight distance to the goal 80,000 → 111,731 → 175,316 → 247,297 → 322,093 mm, idle counter 1, 2, 3, 4, 5, verdict {pulse 4, None, routed}, SideEliminated after 5 pulses, the gap / ford never reached; open control (kind 2) routed at the end of pulse 5 | march potential = remaining PLANNED PATH length (`GoalMinima.Remaining`, §13.2) | `AHiddenFormationMarchingAlongADetourIsNotStationary` (3 cases): kinds 0 / 1 idle 0 at the starts of pulses 0–11 (on the goal from pulse 11), 1..4 at 12–15, routed at the end of pulse 15 (16 pulses); control unchanged | R2-M1, R2-M2 |
| V2 | minor | REPRODUCED (test gap; behaviour correct) | P2 on `3f71cc8`: forest column → {pulse 4, ConcealedEnemyInReach, not routed}, Standoff after 5 pulses; no forest → {pulse 5, None, routed}, SideEliminated after 6 — the verifier's values; VM1 on the r2 tree fails only the new test | test only | `VegetationOnTheEnemySightLineIsConcealmentByTerrain` (forest / none; also asserts that the enemy's line to A is VISIBLE with clarity 2^-20, i.e. the clarity clause, not the blocked-line clause) | VM1 |
| V3 | minor | REPRODUCED (test gap) | VM2 on the r2 tree: apart from the new test only `AmphibiousBattleReplays…` and `ReplayFromRecordedOrders…` (state hashes) fail | test only; P-TR14 stays a PROPOSAL | `AnAttackMarchOnALastKnownPositionIsProgress` | VM2 |
| V4 | minor | REPRODUCED (test gap; the path is reachable) | budget N = 1: an explicit Hold and an Embark in the same pulse → the Embark is logged {Invalid None, Budget TotalExhausted} (domain path, `TryApplyDomainOrder` returns true); VM3 fails only the new test | test only | `ABudgetRefusedDomainOrderKeepsTheExplicitHold` (HoldExplicit still true after the refusal; verdict {pulse 4, DefensiveHold, not routed}) | VM3 |
| V5 | minor | REPRODUCED (test gap) | VM5 fails only the open case of the new test | test only | `HiddenInTheOpenFromAShortSightedEnemyVesselIsNotConcealment` (open: no exemption, routed; forest: exempt (a)) | VM5 |
| V6 | minor | REPRODUCED | P1 on `3f71cc8`: formation 1 re-proposed with entity id 30 exactly on top of formation 3's entity 30 → Accepted (control: fresh id 11 → {Overlap, 11}); Close afterwards → {Overlap, 30}. P1b: id 30 at a free spot → Accepted; Close → Accepted; `new TacticalBattle(closed, …)` → ArgumentException "duplicate entity id 30" | new refusal `DuplicateId` (code 11) and a PASS 0 in every check (§13.2) | `AnEditThatReusesAnEntityIdIsRefused`, `ADuplicateIdInTheDeploymentIsADeterministicRefusal`, `AGroundEditMayNotReuseAVesselEntityId` | R2-M3 … R2-M7 |
| V7 | minor (process) | REPRODUCED as far as the present state shows; that the files existed at the moment of the `rm` is INFERRED | `ls /dev/shm/m7c/*.txt`: no file; `E-baseline.sh:11` writes `E-base-times.txt` (and `E-base-build-*.txt`, `E-base-test-*.txt`), `E-econ-tests.sh:11` writes `E-times.txt` (and `E-<tag>-build.txt`, `E-<tag>-tests.txt`); `E-notes.md:5` points to them | no code change possible (no copy exists anywhere this lane can see); limitation 34; round 2 deleted nothing outside the clone except its own named scratch files | – | – |
| V8 | nit | REPRODUCED | (1) `TacticalBattle.HiddenStationaryDomain.cs:19-21` described the guard as always on, while `TacticalRules.MutualStandoffGuard` defaults to false and `MutualHiddenIdlenessRoutsBothSidesUnlessTheGuardIsOn` pins BothEliminated; (2) §0 row (4) and §4 quoted DIR5's ends as "dead / routed / surrendered / escaped"; DIR5 §2.6: "dead, retreated, routed or surrendered" | comment rewritten (option, default OFF); both report quotes corrected and mapped to the engine's states | – (documentation) | – |

Refuted: none. Not done: V7 cannot be undone; P-TR14 (V3) is pinned, not decided.

### 13.2 What changed, as equations

**V1 — march potential.** For a formation f with directive MoveTo or Attack and a non-empty planned path P (cell-centre
waypoints, P[n−1] the order's point), next waypoint index i = PathIdx, goal cell g = cell(P[n−1]):

    Rem(f) = |Anchor(f) − P[i]| + Σ_{k=i}^{n−2} |P[k+1] − P[k]|        (Rem = 0 when i >= n)
    Track_f(g, Rem): first measurement of g -> best_f(g) := Rem (no progress); else if Rem <= best_f(g) − S -> best_f(g) := Rem, PROGRESS

Along the path Rem falls by exactly the distance the anchor advances (each |·| is an exact floor ISqrt), so every pulse in
which the anchor marches at least one cell along its path is progress, whatever the straight-line distance does. Suffix
sums are cached per path array (recomputed when the formation's path array changes; reference identity, never hashed; a pure
function of the path). Termination: limitation 32. Unchanged: the legacy model tracks nothing; minima per goal cell, never
reset (P-TR3); encoding position in the state hash.

**V6 — ids.** `DeploymentRefusal.DuplicateId = 11`, reported with the smallest offending id.
`Check(placements)`: PASS 0 — after the id sort, the first equal adjacent pair (the smallest duplicated id); only then PASS 1
and PASS 2, so with unique ids the id order is total and the verdict does not depend on input order.
`Check(setup)`: sides, then the smallest duplicate over `EntityIds(setup)` — every entity id of every formation (land on
the ground, land aboard, vessels, aircraft; explicit, or generated Id·1000 + slot) — then the placement check.
`CheckEdit(edited, others, otherIds)`: PASS 0 over the edited entities in ascending id — refused if the id occurs twice in
the edit or in others ∪ otherIds; `Propose` passes the ids of every other formation (non-ground included). `Close` runs
`Check(setup)`, so a duplicate is a deployment refusal, never the battle constructor's exception.

### 13.3 Moved goldens (each attributed, MEASURED)

| Golden | Old → new | Cause |
|---|---|---|
| `ReplayAndMetamorphicTests.GoldenStateHash` (CombinedArms 42) | 81492070… → E04D50B0A0D42363… | V1, goal-minimum VALUES only: with only the minimum values left out of the appended block, the r1 and the r2 potential give the same state hash (18F5198DC0EECE45…); the trace hash E53AA685… did not move |
| `DomainOrdersAndGoldenTests.AmphibState` (lane N) | 4F258FDC… → 5F3F0088D42110F5… | the same measurement (both potentials → 0ACE9B0697BA2338…); `AmphibTrace` 075371D6… and `AmphibPersonnel` 6B252A73… did not move |

No other test moved. The battles themselves are unchanged in every shipped golden and bench case (trace hashes, and the
per-seed bench lines of §13.4).

### 13.4 Validation (MEASURED on the final tree)

- **Builds:** `M7Proto.slnx` Debug and Release, `nice -n 19`, `-m:1`: 0 warnings, 0 errors (only this solution's projects
  changed).
- **Tests:** Debug **581 / 0 / 0**, Release **581 / 0 / 0** (`7118ef0` 487 / 0 / 0: +94; `3f71cc8` 569 / 0 / 0: +12 = the 12
  cases of `VerificationRound2TRTests`, 8 methods; no test removed). Clean Release suite 15 s wall.
- **Determinism scan** (`scripts/check-proto-determinism.sh`): **CLEAN** (65 files under `Proto.M7/`).
- **Mutants** (one Release full-suite run each, bound 150 s = 10 × the clean Release suite; each file restored after its run
  and the SHA-256 of all 110 `.cs` files of `Proto.M7` and `Proto.M7.Tests` equal before and after the batch). No hang.

  | Mutant | What it breaks | Verdict | Suite | Killing tests (semantic in bold; golden-only kills not counted) |
  |---|---|---|---:|---|
  | VM1 | V2: only a blocked line conceals (`Clarity < 1` clause dropped) | **KILLED** | 15s | **`VegetationOnTheEnemySightLineIsConcealmentByTerrain(forest: True)`** |
  | VM2 | V3: an Attack march is not tracked (MoveTo only) | **KILLED** | 13s | **`AnAttackMarchOnALastKnownPositionIsProgress`**, `AmphibiousBattleReplays…` (state hash), `ReplayFromRecordedOrders…` (state hash) |
  | VM3 | V4: a budget-refused domain order clears the explicit Hold | **KILLED** | 13s | **`ABudgetRefusedDomainOrderKeepsTheExplicitHold`** |
  | VM5 | V5: any enemy vessel / aircraft in reach exempts (concealment dropped) | **KILLED** | 15s | **`HiddenInTheOpenFromAShortSightedEnemyVesselIsNotConcealment(forest: False)`** |
  | R2-M1 | V1: the r1 potential (straight distance to the goal-cell centre) | **KILLED** | 12s | **`AHiddenFormationMarchingAlongADetourIsNotStationary` (kinds 0 and 1)**, the two state-hash goldens |
  | R2-M2 | V1: suffix sums never recomputed for a new path (stale cache) | **KILLED** | 13s | **`HiddenStationaryRuleTests.OscillatingHiddenFormationIsStationaryInEffect`**, **`VerificationRound1TRTests.ALandedEntityHasNoRunUp`**, **`AutoResolverVerificationFixTests.AutoTerminatesWhenEffectiveRangedAttackFloorsToZero(seed: 2)`**, the two state-hash goldens (failure modes not inspected one by one: some may be index exceptions from the stale array) |
  | R2-M3 | V6: an edit's id not checked against the other entities | **KILLED** | 14s | **`AnEditThatReusesAnEntityIdIsRefused`**, **`AGroundEditMayNotReuseAVesselEntityId`** |
  | R2-M4 | V6: `Check(setup)` without the all-entity id check | **KILLED** | 13s | **`AGroundEditMayNotReuseAVesselEntityId`** (setup whose ground entity reuses the vessel's id) |
  | R2-M5 | V6: `Propose` without the non-ground ids | **KILLED** | 13s | **`AGroundEditMayNotReuseAVesselEntityId`** |
  | R2-M6 | V6: placement-level PASS 0 removed | **KILLED** | 13s | **`ADuplicateIdInTheDeploymentIsADeterministicRefusal`** |
  | R2-M7 | V6: an id used twice inside one edit not refused | **KILLED** | 14s | **`AnEditThatReusesAnEntityIdIsRefused`** |

  Second verify question (ADR-015 §7.2): R2-M1/M2 and VM2 assert DIR5 §2.6 "stationary" — a hidden unit marching on an order,
  whatever the route the terrain forces, is not stationary (VM2 asserts the lane's P-TR14 reading, which the Director may
  overturn; the test then flips by design); VM1 and VM5 assert DIR5's "distinguish genuine inactivity from active
  concealment" in both directions (vegetation on the line conceals; mere short enemy sight does not); VM3 asserts that a
  refused order changes nothing (D-tactical order semantics); R2-M3 … M7 assert that a deployment refusal is a deterministic
  pure function of the deployment (DIR5 §2.14 edit phase) and that the phase never hands the battle a setup it refuses.
- **P-TR14, the alternative reading MEASURED** (VM2 applied temporarily, probe of the `AnAttackMarchOnALastKnownPosition…`
  scenario, reverted and source hashes re-checked): the attacker's idle counter runs 1..4 at the starts of pulses 2–5 while
  its anchor marches 78 m per pulse (243,400 → 477,400 mm), and it is ROUTED at the end of pulse 5 (no exemption), side 0
  eliminated after 6 pulses — versus the implemented reading: not idle for 10 marching pulses, exempt (a) behind the building,
  Standoff after 14.
- **Bench** (small, Release, `nice -n 19`, shared host; load average 1.1–2.0): `Proto.M7.Bench tactical 10` and `domain 5`.
  **Same battles:** all 70 per-seed result lines (50 tactical, 20 domain: end, side, pulses, events, dropped, per-formation
  survivors / killed / surrendered, people) are identical to the round-1 log (MEASURED, `comm` of the normalized lines).

  | Case (mean pulses / events) | round 1 (ms) | round 2 (ms) |
  |---|---:|---:|
  | 50v50 HI mirrored control (3.7 / 911) | 912 | 916 |
  | 50v50 HI 20 m asymmetric (5.7 / 1,029) | 768 | 761 |
  | 60v60 archers (3.0 / 857) | 889 | 676 |
  | 50v60 HI vs archers (3.0 / 731) | 597 | 592 |
  | 110v110 combined arms (4.0 / 2,210) | 2,745 | 2,614 |
  | **sum of the four pre-round-1 cases** | **5,143** | **4,798** |

  Domain, four runs of `domain 5` on the round-2 tree: galleys 372 / 480 / 463 / 446, sail 435 / 451 / 507 / 478, turret 55 /
  53 / 52 / 50, amphibious 877 / 860 / 882 / 810 ms (round 1: 470 / 468 / 55 / 667). The amphibious difference was checked by
  an interleaved A/B in one session, the r1 potential vs the r2 potential (both builds otherwise identical, three runs each):
  amphibious 710 / 852 / 816 vs 770 / 866 / 856 ms (means 793 vs 831, +5 %, inside the ±10 % spread of either variant), galleys
  505 / 504 / 515 vs 476 / 474 / 481 ms (the r2 build faster). So the round-1 → round-2 amphibious difference is mostly session
  noise, and the path-length potential has no cost this small bench can resolve (INFERRED from the A/B; not profiled). Logs:
  `scratchpad/m7c/reports/TR-r2-bench.txt` (session scratchpad).
