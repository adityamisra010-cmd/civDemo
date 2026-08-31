# CURRENT STATE — the routing document

**Read this first, then verify it.** This file exists so an agent entering the repository with zero
conversation context can work out what to read next. It is a ROUTER and a STATUS BOARD. It is not
the Spine, not a milestone spec, not a D-decision, not an ADR, and it never restates one — where a
fact belongs to another document, this file names the document and stops.

**It is also not evidence.** Every claim below carries its provenance. Before you rely on any of
them, re-derive it from git or from the named document (§9). A value that could not be verified from
the repository is written `UNVERIFIED` with the check that would settle it.

**Measured 2026-08-31** in worktree `wt-food` against `food-anomaly-observability` at `8bd433a`,
with `origin/main` at `070f05b` as fetched. Every git figure below was read from `git rev-list`,
`git branch` or `git log` at that moment, not recalled from a session.

---

## 1. WHERE THE PROJECT IS

| | |
| --- | --- |
| **Current milestone** | **M4** — see §2 for why this contradicts `CLAUDE.md` |
| **Current objective** | M4 "Empire Control Foundation" — the structural minimum for the ratified Empire model |
| **Governing architecture** | `docs/d042-empire-and-player-control-addendum.md` (D-042) |
| **Milestone spec** | `docs/m4-spec.md` (R-1, R-2, R-3 ruled 2026-08-07; packet list FINAL) |
| **Authoritative baseline** | `origin/main` = `070f05b` |
| **Active implementation branch** | `food-anomaly-observability` = `8bd433a` — **LOCAL ONLY, see §3** |
| **Latest implementation commit** | `8bd433a` — M4 Empire control foundation, schema v22 |

**Documents required before touching current work:** `CLAUDE.md` · `docs/m4-spec.md` ·
`docs/d042-empire-and-player-control-addendum.md` · `docs/spine-s8-governance-freeze.md` ·
`docs/civ-sim-architecture-v3-outline.md` · `docs/adr/adr-015-verification-hygiene.md` ·
`docs/gov-3-execution-protocol.md` · `docs/gov-4-repository-freshness.md` · `docs/queue.md`.

---

## 2. THE MILESTONE CONTRADICTION, STATED RATHER THAN RESOLVED

`CLAUDE.md:10` reads **"Current milestone: M3 — active packets: `docs/m3-spec.md` §4"**, and adds a
director amendment about the unmerged art-substrate branch.

The repository disagrees with that line. `docs/m4-spec.md` exists on `origin/main` and declares its
packet list FINAL under a dated director ruling; the merged history on `origin/main` carries T4.3,
T4.4, T4.8, T4.14 and T4.16 merges described as director-certified; and the branch list is dominated
by `t4.*` packet branches.

**The line has not been changed.** It is the director's — `CLAUDE.md` itself says it "changes only
at a milestone exit gate", and no exit-gate ruling for M3→M4 was found in the tree
(`UNVERIFIED`: an explicit M3 exit-gate record would settle it; searching `docs/` for one returns
`docs/m3-exit-session.md`, which records the session but was not confirmed to be the gate ruling).
Treat M4 as current per §5's hierarchy — the later milestone spec and the implementation both
outrank an older line in `CLAUDE.md` — and treat the `CLAUDE.md` line as a known stale entry
awaiting a director edit.

---

## 3. BRANCH AND PUBLICATION STATE — LOCAL, REMOTE AND MAIN ARE DIFFERENT THINGS

Three facts here are load-bearing, and collapsing any of them into "the repository has it" will
mislead the next agent.

1. **Local `main` is stale.** Local `main` = `87fb866`, `origin/main` = `070f05b`: **0 ahead, 8
   behind**. The 8 are the T4.4 colonization merge and the PR#4 AI-constitution admission.
2. **The active branch has no remote.** `food-anomaly-observability` exists **locally only** — no
   upstream, never pushed. It is **17 ahead / 8 behind `origin/main`**.
3. **It was cut from the stale local `main`,** so everything on it — the food-anomaly investigation,
   the capacity-floor fix, the M5 design records, CR-007, D-042 and the M4 foundation — sits on a
   base that predates T4.4. None of it is on `origin/main`.

Anything on that branch is therefore **LOCAL**, not **REMOTE**, and not **MAIN**. Say which one you
mean, every time.

The full branch classification (64 remote branches: 49 MERGED, 15 unmerged) is recorded in the
session report for this packet rather than duplicated here, because it goes stale the moment anyone
pushes. **Re-derive it** with `git branch -r --no-merged origin/main` and the ahead/behind counts —
that command is the record, this file is not.

Two branch facts worth carrying anyway, because they are easy to miss:

- **`adr-019-architecture-addendum`** (`a36b94d`, 1 ahead / 63 behind) holds the **only copy of
  ADR-019**, which is absent from `origin/main`. Do not treat the ADR sequence on main as complete.
- **`claude/civdemo-work-b1z2y4`** exists but is 0 ahead / 63 behind and unrelated to current work.

---

## 4. STATUS OF OPEN THREADS

**Food-anomaly certification: PAUSED, NOT COMPLETE.** The investigation concluded the reported
symptom is not reproducible (0/40 seeds) and that the real defect was a granary capacity floor. The
director ruled ACCEPT on the capacity-floor fix and approved the resulting `DrivenGolden` repin
(`24d107f`). The final full-suite regression run before merge was interrupted and **has not been
re-run**. The fix and its 7-test regression suite are on the active branch; **nothing is merged.**

**M4 Empire Control Foundation: implemented, blocked at the golden boundary.** `8bd433a` adds
`PolityRow`, `CapitalRow`, `CommandSource` and `EmpireQuery`, and moves `CanonicalSchema` to v22.
Two new empty count prefixes move all four pinned world hashes. That is a schema-only change, not a
behavioural one, so **the goldens were deliberately not repinned** — the pin change needs a director
ruling, on the T4.3/T4.8 precedent.

**Open change requests awaiting director ruling:** CR-005 (M5 ownership of Research/Technology/
Institutions) · CR-006 (temporal control and epoch) · CR-007 (B3 exemplar — headline withdrawn by
the author; the record stands) · CR-008 (money owner) · CR-009 (era gates) · CR-010 (institution
definition) · D-042 §14.1 (D-018 income-column staleness).

**Quarantined / manually-run tests:** six `Skip =` entries in `Sim.Tests`. Two are quarantined
pending M4 migration work (T4.1b/ADR-018 §11 — asserts on gross migration, the wrong observable);
four are expensive measurement rigs kept off the default path by design, each naming the review
record that holds its numbers. The historical food-investigation harnesses were removed from the
default test path in `ca0aef0` and must not be re-run casually — see `docs/gov-4-repository-freshness.md` §9.

---

## 5. SOURCE-OF-TRUTH HIERARCHY

When two sources disagree, the higher one wins. **Do not silently reconcile them** — name both
sources, name which is authoritative, and say whether the conflict needs a director ruling.

1. Explicit director rulings in the current task or session
2. Later accepted D-numbered decisions and architecture addenda
3. Accepted ADRs
4. The current milestone specification
5. The current repository implementation
6. Tests and certification records — for behavioural truth specifically
7. Older architecture documents
8. Previous Claude/agent reports
9. Conversation memory
10. Agent inference

Note what this ordering does to a common mistake: a previous agent's report (8) loses to the code
(5), and agent inference (10) loses to everything. §2 above is an application of the rule, not an
exception to it.

---

## 6. KNOWN STALE AND FROZEN DOCUMENTS

**Stale — describes a state the repository has moved past:**

- `CLAUDE.md:10` — the M3 milestone line and its art-substrate amendment (§2).
- `docs/handoff-status.md` — a previous, measured attempt at this same routing role, dated
  2026-07-28 against `t3.4c-variance-fix` at `719152a`. Its branch table, suite counts and milestone
  are all from M3. **It is superseded by this file for routing purposes and left untouched as a
  historical measurement.**

**Frozen — stale in places but deliberately not rewritten:** the Spine
(`docs/civ-sim-architecture-v3-outline.md`), `docs/spine-s8-governance-freeze.md`, closed
D-decisions, accepted ADRs, and every investigation and review record. A later decision superseding
part of one of these does **not** license editing it. If a frozen document creates a real ambiguity
for current work, record the ambiguity (queue entry or CR) — do not resolve it by editing.

---

## 7. QUEUED AND NEXT

`docs/queue.md` is the queue; it is not summarized here. The immediate decisions the tree is waiting
on are the CRs in §4 and the golden-repin ruling for `8bd433a`.

---

## 8. UPDATE RULE

Update this file **after an accepted implementation packet**, and only with what the repository can
show: the new accepted commit, the new current task, newly accepted decisions, newly resolved
blockers, newly discovered stale or conflicting documents, and the next queued packet.

Never update it speculatively, and never to record an intention. It represents repository truth, and
a forecast written here becomes next session's false premise.

---

## 9. PROVENANCE OF EVERYTHING ABOVE

Read from git in worktree `wt-food` on 2026-08-31: branch tips, ahead/behind counts, merged/unmerged
partitions, the local-vs-`origin/main` divergence, and commit subjects. Read from the working tree:
`CLAUDE.md:10`, `docs/m4-spec.md`, `docs/handoff-status.md`, `docs/queue.md`, the `Skip =`
attributes in `Sim.Tests`, and the presence or absence of documents on `origin/main`.

Carried from the session that produced this file, and therefore **secondary evidence** under §5:
the director's ACCEPT ruling on the capacity-floor fix, the paused state of its certification, and
the list of open CRs. Each is verifiable — the CRs as files under `docs/adr/`, the ruling and pause
from the session record — but none was re-derived from git for this file.

`UNVERIFIED` values are marked inline. There is one: the M3→M4 exit-gate ruling (§2).
