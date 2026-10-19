# M5 playtest — known issues and limitations (Director's personal playtest)

**Status: for the Director's review. Not an M5 exit record, not a release note, not packaged.** Written 2026-10-12 with
the D-052 implementation pass on `m5w-integration` (two commits on `f0427d2`), pushed to the new branch
`claude/m5-playtest-d052` for the playtest build. `claude/new-session-yavghm` stays at `2dc6af2`. Governing record:
`docs/d052-m5-integration-audit-rulings.md`.

> **2026-10-15 — batch 3 (D-054), UNCOMMITTED on top of batch 2, not pushed and not in any build yet.**
> - The Director's 2026-10-14 instruction adds three things:
>   - incremental construction with a cancel order (§7);
>   - each Empire's building/roads material split (§7);
>   - a true east-west cylindrical world in worldgen, simulation and rendering (§11).
> - These are batch 2's items 3, 4 and 6, which the batch-2 entry below records as not built. They are built now, the
>   wrap without exploration gating or Satellites (out of scope by the instruction).
> - Sections 6, 7, 9, 11, 12 and 13 are updated below with *Batch 3* lines.
> - §15 lists batch 3's known issues, and §16 is the **manual playtest checklist** for what no headless test can see.
> - Records: `docs/d054-batch3-incremental-construction-allocation-cylinder.md` (§10 holds the Director's questions)
>   and ADR-039.

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
  - *Batch 3:* a cancel order now exists. Each committed queue row has a Cancel button, and a head with work in it asks
    you to confirm ("Lose it"), because cancelling refunds nothing. A Workshop also no longer needs all 10 tools at
    once: it takes them as they come, keeping what has gone in.

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
  - *Batch 3 (D-054 §1): superseded.* Construction is now INCREMENTAL.
    - A project starts as soon as it is first in its settlement's queue and there are builders and the first unit of
      each material.
    - Each turn it takes what the builders can use and the empire holds, materials first. What has gone in is never
      lost while it waits.
    - The queue shows "16 of 40 timber, 8 of 20 stone and 0.8 of 2 adult-years have gone in", the reason it is
      waiting, and the order it is served in: oldest order first, across the empire.
    - Cancelling a part-built project forfeits what went in. There is no refund (D-054 §10 Q1).
- *Batch 3 (D-054 §2):* **the building/roads material split.** A "Materials for building and roads" block sits between
  Construction and Roads. On a turn you order road works, building work is guaranteed its share of each eligible good,
  and road works get the rest. Whatever one side does not need, the other may use.
  - The default is "building first": road works get only what building work leaves, the same as batch 2. The AI never
    changes it.
  - Numeral-free eras show shares in 10; numeral eras show percentages.
  - Dwellings are not under the split. They take their own settlement's timber first.

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
- *Batch 3:* every session recorded by a batch-2 build is an older save schema (v32; this build is v33).
  - **It resumes on the FLAT map it was played on**, at any size (250 px included), and its orders are replayed
    without the trace check. Every batch-2 world was flat; the cylinder arrived with v33.
  - The "RESUMED SESSION" notice reads: "Recorded under save schema v32; this build is v33." / "Replayed from its seed
    and orders on the flat map v32 used (D-008)." / "The saved trace was not checked against this replay." / "The
    world may differ from the one originally played."
  - Measured on a real batch-2 session (seed 42, two settlements founded by order): it resumes into exactly the world
    the batch-2 build played, 14 settlements at turn 8.
  - **It may still differ** where batch 3's construction rules act: a project that waited under batch 2's
    whole-or-wait rule is now built piece by piece on replay (D-054 §10 Q9). If you see that, note the session.
  - `sim inspect` of such a session also rebuilds it flat. It says "trace not checked: …" instead of a reproduction
    verdict, and its "reproduce with:" line points to `sim inspect` itself, because this build's `sim replay` always
    founds the cylinder.
  - CLI `.civsnap` saves of schema v32 load FLAT too: `sim hash` / `sim diff` migrate them explicitly and print a
    one-line migration note on stderr.
- `--size` maps: founding a settlement (player or AI) on a non-canonical map now reads deposits from that map's own
  cells. Before this pass it read the wrong cells, and could crash End Turn for sites in the bottom rows. That crash was
  measured: seed 1, `--size 256 --ai-empires 1`, the AI's founding at turn 179. A `--size` session recorded by an
  earlier build therefore stops reproducing from its first colony founded after turn 0, and is refused like any other
  incompatible session. Canonical-size sessions are unaffected by this fix.
- *D-056 (2026-10-18), cancels in old sessions.*
  - **What changed.** A cancel now records which project it cancels, not only its queue slot (order kind 13). Sessions
    recorded before this build hold the old slot-only cancels (kind 11). Slots are reused, so in a world that has
    drifted from the one played, such a cancel could remove a different project.
  - **What this build does with them.** It applies an old cancel only when the session's trace proves the world is the
    one played at that turn. Otherwise the resume is refused (exit code 2, the message and the dialog), naming the turn
    and the build that recorded the session. A session without old cancels resumes as before.
  - **Your files are protected.** The session files are never changed by a refused resume. After any resume the trace
    could not check turn by turn, the first save copies the recorded log, trace and chronicle aside first, as
    `*.as-recorded.*`. Back up a session folder anyway before resuming it in a new build.
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
  - *Batch 3 (D-054 §3): superseded for the shipped world, which is now an east-west CYLINDER.*
    - Pan east past the edge and you arrive in the west, and the reverse. North and south still stop at the poles.
    - Cities, routes and roads cross the seam, and nothing is drawn twice.
    - The zoom floor is the window's width / the world's width, so the map always fills the window. Opening or closing a
      panel no longer re-zooms.
    - The game opens centred on your first settlement.
    - **The whole world can no longer be seen at once.** At the floor about 47–54 % of its height is visible (53.8 % at
      1280×800, 46.7 % at 1920×1009). A minimap is queued, not built (D-054 §10 Q5).
    - The level of detail now changes at deeper zooms: at the default 1280×800 window, "to Regional" takes 3 wheel
      notches from the floor, where it took 1.
- *Batch 3:* a `--size` the cylinder cannot take (not a multiple of 4 px, or under 12 px) is refused at launch, exit
  code 2, with "error: this world is a cylinder (worldgen.json topology.wrapX), which needs a map size the lattice
  stride divides into at least 3 nodes: size 250 px, stride 4 px. The cylinder's own rule is a multiple of 4 px, at
  least 12 px — the nearest sizes the cylinder takes are 248 px and 252 px." (shown for `--size 250`).
  - When a size the message names is below 124 px, it adds one sentence. For `--size 8` the message ends "— the
    nearest size the cylinder takes is 12 px. That rule is the cylinder's alone: founding also needs room to site every
    settlement, and at the default settlement count, settlement siting can refuse maps this small, with its own
    message."
  - Small sizes the cylinder takes can still be refused by settlement siting with the default 12 settlements: measured
    (2026-10-15, final-round fix 3) for every size the cylinder takes up to 88 px on seeds 42, 1 and 7, and on some
    seeds up to 120 px (seed 42 at 120 px). Every size from 124 to 160 px founded on seeds 1–20 and 42, and 248, 252
    and 256 px found and run.
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
- *Batch 3:* no batch-3 commit exists, so no CI or Windows build has run on it. The GPU side of the seam can only be
  checked by eye: the U-wrapped terrain sampler, the rivers drawn at each copy, and the 2000-texel sheet of
  `--size 1000`. See §16.

## 13. Not in the M5 playtest build (deferred by ruling)

**Batch 2 items not built (CR-021, awaiting the Director):**
- incremental construction and partial progress (M4-D, schema v33);
- editable building/road resource ratios (a new per-empire table, v33, and a new order kind);
- the east-west map wrap gated by explored longitudes, with Satellites revealing the map (no explored-extent state
  until M6; a true cylinder reopens ADR-032 Final Ruling 3, D-015 and the T3.1(b) ocean ring).

*Batch 3 (D-054):* the first two are now BUILT (§7), and the third is built as an UNRESTRICTED cylinder (§11). Its
exploration gating and Satellites stay out of scope by the Director's instruction: "EXPLICITLY OUT OF SCOPE FOR NOW".
There is still no fog, no known extent and no unit movement (M6, M7).


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

## 15. Batch 3 (D-054) — known issues

- **Two calibration tests stay RED (§2), with moved values.**
  - Seed 42: dev migration 8.35755E-05 (was 8.71773E-05).
  - Seed 7: dev population 123,950 (was 124,483).
  - The dev worlds are cylinders now. No frozen reference was touched.
  - The nightly corridors drift as well: density target band 17 → 16 of 20 seeds, and the quarantined migration breach
    77.66 % → 80.83 %. D-054 §6 has the numbers.
- **Cancelling refunds nothing.** Whatever has gone into a project (materials and builders' work) is lost when you
  cancel it. The UI warns before a part-built project is cancelled (D-054 §10 Q1).
- **The AI is more cautious than you.** It orders a building only into an idle queue, and only when the empire already
  holds the whole remaining cost.
  - It cancels its own project when the settlement can no longer build it: no adults left, or a university whose town
    can no longer sustain one. That includes after a bad harvest lowers the food surplus, so an AI may abandon a
    part-built university (D-054 §10 Q3, Q6).
- **The split's default is "building first"** (100), which is batch 2's behaviour. The AI never changes it (D-054 §10
  Q2).
- **The books do not separate building materials from road materials.** Both are booked as construction materials
  (D-054 §10 Q12).
- **The whole world is never on screen at once** (§11). A minimap is queued, not built.
- **Level of detail on the cylinder is keyed on the window's width** (D-054 §10 Q4).
- **The seam is checked headless only on the baked parchment's row mean.** A partly faded seam would pass that test, so
  the eye is the judge (§16; D-054 §10 Q10, Q11).
- **Old sessions.** Batch-2 sessions resume on the flat map they were played on (§9), but under batch 3's rules, so a
  game where construction waited can come back different (D-054 §10 Q9). This build's `sim replay` cannot replay them;
  `sim inspect` and the game's resume can.
- **Slower turns on the shipped world.** About +14 % per turn, from path-building on the cylinder's different road
  network (measured over 60 turns). The same flat world costs about +1 %.
- **No unit movement exists** (M7). "Movement across the seam" means the existing travel: catchments, migration, trade,
  colonisation reach, the AI founding flood, route previews and path-building. All of it wraps.
  - On the shipped worlds, only seed 16 has settlements whose shortest route crosses the seam. A migrant crossing it is
    pinned there with trade and recipe knowledge given at the start. No trade across the seam was seen in 300 turns
    on any of the seven shipped seeds whose land crosses it (D-054 §3, §10 Q18).
- **A younger project can take what an older, waiting one still needs** (D-054 §10 Q17). Work is served oldest order
  first only among projects that can progress this turn. If the older head has no builders this turn, a younger head
  elsewhere in the empire may use the materials the older one was waiting for.

## 16. Manual playtest checklist (batch 3) — what no headless test can see

Launch from a terminal so the seed and size can be chosen, for example `app\Sim.Ui.exe --seed 16 --ai-empires 1`.
Seeds with land across the seam: **16** (a pinned route crosses it between its settlements 3 and 6) and **6** (land and
a river cross it). Note the seed, size, turn and window size of anything odd, and take a screenshot.

1. **Pan across the seam, both ways, on the canonical world (1024 px), seed 16 and then seed 6.**
   - Hold D past the eastern edge until you are back in the west, then hold A the other way.
   - Look for a vertical **seam line**, a **smear** (stretched or clamped texels), or a **crease** in the paper or
     washes at the cross-fade band (the last 32 world px before the seam).
   - Check the **rivers** (seed 6), **coasts, roads, territory and the resource overlays** as they cross.
   - Nothing may be drawn twice or cut off.
2. **Repeat on `--size 1000`** (for example `--seed 16 --size 1000`). This world's terrain sheet is 2000 texels wide, which
   is not a power of two; whether the graphics driver wraps it correctly is untested. Look for the same seam line or
   smear.
3. **Zoom out fully at the seam.** The floor should show the world once across the window, never two copies.
   - Open and close a side panel: the zoom must not change.
   - Wheel in to Regional and Settlement and back.
4. **Click a settlement standing at or across the seam** (on either side). The right card must open, and its route
   preview must take the short way across the seam.
5. **A part-built project.**
   - Order a Granary where the empire is short of stone, and End Turn a few times.
   - The queue should show "… of 40 timber, … of 20 stone … have gone in", a "waiting for …" reason, and the "served
     oldest order first" line.
   - In the numeral-free eras no "%" may appear anywhere in the construction block.
   - Check the "Last turn: … went in" line after an End Turn.
6. **Cancel a part-built head.**
   - Click Cancel on the head with work in it. It must turn into "Lose it" with a line naming what will be lost, and only
     the second click cancels.
   - After End Turn the entry is gone, and the next one is head with nothing in it.
   - A project ordered this turn shows no Cancel.
7. **The allocation control ("Materials for building and roads").** It appears with the Roads block, once a road class
   is known.
   - **In a numeral-free era (Ages I–III):** the share tally reads "building work keeps N shares in 10, road works M",
     with no "%" anywhere. Both ends (all to building, all to roads) are reachable. Set appears only when the draft
     differs, and the queued line reads "Set this turn: …".
   - **In a numeral era (Age IV on):** the readout below the control reads "Building N% · Roads M%", and the control
     row itself carries no number. Check that nothing reads as a building share sitting beside "Roads". From Age VII
     the control is a slider.
   - Order a road the same turn, and check the construction block's "may use up to" line and the roads preview's "after
     building work, about …" line.
   - Quit with a part-built head and a split set, then resume with `app\Sim.Ui.exe --resume runs\<session-dir>`. The
     split, the head's progress and the queue must come back unchanged, with no resume notice.
8. **WASD at the seam.** D moves east, A moves west, and W and S stop at the poles. The HUD's camera x never shows the
   world width (it wraps to 0).
9. **An old (batch-2) session, if you have one.** Resume it with `app\Sim.Ui.exe --resume runs\<session-dir>`.
   - The game must open with the "RESUMED SESSION" notice, whose second line reads "Replayed from its seed and orders on
     the flat map v32 used (D-008)."
   - The map must be FLAT: panning east stops at the edge, with no wrap, and the settlements are where you left them.
   - Note anything that looks different from how you left it, especially a building that is now further along (§9).
10. **A size the cylinder refuses.** Launch with `--size 250`. No game window may open. The error dialog ("civ-sim:
    this world cannot be created") and stderr must show the "this world is a cylinder …" message quoted in §11, and
    the launch must end with exit code 2. The dialog is not yet observed on Windows (§12).

---

## Dated note — 2026-10-17: batch 4 (D-055) — known issues and playtest checklist items (appended at the file's end; the sections above are unchanged)

Batch 4 (the Director's batch-4 instruction) is UNCOMMITTED on top of batch 3, not pushed and not in any build yet.
Records: `docs/d055-batch4-reservations-attribution-allocation-ci-persistence-pathfinding.md` (D-055) and ADR-040. This
note sits at the file's end because D-054 §10 Q19 (where dated notes may sit) is unruled; it supersedes the lines of §15
and §16 it names.

### Known issues (batch 4)

- **§15's last bullet no longer holds** ("A younger project can take what an older, waiting one still needs", D-054 Q17).
  Every project at the head of its settlement's queue now SETS ASIDE what it still needs from the empire's purse, oldest
  order first, even while it waits (for builders, for another material, for its town to sustain it). Younger projects
  and road works may not use it. It is released only when the project can never be built (its project is unknown, its
  settlement has no working adults, or it is an institution the game has no institutions configuration for) — and at once
  when you cancel it.
- **Head-of-line wait.** One long wait now holds up every younger project of the same purse. Measured: in the save/load
  battery's variant a younger granary waits 20 turns (200 years at 10-year turns) behind the capital's university, where
  batch 3 built it at once. The waiting project's line names the holder, why the holder waits and the remedy: give
  Building a share in Labour at the holder's settlement, or cancel the holder (a cancelled holder's set-aside is released
  in the same End Turn; cancelling still refunds nothing). Under a split below 100, road works may use what the split
  does not grant building work.
- **Road works wait too.** A road order is paid only from what building work neither uses nor sets aside (the default
  split, 100, is "building first"). In the scripted save/load battery a waiting university set aside the player's whole
  timber, so two road orders paid nothing until it was built.
- **Priority is the order's age, not progress.** When a project completes or is cancelled, the next one in that
  settlement's queue takes its own order's place: if it was ordered EARLIER than a part-built project waiting elsewhere, it
  goes first and may use what that project had set aside.
- **One word for it.** A project's reservation is always "set aside"; "held" keeps its older meanings (what your
  settlements hold, and "… held for road works …" under the split). The settlement view's own line for its head still
  reads "waiting for stone" without naming the holder; the construction queue's line and the project card name it.
- **Old (batch-3, schema-v33) sessions** resume by replay under batch-4 rules, with the resume notice and without the
  trace check, so the game can come back different from the turn a set-aside first binds. Such a resume can be REFUSED
  when the replay no longer founds a colony a later recorded order names (ADR-034), and a recorded Cancel (settlement,
  slot) can remove a different queue entry if the slots shifted (D-055 §9). Batch-2 sessions behave as §9 and §15 say.
- **Speed.** §15's "about +14 % per turn" is corrected: the cause was seed 42's path search (its shared road target lies
  farther away on the cylinder), not the road network; across seeds 1–10 the cylinder is not slower. Batch 4's
  byte-identical pathfinding changes made the 60-turn bench 44–54 % faster per seed (measured on batch 3 + those changes);
  the hold rule adds about 0.15 ms a turn to construction. The final build's bench is the integration's (D-055 §8).
- **Not verified for this build:** everything batch 4 measured so far ran on scratch clones (D-055 §8); the allocation
  CI leg has not run on GitHub (nothing is pushed); the GPU seam remains the manual checklist's (§16).

### Playtest checklist additions (batch 4) — continuing §16

11. **A project that waits holds its materials.**
    - Order a Granary at settlement A and, in Labour, give A's Building no share; End Turn.
    - Order a Granary at another settlement B that has builders, in a turn when the empire holds no more stone or timber
      than A still needs; End Turn.
    - B's queue line must read like "waiting for stone: … set aside for A's Granary, ordered earlier (turn T), ahead in
      the building order - it has no builders (give Building a share in Labour at A, or cancel it)".
    - A's row must add, in the soft ink, "sets aside … while it waits; projects ordered later wait for it".
    - The EMPIRE view's Realm block must show "Set aside for projects: …" for your own empire only.
    - Give A builders back: A's project progresses the next End Turn. Or cancel A ("Lose it"): B uses the units in that
      same End Turn.
12. **Plates and cards.** With A still waiting, the Build plate for a third project must read "will wait for: … (empire
    has N; M set aside for earlier projects)", and the project card's note must name A's project ("M set aside for A's
    Granary, ahead in the building order").
13. **Roads beside a set-aside** (once a road class is known). Order a road while A waits. The roads preview must read
    "after what building work uses or sets aside, about N of the M … needed is left: …" and, in a soft caption line,
    "… is set aside for A's Granary; road works may not use it". Set the split below 100 (the Materials control): A's row
    must add "; the split lets road works use the rest".
14. **Numeral-free eras (Ages I–III):** no "%" in any of the new lines.
15. **Quit and resume** with A waiting and a split set (`app\Sim.Ui.exe --resume runs\<session-dir>`): the set-aside lines,
    the queue and the split must come back unchanged.
16. **The words.** Nowhere may a project's reservation be called "held" or "reserved"; "held" appears only for what
    settlements hold and for the split's road share.

## Dated note — 2026-10-18: D-056 / D-057 — resuming sessions, and checklist items (appended; the sections above are unchanged)

- **Sessions of this build** (save schema v34, trace beside the manifest) resume exactly as before: the replay is
  checked turn by turn.
- **An older session** (v32 or v33) resumes with its four-line notice, unless a recorded order would be applied to a
  settlement or polity the replay cannot show is the one it was given for. That happens when a colony or revolt-born
  polity was founded after the replay stopped matching the recording.
  - **On refusal:** the game names the order, the turn where the replay first differs, and the remedy. It exits before
    any window opens and changes no file.
  - **What survives:** of the three real older sessions tried, all resume or inspect: the batch-2 fixture, the batch-3
    fixture and a 300-turn de86ace session (D-057 §1).
- **A session whose trace file was deleted** resumes only if its orders name no settlement; otherwise put the trace back.
- **A damaged session file** (a cut-short log, a malformed trace line, a missing manifest) is reported in a dialog
  naming the file, with exit code 2. It no longer crashes the launch.
- **`sim inspect` of an older session** says how far the replay matches the recording ("matches through turn P and
  differs from turn F on").

### Playtest checklist additions (D-056 / D-057) — continuing §16

17. **Cancel and resume.** Order two projects at one settlement and cancel the second; save, quit, `--resume`. The
    cancelled project must stay gone, and the other must keep its progress.
18. **Found a colony, order it, resume.** Found a settlement, set its Labour, save, quit, `--resume`. The colony and its
    Labour must come back unchanged, with no notice (a session of this build).
19. **Deleted trace.** Copy a session directory, delete its `trace-*.csv`, and `--resume` the copy. Expect the refusal
    dialog naming the trace file. The original directory is untouched.
20. **Damaged log.** Copy a session directory, truncate its `orders-*.bin` (any length), and `--resume` the copy. Expect
    the "one of its files cannot be read" dialog, not a crash.

## Dated note — 2026-10-19: Director's playtest of build 231485e (appended; the sections above are unchanged)

- **Continuing a game (D1, fixed in the next build).**
  - **The defect:** `Play civ-sim.cmd` always starts a NEW game.
  - **The fix:** the zip root now also carries `Continue last game.cmd`, which runs `app\Sim.Ui.exe --resume runs`
    and so reopens the game started last (the newest `session-*.json`).
  - **If you started a game by accident,** "Continue" reopens that one. Resume a specific game from a terminal:
    `app\Sim.Ui.exe --resume runs\session-<stamp>.json`.
- **Results:** PT-01 (launch, 68 turns played), PT-02 (resume at turn 68: same turn, population, food, Labour and
  research) and PT-03 (three projects, cancel the middle one, resume) passed on the Director's machine. The playtest
  was closed there by the Director.
- **Feedback recorded for decisions:**
  - a resources view (`docs/m5-resources-view-proposal.md`);
  - projects building one at a time per settlement (same file, §5);
  - the one-file session export and telemetry encoding (`docs/m6-session-export/`).
- **Chronicle wording seen in the session (open):**
  - "ran short: 0 of every hundred measures of need went unmet" — the share rounds to 0;
  - "1 masters and hands" — no singular.

## 2026-10-19 — D-058: projects completed one per turn (the Director's ruling), fixed LOCALLY on `b5-cons`

- **The defect.** Three granaries ordered at one settlement completed one per turn even when a turn's capacity could have
  finished more (PT-03 feedback, `docs/m5-resources-view-proposal.md` §5).
- **The fix** (`docs/d058-build-one-at-a-time-capacity-carries-within-the-turn.md`). Construction stays one project at a
  time, but a turn's leftover building capacity now continues down the queue in the same End Turn. Example: with nine
  ordered and capacity for seven and a bit, the next turn shows 7 built, 1 under construction and 1 not started.
- **Compatibility.** A session recorded by an earlier build in which a settlement could have finished two projects in
  one turn now diverges at that turn, and its resume is refused with the incompatibility dialog. The Director's 68-turn
  session of 2026-10-19 is NOT affected: its replay is identical under both rules on all 68 turns.
- **Still open.** The queue gives no in-turn forecast of how far the continuation will get.
