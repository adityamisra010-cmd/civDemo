# ADR-046 — M6 review fixes: AI cheapest-route research goals, the multi-good Prospect bill, Explore's units in km, educational text in the UI

**Status: ACCEPTED for the D-062 slice (implementation under the Director's M6 review instruction of 2026-10-23; awaiting
the Director's review of the batch).** Branch `claude/new-session-yavghm`; `main` and `m5-exit` untouched. Labels:
MEASURED · DERIVED · CHOSEN (TUNE).

Authority: the Director's written instruction of 2026-10-23 ("Implement the next M6 review changes …", items 1–5). Each
section below names the frozen or recorded text it touches. Nothing in the Spine, the kernel contract or the schema
changes: no new state, no new table, no schema bump (v35 stands). One order field gains a value (§2.4).

## 1. AI research targets — the cheapest remaining route through OR

**Defect (MEASURED, `docs/m6-plan/knowledge-redesign/audit/taxation-gate-trace.md`).** `AiResearchPolicy` built every goal
closure as the prerequisite ancestors with **union over OR** (ADR-033 B). A goal kept pulling alternatives its requirement
no longer needed — the tax goal researched `hieroglyphic` after `proto_writing` had satisfied the OR, the paved-road goal
pulled four scripts for `surveying` — and under the ADR-043 two lanes the Technology lane spent ~150 turns on that before
bronze, delaying Age III from ≈ turn 420 to ≈ 575 on every measured seed. Taxation and the CP rate were not the cause; no
CP value was touched.

**Fix.** `AiResearchPolicy.Routes`: a goal closure is the **cheapest remaining route** through its requirement —
- a completed node is the empty route; an uncompleted node is itself plus the route of its prerequisite;
- **AND** = the union of both sides (every required side kept; shared ancestors once);
- **OR** = the side with the lower route cost (Σ EffectiveCost over the route, ascending index order); bit-equal costs go
  to the side **written first** — the composite key (route cost, source position), a fixed walk of the parsed predicate
  (`Predicate.Fold`, a new read-only structural fold);
- an alternative already satisfied costs 0, so the OR stops pursuing the others;
- an institution atom routes through its own requirement; a declared-unresolved name has no route (an OR takes the other
  side; a goal with no route is empty); comparisons and negations route to nothing.
An Age research fact (ANY listed node, `AgeContent`) is an OR over its nodes; a goal's several roots are an AND. The goal
choice (cheapest remaining closure, then cheapest available node) is unchanged — it now reads the corrected closures.
Switch: `research.json tuning.aiCheapestOrRoute` (absent = the ADR-033 B union exactly); twin `TestConfigs.PreAiRoutes`;
every older research twin strips it (the R4 rule, `UndoTrackCp`; the pre-tranche fixture never had it).

**Measured (seeds 1, 2, 42, 7; canonical AI world; `TaxationGateTraceMeasurement.CheapestRouteFix_FourSeeds`, 900 turns).**

| Seed | Config | Taxation | road_paved | Bronze | Age III | First levy | Age IV | Nodes @300/@600/@900 |
|---|---|---|---|---|---|---|---|---|
| 1 | pre-M6 | 328 | 598 | 412 | 413 | 414 | 828 | 17 / 37 / 78 |
| 1 | M6 before fix | 314 | 507 | 569 | 570 | 571 | 873 | 27 / 58 / 102 |
| 1 | **M6 with fix** | 314 | 494 | **401** | **402** | **403** | — | 27 / 53 / 95 |
| 2 | pre-M6 | 339 | 612 | 425 | 426 | 427 | 843 | 17 / 36 / 76 |
| 2 | M6 before fix | 340 | 533 | 597 | 598 | 599 | — | 25 / 53 / 92 |
| 2 | **M6 with fix** | 340 | 519 | **423** | **424** | **425** | — | 25 / 51 / 90 |
| 42 | pre-M6 | 336 | 608 | 421 | 422 | 423 | 834 | 17 / 36 / 77 |
| 42 | M6 before fix | 321 | 516 | 578 | 579 | 580 | — | 26 / 58 / 95 |
| 42 | **M6 with fix** | 321 | 501 | **406** | **407** | **408** | — | 26 / 52 / 93 |
| 7 | pre-M6 | 335 | 603 | 420 | 421 | 422 | 869 | 17 / 36 / 71 |
| 7 | M6 before fix | 321 | 512 | 574 | 575 | 576 | — | 26 / 58 / 96 |
| 7 | **M6 with fix** | 321 | 499 | **406** | **407** | **408** | — | 26 / 52 / 93 |

(— = not reached within 900 turns.) Bronze, Age III and the first levy return to the pre-M6 timing or earlier on all
four seeds (−152 to −174 turns against the unfixed build); the paved road also comes earlier (−13 to −15). Fewer nodes
are completed by turns 600/900 than before the fix (the redundant OR branches are no longer researched) and still
≈ 25 % more than pre-M6. The table was re-measured on the final review tree (the Prospect bill included): identical.
**Open — Age IV (traced, `audit/taxation-gate-trace.md` §7):** Age IV is not reached within 900 turns in M6 with or without
the fix, and the fix costs seed 1 its Age IV (873 → not reached). Age IV binds on its SUPPORTING milestones (3 of 7 from 3
categories), which the AI's goals have never targeted; the union closure met **Alphabetic script** (`abjad`) by
researching every script alternative, and the cheapest route no longer does. No prerequisite is dropped; the gap is the
AI's Age goal. A supporting-milestone goal is proposed in `docs/queue.md`, not implemented (policy extension; every AI pin
would move again).

**Tests.** `AiResearchRouteTests` (rig graph built so union, first-written, lowest-key and cheapest-single-node each give a
different answer): cheapest alternative; tie to the first-written; AND keeps the expensive required side; nested and
partly completed (the choice flips as costs fall); satisfied OR adds nothing; chains costed whole; shared ancestors once;
unresolved alternatives; the switch-off twin is the union exactly on the shipped content; and the PROPERTY along the
canonical AI run — every goal closure with the completed set satisfies every closure node's prerequisite (no legitimate
prerequisite dropped) and holds no completed node.

## 2. The Prospect bill — grain, timber, stone; an assay that spends bronze

**Audit (MEASURED, `Sim.Tests/Kernel/ProspectEconomyMeasurement`, seeds 1, 2, 42, 7).** Goods: grain, livestock, fish,
timber, stone, clay, copper-ore, tin-ore, fiber, hides, bronze, tools, pottery, cloth — the only metals are bronze
(cast from copper-ore and tin-ore, gated by `tin_bronze`) and tools (from bronze). The founding store is grain only;
timber and stone come from extraction at every settlement (12 of 12 hold both deposits on each seed). Empire purse:
grain 2,467–3,275 at turn 1; timber 701–1,029 at turn 1, 18,000+ by turn 20; stone 2–24 at turn 1, 8–65 at turn 5,
84–315 by turn 20; **bronze and tools 0 in every polity through turn 400**.

**Decision.**
- **Survey** (Prospect, order Amount 0; `sim.json discovery.prospectCost`): **40 grain, 20 timber, 10 stone** — open
  from the founding; affordable within the first few turns (stone binds). The loader **refuses** a survey bill that names
  a concealable good or any good a recipe makes from one (`SimConfigLoader.ConcealedLine`: copper-ore, tin-ore, bronze,
  tools) — the survey can never wait on the deposits it exists to find.
- **Assay** (Prospect, order Amount 1; `discovery.assayCost` + `discovery.assayRequires`): **20 grain, 10 timber, 5 stone,
  2 bronze**, offered once the polity knows **`ore_prospecting`**. Bronze is the real metal sink; no iron or new metal
  category was invented. The assay is an improvement, never a gate: the survey stays open whatever the polity's bronze.
- The grain-only cost and its `copper_smelting` halving (ADR-044 §4) are **superseded** by these two bills.

**Rules (unchanged mechanics, generalized to a bill).** Every good of the bill must be covered from the empire purse
beyond construction's set-asides, at the preview (UI, AI: `DiscoveryQuery.CheckProspect` on the published world) and
again at delivery (DiscoverySystem on the step's own stocks); otherwise **nothing of any good is paid**. One Prospect per
(polity, settlement, turn) whatever its method — a duplicate, or a survey and an assay of the same settlement in one
turn, is refused after the first (no repeat charge); once surveyed, never again. Each good is sunk under
`ReasonIds.Prospecting` through `EmpireStockpile.Draw`; the Observer's per-good accounts carry it (`GoodAccount.Discovery`)
and reconcile. AI: the assay where open and affordable, else the survey (`AiDiscoveryPolicy`).

**§2.4 Order contract.** `OrderKind.Prospect` (15): Amount was "reserved, exactly 0"; it is now the **method — exactly 0
(survey) or exactly 1 (assay)**, anything else refused at load (`OrderLog.ValidateRecord`) and at delivery. Every existing
log (Amount 0) means what it meant: a survey. Explore's Amount stays exactly 0.

**UI.** The Actions menu lists, per settlement, Explore, Prospect and Assay, each with its full bill ("survey, cost 40
grain, 20 timber, 10 stone"); a short purse names each short good and by how much ("not enough in store (8 stone
short)"); the locked assay says "needs Ore prospecting"; a refusal at delivery is explained with the whole bill.

**Tests.** `DiscoveryTests`: the bills from sim.json; turn-exact delivery with every good sunk exactly and every Observer
account reconciling; for EACH good one unit short → refused, nothing paid, state = the orderless step; exactly the bill →
paid to the unit; duplicates and survey+assay in one turn charge once; assay locked → refused; unlocked without bronze →
assay unaffordable while the survey stays open; unlocked with bronze → bronze sunk; the loader's deadlock refusal for
copper-ore, tin-ore, bronze and tools; AI method choice. UI: costs, blockers, the Assay button and its guarded emitter.

## 3. Explore's spatial units (audit; wording fixed, model unchanged)

**MEASURED in code.** `fog.seedRadiusNodes` 3, `exploreStepNodes` 3, `exploreMaxRadiusNodes` 15 are counts of
**knowledge-lattice steps** (`PolityKnowledge.Grid` = `MapTopology.LatticeGrid(size, TraversalLattice.DefaultStride 4)`):
on the canonical 1,024 px map, 256 × 256 nodes of 4 px × 4 km/px = **16 km**. A disc is **Euclidean over node centres**
(`DiscoveryQuery.InDisc`: dx² + dy² ≤ r²), wrapped east–west on the cylinder (`MapTopology.AbsDx`), clipped at the poles.
So: seed 48 km, each Explore +48 km, cap **15 × 16 = 240 km** (709 nodes ≈ 181,000 km²) — straight-line km over ideal
ground, **not** a travel cost (no terrain, road or river enters; pathfinding cost units are a different quantity).

**Assessment.** 240 km is the cap the ADR-044 design states ("about 240 km"); it is five expeditions out from a settlement
and roughly the scale of a few weeks' travel on foot — intended as the limit of settlement-based reconnaissance before
M7's mobile units. Kept.

**Changes.** The UI now states km: "range 48 km → 96 km, cost 60 grain"; "explored to the limit (240 km around it)"
(`DiscoveryQuery.RangeText` through `LatticeGeometry.NodeStepsKm`, the CR-002 chokepoint). **Defect found (documentation
only):** `fog.formationSightNodes 1` was described in sim.json as "its 8-neighbourhood"; the Euclidean disc of radius 1 is
the node and its **4** orthogonal neighbours, which is what the code has always done. The text is corrected; behaviour
unchanged. No distance or pathfinding contract changed. Tests: `DiscoveryRangeTests` (16 km unit, 48/48/240 km, the
Euclidean rim, 709-node disc, wrap at the seam on a cylinder and not on a flat map, polar clipping, the cap and the km
wording).

## 4. Educational text in the game UI

Per the instruction: the research detail panel ("IN HISTORY") and the research node's info card show the node's
educational text as plain prose — no citation, source badge, verification label or bibliography. The display text is
generated into `Sim.Data/content/knowledge-texts.json` (`docs/m6-plan/knowledge-redesign/tools/export_runtime_texts.py`,
`--check` for freshness); only id and text cross; provenance and evidence status stay in the repository corpus
(`corpus/*.json`, `sources/register.json`, `corpus/unresolved-claims.md`) for internal quality control. **Showing a text
does not mark it fact-checked**: the corpus statuses are unchanged (0 of 561 source-checked). The simulation never reads
the file. 561 texts; the two runtime nodes without a corpus entry (`siege_tower`, `crossbow_repeating`, merges deferred
by ADR-045) show none. Tests: every text present, 40–70 words, no citation/verification marker; the detail model carries
it; the painted card shows it.

## 5. Goldens, moved pins and their attribution (MEASURED 2026-10-23 on this tree)

Attribution twin for §1: `TestConfigs.PreAiRoutes` (research.json `tuning.aiCheapestOrRoute` removed; everything else,
the Prospect bill included, as shipped). By the R4 rule every OLDER twin strips §1 and the assay: `UndoTrackCp`,
`PreTreeTranche` (its content no longer copies the switch, and `TestConfigs.NoAssay` drops the assay, whose
`ore_prospecting` is a tranche node) and `PreM6`. The pre-M6 controls reproduce v34 unchanged.

| Pin | Before | After | Cause | Control |
|---|---|---|---|---|
| Founded t300 (= `ci.yml FOUNDED_GOLDEN`), toy t200, driven t300, first reign t40 | unchanged | unchanged | — (no AI; no player order prospects) | full suite |
| AI world t300 (`AiFoundingAttributionTests.S1cTurn300`) | `776d5d0c…` | `2e4759ac…` | §2 alone: the AI's surveys spend timber and stone | `AiWorldTurn300_TheCheapestRouteMovesNothingByTurn300`: the routes-off twin gives the new hash byte for byte |
| Canonical AI, first build | 2 | 3 | §2 alone (the AI's first survey takes the stone its first granary waited on) | routes-off twin: 3 |
| Canonical AI, first levy / A3 | 579/580, 579 | **407/408, 407** | §1 alone | routes-off twin: 579/580, 579 |
| Canonical AI, foundings before the levy | 10 (to 509), 16 settlements | 8 (to 367), 14 | §1: the run ends at the earlier levy; cadence unchanged | routes-off twin: 10, 16 |
| Small CLI world (256 px, 4 settlements): AI orders at 7 / 14 | granary / granary | survey / granary | §2 alone | `RunFromSaveCliTests` (re-pinned; the double-delivery proof moved to the turn-14 granary) |
| `ci.yml` AI-empire leg (600 turns) | 400 orders; first Prospect at 0 | 394; first at 1; all surveys | §2 | run locally: two processes, replay and inspect agree |

**Measured consequence worth knowing (§2).** Stone is the scarce early good (2–24 in an Empire's purse at turn 1), and the
AI surveys as soon as a survey is affordable, so on small worlds its surveys can delay a granary by several turns (the
256 px world: its turn-7 granary became a survey, the granary came at 14). On the canonical map the first granary moves one
turn. No AI policy was changed to hide it: a survey-versus-build priority is an AI-policy question for the Director
(`docs/queue.md`). The AI never assays in the 600-turn CI run (it has surveyed every settlement before it holds bronze and
`ore_prospecting`); the assay path is exercised by the tests, the UI and the player.
