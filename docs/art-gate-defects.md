# Art-substrate visual gate — the three director-reported defects

**Recorded 2026-07-28, before any fix, at the director's instruction.** These three defects existed
only in the director's head until this file: the handoff status doc correctly reported that no
record of them existed in the tree and declined to reconstruct them. ADR-015 §7.12 — a
director-stated defect that lives only in conversation is a measurement nobody can check. This file
is the record; the statuses below are updated as the close-out packet lands.

| # | defect | status |
| --- | --- | --- |
| D-A1 | The header rule is mis-scaled: drawn with uv (0,0)–(1,1) and a hardcoded 8f height, so the native 64×8 asset is stretched across the whole panel width. The ornament smears and its ink weight changes with panel size. | **CLOSED** (director ruling, 2026-07-28): the rule is **PROCEDURAL** — `HeaderRuleBaker`, a double rule with a repeating lozenge, seamless by construction, palette-exact, coverage-antialiased, deterministic; the draw-path fix stays untouched. Evidence for going procedural below. Awaiting visual gate confirmation only. |
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

## D-A1 CLOSED — procedural (director ruling, 2026-07-28)

**Why the generated assets failed — measured, the evidence for this ruling:**

| asset | dimensions | alpha > 127 coverage | ink |
| --- | --- | --- | --- |
| `main` `assets/ui/header-rule.png` | 1536×1024 (1.50:1) | **0.46%** | neutral (post white-key) |
| `abbd0ca` re-drop | 1536×1024 (1.50:1) | **0.90%** | dominant **#A77032 — off-palette** |

Both are a small ornament floating on a ~99%-empty square field. At the pinned texel density of
128, each drawn pixel samples a 128×128 source region that is ~99% empty, so the rule renders as
nothing. The second drop doubled coverage and changed nothing that mattered.

**The ruling's reasoning, recorded:** a double rule with a repeating lozenge is a straightedge and
a stamp. Drawn in code it is seamless BY CONSTRUCTION, exactly on-palette, correct in aspect by
definition, and needs no alpha keying. The single-cartographer rule is strengthened, not waived —
a procedural rule at exactly #3A2E1F satisfies identical ink chemistry better than a generated one
can. Cartographers ruled their lines with instruments; this is the one element on the page that
was never freehand.

**What shipped:** `Sim.Ui/Art/HeaderRuleBaker.cs` (pure, headless, TerrainBaker idiom).
Derived geometry: nativeH = drawH 8 × supersample 4 = 32; repeat 256 screen px ⇒ nativeW = 1024;
at drawH 12/16 the repeat stretches to 384/512 px (period fixed in texture space). Measured
alpha>127 coverage: **34.6% whole texture, 64.9% within the rule's own band** — against 0.46%/0.90%
for the two generated attempts. Manifest: `ui/header-rule` REMOVED (a generated resource is not a
drop point); the seam test is replaced by wraparound byte-equality and the palette test by exact
two-ink equality, both stronger. `assets/ui/header-rule.png` (1.7 MB) deleted — with the manifest
entry gone it would be an orphan the audit flags, and it has no consumer. Five properties pinned,
four proven red against generator mutants (all-transparent → 2 red; wrap broken → seam red;
RGB blended → palette red; stateful → determinism red).

## D-A1 gate round 2 — the lozenge does not repeat (2026-07-28)

**Gate evidence (director's measurement):** at the 256 px screen period, a 705 px panel should
show lozenges at 128/384/640 (3) and a 1283 px panel at 128/384/640/896/1152 (5). Observed:
**1 and 1**, at the same absolute offset from the panel's left edge. The two rule LINES tiled
correctly across the full width in both — which proves nothing about wrap, because horizontally
uniform rows repeated under clamp are indistinguishable from tiling.

**Discriminating check — which case held, with code evidence:** the WIDER one. The vendored
`ImGuiRenderer.RenderDrawData` set blend, depth and rasterizer state but **never set
`SamplerStates[0]`** — ImGui geometry sampled under whatever the last SpriteBatch pass left on
the device, and the pass immediately before `DrawHud` is the settlement-marker batch,
`_spriteBatch.Begin(samplerState: SamplerState.LinearClamp, ...)` (SimUiGame.cs). So EVERY ImGui
draw ran under LinearClamp: the parchment panel background (`uv = size/128`,
`DrawPanelFurniture`) relied on clamp exactly as the header rule did — its ruled lines only
looked right for the same uniform-rows reason, and any panel taller than one tile was streaking
the bottom edge row. Not a header-rule draw-call fault.

**Fix chosen:** `ImGuiRenderer` gains `public static readonly SamplerState TextureSampler =
SamplerState.LinearWrap`, applied to `SamplerStates[0]` in `RenderDrawData` (saved and restored
like the other device state). Least invasive candidate that fixes background AND rule in one
place; matches the reference ImGui backends (imgui_impl_dx11 samples with ADDRESS_WRAP); safe
for the font atlas and 9-slice, whose uv stay inside [0,1]. The draw calls and
`PanelFurniture.HeaderRuleUv` are byte-identical — the ink-weight math (checks 1–2) is untouched.

**The missing composite test — now shipped:** every prior test certified a PIECE (generator
seamless, uv density exact); none certified the RESULT. Third occurrence of this shape on this
asset. `PanelFurniture.HeaderRuleVisibleDiamondCenters(nativeW, nativeH, drawW, drawH,
addressMode)` is the pure model of the composed draw — period derived from the very `HeaderRuleUv`
value the draw call issues, address mode mapped from the very `ImGuiRenderer.TextureSampler` the
renderer applies. `PanelFurnitureTests.ComposedDraw_LozengeRepeats_AtPredictedCenters` asserts
centers {128,384,640} at 705 px and {128,384,640,896,1152} at 1283 px; proven RED by reverting
the sampler to LinearClamp (model collapses to one center, test fails at both widths).
**Coverage statement:** what remains uncovered is the actual GPU sampling — that MonoGame honours
`SamplerStates[0]` for BasicEffect draws and that `RenderDrawData` really applies
`TextureSampler`; that hop is not inspectable headless and takes one eyeball on the gate build.

## D-A1 — CLOSED (director's gate, round 3, 2026-07-29)

All four checks PASS: (1) rule clearly visible on every panel; (2) ink and line weight hold at
~705 px and ~1283 px; (3) the lozenge repeats correctly — the LinearWrap fix confirmed at the
gate; (4) the hairline reads as a good subtle secondary line, no constant change.

**Director's check on the wider finding, recorded as the measurement of record:** the parchment
panel background did NOT fail before the fix — the clamp leak's visible effect was confined to
the header rule. The round-2 note above inferred from code that the background "was streaking";
the gate says otherwise, and the gate is the measurement. No wider rendering-path defect. The
structural point stands and is filed in queue.md: sampler state is implicit and order-dependent,
nothing at the call site distinguishes an element that needs WRAP from one that tolerates CLAMP,
and no headless test sees sampler state — the composite repeat test covers the header rule only.

**The record's value — three rounds, three different failure modes, one asset:**
1. Two GENERATED assets failed on content: 0.46% and 0.90% alpha coverage, #A77032 off-palette —
   invisible ink, caught by the visibility numbers.
2. The PROCEDURAL generator succeeded on content (32:1 aspect, ~34% coverage within-band 64.9%,
   exact two-ink palette, seamless by construction, four red proofs) — and still failed the GATE:
   every unit test certified the pieces while the composed draw ran under an inherited
   LinearClamp sampler nothing had ever asserted.
3. The sampler fix (one explicit LinearWrap in ImGuiRenderer) passed all four gate checks.
Tests certify what they mention; each round's failure lived precisely in what no test mentioned.
