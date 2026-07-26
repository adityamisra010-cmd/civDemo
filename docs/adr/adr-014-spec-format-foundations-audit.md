# ADR-014 — Spec format: foundations audit, dimensional declaration, corridor independence, coupling map

**Status:** ACCEPTED — director ruling 2026-07-26
**Amends:** `docs/spine-s8-governance-freeze.md` §4 (new §4.1)
**Effective:** the M4 spec onward. M3 is mid-flight and is NOT retro-fitted — recorded in S8 §4.1
itself, not only here, because S8 is the document CLAUDE.md points agents at.
**Origin:** the T3.2b detour (CR-002, CR-003)

---

## 1. Why this exists as an ADR at all

S8 is a FROZEN document (§1: "Spine S1–S5 and this S8 … frozen at M0 exit"), and §2 prices the
director override at "a written ADR stating what breaks, which tests and docs change, and the
schedule price." This is that ADR. A freeze whose own amendment procedure is skipped when amending
the freeze document is theatre; recording it costs one file and makes the protocol credible against
itself.

Note this is an *addition* to the documentation cadence, not a reversal of any frozen commitment.
Nothing in §1–§3 or §5 changes, the milestone ladder is untouched, and no existing spec is
invalidated.

## 2. The ruling

Every milestone spec from M4 onward carries four things in addition to its packets:

1. **Foundations audit as packet one** — an explicit pass over the existing constants the
   milestone's new systems will depend on: what each means in real units, whether its denomination
   matches its consumption site, and whether it was derived or merely chosen.
2. **Dimensional declaration** — every new quantity declares its units; every core equation is
   checked for unit balance before implementation.
3. **Corridor independence** — no corridor is specced without naming what it is independent OF and
   how a change in the measured system could make it fail. Self-referential corridors are refused.
4. **Coupling map** — a short table of which existing constants, corridors and emergence tests each
   new system perturbs, doubling as the re-anchoring checklist.

Full text, with the location of each inside the spec skeleton, is in S8 §4.1.

## 3. What it would have caught, concretely

This is the test of whether the format earns its cost. Against the actual T3.2b findings:

| finding | which requirement catches it | how |
|---|---|---|
| `yieldPerFarmlandPerYear` denominated per 256 km² node, consumed as per-km² | **1(b)** | the audit asks the same question at both ends of the pipe; the producer wrote nodes, the consumer multiplied as km² |
| the constant was never derived | **1(c)** | traceable, and the trace is the finding: `40.0` appears bare at T1.5 (`0e63cb8`), becomes `28.0` at T1.6 (`7ddf7a8`) in a retune taken *only* so "the no-order production rate is unchanged". Neither step derived anything |
| `CatchmentSystem.TravelBudget = 15.0` invisible to tuning | **1(a) + 1(d)** | in scope by DEPENDENCY (it reaches M3's systems through `EffectiveArableKm2`). 1(a) is answerable — "15 cost units" converts to 240 km ideal-ground — and it is the (a) plausibility half that then bites: 240 km is indefensible against any historical catchment. Its LOCATION is the separate catch: 1(d) flags a code literal invisible to tuning as a finding in its own right |
| `harvest = arable × yield` could not be dimension-checked | **2** | T1.5 declared the numerator ("1 food = 1 person-year, D-015") but `EffectiveFarmland` — introduced at T1.4 — never declared a unit at all, so the balance check was not failed, it was UNPERFORMABLE. Requirement 2 forces the declaration that makes it performable |
| the proposed 0.12 density floor, self-referential via the cancellation identity | **3** | the bound and the measurement share `s̄`; cancelling shows the sim term disappears |
| nine downstream test failures discovered one at a time | **4** | the coupling map lists catchment/farming's dependents before the work starts |

Six for six — with two honesty notes. First, two of the rows were initially scored wrong in
drafting and only survived once checked against git history and the T1.5 sources — itself a small
argument for the format: the audit questions are answerable, and answering them properly changes
the answer. Second, rows 1, 2 and 4 trace to ONE underlying fault (one field, one constant, one
equation) seen through three different requirements; the table demonstrates coverage of the
recorded findings, not six independent faults.

## 4. Blast radius

**Docs:** S8 §4 gains §4.1; S8 §6's CLAUDE.md patch block and CLAUDE.md itself gain one line each
(the inheritance mechanism — a rule recorded only in S8 reaches no one, because CLAUDE.md is what
every agent actually reads and it made S8 conditional reading). S8 §5's "next permitted document"
line was also corrected under this same override: it still named the M1 spec three milestones after
M1 closed, and it now points at CLAUDE.md's current-milestone line instead of naming any milestone,
so it can never go stale again. The M4 spec, when written, gains one packet (`T4.1`) and two short
sections. No existing spec changes — M0–M3 are written and M3 is mid-flight, so retro-fitting would
violate the same "don't rewrite ratified specs" instinct the freeze exists to protect.

**Tests:** none in CI — a lint that checked for the presence of four section headings would measure
compliance rather than thought. But "no CI gate" is not "no teeth": `T(n).1` carries an explicit
acceptance form in §4.1 — a written table, one row per in-scope quantity, every row answering
(a)–(d) explicitly, "never derived" stated rather than omitted, no unexplained omissions — because
an audit that can pass by silence is not an audit. That is enforcement through the gate every other
packet already uses (CLAUDE.md: definition of done = the packet's stated acceptance criteria), so
it adds no process. This reconciles with ADR-013 §2's "a convention nobody can fail is not a fix":
the failable artifact here is the table, adjudicated by the director.

**Code:** none.

**Schedule price:** one packet per milestone. Cheap when the audit is clean — a table and a
session. Expensive exactly when it should be, i.e. when it finds something, and then the expense is
work that was going to happen anyway, moved earlier and cheaper. T3.2b cost a full directed packet
plus two CRs *after* the dependent systems had already been specified against the bad constants; the
same finding inside `T3.1` would have cost a correction before anything was built on it.

## 5. Where this format would NOT have helped, stated so it is not oversold

CR-003 is the honest limit. No paper audit would have found that the Malthus corridors were passing
on the *product* of two compensating errors — that took running the real kernel across a 6 000-year
campaign and measuring that population is set by the demographic clock rather than the food ceiling.
Requirement 3 asks a corridor to name what it is independent of; the Malthus corridors would
plausibly have answered "of geometry", which is true and still would not have predicted the
coincidence.

That is the ruling's own point: reality correcting the spec is the system working. The format is
aimed at the errors that were catchable on paper, and it should not be defended as more than that.
A second-order caution follows from it — a spec author who has completed the four items may feel
covered. They are not. The four items reduce avoidable surprises; they do not license skipping
measurement, and an audit that returns "all clean" is evidence about paper, not about the world.

## 5b. Evidence the format works — first outing, T3.3 (director-accepted, 2026-07-26)

T3.3 was the first packet written after §4.1 landed, and applying the new requirements to it
surfaced two real defects that nothing else in the project would have caught. Recorded here
because a format's value is an empirical claim, and this is the first evidence for it.

**Requirement 2 (dimensional declaration) caught a contract that contradicted its own data.**
T3.2's `goods.json` doc declared recipe `inputs per output unit`. The data it shipped says
otherwise: `toolmaking` declares `bronze: 1.0` with `output.qty: 2`. Under a per-unit reading
`qty` is decorative and toolmaking's entire value-add disappears. T3.3's code implements the
per-EXECUTION reading, so **the code was right and the declaration was wrong** — the reverse of
the usual assumption, and only visible because requirement 2 forces the declaration to be
written down and checked against the equation. Nothing pinned the reading before; it would have
drifted at T3.4 when prices started weighting recipes. Now corrected in both the data doc and
`GoodsConfig`, and pinned exactly at 2 tools per bronze
(`ProductionTests.Recipe_InputsAndLabor_ArePerEXECUTION_NotPerOutputUnit`).

**The audit habit caught a load-bearing claim with no test behind it.** `ProductionSystem`'s
header asserted that `OverdrawPolicy.Throw` on recipe inputs is safe *by construction*, because
each recipe re-reads the already-drained stock. Timber is an input to three recipes, so that
claim carries the conservation guarantee for the whole crafting path — and it was prose.
`Crafting_ThreeRecipesShareOneScarceInput_NeverOverdraws` now runs all four recipes against 37
timber with the artisan gate open and asserts `start == left + used` exactly.

Both were found by the implementing agent applying the rules to its own packet, which is the
mode the format was written for: the questions are cheap to ask, and asking them properly
changed the answer. Neither is the kind of defect a test suite finds, because in both cases the
code and the tests agreed with each other — it was the *stated contract* that disagreed.

## 6. Consequences

- The next spec written is M4's, per S8 §4 (after M3's exit gate). It is the first to carry §4.1.
- The foundations-audit packet is a normal packet: its acceptance criteria are the §4.1 table form
  (one row per in-scope quantity, (a)–(d) answered explicitly, no silence), its findings are
  evidence, and a genuine conflict it turns up opens a CR under §3 like any other. Dispositions are
  fixed in §4.1 so the packet can close: (b) mismatches → CR; impossible magnitudes → escalate
  before dependents build; untraceable-but-consistent → recorded and queued, NOT corrected. It gains no new
  authority beyond that — an earlier draft of §4.1 gave it a standing mandate to correct
  untraceable constants in-packet, which the ruling did not grant and which is exactly the kind of
  added process the ruling's closing paragraph excludes. Removed.
- Audit scope is DEPENDENCY, not perturbation. An earlier draft scoped it by the coupling map,
  which item 4 defines by perturbation; that reading would have exempted
  `CatchmentSystem.TravelBudget`, the ruling's own origin case, since it reaches M3's systems only
  through `EffectiveArableKm2`. The coupling map is the audit's starting list, never its ceiling.
- `docs/queue.md` remains the destination for "a better way exists" findings from the audit. The
  audit is not a licence to redesign constants that are merely not to taste — it asks whether a
  constant is traceable and dimensionally correct, not whether it is optimal.
