# Lane U: soldiers and formations (M7 graphics prototype G1)

Status: the lane is done. The solution build is clean (0 warnings, 0 errors). All 11 Units tests pass, and so does the whole `Gfx.Tests` project: 48 passed, 0 failed, 0 skipped. Every number in this report is **SOFTWARE RENDERING (llvmpipe, 4 shared CPUs), not Victus performance**. All the art is placeholder grade.

## What was built (`Gfx.Units/`, a presentation-only library that references only `Gfx.Engine`)

| File | Contents |
|---|---|
| `Skeleton.cs` | Canonical 20-joint humanoid: root (on the ground between the feet), pelvis, spine, chest, neck, head, and left/right clavicle, upper arm, forearm, hand, thigh, shin and foot. Metres, Y up, the soldier faces +Z. Uses the XNA row-vector convention. The skin matrix is `T(−bind)·Model`. |
| `ClipLibrary.cs` | Eight procedural clips written as code: idle, walk, run, attack-melee, fire-ranged, hit-react, death, rout-run. Each is a pure function of phase. 32 frames per clip are baked into the engine `BoneTexture` (RGBA32F, 60×256 texels). `Sample` gives two rows and a blend (looping clips wrap, one-shot clips clamp). `SampleStepped` is the reduced-rate variant used for animation LOD. |
| `SoldierMeshes.cs` | Procedural flat-shaded skinned body: about 0.4k triangles at LOD0 (6-sided limbs) and fewer at LOD1 (4-sided). Up to 2 influences are blended at each joint. The material region (cloth, skin or fixed colour) is stored in the vertex alpha. There are 10 equipment parts as separate rigid meshes, each weighted 100 % to one joint: three helmets (cone, crested, kettle), round and rectangular shields, spear, sword, bow, quiver and musket. There is also a three-box rigid proxy for the far LOD. |
| `SoldierLook.cs` | Per-entity variation derived from `(entityId, kit)` through an integer SplitMix64 hash. It covers helmet variant, height (0.93–1.07), width, skin tone, cloth shade and jitter, and animation phase offset. The fields are integers, so the result is the same on every platform. It is presentation only. |
| `UnitEffects.cs` | The `UnitSkinnedLit` effect, auto-discovered through `IEffectSource`. It is the engine's VTF skinning (the GLSL is copied, so the engine is not modified) plus material regions: cloth colour comes from `i_tint.rgb` and skin tone from `i_tint.a`. The shadow pass reuses the engine's `SkinnedDepth`. |
| `BattleTrace.cs` | The frozen-input fixture model and parser (`m7gfx-battle-trace/1`). It exposes the data read-only and computes a SHA-256 `Hash()` over a canonical little-endian encoding. |
| `FormationPlayback.cs` | Turns 2 s steps into frames. Positions are interpolated linearly. Facing comes from displacement, or from the target while a Fire event is live. The clip is chosen from the trace state and events (see R24 below). |
| `SoldierCrowd.cs` | Instanced renderer: one instanced draw per mesh per LOD, with frustum culling and animation LOD (thresholds below). |
| `Demo/UnitsScenes.cs` | Scenes `units-closeup`, `units-clash`, `units-wide5k`, `units-bench-1k` and `units-bench-15k`, on a flat ground plane. Integration with terrain is lane I's job. |

Shared files I touched, each with one additive line: `M7Gfx.slnx` and the ProjectReferences in `Gfx.Demo.csproj` and `Gfx.Tests.csproj`. Tests are in `Gfx.Tests/UnitsTests.cs`. The engine is unchanged.

**Animation LOD thresholds:**
- **LOD0, under 40 m:** full body, every equipment part, frames blended every frame.
- **LOD1, 40–120 m:** 4-sided body, silhouette parts only (shields, spear, bow, musket), frames snapped to the baked frame and stepped at 10 Hz.
- **LOD2, from 120 m:** a rigid proxy drawn with `InstancedLit`. Dead soldiers are laid flat. This proxy is the slot for impostor cards, which were not built.

The shadow pass reuses the culled set from the camera, so soldiers outside the camera view cast no shadow.

## Battle fixture

`fixtures/battle-trace-combinedarms-s7.json` (473 KB) carries the label *"fixture exported from prototype snapshot 2026-11-04"*.
- **How it was made:** a scratch exporter at `…/scratchpad/m7gfx/work-U/Exporter/` references a scratch copy of `prototypes/m7-sprint1/Proto.M7` (copied, never modified). The copy built cleanly, so the fixture is real exported data, not a synthesized stand-in.
- **What it contains:** `TacticalScenarios.CombinedArms(seed 7)` with `KnowledgeAi` on both sides, run to the end of the battle. That gives 4 formations, 220 entities, 104 snapshots at 2 s steps, and 1,802 executed Fire/Damage events. Positions are integer mm.
- **Pinned hash:** SHA-256 `6733FBA5…DECD2561`.

## Evidence

**Tests** (`Gfx.Tests/UnitsTests.cs`, 11 of 11 passing):

| Test | What it checks |
|---|---|
| `SkeletonIsWellFormedAndRestPoseIsIdentitySkin` | Skeleton structure, the rest pose, and that skin weights sum to 1. |
| `BakedRowsEqualExactPosesAndPackRoundTrips` | Each baked row equals the exact pose bit for bit, and the texture packing round-trips. |
| `BlendedBakeStaysWithinErrorBoundOfExactSkinning` | Bake against the CPU reference (see the error bound below). |
| `SampleWrapsLoopsAndClampsOneShots` | Looping clips wrap and one-shot clips clamp. |
| `VariationIsDeterministicPinnedAndVaried` | One look is pinned exactly; 300 ids give more than 290 distinct looks. |
| `FixtureParsesWithExpectedShapeAndPinnedHash` | Fixture counts, entity id order and the pinned hash. |
| `PlaybackInterpolatesPositionsAndDerivesFacing` | Exact positions at a step, the exact midpoint between steps, and yaw. |
| `ClipSelectionFollowsTraceStatesAndEvents` | Death timing, melee, hit-react only after a Damage event, and positions that do not depend on the clip. |
| `PlaybackNeverWritesTheFixture` | Invariance: the hash and the file bytes are unchanged after a full playback. |
| `InstancedSkinnedSoldiersRenderAndPoseMatters` | Instancing smoke test (GPU, `[GfxFact]`). |
| `ClashSceneRenderLeavesFixtureUnchanged` | Invariance through the GPU render path (`[GfxFact]`). |

**Error bound** (GPU formula, matrix lerp of 2 rows, measured against exact evaluation over all 1.6k vertices of body and equipment at 160 off-frame times per clip):
- 2.8 cm or less for every clip except one.
- fire-ranged reaches 12.2 cm, caused by the 0.16 s bow-string release.
- The test bounds are 3 cm and 13 cm. At 16 frames per clip the error was up to 93 cm, which is why the bake uses 32.

**Teeth check:** I applied two mutants and restored both afterwards.
- Making the shader ignore `i_anim` failed `InstancedSkinned…` ("pose rows did not move vertices").
- Replacing the playback position lerp with `pa` failed both the interpolation test and the clip-selection test.

**Captures**, in `/tmp/claude-0/-home-user-civDemo/54b82655-610f-500e-a185-8fae64bdd108/scratchpad/m7gfx/captures/units/`:
- **Close-up variation:** `units-closeup-kits-clips.png` (4 kits × 8 clips), `units-closeup-variation.png` (a same-kit block showing per-entity variation), `units-closeup-dusk.png`.
- **Formation clash from the fixture:** `units-clash-approach.png`, `units-clash-volley.png` (archers firing), `units-clash-melee.png`, `units-clash-aftermath.png` (dead soldiers lying, routed units gone).
- **Wide, 5,000 soldiers:** `units-wide5k-wide.png`, `units-wide5k-frontline.png`.

## Bench — SOFTWARE RENDERING (llvmpipe), not Victus performance

Settings: 1280×720, HDR target plus ACES, frame time includes waiting for the renderer, captured to `captures/units/bench.txt`. The crowd is **synthetic and labelled as such** (blocks of 250 soldiers per army), and the wide camera is fitted to the whole crowd. That means 15k is mostly the LOD2 proxy, and 1k is mostly LOD1 skinned.

| Soldiers | Shadows | p50 | p95 | Draws | Triangles | Instances drawn |
|---|---|---|---|---|---|---|
| 1,000 | on | 251 ms | 488 ms | 13 | 687k | 4,495 |
| 1,000 | off | 129 ms | 208 ms | 8 | 346k | 2,249 |
| 5,000 | on | 279 ms | 599 ms | 15 | 922k | 11,755 |
| 5,000 | off | 160 ms | 246 ms | 9 | 463k | 5,879 |
| 15,000 | on | 374 ms | 542 ms | 11 | 1.35M | 30,679 |
| 15,000 | off | 181 ms | 597 ms | 7 | 676k | 15,341 |

The "instances drawn" count includes each equipment part as its own instance. Draw calls stay at 15 or fewer at every size. Other lanes were sharing the CPUs during these runs, so p95 is noisy. llvmpipe is bound by vertex processing and fill. These numbers prove that the path works and scales in draw calls. They do not measure a GPU.

## Placeholder or not done
- All geometry and motion is procedural placeholder art. The route to Total War quality is authored base meshes and clips retargeted onto `Skeleton`, with this variation and instancing layer kept on top.
- No impostor cards are built: LOD2 is a box proxy.
- There is no LOD hysteresis.
- Soldiers outside the camera view cast no shadow.
- Animation blending between clips is not implemented: a clip change snaps.
- There is no surrender pose. Surrendered soldiers play idle.
- Ground is flat.
- Mapping kits from the fixture is a presentation stub: ranged formations become archers; melee formations become spearmen on side 0 and swordsmen on side 1.

## Director flags (not decided here)
- **R24 choreography (recon-critic §0.6):** positions come only from the fixture lerp, and hit-react and death play only on trace events. However, a melee swing loops for its whole 2 s step window, which is cosmetic choreography inside a step. Whether that is permitted needs a Director rule. The doc comment on `FormationPlayback` states this.
- **Facing:** facing is derived from motion or target (the trace has no facing). It is presentation only, but its meaning should be confirmed.

## Recommended next step
Integration (lane I) should put soldiers on the terrain height. After that, the next slice is: one authored CC0 humanoid in glTF retargeted to `Skeleton`, impostor cards for LOD2, crossfades between clips, and a re-run of this bench on the RTX 4050 to set the LOD distances.

## Dated note — 2026-11-05 (D-079; appended, the text above is unchanged)

Record: `docs/d079-m7-governance-reconciliation-2026-11-05.md` (Director decisions of 2026-11-05, cited DIR5). Each bullet quotes the superseded line, states the extent and what is retained, and cites the replacing DIR5 section and D-079 decision row (DR). Categories: DIRECTOR DECISION, ADMINISTRATIVE CONSEQUENCE, DELEGATED TECHNICAL DECISION, OPEN (needs the Director), READING (labelled interpretation).

Grouped in D-079 §2 as **S491** (1 statements). This is a report, not a ruling: the notes below correct its status against DIR5 and the Phase B audit.

- (`units.md:93`) — "Whether that is permitted needs a Director rule." — in full: DIRECTOR DECISION - cosmetic animation between authoritative steps is permitted and must never change results, positions, health, RNG, timing or hashes. **Replaced by** DIR5 §5.4 (D-079 DR42). [DIRECTOR DECISION]
