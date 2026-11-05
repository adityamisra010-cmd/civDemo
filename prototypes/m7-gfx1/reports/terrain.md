# Lane T: procedural terrain (G1) report

Date: 2026-11-04. Scope: implementation-spec §1 `Gfx.Proc`. Work stayed inside `prototypes/m7-gfx1/`. There were no git writes. All procedural art is placeholder grade.

## What was built
- **`Gfx.Proc/Authoritative/`** is integer-only and passes the scan. It produces gameplay-relevant state.
  - `TerrainTypes.cs` defines `TerrainClass`. Codes 0–7 equal the battle prototype's codes; `Beach=8` and `Desert=9` are new. It also defines `Biome` and `BattleTerrainInputs`, with an integer biome rule.
  - `IntNoise.cs` provides a SplitMix64 hash, Q16 value noise, fBm and ridged noise.
  - `BattleTerrainGrid.cs` holds a 256×256 grid of 4 m cells. Per cell it stores height in mm, water depth in mm and class. It has a canonical little-endian layout (`BTG1`), `FromCanonicalBytes`, and a SHA-256 `HashHex()`.
  - `BattleTerrainGenerator.cs` runs this pipeline:
    1. Relief from fBm plus ridged noise. Amplitude comes from the movement-cost input.
    2. A coastal shelf.
    3. Integer thermal erosion. It uses Jacobi updates and conserves mass exactly.
    4. A river along an integer Dijkstra least-cost path. Priorities are a composite (cost, cell), so the order is deterministic. The water surface never rises downstream and drops at most 100 mm per centre-line cell. A lower-only slope limit then holds every 4-neighbour step to the talus (2.4 m per 4 m cell), so a channel cut through higher ground becomes a talus-sided valley (tested; see Verification).
    5. Sea flooding.
    6. A two-cell road laid by a greedy least-climb walk. It fords rivers, and its leading lane steps away from ground within 1 m of sea level (tested on a synthetic coast; the fixtures never trigger it).
    7. Class assignment: water, road, beach, then slope classes, then forest or jungle gated by moisture and noise.
- **`Gfx.Proc/Presentation/`** uses floats and only reads the grid.
  - `TerrainMesh.cs`:
    - 64×64-cell chunks. Each chunk has one vertex buffer and three LOD index lists (steps 1, 2 and 4), with skirts.
    - Normals, per-sample splat weights, `HeightAt` and `ClassAt`.
    - A water mesh dilated by one cell, so the terrain depth test cuts the shoreline.
  - `SplatModel.cs` computes 8 splat weights: grass, dirt, rock, sand, snow, forest floor, mud, dry grass. They are continuous functions of 3×3-blurred class fractions, slope, relative height and noise, with no hard thresholds (the same idea as `Sim.Ui/Art/TerrainSplat.cs`). They are normalised to sum to 1. It also holds four biome palettes.
  - `MaterialTextures.cs` packs the 8 materials into two tileable 512² RGBA textures, one material per channel. Mip chains are built on the CPU. Two samplers keep the shader inside GL 2.1 limits on Iris Xe.
  - `TerrainEffects.cs` is an `IEffectSource` with three effects:
    - `TerrainSplat` blends by height using material detail, samples at two scales with rotation to hide tiling, uses biplanar mapping for rock on steep faces, applies a macro tint, and uses the standard lighting, fog and shadows.
    - `TerrainDepth`.
    - `Water` has a depth tint, Fresnel sky reflection, sun glint and animated ripple normals.
  - `PropMeshes.cs` builds trees (broadleaf, conifer, palm, acacia), bushes and rocks. Each has a variant seed and two LODs.
  - `Scatter.cs`:
    - Bridson Poisson-disk sampling, thinned by a per-class density.
    - 10 prop groups (3 tree, 3 bush, 4 rock). Each instance gets its own scale, rotation and tint; rocks and bushes are aligned to the ground.
    - `WeatherParams`, which is presentation only.
  - `TerrainScenery.cs` handles the GPU side:
    - Chunks are frustum-culled and LOD-selected by distance (300 / 700 m).
    - The shadow pass draws casters at the LOD the camera will see, because coarser casters caused self-shadow streaks.
    - Props are frustum-culled, switch LOD by distance and are culled by distance.
    - Water is alpha-blended and drawn last.
- **`Gfx.Proc/Scenes/TerrainScenes.cs`** registers two scenes. Their inputs are labelled fixtures.
  - `terrain-temperate`: hills, river, road and forests. The river runs in a deep talus valley; in the day shot only a short stretch is visible at the right edge.
  - `terrain-arid`: south coast, beach, a shallow river (it carries water; not a dry wadi) and desert scrub.
- **Shared files:** I added one line each to `M7Gfx.slnx`, `Gfx.Demo.csproj` and `Gfx.Tests.csproj`. New test file: `Gfx.Tests/TerrainTests.cs`.

**API for lane I:**
- `BattleTerrainGenerator.Generate(inputs)` returns the grid.
- `new TerrainScenery(gd, grid)` gives `.DrawShadowCasters(rc)`, `.DrawOpaque(rc)`, `.HeightAt(x, z)`, `.Weather.FogParams` and `.Mesh.ClassAt`.
- World layout: the map is centred on the origin, +X is east, grid y = 0 is north (−Z), and 1 unit = 1 m.

## Evidence
- `dotnet build M7Gfx.slnx -c Release`: 0 warnings, 0 errors.
- Full `dotnet test Gfx.Tests`: 48 passed, 0 failed, 0 skipped. That includes the other lanes' tests at the time of the run.
- `TerrainTests`: 12 passed.
  - `GridHashesArePinned`:
    - temperate `19da38cd…f6ea6`
    - arid `42216ba2…e4f4`
  - `RepeatedGenerationIsByteIdentical`
  - `DifferentSeedOrCellGivesDifferentTerrain`
  - `CanonicalBytesRoundTripPopulatedGrid`: exact length 589,865 bytes, bit-exact round trip, hash equality, and a one-byte tamper changes the hash.
  - `IntegerOnlyScanPassesOnAuthoritativeFolderAndCatchesFloat`: the scan exits 0 on the real folder and 1 on a planted float file.
  - `ThermalErosionConservesMassExactlyAndReducesSteepness`
  - `RiverRunsDownhillToItsOutletAndIsWater`
  - `BiomesProduceTheirClasses`
  - `SplatWeightsAreNonNegativeAndSumToOne`: every sample of both maps, plus 4,000 random inputs across all biomes.
  - `ChunkLodIndicesAreValid`: index range, no degenerate triangles, exact triangle counts, and each LOD uses exactly its own lattice.
  - `TerrainScenesRenderAndNeverWriteTheGrid`: the renderer-invariance test. Every shot is rendered with shadows on, and the hash before and after the render equals a fresh generation. Mutant check: when `DrawOpaque` was made to write the grid, this test failed; the mutant was then removed.
  - `TerrainEffectsAreRegistered`
- The existing `EffectLoadTests` compiles and links all 9 effects on llvmpipe, including the three new ones.
- `scripts/check-gfx-determinism.sh` reports OK (4 files).
- Captures are in `scratchpad/m7gfx/captures/terrain/`:
  - `terrain-temperate-{day,dusk,tactical-low}.png`
  - `terrain-arid-{day,dusk,tactical-low}.png`
  - These cover 2 biomes × day/dusk, plus a low tactical angle for each.

## Verification (fix round, 2026-11-04)
Each finding was reproduced in this folder before fixing. Grid hashes re-pinned with the cause noted in `TerrainTests.cs`: temperate `be39da87…6fe89`, arid `88b1094a…c9156`.

| # | Finding | Reproduced | Fix | Semantic test | Mutant (killed by) |
|---|---|---|---|---|---|
| 1 major | River leaves one-cell cliffs; talus claim false | Yes: temperate 42,625 mm step at (248,191), same cell as reported | `LimitSlope` after carving: exact two-sweep lower-only L1 transform, so no 4-neighbour step exceeds the talus (2,400 mm). Max step now 2,400 mm on both maps | `NoAdjacentCellStepExceedsTheTalus` | call removed → this test |
| 2 minor | Dry cells below an adjacent water surface | Yes: arid 173 cells with 8-neighbour check (verifier's 84 was a narrower adjacency); temperate 0 | River surface drop capped at 100 mm per path step (backward pass lowers upstream surfaces, cutting deeper). Now 0 on both maps. Rejected alternatives, measured: flood spill drowned 9.8k–16k arid cells (perched river); lowering surfaces drained 198 of 233 river cells; raising banks broke the slope limit (29 pairs) | `NoDryCellLiesBelowAnAdjacentWaterSurface`; river test now also bounds the per-step drop | cap removed → this test and the river test |
| 3 minor | Sea-avoidance claim untested | Yes (by reading: the fixture road band is inland) | `LayRoad` made `internal` for testing; behaviour unchanged | `RoadStepsAwayFromTheSeaAndStaysContinuous`: synthetic coast that falls into the sea; the road must stay continuous and dry | penalty → 0 → this test |
| 4 minor | Scan misses `1e3`, `.5`, `2.`, `Math.FusedMultiplyAdd` | Yes, all four exit 0 | Rules added: exponent, leading-dot and trailing-dot literals; all of `MathF.`; FMA, ScaleB and other float `Math` members; `BitConverter` float reinterpretation | Scan test plants all four plus `MathF.Abs`, and checks that integer look-alikes (`0..2`, `a[1].X`, `0x1E5`, `2.CompareTo`) stay clean | rules removed → this test |
| 5 minor | Labels overstate the river | Yes | Arid scene text now says a shallow (wet) river; the report states where the temperate river is visible | n/a (wording) | n/a |
| 6 minor | Scan test wrote to system temp | Yes | Planted files now go to `<prototype>/.gfx-scan-<pid>` (outside bin/obj, which the scan skips), deleted in `finally` | n/a | n/a |

Re-captures after the fix: `scratchpad/m7gfx/captures/terrain/v2/`. The grey vertical walls are gone. The earlier set in `captures/terrain/` shows the old grid. Not mine to fix: lane E's `EngineUnitTests.DeterminismScanFlagsFloatAndPassesIntegerCode` also writes to the system temp directory and does not clean up when it fails. A failing intermediate run of mine left three such folders; I removed them.

After the fix: `dotnet build M7Gfx.slnx -c Release` gives 0 warnings and 0 errors. `TerrainTests` passes 15/15. The full `Gfx.Tests` suite passes 93/93. The scan reports OK on 4 files.

## Measurements: SOFTWARE RENDERING (llvmpipe, 4 CPUs), not Victus performance
Each run is 1280×720, 30 frames, and the frame time includes waiting for the renderer.

| Scene | Shadows | p50 | p95 | p99 | Draws | Triangles | Prop instances drawn |
|---|---|---|---|---|---|---|---|
| terrain-temperate | on | 219.7 ms | 321.6 ms | 427.9 ms | 53 | 411k | 10,210 |
| terrain-temperate | off | 129.7 ms | 151.9 ms | 183.2 ms | 27 | 303k | 8,243 |
| terrain-arid | on | 184.5 ms | 253.8 ms | 296.5 ms | 55 | 163k | 1,528 |
| terrain-arid | off | 123.8 ms | 180.8 ms | 253.8 ms | 29 | 91k | 747 |

Grid generation, CPU only: about 90–180 ms per 256² map (Release, this container).

## Placeholder or cut
- All meshes, textures and palettes are procedural placeholders. The route to Total War quality is authored base meshes and materials, with this layer supplying variation and placement.
- Not built:
  - Grass and ground-clutter cards.
  - Normal or roughness maps.
  - Far-distance tree impostors. Trees are culled at 900 m.
  - Screen-space or planar water reflection.
- Precipitation is chosen in `WeatherParams` but not rendered: particles were cut to fit the budget. Fog is rendered.
- The optional campaign-seeding demo was not built. The integer inputs are labelled fixtures.
  - The proposed boundary conversion, not implemented: `MoveCostPermille = round(MovementCost·1000)`, `TemperatureDeciC = round(T·10)`, `MoisturePermille = round(m·1000)`, `CoastEdge` from the side with water neighbours, `River` from the Rivers layer.
  - `ElevationM` needs a declared metre scale. The recon found none, which makes this a blocker.

## Director decisions and blockers
1. **The classes are gameplay-relevant, but the campaign has no such fields (recon-sim-seams §1.2).** This generator invents Forest, Jungle, Hills, Mountain, Beach and Desert from integer thresholds on moisture, slope and noise. These need ruling as the authoritative downscale rule, or replacing.
2. **Biome thresholds** (`BattleTerrainInputs.Biome`) are prototype values: Cold ≤ 2.0 °C, Arid < 300‰ moisture, Tropical ≥ 22.0 °C with ≥ 600‰ moisture.
3. **The new classes need cost-table entries in the battle prototype.** `Beach` and `Desert` have none. Water depth is now per cell, so fords are possible; the semantics of fording need a rule.
4. **Campaign elevation has no metre scale.** This blocks real campaign seeding.

## Recommended next step
Lane I should integrate `TerrainScenery` under the units, using `HeightAt` to snap formations. After that, the next terrain slice is:
- Ground-clutter cards and tree impostors.
- One authored material set, to replace the procedural detail channels and test the variation layer.
- The campaign-seeding boundary, once the metre scale and the class rule are ruled.
