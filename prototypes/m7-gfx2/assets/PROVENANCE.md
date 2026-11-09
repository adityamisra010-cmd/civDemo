# G2 asset provenance (`prototypes/m7-gfx2`)

Every externally sourced file the G2 demonstrator may use, with its licence status and the evidence for it.
Written at the G2 setup stage, 2026-11-09, from files fetched and inspected in this container.

- **Pins:** `assets/manifest.tsv` holds one row per file (URL at a fixed upstream commit, sha256, bytes, SPDX
  id, status, source, author, role). The 1,280 MakeHuman targets are one group row, with per-file sha256 values in
  `assets/lists/mh-targets.sha256`.
- **Files:** fetched into `$G2_ASSETS/raw/<source>/<upstream path>` (default `/home/user/g2w/assets/raw`). They are
  never committed. `scripts/fetch-assets.sh` fetches them, `--verify` re-hashes them, and `--summary` prints the
  totals below.
- **Totals (measured 2026-11-09):** 1,532 files, 275,968,449 bytes, from 10 sources. 1,454 files are SHIP-OK and
  78 are PROTOTYPE-ONLY. Counting renderable assets only (no tools, licence texts or index), 1,403 are SHIP-OK and
  73 are PROTOTYPE-ONLY. Every byte is an unmodified copy of the upstream file at the pinned commit.
- **Licence policy:** D-079 §5 Q8 (D-078 Q-QM3) is **OPEN**. Until the Director rules, this file applies the working
  default of the G2 brief: ship-clean first; PROTOTYPE-ONLY only where nothing clean exists.

## 1. Status rules used here

| Status | Rule |
|---|---|
| **SHIP-OK** | The licence is CC0-1.0, MIT, BSD or CC-BY, **and** the evidence is direct: a licence file in the same upstream repository at the pinned commit that names these files or their folder, a licence field or header inside the file, or a licence mark in the image itself. CC-BY and MIT items need the notices in §5 when shipped. |
| **PROTOTYPE-ONLY** | Usable in the prototype only. Applies when the file states a non-permissive licence, when the evidence is indirect or missing, or when the terms are custom. It is flagged here, in the manifest and **in the legend of every capture that shows it**. It must be swappable and is never presented as shippable. |
| **REJECTED** | Not fetched and not used (§7). |

Evidence and reference rows in the manifest (licence texts, READMEs, the CMU index) are never rendered. Their
status says whether that text can travel with a build. They are excluded from the asset counts in §3.

Two evidence strengths are named in §3: **DIRECT** (as defined above) and **INDIRECT** (the licence follows from a
statement elsewhere, for example a project-wide relicensing or an attribution line).

## 2. Sources

| Id | Upstream (pinned revision) | Fetched from | Licence evidence (quoted from the pinned files) |
|---|---|---|---|
| `mh-core` | `github.com/makehumancommunity/makehuman` @ `a8bc2d54ff0ac92e78ff71431b1023eda42bf482` (master, 2024-06-26) | raw.githubusercontent.com | `LICENSE.md` §C: the assets ("The base mesh and proxies", "Targets and modifiers", "Textures", "Clothes", "Poses and expressions") "have been released under CC0 1.0 Universal"; `LICENSE.ASSETS.md` is the CC0 1.0 legal text. Per-file headers: "This asset was explicitly released as CC0 in september 2020" (`base.obj`, all 1,280 `.target` files, the eye meshes, `brown.mhmat`, `skins/default.mhmat`). JSON fields `"license": "CC0"` (`default.mhskel`, `default_weights.mhw`, `face-poseunits.json`); `tpose.meta`: `license CC0`. |
| `mpfb2` | `github.com/makehumancommunity/mpfb2` @ `6ff1eb358f6c1959ced77e79e6dd2406d9a71f49` (master, 2026-10-24) | raw.githubusercontent.com | `LICENSE.md` §C: assets, including "Textures", "Rigs, poses and expressions" and "JSON data with mesh information", "have been released under CC0 1.0 Universal". `weights.*.json`: `"license": "CC0"`. MPFB2's code is GPLv3; no code is fetched. |
| `mhjs-data` | `github.com/makehuman-js/makehuman-data` @ `e5d1a42cb469025a6d2b3ecea3e0252e6feccc1f` (2017-06-17) | raw.githubusercontent.com | A third-party conversion of MakeHuman 1.1-era system assets: meshes as three.js JSON, textures downscaled to 512×512. `package.json`: `"license": "http://www.makehuman.org/license.php"`. Per file: the 18 skin textures and the hair textures `short01`–`short03` carry an in-image CC0 badge ("(0) PUBLICDOMAIN MAKEHUMAN.ORG", "(0) MakeHuman.org" or "(0) www.makehuman.org"; read from enlarged crops of every file). The proxy-mesh JSONs embed `"license": "AGPL3 (see also http://www.makehuman.org/doc/node/external_tools_license.html)"`, "(c) MakeHuman.org 2001-2015". The other textures carry no mark. The official CC0 copies of these system assets (`makehumancommunity/makehuman-assets`, the MakeHuman file server) were not reachable. |
| `cmu-bvh` | `github.com/una-dinosauria/cmu-mocap` @ `09a07f54f3bbb58797325f009282d0b2048a2871` (2015-12-08): the CMU Graphics Lab Motion Capture Database in Bruce Hahne's 2010 MotionBuilder-friendly BVH conversion | raw.githubusercontent.com | `READMEFIRST.txt` v1.1: "CMU places no restrictions on the use of the original dataset, and I (Bruce) place no additional restrictions on the use of this particular BVH conversion." It quotes CMU: "This data is free for use in research and commercial projects worldwide." CMU asks for the acknowledgement "The data used in this project was obtained from mocap.cs.cmu.edu. The database was created with funding from NSF EIA-0196217." No SPDX licence exists. CMU's own site, the authority on its current terms, is unreachable from this container. |
| `terrain3d` | `github.com/TokisanGames/Terrain3D` @ `852b6c9742bcfebab30d06857c547e2d8daa2f60` (main, 2026-11-02) | raw.githubusercontent.com | `project/demo/assets/textures/asset_licenses.txt`: "All ambientCG assets are provided under the Creative Commons CC0 1.0 Universal License." It lists Grass = ambientCG Ground037 and Cliff = Rock023. Repository `LICENSE.txt`: MIT, "Copyright (c) 2023-2026 Cory Petkovsek, Roope Palmroos, and Contributors." The files are Terrain3D's channel-packed versions of the ambientCG maps. |
| `babylon-assets` | `github.com/BabylonJS/Assets` @ `3798844710bb7e76199ee38b175176c0fe4e9e62` (master, 2026-11-04) | raw.githubusercontent.com | `README.md`: "This work is licensed under a Creative Commons Attribution 4.0 International License (Unless specified otherwise in the asset folder)"; `LICENSE` is the CC BY 4.0 legal code. `textures/` has no licence file of its own. `ibl/harties_cliff_view/LICENSE.md`: "The harties_cliff_view IBL has a CC0 License and is free to use. Downloaded from https://polyhaven.com/a/harties_cliff_view. Authors: Dimitrios Savva, Jarod Guest". |
| `drei-assets` | `github.com/pmndrs/drei-assets` @ `456060a26bbeb8fdf79326f224b6d99b8bcce736` (2023-01-04) | raw.githubusercontent.com | `README.md`: "HDRIs from HDRI Haven". The repository has no licence file. HDRI Haven (now Poly Haven) publishes its assets as CC0, but its site is unreachable from this container, so that statement was not read here. |
| `ez-tree` | `github.com/dgreenheck/ez-tree` @ `dcf309bd86bd521083d9c70f01f2de45fdc7c457` (main, 2026-07-16) | raw.githubusercontent.com; media.githubusercontent.com for Git-LFS textures | `LICENSE`: MIT, "Copyright (c) 2024 Daniel Greenheck". `src/app/public/textures/LICENSE.md`: the bark textures "are sourced from ambientcg.com and are released under the Creative Commons CC0 1.0 Universal license", with the eleven `BarkNNN` ids listed. "The leaf textures in `leaves/` … are licensed under the project's own license (see repository LICENSE)." `textures/ground/` and `models/` are not mentioned. |
| `smaa` | `github.com/iryoku/smaa` @ `71c806a838bdd7d517df19192a20f0c61b3ca29d` (master, 2013-11-06) | raw.githubusercontent.com | `LICENSE.txt`: MIT permission notice, "Copyright (C) 2013 Jorge Jimenez … Jose I. Echevarria … Belen Masia … Fernando Navarro … Diego Gutierrez". |
| `meshoptimizer` | `github.com/zeux/meshoptimizer` tag `v1.3` @ `9e1f07b159d3cb777f1c67ed31fc11fd117986f4` (2026-09-25) | raw.githubusercontent.com | `LICENSE.md`: MIT, "Copyright (c) 2016-2026 Arseny Kapoulkine". |

## 3. Assets

Paths are relative to `$G2_ASSETS/raw`. "Upstream transformation" is what the upstream did before our pinned copy.
The fetch itself transforms nothing. Bake-stage transformations will be added to this table when bake scripts exist.

### 3.1 Characters: MakeHuman hm08 core (official, CC0)

| Asset | Files | Licence | Status | Evidence | Upstream transformation; notes |
|---|---|---|---|---|---|
| hm08 base mesh | `mh-core/makehuman/data/3dobjs/base.obj` | CC0-1.0 | SHIP-OK | DIRECT (file header, LICENSE.md §C) | None. 19,158 vertices, 18,486 faces, 21,334 UVs and 172 groups, including the helper geometry (joint cubes, tights and skirt helpers) that a bake must strip. |
| Modelling targets (1,280) | group `mh-core/makehuman/data/targets/` (list `lists/mh-targets.sha256`, 127,152,857 bytes) | CC0-1.0 | SHIP-OK | DIRECT (a header in each of the 1,280 files, LICENSE.md §C) | None. Text offset lists over the base mesh. The 1,280-target `.npz` that the earlier attempt used is to be **baked** from these files, not downloaded. A pre-built `targets.npz` exists in the PyPI `makehuman` 1.3.2 wheel, which carries no author or licence metadata; it was not used (§7). |
| Modifier definitions | `mh-core/makehuman/data/modifiers/*` (10 files) | CC0-1.0 | SHIP-OK | DIRECT (LICENSE.md §C, "Targets and modifiers") | None. Macro (gender, age, muscle, weight, height, proportions) and regional modifier → target mapping. |
| Default skeleton and weights | `mh-core/makehuman/data/rigs/default.mhskel`, `default_weights.mhw` | CC0-1.0 | SHIP-OK | DIRECT (`"license": "CC0"` in both) | None. 163 bones with facial bones. |
| Face and body pose units | `mh-core/makehuman/data/poseunits/face-poseunits.{bvh,json}`, `body-poseunits.json` | CC0-1.0 | SHIP-OK | DIRECT (`"license": "CC0"` in `face-poseunits.json`; LICENSE.md §C for the others) | None. The basis for facial expressions on the default skeleton. MakeHuman's expression presets (`.mhpose`) are system assets and were not reachable. |
| Eye meshes and the brown iris texture | `mh-core/makehuman/data/eyes/{high-poly,low-poly}/*`, `eyes/materials/brown.mhmat`, `brown_eye.png` (1024×1024) | CC0-1.0 | SHIP-OK | DIRECT (file headers) | None. The clean base for iris-colour variation by tinting. |
| Default skin material and T-pose | `mh-core/makehuman/data/skins/default.mhmat`, `poses/tpose.{bvh,meta}` | CC0-1.0 | SHIP-OK | DIRECT (header; `license CC0` in `tpose.meta`) | None. |

### 3.2 Characters: MPFB2 rigs and masks (official, CC0)

| Asset | Files | Licence | Status | Evidence | Upstream transformation; notes |
|---|---|---|---|---|---|
| Game-engine rig (53 bones) and weights | `mpfb2/src/mpfb/data/rigs/standard/{rig,weights}.game_engine.json`, `poses/game_engine_fk/t-pose.json` | CC0-1.0 | SHIP-OK | DIRECT (LICENSE.md §C; `"license": "CC0"` in the weights) | None. 53 bones (`Root`, `pelvis`, `spine_01`–`03`, limbs and three-joint fingers). |
| CMU-MB rig and weights | `…/rigs/standard/{rig,weights}.cmu_mb.json`, `poses/cmu_mb_fk/t-pose.json` | CC0-1.0 | SHIP-OK | DIRECT (as above) | None. Its joint names (`Hips`, `LHipJoint`, `LeftArm`, …) match the CMU BVH conversion, which eases retargeting. |
| Default rig in MPFB2 format | `…/rigs/standard/{rig,weights}.default.json`, `poses/default_fk/t-pose.json` | CC0-1.0 | SHIP-OK | DIRECT (as above) | None. |
| Mesh metadata | `mpfb2/src/mpfb/data/mesh_metadata/{basemesh_vertex_groups.json,hm08_config.json,hm08.mirror}` | CC0-1.0 | SHIP-OK | DIRECT (LICENSE.md §C, "JSON data with mesh information") | None. Vertex groups, helper ranges and the mirror table. |
| Skin region masks | `mpfb2/src/mpfb/data/textures/mpfb_*.jpg` (10, 2048×2048 grayscale), `sss.png` | CC0-1.0 | SHIP-OK | DIRECT (LICENSE.md §C, "Textures") | None. These are **masks** (face, lips, eyelids, ears, nails and others) for a procedural skin shader, not detail textures. |

### 3.3 Characters: MakeHuman 1.1-era system assets (2017 mirror)

| Asset | Files | Licence | Status | Evidence | Upstream transformation; notes |
|---|---|---|---|---|---|
| Skin albedo, 18 variants (young/middle-aged/old × three skin-tone families × male/female) | `mhjs-data/public/data/skins/*/textures/*_diffuse*.png` | CC0-1.0 | SHIP-OK | DIRECT (in-image CC0 badge in every file) | **Downscaled to 512×512 by the mirror.** That is far below production resolution, so these are base colour references, not final skins. The badge sits at the right edge of the atlas; a bake should confirm it lies outside the UV islands or clear it. |
| Hair textures `short01`–`short03` | `mhjs-data/public/data/proxies/hair/short0{1,2,3}/textures/*.png` | CC0-1.0 | SHIP-OK | DIRECT (in-image "(0) www.makehuman.org" badge) | 512×512. Usable for hair cards generated in code. |
| Iris colour variants (9) | `…/proxies/eyes/HighPolyEyes/textures/*_eye.png` | LicenseRef-MH2015-unlabelled | PROTOTYPE-ONLY | none per file; INDIRECT (MakeHuman's 2020 asset relicensing) | 512×512. Swap: tint the official `brown_eye.png` (§3.1). |
| Eyebrow textures (12) and eyelash textures (4) | `…/proxies/eyebrows/eyebrow0NN/textures/*.png`, `…/proxies/eyelashes/*/textures/*.png` | LicenseRef-MH2015-unlabelled | PROTOTYPE-ONLY | as above | 512×512 RGBA strand cards. Swap: brow and lash strands generated in code. |
| Teeth and tongue textures | `…/proxies/teeth/Teeth_Base/textures/teeth.png`, `…/proxies/tongue/tongue01/textures/tongue01_diffuse.png` | LicenseRef-MH2015-unlabelled | PROTOTYPE-ONLY | as above | 512×512. |
| Hair textures `short04`, full beard and moustache | `…/proxies/hair/{short04,Full_beard,Moustache}/textures/*.png` | LicenseRef-MH2015-unlabelled | PROTOTYPE-ONLY | as above; no badge found | 512×512. |
| Proxy meshes: eyebrow, eyelashes, teeth, tongue, hair (`short01`–`04`, full beard, moustache) | `…/proxies/{eyebrows/eyebrow001,eyelashes/Eyelashes01,teeth/Teeth_Base,tongue/tongue01,hair/*}/*.json` (10) | LicenseRef-MH2015-AGPL3-label | PROTOTYPE-ONLY | the file's own metadata says AGPL3 | three.js JSON 3.1 conversion of the 2015 `.mhclo`/`.obj` files, with fitting data (`ref_vIdxs`, `offsets`, `weights`). Swap: cards and shells generated in code over hm08. |

### 3.4 Animation: CMU motion capture (provisional)

All 20 clips are `cmu-bvh/data/<subject>/<clip>.bvh`, LicenseRef-CMU-Mocap, **PROTOTYPE-ONLY (provisional)**,
evidence DIRECT for the converter and quoted for CMU (§2). Upstream transformation (B. Hahne, 2010): converted from
ASF/AMC to BVH, a T-pose added as frame 0, joints renamed to MotionBuilder conventions, Frame Time written as
0.0083333 s (120 fps). Durations come from each file's header. CMU notes that its low-numbered subjects are early
sessions and may be of lower quality.

| Clip | CMU description | Frames | Duration | Intended use |
|---|---|---:|---:|---|
| 16_15 | walk | 472 | 3.9 s | locomotion |
| 35_01 | walk | 359 | 3.0 s | locomotion |
| 143_32 | Walk | 781 | 6.5 s | locomotion |
| 138_01 | Marching | 807 | 6.7 s | formation march |
| 20_06 | soldiers march (2 subjects, subject A) | 290 | 2.4 s | formation march |
| 16_35 | run/jog | 163 | 1.4 s | jog |
| 35_22 | run/jog | 168 | 1.4 s | jog |
| 143_01 | Run | 101 | 0.8 s | charge |
| 140_06 | Idle | 3563 | 29.7 s | idle |
| 140_07 | Idle | 1236 | 10.3 s | idle |
| 77_03 | ready stance | 985 | 8.2 s | guard idle |
| 23_03 | A sits, holds face in hands; B kneels, comforts A (subject B) | 813 | 6.8 s | kneel (surrender base) |
| 90_16 | fall on face | 725 | 6.0 s | death or fall forward |
| 90_18 | RugPullFall | 395 | 3.3 s | fall backward |
| 104_13 | StumbleWalk | 551 | 4.6 s | hit reaction |
| 139_19 | Walk Wounded Leg | 1001 | 8.3 s | wounded locomotion |
| 140_01 | Get Up Face Down | 831 | 6.9 s | get up |
| 140_08 | Get Up From Ground Laying on Back | 899 | 7.5 s | get up |
| 02_07 | swordplay (subject 2, an early session) | 2252 | 18.8 s | weapon-handling reference |
| 79_86 | shooting bow and arrow | 682 | 5.7 s | archer reference |

The CMU index (`cmu-bvh/cmu-mocap-index-text.txt`) and the converter's `READMEFIRST.txt` are pinned as reference and
evidence rows.

### 3.5 Environment: terrain, vegetation and sky

| Asset | Files | Licence | Status | Evidence | Upstream transformation; notes |
|---|---|---|---|---|---|
| Ground037 (grass meadow) PBR | `terrain3d/project/demo/assets/textures/ground037_{alb_ht,nrm_rgh}.png` (1024×1024 RGBA) | CC0-1.0 AND MIT | SHIP-OK | DIRECT (`asset_licenses.txt`; repo `LICENSE.txt`) | Channel-packed by Terrain3D: albedo RGB + height A, normal RGB + roughness A. Photo-sourced; looks good at 1K. Normal-map Y convention to be confirmed in the bake. |
| Rock023 (cliff) PBR | `terrain3d/…/rock023_{alb_ht,nrm_rgh}.png` (1024×1024 RGBA) | CC0-1.0 AND MIT | SHIP-OK | DIRECT (as above) | As above. |
| Rocky ground PBR | `babylon-assets/textures/rockyGround_{basecolor,metalRough,normal}.png` (1024×1024) | CC-BY-4.0 | SHIP-OK (attribution) | DIRECT (repo README and LICENSE) | None known. Gravel ground; the metal/rough packing is presumed to follow the glTF convention and must be confirmed in the bake. |
| Grass with normal | `babylon-assets/textures/grass.png`, `grassn.png` | CC-BY-4.0 | SHIP-OK (attribution) | DIRECT (as above) | Photo lawn grass. |
| Rock with normal | `babylon-assets/textures/rock.png`, `rockn.png` | CC-BY-4.0 | SHIP-OK (attribution) | DIRECT (as above) | `rock.png` looks like procedural noise: **reference-grade only, not recommended**. |
| Dirt, sand and ground albedo | `babylon-assets/textures/{dirt,sand,ground}.jpg` | CC-BY-4.0 | SHIP-OK (attribution) | DIRECT (as above) | Albedo only; normals and roughness to be derived in the bake. |
| Bark PBR sets (11: Bark001–004, 006–008, 012–015) | `ez-tree/src/app/public/textures/bark/BarkNNN_1K-JPG/*_{Color,NormalGL,Roughness}.jpg` (33 files) | CC0-1.0 | SHIP-OK | DIRECT (`textures/LICENSE.md`) | ambientCG 1K sets (1024×1024 or 512×1024); OpenGL-convention normals per the file names. |
| Leaf cards: ash, aspen, oak, pine | `ez-tree/src/app/public/textures/leaves/*.png` (1024×1024, palette PNG with transparency) | MIT | SHIP-OK | DIRECT (`textures/LICENSE.md`; repo LICENSE) | Photo branch cards with alpha. |
| ez-tree generator source | `ez-tree/src/lib/*.js`, `presets/index.js` (8) | MIT | SHIP-OK (tool) | DIRECT (repo LICENSE) | Reference for a C# port of procedural tree generation; never shipped as JavaScript. |
| ez-tree ground textures | `ez-tree/src/app/public/textures/ground/{dirt_color,dirt_normal,grass}.jpg` | LicenseRef-ez-tree-unstated | PROTOTYPE-ONLY | none (folder not covered by `textures/LICENSE.md`; the repo-wide MIT is presumed but the origin is unstated) | Photo soil and grass. |
| ez-tree demo models: grass clump, three flowers, three rocks | `ez-tree/src/app/public/models/*.glb` (7) | LicenseRef-ez-tree-unstated | PROTOTYPE-ONLY | as above | glTF binaries, 63–300 KB each; origin unstated. |
| Sky/IBL: Harties cliff view, 4K | `babylon-assets/ibl/harties_cliff_view/harties_cliff_view_4k.hdr` (4096×2048 RGBE) | CC0-1.0 | SHIP-OK | DIRECT (folder `LICENSE.md`) | Poly Haven HDRI. Usable as a visible backdrop as well as for lighting. |
| Sky/IBL: forest slope, Kiara dawn, Immenstadter Horn (1k) | `drei-assets/hdri/{forest_slope,kiara_1_dawn,immenstadter_horn}_1k.hdr` | LicenseRef-HDRIHaven-unverified | PROTOTYPE-ONLY | INDIRECT (README attribution only) | 1k (1024×512) panoramas: lighting only, too blurry as a backdrop. Promote to SHIP-OK once Poly Haven's CC0 statement for them is read directly. |

### 3.6 Rendering and bake tools

| Asset | Files | Licence | Status | Evidence | Notes |
|---|---|---|---|---|---|
| SMAA shader and lookup textures | `smaa/SMAA.hlsl`, `smaa/Textures/{AreaTex.h,SearchTex.h,AreaTexDX10.dds,SearchTex.dds}`, `smaa/Scripts/*.py` | MIT | SHIP-OK | DIRECT (`LICENSE.txt`) | The HLSL/GLSL source is a porting reference for the MGFX/GLSL 1.20 path; the area and search textures are runtime data. |
| meshoptimizer v1.3 sources | `meshoptimizer/src/*` (22 files) | MIT | SHIP-OK (tool) | DIRECT (`LICENSE.md`) | Bake-time tool only (LOD simplification, vertex-cache ordering); nothing from it ships. |

### 3.7 Perf stage (2026-11-09): no assets; runtime binaries in the Windows benchmark package, build and test tools

The perf stage uses **no art, model, texture, animation or sound asset**: every frame it measures is drawn by the
unchanged G1 code. It adds no row to `manifest.tsv`. What it does redistribute is the Windows benchmark package
(`scripts/package-victus.sh`, a private benchmark build for the Director, not a release). Its third-party binaries
are NuGet packages pinned by version and NuGet sha512; the package carries `THIRD-PARTY-NOTICES.txt`.

| Component | Files in the package | Licence | Status | Evidence | Notes |
|---|---|---|---|---|---|
| .NET runtime 10.0.14, win-x64 (`Microsoft.NETCore.App.Runtime.win-x64` sha512 `mLWGagTP…ybVweCN9A==`; apphost from `Microsoft.NETCore.App.Host.win-x64` 10.0.14) | `coreclr.dll`, `clrjit.dll`, `hostfxr.dll`, `hostpolicy.dll`, `System.*.dll`, `Microsoft.Win32.Registry.dll`, `netstandard.dll`, `clretwrc.dll`, `mscorrc.dll`, `System.IO.Compression.Native.dll`, `Gfx.Demo.exe` (apphost) | MIT | SHIP-OK (runtime) | DIRECT (NuGet licence expression MIT) | Framework assemblies trimmed by the SDK trimmer (`TrimMode=partial`: only assemblies marked trimmable; MonoGame rooted). Not packaged because the benchmark never loads them: `createdump.exe`, `mscordbi.dll`, `mscordaccore*.dll`, `Microsoft.DiaSymReader.Native.amd64.dll`, `msquic.dll`, `clrgc.dll`, `clrgcexp.dll`. |
| MonoGame.Framework.DesktopGL 3.8.5 (sha512 `hiHrjmNe…7LPTQ==`, commit `4f9e3727`) | `MonoGame.Framework.dll` | MS-PL | SHIP-OK (runtime) | DIRECT (nuspec `license` MS-PL) | Already G1's engine dependency; redistributed unmodified. |
| MonoGame.Library.SDL 2.32.10.2 (sha512 `a9GYQtVj…yqVeGw==`) | `SDL2.dll` | Zlib | SHIP-OK (runtime) | DIRECT (`LICENSE.txt` in the NuGet package: the SDL zlib licence, "Copyright (C) 1997-2025 Sam Lantinga") | Unmodified. |
| NVorbis 0.10.4 (sha512 `VvDbbWwK…kj3By6Q==`) | `NVorbis.dll` | MIT | SHIP-OK (runtime) | DIRECT (`LICENSE`: "MIT License, Copyright (c) 2020 Andrew Ward") | A MonoGame dependency listed in `Gfx.Demo.deps.json`, so it must be present; the benchmark never calls it. |
| MonoGame.Library.OpenAL 1.24.3.4 (OpenAL Soft) | none (**not packaged**) | upstream OpenAL Soft is LGPL; the NuGet package's `LICENSE` file quotes only BSD-3-Clause portions | not redistributed | INDIRECT, incomplete | Native audio backend, loaded only when audio is used. An `LD_DEBUG=libs` trace of `--gpu-selftest` (Linux) shows it is never loaded, and the Wine run of the package without it passed. |
| CivSimBench.exe (built here from `Gfx.Perf/launcher/civsimbench.c`) | `CivSimBench.exe` (5,120 bytes) | project code | own code | — | Compiled with mingw-w64 GCC 13.2.0 (apt `gcc-mingw-w64-x86-64` 13.2.0-6ubuntu1+26.1, a GPL-3.0 compiler used as a tool) with `-nostdlib`: no C runtime or libgcc code is linked, only the mingw-w64 import libraries for kernel32 and shell32 (apt `mingw-w64-x86-64-dev` 11.0.1-3build1). |
| NVML (NVIDIA Management Library) | none | NVIDIA driver component | not redistributed | — | Loaded at run time from the user's NVIDIA driver (`System32\nvml.dll`) when present; read only. |
| Wine 9.0 (apt `wine64` 9.0~repack-4build3) and Xvfb (apt `xvfb` 2:21.1.12-1ubuntu1.9) | none | LGPL-2.1-or-later (Wine), MIT/X11 (Xvfb) | test tools only | — | Used in this container to run the Windows package on software GL; nothing from them ships. |

## 4. Licence identifiers used for non-standard terms

| Id | Meaning |
|---|---|
| `LicenseRef-MH2015-AGPL3-label` | A MakeHuman 1.1-era (2015) proxy mesh whose embedded metadata states "AGPL3 (see also …external_tools_license.html)". MakeHuman relicensed its bundled assets to CC0 in 2020; whether that covers these 2015 copies is not verifiable from here, so the file's own label governs. |
| `LicenseRef-MH2015-unlabelled` | A MakeHuman 1.1-era texture from the 2017 mirror with no licence mark. It is probably CC0 under MakeHuman's 2020 relicensing, but no copy with direct evidence was reachable. |
| `LicenseRef-CMU-Mocap` | CMU Graphics Lab Motion Capture Database terms as quoted in §2. Custom terms: no SPDX licence and no explicit redistribution clause. Provisionally usable per the G2 brief, flagged. |
| `LicenseRef-HDRIHaven-unverified` | An HDRI attributed to HDRI Haven by a third-party repository with no licence file. |
| `LicenseRef-ez-tree-unstated` | A file in the MIT-licensed ez-tree repository whose folder the repository's texture attribution file does not cover; its origin is unstated. |

## 5. Notices required when SHIP-OK items ship

- **CC BY 4.0, Babylon.js Assets** (`babylon-assets/textures/*`): "Textures from the Babylon.js Assets repository
  (https://github.com/BabylonJS/Assets), licensed under CC BY 4.0 (https://creativecommons.org/licenses/by/4.0/)",
  plus a note of any changes made in the bake.
- **MIT notices:** Terrain3D (channel-packed ambientCG maps), ez-tree (leaf cards; generator code if ported),
  SMAA, and meshoptimizer if any of it ships (it is planned as a bake tool only).
- **CC0 items** need no notice. Courtesy credits: MakeHuman Team (Data Collection AB), ambientCG, Poly Haven
  (Dimitrios Savva, Jarod Guest).
- **CMU acknowledgement** (requested by CMU, if the clips are used): "The data used in this project was obtained
  from mocap.cs.cmu.edu. The database was created with funding from NSF EIA-0196217."

## 6. Changes from the earlier, lost G2 attempt's classification

The earlier attempt's lessons, as relayed in the G2 brief, were re-verified file by file. The differences:

1. **Skins and hair `short01`–`03`: confirmed CC0** by the badge in each image.
2. **Iris colours, brow, lash, teeth and tongue textures:** the brief listed them in the CC0 core. No mark was found in
   these files, and only the 2017 mirror copies are reachable, so they are **PROTOTYPE-ONLY** here. Clean
   replacements: the official CC0 `brown_eye.png`, tinted, and strand cards generated in code.
3. **ez-tree:** the repository has changed since the earlier attempt. Its bark textures are now eleven ambientCG
   CC0 sets, with a licence file that names them. The oak, willow, birch and pine bark the brief mentions no
   longer exist. The leaf cards are MIT. There is no grass or flower card texture any more. The grass and flowers
   are `.glb` models of unstated origin (PROTOTYPE-ONLY).
4. **Terrain3D:** the CC0 status is now **DIRECT** (`asset_licenses.txt`), where the brief had it as inferred.
5. **meshoptimizer:** taken from the official `v1.3` tag (with `LICENSE.md`), not from the PyPI sdist. The PyPI
   package only has alpha versions, and its sdist contains no licence file.
6. **HDRIs:** the 4K Harties panorama has a direct CC0 file (SHIP-OK). The three 1k drei panoramas are PROTOTYPE-ONLY
   until Poly Haven's statement can be read.
7. **MakeHuman `walks.mhanim`/`walk1.bvh`** (official repo) are labelled "license AGPL3" and target an old rig. They
   are **not pinned**.

## 7. Not used or rejected

| Candidate | Decision and reason |
|---|---|
| Ready Player Me avatars, LaFAN1, Sponza, BabylonJS `villagePack` | Rejected by the earlier attempt for unclear or non-commercial terms. Not re-fetched in this run. |
| PyPI `makehuman` 1.3.2 wheel (`sha256 384f0553…5b4f`, which contains `makehuman/data/targets.npz`) | Inspected, not used. The wheel metadata names no author, licence or project URL; the targets are pinned from the official repository instead. |
| PyPI `meshoptimizer` 0.2.30a0 sdist (`sha256 36b18b61…f5db`) | Inspected, not used. It is an alpha release, and the sdist has no licence file (§6.5). |
| MakeHuman system assets at full resolution (`makehumancommunity/makehuman-assets`, MakeHuman file server) | Not reachable. The GitHub repository asks for credentials (it is not public), and the file server is denied by the session's network policy. |
| CMU clip 111_28 "Standing still" | Not used: subject 111 is a pregnant-subject series, which does not suit a soldier idle. 140_06 and 140_07 are used instead. |
| xbpeng/DeepMimic motion files | Not used. Its motions are a mix of CMU and other data with no per-file terms. Not examined further. |

## 8. Network facts for this run (2026-11-09)

Reached: raw.githubusercontent.com, media.githubusercontent.com (Git LFS content), github.com over the git smart-HTTP
protocol (`git ls-remote` and blob-less partial clones, used only to list trees and resolve commits), pypi.org,
files.pythonhosted.org, registry.npmjs.org, api.nuget.org and the Ubuntu apt archive. codeload.github.com answered
**403** to an archive download (`zeux/meshoptimizer` at a commit), so no manifest row uses it. The hosts the brief
lists as denied (Poly Haven, ambientCG, Quaternius, OpenGameArt, the MakeHuman file server, Blender downloads, the
CMU site, jsdelivr, unpkg, huggingface, archive.org, sketchfab, the github.com web UI and API) were not contacted.

## 9. Gaps that the fetched set leaves open

- **No production-quality clothing, armour, helmets, weapons, shields or banners** were found from a clean,
  reachable source. The 2017 mirror's clothes are modern garments (suits, jeans, dresses, swimwear, a pith helmet
  and similar). They were not fetched, and their licences were not inspected. These items need authored geometry
  (built in code or bought or commissioned, D-078 Q-QM4) for DR45.
- **Skin resolution:** the only reachable skin albedo is 512×512. The demonstrator needs higher-resolution skin
  detail, for example procedural pore and normal detail driven by the MPFB2 region masks, or purchased or
  commissioned skins.
- **Combat animation:** CMU offers one early-session swordplay clip and one archery clip. Blocking, dodging, hit,
  death and surrender motions will need authored or procedural clips layered on the CMU base set.
- **Terrain resolution:** the clean terrain sets are 1K. Close-up shots will need detail mapping or procedural
  micro-detail.
