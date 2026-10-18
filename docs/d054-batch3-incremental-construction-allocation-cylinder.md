# D-054 — THE DIRECTOR'S 2026-10-14 BATCH 3: INCREMENTAL CONSTRUCTION, EDITABLE BUILDING/ROAD RATIOS, THE CYLINDRICAL WORLD

**Status: RECORDED 2026-10-15, uncommitted, for the Director's review.**
- The instruction is the Director's own: the batch beginning "We are continuing development of civDemo", workstreams 1–3
  and its integration and validation section. §0 quotes it.
- This record states how it was carried out: the decisions per workstream, what they supersede, the measured movements
  and the moved pins.
- Lines marked **CONFIRM** are choices the instruction did not settle in so many words, or deviations from the batch's
  own design spec. §10 lists them as questions.
- The technical contract (schema v33, order kinds 11 and 12, the snapshot trailer, the migration, the topology seam) is
  `docs/adr/adr-039-schema-v33-orders-11-12-and-cylinder-topology.md`.
- CR-021 (items 3, 4 and 6 of batch 2) is RESOLVED by this batch (§5 item 12).

**Not an M5 exit record, and not authorisation to publish.** M5 stays NOT complete (D-052 §15). Batch 3 exists as an
uncommitted change on top of the uncommitted batch-2 tree of `m5w-integration` (`de86ace`). Nothing is committed,
pushed, merged, tagged or packaged; the instruction reserves each of those for separate approval.

**Provenance of the numbers.** Every figure below was measured by a stage of this batch on a stated tree:
- the merge stage and the residual stage R1, each on its own intermediate tree, and the combine stage, on tree
  `5ab74d3e…` (the batch-3 diff over the batch-2 base, `b3-final.patch`);
- fix round 1, fix round 2, the final nits round, final-round fix 1 and final-round fix 3, which changed the code
  after the combine stage. The nits round's tree is the batch-3 diff `b3-final-r3.patch`, sha256
  `855a14029c906c51fbf973b6960419b230432ff477c5b3f1e53bbe33024a53f7`. The **final code** is that tree plus
  final-round fix 1's changes (2026-10-15): the battery's part-built save point in
  `Sim.Tests/Kernel/IntegratedSaveLoadBatteryTests.cs` (§7), and the example figures in the doc comments of
  `Sim.Core/State/ConstructionQuery.Incremental.cs`, `Sim.Ui/Actions/ActionSurfaceModel.cs` and
  `Sim.Ui/ViewModel/ConstructionStatusText.cs` (comments only, §1); plus final-round fix 3's changes (2026-10-15):
  when the cylinder's size refusal adds its founding sentence, and that sentence's wording
  (`Sim.Core/Worldgen/WorldgenConfig.cs`, §3), the trailer refusal's remedy wording (`Sim.Cli/SaveFiles.cs`, §4), and
  their tests (`Sim.Tests/Kernel/CliSaveAndSizeMessagesTests.cs`, `Sim.Ui.Tests/CylinderLaunchRefusalTests.cs`). Its
  non-docs diff over the batch-2 base (`git diff --binary -- . ':(exclude)docs'`) has sha256
  `5de7149eb1235c8af3a4ac1973bba65e6dd49a9c00b9f9836b190834f38c04c7` (final-round fix 1's code, before fix 3, had
  `9787f380b2bc69320f1de756a877ea22fe7551d368eb079ddc0053a1a5de482a`). Final-round fix 3 ran the full validation on
  the final code (§8); final-round fix 1's run and the nits round's run on `b3-final-r3.patch` are kept in §8 as
  previous results.
- the **worktree validation** (2026-10-15): the final code applied to the integration worktree
  (`/home/user/m5w/integration`, as `b3-final-v7.patch`, sha256
  `fd01056fe6b940075abd7172cf19500935de42e63e2a4ea78cbf2f563af03384`) and validated there (§8);
- **final-round fix 4** (2026-10-15), which added one test and changed nothing else outside docs:
  `CylinderWorldgenTests.TheFlatWorld_SamplesTheNoiseOnTheClosedUnitSquare_ItsLastColumnAtThePeriodEndpoint` in
  `Sim.Tests/Worldgen/CylinderWorldgenTests.cs` (§8, mutant MA2). With that test, the non-docs diff over the batch-2
  base has sha256 `fa72fa6c2fc05f2126c3f8340f261920a032d435616acb12fb3566734330b623`. In this record **"the final
  code" still means the code without it** (`5de7149e…`): that is what final-round fix 3 and the worktree validation
  ran. Fix 4's own runs (§8) cover the tree with the test. After fix 4 the orchestrator corrected that test's doc
  comment (comment only, V8-TEST-DOC-ONLYHASH), so the non-docs sha256 of the tree as applied to the worktree is
  `317ec1b62861ac7ebb2d48254d41749968711b1f1b2d1cbdaaa39e1791561dc6`.

What the fix rounds changed (VERIFIED by comparing the two patches file by file): `Sim.Cli` (`Program.cs`,
`SaveFiles.cs`, `PlatformNotice.cs` and the new `SessionSchemaNotice.cs`); in Sim.Core only
`Kernel/TopologyFlags.cs` (a constant and a pure function, §4) and the size-refusal TEXT in
`Worldgen/WorldgenConfig.cs` (final-round fix 3 added a private constant there that decides only whether the refusal
adds its founding sentence, §3); `Sim.Ui/UiSession.cs` (`Resume` and `SchemaMismatchNotice` line 2, §4); tests and test
fixtures. No simulation system, content switch or calibration item changed. Final-round fix 1 changed only the four
files named above (VERIFIED with `git diff` against the nits round's tree). Final-round fix 3 changed only the four
code and test files named above for it, besides docs (VERIFIED by comparing every file of its tree with final-round
fix 2's tree, `b3-final-v6.patch` applied to a fresh clone). Final-round fix 4 changed only
`Sim.Tests/Worldgen/CylinderWorldgenTests.cs` (one test added), besides docs (VERIFIED with `git diff` against the
`b3-final-v7.patch` tree). A figure measured only on `5ab74d3e…` is marked so where it appears.

The records stage that wrote this file re-measured nothing. It checked the claims it quotes against the code of the
time (`b3-final-r3.patch`) and the result files. Treat them as gov-4 secondary evidence until re-derived. Final-round
fix 1 measured what this file attributes to it (§1's example figures, §4's order logs, §7's save point, §8), and so did
final-round fix 3 (§3's messages and founding sizes, §4's trailer message, §8) and final-round fix 4 (§8's MA2 runs and
its own validation; the worktree validation's figures in §6 and §8 are read from that run's logs, and fix 4 re-made the
comparisons quoted with them).

**Record history.** First written 2026-10-15 against `5ab74d3e…`. Corrected the same day for `b3-final-r3.patch`: §1 (the
termination and exactly-once tests, what "reserved" protects), §3 (the size refusal, the seam pins), §4 (old sessions,
`sim inspect`), §5 (items 15 and 16), §6 (the final calibration run), §7 (re-rigs, the CLI hash legs), §8 (validation),
§9 and §10. The first version's
statements that a batch-2 session is "re-founded on the shipped cylinder … rather than replayed flat" (its queue line)
and "re-founded on the cylinder and replayed, so the world differs, or refused at a non-stride size. They are never
replayed flat." (its Q9) described the pre-fix tree and are no longer true (§4). Corrected again on 2026-10-15 by
final-round fix 1 (verifier findings REC-ORDERLESS, REC-PROGRESS-EXAMPLE, REC-VERBATIM, REC-HISTORY-QUOTE,
REC-FINAL-TREE-TERM and RC1-3-SAMESTATE): the provenance and this history, §1 (the figures example and the builders
status line), §4 (the pre-fix sessions carried orders), §6 (which tree each measurement is from, and the failures
quoted exactly), §7 (the part-built save point, and the tree of the flat control), §8 and §9. Corrected again on
2026-10-15 by final-round fix 2 (verifier findings REC-HEADER-UISESSION and REC-WHEEL-WIDE; docs only, no code
changed): the fix-rounds list above now names `SchemaMismatchNotice` line 2 beside `Resume` in `Sim.Ui/UiSession.cs`;
the wheel-row reading in `m5-hardening-measurements.md` and its line in `m5-playtest-known-issues.md` §11 now say the
1-notch "to Regional" was at 1280×800 (the wide window took 3 before and after). Corrected again on 2026-10-15 by
final-round fix 3 (verifier findings RC1-4-FLOOR-NOT-NAMED and RC1-2-REMEDY-WORDING, which changed two CLI/UI messages,
and the Director question REC-NOTE-PLACEMENT-Q): the provenance and the fix-rounds list above (the final code now
includes fix 3, and its non-docs sha256); §3 (the size message as printed now, when its founding sentence is added,
and the re-measured founding sizes; this record had said the message "names founding's floor", which it never did);
§4 (the trailer refusal's remedy); §6 and §7 (which run the final-code figures are read from); §8 (fix 3's validation
on the final code; fix 1's run kept as a previous result); §9 (the founding-sentence threshold, run times); §10 (Q19,
new). ADR-039 §4 and §6, `m5-playtest-known-issues.md` §11 and `current-state.md` were brought into line with the new
texts. Corrected again on 2026-10-15 by final-round fix 4 (the W1 calibration verifier's finding F6, mutant MA2, a
golden-only kill; and the worktree validation; one test added, no other code changed): the provenance above (the
worktree validation, fix 4 and the non-docs sha256 with its test; "the final code" keeps its meaning); §6 (the nightly
and `corridors.log` are now MEASURED on the final code, no longer INFERRED; the bench's inference no longer leans on
the nightly's; the worktree run's battery messages); §7 (the two unpinned CI hashes and the founded leg, now also seen on the final code); §8 (the
worktree validation subsection, the combine-stage table's rows the worktree re-measured, and fix 4's MA2 record and
validation); §9 (run times). Corrected once more on 2026-10-15 by the orchestrator after fix 4's verifier
(V8-D054-S8-LEADIN): §8's lead-in to the combine-stage table now says the UI resume row's test passed in the final full
runs; `current-state.md`'s top entry now names fix 4, the non-docs sha256 `fa72fa6c…` and the worktree validation
(V8-CS-STALE); the new test's summary no longer says a ContentHash literal was the only check (V8-TEST-DOC-ONLYHASH).

---

## §0 Source (verbatim, the Director, 2026-10-14)

> **Objective.** "Implement ALL previously discussed features that remain unimplemented. Do not stop at architecture
> proposals or documentation. Implement the systems, integrate them, add regression tests, and validate the result."
>
> **Workstream 1.** "Replace the existing whole-building construction rule with the incremental construction behaviour
> previously specified." … "3. Construction materials should be consumed incrementally as materials become available,
> rather than requiring the entire building's material cost upfront. 4. A shortage of one material must not cause
> already completed work to be lost. 5. Projects must not consume resources already reserved by other projects." …
> "9. Define what happens when construction is cancelled, abandoned, or cannot proceed. Follow existing project
> conventions where possible; document any genuinely unresolved design choice. 10. Ensure completed buildings are created
> exactly once and cannot receive duplicate completion rewards or effects." … "Update the previously frozen
> whole-building construction decision explicitly. Do not silently leave contradictory rules in place."
>
> **Workstream 2.** "Implement player-controlled resource allocation ratios for eligible uses, including buildings and
> roads." … "4. Define how allocations behave when the empire lacks sufficient resources to satisfy every allocation." …
> "Do not assume the percentages mean that resources are consumed regardless of actual demand. Specify and implement
> consistent semantics for both available supply and requested demand. Do not leave the current fixed buildings-first
> allocation policy silently overriding player-configured settings. Update the relevant decision records and
> schema/versioning documentation."
>
> **Workstream 3.** "Implement genuine east-west cylindrical world topology, not merely visual camera wrapping." … "For
> this workstream, implement unrestricted horizontal wrapping without fog-of-war gating. EXPLICITLY OUT OF SCOPE FOR NOW:
> - Restricting camera movement according to explored longitudes. - Unlocking wrapping based on exploration coverage. -
> Satellites research revealing the map. - Satellites-based unlocking or other Satellites integration." … "2. Revise the
> architecture decision that prohibits wrapping." … "7. Preserve existing vertical boundaries unless the current
> architecture explicitly specifies otherwise." … "Do not implement wrapping solely in the renderer while leaving
> simulation calculations unchanged."
>
> **Integration.** "Preserve: … Existing save/load guarantees, with an explicit version migration if required. … Existing
> calibration references unless a change is genuinely required and explicitly documented." … "Do not silently modify
> calibration baselines to make tests pass." … "If an architectural obstacle requires a consequential design decision
> that cannot be inferred from existing requirements, stop only that specific decision-dependent part and report the
> exact question."

Boundary (same message): "Do not commit, push, merge, tag, publish, or switch branches. These actions require separate
approval."

## §1 Workstream 1 — incremental construction (supersedes M4-D's whole-building rule)

**Switch:** `sim.json` `construction.incremental`, shipped ON. Absent or false runs the batch-2 M4-D gate exactly. The
control is `TestConfigs.PreIncrementalConstruction`, which also switches WS2 off.

- **Progress and the lockstep (materials first).**
  - Each queue entry carries integer progress p in [0, N], with N = `ConstructionQuery.ProgressUnits` = **1,000,000**.
  - After progress p, exactly `Delivered(c, p) = ceil(c × p / N)` of an input of cost c has gone in (**CEIL**, exact
    long arithmetic). At p = N exactly c has been delivered.
  - So nothing advances from a bucket edge without the next unit of EVERY input. A shortage of one material stops the
    work and loses none of it (Director item 4).
  - **Labour is a pace, not a gate.** With capacity C adult-years and a project of L, a step advances at most
    floor(C / L × N) units. A project that needs more than one turn of builders simply takes more turns; under M4-D such a
    project could never be built at short turn lengths.
  - **dt.** The floor loses at most 1 ppm per step. The completion turn is therefore the first k with Σ dp ≥ N, and a
    project built at two turn lengths completes within one turn's elapsed years of the same date. It is NOT exactly
    dt-invariant, and nothing claims it is.
- **The building order (FIFO).** Only each settlement's head (its lowest slot) progresses. Heads are served **oldest
  order first** by the composite key **(EnqueuedTurn, SettlementId, Slot)**, ascending.
  - An entry with no progress row reads EnqueuedTurn −1. That covers every entry of a migrated v32 save.
  - Each head draws against what earlier heads left this step, through `EmpireStockpile.Draw` (`Ledger.Flow` sinks under
    `ReasonIds.ConstructionMaterials`, in the batch-2 donor order).
  - A tie-dense test pins the key: `BuildingOrderTieDenseTests`, with 17 or more tied heads, which is where `List.Sort`
    stops being stable.
- **Reservation (Director item 5).** A project never consumes:
  - (a) materials already delivered to another project, which left the purse through the Ledger and are sunk; or
  - (b) what a project EARLIER in the building order takes this step.

  The claims (each queued entry's REMAINING needs) are advisory. The crafted-input guard, the AI's ordering and the
  display read them; they set nothing aside. The player-facing text never says "reserved".
  - **Consequence, measured by the verifier (probe, not a lasting test).** FIFO applies only among heads that progress
    in the step. On a Solo world, an older Granary head at S0 (EnqueuedTurn 0, p = 500,000) waits with no builders,
    while a younger Granary head at S1 (EnqueuedTurn 5, p = 0) has full builders. The purse holds exactly S0's
    remainder: 20 timber and 10 stone. After one step both heads are at p = 500,000 and the purse is empty. The next
    turn, with S0's builders back, S0 is still at 500,000. So a younger head can take the whole remaining need of an
    older head that is waiting. **CONFIRM** (§10 Q17).
- **Start.** An order is accepted whenever the project is available (`IsProjectAvailable`); affordability never gated
  it, and still does not. Work begins when the entry is head and has builders plus the first unit of every input.
- **Completion, exactly once (Director item 10).** At p' = N, in the same step: Structures + 1, and the queue row and
  progress row are removed exactly once.
  - The effects begin as any completed structure's always did: institution founding the next step, and the granary
    factors from the next production step.
  - Pinned for an institution (fix round 1):
    `InstitutionLifecycleTests.AUniversityBuiltPieceByPiece_FoundsNothingWhilePartBuilt_ThenExactlyOneInstitution_AndNoLaterStepFoundsAnother`.
    With all 1,800 timber but only 600 of its 1,200 stone, a university reaches p = 500,000 at step 1, waits at step 2,
    and completes at step 3 once the rest of the stone arrives. No institution exists while it is part-built. Exactly
    one is founded the step after completion, and four more steps found no second one.
  - A part-built project is not a structure. It is not counted, drawn, felt by Unrest services or by the Age II
    "Communal store" milestone. A part-built granary's `FarmYieldFactor` and `FamineIntensityFactor` are exactly 1.0.
- **Cancellation: order kind 11 `CancelConstruction` (ADR-039 §3).**
  - It is validated AGAIN at delivery, where it is consumed: a roster actor, control of the settlement on PREV, the
    payload re-checked, the slot present in this step's queue. Anything else is ignored, never floored.
  - It is applied before this step's enqueues and before any building work.
  - **NO REFUND.** Delivered materials and the labour spent are forfeited; they were sunk as the work went in. A salvage
    refund would need a new ledger source reason and the four-site audit identity. **CONFIRM** (§10 Q1).
  - The next slot becomes the head with its own progress (0).
  - A cancel and an enqueue in one batch read this step's tables, so a re-enqueued entry never inherits forfeited
    progress.
  - **Deviation, recorded:** kind-11 cancels are honoured with `construction.incremental` OFF too. Steps 1, 3 and 4 of
    the step run in both modes.
- **Cannot proceed.** The head waits and KEEPS p (no decay, no timer) while:
  - materials are short;
  - it has no builders (C = 0);
  - an institution host cannot sustain it (viability, checked every step before any progress);
  - a crafted input cannot be made;
  - its project has left the content.
- **Abandoned = structurally stalled.** A head that cannot advance for a STRUCTURAL reason is reported as such: no
  labour adults at all, viability, an unobtainable crafted input, or an unknown project. The player sees the reason and
  may cancel. The AI cancels its OWN structurally stalled heads with an ordinary kind-11 order before it orders anything,
  so a dead head never deadlocks a queue or its claims.
  - The two content-shaped stalls are pinned (fix round 1, after the verifier's mutants MU1 and MU2 survived the
    construction tests):
    - `ConstructionTerminationTests.AHeadStalledOnAnUnobtainableCraftedInput_WaitsKeepingItsProgress_IsStructural_AndTheAiCancelsIt`;
    - `ConstructionTerminationTests.AHeadWhoseProjectLeftTheContent_WaitsKeepingItsProgress_IsStructural_AndTheAiCancelsOnlyItsOwn`;
    - `IncrementalConstructionUiTests.AHeadWhoseProjectLeftTheContent_AndOneStalledOnAnUnobtainableCraftedInput_SayWhyTheyWait`,
      which pins "waiting: this project is no longer known" and "waiting for tools: the empire has none and nothing
      here can make it yet".
  - In both, the head waits keeping p, draws nothing and is structural. The AI cancels its own head with no refund, and
    never the player's. MU1 and MU2 are now killed in both projects.
  - A viability wait counts as structural for the AI whether its cause is the market or the food surplus, as specified.
    The published food-surplus ratio moves with each harvest, so a bad harvest can make the AI forfeit a part-built
    university. The C1 verifier measured the stake at 60 % progress: 1,080 timber, 720 stone and 144 adult-years.
    **CONFIRM** (§10 Q3).
- **Controller change** (the existing convention, made explicit). The queue and its progress belong to the SETTLEMENT.
  After a revolt the new controller's purse pays, and the new controller may cancel; the old one may not.
- **Slot reuse.** `NextSlot` restarts at 0 when a queue empties. The old "never reused" docs were false and are corrected.
  Safety comes from reading this step's tables, matching ProjectId on every read, and the queue/progress invariant
  (ADR-039 §2).
- **AI ordering: a deliberate exception.**
  - The system lets anyone start without the full cost. The AI's ordering stays more cautious: it still orders only into
    an IDLE queue, and only when its reserved purse covers the project's whole remaining inputs (batch-2 prudence).
  - It also orders no ordinary project into a settlement with no labour adults (recorded deviation: the head would stall
    at once).
  - A university site needs floor(C / L × N) ≥ 1 (it was: capacity ≥ L this turn).
  - **Measured consequence:** WS1 never acts in the shipped order-less AI world. The AI's heads always finish in their
    delivery step: 0 turns end with a progress row, and its 300-turn world is byte-identical with WS1 on or off. The
    change reaches games through player orders. Example: the save/load battery's university first reaches
    mid-maturation at turn 26, not 34.
  - **CONFIRM** (§10 Q6).
- **The head-status report** adds one cause the design spec did not list: `MaterialCause.Short`, for "the purse holds
  some, but fewer than the builders could put in this step". Recorded as a deviation.
- **UI (Director item 7).**
  - **Queue rows.** One row per entry. The head carries era progress marks and a figures line with no '%' in any era,
    for example "16 of 40 timber, 8 of 20 stone and 0.8 of 2 adult-years have gone in" (a granary at p = 400,000
    of 1,000,000, the figures `IncrementalConstructionUiTests` pins; the lockstep delivers ceil(cost × p / N) of each
    input, so 17 timber, at p in (400,000, 425,000], comes with 9 stone, never 8).
  - **Status lines.** "waiting for stone: the empire has none", "… ordered earlier, takes the empire's 12 first", "road
    works hold the rest …", "waiting: no builders here - give <the knowledge-derived sector label> a share in Labour"
    (the code's ASCII hyphen, `ConstructionStatusText.Status`), "the town cannot yet sustain it".
  - **Priority line.** "Building work is served oldest order first: …".
  - **Cancel.** A Secondary button on committed rows. A head with p > 0 needs a two-step confirm, Danger "Lose it", whose
    warning names what is lost. Rows ordered this turn have no Cancel.
  - **Build plates** say "will wait for: …".
  - **Foreign settlements** show "Being built: X" and progress marks at most, never a reason.
  - The readability roles (Body/Second in Progress or Danger ink, Caption explanations, Secondary Cancel, Danger "Lose
    it") are pinned in `IncrementalConstructionUiTests`.

## §2 Workstream 2 — the building/roads material split

**Switch:** `sim.json` `construction.allocationRatios`, shipped ON. The control is `TestConfigs.PreAllocationRatios`.
The LOADER REFUSES the switch unless `construction.incremental` and `construction.empireStockpile` are both on: a
whole-or-wait head capped below its cost could never build, and the split is an Empire's.

- **The setting.** Each Empire has a whole-number building share b in [0, 100]; road works get 100 − b. It is stored in
  `ResourceAllocationRow` (v33) and set by order kind 12 `SetResourceAllocation` (ADR-039 §3).
  - **No row means b = 100: "Building first (default): road works get only what building work leaves"**, the batch-2
    behaviour.
  - AI Empires never set it. **CONFIRM** the default of 100 against a neutral 50, which would move AI behaviour (§10
    Q2).
- **Eligible uses.** The two uses that draw the Empire purse BY ORDER: building projects (the construction queue) and road
  works (DevelopRoads).
  - **Housing is excluded.** Dwellings are needs-driven upkeep, drawn from the settlement's OWN timber (and clay) before
    construction. They are never ordered and never drawn from the purse. The UI explanation, the code header and this
    record say so.
  - **Eligible goods** are the union of every goods.json project input and every roads.json class material, read from
    content, never from a code list.
- **The rule, per Empire, per good, when building work starts** (after production, trade and housing):
  - **S** = the Empire purse's holding;
  - **D_b** = what its queue heads would put in this step with no split (builders and the lockstep, oldest order first);
  - **D_r** = what the road order road development will apply for it this step would be charged against S, by road
    development's own arithmetic. That arithmetic is read through two pure State functions,
    `RoadDevelopmentQuery.DeliveredOrderFor` and `SimulateCharges`; no system calls another (law 6). D_r is 0 with no road
    order.
  - **E_b** = floor(S × b / 100), computed exactly as `(S / 100) × b + (S % 100) × b / 100` (no overflow for any long
    S). **E_r** = S − E_b.
  - **G_b = min(D_b, E_b + max(0, E_r − D_r))**: building work may take this much in all, served in the building order.
    Road works, later in the step, draw from what physically remains.
  - So the road share road works do not need flows to building work, and the building share building work does not use
    flows to roads. Road works can always find at least min(D_r, E_r).
- **Semantics against demand (the Director's "supply and requested demand").** The percentages are GUARANTEED SHARES OF
  SUPPLY, never amounts consumed.
  - A use draws only what its own rule needs. Nothing moves except through the two systems' Ledger draws, and no stock
    goes negative.
  - **Shortage** (Director item 4): each side is guaranteed its share of what exists, and whatever one side does not
    need, the other may use.
  - With no road order, building work may use everything.
  - b = 100 never binds (G_b = min(D_b, S)). It is skipped, and is byte-identical to the switch off.
- **The starvation guard, GENERALISED. A deviation from the design spec, recorded here as the C2 fixer asked.**
  - The spec withheld nothing for a road order that could pay nothing from the WHOLE supply (D_r = 0).
  - The implementation also withholds nothing when road development would charge NOTHING on what building work would
    LEAVE it under the split. ADR-032's min-ratio can floor a partial step to nothing; for example, two tools needed and a
    quarter of the timber left pays neither.
  - The mechanism, in the C2 fixer's words: "After the split, the Empire's own heads are resolved again within the
    grants … This gives the forecast building draws A. RoadDevelopmentQuery.SimulateCharges is then run on S − A. If the
    split holds anything back and road development would charge nothing on that remainder, the split is recomputed with
    D_r = 0, so G_b = min(D_b, S)."
  - By the monotonicity of ADR-032's charge, road works still pay nothing, so no unit is ever held idle for a road that
    cannot use it.
  - **What the guard does not promise.** A road order that can pay only SOME of its share may still leave part of that
    share unused for the step. The guarantee is availability, min(D_r, E_r), not consumption. **CONFIRM** (§10 Q7).
- **Stated edge cases:**
  - b = 0: building work uses only what roads do not need.
  - A stateless settlement, or a world with no Controls: no split.
  - **Two Empires on a shared route in one step:** the second Empire's D_r is planned on PREV edges. This is a stated
    approximation of the forecast, never a conservation effect.
- **Order semantics.** An order stamped t governs the split of the step t → t+1 (the SetResearchTarget precedent; pinned
  turn-exact). The last valid order of an Empire in a step wins.
- **The books.** Both uses sink under `ReasonIds.ConstructionMaterials`, so **the per-use split is not separable in the
  ledger books**. The design spec's optional `RoadMaterials` reason id was NOT done (§10 Q12).
- **Action descriptor domain: a deviation.** The spec said "Domain Roads". The implementation adds
  **`ActionDomain.ResourceAllocation = 14`**. The reason: `InfoQuery` (`Find(c, ActionDomain.Roads, rc.EdgeType)`) and
  `ActionSurfaceModel.Roads()` assume every Roads descriptor is a road class whose Id is an edge type, and Id 1 is
  `EdgeTypes.DirtPath`, so a Roads-domain allocation descriptor would collide.
- **UI (Director items 1, 7).** A "Materials for building and roads" block sits between Construction and Roads. It is
  shown exactly when the allocation descriptor is listed.
  - **Numeral-free eras (A1–A3):** a share tally, "building work keeps 7 shares in 10, road works 3", with no inline
    numeral.
  - **Numeral eras:** "Building 70% · Roads 30%".
  - b = 0 and b = 100 are both reachable. Set appears only when the draft differs. The default line and the queued line
    are shown.
  - **Previews.** The construction block's "may use up to" line and the roads preview ("after building work, about …
    is left") are labelled estimates. The EMPIRE view line is read-only and uses the same visibility predicate as the
    block.
- **Cross-process coverage.** No lasting CI leg makes the split BIND; the CI founded-mix leg issues kind 12 but no
  binding road scarcity. Coverage is in-process: replay, snapshot resume, a randomised property test and a one-off
  two-process probe (the C2 fixer's; not a lasting artifact).

## §3 Workstream 3 — the east-west cylinder (world, simulation, rendering)

**Switch:** `worldgen.json` `topology.wrapX`, shipped ON. The control is `TestConfigs.PreCylinderWorldgen`. ONLY
`Worldgen.Generate` reads the config field; everything else reads `TerrainSet.WrapX`, and `MapTopologyGuardTests` enforces
that.

- **The world itself.** x is periodic with period Size. Moving east past the eastern edge returns to the western edge,
  and the reverse. y keeps its hard edges, so **the poles stay ocean** (Director item 7).
  - The edge taper is y-only: `min(y, size − 1 − y)`.
  - The value noise is periodic in x. The loader refuses a non-integer octave period and names the octave.
  - The radial continental mask is unchanged. It is symmetric, so it is continuous across the seam and one landmass
    stays centred.
  - Hydrology wraps x, and its border sinks are on the poles only.
  - The slope stencil, the river- and water-distance searches, the siting windows and the founding hinterland means all
    wrap.
  - With wrapX false, every one of these is byte-identical to batch 2 (`WrapAttributionTests`).
  - Shipped canonical seeds whose land crosses the seam: **6, 8, 14, 16, 17, 18 and 20**. Seed 6 also has a river
    crossing it.
- **Sizes.** A cylinder needs a size the lattice stride (4 px) divides into at least 3 nodes. Any other `--size` is
  refused (`WorldgenConfigLoader.ValidateTopology`, shared by the CLI and the UI launcher). The CLI exits 2; the UI
  writes stderr, shows its dialog and exits 2. Only a flat world, through the test and tool override, takes such a
  size.
  - **The message, final wording** (the nits round, verifier RC1-4; final-round fix 3, verifier
    RC1-4-FLOOR-NOT-NAMED), for `--size 8`, printed after "error: ":

    > this world is a cylinder (worldgen.json topology.wrapX), which needs a map size the lattice stride divides into
    > at least 3 nodes: size 8 px, stride 4 px. The cylinder's own rule is a multiple of 4 px, at least 12 px — the
    > nearest size the cylinder takes is 12 px. That rule is the cylinder's alone: founding also needs room to site
    > every settlement, and at the default settlement count, settlement siting can refuse maps this small, with its own
    > message.

    For `--size 250` it ends at the nearest sizes: "… at least 12 px — the nearest sizes the cylinder takes are 248 px
    and 252 px." The last sentence ("That rule is the cylinder's alone: …") is added only when a size the message names
    is below 124 px: `--size 122` (120 and 124 px) carries it, `--size 126` (124 and 128 px) does not.
  - **Why this wording.** The earlier versions told the user to "Use a size that is a multiple of 4 px and at least
    12 px", and founding then refused 12 px. Measured by the nits round with `sim run --founded --turns 1 --size N`:
    settlement siting refused 12, 16, 20, 24, 32, 48 and 64 px on seeds 42, 1 and 7, and 96 px on seeds 42 and 1;
    128 px founded on all three. The message gives no founding size on purpose: that floor depends on the seed and the
    settlement count. 248, 252 and 256 px all found and run (VERIFIED by the verifier and pinned by
    `CliSaveAndSizeMessagesTests.TheNamedRemedy_Works_SimRunAt248Px_FoundsAndRuns`).
  - **When the founding sentence is added** (final-round fix 3, 2026-10-15). The nits round's sentence read "… and at
    the default settlement count a map far larger than 12 px is needed (settlement siting refuses a smaller one with
    its own message)." It named no founding size, although this record and the code comment said it did, and it was
    added to every refusal: MEASURED before the fix, `sim run --founded --seed 42 --turns 1 --size 250` and
    `sim worldgen --seed 42 --size 250`, which founds nothing, both ended with it. It now claims only that siting CAN
    refuse maps this small, and is added only when a size the message names is below 124 px, the smallest size founding
    was measured to succeed from. Re-measured with `sim run --founded --seed S --turns 1 --size N`: on seeds 42, 1 and 7
    at every size the cylinder takes from 12 to 140 px, siting refused every size up to 88 px on all three; seed 7
    founded from 92 px and seed 1 from 104 px; seed 42 founded at 116 px, was refused at 120 px ("could only place 10
    of 12 sites") and founded from 124 px. On seeds 1–20 and 42 at 100–160 px, every size from 124 px founded on all
    21. Pinned by `CliSaveAndSizeMessagesTests` (the size theory checks, on seed 42, that the sentence is there exactly
    when siting refuses a named size; `sim worldgen` at 250 and 18 px) and `CylinderLaunchRefusalTests` (6, 8 and
    250 px; the launcher at 8 px). The CLI and the UI launcher keep exit code 2 and their reporting path.
  - `sim worldgen`'s own pre-existing `--size >= 16` floor is a separate command's guard and is unchanged.
- **Simulation.** Every x delta, x neighbour and x normalisation goes through **`Sim.Core/Pathing/MapTopology.cs`**
  (ADR-039 §6). The sites covered:
  - the Pathfinder (expand, isochrone, capped relaxation, leg costs, an admissible wrapped A* heuristic, `MinPerUnit`);
  - `LatticeMap.OriginLatticeNode`;
  - `PathBuildSystem.PassableComponent`;
  - `PolityKnowledge`'s node lookup;
  - `GeographicDistance` (and with it the weather kernel);
  - `RoadDevelopmentQuery.SquaredPx`, which gives the donor order for roads, the purse and relief;
  - `SiteAssessment`;
  - a formation's X at founding.
- **Road length: the ADR-032 FR3 amendment.**
  - A new road's length is the straight-line distance between the two site cells **measured on the cylinder (the
    wrapped geodesic: the shorter way round in x)** × `KmPerPx`.
  - This is the same as-the-crow-flies abstraction FR3 already applied to a bay between two land-connected settlements.
  - `roads.maxRouteKm` candidacy uses it, and roads are drawn the short way, consistent with their length.
  - The invariant FR3 protects (a road's length never depends on network performance) is unchanged.
- **Movement.** **No unit movement system exists** (M7, D-052 §4; order kind 10 stays reserved for MoveFormation,
  `OrderLog.cs`). A formation's X is written only at founding, through `WrapCoord`.
  Every existing travel mechanism wraps: catchment, `SettlementDistances`, migration, trade, colonisation reach, the AI
  founding flood, FoundingQuery and the placement mode's routes, and PathBuild. Nothing claims unit movement.
  - **Flows across the seam** are proven on the synthetic `SeamWorld` (`SeamWorldSystemTests.MigrationAndTrade_FlowAcrossTheSeam`):
    migration and trade both cross it.
  - **On the shipped world** (the nits round, verifier WS3-MOVE), a bounded search of about 25 minutes ran the
    canonical cylinder for 300 turns on each of seeds 6, 8, 14, 16, 17, 18 and 20, orderless and with the trade
    knowledge rig (`TestConfigs.KnowTrade`) at turn 0, routing every settlement pair with `RouteQuery`. Measured:
    - only seed 16 has settlement pairs whose shortest route crosses the seam (three, all with its settlement 6);
    - no seed had any `TradeFlows` row, orderless or with the trade rig alone;
    - orderless seed 16 had no migration step attributable to seam pairs alone;
    - seed 16 with trade AND recipe knowledge at turn 0 (`KnowTrade` + `KnowRecipes`, the SeamWorld rig) traded 6,684
      units in 300 turns, none between a seam pair, and had two migration steps that came only from across the seam.
      The first is step 94 → 95: settlement 2 gains one migrant whose only possible sender is settlement 6.
  - That migration is now pinned:
    `SeamCrossingShippedWorldTests.Seed16_WithTradeAndRecipesKnown_AMigrantCrossesTheSeam_FromTheOnlySettlementsThatMaySendTo`.
    It is an EXISTENCE assertion within 120 turns, because the event is a single migrant. Its condition: a settlement's
    inflow is positive and every settlement whose `MigrationSystem.Plan` push or flight weight toward it is positive
    has a seam-crossing route to it. The mutants "NeighbourX never wraps" and "push zeroed on seam pairs in the
    transfer loop only" are both killed.
  - **Not available:** an orderless shipped-world seam flow, and trade between a seam pair on any of the seven seam
    seeds. Trade across the seam is pinned only on SeamWorld. **CONFIRM** (§10 Q18).
- **AI.** The AI uses the same Pathfinder, Isochrone and distances as the player. No AI policy has its own geometry:
  the W1 stage reported `AiConstructionPolicy`, `AiGovernance`, `AiResearchPolicy` and `AiOrders` free of raw x
  geometry, and the AI founding flood uses `LatticeMap` plus `Pathfinder.Isochrone`. The source guard keeps it so. A player-versus-AI parity test on a seam route compares `FoundingQuery.RouteFrom` with
  the `AiFoundingPolicy` flood.
- **Entity uniqueness.** Settlements, sites and deposits keep their ids and cells. Nothing is duplicated in state; a
  formation's X is normalised. Copies exist only in rendering.
- **Shipped-world pins.** On canonical seed 16, the route from settlement 3 to settlement 6 crosses the seam and equals
  the catchment's travel cost, and the same ground laid flat has no such shortcut (`SeamCrossingShippedWorldTests`,
  3 tests with the seam-migration pin above). CI now runs a two-process 300-turn seam leg on seed 16.
- **Rendering and camera** (Sim.Ui; no world hash is affected).
  - **The zoom floor is the WINDOW's width / WorldSize** (a window taller than wide floors on its height instead). At the
    floor the window shows at most one circumference, so no world point is ever on screen twice, and opening or closing
    a panel never re-zooms.
  - The camera's x is normalised, its moves take the shortest wrapped delta, and y is clamped as before.
  - The game opens centred on the player's opening settlement.
  - **The level-of-detail band on a wrapped world is keyed on visible km across the WINDOW's width**, not the map rect's
    width the spec named. The measured reason: keyed on the map rect with a panel shown, the 256-px world's floor sits on
    no plateau at any gate window. **CONFIRM** (§10 Q4).
  - **Copies.** Each lens element is drawn at every copy k ∈ {−1, 0, 1} whose bounds meet the window, with one hit record
    per drawn copy. Frame counters count distinct rows. Rivers, routes and placement routes are unwrapped relative to
    their first point.
  - **Whole-world overview lost.** At the floor the window shows **47–54 % of the world's height**: 53.8 % at 1280×800,
    48.2 % at 1382×778, 46.7 % at 1920×1009 and 50.2 % at 2560×1440, on the canonical 1024-px world. A minimap is
    QUEUED, NOT BUILT (`docs/queue.md`). **CONFIRM** (§10 Q5).
  - **The parchment bake.** It is x-periodic: field samples, the coast's chamfer distance across the seam, and the
    displayed texel. The wash tiles and paper CROSS-FADE (smoothstep) over the last 32 world px (a quarter of a world
    narrower than 128 px) into their value one circumference west, which works for any `--size`.
  - **The acceptance metric is a ROW MEAN.** The seam column's mean |ΔRGB| must not exceed the neighbouring interior
    columns' maximum over the same rows, and must stay at or below 1.10 × the mean of 64 mid-map column pairs. This is
    measured on the canonical and 256-px worlds, in every era palette.
  - **What it cannot see: a partial or narrowed fade.** Mutants F9a (fade capped at 0.5), F9c (capped at 0.8) and F9b
    (an 8-px fade) all SURVIVE. Under F9a the seam measured 14.60 against 14.16 mid-map. A low-frequency probe puts even
    the UNFADED seam below the interior's typical step, because the periodic fields carry the broad colour.
  - Under ADR-015 §7.2 these survivors are recorded as teeth without aim: the test does not catch them, and whether the
    eye sees them is for the playtest. **CONFIRM** (§10 Q11).
  - **Not testable headless.** The GPU passes (the U-wrapped terrain sampler, the river buffer drawn per copy) cannot run
    headless. They are covered by the `VisibleCopies` tests, the bake ΔRGB test and the **manual playtest**
    (`docs/m5-playtest-known-issues.md` §16). **CONFIRM** (§10 Q10).
  - **One untested GPU case.** `--size 1000` gives a 2000-texel sheet that is not a power of two. The shipped MonoGame has
    no Reach guard against wrapping it (measured). That desktop GL repeats such textures is INFERRED, not tested.

## §4 Schema v33, the trailer and the migration (summary; ADR-039 is the contract)

- **v33 appends two tables:**
  - section 59 `ConstructionProgress`: (Settlement, Slot, ProjectId, EnqueuedTurn, Progress), 28 bytes a row;
  - section 60 `ResourceAllocations`: (Polity, BuildingsPercent), 8 bytes a row.

  No existing row changed width.
- **A snapshot ends with an int32 topology trailer**, bit 0 = wraps east-west. It is outside the canonical stream, so
  the world hash never sees it.
- **The first explicit migration.** A v32 save loads as a FLAT world with both new tables empty. Every other version is
  still refused (D-008).
- **One rule for the topology a record implies** (fix round 1, verifier F1): `TopologyFlags.RecordedFlat(v)` =
  `v < TopologyFlags.FirstCylinderSchema` (33). Every world recorded under a schema before v33 was flat, so every such
  record is rebuilt FLAT. Three readers share the rule: `UiSession.Resume`, `sim inspect` and
  `Sim.Cli.SaveFiles.WorldgenFor`.
  - A v33 SAVE is regenerated with the topology its trailer names (ADR-039 §4).
  - A v33 SESSION manifest records no topology. It is rebuilt with this build's shipped worldgen, the one it was
    founded with (§10 Q9).
- **The CLI and saves.** `sim hash` and `sim diff` regenerate a v32 save's terrain flat and print a one-line migration
  note on stderr. A v33 save is regenerated with its trailer's topology, so a flat v33 save loads instead of being
  refused (fix round 1, verifier F2). A v33 save whose trailer's wrap bit alone is wrong is refused with a message
  naming the bit (the nits round, verifier RC1-2; ADR-039 §4 quotes it). Its remedy now reads "… set bit 0 again to
  load it, or save the world again from the session that made it." ("clear" for a flat save whose bit was set);
  final-round fix 3, verifier RC1-2-REMEDY-WORDING. It read "… set bit 0 again, or save the world again, to load
  it.", which a user holding only the file cannot act on.
- **UI sessions (final behaviour).** A batch-2 UI session (manifest schema 32) is re-founded FLAT, on the map it was
  played on, and replayed through the D-008 schema-mismatch path without the trace check. The ADR-034 order checks
  still apply.
  - The resume notice keeps four lines. Line 2 now names the map: "Replayed from its seed and orders on the flat map
    v32 used (D-008)." The other three lines are unchanged: "Recorded under save schema v32; this build is v33.", "The
    saved trace was not checked against this replay." and "The world may differ from the one originally played." A
    session recorded under any other schema keeps line 2 as "Resumed by replaying its seed and orders (D-008)."
  - **Measured on a real batch-2 session**, the fixture `Sim.Tests/Fixtures/v32-session-found/` (written by the batch-2
    build; its README gives the command and the sha256s). It is seed 42 at the canonical size, with two FoundSettlement
    orders whose sites are legal only on the flat world. The batch-2 build reported hash `6157dfde1b3c9dc9806fca088d2042831bcc3f344692e5076039947d7fe756c4`
    at turn 8. This build's resume reaches turn 8 with WrapX false and 14 settlements, and the resumed world's v32
    projection equals `6157dfde…` (`Batch2SessionResumeTests`, 3 tests; VERIFIED in the final full Sim.Ui.Tests run).
    The premise control: the same log on the shipped cylinder is refused at the second FoundSettlement.
  - A batch-2 session at a size the cylinder refuses (250 px) now resumes flat instead of being refused
    (`Batch2SessionResumeTests.ABatch2SessionAt250Px_ResumesFlat_InsteadOfBeingRefusedByTheCylinder`; the session is
    recorded flat by this build and stamped v32).
  - **What is still not faithful.** The topology is now the recorded one, but the SIMULATION rules are this build's:
    WS1 and WS2 are on. A batch-2 session in which a construction head waited for materials or builders can replay to
    a different world (INFERRED from the mechanism; the battery's WS1-alone arm on a flat world measures WS1 moving a
    university's maturation 34 → 26). The fourth notice line says so. **CONFIRM** (§10 Q9).
  - **Superseded pre-fix behaviour** (measured before fix round 1 by the merge stage and the verifier; recorded for
    provenance): such a session was re-founded on the cylinder. At 1024 px and 256 px one without FoundSettlement
    orders resumed onto a different world (turn 6 at `854af265…` and `efdc1d97…` against the recorded `548f0e27…` and
    `81e70efc…`). Neither was orderless: the 1024-px log held five sector-allocation orders (kind 3, turn 2), and the
    256-px log the same five plus one AI research order (kind 6, turn 0), decoded from the merge stage's order files
    by final-round fix 1. One with
    FoundSettlement orders was refused with exit 2 ("error: order (turn 5): FoundSettlement targets settlement 12, which
    does not exist when the order is delivered …"), and any at 250 px was refused with exit 2 by the cylinder's size
    rule.
- **`sim inspect` of a session recorded under another schema** (fix rounds 1 and 2, the nits round). One condition,
  `SessionSchemaNotice.HashesChecked(schema)` = schema equals this build's `CanonicalSchema.Version`, drives the
  platform block, the recipe line and the verdict.
  - A pre-v33 session is rebuilt flat. The verdict is `SessionSchemaNotice.NotChecked`, for example: "trace not
    checked: the session was recorded under save schema v32 and this build is v33, so no replayed hash can equal a
    recorded one (D-008). Replayed from its seed and orders on the flat map v32 used; the rows below are this
    replay's." Before fix round 1 it printed "REPRODUCTION FAILED at turn 0" for an orderless batch-2 session and exited
    2 for one with FoundSettlement orders.
  - Only the plain platform provenance line prints, with no "a hash mismatch below is a determinism finding" line and no
    NOTICE paragraphs, because no hash is compared.
  - The recipe lines are quoted in ADR-039 §7 and in the dated note of `docs/session-records.md`. For a pre-v33 session
    they point to `sim inspect` itself and give the `sim replay` recipe as the recording build's. For a session a
    NEWER schema recorded, they give only the recording build's recipe.
  - An EMPTY trace, under any schema including this build's own v33 sessions, gets the plain platform line and "no turns
    recorded in <trace file> — nothing to inspect.", and no recipe.
  - **`sim replay` has no flat path.** This build's `sim replay --founded` always founds the shipped cylinder, so it
    cannot reproduce a pre-v33 log. `sim inspect` and `UiSession.Resume` are the rebuild paths. A `--manifest`-driven
    flat override on `sim replay` was declined as scope widening (§9, §10 Q9).

## §5 What this supersedes, and what it does not

1. **M4-D (CERTIFIED)**, `docs/milestones.md` "WHAT M4 DELIVERED" and `docs/current-state.md` §4: "that head is either
   built whole this turn or waits unchanged. No build timer, no fractional progress, … Materials leave settlement good
   stocks all-or-nothing". SUPERSEDED with `construction.incremental` on.
   - Progress is stored.
   - Materials go in as they become available.
   - Labour is a pace.
   - **Standing:** the ordered queue with only its head eligible; "no construction currency, no second labour pool";
     capacity is the existing construction sector less housing's published draw; an Empire builds only where it rules.
2. **The v24 row contract** (`WorldState.cs`, `ConstructionQueueRow`: "There is NO progress field here, deliberately").
   The row is unchanged. Progress lives in the appended v33 table, and the code doc says so.
3. **D-053 §1:**
   - "The M4-D gate is unchanged: a head is built whole or waits" (superseded, item 1).
   - "Queue heads are tried in ascending settlement id" (now the FIFO building order, §1).
   - "This fixed priority (buildings, then roads) is what exists until item 4 is ruled" (now the split, whose default
     100 keeps that priority).
   - "(no cancel order exists)" (kind 11 exists).
   - **§5.4**'s "leave the empire's settlements' good stocks, all-or-nothing": the stocks are the same; the
     all-or-nothing is superseded.
   - **§5.6**'s "CanonicalSchema v32: no table, row field or order kind added" stays true of batch 2. Batch 3 adds v33
     and kinds 11 and 12.
4. **ADR-038 §2 "Gate"** (heads in PREV `Settlements` order; "the M4-D whole-or-nothing gate … unchanged") and its
   header's "`CanonicalSchema.Version` stays 32". Both are true of batch 2 and superseded by batch 3 (ADR-039).
5. **ADR-033 D10's "no bank"**, for construction PROGRESS only. Progress is now banked as state.
   - D10's rule stands: construction capacity is spent once, and PathBuild subtracts the published labour at a one-turn
     lag.
   - D10's "unused capacity disappears" was inaccurate before this batch: construction capacity a head does not use is
     banked by PathBuild toward dirt paths, as it always was.
6. **D-052 §5:** "No visibility state and no save-format change ship in M5: `CanonicalSchema.Version` stays 32."
   - The save-format half is SUPERSEDED: v33 ships.
   - The visibility half STANDS: no fog, no known extent and no explored-longitude state. The Director put these out of
     scope.
7. **ADR-005's "version mismatch rejected outright (D-008: no migration)":** superseded for v32 → v33 only, by ADR-039's
   explicit migration. Every other version is still refused with the D-008 message.
8. **ADR-032 Final Ruling 3 (§12.3)**, "the straight-line (flat-raster Euclidean) distance … The map is a flat square
   raster (no wrap), so this is the existing geometry": AMENDED. It is the straight-line distance on the cylinder (§3).
   §12.1's "exact INTEGER squared pixel distance, on the terrain raster" is now the wrapped squared distance
   (`MapTopology.SquaredPx`). The Director's item 2: "Revise the architecture decision that prohibits wrapping".
9. **D-015** (`docs/m1-walking-skeleton-spec.md` §1), "a 4,096 km square world": still 1024 × 1024 px at 4 km/px, now an
   east-west CYLINDER of circumference 4,096 km with hard poles. The rasters are unchanged in size.
10. **D-009** (the continuous-world model): STANDS. Its map was implicitly flat; it is now a cylinder. The continental
    mask is unchanged, so one landmass stays centred.
11. **T3.1(b)'s edge taper** (`docs/queue.md`, "no land touches any world edge") and `NoLandAtTheWorldEdge_TenSeeds`:
    the x half is SUPERSEDED (land may cross the seam, and the terrain is continuous across it). The y half STANDS (the
    poles).
12. **CR-021: RESOLVED by this instruction.**
    - §A (item 3) and §B (item 4) are implemented.
    - §C (item 6) is implemented as the cylinder (W2) and its rendering (W3), unrestricted. Its W1 (known extent,
      explored-longitude gating, Satellites) is explicitly OUT OF SCOPE "for now", and neither built nor faked.
    - CR-021's proposed numbering is superseded: §B.3's `SetResourcePriority = 11` became kind 11 = `CancelConstruction`
      and kind 12 = `SetResourceAllocation`; `ResourcePriorityRow(PolityId, double)` became
      `ResourceAllocationRow(PolityId, int BuildingsPercent)`.
    - §A.3's `ConstructionProgressRow(Settlement, Slot, Good, long Delivered)` became one lockstep progress per entry.
    - §A.3's "no refunds, because no cancel order exists" became: a cancel order exists, and there is still no refund.
13. **The m5w plan's reservations** (`docs/m5w-map-workstream-audit-and-plan.md`, coupling map (c): "v33 = the first
    schema bump to land"). Batch 3 is that bump. S2a's known extent moves to v34, and S3's Action Points table to the
    next free version. Kind 9 and kind 10 are unchanged.
14. **ADR-008:** the decision stands, and the terrain content hash is still folded into every world hash.
    - `TerrainSet.FromLayers` is added as a test and tool factory; the game never calls it.
    - `TerrainSet.WrapX` is NOT folded into the content hash (ADR-039 §5).
15. **ADR-034 §4 and `docs/session-records.md`** (resuming and inspecting old sessions).
    - ADR-034 §4 quotes the four-line `SchemaMismatchNotice` with line 2 "Resumed by replaying its seed and orders
      (D-008)." For a pre-v33 session line 2 now reads "Replayed from its seed and orders on the flat map v32 used
      (D-008)." (§4).
    - ADR-034 §4's limitation "`sim inspect` is unchanged. It prints the manifest's schema in its header and compares
      hashes regardless" is superseded: `sim inspect` compares hashes only under this build's schema (§4).
    - `docs/session-records.md`'s platform-line section describes the hash-check lines and a `sim replay` recipe. For a
      cross-schema, pre-v33 or empty-trace session the output is now the plain platform line and the recipe lines of
      §4.
    - Both carry dated notes.
16. **Other standing statements of the whole-building rule or the flat map**, each given a dated note:
    - `docs/institutions-universities.md` (Materials and labour): "The project is built whole in one turn or waits";
    - `docs/queue.md`'s M4-D entry: "resolved atomically within a turn — no build timer, no fractional progress …
      all-or-nothing";
    - `docs/m3-spec.md` T3.1(b): the edge taper's ocean margin at the world boundary (its x half, as in item 11);
    - `docs/queue.md`'s P1 "fit-to-width (cover) zoom floor" entry: on the cylinder the zoom floor is now the window's
      width (§3).
17. **Not touched:**
    - every frozen calibration item: the `CalibrationBatteryTests` literals, `corridors.json`, the [71,000, 119,000]
      envelope, the per-seed migration values, the famine-disaster hazard (`disaster.hazardPerYear` 0.0), spoilage and
      the store bound;
    - D-052 §4 (movement and Action Points at M7; kind 10 reserved);
    - D-039 and D-040 (fog, known extent);
    - the batch-2 layers' rules: the purse's donor order (now measured on the cylinder), relief, and the granary effects.

## §6 Calibration and world movement under the cylinder

**Every calibration-metric movement below is the cylinder alone, measured both ways.**
- With the cylinder on, every non-hash nightly metric of the combine-stage tree (`5ab74d3e…`) is identical, on 20/20
  seeds, to the cylinder-only (W1) tree's.
- A throwaway build of the combine-stage tree (`5ab74d3e…`) with only `topology.wrapX` false reproduced the batch-2
  nightly on every non-hash metric (20/20 seeds), and its `corridors.log` was byte-identical to batch 2's.
- So WS1, WS2 and the residual stages move no calibration metric.

**The frozen CR-020 reds** (`Dev_MalthusCorridors_AllInBand`; still red, still the only Sim.Tests failures; no literal
touched):

| Seed | Measure | Batch 2 | Batch 3 | Bound (frozen) |
|---|---|---|---|---|
| 42 | dev migration per decade | 8.71773E-05 | **8.35755E-05** | recorded 0.000248624, 0.75× floor 0.000186468 |
| 7 | dev population at turn 1000 | 124,483 | **123,950** | envelope [71,000, 119,000] |

- The dev worlds regenerate as cylinders.
- With wrapX false the W1 stage measured exactly 8.71773E-05 and 124,483.
- **On the final code** (final-round fix 3's full Sim.Tests run, whose 7 `CalibrationBatteryTests` cases are 5 passed
  and 2 failed; the worktree validation's full run and its `CalibrationBatteryTests` filter (§8), final-round fix 1's
  full run, and the nits round's run on `b3-final-r3.patch` and its `CalibrationBatteryTests` filter, gave the same)
  the two error messages read as below. Seed 42's is one line; seed 7's is three lines in the log, reproduced exactly
  (note the two spaces after "Range:"):
  - seed 42: "seed 42: 8.35755E-05 fell below 0.75× its recorded value 0.000248624 — the dev world has degraded beyond
    the quarantined deviation (measured disablement signature ×0.536; largest legitimate correction ×0.836). A NEW
    defect, or a ruled substrate change that must re-pin this envelope deliberately."
  - seed 7:

    ```
    Assert.InRange() Failure: Value not in range
    Range:  (71000 - 119000)
    Actual: 123950
    ```
- Neither the combine stage nor any later stage (the worktree validation included) re-ran the battery with the
  cylinder off, so for the final code the attribution is INFERRED from W1's control plus the matching nightly control.

**Nightly** (`autoplay --seeds 20 --turns 650 --metrics`, rc 0, 0 crashes, 12 settlements on every seed), batch 2 →
batch 3. Measured by the combine stage on `5ab74d3e…`, and **MEASURED on the final code** by the worktree validation
(§8, 2026-10-15): its `nightly-metrics.json` is byte-identical to the combine stage's (every field of all 20 seeds,
`worldHash` included), and so is its `corridors.log` (re-compared by final-round fix 4). Until that run these figures
were INFERRED for the final code (the fix rounds changed no simulation system, and the ci.yml cross-process legs print
identical hashes on both trees); they are now measured on it.

| Metric | Batch 2 | Batch 3 |
|---|---|---|
| Density per arable km², range | [0.354, 0.714] | [0.394, 0.652] |
| Density in the TARGET band [0.15, 0.6] | 17/20 | **16/20**: seed 6 leaves it (0.581 → 0.612); seeds 1 (0.604 → 0.652) and 2 (0.714 → 0.613) are outside before and after |
| Density in its recorded window | 19/20 | **20/20** (seed 3 re-enters: 0.354 → 0.397) |
| Density corridor status | WINDOW BREACH by 3.93 % | **QUARANTINED** |
| Migration gross per decade, range | [2.0103e-4, 3.4869e-4] | [1.7256e-4, 3.5233e-4] |
| Migration band and window | 0/20 and 0/20 | 0/20 and 0/20 |
| Migration WINDOW BREACH (quarantined, non-gating) | 77.6638 % | **80.8272 %** |
| Final population, median | 144,852.5 | 144,701.5 |
| Final population, range | 103,129–165,163 | 103,845–164,523 |

Per-seed moves, batch 2 → batch 3:
- density: −0.10124 (seed 2, −14.18 %) to +0.05603 (largest relative move +12.14 %, seed 3);
- migration: −8.008e-5 (seed 6, −25.67 %) to +8.961e-5 (seed 8, +34.11 %);
- final population: −1,067 (seed 2, −0.66 %) to +1,226 (seed 10, +0.87 %).

The terrain change dominates. The topology recon had estimated ±1.1 % for a sim-only wrap; the measured migration moves
are far beyond that.

**No STOP, and no new calibration red.** No canonical band or era-continuity case failed: the 5 other battery cases
pass. The density and migration movements are quarantined and non-gating, and are reported here for the Director (§10
Q8). The per-seed `worldHash` of every nightly seed moves for the v33 layout as well (INFERRED from the CLI hash legs,
where it was measured).

**Bench** (`sim bench --founded --seed 42 --turns 60`, Release, 3 interleaved rounds, load 0.74–1.05 on 4 CPUs;
measured by the combine stage on `5ab74d3e…`, not re-run on the final code, the worktree validation included. INFERRED
to hold because the fix rounds changed no simulation system; the nightly, inferred on the same ground until the
worktree validation, then measured identical on the final code):
- Shipped config: median **11,877 ms** against batch 2's 10,436 ms (**+13.8 %**). PathBuild accounts for about 94 % of
  the difference (3,912 → 5,266 ms): the cylinder's terrain and road network differ.
- Same flat world (batch-3 code with only wrapX false): 10,854 against 10,726 ms (+1.2 %), inside batch 2's own spread
  across its 6 runs (9,677–10,893).
- The construction phase is about 4× (1.48 → 5.73 ms), which is +0.07 ms a turn.

## §7 Moved pins, with cause and attribution

Every re-pin below carries a dated "BATCH 3 / D-054 RE-PIN (integration R1, 2026-10-14)" cause block at the pin, naming
its layer(s) and its attribution test.

**No re-pin since R1.** Fix rounds 1 and 2, the nits round and final-round fixes 1 and 3 added no RE-PIN block
(VERIFIED: no such line dated 2026-10-15 in the test diff) and moved no batch-2 reference or golden. What they changed
are message pins that moved with their message (the size refusal in `CliSaveAndSizeMessagesTests` and
`CylinderLaunchRefusalTests`, the trailer refusal, the `sim inspect` lines), the re-rigs listed below, and the new
tests' own values.

**The attribution rule.** With both halves of the batch off (`TestConfigs.PreD054Sim` + `PreD054Worldgen`, or
`FoundPreD054`) and the v32 projection (`SchemaProjection.V32` / `HashAtSchemaV32`), every batch-2 reference reproduces
byte for byte: `D054PairAttributionTests`, 5 cases. **Layer key:** L = the v33 layout (two appended tables, EMPTY in
every one of these worlds); C = the cylinder; W1 = incremental construction.

| Pin | Batch 2 → batch 3 | Cause | Lasting attribution |
|---|---|---|---|
| `SnapshotTests.FoundedGoldenHash` (+ ci.yml `FOUNDED_GOLDEN`) | `937577ff…` → `40115e28af11273b9e25c62d93817c48d6c9afe736a60a2ebc6e00428836f1b0` | L + C | new link `FoundedGoldenHashAtV32` = `0549049d…` (C alone); `WrapAttributionTests`; `IncrementalConstructionAttributionTests` (L alone); D054Pair "founded" |
| `SnapshotTests` toy golden (seed 42, turn 200) | `5994e978…` → `222280e5…` | L alone (no terrain) | V32(toy) = `5994e978…`; D054Pair "toy" |
| `DrivenGoldenTests.Golden` | `4f4e0b81…` → `fa1ce900…` | L + C | new link `GoldenAtV32` = `c7fbbe8f…`; D054Pair "driven" |
| `FirstReignTests.PostR1Golden` | `467f2cb6…` → `bccc8aed…` | L + C | new link `PostR1GoldenAtV32` = `101bbdd7…`; D054Pair "first-reign" |
| `AiFoundingAttributionTests.S1cTurn300` (AI world, 300 turns) | `b2e4f333…` → `91836bcf…` | L + C; W1 never acts there | new link `S1cTurn300AtV32` = `0ce1d4d9…`; D054Pair "ai" |
| `AiEmpireIntegrationTests` (targets[1], first build, advance, Age II) | (26, 4, 221, 222) → (25, 2, 221, 222) | C alone | `OneAiEmpire_WithTheCylinderOff_EveryPinReturnsItsBatch2Value` |
| same test (road, levy; Age III) | (134, 135, 423, 424), A3 423 → (134, 135, 422, 423), A3 422 | C alone | the same twin; founds and the 14 held settlements unchanged |
| `MerchantTests` first active turn | 127 (173 active turns) → 148 (45) | C alone | `MerchantsEmerge_WithTheCylinderOff_AtTheBatch2Turn` |
| `IntegratedSaveLoadBatteryTests` save points | (1, 27, 2, 34) → (1, 26, 2, 26) | C moves 27 → 26; W1 moves 34 → 26 | `TheSavePoints_MovedForTheCylinderAndIncrementalConstruction_EachLayerAloneMovesItsOwnPin` (four arms) |
| `ClassSystemTests.ArtisanPresence…` latch / first present / artisans | 67 / 14 / 1 → 10 / 6 / 2 | C alone (dev seed 3) | `ArtisanPresence_WithTheCylinderOff_ReturnsTheBatch2Turns` |
| `AiAgeAdvancementIntegrationTests` (AI, player decision turns) | (379, 333) → (421, 312) | C alone (dev world) | `AdvancesViaTheSameOrderPathway_WithTheCylinderOff_AtTheBatch2Turns` |
| `GlassBoxUiTests.Grievance…` emptying turn | 10 → 11 | C alone (256-px starved session) | `Grievance_WithTheCylinderOff_EmptiesAtTheBatch2Turn` |
| `SettlementCompositionTests` digest | `d8fbf569…` → `31a160dc…` | C alone (a composition digest; L cannot reach it) | `Composition_WithTheCylinderOff_IsTheBatch2Digest` |

The batch-2 ADR-038 §6 pin "AI first build 9 → 4" is now 2 (the row above). The four hash goldens diverge from turn 0:
the terrain is folded into every turn's hash, and the two empty count prefixes are in every turn's stream.

**Premises re-rigged, not re-pinned** (each with its measured premise counts at the test):
- **The CI found-settlement literals** (`scripts/gen-sample-orders.py --found`, `FoundSettlementCiLegTests`, the overlay
  fixture, the resume tests): cells 330985 / 322801 became **349391 / 335063**. The old cells are `PoorSite` on the
  cylinder. The full 300-turn CI leg reproduces: two processes identical, delivery turn-exact, `sim inspect`
  "reproduction VERIFIED: 301 turns".
- **`FamineScenarioTests` determinism legs:** moved to `DisasterSeed` 22 over 150 turns, with the guard raised to at
  least 3 FAMINE settlement-turns. λ = 0.01 in-rig, unchanged.
  - Seed 22's three FAMINE settlement-turns are in worlds **7** (settlement 0), **97** (settlement 11) and **139**
    (settlement 9), measured by the nits round. (R1 wrote these as "steps 8, 98 and 140".)
  - **The replay leg's labour orders** (`S_Determinism_ReplayReproducesRun_WithDisastersLive`) are stamped **135**
    (settlement 0, 30 % farming; `OrderBeforeFamine`) and **146** (80 %; `OrderAfterFamine`). R1 had stamped them 141
    and 146, after every famine, so no famine fell in a world an order had shaped (verifier F3). Now the first is
    delivered in step 135 → 136, BEFORE the world-139 famine, and the second in step 146 → 147, after every famine.
  - Turn-exact pins: farming is not 0.3 at world 135, is 0.3 at 136 and 146, and is 0.8 at 147; a famine falls after
    the first delivery and in or before world 146; the famine worlds are exactly {7, 97, 139}. Replay equality over
    150 hashes is kept. The mutant "orders delivered one turn late, live and replay alike", which replay equality
    alone cannot see, is killed ("Expected: 0.3 Actual: -1").
  - The stamp search (45 stampings): a first order on settlement 0 at 9, 30, 60, 90, 99, 100, 110, 120 or 130 leaves
    only one or two famines; 135 or 138 keeps all three.
- **Older-schema resume rigs, re-rigged flat** (a v32 build could only have recorded a flat session):
  - `ChronicleAndResumeTests.Resume_OfASessionRecordedUnderAnOlderSchema_ReplaysWithTheNotice_AndContinuesLikeAFreshReplay`
    (fix round 1). After the flat-resume fix its cylinder session relabelled v32 failed with "Expected: 0d5c72e1…
    Actual: 71883a5e…". Its live and fresh sessions are now founded flat; every assertion stands.
  - `ResumeNoticeUiTests.AGameOpenedOnASessionResumedUnderAnotherSchema_ShowsTheNotice_AndOneUnderItsOwnSchemaDoesNot`
    (the nits round, verifier RC1-5). Its older-schema half now records a FLAT session, stamps it v32 and asserts
    WrapX false, the live turn and an equal world hash before the toast and kicker. Its own-schema half records a
    separate session on the shipped cylinder and asserts an equal hash and no notice. The mutant "Resume ignores
    RecordedFlat" passed the old rig and is now killed.
- **The battery's part-built save point** (`IntegratedSaveLoadBatteryTests.SaveLoad_MidPartBuiltConstructionHead`,
  added by fix round 1, re-rigged by the nits round, verifier RC1-3). It first took world 3, which is the save the
  "age before decision" test already makes. It now takes the first part-built-head world that no other battery save
  point uses (`OtherSaveTurns()`: 1, 2, 3, 4, 5, 6, 26 and 35–39), which is **world 7**: the capital's university
  (slot 1) at p = 145,000 of 1,000,000. The head is part-built in worlds 3–22 and completes in step 22 → 23. The
  four-tuple pin (1, 26, 2, 26) and its attribution test are untouched.
  - **Re-rigged again by final-round fix 1 (verifier RC1-3-SAMESTATE, 2026-10-15).** World 7 is a distinct world, but
    its `ConstructionProgress` table is world 6's row for row (settlement 0, slot 1, project 11, enqueued turn 0,
    p = 145,000), and world 6 is TaxTurn + 1, which `SaveLoad_BetweenTaxOrderIssueAndEffect` already saves. The save
    point is now the first part-built-head world whose progress table differs from that of every other save point's
    world: **world 8**, p = 231,666 (worlds 8–10 hold 231,666; measured by a probe dump of every world's table,
    removed afterwards). The test asserts k = 8, the single row's p, that no other save world holds the same table,
    and, against vacuity, that worlds 6 and 7 do. `OtherSaveTurns()` is hand-kept; its doc comment now says a later
    save test must add its worlds there.
- **`FamineScenarioTests.S_Seed42_NoFamineWithoutCause`:** re-rigged onto two cylinder worlds, seed 42 and famine seed
  22. Its ρ report now describes the cylindrical seed 42, so it is no longer directly comparable with
  `docs/t4.21-1-foundations-audit.md` §4's flat series.
- **`PathBuildTests.BuildLoop`** (the D-009 growth bar): back on the shipped cylinder with seed 1, which gives +3.51 %
  arable. 26 of 30 dev seeds clear the 2 % bar on the cylinder, against 20 of 30 flat.
- **Kept flat, with their cylinder counts recorded at the test.** Each is a batch-2 layer twin (the R4 rule), or pins a
  topology-independent property:
  - `FoundedHarnessTests` ordered twin: on the cylinder settlement 0 never revolts in 300 turns;
  - the `WorldReconciliationTests` per-settlement-store samples: no dwelling decay in 300 turns on the cylinder;
  - `ExplainRigs.Starved`;
  - the `NonCanonicalSizeFoundingTests` CLI recipe: no bottom-row AI colony on the cylinder.
- **UI P2 pins** (`MapViewportTests`): founded flat, assertions unchanged. The cylinder's behaviour is pinned by
  `CylinderSessionTests` and `CylinderCameraTests`.
- **The gate's wheel rows** moved in zoom and notch count, and all still pass. The full table is in
  `docs/m5-hardening-measurements.md` (2026-10-14, W2).

**Not moved:** every batch-2 attribution constant (`PreD054FoundedGoldenHash` and the rest stay at their batch-2
values; the older-layer controls reach them through the pair and the projection); `RevoltTurn` 39 and the
first-decay 201 (founded flat); `CiPinAgreementTests` (exact, passing).

**Unpinned references.** The founded-mix AI CI leg (600 turns) is now 275 orders of kinds [3..9, 11, 12], final
`f5965ed0…`; batch 2 was 298 orders of kinds [3..9], `40c1b150…`. The seed-16 seam leg's final hash is `2aade7ce…`.
CI asserts two-process identity for both and pins neither hash. Both values are the same on `b3-final-r3.patch`'s tree
(the nits round's cross-process run), whose order, CLI, hash and simulation code the final code shares, and on the final
code itself (the worktree validation's cross-process run, §8).

**The CLI hash legs** (the combine stage, `5ab74d3e…`; shipped Release CLI, 300 turns, save v33 with topology 1). The
V32 projection strips the trailer and the 8 zero bytes of the two empty v33 tables; it equals the W1 cylinder-only
value on every leg, so at turn 300 both v33 tables are empty and the layout is the only other difference.

| Leg | Raw (v33) | V32 projection | Batch 2 |
|---|---|---|---|
| founded 42 | `40115e28af11273b9e25c62d93817c48d6c9afe736a60a2ebc6e00428836f1b0` | `0549049d92f6c59d65a01111485cca4d938f36cfa0696a5fa8575811194fb6c9` | `937577ff…` |
| AI 42 (`--ai-empires 1`) | `91836bcf…` | `0ce1d4d9…` | `b2e4f333…` |
| seed 16 founded | `2aade7ce…` | `66458ab8…` | `6831485d…` |

The direct control: the combine-stage tree (`5ab74d3e…`) with only `topology.wrapX` false, founded 42, has the V32
projection `937577ffb0e73c2b051d8d7832cfb5028c2154d01c9afef4d976b71ccb5e7f54`, the batch-2 golden. On
`b3-final-r3.patch`'s tree the founded leg again ends at `40115e28…` (ci.yml's `FOUNDED_GOLDEN`, the nits round's run),
and so does the final code's cross-process founded leg (the worktree validation, §8); the final code pins the same value in `SnapshotTests.FoundedGoldenHash`, which passed in final-round fix 3's full run
(and in final-round fix 1's).

## §8 Validation (gov-4 §9: a result, not a certification)

The newest results are at the end of this section: the worktree validation of the final code, and final-round fix 4
(both 2026-10-15).

**On the final code** (final-round fix 3, 2026-10-15, in its own scratch clone: HEAD `de86ace` plus the batch-2 base
plus the final batch-3 diff, whose non-docs part has sha256 `5de7149e…` (the header); Release, .NET 10.0.112). The
round's later edits were to docs only, and no test reads the docs it edited; the non-docs sha256 was the same before
and after the runs.

| Step | Result |
|---|---|
| Gates: `check-banned-constructs.sh`, `check-read-isolation.sh` (24 allowlisted files), `check-readonly-proof.sh` | all rc 0, OK (CS0200 and CS1061); run before and after the full suites |
| Research checks (corpus audit, calibration report, unlock audit) `--check` | all rc 0, current (before and after the full suites) |
| `dotnet build Sim.slnx -c Release`, then again with `--no-incremental` | 0 warnings, 0 errors, both times |
| Sim.Tests, FULL | **2,060 passed / 2 failed / 13 skipped (2,075)**, 38 m 10 s. The 2 failures are only the frozen CR-020 seeds, with the messages quoted in §6. Against final-round fix 1's run every other test name is present with the same outcome; the size theory's 4 cases and the trailer theory's 2 were renamed (a new parameter each), and 4 cases are new (sizes 122 and 126; `sim worldgen` at 250 and 18 px) |
| Sim.Ui.Tests, FULL | **975 / 975**, 12 m 56 s; final-round fix 1's 974 names with the same outcomes, plus the launcher at 8 px |
| Calibration battery (its 7 cases, read from the full run) | 5 passed, the same 2 failed |
| Smoke `--smoke --monkey 300` | 13 states, 3,190 checks: 2,988 pass, 202 not offered, 0 FAIL; 3,900 monkey actions; SMOKE PASSED |
| Focused filters | `CliSaveAndSizeMessagesTests` 24/24 and `CylinderLaunchRefusalTests` 8/8 (alone, and again in the full runs); `IntegratedSaveLoadBatteryTests` 14/14 (full run) |
| The messages, run as the real CLI | quoted in §3 and §4: `sim run --founded --seed 42 --turns 1 --size` 250, 8, 122 and 126; `sim worldgen --seed 42 --size 250`; `sim hash` of a flipped copy of a `sim run --founded --seed 42 --turns 2 --save-at 1` save; `Sim.Ui.dll --size 8` (exit 2, the same text) |
| ci.yml "Cross-process determinism (T0.9)" step | NOT re-run: the round changed two message texts and no order, hash or simulation code. The nits round's run below stands for it |

**Mutants of final-round fix 3** (in place in its own clone, each restored and the file's sha256 re-checked; bounded at
4× the clean baseline of the filters: `CliSaveAndSizeMessagesTests` 54 s, bound 214 s; `CylinderLaunchRefusalTests`
7 s, bound 27 s; none hung). They ran before a comment-only reflow of `FoundingMeasuredPx`'s doc comment, after which
the full suites ran.
- **Always add the founding sentence:** KILLED. Sim.Tests 4 failed (the size theory at 250, 254 and 126 px, and
  `sim worldgen` at 250 px), Sim.Ui.Tests 1 (`TheRefusal_NamesWhatTheUserCanChange…`).
- **Never add it:** KILLED. Sim.Tests 4 failed (8, 13 and 122 px, and `sim worldgen` at 18 px), Sim.Ui.Tests 1.
- **The threshold at 128 px** (the figure first recorded): KILLED by the size theory at 126 px ("Assert.EndsWith()
  Failure: String end does not match … se maps this small, with its own message."). Sim.Ui.Tests has no case at the
  threshold and passed.
- **Keyed on the larger named size:** KILLED by the size theory at 122 px.
- **The old trailer remedy** ("… bit 0 again, or save the world again, to load it."): KILLED by both cases of
  `SimHash_OfASaveWhoseTrailerBitWasFlipped_IsRefusedAsATopologyMismatch_NamingTheBit` (the whole-text pin).

**On final-round fix 1's code, the previous result** (final-round fix 1, 2026-10-15, in its own scratch clone: HEAD
`de86ace` plus the batch-2 base plus that round's batch-3 diff, whose non-docs part has sha256 `9787f380…`; Release,
.NET 10.0.112). The round's later edits were to docs only, and no test reads the docs it edited; the non-docs sha256
was the same before and after the runs.

| Step | Result |
|---|---|
| Gates: `check-banned-constructs.sh`, `check-read-isolation.sh` (24 allowlisted files), `check-readonly-proof.sh` | all rc 0, OK (CS0200 and CS1061) |
| Research checks (corpus audit, calibration report, unlock audit) `--check` | all rc 0, current (run again after the docs edits: still current) |
| `dotnet build Sim.slnx -c Release`, then again with `--no-incremental` | 0 warnings, 0 errors, both times |
| Sim.Tests, FULL | **2,056 passed / 2 failed / 13 skipped (2,071)**, 34 m 35 s. The 2 failures are only the frozen CR-020 seeds (§6, quoted). The same 2,071 test names as the nits round's run, each with the same outcome |
| Sim.Ui.Tests, FULL | **974 / 974**, 13 m 26 s; the same 974 names as the nits round's run |
| Calibration battery (its 7 cases, read from the full run) | 5 passed, the same 2 failed |
| Smoke `--smoke --monkey 300` | 13 states, 3,190 checks: 2,988 pass, 202 not offered, 0 FAIL; 3,900 monkey actions; SMOKE PASSED |
| Focused filters | `IntegratedSaveLoadBatteryTests` 14/14 (before the full runs and again after the non-incremental build); `IncrementalConstructionUiTests` 10/10 |
| ci.yml "Cross-process determinism (T0.9)" step | NOT re-run: the round changed no order, CLI, hash or simulation code (one test file, and comments in three source files). The nits round's run below stands for it |

**Mutants of final-round fix 1** (in place in its own clone, each restored and the diff sha re-checked; bounded at 4×
the clean baseline of the filter: `SaveLoad_MidPartBuiltConstructionHead` alone 4 s, bound 16 s; the battery class
10 s, bound 40 s; none hung):
- **Progress rounded down to 1,000 on load** (`CanonicalSchema` read of `ConstructionProgressRow.Progress`). Before
  the fix: SURVIVED `SaveLoad_MidPartBuiltConstructionHead` alone (world 7, p = 145,000), and was killed by the class
  through `SaveLoad_AcrossTheAgeTransition` and `SaveLoad_BetweenTaxOrderIssueAndEffect`, which shows the mutant was
  live. After the fix: KILLED by the test alone (4 s): "Assert.Equal() Failure: Strings differ … Expected:
  "337b47829a70cd2b24f33d936572a91f590f176d5"··· Actual: "97d4c572bcedcf2ae94981597da5f5cdaeaaf424a"···", the
  loaded world 8's hash.
- **The old predicate** (a world no other save uses, whatever its table): KILLED by the pin ("Expected: 8 Actual: 7").
  With the pins moved to world 7 as well, it is KILLED by the semantic assertion: "world 6 already holds world 7's
  progress table".
- One run was void and is not counted: a test-side mutant whose build failed (CS8321), so the old binary ran.

**On `b3-final-r3.patch`'s tree, the previous result** (the nits round, tree HEAD `de86ace` plus the batch-2 base plus
`b3-final-r3.patch`, diff sha `855a14029c906c51…`; Release, .NET 10.0.112; results in the nits round's validation log):

| Step | Result |
|---|---|
| Gates: `check-banned-constructs.sh`, `check-read-isolation.sh` (P7's 24 allowlisted files), `check-readonly-proof.sh` | all rc 0, OK (the readonly proof: mutation attempts fail to compile, CS0200 and CS1061) |
| Research checks (corpus audit, calibration report, unlock audit) `--check` | all rc 0, current |
| `dotnet build Sim.slnx -c Release` | 0 warnings, 0 errors |
| Sim.Tests, FULL | **2,056 passed / 2 failed / 13 skipped (2,071)**, 41 m 29 s. The 2 failures are only the frozen CR-020 seeds (§6, quoted). Against the combine stage's run every test name is still present; the only additions are the fix rounds' new tests, and the size theory's cases were renamed with it |
| Sim.Ui.Tests, FULL | **974 / 974**, 16 m 51 s |
| Calibration battery (`--filter CalibrationBatteryTests`) | 5 passed, the same 2 failed |
| Smoke `--smoke --monkey 300` | 13 states, 3,190 checks: 2,988 pass, 202 not offered, 0 FAIL; 3,900 monkey actions; SMOKE PASSED |
| ci.yml "Cross-process determinism (T0.9)" step | rc 0: orderless and replay byte-identical over 400 turns; the founded golden `40115e28…`; the seed-16 seam leg two-process byte-identical over 300 turns (`2aade7ce…`); founded replay byte-identical; the found-settlement leg "reproduction VERIFIED: 301 turns"; the AI leg "reproduction VERIFIED: 601 turns" with 275 orders of kinds [3, 4, 5, 6, 7, 8, 9, 11, 12] (`f5965ed0…`). Every hash line equals the combine stage's run |
| Focused filters, read from the full runs | `CliSaveAndSizeMessagesTests` 20/20, `IntegratedSaveLoadBatteryTests` 14/14, `FamineScenarioTests.S_Determinism*` 3/3, `SeamCrossingShippedWorldTests` 3/3, `ConstructionSchemaV33Tests` 21/21, `ResumeNoticeUiTests` 1/1, `CylinderLaunchRefusalTests` 7/7, `Batch2SessionResumeTests` 3/3 |

**Mutants of the nits round** (each bounded at 4× the clean baseline of its filter; none hung). Killed: the trailer
recheck never or always firing; the old size wording (in both projects); `hashesChecked` keyed on `!RecordedFlat`
(the verifier's surviving MF); `Reproduce` keyed on `!RecordedFlat`; the recipe printed before the empty-trace return;
the platform block ignoring the empty trace; part-built progress zeroed on load; the old part-built predicate; Resume
ignoring `RecordedFlat`; orders delivered one turn late; the old famine stamp; `NeighbourX` never wrapping; push zeroed
on seam pairs. **One equivalent survivor:** progress quantized to 1,000 on load survives `SaveLoad_MidPartBuiltConstructionHead`
alone, because world 7's p = 145,000 is a multiple of 1,000; run on the whole battery class it is killed by
`SaveLoad_AcrossTheAgeTransition` and `SaveLoad_BetweenTaxOrderIssueAndEffect` (p = 32,777 and 107,777). (Closed by
final-round fix 1: at world 8 the test kills it alone, above.) The round's
first mutant run was contaminated (restored files kept an older mtime, so MSBuild skipped recompiling) and was
discarded; only the rerun is reported.

**Measured by the combine stage on `5ab74d3e…`** (the fix rounds changed no simulation system). Until the worktree
validation none of these was re-run on the final code; that run re-measured the first two rows on it (below), the UI
resume row's test passed in the final full runs, and the bench and the CLI hash legs are still measured only on
`5ab74d3e…`:

| Step | Result |
|---|---|
| Determinism filter (`suite=determinism`) | Sim.Tests 47 / 47 (batch 2: 27 / 27); no Sim.Ui.Tests test carries the trait. On the final code: 48 / 48 in the worktree validation (below) |
| Nightly 20 × 650 + corridors | rc 0 (§6). On the final code: rc 0 and byte-identical in the worktree validation (below) |
| Bench | §6 |
| CLI hash legs with the V32 projection | §7 |
| UI resume of a part-built head with a cancel and a split | hash-identical, completes on the same turn (`IncrementalConstructionUiTests`; it also passes in the final full run) |

### Worktree validation (the final code, on `/home/user/m5w/integration`), 2026-10-15

**The tree.** The integration worktree `/home/user/m5w/integration` (branch `m5w-integration`, HEAD `de86ace`), holding
the batch-2 base and `b3-final-v7.patch` (sha256 `fd01056f…`, header) applied, uncommitted: the final code, non-docs
sha256 `5de7149e…`. Script `final-validate-int.sh`; results in the scratchpad's `final-int/` (`steps.txt`, the step
logs, `simtests.trx`, `uitests.trx`, `calib.log`, `smoke.log`, `xproc.log`, `nightly-metrics.json`,
`corridors.log`). Release, .NET 10.0.112. `steps.txt` records the worktree's `git diff --binary` sha256 as
`a87437ac5ecf61bf…` with 240 status entries at the start and at the end of the run. VERIFIED by final-round fix 4
afterwards, read-only: the worktree still shows that sha and count, and each of the 1,048 tracked files of a scratch
clone of the batch-2 base with `b3-final-v7.patch` applied is byte-identical to the worktree's file at the same path.
The run is the final code WITHOUT final-round fix 4's test (below).

| Step | Result |
|---|---|
| Gates: `check-banned-constructs.sh`, `check-read-isolation.sh` (24 allowlisted files), `check-readonly-proof.sh` | all rc 0, OK (CS0200 and CS1061) |
| Research checks (corpus audit, calibration report, unlock audit) `--check` | all rc 0, current |
| `dotnet build Sim.slnx -c Release` | rc 0, 0 warnings, 0 errors |
| Sim.Ui.Tests, FULL | **975 / 975**, 12 m 1 s. The same 975 names with the same outcomes as final-round fix 3's run (VERIFIED by comparing the two TRX files) |
| Sim.Tests, FULL | **2,060 passed / 2 failed / 13 skipped (2,075)**, 38 m 43 s. The 2 failures are only the pre-existing CR-020 reds, `CalibrationBatteryTests.Dev_MalthusCorridors_AllInBand` seeds 42 and 7, with the messages quoted in §6. The same 2,075 names with the same outcomes as final-round fix 3's run (VERIFIED by comparing the two TRX files) |
| Calibration battery (`--filter CalibrationBatteryTests`) | 7 cases: **5 passed, 2 failed** (the same two, the same messages); step rc 1 for those two |
| Determinism filter (`suite=determinism`) | Sim.Tests **48 / 48**, 8 m 27 s; no Sim.Ui.Tests test carries the trait. 48 = the combine stage's 47 plus `IntegratedSaveLoadBatteryTests.SaveLoad_MidPartBuiltConstructionHead` (fix round 1), the only new name in the trait's classes (VERIFIED by comparing the names) |
| Smoke `--smoke --monkey 300` | 13 states, 3,190 checks: 2,988 pass, 202 not offered, **0 FAIL**, 0 problems; 3,900 monkey actions; SMOKE PASSED |
| ci.yml "Cross-process determinism (T0.9)" step | rc 0, every leg: orderless, two processes byte-identical over 400 turns (`3ab74c89…`); ordered (16 orders) and replay byte-identical over 400 turns (`f18a471a…`); founded orderless, two processes byte-identical over 300 turns, and "founded run matches the pinned golden" (`40115e28…`); the seed-16 seam leg, two processes byte-identical over 300 turns (`2aade7ce…`); founded replay (15 orders) byte-identical over 300 turns (`fc1ce2b8…`); the found-settlement leg, two processes with turn-exact delivery, replay and inspect agree, "reproduction VERIFIED: 301 turns" (`46957e4b…`); the AI leg, two processes, 275 orders of kinds [3, 4, 5, 6, 7, 8, 9, 11, 12], replay and inspect agree, "reproduction VERIFIED: 601 turns" (`f5965ed0…`). The log is byte-identical to the combine stage's (VERIFIED with `diff`) |
| Nightly `autoplay --seeds 20 --turns 650 --metrics` | **rc 0**; year 4500 and 12 settlements on every seed; no crash line |
| `corridors --metrics` | **rc 0**: density QUARANTINED (16/20 in the target band, 20/20 in the recorded window); migration WINDOW BREACH by 80.8272 % (quarantined, non-gating) |

**The nightly against the combine stage's** (`final-b3/nightly-metrics.json`, `5ab74d3e…`). The orchestrator reported
every field identical on 20/20 seeds and `corridors.log` byte-identical. Final-round fix 4 re-measured this before
recording it:
- the two `nightly-metrics.json` files are byte-identical (sha256 `1ae0fe53f1b8ab33…` both);
- field by field, every leaf of every seed (the 9 per-seed fields and every series and derived value, 91,500 leaves)
  is identical on 20/20 seeds, `worldHash` included, and so are the schema and the turn count;
- the per-seed lines of the two autoplay logs (population, year, hash) are identical;
- the two `corridors.log` files are byte-identical (sha256 `e8d829968c671831…` both).

So §6's nightly figures, INFERRED for the final code until this run, are now **MEASURED** on it.

**Not re-run on the final code** (this run included):
- the bench (§6);
- the six CLI hash legs (§7: founded 42, AI 42, dev 42 with 4 and with 6 settlements, dev 7 with 4, and seed 16; 300
  turns each, with their saves and V32 projections). This run's T0.9 founded and seed-16 legs end at the same raw
  hashes as two of them (`40115e28…`, `2aade7ce…`), but the legs themselves were not re-run;
- the cylinder-off controls: the throwaway build with only `topology.wrapX` false (its nightly and `corridors.log`, and
  the founded-42 V32 projection `937577ff…`, §6 and §7).

These were measured by the combine stage on `5ab74d3e…`. The calibration battery's cylinder-off control (8.71773E-05
and 124,483, §6) was measured on the W1 tree.

### Final-round fix 4, 2026-10-15: a semantic test for the flat path's noise scaling (mutant MA2)

**The finding.** The W1 calibration verifier's F6 (a nit): mutant MA2, in `Sim.Core/Worldgen/Worldgen.cs` line 52,
`double nxScale = wrapX ? size : size - 1;` → `double nxScale = size;`, changes the FLAT world's noise x scaling. Every
flat terrain then changes, and v32 saves and batch-2 sessions are rebuilt flat (§4). In the verifier's focused set it
was killed only by `WrapAttributionTests.WithTheCylinderOff_TheTerrainIsTheBatch2TerrainByteForByte`, a ContentHash
literal: a golden-only kill, which CLAUDE.md does not count.

**Reproduced on the final code** (fix 4's own clone: HEAD `de86ace` plus the batch-2 base plus `b3-final-v7.patch`;
Release; MA2 applied in place and rebuilt, then restored):
- the verifier's own filter without the golden (`CylinderWorldgenTests`, `MapTopologyTests`,
  `WrapAttributionTests.TheShippedWorldgen_IsACylinder`, `SeamCrossingShippedWorldTests`, `HydrologyTests`; 42 tests):
  MA2 **SURVIVED**, 42 / 42 passed (24 s);
- the wider filter `FullyQualifiedName~CylinderWorldgen|~Worldgen|~Hydrology|~MapTopology` without the golden (100
  tests, clean 100 / 100 in 49 s): MA2 failed 2 (52 s), and neither asserts the mapping:
  - `ConstructionSchemaV33Tests.TheCliSaveLoader_RegeneratesAV32SaveFlat_AndAV33SaveWithTheShippedWorldgen`: "terrain
    mismatch: the provided TerrainSet's content hash does not equal the hash this save was made against — wrong seed or
    worldgen config." This is the v32 fixture's stored terrain hash: byte identity again, like the golden.
  - `NonCanonicalSizeFoundingTests.ASizedWorldWithAnAiEmpire_ThroughTheCliRecipes_RunsThroughTheAisBottomRowFounding_WithTheMapsOwnDeposits`:
    "no settlement was founded in the bottom rows - the rig no longer reaches the former throw". This is a rig-premise
    guard.
- the golden alone: 4 / 4 cases failed.

**The test.** `CylinderWorldgenTests.TheFlatWorld_SamplesTheNoiseOnTheClosedUnitSquare_ItsLastColumnAtThePeriodEndpoint`
pins the property, not a hash. Off the cylinder, column x samples the noise at x / (size − 1), as row y does at
y / (size − 1), so the last column sits on the noise's period endpoint nx = 1. On flat worlds of 125 and 249 px (neither
a power of two) it asserts two things:
- **grid refinement:** for seeds 1–3, cell (x, y) of the 125-px world and cell (2x, 2y) of the 249-px world (edge taper
  6 and 12 px) have the same elevation, bit for bit. Only a mapping of the form k · x / (size − 1) refines this way.
- **the scale:** with the continental mask reduced to its noise (radial weight 0, noise weight 1) and the taper to 1
  inside a 1-px margin, each interior cell's elevation is exactly the elevation fBm times the mask fBm at (x / 124,
  y / 124). Both are evaluated by the private noise primitive with no periods, reached by reflection as
  `TheNoiseIsExactlyPeriodicInX_TheFieldAtXPlusOneMapIsTheField` does.

It passes on fix 4's tree (0.6 s).

**Mutants** (bounded at 4× the clean baseline of the filter; none hung; each restored from a saved copy with a fresh
mtime and rebuilt, after which `Worldgen.cs` was identical to the v7 tree's):
- **MA2:** **KILLED by the new test alone**, in 2.6 s (clean 2.9–3.0 s over 3 runs, bound 12 s): "seed 1: flat cell
  (1,1) of the 125-px world (0.00719469875057169) and cell (2,2) of the 249-px world (0.007194238133958104) sample
  different noise points: the flat x mapping is no longer x / (size − 1), under which the last column samples the
  period endpoint nx = 1". The scale half alone also killed it (a throwaway copy of that half, which passed clean,
  deleted afterwards): "flat cell (1,1): elevation 0.461086578866837 is not the noise at (1/124, 1/124) =
  0.4611089974673732". In the wider filter with the new test (101 tests; clean 101 / 101
  in 46 s, bound 184 s), MA2 failed 3 in 50 s: the new test and the two above.
- **MW, the cylinder's scale** (`double nxScale = size - 1;`): **KILLED** in the wider filter (47 s, bound 184 s) by
  `TheTerrainIsContinuousAcrossTheSeam_TenSeeds_LikeAnyInteriorColumnPair`: "seed 1: seam elevation step 0 is a flat
  band". This is semantic: column size − 1 then samples nx = 1, a whole period on, so it repeats column 0. Two
  rig-void guards failed with it (`MovementCost_ReadsTheSlopeAcrossTheSeam_BitForBit` and
  `Hydrology_OnACylinder_SinksOnlyAtThePoles_AndARiverMayCrossTheSeam`). The new test passes under MW, as it should: it
  reads only the flat path.
- **Aim** (ADR-015 §7.2): the asserted property is one the flat path must have. It is the batch-2 mapping, which v32
  saves and batch-2 sessions are rebuilt on.

**Fix 4's validation** (its own clone: HEAD `de86ace` plus the batch-2 base plus `b3-final-v7.patch` plus fix 4's
change; non-docs sha256 `fa72fa6c…`, the header; Release, .NET 10.0.112). The docs were edited during and after the
runs; no test reads them.

| Step | Result |
|---|---|
| Gates: `check-banned-constructs.sh`, `check-read-isolation.sh` (24 allowlisted files), `check-readonly-proof.sh` | all rc 0, OK (CS0200 and CS1061), run after the full suite |
| Research checks (corpus audit, calibration report, unlock audit) `--check` | all rc 0, current, run after the full suite |
| `dotnet build Sim.slnx -c Release` | 0 warnings, 0 errors |
| Focused filter (`CylinderWorldgen`, `Worldgen`, `Hydrology`, `MapTopology`, without the golden) | 101 / 101, 46 s |
| Sim.Tests, FULL | **2,061 passed / 2 failed / 13 skipped (2,076)**, 29 m 58 s. The 2 failures are only the pre-existing CR-020 reds (seeds 42 and 7, the messages quoted in §6). Against the worktree validation's run: the same 2,075 names with the same outcomes, plus the new test, passed (VERIFIED by comparing the two TRX files) |
| `git diff --check` | clean |

Not re-run by fix 4: Sim.Ui.Tests (it does not build `Sim.Tests`), the smoke, the T0.9 step, the determinism filter and
the nightly. Fix 4 changed one Sim.Tests file and no product code, so the worktree validation above stands for them.

## §9 Known limitations carried forward

- **Famine determinism rests on 3 events.** No measured canonical cylinder seed has more than 3 FAMINE settlement-turns
  in 300 turns at the rig's λ 0.01, so the famine determinism legs rest on exactly 3 (seed 22, worlds 7, 97 and 139). A
  denser in-rig λ was not used: the hazard is frozen. The replay leg now delivers one order before the last of them
  (§7).
- **Flat re-rigs (§7).** Cylinder equivalents of the four kept flat re-rigs are not built.
- **Seed-42 siting capacity** at the 95.2 km spacing falls to 362 on the cylinder. The batch-2 flat world held at least
  400. `worldgen.json`'s `_docMinSpacing` now quotes the re-measured capacity: median 381 on the cylinder and 400 flat,
  over seeds 42, 1, 2, 3 and 6.
- **A pre-existing defect fixed.** On flat worlds whose size the stride does not divide (250 px), travelled road lanes
  were attached to the wrong lattice nodes (a `de86ace` defect, fixed in W1). Behaviour on such worlds now differs from
  batch 2 wherever a travelled road exists. The cylinder cannot reach it, and no pin covered it.
- **The session manifest records no topology.** A pre-v33 session is rebuilt flat by `TopologyFlags.RecordedFlat`; a
  v33 session is rebuilt with THIS build's shipped worldgen. So:
  - `UiSession.Start`'s `wrapXOverride` (a test and tool seam; `UiSession.Resume` now uses it internally for pre-v33
    sessions) is not in the manifest. A v33 session founded with it is not resumable from its log. The launcher never
    sets it.
  - A later build that changed the shipped topology would rebuild this build's v33 sessions on the wrong map.
  - **CONFIRM** (§10 Q9).
- **`sim replay` has no flat path.** It always founds the shipped cylinder, so no `sim replay` of this build reproduces
  a pre-v33 order log. `sim inspect` and `UiSession.Resume` rebuild such a session flat, and `sim inspect` says so and
  gives the recording build's recipe (§4). Fix round 2 measured that recipe on the batch-2 build: it exits 0 with the
  traces' final hashes (`6157dfde…` for the fixture, `2786a4b7…` for a 250-px session). A `--manifest`-driven flat
  override on `sim replay` was declined as scope widening. **CONFIRM** (§10 Q9).
- **Batch-2 sessions replay under batch-3 rules** (WS1 and WS2 on) on their own flat map. Where a head waited under M4-D
  the replayed world can differ, and the notice says it may (§4; §10 Q9).
- **The trailer recheck is diagnostic only.** `SaveFiles.LoadsUnderTheOtherTopology` catches every exception from its
  re-load and answers false, so the original refusal stands. An unexpected exception type in that probe is therefore
  treated as "not explained by the other topology".
- **`Snapshot.Load`'s own "topology mismatch: …" branch is unreachable from CLI v33 loads**, because the CLI regenerates
  the terrain the trailer names. It still guards API callers that pass their own TerrainSet (ADR-039 §4).
- **The size refusal's founding sentence** is added below 124 px, a threshold measured on 21 seeds (§3), not derived:
  another seed or settlement count can need more, and siting then refuses with its own message. It names no founding
  size on purpose (final-round fix 3).
- **Empty traces.** `sim inspect` of an empty trace now prints the plain platform line and no recipe for EVERY schema,
  including this build's own v33 sessions, which before printed the full hash-check block and a `--turns 0` recipe.
  Intentional and pinned (`SimInspect_OfAnEmptyTrace_SaysSo_AndOffersNoRecipe`).
- **The seam-migration pin needs a rig.** It runs seed 16 with trade and recipe knowledge set at turn 0, not through
  orders, and is an existence assertion within 120 turns (a single migrant at step 94 → 95 would be a fragile
  turn-exact pin). No orderless shipped-world seam flow, and no trade between a seam pair on any of the seven seam
  seeds, was found in 300 turns (§3).
- **An equivalent mutant (closed by final-round fix 1).** Progress quantized to 1,000 on load survived the battery's
  part-built save point alone while it was world 7 (p = 145,000, a multiple of 1,000), and was killed by the battery's
  Age and Tax saves. At world 8 (p = 231,666) the save point kills it alone (§8).
- **WS2 off is unreachable in production.** The "UiSession refuses kind 12 when WS2 is off" rule cannot be reached, because
  the content is embedded and shipped ON. It is covered by a chain of two tests: the emitter calls the shared predicate,
  and the predicate refuses with the switch off.
- **Test run time.** Full Sim.Tests: batch 2 had 1,809 tests in 28 m 07 s; the combine stage 2,054 in 31 m 32 s; the
  nits round 2,071 in 41 m 29 s; final-round fix 1 2,071 in 34 m 35 s; final-round fix 3 (the final code) 2,075 in
  38 m 10 s; the worktree validation (the final code) 2,075 in 38 m 43 s; final-round fix 4 2,076 in 29 m 58 s. Full
  Sim.Ui.Tests: 857 in 12 m 30 s; 970 in 13 m 17 s; 974 in 16 m 51 s; 974 in 13 m 26 s; 975 in 12 m 56 s; the worktree
  validation 975 in 12 m 1 s. Every run was on 4 CPUs, the earlier ones shared with another agent (final-round fix 3's
  machine load was not recorded), so the wall times are not a performance measurement.

## §10 Questions for the Director

None of these has been ruled. Status as of the final code: Q9 is REFRAMED (its first framing described the pre-fix
tree and is obsolete); Q17 and Q18 are NEW (verifier WS1-RESV, the nits round's WS3-MOVE search); Q19 is NEW
(final-round fix 3, verifier REC-NOTE-PLACEMENT-Q); the rest stand as first written.

1. **Refund on cancel.** Cancelling forfeits what went in (no refund). Keep it, or add a salvage refund (a new ledger
   source reason, an audited identity and a ruled fraction)?
2. **Default split.** Absence = 100 ("building first", the batch-2 behaviour, and what every AI Empire keeps), or a
   neutral 50, which would move AI behaviour and the AI goldens?
3. **AI structural-stall cancel on a food-surplus lapse.** Should a viability wait caused by the food surplus count as
   structural, so the AI forfeits a part-built university after a bad harvest?
   - Today it counts, as specified.
   - Alternative (a): count only the market failure. This risks a permanently stalled head holding its queue and its
     claims.
   - Alternative (b): a persistence window. This needs a new TUNE parameter or stored state.
4. **LOD keyed on the window's width** on a wrapped world, against the spec's map-rect width. The alternative is to key
   on the map rect and relax the 256-px plateau criterion.
5. **Loss of the whole-world overview at the zoom floor** (47–54 % of the world's height visible). Accept it, and should
   the queued minimap be built, and when?
6. **The AI's whole-cost prudence.** The AI orders only into an idle queue and only when its purse covers the whole
   remaining cost, while the system lets anyone start without it. Keep the exception?
7. **The generalised starvation guard** (§2). Confirm the deviation. Also confirm the stated limit: a road order able to
   pay only part of its share may leave part of that share unused for the step.
8. **Calibration drift under the cylinder** (§6). All of it is quarantined and non-gating:
   - density target-band membership 17 → 16/20;
   - the density window 19 → 20/20 (WINDOW BREACH → QUARANTINED);
   - the migration breach deepening 77.66 % → 80.83 %;
   - the CR-020 reds at 8.35755E-05 and 123,950.

   Ship `topology.wrapX` on (as shipped now), or rule otherwise.
9. **Old sessions and the topology a record carries (REFRAMED).**
   - *Done, to confirm:* pre-v33 records are rebuilt FLAT by one rule (`TopologyFlags.RecordedFlat`) in the UI resume,
     `sim inspect` and the CLI save loader. A real batch-2 session replays to the batch-2 build's own hash (§4). This
     was the verifier's suggested fix (F1); confirm it.
   - *Open:* a batch-2 session replays under batch-3's rules (WS1 and WS2 on), so where a head waited under M4-D it can
     differ. Keep that, with the notice, or replay a v32 session with the batch off as well (the PreD054 pair: flat
     worldgen, WS1 and WS2 off), which would reproduce the played world?
   - *Open:* this build's `sim replay` has no flat path (§9). Leave it to `sim inspect` and the recording build's
     recipe, or add a `--manifest`-driven flat override (a new CLI input)?
   - *Open:* the session manifest records no topology. v33 sessions are rebuilt with whatever worldgen the reading
     build ships, and `UiSession.Start`'s `wrapXOverride` is not in the manifest, so a session founded with it is not
     resumable. Record the topology in the manifest?
   - *Obsolete:* the first framing ("batch-2 sessions are re-founded on the cylinder and replayed, so the world
     differs, or refused at a non-stride size; they are never replayed flat") described the pre-fix tree.
10. **GPU seam.** The terrain sampler and the river translations across the seam can only be checked by the manual
    playtest (`docs/m5-playtest-known-issues.md` §16).
11. **Seam acceptance metric (F9).** The row-mean |ΔRGB| cannot see a partial or narrowed cross-fade. Accept the metric,
    with the eye as the judge in the playtest, or ask for a stronger one?
12. **`RoadMaterials` reason id** (optional, not done). Should the books separate road materials from building materials?
13. **The famine determinism rig** rests on 3 events at the frozen λ. Accept, or authorise a denser determinism-only rig?
14. **Cylinder equivalents for the four flat re-rigs** (§7). Wanted?
15. **Deviations to confirm:**
    - kind-11 cancels are honoured with incremental construction OFF;
    - the AI orders no ordinary project into a settlement with no labour adults;
    - `MaterialCause.Short`;
    - `ActionDomain.ResourceAllocation = 14` instead of the Roads domain.
16. **Bench.** The shipped config is +13.8 % slower over 60 turns (PathBuild on the cylinder's network). It is reported,
    not ruled.
17. **What "reserved" protects (NEW; verifier WS1-RESV).** Under the ratified definition (§1: claims are counts, not
    set-asides; FIFO applies among heads that progress in the step), a younger head can take the whole remaining need of
    an older, part-built head that is waiting, for example for builders. The verifier's probe: the older head stays at
    p = 500,000 with the purse emptied by the younger one. Your item 5 reads "Projects must not consume resources already
    reserved by other projects." Keep the definition, or should an earlier head's unmet remaining need bound later
    heads' draws from the same purse? The second moves behaviour and needs attribution, so it waits on this ruling.
18. **Movement across the seam on the shipped world (NEW; the nits round's WS3-MOVE search).** There is no unit movement
    (M7). On the shipped world a seam route and its distance are pinned (seed 16), and a seam MIGRATION is pinned only
    with trade and recipe knowledge rigged at turn 0. The search found no orderless seam flow (seed 16 is the only
    shipped seed with seam-crossing settlement routes) and no seam trade on any of the seven seam seeds in 300 turns.
    Trade across the seam is proven on the synthetic SeamWorld only. Accept this coverage?
19. **Where batch 3's dated notes sit in frozen documents (NEW; final-round fix 3, verifier REC-NOTE-PLACEMENT-Q).**
    gov-4 §6 reads: "If a frozen document creates a genuine ambiguity for current work, record the ambiguity — a queue
    entry or a CR — and leave the document alone." Batch 3 kept every superseded text as written and added dated notes.
    In D-009, D-052, D-053, ADR-005, ADR-008, ADR-032, ADR-033, ADR-038, the M1 and M3 specs and the m5w plan they sit
    at the end of the file. These sit INSIDE the body, next to the text they supersede (line numbers on the final
    tree):
    - `docs/milestones.md`, after the M4-D paragraph (l.425);
    - `docs/current-state.md` §4, after the M4-D certification run (l.593);
    - `docs/queue.md`: T3.1(b) (l.133), the M4-D entry (l.1356) and the P1 zoom-floor entry (l.1463);
    - `docs/institutions-universities.md`, inside the Materials-and-labour bullet (l.93);
    - ADR-034, in its S0 section after the resume-notice text and before its tests table (l.178);
    - `docs/session-records.md`, in the section on which machine played a session, before "What this is not"
      (l.127);
    - `docs/m5-playtest-known-issues.md`: the *Batch 3* lines in §6, §7, §9, §11, §12 and §13, which its own dated
      header note (l.8) announces;
    - CR-021: its status line, which read "Status: OPEN — ESCALATED TO THE DIRECTOR.", now reads "Status: RESOLVED
      2026-10-15 (D-054), …" with the old status kept after it as "*As first written:*" (l.3); and a dated note
      follows its opening list (l.15).

    Does gov-4 §6 allow a dated note inside a frozen body when the text it supersedes is kept as written, or should
    these notes move to file-end dated notes? Moving them would change no content, only where it sits.

---

## Dated note — 2026-10-17 (D-055, the Director's batch-4 instruction; appended at the file's end, the text above is unchanged)

D-054 §10 Q19 is unruled, so this note sits at the file's end and nothing above is rewritten. Record:
`docs/d055-batch4-reservations-attribution-allocation-ci-persistence-pathfinding.md` (D-055); contract:
`docs/adr/adr-040-schema-v34-construction-holds-pathfinder-surface-and-cli.md` (ADR-040).

- **§1 "Reservation (Director item 5)" — SUPERSEDED for queue heads.** "The claims (each queued entry's REMAINING needs)
  are advisory. … they set nothing aside" no longer holds for a settlement's queue HEAD: with `sim.json`
  `construction.reservations` (shipped on) every unreleased head SETS ASIDE its remaining need from its purse, in the
  building order, and later heads and road works may not use it (D-055 §1). Non-head entries still only claim. The
  "(a) sunk materials" half stands. The verifier's probe described under "Consequence" (a younger head emptying the purse
  an older, waiting head needs) is the defect the instruction names; under the rule the younger head waits.
- **§10 Q17 — RESOLVED** by the instruction ("Do not ask the Director to resolve this already-established requirement"):
  an earlier head's unmet remaining need bounds later heads' draws from the same purse (D-055 §1, §10).
- **§2 — building demand** is now building work's claim (draws plus set-asides, D_b = `OwnHeadClaims`), and the forecast
  road charge and the starvation guard run on S less the forecast draws and set-asides. D_r and the split's formulas are
  unchanged (D-055 §1.6).
- **§4 — schema.** v33 is no longer current: schema v34 appends section 61 `ConstructionHolds`; the readable versions are
  {32, 33, 34}, and a v33 save loads with `ConstructionHolds` empty (ADR-040 §2–§3).
- **§6 bench paragraph and §10 Q16 — CORRECTED.** The +13.8 % compared the cylinder with batch 2's ROUND-1 median; against
  a same-day flat control the cylinder costs +9.2–9.4 %, and that part is seed 42's A* search size (its shared PathBuild
  target lies farther from the settlements), not the road network, which is the same size. Over seeds 1–10 the cylinder
  settles 5 % fewer A* nodes, and the wrap arithmetic is inside the noise. The byte-identical optimisations O1–O6 cut the
  60-turn bench by 44–54 % per seed, measured on base-b3 + T5 without the hold rule (D-055 §6). The integrated batch-4
  tree (`4602ed4d…`; all of batch 4 against base-b3, at host load 1.80–7.66) ran the 60-turn bench 36.5–47.0 % faster
  per seed, 40.8 % over the five seeds' summed medians (D-055 §8, §10). (Tree qualifier added at the integration's fix
  stage, 2026-10-18.)
- **§7 / §9 and §10 Q14 — ANSWERED.** The four kept-flat re-rigs: revolt and order refusal (the `FoundedHarnessTests`
  ordered twin's subject), the decay sample and the explain-starved sample now have cylinder proofs; the
  `NonCanonicalSizeFoundingTests` recipe stays flat (the cylinder refuses non-stride sizes). The flat originals are
  unchanged (D-055 §3).
- **§5 item 13 — the m5w plan's reservations.** v34 is taken by batch 4, so S2a's known extent moves to **v35** and S3's
  Action Points state to **v36** (the m5w plan carries a dated note). Batch 4 adds no order kind.
- **§2's "Cross-process coverage. No lasting CI leg makes the split BIND"** (and its `docs/queue.md` line) is closed by the
  ci.yml step "Allocation split, binding, cross-process (D-055)" (D-055 §4).
- **Unchanged by batch 4:** Q1–Q13, Q15, Q18 and Q19 stay open as written.
