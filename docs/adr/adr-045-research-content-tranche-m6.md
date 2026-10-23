# ADR-045 — The M6 research-content tranche (knowledge redesign, non-breaking part)

**Status: ACCEPTED under D-062** (the Director's final M6 decisions of 2026-10-22, recorded by the coordinator: the
knowledge-redesign proposal is accepted as the design basis; costs may be adjusted where content justifies; the nuclear
knowledge chain is accepted as knowledge only, with no mechanic). Track TREE of the M6 slice. Implemented on `m6-tree`;
not merged; awaiting the Director's acceptance of the slice.

## 1. What this records

The proposal (`docs/m6-plan/knowledge-redesign/technology-tree-proposal.json`, `civics-tree-proposal.json`) is the
canonical source. This tranche implements its **non-breaking** part in `Sim.Data/content/research.json`:

- the 70 new technologies, keys 427–496 exactly as the proposal assigns them;
- 133 prerequisite rewires, 61 factor re-scores, 22 relocations of practitioner crafts to the Main Technology Tree, 6 of
  the 8 renames, capability-string and description updates on existing keys, Age / domain / date / family /
  university-classification metadata;
- the 60 new civics, keys 1008–1067, as **knowledge-only civics** (§3);
- the entity requirement changes that keep every holder eligible (§4).

It does **not** retire a key, merge two nodes, or move a consumer outside the node graph off an existing key: those need
the remap seam (roadmap KR-2) and are DEFERRED. `researchStage` and `tuning` are untouched (Track CP owns them); the 7
legacy civics are unchanged except their derived `depth` (it follows their technology prerequisites, which the rewires
move — the loader requires it to agree).

The per-change table (every proposal change with its verdict and reason) is generated with the content:
`docs/m6-plan/knowledge-redesign/implementation-status.md`, by
`docs/m6-plan/knowledge-redesign/tools/implement_tranche.py` (deterministic; `--check` exits 1 when either output is
stale). The tool reads the pre-tranche file from `Sim.Tests/Fixtures/research-pre-adr045.json` (byte-identical to
`research.json` at `06e0153`, SHA-256 `7a3bf388…`), which is also the attribution twin's fixture (§6).

## 2. Summary of verdicts

| Change | accepted | revised | deferred | rejected |
|---|---|---|---|---|
| New technology nodes (keys 427–496) | 70 | 0 | 0 | 0 |
| New civics (keys 1008–1067, knowledge-only) | 60 | 0 | 0 | 0 |
| Splits (the new part is a new key) | 10 | 4 | 0 | 0 |
| Rewires (prerequisite edits on existing keys) | 133 | 0 | 0 | 0 |
| Re-scores (factor and cost edits) | 61 | 0 | 0 | 0 |
| Relocations to the Main Technology Tree | 22 | 0 | 0 | 0 |
| Renames (8 proposed on existing keys; 15 new nodes' display names) | 6 | 15 | 1 | 0 |
| Capability-string updates on existing keys | 7 | 1 | 1 | 0 |
| Description updates on existing keys | 6 | 0 | 0 | 0 |
| Metadata (Age relevance, domains, dates, family, university classification) | 36 | 0 | 0 | 0 |
| Entity requirement changes (19 proposed + 2 implied widenings) | 8 | 6 | 7 | 0 |
| Merges and retirements (2 merges; their 2 survivors' merge edits) | 0 | 0 | 4 | 0 |

The siege_ram rename is counted with the merge it belongs to (deferred), so the renames row shows 6 + 1 of the 8 on
existing keys. The 15 revised renames are NEW nodes whose proposal name (e.g. "Geophysical prospecting (magnetic,
electrical, gravity and seismic)") does not fit the narrowest research card on two lines (UR-4, ReadabilityStageBTests):
the parenthetical detail moves to the description.
Nothing is rejected: every deferral waits on a named seam or ruling, not on a judgement against the proposal.

## 3. The 60 new civics: implemented as knowledge-only civics

Decided from the loader rules (`ResearchContent.cs`): a civic is valid when it is not an orphan (it declares a
capability string), carries no `branch`, names at most one primary university domain, and unlocks institutions only
(`ValidateUnlocks`: "a Civics node makes institutional forms eligible … it cannot unlock a building"). Each new civic
carries its proposal capability strings and **no entity unlock** — no regime or policy entity kind is introduced, and
adoption stays M8. The loader accepts all 60 (the content tests load the shipped file). Costs follow the rubric
(U·K^m) exactly as for technologies; whether they are paid in RP or Civics Points is Track CP's (D-061/ADR-043). Each
description ends "Knowledge only for now: adopting a form of government arrives with the government layer (M8)."

## 4. Splits and entity requirements: the non-breaking rule

A split's new part is always added as a new key. Node prerequisites re-pointed to the new part are rewires: completed
knowledge never decays, so no holder loses anything (partial progress waits until the new prerequisite holds, as for
every rewire). A consumer OUTSIDE the node graph — an entity requirement — moves off an existing key only when every
holder of that key stays eligible:

- **Accepted (widenings):** `building.library`, `building.school`, `inst.archive`, `inst.historiography`,
  `inst.natural_philosophy`, `inst.scribal_school` gain `mesoamerican_script`; `unit.siege_engine` gains
  `traction_trebuchet`; `activity.ocean_shipping` gains `ocean_junk` (the last two are carried only in the new nodes'
  unlock lists in the proposal).
- **Revised (new route OR the old one):** `building.lighthouse` = `ashlar_masonry OR stone_fortification` (the split
  makes `stone_fortification` require `ashlar_masonry`, so for every new completion this is the proposal's requirement);
  `infra.sewerage_system` = `sanitary_engineering OR (germ_theory AND reinforced_concrete)`; `inst.guild` =
  `textile_workshop AND (legal_code_roman OR craft_guilds)`; `inst.military_medicine` =
  `surgery_instruments AND (census OR standing_army)`; `inst.mint_institution` =
  `coinage_electrum AND (census OR appointed_officials)`; `inst.statistics_vital` =
  `(census AND calculus) OR (official_statistics AND epidemiology)`. The narrowing each proposal states waits for KR-2.
- **Deferred (the OR form would be a no-op, the narrowing removes a consumer):** `building.spaceport` and
  `project.orbital_launch` (rocket → orbital_launch_vehicle), `infra.power_grid` (electricity_generation →
  ac_transmission), `inst.mass_media_broadcast` (radio → radio_broadcasting), `inst.central_bank` (→ the central_banking
  civic, whose prerequisite IS the old requirement), `inst.mass_schooling` (census → compulsory_education) and
  `inst.quarantine` (census → public_health_board) — in each the new route requires the old key, so keeping holders
  eligible means keeping the old requirement.

So four splits are **revised**: `stone_fortification` (lighthouse widened instead of moved), `electricity_generation`
(ac_transmission added and the grid dependants re-pointed; the grid entity and the rename "Central power station" wait
— the key keeps the eligibility its name describes), `rocket` (orbital_launch_vehicle added, satellite and
reusable_launch re-pointed; spaceport and orbital launch stay, and so does the string "satellite launch"), `radio`
(radio_broadcasting added, television and mobile_phone re-pointed; the mass-media institution and the string "broadcast
media" stay; the rename to "Wireless telegraphy (radio)" is applied, since `inst.mass_media_broadcast` also needs
television, which now needs broadcasting). The other ten splits keep every outside consumer on the existing key and are
accepted.

## 5. Other decisions

- **Merges deferred.** `siege_tower` and `crossbow_repeating` stay, with their keys; their survivors' merge-only edits
  (siege_ram's rename, widened date and gained string; crossbow's gained string) wait with the merge.
- **Eureka rules.** Existing Eurekas are unchanged (append-only; their index is what ResearchEurekaRow carries). The 17
  new technologies with an Eureka carry one authored Eureka each, weight 0.40 (9 machine-evaluable over knowledge); the
  loader's dead- and implied-Eureka checks accept them.
- **Cost tiers.** The loader allows T1–T4/TR/TS and checks no meaning; the shipped convention is T1 ⇔ novelty 0.5, T4 ⇔
  novelty 2.0, T2/T3 for novelty 1.0. New nodes follow it (no new node is T3); a re-scored node whose novelty crosses the
  convention moves tier (5 nodes: internal_combustion, computer, transistor, genome_sequencing T4 → T2;
  guided_munitions T2 → T1). The
  `costRationale.note` of every new and re-scored node carries the proposal's factor rationale.
- **Not in this tranche:** unit entities for the new military nodes (proposal "OPEN DECISION, T6.12"),
  `identification:` / `production:` grants (docket A-5; Track EX owns the M6 Prospect action — no node reveals a
  deposit), the proposed `inst.geological_survey` and `inst.radiation_protection_service`, and the educational corpus
  (0 of 561 texts may ship). The in-game descriptions of the 130 new nodes are short game text written for this tranche.
- **D-047 chronology.** The content test's rule (no must-held prerequisite dated after its dependent) holds on the
  whole graph; the converter checks it too. 82 `emerged` texts carry no era marker (83 before).
- **Calibration report.** `docs/research-calibration-report.md` is regenerated from the content and the committed
  population trace. The trace predates this tree (its turn-1 population is 5,245; this tree's `sim research --seed 42
  --turns 1630 --auto cheapest` measures 5,118 at turn 1, before any research), so re-measuring it would fold unrelated
  drift into this tranche; it is left as committed.

## 6. Golden movements and their attribution

The attribution control is a **content switch-off twin**: `TestConfigs.PreTreeTranche(cfg)` swaps the shipped research
content for the pre-tranche file (`Sim.Tests/Fixtures/research-pre-adr045.json`). No code reads the tranche, so a world
stepped under the twin is the pre-tranche world byte for byte. Age and unit-family content need no reload: Age facts
hold node keys and ids, never dense indices. By the R4 rule (an older layer twin strips every newer layer) the chained
twins strip the tranche through `PreIncrementalConstruction`, the R2a content twin is derived from the pre-tranche file,
and the older-layer attribution tests whose "on" arm pins a pre-tranche literal run on `TestConfigs.PreTreeTrancheSim()`.

**What moved (MEASURED on this tree, in-test harness, by the agent writing this ADR).** Only worlds in which an AI
chooses research move; no code changed, so nothing else can.

| Pin | Old → new | Control (content switch-off twin returns OLD byte for byte) |
|---|---|---|
| `AiFoundingAttributionTests.S1cTurn300` (the live AI world, seed 42, turn 300) | `25f4799d…` → `a7858f32…` (v32 projection `0ce1d4d9…` → `126769ae…`) | `TheResearchTranche_IsTheWholeMove_TheAiWorldOnThePreTrancheContentIsTheBatch4World` |
| `AiEmpireIntegrationTests` canonical AI run | first targets knapping_oldowan, ground_stone_early → grinding_stone, cereal_cultivation; target 2 at 25 → 18; advance 221/222 → 97/98; road 134/135 → 162/163; levy 422/423 → 402/403, A3 422 → 402; foundings and holdings unchanged | `OneAiEmpire_OnThePreTrancheContent_EveryPinReturnsItsBatch4Value` |
| `AiAgeAdvancementIntegrationTests` decision turns | AI 421 → 413, player 312 → 251 | `FoundedAiEmpire_OnThePreTrancheContent_AdvancesAtTheBatch4Turns` |
| `AiPolicyTests` turn-0 goal | the road goal (pick knapping_oldowan) → the core goal (pick grinding_stone) | the same test's pre-tranche block |
| `RoadDevelopmentTests` motor_road depth | 20 → 21 (automobile_mass's chain is now the longest) | content rule: 1 + deepest prerequisite |

**What did not move:** the founded golden (`SnapshotTests.FoundedGoldenHash` and ci.yml `FOUNDED_GOLDEN`,
`0f29efc7…`), the driven golden (`DrivenGoldenTests.Golden`, `eb88663a…`), the first-reign pin
(`FirstReignTests.PostR1Golden`, `f7ca60a5…`) and the toy golden — measured equal on this tree, so ci.yml and
`CiPinAgreementTests` are unchanged. The cause: AI goal research is the only research those worlds lack a fixed order
for; the driven and first-reign logs carry no research order and the founded world has no AI.

**Older-layer controls rebased (the R4 rule), no value changed:** `WrapAttributionTests` (AI world),
`IncrementalConstructionAttributionTests` (AI world), `ReservationsLayoutAttributionTests` (AI world; its raw value is
now `AiFoundingAttributionTests.PreTrancheS1cTurn300`), `CylinderAttributionAi1024Tests` (shared ON arm),
`CylinderAttributionDevAiTests` (ON arm), `CylinderAttributionSeamTests` (trade rig), the cylinder-off controls of
`AiEmpireIntegrationTests` and `AiAgeAdvancementIntegrationTests`, and — through the chained twins — every
PreD054Sim / PreD053 / PreForager / PreTaxAge / PreAiFounding / PreR1 control. `CliConstructionLifecycleTests` compares a
CLI process (which always runs the shipped content) with a session the v33 build recorded: its v33-trace and `sim diff`
legs now use an in-process twin of `sim run` (`InProcessRun`) under the pre-tranche content, and the twin is tied to the
CLI by asserting its shipped-content hashes equal the CLI's hash log.

## 7. Compatibility

- **Keys.** No key is renumbered or retired; 130 keys are appended. Old saves load unchanged (completed rows name only
  existing keys).
- **Saves and replays.** A replay of an old session under the new content diverges wherever an AI or the CLI auto driver
  chooses research (the cheapest-available and goal closures changed). Player research orders name keys and stay valid.
- **Schema.** None (no row type, no section).
- **Track CP** (ADR-043; `m6-cp` 5af05ad) drops `legal_code_roman` from `researchStage.requires` and adds
  `tuning.civicsPoints`. This tranche edits `technologies`, `civics` and `entities` only; the converter keeps every other
  byte of the file identical (asserted: `researchStage` and `tuning` parse equal to the base), so the two merge
  textually. The content is valid under either stage: the 60 new civics and the new trunk nodes are reachable before the
  stage with or without the civic atom. MEASURED: `git merge-file` of this file and CP's 5af05ad file over the
  pre-tranche base is conflict-free, and equals the converter's output written onto CP's file (the converter keeps
  whatever `researchStage` and `tuning` the output file already holds, so its `--check` stays meaningful after the merge).
  CP's two-lane AI will find 67 civics to fund with Civics Points; the merged AI pins are the coordinator's to re-measure.
