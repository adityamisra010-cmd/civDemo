# D-069: originality — no third-party game references in shipped content; briefs name our design, not other games (Director, 2026-11-01)

**Status: RULED.** Recorded from the Director's instruction of 2026-11-01 ("Agreed. Do your recommendations"). It
answers the briefing on Anthropic's change of late October 2026: Claude Code on Opus 5.5 now steers away from
recreating a specific existing game, and Anthropic's usage policy forbids using Claude to infringe IP. Original games,
genre games and games built from one's own design remain supported.

- **What this record does:** it adds rulings. It edits no earlier record (gov-4 §6). Earlier records that cite other
  games as design references (e.g. D-047, the Civ VI comparative research, the unit charter) stay as written. They are
  design research, and this ruling does not withdraw them.
- **What it does NOT do:** it changes no simulation behaviour, number or hash outside the research-text pin named in R2.
  It does not close M6 or touch the `CLAUDE.md` milestone line.

## Rulings

| # | Subject | Ruling |
|---|---|---|
| R1 | Briefs and packets | **Work is specified in terms of this game's own design and of genres, never as a copy of another game.** Write "a civics lane that grants governments, as the 4X genre does", not "do it like Civ VI". Comparative research that studies another game's *pattern* is allowed, provided it lives in `docs/`, is labelled as reference, and never becomes a node-for-node, name-for-name or text-for-text transfer. Rule and procedure: [ip-originality.md](ip-originality.md); a one-line pointer is added to `CLAUDE.md` (Governance). |
| R2 | Shipped content | **Shipped content names no other game.** Shipped content is everything under `Sim.Data/content/` and everything the player can read in the game. It must not name another game, studio or franchise.<br>- **Design notes** move to `docs/`: the `reference` fields of `unit-families.json` and the third-party mentions in `_doc` strings and node descriptions.<br>- **Approved re-pin:** the three player-facing node descriptions that cite "Civ 6 / Civ VI" (`frontier_propulsion`, `trade`, `taxation`) are rewritten without the reference. This is an approved TEXT-only change under the tree closure, and it moves only the TEXT hash (`tree-closure.md` §5). The STRUCTURE hash must not move.<br>- **Guard:** a test fails if any shipped content file names a listed third-party game or studio. |
| R3 | Release gate | **Before any public release, the IP-originality checklist ([ip-originality.md](ip-originality.md) §3) must be run and passed.** It covers the content scan, a name-overlap review of nodes, units, eras and governments against the major games of the genre, help text, art and audio provenance, and the store-page text. A first-pass automated name-overlap report is produced now. It is an input to that review, not a verdict. |
| R4 | Historical names; "Eureka" (Director, 2026-11-02) | **Real historical names stay.** This is a historically grounded game. Technologies, discoveries, institutions, government forms and unit types that existed in history (Gunpowder, Chiefdom, Humanism, Pikemen, Knights…) keep their names even when another game uses the same word; no game owns history. The same holds for upgrade lines that follow the real historical sequence. What the release gate (`ip-originality.md` §3) still checks is narrower: coined or distinctive terms that are another game's invention, and trees that copy another game's layout position for position.<br>**"Eureka" is renamed "Idea"** in everything the player reads: the research screen's heading, its empty-state line and its credit line. "Eureka" is the one coined term the first-pass overlap report flagged, because another game uses that name for the same mechanic. Internal code and data names (`eurekas` in `research.json`, the `ResearchEurekas` table, the `eureka` source id) stay unchanged. They are not player-facing, and renaming them would move the kernel schema and the content hashes for no player-visible gain. |
