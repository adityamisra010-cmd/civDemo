# D-060 — Rival internals isolation: a known rival's books are closed on every player surface

**Status: IMPLEMENTED 2026-10-19 on branch `b6-rival` (LOCAL; base `77d8c30`; not pushed, not merged).** UI-only in
effect: no Sim.Core system, order, save, schema or world hash changed (§8). Every figure in §8 was measured by the agent
writing this record on this branch.

## 1. The instruction (verbatim)

The Director, 2026-10-19: *"i should not be able to see other empire's data."*

The authorization for this work, verbatim: *"Enforce the existing polity-internals access rule across every
player-facing surface identified in the audit. Complete the L7 audit for unit cards and foreign trade information. Do not
assume that the resource-overlay gate protects other screens. Keep public settlement identity separate from private
settlement internals. Preserve intended public information while hiding protected rival figures. Add regression tests
for each affected surface. Tests must vary rival data while holding public identity constant, confirm that rival-facing
output does not reveal those changes, and include positive controls for the player's own data. Do not claim a leak is
fixed without test evidence."*

## 2. Root cause (verified against the tree at `77d8c30`)

- Every player surface gated a settlement on `PlayerKnowledge.IsHiddenSettlement` — "the player does not KNOW it".
- Production knowledge is all-known (`PolityKnowledge.Of`; S2a's known extent is unbuilt), so nothing is ever hidden. A
  KNOWN rival therefore passed every gate, and its people, stores, happiness and so on were shown.
- The rule that already exists for a settlement's books is `PolityKnowledge.ReadsInternals(SettlementId)`
  (`Sim.Core/State/PolityKnowledge.cs:195`): a settlement's books are its controller's; every settlement is readable in
  a world with no control rows. The AI construction policy uses it. In Sim.Ui only the resource overlay's ore-roll
  markers used it (`Sim.Ui/World/ResourceOverlay.cs:541`). That gate protects only the overlay.

## 3. The rule as built

- **The wrapper.** `PlayerKnowledge.ReadsInternals(int id)` (`Sim.Ui/ViewModel/PlayerKnowledge.cs`) caches the core
  seam's `ReadsInternals` per settlement. It is false for a settlement the player does not know. There is no second
  source of truth.
- **Every private figure asks it.** A player surface that shows a settlement's private figures now asks
  `ReadsInternals`, not only `IsHiddenSettlement`. A known rival stays known and selectable; its books stay closed.
- **What the player sees instead.** Where a surface would show a rival's figures, it shows the rival's public identity
  and the line `PlayerKnowledge.NotYoursLine`: *"Not your settlement - its figures are not known to you."* The line has
  no figure and no "%", so it holds in every era, including the numeral-free Ages I–III.
- **Free settlements.** A settlement under no empire (after a revolt) has no controller, so its books are closed too.
  This is the existing rule (`ReadsInternals` is false for it once any control row exists); it is not a new decision.

## 4. Public and private

| Public (shown for a rival) | Private (the rival's books — never shown) |
|---|---|
| name | population and its breakdown (age bands, classes, births, deaths, movement) |
| location / site | grain, goods and every stock; the last harvest |
| owner / controller ("Held by the empire of N", "Free people, under no empire") | happiness and its factors; grievance; unrest |
| that it exists; that it was founded (no figure) | needs and their satisfaction |
| the map token and territory as drawn today: footprint, dwelling blocks, structures and institution markers, the Age banner | prices, price terms, the market |
| route endpoints; roads built | construction queue and progress; structures as a list; institutions' staff and maturity |
| formations the player knows: identity, family, Age form, owner, station, position | labour and sector shares; the labour record (POLICY) |
| a change of ruler; an Age entered; a road built; a structure completed; an institution founded | a founding party and its provisions; deposits |
| | a rival formation's recorded experience and army membership |
| | a rival's tax levy |

Where the split was ambiguous, the stricter reading was taken:
- **Map label text.** The Settlement-zoom label "N people - N dwellings", the size-tier line and the labour caption
  print the rival's figures, so they are private. The drawn token is public as drawn today, so its footprint and blocks
  stay (§9).
- **The labour ring and notables.** The lens's labour-sector ring and its "N notable(s) resident" line are private.
- **A formation's record.** A rival formation's experience and army number are private.
- **A rival's extinction.** It is not told in the ANNALS view: the event's only content is the last people, a figure.
- **A rival's tax change.** It is not told in the ANNALS view: a levy is the rival's own policy.

## 5. Scope decision — the F12 DEV panels stay world-wide

Decided by the orchestrator under the Director's authorization; recorded here, not changed in this packet.

- **What.** The DEV panels (F12, or `--dev`; off by default) keep every KNOWN empire's figures. These are DEV › TURN,
  RECORDS and ECONOMY; RECORDS includes its HUD lines, market and needs.
- **Why.** They are a developer instrument. The audits and the TURN ledger check need every empire's figures.
- **How.** GameUi reads the DEV panels through `PlayerKnowledge.ForDeveloper()`. That view opens the books of every
  settlement the player knows; a hidden settlement stays hidden (P7 is unchanged).
- **Guarded.** A source guard (`D060_TheDeveloperView_StillReadsEveryKnownEmpiresBooks_AndOnlyGameUiAsksForIt`) pins
  that only `Sim.Ui/GameUi.cs` calls `ForDeveloper()`.
- **Labelled.** The DEV panel's first line now reads *"Developer view - shows every empire."*
  (`GameUi.DeveloperViewLine`).
- **Compatibility.** No player surface changes because of this decision. A player who turns DEV on still sees rival
  figures there; that is the decision.

## 6. Scope decision — the session files stay world-wide

Also decided by the orchestrator; recorded here.

- **Which files.** The orders, trace, telemetry, chronicle and forensic files of a session are unchanged.
- **Why.**
  - They are the replay and forensic record. `Resume` and `sim inspect` rebuild and check the session from them.
  - Filtering them would change the session record contract.
  - It would break the D-057 evidence, which compares recorded files byte for byte.
- **The annals.** `UiSession.ExportChronicle` still writes `RecordAnnalLines`, world-wide as before. It is subject only
  to P7's hidden-settlement filter, which never applies in production.
- **What is filtered.** The in-game ANNALS panel reads `UiSession.AnnalLines`. That is now a separate, filtered list
  (§7, L5).
- **Compatibility.**
  - A chronicle-*.txt written by this build is identical to one written by `77d8c30` for the same session.
  - Where the player rules every settlement, the panel and the file are identical, as before.
  - Example: the zero-AI default world, where the player founds every settlement.
  - **The panel and the file now differ only where a rival or free settlement exists.** The old doc line "EXACTLY the
    panel's lines" is restated on `ExportChronicle`.

## 7. Every surface, with its verdict

Verdicts: **gated** (now asks `ReadsInternals`), **public** (shows only public identity; kept), **already safe** (reads
only the player's own data, or is a developer surface by §5/§6). Line numbers are at `77d8c30` unless marked.

| # | Surface | File:line | Verdict and evidence |
|---|---|---|---|
| L1 | Selection card (HUD) | `Sim.Ui/ViewModel/HudModel.cs:62`; `Sim.Ui/GameUi.cs` `SelectionRows` | **Gated.** A rival's card shows its name, `OwnerLine` and `NotYoursLine`, unrouted. No bucket, stock, allocation, need, grievance or happiness row is read. The status band was already own-only (resources-view follow-up). |
| L2 | SETTLEMENT view | `Sim.Ui/ViewModel/PlayerViews.cs:170` | **Gated.** A rival's view is title (name), subtitle (owner), `NotYoursLine` and the formations the player knows to stand there. It has no People, Food, Housing, Happiness, Migration, Work, Structures or Institutions block. In the player's own view: a more attractive rival is named without its attractiveness figure; a rival formation stationed in the player's settlement is named without its experience. |
| L2b | SettlementInspectorModel / GrievanceViewModel (`ScreenModels.Settlement`) | `Sim.Ui/ViewModel/ScreenModels.cs:68` | **Already safe by §5.** Drawn only in DEV › RECORDS. |
| L2c | POLICY labour record (`ScreenModels.Policy`) | `Sim.Ui/ViewModel/ScreenModels.cs:138`; `GameUi.DrawPolicySection` | **Gated.** POLICY is a player section. A rival's record reads as no selection, and the panel shows `NotYoursLine`. |
| L3 | Needs panel | `Sim.Ui/ViewModel/NeedsPanelModel.cs:39` | **Gated** (`MarketModel.BooksOpen`): a rival reads as nobody present. It is drawn only in DEV › RECORDS, which passes the developer view (§5). |
| L4 | Market panel (rows, price breakdown) | `Sim.Ui/ViewModel/MarketModel.cs:77, :116` | **Gated**, the same way as L3. The DEV price plot (`HistoryBuffer.Price`) stays world-wide by §5. |
| L5 | ANNALS panel | `Sim.Ui/UiSession.cs` `ObserveChronicle`/`Step`; `Sim.Ui/ViewModel/StateChronicle.cs:49` | **Gated**; see the annals rule below. |
| L6 | Founding info card | `Sim.Core/State/InfoQuery.cs:1553–1600` (`FoundingCard`) | **Gated** (the one Sim.Core change: a read-only query, no system/order/hash). A rival source shows "A party and its provisions: not known to you"; party and provisions are computed only where `c.K.ReadsInternals(id)`. The UI builds `OfFounding` only for listed (own) sources, but a pinned card survives a capture, so the gate is needed. A UI-side fix would have had to parse the card's text. |
| L6b | Other InfoQuery cards naming a settlement | `InfoQuery.cs` `SectorCard` (:665), `ProjectCard` (:810), `UniversityCard` (:1824), `CellCard` (:1377), `FormationCard` (:1299) | **Already safe or now unreachable.** Sector: the player's own labour activities only ("You do not direct the labour here"). Project at a rival: reachable only from the rival's Structures lines, which L2 no longer builds; the action surface targets own (listed) settlements. Cell (place) card: the source is always the player's (`GameUi.Info.cs:208`). Formation card: family, identity and station only; public. |
| L7a | Unit card | `Sim.Ui/ViewModel/UnitCardModel.cs:122–127`; `GameUi.DrawUnitCard` | **Gated** for the private fields only. The rival's identity, family, Age form, owner, station and position are public (drawn on the map), and the next-Age plan follows from the public Age form and Age banner. Experience and army become `RivalRecordText`. The "not simulated" lines carry no figure. |
| L7b | EMPIRE view trade lines | `Sim.Ui/ViewModel/PlayerViews.cs:664–685` | **Already safe.** Only flows with one end in the player's own settlements are listed (`mine.Contains`): the player's own exports and imports, the quantity the player sent or received, and the partner's name. A flow between two foreign places is never listed. No foreign price or stock is read. |
| L7c | `TradeModel` (world price spreads, every flow) | `Sim.Ui/ViewModel/TradeModel.cs`; drawn by `GameUi.DrawEconomySection` | **Already safe by §5.** Drawn only in DEV › ECONOMY. |
| L7d | Map labels and tokens (Settlement zoom) | `Sim.Ui/World/WorldLens.cs:239–290` (Build), `:1478–1505` (`PaintSettlementLines`), `:1315` (sector ring), `:1305` (notables) | **Gated.** `SettlementLensView.ReadsInternals`. For a rival the projection carries no sectors, top-sector label, notables, active classes or size tier, and the lens prints no people/dwellings line, size-tier line or labour caption. **Public (kept):** the footprint and dwelling blocks (sized by population and dwellings), structures and institution markers, Age banner, name and territory. See §9. |
| L7e | Map hover hint, info registry | `Sim.Ui/GameUi.Info.cs:325`, `Sim.Ui/Info/InfoPanel.cs` | **Public.** The hint is the fixed text "Shift+click: details"; the regions name subjects only. |
| L7f | Legend | `WorldLens` legend | **Public.** Layer names and honest empty states only. |
| L7g | Tab cycling | `Sim.Ui/ViewModel/SettlementSelection.cs:99` | **Public.** It selects known settlements; selection itself is public. Every surface keyed on the selection is gated above. |
| L7h | CameraFocus | `Sim.Ui/ViewModel/CameraFocus.cs` | **Public.** The site of a settlement the caller already holds. |
| L7i | RESOURCES trend place scope | `Sim.Ui/GameUi.Resources.cs:298–334` | **Already safe.** The place scope requires `IsOwnSettlement(_selected)`; a rival selection shows "Select one of your settlements on the map to chart it." Each series is masked to the turns the place was the player's (`OwnershipHistory`). Residual in §9. |
| L7j | ActionSurfaceModel selected path | `Sim.Ui/Actions/ActionSurfaceModel.cs:353, :378, :420` | **Already safe.** Blocks target listed (own) settlements. A rival selection yields only "NAME is not ours to command" (public identity). |
| L7k | PolicyHistoryModel | `Sim.Ui/ViewModel/PolicyHistoryModel.cs` | **Gated** through L2c (its only player caller). |
| L7l | ExplainRouting | `Sim.Ui/ViewModel/ExplainRouting.cs` | **Public** (a pure figure→section table). A rival's card no longer offers figure routes (L1). |
| L7m | PanelLayout | `Sim.Ui/ViewModel/PanelLayout.cs` | **Public** (geometry). |
| L7n | Step notices, Age panel, INSTITUTIONS view, stores strip and card | `ActionSurfaceModel.Notices`, `AgePanelModel`, `PlayerViews.Institutions`, `EmpireStores`/`StoresCard` | **Already safe.** The player's own research, Age, institutions and stores only. |

**The annals rule (L5).** These are the ANNALS panel's lines (`AnnalLines`); the session record (`RecordAnnalLines`)
is unchanged (§6).
- **Core chronicle events.** Every core event carries a figure of its settlement's books.
  - At a settlement whose books the player reads: told as before.
  - A **founding** anywhere: public, told without its figure — *"In the year Y, NAME was founded."*
    (`UiSession.PlayerAnnal`).
  - Every other event at a closed settlement is not told: famine onset and end, extinction, first artisans, migration
    surge, disaster, food shortfall onset and end.
- **State chronicle events** (`StateChronicle.Observe(..., playerBooks: true)`).
  - **Not told:** a rival's tax levy; an institution maturing at a closed settlement; a merchant class emerging there; a
    segment rising or subsiding there.
  - **Told (public, each drawn on the map):** an Age entered (the banner), a road built or modernized, a structure
    completed, an institution founded, a change of ruler, the re-equipment of known formations.
- **Why keep the public events.** They are history the map already shows. Dropping them would hide the rival's
  existence, which the Director's split keeps public.

## 8. Tests and evidence

New file: `Sim.Ui.Tests/KnowledgeLeakTests.Rivals.cs`. The rig is P7's played one-AI world (seed 42, six turns),
read through the PRODUCTION view (all-known), not a stub.
- **The rival's books are perturbed:** people, grain and goods with their last harvest, dwellings, labour, deficit,
  grievance, needs, prices and price terms, class states, a queued and part-built project, and its formations'
  experience and army.
- **Its public identity is held:** name, site and owner.

| Test | Surfaces | Proves |
|---|---|---|
| `D060_AKnownRivalsBooks_DoNotReachTheSurface_UnderTheProductionView_WhileThePlayersOwnDo` (Theory, 7 cases) | L1, L2, L3, L4, L6, L7 unit card, L7 map projection | Rival output byte-identical under the perturbation. The rival's name, owner and closed-books line are still present. POSITIVE CONTROL: the same perturbation of the player's own selected settlement changes the same output. |
| `D060_TheRealUi_WithAKnownRivalSelected_DrawsItsNameAndOwner_AndNothingOfItsBooks` | L1, L2, L2c, L5, L7d via FrameText of the real UI | The rival is Tab-selected. FrameText is captured for the map, every player section, POLICY's labour record and the lens at Settlement zoom over the rival. The text is identical between the two worlds, and its name, owner and closed-books line are shown. POSITIVE CONTROL: the player's own settlement selected and perturbed changes the text, and the people line is drawn for it. |
| `D060_TheAnnalsView_TellsARivalsFoundingWithoutItsFigure_AndNothingOfItsBooks_WhileTheSessionRecordKeepsThem` | L5 core events | The ANNALS panel is identical, and the rival's founding is told figure-free. The session record differs, so it stays world-wide. The own founding carries its figure and moves. Each figured core event type is dropped for the rival and kept for the player's own settlement. |
| `D060_TheStateChronicle_InThePlayersView_LeavesOutARivalsBookEvents_AndTellsThePlayersOwn` | L5 state events | A rival tax change, merchant class and maturing institution are planted: not told in the player's view, told in the record. POSITIVE CONTROL: the same events at the player's own settlement are told. |
| `D060_TheDeveloperView_StillReadsEveryKnownEmpiresBooks_AndOnlyGameUiAsksForIt` | §5 | The developer view sees the perturbation and never opens a hidden settlement. Only `GameUi.cs` calls `ForDeveloper()`. |

**Changed existing tests.** Each of these encoded the leak: a known rival's books, seen all-known, were the expected
output or the positive control. Each is restated in its own file, with a D-060 comment.
- **`KnowledgeLeakTests.PerturbingWhatTheStubHides_…`.** The control reads through the developer view (books open).
- **`TheMigrationView_…`.**
  - All-known, the rival is named without its figure, and the line is identical across the two worlds.
  - The developer view shows the figure, and it moves.
- **`EverySelectionKeyedReader_…`.**
  - The rival's labour record reads as no selection, all-known too.
  - The control is the player's own record.
- **`FrameText_OfEveryPlayerSection_…`.**
  - The old control ("the all-known UI over the two worlds differs") measured the rival's founding figures in the annals.
  - It is now: the all-known UI draws the hidden polity's names, which the stub hides.
  - `D060_TheRealUi_…` carries the own-books control.
- **`Integration_EveryMapFeatureAtOnce_…`.**
  - "unit card" is the one component the rival perturbation no longer reaches (experience is closed).
  - Its stub comparison stays non-vacuous: the all-known card differs from the stub's.
- **`AForeignQueueHead_…` and `WithIncrementalConstruction_AForeignQueueHead_…`.**
  - The foreign view is closed books: no Structures block, invariant under A's and B's stocks.
  - The player's own purse controls are unchanged.
- **`AForeignHeldHead_…` (Holds).**
  - The viewer's knowledge is now the viewer's own (it was always the player's).
  - The rival view shows `NotYoursLine` and no "Being built".
- **`UnitTokenDetailTests`.**
  - The owner-change case now must move Experience and Army.
  - The no-number check expects `RivalRecordText` for rival cards.
  - The hidden-formation control keeps the identity channel; experience and army are closed.

### 8.1 Measured results (2026-10-19, this branch, .NET 10.0.112)

**Gates**
- `scripts/check-banned-constructs.sh`: OK.
- `check-read-isolation.sh`: OK (24 allowlisted files, unchanged).
- `check-readonly-proof.sh`: OK.
- `dotnet build -c Release`: 0 warnings, 0 errors.

**Suites**
- Sim.Ui.Tests: 1,080 passed, 0 failed, at `e19d3ad`. That is 1,069 before, plus 11 new D-060 cases.
- Sim.Tests: 2,635 passed, 0 failed, 13 skipped (the same counts as CI on `6d1988f`).

**World hash unchanged**
- Command: `Sim.Cli run --founded --seed 42 --ai-empires 1 --turns 60`.
- Before (base `77d8c30`, built first) and after: both
  `827d9b00737117e25c3087495a87f18616a1e0f560f60baff5821a855a3878a0`.

**Smoke**
- `--smoke --monkey 300`: SMOKE PASSED. 13 states, 3,862 checks, 0 FAIL — the same check count as CI on `6d1988f`.
- `--smoke --ai-empires 1 --monkey 300`: SMOKE PASSED. 11 states, 3,261 checks, 0 FAIL.
- The gate rows did not change, so `docs/m5-playability-gate.md` is not regenerated.

**Mutation check**
- **The mutant.** `HudModel.From` re-gated on the old rule (`books = true`: IsHiddenSettlement only).
- **Killed by the semantic test:** `D060_AKnownRivalsBooks_…(surface: "L1 selection card")`. The rival's card printed
  "pop 240 (child 97 / adult 137 / elder 6) … grievance 31.56".
- **Also killed by the real-UI test:** `D060_TheRealUi_…`, line 39, "pop 240 …" vs "pop 816 …".
- **The bound.** The run took 11 s, against a 17 s clean baseline of the same 11 cases. The bound was 3× that baseline,
  and the run was well inside it.
- **Restored.** The source was restored afterwards.

## 9. Residual risks and what is not fixed

- **The drawn token is public by the split.** It still reflects the rival's population and dwellings approximately:
  the footprint radius steps with the size class, and one block is drawn per dwellings-per-block. The exact figures
  are not printed. Making the token itself size-blind would change "the map as drawn today", which the Director's split
  keeps public. That needs a ruling.
- **An own settlement's price history before capture.** RESOURCES › price, place scope, plots the HistoryBuffer's
  price series of the player's own selected settlement. That series is not masked to the turns the place was the
  player's (the other series are, through `OwnershipHistory`). A settlement captured from a rival therefore shows its
  pre-capture prices. It is a small, bounded leak (one good's price, of a place the player now rules), and it is not
  fixed here.
- **DEV and session files are world-wide by decision (§5, §6).** A player who turns on F12 can still read rival figures.
- **Fog (S2a) is unbuilt.** "Known" is still all-known in production. When S2a lands, both gates apply: unknown
  settlements hidden (P7), known foreign ones closed (D-060).
- **The read-isolation allowlist is unchanged** (§10). The advisory script and the source guard cannot tell a gated
  read from an ungated one. The metamorphic tests above are the gate of record for D-060.

## 10. The read-isolation script and allowlist

`scripts/check-read-isolation.sh` and `scripts/ui-raw-read-allowlist.txt` are unchanged.
- No new file reads `Settlements`, `MilitaryUnits`, `Buckets` or `Deposits` raw.
- Every changed reader is already listed as `routed` (it references `PlayerKnowledge`).
- `ScreenModels.cs` stays `developer`; its Policy view now also asks `ReadsInternals`.

## 11. Dated note — 2026-10-20: status, and the Director's rulings on §9's two residuals (§1–§10 unchanged)

- **Status.** Pushed in `claude/new-session-yavghm` at `f80dce6`, superseding the "LOCAL … not pushed" status line
  above, which is kept as written. CI run 39338603701 is green, and the Windows build `sim-ui-win-x64-f80dce6` passed
  `windows-smoke` (run 39338603779). Not on `main`.
- **Director-reported playtest (2026-10-20):** rival-data isolation **Pass**. The build is inferred to be `f80dce6`; see
  `docs/m5-playtest-known-issues.md`, note of 2026-10-20.
- **Decision A — the drawn token's size (§9, first bullet): DEFERRED until fog of war.** Keep the current footprint and
  dwelling-block sizing of a rival's token. This is **not** a ruling that the sizing is permanently accepted public
  information. Revisit it when fog-of-war visibility and information-disclosure rules are designed (S2a). Nothing is
  changed in code.
- **Decision B — pre-capture price history (§9, second bullet): INTENTIONAL DISCLOSURE, not a leak.** Pre-capture price
  history stays visible to the settlement's current owner. Historical price graphs are not masked by ownership history.
  §9's wording "a small, bounded leak … not fixed here" is superseded by this ruling. Nothing is changed in code.


## 12. Dated note — 2026-10-20: ACCEPTED by the Director (closure ruling R5)

> Accepted as part of the M5 set, including the §5/§6 scope decisions (F12 DEV panels and session files stay world-wide)
> and the §11 Decisions A and B. Record: `docs/m5-exit-inventory.md` §13.

## 13. Dated note — 2026-10-28: §9 "Fog (S2a) is unbuilt" superseded (D-066 §3; M6 closure track FOG; §1–§12 unchanged)

- **The fog is built and shipped ON** (M6 Track EX, ADR-044; sim.json `fog.enabled` true). "Known" is no longer
  all-known in production: `PlayerKnowledge.Of(world, player, cfg)` is built from `PolityKnowledge.Of` — the stored
  extent, the seed discs, formation sight and settlement sightings. Both gates apply, as §9 anticipated: a settlement the
  player has not discovered is hidden (P7), and a known foreign one is closed books (D-060).
- **The leak gate now runs on the production fog** (D-066 §3): `Sim.Ui.Tests` `KnowledgeLeakTests` (and its Military
  file) gate every family through the production seam with the AI unsighted, no knowledge stub; the stub and fog-off
  legs are kept as controls only.
- **Decision A (§11, the drawn token's size) stays DEFERRED** — it was deferred "until fog-of-war visibility and
  information-disclosure rules are designed"; the M6 fog draws a rival only once it is known, and the token's sizing of a
  known rival is unchanged. It is not ruled here.
