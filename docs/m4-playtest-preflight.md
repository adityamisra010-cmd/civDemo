# M4 PLAYTEST PREFLIGHT

`main` @ `dbef61a` · schema v24 · compiled 2026-09-05.

**Nothing in this document was fixed.** It is a findings log. Categories 3–6 are deliberately left
as-is so the director encounters the M4 game honestly.

**METHOD, AND ITS ONE LIMIT.** The `Sim.Ui` window was never opened. It is a MonoGame DesktopGL
`WinExe`; this container's Xvfb has no GLX module, so graphics-device creation fails
(`NoSuitableGraphicsDeviceException`) under both plain Xvfb and a forced software-GL stack. **That
is an environment limitation, not a repository defect, and no repository change was made for it.**
Findings therefore come from (a) the shipping UI source, (b) the 151 `Sim.Ui.Tests`, and (c) driving
the REAL `UiSession` + `HudModel` — the same objects the window drives — headlessly for 300 turns on
the default seed-42 game. **Nobody has yet confirmed by eye that the UI draws and responds on a real
display; that is the first thing the playtest establishes.**

---

## 1. BLOCKER

**None found.** `Sim.Ui` builds clean (Release, 0 warnings), the session starts, and 300 End Turn
cycles ran without an exception or a stall.

The one caveat above is not a blocker in this category: it is an environment limitation of the
verification container, and the director's own machine is the intended host.

## 2. BUG

**None found.**

One piece of stale prose, recorded and deliberately NOT edited because it changes no behaviour:
`Sim.Ui/ViewModel/LaborOrderFactory.cs:21` says the player-Empire id is "a STANDING DEFAULT, not a
decision: nothing seeds a polity roster yet". Since M4-C, worldgen *does* seed the roster, and
`PolityId(1)` is now the founded player Empire rather than a placeholder. The value is correct; only
the comment is out of date.

## 3. UX PROBLEM

- **Reaching any M4 content costs hundreds of keypresses.** One press = one turn = 10 years, with no
  autoplay or fast-forward. First trade appears ~turn 100; merchants need ~650. That is 650 presses
  of Space to see the milestone's headline new class.
- **The first twenty turns read as decline.** Population 5,140 → 4,513, food 7,830 → **485**,
  grievance 0 → 11.06. The world then recovers strongly (31,374 by turn 300), but a new player's
  first impression is a settlement running out of food with no indication whether that is their
  fault, normal, or a loss condition.
- **The only order has no visible consequence in its own terms.** Applying a labour split changes
  sector bars and, eventually, harvest — but nothing reports "your order was accepted" or shows the
  order taking effect next turn.
- **Grievance is displayed but inert.** It moves and looks meaningful; it drives nothing until M5
  (D-021). Nothing on screen says so.

## 4. MISSING PLAYER AGENCY

Simulation exists; the player has no way to reach it.

- **Happiness (T4.13) is entirely absent from the UI.** It is derived 0..100, it weakly steers
  migration, and zero happiness triggers revolt — and there is not one reference to it anywhere in
  `Sim.Ui`. The player can neither read it nor inspect its factors.
- **Revolt and control loss are invisible.** No indication a settlement is deprived, at risk, or
  lost.
- **Empire identity and control are invisible.** No polity, capital, controlled-territory or
  ownership display, though `ControlRow` is authoritative underneath and the player holds all 12
  settlements.
- **Construction cannot be ordered.** `OrderKind.EnqueueConstruction` exists, is load-validated, and
  is enforced at the consumption point — but no UI emits it, so the whole construction queue is
  unreachable in play.
- **Merchants are invisible** even after they emerge.
- **No save or load.** End Turn autosaves the session ORDER LOG, which is not a savegame; snapshot
  save/load lives only in `Sim.Cli` and the tests.
- **No in-game restart.** Quit and relaunch with a different `--seed`.
- **Colonization is not a player action** — it is autonomous simulation behaviour.
- **Capital designation has no order pathway.**

## 5. OBSERVATION

- **The decision surface is one control.** The five-slider labour split is the entire set of choices
  a player makes. Everything else is camera, selection, display toggles and End Turn.
- **Happiness sat at ~100 for the whole 300-turn run**, so the revolt path — and therefore the
  uncontrolled-settlement pathway T4.5 depends on — is not something a normal session will reach.
  Reaching zero requires total deprivation of both food and housing.
- **No colonization occurred by turn 300** on the default seed; the settlement count stayed 12.
- **Trade is episodic, not a trend.** 0 units at turn 20, 106 at turn 100, 3 at turn 300. A player
  watching the Trade panel sees it come and go.
- **The world is materially comfortable.** Harvests climb steadily and population grows ~6× over 300
  turns; the pressure the M4 mechanisms respond to is mostly not present in a default game.
- **Determinism is player-visible**: the same seed reproduces the same opening exactly, which makes
  A/B comparison of a labour-split decision practical.

## 6. INTENTIONAL NON-FEATURE

Absent by ruling, not oversight — confirmed by inspection:

- no armies, no warfare, no AutoResolver (player-side auto-win is the standing pre-army boundary)
- no diplomacy
- no technology tree, research, or knowledge diffusion
- no money, treasury, banking or finance
- no taxation
- no politics or unrest valves — grievance accrues but acts only at M5 (D-021)
- no AI Empires in a default game (`worldgen.aiEmpires` defaults to 0; the seam is verified at
  1/4/8/50)
- no foreign trade activity — one polity controls everything founded, so every classified pair is
  Domestic or Unruled
- notables exist as a conservation surface with no production driver
- water and clothing are not modelled needs
