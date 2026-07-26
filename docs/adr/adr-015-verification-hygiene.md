# ADR-015 — Verification hygiene: shared worktrees, and acting on findings before verdicts

Status: PROPOSED (director ratification requested for the CLAUDE.md amendment in §6)
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

## 6. Recommendation (director ruling requested)

1. Ratify a CLAUDE.md amendment extending the existing worktree rule:
   *"One worktree per verifying agent, never shared — concurrent mutation of a shared tree
   voids findings and refutations alike. No finding is actionable before its verdict returns;
   applying a fix on a finder's word alone is a review bypass, and a claim written into a commit
   message must have been measured by the agent writing it."*
2. T3.3 stays HELD. The revert changes the packet's behaviour back to what the review actually
   examined, so the tree now under review is `3a57ebf`'s `Craft` plus the kept items in §3.
3. Nothing here is a conflict between frozen items, so no CR is opened. The contested-input
   composition question is already T3.4's by scope, not by exception.
