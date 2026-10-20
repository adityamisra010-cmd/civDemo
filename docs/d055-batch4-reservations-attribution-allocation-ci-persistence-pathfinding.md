# D-055 — THE DIRECTOR'S BATCH 4: MATERIAL RESERVATIONS ACROSS TURNS, CYLINDER FEATURE ATTRIBUTION (W1 F7), A DURABLE ALLOCATION CI LEG, THE PERSISTENCE AUDIT, CYLINDER PATHFINDING

**Status: RECORDED 2026-10-17, uncommitted, for the Director's review.**
- The instruction is the Director's own: "Batch 4 — Independent Audit Remediation", items 1–7. §0 quotes it verbatim.
- This record states how it was carried out: the rule chosen for each item, what it supersedes, the measured
  movements, the moved pins with their attribution, and the limitations.
- **Nothing in it is escalated, and no CR was written.** The instruction says so in as many words ("Do not ask the
  Director to resolve this already-established requirement. If an implementation detail is unspecified, choose the
  smallest deterministic rule consistent with the existing design and document it"). Each such rule is recorded here
  where it applies, marked **RULE CHOSEN**.
- **Authority.** "The Director's latest explicit requirements supersede conflicting historical ADRs, tests and
  implementation assumptions." The instruction therefore RESOLVES D-054 §10 Q17 (§1, §10).
- **The technical contract** (schema v34, the v33 → v34 migration, the construction → roaddevelopment read, the
  Pathfinder surface, the CLI additions) is `docs/adr/adr-040-schema-v34-construction-holds-pathfinder-surface-and-cli.md`.
- **The batch-4 orchestrator's ruling of 2026-10-17 on the kernel** is recorded in §11, as given. It is the batch's own
  ruling, not a Director ruling.

**Not an M5 exit record, and not authorisation to publish.** M5 stays NOT complete (D-052 §15). Batch 4 exists as an
uncommitted change on top of the uncommitted batch-3 tree (the batch-2 base plus `b3-final-v9`) of `m5w-integration`
(`de86ace`). Nothing is committed, pushed, merged, tagged or packaged: "No commit, push, merge, tag or publication is
authorised."

**Provenance of the numbers.** The batch was built in six tracks, each in its own scratch clone, each reviewed by
independent verifiers and closed by an adjudicated fix round (T1a by two rounds and a final fix; T3 by a fixer round, a
second round and a third, fixer, round). Every figure below was measured by a track, or by one of the integration's
stages (merge, update, the two validation stages, fix, records-finalize), on a stated tree, EXCEPT these, each marked
where it appears:
§6 items 1–5 (the batch's item-5 reconnaissance, quoted by T5 without being re-run); §1.11's holds-heavy workload (the T1a
verifier's probe); §1.5's completion-step road payment (a T1a step-3 probe, on the tree §1.5 names). The trees:
- **base-b3** (the batch-3 final tree): `de86ace` plus `b3-final-v9` (sha256 `0f8f45c9…`), tree `b624e62c…`.
- **T1a** (item 1, the Sim.Core rule and schema v34): `t1a-v4.patch`, sha256 `68167754b9638ee52186de065b7d944eae88f78801cd5a53debb53f161eb8897`,
  63 files over base-b3, work tree `6c8e3f18…`. Its earlier revisions (`t1a-final` `01179819…`, tree `48e7c13b…`; v2;
  v3) are history; §1 names what each round changed.
- **T5** (item 5, pathfinding): `t5-v2.patch`, sha256 `a04c3f2c…`, 13 files over base-b3, work tree `3995c272…`.
- **T1b** (item 1, the player-facing text): `t1b-v2.patch`, sha256 `8b1fbb80…`, a delta of 20 files over base-b3 + T1a v4,
  work tree `b9f52cf8…`.
- **T3** (item 3, the allocation CI leg and the CLI): `t3-v4.patch`, sha256 `32846906d71cb60a2f2eeca0f5f1bc1be9e7a26d2fc1d4f8ff3074a14384888d`,
  a delta of 24 files over base-b3 + T1a v4, work tree `0d499208…` — the final revision, after round 2 (`t3-v3.patch`,
  `845d9b09…`, tree `55efd659…`) and round 3 (the fixer). The merge stage merged the fixer round's revision
  (`t3-v2-fixer-round1.patch`, `1b477d7c…`, 21 files, tree `b4aeec26…`); the update stage replaced it (below).
- **T2** (item 2, F7): `t2-v2.patch`, sha256 `e106c3cb…`, a delta of 18 files over base-b3 + `t1a-final`, work tree
  `b4219b0d…`.
- **T4** (item 4, persistence): `t4-v2.patch`, sha256 `cdbdfea5…`, a delta of 7 files over base-b3 + `t1a-final`,
  work tree `4ab9c486…`.
- **The merged tree** (the integration's merge stage, 2026-10-17): the six patches applied in the order T1a, T5, T1b,
  T3, T2, T4; work tree `6a9f6dc9f5808ef73f617d941c1462286eea6cc9`, 133 files, +20,448 / −572 over base-b3. No textual
  conflict; two files were merged 3-way by git with disjoint hunks (`PathBuildSystem.cs`: T1a and T5;
  `IntegratedSaveLoadBatteryTests.cs`: T1a and T4). The merged tree builds with 0 warnings and 0 errors with no
  integration edit to any source file. This record, ADR-040 and the file-end dated notes (§10) were added to that tree
  as docs only.
- **The updated tree** (the integration's update stage, 2026-10-18): the merged tree with T3's fixer-round delta replaced
  by its final revision — the interdiff `b4aeec26…` → `0d499208…` (17 files, +1,516 / −125) applied as written, every
  pre-image equal to the merged file and every post-image equal to T3's final file; no other path changed. Code tree
  `06eae6bab60d6b50999abb20224642cbeac64987`, 136 files, +21,839 / −572 over base-b3 (133 + T3's three new files). It
  builds with 0 warnings and 0 errors, again with no integration edit to any source file.
- **The fixed tree** (the integration's fix stage, 2026-10-18, after the two validation stages ran on the updated tree
  `4602ed4d…` = patch `int-pre-validate.patch` `e4b607ed…`): the adjudicated findings of the integration's verification
  applied — records corrections, and two COMMENT-ONLY source edits (`Sim.Cli/FromSave.cs`: a stale sentence on
  `--ai-empires` replaced by the founding-count rule the code follows;
  `Sim.Tests/Systems/ConstructionLifecyclePersistenceTests.cs`: L10's "never an input" scoped to construction, as X1 is
  scoped since T1a round 2). No code, test assertion, pin or data changed. Work tree
  `30fed100871cb00a8004adb491d5a7479d644e0c`, patch sha256 `2e2353eb…`, 144 files over base-b3; §8 says what was re-run
  on it.
- **This record's tree** (the integration's records-finalize stage, 2026-10-18): the fixed tree plus §8's validation
  table and the matching `docs/current-state.md` entry. Only those two docs files changed; outside `docs/` it is the
  fixed tree, file for file (§8).

Every result in this record is **scratch-only** (a track clone, the integration clone or a throwaway clone of an
integration patch). Results on the integration worktree are reported in the batch-4 report, not in the repository (§8).
gov-4 §9 applies: a result names its tree and is not a certification.

**Record history.** First written 2026-10-17 at the integration's records stage, from the six tracks' records and notes
and the merge stage's notes. Updated 2026-10-18 at the integration's update stage, when T3's final revision replaced the
merged one: the provenance, §4 (the scenario's b = 70 batch, the judgement P11, P12 and PT, the parser, the founding AI
count, the tests, the mutants and the limitations), §5's R7 cell, §7, §8 and §9 items 7 and 8, from T3's round-2 and
round-3 notes and its records fragment. No other section changed. Updated again 2026-10-18 at the integration's fix
stage, for the adjudicated findings of the integration's verification (INT-L1-V1-L1-F1, INT-L2-V2-L2-N1, INT-L3-R3-F1 to
F9): the provenance (the exceptions, the fixed tree), §1.5's probe tree, §1.10 item 7, §1.11, §3 (the explain sample's
seam exposure, TB4), §5 (the legend, the cells and the recorded gaps), §6's summary line, §7's pin count, §8 (the T4
row, the validation stages, the fix stage), §9 items 9, 12, 14 and 15, §10 item 3 and the closing paragraph. Updated
last 2026-10-18 at the integration's records-finalize stage: the provenance (the stages that measured, the fixed tree's
id and patch, this record's tree, the scratch-only paragraph) and §8 (the first paragraph's note; the validation stages'
and the fix stage's paragraphs replaced by the result table on the scratch integrated tree, what was re-run on the fixed
tree, the fix stage's mutants, the records-finalize stage, the check of the validated code, and where the worktree
validation is reported). No other section changed.

---

## §0 Source (verbatim, the Director's batch-4 instruction)

> Batch 4 — Independent Audit Remediation
>
> Continue from the current uncommitted integration worktree. Preserve every existing change. Do not reset, clean, stash, overwrite or discard work. Do not commit, push, merge, tag, publish, or switch branches.
>
> The Director's latest explicit requirements supersede conflicting historical ADRs, tests and implementation assumptions. Do not escalate already-resolved product decisions merely because earlier documents conflict.
>
> 1. Fix the construction reservation defect
>
> The batch-3 report acknowledges that an older, partially built construction project may lose materials it still needs to a younger project when the older project cannot advance.
>
> This violates the requirement that projects must not consume resources reserved by other projects.
>
> Implement deterministic material reservations across turns. Preserve the priority of existing projects according to a documented, deterministic rule. Ensure reservations interact correctly with:
>
> - incremental progress and material consumption;
> - insufficient builders or missing materials;
> - competing projects in different settlements;
> - empire-wide stockpiles;
> - player-controlled building/road material allocation;
> - cancellation, completion, and re-enqueueing;
> - save/load and schema migration.
>
> Do not create fictitious stock, double-count reserved material, allow negative stockpiles, or deadlock all construction because of an invalid reservation. Specify exactly when reservations are released or reallocated. Add adversarial regression tests for these cases.
>
> Do not ask the Director to resolve this already-established requirement. If an implementation detail is unspecified, choose the smallest deterministic rule consistent with the existing design and document it.
>
> 2. Close W1 F7
>
> Add the missing turn-exact feature-attribution proofs against the shipped cylindrical topology, including food relief and every other feature currently covered only by flat-world rigs.
>
> Demonstrate that each test:
>
> - exercises the intended feature on the cylinder;
> - detects a deliberately disabled or broken implementation;
> - distinguishes feature-specific effects from topology effects;
> - remains deterministic.
>
> 3. Make allocation verification durable
>
> Add a real cross-process CI leg that sets a non-default building/road allocation, demonstrates that the split actually constrains spending, and proves deterministic replay or matching hashes across processes.
>
> The test must prove behaviour, not merely prove that an order was accepted or serialized.
>
> Cover scarcity, surplus transfer, competing construction projects, roads, and save/load.
>
> 4. Audit persistence and lifecycle behaviour
>
> Review partial construction, reservation state, allocation settings, cancellation and re-enqueueing across save/load, schema-v32 migration, resumed UI sessions and deterministic replay. Add or correct tests for any uncovered cases.
>
> 5. Profile cylindrical pathfinding
>
> Investigate the reported 13.8% simulation slowdown. Profile the wrapped road/pathfinding workload, identify the actual hotspots, and implement safe optimisations where justified. Do not weaken correctness tests or silently change calibration baselines.
>
> 6. Independent validation
>
> Run the relevant focused tests first, then the complete build and regression suites. Include determinism, cross-process CI, save/load/migration, read-only mutation checks, topology, pathfinding, calibration, UI, and smoke tests.
>
> Clearly distinguish:
>
> - tests run on the actual final worktree;
> - tests run only on scratch clones or intermediate patches;
> - pre-existing failures;
> - new failures;
> - untested or manually verified behaviour.
>
> Do not claim a test passed unless its result and the exact tested revision are available. Do not silently re-pin frozen calibration references.
>
> 7. Preserve and report
>
> Before and after work, record branch, HEAD, index state, worktree manifest and relevant patch hashes. Preserve all pre-existing uncommitted changes. Do not delete unrelated files or perform housekeeping outside the repository.
>
> Return a concise report with each defect, root cause, fix, files changed, test evidence, remaining limitations and the exact final worktree state. Do not claim full completion while any required behaviour remains unimplemented.
>
> No commit, push, merge, tag or publication is authorised.

---

## §1 Item 1 — material reservations across turns ("set aside")

**The defect** (D-054 §1 and §10 Q17). Under batch 3's definition — claims are counts, not set-asides; the building
order applies only among heads that progress in the step — a younger head could take the whole remaining need of an
older, part-built head that was waiting (for example for builders). The instruction names this a defect: "projects
must not consume resources reserved by other projects".

**Terms.** A **head** is the lowest slot of a settlement's construction queue, the only entry that progresses. A
**hold** (in the code; "set aside" for the player, §1.9) is an amount of a good kept back for a head. The **purse key**
names whose stock a head draws from.

### §1.1 The switch, the control and the twins

- **Switch:** `sim.json` `construction.reservations`, shipped **true**. The loader refuses it unless
  `construction.incremental` is true ("construction.reservations requires construction.incremental to be true (D-055)
  …"). `construction.empireStockpile` is NOT required: purse keys follow the holder set (§1.2), so the rule is defined
  with the purse off.
- `Reservations.Enabled(cfg)` is true only when the switch and incremental construction are both on.
- **Control:** `TestConfigs.PreReservations`. Under it construction clears the table and publishes nothing, building
  demand reverts to draws only, and road development subtracts nothing: batch 3 plus the v34 layout, byte for byte
  through `SchemaProjection.V33`, on the founded, driven, AI and battery worlds.
- **The twin enumeration (the R4 rule: an older layer twin strips every newer layer).**
  - **The chained twins strip reservations:** `PreIncrementalConstruction` (the loader refuses reservations without
    incremental), and through it `PreD054Sim`, `PreD053` and every older chained twin (PreForager, PreR2b,
    PreTradeKnowledge, PreRecipeKnowledge, PreTimeAveragedHarvest, PreTaxAge, PreAiFounding).
  - **The single-layer helpers keep reservations ON:** `PreEmpireConstructionStockpile`, `PreEmpireFoodAllocation`,
    `PreGranaryEffects`, `PreAllocationRatios`, and item 2's single-switch helpers (`SingleSwitchTwins`, §3). Each
    switches exactly one layer off.
  - **The recorded R4 exception: `PreAllocationRatios`.** Reservations are newer than the split, but the split's twin
    keeps them: the split's default (b = 100) must equal its absence, and holds are not part of the split. A
    reservations-stripping split twin would make "no split" differ from "the default split" wherever a hold row is
    published. The `TestConfigs` header records it ("Do not 'fix' it into the chain").
  - **A second recorded departure:** item 2's `SingleSwitchTwins` are NON-chained by design (each turns off one feature
    on today's world); the flat literal controls keep the chained rule. Neither set is to be "fixed" into the other.
  - Pinned by `IncrementalConstructionAttributionTests.TheShippedContent_SwitchesReservationsOn_ThePreReservationsTwinChangesOnlyIt_AndTheSingleLayerHelpersKeepIt`.
- **Attribution rule.** Every pin the rule moved reproduces its old value under `PreReservations` (plus
  `SchemaProjection.V33` where the layout matters) (§7).

### §1.2 Who holds, and the purse key

- **Who holds:** every construction queue HEAD, at any progress (0 included), unless it is RELEASED (§1.4).
- **Non-head queued entries never hold.** Their remaining needs stay CLAIMS, read by the obtainability guard, the AI's
  prudence and the display, unchanged.
- **The purse key** (`ConstructionQuery.PurseKey`, read on PREV) is the identity of the holder set the head draws from:
  - the controller's polity id (≥ 0), when `construction.empireStockpile` is on and the settlement is controlled;
  - otherwise −1 − settlementId: a single-settlement purse. That covers the purse off, stateless settlements and a world
    with no Controls rows.
- **The disjoint-holder rule for doubly-controlled places** (fix round 1; **RULE CHOSEN**). With the rule on, each
  purse's holder set is `ConstructionQuery.PurseHolders`: `Holders` (the same draw order) less every place whose own
  purse key is another's. The purses' holder sets are therefore DISJOINT. They differ from `Holders` only in a world
  with two control rows for one place, whose stock then belongs to its **lowest** controller's purse alone (the
  controller `TryGetController` and the purse key already name).
  - Why: the fix round's reproduction showed that with overlapping holder sets a place with two control rows was counted
    in both purses' budgets, a hold in one purse named units the other could draw, invariant (i) threw, and every later
    step threw the same.
  - With the rule off it is `Holders` exactly (batch 3). D-037's control cardinality stays documented-but-unenforced, and
    `Snapshot.Load` still accepts such a world (no CR).
- **The same rule for road works and the split** (round 2; **RULE CHOSEN**). With the rule on, an Empire's road works
  pay from — and its split counts S, D_r and the road forecast over — the places it controls less any place ANOTHER
  Empire's purse owns (`ConstructionQuery.EmpirePursePlaces`). A doubly-controlled place's stock thus belongs to its
  lowest controller's purse alone for construction, road works and the split alike. Every list differs from
  `PayingSettlements` only in a world with two control rows for one place; with the rule off it is `PayingSettlements`
  exactly.
  - MEASURED before and after (a probe on a hand-built double-control world): the split's S counted the shared place's
    40 timber (S 65, G_b 25, guarantee 33, forecast 40, road works paid 0); after it S 25, G_b 12, guarantee 13, forecast
    13, paid 13. Batch 3's arm is unchanged (S 65, 33 = 33).
  - Pinned by `ConstructionReservationTests.TwoControlRowsForOnePlace_TheOtherControllersRoadWorks_CannotSpendTheLowestControllersSetAside`,
    `…_StillRespectTheirOwnPursesSetAside`, `TwoControlRowsForOnePlace_UnderASplit_TheForecastAndTheRoadGuaranteeCountOnlyTheEmpiresOwnPlaces`,
    `TwoControlRowsForOnePlace_ARouteEndingAtThePlace_TheOtherControllersRoadWorksDrawOnlyFromTheirOwnPlaces` (the donor
    half; v4) and `EveryPlaceOfAnEmpireIsAnotherEmpiresPurse_ItsRoadWorksAndItsSplitDrawNothing_TheSetAsideStands` (the
    empty boundary; v4).

### §1.3 The walk, and the priority rule

**One implementation.** `ConstructionQuery.HoldsWalk` (`Sim.Core/State/ConstructionQuery.Holds.cs`) is a pure function.
It is called by `ConstructionSystem`'s step, by `HeadStatus` (the UI estimate), by `AllocationFor` / `OwnHeadClaims`
(building demand and the split's forecast) and, through `SetAsideQuery`, by the UI's readings. The release predicate,
the budget order and the labels therefore cannot differ between the step and what the player is shown. The AI does NOT
read it: its prudence and its `StructuralStall` cancel are unchanged.

**The walk, per purse key and good.**
1. `Budget_g` starts at the purse's holding S_g: NEXT stocks after production, trade and housing, and after this step's
   kind-11 cancels and kind-4 enqueues. Under an Empire's split it starts at min(S_g, max(0, G_b,g)) instead (§1.6). A
   good the split does not cover starts at S. A single-settlement purse is never under a split.
2. Heads are taken in **the building order**: ascending (EnqueuedTurn, SettlementId, Slot), all integer keys.
   EnqueuedTurn is the order's stamp turn (PREV.Clock.Turn of the delivering step); an entry with no progress row (a
   migrated v32 entry) reads −1 and comes first.
3. A RELEASED head draws nothing and holds nothing (it is still resolved, so the UI can show its reason).
4. Every other head is resolved by `ResolveHead` against Budget (the existing lockstep), then `Budget −= draw`, then
   `hold = min(Σ over its inputs of that good of Remaining(c, p'), Budget)`, then `Budget −= hold`, where p' is the head's
   progress after this step's draw.

The draw is subtracted BEFORE the hold, so draws + holds never exceed the budget. (The batch's design review corrected
an earlier formula here; the mutant "cap before the draw" is killed by a unit case with purse 10, cost 30 and labour ⅓.)
By induction Budget ≤ the physical holding left, so a draw never exceeds the stock.

**Priority — the documented, deterministic rule.** Priority is the ORDER'S AGE, (EnqueuedTurn, SettlementId, Slot),
never building progress. **A promoted slot** — the next entry of a settlement whose head completed (it is a head from
the next step) or was cancelled (a head in the same step: the cancel precedes the walk) — **takes its own order's place
in the building order** (round 2; documented, not changed). So an entry ordered EARLIER than a part-built waiting head
elsewhere in the same purse outranks that head as soon as it is promoted, and may draw the units the part-built head set
aside the step before; an entry ordered LATER stays behind it and waits, its line naming the holder. Pinned turn-exact
by `ConstructionReservationTests.APromotedSlot_TakesItsOwnOrdersPlace_OrderedEarlierItOutranksAPartBuiltHead_OrderedLaterItStaysBehind`
(completion and cancel paths; the promoted entry ordered at turn 1 and at turn 5 against the part-built head's turn 3).
The tie-dense test covers holds: `TwentyTiedHeadsAcrossTwoInterleavedPurses_SetAsideInAscendingSettlementId` (20 tied
heads, two purses interleaved).

**Cost.** The walk keeps per-purse state in arrays indexed by the key's position in a sorted array of the keys met
(`Array.Sort` + `Array.BinarySearch` on ints): no Dictionary or HashSet iteration, no LINQ, O(H log H + H × G). With
no head it returns at once.

### §1.4 Released = an INVALID reservation

A reservation is invalid when its holder can never progress in any future state. `ConstructionQuery.ReleaseOf`
evaluates it from PREV and the content, before and independently of `ResolveHead`'s check order:
- **(a)** the project is not in the content (`goods.ProjectById` is null);
- **(b)** the settlement has no labour adults (`InstitutionStaffing.LabourAdults(prev, cfg, settlement) <= 0.0`).
  Sim.Core never removes a settlement, so a destroyed or abandoned settlement is a depopulated one;
- **(c)** the project founds an institution whose viability failure is `ViabilityFailure.NoConfig`, which can never
  become true.

**Valid, possibly long, waits keep holding** — exactly "the older project cannot advance" in the instruction:
- materials short;
- no builders (a construction share of 0 with adults present: the player's labour split). The mutant "a no-builders
  wait is released" is killed by four tests;
- a market viability failure (transient: the published food-surplus ratio and the adults change);
- an unobtainable crafted input (transient: research can open a recipe).

The player sees the holder's reason and the remedy (§1.9). The AI still cancels its own structural stalls
(`StructuralStall`, unchanged).

**No deadlock from an invalid reservation.** An invalid holder releases in every step its condition holds; the oldest
unreleased head of each purse is always resolved against the whole starting budget (invariant (vi)), so no hold ever
blocks it, and cycles are impossible (a head is bounded only by strictly older heads). Pinned by #7 (release (a), (b) and
(c); the depopulated part-built university case, where the younger granary builds in that step) and #18 (an older head
that never gets builders: the younger one progresses with the surplus beyond the hold, and with none it waits with the
remedy line).

### §1.5 Release and reallocation: exactly when

Holds are recomputed from scratch every step, so no stale reservation state exists. They move no goods: only Ledger
draws move goods. They bind exactly two things: **later construction heads** and **road works**.

| Event | When the hold disappears | Who can use the units | Pinned by (`ConstructionReservationTests` unless named) |
|---|---|---|---|
| Cancel (kind 11, validated at delivery, applied before the walk) | in the delivery step | later heads, in that step's walk; no refund | #8; `IncrementalConstructionTests.ProjectsDoNotConsumeResourcesReservedByOtherProjects` (a) |
| Completion (p' = N) | in the completing step: its need is 0 after the final draw | later heads and road works, in that step | #9 (later heads). Road works in the completing step: MEASURED by a T1a step-3 probe, not pinned (a battery road order at turn 21 is paid 56 units in the university's completing step 21 → 22; `t1a-out/s3/probe-battery3.log`, in T1a's clone during step 3 — the step-2 tree `d7b8f69c…` plus step-3 test edits, Sim.Core as in `t1a-final`, tree `48e7c13b…`; not re-measured on T1a v4 or the integrated tree) |
| Next slot promoted after a completion | the promoted slot holds from the NEXT step, in its own order's place | — | #9; `APromotedSlot_…` (completion rows) |
| Next slot promoted after a cancel | holds in the SAME step (the cancel precedes the walk), in its own order's place | — | #8; `APromotedSlot_…` (cancel rows) |
| A promoted slot ordered EARLIER than a part-built waiting head of the same purse | the part-built head's previous-step set-aside is not protected against it (priority is the order's age) | the promoted entry, in its first step as head | `APromotedSlot_…` (turn-exact; 4 rows) |
| Invalid: (a) unknown project, (b) no labour adults, (c) NoConfig | every step the condition holds; the hold returns, in the head's own priority place, when it ends | later heads in those steps | #7 (a), (b), (c) |
| Controller change (revolt) | the key is read on PREV, so from the next step the hold counts against the new purse | the old purse's later heads, from the next step | #11 (the AI does not cancel a head merely for being held) |
| Re-enqueue (a cancel and an enqueue of the same project in one batch) | the new entry has EnqueuedTurn = its stamp turn and p = 0; it holds only once it is a head, behind every older head | — | #10 |
| An extinct Empire | its settlements' heads key to the new controller, or to their own purse when uncontrolled; the CONTROL rows decide, not the roster | — | `AnExtinctEmpire_ItsHeadsKeyByTheControlRowsOnPrev` (both arms) |
| Other consumers (production, appropriation, consumption, trade, housing, tool wear, colonisation) | NOT bound: they may lower a purse below last step's holds, and the next walk recomputes from what exists | — | #17; #23 (no good colonisation moves is a construction input or a road class material) |
| Road works | bound: they use only the payers' holding minus the set-asides of every purse within the payers, read from `ConstructionHolds` AS IT STANDS when road development runs (§1.7, §11) | — | #13, #16, #22; `IncrementalConstructionTests.TwoHeadsAndARoad_…` (30 against 60 units); the battery's turn-1 road order |
| Road works of ANOTHER controller at a doubly-controlled place | not bound there: the place is its lowest controller's alone, so it is neither among their payers nor among their donors; their own purse's set-asides still bind them | — | the four `TwoControlRowsForOnePlace_…` tests (§1.2) |
| Reservations off (`PreReservations`) | the table is cleared and nothing is published | — | #25 |

### §1.6 The building/roads split (D-054 WS2) under the rule

- **Building demand D_b is building work's CLAIM:** Σ over the Empire's unreleased heads of (draws + holds), computed
  with no split (`OwnHeadClaims`). A waiting head now contributes its remaining need.
- **D_r is UNCHANGED:** road development's own charges against S (`SimulateCharges(S)`), as in batch 3.
- **The split is unchanged:** E_b = floor(S × b / 100), E_r = S − E_b, G_b = min(D_b, E_b + max(0, E_r − D_r)).
- **The forecast road charge and the starvation guard** run on S − forecast draws − forecast holds (batch 3: S −
  drawn). The guard's documented limit stays.
- **Holds spend the grant:** the walk's budget starts at G_b, so road works find S − (draws + holds) ≥ S − G_b ≥ min(D_r,
  E_r) of each good. A head's remaining need beyond the grant is NOT held this step; road works may use it, as the
  player's split says, and the waiting head's own line says so.
- **At b = 100** the budget is S: building work keeps its draws AND holds, and road works get only what is neither drawn
  nor set aside. "Building first" therefore covers set-asides.
- **A consequence measured by item 3** (§4): under the rule each unreleased head draws d and sets aside min(its remaining
  need, the budget less d), so the walk spends min(remaining need, budget); with G_b ≤ D_b the step's walk spends
  EXACTLY G_b. The allocation leg's checker asserts "build + holds = G_b" on every row and good; it held on 2,028
  (row, good) pairs of real reports.
- **Labels (counterfactual, one cap).** A head whose progress against the budget is below what it would get against A0 =
  min(physically left, grant − earlier draws) — that is, ignoring earlier holds — is labelled `HeldForEarlier`, naming
  the earliest earlier holder of the binding good. Otherwise `HeldForRoads` when the grant binds below the physical
  holding; and `Unobtainable` only when the physical holding alone (ignoring set-asides and the grant) cannot move the
  head and the crafted input is unreachable — both ways (fix round 1). A head the earlier set-asides do NOT hold back is
  labelled with the A0 resolution's binding good (T1b fix round, display only). Cause order: EmpireHasNone,
  HeldForEarlier, HeldForRoads, TakenByEarlier, Short.

### §1.7 Road works in the same step

- In the shipped pipeline road development runs after construction (pipeline position 22 against 10) and sees NEXT
  stocks as construction left them. Only colonisation acts in between, and it moves grain only (#23 asserts that no
  project input and no road class material is a good colonisation moves).
- **Charge-time holding** (`RoadDevelopmentQuery.SetAsideWithin`) = Σ payers' stock − the set-asides of every purse
  whose holder set lies within the payers. An Empire key counts as within the payers iff every place of its purse (the
  places it controls on PREV, less any place another Empire's purse owns: `EmpirePursePlaces`) is a payer; a
  single-settlement key −1 − s counts iff s is a payer.
- **Donors.** A donor in a single-settlement purse gives at most its stock less its own set-aside
  (`EmpireStockpile.Draw(…, keep)`, per-holder floors). Empire-purse set-asides are fungible, so their cap applies to
  the aggregate.
- **Unchanged:** ADR-032's min-ratio charge and its nearest-first donor order. ADR-032 carries a dated note.
- **With the rule off** `SetAsideWithin` returns 0 whatever the table holds (round 2), so the rule off is batch 3 in ANY
  pipeline order, for both purse kinds (#22's rule-off rows, including the single-settlement row added in v4).
- **Order is data** (§11): road development reads the table as it stands when it runs. Only the shipped order is
  pinned.

### §1.8 Invariants, asserted at the end of every step with the rule on

A violation throws "construction: … (the D-055 hold invariant).". For every purse key and good:
- **(i)** Σ holds ≤ the purse's holding after construction's draws: no fictitious stock.
- **(ii)** Σ draws + Σ holds ≤ the starting budget, and every resolution budget is ≥ 0: no double counting.
- **(iii)** every holder is an unreleased head.
- **(iv)** no stock is negative (equivalent to the Ledger's own overdraw refusal, recorded as an equivalent mutant).
- **(v)** the published table equals the per-key sums, and every good is in the registry.
- **(vi)** the first unreleased head of each purse is resolved against the whole starting budget: no hold ever blocks
  the oldest head, and no cycle can form.

The property test (#20: two Empires × random splits × random heads, some without builders, × road orders on disjoint
routes) also checks conservation, D_r == SimulateCharges(S), draws + holds ≤ G_b, S − d − h ≥ min(D_r, E_r), the
forecast draw and road charge equal to the actual, and the oldest head unblocked, at every step and good. It replaces
batch 3's `BuildingDemand == offDraw` check (a moved pin, §7).

### §1.9 What the player is told (track T1b; one term: "set aside")

A project's reservation is "SET ASIDE" for it. "Held" keeps its two older meanings — possession ("held across your
settlements") and the building/roads split ("… the rest is held for road works …") — and never names a reservation; a
test pins that the two causes never share a verb. Every new or changed string is conditional on
`Reservations.Enabled`: with the rule off the UI says batch 3's words exactly (the control of every re-pinned text pin).
- **The waiting head** (was "waiting for <good>: not enough yet"): "waiting for stone: [N of ]the empire's M are set aside
  for Alpha's Granary[ and other projects], ordered earlier (turn T), ahead in the building order - it has no builders
  (give Building a share in Labour at Alpha, or cancel it)". "ordered earlier" only when the holder's EnqueuedTurn is
  lower; T = EnqueuedTurn + 1, omitted for −1; an equal key reads "ordered the same turn, first in the building order";
  two migrated entries read "…, first in the building order". The holder's own reason and remedy come from its own
  status (no builders; a labour pace; only as far as one good allows — "(cancel it to free them)"; viability; another
  good short). Where the heads ahead also draw this turn the line adds "and N are used this turn by projects ahead of
  it".
- **The holder's own row:** "sets aside 22 timber and 11 stone while it waits; projects ordered later wait for it"
  ("… for the rest of its work; …" when it advances this turn), and "; the split lets road works use the rest" when the
  Empire's split, in force or ordered this turn, is below 100.
- **The project card** reads availability through the walk (a queued head: the purse less the earlier heads' draws and
  set-asides; a would-be new order or a non-head entry: less every set-aside) and names the holders ("; 22 set aside
  for Alpha's Granary, ahead in the building order"). The claims note's parenthetical "(this does not set it aside: …)"
  stays for a claim nothing sets aside, reads "(N of it is not set aside: …)" for a head's claim set aside in part, and
  is dropped for a fully set-aside claim.
- **The Build and Found plates, Blocker and BlockerSeenBy:** "will wait for: 40 timber (empire has 60; 22 set aside for
  earlier projects)"; a queued head's blocker also counts the earlier heads' draws, named apart ("5 used this turn by
  earlier projects"). A new order held behind a set-aside: "… ordered - it is first in line, but at End Turn it waits
  for timber set aside for earlier projects; …".
- **The roads preview and blocker** count what building work SETS ASIDE as well as what it draws ("after what building
  work uses or sets aside, about N of the M timber needed is left: …") and name the projects ("22 timber is set aside
  for Alpha's Granary; road works may not use it"). The road blocker and the basic preview read the coming step's walk
  with the turn's pending orders (`SetAsideQuery.StepTable`), over `EmpirePursePlaces`.
- **The EMPIRE view** (Realm block): "Set aside for projects: 22 timber, 11 stone" — the viewer's OWN purse only.
- **The split's default line:** "Building first (default): road works get only what is left after building work uses
  or sets aside what it needs." **RULE CHOSEN:** the design's fragment "road works get only what building work uses or
  sets aside", read literally, inverts the rule; the fragment is kept with the rule's meaning.
- **Visibility (P7):** hold notes, the priority line, previews and the summary read the viewer's own purse only; in a
  world with two control rows for one place, notes are read only where the settlement's purse is the viewer's own
  (`SetAsideQuery.ViewersOwnPurse`). Pinned by `KnowledgeLeakTests.AForeignHeldHead_YieldsNoHoldText_WhileTheHoldersOwnBooksNameIt`
  and `ConstructionHoldsUiTests.ADoublyControlledPlace_TheHigherControllersCard_NamesNoForeignHead_TheOwnersCardDoes`.
- No '%' in any new line in the numeral-free eras (A1–A3). Readability roles: Body for the waiting status, Caption for
  the card note and the roads line; the holder line at the Second role in the soft ink.
- **No new control, so no new gate row** (`docs/m5-playability-gate.md` dated notes on §3.14 and §3.15).
- Tests: `Sim.Ui.Tests/ConstructionHoldsUiTests` (incl. `.FixRound`), `Sim.Tests/State/SetAsideReadingTests` (incl.
  `.FixRound`), `KnowledgeLeakTests.Holds`, the re-pinned `ResourceAllocationUiTests`.

### §1.10 Details the design left open, and the rule chosen (smallest deterministic rule; recorded)

1. HoldsWalk, the release predicate, the purse key and the invariants live in a new partial file,
   `ConstructionQuery.Holds.cs` (the design named `.Incremental`; `MaterialCause` and `HeadStatus` stay there).
2. Released heads are still resolved (for the UI's reason) but never draw or hold.
3. With the rule off each head resolves against its Empire's grant less earlier draws: batch 3's arithmetic and labels.
4. `HeadStatusReport` gains optional trailing fields (Holder, SetAsideAhead, SetAside by good id, Release), so callers
   compile unchanged.
5. The v32 migration note keeps batch 3's flat-terrain sentence after the design's text (a recorded departure, §2).
6. `SessionSchemaNotice` is not routed through `CanonicalSchema.IsReadable`: sessions are replayed, not loaded, and a
   trace hash covers exactly the schema that wrote it, so it keeps schema equality (a recorded departure).
7. **The battery's road save point.** The save/load battery's script gains one order — the road order once the
   university stands — re-issued each turn until one pays, rather than moving the existing turn-12 order. MEASURED:
   moving it to 21 changed the both-off arm's batch-2 tuple (1, 27, 2, 34) → (1, 27, 2, 38), and to 22 changed the
   WS1-off and both-off arms 34 → 26; either would have contaminated the attribution chain. With and without the added
   order the rule-off arms first differ at world 24 (pre, flatPre) and 33 (ws1Off, bothOff), before their later save
   points, which were re-measured unchanged.
8. The double-control rules of §1.2.
9. The promoted-slot rule of §1.3 (documented, measured identical before and after round 2).
10. "The split lets road works use the rest" is shown when the split is below 100 in force or ordered this turn.
11. A card's Met for a crafted good reads the larger of the claims deduction and the set-aside deduction, never their
    sum (a head's claim IS its set-aside).

### §1.11 What it costs (MEASURED; scratch-only)

`sim bench --founded --turns 60` (no orders, no queue head), interleaved, 3 rounds, shared 4-CPU host (load 2–16):
the CONSTRUCTION phase went from a median 8.96 / 7.87 ms (seeds 42 / 1, base-b3) to 21.66 / 16.48 ms (T1a fix round),
about 0.15 ms a turn more, and its allocation from 18,848 to 31,400 bytes over 60 turns. The totals moved inside the
noise (17.5 / 18.6 s against 18.1 / 16.5 s; pathbuild, untouched by T1a, moves by as much). A holds-heavy 150-turn
workload (the T1a verifier's probe, `t1a-out/verify-4/bench/heavy-summary.txt`, on a pre-fix T1a revision — INFERRED
`t1a-final`, the revision that verifier checked) went from 50 to 93–100 ms in construction, and its construction
allocation from 0.74 to 6.09 MB over the 150 turns, about 36 KB a turn more. Negligible against pathbuild, but not "no
change". The UI runs one more walk per HeldForEarlier line and per Roads contributor (not benchmarked).

### §1.12 The adversarial tests

The design's adversarial list #1–#25 (the numbers the class header and this record use), each with exact long figures,
progress values that are not multiples of 1,000, a premise guard and a named semantic mutant kill. In
`Sim.Tests/Systems/ConstructionReservationTests` (77 cases in the merge stage's run) unless named:

| # | Case | Test |
|---|---|---|
| 1, 2 | Q17 at p > 0 and at p = 0 | `Q17_AnOlderPartBuiltHeadWithoutBuilders_…`, `Q17_AnOlderHeadAtProgressZero_…` |
| 3 | an older head at a labour pace | `AnOlderHeadAtALabourPace_KeepsTheRestOfItsNeed_…` |
| 4 | missing materials | `AnOlderHeadShortOfOneInput_HoldsItsOtherInputs_…` |
| 5 | the held units sit in the younger head's own settlement | `TheHeldUnitsSitInTheYoungerHeadsOwnSettlement_…` |
| 6 | viability and unobtainable waits keep holding | `AViabilityWaitAndAnUnobtainableWait_KeepHolding_…` |
| 7 | release (a), (b), (c) | `AnInvalidReservation_IsReleased_TheYoungerHeadBuildsInThatStep` |
| 8 | cancel of a part-built head | `ACancelledPartBuiltHead_ReleasesItsHoldInTheDeliveryStep_NoRefund_NoGhostRow` |
| 9 | completion | `ACompletion_FreesTheLeftoverInTheSameWalk_AndThePromotedSlotHoldsFromTheNextStep` |
| 10 | re-enqueue | `AReEnqueue_IsANewEntryKeyedAtItsStampTurn_BehindEveryOlderHead_TurnExact` |
| 11 | revolt | `ARevolt_MovesTheHoldToTheNewControllersPurseNextStep_AndTheAiDoesNotCancelAHeldHead` |
| 12 | purse off | `PurseOff_HoldsAreKeyedPerSettlement_…` |
| 13 | a Controls-less world | `AControlsLessWorld_PublishesSingleSettlementHolds_AndRoadWorksRespectThemDonorByDonor` |
| 14 | three settlements of one Empire and a fourth Empire | `ThreeSettlementsOfOneEmpireAndAFourthEmpire_ThePursesAreIndependent` |
| 15 | a cross-good chain | `ACrossGoodChain_EachHeadIsHeldBackByTheOneBeforeIt_AndNamesIt` |
| 16 | the split (b = 100, 50, 0; the guard; an explicit 100 row; the review's counterexample) | `TheSplit_HoldsSpendTheGrant_…`, `TheSplit_ANeedBeyondTheGrantIsNotHeld_…`, `TheStarvationGuard_…`, `AnExplicitHundred_…`, `TheReviewCounterexample_Purse10Cost30LabourOneThird_…` |
| 17 | no fictitious stock | `AConsumerLowersAHeldGood_TheNextHoldNeverExceedsTheHolding_…` |
| 18 | deadlock freedom | `AnOlderHeadThatNeverGetsBuilders_DoesNotFreezeThePurse_…` |
| 19 | tie-dense | `TwentyTiedHeadsAcrossTwoInterleavedPurses_…` |
| 20 | the property test | `ThePropertyTest_TwoEmpires_RandomSplitsHeadsWaitsAndRoads_EveryInvariantEveryStep` |
| 21 | HoldsWalk ≡ step | `HoldsWalkOnPrev_EqualsTheStep_AndHeadStatusAgrees_HeadByHead` |
| 22 | the edge order (§11) | `TheEdgeOrderIsData_…`; `TheV33Migration_IsExactInTheShippedOrder_…` |
| 23 | no colonised good is a construction or road input | `NoColonisedGood_IsAConstructionOrRoadInput` |
| 24 | the battery variant | `IntegratedSaveLoadBatteryTests.TheBatteryVariant_AYoungerGranaryWaitsBehindTheUniversitysSetAside_TwentySteps_AndUnderPreReservationsBuildsAtOnce` |
| 25 | the switch off is batch 3 through V33 | `ReservationsLayoutAttributionTests.FoundedGoldenSeed42Turn300_UnderPreReservations_IsBatch3ThroughV33`, `…DrivenGolden…`; `IntegratedSaveLoadBatteryTests.TheSavePoints_UnderPreReservations_AreBatch3s_AndNoWorldHoldsARow` |

Plus the labels (`TheSplit_TheYoungerHeadBlockedByAnEarlierSetAside_IsHeldForEarlier_AtEveryShare`, `…_IsHeldForRoads`,
`TwoEarlierHoldersOfTheBindingGood_…`, `TheNamedHolder_IsOfTheWaitingHeadsOwnPurse_…`, the two Unobtainable cases), the
invariants (`TheHoldInvariants_PassOnTheWalk_AndNameEachBreach`), the promoted slot, the extinct Empire and the
double-control cases named in §1.2–§1.5.
`Sim.Tests/State/ConstructionHoldsSchemaV34Tests` (21 cases) and `ConstructionHoldsV33FixtureTests` cover schema v34 and
the migration (§2).

Mutant record (semantic kills; each bounded at 4× its filter's clean time; no hang): T1a step 3 10/10; fix round 1
16/16; round 2 11/11; v4 4/4 (N1, N3, N5 and A1 "road development reads PREV's holds"). T1b: 33 + 15 + 26 killed
across its steps and fix round.

---

## §2 Schema v34 and the migration (summary; ADR-040 is the contract)

- **One appended table, section 61, `ConstructionHolds`:** `ConstructionHoldRow(int Purse, int Good, long Units)`, 16
  bytes, after ResourceAllocations. One row per (purse, good) with Units > 0, ascending (Purse, Good). Construction
  clears it at the start of every step whatever the switch, and publishes it only with the rule on. No existing row
  changed width; no order kind was added.
- **`CanonicalSchema.Version` = 34.** The readable versions are the SET {32, 33, 34} (`CanonicalSchema.IsReadable`); a
  v33 stream stops after section 60 and a v32 stream after section 58, and every missing table is empty. Every other
  version is refused with the D-008 message. `OldestReadableVersion` stays 32 (no gate compares with it).
- **`Snapshot.Load` refuses** unsorted or duplicate (Purse, Good), Units ≤ 0 and Good < 0 ("save is corrupt: …
  (schema v34 invariant)"). The goods-registry bound is the end-of-step invariant (v).
- **Never an input to construction.** In the shipped pipeline order the table is also never read before construction
  rebuilds it, so a v33 save migrated to v34 continues exactly as a v34 save of the same state (§11 for other orders).
- **The migration notes** (`sim` loader, one stderr line):
  - v33: "migrated from save schema v33: ConstructionHolds was added empty; the hash printed is the v34 hash
    (SchemaProjection.V33 gives the v33 build's)";
  - v32: "migrated from save schema v32: the three tables added since v32 (ConstructionProgress, ResourceAllocations,
    ConstructionHolds) were added empty; the hash printed is the v34 hash (SchemaProjection.V32 gives the v32 build's)",
    followed by batch 3's sentence "Its terrain was regenerated flat (no v32 world was a cylinder) and its queued
    entries are at progress 0, ordered as enqueued on turn -1." (a recorded departure from the design's text).
- **The v33 fixtures**, recorded by the BATCH-3 build (base-b3's CLI, stamped `b3-final-v9-0f8f45c9`):
  `Sim.Tests/Fixtures/v33-seed42-turn2.civsnap`, `v33-fixture-orders.bin` and `v33-session-q17/`, with
  `README-v33-fixture.md` (the commands, the order records, the printed hashes, every file's sha256). The order log
  forms the Q17 shape: s0's granary loses its builders at turn 1 and s2's younger granary overtakes it.
- **The comparators** (`ConstructionHoldsV33FixtureTests`; every one steps with the shipped executor): (a) the loaded
  save's v33 projection equals the batch-3 hash; (b) the loaded world equals an in-process `PreReservations` run of the
  same orders at the same turn; (c) continuation equals that run's continuation, turn by turn; (d) the same after a v34
  re-save and reload; (e) anti-vacuity: the continuation differs from the all-PreReservations continuation at W = 3,
  where s0 sets aside Remaining(20, 700,000) = 6 stone and s2 draws 3 stone where batch 3 drew 9. The v32 fixture gets
  the same comparators (§5, FX-V32_*).
- **The UI resume of a v33 session** takes the schema-mismatch path (D-008): replayed under batch-4 rules with the
  four-line notice, the trace not checked (MEASURED on the fixture: the replay first differs from the v33 trace at
  turn 3, the first step in which a hold binds). `sim inspect` of a v33 session prints NotChecked(33) and exits 0.
- **Records:** `TopologyFlags.FirstCylinderSchema` stays 33; `SaveFiles.WorldgenFor` keeps v32 flat and reads the
  trailer for v33 and v34.

**The layout move.** Every hash golden moved for the layout alone (an empty section-61 count prefix of 4 bytes). Each was
re-pinned only after `HashAtSchemaV33` reproduced its batch-3 value, and each has a `*_MovedForTheV34LayoutAlone`
companion in `ReservationsLayoutAttributionTests` (§7).

---

## §3 Item 2 — W1 F7 closed: turn-exact feature attribution on the shipped cylinder (track T2)

**The template, every rig** (new `CylinderAttribution*Tests` classes; the flat legs are unchanged):
- **World:** both arms found and step `worldgen.json` as shipped, `Terrain.WrapX` asserted on every arm, the same seed,
  orders and pipeline; world 0 identical.
- **Arms:** ON = the shipped config; OFF = one switch off — a NON-chained single-switch twin (`SingleSwitchTwins`:
  `PreForagerOnly`, `PreTimeAveragedHarvestOnly`, `PreRecipeKnowledgeOnly`, `PreTradeKnowledgeOnly`,
  `PreAiFoundingOnly`, `PreTaxGateOnly`) or a single-layer `TestConfigs` helper.
- **Exercises the feature:** the first action is read INDEPENDENTLY of the hash (the books, state, the order log, an RNG
  stream) and pinned turn-exact under a dated MEASURED block; the semantic reading runs BEFORE the hash-derived turn,
  which must equal it.
- **Separates the feature from topology:** the terrain is held constant across the arms, and the set of tables that
  differ at the divergence world (the write set) is measured and pinned, containing no topology table (Catchment*,
  Network*, PathProgress) unless the feature writes one; AI order logs are equal before the action.
- **Detects a disabled or broken implementation:** each rig's DISABLED mutant (the switch ignored or the system
  returning early) and BROKEN mutant(s) were run, each needing a SEMANTIC first failure (no kill rests on a hash).
- **Deterministic:** a second ON run, or an existing pinned run of the same world.

**The inventory and the rigs** (MEASURED on T2's tree):

| # | Feature | Rig (class) | First action (independent reading) | First different | Write set |
|---|---|---|---|---|---|
| 1 | intra-polity grain relief (D-053 §2) | dev starved; driven 1024 (DevStarved, Driven); the polity boundary on the dev AI world (DevStarved); the seam half (Seam) | grain books: 3; 5; 3 (the AI's stores and books equal the relief-off twin's) | 3; 5; 3 | {ConsumptionDeficits, GoodStocks, LedgerFlows} |
| 2 | granary effects (D-053 §3) | farm half: dev AI shared arm (DevAi), a granary stands 8, acts 355; famine half: dev AI, s0 starved, granary ordered at 3 | 355 (farm); 4 (famine) | 356; 5 (world 4 identical: the PREV pin) | {GoodStocks, LedgerFlows}; {Buckets, LedgerFlows, SettlementVitals} |
| 3 | construction purse (D-053 §1) | dev AI (DevAi) | first Enqueue ON 7, OFF 9; built in world 8 ON | 8 | {ConstructionLabor, GoodStocks, LedgerFlows, Structures} |
| 4 | AI founding (S1c) | dev AI (DevAi) | FoundSettlement stamped 71; one more settlement in 72 | 72 | {Buckets, ClassStates, Controls, Deposits, GoodStocks, Grievance, LedgerFlows, SettlementVitals, Settlements, SmoothedAttractiveness} |
| 5 | tax Age gate (H2) | AI 1024 shared arm (Ai1024) | gate-off SetTaxRate 336 at Age 2 (gate Age 3) | 337 | {TaxPolicies} |
| 6 | forager (R4) | founded 1024 (Founded) | 12/12 wild-food settlements on world 0; each harvests less in 0 → 1 | 1 | {GoodStocks, LedgerFlows} |
| 7 | time-averaged harvest (D-051 1(b)) | founded 1024 (Founded) | dt 3,600 days; each of 12 weather streams draws 44 = (1 + 10) × 4 ON, 4 OFF; the point value on 0/12 rows ON, 12/12 OFF; both steps equal a test-side recomputation bit for bit | 1 | {HarvestWeather, RngStreams} |
| 8 | recipe knowledge (R1) | founded 1024 (Founded) | pottery-firing gated at 12 sites; produced OFF only in world 1 | 1 | {GoodStocks, LedgerFlows} |
| 9 | trade knowledge (R2a) | driven 1024 (Driven) | ungated first TradeFlows row 6 | 6 | {GoodStocks, TradeFlows} |
| 10 | R2b weather geography | founded 1024 (Founded) | 66/66 pairs coupled on world 0; the step 0 → 1 equals the recomputation (kernel exp(−d/range)) bit for bit; road perturbations move 0/12 rows ON, 12/12 under a travel-cost kernel | 1 | {HarvestWeather} |
| 11 | R2b unrest / Dignity | founded 1024 (Founded) | 12 Dignity rows in world 1 ON, 0 OFF | 1 | {Grievance, NeedSatisfaction} |
| 12 | R2b Age-military | founded 1024 (Founded) | warband 301 not a counted identity: SupportingMet 0 ON, 1 OFF | 1 | {AgeEligibility} |
| 13 | revolt and order refusal | founded 1024 + the CLI player log + AI producer, 156 turns, a twin pair (Revolt) | revolt world 37 (new AI polity 2); farming 0.6 at 4, 0 at 34, and 0 at 64/94/124/154 (refused) | n/a (code mutants) | n/a |
| 14 | WS1 absence on the AI world | AI 1024 shared arm vs PreIncrementalConstruction (Ai1024) | no queue row survives a step; no progress or hold row (339 turns) | never (to 300) | — |
| 15b | research non-leak; the governance reach; the Age layer | founded and driven cylinder, `Unarmed()`, two-run strip equality (Separability) | a node completes in world 11; 11 reaches < 1; the Age row in world 1 | — | ⊆ the layer's own tables every turn |
| 16 | reservations (this record, §1) | the dev Q17 player rig vs PreReservations (Reservations) | first hold row world 1; first V33-projected divergence world 3: s2 150,000 vs 450,000, s0 sets aside 6 stone, s2 HeldForEarlier naming s0 | 1; 3 (through V33) | {ConstructionHolds}; through V33 {ConstructionLabor, ConstructionProgress, GoodStocks, LedgerFlows} |
| 17 | relief absence | founded 1024, 300 turns (ReliefAbsence); AI 1024 shared arm (Ai1024) | relief never moves grain (300 / 339 turns) | never | — |
| 17b | granary-effects and construction-purse absence (the cylinder twins of two flat-only founded controls the first inventory missed) | founded 1024, 300 turns, ReliefAbsence's shared ON arm | no granary stands and neither factor differs from 1; no order, queue row or structure in 300 turns | never (each world 300 = `0f29efc7…`) | — |

**Observability samples** (additive cylinder twins; the flat originals unchanged): the DECAY sample on the revolt rig
(156 turns reconcile; decay from turn 15; 19 turns, 133 dwellings) and the EXPLAIN STARVED rig on the dev cylinder (first
deficit world 3 = 0.7842608482471194; 472 of 3,671 grain eaten).

**Seam exposure, per rig.** Every seed-42 and dev rig has **0 same-polity seam-crossing settlement pairs and 0 wrapped
donor orders** (asserted per rig; #10 counts all 66 pairs) — except the EXPLAIN STARVED sample above, whose exposure is
INFERRED 0/0 and not asserted (T2's notes; `CylinderExplainStarvedSampleTests` has no such assertion). This is a STATIC
count over the settlement sites, not a count of the wrapped-dx answers a run computes: those do occur on these rigs (an
instrumented `MapTopology` measured 76, 76 and 144 wrapped-dx answers in the founded-1024 steps 0 → 1, 1 → 2, 2 → 3,
from the shared pathfinding heuristic and the colonisation water search). So the five distance-reading features got SEAM
RIGS (`CylinderAttributionSeamTests`), each killing a "topology-blind" mutant that every seed-42 / dev rig lets through:

| Feature | Seam rig | Exposure | Topology-blind mutant: seam rig / main rig |
|---|---|---|---|
| relief donor order | SeamWorld; the across-seam sibling is nearest the short way AND has the highest id, so the cylinder order [0, 2, 1] differs from raw dx and from id order, both [0, 1, 2] | 2 crossing pairs, 1 wrapped order | raw dx and id order KILLED ("Expected: 0 Actual: 392"; the books: grant 3,170, across 1,983, interior 1,187) / SURVIVED |
| purse donor order | seed 16, a granary at s6 | 5 pairs, 4 wrapped orders | KILLED (the stone donors) / SURVIVED |
| weather geography | seed 16, the weather step under 8 rotations round the cylinder; a one-settlement move changes 12/12 rows; the step equals the recomputation | 5 of 66 pairs | KILLED / SURVIVED |
| AI founding | SeamWorld: the AI on the east edge founds on the west piece (turn 35) | the founding reach crosses the seam | KILLED / SURVIVED |
| trade | SeamWorld.TwoSides: the ungated first flow (world 34) crosses the seam | 1 pair | KILLED / SURVIVED |

**Limitations, recorded:**
- **AI founding and trade read distance only through the SHARED lattice.** Their seam halves prove that the traversal
  lattice / Pathfinder wraps for them (a topology property); neither feature has a policy-level dx reading to mutate, so
  the halves cannot tell a topology-blind feature from topology-blind pathfinding (a non-wrapping lattice fails them
  with the same messages).
- **Seam halves, not seam twins.** The turn-exact ON/OFF attribution is each feature's main rig on the canonical worlds;
  the weather seam rig is metamorphic (rotation invariance), not an ON/OFF pair.
- **TB4 approximates a topology-blind founding policy:** it drops seam-crossing nodes from the flood, which on SeamWorld
  is the flat flood (T2's records).
- **The governance reach** (#15b) reads a distance (the capital's travel cost through the catchment's wrapped
  Pathfinder) and has no seam rig (0 exposure on its worlds); the catchment's seam semantics are `SeamWorldSystemTests`'.
- **Read no distance:** revolt and refusal, unrest, Age-military, WS1, reservations, the forager, the time-averaged
  harvest, recipe knowledge, the tax gate and both granary halves.
- **Equivalent mutants** (no rig can kill them): a granary farm factor or a forager predicate read from NEXT at the
  production seam (production runs before every system that could change them); "the founding policy alters an earlier
  order" (the AI never issues another order in a founding batch on any measured world); a DISABLED feature on an
  ABSENCE rig (#14, #17 — each killed on its feature's positive rig instead).
- **#16:** the HeldForEarlier label is pinned on the binding world W = 3, not on its PREV (the estimate reads
  EmpireHasNone on world 2 because the stone is produced within the step) — a property of the estimate, not a defect.

**#15a — the pure layout byte strips: no cylinder twin, each with its reason.** A fact is a pure layout strip when its
whole question is "does the stream minus a set of tables equal an older stream byte for byte", and the stripped tables
are empty in the run or rows no system reads back. It reads no terrain, distance or topology, and its literal is frozen
history; a cylinder twin would re-measure the same bytes. The behavioural facts among them are #15b or rigs #6–#12.

| Facts (`IntegratedPinAttributionTests` unless named) | Why no cylinder twin |
|---|---|
| the toy-world trailers (`GoldenHashSeed42Turn200_MovedForTheM4SchemaAlone`, `…V25TrailerAlone`, `…V26ResearchTrailerAlone`, `…V28AgeTrailerAlone`, `…V29TransportTrailerAlone`, `…V30GovernanceTrailerAlone`, `…V31InstitutionsTrailerAlone`, `…V32LevyTrailerAlone`; the toy halves of `IncrementalConstructionAttributionTests` and `ReservationsLayoutAttributionTests`) | the toy world has NO terrain: there is no topology to twin |
| `FoundedGoldenSeed42Turn300_MovedForTheM4SchemaAlone`, `FirstReignTurn40_MovedForTheM4SchemaAlone` | strips the M4 tables back to the v22 stream; the literal is the pre-M4 flat world |
| `DrivenGoldenSeed42Turn300_SeparatesTheSchemaMoveFromTheBehaviouralOne` | the layout half is the same strip; the behavioural half (the T4.19 capacity-floor fix) is a historical literal comparison |
| `…MovedForTheDisasterLayoutAlone` (three) | hazardPerYear = 0: the stripped rows are never-drawn disaster RNG streams and the empty disaster table |
| `FirstReignTurn40_MovedForTheResearchLayerAlone` | no research target is set: the research tables are empty; the behavioural non-leak is #15b |
| `FirstReignTurn40_MovedForTheAgeLayerAlone`, `…GovernanceLayerAlone` | the behavioural facts are #15b; first reign is a recorded flat fixture replay |
| `…MovedForTheTransportLayoutAlone` (three) | no run develops a road: both transport tables are empty (asserted) |
| `…MovedForTheInstitutionsLayoutAlone` (three) | no run founds a university or completes a project: the tables are empty |
| `…MovedForTheV32LevyLayerAlone` (three) | no run levies a tax: the levy table is empty (INFERRED from TaxPolicies empty, asserted) |
| `EveryOlderSchemaProjection_RefusesAWorldWithAV33Row_AsTheV32ProjectionDoes` | a property of the projection helpers on a hand-built row, not of any world |
| `IncrementalConstructionAttributionTests` v33 layout halves (founded, driven, AI) | the v33 tables are empty there; the AI world's behaviour half (WS1 never acts) is #14 |
| `ReservationsLayoutAttributionTests.*_MovedForTheV34LayoutAlone` | section 61 is empty on these worlds; the behaviour is #16 |

**D-054 §10 Q14 answered** ("Cylinder equivalents for the four flat re-rigs. Wanted?"). The instruction's "every other
feature currently covered only by flat-world rigs" answers it. Of D-054 §7's four kept-flat rigs: the
`FoundedHarnessTests` ordered twin's subject (revolt and order refusal) is rig #13 on the cylinder; the
`WorldReconciliationTests` decay sample and `ExplainRigs.Starved` have the additive cylinder samples above; the
`NonCanonicalSizeFoundingTests` CLI recipe stays flat with no twin, because the cylinder refuses non-stride sizes. The
flat originals keep their frozen literals.

**ADR-039 §8 corrected** (its file-end note, 2026-10-16): `IncrementalConstructionAttributionTests`' per-layer proof
founds `TestConfigs.DevWorldgen()`, the shipped worldgen at 256 px — the CYLINDER; only its golden reproductions are flat
or terrain-less. The run that note cites: `CylinderAttributionDevStarvedTests` passed on T2's fixed tree (`b4219b0d…`,
base-b3 + `t1a-final` + T2; full Sim.Tests 2,187 passed / 2 failed (the CR-020 pair) / 13 skipped of 2,202), and again on
the merged tree (§8).

**Mutant campaign** (T2's step-3 tree; one Sim.Core edit each, against its rig's class filter and the Seam class where
the feature has a seam half, bounded at 4× the filter's clean time, the original bytes restored): **59 runs, 50 KILLED,
9 SURVIVED, 0 HANG**. Every kill has a semantic first failure. The 9 survivors are the equivalents above and two
expected controls, each killed elsewhere where a kill is possible. The fix round's campaign (the books-based relief
pins, the weather recomputation, the time-average point value, the two absence twins) killed V1, TB1, V2, V2k, V3, V3b,
D10, TB3 (Seam) and G-ABS2 / P-ABS; G-ABS is equivalent on the order-less world and killed on DevAi.

---

## §4 Item 3 — the durable cross-process allocation leg (track T3)

**The ci.yml step "Allocation split, binding, cross-process (D-055)"** in job `determinism-xproc`, after the T0.9 step.

**Scenario S42-ALLOC-H** (`scripts/gen-sample-orders.py --allocation`; `--no-split` is the b = 100 control; `--drop
TURN:KIND` makes the turn-exact twins). Seed 42, founded, no AI Empire (the player rules all 12 settlements), 52 player
records (49 before T3 round 2 added the three granaries of the b = 70 batch). MEASURED windows on the batch-4 tree:
- **Research:** track_road first known in world 99, stone_dry in world 116 (road class 3 needs both and costs stone).
- **Competing heads (117–125):** b = 30 stamped 116 (P, the pre-split save); DevelopRoads every turn from 116; three
  granaries each at settlements 0 and 2 stamped 117. **X = 117** is the first binding step (stone S 64, E_b 19, D_b 40,
  G_b 19 = build 19; settlement 0's head served, settlement 2's waits — the building order); binding steps 117, 118,
  119, 121, 122. **Y = 120:** building's unused share used by road works (D_b 4 < E_b 18, road 58 > E_r 45).
- **The hold (the 1 % road phase):** settlement 4's granary (stamped 126) is labour-paced to 418,976 then 422,236 and has
  no builders from 127. **W = 128:** the granaries stamped 128 at settlements 0 and 2 are HeldForEarlier naming it
  (draws 13 and 0 of 20; 11 stone set aside ahead); the set-aside sits inside the 30 % share (G_b = E_b = 24). The
  b = 100 control serves both. **K = 130** is the save point: a part-built head at 422,236, `ConstructionHolds` (1,
  timber, 51) and (1, stone, 11) binding a younger head, a split row of 30, a route at 0.937. **Z = 131:** road works'
  unused share used by building work (D_b 31 > E_b 25, D_r 12 < E_r 59). **V = 132:** the part-built head cancelled
  (kind 11) and the project re-enqueued (kind 4) in one batch — the new entry keyed 132 at progress 0, behind settlement
  2's head of 128. Builders back at 134.
- **The split CHANGED, binding in its own delivery step (T3 round 2):** **b = 70 at 136, in one batch with granaries at
  settlements 0, 2 and 4** (kinds 12, 4, 4, 4, beside the turn's road order). In the step 136 → 137 (stone S 73, D_b 60,
  D_r 73) the new share binds: E_b = G_b = 51 and building work draws 51 (20, 20 and 11; settlement 4's head stops at
  550,000, HeldForRoads). The kind-12 twin (the same run from save_K without that order, b = 30) sees the same S, D_b and
  D_r and draws 21 (20, 1, 0): E_b = G_b = 21. Timber 102 against 42. In 137 → 138 settlement 4 draws the 9 it still
  needs; no head from 138. **H = 140**: the granaries built at settlements 0, 2 and 4 number (6, 6, 2). No AI polity,
  revolt or colony by 140. Binding steps of A: 117, 118, 119, 121, 122, 128, 129, 130, 132 and 136.
- **What the step does:** generate four logs and `sha256sum -c` them against literals; A0 (founding → 116, save P); A
  (`--from-save` P → 140, `--split-report`, save K); A′ (the same with no report: `cmp` of the hash log and save_K —
  observer neutrality); B (`--from-save` save_K → 140, `--split-report`: B's log and report equal A's tail); the control
  (`--from-save` P, no kind 12); `sim replay` = A0 + A; `sim hash` save_K = line 130; `sim diff` of save_K (A vs A′):
  identical, with a NON-EMPTY `ConstructionHolds` table; the issue-turn saves (132 by a run from save_K, 136 by B) hash to
  their lines; the kind-11 twin equal through 132 and different at 133, the kind-12 twin (now with `--split-report`)
  equal through 136 and different at 137; `split-check` on A (`--expect-hold-at 130`), on A with `--control`, on B
  (`--expect-hold-at 131`), and on A with **`--twin alloc-t12.rep --order-at 136:70`** (PT, below).
- **So the leg proves behaviour, not acceptance:** scarcity (build + holds = G_b < D_b), surplus both ways, competing heads
  in key order, roads, a binding set-aside, a cancel and a re-enqueue, a split CHANGE that binds in its own delivery step,
  and save/load across processes (the hold table is asserted non-empty in the process that wrote save_K, by the processes
  that read it back, and in the process that continued from it — closing the T1a verifier's finding that no
  cross-process leg ever published a hold row). Every report row's split and walk are re-derived by the judgement (P11,
  P12), not read back from the rule.
- **Pinned in process:** `AllocationCiLegTests` (the generator literals, the four sha256, the 52 / 50 record shapes and
  the 136 batch, the step's exact ordered command list including the twin's report and the PT command, the binding
  steps, every premise above, and P11 / P12 on A, B and the control) and `AllocationScenarioTests` (the split at all 24
  steps, the heads 126–137, the worlds' own `ConstructionHolds` and `ConstructionProgress`, the kind-11 and kind-12 twins,
  `Kind12_ChangesBuildingWorksDraw_InItsOwnDeliveryStep_TheTwinDrawsTheOldShare` (both rows of step 136 turn-exact, the
  heads' draws, the build difference = the E_b difference, PT on the in-process pair), a Snapshot round trip at K, and
  the CLI continuing the in-process world K to the same hashes and report). No world hash is pinned anywhere in the leg
  (MEASURED, not pinned: world 140 `be3385a9…`, the control's `182991ab…`).

**The CLI** (ADR-040): `sim run --from-save SAVE` (continue a saved world in a new process), `--split-report PATH` (a
read-only observer's per-step JSONL), and `sim split-check REPORT [--control CONTROL] [--expect-hold-at K] [--twin TWIN
--order-at T:B]` (the judgement in tested C#, properties P0–P12, PK and PT; exit 0 pass, 1 failed with each property
named, 2 unreadable). **P2 is the identity "E_b + E_r = S, build + holds = G_b ≤ D_b, G_b ≤ cap, build + holds + road ≤
S"** (T3 fix round): the grant is spent on every row and good under the D-055 walk (§1.6). It catches an under-spend the
scarce form cannot see: T1a's rule mutated to count set-asides twice leaves the end-of-run hashes unchanged and fails the
judgement at step 117 → 118. The probe refuses a configuration with the hold rule off. T3 rounds 2 and 3 added:
- **P11, the split's arithmetic re-derived, every row and good** (round 2): from the row's own S, b, D_b and D_r, in the
  checker's own integer arithmetic (never the rule's functions): E_b = floor(S·b/100), E_r = S − E_b, cap = E_b + max(0,
  E_r − D_r), G_b = min(D_b, cap), D_r taken as 0 in a row that records the starvation guard; S, D_b, D_r ≥ 0 and b in
  [0, 100].
- **P12, the walk re-derived, every row** (round 2): the heads strictly ascending in the building order (EnqueuedTurn,
  SettlementId, Slot); per good, the walk starts at G_b (S at b = 100), each head is resolved against exactly what the
  heads ahead of it left, and each unreleased head draws and sets aside together min(its need, that budget) — T1a's rule
  per head — while a released head takes nothing. **Its division clause** (round 3): a head reported limited by
  materials (`Materials` or `Unobtainable`) must be short of some good, its budget for that good below its need (a
  theorem of T1a's resolver; it catches a walk that hands each head its budget but lets it only set aside, which keeps
  every spending total). The building-order comparison has a tie-dense test (24 heads, three insertion orders, each of the
  23 adjacent swaps failing P12 by name).
- **PT, `--twin TWIN --order-at T:B`** (round 2): the kind-12 order stamped T changed spending in its own delivery step —
  the report's row T has b = B and the twin's does not; the twin's rows before T are the report's byte for byte; at T
  both saw the same S, D_b and D_r of every good; the twin's own row T satisfies P1, P2, P3, P11 and P12; and building
  work drew a different amount of some good in the two runs. `--twin` and `--order-at` go together (usage error, exit 1).
- **P9** also holds the control's rows to P1, P2, P3, P11 and P12. Failure order: P0, P1, P2, P3, P4–P7, P10, P11, P12, P8,
  P9, PK, PT.
- **The parser** (round 3) refuses, exit 2, a report whose goods are not strictly ascending in id, in a row or in a head
  (a duplicated good used to pass every property), and names a missing or mistyped field and where it is (`goods[0]:
  missing field "holds"`, `heads[1]: missing field "limit"`). The heads' ORDER stays P12's to judge, with the row named.
- **`--from-save`, the founding AI count** (round 3): turn zero is regenerated with the FOUNDING AI Empire count — the AI
  polities holding a Capitals row (capitals are written only at founding and never removed; a revolt-born polity has
  none) when that number plus the player's one is below the founding settlements, else the roster's AI count — and
  `--ai-empires`, if given, must equal it, as in a run from founding; the refusal names revolt-born polities. A save made
  after a real revolt (seed 42, revolt at 37, save at 40, player orders stamped 41, one naming the revolted settlement)
  continues to the uninterrupted run's tail hash for hash. Pinned too (round 3): the up-front ADR-034 validation of a
  continued run (a player record stamped at or after K that the founding world refuses exits 2 with the uninterrupted
  run's message, before any step) and the K − 1 boundary of an emitted run log (accepted from the save at 15, last AI
  record 14; refused from the save at 14).

**MEASURED** (scratch-only; the step extracted verbatim from ci.yml and run LOCALLY with `bash -e`, because it cannot run on
GitHub in this batch — nothing is pushed): exit 0 in 3 m 03 s (T3 step 2) and 139 s (T3 fix round), every output
byte-identical between those two runs; on the re-rigged scenario exit 0 in 123 s (round 2) and 118 s (round 3), the
round-3 run's 26 outputs byte-identical to round 2's (its report B and twin report are the fixtures `k.jsonl` and
`k-no12.jsonl`); and on the updated integration tree (§8) exit 0 in 89 s, its outputs and its log byte-identical to
round 3's. CI-level mutants make the extracted step exit 1 with a named reason: "sim hash of save_K is not line
130" when the load drops the holds — B's comparison alone would have passed; "save_K holds no set-aside"; the generator
ignoring `--drop`: the sha256 check; CM04b (construction reads PREV's split rows — a split applied a step late; every
hash, save, replay and twin comparison passed under it first): `FAIL P1 … timber: building work drew 42 but the split
forecast 102` and `FAIL P2 … stone: … less than its grant G_b = 51`, PT failing too; V2-CEIL (E_b rounded up): `FAIL P11
… E_b = 104155, but floor(347182 x 30 / 100) = 104154`; CM06c (the settlement tie-break reversed): `FAIL P12 … out of THE
BUILDING ORDER`; the T3 verifier's shared-walk mutants MW1 (no head draws) and MW2 (only a purse's first head draws),
which passed split-check on report A before round 3: exit 1 at the step's FIRST split-check, `FAIL P12 … reported limited
by Materials, but its budget covered its whole need of every good`.

**Tests:** `RunFromSaveCliTests` 31, `SplitReportCheckTests` 41, `SplitReportRederivationTests` 31 (new in round 2),
`SplitProbeTests` 11, `AllocationCiLegTests` 12, `AllocationScenarioTests` 8 (134 in all; 89 after the fixer round, 115
after round 2). Mutants: step 1 46 killed; step 2 S01–S11 killed (every Sim.Core mutant by a semantic test); the fix
round's V16, F2a, F2b, F2c, CM13, CM13r and CM13h killed; round 2 31/31 killed (CM04b, V2-CEIL and CM06c in process; 24
checker-clause mutants, each by its named fixture test; S16 semantic, S17–S19 pin kills); round 3 13/13 killed by
semantic tests (the division clause's four, the parser's four, MX1, MX2 and the founding-count rule's three; T3's notes
say "14/14", but its results file lists these 13, each KILLED); no hang.

**Limitations, recorded:**
- The leg is reproduced locally only in this batch; 89–183 s measured on the shared 4-CPU host.
- **The starvation guard is not exercised by the leg** (T3 round 3): no step of S42-ALLOC-H is guarded (every report
  row has `"guarded":false`), so P11's guarded branch is judged on a doctored fixture row only, and removing the guard
  leaves the step's outputs byte-identical. The guard is pinned in process by six tests
  (`ResourceAllocationTests.StarvationGuard_…` ×2, `TheForecastIsTheStep_…ConservedAndGuarded` ×2,
  `ConstructionReservationTests.TheStarvationGuard_ReadsTheForecastOnWhatIsNeitherDrawnNorSetAside`,
  `ThePropertyTest_TwoEmpires_RandomSplitsHeadsWaitsAndRoads_EveryInvariantEveryStep`). The ci.yml step's comment says so.
  A guarded window was not added: it would move the scenario's pins for a rule already pinned semantically. That the probe
  writes D_r = 0 for a guarded row is INFERRED from `AllocationFor` and `SplitProbe.Rows` (T3's).
- `--from-save` cannot tell the founding AI count of a world founded with MORE Empires than settlements once a revolt has
  founded an AI polity (both kinds hold no capital); such a save is refused (`docs/queue.md`). This SUPERSEDES the
  fixer-round limitation that any save with a revolt-born AI polity was refused. The leg's premise (no AI polity, revolt
  or colony by H) is pinned.
- The scenario's figures sit on the hold rule, the research timings and settlement 4's labour pacing; a change to any
  of them fails `AllocationScenarioTests` / `AllocationCiLegTests` BY NAME (which premise drifted).
- Two cross-process sub-cells of the re-enqueue row (continuing from the issue-turn save 132, and from a save at 133
  holding the re-enqueued entry) were measured LOCALLY (PASS) and are not in the ci.yml step; in this scenario the
  re-enqueued entry's persisted key orders nothing after 133 (it is the only head until it is built in 135 → 136).
- D-054 §2's "Cross-process coverage. No lasting CI leg makes the split BIND" (and its `docs/queue.md` line) is closed
  by this leg.

---

## §5 Item 4 — the persistence and lifecycle audit: the matrix as closed (track T4, with T3's cross-process cells)

**Columns:** A — `Snapshot.Load` in process; B — the CLI loader (`SaveFiles.Load` in process, and `sim hash` / `sim diff` /
`sim replay` / `sim inspect` as real processes); C32 / C33 — the REAL v32 / v33 fixture migrated to v34; D1 — UI resume,
same schema; D2 — UI resume of an older-schema session (a flat one relabelled v32, or a v33 one); E — deterministic
replay, pinned on the REPLAYED run; F — cross-process.

**Test short names:** P1 Ln = `ConstructionLifecyclePersistenceTests`; UI n = `ConstructionLifecycleResumeTests`; BAT-x =
the battery's lifecycle variant (`IntegratedSaveLoadBatteryTests.Lifecycle.cs`); FX-x =
`ConstructionFixtureContinuationTests` (four arms: the older build's twin, the load, the v34 re-save, the re-save through
`SaveFiles.Load`); CLI-x = `CliConstructionLifecycleTests`; COL-x = `ConstructionResumeColonyAndBuiltTests`; ALLOC = §4's
ci.yml step.

**Cell values** (legend; added at the integration's fix stage, 2026-10-18, finding INT-L1-V1-L1-F1):
- **a test name**: the test that pins the row's state on that path, within any limit the cell states.
- **ALLOC**: §4's ci.yml step (run locally only, §9 item 7). **hash-only** is the CLI-hash standard of R2 × B: a real
  `sim hash` process loads a save made in that state and prints the run's own hash-log line for that turn; nothing else
  is asserted on that save.
- **N/A**: the state cannot exist on that path; the cell gives the reason.
- **gap**: not covered by a dedicated test; listed under "The recorded gaps" below, with an INFERRED risk note.
- **—** appears only in the last two rows, which audit one surface each (deferred ids; `sim inspect` of the v33 fixture)
  rather than a lifecycle state: the surface was audited on the paths its row names, and no claim is made for the others.

| Row | A / B (in process) | B (processes) | C32 | C33 | D1 | D2 | E | F |
|---|---|---|---|---|---|---|---|---|
| R1a waiting for materials, p > 0 | P1 L1 | **gap** (CLI-hash's s2 is materials-PACED) | N/A as a SAVED state (a v32 save holds no progress: `ConstructionProgress` is v33's section 59, and a migrated entry reads p = 0); after the load FX-V32_A takes the migrated head to 350,000 and completes it, but a p > 0 WAIT there is a **gap** | **gap** (paced) | UI 3 | **gap** | **gap** | **gap** |
| R2 waiting for builders, p > 0 | P1 L2; BAT-no-builders | CLI-hash | FX-V32_E (the migrated head waits at p = 0: p > 0 is N/A as a saved v32 state, as for R1a) | FX-V33_E, FX-V33_A | UI 2 | UI 5 | BAT-replay | CLI-replay, CLI-inspect |
| R3 structural stall | P1 L3; BAT-named-stall (world 8: a viability wait, not released) | **gap** | **gap** | **gap** | **gap** | **gap** | **gap** (not separately pinned) | **gap** |
| R4 head cancel, p > 0 | P1 L4; BAT-cancel | **ALLOC**, hash-only (`sim hash alloc-132.civsnap` = line 132: the save at the kind-11 issue turn V = 132, settlement 4's head at p = 422,236) | FX-V32_G (the promoted row-less slot sets aside its need in the delivery step and binds s2) | FX-V33_B | UI 1 | UI 7 (the shifted slot) | BAT-replay; COL deferred id | **ALLOC** (cancel at p = 422,236 in B, a process that loaded save_K; the kind-11 twin turn-exact); CLI-colony at p = 0 |
| R5 re-enqueue | P1 L5; BAT-cancel | **ALLOC**, hash-only (the same save: the re-enqueue is in V's batch) | FX-V32_F | FX-V33_F | UI 4 | **gap** (not separately required) | BAT-replay | **ALLOC** (in B's step 132 → 133 after the load, served behind an older head) |
| R6 completion next to the save | P1 L6 | CLI-colony | FX-V32_A | FX-V33_A | COL-exactly-once | **gap** | BAT-replay | CLI-colony |
| R7 binding split in force | P1 L7 (b = 0, 50, explicit 100); BAT-split | CLI-diff-split (non-binding row walked) | FX-V32_S | FX-V33_S | N/A: not reachable through the emitters | N/A, as D1 (and a genuine v32 session cannot hold a kind-12 order: kind 12 is schema v33's, ADR-039) | BAT-replay | **ALLOC** (b = 30 binds in A and in B; b = 70 delivered turn-exact by B, a process that loaded save_K, and binding in its own delivery step 136 → 137: 51 of 73 stone against the twin's 21, judged by PT) |
| R8 split changed at the save turn | P1 L8; BAT-split | **ALLOC**, hash-only (`sim hash alloc-136.civsnap` = line 136: B's save at the kind-12 issue turn: b = 30 in force; the b = 70 order, stamped 136, is in the order log, not the save) | FX-V32_S (binding); FX-V32_C | FX-V33_S (binding); FX-V33_C | UI 1 (non-binding) | N/A for a genuine v32 session (kind 12 is v33's); a v33 or relabelled session: **gap** | BAT-replay | **gap** (no process continues from a save made at the kind-12 issue turn: `alloc-136.civsnap` is only hashed) |
| R9 holds, binding | P1 L9, L10 (tamper: never an input to construction, §2); BAT-no-builders, BAT-named-stall | CLI-hash (populated); CLI-diff-v33 (zero rows on the v33 side) | FX-V32_E, FX-V32_G | FX-V33_E/A/C; T1a's migration comparators | UI 1–4 | UI 5; UI 6 (the v33 fixture) | BAT-replay; UI 6 | CLI-replay (the replayed worlds' v33 projection; binding); ALLOC (non-empty in three processes) |
| Deferred ids (ADR-034) | — | CLI-colony (accepted; refused without the colony, exit 2) | — | — | COL deferred id | COL-v33-refusal | COL `ReplayTo` 4 and 5 | CLI-colony |
| `sim inspect` of the v33 fixture | — | `ConstructionHoldsV33FixtureTests` (NotChecked(33)) | — | — | — | — | — | CLI-inspect of a v34 session (VERIFIED) |

**The binding premise.** Every hold assertion that CLAIMS binding has one: the same step under `PreReservations` differs,
or a control without the releasing order differs, and the younger head's exact draw is pinned on the loaded, resumed or
replayed world. Some assertions only check that a set-aside is PUBLISHED and claim no more (FX-V32_A, FX-V32_B,
FX-V32_A-builders, FX-V33_B, BAT's split-in-force step, CLI-hash).

**Findings of the audit:**
1. **A binding split IS reachable in the battery, on timber with track_road alone** (the battery cuts the player's timber
   to 20 and the university sets all of it aside, so a 30/70 split pays a road 215 timber in step 7 → 8 that the
   set-aside keeps with no split). The lifecycle orders form a battery VARIANT, so the scripted run's save points are
   untouched.
2. **A migrated (row-less, v32) head keeps EnqueuedTurn −1** through its first progress, a save and its completion; it
   orders first and sets aside its need while it waits for builders, binding a head ordered after the load.
3. **Exactly once.** A completion after a resume, and one before the save, are each counted once and stay once.
4. **The v32 fixture's batch-2 twin.** The PreD054 pair, run 40 turns with the fixture's orders and the AI producer,
   reproduces the batch-2 build's v32 save exactly (StateEquals, the hash), so the X1 comparators apply to it.

**The recorded gaps** (not covered by a dedicated test; every risk note is INFERRED; the list was completed at the
integration's fix stage, 2026-10-18, so that every **gap** cell above is here):
- **R1a** (a part-built head WAITING for materials, Δp = 0) on the CLI processes, C32 after the load, C33, D2, the
  replayed run and across processes: the CLI saves' and the v33 continuation's s2 is materials-PACED. Pinned only in
  process (P1 L1) and on a same-schema UI resume (UI 3). Low risk: the wait is the same resolver step on every path.
- **R3** (structural stall) on every path but A / B in process: the CLI processes, C32, C33, D1, D2, the replayed run
  and across processes. Pinned in process on all three continuations (P1 L3 (i)–(iv): the clone, `Snapshot.Load` and
  `SaveFiles.Load`, the AI's cancel turn-exact after the load) and by the battery's named stall. Low risk: the stall is
  a function of the world, and `sim hash` and `sim run --from-save` load through that same `SaveFiles.Load`.
- **R5 × F** beyond the step: the two locally-measured sub-cells of §4.
- **D2 × R1a, R3, R5, R6 and R8** (R8 for a v33 or relabelled session). A resume of an older-schema session is the
  same-schema resume's replay (`UiSession.Resume`: re-founded from the manifest, the log replayed through the same
  executor) with the trace check off and, before v33, the world re-founded flat, so these rest on their D1 pins (R1a
  UI 3, R5 UI 4, R6 COL-exactly-once, R8 UI 1; R3 has none, above) and on D2's own pins for R2, R4 and R9 (UI 5–7). Low
  risk. D2 × R5 in particular: no instruction or design item names the three-way combination (resume × old schema ×
  re-enqueue).
- **R8 × F:** no process continues from a save made at a split order's issue turn (`alloc-136.civsnap` is only hashed).
  The split order's turn-exact delivery in a process that loaded a save is R7 × F (B delivers b = 70 after loading
  save_K), and R8 is pinned on both in-process loaders (P1 L8) and on the migrated fixtures (FX-V32_S, FX-V33_S). Low
  risk: a save holds no order; the order is in the log, and its delivery step follows from its stamp alone.
- **R7 × D1 and D2:** no UI session can reach a binding split through the emitters in the first turns (the player knows
  no road class); pinned on the in-process and CLI-loader paths and by the allocation leg.
- **`sim replay` has no `--save-at`:** cross-process replay is pinned by hash-log equality; the turn-exact semantics on
  the in-process replayed run and on two `sim run` saves at consecutive turns.
- **`sim diff` compares two LOADED worlds:** a v33 save's "side" is its migrated, empty `ConstructionHolds` block.

**Mutants:** step 1 10/10 and step 2 14/14 killed; the fix round killed M-RL, M-A, M-OFF, M-B and M-INH (a re-enqueue
into a slot this batch freed inheriting the cancelled entry's key). M-A (kind 12 after building work), M-B (cancel after
building work) and M-C (the resume drops kinds 11 and 12) are killed on the persistence paths, M-A and M-B also on the
migration path. Each bounded at 4× its filter's clean time; none hung.

---

## §6 Item 5 — cylinder pathfinding: the corrected finding, O1–O6 and the bench (track T5)

**The finding, corrected (it corrects D-054 §6 and §10 Q16).** D-054 §6 reads "Shipped config: median 11,877 ms against
batch 2's 10,436 ms (+13.8 %). PathBuild accounts for about 94 % of the difference (3,912 → 5,266 ms): the cylinder's
terrain and road network differ."
1. **The +13.8 % was not a same-day comparison.** It set the cylinder against batch 2's ROUND-1 median (10,436 ms). Batch
   2's round-2 median was 10,726 ms (the baseline moved 2.8 % between rounds), and the batch-3 flat control took 10,854
   ms. Against that same-day flat control the cylinder costs **+9.4 %** (11,877 / 10,854); the item-5 recon reproduced
   **+9.2 %** (16,216 against 14,856 ms, 3 interleaved rounds at load 0.4–1.1). About 4 points of the 13.8 % are round
   drift plus batch-3 changes that are not the wrap.
2. **The cylinder part is seed 42's A\* search size, not the road network.** On seed 42 PathBuild's A* settles 10,054,809
   nodes on the cylinder against 5,217,486 flat (1.93×) over 60 turns. All 12 settlements route to one shared target, the
   most fertile node outside their catchments, which on the periodic-noise terrain lies about 32 % farther away (85.8
   against 64.9 lattice units on average); the heuristic is weak (h(start)/cost 0.12–0.15), so A* is close to Dijkstra
   and its work grows with distance².
3. **The road network is the same size** at turn 59: 142 network nodes, 153 edges on the cylinder against 154 flat; no
   travelled road on either; equal catchment work (57 recomputes and 684 full Dijkstras each).
4. **Across seeds the cylinder is not slower:** over seeds 1–10 (30 turns each) it settles 5.1 % fewer PathBuild A* nodes
   and 2.9 % fewer catchment nodes (PathBuild CPU 34.84 s against 35.41 s flat). Seed 42, the only seed the bench used,
   is a 2× outlier.
5. **The wrap arithmetic costs nothing measurable:** seed 42's cylinder re-run with its lattice forced flat gives the
   identical world hash at +1.7 % total, inside the ±4 % run spread.

(Items 1–5 were measured by the batch's item-5 reconnaissance and quoted by T5 without being re-run.)

**The optimisations — all byte-identical, no state added, nothing shared between systems:**

| # | Change | Where |
|---|---|---|
| O1 | every lattice node's block fertility computed once per PathBuild step, not once per settlement | `PathBuildSystem.Targets` |
| O2 | each passable component flooded at most once per step; a frontier inside a component already flooded in this call reuses that array (a local of the call, read-only after its flood) | `PathBuildSystem.Targets` |
| O3 | the lane overlay in CSR form (`Pathfinder.LaneOverlay`), each node keeping the batch-3 lane order, filled backwards with no Dictionary and no cursor array | `Pathfinder.BuildOverlay` |
| O4 | FindPath expands inline (replacing, not duplicating, the batch-3 internals): the step cost from the direction (`TraversalLattice.StepCostKnown`), the heuristic from the neighbour's known coordinates with the branch-only `MapTopology.DxOnMap`; no delegate per relaxation | `Pathfinder.FindPath` |
| O5 | `Pathfinder.DistancesTo`: the uncapped Dijkstra stops once every passable target is settled; CatchmentSystem uses it for the pairwise settlement distances | `Pathfinder.DistancesTo`, `CatchmentSystem` |
| O6 | `StepCostKnown` also in Expand (Isochrone, Partition, DistancesTo) and RelaxCappedFrom | `Pathfinder` |

PathBuild's step now runs in two passes (bank every settlement's accrual and fix its frontier, in settlement order; then
choose all targets and lay the segments in the same order); pass 1 reads nothing pass 2 writes, so the batch-3 order of
effects is kept.

**Not done:** O7 (pooled scratch arrays) and O9 (a lattice per terrain) are state and would each need an ADR; O8 (an
indexed heap) is deferred; a tighter heuristic, a reverse search or skipped searches are not byte-identical and would
need a ruling and a re-pin.

**Proof of byte-identity.** No existing pin was re-pinned for item 5.
- **Function level:** the batch-3 code is kept verbatim in Sim.Tests as the oracle (`Pathing/Legacy/LegacyPathfinder.cs`,
  `LegacyPathBuild.cs`); 120 differential cases compare it bit for bit with the new code on 14 tie-dense flat and
  cylindrical lattices plus seeds 42 and 16 (`LegacyPathfinderDifferentialTests`, `LegacyPathBuildTargetsTests`,
  `MapTopologyBranchDxTests`: every (x, gx) on sizes 2..12, 63, 64 and 256, both wrap flags). The fix round added
  `PathBuildStepWiringTests` (the two-pass wiring: each builder's own component, own catchment and settlement order) and
  `LegacyCatchmentDistancesTests` (`SettlementDistances` row for row against the batch-3 isochrone on every recompute of
  turns 1–30, seeds 42 and 16); both pass on batch-3 code too. Mutants killed: M1–M4 (required), M4b, M4c, M5, M5b, M8,
  N6, N6b, N6c, PM3 and PM4.
- **Whole run, cross-binary (base-b3 binary against the T5 binary):** every ci.yml `determinism-xproc` leg byte-identical
  (51 of 51 files: toy, founded 42 at 300 = `FOUNDED_GOLDEN` of the time, seed 16, ordered and replay, found-settlement,
  the AI leg at 600 with its emitted order log, session files and telemetry); the `xplat-surveillance` leg's founded hash
  log and its turn-1, -2 and -3 saves; the 60-turn hash logs of seeds 42, 1, 2, 3 and 16; and the nightly (`autoplay --seeds 20 --turns 650 --metrics`) file
  sha256 `1ae0fe53f1b8ab33f801a1322fc382a9feca6d9e1c144de65c77a1ea80f3bab4`, equal to batch 3's, with a byte-identical
  `corridors.log`. The T5 fix round changed only comments in production code; its 60-turn hash logs again equal the
  batch-3 binary's.
- **The O5 early exit is guarded only by `sim bench`, through the catchment phase's TIME.** It changes no result, so no
  result-level test can see it. MEASURED (4 interleaved rounds, seed 42, 60 turns): with the exit disabled catchment took
  4,851–5,595 ms against clean 3,700–4,240 ms, the allocation unchanged (about 775.0 MB). Awaiting a duplicate target once
  matters only where two settlements share an origin node, which D-025 spacing forbids; its loss is invisible on founded
  worlds, even to the bench.

**The bench** (`sim bench --founded --seed S --turns 60`, Release, 5 interleaved rounds, rotated order, load 5.2–6.7 on a
shared 4-CPU host; base = the batch-3 tree, opt = T5, flat = T5 with `topology.wrapX` false in a throwaway clone; medians
of 5, ms; only ratios between interleaved medians are meaningful):

| Seed | base total | opt total | opt / base | PathBuild opt / base | Catchment opt / base | all-phase alloc opt / base | cylinder / flat (both opt): total, PathBuild |
|---|---|---|---|---|---|---|---|
| 42 | 21,452 | 11,843 | **−44.8 %** | −50.0 % | −52.4 % | −57.9 % | +19.8 %, +55.6 % |
| 1 | 19,583 | 10,572 | **−46.0 %** | −57.0 % | −47.2 % | −58.1 % | −7.5 %, −20.1 % |
| 2 | 20,264 | 9,427 | **−53.5 %** | −63.7 % | −57.0 % | −58.1 % | −12.2 %, −27.6 % |
| 3 | 20,452 | 11,398 | **−44.3 %** | −59.2 % | −44.9 % | −58.2 % | −1.2 %, −5.0 % |
| 16 | 22,754 | 12,658 | **−44.4 %** | −53.6 % | −43.4 % | −58.1 % | n/a |

- Over the five seeds the median totals sum to 104,506 ms (base) and 55,897 ms (opt): **−46.5 %**. On every seed the
  slowest opt run is faster than the fastest base run.
- Cylinder against flat on the T5 binary: only seed 42's +19.8 % lies outside the run ranges; over seeds 42 and 1–3 the
  cylinder totals 43,239 ms against flat 43,597 ms (−0.8 %). The remaining seed-42 premium is the same A* node count
  (INFERRED from byte-identity plus the recon's counts), now a larger fraction because the fixed per-step costs fell.
- Colonisation is unchanged at about 499.9 MB allocated per run (RelaxCappedFrom's per-call arrays; pooling them is O7).
- This bench ran on base-b3 + T5, without the hold rule (§1.11). The whole-run identity of the FINAL tree with and
  without T5, and a bench of the final tree, are the integration's validation (§8).

**Suggested summary line (T5's):** the +13.8 % compared the cylinder with batch 2's round-1 median; against a same-day
flat control it is +9.2–9.4 %, and that part is seed 42's A* search size, not the road network. The O1–O6 optimisations
(byte-identical) cut the shipped bench by 44–54 % per seed (measured on base-b3 + T5, no hold rule; the integrated
tree's figures are §8's), and the cylinder's total over seeds 42 and 1–3 now equals the flat world's (−0.8 %).

---

## §7 Moved pins, with attribution

Every re-pin carries a dated D-055 cause block at the pin (2026-10-16), and a `PreReservations` twin or arm (plus
`SchemaProjection.V33` where the layout matters) asserts the old value. The integration's merge stage moved no pin, edited
no test and touched no calibration item (all 669 tests of its three filters passed on the merged tree, §8); nor did its
update stage (§8).

**LAYOUT (the v34 layout alone; each old value reproduced through `SchemaProjection.V33` first;
`ReservationsLayoutAttributionTests.*_MovedForTheV34LayoutAlone`):**

| Golden | Batch 3 → batch 4 |
|---|---|
| founded seed 42 turn 300 (`SnapshotTests.FoundedGoldenHash` and ci.yml `FOUNDED_GOLDEN`) | `40115e28…` → `0f29efc733024c451b7ebc2e8435fdd8a6032d158922430cb290778a98056d3c` |
| toy seed 42 turn 200 | `222280e5…` → `2e6d446161e165475c753a8962f5b8f9e0845807c911f41be5ba5d982bed5ef7` |
| driven (`DrivenGoldenTests`) | `fa1ce900…` → `eb88663ac944da5b4bed584e60fa786ce90d1fc9cc6777906576ac608887e893` |
| first reign (`FirstReignTests.PostR1Golden`) | `bccc8aed…` → `f7ca60a52362a7da30c73be82ad8ecd56aeeb68f932b2ad98d8d3e76e3219057` |
| AI world (`AiFoundingAttributionTests.S1cTurn300`) | `91836bcf…` → `25f4799d581d8de72dc15d487bcba54cb9db399208aba065963eb1f257e9fd10` |

**BEHAVIOUR (the hold rule):**

| Pin | Kind | Batch 3 | D-055 | Control proving the old value |
|---|---|---|---|---|
| `IncrementalConstructionTests.AGatedEarlierHead_…`, renamed `…_TakesNothing_ButSetsAsideItsNeed_AndTheLaterHeadWaits` | DEFECT | built (S1, S0) = (0, 1); S2 (0, 0) | built (0, 0); S2 keeps (40, 20); holds (purse 1) timber 40, stone 20; S0 HeldForEarlier naming S1 | twin `AGatedEarlierHead_UnderPreReservations_…` |
| `IncrementalConstructionTests.ProjectsDoNotConsumeResourcesReservedByOtherProjects` | DEFECT (b); (a) re-asserted | (b) P(S0) 350,000, stone 0, timber 176, sunk 12; (a) P(S0) 350,000, sunk 12 | (b) P(S3) 250,000, P(S0) 0, 7 stone set aside, timber 190, sunk 5, holds timber 70 / stone 7; S0 HeldForEarlier naming S3; (a) no refund, P(S0) 0, sunk 5, holds timber 40 | twin `…_UnderPreReservations_IsBatch3` |
| `EmpireConstructionStockpileTests.ALowerIdHeadThatFailsAGate_…`, renamed `…_DrawsNothing_ButSetsAsideItsNeed_AndTheHigherIdHeadWaits` | DEFECT | built (S1, S2) = (0, 1); S3 (0, 0); 1 queue row | built (0, 0); S3 keeps (40, 20); 2 queue rows; holds timber 40, stone 20; S2 HeldForEarlier naming S1 | twin `ALowerIdHeadThatFailsAGate_UnderPreReservations_…` |
| `ResourceAllocationTests.BuildingDemand_UnderAShortage…`, restated `…_IsWhatItsHeadsWouldDrawOrSetAside_NotTheSumOfTheirNeeds` | WS2 (D_b = claims) | stone (S, D_b, G_b, HoldsBack) = (1000, 25, 25, False); UI (50, 25) | (1000, 40, 40, False); UI (50, 40); the step unchanged; the split-off step publishes 15 stone set aside | arm `reservations: false` |
| `ResourceAllocationTests.TheForecastIsTheStep_TwoEmpires_…` | WS2 | `BuildingDemand == offDraw` | `BuildingDemand == offDraw + offHeld` (the split-off step's own published set-aside); anti-vacuity: a set-aside in D_b > 20 times | arm `reservations: false`: offHeld = 0 at every step |
| `IncrementalConstructionTests.TwoHeadsAndARoad_…` | behaviour | bounded only (0 < units ≤ half) | 30 units (0 → 0.25); holds timber 30, stone 15 | arm `reservations: false`: 60 units (0 → 0.5) |
| `IntegratedSaveLoadBatteryTests.TheScriptedRun_…` save points | behaviour | (1, 26, 2, 26) | (1, 26, 23, 24): world 2 holds all 185 of the player's timber set aside; world 23 holds the turn-22 order's row (152 units, 0 → 0.62) | `TheSavePoints_UnderPreReservations_…`: (1, 26, 2, 26) |
| `IntegratedSaveLoadBatteryTests.TheSavePoints_MovedFor…_EachLayerAloneMovesItsOwnPin` | attribution arms | flat (1, 27, 2, 26); ws2Off (1, 26, 2, 26) | flat (1, 27, 23, 24); ws2Off (1, 26, 23, 24); ws1Off and bothOff unchanged; NEW arms reservations-off (1, 26, 2, 26) and flat + reservations-off (1, 27, 2, 26) | the two new arms |
| `IntegratedSaveLoadBatteryTests.SaveLoad_MidPartBuiltConstructionHead` | behaviour | world 8, p = 231,666 | world 8, p = 306,111 | the same predicate under PreReservations: world 8, 231,666 |
| `SaveLoad_MidPartialRoadModernization`, `SaveLoad_MidUniversityMaturation` | located by measurement | worlds 2 and 26 | worlds 23 and 24 | — |

**Cause of the battery move.** From world 2 the capital's university is the head, ordered at turn 0, a valid long wait
(viability in 8 steps, materials in the rest). It sets aside its remaining need, which exceeds the player's whole timber
purse in every world up to 21, so the turn-1 and turn-12 road orders pay nothing. It keeps the 185 timber the batch-3 road
took, completes one step earlier (21 → 22; batch 3 22 → 23) and matures from world 24 (batch 3: 26).

**TEXT (the wording, §1.9; each with a dated cause block and its batch-3 string asserted under the rule-off control):**
`ResourceAllocationUiTests` — the default line, the construction block's two lines, the roads preview's "after what
building work uses or sets aside, …" (two tests), the EMPIRE view's split line; and Sim.Tests
`EmpireConstructionStockpileTests.AClaimedCraftedInput_…`'s card note ("; 10 set aside for S0's Workshop, ahead in the
building order"). The value pins `ResourceAllocationUiTests` D_b = 80 and (40, E_b, 40) did not move.

**SCHEMA (X1 (a)–(k), each changed or re-rigged with a dated cause block):** the `SnapshotDiff` layout and `LayoutFor`;
`ConstructionSchemaV33Tests` (the layout arithmetic, the v33 stream from the real fixture, the refusal list {31, 35, 0,
27}); the eight `IntegratedPinAttributionTests` chain helpers (a v34 strip first); `Batch2SessionResumeTests`' V32
projection (12 bytes); the 15 `Assert.Equal(33, CanonicalSchema.Version)` pins in 12 files and `ForensicIdentityTests`'
one (16 in 13 files) → 34; `CliSaveAndSizeMessagesTests` (the migration notes, "(schema v34)",
`TheReproduceLine_IsThisBuildsReplay_FromV34…`, a "newer schema" manifest of `Version + 1`); `ChronicleAndResumeTests`'
flat arms (`older = 32` explicit) and a NEW v33 arm
(`ResumeNoticeUiTests.ASessionResumedUnderSchemaV33_IsACylinderRebuild_…`); `WorldStates.StateEquals`, Clone and the
`SystemCatalog` hand-out.

**Not moved (measured):**
- **The AI worlds:** dev 450 turns and production 600 turns with one AI Empire make 10 and 17 enqueue orders, no world
  ends with a queue row, and no hold row is ever published (`AiWorldSeed42Turn300_…` asserts none in 300 turns).
- **Calibration:** no frozen item touched; the two CR-020 reds keep batch 3's values exactly (§8).
- **Items 2–5 re-pinned nothing** that existed before batch 4. T2's fix round re-pinned only its own new relief rig's
  amounts (stock differences → the books), with a dated cause block. T3's round 2 re-pinned only its own scenario's pins
  for its cause, the three granaries queued with the b = 70 order (dated 2026-10-17, each old value reproduced on its
  build from the round-1 order log: the round-1 hash log and report byte for byte, world 140 `44ee50d0…`): the stone rows
  136 (old `(136, 70, 73, 51, 22, 0, 73, 51, 0, 0, 0, 73)`) and 137 (old `(137, 70, 76, 53, 23, 0, 76, 53, 0, 0, 0, 76)`),
  the granaries built by H (5, 5, 1) → (6, 6, 2), the binding steps (+136) and "no head at 136 / 137"; the generator
  literal, the four logs' sha256 (in `AllocationCiLegTests` and ci.yml) and the 49 / 47 → 52 / 50 record shapes were
  re-measured. T3's round 3 changed one message literal of its own test (`--ai-empires` refusal: "which holds 1 AI
  polity" → "which was founded with 1 AI Empire", same world, same exit code) and re-rigged one of its own tests whose
  doctored save had become a revolt-born polity's shape (`ASaveWhoseFoundingCannotBeRegenerated_…`, now doctoring a moved
  capital).

---

## §8 Validation (gov-4 §9: a result, not a certification)

**What was NOT yet run when this record was written** (the integration's records stage, 2026-10-17): the full Sim.Tests and
Sim.Ui.Tests on the merged tree, the determinism filter, the ci.yml cross-process steps (T0.9 and the allocation step) on
the merged tree, `--smoke --monkey 300`, the nightly and `corridors`, the final tree's whole-run identity with and without
T5, and `sim bench` of the final tree. They are the integration's validation stages, which run on this tree after this
record; the application to the integration worktree and the worktree validation follow the integration's verification.
Until then every result below is **scratch-only**. (Since then, on 2026-10-18, the update stage ran the allocation step
locally, and the two validation stages and the fix stage ran the rest of this list — all on scratch clones, none on the
integration worktree. Their result table is below. The worktree validation is reported in the batch-4 report, not here.)

**Per track, on its own final tree** (each a scratch clone; "CR-020" = the two pre-existing calibration reds,
`CalibrationBatteryTests.Dev_MalthusCorridors_AllInBand` seed 42 "8.35755E-05 fell below 0.75× its recorded value
0.000248624" and seed 7 "Range: (71000 - 119000) Actual: 123950", batch 3's values exactly):

| Track (tree) | Build and gates | Full Sim.Tests | Full Sim.Ui.Tests | Other |
|---|---|---|---|---|
| T1a v4 (`6c8e3f18…`) | 0 warnings, 0 errors; the three gates and the three research `--check`s OK | 2,201: 2,185 passed, 3 failed, 13 skipped (four disjoint partitions run together); the 3 = CR-020 + `PopulationTests.ProductionPipeline_PerPhaseBench_Reported` (6,213 ms against its 5,000 ms wall-clock budget at load 32–40; 4/4 PASSED re-run alone, 2,939–3,357 ms: environmental) | not re-run for v4 (comment-only Sim.Core delta); the session/resume/replay classes 47/47; v3: 977/977 | focused 196/196; round 2: the ci.yml determinism-xproc step extracted and run with the v3 build, exit 0, founded 300 = `0f29efc7…` |
| T5 v2 (`3995c272…`) | 0 warnings; gates and research `--check`s OK | 2,205: 2,190 passed, 2 failed (CR-020), 13 skipped, 45 m 26 s; 0 outcome changes against T5 step 2 | not run (no Sim.Ui or production statement changed) | focused 300/300; whole-run identity (§6) on the step-2 tree |
| T1b v2 (`b9f52cf8…`) | 0 warnings; gates OK | 2,220: 2,205 passed, 2 failed (CR-020), 13 skipped, 57 m 40 s | 1,009/1,009, 20 m 11 s | smoke `--report`: SMOKE PASSED, 13 states, 3,190 checks, 0 FAIL |
| T3 final (`0d499208…`, round 3) | 0 warnings; gates OK | 2,335: 2,320 passed, 2 failed (CR-020), 13 skipped, 50 m 9 s; the wall-clock bench passed | 977/977, 16 m 9 s | focused 134/134; the allocation step run locally, exit 0 (118 s). Earlier: fixer round (`b4aeec26…`, the merged revision) 2,290 (2,275 passed, CR-020, 13 skipped), 977/977; round 2 (`55efd659…`) 2,316 (2,301 passed, CR-020, 13 skipped), 977/977 |
| T2 v2 (`b4219b0d…`, on `t1a-final`) | 0 warnings; gates OK | 2,202: 2,187 passed, 2 failed (CR-020), 13 skipped, 1 h 54 m | not run (Sim.Tests only) | focused 78/78 |
| T4 v2 (`4ab9c486…`, on `t1a-final`) | 0 warnings, 0 errors (incremental and `--no-incremental`); the three gates OK | 2,200 in five disjoint partitions: 2,184 passed, 13 skipped, 3 failed = CR-020 + the wall-clock bench (5,742 ms under load; 3,931 ms and PASSED re-run alone: environmental) | step 2: 987/987 (the fix round changed Sim.Tests only) | — |

**The merge stage, on the merged tree** (`6a9f6dc9…`, the integration clone; build `--no-incremental` 0 warnings, 0 errors;
the three gates and the three research `--check`s OK; host load 2–8):
- Sim.Tests, every class T2 and T4 added or changed, every class T1b and T3 added, and T5's added classes: **408 / 408
  passed**, 6 m 58 s.
- Sim.Ui.Tests, T4's and T1b's classes: **88 / 88 passed**, 1 m 29 s.
- Sim.Tests, the identity pins T5 had run on base-b3 (`SnapshotTests.FoundedGolden_Seed42Turn300_MatchesPinnedConstant`,
  DrivenGolden, CiPinAgreement, WrapAttribution, D054PairAttribution), plus ReservationsLayoutAttribution,
  AiFoundingAttribution, IntegratedPinAttribution, ConstructionReservation and ConstructionHoldsSchemaV34: **173 / 173
  passed**, 16 m 21 s.
- So the T1a changes between `t1a-final` and v4 moved nothing these classes measure on the merged tree, although T2's and
  T4's track runs were on `t1a-final`.

**The update stage, on the updated tree** (2026-10-18; code tree `06eae6ba…` — the merged tree with T3's final revision —
plus these records; the integration clone; build `--no-incremental` 0 warnings, 0 errors, Sim.Core.dll `569c24e7…`; the
three gates and the three research `--check`s OK; host load 1–3):
- Sim.Tests, T3's six classes (`AllocationCiLegTests` 12, `AllocationScenarioTests` 8, `RunFromSaveCliTests` 31,
  `SplitProbeTests` 11, `SplitReportCheckTests` 41, `SplitReportRederivationTests` 31): **134 / 134 passed**, 1 m 17 s.
- Sim.Tests, beyond them: the six in-process starvation-guard tests §4 cites, `CliSaveAndSizeMessagesTests` (the usage
  text T3 changed), `FoundSettlementCiLegTests` (the generator T3 changed) and `CliRevoltPolityProducerTests` (the revolt
  rig T3's new test mirrors): **39 / 39 passed**, 55 s.
- The ci.yml step "Allocation split, binding, cross-process (D-055)", extracted verbatim and run LOCALLY with `bash -e` on
  this build: **exit 0, 89 s**; its log and all its outputs byte-identical to T3's round-3 run on T3's own tree (so T5,
  T1b, T2 and T4 move nothing the leg measures); report B and the twin report equal the fixtures `k.jsonl` and
  `k-no12.jsonl`; world 140 `be3385a9…`, the control's `182991ab…`; no report row is guarded.
- Not run in this stage: the full suites and everything else this section lists as not yet run.

**The records stage, on the merged tree's build:** `Sim.Ui --smoke --report` (the gate's §6 regeneration; result in
`docs/m5-playability-gate.md`'s file-end note) and the three research `--check`s.

**The integration's validation of the scratch integrated tree** (design §6, the validation plan; 2026-10-18). Every row
below ran on a scratch tree, either the integration clone or a throwaway clone of an integration patch. None ran on the
integration worktree. The revisions:
- **V**, the validated tree: work tree `4602ed4d46204113758c2b1fb1f2195dacbddba4` = `int-pre-validate.patch`, sha256
  `e4b607ed4f3b3a580ec661f77373412928fbd5f7e3da9cdf5bc0f8147ae98534`, 144 files, +23,611 / −572 over base-b3
  `b624e62c…`. Its part outside `docs/` is the updated code tree `06eae6ba…`. Validation stage A ran in the integration
  clone, and stage B in throwaway clones of the same patch.
- **V − T5**: work tree `e00487bcf67e347de4c00bb5bdf31bf0822acfc2` = V with `t5-v2.patch` (`a04c3f2c…`) reverse-applied,
  without conflict (stage B).
- **base-b3**: `b624e62c…`, for the bench only (stage B).
- **F**, the fixed tree: work tree `30fed100871cb00a8004adb491d5a7479d644e0c` = the fix stage's patch, sha256
  `2e2353eb835315e091d1313c904b053d5fd4308263e01b486c640478c1b8ecf3`, 144 files, +23,718 / −572 over base-b3. F differs
  from V in five files: records text, and `///` comment lines only in `Sim.Cli/FromSave.cs` and
  `Sim.Tests/Systems/ConstructionLifecyclePersistenceTests.cs` (the provenance's "fixed tree").
- **The tree that carries this record** is F plus the records-finalize stage's edits (2026-10-18) to this file and to
  `docs/current-state.md`. No other path changed. A record cannot name the tree that contains it, so the batch-4 report
  gives that tree's id and its patch's sha256. Outside `docs/` that tree is F, file for file (the check below).

CLASS: **scratch-only** = passed, or measured, on a scratch tree, not on the worktree; **pre-existing failure** = red
before batch 4 (the CR-020 pair); **new failure** = a red that batch 4 introduced; **untested-or-manual** = not run.

| # | §6 step and command | Revision | Result | Class |
|---|---|---|---|---|
| 1 | 2. `dotnet build Sim.slnx -c Release --no-incremental` | V; F | 0 warnings, 0 errors on each. Sim.Core.dll is `569c24e7…` on both, and F's Sim.Ui.dll and Sim.Ui.Tests.dll equal V's. Stage B also ran `dotnet build Sim.Cli/Sim.Cli.csproj -c Release --no-incremental` on V, V − T5 and base-b3: 0 warnings, 0 errors each | scratch-only |
| 2 | 2. `./scripts/check-banned-constructs.sh`, `./scripts/check-read-isolation.sh`, `./scripts/check-readonly-proof.sh` (the read-only mutation proof) | V; F; this record's tree | OK, rc 0, each time | scratch-only |
| 3 | 3. `python3 scripts/research-content-audit.py --check`, `research-calibration-report.py --check`, `research-gameplay-unlock-audit.py --check` | V; F; this record's tree | "… is current", rc 0, each time | scratch-only |
| 4 | 1. The focused filters | the merged tree `6a9f6dc9…`; the code tree `06eae6ba…`; F | All passed. The merge stage ran 408 + 173 Sim.Tests and 88 Sim.Ui.Tests, and the update stage 134 + 39 Sim.Tests (both above). On F, the areas the fix stage touched: `ConstructionLifecyclePersistenceTests` 18, `RunFromSaveCliTests` 31, `CliConstructionLifecycleTests` 5 and the five order-as-data pins, 59 / 59 | scratch-only |
| 5 | 4. `dotnet test Sim.Tests/Sim.Tests.csproj -c Release --no-build --logger trx` (FULL) | V; F | 2,577 tests on each: 2,562 passed, 2 failed, 13 skipped (V 34 m 36 s; F 18 m 0 s). Each of the 2,577 has the same outcome on F as on V; the two trx files were compared test by test. The 2 failed are the CR-020 pair (row 11). The wall-clock `PopulationTests.ProductionPipeline_PerPhaseBench_Reported` passed in both runs | 2,562: scratch-only; 2: pre-existing failure; the 13 skipped `[Skip]` measurement rigs: untested-or-manual |
| 6 | 4. `dotnet test Sim.Ui.Tests/Sim.Ui.Tests.csproj -c Release --no-build --logger trx` (FULL) | V; F | 1,019 / 1,019 passed on each (13 m 20 s; 12 m 43 s), with the same outcome test by test | scratch-only |
| 7 | 5. `dotnet test Sim.slnx -c Release --no-build --filter "suite=determinism"` (ci.yml T0.8) | V | Sim.Tests 60 / 60, in 10 classes; no Sim.Ui.Tests test carries the trait. Not run as a filter on F; each of the 60 ran in row 5's F run, with V's outcome | scratch-only |
| 8 | 6. Topology and pathfinding: a `--filter` of 30 classes (MapTopology*, SeamPathfinding*, Seam*, Cylinder*, Pathfinding*, the Legacy differential) | V | 314 / 314 (Sim.Tests 240, Sim.Ui.Tests 74). On F, each of the 314 ran in rows 5–6, with V's outcome | scratch-only |
| 9 | 7. Save/load and migration: a `--filter` of 13 classes (ConstructionSchema*, ConstructionHolds*, IntegratedSaveLoadBattery*, Snapshot*, CliSaveAndSizeMessages*, Batch2SessionResume*, ConstructionLifecycle*, ConstructionFixtureContinuation*, V33SessionResume*) | V | 204 / 204 (162 + 42). On F, all ran in rows 5–6, with V's outcome | scratch-only |
| 10 | Beyond §6: allocation (ResourceAllocation*, Allocation*; 5 classes) and `ConstructionReservationTests` | V | 90 / 90 and 77 / 77. On F, all ran in rows 5–6, with V's outcome | scratch-only |
| 11 | 8. `dotnet test Sim.Tests/Sim.Tests.csproj -c Release --no-build --filter FullyQualifiedName~.CalibrationBatteryTests.` | V (alone); F (in row 5) | 7 tests: 5 passed and 2 failed. The 2 are EXACTLY `Dev_MalthusCorridors_AllInBand` seed 42, "8.35755E-05 fell below 0.75× its recorded value 0.000248624" (`CalibrationBatteryTests.cs`:317), and seed 7, "Range: (71000 - 119000) Actual: 123950" (:770), with batch 3's values. F's full run gives the same two messages. No other calibration test failed, and nothing was re-pinned | 2: pre-existing failure (CR-020); 5: scratch-only |
| 12 | 9. `dotnet Sim.Ui/bin/Release/net10.0/Sim.Ui.dll --smoke --monkey 300` | V; F | SMOKE PASSED on each: 13 states, 3,190 checks, 2,988 pass, 202 not offered, 0 FAIL, 0 problems; 111,121 frames, 3,900 monkey actions | scratch-only |
| 13 | 9. `--filter FullyQualifiedName~.PlayabilityGateTests.` | V (alone); F (in row 6) | 29 / 29 | scratch-only |
| 14 | 10. ci.yml job `determinism-xproc`, step "Cross-process determinism (T0.9)": extracted verbatim (229 lines, sha256 `78471b6c…`) and run LOCALLY under `bash -e` | V (stage A; stage B, beside V − T5); F | Exit 0 each time: V 500 s (A) and 613 s (B), F 512 s. Two processes are byte-identical over the toy run (400 turns), the founded run (300) and seam seed 16 (300). The founded run equals `FOUNDED_GOLDEN` `0f29efc7…`. The found-settlement and AI-Empire legs reproduce (301 and 601 turns; 275 orders, kinds [3, 4, 5, 6, 7, 8, 9, 11, 12]). F's 49 outputs are byte-identical to stage A's | scratch-only (local; not GitHub CI) |
| 15 | 10. ci.yml step "Allocation split, binding, cross-process (D-055)": extracted verbatim (102 lines, sha256 `c7957210…`), run LOCALLY under `bash -e` | V's code (the update stage); V (A, B); F | Exit 0 each time: 89 s (update), 90 s (A), 198 s (B), 87 s (F). The 28 outputs are byte-identical across the four runs; they include the `sim hash` checks of the saves at 130, 132 and 136 | scratch-only (local; not GitHub CI) |
| 16 | §5. Whole-run identity with and without T5 | V against V − T5 | Byte-identical: every output of rows 14 and 15 (53 / 53 and 28 / 28 files), row 17's nightly (`worldHash` included) and the five bench seeds' 60-turn hash logs | scratch-only |
| 17 | 11. `dotnet run --project Sim.Cli -c Release -- autoplay --seeds 20 --turns 650 --metrics nightly-metrics.json`, then `dotnet run --project Sim.Cli -c Release -- corridors --metrics nightly-metrics.json` | V (and V − T5) | rc 0, rc 0. Against batch 3's file (sha256 `1ae0fe53…`), field by field with `worldHash` excluded, all 322 compared fields are identical. `worldHash` differs on all 20 seeds (INFERRED cause: the v34 layout, as for the order-less founded golden). `corridors.log` is byte-identical to batch 3's, including its QUARANTINED `densityPerArableKm2` line and its migration WINDOW BREACH line (80.8272 %), which do not gate (exit 0). No series moved, so no `PreReservations` nightly was needed, and there is no calibration finding | scratch-only; the two corridor lines: pre-existing, not failures |
| 18 | 12. `dotnet Sim.Cli.dll bench --founded --seed S --turns 60`, S ∈ {42, 1, 2, 3, 16}, 5 interleaved rounds (75 runs) | V, V − T5, base-b3 | All rc 0. At load 1.80–7.66, V against base-b3 is −39.6, −39.7, −47.0, −41.8 and −36.5 % per seed, and −40.8 % over the summed medians. Over the summed medians, V against V − T5 is −40.7 %, and V − T5 against base-b3 −0.2 % (inside the noise). All-phase allocation is −57.9 to −58.2 %. The construction phase's allocation goes from 18,848 to 31,400 B over 60 turns | scratch-only (a measurement on a shared host, not a pass or a fail) |
| 19 | Manual: the GPU seam, and a person reading the holds UI | — | not done | untested-or-manual |
| 20 | GitHub CI on a batch-4 commit (none exists; rows 14–15 ran only locally); the Windows build | — | not run | untested-or-manual |

**New failures: none.** Every red in rows 1–18 is one of the two CR-020 reds.

**What was re-run after the fix stage's source edits** (gov-4 §9: a result expires when the code changes). F changed two
source files against V, by `///` comment lines only.
- **Re-run on F**, once each and on one Release build: rows 1–6, 12, 14 and 15, and row 4's focused filters of the
  touched areas.
- **Not re-run as their own commands on F:** rows 7–11 and 13. Their tests all ran inside F's full runs, with V's outcome
  test by test. This was VERIFIED at the records-finalize stage: every test of stage A's seven filter runs (60, 314, 204,
  90, 77, 7 and 29 tests) is in F's two full trx files with the same outcome; 0 are missing and 0 differ.
- **Not re-run on F at all:** rows 16–18 (the identity with and without T5, the nightly and the bench). INFERRED: they are
  unaffected, because no compiled statement changed. Sim.Core.dll and Sim.Ui.dll are byte-identical to V's. The Sim.Cli
  and Sim.Tests DLLs differ; INFERRED, only through the edited comment lines (the embedded PDB checksums). Rows 14 and
  15 run Sim.Cli, and their outputs are byte-identical on F and V.
- **The gate's §6 regeneration** (the records stage, on the merged tree's build) was not repeated either. No Sim.Ui,
  Sim.Ui.Tests or Sim.Data file differs between the merged tree and F. The only Sim.Core files that differ,
  `Observability/SplitReport.cs` and `SplitReportCheck.cs` (T3's final revision), are reached only from Sim.Cli, directly
  or through `SplitProbe`; Sim.Ui and Sim.Data reference none of the three (VERIFIED by `git diff` and `grep`). That the
  smoke report is therefore unchanged is INFERRED.

**The fix stage's mutants.** Two mutants showed that the two corrected comments state what the code does. Each ran on a
throwaway clone of F and was bounded at 4× its filter's clean time:
- **`FromSave.cs`'s `--ai-empires` rule:** the check reverted to the stale sentence's rule (the save's AI polity count,
  revolt-born polities included) fails 2 of `RunFromSaveCliTests`' 31 —
  `ARevoltBornPolity_IsNotAFoundingEmpire_AiEmpiresIsTheFoundingCount` and
  `ASaveMadeAfterARealRevolt_ContinuesToTheUninterruptedTail_AndAiEmpiresIsTheFoundingCount`.
- **L10's scope:** road development handed `prev.ConstructionHolds` instead of `next.ConstructionHolds` in
  `SystemCatalog` passes all 18 `ConstructionLifecyclePersistenceTests` (L10's rigs run construction only) and fails 4
  of the 5 order-as-data pins the corrected comment names (`ConstructionReservationTests.TheEdgeOrderIsData_…`, three of
  its four cases, and `TheV33Migration_IsExactInTheShippedOrder_…`).

**The records-finalize stage** (2026-10-18; the integration clone; this record's tree) changed this file (the provenance,
the scratch-only paragraph, the record history and §8) and `docs/current-state.md`'s batch-4 entry, and nothing else
(VERIFIED: `git diff --stat` against F). No test,
gate, script or ci.yml step reads either file (VERIFIED: `grep` of the `Sim.*` projects, `scripts` and `.github`). Rows 2
and 3 were re-run on this record's tree. Nothing else needed re-running.

**How to check that a tree carries the validated code** (F's code, which is also this record's tree's code). At the
tree's root, run:

```
git ls-files -co --exclude-standard -- ':!docs' | LC_ALL=C sort -u | while IFS= read -r f; do [ -f "$f" ] && sha256sum "$f"; done | sha256sum
```

It must print `736862e6aaa85795df6d0fff67a874efcfbda4202251621900813e9097cc1207` (770 files). Then every file outside
`docs/` is F's, byte for byte.

**Validation on the actual integration worktree** is reported in the batch-4 report, not in the repository. That covers
the design's application step and its full validation ON THE WORKTREE. This record reaches the worktree inside the patch
it describes, so it carries no result measured on the worktree. Read every result in it as scratch-only.

**Measured suite times** (wall, shared 4-CPU host; not performance measurements):
- **T2's classes** (class filters, load 15–30): CylinderAttributionDevStarved + Driven 24 s; DevAi 28 s; Founded 27–34 s;
  Seam 14–28 s; Revolt 67 s; Separability 53 s; Reservations 6 s; ReliefAbsence 133 s (291 s with the fix round's two
  absence twins on one shared ON arm); Ai1024 412 s (the critical path; the only class with the determinism trait);
  explain + twins 6 s; all T2 classes ≈ 800 s (48 tests).
- **T4's** (load 13–20): P1 18 tests 10–15 s; UI 7 tests 20 s; BAT 6 tests 15 s; FX 17 tests 80 s; CLI 5 tests 72 s; COL
  3 tests 41 s.
- **T3's:** 134 tests in 1 m 25 s (T3 round 3) and 1 m 17 s (the update stage), the runner's durations; the extracted
  ci.yml step 89 s – 3 m 03 s.
- **T5's:** the 129 differential and wiring cases; the new fix-round tests add about 30 s.
- **T1a's** focused set (eight classes) 196 tests in 9 m 31 s.
- **Full suites** grew from batch 3's 2,076 Sim.Tests / 975 Sim.Ui.Tests to the per-track totals above. A single-process
  full Sim.Tests run took 45 m 26 s (T5), 50 m 9 s (T3's final revision; 55 m its fixer round), 57 m 40 s (T1b) and
  1 h 54 m (T2) on the shared host; T1a and T4
  ran it as disjoint partitions (T4: a single-process run exceeds the harness's 2-hour limit at load 13–35).

---

## §9 Known limitations

1. **Head-of-line wait.** A valid but long hold makes every younger head of the same purse wait. MEASURED: the battery
   variant's younger granary waits 20 steps (200 sim-years at dt 10) behind the capital's university, where batch 3
   built it at once (`IntegratedSaveLoadBatteryTests.TheBatteryVariant_AYoungerGranaryWaitsBehindTheUniversitysSetAside_…`);
   in the scripted battery the same hold makes the turn-1 and turn-12 road orders pay nothing. This is the Director's
   rule working as specified. The remedies shown are to cancel the holder, give Building a share in Labour at the
   holder, or (under a split) let road works use the rest. A promoted slot ordered earlier can take a part-built
   head's set-aside (§1.3).
2. **v33 sessions under v34** are replayed under batch-4 rules (the D-008 path, the four-line notice, the trace not
   checked; on the fixture the replay first differs at turn 3).
   - **ADR-034 refusal.** If the batch-4 replay never founds a colony that a later recorded order names,
     `ValidateAtDelivery` refuses the resume. Pinned (COL-v33-refusal) with the message "order (turn 3):
     EnqueueConstruction targets settlement 12, which does not exist when the order is delivered (turn 3, 12
     settlement(s)). Its check was deferred because a colony could have been founded with that id by then; none was
     (ADR-034)." The test reaches it by removing the founding order from a session relabelled v33 (INFERRED: a real
     batch-3 session diverges this way only if the hold rule changes whether a colony is founded).
   - **Shifted cancel.** A recorded kind-11 (settlement, slot) order can hit a SHIFTED slot: pinned (UI 7) — the cancel
     removes a part-built entry the player never cancelled, and the one the player did cancel goes on being built. It is
     not refused (the order is valid at delivery). The batch-3 recording in that test is made by the PreReservations
     twin (INFERRED to stand in for the batch-3 build).
3. **A Controls-less world** keys every head to its own settlement's purse, so holds there are per settlement (#13), and
   road works respect them.
4. **Doubly-controlled places** (two control rows for one place; no shipped rule creates one; D-037's cardinality is
   unenforced). Construction, road works, the split, the road blocker and the roads previews follow the lowest
   controller's purse (§1.2, §1.9). The AI's road policy (`RoadDevelopmentPolicy`) still lists every place with a
   control row (`PayingSettlements`): an AI choice surface only, not measured.
5. **A non-shipped pipeline order** reads `ConstructionHolds` as it stands (§11): the previous step's set-asides or a
   loaded save's. In particular, after a v33 load in a road-first order the first step's road works see no set-asides,
   while a v34 save of the same state would carry its rows (pinned in both directions). Only the shipped order is exact.
6. **The GPU seam is untested headless** (D-054 §10 Q10, unchanged): the terrain sampler and the river translations across
   the seam are checked only by the manual playtest.
7. **The allocation leg runs locally only** in this batch (§4), two re-enqueue sub-cells are local measurements, and
   **no step of the leg is starvation-guarded**: the guard is pinned in process only (§4).
8. **`--from-save` refuses a save of a world founded with more Empires than settlements once a revolt has founded an AI
   polity** (its founding AI count cannot be told from the save). Otherwise a revolt-born AI polity no longer makes a save
   refused: T3 round 3 continued a real one to the uninterrupted run's tail (§4; queue).
9. **The persistence gaps** of §5's recorded-gaps list: R1a and R3 beyond the in-process paths; the older-schema UI
   resume (D2) for R1a, R3, R5, R6 and R8; R8 and, beyond the step, R5 across processes; no UI session reaching a binding
   split; `sim replay` has no `--save-at`; `sim diff` compares loaded worlds.
10. **The O5 early exit has no test;** only the bench guards it (§6). The integrated tree's bench is §8's.
11. **The construction phase costs about 0.15 ms a turn more** (§1.11).
12. **Labels are estimates.** A head that advances, but less than its builders could because of an earlier set-aside,
    keeps batch 3's estimate line; the settlement view's own-head line still reads "waiting for stone" without naming the
    holder (the action surface and the card name it); the road blocker with no road order pending reads the walk with no
    split grant, so a set-aside a later split would cap is counted whole; #16's label is read on the binding world; a
    queued head that advances partly (as far as a good allows) and lacks its full remaining need after the earlier heads'
    draws and set-asides now reads Blocked / "It will wait for …" — batch 3's meaning of "will wait for" (the purse does
    not hold the whole remaining need), now counting what the heads ahead take (T1b's fix round).
13. **The AI does not read holds.** Its prudence and its structural-stall cancel are unchanged; a held AI head is not
    cancelled merely for being held (#11).
14. **The gate doc's §6 frame count moved by 14** (117594 → 117608, regenerated by the gate's own command: no row, state,
    verdict or other total changed). The move is not attributed between T1a and T1b (T1b's records).
15. **Pre-existing, environmental: a wall-clock bench can redden a full Sim.Tests run under host load.**
    `PopulationTests.ProductionPipeline_PerPhaseBench_Reported` asserts 200 founded turns in under 5,000 ms; under load
    it exceeded that in T1a's and T4's full runs and passed when re-run alone (§8). It is not attributable to batch 4.

---

## §10 What this supersedes, and what it does not

1. **D-054 §1's "Reservation (Director item 5)" definition** — "The claims (each queued entry's REMAINING needs) are
   advisory. … they set nothing aside" — is SUPERSEDED **for queue heads**: every unreleased head sets aside its remaining
   need (§1). Non-head entries still only claim. The "(a) sunk materials" half stands.
2. **D-054 §10 Q17 is RESOLVED** by the instruction ("Do not ask the Director to resolve this already-established
   requirement"): an earlier head's unmet remaining need bounds later heads' draws from the same purse, and road works'.
3. **D-054 §6's bench paragraph and §10 Q16 are CORRECTED** (§6): the +13.8 % was against a different round's baseline;
   the cylinder part is +9.2–9.4 %, seed 42's A* search size, not the road network; after O1–O6 the 60-turn bench was
   44–54 % faster per seed on base-b3 + T5 (no hold rule; §6). On the integrated tree (`4602ed4d…`, patch `e4b607ed…`;
   the integration's second validation stage, 5 interleaved rounds, load 1.80–7.66) it measured 36.5–47.0 % faster per
   seed against base-b3, 40.8 % over the five seeds' summed medians (§8).
4. **D-054 §10 Q14 is ANSWERED** (§3).
5. **ADR-039 §8's "its worlds are founded flat"** (line 347) is CORRECTED by its file-end note (2026-10-16, track T2).
6. **D-054 §2's building demand** ("D_b = draws") is now the claim (draws + set-asides) (§1.6). The split's formulas
   stand.
7. **ADR-032's road charge** now subtracts the set-asides of every purse within the payers (its file-end note).
   ADR-032's min-ratio charge, nearest-first donor order and Final Rulings stand.
8. **ADR-005 / ADR-039's schema:** v34 appends section 61; the readable set is {32, 33, 34} (ADR-005's note of
   2026-10-16; ADR-039's note of 2026-10-17; ADR-040).
9. **The m5w plan's reservations** (D-054 §5 item 13: "S2a's known extent moves to v34, and S3's Action Points table to
   the next free version"): v34 is taken by `ConstructionHolds`, so **S2a moves to v35 and S3 to v36**. Kind 9 is
   unchanged, kind 10 stays reserved for MoveFormation, and batch 4 adds no order kind.
10. **D-054 §2's "Cross-process coverage. No lasting CI leg makes the split BIND"** (and its `docs/queue.md` line) is
    closed (§4).
11. **The batch's own design spec** (v2) §1.4 and §1.8 #22 as first written (road development reads construction's table
    only when construction runs first, otherwise an empty table, via a kernel edge) are AMENDED by T1a round 2 and the
    orchestrator's ruling (§11).
12. **Not touched:** every frozen calibration item (the `CalibrationBatteryTests` literals, `corridors.json`, the [71,000,
    119,000] envelope, the per-seed migration values, the hazard, spoilage and the store bound); the kernel's behaviour
    (§11); D-052 §4 and §5's visibility half; the batch-2 and batch-3 layers' rules other than D-054 §1's reservation
    definition and §2's D_b.

**Open from D-054 §10, unchanged by this batch:** Q1–Q13, Q15, Q18 and Q19. Q19 (where dated notes sit in frozen documents)
is unruled, so every note batch 4 added to an existing record is a FILE-END dated note. No earlier text was rewritten
except `docs/m5-playability-gate.md` §6's generated totals line (117594 → 117608 frames), regenerated by the gate's own
command and recorded in that document's file-end notes; §6's in-body provenance paragraph keeps its batch-3 words.

---

## §11 The orchestrator's ruling on the kernel (2026-10-17), recorded as given

The batch-4 orchestrator ruled, on 2026-10-17:
- **The construction → roaddevelopment same-turn read is NOT a kernel change.** `Sim.Core/Kernel/TurnExecutor.cs` keeps
  base-b3's behaviour; at most comment-only edits.
- **`SystemCatalog` hands road development `next.ConstructionHolds` read-only** (as `IReadOnlyTable`); construction is the
  table's sole writer.
- **Order is data.** The shipped pipeline order (construction 10th, roaddevelopment 22nd) is pinned by a test that loads
  the shipped `pipeline.json` (`ConstructionHoldsSchemaV34Tests.TheShippedPipeline_RunsConstructionBeforeRoadDevelopment`).
- **A non-shipped order reads the table as it stands**: the previous step's published set-asides carried into NEXT by the
  kernel's PREV → NEXT copy, or a loaded save's rows (pinned by
  `ConstructionReservationTests.TheEdgeOrderIsData_TheShippedOrderReadsThisStepsHolds_AnotherOrderReadsTheTableAsItStands`
  and `TheV33Migration_IsExactInTheShippedOrder_ARoadFirstOrderReadsTheMigratedWorldsEmptyTable`).
- This is the design spec's §1.4 / §1.8 #22 as amended by T1a round 2.

**History.** T1a's first revisions implemented the read as a kernel edge (`SystemRegistration.SameTurnProducer`, two
invokers resolved by `TurnExecutor`'s constructor). CLAUDE.md: "The kernel is frozen after M0 acceptance without director
sign-off"; no sign-off existed, so T1a round 2 REVERTED `TurnExecutor.cs` to base-b3's file exactly and used the design's
existing non-kernel pattern, the GoodStocks share (one NEXT table handed out in `SystemCatalog`, the order in
`pipeline.json`, no kernel guard). With the rule off road development ignores the table, so batch 3 holds in any order.

**On the merged tree** (VERIFIED by the merge stage): `TurnExecutor.cs` differs from base-b3 only by T3's two
documentation-comment hunks (the `ITurnObserver` summary now names the read-only split report besides the bench, and the
`OnPhaseState` remark names `sim bench` and `sim run --split-report` as the CLI runs that pass an observer); every changed
line is a `///` line. `SystemCatalog` hands road development `next.ConstructionHolds` and records the shared table in its
ownership paragraph. The kernel spec's interaction matrix (P-26/Q-66) is still absent; that paragraph and ADR-040 are the
record, as for the GoodStocks share and `ConsumptionSystem`'s precedent.


> **2026-10-20 — Director (closure ruling R5).** D-055 is accepted as part of the M5 set with its stated scope. O7 (pooled
> scratch arrays) and O9 (a lattice per terrain) are recorded as **NOT IMPLEMENTED** (each would need an ADR); O8 (an
> indexed heap) is **DEFERRED**. None of the three is accepted as completed work. Record: `docs/m5-exit-inventory.md` §13.
