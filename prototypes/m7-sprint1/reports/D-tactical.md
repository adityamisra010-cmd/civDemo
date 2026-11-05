# Slice D — tactical vertical slice (M7 Sprint 1)

Spec: `m7s1/implementation-spec.md` §4 (with §1 and §8), in the session scratchpad. Repo `/home/user/m5w/dev`, branch
`claude/new-session-yavghm`. All code is under `prototypes/m7-sprint1/`. No git state was changed. Nothing outside the
folder was touched (the governance docs shown as modified or untracked by `git status` belong to other lanes or the
Director, and are unchanged by this slice). `Sim.slnx` was never built. Slice B and Slice C code is reused **unchanged**.
Two one-line additions were made to shared files: a `tactical` dispatch line in `Proto.M7.Bench/Program.cs` and one line
in `README.md`.

This is a prototype. It does **not** decide O-14 (where the battle framework lives), O-8 (Action Capacity against Law 3),
or any value the Director has not given. Every numeric rule except the five-pulse inactivity count is a labelled
PLACEHOLDER (§3).

## 1. What exists

| file (`Proto.M7/Tactical/`) | contents |
|---|---|
| `TacticalTypes.cs` | `Domain` {Land, Naval, Air}, `ResolverKind` {Tactical, Auto}, `EntityState`, `OrderKind` {Hold, MoveTo, Attack, Retreat, Rally (=Heal), Reengage}, `ExitEdge`, `Posture`, `BattleEndReason`, event kinds, counter-RNG stream ids. |
| `UnitProfiles.cs` | `ProposedCaps` (the Director-revision table, cited, configurable), `CapFamily`, the `UnitProfile` record, and two profiles (heavy infantry 301 and archers 402) with line-level citations. |
| `ActionBudget.cs` | A per-formation, per-pulse strict budget with N as an input. Four rules, refusal reasons, refresh at pulse start. |
| `BattleSetup.cs` | `TacticalRules` (every rule value, each labelled), `FormationSpec`, `EntitySpec`, `SideSpec` (exit edge and objectives), `BattleSetup`. |
| `Control.cs` | `OrderRequest`, `OrderLogEntry` (records every order, including refused ones), `SideView` (knowledge only), `IFormationController`, `ScriptedController` (also used for replay from a log), `PassiveController`, the `KnowledgeAi` Enemy AI, and `AiDecision` records. |
| `EdgeField.cs` | Per-side exit-distance field: multi-source integer Dijkstra using exactly the Slice C A\* step costs and its no-corner-cutting rule. Gives the descent step for withdrawal and rout, and reachability for surrender. |
| `TacticalBattle.cs` | The battle: an `IEventWorld` on the Slice B scheduler and the Slice C spatial layer. It covers formations → entities, movement, detection and knowledge, combat, morale, rout, retreat, surrender, the inactivity rule, termination, aggregation, and the trace and state hashes. |
| `BattleResult.cs` | `FormationResult` (exact aggregation), `BattleResult` (the same shape the AutoResolver will produce), `IBattleResolver`, `TacticalResolver`, and `BattleResolution.Resolve(..., resolver = Tactical, auto = null)`. |
| `TacticalScenarios.cs` | Shared synthetic scenarios on a 48×32 open field: `Meeting(...)` and `CombinedArms(...)`. |

Tests are in `Proto.M7.Tests/Tactical/` (5 files plus support), **52 tests**. The bench is `Proto.M7.Bench/TacticalBench.cs`
(`dotnet Proto.M7.Bench.dll tactical [seeds]`; `tactical golden` prints the replay golden).

## 2. Mechanics and the design decisions taken

**Time (hybrid, O-5).** A battle runs as tactical pulses of `PulseMs` on the global ms timeline. Inside a pulse there is a
fixed `Step` event every `StepMs`. At one instant the event classes run in this order:

1. System: `PulseEnd`, then `PulseStart`, then `Order`.
2. Arrival: `Step` — anchors move, desired entity moves, the Slice C separation step runs, and the minima are updated.
3. Detection: detection, then target choice. Each chosen target becomes a `Fire` event.
4. Fire: hit rolls. Each hit becomes a `Damage` event.
5. Damage: HP loss.
6. Resolve: deaths, morale, rout, heal, escape, surrender, then the elimination check.

All shots at an instant are taken against the world before any damage. All damage lands before any death is resolved, so a
mutual kill kills both (tested). Every state change bumps the entity's generation, so a stale event is dropped by the
Slice B invalidation.

**Formations and entities (O-10 a).** A formation owns orders and expands into `Count` entities. Each entity has an Id,
FormationId, Pos (mm), HP, State and Generation. Entities sit on slot offsets in a block (`FormationColumns` wide,
`SlotSpacingMm` apart) around the anchor, facing away from the side's exit edge. Placement can also be explicit
(`EntitySpec`, in any order). The engine sorts formations and entities by id, and every loop runs in ascending id.

**Orders.** Orders are submitted at pulse start by a controller. Each carries a `StepOffset`, so an order can be delivered
at any internal step. Each order is validated first (unknown formation, not own, not on the field, bad target, no path,
nothing to re-engage). It then goes to the budget. The outcome is written into the order log either way.

- **MoveTo:** the anchor follows the Slice C A\* polyline at the formation pace, which is scaled by the terrain cost of the
  anchor's cell.
- **Attack:** the anchor moves toward the side's KNOWN centroid of the target formation. It re-plans when the target cell
  changes and stops within `EngageRadiusCells`.
- **Retreat:** orderly withdrawal by descending the exit field. On reaching the edge an entity becomes `Escaped`
  (it is not counted as routed).
- **Rally (Heal):** the formation stands, recovers morale faster, and regains HP from a bounded pool.
- **Reengage:** valid only while the directive is Retreat. It resumes the Attack.

**Targeting.** Each entity targets the nearest DETECTED enemy entity within reach. The composite key is (distance², entity
id), so on equal distance the lower id is chosen. A tie-dense test permutes the input order and checks this.

**Action budget (spec §4; brief "approximately two or three").** N is an input; the tests run N = 2 and N = 3. The rules,
checked in this order:

1. Each kind at most once per interval.
2. At most N actions in total.
3. After a Retreat in the interval, no Attack and no Reengage.
4. No Heal (Rally) in an interval that contains an attack, in either order.

Refused orders are recorded with their reason. Under the delegated authority, Reengage also counts as an "attack" for
rule 4.

- The mapping onto ratified Action Capacity is **OPEN** (§10). Points of contact:
  - ledger §17.2: AC is agent-specific;
  - ledger §19: AC is refreshed per War Pulse;
  - the `actionCapacityShare` in (0, 1] of the M6 training contract needs a rule for rounding a share to whole slots;
  - the proposed charter's "1–3 whole actions per refresh" (`unit-catalogue…md:228`) contains N ∈ {2, 3};
  - recon-critic X7: the ratified ledger counts movement as AC spend, while the proposed charter separates the two. This
    prototype counts MoveTo as an action, which follows the brief.

**Detection and knowledge (O-6 delegated).** For each (side, untracked enemy entity, instant) there is **one** counter-RNG
draw. Its probability is p = the best LOS clarity among the side's observers within sight, multiplied by the
stationary-concealment factor when the target is stationary, has not fired, and is concealed (clarity < 1).

- Contact reveals automatically.
- A tracked enemy stays tracked while any observer has LOS within sight, with no re-roll. Otherwise it is lost and
  remembered at its last-seen point.
- An enemy seen dying or leaving is removed from knowledge.

**Deviations from the spec's literal wording, both deliberate:**

- (a) One draw per side, not one per observer. The spec's "each entity detects" would let a crowd of 60 observers acquire
  a stationary hidden unit almost surely within a few steps, which makes concealment meaningless. A side sees no better
  than its best-placed observer.
- (b) The stationary factor applies only when the sight line is concealed. A stationary unit standing in the open is not
  hidden.

**Enemy AI (knowledge only).** `KnowledgeAi` reads a `SideView`, which holds own formations plus known enemies and has no
reference to the battle. Its rules, in order:

1. Low morale → Retreat.
2. Nearest known enemy by (distance², formationId, entityId) → Attack. An Ambush posture holds until a known enemy is
   within `AmbushTriggerCells`.
3. Otherwise → MoveTo the nearest objective, by (distance², index).

Every decision is logged as an `AiDecision`. A metamorphic test shows that moving an undetected enemy elsewhere leaves the
AI's decisions identical.

**Combat (O-15 b).**

- Melee needs contact: centre distance ≤ 2r + reach.
- Ranged fire uses the Slice C `Projectiles.DirectFire` against the enemy discs. Friendly discs are transparent, which is a
  PLACEHOLDER simplification. A shot blocked by terrain is not a valid shot.
- Ranged units do not fire after moving that step. This follows the charter's "cannot fire after a full move",
  `unit-catalogue…md:720`, as a simplification.
- Hit probability = attack/(attack + defence), clamped to [1/20, 19/20], drawn with the counter RNG keyed
  (shooter, instant, target).
- Damage = max(1, attack/5).

**Morale, rout, retreat, surrender.**

- Formation morale is an integer from 0 to 1000. Each death costs `2000/Start`.
- If a Hold or Rally pulse has no casualties, morale gains +20 (Hold) or +60 (Rally).
- Below 300 the formation routs: every Active entity becomes Routing and flees down its exit field. Routing entities do not
  rally (not modelled).
- A Routing entity becomes Escaped at the edge. It becomes Surrendered if its cell has no path to the edge, or if it
  wanted to move, could not, and an Active enemy is in contact.

**Movement priority (O-6 delegated; a finding).** Slice C's separation resolves movers in ascending mover id. Passing entity
ids directly let the lower-id side move first every step. Measured over 20 mirrored 50v50 battles, this gave 14 vs 2
losses, and 2 vs 16 with the ids swapped. The mover key is now a counter-RNG draw keyed (seed, instant, entity id) in the
high bits, with the entity index in the low 16 bits, so it is unique and maps back exactly. This removed the id-order
effect (see §9 for the residual bias).

**Domain and resolver (O-1).**

- `Domain` lives on `FormationSpec` and flows into `FormationResult`. The order, order-log, budget and entity-view types
  carry no domain field (tested by reflection).
- `BattleResolution.Resolve` takes the resolver as an explicit parameter whose default is `Tactical`.
- `Auto` is used only when it is requested and also supplied; otherwise the call throws.
- A Naval formation with the default resolver goes to the tactical engine, which throws `NotSupportedException` ("naval
  module not built"). It is never auto-resolved silently (tested with a stub AutoResolver that records zero calls). The
  naval module is not built.

## 3. Placeholders and citations

| item | value | source |
|---|---|---|
| Caps | HI 50, archers/light 60, tanks 5, helicopters 3, ships 5, fixed-wing 1, snipers 2, mortars 5 | **CITED** `docs/m7-spec.md:391-404` (rows :395-402), "proposed limits". A test checks the repo lines when the repo is present. |
| HI 301: Integrity 50, Melee 50 | | **CITED** `unit-catalogue-and-progression.md:202` (anchor) and `:664` |
| HI Ranged 0 | | **CITED** `:664` "Ranged — until A7" |
| HI Defence 45 | | PLACEHOLDER inside band M (`:211`, `:664` "Defence M") |
| Archers 402: Integrity 35, Melee 25, Ranged 75, Defence 25 | | PLACEHOLDER points in the cited bands (`:721` L–M, L, H–VH, L; bands `:209-213`) |
| Archers range | 2 cells | **CITED** `:223` "short 1–2" and `:721` "Range short (A1–A6)" |
| Speed multiplier | ×1 | PLACEHOLDER midpoint of `:663` / `:720` "Foot (×0.9–1.1)" (band list `:217`) |
| Sight | 12 cells | PLACEHOLDER (invented; `:664` "Sight L" gives no cell mapping) |
| Entity HP | = Integrity | PLACEHOLDER mapping (invented) |
| Battle grid 48×32 | | `docs/d011-battle-layer-addendum.md:35` "~48×32" (the D-012 recommendation; the grid itself is OPEN as O-7) |
| Cell size | 20,000 mm | PLACEHOLDER. No physical size exists for "battle-grid cells of D-012's coarse grid" (`:222`). |
| Pulse / step | 60,000 / 2,000 ms | PLACEHOLDER (`m7-spec.md:385` gives no value) |
| Reference foot speed | 1,300 mm/s | PLACEHOLDER |
| Catch-up | 3/2 | PLACEHOLDER |
| Radius / spacing / columns / reach | 300 / 1,500 / 10 / 600 mm | PLACEHOLDER |
| Engage radius | 2 cells | PLACEHOLDER |
| Eye / muzzle / aim heights | | PLACEHOLDER |
| Hit formula and clamp; damage divisor 5 | | PLACEHOLDER |
| Morale scale, loss, rout threshold, recovery | | PLACEHOLDER |
| Heal pool ½·MaxHp, 1 HP/step | | PLACEHOLDER |
| Stationary-concealment factor | 1/64 | PLACEHOLDER (spec names it, gives no value). Tests use 2⁻²⁰ or 0 as labelled TEST INPUTs. |
| AI retreat morale 450, ambush trigger 3 cells | | PLACEHOLDER |
| Action budget default | 2 | PLACEHOLDER (2 vs 3 is OPEN; both tested) |
| Inactivity pulses | 5 | **DIRECTOR** (brief §2) |
| Terrain cost and concealment tables | | Slice C placeholder tables (Road 700 cited, others placeholder) |

## 4. The inactivity rule and the termination argument (O-4: no pulse cap)

**PRIMARY activity** of an entity in a pulse is any of these bounded-progress events:

- it dealt or received damage;
- it attacked a valid target (a melee blow, or a ranged shot not blocked by terrain);
- it is credited with its side's FIRST detection of an enemy entity;
- a new **running** minimum (by ≥ 1 cell below the last recorded one) of its distance to:
  - a tracked enemy,
  - its side's objectives, or
  - its exit edge, while retreating or routing;
- it changed state (rout, surrender, escape, death).

The minima are never reset, not by new orders either. The first measurement only initialises a minimum.

**ACTIVE CONCEALMENT.** A concealed entity counts as active iff, at pulse end, some enemy with primary activity in that
pulse is within that enemy's sight range of it — the range at which it would be detected if it were not concealed.
Concealed means: in a cell whose concealment multiplier is below 1, and not tracked by the enemy.

**Formations and sides.** A formation is inactive in a pulse when none of its on-field entities is active and none of its
entities changed state. The counter resets on activity. A side is disengaged when every formation it still has on the
field has counter ≥ 5. Its on-field entities then become `Escaped` with `Disengaged = true`. They are preserved and
counted as survivors, never deleted.

**The battle ends** when either of these holds:

- (a) at a Resolve, a side has no Active entity. Remaining Routing entities are then marked `Escaped (RoutedEscape)`.
- (b)/(c) at a pulse end, one or both sides are disengaged.

There is no pulse or event cap anywhere in `Proto.M7`. The tests run under a harness guard of 2,000 pulses; exceeding it
is reported as a FAILURE.

**Termination argument.** Every primary activity consumes a bounded integer potential:

- total HP plus the bounded heal pools: every damage event removes at least 1 HP, except 0-HP overkill at the same
  instant;
- the per-entity running minima, which are non-negative and fall by at least one cell each time;
- first detections, at most n per side;
- monotone state changes, at most 2 per entity.

The one exception is "attacked a valid target" without a hit. Every attack has hit probability ≥ HitMin (1/20 by
default; `TacticalRules.Validate` now refuses HitMin = 0, verify-D F2, §11) and every hit consumes HP, so unbounded attacking terminates with probability 1. This is an almost-sure bound, not a deterministic one,
and is reported as such. Once the potential stops changing, every formation is inactive, and within 5 pulses a side
disengages. Mutual stationary hiding has no primary activity, so it ends after exactly 5 pulses.

## 5. Tests and results

`dotnet build prototypes/m7-sprint1/M7Proto.slnx -c Release -nologo --disable-build-servers` gives **0 warnings, 0 errors**.

`dotnet test … --no-build` gives **Passed 255, Failed 0, Skipped 0**: B+C 203 + Slice D 52 (about 6 s).

`scripts/check-proto-determinism.sh` is **CLEAN** (37 files, comments included). The first run flagged a method named
`Order(` under the LINQ-ordering pattern, so I renamed it `ApplyOrder`.

| spec item | test(s) |
|---|---|
| Action budget N=2, N=3, all rules, refusals recorded, refresh | `ActionBudgetTests` (7 tests: 6 theories over N ∈ {2, 3} plus 1 fact). The in-battle test shows KindRepeated, TotalExhausted (N=2), AttackAfterRetreat (N=3), and refresh in the next pulse. |
| Caps (cited, refuse > cap, configurable) and profiles | `ProfilesCapsDomainTests`. HI 50 ok / 51 refused; archers 60 ok / 61 refused; override. Checks against the repo lines of `m7-spec.md` and the charter. |
| Domain tag + explicit resolver defaulting to Tactical | `ResolverDefaultsToTacticalAndAutoIsNeverImplicit`, `NavalDomainIsNeverSilentlyAutoResolved`, `OrderAndBudgetInterfacesAreDomainAgnostic` |
| Simultaneity + invalidation | `MutualKillAtOneInstantKillsBoth`: both die at t=0, both Damage events come before the Resolve, the old generation is no longer live. |
| Tie-dense targeting | `NearestTargetTieBreaksOnLowestId`: 4 equidistant enemies, 4 permutations, the lowest **entity** id wins (formation ids deliberately out of order). |
| Detection / concealment | `StationaryConcealedEntityIsNotDetectedButOpenOneIs` |
| Knowledge-only AI | `AiDecisionsDependOnlyOnKnowledge` (metamorphic), `AiTargetChoiceTieBreaksOnFormationThenEntityId` |
| Instant-exact order delivery pin | `OrderTakesEffectAtItsStepInstant`: an order for step 3 of pulse 1 applies at exactly 60,000 + 3·2,000 ms. Before that instant the directive is unchanged; the Step at the same instant already moves the anchor. |
| Retreat / rout / surrender | `RetreatOrderWithdrawsToTheEdgeAndEscapes`, `CasualtiesBreakMoraleAndTheFormationRouts`, `RoutedEntitiesWithNoPathToTheEdgeSurrender` (a water-ringed island) |
| Rally/Heal bounded | `RallyHealsFromABoundedPool` (pool ½ gives 50, pool 1/10 gives 40) |
| Inactivity (i) | `HiddenAmbusherWithSearchingEnemyStaysAndSprings`: the counter never reaches 5, the battle lasts past 5 pulses, the ambusher deals damage. Its control `WithoutActiveConcealmentTheAmbusherWouldBeDisengaged` turns the clause off and the ambusher is disengaged at exactly pulse 5, unsprung. |
| Inactivity (ii) | `HiddenUnitWithNoEnemyNearbyDisengagesAfterExactlyFivePulses`: Pulses = 5, EndMs = 300,000, 10/10 DisengagedEscaped = survivors, 0 killed. |
| Inactivity (iii) | `MutualHidingStandoffEndsAfterFivePulses`: BothDisengaged at 5 pulses, nobody detected. |
| Inactivity (iv) | `OscillatingMoverCannotKeepTheBattleAlive` (out of sight: ends at 5 pulses although the anchor moved every pulse). `OscillatingInSightEndsAfterFiveIdlePulses` (moves 3+ cells toward and away from the enemy and still ends within 5–8 pulses). |
| Aggregation (exact) | `AggregationConservesExactlyAcrossSeeds`: 4 scenarios × 8 seeds, each formation Start = Survivors + Killed + Surrendered and Survivors = Active + Escaped, exact `long`. `End()` also throws on any violation. |
| Termination without a cap | Every battle test runs under the harness guard; all 32 conservation battles terminate. |
| Replay + pinned hash | `ReplayFromRecordedOrdersReproducesTraceAndResult`: combined arms, seed 42, KnowledgeAi on both sides. It re-runs from the recorded order log through `ScriptedController.FromLog` and checks that the trace hash, state hash, result hash and order log are identical. **GOLDEN (Linux x64, .NET 10, measured 2026-11-04):** trace `E53AA685…F2AC44E3`, state `88F6322C…1FFE67E1`. Cross-platform equality is UNTESTED (O-17). |
| Metamorphic permutation | `PermutingEntityInputOrderGivesTheIdenticalBattle` (3 permutations of the formation order and of each formation's entity order give an identical trace, state and result; explicit placements equal to the slots reproduce the generated battle). Plus `SameSeedSameBattleDifferentSeedDifferentTrace`. |

## 6. Mutation testing (spec §8)

The mutants were run in a private copy at `m7s1/work/D-mut/` (tools: `tools/mutants.py`, `tools/run.sh`; log:
`mutants-final.txt`), on the final tree. The clean tactical run took 8 s wall. Each mutant's run was bounded at 35 s (about
4.4× clean). No mutant hung.

| mutant | change | killed by (semantic tests) |
|---|---|---|
| M1 | drop the no-Attack/Reengage-after-Retreat rule | `NoAttackOrReengageAfterRetreat` ×2, `RefreshRestoresTheBudget` ×2, `InBattleRefusalsAreRecorded(3)` |
| M2 | the active-concealment clause never finds a searcher | `HiddenAmbusherWithSearchingEnemyStaysAndSprings` |
| M3 | minima follow the current distance instead of a running minimum | the ambusher test, the (ii) five-pulse test, the clause-off control (+ golden) |
| M3b | minima reset whenever an order is accepted | `OscillatingInSightEndsAfterFiveIdlePulses` (+ golden) |
| M4 | deaths applied inside Damage (breaks simultaneity) | `MutualKillAtOneInstantKillsBoth` (+ golden) |
| M5 | nearest-target tie picks the highest id | `NearestTargetTieBreaksOnLowestId` ×4 (+ golden) |
| M6 | heal ignores the bounded pool | `RallyHealsFromABoundedPool(1,10,40)` |
| M7 | no surrender when the exit is unreachable | `RoutedEntitiesWithNoPathToTheEdgeSurrender` |
| M8 | inactivity threshold off by one (6 pulses) | (ii), (iii), (iv), the clause-off control |

All 9 are killed, and each by at least one semantic test, not by the golden alone. The first version of the in-sight
oscillation test did **not** kill M3b; its amplitude was under one cell. I strengthened it to move toward and away from the
enemy by several cells, and it now kills M3b.

## 7. Performance (pre-fix record; post-fix numbers in §11) (Proto.M7.Bench `tactical 10`, Release, 4 CPUs shared, Linux x64; Stopwatch for reporting only)

| battle (KnowledgeAi on both sides, 10 seeds) | mean pulses | mean events | median ms/pulse | max ms/pulse |
|---|---:|---:|---:|---:|
| **50v50** heavy infantry | 5.7 | 1,029 | 19.0 | 28.7 |
| **60v60** archers | 3.0 | 884 | 23.9 | 27.8 |
| 50v60 heavy infantry vs archers | 3.0 | 731 | 18.7 | 18.9 |
| 110v110 combined arms (HI 50 + archers 60 per side) | 4.0 | 2,210 | 68.0 | 71.4 |

There are 30 steps per pulse, so 50v50 costs about 0.6 ms per step. The costs are O(n²) per step: detection pairs (each a
supercover LOS walk), nearest-target scans, and the Slice C separation (O(n² log K)). No broad phase exists. Event
throughput is not the bottleneck: about 0.01 M events/s here, against the scheduler's millions. Timings are noisy on this
shared host, varying by 1.5–2× between runs.

## 8. Behaviour observed with the placeholders (not calibration; these numbers are not tuned)

- Battles are short: 3, 4 or 9 pulses over the 40 bench battles (3–9 minutes at the placeholder pulse length). They end by
  morale collapse rather than annihilation: the killed fraction per formation ranges from 0 to 58 %.
- Archers against heavy infantry win 10/10 with no losses. The heavy infantry routs during the approach. This follows from
  the placeholder hit and damage numbers and the absence of shields or armour against missiles. It is not a balance
  claim.
- Routing heavy infantry blocked by its own archers behind it surrenders (combined arms: 1–5 surrendered per losing heavy
  infantry formation, 24 in total over the 40 bench battles).

## 9. Findings

> Update after verification (§11, F1): most of the bias below was a geometry error in `TacticalScenarios.Meeting`
> (side 1 started 20,000 mm closer to its exit). The anchors are now mirror images. The table below is the pre-fix record.

1. **Residual East/West bias in mirrored battles (OPEN, cause not isolated).** After the movement-priority fix, swapping
   formation ids no longer flips the winner. A geometric bias remains: the side starting in the east loses more often.

   | 40 mirrored 50v50 seeds | east side lost |
   |---|---:|
   | as built (east = side 1) | 31 |
   | formation ids swapped | 24 |
   | full point reflection (east = side 0) | 24 |

   Total kills are nearly equal (615 vs 635), so battles are near coin flips that a small systematic edge decides.

   I tested and ruled out, each in a scratch copy:
   - floor rounding in Slice C `Separation.At`;
   - anchor A\* path ties;
   - centroid floor rounding;
   - rounding in `Toward`. Sign-symmetric rounding is now used there; on its own it did not change the distribution.

   Remaining candidates:
   - direction-dependent half-open and supercover conventions;
   - id-ordered ties that are not point-symmetric.

   **Consequence for Slice E (parity):** the battery should use side-swapped pairs, or report per-side rates, until this
   is isolated.

2. **Separation by entity id is a side bias.** This is fixed here in the mover key. Any future use of Slice C `Separation`
   with id-ordered priority has the same problem.

3. The spec's per-observer detection literal would defeat concealment (§2 deviation a).

## 10. Dependencies and blockers for integration (not decided here)

- **O-14:** where the battle framework lives. The prototype namespace is `Proto.M7.Tactical`, not `Sim.Battle`.
- **O-8 / Action Capacity:** the budget's mapping onto ledger §17.2/§19 AC and onto `actionCapacityShare`, and whether
  movement spends AC (recon-critic X7, G11).
- **Casualties into the campaign:**
  - `FormationResult.Killed` and `Surrendered` are the deltas a future `Ledger.Transfer` would move (ADR-055 tables);
  - O-35 immortality;
  - what Surrendered means (prisoners?);
  - what Disengaged/Escaped means in the Ledger and the campaign (recon-critic G3).
- **RNG:** the battle seed must derive from a `RngRegistry` stream with its state in `WorldState`. This needs kernel
  sign-off (B report).
- **Values and models not decided:**
  - every PLACEHOLDER in §3, in particular:
    - the battle cell size and grid (O-7 / D-012),
    - pulse and step lengths,
    - the detection model,
    - the unit numbers (charter bands only, PROPOSED);
  - which terrain classes exist in the campaign (Slice C: forest/jungle do not exist there).
- **Not built:**
  - the naval and air modules (O-1: full tactical control decided, module out of scope);
  - rallying routed units, flanking, ZOC, formations' facing changes, friendly fire, ballistic arcs for archers;
  - the Enemy AI beyond the simple knowledge-only rules (m7-spec.md:389: no personality layer).
- **Performance budget O-12** is open. The numbers in §7 are measurements, not a budget.

## 11. Verification and fixes (verify-D round, 2026-11-04)

Every finding was first reproduced by me in a private copy of the unfixed tree (`m7s1/work/fixD/repro/`, with the
verifier's `VerifierProbes.cs` added) before anything was changed (ADR-015 §6). Fixes were then applied to
`prototypes/m7-sprint1/` only. Mutants were re-run in a fresh private copy of the fixed tree (`m7s1/work/fixD/mut/`) with
my own runner (`m7s1/work/fixD/tools/mut.py`, the verifier's mutant table, parameterised root). Logs:
`m7s1/work/fixD/logs/`.

### 11.1 Findings

| id | verdict | reproduction (unfixed tree) | change | test: before → after |
|---|---|---|---|---|
| F1 (major) | **FIXED** | `P3`: "side0 anchor-to-exit 290000 mm, side1 anchor-to-exit 270000 mm, field centre 480000, anchor midpoint 490000". `P3c` (80 seeds, 50v50 HI, KnowledgeAi both): (14,34) east lost 60/80, west 20/80; (14,33) east 29/80, west 46/80; (13,34) east 34/80, west 39/80. | `TacticalScenarios.Meeting`: side 1 anchor = `Cell(MirrorColumn(14), 16)` = Cell(33,16); new `MirrorColumn(cx) = FieldW-1-cx`. Doc comment updated (19 cells apart, still beyond sight 12). | `MeetingAnchorsAreMirrorImagesAboutTheFieldCentre` ×3: before **FAILED** ("Expected: 290000, Actual: 270000"), after passed. Also asserts the engine's own `EdgeField` path distances to the two exits are equal. |
| F2 (minor) | **FIXED** | `P1_HitMaxZero_IsAcceptedAndDoesNotTerminate`: "ended=False pulses=201". | `TacticalRules.Validate` throws when `HitMin == 0` (every attack must be able to consume HP; that is what the §4 termination argument needs). Doc of `HitMin` updated. Defaults unchanged. | `HitMinZeroIsRejectedByValidateAndByTheBattle`: before **FAILED** ("Assert.Throws() Failure: No exception was thrown"), after passed. The verifier's P1 probe now fails at `rules.Validate()` on the fixed tree. |
| F3a (minor) | **FIXED** (test gap) | mutant Md: only the golden failed (51/52). | test only | `RoutingEntityInContactWithAnOpenRetreatDoesNotSurrender` (kills Md) + positive control `RoutingEntityBlockedByAnEnemyInContactSurrenders`. |
| F3b (minor) | **FIXED** (test gap) | mutant Mf: only the golden failed. | test only | `ASideLeftWithOnlyRoutingEntitiesIsEliminatedAtThatResolve` (EndMs = 0, 10/10 routed-escaped), also `RoutThresholdIsStrict(-1)`. |
| F3c (minor) | **FIXED** (test gap) | mutant Mg: only the golden failed. | test only | `OverkillIsClampedSoDamageBooksEqualHpLost` (two 10-damage hits on a 1-HP target book DamageTaken = DamageDealt = 1, HP 0). |
| F4 (minor) | **FIXED** (test gap) | mutant Mi survived (52/52). | test only | `ReacquiringALostEnemyIsNotNewActivity`: a 1-entity shuttle crosses the 12-cell sight boundary every pulse for 40 pulses; each side re-acquires the other ≥ 2 times, yet the battle ends BothDisengaged within 5–8 pulses. |
| F5 (minor) | **FIXED** (test gap) | mutant Mj survived, golden unchanged. | test only | `EdgeFieldDescentTieBreaksOnTheLowerCellIndex` (hand case: wall at (9,16), N/S exact tie from (10,16), N wins) and the tie-dense oracle `EdgeFieldDescentMatchesTheCompositeKeyOracleOnATieDenseField` ×4 edges (wall lattice; every reachable cell checked against a brute-force (key, index) argmin; ≥ 20 tie cells required). |
| F6 (nit) | **FIXED** (boundary pin) | mutant Mh survived. | test only | `RoutThresholdIsStrict(0, false)` / `(-1, true)`. The threshold value stays PLACEHOLDER; only the strict `<` is pinned. |
| F7 (nit) | **FIXED** (test aim) | mutant Ma: only `MutualHidingStandoffEndsAfterFivePulses` failed; `StationaryConcealedEntityIsNotDetectedButOpenOneIs` passed. | test only | `StationaryConcealedEntityIsNotDetectedButOpenOneIs` strengthened: target whose only cover is its own jungle cell; with the factor removed (`StationaryConcealment = 1`, control) it IS acquired within 3 pulses; with factor 0 it is NOT. Now fails under Ma. |

No finding was refuted. Before/after evidence for the new tests: on the unfixed source the 17 new/strengthened tests give
4 failures (F1 ×3, F2), the rest pass, as they should (they pin correct behaviour that only mutants break). On the fixed
tree all pass.

### 11.2 Mutant table (fixed tree; clean tactical run 7.9 s wall; bound 40 s ≈ 5× clean; no mutant hung)

| mutant | change | result | killing tests (semantic first) |
|---|---|---|---|
| Md | surrender on contact alone (drop `WantedMove && !Moved`) | killed | `RoutingEntityInContactWithAnOpenRetreatDoesNotSurrender` (+ golden) |
| Mf | elimination counts Active + Routing | killed | `ASideLeftWithOnlyRoutingEntitiesIsEliminatedAtThatResolve`, `RoutThresholdIsStrict(-1, true)` (+ golden) |
| Mg | damage not clamped to HP | killed | `OverkillIsClampedSoDamageBooksEqualHpLost` (+ golden) |
| Mh | rout at `Morale <= RoutThreshold` | killed | `RoutThresholdIsStrict(0, false)` |
| Mi | credit Primary on every re-acquisition | killed | `ReacquiringALostEnemyIsNotNewActivity` |
| Mj | EdgeField tie-break on the higher cell index | killed | `EdgeFieldDescentTieBreaksOnTheLowerCellIndex`, oracle ×4 |
| Ma | stationary-concealment factor removed | killed | `StationaryConcealedEntityIsNotDetectedButOpenOneIs`, `MutualHidingStandoffEndsAfterFivePulses` |
| Mc | Heal-then-Attack allowed | killed | `HealNeverInAnIntervalWithAttack` ×2 |
| Me | searcher needs no primary activity | killed | `MutualHidingStandoffEndsAfterFivePulses` |

Every mutant is killed by at least one semantic test; none relies on the golden alone. Teeth and aim: each new test
asserts a rule stated in §2/§4 (surrender clause, termination (a), exact damage books, strict rout threshold,
first-detection-only bound, composite-key descent, stationary factor), not an incidental number.

### 11.3 Build, tests, scan, perf (fixed tree)

- `dotnet build prototypes/m7-sprint1/M7Proto.slnx -c Release -nologo --disable-build-servers`: **0 warnings, 0 errors**.
- `dotnet test …/M7Proto.slnx -c Release --no-build`: **Passed 271, Failed 0** (B+C 203 + Slice D 68; D was 52, +16).
- `scripts/check-proto-determinism.sh`: **CLEAN** (37 files).
- Goldens: `GoldenTraceHash` / `GoldenStateHash` unchanged (they use `CombinedArms`, which this round did not touch).
- Bench `tactical 10` (post-fix; same host, noisy 1.5–2×): 50v50 HI mean 3.7 pulses, 911 events, median 23.1 ms/pulse
  (max 74.5, first-battle JIT); 60v60 archers 3.0 / 857 / 24.4; 50v60 HI vs archers 3.0 / 731 / 19.7; 110v110 combined arms
  4.0 / 2,210 / 68.3.
- East/West after F1 (50v50 HI, KnowledgeAi both): 40 seeds: east lost 16/40 (was 31/40); 80 seeds: east lost 29/80,
  west lost 46/80. The residual now leans the other way at about 2σ; §9's "cause not isolated" still applies to it.

### 11.4 Open items found while fixing (not changed — outside the findings' scope)

- **`TacticalScenarios.CombinedArms` has the same anchor asymmetry** as F1: side 0 HI/archers at Cell(14)/Cell(12)
  (290,000 / 250,000 mm from the West line), side 1 at Cell(34)/Cell(36) (270,000 / 230,000 mm from the East line). In the
  post-fix bench the east side lost 7 of 10 combined-arms battles. Fixing it (Cell(33)/Cell(35)) moves the replay golden,
  so it needs a re-pin with attribution; recommended before Slice E uses `CombinedArms`.
- The `ScaleTowardZero` comment in `TacticalBattle.cs` still cites §9's "residual East/West bias … cause not isolated";
  read it together with this section.
- The verifier's probes `P1_…` and `P3_…` assert the defects and now fail on the fixed tree, as expected; they live only
  in the verifier's copy.

## Dated note — 2026-11-05 (D-079; appended, the text above is unchanged)

Record: `docs/d079-m7-governance-reconciliation-2026-11-05.md` (Director decisions of 2026-11-05, cited DIR5). Each bullet quotes the superseded line, states the extent and what is retained, and cites the replacing DIR5 section and D-079 decision row (DR). Categories: DIRECTOR DECISION, ADMINISTRATIVE CONSEQUENCE, DELEGATED TECHNICAL DECISION, OPEN (needs the Director), READING (labelled interpretation).

Grouped in D-079 §2 as **S484** (17 statements). This is a report, not a ruling: the notes below correct its status against DIR5 and the Phase B audit.

- (`D-tactical.md:68`) — "**Action budget (spec §4; brief "approximately two or three").** N is an input; the tests run N = 2 and N = 3." — in part: DIRECTOR DECISION (DIR5 §2.8) - maximum two actions per decision window; '2 or 3' is closed and N = 3 is no longer a candidate final value. Retained: the legal-action rules and refusal logging as DELEGATED TECHNICAL work ('You own the detailed legal-action rules'); integration with ratified Action Capacity is required ('Do not replace or ignore'). Same for the charter observation at :83 ('1–3 whole actions per refresh … contains N ∈ {2, 3}'). **Replaced by** DIR5 §2.8 (D-079 DR22). [DIRECTOR DECISION; DELEGATED TECHNICAL DECISION]
- (`D-tactical.md:79`) — "The mapping onto ratified Action Capacity is **OPEN** (§10)." — in part: no longer an open Director question - DIR5 §2.8 orders integration with the ratified agent-specific Action Capacity ('Do not replace or ignore Action Capacity') and delegates the detailed legal-action rules (DELEGATED TECHNICAL). Retained: the listed points of contact as design inputs; the scoped Law 3 instrument (D-076 A-15) is ADMINISTRATIVE. **Replaced by** DIR5 §2.8 (D-079 DR22). [ADMINISTRATIVE CONSEQUENCE; DELEGATED TECHNICAL DECISION]
- (`D-tactical.md:155`) — "(rows :395-402), "proposed limits"." — in part: DIRECTOR DECISION (DIR5 §2.10) - the values are Director-preserved caps ('Recover and preserve the existing numerical caps'), not 'proposed' limits; values unchanged. DELEGATED TECHNICAL: the mortar counted object and the resolution of CAP-1..CAP-5 without new Director decisions; hard caps protect performance, ordinary scale is economy-governed. **Replaced by** DIR5 §2.10 (D-079 DR24, DR25). [DIRECTOR DECISION; DELEGATED TECHNICAL DECISION]
- (`D-tactical.md:164`) — "(the D-012 recommendation; the grid itself is OPEN as O-7)" — in full as to 'OPEN as O-7': DIRECTOR DECISION - hybrid grid pathfinding with continuous LOS/projectiles and fixed-point positions (already D-076 N6, O-7 (c); restated by DIR5 §2.7). Retained: grid and cell dimensions are PLACEHOLDER / DELEGATED TECHNICAL. **Replaced by** DIR5 §2.7 (D-079 DR21). [DIRECTOR DECISION; DELEGATED TECHNICAL DECISION]
- (`D-tactical.md:177`) — "| Action budget default | 2 | PLACEHOLDER (2 vs 3 is OPEN; both tested) |" — in full as to 'PLACEHOLDER' and '2 vs 3 is OPEN': DIRECTOR DECISION - maximum two. Retained: the default value 2. **Replaced by** DIR5 §2.8 (D-079 DR22). [DIRECTOR DECISION]
- (`D-tactical.md:196`) — "**ACTIVE CONCEALMENT.** A concealed entity counts as active iff, at pulse end, some enemy with primary activity in that" — in part: DIR5 §2.6 requires distinguishing genuine inactivity from active concealment, defence OR deliberate waiting; this definition counts a concealed unit as active only when an active enemy is within sight range, so a deliberately waiting hidden unit with no enemy near is disengaged at five pulses (test (ii), :248). Retained: the five-pulse threshold ('at least five pulses') and the active-concealment clause. DELEGATED TECHNICAL: extend the distinction to defence and deliberate waiting. **Replaced by** DIR5 §2.6 (D-079 DR19, DR20). [DELEGATED TECHNICAL DECISION]
- (`D-tactical.md:202`) — "Its on-field entities then become `Escaped` with `Disengaged = true`. They are preserved and" — in part: DIR5 §2.6 names the treatment of a hidden unit stationary for at least five pulses as 'routed or lost'; this classifies the disengaged side as intact survivors (Escaped). Retained: 'never deleted' matches 'Do not automatically destroy every stationary hidden unit'. DELEGATED TECHNICAL: which of routed/lost applies and the casualty/Ledger mapping (§2.9 coherence); narrows D-076 Q-B2. **Replaced by** DIR5 §2.6, §2.9 (D-079 DR19, DR20, DR23). [DELEGATED TECHNICAL DECISION]
- (`D-tactical.md:304`) — "> Update after verification (§11, F1): most of the bias below was a geometry error in `TacticalScenarios.Meeting`" — in part: classifying the 20,000 mm exit-distance difference (:305) as an 'error' corrected to mirror images is superseded - DIR5 §2.14: 'The previously observed 20-metre difference in distance to an exit must not be artificially corrected simply to equalize outcomes' (DIRECTOR DECISION). Retained: the pre-fix measurement record. DELEGATED TECHNICAL: classify the Meeting anchors as realistic asymmetry or accidental test bias and report per side. ADMINISTRATIVE: record that the F1 change predates DIR5. **Replaced by** DIR5 §2.14, §10 (D-079 DR31, DR32, DR53). [DIRECTOR DECISION; ADMINISTRATIVE CONSEQUENCE; DELEGATED TECHNICAL DECISION]
- (`D-tactical.md:338`) — "**O-14:** where the battle framework lives. The prototype namespace is `Proto.M7.Tactical`, not `Sim.Battle`." — in part: DIRECTOR DECISION (DIR5 §1.3) - the separate Sim.Battle project is the chosen architecture direction (O-14 option a); retained: Sim.Battle is NOT created until the architecture review, the prototype stays isolated; presentation/rendering project boundaries remain a technical recommendation (DELEGATED TECHNICAL). **Replaced by** DIR5 §1.3 (D-079 DR4–DR6). [DIRECTOR DECISION; DELEGATED TECHNICAL DECISION]
- (`D-tactical.md:339`) — "**O-8 / Action Capacity:** the budget's mapping onto ledger §17.2/§19 AC and onto `actionCapacityShare`, and whether" — in part: no longer an open Director question - DIR5 §2.8 orders integration with the ratified agent-specific Action Capacity ('Do not replace or ignore Action Capacity') and delegates the detailed legal-action rules (DELEGATED TECHNICAL). Retained: the listed points of contact as design inputs; the scoped Law 3 instrument (D-076 A-15) is ADMINISTRATIVE; whether movement spends AC (:340) is part of the delegated legal-action design. **Replaced by** DIR5 §2.8 (D-079 DR22). [ADMINISTRATIVE CONSEQUENCE; DELEGATED TECHNICAL DECISION]
- (`D-tactical.md:343`) — "O-35 immortality;" — in full as to O-35: DIRECTOR DECISION (DIR5 §2.9) - 'Soldier attrition and mortality belong to M7.14'. **Replaced by** DIR5 §2.9 (D-079 DR23). [DIRECTOR DECISION]
- (`D-tactical.md:344`) — "what Surrendered means (prisoners?);" — in part: DIRECTOR DECISION - contextual post-battle outcomes include taking and releasing prisoners for a defeated army (§4.2). Retained: eligibility, consequences, costs and state transitions are DELEGATED TECHNICAL; casualty/Ledger coherence required (§2.9). **Replaced by** DIR5 §4.2, §2.9 (D-079 DR23, DR37). [DIRECTOR DECISION; DELEGATED TECHNICAL DECISION]
- (`D-tactical.md:345`) — "what Disengaged/Escaped means in the Ledger and the campaign (recon-critic G3)." — in part: a five-pulse stationary hidden unit is treated as routed or lost (§2.6); battle-transient entities collapse into formation counts with coherent casualty/Ledger integration (§2.9). Retained: the exact Ledger mapping is DELEGATED TECHNICAL. **Replaced by** DIR5 §2.6, §2.9 (D-079 DR19, DR20, DR23). [DELEGATED TECHNICAL DECISION]
- (`D-tactical.md:356`) — "the naval and air modules (O-1: full tactical control decided, module out of scope);" — in part: 'module out of scope' is superseded for naval - the naval and combined-arms foundation is authorized Phase C work (§8 C.4) with full TacticalResolver support, direct ship control and controllable transport vessels (§2.1, DIRECTOR DECISION). Retained: the air module is not in the Phase C priority list (the architecture must still support air, §2). **Replaced by** DIR5 §2.1, §8 Phase C item 4 (D-079 DR8, DR51). [DIRECTOR DECISION]
- (`D-tactical.md:359`) — "**Performance budget O-12** is open. The numbers in §7 are measurements, not a budget." — in part: the method is decided (DIRECTOR DECISION) - the real Victus benchmark (RTX 4050 Laptop) precedes final numeric budgets and earlier targets are proposals. Retained: no budget values exist yet (they follow measurement); these numbers stay measurements. **Replaced by** DIR5 §7, §6.5 (D-079 DR48, DR50). [DIRECTOR DECISION]
- (`D-tactical.md:373`) — "| F1 (major) | **FIXED** |" — in part: the verdict 'FIXED' for moving side 1 by 20,000 mm to mirror the anchors is superseded as an outcome-equalizing correction (DIR5 §2.14, DIRECTOR DECISION). Retained: the reproduction evidence and the EdgeField distance test. DELEGATED TECHNICAL: decide whether the asymmetry was accidental test bias (keep a mirrored control) or realistic (keep an asymmetric scenario and report per side). ADMINISTRATIVE: the change predates DIR5. **Replaced by** DIR5 §2.14, §10 (D-079 DR31, DR32, DR53). [DIRECTOR DECISION; ADMINISTRATIVE CONSEQUENCE; DELEGATED TECHNICAL DECISION]
- (`D-tactical.md:419`) — "**`TacticalScenarios.CombinedArms` has the same anchor asymmetry** as F1" — in part: as an outcome-equalizing correction, re-anchoring CombinedArms to Cell(33)/Cell(35) 'before Slice E uses `CombinedArms`' (:421-422) is superseded (DIRECTOR DECISION); re-anchoring stays permitted if the classification (DELEGATED TECHNICAL, D-079 D13) finds the asymmetry to be accidental test bias. Retained: the measurement (east lost 7 of 10 in a 10-battle bench, not significant) and the classification task (DELEGATED TECHNICAL). **Replaced by** DIR5 §2.14, §10 (D-079 DR31, DR32, DR53). [DIRECTOR DECISION; DELEGATED TECHNICAL DECISION]
