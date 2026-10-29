# E1 visual acceptance — the Director's Part G gate checklist (M6 E1)

**Status: NOT YET HELD.** This is the checklist for the Director's visual gate on the E1 object layer (D-038 Part G;
D-066 A3: "One Part G gate for the E1 batch, held at the playtest"; D-066 R6: human gates are never claimed unless
performed). Nothing in this document, in the tests or in the hand-back claims that the gate passed. Automated tests cover
what tests can (one light, palette, convexity, parts-from-state, stages, fold, register, no figure); **the look is the
Director's call (D-023, D-038 G4).**

Authority: D-038 Parts B, C, G, H; D-043 D2; D-052 §6–§7; D-066 A3, A4; style bible §8 (`docs/style-bible-parchment.md`).
Decisions taken to build it: `docs/m6-plan/e1-object-layer-note.md`.

---

## 1. Launch the build

From a clean checkout of the integrated M6 tree:

```
./scripts/bootstrap.sh                       # remote containers only: installs the .NET 10 SDK if missing
dotnet build Sim.slnx -c Release
```

Then play it (Windows: `Sim.Ui\bin\Release\net10.0\Sim.Ui.exe` with the same arguments):

| What | Command | Why |
|---|---|---|
| The canonical game | `dotnet Sim.Ui/bin/Release/net10.0/Sim.Ui.dll --seed 42` | the world the playtest uses; play 20–150 turns so structures, roads and (once researched) cultivation appear |
| A crowded mid-game map | `dotnet Sim.Ui/bin/Release/net10.0/Sim.Ui.dll --seed 42 --settlements 300` | 300 settlements on the canonical map (its siting capacity is 362) |
| The Scale Charter's late game | `dotnet Sim.Ui/bin/Release/net10.0/Sim.Ui.dll --seed 42 --size 2560 --settlements 800` | 800 settlements needs the 2560-px dev size (D-015 dev escape; the canonical cylinder sites at most 362) |

The headless evidence (no window needed) — every surface as SVG and as PNG:

```
dotnet Sim.Ui/bin/Release/net10.0/Sim.Ui.dll --e1-preview e1-preview
```

writes, per world (canonical at turn 150; 300 settlements; 800 settlements at 2560 px), the map at **three zooms**
(`-world`, `-regional`, `-settlement`) in **three Ages** (`-age1`, `-age5`, `-age9`), each over its own parchment bake, and
`e1-parts-sheet.svg/.png` — every part × count stage × register rung, the scaffold, the overflow part, every terrain mark,
a field and every identity's silhouette token. The worlds carry a small, logged ILLUSTRATION (the only constructed state:
granary × 30, workshop × 12, institutions 20/1/10, a queued university, a group of three warbands at the capital; see
`e1-preview-log.txt`) so every stage, the scaffold and a grouped token are on the map. The hand-back's copy is in the
coordinator's scratchpad (`scratchpad/m6/e1-evidence/`).

Zoom in the game: mouse wheel. The bands: World (whole map) → Regional (from about 2,250 km across) → Settlement (from
about 650 km across); maximum zoom is "high zoom".

## 2. G2 — legibility ("a settlement's size is legible at a glance without reading a number; an army reads as an army;
## terrain has relief; the whole reads as one illustrated map")

Tick each, or write what failed:

| # | Look at | Pass when | Verdict |
|---|---|---|---|
| G2.1 | World band, the 300- and 800-settlement maps | You can rank five neighbouring settlements by size from their marks alone, without reading any number | ☐ |
| G2.2 | Regional band on any cluster | Footprints differ visibly in size; a settlement with buildings looks built-up (small solids inside its footprint), an empty one does not | ☐ |
| G2.3 | Settlement band on the capital (illustrated: granary × 30, workshop × 12, institutions) | Each building kind is a distinct small structure; a kind with 30 reads as a complex, 10 as expanded, 1 as a single building, WITHOUT reading the key | ☐ |
| G2.4 | Maximum zoom on the same capital | Counts above one appear as small numbers on the parts; nothing overprints the name, the key or the formations | ☐ |
| G2.5 | A settlement with a queued project | The scaffold reads as "being built", not as a finished building | ☐ |
| G2.6 | Dwellings at the Settlement band, early vs. a settlement with roads/institutions | Huts vs. pitched roofs vs. flat roofs read as a change of built material that follows what was BUILT (the register), not the Age | ☐ |
| G2.7 | Formations at every band | A token reads as a body of troops / ships / engines / aircraft (a silhouette on a plate), not a dot; a grouped token's number reads as "how many formations" | ☐ |
| G2.8 | Regional and Settlement bands over hills, mountains, forests | Terrain has relief: mountains and hills stand up from the paper, forests read as woods; the marks look placed by hand, not stamped on a grid | ☐ |
| G2.9 | The World band | Relief is NOT drawn there (the washes carry the terrain; the World band's layers are D-047 Part 5's ruled density) — say whether that is acceptable or the World band should get relief | ☐ |
| G2.10 | A cultivating settlement (after cultivation is researched) | Fields lie around it on fertile land; a settlement still foraging has none | ☐ |
| G2.11 | The whole map at each band | It reads as ONE illustrated map, not a set of stickers on a shared background | ☐ |

## 3. G3 — the single-cartographer test (the failure condition)

"Any asset that reads as lit differently, or drawn by a different hand, is rejected" (D-038 G3, B2; style bible §8.1).

| # | Look at | Pass when | Verdict |
|---|---|---|---|
| G3.1 | `e1-parts-sheet.png`, every row | Every shadow falls the same way (east-south-east, short) and with the same softness; the lit faces are always on the west/top | ☐ |
| G3.2 | Relief vs. buildings vs. silhouettes, side by side at the Settlement band | Mountains, trees, buildings and tokens look lit by the same sun and drawn by the same hand | ☐ |
| G3.3 | Objects vs. the substrate | The paper, washes, coasts and rivers stay flat (no lighting); only objects carry the light | ☐ |
| G3.4 | All nine Ages (Age I/V/IX previews; or play across Ages) | Objects keep their light and form; only the era's ink/hand and the Age banner change | ☐ |
| G3.5 | Palette | No object colour reads vivid or off-palette against the parchment (the inks are style-bible §2 hexes and their derived lit/shade stops) | ☐ |

## 4. The anatomy fence — C1 to C4 sign-off (D-038 Part C; style bible §7)

| # | Ruling | Check | Verdict |
|---|---|---|---|
| C1 | No anatomically detailed characters, ever | Zoom to maximum on tokens of every family (`e1-parts-sheet.png` shows all 80 identities): no face, limb, head or animation anywhere | ☐ |
| C2 | Settlements, structures, relief, forests, fields, roads, tokens, banners pushed hard | The object tier is present and reads as intended (G2) | ☐ |
| C3 | Where people appear they are silhouettes or tokens | Formations are silhouettes of the formation as an object on a plate; the number counts formations, never people | ☐ |
| C4 | The fence is the right design | At map scale the silhouettes read as armies, not smudges | ☐ |

Automated support (not a substitute for the gate): `ObjectLayerTests` — single light, convexity, parts from state, stage
boundaries, fold (tie-dense), scaffold, register (monotone, Age-blind), terrain on known nodes only, fields only where
cultivation is realized, silhouettes convex and bounded (C1 extended), grouped tokens tie-dense; `UnitTokenDetailTests`
(P11's emblem C1 guard, unchanged); `EraSurfaceTests.TheTerrainSubstrate_IsByteIdenticalInEveryEra` (substrate pin).

## 5. Known limits to judge, not to discover

- **Content is thin**: the shipped content constructs 2 structure kinds and founds 5 university types; walls, temples and
  the like do not exist in state, so they are not drawn (D-038 H5). The register's rungs therefore change modestly.
- **The parts limit never folds with today's content** (8 = the 7 kinds that exist + 1); the fold is exercised by tests and
  is there for M7+ content.
- **Parts are small at the Regional band** (the P12a lattice's glyph is one unit, about 4 px there); they read as built-up
  texture until the Settlement band. Even at high zoom the composite is small against the footprint (P12a's footprint is
  generous and the lots are fixed inside its smallest size); enlarging it means re-laying P12a's lattice (its composition
  digest is pinned) — a decision for this gate, not taken here.
- **Fields** appear only once a settlement's cultivation is realized (researched); the seed-42 player has not researched it
  by turn 150, so the canonical previews show fields only through the logged illustration.
- **Grouped tokens**: in a normal M6 game each empire fields few formations, so a number above a token is rare.
- **The 800-settlement world** uses the 2560-px dev size, not the canonical map.
- **Frame cost** is measured, budgeted and reported in the note (§ frame cost); a Windows GPU run is not part of this gate.

## 6. Sign-off

| Gate | Verdict (PASS / FAIL / PASS WITH NOTES) | Notes | Director | Date |
|---|---|---|---|---|
| G2 legibility | | | | |
| G3 single cartographer | | | | |
| C1–C4 anatomy fence | | | | |
| E1 batch (D-066 A3: one Part G gate) | | | | |
