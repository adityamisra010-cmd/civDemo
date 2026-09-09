# T4.18 — THE CONTROL AUDIT AND THE GAME SCREEN

Branch `t4.18-playtest-followup`, cut from the M4 playtest baseline `98f25c1`.
**Not merged to main.** Every change in this record is in `Sim.Ui` and
`Sim.Ui.Tests`; `git diff` against the baseline touches no other project.

---

## §1 THE CONTROL AUDIT

Every player-facing control on the M4 screen, classified.

| control | class | verdict |
| --- | --- | --- |
| five sector sliders (farming, herding, extraction, crafting, construction) | **3 — FIXED-SUM ALLOCATION** | **was wrong.** Implemented as five independent 0..100 values with an unconstrained sum. Fixed. |
| five sector progress bars | 2 — dependent output | correct, but it sat directly above the sliders showing the same five quantities. Moved below an explicit "currently running" heading so control and consequence are not adjacent and interchangeable. |
| "applies as 19% / 19% / …" preview line | 4 — informational | **deleted.** It existed only because the sliders did not sum to 100; see below. |
| "Apply labor split" button | 1 — independent input (confirm) | correct. One decision, one batch in the order log. Kept. |
| "End Turn" button | 1 — independent input | correct. Kept, moved to the command bar. |
| "territory overlay" checkbox | 4 — informational / view toggle | correct as a checkbox — it is a view state, not a slider. Kept, moved to the command bar. |
| market good row (Selectable) | 4 — informational selection | correct. Read-only; no widget in Market emits an order. Kept. |
| grievance, needs, food, population lines | 2 — dependent output | correct: text, never controls. Kept. |

### §1.1 The one defect, stated as the interaction it produced

The five sliders were five **independent** values whose sum was unconstrained,
and the simulation normalized whatever arrived. So the panel could read
20/20/20/20/20 — an apparent 100% — or 97, or 104, with nothing on screen
distinguishing the three, and a caption underneath predicting what the numbers
would mean once divided through:

```
farming 55   herding 15   extraction 10   crafting 12   construction 8
applies as 55% / 15% / 10% / 12% / 8%
```

That is the packet's *"moving this to X will result in Y becoming Z"* exactly. It
is a calculator with a preview, not a control: the number the director set was
never the number that ran, and he had to read a second line to find out what he
had actually ordered.

### §1.2 A second defect, which needed no player at all

The panel snapped to a settlement's current split by rounding each share
independently:

```csharp
_sectorWeights[s] = (int)Math.Round(Sectors.Share(allocation, s) * 100.0);
```

Five shares of 14.28% each floor to 14, for a total of 70 before any remainder is
dealt; uneven splits round down in several places at once. **The panel could
therefore open reading 99% — or 101% — before the director had touched
anything.** Found by inspection during the audit, not by a failing test; now
pinned by one.

---

## §2 WHAT WAS IMPLEMENTED

`SectorAllocationModel` (pure view-model, 17 tests) makes the five weights a
fixed-sum allocation of exactly 100:

- **Moving one rebalances the others proportionally.** A sector holding half the
  remaining labour gives up half of what has to be found — the behaviour that
  reads as *the others make room* rather than *a slider I never touched jumped*.
- **The invariant holds after any move and any SEQUENCE of moves.** Totals like
  97 and 104 arise from a series of adjustments, so the test drags every slider
  in turn, repeatedly, and asserts 100 each time.
- **Two degenerate cases are handled explicitly**: reducing a sector that held
  everything (nothing to scale up — the freed labour spreads evenly), and pushing
  one sector to 100 (the others empty).
- **Deterministic redistribution.** Integer arithmetic, largest-remainder
  rounding, stable tie-break on the lowest sector index. Ties are the *common*
  case here — an even 20/20/20/20/20 split makes every remainder identical — so
  the tie-dense test the constitution requires is not a corner case but the
  central one: moving farming to 30 must give 30/18/18/17/17, the two lowest
  indices rounding up, every time.
- **The snap from the world's shares uses the same rounding**, closing §1.2.

**The prediction line was deleted because it became an identity.** With the
weights summing to 100, the normalized share the sim runs IS the number on the
slider — asserted through the sim's own `Sectors.Share`, not by argument.

**The order payload is unchanged.** D-032 weights are still submitted *as typed*
and still normalized by the consumer. This constrains what the director can
type, not what the order means: a sum-100 allocation was always expressible, the
UI simply could not hold itself to one. No system, equation, schema or constant
moved.

---

## §3 THE SCREEN — BEFORE AND AFTER

### Before

Five windows, permanently open, proven non-overlapping:

```
civ-sim  440×776   Graphs 440×448   Market 440×316   Annals 352×228   Trade 352×300
```

**863,456 px of panel over a 1,024,000 px viewport — 84.3%.** The panels sit *on*
the map, so what was left for the world was the gaps between them. The HUD alone
was a third of the width at full height, always. Every subsystem shouted at equal
volume whether or not the director was thinking about it.

The old gate proved the panels did not overlap. That is the wrong success
criterion: **it certifies a full screen as correct**, which is how the layout got
that way.

### After

```
┌──────────────────────────────────────────────────────────┐
│  turn 12 · year 120      pop 4,048 · 12 settlements      │  status, 48px
├──────────────────────────────────────────────────────────┤
│ ┌────────────┐                                           │
│ │ Bashet     │                                           │
│ │ 663 souls  │              W O R L D                    │
│ │ food 1,204 │                                           │
│ └────────────┘                                           │
├──────────────────────────────────────────────────────────┤
│ [End Turn] [POLICY][ECONOMY][POPULATION][MARKET][ANNALS]  │  command, 56px
│            [TRENDS][MORE]              ☑ territory        │
└──────────────────────────────────────────────────────────┘
```

- **The world gets the screen: 87% of the viewport with nothing open**, over half
  with a section open. Both pinned as tests.
- **One contextual panel**, right column, opened from the command bar. Clicking
  the open section closes it, so a clean world is one click away.
- **`Section.None` is a real state** — something the old layout had no way to
  express, since five windows were always up.
- **Bars are chrome, not windows**: positioned every frame, no title bar, no
  move, no resize. The old panels were `FirstUseEver` defaults the user could
  drag anywhere, which is why panel overlap was a thing the gate kept
  re-discovering.

### Where everything went

| was | now |
| --- | --- |
| HUD clock + world totals | status band, always visible |
| HUD settlement title, population, food | selection card, floating over the map |
| HUD sector sliders + apply | **POLICY** |
| HUD sector bars, food, grievance | **POLICY**, under "currently running" |
| HUD per-class needs, population, grievance | **POPULATION** |
| HUD debug footer (build, seed, camera, art, fonts) | **MORE** |
| Trade window | **ECONOMY** |
| Market window | **MARKET** |
| Annals window | **ANNALS**, opening on the recent end with a toggle for all |
| Graphs window (six 300×56 thumbnails) | **TRENDS**, one graph at 356×220 with metric and scope selectors |

**Nothing was removed.** Every line the five windows drew still exists behind the
section that owns it. A test asserts every `Section` appears in the navigation
roster exactly once, so a section cannot exist and be unreachable.

---

## §4 WHAT WAS DELIBERATELY NOT DONE

Each of these would have required a simulation or model change, which the packet
forbids. They are recorded as dependencies, not implemented:

1. **A "happiness impact" readout in Policy.** The packet's illustrative Policy
   panel shows taxation, effective rate, administrative reach, legitimacy and a
   happiness delta. **Those are M5 mechanisms** — they live on `m5-full-build`
   and merging them here was explicitly forbidden. M4's Policy panel therefore
   contains the controls M4 actually has. When M5 is ruled on, its tax slider
   joins this panel as a second independent input; the panel was built to take it.

2. **A predicted consequence of a pending labour split** ("this split will yield
   N grain next turn"). M4 has no forecast surface, and building one means either
   a second implementation of the production equations in the UI — which would
   drift from the sim silently — or a speculative step of the simulation. Both
   are model changes. The panel shows what is *currently running* beside the
   pending controls instead, which is a true reading rather than a projection.

3. **A critical-survival indicator in the status band** beyond population, food
   and settlement count. Deciding what counts as "critical" is a model ruling
   (what threshold, on what quantity, by what mechanism), and inventing one in
   the UI would put a judgement about the simulation into the view layer.

4. **Save/load from the UI.** Unchanged and still unwired; snapshot save/load
   exists in the kernel. Out of scope here, still recorded under missing player
   agency.

5. **The cross-platform determinism divergence (CR-013).** Found while verifying
   this branch, using the T4.17 trace on the director's own session: the same
   commit produces the same population, food and settlement counts on Windows
   and Linux but hashes differently from turn 2. It is a kernel/law-5 matter,
   not a UI one, and fixing it would mean changing a simulation expression —
   which this packet forbids. Written up as
   `docs/adr/cr-013-cross-platform-determinism.md`, open for a ruling.

---

## §5 VERIFICATION

- `git diff` against the baseline touches **only `Sim.Ui` and `Sim.Ui.Tests`**.
  No simulation project changed, so no constant, equation, schema, golden,
  corridor or quarantine could have moved. That is the strongest available form
  of the packet's "UI changes must not alter simulation hashes".
- Two tests assert the weaker, behavioural form directly: opening and closing
  every section twice leaves the world hash and the order log identical, and
  re-deriving every panel's view model five times leaves the world hash
  identical.
- One test asserts the decision still travels the order pathway: applying a
  balanced allocation appends exactly five `SectorAllocation` orders.
