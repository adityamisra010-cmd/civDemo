# M4 DIRECTOR PLAYTEST GUIDE

`main` @ `dbef61a` · schema v24 · M4 certified.

---

## Launch

```
./scripts/bootstrap.sh          # only if the .NET 10 SDK is not already present
dotnet run --project Sim.Ui -c Release
```

Optional: `-- --seed 7`, `-- --size 256` (small, fast map), `-- --settlements 4`.

**Requires a real desktop with working OpenGL.** It is a MonoGame DesktopGL window. It will NOT run
over plain SSH, in a container, or under a bare Xvfb — the graphics device creation fails with
`NoSuitableGraphicsDeviceException`. Run it on your own machine.

Worldgen runs *before* the window opens, so expect roughly **2 seconds of black** at canonical
1024² size before anything appears. That is normal, not a hang.

## Starting state

With no arguments you get exactly this, deterministically:

- **seed 42**, canonical map size, **12 settlements**
- **turn 0, year −4000**
- **world population 5,140**; the selected settlement (Settlement 0) has **459 people** and **7,830 food**
- your Empire is `PolityId(1)` and it controls **all 12 settlements** — though **the UI never tells
  you this**; see "What you cannot see"

## Controls

| Input | Does |
| --- | --- |
| **Space** or the **End Turn** button | Advance one turn (10 years) |
| **Left-drag** or **W/A/S/D** | Pan the map |
| **Mouse wheel** | Zoom at the cursor |
| **Left-click** a settlement or its label | Select it |
| **Tab** | Cycle to the next settlement |
| Five sector sliders + **Apply labor split** | **The only order you can issue** |
| `territory overlay` checkbox | Toggle catchment territory |
| **Esc** | Quit |

## Turn loop

One press of Space = one turn = **10 sim years**. There is **no autoplay and no fast-forward** — a
100-turn session is 100 presses. Budget accordingly: reaching the horizons below is a lot of
pressing.

## The one decision you actually make

Select a settlement, set the five sector weights (farming / herding / extraction / crafting /
construction), press **Apply labor split**. The preview line shows the normalized split that will
actually run — weights are submitted as typed and normalized by the consumer, so the preview is
where you see what the sim will do. This emits real `SectorAllocation` orders through the same order
pathway the AI would use, and it is logged.

Everything else in the game is observation.

## Where to look

| Panel | Shows |
| --- | --- |
| `civ-sim` (left) | Clock, world population, selected settlement's population by cohort, food and last harvest, sector bars, grievance, per-class needs, the sliders, End Turn, build/seed/FPS footer |
| `Trade` (top centre) | Trade summary, per-flow lines, per-good rows |
| `Market` (lower right) | Prices |
| `Graphs` (upper right) | History |
| `Annals` (bottom centre) | The chronicle, scrollable, newest last |

## What the M4 systems look like from here

Measured on the default seed-42 game by driving the real session for 300 turns:

| System | Where you see it | What actually happens |
| --- | --- | --- |
| Population | HUD, Graphs | 5,140 → 4,513 by turn 20 → 8,339 by turn 100 → 31,374 by turn 300 |
| Food | HUD | 7,830 → **485** by turn 20, then recovers; harvests climb steadily |
| Grievance | HUD | 0.00 → 11.06 by turn 20 → ~15 thereafter. Drives nothing until M5 (D-021) |
| Trade | Trade panel | First flows appear around turn 100 (106 units); **episodic** — 3 units at turn 300 |
| Merchants | **nowhere** | They emerge only once a settlement's trade volume crosses 200; **not by turn 300** on this seed. Earlier measurement found them by turn 650 |
| Colonization | map / settlement count | **No new settlements by turn 300** on this seed — the count stays 12 |
| Happiness | **nowhere** | Measured ~100 for the whole run |
| Revolt | **nowhere** | Requires happiness at exactly 0, i.e. total deprivation. Did not occur |
| Control / Empire | **nowhere** | You hold all 12 settlements throughout |
| Resources | HUD, Market, Trade | Per-settlement, localized |

## What you cannot see, and cannot do

State plainly so the playtest is not spent hunting for these:

- **Happiness is invisible.** It exists, it is derived 0..100, and it weakly steers migration — but
  no panel displays it and no panel explains it.
- **Your Empire is invisible.** No polity, control, capital or territory-ownership display.
- **Merchants are invisible** even when they emerge.
- **You cannot queue construction**, though the order kind exists and is enforced in the sim.
- **You cannot found, direct or veto a colony.** Colonization is autonomous.
- **You cannot save or load a game.** End Turn writes a session ORDER LOG, which is not a savegame.
- **You cannot restart in-game.** Quit and relaunch with a different `--seed`.
- **There are no AI Empires** in a default game (`worldgen.aiEmpires` defaults to 0).

## Known non-features — absent by milestone ruling, not by oversight

No armies · no warfare or AutoResolver (player-side auto-win is the standing pre-army boundary) ·
no diplomacy · no technology tree, research or knowledge diffusion · no money, treasury or finance ·
no taxation · no politics or unrest valves (M5) · no water or clothing as modelled needs.
