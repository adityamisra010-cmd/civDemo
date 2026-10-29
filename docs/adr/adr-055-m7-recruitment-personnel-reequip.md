# ADR-055 — M7 recruitment slice: taxonomy, formation personnel, recruitment, re-equipment and the family migration

**Status: ACCEPTED — RATIFIED by the Director on 2026-10-28 (D-067 R15), after the documentation amendments recorded in
the amendment log at the end of this ADR. Implemented under D-065 R7 (the Director's direction to implement the family
splits, staged aviation, permitted re-equipment and the founding warband's real personnel on the feature branch); now
integrated in the combined worktree (`wt-m6`, over `0bf7626`), uncommitted and unmerged — no commit, push or merge is
authorized (D-067 R20: the intended order is this M7 pass first, the M6 closure second). The ratification explicitly
covers scope beyond D-065 R7's literal list: the general Recruit (kind 16) and Disband (kind 18) orders, soldier upkeep,
and AI recruitment enabled (`military.aiRecruitment` true).**
Schema **v39**, after the research track's v38 (ADR-054, ResearchPopulations at section 69): the four tables of this ADR are sections **70–73** and the readable set is {32, …, 39}; a v38 or older stream loads with them empty. This ADR is integrated on top of the research track (int-base-r2). ADR number: the research track
takes the first free number (054); this is the second.

Authorities: unit charter `docs/m7-plan/unit-catalogue-and-progression.md` §4.4, §5.1–§5.3, §6.5, §6.6, §7 (incl. §7.18,
§7.22), §13–§16.4; `docs/m7-plan/aviation-progression.md` §3, §4, §5, §9.3; D-065 R1 (re-equipment), R2 (EC-1–EC-3 separate),
R3 (staged aviation), R7 (implementation direction), **R8–R14 (Addendum 3, the Director's 2026-10-27 taxonomy ruling: what is
a controllable formation)**; unit charter §4.5; ADR-031 (unchanged).

## 1. Taxonomy (Q2 resolved, charter §16.4)

`Sim.Data/content/unit-families.json` → **schema 2**, 20 families, 80 identities.

- Appended families 14 `air_defence`, 17 `naval_raiders`, 18 `submarines`, 19 `carriers`, 20 `rotary_wing`, 21
  `unmanned_aircraft`, 22 `reconnaissance` (composition `mobile_agent`: mobile scouts), 23 `strike_aviation`. New identity
  keys follow family × 100 + n; no existing key is reused or renumbered.
- **D-065 Addendum 3 (R8–R14; unit charter §4.5) applied.** A thing is a unit family only when its instances have a
  meaningful independent lifecycle as controllable formations (R8). Combat engineers, military supply and field medical
  support are **category B — abstract military support capabilities** (persisted quantities or allocations the battle and
  war systems will consume; R9–R10): the proposed families **13 `combat_engineers`, 15 `military_supply` and 16
  `field_medical` were removed before landing; keys 13, 15 and 16 stay unallocated and are never reused.** The abstract
  allocations themselves are NOT implemented (future battle-system work). No prospector family (R12: prospecting is the
  one-shot Prospect order, kind 15), no sniper identity (R14: form open, Q10; `sharpshooters` 407 untouched), no
  trade/shipping family (R13). `naval_transport` (family 10) is left exactly as ADR-031 shipped it — no identity added,
  removed or migrated — pending the ADR-031 amendment R13 names. `air_defence` (14) is the mobile formation state only;
  the garrisoned/installed state and garrisoning (R11) are design requirements, not implemented.
- Rehomed (keys unchanged): 101 → 22, 907 → 18 (branch), 908 → 19 (mainline: branch flag cleared), 1102 → 20 (mainline:
  branch flag cleared), 1104 → 21 (branch). The moved identities are removed from `naval_combat`'s and `air`'s `branches`.
- Aviation (aviation-progression §3, §9.3): 1101 `military_aircraft` identity Age A9 → **A8** (Flight I); 1105
  `advanced_piston_aircraft` A9 mainline (`aircraft_metal`); 1106 `jet_fighters` A9 branch (`jet_engine`); 1103 stays a
  branch. `strike_aviation`: 2301 light bombers A8 (`aircraft`), 2302 strategic bombers A9 (`aircraft_metal`), 2303 stealth
  bombers branch (`stealth AND aircraft_metal`). `air_defence`: 1401 AA artillery A8 (`rifled_artillery AND aircraft`), 1402
  SAM branch (`guided_missile AND radar`). `carriers`: 908 mainline + 1901 jet-capable fleet carriers branch
  (`jet_engine AND steam_turbine`, `jetDeck`). `rotary_wing`: 1102. `unmanned_aircraft`: 2101 reconnaissance drones
  mainline (`aerial_reconnaissance AND computer`) + 1104 branch.
- Carrier capability is an identity attribute (`carrierCapable`: no / any / jet_deck; `jetDeck` on carriers), one
  predicate `RecruitmentQuery.CanEmbark` (declarative: no basing system consumes it yet).
- Knowledge gate per identity: `realizedBy` (entity) and/or `requires` (node-id expression in the research dialect, parsed
  and validated against research.json by the loader; ANDed with the entity where both are given) or `baseline` (a
  research.json baseline capability). Schema 2 requires a gate and recruitment data on every identity. **No research.json
  node or entity is added (TC-1 hash untouched).**
- ADR-031 is unchanged: modernization follows the mainline, branches are never targets, preview = applied, unknown
  identities are preserved. NOTE: RC-1 (the research track's `aircraft` retag) is in the integration base this ADR sits on. The charter's
  acceptance line "no aviation identity gated on a node of a later Age" is still NOT enforced by a validator here
  (deferred; a follow-up can add it now that RC-1 has landed).

## 2. Save migration (charter §5.3)

`State.UnitFamilyMigration`: a pure, content-free map (101 1→22, 907 9→18, 908 9→19, 1102 11→20, 1104 11→21) applied by
`CanonicalSchema.Read` to every MilitaryUnits row of a stream **older than v39**; a rehomed identity under any other family
is refused (`InvalidDataException` → Snapshot.Load's corrupt-save diagnostic). `UnitConversions` (history) is never
rewritten. A test pins the map against the content. Switch-off twin: `CanonicalSchema.Read(..., migrateUnitFamilies:
false)` — test-only. **Measured no-op**: both shipped saves (`v32-seed42-turn40.civsnap`, `v33-seed42-turn2.civsnap`) hold
only warbands (301); their migrated and unmigrated reads hash identically.

## 3. Recruitment data and planned goods

Per identity `recruitment`: `personnel` (adults), `years` (recruitment time = re-equipment duration), `inputs` (goods by
name + whole quantity), optional `building` (a research building entity a completed structure at the location must
realize), optional `blockedBy` (a named missing system or ruling). Every good is a goods.json good or is in the explicit
`plannedGoods` map with its owning packet — EC-1 iron-ore, coal, iron, steel; EC-2 horses; EC-3 saltpetre, sulphur,
gunpowder, explosives; EC-4a engines, fuel (M10, provisional); EC-5a aluminium; EC-5b electronics; EC-5c composites; and
**EC-6 uranium** (added for 907 nuclear submarines; the brief's list omitted it). The loader refuses any other name, so a
typo can never silently block. A real good wins over a stale planned entry (an EC packet landing does not break the
load). A1–A3 identities use only existing goods (timber, stone, hides, fiber, bronze, tools, cloth, pottery, grain).
Quantities and personnel are provisional bands (TUNE; charter §15.6 calibration not done).

## 4. Formation personnel (Q7)

- `FormationPersonnelRow(Unit, Count: Conserved, UpkeepRemainder, UnfedUnits)` — schema v39 §70, one row per manned
  formation. **Deviation from "MilitaryUnitRow gains personnel"**: the people live in a companion table so that
  MilitaryUnitRow keeps its v28 layout and every readable older stream stays a byte prefix (the D-056 `BytesAppendedSince`
  invariant every projection and migration relies on). Semantically it is the formation's personnel field.
- **Founding** (`WorldFounding.DrawFoundingPersonnel`): with `sim.json military.enabled` the founding identity's
  `recruitment.personnel` (20) adults move from the capital's adult buckets (proportional, exact integers,
  `RecruitmentQuery.AllocateDraw`) by `Ledger.Transfer`. Switch-off twin (`TestConfigs.PreMilitary`): the token, byte for byte.
- **ConservationAuditor** counts personnel in the population total and escrowed goods in their good's total.
- **Upkeep**: every step each manned formation eats `personnel × military.upkeepGrainPerPersonYear (1.0, DERIVED: D-015
  person-year-equivalent) × dtYears` + the banked remainder, drawn from its settlement first then the owner's purse
  (EmpireStockpile, beyond construction's set-asides), sunk under `FormationUpkeep`; the shortfall accumulates in
  `UnfedUnits` (**no consumer yet** — attrition/desertion is the M7.4 supply slice).
- **Disband** (`DisbandFormation`, kind 18) returns the people to the station's adult buckets by Ledger.Transfer
  (proportional to current adult counts). People in a formation do not age, die or give birth while serving (stated
  simplification).

## 5. RecruitFormation (kind 16)

TargetId = settlement, Amount = identity key. `RecruitmentQuery.CheckRecruit` — the ONE predicate for the system, the
action surface, the UI factory and the AI — implements §6.5: (1) knowledge; blockedBy; planned goods (names the good and
its packet); the building; (7) rule support (a `mobile_agent` family has no movement/basing/water edges in this build);
(4) materials from the empire stockpile beyond construction's set-asides; (5) personnel (at least one adult stays); (6)
support (free grain ≥ military.supportYears × upkeep of every formation of the owner, the new one included); and (2) the
latest realizable mainline in line order (a later mainline whose own predicate holds supersedes; branches alongside).
**The Age is never read** (tested at Ages 1–9). Atomic: judged in full first, then the inputs are sunk (`Recruitment`),
the adults transferred, the row appended (next id = one past every id ever recorded). Delivery: stamped t → in the state
of t+1. Load validation in OrderLog; control of the settlement in OrderValidation (deferral for later settlements).

## 6. ReequipFormation (kind 17) — charter §6.6

TargetId = formation, Amount = target identity key, or **0 = cancel**.

- **Transition policy** (`RecruitmentQuery.TransitionPolicy`, one function): permitted (a) another family, same composition
  (role change), (b) same family → branch, (c) branch → branch; refused: identical target, composition change, and same
  family → mainline with the reason **"OD-2: same-line catch-up is not ruled"**.
- Legality: owner; stationed at a settlement it controls; not already re-equipping; target passes §6.5 items 1, 3, 4, 6, 7
  there (the personnel difference as item 5).
- Acceptance (delivery step, atomic): the target's full inputs move into **escrow** (`ReequipEscrowRow`, v39 §72) by
  Ledger.Transfer — the reservation is physical: no other consumer can spend them (D-055's holds would require every other
  drawer to honour a new hold table; escrow is the conserved equivalent with no change to construction, roads or
  discovery). The personnel difference is drawn/returned by Ledger.Transfer. `ReequipRow` (v39 §71) records
  `RequiredDays = ceil(years × 360)`.
- Progress: from the step after acceptance, `ElapsedDays += dtDays` (Law 3, integer-exact); completion in the first step
  reaching RequiredDays. MilitarySystem runs **after agetransition**, so an Age transition of the same step modernizes the
  source first (logged in UnitConversions), then the re-equipment completes onto the target. Within the system, orders run
  before progress: a cancel delivered in the step a completion would reach wins.
- Completion: escrow sunk (`Reequipment`), family/identity → target, experience kept (b, c) or × `military.
  reequipExperienceTransfer` 0.35 (a; class read after modernization), `ReequipLogRow` cause 1 (v39 §73; never
  UnitConversions).
- Cancel: escrow returned to the station by Ledger.Transfer, labour not refunded, personnel difference not reversed, log
  cause 2. Disbandment releases the escrow too (cause 3; every disbandment is logged so ids are never reissued).
- First valid ReequipFormation per formation per step wins. Labour: the formation's own personnel do the work (it is
  non-operational meanwhile); **deviation**: no draw on the settlement's production labour account (would couple to
  PathBuild/Construction's labour ledger; deferred).

## 7. AI (military.aiRecruitment)

`AiMilitaryPolicy`: one RecruitFormation per AI polity per turn when its NEXT Age has an unmet `formations` milestone and a
legal identity counted by that fact exists; the least composite integer key (identity Age ascending, identity key
ascending, settlement id ascending — `AiMilitaryPolicy.Compare`; no family key); uses only
the polity's own settlements, stocks, people and knowledge; tie-dense tests. Reading the next Age is AI goal-setting (the
AgeAdvancePolicy precedent), never an input of the predicate.

## 8. Measurement (seeds 1, 2, 42, 7) — see the table in the track report

MEASURED with `AiRecruitmentPacingMeasurement` (opt-in): AI (polity 2) Age-entry turns with the policy on vs off.
Results and the ship decision: §8.1 below.

### 8.1 Results

MEASURED on the COMBINED research + units tree (int-base-r2 + this ADR; canonical world, shipped worldgen, one AI Empire =
polity 2, cap 1,630 turns; AI Age-entry turns; "off" = `TestConfigs.PreAiRecruitment`, the military layer otherwise on):

| seed | policy | A2 | A3 | A4 | A5 | A6 | A7 | A8 | A9 | AI recruit orders (turn:identity) | formations at end |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | on | 67 | 341 | 688 | 722 | 791 | 944 | 1072 | 1195 | 241:102 | 2 |
| 1 | off | 67 | 341 | 688 | 722 | 791 | 944 | 1072 | 1195 | — | 1 |
| 2 | on | 67 | 356 | 730 | 768 | 844 | 987 | 1122 | 1251 | 249:102 | 2 |
| 2 | off | 67 | 356 | 730 | 768 | 844 | 987 | 1122 | 1251 | — | 1 |
| 42 | on | 67 | 345 | 698 | 733 | 801 | 931 | 1035 | 1141 | 244:102 | 2 |
| 42 | off | 67 | 344 | 698 | 733 | 801 | 930 | 1035 | 1141 | — | 1 |
| 7 | on | 67 | 344 | 695 | 728 | 796 | 923 | 1042 | 1162 | 242:102 | 2 |
| 7 | off | 67 | 344 | 695 | 728 | 796 | 923 | 1042 | 1162 | — | 1 |

The worlds first differ the turn after the one recruitment (242 / 250 / 245 / 243). Every Age entry is identical to the
control on seeds 1, 2 and 7, and within one turn on seed 42 (A3 and A7 +1). **Decision: `military.aiRecruitment` ships
ON** — pacing is not broken. The AI recruits exactly one formation (102 skirmishers, for the A3 formations milestone) and
none later: every A4+ identity a later milestone would count is blocked by a planned good (EC-1–EC-4a), so from A4 on the
formations milestones are met by free modernization or not at all, as before. (Earlier runs on the bare 0bf7626 tree,
superseded: with families 13/15/16 present the AI also recruited 1602 military hospital corps and moved A7/A8 by up to −11
turns; without them, ±1 turn.)

## 9. Integration surfaces

AvailableActionsQuery (domain Military): Recruit (per settlement; known identities; refusal as blocker), Re-equip (per
formation; OD-2 refusal visible), Cancel re-equipment, Disband. Sim.Ui: `MilitaryOrderFactory`, `UiSession.EmitRecruit /
EmitReequip / EmitCancelReequip / EmitDisband`, ActionDispatch, the roster block's order buttons, the unit card's
personnel and re-equipment lines (render state only). The CLI lists no orders by kind; its save notes, `sim diff` layout
and ReplayGuard know v39.

**Older worlds and the military layer (the R4 rule, and the session rule).** The military layer acts at the FOUNDING (the
warband's people), so switching it off must reach the founding too:
- every `TestConfigs.Pre*` attribution twin strips it (`TestConfigs.PreMilitary`); a test whose ON arm is compared with
  such a twin uses the military-off world as its ON arm;
- `HeadlessFounding.Found` and `UiFounding.Found` take `withoutMilitary`; `UiSession.StartWith` founds with it whenever its
  config's layer is off; `sim inspect` and `UiSession.Resume` replay a session recorded before v39
  (`RecruitmentQuery.RecordedWithoutMilitary`) founded and stepped without it; the CLI's `--without-discovery` implies it
  (founding and rules), so a `--without-discovery` leg is the in-process `TestConfigs.PreDiscovery` world;
- a migrated pre-v39 save has no personnel rows: its warbands stay tokens that hold no one (the migration note says so).

## 9.1 Moved pins (MEASURED on the combined research + units tree, int-base-r2; each with its military-off control)

The "old" column is the research track's pin (ADR-054, schema v38); the control is TestConfigs.PreMilitary (the research
switches as shipped), whose SchemaProjection.V38 (sections 70–73 stripped, asserted empty) reproduces it byte for byte.

**Historical values (amended 2026-10-28, D-067 R15).** The "new (v39)" column holds the values at the time of the M7
pass, on the combined research + units tree. They are not the live pins. The M7 merge (research + units + economy) and
then the M6 closure moved several of them again. The current values and every later move, each with its control, are in
`docs/m6-plan/golden-attribution-register.md` (rows 12 and 14, and its dated notes of 2026-10-28). The last column below
gives the live value in the integrated tree as of 2026-10-28.

| pin | old (v38, research) | new (v39, at the time of the M7 pass; historical) | cause | control | live 2026-10-28 (see the register) |
|---|---|---|---|---|---|
| SnapshotTests.FoundedGoldenHash + ci.yml FOUNDED_GOLDEN | 1f9f3d4f… | bb825342… | value + layout | MilitaryAttributionTests: PreMilitary twin V38 = old | d5ebd0b2… (M7 merge, register row 14; unchanged by the M6 closure) |
| DrivenGoldenTests.Golden | f23e0dcd… | f320440f… | value + layout | same | 55afd498… (M7 merge, row 14; unchanged by the M6 closure) |
| FirstReignTests.PostR1Golden | 389e0526… | df73d4f2… | value + layout | same | 064ed6b7… (M7 merge, row 14; unchanged by the M6 closure) |
| AiFoundingAttributionTests.S1cTurn300 | 02500b4a… | 3153b7e3… | value (personnel, upkeep, AI recruitment) + layout | same | 5458cb3d… (M7 merge ac4554bf…, then the M6 closure; register dated note) |
| SnapshotTests.ToyGoldenHash | 175f55be… | ff4ea411… | layout alone | V38 of the live world = old | ff4ea411… (unchanged) |
| AiFoundingAttributionTests.PreTranche2Turn300 | 51b33ea0… | 5fb6aa9e… | layout alone (older twin) | V38 = old | 5fb6aa9e… (unchanged) |
| DiscoveryLayoutAttributionTests.PreM6FoundedGolden | 0dadbdfa… | bb9cc22d… | layout alone (older twin) | V38 = old | bb9cc22d… (unchanged) |
| DiscoveryLayoutAttributionTests.PreM6AiTurn300 | 5258fe16… | 55fbce7e… | layout alone (older twin) | V38 = old | 55fbce7e… (unchanged) |
| AiEmpireIntegrationTests (canonical AI): advance / road / levy / A3 | 65/66, 134/135, 379/380, 343 | 66/67, 135/136, 380/381, 345 | value (aiRecruitment off alone: A3 344) | `OneAiEmpire_WithTheMilitaryLayerOff_…` returns old | 66/67, 135/136, 373/374, 344 (levy and A3 moved by the M6 closure) |
| AiAgeAdvancementIntegrationTests (AI, player A2) | 413, 227 | 420, 230 | value | `FoundedAiEmpire_WithTheMilitaryLayerOff_…` | 420, 230 (unchanged) |
| MerchantTests first active merchant town | 39 | 120 | value | `MerchantsEmerge_WithTheMilitaryLayerOff_…` | 120 (unchanged) |
| RunFromSaveCliTests AI colony founded turn (rig premise) | 73 | 76 | value | — (CLI rig) | 77 (moved by the M6 closure) |
| Sim.Ui SettlementCompositionTests.PinnedCanonicalDigest | 31a160dc… | ed95b7f7… | value (capital adults) | `Composition_WithTheMilitaryLayerOff_IsTheV37Digest` | ed95b7f7… (unchanged) |
| AvailableActions turn-one count (Sim.Tests + Sim.Ui) | 133 | 146 | + 12 Recruit, + 1 Disband | — (surface) | 146 (unchanged) |

Controls: `MilitaryAttributionTests` (every hash row), `Adr054AttributionTests` and the layout chain
(`ResearchReserveLayoutAttributionTests.LayoutAlone`, `CivicsPointsAttributionTests`) now run their "live" arm as the
military-off twin and compare through SchemaProjection.V38 / V37. Older-subject rigs (class emergence, famine determinism,
food relief, founding endowment, construction reservation and incremental construction, the per-settlement-store observed
world, the purse per-layer arm) run on the military-off world by the R4 rule instead of moving their pins. These pins are
valid for this combined tree; the economy track moves FOUNDED_GOLDEN and the AI goldens too, so the final integration
re-derives them with the same controls.

**Observability.** TurnRecord's grain and good accounts carry `Military` (reasons 19–21); PopulationAccount carries
`InFormations` (people in no settlement's record); the per-settlement store-loss identity absorbs military grain
(`StoreLossesIdentity`); the telemetry grain object writes `military` only when non-zero (a military-off world's line body
is byte-identical; the reader takes it as 0). **Tag (amended 2026-10-28):** the M7 pass shipped this line-shape change
under the unchanged `telemetry/v4` tag; the M6 closure moved the writer to `telemetry/v5` (D-066 A15, ADR-059;
`TelemetryWriter.Schema`); the reader reads v5 and v4 (`ReadableSchemas`). "Byte-identical" above therefore refers to the
military term's absence from the line body only: every line written now carries the `telemetry/v5` tag.

## 10. What stays blocked / unresolved

- **Blocked**: every A4+ identity needing iron/steel/coal (EC-1), mounts (EC-2), gunpowder/explosives (EC-3); all
  aviation (engines and fuel, **EC-4a, M10 provisional**; Flight II–III also aluminium EC-5a, electronics EC-5b, composites
  EC-5c) and the **airfield** (`building.airfield`: no goods.json project builds it); every `mobile_agent` family (naval,
  air, missiles, reconnaissance) — no MobileAgent table, movement, basing or water edges; strategic missiles (launch facility + M10 governance); nuclear submarines (EC-6 + M10).
- **Unresolved**: **OD-2** (same-line catch-up) — refused, isolated in `TransitionPolicy`; RC-1 (`aircraft` retag) is the
  research track's; Q4, Q8, OD-1, OD-3 untouched; R11 garrisoning, R13 naval transport's reclassification (ADR-031
  amendment) and R14 / Q10 snipers are open and untouched here.
- **Not implemented by ruling (D-065 R9–R10)**: the category-B abstract support capabilities (engineers, supply, medical) —
  future battle-system work, no buffs invented here.
- **Deferred**: upkeep shortfall consequences (M7.4), salvage credit, re-equip labour from the production account, the
  aviation Age validator (after RC-1).

## Amendment log — 2026-10-28 (D-067 R15)

Authority: D-067 R15 (D-067 §3) ratifies this ADR after these documentation
amendments. This ADR was PROPOSED and uncommitted, so the stale facts below were corrected in place (gov-4 §6 forbids
rewriting history; each in-place change is listed here, old → new, with its evidence). **No design decision, value,
mechanism or behaviour was changed.** Each fact was checked against the integrated worktree (`wt-m6`) on 2026-10-28.

| # | Location | Old → new | Evidence |
|---|---|---|---|
| 1 | Status line | "PROPOSED — implemented on the units-track feature worktree under D-065 R7 (…); not merged." → "ACCEPTED — RATIFIED by the Director on 2026-10-28 (D-067 R15), after the amendments logged here", plus: integrated in the combined worktree, uncommitted and unmerged; no commit, push or merge authorized (R20: M7 pass first, M6 closure second); ratification explicitly covers scope beyond D-065 R7's literal list (Recruit 16, Disband 18, soldier upkeep, AI recruitment enabled). | `docs/d067-m6-exit-gate-director-rulings.md:77, 79–83, 138`; `docs/d065-m7-charter-rulings.md:41` (R7's list, "for these items only") |
| 2 | §7, the AI's choice key | "composite integer key (identity Age, identity key, settlement id)" → "the least composite integer key (identity Age ascending, identity key ascending, settlement id ascending — `AiMilitaryPolicy.Compare`; no family key)". The order was already as the code has it; the change states the directions explicitly and rules out the family key that `sim.json`'s description had wrongly named (corrected the same day). "Identity key" is the identity's content key (`UnitIdentity.Key`, e.g. 102). | `Sim.Core/Systems/Military/AiMilitaryPolicy.cs:16–17, 44, 49, 64, 77–84`; tie-dense test `Sim.Tests/Military/AiMilitaryPolicyTests.cs:62`; `Sim.Data/content/sim.json:362` |
| 3 | §9.1, pin table | The "new (v39)" column is relabelled "new (v39, at the time of the M7 pass; historical)"; its values are kept unchanged. A note above the table and a new last column "live 2026-10-28 (see the register)" are added. Changed live values: founded `bb825342…` → `d5ebd0b2…`, driven `f320440f…` → `55afd498…`, first reign `df73d4f2…` → `064ed6b7…` (all three by the M7 merge, register row 14; unchanged by the M6 closure); AI world `S1cTurn300` `3153b7e3…` → `ac4554bf…` (M7 merge) → `5458cb3d…` (M6 closure); AI integration levy / A3 `380/381, 345` → `373/374, 344`; CLI AI colony turn `76` → `77`. Every other row is unchanged. | `docs/m6-plan/golden-attribution-register.md:16–19, 40, 42, 64, 74`; `Sim.Tests/Kernel/SnapshotTests.cs:1184, 1201`; `.github/workflows/ci.yml:266`; `Sim.Tests/Kernel/DrivenGoldenTests.cs:118`; `Sim.Tests/Systems/FirstReignTests.cs:58`; `Sim.Tests/Kernel/AiFoundingAttributionTests.cs:129, 170`; `Sim.Tests/Kernel/DiscoveryLayoutAttributionTests.cs:34, 42`; `Sim.Tests/Kernel/AiEmpireIntegrationTests.cs:310, 374–375`; `Sim.Tests/Systems/AiAgeAdvancementIntegrationTests.cs:109`; `Sim.Tests/Systems/MerchantTests.cs:211`; `Sim.Tests/Kernel/RunFromSaveCliTests.cs:185`; `Sim.Ui.Tests/SettlementCompositionTests.cs:726`; `Sim.Tests/State/TurnOneActionSurfaceTests.cs:176`; `Sim.Ui.Tests/ActionSurfaceTests.cs:208` |
| 4 | §9.1, "Observability" | "(a military-off world's line is byte-identical; the reader takes it as 0)" → "line body is byte-identical", plus a tag note: the pass shipped the line-shape change under the unchanged `telemetry/v4` tag; the writer now emits `telemetry/v5` (D-066 A15, ADR-059), and the reader reads v5 and v4. | `Sim.Core/Observability/TelemetryWriter.cs:35, 45, 51`; `docs/d066-m6-closure-rulings.md:60`; `docs/adr/adr-059-one-file-session-export.md:128` |

**Accepted consequences (D-067 R15, D-067 §3).** Recorded, not changed:
- **Soldiers are immortal until M7.4.** Formations have no attrition (§4: people in a formation do not age, die or give
  birth; the upkeep shortfall accumulates in `UnfedUnits`, which has no consumer).
- **The founding warband's draw changes early history.** Its 20 adults leave the capital by `Ledger.Transfer` at founding
  (§4), which moves early pins (§9.1; for example the first merchant town, 39 → 120).
- No new behaviour is introduced to remove either limitation.
