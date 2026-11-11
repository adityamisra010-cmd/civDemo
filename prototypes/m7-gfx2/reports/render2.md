# G2 stage render2: shadows and AO, crowds, culling, TAA

Stage agent render2, branch `g2/render2` (cut from `g2/trunk` 7dd134a), 2026-11-11. Scope: the renderer's second pass
under the D-078 graphics quality mandate (D-079 DR43–DR50, D-077), MonoGame 3D only, presentation only. All numbers
below were measured in this container on **llvmpipe (Mesa 25.2.8, LLVM 20.1.2, 256-bit, 4 CPUs): llvmpipe, not Victus
evidence**. No hardware GPU ran this code. Quality references were used as references only; nothing player-facing names
another game (D-069).

**Verdict, honestly.** Units now stand on the ground: GTAO darkens the sky light where feet, shields and bodies meet the
ground and each other, and contact shadows put a thin, sharp shadow under feet where the 2,048² cascades are too coarse;
with both switched off the frame is the trunk's, bit for bit. In the crowd scenes G1's procedural soldiers are replaced
by instanced, GPU-skinned stage-chars soldiers at 220, 2,032 and 10,084, with three levels of detail that cross-fade
without holes or pops and per-soldier variation. Frustum, occlusion and cascade-caster culling remove work without
changing a pixel in the shots compared, and TAA removes stair-stepping and hair noise in motion (softer than FXAA) while
stills keep their 16-sample accumulation. The before/after pairs are a clear step up. **It is not production quality:**
§7 lists what still looks placeholder (the nearest crowd soldiers are the faceted `lod1` mesh, a formation moves in two
clip rhythms with spears passing through shields at the contact line, the far crowd is a carpet of identical silhouettes
with no shadows beyond 320 m, all motion is PROTOTYPE-ONLY CMU capture, AO and contact shadows have the usual
screen-space limits). Nothing here was measured on a real GPU.

## 1. What changed

Everything is presentation code. The G2 frame of `ForwardRenderer` (render1's, §1 of `reports/render1.md`) gains a
G-buffer-lite, two screen-space lighting terms, a TAA resolve and a cascade debug view; `Gfx.Chars` gains an instanced
crowd; `Gfx.Engine/Scene` gains a CPU occlusion buffer; `Gfx.Demo` gains three crowd scenes and a motion command. Every
part is a switch (§1.7), and with the screen-space switches off no G-buffer is written at all.

### 1.1 G-buffer-lite (`Mgfx/GBufferLayout.cs`, `Mgfx/EffectBuilder.cs`)

A forward renderer has no light buffers, so screen-space AO would normally darken the whole pixel, sunlight included.
Here every pixel program (each of the 52 effects the demo compiles, generated through `EffectBuilder`) writes, next to
its colour, a **linear view depth** (R32F, `1/gl_FragCoord.w`; the sky writes 10⁶ m), the **shares of its final
luminance that are ambient (sky) light and direct (sun) light** (RGBA8: r ambient, g direct, b = 1 when the effect
reported its light, a = the shadow cascade used, for the debug view) and, for effects that move vertices themselves, the
**object motion** (RGBA16F, for TAA). The lighting library reports the ambient term (`gbAmbient` in `shadePbrFull`, the
character surface shaders and the foliage shader) and the fog functions record the surface radiance and its
transmittance (`gbSurface` in `aerialPerspective` and `applyFog`); a common epilogue turns them into the two shares. An
effect that never reports leaves both shares at 0, so AO and contact shadows leave its pixels alone. Blended effects
(water, the cornea) keep the depth below them and scale the shares below by 1 − alpha. The four targets are bound only
when a pass needs them (`RenderFeatures.NeedsGBuffer`).

### 1.2 GTAO and contact shadows (`Effects/ScreenSpaceEffects.cs`, `Rendering/ScreenSpaceLighting.cs`)

- **GTAO at half resolution** (after Jimenez et al. 2016): view-space positions from the linear depth, normals from the
  smaller one-sided depth differences (no smearing across silhouettes), 3 slices × 2 sides × 4 steps over a 1 m world
  radius (at most a sixth of the screen height), horizons clamped to the normal's hemisphere and the cosine-weighted
  visible arc integrated analytically; a 4×4 interleaved rotation pattern, rotated again per still sample or TAA frame,
  then a 4×4 depth-aware blur; fades out between 150 and 225 m. The apply pass upsamples it with depth-aware bilinear
  weights and lifts it with the paper's multi-bounce fit (albedo 0.3).
- **Contact shadows at full resolution**: from each pixel that faces the sun, 8 jittered steps along the sun direction
  over 0.3 m, marched in screen space against the depth; a step that falls behind the depth by more than a bias and less
  than 0.1 m occludes, sharper the nearer the occluder; fades out between 80 and 160 m. (A first version at half
  resolution was diluted by the blur: its measured darkening at a test pillar's foot was 3 %; at full resolution it is
  above 10 %, pinned by a test.)
- **Applied to the right light**: the HDR colour is multiplied by 1 − ambient share × (1 − AO) − direct share × (1 −
  contact visibility), after the opaque pass and before transparent content. AO never darkens sunlight and contact
  shadows never darken sky light; a test pins that the AO darkens the ground at a pillar's sunlit foot much less than at
  its shadowed foot.

### 1.3 Cascaded shadows: what render2 added

The four stabilised cascades with tent PCF and blend bands that replace G1's single map are **render1's**
(`CascadedShadowMap.cs`, `reports/render1.md` §1). render2 adds: a test that pins their stabilisation (each cascade's
centre stays on its texel grid and its radius stays fixed while the camera moves); a **cascade debug view**
(`debug=cascades`: each pixel tinted by the cascade it read, blend bands visible); **cascade caster culling**
(`castercull`, `RenderContext.CoveredByFinerCascade`): a caster whose bounding sphere and whose shadow, thrown 30 m away
from the sun, lie inside the finer cascade's inner square and depth range is skipped in the coarser cascades, which
never sample there (pinned bit-identical by a GPU test; the crowd and the test scene use it); and the contact shadows
above for the detail the 2,048² cascades cannot resolve.

### 1.4 The crowd (`Gfx.Chars/Crowd`)

- **Soldiers**: the stage-chars soldiers themselves, as 8 identity × outfit variants (4 per force: gambeson and spear,
  mail and spear, surcoat and sword, levy and spear), each at three levels: the outfit's `lod1` (about 3,200 triangles),
  `lod2` (about 1,600) and a new `lod3` (638–781) baked from `lod2` by `scripts/bake-crowd.sh`.
- **Instancing**: one draw per variant, level and pass (at most 24 per pass); each instance carries 160 bytes (an affine
  world transform, two clip samples, its look, its LOD fade, and the previous frame's clip samples and foot motion for
  TAA).
- **GPU skinning from a bone texture**: `CrowdAnimationBank` bakes every 30 fps frame of 16 clips of the 8 identities as
  skin matrices into one RGBA32F texture (rows wrapped into columns at 4,096, the HiDef limit). The vertex shader
  blends, per joint influence, two frames of clip A and two frames of clip B, then the two clips by the instance's
  weight (`CrowdEffects.SkinLib`; its CPU twin `CrowdAnimationBank.SkinCpu` is what the tests compare against). The
  coarsest level snaps to the nearest frame of the heavier clip.
- **Three levels with a dithered cross-fade** (`CrowdLod`): the level follows the soldier's projected height (lod1 from
  80 px, lod2 from 32 px, lod3 below; `crowdlod` scales the height first). Within ±15 % of a threshold both levels are
  drawn with exactly complementary interleaved-gradient-noise masks (the outgoing level keeps the pixels at or above the
  fade, the incoming one the others), so every pixel shows one level and nothing pops; the pattern shifts per still
  sample, so accumulated stills average it. Shadow and reflection passes draw one level, one coarser than the view's and
  never lod1.
- **Per-soldier variation** (`CrowdLook`, from a hash of the soldier's key; no random state): identity and outfit
  variant, a dye hue rotation (±0.035 turns) and value (0.80–1.12) on saturated cloth only, skin tone (±12 %), mud on
  the lower legs, metal tarnish, height (±5 %) and width (±4 %); plus a per-soldier clip phase.
- **Scenes** (`Gfx.Demo/Scenes/CrowdScenes.cs`, scripted presentation, not a simulation): `crowd-formation`, two
  formations of 110 (220) on the render1 meadow, front ranks thrusting and blocking, the rest idling with two idle clips
  blended per soldier; `crowd-battle-2k` (8 formations, 2,032) and `crowd-battle-10k` (40 formations, 10,084) on the env
  temperate battlefield beside the Iron Age village, on the flattest dry open strip a search finds outside the village
  (water and forest cells excluded), engaged front lines with four fallen each.

### 1.5 Culling (`Scene/OcclusionBuffer.cs`, `CrowdRenderer`, `BattleOccluders`)

- **Frustum**: every soldier's bounding sphere against the view frustum, and against each cascade's frustum in the
  shadow passes (`frustum`).
- **CPU occlusion (hi-Z)**: a 256×144 software depth buffer rasterised on the CPU each frame from the battle map's
  occluders, then eroded by one pixel (each pixel takes the farthest depth of its 3×3 neighbourhood, so a half-covered
  pixel never occludes) and reduced into a max-depth pyramid. A soldier is culled only when the whole screen rectangle
  of its bounding box lies behind the farthest occluder depth there (`occlusion`). The occluders are built to stay
  inside the real geometry: the terrain as an 8 m grid whose vertices sit 5 cm under the lowest true height within ±16
  m, and the village palisade as thin boxes inside the wall, on the lowest ground of each segment, 85 % of its nominal
  height. Tests pin both bounds.
- **Cascade casters** (§1.3) and a 320 m shadow distance for crowd casters.

### 1.6 TAA (`Effects/TaaEffects.cs`, `Rendering/TaaHistory.cs`)

Interactive frames (`aa=taa`; the explorer's default) render with a Halton(2,3) sub-pixel jitter over 8 frames. The
resolve reprojects the previous resolved frame through the pixel's linear depth and the previous unjittered
view-projection, or along the object motion that the crowd and the hero characters write (their skinning is evaluated
for the previous frame too); samples it with a Catmull-Rom filter; clips it to the variance box of a 13-tap
neighbourhood in YCoCg on Karis-weighted colours; and blends 10 % of the current frame, up to 30 % when the history
moved 12 px or more. History that reprojects off screen is dropped per pixel. The renderer keeps the history across
**continuous** camera motion (less than max(4 m, 15 % of the focus distance) of translation, 20° of rotation and 0.02
rad of field of view between frames) and drops it on a cut, a reset or after a frame rendered without TAA, restarting
the jitter so the frame after a cut equals a fresh renderer's first frame bit for bit. **Stills keep their jittered
16-sample accumulation** (`RenderStill`), unchanged.

### 1.7 Switches, scenes, tools

`--set` switches: `ssao`, `aoradius`, `aostrength`, `aofade`, `contact`, `contactlen`, `contactthick`,
`debug=depth|ao|contact|shares|cascades`, `aa=none|fxaa|taa`, `taaweight`, `taagamma`, `castercull`, and for the crowd
scenes `crowd`, `crowdlod`, `crowdlevel=-1|0|1|2`, `crowdfade`, `crowdlooks`, `crowdblend`, `frustum`, `occlusion`.
`--preset g1` turns the screen-space terms off. `Gfx.Demo --motion` renders interactive frames along a camera move; the
crowd scenes' frame report prints levels, culling counts, shadow instances and CPU times. Scripts: `bake-crowd.sh`,
`capture-render2.sh`, `bench-render2.sh` (+ `bench-render2-table.py`), `review-sheets-render2.py`.

## 2. Assets and provenance

No new external file was downloaded or pinned; `assets/manifest.tsv` is unchanged. `assets/PROVENANCE.md` §3.12 (new)
lists what this stage uses and makes:

- **Crowd LOD3 bake** (`scripts/bake-crowd.sh` → `tools/crowdbake/crowdbake.py`, outputs in `$G2_ASSETS/baked/crowd`,
  never committed): 9 files, 458,043 bytes, from the stage-chars `lod2` meshes (CC0 inputs + generated) with
  meshoptimizer v1.3 (MIT, a bake tool that never ships). **SHIP-OK.** `--check` re-bakes in memory and compares byte
  for byte.
- **Crowd animation bank** (runtime texture only): the stage-chars CMU retargets. **PROTOTYPE-ONLY (provisional)**, as
  the clips it is made from. Every crowd capture and every `chars-forces` capture is posed by CMU motion, so every
  legend in §3 and §6 says so.
- **Per-soldier looks**: generated in code (MurmurHash3's public-domain finaliser as the hash).
- **Everything else** (G-buffer, GTAO, contact shadows, cascade debug and caster culling, occlusion buffer, TAA): code,
  implemented from publications (Jimenez et al. 2016 GTAO; Jimenez 2014 interleaved gradient noise; Karis 2014 and Salvi
  2016 for TAA); no code copied.

## 3. Captures (before and after; looked at)

All frames 1280×720 on llvmpipe, written by `scripts/capture-render2.sh` to the scratchpad
(`…/scratchpad/m7gfx2/captures/render2/final/`, one folder per set, with each run's log). Stills use 16 jittered samples
unless a set says otherwise (the 10,000-soldier stills use 8). BEFORE frames: the trunk's own 16-sample `chars-forces`
stills (`…/captures/render2/before/`, rendered from `g2/trunk` 7dd134a at the start of this stage) and G1's pristine
`units-*` captures (`…/captures/render1/g1/`). **Every frame with characters or crowds is posed by CMU motion capture:
PROTOTYPE-ONLY.** What I looked at, and what I saw:

- **AO and contact shadows off = the trunk frame, bit for bit.** The six `chars-forces` stills with `ssao=0,contact=0`
  equal the trunk's BEFORE stills exactly (0 pixels differ in each of the six). So the changes to every effect (the
  G-buffer epilogues, the light reporting, the previous-frame skinning) change nothing in the frame when the
  screen-space terms are off.
- **AO and contact shadows on** (`forces`, `env`): in the six `chars-forces` stills 68–98 % of the pixels change, by
  0.6–4.2 levels on average (out of 255) and at most 82–149; in the two village stills 76–92 %, by 0.6–2.7, at most
  43–114. Where: under the boots and along the soles (contact shadows), between legs, under arms, skirts and shield
  rims, in the folds of the faces under helmet brims (AO), at the bases of grass clumps and between palisade logs in the
  village gate shot. The feet now sit on the ground; before, the ground right under a toe was as bright as open ground.
  The change is deliberately subtle in sunlight (AO darkens only the sky-light share) and clearer in shadow. With
  auto-exposure on, the meter answers the darker frame: the sky of the village gate shot is 2/255 brighter with AO on,
  its foreground grass 5/255 darker.
- **Debug views** (`debug=ao|contact|shares|cascades`, one sample): the AO term is a soft darkening at contacts with
  faint banding on open ground at grazing angles; the contact term is a thin, hard shadow under feet and on body parts
  that face the sun with an arm, shield or garment within 0.3 m towards it; the shares view shows sunlit surfaces as
  direct, shadowed ones as ambient, the sky as unreported; the cascade view shows four bands with soft blend borders and
  the soldiers in cascade 1.
- **Crowds** (`crowd220`, `crowd2k`, `crowd10k`): the instanced soldiers are the stage-chars soldiers, with their kit,
  shield emblems and helmets, in place of G1's low-poly procedural figures. Front ranks thrust and block, rear ranks
  idle out of step with each other, the engaged lines of the battle scenes have their fallen. Colour, height and mud
  vary from soldier to soldier; the dusk shot keeps long soft shadows under 220 soldiers. The 10,000 read as an army
  laid out on the battlefield at vista distance.
- **Levels** (`lod0`/`lod1`/`lod2` = forced lod1/lod2/lod3): lod1 and lod2 are hard to tell apart at the close shot's
  distance; lod3 flattens helmets into facets and coarsens shields, which is why the automatic choice only uses it below
  32 px. `fade-*`: soldiers inside a cross-fade band show a stipple of the two levels in a single sample and a clean
  average after 16.
- **Culling**: `cull-on` and `cull-off` (frustum, occlusion and cascade-caster culling all off) are identical in the 2k
  village and vista and the 10k village shots (0 pixels differ), while culling draws 1,258 instead of 2,032 soldiers and
  1,436 instead of 5,744 shadow instances in the 2k village shot (774 hidden behind the terrain and the palisade), 2,501
  instead of 8,128 shadow instances in the 2k vista, and 6,633 instead of 10,084 soldiers (3,066 outside the view, 385
  hidden) and 10,670 instead of 39,860 shadow instances in the 10k village shot.
- **TAA in motion** (`motion-220`, a sideways pan; `motion-forces`, an orbit; frame 16 of each): with no AA the spears,
  shield rims and helmet edges stair-step and the hashed-alpha hair is noise; FXAA softens the steps but keeps the hair
  noise; TAA resolves both (clean spear shafts, smooth hair) and is softer overall, with slight smearing on the
  fastest-moving heads.

## 4. Costs (llvmpipe, not Victus evidence)

`scripts/bench-render2.sh` (raw logs and `bench-render2.tsv` in the scratchpad, `…/m7gfx2/bench/render2/`), run on the
build of commit 82c8198. The last commit (78fe269) adds the `crowdlooks` and `crowdblend` switches (code that runs only
when they are off), applies the crowd switches before the shadow pass as well, and drops the TAA history after a frame
rendered without TAA; with the default settings it renders the same frames (checked: the `crowd-formation` front-low and
close 16-sample stills of the two builds are bit-identical), so the numbers below stand for the defaults. `Gfx.Demo
--bench`: 1280×720, the scene's own settings (render2 defaults: AO, contact shadows, FXAA and all culling on), 3 warm-up
frames, then 4–10 measured frames per variant (fewer for the larger crowds), several variants on one loaded scene, the
GPU synced at the end of every frame; p50 of the measured frames. The per-pass profile syncs the GPU after every pass,
which costs time of its own.

### 4.1 Crowd at 220, 2,032 and 10,084 soldiers

| Scene, shot | Soldiers drawn (instances lod1 / lod2 / lod3; in fade) | Crowd triangles, main / shadow | Frame p50 | Without the crowd (`crowd=0`) | The crowd's cost |
|---|---|---|---:|---:|---:|
| crowd-formation, front-low | 179 of 220 (178 / 45 / 0; 44) | 0.64 M / 0.82 M | 966 ms | 687 ms | +279 ms |
| crowd-formation, wide | 220 of 220 (0 / 220 / 6; 6) | 0.36 M / 0.21 M | 773 ms | 564 ms | +208 ms |
| crowd-battle-2k, line | 1,908 of 2,032 (19 / 625 / 1,580; 316) | 2.17 M / 1.63 M | 1,589 ms | 934 ms | +655 ms |
| crowd-battle-2k, vista | 2,032 of 2,032 (0 / 0 / 2,032; 0) | 1.39 M / 1.72 M | 2,669 ms | 2,284 ms | +385 ms |
| crowd-battle-10k, line | 10,068 of 10,084 (0 / 0 / 10,068; 0) | 6.91 M / 4.42 M | 2,710 ms | 804 ms | +1,906 ms |
| crowd-battle-10k, vista | 6,811 of 10,084 (0 / 0 / 6,811; 0) | 4.67 M / 6.99 M | 5,562 ms | 3,811 ms | +1,751 ms |

Crowd CPU per frame (same runs): `SetAgents` (world transforms, bank rows, looks) 220 0.07 ms, 2,032 0.59 ms, 10,084
3.06 ms; culling and level choice of the main pass 0.02 ms, 0.62 ms, 2.55 ms; shadow selection over the four cascades
0.11 ms, 0.78 ms, 1.46 ms.

### 4.2 LOD sweep

| Levels | crowd-formation, wide (220): instances lod1 / lod2 / lod3 (in fade) | triangles main / shadow | p50 | crowd-battle-2k, line (2,032): instances | triangles main / shadow | p50 |
|---|---|---|---:|---|---|---:|
| auto (default) | 0 / 220 / 6 (6) | 0.36 M / 0.21 M | 756 ms | 19 / 625 / 1,580 (316) | 2.17 M / 1.63 M | 1,609 ms |
| all lod1 (`crowdlevel=0`) | 220 / 0 / 0 (0) | 0.70 M / 0.50 M | 879 ms | 1,908 / 0 / 0 (0) | 6.07 M / 3.88 M | 2,815 ms |
| all lod2 (`crowdlevel=1`) | 0 / 220 / 0 (0) | 0.36 M / 0.21 M | 760 ms | 0 / 1,908 / 0 (0) | 3.12 M / 1.62 M | 1,807 ms |
| all lod3 (`crowdlevel=2`) | 0 / 0 / 220 (0) | 0.15 M / 0.21 M | 682 ms | 0 / 0 / 1,908 (0) | 1.31 M / 1.62 M | 1,471 ms |
| bias 0.5 (`crowdlod=0.5`) | 0 / 23 / 220 (23) | 0.18 M / 0.21 M | 688 ms | 0 / 90 / 1,904 (86) | 1.45 M / 1.62 M | 1,456 ms |
| bias 2 (`crowdlod=2`) | 220 / 119 / 0 (119) | 0.89 M / 0.47 M | 808 ms | 389 / 1,475 / 793 (749) | 4.21 M / 2.02 M | 2,380 ms |
| hard switches (`crowdfade=0`) | 0 / 220 / 0 (0) | 0.36 M / 0.21 M | 671 ms | 0 / 447 / 1,461 (0) | 1.74 M / 1.63 M | 1,536 ms |

### 4.3 Culling

| Shot | Culling | Soldiers drawn (frustum-, occlusion-culled) | Crowd shadow instances (skipped by caster culling) | Frame triangles | Frame p50 | Occlusion build, CPU |
|---|---|---|---|---:|---:|---:|
| 2k village | all on (default) | 1,258 (0, 774) | 1,436 (1,436) | 3.2 M | 1,383 ms | 2.7 ms |
| 2k village | occlusion off | 2,032 (0, 0) | 1,436 (1,436) | 3.7 M | 1,400 ms | off |
| 2k village | frustum off | 1,258 (0, 774) | 4,308 (1,436) | 5.1 M | 1,461 ms | 2.8 ms |
| 2k village | caster culling off | 1,258 (0, 774) | 2,872 (0) | 4.2 M | 1,453 ms | 2.9 ms |
| 2k vista | all on (default) | 2,032 (0, 0) | 2,501 (2,275) | 9.8 M | 2,738 ms | 15.3 ms |
| 2k vista | occlusion off | 2,032 (0, 0) | 2,501 (2,275) | 9.8 M | 2,765 ms | off |
| 2k vista | caster culling off | 2,032 (0, 0) | 4,776 (0) | 11.3 M | 2,797 ms | 7.4 ms |
| 10k village | all on (default) | 6,633 (3,066, 385) | 10,670 (9,975) | 13.1 M | 2,468 ms | 5.8 ms |
| 10k village | occlusion off | 7,018 (3,066, 0) | 10,670 (9,975) | 13.4 M | 2,521 ms | off |
| 10k village | frustum off | 9,699 (0, 385) | 29,885 (9,975) | 28.5 M | 3,146 ms | 5.6 ms |
| 10k village | caster culling off | 6,633 (3,066, 385) | 20,645 (0) | 20.0 M | 3,529 ms | 7.2 ms |
| 10k line | all on (default) | 10,068 (0, 16) | 6,425 (8,470) | 13.9 M | 2,641 ms | 5.3 ms |
| 10k line | occlusion off | 10,084 (0, 0) | 6,425 (8,470) | 13.9 M | 2,641 ms | off |
| 10k line | caster culling off | 10,068 (0, 16) | 14,895 (0) | 19.8 M | 3,399 ms | 3.9 ms |

### 4.4 AO, contact shadows, TAA

| Scene, shot | Variant | Frame p50 | p95 |
|---|---|---:|---:|
| chars-forces, lines-34 | `g2` | 1,277 ms | 1,402 ms |
| chars-forces, lines-34 | `ssao=0` | 1,330 ms | 1,468 ms |
| chars-forces, lines-34 | `contact=0` | 1,333 ms | 1,382 ms |
| chars-forces, lines-34 | `ssao=0,contact=0` | 1,295 ms | 1,358 ms |
| crowd-formation, front-low | `g2` | 962 ms | 1,064 ms |
| crowd-formation, front-low | `ssao=0` | 953 ms | 1,027 ms |
| crowd-formation, front-low | `contact=0` | 918 ms | 1,082 ms |
| crowd-formation, front-low | `ssao=0,contact=0` | 927 ms | 1,089 ms |
| env-temperate, settlement | `g2` | 1,837 ms | 1,940 ms |
| env-temperate, settlement | `ssao=0,contact=0` | 1,870 ms | 1,990 ms |
| chars-forces, lines-34 | `aa=none` | 1,268 ms | 1,357 ms |
| chars-forces, lines-34 | `aa=fxaa` | 1,310 ms | 1,393 ms |
| chars-forces, lines-34 | `aa=taa` | 1,336 ms | 1,450 ms |
| crowd-formation, front-low | `aa=none` | 1,002 ms | 1,292 ms |
| crowd-formation, front-low | `aa=fxaa` | 994 ms | 1,077 ms |
| crowd-formation, front-low | `aa=taa` | 1,076 ms | 1,147 ms |

### 4.5 Per-pass profile (GPU synced after each pass; ms per frame)

| Scene, shot | Variant | Frame p50 | scene | shadows | ssao | transparent | taa | fxaa | bloom-down | bloom-up | composite |
|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| chars-forces, lines-34 | `g2` | 1,310 | 943.5 | 363.5 | 30.5 | 2.6 | – | 12.2 | 11.9 | 7.8 | 14.8 |
| chars-forces, lines-34 | `ssao=0,contact=0` | 1,295 | 941.9 | 316.7 | – | 2.5 | – | 11.8 | 11.0 | 7.4 | 12.4 |
| chars-forces, lines-34 | `aa=taa` | 1,355 | 931.7 | 323.5 | 31.5 | 1.9 | 32.3 | – | 11.9 | 7.2 | 14.5 |
| crowd-formation, wide | `g2` | 810 | 645.4 | 101.3 | 29.3 | 1.8 | – | 9.5 | 11.7 | 6.8 | 12.9 |
| crowd-formation, wide | `crowd=0` | 646 | 518.2 | 63.4 | 27.4 | 1.9 | – | 8.5 | 10.9 | 7.0 | 12.2 |
| crowd-formation, wide | `aa=taa` | 809 | 628.3 | 92.8 | 27.0 | 2.0 | 32.7 | – | 11.2 | 7.9 | 12.4 |
| crowd-battle-2k, line | `g2` | 1,737 | 1059.4 | 600.8 | 23.3 | 8.3 | – | 10.9 | 11.4 | 7.5 | 13.0 |
| crowd-battle-2k, line | `crowd=0` | 1,018 | 574.2 | 359.6 | 22.0 | 45.6 | – | 6.8 | 9.6 | 8.0 | 11.5 |
| crowd-battle-10k, line | `g2` | 2,593 | 1592.6 | 874.8 | 21.3 | 6.5 | – | 8.1 | 10.1 | 7.4 | 13.7 |
| crowd-battle-10k, line | `crowd=0` | 848 | 403.3 | 348.4 | 24.1 | 6.7 | – | 7.1 | 10.9 | 8.4 | 12.3 |


### 4.6 Reading the costs

- **Noise.** Within this run the same configuration measured in different groups agrees within about 3 % (for example
  `crowd-formation` front-low at the defaults: 966, 962 and 994 ms). Between this run and an earlier full run on a
  previous build the same configurations differed by up to 12 % (most by under 5 %) and by 24 % in the 10k vista (7,289
  ms then, 5,562 ms now; 4 frames each), so the crowd's cost in that shot is +1.8 s to +3.7 s depending on the run.
  Differences under about 10 % are not findings.
- **The crowd scales well below linear in its count.** From 220 to 10,084 soldiers (×46) its cost grows about ×6–9
  (+208–279 ms to +1.75–1.91 s), because the levels put distant soldiers on 640–780-triangle meshes and shadow casters
  one level coarser, and the culling drops what cannot show. On llvmpipe the cost is mostly triangles and fill in the
  main and shadow passes (the profile: the 10k line's scene pass 1,593 ms with the crowd against 403 ms without, its
  shadow pass 875 against 348). The crowd's CPU work stays small: about 3 ms for `SetAgents` and 2.5 ms for culling and
  levels at 10,084 soldiers.
- **The levels earn their keep where soldiers are close.** In the 2k line shot, forcing every soldier to `lod1` costs
  1.75× the frame of the automatic choice (2,815 against 1,609 ms; 6.1 M against 2.2 M crowd triangles); the automatic
  choice is within 10 % of all-`lod3` while keeping the near soldiers at `lod1`/`lod2`. The cross-fade's extra instances
  (316 soldiers drawn twice) cost too little to measure here (1,609 against 1,536 ms with hard switches).
- **Culling.** Cascade caster culling is the largest saving in these scenes: 22–30 % of the 10k frames (the 10k village
  3,529 → 2,468 ms, the 10k line 3,399 → 2,641 ms) and about 2–5 % of the 2k ones. Frustum culling matters when the army
  is partly behind the camera (the 10k village: 3,146 → 2,468 ms, and 29,885 → 10,670 shadow instances). Occlusion
  culling removes 774 of 2,032 soldiers (38 %) in the 2k village shot and 385 in the 10k village shot, but its frame
  saving on llvmpipe is within the noise (1–2 % in this run, 7 % in the earlier one) and it costs 2–15 ms of CPU per
  frame for 10,000–23,000 occluder triangles (the same 10,008-triangle build took 2.0–12.9 ms in different frames as
  llvmpipe's threads took the cores). On a GPU, where triangles are cheap and the CPU is not shared with the rasteriser,
  that balance is unknown: it may be worth skipping the build when nothing was hidden.
- **AO and contact shadows** cost 21–31 ms per frame in the profile (half-resolution GTAO, blur, full-resolution contact
  march and apply at 1280×720), 1–4 % of these frames; the frame-level comparison is within the noise. The G-buffer
  writes add no measurable time to the scene pass here.
- **TAA** costs 32–33 ms per frame for the resolve against 8–12 ms for FXAA; frame to frame on `chars-forces` TAA, FXAA
  and none are within 6 % of each other (1,336, 1,310 and 1,268 ms) and on the 220 front-low TAA is 8 % above FXAA
  (1,076 against 994 ms).

## 5. Tests, builds, checks

### 5.1 Builds

`dotnet build M7Gfx2.slnx --no-incremental` (`nice -n 19`, `-m:1`, `-nodeReuse:false`,
`DOTNET_CLI_USE_MSBUILD_SERVER=0`), Debug and Release, on the final source: 9 projects each, **0 warnings, 0 errors**
(warnings are errors in this solution).

### 5.2 Tests

Full suite, one test host each, no out-of-memory: **Debug 257 passed, 0 failed, 0 skipped (3 min 12 s); Release 257
passed, 0 failed, 0 skipped (1 min 33 s)** (`dotnet test Gfx.Tests/Gfx.Tests.csproj --no-build`, llvmpipe, TRX logs in
`/home/user/g2w/scratch-render2/trx/`). Per class, the same in both: Avatar 18, Camera 8, Chars 24, Cinematic 26, Crowd
12, EffectLoad 4, EngineUnit 6, EnvLayout 12, EnvRender 3, EnvTree 6, Frustum 5, Integration 2, PerfDevice 10,
PerfEnvTap 1, PerfProcess 5, PerfUnit 41, Render1Math 17, Render1Render 7, Render2Math 12, Render2Render 10, Render 2,
Terrain 15, Units 11. The device tests that need the chars bake ran (none skipped).

An earlier full Release run during the stage (commit 5e42525) had one failure:
`PerfDeviceTests.WarmupLastsAtLeastItsMinimumTime`, the perf stage's wall-clock test (at least five warm-up frames in
its 3 s minimum), which passed when run alone; the trunk documents the same failure once with the env tests beside it.
render2's CPU-heavy test classes (`Render2MathTests`, `CrowdTests`) now run in the serial collection with the env tests;
both final runs passed it. **Not run:** the repository's `scripts/validate.sh` tiers (the simulation solution; this
stage changes nothing outside `prototypes/m7-gfx2`) and G1's tests (`prototypes/m7-gfx1` is unchanged).

The stage's own tests (34 in three classes; `[GfxFact]`/`[CharsGfxFact]` tests run on the device, here llvmpipe):

| Class | Tests | What they pin |
|---|---:|---|
| `Render2MathTests` (CPU) | 12 | every effect writes the four G-buffer outputs and never `gl_FragColor`; every lit surface effect reports its ambient light, and the fog functions record the surface and its transmittance; cascade caster culling keeps casters whose sphere or shadow throw can leave the finer cascade's inner square or depth range; TAA history kept across small moves and dropped on jumps, turns and zooms (the jump allowance grows with the focus distance); the jitter is a deterministic Halton sequence inside the pixel; the crowd LOD rule (thresholds, complementary fade pairs, monotonic in size, bias, force, shadow level); looks inside their bounds and varied; the occlusion buffer hides only what is wholly behind a wall and keeps silhouettes conservative, down to the sub-pixel case its erosion exists for; terrain occluders never rise above the ground (4,000 samples); wall occluders never rise above the palisade on sloping ground and leave the gate open |
| `Render2RenderTests` (device) | 10 | AO darkens the ground at a pillar's foot, much less (relative) on the sunlit side than on the shadowed side, and leaves open ground alone; contact shadows darken the ground at the pillar's foot on the side away from the sun and leave the ground beside it alone; the G-buffer holds the linear depth (5.5 m at the slab) and the expected shares in shadow and sun, the sky its depth; cascades stay on their texel grid and keep their size while the camera moves; caster culling culls casters and changes no pixel; the cascade debug view shows three cascades; TAA keeps its history on small moves (and it contributes) and drops it on a cut (the frame then equals a fresh renderer's, bit for bit), on a reset and after a frame without TAA; TAA rejects history the current frame contradicts (a removed box leaves no trail); TAA converges towards the 16-sample still on edges (error under 70 % of one unfiltered frame's); with the screen-space switches off no G-buffer is created, and with AO strength 0 the G-buffer frame equals it |
| `CrowdTests` | 12 | the bank holds every clip frame's skin matrices (against the stage-chars forward kinematics), wraps loops and clamps one-shots, and wraps rows past the HiDef limit into columns; the LOD3 bake matches its manifest; GPU skinning puts the soldier where the CPU twin of the shader does (projected bounding boxes within 2.5 px, for a variant in the bank's first column and one in its last); the two halves of a cross-fade are exact complements (bit for bit); two-clip blend weight 0 is clip A bit for bit and weight 1 is clip B within 12 px; looks change the soldier's pixels; the `crowdlooks` and `crowdblend` switches give exactly the neutral soldier and the heavier clip; moving soldiers write their screen motion (within 10 %); the crowd scene renders and never writes the authoritative grid; `Gfx.Chars/Crowd` references no simulation assembly |

`EnvTests.EnvironmentOffDrawsExactlyTheG1Content` was changed: it now compares with `ssao=0,contact=0` (AO legitimately
changes the env frame; the env switches' own G1 identity is what that test pins).

### 5.3 Mutants (bounded)

One mutation at a time in a separate worktree (`/home/user/g2w/render2-mut`, detached at the commit under test), Release
rebuild of the test project, then the render2 test classes with a time bound of max(3 × the clean run of the same
filter, 90 s); the file restored from git after each. **Final round: all 23 mutants on commit 78fe269**, clean run 18 s
(34 tests), bound 90 s (5×); mutant runs took 17–28 s; none hung. Script:
`/home/user/g2w/scratch-render2/mutants/mutants.py` (exact search and replacement texts in it; not committed).

| Mutant | Change | Killed by (semantic test) |
|---|---|---|
| M1 | AO multiplies the whole colour, not the ambient share | `AmbientOcclusionDarkensOnlyTheAmbientShareAtContacts` |
| M2 | GTAO: the −dir horizon angle with the wrong sign | `AmbientOcclusionDarkensOnlyTheAmbientShareAtContacts` |
| M3 | contact-shadow ray marched away from the sun | `ContactShadowsFallAwayFromTheSun` (and `TaaRejectsHistoryTheCurrentFrameContradicts`) |
| M4 | cascade centre not snapped to texels | `CascadesStayOnTheirTexelGridAndKeepTheirSize` |
| M5 | caster culling ignores the shadow's 30 m throw | `CasterCullingKeepsCastersWhoseShadowCanLeaveTheFinerCascade` |
| M6 | TAA keeps its history across translation jumps | `TaaHistorySurvivesSmallMovesAndNotCuts`, `TaaKeepsHistoryOnSmallMovesAndDropsItOnCuts` |
| M7 | TAA drops its history every frame | 5 tests, among them `TaaKeepsHistoryOnSmallMovesAndDropsItOnCuts`, `TaaConvergesTowardsTheAccumulatedStill` |
| M8 | incoming LOD mask is not the outgoing one's complement | `LodCrossFadeMasksAreExactComplements` |
| M9 | two-clip blend ignores its weight | `TwoClipBlendEndsAreTheClipsThemselves` |
| M10 | bank rows past 4,096 not wrapped into the next column (shader) | `CrowdSkinningMatchesTheCpuReference` (the variant in the last column) |
| M11 | occlusion buffer not eroded | `OcclusionSilhouettesAreConservative` |
| M12 | terrain occluder vertices at the true height, not the local minimum | `TerrainOccludersStayUnderTheGround` |
| M13 | the sky writes depth 0 | `GBufferHoldsLinearDepthAndLightShares` |
| M14 | the crowd's previous position ignores the feet's move | `MovingSoldiersWriteTheirScreenMotion` |
| M15 | the lod1/lod2 fade runs backwards across its band | `CrowdLevelsFollowProjectedSizeWithComplementaryFades` |
| M16 | jitter not centred on the pixel | `TaaJitterIsDeterministicAndInsideThePixel` |
| M17 | `shadePbrFull` does not report its ambient light | `AmbientOcclusionDarkensOnlyTheAmbientShareAtContacts`, `ContactShadowsFallAwayFromTheSun`, `GBufferHoldsLinearDepthAndLightShares` (and the source check `LitSurfaceEffectsReportTheirAmbientLight`) |
| M18 | the TAA resolve ignores its history | `TaaConvergesTowardsTheAccumulatedStill` |
| M19 | TAA history not clipped to the current neighbourhood | `TaaRejectsHistoryTheCurrentFrameContradicts` |
| M20 | wall occluder boxes on the higher end of their segment (the rule before this stage's own fix) | `WallOccludersNeverRiseAboveTheWall` |
| M21 | the `crowdlooks` switch ignored | `LooksAndBlendSwitchesGiveTheNeutralSoldierAndTheHeavierClip` |
| M22 | with blending off, the lighter clip kept | `LooksAndBlendSwitchesGiveTheNeutralSoldierAndTheHeavierClip` |
| M23 | a frame without TAA leaves the history valid | `TaaKeepsHistoryOnSmallMovesAndDropsItOnCuts` |

23 of 23 killed. **Found by the mutants on the way (and fixed):** in the first round (20 mutants, commit 49f8698), **M11
survived**: the silhouette test's sphere reached more than a pixel past the wall's edge, so the erosion it was written
for never mattered. The test now builds the sub-pixel case the erosion exists for (a pixel whose centre lies inside the
wall although part of it does not); M11 was then killed (second round, commit 82c8198) and again in the final round. M19
has a test only because it was written for it in this stage: before, no test caught ghosting.

Teeth and aim: each asserted property is one the renderer ought to have — AO darkens only sky light and leaves open
ground alone; contact shadows fall away from the sun; cascades do not shimmer; an optimisation (caster, frustum or
occlusion culling) never removes a visible pixel; history survives continuous motion and not cuts or AA switches; ghosts
are rejected; cross-fades have no holes and no backwards fades; blend ends are the clips; every variant animates;
switches do what they say; the sky is recognisable in the depth target; jitter does not shift the image. Not covered by
any test: how the resolve uses the written object motion (only the write is pinned, M14), and the TAA current-frame
weight and clip width as numbers.

### 5.4 Other checks

- **The trunk frame with the screen-space terms off**: the six `chars-forces` 16-sample stills with `ssao=0,contact=0`
  are bit-identical to the trunk's own (§3).
- **Culling changes no pixel** in the three shots compared (§3), and the GPU test `CasterCullingChangesNoPixel`.
- **Determinism and isolation**: `scripts/check-gfx-determinism.sh` OK (4 files in `Gfx.Proc/Authoritative`; render2
  adds none). Crowd looks and phases come from a hash of the agent key; the jitter is a fixed sequence; no random-number
  state, no wall clock in what is drawn (the crowd's CPU timings use `Stopwatch` for the frame report only).
- **G1 untouched**: `git diff 1601c74..HEAD -- prototypes/m7-gfx1` is empty; nothing outside `prototypes/m7-gfx2`
  changed since the trunk base 7dd134a.
- **Crowd bake**: `scripts/bake-crowd.sh --check` re-baked the LOD3 meshes in memory: 10 files compared (the 9 meshes
  and `MANIFEST.tsv`), 0 differ.
- **Originality (D-069)**: no other game is named in code, scenes, captions or this report (grep of the diff for the
  usual names: none).

## 6. Review JPEGs

`reports/captures/render2/`, made by `scripts/review-sheets-render2.py` from the captures of §3 (each under 170 KB;
726,605 bytes in all; all review JPEGs on this branch together 4,963,217 bytes, plus about 0.9 MB from stage kit on its
own branch). Every sheet's legend says: llvmpipe, not Victus evidence; CMU motion PROTOTYPE-ONLY in frame.

| File | Shows |
|---|---|
| `render2-01-ao-contact.jpg` | `chars-forces` lines-low with AO and contact shadows off (the trunk frame, bit for bit) and on, the difference ×6, the AO and contact terms alone; the feet at full size; the env village gate (grass, palisade) off and on; the light shares |
| `render2-02-crowd-g1-vs-g2.jpg` | G1's procedural soldiers (units-clash, units-wide5k) next to the render2 crowd at 220, 2,032 and 10,084 |
| `render2-03-crowd-lod.jpg` | the same soldiers forced to lod1, lod2 and lod3; a lod2/lod3 cross-fade in one sample, after 16 samples and as a hard switch (×2); force A from behind (variation), the dusk shot, the 2k line with automatic levels |
| `render2-04-taa-motion.jpg` | frame 16 of a camera pan over the 220 and of an orbit around the line-up: no AA, FXAA, TAA (×2 crops of the edgiest window) |
| `render2-05-cascades-culling.jpg` | the cascade debug view (line-up, 10k vista), the 2k village shot whose culling the costs measure, the 10k rear |

## 7. What still looks placeholder

Plainly: none of this is production quality.

- **Crowd soldiers up close.** The nearest crowd soldiers are the outfits' `lod1` meshes (about 3,200 triangles); within
  a few metres their helmets show facets and hems look simplified. The full hero meshes and the hero shading path (hair
  cards, eyes, morph targets) are not used by the crowd. `lod3` flattens helmets into facets; it is only used below 32
  px, where that does not read, but the transition band is where a sharp eye would catch it.
- **Motion.** All crowd motion is the CMU retargets (PROTOTYPE-ONLY): a formation's front ranks run one 3.2 s
  thrust-and-block cycle and the rest two idle clips, each soldier with its own phase and blend; the result reads as a
  crowd that moves in two rhythms. No foot IK: on slopes feet float or sink a few centimetres. No collision: at the
  contact line spears pass through shields and bodies. No march, charge or rout is staged (the clips exist).
- **The far crowd.** Beyond 320 m soldiers cast no shadow; at vista distance a formation is a carpet of 640–780-triangle
  silhouettes a few pixels tall, with no impostors, banners, dust or trampled ground.
- **AO is screen-space.** It knows nothing outside the frame or behind the first surface, darkens a little beside thin
  objects seen against far ground (halos), shows faint banding on open ground at grazing angles in a single frame, and
  fades out between 150 and 225 m, so distant formations get none.
- **Contact shadows are screen-space.** They vanish with their occluder when it leaves the frame, and their term can be
  hard and black on body parts with an arm or shield just sunward (visible in the debug view; in the frame it only
  scales the direct share).
- **TAA is soft.** There is no sharpening pass; in motion the image is softer than FXAA, the fastest-moving heads smear
  slightly, and transparent surfaces (water) reproject through the depth behind them.
- **Shadows** are render1's cascades (four 2,048² tiles in a 4,096² atlas); crowd shadows use one level coarser than the
  view.

## 8. Limitations and risks

- **No hardware GPU ran this.** Every cost here is llvmpipe on 4 CPU cores, where fill rate and vertex shading compete
  for the same cores as the CPU work; relative costs on the Victus will differ. Two things to watch there: the crowd
  vertex shader reads up to 48 bank texels per vertex while two clips blend (4 influences × 3 texels × 2 frames × 2
  clips; 24 for the coarsest level), twice that with TAA on (the previous frame is skinned too), and the instance
  buffers carry 160 bytes per soldier per pass (up to six passes: the view, four cascades and, on the env battlefield,
  the water reflection).
- **The CPU occlusion buffer costs 2–15 ms per frame here** (rasterising 10,000–23,000 occluder triangles at 256×144)
  whether or not it hides anything: in the vista shots it hides nothing. A rule to skip it (a high camera, or nothing
  hidden in the last frames) is not implemented. The palisade occluder treats the log wall as solid: a soldier seen only
  through the slits between logs (up to 5 cm wide) is culled. The culled shots tested are pixel-identical to the
  unculled ones, but that is three shots, not a proof.
- **Cascade caster culling assumes a 30 m shadow throw**: a caster skipped in the coarser cascades has its shadow
  checked only 30 m along the light; ground more than 30 m further down that line (a cliff edge) and outside the finer
  cascade's depth range would lose that shadow.
- **TAA history policy.** History is kept across continuous camera motion and dropped on cuts by thresholds; content
  that changes abruptly without a camera cut (a scene switch at the same pose) must set `ResetHistory`. Object motion is
  written only by the crowd and the hero characters; anything else that moves on its own (trees in the wind, water)
  relies on the variance clip.
- **Auto exposure meters after the AO**, so turning AO on brightens unoccluded parts of the frame by a level or two.
- **Memory**: the G-buffer adds 16 bytes per pixel at full resolution (R32F depth, RGBA8 shares, RGBA16F motion) plus
  two half-resolution RGBA16F AO targets and two full-resolution TAA history targets.
- **Crowd scenes are scripted.** They stand in for published battle state; `CrowdAgent` (position, yaw, variant, two
  clips with times and a blend, look) is the interface a state adapter would fill. Nothing is wired to the simulation.

## 9. Sim/presentation boundary

- Everything here is presentation (DR42): the crowd scenes are scripted presentations that read the generated grid and
  the env layout and never write them (test `CrowdSceneRendersAndNeverWritesTheGrid` hashes the authoritative grid
  before and after rendering); `Gfx.Chars/Crowd` references no simulation assembly (test
  `CrowdLayerReferencesNoSimulationAssembly`); agents come from a scene or, later, from published state, and nothing is
  written back.
- Per-soldier looks and clip phases come from a hash of the agent key, not from any random-number state; TAA jitter is a
  fixed Halton sequence; the occlusion buffer, the LOD choice and the cascade caster test only decide what is drawn.
  Nothing here touches results, positions, health, RNG, timing or hashes.
- `scripts/check-gfx-determinism.sh` (the integer-only scan of `Gfx.Proc/Authoritative`) is clean; render2 adds no file
  there.

## 10. Files

New: `Gfx.Engine/Mgfx/GBufferLayout.cs`, `Gfx.Engine/Effects/ScreenSpaceEffects.cs`, `Gfx.Engine/Effects/TaaEffects.cs`,
`Gfx.Engine/Rendering/ScreenSpaceLighting.cs`, `Gfx.Engine/Rendering/TaaHistory.cs`,
`Gfx.Engine/Scene/OcclusionBuffer.cs`, `Gfx.Engine/Scene/IFrameReport.cs`; `Gfx.Chars/Crowd/` (`CrowdAnimationBank.cs`,
`CrowdEffects.cs`, `CrowdLod.cs`, `CrowdRenderer.cs`, `CrowdTypes.cs`); `Gfx.Demo/Scenes/CrowdScenes.cs`,
`Gfx.Demo/MotionCapture.cs`; `tools/crowdbake/crowdbake.py`; `scripts/bake-crowd.sh`, `capture-render2.sh`,
`bench-render2.sh`, `bench-render2-table.py`, `review-sheets-render2.py`; `Gfx.Tests/Render2Tests.cs`
(`Render2MathTests`), `Render2RenderTests.cs`, `CrowdTests.cs`; this report and `reports/captures/render2/`.

Changed: `Gfx.Engine/Mgfx/EffectBuilder.cs` (G-buffer outputs, sky/blended/velocity flags), `Effects/GlslLib.cs`
(ambient and surface reporting, cascade index), `Effects/BuiltinEffects.cs`, `Effects/PostEffects.cs`,
`Rendering/ForwardRenderer.cs` (G-buffer binding, screen-space pass, TAA path, debug views, cascade caster culling
context), `Rendering/RenderContext.cs` (velocity matrices, cascade culling), `Rendering/RenderSettings.cs` (switches);
`Gfx.Chars/Rendering/CharEffects.cs` and `CharRenderer.cs` (ambient reporting, object motion from the previous bone
palette); `Gfx.Proc` water and foliage effects (blended G-buffer, ambient reporting); `Gfx.Demo/Program.cs` (`--motion`,
frame reports), `InteractiveGame.cs` (TAA default, key 9); `Gfx.Tests/EnvTests.cs` (one test compares with the
screen-space terms off); `README.md`; `assets/PROVENANCE.md` (§3.12). G1 (`prototypes/m7-gfx1`) is unchanged.

## 11. Reproduce

```
./scripts/bootstrap.sh                                   # .NET 10 SDK (repo root)
cd prototypes/m7-gfx2
scripts/bake-render1.sh && scripts/bake-env.sh && scripts/bake-chars.sh && scripts/bake-crowd.sh   # bakes (never committed)
DOTNET_CLI_USE_MSBUILD_SERVER=0 nice -n 19 dotnet build M7Gfx2.slnx -c Release -m:1 -nodeReuse:false
SDL_VIDEODRIVER=offscreen LIBGL_ALWAYS_SOFTWARE=1 dotnet test Gfx.Tests/Gfx.Tests.csproj -c Release --no-build
scripts/capture-render2.sh OUT_DIR                       # captures of §3
scripts/review-sheets-render2.py G1_CAPTURES OUT_DIR reports/captures/render2
scripts/bench-render2.sh BENCH_DIR                       # costs of §4 (llvmpipe without a GPU)
```
