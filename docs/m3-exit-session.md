# M3 EXIT SESSION — DIRECTOR'S BRIEF

Read this while playing. Every step says what it is testing.

---

## BEFORE YOU PLAY — ONE CHECK ON DOWNLOAD

Unzip, and **look at where `runs/` is** after you close a session.

- `runs/` at the **ZIP ROOT**, beside `Sim.Ui.exe` → **the launcher holds.**
- `runs/` inside **`app/`** → **it does not**, and the T3.11 packaging fix is wrong.

This is the one thing T3.11 could not measure. The launcher's `cd /d "%~dp0"` was
*reasoned* from cmd.exe semantics, not measured, because no Windows machine was
available to this project. Your download is the measurement. Either answer is
useful; report which.

---

## READ THIS FIRST — ONE OF THE FOUR THINGS YOU WERE TOLD TO WATCH CANNOT HAPPEN

The M3 spec's exit session says: *rule settlements into different production mixes, cause a
shortage, watch prices and trade respond, read the annals.*

**Three of those four are live. "Watch TRADE respond" is not.**

Trade volume on the canonical world is **zero**, and this is the measured, escalated, expected
state — not a bug you are hunting. Two independent causes:

1. **The deadband exceeds the largest price gap the band can express.** For bulk goods at map
   distances the threshold is ≈ 23–35; the maximum possible gap is `20.0 − 0.05 = 19.95`. Ores
   and stone are structurally untradeable overland at *any* divergence. The water counterfactual
   says the model is CORRECT — bulk moved by water — so this is a missing transport mode.
2. **Both sides of every pair sit on the same price band edge**, so the gap is identically zero.

**The Trade panel states this per good** — `GapZero`, `GapUnderDeadband`, `Numeraire` — so you can
confirm the reason rather than hunt for a response. Do not spend session time trying to provoke a
trade. If you *do* see a nonzero flow, that is a finding worth reporting.

---

## THE SESSION, IN ORDER

### 1. Confirm the build (30 seconds)
Window title and debug panel both read **`civ-sim M3 (<sha>, <date>)`**. Check the sha matches
the artifact you downloaded.
*Testing: that you know which build you are holding.*

### 2. Rule three settlements into genuinely different mixes (the core test)
Pick three. Give one a **granary** mix (farming-heavy), one an **extraction** mix, one a
**crafting** mix. Keep farming ≥ 30 % everywhere unless you are deliberately starving someone —
below that you will measure T3.8's collapse instead of the economy.
Run 30–50 turns.
*Testing: the milestone's whole premise — that settlements become different places. Watch their
stocks, prices and needs diverge.*

### 3. Cause a shortage (deliberately)
Take one settlement to **100 % farming** and hold it. Its crafting stops; pottery and cloth
consumption goes unmet.
*Testing: that the needs system reads the shortage. Expect **Comfort** to fall and grievance to
climb — Comfort is the residual accruer, see the baseline below. Shelter should hold up for a
long while: that is B-2's timber costume, and it is expected.*

### 4. Watch PRICES respond (this one works)
On the shortage settlement, watch the Market panel. Prices move on consumption, input demand,
production and stock release.
*Testing: the D-033 solver end to end. Expect many non-grain goods to sit at 0.05 or 20.0 — the
band edges — which is escalation 2 in front of you.*

### 5. Read the annals
Open the Annals panel; the export is written next to the exe.
*Testing: that the session produced a legible history, and that the chronicle survived the
M3 systems landing on top of it.*

### 6. Save and stop
Both files autosave to `runs/`, twinned by timestamp. The order log replays hash-identically:
```
sim replay --founded --seed S --orders runs/orders-<stamp>.bin --turns N
```
*Testing: the exit criterion that your session replays deterministically.*

---

## BASELINES — ALREADY MEASURED, SO YOU HAVE SOMETHING TO COMPARE AGAINST

**The reference mix (T3.9b gate, filed against T3.12).** Seed 42, **Nenatul**, applying as
**25 / 21 / 18 / 21 / 16**:

| reading | value |
| --- | --- |
| grievance | 6 |
| Shelter | 1.00 |
| Comfort | 0.98 |
| Sustenance | 0.97 |
| population | 2,095 and rising |

**Its recorded qualifications, which travel with it:** this was **not a controlled comparison**,
and it points **opposite to R1 on population**. Use it as a sanity anchor, not as proof that a
balanced mix is better.

**T3.8's after-column** — the same reading on the repaired tree:

| settlement | mix | grievance |
| --- | --- | --- |
| Mothian | healthy | **10.85** |
| Hikiavur | 100 % farming | **132.46** |

Hikiavur's residual is **Comfort's flow reading** — it has no pottery or cloth this period, so
Comfort reads 0.0000 and grievance accrues against it. That is the shape you should expect to
reproduce in step 3.

---

## ONE EXPERIMENT WORTH FIVE MINUTES — THE Q5 SATURATION QUESTION

**Question:** does Comfort saturate on almost any nonzero crafting?

Both existing data points are extremes — **0.98 at 21 % crafting**, **0.00 at zero** — and
nothing measures the middle. If Comfort is effectively binary, the crafting sector has no
interesting interior and the M4 household-goods model needs to know that before it is designed.

**The run:** two settlements in the **same world and seed**, differing **ONLY in crafting share**
— **15 % vs 25 %** — everything else identical. Compare Comfort after 30+ turns.

**Decompose it (§7.15) — do not read Comfort alone.** Comfort is a **fill ratio whose denominator
moves with demand**: a settlement with more people demands more pots, so the same crafting share
can produce a *different* fill. Record, separately:
1. pottery and cloth **demanded**;
2. pottery and cloth **eaten**;
3. the resulting **Comfort**;
4. **population**, because it drives the denominator.

Two settlements with the same Comfort and different demand are not the same measurement.

---

## WHAT TO REPORT BACK

1. `runs/` at zip root, or inside `app/`?
2. Did the three mixes visibly diverge?
3. Did the shortage show up as Comfort, as expected — and what did grievance reach?
4. Any nonzero trade flow (a finding if so).
5. The Q5 answer, if you ran it, with the four numbers.
6. Anything that looked wrong that this brief did not warn you about.
