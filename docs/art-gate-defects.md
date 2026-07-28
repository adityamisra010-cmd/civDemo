# Art-substrate visual gate — the three director-reported defects

**Recorded 2026-07-28, before any fix, at the director's instruction.** These three defects existed
only in the director's head until this file: the handoff status doc correctly reported that no
record of them existed in the tree and declined to reconstruct them. ADR-015 §7.12 — a
director-stated defect that lives only in conversation is a measurement nobody can check. This file
is the record; the statuses below are updated as the close-out packet lands.

| # | defect | status |
| --- | --- | --- |
| D-A1 | The header rule is mis-scaled: drawn with uv (0,0)–(1,1) and a hardcoded 8f height, so the native 64×8 asset is stretched across the whole panel width. The ornament smears and its ink weight changes with panel size. | **FIXED** — `PanelFurniture.HeaderRuleUv`: native dims from the loaded asset, uniform scale set by the vertical mapping, horizontal overflow tiled; ink-weight invariance pinned by `PanelFurnitureTests` (proven red 5/9 against the old u=1 extent). **GATE 2026-07-28: FAILED — see §D-A1 gate verdict below. The draw-path fix is CORRECT and stays; the ASSET is the defect. OPEN, blocked on a director-supplied replacement asset.** |
| D-A2 | Settlement markers are too small to click (hit radius 11 px), and the name labels are not clickable at all — `HitTest` tests marker-centre distance only, so the label region is dead to the mouse. | **FIXED** — hit target derived from the named 44 px standard (Apple HIG / WCAG 2.5.5 Enhanced): `MinTargetDiameterPx = 44`, radius 22; marker raised 14→20 px as a visual affordance inside the target. Labels clickable via renderer-measured `LabelRect`s handed into the pure view-model; ranking rule stated (marker-centre distance ASC, id ASC, whatever shape admitted) and pinned tie-dense. Proven red 2/12 with label admission removed. **GATE 2026-07-28: PASS.** |
| D-A3 | River width scales with zoom: `RiverMesh` widths are in WORLD units, so a 2.4 world-px river is ~76 screen px at 32×. Markers and labels are constant screen size; rivers are the odd one out, and the style bible §2 calls rivers "ink-blue hairlines". | **FIXED** — option (iii): screen width = clamp(worldWidth × zoom, 1.0 px, 6.0 px). Mesh stays world-space (half-width = clampedScreenWidth/zoom) so the existing transform draws it; rebuilt on zoom change at a measured 0.533 ms (canonical 1024², 11,214 vertices). Rank ordering pinned as a property test across a 0.25–32× zoom sweep (proven red 2/6 with the clamp removed). **GATE 2026-07-28: PASS.** |

## Distinct from the three queued UI-polish items

These are NOT the three items already in `docs/queue.md` (terrain detail-on-zoom, river-polyline
Chaikin corner smoothing, true river breadth from discharge). Those stay queued and are not done
here. In particular, D-A3 is about the zoom **response** of river width, not about where the width
comes from — the "true river breadth from discharge/accumulation" queue item is unaffected and
still open.

## Acceptance

The D-023 Visual Gate on the CI artifact, run by the director. Tests in this packet guard the
arithmetic; the gate is the acceptance.

## D-A1 gate verdict (2026-07-28): the rule is INVISIBLE, not mis-scaled — asset defect, draw path correct

**Director's measurement:** `assets/ui/header-rule.png` is 1536×1024 with alpha > 127 on only
0.46% of pixels. At the pinned texel density of 128, each drawn pixel samples a 128×128 source
region that is 99.5% near-transparent — the rule renders as nothing. The constant-ink-weight draw
path (`PanelFurniture.HeaderRuleUv`) is CORRECT and stays. Do not revert it; do not fix the asset
speculatively — **the director supplies the replacement.**

### Review finding against this packet's own tests (ADR-015 §7.5 + §7.2)

Recorded at the director's instruction, against my own acceptance evidence:

- `Assert.Equal(1024.0 / h, dyN, 3)` — texel density = 128.000 at both widths — is true **by
  construction of the formula under test** and cannot fail while the formula exists. That is a
  quantity resting against its own definition (§7.5's shape, one level up).
- "Ink weight identical at 200 px and 900 px" is satisfied by **zero ink at both widths**. The
  test certified an invisible rule. Teeth are not aim (§7.2): the assertions had teeth against the
  old stretch, and were aimed at the wrong property for the new asset.
- **The missing test is a VISIBILITY assertion** and it does not exist: over the drawn strip's
  sampled region, the alpha-weighted ink coverage per screen pixel must exceed a stated floor.
  It ships with the replacement asset, proven red against the current one.

### Required replacement asset spec

| property | requirement |
| --- | --- |
| geometry | WIDE and SHORT — a rule, not a sheet. Native height within a small multiple of the 8 px display height (≤ 64 px tall); width sized for horizontal tiling. |
| tiling | SEAMLESS IN X: left and right edges must meet cleanly, because `HeaderRuleUv` tiles the overflow horizontally (u > 1 on wide panels). |
| alpha | REAL alpha channel, keyed against the parchment cream **#EFE3C8**, not white — the 2026-07-28 repair white-keyed by luminance, which is the correct mechanical transform but cannot conjure coverage that is not drawn. Ink coverage must be a rule's worth: a drawn line spanning the full width, not 0.46% of a mostly-empty sheet. |
| ink | inside the style bible §2 gamut (the burnt-sienna deviation is resolved; keep it resolved). |

### AssetManifest decision

`ui/header-rule` is registered `Tileable: false`, which exempts it from the §4/§5 seamless-clause
checks — written when the rule was drawn once across the panel. Under `HeaderRuleUv` the asset IS
tiled in x. **Decision: flip to `Tileable: true` (or a Tileable-in-X variant if the checker is
axis-aware) in the same commit that lands the replacement asset**, so the seam check and the asset
that must satisfy it arrive together and the flag never fronts an asset that fails it.
