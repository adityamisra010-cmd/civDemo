# M4 PLAYER CONTROL INVENTORY

**Compiled 2026-09-05 against `main` @ `dbef61a`.** Every row below was read from the shipping UI
code and cross-checked against the 151 `Sim.Ui.Tests`, then exercised through the REAL `UiSession`
loop headlessly for 300 turns. **Nothing here is invented, and no control was added.**

**A LIMITATION STATED UP FRONT.** The window itself was never opened during this pass. `Sim.Ui` is a
MonoGame DesktopGL (`WinExe`) app and this container's Xvfb ships without the GLX extension module,
so no OpenGL context can be created (`NoSuitableGraphicsDeviceException`). That is an environment
limit, not a repository defect. Consequently: the CONTROLS below are derived from code and tests,
and the BEHAVIOUR below is measured through the same `UiSession`/`HudModel` the window drives — but
nobody has yet confirmed by eye that these widgets draw and respond on a real display.

---

## The controls that exist

| Area | Player Action | Available? | How to Access | Observable Consequence | Notes |
| --- | --- | --- | --- | --- | --- |
| Time | Advance one turn | **YES** | `End Turn [Space]` button in the `civ-sim` panel, or the **Space** key | Clock line advances one turn / 10 years; every panel refreshes | Both paths call the same `EndTurn()`. Space is suppressed while ImGui wants the keyboard |
| Time | Advance many turns | **NO** | — | — | No autoplay, no fast-forward, no "run N turns". One press = one turn |
| Camera | Pan | **YES** | Left-drag, or **W/A/S/D** | View moves | Drag is distinguished from click by a 4 px threshold |
| Camera | Zoom | **YES** | Mouse wheel | View scales about the cursor | |
| Selection | Select a settlement | **YES** | Left-click a settlement or its label | HUD switches to that settlement; sliders resync | A miss keeps the current selection |
| Selection | Cycle settlements | **YES** | **Tab** | Selection advances in settlement-id order | |
| Orders | Set the five-sector labour split | **YES** | Five `0..100` sliders + **Apply labor split** | Emits `SectorAllocation` orders into the real order log; the preview line shows the normalized split that will run | **This is the only order the player can issue.** Disabled until at least one sector is positive |
| Display | Territory overlay | **YES** | `territory overlay` checkbox | Draws catchment territory | Pure display toggle |
| Display | Clock / year | **YES** | HUD clock line | — | |
| Display | World population and settlement count | **YES** | HUD world line | — | |
| Display | Settlement population by cohort | **YES** | HUD | child / adult / elder | Selected settlement only |
| Display | Settlement food and last harvest | **YES** | HUD | — | Selected settlement only |
| Display | Sector split as bars | **YES** | HUD | Five labelled progress bars | Display only |
| Display | Grievance | **YES** | HUD | Single number | Display only; D-021 says it drives nothing until M5 |
| Display | Per-class needs satisfaction | **YES** | HUD needs blocks | Bound needs show values; unbound are labelled as not simulated | |
| Display | Market prices | **YES** | `Market` panel | — | Read-only |
| Display | Trade flows and per-good rows | **YES** | `Trade` panel | — | Read-only |
| Display | Graphs / history | **YES** | `Graphs` panel | — | Read-only |
| Display | Annals (chronicle) | **YES** | `Annals` panel, scrollable | — | Read-only |
| Display | Build identity, seed, FPS, camera, asset provenance | **YES** | HUD footer | — | |
| Session | Order log autosave | **YES (automatic)** | Happens on every End Turn | A session order log is written to disk | **Not a savegame.** It records orders, not world state |
| Session | Quit | **YES** | **Esc** | Window closes | |

## Controls that do NOT exist

Recorded because their absence is the useful information, not because they are wanted.

| Area | Action | State |
| --- | --- | --- |
| Empire | See which Empire you are, or which settlements you control | **ABSENT from the UI.** The order layer hard-codes the issuer as `PolityId(1)`; nothing displays polity, control or capital |
| Happiness | See a settlement's happiness, or its contributing factors | **ABSENT from the UI.** Zero references to happiness anywhere in `Sim.Ui` |
| Revolt | See that a settlement is near revolt, or that one has revolted | **ABSENT from the UI** |
| Merchants | See merchant emergence, merchant towns, or trade volume as a class signal | **ABSENT from the UI** |
| Construction | Queue a construction project | **ABSENT from the UI.** `OrderKind.EnqueueConstruction` exists, is load-validated and is enforced at the consumption point, but no UI emits it |
| Colonization | Direct or veto a colony founding | **ABSENT.** Colonization is autonomous simulation behaviour |
| Capital | Designate or move a capital | **ABSENT.** No order kind exists for it |
| Save / Load | Save or load a game | **ABSENT from the UI.** Snapshot save/load exists in `Sim.Cli` and the test suite only |
| Restart | Restart a world in-game | **ABSENT.** Quit and relaunch with a different `--seed` |
| AI Empires | See or interact with rival Empires | **ABSENT.** `worldgen.aiEmpires` defaults to 0, so none exist in a default game |
| Diplomacy, war, armies, technology, finance | — | **ABSENT by milestone ruling** — not M4 |

## Launch-time configuration

Passed on the command line, not in-game:

| Flag | Effect | Default |
| --- | --- | --- |
| `--seed N` | World seed | `42` |
| `--size PX` | Map size override (D-015 dev preview) | canonical |
| `--settlements N` | Starting settlement count override | canonical (12) |
| `--audit-assets [root]` | Headless art audit, then exits | — |
| `--generate-placeholder-assets [root]` | Writes missing placeholder art, then exits | — |
