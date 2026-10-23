# Taxation gate trace — what gates the tax edict, and what delays the first levy

Director review item 2 (2026-10-23): *"Trace the actual prerequisite closure, research-stage expression, Age facts and
runtime gate. Record whether the brief is wrong, the code differs from the proposal, or the validator is inspecting the
wrong relationship. Do not change the rule just to make the validator pass."* No rule was changed by this trace.

Labels: **READ** (quoted from the tree at the commit carrying this file) · **MEASURED** (seed 42, canonical AI world,
`Sim.Tests/Kernel/TaxationGateTraceMeasurement.cs`, skipped by default, run explicitly) · **DERIVED** (follows from
READ rules plus MEASURED turns; not separately instrumented).

## 1. The four relationships (READ)

| Relationship | Where | What it says about taxation |
|---|---|---|
| Prerequisite closure | `Sim.Data/content/research.json` civic `taxation` (key 1007, cost 1050, Age A3) | `token_counting AND stamp_seal AND (proto_writing OR hieroglyphic OR chinese_script)` — identical in `civics-tree-proposal.json` (key 1007, same expression, same cost) |
| Research-stage expression | `research.json` `researchStage.requires` | `medicine_hippocratic AND (geometry_axiomatic OR algebra) AND stamp_seal` — taxation is **not** in it (it never was; D-062 R5 removed only `legal_code_roman`) |
| Age facts | `Sim.Data/content/ages.json` | **no** Age milestone names taxation; Age III core is `a3_bronze` (`arsenical_bronze OR tin_bronze`) |
| Runtime gate | `sim.json` `governance.taxationRequires: "taxation"` (:268) and `taxationMinAge: 3` (:270); `Sim.Core/State/Governance.cs` `CanLevyTax` (:251) / `GateOf` (:262) | the tax edict is operational only when the polity **knows the Taxation civic AND its current Age ≥ III** (Inert → NeedsKnowledge → NeedsAge → NeedsSeat → Open) |

## 2. Verdict on the three hypotheses

| Hypothesis | Finding |
|---|---|
| The brief is wrong | **No.** "Taxation gates the live tax edict together with an Age III rule" is exactly the runtime gate above. |
| The code differs from the proposal | **No.** The civic's key, prerequisite expression and cost are identical in `research.json` and the proposal JSON; the M6 slice did not touch the edict. |
| The validator inspects the wrong relationship | **Yes, originally.** The package validator read only `research.json`/`ages.json` relationships (stage expression, Age core facts). The edict's gate lives in `sim.json`, and the Age III half is an edict rule, not a node prerequisite and not an Age fact — so the validator could not see that taxation's closure is mandatory. The 2026-10-22 patch hard-coded `{"taxation", "library", "legal_code_roman"}` in `validate.py` V24 and `integrate.py`; this review replaces the literals with values read from `sim.json` `governance.taxationRequires` and `research.json` `inst.university.requires`, so a content change cannot silently desynchronize them (adversarial fixtures: `tools/test_validate.py`). |

## 3. What actually delays the first levy (MEASURED)

The earlier report (`m6-validation-report.md` §4 and `implementation-roadmap.md` priority 1, both before this trace)
attributed the move of the AI's first levy from turn 422 to 579 to "the CP economy pacing `taxation`". **That
attribution was wrong** and is corrected in both files.

| Configuration | Taxation civic | `law_code` | Bronze node | A3 eligible | A3 entered | First levy |
|---|---|---|---|---|---|---|
| M6 (shipped) | 321 | 365 | 578 | 578 | 579 | 580 |
| discovery off | 300 | 336 | 542 | 542 | 543 | 544 |
| CP off (stage restored) | 325 | — | 424 | 424 | 425 | 426 |
| tree tranche off | 344 | 380 | 466 | 466 | 467 | 468 |
| discovery + CP off | 310 | — | 401 | 401 | 402 | 403 |
| discovery + tree off | 327 | 360 | 439 | 439 | 440 | 441 |
| pre-M6 (all off) | 336 | — | 421 | 421 | 422 | 423 |

(First levy = first turn a `TaxPolicies` row of the AI carries a positive rate; it is A3 entry + 1 in every row,
consistent with an order applying the turn after it is issued. The 422/579 in the earlier report were A3-entry turns.)

- The **Taxation civic completes earlier under M6** (321) than before (336). Knowledge is never the binding half.
- In every configuration **the levy follows Age III entry**, and Age III entry follows the turn the AI completes its
  bronze node (the `a3_bronze` core; the supporting milestones are met long before — M6: artisans 112, population 163,
  writing 247, law 365).
- The bronze node moves 421 → 578. Removing one layer from the shipped M6 world: CP lanes off → 424 (−154); tree
  tranche off → 466 (−112); discovery off → 542 (−36). The effects are not additive: with CP off, the tranche is
  neutral to faster (discovery + CP off, tranche on: 401 vs pre-M6 421). The CP layer is the largest contributor, but
  not through taxation.

### 3.1 The mechanism (MEASURED order, DERIVED cause)

Technology completions before the bronze node, discovery off in both runs (the CP-on run also lists the goal its
Technology lane worked, from `AiResearchPolicy.Goals` restricted to the lane's tree and `ChooseGoal`):

| | CP on (two lanes, ADR-043) | CP off (one pool) |
|---|---|---|
| through turn 299 | identical technology sequence ending `stamp_seal@299` | identical |
| next | tax goal still active (civic still funding) → `hieroglyphic@334` | `taxation@310` (the civic itself, on the one pool) |
| then | goal **road** from 334: `calendar_lunar@340 surveying@363 cuneiform@392 chinese_script@419 mesoamerican_script@444 road_paved@483` | goal **core**: `ochre_processing@313 copper_native@322 mudbrick@333 kiln_updraft@371 copper_smelting@392 arsenical_bronze@401` |
| then | goal **core** from 483: `ochre_processing@485 … copper_smelting@536 arsenical_bronze@542` | — |

DERIVED cause, from the policy's own rule (`AiResearchPolicy` header; ADR-033 B, ADR-043):

1. Goal closures are prerequisite ancestors **with union over OR**. The tax goal's closure therefore contains every
   script in `(proto_writing OR hieroglyphic OR chinese_script)`, although `proto_writing@261` already satisfied it.
   In the two-lane model the civic is funded by CP on its own lane, so the Technology lane keeps working the tax
   goal's technology closure while the civic finishes and picks `hieroglyphic` (3,520 RP), which the requirement does
   not need. In the one-pool model the cheapest node of that goal was the civic itself (1,050), so this does not
   happen.
2. Completing `hieroglyphic` lowers the remaining cost of the **paved-road** goal (`surveying` needs
   `cuneiform OR hieroglyphic OR chinese_script OR mesoamerican_script`, again unioned) below the bronze core's. The
   Technology lane then spends turns 334–483 on the paved-road closure, including three more scripts that the OR does
   not need, before it returns to bronze.

So the delay is an **AI goal-ordering interaction** between the pre-existing union-over-OR closure rule and the new
two-lane funding. It is not caused by the CP rate, the taxation civic or the edict's gate. The tranche's −112 and
discovery's −36 (removal effects in the shipped world) were not traced to a mechanism here; that the tranche delays
bronze only when two lanes are on is consistent with it changing the goal costs this comparison reads, but that is
not measured.

## 4. What this trace does not change

- No rule, cost, goal or gate was edited. The finding is recorded as a queue line (`docs/queue.md`) for the AI research
  policy: count an OR group's closure through its cheapest unmet branch, or drop branches once the OR is satisfied.
  That is an AI-policy design change, out of this review's scope.
- Single seed (42), single AI world. Not a balance study.

## 5. Reproduce

Remove the `Skip` from the two facts in `Sim.Tests/Kernel/TaxationGateTraceMeasurement.cs`, then
`dotnet test Sim.Tests -c Release --filter "FullyQualifiedName~TaxationGateTraceMeasurement" --logger "console;verbosity=detailed"`
(≈ 5 min + 1.5 min on this container).
