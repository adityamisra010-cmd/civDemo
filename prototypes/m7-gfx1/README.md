# prototypes/m7-gfx1 — M7 graphics prototype, slice set G1

Director-authorized, bounded graphics prototype (brief of 4 Nov 2026). **Not** in `Sim.slnx`, not in CI, no
`Sim.Ui` reference. Self-contained build (`Directory.Build.props` does not import the root one). Revert = delete
this folder. It does not decide O-14 or project boundaries.

## Build / test / run
```
dotnet build prototypes/m7-gfx1/M7Gfx.slnx -c Release
dotnet test  prototypes/m7-gfx1/Gfx.Tests/Gfx.Tests.csproj -c Release
dotnet prototypes/m7-gfx1/Gfx.Demo/bin/Release/net10.0/Gfx.Demo.dll --sysinfo
dotnet .../Gfx.Demo.dll --list | --capture <scene|all> --out DIR | --bench <scene> [--frames N] [--no-shadows] | --interactive
```
Headless Linux: `SDL_VIDEODRIVER=offscreen LIBGL_ALWAYS_SOFTWARE=1` — set automatically (native `setenv`) when no
display is present. Rendering tests SKIP when no GL device can be created. All timings measured in the container
are **SOFTWARE RENDERING (llvmpipe)**, never Victus performance.

Windows/Optimus: the game must run on the RTX dGPU. MonoGame DesktopGL cannot export `NvOptimusEnablement` from
managed code; until a native shim exists, set Windows Graphics settings → Gfx.Demo → High performance.
`--sysinfo` prints `IntelIGPU` when the iGPU is in use.

## Layout
| Project | Lane | Content |
|---|---|---|
| `Gfx.Engine` | E | MGFX-from-GLSL effects, forward renderer (shadow → HDR → ACES post), cameras, culling/LOD, capture, stats, sysinfo, headless host, scene registry |
| `Gfx.Demo` | E (+all) | exe; scenes in `Gfx.Demo/Scenes/` or any referenced `Gfx.*` library |
| `Gfx.Tests` | all | xunit; `[GfxFact]` for device tests |
| `Gfx.Proc`, `Gfx.Units`, `Gfx.Avatar`, `Gfx.Cinematics` | T, U, A | added by those lanes (add to `M7Gfx.slnx` + reference from Demo/Tests) |
| `scripts/check-gfx-determinism.sh` | E | integer-only scan of `Gfx.Proc/Authoritative/` (or dirs given) |
| `reports/` | all | lane reports |

Shaders: GLSL 1.20 packed into MGFX v11 at runtime (no content pipeline, ADR-009 raw-load premise); the writer is
adapted from the recon probe; vertex texture fetch uses the critic's pixel-stage-sampler workaround.
All procedural art here is placeholder-grade.
