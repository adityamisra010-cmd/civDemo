# Research-economy candidate: the final four-seed trial (2026-10-26)

**Director instruction (2026-10-26, "execute the next approved trial"):**
1. Recalibrate civics Age 7 with the civics gate rule on.
2. Evaluate cost batches b1, b2, b3 and b5 together on the recalibrated candidate, keeping each batch attributable.
3. Prepare a candidate configuration only.
4. Validate it.
5. Recommend whether it is ready for a separate default-adoption decision.

**Unchanged:** `Sim.Data/content/research.json` (sha256 prefix `0e8fb9897eec23f4`), every shipped default, `main`, tags and
releases.

**Runs:** seeds 1, 2, 42 and 7, 1,630 turns each, by `OverlayRun_FourSeeds`. Every run listed here also checked replay and
mid-run save/load, unless noted.

Parts A–E are **measured**. Part F is **recommendation**.

## A. The candidate (what it is, exactly)

| Part | Value | File |
|---|---|---|
| Switches | `researchOverflow` on, `researchReserve` {3, 1}, `aiGateAwareGoals` on, `aiCivicsGateGoals` on | `trial/research-economy-candidate.json` |
| Pacing scale | The calibrated trial scale (pass 8), with **civics Age 7 = 1.479361** (was 0.757433); every other entry unchanged | same |
| Cost batches | **b1** (7 nodes, Ages 3–5, both trees, increases) and **b3** (9 civics, Ages 6–9, increases) | `trial/batches/b1-under-a3-5.json`, `trial/batches/b3-under-a6-9-civ.json` |
| Excluded | **b5** (cheaper-but-later anomalies, and civics A7 out of tolerance) and **b2** (costs seed 2 its technology Age 9). See C. | — |
| Full content file | `candidate/research.candidate.json`, sha256 prefix `4ed3c69e32bba343`, written by `tools/apply_overlay.py` from the shipped file and the overlays in `candidate/manifest.json` | `candidate/` |

**Pinned** by `Sim.Tests/Kernel/ResearchCandidateTests.cs`:
- the file is the shipped content plus the manifest's overlays, node for node (cost, rationale, tuning);
- it differs from the shipped file only in the five tuning members and the 16 batched nodes' `cost` and `costRationale`;
- the shipped content keeps every switch off.

**Teeth:** a stale candidate file, one written with b2 still in the manifest, fails two of the three tests.

**Integrity:** `tools/tree_closure_check.py candidate/research.candidate.json` reports no FAIL, and the cost model is
consistent on all 1,005 nodes.

## B. Civics Age 7, recalibrated with the civics gate rule on (measured)

The full trajectory is in `pacing-recalibration.md` §7.
- **Procedure:** `recalibrate_trial.py`, `--damp 0.5 --max-step 1.25`, every other scale frozen.

| Civics A7 scale | Civics A7 complete (s1, s2, s42, s7) | Slowest-seed residual | Every seed completes every Age? |
|---:|---|---:|---|
| 0.757 (start) | 1183, 1250, 1130, 1142 | −75 | yes |
| 0.947 | identical | −75 | yes |
| 1.183 | 1184, 1253, 1130, 1142 | −72 | yes |
| **1.479 (selected)** | 1186, **1310**, 1135, 1143 | **−15** | **yes** |
| 1.606 | 1187, 1365, 1135, 1145 | +40 | **no**: seed 2 technology A9 |
| 1.744 (the procedure's next step) | 1186, 1430, 1134, 1143 | +105 | **no**: seed 2 technology A9 |
| 1.849 | 1186, 1468, 1135, 1143 | +143 | **no**: seed 2 technology A9 |

**Why 1.479.** It is the only measured value at which both hold:
- every seed completes every Age;
- the slowest seed is within ±30 of the civics A7 target.

Civics A7 is bound by technology prerequisites on three seeds:
- `insurance_underwriting` waits on `probability_statistics`;
- `military_engineers` waits on `trace_italienne`;
- `central_banking` waits on `paper_money`.

So the procedure's pincer term cannot be satisfied on those seeds. It would keep raising the scale until seed 2 overshoots.

**The rule at 1.479.**
- **Rule on vs rule off:** bit-identical on all four seeds. At this scale the rule is inert: it starves no goal and delays no
  core milestone.
- **Prerequisites and sweep cohort:** respected on every run. `tools/check_research_order.py` checks every node's
  prerequisites and Age-sweep cohort against its completion turn over the whole run, and it HOLDs on every run in this
  document. Its teeth were proven on injected violations.
- **Determinism:** replay EQUAL and mid-run save/load equal on all four seeds, with 0 reconstruction mismatches.

## C. Cost batches on the recalibrated candidate (measured)

**Reference:** the recalibrated candidate without batches (`cand`).

**Batch table columns:** mean shift in Age-completion turn over four seeds, with the slowest seed's shift in brackets.

| Run | Tech A1–A3 | Tech A4–A6 | Tech A7–A9 | Civics A1–A3 | Civics A4–A6 | Civics A7–A9 | Age 9 entered (mean) | RP lost | Anomaly flags |
|---|---|---|---|---|---|---|---:|---|---|
| b1 alone | +0.0 [+0] | +0.7 [+3] | +0.8 [+6] | +1.2 [+4] | +1.1 [+12] | +1.2 [+6] | +1.5 | 46.2 % → 46.3 % | none |
| b2 alone | +0.0 [+0] | +0.7 [+3] | +3.8 [+5] | +0.0 [+0] | +1.0 [+8] | +3.4 [+5] | +4.0 | 46.2 % → 46.0 % | none |
| b3 alone | +0.0 [+0] | −0.3 [+0] | −0.8 [+0] | +0.0 [+0] | +2.3 [+13] | +0.4 [+11] | −0.8 | 46.2 % → 46.2 % | none |
| b5 alone | +0.0 [+0] | −0.8 [+2] | −3.8 [−1] | +0.0 [+0] | −1.6 [+13] | −7.3 [+0] | −3.8 | 46.2 % → 46.4 % | **cheaper-but-later** |
| **b1 + b2 + b3 + b5** (as instructed) | +0.0 [+0] | +1.5 [+6] | +1.4 [+7] | +1.2 [+4] | +1.6 [+21] | −0.4 [+9] | +1.5 | 46.2 % → 46.3 % | **cheaper-but-later** |
| b1 + b2 + b3 | +0.0 [+0] | +1.4 [+4] | +4.7 [+8], **1 never** | +1.2 [+4] | +3.9 [+15] | +5.9 [+17] | +5.0 | 46.2 % → 46.0 % | none |
| **b1 + b3 (candidate)** | +0.0 [+0] | +0.6 [+3] | +0.5 [+5] | +1.2 [+4] | +3.6 [+14] | +2.2 [+13] | +1.2 | 46.2 % → 46.2 % | **none** |

**Regression 1: all four batches together, isolated to b5.**
- **Civics A7 on seed 2:** −43 turns (1,310 → 1,267 alone; 1,283 combined, −27). The residual becomes −58, outside the ±30
  that selected 1.479.
- **Cheaper-but-later.** b5 cuts `steam_atmospheric` and `mine_pumping_engines`, yet both complete later:
  - `steam_atmospheric`: 1 to 6 turns later on every seed;
  - `mine_pumping_engines`: 2 to 5 turns later on seeds 1 and 7;
  - the `a8_steam` and `a9_combustion` milestones that need them are 1 to 7 turns later.
- **Attribution:** b5 alone already flags `steam_atmospheric` and `a8_steam`; b1, b2 and b3 alone flag nothing.
- **Mechanism:** not traced. b5 cuts 4 technologies and 12 civics in Ages 5–7, so the AI's research order changes. A cheaper
  node landing later is a symptom of that reordering, not of its own price.

**Regression 2: b1 + b2 + b3, isolated to b2.**

On seed 2, technology Age 9 is not completed within 1,630 turns (517 of 525 technologies done). Seed 2's technology A9
completion turn:

| Run | Seed 2 technology A9 complete |
|---|---:|
| candidate, no batch | 1625 |
| b1 alone | 1623 |
| b3 alone | 1623 |
| b2 alone | **1630**, the last turn of the run |
| b1 + b2 + b3 | **never** |
| b1 + b3 | 1623 |

- **b2 is responsible.** It raises 13 technology costs in Ages 6–9. Technology Age 9 is throughput-bound (D4, about 140
  turns at any cost), and seed 2 had 5 turns of slack.
- **Why the four-batch run hid it:** b5 alone brings seed 2's technology A9 5 turns earlier (1620), which offsets b2's +5.
- **The standard applied:** the same one that rejected civics A7 scales 1.606 and above. A slowest seed that loses an Age
  within the run is a hard regression.

**b1 + b3, the candidate, per seed** (turn each Age's finite nodes are complete; residual against the D-063 R6 target):

| Seed | Tech A1 | A2 | A3 | A4 | A5 | A6 | A7 | A8 | A9 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 1 | 269 (+29) | 480 (−30) | 649 (−41) | 758 (−62) | 905 (−65) | 1086 (−69) | 1238 (−87) | 1400 (−60) | 1541 (+21) |
| 2 | 280 (+40) | 510 (+0) | 693 (+3) | 821 (+1) | 970 (+0) | 1157 (+2) | 1320 (−5) | 1482 (+22) | 1623 (+103) |
| 42 | 271 (+31) | 485 (−25) | 657 (−33) | 770 (−50) | 902 (−68) | 1054 (−101) | 1178 (−147) | 1329 (−131) | 1474 (−46) |
| 7 | 271 (+31) | 485 (−25) | 655 (−35) | 766 (−54) | 900 (−70) | 1060 (−95) | 1194 (−131) | 1350 (−110) | 1495 (−25) |

| Seed | Civics A1 | A2 | A3 | A4 | A5 | A6 | A7 | A8 | A9 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 1 | 281 (+41) | 480 (−30) | 640 (−50) | 757 (−63) | 898 (−72) | 1102 (−53) | 1186 (−139) | 1390 (−70) | 1486 (−34) |
| 2 | 305 (+65) | 513 (+3) | 702 (+12) | 821 (+1) | 1027 (+57) | 1158 (+3) | 1323 (−2) | 1475 (+15) | 1569 (+49) |
| 42 | 286 (+46) | 486 (−24) | 648 (−42) | 771 (−49) | 899 (−71) | 1062 (−93) | 1137 (−188) | 1331 (−129) | 1416 (−104) |
| 7 | 287 (+47) | 486 (−24) | 646 (−44) | 763 (−57) | 903 (−67) | 1071 (−84) | 1148 (−177) | 1346 (−114) | 1438 (−82) |

**Four-seed comparison: the recalibrated candidate with and without the approved batches** (slowest-seed residual):

| | Tech A1 | A2 | A3 | A4 | A5 | A6 | A7 | A8 | A9 | Civics A1 | A2 | A3 | A4 | A5 | A6 | A7 | A8 | A9 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| Without batches | +40 | +0 | +3 | +1 | +1 | +4 | −3 | +24 | +105 | +65 | −1 | +10 | +1 | +54 | +5 | −15 | +14 | +51 |
| With b1 + b3 | +40 | +0 | +3 | +1 | +0 | +2 | −5 | +22 | +103 | +65 | +3 | +12 | +1 | +57 | +3 | −2 | +15 | +49 |

**Polity Age entered, shift against the unbatched candidate:**

| Seed | A2 | A3 | A4 | A5 | A6 | A7 | A8 | A9 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| 1 | +0 | +0 | +0 | +0 | +0 | +0 | +0 | +0 |
| 2 | +0 | +0 | +0 | +0 | +0 | +2 | −2 | −2 |
| 42 | +0 | +0 | +0 | +0 | +2 | +2 | +2 | +5 |
| 7 | +0 | +0 | +0 | +0 | +0 | **+14** | +2 | +2 |

- **Seed 7 enters Age 7 14 turns later.** b1 alone shows +15 there and b3 alone +16. It is recovered by Age 8 (+2).
- **Seed 7 does not bind any target.** It is 131–177 turns ahead of target in Age 7.

**RP lost by polity Age** (four seeds pooled, candidate → b1 + b3):

| Age | A1 | A2 | A3 | A4 | A5 | A6 | A7 | A8 | A9 | All |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| Without batches | 3.0 % | 2.4 % | 1.2 % | 3.6 % | 3.9 % | 4.0 % | 3.2 % | 6.5 % | 76.0 % | 46.2 % |
| b1 + b3 | 3.0 % | 2.4 % | 1.2 % | 3.6 % | 4.0 % | 3.8 % | 2.8 % | 6.6 % | 76.3 % | 46.2 % |

No Age gains two points or more. Age 9's waste is the documented throughput limitation (D4).

**Reserve behaviour.**
- **The AI** never holds a reserve in either run: 0 turns at the cap, 0 turns holding one, 0 technology waiting steps. Goal
  switches are 1–5 per run.
- **A casual player** (3-turn order delay):
  - keeps pace with the AI, with polity Age entry within −53…+3 turns;
  - holds a reserve on 162–196 turns per seed, reaches the cap on 2–9 turns, and the largest reserve is 3.00 turns of RP;
  - RP lost is 47.4 % (47.6 % under the calibrated trial).
- **An idling player** (6-turn delay):
  - falls 33–124 turns behind by Age 9;
  - reaches the cap on 18–26 turns per seed;
  - RP lost is 46.7 % (46.8 % under the calibrated trial).
- **Both proxies** fail to complete technology Age 9 on seed 2 within 1,630 turns. This is not new: it holds under the
  calibrated trial (pass 8), the four-batch run and b1 + b2 + b3 alike.
- **The idler's civics Age 7:** the idling proxy's slowest civics A7 residual is +130 here. It was −39 at pass 8, before
  the recalibration, and +82 with all four batches. The likely cause is the dearer civics Age 7. It is not isolated, because
  there is no unbatched proxy run. Reported, not compensated.

**Anomalies (b1 + b3):**
- cheaper node completed later: none;
- milestone later although a node it needs got cheaper: none;
- milestone earlier although a node it needs got dearer: none;
- waste moved into an Age: none.

**Determinism (b1 + b3):** replay EQUAL and mid-run save/load equal on all four seeds, with 0 reconstruction mismatches.
Order invariants HOLD.

## D. Validation

See §G.

## E. Adoption impact, if the candidate became the default (measured on a scratch tree; nothing was changed)

**Method.** In a scratch worktree at `b698c90`, `research.candidate.json` was copied over the shipped `research.json` and
rebuilt. Nothing from that tree is committed.

| Check | Shipped | Candidate | Consequence |
|---|---|---|---|
| `sim run --founded --seed 42 --turns 300`, per-turn hashes | final `8abc32b0…` | final `1e5f035b…`; **differs from turn 1** | Every founded/AI world is a different game from the first turn. The switches and pacing scale act at once. |
| `sim run --seed 42 --turns 400` (M0 toy world, no research) | `903c7028…` | **identical on all 400 turns** | Worlds without research are untouched. |
| UI `--resume` of a session recorded by the shipped build | resumes at turn 30 | **refused, exit 3**: "This saved session cannot be resumed with this build … Replaying its orders here produces a different world from turn 1"; the session files are unchanged | The existing trace check refuses every pre-adoption session. There is no migration and no silent reinterpretation. |
| CLI `sim run --from-save` of a shipped turn-100 save, 20 more turns | `daeeffac…` | `655a3444…`, **no warning** | **Risk.** `--from-save` restores the snapshot and continues under the new content with no content check. The old game silently continues under new rules. |
| Moved test pins | — | 23 (§G.1) | Each needs a re-pin with a switch-off twin returning the old hash. |

**Save-resume compatibility.**
- **UI sessions:** adopting the candidate makes every saved UI session from an earlier build unresumable. It fails safe, with
  a clear message.
- **The message's last line** says that when both builds are unstamped local `dev` builds, a mismatch "is a determinism
  defect". That is misleading for a deliberate content change. Stamped release builds name both builds instead.
- **CLI snapshots** (`--from-save`) keep loading and silently mix rules.
- **Proposed fix** (not made; it would be its own change):
  - record the research content hash in the save header;
  - have `--from-save` refuse, or warn on, a mismatch, as the UI trace check already does.
- **Schema:** none. v37 already carries the reserve table, and the civics gate rule adds no state.

## F. Recommendation (not measured; for the Director)

1. **Civics Age 7 = 1.479361.** Adopt it with the candidate. It is the only measured value within ±30 on the slowest seed
   that costs no seed an Age.
2. **Cost batches.** Adopt b1 and b3. Do not adopt b2 or b5 in this form:
   - **b5:** its cuts reorder the queue and produce cheaper-but-later steam nodes. Re-cut it without the A5–A7 civics or
     re-measure it after D4.
   - **b2:** sound on pricing, but it consumes the last 5 turns of seed 2's technology Age 9 slack. Revisit it after D4
     (final-Age throughput).
3. **Readiness.** The combined candidate (switches + recalibrated scale + b1 + b3) **is ready for a separate
   default-adoption decision**. All four seeds complete every Age, there are no anomaly flags, replay and save/load are
   exact, the AI never holds a reserve, and the order invariants hold. Conditions for that decision:
   - (a) re-pin the moved pins in §G, each with a switch-off attribution twin;
   - (b) accept that UI sessions saved earlier are refused, and decide whether to fix `--from-save` first (recommended);
   - (c) the open items stay open and are not blockers: technology Age 9 throughput (D4), Age 1 residual and seed spread
     (D5).
4. **Not done, as instructed:** papyrus and telephone effects, final-Age repeatables, broader progression changes, and the
   default switch itself.

## G. Validation and moved pins

### G.1 Pins that would move if the candidate became the default (measured)

**How measured.**
- Scratch tree at `b698c90` with `research.candidate.json` as the shipped content.
- Ran the full `Sim.Tests` (556 kernel + 2,497 other) and `Sim.Ui.Tests` (1,119) suites.
- Result: 23 tests fail, every one of them a pin of the current default. No test outside these four groups fails.
- `ResearchContentTests.Canonical_Costs_…` passes on b1 + b3. It failed on a b1 + b2 + b3 candidate.

| Group | Tests | What moves | What adoption must do |
|---|---|---|---|
| **Switch-off guards** (assert the shipped content keeps a switch off) | `ResearchOverlayTests.ShippedResearch_KeepsTheThreeTrialSwitchesAbsent`; `AiCivicsGateGoalTests.Loader_SwitchParsed_AbsentIsFalse_ShippedResearchLeavesItAbsent`; `ResearchReserveTests.TheShippedContent_HasNoReserve_TheSwitchIsOff`; `ResearchReserveLayoutAttributionTests.TheShippedContent_HasTheSwitchOff_…`; `Sim.Ui.Tests ResearchReserveDisplayTests.WithTheSwitchOff_NoSurfaceCarriesAReserve` | The switches are on by design | Invert each guard to the adopted values. Keep a switch-off twin (TestConfigs) proving each old behaviour is still reachable. |
| **World-hash goldens** | `SnapshotTests.FoundedGolden_Seed42Turn300` (`8abc32b0…` → `1e5f035b…`, also `FOUNDED_GOLDEN` in `ci.yml`); `DrivenGoldenTests.DrivenGolden_Seed42Turn300`; `CivicsPointsAttributionTests` ×4; `ResearchLevelsLayoutAttributionTests` ×3; `AiFoundingAttributionTests.WithTheSwitchOff_…`; `FirstReignTests.FirstReign_PostFix_…` (hash); `DiscoverySchemaV35Tests.AV34Save_…` (first step after a v34 load) | Any world that runs research | Re-pin each. Each needs an attribution control: the same run with the candidate overlay removed returns the old hash. |
| **AI trajectory pin** | `AiEmpireIntegrationTests.OneAiEmpire_OnTheCanonicalFoundedWorld_…` | First target and first build unchanged (0, 2). Age II advance 65/66 → **73/74**. | Re-pin, attributing the move with the switch-off twin. Not attributed here to a single switch. The four-seed runs show the same Age II entry, turns 74–75. |
| **UI text and recorded sessions** | `ActionSurfaceTests.TurnOne_TheStatusBand_…` and `ResearchPointDisplayTests.WaitingLane_…`; `Batch2SessionResumeTests` ×2 and `ConstructionLifecycleResumeTests.TheRealV33Session_…` | The status line gains "- reserve 0/93" (the reserve is on). Three recorded real sessions no longer replay into their trace: the same refusal as §E. | Re-word the two text pins. Re-record the three session fixtures under the new default, or pin them to the switch-off config. |

**Unchanged under the candidate:**
- the M0 toy-world goldens;
- the CI allocation leg's four pinned `alloc*.bin` hashes (`sha256sum -c` passes);
- every other test.

### G.2 Validation of this branch (shipped defaults; candidate files present)

See §G.3, recorded from the final validation run.
