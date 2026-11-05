# Lane E — engine core (G1) report

Date 2026-11-04. Scope: implementation-spec §1 `Gfx.Engine`, `Gfx.Demo` skeleton, `Gfx.Tests`, scan script, README,
`M7Gfx.slnx`, `Directory.Build.props`. Nothing outside `prototypes/m7-gfx1/` was touched. No git writes.

## Built (files)
- `Gfx.Engine/Mgfx/MgfxGl.cs` — MGFX v11/GL writer (attributed copy of the recon probe writer).
  `Mgfx/EffectBuilder.cs` — DSL: params per stage, textures (VTF workaround built in), attributes, varyings, mains; posFixup epilogue auto-appended.
- `Effects/` — `GlslLib` (standard param set, PCF shadow, Lambert + hemispheric ambient in linear light, exponential height fog, ACES + gamma),
  `BuiltinEffects` (InstancedLit, InstancedDepth, SkinnedLit, SkinnedDepth, Sky, Post), `EffectLibrary` (+ `IEffectSource` discovery, `WarmupAll`).
- `Rendering/` — vertex/instance types, `GpuMesh`, `InstanceBuffer<T>`, `Primitives` (box, plane, 2-joint skinned bar), `BoneTexture` (pack/upload + CPU reference skin),
  `ShadowMap` (2048² Single, texel-snapped ortho fit, normal-offset + depth bias), `RenderContext`, `ForwardRenderer`.
- `Cameras/` — `CameraPose`, `TacticalCamera` (pan/rotate/zoom/edge clamp/follow, zoom-coupled pitch, exp smoothing), `CinematicCamera`/`CameraShot` (Catmull-Rom, dolly, orbit, ease, fov zoom, cut/blend).
- `Scene/` — `SunModel` (time of day → sun/moon dir, colours, ambient, sky, fog), `Frustum` (+ sphere batch cull), `ChunkLod` (distance LOD, hysteresis, visible-chunk list), `DemoScene`/`DemoSceneAttribute`/`SceneRegistry`.
- `Diagnostics/` — `Capture`, `FrameStats` (p50/p95/p99, draws/tris/instances), `SysInfo` (GL strings, CPU, RAM, OS; IntelIGPU / Software flags).
- `Hosting/GfxHost.cs` — process-singleton GL device on a dedicated thread; `HeadlessEnvironment` (native setenv of offscreen/software when no display).
- `Gfx.Demo/` — `Program` (`--sysinfo --list --capture --bench --interactive`), `InteractiveGame` (untested here: no display), `Scenes/EngineInstancedScene.cs`.
- `Gfx.Tests/` — 25 tests; `scripts/check-gfx-determinism.sh`; `README.md`.

## Works (evidence)
- All 6 effects compile+link on llvmpipe: `EffectLoadTests.EveryRegisteredEffectCompilesAndLinks`; `--sysinfo` lists each as OK.
- HDR target `SurfaceFormat.HalfVector4` renders on llvmpipe (recon listed it as untested); Color fallback is coded but not exercised.
- Instanced shadowed scene: captures `scratchpad/m7gfx/captures/engine/engine-instanced-{day,dusk,closeup}.png` (2,880 boxes + 40 skinned bars, PCF shadows, fog, sky, ACES). `sysinfo.txt` alongside.
- Instanced GPU skinning via bone texture: `RenderTests.BoneTextureVertexFetchSkinsLikeTheCpuReference` — pose row changes the image AND GPU-skinned output matches the CPU-skinned reference to < 2 % of covered pixels. Mutant (VS ignores `i_anim` rows) → test FAILS ("0 px changed").
- Capture non-empty + shadows darken + frustum culling active in the real scene: `RenderTests.EngineSceneCaptureIsNonEmptyAndShadowsDarkenIt`.
- Frustum vs MonoGame `BoundingFrustum` on 5,000 points, LOD hysteresis, chunk list: `FrustumTests.*`. Camera math: `CameraTests.*` (8).
- Skip path: with `SDL_VIDEODRIVER=dummy` the 4 device tests SKIP, 21 pass.
- Scan script: flags float/double/literals/Math transcendental/Random/Dictionary/HashSet/DateTime/GetHashCode/Parallel; `EngineUnitTests.DeterminismScanFlagsFloatAndPassesIntegerCode`.

## Numbers — SOFTWARE RENDERING (llvmpipe, 4 CPUs), 1280×720, not Victus performance
| engine-instanced, 30 frames | p50 ms | p95 ms | p99 ms | draws | tris |
|---|---|---|---|---|---|
| shadows on (2048²) | 70.0 | 116.3 | 204.8 | 7 | 75,086 |
| shadows off | 31.8 | 46.7 | 58.4 | 5 | 35,406 |
Frame time includes waiting for llvmpipe (1-pixel readback per frame).

## Placeholder / limits
- Single shadow map (no cascades); PCF is 3×3 point-sampled. Lighting is Lambert + hemispheric ambient (no specular/PBR, no normal maps).
- No MSAA on render targets (HDR target is non-multisampled) — AA is a gap for the next slice (FXAA in Post is the cheap route).
- `InteractiveGame` is written but not run (no display here). Optimus `NvOptimusEnablement` export is not possible from managed code — documented workaround only.
- MGFX format is MonoGame-internal: the effect-load test is the upgrade tripwire.

## Decisions needed / blockers
None for this lane. Real-GPU validation (RTX 4050, Iris Xe) of GLSL 1.20 + VTF + HalfVector4 is still open (critic R3).

## Next step
Lanes T/U/A build on this API; the integration lane adds cascades/AA only if the Victus smoke shows headroom.
