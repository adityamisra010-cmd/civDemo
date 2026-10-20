# Resources view — proposal (Director's playtest feedback, 2026-10-19)

**Status: IMPLEMENTED (2026-10-19, branch `b5-res`), UI only.** The Director's go-ahead, verbatim: "yes build resources
view. Remove the trends thing actually. Or make it my empire specific. i should not be able to see other empire's data.
trends and resource view get merged into the same view/button." What was built is §6; §1–§5 keep their words as proposed.

## 1. The feedback

> "i dont know where to see all resources. i can only see food. There should be a resources toolbar or something like
> in Manor Lords"

Today the top bar shows TURN, YEAR, POPULATION, FOOD, research and the Age. The game tracks 14 goods per settlement
(`Sim.Data/content/goods.json`), but the player sees only food at a glance:

| Group | Goods (ids) |
|---|---|
| Food | grain (1), livestock (2), fish (3) |
| Raw materials | timber (4), stone (5), clay (6), copper-ore (7), tin-ore (8), fiber (9), hides (10) |
| Made goods | bronze (11), tools (12), pottery (13), cloth (14) |

## 2. The reference

Manor Lords keeps a region-specific top bar (population, approval, supplies, regional wealth, livestock). Its supplies
readout is the months left before food or fuel runs out, showing the lower of the two. Storehouses hold materials and
granaries hold food. Holding Tab gives a quick overview of the numbers across the settlement. Sources:
[Hooded Horse wiki — Beginner's Guide](https://wiki.hoodedhorse.com/Manor_Lords/Beginner's_Guide),
[GamesRadar — Manor Lords tips](https://gamesradar.com/manor-lords-tips),
[GameLuster — 10 starting tips](https://gameluster.com/manor-lords-10-starting-tips/).

## 3. Proposed design

**A. Resource strip in the top bar** (after FOOD).
- Three compact groups: **Food · Materials · Goods**. Each shows the empire total of its members the player has, with
  a small rising / falling mark against the last turn.
- Only goods the player can use or holds: an unknown or zero good is not shown, so the Stone Age bar stays short.
- Own empire only (the P7 knowledge rule): nothing about a foreign empire's stores.

**B. Hover card per group.** Each good on its own line:
- the empire total;
- the change since last turn;
- how much is **set aside for projects** (batch-4 wording, never "held" or "reserved");
- the largest holding settlements.

**C. STORES panel** (a key, e.g. `R`, plus a click on the strip). A table, goods × your settlements, with an empire
column. Rows show what is set aside and what the Materials split leaves for roads. Like Manor Lords' Tab overview, it
answers "where is my stone?" in one place.

**D. Numeral-free eras.** Ages I–III keep their rule: counts are fine (population is already a count), no "%".

## 4. Why it is cheap and safe

- **UI only.** Every number already exists in state: `GoodStocks` rows per settlement; `EmpireStockpile.Holders` for
  "my empire's settlements"; construction set-asides as the batch-4 lines compute them. No system, order, save or hash
  changes.
- **Sessions stay compatible:** existing saves resume unchanged.
- **Tests:**
  - the strip's totals equal the sum of `GoodStocks` for own settlements (a populated-world test);
  - no foreign settlement contributes (a leak-gate row);
  - no "%" in Ages I–III;
  - the smoke gate covers the new controls.

## 5. Related feedback, recorded for a ruling

> "i think all 3 granaries must have been built in one go. they built one by one turn by turn"

- **The rule today:** each settlement's queue builds only its head project (ADR-039 / D-054 incremental construction).
  A second project starts when the first completes.
- **What the change would take:** building several at once changes construction itself — throughput, the batch-4
  set-aside priority, AI behaviour and the goldens. That is a simulation rule change for the Director to rule on, not
  a UI fix.
- **Options for the ruling:**
  - (a) keep one-at-a-time and say so on the queue ("builds one project at a time");
  - (b) parallel building split by the Building share;
  - (c) a per-settlement "work crews" count unlocked by research.

## 6. What was built (2026-10-19)

**What TRENDS showed, and what of it was foreign.** TRENDS (bottom bar, key 6) drew one graph. Its metrics were
population, food, deficit, happiness, grievance per class, inflow, outflow, births, deaths, harvest, eaten, dwellings
(`TrendsModel.Metrics`, from the observation history) and a price (the `HistoryBuffer`). Its WORLD scope summed (or
averaged) every settlement the player KNOWS (`TrendsModel.World` with `PlayerKnowledge`). Under the production view —
all-known until S2a — that is every settlement of every Empire. Its settlement scope and its price graphed whatever
settlement was selected, a rival's included. So every world-scope series, and any rival selection, showed foreign data.

**Built (Sim.Ui only; no Sim.Core system, order, save or hash changed):**

- **Resource strip** in the status band after FOOD: `Food stores · Materials · Goods`. Each is the empire total of its
  shown goods with a rising / falling / steady mark (a shape, not a glyph) against the start of last turn. Goods come
  from goods.json categories (food / raw / processed). A good is shown when the empire holds some now, held some at the
  start of last turn, or has some set aside. A narrow band drops the settlement count and the Age's name first, then
  the strip's labels to their first letters. Each group is a control (`strip:Food`, `strip:Materials`, `strip:Goods`)
  that opens RESOURCES.
- **Hover card** under a group while the pointer rests on it. Per good it shows the empire total, the change since last
  turn, the amount set aside for projects (`SetAsideQuery.ViewerTotals`: the player's own purse) and the three largest
  holding settlements of the player's. It is painted like the info hint (`StoresCard`).
- **RESOURCES** replaces TRENDS in the same slot and on the same key (6) (`Section.Resources`, button "RESOURCES").
  - **Stores table:** one row per shown good, with the empire total, the change, the set-aside and one column per
    settlement of the player's. Under it are the building/roads split's line (exactly where the EMPIRE view shows it)
    and a note on what "set aside" means.
  - **Each row is a control** (`store-<good id>`) that charts that good's stores.
  - **Trend graph, the player's own empire only:** the former metrics plus "stores". Scope is `trend-empire` or
    `trend-place`; the place scope applies to one of the player's settlements only.
  - **Own settlements, turn by turn.** The empire series sums a turn's records over the settlements that were the
    player's on that turn (`UiSession.Ownership`, UI-side, rebuilt by replay). A settlement taken from a rival brings no
    foreign past.
  - **Nothing foreign is drawn.** The view's world scope and rival selections are gone, not hidden by a toggle.
- **"Own" means the control relation** — the places the player's Empire controls (the set `EmpireStockpile.Holders`
  pools). The totals are `EmpireStockpile.Available` sums.
- **No `Settlements` row is read**, so the read-isolation allowlist is unchanged.
- **Counts only:** no "%" in any era.

Tests: `ResourcesViewTests` and `KnowledgeLeakTests.Resources.cs`; gate rows in `docs/m5-playability-gate.md`. Previews:
`Sim.Ui --resources-preview DIR` writes PNGs of real frames (`FrameRaster`).

**Follow-up the same day (the Director's instruction covers the top bar):**

- **The status band's POPULATION, FOOD and settlement count now read the player's OWN empire.** They use the strip's set,
  `EmpireStores.OwnSettlements` = `RoadDevelopmentQuery.PayingSettlements`: the control relation, or every settlement in
  a toy world with no control rows. Before, they summed every settlement the player KNEW (`HudModel.From`), which is every
  Empire's while all-known. FOOD is now the grain of your own settlements, so it equals the grain line of the Food stores
  card.
- **The rest of the band was audited.**
  - TURN and YEAR: the clock, not a sum.
  - Research chip: the player's own research.
  - Age chip: the player's own capital and Age.
  - The strip: own.
  - DEV's "last turn" digest: developer-only, deliberately world-wide (the TURN audit).
  - **No other always-visible figure sums foreign settlements.**
- **Click routes.** A click on POPULATION / FOOD routes the player to the EMPIRE view (own). With DEV on it routes to the
  TURN audit's world accounts, a developer surface, unchanged.

**Recorded for a ruling (not changed):**

- **The selection card of a selected rival settlement** shows its population (by age band), grain store, last harvest,
  happiness and grievance. The player can select any KNOWN settlement (P7), so a rival's internals are visible one
  settlement at a time.
- **The SETTLEMENT view, the market and needs panels and the unit card** of a selected rival behave the same way.
- **The EMPIRE view's trade lines** name known foreign partners of the player's own flows.

## 7. Status note — 2026-10-19 (later; dated, §1–§6 unchanged)

- **Merged and pushed** in `claude/new-session-yavghm` at `6d1988f`; not on `main`. The Director has not tried it yet.
- **§5's construction question is ruled.** D-058 (`docs/d058-build-one-at-a-time-capacity-carries-within-the-turn.md`):
  one project at a time, with the turn's leftover capacity carried down the queue within the same turn.
- **§6's "recorded for a ruling" items are defects, not open questions.** The Director's instruction already rules them:
  "i should not be able to see other empire's data". The 2026-10-19 audit confirms each one shows a rival's private
  figures, gated only by "known" (`PlayerKnowledge.IsHiddenSettlement`), never by "own"
  (`PolityKnowledge.ReadsInternals`). The fix is recorded in a later dated note.

## 8. Status note — 2026-10-19 (later; dated, §1–§7 unchanged): §6's ruling items are fixed (D-060)

The items §6 recorded for a ruling, and §7 called defects, are fixed on branch `b6-rival` (LOCAL). Record:
`docs/d060-rival-internals-isolation.md`.
- **The selection card of a selected rival** shows its name, owner and "Not your settlement - its figures are not known
  to you." — no population, grain, harvest, happiness or grievance.
  - Test: `D060_AKnownRivalsBooks_DoNotReachTheSurface_UnderTheProductionView_WhileThePlayersOwnDo` (case L1 selection card).
  - Test: `D060_TheRealUi_WithAKnownRivalSelected_DrawsItsNameAndOwner_AndNothingOfItsBooks`.
- **The SETTLEMENT view** (case L2), **the needs panel** (L3), **the market panel** (L4) and **the unit card** (L7 unit
  card: experience and army only; identity and station stay public): the same Theory, one case each.
- **The EMPIRE view's trade lines** were audited and are already safe. They list only flows with one end in the
  player's own settlements, naming the partner. No foreign price, stock or foreign-to-foreign flow is read (D-060 §7,
  L7b).
- **Also fixed** (not in §6):
  - POLICY's labour record of a rival;
  - the map's Settlement-zoom labels (L7 map projection);
  - a rival source's founding card (L6);
  - the ANNALS panel: `D060_TheAnnalsView_…` and `D060_TheStateChronicle_…`.
- **DEV stays world-wide** by decision; `D060_TheDeveloperView_…` pins it.

## 9. Status note — 2026-10-20 (dated, §1–§8 unchanged): resource persistence and the next-turn figure

**The Director's playtest of `f80dce6` (build inferred; see the known-issues note of 2026-10-20):** the resources view
and strip pass, except that a resource is omitted once its stock reaches 0.

- **Root cause.** `EmpireStores.IsShown` (§6: "held now, held at the start of last turn, or set aside"). A good that ran
  out showed one more turn with 0 and a falling mark, then vanished.
- **The rule now.** A good is shown once it is **unlocked** in this game, and stays shown for the rest of it, at 0 when
  empty. "Unlocked" keeps §6's meaning of use: the player's empire has held some of it — at the founding or after any
  turn (holding a good is how the player comes to use it; no research rule is restated). A good never held is still not
  shown, so the Stone Age strip stays short.
  - `ResourceCatalogue` (`Sim.Ui/ViewModel/ResourceCatalogue.cs`): UI-side, filled by the founding capture and the one
    Step path, so resume's replay rebuilds it and a new game starts empty. No file, save or hash.
  - It reads the player's controlled settlements only (`EmpireStores.OwnSettlements`): a rival's stores unlock nothing.
- **The next-turn figure.** RESOURCES has a "Next turn" column, and each card line ends "N next turn".
  - It is `UiSession.PreviewNextWorld()`: the production step (`BuildProductionExecutor`'s era and pipeline) run on
    the current world with this turn's queued orders, summed over the settlements that are the player's in that world.
    `TurnExecutor.Step` never mutates its input. The preview steps its own copy of the order log.
  - So the figure follows the simulation's accounting exactly: material charges, set-asides (part of the stores, not a
    deduction), project priority, affordability, D-058 carry-over, extraction, consumption and trade, each once.
    Nothing is restated in the UI.
  - **Not included:** the AI's orders of the turn (rivals' decisions the player has not seen; D-060). With no AI the
    figure equals the next turn exactly. With one AI it differs only where an AI order of this turn reaches the
    player's own stores. Measured: 40 turns of seed 42, with no AI and with one AI, with 0 mismatches on every good.
  - **Cost:** one turn step, about 120–180 ms (880 ms the first time, while loading the data), computed only when
    RESOURCES or a strip card is shown and cached until the world or the order count changes.
- **Tests** (`Sim.Ui.Tests/ResourcesPersistenceTests.cs`, 11 tests):
  - persistence at 0 through six turns, where the old rule drops the good;
  - persistence after further stock changes;
  - the catalogue equals the goods ever held (so goods never held are not listed);
  - a rival's stores never unlock a good;
  - game scope, and rebuild on resume;
  - 0 is drawn in the table and the card;
  - the next-turn figure equals the next turn for every good, and for a good at 0 now;
  - construction is charged once, and not the whole queue;
  - set-asides are taken once;
  - the preview changes no world, order or hash;
  - the next-turn figure reads no rival's stores.

  Four mutants were killed, each by a semantic test: the old show rule, a catalogue over every settlement, set-aside
  subtracted twice, and a preview cache that ignores new orders.
