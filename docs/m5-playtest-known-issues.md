# M5 playtest — known issues and limitations (Director's personal playtest)

**Status: for the Director's review. Not an M5 exit record, not a release note, not packaged.** Written 2026-10-12 with
the D-052 implementation pass on `m5w-integration` (two commits on `f0427d2`), pushed to the new branch
`claude/m5-playtest-d052` for the playtest build. `claude/new-session-yavghm` stays at `2dc6af2`. Governing record:
`docs/d052-m5-integration-audit-rulings.md`.

> **2026-10-14 — batch 2 (D-053), UNCOMMITTED on `m5w-integration`, not pushed and not in any build yet.**
> - The Director's 2026-10-13 batch adds the empire purse for construction and roads (§7), grain relief in a shortage
>   (§5), and granary effects (§14).
> - Items 3, 4 and 6 (incremental construction, building/road ratios, the explored-longitude map wrap) are NOT built:
>   CR-021 (§13).
> - Sections 3, 5, 7, 9, 12 and 13 are updated below. Everything else describes the pushed `de86ace` build unchanged.

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
| seed 7 | dev population 124,455 at turn 1000, above the envelope's 119,000 (batch 2: 124,483, from one relief settlement-turn; same assertion, still red) | the same; seed 7's migration (1.0083E-04, ×0.3497) is also below its floor, hidden because the population check runs first |

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
  - *Batch 2:* construction now draws on the whole empire's stock (§7). A stone-poor city builds as soon as the
    empire holds the stone. A settlement that is NOT in an empire still builds from its own stock only.
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
- **Batch 2 — grain relief in a shortage (D-053 §2).**
  - When one of your settlements cannot feed itself and its own store is empty, your empire's spare grain feeds it.
  - The hungriest settlement (largest unmet share of its need) is fed first; ties go by the previous turn's food state,
    then the settlement id.
  - Only grain moves. Donors are drawn nearest-first.
  - Consequence: one settlement of your empire can no longer starve, abandon farming into FAMINE, or revolt from hunger
    while the rest of the empire has spare grain. Famine now needs the whole empire to be short.
  - The urgency ranking uses the nominal deficit. Across food states it is not exactly starvation risk (D-053 §6 Q1).

## 6. Workshop and tools

- The Workshop is not offered in Age I: its tools cannot be made before bronze (the guard; the card shows the chain to
  Tin bronze, Age III). There is no primitive stone-and-wood toolmaking yet (approved direction, mechanism deferred;
  D-052 §11).
- INFERRED, not reproduced: in Age III+ a Workshop you queue can sit at the head of the construction queue for a long
  time if the settlement cannot hold 10 tools (farmers wear tools out), and there is no cancel order. If a queue stalls
  on a Workshop, note the settlement and turn.

## 7. Central stockpile — partly built (batch 2, D-053 §1–§2)

- **Construction and roads draw on the empire's stock.** A project in any of your settlements is paid from every
  settlement your empire controls: the building city first, then the others nearest-first, ties by id.
  - The blocker says "needs 20 stone (empire has 6)".
  - Projects are tried in settlement-id order each turn. Roads are paid after buildings from what is left. That fixed
    order stays until item 4 (ratios) is ruled (CR-021 §B).
- **Queued projects claim their crafted inputs.** A second Workshop is refused while the empire's only 10 tools are
  already claimed by a queued one, and the card names the claim.
- **Still per settlement:**
  - housing materials;
  - production recipe inputs;
  - tool equipping;
  - trade and prices;
  - migration's food check;
  - colony provisions;
  - livestock and fish.
  D-051 §6 stays open for these.
- **Construction is still built whole.** A project waits until the empire holds its full cost, then is built in one
  turn. There is no partial progress (item 3, CR-021 §A).

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
- *Batch 2:* sessions recorded by the `de86ace` build stop reproducing at the first turn where a construction waited on
  stock a sibling held, relief applied, or a granary acted. Measured: the seed-42 one-AI world first differs at turn 5. Expect
  any AI game, and any game where you built, to be refused. They are refused like any other incompatible session. Start a new game on a batch-2 build.
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

**Batch 2 items not built (CR-021, awaiting the Director):**
- incremental construction and partial progress (M4-D, schema v33);
- editable building/road resource ratios (a new per-empire table, v33, and a new order kind);
- the east-west map wrap gated by explored longitudes, with Satellites revealing the map (no explored-extent state
  until M6; a true cylinder reopens ADR-032 Final Ruling 3, D-015 and the T3.1(b) ocean ring).


Fog of war, scouts and visibility (M6) · the visual milestone: drawn buildings/institutions, Age terrain, richer unit
art (M6) · movement and Action Points (M7) · the Spine's M5 items budget, authority / bandwidth economy and laws-lite
(deferred; destination not yet ruled) · D-051 §1(a), §2, §3, §6 and primitive toolmaking (after the playtest).

## 14. Granaries (batch 2, D-053 §3)

- **Local farm output.** A granary raises its own settlement's CULTIVATED grain harvest: +3.2 % for the first,
  diminishing, never more than +5 %.
  - It has no effect while the settlement still gathers wild food, and the card says so.
  - Most early granaries stand in wild-food settlements, so expect no visible harvest change until farming arrives.
- **Empire-wide famine intensity.** Each granary in the empire eases the starvation-driving shortfall in every one of
  its settlements: −9.8 % for the first, never more than −25 %.
  - No grain moves; it stands for organised relief.
  - It acts only in a severe shortage or a famine. In 300 measured AI turns it never acted, and relief (§5) removes
    most single-city famines. You may not see it unless your whole empire runs short.
- **What it does not do.**
  - A granary does NOT increase storage. The store bound (1.5 years of demand) and spoilage are unchanged.
  - The settlement food line now says "grain store", not "granaries".
- **Unchanged.** The Age II "Communal store" milestone and the Age III levy relief.
