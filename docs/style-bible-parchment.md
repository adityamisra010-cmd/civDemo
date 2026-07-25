# CIV-SIM VISUAL STYLE BIBLE — "PARCHMENT ATLAS"
### The single source of truth for all generated art. Every asset is generated and judged against this document. Ships to docs/ before the substrate renderer packet.

---

## 0. THE ONE-LINE BRIEF
An aged explorer's atlas: a single sheet of weathered parchment on which a 6,000-year world is drawn in iron-gall ink and muted earth washes. The material never changes; only what is drawn upon it evolves. Readable first, beautiful second, never busy.

## 1. THE MEDIUM (frozen — the frame that never changes)
- **Substrate:** aged laid-paper / vellum. Warm cream, faint mottling, subtle fiber grain, gentle edge-darkening (age, not damage). NOT crisp white, NOT heavily burnt/pirate-map brown, NOT high-contrast.
- **Rendering logic:** land and sea are flat-ish tonal washes with hand-inked linework, the way a cartographer paints — NOT photoreal terrain, NOT 3D relief, NOT painterly brushstrokes (that's gouache — wrong medium). Think a fine 18th–19th c. hand-colored map, restrained and legible.
- **Lighting:** none. A map is lit flat. No cast shadows, no sun angle, no ambient occlusion. Depth comes from linework density and wash tone only.

## 2. PALETTE (authoritative hex — all art generated to these; renderer tints to these)
Parchment base:
- Paper light `#EFE3C8` · Paper mid `#E3D3AE` · Paper shade/edge `#C9B588`
Ink:
- Ink primary (coast, borders, labels) `#3A2E1F` · Ink soft (contours, hatching) `#6B5A3E`
Land washes (elevation/vegetation, low→high):
- Lowland green `#A9B080` · Fertile green `#8F9C63` · Plain tan `#C4B183` · Arid `#CBBE93` · Upland umber `#A98B63` · Peak pale `#DCC9A0`
Water:
- Shallows `#9DB3B0` · Sea `#7C99A0` · Deep sea `#5F7E88` (all muted, ink-washed — NOT saturated blue)
Rivers: Sea/`#6E8A93`, drawn as ink-blue hairlines.
Accents (sparingly, for symbology later):
- Iron-red `#8C4A3A` (borders, war) · Verdigris `#5E7A6B` · Gold-leaf `#B08A3E` (rare emphasis)
**Rule:** everything desaturated and warm-biased. If a swatch looks vivid, it's wrong. Territory tints (the 12 political colors) are generated as *ink washes over parchment*, not opaque fills — muted, ~35% strength.

## 3. TYPOGRAPHY
- **Labels:** a humanist/old-style serif with an engraved feel (e.g. Cormorant, EB Garamond, IM Fell) — evokes hand-lettering without being an illegible script. Settlement names in ink-primary, small caps or title case, subtle letter-spacing.
- **HUD/numbers:** the SAME serif for headers; a clean, legible companion (a slab or lining-figure serif) for dense numbers and tables — readability wins over theme in the data panels.
- Never: fantasy blackletter, pirate script, or anything that fights legibility. This is a scholar's atlas, not a treasure map.

## 4. ASSET MANIFEST (what gets generated — substrate pass only; symbology deferred)
Each entry: purpose · spec · tiling/format.
1. **Parchment base texture** — the paper itself. Large, seamless, tileable, subtle (no dominant blotches that repeat visibly). 2048² PNG, tileable. Generate 2–3 variants; renderer picks one per world seed.
2. **Grain/age overlay** — a separate faint fiber+mottle layer multiplied over everything (including UI). Very low contrast. 2048² tileable PNG, grayscale.
3. **Terrain wash tiles** — one seamless swatch per land class in §2 (lowland, fertile, plain, arid, upland, peak) + shallows/sea/deep. These are what the shader blends by elevation/moisture/fertility. 1024² each, tileable, in-palette. This single item is the biggest visual upgrade — it replaces flat color fills with painted parchment.
4. **Coastline ink treatment** — a thin darker ink band the renderer draws where land meets sea (the classic hand-map coast line), plus optional faint parallel "engraved sea" lines offshore. Spec as a shader effect + one hairline texture, not a full asset.
5. **UI frame furniture** — panel border/corners, a header rule, button plate, a scroll/parchment panel background for the Annals, a compass rose (decorative, corner). In-palette, ink-on-parchment. PNGs with transparile edges (9-slice-friendly where possible).
6. **Icon seeds (substrate-safe only)** — the generic settlement marker in the parchment style (a small inked ring/dot that reads at all zooms) and the core HUD stat glyphs IF they can be drawn timelessly (food, population, labor). Anything era-specific (city tiers, ports, production, armies) is DEFERRED to the symbology packet — do not generate it now.

**Explicitly NOT this pass:** settlement size tiers, road/rail/trade visual language, army/border/politics symbology, map-mode legend art. Those wait for the M4/M5 art-direction packet (queue). Generating them now guarantees a redraw.

## 5. THE PROMPT SKELETON (paste into the image generator; fill the ⟨slot⟩)
> "⟨asset⟩, in the style of an aged 18th–19th century hand-drawn explorer's atlas on weathered cream parchment. Iron-gall ink linework, muted earth-tone washes, desaturated and warm. Flat cartographic rendering — no 3D, no dramatic lighting, no photorealism, no painterly brushstrokes. Restrained, scholarly, legible. Color palette limited to warm creams, soft umbers, muted sage greens, and grey-blue seas. Seamless tileable texture, no border, no text, no labels. ⟨extra per asset⟩"

Per-asset extra slots:
- terrain tiles: "even seamless field of ⟨class⟩ terrain wash, subtle tonal variation only, must tile edge-to-edge"
- parchment base: "blank aged paper, faint fiber grain and gentle mottling, no imagery"
- UI frame: "ornamental but restrained border element, ink on parchment, transparent background"

**Consistency discipline (the thing that prevents drift):** generate ALL assets in one session with the same skeleton; regenerate any that stray rather than accepting a near-miss; check every tile against the palette; verify seamless tiles actually tile (drop into a 2×2 grid and look for seams/repeats). One off-style asset poisons the coherence of the whole map.

## 6. ACCEPTANCE (how the substrate packet is judged)
- All manifest §4 items present in-palette and, where tileable, seam-free in a 2×2 test.
- The map renders as painted parchment with inked coasts and rivers, no flat color blocks, no visible tile grid at any zoom.
- UI panels wear the frame furniture; Annals reads on a parchment panel; typography per §3.
- The medium reads identically at year −4000 and any later date (frame is era-invariant).
- Nothing era-specific was generated (symbology deferral respected).
- Director visual gate: it looks like an atlas, not a debug tool.
