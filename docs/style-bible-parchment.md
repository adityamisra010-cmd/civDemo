# CIV-SIM VISUAL STYLE BIBLE — "PARCHMENT ATLAS"
### The single source of truth for all generated art. Every asset is generated and judged against this document. Ships to docs/ before the substrate renderer packet.

---

> **RENAME (CONV-1, 2026-08-06) — the paper texture is no longer called "grain".**
> `grain` is namespaced to the SIM domain: it is `goods.json` id 1, the numeraire against which
> every price in the world is denominated, and it is serialized. This document previously used
> "grain" for the paper fibre texture in three places (§1 substrate, §4 item 2, §5 prompt
> skeleton); all three now say **paper fibre / fibre**. Nothing here became false — it is a
> rename, not a supersession, so the wording is corrected in place rather than struck through.
>
> **Older documents keep the older phrasing on purpose.** `docs/d038-visual-target.md` §A4 says
> "grain overlay" and the art gate records use the earlier wording; those are a ratified director
> ruling and closed gate records, and closed records are never retroactively edited (S8 §5). If
> you are reading them, "grain overlay" means this document's **Fibre/age overlay**.
> Rule and rationale: `docs/conv-1-term-namespacing.md`.

---

## 0. THE ONE-LINE BRIEF
An aged explorer's atlas: a single sheet of weathered parchment on which a 6,000-year world is drawn in iron-gall ink and muted earth washes. The material never changes; only what is drawn upon it evolves. Readable first, beautiful second, never busy.

## 1. THE MEDIUM (frozen — the frame that never changes)
- **Substrate:** aged laid-paper / vellum. Warm cream, faint mottling, subtle paper fibre, gentle edge-darkening (age, not damage). NOT crisp white, NOT heavily burnt/pirate-map brown, NOT high-contrast.
- **Rendering logic:** land and sea are flat-ish tonal washes with hand-inked linework, the way a cartographer paints — NOT photoreal terrain, NOT 3D relief, NOT painterly brushstrokes (that's gouache — wrong medium). Think a fine 18th–19th c. hand-colored map, restrained and legible.
- **Lighting:** none. A map is lit flat. No cast shadows, no sun angle, no ambient occlusion. Depth comes from linework density and wash tone only. **[SUPERSEDED by D-038 Part B — replaced by the two-layer rule below; line retained per S8 §5 audit-trail convention.]**
- **Lighting (two-layer rule, D-038 Part B):**
  - THE SUBSTRATE LAYER IS LIT FLAT, FOREVER. Paper, washes, coastlines, hydrography, UI furniture. No lighting model. Unchanged from before.
  - THE OBJECT LAYER CARRIES ONE CONSISTENT LIGHT. Settlements, terrain relief, forests, armies, structures. A single global light direction, stated once as a constant and never varied per asset, with a contact shadow. Objects sit ON the paper; they are not part of it.
- **SINGLE-CARTOGRAPHER RULE:** every generated asset must use the identical paper substrate and ink chemistry. No asset may introduce a new paper color, saturation, lighting model, brush style, or decorative vocabulary. Viewed individually, every asset must appear painted by the same cartographer on the same sheet. Production method: the parchment base is generated FIRST, and all other batches are generated AGAINST that base image as visual reference.
- **Single-cartographer extension (D-038 B2):** the rule EXTENDS rather than relaxes for the object layer — every object asset must read as rendered by one hand under one light on the same sheet. One light angle, one shadow treatment, one palette. An asset lit differently is rejected, not accepted as a near-miss.

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

**Object-layer shading stops (added by D-038; desaturated warm bias above stands intact):** each land-class wash gains a lit/base/shade stop triple for the object layer's single global light — derived from that class's §2 wash hex (lit: lightened toward the paper tones; shade: deepened toward the ink tones), never introducing a new hue. Applies per land class (lowland, fertile, plain, arid, upland, peak). The stops obey the palette rule unchanged: if a stop looks vivid, it's wrong. Exact stop hexes are fixed by the visual milestone's packet against these constraints (D-038 F3/F4 — no asset work before then).

## 3. TYPOGRAPHY
- **Labels:** a humanist/old-style serif with an engraved feel (e.g. Cormorant, EB Garamond, IM Fell) — evokes hand-lettering without being an illegible script. Settlement names in ink-primary, small caps or title case, subtle letter-spacing.
- **HUD/numbers:** the SAME serif for headers; a clean, legible companion (a slab or lining-figure serif) for dense numbers and tables — readability wins over theme in the data panels.
- Never: fantasy blackletter, pirate script, or anything that fights legibility. This is a scholar's atlas, not a treasure map.

## 4. ASSET MANIFEST (what gets generated — substrate pass only; symbology deferred)
Each entry: purpose · spec · tiling/format.
1. **Parchment base texture** — the paper itself. Large, seamless, tileable, subtle (no dominant blotches that repeat visibly). 2048² PNG, tileable. Generate 2–3 variants; renderer picks one per world seed.
2. **Fibre/age overlay** — a separate faint fibre+mottle layer multiplied over everything (including UI). Very low contrast. 2048² tileable PNG, grayscale.
3. **Terrain wash tiles** — one seamless swatch per land class in §2 (lowland, fertile, plain, arid, upland, peak) + shallows/sea/deep. These are what the shader blends by elevation/moisture/fertility. 1024² each, tileable, in-palette. This single item is the biggest visual upgrade — it replaces flat color fills with painted parchment.
4. **Coastline ink treatment** — a thin darker ink band the renderer draws where land meets sea (the classic hand-map coast line), plus optional faint parallel "engraved sea" lines offshore. Spec as a shader effect + one hairline texture, not a full asset.
5. **UI frame furniture** — panel border/corners, a header rule, button plate, a scroll/parchment panel background for the Annals, a compass rose (decorative, corner). In-palette, ink-on-parchment. PNGs with transparile edges (9-slice-friendly where possible).
6. **Icon seeds (substrate-safe only)** — the generic settlement marker in the parchment style (a small inked ring/dot that reads at all zooms) and the core HUD stat glyphs IF they can be drawn timelessly (food, population, labor). Anything era-specific (city tiers, ports, production, armies) is DEFERRED to the symbology packet — do not generate it now.

**SEAMLESS CLAUSE (applies to every tileable entry above — 1, 2, 3, and any tiling UI fill):** tileable textures must edge-wrap — left edge continues into right, top into bottom, no seam, border, or central focal point. Verify by 2×2 tiling before acceptance; regenerate any tile showing a seam or a visibly repeating feature.

**Explicitly NOT this pass:** settlement size tiers, road/rail/trade visual language, army/border/politics symbology, map-mode legend art. Those wait for the M4/M5 art-direction packet (queue). Generating them now guarantees a redraw.

**OBJECT TIER (added by D-038 Part A4/B1 — built only at the inserted visual milestone after M5, per D-038 Part E; the substrate manifest above survives unchanged):**
7. **Settlements and structures** — size legible at a glance without reading a number (D-038 G2).
8. **Terrain relief** — depth on the object layer under the single global light.
9. **Forests, fields, roads and bridges.**
10. **Ships and carts.**
11. **Army and formation tokens, banners** — silhouettes/token clusters per §7 anatomy fence and D-011 §4.
12. **Borders, production and resource glyphs.**

All object-tier entries carry the object layer's one consistent light with contact shadow (§1 two-layer rule) and are subject to the single-cartographer extension (§1) and the anatomy fence (§7). Production method per §5's D-038 ordering (procedural / parametric render / image generation third-choice).

## 5. THE PROMPT SKELETON (paste into the image generator; fill the ⟨slot⟩)
> "⟨asset⟩, in the style of an aged 18th–19th century hand-drawn explorer's atlas on weathered cream parchment. Iron-gall ink linework, muted earth-tone washes, desaturated and warm. Flat cartographic rendering — no 3D, no dramatic lighting, no photorealism, no painterly brushstrokes. Restrained, scholarly, legible. Color palette limited to warm creams, soft umbers, muted sage greens, and grey-blue seas. Seamless tileable texture, no border, no text, no labels. ⟨extra per asset⟩"

Per-asset extra slots:
- terrain tiles: "even seamless field of ⟨class⟩ terrain wash, subtle tonal variation only, must tile edge-to-edge"
- parchment base: "blank aged paper, faint paper fibre and gentle mottling, no imagery"
- UI frame: "ornamental but restrained border element, ink on parchment, transparent background"

**Consistency discipline (the thing that prevents drift):** generate ALL assets in one session with the same skeleton; regenerate any that stray rather than accepting a near-miss; check every tile against the palette; verify seamless tiles actually tile (drop into a 2×2 grid and look for seams/repeats). One off-style asset poisons the coherence of the whole map.

**SEAMLESS CLAUSE:** tileable textures must edge-wrap — left edge continues into right, top into bottom, no seam, border, or central focal point. Verify by 2×2 tiling before acceptance; regenerate any tile showing a seam or a visibly repeating feature.

**PRODUCTION METHOD ORDERING (D-038 Part D):** two production methods are sanctioned, chosen by asset kind, not by convenience:
1. **PROCEDURAL, IN CODE** — the default for anything geometric (terrain washes, borders, glyphs, icon tiers, UI furniture, tokens). Precedent: HeaderRuleBaker (T3.8-era D-A1), palette-exact and seamless BY CONSTRUCTION.
2. **PARAMETRIC RENDER** — for objects wanting depth. A scripted scene description, rendered headless to sprite sheets, committed as assets with the script beside them. The script is the asset's source; the sprite is its build output. Assets must be REGENERABLE — a palette change regenerates every asset rather than invalidating them.
3. **IMAGE GENERATION is third-choice.** Its recorded failure (D-A1, docs/art-gate-defects.md: alpha>127 at 0.46% and 0.90%, off-palette #A77032) is a verdict on generation FOR GEOMETRIC SLOTS, not on generation generally; it remains viable for organic and illustrative material.

**THE PARAMETRIC RENDER SPEC (companion to the prompt skeleton, per D-038 D3):** every parametric-render asset states its **scene**, **camera**, **light angle** (the single global constant of §1's object layer — never varied per asset), **output resolution**, and **palette binding** (which §2 hexes each material binds to, so regeneration follows a palette change).

## 6. ACCEPTANCE (how the substrate packet is judged)
- All manifest §4 items present in-palette and, where tileable, seam-free in a 2×2 test.
- The map renders as painted parchment with inked coasts and rivers, no flat color blocks, no visible tile grid at any zoom.
- UI panels wear the frame furniture; Annals reads on a parchment panel; typography per §3.
- The medium reads identically at year −4000 and any later date (frame is era-invariant).
- Nothing era-specific was generated (symbology deferral respected).
- Director visual gate: it looks like an atlas, not a debug tool. **[SUPERSEDED for the object layer by D-038 Part G — bar DISCHARGED; retained per S8 §5 audit-trail convention. Substrate acceptance above stands.]**

**Object-layer acceptance (D-038 Part G):**
- The new bar: A SETTLEMENT'S SIZE IS LEGIBLE AT A GLANCE WITHOUT READING A NUMBER; an army reads as an army; terrain has relief; and the whole reads as one illustrated map rather than a set of assets on a shared background.
- The single-cartographer test survives as the failure condition: any asset that reads as lit differently, or drawn by a different hand, is rejected.
- Director visual gate as ever. Automated tests cover palette exactness, seamlessness and regenerability; the look is the director's call (D-023).

## 7. THE ANATOMY FENCE (added by D-038 Part C — director's explicit constraint)
- **NO ANATOMICALLY DETAILED CHARACTERS.** Not now, not later, at any milestone. People never render as figures with faces, limbs or animation cycles.
- **Permitted and to be pushed hard:** settlements and structures, terrain relief, forests, fields, roads and bridges, ships and carts, army and formation tokens, banners, borders, production and resource glyphs.
- **Where people must appear they are silhouettes or tokens.** Already ruled for battles — D-011 §4: "each token drawn as a cluster of tiny sprites thinning as strength drops — Ultimate-General-style". D-038 C1 generalises that ruling to the whole visual layer rather than creating a new one.
- **The fence is also the right design:** at map scale a figure reads as a smudge; a silhouette reads as an army. The constraint costs nothing the game needs.
