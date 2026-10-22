# Governance constraints digest — knowledge redesign (technology tree, civics tree, node content, resource discovery, fog of war)

**What this is.** A read-only extraction, from the repository's governance records, of every rule found that constrains a
redesign of the Technology tree, the Civics tree, the educational/descriptive content of nodes, resource discovery and
fog of war. It is the designers' rulebook. **It rules nothing, upgrades no status and edits no record.**

**Tree.** `/home/user/m5w/dev`, branch `claude/new-session-yavghm`, HEAD `edba2b5`; `main` = `m5-exit` = `6e51a3e`
(verified with `git rev-parse`). Every `path:line` below was read on this tree. Content/code facts marked
**FACT@edba2b5** were measured by this digest's author on this tree (scripts in the session scratchpad; nothing built).

**Status labels (never upgraded).**
- **RATIFIED** — a Director ruling, a ratified record, or a frozen document.
- **DIRECTED** — a Director instruction quoted in the record and not yet ruled (today: the 2026-10-21 M6.1 instruction
  quoted in D-061 §1, and the 2026-10-20 instruction items quoted in the planning package).
- **PROPOSED** — an unratified proposal (the draft M6 spec, D-061, the m6-plan registers, the arch-* design lanes,
  ADR implementer resolutions marked "overridable").
- **SUPERSEDED** — replaced; the replacing record is cited.
- **OPEN** — unresolved, with a named owner (a CR, a D-record row, a docket item).
- **FACT@edba2b5** — not a rule: a measured property of code or content that a redesign collides with.

**Two governing facts to keep in mind while reading.**
1. **M6 is not ratified.** "Current milestone: M6 … spec in preparation, NOT ratified; no active packets until
   `docs/m6-spec.md` is ratified" (`CLAUDE.md:10`). Everything in `docs/m6-spec.md`, `docs/m6-plan/*`,
   `docs/d061-civics-points-supersession.md` is PROPOSED, except the dated RULED lines they quote.
2. **The one-RP-pool rule is still in force.** D-061 (Civics Points) is PROPOSED; its ruling block is empty:
   "Until a line is written here, every passage in §2 remains in force as written." (`docs/d061-civics-points-supersession.md:142-143`).

**Source-of-truth order used when records disagree** (`docs/gov-4-repository-freshness.md:19-32`; ledger
`docs/design/director-architecture-and-decision-ledger.md:70-81`): explicit Director rulings → later D-records/addenda →
accepted ADRs → current milestone spec → implementation → tests → older architecture → agent reports. "A conflict is not
yours to dissolve." (`docs/gov-4-repository-freshness.md:34`). Conflicts found are listed in §12, not resolved.

---

## 1. Frozen and settled structure

### 1A. Trees, subtrees, when subtrees open

| # | Rule (verbatim, ≤25 words) | Source | Status | Implication for the redesign |
|---|---|---|---|---|
| S1-01 | "There are exactly TWO top-level research trees:" | `docs/d044-research-progression-rulings.md:109` | RATIFIED; ★ (`d044:789`) | Civics stays Tree 2; no third tree for education, religion, doctrine, industry. |
| S1-02 | "Tree 1 contains exactly FIVE specialized subtrees:" (Military, Medicine, Engineering, Natural Science, Agriculture) | `d044:115-123` | RATIFIED; ★ (`d044:790`) | A deeper Technology tree must fit Main + these five; no new numbered subtree. |
| S1-03 | "Do NOT introduce: 1.3.1 1.3.2 Tree 3 Tree 4 Industry & Energy tree separate Naval tree separate Science tree" | `d044:130-137`; ledger §26 item 22 (`docs/design/director-architecture-and-decision-ledger.md:1134-1135`) | RATIFIED | No sub-subtrees and no domain trees. Industry/energy knowledge sits in Engineering by implementer resolution ADR-029 R-8 (`docs/adr/adr-029-research-engine.md:483`, PROPOSED/overridable). |
| S1-04 | "Internal prerequisite branches inside a tree are allowed, but they are not additional numbered trees." | `d044:139` | RATIFIED | Deep internal branching is legal; it must not be presented or stored as a tree. |
| S1-05 | "There is ONE authoritative Technology graph." / "Do not duplicate nodes between trees." | `d044:462`, `d044:460`; `d044:454` (subtrees are views) | RATIFIED | One node, one record; subtree membership is a node property (`branch`), never a copy. |
| S1-06 | "The specialized branches become available when the civilization reaches the university/research institutional stage." | `d044:145` | RATIFIED; ★ (`d044:796`) | All five subtrees open together, at one stage. |
| S1-07 | "The trigger is NOT simply: Age == X" … "do NOT over-engineer progressive branch opening." | `d044:147-153` | RATIFIED | No Age trigger and no faculty-by-faculty opening; civ-progression §8.4.3: "An Age transition never opens a subtree." (`docs/design/civilization-progression-architecture.md:175`). |
| S1-08 | Stage = "medicine_hippocratic AND (geometry_axiomatic OR algebra) AND stamp_seal AND legal_code_roman" | `Sim.Data/content/research.json:254`; reading `docs/adr/adr-029-research-engine.md:343-360` | FACT@edba2b5 (content is RATIFIED per D-046 F§1, `docs/d046-research-foundation-gate-rulings.md:367`) | A civic atom (`legal_code_roman`) gates all five subtrees (see §5, §7, §12). |
| S1-09 | "researchStage.requires is an AND / OR expression over node ids only." | `Sim.Core/Systems/Research/ResearchContent.cs:506-507` | FACT@edba2b5 | ADR-029 says an institution-present atom can be added "without changing engine code" (`adr-029:359-360`); the loader refuses non-node atoms, so it would need a loader change. |
| S1-10 | "this Main-tree technology cannot be researched before the research stage … Invalid cross-tree dependency." | `ResearchContent.cs:605-608`; validator table `adr-029:173, 181` | FACT@edba2b5 | Any deepened trunk must stay reachable before the stage; no trunk node may need subtree knowledge. |
| S1-11 | "a Civics node carries no 'branch' — Tree 2 has no numbered subtrees (D-044 R3)." | `ResearchContent.cs:430`; `d044:125-126` | RATIFIED + FACT | Micro/macro civics must be a node property or internal branches, not Tree-2 subtrees. |
| S1-12 | "These remain TWO separate actual research trees." / "Civics is NOT a third tree." | `docs/d047-civ6-reference-and-progression-rulings.md:203-205` | RATIFIED | Both trees live under the KNOWLEDGE & TECHNOLOGY lens (`d047:198-201`); adopted civic effects show under INSTITUTIONS (`d047:207`). |
| S1-13 | "These are lenses, not seven independent linear trees." | ledger `:182` (lenses `:172-180`) | RATIFIED | Techniques, Institutions, Infrastructure, Industry, Military, Applications are views, not research trees. |

### 1B. Node model fields

| # | Rule (verbatim, ≤25 words) | Source | Status | Implication |
|---|---|---|---|---|
| S1-14 | "Every Technology node should support:" Identity (ID, Name, Description, Historical period / Age relevance); Research; Unlocks; Progression (family, generation); Effects | `d044:208-237` | RATIFIED | The minimum field set for any redesigned node, Technology or Civics (`d044:367-376` for Civics). |
| S1-15 | "Use stable IDs. … Do not encode the technology graph directly in C#. Technology content must remain data-driven." | `d044:239-243` | RATIFIED | Content edits live in `research.json`; no node id in C# (precedent `docs/r2a-trade-citystates-record.md:18`). |
| S1-16 | Shipped fields: key, id, name, desc, age, frontier, emerged, domain, secondaryDomains, depth, cost, prereq, eurekas, unlocks{…}, family, generation, effects.immediate, repeatable, universityRelevance, costRationale; `branch` on technologies | `Sim.Data/content/research.json` (schema `civ-sim/research@2`, `ResearchContent.cs:283`) | FACT@edba2b5 | A schema change needs a loader change and a content-schema bump; `depth` is derived and a stale value is refused (`adr-029:183`). |
| S1-17 | "`age` — A1..A9, metadata only (R13)." / age F → A9 + `frontier` | `adr-029:65`; `ResearchContent.cs:396-398`; R-11 `adr-029:486` | RATIFIED rule (D-044 R13) + FACT | No tenth Age; frontier nodes are A9 with `frontier: true`. |
| S1-18 | "University relevance in the research corpus is domain classification only." | ledger `:542-543`; `d046:303` | RATIFIED | No numbers per node for universities. |
| S1-19 | Entity kinds: building, infrastructure, institution, unit, activity, project, recipe (prefixes `building.` … `recipe.`) | `ResearchContent.cs:293-295` | FACT@edba2b5 | No government-form, policy or law entity kind exists; adding one is a loader change (see §6, §7). |
| S1-20 | "`effects.immediate` remains empty." | ledger `:1042`; loader rule R-19 `adr-029:494` | RATIFIED | A node never carries its own effect; D-044 R11's "Apply declared immediate effects" (`d044:345`) is vacuous by this rule. |

### 1C. Families and generations

| # | Rule | Source | Status | Implication |
|---|---|---|---|---|
| S1-21 | "Generations are meaningful advancements, not arbitrary repeated levels." | `d044:261` (families may have generations, `:249`) | RATIFIED | Deepening by numbered-level ladders (Tool II, Tool III) is barred unless each level is a real advance. |
| S1-22 | "Do not automatically connect generations unless the actual prerequisite graph says they are prerequisites." | `d044:263` | RATIFIED | Generation is display lineage; ADR-029: "not a dependency rule" (`adr-029:83-85`). |
| S1-23 | "But do NOT manufacture generations merely to create more nodes." | `docs/d045-research-calibration-rulings.md:477` | RATIFIED | Directly limits deepening the tree by generation padding. |
| S1-24 | "Generation metadata must not automatically determine cost." | `d045:479` | RATIFIED | Cost comes from the six factors only (§3). |

### 1D. Repeatable, frontier and speculative sets

| # | Rule | Source | Status | Implication |
|---|---|---|---|---|
| S1-25 | "DEFER recursive/repeat mechanics." / "Keep the 10 repeatable nodes." | `d046:151-153` | RATIFIED | Ten `frontier_*` nodes stay; no repeat levels. |
| S1-26 | "A repeatable becomes available after finite non-repeatable research in its own subtree is exhausted." | `d046:155-158`; ledger `:1022-1023` | RATIFIED | Adding finite nodes to a subtree moves `finite_nodes_to_exhaust` (loader-checked, `ResearchContent.cs:938-940`; precedent `docs/adr/adr-032-transport-inter-city-road-foundation.md:145-146`). |
| S1-27 | "Do NOT redesign:" — repeat cost escalation; repeat effects; repeat-count persistence; diminishing returns; repeatable interaction with Age; repeatable balancing | `d046:160-167`; ledger §22 `:1027-1034` | RATIFIED | Out of scope for any redesign. |
| S1-28 | Tier TR ⇔ recursive set; TS ⇔ `speculative_finite`; a finite node may not require a recursive one; a recursive node must sit in a subtree | `ResearchContent.cs:917-930` | FACT@edba2b5 | New speculative nodes must be listed in `researchSets.speculative_finite` and tiered TS. |
| S1-29 | Sets today: finite 423, recursive 10, speculative_finite 6 | `docs/research-corpus-audit.md:21-23` | FACT@edba2b5 | "The 150-250-turn final-Age target applies to the finite meaningful research corpus." (`d045:491-492`). |

### 1E. The ★ "do not ask again" list (D-044 SETTLED)

| # | Rule | Source | Status | Implication |
|---|---|---|---|---|
| S1-30 | "Do not ask me again:" how many branches · whether Civics shares the research resource · whether universities reduce cost · when branches appear · whether technology equals knowledge · whether partial progress is retained · whether Technology is linear · whether there are five specialized branches | `d044:750-772` | RATIFIED | An agent may not re-ask any of these. |
| S1-31 | ★ rows: two trees; exactly five subtrees; not linear; no separate Knowledge resource/tree; one shared pool; subtrees open together at the stage; universities reduce EffectiveCost | `d044:789-797` | RATIFIED | Unmarked rows (single target, completion semantics, Eurekas, Age) are ratified but "not closed against the Director's own later revision" (`d044:783-785`). |
| S1-32 | "Re-opened once, by the Director, and closed again by this record." (the pool question) | `docs/d061-civics-points-supersession.md:66` | PROPOSED | The ★ pool row is re-opened only by the Director's 2026-10-21 direction; until D-061 §9 is signed it stands. |

### 1F. Corpus preservation, keys, content discipline

| # | Rule | Source | Status | Implication |
|---|---|---|---|---|
| S1-33 | "Do NOT discard it. Do NOT replace it with ten sample technologies." / "Preserve existing stable IDs wherever possible." | `d044:431-437` | RATIFIED | Redesign = extend/normalise the 433-node corpus, never replace it. |
| S1-34 | "the existing technology tree already exists — no replacement" | quoted in `docs/m6-plan/m6-scope-register.md:84` | DIRECTED (2026-10-20 instruction) | Same: no wholesale replacement. |
| S1-35 | "Never renumber existing technology/content keys." / "Append new nodes with new keys." | ledger `:161-163` | RATIFIED | New nodes get new keys; the 7 civics (1001–1007) cannot be re-keyed. |
| S1-36 | "keys must be unique and ascending across technologies then civics." | `ResearchContent.cs:387-389` | FACT@edba2b5 | Technology keys run 1–426 and civics start at 1001, so at most 574 new technology keys (427–1000) fit before the loader rule must change. |
| S1-37 | "Do not fabricate historical technologies merely to hit a node count." | `d044:692` | RATIFIED | Depth must come from real, sourced advances. |
| S1-38 | "create the smallest reasonable data-driven completion consistent with the existing corpus." | `d044:690` | RATIFIED (scoped to the 2026-09-30 content task) | Signals the Director's preference for minimal additions. |
| S1-39 | "Do not collapse Techniques into Technology nodes merely to make the corpus easier to balance." / "Do not move such nodes without Director approval." | `d045:505-506, 521`; ledger §26 item 21 (`:1133`) | RATIFIED | 38 technique candidates stay put (`d046:396`); no Technique tree. |
| S1-40 | "Content changes touch Director-ratified content (D-046 F§1), so each is a Director decision before it is an edit." | `docs/design/research-capability-ownership.md:77` | PROPOSED (design document: "Nothing here rules anything", `research-capability-ownership.md:9`) | Treat node/cost/prereq edits as Director-gated (tension with S8 LIVING data, §12 X-08). |
| S1-41 | "The Technology tree is preserved: no node, cost or prerequisite changed without a recorded reason" | `docs/m6-spec.md:303-304` | PROPOSED (exit criterion 1) | Every change needs a written reason row. |
| S1-42 | "0 completed Technology/Knowledge nodes." / "Baseline capabilities exist outside the research graph." | `d045:26, 36` | RATIFIED | No pre-completed nodes; baseline is separate (see §6). |

---

## 2. Prohibited reintroductions and other explicit "do not" rules

### 2A. Ledger §26 — every item (`docs/design/director-architecture-and-decision-ledger.md:1109-1141`; RATIFIED per annex R3 `:1481`)

"Future work must not silently reintroduce:" (`:1111`)

| # | Item (verbatim) | Line | Status | Implication for the redesign |
|---|---|---|---|---|
| P-01 | "A universal linear technology tree." | `:1113` | RATIFIED | Depth must be a graph (AND/OR), not a ladder. |
| P-02 | "Separate research currencies per branch." | `:1114` | RATIFIED; supersession for the Technology/Civics pair PROPOSED (`d061:67`) | No per-subtree points; CP only if D-061.1 is ratified. |
| P-03 | "A Cognitive Pool as a second currency." | `:1115` | RATIFIED; same PROPOSED carve-out | — |
| P-04 | "A general RP reserve." | `:1116` | RATIFIED; stays in force under D-061 (`d061:67`; `docs/m6-spec.md:60-61`) | No bank of unspent RP or CP. |
| P-05 | "Manual percentage allocation among multiple research targets." | `:1117` | RATIFIED; kept under D-061 (`m6-spec:60`) | Two targets (one per pool) at most; no splits. |
| P-06 | "Direct Age-gated technology availability." | `:1118` | RATIFIED | Node availability and cost never read Age (see §8). |
| P-07 | "A universal CapabilitySystem." | `:1119` | RATIFIED | Each domain realizes its own unlocks (see §6). |
| P-08 | "Unrestricted individual building objects on the world map." | `:1120` | RATIFIED | Institutions/buildings unlocked by nodes render aggregated (ledger §16 `:818-836`). |
| P-09 | "Universal hard institution caps." | `:1121` | RATIFIED | Viability thresholds and diminishing returns instead (ledger §14 `:777-796`). |
| P-10 | "RP multiplied by `dtYears`." | `:1122` | RATIFIED | RP stays per strategic turn (ADR-030). |
| P-11 | "A reusable generic per-turn exception mechanism." | `:1123` | RATIFIED; kept under D-061 (`d061:67-68`) | CP per turn needs its own ADR-043, not a shared mechanism. |
| P-12 | "Individual ownership of civilization knowledge." | `:1124` | RATIFIED | Knowledge is polity-owned (see §4, §6). |
| P-13 | "Immortal historical people." | `:1125` | RATIFIED | Any great-person content (famous scholars, inventors) must use finite lifespans. |
| P-14 | "Mandatory historical-nationality recruitment." | `:1126` | RATIFIED | — |
| P-15 | "Mandatory cross-national recruitment payment." | `:1127` | RATIFIED | — |
| P-16 | "Identical movement profiles for all MobileAgents." | `:1128` | RATIFIED | Any scout agent gets its own movement profile (whether scouts exist as agents is OPEN, D-J). |
| P-17 | "Road-only movement." | `:1129` | RATIFIED | Sight/discovery must not assume road-bound agents. |
| P-18 | "Blanket player-control prohibition during War Pulses." | `:1130` | RATIFIED | — |
| P-19 | "Technology repricing solely to hit target Age duration." | `:1131` | RATIFIED | See §3C: no cost change to tune pacing. |
| P-20 | "Fake baseline research nodes." | `:1132` | RATIFIED | Basic scouting, founding, gathering, fire, fishing stay outside the graph. |
| P-21 | "A separate Technique research tree merely because techniques exist." | `:1133` | RATIFIED | — |
| P-22 | "An Industry & Energy Technology branch merely because those domains exist." | `:1134-1135` | RATIFIED | — |
| P-23 | "Generic Age modifiers before relevant systems exist." | `:1136` | RATIFIED | No per-Age percentage table, including for civics. |
| P-24 | "Artificial minimum RP spending to defeat credit-only completion." | `:1137` | RATIFIED | A node fully paid by Eureka/exposure completes. |
| P-25 | "Eureka overflow." | `:1138` | RATIFIED | Credit never spills to another node. |
| P-26 | "Eureka credit calculated from discounted EffectiveCost." | `:1139` | RATIFIED | Eureka uses BaseCost. |
| P-27 | "Mechanical Eureka conditions copied from realization materials without justification." | `:1140-1141` | RATIFIED | Resource-possession Eurekas need a historical justification (§4). |

### 2B. Other explicit prohibitions

| # | Rule (verbatim, ≤25 words) | Source | Status | Implication |
|---|---|---|---|---|
| P-28 | "There is NOT: Technology + separate Knowledge resource + separate Knowledge Tree. Do not create those." | `d044:62-70` | RATIFIED | No knowledge/education tree beside Technology. |
| P-29 | "Do NOT create: Science Points Culture Points Military Research Points … Knowledge Points as a second resource" | `d044:90-97` | RATIFIED; CP carve-out PROPOSED (`d061:65`) | Only CP could be added, by D-061.1. |
| P-30 | "Do not create a second accumulated research currency." | `d044:103` | RATIFIED (see P-29) | — |
| P-31 | "Do NOT invent manual 60/20/20 allocation between technologies." | `d044:315` | RATIFIED | — |
| P-32 | "Do not invent a second condition language." | `d044:333` | RATIFIED | Eurekas/requirements use the D-020 dialect only. |
| P-33 | "Do NOT create a separate delayed \"knowledge realization\" phase for the Technology itself." | `d044:351`; ledger `:220-221` | RATIFIED | Completion is the knowledge; realization is downstream. |
| P-34 | "Do not create Culture Points." / "Do not copy Technology into Civics under another name." | `d044:378-380` | RATIFIED | Civics must be semantically distinct, not a relabelled tech list. |
| P-35 | "Do NOT create a universal CapabilitySystem." | `d044:202` | RATIFIED | — |
| P-36 | "Do not create fake downstream systems simply to make tests pass." / "Do not create speculative simulation mechanics." | `d044:626, 661` | RATIFIED | Nodes whose consequence has no owning system stay knowledge-only/deferred. |
| P-37 | "Do NOT create a Builder MobileAgent." / "Do NOT create fake research nodes merely to represent:" settlement founding, basic scouting, basic clubmen, construction, basic food gathering | `d045:55-62` | RATIFIED | Applies to any fog/scouting node. |
| P-38 | "Do NOT add literacy, education, universities, health, connectivity, institutional maturity, etc. to the base population formula yet." | `d045:111-112` | RATIFIED | Education content cannot feed RP until its system exists. |
| P-39 | "Do NOT fabricate machine-evaluable conditions from ambiguous Eureka prose." | `d045:352` | RATIFIED | Unevaluable Eurekas are labelled future-system, not forced. |
| P-40 | "Do not silently resolve remaining Director-level questions." (incl. university formulas, foreign-knowledge bonuses, Age-transition research effects, milestone placement) | `d045:525-541` | RATIFIED | Open decisions in §12 stay open in any redesign document. |
| P-41 | "Do NOT introduce a minimum research-spend requirement." | `d046:64` | RATIFIED | = P-24. |
| P-42 | "Do NOT use: CLP Cognitive Load Points Cognitive Pool as active architectural terminology." | `d046:189-195`; ledger `:262-266` | RATIFIED | "The authoritative name is:" Research Points (RP) (ledger `:250-252`). |
| P-43 | "Do not merely preserve a historically impossible edge because Civ VI would permit it for gameplay." | `d047:41` | RATIFIED | Causal-historical rule over gameplay convenience (§5). |
| P-44 | "Do NOT copy Civ VI's flat Science output." | `d047:164` | RATIFIED | No yield-from-tiles science. |
| P-45 | "Do NOT introduce Civ VI-style paid manual upgrades." | `d047:372` | RATIFIED | Modernization stays automatic and free. |
| P-46 | "The Civ VI policy-card model is not adopted." | `docs/design/civilization-progression-architecture.md:76` | RATIFIED constraint | No policy cards / "spend points → bonus" (also `docs/m6-plan/m6-scope-register.md:101`). |
| P-47 | "FORBIDDEN … \"Steel Policy +20 → 20% more steel plants\"" | `docs/adr/adr-019-architecture-constitution-addendum.md:113-115` | RATIFIED (ADR-019, cited RATIFIED by civ-progression `:7`) | Civic effects change conditions, never outcomes (`adr-019:122-123`). |
| P-48 | "Avoid hard-coding governments as bundles of direct bonuses." | `docs/d042-empire-and-player-control-addendum.md:95` | RATIFIED | Macro civics/government forms carry no inherent bonuses. |
| P-49 | "Do not introduce universal objects such as:" `CapabilitySystem`, `ProgressionSystem`, universal modifier engines, "merely to centralize unrelated rules." | ledger `:141-147` | RATIFIED | Applies to any central engine for civic effects. |
| P-50 | "Do not reduce institutions to static bonuses." | ledger `:771` | RATIFIED | Civic-made institutions need lifecycles (§7). |
| P-51 | "Do not invent a universal immediate-effect mechanism." | ledger `:1044` | RATIFIED | — |
| P-52 | "Do not collapse all progression into one generic bonus system." | ledger `:1105` | RATIFIED | — |
| P-53 | "Do not fill these gaps with invented constants." | ledger `:1183` | RATIFIED | Deferred formulas (surge, university discount, repeats, exposure) stay unfilled. |
| P-54 | "Do not implement a generic permanent Age modifier table before the relevant systems exist." | ledger `:675-676`; D-043 A7 `docs/d043-civilization-progression-ages-and-mobile-agents.md:262-268` | RATIFIED | — |
| P-55 | "Do not invent a second exploration system." | `docs/d040-discovery-and-control.md:49` | RATIFIED | Fog/discovery reuse D-039 reconnaissance (§10). |
| P-56 | "DO NOT ADD A `loyalty` FIELD." | `d040:105` | RATIFIED | Distance effects run through control/reach, not stored scalars. |
| P-57 | "NOTHING SPAWNS." | `docs/d037-emergent-polities.md:22` | RATIFIED | Scholars, scouts, staff must come from simulated population. |
| P-58 | "No era gate, no date, no unlock." (quoted by D-040 B3) / "NO TECHNOLOGY UNLOCK. LAW 4 BINDS." | `d040:59-64` | RATIFIED; reading narrowed by D-047 r7/r8 (`d044:874-881`) | Research may unlock CLASSES; it never itself opens sea travel or a network edge type. |

---

## 3. Costs

### 3A. Where the rubric is defined (it is distributed; there is no single rubric document in the tree)

| Piece | Location | Status |
|---|---|---|
| Formula and the six factors' meanings | ledger §7.1–§7.4 `docs/design/director-architecture-and-decision-ledger.md:376-443` | RATIFIED (method); U, K are CALIBRATION (annex R3 `:1452`) |
| Constants and their bases | `Sim.Data/content/research.json:37-45` (`tuning.costModel`) | RATIFIED content (D-046 F§1) |
| Factor scales, tier list, tolerance, adjustment ban | `Sim.Core/Systems/Research/ResearchContent.cs:290, 808-848, 919-922` | FACT@edba2b5 (loader) |
| prerequisite-complexity convention | `docs/d047-civ6-reference-and-progression-rulings.md:467-470`; ledger `:425-426` | RATIFIED convention (D-047 Part C is the repository's record of applying ruling 1) |
| The cost-model origin ruling | `docs/d046-research-foundation-gate-rulings.md:50-56` (G1), `d046:299` (F§1) | RATIFIED |
| D-045 cost principles (band scheme superseded) | `docs/d045-research-calibration-rulings.md:123-183`; supersession `d045:608` | RATIFIED principles; band representation SUPERSEDED by D-046 F§1 |
| Per-node rationale (`costRationale.note`) | e.g. `research.json:338-352` (`fire_making`), `motor_road`, `trade`, `oral_rehydration` | FACT@edba2b5 |
| The finalization proposal's cost report (`research-final-cost-report.md`) | **not in the repository on any ref** (git log/grep, this digest); ledger annex R1 says its content lives in `costRationale` and the audits (`ledger:1391`) | FACT@edba2b5 — the authoritative long-form rubric is absent; reconstruct from the rows below |

### 3B. The cost model and the six-factor rubric

| # | Rule (verbatim, ≤25 words) | Source | Status | Implication |
|---|---|---|---|---|
| C-01 | "BaseCost = U × K^magnitude" | `d046:54`; ledger `:383` | RATIFIED | Every Technology and Civics node, including new ones (Civics today use the same U; `docs/m6-foundations-audit.md:36`). |
| C-02 | "magnitude = novelty + technical difficulty + material sophistication + institutional sophistication + breadth of capability + prerequisite complexity" | ledger `:389-396` | RATIFIED | Exactly six factors; `magnitude` must equal their sum within 1e-9 (`ResearchContent.cs:836-837`). |
| C-03 | "U = 92.4" / "K = 2.0" | ledger `:401-402`; `research.json:39-42` | RATIFIED method; values CALIBRATION | U basis: "a magnitude-0 node takes a founded civilization (5,000 people, 30.8 RP/turn) 3 turns" (`research.json:40`); K basis: "each magnitude point doubles cognitive effort" (`:42`). |
| C-04 | "Age enters only as a bounded prior inside difficulty and institutional" | `research.json:43`; ledger `:412-413, 419-420` | RATIFIED | Age may shape only these two factors; never a separate multiplier ("No runtime Age multiplier", `m6-foundations-audit.md:30`). |
| C-05 | **Novelty:** "incremental refinement to paradigm-founding change." | ledger `:410` | RATIFIED | Loader range [0.5, 2.0] (`ResearchContent.cs:810`). Corpus uses 0.5 (66 nodes), 1.0 (284), 2.0 (83) — FACT@edba2b5. |
| C-06 | **Technical difficulty:** "difficulty of achieving the technology; Age may be a bounded context prior." | ledger `:412-413` | RATIFIED | Range [0, 3.5]. Corpus values 0–3.5 in 0.25/0.5 steps; see the Age-prior table C-14. |
| C-07 | **Material sophistication:** "material requirements; theories do not receive high material scores merely because later apparatus was required to test them." | ledger `:415-417` | RATIFIED | Range [0, 2.0]. Corpus: 0.0 (240), 0.75 (79), 1.5 (70), 2.0 (42), others 2. |
| C-08 | **Institutional sophistication:** "institutional requirements; Age may be a bounded context prior." | ledger `:419-420` | RATIFIED | Range [0, 3.0]. Second Age-prior factor (C-14). |
| C-09 | **Breadth of capability:** "scope of what the technology unlocks; use downstream scope, never prerequisite depth." | ledger `:422-423` | RATIFIED | Range [0, 1.5]. Corpus: 0.0 (90), 0.5 (217), 1.0 (88), 1.5 (38). |
| C-10 | **Prerequisite complexity:** "independent requirements; top-level AND requirements count, OR groups count once." | ledger `:425-426` | RATIFIED | Range [0, 1.0]. Convention: one requirement 0.0, two 0.5, three or more 1.0 (`d047:467-470`). |
| C-11 | "Do not count every OR alternative as an independent prerequisite-complexity requirement." | ledger `:345-346` | RATIFIED | OR pathways (Group B) do not raise cost. |
| C-12 | prereq_complexity convention measured: the ledger reading (top-level conjuncts, OR group = 1) matches 432 of 433 nodes; the one exception is `taxation` (3 conjuncts, 0.5) | `research.json` (`taxation` key 1007) | FACT@edba2b5 | D-047's "number of prerequisite atoms … 402 of 430" phrasing (`d047:468-469`) is the weaker reading (411/433 today). Use the ledger reading. |
| C-13 | Tier list `["T1", "T2", "T3", "T4", "TR", "TS"]`; TR only for the recursive set, TS only for `speculative_finite` | `ResearchContent.cs:290, 919-922` | FACT@edba2b5 | T1–T4 have **no definition in the tree**. Measured correlation (one direction only): every T1 node has novelty 0.5 (54/54; 66 nodes carry 0.5); every T4 node has novelty 2.0 (77/77; 83 carry 2.0); T2 mostly novelty 1.0 (244/249); T3 mostly breadth ≥1.0 (33/37). Tier is a label; cost follows magnitude. |
| C-14 | Age prior, measured modal values (difficulty / institutional): A1 0.0/0.0 · A2 0.5/0.25 · A3 1.0/0.75 · A4 1.25/1.0 · A5 1.5/1.25 · A6 1.75/1.25 · A7 2.0/1.5 · A8 2.5/2.0 · A9 3.0/2.5 | computed from `research.json` | FACT@edba2b5 | The prior is near-deterministic per Age; "78.0 %" of log-cost variance is between Ages (`docs/research-corpus-audit.md:37`). Re-dating a node re-prices it. Exceptions exist and are documented (`oral_rehydration`: "a later-Age node that should be cheap", A9, difficulty 0.5, `research.json:25545`). |
| C-15 | Magnitude range 0.5–13.0 ⇒ BaseCost ≈ 131–756,941 RP; `rocket` sits at every factor's maximum (13.0 → 756,960 RP) | ranges `ResearchContent.cs:810-811`; `research.json` (`rocket`) | FACT@edba2b5 | No node can cost more than ~757k RP without widening a loader range (code change). |
| C-16 | "calibration_adjustment … — no calibration adjustment is permitted; cost is content-derived (finalization ruling 1)." | `ResearchContent.cs:838-840`; `research.json:44` | RATIFIED (D-046 G1/F§1) + FACT | The field must be 0. |
| C-16b | "Do not reintroduce ADR-029 R-10." | `d046:299`; R-10 "Was" column `adr-029:608` | RATIFIED | R-10 priced a node as "BaseCost = band weight × Age unit"; no per-Age cost unit or band table may return. |
| C-17 | "the cost must come from the content factors, never be set independently." Tolerance: max(5 RP, 0.1 % of cost) | `ResearchContent.cs:843-848`; R-27 `adr-029:649` | FACT@edba2b5 | Costs are U·K^m rounded to tens; largest deviations 19.20 RP (`rocket`), 2.67 % (`atlatl`) (`research-corpus-audit.md:35`). `content_cost` must equal `cost` (`ResearchContent.cs:841-842`). |
| C-18 | "Prerequisite structure can influence cost, but cost must primarily represent the magnitude of the actual technological advancement." | `d045:138-139` | RATIFIED | — |
| C-19 | "Do not mechanically make every later-generation technology more expensive." / "Do not make every sibling node identical unless their actual magnitude warrants it." | `d045:154, 156` | RATIFIED | — |
| C-20 | "The corpus should be auditable so a reviewer can understand why a node has its cost." | `d045:162-163`; `:171-172` (record the rationale) | RATIFIED | Every new node needs factor values and, by practice, a `note` (e.g. `motor_road`'s per-factor note). |
| C-21 | "The existing prerequisite-depth-derived costs are NOT acceptable as the final content model." / "Do NOT simply calculate: cost = prerequisite depth × constant" | `d045:125-136` | RATIFIED | Depth never enters cost (`research-corpus-audit.md:31-32`). |
| C-22 | Worked examples: `fire_making` 2.0+0+0+0+1.5+0 = 3.5 → 1,050 (T4); `legal_code_roman` 1.0+1.25+0+1.5+1.0+0 = 4.75 → 2,490 (T3); `trade` 3.25 → 880 (T2, "same magnitude as arithmetic_babylonian"); `motor_road` 8.5 → 33,450 (T2, "like macadam") | `research.json:338-352` (`fire_making`); `research.json:28105` (`trade` note); `docs/r2a-trade-citystates-record.md:15`; `adr-032:143-144` | FACT@edba2b5 (trade's 880 RP is RATIFIED: `docs/adr/adr-033-m5-integration-pass.md:249-250`) | Precedent for adding nodes: score six factors, match an analogous node, append a new key, regenerate the audits (`adr-032:140-158`). |
| C-22b | Corpus anchors, factors N/D/M/I/B/P → magnitude → cost (tier): `atlatl` A1 1.0/0/0/0/0/0 → 1.0 → 180 (T2) · `fire_making` A1 2.0/0/0/0/1.5/0 → 3.5 → 1,050 (T4) · `trade` A3 1.0/0.5/0/0.75/0.5/0.5 → 3.25 → 880 (T2) · `taxation` A3 civic 1.0/0.5/0/1.0/0.5/0.5 → 3.5 → 1,050 (T2) · `legal_code_roman` A4 civic 1.0/1.25/0/1.5/1.0/0 → 4.75 → 2,490 (T3) · `oral_rehydration` A9 1.0/0.5/0/1.0/0/0.5 → 3.0 → 740 (T2) · `motor_road` A9 0.5/2.5/1.5/2.5/0.5/1.0 → 8.5 → 33,450 (T2) · `rocket` A9 2.0/3.5/2.0/3.0/1.5/1.0 → 13.0 → 756,960 (T4) | `research.json:1139` (atlatl), `:292` (fire_making), `:28039` (trade), `:28433` (taxation), `:28164` (legal_code_roman), `:25481` (oral_rehydration), `:27978` (motor_road), `:20245` (rocket) — each node's `costRationale` | FACT@edba2b5 | Score a new node by matching these anchors factor by factor. Reading of the scale (INFERRED from ledger `:410`): novelty 0.5 is incremental refinement (`motor_road`), 2.0 paradigm-founding (`fire_making`, `rocket`). |

### 3C. When and how costs may change

| # | Rule | Source | Status | Implication |
|---|---|---|---|---|
| C-23 | "Technology costs must NOT be changed to force a target Age duration." / "No calibration adjustment is permitted merely to hit an Age-duration target." | `d046:50, 56` | RATIFIED | Pacing is fixed through population/RP or the Age-to-dt mapping, never costs. |
| C-24 | "Do not change technology costs solely to force an Age to last a chosen number of turns." | ledger §7.3 `:430-431`; §26 item 19 `:1131` | RATIFIED | Same. |
| C-25 | "DO NOT solve the pacing problem by simply making every final-Age node enormously expensive." | `d045:226-227` | RATIFIED | — |
| C-26 | Cost moves only when a factor moves under the convention; "No other factor is touched." | `d047:467-470` | RATIFIED practice (D-047 Part C) | A prerequisite edit that changes the requirement count must move `prereq_complexity`, `magnitude` and `cost` together. |
| C-27 | "This is a hard gameplay calibration target." — final Age "approximately 150-250 turns" (centre 200) for a research-optimized civilization | `d045:187-200` | RATIFIED target; cost lever forbidden by C-23; "DO NOT ratify ~108M as the canonical A9 population" (`d046:39`) | The target survives as a reading of population scenarios (`docs/research-calibration-report.md:54-64`), not a cost lever. |
| C-28 | Finite workload 11,059,750 RP; A9 8,763,340 RP over 115 nodes; canonical seed 42 produces 696,745 RP in 1,630 turns = 6.3 %; A7–A9 never reached | `docs/research-calibration-report.md:33-35, 103, 115-117` | FACT@edba2b5 (report is "PROVISIONAL, NOT FROZEN", `:1, 6`) | A deeper tree adds workload the shipped demography cannot reach; pricing it down is barred by C-23. |
| C-29 | "No knowledge corridor is an M6 exit criterion." (self-referential candidates refused) | `docs/m6-spec.md:140-144` | PROPOSED | Any pacing gate needs an external, non-cancelling bound (S8 §4.1 item 3, `docs/spine-s8-governance-freeze.md:158-161`, RATIFIED). |
| C-30 | "Tuning data files and `TUNE` parameters is always allowed." | `CLAUDE.md:30`; `spine-s8:24` | RATIFIED | U, K and the RP coefficients are CALIBRATION, "replaceable without a ruling" (`d045:10`; ledger `:1452`; `rpPerTurn` "calibration — tunable", `research.json:29`), but moving U or K to hit an Age duration is still barred (C-23); per-node content is ratified content (§12 X-08). |

### 3D. EffectiveCost, modifiers, RP

| # | Rule | Source | Status | Implication |
|---|---|---|---|---|
| C-31 | "No modifier stack may reduce effective cost below 20% of BaseCost." | ledger `:459`; `d046:68-72` | RATIFIED | The 0.20 is stored as `tuning.effectiveCostFloorFraction` (`research.json:31`). |
| C-32 | Universities: "relevant specialized university → reduces effective research cost of relevant Technology nodes." / formula "NOT ratified" | `d044:177-178, 192`; ledger `:545-552` | RATIFIED rule; formula DEFERRED | Shipped factor `1 − ⅓(1 − e^(−X))`, 1/3 CHOSEN (`m6-foundations-audit.md:47`); Main-tree and Civics nodes always pay BaseCost (`:29`). |
| C-33 | "Freeze the currently ratified five university types." | `d047:156` | RATIFIED | No new university type for new subtree content. |
| C-34 | "A newly founded university should not immediately perform like a mature research institution." | `d045:435-436` | RATIFIED | — |
| C-35 | D-021 brake owed by the first writer of a university cost modifier | civ-progression `:195`; ADR-033 D6 `adr-033:147-149` | RATIFIED (D-021 project-wide, `docs/d021-stability-doctrine.md:8`) | Any new research-accelerating loop (diffusion, CP from institutions) ships its brake with it. |
| C-36 | "RP per turn = coefficient × population^exponent" … "The rate is not multiplied by `dtYears`." | `docs/adr/adr-030-per-turn-research-points.md:17-23` | RATIFIED (coefficient 0.08, exponent 0.699 are CALIBRATION) | RP inputs are population only today (`m6-foundations-audit.md:25`). |

---

## 4. Eurekas, foreign exposure and diffusion

| # | Rule (verbatim, ≤25 words) | Source | Status | Implication |
|---|---|---|---|---|
| E-01 | "Eureka conditions can accelerate a Technology independently of whether it is the currently selected research target." | `d044:323` | RATIFIED | — |
| E-02 | "Eureka credit cannot exceed the remaining cost." / "No Eureka overflow to another Technology." | `d044:327-329` | RATIFIED | — |
| E-03 | "Eureka conditions should use the existing deterministic predicate architecture where applicable." | `d044:331` | RATIFIED | Conditions read node atoms, `stock_<good>` quantities and registered variables; a settlement-scoped operand holds if it holds in at least one controlled settlement (`adr-029:264-271`). |
| E-04 | "40% of BASE technology cost" for a fully satisfied Eureka | `d045:254-258` | RATIFIED; "Retain the existing civDemo 40% BaseCost ceiling." (`d047:186`) | — |
| E-05 | "Eureka credit uses BaseCost, not discounted EffectiveCost." | ledger `:488` | RATIFIED | — |
| E-06 | "Do NOT assume all future Eurekas are necessarily equally weighted." / "Total credit for one Eureka may never exceed: 40% of base technology cost." | `d045:310, 325-327` | RATIFIED | Weights explicit or 0.40 ÷ N; a node's weights sum ≤ 0.40 (`d046:300`). |
| E-07 | "A satisfied Eureka condition can award its credit only once." | `d045:331` | RATIFIED | Idempotent; 0/N…N/N queryable (`d045:341-348`). |
| E-08 | "Eureka and foreign-exposure credits share one pool." | ledger `:463`; `d046:74-80` | RATIFIED | Exposure + Eureka ≤ 0.40 × BaseCost per node, ≤ remaining EffectiveCost, provenance kept per source. |
| E-09 | "ALLOW credit-only completion." | `d046:62`; ledger `:479-484` | RATIFIED | — |
| E-10 | "Accept the Civ VI-style meaningful Eureka philosophy." (achievement, discovery, observation, exposure, institutional condition, practical accomplishment, historical circumstance) | `d047:170-182` | RATIFIED | Civic "Inspirations" (spec §8, `m6-spec:372`, PROPOSED) would follow the same rules. |
| E-11 | "Reject trivial \"you possess the raw material required to manufacture this\" conditions unless the material possession itself is historically meaningful evidence for the technology." | `d047:184` | RATIFIED | Resource-based Eurekas need meaningful exposure (`arsenical_bronze` precedent: waits for an arsenical-ore state, `d047:512`). |
| E-12 | "Retain a Eureka only if it is a meaningful historical/gameplay accelerator." / "if every path to a technology already implies the condition, it is not a useful Eureka" | ledger `:518-527` | RATIFIED | Loader rejects implied and dead Eurekas (`adr-029:636`; dead-Eureka check `ResearchContent.cs:620`). |
| E-13 | "Do NOT fabricate machine-evaluable conditions from ambiguous Eureka prose." | `d045:352` | RATIFIED | Future-system Eurekas carry `when: null` and a class. |
| E-14 | Curated set: 81 Eurekas on 80 nodes; 17 evaluable today; 34 wait for `future-system:resources/terrain` | `docs/research-corpus-audit.md:62-66` | FACT@edba2b5 (D-046 F§1 recorded 18 evaluable, `d046:300`; the audit on this tree counts 17) | Resource discovery state could make up to 34 Eurekas evaluable — a coupling to name in any resource design. |
| E-15 | Engine order: a node's Eurekas in index order, then foreign exposure; all credit before RP; a held Eureka fires once even if it pays nothing | `adr-029:645-647` (R-23…R-25) | PROPOSED (implementer resolutions, overridable) — implemented | — |
| E-16 | `ResearchExposureRow` "NEW input seam, no writer" | `adr-029:632`; `m6-foundations-audit.md:31` | FACT@edba2b5 | Foreign exposure exists only as a seam. |
| E-17 | "Foreign knowledge is not a separate progression tree." | ledger `:697` | RATIFIED | Diffusion credits nodes; it never adds a tree. |
| E-18 | "Discovery, contact, understanding, adoption, and realization are distinct concepts." | ledger `:730-731` | RATIFIED principle; "Exact implementation is deferred." (`:728`) | — |
| E-19 | "An isolated civilization does not receive catch-up knowledge from an unknown/uncontacted civilization." / "Direct contact is stronger than transitive exposure." | ledger `:733-736` | RATIFIED principle | Diffusion must read contact, which under fog means contact within known extent (`docs/m6-plan/m6-ruling-docket.md:71-72`, PROPOSED sequencing). |
| E-20 | "First-mover advantage comes from mature institutions, personnel, production, infrastructure, and organizational capability, not exclusive permanent knowledge ownership." | ledger `:738-740` | RATIFIED | — |
| E-21 | Potential pathways (13): trade, open borders, migration, scholars, craftsmen, engineers, students, military observers, captured equipment, prisoners, observation, institutional imitation, warfare lessons | ledger `:699-713` | RATIFIED list; choice of M6 pathways OPEN (`m6-spec:215-216`) | — |
| E-22 | Exposure credit "= `w_contact × BaseCost` with w ≤ 0.4 − w_eureka", credited "in settlement-id order, never exceeding the shared acceleration pool" | `m6-spec:129, 210-212` | PROPOSED (T6.10) | — |
| E-23 | "do not silently decide: … foreign-knowledge research bonuses" | `d045:533-538` | RATIFIED | Exposure weights need a Director ruling. |
| E-24 | Knowledge persistence: "Completed knowledge never decays, and it is never removed by revolt or annexation." | `docs/d048-knowledge-persistence-rulings.md:20-21` | RATIFIED | Diffusion only adds (spec I-8, `m6-spec:98-99`, PROPOSED restatement). |
| E-25 | "Revolt copies completed research only." / "Research progress remains with the parent." / "Eureka credit remains with the parent." | `d048:11-14` | RATIFIED | — |
| E-26 | "Annexation, when implemented, merges knowledge by union and never removes completed knowledge." | `d048:46` | RATIFIED | — |
| E-27 | "The controller is NOT granted the settlement's knowledge." (conquered/city-state knowledge diffusion "is OPEN") | `docs/city-state-progression.md:60-62` | OPEN | — |
| E-28 | D-021 brakes for diffusion: rising expectations and brain drain | `docs/design/arch-FGH-civics-institutions-diffusion.md:511-529` | PROPOSED (D-021 itself RATIFIED) | T6.10 carries a "brake if accelerating" (`m6-spec:281`, PROPOSED). |
| E-29 | "No carrier, no coupling." — trade flow as contact edge; "A good is not a teacher" (opposite verdicts) | `arch-FGH:423-433, 448-457` | PROPOSED (both lanes) | D-035-C carrier test is RATIFIED: "Name the physical carrier — a good, a purse, a building, a policy, a body, a season." (`docs/d035-needs-aggregation.md:91`). |

---

## 5. Prerequisites

| # | Rule (verbatim, ≤25 words) | Source | Status | Implication |
|---|---|---|---|---|
| R-01 | "The graph is not a linear technology tree." … "AND OR nested AND/OR prerequisite expressions." | `d044:269-275` | RATIFIED; ★ (`d044:791`) | — |
| R-02 | "Prerequisite edges represent causal requirements, not merely historical chronology." | `d044:283` | RATIFIED | — |
| R-03 | "Every prerequisite relationship must be causally and historically possible before the technology/capability it enables." | `d047:37` | RATIFIED (D-047 r1) | Pinned by `D047_Causal_NoRequiredPrerequisiteEmergedAfterItsDependent` (`d047:430, 499-500`), which reads `emerged`. New nodes need dates. |
| R-04 | "If a prerequisite chronologically follows the technology it currently gates, treat the edge as incorrect and fix/re-scope it." | `d047:39` | RATIFIED | — |
| R-05 | "A dependency edge means causal dependency — could B exist without A? — never chronology." | civ-progression `:101` | RATIFIED | — |
| R-06 | "Reject circular prerequisite graphs." | `d044:285`; loader cycle report `adr-029:175` | RATIFIED + FACT | — |
| R-07 | "Feedback between domains … runs through simulation state, not through prerequisite edges." | civ-progression `:103` | PROPOSED (labelled INFERRED there) | — |
| R-08 | "Prerequisites may contain AND/OR/NOT logic." | ledger `:341` | RATIFIED text — **contradicted by the loader** (R-09) | OPEN (gap audit G-33). |
| R-09 | "NOT is not allowed in a prerequisite — … completing knowledge can never make a node LESS available." | `ResearchContent.cs:463-465`; also requirements `:330-331`, stage `:506` | FACT@edba2b5 (ADR-029 §2.2 `adr-029:71-72`, PROPOSED) | Design with monotone (NOT-free) prerequisites unless the Director rules G-33 (`docs/design/director-ledger-gap-audit.md:687-696`). |
| R-10 | "OR alternatives are genuine alternative pathways." | ledger `:343` | RATIFIED | Group B widened writing/law/numeration/record-keeping pathways (`ledger:357-372`); "The genuinely Babylonian-specific nodes remain specific." |
| R-11 | Orphan rule: "nothing requires it, it unlocks no entity and declares no capability, so completing it would change nothing." | `ResearchContent.cs:595-597` | FACT@edba2b5 | Every new node needs a dependent, an entity unlock or a capability string. |
| R-12 | "Dependent technologies are unavailable until their prerequisites are complete." Availability reads PREV; dependents open the step after completion | `d044:287`; `adr-029:236-237` (R-17) | RATIFIED + FACT | — |
| R-13 | Research stage composition (S1-08) contains the civic `legal_code_roman`; `inst.university` "requires": "library AND legal_code_roman" | `research.json:254, 29068` | FACT@edba2b5 | "civics require technologies, and the research stage requires a civic" (`adr-029:108-109`). Under CP the stage is paced by CP (`m6-ruling-docket.md:51-62`). |
| R-14 | D-061.8: civic atoms inside Technology gates "are legitimate \"exposure\" edges" and T6.3 must measure stage reachability | `m6-ruling-docket.md:34`; `d061:155-156` | PROPOSED | Cross-tree edges allowed only as explicit exposure prerequisites (`m6-spec:199-200`, PROPOSED). |
| R-15 | Validators required: duplicate ids, invalid tree/branch, missing/circular prerequisites, invalid Eurekas, costs, unlocks, orphans, unreachable, invalid cross-tree dependencies | `d044:698-711` | RATIFIED | All shipped in the loader (`adr-029:168-183`). |
| R-16 | "Do not leave the majority of the graph disconnected just because it is inconvenient." | `d044:684` | RATIFIED | — |
| R-17 | "If prerequisites are satisfied, a civilization may research a later-Age technology." | ledger `:352-353` (§6.1 "Age does not gate research", ledger `:348`) | RATIFIED | A deep tree can be climbed at any Age: a polity that meets the prerequisites may take a later-Age node early. |

---

## 6. Unlocks, capabilities, realization, node content

### 6A. Completion = eligibility only

| # | Rule (verbatim, ≤25 words) | Source | Status | Implication |
|---|---|---|---|---|
| U-01 | "Technology completion does NOT directly spawn: buildings institutions units infrastructure" | `d044:404-410` | RATIFIED | — |
| U-02 | "Engineering Technology completed → engineering capability available → factory/construction/vehicle/etc. may become eligible → actual realization occurs through the relevant system." | `d044:416-419` | RATIFIED | Every unlock names its owning system. |
| U-03 | "Researching a technology never creates a building. It can make one AVAILABLE." | `docs/adr/adr-028-viability-and-saturation.md:51` | RATIFIED | Lifecycle LOCKED → AVAILABLE → UNDER_CONSTRUCTION → ACTIVE → MATURE → SATURATED (`adr-028:42-49`). |
| U-04 | "Do NOT revert to a model where research completion merely discovers a technology and some second research process must \"realize\" it." | ledger `:220-221` | RATIFIED | — |
| U-05 | "No node completion writes anything but its completion row. Every capability is a predicate answered by a query" | `m6-spec:81-82` (I-1) | PROPOSED restatement of RATIFIED D-044 R11/R14 | — |
| U-06 | "Research unlocks the capability/class. Research does NOT itself construct the vessel." | `d047:122-124` | RATIFIED | Transport/road nodes unlock CLASSES; owning systems realize (`d047:136-138`). |
| U-07 | "Do not use calendar Age alone as the gate." | `d047:146` | RATIFIED | — |
| U-08 | "One predicate, two callers." — the UI lists exactly what the simulation would accept | `adr-033:76-78`; `m6-spec:224-227` | RATIFIED design of ADR-033 D2 (ADR status "ACCEPTED for implementation", `adr-033:3`) | Every new gated verb ships a T15-style injected-order rejection test. |
| U-09 | "Only actions that exist are listed." | `adr-033:79-82` | as U-08 | Knowledge-only consequences never appear as fake controls. |
| U-10 | Live consumer types: recipe, activity, trade, tax, age, stage, institution, road, building | `docs/research-gameplay-unlock-audit.md:7-17` | FACT@edba2b5 | New node consequences must map to one of these or to a new owning system. |
| U-11 | Counts: B research-gated gameplay 51 · C knowledge-only 262 · J explicitly deferred 70 · F unit unlocks 30 | `research-gameplay-unlock-audit.md:28-36` | FACT@edba2b5 | Most nodes are knowledge-only; deepening adds to C/J unless realizing systems are built. |

### 6B. The five states and their owners

| # | Rule | Source | Status | Implication |
|---|---|---|---|---|
| U-12 | "Prerequisites, researched/available status, adoption and active effects must remain distinct." | `d061:25` | DIRECTED | — |
| U-13 | "Unresearched → Researched (= Available/Eligible) → Adopted → Active plus Realized … No state is implied by another" | `m6-spec:84-87` (I-2) | PROPOSED | Research owns the first two; institution/regime-module tables own Adopted/Active; realizing systems own Realized. |
| U-14 | "Available and Adopted are distinguished in the model and the presentation without implementing political transitions in M6." | `docs/m6-plan/m6-decision-register.md:235-237` (D-D) | RATIFIED (RULED 2026-10-20) | — |
| U-15 | "A Civic completion yields a political or governance capability that makes institutional forms, policies and arrangements eligible. Adoption is a separate act" | civ-progression `:78` (§5.4) | RATIFIED (DIRECTOR RULING) | — |
| U-16 | Template: tax gate chain Inert → NeedsKnowledge → NeedsAge → NeedsSeat → Open, "a reason for every locked state" | `m6-spec:186-187`; `m6-foundations-audit.md:59` | FACT@edba2b5 / PROPOSED as template | — |

### 6C. Capability vocabulary, scope, no God object

| # | Rule | Source | Status | Implication |
|---|---|---|---|---|
| U-17 | "State → Published Variables → Predicates → Capabilities → Available Actions" | `d042:156-158` | RATIFIED | — |
| U-18 | "Capability evaluation must be able to distinguish SCOPE — Empire-level and Settlement-level" | `d042:160-161` | RATIFIED | Recipes are settlement-scoped (`city-state-progression.md:58`); research is polity-scoped. |
| U-19 | "Do not create a universal God system such as a `CapabilitySystem`" | `d042:146-147` | RATIFIED | `AvailableActionsQuery` "is not a CapabilitySystem" (`adr-033:70-72`). |
| U-20 | `UnlockedCapabilities` strings are display only | `m6-scope-register.md:36`; `m6-spec:273` (T6.2 review) | FACT / PROPOSED | Capability text is descriptive; it creates no mechanic. |
| U-21 | "The seam proposed below is conformant only as a shared predicate grammar consumed independently by each domain system" | `docs/capability-architecture-decision.md:171-173` | RATIFIED constraint (D-042) quoted in a historical record | — |
| U-22 | "Not every building requires a technology." / "A null technology requirement is valid and must not be filled with an invented prerequisite." | `adr-028:53` | RATIFIED | — |

### 6D. Baseline capabilities and knowledge-only nodes

| # | Rule | Source | Status | Implication |
|---|---|---|---|---|
| U-23 | Baseline at minimum: "Settlers / settlement founding", "Scouts / exploration", "Clubmen or equivalent basic military", "Basic food/resource gathering", "Basic internalized construction" | `d045:38-45` | RATIFIED | Basic scouting and resource gathering may never be research-gated. |
| U-24 | "Fire itself is BASELINE." / basic hafting, basic fishing, primitive shore-hugging transport, basic paths BASELINE | `d047:58, 70, 80, 110, 142` | RATIFIED | Advanced forms are technology-owned (`d047:72, 84, 114, 144`). |
| U-25 | "The initial world may contain baseline capabilities without implying completion of the entire related research branch." | ledger `:1011-1012` | RATIFIED | — |
| U-26 | 13 baseline records in `research.json` (e.g. `baseline.exploration` "not yet simulated") | `research.json:49-120` (exploration `research.json:57-59`); `research-gameplay-unlock-audit.md:41-69` | FACT@edba2b5 | The unlock audit's class A counts 25 (baseline records plus null requirements, `research-gameplay-unlock-audit.md:27`). |
| U-27 | "No sector is gated and production is unchanged." (sectors present the ACTIVITY knowledge makes eligible) | `adr-033:38-39` | RATIFIED (Director 2026-10-02 directive, ADR-033 D1) | Research changes labels/activities, not baseline output. |
| U-28 | Verb-granting civics: "knowledge-only until M7" (D-Q) | `m6-decision-register.md:25, 223-226` | OPEN (recommended) | e.g. "Levying Troops" stays content-only in M6. |
| U-29 | "332 knowledge-only nodes stay honest" (each with a knowledge-only label) | `docs/m6-plan/m6-task-register.md:84` | PROPOSED | — |

### 6E. Deferred entities, modernization

| # | Rule | Source | Status | Implication |
|---|---|---|---|---|
| U-30 | "research completion grants KNOWLEDGE only (decision 6); the physical entity is built through the existing order/construction pathway" | `docs/deferred-entity-realization-plan.md:14-16` | RATIFIED rule (Director decision 6); tier membership "INFERRED and OPEN" (`:83-84`) | 115 deferred entities; priority A–G is the Director's LOCKED order (`:5`). |
| U-31 | "E. Military/unit realization — owned by M7 Battle Layer (do not pull forward)" | `deferred-entity-realization-plan.md:57` | RATIFIED placement | 23 `unit.*` entities stay inert in M6. |
| U-32 | "units automatically convert to the appropriate next-Age version of their unit type. This is automatic. It is free." | `d047:366-370` | RATIFIED ("DO NOT reopen this.", `:362`) | Military tech nodes do not upgrade units; Age transition does. |

### 6F. Node text ("educational content") — rules that bind descriptions

| # | Rule | Source | Status | Implication |
|---|---|---|---|---|
| U-33 | Node identity: Technology ID, Name, Description, "Historical period / Age relevance" | `d044:210-214` | RATIFIED | — |
| U-34 | Baseline-claiming node strings were "re-scoped to improvements or classes" or recorded as non-owning | `research-capability-ownership.md:102`; `d047:538-559` | RATIFIED practice (D-047 rulings 2–8) | Text must not imply that a baseline activity needs the node. |
| U-35 | "Historical evidence separated from design abstraction; no universal claims." | `m6-task-register.md:112-113` | PROPOSED | — |
| U-36 | "Use Civilization VI as a source of comparative design lessons, not as permission to copy its content or assume its systems fit this simulation." | `d061:27-28` | DIRECTED | Civ VI may inform; content is original. |
| U-37 | Civ VI comparison must be cited; "content is authored against uncited recollection" is risk R-21 | `docs/m6-plan/m6-risk-register.md:33`; D-U `m6-ruling-docket.md:46` | PROPOSED / OPEN | — |
| U-38 | "No explanation string may exist that is not derived from state." | `docs/civ-sim-architecture-v3-outline.md:24` (Spine law 6) | RATIFIED (FROZEN) | Text that explains live state must come from queries; static history text must not claim effects the simulation lacks. |

---

## 7. Civics

| # | Rule (verbatim, ≤25 words) | Source | Status | Implication |
|---|---|---|---|---|
| V-01 | "Civics is Tree 2." / "It uses the SAME Cognitive Load Point pool." | `d044:363-365` | RATIFIED; supersession PROPOSED (D-061.1) | Until D-061 §9 is signed, civics cost RP from the one pool. |
| V-02 | "There is exactly one shared RP pool. Technology and Civics draw from the same pool." | `d046:202-204`; ledger `:272-274` | RATIFIED; supersession PROPOSED | — |
| V-03 | "one shared Research Points pool and one target. … No separate Civics Points currency." (D-I) | `m6-decision-register.md:232-234` | RATIFIED (RULED 2026-10-20) | The latest ruling in force. |
| V-04 | "Civics uses a separate Civics Points pool, distinct from Research Points." | `d061:17` | DIRECTED (2026-10-21) | Conflict recorded as CR-023 (`docs/adr/cr-023-civics-points-vs-single-pool-rulings.md:3`, OPEN). |
| V-05 | "The Civics progression graph is a genuinely distinct tree, not a small branch or renamed subset of the existing science/technology tree." | `d061:18-19` | DIRECTED | Consistent with D-044 R12 (`d044:380-382`). |
| V-06 | "Micro-civics represent age-appropriate practices, institutions or policies." / "Macro-civics represent government forms or broader institutional arrangements." | `d061:22-23` | DIRECTED | No micro/macro field exists yet (`m6-spec:197-200`, PROPOSED schema). |
| V-07 | "Researching a civic makes its consequences eligible according to explicit rules; it must not automatically activate a government or silently apply political effects." | `d061:20-21` | DIRECTED | — |
| V-08 | "Completing an Age must not automatically grant every government form." | `d061:24` | DIRECTED | See §8 and §12 X-10. |
| V-09 | "Optional civic branches must not become mandatory merely to progress through the entire simulation." | `d061:26` | DIRECTED | Spec I-7: a validator proves CORE milestones and macro-civic prerequisites are reachable without `optional` nodes (`m6-spec:96-97`, PROPOSED). |
| V-10 | "Civics must support: node identity cost prerequisites partial progress completion unlocks effects branching" | `d044:367-376` | RATIFIED | — |
| V-11 | "Civics is a separate progression domain with its own content and semantics." | `d044:382` | RATIFIED | — |
| V-12 | "Technology answers what does the civilization know? Civics answers how does it organize and govern itself?" | civ-progression `:72` | RATIFIED (DIRECTOR RULING) | Coverage list (government, legal systems, citizenship, taxation, bureaucracy, diplomatic norms…) with "No node list is fixed here." (`:74`). |
| V-13 | Civic completion → capability → eligibility → adoption (U-15) | civ-progression `:78` | RATIFIED | — |
| V-14 | "Government type is a strategic operating environment … as mechanism, not modifier." | civ-progression `:80`; `adr-019:197-199` | RATIFIED | — |
| V-15 | "composable modules, not enum governments" | `docs/civ-sim-architecture-v3-outline.md:85` | RATIFIED (FROZEN Spine row; politics milestone = M8 after rebase) | Government forms are module sets, not an enum. |
| V-16 | "M6 defines civic prerequisites and government eligibility; government adoption and active political effects belong to M8." | `m6-decision-register.md:235-237` (D-D) | RATIFIED | M6 civics content ends at eligibility. D-E and D-F (unit progression; training/AP): §9 M-07, M-08. |
| V-17 | Regime module set ("serialized, latched, per Empire") and GOVERNMENT LABEL ("Not stored, not serialized, and never read by any mechanism") | `arch-FGH:130-131` | PROPOSED (adopted in `m6-spec:191-193`, PROPOSED) | D-V: content-only in M6, "a table with no writer is dead state" (`m6-spec:412-413`, PROPOSED). |
| V-18 | "a Civics node makes institutional forms eligible (architecture §5.4); it cannot unlock a" building/unit/activity/… | `ResearchContent.cs:1240-1242`; `adr-029:106-107` | FACT@edba2b5 (loader rule; ADR-029 PROPOSED) | **Verified: Civics may unlock only Institution entities.** Macro civics making government forms eligible need a new representation (loader/content change). |
| V-19 | Taxation: "The only gate is now the Civics node `taxation`: key 1007, A3, 1,050 RP." | `adr-033:268-271` | RATIFIED (R5, Director 2026-10-04) | A civic gates a verb through `sim.json governance.taxationRequires` (`Sim.Data/content/sim.json:268`), not through `unlocks`. |
| V-20 | "Taxation is an A3 (Bronze Age) capability and cannot be operational before A3." | `docs/d049-taxation-and-revolt-model.md:36` | RATIFIED | An Age gate on a civic capability exists (§8, §12 X-06). |
| V-21 | "Laws-lite and the authority/bandwidth economy assigned to the M6 civic specification and implementation plan, subject to architecture review." | `docs/m5-exit-inventory.md:408-409` (R4) | RATIFIED (RULED 2026-10-20) | Neither term is defined anywhere (`m6-foundations-audit.md:62`). |
| V-22 | "Budget/treasury: … unassigned pending a definition" | `m5-exit-inventory.md:409-410`; CR-008 "There is NO treasury requirement." (`:386`) | RATIFIED | No civic may depend on a treasury. |
| V-23 | Seven civics today (keys 1001–1007); six of seven realize only through Age milestones; every civic `inst.*` is deferred | `m6-foundations-audit.md:36` | FACT@edba2b5 | — |
| V-24 | D-061.1–.8: supersession; CP per turn by ADR-043; CP inputs (population term + Active-institution term, no RP term); key space; migration 1:1; schema slot; one civics target; Age-milestone coupling | `d061:115-124` | PROPOSED (§9 ruling block empty, `d061:142-143`) | — |
| V-25 | D-061.4 revised: "shared key space with a reserved civics range and a cross-tree fence, not a separate file" | `d061:152-154`; `m6-ruling-docket.md:30` | PROPOSED | Also forced by ledger §2.5 (never renumber keys, `:161`, RATIFIED). |
| V-26 | "CP must never be the left operand of a gate" / "CP's name must not enter the published-variable registry readable by D-020 predicates." | `d061:83-84` | PROPOSED (basis arch-O II.2 is itself PROPOSED, `docs/design/arch-O-invariants.md:221-224`) | Only node completion gates. |
| V-27 | "One graph loader, one predicate grammar, one availability query, one engine parameterized by (tree, pool, target). No `CivicsQuery` twin" | `m6-spec:90-91` (I-4) | PROPOSED | Risk R-02/R-19 (`m6-risk-register.md:10, 31`). |
| V-28 | "A second per-turn quantity needs its own ADR." | `adr-030:37` | RATIFIED | CP per turn ⇒ ADR-043 (PROPOSED, `d061:68`). |
| V-29 | Policy cards / flat bonuses refused (P-46, P-47, P-48) | civ-progression `:76`; `adr-019:113-115`; `d042:95` | RATIFIED | — |
| V-30 | Institutional trade-off: "one institution raises one need and lowers another" | `docs/d035-needs-aggregation.md:86` | RATIFIED | The legal shape for a civic institution's need effect. |
| V-31 | "what is an institution" (CR-010) "never written" | `m6-decision-register.md:170-173` (D-M) | OPEN | Civic-made institutions need it (recommended: the university shape generalized, `m6-spec:349`). |
| V-32 | "Finalizing a historical civics catalogue or balance values without a design basis" is out of M6 scope | `m6-spec:66`; risk R-01 `m6-risk-register.md:9` | PROPOSED | Each civic node needs a realizing mechanism or a knowledge-only mark (`m6-spec:275`). |
| V-33 | No macro civic as an Age CORE milestone; Age entry never grants a form | `m6-spec:207-209`; `d061:155-156` | PROPOSED | Today `a5_systematic_law` CORE = `legal_code_roman` (`Sim.Data/content/ages.json:85`); micro/macro class of that node is unassigned. |
| V-34 | Civics surfaced: Technology and Civics under KNOWLEDGE & TECHNOLOGY; "Active/adopted civic/government effects are surfaced under INSTITUTIONS." | `d047:198-207` | RATIFIED | — |
| V-35 | Decision hierarchy: Director rulings → ADRs → "Civ VI precedent for the closest analogous mechanic" → new design | `d047:384-389` | RATIFIED (D-047 A.2) | Narrowed in spirit by U-36 (DIRECTED). |

---

## 8. Ages

| # | Rule (verbatim, ≤25 words) | Source | Status | Implication |
|---|---|---|---|---|
| A-01 | "Ages are civilization-development states, not date brackets." / "Age progression is irreversible." | `docs/d043-civilization-progression-ages-and-mobile-agents.md:72, 76` | RATIFIED | — |
| A-02 | "An Age is computed state derived from milestones, never a calendar." | `docs/adr/adr-031-ages-advancement-and-unit-modernization.md:19` | RATIFIED rule (D-047 r12-r13); ADR status "ACCEPTED for implementation" (`adr-031:3`) | — |
| A-03 | "No calendar gates: capability derives from computed state, never from dates or era labels." | `CLAUDE.md:19`; Spine law 4 `civ-sim-architecture-v3-outline.md:22` | RATIFIED (FROZEN) | — |
| A-04 | "Technology completion does not automatically advance Age." / "Age does not directly unlock a Technology merely because the calendar entered that Age." | `d044:392-394` | RATIFIED | — |
| A-05 | "No Technology or Civics node is unlocked by a change of Age." | civ-progression `:241` | RATIFIED | Applies to civics too. |
| A-06 | "Age measures civilization development; it is not a hidden research lock." | ledger `:355` | RATIFIED | Research never reads Age (`adr-031:25-27`); ledger §26 item 6. |
| A-07 | "Availability and cost never read the Age; the Age reads research" / "Completing an Age grants nothing by itself; in particular no government form." | `m6-spec:94-95` (I-6) | PROPOSED | Stronger than V-08 (DIRECTED); see §12 X-10. |
| A-08 | Age transition: mandatory core milestones + supporting milestones with category coverage | `d043:94-102`; ledger `:621-625` | RATIFIED | — |
| A-09 | Five categories: Technological, Material-Economic, Institutional-Social, Systemic, Military Realization | ledger `:627-633`; `d047:219-225` | RATIFIED | — |
| A-10 | "Individual owning systems publish milestone facts." / "A thin Age evaluator reads those facts." | `d047:217, 227` | RATIFIED (AGE-MS-1) | Fact kinds include `research` and `research_count` (`ages.json:9`; `adr-031:122-125`). |
| A-11 | "Researching a military technology does not automatically satisfy Military Realization." | ledger `:635-636` | RATIFIED | — |
| A-12 | "Milestones are visible to the player but are not an eighth Tree." / "Partial Age progress has no direct mechanical effect." | ledger `:638, 640` | RATIFIED | — |
| A-13 | "Milestones measure realized civilization development; they are not hidden research locks." | ledger `:642-643` | RATIFIED | — |
| A-14 | `ages.json` "status": "PROVISIONAL"; final thresholds deferred | `Sim.Data/content/ages.json:3, 6`; ledger `:1172` | RATIFIED structure; lists/thresholds PROVISIONAL | Core facts name specific nodes (A2 `cereal_cultivation` … A9 `electricity_generation`, `internal_combustion`); "The loader resolves every reference (research node ids…)" (`adr-031:118-119`). |
| A-15 | `research_count` facts: A8 "min": 200 technology nodes; A9 "min": 280 | `ages.json:149, 168` | FACT@edba2b5 (PROVISIONAL content) | A deeper tree makes these thresholds easier; they must be re-read in any redesign. |
| A-16 | Civics as Age facts (InstitutionalSocial): `law_code` (A3), `census` (A4), `legal_code_roman` (A5 CORE), `coined_wage` (A5), `patent`, `joint_stock` (A7) | `ages.json:50, 70, 85, 92, 129, 130` | FACT@edba2b5 | Under CP, civic pace gates Age entry (D-061.8 PROPOSED). |
| A-17 | "Age advancement is NOT automatic immediately when all milestones are satisfied." / "The PLAYER or AI must explicitly choose to advance." | `d047:241, 247` | RATIFIED | Order kind 7, effective t+1 (`adr-031:88-94`). |
| A-18 | Clicking ADVANCE AGE "makes the new Age's technology/capability environment active." | `d047:255, 263` | RATIFIED | Tension with A-04/A-05 (§12 X-06). |
| A-19 | "Entering a new Age can unlock: new buildings new institutions new infrastructure new units new techniques new applications" | `d043:304-311` (A8) | RATIFIED (LOCKED); D-043 itself flags it as the Age-as-input case of OPEN CR-017 §1 and a collision with D-040 B3 (`d043:339-343`) | Tension with A-05, A-07 (§12 X-06, X-10). |
| A-20 | "Partial completion of an Age's milestone requirements produces NO generic partial Age modifier." | `d043:155` | RATIFIED | — |
| A-21 | "Entering a new Age does not erase old capabilities." | ledger `:682` | RATIFIED | — |
| A-22 | "Exact Age-to-dt values are not frozen." / Age→dt mapping "becomes decisive for calendar pacing" | ledger `:617`; `adr-030:45` | OPEN (`d046:392`) | Pacing levers: population and Age→dt, not costs. |
| A-23 | A revolt-born polity "inherits its parent's CURRENT Age" | `d048:62-63` | RATIFIED | — |
| A-24 | "research completion does not advance an Age, and an Age does not simply unlock a node." | `docs/milestones.md:135-136` | RATIFIED (roadmap record) | "This is not a disguised tech tree." (`:138`). |
| A-25 | Age surge: shape ruled, formula DEFERRED; no numeric effect applied | `d043:171-213`; `ages.json:185` | RATIFIED shape; DEFERRED (DD-14) | No civic/knowledge content may assume a surge number. |
| A-26 | CR-017 §1 "THE AGE AS AN INPUT vs LAW 4" | `docs/adr/cr-017-ages-surge-and-mobile-agents-vs-frozen-items.md:3, 42` | OPEN | — |

---

## 9. Milestone ownership

| # | Rule / allocation | Source | Status | Implication |
|---|---|---|---|---|
| M-01 | Ladder: M6 Knowledge / Research / Technology · M7 Battle Layer · M8 Politics / Diplomacy · M9 Society · M10 Integrated Civilization Simulation · M11+ | `docs/milestones.md:15-28`; `spine-s8:18-21` | RATIFIED ("The rebased order is itself frozen.") | — |
| M-02 | M6 scope: "the Technology and Civics trees; research points (RP) and progression; prerequisites; Eureka / foreign-exposure acceleration; …" | `milestones.md:114-123` | RATIFIED (roadmap level) | Also carries E1 visuals and fog/scouts (D-052, `milestones.md:30-35`) and the one-file export (`:40-43`). |
| M-03 | "M7 — Battle Layer CONSUMES M6's authoritative technology/capability state. No parallel tech vocabulary inside the battle layer" | `milestones.md:93-95` | RATIFIED | Military knowledge content is designed in M6, consumed in M7. |
| M-04 | Movement, Action Points, right-click move → M7 | `docs/d052-m5-integration-audit-rulings.md:46-51` | RATIFIED | Scouts as movers are M7. |
| M-05 | Fog, LIVE/GHOST/UNKNOWN, scouts, sighting memory, extent storage, known-extent restriction → M6 | `d052:53-63` | RATIFIED | See §10. |
| M-06 | Government adoption, active effects, regime change, coups, elections, legitimacy stock, succession, diplomacy, D-019 verb freeze → M8 | `m6-spec:57-58`; D-D `m6-decision-register.md:235-237` | RATIFIED (D-D) / PROPOSED (spec fence) | — |
| M-07 | Unit progression: "semantics, content schema, validation and the M7 contract. No serialized per-unit progression state in M6." | `m6-decision-register.md:238-239` (D-E) | RATIFIED | — |
| M-08 | Training/AP: "contract only. No executable training state, training order or AP-resolution change in M6." | `m6-decision-register.md:240-241` (D-F) | RATIFIED | — |
| M-09 | MobileAgent (scouts, settlers, special people) carrier: M7 creates the first MobileAgent if D-J is ruled as recommended | `docs/m6-plan/m7-handoff-register.md:20, 38`; `m6-spec:347` | OPEN (D-J) | — |
| M-10 | Capital succession → M8; finer population segments → M9 | `adr-033:305-307` (D-050) | RATIFIED | — |
| M-11 | D-051 §2 resources → M6 (with fog); §1(a) → M9; §6-remainder → M10 | `m5-exit-inventory.md:398-401` (R2) | RATIFIED | Resource scarcity work is an M6 item. |
| M-12 | Espionage M11+ ("reconnaissance is stale, espionage is uncertain") | `docs/d039-command-fog-and-siege.md:183-184, 220-225` | RATIFIED | No espionage-based knowledge theft in M6. |
| M-13 | D-039 Parts A/D/E (command friction, siege, campaign) → M7; Part C display → M6 | `d039:6-10`; `d052:53-66` | RATIFIED | — |
| M-14 | Deferred entity tier E (units, castles, military medicine) → M7 | `deferred-entity-realization-plan.md:57-63` | RATIFIED placement | — |
| M-15 | "Never write or modify specs beyond the current milestone + 1. Never implement ahead of the ratified spec." | `CLAUDE.md:28` | RATIFIED | Redesign documents stay M6 (M7 only as handoff contracts). |
| M-16 | "Nothing beyond n+1 is ever written." — the n+1 spec is written only after M(n)'s exit gate | `spine-s8:48-51` | RATIFIED (FROZEN) | Stricter than M-15: during M6 no M7 spec; see §12 X-15. |
| M-17 | Exempt classes: "decision records/ADRs …, the queue, and hotfix specs" | `spine-s8:52` | RATIFIED | A redesign proposal is design input, not a spec; it allocates nothing (`docs/m6-plan/README.md:3-8`). |
| M-18 | Every milestone spec carries the four S8 §4.1 items; T(n).1 is authoritative over §3 | `CLAUDE.md:29`; `spine-s8:72-75, 197-200` | RATIFIED | A redesign entering the M6 spec must update the dimensional declaration and coupling map. |
| M-19 | Ages are a simulation dimension, not development milestones: "no milestone is scoped to an Age" | `milestones.md:140-146` | RATIFIED | Content for all nine Ages belongs to the model; there is no ancient-first development phase. |
| M-20 | "A better way exists" is NOT a conflict — queue it and proceed | `CLAUDE.md:27`; `spine-s8:35` | RATIFIED | Ideas that do not fit frozen items go to `docs/queue.md`. |

---

## 10. Fog of war, discovery, sight, resources, AI information symmetry

### 10A. The ratified model of discovery

| # | Rule (verbatim, ≤25 words) | Source | Status | Implication |
|---|---|---|---|---|
| F-01 | "YOU CANNOT SETTLE WHAT YOU DO NOT KNOW EXISTS" | `docs/d040-discovery-and-control.md:25` | RATIFIED | — |
| F-02 | "not a fog-of-war overlay on a known map; it is a world whose extent is itself discovered." | `d040:30-31` | RATIFIED | Unknown land is absent from what a polity can act on (`d040:37-39`). |
| F-03 | "Settlement, claim and trade all operate only within known extent." | `d040:39` | RATIFIED | S2b gating (`m6-spec:236-237`, PROPOSED). |
| F-04 | "DISCOVERY IS THE SAME MECHANISM AS D-039's RECONNAISSANCE, AT A DIFFERENT SCALE." / "Do not invent a second exploration system." | `d040:41, 49` | RATIFIED | — |
| F-05 | "Once known, land stays known." | `d040:52-53` | RATIFIED | Extent bits never clear (`m6-spec:241`, PROPOSED restatement). |
| F-06 | D-039 B2/B4 "apply to reaching the land, not to retaining the knowledge." | `d040:53-54` | RATIFIED | — |
| F-07 | "NOT DESIGNED HERE: how extent is stored, whether it is per-polity or per-settlement, what granularity it uses" | `d040:82-83` | RATIFIED deferral → D-K OPEN | "one node is 16 × 16 km" (`d040:86-87`). |
| F-08 | "Every number is derived by the packet that builds the mechanism, from a stated reference class." | `d040:180-181` | RATIFIED | Sight budgets need a derivation. |
| F-09 | "INFORMATION QUALITY IS BOUGHT WITH WHATEVER IS SCARCE IN THAT ERA, NEVER WITH MONEY." / "A scout is a person drawn from a bucket" | `d039:50-53` | RATIFIED | Scouting costs food and people (law 1). |
| F-10 | "DIMINISHING RETURNS. Each additional scout adds less than the last." | `d039:57` | RATIFIED | "Derive the curve from a reference class at M6 spec" (`:58-59`). |
| F-11 | "RECONNAISSANCE IS POINTED, NOT GLOBAL. Scouts see WHERE THEY LOOK." | `d039:61` | RATIFIED | — |
| F-12 | "DISTANCE DEGRADES INFORMATION INDEPENDENTLY OF SCOUT COUNT." / "a vantage point extends effective reconnaissance range" | `d039:66, 69-70` | RATIFIED ("present and mild, not punishing", `:68-69`) | Vantage "enters as a budget multiplier" (`m6-spec:127`, PROPOSED). |
| F-13 | "SCOUTS ARE RELIABLE." — "Reports are STALE, not WRONG." | `d039:72-74` | RATIFIED | No false intelligence (espionage is M11+). |
| F-14 | "OPEN QUESTION, NOT RULED: whether scouts are a CLASS …, a state flag …, or an abstract per-polity capability score" | `d039:77-79` | OPEN (D-J) | Recommended: none in M6 (`m6-spec:347`, PROPOSED); "a score is a coefficient with no mechanism behind it (law 2 risk)". |
| F-15 | LIVE / GHOST / UNKNOWN; "A ghost that comes back into observation reverts to LIVE immediately" | `d039:86-96` | RATIFIED; moved to M6 (`d052:57`) | — |
| F-16 | "LAW 4 BINDS ALL OF IT. Nothing above may gate on era, date or turn number." | `d039:43-44` | RATIFIED | Sight/recon capability derives from computed state (literacy, roads, institutions). |
| F-17 | "NO TECHNOLOGY UNLOCK. … a tech-tree node opening sea travel is a calendar gate wearing a tree." | `d040:59-61` | RATIFIED; reconciled by D-047 r7 (classes) | A node may unlock an oceanic transport CLASS (`d047:114-124`); it may not itself grant discovery of a continent. |
| F-18 | "Sea travel extends the network's edge types; it is not a separate movement mode." | `d040:80` | RATIFIED (conflict with ledger §18 open, ledger R4.13 `:1566-1571`) | — |

### 10B. Allocation, seam and storage

| # | Rule | Source | Status | Implication |
|---|---|---|---|---|
| F-19 | M6 work: "full fog; … LIVE / GHOST / UNKNOWN …; scouts …; persisted sighting memory; discovered-extent storage …; the known-extent restriction of settle, claim and trade" | `d052:55-63` | RATIFIED | — |
| F-20 | "closing the information channels named in §3, including the placement mode's legality bit" | `d052:62-63`; channel `docs/queue.md:1473` | RATIFIED (M6 obligation); which world the order is judged against is OPEN (DW-002) | — |
| F-21 | "ONE read seam answers what a polity knows: `PolityKnowledge`" | `docs/adr/adr-037-player-knowledge-seam.md:35` | RATIFIED (`d052:38-44`) | — |
| F-22 | "Its only future input is S2a's stored known extent; the visible set is derived each turn from PREV (never stored)." | `adr-037:40-41` | RATIFIED | — |
| F-23 | "Nothing else may feed it — no UI flag, no difficulty setting, no second table." | `adr-037:41-42` | RATIFIED | Difficulty may not alter fog. |
| F-24 | Open channels recorded for M6: AI valuation of known cells, road routes within 600 km, foreign books of known settlements, terrain bake | `adr-037:136-162`; `d052:42-44` | RATIFIED (closure owed in M6) | — |
| F-25 | AI information leak "accepted until M6's known extent" | `d052:27-29` (ADR-036 §8(h)) | RATIFIED, time-limited | M6 must close it. |
| F-26 | "S2a moves to v35 and S3 to v36" / "kind 10 stays reserved for MoveFormation" | `docs/d055-batch4-reservations-attribution-allocation-ci-persistence-pathfinding.md:1306-1308` | RATIFIED (D-055 accepted at R5, `m5-exit-inventory.md:413-414`) | "First to land" sequencing generalized as D-H is OPEN (`m6-spec:352`). |
| F-27 | No visible set, no extent, `PolityKnowledge.Of` all-known; `Pathfinder.Isochrone` exists with unbounded callers | `m6-foundations-audit.md:72-73` | FACT@edba2b5 | — |
| F-28 | D-K: "per-polity bitset over the 16 km lattice (v-next), monotone OR, sight = `Pathfinder.Isochrone` from controlled settlements and owned formations with ONE TUNE budget" | `m6-ruling-docket.md:37` | OPEN (recommended) | Earlier plan: "There is no scout bonus and no new constant" (`docs/m5w-map-workstream-audit-and-plan.md:733`) — see §12 X-12. |
| F-29 | Extent: "seeded from founding catchments; revolt copies the parent's extent; annexation unions"; `DiscoverySystem` after Catchment, before Colonization, "no RNG" | `m6-spec:230-232` | PROPOSED | — |
| F-30 | Known extent unit: "bits over lattice nodes (65,536 nodes … = 8,192 bytes per polity; monotone OR)" | `m6-spec:128` | PROPOSED | — |
| F-31 | "A deterministic sight flood needs NO stream; a stochastic recon reliability (D-039 B5) would need a new SystemId's stream and move every golden" | `m6-foundations-audit.md:77` | FACT@edba2b5 | Keep sight deterministic unless a ruling accepts the golden cost. |
| F-32 | Fog perturbs quarantined corridors `densityPerArableKm2` and `migrationGrossPerDecade`; "measured, not re-derived" | `m6-spec:153-157, 169` | PROPOSED | — |
| F-33 | D-T: AI assumes unseen terrain "impassable and worthless for valuation (symmetric with the player's `SiteAssessment` withholding)" | `m6-spec:348` | OPEN (recommended) | — |
| F-34 | DEV panels stay world-wide; "Developer view - shows every empire." | `docs/d060-rival-internals-isolation.md:66-77` | RATIFIED (D-060 accepted at R5) | Developer surfaces are exempt from fog; player surfaces are not. |

### 10C. AI information symmetry

| # | Rule | Source | Status | Implication |
|---|---|---|---|---|
| F-35 | "Symmetry. AI actors use player-identical verbs and information class. Difficulty = information and friction, never hidden resources." | `docs/civ-sim-architecture-v3-outline.md:25` | RATIFIED (FROZEN Spine law 7) | — |
| F-36 | "the AI cannot use information it has not legitimately discovered" | `adr-037:19-20` (the Director's P7 addition) | RATIFIED | Applies to research choice and civics strategy too. |
| F-37 | "the AI and the player are NOT yet held to the same information class here." | `adr-037:147-149` | FACT (recorded) | — |
| F-38 | `AiResearchPolicy` and `AiGovernance` "read `IReadOnlyWorldState` directly, NOT through `PolityKnowledge`" | `m6-foundations-audit.md:63` | FACT@edba2b5 | I-9 routes them through the seam (`m6-spec:100-102`, PROPOSED). |
| F-39 | "No hidden map knowledge unavailable to an equivalent player." | `docs/m5-ai-constitution.md:212` | PROPOSED (constitution status unasserted, `docs/design/arch-PQ-conflicts-and-questions.md:847`) | — |
| F-40 | "AI civilizations use identical rules." | civ-progression `:225` | RATIFIED (Spine law 7) | — |
| F-41 | Quarantined test "designed to fail when S2b closes it" | `m6-foundations-audit.md:75` | FACT@edba2b5 | Closing the channel must flip it deliberately. |

### 10D. Resource discovery

| # | Rule | Source | Status | Implication |
|---|---|---|---|---|
| F-42 | "Knowledge of a resource is not simulated (every deposit exists at every settlement from founding and is known wherever its ground is known)"; "no research gates extraction (ADR-033 D1)" | `docs/queue.md:1471` | FACT (recorded, P10) | There is no resource-discovery state to build on. |
| F-43 | "No per-cell resource quantity exists (deposits are written per settlement at founding …)" | `queue.md:1470` | FACT (recorded, P9) | — |
| F-44 | Every deposit good gets "an availability class"; common materials get "an abundance floor"; regional richness "geographically correlated"; "Rare and strategic goods stay rare." | `docs/d051-m5-polish-rulings.md:40-47` | RATIFIED; scheduled M6 (R2); "§2 needs stored known extent (M6.13)" (`d051:142`) | Resource discovery must sit on extent + D-051 §2 classes. |
| F-45 | Shortcut: "it highlights known deposits only; manual access remains; it never reveals unknown tiles." | `d052:102-103` | RATIFIED | — |
| F-46 | "Basic food/resource gathering" is baseline; "No sector is gated" | `d045:43`; `adr-033:38` | RATIFIED | Research may not hide baseline extraction; it may unlock advanced recipes/activities. |
| F-47 | Overlay/place card: "an unknown node is not evaluated"; values whose reach touches unknown ground are "withheld" | `adr-037:174-185, 202-205` | RATIFIED (ADR-037 addenda) | Resource views obey the knowledge seam. |
| F-48 | Rival deposits are among the masked "rival books" | `m6-foundations-audit.md:74` (D-060) | RATIFIED (D-060 accepted) | — |
| F-49 | Eurekas of class `future-system:resources/terrain`: 34 | `research-corpus-audit.md:65` | FACT@edba2b5 | A resource-state design can make these evaluable (coupling to declare). |
| F-50 | "mine -> deposit viability" | ledger `:787` | RATIFIED | Mines (if realized) need deposit-viability, not a cap. |

---

## 11. Persistence, determinism and verification rules

| # | Rule (verbatim, ≤25 words) | Source | Status | Implication |
|---|---|---|---|---|
| D-01 | Banned: "`System.Random` · `DateTime.Now/UtcNow` in sim code · `float` · … iterating `Dictionary`/`HashSet` in sim logic … LINQ in hot paths" | `CLAUDE.md:20` | RATIFIED (FROZEN list, `spine-s8:15`) | — |
| D-02 | "All randomness via `RngRegistry` streams; RNG state lives in `WorldState`." | `CLAUDE.md:20` | RATIFIED | A new drawing system moves every golden (`m6-foundations-audit.md:88`). |
| D-03 | "Any ordering or argmax over double-valued scores uses a composite key with a stable integer tie-break (score, id)" + tie-dense test | `CLAUDE.md:37` | RATIFIED | AI research/civics goal choice, cheapest-available, exposure ordering. |
| D-04 | "Every new serialized row type ships a POPULATED-table test: exact ExpectedLength, bit-exact round-trip, hash equality." | `CLAUDE.md:38` | RATIFIED | CP pool/target/progress, extent, sighting memory rows. |
| D-05 | "Every order-delivery semantic (when an order applies relative to when it was issued) gets its own turn-exact pin" | `CLAUDE.md:39` | RATIFIED | A civics-target order (kind 14, PROPOSED, `m6-spec:274`) needs one. |
| D-06 | "ADDING STATE? Three edits, same file: (1) write in Write, (2) read in Read, (3) width in ExpectedLength." | `Sim.Core/Kernel/CanonicalSchema.cs:14-16` | FACT@edba2b5 (ADR-005 chokepoint, RATIFIED) | — |
| D-07 | Readable set `{32, 33, 34}`; migration is exact "because every table a readable version lacks was APPENDED after every table it has" | `CanonicalSchema.cs:181-195, 197-204` | FACT@edba2b5 | Append tables, never widen rows (risk R-05, `m6-risk-register.md:13`). |
| D-08 | "Persist the minimum authoritative research state" … "Derived availability should be recomputable. Do not serialize unnecessary derived state." | `d044:505-515` | RATIFIED | Availability, stage, EffectiveCost, visible set stay derived. |
| D-09 | "Never rely on unordered dictionary iteration. Use stable IDs and explicit ordering." | `d044:535-537` | RATIFIED | — |
| D-10 | Stable content IDs "are part of the save/order contract." | ledger `:159` | RATIFIED | Keys in orders/rows; never renumber. |
| D-11 | "Every moved golden carries a RE-PIN cause block and an attribution twin" | `m6-spec:103-104` (I-10) | PROPOSED codification; practice: "Re-pin once, itemised, with the measured cause." (`d046:345`, RATIFIED for that pass) | "No single normative doc states the attribution rule" (`m6-foundations-audit.md:86`). |
| D-12 | Re-pinning world goldens "is a Director ruling" | `adr-029:530-531` (gov-4 §5) | RATIFIED practice | — |
| D-13 | Always stops: "any golden moving that the packet did not predict would move" / "any determinism result" | `docs/gov-3-execution-protocol.md:120-121` | PROPOSED (GOV-3 Parts A–B are a CANDIDATE ADR-015 section, `queue.md:736-747`) | — |
| D-14 | Cross-process determinism leg per new order kind; replay twins | `m6-spec:261-264`; CI `determinism-xproc` (`m6-foundations-audit.md:86, 90`) | PROPOSED (spec) / FACT (CI) | — |
| D-15 | "One worktree per verifying agent, never shared" / "No finding is actionable before its verdict returns" | `CLAUDE.md:41`; `docs/adr/adr-015-verification-hygiene.md:181-185` | RATIFIED | — |
| D-16 | Mutant runs bounded; "A mutant that HANGS is itself a finding"; "teeth are not aim" | `CLAUDE.md:42`; `adr-015:193-236` | RATIFIED | — |
| D-17 | "Every guard, clamp, rail, floor, cap or validation ships with a test that FAILS when the guard is removed" | `adr-015:306-308` | RATIFIED | New validators (micro/macro, optional-branch reachability, cross-tree fence) need proven reds. |
| D-18 | "A FINDING IS A MEASUREMENT PLUS AN INTERPRETATION, AND THEY VERIFY SEPARATELY." | `adr-015:490-491` | RATIFIED | — |
| D-19 | "Test results expire when code changes." | `docs/gov-4-repository-freshness.md:141` | RATIFIED (by reference, `CLAUDE.md:12-13`) | — |
| D-20 | "Never rewrite a frozen document because a later decision supersedes part of it." | `gov-4:104` | RATIFIED | Supersession is by dated, append-only notes. |
| D-21 | "Saves break between milestones (D-008)." / unreadable streams recover by "seed + order-log replay" | `CanonicalSchema.cs:20, 192-193` | RATIFIED (D-008) | — |
| D-22 | Determinism list: prerequisite evaluation, eligibility, progress, cost modifiers, Eureka evaluation, completion ordering, unlock application, serialization, enumeration | `d044:523-533` | RATIFIED | — |
| D-23 | Completion order within a step: polity table order, then node index order | `adr-029:493` (R-18) | PROPOSED (implementer resolution) — implemented | — |
| D-24 | Glass Box: "Research state must be observable without mutating simulation state." | `d044:476`; ledger `:126-137` | RATIFIED | Civics and fog queries are read-only. |

---

## 12. Contradictions and tensions between records

| # | Side A | Side B | Status / owner | Note for designers |
|---|---|---|---|---|
| X-01 | One pool, ★ settled: "There is ONE shared Cognitive Load Point pool." (`d044:82`); ledger §5.2/§26 items 2–3; D-I RULED (`m6-decision-register.md:232-234`) | "Civics uses a separate Civics Points pool, distinct from Research Points." (`d061:17`) | OPEN — CR-023 (`cr-023:3`); D-061.1 awaiting signature (`d061:142-143`) | Design both ways or wait; the one-pool rule governs today. |
| X-02 | Overflow LOST: "completion overflow is LOST" — the Director's 2.2 freeze ("Unspent RP after completing the active technology is LOST") (`d047:448-452`; `director-ledger-gap-audit.md:108-109`); ADR-029 B.1: "it is the Director's rule" (`adr-029:742-743`); gap audit G-01 "CLOSED" (`director-ledger-gap-audit.md:946`) | D-R carry recommended as an OPEN decision (`m6-spec:344`; `m6-ruling-docket.md:35`); ADR-029 dated note "remain an open Director decision" (`adr-029:770`); audit A5 (`m6-foundations-audit.md:28`) | Recorded freeze vs re-proposed decision; the M6.1 records (spec D-R, docket, audit A5, ADR-029 note) do not cite D-047 Part B (grep, this digest) | A carry rule would reverse a recorded Director freeze; it needs an explicit ruling. |
| X-03 | "Prerequisites may contain AND/OR/NOT logic." (ledger `:341`) | Loader refuses NOT (`ResearchContent.cs:463-465`); D-044 R8 names AND/OR only (`d044:273-275`) | OPEN — G-33 (`director-ledger-gap-audit.md:687-696`) | Design monotone prerequisites. |
| X-04 | Spine row: "no tree; domain lattice lite" (`civ-sim-architecture-v3-outline.md:84`, FROZEN) | Two research trees (D-044 R3); the Director-authorized rebase note names "Technology and Civics trees" (`outline:113-117`) | D-044 Part D: "Status of every row: UNRESOLVED." (`d044:837`; T2 `d044:844`) | The trees are views of one graph (D-044 R16) — the reading the implementation takes. |
| X-05 | "NO TECHNOLOGY UNLOCK. LAW 4 BINDS." (`d040:59`) | TECHNOLOGY-OWNED class (ledger §21 `:990-992`); research unlocks transport/road CLASSES (`d047:107-150`) | T3 "RESOLVED by D-047" (`d044:874-881`) vs ledger annex R4.12 "OPEN" (`ledger:1556-1564`) | Research may unlock classes/eligibility; it never builds or opens an edge type by itself. |
| X-06 | Research never reads Age; "No Technology or Civics node is unlocked by a change of Age." (civ-progression `:241`; `d044:394`; ledger §26 item 6); ADR-031: "The Age IS read as an input in one place: modernization." (`adr-031:28`) | "Entering a new Age can unlock: new buildings new institutions …" (`d043:304-311`); "makes the new Age's technology/capability environment active" (`d047:263`); "Taxation is an A3 … capability" (`d049:36`) | CR-017 §1 OPEN (`cr-017:42`); ADR-031's "one place" is stale since the tax gate | Reading, not a ruling: Age may gate realization/adoption eligibility (precedent: tax), never node availability or cost. |
| X-07 | "Civics may unlock only institutions" (`adr-029:106-107`; loader `ResearchContent.cs:1240-1242`) | §5.4: civics make "institutional forms, policies and arrangements eligible" (civ-progression `:78`); macro civics = government forms (`d061:23`, DIRECTED); the tax verb is gated by a civic via `sim.json:268` | Implementation narrower than the ruling | Government-form/policy eligibility needs a new representation (content shape, D-V PROPOSED). |
| X-08 | "All data files and every `TUNE` parameter — tuning is play, not amendment." (`spine-s8:24`); `CLAUDE.md:30` | `research.json` is "RATIFIED content" (`d046:367`); content changes are Director decisions (`research-capability-ownership.md:77`, design doc); exit 1 "no node, cost or prerequisite changed without a recorded reason" (`m6-spec:303`, PROPOSED) | Unruled | Cautious reading: calibration constants (U, K, floor, ceiling, RP coefficients) are tunable but never to hit an Age duration (C-23, C-30); nodes, prerequisites and per-node factors are ratified content, so each change needs a recorded reason and a Director decision. |
| X-09 | Final Age "approximately 150-250 turns" is "a hard gameplay calibration target" (`d045:187-191`) | "Technology costs must NOT be changed to force a target Age duration." (`d046:50`); measured 6.3 % of workload in 1,630 turns, A7–A9 unreached (`research-calibration-report.md:103, 115-117`); no knowledge corridor at M6 exit (`m6-spec:140-144`, PROPOSED) | Pacing levers left: population inputs, Age→dt mapping (OPEN, `d046:392`) | A deeper tree widens the gap; costs cannot close it. |
| X-10 | "Completing an Age must not automatically grant every government form." (`d061:24`, DIRECTED) | "Completing an Age grants nothing by itself; in particular no government form." (`m6-spec:95`, PROPOSED); "Entering a new Age can unlock … new institutions" (`d043:306`, RATIFIED) | Unruled; I-6 is stronger than the direction | Ask before writing Age→form coupling either way. |
| X-11 | Spec I-3 and D-061 §4.3 rest on the "arch-O II.2 fence" (`m6-spec:88-89`; `d061:83`) | arch-O: "Nothing in SECTION II is ratified" (`arch-O-invariants.md:13`); II.2 is "PROPOSED (residue only)" (`arch-O-invariants.md:221-224`); D-042 §9.5 once allowed a reserve, superseded by D-044 R20-D (`d044:816`) | PROPOSED basis cited as a fence | The rule that no accumulating scalar (RP or CP) may gate anything is a proposal, not yet a ruling. |
| X-12 | S2a plan: "There is no scout bonus and no new constant." (`m5w-map-workstream-audit-and-plan.md:733`); D-K as first registered: "no new constant" (`m6-decision-register.md:19`) | D-K as now recommended: "budget as one TUNE constant in cost units" (`m6-spec:346`) | OPEN (D-K) | — |
| X-13 | `baseline.exploration` "Scouts / exploration" is baseline (`d045:41`) | Scouts are an OPEN class/flag/score question (`d039:77-79`) and recommended M7 MobileAgents (`m6-spec:347`) | OPEN (D-J) | Basic discovery must work with no scout entity and no research. |
| X-14 | D-039's "M6" means the battle layer (pointer, `d039:6-10`); ADR-037 §1 placed GHOST memory at M7 (`adr-037:22`, corrected by its own note `adr-037:10-12`); m5w S2c "no GHOST until M7" (`m5w:794, 810`) | D-052 §5 gives scouts, LIVE/GHOST/UNKNOWN and sighting memory to M6 (`d052:55-60`) | Resolved by the later ruling (`m6-plan/m6.1-review-package.md:57-59`) | Cite D-052 §5, not the older placements. |
| X-15 | "Never write or modify specs beyond the current milestone + 1." (`CLAUDE.md:28`) | "Nothing beyond n+1 is ever written"; the n+1 spec waits for M(n)'s exit (`spine-s8:48-51`, FROZEN) | S8 governs (frozen) | During M6, nothing M7-or-later beyond handoff contracts. |
| X-16 | The docket and D-061 cite `CanonicalSchema.cs:14-16` for the append-only premise that forbids re-keying civics rows (`m6-ruling-docket.md:30, 74-76`; `d061:153`) | `CanonicalSchema.cs:14-16` is the three-edit rule; the append premise is at `CanonicalSchema.cs:197-204` and concerns appended tables, not row values | Citation drift (found by this digest) | The stronger basis for keeping civics keys is ledger §2.5 "Never renumber existing technology/content keys." (`:161`). |
| X-17 | prereq_complexity: "number of prerequisite atoms crosses the corpus's own prereq_complexity convention (one atom 0.0, two 0.5, three 1.0 — 402 of 430 nodes follow it)" (`d047:467-469`) | "top-level AND requirements count, OR groups count once" (ledger `:425-426`) | FACT@edba2b5: ledger reading fits 432/433 nodes, atom reading 411/433 | Score with the ledger reading. |
| X-18 | ADR-029 status "PROPOSED" (`adr-029:3`; acceptance owed, DW-050 `deferred-work-register.md:90`) | m6-spec lists ADR-029 §10 among "DECISIONS CLOSED … RATIFIED" (`m6-spec:30, 35`) | The rules are RATIFIED through D-044/D-046/D-047; ADR-029's own implementer resolutions stay overridable | Cite D-records for rules, ADR-029 for mechanics. |
| X-19 | ADR-029: an institution-present atom can be added to the stage "without changing engine code" (`adr-029:359-360`) | Loader: the stage is "an AND / OR expression over node ids only" (`ResearchContent.cs:506-507`) | FACT@edba2b5 | A real institutional stage needs a loader change. |
| X-20 | D-009 (FROZEN): bridges/tunnels are "expensive, era-gated, terrain-crossing edges" (`d009-d010-map-population-addendum.md:12`); D-011 (FROZEN): "(era-gated additions: bombard, air strike, dig-in)" (`d011-battle-layer-addendum.md:13`) | Law 4; D-040 B3; D-039 A5 (`d039:43-46`) | CR-009 "was never written" (`director-ledger-gap-audit.md:232`; DW-015/DW-054 `deferred-work-register.md:40, 94`); G-05 OPEN (`director-ledger-gap-audit.md:230`) | Infrastructure/military tech that unlocks these must use computed conditions, not era labels. |
| X-21 | D-047 A.2: Civ VI precedent is level 3 of the decision hierarchy (`d047:384-389`, RULED) | "Use Civilization VI as a source of comparative design lessons, not as permission to copy its content" (`d061:27-28`, DIRECTED) | Compatible but narrower | Cite Civ VI for lessons; author original content. |
| X-22 | civ-progression §3.3 names six lenses (`civilization-progression-architecture.md:45`; STALE per annex R4.7) | ledger §3 names seven, adding KNOWLEDGE (`ledger:172-180`; annex R4.7 `:1520-1526`) | Part I governs | — |
| X-23 | Spec §1.12: "research never reads the Age; modernization automatic and free" lists D-043 A8 as a source (`m6-spec:45`) | D-043 A8 also says Age entry "can unlock" buildings/institutions/units (`d043:304-311`) | Same tension as X-06 | — |

---

## Appendix — status of the main sources (as read at `edba2b5`)

| Source | Status | Basis |
|---|---|---|
| `CLAUDE.md` | RATIFIED (constitution) | — |
| `docs/spine-s8-governance-freeze.md`, `docs/civ-sim-architecture-v3-outline.md`, D-009/D-010, D-011, D-018 | RATIFIED, FROZEN | `spine-s8:13-21, 206` |
| D-044, D-045, D-046, D-047 | RATIFIED Director rulings (calibration values PROVISIONAL) | `d044:7-10`; `d045:3, 8-11`; `d046:3`; `d047:3` |
| D-039, D-040, D-042, D-043 (Parts A–D), D-048, D-049, D-051, D-052 | RATIFIED | each record's status line |
| D-054, D-055, D-060 | accepted at M5 closure (R5) | `m5-exit-inventory.md:413-419` |
| Director ledger Part I | RATIFIED | `ledger:38`; annex R3 `:1429-1483` |
| ADR-028, ADR-030, ADR-036, ADR-037 | RATIFIED | each status line |
| ADR-029 | PROPOSED (acceptance owed) implementing RATIFIED rulings | `adr-029:3`; DW-050 |
| ADR-031, ADR-032, ADR-033 | ACCEPTED for implementation | status lines |
| ADR-015 §6–§7.17 | RATIFIED | `adr-015:179` |
| GOV-4 | RATIFIED by reference | `CLAUDE.md:12-13` |
| GOV-3 Parts A–B | PROPOSED (CANDIDATE ADR-015 section) | `queue.md:736-747` |
| civilization-progression-architecture | mixed: DIRECTOR RULING items RATIFIED; PROPOSED/INFERRED items as labelled | `civilization-progression-architecture.md:15` (label key) |
| `docs/m6-spec.md`, `docs/m6-foundations-audit.md` (findings MEASURED), `docs/m6-plan/*` | PROPOSED (dated RULED lines quoted inside are RATIFIED) | `m6-spec:3-4`; `m6-plan/README.md:3-8` |
| D-061 | PROPOSED; its §1 is DIRECTED | `d061:3-4, 10-11` |
| CR-023, CR-017, CR-005, CR-006 | OPEN | status lines |
| arch-C, arch-D, arch-FGH, arch-O Section II, arch-PQ | PROPOSED (author authority none) | `arch-O:13`, `arch-FGH:11` |
| capability-architecture-decision, milestone-architecture-governance | historical; roadmap SUPERSEDED by the 2026-10-03 rebase (`milestones.md:3-13`) | their `:3` headers |
| research-capability-ownership | RATIFIED requirement (D-046 G3); its classifications are applications: "Nothing here rules anything" | `research-capability-ownership.md:7-10` |
| `Sim.Data/content/research.json` | RATIFIED content (D-046 F§1) | `d046:367` |
| `Sim.Data/content/ages.json` | structure RATIFIED (D-047 r12); lists/thresholds PROVISIONAL | `ages.json:3, 6` |
| `docs/m5-ai-constitution.md` | status unasserted (treated PROPOSED) | `arch-PQ:847` |
