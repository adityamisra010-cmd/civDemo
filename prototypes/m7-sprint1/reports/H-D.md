# H-D — M7 lane D: caps and counted objects, support capabilities, War Sandbox, economic scale (Phase D groundwork)

Date 2026-11-09. Clone `/home/user/m7w/D`, branch `lane/D`, base `45c3313` (the development branch `claude/new-session-yavghm`
as handed to this lane). No clone or checkpoint existed for lane D at the start: the lane was done from scratch on a fresh clone.
ISOLATED PROTOTYPE AND DOCS ONLY: nothing in `Sim.Core`, `Sim.Data`, `Sim.Cli`, `Sim.Tests` or `Sim.Ui` changed (D-079 Q5
OPEN); no unit data or cap changed (D-079 §4 D10); no permanent project, no CI file.

Status words: **MEASURED** (run in this lane, on this tree), **DERIVED** (exact arithmetic, pinned by a test), **INFERRED**
(argued, not isolated), **PROPOSAL** (lane D's design under a delegation; decided by nobody), **READING** (an interpretation,
both alternatives stated), **DD** (a quoted Director decision).

Authority: D-079 DR24, DR25, DR29, DR30, DR31, DR32, DR33, §4 D8 (support part), D10, D15, D20, §1.5 R2, R4, R5, R6, R7;
§5 Q3, Q4, Q5, Q6, Q14 stay OPEN and are not decided here; D-076 §5 (CAP-1..CAP-5); charter §4.5 (categories A–E; D-065 R8).
The DIR5 verbatim copy is lost; the records' quoted text is the authority, and no lost figure is reconstructed.

---

## 0. Summary

| Brief item | State | Evidence |
|---|---|---|
| (1) Caps and counted objects | **Done.** *(Round 2: `ReinforceToCaps`' two stopping rules had no test until round 2; note below.)* Proposal `docs/m7-plan/caps-and-counted-objects-proposal.md` (mortar counted object with three alternatives and a labelled recommendation: the crewed tube; CAP-1..CAP-5 along D10's basis with every touched data row cited by line, nothing changed; A–E classification of every new identity; READING S1 for the sandbox). Prototype: `Proto.M7/Caps/` — DR24 cap check at formation creation and at every edit, refusal codes, exact conservation, tie-dense distribution; lane N's CAP-5 treatment reused | §1, §6, §7 |
| (2) Support capabilities | **Done.** *(Round 2: not as stated; see the round-2 note below the table.)* Proposal `docs/m7-plan/support-capabilities-proposal.md`; prototype `Proto.M7/Support/` over lane E's economy (default-off hook): allocation per War Pulse (why: §2), saturating stacking, limits, costs through lane E's per-pulse settlement, medics on lane W's collapse split and the care of the wounded, supply wagons inside lane E's train and supply line, the morale rule through the TacticalResolver's end morale; four new serialized rows | §2, §6, §7 |
| (3) War Sandbox | **Done (core).** *(Round 2: the replay could be defeated through the inspection surface until round 2; note below.)* Proposal `docs/m7-plan/war-sandbox-proposal.md`; prototype `Proto.M7/Sandbox/`: isolated session with a sandbox-scoped ledger (D15 form), campaign hash identical before and after a sandbox battle (MEASURED), any designed family regardless of Age, terrain editing refused for an encounter, deployment only through lane TR's `DeploymentPhase`, AI-vs-AI through lane P's AutoResolver (acceptance OPEN, Q4), replay with a hash pin and a turn-exact order-delivery pin; the two thirds-violating scenarios offered as a PROPOSAL only | §3, §6, §7 |
| (4) Economic scale | **Done.** *(Round 2: one MEASURED row was the formula; note below.)* Population trace re-measured on HEAD with the economy prototype (byte-identical to the CLI over all 1,630 turns, four seeds) — Q-L2 resolved by measurement; proposal `docs/m7-plan/economic-scale-proposal.md` with measured evidence over 4–6 seeds and horizons up to turn 1630; both food orderings shown where results depend on them (lane F's comparison regenerated on HEAD: identical apart from a wall-time field, battery CSV SHA-256 equal); no data change | §4, §8 |
| (5) This report | Done | — |

*Verification round 1 (§13, 2026-11-09): the verifier's 18 findings were all REPRODUCED. Fixes: the sandbox casualty rows are
now pinned against the battle result; the support cost terms are pinned term by term; the gun crew is fielded with its gun;
the sandbox's terrain is handed out only as copies; large forces are laid out inside the field; the codec tests now tell every
same-typed field pair apart. The documents gain READING C1 (what a soldier-class cap counts), READING A (the AI-vs-AI
fallback) and a complete CAP-3 classing. Statements that round 1 superseded are marked in place below.*

*Verification round 2 (§14, 2026-11-09): the verifier's 6 findings were all REPRODUCED. "Done" for item (2) did not hold as
stated: the after-battle morale rule raised campaign morale through combat (D-R2-01), and nine documented support mechanisms had
no semantic test (D-R2-02). Fixes: a battle now lowers the campaign morale by its net loss and never raises it; every raw
allocation order is refused and recorded instead of crashing the world, and refusal code 4 is reachable; the sandbox hands out
copies of its ledger, deployment phase and battle result; the economy probe runs the real twin on research-driven worlds too, and
the economic-scale table's research row at turn 25 now shows the twin (stone binds in three of four seeds). Twenty-one new test
cases (20 in M7Proto, 1 in M7Econ) pin the fixes and the previously untested mechanisms (the support layer, `ReinforceToCaps`'
stopping rules, the sandbox surface, the research twin). Statements that round 2 superseded are marked in place below.*

---

## 1. What exists (files; all new unless marked)

| File | Role |
|---|---|
| `docs/m7-plan/caps-and-counted-objects-proposal.md` | item (1) PROPOSAL |
| `docs/m7-plan/support-capabilities-proposal.md` | item (2) PROPOSAL |
| `docs/m7-plan/war-sandbox-proposal.md` | item (3) PROPOSAL |
| `docs/m7-plan/economic-scale-proposal.md` | item (4) PROPOSAL and evidence |
| `Proto.M7/Caps/DirectorCaps.cs` | DR24 caps as prototype constants (citing DR24, not Sim.Data); counted object per class; persons-per-machine input with no shipped value; `Check` |
| `Proto.M7/Caps/FormationRoster.cs` | creation and edits (create, split, merge, reinforce, detach, transfer, disband) with the cap check, all-or-nothing, people in a `WarLedger`; `ReinforceToCaps` on the exact (fill fraction, id) key; `SplitSizes` |
| `Proto.M7/Support/SupportTypes.cs`, `SupportMath.cs`, `SupportCodec.cs`, `SupportWorld.cs` | the support layer: kinds, refusal codes, PROPOSAL coefficients, pure equations, four row codecs, the world composing lane E's |
| `Proto.M7/WarEcon/SupportOverlay.cs` | the seam (`ISupportOverlay`, `SupportComposition`) in lane E's namespace |
| `Proto.M7/WarEcon/WarEconomyWorld.cs` (**lane E's file, edited**) | default-off hook: `WarEconSetup.Support` (null = unchanged), the latched support in the pulse plan (needs, train capacity, supply line, march, pay), four read accessors and `SetMoraleAfterBattle` — 53 lines added and 9 replaced (`git diff --numstat`), each change under a `[D]` comment |
| `Proto.M7/War/WarLedger.cs` (**shared file, edited**) | `Commodity.Vehicles = 13` appended (codes 0–12 unchanged) |
| `Proto.M7/Sandbox/SandboxTypes.cs`, `SandboxSession.cs`, `SandboxCodec.cs` | the sandbox core, its two row codecs and the read-only encounter snapshot of lane W's campaign world |
| `Proto.M7.Econ/ScaleProbe.cs`; `Proto.M7.Econ/Program.cs` (**the economy prototype's entry point, edited: 2 lines added, a comment and the dispatch**) | the `scale-probe` measurement command |
| `Proto.M7.Tests/LaneD/CapsRosterTests.cs`, `SupportCapabilityTests.cs`, `SandboxTests.cs` | 50 test cases (24 + 15 + 11) *(round 1: 64 = 26 + 20 + 18, plus the helper `RowFieldCoverage.cs`; §13)* |
| `Proto.M7.Econ.Tests/ScaleProbeTests.cs` | 9 test cases (two facts and a seven-case theory) |

---

## 2. The designs (one paragraph each; the documents hold the detail)

**Caps.** One rule for every class: people are the conserved stock; machines are battle-transient (READING R7), so a
machine-counted cap is checked on ⌈persons ÷ persons per machine⌉. Mortar counted object: the crewed tube (recommended over
crew members and teams). CAP-1: retune 306–308 (`unit-families.json:81–83`) to the heavy cap as TUNE in M7.6 (or a lighter
classing). CAP-2: class spear as heavy/aggregated and retune 204 (`:64`). CAP-3: realistic crews with personnel retuned (land
vehicles and aircraft fit the bands; ships need the band set extended). CAP-4: a category-A sniper identity (a branch of family
4) and a category-A mortar identity (recommended home: a branch of siege, family 8), neither created. CAP-5: no cap invented;
R6 classing only; no performance cap proposed (no measurement shows the need).
*Round 1 (§13): "one rule for every class" was not true across lane D's own deliverables. The Campaign Encounter applies lane
W's entity rule, which is now READING C1 with both alternatives (D-R1-03). CAP-1 is now retuned to the cap, 50, per D10's
basis (D-R1-15). CAP-3 now has its full class membership; option (A)'s values were corrected, because a fixed-wing value of 20
refuses the 40-personnel bombers (D-R1-08).*

**Support.** Two capabilities from the DIR5 examples (field medical, supply train); combat engineers a labelled proposal only.
A polity's pool; standing targets set by panel orders; applied at the army's next War Pulse start inside lane E's plan, in lane
E's (Priority, Id) order; limits per 1000 men; saturating stacking u(c) = c ÷ (c + c₀); medics move a share of the battle's dead
to the wounded inside lane W's collapse and run the care of the wounded at per-sim-year rates with lane E's daily compounding;
supply wagons are lane E's wagons for the pulse (capacity, throughput, the ox-train pace, fodder); costs are lane E's ration,
pay and fodder terms; the army's campaign morale becomes the TacticalResolver's end morale after a battle, plus a medical
restore.
*Round 1 (§13): support is allocated to ARMIES, not to formations as the brief said; the deviation and its reasons are now
stated (D-R1-13). Only the after-battle half of the morale rule is implemented; the start half is an unchecked assumption, and
a stand-in battle can raise morale (MEASURED, D-R1-16).*
*Round 2 (§14): the after-battle half itself raised morale through combat. The TacticalResolver's in-battle recovery lifts a
formation that holds without loss above its start (archers 800 → 880, MEASURED), and the rule SET the campaign morale to the end
morale, so a battle started at the army's own morale raised it (D-R2-01). The rule is now "the campaign morale falls by the
battle's net loss (start − end, less the medical restore) and never rises". Nine documented mechanisms (coverage from the staff
granted, the hitch rule, care over the elapsed time and its carries, refusal codes 5 and 6, a disbanded army's wounded going
home, the in-kind pay) gained their first semantic tests (D-R2-02).*

**Sandbox.** A session owns its own ledger: forces minted from its own mint, casualties through lane W's collapse into its own
sinks; no reference to any campaign world (an encounter copies a value snapshot). Any designed family with a battle profile, any
Age; campaign availability stays computed-Age-gated outside the sandbox. Caps per formation (READING S1 (a)). Terrain editable
except in an encounter. Deployment only through lane TR's phase. AI-vs-AI through lane P's default policy. Replay from the
session log and the battle's order log.
*Round 1 (§13): "caps per formation" does not hold in a Campaign Encounter, which fields lane W's formations by the entity
rule (READING C1, D-R1-03). The AI-vs-AI fallback to the TacticalResolver is READING A, not a Director decision (D-R1-12). The
gun crew now carries its gun (D-R1-09). The terrain lock now holds beyond the API (D-R1-10). Large forces are laid out
inside the field (D-R1-14).*
*Round 2 (§14, D-R2-06): the replay could still be defeated through the inspection surface: the live deployment phase accepted
an unlogged proposal that the battle was fought from, and the live ledger and battle result accepted writes (MEASURED). All
three are now handed out as copies.*

**Economic scale.** The shipped predicate binds on adults wherever grain is present, so with research progress 50,000 soldiers
are recruitable at once from about turn 200; later the purse often holds no grain and the predicate blocks every recruitment.
The economy grows four orders of magnitude over the game, so "50,000 difficult" needs the Director to say at which stage (Q-S1).
Proposed mechanism for A-38: a recurring per-soldier maintenance in produced goods and money, settled per War Pulse through
lane E's path, sized by the identity's equipment.
*Round 2 (§14, D-R2-05): "binds on adults wherever grain is present" does not hold at turn 25, where stone binds in four of six
no-order seeds and, by the real twin measured only now, in three of four research seeds (2,260–3,540 accepted against the
formula's 3,320–3,540).*

---

## 3. Readings applied (each with both alternatives in its document)

| Reading | Applied | Alternative | Where |
|---|---|---|---|
| R2 (D-079): "era" = computed Age | campaign availability by computed Age; the sandbox ignores it (DR33) | calendar eras → a CR | sandbox proposal §3 |
| R4 (D-079): Battle Test autoplay is AI-vs-AI | AutoResolver | TacticalResolver autoplay | sandbox proposal §1 |
| R5 (D-079): deployment zones = the thirds | only lane TR's phase | editor-defined zones | sandbox proposal §5 |
| R6 (D-079): every vessel family under Ships 5; drones by airframe class | applied in the classification | vessel families uncapped | caps proposal §3 CAP-5 |
| R7 (D-079): vehicles battle-transient | counted from people | a conserved machine stock (V2) | caps proposal §2 |
| **S1 (lane D): caps in the sandbox** | (a) caps per formation; large forces split | (b) caps lifted in the sandbox | caps §5, sandbox §4 |
| **M (lane D): "morale decreases when troops participate in combat"** | (a) the TacticalResolver's casualty-driven loss, carried into the campaign (since round 2: as the battle's net loss, never a gain; D-R2-01) | (b) an extra participation term in both resolvers | support §3.3 |
| **C1 (lane D, round 1): what a soldier-class cap counts** | (a) persons: caps prototype and sandbox force creation (the recovered table's "50 soldiers"; D10's "retune personnel to the cap") | (b) battle entities, n ≤ cap, each standing for persons: lane W's collapse, and so the sandbox's Campaign Encounter | caps §2.2, sandbox §4 |
| **A (lane D, round 1): AI-vs-AI battles the AutoResolver cannot model** | (a) lane P's default policy: TacticalResolver, headless | (b) refused until the AutoResolver covers those forces (DR30's literal text) | sandbox §6 |

---

## 4. Decisions the Director must take (only genuinely undecided ones)

1. **Q-S1 — at which stage of the game DR25's numbers apply** (economic-scale proposal §6). (a) Absolute at every stage → a
   per-soldier cost growing by orders of magnitude with the era; (b) at a reference stage → later eras field larger armies as
   the economy grows; (c) as shares of the polity → one calibration for all stages, DR25 restated relatively. Gates the A-38
   calibration (with Q3).
2. **READING S1** (caps in the sandbox): confirm (a) or choose (b).
3. **READING M** (morale and combat participation): confirm (a) or choose (b) (a change to both resolvers; parity and tactical
   pins move).
4. **The two thirds-violating shipped scenarios** (sandbox proposal §5.1): accept option 1 (authored starts, Battle Test only,
   labelled) or only option 2 (a DIR5-legal variant beside them). Allowing an ambush to start in the middle third as a rule
   would amend DR31; not proposed.
5. Already flagged by D-079 and unchanged here: **R6** confirmation.
6. *(Round 1.)* **READING C1** (caps proposal §2.2): confirm that a soldier-class cap counts persons (a), or say that it counts
   battle entities (b). This is delegated design for M7.6 ("define precisely what is counted for each family"). It is flagged
   because it fixes what DR24's caps mean, and lanes W and D apply different rules today.
7. *(Round 1.)* **READING A** (sandbox proposal §6): confirm the fallback to the TacticalResolver (a), or require refusal (b).

Everything else in the four documents is delegated (D8, D10, D15, D20) and is presented for review, not for decision.

---

## 5. MEASURED vs INFERRED (the main claims)

| Claim | Label | Evidence |
|---|---|---|
| The HEAD trace of `sim research --seed 42 --turns 1630 --auto cheapest` differs from the committed CSV from turn 1; 89,971,315 against 83,718,486 at turn 1630 | MEASURED | CLI on HEAD, `cmp`; ratios §8 |
| The economy prototype's research driver reproduces the CLI trace byte for byte (turn, population), 1,630 turns, seeds 42, 1, 2, 7 | MEASURED | `scale-probe` vs CLI, `diff`; first 25 turns pinned by a test |
| Lane F's six baseline fixtures regenerate on HEAD identically apart from the wall-time field; the regenerated battery CSV has the same SHA-256 (`47b67aeb…cf0c`) | MEASURED | §8 |
| Real recruitment twins on HEAD accept 2,240–3,500 (turn 25) and 10,880–13,680 (turn 200) extra soldiers (no orders, six seeds) | MEASURED | `twins-none.csv` |
| With the research driver the predicate allows 134,840–150,240 extra soldiers at turn 200 | MEASURED (formula; validated against the twin where adults bind) *(round 2, D-R2-05: that validation was of the no-order twin; the research twin itself, measured in round 2, accepts 134,840–150,260, every issued order accepted)* | `probe-research.csv`; round 2: `D-fix2/logs/twin200-seed*.csv` |
| *(Round 2, D-R2-05.)* With the research driver the real twin accepts 2,260–3,540 extra soldiers at turn 25 (the formula: 3,320–3,540); stone binds in seeds 42, 2 and 7 | MEASURED (twin; harness and `scale-probe --twin-at 25`) | `D-fix2/logs/twin25-seed*.csv` |
| From turn 650 to 1630 every sampled boundary of every research seed has no grain in the purse; the predicate refuses all recruitment | MEASURED (three of four seeds also at turn 500; seed 42 also at 425, 450, 475, 550, 600) | probe CSVs; diagnostic run |
| Why the grain purse is empty | INFERRED (basket consumption) | not isolated |
| 50,000 soldiers cost 34–56 % of the research world's surplus at turn 200 in lost labour | INFERRED from measured quantities (labour cost per soldier from lane F's turn-25 and turn-200 twins) | economic-scale proposal §4 |
| Lane E's economy is byte-identical with the support layer attached and nothing allocated | MEASURED | `WithoutSupport_LaneEsEconomyIsByteIdentical`; all lane E and S tests pass |
| A campaign world's state hash is identical before and after a sandbox encounter battle; it equals a twin afterwards | MEASURED | `CampaignWorld_StateHashIdentical_BeforeAndAfterASandboxBattle` |
| The support and sandbox coefficients (save, care rates, limits, restore, default objectives) | PROPOSAL values, not calibrated | the documents |
| A formation of 5 mortar tubes is a historical section or platoon size | INFERRED, unverified general knowledge | caps proposal §2.1 |

---

## 6. Tests (MEASURED; `nice -n 19`, `-m:1`, `-nodeReuse:false`, `DOTNET_CLI_USE_MSBUILD_SERVER=0`, solutions named)

Baseline on `45c3313` before any change (2026-11-09 19:08–19:14Z): **M7Proto** Debug 1151/0/0, Release 1151/0/0; **M7Econ**
Debug 101/0/0, Release 101/0/0; every build 0 warnings, 0 errors.

**Closing runs** (2026-11-09 20:11–20:19Z, script `…/scratchpad/m7c/D-work/final.sh`, forced rebuild `--no-incremental`;
the code was `de6ebad`'s — the edits made during the run were documents only):

| Solution | Config | Build | Tests (passed / failed / skipped) | Test run |
|---|---|---|---|---|
| M7Proto.slnx | Debug | 0 warnings, 0 errors | **1201 / 0 / 0** (1151 baseline + 50 lane D) | 50 s |
| M7Proto.slnx | Release | 0 warnings, 0 errors | **1201 / 0 / 0** | 20 s |
| M7Econ.slnx | Debug | 0 warnings, 0 errors | **110 / 0 / 0** (101 baseline + 9 lane D) | 1 m 48 s |
| M7Econ.slnx | Release | 0 warnings, 0 errors | **110 / 0 / 0** | 1 m 46 s |

(`M7Econ.slnx` builds `Sim.Core` and `Sim.Data` as project references, read-only; nothing in them changed.)

**Lane D tests and what each pins** (semantic unless marked):
- Caps (24): DR24 values equal lane N's battle-entry table; every class accepted at the cap and refused one person above (the
  counted value and cap in the verdict; a refused edit changes nothing); machine classes without a crew rule refused; uncapped
  classes never refused for size; refusal codes of create, split, merge (classes, identities, owners), reinforce, detach,
  transfer; a 4,000-edit deterministic mixed sequence conserving people exactly after every edit with every formation within its
  cap; tie-dense `ReinforceToCaps` (eight equal formations in six creation orders; the exact cross-class tie 10/50 = 12/60;
  an independent BigInteger oracle); `SplitSizes`; identical log and hash for the same edit sequence.
  *Superseded in part (round 1, D-R1-05): "conserving people exactly" was checked on the ledger total, which is conserved by
  construction; a Disband that stranded its people passed. The sequence now checks depots + formations = opening, and Disband
  has its own test.*
- Support (15): byte-identical lane E without support; the supply line created by support wagons; the ox-train pace (exact lane
  E formula); the cost terms recomputed exactly (standing need, pay due); the medical save (exact) with lane W's split when no
  staff; the care rates and HeadcountLoss equality; the care in the world (exact per-pulse strength identity, conservation,
  contiguity); morale from a REAL TacticalResolver battle; the turn-exact allocation delivery pin (five instants); tie-dense pool
  contention under permuted setups and priorities; limits re-applied at every latch and refusal codes; saturating stacking and
  target replacement; returns and disbandment conserving every commodity; populated-table codec round trip (four row types);
  replay from the order log.
  *Superseded in part (round 1): "the cost terms recomputed exactly" held only for the standing need and pay of a halted,
  battle-free first pulse; four documented terms were unpinned (D-R1-02). "Morale … from a real battle" computed its expected
  restore with the function under test (D-R1-04). The battle's destinations for prisoners and deserters were unpinned
  (D-R1-06). The populated tables had ArmyId = WarId in every allocation row (D-R1-07). Each now has its own test (§13).*
- Sandbox (11): AI-vs-AI equals a direct AutoResolver run; sandbox accounting exact; prisoners and dead in their own sinks (a
  real TacticalResolver battle that takes prisoners); campaign isolation (state hash, totals, twin); terrain editing and the
  encounter lock; any family any Age with campaign availability by computed Age; the split at the cap; deployment only through
  the thirds; the order-delivery pin with replay; whole-session replay (a different seed differs); populated-table codec round
  trip.
  *Superseded in part (round 1): "sandbox accounting exact" meant that the totals conserve. No test compared a casualty row
  with the battle result, and two wrong mappings passed (D-R1-01). The encounter lock was API-only (D-R1-10). The casualty
  table had Wounded = Captured = 0 in every row (D-R1-07). Each is now pinned (§13).*
- Economy probe (9 cases): the predicate bound at its rules' boundaries (seven cases: one adult stays, the purse binds, owned
  soldiers, nothing recruitable); the formula bound equals the real twin where adults bind (seed 1, turn 25: 3,500); HEAD pin (a
  golden, labelled) of the research-driver trace, turns 1–25 of seed 42.

**Moved pins:** none. Every pre-existing test passes unchanged in Debug and Release (counts above), including all of lane E's
and lane S's (the hook is default-off) and every test that encodes `Commodity` (code 13 is appended).

**Determinism scan:** `scripts/check-proto-determinism.sh` — scanned 102 files under `Proto.M7/`; pattern groups with findings: 0;
CLEAN (the scan covers lane D's new `Caps`, `Support` and `Sandbox` code; it does not cover `Proto.M7.Econ`, as before).

**Doc links:** `python3 scripts/check-doc-links.py --all` (every `*.md` under `docs/` and the repository root) — 461 files
checked, 0 broken links; and, since `--all` does not cover `prototypes/`, this report checked by name:
`python3 scripts/check-doc-links.py prototypes/m7-sprint1/reports/H-D.md` — 1 file, 0 broken links. Both re-run after the
last edit.

---

## 7. Mutants (bounded; each kill backed by a semantic test)

Bound: 35 s per test run, about 10 × the measured clean filtered run of the lane D tests (3.2–3.4 s wall, 49 tests); M19 (the
economy probe) 440 s, 10 × its clean filtered run (44 s). Each mutant is one exact string replaced in one file (asserted unique),
a rebuild, the filtered run, and the file restored from the in-memory original. Script and raw results:
`…/scratchpad/m7c/D-work/mutants.py`, `mutants/results-run1.json`, `results-run2.json`, `mutant-probe.py`, `mutant-probe.log`
(round 1), `mutant-probe-round2.log`.
*Superseded in part (verification round 1, D-R1-18): the bound covered the TEST step only. The rebuild before it had no
timeout (`build()` calls `run()` without one), so a mutant that hung the build would not have been stopped. None did. Round
1's script bounds the build and the test together (§13).*

| # | Mutation | Result | Killed by (semantic) | Run |
|---|---|---|---|---|
| M1 | machine count ceil → floor | KILLED | `Create_AcceptedAtTheCap_RefusedOnePersonAbove` | 3.1 s |
| M2 | cap check off by one | KILLED | `Create_AcceptedAtTheCap_RefusedOnePersonAbove` (+6) | 3.2 s |
| M3 | tie-break to the highest id | KILLED | `ReinforceToCaps_TieDense_…`, `…ExactFractionTies…MatchesOracle` | 3.5 s |
| M4 | merge across identities | KILLED | `Merge_RefusedAboveTheCap_AcrossClassesAndAcrossIdentities` | 3.3 s |
| M5 | split remainder to the last parts | KILLED | `SplitSizes_ExactFewestEvenParts` (+1) | 3.5 s |
| M6 | medical save without saturation | KILLED | `Medical_BattleSave_…`, `Stacking_IsSaturating_…` | 3.5 s |
| M7 | medical limit ignored | KILLED | `Limits_AndRefusalCodes` | 3.4 s |
| M8 | care does not cut deaths of wounds | KILLED | `Medical_Care_MoreRecoverFewerDie_…` | 3.4 s |
| M9 | recovered wounded sent to the dead | round 1 **SURVIVED**; round 2 KILLED | `Medical_CareInTheWorld_…` after adding the exact per-pulse strength identity | 3.4 / 4.7 s |
| M10 | battle leaves campaign morale unchanged | KILLED | `Morale_ACampaignArmyLeavesABattleAtTheTacticalEndMorale_…` | 3.6 s |
| M11 | support wagons left out of lane E's supply line | KILLED | `SupplyTrain_RunsASupplyLine_…` | 3.3 s |
| M12 | support staff unpaid | KILLED | `SupportCosts_AreLaneEsRationPayAndFodderTerms_Exactly` | 3.3 s |
| M13 | encounter terrain editable | KILLED | `TerrainEditing_AllowedInTheSandbox_RefusedInACampaignEncounter` | 3.2 s |
| M14 | no split at the cap | KILLED | `ForceCreation_SplitsAtTheDirectorCap` (+2) | 3.4 s |
| M15 | AI-vs-AI on the TacticalResolver | KILLED | `AiVsAi_UsesTheAutoResolverModel2_Unchanged` | 14.2 s |
| M16 | captured counted as dead | round 1 **SURVIVED**; round 2 KILLED | `SandboxAccounting_PrisonersAndDeadReachTheirOwnSinks` (new) | 3.3 / 5.0 s |
| M17 | no battle objectives | KILLED | `SandboxAccounting_ConservesExactly_…`, `CampaignWorld_StateHash…` (+1) | 2.9 s |
| M18 | replay skips terrain edits | KILLED | `Replay_ReproducesAWholeSession_BitForBit` | 3.4 s |
| M19 | probe adult bound loses "one adult stays" | round 1 **SURVIVED**; round 2 KILLED | `PredicateBound_AtTheRulesBoundaries` (new; the bound extracted unchanged into `ScaleProbe.PredicateBoundExtra`) | 46.6 / 45.1 s |

No mutant hung. **The three round-1 survivors were real test gaps, not mutations the system may tolerate:** M9 survived because the
world-care test compared total men between two runs, and the medical morale restore lowers desertion in the medical run, so men
stayed higher even with recovered men killed (teeth without aim); the exact per-pulse identity now pins the mechanism. M16 was
equivalent in every tested sandbox battle because the AutoResolver's surrender share is 0; a TacticalResolver battle that takes
prisoners now pins the captured sink. M19 was equivalent in the one world the probe test steps (no settlement held an exact
multiple of 20 adults); the predicate's boundary is now tested directly. All three properties are ones the system ought to have
(ADR-015 §7.2): returned men rejoin the ranks; prisoners are not dead; one adult stays.

---

## 8. Measurements (raw outputs in `…/scratchpad/m7c/D-work/`)

| Run | Command | Wall | Output |
|---|---|---|---|
| CLI traces | `dotnet Sim.Cli.dll research --seed S --turns 1630 --auto cheapest --emit-orders … --trace-population …`, S = 42, 1, 2, 7 (Release build of Sim.Cli into the scratchpad; read-only use) | 56–59 s per seed | `trace/population-trace-seedS.csv`, world hashes `f3a004a8…` (42), `3c639603…` (1), `06fccfb5…` (2), `d7c43286…` (7) |
| Research probe | `scale-probe --seeds 42,1,2,7 --driver research --turns 0,1,25,100,200,250,325,400,500,650,800,1000,1200,1630` | 1,122 s | `probe/research/probe-research.csv`; traces equal the CLI's |
| Order-less probe + twins | `scale-probe --seeds 1,2,3,5,7,42 --driver none --turns 0,1,25,100,200,325 --twin-at 25,200` | 640 s | `probe/none/probe-none.csv`, `twins-none.csv` |
| Diagnostic | `scale-probe --seeds 42 --driver research --turns 200,325,400,425,450,475,500,550,600,650` (adds purse grain and first refusal) | — | `econ-diag/out/probe-research.csv` |
| Lane F on HEAD | `food-baseline --seeds 1,2,42,7,3,5 --max-turn 325 --twins 25,200 --otwin-horizon 20`; `food-compare` | 182 s; 88 s | `lanef/fixtures/`, `lanef/H-F-tables-HEAD.md` (differs from `reports/H-F-tables.md` only in its wall-time field), `lanef/runs-HEAD.csv` |

Committed-CSV vs HEAD (seed 42): ratio 0.9957 (t1), 0.9904 (t25), 0.9831 (t100), 0.9815 (t200), 0.9724 (t325), 1.0333 (t500),
1.0556 (t800), 1.0863 (t1000), 1.0748 (t1200), 1.0747 (t1630).
*Verification round 1 (D-R1-11): these ten sampled turns do not bound the deviation. Over all 1,630 turns it runs from −4.62 %
(turn 370) to +13.69 % (turn 1061), MEASURED; see §13.*

---

## 9. Limitations

1. Prototype only: the support layer composes lane E's world and the battle hand-off is a stand-in (lane W collapse inputs);
   the sandbox fields only the five families with battle profiles; naval and air forces are not offered by the sandbox.
2. Support staff and animals are not exposed to lane E's desertion, disease or animal mortality; pools are opening inputs (no
   raising cost); ammunition endurance is unaffected by support wagons (lane E delivers munitions without carriage loss).
3. The care of the wounded and its base rates stand in for M7.14's attrition model.
4. The economic-scale evidence uses one research driver (cheapest first) and four seeds for the research worlds; the labour
   cost per soldier is measured at turns 25 and 200 only; why the grain purse empties in later eras is not isolated.
5. The predicate formula ignores material inputs (stone binds early in four seeds); the twin is the authority there.
   *(Round 2, D-R2-05: four of six no-order seeds and three of four research seeds at turn 25; the research twins were
   measured only in round 2.)*
6. No Windows run; no hardware GPU involvement (none needed: no graphics work in this lane).
7. The sandbox's default objectives and every support coefficient are PROPOSAL values with no calibration.
8. *(Round 1.)* Lanes W and D count soldier-class caps differently (READING C1); the sandbox's Campaign Encounter inherits lane
   W's rule.
9. *(Round 1.)* The gun crew now carries its gun, but the knowledge-only AI advances gun crews, and lane N's guns fire only
   from stationary crews. So in an AI-vs-AI sandbox battle the guns never fire (MEASURED: 0 shots; with the crews holding, 12
   shots and 58 of 60 archers killed). One gun is fielded per person; no crew-per-gun rule is applied.
10. *(Round 1, observed, not reported, not changed.)* The sandbox hands out its live `Ledger` and `DeploymentPhase`, which accept
    writes outside the session log.
    *Superseded (verification round 2, D-R2-06): both, and the battle result, are now copies (MEASURED before the fix: the
    replay diverged in all three cases).*
11. *(Round 1.)* Only the after-battle half of the support morale rule is implemented; a stand-in battle can raise morale.
    *Superseded (verification round 2, D-R2-01): the after-battle half raised morale too, even for a battle started at the
    army's own morale (the in-battle recovery); since round 2 a battle changes the campaign morale by its net loss only and never
    raises it, whatever its start. What remains unchecked is that `MoraleStart` is the battle's real start (M7.15).*

---

## 10. Proposals for the Director (nothing here is decided)

See §4 for the four undecided points. Proposals for review (delegated work, M7.6, M7.9, M7.14, M7.18, A-38):
- P-D1 the crewed tube as the mortar counted object; P-D2 CAP-1..CAP-4 options as listed (TUNE in M7.6, attributed golden
  moves; ships need the band set extended); P-D3 no performance cap for CAP-5 families until a measurement shows the need;
- P-D4 support allocation per War Pulse with the delivery rule pinned here; P-D5 saturating stacking u(c) = c ÷ (c + c₀);
  P-D6 the support cost terms through lane E's settlement; P-D7 the morale coupling campaign ↔ battle;
- P-D8 the sandbox-scoped ledger as the D15 accounting form; P-D9 default objectives at deployment close; P-D10 the two
  thirds-violating scenarios offered as authored (Battle Test only, labelled) and as a DIR5-legal variant;
- P-D11 A-38's mechanism: a recurring per-soldier maintenance in produced goods and money per War Pulse, sized by equipment;
  P-D12 A-38 to decide what the support test reads (grain only today); P-D13 recalibrate against research-driven worlds and
  regenerate `scripts/research-calibration/population-trace-seed42.csv` from HEAD (stale by −2.8 % to +8.6 %).
  *Superseded (verification round 1, D-R1-11): stale by −4.62 % to +13.69 % over all 1,630 turns (MEASURED); the narrower
  range came from ten sampled turns.*
- *(Round 1.)* P-D14 one counted-object rule for the soldier classes, chosen once for lanes W and D in M7.6 (READING C1; lane D
  proposes persons). P-D15 a read-only inspection surface for the sandbox (copies or views of the ledger and the deployment
  phase). P-D16 an AI that holds crew-served guns (lane N's land guns fire only when stationary); this belongs to the AI lanes.
  *(Round 2: P-D15 is implemented in the prototype as copies of the ledger, the deployment phase and the result, D-R2-06.)*

---

## 11. What ran, what was NOT RUN, what was deferred

**RAN (MEASURED):**
- Baseline before any change: M7Proto Debug and Release 1151/0/0; M7Econ Debug and Release 101/0/0 (19:08–19:14Z).
- Per step: filtered lane D runs; the full M7Proto suite in Debug at 19:52Z (1200/0/0, before the prisoners test was added).
- Closing: §6 table (both touched solutions, Debug and Release, forced rebuild), the determinism scan, the doc-link check.
- Mutants: 19 (§7), bounded, three survivors closed by aimed tests in round 2; none hung.
- Measurements (§8): `Sim.Cli` built in Release into the scratchpad (0 warnings, 0 errors; read-only use) and run for four
  seeds; the research-driver probe (four seeds, 1,630 turns); the order-less probe with real twins (six seeds); the seed-42
  diagnostic; lane F's fixture and comparison regeneration on HEAD.
- Originality check (D-069): no other game is named in any file this lane changed (grep over the diff's file list).

**NOT RUN:** the repository's `scripts/validate.sh` tiers (no `Sim.*` file changed, so no Sim tier applies; the prototype
solutions are outside it); `M7DetMath.slnx` (not touched); the G2 graphics prototype (not this lane's); `Proto.M7.Bench` (no
benchmark was needed: no hot-path change claimed); a Windows run (no Windows host).

**Deferred:** the calibration itself (A-38, M7.14, after Q-S1 and Q3); the data changes of CAP-1..CAP-4 (M7.6); the sniper
and mortar identities (an authorized packet); the sandbox inspection user interface, Battle Test's multi-seed runner and naval
and air forces in the sandbox (M7.18); regenerating the calibration trace CSV (not in this lane's scope).

---

## 12. Corrections to this lane's own records

- Commit `b6f3b62`'s message says the lane E hook adds "three accessors and SetMoraleAfterBattle"; it adds **four** read
  accessors (`MenAccountOf`, `CivilianAccountOf`, `SupportOf`, `MenLatchedOf`) and `SetMoraleAfterBattle` (measured in the
  file). History is not rewritten; this note is the correction.

---

## 13. Verification round 1 (fix round, 2026-11-09)

Base: `lane/D` at `6433c40`, in this lane's clone `/home/user/m7w/D`. Input: the verifier's 18 findings (verify-D-r1). Rule
applied: a finding is acted on only after it is reproduced in this clone; a refuted finding gets no change. All runs used
`nice -n 19`, `-m:1`, `-nodeReuse:false`, `DOTNET_CLI_USE_MSBUILD_SERVER=0`, with the solution or project named. Raw outputs
are in `/tmp/claude-0/-home-user-civDemo/54b82655-610f-500e-a185-8fae64bdd108/scratchpad/m7c/D-fix1/`: `stepA.log`,
`stepC.log`, `mutants/`, `probes/`, `trace/` and `summary.txt`.

**How the findings were reproduced.**
- *Mutants.* The verifier's mutants V1–V13 were re-declared with their exact strings in this round's own script
  (`D-fix1/mutants.py`; the verifier's clone was not touched). Each was run on this clone at `6433c40`: the LaneD filter
  first, then the FULL M7Proto suite for each survivor.
- *Probes.* The verifier's probe file was copied into this clone with only its output path changed, and was run. Its outputs
  are byte-identical to the verifier's (P1–P7, W). Its witnesses were run against V1 and V6–V10. The file was deleted before
  the commit.
- *Trace.* A Release `Sim.Cli` was built from this clone (`Sim.*` unchanged since `45c3313`) and the seed-42 trace was run.
- *Documents.* Checked with `sed` and `grep`.

**Bounds (ADR-015 §7.1).** Each mutant run is bounded at 10 × the measured clean run of the same kind, with the build and the
test TOGETHER inside the bound. This closes D-R1-18 for this round's runs. The lane's round-0 script in
`…/m7c/D-work/` was written by another run, so it is not edited (deletion rule). Clean runs, with a forced rebuild of
`Proto.M7`:

| Tree | LaneD filter (bound) | Full suite (bound) |
|---|---|---|
| `6433c40` | 8.3 s (83 s) | 46.8 s (468 s) |
| round-1 fixes (`0815a18`) | 11.1 s (111 s) | 53.5 s (535 s) |

No mutant hung.

### 13.1 Per-finding verdicts

| ID | Sev. | Verdict | Reproduction (this round, MEASURED unless marked) | Fix | Semantic test; mutant after the fix |
|---|---|---|---|---|---|
| D-R1-01 | major | **REPRODUCED** | V9 survived LaneD (8.2 s) and full (49.1 s, 1201 passed); V10 survived (8.4 s / 58.3 s). Verifier witness W9_W10 passes on the clean tree and kills both (5.7 / 6.0 s) | tests only: per-formation accounting against `BattleResult`, by hand, 3 seeds; per-formation checks added to the TacticalResolver prisoners test | `SandboxAccounting_MatchesTheBattleResult_PerFormation`: V9 KILLED 10.6 s, V10 KILLED 10.3 s |
| D-R1-02 | major | **REPRODUCED** | V2, V3, V4, V5 each survived LaneD (8.0–8.3 s) and full (47.4–52.6 s) | tests only: the march extras, the patients' ration (with lane E's integrator carry) and the staff as carriers, each recomputed by hand | `SupportCosts_PatientsEatTheBaseRation_Exactly` (V2 KILLED 10.5 s); `SupportCosts_MarchExtrasOfStaffAndAnimals_Exactly` (V3 10.4 s, V4 10.6 s); `SupportCosts_StaffCarryRationsInTheTrainCapacity_Exactly` (V5 10.9 s) |
| D-R1-03 | major | **REPRODUCED** | probe P1 identical: `DirectorCaps` refuses 3,000 heavy infantry (counted 3,000, cap 50); the encounter fields them as 1 formation of 50 entities; Quick Battle splits them into 60 | documents: READING C1 with both alternatives, their bases and consequences; the sandbox §4 claim corrected; flagged for confirmation. Code unchanged (the rule belongs to M7.6, for lanes W and D together) | `CampaignEncounter_FieldsLaneWsFormationsByTheEntityRule_ReadingC1b` pins the stand-in's current behaviour |
| D-R1-04 | minor | **REPRODUCED** | V7 survived (8.1 / 50.3 s), V8 survived (7.9 / 52.3 s); witness W7 kills both (6.1 / 6.1 s) | test expectation recomputed by hand; a literal-number test (loss 400, saved 80, restore 40, 640,000) | `Morale_MedicalRestore_ByHand_TenDeadOfFifty` and the corrected morale test: V7 KILLED 10.9 s, V8 KILLED 10.8 s |
| D-R1-05 | minor | **REPRODUCED** | V1 survived (12.1 / 50.4 s); witness W1 kills it (5.7 s) | the 4,000-edit sequence now asserts depots + Σ formation persons = opening after every edit; a Disband test | `Disband_ReturnsEveryPersonToTheDepot_AndTheRosterAccountsForEveryone` and `AnyEditSequence_…`: V1 KILLED 12.3 s |
| D-R1-06 | minor | **REPRODUCED** | V6 survived (8.2 / 50.8 s); witness W6 kills it (6.0 s) | test: the captured sink equals the battle's captured (80); the civilians rise by the deserters (48) at the battle instant | `SupportBattle_SendsPrisonersToTheCapturedSink_AndDesertersHome`: V6 KILLED 10.6 s |
| D-R1-07 | minor | **REPRODUCED** | V12 survived (8.4 / 60.7 s), V13 survived (8.3 / 49.9 s); probe P4 identical (Wounded = Captured = 0 in every row) | populated tables with distinct values (the TacticalResolver prisoners battle plus an AutoResolver battle; a second army and a second battle in the support run); `RowFieldCoverage` asserts that some row tells every same-typed field pair apart | both codec tests: V12 KILLED 10.5 s, V13 KILLED 10.4 s |
| D-R1-08 | minor | **REPRODUCED** | probe P5 identical (fixed-wing 20: 40 persons count 2 airframes, cap 1); the CAP-3 table lists 4 rows; family 23 unclassed; option (B)'s rows uncited | documents: the full class membership with lines (30 rows), corrected per-class values (tanks 12, helicopters 7, ships 12, fixed-wing 40), option (A') per identity, option (B)'s rows | `Cap3_OptionA_PerClassCrewValues_MinimalValuesKeepEveryMachineRowWithinItsCap` (values computed independently; each value minus one refuses a row) |
| D-R1-09 | minor | **REPRODUCED** | probe P2 identical (no weapons, Ranged 0; archers killed 0) | code: the catalogue entry of 4992 carries lane N's `FieldGun`, copied into each formation; documents: the fallback, and the AI limitation (MEASURED: 0 shots under the knowledge-only AI, 12 shots and 58 archers killed when the crews hold) | `GunCrew_IsFieldedWithItsFieldGun_AndFires`: F1 (weapon not attached) KILLED 9.9 s; F6 (shared weapon arrays) KILLED 9.6 s |
| D-R1-10 | minor | **REPRODUCED** | probe P3 identical (encounter cell Forest after a refused edit; the replay's cell Open) | code: `Terrain` returns a copy; `DeployedSetup` returns a detached copy (grid and arrays); the deployment phase works on a copy of the grid; `Formations` returns detached specs | `Terrain_IsHandedOutAsACopy_SoTheLockAndTheReplayHold`: F2 KILLED 10.5 s, F3 KILLED 10.6 s, F5 KILLED 10.4 s |
| D-R1-11 | minor | **REPRODUCED** | Release `Sim.Cli`, seed 42, 1,630 turns, 55 s: the trace is byte-identical to the lane's and the verifier's (SHA-256 `4a2aa8c5…ba1a`). Against the committed CSV: min −4.62 % (turn 370, 8,406,671 vs 8,813,574), max +13.69 % (turn 1061, 73,059,072 vs 64,259,400); 72 turns above +8.63 %, 84 below −2.76 %, none equal | documents: the economic-scale proposal §1, §8 and P-D13 here corrected in place | — (documents) |
| D-R1-12 | minor | **REPRODUCED** | `war-sandbox-proposal.md:135` labels the fallback "(DD)"; DR30 (`d079:75`) says only "AI-vs-AI sandbox battles use AutoResolver" | documents: READING A with both alternatives, in the sandbox §6 and §8 and in §3 and §4 here | — (documents) |
| D-R1-13 | minor | **REPRODUCED** | `grep -i formation` finds no allocation target in the support proposal; §2.1 allocates to armies | documents: the deviation stated, with its three reasons and the per-formation alternative | — (documents) |
| D-R1-14 | minor | **REPRODUCED** | probe P6 identical (20 formations, 16 anchors off the grid; close refused, lane TR code 3) | code: the parts are laid out column by column inside the field, and the whole force is refused (nothing minted) when a part would leave it. After the fix the same probe gives 0 off the grid, and the deployment opens and closes | `ForceCreation_LargeForce_IsLaidOutInsideTheField_AndDeploys`: F4a (the old placement) KILLED 10.2 s, F4b (first anchor only) KILLED 10.3 s |
| D-R1-15 | nit | **REPRODUCED** | `caps…proposal.md:95–102` recommends 40 "to stay on the existing band set"; D10: "retune personnel to the cap" | documents: CAP-1 PROPOSAL now 50 (the cap); 40 kept as the alternative | — (documents) |
| D-R1-16 | nit | **REPRODUCED** | code: `MoraleStart` is only range-checked (`SupportWorld.cs:128`). Round-1 probe P16: campaign morale 800,000 → 900,000 for a battle ending at 900 of 1,000 | documents: only the after-battle half is implemented; the gap and its owner (M7.15) stated | — (documents; a pinning test would pin a flaw) |
| D-R1-17 | nit | **REPRODUCED** | `H-N.md` §10: item 1 is the transports, item 2 the guns | documents: citation corrected | — |
| D-R1-18 | nit | **REPRODUCED** | `D-work/mutants.py`: `build()` calls `run()` with no timeout | this round's script bounds the build and the test together; §7 marked in place | — |

Verdicts: 18 REPRODUCED, 0 REFUTED. Each mutant kill above is a failure of the named semantic test. The properties are ones
the system ought to have (ADR-015 §7.2):
- the sandbox's casualty rows are the battle's result;
- every documented cost term is charged;
- the restore follows its formula;
- prisoners are not deserters;
- a disbanded formation's people go home;
- a codec writes each field in its own slot;
- a gun crew has its gun;
- locked terrain stays locked;
- an accepted force is inside the field.

### 13.2 Tests (MEASURED)

| Solution | Config | Before (`6433c40`, 21:11–21:12Z) | After (final tree, closing run 21:48–21:50Z) | Build |
|---|---|---|---|---|
| M7Proto.slnx | Debug | 1201 / 0 / 0 (test step 44 s) | **1215 / 0 / 0** (48 s) | 0 warnings, 0 errors, forced rebuild |
| M7Proto.slnx | Release | 1201 / 0 / 0 (22 s) | **1215 / 0 / 0** (22 s) | 0 warnings, 0 errors, forced rebuild |

The 14 new cases (lane D: 50 → 64; caps 26, support 20, sandbox 18):
- caps: `Disband_…`, `Cap3_…`;
- support: `SupportCosts_MarchExtras…`, `…PatientsEatTheBaseRation…`, `…StaffCarryRations…`, `SupportBattle_SendsPrisoners…`,
  `Morale_MedicalRestore_ByHand…`;
- sandbox: `SandboxAccounting_MatchesTheBattleResult_PerFormation` (×3), `CampaignEncounter_…ReadingC1b`, `Terrain_…`,
  `GunCrew_…`, `ForceCreation_LargeForce_…`.

Tests strengthened in place: `AnyEditSequence_…`, `Morale_ACampaignArmy…`, `SandboxAccounting_PrisonersAndDead…`, both
codec tests, and `PopulatedRun`, which `Replay_FromTheOrderLog_…` also uses. **Moved pins: none.** Every test outside lane
D's own test files is unchanged and passes. The verifier's probe file and this round's two probe classes were temporary and are
not in the tree. The mutant runs of 13.1 used the tree of commit `0815a18`. The only later code edit is test-only, in `Cap3_…`
(a dictionary replaced by a tuple array), and it does not affect any mutant's verdict. The closing runs of 13.2 used the final
tree.

**Determinism scan:** `scripts/check-proto-determinism.sh` scanned 102 files under `Proto.M7/` and found nothing (CLEAN).

**Doc links:** 461 files under `--all` and this report by name, 0 broken links (13.4).

### 13.3 What changed

- **Code.** `Proto.M7/Sandbox/SandboxTypes.cs`: `DesignedFamily.Weapons`, and 4992 carries `FieldGun`.
  `Proto.M7/Sandbox/SandboxSession.cs`: copies on the inspection surface; `SplitAnchors`; weapons copied into each
  formation; the deployment phase gets a grid copy.
- **Tests.** `CapsRosterTests.cs`, `SupportCapabilityTests.cs`, `SandboxTests.cs`; new `RowFieldCoverage.cs`.
- **Documents.** The four proposals and this report, with superseded statements marked in place.
- **Not changed.** Nothing in `Sim.*`, unit data or caps; nothing in `Proto.M7.Econ` or its tests; no other lane's file.

### 13.4 What ran, what was NOT RUN, what was deferred

**RAN:**
- the baselines and closing runs of 13.2;
- 13 verifier mutants on `6433c40` (12 survived the full suite; V11 was killed);
- 6 witness runs;
- 20 mutants on the fixed tree (13 verifier + 7 round-1), all KILLED;
  *(Superseded in part by verification round 2, D-R2-02 and D-R2-03: "all KILLED" held for that set only. Round 2's verifier
  found 11 more non-equivalent mutants of documented mechanisms that survived the full suite, reproduced in §14.)*
- probes P1–P7, W, P9 (gun fire, 4 configurations) and P16 (morale raise);
- the `Sim.Cli` Release build and the seed-42 trace;
- the determinism scan;
- `python3 scripts/check-doc-links.py --all` (461 files, 0 broken links) and the same script on this report by name (1 file,
  0 broken links), re-run after this report's last edit.

**NOT RUN:**
- `M7Econ.slnx`: not touched in round 1 (`git diff 6433c40 -- Proto.M7.Econ Proto.M7.Econ.Tests` is empty). Its round-0
  counts (110 / 0 / 0, Debug and Release) are NOT re-measured here.
- `M7DetMath.slnx`, the G2 graphics prototype, `Proto.M7.Bench` runs (no hot path claimed), `scripts/validate.sh` (no
  `Sim.*` change) and a Windows run.

**Deferred:**
- READING C1's reconciliation of lanes W and D, to M7.6;
- an AI that holds crew-served guns, to the AI lanes (P-D16);
- a read-only sandbox inspection surface (P-D15); *(done in round 2 for the ledger, the deployment phase and the result, §14)*
- enforcing the battle's start morale, to M7.15's hand-off; *(round 2: the remedy for morale rises was in the after-battle
  rule, §14; what M7.15 must still guarantee is that `MoraleStart` is the battle's real start)*
- regenerating the calibration CSV, which is outside this lane's scope.

### 13.5 Observations not in the findings (not changed)

- The sandbox still hands out its live `Ledger` and `DeploymentPhase`. Both accept writes outside the session log
  (limitation 10).
  *(Superseded: verification round 2 measured it, D-R2-06, and fixed it; §14.)*
- The knowledge-only AI advances gun crews, so their guns never fire in an AI-vs-AI battle (limitation 9). This is measured,
  not pinned.
- The lane's internal mutant rounds in §7 are called "round 1" and "round 2". They are not verification rounds.

---

## 14. Verification round 2 (fix round, 2026-11-09)

Base: `lane/D` at `dcb6170`, in this lane's clone `/home/user/m7w/D`. Input: the verifier's 6 findings (verify-D-r2). Rule
applied: a finding is acted on only after it is reproduced in this clone; a refuted finding gets no change. All runs used
`nice -n 19`, `-m:1`, `-nodeReuse:false`, `DOTNET_CLI_USE_MSBUILD_SERVER=0`, with the solution or project named. Raw outputs
are in `/tmp/claude-0/-home-user-civDemo/54b82655-610f-500e-a185-8fae64bdd108/scratchpad/m7c/D-fix2/`: `logs/` (builds,
tests, twin runs), `probes/`, `mut/` (clean bounds and the `results-*.jsonl` / `kills-*.jsonl` records), `probe25/` (the
in-tree probe's research twins), `harness/`, and the scripts `mutants.py` (with `mutants_fix.json`), `proto-suite.sh` and
`base-econ.sh`. Commits of this round: `ed49759` (code and tests), `7b2a56c` (documents and one test case), and the commit
that adds this section.

**How the findings were reproduced.**
- *Mutants.* The verifier's S1–S10, C1, C2 and its controls K1, K2 were re-declared with their exact strings in this round's own
  runner (each string asserted unique; the verifier's clone and files were not touched) and run on `dcb6170`: the LaneD filter
  first, then the FULL M7Proto suite for each survivor.
- *Witnesses and probes.* The verifier's witness file was copied into this clone with only its output path changed. Its probes
  P_M, P_D and P_N gave outputs byte-identical to the verifier's (`diff`); its witnesses passed on the clean tree (9 of 9) and
  each failed under its mutants. This round's own probe file measured what the findings name without measuring: the sandbox's
  live ledger, the battle result of the same class, and the A field of an allocation order. Both files were deleted before any
  commit.
- *Twins.* A scratchpad harness (read-only use of the clone's `Proto.M7.Econ` and Sim.Core, Release) stepped the lane's own
  `ScaleProbe.DriveResearch` and called lane F's `Calibration.MaxRecruitment` (horizon 1) at turns 25 and 200, seeds 42, 1, 2
  and 7. After the fix the probe itself (`scale-probe --seeds 42,1,2,7 --driver research --turns 25 --twin-at 25`) gave the
  same turn-25 twins.

**Bounds (ADR-015 §7.1).** Every mutant run is bounded at 10 × the measured clean run of the same kind, build and test together:

| Tree | LaneD filter (bound) | Full M7Proto suite (bound) | Other filter (bound) |
|---|---|---|---|
| `dcb6170` | 10.3 s (103 s) | 55.3 s (553 s) | the verifier's witnesses: 5.9 s (59 s) |
| round-2 fixes (`ed49759` with `7b2a56c`'s test case) | 13.5 s (135 s) | 67.0 s (670 s) | `ScaleProbeTests.ResearchTwin…` (M7Econ): 23.8 s (238 s) |

No mutant hung.

### 14.1 Per-finding verdicts

| ID | Sev. | Verdict | Reproduction (this round, MEASURED) | Fix | Semantic test; mutants after the fix |
|---|---|---|---|---|---|
| D-R2-01 | major | **REPRODUCED** | P_M byte-identical to the verifier's: archers holding at 800 against 5–20 heavy infantry lose nobody, kill 3–10 and end at 880; the campaign morale goes 800,000 → 880,000 in 24 of 24 runs (under the AI: 800,000 → 800,000, 24 of 24). Source: `TacticalBattle.cs:1001–1004` (recovery capped at MoraleMax, not at the start); `SupportWorld.cs:285–289` (the set rule) | code: `SupportMath.MoraleAfterBattle` (net loss start − end less the restore, never below zero; floored at 0), used by `SupportWorld.OnBattle`. Documents: support §3.3 (rule, D-R1-16 note, READING M (a)), §7 limitation 4, §8; this report §0, §2, §3, §9 limitation 11, §13.4 | `Morale_ABattleNeverRaisesCampaignMorale_ArchersHoldingWithoutLossEndAboveTheirStart` (real TacticalResolver battles, 2 seeds, 0 and 100 staff) and `Morale_ABattleLowersCampaignMoraleByItsNetLossOnly` (6 stand-in cases, by hand). F1a (the round-1 rule) KILLED 11.6 s by 6 cases, both real battles among them; F1b (a gain carried) 11.3 s; F1c (clamped at the campaign morale, start ignored) 11.5 s; F1d (restore ignored) 11.4 s; F1e (no floor) 11.9 s |
| D-R2-02 | major | **REPRODUCED** | S1–S8 and S10 survived the LaneD filter (10.4–11.6 s) and the full suite (44.4–59.1 s, 1215 passed); S9 survived too (11.1 / 62.1 s) and is EQUIVALENT (below). The verifier's witnesses kill each (5.8–6.0 s, bound 59 s). No test referenced `NavalForce` or `ArmyDisbanded` (`grep`) | tests only (8 cases, 6 methods). Documents: support §2.3, §3.1, §4, §5 notes; this report §0, §2, §13.4 | KILLED on the fixed tree, each by its own test: S1 `Medical_CoverageIsTheStaffGranted_NotTheTarget_AndTheSaveFollowsIt` 11.7 s; S2, S3 `SupplyTrain_AWagonIsGrantedOnlyWithItsTwoDraftAnimalsAndItsDriver` 11.9, 11.4 s; S4, S5 `Medical_CareSettlements_AreExactOverTheirElapsedTime_AndCarryTheThousandths` 11.2, 11.0 s; S6, S7 `Allocation_RefusedForANavalForce_AndForADisbandedArmy` 11.4, 10.9 s; S8 `Disbandment_SendsTheWoundedHome_TheDeadAccountHoldsOnlyTheDead` 11.0 s; S10 `SupportCosts_StaffArePaidInKind_WhereThePolityHasNoCoinage` 11.1 s. S9 survives the full suite (12.3 / 67.1 s, 1235 passed): equivalent |
| D-R2-03 | minor | **REPRODUCED** | C1 survived (10.7 / 44.0 s), C2 survived (10.5 / 49.9 s); the verifier's witness kills both (5.7, 5.9 s) | tests only. Documents: caps proposal §6 | `ReinforceToCaps_StopsAtEveryCapAndAtTheEmptyDepot_ReturningWhatMoved`: C1 KILLED 11.4 s, C2 KILLED 11.1 s |
| D-R2-04 | minor | **REPRODUCED**, extended | P_N byte-identical: no raw B produced NegativeTarget (−5 and −1 → UnknownKind), and 7 of 14 raw values threw `OverflowException` at `SupportWorld.cs:165`. Same class, measured while reproducing: A = 2^47, −2^47 − 65,536, `long.MaxValue` and `long.MinValue` threw `OverflowException` in `SupportKinds.Unpack` (`SupportTypes.cs:44`); A = 2^47 − 1 did not (DERIVED: every A ≥ 2^47 or ≤ −2^47 − 65,536 throws) | code: `OnAllocate` total over every (A, B): A outside 0..2^47 − 1 (`SupportKinds.MaxPacked`) → UnknownArmy, polity and army −1; B < 0 → NegativeTarget, checked before the kind (kind byte 0, B as the target); the kind byte only when it fits. Documents: support §2.3 (a new table under the old one) | `AllocationOrders_AnyRawValue_IsRefusedAndRecorded_TheWorldRunsOn` (16 raw orders at one window, every row checked field by field, then a valid order latched): F4a (unguarded kind cast) KILLED 11.3 s; F4b (NegativeTarget check removed) 11.2 s; F4c (A unguarded) 11.4 s; F4d (a negative target recorded mod 2^32) 11.1 s |
| D-R2-05 | minor | **REPRODUCED** | harness on research worlds at turn 25 (Release), formula / twin: 3,320 / 3,080 (seed 42, stone), 3,540 / 3,540 (1, adults), 3,520 / 3,000 (2, stone), 3,380 / 2,260 (7, stone), identical to the verifier's. Cause: `ScaleProbe.Run` ran the twin only for the order-less driver | code: the twin runs for either driver (`ScaleProbe.cs`); the probe then gives the same four twins (`probe25/twins-research.csv`). Documents: economic-scale §2 row 25 research and two notes, §3, §5 L4, §7; this report §2, §5, §9 limitation 5 | `ResearchTwin_Runs_AndStoneBindsBelowTheFormula_Seed7Turn25` (M7Econ): F5 (twin skipped again for the research driver) KILLED 19.0 s (bound 238 s). Also measured: research twins at turn 200 accept 134,840 / 150,260 / 147,920 / 139,600 (formula 134,840 / 150,240 / 147,920 / 139,600), every issued order accepted |
| D-R2-06 | minor | **REPRODUCED**, extended | P_D byte-identical (unlogged `Propose` accepted, 0 log rows; deployed anchor (50000, 90000) in the session and (170000, 330000) in the replay; both hashes unequal). This round: an unlogged ledger transfer of 100 into a formation made it fight with 140 in the session and 40 in the replay (session hash ≠ replay hash). Same class: a write into `Result.Formations` changed the session hash, which then differed from the replay's | code: `Ledger`, `Deployment` and `Result` return copies (the phase rebuilt edit for edit on a detached copy of its base setup and closed where the session closed it). Documents: sandbox §5, §6, §7 item 8; this report §2, §9 limitation 10, §10, §13.4, §13.5 | `InspectionSurface_HandsOutCopies_OfTheLedgerTheDeploymentPhaseAndTheResult` (the P_D scenario, ending in replay equality) and `Deployment_IsHandedOutAsTheSessionsPhase_EditForEdit`: F6a (live ledger) KILLED 11.3 s; F6b (live phase) 10.9 s; F6c (live result) 11.0 s; F6d (copy closed only at the end) 10.4 s |

Verdicts: 6 REPRODUCED, 0 REFUTED. Controls: K1 and K2 KILLED on `dcb6170` (10.5, 10.6 s); K2 again on the fixed tree (10.7 s;
K1's line no longer exists). Each kill above is a failure of the named semantic test, and each property is one the system ought
to have (ADR-015 §7.2):
- combat never raises campaign morale (DR29's quoted text);
- the staff granted, not the staff requested, set the medical effect;
- a wagon goes with its two animals and its driver;
- the care follows the elapsed time and keeps its fractions;
- a naval or a disbanded army gets no support;
- a disbanded army's wounded are not dead;
- staff without coinage are paid in kind;
- reinforcement stops at the cap and at the empty depot;
- a malformed replayed order is refused, not fatal;
- a MEASURED row reports a measurement;
- only logged edits reach a sandbox session.

**S9 is EQUIVALENT, not a gap.** It sends a disbanded army's recovered men to its closed men account instead of home, in a
branch of `SettleCare` that cannot run: lane E disbands an army only in its pulse-end handler (`WarEconomyWorld.cs:967`, the
only call of `Disband`); `SupportWorld.Execute` settles the war's care at that same instant BEFORE lane E's handler, so
`ReturnAll`'s settlement sees dt = 0 and returns; a returned army is never latched, and a disbanded army fights no battle. The
branch is defensive and was not changed.

### 14.2 Tests (MEASURED)

| Solution | Config | Before (`dcb6170`) | After (final tree) | Build (after, forced rebuild) |
|---|---|---|---|---|
| M7Proto.slnx | Debug | 1215 / 0 / 0 (test step 50 s) | **1235 / 0 / 0** (59 s) | 0 warnings, 0 errors |
| M7Proto.slnx | Release | 1215 / 0 / 0 (18 s) | **1235 / 0 / 0** (33 s) | 0 warnings, 0 errors |
| M7Econ.slnx | Debug | 110 / 0 / 0 (113 s) | **111 / 0 / 0** (109 s) | 0 warnings, 0 errors |
| M7Econ.slnx | Release | 110 / 0 / 0 (118 s) | **111 / 0 / 0** (108 s) | 0 warnings, 0 errors |

The M7Econ "before" counts were measured on `dcb6170`'s files extracted with `git archive` into the scratchpad (`base-dcb6170/`:
the root `Directory.Build.props`, `Sim.Core`, `Sim.Data` and the prototype's economy projects), because the clone had moved on;
round 1 did not re-measure M7Econ (§13.4). The "before" builds had 0 warnings and 0 errors too. Times are wall times on a shared
4-CPU host; the closing Release M7Proto run overlapped the M7Econ before-run. In both the before and the after logs a Release build
of `M7Econ.slnx` compiles `Sim.Core` and `Sim.Data` (project references outside the solution) in Debug; not changed.

The 21 new cases:
- support (17): `Morale_ABattleNeverRaisesCampaignMorale_…` (2), `Morale_ABattleLowersCampaignMoraleByItsNetLossOnly` (6),
  `Medical_CoverageIsTheStaffGranted_…`, `SupplyTrain_AWagonIsGrantedOnly…` (3), `Medical_CareSettlements_…`,
  `Allocation_RefusedForANavalForce_…`, `Disbandment_SendsTheWoundedHome_…`, `SupportCosts_StaffArePaidInKind_…`,
  `AllocationOrders_AnyRawValue_…`;
- caps (1): `ReinforceToCaps_StopsAtEveryCapAndAtTheEmptyDepot_ReturningWhatMoved`;
- sandbox (2): `InspectionSurface_HandsOutCopies_…`, `Deployment_IsHandedOutAsTheSessionsPhase_EditForEdit`;
- economy probe (1): `ResearchTwin_Runs_AndStoneBindsBelowTheFormula_Seed7Turn25`.

**Moved pins: none.** Two documentation comments in lane D's own tests changed (`Morale_ACampaignArmyLeaves…`, whose
assertions are unchanged and pass under the new rule, and a round-1 comment in `SandboxTests` that called the phase "live").
Every test outside lane D's own test files is unchanged and passes.

**Determinism scan:** `scripts/check-proto-determinism.sh` scanned 102 files under `Proto.M7/` and found nothing (CLEAN). It
does not cover `Proto.M7.Econ`, as before; this round's change there removes one condition.

**Doc links:** `python3 scripts/check-doc-links.py --all`: 461 files checked, 0 broken links; and, since `--all` does not cover
`prototypes/`, this report by name: 1 file, 0 broken links. Both re-run after this report's last edit.

### 14.3 What changed

- **Code.** `Proto.M7/Support/SupportMath.cs`: `MoraleAfterBattle`. `Proto.M7/Support/SupportWorld.cs`: `OnBattle` uses it;
  `OnAllocate` is total. `Proto.M7/Support/SupportTypes.cs`: `SupportKinds.MaxPacked`; refusal and `SupportBattle`
  documentation. `Proto.M7/Sandbox/SandboxSession.cs`: copies of the ledger, the deployment phase and the result.
  `Proto.M7.Econ/ScaleProbe.cs`: the twin for either driver.
- **Tests.** `SupportCapabilityTests.cs`, `CapsRosterTests.cs`, `SandboxTests.cs`, `ScaleProbeTests.cs` (the 21 cases above).
- **Documents.** The four proposals and this report, with superseded statements marked in place.
- **Not changed.** Nothing in `Sim.*`, unit data or caps; no other lane's file (lane E's `WarEconomyWorld.cs` and lane TR's
  `DeploymentPhase` are untouched this round); no CI file; no permanent project.

### 14.4 What ran, what was NOT RUN, what was deferred

**RAN:**
- the baselines on `dcb6170` (M7Proto Debug and Release; M7Econ Debug and Release on the extracted files) and the closing runs
  of 14.2;
- 14 mutants on `dcb6170` (12 survivors re-measured, 2 controls killed), 11 witness runs of the verifier's witnesses, and 27
  mutants on the fixed tree (the 12 again, 14 fix mutants F1a–F6d and F5, control K2);
- the probes P_M, P_D, P_N (the verifier's) and this round's three (ledger, result, A field);
- the twin harness at turns 25 and 200 (four seeds each) and `scale-probe --driver research --twin-at 25` (four seeds);
- the determinism scan; `python3 scripts/check-doc-links.py --all` and the same script on this report by name.

**NOT RUN:**
- `M7DetMath.slnx`: not touched;
- `Proto.M7.Bench` runs: no hot path claimed (the bench project builds inside M7Proto.slnx);
- `scripts/validate.sh`: no `Sim.*` change;
- the G2 graphics prototype; a Windows run;
- research twins at turns 100, 325 and 400 (those rows of the economic-scale table are still the formula, as now marked);
- the verifier's mutant X1 and witness `W_X1` (the default objective) are outside the six findings: `W_X1` passed on the clean
  tree inside the witness filter; X1 was not run.

**Deferred:**
- READING M (a) or (b): the Director's (§4 item 3, unchanged);
- that `MoraleStart` is the battle's real start morale: the M7.15 hand-off;
- copying the sandbox's three logs (below).

### 14.5 Observations not in the findings (not changed)

- The sandbox's three logs (`Log`, `Casualties`, `BattleOrders`) are handed out as `IReadOnlyList` views of the live lists. They
  have no write member, but a downcast to the list type could write them. The result's personnel report is shared; neither the
  session's state nor its hash uses it.
- In the P_M scenario the knowledge-only AI's archers do not hold, so they neither recover nor lose: under READING M (a) combat
  without own losses costs no morale. Whether taking part alone should cost morale is READING M's alternative (b), still open.
