# M5 playtest — known issues and limitations (Director's personal playtest)

**Status: for the Director's review. Not an M5 exit record, not a release note, not packaged.** Written 2026-10-12 with
the D-052 implementation pass on `m5w-integration` (two commits on `f0427d2`), pushed to the new branch
`claude/m5-playtest-d052` for the playtest build. `claude/new-session-yavghm` stays at `2dc6af2`. Governing record:
`docs/d052-m5-integration-audit-rulings.md`.

**Playtest readiness is not M5 exit (D-052 §15).** This list is what the Director should know before playing. M5 stays
NOT complete; the exit and merge conditions are in D-052 §15.

---

## 1. How the playtest build starts

- `Play civ-sim.cmd` (the zip root launcher) starts a new game with **one AI Empire** (`--ai-empires 1`, D-052 §13).
  The session files carry `-a1` in their names. For a solo game, run `app\Sim.Ui.exe` from a terminal without the flag.
- Resuming is from a terminal: `app\Sim.Ui.exe --resume runs\<session-dir or manifest>`.
- Non-canonical sizes (`--size PX`) are a developer option; founding on them is fixed in this pass (§9).

## 2. Two calibration tests are RED (ruled; disclosed)

`CalibrationBatteryTests.Dev_MalthusCorridors_AllInBand` fails for two seeds:

| case | failure | cause |
|---|---|---|
| seed 42 | dev migration 8.71773E-05 per decade, below 0.75 × its recorded 0.000248624 | D-051 §1(b) time-averaged weather removes most weather-driven famine flight |
| seed 7 | dev population 124,455 at turn 1000, above the envelope's 119,000 | the same; seed 7's migration (1.0083E-04, ×0.3497) is also below its floor, hidden because the population check runs first |

- Ruled by the Director (D-052 §9, CR-020 §B): time-averaged weather stays ON; every frozen calibration reference is
  preserved; nothing is widened, waived, re-pinned or reverted; calibration is reassessed from measured evidence after
  the playtest.
- What it means for play: these are teeth on the 256-px DEV calibration world, not the canonical game world. They do
  not stop the game; they mean CI's `build-and-test` and `calibration` jobs will be red on this build (and CI skips
  its readonly-proof step after a failed Test step), and they block any merge to `main` and the M5 exit (GOV-3 B6).

## 3. Resources (D-051 §2 deferred until after the playtest)

- No availability classes, no common-material floor, no regional richness. Stone rides the raw elevation channel: in
  the earlier playtest 38 % of settlements had effectively no stone and the first granary waited until turn 113.
  Expect some settlements to be slow to build.
- Every deposit good is "present" at every settlement; there is no per-place scarcity yet.

## 4. The "now usable" resource shortcut — limitation

- It appears when research makes a resource usable (bronze casting → copper and tin ores; pottery firing → clay;
  farming → fertility), lasts **5 turns** (turns, not years: early turns are 10 years, so about 50 game years), and
  opens its overlay when you click "Show on map". The overlays stay available manually afterwards.
- **It outlines known LAND, not deposits.** Until D-051 §2 there is no per-place deposit presence, so the outline marks
  every known place whose channel value is above zero — on the canonical world, the whole known landmass (for the
  ore channel, the coastline of known land). It never reveals unknown ground and never says "discovered".

## 5. Food and population (D-051 §1(a) and §3 deferred)

- Flight still reads the previous turn's one-turn deficit (§1(a) smoothing is deferred); with time-averaged weather
  the per-decade spread is ~0.19 instead of ~0.30, so famine flight is much rarer than in the `2dc6af2` build.
- There is no "next turn" food / population forecast (§3).

## 6. Workshop and tools

- The Workshop is not offered in Age I: its tools cannot be made before bronze (the guard; the card shows the chain to
  Tin bronze, Age III). There is no primitive stone-and-wood toolmaking yet (approved direction, mechanism deferred;
  D-052 §11).
- INFERRED, not reproduced: in Age III+ a Workshop you queue can sit at the head of the construction queue for a long
  time if the settlement cannot hold 10 tools (farmers wear tools out), and there is no cancel order. If a queue stalls
  on a Workshop, note the settlement and turn.

## 7. No central stockpile

Each settlement uses its own stores (D-051 §6 deferred until after the playtest). No pooling between your settlements.

## 8. Units

- **Display differs from the closed decision (D-052 §7):** the map draws one emblem token PER FORMATION, shows no count,
  and selecting it opens a single-formation card. The agreed design — one representative per unit type with its
  formation count, and a combined army panel — is NOT built yet. Each polity has one formation today, so the visible
  difference is small.
- Units cannot move: movement and Action Points are M7 (D-052 §4). The card's movement line still says it is "awaiting
  the Director's ruling" — stale wording; the ruling is M7.

## 9. Saves and resume

- **Sessions recorded by another build cannot be resumed** (D-052 §13). That includes every session from the `2dc6af2`
  playtest build: the simulation changed (time-averaged weather, the AI founding policy), so the replay produces a
  different world from turn 1. This build refuses such a session with an incompatibility error before any window
  opens — on stderr with exit code 3, and on Windows in a native error dialog (SDL message box; best-effort, not
  exercised on Windows yet — see §12). Start a new game with this build; the old session files are not changed.
- Same-build save and resume work as before (replay of the order log, every turn's hash checked).
- A session recorded under an older SAVE SCHEMA is still resumed by replay with the "RESUMED SESSION" notice (D-008).
- `--size` maps: founding a settlement (player or AI) on a non-canonical map now reads deposits from that map's own
  cells. Before this pass it read the wrong cells, and could crash End Turn for sites in the bottom rows. That crash was
  measured: seed 1, `--size 256 --ai-empires 1`, the AI's founding at turn 179. A `--size` session recorded by an
  earlier build therefore stops reproducing from its first colony founded after turn 0, and is refused like any other
  incompatible session. Canonical-size sessions are unaffected by this fix.
- Only two resume failures get the clear report: a different build's session, and an order log the order checks
  reject (exit code 2, "error: <message>", also shown in the dialog). Other `--resume` failures still end with an
  unhandled error and no dialog: a wrong path, a corrupt or truncated manifest, a missing order log. Do not edit or
  move files inside a session folder.
- INFERRED, not measured: a build whose turn-0 world differs (for example a founding change) would be reported as
  "different from turn 1", or through the order-check message, because the turn-0 trace row is not compared. This does
  not apply to `2dc6af2`, whose turn-0 world equals this build's (measured).

## 10. AI behaviour you will see

- The AI founds at most one settlement per 350 years per polity (accepted pace, D-052 §2); you can found every turn.
- AI colonies are often far from their source (median 953 km measured, up to ~2,400 km): the AI reads the whole map
  until M6's known extent (accepted, D-052 §2).
- Everything is known: there is no fog of war in M5 (D-052 §5). F12 / `--dev` shows omniscient developer panels.

## 11. Map and performance

- The camera keeps a contain fit: in a wide window there is paper beside the square world, and the status and command
  bars do not fold (unruled, D-052 §14).
- With all six resource overlays on at World zoom the map alone measured 12–17 ms per frame headless; frame drops are
  possible on large worlds or slower machines. No GPU frame timing exists yet — please note any stutter.
- The overlay key reads each place as a 16 km node sampled at its centre (not stated in the key).

## 12. Not verified for this build

- **This push is the first CI run** on any commit after `2dc6af2`. Its `ui-artifact` run builds the zip
  (`sim-ui-win-x64-<sha>`) and its `windows-smoke` job runs the shipped exe headless; its `ci` run will show the two
  known calibration reds (§2).
- The native incompatibility dialog (§9) is untested on Windows; if it fails, the error still goes to stderr and the
  launch still ends cleanly.
- Measured headless on Linux only: the smoke gate, frame costs and every test count in the handoff report.

## 13. Not in the M5 playtest build (deferred by ruling)

Fog of war, scouts and visibility (M6) · the visual milestone: drawn buildings/institutions, Age terrain, richer unit
art (M6) · movement and Action Points (M7) · the Spine's M5 items budget, authority / bandwidth economy and laws-lite
(deferred; destination not yet ruled) · D-051 §1(a), §2, §3, §6 and primitive toolmaking (after the playtest).
