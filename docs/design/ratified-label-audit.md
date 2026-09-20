# RATIFIED-LABEL AUDIT — every residual `RATIFIED (…)` parenthetical traced to its actual source

**Directed packet, 2026-09-20.** The Director authorised exactly this: *"For every residual RATIFIED
parenthetical: trace its actual source. IF genuinely ratified: retain RATIFIED. IF only established
by implementation/evidence: relabel appropriately. IF genuinely ambiguous: DIRECTOR DECISION
REQUIRED. IF unable to verify: UNVERIFIED."* with the standing constraints *"Do not change any
simulation behavior. Do not change any specification. Do not reinterpret a decision."*

**Tree.** Branch `claude/civdemo-work-b1z2y4`, parent commit `f156626`, working tree clean before
this packet. `main` untouched at `dbef61a`. Nothing merged.

**Scope compliance, MEASURED (this packet, by `git diff`).** Files touched: the 14 design documents
below plus this new file. Production code, schema, tests, data, config, corridors, CI, `CLAUDE.md`,
`docs/adr/` and every specification: **zero bytes changed**. Every one of the 86 edits is a
single substitution of the leading label token on one line; the parenthetical text, the claim, the
quotation and the source citation are byte-identical. Verified mechanically: 86 changed lines, 86
expected, 0 lines differing in anything but the label token.

## 1. THE COUNT

| | |
| --- | --- |
| `RATIFIED (…)` occurrences found | **203** (not ~201 as previously estimated) |
| **RETAINED as RATIFIED** | **117** occurrences — of which **115** are claims traced to a ruling or a frozen document, and **2** are label-key legend lines, not claims (`arch-N-milestone-allocation.md:16`, `arch-O-invariants.md:15`) |
| **Relabelled MEASURED** — established only by implementation or by an evidence record | **77** |
| **Relabelled UNVERIFIED** — the ratification could not be verified against a primary record | **6** |
| **Relabelled DIRECTOR DECISION REQUIRED** — genuinely ambiguous | **3** |
| relabelled, total | **86** |

## 2. THE TRACING RULE APPLIED

The design documents' own label key reads `RATIFIED (cite file:line)`. The **source cell decides**,
never the parenthetical's own wording — a parenthetical that says "shipped contract" or "director
ruling" is a claim about status, not evidence for it.

- **RETAIN RATIFIED** — the cited source is a ruling or a frozen document: `CLAUDE.md` (the laws),
  `docs/civ-sim-architecture-v3-outline.md` (the Spine), `docs/spine-s8-governance-freeze.md`, a
  closed D-decision (`d009`/`d010`/`d011`/`d018`/`d020`/`d021`/`d035`/`d037`/`d040`/`d041`/`d042`),
  a closed milestone spec, `docs/observability-architecture.md`, an ACCEPTED ADR on `main`, a ruled
  CR, or `docs/m4-pre-spec-dependencies.md` §1/§6 (which records director rulings **first recorded
  there** — verified at `:20-41`).
- **MEASURED** — the only source is code, data, a script, CI, a measurement record, an investigation
  record, an inventory, a packet architecture document, or a **finding** (including a finding
  recorded inside a ruled CR: `cr-015` §2 is headed "EVIDENCE", not ruling).
- **UNVERIFIED** — the row asserts a ruling or a certification whose only attestation is a code
  `_doc` comment or `docs/queue.md` (secondary evidence under GOV-4). Whether those attest a ruling
  is not something this packet may decide, and no primary ruling record was found.
- **DIRECTOR DECISION REQUIRED** — the source is an explicitly **overridable orchestrator decision**
  taken under a delegated ownership grant, or an **OPEN** CR.

## 3. WHAT DECIDED THE FOUR HARD CLASSES

**3a. `cr-015` is structurally decisive, and its own headings settle it.** §1 FROZEN ITEMS IN
CONFLICT · §2 EVIDENCE · §3 OPTIONS · §4 BLAST RADIUS · §5 RECOMMENDATION · **§6 DIRECTOR RULING**
(§6.1 the ruling, §6.2 amendments) · **§6.3 orchestrator decisions under the mandate's ownership
grant** · §6.4 ESCALATED (G1). A citation into §6.1/§6.2 is a ruling and RETAINS. A citation into §2
is a finding and became MEASURED (`recovered-decisions-food-disaster-needs.md:304`, `:305`). A
citation into §6.3 became DIRECTOR DECISION REQUIRED — `cr-015:482-484` states in terms that *"the
director may override"*.

**3b. The unmerged-ADR provenance, and why it did NOT drive relabelling.** MEASURED (`git ls-tree`):
`adr-022`…`adr-026`, `cr-013`…`cr-016` exist **only on this branch** — they are not on `main`, which
`CLAUDE.md` calls accepted truth and GOV-4 distinguishes from LOCAL and REMOTE. This packet did not
relabel a site merely for citing one, because the underlying decisions in `adr-024`/`adr-025` are
the CR-015 ruling's own content. **It is surfaced as an open question, not resolved** — see §5 Q-A.

**3c. Rulings attested only in a code `_doc`.** Six sites assert a director ruling or a director
certification whose only citation is a `.cs` file's doc comment, or `docs/queue.md` which the row
itself flags as *"secondary evidence under GOV-4"*. One row goes further and states the premise
outright — `recovered-decisions-knowledge-tech.md:54`, *"code \`_doc\` counts as a decision record"*.
That premise is unruled. All six became **UNVERIFIED**.

**3d. Cross-references (`RATIFIED (F15)`, `(G30)`, `(secondary F-47)`).** Every arch document carries
its own local findings table, so these all resolve locally; none dangles. Each was traced to the
referenced row's **own source cell**, and the label follows that. This surfaced a defect of its own:
**`arch-I-climate-environment.md` row F26 is itself labelled `RATIFIED` with `Sim.Core/State/WorldState.cs:320-324`
as its only source** — a code fact. The citing site `:585` is relabelled MEASURED here; the row's own
label is outside this packet's authorised scope and is filed as §5 Q-C.

## 4. THE 86 RELABELLED SITES

| site | parenthetical (unchanged) | new label | traced source |
| --- | --- | --- | --- |
| `arch-C-knowledge.md:54` | (shipped contract) | **MEASURED** | `Sim.Core/Systems/ClassMobility/Predicate.cs:10-27` |
| `arch-C-knowledge.md:55` | (shipped) | **MEASURED** | `Sim.Core/Systems/ClassMobility/ClassMobilitySystem.cs:28-33`; row `Sim.Core/State/WorldState.cs:403` |
| `arch-C-knowledge.md:56` | (shipped data + code) | **MEASURED** | `Sim.Data/content/goods.json:3`; `Sim.Core/Systems/Production/ProductionSystem.cs:388-396`; live gates `goods.json:156`, `:175` (`"requires": "artisan_share > 0.05"`) |
| `arch-C-knowledge.md:58` | (shipped law, stated in code) | **MEASURED** | `Sim.Core/State/Variables.cs:24-32` |
| `arch-C-knowledge.md:59` | (shipped) | **MEASURED** | `Sim.Core/Systems/ClassMobility/Predicate.cs:22-25`; `Sim.Core/State/WorldState.cs:388-394` |
| `arch-C-knowledge.md:72` | (director ruling, shipped) | **UNVERIFIED** | `Sim.Core/State/SettlementHappiness.cs:6-35` |
| `arch-E-breakthrough-domains.md:69` | (shipped contract; closed at `docs/m2-spec.md:8`) | **MEASURED** | `Sim.Core/Systems/ClassMobility/Predicate.cs:10-27` |
| `arch-E-breakthrough-domains.md:70` | (shipped data + code) | **MEASURED** | `Sim.Data/content/goods.json:3`, `:156`, `:175`; `Sim.Core/Systems/Production/ProductionSystem.cs:394-396` |
| `arch-E-breakthrough-domains.md:72` | (shipped law, stated in code) | **MEASURED** | `Sim.Core/State/Variables.cs:24-32` |
| `arch-E-breakthrough-domains.md:74` | (shipped) | **MEASURED** | `Sim.Core/Systems/ClassMobility/ClassMobilitySystem.cs:28-33`; correction `docs/milestone-architecture-governance.md:242-251` |
| `arch-E-breakthrough-domains.md:75` | (shipped, under CR-015/ADR-024) | **MEASURED** | `Sim.Core/Systems/Disaster/DisasterSystem.cs:9-11`, `:34`, `:56-60` |
| `arch-E-breakthrough-domains.md:79` | (director ruling, shipped) | **UNVERIFIED** | `Sim.Core/State/SettlementHappiness.cs:6-35`; gate `scripts/check-read-isolation.sh:1-25` |
| `arch-E-breakthrough-domains.md:81` | (shipped) | **MEASURED** | `Sim.Core/Systems/Production/ProductionSystem.cs:35-44`, `:227-233`, `:264-271` |
| `arch-E-breakthrough-domains.md:82` | (shipped) | **MEASURED** | `Sim.Core/Systems/Production/ProductionSystem.cs:58-72` |
| `arch-E-breakthrough-domains.md:83` | (shipped, M4-D) | **MEASURED** | `Sim.Core/Systems/Construction/ConstructionSystem.cs:37-48` |
| `arch-E-breakthrough-domains.md:160` | (F15) | **MEASURED** | `FoodState` |
| `arch-E-breakthrough-domains.md:161` | (F15) | **MEASURED** | `FoodState.EffectiveDeficit(d, state, cfg)` |
| `arch-FGH-civics-institutions-diffusion.md:92` | (shipped, director ruling) | **UNVERIFIED** | `Sim.Core/State/SettlementHappiness.cs:6-38`; gate at `scripts/check-read-isolation.sh` |
| `arch-FGH-civics-institutions-diffusion.md:100` | (shipped) | **MEASURED** | `Sim.Core/Systems/ClassMobility/ClassMobilitySystem.cs:28-33`; correction `docs/milestone-architecture-governance.md:242-251` |
| `arch-FGH-civics-institutions-diffusion.md:101` | (shipped) | **MEASURED** | `Sim.Data/content/goods.json:3`, `:136`, `:156`, `:159`, `:175`; `Sim.Core/Systems/Production/ProductionSystem.cs:394-396` |
| `arch-FGH-civics-institutions-diffusion.md:104` | (shipped) | **MEASURED** | `Sim.Core/Systems/Migration/MigrationSystem.cs:20-21`; `Sim.Core/State/WorldState.cs:110-125`; `docs/adr/adr-025-bounded-migration-exit-openness-basin-caps-vacancy.md:1-10` |
| `arch-FGH-civics-institutions-diffusion.md:105` | (shipped) | **MEASURED** | `Sim.Core/Systems/Trade/TradeArbitrageSystem.cs:12-33`; `Sim.Core/State/WorldState.cs:459` |
| `arch-FGH-civics-institutions-diffusion.md:106` | (shipped) | **MEASURED** | `Sim.Core/Systems/Trade/TradeScope.cs:6-45` |
| `arch-FGH-civics-institutions-diffusion.md:107` | (shipped) | **MEASURED** | `Sim.Core/State/NotableLifecycle.cs:5-33`, `:115-134`; `Sim.Core/State/WorldState.cs:620-634` |
| `arch-FGH-civics-institutions-diffusion.md:108` | (shipped) | **MEASURED** | `Sim.Core/Systems/Revolt/RevoltSystem.cs:16-45` |
| `arch-I-climate-environment.md:437` | (F17) | **MEASURED** | `sqrt(ln(1 + CV²))` |
| `arch-I-climate-environment.md:494` | (E-88, E-89, and re-read this pass at `SettlementHappiness.cs:41-49`) | **MEASURED** | `SettlementHappiness.cs:41-49` |
| `arch-I-climate-environment.md:559` | (E-81) | **MEASURED** | — (prose site; traced in §3) |
| `arch-I-climate-environment.md:585` | (F26) | **MEASURED** | — (prose site; traced in §3) |
| `arch-JKL-food-disaster-needs.md:195` | (`ProductionSystem.cs:227-234`, verified this pass) | **MEASURED** | `toolFactor = 1 + 0.3 × equipRatio` |
| `arch-JKL-food-disaster-needs.md:367` | (G35) | **MEASURED** | — (prose site; traced in §3) |
| `arch-JKL-food-disaster-needs.md:584` | (G38) | **MEASURED** | — (prose site; traced in §3) |
| `arch-JKL-food-disaster-needs.md:790` | (G28) | **MEASURED** | — (prose site; traced in §3) |
| `arch-JKL-food-disaster-needs.md:834` | (G27) | **MEASURED** | — (prose site; traced in §3) |
| `arch-M-cross-system-graph.md:333` | (shipped data) | **MEASURED** | — (prose site; traced in §3) |
| `arch-M-cross-system-graph.md:486` | (shipped data) | **MEASURED** | *Source A, MEASURED (shipped data):* `sim.json:43` states the granary constant's carrier as *"a |
| `arch-N-milestone-allocation.md:209` | (a recorded finding) | **MEASURED** | — (prose site; traced in §3) |
| `arch-PQ-conflicts-and-questions.md:252` | (a recorded finding) | **MEASURED** | — (prose site; traced in §3) |
| `density-window-verification.md:71` | (`corridors.json:63`) | **MEASURED** | demography-driven numerator."* MEASURED (`corridors.json:63`). **On the arm measured here that |
| `density-window-verification.md:335` | (quoted verbatim) | **MEASURED** | `.github/workflows/ci.yml` at `9f6c6ae`. MEASURED (quoted verbatim). |
| `m4-closure-audit.md:63` | (criterion) | **MEASURED** | `docs/milestones.md:350` — "AWAITING THE DIRECTOR — the machinery is complete and tested" |
| `m4-closure-audit.md:587` | (D4) | **MEASURED** | — (prose site; traced in §3) |
| `recovered-decisions-civics-institutions.md:249` | (shipped contract) | **MEASURED** | `Sim.Core/State/WorldState.cs:579-589` |
| `recovered-decisions-civics-institutions.md:250` | (shipped contract) | **MEASURED** | `Sim.Core/State/WorldState.cs:591-608` |
| `recovered-decisions-civics-institutions.md:251` | (shipped contract) | **MEASURED** | `Sim.Core/State/WorldState.cs:659-669` |
| `recovered-decisions-civics-institutions.md:252` | (shipped contract) | **MEASURED** | `Sim.Core/State/WorldState.cs:671-684` |
| `recovered-decisions-civics-institutions.md:253` | (shipped contract) | **MEASURED** | `Sim.Core/State/WorldState.cs:686-704` |
| `recovered-decisions-civics-institutions.md:254` | (shipped contract) | **MEASURED** | `Sim.Core/State/WorldState.cs:706-724` |
| `recovered-decisions-civics-institutions.md:255` | (shipped contract) | **MEASURED** | `Sim.Core/State/EmpireQuery.cs:3-14`; extinction `:36-40` |
| `recovered-decisions-civics-institutions.md:257` | (shipped) | **MEASURED** | `Sim.Core/Worldgen/WorldFounding.cs:254-295`; default `Sim.Core/Worldgen/WorldgenConfig.cs:55`; verification at 1/4/8/50 recorded `docs/m4-exit-inventory.md:54` |
| `recovered-decisions-civics-institutions.md:258` | (shipped) | **MEASURED** | `Sim.Core/Worldgen/WorldFounding.cs:297-304` |
| `recovered-decisions-civics-institutions.md:259` | (shipped) | **MEASURED** | Still deliberately deferred and OPEN: *"an explicit order **scope** field, a permission matrix"* (`docs/queue.md:1288-1290`). A future governance layer needs both. |
| `recovered-decisions-civics-institutions.md:260` | (shipped M4) | **MEASURED** | `Sim.Core/Systems/Revolt/RevoltSystem.cs:16-32` |
| `recovered-decisions-civics-institutions.md:261` | (shipped) | **MEASURED** | `Sim.Core/Systems/Revolt/RevoltSystem.cs:34-39` |
| `recovered-decisions-civics-institutions.md:263` | (shipped, with the stated non-collision argument) | **MEASURED** | `Sim.Core/Systems/Revolt/RevoltSystem.cs:6-11` |
| `recovered-decisions-civics-institutions.md:264` | (shipped; a director ruling) | **UNVERIFIED** | `Sim.Core/State/SettlementHappiness.cs:6-25` (T4.13, director ruling) |
| `recovered-decisions-civics-institutions.md:265` | (shipped) | **MEASURED** | `Sim.Core/State/SettlementHappiness.cs:27-38` |
| `recovered-decisions-civics-institutions.md:266` | (shipped, stated as a deliberate absence) | **MEASURED** | `Sim.Core/State/SettlementHappiness.cs:40-49` |
| `recovered-decisions-civics-institutions.md:267` | (shipped, director-certified) | **UNVERIFIED** | `docs/queue.md:1313-1316` (a queue record — secondary evidence under GOV-4; the query it names exists at `Sim.Core/State/EmpireQuery.cs` and is called at `Sim.Core/Worldgen/WorldFounding.cs:306`) |
| `recovered-decisions-civics-institutions.md:268` | (shipped law, stated in code) | **MEASURED** | `Sim.Core/State/Variables.cs:24-32` |
| `recovered-decisions-civics-institutions.md:270` | (M4 exit record) | **MEASURED** | `docs/m4-exit-inventory.md:58`, `:114`, `:196`, `:323` |
| `recovered-decisions-civics-institutions.md:360` | (a recorded finding; the Spine table *"was never patched"*) | **MEASURED** | `docs/m4-pre-spec-dependencies.md:143-160` |
| `recovered-decisions-civics-institutions.md:362` | (record) | **MEASURED** | `docs/m4-pre-spec-dependencies.md:43-72` |
| `recovered-decisions-civics-institutions.md:372` | (measured blast radius) | **MEASURED** | `docs/adr/cr-005-m5-research-technology-institutions-placement.md:122-133` |
| `recovered-decisions-climate-env-agri.md:171` | (measured, director commendation) | **MEASURED** | `adr-013:275-278` |
| `recovered-decisions-climate-env-agri.md:175` | (with the rejected first implementation recorded) | **MEASURED** | `Sim.Core/Systems/Colonization/ColonizationSystem.cs:44-60` |
| `recovered-decisions-food-disaster-needs.md:95` | (shipped) | **MEASURED** | `docs/d021-stability-doctrine.md:33`; implemented `Sim.Core/Systems/NeedsGrievance/NeedsGrievanceSystem.cs:66-72` |
| `recovered-decisions-food-disaster-needs.md:114` | (shipped) | **MEASURED** | `Sim.Core/Systems/NeedsGrievance/NeedsGrievanceSystem.cs:47-52` |
| `recovered-decisions-food-disaster-needs.md:161` | (the recommendation was NO CHANGE) | **MEASURED** | `docs/m4-d035a-calibration-measurement.md:22-32` (measurement only; *"No production change, no golden change, no code change"*, `:3-4`) |
| `recovered-decisions-food-disaster-needs.md:233` | (classification of record) | **MEASURED** | `docs/t4.20-food-semantics.md:135-137` |
| `recovered-decisions-food-disaster-needs.md:253` | (closed) | **MEASURED** | `Sim.Core/Systems/Consumption/ConsumptionSystem.cs:260-262`; raised as §7.2 of `docs/food-anomaly-investigation.md` |
| `recovered-decisions-food-disaster-needs.md:261` | (classification of record) | **MEASURED** | `docs/food-anomaly-investigation.md:616-624` |
| `recovered-decisions-food-disaster-needs.md:289` | (orchestrator decision, overridable) | **DIRECTOR DECISION REQUIRED** | `docs/adr/cr-015-famine-is-exceptional.md:490-495` (orchestrator decision under the mandate's ownership grant, *"the director may override"*, `:482-484`) |
| `recovered-decisions-food-disaster-needs.md:298` | (orchestrator decision) | **DIRECTOR DECISION REQUIRED** | DIRECTOR DECISION REQUIRED (orchestrator decision) — *"This decision does not resolve this CR"* (`cr-016:176-177`) |
| `recovered-decisions-food-disaster-needs.md:304` | (declared as a finding) | **MEASURED** | `docs/adr/cr-015-famine-is-exceptional.md:275-279`; applied `docs/t4.2-review-record.md:390` |
| `recovered-decisions-food-disaster-needs.md:305` | (a finding) | **MEASURED** | `docs/adr/cr-015-famine-is-exceptional.md:280-282` |
| `recovered-decisions-food-disaster-needs.md:310` | (scoping of the open question) | **DIRECTOR DECISION REQUIRED** | `docs/adr/cr-016-armed-disaster-fallout.md:112-126` |
| `recovered-decisions-food-disaster-needs.md:341` | (measured at both values, recorded at the site) | **MEASURED** | `docs/adr/cr-015-famine-is-exceptional.md:697-699, :692-696` |
| `recovered-decisions-knowledge-tech.md:54` | (shipped contract; code `_doc` counts as a decision record) | **UNVERIFIED** | `Sim.Core/Systems/ClassMobility/Predicate.cs:10-27` |
| `recovered-decisions-knowledge-tech.md:55` | (shipped contract) | **MEASURED** | `Sim.Core/Systems/ClassMobility/Predicate.cs:22-25`; `Sim.Core/State/WorldState.cs:388-394` |
| `recovered-decisions-knowledge-tech.md:56` | (shipped contract) | **MEASURED** | `Sim.Core/State/Variables.cs:3-8` |
| `recovered-decisions-knowledge-tech.md:57` | (shipped law, stated in code) | **MEASURED** | `Sim.Core/State/Variables.cs:24-32` |
| `recovered-decisions-knowledge-tech.md:58` | (shipped contract) | **MEASURED** | `Sim.Core/Systems/ClassMobility/ClassMobilitySystem.cs:28-33`; row `Sim.Core/State/WorldState.cs:396-403` |
| `recovered-decisions-knowledge-tech.md:61` | (shipped, MEASURED from source) | **MEASURED** | `Sim.Core/Systems/Production/ProductionSystem.cs:394-396`; parsed once at construction `:105-108`; validated at load `Sim.Core/Systems/GoodsConfig.cs:241-245` |
| `recovered-decisions-knowledge-tech.md:62` | (shipped data) | **MEASURED** | `Sim.Data/content/goods.json:136` + `:156`, `:159` + `:175` |
| `recovered-decisions-knowledge-tech.md:63` | (shipped data; TUNE values are LIVING under `docs/spine-s8-governance-freeze.md:20`) | **MEASURED** | MEASURED (shipped data; TUNE values are LIVING under `docs/spine-s8-governance-freeze.md:20`) |

## 5. WHAT THIS PACKET WILL NOT DECIDE

- **Q-A.** Does an ADR that exists only on an unmerged branch, authored by an implementing agent
  under a mandate's ownership grant and never director-accepted, carry RATIFIED status? This governs
  `adr-022`…`adr-026` and every design-document row that cites them.
- **Q-B.** Does a director ruling attested only in a code `_doc` comment, or recorded only in
  `docs/queue.md`, count as a ratified record? Six sites are held at UNVERIFIED pending this.
- **Q-C.** Beyond the parentheticals the Director scoped, some **local findings-table rows carry a
  bare `RATIFIED` over a code-only source** — `arch-I` F26 is the proven instance. A sweep of bare
  `RATIFIED` cells was **not authorised by this packet and was not performed**. The count of such
  rows is unmeasured.
- **Q-D.** `recovered-decisions-food-disaster-needs.md:310` cites `cr-016`, which is **OPEN**. It is
  held at DIRECTOR DECISION REQUIRED, consistent with CR-016's own status.

Nothing above is a ruling. No simulation behaviour, specification, or decision was changed or
reinterpreted.
