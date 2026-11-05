# D-080: War Pulse answers — boundary orders, era length, observers, per-pulse economics (Director, 2026-11-05)

**Status: RULED (Director, 2026-11-05).** Administrative record of the Director's answers to the four questions raised by
Phase C lane T2 (`prototypes/m7-sprint1/reports/G-war-pulse-t2.md`, commit f58d45b). Categories as in D-076..D-079:
DECIDED, ADMIN, DELEGATED, READING, OPEN.

**Source:** the Director's message of 2026-11-05 (cited **WA**). Verbatim working copy:
`/tmp/claude-0/-home-user-civDemo/54b82655-610f-500e-a185-8fae64bdd108/scratchpad/m7gov/director-answers-t2-2026-11-05.md`.

## 1. Decisions

| ID | Category | Decision | Director's words (WA) |
|---|---|---|---|
| WA1 | DECIDED | An order issued exactly at a War Pulse or campaign-turn boundary instant applies to the pulse or turn that starts at that instant. It does not wait for the next one. This confirms the rule T2 implemented and pinned (boundary instant and ±1 ms). | "No waiting, applies as now." |
| WA2 | DECIDED | A War Pulse that crosses an era change keeps its starting era's pulse length to its end. This confirms ADR-061 §3.11; no rescaling of the remainder. | "It keeps the starting era's length." |
| WA2-F | ADMIN (fact checked) | The Director asked whether the era only shifts at turn end. It does: era-pacing bands must end exactly on turn boundaries (`Sim.Core/Kernel/EraTableLoader.cs:109`, ADR-002), and the turn length is keyed on the turn-start date. There is no mid-turn era change, so a pulse can cross an era change only where it also crosses a turn boundary. | "Era is only shifted at turn end right? there's no mid turn era change." |
| WA3 | DECIDED | Observers of a war who are not participants may see **battle results** as a side-panel notification, opened on click. | "Outside observers can see battle results as a side panel notification, if they click it" |
| WA3-D | DELEGATED (design) | A notification system modelled on the Director's reference (Civilization VI: events shown as hoverable icons at the lower right) is invited as an add-on. Its design is delegated; it is presentation only, reads fog-filtered published state, and never writes simulation state. | "civ 6 has an amazing notifications system … look it up, that might be a good add on" |
| WA3-R | READING | "Outside observers" is read as polities that can observe the war under fog (ADR-061 §3.11 locality; T2 `WarFog`), not every polity in the world. War economics are not granted to observers (WA3 names battle results only). Both readings are flagged for confirmation in the next report. | — |
| WA4 | DELEGATED (criterion set) | Which economic systems settle per pulse, and what happens when a polity cannot pay, are delegated with a Director criterion: resolve them as realistically as possible, as they would unfold in the real world. This fills ADR-061 §3.11 "[DELEG pending] which systems settle per pulse". Laws 1–4 still bind (Ledger conservation, mechanisms over modifiers, dt-correctness, no calendar gates). | "Resolve this as realistically as possible like how it would unfold in the real world." |

## 2. Superseded or narrowed

- ADR-061 §3.11 "[DELEG pending] Which systems settle per pulse": now delegated with the WA4 realism criterion (not superseded; narrowed).
- T2 report questions 1–4 (`G-war-pulse-t2.md`): answered by WA1, WA2, WA3 and WA4. T2 question 5 (which systems settle per pulse) is the same as WA4.

## 3. Not decided here

- War Pulse durations per era (D-079 Q1) remain a proposal awaiting the Director.
- The significant-event list (D-079 Q2) remains a proposal awaiting the Director.
