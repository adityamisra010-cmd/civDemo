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

**Not changed, recorded for a ruling:**

- The status band's POPULATION and FOOD figures still sum every settlement the player KNOWS (`HudModel.From`), which is
  every Empire's while all-known. They are not the empire's own.
- The EMPIRE view's trade lines likewise name known foreign partners.

