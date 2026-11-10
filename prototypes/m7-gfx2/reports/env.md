# G2 stage env: one detailed battlefield environment

Stage agent env, branch `g2/env` (cut from `g2/trunk` 808d88c), 2026-11-10. Scope: DR45/DR49 priority 4, one
detailed battlefield environment, presentation only, new code in `Gfx.Proc/Environment`. All numbers below were
measured in this container on **llvmpipe (Mesa 25.2.8, LLVM 20.1.2, 4 CPUs): llvmpipe, not Victus evidence**. No
hardware GPU ran this code.

**Verdict, honestly.** The G1 lollipop trees, blob rocks and flat water are replaced on these maps by generated trees
of six species with bark and leaf-card materials, a material-driven ground cover, boulders and litter, a river with
flow, refraction and reflection, and two settlements built from a parametric kit. The before/after pairs are a large
step up from G1. It is **not production quality**: §8 lists what still looks placeholder (stepped river banks from the
4 m grid carving, simple settlement massing and textures, blurrier distant impostors, bluish shade, a bare approach to
the town gate). Nothing here was measured on a real GPU.

## 1. What changed

Everything is behind switches (§1.6); with all of them off the frame is bit-identical to the plain G1 terrain
scenery. 56 files, +11,032/−37 lines against 808d88c (5,509 of them C# in `Gfx.Proc/Environment`, the rest tests,
fixtures, the bake script and the 15 embedded presets).

### 1.1 Trees (`Environment/EzTree`, `Environment/Trees`)

- **ez-tree port.** `EzRng`, `EzMath`, `EzTreeOptions`, `EzTreeGenerator`: a C# port of ez-tree's generator
  (`src/lib`, MIT) at the pinned commit, double precision; its trellis growth is not ported (no preset uses it).
  The RNG reproduces JavaScript int32 semantics (multiply with carry, `ToInt32` wraps, arithmetic shifts). It matches an independent Python oracle (`scripts/ez-rng-oracle.py`,
  written from the ECMAScript rules, not from the C#) **bit for bit**: 1,920 values over 16 seeds, raw uint32 and the
  IEEE-754 bits of `random()` and `random(0.3, −0.3)` (`fixtures/ez-rng-oracle.txt`); the same file is produced by the
  original JavaScript under Node. The generated geometry matches the original JavaScript run under Node with three r180:
  while porting, all fifteen presets in full (largest difference 3.7·10⁻¹¹ relative to max(1, |value|), attributed to the
  two runtimes' sin/cos/atan2); in the committed test, four presets (vertex and index counts, the index buffers' SHA-256,
  and every 97th value of the position, normal and uv arrays within 10⁻⁹). The 15 presets are
  embedded unmodified and checked against their manifest pins.
- **Species** (`TreeSpecies`): temperate oak, ash, aspen (summer foliage), pine, shrub (hazel/hawthorn-like); arid
  scrub (maquis), juniper and arid pine. Each is a set of ez-tree presets with seeded variants and a few option
  modifiers (summer leaf density, scrub leaf size); 25 variants on the temperate map, 8 on the arid one.
- **Materials** (`TreeModel`, `TreeEffects`): bark from the ambientCG photo sets (CC0) with calibrated albedo, OpenGL
  normals, roughness and a cavity term, mapped in metres along the branch (no stretching on thin twigs); leaf cards
  from ez-tree's photo cards (MIT) with bled colour, calibrated albedo, a derived normal and per-texel translucency.
  Foliage shading: wrapped diffuse, low-F0 GGX at roughness 0.65, light transmitted through the leaf toward a
  backlit viewer, and a 20³ voxel leaf-area occlusion per model (7 directions) so inner leaves are darker.
- **Wind** (vertex shader): trunk sway, branch flutter by branch level and leaf flutter, phase per instance.
  Cosmetic: it moves vertices only.
- **Detail levels** (`ForestRenderer`): full mesh, a reduced mesh (`EzDetail.Lod1`: fewer sections and segments,
  fewer and larger leaves), and a hemi-octahedral impostor: 8×8 views on the upper hemisphere (outer ring
  horizontal), 128 px each (96 px for shrubs), baked 2× supersampled with albedo, octahedral normal, depth and
  occlusion atlases; depth is written per pixel (`gl_FragDepth`) so impostors intersect the ground and each other.
  Switch distances are 42 m and 135 m for a 20 m tree, scaled by height (×0.3 to ×1.35), and every switch is a
  **screen-space dithered cross-fade** over ±14 % of the distance with exactly complementary masks (no pop, no
  double-blend). Shadows and reflections use one level per tree.
- **Forest placement** (`ForestLayout`): from the grid's Forest/Jungle classes, a warped signed-distance field to the
  forest edge; interior trees on a jittered grid with clearings from a noise field; an edge mantle of shrubs and
  pioneer aspen/ash; solitary open-ground trees; arid maquis, juniper and pine on dry slopes. It keeps out of water,
  roads, settlements and other exclusions. The canopy cover it produces drives the forest floor (§1.2).

### 1.2 Ground (`Environment/Ground`)

- **Ground cover** (`GroundCover`): 16 cards drawn in code into one atlas (four grasses, two dry grasses, sedge, two
  fern fronds, daisy, buttercup, knapweed, poppy, dock, thistle, cushion plant); grass tufts are three crossed cards,
  ferns seven arching fronds. The card at each 0.33 m candidate is chosen from the terrain material weights there
  (meadow grasses with flowers in single-species patches; dry grass on dry ground; ferns in patches only where the
  forest floor is above 0.45; sedges in clumps 0.25–1.1 m above the water; nothing in water or within 0.25 m above
  it). Thinned with distance by a rank prefix (all within 14 m, down to 14 % at 55 m, none beyond 85 m), so a tile's
  instance list is drawn as one prefix. Wind sway as for trees.
- **Forest floor from the canopy** (`EnvironmentScenery.ForestFloorSplats`): the terrain's forest-floor weight follows
  the canopy cover of the placed crowns, not the grid cells; grassy talus slopes keep rock as noise outcrops only.
- **Boulders, pebbles, twigs** (`Rocks`): six boulder meshes (subdivided icosphere, noise, fracture planes) with
  render1's Rock023 bake triplanar, moss on upward faces, a damp base; two detail levels; pebbles and twigs as litter
  within 26 m.

### 1.3 Water (`Environment/Water`)

`FlowField` takes the river path from the generation trace and gives each water sample a flow vector (downstream,
faster in the channel, at most 1.1 m/s). The water surface (`EnvWater`) shades with two flow-mapped normal scales
(two phases, Vlachos 2010), calmer in shallows, wind ripples on still water; Schlick Fresnel; a planar reflection
(half resolution, oblique near plane clipped at the water level, rendered once per still) that falls back to the sky
table where it is not valid; refraction of the scene colour with Beer–Lambert absorption along the refracted path
(temperate turbid green-brown, arid clear); depth per pixel from the terrain's own triangulation, so the waterline is
continuous; foam in broken patches where the current presses on the bank (never a continuous line); a 20 cm soft
shore edge. The terrain shader darkens and glosses wet banks and silts the submerged bed from a water-level texture.

### 1.4 Structures (`Environment/Structures`)

A parametric kit (`StructureKit`, `StructureBuilder`) builds every piece from boxes, quads, tubes and cones with wall-
frame UVs in metres, fitted to the terrain (footings reach below the lowest ground under the footprint, floors sit
above the highest): walls with openings, reveals, arches with voussoirs and shutters; gable, hip and conical roofs
(thatch with thick eaves, verges and ridge roll; tiles with ridge caps); palisades, an Iron Age gate tower, round-
houses (age-tinted thatch, daub walls), granaries, pens, haystacks; Classical houses (lime plaster, tiled roofs,
stone plinths), a temple with columns, curtain walls with crenellations, towers, a town gate and paved streets. Ten
materials (`scripts/bake-env.py struct`): logs (oak bark), hewn wood, planks, wattle and daub, thatch, ashlar and
rubble and paving (drawn over Rock023), lime plaster, terracotta tiles. A world-space weathering term breaks up the
repeats (large-scale tone, streaks, thatch greying).

Settlements (`Settlements`), planned on the generated grid on dry, flat ground near a road (set dressing; the
authoritative grid does not know them):
- **Iron Age palisaded village** (temperate): a 46 m ring palisade with a gate tower facing the road, eleven
  roundhouses and a hall, granaries and pens, trampled ground. 37,382 triangles.
- **Classical stone-walled town** (arid): a 128 × 96 m circuit of ashlar walls with towers and a gate, a paved street
  grid, a temple, lime-washed houses of one and two floors. 39,446 triangles.
- A medieval style was **not** built (§9).

### 1.5 Scenes and shots (`EnvironmentScenes.cs`)

| Scene | Shots | Map |
|---|---|---|
| `env-trees` | lineup-oak-ash, lineup-aspen-pine, lineup-shrubs, oak-close, backlit, lod-sweep | the temperate fixture's flattest meadow strip, trees only |
| `env-temperate` | **forest-edge, vista, river**, river-high, **settlement, settlement-gate** | `terrain-temperate`'s fixture (forest, river, Iron Age village) |
| `env-arid` | **fortification, fortification-gate**, town-street, maquis, vista | the arid fixture (maquis, Classical town) |

Shots are found on the generated map at load (river path from the trace, settlement site, forest edge). Gate, street
and river shots pick the hour whose sun is behind the camera and to one side (`SunBehind`): the lost attempt's
gate shot metered on a wall in shade, and looking into the sun the far river reach becomes one glitter sheet.

### 1.6 Switches, engine hooks

`RenderFeatures` gains `trees`, `impostors`, `lodfade`, `wind`, `groundcover`, `rocks`, `water`, `reflect`,
`structures`, `forestfloor` (and `env` for all of them); `--preset g1` turns them all off. Engine changes, all inert
for content that does not use them: the HDR target preserves its contents; an `ITransparentContent` pass after the
opaque pass with an optional scene-colour copy; an `IPlanarReflectionContent` pass with an oblique projection
(`ForwardRenderer.ObliqueNearPlane`); `RenderContext` exposes the still sample index, viewport, reflection state and
the two extra textures. `TerrainScenery` gains alternative splats and look, a wet map and a prop filter (all unset by
default). `Gfx.Demo` prints load times and environment counts and `--bench --variants` benches several switch sets on
one loaded scene. **The existing scenes render bit-identically to trunk**: `terrain-temperate` (3 shots),
`battlefield` (4) and `render1-coast` (2) captured with a trunk build and with this build in the G2 frame (9 shots)
and through the G1 preset (7 shots): 16 of 16 identical (§6.3).

## 2. The lost attempt's lessons

| Lesson | What this stage does | Evidence |
|---|---|---|
| Impostor forest as a uniform canopy carpet from high cameras | Clearings, an edge mantle, species mix with conifers, solitary trees, per-instance tint and scale; impostors keep their own silhouette and depth | `env-05-vistas`; A/B `impostors-0` (scratchpad): with every far tree as a reduced mesh the vista looks the same |
| Brown cell-aligned forest-floor bands | Forest floor from the placed canopy, not the cells | `env-08-switches` (`forestfloor=0` shows the bands); test `ForestFloorFollowsTheCanopyNotTheCells` |
| Simple massing, repeating stone marks, flat black arrow slits, plank-like hall roof | Arched windows with reveals and voussoirs, a gate arch with doors, string courses, crenellations; ashlar drawn per block from different parts of the photo on a 6 m repeat plus world-space weathering; thatch with thick eaves, verges and ridge roll | `env-03`, `env-04` |
| Walled town reading as a generic castle | Lime-washed houses with tiled roofs inside, a temple, paved streets, rectangular towers with tiled hip roofs | `env-04` (still partly castle-like, §8) |
| River foam as a dashed line | Foam only in noise patches where the current presses on the bank, advected with the flow | `env-02` |
| Ferns in the shallows | Nothing grows in water or within 0.25 m above it; ferns only on forest floor; sedges in clumps at 0.25–1.1 m | test `GroundCoverFollowsTheMaterialStaysOutOfWaterAndThinsByRank` |
| Fortification-gate shot overexposed (metering on a shaded wall) | Gate shots lit from behind the camera (`SunBehind`) | `env-04` |
| Flowers as colour dots | Flower cards with stems and leaves, one species per patch, 30 % of the meadow candidates inside a patch | `env-01` (they still read as dots beyond about 15 m, §8) |
| Arid bushes greener than the land | Scrub foliage recoloured to dusty olive and tinted lighter; shrub impostors use a gentler occlusion-to-sun mapping (they were near black next to their meshes) | `env-05` (bushes now olive at distance; junipers and pines stay dark green) |
| Tree LOD pops at 42 m and 135 m | Same distances (scaled by tree height), but every switch is a dithered cross-fade with complementary masks | `env-07` (LOD sweep), test `LodLevelsCrossFadeWithExactComplements`, A/B `lodfade-0` |
| Single 2048 shadow map, blobby crowns | render1's four 2048² cascades; leaf cards and impostors cast alpha-tested shadows | `env-06` (dappled crown shadows) |

## 3. Assets and provenance

`assets/PROVENANCE.md` §3.9 lists every input, transformation and licence. All are SHIP-OK: six ambientCG bark sets
(CC0, via ez-tree), four ez-tree leaf cards (MIT), Terrain3D's Rock023 (CC0 via MIT), the 15 ez-tree presets (MIT,
pinned by this stage) and the ez-tree generator port (MIT; notice embedded next to the presets). The ground-cover
atlas, the water normal map and six of the ten structure materials are drawn in code. **No PROTOTYPE-ONLY asset is
used**: ez-tree's ground textures and `.glb` grass, flower and rock models are not read. `scripts/bake-env.sh`
re-checks every raw input's sha256 before baking; a second full bake reproduced all 45 outputs' sha256 (137 s).

## 4. Captures

Full-size PNGs (scratchpad, not committed):
`/tmp/claude-0/-home-user-civDemo/54b82655-610f-500e-a185-8fae64bdd108/scratchpad/m7gfx2/captures/env/final-set/`
(`scripts/capture-env.sh`, final build; the `env-trees` images re-captured after the line-up fix, which put the
aspen, pine and shrub line-ups on the meadow, where the earlier framing had shown only oaks and ashes): `final/` the
17 deliverable stills (16-sample jittered accumulation),
`before-g2-env-off/` the same cameras with the environment off (the G1 content in the render1 frame, 16 samples),
`before-g1-preset/` the same cameras through the G1 preset (G1 frame, G1 content), `ab/<switch>/` one-switch-off
frames (one sample, FXAA) against `ab/on/`. Pristine G1 has no environment scenes, so these cameras' BEFORE frames
come from G2 with the environment off; the G1-preset frames of existing scenes match pristine G1 to 1/255 (render1
report §6.3).

Review JPEGs (committed, `reports/captures/env/`): see §7.

## 5. Costs (llvmpipe, not Victus evidence)

`scripts/bench-env.sh` (whole run 997 s): 1280×720, one sample with FXAA (the interactive frame, not the 16-sample
still), 10 measured frames after 3 warm-up frames, p50 and p95 of the CPU frame time with a GPU sync per frame. All
variants of a shot run on one loaded scene (`--variants`). **llvmpipe, not Victus evidence.** The noise between
variants is about ±140 ms here (`rocks=0` on the river measured +94 ms and `forestfloor=0` on the vista −136 ms, both
switches that remove work or change no geometry), so only larger deltas are resolved.

### 5.1 Frames and per-switch deltas

**env-temperate forest-edge**

| Variant | p50 ms | p95 ms | Δ p50 vs all on | Draws | Triangles |
|---|---:|---:|---:|---:|---:|
| all on (G2 frame + environment) | 2244 | 2451 | — | 590 | 8,071,584 |
| `env=0` (G1 content in the G2 frame) | 584 | 657 | −1660 | 159 | 1,423,629 |
| `--preset g1` (G1 frame and content) | 119 | 128 | −2125 | 65 | 634,526 |
| `trees=0` | 708 | 774 | −1536 | 245 | 1,847,414 |
| `impostors=0` | 9423 | 9842 | +7179 | 481 | 87,177,122 |
| `lodfade=0` | 2068 | 2344 | −176 | 584 | 7,834,866 |
| `wind=0` | 2154 | 2273 | −90 | 590 | 8,071,584 |
| `groundcover=0` | 2047 | 2235 | −197 | 541 | 7,892,038 |
| `rocks=0` | 2202 | 2439 | −42 | 582 | 7,976,874 |
| `forestfloor=0` | 2156 | 2257 | −88 | 590 | 8,071,584 |

**env-temperate river**

| Variant | p50 ms | p95 ms | Δ p50 vs all on | Draws | Triangles |
|---|---:|---:|---:|---:|---:|
| all on (G2 frame + environment) | 1375 | 1441 | — | 453 | 5,055,581 |
| `env=0` (G1 content in the G2 frame) | 405 | 466 | −970 | 149 | 1,098,272 |
| `--preset g1` (G1 frame and content) | 79 | 115 | −1296 | 60 | 295,651 |
| `water=0` | 1285 | 1359 | −90 | 428 | 4,993,477 |
| `reflect=0` | 1379 | 1440 | +4 | 429 | 4,993,478 |
| `groundcover=0` | 1351 | 1473 | −24 | 411 | 4,880,627 |
| `rocks=0` | 1469 | 1581 | +94 | 430 | 4,725,021 |
| `trees=0` | 566 | 648 | −809 | 245 | 1,694,883 |

**env-temperate vista**

| Variant | p50 ms | p95 ms | Δ p50 vs all on | Draws | Triangles |
|---|---:|---:|---:|---:|---:|
| all on (G2 frame + environment) | 1136 | 1163 | — | 215 | 620,254 |
| `env=0` (G1 content in the G2 frame) | 651 | 682 | −486 | 106 | 1,172,545 |
| `--preset g1` (G1 frame and content) | 101 | 117 | −1035 | 55 | 481,002 |
| `trees=0` | 688 | 753 | −448 | 128 | 1,404,600 |
| `impostors=0` | 17482 | 18761 | +16346 | 294 | 148,300,268 |
| `lodfade=0` | 1093 | 1193 | −43 | 215 | 620,254 |
| `wind=0` | 1098 | 1216 | −38 | 215 | 620,254 |
| `groundcover=0` | 1144 | 1263 | +7 | 215 | 620,254 |
| `rocks=0` | 1006 | 1132 | −130 | 208 | 462,964 |
| `forestfloor=0` | 1000 | 1114 | −136 | 215 | 620,254 |
| `structures=0` | 1126 | 1214 | −10 | 201 | 545,490 |

**env-arid fortification-gate**

| Variant | p50 ms | p95 ms | Δ p50 vs all on | Draws | Triangles |
|---|---:|---:|---:|---:|---:|
| all on (G2 frame + environment) | 1106 | 1197 | — | 177 | 840,706 |
| `env=0` (G1 content in the G2 frame) | 825 | 876 | −281 | 147 | 621,197 |
| `--preset g1` (G1 frame and content) | 80 | 90 | −1026 | 59 | 239,992 |
| `structures=0` | 1031 | 1198 | −75 | 147 | 643,476 |
| `trees=0` | 984 | 1042 | −122 | 189 | 987,620 |
| `groundcover=0` | 1110 | 1267 | +4 | 174 | 836,344 |
| `rocks=0` | 1101 | 1182 | −6 | 169 | 675,876 |

Instances drawn in the all-on frames (`[env] last frame`): forest edge 5 full + 126 reduced + 1,863 impostor trees
(1.10 M tree triangles), 27,564 cover cards in 33 tiles, 86 litter pieces, 206 boulders; river 4 reduced + 32
impostor trees, 13,502 cover cards, 73 boulders; vista 8,141 impostor trees, no cover (beyond 85 m), 858 boulders;
fortification gate 328 impostor trees, 447 cover cards, 97 boulders.

### 5.2 Per-pass profile (forest edge, all on, GPU sync after each pass)

| Pass | ms per frame |
|---|---:|
| shadows (four cascades; trees, cover and boulders cast) | 1,234 |
| scene (terrain, trees, boulders, structures, cover) | 1,091 |
| composite, FXAA, bloom down/up, exposure | 42 |
| reflection (no water in view: skipped) | 0.05 |

### 5.3 Load

| | env-temperate (4 loads) | env-arid (1 load) |
|---|---:|---:|
| Whole scene load | 23.6–24.8 s | 7.8 s |
| Impostor bake (GPU) | 20.6–21.6 s (25 variants) | 5.0 s (8 variants) |
| Tree models (CPU: ez-tree, two reduced levels, occlusion voxels) | 1.18–1.24 s | 0.48 s |
| Forest layout | 0.61–0.87 s | 0.99 s |
| Settlement plan and geometry | 71–103 ms | 111 ms |
| Content | 11,505 trees, 1,115 boulders, village 37,382 tris, water 4,382 tris | 3,818 trees, 2,114 boulders, town 39,446 tris, water 14,782 tris |

Ground-cover tiles are generated when first drawn (16 m tiles, cached). The bake (`scripts/bake-env.sh`, CPU only)
took 137 s for 45 outputs.

### 5.4 Reading the costs

- On llvmpipe the environment multiplies the frame time by 3.8 at the forest edge (584 → 2,244 ms), 3.4 at the river,
  1.7 on the vista and 1.3 at the town gate. Trees are most of it (−1,536 ms without them at the forest edge), and of
  the trees most is shadow casting: the four cascades redraw every tree within their range with alpha-tested leaf
  cards (shadows 1,234 ms against 1,091 ms for the whole scene pass).
- Impostors are what makes a forest affordable at all: without them the forest edge draws 87 M triangles (9.4 s) and
  the vista 148 M (17.5 s), 4× and 15× the frame with them.
- Ground cover costs about 200 ms at the forest edge (27,564 cards); the water surface, reflection, wind, LOD fades,
  boulders, forest floor and structures are each below the noise here.
- llvmpipe rasterises on 4 CPU cores, so these ratios say nothing reliable about a GPU, where fill rate (cards and
  impostors overlapping), alpha-tested shadow casters and draw-call count weigh differently. `Gfx.Demo --bench` and
  the perf harness's `--scene <name>` both take any registered scene, so these scenes can be measured on the Victus;
  the harness was not run on an env scene here, and no hardware measurement exists.

## 6. Tests, builds, checks

### 6.1 Builds

`dotnet build M7Gfx2.slnx -c Debug|Release -m:1 -nodeReuse:false --no-incremental` (`nice -n 19`,
`DOTNET_CLI_USE_MSBUILD_SERVER=0`) on the committed tree of `afc4920` (the later commit changes only this report),
all eight projects compiled: **Debug: 0 warnings, 0 errors. Release: 0 warnings, 0 errors.** No warning to list.

### 6.2 Tests

`dotnet test Gfx.Tests` on each of those builds (GPU tests on llvmpipe; the env and render1 bakes present, so nothing
skipped): **Debug: 198 passed, 0 failed, 0 skipped (2 min 30 s). Release: 198 passed, 0 failed, 0 skipped (1 min 10 s).**
Trunk had 177; this stage adds 21 in three new classes and changes no existing test file.

| Class | Tests (each config) | Class | Tests (each config) |
|---|---:|---|---:|
| AvatarTests | 18 | **EnvTreeTests** (new) | 6 |
| CameraTests | 8 | FrustumTests | 5 |
| CinematicTests | 26 | IntegrationTests | 2 |
| EffectLoadTests | 4 | PerfDeviceTests | 10 |
| EngineUnitTests | 6 | PerfProcessTests | 5 |
| **EnvLayoutTests** (new) | 12 | PerfUnitTests | 41 |
| **EnvRenderTests** (new) | 3 | Render1MathTests | 17 |
| RenderTests | 2 | Render1RenderTests | 7 |
| TerrainTests | 15 | UnitsTests | 11 |

**A finding on the way:** the first Debug run had one failure, `PerfDeviceTests.WarmupLastsAtLeastItsMinimumTime`
("warm-up frames 2"), a wall-clock test of the perf stage. Alone it passed 3 times of 3. The new env classes build
forests and tree models on the CPU and ran in parallel with it. They now share an xunit collection with
`DisableParallelization` (they run after the parallel phase); the full runs above are after that change, and a run
between them on the same code also passed 198 of 198 in both configurations. The perf test itself is unchanged.

What the new tests pin:

- `EnvTreeTests` (CPU): the RNG equals the Python oracle bit for bit (1,920 values); `random(max, min)` follows the
  original's formula; `JsRound` rounds halves up like JavaScript; the generator reproduces the JavaScript reference
  for four presets (counts and index-buffer SHA-256 equal, sampled positions, normals and uvs within 10⁻⁹); the embedded presets
  are the pinned files (SHA-256); generation is deterministic.
- `EnvLayoutTests` (CPU): forest layout is deterministic and keeps out of water, roads and exclusions; the forest has
  an edge mantle, clearings and a species mix; the forest floor follows the canopy, not the cells (crowns outside
  forest cells get forest floor); LOD cross-fades have exactly complementary weights and no gaps; impostor view
  directions round-trip and cover the horizon; ground cover follows the material, stays out of water and 0.25 m
  above it, sedges only near water, and thins by rank prefix; the river flow runs downstream; the oblique projection
  clips exactly at the water plane; structures stand on the ground and reach below it; settlements are planned
  deterministically on dry ground; switches parse and the G1 preset turns them off; tree models are built in metres
  with canopy occlusion and wind weights.
- `EnvRenderTests` (llvmpipe): the environment never writes the grid and renders repeatably (§10); with every switch
  off it draws exactly the plain G1 terrain scenery (0 changed pixels, G2 frame and G1 preset) and with them on it
  changes more than a quarter of the frame; every impostor atlas frame has coverage.

### 6.3 Existing scenes unchanged

`terrain-temperate` (3 shots), `battlefield` (4) and `render1-coast` (2) captured with a trunk (808d88c) Release
build and with this stage's final build, G2 frame, plus `terrain-temperate` and `battlefield` through `--preset g1`:
**16 of 16 images bit-identical.**

### 6.4 Repeatability

Two full runs of `scripts/capture-env.sh` in separate processes (the second after the gate-camera and arid-cover
changes): 46 of the 48 images shared by both runs are bit-identical; the two that differ (arid fortification, 212
pixels; maquis, 1,839 pixels) are exactly the frames the arid-cover change touches. The env-trees images were
re-captured after the line-up fix (§4).

### 6.5 Other checks

- `scripts/check-gfx-determinism.sh`: OK; this stage changed no file in `Gfx.Proc/Authoritative`. No `System.Random`,
  `DateTime.Now`/`UtcNow`, `AsParallel`, `Parallel.*` or `GetHashCode()` in `Gfx.Proc/Environment`; its dictionaries
  are lookups (model cache, textures, ground-cover tiles, icosphere midpoints), and tree batches are grouped in a
  `SortedDictionary` with ordinal keys.
- The bake is reproducible (45 outputs, sha256-identical on a second run) and checks its inputs against the pins.
- `prototypes/m7-gfx1` is unchanged against `1601c74` (empty diff).

## 7. Review JPEGs

Eight labelled JPEGs in `reports/captures/env/` (1,256,121 bytes in all, the largest 191,892 bytes), made by
`scripts/review-sheets-env.py` from the final captures. Every legend: llvmpipe, not Victus evidence; no
PROTOTYPE-ONLY assets; BEFORE = the same camera with the environment off.

| JPEG | Bytes | Shows |
|---|---:|---|
| `env-01-forest-edge.jpg` | 128,756 | Forest edge BEFORE/AFTER: edge mantle, aspen and ash pioneers, meadow cover with flower patches, the village beyond |
| `env-02-river.jpg` | 189,868 | River from the bank and from the valley side, BEFORE/AFTER: refraction, reflection of the hills, clumped sedges, boulders; the stepped bank (§8) |
| `env-03-iron-age-village.jpg` | 135,937 | Village and gate BEFORE/AFTER: palisade ring, roundhouses, gate tower with thick thatch |
| `env-04-classical-town.jpg` | 114,942 | Town and gate BEFORE/AFTER: ashlar curtain wall, towers, arched gate and windows, tiled roofs |
| `env-05-vistas.jpg` | 144,842 | Both vistas BEFORE/AFTER: impostor forest with clearings from 120 m up; arid maquis with olive scrub |
| `env-06-trees.jpg` | 168,894 | Species line-ups (oak, ash, aspen, pine, shrubs), oak close up, backlit translucency, arid maquis |
| `env-07-ground-lod.jpg` | 180,990 | LOD sweep to 290 m, the town street, ground cover in the G2 frame against the same camera through `--preset g1` |
| `env-08-switches.jpg` | 191,892 | One switch off at a time: trees, ground cover, water (render1 water), reflection, structures, forest floor (cell-based bands) |

## 8. What still looks placeholder

Looked at in every final capture (full size) and the A/B frames:

- **River banks step.** The authoritative grid carves the river in 4 m cells with Chebyshev steps; the water's edge
  follows the real terrain, so on diagonal reaches the bank is a staircase with dark risers (`env-02`, river-high).
  The render1 water (`water=0`) hides much of it because its surface reaches further up the bank. Fixing it needs a
  presentation-side smoothing of the drawn bank heights (and the same surface for everything placed on it), not done
  here.
- **Settlements are simple.** The village's roundhouse roofs are plain bell cones of one thatch texture; the town's
  houses are boxes with a few windows, one door and a tiled roof; the temple is a box with columns; there are no
  people, carts, smoke, gardens, wells or clutter. Wood grain on the palisade and gate is drawn and reads
  cartoonish close up. The town's square towers and crenellations still read partly as a castle.
- **The approach to the town gate is bare**: the trampled ground in front of the wall has almost no cover and the
  rise in front hides the foot of the wall (`env-04`).
- **Impostors are softer than meshes** past 135 m (8×8 views at 128 px): crowns lose their leaf detail and read as
  rounded blobs from high cameras; from 300 m the forest reads as a dense field of rounded crowns with clearings.
- **Shade is blue.** Bark, rock and wall faces away from the sun take the blue sky light without ground bounce, so
  trunks and shaded facades look blue-grey (render1's lighting, which this stage did not change).
- **Flowers** read as coloured dots beyond about 15 m; the dock seed heads read as orange sticks.
- **Arid ground** keeps render1's patchy sand and grey rock blotches; the maquis ground between tufts is dark.
- **Talus slopes** above the river show render1's rock as pale, smooth patches that read like concrete from afar.
- **Shrub and tree variety** is limited to 25 temperate and 8 arid variants of ez-tree presets; there are no dead
  trees, stumps, fallen logs or saplings.

## 9. Limitations and risks

- **Medieval settlement not built.** The kit has the pieces (curtain walls, towers, gates, gable roofs) but no
  medieval layout, materials or buildings were made.
- **Set dressing only.** Settlements, trees and rocks are not in the authoritative grid: units would walk through them
  and line of sight ignores them. The settlement exclusions keep trees and cover off the site, nothing more.
- **Fixed scenes.** Shots are computed for the two fixture maps; another map gets the same procedures, but the
  settlement site search always takes the best-scoring disc, so on a map with no flat dry ground it would build on
  the least bad, wet or steep, site (there is no refusal).
- **Planar reflection for one plane.** The reflection is rendered for the water level nearest the camera; reaches at
  other levels use it when the error is small relative to their distance (a stepping river far away) and fall back to
  the sky table otherwise. It draws the scene a second time at half resolution (in the river bench frame 24 more
  draws and 62,000 more triangles; its time is below the noise on llvmpipe, §5).
- **Refraction uses the scene colour**, so objects in front of the water can bleed into its refraction at their edges
  (no depth-aware rejection).
- **Load time.** Impostor atlases are baked on the GPU at load (25 variants: §5); on llvmpipe this dominates the
  scene load. Nothing is cached on disk.
- **Wind is visual only** and has no gusts; grass and trees share one wind direction.
- **Two shader hashes:** the env shaders use `envHash` (PROVENANCE §3.9, MIT) instead of the `fract(sin(x))` hash,
  whose argument loses its fractional bits at world coordinates of hundreds of metres in float32; no visible artefact
  was traced to it here (the banded arcs on the town facades were shadow self-occlusion, fixed by offsetting the
  shadow lookup out of the visible side). render1's terrain shader still uses the sine hash (not changed here).
- **No Victus numbers.** Every cost here is llvmpipe; the GPU-side costs (fill rate of cards and impostors, overdraw,
  shadow cascades with alpha-tested foliage) are unmeasured on hardware.

## 10. Sim/presentation boundary

- The environment reads the authoritative grid (`BattleTerrainGrid`) and the generation trace (river path) and never
  writes them. `EnvironmentNeverWritesTheGridAndRendersRepeatably` builds the whole environment (with a settlement),
  renders a settlement view and a river view (water, refraction and reflection on) twice each, and checks that the
  repeated renders are bit-identical, that the grid's SHA-256 is unchanged afterwards, and that a fresh generation
  from the same inputs has the same hash.
- Settlements, trees, cover and rocks are set dressing: the simulation does not know them, so a unit can walk through
  a roundhouse. Making them matter (cover, line of sight, passability) is a simulation decision for a later lane, not
  something presentation can add.
- Wind, water flow and foam animate with the scene time only; they touch no result, position, health, RNG, timing or
  hash. All generation is seeded from the grid seed and positions (hashes), never from `System.Random` or the clock.
- `Gfx.Proc/Authoritative` is unchanged (`scripts/check-gfx-determinism.sh` OK).

## 11. Files

| Path | What |
|---|---|
| `Gfx.Proc/Environment/EzTree/` | ez-tree port (`EzRng`, `EzMath`, `EzTreeOptions`, `EzTreeGenerator`), the 15 presets and the MIT notice |
| `Gfx.Proc/Environment/Trees/` | `TreeSpecies`, `TreeModel` (meshes in metres, canopy occlusion), `TreeEffects`, `ImpostorEffects`, `TreeVariantGpu` (GPU buffers, impostor bake), `ForestRenderer` (LOD assignment, cross-fade, instancing), `ForestLayout` |
| `Gfx.Proc/Environment/Ground/` | `GroundCover` (cards, choice, thinning, litter), `Rocks` (boulders, twigs, renderer) |
| `Gfx.Proc/Environment/Water/EnvWater.cs`, `WaterField.cs` | Flow field, water surface and shader; nearby water level per sample |
| `Gfx.Proc/Environment/Structures/` | `StructureMesh` (materials, builder), `StructureKit` (pieces), `StructureRenderer`, `Settlements` (village, town) |
| `Gfx.Proc/Environment/EnvironmentScenery.cs`, `EnvironmentScenes.cs`, `EnvGlsl.cs`, `EnvTextures.cs` | The environment as one piece of scenery, the three scenes and their shots, shared shader code, baked texture loading |
| `Gfx.Engine/Rendering/ForwardRenderer.cs`, `RenderContext.cs`, `RenderSettings.cs` | Transparent and planar-reflection passes, scene-colour copy, oblique projection, the environment switches |
| `Gfx.Proc/Presentation/TerrainScenery.cs`, `G2TerrainEffects.cs` | Alternative splats and look, wet map, prop filter (all inert unless set) |
| `Gfx.Demo/Program.cs` | Load and environment reports; `--bench --variants` |
| `Gfx.Tests/EnvTreeTests.cs`, `Gfx.Tests/EnvTests.cs` | The tests of §6.2 |
| `fixtures/ez-rng-oracle.txt`, `fixtures/ez-tree-js-reference.json`, `scripts/ez-rng-oracle.py` | Port references |
| `scripts/bake-env.sh`, `scripts/bake-env.py` | The bake |
| `scripts/capture-env.sh`, `scripts/review-sheets-env.py`, `scripts/bench-env.sh` | Captures, review JPEGs, costs |
| `assets/manifest.tsv` (+15 preset pins), `assets/PROVENANCE.md` §3.9 and §5, `README.md` | Pins, provenance, how to run |
| `reports/env.md`, `reports/captures/env/` | This report and its review JPEGs |
