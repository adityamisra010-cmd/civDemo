# CIVILIZATION PROGRESSION ARCHITECTURE

**Status:** ARCHITECTURE — design only. No code, data, constant, test or golden. The Technology Graph data (`tech-graph-v0.6.json`) is unchanged by this document.

**Authority:** Director rulings of this session, recorded here. Where a ruling amends a ratified record, the amendment is named in §18 and §19 rather than applied silently.

**Depends on two records, both now on `main`:** ADR-019 is merged on `main` as `0dd4bd2`. ADR-028 was committed on `adr-028-viability-and-saturation` as `c1d1648` and merged to `main` through PR #8 as `3bba69d`. Citations below labelled RATIFIED by ADR-019 or ADR-028 are therefore RATIFIED.

**Related open change records on `main`:** CR-005 (placing research, technology and institutions at M5 conflicts with the frozen milestone order — OPEN) and CR-006 (a 10,000 BCE epoch and continuous simulation conflict with the frozen kernel contract and Charter — OPEN). This document does not resolve either.

**Reconciled 2026-09-30 with D-044** (`docs/d044-research-progression-rulings.md`, Director rulings of 2026-09-30, recorded verbatim). D-044 R20 directed that §3.2, §3.4, §4, §8, §8.2, §8.3, §8.4, §8.4.2 and §8.5 be corrected, together with the wording on literacy, on completion and on Civics. Those passages are rewritten in place below and labelled **D-044**. Every replaced passage is preserved verbatim in **§20**, so the audit trail is intact. The implementation of these rulings is recorded in `docs/adr/adr-029-research-engine.md`. This document itself still contains no code, data, constant, test or golden.

**Provenance labels:** RATIFIED (cite) · DIRECTOR RULING (this session; not previously in the tree) · MEASURED (cite tree) · PROPOSED · INFERRED · DIRECTOR DECISION REQUIRED · UNVERIFIED.

---

## 1. Purpose and scope

**1.1** Define the single graph through which a civilization develops, the player-facing views onto it, the resources that drive it, and how its nodes become real. **DIRECTOR RULING.**

**1.2** Out of scope: node content, formulas, costs, constants, World Congress mechanics, Tourism, and any simulation system. Each is named in §19 where it is owed. The research engine that implements §3 and §8 is specified in ADR-029, not here.

## 2. The Civilization Progression Graph

**2.1** There is **one** authoritative Civilization Progression Graph. Every progression domain lives in it. Nodes may depend on nodes in any domain. **DIRECTOR RULING.**

**2.2** A node's **domain** says what kind of thing it is. A node's **realization model** says how the civilization acquires it. These are independent properties (§7). **DIRECTOR RULING.**

**2.3** The graph records dependency. It never records coordinates, rendering, or visual morphology (§15). **RATIFIED** by ADR-019 and the World Vocabulary chain; **MEASURED** clean in v0.6 — no technology field encodes position or rendering.

**2.4** Consistent with D-042 §9.7: *"Do not collapse these concepts into a single rigid technology tree."* **RATIFIED.** One graph is not one tree: it is a dependency network viewed through several lenses.

## 3. Graph, trees, branches and lenses

**3.1** A **tree** is a filtered view of the graph, not an independent structure. **DIRECTOR RULING.**

**3.2** Exactly two views are progression trees — things the player directs research into. Both are driven by the **one shared Cognitive Load Point (CLP) pool** (§8):
- **Tree 1 — Technology.** Technology *is* the civilization's knowledge (§4). The tree is a **Main Technology Tree** (the trunk) plus exactly five specialized subtrees: 1.1 Military, 1.2 Medicine, 1.3 Engineering, 1.4 Natural Science and 1.5 Agriculture (§8.4).
- **Tree 2 — Civics.** It has its own content and semantics (§5) and no numbered subtrees.

Nothing else is a player-facing tree. There is no Tree 3, no Industry & Energy tree, no Naval or Science tree, and no 1.3.1. Internal prerequisite branches inside a tree are allowed; they are not additional numbered trees. **DIRECTOR RULING — D-044 R2, R3, R12.**

**3.3** The remaining domains — Techniques, Institutions, Infrastructure, Industry, Military, Applications — are **lenses**: views of what the civilization can do or has, realized through their own mechanisms (§7, §9). **DIRECTOR RULING**, qualified by §7.2: researchability belongs to the node, so a lens may still contain a researchable node.

**3.4** A **subtree** (also called a *branch*) is a subdivision of Tree 1, not a separate tree. There are five, and they are **views** of the one Technology graph. A node sits in exactly one of them, or in the Main Technology Tree, and is never duplicated. All five draw on the same CLP pool as the Main tree and as Civics. They may depend on each other and on any domain. **DIRECTOR RULING — D-044 R3, R15, R16;** it continues the earlier ruling *"not six independent technology trees — research branches within the Technology/Knowledge progression."* D-044 has since reduced the count from six to five (§20).

**3.5** One word on two axes. **Military** names both a Tree-1 branch (researched military knowledge) and a lens (units, doctrine, academies and military applications that knowledge makes possible). The same node can appear in both views; it is never duplicated. **INFERRED** from §3.3 and §3.4.

**3.6** A cross-domain prerequisite is shown and referenced from every view that needs it, never copied into a second tree. **DIRECTOR RULING.**

## 4. The eight domains

| domain | what it represents | typical realization |
|---|---|---|
| **Technology** (the civilization's knowledge) | scientific, technical, accumulated knowledge | research (CLP) |
| **Civics** | political, governmental, legal, administrative and social organization | research (CLP — the same pool), then adoption |
| **Techniques** | practical methods of production; may emerge from knowledge but are not it | practice, or research where codified |
| **Institutions** | persistent organizational capability | establishment and maturation |
| **Infrastructure** | constructed physical systems and networks | construction |
| **Industry** | organization of production and industrial capability | establishment, construction, labour |
| **Military** | military technology, organization, doctrine, capability | research, recruitment, organization |
| **Applications** | concrete uses arising from combinations of the above | realized when prerequisites hold |

**DIRECTOR RULING.** No domain is added to imitate another game's interface.

**4.1 Technology = knowledge — DIRECTOR RULING, D-044 R1.** A Technology node is a discrete body of civilization knowledge. Completing it means the civilization now possesses that knowledge, and the node's declared capabilities and unlocks become available. There is no separate Knowledge resource, Knowledge domain or Knowledge tree. The civilization's **knowledge base** is exactly the set of its completed Technology and Civics nodes. Individuals do not own it: when a scientist dies, knowledge the civilization has acquired remains.

## 5. Civics as a first-class domain

**5.1** Technology answers *what does the civilization know?* Civics answers *how does it organize and govern itself?* Civics is not a Technology category. **DIRECTOR RULING.**

**5.2** Civics may eventually cover forms of government, political and administrative organization, legal systems, citizenship, representation, taxation and governance mechanisms, bureaucracy, social and political institutions, diplomatic norms, and international governance concepts. **No node list is fixed here. DIRECTOR RULING.**

**5.3 The Civ VI policy-card model is not adopted.** Civ VI civics unlock slottable cards with flat effects (+100% Culture from a building class; +4 combat strength under a government). ADR-019 Part 3 forbids deterministic modifiers — *"Steel Policy +20 → 20% more steel plants"* — and law 2 forbids free-floating permanent buffs. **RATIFIED constraint.**

**5.4** A Civic completion yields a political or governance **capability** that makes institutional forms, policies and arrangements **eligible**. Adoption is a separate act with its own conditions. **DIRECTOR RULING:** *"RP → civic completion → political/governance capability → institutional eligibility → actual institutional adoption, rather than … +5% arbitrary bonus."*

**5.5** Government type is a strategic operating environment affecting legitimacy, execution, corruption, protest, suppression, foreign perception and trade — as mechanism, not modifier. **RATIFIED:** ADR-019 §7.2. Civics is where government forms become eligible; ADR-019 governs what they do.

**5.6** Civics realization connects to M7's *"institutions as modules, regime change, coups/revolts"* (Spine `:111`). **RATIFIED home.**

**5.7 Civics reclassification (PROPOSED at `93270cd`; APPLIED by the ADR-029 migration):** several entries the v0.4 registry filed as institutions read as Civics under this ontology — written law (`law_code`), Roman systematic law (`legal_code_roman`), census, coined wage labour, patent, joint-stock company. The migration made these six the Civics tree's nodes (keys 1001–1006, ADR-029 §2.3; D-044 R12). Each registry institution stays an institution whose knowledge requirement is now its civic. No other Civics content was invented. (Original wording in §20.)

## 6. Cross-domain dependencies

**6.1** Any node may depend on nodes in any domain. **DIRECTOR RULING.** Illustrative shapes only, not content:

```
Technology → Technique → Institution → Application
Civics → Institution → Infrastructure → Application
Technology + Industry + Institution → Application
Technology + Civics → administrative capability
```

**6.2** A dependency edge means **causal** dependency — *could B exist without A?* — never chronology. **RATIFIED** by the Technology Graph critique record (v0.5: 25 chronology-as-dependency edges removed).

**6.3** The graph must stay acyclic in its **prerequisite** edges. Feedback between domains — universities raising research, which enables universities — runs through **simulation state**, not through prerequisite edges. **INFERRED** from the v0.4 validator rule and D-021.

## 7. Realization models — researchability is a node property

**7.1 DOMAIN ≠ MECHANISM. DIRECTOR RULING.** Each node declares a realization model. Candidates, not a closed set:

| model | means | typical domains |
|---|---|---|
| **researched** | progressed with Cognitive Load Points (the one shared pool) until complete | Technology, Civics; a codified Technique |
| **established** | founded when conditions hold, then matures | Institutions, some Industry |
| **constructed** | built by the construction system | Infrastructure, buildings |
| **practised** | emerges from doing the work | Techniques |
| **realized** | exists whenever its prerequisites hold | Applications |
| **recruited / organized** | raised from population and resources | Military units and formations |

**7.2** Researchability is **not** a blanket property of a domain. A Technique may be researchable where it represents a meaningful body of codified knowledge. An Institution is generally established, not researched. Infrastructure is constructed. An Application is generally realized, not researched. **DIRECTOR RULING.**

**7.3** Completion of a researched node is **immediate for the knowledge itself**. The civilization possesses the knowledge at completion, and the node's declared capabilities and downstream eligibility unlock then. There is no delayed knowledge-realization phase (**DIRECTOR RULING — D-044 R11**). Completion never directly creates a world entity: it makes downstream entities **eligible / AVAILABLE**. Their realization happens in the owning mechanism through the lifecycle **LOCKED → AVAILABLE → UNDER_CONSTRUCTION → ACTIVE → MATURE → SATURATED/DIMINISHING**. **RATIFIED:** ADR-028 §3 (DD-13, DD-14); **DIRECTOR RULING — D-044 R14.**

**7.4** Availability may also be gated by resources, sites, settlement conditions, institutions or other buildings, and some entities are baseline with no technology gate at all. **RATIFIED:** ADR-028 §3; Director ruling of v0.4.1.

## 8. Progression resources and the research relationship

### 8.1 The Cognitive Load Point pool — DIRECTOR RULING (D-044 R2)

```
Population + education + literacy + institutions + health + specialization + connectivity
                                   ↓
                          Cognitive capacity
                                   ↓
            COGNITIVE LOAD POINTS (CLP) — one pool, a rate per sim-year
                                   ↓
                 ONE active research target at a time
       (a Main-tree, subtree or Civics node — the player's choice)
                                   ↓
          progress on that node; partial progress is kept per node

   Eurekas credit their own node directly, whatever the active target is (§8.6)
```

**8.1.1** **Cognitive Load Points (CLP)** is the authoritative name of the one research resource. It replaces both "Research Points" and the earlier term "Cognitive Pool". Everything researchable consumes the same pool: Technology, the five Technology subtrees, and Civics. There are no Science, Culture, Military, Medical, Engineering or Knowledge points, and no second accumulated research currency. **DIRECTOR RULING — D-044 R2, R12.**

**8.1.2** Population is an **input**, not a linear multiplier. *"Cognitive Pool = Population × constant"* is forbidden. The pool is a function of population, education, literacy, institutions, health, specialization and connectivity, with diminishing marginal contribution from population. **DIRECTOR RULING** (unchanged). No functional form, coefficient or illustrative number is adopted here. **D-045 §2 (2026-10-01)** gives the Director's calibration anchors — population 100 → 2 RP and 1,000 → 10 RP per turn — and a PROVISIONAL CALIBRATION curve through them, RP(P) = 2·(P/100)^0.69897. ADR-029 §6 implements it over total population, the only one of these inputs that exists in simulation state. The others become FUTURE modifiers when their systems exist, and the per-turn reading is OPEN (CR-018).

**8.1.3** The player does **not** split the pool between channels or between nodes. They choose where the current CLP throughput goes by selecting **one active target**. Manual 70/30, 60/20/20 or any other percentage allocation is not part of the model. Institutions act on research through **cost** (§8.5), not by adding flat CLP. **DIRECTOR RULING — D-044 R9, R5.**

**8.1.4** CLP, Diplomatic Favor and Diplomatic Victory Points are three distinct quantities and must not be conflated. Production and construction capacity is a fourth, separate thing. "Science" and "Culture" are not progression quantities. **DIRECTOR RULING** (the four-quantity ruling, as amended by D-044 R2).

### 8.2 Accumulation — RULED (D-044 R20-D)

There is **no general bank or reserve** of unused research points. The only retained research quantity is **partial progress, stored per node**. Throughput that reaches no node is not stored. This resolves the earlier conflict with D-042 §9.5 in favour of the later ruling. D-042 §9.5 is superseded, and D-042 carries an append-only note that points here.

### 8.3 Concurrency — RULED (D-044 R9, R20-E)

There is **one active allocation target at a time**. **Multiple nodes may retain partial progress.** Changing the target never erases progress. If Technology A stands at 60 / 100 and the player switches to B and later returns, A still stands at 60 / 100. There are no multi-allocation percentages and no queue. D-042 §9.3 and the §12 queue bullet are superseded to that extent, and D-042 carries append-only notes.

### 8.4 Tree 1 subtrees — DIRECTOR RULING (D-044 R3, R4, R15)

**8.4.1** Tree 1 is the **Main Technology Tree** (the trunk) plus exactly **five** specialized subtrees:

| # | subtree | covers |
|---|---|---|
| 1.1 | **Military** | military and naval knowledge |
| 1.2 | **Medicine** | medical and biological-medical knowledge |
| 1.3 | **Engineering** | materials, construction, mechanical and civil engineering, transport and communications; and — by implementer resolution, not a ruling (see below) — industrial production, power generation, energy systems, manufacturing and industrial processes |
| 1.4 | **Natural Science** | the physical, chemical and life sciences as bodies of knowledge |
| 1.5 | **Agriculture** | cultivation, breeding, food production |

There is no Industry & Energy subtree (**DIRECTOR RULING — D-044 R3, R20-F**). D-044 removes the subtree but does not say where its knowledge goes. Placing it in Engineering, or in the trunk if it precedes the research stage, is **implementer resolution ADR-029 §13 R-8 — PROPOSED, overridable by the Director**. Its reason is the corpus: the v0.6 primary domains `industry` and `energy` have no ruled subtree, and Engineering is the ruled subtree whose coverage (materials, mechanical engineering) they extend. Heavy cross-dependency between subtrees is expected: Engineering → steam engine → industrial machinery, with prerequisites drawn from Natural Science.

**8.4.2 Opening.** The five subtrees become available **together**, as research subtrees, when the civilization reaches the **university / research institutional stage**. The trigger is the relevant education, university and research capability and institutional state. It is **not** `Age == X`, and there is no faculty-by-faculty progressive opening. Once the stage exists, the player can research all five using the same CLP pool. The stage exists specifically to support specialized universities (§8.5). **DIRECTOR RULING — D-044 R4, R20-G.** ADR-029 §8 records how the trigger is evaluated from the state that exists today.

**8.4.3** An Age transition never opens a subtree. Age gives historical context and constrains plausibility only. **DIRECTOR RULING;** consistent with law 4. **RATIFIED.** Also D-044 R13.

**8.4.4** Experimental Method remains an institution. It is not converted to a technology to serve as a trigger. **DIRECTOR RULING.**

**8.4.5** Before the stage, all Tree-1 knowledge sits in the **Main Technology Tree**. Nodes that logically belong to the trunk stay in the trunk after the stage as well: not every node is forced into a subtree (**D-044 R15**). The classification of the existing corpus into trunk and subtrees is recorded in ADR-029 §8.

**8.4.6** Each subtree may terminate in repeatable frontier lines — Military in military and cyber research, Medicine in biomedical research, and so on. **INFERRED** from the v0.6 frontier design; the mapping is **PROPOSED**. The former Industry & Energy line folds into Engineering. Repeatable levels conflict with idempotent completion (D-044 R11) and are an **open** question (D-044 Part D, T6).

### 8.5 Specialized universities — DIRECTOR RULING (D-044 R5)

**8.5.1** Specialized universities — Medical, Engineering, Military, Natural Science, Agricultural — are institutions / specializations. Their research effect is to **reduce the effective research cost** of the relevant Technology nodes:

```
BaseCost  →  relevant specialized-university modifiers  →  EffectiveCost
```

They do not create a separate research currency, do not add flat global CLP and do not create a separate research queue. The exact numerical scaling formula is **not ratified**. The modifier architecture must be able to carry diminishing returns, university maturity and local viability later (ADR-028 §1–§2, DD-13, DD-14). There is no universal `CapabilitySystem`. **DIRECTOR RULING — D-044 R5, R20-H.**

**8.5.2 What stands between the rulings and working universities.** The three kinds of dependency are distinguished as D-044 R20-I requires:

- **Architecture dependency — D-021, RATIFIED.** "Research → cheaper university research → more universities" is a positive feedback loop. D-021's paired-feedback rule requires a brake that strengthens with amplitude, in the same milestone as the loop. The brake is owed by the packet that **closes** the loop, which is the first packet that writes a university cost modifier. It is not owed by the research engine, whose cost seam carries no university term until an institutions system writes one.
- **Implementation dependency.** A university modifier needs a system that establishes and matures university institutions and computes their viability. **No institutions system exists** (CR-010, "the definition of institution", was never written). Literacy is **not** required by any ratified system for research itself.

  The previous wording of this section borrowed arch-M's finding **C-7** (`docs/design/arch-M-cross-system-graph.md:318`). C-7 is a *different* loop: practice → holding → practice. For that loop arch-M names three proposed brakes. One of them is ratified: D-021's clause *"Education is a flow with a lag …"*. arch-M then records, as **INFERRED**, that this brake runs through literacy and is *"currently unsatisfiable, not merely unbuilt"*.

  The label "MEASURED / RATIFIED" that this section carried for that unsatisfiability over-stated its source. Only the D-021 clause itself is ratified. Whether the brake for the university loop must also run through literacy is **not ruled**, and no literacy mechanic is invented.
- **Systems not yet implemented.** Institutions, universities, education and literacy.

The research engine and its BaseCost → EffectiveCost seam are therefore built, and they are not blocked by the missing systems. Only the university **writer** waits for the institutions system and its brake. **DIRECTOR RULING — D-044 R20-I; RATIFIED constraint** (D-021).

**8.5.3** A larger population raising the CLP pool, which raises growth, is a loop that D-021 governs. Diminishing marginal contribution (§8.1.2) is a damping term, not by itself the ratified brake. **INFERRED.** In the implemented engine research feeds nothing back into population, so this loop is not closed in the tree either.

### 8.6 Eureka — DIRECTOR RULING (D-044 R10)

A Eureka condition accelerates **its own node**, independently of whether that node is the active target. The credit is applied to that node. It is capped at the node's remaining cost and never overflows to another node. Eureka conditions use the existing deterministic predicate architecture (D-020), not a second condition language.

## 9. Institutions, infrastructure and industry

**9.1** These are realized through establishment, construction and labour, subject to the ADR-028 lifecycle and viability model: hierarchical and type-specific (DD-13), saturation through thresholds and diminishing returns (DD-14), no universal cap and no universal `CapabilitySystem` (D-042 §7.3, §12). **RATIFIED.**

**9.2** Building = physical host; institution = organizational or social function; institutions may be hosted by buildings and are never independent map entities when their manifestation is through a host. **RATIFIED:** Director ruling of v0.4.1.

**9.3** The shipped construction system has no availability predicate (`ConstructionSystem.cs:92-97`), and construction output is inert (arch-E F23, re-verified on `de5e00e`). **MEASURED.** Eligibility from the graph requires a predicate the simulation does not yet have.

## 10. Military progression

**10.1** Military knowledge is researched in the Tree-1 **Military** branch. Units, formations, doctrine and military institutions are realized through recruitment, organization and establishment. **DIRECTOR RULING.**

**10.2** D-011 owns unit definitions; the graph records only unlock relationships. Formations are token properties; units change and verbs do not. **RATIFIED:** D-011.

**10.3** AI civilizations use identical rules. **RATIFIED:** Spine principle 7 (`civ-sim-architecture-v3-outline.md:25`).

## 11. Applications — the capability layer

**11.1** Applications are concrete uses that arise from combinations of knowledge, techniques, institutions, infrastructure, industry and military capability. They are generally **realized** when their prerequisites hold, not researched. **DIRECTOR RULING.**

**11.2** The chain *Technology → Capability → Application → simulation state → observable world state → visualization* is preserved. **RATIFIED** (World Vocabulary architecture).

## 12. Ages

**12.1** The nine Ages — A1 Prehistoric/Stone, A2 Neolithic/Agricultural, A3 Bronze, A4 Iron, A5 Classical/Imperial, A6 Medieval, A7 Early Modern, A8 Industrial, A9 Modern/Contemporary — are authoritative. **No tenth Age.** **DIRECTOR RULING.** **Previously recorded on branch `claude/civdemo-work-b1z2y4`, not on `main`:** D-043 (`docs/d043-civilization-progression-ages-and-mobile-agents.md:56-86`, commit `5725605`) records the nine Ages verbatim as a Director ruling of 2026-09-29. This document is the first record of them on `main`. **MEASURED:** the legacy Trees content on `claude/civdemo-work-b1z2y4` defines six explicitly placeholder ages (`AGE_00`–`AGE_05`, `ages.json`: *"PLACEHOLDER AGES. No historical names, dates, milestone definitions or transition rules"*).

**12.2** The shipped `Sim.Data/content/era-pacing.json` defines **seven** dt bands — Neolithic, Bronze/Iron, Medieval, Early Modern, Industrial, Modern, Information+ — starting at −4000. **MEASURED.** These are implementation state, not the authoritative Age model. **DIRECTOR RULING.**

**12.3** The eventual relationship is Global Age → calendar/dt policy, one policy per Age. The mapping and every value are **not decided here** (§19). **DIRECTOR RULING.**

**12.4** No Technology or Civics node is unlocked by a change of Age. **DIRECTOR RULING; RATIFIED** (law 4).

**12.5** *"Partial Age progress has no direct mechanical effect"* is cited as an earlier ruling. **MEASURED:** no record of it exists on `main`. It is recorded, not on `main`, in D-043 A4 on branch `claude/civdemo-work-b1z2y4` (`docs/d043-civilization-progression-ages-and-mobile-agents.md:151-170`): *"Partial completion of an Age's milestone requirements produces NO generic partial Age modifier."* Recorded here as a Director statement.

**12.6** Most A1 and much A2 knowledge predates the shipped campaign start (−4000), so under the current simulation those nodes are starting holdings rather than researched in play. This sits against *"the game begins in the Stone Age."* **MEASURED tension.** It is the subject of the open **CR-006** (a 10,000 BCE epoch against the frozen kernel contract and Charter); this document defers to that record.

**12.7** The v0.6 graph places frontier nodes in an age value `F`. Under §12.1 that is a tenth Age and must change (§17). **MEASURED defect.**

## 13. World Congress and diplomacy

**13.1** Diplomatic progression does **not** become a ninth progression tree. **DIRECTOR RULING.**

**13.2** World Congress is a first-class **international institution / system**. It may later connect to the graph as follows, none of it designed here:
- **Civics** makes forms of international participation eligible — diplomatic norms, international governance concepts.
- **Institutions** — World Congress is itself an institution, established when its conditions hold.
- **Applications** — resolutions and treaties as realized capabilities.
- **International conditions** — contact and mutual recognition among polities (D-037 A3's three quantities).
**PROPOSED.**

**13.3** Diplomatic Favor (spent on World Congress votes), Diplomatic Victory Points, and diplomatic relations are three distinct concepts. **DIRECTOR RULING.**

**13.4 Two Civ VI mechanisms are not adopted.** Civ VI's Congress convenes from the Medieval era — an era gate, forbidden by law 4 — so ours convenes from computed conditions. Civ VI meets every 30 turns; our dt varies from 10 years to 0.5, so cadence must be in sim-years or event-driven. **RATIFIED constraint** (law 4; `era-pacing.json`).

**13.5** D-037 C5 (ratified) forbids any timer, event or arbitration that exists merely to clear a dispute, and justifies it with *"There is no win condition to serve."* Victory conditions make that justification false. The prohibition stands independently; World Congress must not become a dispute-clearing mechanism. **RATIFIED constraint; the C5 justification clause is DIRECTOR DECISION REQUIRED** (open from the victory-conditions ruling).

## 14. Culture and Tourism

**14.1** Culture is **not** a progression channel. Civics is driven by the **same CLP pool** as Technology, and there are no Culture Points and no Culture allocation (**DIRECTOR RULING — D-044 R12, R20-K**). Culture as a progression quantity does not exist. The earlier ruling that it is not Tourism and produces no Culture Victory still stands (§14.2).

**14.2** Culture Victory is removed, and no Tourism placeholder is created, until a genuine cultural-attraction simulation exists to support it. **DIRECTOR RULING.**

**14.3 Naming — MOOT for progression since D-044** (there is no Culture progression channel to name). The paragraph below is kept because it still governs any future use of the word. **Original heading: DIRECTOR DECISION REQUIRED (conflict).** *Culture* is already a shipped mechanical term: the population bucket dimension (`CultureId`, `reg.Cultures`; `WorldFounding.cs:51-88`; Spine `:44`), and the subject of D-041 attachment. CONV-1's claimant rule gives the bare word to the shipped system. The progression channel therefore needs a namespaced internal name, such as `civic culture` or `culture (progression)`. The player-facing label may still read "Culture". The Director's own note — *"Culture is not the same thing as cultural identity"* — is the reason.

**14.4** Moot since D-044: there is no Culture generation model, because Civics consumes CLP (§8.1.1).

## 15. World Vocabulary

Progression defines capability and dependency. Simulation systems implement capability. World Vocabulary defines observable manifestation. Visualization reads simulation and world state. No progression node carries sprite coordinates, rendering instructions or visual morphology. **RATIFIED.**

## 16. Legacy Trees UI

**16.1** The legacy Trees graph is **MEASURED** on the unmerged branch `claude/civdemo-work-b1z2y4` (tip `6dded01`, 22 commits ahead of `main`, last commit 2026-09-30): content at `Sim.Ui/UiContent/trees/the-trees.json` — **31 nodes, 22 edges, 7 lenses, 12 node types, 13 relation kinds** — rendered by `Sim.Ui/Trees/` and covered by `TreesContentTests`, `TreesReviewRegressionTests` and `TreesScreenTests`. It does not exist on `main`. It is legacy/demo and non-authoritative (**DIRECTOR RULING**) and is not to be deleted.

**16.1.1 Warning.** This branch was earlier listed among remote branches pending deletion through the GitHub UI. It is active and holds the only copy of the Trees UI. It must not be deleted.

**16.2** The future progression UI renders filtered views of the one graph (§3). **DIRECTOR RULING.**

## 17. Schema evolution from tech-graph-v0.6

No change is applied by this document. Required, in order: **PROPOSED.**

1. **Rename the container** — Technology Graph → Civilization Progression Graph; `technologies[]` → `nodes[]`.
2. **Add `domain`** — one of the eight (§4). Every current node is `technology` until reclassified.
3. **Add `realization`** — one of the models in §7.1.
4. **Add `tree` and `branch`** — `tree` ∈ {technology, civics} for researched nodes; `branch` ∈ the **five** (§8.4) or null for Main-tree nodes; the research-stage opening condition recorded as first-class data. *(Applied by the ADR-029 migration.)*
5. **Remove age `F`** — frontier nodes become A9 with `frontier: true`; the viewer renders them as a sub-band of A9 (§12.7). *(Applied by the ADR-029 migration; the corpus file itself is unchanged.)*
6. **Promote registry entities to graph nodes** — the 28 institutions, 24 infrastructure and 23 units become nodes in their domains, keeping their stable namespaced IDs. The 30 reclassified techniques become Technique nodes. The 8 world activities and the capability text become Application nodes.
7. **Reclassify** the Civics candidates in §5.7. *(Applied by the ADR-029 migration: the six candidates are the Civics tree's nodes.)*
8. **Start the Civics tree** — schema only; no node list is fixed by this document.
9. **Industry domain** — currently has no entities, only tags.
10. **Validator** — extend to domains, realization models, branch opening conditions, and cross-domain acyclicity. Retain the reverse-index agreement check. Add a project reverse index (open gap from the v0.6 audit).
11. **Resolve the v0.6 audit defects first** — the filler Eurekas, four wrong edges and projects reverse index recorded in the v0.6 audit — before any reclassification, so the migration starts from a clean graph.

## 18. Ratified, ruled, proposed and unresolved

| item | status |
|---|---|
| One graph; trees are views; branches within Tree 1 | DIRECTOR RULING |
| Eight domains | DIRECTOR RULING |
| Domain ≠ realization mechanism | DIRECTOR RULING |
| Civics first-class; no policy-card modifiers | DIRECTOR RULING; RATIFIED constraint (ADR-019 Part 3, law 2) |
| One Cognitive Load Point (CLP) pool; population as a non-linear input | DIRECTOR RULING (D-044 R2) |
| Technology = knowledge; Civics on the same CLP pool; no Science or Culture quantities | DIRECTOR RULING (D-044 R1, R2, R12) |
| Two trees; Main Technology Tree + five subtrees opening together at the university/research stage; no Age trigger | DIRECTOR RULING (D-044 R3, R4) |
| Experimental Method stays an institution | DIRECTOR RULING |
| Lifecycle, viability, saturation | RATIFIED (ADR-028) |
| Nine Ages authoritative; dt bands are implementation | DIRECTOR RULING (first record on `main`; previously D-043 on `claude/civdemo-work-b1z2y4`) |
| Tourism and Culture Victory out | DIRECTOR RULING |
| No general bank of unused CLP; partial progress per node | **RULED** — D-044 R20-D; D-042 §9.5 superseded (append-only note) |
| One active target; many nodes keep partial progress | **RULED** — D-044 R9; D-042 §9.3, the §12 queue bullet and the §15 rows superseded (append-only notes) |
| "Culture" as a bare internal term | **MOOT** for progression (no Culture channel); CONV-1 still governs the word |
| D-037 C5 justification clause | DIRECTOR DECISION REQUIRED |
| Legacy 31-node Trees UI | MEASURED — branch `claude/civdemo-work-b1z2y4` |
| Partial-Age-progress ruling | MEASURED — not on `main`; D-043 A4 on `claude/civdemo-work-b1z2y4` |
| ADR-019, ADR-028 | ADR-019: merged on `main` (`0dd4bd2`). ADR-028: committed on its dedicated branch (`c1d1648`), merged to `main` through PR #8 (`3bba69d`) |
| Specialized-university mechanic | **Cost seam built** (ADR-029). The university **writer** waits for an institutions system and its D-021 brake (§8.5.2) |

## 19. Open decisions

1. ~~Accumulation~~ — **RULED** by D-044 R20-D (§8.2).
2. ~~Concurrency~~ — **RULED** by D-044 R9 (§8.3).
3. ~~Culture naming~~ — **MOOT** (§14.3).
4. ~~Culture generation~~ — **MOOT** (§14.4).
5. **CLP function** — inputs, form and constants, each derived from a stated reference class (§8.1.2). ADR-029 ships a provisional, population-only form, labelled chosen and not derived. **Still open.**
6. ~~Branch triggers~~ — **RULED** by D-044 R4: one stage, all five subtrees (§8.4.2). How the institutional half of the stage is evaluated once an institutions system exists is ADR-029 §8.
7. **Age → dt mapping** — reconcile the nine Ages with the seven shipped dt bands (§12.3).
8. **Campaign start** — Stone Age start against the shipped −4000 Neolithic start (§12.6).
9. **Civics node content and adoption mechanics** (§5).
10. **World Congress** — convening conditions, cadence in sim-years, favor and point sources (§13).
11. **D-021 brake** — owed by the first packet that writes a university cost modifier (§8.5.2). Literacy is a candidate, not a requirement. **Still open.**
12. **D-037 C5** — supersede the justification clause, keep the prohibition (§13.5).
13. **Legacy Trees UI** — located (§16.1); decide its future relative to this graph. Do not delete its branch.
14. **CR-005 and CR-006** — both OPEN; the milestone placement of this progression system and the campaign epoch are ruled there, not here.

## 20. Supersession record — 2026-09-30 (original wording preserved)

D-044 R20 directed the corrections above. Every passage that was replaced is reproduced here **verbatim**, in document order, so the pre-reconciliation text of `93270cd` is recoverable from this file alone. Additive notes are not listed. These are the header notice, §4.1 and §8.6 (new text), and the "Applied by the ADR-029 migration" annotations in §17.

### §1.2 — original

````markdown
**1.2** Out of scope: node content, formulas, costs, constants, the Culture generation model, World Congress mechanics, Tourism, and any simulation system. Each is named in §19 where it is owed.
````

### §3.2 — original

````markdown
**3.2** Two views are progression trees — things the player directs effort into:
- **Tree 1 — Technology / Knowledge**, driven by the Science allocation of the Cognitive Pool (§8), with research **branches** (§8.4).
- **Tree 2 — Civics**, driven by the Culture allocation (§8). No branches at present.
**DIRECTOR RULING.**
````

### §3.4 — original

````markdown
**3.4** A **branch** is a subdivision of Tree 1, not a separate tree. Branches share one resource (the Science allocation) and may depend on each other and on any domain. **DIRECTOR RULING:** *"not six independent technology trees — research branches within the Technology/Knowledge progression."*
````

### §4 table — original

````markdown
| **Technology / Knowledge** | scientific, technical, accumulated knowledge | research (Science) |
| **Civics** | political, governmental, legal, administrative and social organization | research (Culture), then adoption |
````

### §5.7 — original

````markdown
**5.7 Candidate reclassification (PROPOSED, not applied):** several entries the v0.4 registry filed as institutions read as Civics under this ontology — written law (`law_code`), Roman systematic law (`legal_code_roman`), census, coined wage labour, patent, joint-stock company. Reviewed at the schema step (§17).
````

### §7.1 row — original

````markdown
| **researched** | progressed with a Cognitive Pool allocation until complete | Technology, Civics; a codified Technique |
````

### §7.3 — original

````markdown
**7.3** Completion of a researched node never directly creates a world entity. It makes downstream entities **eligible / AVAILABLE**. Realization happens in the owning mechanism through the lifecycle **LOCKED → AVAILABLE → UNDER_CONSTRUCTION → ACTIVE → MATURE → SATURATED/DIMINISHING**. **RATIFIED:** ADR-028 §3 (DD-13, DD-14).
````

### §8 (whole section, §8.1–§8.5.3) — original

````markdown
## 8. Progression resources and the research relationship

### 8.1 The Cognitive Pool — DIRECTOR RULING

```
Population + education + literacy + institutions + health + specialization + connectivity
                                   ↓
                          Cognitive capacity
                                   ↓
                     COGNITIVE POOL  (per turn)
                                   ↓
              ┌────────────────────┼───────────────────┐
              ↓                    ↓                   ↓
     Science allocation     Culture allocation    [future channels]
              ↓                    ↓
         Technology              Civics
```

**8.1.1** The Cognitive Pool is the civilization's per-turn throughput for deliberate progression. It **replaces "Research Points"** as the conceptual term. **DIRECTOR RULING.**

**8.1.2** Population is an **input**, not a linear multiplier. *"Cognitive Pool = Population × constant"* is forbidden. The pool is a function of population, education, literacy, institutions, health, specialization and connectivity, with diminishing marginal contribution from population. **DIRECTOR RULING.** No functional form, coefficient or illustrative number is adopted; the Director's example figures were stated as illustrative only.

**8.1.3** The player allocates the pool between channels — for example 70/30 Science/Culture — and institutions, policies and population composition change the **efficiency** of each channel. An efficiency term is a coefficient inside the conversion equation, which law 2 permits. **DIRECTOR RULING; RATIFIED constraint** (law 2).

**8.1.4** Science, Culture, Diplomatic Favor and Diplomatic Victory Points are four distinct quantities and must not be conflated. Production and construction capacity is a fifth, separate thing. **DIRECTOR RULING.**

### 8.2 Accumulation — DIRECTOR DECISION REQUIRED (conflict)

The Cognitive Pool ruling states: *"Cognitive Pool is not a bank of accumulated points … Unused capacity should therefore not accumulate indefinitely."*

**D-042 §9.5 (`:170`, RATIFIED)** states: *"Unallocated knowledge ACCUMULATES AS A RESERVE rather than disappearing, and the player may deliberately hold a non-optimal reserve."*

These conflict. Options, none taken:
- **(a)** Amend D-042 §9.5: unallocated capacity is lost each turn.
- **(b)** Keep §9.5: unallocated capacity accumulates as a reserve, spent later. The pool is still per-turn throughput; the reserve is a separate stock. arch-C's proposed fence (recovery gap **G-03**) — the reserve may appear only inside a rate equation, never as the left side of a gating comparison — would apply.
- **(c)** Keep §9.5 with a decay or cap on the reserve, derived later.

Retained **per-node progress** is unaffected by this choice: partial progress on a node is kept when allocation moves elsewhere. **DIRECTOR RULING.**

### 8.3 Concurrency within a channel — DIRECTOR DECISION REQUIRED (conflict)

The rulings speak of *"the active Technology / Civic"* — singular. **D-042 §9.3 (`:167`, RATIFIED):** *"Research activities proceed in PARALLEL"*, and §12 bans *"a rigid one-at-a-time research queue."* The Science/Culture split is already parallel across channels.

Options: **(a)** within a channel, the allocation is spread across concurrent nodes, as D-042 requires; **(b)** amend D-042 §9.3 and §12 to allow one active node per channel. The Director earlier stated *"one at a time isn't necessary,"* which reads as (a). **Recommendation: (a).**

### 8.4 Tree 1 branches — DIRECTOR RULING

**8.4.1** Six research branches within Technology / Knowledge:

| branch | covers |
|---|---|
| **Military** | military and naval knowledge |
| **Medicine** | medical and biological-medical knowledge |
| **Engineering** | materials, construction, mechanical and civil engineering, transport, communications |
| **Industry & Energy** | industrial production, power generation, energy systems, manufacturing, industrial processes |
| **Natural Science** | the physical, chemical and life sciences as bodies of knowledge |
| **Agriculture** | cultivation, breeding, food production |

Heavy cross-dependency between branches is expected (Engineering → steam engine → industrial machinery ← Industry & Energy). **DIRECTOR RULING.**

**8.4.2 Progressive branching.** Each branch opens when its relevant prerequisite development is completed — a specialized faculty or capability becoming available — not all at once. **DIRECTOR RULING (option B).**

**8.4.3** An Age transition never opens a branch. Age gives historical context and constrains plausibility only. **DIRECTOR RULING;** consistent with law 4. **RATIFIED.**

**8.4.4** Experimental Method remains an institution. It is not converted to a technology to serve as a branch trigger. **DIRECTOR RULING.**

**8.4.5** Before a branch opens, its knowledge sits in the Tree-1 trunk. The branch opening conditions and each branch's trigger node are **not fixed here** (§19).

**8.4.6** Each branch terminates in its repeatable frontier lines — Military in military and cyber research, Medicine in biomedical research, and so on — so every specialization continues indefinitely. **INFERRED** from the v0.6 frontier design; the mapping is **PROPOSED**.

### 8.5 Specialized universities and the D-021 loop — constraint

**8.5.1** Universities improve the civilization's ability to convert population into effective cognitive output, and specialized faculties raise the efficiency of their branch, with diminishing returns and local saturation. They do not add flat science. **DIRECTOR RULING; RATIFIED constraint:** ADR-028 §2 (DD-14).

**8.5.2** Research enabling universities that accelerate research is a **positive feedback loop.** D-021 requires a paired brake in the same milestone. The ratified brake runs through **literacy** (arch-M **C-7**), and **no literacy variable exists** — arch-M records the pairing as *"currently unsatisfiable, not merely unbuilt."* **MEASURED / RATIFIED constraint.** The specialized-university mechanic may be designed, but not built, before literacy or another ratified brake exists.

**8.5.3** For the same reason, a larger population raising the Cognitive Pool, which raises growth, is a loop that D-021 governs. Diminishing marginal contribution (§8.1.2) is a damping term, not by itself the ratified brake. **INFERRED.**
````

### §14.1 — original

````markdown
**14.1** Culture is a **progression channel**: an allocation of the Cognitive Pool that drives Civics. It is not Tourism and does not produce a Culture Victory. **DIRECTOR RULING.**
````

### §14.3 — original

````markdown
**14.3 Naming — DIRECTOR DECISION REQUIRED (conflict).**
````

### §14.4 — original

````markdown
**14.4** Culture's generation sources, costs and constants are not decided here. Under §8.1 it is an allocation of the Cognitive Pool, so it has no separate generation model unless a later ruling gives it one. **INFERRED; DIRECTOR DECISION REQUIRED** to confirm.
````

### §17.4 — original

````markdown
4. **Add `tree` and `branch`** — `tree` ∈ {technology, civics} for researched nodes; `branch` ∈ the six (§8.4) or null for trunk nodes; branch opening conditions recorded as first-class data.
````

### §17.5 — original

````markdown
5. **Remove age `F`** — frontier nodes become A9 with `frontier: true`; the viewer renders them as a sub-band of A9 (§12.7).
````

### §17.7 — original

````markdown
7. **Reclassify** the Civics candidates in §5.7.
````

### §18 CP row — original

````markdown
| Cognitive Pool; population as a non-linear input | DIRECTOR RULING |
| Science → Technology; Culture → Civics; four distinct quantities | DIRECTOR RULING |
| Six branches; progressive opening; no Age trigger | DIRECTOR RULING |
````

### §18 conflicts — original

````markdown
| No accumulation of unused capacity | **CONFLICT** with D-042 §9.5 |
| Single active node per channel | **CONFLICT** with D-042 §9.3, §12 |
| "Culture" as a bare internal term | **CONFLICT** with CONV-1 and the shipped bucket dimension |
````

### §18 univ — original

````markdown
| Specialized-university mechanic | Designable; not buildable until a D-021 brake exists |
````

### §19 1-6 — original

````markdown
1. **Accumulation** — amend D-042 §9.5, keep the reserve, or keep it with decay (§8.2).
2. **Concurrency** — parallel allocation within a channel, or amend D-042 §9.3/§12 (§8.3). Recommended: parallel.
3. **Culture naming** — the internal term for the progression channel (§14.3).
4. **Culture generation** — confirm Culture is purely an allocation of the Cognitive Pool, or give it its own sources (§14.4).
5. **Cognitive Pool function** — inputs, form and constants, each derived from a stated reference class (§8.1.2).
6. **Branch triggers** — the prerequisite development that opens each of the six branches (§8.4.5).
````

### §19 11 — original

````markdown
11. **D-021 brake** — literacy or another ratified brake before specialized universities are built (§8.5).
````
