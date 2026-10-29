# Director directions, 2026-10-24: civics expansion, government switching, Age sweep rule

**Status: PROPOSED design data and recorded Director direction, for review in the tree viewer. Nothing here is runtime
content or code yet.** Wording ratification is pending (§4). The review page reads `civics-tree-proposal.json`,
`technology-tree-proposal.json` and `data/government-packages.json`.

## 1. Civics expansion (D13 lane)

**Why the tree was small.** The D12 civics lane covered government and law only (67 civics). The Civilization VI study
(`civ-vi-comparative-research.md`) compared mechanics, not civics coverage, and Civilization VII was not checked for
content. So whole domains that both games' civics cover were absent from both of our trees: religion, philosophy and
learning, arts and recreation, media, exploration and overseas rule, modern ideologies, and environment.

**What changed.** `data/proposal-parts/D13-civics-expansion.json` adds 121 civics, written by
`tools/author_d13_civics.py`:

- 7 new strands: belief & religion (11), thought & learning (11), arts & culture (10), information & media (10),
  exploration & overseas rule (6), political ideas & ideologies (7), environment & urban order (8).
- Extensions of the D12 strands (law, administration, economy, society, diplomacy, military, legitimacy, representation).
- 4 more government forms: steppe empire, elective monarchy, revolutionary republic and fascist state.

The civics tree now has 188 nodes; the technology tree is unchanged at 494.

Every new civic records its Civilization VI and VII analogues (`civic.comparativeAnalogs`). The Civilization VII
analogues are taken from that game's shipped data. The Civilization VI analogues for civics are from memory and will be
replaced by the cross-check table once the Civilization VI civics list is in.

**Key stability.** `tools/integrate.py` now places D13 keys after the D12 range, so keys 1008–1067, which runtime content
already carries, are unchanged. A run without the D13 file reproduces the previous files byte for byte.

**Validator results.**

- Graph ERROR 0; chronology covers 817 dated must-hold edges.
- 121 C01 errors remain: the new civics have no educational text yet. The corpus stage is still to do and is blocked
  from shipping anyway.
- The V18 warnings are deferred grants, the same class as D12's.

**Proposed institution re-points (OD-11).** The D13 civics are candidates to replace these entities' current technology
requirements:

| Institution | Proposed civic requirement |
|---|---|
| `inst.scribal_school` | `scribal_schools` |
| `inst.archive` | `royal_archives` |
| `inst.natural_philosophy` | `philosophical_schools` |
| `inst.scientific_society` | `scientific_societies` |
| `inst.mass_media_broadcast` | `public_broadcasting` |

**Not included, and why.**

- Social insurance and public debt: R-C9 / CR-008, because there is no treasury.
- Chattel slavery as a civic: an OPEN DECISION. Abolition exists; the class system (D-018) may model the institution
  instead.
- Speculative future governments (Civilization VI's Future Era forms).

## 2. Government forms: switching and built-in policies

**Direction.** Once a government form's node is researched (its prerequisites first, then the node itself), the player
may switch to that form. Each form's advantages are visible on its node. There are no policy cards. What Civilization VI
spreads across Military, Economic, Diplomatic and Wildcard card slots is fixed per government and built in.

**Data.** `data/government-packages.json` is written by `tools/author_government_packages.py` and covers 22 forms. Each
records:

- who rules and the source of legitimacy;
- its built-in policies by category, with trade-offs;
- its Civilization VI analogue and that government's card-slot counts (base-ruleset `Governments.xml`), and its
  Civilization VII analogue;
- its building-block civics (the module set).

**How law 2 is kept.** Every built-in policy changes a coefficient inside a named resolution equation. Where the
equation is live today, the policy names its `sim.json` key and shows `current → under this form`. Examples:

- administrative reach, `governance.authorityDecayCostUnits`: 25 → 35 under the bureaucratic empire;
- tax extraction, `governance.taxExtractionResponseMax`;
- the trade gap and trade cost;
- class promotion, migration and granary relief.

Where the equation is not yet built, the policy names the owning system and milestone (Battle layer M7, Diplomacy and
legitimacy M8, needs and grievance M9). All values are TUNE.

**Switching rule (proposed).**

- **Order and parity.** One `AdoptGovernment(form)` order serves player and AI alike. It is valid once the form's node
  is researched.
- **First adoption.** The first adoption, from kin rule, is free and immediate.
- **Cost.** A later switch costs Civics Points equal to 25 % of the new form's research cost. If the bank is short, the
  order is refused and nothing is paid.
- **Transition.** The switch is followed by a transition of 2 sim-years plus 1 year for each class whose enfranchisement
  changes. During it the old package is off, the new one is not yet on, and tax extraction runs at half its rate.
- **Limits.** No new switch while a transition runs, and none within 10 sim-years of the last switch. Both are measured
  as elapsed sim-time since the polity's own act, which is state, not a calendar gate.
- **Exchange.** The RP↔CP exchange of `cp-economy-proposal.md` §3 rides on the same order.

**Milestone.** Adoption is M8 (D-D; D-062 R3), so the switch itself arrives in M8. Until then each node shows its
package as available when switching arrives. Moving adoption earlier would change the ratified milestone order, so it
needs an explicit ruling.

## 3. The Age sweep rule

**Direction.**

- No empire may research early nodes late; for example, dog domestication must not still be open in Age 4.
- To research any node of Age x + 2, every node of Age x must already be researched, in both trees.
- Ages 1 and 2 are exempt.

**Effect.**

- Age x + 2 entry now implies the Age x sweep, because every Age's CORE facts are research nodes of that Age. Age 3
  needs all of Age 1, Age 4 all of Age 2, and so on up to Age 9, which needs all of Age 7.
- Nothing is optional across two Ages any more. "Optional" now means only "not needed for the next Age". That includes
  every government form and every regional node; researching a form is not adopting it.

**Checked on the proposed graph** (fixpoint with the stage gate and the sweep rule):

- all 682 nodes remain reachable, so there is no deadlock;
- no prerequisite edge points to a later Age.

**What each sweep costs.** "Prior Ages" is the cumulative cost of all Ages before the one shown, both trees, which is
what the next-but-one Age requires.

| Age | Technologies | Technology cost (RP) | Civics | Civics cost (CP) | Prior Ages, RP | Prior Ages, CP |
|---|---:|---:|---:|---:|---:|---:|
| 1 | 28 | 11,190 | 5 | 1,460 | — | — |
| 2 | 52 | 47,710 | 7 | 5,570 | 11,190 | 1,460 |
| 3 | 41 | 87,080 | 23 | 34,200 | 58,900 | 7,030 |
| 4 | 27 | 83,870 | 17 | 40,790 | 145,980 | 41,230 |
| 5 | 35 | 158,140 | 23 | 79,840 | 229,850 | 82,020 |
| 6 | 42 | 185,020 | 17 | 75,580 | 387,990 | 161,860 |
| 7 | 37 | 307,930 | 33 | 175,590 | 573,010 | 237,440 |
| 8 | 78 | 1,831,250 | 40 | 417,450 | 880,940 | 413,030 |
| 9 | 154 | 10,446,720 | 23 | 624,740 | — | — |

**Implementation (recommended).**

- **One engine rule.** Research eligibility adds the predicate "every node whose Age is a − 2 is complete" for a node of
  Age a ≥ 3, applied in `ResearchQuery` for player and AI alike. Content stays as it is: causal prerequisites stay
  causal and are not padded with hundreds of atoms.
- **Visible in the UI.** The node card shows the sweep as its own requirement line with progress ("23 of 33 Age 3
  nodes").
- **AI.** Every AI goal closure adds the cheapest remaining nodes of the open sweep; otherwise the AI stalls at the
  sweep.
- **Validator.** A V27 check (sweep fixpoint; no prerequisite edge to an Age two or more later) joins the validator.

The rule ships together with the reviewed civics content, so the goldens are re-pinned once rather than twice.

**Tensions with frozen rules,** to be confirmed in the ruling text:

- **Law 4 (no gates from dates or era labels).** Eligibility still derives from computed state, namely which nodes are
  complete. The Age label only defines the content set, in the same way a prerequisite list is content. Reading it as
  compliant needs your confirmation.
- **D-047 ruling 1 (causal prerequisites).** The sweep is deliberately not a prerequisite edge, so causal edges stay
  causal.
- **D12 rules R-C2 and R-C5, and validator V24.** Every civic now sits inside some Age's mandatory closure, so
  "optional" must be redefined as above.

**Pacing risk (ESTIMATE; must be measured).** Civics Points are the likely bottleneck. D13 roughly doubles to quintuples
civics cost per Age: Age 8 civics go from 69,700 to 417,450 CP. Entering Age 9 now needs every Age 7 node, which is
413,030 CP of cumulative civics. The measurement plan is to re-run seeds 1, 2, 42 and 7 to turn 1,600 with and without
the rule and report Age entries. Costs may then be retuned, without editing nodes to hit pacing (C-23).

## 4. Pending

1. Your review of the nodes in the viewer: the flags and notes are read back from its store.
2. Ratification of the wording of §3 (the reading of Law 4, and the redefinition of "optional").
3. The Civilization VI and VII cross-check tab (in progress).
4. After review: import the reviewed civics into `research.json`, implement the sweep rule (ADR) and its AI goal, write
   the corpus texts for the new civics, measure pacing and re-pin the goldens with attribution.

---
**Dated note, 2026-10-28:** §4 item 2 ("optional" = not needed for the next Age) was ADOPTED under the Director's 2026-10-28 decision rules (D-066 A6). It is implemented as the I-7 validator (ADR-057). Director confirmation is requested at the M6 exit gate. Text above unchanged.
