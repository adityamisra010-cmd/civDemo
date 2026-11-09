# G2 stage `perf`: the Victus benchmark harness and the Windows BEFORE package

Stage of the G2 quality demonstrator (`prototypes/m7-gfx2`), DR49 priority 1 (D-079 DR48, DR49; D-078 QM8, QM9).
Branch `g2/perf`, cut from `g2/trunk` at `0395b6c`. Date: 2026-11-09.

**Status in one paragraph.** The benchmark harness exists, is tested, and has run end to end in this container on
llvmpipe (software GL) and, as a Windows build, under Wine 9 with Xvfb. The Windows package for the Director is
built (`g2-before-3cc3ce3.zip`, 7,272,515 bytes, 69 files). **It has not run on the Victus or on any NVIDIA or Intel GPU**, so this report
contains **no Victus evidence**: every timing below is labelled *llvmpipe, not Victus evidence*. This stage changes
no rendering: the BEFORE baseline is exactly G1's renderer and G1's placeholder scenes, which still look like a
low-poly prototype (§14). The 117-run llvmpipe suite found a GL memory leak in MonoGame 3.8.5 that grew the process
by about 8 GB over the suite; on the Victus that would exceed the RTX 4050's 6 GB partway through (a projection, not
measured). The harness now works around it (§9.3).

## 1. What now exists

| Item | Where | What it is |
|---|---|---|
| Benchmark harness | `Gfx.Perf/` (new library: 21 C# files, the project file, the launcher source, 5 package files) | Suites, sweeps, single scenes and a GPU self-test; GPU timer queries, CPU sections, memory, VRAM, NVML, counters, flags; CSV, JSON and summary output. Started through `Gfx.Demo` (`--suite`, `--sweep`, `--scene`, `--gpu-selftest`, `--list-configs`, `--perf-help`) |
| Optional marker hook | `Gfx.Engine/Diagnostics/PerfMarkers.cs` (new file) | `PerfMarkers.Segment("name")` / `Counter(...)` and `IPerfLodScalable` for later G2 code. **No G1 code calls it**; the harness picks up whatever instrumented code reports |
| Discrete-GPU launcher | `Gfx.Perf/launcher/civsimbench.c`, `scripts/build-launcher.sh` | `CivSimBench.exe`, 5,120 bytes, no C runtime, exports `NvOptimusEnablement` and `AmdPowerXpressRequestHighPerformance`, runs `Gfx.Demo.dll` in-process through `hostfxr_main_startupinfo` |
| Windows package | `scripts/package-victus.sh`; files in `Gfx.Perf/package/` | `g2-before-3cc3ce3.zip` in the scratchpad `victus/` folder: self-contained win-x64 build, launcher, `run-benchmark.bat`, `README-VICTUS.txt`, GPU-preference scripts, `THIRD-PARTY-NOTICES.txt`, `PACKAGE-SHA256.txt` |
| Comparison tool | `scripts/compare-perf.py` | Matches two result files (JSON or CSV) by configuration values and prints per-configuration medians and deltas |
| Tests | `Gfx.Tests/PerfTests.cs` | 53 tests in three classes (§10) |

## 2. What changed in the tree (`git diff 0395b6c` on this branch)

- **New:** `Gfx.Perf/*`, `Gfx.Engine/Diagnostics/PerfMarkers.cs`, `Gfx.Tests/PerfTests.cs`, `scripts/build-launcher.sh`,
  `scripts/package-victus.sh`, `scripts/compare-perf.py`, `reports/perf.md`, `reports/captures/perf/*.jpg`.
- **Changed, outside `Gfx.Perf`:** `Gfx.Demo/Program.cs` (+3 lines: forward the benchmark options to `PerfCli`,
  print its usage), `Gfx.Demo/Gfx.Demo.csproj` (reference `Gfx.Perf`; copy `fixtures/battle-trace-combinedarms-s7.json`
  into build and publish output, §9.2), `Gfx.Tests/Gfx.Tests.csproj` (reference), `M7Gfx2.slnx` (+1 project),
  `assets/PROVENANCE.md` (§3.7).
- **Unchanged (checked with `git diff --stat 0395b6c`):** `Gfx.Engine/Rendering`, `Gfx.Engine/Scene`, `Gfx.Proc`,
  `Gfx.Units`, `Gfx.Avatar`, `Gfx.Cinematics`, `Gfx.Demo/Scenes`. G1 is untouched: `git diff --stat 1601c74 --
  prototypes/m7-gfx1` is empty. `scripts/check-gfx-determinism.sh`: OK (4 files).
- Code and scripts: 38 files, +4,912 / -1 lines (`git diff --stat 0395b6c 3cc3ce3 -- prototypes/m7-gfx2`); this report and the five review JPEGs come on top.

## 3. How it measures

The harness opens its own window and GL device (a MonoGame `Game`, so the close button, focus and events work). Per
run it loads the scene bound to the slot, renders it through the **unchanged** G1 `ForwardRenderer` into an
offscreen target of exactly the configured resolution, and shows a scaled preview in a 960x540 window (vsync off,
no fixed time step, no sleep when unfocused). The scene is wrapped in a `ContentTap`: an `IRenderContent` that
forwards the renderer's two calls untouched and brackets them with GPU timestamps and stopwatch readings. A test
proves the tapped frame is pixel-identical to the untapped one (§10).

| Metric | Definition |
|---|---|
| Frame time | Wall time between consecutive frame starts (what the player sees; FPS = 1000 / mean). The first measured frame has no interval (it would include the measurement-start sampling), so N measured frames give N-1 intervals |
| CPU frame time | Scene update + render submission + preview blit on the CPU. Present is separate (`present_ms`); harness book-keeping is separate (`harness_ms`) |
| GPU frame time | GL timestamp span `frame.begin` … `present.blit.end`. When the CPU is the bottleneck the GPU idles between commands inside that span, so it is an upper bound of GPU work |
| GPU per pass | Timestamps `update.end`, `shadow.casters.begin/end`, `opaque.begin/end`, `render.end`, `present.blit.end` give: uploads, shadow clear, shadow casters, scene clear + sky, opaque, post, preview blit. Named segments from `PerfMarkers` are added when code reports them (G1 reports none) |
| GPU timer mechanics | `glQueryCounter(GL_TIMESTAMP)` through `SDL_GL_GetProcAddress`; a ring of 8 query sets, read only when `GL_QUERY_RESULT_AVAILABLE`, so the CPU never waits; a set still pending when the ring comes round is dropped and flagged; after the measured frames one `glFinish` collects the rest |
| CPU per section | Update (pose and animation evaluation), render submission, shadow-caster calls, opaque calls, the first content pass, blit, present, harness |
| Animation CPU | `anim_cpu_ms` = the scene update. **Caveat:** in the G1 crowd the per-soldier clip sampling and instance packing run inside the first draw pass (`SoldierCrowd.Prepare`), so for crowd scenes they are in `cpu_first_pass_ms`, not in `anim_cpu_ms`. Later code can mark it with `PerfMarkers` |
| Memory | Working set (start, peak every 60 frames, end), private bytes, GC heap peak, GC committed, allocation per frame, gen0/1/2 collections |
| VRAM | `GL_NVX_gpu_memory_info` (dedicated, total and current available, evictions); `GL_ATI_meminfo` fallback; "unavailable" otherwise. Device-wide, not per process |
| App GPU memory | A census of every live MonoGame graphics resource (textures, render targets with depth, vertex and index buffers), sized from format and dimensions: an estimate of what this process holds, plus the size of MonoGame's framebuffer cache |
| NVML | Background sampler (5 Hz): graphics, SM and memory clocks, temperature, power and power limit, utilization, memory, P-state, clock-event (throttle) reasons. Loaded from the NVIDIA driver when present |
| Utilization | Process CPU (% of one core and of all cores), system CPU (`GetSystemTimes` / `/proc/stat`), GPU utilization (NVML) |
| Counters | Draw calls, triangles, instances (G1 `FrameStats`, shadow pass included); soldier LOD0/LOD1/LOD2/culled counts; terrain chunks and prop instances drawn |
| Load times | Device creation, effect compilation (warm-up of all effects), scene load, time to first frame |
| Distribution | p50/p95/p99 (nearest rank, same definition as G1), mean, min, max, stddev; FPS histogram (≥240, 144-240, 120-144, 90-120, 60-90, 30-60, 20-30, 10-20, <10); per-frame series in the JSON |
| Determinism | Frame-based camera paths (u = measured frame / measured frames) and scene time; fixed seeds (the G1 fixtures); no randomness in the harness |
| Warm-up | At least the suite's warm-up frames **and** 2 s (standard, quick, sweeps, `--scene`) so GPU clocks ramp after each scene load; warm-up frames are never measured |
| Order | Rounds interleave (round 1 runs all 39 configurations, then round 2, then 3), so thermal drift spreads over every configuration and shows as round-to-round spread |

## 4. Scene slots, bindings and camera paths

`--bind A=…,B=…` maps the D-078 QM8 slots to concrete scenes (a registered scene, `--list`, or `crowd:N`). The
defaults are G1 stand-ins:

| Slot (QM8) | Default binding | Camera path | What the stand-in lacks against QM8 |
|---|---|---|---|
| A soldier close-up | `units-closeup` | orbit of the scene's first shot, ±26°, 10 s of animation | G1 low-poly procedural soldiers; no faces, no authored asset |
| B formation of 1,000 | `crowd:1000` (G1's synthetic battle line, the class behind `units-bench-1k`) | ~65 m from the front-line centre, ±34° | low-poly soldiers on flat ground |
| C large battlefield | `battlefield` | orbit of the 230 m tactical shot, ±20° | 220 fixture soldiers, not thousands; no structures or effects |
| D cinematic | `battlefield-cine` | the cinematic planner's own shot sequence, played over the run | parametric avatars, placeholder faces and dress |
| E campaign environment | `terrain-temperate` | orbit of the 200 m-high day shot, ±34° | **no settlement exists in G1** |
| crowd sweep | slot B's binding with N soldiers | the scene's whole-army "wide" shot, ±9° | as B |

## 5. Suites

- **standard** (`run-benchmark.bat` default): 39 configurations × 3 rounds; 90 warm-up frames (≥ 2 s) + 600 measured.
  - resolution, 15: A-E × 1280x720, 1920x1080, 2560x1440 (LOD ×1, default shadows; 1920x1080 is the baseline)
  - LOD distance, 8: B and C × 0.25, 0.5, 2, 4 at 1920x1080 (scales soldier LOD 40/120 m, terrain chunk LOD 300/700/6000 m, prop LOD/cull distances and the prop shadow-cast distance)
  - shadows, 9: A, C and E × off, 1024, 4096 at 1920x1080 (G1's default is 2048)
  - crowd, 7: 500, 1,000, 2,000, 4,000, 8,000, 12,000, 16,000 soldiers at 1920x1080
  - JPEG captures in round 1: the five 1080p baselines, LOD C ×0.25 and ×4, crowd 16,000.
- **quick**: A-E at 1920x1080, 1 round, 60 warm-up (≥ 2 s) + 300 measured. **smoke**: A-E at 1280x720, 10 + 30.
- **sweeps** (`--sweep resolution|lod|shadows|crowd`, baseline included, 1 round) and **single scenes** (`--scene`).
- Exit codes: 0 finished, 1 error, 2 bad options, 3 refused (`--require-nvidia` off NVIDIA), 4 window closed early
  (results kept). A scene that fails to load or render is recorded as an error run and the suite continues.

## 6. Labels and flags

- **Victus evidence only on the RTX 4050.** `VALID_VICTUS_RTX4050` when GL_RENDERER names an NVIDIA RTX 4050;
  everything else is `INVALID_NOT_RTX4050` (kinds: software, Intel iGPU, other NVIDIA, other), in every file name,
  in a banner in the console, log and summary, and in the window title.
- **Per-run flags** (with a note each; anything not measurable is listed as *not evaluated*, never as passed):
  `ON_BATTERY`, `WINDOW_NOT_FOCUSED`, `THERMAL_OR_POWER_THROTTLE` (NVML power cap, HW slowdown, SW/HW thermal,
  power brake), `VSYNC_OR_FRAME_CAP_SUSPECTED` (interval within 3 % of a common refresh period, p10-p90 spread
  < 10 %, and > 20 % of the frame waiting beyond the app's CPU and GPU work), `GPU_TIMER_FRAMES_DROPPED`,
  `GEN2_GC_DURING_MEASUREMENT`; also `GPU_TIMERS_UNAVAILABLE`, `GPU_TIMER_NON_MONOTONIC`, `SOFTWARE_RENDERER`,
  `INTEL_IGPU`, `NOT_RTX4050`, `DEBUG_BUILD`.

## 7. Output

`results/perf-<yyyyMMdd-HHmmss>-<LABEL>.csv` (one row per run, 94 columns, appended as each run ends, so a crash keeps
the finished runs), `.json` (`civsim-perf/1`: machine, GL strings and extensions, NVML, suite, bindings, per-run
distributions, passes, sections, counters, memory, VRAM, census, utilization, power, flags, per-frame series, the
round-median aggregate, session notes) and `-summary.txt` (banner, machine, median-of-rounds table, flags);
`results/captures/<config>-<scene>-<LABEL>.jpg`; `--gpu-selftest --out PREFIX` writes `PREFIX.txt/.json/.jpg`.
`scripts/compare-perf.py BEFORE AFTER` lines two result files up by configuration values.

## 8. Optimus: launcher and GPU preference

- `CivSimBench.exe` (5,120 bytes; mingw-w64 GCC 13.2, `-nostdlib`; imports `KERNEL32.dll` and `SHELL32.dll` only;
  console subsystem; reproducible bytes, sha256 `6ab9bd52…5eda14`) exports `NvOptimusEnablement = 1` and
  `AmdPowerXpressRequestHighPerformance = 1`, finds `hostfxr.dll` and `Gfx.Demo.dll` next to itself and calls
  `hostfxr_main_startupinfo` (the entry the .NET apphost itself uses), passing every argument through.
- The harness records which host it runs in and whether the process image exports the two symbols
  (`GetProcAddress(GetModuleHandle(NULL), …)`), so every result file says whether the launcher was used.
- `Set-GPU-Preference-HighPerformance.cmd` / `Undo-GPU-Preference.cmd` write and remove the per-user Windows
  graphics preference (`HKCU\Software\Microsoft\DirectX\UserGpuPreferences`, `GpuPreference=2;`) for both
  executables; no administrator rights, nothing else touched. README step 3 gives the NVIDIA Control Panel route.
- **Not verified:** that the NVIDIA driver on the Victus honours the export or the preference for this OpenGL
  program. That is exactly what GPU checks A and B in `run-benchmark.bat` measure on the Victus.

## 9. The Windows package

### 9.1 Contents and build

| | |
|---|---|
| File | `/tmp/claude-0/-home-user-civDemo/54b82655-610f-500e-a185-8fae64bdd108/scratchpad/m7gfx2/victus/g2-before-3cc3ce3.zip` |
| sha256 | `29b27d57dff1c72a0ba06e437cb899bae63fd32b370442994ea1f49ceb325521` |
| Size | 7,272,515 bytes zipped; 69 files, 17,099,970 bytes unpacked, in one folder `g2-before-3cc3ce3/` (plus `fixtures/`) |
| Source | commit `3cc3ce3` on `g2/perf` (stamped into every result file and the README); later commits on the branch touch only `reports/` and the prototype `README.md`, neither of which is packaged |
| Build | `scripts/package-victus.sh`: `dotnet publish Gfx.Demo -c Release -r win-x64 --self-contained`, .NET 10.0.14, `PublishTrimmed` with `TrimMode=partial` (only assemblies marked trimmable, i.e. the framework; MonoGame rooted because the harness reflects into it; all `Gfx.*` kept whole) |
| Application | `Gfx.Demo.exe` (apphost, 162 KB), `Gfx.Demo.dll` and the six `Gfx.*.dll` with their `.pdb` (line numbers in any crash report), `MonoGame.Framework.dll`, `SDL2.dll`, `NVorbis.dll`, `fixtures/battle-trace-combinedarms-s7.json` |
| Launcher | `CivSimBench.exe`, 5,120 bytes, sha256 `6ab9bd52930fba04ec851519cd1a6a06f11f40ef90f419ac32b6594b255eda14` (reproducible) |
| Runtime | trimmed .NET 10 (`coreclr.dll`, `clrjit.dll`, `hostfxr.dll`, `hostpolicy.dll`, 33 framework assemblies) |
| Scripts and text | `run-benchmark.bat`, `Set-GPU-Preference-HighPerformance.cmd`, `Undo-GPU-Preference.cmd`, `README-VICTUS.txt`, `THIRD-PARTY-NOTICES.txt` (all CRLF), `PACKAGE-SHA256.txt` |
| Not packaged | `openal.dll` (MonoGame's audio backend: never loaded, PROVENANCE §3.7), `createdump.exe`, `mscordbi.dll`, `mscordaccore*.dll`, `Microsoft.DiaSymReader.Native.amd64.dll`, `msquic.dll`, `clrgc.dll`, `clrgcexp.dll` (debugger, dump, QUIC and standalone-GC support the benchmark never loads; the .NET host does not require native files listed in `deps.json`, shown by the Wine runs) |

### 9.2 What the Wine runs found and what was fixed

The package was run under Wine 9.0 (apt) with Xvfb and llvmpipe, from the unzipped archive:

| Run (Wine 9.0 `wine64` from apt, Xvfb, llvmpipe) | Result |
|---|---|
| `Gfx.Demo.exe` and `CivSimBench.exe` with `--perf-help`, a bad option, `--suite standard --list-configs` (package `ec828fe`, same hosting and options code) | exit 0 / 2 / 0; 39 configurations listed, so the trimmed build still discovers the scenes by reflection |
| `CivSimBench.exe --gpu-selftest` (inside the bat, final package) | exit 0; host "launcher (CivSimBench.exe)"; "NvOptimusEnablement exported, AmdPowerXpressRequestHighPerformance exported" (read back by the process from its own image); 11 effects compiled; slot A rendered with a HalfVector4 HDR target, 3,392 colours, GPU timer frames 24/24; JPEG written and looked at |
| `run-benchmark.bat smoke anyway`, final package | GPU checks A and B: exit 3, `[INVALID]`, the warning banner; the smoke suite ran all five slots (exit 0); dxdiag (Wine's) wrote `dxdiag.txt`; the zip step printed its fallback message because Wine has neither PowerShell nor `tar` |
| `CivSimBench.exe --suite standard --rounds 1 --warmup 1 --frames 3`, final package | all 39 configurations `ok`, exit 0, working set ≤ 669 MB; 115 framebuffers and 76 renderbuffers reclaimed (§9.3) |

Defects the Wine runs found, all fixed and re-run:
1. **Slot C could not load in the published package**: `fixtures/battle-trace-combinedarms-s7.json` was not in it.
   `Gfx.Demo.csproj` now copies it to build and publish output; test `BuildOutputCarriesTheBattleTraceFixture` pins it.
2. **The bat called llvmpipe `[VALID]`**: Wine's `cmd` ran `findstr … >nul 2>&1 && set …` unconditionally. The checks
   now use `--require-nvidia` (exit 3 off NVIDIA) and read the label on a separate line; no `&&`/`||` remains in the
   package scripts.
3. **An endless re-check loop**: Wine's `choice` returned an unexpected errorlevel, which the `if errorlevel` cascade
   read as "check again". Answers are now matched exactly, anything unexpected stops, and re-checks are capped at 5.
4. **Negative VRAM figures**: the NVX/ATI values were read as signed ints; now unsigned.

Not exercised under Wine: NVML, real Optimus switching, real power and battery state, `nvidia-smi` (Wine's `where`
stub reports success, so the bat wrote a "not recognized" line into `nvidia-smi-q.txt`; on Windows `where` fails
cleanly when the tool is absent), and the zip step.

### 9.3 MonoGame 3.8.5 render-target leak (found by the 117-run suite; worked around)

The first 117-run llvmpipe suite grew the process working set from 201 MB to 8,257 MB, about 70 MB per run, while
the census of live MonoGame resources for each configuration was identical in all three rounds and the GC heap
stayed under 26 MB. Cause, read from MonoGame's source at the pinned commit `4f9e3727`
(`GraphicsDevice.OpenGL.cs`): `PlatformCreateRenderTarget` creates a colour renderbuffer only for multisampled
targets, and `PlatformDeleteRenderTarget` does all of its clean-up inside `if (color != 0)`. Disposing an ordinary
`RenderTarget2D` therefore never deletes its depth/stencil renderbuffers or the framebuffer objects cached for it,
and because such a framebuffer still references the colour texture, GL keeps that storage too. The harness creates
a renderer (HDR and shadow targets) and an output target per run, so a full Victus run would accumulate GL memory
on the same scale (several GB; projected, not measured) and distort the later runs.

`RenderTargetReclaimer` (harness side; MonoGame and the renderer unchanged) deletes, after each run, the cached
framebuffers and the renderbuffers of every *disposed* render target, which is what MonoGame already does for MSAA
targets. Measured on llvmpipe, the same configuration 25 times: before, 201 → 1,321 MB, linear (46.6 MB per run);
after, 199 MB rising to a 293-341 MB plateau, with MonoGame's framebuffer cache at 3 entries after every run. The
second 117-run suite (§12.3) confirms it over all configurations. Every result file states how many objects were
reclaimed (`session_notes`). **The G1 engine has the same exposure** wherever it recreates render targets (for
example `ForwardRenderer.EnsureTargets` on a window resize); the engine stage should adopt the reclaimer or a
MonoGame fix.

### 9.4 What the Director does, and what comes back

`README-VICTUS.txt` (in the package) gives the steps: unzip to a short local path; charger in, OMEN Gaming Hub
Performance mode, other programs closed; GPU preference (script, NVIDIA Control Panel, MUX if offered); double-click
`run-benchmark.bat`; send back `victus-results-N.zip`. The bat runs GPU check A (`Gfx.Demo.exe --gpu-selftest
--require-nvidia`) and check B (`CivSimBench.exe …`), prints a large warning and asks R/Y/N when neither got the
RTX 4050, warns when on battery, runs the suite with whichever executable got the RTX 4050, then dxdiag and
`nvidia-smi -q`, and zips `results\` with `Compress-Archive` (or `tar`). Duration: an estimate of 15-40 minutes on
the RTX 4050 (117 runs, each a load, ≥ 2 s of warm-up and 600 measured frames); nobody has measured it.

## 10. Verification

- **Builds:** `dotnet build M7Gfx2.slnx --no-incremental`, Debug and Release: 0 warnings, 0 errors
  (TreatWarningsAsErrors on). The Windows publish adds no warning that fails the build (`NETSDK1074` is suppressed
  for the cross-OS apphost; trim-analysis warnings are suppressed because MonoGame and all `Gfx.*` are kept whole).
- **Tests:** 150 passed, 0 failed, 0 skipped in Debug and in Release (one host, no OOM): G1's 97 with unchanged
  per-class counts (Avatar 18, Camera 8, Cinematic 26, EffectLoad 4, EngineUnit 6, Frustum 5, Integration 2,
  Render 2, Terrain 15, Units 11) plus 53 new: `PerfUnitTests` 39 (suite shape, options, labels, percentiles,
  histogram, flags, CSV, camera paths, the fixture in the build output), `PerfDeviceTests` 9 (GPU timer, census,
  pixel identity of the tapped frame, a headless session and its files, warm-up time, the LOD-mix semantic pin,
  grid and trace hashes unchanged by a harness run with LOD ×4, the render-target reclaimer twice) and
  `PerfProcessTests` 5 (child processes: exit codes 0, 2, 3 and 4 with a real SDL_QUIT, and the GPU self-test).
- **Determinism and isolation:** `scripts/check-gfx-determinism.sh` OK; the harness never writes simulation state
  (test `HarnessRunLeavesTheAuthoritativeGridAndTraceUnchanged` hashes the battlefield grid and the battle trace
  before and after a run); G1 diff empty; renderer and scene code unchanged (§2).
- **Mutants** (one change each, Debug rebuild, the named test with a 300 s bound, about 60× the 5 s clean
  perf-test run; the file restored from git afterwards, `git status` clean):

| Mutant | Killed by (semantic test) | Time |
|---|---|---|
| M1 any NVIDIA GPU counts as Victus | `OnlyTheRtx4050IsVictusEvidence` | 10 s |
| M2 LOD multiplier not applied to crowds | `LodMultiplierShiftsTheCrowdLodMix` | 7 s |
| M3 GPU timestamps never harvested | `GpuTimerReturnsEveryFramesMarkersInOrder` | 7 s |
| M4 the tap skips the opaque pass | `TappedRenderIsPixelIdenticalToTheG1Renderer` | 7 s |
| M5 first measured interval kept | `HeadlessSessionMeasuresARunAndWritesConsistentFiles` | 7 s |
| M6 vsync heuristic ignores app idle time | `VsyncIsSuspectedOnlyWhenTheIntervalIsPinnedAndTheAppIsIdle` | 6 s |
| M7 warm-up ignores its minimum time | `WarmupLastsAtLeastItsMinimumTime` | 7 s |
| M8 a closed window exits 0 | `ClosingTheWindowEarlyExitsWithCode4AndKeepsResults` | 9 s |
| M9 the session never reclaims render targets | `RepeatedRunsLeaveNoStaleFramebuffers` | 8 s |
| M10 the reclaimer keeps stale cache entries | `ReclaimerDeletesWhatMonoGameLeaksForDisposedRenderTargets` | 6 s |

  All ten killed, none hung. Each asserted property is one the harness must have (only the RTX 4050 is Victus
  evidence; the LOD sweep changes LOD; GPU times are collected; the harness does not change the image; the first
  interval is excluded by design; the flag needs idle time; the warm-up honours its time; exit 4 is the contract;
  memory does not leak).
- **End to end:** §9.2 (Windows package under Wine) and §12 (llvmpipe suites: smoke, quick, and the 117-run
  structure, all exit 0).

## 11. Compatibility with the lost attempt's package (`g2-before-c86bc7f.zip`)

Only what the stage task records about the lost package is known; its source and configuration list did not survive.

**Kept:** the result layout and names (`results/perf-<time>-VALID_VICTUS_RTX4050.json/.csv/-summary.txt`,
`results/captures/*.jpg`, `results/selftest-plain-exe.*`, `results/selftest-launcher.*`, `results/bench-log.txt`,
`results/dxdiag.txt`, packed into `victus-results-<n>.zip`), the scene slots A-E, a standard suite of 39
configurations × 3, the exit codes, the six named flags, the launcher's two exports, the two GPU checks.

**Differences we know of:**
1. The 39 configurations are defined here (§5) and may not be the lost list. Every row carries its slot,
   resolution, LOD multiplier, shadow setting, crowd size and camera path, and `compare-perf.py` matches on those
   values, so the two result sets line up wherever they measured the same thing.
2. Frame counts (90 + 600, warm-up ≥ 2 s) and the camera paths are this stage's; the lost ones are unknown.
3. CSV columns and the JSON schema (`civsim-perf/1`) are this stage's; the lost ones are unknown, so the lost CSV
   can be compared only through columns with the same names.
4. The non-Victus label is `INVALID_NOT_RTX4050` (the lost package used an `INVALID_` prefix; its exact suffix is
   unknown).
5. Extra result file: `results/nvidia-smi-q.txt`. Extra package files: `THIRD-PARTY-NOTICES.txt`,
   `PACKAGE-SHA256.txt`, `fixtures/battle-trace-combinedarms-s7.json`.
6. Package: 7.3 MB zipped, 69 files, 17.1 MB unpacked (lost: 11.6 MB, 69 files). `openal.dll` and the runtime's debugger, dump, QUIC and
   standalone-GC files are not packaged (§9.1). Launcher: 5,120 bytes from mingw-w64 GCC (lost: 6.6 KB from
   clang/lld); in-process hosting through `hostfxr_main_startupinfo` (the lost method is unknown).
7. `run-benchmark.bat` takes an optional suite (`standard`, `quick`, `smoke`) and `anyway`; GPU checks use
   `--require-nvidia` (exit 3) plus the label.
8. The render-target leak workaround (§9.3) is new. Whether the lost harness recreated render targets per run, and
   so leaked, is unknown; if it did, the later runs of its standard suite ran under growing memory pressure, so its
   later rounds should be compared with care.

## 12. Costs measured here (llvmpipe, not Victus evidence)

Machine: 4-CPU Xeon @ 2.1 GHz container, llvmpipe (LLVM 20.1.2, 256 bits), Mesa 25.2.8, GL 4.5, .NET 10.0.14,
Release build. A simulation lane shares the machine; repeat runs differ by up to 6-16 % (§12.2).
**On llvmpipe the CPU executes the rasterization, so CPU, GPU and frame time are all about the same number, and the
per-pass GPU split is not meaningful** (llvmpipe runs a render target's commands in one deferred batch). The
structural counters (draws, triangles, instances, LOD counts, memory census) do not depend on the renderer.

### 12.1 Quick suite: the five slots at 1920x1080 (1 round, 60 warm-up frames and ≥ 2 s, 300 measured)

Result `perf-20261109-140017-INVALID_NOT_RTX4050` (scratchpad, not committed); session 244 s; device created in
128 ms; all 11 G1 effects compiled in 97 ms.

| Slot | Scene | Frame p50 / p95 / p99 ms | GPU p50 ms | CPU p50 ms | Load ms | First frame ms | Draws | Triangles | Instances | Soldiers LOD0/1/2/culled | App GPU MB | Working set peak MB |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| A | `units-closeup` | 52.4 / 62.1 / 68.8 | 51.9 | 52.2 | 45 | 109 | 25 | 46,300 | 263 | 36/0/0/28 | 68.5 | 230 |
| B | `crowd:1000` | 207.4 / 233.7 / 254.7 | 206.5 | 207.0 | 13 | 252 | 17 | 799,461 | 5,098 | 24/976/0/0 | 68.7 | 271 |
| C | `battlefield` | 136.4 / 163.1 / 186.5 | 135.9 | 136.1 | 1178 | 171 | 48 | 290,206 | 5,997 | 0/0/220/0 | 80.6 | 373 |
| D | `battlefield-cine` | 125.2 / 149.1 / 169.8 | 124.9 | 125.0 | 1150 | 135 | 67 | 457,416 | 9,897 | - | 80.7 | 424 |
| E | `terrain-temperate` | 134.2 / 152.0 / 162.9 | 132.9 | 133.2 | 885 | 153 | 53 | 386,882 | 9,729 | - | 79.8 | 423 |

Other per-run figures (p50): scene update 0.02-0.25 ms (D is highest: CPU skinning of the two avatars); first content
pass 2.9 ms (A) to 74.5 ms (B, which includes the 1,000-soldier `Prepare`); present 0.01 ms (offscreen EGL);
harness book-keeping 0.008 ms per frame; allocation 0.004-0.033 MB per frame; no gen-2 collection while measuring;
process CPU 215-334 % of one core (llvmpipe rasterizes on all 4 cores). Every frame is below 30 FPS here.

### 12.2 Smoke suite (1280x720, 10 + 30 frames): frame p50 ms

A 35.5, B 195.0, C 89.3, D 82.4, E 87.9 (llvmpipe). Noise floor of this machine, from `compare-perf.py` on two
runs of the same Release build about 15 minutes apart: smoke (30 frames) differs by up to 16 % per slot, quick
(300 frames) by up to 6 %.

### 12.3 The standard suite's 39 configurations × 3 rounds, with 10 measured frames per run

`--suite standard --warmup 2 --frames 10` (the full structure, short runs): 117 runs, all `ok`, exit 0, 238 s,
39 aggregate rows of 3 repetitions each, result `perf-20261109-140421-INVALID_NOT_RTX4050`. Frame times from 10
frames are rough (round spread up to 24 %); the counters are deterministic means over the sampled frames of the
camera path and do not depend on the renderer.

| Configuration | Frame p50 ms, median of 3 rounds | Round spread | Draws | Triangles | Instances | Soldiers LOD0/1/2/culled | Props drawn | App GPU MB |
|---|---|---|---|---|---|---|---|---|
| `res-A-1280x720` | 36 | 14 % | 25 | 46,109 | 261 | 36/0/0/28 | - | 50.9 |
| `res-A-1920x1080` | 56 | 24 % | 25 | 46,109 | 261 | 36/0/0/28 | - | 68.5 |
| `res-A-2560x1440` | 76 | 10 % | 25 | 46,109 | 261 | 36/0/0/28 | - | 93.1 |
| `res-B-1280x720` | 149 | 4 % | 17 | 799,516 | 5,098 | 24/976/0/0 | - | 51.1 |
| `res-B-1920x1080` | 171 | 22 % | 17 | 799,516 | 5,098 | 24/976/0/0 | - | 68.7 |
| `res-B-2560x1440` | 180 | 3 % | 17 | 799,516 | 5,098 | 24/976/0/0 | - | 93.3 |
| `res-C-1280x720` | 92 | 7 % | 48 | 289,948 | 5,992 | 0/0/220/0 | 1,402 | 63.0 |
| `res-C-1920x1080` | 142 | 8 % | 48 | 289,948 | 5,992 | 0/0/220/0 | 1,402 | 80.6 |
| `res-C-2560x1440` | 214 | 15 % | 48 | 289,948 | 5,992 | 0/0/220/0 | 1,402 | 105.2 |
| `res-D-1280x720` | 83 | 7 % | 66 | 447,860 | 9,596 | - | 2,723 | 63.1 |
| `res-D-1920x1080` | 122 | 16 % | 66 | 447,860 | 9,596 | - | 2,723 | 80.7 |
| `res-D-2560x1440` | 188 | 5 % | 66 | 447,860 | 9,596 | - | 2,723 | 105.3 |
| `res-E-1280x720` | 101 | 6 % | 53 | 388,978 | 9,754 | - | 7,957 | 62.2 |
| `res-E-1920x1080` | 144 | 15 % | 53 | 388,978 | 9,754 | - | 7,957 | 79.8 |
| `res-E-2560x1440` | 206 | 8 % | 53 | 388,978 | 9,754 | - | 7,957 | 104.4 |
| `lod-B-x0.25` | 59 | 23 % | 5 | 76,610 | 2,003 | 0/0/1000/0 | - | 68.6 |
| `lod-B-x0.5` | 104 | 23 % | 11 | 270,437 | 2,627 | 0/310/690/0 | - | 68.6 |
| `lod-B-x2` | 210 | 11 % | 23 | 1,079,695 | 7,348 | 672/328/0/0 | - | 68.8 |
| `lod-B-x4` | 220 | 9 % | 17 | 1,200,098 | 8,003 | 1000/0/0/0 | - | 68.7 |
| `lod-C-x0.25` | 95 | 12 % | 31 | 41,068 | 474 | 0/0/220/0 | 9 | 80.6 |
| `lod-C-x0.5` | 114 | 19 % | 35 | 74,676 | 1,122 | 0/0/220/0 | 656 | 80.6 |
| `lod-C-x2` | 189 | 7 % | 63 | 952,794 | 22,458 | 0/197/23/0 | 1,465 | 80.6 |
| `lod-C-x4` | 237 | 6 % | 73 | 2,354,175 | 30,466 | 0/220/0/0 | 1,465 | 80.6 |
| `shd-A-off` | 36 | 19 % | 14 | 25,360 | 132 | 36/0/0/28 | - | 36.5 |
| `shd-A-1024` | 48 | 12 % | 25 | 46,109 | 261 | 36/0/0/28 | - | 44.5 |
| `shd-A-4096` | 64 | 18 % | 25 | 46,109 | 261 | 36/0/0/28 | - | 164.5 |
| `shd-C-off` | 100 | 2 % | 21 | 93,354 | 1,632 | 0/0/220/0 | 1,402 | 48.6 |
| `shd-C-1024` | 132 | 10 % | 48 | 289,948 | 5,992 | 0/0/220/0 | 1,402 | 56.6 |
| `shd-C-4096` | 180 | 7 % | 48 | 289,948 | 5,992 | 0/0/220/0 | 1,402 | 176.6 |
| `shd-E-off` | 118 | 7 % | 27 | 290,816 | 7,974 | - | 7,957 | 47.8 |
| `shd-E-1024` | 146 | 7 % | 53 | 388,978 | 9,754 | - | 7,957 | 55.8 |
| `shd-E-4096` | 174 | 9 % | 53 | 388,978 | 9,754 | - | 7,957 | 175.8 |
| `crowd-500` | 99 | 3 % | 11 | 340,290 | 2,501 | 0/375/125/0 | - | 68.6 |
| `crowd-1000` | 146 | 12 % | 13 | 688,290 | 4,501 | 0/875/125/0 | - | 68.7 |
| `crowd-2000` | 140 | 3 % | 13 | 675,716 | 6,410 | 0/704/1296/0 | - | 68.8 |
| `crowd-4000` | 164 | 3 % | 15 | 843,632 | 9,732 | 0/833/3167/0 | - | 69.0 |
| `crowd-8000` | 120 | 8 % | 8 | 646,411 | 16,195 | 0/96/7904/0 | - | 69.5 |
| `crowd-12000` | 149 | 2 % | 9 | 937,492 | 24,218 | 0/108/11892/0 | - | 69.9 |
| `crowd-16000` | 174 | 10 % | 8 | 1,209,153 | 32,107 | 0/80/15893/28 | - | 70.4 |

What the structure shows (machine-independent):
- **Resolution** changes no geometry (same draws and triangles); only the render targets grow (app GPU memory
  51 → 69 → 93 MB for slot A).
- **LOD distance** is the strongest geometry lever: slot C goes from 41 k triangles (×0.25) to 2.35 M (×4), slot B
  from 77 k to 1.2 M; at ×4 every B soldier is LOD0.
- **Shadows** cost one extra pass of the casters (slot C: 93 k triangles and 21 draws without shadows, 290 k and 48
  with) and 128 MB of render target at 4096² (app GPU memory 48.6 → 176.6 MB).
- **Crowd size** at the whole-army camera mostly adds LOD2 proxies: 16,000 soldiers draw 1.21 M triangles in 8
  draw calls with 15,893 of them as proxies, and 8,000 draw fewer triangles than 4,000 because the camera backs off
  with the army's size. A close camera on a large army is not in the suite.
- **Memory over 117 runs** (after the §9.3 workaround): working set 200-760 MB, median per round 549, 570 and
  630 MB; MonoGame's framebuffer cache holds 2-4 entries after every run; the live-resource census of each
  configuration is identical in all three rounds. A residual rise of about 1 MB per run remains unexplained (native,
  not GC heap).

### 12.4 Harness costs

Per frame, the harness adds 7-9 GL timestamp queries, two stopwatch readings per content call and its book-keeping
(0.008 ms p50 in §12.1); every 60 frames a working-set and GC reading, every 120 frames one `glGetIntegerv` for VRAM;
NVML is read on its own thread. Per run it adds a forced GC and one census before the warm-up, and one `glFinish`
after the measured frames. None of this runs inside a measured frame except the per-frame part.

## 13. Captures and before/after

**Before = after for this stage, by design.** The perf stage changes no rendering, and that was measured twice:
- 21 `--capture` shots of five scenes (`units-closeup`, `units-bench-1k`, `battlefield`, `battlefield-cine`,
  `terrain-temperate`, 1280x720), rendered by pristine G1 (built from `git archive 1601c74`) and by this branch:
  **0 differing pixels in all 21** (llvmpipe; PNGs in the scratchpad, `captures/perf/beforeafter-*`).
- In-process, the frame drawn through the harness's `ContentTap` equals the frame drawn by the G1 renderer
  directly, pixel for pixel (test `TappedRenderIsPixelIdenticalToTheG1Renderer`).

The BEFORE images the later G2 stages will be compared against are the five scene slots as the harness renders them
(quick suite, 1920x1080, u = 0.5 of each camera path), committed as 960x540 review JPEGs (316 KB in total):

| Slot | Review JPEG (`reports/captures/perf/`) | What it shows (legend: llvmpipe, not Victus evidence; no external or PROTOTYPE-ONLY asset in any frame) |
|---|---|---|
| A | `before-slotA-units-closeup-1080p-llvmpipe.jpg` | G1 kit × clip lineup: low-poly soldiers with simple helmets, round and rectangular shields and spears, no faces |
| B | `before-slotB-crowd-1000-1080p-llvmpipe.jpg` | 1,000 soldiers in four blocks on flat ground, seen from ~65 m (24 LOD0, 976 LOD1) |
| C | `before-slotC-battlefield-1080p-llvmpipe.jpg` | the procedural battlefield from 230 m: the 220 fixture soldiers are specks; scatter trees and rocks |
| D | `before-slotD-battlefield-cine-1080p-llvmpipe.jpg` | the surrender cinematic: two parametric avatars in white robes, box-shaped set trees |
| E | `before-slotE-terrain-temperate-1080p-llvmpipe.jpg` | terrain, forests, river and road from 200 m up; no settlement exists |

All captures are in `/tmp/claude-0/-home-user-civDemo/54b82655-610f-500e-a185-8fae64bdd108/scratchpad/m7gfx2/captures/perf/`
(67 files; `README.txt` there): the quick and smoke slots, the eight round-1 captures of the 117-run suite, the Wine
runs, and the before/after PNG sets. Looked at: the slot captures, LOD C ×0.25 and ×4, crowd 16,000 and the Wine
self-test frame. They show what the numbers measure: at LOD ×0.25 the forest's trees and most scatter are culled at
the 230 m tactical distance (the dark forest-floor splat stays), at ×4 the 220 soldiers change from proxies to LOD1,
and the 16,000-soldier army is 64 blocks of mostly proxy soldiers.

## 14. What still looks placeholder

Everything this harness measures is G1 placeholder art, unchanged: low-poly procedural soldiers without faces,
flat-coloured cloth and equipment boxes; parametric avatars with simple heads and white robes; box-shaped "trees"
in the cinematic set; procedural terrain with low-poly scatter trees and rocks and no authored materials; no
settlement, no fortification, no structures, no weather. Nothing in this stage is production quality, and nothing
here should be read as a judgement of the quality the G2 later stages will reach.

## 15. Limitations and what was not done

1. **No real GPU has run any of this.** Not run on the Victus, on Windows hardware, or on any NVIDIA or Intel GPU.
   Untested paths: NVML, `GL_NVX_gpu_memory_info` on NVIDIA, the effect of the Optimus export and of the Windows
   graphics preference on the NVIDIA driver, the battery and power-mode readings on a laptop, `nvidia-smi`, the real
   dxdiag, and the `Compress-Archive`/`tar` zip step (Wine has neither). The code for each is defensive: anything
   that cannot be read is reported as unavailable or *not evaluated*.
2. **llvmpipe numbers are not GPU numbers** (§12): CPU ≈ GPU ≈ frame time, and the per-pass GPU split is an
   artefact of llvmpipe's deferred rasterization.
3. **GPU frame time is an upper bound when the CPU is the bottleneck** (the timestamp span includes GPU idle time).
4. **Animation CPU for G1 crowds** sits in `cpu_first_pass_ms`, not in `anim_cpu_ms` (§3); G1 code has no named
   segments, so passes cannot be split further without instrumenting later code with `PerfMarkers`.
5. **VRAM** from `GL_NVX_gpu_memory_info` is device-wide (all processes); the app census is an estimate (driver
   padding, alignment and internal copies are invisible).
6. **Presentation cost** is that of a 960x540 windowed preview; fullscreen presentation of a 1080p/1440p frame (and
   the Optimus copy of a full-size frame) is not measured. The measured resolution is the offscreen target.
7. **Flags are heuristics where they say so:** `VSYNC_OR_FRAME_CAP_SUSPECTED` can miss a cap that coincides with
   the app's own frame time, or flag a blocking present; the notes give the numbers behind each flag.
8. **Thermal state:** no cool-down pauses between runs; interleaved rounds and the NVML clock-event reasons make
   drift visible instead of hiding it.
9. **The lost package's configuration list, frame counts and CSV columns are unknown** (§11); comparisons with it
   go through configuration values and same-named columns only.
10. **MonoGame 3.8.5 render-target leak:** worked around in the harness only (§9.3); the engine still has it.
11. **`openal.dll` is not packaged.** A later stage that adds audio must package it again, with its LGPL notice.
12. **Unsigned executables:** Windows SmartScreen may warn (README says how to proceed). No code signing.
13. **Run time on the Victus is an estimate** (15-40 minutes); nobody has measured it.
14. **Slot stand-ins are weak against QM8:** C has 220 soldiers, not thousands; E has no settlement; A and D are
    placeholder characters (§4, §14). The harness measures whatever the slots are bound to; the later stages must
    bind real scenes before AFTER numbers mean anything for the quality targets.

## 16. How to run

```
export DOTNET_CLI_USE_MSBUILD_SERVER=0 SDL_VIDEODRIVER=offscreen LIBGL_ALWAYS_SOFTWARE=1
cd prototypes/m7-gfx2
nice -n 19 dotnet build M7Gfx2.slnx -c Release -m:1 -nodeReuse:false
dotnet Gfx.Demo/bin/Release/net10.0/Gfx.Demo.dll --perf-help
dotnet Gfx.Demo/bin/Release/net10.0/Gfx.Demo.dll --suite smoke --out /tmp/res --log /tmp/res/bench-log.txt
dotnet Gfx.Demo/bin/Release/net10.0/Gfx.Demo.dll --suite standard --list-configs
dotnet Gfx.Demo/bin/Release/net10.0/Gfx.Demo.dll --gpu-selftest --out /tmp/res/selftest-plain-exe
dotnet Gfx.Demo/bin/Release/net10.0/Gfx.Demo.dll --scene C --size 2560x1440 --lod 2 --shadows 4096 --out /tmp/res
scripts/build-launcher.sh                     # CivSimBench.exe (mingw-w64)
scripts/package-victus.sh                     # Windows package from the committed tree -> scratchpad victus/
python3 -I scripts/compare-perf.py BEFORE.json AFTER.json --metric frame_ms_p50
```

Later G2 stages measure their AFTER state with the same package script, binding the slots to their scenes
(`--bind`, or new defaults in `SlotBindings.Defaults`), and compare with `compare-perf.py`.
