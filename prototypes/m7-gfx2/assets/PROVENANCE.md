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
- **Totals after stage chars (measured 2026-11-10 with `scripts/fetch-assets.sh --summary`):** 1,546 files,
  295,884,503 bytes, from 11 sources (the setup totals above plus the render1 reference rows and the 12 CMU clips of
  §3.4). The setup line above is kept as written.
- **Totals after the chars + env merge (measured 2026-11-10 with `scripts/fetch-assets.sh --summary` on the merged
  `g2/trunk`):** 1,561 files, 295,906,616 bytes, from 11 sources (the chars totals above plus the 15 ez-tree preset rows
  of §3.10, 22,113 bytes); 1,471 SHIP-OK and 90 PROTOTYPE-ONLY. `--verify` re-hashed all 1,561 files on disk: 0 missing,
  0 mismatched. The lines above are kept as written.
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
| `threejs-ref` | `github.com/mrdoob/three.js` tag `r180` @ `0af9729d0c143a86a1d725d6e2c3ad83301f3f34` (added by stage render1, 2026-11-09) | raw.githubusercontent.com | `LICENSE`: "The MIT License", "Copyright © 2010-2025 three.js authors". The tonemapping chunk is the reference for the G2 PBR Neutral, ACES (Hill fit) and AgX tonemappers (ported to GLSL 1.20 in `Gfx.Engine/Effects/PostEffects.cs`); the chunk itself cites its upstream sources (Khronos PBR Neutral, Stephen Hill's ACES fit, Filament/Blender AgX, Wrensch's minimal AgX fit). |
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

**Added by stage chars (2026-11-10, pinned in `manifest.tsv` with the same status and evidence):** 12 more clips from
the same conversion, for idles, march, guard, blocks and lunges. Descriptions from the pinned CMU index; frames and
durations from each file's header.

| Clip | CMU description | Frames | Duration | Intended use |
|---|---|---:|---:|---|
| 20_07 | soldiers march (2 subjects - subject A) | 244 | 2.0 s | march cycle (`march`) |
| 76_04 | defensive guard pose | 418 | 3.5 s | cower or guard (`cower`) |
| 77_02 | standing | 938 | 7.8 s | the default idle (`idle`) |
| 82_08 | stand still; casual walk forward | 1700 | 14.2 s | not used by the current bake |
| 113_21 | Standing Still | 1368 | 11.4 s | not used by the current bake |
| 137_28 | Normal Wait | 3723 | 31.0 s | not used by the current bake |
| 137_41 | Strong Man Wait | 5195 | 43.3 s | a second idle (`idle_proud`, frames 2160–3000) |
| 141_20 | Waiting | 764 | 6.4 s | not used by the current bake |
| 144_07 | Left_Blocks | 2449 | 20.4 s | not used by the current bake |
| 144_11 | Left_Lunges | 3430 | 28.6 s | not used by the current bake |
| 144_17 | Lunges | 2821 | 23.5 s | thrust (`thrust`, frames 708–948) |
| 144_26 | Right_Blocks | 3348 | 27.9 s | block (`block`, frames 1848–2016) |

The current bake (`tools/charbake/clips.py`) uses 16_15, 35_01, 20_07, 35_22, 143_01, 77_02, 137_41, 144_17, 02_07 (two
cuts), 144_26, 76_04, 23_03, 90_16, 90_18, 104_13, 139_19 and 79_86; the other pinned clips are unused. Their
retargets are written to separate `<identity>__clips.g2ch` files (§3.9), so no identity or outfit file contains CMU data.

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

### 3.8 Stage render1 (renderer core): files used, bake outputs and runtime transformations

Added 2026-11-09 by stage render1 as §3.7; renumbered §3.8 when `g2/perf` and `g2/render1` were merged into
`g2/trunk` (both stages had added a §3.7). `reports/render1.md`, written on its branch before the merge, cites this
section as §3.7. Every input below is an existing SHIP-OK row above; nothing PROTOTYPE-ONLY is
used by the renderer core, so no render1 capture needs a PROTOTYPE-ONLY legend (the G1 placeholder props, soldiers
and avatars that appear in the same frames are procedural code, not external assets).

**Bake** (`scripts/bake-render1.sh`, which verifies each input's sha256 against `assets/manifest.tsv` and then runs
`scripts/bake-render1-terrain.py`). Outputs go to `$G2_ASSETS/baked/render1/terrain/` and are never committed;
`MANIFEST.tsv` there records each output's sha256 and bytes, the numpy and Pillow versions, and `calibration.json` the
measured albedo means. Output container: `R1TX` (RGBA8, full mip chain; header documented in the script and in
`Gfx.Engine/Assets/AssetRoot.cs`). Measured run (2026-11-09): 10 files of 5,592,428 bytes each, 4 s.

| Set | Inputs (raw) | Transformations | Licence of the output | Status |
|---|---|---|---|---|
| grass | `terrain3d/…/ground037_alb_ht.png`, `ground037_nrm_rgh.png` | Albedo to linear light, scaled ×0.336 (mean linear luminance 0.312 → 0.105, a natural meadow albedo), back to sRGB. Height (alpha) and normal (OpenGL +Y, verified by height-gradient correlation +0.78) kept; roughness kept, floored at 0.62. Mips: box filter in linear light, mean height and mean normal, roughness widened by the Toksvig factor of the mean normal's length, renormalised | CC0-1.0 AND MIT (as the input) | SHIP-OK |
| gravel | `babylon-assets/textures/rockyGround_basecolor.png`, `rockyGround_normal.png` | Albedo ×0.579 (0.328 → 0.19). Height derived from albedo luminance (3×3 blur, 2–98 % stretch). Normal map Y flipped (DirectX −Y to OpenGL +Y; luminance-gradient correlation −0.42 before the flip). Roughness derived (0.80 + 0.14 × (1 − height)). The upstream `metalRough` map is not used (its blue channel is 1.0 everywhere, which would read as metal). Mips as above | CC-BY-4.0 (attribution, §5) | SHIP-OK (attribution) |
| rock | `terrain3d/…/rock023_alb_ht.png`, `rock023_nrm_rgh.png` | Albedo ×0.780 (0.269 → 0.21). Height and normal kept (OpenGL +Y, correlation +0.83). Roughness remapped from 0.557–0.788 to 0.72–0.97 so dry rock never mirrors the sky. Mips as above | CC0-1.0 AND MIT | SHIP-OK |
| sand | `babylon-assets/textures/sand.jpg` (894²) | Resized to 1024² with wrap (3×3 tile, Lanczos, centre crop: no border seam). Albedo ×0.809 (0.371 → 0.30). Height derived from luminance (two 3×3 blurs, stretch); normal derived from height (Sobel with wrap, strength 3, OpenGL +Y); roughness 0.90–0.98. Mips as above | CC-BY-4.0 (attribution) | SHIP-OK (attribution) |
| soil | `babylon-assets/textures/dirt.jpg` | Albedo ×0.644 (0.202 → 0.13). Height, normal (strength 4) and roughness (0.84–0.92) derived as for sand. Mips as above | CC-BY-4.0 (attribution) | SHIP-OK (attribution) |

**Runtime use of the CC0 HDRI** (`babylon-assets/ibl/harties_cliff_view/harties_cliff_view_4k.hdr`, §3.5, SHIP-OK),
only when a frame runs with `ibl=hdri`: decoded from RGBE, area-resampled to 1024×512, texels brighter than 16× the
99th-percentile luminance clamped (the sun; the engine's analytic sun light carries it, with shadows), turned about
the vertical axis so its brightest spot lies at the analytic sun's azimuth, scaled so its level-ground sky irradiance
equals the analytic sky's, then GGX-prefiltered into a 256×128 mip chain plus nine irradiance SH coefficients. Nothing
is written to disk.

**Algorithms implemented from publications** (no code copied, no asset): clear-sky single scattering with Earth
coefficients and the multiple-scattering approximation (Bruneton 2017; Hillaire 2020, "A Scalable and Production
Ready Sky and Atmosphere Rendering Technique"); GGX prefiltering with filtered importance sampling (Křivánek and
Colbert 2008); irradiance spherical harmonics (Ramamoorthi and Hanrahan 2001); the split-sum environment BRDF (Karis
2013) with height-correlated Smith visibility (Heitz 2014); specular occlusion (Lagarde and de Rousiers 2014);
Toksvig roughness; hex-tiling (Mikkelsen 2022, JCGT); the 13-tap bloom downsample and Karis average (Jimenez 2014);
FXAA's edge test, edge walk and sub-pixel blend (Lottes 2009); practical split scheme for cascades (Zhang et al.
2006). The tonemapper constants come from the `threejs-ref` row (MIT).

### 3.9 Stage chars (characters): files used, bake outputs and transformations

Added 2026-11-10 by stage chars. Every input is an existing row above; nothing new is downloaded except the 12 CMU
clips listed in §3.4. Bake: `scripts/bake-chars.sh` (re-hashes every source it reads with `scripts/fetch-assets.sh
--verify`, builds meshoptimizer, runs `tools/charbake/charbake.py`); outputs go to `$G2_ASSETS/baked/chars/` and are
never committed; `MANIFEST.tsv` there lists each output's sha256 and bytes. `--check` re-bakes in memory and compares
every output byte for byte.

| Output | Inputs (raw) | Transformations | Licence of the output | Status |
|---|---|---|---|---|
| Identity bodies (`<id>.g2ch` MESH body): 10 identities | `mh-core` base.obj, 1,280 targets (macro and regional modifiers used per identity), `mpfb2` `rig/weights.game_engine.json` | Base mesh morphed by the identity's macro (gender, age, muscle, weight, height solved for the stature, proportions, ancestry mix) and regional modifiers; converted to metres (ground at y = 0); helper geometry stripped (body group only); triangulated; split at UV seams; 4 joint influences per vertex from the MPFB2 weights, quantised to bytes summing to 255 | CC0-1.0 | SHIP-OK |
| Skeleton and sockets (SKEL, SOCK) | `mpfb2` game_engine rig (53 bones) | Joint positions from MPFB2's joint-cube strategy on each identity's morphed mesh; two eye bones added at the fitted eye centres; grip and shield sockets computed from the hand and forearm joints | CC0-1.0 | SHIP-OK |
| Expressions (MRPH: blink, battlecry, pain, grim, calm) | `mh-core` `face-poseunits.{bvh,json}`, `default.mhskel`, `default_weights.mhw` | Weighted pose units (choices in `expressions.py`) posed on the default skeleton; posed minus rest mesh = sparse per-vertex deltas (positions and normals); carried onto teeth, tongue, lashes, brows and beard through the skin vertex under each part | CC0-1.0 | SHIP-OK |
| Eyes (MESH eyes, cornea) and iris texture (`tex/<id>_eye.r1tx`) | `mh-core` high-poly eye proxy (mhclo, obj), `brown_eye.png` | Proxy fitted to the morphed face; split into eyeball and clear cornea shell by the texture's alpha; iris hue and value moved to the identity's eye colour | CC0-1.0 | SHIP-OK |
| Skin albedo and masks (`tex/<id>_skin.r1tx`, `_skinmask.r1tx`) | `mhjs-data` skins `young_caucasian_male`, `middleage_caucasian_male`, `old_caucasian_male`, `young_african_male`, `middleage_african_male`, `young_asian_male` (512², CC0 badge); `mpfb2` region masks (face, lips, eyelids, ears, inside-mouth) | Upscaled (Lanczos) to 1,024² (soldiers) or 2,048² (avatar, commander); tone-matched in linear light to the identity's skin tone (a READING of G1's display tones: chroma pulled 30 % to grey, scaled to 90 %); procedural mottling, capillary redness, cavity occlusion (alpha); painted hair: flow-aligned strand strokes rasterised in UV space under the scalp hair, beard and brows; the CC0 badge strip is checked to lie outside every UV island | CC0-1.0 | SHIP-OK |
| Hair, beards, brows, lashes, teeth, tongue | `mh-core` base mesh (scalp, face, lash and teeth helper geometry as surfaces only) | Generated in code: scalp hair and beards as fur shells over the identity's skin (flow-aligned UVs), a tied-back tail and long hair as cards grown on the scalp, brows as cards, lashes on the CC0 lash helpers, procedural dentition on the teeth helper bands. No MakeHuman proxy mesh (the AGPL-labelled 2015 ones, §3.3) is used | CC0-1.0 (inputs) + generated | SHIP-OK |
| Garments, armour, helmets, shields, weapons (`<id>__<outfit>.g2ch`) | the identity's own body (above) | Generated in code: shells of the skin cut by smooth fields with woven borders and hems (cut-out islands of under 150 skin vertices kept, so no pinholes), parametric skirts fitted to the body and to the legwear under them, shoes fitted to the feet, skull-fitted helmet bowls; the bronze cuirass and the crested helmet are shells of the torso and of the whole head whose edges are cut on a smoothed copy of the shell itself and faired; boot shafts worn over hose are the hose's own surface (the MakeHuman CC0 tights helper) 4 mm further out; lathe and extrusion shapes for weapons and shields; original geometric heraldry painted in code | generated | SHIP-OK |
| LODs (MESH lod1, lod2 in each outfit file) | the meshes above; `meshoptimizer` v1.3 sources (tool) | Inner garment layers under an outer layer dropped; per part `meshopt_simplifyWithAttributes` (positions and normals, absolute error 12 mm at LOD1 and 30 mm at LOD2, retried at 2.5x and 6x) to an area-weighted budget, `meshopt_simplifySloppy` (2 % of the extent) only for a part still at more than three times its budget; merged into one single-material mesh per identity × outfit (about 3,000 and 1,500 triangles); meshoptimizer is compiled into a shared library at bake time and never ships | CC0-1.0 (inputs) + generated | SHIP-OK |
| Procedural textures (`tex/fabric`, `wood`, `trim`, `hair_atlas`, `hair_lying`, `fur`, `skin_detail`, `emblem_*`) | none | Generated in code (`materials.py`, `hair.py`, `skin.py`) | generated | SHIP-OK |
| Clips (`<id>__clips.g2ch`, CLIP chunks) | `cmu-bvh` clips listed in §3.4 | Rotation transfer onto each identity's rig with a matched T-pose (limbs aligned to the source's frame 0, spine and neck keep the rig's rest posture), resampled to 30 fps, hips translation scaled by leg length, turned to travel along +Z; cycles cut between left foot strikes, made in place (average horizontal velocity removed and stored as the clip's natural speed), loop-corrected; one-shots grounded on their first 0.5 s; planted-foot bits from height and speed thresholds | LicenseRef-CMU-Mocap | PROTOTYPE-ONLY (provisional) |

**Runtime use:** characters are drawn with code-defined shaders (`Gfx.Chars/Rendering/CharEffects.cs`); the scenes'
ground is the render1 terrain bake (§3.8: grass CC0+MIT; gravel, sand and soil CC-BY-4.0). Every stage-chars capture
shows characters posed by CMU clips, so every legend names the CMU data as PROTOTYPE-ONLY.

### 3.10 Stage env (battlefield environment): files used, bake outputs and runtime transformations

Added 2026-11-10 by stage env on `g2/env` (cut from `g2/trunk` 808d88c) as §3.9; renumbered §3.10 when `g2/chars` and
`g2/env` were merged into `g2/trunk` (both stages had added a §3.9; chars was merged first and keeps §3.9).
`reports/env.md`, written on its branch before the merge, cites this section as §3.9. Every external input below is an
existing SHIP-OK row of §3.5 except the ez-tree presets, which this stage pinned (15 new rows in `manifest.tsv`).
**No PROTOTYPE-ONLY file is used by the environment**: the ez-tree ground textures and `.glb` grass, flower and rock
models (§3.5, PROTOTYPE-ONLY) are not read by any code or script. No env capture therefore needs a PROTOTYPE-ONLY
legend.

**Bake** (`scripts/bake-env.sh`, which verifies each of its 23 raw inputs' sha256 against `assets/manifest.tsv` and then
runs `scripts/bake-env.py`). Outputs go to `$G2_ASSETS/baked/env/` and are never committed. `MANIFEST.tsv` there
records each output's sha256 and bytes and the numpy and Pillow versions, and `calibration.json` the measured means.
Output container: `R1TX` (render1's, RGBA8 with mips). Measured run (2026-11-10, numpy 2.5.3, Pillow 12.3.0): 45 files,
301 MB on disk, 137 s; a second full run reproduced every sha256.

| Set | Inputs (raw) | Transformations | Licence of the output | Status |
|---|---|---|---|---|
| bark (6 sets: oak, ash, aspen, pine, shrub, scrub) | `ez-tree/src/app/public/textures/bark/Bark{001,004,007,014,002,012}_1K-JPG/*_{Color,NormalGL,Roughness}.jpg` (18 files) | Albedo to linear light and scaled to a natural mean linear luminance (Bark001 0.222 → 0.12, Bark004 0.292 → 0.15, Bark007 0.356 → 0.28, Bark014 0.149 → 0.11, Bark002 0.187 → 0.11, Bark012 0.208 → 0.13). Roughness remapped to [floor, 1] (floor 0.70–0.82). OpenGL normals kept. A cavity term derived from how far the normal map bends from flat (blurred, clamped 0.35–1) packed with the normal. Mips in linear light, mean normal renormalised, roughness widened by the Toksvig factor. The oak set is also the palisade and log material of the structures | CC0-1.0 | SHIP-OK |
| leaves (6 cards: oak, ash, aspen, pine, scrub, juniper) | `ez-tree/src/app/public/textures/leaves/{oak,ash,aspen,pine}.png` | Colour bled into the transparent texels (no dark fringes in mips). Albedo scaled to a target mean linear luminance of the opaque texels (oak 0.192 → 0.075, ash 0.170 → 0.080, aspen 0.482 → 0.085, pine 0.156 → 0.055). Recolours in HSV keeping the card's own hue spread at half strength: aspen autumn yellow → summer green (hue 0.235, saturation ×0.78); scrub = the ash card as dusty olive (hue 0.17, ×0.42, target 0.062); juniper = the pine card as blue-grey-green (hue 0.30, ×0.35, target 0.050). Translucency from normalised luminance (0.35–1), a mild normal from luminance. Mips keep the alpha-tested coverage of level 0 (per-level alpha scale) | MIT (notice §5) | SHIP-OK |
| ground-cover atlas | none: 16 cards drawn in code (four grasses, two dry grasses, sedge, two fern fronds, daisy, buttercup, knapweed, poppy, dock, thistle, cushion plant) | Drawn at 2× and box-filtered; colour, coverage, height and translucency layers; coverage-preserving alpha mips; 10 levels kept | project code | own |
| water normal map | none: a tileable sum of directional wave octaves drawn in code | — | project code | own |
| structure materials (wood, planks, daub, thatch, plaster, tiles: drawn; ashlar, rubble, paving: drawn block layouts over stone) | `terrain3d/project/demo/assets/textures/rock023_alb_ht.png` for the stone surface of ashlar, rubble and paving | Rock023 albedo resized (2048² for ashlar, 1024² otherwise) and offset per use. Ashlar: each block takes its own seeded patch, whose luminance modulates a warm limestone colour (the photo's own colour is not kept); drafted margins, mortar joints and chipped arrises drawn in code. Rubble: Voronoi fieldstones in mortar, a tone per stone, the photo's luminance as surface detail. Paving: rectangular slabs, the photo's luminance as surface detail. Height, normal (OpenGL +Y) and roughness derived; mips as for bark | CC0-1.0 AND MIT for the three stone sets (as the input), project code otherwise | SHIP-OK |

**Runtime reuse of render1 bakes:** the boulders use render1's `rock` set (Rock023, §3.8) triplanar, with a moss term
and a damp base drawn in the shader. The terrain keeps render1's sets; the environment only changes the splat
weights it draws with (forest floor from the canopy, trampled ground, grassy talus).

**Code ported or embedded:**

| Item | Source | Licence | Status | Transformation |
|---|---|---|---|---|
| ez-tree presets (15 JSON: ash, aspen, oak, pine × small/medium/large, bush_1–3) | `ez-tree/src/lib/presets/*.json` at the pinned commit (15 new manifest rows, role `tool`) | MIT | SHIP-OK | Embedded unmodified as resources of `Gfx.Proc` (`Environment/EzTree/presets/`); a test checks their sha256 against the manifest pins. The MIT notice travels as `presets/LICENSE-ez-tree.txt` |
| ez-tree generator | `ez-tree/src/lib/{rng,tree,branch,options,enums}.js` and the three.js math they call (the trellis growth of `trellis.js` is not ported: no preset enables it, and a preset that did would be refused) | MIT (ez-tree; three.js for the Euler/quaternion formulas) | SHIP-OK (port) | Ported to C# (`Gfx.Proc/Environment/EzTree/`): the RNG with JavaScript int32 semantics, the branch/leaf generator and its mesh output, in double precision, with additions for this game (two reduced detail levels, per-vertex extras for wind and occlusion). Verified three ways: an independent Python oracle (`scripts/ez-rng-oracle.py`, written from the ECMAScript rules) reproduces the RNG bit for bit (`fixtures/ez-rng-oracle.txt`, 1,920 lines, identical to the JavaScript original's output under Node); the generator's geometry matches the original run under Node with three r180 (all fifteen presets while porting, largest difference 3.7e-11 relative; committed check `fixtures/ez-tree-js-reference.json`: four presets, counts, index SHA-256 and every 97th vertex value within 1e-9) |

**Cross-check tools (not committed, not shipped):** Node.js v22.23.4 and the npm package `three` 0.180.0 (MIT), used
once in `/home/user/g2w/env-work/node` to run the original JavaScript generator and write the two fixtures.

**Algorithms implemented from publications or common practice** (no code copied, no asset): hemi-octahedral
impostors (after Ryan Brucks' octahedral impostors, 2018) with depth and normal atlases; screen-space dithered LOD
cross-fades with complementary masks (interleaved gradient noise, Jimenez 2014); coverage-preserving alpha mips
(Castaño 2010); flow-mapped water normals with two phases (Vlachos 2010); Beer–Lambert absorption; Schlick's Fresnel;
oblique near-plane clipping for the planar reflection (Lengyel 2005). One formula is taken as published: the
three-line sine-free value hash `envHash` (`EnvGlsl.Common`) is Dave Hoskins' "hash12" from his "Hash without Sine"
shader, which its author's header releases under the MIT licence (that page was not re-read from this container;
the formula is short enough to replace if the notice is not wanted).

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
  plus a note of any changes made in the bake (render1: the transformations in §3.8).
- **MIT notices:** Terrain3D (channel-packed ambientCG maps), ez-tree (leaf cards; the C# port of its generator and the
  embedded presets, stage env §3.10: `Gfx.Proc/Environment/EzTree/presets/LICENSE-ez-tree.txt`),
  SMAA, meshoptimizer if any of it ships (it is planned as a bake tool only), three.js (`threejs-ref`: the G2
  PBR Neutral, ACES and AgX tonemapper code is a port of its tonemapping chunk), and Dave Hoskins' "Hash without
  Sine" (the `envHash` formula of stage env, §3.10).
- **CC0 items** need no notice. Courtesy credits: MakeHuman Team (Data Collection AB), ambientCG, Poly Haven
  (Dimitrios Savva, Jarod Guest).
- **CMU acknowledgement** (requested by CMU, if the clips are used): "The data used in this project was obtained
  from mocap.cs.cmu.edu. The database was created with funding from NSF EIA-0196217." Stage chars uses the clips
  (§3.9) and writes this sentence into every `<identity>__clips.g2ch` INFO chunk.

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
