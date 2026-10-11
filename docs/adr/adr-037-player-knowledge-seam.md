# ADR-037 — THE PLAYER-KNOWLEDGE READ SEAM (ALL-KNOWN) AND THE AI'S INFORMATION CLASS

**Status:** PROPOSED. Written 2026-10-11 by packet P7 of the M5 map / exploration / settlement workstream (branch
`m5w-p7-player-knowledge-seam`, cut from `m5w-integration` @ `b49ae203`, LOCAL, not pushed, not merged).

**Governing record:** Spine principle 7 (frozen; `docs/civ-sim-architecture-v3-outline.md`: AI actors use the player's
verbs and information class — information asymmetry is permitted, rule asymmetry is not); the Director's P7 addition
(*"the AI cannot use information it has not legitimately discovered"*); D-040 A4/B1/B2/B5 (the map is DISCOVERED — a
computed, permanent extent at the traversal lattice's granularity; its storage is the building packet's, S2a); D-039
C1 (a formation not observed is UNKNOWN; last-seen GHOSTs need persisted sighting memory, M7). The workstream plan's
P7 packet and the critic's additions (a) and (b).

**Touches the kernel contract:** no. `Sim.Core/Kernel` is untouched; no schema, order, system or pipeline change; no
golden moves (§5). New public Sim.Core surface (decision ledger §29 asks for new seams to be flagged): the type
`Sim.Core.State.PolityKnowledge`; the constant `TraversalLattice.DefaultStride` (the default stride argument, named);
`LabourActivities.IsHerded`; optional `knowledge` parameters on `InfoQuery.Card`, and `PolityKnowledge` overloads of
`AiFoundingPolicy.Decide` / `BestSiteFor`.

---

## 1. The decision

ONE read seam answers what a polity knows: `PolityKnowledge` (Sim.Core/State) — `Known(node)`, `Visible(node)`,
`KnowsCell`, `KnowsSettlement`, `KnowsUnit`, `KnowsPolity`, and `ReadsInternals` (a settlement's books are its
controller's). Granularity: the traversal lattice's 16 km node (D-040 B5); a cell is known iff its stride block is.

* **All-known today.** `PolityKnowledge.Of(world, polity)` is THE production entry and restricts nothing: no extent is
  stored before S2a. Its **only future input is S2a's stored known extent**; the visible set is derived each turn from
  PREV (never stored). Nothing else may feed it — no UI flag, no difficulty setting, no second table.
* **Derived answers.** `KnowsSettlement` = controls it, or its site is VISIBLE (live state is shown only while seen —
  no ghost memory before M7); `KnowsUnit` = owns it, or where it stands (its station's site when stationed) is visible;
  `KnowsPolity` = itself, or contact through a known settlement. Whether a known-but-unseen foreign settlement is drawn
  at all is the fog ruling's (S2c).
* **The test seam.** `PolityKnowledge.Restricted(world, polity, known, visible)` builds a stub from explicit node masks
  (Visible ⊆ Known enforced). Its only non-test caller is the headless harness (`Sim.Ui/Headless/KnowledgeStubs.cs`),
  pinned by the source guard.

## 2. The player (Sim.Ui)

`PlayerKnowledge` (Sim.Ui/ViewModel) is a per-turn VIEW built from the seam for the player's Empire — every answer is
the seam's; it only caches the per-settlement and per-polity answers and adds `IsHiddenSettlement` (exists and not
known). `UiSession.Knowledge` rebuilds it after every step. Every player-facing reader goes through it:

| reader | what it does with knowledge |
|---|---|
| `WorldProjection.Build` (observer argument) | hidden settlements, formations, territory blocks, dirt-path steps (both nodes known), built routes (both ends known) are not in the projection, and not read; `Absent` counts only shown structures. The lens paint, `LensFrame` and every hit test read the projection, so picking is filtered at once. |
| `SettlementSelection.CycleNext` / `Opening` | Tab cycles known settlements only; the session opens on the first known one. |
| `HudModel.From` | world totals (population, food, settlement count) sum known settlements (dataGap default — the fog ruling may redefine a "world total"); a hidden selection reads as none. |
| annals (`UiSession`, `StateChronicle`) | an event is appended only when every settlement and polity it names is known; a re-equipment counts known formations only. Filtered in UiSession before a line is appended. |
| `PlayerViews.Settlement` / `Empire` | a hidden settlement reads as absent; listed formations, the "more attractive" migration comparison, trade and road lines name known subjects only. |
| `MarketModel`, `NeedsPanelModel` | a hidden settlement's rows are never read. |
| `UnitCardModel.Build`, `InfoQuery.Card` (formation) | an unknown formation reads exactly as an id that names nothing. |
| `TrendsModel.World`, `HistoryBuffer.Capture` | the world series (what `ImGui.PlotLines` is handed) sum known settlements only. |
| the selection (`GameUi.RefreshHud`; added after review) | a selection the player no longer knows — a step moved it out of sight (S2a's ordinary case), or a surface named a hidden settlement — is dropped (−1) before any reader is keyed on it; RefreshHud runs after every step and every selection change. Behind it, each selection-keyed reader reads a hidden settlement as no selection: `TrendsModel.Settlement`, `HistoryBuffer.Price` (Market and Trends), `ScreenModels.Settlement` / `Policy` (the labour record), `ActionSurface.ForSession` (its "not ours to command" note). |
| player text | raw settlement ids are gone from player text: `HudModel.TitleLine` says "Unnamed settlement" / "No settlement selected" (it said "Settlement N"; ids are dense, so a colony's id counted the world's settlements). |

`InfoQuery.DepositSector` no longer scans settlements (it asked `GoodsOf` of the first settlement anywhere holding the
deposit, foreign ones included): it is production's content rule, `LabourActivities.IsHerded` (livestock and fish are
Herding/fishing, every other deposit Extraction). The one observable change: in a world where NO settlement holds a
livestock or fish deposit, that good's card now names Herding/fishing (the old fallback said Extraction, which is not
what production would do with such a deposit).

## 3. The AI (Sim.Core)

* `AiFoundingPolicy`'s SITE SCAN asks the seam first: the pre-filter's first arm is `KnowsCell` for the issuer, so an
  unknown cell is never admitted and never raises the ranking's bar — an UNKNOWN cell is never picked. The pick is NOT
  a function of known terrain only (corrected after review; the first text of this section claimed it was): a known
  cell's valuation is the frontier score raster, which integrates terrain under unknown nodes (§6). All-known today, so
  the order is byte-identical. Recorded, not hidden: that valuation channel, the reachability flood across the
  issuer's whole connected component, and the standing-site occupancy / spacing / claimed masks — the predicate's own
  rule arms, read over every settlement as FoundingQuery reads them (S2b decides all three).
* `AiConstructionPolicy` asks `ReadsInternals` (the availability predicate's own control arm, toy-world guard
  included) BEFORE it reads any settlement's queue — it used to read every settlement's queue and discard foreign ones
  at the availability predicate. No behaviour change (a foreign queue could never change the output); pinned
  structurally.
* `AiFoundingPolicy.IsDue` tests control before reading a settlement's founding turn.

## 4. The leak gate

The gate of record is METAMORPHIC on VALUES (Sim.Ui.Tests `KnowledgeLeakTests`; Sim.Tests `PolityKnowledgeTests`):
the one-AI world after six played turns, seen through a stub that hides the AI polity whole; everything of that
polity is perturbed (people and stocks through the Ledger, deposits, dwellings, structures, queue, labour, deficits,
grievances, needs, prices, class states, formations — one added —, tax, Age, control, conversions, and the terrain
under every unknown node), and hidden EVENTS are planted. Identical under the stub: projection + lens (SVG bytes and
LensFrame), HUD, player views, market, needs, Tab, unit and formation cards, good cards, both chronicles' annals, the
Trends world series and `ForPlot`, the history buffer's world series, and FrameText of every player section of the
real UI. Each has a POSITIVE CONTROL (all-known, the same perturbation changes it). After review, the selection-keyed
readers too: a hidden settlement's own Trends series and price series are identical under the stub whatever it does
(control: all-known they move); each reads a hidden settlement exactly as no selection (control: all-known each reads
it); and two plays of the real UI that differ only in WHICH rival settlement was selected before the step hid the
rival draw the same text in every player section, naming neither (control: when nothing is hidden, they differ).
The subject filters each have their own test and control too (review finding 3): a trade flow each way and a travelled
road planted between a known and a hidden settlement leave the Empire view's Trade and Roads blocks and the
projection's routes identical through the stub (all-known, each changes and names it), and real settlement records
whose hidden neighbours are the most attractive leave every known settlement's Migration comparison identical
(all-known, it names the hidden place).

The AI: raising every unknown cell's own score cannot move the founding pick (control: all-known it does); whatever the
TERRAIN under unknown nodes (fertility 0 or 1 over half the map), the pick is a known cell and the oracle's best known
legal cell (control: all-known, fertility 1 there puts the pick on an unknown cell); the valuation channel of §6 is
QUARANTINED, not skipped (`KnownCellsValuations_StillIntegrateTerrainUnderUnknownNodes_…` asserts it is still open and
fails, naming the invariance tooth to restore, the moment S2b closes it); perturbing every row of every settlement the
AI does not control, and every formation it does not own, leaves `AiOrders.For` identical over twelve played turns
(control: emptying its own stocks changes its orders).

`scripts/check-read-isolation.sh` gains an ADVISORY section (a grep cannot tell a guarded read from an unguarded one):
a raw read of `Settlements`, `MilitaryUnits`, `Buckets` or `Deposits` through a world receiver in Sim.Ui fails unless
the file is on `scripts/ui-raw-read-allowlist.txt` with its class and reason. The source guard reads the SAME list,
requires every `routed` file to reference `PlayerKnowledge`, rejects stale entries, and pins the stub's callers.

Gate: a tenth smoke state, **hidden rival** (the one-AI world through the stub), with two rows — "Tab never reaches a
hidden settlement" and "a click on a hidden position selects nothing". In every all-known state the rows are not drawn,
so those states' reports are unchanged.

## 5. Unchanged (measured)

All-known, every reader is byte-identical to the base: the founded seed-42 300-turn golden (`937577ff…`, two CLI
processes), the found-settlement leg and the 600-turn AI leg of ci.yml (hash logs and run logs byte-identical to the
base binary's), every SVG preview, the projection/LensFrame/SVG digests over seven worlds, and every pre-existing gate
row. The one intended text change is §2's raw-id title. Evidence: the packet's commit messages and
`docs/m5-playability-gate.md` §3.6.

## 6. Known remaining channels (not this packet's)

* **The AI's valuation of a known cell integrates terrain under unknown nodes** (recorded after review; §3 first
  claimed otherwise). `SettlementSiting.CandidateScores` — the score the site scan ranks — is region fertility (a box
  sum over ±`fertilityRadiusPx` = 3 px, which always reaches the neighbouring 4-px nodes) × water access (a BFS over
  the whole map, cutoff `waterAccessCutoffPx` = 24 px), and the candidate floor is the median of every positive-scoring
  cell. MEASURED on the dev rig with one AI after one step (Sim.Tests `PolityKnowledgeTests` rig; a throwaway probe for
  the numbers): hiding only the eight nodes around the AI's free pick (its own node known) and zeroing their fertility
  moves the pick 36463 → 42559, both KNOWN cells; cell 36463's score falls 48.96 → 16.00. With half the map unknown,
  changing only unknown fertility moves 472 (fertility 0) / 468 (fertility 1) known cells' scores, flips
  `IsFrontierCandidate` for 433 / 2,883 known cells, and moves the floor 18.81 → 19.56 / 26.54. No known cell's terrain
  changed in either. The player's outputs the UI leak gate covers are held to the stricter class (it perturbs that
  terrain and requires each of them identical), so the AI and the player are NOT yet held to the same information
  class here. What closes it — a
  valuation over known cells only, and what a polity assumes of terrain it has not seen — is a modelling decision of
  S2b (ADR-036 §8 item (h)); this packet does not invent it. The floor is also the predicate's own rule arm (shared with
  the player's order), like the masks of §3.
* `RoadDevelopmentQuery.Routes` nominates routes to any settlement within 600 km — the player's road panel and the AI
  both see such a route's endpoint (a simulation predicate: S2b, with trade pairing and emergent colonization).
* A KNOWN foreign settlement's books (population, food, queue) are still shown in its player view, as before — what
  observation shows is the fog ruling's (S2c).
* The terrain bake and the river mesh are drawn whole (S2c's single-owner Fog layer); `MapInk.PolityOf`'s colour index
  is `polity % roster size`.
* The developer surfaces (DEV › TURN, RECORDS, ECONOMY tables) are unfiltered by design. The chronicle export
  (chronicle.txt) writes the session's annals, so it is filtered like the panel (all-known: identical).
* The forecast of D-051 §3 (not implemented) would step the full world; when both exist, that is a fold of hidden
  state into player numbers to resolve.
