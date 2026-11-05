# G1 summary: M7 graphics prototype (lanes E, T, U, A, I)

Director-authorized, bounded prototype under `prototypes/m7-gfx1/`. It is not in `Sim.slnx` and not in CI, and it has no git history: nothing was committed, pushed or staged. To revert, delete the folder. It does not decide O-14 or the project boundaries. §4 is a recommendation only.

All timings in this report are **SOFTWARE RENDERING (llvmpipe, 4 CPUs, container)**. They are not Victus performance.

Captures (outside the repo): `/tmp/claude-0/-home-user-civDemo/54b82655-610f-500e-a185-8fae64bdd108/scratchpad/m7gfx/captures/{engine,terrain,units,avatar-cinematics,integration}/`.

## 1. Implemented vs recommended

| Area | Implemented (evidence) | Recommended / not done |
|---|---|---|
| Engine (E) | MonoGame 3.8.5 DesktopGL HiDef, no content pipeline. The GLSL 1.20 → MGFX writer is built at runtime.<br>Forward renderer: shadow pass (2048, PCF) → HDR (HalfVector4) → exposure + ACES + gamma.<br>Also: sky, height fog, sun model, tactical and cinematic cameras, frustum + chunk LOD, capture, frame stats, `--sysinfo` with an iGPU flag.<br>Tests: `EffectLoadTests`, `CameraTests`, `FrustumTests`, `RenderTests`, `EngineUnitTests` | Cascaded shadows; AA; GPU timer queries; a render graph (§3); real-GPU validation of VTF and HalfVector4 |
| Terrain (T) | `Gfx.Proc/Authoritative` is integer-only, seeded and SHA-256 pinned. It builds a 256×256 grid of 4 m cells with heights in mm and 9 classes, plus erosion, a river, a talus limit and roads.<br>Presentation: 64² chunks with 3 LODs and skirts, splat blending, procedural materials, Poisson scatter of trees, rocks and bushes, water, biome palette and fog.<br>Tests: `TerrainTests` (15), scan OK | Precipitation particles (cut); reflections; impostors; campaign seeding, which is blocked by the elevation scale (§5) |
| Units (U) | 20-joint skeleton, procedural skinned soldier, equipment parts, per-entity variation, 8 clips baked into a bone texture, instanced GPU skinning (VTF), and 3 animation LODs (skinned, then stepped, then proxy).<br>The fixture was exported from a scratch copy of Proto.M7; positions are interpolated and clips follow trace events.<br>Tests: `UnitsTests` (11) | Authored glTF humanoid; impostor cards; clip crossfades. The facing rule and the R24 rule are flagged for the Director |
| Avatar + cinematics (A) | `AvatarProfile` (versioned JSON, hashed, ≥12 morphs, role preferences). Era dress is computed from the capability tier. Parametric head and body.<br>`CinematicPlanner` is a pure function with rule truth tables. The player covers 8 outcome fixtures with a banner and a war-record card.<br>Tests: `AvatarTests` (18), `CinematicTests` (26) | Real `WarOutcomeRecord` from the sim; authored character art; cloth; weather beyond fog and exposure |
| **Integration (I, this lane)** | `Gfx.Demo/Scenes/BattlefieldScenes.cs`:<br>• **`battlefield`**: lane T temperate battlefield + lane U fixture formations (220 soldiers), with shadows, fog, sun and the `TacticalCamera`. Soldiers snap to `HeightAt`. A presentation-only `BattleSite` search places the fixture on the flattest dry, open strip.<br>• **`battlefield-cine`**: lane A surrender cinematic staged on the same terrain, on the flattest prop-free spot near the battle.<br>Tests: `IntegrationTests` (2): renderer invariance (grid and trace hashes unchanged after rendering every shot) and terrain snap. A snap mutant was killed by the semantic test (bounded at 120 s; it ran in about 3 s) | The battle sim should run on the grid, so that positions are terrain-aware; the site search stands in for that. The cinematic's aftermath does not yet use the trace's dead bodies |

Lane I hooks added to lane A code. All are default-preserving, and lane A's tests still pass:
- `CinematicSceneBase` gained the virtuals `Origin`, `SetGround` and `DrawBackground`.
- `CineSets.Build` gained `ground = true`.
- `PosedAvatar.SetPose` and `SkinPlaced` gained `groundWorldY = 0`.

### Integration captures (`captures/integration/`)
- `battlefield-wide.png`: TacticalCamera at 230 m, during the archer volley, at 10:30.
- `battlefield-mid.png`: 70 m, at the melee.
- `battlefield-follow-low.png`: 13 m follow cam behind the side-0 infantry, at 09:30. Shadowed and skinned at LOD0.
- `battlefield-dusk.png`: 40 m, aftermath at 17:42, facing the sun. Bodies are down and survivors are standing.
- `battlefield-sheet.png`: 2×2 contact sheet of the four shots above.
- `battlefield-cine-*.png` (9 frames) and `cine-strip.png` (8-frame strip).
  - Order: aftermath, avatar intro, opponent intro, confrontation, kneel at 30% and 92%, banner, war-record card.
  - The war-record card reads "FIXTURE DATA - NOT SIMULATION STATE".

Known visual limits:
- All art is placeholder-grade.
- The cinematic set keeps lane A's boxy set trees next to the terrain's own trees.
- The wide shot shows how small 220 soldiers are on a 1 km map.
- Where the infantry stands among trees in the follow shot, the trees are scatter decoration. Forest classes carry cover authoritatively; tree positions do not.

## 2. Gates (run on this tree, 2026-11-04)

- **Build:** `dotnet build M7Gfx.slnx -c Release --no-incremental` gave **0 warnings, 0 errors**, with TreatWarningsAsErrors on.
- **Tests:** `dotnet test Gfx.Tests -c Release` gave **97 passed, 0 failed, 0 skipped**, in 8 s. All GPU tests ran on llvmpipe; none skipped.
- There is only one test project. Counts per class:

| Class | Pass | Class | Pass |
|---|---|---|---|
| AvatarTests | 18 | FrustumTests | 5 |
| CameraTests | 8 | IntegrationTests | 2 |
| CinematicTests | 26 | RenderTests | 2 |
| EffectLoadTests | 4 | TerrainTests | 15 |
| EngineUnitTests | 6 | UnitsTests | 11 |

- **Determinism scan:** `scripts/check-gfx-determinism.sh` reports `OK (4 files scanned in Gfx.Proc/Authoritative)`.
- **`--sysinfo`:** Mesa llvmpipe (LLVM 20.1.2), GL 4.5 compat, GLSL 4.50, MonoGame 3.8.5, HiDef, 4 CPUs, 10 GiB, Ubuntu 24.04, .NET 10.0.12. Flags: Software. A SOFTWARE RENDERING warning is printed.

### Bench: SOFTWARE RENDERING (llvmpipe), not Victus performance
Settings: 1280×720, shadows on, 30 measured frames after 3 warm-up frames, first capture shot of each scene. CPU frame time is measured synchronously after reading back one pixel.

Draw calls, triangles and instances are for the last frame and include the shadow pass.

| Scene | p50 ms | p95 ms | p99 ms | draws | triangles | instances |
|---|---:|---:|---:|---:|---:|---:|
| **battlefield** (wide) | 103.3 | 145.0 | 150.6 | 49 | 293,464 | 5,861 |
| **battlefield-cine** | 100.2 | 132.7 | 134.3 | 61 | 409,643 | 8,970 |
| terrain-temperate | 107.4 | 134.9 | 137.2 | 53 | 400,292 | 9,856 |
| terrain-arid | 73.0 | 88.4 | 91.5 | 55 | 161,896 | 1,505 |
| units-clash (220) | 42.5 | 54.9 | 59.7 | 13 | 66,818 | 403 |
| units-closeup | 38.8 | 52.1 | 52.4 | 25 | 41,410 | 231 |
| units-bench-1k | 137.5 | 160.7 | 180.9 | 13 | 687,010 | 4,495 |
| units-wide5k | 178.0 | 203.7 | 231.4 | 15 | 922,338 | 11,755 |
| units-bench-15k | 196.4 | 227.6 | 231.1 | 11 | 1,347,986 | 30,679 |
| engine-instanced | 59.4 | 71.2 | 75.4 | 7 | 75,086 | 5,316 |
| avatar-eras | 41.9 | 60.0 | 63.1 | 27 | 119,538 | 27 |
| avatar-faces | 42.8 | 55.7 | 62.1 | 27 | 71,486 | 27 |
| avatar-turntable | 25.3 | 48.3 | 74.4 | 5 | 10,448 | 5 |
| cine-execution | 39.5 | 48.9 | 70.8 | 4 | 15,042 | 4 |
| cine-surrender | 33.8 | 42.8 | 44.2 | 4 | 15,042 | 4 |
| cine-capture | 36.8 | 45.1 | 51.9 | 4 | 15,042 | 4 |
| cine-peace | 37.9 | 46.0 | 66.6 | 4 | 15,042 | 4 |
| cine-inconclusive | 36.1 | 52.6 | 57.6 | 4 | 15,042 | 4 |
| cine-naval | 35.4 | 43.5 | 46.9 | 4 | 8,826 | 4 |
| cine-siege | 40.9 | 50.5 | 57.4 | 4 | 14,730 | 4 |
| cine-defeat | 35.0 | 59.4 | 62.6 | 4 | 15,042 | 4 |

What this shows, from structure rather than speed:
- Draw calls stay at ≤ 61 even at 15k soldiers, because instancing, per-LOD buckets and chunking work.
- The triangle counts are the budget levers: 1.35 M at 15k soldiers, 0.4 M for temperate terrain with props.
- Speed on the RTX 4050 is unknown until the Victus run in §6.

## 3. Recommended 3D architecture (recommendation, not implemented beyond the prototype)

- **Passes, then a small render graph.**
  - The prototype's fixed forward chain works: shadow → HDR scene (sky, opaque, water) → post (exposure, ACES, gamma).
  - The next step is a declarative, ordered pass list: shadow cascades → depth prepass (optional) → opaque → decals → water/transparent → particles → post (tonemap, AA) → HUD.
  - Each pass declares its targets and reads. Content registers draw buckets per pass (mesh × LOD × material). It does not issue raw calls.
  - Stay forward-lit. MonoGame GL has no MRT-friendly G-buffer budget on Iris Xe, and there is no compute.
- **Sim and render threading.**
  - The authoritative battle sim runs at a fixed pulse (the 2 s step, or the ADR-061 sub-step) on its own thread or task. It publishes **immutable snapshots**: double-buffered arrays of positions in mm, states and events.
  - The render thread only reads the latest two snapshots. GL stays on one thread.
  - Render never writes sim state. The prototype proves the read-only half: the renderer-invariance tests hash the grid and the trace before and after rendering.
- **Interpolation.**
  - Render time = sim time − one step, then lerp between snapshots s and s+1 (`FormationPlayback`). Facing comes from displacement or target, and clips are selected from state plus events.
  - R24 holds: the animator chooses pose and timing only. The prototype interpolates a 2 s trace; the same code serves the live sim.
- **Effect library.**
  - GLSL 1.20 sources live in code. They are packed into MGFX at load, with no mgfxc and no Wine (ADR-009 raw-load premise kept), plus a shared GLSL include library and a standard parameter binder (`RenderContext.Apply`).
  - Vertex texture fetch uses the critic's pixel-stage-sampler workaround.
  - Recommendation: keep it, and add a startup self-test that already exists as `--sysinfo` effect warm-up. Then a pre-packed `.mgfx` cache keyed on the source hash, for the Windows load time.
- **Asset pipeline.**
  - Use **glTF 2.0** through **SharpGLTF.Core/Runtime 1.0.7 (MIT, pre-approved)**, imported at build or first load into an engine-native binary cache: meshes, skins retargeted onto the canonical 20-joint `Skeleton`, and clips baked into bone textures.
  - Add a **catalogue** (JSON under `assets/`, validated by a test) with one row per asset:
    - key, kind (mesh/material/clip/texture)
    - **provenance**: source, author, licence (CC0/purchased/commissioned/procedural), licence URL or proof, date
    - era and biome tags, LOD set, triangle budget, and a variation-slot schema
  - Procedural code layers variation (tint, wear, part swaps, placement) on top of authored base assets. It does not replace them.

## 4. Recommended project boundaries (recommendation; needs Director authorization before any change to `Sim.slnx` or CI)

1. **`Sim.Battle`**: the authoritative battle sim plus authoritative battlefield generation. `Gfx.Proc/Authoritative` is the template: integer-only, seeded, SHA-256 canonical bytes.
   - It references Sim.Core and Sim.Data only.
   - It **must be added to `check-banned-constructs.sh` SIM_DIRS and ALL_DIRS**, plus the read-isolation review, plus a twin-process determinism test. This is O-14 option (a). The kernel deltas (sub-turn clock) go through ADR-061.
2. **One presentation project** (`Sim.Presentation3D` or `Sim.Rendering`) holding engine, procedural presentation, units/animation, avatar appearance and cinematics as **namespaces**.
   - The prototype's five Gfx.* libraries share one float/GPU/skeleton type system. Splitting them added project plumbing and no determinism protection. The protection is the one-way sim → presentation edge.
   - Add a csproj guard test: no sim-surface project may reference a presentation project or MonoGame or ImGui.
   - This needs an ADR-009 amendment, because MonoGame would then exist outside Sim.Ui.
3. **No `Sim.Assets` code project.** The catalogue and provenance data live as JSON with a validator test.
4. **Avatar profile outside WorldState.**
   - It is a versioned, hashed sibling file next to the session files (the `SessionManifest` precedent). It is not in `WorldHash` and not in the order log.
   - Era dress is recomputed from realized capability state at render time (Law 4; the `UiEra` precedent).
   - D-077 should state that the avatar is presentation-only (Ledger §17.2).

## 5. Blockers and Director decisions needed

1. **D-038 C1/C3 supersession for faces.** It is needed for the avatar and cinematics. D-075 covers the battlefield only.
2. **Execution as a sim outcome**, plus a content-rating owner, and who decides what is "historically appropriate".
   - No leader, war or peace state exists in Sim.Core, so cinematics run on labelled fixtures.
3. **R24 sub-step melee choreography**: the swing loops within a 2 s step. Also the meaning of soldier facing, which is not in the trace.
4. **The terrain downscale rule.**
   - Forest, Jungle, Hills, Mountain, Beach and Desert are invented from integer thresholds, and the biome thresholds are prototype values.
   - Beach and Desert need cost-table rows.
   - Fording semantics are open.
   - **Campaign elevation has no metre scale.** This blocks real campaign seeding.
5. **Push authorization for the Victus benchmark** (§6). Without it there is no Windows artifact and no real-GPU numbers.
6. **Optimus.** DesktopGL cannot export `NvOptimusEnablement` from managed code. A tooling decision is needed: an nvpatch post-publish step, a native launcher stub, or a user-side setting.
7. **Real-GPU validation** of GLSL 1.20, VTF and HalfVector4 on the RTX 4050 and the Iris Xe (critic R3). This is the main technical risk; no real GPU has run this code.
8. **Open minor verification findings carried forward. None is a blocker.**
   - Lane T: a road sea-avoidance mutant survives.
   - Lane U: 6 coverage gaps, including LOD and facing mutants.
   - Lane A: the era-lineup invariance test has no write path to catch.

## 6. Victus benchmark plan (needs push authorization)

1. **Build.** On push authorization, CI builds `Gfx.Demo` win-x64 self-contained, as `ui-artifact.yml` does for Sim.Ui, and zips it with a `Run benchmark.cmd`.
   - The script runs `Gfx.Demo --sysinfo`, then `--bench <scene> --frames 1800 --size 1920x1080` for `battlefield`, `battlefield-cine`, `terrain-temperate`, `units-bench-1k`, `units-wide5k` and `units-bench-15k`, each with shadows on and off.
   - It writes `bench-<sha>.txt` next to the exe. The Director double-clicks it and returns the file.
   - Alternative: hand the Director a locally built zip. That still needs an authorized delivery path.
2. **Force the dGPU.**
   - Before running: Windows Settings → System → Display → Graphics → add `Gfx.Demo.exe` → **High performance**, or the NVIDIA Control Panel equivalent.
   - `--sysinfo` must report the NVIDIA renderer. If it prints `IntelIGPU`, the run is **invalid as a Victus score** and should be redone. That run is still kept as the Iris Xe must-not-crash tier.
3. **Conditions.** AC power and the HP performance mode are recorded. 3 repetitions; report the median.
4. **Targets** (PROPOSAL, from recon-hardware §4), judged on the RTX 4050 at 1080p:
   - Battlefield p95 ≤ 16.7 ms and cinematic p95 ≤ 33.3 ms.
   - ≤ 2,000 draws and ≤ 3 M triangles.
   - Iris Xe: must run without a crash.
5. **Follow-ups the results decide.** GPU timer queries and VRAM readout come next, then the LOD distances (currently 40 / 120 m) and the shadow cascade count.

## 7. Recommended next graphics slice (G2)

1. **Victus run** (§6). Set the LOD and shadow budgets from real numbers.
2. **One authored asset path end to end.** One CC0 humanoid in glTF via SharpGLTF, retargeted to `Skeleton`, plus one authored ground material set. The catalogue row carries provenance, and the procedural variation layer goes on top.
3. **Battle sim on the grid.** Have the Proto.M7 snapshot, or Sim.Battle once authorized, read `BattleTerrainGrid` costs and heights, so the integration's `BattleSite` stand-in disappears. Add impostor cards (LOD2) and clip crossfades.
4. **Cinematic aftermath from the trace's final snapshot**: bodies and survivors at the real positions. This waits for the face and execution rulings before any `WarOutcomeRecord` exists in the sim.

## 8. git status of `/home/user/m5w/dev` (read-only check)

`git status --porcelain` shows 31 entries. **This workstream's only path is `?? prototypes/m7-gfx1/`**: 88 untracked files with `--untracked-files=all`; `bin/` and `obj/` are ignored.

Other paths belong to other work and were not touched:
- `?? prototypes/m7-sprint1/` (110 files)
- ` M` 23 tracked docs: `docs/adr/adr-009`, `adr-019`, `adr-022`, `adr-055`, `cr-006`, `cr-013`, `cr-017`, `civ-sim-architecture-v3-outline.md`, `current-state.md`, `d011`, `d038`, `d039`, `d052`, `d065`, `d067`, `design/recovered-decisions-architecture-invariants.md`, `m0-kernel-spec.md`, `m6-plan/e1-visual-acceptance-checklist.md`, `m6-plan/m7-handoff-register.md`, `m7-plan/aviation-progression.md`, `m7-plan/unit-catalogue-and-progression.md`, `milestones.md`, `style-bible-parchment.md`
- `??` `docs/adr/adr-060…`, `docs/d075…`, `docs/d076…`, `docs/d077…`, `docs/m7-plan/m7-2-docket-m7-3-contracts.md`, `docs/m7-plan/m7-2-m7-3-revision-2026-11-04.md`, `docs/m7-spec.md`

## 9. File inventory (`prototypes/m7-gfx1/`, lines via `wc -l`, 11,687 total including the fixture)

| Path | Lines | Purpose |
|---|---:|---|
| `Directory.Build.props` / `M7Gfx.slnx` / `README.md` | 22 / 9 / 34 | Isolated build settings (no root import), solution, usage |
| `scripts/check-gfx-determinism.sh` | 57 | Integer-only scan of `Gfx.Proc/Authoritative` |
| `fixtures/battle-trace-combinedarms-s7.json` | 1917 | Battle trace exported from a scratch copy of Proto.M7 (CombinedArms seed 7, 104 steps), labelled |
| **Gfx.Engine** | | |
| `Mgfx/MgfxGl.cs`, `Mgfx/EffectBuilder.cs` | 101, 127 | GLSL → MGFX v11 writer (attributed to the recon probe); effect assembly |
| `Effects/EffectLibrary.cs`, `BuiltinEffects.cs`, `GlslLib.cs` | 124, 151, 84 | Effect registry and warm-up; lit/instanced/skinned/depth/sky/post shaders; shared GLSL |
| `Rendering/ForwardRenderer.cs`, `RenderContext.cs`, `ShadowMap.cs` | 117, 71, 49 | Shadow → HDR → post frame; standard parameter binding; directional 2048 PCF |
| `Rendering/GpuMesh.cs`, `VertexTypes.cs`, `Primitives.cs`, `BoneTexture.cs` | 83, 86, 68, 66 | Instanced meshes and buffers; vertex and instance layouts; box/plane builders; bone-matrix texture |
| `Cameras/CameraPose.cs`, `TacticalCamera.cs`, `CinematicCamera.cs` | 22, 71, 81 | Pose and easing; pan/rotate/zoom/clamp/follow; spline, dolly and orbit shots |
| `Scene/DemoScene.cs`, `Frustum.cs`, `SunModel.cs` | 69, 95, 54 | Scene base and registry; culling and chunk LOD; time of day → sun, sky and fog |
| `Diagnostics/Capture.cs`, `FrameStats.cs`, `SysInfo.cs` | 47, 51, 69 | PNG capture; p50/p95/p99 and draw counters; GL/CPU/RAM info and iGPU flag |
| `Hosting/GfxHost.cs` | 123 | Headless or offscreen device host (SDL offscreen + llvmpipe) |
| **Gfx.Proc** | | |
| `Authoritative/BattleTerrainGenerator.cs`, `BattleTerrainGrid.cs`, `IntNoise.cs`, `TerrainTypes.cs` | 300, 96, 65, 47 | INTEGER-ONLY grid generation, canonical bytes and hash, integer noise, classes, biomes and inputs |
| `Presentation/TerrainMesh.cs`, `TerrainScenery.cs`, `SplatModel.cs`, `MaterialTextures.cs` | 226, 182, 72, 78 | Chunked LOD mesh, splats and height queries; GPU terrain, water and props; blend weights and palettes; procedural textures with mips |
| `Presentation/Scatter.cs`, `PropMeshes.cs`, `TerrainEffects.cs`, `PNoise.cs`, `Vertices.cs` | 157, 179, 106, 44, 30 | Poisson prop scatter; tree, bush and rock meshes; splat and water shaders; float noise; vertex types |
| `Scenes/TerrainScenes.cs` | 73 | `terrain-temperate` and `terrain-arid` scenes (fixture inputs) |
| **Gfx.Units** | | |
| `Skeleton.cs`, `SoldierMeshes.cs`, `SoldierLook.cs` | 76, 237, 83 | 20-joint skeleton; skinned body, equipment and proxy; integer-hash per-entity variation |
| `ClipLibrary.cs`, `UnitEffects.cs`, `SoldierCrowd.cs` | 223, 66, 131 | 8 procedural clips baked to a bone texture; unit skinned shader; instanced skinned crowd with 3 LODs |
| `BattleTrace.cs`, `FormationPlayback.cs` | 121, 184 | Fixture parser and hash; step interpolation, facing and clip selection (R24 flagged) |
| `Demo/UnitsScenes.cs` | 218 | Close-up, clash, 5k scene and 1k/15k benches |
| **Gfx.Avatar** | | |
| `AvatarProfile.cs`, `EraDress.cs` | 238, 70 | Versioned and hashed identity profile; capability tier + role → outfit |
| `AvatarMesh.cs`, `AvatarRenderer.cs`, `Demo/AvatarScenes.cs` | 405, 113, 128 | Morph-weighted head and body; CPU-skinned posed avatar (+ ground parameter, lane I); eras, faces and turntable scenes |
| **Gfx.Cinematics** | | |
| `WarOutcome.cs`, `CinematicPlanner.cs` | 69, 248 | Labelled outcome fixtures; pure planner with validity rules |
| `CinematicPlayer.cs`, `CineClips.cs`, `CineSets.cs`, `CardText.cs` | 168, 219, 144, 113 | Shot timeline and cameras; two-character clips; set backdrops (+ `ground` flag, lane I); glyph text |
| `Demo/CinematicScenes.cs` | 251 | Banner, card, 8 outcome scenes, and the `Origin`/`SetGround`/`DrawBackground` hooks (lane I) |
| **Gfx.Demo** | | |
| `Program.cs`, `InteractiveGame.cs` | 124, 77 | `--sysinfo`/`--list`/`--capture`/`--bench`; windowed tactical mode (not exercised headless) |
| `Scenes/EngineInstancedScene.cs` | 121 | Lane E acceptance scene |
| `Scenes/BattlefieldScenes.cs` | 246 | **Lane I**: `BattleSite` placement, `battlefield`, `battlefield-cine` |
| **Gfx.Tests** | | |
| `GfxTestSupport.cs` | 25 | `[GfxFact]`: skips when there is no device |
| `EngineUnitTests`, `EffectLoadTests`, `CameraTests`, `FrustumTests`, `RenderTests` | 88, 46, 100, 87, 104 | Engine maths, effect load, cameras, culling, render smoke |
| `TerrainTests`, `UnitsTests`, `AvatarTests`, `CinematicTests`, `IntegrationTests` | 334, 291, 198, 293, 63 | Lane T, U, A and I tests |
| `reports/engine.md`, `terrain.md`, `units.md`, `avatar-cinematics.md`, `g1-summary.md` | 47, 124, 97, 119, — | Lane reports and this summary |
| `*.csproj` (7) | 11–18 each | Project files (MonoGame 3.8.5 via Gfx.Engine; xunit 2.9.3, Test.Sdk 17.14.1, runner 3.1.4) |

## Dated note — 2026-11-05 (D-078; appended, the text above is unchanged)

Record: `docs/d078-graphics-quality-mandate.md` (Director graphics quality mandate of 2026-11-05). D-078 §3 (A-28) lists this note; it was appended on 2026-11-05 during the D-079 pass (D-079 A-30).

- **S99** (§7, line 167, "## 7. Recommended next graphics slice (G2)": Victus run, one authored asset end to end, battle sim on the terrain grid, cinematic aftermath) — **superseded as to priority:** the next graphics sprint is the quality demonstrator (D-078 QM3, QM4). The G1 items survive as later work, except the Victus measurement, which D-078 QM9 keeps. G1 is a prototype report, not a frozen record.

## Dated note — 2026-11-05 (D-079; appended, the text above is unchanged)

Record: `docs/d079-m7-governance-reconciliation-2026-11-05.md` (Director decisions of 2026-11-05, cited DIR5). Each bullet quotes the superseded line, states the extent and what is retained, and cites the replacing DIR5 section and D-079 decision row (DR). Categories: DIRECTOR DECISION, ADMINISTRATIVE CONSEQUENCE, DELEGATED TECHNICAL DECISION, OPEN (needs the Director), READING (labelled interpretation).

Grouped in D-079 §2 as **S489** (14 statements). This is a report, not a ruling: the notes below correct its status against DIR5 and the Phase B audit.

- (`g1-summary.md:15`) — "The facing rule and the R24 rule are flagged for the Director" — in part: R24 is ruled - cosmetic animation between authoritative steps is permitted and must never change results, positions, health, RNG, timing or hashes (DIRECTOR DECISION). Retained: the facing rule is not addressed by DIR5; as a presentation-only derivation it falls under §5.4's constraints (DELEGATED TECHNICAL unless facing ever enters the simulation). **Replaced by** DIR5 §5.4 (D-079 DR42). [DIRECTOR DECISION; DELEGATED TECHNICAL DECISION]
- (`g1-summary.md:117`) — "## 4. Recommended project boundaries (recommendation; needs Director authorization before any change to `Sim.slnx` or CI)" — in part: item 1 (Sim.Battle) is adopted as the Director's chosen direction, but creation still awaits the architecture review (no Sim.slnx or CI change now); item 4 (avatar profile outside WorldState) is a DIRECTOR DECISION (§5.1). Retained: items 2-3 (one presentation project, no Sim.Assets) remain a technical recommendation - 'Rendering and presentation project boundaries still require a technical recommendation' (DELEGATED TECHNICAL). **Replaced by** DIR5 §1.3, §5.1 (D-079 DR4–DR6, DR39). [DIRECTOR DECISION; DELEGATED TECHNICAL DECISION]
- (`g1-summary.md:121`) — "This is O-14 option (a)." — in part: DIRECTOR DECISION (DIR5 §1.3) - the separate Sim.Battle project is the chosen architecture direction (O-14 option a); retained: Sim.Battle is NOT created until the architecture review, the prototype stays isolated; presentation/rendering project boundaries remain a technical recommendation (DELEGATED TECHNICAL). **Replaced by** DIR5 §1.3 (D-079 DR4–DR6). [DIRECTOR DECISION; DELEGATED TECHNICAL DECISION]
- (`g1-summary.md:130`) — "D-077 should state that the avatar is presentation-only (Ledger §17.2)." — in full: DIRECTOR DECISION - the avatar is purely visual, has no gameplay effects and stays outside authoritative simulation state, deterministic hashes and gameplay calculations (closes D-077 Q-V2). **Replaced by** DIR5 §5.1 (D-079 DR39). [DIRECTOR DECISION]
- (`g1-summary.md:134`) — "**D-038 C1/C3 supersession for faces.** It is needed for the avatar and cinematics. D-075 covers the battlefield only." — in full as a blocker: recorded by D-077 S86/S87 (2026-11-04) and widened by D-078 S100; DIR5 §5.1 (customizable face and body) and §6.2 (credible faces for characters, soldiers and the avatar) make detailed faces a Director requirement. Retained: the campaign-map question (D-078 Q-QM1) is outside this line. **Replaced by** DIR5 §5.1, §6.2 (D-079 DR39, DR45). [DIRECTOR DECISION]
- (`g1-summary.md:135`) — "**Execution as a sim outcome**, plus a content-rating owner, and who decides what is "historically appropriate"." — in part: DIRECTOR DECISION - if an empire loses all its cities its leader is executed, as an authoritative simulation outcome (§4.1); captured-leader decisions are victor choices inside the delegated post-battle framework (§4.2); execution is shown only when the authoritative fate is executed and the content is enabled (§5.3). Retained: who owns the content setting or rating policy is not addressed by DIR5 (treat as a presentation setting, DELEGATED; escalate only if a rating policy is needed). **Replaced by** DIR5 §4.1, §4.2, §5.3 (D-079 DR36, DR37, DR41). [DIRECTOR DECISION; DELEGATED TECHNICAL DECISION]
- (`g1-summary.md:137`) — "**R24 sub-step melee choreography**: the swing loops within a 2 s step." — in part: ruled - permitted as cosmetic animation between authoritative steps (no change to results, positions, health, RNG, timing or hashes; DIRECTOR DECISION). Retained: soldier facing (same line) is not addressed by DIR5. **Replaced by** DIR5 §5.4 (D-079 DR42). [DIRECTOR DECISION]
- (`g1-summary.md:143`) — "**Push authorization for the Victus benchmark** (§6). Without it there is no Windows artifact and no real-GPU numbers." — in full as to authorization: pushes to the development branch are authorized (§1.3) and a real Windows build and benchmark on the Victus is required (§6.5) (DIRECTOR DECISION). Retained: forcing the dGPU (Optimus) and the build path are DELEGATED TECHNICAL; prototypes stay isolated (§1.3). **Replaced by** DIR5 §1.3, §6.5 (D-079 DR4–DR6, DR48). [DIRECTOR DECISION; DELEGATED TECHNICAL DECISION]
- (`g1-summary.md:147`) — "Lane T: a road sea-avoidance mutant survives." — in full as a status (Phase B R16): the mutant is killed by `RoadStepsAwayFromTheSeaAndStaysContinuous` (terrain.md Verification #3), which passed in the final verification run. **Replaced by** DIR5 §8 Phase B (D-079 DR51). [ADMINISTRATIVE CONSEQUENCE]
- (`g1-summary.md:148`) — "Lane U: 6 coverage gaps, including LOD and facing mutants." — retained as a status (Phase B R18): the six gaps were never persisted outside this summary and are not fixed; re-running the verifier and a fix round is DELEGATED TECHNICAL work. **Replaced by** DIR5 §8 Phase B (D-079 DR51). [ADMINISTRATIVE CONSEQUENCE]
- (`g1-summary.md:149`) — "Lane A: the era-lineup invariance test has no write path to catch." — in full as a status (Phase B R17): fixed (avatar-cinematics.md Verification #2); mutant M6 now fails the test, which passed in the final verification run. **Replaced by** DIR5 §8 Phase B (D-079 DR51). [ADMINISTRATIVE CONSEQUENCE]
- (`g1-summary.md:151`) — "## 6. Victus benchmark plan (needs push authorization)" — in part: push authorized (§1.3). The plan's capture set is extended by DIRECTOR DECISION (§6.5): soldier close-up, formation, large battlefield, cinematic camera, campaign environment; frame-time and frame-rate distribution, GPU and CPU utilization, VRAM and RAM, load times and scalability; before/after screenshots. Retained: the plan's method; its targets (:161-164) stay proposals (§7). **Replaced by** DIR5 §1.3, §6.5, §7 (D-079 DR4–DR6, DR48, DR50). [DIRECTOR DECISION]
- (`g1-summary.md:167`) — "## 7. Recommended next graphics slice (G2)" — in part: the order is replaced by DIR5 §6.6 (1 Victus benchmark; 2 one production-quality authored asset with provenance; 3 high-quality soldier and avatar demonstrator; 4 one detailed battlefield environment; 5 battle simulation on the terrain grid; 6 cinematic aftermath; 7 procedural structures; 8 weather). Items 3-4 come before 'Battle sim on the grid' (:171), and :170's 'One CC0 humanoid' must meet 'production-quality' under the mixed asset strategy (§6.4). Retained: :169, :170 and :172 in substance (partly recorded already by D-078). **Replaced by** DIR5 §6.6, §6.4 (D-079 DR47, DR49). [DIRECTOR DECISION]
- (`g1-summary.md:172`) — "This waits for the face and execution rulings before any `WarOutcomeRecord` exists in the sim." — in part: the rulings exist (faces §5.1/§6.2; execution §4.1/§5.3) and M7 owns basic military records including leader fates (§4.3). Retained: the aftermath still needs a real war-outcome record to be built (which M7.n packet is ADMINISTRATIVE planning). **Replaced by** DIR5 §4.1, §4.3, §5.1, §5.3 (D-079 DR36, DR38, DR39, DR41). [ADMINISTRATIVE CONSEQUENCE]
