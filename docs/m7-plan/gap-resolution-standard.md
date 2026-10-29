# Gap-Resolution Standard (reusable) and the bounded charter audit of 2026-10-27

**Status: PROPOSED working standard.** This is documentation only. It changes no content, code or rule.

**What it is for.** A gap is any place where:
- an intended capability cannot be realized; or
- it is realized at the wrong point; or
- it has no consumer; or
- it contradicts chronology or a ruling.

The standard turns a gap into one of three outcomes:
- a resolved design choice;
- a proposal with evidence requirements;
- an escalation the Director can answer without re-deriving the problem.

First applied to aviation ([aviation-progression.md](aviation-progression.md)); the audit in §3 applies it to both
charters.

---

## 1. The method

| Step | Question | Output |
|---|---|---|
| **1. Detect** | Where does the intended progression diverge from what the tree, content or rules would actually do? Sources: rulings, chronology, a scan of the data, a measurement. | One sentence stating the gap |
| **2. Trace** | Follow the chain end to end: prerequisite → research node → capability → building, unit or order → consuming system → goods, labour, upkeep → UI and AI → tests and audit | The first broken or missing link, by name |
| **3. Measure** | What does the live tree do, in turns, on seeds 1, 2, 42, 7? Read rules from code, not from summaries: availability, the Age sweep, AI goals, pacing scales. | A table of measured numbers, with the tree head |
| **4. Compare** | Against chronology (as orientation, never as a gate: Law 4) and against every binding ruling and ADR. Which items are frozen? | The constraints the fix must keep |
| **5. Design** | The complete progression, with a named consumer for every capability. Statuses kept separate: existing / M7 candidate / M8+ / blocked / design-only. | Stages, identities, buildings and goods, each with an owner |
| **6. Smallest fix** | The smallest causally valid change. Prefer data over code, a tag over a node, a node over a mechanism. State the rejected alternatives and why they are larger or wrong. | One proposal + rejected list |
| **7. Evidence** | What must be measured before adoption, by whom, with which attribution twin or pin | E-items |
| **8. Classify** | Is the question **answered by a ruling** (apply it, do not reopen), **routine** (decide it here; the Director may overturn), or **escalation-worthy**? Escalation is only for a genuine conflict between ratified items, or a material scope or cost trade-off. | Disposition + basis |
| **9. Record** | Charter text; proposals marked as proposals; open decisions with exact evidence; a CR when frozen items conflict (the CLAUDE.md conflict rule) | Diff limited to documentation |

## 2. The checklist (copy into any gap review)

- [ ] The tree head and the branch are stated; facts were re-read from the tree, and prior reports were treated as
      secondary evidence.
- [ ] Every id named exists in `research.json` / `unit-families.json` / `goods.json`, or is marked "(new)" or proposed.
- [ ] The gap is traced through all nine links of step 2; the first broken link is named.
- [ ] Eligibility rules were checked in code:
  - availability ignores the polity Age (D-044 R13);
  - the Age sweep reads content Age a − 2 in both trees (D-063 R2);
  - the AI goal set (`AiResearchPolicy`);
  - the pacing scale per (tree, Age).
- [ ] Recruitment reads knowledge, not the Age (unit charter §6.5). Modernization reads the Age (ADR-031). Each case was
      considered.
- [ ] Inside A9 (the last Age), ADR-031 does no work: in-Age progression needs branches or re-equipment (D-065 R1).
- [ ] No identity or building is gated on a node of a later Age than itself unless a proposal fixes it.
- [ ] A counter never precedes its threat.
- [ ] Every proposed entity is classified A–E under the architecture rule (unit charter §4.5, D-065 R8). It
      states its lifecycle, ownership, persistence, consumer, map presence, presentation and no-double-counting rule.
      Military relevance alone never makes a unit family.
- [ ] Every capability has a named consuming system; otherwise it is blocked or deferred, never "M7 candidate" by
      assumption.
- [ ] Missing goods are named and assigned to a packet; no good is written into a recipe by a design document.
- [ ] No global or free-floating multiplier (Law 2). Every "bonus" is a term inside a named equation with a twin and a
      measurement.
- [ ] Conservation (people, goods), dt-correctness, determinism (composite keys, tie-dense tests), replay and delivery
      pins, and populated-table tests are listed for each new row type or order.
- [ ] Milestone ownership respected:
  - M6 knowledge;
  - M7 battle and military realization;
  - M8 politics;
  - M9 society;
  - M10 integration;
  - M11+ depth.
  Carrier combat, espionage operations and advanced energy are not pulled forward.
- [ ] Settled rulings applied, not reopened; TC-1 left PROPOSED; a tree change written as a proposal with E-items.
- [ ] Escalations limited to genuine conflicts and material trade-offs, each with the exact evidence needed.
- [ ] The diff touches documentation only (unless the packet authorizes more).

## 3. The bounded audit of both charters (2026-10-27, head `0bf7626`)

**Scope.** Bounded to gaps of the aviation kind. Not covered: costs, balance numbers, educational text, and anything the
2026-10-26 reviews already settled.
- Identity or building Age against knowledge Age.
- Sweep-tag gates.
- Final-Age progression.
- Consumerless capabilities.
- Rate parities the Director named.

| # | Gap found | Trace / measurement | Disposition |
|---|---|---|---|
| G-1 | **Aviation gated at A9 by the sweep** | §2 of aviation-progression: 89–121 turns of A8 blocked | Proposal RC-1 (OD-1) |
| G-2 | **One air identity for all of powered flight:** 1101's only gate is `aircraft`, and §6.5 does not read the Age | `unit-families.json`; §6.5 | Resolved: knowledge-gated stages; `strike_aviation` (23) |
| G-3 | **Bombers as branches would modernize into fighters** | ADR-031 §3 rule 2; `UnitFamily.Successor` | Resolved: family split (unit charter §7.22) |
| G-4 | **No progression inside A9 for the other families** (armour 605, siege 806, line infantry 308, ranged 408, light cavalry 506 each have one A9 identity covering about a century) | Same cause as G-2/G-3: A9 is the last Age | **Routed, not resolved:** design-only. Each family adopts the aviation pattern (A9 branches + re-equipment) when its goods packet lands (EC-4a/EC-5, M11+). No ruling needed. |
| G-5 | **Knowledge-early identities:** five identities sit one or two Ages after their gating knowledge: spearmen 201 (A2; `hafting` A1), archers 402 (A2; `bow_simple` A1), crossbowmen 404 (A6; `crossbow` A4), knights 603 (A6; `stirrup` A5), bombards 804 (A7; cannon nodes A6) | Scan of `realizedBy` requirements vs identity Ages. ADR-031 never downgrades (rule 3 preserves), so this is safe. But a knowledge-early identity counts toward later `aN_formation` milestones (`minIdentityAge`), and it is recruited ahead of intermediate identities by "latest realizable". | **Resolved (design): keep.** It is computed-state progression (Law 4). M7.3 calibration must measure how often a knowledge-early formation satisfies a later Age's formation milestone. |
| G-6 | **Entity-gating nodes dated before their Age band** (21 nodes, for example `cast_iron` A5 at 500 BCE, `paper` A5, `hydroelectric` A9 at 1895) | A date-band scan with approximate bands (A5 to 1000 CE, A6 1000–1450, A7 1450–1760, A8 1760–1900, A9 1900+) | **No action:** these are TC-1 placement choices (global diffusion dates). No ruling requires them earlier, and TC-1 allows tree changes only for a material defect. Listed for the TC-1 confirmation review. |
| G-7 | **A capability with no consumer:** the tier-1 landing strip is buildable from existing goods | Construction exists; no air unit exists | Resolved: **blocked** until air units are realizable (consumer rule) |
| G-8 | **Carriers reachable in A8 knowledge with an A9 identity** | `unit.aircraft_carrier` = `aircraft` AND `steam_turbine` (A8 under RC-1) | Resolved: kept. Consistent with HMS Argus (1918); materials block it in any case. OD-5 records the deviation from the brief. |
| G-9 | **Air defence beginning at A9** while its threat moves to A8 | the counter rule | Resolved: AA artillery at A8 (unit charter §7.10) |
| G-10 | **Intelligence-row inconsistency:** §13 row IN said "M8 (gathering)" while §5.1 and D-037 E4 say M8 hooks only | text comparison | Resolved: row aligned (Q9) |
| G-11 | **Base RP ≠ base CP** (Director direction D-065 R5): equal only in 10-year turns; CP is 1/20 of RP in 0.5-year turns | `research.json` tuning; ADR-030; ADR-043 §3; the `era-pacing.json` bands | **Escalated:** frozen items conflict (Law 3 / ADR-030 §3 / ADR-043). [CR-026](../adr/cr-026-research-and-civics-base-rate-parity.md) recommends a scoped per-turn CP exception with one shared base function and a civics pacing recalibration. |

**Not audited (stated, not silently skipped):**
- naval generations inside A9 beyond carriers (folded into G-4);
- civics-tree gating of military doctrine identities;
- the balance bands of every sheet.
Each would be its own bounded pass.
