# Lane A: persistent avatar and victory cinematics (M7 graphics prototype G1)

Status: the lane is done. The solution build is clean (0 warnings, 0 errors). All 44 lane-A tests pass: 42 run without a GPU and 2 render (`[GfxFact]`). Verifier findings are addressed in **Verification** at the end. Every timing below is **SOFTWARE RENDERING (llvmpipe, 4 CPUs), not Victus performance**. All the art is placeholder grade.

## What was built

| File | Contents |
|---|---|
| `Gfx.Avatar/AvatarProfile.cs` | Versioned JSON profile (schema v1, presentation only, outside WorldState). It holds:<ul><li>a GUID id and a display name;</li><li>body: height in mm, build and shoulders in permille;</li><li>**14 face morph weights** (signed permille), plus skin, eye and hair colour, hair style and facial hair;</li><li>heraldry: two colours and a pattern;</li><li>clothing **preferences** for each role (military, civil, royal). No era is stored.</li></ul>Every field is an integer, enum or string. `Hash()` is SHA-256 over canonical little-endian bytes. `IdentityHash()` covers only the physical fields. There is a v0→v1 migration step, validation, `Sample(seed)` fixtures (integer SplitMix64) and `PlayerFixture()`. |
| `Gfx.Avatar/EraDress.cs` | `EraFromCapabilityTier(int)` maps tiers 0–2 to ancient, 3–4 to medieval, 5 to early-modern and 6 and up to modern. The input is a labelled fixture tier and never a date (Law 4). It stands in for an `AgeQuery` index, as in the ADR-033 `UiEra` precedent. `Select(profile, era, role)` is a pure function that returns an `Outfit`: garment (tunic, robe, mail with surcoat, coat, suit or uniform), headgear (bronze helm, laurel, kettle helm, crown, tricorne or peaked cap), cloak, cuirass and colours. |
| `Gfx.Avatar/AvatarMesh.cs` | Parametric skinned mesh on the canonical 20-joint `Gfx.Units.Skeleton`.<ul><li>**Head:** a 24×32 deformed ellipsoid with **linear blend shapes**: `base + Σ wᵢ·Δᵢ`.</li><li>**Parts:** eyes and ears whose size and placement are also blend channels; shells for hair, beard and moustache; vertex colour for lips, brows, age lines and stubble.</li><li>**Body:** tubes and ellipsoids scaled by build and shoulders. Outfit parts are added on top, and there is an optional sword.</li><li>**Layout:** vertices `[0, IdentityVertexEnd)` are the identity. Outfits only recolour clothed regions and append parts.</li></ul> |
| `Gfx.Avatar/AvatarRenderer.cs` | `DynamicMesh` (CPU-updated VertexPNC, drawn through the engine's `InstancedLit`/`InstancedDepth` with one instance). `PosedAvatar` does CPU linear-blend skinning for the two hero characters. |
| `Gfx.Avatar/Demo/AvatarScenes.cs` | Scenes `avatar-turntable`, `avatar-eras` (4 eras × 3 roles, one identity) and `avatar-faces` (12 sample profiles). |
| `Gfx.Cinematics/WarOutcome.cs` | **LABELLED FIXTURE** DTO: kind (9), victor, opponent leader fate (6), domain, era tier, culture ids, war record, weather/time seed. It has a SHA-256 hash and 8 fixtures. |
| `Gfx.Cinematics/CinematicPlanner.cs` | Pure, integer-only `(outcome, seed, settings) → CinematicPlan`. The plan is the ordered shots (beat, camera move, side, framing, duration in ms, clip per character) plus set, hour, weather, banner side, roles, armed flag and notes. Clips are chosen by tag from `ClipCatalog` (19 clips, 11 tags). Rules R1–R8 are written as code (see below). The plan has its own hash and a `Dump()`. |
| `Gfx.Cinematics/CineClips.cs` | Clip library written as code on the same skeleton: stand ×2, walk-in, escort-walk, bound-walk, led-captive, kneel-one, kneel-both, kneel-fall, handshake, clasp-bow, strike-overhead, strike-side, victory-raise-arm, victory-raise-both, bow-deep, bow-head and standoff ×2. |
| `Gfx.Cinematics/CinematicPlayer.cs` | Evaluates a plan at time t on the CPU, deterministically:<ul><li>the active shot;</li><li>staging of both characters around two marks, with spacing set by the outcome (walk-in path, the captive led away);</li><li>banner raise;</li><li>camera choreography for each (beat, move): crane, dolly, orbit, two-shot push, low angle, over-shoulder or static. These are Catmull-Rom/ease based, using `Gfx.Engine.Cameras`.</li></ul>It also maps weather to fog and exposure. |
| `Gfx.Cinematics/CineSets.cs` | Procedural sets from an integer hash:<ul><li>aftermath field: debris, shields in both sides' colours, a tree line;</li><li>fallen fortress: a breached wall, broken towers, rubble;</li><li>naval dock: water, ships, a wreck, flotsam.</li></ul> |
| `Gfx.Cinematics/CardText.cs` | `CineUnlit` effect (an `IEffectSource`) and a 5×7 glyph-quad font (no SpriteFont, no content pipeline). |
| `Gfx.Cinematics/Demo/CinematicScenes.cs` | Waving heraldic `BannerMesh` and `CinematicSceneBase`, which draws two avatars, the set, banners and the war-record card. The card reads "FIXTURE DATA - NOT SIMULATION STATE" (ledger §24). Registered scenes: `cine-execution`, `cine-surrender`, `cine-capture`, `cine-peace`, `cine-inconclusive`, `cine-naval`, `cine-siege` and `cine-defeat`. |
| `Gfx.Tests/AvatarTests.cs`, `Gfx.Tests/CinematicTests.cs` | 42 tests. |

Shared files: `M7Gfx.slnx` (+2 projects), `Gfx.Demo.csproj` (+2 ProjectReferences) and `Gfx.Tests.csproj` (+1 ProjectReference). Each change is additive. The engine is unchanged.

## Planner rules (code + tests)

- **R1 Fate governs.** The outcome beat follows the opponent leader's recorded fate. If the kind conflicts with the fate, this is recorded as a note and never acted out.
- **R2 No execution after surrender.** There is never an execution after a surrender (fate Surrendered, or kind Surrender with any fate). If such a record says Executed, the cinematic cuts to the aftermath.
- **R3 Execution needs two things.** It happens only when the fate is Executed **and** `AllowExecutionContent` is on. Otherwise the cinematic cuts to the aftermath.
- **R4 Inconclusive war, or no victor.** The beat is a standoff with no victory clip and no banner beat.
- **R5 Defeat.** The player's avatar yields on the losing side and the opponent's banner is shown. The avatar is never killed, captured or kneeling.
- **R6 Set.** Naval domain gives the naval set and Siege gives the fortress set. The recorded domain wins. A Naval/SiegeVictory kind picks the set only on a Land-domain record.
- **R7 Fixed beat order.** Aftermath → AvatarIntro → [OpponentIntro] → Confrontation → OutcomeBeat → [Banner] → WarRecordCard.
- **R8 Variation.** The seed changes camera moves, sides and framing, the hour, weather, clip variants and durations (±0.5 s). It never changes the beats, the set or the banner.

## Evidence

**Tests** (`dotnet test Gfx.Tests -c Release --filter "AvatarTests|CinematicTests"`): 44 passed, 0 failed, 0 skipped.

Avatar:
- `ProfileRoundTripsBitExactThroughJsonAndFile`: populated profile; identical canonical bytes, JSON and hash.
- `ProfileHashIsPinned`: `925A4FE1…4E15A53D`.
- `HashSeesEveryMorphAndIdentityHashIgnoresClothingPreferences`.
- `V0ProfileMigratesToV1`.
- `ValidationRejectsBadProfiles`.
- `AtLeastTwelveFaceMorphsEachDeformTheHeadDistinctly`: each of the 14 morphs moves the head by more than 1 mm, all 14 deformations differ pairwise, and −w equals −Δ (linearity).
- `EraIsComputedFromTheCapabilityTier` ×7.
- `EraTierRejectsNegativeAndProfileStoresNoEra`.
- `IdentityIsInvariantAcrossEveryEraAndRoleOutfit`: 8 tiers × 3 roles. Identity vertex positions and head colours are bit-identical, and the profile hash is unchanged.
- `OutfitFollowsRolePreference`.
- `MeshIsValidAndCpuSkinningMatchesTheBoneTextureReference`: maximum error below 1e-5 m against `BoneTexture.SkinCpu`.
- `EraLineupRendersFiguresAndNeverWritesTheProfile` (GPU): hashes the same profile instance the scene renders.

Cinematics:
- `TruthTableRulesHoldForEveryCombination`: all 972 combinations (9 kinds × 6 fates × 3 victors × 2 content settings × 3 domains).
- `TruthTableExplicitRows` ×17.
- `ConflictsAndCutsAreRecordedAsNotes`.
- `PlanIsDeterministicPinnedAndNeverWritesTheOutcome`: the execution plan with seed 1 is pinned at `7276C8F2…5563CE332`.
- `SeedsVaryCameraLightWeatherAndClipsButNeverTheBeats`: 32 seeds × 8 fixtures. The seed also varies every shot's duration (±0.5 s, R8); the beat order never changes.
- `StagedCharactersStayAboveTheGroundAndTheExecutedLeaderLiesFallen`: all 8 fixtures, every 0.2 s, both characters skinned exactly as drawn; lowest vertex above −0.04 m; the executed leader at u = 0.92/0.99 lies on the ground (top below 0.75 m).
- `PlannerOutcomeAndProfileSourcesPassTheIntegerOnlyScan`: runs `scripts/check-gfx-determinism.sh` on the planner, outcome and profile sources (string literals blanked).
- `PlayerStagesTheOutcomeFromThePlan`: the leader falls only after the strike, the captive is led away, and inconclusive never raises a banner.
- `WritePlanDumpsForEveryFixture`.
- `RenderingACinematicNeverWritesItsFrozenInputs` (GPU): the outcome, both profiles and the plan hash are the same before and after the render.

**Teeth check:** I applied two mutants and ran the suite on each. Each turned semantic tests red, and I restored both afterwards.
- Disabling R2 failed 3 tests: the truth table, an explicit row and the notes test.
- Zeroing the NoseBridge blend shape failed the morph test.

**Captures** are in `scratchpad/m7gfx/captures/avatar-cinematics/` (84 PNGs at 1280×720, with shadows):
- `avatar-turntable-yaw000…300.png` and `avatar-turntable-face-front/-34.png`.
- `avatar-eras-lineup-{military,civil,royal}.png`: same identity, 4 eras each.
- `avatar-faces-sheet-{34,front}.png`.
- `cine-{execution,surrender,capture,peace,inconclusive,naval,siege,defeat}-NN-<beat>-<pct>.png`: one key frame per shot, and three for the outcome beat.
- `plans/plan-*.txt`: plan dumps for all 8 fixtures, plus `plan-execution-content-off.txt`, which shows the R3 aftermath cut.
- `profile-player.json`: the profile file, written to the scratchpad and not the repo.

**Bench, SOFTWARE RENDERING (llvmpipe), not Victus performance:** `cine-execution` at 1280×720, 60 frames, aftermath into the avatar intro, two CPU-skinned characters plus the set, about 33k triangles counting both passes.

| Shadows | p50 | p95 | p99 |
|---|---|---|---|
| On | 71.8 ms | 111.0 ms | 266.7 ms |
| Off | 53.5 ms | 96.7 ms | 124.7 ms |

## Placeholder and limits

- **Art and motion.** The art is placeholder grade: blocky bodies, faceted heads, shell hair, code-written motion. The production path is authored base heads and bodies, with blend-shape targets driven by these same 14 channels, authored or mocap clips retargeted to `Skeleton`, and this planner and staging layer on top.
- **Skinning.** Hero characters are skinned on the CPU (2 per scene, about 5k vertices each). Crowds use lane U's GPU path.
- **Weather.** Weather is fog density plus exposure only. Rain and snow particles were cut.
- **Cloth on the ground.** Outfit vertices (skirts, robes) are clamped to the ground plane in `PosedAvatar.SkinPlaced` so a rigid pelvis-skinned robe does not pass through the ground when its wearer kneels or falls. Placeholder for cloth simulation; the body is never clamped.
- **Card text.** The war-record card uses a 5×7 bitmap font.
- **Bench coverage.** The bench measures the plan's first shot only, because `--bench` uses `Shots[0]`.
- **Not exercised here.** `--interactive` and real hardware (RTX 4050, Iris Xe) were not run.

## Blockers and Director decisions needed

1. **D-038 C1/C3 supersession for faces.** D-075 S20 covers only the battlefield. A face-bearing avatar used in cinematics, diplomacy and events needs a new, scoped supersession. It was implemented here only as an authorized prototype.
2. **Execution as a sim outcome.** No leader entity or war/peace/surrender state exists in Sim.Core, so everything here runs on a labelled fixture. Execution kills a person, which is a Ledger flow (Law 1) and must be a simulation decision: an order or a rule. It also needs a content-rating owner, since no rating rule exists. The `AllowExecutionContent` switch is a placeholder for that policy.
3. **Who decides "historically appropriate".** The planner currently depicts whatever the record says. A data rule keyed on Age and culture, versus a player order, is open.
4. **Ledger §17.2 reading.** The avatar is classified as presentation-only and not a gameplay object. D-077 should state that explicitly.

## Recommended next step

Have the integration lane play one cinematic on the real battle terrain (replacing `CineSets.Field`), using the battle trace's final state as the aftermath. In parallel, ask the Director for the D-038 face supersession and an execution and content-rating ruling, before any `WarOutcomeRecord` is defined in the simulation (M8 diplomacy and leaders).

## Verification

A verifier returned 1 major and 5 minor findings. Each was reproduced in this tree before any change.

| # | Finding | Verdict | Fix and semantic test |
|---|---|---|---|
| 1 (major) | Executed leader sinks into the ground | **Confirmed and wider.** Opponent min Y −0.551 m at u = 0.92/0.99 and −0.303 m when kneeling, matching the verifier. The same −0.30 m sink hit `kneel-one` (surrender). Three causes: toes pointed down into the ground when kneeling; the fall pivoted at the feet, not the knees; and the robe hem, rigidly skinned to the pelvis, went through the ground. | `CineClips`: kneeling feet now point backward, the root height comes from knee and boot contact probes for the whole transition, and the fall pivots about the knee line with the shins kept flat. `PosedAvatar.SkinPlaced` keeps outfit vertices on the ground (see Placeholder). New test `StagedCharactersStayAboveTheGroundAndTheExecutedLeaderLiesFallen`; it failed on the old clips (listed every sinking frame) and fails if the outfit clamp is removed. Captures `cine-execution-*` and `cine-surrender-*` regenerated: the 92 % frame now shows the whole fallen body. |
| 2 | Lineup invariance test cannot fail | **Confirmed.** The scene built its own fixture. | `AvatarErasScene(AvatarProfile)` constructor (the parameterless one is kept for the registry). The test hashes the instance it passes in. The M6 mutant (`FaceMorphs[0] = 999; HeightMm = 2000` in `Build`) now fails it. |
| 3 | Report claim of 92/93 out of date | **Confirmed.** Full `Gfx.Tests`: 95 passed, 0 failed. | The out-of-lane blocker line was removed. |
| 4 | Integer-only claim unguarded | **Confirmed.** The scan's only hits were 6 false positives inside hex and GUID strings. | `PlannerOutcomeAndProfileSourcesPassTheIntegerOnlyScan` runs the scan on the three sources with string literals blanked. A mutant appending `float Half(float x) => x * 0.5f` to the planner fails it. |
| 5 | Seed also varies durations | **Partly refuted.** The R8 line already said "durations (±0.5 s)". The test bullet did not. | The bullet now mentions durations. |
| 6 | Blotchy lower faces; siege outcome framing | **Recorded, not fixed.** Placeholder-grade visuals, not trivial. | None. |

Mutant runs restored byte-identical (checked with `cmp`). After the fixes: `dotnet build M7Gfx.slnx -c Release` 0 warnings, 0 errors; lane A 44/44; full `Gfx.Tests` 95/95. Capture frame times are SOFTWARE RENDERING (llvmpipe), not Victus performance.

## Dated note — 2026-11-05 (D-079; appended, the text above is unchanged)

Record: `docs/d079-m7-governance-reconciliation-2026-11-05.md` (Director decisions of 2026-11-05, cited DIR5). Each bullet quotes the superseded line, states the extent and what is retained, and cites the replacing DIR5 section and D-079 decision row (DR). Categories: DIRECTOR DECISION, ADMINISTRATIVE CONSEQUENCE, DELEGATED TECHNICAL DECISION, OPEN (needs the Director), READING (labelled interpretation).

Grouped in D-079 §2 as **S490** (5 statements). This is a report, not a ruling: the notes below correct its status against DIR5 and the Phase B audit.

- (`avatar-cinematics.md:97`) — "A face-bearing avatar used in cinematics, diplomacy and events needs a new, scoped supersession." — in full: delivered by D-077 S86/S87 (2026-11-04) and required by DIR5 §5.1 (customizable face) and §6.2 (credible faces) (DIRECTOR DECISION). **Replaced by** DIR5 §5.1, §6.2 (D-079 DR39, DR45). [DIRECTOR DECISION]
- (`avatar-cinematics.md:98`) — "must be a simulation decision: an order or a rule. It also needs a content-rating owner, since no rating rule exists." — in part: a rule is decided - loss of all cities means the leader is executed, authoritatively (§4.1); the 'order' route exists only as the victor's captured-leader decision inside the delegated post-battle framework (§4.2); AllowExecutionContent matches §5.3's 'relevant cinematic content is enabled'. Retained: rating-policy ownership is not addressed by DIR5 (DELEGATED as a presentation setting). **Replaced by** DIR5 §4.1, §4.2, §5.3 (D-079 DR36, DR37, DR41). [DELEGATED TECHNICAL DECISION]
- (`avatar-cinematics.md:99`) — "A data rule keyed on Age and culture, versus a player order, is open." — in part: execution on losing all cities is a fixed rule (§4.1); other leader outcomes are victor choices inside the delegated post-battle framework, whose eligibility must match the actual outcome (§4.2); cinematics depict only the authoritative record (§5.3). Retained: any Age/culture appropriateness layer is DELEGATED design inside §4.2 (flag only if a Director-level choice emerges). **Replaced by** DIR5 §4.1, §4.2, §5.3 (D-079 DR36, DR37, DR41). [DELEGATED TECHNICAL DECISION]
- (`avatar-cinematics.md:100`) — "D-077 should state that explicitly." — in full: DIRECTOR DECISION - the avatar is purely visual, outside authoritative state, hashes and gameplay calculations. **Replaced by** DIR5 §5.1 (D-079 DR39). [DIRECTOR DECISION]
- (`avatar-cinematics.md:104`) — "before any `WarOutcomeRecord` is defined in the simulation (M8 diplomacy and leaders)." — in part: the basic war and leader-fate record (wars, battles, participants, outcomes, casualties, leader fates) is M7 scope; M8 keeps broader political and diplomatic relations, treaties and long-term consequences (§4.3). The requested face and execution rulings are given (§5.1, §4.1, §5.3). **Replaced by** DIR5 §4.3, §4.1, §5.1, §5.3 (D-079 DR36, DR38, DR39, DR41). [DIRECTOR DECISION]
