# G2 stage render1: renderer core upgrade

Stage agent render1, branch `g2/render1`, 2026-11-09. Scope: Gfx.Engine rendering and shaders, plus the terrain,
water and far-field shading in Gfx.Proc that the task names (D-078 QM7; D-079 DR46). All numbers below were measured
in this container on **llvmpipe (Mesa 25.2.8, LLVM 20.1.2, 4 CPUs): llvmpipe, not Victus evidence**. No hardware GPU
ran this code.

**Verdict, honestly.** The sky, light, atmosphere, terrain surfaces, water, shadows and image pipeline now use
physically based models and photographic ground materials, and the before/after pairs show a large step up from G1.
They are not finished: §7 lists what still looks wrong (a dusk battle shot that reads cool, patchy arid ground,
blotchy far-field land, a faint seam in the coast water). The frames are still dominated by G1's procedural low-poly
trees, bushes, rocks, soldiers and avatars, which other G2 stages own; those still look placeholder, and no render1
change hides that. Nothing here is production quality.

## 1. What changed

Two frame paths now exist in `ForwardRenderer`:

- **G1 frame** (`--preset g1`): G1's shadow map, gradient sky, Lambert shading, height fog and ACES-fit post. Kept as
  the BEFORE path inside G2. It now shares its shaders with the G2 branches (uniform switches), and it matches
  pristine G1's captures to within 1/255 on a few pixels per shot (§6.3).
- **G2 frame** (default), stage by stage:
  1. **Analytic atmosphere** (`Atmosphere/AtmosphereModel.cs`, `Lighting/SceneLighting.cs`): Rayleigh, Mie and ozone
     single scattering ray-marched on the CPU against a precomputed transmittance table, plus Hillaire's (2020)
     isotropic multiple-scattering term; Earth coefficients from Bruneton (2017) and Hillaire (2020). Output: a
     192×108 sky-view table (azimuth from the sun × elevation, horizon-weighted). The sun light is the
     atmosphere-transmitted sun (white-balanced at 60°, so it reddens physically towards the horizon); the sky
     irradiance comes from the same table. The sky shader draws the table plus a limb-darkened sun disc.
  2. **Aerial perspective and height fog** (`GlslLib.aerialPerspective`): a homogeneous air layer at the camera's
     altitude, L = L0·T + J(θ)·(1 − T), with J built from the Rayleigh and Mie phase functions and the multiple-scattering
     term of the same model, so haze is bluish by day and glows towards a low sun; then G1's exponential height fog
     (density ×0.35), lit by the sun (Henyey-Greenstein, g = 0.55) and the sky. Haze is a per-biome aerosol multiplier
     (temperate 3, arid 3.5, tropical 4, cold 2).
  3. **Far field** (`Gfx.Proc/Presentation/FarField.cs`): land and, on coastal maps, sea from the map edge to 30 km.
     The inner ring uses the authoritative edge heights (read only); outward it blends into biome hills and distant
     ridges, continues the map's coastline, and fades into the horizon colour at its outer edge. G2 frames use a
     40 km far plane (G1 scenes set 3–4 km, which cut the far field off).
  4. **PBR shading** (`GlslLib.shadePbrFull`): metal/rough, GGX distribution, height-correlated Smith visibility,
     Schlick Fresnel; normal, roughness, metallic and AO inputs; horizon fade for normal-mapped reflections; specular
     occlusion from AO. Every G1 effect gets it through the unchanged `shadeLit` signature (dielectric, roughness 0.75).
  5. **Image-based lighting** (`Lighting/IblMath.cs`): GGX prefiltering with filtered importance sampling into a
     256×128 equirect mip chain (mip k = roughness k/5), nine irradiance SH coefficients, and a 32×32 split-sum BRDF
     table; sources: the analytic sky (default) or the CC0 HDRI (`ibl=hdri`; sun clamped out, turned to the analytic
     sun's azimuth, scaled to the analytic sky's irradiance).
  6. **Cascaded shadows** (`Rendering/CascadedShadowMap.cs`): four cascades in one 4096² atlas, one shared light view
     (cascade k's clip position is an affine function of cascade 0's, so each vertex computes one light-space
     position), bounding-sphere fits snapped to texels, practical split scheme to 1,600 m, depth range stretched
     towards the sun to every caster of the scene, 16-tap tent PCF, blended cascade borders.
  7. **Post** (`Effects/PostEffects.cs`): 13-tap Karis-averaged bloom down chain and tent up chain (energy-conserving
     4.5 % lerp); centre-weighted log-luminance metering with partial adaptation (target = 0.8·mean + 0.2·reference)
     and temporal smoothing (separate brighten/darken rates); tonemappers: PBR Neutral (default), ACES (Hill fit) and
     AgX, ported from three.js r180 (MIT), plus G1's ACES fit for comparison; a 32³ 3D LUT applied in display space
     (a light house look; `.cube` files load); FXAA-style edge AA for interactive frames.
  8. **Stills** (`ForwardRenderer.RenderStill`): N frames with a deterministic Halton(2,3) sub-pixel jitter, averaged in
     linear display space (exposure metered once, shadows rendered once). Captures use 16 samples.
  9. **Photographic terrain** (`G2TerrainEffects.TerrainPbr`, `TerrainMaterials.cs`, `scripts/bake-render1.sh`): five
     baked sets (grass Ground037, gravel rockyGround, rock Rock023, sand, soil), each albedo+height and
     normal+roughness with full mip chains; albedo calibrated to natural means (§3); ×16 anisotropic filtering;
     height-blended transitions; macro brightness and hue variation; hex-tiling on every set (rock biplanar on
     steep faces, rotation limited on cliffs so strata stay level); explicit-gradient sampling so hex cells have no
     mip seams; per-biome tints for the eight splat materials.
  10. **Water** (`G2TerrainEffects.WaterPbr`): five ripple octaves that fade once a wavelength spans fewer than ~4
      pixels (the removed slope variance widens the sun glint instead of aliasing), sky-table reflections with
      Fresnel, two-way depth absorption, and an opaque far sea.

Every part is a switch (`--set name=value`, list in `Gfx.Demo --help`; the windowed explorer toggles them with G and
1–9). Showcase scenes: `render1-materials` (PBR spheres), `render1-closeups` (cliff, grazing meadow, gravel track) and
`render1-coast` (beach and coast).

## 2. The lost attempt's lessons, fixed up front

| Lesson | Fix | Evidence |
|---|---|---|
| High shots showed a grey (day) or flat orange (dusk) wall beyond the 1 km map | Far field of land and sea to 30 km, aerial perspective, horizon fade, 40 km far plane | `r1-01`, `r1-03`, `r1-13` (farfield off/on); test `FarFieldStartsOnTheMapEdgeAndOnlyCoastalMapsGetASea` |
| Distant water showed a moiré grid | Ripple octaves fade by wavelength against the pixel footprint; lost variance roughens the glint | `r1-11` (ripplefade off/on) |
| Sunlit photographic ground brighter than the sky (the sky read grey) | Physically based sky radiance; Lambert with 1/π; albedo calibrated (grass 0.105, soil 0.13, gravel 0.19, rock 0.21, sand 0.30 linear) | §6.4 luminance check; `r1-01`, `r1-06` |
| Dry ground and rock mirrored the blue sky | Roughness floors (rock 0.72–0.97, grass ≥ 0.62), split-sum DFG, horizon fade, specular occlusion | `r1-08`, `r1-12` |
| Rock cliffs showed a repeating brick grid | Hex-tiling (Mikkelsen 2022) with rotation limited on cliff faces; biplanar rock | `r1-08` (hex off/on) |
| Dusk shadows were cut off: the shadow map cast only ~400 m towards the sun | Cascades stretch their light-space depth range to every caster of the scene | `r1-14`, `r1-02`; test `CascadesCoverTheirSlicesAndReachCastersTowardsTheSun`. The battlefield's own 17:42 shot still does not read as dusk (`r1-05`, §7) |

## 3. Assets and provenance

All inputs are existing SHIP-OK rows; PROVENANCE.md §3.7 records every transformation. Nothing PROTOTYPE-ONLY is used
by the renderer core, so no render1 capture needs a PROTOTYPE-ONLY legend.

- Terrain bake (`scripts/bake-render1.sh`, sha256-checked inputs, outputs in `$G2_ASSETS/baked/render1/terrain`, never
  committed): 10 R1TX files of 5,592,428 bytes each, in about 4 s (4.3 s on the last run). Measured upstream → baked mean linear albedo: grass 0.312 →
  0.105, gravel 0.328 → 0.19, rock 0.269 → 0.21, sand 0.371 → 0.30, soil 0.202 → 0.13. Normal conventions verified
  by correlating each normal map with its height gradient (Terrain3D sets +0.78 to +0.83: OpenGL; rockyGround −0.42 on
  the Y channel against an albedo-luminance height proxy: DirectX, flipped to OpenGL). A second bake into a scratch
  folder reproduced all 10 outputs byte for byte (same sha256).
- HDRI: Poly Haven "Harties cliff view" 4K (CC0, via BabylonJS/Assets), transformed in memory only.
- New pinned reference: three.js r180 `tonemapping_pars_fragment.glsl.js` + LICENSE (`threejs-ref`, MIT).
- CC-BY-4.0 notice applies to the gravel, sand and soil sets (BabylonJS/Assets); MIT notices to Terrain3D and three.js.

## 4. Captures

Full-size PNGs (scratchpad, not committed):
`/tmp/claude-0/-home-user-civDemo/54b82655-610f-500e-a185-8fae64bdd108/scratchpad/m7gfx2/captures/render1/` —
`g1/` pristine G1 (BEFORE); from the final build (`scripts/capture-render1.sh`): `final2/g2/` G2 16-sample stills,
`final2/g1path/` the G1 frame path of the new scenes, `final2/ab/<switch>/` one-switch-off frames (`final/` is the
earlier run, bit-identical); `g2-preset-g1-final2/` all 69 G1 shots through the G1 frame path; `sheets-final/` the
review JPEGs; `inspect-final/` contrast-stretched crops used for §7. Review JPEGs (committed,
`reports/captures/render1/`): see §7.

Where each feature's before/after and cost are (A/B frames under `final2/ab/`, all from the final build):

| Feature | Switch (off, or an alternative) | Review JPEG | A/B frames | Cost (§5) |
|---|---|---|---|---|
| Analytic sky | `sky=legacy` | `r1-01`, `r1-02`, `r1-13` | `sky-legacy/` | table B |
| Aerial perspective, height fog | `aerial=0` | `r1-01`, `r1-02`, `r1-13` | `aerial-0/` | table B |
| Far field (land and sea) | `farfield=0` | `r1-01`, `r1-03`, `r1-13` | `farfield-0/` | table B |
| PBR shading | `pbr=0` | `r1-07` | `pbr-0/` | table B |
| IBL (sky, HDRI) | `ibl=none`, `ibl=hdri` | `r1-07` | `ibl-none/`, `ibl-hdri/`, `ibl-hdri-bg/` | table B |
| Cascaded shadows | `shadows=single`, `cascades=2` | `r1-02`, `r1-14` | `shadows-single/` | tables A, B; §5.2 |
| Tonemappers | `tonemap=acesfit\|aces\|agx\|linear` | `r1-10` | `tonemap-*/` | table B; §5.2 |
| Auto exposure | `autoexposure=0` | `r1-15` | `autoexposure-0/` | table B; §5.2 |
| Bloom | `bloom=0` | `r1-15` | `bloom-0/` | table B; §5.2 |
| 3D LUT grade | `grade=0` | `r1-15` | `grade-0/` | table B; §5.2 |
| FXAA; jittered stills | `aa=none`; `samples=1` | `r1-09` | `aa-none/` | table B; §5.2, §5.3 |
| Photographic terrain | `terrain=procedural` | `r1-04`, `r1-06`, `r1-12`, `r1-16` | `terrain-procedural/` | table A |
| Anisotropic filtering ×16 | `aniso=1`, `aniso=8` | `r1-06`, `r1-16` | `aniso-1/` | table A |
| Hex-tiling | `hex=0` | `r1-08` | `hex-0/` | table A |
| Macro variation | `macro=0` | `r1-16` | `macro-0/` | table B |
| Water ripple fade | `ripplefade=0` | `r1-11` | `ripplefade-0/` | table B |

## 5. Costs (llvmpipe, not Victus evidence)

1280×720, 20 measured frames after 3 warm-up frames, p50 of CPU frame time with a GPU sync (`scripts/bench-render1.sh`).
Run-to-run noise here is about ±50 ms on these frames (`autoexposure=0`, which removes work, measured +51 ms):
smaller per-switch deltas are not resolved by frame times, so the synced per-pass profile (§5.2) is the measure for
the small passes.

### 5.1 Frames and per-switch deltas

**A. Final build** (`b6bed59`; p50 ms, in brackets the change from G2 with every switch on):

| Variant | temperate day | battlefield follow-low | materials noon | coast high |
|---|---:|---:|---:|---:|
| G1 frame (`--preset g1`) | 104 | 127 | 90 | 53 |
| **G2, every switch on** | **689** | **488** | **604** | **496** |
| shadows=single (G1's one map) | 561 (-128) | 366 (-122) | 533 (-71) | 452 (-44) |
| terrain=procedural (G1 materials, G2 light) | 390 (-298) | 336 (-152) | 291 (-312) | 230 (-266) |
| aniso=1 (trilinear only) | 356 (-332) | 323 (-165) | 292 (-312) | 234 (-262) |
| aniso=8 | 522 (-166) | 450 (-38) | 540 (-64) | 349 (-146) |
| hex=0 (no hex-tiling) | 399 (-290) | 405 (-83) | 449 (-154) | 274 (-222) |
| p95, G1 frame / G2 | 123 / 747 | 142 / 518 | 112 / 643 | 66 / 529 |
| Draws, G1 frame / G2 | 53 / 102 | 71 / 180 | 63 / 140 | 53 / 129 |
| Triangles, G1 frame / G2 | 400,292 / 1,094,459 | 422,851 / 1,551,952 | 444,916 / 948,215 | 185,082 / 403,377 |
| G2 / G1 frame | 6.6× | 3.8× | 6.7× | 9.4× |

**B. Every other switch** (first run, before the hex-tap optimization and the last terrain and water changes; p50 ms with the change from that run's G2 with every switch on). Changes under about ±50 ms are within this container's run-to-run noise:

| Switch off | temperate day | battlefield follow-low | materials noon | coast high |
|---|---:|---:|---:|---:|
| G2, every switch on (that run) | 664 | 596 | 833 | 479 |
| sky=legacy | 645 (-19) | 554 (-42) | 789 (-44) | 473 (-6) |
| aerial=0 | 667 (+3) | 576 (-19) | 818 (-15) | 464 (-15) |
| farfield=0 | 642 (-22) | 594 (-2) | 816 (-17) | 426 (-53) |
| pbr=0 | 653 (-11) | 595 (-0) | 839 (+6) | 472 (-7) |
| ibl=none | 642 (-23) | 578 (-17) | 839 (+6) | 464 (-15) |
| ibl=hdri | 666 (+2) | 578 (-18) | 827 (-7) | 482 (+3) |
| cascades=2 | 631 (-34) | 507 (-89) | 788 (-45) | 472 (-7) |
| tonemap=acesfit | 671 (+7) | 589 (-7) | 822 (-11) | 485 (+6) |
| tonemap=agx | 683 (+19) | 586 (-9) | 782 (-51) | 475 (-4) |
| autoexposure=0 | 716 (+51) | 569 (-27) | 812 (-21) | 474 (-5) |
| bloom=0 | 693 (+28) | 577 (-19) | 768 (-65) | 473 (-6) |
| grade=0 | 672 (+7) | 578 (-18) | 790 (-43) | 462 (-17) |
| aa=none | 653 (-12) | 547 (-48) | 794 (-39) | 485 (+6) |
| macro=0 | 654 (-11) | 559 (-37) | 809 (-24) | 492 (+13) |
| ripplefade=0 | – | – | – | 478 (-1) |

### 5.2 Per-pass profile (G2 defaults, sync after each pass)

Final build, `--profile` (a GPU sync after every pass), 20 frames:

| Pass (ms per frame) | temperate day | battlefield follow-low | materials noon | coast high |
|---|---:|---:|---:|---:|
| Cascaded shadows (4 cascades, 2048² each) | 136.72 | 182.09 | 134.42 | 68.85 |
| Scene: sky, terrain, far field, props, units, water | 569.65 | 283.39 | 552.38 | 390.07 |
| Bloom down chain (6 levels, also feeds the meter) | 12.40 | 10.73 | 12.25 | 11.23 |
| Metering and adaptation | 0.18 | 0.30 | 0.22 | 0.30 |
| Bloom up chain | 8.01 | 6.61 | 8.15 | 6.86 |
| Composite: exposure, tonemap, 3D LUT | 14.51 | 11.27 | 14.07 | 12.16 |
| FXAA | 11.73 | 11.00 | 13.32 | 6.52 |
| Atmosphere and IBL per frame (sun fixed) | 0.03 | 0.03 | 0.04 | 0.04 |
| Sum of the passes | 753 | 505 | 735 | 496 |
| One-time CPU: sky table (incl. the once-per-haze model build) | 504 | 693 | 411 | 581 |
| One-time CPU: IBL rebuild from the sky | 146 | 239 | 178 | 152 |

### 5.3 Reading the costs

- **The G2 frame costs 3.8–9.4× the G1 frame here** (488–689 ms against 53–127 ms at 1280×720). This is a software
  rasteriser on 4 CPU threads; none of these numbers predicts the Victus, and the relative weights will differ on a GPU.
- **Most of the difference is the photographic terrain shader.** Going back to G1's procedural ground materials (G2
  light kept) saves 152–312 ms; `aniso=1` saves 165–332 ms; `hex=0` saves 83–290 ms. llvmpipe filters anisotropically
  in software (up to 16 trilinear taps per lookup), and hex-tiling samples up to three cells per lookup (taps under
  3 % weight are skipped). On a GPU, anisotropic filtering is done by the texture units, so measure there before trading
  quality away.
- **Levers if a hardware profile shows the terrain too expensive:** `aniso=8` (here −38 to −166 ms), hex-tiling on rock
  only, fewer blended layers per pixel. None is applied: the brief asks for ×8–16 and hex-tiled rock, and llvmpipe is
  not the target.
- **Shadows:** the four 2048² cascades take 69–182 ms per frame in the synced profile; G1's single 2048² map is
  44–128 ms cheaper (p50). The battlefield pays most because of its unit casters. `cascades=2` saved 7–89 ms in the
  first run.
- **Post chain** (bloom down and up, metering, composite with tonemap and 3D LUT, FXAA): 37–48 ms per frame here,
  almost independent of the scene.
- **Atmosphere and IBL:** 0.03–0.04 ms per frame (uniform updates) while the sun is fixed. Their CPU builds run only
  when the sun, the haze or the IBL source changes: 0.41–0.69 s for the sky table (including the once-per-haze model
  build) and 0.15–0.24 s for the IBL prefilter, on 4 threads. A live time-of-day would hitch at every rebuild (§9).
- **Small switches:** sky, aerial perspective, far field, PBR, IBL, tonemapper, auto exposure, bloom, grade, FXAA,
  macro variation and ripple fade moved the frame time by noise-sized amounts in the first run (table B); only four
  entries reach ±50 ms (`farfield=0` on the coast −53, `bloom=0` −65 and `tonemap=agx` −51 on the materials shot,
  `autoexposure=0` +51 on the temperate shot). The synced profile above gives their pass costs.
- **The synced profile adds up to more than the frame** (496–753 ms against the p50): a sync after every pass stops
  llvmpipe from overlapping passes. Read it for relative weights, not totals.
- **A deliverable 16-sample still** at 1280×720 took 3.8–11.9 s per terrain or battlefield shot and 1.0–2.3 s per
  character close-up in the final capture run (the capture log's frame time, which includes each scene's one-time sky
  table and IBL build).

## 6. Tests, builds, checks

### 6.1 Builds

`dotnet build M7Gfx2.slnx -c Debug|Release -m:1 -nodeReuse:false --no-incremental` (forced recompile, `nice -n 19`,
`DOTNET_CLI_USE_MSBUILD_SERVER=0`) on the committed tree of `b6bed59` (the later commit adds this report, the review
JPEGs and two more sheets plus one corrected sheet title in `scripts/review-sheets-render1.py`; none of it is
compiled): **Debug: 0 warnings, 0 errors. Release: 0 warnings, 0 errors.**

### 6.2 Tests

`dotnet test` against each build (GPU tests on llvmpipe; the bake and the HDRI were present under `$G2_ASSETS`, so no
test skipped): **Debug: 121 passed, 0 failed, 0 skipped (1 min 25 s). Release: 121 passed, 0 failed, 0 skipped
(42 s).** The suite had 97 tests before render1; render1 adds 24 and changes no existing test file.

| Class | Tests (each config) | Class | Tests (each config) |
|---|---:|---|---:|
| AvatarTests | 18 | IntegrationTests | 2 |
| CameraTests | 8 | **Render1MathTests** (new) | 17 |
| CinematicTests | 26 | **Render1RenderTests** (new) | 7 |
| EffectLoadTests | 4 | RenderTests | 2 |
| EngineUnitTests | 6 | TerrainTests | 15 |
| FrustumTests | 5 | UnitsTests | 11 |

What the new tests pin:

- `Render1MathTests` (CPU): a clear noon sky is blue at the zenith and brighter at the horizon; sunlight reddens towards
  the horizon and is white at 60°; sky light is a plausible share of sunlight; the sky table's elevation mapping
  round-trips and the table is deterministic; SH9 irradiance of a constant environment is π·L, and of an upper
  hemisphere lights up and not down; a prefiltered constant environment stays constant at every level; the split-sum
  BRDF table is bounded (≤ 1.001, quadrature overshoot about 2·10⁻⁴) and reflects everything for F0 = 1; Hammersley
  points are deterministic; Radiance `.hdr` flat encoding round-trips and run-length scanlines decode; the identity grade
  is the identity and the house look is monotonic; `.cube` files load; still jitter is deterministic, centred and
  distinct; switches parse and the G1 preset is the G1 frame; the far field starts on the map edge and only coastal
  maps get a sea; the baked sets match their manifest.
- `Render1RenderTests` (llvmpipe): the G2 frame draws a blue sky above the terrain; each of 14 switches changes more
  than 200 pixels of a terrain close-up (the far-field and ripple switches are shown by `r1-13` and `r1-11` instead);
  4-sample stills are bit-identical across runs and within 3 % of the 1-sample mean luminance; auto exposure adapts
  over frames after a noon-to-dusk cut, never in one frame and never backwards; HDRI lighting loads, differs from sky
  lighting and stays within ±30 % of its mean luminance; the cascades nest, share one affine light space and reach a
  caster 450 m up-sun; G1-preset and G2 frames (with stills and IBL) are bit-identical when repeated, and neither
  changes the grid or trace hash.

### 6.3 The G1 frame path against pristine G1

All 69 G1 shots captured with `--preset g1` on the final build, compared with pristine G1's own captures
(`prototypes/m7-gfx1` at `1601c74`, same container, same size): **9 of 69 are bit-identical; the other 60 differ by
at most 1/255 in a channel, on 1 to 35 pixels each (mean 6.7) out of 921,600.** The likely cause is that the G1 shading
now shares its shaders with the G2 branches (uniform switches), which can change floating-point evaluation on a few
pixels.

### 6.4 Lesson 3: sunlit ground against the sky

Mean Rec. 709 luma of the display image on the temperate tactical-low shot, sky region rows 0–150 × columns 600–1280,
sunlit meadow rows 450–560 × columns 300–700 (8-bit values):

| | Sky luma (mean RGB) | Sunlit meadow luma (mean RGB) | Ground / sky |
|---|---|---|---:|
| G1 (pristine) | 197.6 (183, 200, 219) | 166.6 (165, 174, 94) | 0.84 |
| G2 (final) | 129.6 (94, 134, 195) | 101.6 (101, 105, 63) | 0.78 |

G2's sky is saturated blue and the sunlit ground stays below it, the relation a photograph of a clear day shows; the
lost attempt had the photographic ground brighter than the sky.

### 6.5 Other checks

- 16-sample stills keep the brightness of single frames: on the 13 shots captured both ways, the still's mean luma is
  0.998–1.003 times that of the single FXAA frame (mean 1.000).
- Rendering is repeatable across processes: a second full run of `scripts/capture-render1.sh` on the final build
  reproduced all 85 images of the earlier run bit for bit, and a second `--preset g1` run reproduced all 69 G1-path
  images bit for bit.
- `scripts/check-gfx-determinism.sh`: OK (4 files in `Gfx.Proc/Authoritative`); render1 changed no file in
  `Gfx.Proc/Authoritative`. No `System.Random`, `DateTime.Now`/`UtcNow`, `AsParallel` or `GetHashCode()` in any file
  render1 changed (`Parallel.For` is used by presentation-only CPU precompute; see §9).
- The bake is reproducible: a second bake into a scratch folder produced the same sha256 for all 10 outputs.
- `prototypes/m7-gfx1` is unchanged against `1601c74` (empty diff).

## 7. Review JPEGs

Sixteen labelled JPEGs in `reports/captures/render1/` (1,368,190 bytes in all, the largest 142,538 bytes), made by
`scripts/review-sheets-render1.py` from the final captures. Every sheet carries the legend
"llvmpipe software GL (not Victus evidence). Props, soldiers, avatars: G1 procedural placeholders. No PROTOTYPE-ONLY
assets." BEFORE panels of G1 scenes come from pristine G1; BEFORE panels of the new render1 scenes come from the G1
frame path (`--preset g1`), since pristine G1 has no such scenes.

| JPEG | Shows |
|---|---|
| `r1-01-temperate-day.jpg` | G1 vs G2, temperate day: analytic sky, aerial perspective, far field to the horizon, photographic terrain |
| `r1-02-temperate-dusk.jpg` | G1 vs G2, temperate dusk (sun 0.9°): sunset sky, reddened sun, long cascaded shadows, lit haze |
| `r1-03-arid-day.jpg` | G1 vs G2, arid coast: far sea and coastline beyond the 1 km map where G1 shows a grey void |
| `r1-04-battlefield-wide.jpg` | G1 vs G2, battlefield wide (230 m): photographic hex-tiled ground, PBR shading, cascades |
| `r1-05-battlefield-dusk.jpg` | G1 vs G2, the battlefield "dusk" shot (17:42, sun 11°, back-lit): G2 reads sky-lit and cool, not golden |
| `r1-06-ground-grazing.jpg` | 1:1 crop of tactical-low ground: G1's smearing and stepped shadow edge vs ×16 anisotropic photographic ground |
| `r1-07-materials.jpg` | PBR spheres (white dielectric, gold, iron; roughness 0 to 1): G1 path, G2 with sky IBL, G2 with the CC0 HDRI |
| `r1-08-cliff-hex.jpg` | Rock close-up, 1:1 crop: G1 path, G2 `hex=0` (grid repeat), G2 hex-tiled |
| `r1-09-anti-aliasing.jpg` | 2× crop: G1, G2 without AA, G2 FXAA (interactive), G2 16-sample jittered still |
| `r1-10-tonemappers.jpg` | Dusk battlefield through ACES fit (G1's), ACES (Hill), AgX and PBR Neutral (G2 default) |
| `r1-11-coast.jpg` | Coast from 75 m: G1 path (sea stops at the map edge), G2 `ripplefade=0` (moiré), G2 |
| `r1-12-ground-closeups.jpg` | Meadow at a grazing angle and the gravel track: G1 path vs G2 |
| `r1-13-atmosphere-ab.jpg` | Temperate day with `farfield=0`, `aerial=0`, `sky=legacy`, and all on |
| `r1-14-dusk-shadows.jpg` | Temperate dusk: G1's single map around the target vs four cascades reaching every caster up-sun |
| `r1-15-post-ab.jpg` | One switch off vs G2: `autoexposure=0` on the dusk shot, `bloom=0` on golden-hour spheres, `grade=0` on the day shot |
| `r1-16-terrain-ab.jpg` | Grazing-meadow crop with `terrain=procedural`, `aniso=1` and G2; tactical-low with `terrain=procedural`, `macro=0` and G2 |

What the sheets show plainly, good and bad:

- In the day and sunset shots, sky, haze, distance and light read as one coherent outdoor scene, and the 1 km map no
  longer ends in a wall (`r1-01`, `r1-02`, `r1-03`, `r1-13`). Ground surfaces are photographic and stay sharp into
  the distance (`r1-06`, `r1-12`, `r1-16`).
- `r1-05`: the battlefield's 17:42 shot (sun 11°, camera facing roughly towards the sun) reads sky-lit and cool, not
  golden. Likely reasons, not investigated further: an 11° sun is only mildly reddened, and the faces turned to the
  camera are lit mostly by the blue sky. The shot is not a sunset; a later stage should pick a lower sun or a
  different camera if the battle needs to read as dusk.
- `r1-02`, `r1-14`: at a 0.9° sun, small terrain bumps cast long streaky shadows across the meadow, and the haze over
  the far field towards the sun glows in a streaky band that can read as smoke or dust.
- `r1-03`: the arid ground still reads patchy. G1's splat layout (sand, gravel and rock patches) shows through the
  photographic sets, and the gravel and rock read cool grey next to the sand.
- `r1-01`, `r1-03`, `r1-11`: the far-field land is a vertex-coloured heightfield; its forest patches read as dark
  blotches near the map edge. On the coast fixture a faint straight band remains in the water along the map edge,
  where the in-map water's fade meets the far sea (clear once the contrast is stretched), and the far sea takes a
  slight mauve tint at the horizon.
- `r1-15`: with `autoexposure=0` the dusk shot is almost black, because the manual exposure is a daylight
  calibration; auto exposure is what makes dusk usable. Bloom and the grade are subtle at review size.
- `r1-16`: ×16 anisotropic filtering keeps the mid-distance grass detailed where `aniso=1` blurs it, and G1's
  procedural materials smear; macro variation is subtle at review size.
- Every sheet: trees, bushes, rocks, soldiers and avatars are G1's procedural low-poly placeholders.

## 8. What still looks placeholder

- All characters (soldiers, commanders, avatars) and all props (trees, bushes, rocks, set dressing): G1's procedural
  low-poly meshes with flat vertex colours. They are lit by the new PBR path but are unchanged otherwise.
- Terrain shape is G1's 4 m grid: river banks are steps and steep faces are facets at close range (the river-bank
  close-up was dropped for that reason); there is no grass geometry, only the ground texture.
- The far field is a procedural, vertex-coloured heightfield with no trees or settlements; it reads as distant land
  only through the haze.
- No clouds, no weather. Night is an artistic dim floor with no stars.
- Water has no refraction or screen-space reflections, no shoreline foam and no wave geometry; shallow water uses
  alpha, so the bed is not tinted by the water column.
- No screen-space ambient occlusion; only the terrain's height-derived micro-occlusion.

## 9. Limitations and risks

- **llvmpipe only.** Every number in this report is llvmpipe (Mesa 25.2.8, 4 CPU threads); no hardware GPU ran this
  code. Costs, pass weights and the visual effect of filtering settings must be measured on the Victus (stage perf's
  harness) before any budget decision.
- **GL extensions.** The terrain's explicit-gradient sampling (hex cells without mip seams) and the IBL mip lookup use
  `GL_ARB_shader_texture_lod`; terrain sharpness needs `EXT_texture_filter_anisotropic`. llvmpipe exposes both. The
  shaders have `#ifdef` fallbacks (plain `texture2D`, so hex cells would show mip seams), but no driver without the
  extension was available here, so the fallbacks have not been exercised.
- **MonoGame 3.8.5 quirks that every G2 stage will meet:** (1) `RenderTargetUsage.DiscardContents` clears a target
  every time it is bound (XNA emulation), so accumulation and additive targets must use `PreserveContents`; (2)
  creating or uploading a texture mid-frame leaves MonoGame's texture-binding cache stale, and a later draw can sample
  the wrong texture (here the far sea sampled the macro texture instead of the sky table). After any upload, set
  `GraphicsDevice.Textures[i] = null` for the slots in use; `RenderContext.Apply` now forces slots 13–15.
- **Conventions for other stages' effects:** lit effects declare `varying vec3 v_wpos` (world position; aerial
  perspective and cascades read it) and keep calling `shadeLit` (G1's signature, PBR inside). Texture slots: 0 shadow
  atlas, 1 bones, 2–12 materials, 13 IBL specular, 14 BRDF table, 15 sky-view table.
- **CPU rebuilds.** The sky table (0.41–0.69 s) and the IBL prefilter (0.15–0.24 s) run on the CPU whenever the sun,
  the haze or the IBL source changes. Fine for stills and fixed-time scenes; a live time-of-day needs a time-sliced or
  GPU rebuild (not done).
- **Presentation-only parallel loops.** The CPU tables use `Parallel.For` over rows; each iteration writes only its own
  row and every reduction is sequential, so the results do not depend on scheduling (the table determinism test and
  the bit-identical repeated frames confirm it here). None of this code is simulation code or feeds back into it.
- **Depth range.** G2 frames use a 40 km far plane with a 24-bit depth buffer, so depth precision at distance is
  coarse. No depth artefact was seen in these captures, but a reversed-Z depth or a separate far-field pass would
  remove the risk (not done).
- **Frame cost.** On llvmpipe the G2 frame is 3.8–9.4× the G1 frame; most of it is terrain filtering (§5.3).
- **Manual exposure** (`autoexposure=0`) is a fixed daylight calibration: dusk and night frames go almost black
  with it (`r1-15`). Keep auto exposure on outside A/B work.
- **G1 preset fidelity.** The G1 frame path matches pristine G1 to within 1/255 on a few pixels per shot, not bit for
  bit (§6.3).
- **HDRI lighting** is a lighting variation, not a matching environment: it is turned to the analytic sun's azimuth,
  its own sun is clamped out and it is scaled to the analytic sky's irradiance; the background stays the analytic sky
  unless `hdribg=1`.
- **The terrain bake is not committed.** Without `$G2_ASSETS/baked/render1/terrain` the renderer falls back to G1's
  procedural ground materials (the load failure is recorded, not thrown); run `scripts/bake-render1.sh` once per
  machine.

## 10. Sim/presentation boundary

Rendering reads published state only. The atmosphere, IBL, cascades, bake and post code are presentation-only and touch
no grid, trace, RNG or timing; the far field reads the authoritative grid's edge heights and writes nothing.
`Gfx.Proc/Authoritative` is unchanged and the determinism scan is clean. Tests: `BattlefieldRendersAndNeverWritesGridOrTrace`
(now running the G2 frame by default) and `G2RenderingIsRepeatableAndNeverWritesTheGridOrTrace` (G1 preset and G2 with
stills and IBL: grid and trace hashes unchanged, repeated frames bit-identical).

## 11. Files

38 files changed in `prototypes/m7-gfx2` over the trunk commit `0395b6c` (4,647 insertions, 76 deletions; 19 new,
19 modified), plus this report and the 16 review JPEGs. Nothing outside `prototypes/m7-gfx2`; `prototypes/m7-gfx1`
untouched.

- **New, Gfx.Engine:** `Atmosphere/AtmosphereModel.cs`; `Lighting/SceneLighting.cs`, `IblMath.cs`, `RadianceHdr.cs`,
  `ColorGrade.cs`; `Rendering/RenderSettings.cs`, `CascadedShadowMap.cs`; `Effects/PostEffects.cs`;
  `Assets/AssetRoot.cs`.
- **Changed, Gfx.Engine:** `Rendering/ForwardRenderer.cs` (G1 and G2 frame paths, stills, profiling),
  `RenderContext.cs`, `Primitives.cs`; `Effects/GlslLib.cs` (PBR, IBL, cascades, aerial perspective),
  `BuiltinEffects.cs`; `Mgfx/EffectBuilder.cs`; `Scene/SunModel.cs`; `Diagnostics/SysInfo.cs`.
- **New, Gfx.Proc:** `Presentation/G2TerrainEffects.cs`, `TerrainMaterials.cs`, `FarField.cs`.
- **Changed, Gfx.Proc:** `Presentation/TerrainScenery.cs`, `TerrainMesh.cs`, `TerrainEffects.cs`, `Vertices.cs`;
  `Scenes/TerrainScenes.cs`.
- **Gfx.Demo:** `Program.cs` (`--preset`, `--set`, `--shots`, `--profile`), `InteractiveGame.cs` (G and 1–9 toggles),
  `Scenes/RenderShowcaseScenes.cs` (new), `Scenes/BattlefieldScenes.cs`.
- **Tests:** `Gfx.Tests/Render1Tests.cs` (new).
- **Scripts:** `bake-render1.sh`, `bake-render1-terrain.py`, `bench-render1.sh`, `capture-render1.sh`,
  `review-sheets-render1.py` (all new).
- **Docs and provenance:** `README.md`, `assets/PROVENANCE.md`, `assets/manifest.tsv`, this report.
