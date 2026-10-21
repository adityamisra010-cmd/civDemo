# M6 planning package — index and the M5 → M6 transition note

**Status: PLANNING INPUT for the M6 spec. Not the spec. Nothing here is ratified, allocates a packet, or authorizes
code.** Prepared 2026-10-20 against `claude/new-session-yavghm` @ `dc83a67` (the tree every citation in this folder
is pinned to) on the Director's instruction "M5 closure preparation and M6 initiation" of 2026-10-20. Author authority:
none — every allocation, scope line and recommendation below is a proposal until the Director rules and the M6 spec is
written and ratified under S8 §4 (`docs/spine-s8-governance-freeze.md:48-52`: the M(n+1) spec is written only after
M(n)'s exit gate passes; a planning document is design input, like `docs/m6-session-export/README.md`).

Labels used throughout: **RATIFIED** (a ruling or frozen document, cited) · **RECORDED** (a repository document says so)
· **MEASURED** (read from code or run) · **PROPOSED** (this package's proposal) · **INFERRED** · **DIRECTOR DECISION
REQUIRED**.

## The documents

| File | What it is | Director's required document |
|---|---|---|
| `../m5-exit-inventory.md` | M5 closure report and evidence matrix; proposed tag; the closure ceremony | 1 |
| `m6-scope-register.md` | Authoritative M6 definition, in/out of scope, additions proposed on 2026-10-20 and where each sits against the frozen allocation, milestone acceptance criteria | 2 |
| `m6-task-register.md` | Workstreams M6.1–M6.24 with stable IDs, objective, scope, reuse, ownership, prerequisites, decisions, deliverables, acceptance, tests, determinism/persistence implications, size, risks, parallelism, exit criteria | 3 |
| `m6-dependency-graph.md` | The directed dependency graph (table + Mermaid), the critical path, independent lanes, the proposed execution sequence | 4 |
| `m6-decision-register.md` | Decisions that genuinely need the Director, incl. the architecture conflicts; each with options, consequences, recommendation and what stays blocked | 5 |
| `m6-risk-register.md` | Risks with owner, trigger, mitigation | 6 |
| `m7-handoff-register.md` | What M6 must provide and what M7 must implement, item by item | 7 |
| this README | Index and the transition note | 8 |
| `deferred-work-register.md` | Every deferred item found in the record (DW-001…), with source, who deferred it, destination and whether a ruling is owed — the M6.1 deliverable's seed | (M6.1) |

## The transition note — from M5's proposed tagged baseline to M6's development baseline

1. **M5's baseline.** The closure candidate is the branch `claude/new-session-yavghm` at the commit carrying
   `docs/m5-exit-inventory.md` (code identical to `4d2de04`/`dc83a67`, whose CI and Windows smoke are green). On the
   Director's rulings (`m5-exit-inventory.md` §6) it is merged into `main` by pull request, and the Director publishes
   the Release `m5-exit` at the merge commit — the tag is minted by the publish and the win-x64 zip is attached by
   `ui-artifact.yml`. Until that ruling M5 is **NOT closed**.
2. **M6's development baseline is `main` after that merge.** CLAUDE.md's loop (`CLAUDE.md:48-50`) cuts one branch per
   packet from `main`; the long-lived `claude/new-session-yavghm` branch ends with the M5 merge. Until the merge, M6
   planning documents (this folder) live on that branch so the Director reviews one tree.
3. **What happens between the tag and the first M6 packet** (S8 §4, `spine-s8:48-52`): write `docs/m6-spec.md` from
   this package, with the four §4.1 items (FOUNDATIONS AUDIT as packet one — `T6.1`; dimensional declaration; corridor
   independence; coupling map — `spine-s8:72-188`; CLAUDE.md:29); the Director ratifies it; packets are cut. **No M6
   gameplay code before ratification.** The CLAUDE.md:10 milestone line changes at the exit gate to "M6 … spec in
   preparation" (prepared text in `m5-exit-inventory.md` §12).
4. **Authoritative M6 definition** (RATIFIED/RECORDED): `docs/milestones.md:23, 51-52, 74-88` — "the Technology and
   Civics trees; research points (RP) and progression; prerequisites; Eureka / foreign-exposure acceleration; persistent
   civilization-owned knowledge; capability acquisition; research-gated actions; realization requirements and the
   capability → realization chain; knowledge diffusion; universities and research modifiers; Age integration;
   tech-dependent economic, infrastructure and military capabilities; corpus gameplay audit; automatic propagation of
   authored consequences into legal gameplay; simulation-side rejection of illegal actions; UI exposure of legal actions;
   save/load/replay/determinism coverage; tech-dependent content and assets", plus by D-052: fog of war, scouts and
   visibility persistence (§5), the E1 visual milestone (§6), and the one-file session export (`milestones.md:40-43`).
   M7 consumes M6's technology/capability state and owns movement, Action Points and recruitment (`milestones.md:36,
   53-58`; D-052 §4); M8 owns politics, diplomacy, regime change (`milestones.md:25, 59-61`; Spine `:134`).
5. **What the 2026-10-20 instruction adds that the record does not already allocate to M6** — each is a decision, not
   a given (see `m6-decision-register.md`): government ADOPTION and active-government effects (the Spine places regime
   change at M8); per-unit progression STATE, promotion trees and repeatable training (units and Action Points are M7's
   by D-052 §4 and D-011; nothing earns experience before M7); Settler/Hero progression (no such unit objects exist).
   The package plans the Director's items in a way that respects the frozen order — content, contracts and placeholders
   in M6; state and resolution in M7/M8 — and names exactly where a different choice needs a priced override
   (`spine-s8:37`).
6. **Nothing in this package changes the frozen order, any D-decision, any ADR, a golden hash or a schema.** Where it
   found a contradiction it recorded it (`m6-decision-register.md` D-C…, `m5-exit-inventory.md` §10) and stopped.


## Dated note — 2026-10-21 (M6.1; appended, the text above is unchanged)

- `docs/m6-spec.md` is DRAFTED (NOT ratified) with the four S8 §4.1 items; `docs/m6-foundations-audit.md` is the T6.1
  table (MEASURED on `a0b3b25`); `docs/d061-civics-points-supersession.md` (PROPOSED) and `docs/adr/cr-023-…md` (RAISED)
  carry the Civics Points direction of the Director's 2026-10-21 instruction, which conflicts with D-I as ruled on
  2026-10-20. **This package is superseded by the spec on the spec's ratification; until then both are planning input.**
  Where this package says "one shared RP pool" (S-03, §4 row 1, §5 bullet 5, §7 criterion 2, M6.4) read D-061.
- The transition note's item 2 ("the long-lived branch ends with the M5 merge") is overtaken: M6.1's documentation
  continues on `claude/new-session-yavghm` (= `main` + docs commits) by the session's branch assignment; packets cut
  from `main` after ratification per CLAUDE.md.
- Review package: `m6.1-review-package.md`.
