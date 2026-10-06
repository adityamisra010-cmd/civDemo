# Era UI — the interface follows the player's Age (ADR-033 D8)

Branch `m5i-u1-era-theme`, cut from `m5-integration` @ `ffbb6f8`. UI only: `Sim.Ui/**`, `Sim.Ui.Tests/**`,
`assets/fonts/` (one added OFL face) and `docs/`. The simulation is not changed and nothing here is stored in it.

The interface has one visual language that evolves across the nine Ages: the same layout regions, navigation,
interaction rules and semantic colours, drawn in the material, edges, ornament, type, density and controls of the
civilization's current Age. Research changes WHAT is shown (actions, nodes, icons); the Age changes HOW it is
shown. There is no theme change per researched node.

## 1. Derivation

```
UiEra era = UiEras.Of(world, cfg.Ages, player)   // = UiEras.FromAge(AgeQuery.CurrentAge(world, cfg.Ages, player))
EraTheme theme = EraThemes.For(era)              // pure, memoised, deterministic
```

- `UiEras.Of` is the only derivation. No Age content (or an Age outside 1..9) clamps: none → `Prehistoric`.
- The era is never written to `WorldState`, a snapshot or the order log. Because it is a function of the
  authoritative Age, a save/load round trip at any Age yields the byte-identical theme
  (`EraThemeTests.SaveLoad_PreservesTheAge_AndThereforeTheIdenticalTheme`).
- `SimUiGame` derives it in `LoadContent` and again after every `EndTurn`; when the era changes it starts the
  cross-fade (§6). The live renderer and the headless SVG previews call the same function.
- Jitter (rough edges, texture placement) is seeded from stable ids through `FrameNoise` (a SplitMix64
  finaliser), never from time or `Random`.

## 2. The token system — `Sim.Ui/Theme/EraTheme.cs`

| Tokens | What they carry |
|---|---|
| `MaterialTokens` | The material (`Stone` … `Glass`): field, panel, raised, sunken, the dark frame material (`Chrome`), borders, hairline, accent pair, and the texture (grain colour, density, alpha, size). |
| `InkTokens` | Text, soft and dim text, rules, ink on the accent, and the inks used on the dark frame bars (`OnChrome*`). |
| `SemanticTokens` | Available, active, completed, locked, progress, danger, positive, food, knowledge, military, infrastructure, prerequisite, dependent, their card fills, and the eight lane hues. |
| `EdgeTokens` | Roughness and jitter, corner style (`Organic`, `Rounded`, `Chamfered`, `Square`, `Fine`), border width, double rule, fasteners. |
| `OrnamentTokens` | Level 0–4 and motif (`Weave`, `Chevron`, `Rivet`, `Meander`, `Illumination`, `Fleuron`, `Dimension`, `Hairline`). |
| `TypographyTokens` | A `TextStyle` (face, weight, tracking, case) per `FontRole` (body, heading, title, numeric, caps), size scale, line height. |
| `DensityTokens` | Density level, padding, gap, and `CardDetail` 1–5 (how much a research card shows). |
| `ControlTokens` | Granularity, progress style (`Notches`, `Segments`, `Bar`, `GraduatedBar`), segment count, stroke, grab size. |
| `IconTokens` | Icon style (`Daubed` … `Precise`), stroke, wobble, filled. |
| `ChartTokens` | Sophistication 0–4 (tallies → graduated grids), grid lines, ticks, labels, line width. |
| (map ink) | Not a theme token group: the world layer's one ink record is `Sim.Ui.World.MapInk`, derived from the theme by `MapInk.For(theme)` (see §7). |

`EraTheme.Canonical()` is a full `path=value` dump (doubles round-trip, colours as hex); the determinism tests
compare it, and the preview log records its hash per era.

## 3. The nine eras — `Sim.Ui/Theme/EraThemes.cs`

| Age | Theme (medium) | Edge | Ornament | Type | Detail · progress · icons · charts |
|---|---|---|---|---|---|
| A1 | Stone and ochre (charcoal and ochre on stone) | organic chipped slab, rough | none | Garamond, bold headings | 1 · tally notches · daubed · pebble tallies |
| A2 | Clay and reed (incised clay and woven reed) | rounded tablet | woven band | Garamond | 2 · clay counters · incised · bars |
| A3 | Cast bronze (cast bronze on sandstone) | chamfered plaque, double rule, studs | chevrons | Garamond, capital titles | 3 · 5 segments · cast · bars |
| A4 | Forged iron (forged iron on lime plaster) | square, heavy border, rivets | rivets | Garamond, capital titles | 3 · 8 segments · forged · bars on an axis |
| A5 | Marble and gilt | square, fine double rule | meander | inscriptional capitals | 4 · 10 segments · carved · bars on an axis |
| A6 | Vellum and gold leaf (the style bible's palette) | square, double rule | illuminated corners, decorated initials | Garamond | 4 · bar · illuminated · line chart |
| A7 | Print on laid paper | square, printer's double rule | fleurons | print Garamond | 5 · bar · engraved · ruled line chart |
| A8 | Engineering drawing (drafting film) | square, bolts | dimension ticks | Plex Serif body, Plex Sans heads, tracked caps | 5 · graduated bar · technical · gridded area chart |
| A9 | Glass and graphite | fine hairline, small radius | hairline accent | IBM Plex Sans | 5 · graduated bar (20) · precise · gridded area chart |

What the research card adds with `CardDetail`: every era shows the state (fill, inner rule, mark), name, cost,
the Age numeral and full name, retained progress and the cross-lane label; prerequisites, lock reasons and
Eurekas are always on hover and in the detail panel. A2 adds Eureka pips, A3 the prerequisite/dependent stubs and
the lane segments' completion share, A5 discounts and the lanes' done/total counts, A7 the target's
estimate to complete.

## 4. Continuity — what never changes

1. **Geometry.** Layout, card placement, the canvas, every hit rect, and the ImGui window, frame and item
   geometry are the same in every era. Painters place content at fixed offsets that clear every era's frame and
   ornament band; nothing measured in an era's type moves a region. Pinned by
   `EraSurfaceTests.Continuity_LayoutAndHitRegionsAreIdenticalInEveryEra` and
   `EraThemeTests.Continuity_TheImGuiGeometry_IsEraInvariant`.
2. **One polarity.** Every era is dark ink on a light record surface, with darker frame-material bars; ink
   contrast floors hold in every era (`Continuity_OneGroundPolarity_AndLegibleInkInEveryEra`).
3. **Semantic hue families.** Every semantic token is generated from a fixed hue in `SemanticFamilies`; an era
   only changes the pigment (saturation, lightness). Available is blue, active teal, completed gold, progress and
   prerequisite orange, dependent blue, danger red, locked neutral — in every era.
4. **Marks keep their meaning.** Completed is a check, active a ring with its progress arc, available a dot,
   locked a padlock, in the era's hand (`EraMarks`).
5. **Navigation and text.** The same sections, tabs, lenses and controls exist in every era; control and
   navigation labels shrink to fit rather than lose a word (pinned for the research screen's navigation in
   every era). Case is applied when text is set; a `TextCmd` always carries the text the screen meant. Every
   card names its Age in full, on the card, in every era.

## 5. Painting — one vocabulary for the game and the previews

- `PanelFrame.Paint(DrawList, rect, theme, id[, kind, fill, edge, weight])` paints a frame of `FrameKind`
  (`Panel`, `Card`, `Chip`, `Button`, `Bar`, `Modal`, `Toast`) that never leaves its rect; `Outline`, `Field`
  and `Rule` give the outline, the ground texture and the era's rule.
- `EraMarks` (state marks, progress, close, arrow), `ThemeText` (`Write`, `Title`, `Width`, `Fit`, `FitSize`,
  `Wrap`) and `ChromeFurniture` (the frame behind each ImGui chrome window).
- The live renderer replays the same `DrawList` through `DrawListImGuiBackend` (behind the ImGui windows) and
  the previews through `SvgWriter`, so headless rendering runs the same visual-state code as the game.
- `UiTheme.StyleFor(EraTheme)` is a pure mapping onto ImGui colours and sizes; `UiTheme.Apply` writes it.
  Window backgrounds are transparent because the painted frame is the surface: every chrome window
  (`BeginChrome`) paints its `ChromeFurniture` first, and a new window must do the same (popups and
  tooltips keep an opaque `PopupBg`). Fonts: EB Garamond,
  IBM Plex Serif and IBM Plex Sans (OFL, `assets/fonts/`), all baked into the atlas before upload with
  Latin-1 glyph ranges. Weights of 600 and above render bold (double strike in the game, font weight in the
  SVG); the themes use 400 for running text because the atlas holds the regular instances.
- **The text boundary.** `DrawList.Substitutions` is the one table of typographic stand-ins (em dash, en dash,
  ellipsis, arrows, curly quotes, …). The SVG path applies it to the string; the ImGui path applies it in the
  font atlas (`AddRemapChar` for single-character stand-ins, real glyphs for the multi-character ones), so an
  em dash that reaches `ImGui.TextUnformatted` from any string renders as the hyphen instead of `?`.

## 6. The Age transition

When `EndTurn` moves the player into a new Age, the derived theme changes and `EraTransition` cross-fades the
tokens over 1.6 s of UI time (colours mix, measures interpolate, discrete tokens switch at the midpoint); the
simulation never sees it. The transition panel reads "Your civilization has entered the {full Age name}", with
the surge and modernization line and "Records are now kept in {medium}." No cinematic. Headless renders show
the end state.

## 7. Convergence

- `ParchmentPalette` keeps the bible's pinned colours; the A6 theme is that palette exactly
  (`ParchmentEra_IsTheStyleBiblePalette`). Parchment is one point on the continuum, its centre.
- `ProgressionPalette` keeps only `BranchOf(theme, …)` and `StateLabel`; the research screens take every colour
  from the theme (a caller that used its colour constants now reads `theme.Semantic` / `theme.Material`).
- `UiTheme`'s panel colours come from the theme.
- ONE MAP-INK SOURCE (stream U2a convergence). The theme's former `MapInkTokens` and the world layer's
  `MapInk` described the same thing; `MapInk` is now the single complete record the world layer consumes and
  `MapInkTokens` is deleted (with its dead palisade/hut/house inks). `MapInk.For(theme)` derives it: identity
  inks (polities, sectors, universities and institutions, structures and dwellings, road-class fills, the
  selection ring, the capital mark) are constant in every era; the neutral inks follow the era by exactly its
  departure from the parchment — the line-work ink, the darkest glyph ink and the engineered-road kerb move
  with the era's text ink, the path casing, name plate and settlement halo (the map's legend paper) with its
  record surface — so `MapInk.For(A6) == MapInk.Default`, today's map. `SimUiGame` passes it to
  `WorldLens.Paint` (it fades with the theme during an Age transition), and so do the era and Age previews
  (`MapInkEraTests`).

## 8. Adding an era-specific treatment

1. Express it as a token. If an existing token can carry it (a colour, a motif, a progress style, a
   `CardDetail` level), set it in that era's builder in `EraThemes.cs` — values there are TUNE.
2. If it needs a new token, add the field to the record in `EraTheme.cs`, give every era a value, add it to
   `ThemeLerp.Between`, and let `Canonical()` pick it up (it reflects over the records).
3. Paint it in the shared painters (`PanelFrame`, `EraMarks`, `ThemeText`), switched on the token, never on
   the era number, and seed any irregularity from the caller's id through `FrameNoise`.
4. Keep the continuity rules: stay inside the rect, do not move content or hit rects, keep semantic colours in
   their family, never rewrite text.
5. Pin it: a test that the era's surface shows the treatment and the other eras' surfaces are unchanged in
   geometry, then run `render-previews.sh` and look at the result.

## 9. Previews

`docs/architecture/era-ui-preview/render-previews.sh` runs `sim-ui --era-preview`: the research preview's
stepped seed-42 world (turn 87) with only the player's Age row (and one transition row) changed, so the nine
eras are derived exactly as the game derives them. For each Age it paints the Technology tree as it opens, the
capital Age panel, and a chrome sample (the HUD panel frames, the docked Age panel, the command bar, and a mock
action list and chart labelled as illustrative), screenshots them with headless Chromium, and composes
`contact-sheet.png` (one row per Age). `preview-log.txt` records each SVG's SHA-256 and theme hash; two runs
are byte-identical (`EraSurfaceTests.Previews_AreByteIdenticalAcrossRuns_AndEachEraIsDerivedFromTheWorld`). The
SVGs link the fonts by absolute path, so the logged SVG hashes reproduce on the same checkout path; the theme
hashes reproduce anywhere.

The previews measure text with `ApproxTextMeasure`, whose styled widths are fitted to Chromium's advances for
the three faces and Garamond's weights; the game measures with the real fonts.

## 9a. Readability floors (M5 polish UR-1/UR-2, 2026-10-06)

> **DATED NOTE 2026-10-06 (Director directive 2026-10-06 §2: later eras must not shrink type; gameplay information
> beats decorative texture).** The era theme keeps its evolving typography (ADR-033 D8: face, weight, tracking and
> case still change from A1 to A9) but **no era sets type smaller than the reference**: `TypographyTokens.SizeScale`
> is floored at 1.0 (`EraThemes.Readable`; A7–A9 were 0.98 / 0.95 / 0.94, and `EraThemeTests` pinned that shrink —
> the pins now assert the floor instead). Text sizes are set by ROLE (`Sim.Ui/Theme/TypeScale.cs`: Display, Title,
> Heading, Body, Data, KPI, Secondary, Caption) per face by x-height (Garamond Body 20 px = Plex Body 16 px, both an
> 8 px x-height), × the UI scale (`UiScale`: `clamp(snap⅛(max(H/1080, dpi)), 1, 2) × user step`), with Caption as the
> floor. The atlas holds every face at a ladder of sizes and a run is drawn from the smallest raster at least its size
> (`UiTheme.Fonts.Face`), never a minified one. Text inks: the semantic colours are pigments; words set in a semantic
> family use its TEXT ink (`EraTheme.TextInk`, darkened toward the body ink to ≥ 5.5:1 on the panel); `TextDim` is
> floored at 5.0:1; the Available and Locked card fills are ≥ 1.4:1 apart. The ImGui frame height is era-invariant
> (30 px × s): the Plex eras' smaller body px is padded to it (`UiTheme.FramePaddingY`). Decoration: see the style
> bible §4 item 2 amendment (fibre overlay at 35 % over the interface) and `PanelFrame` (texture in the frame band,
> calm under the words).

## 10. Limits

- The live GPU path (ImGui style, the frames behind the windows, the atlas with remaps, double-strike bold and
  per-glyph tracking) could not be shown in this container: there is no GL context. The atlas itself is built
  headlessly and tested (`ImGuiTextBoundaryTests`); the drawing is the same `DrawList` the previews show.
- `docs/architecture/research-tree-ui/` and `docs/architecture/age-and-world-ui/` predate the era theme. Their
  scripts now derive the theme from their world, so re-running them renders the current look.
