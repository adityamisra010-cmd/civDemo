# ADR-030 — PER-TURN RESEARCH POINTS: A SCOPED EXCEPTION TO LAW 3

**Status:** RATIFIED — Director ruling (research corpus finalization, ruling 2).

**Excepts:** CLAUDE.md law 3, *"dt-correctness: every rate is per-sim-year; integrate with `dtYears`. Never hardcode per-turn amounts."* — for research only, as scoped in §2. Law 3 is **not** amended. Its text is unchanged and binds every other system.

**Supersedes in part:** ADR-029 §6, the line *"The rate is CLP per sim-year, integrated over `dtYears` (law 3)"*, and implementer resolution R-4.

**Relates to:** D-044 R2 (one shared research pool), D-044 R9 (one active target), ADR-029 §5 (step semantics).

---

## §1 — RULING

Research Points (RP) are generated and consumed **per strategic turn**.

```
RP per turn = coefficient × population^exponent
              anchors: 100 → 2 RP/turn, 1,000 → 10 RP/turn
              ⇒ exponent = log10(5) ≈ 0.699, coefficient = 0.08
```

The rate is **not** multiplied by `dtYears`. The function is sublinear, as architecture §8.1.2 requires. Coefficient and exponent are calibration values and remain tunable; the **per-turn interpretation** is the ruling.

## §2 — SCOPE (exhaustive)

The exception applies to exactly three things:

1. **RP generation** — the per-turn RP a polity produces.
2. **Research allocation** — directing that turn's RP to the active target.
3. **Research progress** — the per-node accumulated progress, and overflow into the next target.

It does **not** apply to anything else, including systems that read research state. Demography, production, consumption, trade, migration, construction and every other rate stay per-sim-year under law 3.

## §3 — WHAT THIS RECORD DOES NOT CREATE

- **No reusable exception mechanism.** No attribute, flag, base class, configuration switch or helper may mark another system as "per-turn". A second per-turn quantity needs its own ADR.
- **No change to law 3's text**, and no change to any other law.
- **No change to Eureka or foreign-exposure credits.** These are progress credited on an event, not rates; they were never dt-integrated and are unaffected.

## §4 — CONSEQUENCES (stated, accepted)

- **Research per sim-year varies with dt.** With equal population, a polity in a 0.5-year band researches 20× more per sim-year than one in a 10-year band. Accepted: research pacing is a game-turn quantity.
- **An era-band change alters research per sim-year at a stroke.** Accepted for the same reason.
- **The Age-to-dt mapping (architecture §19.7) becomes decisive for calendar pacing**, because research no longer scales with calendar time.
- **Goldens move.** The four world goldens and the research pins must be re-pinned, once, itemised, with the measured cause.
- **ADR-029 §1's measured pacing table is obsolete** and must be re-measured.

## §5 — ENFORCEMENT

- The research step accrues RP without `dtYears`. That one code site carries a comment citing ADR-030 §2.
- A test asserts that RP accrual is **identical across different `dtYears`** at equal population. This proves the exception, and only research, is per-turn.
- Any law-3 conformance check excludes exactly that site and nothing else.

## §6 — WHAT THIS DOES NOT DECIDE

The military-population weight in the RP input (a calibration parameter, inert until soldier accounting exists), the reference populations per Age, and any non-population input to RP (architecture §19.5).

---

## §7 — CITATION VERIFICATION AND THE CODE SITE (appended by the implementer at commit; §1–§6 above are the draft verbatim)

**Citations, checked against the tree on `research-progression-foundation`:**

| Cited | Found | Verdict |
|---|---|---|
| CLAUDE.md law 3 | `CLAUDE.md` "Non-negotiable laws", item 3: *"dt-correctness: every rate is per-sim-year; integrate with `dtYears`. Never hardcode per-turn amounts."* | Quoted verbatim. Unchanged by this record |
| ADR-029 §6, the line *"The rate is CLP per sim-year, integrated over `dtYears` (law 3)"* | ADR-029 §6 as first written (`ced5009`), under "Dimensions". The D-045 calibration then revised §6 (`229002d`) to `credited per step = RP(P) × dtYears / rpReferenceTurnYears`, leaving the per-turn question OPEN as CR-018. Both are the per-sim-year reading this record supersedes | Correct, with that history |
| ADR-029 §13 R-4 | `CLP = coefficient × adults^exponent` (ced5009), already struck through and superseded by D-045 §2 (RP(P) through the anchors over total population). This record supersedes the dt-integration only; the anchors and total population stand | Correct |
| D-044 R2, R9; ADR-029 §5 | R2: one shared pool for both trees. R9: one active target. ADR-029 §5: the step semantics | Correct |
| Architecture §8.1.2, §19.5, §19.7 | `docs/design/civilization-progression-architecture.md`: §8.1.2 forbids a linear population pool; §19 item 5 is the pool's non-population inputs; §19 item 7 is the Age → dt mapping | Correct |

**The one code site (§5).** `Sim.Core/Systems/Research/ResearchSystem.cs`, step 3 ("Throughput to the one active
target"): the polity's `ResearchQuery.ResearchPointPool` for the turn goes to the target with no `dtYears` factor, under
a comment citing ADR-030 §2. `ResearchQuery.ResearchPoints(tuning, population)` is the RP function itself
(coefficient × population^exponent), not a per-turn mechanism, and no attribute, flag, base class or switch marks
anything as per-turn (§3).

**The enforcing test (§5).** `ResearchEngineTests.ResearchPoints_ArePerTurn_IdenticalAcrossDifferentDtYears_TheAdr030Exception`:
the same world stepped under dt 10, 5, 1 and 0.5 credits the identical RP. The anchors are pinned by
`ResearchPoints_TheAnchors_Population100Gives2_And1000Gives10_ToTolerance` and sublinearity by `ResearchPoints_AreMonotone_AndSublinear_MoreResearchButLessPerPerson`.

**Law-3 conformance check.** No automated law-3 conformance check exists in the repository (the banned-constructs and
read-isolation scripts do not inspect dt). There is therefore nothing to exclude the site from; if one is written, it
excludes exactly the site above (§5).

**Overflow (§2.3).** §2 scopes "overflow into the next target" to per-turn semantics; it does not rule that overflow
carries. The engine keeps ADR-029 §13 R-2 — RP past a node's remaining cost, and RP with no target, is not stored — and
that question stays a Director decision (ADR-029 addendum A).
