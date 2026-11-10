# prototypes/m7-gfx2: M7 graphics prototype G2, the quality demonstrator

A bounded, Director-authorized graphics prototype (D-078; D-079 DR43–DR50; D-077). Its job is to move the M7 battle
graphics from placeholder art to the quality floor set in D-078 QM2, by DR49's priority order:
1. a real Victus benchmark;
2. one production-quality authored asset, from source to rendering, with provenance;
3. the soldier and avatar demonstrator;
4. one detailed battlefield environment;
5. later: battle on the terrain grid, aftermath cinematics, weather.

It is a copy-forward of G1 (`prototypes/m7-gfx1`). It is **not** in `Sim.slnx`, not in CI and not in any scan
script. It has no `Sim.Ui` reference, and its `Directory.Build.props` does not import the root one. To revert,
delete this folder. It does not decide O-14 or the project boundaries.

**State at setup (2026-11-09):** the code is G1's, unchanged except the solution name. Everything it renders is
still G1's **placeholder-grade** art: procedural low-poly soldiers, boxy props, procedural terrain materials. The
external assets in `assets/` are pinned and fetched, but **no code uses them yet**.

## Rules that bind every G2 stage

- **Before/after:** G1 (`prototypes/m7-gfx1`, solution `M7Gfx.slnx`, reports in `prototypes/m7-gfx1/reports/`) is
  the BEFORE baseline and is **never modified**. `git diff --stat 1601c74 -- prototypes/m7-gfx1` must stay empty.
  G1 can re-render its own captures (build `M7Gfx.slnx`, then `Gfx.Demo --capture …`), so before/after pairs come
  from the same scenes and cameras.
- **Rendering never influences the simulation (DR42, DR50).** Presentation reads published state or traces only.
  Cosmetic animation never touches results, positions, health, RNG, timing or hashes. The avatar is purely visual
  (DR39). `Gfx.Proc/Authoritative` stays integer-only (`scripts/check-gfx-determinism.sh`).
- **No hardware claims from this container.** It has no GPU: llvmpipe software GL, via
  `SDL_VIDEODRIVER=offscreen LIBGL_ALWAYS_SOFTWARE=1`. Label every number measured here "llvmpipe, not Victus
  evidence" (D-078 QM9, DR48).
- **Say what is still placeholder.** Never call placeholder graphics production quality.
- **Originality (D-069; D-078 QM2 READING):** quality references are references only. No asset, character, art,
  likeness, level, UI or look from another game is used or recreated, and nothing player-facing names another game.
- **Assets:** never commit large binaries. Every external file is pinned in `assets/manifest.tsv` and has a licence
  status in `assets/PROVENANCE.md`. Any capture that shows a PROTOTYPE-ONLY asset says so in its legend.
- **No permanent projects** outside `prototypes/` (D-079 DR4–DR6).

## Layout

| Path | Content |
|---|---|
| `M7Gfx2.slnx`, `Directory.Build.props` | Solution and isolated build settings (net10.0, warnings as errors, invariant globalization, MonoGame 3.8.5 pinned) |
| `Gfx.Engine/` | MGFX-from-GLSL effects, forward renderer (shadow → HDR → ACES post), cameras, culling/LOD, capture, frame stats, `--sysinfo`, headless host, scene registry |
| `Gfx.Proc/` | `Authoritative/`: integer-only seeded battlefield grid (SHA-256 pinned). `Presentation/`: terrain mesh, splats, scatter, water, props. `Environment/` (stage env): ez-tree C# port, tree species, impostors and forest layout, ground cover, boulders, river water, structure kit and settlements, the `env-*` scenes |
| `Gfx.Units/` | Skeleton, procedural soldier meshes and variation, clip library baked to a bone texture, instanced GPU crowd, battle-trace playback |
| `Gfx.Avatar/`, `Gfx.Cinematics/` | Persistent avatar profile and era dress; outcome-driven cinematic planner and player |
| `Gfx.Chars/` (stage chars) | Character file reader (`.g2ch`), GPU skinning with a morph stream, character material shaders, LODs, sockets, the cosmetic animation layer (locomotion blend, layers, IK, look, hit reactions) |
| `tools/charbake/` (stage chars) | Python bake of identities, outfits, LODs and clip files from the MakeHuman CC0 core and the MPFB2 rig (`scripts/bake-chars.sh`) |
| `Gfx.Demo/` | Executable: `--sysinfo`, `--list`, `--capture`, `--bench`, `--interactive`, `--chars-costs`; the `chars-*` scenes; forwards the benchmark options to `Gfx.Perf` |
| `Gfx.Perf/` | Victus benchmark harness (perf stage): `--suite`, `--sweep`, `--scene`, `--gpu-selftest`; `launcher/` (CivSimBench.exe source), `package/` (Windows package files). See `reports/perf.md` |
| `Gfx.Tests/` | xunit; `[GfxFact]` device tests skip, not fail, when no GL device can be created |
| `fixtures/` | `battle-trace-combinedarms-s7.json`, the labelled battle-trace fixture from G1; `ez-rng-oracle.txt` and `ez-tree-js-reference.json`, the ez-tree port's reference outputs (stage env) |
| `scripts/check-gfx-determinism.sh` | Integer-only scan of `Gfx.Proc/Authoritative` |
| `scripts/fetch-assets.sh`, `scripts/pin-assets.sh`, `scripts/assets-common.sh` | Asset fetch, verify and pin tools (below) |
| `assets/manifest.tsv`, `assets/lists/` | sha256 pins for every external file |
| `assets/PROVENANCE.md` | Licence status and evidence for every external asset |
| `reports/` | G2 stage reports. Small JPEG captures for review go in `reports/captures/` (at most about 300 KB each and about 6 MB in total across G2) |

## Assets: fetch, verify, add

External files live **outside the repository**, under `$G2_ASSETS` (default `/home/user/g2w/assets`):
`raw/<source>/<upstream path>` for fetched files and `baked/` for bake outputs. `assets/.gitignore` guards against
an in-tree root.

```
prototypes/m7-gfx2/scripts/fetch-assets.sh              # idempotent: download what is missing, verify every sha256
prototypes/m7-gfx2/scripts/fetch-assets.sh --verify     # no network, no writes: re-hash everything (exit 3 if missing, 1 if mismatched)
prototypes/m7-gfx2/scripts/fetch-assets.sh --offline --from DIR   # no network: restore raw/ from a copied tree
prototypes/m7-gfx2/scripts/fetch-assets.sh --summary    # files, bytes and status per source
prototypes/m7-gfx2/scripts/fetch-assets.sh --only mh-core,cmu-bvh
```

- Downloads go only to allowlisted hosts (raw.githubusercontent.com, media.githubusercontent.com for Git-LFS
  content, files.pythonhosted.org, registry.npmjs.org), and only from URLs pinned to a full commit or an
  immutable package path.
- A clean, unfiltered run writes `raw/MANIFEST.sha256`, a stamp of the committed pins, which bake scripts can
  compare.
- At setup the manifest pins 1,532 files (276 MB) from 10 sources. A full fetch into an empty root, with
  `--jobs 8`, downloaded and verified all of them in 38 s here (2026-11-09).
- **Adding an asset:** run `scripts/pin-assets.sh --source ID --base <pinned URL/> --dest ID/ --role asset --spdx …
  --status SHIP-OK|PROTOTYPE-ONLY --author … PATH…`. It downloads, hashes and appends rows; it refuses unpinned
  URLs and changed pins. Then add the licence evidence to `assets/PROVENANCE.md` **before** any code uses the file.
- **Baking (later stages):** bake scripts go in `scripts/` (for example `bake-*.sh` or a small tool project inside
  this prototype). They read `raw/` and write `baked/`, never commit outputs, and record every transformation
  (resampling, channel packing, retargeting, simplification) in the PROVENANCE row of the asset they change. The
  MakeHuman 1,280-target `.npz` is one such bake product.

## Build, test, run, capture

Run `/home/user/civDemo/scripts/bootstrap.sh` if `dotnet` is missing. Headless GL in a fresh container also needs
the EGL loader: `apt-get install --no-install-recommends libegl1 libegl-mesa0`. Without it, SDL's offscreen driver
cannot create a context and the 12 `[GfxFact]` tests skip. The machine is shared with a simulation lane, so build
niced and single-node:

```
export DOTNET_CLI_USE_MSBUILD_SERVER=0 SDL_VIDEODRIVER=offscreen LIBGL_ALWAYS_SOFTWARE=1
cd prototypes/m7-gfx2
nice -n 19 dotnet build M7Gfx2.slnx -c Release -m:1 -nodeReuse:false
nice -n 19 dotnet test Gfx.Tests/Gfx.Tests.csproj -c Release --no-build
scripts/check-gfx-determinism.sh
dotnet Gfx.Demo/bin/Release/net10.0/Gfx.Demo.dll --sysinfo
dotnet Gfx.Demo/bin/Release/net10.0/Gfx.Demo.dll --list
dotnet Gfx.Demo/bin/Release/net10.0/Gfx.Demo.dll --capture <scene|all> --out DIR [--size 1280x720] [--no-shadows]
dotnet Gfx.Demo/bin/Release/net10.0/Gfx.Demo.dll --bench <scene> [--frames N] [--size WxH] [--no-shadows]
dotnet Gfx.Demo/bin/Release/net10.0/Gfx.Demo.dll --interactive [scene]      # needs a display
```

- **Test host memory:** the `Gfx.Tests` host can peak near 5 GB RSS. If it is OOM-killed, run the tests one class
  group at a time (`--filter FullyQualifiedName~Gfx.Tests.UnitsTests`) and say so in the report.
- **Captures:** full-size PNGs go to the scratchpad, never to the repository. Review copies are small JPEGs in
  `reports/captures/`. Each capture legend states the renderer (llvmpipe) and lists any PROTOTYPE-ONLY assets in
  the frame.
- **Setup baseline (2026-11-09, this container):**
  - Debug and Release builds: 0 warnings, 0 errors.
  - Tests: 97 passed, 0 failed, 0 skipped in each configuration. The per-class counts equal G1's (Avatar 18,
    Camera 8, Cinematic 26, EffectLoad 4, EngineUnit 6, Frustum 5, Integration 2, Render 2, Terrain 15, Units 11).
  - The determinism scan is clean.
  - `--sysinfo`: llvmpipe (LLVM 20.1.2), Mesa 25.2.8, GL 4.5. All numbers here are llvmpipe, not Victus evidence.

## Renderer core (stage render1)

The G2 frame (analytic atmosphere, far field, PBR with image-based lighting, cascaded shadows, photographic terrain,
bloom, auto exposure, tonemapper and LUT, FXAA, jittered stills) is described in `reports/render1.md`. Every part is a
switch: `Gfx.Demo --capture <scene> --set name=value[,name=value]`, or `--preset g1` for the G1 frame (list:
`Gfx.Demo --help`). The photographic terrain needs `scripts/bake-render1.sh`; without the bake, G1's procedural
materials are used (the load failure is kept in `TerrainScenery.MaterialsFailure`; nothing is printed). `scripts/capture-render1.sh` and `scripts/bench-render1.sh` reproduce the render1 captures and the
per-switch llvmpipe costs.

## Battlefield environment (stage env)

One detailed battlefield environment on the generated grid, presentation only (it reads the grid and the generation
trace and never writes them; a GPU test proves the grid hash unchanged). Code in `Gfx.Proc/Environment/`, scenes
`env-trees`, `env-temperate` and `env-arid` (`Gfx.Demo --list`), report `reports/env.md`.

- **Bake first:** `scripts/bake-env.sh` (bark, leaf cards, ground-cover atlas, water normals, structure materials into
  `$G2_ASSETS/baked/env`; it checks every raw input's sha256 against the manifest). The render1 bake is needed too.
- **Switches** (`--set`, or `env=0|1` for all of them; `--preset g1` turns them all off): `trees`, `impostors`,
  `lodfade`, `wind`, `groundcover`, `rocks`, `water`, `reflect`, `structures`, `forestfloor`. A switch off draws
  that part's G1 content; with all of them off the frame is bit-identical to the plain G1 terrain scenery, in the
  G2 frame and through the G1 preset (a GPU test).
- **Reproduce:** `scripts/capture-env.sh OUT_DIR` (deliverable stills, BEFORE frames, per-switch A/B),
  `scripts/review-sheets-env.py OUT_DIR reports/captures/env`, `scripts/bench-env.sh OUT_DIR` (per-switch llvmpipe
  costs; `Gfx.Demo --bench … --variants "g2;env=0;trees=0"` benches several switch sets on one loaded scene).
- **Review aid:** `G2_ENV_LOD="lod1At,impostorAt"` moves the tree LOD distances (for example `1,2` draws every tree as
  an impostor).

## Victus (reference machine)

The Victus benchmark (DR48, DR49 item 1) needs a Windows build of `Gfx.Demo` on the Director's HP Victus (RTX 4050
Laptop GPU), forced onto the discrete GPU. G1's plan (`prototypes/m7-gfx1/reports/g1-summary.md` §6, as amended by
its D-079 dated note) still applies. The build route is DELEGATED (D-079 D16). No measurement from this container
is evidence of Victus performance.

**Perf stage (2026-11-09):** the harness (`Gfx.Perf`), the discrete-GPU launcher and the Windows package script
exist: `scripts/package-victus.sh` builds `g2-before-<commit>.zip` (self-contained win-x64, `run-benchmark.bat`,
`README-VICTUS.txt`); `scripts/compare-perf.py` compares two result files. Details, the MonoGame 3.8.5
render-target leak it works around, and what is still unverified: `reports/perf.md`.

**Since the perf + render1 merge (`g2/trunk`, 2026-11-09):** the harness measures the G2 frame by default (the
scenes' own switches; there is no harness option for `--preset g1`). The shadow sweep sets the G2 cascade size as
well as G1's single map, each JSON run records `render_features`, the GPU casters span covers every cascade, and the
review capture re-primes auto exposure. Not yet adapted: `scripts/package-victus.sh` still defaults to the name
`g2-before` and `README-VICTUS.txt` still describes the BEFORE package (G1 renderer), and the package has no
`assets/` folder next to the executable (where `AssetRoot` looks), so on Windows the G2 frame would fall back to G1's
procedural ground materials (the HDRI is needed only for `ibl=hdri`). An AFTER package needs its own name, README
text and the render1 terrain bake (with its CC-BY notice).

**Since the chars + env merge (`g2/trunk`, 2026-11-10):** the harness's `ContentTap` forwards stage env's optional
passes (`ITransparentContent`, `IPlanarReflectionContent`), which the renderer finds on the content it is handed.
Before, a tapped env frame had no water at all (env scenes draw all water in the transparent pass) and no reflection
pass, while the review capture, drawn untapped, had both. The mirrored redraw and the water are reported as the
sections `seg:reflection` and `seg:transparent` (CPU and GPU), so `opaque` stays the main scene pass; the rest of the
reflection pass falls in `clear_sky` and the water in `post`. Content without these passes is measured exactly as
before (test `PerfEnvTapTests`). The suite's slots A–E still bind the G1 stand-ins; `--scene <name>` or
`--bind A=chars-hero,…` runs the new scenes, which also need their bakes (`$G2_ASSETS/baked/chars`, `…/env`) that
the Windows package does not carry yet.

## Characters (stage chars)

Soldiers, the force A commander and the player avatar (G1 PlayerFixture identity) built from the MakeHuman CC0 core
and the MPFB2 53-bone game-engine rig: `tools/charbake` (Python, numpy; `scripts/bake-chars.sh`, `--check` for a
byte-for-byte re-bake) writes identities, outfits, LODs and clip files to `$G2_ASSETS/baked/chars`; `Gfx.Chars` draws
and animates them (GPU skinning with a morph stream, material shaders, LODs, sockets, the cosmetic animation layer);
`Gfx.Demo --capture chars-hero|chars-eras|chars-forces|chars-lod|chars-anim` and `Gfx.Demo --chars-costs`
(`scripts/capture-chars.sh`); review sheets from `scripts/review-sheets-chars.py`. Details, captures, costs and what
still looks placeholder: `reports/chars.md`. The CMU clips are PROTOTYPE-ONLY and live in their own
`<identity>__clips.g2ch` files (assets/PROVENANCE.md §3.9).

Reproducing a chars still: the hair's hashed-alpha seed advances with every opaque pass of the scene's character
renderer (`CharRenderer.FrameSeed`), so a still is reproduced bit for bit only with the same preceding shots in the
same process. Capture whole scenes as `scripts/capture-chars.sh` does; `--shots face-34` alone draws other strand
patterns (measured at the chars + env merge: the 20 `chars-hero` stills of a whole-scene capture equal the stage's
own final captures byte for byte, `face-34` captured alone differs in its hair pixels only).
