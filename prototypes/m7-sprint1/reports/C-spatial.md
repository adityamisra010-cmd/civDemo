# Slice C — spatial foundation (M7 Sprint 1)

Spec: `m7s1/implementation-spec.md` §3 (with §1 and §8), in the scratchpad. Repo `/home/user/m5w/dev`, branch
`claude/new-session-yavghm`. Everything is under `prototypes/m7-sprint1/`. No git state was changed, and nothing outside
the folder was touched. `Sim.slnx` was never built. The Slice B code (`FixedMath.ISqrt`, `DivFloor`, `MulDivFloor`,
`Q32`, `CounterRng`) is reused and was not modified.

## 1. What exists

| file (`Proto.M7/Spatial/`) | contents |
|---|---|
| `Vec2L.cs` | `Vec2L(long X, long Y)` in **mm**. Provides an Int128 dot/cross/squared length, and `Length`/`Distance` = exact floor `ISqrt(UInt128)`. Also defines `CellCoord`, `SpatialMath.ISqrtCeil`, and the documented range. |
| `Terrain.cs` | `TerrainClass` (Road, Open, Forest, Jungle, Hills, Mountain, Water, Wall; stable byte codes). `TerrainCostTable` is an input table with a validator. `ConcealmentTable` is a Q32 input table with a validator. Both have a labelled `PlaceholderTestTable()`. |
| `TerrainGrid.cs` | One grid serves both the planner and the continuous queries. It holds the class byte and `HeightMm` (long) per cell, with `CellSizeMm` defaulting to 4,000 (it must be even). It provides `CellOf` (floor division, safe for negatives), `CellCenter`, `IsPassable`, and extent and height bounds. |
| `Supercover.cs` | Exact integer supercover walk. For each cell it emits the parameter interval `[TIn, TOut]` over a common denominator. Available as a streaming visitor (generic struct, no allocation) or as `Cells(...)`, plus `SegmentPassable`. |
| `GridPathfinder.cs` | Integer A*: 8-connected, octile 1414/1000, no corner cutting. The open list is an explicit binary heap keyed `(f, h, cellIndex)`. Work arrays are reused and reset by a stamp. Has a neighbour-order test hook. |
| `PathMovement.cs` | Cell-centre waypoints, terrain-aware `SegmentTimeMs`/`PolylineTimeMs` (one Int128 numerator, rounded up once), and `PositionAt`. |
| `LineOfSight.cs` | `Query(...) -> LosResult(Visible, Q32 Clarity, Block, BlockCell)`. Blocked by Wall, height, or leaving the grid; vegetation multiplies Clarity as a Q32 product. |
| `Projectiles.cs` | Exact segment-vs-disc test in Int128; `FirstHit` ordered by `(t, id)`; `DirectFire` on the grid; `BallisticShot`/`Fly`/`ArcAt` for the closed-form integer arc. |
| `Separation.cs` | `Step(movers, grid?, costs?)` resolves one movement step in ascending id, truncating each move so no discs overlap, with an exact segment-enters-open-disc test. |

Tests are in `Proto.M7.Tests/Spatial/`: 5 test files plus `SpatialTestSupport.cs`, with **67 tests**. The bench is
`Proto.M7.Bench/SpatialBench.cs`. I added one line to `Program.cs` to dispatch the `spatial` mode, and one line to the
README.

## 2. Definitions and design decisions (spec §3 delegated choices; O-6/O-7/O-7a)

1. **Units and range.**
   - Every position is in millimetres (`long`) and must satisfy |X|, |Y| ≤ 10^9 mm. A query throws when it gets anything outside that range.
   - Proven bounds that follow from the range:
     - a displacement component is at most 2·10^9;
     - a squared length is at most 8·10^18;
     - any product of two squared lengths is at most 6.4·10^37, which fits in Int128.
   - The grid's extent is limited to half that range (5·10^8 mm), so a bounded ballistic step that lands off the grid still stays inside the proven range.
   - Entity radius is at most 10^6 mm. Velocity components are at most 10^7 mm/s.
   - `Proto.M7` compiles with overflow checking on.
     - A violated assumption therefore throws rather than silently wrapping.
     - This caught one real bug during development: a `(uint)` bounds check on a negative int throws under checked arithmetic.
2. **Cells.**
   - Cell (cx, cy) covers the half-open square `[cx·S, (cx+1)·S)²`.
   - Cells off the grid are impassable, block LOS, and stop projectiles.
   - S must be even, so a cell centre is an exact integer and never lies on a grid line.
3. **Supercover definition.** The cell set does not depend on direction; reversal invariance is tested.
   - **Membership:** every cell that contains a point of the closed segment.
   - **Corner rule:** if a segment that is not axis-parallel passes through a lattice corner (including an endpoint exactly on a corner), **all four cells at that corner** are included. This is the spec's "a corner crossing visits both side cells".
   - A segment running exactly along a grid line covers only the +side row or column. That is the row or column the moving point actually occupies.
   - Crossing parameters are exact rationals over D = max(|dx|,1)·max(|dy|,1).
   - **Crossing rule:** moving +x, the point enters cell cx+1 at x = (cx+1)S, allowed up to t = 1. Moving −x, it enters cx−1 only strictly after x = cx·S, so t < 1 is required. The same applies on y.
   - Every cell is emitted exactly once, in non-decreasing TIn order.
4. **A\*.**
   - **Step costs:**
     - orthogonal: (c_a + c_b)·S/2, exact;
     - diagonal: ⌊(c_a + c_b)·S·1414/2000⌋, using the documented constant DiagNum/DiagDen = 1414/1000.
   - **Units:** cost ×1000 per mm. On ground with cost c at speed v mm/s, the time in ms is cost·mm/v.
   - **No corner cutting:** a diagonal step needs both orthogonal neighbours to be passable.
   - **Heuristic:** octile distance × m, where m is the **minimum passable cost present in this grid**. It is recomputed by `RefreshCosts`.
     - h is admissible because every step costs at least the h unit.
     - h is consistent (a metric) because m·S ≤ diagUnit ≤ 2·m·S, so a closed cell is never reopened.
     - I chose the grid minimum over the table minimum after measuring: on a 250² open field the table minimum (Road 700) expanded 37,180 nodes, against 250 with the grid minimum.
   - **Heap key:** `(f, h, index)`.
     - Equal keys can occur only as duplicate entries of the same cell (lazy deletion).
     - Extraction order is therefore a pure function of the key set.
   - **Parent rule:**
     - strictly lower g, or equal g from a parent with a lower cell index (a canonical tie-break);
     - the returned path does not depend on the order in which neighbours are enumerated (tested with 12 permutations).
5. **Consistency between the grid and continuous space.** Entities follow the polyline through cell centres.
   - An orthogonal step between centres touches only its two end cells.
   - A diagonal step passes exactly through the shared corner. The corner rule adds both side cells, and no-corner-cutting guarantees those are passable.
   - So continuous motion never enters a cell that the planner treats as impassable. This is tested exhaustively, and a negative control shows that a corner cut **would** be caught.
6. **Movement time.**
   - `time_ms = ⌈Σ_cells (TOut−TIn)/D · L · cost / v⌉`, where L = ⌊|b−a|⌋.
   - It is computed as one Int128 numerator and rounded up once, so a move is never early.
   - With the default 4,000 mm cell, L_diag = ⌊4000√2⌋ = 5656 = 4000·1414/1000. So the movement time of each segment **equals ⌈A\* step cost / v⌉ exactly**; this is tested.
   - For other cell sizes the two differ by less than 1 mm-equivalent per diagonal step. That difference is documented and not tested.
7. **LOS.**
   - The sight line runs from z0 = ground(observer) + eye to z1 = ground(target) + target height.
   - z at n/D = z0 + ⌊(z1−z0)·n/D⌋, using integer interpolation.
   - A cell blocks if its flat top is above the line somewhere inside the cell, i.e. `H > min(z(TIn), z(TOut))`. Equal heights do not block.
   - The observer's and target's own cells are excluded from the height test, because they stand on those cells.
   - Concealment is the product over all membership cells except the observer's own. The target's cell always counts. Corner-side cells have zero length and do not count.
   - Wall blocks LOS at any height. Water does not block LOS.
8. **Segment vs disc.**
   - The disc is closed. **A tangent is a hit**, and a segment that ends exactly on the boundary hits at t = 1.
   - Whether there is an intersection is decided exactly from the closest point (Int128).
   - The entry parameter is t = (P − ⌈√(P² − A(|w|² − r²))⌉)/A.
     - This is the exact root rounded down to a multiple of 1/A.
     - It is never later than the true entry; the test checks the bracket exactly.
   - A zero-length segment hits iff |w| ≤ r, with t = 0.
   - **First hit:** the minimum of (t numerator, id). All discs in one query share the denominator A. An equal t goes to the **lower id**.
9. **Direct fire.**
   - The result is the first hit, unless a blocking cell (Wall, off-grid, or height) is entered **strictly before** the hit parameter.
   - A hit point exactly on a wall face counts as a hit.
   - The height test excludes the shooter's cell and the aim cell.
10. **Ballistic arc.**
    - The arc is sampled every `stepMs` in closed form, so no rounding accumulates:
      - x(t) = x0 + ⌊vx·t/1000⌋;
      - z(t) = z0 + ⌊(2000·vz·t − g·t²)/2·10^6⌋, with g = 9,807 mm/s².
    - The impact step is the first step whose straight 3-D chord (p(k−1), z(k−1)) → (p(k), z(k)), walked over the supercover of the step segment, crosses a Wall or off-grid cell (stop at its entry), or meets a cell whose top is at or above the chord: entry height ≤ top (stop at the entry, a face hit) or exit height ≤ top (stop at the exit). For the arrival cell the exit is p(k), so this contains the old sampled rule "z ≤ ground at p(k)". *(Changed in the verification round, C-V-4: before, only p(k) was height-tested and a shell could pass through a raised cell between samples.)*
    - On that step, the step segment is tested against the discs. A disc hit counts iff it is at or before the stop parameter, so a disc touching a wall face exactly is hit, as in direct fire. *(Changed in C-V-5: it was strictly before.)*
    - **Walls are treated as infinitely tall**: a lob cannot clear one. This is a prototype simplification and is tested and documented.
    - Termination: Fly rejects (ArgumentOutOfRange) any shot with |z0| > 2·10^9 mm or with z(MaxFlightMs) > −10^9 mm, MaxFlightMs = 3,600,000 (both PLACEHOLDER engineering bounds, C-V-6/C-V-9). Every accepted shot is down within MaxFlightMs, so at most 3.6·10^6 steps (≈0.2 s measured at the worst accepted input).
11. **Separation.**
    - Movers are processed in ascending id after sorting by id; duplicate ids are rejected.
    - Each move is truncated so that its disc never enters the **open** interior of another disc. Touching is allowed; overlap means d² < (r_i + r_j)².
    - **Deviation from the spec's wording, made under O-6:**
      - The obstacles are *all* current discs: committed lower ids at their new positions, and higher ids not yet moved at their old positions.
      - The spec says "a disc already committed this step". Taken literally, a lower id can step onto a higher id that has not moved yet, and the result overlaps.
      - Mutant M5 shows exactly this failure: the crowd and stationary-obstacle tests fail.
    - **Truncation:** a binary search returns a k such that the actual integer segment Pos→q(k) is clear (and passable, if a grid is given) and Pos→q(k+1) is not. This is a boundary of the valid set, **not necessarily the largest valid k** (corrected in C-V-7; the predicate is non-monotone, 17 of 3,641 small random cases differ).
    - The search checks the segment that is actually realised. So the invariant still holds where rounding makes the predicate non-monotone.
    - Cost is O(n² log K) per step. A broad phase would be an optimisation that does not change the semantics.

## 3. Placeholders (all labelled in code)

- **Cost table (`TerrainCostTable.PlaceholderTestTable`):**
  - **Road 700** — cited. It is `roads.classes` road_built speedFactor 0.7 ×1000 (`sim.json`; recon-military §1.4).
  - **Open 1000** — the baseline, by definition.
  - **Forest 1500, Hills 1800, Jungle 2500, Mountain 4000** — PLACEHOLDER (the spec §3 list).
  - Water and Wall are impassable.
- The validator enforces only the Director's binding order, **Road < Open < Jungle < Mountain** (strict). It also requires Water and Wall to be impassable and every cost to be in [1, 10^6]. Where Forest and Hills fall is deliberately **not** constrained, because no ordering for them is stated.
- **Concealment (`ConcealmentTable.PlaceholderTestTable`):**
  - Forest ½ and Jungle ¼ — PLACEHOLDER.
  - Road and Open are 1, which the validator requires.
  - Hills, Mountain and Water are 1 — PLACEHOLDER.
  - Wall is 0 and never consulted, because Wall blocks first.
- The default cell of 4,000 mm comes from the spec. Eye and target heights (1,700 mm), speeds, and radii in tests are test inputs only.
- No terrain class exists in the campaign (recon-critic A4). These classes are prototype-only.

## 4. Tests and results

- `dotnet build prototypes/m7-sprint1/M7Proto.slnx -c Release -nologo --disable-build-servers`: **0 warnings, 0 errors**.
- `dotnet test … --no-build`: **Passed 190, Failed 0, Skipped 0** (whole M7Proto, about 0.9 s) at delivery; **203** after the verification round (+13 in `VerificationFixCTests`, §9). The spatial part was **67** at delivery:
  - Supercover 16
  - Pathfinder 21
  - ConsistencyAndMovement 5
  - LosAndFire 16
  - BallisticAndSeparation 9
- `scripts/check-proto-determinism.sh`: **CLEAN** (28 files, comments included). The first run flagged the word "Half-open" in a comment, so I reworded it.

What each spec bullet is tested by:

- **Vec2L / CellOf.** Floor division on negatives (−1, −4000, −4001, ±10^9). Exact floor distance, including corner-to-corner of the range: 2,828,427,124, checked by its defining inequality. Out-of-range input throws.
- **Supercover.**
  - Brute-force oracle of the definition: membership from exact dense rational samples, plus the corner rule. Run on 3,500 random segments, including exact diagonals, boundary endpoints and negative coordinates.
  - Each cell exactly once; TIn non-decreasing; membership intervals partition [0, D] exactly; the walk ends in CellOf(b).
  - The cell set is invariant under reversal (2,000 segments).
  - Explicit corner, mixed-direction corner, near-miss by 1 mm, corner endpoints, half-open endpoints, edge-running, zero length, and a range-wide segment.
- **Validator.** 11 violating tables are rejected with reasons. Forest and Hills are unconstrained. The concealment validator is tested.
- **A\*.**
  - Optimal against an independent O(N²) Dijkstra on 160 random grids with walls and water (>60 reachable cases).
  - Every path is re-checked for adjacency, passability and no corner cutting, and its recomputed cost must equal the reported cost.
  - The heuristic is admissible at the start.
  - **Tie-dense:** on an open 40×40 field and on a uniform-cost field with scattered walls, the same path comes back under 12 neighbour-order permutations and with reused work arrays.
  - **Hand-derived tie pins:** one where the h tie-break decides. *(The second pin claimed the equal-g lower-parent-index rule decides; mutant V1 showed it does not, because the lower-index parent offers first there. The verification round added a pin where the higher-index parent offers first — see §9.)*
  - Corner cutting, roads preferred and mountains avoided, unreachable goal, impassable goal, and start equal to goal.
- **Consistency proof test.** On 60 random grids with 22% walls, every waypoint segment of every found path touches only passable cells (>1,000 segments, >200 of them diagonal). A negative control shows that a corner-cut diagonal touches the wall.
- **Movement time.**
  - Per segment, it equals ⌈A\* step cost / v⌉ on random paths.
  - Worked examples: 4000, 5656, 3400 (mixed road and open), 5100 (an unequal split).
  - Time follows road < open < jungle < mountain. Rounding is up (1 mm at 3 mm/s gives 334 ms). Crossing a wall throws.
- **LOS.**
  - A clear line; Wall blocking, with the block cell reported; a corner-touching wall on a diagonal; water does not block.
  - The exact height threshold: 1000 does not block, 1001 does.
  - An interpolated ridge: 11,700 is the minimum of the line over the cell, and 11,701 blocks.
  - Endpoint cells are excluded.
  - The Q32 product is exact: ½·½·¼ = 1/16. The observer's cell is excluded and the target's cell is included (also tested reversed and within a single cell).
  - Wall blocking is symmetric on 600 random lines. Off-grid endpoints are not visible.
- **Projectiles.**
  - A tangent is a hit (t = ½ exactly), and 1 mm further is a miss.
  - Exactly at the endpoint is a hit with t = 1, and 1 mm beyond is a miss. Starting inside gives t = 0; a disc behind the start is a miss or a hit depending on whether it touches.
  - Zero-length segments. The exact root for a perfect-square discriminant. The rounding bracket for an irrational discriminant: t is outside or on, and t + 1/A is strictly inside.
  - Mirrored negative coordinates give equal results.
  - Huge coordinates (A = 8·10^18, r = 10^6): numerator 3,997,171,572,875,253,809, computed independently with Python `math.isqrt`.
  - A tie at identical t goes to the lower id regardless of input order (6 permutations). The shooter is ignored.
  - Direct fire: hit before the wall; a hit exactly on the wall face; blocked 1 mm later; a height block; leaving the grid.
- **Ballistic.**
  - Hand-derived golden: z(550) = 16, z(560) = −38, impact at (58,000, 6,000) at 560 ms after 56 steps.
  - An entity is hit only on the impact step (x = 57,200 at 552 ms); an entity under the mid-flight arc is not hit.
  - A tie at the impact step goes to the lower id in both input orders.
  - Wall at 380 ms; hill at 460 ms with x = 48,000; leaving the grid at 380 ms.
  - The closed-form samples are deterministic.
- **Separation.**
  - Head-on pair: id 1 reaches 1000 and id 2 stops touching at 1200. Swapping the ids swaps the result.
  - No tunnelling through a small disc (stops at 4,940); a stationary higher id is an obstacle.
  - With a grid, a mover stops at the last mm before a wall cell.
  - Duplicate ids and initial overlap are rejected; exact touching is accepted.
  - **Crowd:** 100 discs over 40 converging steps. After every step, **no pair overlaps**. Under 3 different per-step input permutations the results are identical step by step. The test is not vacuous: total distance falls by more than 20%, with more than 500 truncations.

## 5. Mutation testing

- **Where:** a private copy, `m7s1/work/C/mut`. The driver is `m7s1/work/C/mutants.py` and the log is `m7s1/work/C/mutants.log`.
- **Bound:** the clean build and test takes 17 s, so each mutant was bounded at 85 s (5×).
- **Result:** 12 of 12 mutants were killed, each by at least one semantic (non-golden) test. None hung.

| # | mutant | killing semantic test(s) |
|---|---|---|
| M1 | corner cutting allowed | Every_path_segment_touches_only_passable_cells, Astar_is_optimal…Dijkstra, No_corner_cutting…, Movement_time_matches_planner… |
| M2 | supercover interior corner rule dropped | Matches_bruteforce_oracle…, Exact_corner_crossing…, Negative_control…, Wall_blocks_and_corner_touching_walls… |
| M3 | heap key ignores h | Tie_break_prefers_lower_h_then_lower_index_pinned (hand-derived) |
| M4 | tangent treated as a miss | Tangent_is_a_hit_and_one_mm_further_is_a_miss |
| M5 | only committed lower-id discs are obstacles (literal spec wording) | Crowded_scenario_never_overlaps…, No_tunnelling…stationary_discs_are_obstacles |
| M6 | LOS height uses max instead of min | Height_uses_interpolated_line… |
| M7 | −x crossing allowed at t = 1 (breaks half-open) | Matches_bruteforce_oracle…, Cell_set_is_reversal_invariant, Half_open_boundary_endpoints, Endpoint_exactly_on_a_corner… |
| M8 | first-hit tie goes to the higher id | First_hit_ordering…, Entity_is_hit_only_on_the_impact_step |
| M9 | movement time floors instead of ceil | Movement_time_examples…, Movement_time_matches_planner… |
| M10 | diagonal step charged as orthogonal | Astar_is_optimal…Dijkstra, Straight_open_corridor…, Tie_break… |
| M11 | LOS concealment counts the observer's cell | Vegetation_accumulates_concealment… |
| M12 | wall entered exactly at the hit parameter blocks | Direct_fire_hits_or_is_blocked_by_wall_before_the_hit |

**Teeth versus aim (ADR-015 §7.2).** Each killed property is one the system should have:

- grid and continuous consistency (M1, M2);
- a definition that does not depend on direction (M7);
- total-order tie-breaks (M3, M8);
- the documented boundary conventions (M4, M12);
- the separation invariant (M5);
- conservative timing (M9).

## 6. Performance (`dotnet Proto.M7.Bench.dll spatial 9`, Release, Linux x64, 4 CPUs; median of 9 after a warm-up; two runs)

| case (250×250, 4 m cells) | median ms | expanded | notes |
|---|---|---|---|
| A* open field, corner to corner | 0.25 | 250 | the grid-minimum heuristic is exact on uniform ground |
| A* open field, (0,0)→(249,120) | 0.18 | 250 | |
| A* mixed terrain, 15% walls + 3% water, corner | 15.8 – 15.9 | 49,647 | the grid contains Road (700), so h is weak against 1000–4000 terrain |
| A* serpentine maze, corner (15,812-cell path) | 4.5 – 6.7 | 46,812 | |
| A* unreachable goal (full expansion) | 16.5 – 21.0 | 62,379 | the worst case is about 3–4 M nodes/s |
| A* mixed, 20 random pairs | 2.35 – 2.41 per query | mean 10,144 | 14/20 reachable |
| LOS, 100k queries with offsets ≤ 60 m | 34 – 39 (≈2.6–2.95 M/s) | – | |
| Separation, 120 movers, with grid check | 2.7 – 2.9 | – | O(n² log K) |

An earlier run with a table-minimum heuristic (m = 700) took 6–28 ms on the open field with 32–37k expansions. That is the
measurement behind decision 4.

## 7. Dependencies and blockers for integration

1. **O-14:** where the spatial layer lives is undecided. This code is a prototype; it is not the battle framework.
2. **No terrain-class layer in the campaign** (recon-critic A4, recon-military §1.4). The campaign has elevation, water, temperature, moisture, fertility, movementCost and rivers, but no forest, jungle, hills or mountain classes.
   - Building a tactical grid from the campaign map needs a classification rule.
   - Every threshold such a rule would need would be invented, so this needs a Director or content decision.
   - Battle-map size and generation are also open: D-011's "~48×32" is only a recommendation (recon-critic G5).
3. **Cost and concealment values** beyond Road 700 are placeholders. Only the ordering is binding.
4. **The campaign pathfinder uses doubles** (`Pathfinder.cs`, `TraversalLattice.cs:57` √2 literal, `Math.Sqrt` heuristic).
   - This layer is a separate integer grid, so M6 behaviour is untouched.
   - The two pathfinders will not agree bit-for-bit, and they do not need to: they work at different scales.
   - Unifying them would change M6 goldens.
5. **Simplifications I made that the Director may want revisited:**
   - walls are infinitely tall;
   - the entity footprint is a 2-D disc, and ballistic hits ignore entity height;
   - grid passability for separation checks only the centre point;
   - a segment running along a grid line covers only the +side.

## 8. Open questions (not answered here)

1. Real per-class movement costs and concealment multipliers, and whether Forest and Hills are ordered relative to the other classes.
2. Battle-map dimensions, cell size, and how tactical terrain is derived from the campaign map (see dependency 2).
3. Whether walls and structures need finite heights, which would let indirect fire clear them; this is siege scope, which is not decided.

## 9. Verification and fixes (verify-C-spatial round, 2026-11-04)

Each finding was reproduced first on a private pre-fix copy (`m7s1/work/fixC/base`, the delivered tree plus the verifier's
`VerifyProbes.cs`); nothing was changed on a finder's word alone (ADR-015 §6). New tests: `Proto.M7.Tests/Spatial/VerificationFixCTests.cs`
(13 tests, every expected value hand-derived in its comment).

**Reproduction (pre-fix copy).** Delivered suite under mutants V1, V2, V3, V5, V7: all five SURVIVED (190/190 pass; log
`m7s1/work/fixC/tools/repro-mut.log`). Probes: P2 fail (step 10 → Ground t = 180 at x 20,000; step 300 → Ground t = 600 at x 62,000),
P6 fail (`ClassAt(10,0) -> Wall`), P7 fail (`Fly: OverflowException`), P9 fail (ballistic `Wall` at 380 ms vs direct fire `Hit` id 4),
P14 fail (`finished within 10 s: False`), P4 prints `not-largest 17/3641` (no overlap), P10 fingerprint `E3D8D36EDAFFE7FF`, P1 `asymmetric 0/20000`.

**Before/after for the new tests.** On the pre-fix copy (new constants replaced by literals): 5 fail, 7 pass, and the low-gravity
test does not finish (killed by a 25 s timeout). The 7 that pass there are pins for test gaps (C-V-1/2/3/7), which have no code fix;
their teeth are shown by the mutant table below. On the fixed tree: 13/13 pass.

| id | sev | verdict | evidence / change |
|---|---|---|---|
| C-V-1 | major | **FIXED (test gap)** | V7 survived 190/190 (reproduced). Added `Los_ascending_line_ridge_threshold_is_exact` (the ridge test reversed: entry 11,700, exit 13,922; 11,700 visible, 11,701 Height at (4,0)) and `Los_height_blocking_is_symmetric_on_random_height_fields` (20,000 lines). Both fail under V7. No code change; the clean code was already symmetric (0/20,000). |
| C-V-2 | minor | **FIXED (test gap)** | V2, V3, V5 survived (reproduced). Added `Ballistic_z_equal_to_ground_is_an_impact` (ground 16 = z(550) → Ground at 550; 15 → 560), `Los_corner_side_tall_cell_blocks_by_height` (Height at (2,1); top 1,700 → visible) and `Direct_fire_at_a_target_standing_on_a_hill_is_not_blocked_by_its_own_cell` (Hit id 9; the same hill one cell earlier blocks). Each kills its mutant. |
| C-V-3 | minor | **FIXED (test gap)** | V1 survived (reproduced). The delivered "lower parent index" pin did not pin it, because the lower-index parent was expanded first. Added `Astar_equal_g_from_a_later_expanded_lower_index_parent_replaces_the_parent`: 3×3 grid, Road at (0,0) so m = 700, (2,2) → (0,1). Parent 7 (f 7,959,200) offers the goal first, then parent 4 (f 8,456,000) offers equal g = 9,656,000. Expected path 8→4→3 (V1 gives 8→7→3), the same under 8 neighbour permutations. |
| C-V-4 | minor | **FIXED (code)** | Reproduced (P2). `Fly` now height-tests the 3-D chord of each step in every supercover cell, with the LOS interpolation. It stops at the entry (Z(TIn) ≤ top, a face hit) or the exit (Z(TOut) ≤ top). The arrival cell's exit is p(k), so the documented sampled rule is kept exactly, and all delivered ballistic goldens are unchanged. Tests: `Ballistic_shell_cannot_pass_through_a_raised_cell_between_samples` (step 10 and step 300 both hit at (20,000, 6,000), t = 180; z 1,341 and 1,234) and `Ballistic_face_entry_threshold_and_inside_cell_descent_are_exact` (top 1,234 → face at t 180; 1,233 → exit (24,000) at t 220, z 1,175; 1,174 → flat-ground exit of cell 13 at (56,000), t 540, z −2). **Semantic change to note:** with large steps the reported ground impact can now be a cell boundary before the sample (never after it). The spec text "first step where z ≤ ground" is met at step granularity, and the impact point within the step is refined. |
| C-V-5 | minor | **FIXED (code)** | Reproduced (P9). The ballistic disc-vs-stop comparison is now `≤`, the same tie rule as `DirectFire`. Test `Ballistic_entity_touching_a_wall_face_is_hit_like_direct_fire`: HitEntity 4 at (40,000, 6,000), z 791, t 380, step 38; 1 mm further → Wall (ballistic) and Blocked (direct). |
| C-V-6 | minor | **FIXED (code)** | Reproduced (P14 did not finish in 10 s). `Fly` rejects shots with z(MaxFlightMs) > −10^9 (MaxFlightMs = 3,600,000, **PLACEHOLDER** engineering bound, not a Director value). With the default gravity every in-bounds shot is accepted. Worst accepted cases, measured: vz = 10^7, step 1 ms → 2,039,360 steps in 176 ms; z0 = 2·10^9 → 2,222,851 steps in 204 ms. Test `Ballistic_rejects_shots_not_guaranteed_down_within_the_flight_bound` runs on a worker with a 10 s budget, so a missing bound fails instead of hanging. It also checks that vertical vz = 10^7 with 1 s steps lands at 2,040,000 ms after 2,040 steps. |
| C-V-7 | nit | **FIXED (documentation)** | Reproduced (P4: 17/3,641 not largest, no overlap). The Separation doc comment and §3.11 now state the actual guarantee: Valid(k) and !Valid(k+1), a boundary of the valid set rather than the largest k. No code change. `Separation_returns_a_valid_boundary_k_with_the_next_k_invalid` asserts that guarantee on the same 4,000 cases. It passes before and after, since nothing changed in the code. |
| C-V-8 | nit | **FIXED (code)** | Reproduced (P6: `ClassAt(10,0) -> Wall`). `TerrainGrid.Index` throws ArgumentOutOfRange for off-grid cells, so `ClassAt`/`HeightAt` do too. Hot loops use the `*AtIndex` forms or check `InBounds` first, and the LOS bench is unchanged. Test `Grid_accessors_throw_for_off_grid_cells`. |
| C-V-9 | nit | **FIXED (code)** | Reproduced (P7: `Fly: OverflowException`). `BallisticShot.MaxAbsZ0Mm` = 2·10^9 (**PLACEHOLDER** engineering bound) is checked at entry. Test `Ballistic_launch_height_is_range_checked` covers long.MaxValue−10 and ±(bound+1) rejected, and ±bound accepted. Related but out of this finding's scope: `LineOfSight.Query` also does not range-check eye and target heights (the P7 LOS call with eye = long.MaxValue returned without throwing). Left as is and noted here. |

**Mutant table (fixed tree, private copy `m7s1/work/fixC/after`).** Bound: 5× the clean build+test (≈12 s), so 60 s per mutant.
Driver `m7s1/work/fixC/tools/mut2.py`, log `m7s1/work/fixC/tools/mut-after.log`. The tree was diff-verified as restored afterwards. No mutant hung.

| # | mutation | result | killing semantic test(s) |
|---|---|---|---|
| V1 | A* equal-g lower-parent-index tie-break disabled | KILLED | `Astar_equal_g_from_a_later_expanded_lower_index_parent_replaces_the_parent` |
| V2 | ballistic `Z(TOut) ≤ top` → `<` (the z ≤ ground rule) | KILLED | `Ballistic_z_equal_to_ground_is_an_impact` |
| V3 | LOS height test skips corner-side cells | KILLED | `Los_corner_side_tall_cell_blocks_by_height` |
| V5 | DirectFire height-tests the aim cell | KILLED | `Direct_fire_at_a_target_standing_on_a_hill_is_not_blocked_by_its_own_cell` |
| V7 | LOS compares only the exit-point height | KILLED | `Los_ascending_line_ridge_threshold_is_exact`, `Los_height_blocking_is_symmetric_on_random_height_fields` |
| N1 | ballistic face entry `Z(TIn) ≤ top` → `<` | KILLED | `Ballistic_face_entry_threshold_and_inside_cell_descent_are_exact` |
| N2 | ballistic height only at the sample (pre-fix C-V-4) | KILLED | `Ballistic_shell_cannot_pass_through_a_raised_cell_between_samples`, `…face_entry_threshold…` |
| N3 | ballistic disc-vs-stop tie strict (pre-fix C-V-5) | KILLED | `Ballistic_entity_touching_a_wall_face_is_hit_like_direct_fire` |
| N4 | flight-time bound removed (pre-fix C-V-6) | KILLED (by the test's own 10 s budget, run 18.9 s, below the 60 s bound) | `Ballistic_rejects_shots_not_guaranteed_down_within_the_flight_bound` |
| N5 | launch-height check removed (pre-fix C-V-9) | KILLED | `Ballistic_launch_height_is_range_checked` |
| N6 | `Index` bounds check removed (pre-fix C-V-8) | KILLED | `Grid_accessors_throw_for_off_grid_cells` |

The delivered mutants M1–M12 (§5) were re-run on the fixed tree (`m7s1/work/fixC/tools/mut-author-after.log`): all 12 are still KILLED.

**After the fixes.** `dotnet build M7Proto.slnx -c Release`: 0 warnings, 0 errors. `dotnet test M7Proto.slnx --no-build`: **Passed 203, Failed 0**.
`scripts/check-proto-determinism.sh`: **CLEAN** (28 files). Bench (`spatial 9`) is unchanged within noise: A* open 0.29 ms, mixed 11.9 ms,
maze 4.9 ms, unreachable 19.8 ms, 20 pairs 2.47 ms/query, LOS 100k 36.1 ms (2.77 M/s), separation 120 movers 2.79 ms.
Changed code: `Projectiles.cs` (Fly, StepVisitor, bounds, doc), `TerrainGrid.cs` (Index), `Separation.cs` (doc only).

## Dated note — 2026-11-05 (D-079; appended, the text above is unchanged)

Record: `docs/d079-m7-governance-reconciliation-2026-11-05.md` (Director decisions of 2026-11-05, cited DIR5). Each bullet quotes the superseded line, states the extent and what is retained, and cites the replacing DIR5 section and D-079 decision row (DR). Categories: DIRECTOR DECISION, ADMINISTRATIVE CONSEQUENCE, DELEGATED TECHNICAL DECISION, OPEN (needs the Director), READING (labelled interpretation).

Grouped in D-079 §2 as **S483** (2 statements). This is a report, not a ruling: the notes below correct its status against DIR5 and the Phase B audit.

- (`C-spatial.md:237`) — "**O-14:** where the spatial layer lives is undecided." — in part: DIRECTOR DECISION (DIR5 §1.3) - the separate Sim.Battle project is the chosen architecture direction (O-14 option a); retained: Sim.Battle is NOT created until the architecture review, the prototype stays isolated; presentation/rendering project boundaries remain a technical recommendation (DELEGATED TECHNICAL). **Replaced by** DIR5 §1.3 (D-079 DR4–DR6). [DIRECTOR DECISION; DELEGATED TECHNICAL DECISION]
- (`C-spatial.md:257`) — "this is siege scope, which is not decided." — in part: siege scope is decided (DIRECTOR DECISION: persistent operations evaluated through battle pulses, at most three campaign-turn equivalents, wall level and fortifications in the effectiveness model, gunpowder transition). Retained: finite wall heights and indirect fire clearing walls are DELEGATED TECHNICAL ('Design and calibrate the equations'). **Replaced by** DIR5 §2.2 (D-079 DR9). [DIRECTOR DECISION; DELEGATED TECHNICAL DECISION]
