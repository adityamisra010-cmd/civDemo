# Caps and counted objects — PROPOSAL (M7 lane D, 2026-11-09)

**Status: PROPOSAL.** Nothing in this document is a Director decision unless it is quoted from one. It changes no data:
`Sim.Data/content/unit-families.json` and every cap value stay as they are (D-079 §4 D10: "Nothing changes in data now").
The prototype code it describes lives in `prototypes/m7-sprint1/Proto.M7/Caps/` and reads nothing from `Sim.Data`.

**Authority.** D-079 DR24 (the caps, DIR5 §2.10), DR25 (economic scale; "Resolve the documented CAP-1 to CAP-5 conflicts in
D-076 §5 without inventing new Director decisions"), §4 D10 (mortar counted object and CAP-1..CAP-5, DELEGATED, with its
recorded basis), §1.5 READINGS R6 (vessel and drone families under existing caps) and R7 (vehicles battle-transient), DR30
(sandbox), DR33 (snipers). D-076 §5 (the CAP-1..CAP-5 conflict rows). Charter §4.5 (categories A–E; D-065 R8: a unit family
is a controllable formation only).

Labels used below: **DD** Director decision (quoted); **READING** an interpretation with both alternatives; **PROPOSAL**
lane D's design; **MEASURED** run in this lane; **INFERRED** argued, not measured.

**[Lane CB, 2026-11-11, (D-082 DA5).]** [D-082](../d082-morale-caps-ties-director-answers-2026-11-10.md) DA5 decided READING C1
(§2.2): "A soldier cap counts battlefield units, not people" ("Battlefield units."; DA5-R (a) applied: "The cap bounds how many
entities one formation fights as; a formation's size in persons is limited by the economy, not by the cap"), and DA4 confirmed
READING S1 (a) (§5). Every statement below that counts the PERSONS of a soldier class is superseded; each is marked in place with
"(D-082 DA5)" and the text it replaces. The one rule for lanes W and D, what one battle entity standing for many persons is, and
the consequences (CAP-1 and CAP-2 dissolve) are in [battle-entity-proposal.md](battle-entity-proposal.md). Machine classes,
the mortar's crewed tube and lane N's CAP-5 treatment are unchanged.

---

## 1. What is fixed and what is open

| Item | Status | Source |
|---|---|---|
| Heavy/aggregated infantry 50; archers/light infantry 60; tanks 5; helicopters 3; ships 5; fixed-wing aircraft 1; snipers 2; mortars 5 | **DD** (values) | DR24: "Recover and preserve the existing numerical caps" |
| "The counted object for mortar caps still needs a technical definition" | DELEGATED (D10) | DR24, D10 |
| CAP-1..CAP-5 resolution | DELEGATED (D10), "without inventing new Director decisions" | DR25, D10 |
| Recorded basis: "the caps prevail over shipped personnel bands, which are provisional TUNE values" | ADMIN record of the audit | D10, citing `docs/adr/adr-055-m7-recruitment-personnel-reequip.md:74` ("Quantities and personnel are provisional bands (TUNE; charter §15.6 calibration not done)") and `CLAUDE.md:30` ("Tuning data files and `TUNE` parameters is always allowed") |
| "Ships 5" applies to every vessel family; drones under their airframe-class cap | **READING R6** (applied; flagged for Director confirmation in D-079 §5) | D-079 §1.5 |
| Vehicles are battle-transient (O-11 V1); no conserved vehicle stock | **READING R7** (applied) | D-079 §1.5 |
| Where the work lands | M7.6 (B7-07/B7-08 mapping per A-34) | D10 |
| **(D-082 DA5)** A soldier cap counts battlefield units (battle entities), not people; DR24's caps apply in the sandbox (DA4) | **DD** | D-082 DA4, DA5 ("caps in sandbox apply"; "Battlefield units.") |

---

## 2. The counted object of every cap class (PROPOSAL)

**One rule for every class.** People are the conserved stock (Law 1). A machine — vehicle, hull, airframe, crewed tube — is a
battle-transient entity derived from the people who crew it (READING R7, V1). So a cap is checked on what the formation's
people field:

- **soldier-counted classes:** ~~counted = persons;~~ **(D-082 DA5) counted = the battle entities the formation fights as,
  min(persons, cap)**, so a soldier formation is never refused for size (`DirectorCaps.EntitiesOf`; battle-entity proposal §1).
  Replaces "counted = persons";
- **machine-counted classes:** counted = ⌈persons ÷ persons per machine⌉. A partly crewed machine is still a machine, so 21
  tank crewmen at 4 per tank are 6 tanks and exceed the cap of 5. "Persons per machine" is the crew-per-machine TUNE rule
  D10 assigns to M7.6; **no value is shipped**, and the prototype refuses a machine-counted check without one
  (`CapRefusal.NoCrewRule`) rather than guessing.

| DR24 class | Cap | Counted object (PROPOSAL) | Counted from people | Prototype (`Proto.M7/Caps/DirectorCaps.cs`) |
|---|---:|---|---|---|
| Heavy/aggregated infantry | 50 | ~~one soldier~~ one battle entity (D-082 DA5) | ~~persons~~ min(persons, 50) (D-082 DA5) | `CountedObject.Soldier` |
| Archers and lighter infantry | 60 | ~~one soldier~~ one battle entity (D-082 DA5) | ~~persons~~ min(persons, 60) (D-082 DA5) | `CountedObject.Soldier` |
| Snipers | 2 | ~~one soldier~~ one battle entity (D-082 DA5) | ~~persons~~ min(persons, 2) (D-082 DA5) | `CountedObject.Soldier` |
| Tanks | 5 | one armoured vehicle with its crew | ⌈persons ÷ crew⌉ | `CountedObject.Vehicle` |
| Helicopters | 3 | one helicopter with its crew | ⌈persons ÷ crew⌉ | `CountedObject.Helicopter` |
| Ships | 5 | one vessel with its company (every vessel family, R6) | ⌈persons ÷ company⌉ | `CountedObject.Hull` |
| Fixed-wing aircraft | 1 | one aircraft with its crew | ⌈persons ÷ crew⌉ | `CountedObject.FixedWingAirframe` |
| Mortars | 5 | **one tube with the crew that serves it** (§2.1, RECOMMENDED) | ⌈persons ÷ crew per tube⌉ | `CountedObject.CrewedTube` |
| No cap (CAP-5, R6 residue) | — | none (economy-governed) | — | `CountedObject.Uncounted`; lane N's `CapFamily.Uncapped` reused |

This matches what the battle layer already does: lane N's vessel and aircraft formations count HULLS and AIRFRAMES as the
battle entity (`FormationSpec.Count`) and keep their people in the battle's personnel ledger (`reports/H-N.md`, verification
round 1, N-R1-05; `Proto.M7/Tactical/BattleResult.cs` `FormationResult` summary). The DR24 values in the prototype equal lane
N's battle-entry table value for value, pinned by `Dr24Values_EqualLaneNsBattleEntryTable_AndCiteTheDirectorDecision`.
*Superseded in part (verification round 1, D-R1-03): "matches what the battle layer already does" holds for the machine
classes only. For the soldier classes, lane W's collapse (the campaign stand-in of the battle layer) counts battle ENTITIES,
not persons, and lane D's own sandbox inherits that rule in its Campaign Encounter mode. The conflict and both alternatives
are READING C1 (§2.2).*

### 2.1 The mortar counted object (D10)

The only primary text is the Director revision's table: "Mortars | 5 weapon systems/crews, exact counted object to be
defined" (`docs/m7-spec.md:402`), and DR24's "The counted object for mortar caps still needs a technical definition".

| Alternative | What one counted object is | Data | Battle entities | AutoResolver | Economy |
|---|---|---|---|---|---|
| **(a) Crewed tube — RECOMMENDED** | one mortar tube with the crew that serves it; a formation holds up to 5 tubes | a new mortar identity carries `personnel` = tubes × crew per tube (≤ 5 × crew) and a crew-per-tube TUNE value (M7.6); recruitment inputs include the tubes | one entity per tube (a crew-served weapon, like lane N's `WeaponSpec` guns); its people are a crew in the battle's personnel ledger; a tube whose crew falls below its minimum stops firing; a destroyed tube's survivors stay people | needs a crew-served indirect-fire model: today the AutoResolver refuses domain weapons (`AutoResolver.cs`, NotSupported) and lane P's `ResolverPolicy` sends such a battle to the TacticalResolver | recruitment cost per tube (materials) + crew rations and pay per person + ammunition per tube per combat day (lane E's `MunitionsPerGunCombatDay` shape) |
| (b) Crew members | one person of a mortar crew; a formation holds up to 5 people | `personnel` ≤ 5 (bands 3 or 5) | one entity per person (like snipers); the tube is not an entity, or there is at most one tube | modelled as a small ranged formation; no weapon-system state | cheap formations; a battery needs many formations (more orders, more panel rows) |
| (c) Teams | one crew unit, whether or not it still has a tube | `personnel` = teams × team size | one entity per team, tubes as equipment of the team | as (a), but a team without a tube still counts against the cap | as (a); tube losses do not free cap room |

**Recommendation (PROPOSAL):** (a). Reasons: the source's own words are "weapon systems/crews", i.e. the system served by its
crew; (a) is the same rule as the other machine classes (machines counted, people conserved, R7), so one code path serves
all of them (implemented); a formation of up to 5 tubes is the size of a historical mortar section or platoon (a general
order of magnitude, unverified here), whereas (b) caps the formation at 5 people — fewer than a single crew of a heavy mortar.
(c) differs from (a) only after losses; (a) lets the people who lost a tube leave the cap count instead of holding cap room.

### 2.2 READING C1: what a soldier-class cap counts (added in verification round 1, D-R1-03)

"One rule for every class" above counts PERSONS for the soldier classes. The prototypes contain a second rule, and lane D's
own deliverables used both without saying so:

- **(a) Persons.** APPLIED in the caps prototype (`DirectorCaps.CountOf`, `FormationRoster`) and in the sandbox's own force
  creation (Quick Battle, Scenario Editor and Battle Test split a force at the cap). Basis: the recovered table counts "50
  soldiers", "60 soldiers" and "2 personnel" (`docs/m7-spec.md:395–402`, the table DR24 recovered); D-076 records CAP-1 and
  CAP-2 as personnel against the cap (`d076:299–300`); D10's recorded basis is "retune personnel to the cap". Consequences: a
  heavy-infantry formation holds at most 50 people; 50,000 soldiers need at least 1,000 such formations (834 at the archers'
  60); CAP-1 and CAP-2 are real conflicts; a battle fields one entity per person (lane W's P = n case).
- **(b) Battle entities.** APPLIED by lane W (`BattleCollapse` summary: "A campaign formation of P persons fights as n
  entities (n ≤ the cap …)"; `reports/H-W.md` §5 measures 100 persons per entity), and therefore by the sandbox's Campaign
  Encounter (`SandboxSession.AddEncounterForce`: entities = min(persons, cap)). Consequences: the cap bounds the battle's
  entity count, not the formation's size; CAP-1 and CAP-2 dissolve (60 persons fight as 50 entities), which contradicts the
  conflicts D-076 recorded and D10's basis; formation size is then limited by the economy alone; an entity that stands for
  many persons needs a definition of its hit points, morale weight and fire, and none is designed; no data row needs a retune.
- **MEASURED.** Verifier probe P1, reproduced in this round, now pinned by
  `CampaignEncounter_FieldsLaneWsFormationsByTheEntityRule_ReadingC1b`: `DirectorCaps.Values.Check(1, HeavyInfantry, 3000)`
  refuses (ExceedsCap, counted 3,000, cap 50). The encounter loaded from lane W's campaign world fields the same 3,000 persons
  as ONE formation of 50 entities, and the battle-entry check accepts it. Quick Battle splits 3,000 into 60 formations (on a
  field that holds them; on the 48 × 32 test field, 60 formations do not fit in one call, so since D-R1-14 the force is refused
  whole, `OutOfGrid`). Under (a), lane W's 3,000-person campaign formations could not exist; under (b), the Quick Battle
  split is unnecessary.

The spec says "M7.1/M7.6 must define precisely what is counted for each family" (`docs/m7-spec.md:404`). The choice is
therefore delegated design work for M7.6 (B7-07/B7-08), and it must be made ONCE, for lanes W and D together. ~~Lane D proposes
(a) on the textual basis above. It is flagged for confirmation (§7) because it fixes what DR24's caps mean, and the Director
may correct the reading.~~

**(D-082 DA5) Decided: (b).** The Director answered "Battlefield units." (DA5), replacing lane D's proposal "(a) on the textual
basis above". Lane CB made it ONE rule for lanes W and D: `DirectorCaps.EntitiesOf` (min(persons, cap) for a soldier class) in the
caps prototype, the roster and both of the sandbox's ways of creating a formation (`SandboxSession.Fielded`). MEASURED: the check
accepts 3,000 heavy infantry with 50 counted entities; Quick Battle and the Campaign Encounter field the same formation of 50
entities standing for 3,000 and fight the same battle (`CampaignEncounterAndForceCreation_FieldTheSameFormation_…_DA5`, which
replaces `CampaignEncounter_FieldsLaneWsFormationsByTheEntityRule_ReadingC1b`). The consequences listed under (b) above follow:
CAP-1 and CAP-2 dissolve (§3), formation size is limited by the economy, and what one entity stands for is designed in
[battle-entity-proposal.md](battle-entity-proposal.md) (its hit points and output are a PROPOSAL, not built).

---

## 3. CAP-1 to CAP-5 (D10, along its recorded basis)

Rows cited by line in `Sim.Data/content/unit-families.json` as it stands at `45c3313` (verified in this lane; **not
changed**). "Golden moves" are the pins a later TUNE change would move; they cannot be measured without changing data, so
every scope below is INFERRED and must be measured and attributed in the M7.6 packet that changes the data (DR13 attribution
discipline; ADR-061 §4).

### CAP-1 — heavy/line infantry above the cap

| Row | Line | Personnel | Cap (DR24) |
|---|---:|---:|---:|
| 306 `musketeers` (age 7) | 81 | 60 | 50 |
| 307 `line_infantry` (age 8) | 82 | 60 | 50 |
| 308 `mechanized_infantry` (age 9) | 83 | 60 | 50 |

Options (D10's own two): **(A)** class family 3 (`heavy_infantry`, line 68) as heavy/aggregated infantry in M7.6 and retune
the three rows' `personnel` to the cap as TUNE (the shipped bands are 3/5/10/20/40/60, so the choice is 40 — an existing band
— or 50, a new band value), with attributed golden moves; **(B)** document a lighter-infantry classing of 306–308 (cap 60, no
data change). Consequences: (A) keeps the charter's reading of family 3 as "the main battle infantry" (`unit-families.json:69`;
charter §5.2 row 3) and moves any pin that recruits or re-equips these identities (INFERRED scope: worlds with AI empires that
reach Age 7+, since `aiRecruitment` is on in `sim.json`; the order-less founded golden founds only warbands, ADR-055); (B)
changes nothing but classes line and mechanized infantry as "lighter", which the charter does not support. **PROPOSAL:** (A),
retuning to 40 to stay on the existing band set (50 would add a band value; either is TUNE).
*Superseded (verification round 1, D-R1-15): that recommendation let the provisional band set choose the value. This goes
against D10's recorded basis ("retune personnel to the cap"; the caps prevail over the provisional TUNE bands) and against
this document's own CAP-2 wording. **PROPOSAL now:** (A), with 306–308 retuned to the cap, **50** (a new band value; TUNE).
The alternative within (A) is 40, which keeps the existing band set and leaves each formation 10 people below the cap.*

**(D-082 DA5) CAP-1 dissolves.** Replaces "**PROPOSAL now:** (A), with 306–308 retuned to the cap, **50**": under DA5 a 60-person
formation of 306, 307 or 308 (`unit-families.json` :81–:83) fights as 50 battle entities standing for 1.2 persons each, so no
personnel exceeds anything and **no row needs a retune** (lane D's own consequence of (b), §2.2; D-082 §3). Options (A) and (B)
are moot; nothing is retuned. Battle-entity proposal §5.

### CAP-2 — pike and shot, if spear is heavy

| Row | Line | Personnel | Cap if class "heavy/aggregated" | Cap if class "lighter" |
|---|---:|---:|---:|---:|
| 204 `pike_and_shot` (age 7) | 64 | 60 | 50 | 60 |

Options: **(A)** class family 2 (`spear_anti_cavalry`, line 53: "Close-order infantry that holds a line against mounted
attack", line 54) as heavy/aggregated infantry and retune 204 to the cap (TUNE, golden moves attributed); **(B)** class it
with the lighter infantry (no change). **PROPOSAL:** (A): close-order pike blocks are the archetype of aggregated infantry; the
other spear rows (201 at 20, 202/203 at 40; lines 61–63) are already within 50, so only 204 moves.

**(D-082 DA5) CAP-2 dissolves.** Replaces "retune 204 to the cap" in (A): classed heavy, 204 `pike_and_shot` (:64, personnel
60) fights as 50 entities standing for 1.2 persons; classed lighter, as 60. **No row is retuned** under either class. The class
of family 2 still matters for which cap bounds its entities (50 or 60), a classing question for M7.6 only.

### CAP-3 — machines counted by number, data counts people

| Row | Line | Personnel | Cap (DR24) | Persons per machine that would make the row fit exactly |
|---|---:|---:|---:|---:|
| 605 `tanks` (age 9) | 134 | 60 | 5 tanks | 12 |
| 1102 `helicopters` (age 9) | 291 | 20 | 3 helicopters | 7 (⌈20/3⌉) |
| 906 `destroyers` (age 9) | 180 | 60 | 5 ships | 12 |
| 1101 `military_aircraft` (age 8) | 209 | 20 | 1 aircraft | 20 |

D10: "V1 transient machines (§1.5 R7) and a crew-per-machine TUNE rule in M7.6". Two ways to choose the rule's values:

- **(A) crew-per-machine = the formation's whole headcount per machine** (crew plus maintainers, drivers, ground crew): the
  values in the last column keep every shipped row within its cap with no data change. Consequence: a "destroyer" of 12
  people per hull is far below a real ship's company (INFERRED: of the order of 150–300), so the people the economy feeds and
  the battle loses with a hull are understated by an order of magnitude.
- **(B) realistic crews, personnel retuned to cap × crew** (TUNE, golden moves attributed): tanks 5 × about 4 = 20 (an
  existing band); helicopters 3 × a small crew; aircraft 1 × its crew; ships 5 × a ship's company — **hundreds per formation,
  outside the shipped bands 3–60**, so the band set itself is extended (TUNE). Lane N's vessel classes already carry per-hull
  crews (`VesselClass.Crew`) in the battle model.

**PROPOSAL:** (B) for land vehicles and aircraft (realistic crews fit the existing bands); for vessels the same rule needs the
band extension, which the M7.6 packet should present with its measured economic consequence (lane E's ships' companies,
`reports/H-E.md` §12, already price crews per hull).

*Superseded in part (verification round 1, D-R1-08): option (A)'s sentence "the values in the last column keep every shipped
row within its cap with no data change" is FALSE. The table above lists D-076's four example rows only, and the crew rule is
one value per CAP CLASS (`DirectorCaps.WithCrew`), not per identity. At 20 persons per airframe, the 40-personnel strike
aviation rows 2302 and 2303 count 2 airframes against the fixed-wing cap of 1 (MEASURED: verifier probe P5, reproduced; pinned
by `Cap3_OptionA_PerClassCrewValues_MinimalValuesKeepEveryMachineRowWithinItsCap`). Family 23 was never classed, and option
(B)'s touched rows were not cited. The class membership, the corrected values and the rows each option touches follow.*

**CAP-3 class membership (verification round 1).** Every machine-class row of `unit-families.json` at `45c3313`, with its line
and personnel (DR24 classes; vessels under READING R6; nothing changed):

| DR24 class (cap) | Family (key) | Rows: key `id` (line) personnel |
|---|---|---|
| Tanks (5) | `heavy_cavalry` (6) | 605 `tanks` (:134) 60 |
| Helicopters (3) | `rotary_wing` (20) | 1102 `helicopters` (:291) 20 |
| Ships (5) | `naval_combat` (9) | 901 (:175) 40; 902 (:176) 60; 903 (:177) 60; 904 (:178) 60; 905 (:179) 60; 906 `destroyers` (:180) 60 |
| Ships (5), R6 | `naval_transport` (10) | 1001 (:191) 20; 1002 (:192) 20; 1003 (:193) 40; 1004 (:194) 40; 1005 (:195) 40; 1006 (:196) 40 |
| Ships (5) | `naval_raiders` (17) | 1701 (:250) 40; 1702 (:251) 60; 1703 (:252) 60 |
| Ships (5), R6 | `submarines` (18) | 1801 (:265) 40; 907 (:266) 60 |
| Ships (5), R6 | `carriers` (19) | 908 (:279) 60; 1901 (:280) 60 |
| Fixed-wing aircraft (1) | `air` (11) | 1101 `military_aircraft` (:209) 20; 1105 (:210) 20; 1106 (:211) 20; 1103 (:212) 20 |
| Fixed-wing aircraft (1) | `strike_aviation` (23) | 2301 (:330) 20; 2302 `strategic_bombers` (:331) 40; 2303 `stealth_bombers` (:332) 40 |
| Fixed-wing (1) or helicopters (3), R6, class fixed in M7.6 | `unmanned_aircraft` (21) | 2101 (:304) 10; 1104 (:305) 10 |

Family 23 is classed here as fixed-wing aircraft (bombers are fixed-wing aircraft; PROPOSAL). D-076 §5 lists neither family
23 nor `naval_raiders` among the uncapped families, and DR24's own class names ("fixed-wing aircraft", "ships") cover them.
Not classed as machines (a classing question for M7.6, flagged only): 506 `armoured_reconnaissance` (:119, family 5, which
R6 keeps uncapped as cavalry) and 308 `mechanized_infantry` (:83, CAP-1, counted as soldiers).

- **(A), corrected: one value per class.** A class value keeps every row of the class within the cap only when it is at least
  ⌈largest personnel of the class ÷ cap⌉. The minimal values are **tanks 12, helicopters 7, ships 12, fixed-wing 40**. Drones
  fit either candidate class at these values (10 persons: 1 airframe at 40; 2 helicopters at 7). Each value minus one refuses
  a row. Every row below its class's largest then counts fewer machines than its personnel suggests: a 20-person aircraft
  formation is one airframe "of 40". The understated crews of (A) remain.
- **(A') one value per identity**, ⌈personnel ÷ cap⌉ (for example 1101 20, 2302 40, 1001 4, 906 12). Every row fits by
  construction, so the cap binds only on merges and reinforcements, never on a standard recruited formation. The TUNE rule
  then has one value per identity; the prototype holds one value per class, so (A') needs a per-identity input (not built).
- **(B) realistic crews, personnel retuned to cap × crew.** Potentially every row of the table above is touched: 30 rows (1
  tank, 1 helicopter, 19 vessel, 7 fixed-wing, 2 drone rows), each to its class cap × its crew value. The exact set is known
  only once the M7.6 packet chooses the crews, and each moved golden is attributed there.

The PROPOSAL above is unchanged: (B) for land vehicles and aircraft, and for vessels with the band extension. (A) at the
corrected minimal values is the fallback if M7.6 wants no data change.

### CAP-4 — capped classes with no identity

| Class | Today | Line |
|---|---|---:|
| Snipers (cap 2) | no sniper identity; 407 `sharpshooters` (age 8, 60 personnel) is a line formation of rifled marksmen | 102 (407); line 13 (`_doc`: "no … sniper (R14: open, Q10)") |
| Mortars (cap 5) | no mortar identity anywhere | — |

D10: "a category-A sniper identity capped at 2, distinct from 407; the mortar identity classified A–E per charter §4.5 and
created only in an authorized packet". DR33: "Snipers are independent small specialist formations."

- **Sniper identity (new, proposed, NOT created):** category **A** (§4 below). Home: a branch identity of `ranged_infantry`
  (family 4, line 87) at the age of rifled weapons (the age of 407, 8), personnel ≤ 2, recruited not upgraded (a branch, like
  405 longbowmen). The prototype's `sniper_team(prototype)` (key 4990, lane N) is a test identity only.
  **(D-082 DA5)** Replaces "personnel ≤ 2" as a cap requirement: a sniper formation of more persons fights as 2 battle entities,
  so the cap no longer bounds its personnel. DR33's "independent small specialist formations" then rests on the identity's
  personnel band (data, TUNE in M7.6) and its cost, not on the cap. The proposal of a small band (2) stands as a design choice.
- **Mortar identity (new, proposed, NOT created):** category **A** (§4). Two homes, both PROPOSALS: (i) a branch of `siege`
  (family 8, line 152), whose charter role already includes "later indirect fire" (charter §7.8) and whose 805/806 are its
  indirect-fire identities (lines 163–164); (ii) a branch of `ranged_infantry` (family 4), next to 408 `machine_gun_teams`
  (line 103, crew-served direct fire). (i) is recommended: indirect fire and its counter-battery exposure are the siege
  family's role. Either way the cap class is "Mortars 5" with the crewed-tube counted object (§2.1).

### CAP-5 — families with no cap

D-076 §5: "Cavalry, siege, naval transport, submarines, carriers, air defence, drones, missiles and reconnaissance have no
cap. Any cap for them would be invented." D10 and READING R6 narrow it **by classification only**:

| Family | Line | Treatment |
|---|---:|---|
| `naval_transport` (10), `submarines` (18), `carriers` (19) | 184, 256, 270 | under "Ships 5" (R6) |
| `unmanned_aircraft` (21) | 295 | under its airframe class's cap (R6; the class is fixed in M7.6) |
| `light_cavalry` (5), `heavy_cavalry` (6) except 605 tanks, `ranged_cavalry` (7), `siege` (8), `air_defence` (14), `strategic_missile` (12), `reconnaissance` (22) | 107, 123, 138, 152, 228, 216, 309 | **uncapped** (economy-governed); no cap value is invented |

The prototype reuses lane N's treatment (`reports/H-N.md` §10 item 2: crew-served guns uncapped, `CapFamily.Uncapped`;
transports under "Ships 5"): an uncapped class is never refused for size (`UncappedClass_ReusesLaneNsCap5Treatment_NeverRefusedForSize`).
*Corrected citation (verification round 1, D-R1-17): transports under "Ships 5" are `reports/H-N.md` §10 item 1; crew-served
guns uncapped are item 2.*
**No performance cap is proposed:** D10 allows one "only if measurement shows the need", and this lane measured nothing that
shows it. What such a measurement would have to show: a battle cost (time per pulse, memory) that grows past a stated budget
with the entity count of an uncapped class at sizes the economy allows (economic-scale proposal §5); the budgets themselves
wait on the Victus benchmark (D-079 DR50, Q9).
**(D-082 DA5) Measured since (lane CB):** uncapped families keep one battle entity per person (lane N's treatment, unchanged),
so their battle cost grows with their persons, while every capped class's is bounded by its cap. The TacticalResolver's cost per
pulse grows with the square of the entities (MEASURED, battle-entity proposal §6), and the prototype engine cannot field a
formation of 1,000 or more entities with generated ids (a hard limit, `TacticalBattle`: "generated ids need Count < 1000").
Whether that is "the need" D10 names is the Director's to judge; no cap value is proposed (battle-entity proposal §8 item 3).

---

## 4. A–E classification of every new identity this lane names (charter §4.5)

| Proposed entity | Category | Lifecycle and owner | Controllable or abstract | Authoritative state, persistence | Consumer | Map location / visibility | How the player sees it | Interactions; no counting twice |
|---|---|---|---|---|---|---|---|---|
| Sniper team identity (new; branch of family 4) | **A** | recruited by a polity, persists as a formation until disbanded or destroyed | controllable formation | a `MilitaryUnits` row of the new identity with `FormationPersonnelRow` people (ADR-055); the new identity row needs the content change, a schema-stable load and the populated-table tests ADR-055 already has | movement, orders, battle (personal concealment, long range: lane N profile) | yes: a formation on the map; hard to detect in battle | as an independent small formation (DR33) | never part of another formation's headcount; its 2 people are its own |
| Mortar identity (new; branch of family 8) | **A** | as above | controllable formation | as above, plus a crew-per-tube TUNE value | battle (indirect fire), siege (lane S assault), economy (ammunition) | yes | as a formation of up to 5 tubes | tubes are battle-transient (R7); its people are counted once, in its formation |
| Field medical support (DIR5 example "medics") | **B** (already, D-065 R10; withdrawn key 16) | a polity's pool, allocated to an army per War Pulse | abstract, panel-managed (DR29) | pool and army accounts + `SupportAllocationRow`, `SupportCareRow`, `SupportBattleRow` (prototype) | casualty split, care of the wounded, morale restore | no map position; never a battle entity | in the support panel | its staff are never counted as soldiers of the army (separate accounts) |
| Supply train support (DIR5 example "supply wagons") | **B** (already, D-065 R10; withdrawn key 15) | as above | abstract, panel-managed | pool and army accounts (wagons a conserved `Vehicles` stock in the prototype) + `SupportAllocationRow` | lane E's train capacity, supply line, march speed, fodder | none | in the support panel | its wagons are added to the army's own train for the pulse, never to another army's in the same pulse |
| Combat engineers (labelled PROPOSAL only, not implemented) | **B** (D-065 R10; withdrawn key 13) | — | abstract | — | siege works (lane S), field fortification | none | in the support panel | — |

The prototype identities 4990, 4991 and 4992 (lane N's `sniper_team`, `riflemen`, `gun_crew`) are TEST identities, not
proposals; the sandbox uses them only because no data identity has a battle profile yet.

---

## 5. Caps in the War Sandbox (READING S1)

DR30: "Unrestricted experimental force creation, including units unavailable in the campaign." DR24: the caps. The two can be
read together in two ways:

- **(a) APPLIED in the prototype:** "unrestricted" concerns WHICH units and HOW MANY (any designed family, any era, any number
  of formations); every formation still respects its DR24 cap, and a large experimental force is created as the fewest
  formations within the cap (`FormationRoster.SplitSizes`; 130 heavy infantry → 44 + 43 + 43). Consequence: the sandbox runs
  the same battle engine as the campaign (DR7, DR30 "Campaign resolver rules remain unchanged"), whose entry check refuses an
  over-cap formation (`TacticalBattle`, `AutoBattle`: `setup.Caps.Check`).
- **(b)** "unrestricted" also lifts the caps in the sandbox. Consequence: the battle-entry check needs a sandbox override (lane
  N's `ProposedCaps.WithOverride` exists for tests), performance is unbounded by the caps, and sandbox results are no longer
  comparable to campaign battles of the same forces.

This is a READING, applied until the Director corrects it; both alternatives are in `docs/m7-plan/war-sandbox-proposal.md` §4.

**(D-082 DA4, DA5)** DA4 confirmed (a): "caps in sandbox apply". DA5 changes what the cap counts, so "a large experimental force
is created as the fewest formations within the cap (`FormationRoster.SplitSizes`; 130 heavy infantry → 44 + 43 + 43)" is
replaced: the fewest formations within the cap, counted in battle entities, is ONE formation for every class the sandbox fields
(130 heavy infantry → one formation of 50 entities standing for 130 persons; MEASURED,
`ForceCreation_IsOneFormation_WithinTheCapInBattleEntities_DA5`, which replaces `ForceCreation_SplitsAtTheDirectorCap`).

---

## 6. The prototype (what exists, what it proves)

- `Proto.M7/Caps/DirectorCaps.cs`: the DR24 table as prototype constants (citation "D-079 DR24"), counted objects, the
  persons-per-machine input (`WithCrew`, no default), `Check` (refusal + counted + cap).
  **(D-082 DA5)** `EntitiesOf` (the one rule; `CountOf` = `EntitiesOf`) replaces "counted = persons" for the soldier classes;
  `MaxPersons` of a soldier class is long.MaxValue (no persons cap), `FullStrengthPersons` its cap.
- `Proto.M7/Caps/FormationRoster.cs`: creation and every edit — `Create`, `Split`, `Merge`, `Reinforce`, `Detach`,
  `Transfer`, `Disband` — each checked completely before any person moves; all-or-nothing; people moved only by `WarLedger`
  transfers (exact conservation); one formation is one identity (merge or transfer across classes or identities refused).

| Refusal (`CapRefusal`, stable codes) | When |
|---|---|
| 0 None | accepted |
| 1 NonPositive | a request ≤ 0, or an edit that would leave a formation empty |
| 2 ExceedsCap | counted objects after the edit > the DR24 cap |
| 3 ClassMismatch | merge or transfer across classes or identities |
| 4 UnknownFormation, 5 DuplicateId, 8 SameFormation, 9 NotOwner | as named |
| 6 InsufficientPersons | the source holds fewer people than the edit moves |
| 7 DepotShort | the owner's pool holds fewer people than the edit draws |
| 10 NoCrewRule | a machine-counted class without a persons-per-machine input |

**Ordering.** `ReinforceToCaps` hands recruits one at a time to the formation with the least fill fraction persons ÷ max
persons, compared exactly by cross-multiplication in `Int128`, ties to the lowest id (the CLAUDE.md composite key).
**(D-082 DA5)** Only formations whose cap bounds their persons take part (machine classes with a crew rule); soldier formations
have no persons cap to fill. The tie-dense and stopping-rule tests that used heavy infantry and archers now use tanks, ships,
helicopters and mortars (TEST INPUT crews), with the same properties pinned (lane CB report, moved pins). Tie-dense
tests: eight formations at the same fraction created in six permuted orders give the same distribution and roster hash; the
exact tie 10/50 = 12/60 across classes is broken by id only; an independent brute-force oracle (BigInteger rationals) agrees.
*Verification round 1 of lane CB (CB-R1-01, CB-R1-08), corrections to the note just above: (1) "Only formations whose cap bounds
their persons take part … soldier formations have no persons cap to fill" is READING CB-4 (a), lane CB's choice, not a consequence
of DA5: under alternative (b) a soldier formation is filled up to its full complement of entities at one person each
(`FullStrengthPersons`, its cap), which keeps lane D's automatic reinforcement of understrength infantry and the three original
pins (MEASURED, scratchpad probe; both alternatives in [battle-entity-proposal.md](battle-entity-proposal.md) §1, READING CB-4);
those three pins moved because of (a). (2) "tanks, ships, helicopters and mortars": the re-based tests use tanks, ships and
helicopters; no mortar formation takes part. (3) "the exact tie 10/50 = 12/60 across classes": stale since lane CB; the cross-class
tie is now 4/20 (tanks, TEST INPUT crew 4) = 200/1,000 (ships, crew 200), broken by id only, and the oracle agrees.*
*Verification round 2 (D-R2-03): the two stopping rules, "no capped formation has room" and "the depot is empty", had no test:
every test handed out fewer recruits than the room and the depot held, so filling one person past the cap (verifier mutant C1)
or ignoring the depot (C2) survived the full suite (reproduced), and under each the function returned grants whose internal
`Reinforce` was refused and moved nobody. `ReinforceToCaps_StopsAtEveryCapAndAtTheEmptyDepot_ReturningWhatMoved` now pins both
rules, each binding alone, and that the returned grants are what moved.*

**Tests (MEASURED):** `Proto.M7.Tests/LaneD/CapsRosterTests.cs`, 24 cases, all passing in Debug and Release (counts in
`reports/H-D.md` §6), including a 4,000-edit deterministic mixed sequence that checks after EVERY edit that the people total
equals the opening total exactly and that every formation is within its cap. Mutants M1–M5 (`reports/H-D.md` §7).
*Superseded in part (verification round 1, D-R1-05): the "people total" was the ledger's total over every account, which is
conserved by construction. It could not see people stranded in a removed formation's account (verifier mutant V1, Disband
without the transfer back to the depot, survived the full suite). The sequence now asserts depots + Σ formation persons =
opening after every edit, and `Disband_ReturnsEveryPersonToTheDepot_AndTheRosterAccountsForEveryone` pins Disband directly.
Counts after round 1 are in `reports/H-D.md` §13.*

---

## 7. For the Director

Nothing here needs a new Director decision: the mortar counted object and CAP-1..CAP-5 are delegated (D10) and land as TUNE
in M7.6. Two readings are flagged for confirmation, both applied until corrected:

1. **READING R6** (already flagged in D-079 §5): vessel families under "Ships 5", drones under their airframe class.
2. **READING S1** (this document §5): caps apply per formation in the sandbox.
3. **READING C1** (§2.2; added in verification round 1): a soldier-class cap counts persons (a). This is applied in lane D's
   caps prototype and in the sandbox's own force creation. Lane W's collapse, and with it the sandbox's Campaign Encounter,
   applies (b), battle entities. M7.6 must reconcile the two prototypes whichever reading stands.
   **(D-082 DA5) Answered: (b).** Reconciled by lane CB: one rule for lanes W and D (§2.2). **READING S1** (item 2) was confirmed by
   DA4. The decisions left are in [battle-entity-proposal.md](battle-entity-proposal.md) §8.
