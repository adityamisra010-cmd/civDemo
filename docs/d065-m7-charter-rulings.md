# D-065: M7 charter rulings: re-equipment, EC packets, staged aviation, energy owner open (Director, 2026-10-27)

**Status: RULED. Recorded from the Director's charter review of 2026-10-27** ("M7 Charter Review: Aviation Progression,
Approved Rulings, and Proactive Gap Resolution").

This record adds rulings. It edits no earlier record. Precedence follows gov-4 §1: an explicit Director ruling ranks
above an earlier one where they differ, and the earlier text stays unedited.

**Applied in:**
- [unit charter](m7-plan/unit-catalogue-and-progression.md) §1, §6.6, §7, §13, §16;
- [building charter](m7-plan/building-and-infrastructure-charter.md) §5.19 and §12;
- [aviation progression](m7-plan/aviation-progression.md).

| # | Ruling (Director's words, essentials verbatim) | Relation to earlier decisions |
|---|---|---|
| R1 | "Manual re-equipment is permitted as a distinct recruitment/re-equipment order." It needs "explicit legality, material and labour costs, personnel conservation, experience handling, identity/family transitions, deterministic order resolution and replay behaviour." "Do not conflate it with ADR-031's automatic, free Age-transition modernization; preserve the latter unless a separate ruling changes it." | Answers unit charter Q1. ADR-031 and D-047 ruling 18 stand for Age-transition modernization; the Civ VI-style paid upgrade stays barred. The boundary case (a same-line "catch-up" to the current mainline identity) is **not** covered by either ruling and is raised as an open decision, not decided here (aviation progression §13, OD-2). |
| R2 | "EC-1 to EC-3 are separate, individually reviewed packets within M7 (metals, mounts, gunpowder). Do not combine them." | Answers unit charter Q5 (order and scope). Each packet still needs its own approval. EC-4 to EC-6 are unchanged by this ruling. |
| R3 | "Aviation must progress through multiple stages across Ages, not all at A9." | Answers unit charter Q3 and conflict C9. The stages are Flight I (late A8), Flight II (A9) and Flight III (later A9). The research-content change this needs (RC-1) is a proposal and needs TC-1 approval. |
| R4 | Q8/B5 (energy-system ownership) "remains unresolved. Record it as open; do not invent a ruling." | Unit charter Q8 and building charter B5 stay **open**. |

## Addendum (same day): base knowledge and civics rates

| # | Direction (Director's words, verbatim) | Relation to earlier decisions |
|---|---|---|
| R5 | "do ensure that the base production rate (like without benefits of universities, buildings, buffs, penalties, espionage) of knowledge and civics points will be the same (since theyre population dependent)" | **Not yet satisfied by the live model.** Both rates use the same population input and exponent, but RP is per turn (ADR-030) and CP per sim-year × `dtYears` (ADR-043 §3). They are equal only in 10-year turns, and CP is 1/20 of RP in 0.5-year turns. Making them equal per turn needs a second scoped per-turn exception, which ADR-030 §3 says requires its own ADR. The conflict, options and recommendation are in [CR-026](adr/cr-026-research-and-civics-base-rate-parity.md). No content is changed until its form is ruled. |

**Design targets, not rulings.** The brief's Flight I–III proposal and its historical anchors (Wright Flyer 1903; first
jet flight 1939; HMS Argus 1918) are design targets. They inform the progression and never gate anything (Law 4).

**Not changed by this record:**
- code, content, schemas and defaults;
- TC-1, which stays PROPOSED;
- the milestone order;
- `main`, tags and releases.

## Addendum 2 (same day): binding RP/CP rule and the direction to implement

| # | Ruling (Director's words, verbatim) | Relation to earlier decisions |
|---|---|---|
| R6 | "Every Research Point (RP) and Civics Point (CP) calculation is per turn and based on average population." "This is a binding model requirement." | **Resolves CR-026.** CP becomes per turn on the same base function as RP. ADR-030's scoped per-turn exception is extended to CP by a new ADR; ADR-043 §3 is superseded in part. Law 3's text is unchanged. "Average population" is not defined elsewhere in the repository; its implementing definition is recorded in that ADR as a stated assumption. |
| R7 | The Director directs implementation of the outstanding M7 work on the feature branch ("CC Execution Prompt: M7 Progression, Aviation, Research/Civics and Charter Resolution"): the RP/CP correction, RC-1, the staged aviation progression, the family splits, permitted re-equipment, the founding warband's real personnel, and EC-1 to EC-3 as separate packets. EC-4a stays provisionally M10. Q4, Q8/B5, OD-2 and OD-3 stay open unless the sources resolve them. | Authorizes implementation on the feature branch ahead of a full M7 spec, for these items only. The feature branch is the safety boundary; merge, commit and release still need explicit authorization. RC-1 (OD-1) is treated as approved by this direction. |

## Addendum 3 (same day): unit taxonomy, abstract military support, garrisoning

**Status: RULED (Director, 2026-10-27, "Ratify Unit Taxonomy, Abstract Military Support, and Garrisoning
Architecture").** It applies to every current and future entity proposal. The detail is in the
[unit charter](m7-plan/unit-catalogue-and-progression.md) §4.5.

| # | Ruling (essentials) | Relation to earlier decisions |
|---|---|---|
| R8 | **The architecture rule.** "A thing belongs in the controllable unit tree only when instances of it have a meaningful lifecycle as independently controllable formations. Military relevance alone is not sufficient." The tests are: a meaningful map position; independent selection, command or movement; persistence as a formation across turns; its own identity, location, visibility and movement state. A justified exception is allowed only where an approved design documents it. Classify by simulation semantics, not by name or visuals. An abstract quantity can still be a first-class, deterministic, persisted value. | New. Consistent with D-043 B1 (MobileAgent examples are armies, formations and special people and groups) and D-011 (formations are atomic). |
| R9 | **Five categories.** **A** controllable formations; **B** abstract military support capabilities; **C** air defence in a mobile and a garrisoned/installed state; **D** one-shot actions; **E** implicit economic and transport systems. | Narrows D-065 R7's "family splits": combat engineers, military supply and field medical are category B, not unit families. The proposed keys 13, 15 and 16 are withdrawn and stay unallocated. |
| R10 | **Category B.** Combat engineers, military supply, field medical support, and command or other specialist support where no individual is needed, are persisted quantities or allocations consumed by the battle and war systems. The battle system owns the rules, which are defined explicitly when implemented; no invented buffs. | New. A character system may still own individual people (D-043 B3, D-047 r15–16). |
| R11 | **Category C and garrisoning.** Air defence has two permitted states: a mobile formation, and a garrisoned or installed capability at an eligible city, settlement or installation. **Garrisoning is an approved future feature requirement.** One capability must never be counted as both a mobile formation and a garrison. | New. Work item GA (unit charter §13–§14). |
| R12 | **Category D.** Prospecting is a one-shot action: one completed operation reveals one resource, subject to the existing discovery mechanics. No prospector stays as a map unit. Future task tokens are actions, not units. | Consistent with the existing Prospect order (kind 15, ADR-044). |
| R13 | **Category E.** Traders, commercial shipping, convoys, shipping capacity and trade volume stay abstract unless an approved design introduces controllable physical formations. **Naval transport is reconsidered under R8.** Military lift is an abstract capability unless transport vessels need independent movement and control. | **Pending data change:** `naval_transport` (family 10) exists in `unit-families.json` under ADR-031. Reclassifying it needs a future ADR-031 amendment and a save migration; no data is removed now. |
| R14 | **Snipers** are an approved design requirement, distinct from `sharpshooters` (407) unless an analysis justifies a merge. **Open:** independent formation, or a specialist capability attached to another formation. | New. Recorded as open question Q10 (unit charter §16.4). |

## Dated note — 2026-11-03 (D-075; appended, the text above is unchanged)

Row S37 of D-075 §2.3, and D-075 R54. R1–R4, R8's body, R9, R10, R12, R13 and R14 otherwise stand. R10 (category B as "persisted quantities or allocations") against the Director's individual entities is OPEN (D-075 O-33); R13 (naval transport) is OPEN (O-1); R14 (sniper form) is OPEN (O-32). R3 is applied through D-075 R12 ("Build generic contracts; implement only ratified era/capability scope", REV:537).

- R8, line 51 (relation column) — "D-011 (formations are atomic)" — SUPERSEDED IN PART, as D-011 §3's "atomic" (D-075 S8): atomicity no longer bars individually simulated member entities (retained: R8's body, the architecture rule; subordinate members are not new player-commandable families, D-075 R19). Replaced by D-075 R13, R15, R19; Director's revision §A, §C and §H (REV:383, :449, :538), docs/d075-m7-unified-warfare-direction.md (S37).
- R11, line 54 — "Garrisoning is an approved future feature requirement." — placement now DECIDED: Garrisoning (GA) is an M7 packet, B7-25, on M7's critical path into B7-29 (not a supersession of R11's text; GA's design remains OPEN, D-075 O-36). Decided by D-075 R53, R54; the Director's clarification of 2026-11-03, item 9, docs/d075-m7-unified-warfare-direction.md.

Recorded administratively under the Director's decisions of 2026-11-03; the historical text above is preserved.

## Dated note — 2026-11-05 (D-079; appended, the text above is unchanged)

Record: `docs/d079-m7-governance-reconciliation-2026-11-05.md` (Director decisions of 2026-11-05, cited DIR5). Each bullet quotes the superseded line, states the extent and what is retained, and cites the replacing DIR5 section and D-079 decision row (DR). Categories: DIRECTOR DECISION, ADMINISTRATIVE CONSEQUENCE, DELEGATED TECHNICAL DECISION, OPEN (needs the Director), READING (labelled interpretation).

- **S237** (`d065-m7-charter-rulings.md:54`) — "**Garrisoning is an approved future feature requirement.**" — IN PART: full garrison functionality is M7 scope (cities, forts, airports, harbours, installations); retained: category C, never counted twice. **Replaced by** DIR5 §2.3 (D-079 DR10). [DIRECTOR DECISION]
- **S238** (`d065-m7-charter-rulings.md:56`) — "**Naval transport is reconsidered under R8.** Military lift is an abstract capability unless transport vessels need independent movement and control." — IN PART: controllable transport vessels included; retained: category E commercial shipping. **Replaced by** DIR5 §2.1 (D-079 DR8). [DIRECTOR DECISION]
- **S239** (`d065-m7-charter-rulings.md:57`) — "**Open:** independent formation, or a specialist capability attached to another formation." — IN FULL: independent small specialist formations; retained: distinct from 407. **Replaced by** DIR5 §2.15 (D-079 DR33). [DIRECTOR DECISION]
- **S240** (`d065-m7-charter-rulings.md:61`) — "R14 (sniper form) is OPEN (O-32)." — IN FULL: R10 (O-33 a), R13 transport and R14 form all answered. **Replaced by** DIR5 §2.12, §2.1, §2.15 (D-079 DR8, DR29, DR33). [DIRECTOR DECISION]
