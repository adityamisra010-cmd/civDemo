# ADR-009 — UI stack: MonoGame DesktopGL + ImGui.NET (T1.7)

**Status:** accepted (director-sanctioned D-003 amendment, T1.7 session order).

## Decision

`Sim.Ui` is the game window (m1 spec §3: "UI is a view + order source,
single-threaded"). Its dependencies — the ONLY new packages in the repo, both
confined to `Sim.Ui`:

| Package | Version | Role |
| --- | --- | --- |
| MonoGame.Framework.DesktopGL | 3.8.5 | window, GPU, input (SDL/OpenGL) |
| ImGui.NET | 1.91.6.1 | immediate-mode debug UI |

The ImGui⇄MonoGame renderer binding is **vendored** (`Sim.Ui/ImGuiIntegration/
ImGuiRenderer.cs`, the standard community implementation, ~250 lines) rather
than pulled as a third package: it is small, reviewable, and the packet
sanctioned "implement or vendor".

`Sim.Core`, `Sim.Data`, `Sim.Cli`, `Sim.Tests` remain xUnit+FsCheck only.
View-model tests live in a separate `Sim.Ui.Tests` project (xUnit) so the core
test project keeps zero UI dependencies; they cover pure logic only (camera
math, palette, texture bake) and run headless in CI.

## No TFM pin

MonoGame 3.8.5 consumes cleanly from `net10.0` — the D-002 escape hatch
(net8 pin) was NOT needed. All projects stay on net10.0.

## The render-outside-determinism boundary

The determinism surface is `WorldState` + the systems that write it. `Sim.Ui`
sits strictly outside it:

- `Sim.Ui` references `Sim.Core`/`Sim.Data`; **nothing references `Sim.Ui`**.
  The compiler enforces that no sim code can read UI state.
- The UI holds a founded `WorldState` and READS it (bakes terrain once per
  ADR-008 immutability; renders; will submit orders through the T0.7 log from
  T1.8 — the log is replayable data, so player input enters the sim only
  through the already-deterministic pipe).
- Consequently floats, wall-clock frame timing, GPU/driver variation and
  Dictionary iteration in `Sim.Ui` cannot alter a single world hash, and
  `scripts/check-banned-constructs.sh` deliberately excludes the `Sim.Ui*`
  paths (noted in the script). Law 5 continues to bind everything the gate
  scans.

## Consequences

- Rendering: rasters bake once into a `Texture2D` (pure `TerrainBaker`,
  byte-deterministic per seed — tested); bilinear sampling gives the D-023
  "no visible tiles" smoothness at every zoom. Rivers are NOT baked (first
  visual gate bounced staircased raster rivers): they render as world-space
  vector quad-strips from the discharge-ranked polylines (`RiverMesh`, pure,
  width by rank), anti-aliased by 4× MSAA — chosen over per-vertex feathering
  as one flag on immutable geometry. The raster river layer in `TerrainSet`
  is untouched (sim/fertility data, hash-bound).
- CI publishes a win-x64 self-contained `Sim.Ui` zip as an Actions artifact
  (branch-runnable this packet; T1.10 formalizes per-main-merge + README).
  No content pipeline (MGCB) is used — textures come from raw bytes, ImGui
  uses its default font — so cross-compiling the Windows artifact from the
  Linux CI runner is a plain `dotnet publish`.
- Windowed/manual criteria (continent look, 60 fps feel, zoom smoothness) are
  the director's half of the D-023 Visual Gate — deliberately not automated.
