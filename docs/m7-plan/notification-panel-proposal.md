# Notification panel for war events: dispatches, decisions and the battle report (PROPOSAL)

**Status: PROPOSAL (M7 lane U, 2026-11-09; revised in verification rounds 1 and 2 the same day). Not ratified, not
implemented.** Changes made in verification round 1 are marked in place with *(r1, U-Vn …)*, those of round 2 with
*(r2, U2-n …)*; the lane report's §10 and §11 list every finding and its verdict. Presentation only. It reads only
fog-filtered published state, through the player-knowledge seam ([`PolityKnowledge`](../../Sim.Core/State/PolityKnowledge.cs),
wrapped in Sim.Ui by [`PlayerKnowledge`](../../Sim.Ui/ViewModel/PlayerKnowledge.cs)) and a per-polity published event log
(§10). It never writes simulation state, draws no simulation RNG and changes no timing, order log or hash
(plan revision INV-8). Nothing in `Sim.Core`, `Sim.Data`, `Sim.Cli`, `Sim.Tests` or `Sim.Ui` changes with this document
(D-079 §5 Q5 is open). The lane report is
[`prototypes/m7-sprint1/reports/H-U.md`](../../prototypes/m7-sprint1/reports/H-U.md).

**What this document is.** The design that D-080 WA3-D delegates: "A notification system modelled on the Director's
reference … is invited as an add-on. Its design is delegated; it is presentation only, reads fog-filtered published
state, and never writes simulation state." It is written in this game's own terms. The pattern study it draws on is
kept apart, labelled as reference, in Annex R (D-069 R1).

**What it is not.**
- It is not the list of significant events. That list is OPEN (D-079 §5 Q2, DR17) and the Director's. The design works
  for any approved list (§4).
- It is not a knowledge rule. What a polity learns, and when, is the knowledge layer's: contract C7 (detection and
  knowledge, homed in M7.8 and M7.12) and contract C19 (War Pulse locality and the observation rule, homed in M7.14) of
  [the 2026-11-05 plan revision](m7-2-m7-3-revision-2026-11-05.md). This document states what it needs from that layer
  (§10.1) and reads nothing else. *(r1, U-V5: it said "M7.8; contracts C7 and C19"; C19 is homed in M7.14, plan
  revision `:569`.)*
- It is not code, art or audio. No icon is drawn here and no sound exists in `Sim.Ui` today.

Statements are labelled **MEASURED** (read or run in this lane's clone at base `1601c74`; in verification round 1 at
`e38bbcc` and in round 2 at `9df5ffb`, whose code and reports are those of `1601c74`; round 2's runs were scratch probes
in a temporary worktree of the clone, never committed, lane report §11), **INFERRED** (reasoned, not run) or
**PROPOSAL** (design for Director review). Every report cited is on the development branch at `1601c74`.

---

## §0 Summary

The player sees three surfaces and one card:

| Surface | What it holds | When it is there |
|---|---|---|
| **Decisions** (the tray: the first block of the right-hand context column, which a window opens; *r2, U2-1*: r1 made it "the window's mode bar at the top of the map, right of the selection card", where the Age panel and the overlay key stand) | One entry per significant event (hit) of the decision window that is open for the player now; an entry holds that hit's decision items (*r1, U-V2*) | Only while a window is open. The clock is held at the window's instant until the player presses **Resume**. |
| **Dispatches** (the rail, at the map's right edge, standing on the LEGEND tab or the legend, and yielding to every existing control in its column; *r1, U-V1*; *r2, U2-1, U2-2*) | Informational notices, stacked by kind; ongoing conditions at the foot of the rail | Always, where its column has room for one icon (§3). Nothing on the rail ever stops the clock. |
| **Dispatch log** (a filter of the ANNALS section) | Every notice the player ever received, in order | Always. Dismissing a notice never removes its log line. |
| **Battle report** (a card in the right-hand context column) | What the player knows of one battle or siege | Opened by a click on a battle notice. This is the "side panel" of D-080 WA3. |

Five rules carry the design (each is detailed below):

1. **One source.** Every notice is built from the player's own published event rows and `PlayerKnowledge`. No notice is
   built from an authoritative table that the player's fog has not filtered (§5, §10).
2. **Decision items are window hits.** A notice is a decision item exactly when it is a hit of a decision window that the
   player receives (T1 R4, T2 `WindowsFor`, ADR-061 §3.6). It holds the clock but never forces a choice: every choice
   offered at a window has a default that Resume applies. After a battle: Occupy, Hold
   ([H-W §3.1](../../prototypes/m7-sprint1/reports/H-W.md)). When a battle is joined (N-101): the TacticalResolver, the
   default for a battle with a player side (lane P's `ResolverPolicy.Default`, `ResolverPolicy.cs:41–45`; D-079 DR8
   "AutoResolver is an optional shortcut, never the default", applied to player battles by D-079 §1.5 R3). *(r1, U-V12:
   it named only the post-battle defaults.)*
3. **Observers see battle results only (WA3).** A polity that observes a war under fog, and is not a participant, receives
   a battle result as a dispatch that opens the battle report on click. It never receives a decision item for another
   polity's battle, and never receives war economics (D-080 WA3, WA3-R). The catalogue also offers observers three
   notices that are not battle results: a war first seen (N-601, with its log line N-602) and a leader's execution and a
   war's end after a capture the observer saw (N-303, N-309). Each widens WA3 and is **off unless the Director approves
   it** (§15, U-Q5 and U-Q10). *(r1, U-V4: the catalogue gave observers these notices while this rule said "results
   only", and N-601 was put to no Director question.)*
4. **One deterministic order per surface.** The tray follows its window's own key order (class, priority, source,
   sequence; T1 R1–R2), one entry per hit (§9 item 1). Every other list is sorted by
   `(KnownAtMs, KindRank, SubjectId, LogSeq)`, all stable integers (§10.3), with a tie-dense test (§13.2). *(r1, U-V2:
   it said every list, the tray included, used the second key.)*
5. **Expiry by simulation time, never by wall-clock time.** A dispatch lives until the end of the campaign turn after the
   one it was learned in. A decision item lives while its window is open. An ongoing notice lives while its condition
   holds (§8).

---

## §1 Authority and what this proposal may decide

| Source | What it fixes for this design |
|---|---|
| [D-080](../d080-war-pulse-director-answers-2026-11-05.md) WA3 (DECIDED) | Non-participant observers may see **battle results** as a side-panel notification, opened on click. |
| D-080 WA3-D (DELEGATED) | The notification system's design is delegated: presentation only, reads fog-filtered published state, never writes simulation state. **This document is that design.** |
| D-080 WA3-R (READING, flagged) | Observers are the polities that can observe the war under fog (ADR-061 §3.11 locality; T2 `WarFog`), not every polity. War economics are not granted to observers. Applied here as written; the Director may correct either reading. |
| [D-079](../d079-m7-governance-reconciliation-2026-11-05.md) DR12, DR15–DR18, DR22, DR36–DR38, DR41 | War Pulses are local and respect fog; event-triggered decision windows; the significant list is a proposal (Q2 OPEN); at most two actions per decision window; leader execution, post-battle choices, war records; cinematics agree with outcomes. |
| D-079 §1.5 R8 (READING) | Event-triggered windows share the enclosing window's two action slots (as applied in T3). Bears on what the decision tray must say (§9). |
| [ADR-061](../adr/adr-061-hybrid-event-time-decision-windows.md) §3.6, §3.12 | A window opens for each polity for which the event is significant **and known** under its fog; "the UI pauses only for the human's own window, so a pause can never reveal an event the human cannot know"; presentation reads published state and never writes it. |
| Plan revision 2026-11-05: INV-5, INV-8, INV-10; C7, C10, C12, C19, C23, C31–C33, C35 | Fog-honest knowledge; presentation never affects simulation; one authoritative outcome feeds records, choices and cinematics; War Pulse observation rule; records; the outcome selector as a pure function of records. |
| [D-060](../d060-rival-internals-isolation.md) | Another polity's books (stocks, queues, people) are never shown to the player. War economics are books. |
| [D-039](../d039-command-fog-and-siege.md) A2, B5, C1 | Position, strength and outcome are separate information channels; reports are stale, never wrong; live, ghost and unknown display states. |
| [D-069](../d069-originality-and-third-party-game-references.md), [ip-originality.md](../ip-originality.md) | Our own design and our own words. No other game is named in anything the player reads. Icons and sounds need recorded provenance. |
| [Temporal-control placeholder](../m5-temporal-control-and-player-agency-placeholder.md) (agreed model, NOT ratified) | Design input only: "interrupt the player because their decision matters"; warnings are not interrupts; no confirmation dialogs; player-defined attention triggers. Its hand-back conflict (CR-006 §1) is now ruled by DIR5 §2.5. |

**Taken under the WA3-D delegation (PROPOSAL; each can be amended by the Director):** the vocabulary (§2), the surfaces
and layout (§3), the class rule (§4), the observer payload and the naming of unknown polities (§5), the catalogue
treatment (§6), icons and controls (§7), stacking and expiry (§8), the data contract on the presentation side (§10.2),
accessibility (§11) and the test plan (§13).

**Not taken here, because they are someone else's:** which kinds are significant (Director, D-079 Q2); what a polity
learns and when (the knowledge layer: C7 in M7.8 and M7.12, C19's observation rule in M7.14; *r1, U-V5*); whether the
per-polity event log is stored simulation state (an ADR when the
packet is authorized, §10.1); the Standoff battle end (Director, H-TR P-TR1); AutoResolver acceptance (Director,
D-079 Q4); the execution content default (Director, D-079 Q12); the disposition of a city taken by the player's own
order at T (the war-outcome rules, DR37 and ADR-061; U-Q11; *r2, U2-5*).

---

## §2 Vocabulary (our own terms)

| Term | Meaning |
|---|---|
| Notice | One thing the player is told. Every notice has a kind, a subject, an instant the player learned it, and a log line. |
| Decision item | A notice bound to a hit of the decision window that is open for the player. Shown on the Decisions tray. The brief's "blocker". |
| Dispatch | An informational notice of a one-off event. Shown on the rail. |
| Ongoing notice | An informational notice of a condition that holds now (an army in arrears, a city under siege). Shown at the foot of the rail while the condition holds. |
| Order note | The game's reply to the player's own order: when it takes effect, or why it was refused. Shown beside the control the order came from, then logged. |
| Status chip | Not a notice: a small always-current figure in the status band (the war clock, actions left in this window). |
| Stack | The notices of one kind on the rail, shown as one icon with a count. |
| Relation | Why a polity receives a notice: Participant, Owner, Victim, Captor, Origin (of prisoners) or Observer (§5). |
| Kind rank | The stable integer that orders kinds at one instant (§10.3). |

Player-facing labels proposed: "Dispatches", "Decisions at <date>", "Ongoing", "Resume", "Battle report", "Dispatch
log". None names another game (D-069 R2). The internal word "notification" stays out of the player's text.

---

## §3 Surfaces and layout

The layout keeps the existing chrome of [`PanelLayout`](../../Sim.Ui/ViewModel/PanelLayout.cs) (MEASURED by reading):
the status band at the top (52 px, `:84`), the command bar at the foot (60 px, `:89`) with End Turn at its left end
([`ChromeGeometry.CommandRow`](../../Sim.Ui/ViewModel/ChromeGeometry.cs) `:297`), the selection card top left, and the
context column on the right (420–560 px, `:118–123`). The minimum window is 1080 × 640 (`:54`).

```
+----------------------------------------------------------------------------------------+
| status band: year, population, food, research, Age   [war clock chip] [actions 1/2]    |
+--------------------------------------------------------------+-------------------------+
| [selection card]                                             | Decisions at <date> 1/2 |
| [Age panel or  ]                                     [stack] |  entry . entry   Resume |
| [ unit card    ]          map                        [stack] +-------------------------+
| [ (the left    ]                          Dispatches [stack] |  a section, or the      |
| [  column)     ]                                   [ongoing] |  DETAILS card           |
| [              ] [OVERLAYS]                     [LEGEND tab] |  (Battle report here)   |
+--------------------------------------------------------------+-------------------------+
| [ End Turn | Resume ]  [Place] [Empire] [Policy] [Institutions] [Annals] [Resources]  ...|
+----------------------------------------------------------------------------------------+
```

*(r2, U2-1: the sketch drew the tray on the map, right of the selection card: "[selection card] [Decisions at <date> .
entry . entry Resume]".)*

**What already floats over the map rect (MEASURED by reading `Sim.Ui` at `e38bbcc`, whose code is that of `1601c74`;
added in verification round 1, U-V1).** The first version of this section missed every row of this table below the
selection card.

| Where | Existing control | Anchor (file:line) |
|---|---|---|
| Top left | The selection card (always shown) | `PanelLayout.Selection` (`PanelLayout.cs:106–107`) |
| Top of the map, right of the selection card | The placement bar (placement mode only), 240–560 px × the UI scale wide, centred on the map where it fits | `DrawPlacementBar` ([`GameUi.Placement.cs`](../../Sim.Ui/GameUi.Placement.cs) `:184–186`, `:208`) |
| Top, centred on the window | The End Turn toast (`NoticeToast`, 6 s of UI time) and the Age toast, painted over the chrome | [`GameUi.cs`](../../Sim.Ui/GameUi.cs) `:949`; `NoticeToast.cs:46–54` |
| Top right | The folded context column's tab, while a section is folded | `DrawContextTab` (`GameUi.cs:1733–1745`) |
| Left column, under the selection card | The Age panel (x 12 px × scale, at most 480 px × scale wide, down to just above the command bar; painted on the foreground list over the ImGui windows, and it takes the clicks in its rect, `:859`, `:941–944`; *r2*) or the formation card (320 px × scale wide, as the selection card, `UnitCardWidth`, `:197`; *r2*); they share the column | `AgePanelRect` (`GameUi.cs:837–845`); `:1823`; `SelectUnit` (`:1757`) |
| Bottom left, right of the left column | The OVERLAYS tab, which grows upward with the NOW USABLE block and, unfolded, the overlay controls; the overlay key beside or above it, kept to its half of the map (above the tab, and in the map's upper half, it runs to the map's right margin, `:138`; *r2, U2-2*) | `OverlayTab` ([`GameUi.Overlay.cs`](../../Sim.Ui/GameUi.Overlay.cs) `:69–96`); `OverlayKeyAt` (`:129–143`) |
| Bottom right (the map rect's corner) | The LEGEND tab, always on the map; while the legend is shown, the legend just above its tab, right-aligned with it, at most 340 px × scale wide | `LegendTab` (`GameUi.cs:1702–1709`); `:913–919`; `LegendMaxWidth` (`:273`) |
| Bottom right of the window, under the panels | The compass rose (decorative, 132 px, on the background list) | `DrawCompassRose` (`GameUi.cs:1070`) |

The legend and the overlay key take their own clicks (`OnLegend`, `GameUi.cs:1692–1694`); the tabs, cards and bars are
ImGui windows; the legend and the overlay controls have playability-gate rows (`LegendRows`,
`Sim.Ui/Headless/PlayabilityGate.cs:2261`; `Sim.Ui/Headless/PlayabilityGate.Overlay.cs:36–72`).

**The controls of that table already collide in some states (MEASURED in verification round 2 through the real UI,
`UiFrameHarness`, seed 42, UI scale 1, at `9df5ffb`, whose `Sim.Ui` is that of `1601c74`; lane report §11; *r2,
U2-3*).** This design does not repair them (`Sim.Ui` is not modified here; the lane report proposes a queue line):
- At 1080 × 640 with a section open and the Age panel open, the OVERLAYS tab (x 504–646, y 514–568) lies over the LEGEND
  tab (x 510–636) and runs 10 px past the map's right margin (x 636). A click on the OVERLAYS control there toggles the
  legend instead (overlays open: false to false; legend shown: false to true), so the overlay controls cannot be
  unfolded by click in that state. At 1280 × 800 the same click unfolds them and the tabs do not touch (x 504–646 and
  710–836).
- In placement mode at 1080 × 640 with the Age panel open, the placement bar (x 344–636, y 64–246) runs 16 px into the
  Age panel (x 12–492, top y 230) over 148 px of width; at 1280 × 800 it ends 4 px above it (y 226).
- The overlay key's form above its tab ignores the legend's reach (`GameUi.Overlay.cs:138`), so with the legend shown
  it lies over the legend: 328.3 × 260 px at 1280 × 800 (Age panel open, every layer shown, the controls folded; key
  x 504–835.3, y 408–668; legend x 507–836, y 365.9–668), 328.3 × 18.1 px with the controls unfolded (key y 64–384), and
  97.5 × 180 px at 1080 × 640 without the Age panel (one layer shown; key x 12–404.5, y 328–508; legend x 307–636,
  y 205.9–508).

Every rule below is stated against the existing controls' rects as they are drawn, these collisions included: no rule
depends on the existing controls being apart, and the layout row of §13.3 (E7) asserts only pairs that include a new
surface.

**The new surfaces (PROPOSAL; revised in verification rounds 1 and 2).** Each new surface yields to the existing
controls: it takes what they leave free, and the existing anchors that change are named below (two terms of the overlay
key's anchor, for the rail; the context column's content top and its open state during a window, for the tray).
*(r2, U2-1, U2-2: r1 said "No new surface overlaps any control of that table; who yields where they meet is stated,
and a layout row pins it (§13.3, E7)". The tray's rule could not keep that promise (U2-1, below), and the rail's "one
change" was one of two terms (U2-2).)*

- **The rail (Dispatches).** A column of stack icons, `RailWidth` × the UI scale wide (a layout constant for the
  implementing packet; the figures below use 48 px, INFERRED), inside the right margin of the map rect
  ([`PanelLayout.MapRect`](../../Sim.Ui/ViewModel/PanelLayout.cs)): against the window edge when no section is shown,
  against the context column's left edge when one is.
  - *Placement.* The rail keeps one margin clear of every existing control that reaches its column: the LEGEND tab and,
    while it is shown, the legend; the OVERLAYS tab (it reaches the column at the narrow window, above); the folded
    column's tab; the placement bar; and, at a large UI scale on a narrow window, the selection card and the Age panel
    or formation card (INFERRED from their anchors: at 1080 × 640 and a UI scale of 1.25 the Age panel ends at x 501 and
    the map at x 540, `GameUi.cs:419–434`, `:837–845`). It fills the lowest free stretch of its column that holds at
    least one icon, from that stretch's foot upward, with ongoing notices at the foot. In the usual states its foot
    stands one margin above the LEGEND tab, or above the legend while the legend is shown (the tab keeps the corner),
    and its top one margin below the folded column's tab or the placement bar where they reach its column. (The tray no
    longer stands on the map, so it is not among them; *r2, U2-1*.)
  - *The overlay key yields.* While the rail is drawn, the key stops one margin short of the rail's column in both of
    its forms: beside the OVERLAYS tab, its limit (`OverlayKeyAt`, `GameUi.Overlay.cs:134`) takes the larger of the
    legend's reach (while the legend is shown) and the rail's column; above the tab, its width bound (`:138`, which also
    bounds the key's form in the map's upper half, `:142–144`) ends at the rail's column instead of the map's right
    margin. These two terms of one anchor are the only change the rail makes to an existing control. Cutting the limit
    sends the key above its tab more often (`:135–136`), which the second term covers. The key's form above the tab is
    then narrower by the rail's column: at 1280 × 800 with the Age panel open it is 331.3 px wide today (MEASURED, x
    504–835.3) and would be about 271 px; at 1080 × 640 with the Age panel open, 132 px today (INFERRED from `:138`; a
    state in which the controls cannot be unfolded by click, above) and about 72 px. *(r2, U2-2: r1 said "The overlay
    key's right limit must stop short of the rail as it already stops short of the legend's reach (`OverlayKeyAt`,
    `GameUi.Overlay.cs:134`): the one change to an existing control's anchor that the rail needs." The form above the
    tab ignores that limit and runs to the map's right margin, MEASURED at x 504–835.3 at 1280 × 800 with the Age panel
    open, in the map's lower half (y 408–668) and in its upper half (y 64–384); cutting the limit alone would have put
    the key over the rail more often.)*
  - *Room.* Stacks that do not fit in that stretch fold into a "+n" icon that opens the Dispatch log filtered to the
    notices that are live now. Where no free stretch holds one icon, the rail is not drawn until room returns.
    Nothing is lost: every notice is in the Dispatch log, and expiry runs on simulation time (§8). Example (INFERRED
    from two measured rects, taken in two sessions): at 1080 × 640 with the legend shown, the legend's top is at
    y 205.9, so the rail's foot is at y 193.9 and its stretch under the map's top margin (y 64) is 129.9 px; in
    placement mode the bar's bottom is at y 246, which leaves no free stretch.
  - The two toasts may paint over the rail for their few seconds, as they paint over the context column and the
    placement bar today, and take no clicks (`NoticeToast` has no input; the Age toast registers no hit region,
    `AgeScreen.cs:653–674`) (INFERRED from their width rules: centred on the window, at most 720 px wide for the End Turn
    toast and `Sp(760)` for the Age toast, less 40 px of the window's width, `NoticeToast.cs:52`, `AgeScreen.cs:658`).
    The compass rose stays under the rail, as it stays under the legend tab.
- **The tray (Decisions).** Shown only while a decision window is open for the player, as the **first block of the
  context column**: a header row ("Decisions at <date>" and the actions left in the window, §9), its entries, and a
  **Resume** button that does exactly what the End Turn button does while a window is open. The column's section or
  DETAILS card follows below it and scrolls as it does today. The tray's height is its content, at most half the
  column's height; its entries scroll beyond that. If the window opens while the column is closed or folded, the column
  opens or unfolds for the window (the map rect narrows and reflows as when the player opens a section), and Resume
  returns the column to the state it had: closed, folded, a section or the DETAILS card. The player may fold the column
  during a window; its tab then reads "< DECISIONS" and unfolds it. The tray changes two things of an existing control,
  the context column: while a window is open the column has content (`ContextHasContent`, `GameUi.cs:339`, and so
  `ContextShown`, `:328`), and its section or card starts below the tray (`DrawContextPanel`, `GameUi.cs:1991–2032`;
  `DrawInfoContext`, `GameUi.Info.cs:242`). Nothing is placed on the map for the tray, so the left column, the
  placement bar, the overlay key, the folded column's tab and the toasts' positions are untouched by it. *(r2, U2-1:
  r1 said "It is the window's mode bar, at the top of the map, placed by the placement bar's rule (right of the
  selection card, 240–560 px × scale wide, centred on the map where it fits) and ending one margin left of the folded
  column's tab when that shows. While the placement bar shows as well, the tray sits one margin below it, and below a
  toast while one shows. … No existing control's anchor changes for the tray." Measured against that rule, the tray
  stood over the Age panel: why-here, below.)*
- **The primary verb.** While a window is open, the End Turn button reads **Resume** (the turn continues to the next
  stop or to the turn's end). Space keeps working through the existing `EndTurnKey` predicate. The label change is a
  layout case for the chrome tests (the narrow-window gate at 1080 px).
- **The Battle report** opens in the context column as a DETAILS card (the existing pinned info card,
  [`InfoPanel`](../../Sim.Ui/Info/InfoPanel.cs)), below the tray while a window is open, so no new navigation button is
  needed.

Why here (INFERRED; corrected in verification rounds 1 and 2). The rail stays in the lower right that the Director's
reference describes ("hoverable icons at the lower right", D-080 WA3-D; WA3-D fixes no corner, so the anchor is this
design's choice), standing on the LEGEND tab instead of displacing it. The tray is a view of a mode (the clock is held),
but the top of the map, where the existing interface shows its one mode, placement, as a bar, cannot hold a decision
block in every state of the existing chrome (MEASURED in verification round 2; lane report §11; *r2, U2-1*):
- right of the selection card, as the placement bar's rule puts it (`GameUi.Placement.cs:184`), the block's left
  148 px stand over the Age panel (selection card x 12–332, so the block starts at x 344; Age panel x 12–492) at every
  window size, and only the room above the Age panel's top separates them: 166 px from the map's top margin with the
  selection card unfolded, 68 px with it folded, while the smallest block in the placement bar's frame (its header, one
  button row and its margin, with no entry) is 104.4 px; one margin below the placement bar it starts 28 px
  (1080 × 640) or 8 px (1280 × 800) inside the Age panel. The overlap would not be only drawn: the Age panel is painted
  over the ImGui windows and takes the clicks in its rect (`GameUi.cs:941–944`, `:859`, `:869–873`);
- right of the Age panel, the map leaves 132 px at 1080 × 640 with a section open, against a block 240 px × scale wide
  at least;
- the overlay key's form in the map's upper half covers the top right from the map's top margin to a margin above its
  centre line (x 504–835.3, y 64–384 at 1280 × 800, Age panel open, the controls unfolded).

The context column is chrome that no floating control covers (only the toasts paint over it, for their few seconds,
taking no clicks), and it is where a decision's panel opens (§7.2), so an entry and its panel stand in one column. The
placement mode already uses the same column for its card (`GameUi.Placement.cs:126–138`). The cost (INFERRED): a window
that opens while the column is closed narrows the map until Resume. The map's foot has no room for the tray either: the
left column and the OVERLAYS tab hold its left part and the LEGEND tab its right corner, and at the narrow 1080 × 640
window with the Age panel and a section open, the foot between the Age panel (ending at x = 492) and the map's right
edge (x = 648) is 156 px at UI scale 1 (INFERRED from the anchors above). *(r1, U-V1: it said "the lower right of the
map is the only corner the current chrome leaves free in both states of the context column", which is false, since the
LEGEND tab holds that corner and the OVERLAYS tab the other, and it put the tray "directly above the End Turn button",
where the Age panel, the formation card or the OVERLAYS tab stand.)* *(r2, U2-1: r1 said "The tray is a mode bar
because a decision window is a mode (the clock is held), and the existing interface already shows its one mode,
placement, as a bar at the top of the map.")*

---

## §4 The class rule: decision items versus informational notices

**Rule C1.** A notice is a **decision item** if and only if it is a hit of a decision window that is open for the player
now: a window at instant T whose fog-filtered hit list for the player (T2 `WarTheaterWorld.WindowsFor`) contains the
notice's event. Significance is data (`SignificantKinds`, [G-time-events-t1](../../prototypes/m7-sprint1/reports/G-time-events-t1.md)
R4); the list is OPEN (D-079 Q2). So "decision item" is not a fixed property of a kind. In the catalogue (§6) a kind is
marked:
- **D (if approved)**: on T1's proposed list, or marked significant in a prototype world; it becomes a decision item for
  the recipients the window rule gives, once the Director approves it into the list;
- **D-candidate**: proposed by a lane report as a candidate for Q2 (for example H-E §9), informational until approved;
- **I**: informational for everyone.

*(r1, U-V3: added.)* A prototype world's decision windows are not per recipient: T1's `Runner` lists every significant
event at an instant, and only T2's `WindowsFor` filters by polity. So "marked significant in a prototype world" does not
say to whom a kind is significant. Where T1's list does not say either, the catalogue row names the recipients for whom
it proposes a decision item (marked "D (if approved)") and the others ("I"), and the Director's list decides (D-079 Q2).
The labels in §6 follow this rule; the round-1 corrections are N-303, N-304, N-407 and N-410, the round-2 ones N-201
and N-401. *(r2, U2-4: round 1 made this claim while N-201's defender form was labelled "D-candidate" although T1 pri 0
covers a threatened last city, and N-401 was labelled "I" although H-E §9 lists `ArrearsBegan` as a Q2 candidate.)*

**Rule C2.** Everything that is not a decision item is informational: a dispatch, an ongoing notice or an order note. No
informational notice stops the clock, plays an interrupting animation or takes focus.

**Rule C3.** A decision item holds the clock; it never forces a choice. Resume is always available. Where a choice is
offered only at that instant (a captured settlement's disposition, H-W rule L2), the item says so, and Resume applies
the default (Occupy). At a battle joined (N-101), Resume hands the battle to the TacticalResolver, the default for a
battle with a player side (`ResolverPolicy.Default`, [`ResolverPolicy.cs`](../../prototypes/m7-sprint1/Proto.M7/Tactical/ResolverPolicy.cs)
`:41–45`; D-079 DR8 as applied to player battles by §1.5 R3); resolving summarily is always the player's explicit
choice. *(r1, U-V12: the N-101 default was not stated.)* This follows the agreed temporal-control principle "no
confirmation is demanded" (placeholder §10; design input, not ratified).

**Rule C4.** An observer never receives a decision item for another polity's battle: an event is significant only to the
polities it is significant to (T1 §4 "significant to … and known under fog"), and WA3 grants observers a notification
"if they click it". Observer battle results are dispatches (§6.4, N-103).

**Rule C5.** A notice never opens a window, and no UI action opens one. Orders never open windows (T1 R4); notices are
views of windows, not causes of them.

---

## §5 Who sees what: recipient relations and the fog rules (WA3-R)

**Relations.** A polity receives a notice for one of these reasons:

| Relation | Who | Typical notices |
|---|---|---|
| Participant | A side of the war or battle (C19's participant set: the belligerents and their formations) | Battle joined, battle result, siege events, outcome choices |
| Owner | The polity whose army, garrison, installation or treasury it is | War economics (its own books) |
| Victim | The owner of a locality that another polity's force acts on | Stocks taken from its locality, occupation of its locality, famine risk there |
| Captor / Origin | The polity holding prisoners or a captive leader / the polity whose men or leader are held | Prisoner and leader notices |
| Observer | A non-participant that observes the event under fog at the instant (T2 `WarFog`: an observer of its own present with the place in sight) | Battle results only |

**Fog rules.**
- **F1 (one source).** A notice is built only from the recipient's published event rows (§10.1) and `PlayerKnowledge`.
  The builder never reads an authoritative war table directly (battle casualties, siege state, army books). A source
  guard pins this (§13.1, L7), as `KnowledgeLeakTests` does for today's readers.
- **F2 (observers, WA3 and WA3-R).** An observer receives battle results only: the place, the instant it learned of it,
  the combatants it knows, the public outcome class (§6.4) and the side that held the field. No casualty figures, no
  siege internals (health, food, morale, disease, breach), no war economics. Losses an observer actually saw arrive
  later through the knowledge layer's strength channel (D-039 A2), if and when the knowledge layer publishes it (C7:
  M7.8, M7.12); until then the observer report has no numbers. PROPOSAL; the Director may widen it: the payload (§15,
  U-Q2), and the non-result notices N-601/N-602 (U-Q10) and N-303/N-309 (U-Q5), each **off unless approved**. *(r1,
  U-V4: the catalogue gave observers those notices without saying they widen this rule; r1, U-V5: it said "if and when
  M7.8 publishes it".)*
- **F3 (books, D-060).** War economics are the owner's books. A victim learns only what happened to its own books (grain
  or money taken from its locality, trade income lost to an occupation), never the acting army's arrears, morale or
  cohesion. A participant learns its own casualties from its own rows; an enemy's losses only as its own knowledge
  publishes them (D-039 A2), never from the enemy's casualty rows.
- **F4 (naming; P7 precedent).** A notice names only polities and places the recipient knows (`KnowsPolity`,
  `KnowsSettlement`). A combatant without contact is "an unidentified force"; its id appears nowhere in the player's text.
  This differs from the annals' rule ([`StateChronicle`](../../Sim.Ui/ViewModel/StateChronicle.cs) P7, which drops an
  event that names an unknown subject), because dropping would hide a battle the observer saw. PROPOSAL (§15, U-Q3).
- **F5 (timing, ADR-061 §3.6).** A notice's time is the instant the recipient learned it (`KnownAtMs`), never earlier.
  The UI stops only at the player's own windows. While the clock runs to the next stop, progress is shown only as
  simulation time (a date advancing), never paced by or counting sub-steps: a sub-step count would reveal hidden
  significant events, because every significant event splits the turn whether or not anyone can see it. Sounds and
  animations are functions of the player's visible notices only.
- **F6 (staleness, D-039 B5).** Reports are stale, never wrong. When the event instant and the learned instant differ, the
  notice says both ("fought on <date>; word reached you on <date>").
- **F7 (ids).** No raw simulation id appears in player text (the `RawSettlementIds_AreNotInPlayerText` precedent). A
  battle is named by its place and date. `LogSeq` is dense per recipient, so gaps cannot reveal other polities' events.

---

## §6 The catalogue of notice kinds

Columns: **Kind** (proposed code and name); **Source** (report section and prototype identifier, read at `1601c74`);
**Recipients under fog**; **Class** (§4: D if approved / D-candidate / I; and the informational form); **Icon** (§7.1);
**Hover**; **Click**; **Stack / expiry** (§8: E = dispatch expiry, W = while the window is open, C = while the condition
holds, N = until the next stop or turn start, L = log only; "W, then X" = a decision item while its window is open,
which at Resume takes form X: E a dispatch on the rail, L its log line only, C an ongoing notice; *r2, U2-7*: the
legend did not define "W, then X", and §8 and §9 said every decision item became a log line at Resume).

### 6.1 Decision-window significant events (T1's proposed list; D-079 Q2 OPEN)

[G-time-events-t1 §4](../../prototypes/m7-sprint1/reports/G-time-events-t1.md) proposes ten entries for Director approval.
Each maps to one or more notice kinds; the class "D (if approved)" applies only to the recipients named.

| T1 pri | T1 entry | Notice kinds | Decision item for | Everyone else under fog |
|---|---|---|---|---|
| 0 | Own city captured, or own last city threatened | N-301 (former owner), N-201 (own city besieged) | The polity that lost the city, or whose last city is threatened | Observers of the capture: N-103 |
| 1 | War declared on us, or an observed hostile force enters our territory | N-606, N-607 | The target polity | Nothing by default (N-601, a war first seen, is a proposed widening of WA3, off unless approved: U-Q10; *r1, U-V4*) |
| 2 | Battle begins involving our formations | N-101 | Participants | Nothing (WA3 grants results, not starts) |
| 3 | Battle concluded involving our formations | N-102, N-306, N-301 | Participants (the post-battle window: prisoners, dispositions). One tray entry per hit: a battle's result and its prisoners are one `BattleResolved` hit (§9 item 1; *r1, U-V2*) | Observers: N-103 |
| 4 | Ruler death, succession or regency | N-304 (own leader taken captive), N-310 (own leader executed while held; *r1, U-V6*), N-701 | The ruler's polity | Captor: N-304 as captor (D if approved, because `LeaderFateChanged` is significant in lane W's world, not by T1 pri 4; *r1, U-V3*) |
| 5 | Revolt, secession or civil-war outbreak in our territory | N-208 (revolt in an occupied installation), N-706 (*r1, U-V3*: N-701 no longer covers both pri 4 and pri 5) | The installation's or territory's controller | Nothing (not a battle result) |
| 6 | Wonder-class construction completed | N-702 | The builder | Outside M7's war scope; its own knowledge rule |
| 7 | Famine onset or epidemic outbreak | N-703; war-caused N-407 (its famine-deaths form) and N-410; N-407's famine-risk form is a D-candidate (H-E §9) (*r1, U-V3*: both were labelled D-candidate) | The polity whose settlement it is | Nothing |
| 8 | Peace offer, ultimatum or call to arms with an in-turn deadline | N-704 | The addressee | M8 |
| 9 | First contact with a new polity | N-705 | Both polities | Nothing |

Hover and click for these follow their kind's row: the war kinds in §6.2–§6.6, the non-war kinds N-701 to N-706 in
§6.9. The non-war kinds are listed so the tray can show every T1 entry; their content belongs to other milestones, and
§6.9 gives only what the tray and the rail need. *(r1, U-V3: this said "their kind's row below", but N-701 to N-705
had no row.)*

### 6.2 War Pulse and orders ([G-war-pulse-t2](../../prototypes/m7-sprint1/reports/G-war-pulse-t2.md); D-080 WA1)

| Kind | Source | Recipients | Class | Icon | Hover | Click | Stack / expiry |
|---|---|---|---|---|---|---|---|
| — Pulse start, pulse end, turn-start straddle split | T2 §2; `WarKinds.PulseStart/PulseEnd/TurnStart`; `KnowledgeEntry` | — | **No notice.** 90–180 pulses per turn per war at T2's proposed lengths (T2 §7, MEASURED there) would flood the rail. They drive the war clock chip. | — | — | — | — |
| N-601 War observed | First `KnowledgeEntry` of a war the polity is not party to (T2 §4) | Observer | **A proposed widening of WA3 (not a battle result), off unless the Director approves it (U-Q10; *r1, U-V4*).** If approved: I, dispatch | Two banners, eye mark | "War seen between <known polity or unidentified force> and <…> near <place>" | Centre the map on the theatre; open the war's report card | Stack by kind; E |
| N-602 War out of sight | The observer's last observer leaves sight | Observer | With N-601 (U-Q10): I, L | — | — | — | L |
| N-603 Order takes effect | T2 §6's delivery rule applied to the order's instant T: a window order at T; a pulse order at the first pulse start B ≥ T of its war; a turn order at the first turn start S ≥ T (WA1: B = T or S = T applies now). At T the note states the instant the rule gives; the `LatchRecord` written when the order latches confirms it (`EffectiveMs`): at T itself only when T is the boundary (`WarTheaterWorld.cs:446–464` pulse order, `:468–482` turn order), otherwise at the boundary (`:343`, `:362`). *(r1, found while fixing U-V7: the source was given as the `LatchRecord` alone, which does not yet exist at T for an order that waits for a boundary.)* **When no boundary comes** *(r2, U2-6: added)*: a pulse order issued inside its war's last pulse after that pulse's start, or a turn order issued in the campaign's last turn after its start, is neither latched nor refused, because the next pulse start or turn start is never scheduled (`:380–385`, `:367`; the order waits in `:462–464`, `:480–482`). The note then promises no instant it cannot know: in the campaign's last turn it says the order cannot take effect (the calendar is known at T); a pulse order's note names the next pulse start while the war's schedule continues past the current pulse (in T2, while that pulse ends before the war's stop instant, `:380`) and otherwise says the order waits for the war's next pulse; if the war ends first, a lapse note built from the producer's lapse row (§10.1), which T2 does not write, replaces it at the war's end | The issuer | I, order note | Seal | "Takes effect now" (window order); "Supply rate takes effect with the pulse starting <date> (now)" or "… at the next turn, <date>"; "This is the last turn: this order will not take effect"; "Order lapsed: <war> ended before its next pulse" | Open the order's surface | N; a lapse note: N from the war's end |
| N-604 Order refused | `OrderRefusal` (`WarRefusal`: NotARecipient, BadTarget, WarEnded, BadValue, UnknownKind) | The issuer | I, order note | Broken seal | The refusal reason in plain words | Open the order's surface | N |
| N-605 War upkeep or supply in arrears | T2 §5 (`paid = min(due, balance)`, shortfall recorded as arrears) | Owner | I, ongoing | Coin with a gap | "War upkeep unpaid: <amount> owed" | Open the empire's war accounts | C |
| N-606 War declared on you | The war predicate (B7-13; C19, C33). In the prototypes wars are inputs (H-W limitation 10) | The target | D (if approved, T1 pri 1) | Banner, red edge | "<polity> is at war with you" | Open the war's report card | W, then E |
| N-607 Hostile force in your territory | Knowledge layer: an enemy formation the polity knows (`KnowsUnit`) inside its territory | Owner of the territory | D (if approved, T1 pri 1) | Banner over border line | "<force> seen at <place>" | Centre the map; select the formation | W, then E |

Status chips (not notices): the **war clock** ("At war: pulse 37 of this turn; next pulse <date>") for each war the
player is in, from its own pulse records; it shows nothing of wars the player is not in.

### 6.3 Two-action windows ([G-action-windows-t3](../../prototypes/m7-sprint1/reports/G-action-windows-t3.md); DR22, C23)

| Kind | Source | Recipients | Class | Icon | Hover | Click | Stack / expiry |
|---|---|---|---|---|---|---|---|
| — Actions left in this window | `ActionWindowBook` slots (2 per polity per enclosing window, T3 §1) | The player | **Status chip** "Actions 1/2", and the tray header (§9) | — | Which window the slots belong to (this War Pulse, or this turn) | — | — |
| — Action Capacity refreshed | `OnTurnStart` / `OnPulseStart` refresh (T3 §1) | The player | **No notice**; the agent's card shows its capacity | — | — | — | — |
| N-604 Action refused (T3 codes) | `WarRefusal` 10–17: SlotsExhausted, CapacityExhausted, NotInActionSet, KindRepeated, CombinationForbidden, HitAndRun, NotOwnAgent, OutOfReach | The issuer | I, order note | Broken seal | Plain words: "No action left in this window (2 of 2 used)", "Not enough Action Capacity", "This agent cannot <act>", "Already <act>ed in this window", "Cannot retreat after moving in the same window", "Cannot attack a force you withdrew from, in this window or your next one, unless it attacks you or closes on you first" (T3 AE6; `HitAndRunCooldownWindows` = 1, `ActionWindows.cs:73`, `:251`; *r1, U-V10*: it said "until your next window", but the bar also holds through the next window), "Out of reach" | Open the agent's card | N |
| N-604 NotARecipient | T2 order gate | The issuer | Developer view only: the UI only stops at the player's own windows, so this refusal means a UI defect | — | — | — | L |

A refused action changes nothing in the world (T3 AE5); the order note is the only trace the player sees, plus its log line.

### 6.4 Battles, battle endings and the AutoResolver ([H-TR](../../prototypes/m7-sprint1/reports/H-TR.md), [H-P](../../prototypes/m7-sprint1/reports/H-P.md), [H-W](../../prototypes/m7-sprint1/reports/H-W.md))

**Public outcome classes.** A battle's authoritative end (`BattleEndReason`, or lane S's `SiegeOutcome`) is shown through
the same mapping as lane W's `CinematicSelector` ([`OutcomePresentation.cs`](../../prototypes/m7-sprint1/Proto.M7/War/Outcomes/OutcomePresentation.cs)),
so notices, cinematics and records cannot disagree (INV-10):

| Authoritative end | Participant's notice | Observer's notice | Note |
|---|---|---|---|
| `SideEliminated`, `SideDisengaged` with a winner side | Victory / Defeat (by the viewer's side) | "<side> held the field" | Includes routs, escapes and surrenders |
| `BothEliminated`, `BothDisengaged` | Inconclusive: both forces broke / withdrew | "Neither side held the field" | Never shown as a victory |
| `Standoff` | Inconclusive: neither side pressed the battle | Same | **UNRATIFIED** (H-TR P-TR1). If the Director rejects it, this row goes. Never shown as a victory or a withdrawal. |
| Siege `Capitulated` / `CapCapitulation` | Surrender (unconditional / on terms, the garrison marched out) | "<place> surrendered" | |
| Siege `Stormed` / `GarrisonDestroyed` | Taken by storm / garrison destroyed | "<place> was taken" | |
| Siege `Relieved` / `Lifted` / `CapLifted` | Siege raised (relieved / lifted / held to the limit) | "The siege of <place> was raised" | |

| Kind | Source | Recipients | Class | Icon | Hover | Click | Stack / expiry |
|---|---|---|---|---|---|---|---|
| N-101 Battle joined | T1 pri 2; the battle's start (campaign hand-off, M7.15) | Participants | D (if approved) | Crossed blades, open ring | "Your <formations> engaged <force> near <place>" | Open the battle choice (fight in detail or resolve summarily, DIR5 §2.1); centre the map. Resume without a choice: the TacticalResolver, the default for a battle with a player side (rule C3; *r1, U-V12*) | W, then L |
| N-102 Battle result | T1 pri 3; `OutcomeKinds.BattleEnded`/`BattleResolved` (85/86, the latter significant in lane W's world) | Participants | D (if approved) | Crossed blades, closed ring; valence mark | Outcome class (table above); own losses by cause (dead, wounded, captured, deserted, from lane W's collapse, H-W §5) | Open the Battle report (participant view) and, if prisoners were taken, the prisoner panel | W, then E |
| N-103 Battle observed | WA3; knowledge layer publishes an observed result (C19 observation pin: a city in sight learns at the observed instant, not before) | Observers | **I, dispatch. Opens the side panel on click (WA3).** No auto-expand, no sound. | Crossed blades with eye mark | One line: "Battle near <place>: <public outcome>" | Open the Battle report (observer view, §5 F2); centre the map | Stack by kind; E |
| N-104 Formation routed by inactivity | DIR5 §2.6 five-pulse rule (H-TR §3, `HiddenStationaryRule`) | Its owner | I, battle report line and in-battle feed (§6.8) | Broken banner | "Routed after five pulses hidden and idle" | Open the formation's card | With its battle |

**AI-vs-AI results and the AutoResolver.** By lane P's default policy (`ResolverPolicy`, H-P §3) an AI-vs-AI land battle is
resolved by the AutoResolver; naval and air battles never default to it. For an observer the result is an ordinary N-103:
the outcome is authoritative whichever resolver produced it. The resolver is shown only in the developer view ("Auto,
model 2"). Lane P's model is **not at parity** (16 of 29 scenarios, 4 of 9 held out, H-P §5.4) and is not adopted;
nothing in the notification depends on that adoption. A player who chooses to resolve its own battle summarily
(DIR5 §2.1) gets an ordinary N-102; the report says "resolved summarily", which the player already knows.

### 6.5 Sieges and garrisons ([H-S](../../prototypes/m7-sprint1/reports/H-S.md))

Prototype facts (MEASURED by reading `SiegeTypes.cs` and H-S §2.2, §2.5, §11): significant kinds are `SiegeEnded` (67),
`AssaultResolved` (68) and `Revolt` (70); there is no event for a siege's start or for a breach (the breach is a state,
wall integrity at or below 350‰ with a fortification); the siege world has **no fog** (H-S §11), so every observer rule
below is a requirement on the knowledge layer, not a property of the prototype.

| Kind | Source | Recipients | Class | Icon | Hover | Click | Stack / expiry |
|---|---|---|---|---|---|---|---|
| N-201 Siege begun | Investment (M7.13 capture path; `SiegeSpec.StartMs` in the prototype) | Besieger owner; defender (controller) | Defender: D (if approved, T1 pri 0) when the place is its last city; I, dispatch for any other place (no lane report proposes a siege's start for Q2). Besieger: I, dispatch. *(r2, U2-4: the defender was "D-candidate (T1 pri 0 covers a threatened last city only)", against §4's rule and §6.1 row 0.)* | Tower with ring | "<place> invested by <force>" / "Your force invests <place>" | Open the siege card | W, then E (defender, last city); E (others) |
| N-202 Assault result | `AssaultResolved` (68), `BattleRecord` purpose 1 | Participants | D (if approved; significant in the prototype) | Tower, crossed blades | "Assault on <place> repulsed / carried" | Open the siege card at the assault | W, then E |
| N-203 Walls breached | Derived: wall integrity crosses the practicable-breach level (`BreachPracticablePermille` 350) | Besieger; defender | I, dispatch | Tower with crack | "A practicable breach at <place>" | Open the siege card | E |
| N-204 Relief arrived; relief battle result | `ReliefArrival` (64); `BattleRecord` purpose 2 | Relief owner, besieger, defender | Arrival: I, dispatch; battle result: as N-102 | Tower with banner approaching | "A relief force reached <place>" / outcome | Open the siege card | E (result: W, then E) |
| N-205 Sally result | `OrderSally` (182); `BattleRecord` purpose 3 | Participants | As N-102 | Tower, outward arrow | "Sally from <place> beaten back / succeeded" | Open the siege card | W, then E |
| N-206 Siege ended | `SiegeEnded` (67), `SiegeEndRecord.Outcome` (7 outcomes, H-S §2.5) | Participants; the taker also gets N-301 | D (if approved; significant in the prototype) | Tower: flag (taken), open gate (raised), white flag (surrender) | Outcome class (§6.4 table), duration, pulses | Participants: the siege card and, for the taker, the disposition panel | W, then E |
| N-206 seen by an observer | Same, through the knowledge layer | Observers who see the place at the end instant | I, as N-103 (siege subtype) | As N-103 | "The siege of <place> ended: <public outcome>" | Battle report (observer view) | E |
| N-207 Siege limit approaching | Cap = start + 3 × the length of the campaign turn containing the start, fixed at the start (DIR5 §2.2; D-079 R15) | Participants | I, ongoing (countdown) | Tower with hourglass | "<n> days to the siege limit (<date>). At the limit the weaker side yields; the defender keeps the city on a tie" (H-S §2.5; the adjudication rule is itself a lane S proposal, H-S §10.2) | Open the siege card | C (ends with N-206) |
| N-208 Revolt in an occupied installation | `Revolt` (70): unrest reaches 1 while occupied (H-S §3) | The controller (occupier) | D (if approved, T1 pri 5) | Tower with raised fist | "<place> has risen against your garrison" (in the prototype a revolt has no further consequence, H-S §10.10) | Open the installation card | W, then E |
| N-209 Raid result | `RaidArrival` (66), `RaidRecord` | Raider; defender | I, dispatch (both); observers: as N-103 if they see the place | Torch | "Raid on <place> repelled / <grain> carried off" (the defender's own loss; the raider's own haul) | Open the installation card | E |
| N-210 Under siege | Siege state as the defender's own books: day, food at the current ration, ration level, garrison morale (H-S §2.3–§2.4) | Defender only | I, ongoing | Tower with sheaf | "Day <d>; food for about <n> days at <ration>" | Open the siege card (defender view) | C |
| N-211 Besieging | The besieger's own books: its siege health, supply, the walls' visible state | Besieger only | I, ongoing | Tower with tents | "Day <d>; your lines <state>; walls <state>" | Open the siege card (besieger view) | C |
| N-212 Garrison unpaid | Lane E arrears reused for garrisons (H-S §0.1, §3) | Owner | I, ongoing | Coin with gap over tower | "Garrison of <place> unpaid for <m> months; desertion rising" | Open the installation card | C |
| N-604 Garrison order refused | `SiegeRefusal` codes (Invested, Capacity, Eligibility, …) | The issuer | I, order note | Broken seal | "<place> is invested: no formation can enter or leave" | Open the installation card | N |

**Fog checks specific to sieges (§13.1).** The besieger's notices never show the defender's food, ration, morale,
disease or health; the defender's never show the besieger's supply or health. Lane S's model computes both sides' health
in one world; the knowledge layer must split them.

### 6.6 War outcomes ([H-W](../../prototypes/m7-sprint1/reports/H-W.md); DR36–DR38, C31–C33)

| Kind | Source | Recipients | Class | Icon | Hover | Click | Stack / expiry |
|---|---|---|---|---|---|---|---|
| N-301 Settlement taken | `SettlementTaken` (83, significant); lane S captures | Taker; former owner | Taker: D (if approved): the disposition is offered **only at this instant** (H-W L2); Resume applies Occupy. Former owner: D (if approved, T1 pri 0). **Lapsed form** *(r2, U2-5: added)*: when the capture is made by the taker's own order at T (lane S's immediate assault, issued at a siege pulse start, WA1), no hit is raised at T and the choice has passed by the next stop (§9 item 10), so the taker gets I, dispatch (unless it ordered a disposition in that same window after the assault order, which then applies and is reported as N-302), and the former owner a dispatch at its next stop | Gate with flag | Taker: "<place> is yours. Occupy (default), sack or raze — choose now". Lapsed form: "<place> is yours and occupied. Your assault took it after that moment's decisions had closed, so the choice to sack or raze it has passed." Former owner: "<place> taken by <polity>" | Taker: the disposition panel (Occupy, Sack, Raze with their eligibility from H-W §3.1, L13); lapsed form: the settlement card, never the disposition panel; former owner: the settlement card | W, then E; lapsed form: E |
| N-301 seen by an observer | Same, through the knowledge layer | Observers of the settlement | I, as N-103 | As N-103 | "<place> taken by <known polity or unidentified force>" | Battle report (observer view) | E |
| N-302 Settlement disposed | `SettlementOutcomeRow` (Occupy, Sack, Raze) | Taker: order note. Former owner and the population's polity: dispatch (refugees arriving are its own books). Observers of the place: within N-103 ("taken and razed"; a raze removes the settlement from their map) | I | Gate with flame (sack), gate struck through (raze) | Taker: what the choice did (loot, ruins). Others: what they can know (the fact; their own refugees) | The settlement card | E |
| N-303 Leader executed | `LeaderFateChanged` (84) to Executed, cause `LastCityLost` (C31) | The polity that took the last city (`ByPolity`), the captor if any (`HeldBy`), the participants of the wars the eliminated polity leaves; the eliminated polity, if it is the player, gets the defeat screen, not a notice | Taker and captor: D (if approved). `LeaderFateChanged` is significant in lane W's world (`WarOutcomeWorld.cs:452–454`, scheduled at `(T, Significant)`, `:709`), so it is a hit of their window at T, with no choice of its own (the related choice is N-301's). Participants of the other wars: I, dispatch (T1 pri 4 covers a polity's own ruler only; §4). *(r1, U-V3: it said "I, dispatch" for every recipient, while N-304 made the same kind a decision item.)* | Crown with black border (no depiction of the act) | "<polity> lost its last city. Its leader was executed. The war ended in conquest." | Open the war record card | W, then E (taker, captor); E (others) |
| N-303 seen by others | Knowledge layer (OPEN: §15, U-Q5) | Proposed if U-Q5 approves it: observers of the final capture. **A widening of WA3, off unless approved** (*r1, U-V4*) | I, dispatch | As above | As above | As above | E |
| N-304 Leader taken captive | `LeaderFateChanged` to Captive, cause `SeatTaken` | Captor; the captive's polity | The captive's polity: D (if approved, T1 pri 4: its own ruler is held). Captor: D (if approved: `LeaderFateChanged` is significant in lane W's world, `:452–454`, `:709`; T1 pri 4 does not cover another polity's ruler). Then ongoing "captive leader held" (captor) / "your leader held by <polity>". *(r1, U-V3: it said "Both: D (if approved, T1 pri 4)".)* | Crown with chain | Captor: "<leader> is your captive: hold, release, ransom" (execute only if the Director-level rule permits, H-W DQ1; then N-310). Captive's polity: "Your leader is held by <polity>" | The leader panel | W, then C |
| N-305 Leader freed | `FateCause.Released` / `Ransomed` | Both parties | I, dispatch | Crown, open chain | "<leader> released / ransomed for <price>" | The leader panel | E |
| N-306 Prisoners taken | `PrisonerAction.Intake` (garrison taken, men surrendered), `BattleResolved` (86) window | Captor; origin | Captor: D (if approved) at the post-battle window, then ongoing "prisoners held: decide at any window" (H-W §3.1). Origin: dispatch, then ongoing "<n> of your men held by <polity>" | Chain link | Counts by origin; rations unpaid if any (H-W §3.2) | The prisoner panel | W, then C |
| N-307 Prisoner action | `PrisonerAction` Release, Conscript, Enslave, Ransom, Exchange | Actor: order note. Origin: dispatch for Release, Ransom and Exchange only (its men come home: its own books). Enslave and Conscript are the captor's books: the origin sees only that its men are still held | I | Chain link, arrow | Actor: what the order did. Origin: "<n> of your men returned (parole / ransom / exchange)" | The prisoner panel | N (actor), E (origin) |
| N-308 Standing terms or offer | `OrderRansomTerms` (202), `OrderExchange` (204) | Issuer: order note. Counterpart: dispatch (it needs to know the offer to meet it, H-W L9, L10) | I | Scales | "<polity> offers to exchange up to <n> prisoners" | The prisoner panel | E (counterpart), N (issuer) |
| N-309 War ended | `WarRow` outcome Conquest or MutualCollapse (`NegotiatedPeace` is reserved for M8) | Participants; observers who observed that war only if U-Q5 approves it (**a widening of WA3, off unless approved**; *r1, U-V4*) | I, dispatch | Two banners, lowered | "The war between <…> and <…> ended: conquest / both sides collapsed" | Open the war record card | E |
| N-310 Captive leader executed (*r1, U-V6: added; the branch implements this fate*) | `LeaderFateRow` to Executed, cause `ExecutedAsCaptive` (5): the captor's `LeaderDecision.Execute`, accepted only under `CaptiveExecutionRule.Permitted`, a Director-level flag (C32; H-W DQ1) (`OutcomeTypes.cs:105–106`; `WarOutcomeWorld.cs:1035–1037`). The polity is not eliminated and the war goes on (H-W §3.2); it stays leaderless (H-W DQ4). Being an order, it raises no window hit at its instant: `ExecuteLeader` writes the fate row and schedules nothing (`:713–723`; §9 item 10) | The captor; the leader's polity (Origin); the war's other participants | Captor: I, order note. The leader's polity: D (if approved, T1 pri 4: its ruler's death); as built no hit is raised for it (§9 item 10), so until the producer raises one it reaches the polity as a dispatch at its next stop. Other participants: I, dispatch. Outside the war: as N-303 seen by others (U-Q5, off unless approved) | Crown with black border (no depiction of the act) | Captor: "<leader> executed". The leader's polity: "Your leader, held by <polity>, has been executed." Other participants: "<polity>'s leader, held by <polity>, was executed" | The leader panel | N (captor); W, then E, at its next window if a hit is raised there, else E (the leader's polity; *r2, U2-7*: it said "W at the next stop if a hit is raised"); E (others) |

**War records versus the log.** The war record (C33: wars, battles, participants, outcomes, casualties, leader fates) is
simulation state. The Dispatch log is the player's UI-side history of what it learned. A participant's Battle report
reads its own rows of the record (its own casualties); an observer's report reads only its observer rows (§10.1).

**Execution and the content setting.** The notice states the fate in plain words whenever the record says Executed
(INV-10). The content setting of D-079 Q12 gates depiction in cinematics (DR41); it does not hide the fact. The icon never
depicts the act. READING, flagged (§15, U-Q8).

### 6.7 War economy ([H-E](../../prototypes/m7-sprint1/reports/H-E.md); D-080 WA4)

Prototype facts (MEASURED by reading `WarEconomyWorld.cs`): `CascadeKind` has 20 members (`:41–57`). `HungerBegan` and
`MovementRestricted` are emitted at **every** pulse start at which the condition holds (`OnPulseStart`, `:662–663`), not
only when it begins, so they are conditions, not events. `ArrearsBegan` is emitted once, at the pulse end at which
arrears start (`OnPulseEnd`, `:882`); `ArrearsCleared` when they reach zero (`:703`, `:796`). Each `CascadeEvent`
carries one `Polity` field, which is the army's owner for most kinds but the locality's owner for `Plunder`,
`LocalFamine`, `Occupation` and `OccupationEnded` (`:445`, `:779`, `:803`, `:956`); a contribution levied on a locality
is logged under the army's owner only (`:515`, `:520`). The published log therefore needs one row per recipient
(§10.1), not one polity field per event.

**Observers receive nothing in this section (WA3-R).** Victims receive only effects on their own books (F3).

| Kind | Source (`CascadeKind`) | Recipients | Class | Icon | Hover | Click | Stack / expiry |
|---|---|---|---|---|---|---|---|
| N-401 Pay in arrears | `ArrearsBegan` → `ArrearsCleared` | Owner | D-candidate (H-E §9 lists `ArrearsBegan` as a Q2 candidate); default: I, ongoing, as N-404's mutiny. *(r2, U2-4: it said "I, ongoing (H-E §9 lists ArrearsBegan as a Q2 candidate)", against §4's rule.)* | Coin with gap | "<army>: <m> months unpaid; desertion <rate>‰ a year; morale <x>" (own books, `ArmyPulseRow`) | The army card | C; begin and end logged |
| N-402 Army hungry | `HungerBegan` (per pulse) | Owner | I, ongoing | Sheaf with gap | "<army>: <share> of its ration missing" | The army card | C |
| N-403 March slowed by supply | `MovementRestricted` (per pulse) | Owner | I, ongoing | Boot with gap | "<army> marched <share> of its planned distance" | The army card | C |
| N-404 Mutiny | `MutinyBegan`, `MutinyEnded` | Owner | D-candidate (H-E §9); default: dispatch at both ends and ongoing "mutinous" in between | Broken banner | "<army> has mutinied: it refuses to march and plunders where it stands" (`MutinousArmy_RefusesToMarch`) / "The mutiny of <army> is over" | The army card | Dispatch E; ongoing C |
| N-405 Army disbanded | `Disbanded` (strength under 10 %, or 7 days at zero cohesion while mutinous) | Owner (victim of an occupation: N-409 ends) | D-candidate (H-E §9); default: dispatch | Scattered marks | "<army> disbanded: <n> men walk home; its stores return to <polity>" | The empire's army list | E |
| N-406 Stocks taken from your locality | `ForcedRequisition`, `Plunder`, `Contribution`, `ContributionInKind` | Victim (the locality's owner); the acting army's owner sees only "living off enemy land" in its army card | I, dispatch for the victim | Sheaf and coin, arrow out | "<grain / money> taken from <place> by <known polity or unidentified force>" (own loss only) | The settlement card | Stack by kind; E |
| N-407 Famine in a locality | `LocalFamine` (famine risk after requisition: the stock is below the locals' reserve, not yet out; `WarEconomyWorld.cs:775–780`), `LocalityFamineDeaths` (turn-keyed: the stock ran out over the interval, so people died; `:1029–1033`) | The locality's owner | Deaths form: D (if approved, T1 pri 7 famine onset) at the first settlement with deaths; lane E emits one at every such settlement, and the later ones are dispatches. Risk form: D-candidate (H-E §9); default: dispatch. *(r1, U-V3: the whole kind was labelled D-candidate although its deaths form is T1 pri 7's famine onset.)* | Empty bowl | "<place>: stores below what its people need" (risk) / "<place>: the food has run out; <n> dead" (deaths) | The settlement card | W, then E (deaths); E (risk) |
| N-408 War credit | `Borrowed`, `CreditDefault` (turn-keyed) | The polity | `CreditDefault`: D-candidate (H-E §9); default: dispatch. `Borrowed`: log line | Coin with seal / broken seal | "Your lenders have closed credit after a missed interest payment" | The empire's war accounts | E / L |
| N-409 Enemy force occupies your locality | `Occupation`, `OccupationEnded` | Victim | I, ongoing | Banner on a town | "<force> stands in <place>: its trade income is lost to you" | The settlement card | C |
| N-410 Civilian famine | `CivilianFamine` (turn settlement: the home granary, lane E's stand-in for the settlements' stocks, could not feed the people; `WarEconomyWorld.cs:970–976`) | The polity | D (if approved, T1 pri 7 famine onset) at the first settlement with famine; later ones are dispatches. *(r1, U-V3: it said "D-candidate (T1 pri 7)", contradicting §4's rule for kinds on T1's list.)* | Empty bowl | "Famine at home: <n> short" | The empire's food view | W, then E |
| N-411 March complete | `Arrived` | Owner | I, dispatch | Boot with flag | "<army> reached <place>" | The army card | E |
| N-412 Port call | `PortCall` | Owner | I, log only | — | — | — | L |
| N-413 Ship's company in distress | Scurvy rate rising (`ScurvyPermilleYear`), bunker coal short (H-E §2 rows 20, 22) | Owner | I, ongoing | Hull with sheaf / with coal | "<squadron>: scurvy <rate>‰ a year" / "lies at anchor for want of coal" | The squadron card | C |
| N-414 The treasury cannot pay the armies | Derived for presentation: two or more armies in arrears, or credit closed (an aggregate of N-401 and N-408; no new state) | The polity | I, ongoing summary | Coin, struck | "<n> armies unpaid; credit closed" | The empire's war accounts | C |

**Desertion** is a rate, not an event (H-E §2 row 14). It appears inside N-401, N-402 and N-404 (the hover shows the
army's latched rate and the men who left this pulse) and in the army card. No separate notice: the rate changes every
pulse and would repeat on the rail.

### 6.8 Inside a battle: naval, air and ramming ([H-N](../../prototypes/m7-sprint1/reports/H-N.md), [H-RAM](../../prototypes/m7-sprint1/reports/H-RAM.md); D-081)

These are not campaign notices. They appear in the **battle event feed** of the tactical battle view (M7.4/M7.10
presentation), for the side whose entities detect them (battle fog, C7), and their totals appear in the Battle report.
They never reach the campaign rail and never reach an observer outside the battle.

| Kind | Source | Recipients | Class | Icon | Hover | Click | Expiry |
|---|---|---|---|---|---|---|---|
| N-501 Hull sunk | Hull at or below 0 at Resolve; survivors by `ShipwreckSurvival` (H-N §3) | The owner; the enemy if it detects the hull | I, feed | Hull, waves over | "<vessel> sank; <n> saved, <m> lost with her" (own); "Enemy hull sunk" (enemy) | Centre on the place | With the battle |
| N-502 Hull taken | Boarding capture (aboard ≤ ¼ at fight start and the boarder has more people) or a grappled hull routing | Both sides | I, feed | Hull with grapnel | "<vessel> boarded and taken; crew captured" | Centre | With the battle |
| N-503 Ram struck | `DomainKinds.Ram` (124), `RamOutcome.Breach` / `Glance` (D-081 RM3–RM4) | The rammer's side; the rammed side | I, feed | Prow | "Rammed <vessel> at <speed>: holed" / "glancing blow, no breach" | Centre | With the battle |
| N-504 Bow contact, no ram | `DomainKinds.RamCollision` (125): below ramming speed, or a pair already rammed (RM1) | The rammer's side | I, feed | Prow, struck | "Collision only: <vessel> has already rammed this ship" / "too slow to ram" (explains RM1 and RM3 to the player) | Centre | With the battle |
| N-505 Troops lost with a vessel | `LostWithVessel` personnel flow | The owner | I, feed | Hull with figures | "<n> soldiers lost with <transport>" | Centre | With the battle |
| N-506 Landing made | Disembark onto passable land; troops exposed for 20 s (H-N §1) | The owner; the enemy if it detects the landing | I, feed | Boat to shore | "<formation> landed at <place>" | Centre | With the battle |
| N-507 Aircraft lost or grounded | Shot down; or grounded at the damage-abort threshold (N-R1-06) | The owner; the enemy if detected | I, feed | Wing, struck | "<aircraft> lost / grounded for the rest of the battle" | Centre | With the battle |
| N-508 Sortie returned | Bingo fuel, empty racks or damage (H-N §1) | The owner | I, feed | Wing, return arrow | "<flight> returning: <reason>" | Centre | With the battle |
| N-509 Formation routed or surrendered | Rout; surrender; DIR5 five-pulse rule (N-104) | The owner; the enemy if detected | I, feed | Broken banner / white flag | "<formation> routed / surrendered" | Centre | With the battle |

A naval battle's campaign result is an N-102 or N-103 like any other. Lane W's records do not yet distinguish a naval
battle (`BattleKind` has Field, Siege, Storming; H-W limitation 15), so the "naval outcome" distinction that C35 keeps
needs a domain field on the record before the notice can show it.

### 6.9 Non-war significant kinds (T1 entries 4–9; content of other milestones) *(r1, U-V3: added)*

No prototype on the branch emits these kinds. They are here so that the tray can show every T1 entry with an icon, a
hover, a click and an expiry; their sources, knowledge rules and wording belong to the milestones named. Every one is
"D (if approved)" for its recipients by §4's rule, because each is on T1's proposed list.

| Kind | Source | Recipients | Class | Icon | Hover | Click | Stack / expiry |
|---|---|---|---|---|---|---|---|
| N-701 Ruler died; succession or regency | T1 pri 4 (no succession rule exists yet: H-W DQ4) | The ruler's polity | D (if approved, T1 pri 4) | Crown with ribbon | "<ruler> has died; <heir> succeeds" / "A regency rules for <heir>" | The leader panel | W, then E |
| N-706 Revolt, secession or civil war in your territory | T1 pri 5 (the occupied-installation case is N-208) | The territory's owner | D (if approved, T1 pri 5) | Town with raised fist | "<place> has risen: revolt / secession / civil war" | The settlement card | W, then E |
| N-702 Wonder-class construction completed | T1 pri 6 (outside M7) | The builder | D (if approved, T1 pri 6) | Monument | "<work> completed at <place>" | The settlement card | W, then E |
| N-703 Famine or epidemic | T1 pri 7 (a settlement's food stock reaches 0; an epidemic breaks out); the war-caused forms are N-407 and N-410 | The settlement's owner | D (if approved, T1 pri 7) | Empty bowl / fever mark | "<place>: its food has run out" / "Sickness has broken out at <place>" | The settlement card | W, then E |
| N-704 Offer or demand with a deadline | T1 pri 8 (a peace offer, ultimatum or call to arms with an in-turn deadline; M8) | The addressee | D (if approved, T1 pri 8) | Sealed letter | "<polity> offers peace / makes a demand / calls you to arms; answer by <date>" | The offer's panel (M8) | W, then E |
| N-705 First contact | T1 pri 9 | Both polities | D (if approved, T1 pri 9) | Two figures meeting | "First contact: <polity>, near <place>" | Centre the map on the place of contact | W, then E |

---

## §7 Icons, hover, click and keyboard

### 7.1 Icons (our own, PROPOSAL)

- **Frame by class**, so the class never depends on colour: decision items in a square frame; dispatches in a round
  frame; ongoing notices in a hexagonal frame; order notes as an inline seal.
- **Glyph by family**, drawn in the parchment ink style of the existing art substrate: crossed blades (battle), tower
  (siege), gate (settlement), crown (leader), chain link (prisoners), coin (pay and credit), sheaf (food), boot (march),
  banner (war, mutiny), hull and prow (naval), wing (air), eye mark (observed, not fought).
- **Valence mark** beside the glyph: up (gain), down (loss), dot (neutral). Colour repeats the valence but never carries
  it alone (§11).
- **Provenance.** Every icon is made for this game and recorded in `AssetManifest` with its source (D-069 R3 item 5;
  DR47). None is taken or traced from another game.

### 7.2 Hover, click, dismiss, keyboard

| Gesture | Decision item | Dispatch | Ongoing notice |
|---|---|---|---|
| Hover (or keyboard focus) | Expands to title and two lines; shows what can be done and whether the choice lapses at this instant | Expands to title and one or two lines; a stack shows "1 of n" and cycle arrows | Expands to the condition and its current figures |
| Left click / Enter | Opens the decision's panel in the context column, below the tray (*r2, U2-1*), and centres the map on the subject if the player knows its place | Opens the subject's DETAILS card (the Battle report for battle kinds) and centres the map | Opens the list of subjects (for example, every army in arrears), each row clickable |
| Right click / Delete | Not available: the window holds the item; Resume releases it | Dismisses this notice (its log line stays) | "Quiet until it changes": hidden until its count or subject set changes |
| Shift + right click | — | Dismisses the whole stack | — |
| Arrow keys | Move between items | Cycle within a stack; move between stacks | Move between notices |

A click or right click on the rail or tray is consumed there and never reaches the map (a known criticism of the
reference pattern is a dismissal click that also moved a unit, Annex R, R-12). On the first appearance of a dispatch the
rail does not auto-expand it for observers (WA3: "if they click it"); for the player's own dispatches a one-time
expansion of at most a few seconds of UI time is optional and off under "reduce motion" (§11).

---

## §8 Stacking, expiry, dismissal and the log

All rules are pure functions of the published rows, the open window, the latest published instant and the UI state.
"Turn" is the campaign turn; `S(t)` is the start instant of turn `t`; `TurnOf(x)` is the turn containing instant `x`.

| Class | Lives | Stacks by | Dismissable | On expiry |
|---|---|---|---|---|
| Decision item | From the window instant T while the window is open; gone from the tray at Resume | Not stacked: one tray entry per hit, in the window's key order; an entry may hold several items (§9 item 1). *(r1, U-V2: it said "Kind, within the tray", which reorders hits of one priority that the window interleaves by source.)* | No | At Resume it takes the form its catalogue code names after "then" (§6): E, a dispatch on the rail from T until `S(TurnOf(T) + 2)`, as any dispatch learned at T; L, its log line only; C, an ongoing notice while the condition holds (for example "prisoners held"). Its one log line is written at T in every case. *(r2, U2-7: it said "Becomes a log line; any lasting consequence becomes an ongoing notice", against the catalogue's "W, then E" rows.)* |
| Dispatch | From `KnownAtMs` until `S(TurnOf(KnownAtMs) + 2)`: it survives one End Turn | Kind (one icon, a count, members in canonical order, the newest shown) | Yes, singly or by stack | Leaves the rail; the log line stays |
| Ongoing notice | While its condition holds in the latest published state | Kind (the count is the number of subjects) | Quietable until the condition changes | Leaves the rail when the condition ends; begin and end are logged |
| Order note | From the order's instant until the next stop or the next turn start, whichever is first | Not stacked | Yes | Log line |
| In-battle feed item | For the battle | Not stacked | — | Summarized in the Battle report |

- **Why "survives one End Turn" (INFERRED).** A dispatch learned early in a turn is still on the rail at the turn's end,
  and one learned at the turn's last window is still there for the whole next turn. Expiry in simulation days would
  behave very differently across the era table's turn lengths (3,600 to 180 days); expiry in turns does not.
- **Pinning.** A dispatch can be pinned; a pinned dispatch stays until unpinned.
- **The log.** Every notice, every class, appends one log line at its `KnownAtMs`, in canonical order, whatever happens to
  it on the rail. The log is shown in the ANNALS section with a filter (all, war, dispatches) and keeps the section's
  "recent / all n entries" behaviour (`DrawAnnalsSection`, [`GameUi.cs`](../../Sim.Ui/GameUi.cs) `:2582–2602`). Ongoing
  notices log their begin and their end only, never a line per pulse (§6.7: two of the prototype's kinds re-emit every
  pulse).
- **UI state.** Dismissed, pinned and quiet flags and the focus are UI state. They never enter `WorldState`, a hash or
  the order log. PROPOSAL: keep them in a versioned UI sidecar beside the session files (the avatar-profile precedent,
  ADR-062 R-5); with no sidecar, nothing is dismissed. A resumed session rebuilds the rail and the log by replay, as
  `UiSession` rebuilds the annals today.

---

## §9 Decision windows and notices

1. **The window is the authority; the tray is its view.** The tray has exactly one **entry** per hit of the player's open
   window, in the window's own key order (class, priority, source, sequence; T1 R1–R2). An entry holds the decision
   items its hit brings, usually one and sometimes more, ordered within the entry by `(KindRank, LogSeq)`. For example, a
   captor's battle result and the prisoners taken in it come from one `BattleResolved` hit (`WarOutcomeWorld.cs:879`):
   lane W records the prisoner intake as a row (`:875–878`), and no intake kind is in its significant set (`:452–454`).
   The window's key and the canonical key of §10.3 are different keys, and this design does not need them to agree:
   - class and priority are the scheduler's. Lane W schedules `SettlementTaken`, `LeaderFateChanged` and
     `BattleResolved` at priorities 0, 1 and 2 (`:708–709`, `:879`), and lane S its significant kinds at 0
     (`SiegeWorld.cs:600`, `:910`, `:1381`), so kinds of one priority interleave by source;
   - significance is independent of class (ADR-061 §3.6);
   - one kind can serve different T1 entries for different recipients (N-301: pri 0 for the former owner, pri 3 for the
     taker).
   An invariant test pins one entry per hit and one hit per entry, in the window's order (§13.2, O2). *(r1, U-V2: it
   said the tray had one item per hit and that the window's order was the canonical order once `KindRank` held the
   approved priority. Neither holds in general.)*
2. **Actions.** The tray header shows the two-action budget of the enclosing window (T3: per polity per War Pulse or per
   turn) and, per agent, its Action Capacity. Under reading R8 as applied (T3 Q2), an event-triggered window does not
   refresh the slots, so the tray may have to say that no agent action is left. This bears on DIR5 §2.5 ("Players must
   be able to act on significant events …"); the tray makes the state visible, it does not decide R8. Post-battle orders
   (dispositions, prisoner actions, ransom terms, exchange offers, leader decisions) do **not** count against the
   two-action maximum as built ([H-W](../../prototypes/m7-sprint1/reports/H-W.md) DQ8, open for the Director), so the
   wording must not suggest that nothing can be done: "No agent actions left in this War Pulse; the decisions below are
   still open" when the window holds such a decision, and "No agent actions left in this War Pulse; you can review and
   resume" only when it holds none. If the Director rules that they count, the two cases merge. *(r1, U-V12: the
   wording ignored DQ8; a taker with no slots left could still dispose of the city.)*
3. **Resume** closes the window. Each decision item leaves the tray and takes the form its catalogue code names after
   "then" (§6, §8): a dispatch on the rail (E), its log line only (L) or an ongoing notice (C). *(r2, U2-7: it said
   "Decision items become log lines" and "Lasting matters become ongoing notices", while most catalogue rows read
   "W, then E".)* Choices not taken take their defaults (Occupy; Hold; at a battle joined, the TacticalResolver, rule C3;
   *r1, U-V12*).
4. **Several windows in one turn.** Each window replaces the tray. Nothing accumulates on the tray; the rail and the log
   keep the history.
5. **No window from the UI.** No notice and no UI action opens a window (C5). Observers' events never open the observer's
   window (C4).
6. **Order delivery.** An order given at a window applies at T (T1); a pulse or turn order applies at the first boundary
   at or after T (T2; D-080 WA1). The order note says which (N-603), and, where no boundary comes (the war's last pulse,
   the campaign's last turn), it promises no instant (*r2, U2-6*).
7. **Replay.** Replay never stops but records the same windows (T1 R4). The tray and the rail rebuilt from a replay equal
   the live ones (§13.2, O3).
8. **AI windows are invisible.** AI polities receive windows under the same rules (ADR-061 §3.6); the UI reads only the
   player's.
9. **Auto-continue (Director question U-Q6).** A player-chosen list of kinds for which the UI resumes at once would be
   presentation only: the window still exists, nothing in the world changes, and T3 AE4 already pins that the action
   budget is identical whether a window is shown or skipped. It would match the placeholder's "player triggers". It is
   not proposed for kinds whose choice lapses at the instant (N-301 for the taker). Default: off.
10. **Consequences of an order given at the window.** Orders at T execute after every world event at T, when the window
    at T has closed, so a capture caused by an order at T (lane S's immediate assault) raises no hit at T; lane W pins
    this for the leader-fate change ([H-W](../../prototypes/m7-sprint1/reports/H-W.md) §2.2, limitation 11). Its notices
    reach the player at the next stop, as dispatches or as items of that later window. The tray cannot show them at T;
    changing that would be an ADR-061 change, not a presentation one. *(r2, U2-5: added)* The same holds for the
    capture's `SettlementTaken` hit, and with it the taker's disposition, which lane W offers only at the capture instant
    (H-W L2). MEASURED in verification round 2 on lane W's rig (an immediate assault order at a siege pulse start that
    storms the city; lane report §11): the only hit at T is the test probe's; a Sack ordered at the player's next stop is
    refused (`WindowClosed`, `WarOutcomeWorld.cs:756`) and the city stays occupied; an AI taker on the same path sacks at
    T by doctrine, because the settle step that lets AI victors dispose runs without a scheduler (`:665`, `:698–703`);
    and a Sack issued in the same window as the assault order, before any capture is shown, is accepted when the assault
    takes the city, since orders run in log order. So the tray cannot offer that choice at T and the rail must not offer
    it later: the taker gets N-301's lapsed form (§6.6), pinned by E8. Whether the player should keep the choice on this
    path, and the difference from an AI taker, are for the Director (§15, U-Q11).

---

## §10 Data contract

### 10.1 Input: the per-polity published event log (a requirement on the knowledge layer; not built)

The prototype worlds publish per-polity knowledge only in T2 (`KnowledgeEntry(AtMs, WarId, PulseIndex, Kind)` and the
fog-filtered `WindowsFor`); the siege world has no fog (H-S §11); lane E's events carry one polity field (§6.7); lane W's
records are authoritative rows with no recipient. WA3 therefore needs the knowledge layer (C7: M7.8, M7.12; C19's
observation rule: M7.14; *r1, U-V5*) to publish, at
the instant a polity learns something, one row per recipient:

| Field | Type | Meaning |
|---|---|---|
| `Recipient` | polity id | Who learned it |
| `LogSeq` | i64, dense per recipient | Append position in that recipient's log (0, 1, 2, …); never a global sequence |
| `KnownAtMs` | i64 | Instant the recipient learned it (global milliseconds, ADR-061 §3.1) |
| `EventAtMs` | i64 | Instant it happened (≤ `KnownAtMs`) |
| `Kind` | u16 | Notice kind (§6) |
| `Relation` | u8 | Participant, Owner, Victim, Captor, Origin, Observer |
| `SubjectId` | i64 | The battle, siege, war, settlement, army or leader polity the kind is about (one id space per kind); for an order note, the order's log index |
| `WarId`, `Pulse`, `Turn` | i32, i64, i64 | The war (or none), its War Pulse index and the campaign turn of `KnownAtMs` |
| `PlaceNode` | i32 | Lattice node of the place, or −1 |
| `PartyA`, `PartyB` | polity ids | The parties as the recipient may name them; −1 = unidentified (F4) |
| `Outcome` | u8 | Public outcome class (§6.4), or 0 |
| Payload | fixed fields by relation | Only what the relation allows (F2, F3): own casualties by cause for a participant; the amount taken for a victim; counts for a captor or origin; **no numeric field for an observer in M7** |

Rules for the producer: rows are appended in scheduler key order at the knowledge instant; observer rows are a separate
row type with no book fields (a leak becomes impossible by construction, as lane W's records carry no treaty field by
construction); a POPULATED-table round-trip test per row type (`CLAUDE.md` rule; T1.1/T1.3 precedent); *(r2, U2-6:
added)* an order that waits for a boundary that never comes (its war ends, or the campaign does, before the next pulse
or turn start) gets a lapse row at that end instant for its issuer, because T2 writes neither a latch nor a refusal for
it (`WarTheaterWorld.cs:380–385`, `:367`; MEASURED in verification round 2, lane report §11). Because AI
polities must decide on the same information (Spine Law 7), the log is a candidate for simulation state, hashed and
serialized. **That is an ADR for the packet that builds it (schema change, Tier 3), not decided here (§15, U-Q1).**

Alternative, if no such log is built: the UI derives participant and owner notices from `(prev, next)` reads per
sub-step, as [`StateChronicle`](../../Sim.Ui/ViewModel/StateChronicle.cs) derives annal events per step. This needs no
schema change but cannot know the learned instant inside a sub-step, cannot serve the AI, and cannot produce observer
notices at all (an observer's knowledge depends on where its observers stood at the instant). Not recommended for WA3.

### 10.2 Output: the presentation query (UI side; PROPOSAL)

`NotificationFeed.Build(knowledge, rows, window, uiState, nowMs, calendar)` returns `FeedView`:
- `knowledge`: the player's `PlayerKnowledge` for the published state;
- `rows`: the player's published rows with `Recipient` = the player (nothing else is passed);
- `window`: the player's open decision window view (`PolityWindow`: instant and hits), or none;
- `uiState`: dismissed, pinned and quiet keys; settings (§11);
- `nowMs`: the latest published instant the UI shows;
- `calendar`: turn starts, for `S(t)` and dates.

`FeedView` holds `Tray` (one entry per hit of the open window, in the window's key order, each with its decision items,
§9 item 1; *r1, U-V2*), `Rail` (stacks: kind, class, count, newest key, title,
summary, icon, valence, members), `Ongoing`, `Log` (lines in canonical order) and, for each item, the `InfoSubject` its
click opens. It is a pure, deterministic function: no state is written, nothing is cached across worlds, no dictionary is
iterated, no LINQ, culture-invariant formatting (the `PlayerKnowledge` conventions). It draws no RNG of any kind.

### 10.3 The canonical order

Every list except the tray is sorted ascending by the composite key **`(KnownAtMs, KindRank, SubjectId, LogSeq)`**, all
integers. The tray follows its window's key (§9 item 1). *(r1, U-V2: it said every list.)*
- `KindRank` = one integer per kind, from one data table (proposed: the kind's number in §6, so N-101 sorts before
  N-102). It is not the significance priority: the tray takes its order from the window, so no list needs the two to
  agree. *(r1, U-V2: it was the approved priority for significant kinds and 1000 + the kind's code otherwise. One kind
  that serves two T1 entries, N-301, cannot hold two priorities.)*
- `SubjectId` breaks ties between subjects of one kind at one instant (ids are stable integers, never hashes).
- `LogSeq` makes the order total (two notices of one kind about one subject at one instant, for instance two relations).
- Display order derives from it: rail stacks by their newest member's key, the newest lowest, just above the ongoing
  notices, which sit at the foot (nearest the legend tab) in `KindRank` order; the log in canonical order; within a tray
  entry, `(KindRank, LogSeq)`.

No score is compared as a double anywhere in this design; if one ever is, it enters as `(score, id)` with a tie-dense test
(`CLAUDE.md`).

---

## §11 Accessibility

- **Never colour alone.** Class by frame shape, valence by a mark, family by glyph; colour repeats them (§7.1). Contrast
  through the era theme's ink tokens, checked as `EffectiveContrastTests` does today.
- **Text first.** Every notice has a full sentence (its log line), written to `FrameText` so headless tests and any text
  reader see exactly what the screen shows (the `Notices_AreRecordedUnderTheirOwnSurface_…` precedent,
  `Sim.Ui.Tests/FrameTextTests.cs:166`).
- **Keyboard.** Every gesture of §7.2 has a key path; a key focuses the rail and another the tray; Space releases the
  window (Resume) through the existing `EndTurnKey` predicate.
- **No time-limited reading.** Nothing the player must read disappears on a wall-clock timer: hover expansions collapse,
  but the text stays one hover, one focus or one log line away; expiry is in simulation turns (§8).
- **Reduce motion.** A setting turns off slide-outs and first-show expansions; the rail then changes in place.
- **Sound.** None in this proposal: `Sim.Ui` has no audio subsystem today (MEASURED: no audio type found by grep), and
  asset licensing is open (D-079 Q8). If added later, a cue per valence, optional, never the only signal, with a volume
  setting.
- **Scale.** Type sizes follow the existing type roles and UI scale; the rail's icon size scales with them.

---

## §12 Performance budget (PROPOSAL; nothing here is measured)

DR48: "Proposed targets in earlier reports are not approved budgets. Measure first." DR50: the earlier targets "are
proposals, not approved targets", and "The Victus benchmark must precede final numeric performance budgets." These are
structural bounds and numbers to measure, not claims. *(r1, U-V8: the first sentence was attributed to DR50; it is
DR48's, `d079:108`.)*

| Item | Bound (INFERRED from the design) | To measure when built |
|---|---|---|
| Feed build | O(new rows) per published sub-step to append; O(k log k) to order the k live items (k ≤ a few hundred) | Build time per sub-step with 10³ live items and 10⁵ log lines |
| Per frame | O(visible): at most the stacks that fit between the rail's foot and top (§3) plus the tray's visible entries in the context column (at most 8) (*r1, U-V1, U-V2*: it said "about 12 at 1280 × 800", a count derived from the old rail anchor; *r2, U2-1*: it said "between the legend tab and the top band (§3) plus the tray") | Paint time of rail and tray at 1280 × 800 and at the opening window, on the reference machine (DR48) |
| Memory | One log line per notice; ongoing notices log begin and end only | Log size after a long war (lines, bytes) |
| Flood control | No per-pulse notices (§6.2); per-pulse conditions are ongoing notices | Notices per turn in the densest war of a soak run |
| Simulation cost | Zero: the builder runs after a step, reads published rows, writes nothing | A renderer-invariance style check: world hash identical with the panel on, off and clicked (§13.3, E5) |

---

## §13 Test plan (for the implementing packet; nothing here is run)

Each test belongs in `Sim.Ui.Tests` when the packet is authorized; the leak tests extend `KnowledgeLeakTests` with a
notifications family on both of its lenses (the production fog as the gate of record; the stub as a control) and keep
its positive-control discipline.

### 13.1 Leak tests (an observer never sees what fog hides)

| # | Test | Positive control |
|---|---|---|
| L1 | Perturb everything the observer's fog hides (a distant war's battles, sieges, economics; a hidden polity's armies): the observer's tray, rail, log, Battle report card and `FrameText` are identical | The all-known view: the same perturbation changes them |
| L2 | Perturb a foreign army's books (arrears, hunger, mutiny, disbandment): the player's feed is identical, **even all-known** (books rule, D-060) | The owner's view changes |
| L3 | Timing: the instants at which the UI stops, and everything shown while the clock runs, are identical with and without hidden significant events | A known significant event adds a stop |
| L4 | Sounds and animations triggered are identical under L1's perturbation | All-known view differs |
| L5 | Schema guard: the observer row type and the observer report view have no casualty, siege-internal or economy field (reflection test, as lane W's no-treaty-field test) | — |
| L6 | An observed battle with a combatant the observer has no contact with: the text says "unidentified force"; that polity's name and id appear nowhere in the feed or `FrameText` | With contact, the name appears |
| L7 | Source guard: the feed builder reads only `PlayerKnowledge` and the player's published rows (allowlist), never a raw war table | An injected raw read in an unlisted file is caught (the `SourceGuard_HasTeeth_…` precedent) |
| L8 | No raw id in player text; `LogSeq` is dense per recipient (gaps would count other polities' events) | — |
| L9 | Siege split: the besieger's notices and cards never show the defender's food, ration, morale, disease or health, and the reverse | The defender's own card shows them |
| L10 | *(r1, U-V4: added.)* Observer kinds: with the widenings of U-Q5 and U-Q10 off (the default), every notice an observer receives is a battle result (N-103 and the observer forms of N-206 and N-301); switching one widening on adds exactly its kinds | A participant's feed holds non-result kinds |

### 13.2 Ordering

| # | Test |
|---|---|
| O1 | **Tie-dense:** at least 64 notices at one instant across at least 3 kinds, with at least 40 exact `(KnownAtMs, KindRank)` ties and repeated subjects; three or more input permutations give one identical order; `SubjectId` ascending within a kind; `LogSeq` decides the remaining ties. The fixture is built by hand (the builder is a pure function of its rows) and **must include tied pairs whose `LogSeq` order is the reverse of their `SubjectId` order**: rows appended in scheduler key order, where the source id is the subject id (lane W, `WarOutcomeWorld.cs:708–709`, `:879`), would make `LogSeq` follow `SubjectId`, and a sort without `SubjectId` would then pass. *(r1, U-V7: the reversed pairs were not required.)* |
| O2 | Tray = window: one tray entry per hit of the open window and one hit per entry, in the window's key order (class, priority, source, sequence). Fixtures include windows whose order differs from the canonical order (hits of two priorities; and hits of two kinds at one priority whose source order is the reverse of their `KindRank` order, as lane W's `SettlementTaken` and lane S's `SiegeEnded` can be at priority 0) and a `BattleResolved` hit with prisoners taken, which gives one entry holding two items (N-102 and N-306). *(r1, U-V2: it said "exactly one tray item" per hit.)* |
| O3 | Replay: live play, a run with stops off and a replay give identical feeds at every window and turn start |

### 13.3 Expiry, stacking and delivery pins (instant-exact, with ±1 ms neighbours)

| # | Test |
|---|---|
| E1 | A dispatch learned in turn t: present at `S(t+2) − 1 ms`, gone at `S(t+2)` and at `S(t+2) + 1 ms`; dismissing it hides it at once; its log line stays |
| E2 | A decision item: absent at `T − 1 ms`, present at T while the window is open, gone from the tray after Resume; never dismissable. *(r2, U2-7: added)* At Resume it takes its catalogue form: a "W, then E" item (N-102) is on the rail at T after Resume, still there at `S(TurnOf(T) + 2) − 1 ms` and gone at `S(TurnOf(T) + 2)`; a "W, then L" item (N-101) is on neither the tray nor the rail; a "W, then C" item (N-306) is an ongoing notice while its condition holds; the log holds exactly one line for each, written at T |
| E3 | An ongoing notice: present exactly while its condition holds (pins at the condition's begin and end instants, ±1 ms) and logs begin and end once each, however many pulses re-emit the condition |
| E4 | Stacking: k notices of one kind make one stack of count k; cycling follows canonical order; a later member increments the count; dismissing one decrements it; a stack dismissal removes only dismissable members |
| E5 | Presentation independence: the world hash, the RNG draws and the order log are identical with the panel absent, present, hovered, clicked and dismissed (INV-8) |
| E6 | WA3: an observer's battle result never stops the observer's clock, never auto-opens, and opens the Battle report on click |
| E7 | A click on the rail or tray never reaches the map (no selection or order results from it). Layout rows *(r1, U-V1: added; r2, U2-1 to U2-3: rewritten, r1's row quoted below)*: (a) **tray**: while a window is open the tray lies inside the context column's rect and the column's section or DETAILS card starts below it; a window opened with the column closed, folded, on a section and on the DETAILS card leaves the column, after Resume, closed, folded, on that section and on that card; (b) **rail**: its rect never overlaps the rect of any existing control of §3's table (the selection card, the Age panel, the formation card, the placement bar, the folded column's tab, the OVERLAYS tab with its NOW USABLE block and controls, the overlay key, the LEGEND tab and the legend), at the opening window, at 1280 × 800 and at the narrow 1080 × 640 window (there also at a UI scale of 1.25), with the context column open, folded and closed, the legend shown and hidden, the overlays folded and unfolded with one and with every layer shown, the Age panel open, a formation card shown, the selection card folded, and the placement bar shown; where §3's rule leaves less than one icon the rail is absent (pin: 1080 × 640, legend shown, placement bar shown) and reappears when room returns; (c) **key**: while the rail is drawn the overlay key stops one margin short of the rail's column in both forms, beside its tab and above it (the latter in both halves of the map); (d) the rows assert only pairs that include a new surface: the collisions among existing controls recorded in §3 are not asserted, and the existing legend and overlay rows still pass in every state in which they pass today. *(r1's row said: "the rail's and the tray's rects never overlap any control of §3's table … with … the placement bar shown; the overlay key stops short of the rail; the existing legend and overlay rows still pass". Its tray rule failed it with the Age panel open (U2-1), its key rule covered one of the key's two forms (U2-2), and two existing controls already collide in one of its states (U2-3).)* |
| E8 | The settlement disposition: the taker's tray item says the choice lapses at this instant; Resume without a choice records Occupy. At a battle joined (N-101), Resume without a choice hands the battle to the TacticalResolver (*r1, U-V12*). *(r2, U2-5: added)* A city taken by the taker's own immediate assault order at T (lane W's rig, as in lane report §11): no tray entry for the capture at T; at the player's next stop the taker's N-301 is the lapsed form (a dispatch that says the city is occupied and the choice has passed), it offers no disposition and its click opens the settlement card; the same capture made by a world event (an assault executed at a siege pulse start) gives the decision form at its instant (control) |
| E9 | *(r1, U-V7: added.)* A dispatch's first appearance: built at `nowMs = KnownAtMs − 1 ms` it is absent, at `KnownAtMs` present, at `KnownAtMs + 1 ms` present; with `EventAtMs < KnownAtMs` it is also absent at `EventAtMs`, and its text gives both dates (F5, F6) |
| E10 | *(r1, U-V7: added.)* The order note (N-603), one pin per delivery semantic (T2 §6; ADR-061 §3.7): a window order at T says "now" and applies at T; a pulse order issued at B − 1 ms, B and B + 1 ms (B a pulse start) states the pulse starting at B, at B ("now", WA1) and at the next pulse start B′; a turn order issued at S − 1 ms, S and S + 1 ms (S a turn start) states S, S ("now") and the next turn start S′. For each pulse and turn order, the instant the note states at issue equals the `EffectiveMs` of the `LatchRecord` written when the order latches; for the window order, the trace shows it delivered at T. *(r2, U2-6: added)* An order whose boundary never comes: a pulse order issued 1 ms after the start of its war's last pulse and a turn order issued 1 ms after the start of the campaign's last turn (controls: the same orders at those starts apply at once). No `LatchRecord` and no refusal is written (as built); the note at issue states no instant (the last-turn note says the order cannot take effect); the pulse order's lapse note is absent at the war's end − 1 ms and present at the war's end and + 1 ms, once the producer publishes the lapse row (§10.1) |

### 13.4 Mutants (for the implementing packet)

Each bounded at 10 × the clean filtered run, each killed by a semantic test, a hang recorded as non-termination
(ADR-015 §7.1): drop the fog filter (L1); sort without `SubjectId` (O1, killed by its reversed pairs); expiry one turn
late (E1); a dismissable decision item (E2); an observer payload with casualties (L5); a UI stop at a hidden window (L3);
a global `LogSeq` (L8); a per-pulse log line for a condition (E3). *(r1, U-V7: added)* a dispatch shown from
`EventAtMs` instead of `KnownAtMs` (E9); an order note that applies a boundary order strictly after the boundary, `>`
for `≥` (E10); the tray sorted by the canonical key instead of the window's (O2, U-V2); an observer widening on by
default (L10, U-V4). *(r2: added)* a "W, then E" decision item dropped from the rail at Resume (E2, U2-7); the rail
standing on the LEGEND tab while the legend is shown (E7 b, U2-1); the key's width bound above its tab left at the map's
right margin (E7 c, U2-2); a lapsed N-301 that opens the disposition panel (E8, U2-5); an order note that names the next
pulse start after the war's last pulse (E10, U2-6).

---

## §14 Fit with the existing Sim.Ui (read, not modified)

| Existing piece (MEASURED by reading) | How the proposal uses it |
|---|---|
| [`NoticeToast`](../../Sim.Ui/Actions/NoticeToast.cs): the End Turn announcement at the top of the map, 6 s of UI time (`:17`), at most 4 lines (`:53`), kicker "THIS TURN" (`:20`); fed from `ActionSurfaceModel.Notices`, which diffs the action space between turns; "UI time only: it never reads or writes simulation state" | Unchanged. It keeps announcing research and new actions at the turn boundary. Its lines also go to the Dispatch log (non-war filter). War notices use the rail, not the toast. |
| [`StateChronicle`](../../Sim.Ui/ViewModel/StateChronicle.cs) and the annals in [`UiSession`](../../Sim.Ui/UiSession.cs) (`AnnalLines`, `PlayerAnnal`, `AdmitsAnnal`, `:343`, `:356`): UI-side history rebuilt by replay, filtered by subject (P7) and by books (D-060) | The Dispatch log is a filter of the same ANNALS section, built the same way (pure function, rebuilt by replay). F4 differs from P7 deliberately (§5). |
| [`PlayerKnowledge`](../../Sim.Ui/ViewModel/PlayerKnowledge.cs) (`KnowsSettlement`, `IsHiddenSettlement` `:118`, `ReadsInternals` `:130`, `KnowsPolity`, `KnowsUnit`) over [`PolityKnowledge`](../../Sim.Core/State/PolityKnowledge.cs) | The builder's only knowledge input (F1). Its questions decide naming (F4) and books (F3). |
| [`HudModel`](../../Sim.Ui/ViewModel/HudModel.cs) and the status band | The war clock and actions chips (§6.2, §6.3) are status, built like the HUD's figures. |
| [`PanelLayout`](../../Sim.Ui/ViewModel/PanelLayout.cs), [`ChromeGeometry`](../../Sim.Ui/ViewModel/ChromeGeometry.cs) | The rail's rect derives from the map rect and from the controls already on it, the tray's from the context column's rect (§3); the End Turn label change is a command-row layout case. *(r1, U-V1: it said they derive from the map rect alone; r2, U2-1: it said "Rail and tray rects derive from the map rect and from the controls already on it".)* |
| The LEGEND tab and the legend (`LegendTab`, `GameUi.cs:1702–1709`; `:913–919`), and the compass rose (`:1070`) *(r1, U-V1: added)* | Unchanged. The rail stands on the tab (and on the legend while it is shown); the tab keeps the map's bottom-right corner. The compass rose stays under the rail as it stays under the tab. |
| The OVERLAYS tab, its NOW USABLE block, controls and key (`OverlayTab`, `GameUi.Overlay.cs:69–96`; `OverlayKeyAt`, `:129–143`) *(r1, U-V1: added)* | The tab is unchanged; the rail's foot stands above it where it reaches the rail's column. Two terms of the key's anchor change while the rail is drawn: its limit beside the tab (`:134`) and its width bound above the tab (`:138`, which also bounds its form in the map's upper half) stop one margin short of the rail's column (§3). *(r2, U2-2: it said "Unchanged, except one term: the key's right limit (`:134`) also stops short of the rail, as it stops short of the legend's reach today".)* |
| The left column: the Age panel (`AgePanelRect`, `GameUi.cs:837–845`) and the formation card (`:1823`) *(r1, U-V1: added)* | Unchanged. Nothing new is placed in the left column or at the map's foot. *(r2, U2-1: in round 1 this was not so: the tray, placed right of the selection card, stood over the Age panel's right 148 px.)* |
| The placement bar (`DrawPlacementBar`, `GameUi.Placement.cs:184–186`, `:208`), the toasts (`GameUi.cs:949`) and the folded column's tab (`DrawContextTab`, `GameUi.cs:1733–1745`) *(r1, U-V1: added)* | Unchanged. The rail stops below the folded column's tab and the placement bar where they reach its column; the toasts paint over the rail and over the tray in the context column for their few seconds, as they paint over the column today, and take no clicks. *(r2, U2-1: it said "The tray uses the placement bar's placement rule and sits below the bar or a toast when either shows; the rail stops below the tab, the tray and the bar where they reach its column".)* |
| The legend and overlay key's own click handling (`OnLegend`, `GameUi.cs:1692–1694`) *(r1, U-V1: added)* | The rail and tray take their own clicks in the same way, so a click on them never reaches the map (E7). |
| The context column (`DrawContextPanel`, `GameUi.cs:1991–2032`; `DrawInfoContext`, `GameUi.Info.cs:242`; `ContextHasContent`, `ContextShown`, `GameUi.cs:339`, `:328`) *(r2, U2-1: added)* | While a window is open the tray is the column's first block and its section or DETAILS card starts below it; the column has content for the window and returns to its prior state at Resume (§3). |
| [`InfoPanel`](../../Sim.Ui/Info/InfoPanel.cs) (the DETAILS card in the context column) | The Battle report is a DETAILS card (below the tray while a window is open). A battle subject needs a new `InfoKind` in `Sim.Core/State/InfoQuery.cs`, a `Sim.Core` change for the implementing packet (D-079 Q5). |
| `KnowledgeLeakTests` (two lenses, positive controls, source guard with allowlist, `RawSettlementIds_AreNotInPlayerText` `:829`, `FrameText_OfEveryPlayerSection_…` `:806`) | The leak tests of §13.1 are a new family in the same harness. |
| The playability gate's rows for the toast | New gate rows: the rail appears, a stack cycles, the tray appears at a window and Resume clears it; and the layout row of E7 (§13.3) across the gate's window sizes and panel states (*r1, U-V1*). |

Nothing references `Sim.Ui` (ADR-009 one-way rule); the proposal adds no reference in either direction.

---

## §15 Dependencies and open points for the Director

**Dependencies (the design waits on these; none is decided here):**
- ADR-061's landing in the campaign kernel (D-079 Q5): no campaign window exists before it.
- The knowledge layer's observation rule and its published log (C7: M7.8, M7.12; C19's observation rule: M7.14; §10.1).
  *(r1, U-V5: it said "M7.8; C7, C19".)*
- H-W DQ8 (whether post-battle orders count against the two-action maximum): it changes the tray's wording (§9 item 2).
  *(r1, U-V12: added.)*
- The significant list (D-079 Q2): it decides which kinds become decision items.
- Lane W's records with a recipient-filtered view and a naval domain field (C33, C35; H-W limitation 15).
- The Standoff ruling (H-TR P-TR1) and AutoResolver acceptance (D-079 Q4): both change what N-102/N-103 may say, not how
  the notice works.
- *(r2, U2-6: added)* A lapse row for an order whose boundary never comes (§10.1): T2 writes none, so until the producer
  does, a pulse order's note cannot be replaced by its lapse note.
- *(r2, U2-5: added)* The ruling on a city taken by the player's own order at T (U-Q11): it decides whether N-301's
  lapsed form stays the taker's only notice on that path.

**Questions for the Director (each with this proposal's recommendation):**

| # | Question | Recommendation |
|---|---|---|
| U-Q1 | Should the per-polity event log (§10.1) be simulation state, so the AI reads the same knowledge (Spine Law 7)? | Yes; an ADR in the packet that builds the published log (the knowledge layer: C7 in M7.8/M7.12, C19's observation rule in M7.14; *r1, U-V5*: it said "the knowledge packet (M7.8)"). The UI-diff alternative cannot serve observers. |
| U-Q2 | Observer payload: outcome class, place and known combatants only (proposed), or also the losses the observer saw (needs the strength channel), or the authoritative casualties (leaks; not recommended)? | Outcome only for M7; observed strength when the knowledge layer publishes it (C7: M7.8, M7.12; *r1, U-V5*). |
| U-Q3 | An observed combatant with no contact: name it "unidentified force" (proposed) or drop the notice (the annals' rule)? | Unidentified force. |
| U-Q4 | Should observers also be told when a battle **begins** in their sight, or only of results (WA3's words)? | Results only. |
| U-Q5 | Who learns of a leader's execution (N-303, N-310) and of a war's end (N-309) outside the war: participants only (the strict reading of WA3), observers of the final capture (proposed; a widening of WA3, off unless approved), or every polity in contact with the eliminated one? | Observers of the final capture; a rule for C19's observation rule (M7.14). *(r1, U-V4, U-V5, U-V6: the widening is now stated, the packet corrected from "M7.8", and N-310 added.)* |
| U-Q6 | May the player choose kinds for which the UI resumes at once at a window (presentation only; §9 item 9)? | Yes, default off, never for a choice that lapses at the instant. |
| U-Q7 | Dispatch expiry: survives one End Turn (proposed), a number of simulation years, or never (manual dismissal only)? | Survives one End Turn. |
| U-Q8 | Reading: a text notice states an execution whatever the execution content setting (D-079 Q12), which governs depiction only. Confirm. | Confirm. |
| U-Q9 | WA3-R (restated from D-080): observers are polities observing the war under fog; war economics are never granted to observers. Confirm both readings. | Confirm. |
| U-Q10 | *(r1, U-V4: added.)* Should an observer be told when a war first comes into its sight (N-601) and log when it leaves (N-602), or only of battle results (WA3's words)? | Results only, as for U-Q4. N-601 and N-602 stay in the catalogue, off, for the Director to switch on. |
| U-Q11 | *(r2, U2-5: added.)* A city taken by the player's own immediate assault order (issued at a siege pulse start, WA1) leaves the player no instant at which to choose Occupy, Sack or Raze (DR37's post-battle choice): the capture raises no window hit at T, and the choice has lapsed by the next stop, while an AI taker on the same path disposes by doctrine at T (MEASURED, lane report §11; §9 item 10). Options: (a) accept it: Occupy applies and N-301's lapsed form says so; (b) let the assault order carry a disposition that applies if the order takes the city, as an AI's doctrine does (the prototype world already accepts a Sack issued in the same window after the assault order: MEASURED); (c) raise a window hit after an order's consequences at T (an ADR-061 change; H-W limitation 11); (d) give an AI taker on that path Occupy as well. | (b), which treats the player as the AI's doctrine is treated and changes no world rule; until then (a), the lapsed form. The ruling is the war-outcome rules' (DR37, ADR-061), not this presentation's. |

---

## Annex R — Reference research (pattern study; NOT design; D-069 R1)

This annex studies how other strategy games tell the player about events. It is reference only. Nothing here is
specified for this game. No name, text or art from these games may enter shipped content, and nothing of theirs is
transferred node for node, name for name or text for text (D-069 R1, R2, R4; [ip-originality.md](../ip-originality.md)
§3). Those rules do not bar a genre convention such as the corner that holds an alert rail; §3 sets this design's
anchors from this game's own chrome. *(r1, U-V11: it said no "layout position" may enter shipped content, which D-069
does not say and which §3, placing the rail at the lower right, contradicted.)*

Labels: **[CODE]** read in the scripts named in Annex P; **[SEARCH]** a web-search summary (the pages themselves could
not be opened from this session, Annex P); **[KNOW]** unverified general knowledge.

### R.1 The Director's reference (Civilization VI's notification system)

| # | Topic | Finding | Label |
|---|---|---|---|
| R-1 | Position | The Director describes "events shown as hoverable icons at the lower right" (D-080 WA3-D). The panel script lays the notifications out as a vertical scrolling rail sized against the screen height minus a top-bar offset (50 px) and an "action corner" offset (300 px, commented as keeping the rail visible further down the screen); when the stack overflows it shifts aside for a scroll bar (`notificationpanel.lua` 22–31, 254–282). The rail sits above the end-turn control in the lower right. The layout file was not reachable, so the exact anchor is not read. | [KNOW] for the corner; [CODE] for the offsets and scrolling |
| R-2 | Whose notices | Only notifications for the local player are processed (add and dismiss handlers check the player id, 1288–1289, 1317–1318). | [CODE] |
| R-3 | First appearance | A new notice slides out with its text, pauses 2 s, then retracts to its icon (`TIME_PAUSE_FIRST_SHOW_NOTIFICATION` = 2; 727–736). | [CODE] |
| R-4 | Hover | Mouse enter slides the expanded area out (title in capitals and a summary); mouse exit retracts it after 1 s (`TIME_PAUSE_MOUSE_OVER_NOTIFICATION` = 1; 746–786, 836–853, 923–928). | [CODE] |
| R-5 | Click | Left click activates: by default it moves the camera to the notice's location and selects its unit or city target (576–618, 637–644); per-type handlers open the relevant screen instead (research, civics, production, government, raze decision, religion, great people, envoys, artifacts, espionage, diplomacy, continent lens; 169–188, 1029–1198). | [CODE] |
| R-6 | Stacking | One entry per notification type per player; a second notice of the same type joins the type's list instead of adding an icon (373–429). With two or more, the entry shows a count, page pips and left/right arrows; the arrows cycle with wrap-around and move the camera to each member (886–913, 969–1001). "Auto" notices carry an engine-maintained count, such as units needing orders (721–722, 881–884). Notification groups are tracked but no group UI is drawn in this script (396–412; the group instance manager is created at 37 and only cleared at 1338). | [CODE] |
| R-7 | Dismissal | Right click asks to dismiss; the engine decides whether the user may (`CanUserDismiss`; 700, 942–951). A stack's dismiss button dismisses every user-dismissable notice of that type (894, 549–571). Removal is driven by the engine's dismissed event (1317–1329); the panel also drops a notice whose target unit was killed (1507–1533). When the engine removes an informational notice on its own (at a turn's end, or when its condition resolves) is decided in the game core, not in these scripts, and was not verified. | [CODE]; the engine's own expiry rule not verified |
| R-8 | Phases | A notice that is not valid in the current turn phase (simultaneous multiplayer phases) can be dismissed but not activated, with a tooltip saying so (714–719, 956–964). | [CODE] |
| R-9 | Blocking versus informational | Notices that block the end of the turn get **no icon on the rail**; the action panel shows them (682–685). The action panel's large end-turn button shows the first blocker's message, icon and tooltip, and a click opens that blocker's decision instead of ending the turn (`actionpanel.lua` 186–220, 524–607). At most 3 small blocker buttons sit around it (controls 2 to 4): `MAX_BLOCKER_BUTTONS` = 4 counts the big button too, although its comment calls it the number of buttons around the big one (25; the count starts at 1 "to account for current main blocker", 139; the small buttons run from 2, 225, to 4, 261). The script has a "+" overflow list for blockers beyond the fourth visible one (274–303); a count shows several blockers of the active type (317–330). The blocker types listed are units needing orders, stacked units, research, civic, policy slots, government change, raze decision, production, religion items, envoys, great people, a spy's escape route, a spy's dragnet priority and artifacts (88–104). An optional automatic end of turn (a user setting) fires when nothing blocks, no unit has moves left and no city can make a ranged attack (506–519); the mod's copy adds a further condition of its own at 512, named after the mod. *(r1, U-V9: it said "up to 4 blocker buttons sit around it", omitted two blocker types, and gave the automatic end of turn's conditions incompletely, inside a range that includes the mod's edit.)* | [CODE] |
| R-10 | Sounds | Per-type sounds on arrival, grouped by valence (negative, neutral, positive; 190–230); a sound when the rail first fills and another when it empties (286–315); none until loading completes (1296). A blocker's sound plays once until that blocker leaves the list (`actionpanel.lua` 166–180, 305–312). | [CODE] |
| R-11 | Transient status messages | A separate panel shows non-interactive messages at the upper centre for 10 s by default; a click removes one; news about other players uses a different colour (`statusmessagepanel.lua` 1–11, 282–355; layout anchor centre-top, 120 px down, and the second message colour, in `statusmessagepanel.xml`). The mod's copy adds per-message-type ignore settings for that news (an ignored-messages list built in functions named for the mod, so the filter is the mod's addition rather than the game's: INFERRED from the names). | [CODE] |
| R-12 | Log, filtering, criticisms | Workshop mods add a persistent "Notification Log" that keeps entries across turns (last 50, turn-tagged), which suggests the base game keeps no such history. Player threads complain of notices that must be dismissed every turn, notices stuck until a reload, banners covering the screen, and a right-click dismissal that also moved a unit. Some results in the same searches concern the successor title, so these are indicative only. | [SEARCH] |

### R.2 Two other strategy-game patterns

| # | Game family | Finding | Label |
|---|---|---|---|
| R-13 | Paradox grand strategy (Europa Universalis IV, Crusader Kings II, Stellaris) | Message types can be set per type to show as a pop-up and to pause the game (a Crusader Kings II forum thread; in Europa Universalis IV a console command sets every message to pop up and pause). Stellaris players report the per-type settings ignored apart from auto-pause, keep pop-ups off and click the short "toast" entries instead; Stellaris keeps a situation log. Forum titles complain of message settings reset by patches and ask for player-made custom alerts (titles only). | [SEARCH] |
| R-14 | Total War: Warhammer III | A small button beside End Turn opens tick boxes choosing which end-turn notices to show, used to silence a repeating reminder. | [SEARCH] |
| R-15 | Both families | Persistent conditions ("you can do X") appear as a row of alert icons separate from one-off event messages. | [KNOW] |

### R.3 What the study suggests, restated as genre patterns (and where this design departs)

| Pattern seen | This design |
|---|---|
| Decisions apart from news, beside the end-turn control (R-9) | Decisions apart from news is adopted as a genre pattern, in our own form: the tray is the view of a **mid-turn decision window** that holds the clock, a mechanism this game has and the reference does not; it never forces a choice (C3). The placement differs: the tray is the first block of the context column, where a decision's panel opens, with its own Resume, not a set of buttons around the end-turn control (§3; *r2, U2-1*: it said "the tray is a mode bar at the top of the map with its own Resume"; *r1, U-V1*). |
| Hover to read, click to go to the subject, stack by type with a count (R-3 to R-6) | Adopted in our own form (§7, §8), with the stack's members in a deterministic order (§10.3). |
| Engine-driven expiry; stuck notices; repeated manual dismissal (R-7, R-12) | Replaced: expiry is a pure function of simulation turns and conditions, tested at the instant (§8, §13.3). |
| No history (R-12) | Every notice has a log line; dismissal never loses information (§8). |
| Filters added by mods and by other games (R-11, R-13, R-14) | Offered only for informational kinds; decision items cannot be filtered, only auto-continued if the Director allows it (§9 item 9). |
| News about others gated by diplomatic access in the reference (R-11; [KNOW] for the gating) | Gated by fog instead (observation under `PolityKnowledge`); diplomacy is M8's. |
| Sounds per valence (R-10) | Deferred: no audio exists; never the only signal (§11). |
| Pop-ups that pause (R-13) | Rejected for informational notices: only the simulation's own decision windows stop the clock (C2). |

---

## Annex P — Provenance of the research (lane U, 2026-11-09)

| Source | How obtained | Identity |
|---|---|---|
| `notificationpanel.lua` from the CQUI mod repository (a copy of the game's notification panel script with a few lines marked "CQUI": 1307–1309, 1535–1539, 1570–1571) | `curl` through the session proxy from `https://raw.githubusercontent.com/CQUI-Org/cqui/master/Assets/UI/Panels/notificationpanel.lua`, 2026-11-09 | 1,573 lines; SHA-256 `25701ebd02c0f7c5e7ea83de2c17de16c929a0643c837146b7625d16e3cbc90b` |
| `actionpanel.lua` (same repository; mod-edited copy, 12 lines mention CQUI) | `.../master/Assets/UI/actionpanel.lua`, 2026-11-09 | 1,189 lines; SHA-256 `34eaee01cc6bb2299376afb2232260c09dcd3dc12794845b8b58e1a2e945553a` |
| `statusmessagepanel.lua` and `.xml` (same repository) | `.../master/Assets/UI/Panels/statusmessagepanel.lua` and `.xml`, 2026-11-09 | 421 lines, SHA-256 `91f7efb807faadd1a4ff11cd1c653465909e375a31e5b955a7257a81a317765a`; 33 lines, SHA-256 `feeb257f5f891a6c370dab63a39456c1b0558eb33a10f62a9e8f959011a67440` |
| `cqui.modinfo` (used only to find the files) | `.../master/cqui.modinfo`, 2026-11-09 | SHA-256 `488fa1eb6b004a114caa2673d16f1ed35856354d20b3613acc696daafc3998a4` |
| Web searches (R-12 to R-14) | The session's web-search tool; summaries only | Opening the result pages failed: `steamcommunity.com` and `forums.civfanatics.com` do not resolve from this session (they would have to be allowed in the session's network settings). The GitHub API answered 403 for the repository (access not enabled for this session); raw file reads worked. |

The scripts were read as text and are not reproduced here beyond identifiers, constants and line numbers. The files are
the mod's copies at the repository's `master` branch on the date fetched, not a verified release of the game; the
notification panel's layout file is not in the mod and was not read. Downloaded copies are in the lane's scratch area
only, not in the repository.
