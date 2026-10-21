# CR-023 — Contradiction report: the 2026-10-21 Civics Points direction vs the ratified single-RP-pool rulings and the 2026-10-20 D-I ruling

**Status: RAISED 2026-10-21 (M6.1). Awaiting the Director's ruling. No code changed.** Format per CLAUDE.md
"Governance": (1) items in conflict, (2) evidence, (3) ≤3 minimal fix options, (4) blast radius, (5) recommendation.
Tree: `claude/new-session-yavghm` @ `a0b3b25`. The proposed resolution record is `docs/d061-civics-points-supersession.md`.

## 1. Items in conflict

- **A. The ratified single-pool rulings** — D-044 R2 (`d044:82-103`), D-044 R12 (`d044:363-380`), D-046 G5
  (`d046:202-204`), ledger §26 items 2–4 (`director-architecture-and-decision-ledger.md:1109-1121`), ADR-030 §3
  (`adr-030:37`), and the D-044 Part B ★ "do not ask again" marking (`d044:782-791`).
- **B. The 2026-10-20 closure ruling D-I** (`m6-decision-register.md:230-234`; `m5-exit-inventory.md` §13): "one shared
  Research Points pool and one target … No separate Civics Points currency."
- **C. The 2026-10-21 Director direction** (M6.1 instruction §3): "Civics uses a separate Civics Points pool, distinct
  from Research Points. The Civics progression graph is a genuinely distinct tree …"
- **D. Planning documents written under A/B** — `m6-scope-register.md` S-03, §4 row 1, §5 bullet 5, §7 criterion 2;
  `m6-task-register.md` M6.4 ("on the same RP"); `m6-decision-register.md` D-I options and recommendation;
  `m6-dependency-graph.md` M6.4 edge text.
- **E. Code** — `ResearchEngineTests.SharedPool_OneTargetAcrossBothTrees_TotalInvestedEqualsTotalRp` and the engine it
  pins (file:line in `docs/m6-foundations-audit.md`): the 7 civics nodes are funded from the one RP pool today.

A, B and E agree with each other and contradict C. D restates A/B and so also contradicts C.

## 2. Evidence

The passages are quoted verbatim with line numbers in `docs/d061-civics-points-supersession.md` §2 (S-1…S-12). The
chronology (D-044/D-046 → the 2026-10-20 instruction asking for Civics Points → the 2026-10-20 D-I ruling keeping one
pool → the 2026-10-21 direction for Civics Points) is stated in D-061 §2. None of A is a *frozen Spine* item under S8
§1; they are Director rulings and a ratified ledger, which the Director may supersede by a dated record. ADR-030 §3 is
not contradicted by C: it requires that a second per-turn quantity has its own ADR, which D-061 proposes (ADR-043).

## 3. Minimal fix options (≤3)

1. **Supersede A and B for Civics only, by D-061 as drafted (recommended).** Two currencies, two trees, two targets;
   Technology unchanged; CP per-turn by its own ADR-043; the one-pool test replaced by two conservation pins in the
   packet that lands CP; the planning documents get dated notes. Cost: the architecture and migration items in D-061
   §4–§5; the eight sub-decisions D-061.1–8.
2. **Keep A/B; realize the Director's intent as "one pool, two targets with a player-set split".** Supersedes only
   R9 (one target). Satisfies "distinct tree" and "separate targeting" but not "a separate Civics Points pool, distinct
   from Research Points" — it is a split of RP, which the 2026-10-20 instruction explicitly called "combining the
   currencies". Cost: smaller (no second per-turn exception, no second pool row). Does not meet C as worded.
3. **Keep A/B and defer C to a later milestone.** Not consistent with the Director's statement that C "is the intended
   direction". Listed for completeness only.

## 4. Blast radius (of option 1)

- **Rulings/docs:** D-044 R2/R12 (superseded for Civics), D-046 G5 (same), ledger §26 items 2–3 (same), D-I (re-ruled),
  the five planning documents (dated notes), `m5-exit-inventory.md` §13 (dated note). Nothing frozen under S8 §1 moves.
- **Code (later packets, not now):** a parameterized research engine; a CP pool row, civics target and civics progress
  rows (schema bump); `SetCivicsTarget` order kind; AI civics goal; civics UI; migration of 7 nodes' progress; the
  one-pool test superseded; goldens re-pinned with attribution; a CP-rate calibration measurement.
- **Risks (register):** R-02 (second engine) rises unless the one-engine rule is held; R-05 (schema sequencing) and
  R-13 (self-referential knowledge corridor) are engaged; R-04 (determinism) for any CP-dependent ordering.

## 5. Recommendation

Option 1. Ratify D-061.1 now so the spec can be written against the intended direction; rule D-061.2–8 with the spec
(they are design rulings, not blockers for M6.2, M6.13, M6.14, M6.15). Keep every superseded passage unedited.
