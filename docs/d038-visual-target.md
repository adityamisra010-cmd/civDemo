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

E2. WHY AFTER M5: the symbology deferral's own reasoning — "symbology encodes what the map CONTAINS, and M3 (goods/markets), M4 (neighbors/armies), M5 (unrest) each change that content; redrawing piecemeal in the interim is wasted work." After M5 the content is settled. That reasoning is upheld, not overridden.

E3. WHY NOT INSIDE M5: M5 is the governing loop and the "it's a game now" checkpoint. M4 is already ruled to stay whole as a large milestone; two consecutive mega-milestones is a different bet and is not taken.

E4. WHY BEFORE M6: D-011 §4 already specifies how battle formations render. If the visual language exists first, M6 INHERITS it rather than inventing a second one.

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
    > **Adjacent, and worth knowing before the visual milestone designs anything:** the same
    > D-009 line says settlement footprints are *"organic blobs that grow along the network and
    > terrain suitability… consuming real farmland as they expand"*, and D-017 schedules the
    > sprawl model at M3 (`:60`). **Sprawl was not built.** A composed sprite and an organic
    > footprint are different answers to overlapping questions; whoever owns the visual milestone
    > should know the footprint half is unimplemented rather than assume it exists to compose on.

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
