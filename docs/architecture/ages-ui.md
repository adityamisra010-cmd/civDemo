# AGES — UI FOUNDATION AND PLACEHOLDER DATA STRUCTURE

**Status:** foundation on the post-M4 working branch (see [`the-trees-ui.md`](the-trees-ui.md) for
provenance, the read-only rules and the file map). **No Age exists in the simulation**, and
nothing here advances one. Ages, milestones, dates, Δt and catch-up are placeholders that the
Director fills in.

---

## §1 The concept, as built

Ages are a **separate historical framework around the Trees**, not an eighth lens. The Age view is
its own tab, and the Trees header and navigation carry an Age context strip.

- **Forward only.** A civilization's Age never regresses. The view enforces it as a *display*
  invariant. `AgeForwardGuard` keeps showing the later Age if a source ever reports an earlier
  one, and prints a warning. The placeholder state script is rejected at load if its steps go
  backward. Both are tested.
- **Capability can still degrade.** The Trees state `degraded` shows capability loss on a node,
  independent of the Age (task §3).
- **Nothing is computed.** Age progress, milestone completion and the transition are
  **reported** by an `IAgeStateSource`. The view prints "as reported by \<source\> — the
  completion rule is a Director decision (not computed here)".

## §2 The Age view

**Left: the Age track.**

- Every Age, in order, on a vertical timeline, each drawn with its icon (Star + Hourglass).
- **Passed** Ages show a full, complete ring. The **current** Age shows an arc and fill at its
  reported progress. The **next** Age, when a transition is pending, shows a finely dashed ring
  with a pulse. **Future** Ages show a dashed ring.
- Each row reads "passed", "current · 72 %", "next · transition pending" or "future", and is
  clickable to view that Age.

**Centre: the viewed Age** (the current Age by default).

- Its name, a CURRENT / PASSED / FUTURE tag and a PLACEHOLDER AGE tag.
- Its short description.
- **Progress.** A bar with the reported percentage and the "not computed here" note.
- **Transition status.**
  - pending: "TRANSITION PENDING → AGE 04", with a sweep animation;
  - not pending: "Next transition: Age 04 — not pending";
  - past and future Ages each have their own line.
- **Previous / next** buttons and a *current Age* button.
- **The milestone checklist**, split into **Mandatory** and **Supporting**, each with a
  met / total count.
  - Each row has a check mark: ✓ complete, ~ partial, empty not started.
  - Each row shows the name, the category tag (asterisked as provisional), a progress bar and a
    percentage.
  - A second line shows prerequisites with their status, the reported evidence, or the
    description.

**Right: detail.**

- **A selected milestone:**
  - glyph (Lozenge: available, in progress or complete);
  - mandatory / supporting and category;
  - the PLACEHOLDER tag and completion with progress;
  - description;
  - prerequisites (clickable);
  - evidence: the source's report, then the content's evidence notes;
  - historical source;
  - **In the Trees:** its node references, which jump to the node in the graph.
- **Otherwise, the Age's metadata:**
  - date range (display only, never a gate);
  - visual theme, icon and transition effect;
  - **Δt (placeholder)**;
  - **Catch-up (placeholder)**;
  - historical context;
  - milestone counts.

It reproduces the task's §19 example: Age 03, progress 72 %, ✓ ✓ ~ and empty marks, and "Next
transition: AGE 04". See `trees-preview/07-ages-current.png`, `08-ages-transition-pending.png` and
`09-ages-new-age.png`.

## §3 Milestone visualization and partial progression

| completion (reported) | check mark | bar | milestone glyph |
| --- | --- | --- | --- |
| not started | empty box | 0 % | Available ring |
| partial | tilde | reported fraction, gold | InProgress arc + fill |
| complete | filled box with a tick | 100 %, verdigris | Complete |

Partial progress exists at two levels, and both are shown exactly as reported:

- per milestone (`MilestoneStatus.Progress`);
- per Age (`AgeStateSnapshot.CurrentProgress`).

The view counts met mandatory milestones ("2/4"), which is a count, not a rule. It never turns a
count into an Age percentage.

## §4 Transition visualization

"Current Age → transition indication → new Age → settled" (task §13):

1. **Transition indication.** When the source reports `Transition.Pending`:
   - the banner reads TRANSITION PENDING → AGE nn and a sweep crosses it (`age-transition`);
   - the next Age in the track pulses;
   - the header shows "→ AGE nn".
2. **New Age.** When the reported current Age moves forward, the new Age's icon flashes once
   (`age-entered`). This happens in the header, the track and the Trees Age context.
3. **Settled.** After the flash nothing moves.

The durations live in `animations.json`. The animation never decides that a transition happened:
it follows the report. See [`the-trees-ui.md`](the-trees-ui.md) §9.

## §5 The placeholder data structure

**Content** lives in `ages.json`; the full field reference is in
[`the-trees-content-schema.md`](the-trees-content-schema.md) §3.

| task §3 field | where |
| --- | --- |
| age id | `ages[].id` (`AGE_00`…) |
| display name | `ages[].displayName` ("Age 00"…) |
| short description | `ages[].shortDescription` |
| ordering | `ages[].order` (unique, validated) |
| historical date / range metadata | `ages[].dateRange` (labels, optional years, note; display only) |
| milestone references | `ages[].milestones` (ordered ids; each milestone's `age` must agree) |
| progress percentage | **runtime**: `IAgeStateSource.Current.CurrentProgress` |
| completed / partial / transition state | **runtime**: standing (passed / current / future) from the current Age; `Transition`; milestone completion |
| visual theme | `ages[].visualTheme` (register, accent token, motif) |
| visual icon | `ages[].icon` (glyph base + mark) |
| transition effects placeholder | `ages[].transitionEffect` (animation id in `animations.json` + note) |
| Δt placeholder | `ages[].deltaT` (`yearsPerTurn` null, note) |
| catch-up metadata placeholder | `ages[].catchUp` (`pathwayBased`, note) |

**Milestones** carry these fields (task §4):

- `id`, `age`, `name`, `description`;
- `category`, one of `milestoneCategories[]`: technological, material-economic,
  institutional-social or systemic, all flagged `provisional`;
- `mandatory`;
- `prerequisites`, other milestones, with cycles rejected;
- `evidenceNotes`;
- `historicalSource`;
- `nodeRefs`, links into the Trees graph, validated against it;
- `placeholder`.

Completion, progress and reported evidence are **runtime**, from `IAgeStateSource`. The "visual
state" is derived from completion by the view.

**Runtime interface.** `IAgeStateSource.Current` returns an immutable `AgeStateSnapshot`
containing the sequence, source label, placeholder flag, current Age id, reported progress,
transition status and the milestone statuses. The placeholder implementation is `DemoStateSource`
(`demo-state.json`, four steps):

1. Age 03 at 72 %.
2. A milestone met, 86 %.
3. A transition pending.
4. Age 04 settled.

## §6 Explicit TBD areas

| TBD | slot | Director decision |
| --- | --- | --- |
| the real Age list, names, dates and historical context | `ages.json` | DD-T6 / DD-04 |
| milestone definitions, categories (provisional), mandatory flags and sources | `ages.json` `milestones` | DD-T6 |
| the Age completion rule (which milestones, partial credit) and what progress % means | `IAgeStateSource.CurrentProgress` | DD-T6 / DD-04 |
| Age advancement itself, including when a transition is pending or done | `AgeStateSnapshot.Transition` | DD-04 |
| Δt per Age and the global player-cycle boundary rule | `ages[].deltaT` | DD-T7 / DD-02 |
| catch-up: pathway-based (contact, exposure, absorptive capacity), never a universal behind-the-leader modifier | `ages[].catchUp` | DD-T7 / DD-04 |
| the Age-to-visual-register binding | `ages[].visualTheme.register` | DD-T8 |

None of these is implemented. The screen says so at each place.

## §7 D-043 status note (2026-09-29, append-only; nothing above is rewritten)

The Director's rulings of 2026-09-29 (`docs/d043-civilization-progression-ages-and-mobile-agents.md`,
PART A) bear on this document as follows. **No table row, content file, code or test was changed by
this note.**

**Consistent with the ruling:**
- §1 "Forward only" matches the ruled "Age progression is irreversible".
- §1 "Capability can still degrade" matches the ruled "Permanent Civilization Development":
  earlier capabilities persist unless they actually degrade.
- `dateRange` is "display only, never a gate". That fits Ages that are "not date brackets",
  provided it stays historical reference text.

**Diverging from the ruling:**
- **The Ages.** §5 has six placeholder Ages; the ruling has exactly nine named Ages, A1–A9
  (D-043 F25).
- **"Core" and "Mandatory".** The ruling says "Core". The checklist's "Mandatory" means the same
  and differs only in name.
- **Categories.** The ruling categorizes only Supporting milestones, with alternatives inside each
  category and coverage required across several. §5's four provisional categories apply to every
  milestone, and the schema cannot express sufficiency, coverage or alternatives (D-043 F27).
- **The demo transition.** At step 2 the transition is pending with the only supporting milestone
  at 0.6 (MEASURED; D-043 F27).
- **The Age view.** §2 lacks three of the things the UI must "eventually" expose: category
  coverage, alternative pathways and remaining requirements (D-043 F28).
- **The progress percentage.** §3's per-Age % has no effect, so the ruling does not forbid it
  ("70% completion does not mean 70% of an Age bonus"). What it should mean remains DD-T6
  (D-043 F29).
- **`transitionEffect`.** It is a presentation animation, not the ruled Age Transition Surge
  (D-043 F30).

**Two Director entries for this view's TBD areas**, recorded in D-043 and in `docs/queue.md`
rather than as new rows in §6:
- **DD-13**, generic current-Age effects: DEFERRED · DIRECTOR DECISION REQUIRED.
- **DD-14**, the Age Transition Surge model: DEFERRED · DIRECTOR DECISION REQUIRED.

**Still open:** whether an Age may be READ by mechanisms, which is CR-017 §1.
