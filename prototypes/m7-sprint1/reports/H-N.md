# Lane N — naval, air and combined-arms foundation in the TacticalResolver (M7 Phase C)

Date: 2026-11-05. Clone `/dev/shm/m7c/N`, branch `lane/N`, base `fc5230c`. Everything is in `prototypes/m7-sprint1`
(D-079 Q5 open: nothing touches `Sim.*`; no new project). Every number in a vessel, airframe, weapon or rule table is a
**PLACEHOLDER** (no ratified value exists); every statement below is marked **measured** (run in this clone) or
**inferred** (argued, not measured).

Provenance of this version: the lane was paused mid-run by the Director and resumed. On resume the work-in-progress was
inspected (`git diff --cached fc5230c` plus five later unstaged edits: the uncapped cap class and six new test cases), the
mutant scratch copy was found to hold the residue of the interrupted mutant N14 (ram damage line) and was restored to
byte equality with the clone before any further run; the lane clone itself never carried mutant residue (the harness
mutates only the scratch copy). The resumed session ran **throttled** on the user's instruction ("Throttle by 90%"):
`nice -n 19`, single-node builds (`-m:1`), a 3-seed domain bench, and a prioritized mutant subset (§6) instead of the full
list. Every number in §5–§7 was re-measured on the final tree after resume unless marked otherwise. **Correction
(verification round 1, N-R1-07):** the §7 land-only A/B table was measured BEFORE the pause and was not repeated after
resume; it is now marked so in §7. The round-1 fixes and every number measured for them are in §11.

## 1. What now exists (summary)

The naval and air modules are part of the **same** `TacticalBattle` as the land slice — one clock and event queue, one
order of resolution, one id space, one order log and replay, one knowledge model (m7-spec I.1). DIR5 §2.1 is implemented:
a naval battle with the default resolver goes to the tactical engine and is fought out there (measured: the moved
`NavalDomainIsNeverSilentlyAutoResolved` and `NavalBattleDefaultsToTheTacticalResolverAndIsResolvedTactically`); the
AutoResolver is never called implicitly (it still refuses naval — lane P's territory, untouched).

| Area | What it can do (all measured by tests unless marked) |
|---|---|
| Naval movement | Hulls with fixed-point position, heading (binary angle units) and speed; per-second turn rate, acceleration and max speed integrated over the step; water-only movement (naval view of the grid), no hull overlap; line ahead behind a path-following leader, line abreast on arrival; sail propulsion with a wind polar (no-go zone, close-hauled tacking); crew losses slow the hull and its reload |
| Naval combat | Weapon mounts with arcs (bow, broadside, turret), ranges, reload, ammunition; hit roll vs evasion with range falloff; hull and crew damage; ram (damage ∝ closing speed, cooldown, stops the rammer); boarding (simultaneous crew fight with stochastic rounding), capture by the strike-colours threshold or when a grappled hull routs; sinking; derelicts; damage control (bounded repair pool); formation morale and rout to the exit edge. Round 1: damage control and the boarding stochastic rounding had no semantic test before round 1 (N-R1-03b/c); they have now (§11) |
| Transports | Embark / Disembark orders with capacity (refused if the formation does not fit), time cost per person per hull (exact ms carry; **round 1: ONE time budget per hull and step, shared by every formation it serves and by both operations — N-R1-01**), hull must be stopped; amphibious landing onto passable land cells near the hull, landed troops exposed for 20 s; troops aboard take a share of crew casualties, are lost or (fraction) saved when the hull sinks, captured with a taken hull, withdraw with an escaping hull — always through personnel-ledger transfers. Round 1: the stopped-hull rule, the share of crew casualties for troops aboard and the 20 s landing exposure had no semantic test before round 1 (N-R1-02, N-R1-03a); they have now (§11) |
| Air | Based / Airborne / Returning phases; take-off at order time, sortie count, turnaround (**round 1: an airframe at or below the damage-abort threshold is grounded for the rest of the battle — no in-battle repair, so no relaunch — N-R1-06**); fuel = flight time, burn ≥ 1 per step; automatic return at bingo fuel, empty racks or damage; fixed wing never below minimum speed — on station it loiters AT the minimum speed (maximum endurance) and circles, it cannot hover; rotary wing hovers but burns fuel doing it; recon (look-down observation through fog), strike (bombs over the target, rockets at stand-off), interception (forward guns), escort (intercepts threats to the escorted formation), search over the front when the target is unknown |
| Combined arms | Cross-domain knowledge with one acquisition draw per side and target (best observer of any domain; metamorphic test); shore bombardment and land/air area fire with dispersion and blast falloff; air support to land through the land damage path (HP, death, losses, ledger Killed); danger-close refusal; land guns (coastal, field, air-defence) as domain weapons of land formations, uncapped as CAP-5 rules; engagement eligibility by target class (NotCapable refusals); escort |
| Snipers | Independent formations, cap 2 (proposed caps table), personal concealment (camouflage), long range and sight, low fire rate (`FireIntervalSteps`), counted as "concealed" for the active-concealment clause |
| Formation commands | All domains through the same `OrderRequest` / order log; six new kinds (Embark, Disembark, Board, Bombard, Patrol, Escort), each charged to the action budget as one of the six land classes, so the two-action maximum and the anti-loop rules apply to every domain |
| Personnel | A battle personnel ledger (law 1 stand-in): every person in exactly one account, transfers only, total constant at every instant (checked at every completed instant in every new test) |
| Classes by capability | `NavalDesign.Derive(capabilities, role)` — oars/sails/steam × gunpowder/turrets → class; no date or era input |

Land-only battles are bit-identical to Slice D: every hook is a no-op without vessels, aircraft, domain weapons or
embarked formations (measured: the Slice D trace/state goldens, the parity battery and all 369 pre-existing tests pass
unchanged except one moved pin, §8).

## 2. Architecture and the shared-file edits

New files (all code of the lane):

| File | Content |
|---|---|
| `Proto.M7/Fixed/FixedTrig.cs` | integer sine table (Taylor series in Int128 Q60 at type init), nearest-angle atan2, angle arithmetic |
| `Proto.M7/Spatial/HeadingMotion.cs` | turn/speed/displacement integration, braking speed, the naval view of a grid |
| `Proto.M7/Tactical/DomainTypes.cs` | `WeaponSpec`, `VesselClass`, `AirframeClass`, `DomainRules` (incl. wind polar), `MultiDomainSetup`, enums |
| `Proto.M7/Tactical/DomainProfiles.cs` | PLACEHOLDER weapons, 10 vessel classes, 4 airframes, sniper / riflemen / gun-crew profiles; `NavalDesign` |
| `Proto.M7/Tactical/PersonnelLedger.cs` | the personnel ledger and its report |
| `Proto.M7/Tactical/DomainView.cs` | controller-facing views, `DomainAi`, `CompositeController` |
| `Proto.M7/Tactical/MultiDomainScenarios.cs` | line battles (three propulsion eras), amphibious combined-arms scenario, plan and defence |
| `Proto.M7/Tactical/TacticalBattle.Domain.cs` | domain state, construction, order routing and validation, views, inactivity, results, hashing, ledger glue |
| `Proto.M7/Tactical/TacticalBattle.DomainMove.cs` | vessel and aircraft movement, transport operations, activity minima |
| `Proto.M7/Tactical/TacticalBattle.DomainCombat.cs` | cross-domain knowledge, target choice, fire, boarding, ram, damage, resolution |
| `Proto.M7.Bench/DomainBench.cs` | `domain` bench mode (also `golden`, `watch`) |
| `Proto.M7.Tests/MultiDomain/*.cs` | 8 test files, 61 test cases |

Edits to existing shared files (small, append-only or delimited by `// lane N` comments; none in T3's
`WarTheaterWorld.cs`, `Runner.cs`, `EventScheduler.cs`):

| File | Edit |
|---|---|
| `TacticalBattle.cs` | `partial`; constructor splits land / domain formations and calls `DomainInit`; hooks in `IsLive`, `Execute`, `PulseStart`, `ApplyOrder`, `BuildView`, `Step`, `Detect` (3), `Fire` (sniper interval), `Resolve`, `SetState` (ledger), `CheckElimination`, `PulseEnd` (2), `IsConcealed`, `SearcherNear`, `End` (embarked count, domain results, personnel report), `StateHashHex`; `Ent.NextRangedMs`; personal-concealment line in detection — 74 changed lines |
| `TacticalTypes.cs` | `OrderKind` += 6 kinds (values 6–11), `EntityState.Embarked = 5`; Domain doc |
| `Control.cs` | `OrderInvalid` += 5 codes (8–12); `SideView.Domain` property |
| `BattleSetup.cs` | `FormationSpec` += `Vessel`, `Airframe`, `Weapons`, `EmbarkedOn` (init); `BattleSetup.MultiDomain` |
| `UnitProfiles.cs` | `UnitProfile` += `FireIntervalSteps` (default 1), `PersonalConcealment` (default One); `CapFamily.Uncapped = 8` handled by `CapOf` (int.MaxValue), `Check` (never refuses a positive count) and `WithOverride` (refuses: no cap to override) |
| `BattleResult.cs` | `FormationResult.EmbarkedAtEnd`, `BattleResult.Personnel` (init; `ResultHash` unchanged); round 1: doc comments only — the counted unit per domain (N-R1-05) |
| `ActionBudget.cs` | `ClassOf(kind)` and its use in `Check`/`TryAccept` (6 lines) — **merge note for T3** |
| `Proto.M7.Bench/Program.cs` | one dispatch line |
| `ProfilesCapsDomainTests.cs` | the moved pin (§8) |

## 3. Equations and rules (all coefficients PLACEHOLDER unless stated)

**Angles and trigonometry.** One turn = 65,536 units. `Sin` from a quarter-wave table of 16,385 Q30 entries computed by
an Int128 Q60 Taylor series (π/2 · 2^60 as a literal); `|sin² + cos² − 2^60| ≤ 2^32` over all angles; `Atan2` returns the
table angle nearest the direction (binary search on exact cross products), within 1 unit of the true angle over all
65,536 angles (measured, two radii). Table hash pinned (`B1DE0D3E…`).

**Vessel kinematics (per step, dt = StepMs).** desired (heading h*, speed v*) from the directive;
`Δh ≤ TurnRate·dt/1000 · clamp(2v/vmax, 1/4, 1)` (sail: rudder needs way; oar/engine factor 1);
`vmax = MaxSpeed · crewFactor · sailFactor(h)`, `crewFactor = clamp(crew/crewNeeded, 1/4, 1)` (0 when nobody aboard);
`v* ≤ vmax · max(1/4, cos(h* − h))` (a hard-turning hull loses way); `|Δv| ≤ Accel·dt/1000`; end point
`p + (v·dt/1000)·(cos h, sin h)` rounded toward zero; resolved by the Slice C separation step on the naval view (water
only, no overlap); a truncated move sets `v` to the distance actually covered. Arrival: inside `max(2r, cell/2)` a
braking hull stops. Movement priority between hulls: counter-RNG draw per (instant, hull) (as land, no side bias).

**Sail polar.** off = |heading − windFrom|: `off ≤ 60°` → 0 (no headway); close-hauled 500‰ rising linearly to 1000‰ at
90°; 1000‰ to 135°; falling to 750‰ dead downwind. A sail hull never points inside the no-go zone: it takes the
close-hauled heading on the tack nearer its heading (crude tacking; measured: makes upwind progress; zero speed inside the
zone). Engine and oar hulls ignore the wind (measured: identical trace with and without wind).

**Line ahead / abreast.** Leader = lowest-id Active hull; it follows the A* waypoint path (naval view) at 3/4 of the
slowest hull's maximum; follower k keeps station `k · LineSpacing` astern (`LineSpacing = 8 radii`); when the leader has
arrived, followers form line abreast on it (transports side by side off a beach).

**Directive behaviour (vessel).** MoveTo/Patrol (path, then stop), Hold/Rally (heave to; Rally repairs
`RepairPerS · dt` hull from a pool of 20 % of Hull), Attack vessel (ram ship: straight at the nearest tracked target at
full speed; broadside: put the target abeam, closing at 45° beyond the preferred range `0.7 · maxRange`, opening at 45°
inside half of it; bow/turret: close to the preferred range), Attack land / Bombard (approach to the preferred range of the
area weapon, abeam for broadsides), Board (close at full speed, slow to the boarding speed in the last hull lengths),
Escort (screening station two line spacings abeam of the escorted formation's course — its lead hull's heading — weapons free), Retreat/rout (descend the naval exit field; at the edge → Escaped).

**Engagement sequence (m7-spec REV:432).** For each shooter (ascending id) and mount (ascending index): reload elapsed and
ammunition left → candidates = enemies the side TRACKS, whose class is in the weapon's targets, in `[min, max]` range
(slant range to an airborne target, horizontal otherwise), inside the mount arc (bow: ±ArcHalf of the heading; broadside:
±ArcHalf of either beam; turret: any), with LOS for direct fire from the shooter's eye height; area fire refused when a
friendly land entity is within `blast + 2·dispersionHalfWidth` of the aim point ("danger close"); limited-ammunition
weapons fire only at the designated target. Choice = minimal `(priority, distance², entity id)`; priority 0 = the
formation's directive target. Measured tie-dense test: twelve hulls at exactly 500 m → lowest id, independent of input
order; designated target first.

**Hit and damage.** Direct: `p = clamp(acc/(acc+def), HitMin, HitMax) · (F + (1−F)·(max−d)/max)`, F = 500‰, def = vessel or
airframe evasion, or the target formation's effective defence for a land entity; one counter-RNG draw keyed (shooter,
instant, target, mount). Area: impact = aim + uniform offset in a square of half-width `range · dispersion‰`
(two `NextBelow` draws); each land entity of EITHER side within the blast radius R is hit with probability `(R − d)/R`;
vessels likewise with d to the hull edge. Land hit: HP (then the land death/morale path). Vessel hit: `hull −= HullDamage`;
`people = min(CrewDamage, aboard)` split `emb = ⌊people · embarked/aboard⌋`, `crew = people − emb` (crew via ledger
Crew→Killed immediately; embarked victims chosen at Resolve by ascending (counter-RNG draw, id)). Air hit: airframe HP.

**Ram.** Ram ship, Attack/Board directive, speed ≥ max/2, enemy hull in grappling contact inside ±30° of the bow, cooldown
over: hull damage `⌊RamDamage · v / vmax⌋` (≥ 1), rammer stops, cooldown 30 s. Measured: damage in [Ram/2, Ram] and
exactly the target's hull loss; no ram at boarding speed below max/2. (Round 2, N-R2-07, measured: against a single hull the
cooldown never binds — the rammer is held in contact at speed 0 and never regains ramming speed there; it binds when the
rammed hull sinks and the next one is near: a 2 s cooldown rams again 10 s later, the 30 s cooldown does not — §12.)
Superseded in part by D-081 (2026-11-05); see H-RAM.md

**Boarding.** Board directive, speed ≤ 2.5 m/s, enemy hull within `rA + rB + 5 m`: one fight per unordered pair per step;
casualties on B = `aboardA · p(meleeA, defenceB) · 20‰/s · dt` with the integer part plus a counter-RNG Bernoulli on the
remainder (exact expectation), capped; both directions from pre-fight numbers (simultaneous). Capture at Resolve when
`aboard ≤ ¼ · aboard-at-fight-start` and the boarder has more people, or when a grappled hull is routing (the land rule
"routing, blocked, enemy in contact ⇒ surrender"). A taken hull: crew → Captured, embarked troops → Surrendered.

**Sinking.** Hull ≤ 0 at Resolve: `saved = ⌊crew · s⌋` (s = ShipwreckSurvival, default 0) → Escaped (ShipwreckSurvivor),
the rest → LostWithVessel; embarked troops ordered by (draw, id), the first `⌊n · s⌋` escape, the rest die
(LostWithVessel), each through a ledger transfer; formation morale takes the losses. Nobody aboard ⇒ derelict (Dead).

**Transports.** Embark (land formation → own transport formation): valid if the transport formation's free capacity ≥ the
formation's Active count and a land path to the embark point (nearest passable land cell to the lead hull within 60 m)
exists; per hull per step, while stopped (≤ 0.5 m/s) with someone in range: `credit += dt`; board
`min(⌊credit / EmbarkMsPerPerson⌋, in range, space)` in ascending id; credit reset when nothing more can board (no banking
of idle time). **Round 1 (N-R1-01):** the credit is ONE budget per hull and step — `dt` is added once, the first time the
hull works in the step, and every person boarded or landed (any formation, either operation) spends from the same carry,
in the fixed order embarking formations ascending, then landing formations ascending (hulls ascending); the carry is cleared
at the end of the step unless time was what stopped the hull (people still waiting). Disembark (to a passable land point): per hull while stopped and within 80 m of land: same carry with
`DisembarkMsPerPerson`; landing spots = passable land cells within 80 m ascending (distance², index), a 5×5 lattice at
slot spacing per cell, first point overlapping no on-field entity; landed troops exposed 20 s; the formation's anchor
moves to the first spot and it marches to the ordered point. Embark/landing are **not** primary activity (they are
reversible; counting them would let an embark/disembark loop keep a battle alive — measured: the loop scenario ends
BothDisengaged; the mutant counting them ends otherwise).

**Air.** Take-off at order time (order delivery pin), if a sortie is left and turnaround is over (else NotReady /
NoSorties); round 1 (N-R1-06): and the airframe is above the damage-abort threshold (`HP > ½`) — no in-battle repair, so a
damaged airframe that came home stays on the ground (NotReady when no airframe of the formation can launch). Burn per step = `⌈dt · max(v, floor)/cruise⌉` with floor = MinSpeed (fixed wing) or `1.2 · cruise` (rotary
hover) — strictly positive. Bingo: `fuel ≤ ⌈dist(base)·1000/cruise⌉ + 60 s` ⇒ Returning; also empty limited racks or
`HP ≤ ½` ⇒ Returning; orders cannot override it (measured). Landing within 100 m of base: refuel, turnaround starts.
Fixed wing: `v ≥ MinSpeed` always; within 2 km of its patrol point it requests no speed and the minimum-speed floor keeps it
flying (loiter, circling its point); rotary: hovers within 25 m, slowing with `max(0, cos)` of the
bearing error. Observation: look-down, clarity = the concealment of the target's own cell (canopy), exposure and personal
concealment as on land; positions may exceed the battle area by 2 km (orbits), where ground observers cannot see them.
Unknown attack target ⇒ search over the midpoint of the two air bases; escort target gone ⇒ return.

**Knowledge across domains.** Land targets: the land loop draws first with p_land; this layer draws with the SAME key and
the vessel/aircraft observers' best p_x, so acquisition is `u < max(p_land, p_x)` — one draw per (side, target, instant)
against the best observer of any domain. Vessels and aircraft as targets: detection range = observer sight × target
signature (vessels 300–400 %, aircraft 200 %); LOS from land and sea; clarity 1 (sea and sky do not hide); grappled hulls
are in contact.

**Inactivity / termination.** Domain formations join the five-pulse rule with the same activity definition (damage dealt
or taken, a valid shot, a new minimum of ≥ 1 cell toward a tracked enemy / objective / exit, state change, first
detection, take-off/landing). Bounded potentials: hull, HP and crew are finite and every engagement has `p ≥ HitMin > 0`
and damage ≥ 1; sorties are finite and fuel strictly decreases; boarding casualties have positive expectation. A based
aircraft with sorties and an airframe fit to fly keeps its side in the battle (it can still act) but does not hold the
field; one grounded by damage does not (no repair in battle — round 2, N-R2-01). (Inferred argument;
every test battle terminated under the 400-pulse harness guard — measured.)

**Snipers.** `UnitProfile.FireIntervalSteps` (default 1) gates legacy ranged fire: next shot no earlier than
`t + interval · StepMs` after a valid shot; `PersonalConcealment` multiplies the enemy's acquisition chance unless the
sniper fired within the last step, and makes it "concealed" for the active-concealment clause. Sniper profile: cap family
Snipers (2), range 30 cells / sight 35 (default 20 m cells), interval 5 steps, concealment 1/8.

**Personnel ledger.** Accounts (formation, place, carrier): Field, Aboard(vessel), Crew(vessel/aircraft), Killed,
LostWithVessel, Captured, Escaped; opening balances = where people are at the start; afterwards transfers only (land state
changes mirror through `SetState`; crew casualties, sinkings, captures, landings, embarkation). `PersonnelConsistency()`
cross-checks the ledger against every entity state; `DT.RunChecked` calls it at **every completed instant** of every
new battle test (measured: never violated).

## 4. Determinism

Integer only (`check-proto-determinism.sh`: CLEAN, 57 files — measured). Every loop in ascending entity / formation id;
every choice over a score uses a composite key with the id last; every random draw is a counter-RNG draw keyed by
(stream, ids, instant) — new streams `0x7AD0_0001…0006` (Hit, Dispersion, Boarding, Victims, MovePriority, Blast); the
cross-domain acquisition reuses the land Detection key on purpose (one draw per side). No LINQ, no unordered containers
in the new Proto.M7 code (grep). Replay from the order log reproduces trace, state, result and personnel hashes (measured,
amphibious battle); permuting the formation input order gives the identical battle (measured).

## 5. Tests (measured)

(Counts below are the lane's pre-round-1 tree `623c6ec`; the round-1 tree has 444 — see §11.)

| Solution | Debug before | Debug after | Release before | Release after |
|---|---|---|---|---|
| `M7Proto.slnx` | 369 pass / 0 fail / 0 skip | **430 / 0 / 0** | 369 / 0 / 0 | **430 / 0 / 0** |
| `M7Econ.slnx`, `M7DetMath.slnx` | not touched by this lane, not re-run | — | — | — |

Before = base `fc5230c` built and tested in a separate clone (`/dev/shm/m7c/N-base`) earlier the same day; the base tree
is unchanged, so those counts stand. After = the final lane tree, re-run after resume (Release 430 in 10 s, Debug 430 in
31 s test time; both binaries newer than every source file — checked).

New test cases (61 = 430 − 369, all in `Proto.M7.Tests/MultiDomain`; counted with `--list-tests`): FixedTrig (5:
endpoint/symmetry/identity over all angles, atan2 inverse, TurnToward rules, table golden, dt-correct helpers); Naval (15:
turn/speed/accel bounds for two step lengths, water only in the amphibious battle, a hull steering inland stops at the
shore, an escort keeps station on moving transports, wind no-go and upwind progress, engine ignores wind, arcs for
broadside/bow/turret, tie-dense target choice + designation, ram, boarding capture through the ledger, strike-colours
threshold before any rout, sinking with exact survivor split, crew losses slow the hull and lengthen its reload exactly,
capability derivation without dates); Transport (8 incl. 3-way delivery pin); Air (12 incl. 3-way take-off pin, damage
abort exactly at the ½-HP threshold (two cases), strike aircraft supporting the land battle through the land damage path
and the ledger); Combined arms (8 incl. crew-served guns uncapped while capped families keep their caps, and the
one-acquisition-draw metamorphic test under common random numbers); Snipers (4); Orders and goldens (9 incl. 3-way vessel
order pin, replay, permutation, sail golden, land golden untouched).

Goldens pinned (Linux x64, .NET 10; integer-only ⇒ expected platform-independent, cross-platform untested, O-17):
FixedTrig table `B1DE0D3E…3323`; amphibious battle seed 42 trace `14B8A338…F7DA`, state `E0AA0503…ADD1`, personnel
`08280726…E85C` (**re-pinned in round 1**, cause N-R1-06 — §8, §11); sail line battle seed 42 trace `8433C5E9…7685`. Slice D
goldens unchanged.

POPULATED-table rule: this lane adds **no serialized row type** (the personnel ledger is hashed, not serialized), so no
round-trip test is owed; when the ledger or the domain setup is persisted (sandbox scenarios, M7.18) it owes one.

## 6. Mutation testing (measured; separate scratch tree; bound = 10 × the clean time)

Harness: `N-work/mutants.py` (kept out of the repository) mutates only the scratch copy `/dev/shm/m7c/N-mut`, restores
every file from the lane clone after each run and checks its SHA-256. One run = Release build + the filtered test run
(`MultiDomain` + `ProfilesCapsDomainTests` + `ActionBudgetTests`). A run that hits its bound is recorded as HANG; none did.

- Run 1 (before the pause, `-m:2`): bound from the clean filtered build+test of the same copy (36 s build + 24 s test under
  load average ≈ 40); the exact BOUND value was not logged. 84 tests in the filter then.
- Run 2 (after resume, `nice -n 19`, `-m:1`): clean time measured after touching a Proto.M7 source (forces the same
  recompile a body mutation causes; the tests' reference assembly is unchanged so the test project is not recompiled) =
  **8 s** → bound **80 s**. 87 tests in the filter (the six new cases are in it).

Every kill below includes at least one semantic (non-golden) test; golden-only kills are not counted.

| Mutant (what it breaks) | Run | Result | Semantic killer(s) (goldens that also failed in brackets) |
|---|---|---|---|
| N01 turn rate ignored | 1 | KILLED 60 s | `TurnRateSpeedAndAccelerationBoundsHoldEveryStep` ×2 [sail, amphibious] |
| N02 acceleration ignored | 1 | KILLED 71 s | `TurnRateSpeedAndAccelerationBoundsHoldEveryStep` ×2 [sail, amphibious] |
| N03 vessels move on the land grid | 1 | KILLED 82 s | escort station, boarding capture, kinematic bounds ×2, sail no-go, ram, steering inland stops at shore [sail, amphibious] |
| N04 sail no-go zone ignored | 1 | KILLED 106 s | `SailingShipMakesNoWayInsideTheNoGoZoneButBeatsUpwind` [sail] |
| N05 wind slows engines too | 1 | KILLED 97 s | `EnginePropulsionIgnoresTheWind` |
| N06 escort screen offset ×4 | 1 | KILLED 43 s | `EscortKeepsItsScreeningStationOnTheTransports` |
| N07 crew factor ignored | 1 | golden-only (not counted) | [sail, amphibious] |
| N07 crew factor ignored | 2 | **KILLED 11 s** | `CrewLossesSlowTheHullAndLengthenItsReload` (new) [sail, amphibious] |
| N10 broadside bears anywhere | 1 | KILLED 126 s | `BroadsideBearsOnlyAbeamBowGunsOnlyAheadTurretsAnywhere` [sail] |
| N11 bow arc ignored | 1 | KILLED 133 s | `BroadsideBearsOnlyAbeamBowGunsOnlyAheadTurretsAnywhere` [amphibious] |
| N12 tie-break → highest id | 1 | KILLED 96 s | `TargetChoiceIsTieDenseLowestIdThenDesignatedFirst` |
| N13 designation ignored | 1 | KILLED 91 s | `TargetChoiceIsTieDenseLowestIdThenDesignatedFirst`, `CoastalGunsHoleShipsAndStrikeAircraftBombThem` |
| N21 sinking: embarked troops vanish (no ledger flow) | 2 | KILLED 10 s | `SunkTransportsTroopsAreLostThroughLedgerFlowsNeverVanish` ×2 |
| T01 transport capacity check removed | 2 | KILLED 9 s | `EmbarkRefusedWithoutCapacityAndByTheWrongDomain` |
| T07 embark without ledger transfer | 2 | KILLED 10 s | `EmbarkTakesEffectAtTheDeliveredStepAndCostsTime` ×3, `EmbarkDisembarkLoopIsNotActivitySoTheBattleStillEnds` |
| A01 fixed wing may fly below minimum speed (hover) | 2 | KILLED 9 s | `FixedWingCannotHoverRotaryHoverBurnsFuel` |
| A11 damage abort ignored | 2 | KILLED 10 s | `DamageAbortSendsTheAircraftHomeOnlyAtTheThreshold` (new) [amphibious] |
| A13 bombs never hit land | 2 | KILLED 10 s | `StrikeAircraftSupportsTheLandBattleThroughTheLandDamagePath` (new) [amphibious] |
| C03 independent cross-domain acquisition draw | 2 | KILLED 8 s | `CrossDomainAcquisitionIsOneDrawAgainstTheBestObserver` (new) [amphibious] |
| O02 domain order applied one step late | 2 | KILLED 11 s | `VesselOrderTakesEffectAtTheDeliveredStep` ×3 (turn-exact pin, ±1), `StrikeAircraftSupports…`, `CrewLossesSlow…` [amphibious] |

Totals: 19 distinct mutants run to completion (11 in run 1; 9 in run 2, one of which re-ran N07), 0 survived, 0 hung, 0
build failures. **Not run** (designed and syntax-checked by `mutants.py --check` — every pattern matches exactly once — but not
executed because of the throttle): N08, N14 (interrupted by the pause; its result is void), N15–N20, N22–N27, T02–T06,
A02–A10, A12, C01, C02, C04, S01–S03, O01 — 36 mutants. Their kill status is **unknown**, not assumed. The teeth question
(ADR-015 §7.2) for the run-2 kills: each killing test asserts a property the brief requires (no hovering exploit; capacity;
troops never vanish; one draw per side; turn-exact order delivery; crew-limited speed and reload; damage abort; air support
hits land through the land path).

## 7. Performance (Proto.M7.Bench, Release, 4 CPUs shared by ~9 agents; Stopwatch for reporting only)

**Round 2 (N-R2-06):** the numbers below describe the pre-round-1 tree. The delivered tree was re-measured against
`623c6ec` in an interleaved A/B — see §12.4; that table supersedes the "after" columns here for the delivered tree.

Land-only regression check — interleaved A/B, baseline `fc5230c` build vs lane N build, `tactical 5`, three runs each,
identical pulses and events in both (measured **BEFORE the pause**, on the pre-resume tree; not repeated after resume nor
in round 1 — N-R1-07):

| battle (5 seeds) | baseline total ms (3 runs) | lane N total ms (3 runs) |
|---|---:|---:|
| 50v50 heavy infantry | 2394 / 1426 / 2335 | 2205 / 2401 / 2366 |
| 60v60 archers | 1789 / 1140 / 2661 | 1471 / 2063 / 1908 |
| 50v60 | 1494 / 1386 / 1802 | 1287 / 1746 / 1550 |
| 110v110 combined arms | 6696 / 6889 / 6206 | 4347 / 6134 / 6167 |
| sum | 36,218 | 33,645 |

No measurable regression at this noise level (run-to-run spread of the same build is up to ~2×).

New domain battles (`domain 3`, after; run once at `nice -n 19` on the shared machine — 3 seeds instead of 5 because of
the throttle; Stopwatch, reporting only; every battle conserved its personnel total, `people n/n`):

| battle (3 seeds) | mean pulses | mean events | total ms | median ms/pulse | max ms/pulse |
|---|---:|---:|---:|---:|---:|
| galleys 5v5 (oar, ram) | 18.7 | 2712 | 451 | 9.47 | 9.54 |
| sail line 5v5 (broadside, wind) | 48.0 | 4750 | 376 | 2.67 | 2.95 |
| turret 5v5 (engine, long range) | 10.3 | 1788 | 35 | 1.13 | 1.20 |
| amphibious combined arms (10 formations, 119 entities at start) | 14.0 | 1968 | 534 | 12.14 | 14.09 |

There is no "before" for these battles (the module did not exist at `fc5230c`). Galley battles are the most expensive per
pulse among the pure naval ones (close-range manoeuvring: ram and boarding contacts every step); the amphibious battle is
dominated by the land slice it contains. The numbers include the shared-machine noise (§7 above: up to ~2× run-to-run).

## 8. Moved pins and behaviour changes (with attribution)

- `ProfilesCapsDomainTests.NavalDomainIsNeverSilentlyAutoResolved`: asserted `NotSupportedException("module is not
  built")`. DIR5 §2.1 orders the naval module, which this lane built. The protected invariant (default resolver Tactical,
  AutoResolver never called implicitly) is unchanged; the test now asserts that an invalid naval formation (no vessel class)
  is refused as an invalid setup without touching the AutoResolver, and that a valid naval battle resolves tactically.
- `ActionBudget.Check/TryAccept` accept the six new kinds by class (previously they threw for kind > Reengage);
  `ActionBudgetTests.EachKindAtMostOncePerInterval` iterates every `OrderKind` and passes unchanged.
- No other existing test or golden moved.
- **Round 1:** the amphibious golden (trace, state, personnel) was re-pinned: `14B8A338…F7DA` / `E0AA0503…ADD1` /
  `08280726…E85C` → `3C5DA2E7…7944` / `9A439187…6B48` / `E28B1BE9…C963`. Cause: N-R1-06 only (the launch gate). Measured
  attribution: the tree with only the N-R1-01 change reproduces the three old hashes; the tree with only the N-R1-06 change
  reproduces the three new ones. The sail golden and the Slice D goldens did not move.
- **Round 2:** the amphibious golden (trace, state) was re-pinned again: `3C5DA2E7…7944` / `9A439187…6B48` →
  `5B72E757…9AAF` / `6E3963D9…44A0`; personnel `E28B1BE9…C963` unchanged. Cause: N-R2-03 only (§12.2).

## 9. Limitations (honest list)

- **Placeholders everywhere.** All vessel, airframe, weapon and rule values are invented orders of magnitude; accuracies
  were lowered after the first runs because modern gunnery and air defence hit far too often (battles ended in 2 pulses);
  not calibrated, not historical claims.
- **No fortification / protection model.** A coastal battery is as exposed as infantry in the open (garrisons and works
  are C30, not this lane); blast lethality ignores cover and posture (close-order troops are devastated; the amphibious
  scenario sets 5 m spacing as a test input).
- **Land LOS concealment ignores height** (pre-existing Slice C rule): a sight line from a mast over a forest is concealed
  by every forest cell it crosses.
- **Hulls are discs**, not ellipses; the hull centre (not its edge) is held to water; no shallows, draught, tides,
  currents, sea state, night, weather or visibility; wind is a constant battle input (no source in the campaign yet).
- **Tacking is crude** (close-hauled on the nearer tack, no planned beats); no anchoring, no towing, no fire/flooding
  spread, no ammunition magazines on vessels (unlimited unless the weapon says otherwise), no critical hits.
- **Air** is a single altitude per airframe class (no climb, no altitude tactics, no radar, no SAM guidance, no carrier
  operations); aircraft off the battle area cannot be seen from the ground; interception is pure pursuit; bombs vs ships
  use a hit roll (vs land: dispersion).
- **Boarding** uses the vessel's crew values for embarked troops too; prize crews are not modelled (a taken hull leaves the
  battle).
- **Domain AI** (`DomainAi`) is minimal (attack the nearest known enemy vessel / aircraft); the amphibious attacker is a
  script. No AI for transports or bombardment planning.
- **Snipers** use the global damage formula (`Ranged / 5`): three hits to kill heavy infantry; no target priority
  (officers), no counter-sniper behaviour in the land AI.
- **Siege** (DIR5 §2.2) is not in this lane.
- **Scale/perf** with many hulls is untested beyond 10 vessels + ~120 land entities; vessel separation reuses the O(n²)
  Slice C step.
- **AI-vs-AI naval** (RDG-3: AutoResolver) cannot run: the AutoResolver has no naval model (lane P / later).
- **Mutation pass is partial** (throttled resume): 19 of 55 designed mutants ran (all killed by semantic tests); 36 were not
  run and their status is unknown (§6). The domain bench ran 3 seeds once, not 5.
- **`M7Econ.slnx` and `M7DetMath.slnx`** were not re-run (this lane does not touch their projects; inferred unaffected).
- **Round 1 additions.** `FormationResult` counts hulls / airframes for vessel / aircraft formations (now documented, N-R1-05);
  per-person measures of a multi-domain battle must come from `BattleResult.Personnel`. Bombard, Board and Escort have no
  turn-exact pin of their own (their delivery is the same directive switch as the pinned vessel MoveTo; their first EFFECT
  depends on geometry); Disembark now has one (N-R1-04). OBSERVATION, not fixed (outside the round-1 findings): a landed
  soldier who fires inside its 20 s exposure window gets `RevealUntilMs = t + StepMs`, which SHORTENS the landing exposure
  (inferred from `TacticalBattle.cs:701/708` and `TacticalBattle.DomainCombat.cs:370`; not measured). An airframe grounded by
  damage is never repaired in battle (no repair mechanism exists; a proposal, not a decision). The domain and land benches
  were not re-run in round 1 (throttle).

## 10. Proposals for the Director (not decided here)

Each is a PROPOSAL with rationale; the Director decides. Items marked DELEGATED were decided here under the delegated
technical authority (DIR5 §2.5 last line; C29 "Delegated") and are reported, not asked.

1. **Transports under "Ships 5" (CAP-5 READING, OD-5).** The prototype applies the READING already recorded in the M7.2/M7.3
   revision §5 — every vessel family, transports included, is capped at 5 hulls per formation. Proposal: confirm the reading.
   Consequence: lifting a 50/60-person formation takes 2 hulls at the placeholder capacities.
2. **Crew-served guns (coastal, field, air-defence) are uncapped — applied, not decided here.** D-076 §5 CAP-5 (`d076:303`)
   lists siege and air defence among the families with no cap ("Any cap for them would be invented"), and D-079 R6 keeps them
   uncapped. The gun-crew profile (family key 8 = siege, which holds bombards and field artillery; anti-aircraft guns are
   air defence, key 14) therefore uses an explicit `CapFamily.Uncapped` class: `Check` never refuses a positive count and no
   cap value exists to override (measured: a 12-team AA battery is accepted; Ships 5 and Snipers 2 still refuse). An earlier
   draft of this lane used the Mortars cap (5) as a reading; that would have invented a cap and was replaced. Proposal: the
   caps table in the M7 spec carries an explicit "uncapped (economy-governed)" row for these families, so tools and
   tests read the rule from one place.
3. **Survivors of a sinking / shoot-down.** Default 0 % (everyone aboard lost) is a conservative PLACEHOLDER; the mechanism
   (exact split, survivors Escaped through the ledger) exists. Proposal: derive survival from state (sea state, distance to
   friendly hulls or shore, rescue capability) when weather and harbours exist; until then calibrate a single fraction per
   era-independent hull type. Whether rescued enemies become prisoners is a post-battle (C32) question.
4. **Wind and weather as battle inputs.** Sail-era tactics depend on the wind (weather gauge); the prototype takes a constant
   wind as a battle input. Proposal: the campaign's climate state supplies wind (and later visibility/sea state) at battle
   setup; until then sandbox battles set it explicitly.
5. **Air basing.** Bases are points on each side's map edge (an abstraction of off-map airfields / carriers). Proposal: bind
   bases to campaign airports and harbours through C30 garrisons; carriers wait for an authorized packet.
6. **AI-vs-AI naval battles (RDG-3).** The campaign READING sends AI-vs-AI battles to the AutoResolver, which has no naval
   model (it refuses naval). Proposal: build the naval auto model by fitting it to this tactical module (the C16 battery
   method), before any campaign AI-vs-AI naval battle can occur. Round 1 (N-R1-05): fit on PEOPLE from
   `BattleResult.Personnel` — `FormationResult` (and so `ParityOutcome.Of`, which sums `Start` and `PersonnelLoss`) counts
   hulls / airframes for vessel / aircraft formations, so a battery built on `ParityOutcome` would mix hulls with people.
   Merge note for lane P (owner of `ParityBattery.cs`, not edited here): `ParityOutcome.Of` should refuse, or switch to
   personnel for, results with domain formations before any naval fit.
7. **Order kinds.** Six new kinds (Embark, Disembark, Board, Bombard, Patrol, Escort), each charged to the action budget as one
   of the six existing classes (DELEGATED: so the Director's two-action maximum and the anti-loop rules cover every domain
   without new budget rules). Lane T3's two-action windows should adopt `ActionBudget.ClassOf` (merge note).
8. **Sniper identity and values.** The sniper is a prototype test identity (key 4990) with placeholder camouflage 1/8 and one
   shot per 5 steps; C36 says the identity is created only in an authorized packet. Proposal: create it in M7.6 with values
   calibrated against the land battery.
9. **Battle personnel ledger → campaign Ledger.** Killed / LostWithVessel / Captured / Escaped balances are the per-formation
   deltas a campaign `Ledger.Transfer` would apply (O-35, ADR-055). Proposal: keep LostWithVessel as its own reason in the
   campaign record (C33 battle record "casualties per side and category").
10. **DELEGATED rules reported for review:** a based aircraft with sorties (and an airframe fit to fly — N-R2-01) keeps its
    side in the battle but does not hold the field; embarking/landing is not primary activity (termination); a grappled routing hull is taken (land surrender-in-contact
    rule); aircraft observe by looking down (clarity = the target cell's canopy only); one acquisition draw per side shared
    across domains; area-fire danger-close refusal; limited-ammunition weapons only at designated targets.

## 11. Verification round 1 (fix agent, 2026-11-05)

Clone `/dev/shm/m7c/N`, branch `lane/N`, starting commit `623c6ec`. Run **throttled** on the user's instruction ("Throttle by
90%"): `nice -n 19`, single-node builds (`-m:1`), mutants one at a time, no bench re-run. Every finding was first reproduced
against the tree (base `623c6ec`, Release); only reproduced findings were changed. Mutant runs: separate scratch copies
(`/dev/shm/m7c/N-fix-mut`, restored from a pristine copy after every run and byte-compared), full Release suite each,
bound = 10 × the clean build+suite time (base 17 s → 170 s; fixed tree 15 s → 150 s); no run hit its bound (no hang).

### 11.1 Per-finding verdicts

| ID | Sev. | Verdict | Reproduction evidence (measured unless marked) | Change | Semantic test(s) (kill evidence on the fixed tree) |
|---|---|---|---|---|---|
| N-R1-01 | major | **REPRODUCED** | New test `HullLoadingTimeIsSharedByFormationsLanding` on the base code: one formation of 20 landed 2,4,6,…; the same men as two formations of 10 landed 4,8,12,16,20 (2× the hull's rate) | `TransportOps`: ONE time budget per hull and step (`CreditHull` credits `dt` once; every person boarded or landed, any formation or operation, spends from it; idle carry cleared at the end of the step unless time stopped the hull). Single-formation, single-operation behaviour unchanged (argued case by case — inferred; consistent with the measurements: the tree with only this change reproduces the old amphibious hashes, and every pre-existing transport test passes) | `HullLoadingTimeIsSharedByFormationsLanding` (metamorphic split vs unsplit + exact series), `…FormationsEmbarking` (≤ ⌊dt/E⌋ per step for two formations, with a non-vacuity check that the second formation waited in range), `…LandingAndEmbarking` (Σ boarded·E + landed·D ≤ steps·dt, E = 1.5 s, D = 1 s, with a check that both shared a step). Mutant F01L (landing path credits every call): KILLED by `…FormationsLanding`, `…LandingAndEmbarking`. F01E (embark path credits every call): KILLED by `…FormationsEmbarking` |
| N-R1-02 | major | **REPRODUCED** | Base: V1 (troops never share casualties) SURVIVED 430/0 (16 s); V2 (landing exposure deleted) SURVIVED 430/0 (19 s) | Tests only | `HitsOnATransportKillEmbarkedTroopsInProportionWhileItIsAfloat` (exact split per hit, composed in execution order, crew/embarked/both Killed balances, ≥ 1 instant with troops killed afloat): V1 KILLED by it alone. `LandedTroopsAreExposedForTheLandingWindow` (camouflaged troops, 1/8, are acquired at the first detection after landing; exposure ends exactly 20 s after each landing — new inspection accessor `RevealedUntilMs`): V2 KILLED by it alone, by the detection assertion ("landed soldier 2000 (at 0) not acquired by 2000"), not only by the accessor |
| N-R1-03 | major | **REPRODUCED** | Base: V3 (stopped-hull check dropped) SURVIVED (17 s); V5 (repair-pool bound dropped) SURVIVED (17 s); V6 (Bernoulli remainder deleted) SURVIVED (18 s); V4 (air clarity = 1) KILLED only by the amphibious golden (15 s); grep: no Rally / repair test | Tests only | `EmbarkNeedsAStoppedHull` (a hull under way passes within embark range; nobody boards while speed > 0.5 m/s; non-vacuity asserted): V3 KILLED. `RallyRepairsAtTheRateFromABoundedPool` (≤ RepairPerS·dt per step, total = pool, end hull = Hull − damage + pool with damage > pool): V5 KILLED. `BoardingCasualtiesAreTheIntegerPartPlusABernoulliOnTheRemainder` (every fight checked exactly against the keyed draw; E = 0.4 per side and step): V6 KILLED. `LookDownClarityIsTheCanopyOfTheTargetsOwnCell` (full canopy hides the target from the recon flight, open ground does not): V4 KILLED by it (and the golden) |
| N-R1-04 | minor | **REPRODUCED** (Disembark) | Base: V7 (landing starts one step late) KILLED only by the amphibious golden (16 s); no Disembark pin (grep) | Test only | `DisembarkTakesEffectAtTheDeliveredStep` k = 2, 3, 4 (0 ashore one step before, ⌊dt/D⌋ at the delivered step, 2× one step after): V7 KILLED by all three cases (+ two N-R1-01 tests, + golden). Bombard / Board / Escort: no own pin added (limitation, §9) |
| N-R1-05 | minor | **REPRODUCED** | New test on the base code: a transport that loses crew to gunfire and stays afloat reports `Start = Survivors = 1`, `Killed = PersonnelLoss = 0` while the personnel report shows crew Killed > 0; `ParityOutcome.Of` sums those fields (code, `ParityBattery.cs:15-18`) | Documentation of the counted unit per domain on `FormationResult` / `PersonnelLoss` (doc comments only; no behaviour change); report §9, §10 #6 (merge note for lane P: `ParityBattery.cs` not edited) | `VesselFormationResultsCountHullsAndThePersonnelReportCountsPeople` pins the documented unit. No mutant (documentation fix) |
| N-R1-06 | minor | **REPRODUCED** | New test `AnAircraftGroundedByDamageIsNotRelaunched` on the base code: after the damage abort and landing, the next Patrol order relaunched the fighter (`Returning` observed where `Based` was expected) | Launch gate: an airframe takes off only if above the damage-abort threshold (`AirFit`, shared with `AirCanContinue`); with none fit the order is refused `NotReady`. Moves the amphibious golden (attribution §8) | `AnAircraftGroundedByDamageIsNotRelaunched` (stays Based, no sortie spent, no take-off/landing generation change, later orders refused NotReady). Mutant F06 (gate removed from the ready count): KILLED by it (+ golden) |
| N-R1-07 | minor | **REPRODUCED** | Text: line 14 ("re-measured … unless marked") vs the unmarked §7 land-only table, and the lane's own not_done | Report: §7 table marked "measured BEFORE the pause", provenance line corrected. The bench was **not** re-run (throttle) | — (report) |
| N-R1-08 | nit | **REPRODUCED** | `DomainEndStart` handles only routing entities; airborne aircraft stay Active (code) | Doc comment corrected | — (comment) |
| N-R1-09 | nit | **REPRODUCED** (construct) | `DomainBench.cs` interpolated `{x:F2}` etc. without InvariantCulture. Runtime effect already neutralised by `InvariantGlobalization=true` in `Directory.Build.props` (read, not measured on another culture). The unlogged run-1 bound is a past omission that cannot be recovered; it stays disclosed in §6 | Every report line of `DomainBench` formatted through `FormattableString.Invariant` | — (reporting code; `domain golden` output unchanged in content — measured) |

### 11.2 Moved pins (attribution measured)

Amphibious golden only (trace, state, personnel) — cause N-R1-06; see §8. Sail golden, FixedTrig table and Slice D goldens
unchanged (the full suites pass with them).

### 11.3 Tests (measured, `M7Proto.slnx`; the only solution whose projects were touched)

| Build | Before (`623c6ec`) | After (round 1) |
|---|---|---|
| Release | 430 pass / 0 fail / 0 skip | **444 / 0 / 0** |
| Debug | 430 / 0 / 0 | **444 / 0 / 0** |

`M7Econ.slnx` and `M7DetMath.slnx` reference neither `Proto.M7` nor its tests (checked in the `.slnx` / `.csproj` files) and
were not re-run. 14 new test cases (444 − 430): Transport 8 = `HullLoadingTimeIsSharedBy{FormationsLanding,
FormationsEmbarking, LandingAndEmbarking}`, `EmbarkNeedsAStoppedHull`, `LandedTroopsAreExposedForTheLandingWindow`,
`DisembarkTakesEffectAtTheDeliveredStep` ×3; Naval 4; Air 2. Release suite 13 s, Debug 32 s test time. Determinism scan (`scripts/check-proto-determinism.sh`):
CLEAN, 57 files.

### 11.4 Mutants (round 1)

| Mutant | Tree | Result | Killing tests (goldens in brackets) |
|---|---|---|---|
| V1 troops never share hull casualties | base | SURVIVED 430/0 | — |
| V1 | fixed | KILLED 20 s | `HitsOnATransportKillEmbarkedTroopsInProportionWhileItIsAfloat` |
| V2 landing exposure deleted | base | SURVIVED | — |
| V2 | fixed | KILLED 17 s | `LandedTroopsAreExposedForTheLandingWindow` |
| V3 embark at any speed | base | SURVIVED | — |
| V3 | fixed | KILLED 16 s | `EmbarkNeedsAStoppedHull` |
| V4 air clarity = 1 | base | golden-only | [amphibious] |
| V4 | fixed | KILLED 17 s | `LookDownClarityIsTheCanopyOfTheTargetsOwnCell` [amphibious] |
| V5 repair pool unbounded | base | SURVIVED | — |
| V5 | fixed | KILLED 19 s | `RallyRepairsAtTheRateFromABoundedPool` |
| V6 no Bernoulli remainder | base | SURVIVED | — |
| V6 | fixed | KILLED 18 s | `BoardingCasualtiesAreTheIntegerPartPlusABernoulliOnTheRemainder` |
| V7 landing one step late | base | golden-only | [amphibious] |
| V7 | fixed | KILLED 16 s | `DisembarkTakesEffectAtTheDeliveredStep` ×3, `HullLoadingTimeIsSharedByFormationsLanding`, `…LandingAndEmbarking` [amphibious] |
| F01L landing credits a hull per formation | fixed | KILLED 16 s | `HullLoadingTimeIsSharedByFormationsLanding`, `…LandingAndEmbarking` |
| F01E embarking credits a hull per formation | fixed | KILLED 19 s | `HullLoadingTimeIsSharedByFormationsEmbarking` |
| F06 damaged airframe counted as ready | fixed | KILLED 19 s | `AnAircraftGroundedByDamageIsNotRelaunched` [amphibious] |

Teeth and aim (ADR-015 §7.2): each killing test asserts a property the brief or the report claims — a hull's time is one
budget (no split-force exploit), troops aboard are vulnerable, landed troops are exposed, embarking needs a stopped hull,
repair is bounded, boarding casualties have exact expectation, aircraft look down through the target's own cover, Disembark
takes effect at the delivered step, a damaged airframe cannot be cycled to spend sorties. Not run in round 1: a mutant that
removes only the end-of-step idle-carry reset (banking of idle time) — its kill status is unknown.


## 12. Verification round 2 (fix agent, 2026-11-05)

Clone `/dev/shm/m7c/N`, branch `lane/N`, starting commit `f74acdb`. Run **throttled** on the user's instruction ("Throttle by
90%"): `nice -n 15/19`, single-node builds (`-m:1`), one full-suite run per mutant, a small interleaved bench. The
verifier's probe file (`VerifyR2Probes`) is not in the clone, so every finding was reproduced with my own tests or mutant
runs; for N-R2-01 my test gave exactly the verifier's numbers on the unmodified `f74acdb` code. Mutant bound: 150 s
(10 × the ~15 s clean build-free suite); no run reached it (no hang).

### 12.1 Per-finding verdicts

| ID | Sev. | Verdict | Reproduction evidence (measured unless marked) | Change | Semantic test(s) / kill evidence (round-2 tree) |
|---|---|---|---|---|---|
| N-R2-01 | major | **REPRODUCED** | New test on unmodified `f74acdb` (Release): one fighter vs one AA gun, hit to 14/30 and landed. Sorties = 1: `SideEliminated side=0 holds=1 pulses=1`; Sorties = 3 (2 left, can never launch): `BothDisengaged side=-1 holds=-1 pulses=6` | `DomainActiveCounts`: a Based aircraft counts for elimination only with a sortie left **and** `AirFit` (no repair in battle). Doc comment, §3 and §10 #10 amended | `ASideLeftOnlyWithADamageGroundedAircraftIsEliminated` (both runs SideEliminated, side 0, holds 1, same pulses and end time). Mutant M01 (fix removed): KILLED by it alone |
| N-R2-02 | minor | **REPRODUCED** (coverage) | Mutant M2 (`&& AirFit(d)` removed from the take-off loop) on the round-2 tree: every one of the 444 pre-existing tests passed; only the new test below failed (round-2 code differs from `f74acdb` only in the N-R2-01/03 lines, which M2 does not touch) | Test only | `TheTakeOffLoopKeepsADamagedAirframeOnTheGroundWhenItsWingmanLaunches` (two attack helicopters, gun fires once; the pulse-4 Patrol is ACCEPTED, the fit wingman takes off, the damaged airframe stays Based with unchanged sorties and generation). M2: KILLED by it alone |
| N-R2-03 | minor | **REPRODUCED** | New test on `f74acdb`: after the damaged fighter lands, the view shows `Ready = 1` every pulse while each launch order is refused NotReady (`Expected 0, Actual 1`). The DomainAi part, inferred by the verifier, is now **measured**: in the amphibious golden (seed 42) the defence AI issued `p14 s1 F24 Attack`, refused NotReady | `OwnAirFormationView.Ready` applies the validator's fitness check (`AirFit`). Moves the amphibious golden (§12.2) | `TheSideViewDoesNotCountADamageGroundedAirframeAsReady` (Based 1, Ready 0, SortiesLeft 2, order refused NotReady, every pulse after landing). M03 (fix removed): KILLED by it + [amphibious golden] |
| N-R2-04 | minor | **REPRODUCED** | Mutant M1b (end-of-step idle-carry reset disabled, `&& vi < 0`) on the round-2 tree: all 444 pre-existing tests passed; only the new test failed | Test only | `AHullDoesNotBankIdleLoadingTime` (1 man lands in pulse 0 using 1 000 of the 2 000 ms step; 10 men ordered a pulse later land exactly 2,2,2,2,2 — a banked remainder gives 3 at the first step). M1b: KILLED by it alone |
| N-R2-05 | minor | **REPRODUCED** | grep of `Proto.M7.Tests/MultiDomain`: no delivery pin for Bombard, Board, Escort, vessel Patrol, air Attack, air Escort | Tests only | `VesselOrderKindsTakeEffectAtTheDeliveredStep` (Patrol, Board, Escort × k = 3,4,5: directive and heading/speed unchanged at tk − step; directive = kind and hull turning / under way at tk); `BombardFiresFirstAtTheDeliveredStep` (k = 3,4,5: first area round exactly at tk; TEST INPUT 100 m sight so the warship makes no aimed fire before the order; every round is area fire); `AirOrderKindsTakeEffectAtTheDeliveredStep` (Attack, Escort × k = 5,6,7: Based and old directive at tk − step, Airborne and new directive at tk). No delivery-shift mutant was run against these pins (throttle; limitation) |
| N-R2-06 | minor | **REPRODUCED** | `git diff 623c6ec f74acdb` adds per-step work in `TransportOps`; §7/§11 said "no bench re-run" | Report: interleaved A/B of `623c6ec` vs the delivered tree (§12.4); §7 pointer | — (measurement) |
| N-R2-07 | nit | **REPRODUCED** | Mutant M5 (cooldown 0) on the round-2 tree: all 444 pre-existing tests passed. Probes: against ONE hull the cooldown never binds — ram times identical for 30 s and 2 s cooldowns over 15 pulses (`[24000]` both; with TEST INPUT acceleration 50 m/s² `[20000]` both; against a hull fleeing at 1 m/s over 6 pulses `[26000]` both); the rammer is held in contact (distance 16 m = 2 radii) at speed 0 from 28 s on. The verifier's "maybe redundant" hypothesis is refuted for a line of hulls: three TEST INPUT hulls of 50 hull points, 40 m apart — 2 s cooldown rams at 24 s and 34 s; 30 s cooldown rams only at 24 s in 6 pulses | Test only; §3 note | `TheRamCooldownSpacesAHullsRams` (premise: quick second ram with a 2 s cooldown; with the real 30 s cooldown identical first impact, no two rams closer than 30 s, the quick second ram absent). M5: KILLED by it alone |
| N-R2-08 | nit | **REPRODUCED** | Code + the verifier's M4 record: no sniper test makes the sniper fire under camouflage. On the round-2 tree M4 is killed by the transport test and the new test | Test only | `ASniperIsExposedForTheStepAfterItFires` (TEST INPUT camouflage 2^-20; heavy infantry 160 m away inside its 240 m sight: the silent sniper is never acquired in 3 pulses, the same sniper firing is). M4: KILLED by it and by `LandedTroopsAreExposedForTheLandingWindow` |

Teeth and aim (ADR-015 §7.2): each killing test asserts a property the brief or this report states — a side whose aircraft
can no longer fly is eliminated whatever its unused sorties; a damaged airframe is never cycled to spend sorties; the side
view tells a controller exactly what the validator will accept; a hull's idle time is not banked; each order kind applies at
its delivered step; rams of one hull are spaced by the cooldown; firing lifts a sniper's camouflage.

### 12.2 Moved pins (attribution measured)

Amphibious golden: trace `3C5DA2E7…7944` → `5B72E757…9AAF`, state `9A439187…6B48` → `6E3963D9…44A0`, personnel
`E28B1BE9…C963` unchanged; replay equality still holds. Cause: **N-R2-03 only**. The tree with only the N-R2-01 change
passes the old golden; the tree with only the N-R2-03 change produces the new hashes; the `domain watch 3 42` order logs of
the two trees differ by exactly one line, `order p14 s1 F24 Attack tgt=13 inv=NotReady` (present before, absent after). No
other golden moved.

### 12.3 Tests (measured, `M7Proto.slnx`, the only solution whose projects were touched)

| Build | Before (`f74acdb`) | After (round 2) |
|---|---|---|
| Release | 444 / 0 / 0 (round-1 and verifier figure; not re-run as a whole by me) | **468 / 0 / 0** (12 s) |
| Debug | 444 / 0 / 0 (round-1 figure) | **468 / 0 / 0** (29 s) |

24 new cases, all in the new file `Proto.M7.Tests/MultiDomain/VerificationRound2Tests.cs`: N-R2-01 1, -02 1, -03 1, -04 1,
-05 9 + 3 + 6, -07 1, -08 1. Build 0 warnings / 0 errors (Release and Debug). Determinism scan
(`scripts/check-proto-determinism.sh`): CLEAN, 57 files. `M7Econ.slnx` / `M7DetMath.slnx` untouched, not re-run.

### 12.4 Performance, re-measured (N-R2-06)

Interleaved A/B, `623c6ec` (before round 1) vs the delivered tree, Release, `nice -n 19`, load average ~1.5 on the shared
4-CPU machine; Stopwatch, reporting only. Mean pulses and events were identical before and after in every case.

| battle | before total ms (runs) | after total ms (runs) |
|---|---|---|
| galleys 5v5, 3 seeds | 361 / 330 / 317 / 424 / 338 (median 338) | 299 / 388 / 362 / 342 / 373 (median 362) |
| sail line 5v5, 3 seeds | 383 / 378 | 284 / 343 |
| turret 5v5, 3 seeds | 34 / 34 | 33 / 29 |
| amphibious combined arms, 3 seeds | 473 / 471 / 507 / 505 / 539 (median 505) | 527 / 591 / 621 / 599 / 574 (median 591) |
| land 50v50 heavy infantry, 5 seeds | 579 / 456 | 562 / 664 |
| land 60v60 archers | 363 / 308 | 372 / 374 |
| land 50v60 | 285 / 262 | 286 / 282 |
| land 110v110 combined arms | 1361 / 1227 | 1382 / 1292 |

The amphibious battle was slower after in 5 of 5 pairs (median +17 %, about 2 ms per pulse); this is **not attributed**.
The code added per step since `623c6ec` (two `Array.Clear` calls on per-hull flags, one reset loop over the domain
entities, `AirFit` in three predicates) looks too small to explain it (inferred, not profiled). The land-only and other
naval cases show no consistent direction at this noise level.

### 12.5 Mutants (round 2; round-2 tree, full Release suite each, bound 150 s)

| Mutant | Result | Killing tests (goldens in brackets) |
|---|---|---|
| M01 elimination ignores damage-grounding | KILLED 467/1, 12 s | `ASideLeftOnlyWithADamageGroundedAircraftIsEliminated` |
| M03 view Ready ignores fitness | KILLED 466/2, 15 s | `TheSideViewDoesNotCountADamageGroundedAirframeAsReady` [amphibious] |
| M2 take-off loop ignores fitness | KILLED 467/1, 13 s | `TheTakeOffLoopKeepsADamagedAirframeOnTheGroundWhenItsWingmanLaunches` |
| M1b idle hull time banked | KILLED 467/1, 13 s | `AHullDoesNotBankIdleLoadingTime` |
| M5 no ram cooldown | KILLED 467/1, 16 s | `TheRamCooldownSpacesAHullsRams` |
| M4 sniper camouflage kept while firing | KILLED 466/2, 14 s | `ASniperIsExposedForTheStepAfterItFires`, `LandedTroopsAreExposedForTheLandingWindow` |

Script and raw output: scratchpad `m7c/N-r2/mutants.sh`, `mutants.out`, `bench.out` (not committed).

### 12.6 Round-2 limitations

- The new delivery pins (N-R2-05) were not mutated (no delivery-shift mutant); their teeth are argued from the ±1
  neighbour assertions, not measured.
- The amphibious bench delta (§12.4) is measured but not attributed.
- `f74acdb` was not re-run as a whole by me; its 444/0/0 count is the round-1 / verifier figure. The mutants' "444
  pre-existing tests passed" statements were measured on the round-2 tree.
- The ram cooldown is shown to matter only when the rammed hull sinks next to another target; against a single hull it
  never binds (measured), because the rammer stays in contact at speed 0.
