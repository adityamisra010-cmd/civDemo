# assets/ — the art drop point

Everything the renderer draws lives here. The renderer addresses art by
**manifest key** (`Sim.Ui/Art/AssetManifest.cs`), never by hard-coded path, so
**replacing any file below with real generated art requires no code change and
no rebuild of the manifest** — drop the PNG on top and relaunch.

## What is here today

**Real art (11 keys):** the parchment sheet, the grain overlay, and all nine
terrain washes — the director's generated 1254² assets. **Stand-ins (7 keys):**
the coast hairline and the six UI furniture pieces are still programmatic
placeholders from `Sim.Ui/Art/PlaceholderArt.cs`.

Audit any drop — which keys are real, which are stand-ins, which files are
orphaned:

```bash
dotnet run --project Sim.Ui -c Release -- --audit-assets ./assets
```

Regenerate missing stand-ins (existing files are **never** overwritten):

```bash
dotnet run --project Sim.Ui -c Release -- --generate-placeholder-assets ./assets
```

### Naming is forgiving

Each key accepts its primary filename **plus aliases**, so art named by a
generation session drops in unchanged — `terrain/deep.png` also answers to
`deepsea.png`/`deep-sea.png`, and the audit prints which name resolved.

### Paper variants are optional

`parchment/parchment.png` is the single sheet used by **every** world seed. The
bible allows 2–3 variants (`base-1.png`, `base-2.png`); when more than one
resolves, the seed picks among them. Un-provided variants are not deficiencies
and are excluded from the placeholder count.

## The manifest (style bible §4)

| Path | Purpose | Tileable |
|---|---|---|
| `parchment/parchment.png` | **The paper** (primary; used by every seed) | ✅ |
| `parchment/base-{1,2}.png` | Optional extra sheets; seed picks among those present | ✅ |
| `parchment/grain.png` | Age/fibre overlay, multiplied over map **and** UI | ✅ |
| `terrain/{lowland,fertile,plain,arid,upland,peak}.png` | Land washes, blended by elevation/moisture/fertility | ✅ |
| `terrain/{shallows,sea,deep}.png` | Water washes, blended by depth (`deep` ← `deepsea.png`) | ✅ |
| `ink/coast-hairline.png` | Offshore engraved hairline | — |
| `ui/panel.png` | 9-slice panel border/corners | — |
| `ui/header-rule.png` | Rule under panel titles | — |
| `ui/button-plate.png` | Button plate | — |
| `ui/annals-bg.png` | Parchment sheet behind the Annals | — |
| `ui/compass-rose.png` | Corner compass rose (decorative) | — |
| `ui/settlement-marker.png` | Generic inked settlement marker | — |
| `fonts/` | EB Garamond + IBM Plex Serif (OFL 1.1) — see `fonts/README.md` | — |

Sizes are **not** fixed by the code: every image is sampled by UV, so the
bible's 2048²/1024² art drops straight in over these smaller stand-ins.

## What the drop must satisfy (enforced by tests, not by trust)

`Sim.Ui.Tests/AssetSeamTests.cs` runs against whatever is in this folder:

- **Seamless clause (§4/§5)** — every tileable asset must edge-wrap (left into
  right, top into bottom) with no seam and no central focal point. The 2×2
  tiling check is automated: the join's roughness may not exceed 3× the tile's
  own interior roughness. *Measured on the real drop: worst ratio 1.73.*
  Whatever residual remains is then removed at the sampling stage by the
  cross-fade (below), so the RENDERED sheet shows no seam at all.
- **Single-cartographer rule (§1)** — judged in aggregate against the bible's
  own most saturated ink (0.648): mean and 99.9th-percentile saturation must be
  inside the gamut, and under 0.01% of pixels may exceed it. *Measured: every
  asset's mean 0.01–0.49, p99.9 ≤ 0.56; `terrain/lowland` carries 5 ink specks
  in 1.57 M pixels (0.0003%).*

## Two things the renderer does FOR the art

- **Seam cross-fade.** Generated art only approximately edge-wraps. Near a tile
  edge the sampler blends to a second tap half a tile away (where the seam maps
  to tile interior), so the discontinuity cannot reach the output. *Rendered
  seam ratio: 1.235 without, 1.001 with.*
- **Auto tile scale.** Tiles are shown at their own resolution
  (`tileWidth / supersample` world px per tile) rather than a fixed span, so a
  1254² wash is not minified 5.7:1 into mush — and the repeat period stretches
  to ~627 px, longer than half the canonical world.

A **missing or corrupt** file is never fatal: `AssetLibrary` substitutes the
programmatic placeholder, counts it, and the debug panel reports
`art: N/M PLACEHOLDER`.

## Deferred — do not add here yet

Settlement size tiers, roads/trade, army/border/politics symbology, map-mode
legend art. Those belong to the post-M4/M5 symbology packet (`docs/queue.md`);
generating them now guarantees a redraw.
