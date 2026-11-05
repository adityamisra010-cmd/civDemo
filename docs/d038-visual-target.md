# D-038 — THE ILLUMINATED ATLAS: VISUAL TARGET AND ITS MILESTONE

Director design ruling. Decision record — exempt document class under S8 §4.
Amends docs/style-bible-parchment.md §1, §2, §4, §5, §6 and adds §7.
Supersedes the visual ceiling implied by "the debug UI is the game UI" (D-002) for the MAP SURFACE only; D-002's doctrine stands unchanged for panels and data display.

## PART A — WHAT CHANGES AND WHY

A1. THE CEILING WAS TOO LOW AND THE DIRECTOR IS RAISING IT. The parchment substrate is accepted and stays. What is rejected is the flat ceiling: a map on which nothing has depth, no settlement grows visibly, and no army reads as a thing rather than a dot. The target is a genuinely beautiful map, at or above the stylized-4X reference class.

A2. THE REFERENCE CLASS WAS ALREADY NAMED. The symbology deferral (queue.md, T2.12 director ruling) targets "the Troy/Humankind stylized reference". This ruling does not introduce a new ambition; it states the one already recorded and gives it a milestone.

A3. THE DIRECTION IS THE ILLUMINATED ATLAS, NOT THE STYLIZED WORLD. Two coherent directions existed. The rendered 3D world (Civilization VI proper) is REJECTED: it discards the substrate already built, and it fights the six-thousand-year, twelve-to-eight-hundred-settlement scale actually being simulated. The illuminated atlas is ADOPTED: parchment stays the ground, and the things drawn ON it gain depth, relief and light. Reference class: Crusader Kings 3's map, Old World, Total War campaign maps, Humankind.

A4. NOTHING ALREADY BUILT IS DISCARDED. Style bible §4's substrate manifest — parchment base, grain overlay, terrain washes, coastline ink, UI frame furniture, the procedural header rule — all survive unchanged. This ruling adds an object tier above them.

## PART B — THE TWO-LAYER LIGHTING RULE (amends §1)

B1. Style bible §1 currently reads "Lighting: none. A map is lit flat. No cast shadows, no sun angle, no ambient occlusion." That becomes a TWO-LAYER rule:

THE SUBSTRATE LAYER IS LIT FLAT, FOREVER. Paper, washes, coastlines, hydrography, UI furniture. No lighting model. Unchanged from today.

THE OBJECT LAYER CARRIES ONE CONSISTENT LIGHT. Settlements, terrain relief, forests, armies, structures. A single global light direction, stated once as a constant and never varied per asset, with a contact shadow. Objects sit ON the paper; they are not part of it.

B2. The single-cartographer rule (§1) EXTENDS rather than relaxes: every object asset must read as rendered by one hand under one light on the same sheet. One light angle, one shadow treatment, one palette. An asset lit differently is rejected, not accepted as a near-miss.

## PART C — THE ANATOMY FENCE (new §7), DIRECTOR'S EXPLICIT CONSTRAINT

C1. NO ANATOMICALLY DETAILED CHARACTERS. Not now, not later, at any milestone. People never render as figures with faces, limbs or animation cycles.

C2. WHAT IS PERMITTED AND SHOULD BE PUSHED HARD: settlements and structures, terrain relief, forests, fields, roads and bridges, ships and carts, army and formation tokens, banners, borders, production and resource glyphs.

C3. WHERE PEOPLE MUST APPEAR THEY ARE SILHOUETTES OR TOKENS. This is already ruled for battles — D-011 §4: "each token drawn as a cluster of tiny sprites thinning as strength drops — Ultimate-General-style". C1 generalises that ruling to the whole visual layer rather than creating a new one.

C4. THE FENCE IS ALSO THE RIGHT DESIGN. At map scale a figure reads as a smudge; a silhouette reads as an army. The constraint costs nothing the game needs.

## PART D — PRODUCTION METHOD (amends §5)

D1. TWO PRODUCTION METHODS ARE NOW SANCTIONED, and the choice is by asset kind, not by convenience.

PROCEDURAL, IN CODE — the default for anything geometric. Precedent: HeaderRuleBaker (T3.8-era D-A1) produced a correct asset in 143 lines after two rounds of image generation had failed, and it is palette-exact and seamless BY CONSTRUCTION rather than by inspection. Terrain washes, borders, glyphs, icon tiers, UI furniture, tokens.

PARAMETRIC RENDER — for objects wanting depth. A scripted scene description, rendered headless to sprite sheets, committed as assets with the script beside them. The script is the asset's source; the sprite is its build output. Assets must be REGENERABLE — a palette change regenerates every asset rather than invalidating them.

D2. IMAGE GENERATION IS THIRD-CHOICE AND ITS FAILURE MODE IS RECORDED. Two D-A1 rounds failed on a geometric slot with a hard aspect ratio and a tiling requirement (measured: alpha>127 at 0.46% and 0.90%, off-palette #A77032). That is a verdict on generation FOR GEOMETRIC SLOTS, not on generation generally; it remains viable for organic and illustrative material. State the distinction so the record is not read as a blanket rejection.

D3. §5's prompt skeleton stays for generated assets and gains a companion: the parametric render spec, stating scene, camera, light angle, output resolution and palette binding.

## PART E — THE MILESTONE

E1. THE VISUAL PUSH IS AN INSERTED MILESTONE AFTER M5 AND BEFORE M6. It absorbs the deferred symbology packet rather than running alongside it.

> **2026-10-12 — Director (`docs/d052-m5-integration-audit-rulings.md` §6): the visual milestone is placed at M6.**
> - M5 keeps the composed visual tier.
> - Individually drawn buildings and institutions (count staging), detailed Age-specific terrain and architecture, and
>   richer unit art are scheduled with M6.
> - The order and names in `docs/milestones.md` are unchanged.
> - C1 (no figures) stands.

E2. WHY AFTER M5: the symbology deferral's own reasoning — "symbology encodes what the map CONTAINS, and M3 (goods/markets), M4 (neighbors/armies), M5 (unrest) each change that content; redrawing piecemeal in the interim is wasted work." After M5 the content is settled. That reasoning is upheld, not overridden.

E3. WHY NOT INSIDE M5: M5 is the governing loop and the "it's a game now" checkpoint. M4 is already ruled to stay whole as a large milestone; two consecutive mega-milestones is a different bet and is not taken.

E4. WHY BEFORE M6 *(2026-10-03 roadmap rebase: the battle layer is now **M7**; the reasoning applies to M7)*: D-011 §4 already specifies how battle formations render. If the visual language exists first, M6 INHERITS it rather than inventing a second one.

E5. WHY NOT M10: M10 is five milestones out. The director's play sessions are the project's defect-finding instrument — both the M1 and M2 exit gates found real defects that way and M2's exit was HELD on two. Making the game legible and pleasant to sit with earlier is tooling, not vanity. A smaller polish pass at M10 remains expected.

E6. THE RENDERER IS ARCHITECTURALLY FREE, AND THAT IS WHY THIS IS AFFORDABLE. Measured: Sim.Ui is 4,778 lines; Sim.Core is 11,738; no PRODUCTION project references Sim.Ui — only its own test project (Sim.Ui.Tests) does (verified by project-reference grep). [Corrected at certification, DIRECTOR-AUTHORED: the original text read "NOTHING references Sim.Ui" — the grep behind that claim excluded the path prefix Sim.Ui, which silently excluded Sim.Ui.Tests too; the T3.8 handback's §7.12 finding caught it, and the substantive claim is unaffected.] ADR-009 guarantees "floats, wall-clock frame timing, GPU/driver variation and Dictionary iteration in Sim.Ui cannot alter a single world hash." The visual layer therefore carries ZERO simulation risk, and a future engine change would rewrite the window, not the game. Record that as a standing property, not as a plan.

## PART F — WHAT THIS RULING DOES NOT DO

F1. It does not schedule an engine change. MonoGame + ImGui (ADR-009) stands.
F2. It does not amend D-002 for panels and data display. "The debug UI is the game UI" holds for HUD, market, graphs and annals. This ruling governs the MAP.
F3. It does not authorise any work before M5. Assets built now get redrawn — that is the deferral's whole point and it is unchanged.
F4. It does not commit an asset budget. Whether objects are procedural, rendered, purchased or commissioned is the visual packet's decision, against D1's ordering.

## PART G — ACCEPTANCE (amends §6)

G1. §6's current bar — "it looks like an atlas, not a debug tool" — is DISCHARGED and superseded for the object layer.
G2. The new bar: A SETTLEMENT'S SIZE IS LEGIBLE AT A GLANCE WITHOUT READING A NUMBER; an army reads as an army; terrain has relief; and the whole reads as one illustrated map rather than a set of assets on a shared background.
G3. The single-cartographer test survives as the failure condition: any asset that reads as lit differently, or drawn by a different hand, is rejected.
G4. Director visual gate as ever. Automated tests cover palette exactness, seamlessness and regenerability; the look is the director's call (D-023).

---

## PART H — HOW A SETTLEMENT SHOWS WHAT IT HAS BUILT

Director ruling, taken after D-038's original filing. **Append-only: amends nothing in Parts
A–G**; it answers a question they left open.

**CITATION CHECK (§7.12).** Every citation below was verified against the tree at `cadcc83`.
All hold except one, recorded at H2 as a finding: the tree does not contain the phrase
"one settlement per place".

H1. **THE QUESTION.** A settlement gains institutions and works over the campaign — granaries,
    walls, temples, barracks, libraries, universities. Should the map show them, or should they
    remain a list revealed on selection?

    **RULED: THE MAP SHOWS THEM.** A settlement the director has watched for two thousand years
    should look like what it has become. This is the same argument A1 made for raising the visual
    ceiling at all.

H2. **THE DESIGN CONSTRAINT THAT SHAPES THE ANSWER.** Districts inside a settlement are
    ABSTRACTED. So a university is NOT an object at coordinates inside a settlement — it is a
    **CAPABILITY THE SETTLEMENT HAS**. Any visual treatment must express a capability, never
    imply an internal map.

    > **FINDING (§7.12) — the citation as directed does not match the tree, though its substance
    > does.** The ruling was cited as "D-009 rules ONE SETTLEMENT PER PLACE, with districts
    > abstracted". **No line in the tree says "one settlement per place."** What D-009 actually
    > says, verbatim, is two separate things:
    > - *"Districts inside remain abstracted (v3 position holds)"*
    >   (`d009-d010-map-population-addendum.md:15`) — this is the load-bearing half, and it fully
    >   supports H2's constraint;
    > - *"The unit of 'where' is a settlement and its hinterland"* (`:13`) — the granularity
    >   ruling, which is about settlements-and-catchments as the unit of place, not about a
    >   one-per-place cardinality.
    >
    > The tree wins: **cite the abstraction of districts, not a one-per-place rule.** The
    > constraint H2 imposes is unaffected — it rests entirely on districts being abstracted — so
    > this corrects the citation, not the ruling. Recorded because a future reader chasing
    > "one settlement per place" through D-009 will not find it.
    >
    > **THE FOOTPRINT IS THE SAME DEFERRED ITEM, NOT A MISSING PREREQUISITE** (corrected on the
    > director's ruling; an earlier draft of this note warned that "sprawl was not built", which
    > was half right and misleading). `m3-spec.md:15` reads: *"**D-017 CLOSED — Settlement
    > growth.** A settlement carries a **size** stock grown from population and construction,
    > which drives housing capacity, catchment radius bonus, and (render-side) footprint. Sprawl
    > visuals are symbology — deferred per the queue."* The footprint was **ruled RENDER-SIDE and
    > deferred to this same visual milestone.** So the composed sprite and the organic footprint
    > are not overlapping answers to one question — **they are one deferred item, and the composed
    > sprite may well be HOW the footprint gets expressed.** The visual milestone should treat
    > them together.
    >
    > **FINDING (§7.12), MEASURED — "D-017 CLOSED" OVERSTATES, AND A MECHANICAL HALF WENT WITH
    > THE VISUALS.** D-009:15 says footprints *"consume real farmland as they expand (the
    > food-ceiling tension made visible)"*. **Consuming farmland is not render-side.** Measured
    > against the shipped sim at `cadcc83`: a settlement's growth **NEVER reduces arable — its
    > own or a neighbour's.** `EffectiveArableKm2` is summed from `BlockArableKm2`
    > (`CatchmentSystem.cs:204`), a pure function of TERRAIN — immutable after worldgen per
    > ADR-008 — and the lattice; no writer anywhere subtracts for settlement size or dwellings.
    > The only thing `SizeTier` does to land is the **opposite**: it RAISES the travel budget
    > (`TierBudget`, `base × (1 + sizeBonusMaxRatio × tier/4)`), so growth ADDS reachable nodes.
    > A neighbour's arable can shrink only through catchment partition competition — reachability,
    > not consumption.
    >
    > So D-017's size stock, housing capacity and catchment bonus are real and closed; **the
    > farmland-consumption mechanism is not implemented and is not scheduled anywhere.** Filed for
    > the M3 exit record and carried in the milestone entry's known-open list. **Not fixed here,
    > and Part H does not depend on it** — a composed sprite reads state; it does not need the
    > footprint to consume land.

H3. **TWO OPTIONS WERE CONSIDERED.** Both recorded, because the rejected one is cheaper and a
    future reader should see it was weighed.

    - **GLYPH RING** — small icons arranged around the settlement marker. Cheap, legible, scales
      to the Scale Charter's late-game settlement counts (*"settlements ~50 (ancient) → 300–800
      (late)"*, `d009-d010:19` — verified), needs no new sim concept, and is pure
      procedural-in-code under D1. **REJECTED as the primary treatment.**
    - **COMPOSED SETTLEMENT SPRITE** — the settlement's own art changes shape as it gains
      institutions and size. **ADOPTED.** Rationale: it makes the settlement itself the thing
      that grew, rather than decorating a marker that did not, and it extends T3.8's size tiers,
      which already vary the settlement by what it has built (`SummaryRow.SizeTier`, a quantized
      0..4 step computed from dwellings — `WorldState.cs:90-95`, `CatchmentSystem.cs:60` —
      verified).

    The director's ruling in substance: **the composed sprite is the heavier option and is chosen
    because it is better, with the additional work accepted deliberately.**

H4. **THE COMBINATORIAL PROBLEM IS THE REAL ENGINEERING DECISION, AND IT MUST BE SOLVED BY PARTS
    AND ASSEMBLY, NOT BY PRE-RENDERING COMBINATIONS.**

    Size tiers × walls × temple × barracks × library × whatever M5–M8 adds is a combination count
    that grows multiplicatively. Pre-rendering every combination does not scale and never will.

    **REQUIRED APPROACH:** each PART is authored once — a tier's built form, a wall ring, an
    institution's structure — and the settlement is **COMPOSITED AT DRAW TIME** from the parts
    its state says it has. Adding an institution then costs one part, not a re-render of the
    whole matrix.

    This suits D1's parametric-render method exactly: parts are scripted scene descriptions
    rendered headless to sprites, and the script is the part's source. **Note the constraint it
    imposes: EVERY PART MUST BE AUTHORED AGAINST THE SAME LIGHT ANGLE AND THE SAME GROUND PLANE**,
    or composited settlements will read as collage. That is B2's single-cartographer rule applied
    at the part level, and it is **stricter here than anywhere else in the visual layer**, because
    the parts sit adjacent in one image.

H5. **WHAT DRIVES THE COMPOSITION IS COMPUTED STATE, AND LAW 4 BINDS.** A settlement shows walls
    because it HAS walls, a university because it HAS one. No era gate, no date, no unlock. The
    sprite is a **READ** of state — it never becomes a source of truth, and nothing in the sim may
    ever consult it. (ADR-009 already guarantees the direction of that dependency: nothing in
    `Sim.Ui` can alter a world hash — E6.)

H6. **GLYPHS ARE NOT REJECTED, ONLY DEMOTED.** Things that must be legible at world-fit zoom, or
    that have no built form — a trade status, an unrest marker, a production emphasis — remain
    glyphs, procedural in code per D1. **The composed sprite carries what a settlement IS; glyphs
    carry what is happening to it.** Stated here so the visual packet does not have to invent the
    division.

H7. **WHAT THIS DOES NOT DO.** It does not schedule work — the inserted visual milestone after M5
    and before M6 owns it (E1). It does not amend the abstraction of districts; H2 explicitly
    upholds it. It does not authorise any asset before that milestone, for the reason the
    symbology deferral already gives and which is verified in `docs/queue.md`: *"symbology encodes
    what the map CONTAINS, and M3 (goods/markets), M4 (neighbors/armies), M5 (unrest) each change
    that content; redrawing piecemeal in the interim is wasted work"*. M4 and M5 change what a
    settlement contains, and drawing before that is redrawing.

H8. **ONE OPEN QUESTION, NOT RULED:** how many distinct parts a settlement may show before the
    sprite becomes unreadable at map zoom, and what happens past that limit — do parts merge, does
    the sprite abstract upward, or does the settlement show only its most significant works? This
    is a legibility question that **cannot be answered before the parts exist.** The visual
    milestone owns it; **name it there rather than discovering it mid-packet.**

## Dated note — 2026-11-03 (D-075; appended, the text above is unchanged)

Rows S19–S23 of D-075 §2.2. D-038's C1, C3 and E4 reach battles (C3 at line 33 and E4 at line 64 say so in terms). The supersession below is bounded by the Director's decision of 2026-11-03 — the M7 battlefield and the War Sandbox — and not by any scope statement in this record. For the campaign map, D-038 stands unchanged (A3, B1/B2, C1–C4, D1–D3, E6, F1–F4, G1–G4, Part H); whether the map should change is OPEN and raised only for awareness (D-075 O-26). How battlefield assets are produced (F4, line 75; D1–D3) is OPEN (D-075 O-25); the host engine is OPEN (D-075 O-13).

- A3, line 13 — "The rendered 3D world (Civilization VI proper) is REJECTED" — SUPERSEDED IN PART: only as the rejection reaches the battlefield through E4 (retained: the campaign map). Replaced by D-075 R21; Director's revision §A and §E (REV:382, :473), docs/d075-m7-unified-warfare-direction.md (S19).
- C1, line 29 — "NO ANATOMICALLY DETAILED CHARACTERS. Not now, not later, at any milestone. People never render as figures with faces, limbs or animation cycles." — SUPERSEDED IN PART, for the M7 battlefield and War Sandbox: animated individual figures are decided; superseding the prohibition does not mandate faces (retained: the campaign map). Replaced by D-075 R16, R22, R24, supported by R13; Director's revision §A, §C and §E (REV:383, :450, :454, :473), docs/d075-m7-unified-warfare-direction.md (S20).
- C3, line 33 — "WHERE PEOPLE MUST APPEAR THEY ARE SILHOUETTES OR TOKENS. This is already ruled for battles — D-011 §4 …" — SUPERSEDED IN PART, for the battlefield and War Sandbox (follows D-011 §4's supersession, D-075 S9) (retained: the campaign map). Replaced as C1 above (S21).
- Dated note of 2026-10-12, line 58 — "C1 (no figures) stands." — SUPERSEDED IN PART, as C1 above (retained: the campaign map). Replaced as C1 above (S22).
- E4, line 64 — "D-011 §4 already specifies how battle formations render. If the visual language exists first, M6 INHERITS it rather than inventing a second one." — SUPERSEDED IN PART: the M7 3D battlefield's visual language is not inherited from the map's token language (retained: the map's own inheritance). Replaced by D-075 R21, R22; Director's revision §A and §E (REV:382, :473), docs/d075-m7-unified-warfare-direction.md (S23).

Recorded administratively under the Director's decisions of 2026-11-03; the historical text above is preserved.

## Dated note — 2026-11-04 (D-077; appended, the text above is unchanged)

Record: `docs/d077-visual-workstream-procedural-graphics-avatar-cinematics.md` (Director visual decisions of 2026-11-04).
- **S86 (C1), S87 (C3), in part, widened beyond D-075 S20:** the player's persistent avatar (customizable face and body) and the opposing leader or representative render as detailed animated figures in cinematics, diplomatic encounters and campaign-event scenes (D-077 V3, V4). The campaign map itself is retained under C1/C3.

## Dated note — 2026-11-05 (D-078; appended, the text above is unchanged)

Record: `docs/d078-graphics-quality-mandate.md` (Director graphics quality mandate of 2026-11-05). D-078 §3 (A-28) lists this note; it was appended on 2026-11-05 during the D-079 pass (D-079 A-30).

- **S100** (C1, line 29, "NO ANATOMICALLY DETAILED CHARACTERS. Not now, not later, at any milestone."; C3, line 33, "WHERE PEOPLE MUST APPEAR THEY ARE SILHOUETTES OR TOKENS."), as already superseded in part by D-075 S20–S22 and D-077 S86–S87 — **widened:** detailed faces, eyes, hair and skin are now **mandated** for battlefield soldiers and commanders (D-078 QM4). The D-075 note's "superseding the prohibition does not mandate faces" (line 219) no longer holds. Retained: the campaign map itself (D-075 O-26 and D-038 A3 as to the map), subject to D-078 Q-QM1.

## Dated note — 2026-11-05 (D-079; appended, the text above is unchanged)

Record: `docs/d079-m7-governance-reconciliation-2026-11-05.md` (Director decisions of 2026-11-05, cited DIR5). Each bullet quotes the superseded line, states the extent and what is retained, and cites the replacing DIR5 section and D-079 decision row (DR). Categories: DIRECTOR DECISION, ADMINISTRATIVE CONSEQUENCE, DELEGATED TECHNICAL DECISION, OPEN (needs the Director), READING (labelled interpretation).

- **S215** (`d038-visual-target.md:29`) — "NO ANATOMICALLY DETAILED CHARACTERS. Not now, not later, at any milestone." — in part, widened again after D-075 S20, D-077 S86, D-078 S100 [DIRECTOR DECISION]: detailed human anatomy, credible faces, eyes, skin, hair and facial topology are mandated for battlefield soldiers, commanders and the player avatar, with full animation (locomotion, combat, death, surrender); retained: the campaign map (D-075 O-26 and D-078 Q-QM1 OPEN). **Replaced by** DIR5 §5.1, §6.2 (D-079 DR39, DR45). [DIRECTOR DECISION]
- **S216** (`d038-visual-target.md:33`) — "WHERE PEOPLE MUST APPEAR THEY ARE SILHOUETTES OR TOKENS." — in part, as C1 (after D-075 S21, D-077 S87) [DIRECTOR DECISION]; retained: the campaign map. **Replaced by** DIR5 §5.1, §6.2 (D-079 DR39, DR45). [DIRECTOR DECISION]
- **S217** (`d038-visual-target.md:39`) — "D1. TWO PRODUCTION METHODS ARE NOW SANCTIONED, and the choice is by asset kind, not by convenience." — in part, for M7 battlefield, character, avatar and cinematic assets [DIRECTOR DECISION]: a mixed procedural/purchased/commissioned/custom-rendered strategy with high-quality authored base assets and procedural variation, provenance and licensing tracked; retained: the campaign map's procedural-in-code and parametric-render methods, D2's record on image generation. **Replaced by** DIR5 §6.4 (D-079 DR47). [DIRECTOR DECISION]
- **S218** (`d038-visual-target.md:58`) — "C1 (no figures) stands." — in part, as C1 (after D-075 S22) [DIRECTOR DECISION]; retained: the campaign map. **Replaced by** DIR5 §5.1, §6.2 (D-079 DR39, DR45). [DIRECTOR DECISION]
- **S219** (`d038-visual-target.md:75`) — "Whether objects are procedural, rendered, purchased or commissioned is the visual packet's decision, against D1's ordering." — in part, for M7 visuals [DIRECTOR DECISION]: the Director has fixed the mix (all four sources, D-077 V10) and the ordering (authored base assets first, procedural variation multiplies quality), so "against D1's ordering" no longer governs them; retained: the map. **Replaced by** DIR5 §6.4 (D-079 DR47). [DIRECTOR DECISION]
- **S220** (`d038-visual-target.md:216`) — "How battlefield assets are produced (F4, line 75; D1–D3) is OPEN (D-075 O-25); the host engine is OPEN (D-075 O-13)." — closes both (already D-077 V6, V10, S94) [DIRECTOR DECISION]; retained: the rest of the note, including O-26 for the campaign map. **Replaced by** DIR5 §6.1, §6.4 (D-079 DR44, DR47). [DIRECTOR DECISION]
- **S221** (`d038-visual-target.md:219`) — "superseding the prohibition does not mandate faces" — in full [DIRECTOR DECISION]: faces are mandated ("Credible faces, eyes, skin, hair and facial topology"; avatar "Convincing face and body"); already D-078 QM4/S100, whose dated note on this file was appended in the same pass (line 231 above). **Replaced by** DIR5 §6.2, §5.1 (D-079 DR39, DR45). [DIRECTOR DECISION]
