# GOV-4 — REPOSITORY FRESHNESS AND STALENESS CONTROL

**Governance record. Documentation and workflow only.** No simulation behaviour, no gameplay, no
goldens, and no architecture is changed or redesigned by this document.

**Why this exists.** An agent's context is truncated, a session ends, a new conversation starts, or a
branch is switched — and the agent resumes from a summary of what a previous agent *said* rather
than from what the repository *contains*. The failure is not that summaries are wrong; it is that a
summary and a measurement look identical once they are both prose. This record makes the repository
the mechanism for reconstructing project state, and demotes everything else to evidence that must be
checked.

**The one-line version.** The repository is the state. Reports are hearsay. Verify before you act.

**Entry point:** `docs/current-state.md`.

---

## 1. SOURCE-OF-TRUTH HIERARCHY

When two sources disagree, the higher one wins:

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

**A conflict is not yours to dissolve.** Do not silently reconcile two sources, and do not pick the
one that makes the current task easier. Identify both sources, state which is authoritative under
this ordering, and state whether the conflict needs a director ruling. A conflict between two FROZEN
items is the CR path in `CLAUDE.md`, not a judgement call.

---

## 2. COMMIT PROVENANCE

A report saying *"commit `abc123` implements X"* is not evidence that X is in the tree you are about
to edit. Before relying on it:

1. The commit exists (`git cat-file -t`).
2. It is reachable from the current branch — or you have explicitly inspected the branch it is on.
3. The actual diff says what the report says (`git show`).
4. The files it claims to add exist.
5. You know whether it is merged into `main`.
6. You know whether a later commit supersedes it.

If any step fails, the claim is **UNVERIFIED**. Say so; do not repair it by inference.

**Never collapse LOCAL / REPORTED / REMOTE / MAIN into "the repository has it."** These are four
different states, and the difference between them is exactly what a stale-context failure hides. An
unpushed local branch is not on the remote; a remote branch is not on `main`.

---

## 3. BRANCH FRESHNESS

A branch is **not** current because it has a descriptive name, has recent commits, was used in a
previous conversation, or is called active by a previous report. Age alone establishes nothing —
neither newness nor staleness.

Before working on a branch, record: name · HEAD SHA · upstream (or NONE) · merge-base with `main` ·
commits ahead · commits behind · last commit date · whether its unique commits already exist
elsewhere.

Then classify it: **ACTIVE · STALE · MERGED · SUPERSEDED · UNKNOWN.** `UNKNOWN` is a legitimate
answer and is preferable to a confident wrong one.

---

## 4. WORKING BRANCH RULE

Before implementing a packet, verify the branch you are about to modify is the intended one, and
report: current branch · HEAD · main baseline · ahead/behind · working-tree status.

If the branch does not correspond to the packet's work, **STOP before editing.** Do not create a new
branch unless the packet requires it, and never switch branches silently — if you move, say so and
say why.

---

## 5. NO AUTOMATIC CLEANUP

Claude Code must **never**, as part of ordinary implementation work: delete, rename or archive
branches · delete ADRs, D-documents or investigation records · squash or rewrite history · rebase or
merge other branches · modify goldens · modify simulation code outside the packet's scope.

Stale branches are **recorded, not cleaned.** Old branches hold the evidence of why decisions were
made, and that evidence is worth more than a tidy branch list. Deletion, renaming and archival each
need their own director instruction.

---

## 6. FROZEN DOCUMENTS

**A document can be stale without being wrong.** Some are deliberately frozen historical records —
the reasoning as it stood, which is the point of keeping them.

Never rewrite a frozen document because a later decision supersedes part of it. The chain is:

```
older document  ──(remains historical)──▶  later D-decision / ADR  ──▶  current implementation
```

If a frozen document creates a genuine ambiguity for current work, **record the ambiguity** — a
queue entry or a CR — and leave the document alone. Rewriting it destroys the provenance the freeze
was protecting.

---

## 7. MILESTONE FRESHNESS

Never determine the current milestone from a single old document. `CLAUDE.md` may carry a stale
milestone line; the Spine carries an older milestone sequence; later D-decisions may resequence
milestones; queue entries describe work that has not started.

Determine it from the latest ratified milestone architecture together with current repository state,
and read the roadmap through the latest accepted D-decisions and addenda. `docs/current-state.md` §2
records the standing example of exactly this divergence.

---

## 8. PACKET FRESHNESS

A packet should identify its milestone · packet name · governing D-decision or ADR · relevant
milestone spec · frozen contracts · exact scope · explicit exclusions.

Verify those sources still exist and still apply before implementing. **If the packet conflicts with
a later accepted repository decision, STOP and report the conflict.** A packet is an instruction, not
a fact about the tree, and executing a stale one produces confidently wrong work.

---

## 9. TEST FRESHNESS

**Test results expire when code changes.** An old green run is not evidence that current HEAD passes.

Every certification claim must state: the commit SHA tested · the branch · the exact command · the
test set · the result · and whether that SHA is still the current implementation. A result whose SHA
is no longer current is a historical record, not a certification.

The converse is also a rule: do not re-run expensive historical investigations to refresh a number
that has no bearing on the current change. Freshness is about not overclaiming, not about re-running
everything.

---

## 10. UPDATING `current-state.md`

After an accepted implementation packet, update `docs/current-state.md` with the new accepted
commit, the new current task, newly accepted decisions, newly resolved blockers, newly discovered
stale or conflicting documents, and the next queued packet.

Never update it speculatively. It represents repository truth; an intention recorded there becomes
the next session's false premise.
