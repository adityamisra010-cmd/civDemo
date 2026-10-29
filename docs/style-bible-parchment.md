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

   > **AMENDED 2026-10-06 (M5 polish, UI readability UR-2; Director directive 2026-10-06 §2 — "gameplay information
   > beats decorative texture").** The overlay is still multiplied over the whole frame, map and interface alike, so the
   > window still reads as one sheet of paper — but at two amplitudes: the MAP takes the fibre at full depth; the
   > INTERFACE (every panel, chip, card and label) takes it at 35 % of its depth (`factor' = 1 − 0.35 × (1 − fibre)`,
   > mean factor 0.952 instead of 0.864 on the shipped asset). Measured reason: at full depth the overlay consumed a
   > quarter of the text's contrast budget (A1 body text 9.78:1 on its tokens, 7.57:1 rendered). The asset is
   > unchanged; the host draws it in two multiply passes (`Sim.Ui/Art/FibreOverlay.cs`: the map's remainder before the
   > interface, the soft share after it). Panels and cards likewise keep their era texture in the FRAME BAND and are calm
   > under the words (`PanelFrame`: marks over a content rect are one in four at alpha ≤ 0.06). The sentence above is
   > kept as written (S8 §5 audit trail); this note is its current reading.
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

---

> **NOTE (2026-10-02, append-only) — the UI frame now follows the player's Age (ADR-033 D8).**
> The §6 acceptance line "The medium reads identically at year −4000 and any later date (frame is
> era-invariant)" is SUPERSEDED FOR THE UI FRAME by the Director's instruction of 2026-10-02, recorded
> in `docs/adr/adr-033-m5-integration-pass.md` D8. The interface's panels, chrome, controls and UI
> typography now take a visual era derived by one pure function from the player's authoritative Age
> (`UiEra = f(AgeQuery.CurrentAge(...))`, never stored in the simulation), on one token system that evolves
> across the nine Ages around the same layout regions, navigation, interaction rules and semantic colours
> (`docs/architecture/era-ui.md`). The superseded line stays in §6 as written, per the S8 §5 audit-trail
> convention; nothing above this note was edited.
>
> **What still holds.** The parchment substrate of §1–§2 is unchanged and remains the A6-centred material:
> the UI's Medieval era (A6) is this document's palette exactly (paper light, mid and shade; ink primary
> and soft; iron-red, gold-leaf, verdigris), the centre of the continuum rather than its only point. The
> map substrate — parchment, washes, coastlines, hydrography — is not re-themed by this change; UI
> furniture stays lit flat in every era (§1, two-layer rule); the single-cartographer rule for generated
> art and §7 are untouched. D-002 (ImGui is the game UI) and D-038 (the map shows what IS) are unaffected.

---

## 8. THE OBJECT LAYER — CONVENTIONS (M6 E1, 2026-10-28; appended — nothing above is edited)

> Authority: D-038 Parts B (two-layer lighting), C (anatomy fence), D1 (procedural first), G (acceptance), H (composed
> settlements; H8 answered by D-066 A4); D-043 D2 (count staging); D-052 §6–§7 (unit art, one token per type);
> D-066 A3 (register driven by realized state) and A4 (bounded parts, overflow fold). Code: `Sim.Ui/World/ObjectLight.cs`,
> `PartManifest.cs`, `WorldLens.Parts.cs`, `StyleRegister.cs`, `TerrainMarks.cs`, `UnitSilhouette.cs`; inks in
> `MapInk.cs`; data in `Sim.Ui/UiContent/world/settlement-classes.json` (`parts`). Decisions: `docs/m6-plan/e1-object-layer-note.md`.
> The look is the Director's call at the Part G gate (`docs/m6-plan/e1-visual-acceptance-checklist.md`); nothing here
> claims that gate.

**8.1 The one light (D-038 B1/B2, H4).** Stated ONCE, as constants (`ObjectLight`), never varied per asset:
- the light comes from the **north-west** (`FromDeg` 315); every object's **contact shadow** is its own silhouette
  translated along the screen direction **(2, 1)/√5** (east-south-east, flattened onto the paper) by **0.16 × its height**,
  in `MapInk.ObjectShadow` (#2C2318, the bible's ink deepened) at **32 %** opacity;
- every face turned to the light is its base moved **38 %** toward Paper light #EFE3C8 (`MapInk.Lit`); every face turned away,
  **30 %** toward Ink primary #3A2E1F (`MapInk.Shade`) — the §2 "object-layer shading stops", derived, never a new hue;
- one ground plane: objects stand on the paper (y down); no cast shadow beyond the contact shadow, no ambient occlusion;
- an object under **3 px** tall is drawn flat (no shadow, no faces): one threshold for every object;
- ONE shared painter casts shadows (`ObjectPaint.Solid`); a shadow is always followed by its caster. The single-light test
  (`ObjectLayerTests.EveryShadowOffset_IsTheOneLightsConstant_AppliedToItsCaster`) proves every shadow in the layer is its
  caster translated by this constant. A part lit any other way is rejected (B2), not accepted as a near-miss.

**8.2 Production and geometry (D-038 D1).** Every object is PROCEDURAL, in code, from the style bible's palette — no
external art asset, no generated image. Every polygon is CONVEX (the ImGui backend fills convex polygons only; pinned by
`EveryObjectPolygon_IsConvex`). The parts manifest (`PartManifest.Entries`) lists every part: dwelling, the three
structure forms, the institution halls (by university key, by a word of the type's content name — a law court draws the
scales — or the generic hall), the scaffold, the overflow part, relief (hill, mountain, peak), forest, field, and the
formation silhouette. A new institution type draws a hall the day its content exists.

**8.3 Composed settlements and count staging (D-038 H4; D-043 D2).** A settlement is composited at draw time from the parts
its state holds, each on its fixed lot (P12a's lattice, unchanged): one part per structure KIND and per institution TYPE,
aggregated. A part of count c is drawn at the stage of the last threshold c reaches — **1 small (one building), 10
expanded (a building and a wing), 20 campus (three about a court), 30 complex (five)** — thresholds data-defined
(`settlement-classes.json parts.countStages`). At **high zoom** (the Settlement band, the part's glyph at least 14 px) a part
counted above one shows its NUMBER on a small paper tag; the key beside the footprint names every count at the Settlement
band. A part UNDER CONSTRUCTION (a queued project, or a founding project built and not yet founded) is a scaffold on the lot
its finished part will take — never the finished part.

**8.4 The parts limit and the overflow rule (D-066 A4; D-038 H8).** One settlement's composite draws at most
**`maxParts` = 8** parts (`settlement-classes.json`). A settlement holding more kinds draws its **7 most significant** —
institutions before structures, then the larger count, then the lower key (an integer composite key; tie-dense pinned) —
and folds the rest into **ONE overflow part** (three small blocks) on a lot of its own, claimed after every kind so no
part moves for it; at high zoom its number is **"+N", the count of kinds folded**; its map marker carries the folded
total. 8 = every kind the shipped content can found at one settlement today (2 structure kinds, 5 university types) plus
one, inside the lattice's 14 glyph lots — so with today's content nothing folds; the rule is there for the content to come.

**8.5 The style register (D-066 A3; Law 4).** Built material follows REALIZED state only — what the settlement has built
and the best road class ACHIEVED on a route ending at it — in four rungs: **earthen** (round huts, thatch) → **timber**
(any structure, or a trackway) → **masonry** (a founded institution, 10 structures, or a built or paved road) →
**engineered** (20 institutions, or a macadam road or highway). Monotone: building more never lowers it. The rung chooses
dwelling roofs and walls and structure roofs (`MapInk.RegisterWalls` / `RegisterRoofs`, bible hexes). **The Age banner is
the only Age-keyed element of the map**: the Age, the date and the knowledge mask choose nothing physical (pinned by
`TheAge_ChangesNothingOnTheMapButTheBanner`; the era's ink and HAND of P13 — stroke weight, plate cut, banner fly — are
unchanged and change no geometry).

**8.6 Terrain objects (D-038 G2 "terrain has relief"; §4 items 8–9).** Relief (hills, mountains, peaks with a pale cap),
forests (tree crowns) and cultivated fields are OBJECTS the lens draws on the paper from the Regional band in; the
SUBSTRATE (bake, washes, coasts, rivers) is untouched and byte-identical in every era. Relief and forests re-draw what the
immutable worldgen rasters hold (decorative: they never suggest a resource or class the simulation lacks; the legend says
so), only on nodes the player knows, spaced at least 30 px apart on screen and at most 48 px a mark (closer in a cell holds
up to 3 × 3 marks), each seeded from its place so the marks read as a hand's, not a grid. Fields lie only on the fertile,
known catchment of a settlement whose cultivation is REALIZED (its economy farms, not forages), up to its farming share.

**8.7 Formations: silhouettes and one token per type (D-038 C1/C3; D-052 §6–§7).** A formation is drawn as a SILHOUETTE
keyed by its current identity — the formation as an object seen from above the paper (a rank block with its standard, a
spear hedge, a loose screen, a mounted wedge, an engine on wheels, a hull with sails, an aircraft's plan) — on the
owner's plate, under the one light, with the P11 form pips below. **Never a figure:** convex filled polygons only, at most
8 vertices each and 12 per token, no circle, curve or polyline, inside the plate (the C1 guard,
`TheSilhouette_IsConvexSolids_NeverAFigure_ForEveryIdentity_InEveryEra`). The formations of one owner, one place and one
identity are **one token** whose **number above it counts the formations — never people** (a formation holds no headcount on
the map); selecting it names the composition on the card.

**8.8 Frame cost.** The object layer's stated budget is every frame-cost row within **2.0 ×** its pre-E1 baseline (the
canonical sweep at 300 and 800 settlements). MEASURED at E1: **not met in 20 of 48 rows** — all of them near-empty-map rows
(base 0.14–1.3 ms); the most expensive row of every world is unchanged within noise. The terrain marks are bounded per frame
(at most ~1,150 places in any viewport). Numbers and the options: `docs/m6-plan/e1-object-layer-note.md` §2.
