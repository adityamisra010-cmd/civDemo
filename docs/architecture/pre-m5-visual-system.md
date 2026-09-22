# PRE-M5 VISUAL SYSTEM — A PROCEDURAL GRAMMAR FOR `base · domain · era · maturity · state`

**Design foundation, not a packet. Nothing here amends a ratified visual ruling.** This document
establishes the reusable primitives and compositional rules the Director asked for in Phase 2 of
the pre-M5 reconnaissance, against the tree at `main` = `de5e00e` (M4 CLOSED, tag `m4-exit`).
Companion: `docs/architecture/pre-m5-repository-audit.md` (Phase 1). Governing visual rulings it
builds under, in force and unamended: `docs/d038-visual-target.md` (D-038 Parts A–H),
`docs/style-bible-parchment.md` (§1 two-layer lighting, §2 palette, §7 anatomy fence),
`docs/adr/adr-009-ui-stack.md` (render outside the determinism surface), D-011 §4 (formation tokens).

**What was implemented alongside this document, and how to remove it** — see §9. It is one folder,
one test file, one headless flag and one PNG; no simulation file is touched and nothing in the map
draw path consumes it.

---

## §0 THE THREE RULES EVERYTHING BELOW OBEYS

1. **A glyph is a READ.** Every image the grammar produces is a pure function of a `GlyphSpec`
   record. A view-model builds the spec from `IReadOnlyWorldState` (and, later, from observation
   records); the grammar never sees the world and nothing in `Sim.Core` can see the grammar
   (ADR-009: *"nothing references Sim.Ui"*). D-038 H5 states the same for sprites — *"the sprite is
   a READ of state — it never becomes a source of truth"* — and it binds glyphs identically.
2. **No mechanic exists to make a picture possible.** If a state cannot be read from the tree or an
   observation record, the glyph does not show it. A tooltip prints "not recorded" before it prints
   a number the simulation did not produce (`docs/observability-architecture.md` §0: the five
   permitted field kinds — READ, SUMMED, DIFFERENCED, RESIDUAL, RECOMPUTED — and *"nothing else is
   permitted"*).
3. **Era is a style register, never a gate.** The `Era` axis selects a rendering vocabulary. Its
   value is handed to the grammar by the view-model from **computed state** — a structure's own
   kind, a tier, or (when M7's *"computed era labels"* exist, Spine `:110`) a label that is
   *"descriptive output only"* (Spine `:22`, CLAUDE.md law 4). No glyph, atlas or view-model in this
   design reads `SimClock`, a year, or a turn number to choose a vocabulary. The audit's concept
   C02–C06 findings explain why this matters for the whole Age proposal.

---

## §1 WHAT EXISTS TODAY — the honest inventory (READ from source unless marked)

| area | what the tree has | where |
| --- | --- | --- |
| Stack | MonoGame DesktopGL 3.8.5 + ImGui.NET 1.91.6.1; `Sim.Ui` referenced by nothing but `Sim.Ui.Tests` | `Sim.Ui/Sim.Ui.csproj:25-26`, `docs/adr/adr-009-ui-stack.md` |
| Map draw path | terrain texture (SpriteBatch) → world-space vector layers (catchment fill, paths, rivers) → **settlement markers, world-anchored at CONSTANT SCREEN SIZE** (20 px, `SettlementSelection.MarkerScreenPx`) with a gold halo on selection → ImGui HUD → grain overlay multiplied over everything | `Sim.Ui/SimUiGame.cs:547-616` |
| Sprite/icon system | **one** icon: the generic settlement marker (`AssetKind.SettlementMarker`, an inked ring). No icon tiers, no per-kind glyphs, no atlas. | `Sim.Ui/Art/AssetManifest.cs:16-20`, `PlaceholderArt.cs:245-263` |
| Procedural baking | **exists and is the precedent**: `HeaderRuleBaker` (analytic coverage → palette-exact RGB + coverage alpha, seamless by construction, 143 lines), `PlaceholderArt` (periodic value-noise paper, washes, panel, compass rose, marker), `TerrainBaker` (byte-deterministic per seed). All pure: `ArtImage(Width, Height, byte[] Rgba)`, no MonoGame types. | `Sim.Ui/Art/HeaderRuleBaker.cs`, `PlaceholderArt.cs`, `PngCodec.cs:7` |
| Palette | `ParchmentPalette` — the closed set `BibleColors` (18 hexes), saturation gamut helpers, territory ink-wash compositor | `Sim.Ui/Art/ParchmentPalette.cs` |
| Theme / typography | `UiTheme`: EB Garamond (labels/headers), IBM Plex Serif (numbers); named metrics (`BodyFontPx` 19, `FrameHeightPx` 29, paddings) shared by renderer and headless geometry tests | `Sim.Ui/Art/UiTheme.cs:36-48` |
| Animation | **none.** `GameTime` is used for camera panning (`:498`) and an FPS smoother (`:549-550`). No tween, no per-frame visual state, no transition of any drawn element. | `Sim.Ui/SimUiGame.cs:439-545`, `:547-550` |
| Hover / tooltip | **none.** Zero calls to `SetTooltip`/`BeginTooltip`/`IsItemHovered`/`SetItemTooltip` in `Sim.Ui` (MEASURED: grep, 0 hits). Selection is a pure hit-test model over marker discs and measured label rects (`SettlementSelection.HitTest`, 44 px minimum target). | `Sim.Ui/ViewModel/SettlementSelection.cs:28-64` |
| Information hierarchy | exists for PANELS, not for glyphs: status band → selection card → command bar → one context panel (T4.19 lane B); inspector tabs route to explain queries via `ExplainRouting` | `docs/m4-player-information-ui.md` §1, `Sim.Ui/ViewModel/ExplainRouting.cs` |
| Headless entry points | `--audit-assets`, `--generate-placeholder-assets` run without a window and exit — the precedent for a headless contact-sheet export | `Sim.Ui/Program.cs:12-40` |
| Tests | headless view-model tests only; procedural assets are pinned on four properties — VISIBILITY floor, SEAMLESS by byte equality, PALETTE-EXACT per visible pixel, DETERMINISTIC bake — with red proofs against mutants of the generator | `Sim.Ui.Tests/HeaderRuleBakerTests.cs`, `ArtSubstrateTests.cs` |
| Assets | 25 files: terrain 9, ui 5, fonts 5, parchment 4, ink 1. No sprite sheets. | `assets/` |
| Ratified visual target | D-038: illuminated atlas; two-layer lighting (substrate flat forever, object layer one light + contact shadow, *"stated once as a constant"*); anatomy fence (no figures — silhouettes/tokens); production order procedural → parametric render → image generation; Part H: settlements are **composed sprites assembled at draw time from parts**, and *"glyphs carry what is HAPPENING to a settlement, the sprite carries what it IS"* (H6) | `docs/d038-visual-target.md` B1, C1–C3, D1, H4–H6 |
| The deferral | Symbology (icon tiers, trade/route language, army markers, unrest, legend) is DEFERRED to the inserted visual milestone after M5, before M6; D-038 F3/H7: *"does not authorise any asset before that milestone"* | `docs/queue.md:96-110`, `docs/d038-visual-target.md` E1, F3, H7 |

**Two consequences fall straight out of this table.** First, the grammar's raster back-end is not
new engineering — it is `HeaderRuleBaker`'s technique (analytic coverage, palette-exact ink,
supersampled alpha) generalised from one ornament to a family of devices, and it inherits that
file's four test properties unchanged. Second, hover and animation are not "extended" by this
design; they are **introduced**, so their conventions can be set cleanly rather than retrofitted.

**A governance note the Director should see before reading further.** D-038 F3 and H7 say no
asset work before the inserted visual milestone; the symbology deferral's reason is that M4/M5
change what the map contains and early art gets redrawn. This document and the code beside it are
**infrastructure — primitives and composition rules — not object-tier assets**: D-038 D1 itself
names glyphs as *"procedural, in code"*, and a code grammar is regenerable by definition (a palette
change regenerates every glyph — D1's own requirement). The Director's Phase 2 instruction
explicitly asks for this foundation. Recorded so that, if the Director reads it as inside F3's
fence, §9 shows the one-folder removal; the audit lists it as conflict **X-VIS-1** for an
append-only note on D-038 when the architecture is ratified.

---

## §2 THE GRAMMAR — seven axes, one composition order

```
GlyphSpec = Base × Domain × Era × Maturity × State × Placement × SizeClass
```

Every axis is a small closed enumeration or a bounded scalar; the product space is generated, never
authored. **The asset count is the number of PRIMITIVES (≈ 8 bases + ≈ 8 domain marks + 5 era
registers + 1 state alphabet), not the number of combinations.** That is the whole answer to "do
not create hundreds of bespoke assets": adding a domain costs one mark; adding a base costs one
silhouette; nothing is re-authored.

### 2.1 Base — the silhouette family (WHAT KIND of thing)

The base must read alone at 16 px. Eight families, each an analytic silhouette in the unit box:

| base | silhouette | used for |
| --- | --- | --- |
| `Hall` | wide gable (pentagon, 5:3) | ordinary buildings, granary, workshop |
| `Tower` | tall narrow rectangle with a cap | watchtower, keep, chimney-class industry (with `Industrial` register) |
| `Ring` | annulus | walls, enclosure, a settlement's fortification |
| `Dome` | half-disc on a plinth | temple, shrine, observatory |
| `Portico` | plinth + 3 columns + pediment | **institutions** (university, court, academy) — a capability, never a map object (D-038 H2) |
| `Formation` | flat rectangle "bar" containing a cluster of small marks | **units** — the token of D-011 §4 / D-038 C3; strength = mark count, thinning as it drops. **No figures, ever** (§7 anatomy fence). |
| `Node` | circle | progression / research nodes |
| `Lozenge` | diamond (the `HeaderRuleBaker` lozenge, reused) | milestones and Age cells |
| `Heap` | three overlapping discs on a baseline | resources / goods stocks |
| `Link` | two small nodes joined by a stroke | infrastructure edges (road, bridge, canal, sea lane — the stroke style is the domain mark's job) |

That is ten; `Heap` and `Link` are cheap and the task lists resources and infrastructure by name,
so they are in. Silhouettes are **ink outline + optional wash fill**; the fill is what `Maturity`
drives (§2.4).

### 2.2 Domain — the mark (WHAT FIELD it belongs to)

A small ink device drawn in the **domain slot**: the upper-right quadrant of the glyph box for
`Hall/Tower/Portico/Node`, the centre for `Lozenge/Heap`, and the stroke itself for `Link`. The
mark contract: fits a k×k box (k = ¼ of the glyph size), ink only, at least one fully-opaque pixel
at 24 px, and **suppressed below 24 px** (§2.7). First alphabet — a VISUAL alphabet whose binding
to simulation domains is the knowledge packet's decision, not this document's:

`Agriculture` (three-spike sheaf) · `Craft` (six-tooth cog) · `Military` (chevron/spearhead) ·
`Maritime` (two wave crests) · `Letters` (open book: two rectangles) · `Medicine` (plain Greek cross
in INK — never red: the palette forbids it and the red-cross emblem is protected) · `Commerce`
(balance: a T with two pans) · `Civic` (single column).

For `Link`, the "mark" is the stroke vocabulary: road = double hairline; bridge = arc over the
stroke; canal = stroke with hatch ticks; sea lane = dashed stroke in `River` blue.

### 2.3 Era — the style register (HOW it is drawn), never WHEN

Five registers, each a rendering vocabulary applied to the base outline: `Primitive` (rounded
corners, thatch hatch on the roof plane, line weight 1.0), `Classical` (straight lines, columns
where the base has a facade), `Medieval` (crenellation on `Ring/Tower`, pointed cap), `Industrial`
(sawtooth roofline on `Hall`, a stack on `Tower`, line weight 1.25), `Modern` (flat roof, grid
hatch, line weight 1.5). **The register is an input.** Where it comes from, in order of what the
tree can supply:

- today: nothing — every structure kind in `Sim.Data` is register-less, so the view-model passes
  `Primitive` for all and the axis is inert (this is correct: the tree has no era vocabulary and
  the grammar must not invent one from the date);
- when a structure TYPE carries a register in its data (a `Factory` type is `Industrial` because
  of what it IS), from that type — computed state;
- when M7's computed era labels exist, from the label — output-only, per law 4.

The register is **suppressed below 32 px** (§2.7); at map zoom a hall is a hall.

### 2.4 Maturity — the wash fraction (HOW DEVELOPED)

`Maturity ∈ [0, 1]`, quantised to the four names the task uses — `Nascent` (0–¼), `Developing`
(¼–½), `Mature` (½–¾), `Eminent` (¾–1) — rendered as the base's wash **filling from the baseline
upward** by the fraction (a rising tide inside the outline), in `InkSoft` at 45 % alpha over the
paper. An `Eminent` glyph additionally gains a one-pixel `GoldLeaf` underline — the bible's "rare
emphasis" accent, used for nothing else in the grammar. Maturity is read from a stock the view-model
can name (a structure's `Progress`, an institution's own scalar when one exists); if no stock
exists, the view-model passes 1.0 and the axis is inert.

### 2.5 State — the indicator alphabet (WHAT IS HAPPENING TO IT)

One ring around the base and, for some states, an overlay. The alphabet is closed and each member
has a **distinct silhouette-level signature** so the states are distinguishable in greyscale at
16 px (this is tested, §8):

| state | ring | silhouette | overlay |
| --- | --- | --- | --- |
| `Locked` | dashed hairline | outline at 40 % ink alpha ("ghosted") | — |
| `Available` | solid hairline | outline, no wash | — |
| `InProgress` (researching / under construction) | solid hairline + **progress arc** (heavy, clockwise from 12 o'clock, fraction ∈ [0,1]) | outline + wash to the fraction | diagonal scaffold hatch for construction |
| `Unlocked` / `Complete` | solid heavy ring | outline + full wash | — |
| `Stalled` | solid hairline + arc frozen + two ink ticks at the arc head | as `InProgress` | — |
| `Decayed` | solid hairline with gaps | outline with gaps | — |

Veterancy is a badge, not a state: 0–3 chevrons beneath the `Formation` token (`Recruit` none,
`Regular` one, `Veteran` two, `Elite` three, the third in `GoldLeaf`). Age standing is a header
device, not a per-glyph state (§4).

### 2.6 Placement — panel or map (the light)

`Panel`: flat, no shadow — a printed device on the parchment plate, like the UI furniture (style
bible §1: the substrate layer and UI furniture are lit flat forever). `Map`: world-anchored at
constant screen size (the marker precedent, `SimUiGame.cs:583-606`) with a **contact shadow**
offset along the **one global light constant** (D-038 B1: *"stated once as a constant and never
varied per asset"*). That constant lives in the grammar module (`ObjectLight.Direction`) precisely
so the future parametric-render parts (Part H) bind to the same value; if they do not, Part H's
collage failure is the result. The shadow is `InkPrimary` at 25 % alpha, 1 px offset at 16 px
scaling with size class.

### 2.7 SizeClass — level of detail is ADDED with size, never lost to crowding

| size | drawn |
| --- | --- |
| 16 px | base silhouette + state ring (+ progress arc) only. **No domain mark, no register; the maturity wash is binary (full at ≥ ½, none below).** |
| 24 px | + domain mark |
| 32 px | + era register vocabulary + full maturity wash |
| 48 px | + badges (veterancy chevrons, `GoldLeaf` underline), + `Decayed` gaps |

This is the information-density rule the task asks for, stated as a contract rather than a taste:
**a glyph at size N is byte-identical to the same spec with the suppressed axes set to their
neutral values** — which is how the test proves the rule rather than eyeballing it.

### 2.8 Composition order (z, bottom to top)

`shadow (Map only) → ring background → maturity wash → base outline in the register's vocabulary →
domain mark → state overlay (hatch / progress arc / gaps) → badges`. Every layer is ink-on-
transparent; **RGB is palette-exact on every visible pixel and antialiasing lives only in alpha**
(the `HeaderRuleBaker` rule: *"intermediate RGB would bake a paper assumption into the ink"*).
Overlapping ink takes the max coverage, never a blend, so no pixel leaves the bible set.

---

## §3 PROGRESSION NODES AND EDGES — a display grammar for a producer that does not exist yet

A progression view (research tree, institution lattice, Age milestones) is `Node`/`Lozenge` glyphs
joined by edges. Edges use the same state alphabet: `Locked` = dashed hairline, `Available` = solid
hairline, `Unlocked` = heavy line; an edge takes the state of its target. A `Partially developed`
node is `InProgress` with the wash fraction. Layout is the view-model's (deterministic: a layered
DAG layout with a stable integer tie-break on node id — the same rule CLAUDE.md sets for every
ordering over doubles).

**Stated plainly:** the tree has no knowledge, research, institution or Age table (audit §1:
0 files for every such identifier in `Sim.Core`), the Spine places knowledge at M6→M7 after D-011
§6, and CR-005 is OPEN on where research lives. The node grammar therefore consumes a `NodeView`
record whose producer is future, ratified work. Nothing in this document or the code names a
technology, a prerequisite or a research cost.

---

## §4 AGE, MATURITY AND VETERANCY AS DISPLAYED STATE

- **Age (global and per-civilization).** A header device, not a per-glyph axis: `NEOLITHIC · MID`
  as three `Lozenge` cells (Early/Mid/Late) filled 1/2/3, drawn in the status band for the world
  and on the polity card for the player's civilization, with a small ink arrow when the two differ
  (behind ◂ / ahead ▸). **Both labels are computed output** (Spine `:22`, `:110`); the device draws
  what it is handed and the sim never consults it. Today no producer exists; the device is
  specified so that when M7's labels exist the HUD does not invent a second vocabulary.
- **Maturity** — §2.4, on the glyph.
- **Veterancy** — §2.5, chevrons under the formation token. Per-unit persistent state does not
  exist in the tree (`NotableRow` is the only per-person row and *"DELIBERATELY ABSENT: competence,
  traits, experience, and every battle field"*, `Sim.Core/State/WorldState.cs:630-633`); the badge
  is specified against D-011 §4's token, not against a stock.
- **Locked / Available / Researching / Unlocked** — §2.5, on the glyph and its edges.

---

## §5 ANIMATION CONVENTIONS — three kinds, nothing else

The tree has no animation (§1), so the conventions are set here rather than inherited.

**The principle.** Animation is a *transition between two authoritative states*, never a state of
its own. At every instant the target of every transition is the authoritative value; a transition
is interruptible and convergent; **nothing moves while the state it shows is unchanged.**
Presentation time is wall-clock (legal in `Sim.Ui`, ADR-009) and is never written anywhere.

| kind | when | form | bound |
| --- | --- | --- | --- |
| **State transition** | a glyph's `State`/`Maturity` changed between two observed turns | cross-fade of ring/wash, old → new | 250 ms |
| **Progress motion** | an `InProgress` fraction changed at a turn boundary | the arc sweeps from the old fraction to the new | 300 ms |
| **Attention pulse** | a change the observability layer already classifies as decision-relevant (nothing invented here; today: none exist, so today: none fire) | ONE ring pulse, 1.0 → 1.15 → 1.0 scale | 600 ms, never loops |

**Turn-boundary batching.** All transitions for a turn start on the same frame (the "turn tick"),
so the eye reads one coherent change per End Turn rather than a trickle. **Banned:** idle loops,
bobbing, particles, glow, any motion on a glyph whose spec did not change, any animation that
implies a sub-turn event the simulation did not produce (this is the visual form of *"do not invent
gameplay mechanics merely to make an animation possible"* — a sweeping arc between turn N and N+1
shows two authoritative values and the interpolation between them; it does not claim the world had
a state at N+½).

**Transitions when the clock varies.** The audit's central finding is that Draft 1 proposes a
variable strategic Δt. The conventions above are stated in wall-clock milliseconds per transition,
not per sim-year, so they are indifferent to dt; the only rule that touches the clock is that a
transition is keyed to a **turn boundary as the executor defines it**, whatever length that turn
had.

---

## §6 HOVER, INSPECTION AND THE TOOLTIP HIERARCHY

Three tiers, each a strictly larger read of the same state — never a different source:

| tier | trigger | content | source rule |
| --- | --- | --- | --- |
| 0 — glance | none | the glyph (§2) | `GlyphSpec` from state |
| 1 — hover | 250 ms dwell, no animation, no motion of the glyph | a four-line tooltip (below) | observability §0 kinds only |
| 2 — inspect | click | the existing inspector / context panel, routed by `ExplainRouting` to the explain queries (`GrievanceExplanation.For`, `CausalChain.ForNeed`, `Levers.For`, …) | the T4.19 contract, unchanged |

**The tooltip's four lines, in this order and never reordered:**

1. **Identity** — name · base kind · domain (e.g. *University · institution · Medicine*).
2. **State** — the state word and its fraction; an ETA **only if a projection observer produces
   one** (none exists — `docs/m5-temporal-control-and-player-agency-placeholder.md` §6 lists
   projection as a *"new M5 component, read-only"*; until then the line reads *"progress 0.42 —
   completion: not recorded"*).
3. **Cause** — the lever or predicate that drives the state, from `Levers.For` / `CausalChain`
   when the query exists for that entity; *"not recorded"* otherwise.
4. **Navigation** — *"click to inspect"* and the panel it opens.

Hard rule, inherited from `docs/observability-architecture.md` §0: every number on a tooltip is
READ / SUMMED / DIFFERENCED / RESIDUAL / RECOMPUTED (a public static sim function on stored state)
— **never an observer-side copy of a formula**. A tooltip that would need one prints GAP.

Hover on the **map** uses the existing hit-test model (`SettlementSelection.HitTest`, 44 px minimum
target): the hover target is the same disc as the click target, so what the player can hover is
exactly what they can select.

---

## §7 HOW THIS MEETS D-038 WITHOUT PRE-EMPTING THE VISUAL MILESTONE

- **Glyphs vs sprites (H6).** The grammar is the *"what is happening"* tier D-038 demotes glyphs to,
  plus the panel/progression tier D-002 governs. It does not author the composed settlement sprite
  (Part H, parametric render, the inserted milestone's job). The two must share three things, and
  the grammar exposes them as constants so the sprite packet can bind to them instead of inventing
  twins: the light constant (§2.6), the state alphabet (§2.5), and the palette (`ParchmentPalette`).
- **Parts and assembly (H4).** The grammar is H4's principle at glyph scale — primitives composed at
  bake time from what state says — and its size-LOD rule (§2.7) is an answer to H8's open
  legibility question *for glyphs*; H8 remains open for sprites.
- **Anatomy fence (C1–C3).** `Formation` is the only base that stands for people, and it is a bar
  of marks. There is no `Figure` base and the grammar has no way to draw one.
- **Two-layer lighting (B1).** Panel glyphs are flat; map glyphs carry the one light as a contact
  shadow. Nothing else in the grammar knows about light.
- **Production method (D1).** Procedural in code. Every glyph is regenerable from the palette.

---

## §8 THE TEST CONTRACT (what the code beside this document pins)

Inherited from `HeaderRuleBakerTests` and extended per axis. All headless, all against mutants of
the generator, none against a committed PNG:

1. **Palette-exact:** every visible pixel of every glyph in the contact sheet is one of
   `ParchmentPalette.BibleColors` — no blends.
2. **Deterministic:** two bakes of the same spec are byte-identical; the contact sheet is stable.
3. **Visible at 16 px:** every base at every state has ≥ 10 % of its box at alpha>127 and a peak
   pixel at alpha ≥ 200 (a 1 px hairline rarely covers a whole pixel, so "fully opaque" is the
   wrong bar at this size). MEASURED on the shipped grammar: Locked, the faintest state by
   design, sits at 14–21 %; every other state at 19–48 %; the peak is 239 everywhere.
4. **States are distinguishable:** every pair of states on the same base differs in ≥ 4 % of
   pixels at 16 px — the greyscale-distinguishability rule made numeric.
5. **LOD is a contract:** a 16 px glyph is byte-identical to the same spec with domain, register
   and badges reset to neutral (§2.7).
6. **Maturity is monotone:** ink coverage is non-decreasing in the wash fraction.
7. **Anatomy fence by construction:** the `Base` enumeration has no figure member and the
   `Formation` token's mark count never exceeds the strength-band table.
8. **Read-only by construction:** no public member of the grammar namespace takes or returns a
   type from the `Sim.Core` assembly (reflection test) — the grammar cannot read the world.
9. **The light is one constant:** the contact-shadow offset of every `Map` glyph is the same
   vector, equal to `ObjectLight.Direction`.

---

## §9 WHAT WAS IMPLEMENTED, AND HOW TO REMOVE IT

**Implemented (isolated, reversible; no simulation file touched; not consumed by the map draw
path):**

| item | path | remove by |
| --- | --- | --- |
| the grammar: spec records, coverage rasteriser, composer, contact sheet | `Sim.Ui/Art/Glyphs/` | `git rm -r Sim.Ui/Art/Glyphs` |
| the test contract (§8) | `Sim.Ui.Tests/GlyphGrammarTests.cs` | `git rm` |
| headless export `sim-ui --glyph-sheet [path]` (the `--audit-assets` precedent) | one guarded block in `Sim.Ui/Program.cs` | delete the block |
| the rendered sheet for review without a build | `docs/architecture/glyph-sheet-v0.png` | `git rm` |

Deleting those four things returns the tree to `de5e00e` behaviour exactly; nothing else
references them. `dotnet build`, `dotnet test`, and the three gate scripts were run after the
addition — results are in the audit's §9 and in the commit.

**Not implemented, deliberately:** no wiring into `SimUiGame.Draw`, no atlas upload, no hover or
tooltip code, no animation clock, no view-model that maps `WorldState` rows to specs. Those are
the symbology packet's work at the inserted milestone (D-038 E1) or, for progression/Age devices,
the packets that own their producers. The grammar exists so that when they arrive they compose from
one vocabulary rather than inventing three.

---

## §10 RECOMMENDATIONS (concise — mirrored in the audit's deliverable 4)

1. **Adopt the seven-axis grammar and the size-LOD contract** as the symbology packet's starting
   point; treat §2.1's ten bases and §2.2's eight marks as the initial alphabet, extended by data.
2. **Bind the future composed-sprite parts (D-038 Part H) to the grammar's light constant, state
   alphabet and palette**; do not let the sprite packet declare a second light or a second
   "under construction" treatment.
3. **Set the three animation kinds and the batching rule now**, before any animation exists, and
   make "nothing moves while state is unchanged" a test when the first transition ships.
4. **Introduce hover with the four-line tooltip hierarchy and the §0 field-kind rule**, reusing
   the selection hit-test as the hover target; no tooltip may print a number the sim did not
   produce.
5. **Keep era as a register fed by computed state.** Any Age display is a header device reading a
   computed label; the audit's C02–C06 rulings decide whether such a label may exist at all.
6. **Do not author object-tier assets** (settlement sprites, terrain relief, army tokens with
   depth) before the inserted visual milestone; D-038 F3/H7 stand. The grammar is the tier beneath
   them.
7. **Add one append-only note to D-038** when the pre-M5 architecture is ratified, recording that
   the glyph grammar (procedural infrastructure) was established ahead of the visual milestone by
   Director instruction and is not the object tier (audit conflict X-VIS-1).
