# D12 — Civics lane notes (design rationale, gate audit, balance, open decisions)

**Status: PROPOSED design for Director review. Nothing here is ratified, nothing is runtime content, and no `Sim.*` file was changed (code and content were read only for MEASURED citations).** Canonical data: `data/proposal-parts/D12-civics.json` (this lane's part file; `meta` carries every table below in machine-readable form). Labels: **MEASURED** (read from the tree or the package's measurements, cited) · **RESEARCHED** (none this session) · **INFERENCE** · **PROPOSED** · **OPEN DECISION** · **IMPLEMENTATION DEFERRED**.

**Evidence limit stated first.** The web search budget was exhausted and fetch could not resolve any host during this session, so **no external source was retrieved** and no sources file was written. Every historical anchor (`emerged`, the examples inside concepts) is therefore **INFERENCE** from general historical knowledge, chosen from well-known dated events and hedged with '~' where approximate; contested readings are flagged in the text. §15 lists the verification targets for the corpus stage.

## 1. What this lane delivers

- **Existing civics:** 7 kept, 0 changed (keys 1001–1007; ids, names, Ages, prerequisites, factors and costs unchanged). Their capability blocks are re-stated with MEASURED citations; the auto-fill had named `RoadDevelopmentSystem` as census's owner because the substring 'road' occurs in `inst.mass_media_broadcast`.
- **New micro-civics:** 42 — practices, institutions and policies in eight strands. **New macro-civics:** 18 — regime forms, each defined as a named set of micro modules (arch-FGH's regime module set; content only in M6 per D-V; adoption in M8 per D-D).
- **Total civics proposed:** 67; new civic workload 316,980 points (U = 92.4).
- **Integration check (MEASURED in a scratch copy of the package, not in the shared tree):** `tools/integrate.py --allow-incomplete` → civics 67 records (60 new, keys 1008–1067); `tools/validate.py --no-corpus` → **0 errors**. Civics warnings: V06/V07 (the existing `legal_code_roman` gate, expected) and one V18 per `regime:`/`policy:` grant (the intended IMPLEMENTATION DEFERRED marker). `tools/render.py` renders the civics detail view.
- **Lane-script checks (all pass):** ids unique against every node id and every entity short id; display names unique; prerequisites resolve; no cycle; no Age inversion; prereq_complexity equals the ledger convention; quarter-step factors within loader ranges; every new `emerged` carries an era marker and no must-hold prerequisite is dated later (61 dated edges); **no new civic inside any ratified gate closure**; no macro is a prerequisite of anything and no macro requires another; no macro's prerequisite closure contains an 'optional' civic (I-7).

## 2. Design rationale

**What a civic is here.** Technology answers what a civilization knows; Civics answers how it organizes and governs itself (civ-progression §5.1, RATIFIED). A civic is admitted only if it is a distinct institution, practice, policy or regime arrangement with a decision value and a named consumer. No civic copies a technology node: where an existing node holds the knowledge of an instrument (bill of exchange, paper money, surveying, standard weights), the civic holds the institution that uses it (merchant law, central banking, land registry, officialdom).

**Eight strands (internal branches, not subtrees — D-044 R3; civics carry no branch, loader rule).** Law & justice · Administration & fiscal · Economy & commerce · Society & welfare · Diplomacy · Military institutions · Legitimacy & succession · Representation & local government — plus the regime forms. The strand is a civic-block field; whether the UI draws one lane per strand is OD-10 (the shipped layout makes one Civics lane per distinct `domain`, `ResearchTreeLayout.cs:125-131`, MEASURED).

**Modules and forms.** Micro civics that a government is made of (council, assembly, elected magistracies, hereditary rank, officialdom, provinces, standing army, representation, constitution, separated powers, responsible ministers, franchise, parties, planning) are regime *modules*; each macro civic is a named set of them — never an enum and never a bonus bundle (Spine `:85`; D-042 §5; arch-FGH §2.1–2.2). Forms are **leaves**: nothing requires a form and no form requires another, so there is no government ladder; 10 of 18 forms carry OR routes in their own prerequisites (two routes to kingship's legitimacy, to theocratic rule, to feudal legitimacy, to military rule, to the centralized officialdom of absolutism, to a constitutional limit, to the units of a federation, to the communes of a mercantile republic, to the backing of a one-party state).

**Five states, four owners (I-2).** Every civic states what Researched/Available, Adopted, Active and Realized mean for it. Institution-type civics are *realized by founding* and never 'adopted' (D-M); policies and modules are adopted by an explicit act (M8, D-D); military institutions are knowledge-only until M7 (D-Q); forms are content definitions in M6 (D-V).

**An owner for every promise.** Each civic lists its promises with owner, milestone and status. Today's loader lets a civic unlock only Institution entities (`ResearchContent.cs:1240-1242`), so every `regime:`/`policy:` grant and every proposed new `inst.*` entity is **IMPLEMENTATION DEFERRED**; every new civic still carries a capability string so it is loader-valid (non-orphan rule, `:591-600`). Primary milestones of the 60 new civics: M8 44, M7 4, M6 2, M9 8, M10+ 2.

**Optional means optional (I-7).** Roles: gate 3, age-supporting 4, module 25, optional 17, regime-form 18. Only three civics are *gates* (law_code, legal_code_roman, taxation — all existing). No new civic enters the closure of the research stage, any Age CORE fact, the tax edict or inst.university; no civic is added to any Age fact; forms are never Age facts.

**Distinct from Technology, coupled explicitly.** Civics may require technology where the knowledge is causal (writing for officials and envoys, paper for examinations, the press for compulsory education and parties, the telegraph for planning). No technology requires a civic. The only Technology-side civic couplings remain the existing ones audited in §4.

## 3. The seven existing civics

| key | id | name | Age | cost | role | disposition | note |
|---|---|---|---|---|---|---|---|
| 1001 | `law_code` | Written law | A3 | 1,240 | gate | keep | Keep. Written law is in the closure of every ratified civic gate (the research stage, inst.university and the A5 CORE milestone, all via legal_code_roman) and is the civic of the D-P vertical slice; its prerequisite (full writing in any of three independent scripts) is causal. |
| 1002 | `legal_code_roman` | Systematic law | A4 | 2,490 | gate | keep | Keep id, name, prerequisite and cost. The id carries 'roman' but the display name 'Systematic law' is culture-neutral (Roman, Chinese and Islamic systematic jurisprudence all fit); ids are never renamed. Its gate role (research-stage atom, A5 CORE, inst.university) is audited in meta.gateAudit; the recommendation is to keep it with a measured pacing guarantee (meta.stageAtomOptions). |
| 1003 | `census` | Census | A4 | 4,970 | age-supporting | keep | Keep cost and prerequisite. Its breadth (1.5) is now justified twice over: six institution gates today and, in this proposal, six civic dependants (it becomes the administration strand's hub). Five of its six institution gates use it as a proxy for a more specific institution; re-pointing them is recommended in meta.gateAudit G-11…G-15 (OPEN DECISION OD-05). |
| 1004 | `coined_wage` | Wage labour in coin | A4 | 1,240 | age-supporting | keep | Keep. A supporting Age fact (one of several), not a gate by rule — but MEASURED binding for the AI's A5 entry in the one-pool baseline on all four seeds; an AI civics lane must value next-Age supporting civics. |
| 1005 | `patent` | Patent system | A7 | 4,180 | age-supporting | keep | Keep. The prerequisite printing_press is classified 'unclear' in the gate audit (INFERENCE: the earliest codified patent law is generally dated before printed patent specifications; printing's role is disclosure) but no change is proposed without a causal review (D-047 Part C method). |
| 1006 | `joint_stock` | Joint-stock company | A6 | 2,960 | age-supporting | keep | Keep. MEASURED binding for the AI's A7 entry on seeds 1, 2 and 42 (a7_company). Its second institution, inst.central_bank, is better gated by the new central_banking civic (same closure plus one node) — recommended, OPEN DECISION. |
| 1007 | `taxation` | Taxation | A3 | 1,050 | gate | keep | Keep. The dedicated tax civic (R5, Director 2026-10-04; d049 H2): the only atom of the tax edict's knowledge gate. Its prereq_complexity 0.5 departs from the ledger convention (three top-level conjuncts → 1.0; digest C-12) — kept, because its 1,050 RP is a ratified figure (adr-033:268-271). |

## 4. Gate audit (every civic gate today)

| id | gate | civic atoms | classification | recommendation |
|---|---|---|---|---|
| G-01 | research stage atom — researchStage.requires 'medicine_hippocratic AND (geometry_axiomatic OR algebra) AND stamp_seal AND legal_code_roman' (research.json:254); opens all five subtrees (ResearchQuery.cs:81-94) | legal_code_roman, law_code (closure) | intentional (derived from inst.university's requirement, research.json:255 _doc, ADR-029 §8); consequence under CP unexamined | keep (Option A) with tolerances P1-P6; Option B as remedy |
| G-02 | inst.university — 'library AND legal_code_roman' (research.json:29068); founded by the five university projects | legal_code_roman | intentional: a university is a chartered corporation (institutional requirement) | keep (under Option B it becomes the only place the civic gates learning) |
| G-03 | a5_systematic_law (A5 CORE) — research fact legal_code_roman (ages.json:85) | legal_code_roman | intentional: systematic law as the Classical Age's institutional mark; a micro civic, not a government form | keep; inside tolerance P4 |
| G-04 | a3_law (A3 supporting) — research fact law_code (ages.json:50) | law_code | intentional; not structural (A3 has three institutional_social facts and needs only 2 categories) | keep |
| G-05 | a4_census (A4 supporting) — research fact census (ages.json:70) | census | intentional; not structural (a4_merchants is a second institutional_social fact) | keep |
| G-06 | a5_wages (A5 supporting) — de facto mandatory — research fact coined_wage (ages.json:92); A5 needs 3 supporting facts spanning 3 categories (ages.json:97-98) | coined_wage | accidental de facto gate: A5 has no material_economic supporting fact, military realization (a5_heavy_infantry) is pending until M7, and a5_wages is A5's only institutional_social fact — so a 3-category span requires coined_wage until M7 | modify (Ages content, OPEN DECISION OD-02): add a non-civic A5 supporting fact in the missing material_economic category, or widen a5_wages; until ruled, treat coined_wage as a gate civic in tolerance P4 and in the AI civics lane (and in the one-pool AI today: add it to the core goal when the CORE facts are met) |
| G-07 | a7_patent / a7_company (A7 supporting) — research facts patent, joint_stock (ages.json:129-130) | patent, joint_stock | intentional; binding in practice but not structural (A7 has technological, systemic and material_economic alternatives) | keep; AI civics lane values next-Age supporting civics (priority 2) |
| G-08 | tax edict — sim.json governance.taxationRequires 'taxation' (:268) + taxationMinAge 3 (:270) | taxation | intentional (R5 Director 2026-10-04; d049 H2 RATIFIED) | keep |
| G-09 | inst.law_code / inst.legal_code_roman / inst.census / inst.coined_wage / inst.patent / inst.joint_stock — each requires its own civic (research.json:28984, 29040, 29033, 29026, 29131, 29117) | law_code, legal_code_roman, census, coined_wage, patent, joint_stock | intentional (architecture §5.7 reclassification: the institution's knowledge requirement is its civic) | keep |
| G-10 | inst.guild — 'textile_workshop AND legal_code_roman' (research.json:29089) | legal_code_roman | unclear: the civic atom says nothing about crafts; a guild is a chartered craft corporation | modify the civic half: 'textile_workshop AND craft_guilds' (craft_guilds = legal_code_roman OR municipal_charter); unlock lists move with it |
| G-11 | inst.military_medicine — 'surgery_instruments AND census' (research.json:29054) | census | accidental: census stands in for 'state organization'; a military hospital serves a standing army | modify: 'surgery_instruments AND standing_army' |
| G-12 | inst.mint_institution — 'coinage_electrum AND census' (research.json:29061) | census | accidental: striking state coin needs officials, not a population count | modify: 'coinage_electrum AND appointed_officials' (closure cheaper: 2,090 instead of 4,970) |
| G-13 | inst.quarantine — 'hospital AND census' (research.json:29096) | census | unclear: registration helps, but quarantine is the power of a health authority | modify: 'hospital AND public_health_board' (census stays in the closure) |
| G-14 | inst.statistics_vital — 'census AND calculus' (research.json:29145) | census | intentional but coarse: vital statistics grew from mortality-table analysis and civil registration, not from calculus (INFERENCE; D10's view, cross-lane request) | modify: 'official_statistics AND epidemiology' (epidemiology = D10's PROPOSED node, A8, medicine subtree); fallback 'official_statistics AND calculus' if that node is not adopted; census stays in the closure |
| G-15 | inst.mass_schooling — 'printing_press AND newspaper AND census' (research.json:29152) | census | unclear: census as a proxy for registering children | modify: 'printing_press AND newspaper AND compulsory_education' (census stays in the closure) |
| G-16 | inst.central_bank — 'joint_stock AND paper_money' (research.json:29124) | joint_stock | intentional (the English case was a joint-stock bank) but a proxy for a distinct institution | modify: 'central_banking' (its prerequisites are exactly joint_stock AND paper_money, so the closure gains one node) |
| G-17 | AI goal closures — core (next Age CORE), road, tax, university goals over one tree-agnostic closure (AiResearchPolicy.cs:63, 105-181, 213-235); fallback 'cheapest available anywhere' | legal_code_roman, law_code, taxation | intentional (content-driven) but cross-pool under D-061; misses structural supporting civics (G-06) | modify in T6.16: split each goal closure per pool; an AI civics lane (gate civics → tax → next-Age supporting civics → realizable civics → fallback); add structural supporting civics to the core goal |
| G-18 | UI: INSTITUTIONS lens — 'Adopted civics are completed Civics nodes' (Sim.Ui/Progression/Lenses.cs:62-68) | all | contradicts I-2 (Researched ≠ Adopted) | modify in T6.6: 'Researched/Available' until an adoption act exists |
| G-19 | patent prerequisite — 'legal_code_roman AND printing_press' | legal_code_roman | unclear: the earliest codified patent law is generally dated before printed patent specifications; printing's role is disclosure (INFERENCE) | keep pending a D-047-style causal review; no change proposed |

**The structural finding (G-06, MEASURED + INFERENCE).** A5 entry needs three supporting facts spanning three categories (`ages.json:97-98`). A5 has no material-economic supporting fact, its military fact is pending until M7, and `a5_wages` (= `coined_wage`) is its only institutional-social fact — so until M7 every polity must research `coined_wage` to enter A5. The package's one-pool AI baseline shows exactly that: the A5 entry order turn equals the `coined_wage` completion turn on all four seeds (1233/1245/1225/1422), although both CORE facts were met by turns 860/875/865/922 — the AI waited 360–500 turns for a civic that is in none of its goals. The same equality holds for A7 and `joint_stock`, but A7 has non-civic alternatives, so that one is behavioural, not structural.

## 5. The research-stage civic atom — options, recommendation, tolerances

**Question.** D-061.8 (widened): should the civic legal_code_roman stay an atom of the research stage (researchStage.requires, research.json:254) now that civics may be paced by a separate currency?

**MEASURED baseline.** One-pool AI polity (measurements/baseline-summary.json ai_polity): legal_code_roman completed at turns 635/649/644/639 and the stage at 702/716/710/705 (seeds 1/2/42/7) — the civic atom never bound the stage; geometry_axiomatic did. Civic slack 66-67 turns on every seed.

- **Option A — Keep the civic atom, with a measured pacing guarantee.** No content change. T6.3 measures the tolerances below against the one-pool attribution twin; the AI civics lane works gate civics first. *For:* no change to ratified-derived content (D-044 R4 'university / research institutional stage'; the stage _doc derivation, research.json:255; ADR-029 §8); keeps one explicit, measured point where the two economies interlock — the institution of learning; today's measured slack (66-67 turns) and the cost ratio (civic 3,730 vs technology 24,930 = 0.15) make a stall unlikely under any CP rate ≥ 0.3 of RP. *Against:* technology after A5 waits on the civics economy if CP is slow or the civics target is spent elsewhere; a human player who neglects civics locks all five subtrees — legal, but the UI must say so. *Risk:* depends on D-061.3 (CP inputs) and on an AI civics lane that exists only from T6.16.
- **Option B — Move the civic requirement from the knowledge-level stage to the realized university.** researchStage.requires → 'medicine_hippocratic AND (geometry_axiomatic OR algebra) AND stamp_seal'; inst.university keeps 'library AND legal_code_roman', so founding a university (and its research-cost seam) still needs systematic law; the stage _doc and ADR-029 §8 receive dated notes. *For:* CP pace can never stall technology knowledge; causally clean: specialized knowledge needs no law code; a chartered university does. *Against:* re-reads a ★ settled ruling: 'the specialized branches become available when the civilization reaches the university/research institutional stage' (d044:145, ★ d044:796; digest S1-06) — the stage would become the knowledge half only; the stage expression itself is ratified content (D-046 F§1; digest S1-08) — a Director ruling; ResearchContentTests.cs:233-252 pins 'stage needs legal_code_roman'; goldens move where the civic lagged (attributed); stage closure falls from 18 nodes / 28,660 to 16 / 24,930 in the one-pool world too. *Risk:* a ratified-content change made to protect a pacing that has not been measured yet.
- **Option C — An OR alternative of civic routes to institutional learning.** researchStage.requires '… AND (legal_code_roman OR civil_service_examinations)' — the chartered-corporation route or the state-examination route. *For:* historically plural routes to higher learning. *Against:* still paced entirely by CP — does not answer the question; puts civil_service_examinations (and paper, 11,830) inside a ratified gate, breaking R-C2; needs a D-047-style causal review of the new edge. *Risk:* adds a progression-relevant civic for no pacing benefit.

**Sensitivity (ESTIMATE, `tools/balance.py` cp_coupling, seed 42; a static lower-bound model, not a simulation).** Turns by which the research stage would wait on its civic part, by CP/RP ratio and by CP spent on other civics first:

| CP spent on other civics first | 0.05 | 0.1 | 0.15 | 0.25 | 0.5 | 1.0 |
|---|---|---|---|---|---|---|
| 0 | 249 | 79 | 0 | 0 | 0 | 0 |
| 10000 | 709 | 433 | 307 | 169 | 17 | 0 |
| 30000 | 1269 | 809 | 622 | 427 | 222 | 58 |

With no other civic bought first the break-even ratio is 0.15 (= 3,730 / 24,930). This proposal adds 34,760 points of new civics (micro and macro) whose metadata Age is A1-A4: a player who buys most of them before systematic law delays the subtrees by roughly 60-430 turns at ratios 1.0-0.25 (ESTIMATE). That is a player's choice, but a heavy one; it is the main argument for Option B and the reason for the UI rule below.

**Recommendation.** A, because the stage's institutional character is a ★ settled ruling (S1-06) and its expression is ratified content (S1-08): keep the atom, with tolerances P1-P6 as T6.3 acceptance, an AI civics lane that works gate civics first, D-061.3 aiming at CP ≥ 0.5 × RP (10,000 CP of other civics then costs about 17 turns, ESTIMATE), and a UI rule that marks legal_code_roman (and law_code) as 'opens the five subtrees' in the civics panel. B is the pre-registered remedy if P1-P4 fail after those remedies — it needs the Director to read S1-06 as the knowledge half of the university stage. C is rejected. The same guarantee covers the A5 CORE fact a5_systematic_law and the structural a5_wages gate (G-06).

**Tolerances (PROPOSED for T6.3 acceptance).** An A/B attribution comparison between the CP world and the one-pool twin (TestConfigs.PreCivicsPoints) on the same seeds — not a calibration corridor: no coefficient is tuned to hit it and its only consequence is a structural remedy (m6-spec §3.3; digest C-29). Protocol: Seeds 1, 2, 42, 7; horizon 1,630 turns; drivers (i) the AI polity (sim run --founded --ai-empires 1) and (ii) the --auto cheapest research driver. Measure T_tech (stage technology atoms complete), T_lcr (legal_code_roman complete), T_stage, T_A5core, T_A5entry, T_A7entry; civic lag L = T_stage − T_tech.

- **P1 (liveness (hard)):** every polity that reaches the research stage (and A5 entry) in the twin also reaches it in the CP world within the horizon
- **P2 (non-regression):** T_stage(CP) ≤ T_stage(twin) + 10 turns on every seed and driver (≈1.4 % of the measured 702-716)
- **P3 (civic slack):** median L = 0 and max L ≤ 5 turns over seeds × drivers (today L = 0 with 66-67 turns of slack)
- **P4 (Age entry):** T_A5entry(CP) ≤ T_A5entry(twin) + 10 turns — covers the CORE civic legal_code_roman and the structural a5_wages civic coined_wage
- **P5 (design-time precondition (before any run)):** CP_turn(P) ≥ 0.30 × RP_turn(P) for population P in [20,000; 300,000] — the range in which the AI approaches the stage (56,550-59,580 at turn 500, 101,260-106,800 at turn 650, 153,054-270,681 at turn 800 in the baseline). 0.30 = 2 × 0.15, the ratio of the stage's civic part (3,730) to its technology part (24,930); the factor 2 covers taxation (1,050) bought first and the civics target spent elsewhere. Target (not a gate): ≥ 0.5, which keeps a 10,000-CP detour through other civics to about 17 turns of stage delay (ESTIMATE, sensitivityEstimate).
- **P6 (remedy order):** if P1-P4 fail: (1) the AI civics lane works gate civics first; (2) revise D-061.3 CP inputs; (3) Option B. Never a civic or technology cost change (C-23).
- **Human players:** The tolerances bind AI and scripted drivers. A human who neglects civics delays the stage by choice; the stage lock must name the civic prerequisite across trees (legal_code_roman playerCommunication).

## 6. Micro-civic catalogue (42 new)

| id | name | Age | strand | prerequisites | magnitude → cost | primary owner · milestone | role |
|---|---|---|---|---|---|---|---|
| `kin_council` | Council of elders | A1 | representation-local-government | — | 1.75 → 310 | Adoption (M8) — regime module set · M8 | module |
| `hereditary_rank` | Hereditary rank | A2 | legitimacy-succession | cereal_cultivation OR sheep_goat | 3 → 740 | Adoption (M8) — regime module set; ClassMobilitySystem (M9) · M8 | module |
| `organized_priesthood` | Organized priesthood | A2 | legitimacy-succession | calendar_lunar AND (cereal_cultivation OR sheep_goat) | 3.5 → 1,050 | Adoption (M8) — regime module; ClassMobilitySystem (M9) · M8 | module |
| `oath_alliance` | Oath alliances | A2 | diplomacy | kin_council | 2.25 → 440 | Diplomacy (M8) · M8 | module |
| `corvee_labour` | Corvée labour | A3 | administration-fiscal | taxation | 3.25 → 880 | GovernanceSystem + ConstructionSystem (policy adoption M8) · M8 | optional |
| `appointed_officials` | Appointed officials | A3 | administration-fiscal | taxation AND (cuneiform OR hieroglyphic OR chinese_script) | 4.5 → 2,090 | Adoption (M8); ClassMobilitySystem (M9) · M8 | module |
| `sacral_kingship` | Sacred kingship | A3 | legitimacy-succession | hereditary_rank AND organized_priesthood | 4.25 → 1,760 | Adoption (M8) · M8 | module |
| `service_tenure` | Land held for service | A3 | military-institutions | taxation AND law_code | 4.5 → 2,090 | Battle layer (M7); Adoption (M8); ClassMobilitySystem (M9) · M7 | module |
| `royal_envoys` | Envoys and diplomatic letters | A3 | diplomacy | cuneiform OR hieroglyphic OR chinese_script | 2.75 → 620 | Diplomacy (M8) · M8 | optional |
| `sworn_treaties` | Sworn treaties | A3 | diplomacy | royal_envoys AND oath_alliance | 3.75 → 1,240 | Diplomacy (M8) · M8 | optional |
| `elite_council` | Council of notables | A3 | representation-local-government | kin_council AND hereditary_rank | 3.75 → 1,240 | Adoption (M8) · M8 | module |
| `succession_law` | Rules of succession | A3 | legitimacy-succession | hereditary_rank AND law_code | 3.75 → 1,240 | Adoption (M8) · M8 | module |
| `provincial_administration` | Provincial governors | A4 | administration-fiscal | appointed_officials AND census | 5.25 → 3,520 | GovernanceSystem (reach) — R4 candidate; Adoption (M8) · M6 | module |
| `standing_army` | Standing army | A4 | military-institutions | appointed_officials | 4.5 → 2,090 | Battle layer (M7); ClassMobilitySystem (M9); Adoption (M8) · M7 | module |
| `land_registry` | Land registry | A4 | administration-fiscal | surveying AND law_code | 4.25 → 1,760 | InstitutionsSystem + GovernanceSystem · M8 | optional |
| `citizen_assembly` | Popular assembly | A4 | representation-local-government | kin_council | 4.25 → 1,760 | Adoption (M8) · M8 | module |
| `elected_magistracies` | Elected magistracies | A4 | representation-local-government | citizen_assembly | 3.75 → 1,240 | Adoption (M8) · M8 | module |
| `tax_farming` | Tax farming | A4 | administration-fiscal | taxation AND coinage_electrum | 3.5 → 1,050 | GovernanceSystem (tax instrument) — R4 candidate · M6 | optional |
| `craft_guilds` | Craft guilds | A5 | economy-commerce | legal_code_roman OR municipal_charter | 4.25 → 1,760 | ClassMobilitySystem (M9); InstitutionsSystem if inst.guild is re-pointed · M9 | optional |
| `municipal_charter` | Municipal charters | A5 | representation-local-government | law_code AND (elite_council OR citizen_assembly) | 5.25 → 3,520 | Adoption (M8) · M8 | module |
| `religious_toleration` | Religious toleration | A5 | society-welfare | organized_priesthood AND legal_code_roman | 5 → 2,960 | Society (M9) · M9 | optional |
| `civil_service_examinations` | Civil service examinations | A6 | administration-fiscal | appointed_officials AND paper | 6 → 5,910 | ClassMobilitySystem (M9); Adoption (M8) · M9 | module |
| `representative_assembly` | Representative assembly | A6 | representation-local-government | taxation AND (municipal_charter OR service_tenure) | 5.75 → 4,970 | Adoption (M8) · M8 | module |
| `commercial_law` | Merchant law | A6 | economy-commerce | legal_code_roman AND trade | 5 → 2,960 | Adoption (M8); TradeArbitrageSystem (M10+) · M8 | module |
| `central_banking` | Central banking | A7 | economy-commerce | joint_stock AND paper_money | 5.75 → 4,970 | none (M10+) — no monetary system · M10+ | optional |
| `written_constitution` | Written constitution | A7 | law-justice | legal_code_roman AND representative_assembly | 6 → 5,910 | Adoption (M8) · M8 | module |
| `separation_of_powers` | Separation of powers | A7 | law-justice | written_constitution | 5 → 2,960 | Adoption (M8) · M8 | module |
| `bill_of_rights` | Bill of rights | A7 | law-justice | legal_code_roman AND (representative_assembly OR elite_council) | 5.75 → 4,970 | NeedsGrievanceSystem (M9); Adoption (M8) · M9 | module |
| `law_of_nations` | Law of nations | A7 | diplomacy | sworn_treaties AND legal_code_roman | 5 → 2,960 | Diplomacy (M8) · M8 | optional |
| `official_statistics` | Statistical office | A7 | administration-fiscal | census AND hindu_arabic | 6 → 5,910 | Society (M9); InstitutionsSystem if re-pointed · M9 | module |
| `public_health_board` | Boards of health | A7 | society-welfare | census AND medicine_hippocratic | 5.5 → 4,180 | Society (M9); InstitutionsSystem if re-pointed · M9 | optional |
| `compulsory_education` | Compulsory education | A7 | society-welfare | census AND printing_press | 6 → 5,910 | Society (M9); InstitutionsSystem if re-pointed · M9 | optional |
| `civil_police` | Professional police | A7 | law-justice | appointed_officials AND law_code | 5.5 → 4,180 | Adoption (M8); NeedsGrievanceSystem (M9) · M8 | optional |
| `resident_embassies` | Resident embassies | A7 | diplomacy | sworn_treaties AND appointed_officials | 5 → 2,960 | Diplomacy (M8) · M8 | optional |
| `ministerial_responsibility` | Responsible ministers | A8 | representation-local-government | representative_assembly AND appointed_officials | 6.25 → 7,030 | Adoption (M8) · M8 | module |
| `mass_suffrage` | Mass suffrage | A8 | representation-local-government | representative_assembly AND census | 7 → 11,830 | Adoption (M8) · M8 | module |
| `mass_political_party` | Mass political parties | A8 | representation-local-government | mass_suffrage AND printing_press | 6.75 → 9,950 | Adoption (M8) · M8 | module |
| `universal_conscription` | Universal conscription | A8 | military-institutions | standing_army AND census | 7 → 11,830 | Battle layer (M7); NeedsGrievanceSystem (M9); Adoption (M8) · M7 | optional |
| `general_staff` | General staff | A8 | military-institutions | standing_army AND cartography | 6.5 → 8,360 | Battle layer (M7); Adoption (M8) · M7 | optional |
| `laws_of_war` | Laws of war conventions | A8 | diplomacy | law_of_nations | 5 → 2,960 | Diplomacy (M8) · M8 | optional |
| `abolition` | Abolition of slavery and serfdom | A8 | society-welfare | legal_code_roman | 6 → 5,910 | ClassMobilitySystem (M9) · M9 | optional |
| `central_planning` | Central economic planning | A9 | economy-commerce | official_statistics AND telegraph | 8 → 23,650 | ProductionSystem (M10+); Adoption (M8) · M10+ | module |

## 7. Macro-civic catalogue (18 new regime forms)

| id | name | Age | prerequisites (modules) | magnitude → cost | cheapest civic route | compatible labels |
|---|---|---|---|---|---|---|
| `chiefdom` | Chiefdom | A2 | hereditary_rank | 3.5 → 1,050 | 1,790 | tribal_confederacy |
| `tribal_confederacy` | Tribal confederacy | A2 | oath_alliance | 3.5 → 1,050 | 1,800 | chiefdom |
| `city_state_oligarchy` | Oligarchic city-state | A3 | elite_council AND law_code | 4.5 → 2,090 | 5,620 | — |
| `territorial_kingship` | Territorial kingship | A3 | (sacral_kingship OR succession_law) AND appointed_officials | 4.75 → 2,490 | 8,850 | — |
| `theocracy` | Theocratic rule | A4 | organized_priesthood AND (law_code OR sacral_kingship) | 5 → 2,960 | 5,250 | — |
| `bureaucratic_empire` | Bureaucratic empire | A5 | provincial_administration AND standing_army | 5.75 → 4,970 | 18,690 | — |
| `assembly_democracy` | Assembly democracy | A5 | citizen_assembly AND law_code | 5.5 → 4,180 | 7,490 | — |
| `classical_republic` | Mixed-constitution republic | A5 | elected_magistracies AND elite_council | 5.5 → 4,180 | 9,470 | — |
| `feudal_monarchy` | Feudal monarchy | A6 | service_tenure AND (succession_law OR sacral_kingship) | 6.25 → 7,030 | 13,390 | — |
| `merchant_republic` | Mercantile republic | A6 | commercial_law AND (elected_magistracies OR municipal_charter) | 6 → 5,910 | 15,910 | — |
| `military_rule` | Military rule | A6 | standing_army OR service_tenure | 6 → 5,910 | 10,290 | — |
| `absolute_monarchy` | Absolute monarchy | A7 | standing_army AND (provincial_administration OR civil_service_examinations) | 6.25 → 7,030 | 20,750 | — |
| `constitutional_monarchy` | Constitutional monarchy | A7 | succession_law AND (written_constitution OR (bill_of_rights AND representative_assembly)) | 6.25 → 7,030 | 30,260 | parliamentary_government, federal_republic |
| `presidential_republic` | Presidential republic | A7 | separation_of_powers AND elected_magistracies | 6.25 → 7,030 | 32,480 | federal_republic |
| `federal_republic` | Federation | A7 | written_constitution AND (municipal_charter OR provincial_administration) | 6.25 → 7,030 | 28,280 | presidential_republic, parliamentary_government, constitutional_monarchy, one_party_state, marxist_leninist_state |
| `parliamentary_government` | Parliamentary government | A8 | ministerial_responsibility | 7 → 11,830 | 33,800 | constitutional_monarchy, federal_republic |
| `one_party_state` | One-party state | A9 | mass_political_party AND (written_constitution OR standing_army) | 8.25 → 28,130 | 71,910 | federal_republic |
| `marxist_leninist_state` | Marxist–Leninist state | A9 | mass_political_party AND central_planning | 8.5 → 33,450 | 102,610 | federal_republic |

Each form's civic block carries the content M8 will need, all qualitative and inert in M6: modules, enfranchisement, legitimacy source, verb disposition (the D-019 matrix is OPEN until M8), compatible labels and the derived government label. Definitions are descriptive and do not rank forms.

## 8. Costs — rubric and the micro/macro balance

- **Rubric.** Micro: novelty 1.0 (0.5 only for an extension of an existing civic: tax_farming, laws_of_war); difficulty = Age prior (±0.25 with a note); material 0; institutional = Age prior ±0.25–0.5 with a note (civics are institutional by nature: the seven existing civics sit 0–0.5 above the prior); breadth 0.5 / 1.0 / 1.5 by downstream scope; prereq_complexity by convention. Macro: novelty 1.0; difficulty = Age prior; material 0; institutional = Age prior + 0.25…0.75 (coordination of several modules, noted per form); breadth 1.0 (1.5 where the form reorganizes every civil domain: feudal_monarchy, military_rule); prereq_complexity by convention.
- **Age prior (digest C-14, FACT):** A1 0/0 · A2 0.5/0.25 · A3 1/0.75 · A4 1.25/1 · A5 1.5/1.25 · A6 1.75/1.25 · A7 2/1.5 · A8 2.5/2 · A9 3/2.5 (difficulty/institutional).
- **Existing anchors:** taxation 3.5 (1,050) · law_code 3.75 (1,240) · coined_wage 3.75 (1,240) · legal_code_roman 4.75 (2,490) · joint_stock 5.0 (2,960) · patent 5.5 (4,180) · census 5.75 (4,970).
- Every new node names a `costAnalog` (the existing node of the same Age with the nearest magnitude) and a one-line-per-factor `costRationale`. No cost was chosen to hit a pace (C-23).

| Age | micro n | micro magnitude min/median/max | macro n | macro magnitude median | premium over micro median | premium over reference micro |
|---|---|---|---|---|---|---|
| A1 | 1 | 1.75/1.75/1.75 | 0 | — | — | — |
| A2 | 3 | 2.25/3.0/3.5 | 2 | 3.5 | 0.5 | 0.75 |
| A3 | 10 | 2.75/3.75/4.5 | 2 | 4.625 | 0.875 | 0.875 |
| A4 | 9 | 3.5/4.25/5.75 | 1 | 5.0 | 0.75 | 0.75 |
| A5 | 3 | 4.25/5.0/5.25 | 3 | 5.5 | 0.5 | 0.75 |
| A6 | 4 | 5.0/5.375/6.0 | 3 | 6.0 | 0.625 | 1.0 |
| A7 | 11 | 5.0/5.5/6.0 | 4 | 6.25 | 0.75 | 0.75 |
| A8 | 7 | 5.0/6.5/7.0 | 1 | 7.0 | 0.5 | 0.5 |
| A9 | 1 | 8.0/8.0/8.0 | 2 | 8.375 | 0.375 | 0.875 |

**Balance rule and why.** macro ≈ the same Age's micro + 0.5…1.0 magnitude, i.e. ×1.4…×2 in cost. Breadth: a form reorganizes several domains at once (participation, administration, legitimacy), so its downstream scope is civilization-wide where a micro's is one or two domains — the +0.5 breadth step. Institutional coordination: a form binds several modules into one arrangement — +0.25…0.75 over the institutional prior, noted per form. Difficulty stays at the Age prior: the hard part of a form (its modules) is already paid in the modules' own costs; the form's closure cost = modules + premium (meta.macroClosures). Bounded at +1.0: forms are optional alternatives; a larger premium would make 'optional' mean 'unreachable' for a civilization that develops several modules, and a premium below +0.5 would make a form a free relabelling of its modules. Premium of the macro median over the micro median: A2 +0.5 · A3 +0.875 · A4 +0.75 · A5 +0.5 · A6 +0.625 · A7 +0.75 · A8 +0.5 · A9 +0.375 (A9 has a single micro, central_planning, itself economy-wide; against the A9 reference micro the premium is +0.875). See table.

**Workload if D-061 is NOT ratified (one pool).** New civic cost as a share of each Age's technology cost: A1 2.8 % · A2 7.2 % · A3 19.8 % · A4 16.7 % · A5 13.7 % · A6 17.1 % · A7 25.6 % · A8 5.0 % · A9 1.0 %. The 14–26 % at A3–A7 would compete with technology for RP and enter the AI's 'cheapest available anywhere' fallback, where technologies win cost ties (engine audit Q11) — hence OD-03.

## 9. AI valuation approach (PROPOSED for T6.16)

Goal order of an AI civics lane:

1. gate civics: the civic part of the unmet next-Age CORE closure, the research-stage closure and the university closure (legal_code_roman, law_code); the tax edict's civic while unknown (taxation)
2. structural and next-Age supporting civics (coined_wage while G-06 stands; joint_stock/patent at A7; census at A4 if it would bind)
3. civics whose owner exists in the running build (institutions it can found, policies it can adopt) — valued by that consumer
4. modules of the regime the AI's M8 evaluation prefers (M8)
5. fallback: the cheapest available civic

- Rule: composite key (remaining closure cost, ordinal) for goals and (EffectiveCost, key) for nodes, tie-dense tests (I-11)
- Rule: a deferred promise is never read as a benefit; civics with owners in a later milestone have no goal value until it ships
- Rule: civics are polity-internal: valuation reads only the polity's own state; never another polity's civics state (metamorphic fence, I-9)
- Rule: under one pool (D-061 not ratified) the cheapest-anywhere fallback must exclude civics without a shipped owner, or the 60 new civics absorb RP

## 10. Relocation candidates from Technology

Keep every candidate in Technology; record recommendations only (crossLaneRequests). Moving a node between trees changes its key (technologies-then-civics key order, ResearchContent.cs:383-390; keys are identity, Ids.cs:176-184): old saves lose it, recorded orders for it are ignored, and there is no alias or remap (engine audit Q14). Each candidate is knowledge of an instrument or technique; its institutional half is represented by a civic.

- `bill_of_exchange` — an instrument (written order to pay at a distance) — knowledge; institutional half = commercial_law; double_entry and joint_stock depend on it; a6_finance names it.
- `double_entry` — an accounting technique — knowledge; no civic half.
- `paper_money` — printed notes — knowledge (printing, anti-counterfeiting); institutional half = central_banking.
- `stamp_seal` — a technique of authentication — knowledge; a technology atom of the research stage; administrative use is taxation/census.
- `standard_weights` — metrology — knowledge; the state guarantee is part of appointed officialdom.
- `coinage_electrum` — state-stamped metal — the A5 CORE fact a5_coinage; cannot move.
- `trade` — formal exchange between communities — a live gate (TradeQuery.CanTrade); knowledge of the practice; institutional half = commercial_law.
- `surveying` — measurement — knowledge; its 'land register' capability is realized institutionally by land_registry.
- `newspaper (inst.newspaper)` — already an institution entity, not a node — nothing to move.
- `library (inst.library, building.library)` — already institution/building entities, not nodes — nothing to move.

## 11. Candidates considered and rejected

- **customary_law** — unwritten custom is the baseline of every society; as a node it had no consumer distinct from kin_council (folded into its concept).
- **royal_post (state courier relays)** — duplicates infra.courier_relay (track_road AND horse_domestication), whose realization is frozen behind a transport packet; the organizational half is appointed_officials + provincial_administration.
- **famine_relief / ever-normal granary** — already modelled: GranaryEffects.FamineIntensityFactor is explicitly the organized-relief capacity of the granary system (Sim.Core/State/GranaryEffects.cs); a civic would duplicate or gate a baseline.
- **social_insurance** — a contributory fund needs a treasury; no civic may depend on one (V-22, CR-008); revisit with a fiscal definition.
- **public_debt / state treasury / budget** — budget/treasury unassigned pending a definition (CR-008, V-22).
- **insurance, stock exchange** — no owner before M11+; financial depth.
- **international organizations / collective security** — the World Congress / Diplomatic Favor model is rejected (m6-spec §8, civ-progression §13.4); no owner.
- **political police / security service** — would gate the D-010 'repress' remedy, which the frozen menu keeps available to every regime ('no empty menus'); civil_police covers ordinary order.
- **slavery / unfree-labour civic** — not proposed: whether D-018's 'unfree-labor institution' is a researchable civic or an emergent social condition is an ethical and design ruling (OD-06); only abolition is proposed.
- **state mint, scribal school, archive, library, hospital civics** — existing inst.* entities with technology requirements; a civic would duplicate them.
- **nomadic steppe empire (separate form)** — covered by tribal_confederacy plus military modules; a separate label adds no module.
- **fascist / corporatist state (separate form)** — a one-party state with a corporatist economy; one_party_state is defined ideology-neutrally; a corporatism module could be added if M8 needs it.
- **elective monarchy, enlightened absolutism, technocracy** — variants expressible by module choice (succession_law by election; absolute_monarchy) without a new label.
- **civic Eurekas (an Inspiration analogue)** — deferred, not rejected: the civics graph may carry Eurekas under the same 0.4 ceiling; needs realized-state conditions (OD-09).
- **knotted-cord (khipu) route to census and taxation** — offered by D05; not taken up here: it would change the prerequisites of two existing civics that sit on ratified consumers (taxation is the tax-edict gate, census an Age fact) and it needs a records node that is knowledge (technology), not an institution — a candidate for a later D-047-style causal review with D05.

## 12. Civilization VI — lessons only (INFERENCE from general knowledge of the game; nothing copied)

- Two currencies, two trees, two targets (adopted shape, D-061) — but no flat yields and no policy cards (rejected, V-29).
- Give the civics currency something to buy from the first turns: four A1-A2 micro civics and two A2 forms.
- Avoid a strict government ladder: forms here are leaves over modules, reachable by OR routes, never prerequisites of each other.
- Cross-tree coupling there is through boosts only; here it is one explicit gate (the research stage) plus civic→technology prerequisites — kept explicit and measured.
- Government as mechanism: a form is the label of a module set whose effects enter named equations (arch-FGH), not a bundle of bonuses.
- Names: where a historical or anthropological term coincides with a term the game also uses (chiefdom, theocratic rule), the term is kept because it is the standard scholarly one; elsewhere the precise historical term is used (mixed-constitution republic, mercantile republic, territorial kingship). No game text, ordering or numbers were used.

## 13. Risks

- **K-1** CP pacing stalls technology through the stage atom — mitigation: tolerances P1-P6; Option B remedy.
- **K-2** the expansion lands under one pool and dilutes RP (+14-26 % at A3-A7) and the AI fallback — mitigation: OD-03.
- **K-3** content without owners: 44 of 60 new civics wait for M8, 8 for M9 — mitigation: every promise named and dated; capability strings keep them loader-valid; R-C7.
- **K-4** a regime label read by a mechanism (enum government) — mitigation: regimeModule.rule; the enum-government test (arch-FGH §2.2) in T6.5.
- **K-5** historical anchors unverified — mitigation: INFERENCE labels; corpus-stage verification list.
- **K-6** the de facto A5 gate (coined_wage) slows AI Age entry today — mitigation: OD-02; AI lane priority 2.
- **K-7** test pins move when civics are appended (7-civic pin, Lenses counts) — mitigation: count pins only; existing records unchanged; emerged markers keep the 83 pin.
- **K-8** a new root civic (`kin_council`, no prerequisite) is available at founding: it changes the available set shown in the UI and, under one pool, the AI fallback set; with no Eureka it does not move order-less goldens (engine audit Q14 'Append' row).

## 14. Open decisions for the Director

- **OD-01** D-061.8: keep legal_code_roman in the research stage? Options: A keep + tolerances P1-P6 / B move to the realized university / C OR of civic routes. Recommendation: A, with B pre-registered as remedy.
- **OD-02** a5_wages makes coined_wage de facto mandatory for A5 entry until M7 (G-06). Change Ages content? Options: add a non-civic A5 material_economic supporting fact / widen a5_wages / accept coined_wage as a gate civic. Recommendation: add a non-civic fact (restores 'supporting = optional'); until then treat coined_wage as a gate in P4 and the AI lane.
- **OD-03** Land the 60 new civics only with Civics Points (D-061.1)? Options: only with CP / under one pool, gated from the AI fallback / under one pool unchanged. Recommendation: only with CP: under one pool they add 14-26 % to the A3-A7 workload (meta.workloadByAge) and enter the AI fallback.
- **OD-04** How are regime:/policy: grants represented (Civics may unlock only institutions)? Options: a regime/policy entity kind (loader change, T6.5) / a civics content section read by M8 / capability strings only until M8. Recommendation: capability strings in M6 (no loader change), the representation decided with T6.5/D-V.
- **OD-05** Re-point seven institution requirements (G-10…G-16)? Options: accept each / reject each. Recommendation: accept: each replaces a proxy civic atom by the specific civic; reverse-index unlock lists move with them (T6.4).
- **OD-06** D-018's Enslaved/Bonded class needs an 'unfree-labor institution': a civic or an emergent condition? Options: emergent social condition (M9) / a civic adopted by policy. Recommendation: emergent condition owned by M9; this lane proposes only abolition.
- **OD-07** R4 assigns 'laws-lite' and the 'authority/bandwidth economy' to the M6 civic specification, but neither term is defined anywhere (m6-foundations-audit.md:62; digest V-21). Which civics, if any, take effect in M6? Options: provincial_administration (reach from provincial seats) and tax_farming (tax instrument) / none — M6 eligibility only. Recommendation: read 'authority economy' as reach by delegation and 'laws-lite' as instruments on existing flows: the two named, subject to the R4 architecture review; each needs an order kind and a policy row (schema, D-H).
- **OD-08** CP generated when no civic is available: lost (as RP today) or carried? Options: lost / carried (D-R analogue). Recommendation: rule with D-R; pre-literate civics are deliberately few (A1-A2: 4 micro, 2 macro).
- **OD-09** Civic Eurekas (Inspiration analogue)? Options: yes, under the 0.4 shared ceiling with realized-state conditions / no. Recommendation: design in T6.4 after D-U (cited Civ VI comparison).
- **OD-10** Civics UI lanes: strands as lanes? Options: set domain to strand-like vocabulary (UI-only) / add a strand field read by the layout / one 'governance' lane. Recommendation: a strand field read by the layout (T6.6); domain keeps existing vocabulary.
- **OD-11** Keep the Age metadata of forms with long histories (theocracy A4, military_rule A6)? The Age prior prices them. Options: as proposed / move. Recommendation: as proposed; costs move with any change (C-14).

## 15. Historical verification targets (for the corpus stage — none verified here)

| id | anchor to verify |
|---|---|
| `kin_council` | ~10 kya and earlier: councils of household heads in forager and early farming societies (ethnographic inference; no direct date) |
| `hereditary_rank` | ~4,500 BCE Varna (Copper Age status burials; interpretation as inherited rank is an archaeological inference) |
| `organized_priesthood` | ~5,000 BCE Eridu temple sequence; ~3,500 BCE Uruk temple households |
| `oath_alliance` | undated pre-literate practice (ethnographic analogy); first written attestation ~2,400 BCE in the oaths of the Ebla treaty |
| `corvee_labour` | ~2,600 BCE Old Kingdom Egypt (rotating labour crews on royal works) |
| `appointed_officials` | ~2,600 BCE Old Kingdom Egypt (royal officials); ~2,100 BCE Ur III provincial governors |
| `sacral_kingship` | ~3,100 BCE Egypt (divine kingship of the first dynasties) |
| `service_tenure` | ~1,750 BCE Babylon (ilku service-land in Hammurabi's laws); later fiefs and Ottoman timars |
| `royal_envoys` | ~2,400 BCE Ebla (royal letter to Hamazi); ~1,350 BCE Amarna correspondence |
| `sworn_treaties` | ~2,400 BCE Ebla–Abarsal treaty; ~1,259 BCE Egyptian–Hittite treaty |
| `elite_council` | ~700 BCE Spartan council of elders; Roman senate by ~500 BCE |
| `succession_law` | ~1,500 BCE Hittite Proclamation of Telipinu (order of royal succession) |
| `provincial_administration` | ~2,300 BCE Akkad (governors over conquered cities); ~740 BCE Assyrian provinces; ~520 BCE Achaemenid satrapies |
| `standing_army` | ~2,300 BCE Akkad (permanent royal troops); ~740 BCE Assyria (Tiglath-Pileser III) |
| `land_registry` | ~2,500 BCE Egyptian field surveys for assessment; 1086 CE Domesday survey; 1858 CE Torrens registration of title |
| `citizen_assembly` | ~700 BCE assemblies of Greek communities; assemblies of free men among Iron Age European peoples (Roman-era accounts) |
| `elected_magistracies` | ~683 BCE annual archons at Athens; 509 BCE Roman consuls |
| `tax_farming` | ~400 BCE Athenian tax contracts; ~200 BCE Roman publicani; 1681 CE French General Farm |
| `craft_guilds` | ~100 BCE Roman craft collegia; ~1100 CE European craft guilds |
| `municipal_charter` | ~90 CE Flavian municipal law (Lex Irnitana); ~1100 CE European town charters |
| `religious_toleration` | ~260 BCE edicts of Ashoka; 313 CE Edict of Milan; 1598 CE Edict of Nantes |
| `civil_service_examinations` | 605 CE Sui imperial examinations; 958 CE Korean state examination; 1855 CE British civil service examinations |
| `representative_assembly` | 1188 CE Cortes of León; 1265 CE English parliament with town representatives |
| `commercial_law` | ~800 CE Islamic commercial law; ~1100 CE European law merchant and fair courts |
| `central_banking` | 1668 CE Sveriges Riksbank; 1694 CE Bank of England |
| `written_constitution` | 1653 CE Instrument of Government (England); 1787 CE United States Constitution |
| `separation_of_powers` | 1748 CE Montesquieu's The Spirit of the Laws; 1787 CE United States Constitution |
| `bill_of_rights` | 1215 CE Magna Carta (a precursor); 1689 CE English Bill of Rights; 1791 CE United States Bill of Rights |
| `law_of_nations` | 1625 CE Grotius, On the Law of War and Peace; 1648 CE Peace of Westphalia |
| `official_statistics` | 1749 CE Swedish population tables (Tabellverket); 1801 CE first British census |
| `public_health_board` | 1377 CE Ragusa quarantine ordinance; 1486 CE permanent health magistracy of Venice; 1848 CE English General Board of Health |
| `compulsory_education` | 1717 CE Prussian school decree; 1763 CE Prussian General School Regulation; 1852 CE Massachusetts compulsory attendance law |
| `civil_police` | 1667 CE Paris lieutenant-general of police; 1829 CE London Metropolitan Police |
| `resident_embassies` | ~1450 CE resident ambassadors among the Italian states |
| `ministerial_responsibility` | 1782 CE resignation of Lord North's ministry (Britain); 1848 CE responsible government in Nova Scotia |
| `mass_suffrage` | 1820–1830 CE broad white male suffrage in most US states; 1848 CE universal male suffrage in France; 1893 CE women's suffrage in New Zealand |
| `mass_political_party` | 1830 CE organized national parties in the United States; 1875 CE German Social Democratic Party |
| `universal_conscription` | 1793 CE French levée en masse; 1798 CE Jourdan Law |
| `general_staff` | 1806–1814 CE Prussian staff reforms; 1810 CE Prussian war academy |
| `laws_of_war` | 1863 CE Lieber Code; 1864 CE first Geneva Convention; 1899 CE Hague Convention |
| `abolition` | ~594 BCE Solon's cancellation of debt bondage at Athens; 1833 CE British Slavery Abolition Act; 1861 CE emancipation of the Russian serfs |
| `central_planning` | 1921 CE Gosplan; 1928 CE first Soviet Five-Year Plan |
| `chiefdom` | ~4,500 BCE ranked Copper Age societies (their reading as chiefdoms is an archaeological inference); ethnographic chiefdoms of Polynesia |
| `tribal_confederacy` | ~209 BCE Xiongnu confederation; ~1450 CE Haudenosaunee Confederacy (founding date debated) |
| `city_state_oligarchy` | ~700 BCE Greek oligarchic poleis; ~1100 CE Italian communes |
| `territorial_kingship` | ~3,100 BCE unified Egypt; ~2,300 BCE Akkad; ~1,600 BCE Shang China |
| `theocracy` | ~1,070 BCE rule of the high priests of Amun at Thebes; 756 CE Papal States; 1642 CE Ganden Phodrang government of Tibet |
| `bureaucratic_empire` | ~520 BCE Achaemenid Persia; 221 BCE Qin unification of China; 27 BCE Roman principate |
| `assembly_democracy` | 508 BCE reforms of Cleisthenes at Athens |
| `classical_republic` | 509 BCE traditional founding of the Roman Republic |
| `feudal_monarchy` | ~900 CE post-Carolingian West Francia; 1066 CE Norman England |
| `merchant_republic` | ~1100 CE Venice and Genoa; 1297 CE closing of Venice's Great Council; ~1350 CE Hanseatic city republics |
| `military_rule` | 1192 CE Kamakura shogunate; 1250 CE Mamluk Sultanate; numerous twentieth-century juntas |
| `absolute_monarchy` | 1643–1715 CE Louis XIV of France; 1682–1725 CE Peter I of Russia |
| `constitutional_monarchy` | 1689 CE English Bill of Rights settlement |
| `presidential_republic` | 1787 CE United States Constitution |
| `federal_republic` | 1787 CE United States Constitution; 1848 CE Swiss federal constitution |
| `parliamentary_government` | 1830–1870 CE consolidation of cabinet government in Britain |
| `one_party_state` | 1922 CE founding of the USSR; 1925–1945 CE single-party rule in Turkey |
| `marxist_leninist_state` | 1922 CE founding of the USSR; 1928 CE first Five-Year Plan; 1949 CE People's Republic of China |

Also to verify (concept texts): the Ebla letter to Hamazi and the Ebla–Abarsal treaty as the earliest diplomatic texts; the Old Kingdom rotating labour crews as corvée; Sargon's permanent troops; the Hittite Proclamation of Telipinu on succession; the Theban high priests of Amun as theocratic rulers; the Venetian health magistracy of 1486; Solon's cancellation of debt bondage; the contested readings of 'chiefdom', 'feudal' and the Haudenosaunee founding date; and the claim behind gate audit G-19 that codified patent law preceded printed specifications.

## 16. Cross-lane requests

- **D05-communication-computing:** Keep bill_of_exchange, paper_money, coinage_electrum, trade and stamp_seal in Technology (relocation analysed and declined). *Reason:* Each is knowledge of an instrument or technique; moving a node between trees re-keys it and old saves lose it (no alias/remap, engine audit Q14). Their institutional halves are civics: commercial_law, central_banking, appointed_officials/taxation.
- **D05-communication-computing:** If cuneiform/hieroglyphic/chinese_script, paper, printing_press, paper_money, telegraph or trade are split, keep the key on the part carrying full writing, paper, the press/public sphere, printed notes, electric signalling and formal exchange — or tell D12. *Reason:* They are prerequisites of 9 new civics (meta.technologyDependencies); integrate.py rewrites only merged/replaced atoms.
- **D04-natural-science:** Keep double_entry in Technology; its subtree placement (natural_science) is D04's call. *Reason:* An accounting technique, not an institution; it names a6_finance (supporting) and has no civic half.
- **D03-science-early:** Keep standard_weights, surveying and calendar_lunar in Technology; phrase surveying's 'land register' capability as knowledge (measuring and recording boundaries) without tax effects. *Reason:* The public register of title and dues is the civic land_registry; calendar_lunar, surveying and hindu_arabic are prerequisites of organized_priesthood, land_registry and official_statistics.
- **D01-agriculture-food:** If cereal_cultivation or sheep_goat is split, keep the keys on the domestication parts. *Reason:* hereditary_rank and organized_priesthood require a storable food surplus through them.
- **D08-transport-naval:** cartography is a prerequisite of general_staff; infra.courier_relay already covers the state courier system's physical half (D12 rejected a duplicate 'royal post' civic). *Reason:* Avoid a duplicate courier node in either tree.
- **D09-military:** Do not add technology nodes for standing armies, conscription, general staffs or land-for-service; if unit families need a professional-army precondition, express it through the M7 recruitment contract reading the civic's completion, not a technology prerequisite naming a civic. *Reason:* They are civics (institutions); a technology prerequisite naming a civic breaks the V04 fence.
- **D10-medicine:** medicine_hippocratic is a prerequisite of public_health_board; inst.quarantine and inst.military_medicine are recommended to take public_health_board and standing_army instead of census (gate audit G-11, G-13). Reply to D10's request: ACCEPTED — G-14 now recommends inst.statistics_vital → 'official_statistics AND epidemiology' (fallback calculus if epidemiology is not adopted); public_health_board does not require epidemiology (its 1377 anchor predates it). *Reason:* Entity requirements are content outside lane part files; recorded for T6.4.
- **D05-communication-computing:** Reply to D05's note: acknowledged that printing_press, paper_money and bill_of_exchange move to the trunk (PROPOSED) — patent, joint_stock, compulsory_education, mass_political_party and central_banking then no longer wait on the research stage; no D12 edit needed. The khipu route is recorded as a rejected candidate for now (meta.rejectedCandidates). *Reason:* Civic prerequisites hang on D05 nodes; ids preserved.
- **D03-science-early:** Reply to D03's research-stage naming note: legal_code_roman's display name is already the tradition-neutral 'Systematic law' (Roman, Chinese and Islamic systematic jurisprudence all fit); the id stays (keys and ids are never renamed, S9). No change. *Reason:* The id is identity in saves and orders; only the display name is player-facing.
- **integration (T6.4 content: entity requirements and Age facts):** Apply OD-05 re-points if ruled (inst.guild, inst.military_medicine, inst.mint_institution, inst.quarantine, inst.statistics_vital, inst.mass_schooling, inst.central_bank) with their reverse-index unlock edits; consider OD-02 (a non-civic A5 supporting fact). *Reason:* Unlock lists must be the exact reverse index of entity requirements (ResearchContent.cs:1246-1255).
- **all technology lanes:** No technology prerequisite may name a civic; civic consequences reach technology only through entity requirements or the research stage. Do not add technology nodes that duplicate a civic (e.g. 'bureaucracy', 'postal service', 'press freedom'). *Reason:* D-061 'genuinely distinct tree'; V04 fence; R-C1.

