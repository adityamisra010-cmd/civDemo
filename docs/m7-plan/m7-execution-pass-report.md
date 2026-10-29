# M7 Execution Pass: Report (2026-10-27 to 2026-10-28)

**Status:** IMPLEMENTED, UNCOMMITTED, awaiting the Director's review.
- **Branch:** `claude/new-session-yavghm`, working tree over `0bf7626`.
- **Not done:** nothing was committed, pushed, merged, tagged or released.
- **Size:** 216 files, +14,200 / −800.

This report covers the whole pass:
- the M7 execution prompt: RP/CP, RC-1, units, EC-1..3, the research-economy candidate, the charter gaps;
- the audit fixes;
- the permanent tiered validation policy;
- the unit taxonomy ruling;
- the integration of all of it into one validated tree.

---

## 1. Strategic summary

**What exists now:**

| Area | Result |
|---|---|
| Research and civics | Research and civics points are computed per turn from the average population, with one shared base function, as D-065 R6 requires. Civics pacing is recalibrated. |
| Aircraft (RC-1) | Moved to Age 8. |
| Military layer | Real people in formations; recruit, re-equip and disband orders. The AI recruits through legal orders only. |
| Resource packets | Metals, mounts and gunpowder exist as three separately switchable packets. |
| Validation | Change-aware tiers, a slow-test tier backed by measurement, and a machine lock. |

The whole combined tree passed Tier 3:
- both full test suites;
- the CI determinism and cross-process legs, run locally.

**What is weak.** These are the parts the Director should look at first:
1. **The new content barely reaches a normal game.**
   - In natural play no AI reaches iron, coke, steel, saltpetre or dynamite within 700 turns, so EC-1 and EC-3 change nothing. EC-2 (horses) starts only at turn 614.
   - The AI recruits exactly once (turn 243): every later unit needs a packet good it never reaches.
   - The engine is built; the AI does not walk into it.
2. **RC-1 did not achieve its aim.** Aircraft now complete 80–107 turns after Age 9 entry, against 31 before, because the Age 8 pacing scale makes the node 5.7–6.8× dearer.
3. **End-game research loses 84–87% of its research points.**
   - On 65–69 of roughly 141–145 turns after Age 8, the AI's cheapest destination is a repeatable level, so no finite node completes.
   - The diagnosis is done; the fix is AI policy and needs an ADR.
4. **Soldiers cannot die yet.** ADR-055 §4 by design, until M7.4. In a starving world the founding warband outlives its settlement.
5. **The founding warband moves early history noticeably.** It draws 20 people from the first settlement; for example, the first merchant town comes at turn 120 instead of 39 (pinned and attributed).

---

## 2. What was built, by track

### A. RP/CP per turn on average population (D-065 R6, ADR-054)

**Formulas:**
- Before: RP = 0.08 × P^0.699 per turn, and CP = floor(1000 × 0.008 × P^0.699 × dtYears).
- After: both lanes call `ResearchQuery.BaseKnowledgePointsPerTurn` = 0.08 × P^0.699. CP is per turn and is never multiplied by dtYears.
- Base RP equals base CP exactly at dt 10, 5, 3, 2, 1 and 0.5 (tested).

**Average population:**
- P is the mean of the holder's population at the start of the previous research step and at the start of this one.
- On a holder's first step, P is the current population.
- The previous value is stored in the new schema v38 table `ResearchPopulations`, section 69.

**Switches:** `civicsPoints.perTurn` and `populationAverageTurns: 2` are both on. The twin `TestConfigs.PrePerTurnCivics` reproduces every old hash.

**Records:**
- ADR-054;
- a dated note on ADR-043;
- CR-026 resolved;
- TC-2 in `tree-closure.md`.

**Civics pacing, shipped:**
- Ages 1–6 and 8 finish within −7…+14 turns of technology.
- Ages 7 and 9 sit at −47…−61, a structural gap that existed before this change.
- Every seed completes every Age.

### B. RC-1: aircraft moved from Age 9 to Age 8

**Changes:**
- Magnitude 9.5 → 8.5 and cost 66,900 → 33,450 (ADR-045 re-score).
- STRUCTURE hash → `489dc44e…`; TEXT unchanged.

**Consequence (§7, D1):** aircraft completes 80–107 turns after Age 9 entry. Before RC-1 it completed 31 turns after, one turn behind glider.

### C. Research-economy candidate and Age 9 throughput diagnosis

**Candidate:**
- Its civics scales are recalibrated in `trial/research-economy-candidate.json`, kept separate from the shipped defaults.
- Every seed completes every Age.
- Age 4 is entered 59–62 turns later, and Ages 5–9 21–45 turns earlier.
- It is bistable in Ages 2–3.

**Age 9 throughput, measured cause:**
- In the end game the AI's destination is the cheapest available node.
- When that node is a repeatable level, no finite node completes; this happens on 65–69 of about 141–145 turns.
- 84–87% of the research points in that window are lost.
- RP, cost, the Age sweep and prerequisite depth are not binding.
- A model (`tools/a9_throughput_model.py`) gives about +77 turns as is, against +22–30 if every affordable node were paid for.
- Two AI-policy fixes are proposed in the queue, not implemented.

### D. Units (ADR-055, schema v39)

**Families:**
- 20 families and 80 identities.
- Families 13, 15 and 16 are withdrawn per D-065 R10; their keys stay unallocated.
- `strike_aviation` is a separate family (23).

**Schema v39 (sections 70–73):** formation personnel, re-equipment, and the military tables. The save migration refuses unmapped families.

**Orders:**
- 16 RecruitFormation, 17 ReequipFormation and 18 DisbandFormation.
- Re-equipment covers role and branch changes only; same-line catch-up (OD-2) stays refused.

**Personnel:**
- The founding warband draws real people; they are moved through the Ledger.
- Formation upkeep is paid in grain, with its own sinks: Recruitment 19, FormationUpkeep 20, Reequipment 21.

**AI recruitment:**
- Shipped on. Across seeds 1, 2, 42 and 7, every AI Age-entry turn is identical to the run with the policy off, or one turn later.
- In 600 turns the AI recruits once, at turn 243.

**UI:** military order buttons on the action surface, routed through the `PlayerKnowledge` seam, with no raw world reads (§5).

### E. Economy packets EC-1, EC-2 and EC-3 (ADR-056)

**Packets:**
- `goods.json` gains a `packets[]` list with an `enabled` flag per packet. A disabled packet removes its goods and recipes at load, so its twin is byte-identical to the earlier world.
- **Knowledge gates:** a recipe or good names existing research node ids in a new `knowledge` field. There are no `research.json` edits and nothing new under TC-1.
- **EC-1 metals:** iron-ore, coal, iron and steel, with five recipes.
- **EC-2 mounts:** horses breed on pasture under herders. Fodder (23) and HerdLosses (22) are new sinks.
- **EC-3 gunpowder:** saltpetre, sulphur, gunpowder and explosives.
- No schema change.

**Planned goods resolve both ways:**
- Packet on: a unit's recruitment input resolves to the real good.
- Packet off: the input falls back to "planned", which blocks the unit.
- `UnitFamilyContent.ResolvedAgainst` re-applies this whenever packets switch; `EconomyRecruitmentTests` checks both directions.

**Measured, packet against its switch-off twin, four seeds:**
- **Given the knowledge at founding:** population changes by at most ±0.2%.
- **Natural play:** identical to the twin, because no gate is reached.

**Dilution:** crafting labour splits equally across all available recipes, so cloth and pottery fall 3–5% once a packet's knowledge is met (queue).

### F. Audit fix (fog of war)

- `DiscoverySystem` now passes `cfg.Research` to `KnownBits`. Land revealed through research-improved sight stays known after a formation leaves.
- Two new test classes cover it.
- No pinned golden moves, including the AI world.

### G. Permanent tiered validation policy

**Pieces:**
- Policy: `docs/validation-policy.md`.
- Entry point: `scripts/validate.sh {targeted|integration|full}`.
- Selector: `scripts/tiered_validation.py`, driven by `scripts/validation-map.json`.
- Its own self-test (31 tests) and a link checker; CI runs both.

**Slow tier, measured on the final tree's full suites:**
- In Sim.Tests, 146 of 3,254 tests (4.5%) take 15 s or more, and they use 86% of the suite's test time (130 of 152 min).
- In Sim.Ui.Tests, 13 of 1,125 tests use 42%.
- Tier 1 now skips the 150 recorded slow tests (`scripts/test-durations.json`); Tiers 2 and 3 still run them.
- On the earlier Granary change, Tier 1 went from 11m25s to 5m38s.

**Machine lock:** one full suite at a time per machine, through `flock`.

**Fixed this pass:** the `research-tool-tests` entry ran `unittest discover`, which found 0 tests. The selector correctly reported that as FAIL; the entry now runs the scripts directly (see §5).

### H. Unit taxonomy (D-065 Addendum 3, R8–R14), documentation only

**Rule:** only an independently controllable formation is a unit family.

**Categories:**

| Category | Meaning | Examples |
|---|---|---|
| A | Controllable formations | — |
| B | Abstract military support | engineers, supply, medical |
| C | Air defence | mobile or garrisoned |
| D | One-shot actions | prospecting |
| E | Implicit economy and transport | — |

Where it is recorded:
- unit charter §4.5;
- building charter §5.16a (garrisoned air defence);
- work item GA (proposed for M7.8);
- the new gap-checklist item;
- the CLAUDE.md entity-proposal line.

---

## 3. Integration

**Order:** dev changes, then research (v38), then units rebased (v39), then economy merged on top. Each agent worked in its own worktree. The coordinator kept the integration tree.

**Conflicts resolved:**
- Reason ids collided at 19/20. Units keeps 19–21; economy moves to 22/23.
- 30 attribution twins in `TestConfigs` now compose both ways:
  - every older twin strips both new layers;
  - `PreEconomyTrack` keeps the military layer;
  - `PreMilitary` keeps the packets.
- The Observer and telemetry carry both the military and the herd terms. A world without a layer still produces byte-identical lines.

**Live goldens, re-pinned on the combined tree. Every pin has two measured, exact controls:**

| Pin | Packets off = units pin | Military off = research+economy pin | Combined |
|---|---|---|---|
| Founded + ci.yml `FOUNDED_GOLDEN` | bb825342… | bd8a343b… | d5ebd0b2c642ec5c… |
| Driven | f320440f… | d6862db0… | 55afd498a326ab2a… |
| First reign | df73d4f2… | 58f01705… | 064ed6b76374dea4… |
| AI world `S1cTurn300` | 3153b7e3… | 56e8e867… | ac4554bfdcd3d5b6… |

**Other moved pins:**
- Layout-only pins (toy, PreTranche2, PreM6*) moved only by the research and units layouts.
- AI integration values moved under units: road 135/136, levy 380/381, Age 3 at 345.
- First merchant town 39 → 120.
- Each of these has its attribution test.

---

## 4. Validation (Tier 3, on the final tree)

| Check | Result |
|---|---|
| Build | PASS, 0 warnings, 0 errors |
| Banned constructs, read isolation, read-only proof | PASS |
| Research audit, calibration report, unlock audit, tree closure, charters, doc links (all files) | PASS (unlock audit regenerated) |
| Validation selector self-test | PASS |
| **Sim.Tests, full** | **PASS: 3,219 passed, 0 failed, 36 skipped (measurement-only), 49m48s under load** |
| **Sim.Ui.Tests, full** | **PASS: 1,126/1,126, 20m34s** |
| CI determinism harness (T0.8), run locally | PASS: 75/75. The local exit was non-zero only because `$GITHUB_STEP_SUMMARY` is unset. |
| CI cross-process legs (T0.9), run locally: orderless, replay, founded, seam, found-settlement, AI world over 600 turns, inspect | PASS. Reproduction verified at 301 and 601 turns. |
| CI allocation leg (D-055), run locally | PASS |
| `research-tool-tests` | **FAIL, pre-existing:** also red on `0bf7626` (0/25 validator fixtures; `validate.py` reports 341 errors). See §7, D9. |

**Not run (none of these is reported as passing):**
- `sim bench` (heavy; production phase measured at +55% by the economy track under load, inside noise);
- the four-seed overlay trial (heavy; run separately by the research and units tracks);
- `calibration-nightly` and the Windows / cross-platform smoke (CI-only);
- manual GUI acceptance.

## 5. Defects found and fixed during integration

| # | Defect | Fix |
|---|---|---|
| 1 | The first integration base (`int-base-r1`) lacked the fog fix and the tracked doc edits: a three-way apply failed silently on `ci.yml` | Rebuilt as r2; the units agent re-ported onto it |
| 2 | ReasonId collision: units and economy both used 19/20 | Economy moved to 22/23 |
| 3 | Planned goods resolved only at load, so switching a packet off left dangling good ids | `ResolvedAgainst(goods)` plus `EconomyRecruitmentTests` |
| 4 | **Fog breach:** `MilitaryOrderFactory` read `world.MilitaryUnits` raw | Routed through `PlayerKnowledge` and `RecruitmentQuery`; new metamorphic leak test, killed against a mutant |
| 5 | Three GlassBox tests: the warband's people live in no settlement | Exact identity kept, plus an InFormations term and a shipped-config twin |
| 6 | `MilitarySystem` computed a map column with raw arithmetic (topology guard) | New `MapTopology.CellPosition` |
| 7 | The fog test removed a formation without its personnel row | Rig now models the loss consistently |
| 8 | Dead-world, Annals and A1-surface tests lost their premises to the warband's draw | Older-twin rule applied, plus shipped-config attribution twins |
| 9 | ci.yml's AI kind set was exact, but the AI now recruits (kind 16) | Kind 16 added, with a measured comment |
| 10 | IntegratedPinAttributionTests: 43 failures in the research patch alone (schema 38) | Fixed by the units track; 47/47 on the combined tree |
| 11 | Validation map: `research-tool-tests` ran 0 tests | Calls the scripts directly; now reports their real (pre-existing) failure |

## 6. Measured findings (numbers)

**Research:**
- Shipped civics completion gap: Ages 1–6 and 8 within −7…+14 turns.
- Ages 7 and 9 sit at −47…−61; structural and pre-existing.

**Aircraft (RC-1):**
- 80–107 turns after Age 9 entry.
- Option B in §7 (magnitude 6.0) brings it back to glider + 1 turn, measured on four seeds.

**Units, AI Age entry:**
- With the policy off, seed 42 runs A2–A9 at 67 / 344 / 698 / 733 / 801 / 930 / 1035 / 1141.
- With it on, Age 3 and Age 7 are one turn later (345 and 931). The other seeds are unchanged.

**Economy, natural play (seed 42, 700 turns):**
- Only `horse_domestication` is reached, at turn 614; the herd grows to 382k horses.
- 13.7M grain goes to fodder, all of it grain that would otherwise have spoiled. No food figure moves.

**Warband under famine:**
- The settlement dies out at turn 18, against 11 with the military layer off.
- 20 soldiers survive, with the unfed counter rising every turn.

---

## 7. Decisions for the Director

| # | Decision | Options | Recommendation |
|---|---|---|---|
| D1 | RC-1 timing | A keep · B aircraft magnitude 8.5 → 6.0 · C AI aviation goal (ADR) · D revert | **B now.** It is measured, data-only, and restores aircraft to glider + 1. C later, with the AI goal work in D3. |
| D2 | Age 9 research throughput (84–87% of points lost) | AI prefers finite nodes over repeatables · end-game destination that covers the remaining nodes | **Finite-first preference**, as an ADR: smallest change, biggest effect (+77 → +22–30 turns by the model) |
| D3 | Economy packets inert in normal play | AI material-needs research goal (ADR-052/053 machinery) · research pacing into A3/A4 · leave inert | **AI material-needs goal.** Without it EC-1..3 and every unit beyond skirmishers is dead content |
| D4 | Soldiers immortal until M7.4 | accept · amend ADR-055 so attrition lands now | **Accept**, but put M7.4 supply/attrition next in the unit sequence |
| D5 | Research-economy candidate | adopt · keep as a trial | **Keep as a trial.** It is bistable in Ages 2–3 and enters Age 4 59–62 turns later |
| D6 | Warband draw moves early history (first merchant town 39 → 120) | accept · smaller founding warband · draw from a different pool | Accept for M7; review it in the next playtest |
| D7 | Naval transport (family 10) | ADR-031 amendment per D-065 R13 · keep | Amend: transport becomes implicit (category E) |
| D8 | Q10 snipers · GA garrisoning placement (M7.8) · OD-2 same-line catch-up · OD-3 / EC-4a owner · Q4 (CR-017 §7) · Q8/B5 · CR-017 §3/§5 · B1/B6 wording | — | Still open; this pass decided none of them |
| D9 | Research tool tests red on base (M6 proposal package drifted) | reconcile the package · archive it and retire the tools | **Archive.** It is a planning snapshot that the shipped tree has superseded |
| D10 | Unlock audit cannot see the `knowledge` gate | teach the audit · accept the OPEN rows | **Teach the audit** (small tooling change) |

## 8. Risks

- **Validation gaps:** full suites ran once on the final tree. No mutant runs on the integration fixes beyond the leak test. `sim bench` not run on the final tree.
- **Old saves:** `sim run --from-save` has no research-content check, so a pre-ADR-054 save continues under the new rules. UI sessions recorded before this change are refused on resume, as with any rule change.
- **Hard-coded schema literals:** many tests assert `CanonicalSchema.Version == 39`. The next schema bump touches all of them.
- **UI test twins:** `TestFog.Off` and `TestTranche2.Off` keep the military layer on so recorded v39 sessions still resume. This is deliberate, but they are not full older-world twins.

## 9. Files (main ones)

**Decision records:**
- `docs/d065-m7-charter-rulings.md`;
- `docs/adr/adr-054-…`, `adr-055-…`, `adr-056-…`;
- `docs/adr/cr-026-…` (resolved);
- `docs/adr/adr-043-…` (dated note).

**Charters:**
- `docs/m7-plan/unit-catalogue-and-progression.md`;
- `building-and-infrastructure-charter.md`;
- `aviation-progression.md`;
- `gap-resolution-standard.md`;
- this report.

**Research calibration:**
- `docs/m6-plan/knowledge-redesign/research-cost-pass/per-turn-cp-recalibration.md`;
- `trial/research-economy-candidate.json`;
- `tools/a9_throughput_model.py`.

**Code:**
- `Sim.Core`: Research (ADR-054), Military (ADR-055), Production / EconomyKnowledge (ADR-056), Pathing/MapTopology, State schema v38–v39;
- `Sim.Ui`: MilitaryOrderFactory, UiSession, UiFounding;
- `Sim.Cli`: `--without-economy`, `--without-discovery` (strips military too);
- content: `research.json`, `unit-families.json`, `goods.json`, `sim.json`.

**Validation:**
- `docs/validation-policy.md`;
- `scripts/{validate.sh, tiered_validation.py, validation-map.json, test_tiered_validation.py, check-doc-links.py, test-durations.json}`;
- `.github/workflows/ci.yml`;
- `CLAUDE.md`.

**Registers:** `docs/current-state.md`, `docs/queue.md`.

## 10. Next steps

1. The Director rules on D1–D10.
2. On approval: commit on `claude/new-session-yavghm`, open a PR to `main`, and watch CI to green. That includes the Windows smoke and the nightly calibration, which cannot run here.
3. M7 follow-on packets, in this order:
   - the AI material-needs goal (D3) and the finite-first research preference (D2), as one AI-policy ADR;
   - M7.4 supply and attrition (D4);
   - the GA garrisoning stage.

### Ready-to-copy next prompt

```
Director rulings on the M7 execution pass (docs/m7-plan/m7-execution-pass-report.md §7):
D1 <A|B|C|D>, D2 <…>, D3 <…>, D4 <…>, D5 <…>, D6 <…>, D7 <…>, D9 <…>, D10 <…>.
Authorized: commit the working tree on claude/new-session-yavghm, push, open a PR to main, and drive CI to green
(do not merge without my explicit ruling). Then implement the rulings above as separate packets under the tiered
validation policy (Tier 1 while iterating, one Tier 3 on the integrated tree), starting with the AI-policy ADR for
D2 + D3. Record each ruling in docs/d065-m7-charter-rulings.md as Addendum 4.
```
