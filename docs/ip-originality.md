# IP originality: how we design, and the release gate (D-069)

**Authority:** [D-069](d069-originality-and-third-party-game-references.md) (Director, 2026-11-01).

This game is an original civilization simulation. Its mechanisms, numbers, names, text and art are our own:
- the deterministic ledger;
- population classes and needs;
- the fog lattice;
- the two research lanes;
- the procedurally generated, Sumerian-derived place names;
- the era style.

Being in a genre (4X / grand strategy) is fine. Reproducing a specific game is not: that is an IP risk, and Claude
Code also declines it.

## 1. Writing briefs and packets

- **Do:** describe what we want in our own terms and in genre terms: "a second research lane for social institutions
  that unlocks governments", "explore reveals land at a food cost".
- **Do:** study other games as design research in `docs/`, labelled as reference, to understand a *pattern* (why a
  tree has two lanes, how pacing feels).
- **Don't:** ask for another game's tree, unit roster, numbers, names or text to be reproduced ("copy Civ VI's civics
  tree", "make it exactly like X but renamed"). Don't ship a node-for-node or name-for-name mapping.
- **Don't:** put another game's name in anything the player can read, or in `Sim.Data/content/`.

## 2. What counts as shipped content

- **Shipped:** every file under `Sim.Data/content/` (descriptions, `_doc` strings and data fields alike), every
  player-facing string in `Sim.Ui`, and the published build and its store or readme text.
- **Not shipped:** `docs/`, and test fixtures that are clearly marked as such.

## 3. Release gate checklist (run before any public release; record the result as a dated note below)

1. **Content scan:** the third-party-reference guard test passes, and no shipped file names another game, studio or
   franchise.
2. **Name overlap:** review node names (technology and civics), government forms, unit family and identity names, era
   and Age names, building names and policy names against the major games of the genre, starting from the first-pass
   report (§4).
   - Common historical terms (e.g. "Bronze Working", "Feudalism", "Skirmisher") are fine. Real historical names stay
     even when another game uses the same word, and so do upgrade lines that follow the real historical sequence
     (D-069 R4). This is a historically grounded game.
   - Distinctive coined names, distinctive combinations, or the same names in the same tree structure need renaming.
3. **Tree structure:** our trees must not mirror another game's tree position for position, even under different
   names.
4. **Text:** descriptions, quotes and help text are our own writing. Historical sources are cited in `docs/`, never
   another game's text.
5. **Art, audio and fonts:** every asset has a recorded provenance (made by us, generated, or licensed, with the
   licence noted). Nothing is taken from another game.
6. **Store and readme text:** no "like Civilization" positioning that uses another game's trademark beyond plain
   comparative fact.
7. **Sign-off:** the Director records PASS, or the list of renames.

## 4. First-pass name-overlap report

A first-pass automated report produced under D-069:
[m6-plan/ip-name-overlap-report.md](m6-plan/ip-name-overlap-report.md). It lists exact and near name matches between
our content and the reference lists already in `docs/` research. It is an input to §3 item 2, not a verdict.

## Dated notes

- 2026-11-01: created under D-069. No release has been made, so the gate has not been run.
- 2026-11-02 (D-069 R4): historical names are confirmed as fine. The one flagged coined term, "Eureka", is renamed
  "Idea" in the player-facing research screen, so the first-pass report now flags no names.
