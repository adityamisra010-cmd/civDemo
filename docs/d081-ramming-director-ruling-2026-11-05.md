# D-081: Ramming — once per target, run-up to ramming speed, real-world realism (Director, 2026-11-05)

**Status: RULED (Director, 2026-11-05).** Administrative record of the Director's answer to the open question raised by
Phase C lane N (`prototypes/m7-sprint1/reports/H-N.md`, open question on the ram cooldown; commit 7118ef0). Categories as in
D-076..D-080: DECIDED, ADMIN, DELEGATED, READING, OPEN.

**Source:** the Director's message of 2026-11-05 (cited **RM**). Verbatim working copy:
`/tmp/claude-0/-home-user-civDemo/54b82655-610f-500e-a185-8fae64bdd108/scratchpad/m7gov/director-answers-ram-2026-11-05.md`.

## 1. Decisions

| ID | Category | Decision | Director's words (RM) |
|---|---|---|---|
| RM1 | DECIDED | A ship rams a given enemy ship at most once. It cannot back off and ram the same ship again. | "ramming happens once. It can't back up and ram again" |
| RM2 | DECIDED | After a ram, the ramming ship may steer away and ram a different enemy ship. | "but it can steer away and go ram another ship" |
| RM3 | DECIDED | A ram requires ramming speed. The ship must build up to that speed, so it must start its run far enough from the target to reach it. | "It will need to build to speed and should be some distance away at least to build to that speed." |
| RM4 | DELEGATED (criterion set) | The ramming mechanics are delegated with a Director criterion: make them realistic, as ramming works in the real world. This covers the ramming-speed threshold, the run-up taken from the hull's own acceleration, impact damage from speed and angle, damage to the rammer, and freeing the ram after impact. Laws 1–4 still bind (conservation through ledger flows, mechanisms over modifiers, dt-correctness, no calendar gates); every coefficient stays a PLACEHOLDER with a cited rationale until tuned. | "Make it realistic like how ramming would work irl" |
| RM-R | READING | "Once" is read per pair (ramming ship, rammed ship): RM1 forbids ship A ramming ship B a second time; it does not stop a different ship C from ramming B. The run-up distance is read as following from the hull's acceleration and the ramming speed, not as a fixed number of metres. Lane N's 30 s ram cooldown is read as superseded by RM1–RM3, because the run-up is the physical mechanism that a timer only approximated. All three readings are flagged for confirmation in the next report. | — |

## 2. Superseded or narrowed

- Lane N ram rule (`H-N.md` §3 "Ram": speed ≥ max/2, ±30° bow contact, damage ∝ v/vmax, rammer stops, 30 s cooldown) and its
  measured behaviour (`H-N.md` §12, N-R2-07: a cooldown that binds only when a rammed hull sinks next to another target):
  narrowed by RM1–RM3 and RM4; the cooldown is superseded per RM-R.
- Lane N open question ("Should a rammer be able to back off and re-ram the same hull?"): answered, no (RM1).

## 3. Not decided here

- Numeric values (ramming speed per hull class, acceleration, damage coefficients) remain PLACEHOLDERS for tuning.
- Which hull classes can ram follows capability (`NavalDesign.Derive`), not era or date; no change is decided here.
