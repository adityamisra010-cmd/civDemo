# ADR-015 — Verification hygiene: shared worktrees, and acting on findings before verdicts

Status: **RATIFIED** (director ruling, 2026-07-26 — §6 accepted as proposed; §7 added under the
same ruling)
Date: 2026-07-26
Context: T3.3 review round 2. Written by the implementing agent against its own work.

## 1. What happened

T3.3's five-lens adversarial review had two lenses die on HTTP 529. The director held
acceptance and instructed a re-run of exactly those two, pinned to the packet commit. The
re-run produced 7 raw findings. I read the findings, judged two of them serious, and applied
fixes — **before the verify stage had returned a single verdict**. I then wrote those findings
into a commit message as established fact.

When the verify stage did return, it refuted all 7. Two of its refutations were about the two
fixes I had already shipped. I re-measured both myself, in trees I extracted with
`git archive` and verified by md5, and the refutations were correct:

**Finding A ("BLOCKING: crafting is not dt-invariant under input contention").** The
observation was real: with timber scarce, the production *mix* varies with dt. The inference
was wrong. Law 3 requires every *rate* to be per-sim-year and integrated with `dtYears`, which
`Craft` does. The input cap is an inventory *level*; a Leontief `min(rate × dt, level)` is
correct precisely because only one side scales. Scaling the level by dt would authorise
consuming ten times the stock that exists at dt = 10 — a law 1 break dressed as a law 3 fix.
Which recipe wins a contested input is the allocation question relative prices answer, and
prices arrive at T3.4.

My fix — two-pass proportional rationing — was a regression on three axes. Measured at
`3a57ebf` (sequential) vs `ba9fafc` (rationed), 10 sim-years, identical worlds:

| case | sequential | rationed |
| --- | --- | --- |
| intra-turn chain, bronze starts 0 → tools, dt 10/5/2.5/1/0.5 | `50 50 50 50 50` | `0 24 36 44 46` |
| contested timber 60 → pottery | `120 100 84 76 70` | `60 75 73 71 71` |
| timber replenished by extraction, 100 y → pottery | `53332` at every dt | `32665 … 33301` |
| timber consumed of 60 endowed | `60` at every dt | **`45`** at dt = 10 |

The chain case is the decisive one. Sequential registry order is not untidiness to be cleaned
up — it is what makes the recipe *graph* work inside one turn: bronze-casting precedes
toolmaking, so toolmaking re-reads a stock bronze-casting has already credited. Rationing
computes every ration from stocks read *before any recipe commits*, so bronze read 0 at ration
time, the ration went to 0, and toolmaking was scaled out of existence. I replaced an exactly
dt-invariant behaviour with a 0-vs-46 spread, worst at dt = 10 — the shipped Neolithic band —
in order to shrink a spread that was not a defect. And the last row shows the fix also stranded
15 of 60 units of a scarce input, because my rationing multiplied a recipe's scale by every
contested good's ration instead of taking the minimum.

**Finding B ("MAJOR: the dt test is triple-vacuous — `* ctx.DtYears` can be deleted from
`Craft` with all nine tests passing").** The vacuity of that one test was real:
`Production_DtCorrect` allocated zero crafting labor. The load-bearing consequence was false. I
applied the mutant at `3a57ebf` and ran the filter: `Crafting_ScalesWithLabor_UntilInputsBind`
**fails** (Expected 50000, Actual 5000). A different test in the same file already pinned line
306, through the input ceiling. I had asserted the opposite in a commit message without ever
running the mutant myself.

## 2. The mechanism of the failure

Two causes, and the second is the serious one.

**(a) Five verifying agents shared one pinned worktree.** They ran concurrent mutation
experiments in it. One agent stripped `* ctx.DtYears` from the shared tree while another was
taking measurements in it. Two agents reported the tree being mutated or deleted from under
them mid-measurement, and recovered by extracting clean trees themselves — which is the only
reason any of their numbers are trustworthy. The deletion was mine: I ran
`git worktree remove --force` on the pin believing the workflow had finished. Under the
standing rule — *findings against any other tree are void* — everything measured in that window
was void in **both** directions. The rule was written to stop false findings; it protects false
refutations equally, and I had not thought of it that way.

**(b) I acted on raw findings before their verdicts returned.** This is the real defect. The
verify stage is not decoration; it is the step that distinguishes a real defect from a
plausible-sounding one, and it exists because finder agents are rewarded for finding things. I
skipped it, shipped a regression, and recorded an unverified claim as fact in the permanent
history. The director's diagnosis of the previous round — *"a review that looks complete because
most of it ran is the vacuity pattern at the review level"* — has a sibling one level up: **a
fix that looks verified because a lens said so.** A finding is a hypothesis. My own certainty
that it was obviously right is exactly the feeling the verify stage is there to overrule.

## 3. What was reverted, and what was kept

Reverted: `ProductionSystem.Craft` restored byte-identical to `3a57ebf`, and the now-unused
`PerUnit` helper removed with it.

Kept, because each is independently defensible on grounds that do not depend on the refuted
findings — flagged so the record does not imply they were confirmed:

- **Duplicate-input rejection in `GoodsConfig`.** Refuted as a T3.3 *defect* (unreachable
  without hand-authoring a recipe the schema does not define). Kept anyway: data files are
  director-tunable by standing rule, so a `goods.json` edit could reach it, and a load-time
  message costs nothing against a kernel crash.
- **`SystemCatalog`'s sanctioned-shared-stock record.** Refuted as a defect; the doc was
  genuinely stale (named `Farming`, mis-assigned `ConsumeRemainder`). Doc-only.
- **`LastProducedUnits` zeroing.** Split verdict — confirmed by the first round, refuted by the
  second as observational and therefore not a law-1 matter. Kept as a truthfulness fix; the
  founded golden is unchanged either way.
- **The un-vacuumed `Production_DtCorrect`** (crafting share 1.0, inputs endowed, non-vacuity
  asserted per sector). The vacuity was real regardless of what it did or did not prove.
- **HUD truthfulness and the two demolition-gate holes**, which came from the two lenses that
  were never contaminated and were confirmed in round one.

Added: `Crafting_IntraTurnRecipeChain_IsExactlyDtInvariant` asserts exact equality of tool
output across five dt, and `Crafting_ContestedScarceInput_ConservesExactlyAtEveryDt` pins
`endowed == left + used` with no epsilon at every dt while deliberately *not* asserting
composition invariance, with the measured composition table and the T3.4 reason in the comment.
Both **fail** against the rationed build (chain: exact-equality failure; conservation:
Expected 60, Actual 45). A revert without the test that would have caught the regression is
half a revert.

## 4. Why the packet's own tests did not catch it

They very nearly did, and the near-miss is instructive. No test covered the intra-turn recipe
chain, because the chain is emergent from registry order rather than stated anywhere as a
contract — the kind of load-bearing behaviour ADR-014 §4.1 requirement 2 exists to force into
the open, and neither I nor any lens declared it. `Crafting_ThreeRecipesShareOneScarceInput`
pinned conservation on the contested path and passed under both implementations, because
rationing conserved what it consumed; it just consumed less. Conservation tests cannot see
under-utilisation. That is a genuine gap in what "the books close" proves.

## 5. Standing in the S8 §4.1 record

ADR-014 §5b cites T3.3 as first-outing evidence that the spec format works, on two items the
director accepted independently — the recipe-contract correction and the `Throw`-safety test.
Both stand; neither is touched by this ADR. But §5b should not be read as the whole verdict on
that first outing. The same packet also produced a shipped regression from an unverified
finding. The honest summary is that the *format* found real things and the *process around it*
failed, which is why this ADR is about process and not about §4.1.

## 5b. The fifth lens, and what its silence had been hiding

The lens that never ran in the original review was test power. It produced no output *and no
error*, which is why its silence read as clean — the failure mode the director named one level
up, at the review level, has a quieter version at the lens level: a lens that never starts looks
exactly like a lens that found nothing.

Run pinned, it applied 26 mutants and **7 survived**. Five findings were confirmed by an
independent verify stage that tried to refute each and could not; one (a ±1 tolerance on a clay
ratio) was refuted and left alone. Two were BLOCKING, and the first was against the accept
clause's opening phrase:

- The entire `OrderKind.SectorAllocation` path — the mechanism that *delivers* "sector labor
  allocation (D-032)" — had no test anywhere. Making the handler fully inert passed all 357
  tests. Every other test hand-builds `SectorAllocationRow` directly and so bypasses orders.
- The whole herding/fishing sector could be deleted with 309 tests passing.
- Deposit magnitude was unpinned: an ordering assert (`stone > clay`) survived both a 10×
  labor error and the deletion of the per-worker abundance factor, because the ordering holds
  either way.
- Crafting labor was never pinned as being *split* across recipes: one crafter working four
  jobs at once survived.
- `Recipe_InputsAndLabor_ArePerEXECUTION` pinned the *input* half only. Deleting
  `* recipe.Output.Qty` from the labor allowance survived the full suite — the same contract
  ADR-014 §5b credits the format with correcting, pinned on one side.

Kill-record, run on an isolated tree materialised from `46f706a` and owned by one process
(the rule in §6, applied to its own evidence). Baseline 62/62 green; each mutant applied to a
freshly restored pristine file, built, run, reverted:

| mutant | result | killed by |
| --- | --- | --- |
| decode `>> 3` → `>> 2` | KILLED | `SectorOrder_TargetPacking_ShiftWidthIsPinned` + the five-weight test |
| SectorAllocation handler inert | KILLED | both sector-order tests |
| delete herding `FromDeposits` | KILLED | `Herding_WithoutDeposits_…FollowsAbundance` |
| Craft: drop `* Output.Qty` | KILLED | `Recipe_LaborIsPerEXECUTION_…` + the chain test |
| Craft: `laborPerRecipe = pool` | KILLED | `Crafting_LaborIsSplitAcrossAvailableRecipes_…` |
| abundance² → abundance¹ | KILLED | extraction + herding magnitude pins |
| extraction pool × 10 | KILLED | extraction magnitude pin |

One entry in the lens's own kill-record is **void** against the shipped tree, and it matters
because it points the same way as everything else in this ADR: the lens credits
`Crafting_WithAContestedScarceInput_IsDtInvariant` with killing a revert of the rationing loop —
"the BLOCKING dt-lens fix does have teeth." That test no longer exists. It asserted a property
the system should not have and was removed with the regression. A test can have perfect teeth
and still be biting the wrong thing, which is the whole of §1 restated in one line.

The lens also proposed one wrong number (2000 clay; the closed form is
`1000 × 4.0 × 0.2 × 10 = 8000`), caught by computing it before writing the assert rather than
after. Verifier arithmetic gets checked too.

## 6. Ruling (RATIFIED 2026-07-26 — accepted as proposed)

1. Ratify a CLAUDE.md amendment extending the existing worktree rule:
   *"One worktree per verifying agent, never shared — concurrent mutation of a shared tree
   voids findings and refutations alike. No finding is actionable before its verdict returns;
   applying a fix on a finder's word alone is a review bypass, and a claim written into a commit
   message must have been measured by the agent writing it."*
2. T3.3 stays HELD. The revert changes the packet's behaviour back to what the review actually
   examined, so the tree now under review is `3a57ebf`'s `Craft` plus the kept items in §3.
3. Nothing here is a conflict between frozen items, so no CR is opened. The contested-input
   composition question is already T3.4's by scope, not by exception.

## 7. Standing rules added under the same ruling

### 7.1 Every mutant run gets a hard timeout

A mutant run is bounded at a **stated multiple of the clean-suite baseline**, and the baseline
and the multiple are both written down before the sweep starts. The clean `Sim.Tests` baseline is
~3–5 minutes wall-clock; a mutant sweep on a filtered subset is seconds to tens of seconds. A
run that exceeds its bound is killed, not waited on.

**A mutant that hangs is itself a finding.** It is recorded in the kill-record in the same form
as any other result — `M<n> hangs the suite; non-termination under this mutation` — and never
treated as a reason to wait indefinitely or as a gap in the sweep. Non-termination is a stronger
result than a survived mutant, not a weaker one: a mutation that turns a terminating computation
into a non-terminating one has found a loop whose exit condition depends on the mutated
expression. Where the cause is cheap to determine, state it in one line next to the record
(which loop, which condition); where it is not, record the hang and move on rather than paying
for a diagnosis the sweep does not need.

Rationale: a hung run is indistinguishable from a slow one only until you have a baseline, and
we have one. Waiting on it converts a bounded sweep into an unbounded one and — worse — blocks
every downstream stage behind it, so a single non-terminating mutant can make an entire review
look incomplete for a reason that has nothing to do with the code under review. That is the same
shape as §1's failure: a review stalled by its own machinery, reported as a review still running.

### 7.2 Standing caution for verify stages: teeth are not aim

**A lens can have perfect teeth and bite the wrong thing.** A verify stage confirms that a test
fails against a mutant; it does not confirm that the test is asserting a property the system
ought to have. Both are required, and only the first is mechanical.

The two cases from this packet, both accepted by the director as void-rather-than-stand:

1. The test-power lens credited `Crafting_WithAContestedScarceInput_IsDtInvariant` with killing a
   revert of the rationing loop — "the BLOCKING dt-lens fix does have teeth." True, and
   irrelevant: that test asserted a property the system should not have, and was deleted with the
   regression. A kill-record entry naming a test that no longer exists, or that pins a wrong
   property, is void however cleanly it was measured.
2. The same lens proposed an assertion value of 2000 clay where the closed form gives
   `1000 × 4.0 × 0.2 × 10 = 8000`. Caught by computing the value before writing the assert
   rather than after. **Verifier arithmetic gets checked too** — a verify stage is not a source
   of truth about numbers, only about whether a run went red.

Operationally, a verify stage must therefore answer two questions and report both: *does the
test fail against the mutant* (mechanical, run it) and *is the property it asserts one the
system is supposed to have* (a judgement, and the one that cannot be delegated to a test run).
When those two answers disagree, the second wins.

### 7.3 A review is incomplete until every lens has reported — including the ones that never started

**Director ruling, 2026-07-26, recorded at the director's own instruction as a director-level
error.** The record should say so plainly, because the failure was not the implementing agent's:

> Acceptance was held for the two lenses that ERRORED — conservation and dt-determinism, the two
> judged most important for this packet. Both returned **zero confirmed findings**. The packet's
> worst defects came from the test-power lens, which never started in round one and whose silence
> nobody questioned, the director included. Chief among them: an untested `SectorAllocation`
> path that could be made fully inert while 357 tests passed — the packet's *headline mechanism*,
> the accept clause's opening phrase, delivered by code nothing exercised.

**THE RULE: A REVIEW IS INCOMPLETE UNTIL EVERY LENS HAS REPORTED, INCLUDING THE ONES THAT NEVER
STARTED.**

An errored lens is loud. It produces a stack trace, an HTTP status, a visible failure — it gets
noticed, and it got noticed here, which is why acceptance was correctly held. A lens that
silently never launched produces *nothing at all*, and nothing at all is indistinguishable from
a clean report. It is therefore the more dangerous omission, and it is the one the process had
no defence against.

Operationally, before handback:

1. **Enumerate the lenses the packet REQUIRED** — from the packet's own adversarial mandate,
   written down before the review runs, not reconstructed afterwards from what came back.
2. **Check each against the lenses that actually RAN**, by name, against the returned results —
   not against the launch call, which is precisely what succeeded here while the lens did not.
3. **A missing lens is a BLOCKING condition, not an oversight.** It is reported as such in the
   handback, in the same register as a failing gate. "Four of five ran" is not a review; the
   handback must say which five were required, which reported, and what each returned.

**STANDING PRACTICE (director ruling, 2026-07-26): the lens manifest is written as a committed
file BEFORE implementation begins, on every adversarial-mandatory packet.** Not a T3.4 one-off.
The manifest names each required lens, what it attacks, and the §7.1 mutation bounds, and it
carries the handback checklist. First instance: `docs/t3.4-lens-manifest.md`, committed as the
packet's first artifact ahead of a line of implementation code.

The ordering is the whole point and is not negotiable for convenience. A list written after the
results arrive is not a checklist, it is a description — it cannot detect a missing lens, because
whatever came back defines what was required. Writing it first is what converts "did everything
run?" from a memory question into a diff. The packet's own count may exceed the spec floor where
the packet's risk argues for it (T3.4 requires six against a floor of five, naming test-power
separately rather than trusting it to fold into another lens).

This is the same defect as §1 and §7.1 at a third altitude, and the family is now complete:

| altitude | what looks fine | what is actually true |
| --- | --- | --- |
| §1 — the fix | a fix looks verified because a lens said so | the verdict never returned |
| §7.1 — the run | a hung run looks like a slow one | it will never terminate, and blocks everything behind it |
| §7.3 — the review | a silent lens looks like a clean lens | it never launched |

In every case the artefact that should have carried the alarm is *absence*, and absence is the
one signal a passing check cannot distinguish from success. The countermeasure is the same each
time: state what you require BEFORE you run it, then reconcile against that list — never read
the absence of a complaint as a report of no complaints.


### 7.4 Every guard, clamp or rail ships with a test that fails when it is removed

**Director ruling, 2026-07-26, from the T3.4 review.** The per-turn relative-change rail — a
shipped safety mechanism, one of the two clamps D-033 names explicitly — could be **deleted from
the shipped source with all 333 tests green**. The rail's own test drove such an enormous excess
that the BAND clamped the price anyway, so removing the rail changed nothing the test measured.

**A shipped safety mechanism whose absence no test detects is not a safety mechanism.** It is a
comment that happens to compile. The rule:

> Every guard, clamp, rail, floor, cap or validation ships with a test that FAILS when the guard
> is removed — and the red is PROVEN, not assumed. Delete the guard, run the test, watch it fail,
> restore it. A guard added without that step has not been tested; it has been described.

The failure mode is specific and worth naming, because it is not carelessness: the test existed,
was written in good faith, and named the right mechanism. It was disarmed by a fixture so extreme
that a *different* mechanism reached the answer first. A guard test must therefore be built on a
fixture where the guard under test is **the only thing** that can produce the asserted bound.

### 7.5 Never assert on a quantity resting against its own limit

Every confirmed T3.4 test-power finding had one shape: a long horizon drove the price onto a band
edge, and the assertion then compared two **clamp constants**. `0.05 == 0.05` is true whatever
the system under test does. The tests were not weak — they were *disarmed*, and they went on
passing.

> **General form: an assertion on a saturated quantity compares limits, not behaviour.** Before
> comparing two values produced by a system with clamps, assert that the values are strictly
> inside their limits. If they are not, the comparison is vacuous no matter how exact it looks.

The shipped pattern is `PriceTests.AssertOffBandEdges`, called at every price-comparing assertion
point. It failed loudly the moment it was introduced — the scarcity-shock "control good" had been
asserting `BandMin == BandMin` — which is the argument for the guard being mechanical rather than
a habit of care.

This is the same family as §7.1 and §7.3: the artefact that should have carried the alarm is a
value that *looks* like a measurement and is actually a constant.

### 7.6 The empirical case for the six-lens floor

`MarketScaleFloor` was a dt-independent constant in a denominator whose numerator scales with dt.
**Lens 1 (no-global-solve) chased it and deliberately declined to raise it**, reasoning that the
per-step rail masked it everywhere except an unreachable corner. **Lens 4 (dt-determinism) raised
it, and an independent verifier confirmed it.** Both analyses were careful; they weighed the same
code and reached opposite calls.

That is the empirical argument for a lens FLOOR rather than a lens budget: not that six reviewers
find six times as much, but that a defect can sit exactly on the boundary between two lenses'
remits and be reasoned away by whichever one meets it alone. Recorded in
`docs/t3.4-lens-manifest.md` as the rationale for requiring six against a spec floor of five.


### 7.7 A corridor insensitive to its own control parameter is measuring something else

**Director ruling, 2026-07-26, from T3.4b.** The migration magnitude corridor was defended and
attacked for two sessions as though it measured a migration propensity. It did not.

`baseRatePerYear` 0.03 / 0.018 / 0.012 produced 0.43 / 0.41 / **0.42** %/decade — barely moving,
and NON-MONOTONICALLY. The cause: T2.8's gap-closing cap is `GapClosingFraction × m*` with
`m* = (R_j·P_i − R_i·P_j)/(R_i + R_j)`, a function of resources and population only. Whenever the
cap binds, **the base rate cancels out of the result entirely**. The corridor was measuring a
structural property of the world's land heterogeneity, on a world that at calibration time had
neither weather nor a live land signal.

> **THE RULE: a corridor whose measured value is insensitive to the parameter that nominally
> controls it is measuring something else. Establish what actually sets the value before
> defending or moving the band.**

This is a cousin of the density corridor's cancellation identity (ADR-013): there, a bound
derived as `P_hist/(H × s̄)` cancelled against the sim's own `s̄` and the instrument became a
mirror. Here the cancellation is in the *mechanism* rather than the algebra, and it was found by
**measurement** — sweeping the parameter and watching the output refuse to move — rather than by
inspecting a formula. Both failure modes produce a number that looks like evidence and is not.

The diagnostic is cheap and should be routine: **sweep the nominal control parameter before
trusting a corridor.** If the value does not respond, the band is not describing what its name
says. Note that the lever may be one-sided — here, pushing the rate DOWN did nothing while
pushing it UP 10× did move the value, by lifting desire above the cap in more pairs.


### 7.8 A new stochastic driver can silently destroy an existing experiment's control

**Director ruling, 2026-07-26, from T3.4b.** Recorded as doctrine because it will recur: every
future stochastic driver — weather, disease, raids, price shocks — creates this hazard on the day
it lands.

> **When a new stochastic driver is introduced, existing controlled experiments may silently lose
> their control.** The baseline arm acquires the same variance as the treatment arm and the
> contrast collapses.

The T3.4b instance, measured: `Famine_MortalitySpike` compares a deliberately starved arm against
a fed baseline, and the contrast IS the measurement. At the derived `sigmaLogYield` the famine arm
reported **LOWER** per-capita mortality than its own baseline — **0.609 vs 0.707** — because the
baseline had bad years too. The grievance rig's starvation window likewise *fell*, 5.96 → 2.04.
Neither rig was broken; both had been quietly disarmed by noise arriving on both arms at once.

**Running such rigs with the driver disabled RESTORES the control; it does not suppress the
phenomenon.** The distinction that matters, and it is easy to blur:

| instrument | purpose | treatment of a new driver |
| --- | --- | --- |
| a **rig** | isolate ONE variable | disable the driver — it is a confound |
| a **system-level soak** | observe INTERACTION | keep the driver — it is the subject |

Removing a confound from a rig is not the same act as hiding it from a soak, and the second would
be the vacuity pattern. The test is what the instrument exists to measure: a controlled experiment
that lets an uncontrolled variable into both arms has stopped being controlled, whatever its name
still says.

**THE STANDING OBLIGATION: every future stochastic driver must AUDIT EXISTING RIGS for this
collapse** — enumerate the controlled experiments the driver can reach, check each for contrast
loss, and either isolate the rig or state why it survives. Silence is not evidence the rigs held;
in T3.4b two of them had already inverted before anyone looked.


### 7.9 A third way a lens vanishes: the harness discards a completed result

T3.4b's six-lens review was run, and **all six lenses reported nothing** —
`allSixReported: false`, every lens in `missing`, each with
`StructuredOutput retry cap (5) exceeded — 5 failed calls with no valid output`. Thirty failed
emissions. The journal showed six `started` entries and zero results.

**The lenses had not failed. They had finished.** Usage records ~240k subagent tokens and 90 tool
calls across the six — each had read the code, run tests, and reached conclusions. What failed was
the *emission*: the findings schema I wrote was strict (`additionalProperties: false`, required
fields, closed enums on `severity` and mutant `result`), and every attempt to serialise a real
finding against it was rejected until the retry cap ran out. The investigation was then thrown
away.

This is a THIRD distinct mechanism by which a required lens produces silence, and the family is
worth naming together because each looks different and they all end the same way:

| # | mechanism | what it looks like | first seen |
| --- | --- | --- | --- |
| 1 | the lens never launched | nothing at all, no error | T3.3 round 1 |
| 2 | the lens errored in flight | HTTP 529, visible failure | T3.3 round 2 |
| 3 | **the harness discarded a completed result** | retry-cap error, work already done | T3.4b |

Only §7.3's rule — reconcile required lenses **by name against returned results**, never against
the launch call — catches all three. Against mechanism 3 in particular, "the workflow ran and
finished" is true and useless: it ran, it finished, and it reported nothing.

**THE LESSON FOR SCHEMA DESIGN: a validation schema sits DOWNSTREAM of all the expensive work, so
its failure mode is uniquely bad — it converts a finding into silence after paying full price for
it.** Prefer permissive: no `additionalProperties: false`, minimal required fields, enums
expressed as descriptions rather than constraints, and union types where a model might reasonably
emit either. A schema strict enough to reject a real finding is worse than no schema at all. Brief
agents on an emission budget too — an oversized result is a result that never existed.
