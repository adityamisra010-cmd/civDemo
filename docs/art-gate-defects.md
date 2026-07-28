# Art-substrate visual gate — the three director-reported defects

**Recorded 2026-07-28, before any fix, at the director's instruction.** These three defects existed
only in the director's head until this file: the handoff status doc correctly reported that no
record of them existed in the tree and declined to reconstruct them. ADR-015 §7.12 — a
director-stated defect that lives only in conversation is a measurement nobody can check. This file
is the record; the statuses below are updated as the close-out packet lands.

| # | defect | status |
| --- | --- | --- |
| D-A1 | The header rule is mis-scaled: drawn with uv (0,0)–(1,1) and a hardcoded 8f height, so the native 64×8 asset is stretched across the whole panel width. The ornament smears and its ink weight changes with panel size. | **FIXED** — `PanelFurniture.HeaderRuleUv`: native dims from the loaded asset, uniform scale set by the vertical mapping, horizontal overflow tiled; ink-weight invariance pinned by `PanelFurnitureTests` (proven red 5/9 against the old u=1 extent). Awaiting visual gate. |
| D-A2 | Settlement markers are too small to click (hit radius 11 px), and the name labels are not clickable at all — `HitTest` tests marker-centre distance only, so the label region is dead to the mouse. | **FIXED** — hit target derived from the named 44 px standard (Apple HIG / WCAG 2.5.5 Enhanced): `MinTargetDiameterPx = 44`, radius 22; marker raised 14→20 px as a visual affordance inside the target. Labels clickable via renderer-measured `LabelRect`s handed into the pure view-model; ranking rule stated (marker-centre distance ASC, id ASC, whatever shape admitted) and pinned tie-dense. Proven red 2/12 with label admission removed. Awaiting visual gate. |
| D-A3 | River width scales with zoom: `RiverMesh` widths are in WORLD units, so a 2.4 world-px river is ~76 screen px at 32×. Markers and labels are constant screen size; rivers are the odd one out, and the style bible §2 calls rivers "ink-blue hairlines". | OPEN |

## Distinct from the three queued UI-polish items

These are NOT the three items already in `docs/queue.md` (terrain detail-on-zoom, river-polyline
Chaikin corner smoothing, true river breadth from discharge). Those stay queued and are not done
here. In particular, D-A3 is about the zoom **response** of river width, not about where the width
comes from — the "true river breadth from discharge/accumulation" queue item is unaffected and
still open.

## Acceptance

The D-023 Visual Gate on the CI artifact, run by the director. Tests in this packet guard the
arithmetic; the gate is the acceptance.
