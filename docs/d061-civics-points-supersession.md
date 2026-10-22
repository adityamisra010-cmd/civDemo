# D-061 — Civics Points: a separate civics currency and a genuinely distinct civics tree (PROPOSED supersession record)

**Status: PROPOSED — a drafted Director supersession record awaiting the Director's ratification. Nothing in this file
is in force until a dated ruling below says "RATIFIED". No code, content, schema or golden changes accompany it.**
Prepared 2026-10-21 by the M6.1 agent on the Director's instruction "M6.1 — Begin the M6 Specification and Foundations
Audit" (§3, "Resolve the Civics Points governance conflict formally"). Tree: `claude/new-session-yavghm` @ `a0b3b25`
(= `main` `6e51a3e` + two docs-only closure commits). Format: append-only, dated, per `docs/gov-4-repository-freshness.md`
(the chain *older document → later D-decision → current implementation*; the older documents are NOT edited).

Labels: **RATIFIED** (a Director ruling on the record) · **DIRECTED** (the Director's 2026-10-21 instruction text, not
yet a ruling in the record) · **PROPOSED** (this record's proposal) · **MEASURED** (read from code) · **INFERRED**.

---

## 1. The direction being recorded (DIRECTED, 2026-10-21 — quoted from the M6.1 instruction)

> - Civics uses a separate Civics Points pool, distinct from Research Points.
> - The Civics progression graph is a genuinely distinct tree, not a small branch or renamed subset of the existing
>   science/technology tree.
> - Researching a civic makes its consequences eligible according to explicit rules; it must not automatically activate
>   a government or silently apply political effects.
> - Micro-civics represent age-appropriate practices, institutions or policies.
> - Macro-civics represent government forms or broader institutional arrangements.
> - Completing an Age must not automatically grant every government form.
> - Prerequisites, researched/available status, adoption and active effects must remain distinct.
> - Optional civic branches must not become mandatory merely to progress through the entire simulation.
> - Use Civilization VI as a source of comparative design lessons, not as permission to copy its content or assume its
>   systems fit this simulation.

And: *"The existing D-I decision and planning documents say there must be one shared RP pool and no Civics Points. Do
not edit the old ruling in place or quietly disregard it."* — *"The Director's new instruction is the intended
direction. Your job is to document its governance consequences and prepare a sound design, not to reopen the question
as if no direction had been given."*

## 2. The authoritative record this direction supersedes — exact passages (all RATIFIED unless marked)

| # | Record | Passage (verbatim) | What it fixed |
|---|---|---|---|
| S-1 | `docs/d044-research-progression-rulings.md:82` (Director verbatim, §2 RESEARCH RESOURCE) | "There is ONE shared Cognitive Load Point pool." | one pool for everything researchable |
| S-2 | `d044:86-100` | "Do NOT create: Science Points / Culture Points / Military Research Points / Medical Research Points / Engineering Research Points / Knowledge Points as a second resource" | no second named currency |
| S-3 | `d044:103` | "Do not create a second accumulated research currency." | the general prohibition |
| S-4 | `d044:363-365` (§12 CIVICS) | "Civics is Tree 2. It uses the SAME Cognitive Load Point pool." | civics funded from the one pool |
| S-5 | `d044:378-380` | "Do not create Culture Points. Do not copy Technology into Civics under another name." | no civics currency; civics distinct in semantics |
| S-6 | `d044:782-791` (Part B, the ★ "do not ask again" list) | the pool question and "How many top-level trees? Two: Tree 1 Technology, Tree 2 Civics" are marked settled | the question was closed |
| S-7 | `docs/d046-research-foundation-gate-rulings.md:202-204` (G5) | "There is exactly one shared RP pool. Technology and Civics draw from the same pool." | renamed CLP → RP; one pool restated |
| S-8 | `docs/design/director-architecture-and-decision-ledger.md:1109-1121` (§26 PROHIBITED REINTRODUCTIONS, RATIFIED per `:1481`) | "2. Separate research currencies per branch. 3. A Cognitive Pool as a second currency. 4. A general RP reserve. … 11. A reusable generic per-turn exception mechanism." | the standing prohibition list |
| S-9 | `docs/adr/adr-030-per-turn-research-points.md:37` (§3) | "No reusable exception mechanism. … A second per-turn quantity needs its own ADR." | the per-turn carve-out is RP-only |
| S-10 | `docs/m6-plan/m6-decision-register.md:230-234` (dated note 2026-10-20, D-I RULED) | "one shared Research Points pool and one target. Civics is a distinct progression graph on `ResearchTree.Civics` with its own node costs, prerequisites and adoption preconditions. No separate Civics Points currency." | the 2026-10-20 closure ruling |
| S-11 | `docs/m5-exit-inventory.md` §13 ("M6 decisions") and `docs/m6-plan/m6-scope-register.md` S-03, §4 row 1, §5 bullet 5, §7 criterion 2 | the same ruling carried into the closure record and the planning package | planning input, PROPOSED/RECORDED |
| S-12 | `Sim.Tests/.../ResearchEngineTests.SharedPool_OneTargetAcrossBothTrees_TotalInvestedEqualsTotalRp` (MEASURED name from the decision register; file:line in the T6.1 audit) | the built behaviour: the 7 civics nodes cost RP from the one pool, one target | code pins the one-pool rule |

**Chronology, stated so the record cannot be misread:** D-044 (2026-09) and D-046 ruled one pool. The Director's
2026-10-20 M6-initiation instruction asked for Civics Points ("Do not silently reuse Research Points or combine the
currencies without an explicit, evidence-backed design decision" — quoted in `m6-decision-register.md` D-I). The
planning package recommended keeping one pool; the Director's closure rulings of 2026-10-20 took that recommendation
(S-10). The 2026-10-21 M6.1 instruction (§1 above) directs Civics Points and a genuinely distinct tree. **This record
treats the 2026-10-21 direction as the Director's intended direction and the 2026-10-20 D-I ruling as the ruling it
supersedes.** The 2026-10-20 ruling is not edited; it is superseded in whole by the dated ruling block in §9 when the
Director signs it.

## 3. What is superseded, and exactly how far (PROPOSED scope)

| Item | Disposition under this record |
|---|---|
| S-1…S-5, S-7 (one pool; no civics currency) | **SUPERSEDED for Civics only.** Technology keeps the one RP pool, one target. Civics gets a second accumulated currency, **Civics Points (CP)**, with its own pool and its own target. No third currency is created; the "do not create" list in S-2 stays in force for every other name on it. |
| S-6 (★ do-not-ask-again) | **Re-opened once, by the Director, and closed again by this record.** The ★ marker is honoured in that the agent did not re-ask; the Director re-asked. |
| S-8 items 2 and 3 | **SUPERSEDED for the Technology/Civics pair only** (two currencies, one per top-level tree). Items 4 (general RP reserve) and 11 (reusable per-turn exception) stay prohibited: CP is not a reserve, and CP's per-turn treatment needs its own ADR (next row), not a generic mechanism. |
| S-9 (ADR-030 §3) | **Stands, and is the reason a new ADR is required:** `ADR-043 — Per-turn Civics Points` (PROPOSED, to be written with the M6.4 packet, ratified by the Director) scoping the second per-turn exception exactly as ADR-030 §2 scopes the first. |
| S-10, S-11 (D-I ruled; planning package) | **SUPERSEDED.** D-I is re-ruled by §9. The planning package's S-03, §4 row 1, §5 bullet 5 and §7 criterion 2 receive dated notes pointing here (not rewrites). |
| S-12 (the one-pool test) | **To be superseded by a dated pin in the M6.4 packet:** the test's property (invested RP across both trees equals total RP) becomes two properties (invested RP = total RP within Technology; invested CP = total CP within Civics). The old test is not deleted before the new pins exist. |
| D-044 R9 (one target), R11/R14 (completion = eligibility only), R3 (two top-level trees), R8 (non-linear prerequisites), the five Technology subtrees | **Unchanged.** One target *per pool*; completion still grants eligibility only; still two top-level trees. |
| civ-progression §5.4 (Director ruling: RP → civic completion → capability → eligibility → adoption) | **Unchanged in structure; the first term becomes CP.** |
| D-D (M6 eligibility, M8 adoption), D-E, D-F | **Unchanged.** |

## 4. Why a second currency is a material architecture change (not a rename)

1. **Dimensional.** RP is the only quantity allowed to be per-turn (ADR-030 §1–§2; law 3 otherwise). CP would be the
   second. Either CP is per-turn (a second scoped exception → ADR-043) or per-sim-year × `dtYears` (law 3 as written,
   but then civics pacing varies with the era band differently from technology's). The spec must choose and declare the
   unit; this record recommends **per-turn, by ADR-043, mirroring ADR-030** so the two trees pace alike.
2. **Ownership and state.** A CP pool, a civics target and per-civic progress are new polity-scoped state → new rows →
   a schema bump → POPULATED-table tests → golden re-pins with attribution (CLAUDE.md laws; D-H sequencing).
3. **Gating discipline.** CP must never be the left operand of a gate (arch-O II.2 fence, `arch-O:221-250`): only *node
   completion* gates. CP's name must not enter the published-variable registry readable by D-020 predicates.
4. **Engine.** The one-engine rule (risk R-02): no second loader, predicate grammar or availability query. The existing
   `ResearchSystem` / `ResearchQuery` are parameterized by (tree, pool, target) rather than duplicated.
5. **Calibration.** ADR-030's research-rate report is RP's measured baseline; CP needs its own measured baseline and a
   corridor-independence statement (arch-PQ Q-26) before any civics corridor is named.
6. **AI symmetry.** The AI must pursue a civics target with the same information constraints as the player
   (Spine principle 7); `AiResearchPolicy`'s tax goal currently reaches `taxation` through the RP target.

## 5. Affected interfaces, accounting, persistence, AI, UI, tests, calibration, consumers (INFERRED from the record; the
T6.1 audit `docs/m6-foundations-audit.md` carries the file:line evidence)

| Area | Today (MEASURED by T6.1) | Change implied by CP | Who decides |
|---|---|---|---|
| Research interfaces | `ResearchQuery.{IsAvailable, EffectiveCost, TargetOrder, IsKnowledgeEligible, Holds, RequirementMet, CheapestAvailable}` over one key space; `ResearchTree.{Technology, Civics}` tag | add a pool/target dimension: `SetCivicsTarget` order kind (next free ≥ 14), per-pool `CheapestAvailable`; no new query type | agent proposes shape; Director ratifies via spec |
| Resource accounting | RP per turn = 0.08·P^0.699 (ADR-030, RECORDED; T6.1 verifies) → one target; overflow to next target | a CP generation formula with its own inputs (population? institutions? literacy?) — **not** a fraction of RP (that would be a split, not a currency); CP overflow rule | **Director** (the formula's inputs are a design ruling) |
| Persistence / schema | v34; readable {32,33,34}; v35 reserved for fog (D-055 note) | new tables: polity CP pool, civics target, civics progress (or a `tree` column on existing progress rows if append-only permits) → one schema bump per D-H; migration of the 7 civics nodes' existing RP progress (carry as CP 1:1, or reset with a dated attribution) | **Director** (D-H slot; migration rule) |
| AI | `AiResearchPolicy` goal-sequential incl. `TaxGoal` via the RP target | a civics goal lane targeting CP; composite-key argmax; information fence under fog (M6.23) | agent proposes; metamorphic tests required |
| UI | one research panel, one target; civics nodes listed under the same panel | a civics panel or tab with its own target and CP figure; Available vs Adopted display (D-D) | agent proposes (M6.7) |
| Tests | `ResearchEngineTests` (39), `ResearchContentTests` (50), `SharedPool_OneTarget…` | the one-pool pin superseded by two conservation pins; new turn-exact delivery pin for the civics target order; populated-table tests | agent writes; Director accepts in the packet |
| Calibration | ADR-030 research-rate report; no knowledge corridor (Q-26 open) | a CP-rate measurement; corridor independence declared before any civics corridor | spec author; Director if a corridor is proposed |
| Existing civics consumers | 7 civics nodes (law_code, legal_code_roman, census, coined_wage, patent, joint_stock, taxation); the tax gate `taxationRequires`; Age milestone `a5_systematic_law` CORE = legal_code_roman | the nodes move to the CP pool; the consumers are unchanged (they read completion); the Age-milestone link is reviewed so that "completing an Age must not grant every government form" holds | agent audits (M6.16); Director rules on Age-milestone coupling |
| Content | one `research.json` key space, `tree` field | **"genuinely distinct tree"**: either a separate content file (`civics.json`) on its own key space, or the same key space with a validator that forbids cross-tree prerequisites except through explicit "exposure" edges. Micro/macro kinds; optional-branch rule | **Director** (key-space choice is a content-architecture ruling) |

## 6. Decisions the agent may propose vs decisions that require the Director (PROPOSED split)

**Agent proposes (in the spec, for ratification with it):** the engine parameterization; the order-kind number and
delivery semantics; the content schema for micro/macro civics and the validator rules (including "every Age's CORE
milestone set is reachable without any optional branch"); the UI layout; the test plan; the migration mechanics once
the migration rule is ruled.

**Director ratifies explicitly (each is a separate line in §9):**
- **D-061.1** The supersession itself (§3).
- **D-061.2** CP's dimension: per-turn by ADR-043 (recommended) or per-sim-year under law 3.
- **D-061.3** CP generation inputs (recommended: a population term with its own exponent, plus an institutional term
  from Active institutions; no RP term; no fraction-of-RP split).
- **D-061.4** The civics key space: separate content file (recommended) or shared key space with a cross-tree fence.
- **D-061.5** Migration of existing civics progress at the schema bump: carry 1:1 as CP (recommended) or reset.
- **D-061.6** The schema slot (D-H): civics tables take the next landed version; fog's v35 reservation moves by dated
  note if civics lands first.
- **D-061.7** One civics target (recommended, mirroring R9) or several.
- **D-061.8** Whether the Age-milestone coupling of civics nodes (InstitutionalSocial category) stays as is.

## 7. What stays blocked until ratification

All M6 gameplay implementation (S8 §4; CLAUDE.md "Never implement ahead of the ratified spec"). Specifically M6.4
(civics model), M6.5 (content), M6.6 (government forms), M6.7 (civics UI), M6.23 (AI civics), M6.17's civics tables.
Nothing in M6.2 (audit), M6.13 (fog), M6.14 (E1), M6.15 (export) waits on this record.

## 8. Comparative Civ VI note (scope-neutral)

Civilization VI funds Culture (civics) and Science (technologies) from two separate yields with two separate trees and
two active targets; policy cards are the realization channel. This record adopts the *two currencies, two trees, two
targets* shape as a comparative lesson and **rejects** the policy-card bonus model (civ-progression §5.3; law 2
"mechanisms over modifiers"; scope register §5). The comprehensive comparison is a separate deliverable (M6.1 review
package §4) and does not widen M6's scope through this record.

## 9. Ruling block (append-only; the Director writes here)

> _[empty — awaiting the Director. Suggested form: "2026-10-DD — RATIFIED D-061.1–D-061.8 as recommended / with
> the following amendments: …" Until a line is written here, every passage in §2 remains in force as written.]_


## Dated note — 2026-10-21 (M6.1 docket; appended, §1–§9 unchanged; §9 still empty)

- **§5 gains a row — Technology eligibility.** The civic `legal_code_roman` gates the research stage (all five Technology
  subtrees), `inst.university` and the A5 CORE milestone; census/coined_wage/patent/joint_stock gate 13 `inst.*` entities
  (`research.json:254, 28984-29152`; `ages.json:85`). Under this supersession those gates are paced by CP. **D-061.3 and
  D-061.8 must be ruled together**, and T6.3 measures reachability before/after (docket C.1).
- **§6 D-061.4 recommendation REVISED:** shared key space with a reserved civics range and a cross-tree fence, not a
  separate file — re-keying existing rows at migration would rewrite rows, which the append-only premise behind the
  readable set forbids (`CanonicalSchema.cs:14-16`; docket C.5). D-061.5's "carry 1:1" becomes trivial under this choice.
- **§6 D-061.8 WIDENED:** "whether civic atoms inside Technology gates (the stage, `inst.university`) and Age milestones stay
  as is" — recommended yes, with the rule that no macro civic is ever an Age CORE milestone.


## Director intent note — 2026-10-22 (NOT RATIFIED; input for a future dated ruling)

Appended 2026-10-22; §1–§9 and the dated note of 2026-10-21 are unchanged; §9 is still empty. This note records the
Director's stated intent as **input**. It is not a ruling, it marks nothing RATIFIED, and every passage in §2 stays in
force until a dated line in §9 says otherwise.

- **Two separate pools, population-led Civics Points.** RP and CP are separate pools feeding distinct trees. CP comes
  partly from population (a population-based formula: larger population, more CP) and partly from other source
  categories named as directions, not as claims that they exist: social-sciences universities, colleges, embassies,
  spies, artists, literary scholars and other justified institutions or specialists. CP is **not** a fixed fraction or
  a split of RP; its inputs are defined independently (consistent with D-061.3's "no RP term, no fraction of RP").
- **Exchange only on the government-change turn.** An RP↔CP exchange is allowed only in the turn the empire changes
  government; there is no general conversion. Direction, ratio, limits, transaction order and eligibility are OPEN
  DECISIONS; no exchange is implemented. Government change is M8 (D-D), so any exchange is IMPLEMENTATION DEFERRED to
  M8 and needs its own dated ruling and ADR scope (the package's analysis: `cp-economy-proposal.md` §3, §5 G-2).
- **Happiness penalty not developed.** A temporary happiness penalty was mentioned only as an example; it is explicitly
  not developed, tuned or prototyped.
- **Supersession only through the formal process.** The single-pool rulings listed in §2 (S-1…S-12) may be superseded
  only through this append-only record and a dated Director ruling in §9; no older document is edited.
- **Staged ratification.** Architecture first; mechanics and content at later gates.
- **Pointer.** The design analysis for this intent — source catalogue, candidate CP equations, the exchange's
  structural limit, and the `legal_code_roman` cross-pool gate options with a static ESTIMATE — is
  `docs/m6-plan/knowledge-redesign/cp-economy-proposal.md` (PROPOSED; not ratified).

## §9 ruling — appended 2026-10-22 (recorded by the agent on the Director's explicit instruction; §9's placeholder above is left as written)

> **2026-10-22 — RULED by the Director (D-062 R4, R5; `docs/d062-m6-director-final-rulings.md`).** D-061.1 RATIFIED:
> Civics Points are a separate currency from Research Points, **accumulate in a bankable balance** (beyond this
> record's proposal: a balance, not only per-node progress) and are spent on civic progression. D-061.8: the research
> stage no longer requires `legal_code_roman`; `inst.university` keeps it. D-061.2–D-061.7 were delegated to the
> agent and are decided in ADR-043: CP per sim-year (law 3); a population term now, other sources deferred; shared key
> space with keys 1001–1007 kept (no re-keying, so existing progress carries 1:1); schema v35 for the M6 slice; one
> civics target. The government-change-turn exchange stays unimplemented (M8).

