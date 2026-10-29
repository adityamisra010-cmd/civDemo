# E1 — the object layer: decisions, budget and measurements (M6 closure track E1, 2026-10-28)

**Status:** IMPLEMENTED in the E1 patch; presentation only (Sim.Ui). The Part G gate is NOT held
(`docs/m6-plan/e1-visual-acceptance-checklist.md`). Conventions: style bible §8 (`docs/style-bible-parchment.md`).

**Authority:** D-066 R2 ("E1 visuals"), A3 (register from realized state; Age banner only Age-keyed; one Part G gate),
A4 (bounded parts, overflow fold, number set in the conventions); D-038 Parts B, C, D1, G, H; D-043 D2; D-052 §6–§7
(and its 2026-10-20 note deferring §7 to E1); ADR-009 (nothing in Sim.Ui can move a world hash).

**No simulation change.** No file under Sim.Core, Sim.Data or Sim.Cli is touched; no golden moves (the golden tests were
run by name — see the hand-back).

## 1. Decisions (each simplest, deterministic, testable, reversible — D-066 §2's rule)

| # | Decision | Basis / rationale |
|---|---|---|
| E1-1 | **One light** from the north-west; contact shadow = the silhouette translated along (2, 1)/√5 by 0.16 × its height, ink #2C2318 at 32 %; lit stop 38 % toward Paper light, shade stop 30 % toward Ink primary; objects under 3 px drawn flat. One constant class (`ObjectLight`), one shadow painter (`ObjectPaint.Solid`). | D-038 B1 ("stated once as a constant"), H4 (same light and ground plane for every part); style bible §2 stops. The NW light is the cartographic convention; 0.16 keeps every part's shadow inside its P12a glyph box, so no lot or footprint validation changes. |
| E1-2 | **Procedural only**, convex polygons only. | D-038 D1 (procedural first for geometric things); the ImGui backend fills convex polygons only. |
| E1-3 | **Count stages** 1/10/20/30 in `settlement-classes.json parts.countStages` (validated: four ascending thresholds from 1). | D-043 D2 (ratified numbers; "data-defined"). |
| E1-4 | **"High zoom"** for a part's number = the Settlement band with the part's glyph ≥ 14 px (camera ≥ 22.4 px per world px; the camera's maximum is 32). The key beside the footprint names every count at the Settlement band regardless. | D-043 D2 ("at high zoom, the visual can display the number"); below 14 px a tag is larger than the part it labels (measured on the previews). |
| E1-5 | **Parts limit `maxParts` = 8**; significance = (institution first, count DESC, key ASC) — integer composite key; overflow = ONE part on its own lot (a claim after every kind's, so no lot moves), its number "+N" = kinds folded, its marker = the folded total. | D-066 A4 ("the number is set in the E1 conventions doc"); 8 = the 7 kinds shipped content can hold + 1, ≤ the 14 lots. Reversible: one JSON number. |
| E1-6 | **Under construction** = a scaffold on the kind's lot (the lot the built part takes); the state is `ConstructionQueue` rows and founding projects built-but-not-founded (InstitutionsQuery.Lifecycle's rule); carried only where the player reads the settlement's books (D-060). Not a composition part, so P12a's composition digest is unchanged. | P12b plan; P12a "only what is built is a part" pin kept. |
| E1-7 | **Style register**: earthen → timber (any structure, or a trackway) → masonry (an institution, 10 structures, or a built/paved road) → engineered (20 institutions, or macadam/highway), the max of the rungs earned; the route input is the class ACHIEVED, never the target. It chooses roofs and walls only. | D-066 A3; Law 4 (capability from computed state); D-038 H5. Thresholds are code constants (`StyleRegister`), TUNE-able. |
| E1-8 | **Terrain marks** (hills, mountains, peaks, forests) baked once per terrain from the immutable rasters with hard thresholds, drawn from the Regional band in, only on known nodes, as a new lens-owned map layer `ObjectTerrain` / information layer `TerrainRelief` (legend: "decorative"). The World band's layer list is unchanged. | The substrate pin (`EraSurfaceTests`) forbids touching the bake; D-047 Part 5's World-band density is pinned (`Layers_PerZoom_AreTheRuledInformationDensity`) — left to the Director at the gate (checklist G2.9). |
| E1-9 | **Fields** only for a settlement whose cultivation is REALIZED (`LabourActivities.HarvestsWildFood` false — the economy's own predicate), on its known fertile catchment nodes nearest its site, up to its farming share (the ordered split, else the economy's `Sectors.Default`), only where the player reads its books. | Law 4 / D-066 A3: no visual implies a capability not exercised. |
| E1-10 | **Formation silhouettes** keyed by identity: role shape from the identity's family, variant from its form; convex polygons ≤ 8 vertices, ≤ 12 per token, inside the plate above the P11 pips. P11's `UnitToken` painter is kept (its C1 pin unchanged); the lens draws `FormationToken`. | D-052 §6 ("authored silhouettes"), D-038 C1/C3. |
| E1-11 | **D-052 §7 grouped token**: one token per (owner, anchor, identity); id = lowest member id (what a click selects); members listed on `UnitPlacement.Members`; groups in table order of their first member; fan slots by representative id; the number above the plate = formations, never people; the card's composition line names the grouped ids. Legend and absent line re-pinned; the playability gate's formation checks now find "the token holding" a formation. | D-052 §7 "governs over the per-formation, no-count default that P11 shipped"; 2026-10-20 note deferring it to E1. |
| E1-12 | **Institutions drawn generically from content**: university keys 1–5 keep their H1 emblems; any other type by a word of its content name (court/law/justice → scales, …), else the generic hall. The CIV track's law court gets a hall with scales with no coordination. | Brief's reconciled facts; D-066 A1 (institution types content-driven). |
| E1-13 | **Tooling**: `--frame-cost map … --size PX` (the high-count world at another size; measurement only); `--e1-preview DIR [--quick]` (SVG + PNG evidence); `FrameRaster.Rasterize(…, under)` (a background image for those PNGs). | The canonical cylinder sites at most 362 settlements (measured: 547 at 1536 px, 696 at 2048 px, 901 at 2560 px), so 800 needs a larger dev-size world. |

## 2. Frame-cost budget (E1.0) and re-measurement (E1.6)

**E1.0 — the baseline, recorded before any rendering change.** The pre-E1 build (the base tree plus the measurement-only
`--size PX` option) was copied aside and swept: `--frame-cost map --rounds 3 --settlements 300` (canonical size) and
`--frame-cost map --rounds 3 --settlements 800 --size 2560`. **The budget, stated with it: every (window, world, band) row of
the plain-lens worlds (canonical, high-count) within 2.0 × its baseline median ms/frame**, the baseline re-measured at E1.6
by running the base binary and the E1 binary alternately (base, E1, base, E1) under the same load. Overlay worlds reported,
not budgeted (E1 does not change the overlay).

**E1.6 — MEASURED (two alternating rounds each side, 3 sweep rounds per run; a shared 4-CPU container, load average 2–7; every
world's hash unchanged by every sweep: canonical `5778d20b…`, 300-settlement `f9edf55e…`, 800-settlement `0c4a56da…`).**
The first E1 measurement put 25 of 48 rows over budget (worst 7.0 ×, the canonical 12-settlement map at 3840 × 2160 where the
base lens costs 0.14 ms); a per-frame bound on terrain-mark places (`TerrainMarks.MaxPlacesPerFrame`: a larger window draws
larger marks, not more) brought it to the table below. **Result: 28 of 48 rows within 2.0 ×; 20 rows OVER — the budget is
NOT MET.**

| world | rows within 2.0 × | rows over | the over rows |
|---|---|---|---|
| canonical (12 settlements, turn 150) | 10 of 24 | 14 | Regional and Settlement bands at most windows: base 0.14–0.35 ms → E1 0.28–0.98 ms (2.0–4.8 ×) |
| high-count 300 (turn 1) | 10 of 12 | 2 | Settlement band at 1920 and 2560: base 0.44–0.63 → 1.1–1.7 ms (2.6–2.8 ×) |
| high-count 800 (2560 px, turn 1) | 8 of 12 | 4 | Settlement band: base 0.8–1.3 → 1.8–2.7 ms (2.0–2.2 ×) |

Reading (counts MEASURED; causes INFERRED from the command counts): every over row is one whose BASE cost is a near-empty map
(the canonical world's 12 settlements, or a Settlement-band view of a turn-1 world) — 0.14 to 1.3 ms — to which the object
layer adds a screen's worth of relief and forest marks (commands/frame 613 → 885–1,530 on the canonical Settlement band).
**The most expensive row of any world is unchanged: high-count 800 at the World band, 3.9–4.9 ms base → 4.8–5.3 ms E1**
(the World band's plateau draws no terrain marks; its cross-fade frames do); the largest E1 row elsewhere is 2.8 ms. Max list vertices stay below the base's own
maximum (170,838, the 800-world World band). The full tables: the coordinator's `scratchpad/m6/e1-evidence/
frame-cost-comparison.md` (final) and `…/v1/` (before the bound).

**Options for the Director/coordinator (not decided here):** (a) accept with the budget restated per window as the worst
band's absolute cost (every window's worst row is within 1.5 × of the base's worst row, the 1.5 from one noisy E1 sweep that
read 4.34 ms against 2.73 in the other); (b) thin the marks further
(`TerrainMarks.MinScreenPitchPx`, `ThinFraction`, `MaxPlacesPerFrame` are TUNE constants) and re-measure; (c) draw relief and
forests only from the Settlement band's plateau. The gate's legibility verdict (checklist G2.8) bears on (b) and (c).

## 3. What is not done / open

- The Part G gate (G2, G3, C1–C4) is the Director's and is NOT held.
- The frame-cost budget is not met in 20 of 48 rows (§2).
- Relief is not drawn at the World band (D-047 Part 5's pinned density; checklist G2.9).
- The composite stays small inside P12a's generous footprint at high zoom (re-laying P12a's lattice would move its pinned
  composition digest; left to the gate).
- Roads and bridges, ships and carts as objects (style bible §4 items 9–10) beyond what exists are not drawn: no state
  carries a bridge, a ship's position or a cart.
