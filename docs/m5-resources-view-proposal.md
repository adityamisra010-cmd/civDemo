# Resources view — proposal (Director's playtest feedback, 2026-10-19)

**Status: PROPOSAL, awaiting the Director's go-ahead. Nothing is built.**

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
