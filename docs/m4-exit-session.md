# M4 EXIT SESSION — DIRECTOR'S BRIEF

Read this while playing. Every step says what it is testing.

**What this file is.** The `session brief` deliverable of **T4.15 — M4 exit artifact**
(`docs/m4-spec.md:282`), which was never written. M3's analogue is `docs/m3-exit-session.md`
and the M3 criterion row cites it by name (`docs/milestones.md:239`). Its absence was the
one part of blocker **B1** an agent could discharge, and this is that discharge.

**What this file is NOT.** It is not the exit session. `m4-spec.md:384` names an act with a
named actor — *"Director exit session from the CI zip, log replaying hash-identical, with a
T3.12a replay report attached"* — and `CLAUDE.md:34` settles what an agent may substitute
for it: *"Definition of done = the packet's stated acceptance criteria. Your own unit tests
are additive, never a substitute."* No automated scenario discharges this criterion. The
MEASUREMENT half is already done and is attached below; the play-and-judge half is yours.

---

## BEFORE YOU PLAY — GETTING THE BUILD

The zip is built by `ui-artifact.yml` and is **`sim-ui-win-x64-<sha>`** under that
workflow's run for the candidate commit.

For **`95faa61`** it already exists and was produced by CI, not locally:

| | |
| --- | --- |
| artifact | `sim-ui-win-x64-95faa61` |
| run | [35568905793](https://github.com/adityamisra010-cmd/civDemo/actions/runs/35568905793) |
| size | 80,854,949 bytes |
| sha256 | `51967e0f35906022dc6834d607ed20371c444e9cbfa13b1143ada91c5551f848` |
| built from | `head_sha 95faa61`, conclusion **success** |

MEASURED, and worth knowing: until this audit, **no zip had ever been built for the
candidate.** `ui-artifact.yml` triggered on `['main', 't[0-9]*.*', 'art-*']` and the M4
candidate lives on `claude/civdemo-work-b1z2y4`, which matches none of them — so the very
commit the exit session is held against had no artifact. That run was a hand-pressed
`workflow_dispatch`; `'claude/**'` has since been added to the trigger so it is automatic.

**The one packaging check, carried over from M3.** Unzip, play a session, close it, and look
at where `runs/` is. At the **zip root**, beside `Play civ-sim.cmd` → the launcher holds.
Inside **`app/`** → it does not. Linux CI still cannot answer this; your download is the
measurement. Either answer is useful; report which.

---

## READ THIS FIRST — TWO THINGS THAT CANNOT HAPPEN, SO YOU DO NOT HUNT THEM

**1. No unprovoked famine will appear.** The famine-class disaster ships **COMPLETE, TESTED
AND INERT**: `disaster.hazardPerYear` is **0.0**. It was armed at the derived 0.01, the
fallout was measured — a world negative by construction and a broken permanent
dt-invariance detonator — and it was escalated as `docs/adr/cr-016-armed-disaster-fallout.md`,
which is **OPEN**. The rate is your ruling. Until it is taken, the only famine cause
reachable in play is **deliberate abandonment**, which step 3 below uses.

**2. Ordinary weather is never famine, by construction.** That is CR-015's ruling, and
`S_WeatherOnly_NeverFamine` holds it. Weather still bites — it produces STRESS and SEVERE,
and SEVERE still kills. If you see a settlement starve without a cause you gave it, that is
the adaptation ladder working, not a disaster you missed.

---

## THE SESSION, IN ORDER

### 1. Confirm the build (30 seconds)
Window title and debug panel both read **`civ-sim M4 (<sha>, <date>)`**. Check the sha is
the one you downloaded.
*Testing: that you know which build you are holding.*

### 2. Rule settlements into genuinely different mixes
Pick three. Give one a farming-heavy mix, one extraction-heavy, one crafting-heavy. Keep
farming ≥ 30 % everywhere you are not deliberately starving someone. Run 30–50 turns.
*Testing: the same premise M3 tested — that settlements become different places — but now on
top of M4's goods economy. Watch stocks, prices and needs diverge.*

**New at M4, and worth a minute:** every settlement now answers to an **Empire**. Founding
instantiates the world's one player-commanded Empire in the same operation that creates the
settlements, so there is no moment where a playable world has settlements commanded by
nobody. An Empire **may only build where it rules** (M4-D). The command source says WHO
decided and never WHAT the world does — a player-issued and an AI-issued order produce a
bit-identical world, which `EmpireOrderSeamTests` holds by world hash.

### 3. Cause a shortage — and this is the step that changed most
Take one settlement to **0 % farming**. Under CR-015 that row IS abandonment: farming and
herding both zero, which is one of the only two famine causes, so the settlement classifies
**FAMINE / Abandonment** rather than merely running a deficit, and its starvation reads the
WHOLE deficit with no adaptation.

MEASURED on the canonical world, seed 42, settlement 0 ordered to 0 % farm at turn 1, at the
**shipped** config (this is the §6 "scarcity can bite" criterion, demonstrated):

| turn | year | population | starvation | migrants moved |
| --- | --- | --- | --- | --- |
| 3 | 30 | 5,153 | 0 | 252 |
| **4** | **40** | **5,024** | **139** | **396** |
| 5 | 50 | 5,053 | 19 | 174 |
| 10 | 100 | 5,235 | 0 | 34 |
| 40 | 400 | 6,618 | 0 | 3 |

159 starvation deaths across the 40 turns; the ledger reconciles exactly at every turn
(discrepancy 0). The orderless twin never reaches FAMINE at that settlement.

*Testing: that the food balance decides famine, that the cause is named, and that the
response is proportionate. Watch the settlement inspector's food tab — it now states the
STATE and its CAUSE beside the number, not just the number.*

### 4. Watch MIGRATION respond — the T4.21 rework, and the thing most worth your eye
Before T4.21 a famine settlement could lose ~79 % of its people in one turn and the
destination could take an inflow of 17× its own population. Both are now bounded: flight is
an exact hazard on the best available exit, destinations are bounded by a vacancy the food
influx actually supports, and **vacancy is not attractiveness**.

*Testing: whether the bounded cascade reads as a civilization responding to hardship or as a
throttle. Numbers cannot answer that; you can. **This is the single most important thing to
judge in this session.*** Seed 42's Libur episode went from 79.2 % outflow to 22.0 %, and
starvation deaths there went **UP** 41 → 86 — the packet made the response proportionate,
not cheaper. Read that as a design question, not a regression.

### 5. Watch PRICES respond
On the shortage settlement, watch the Market panel. Prices move on consumption, input demand,
production and stock release.
*Testing: the D-033 solver end to end.*

### 6. Watch TRADE — and unlike M3, this one works now
M3's brief had to tell you not to hunt for a trade response because volume was identically
zero. It is not zero any more. MEASURED on the canonical world at turn 650: **2,797 units of
trade flow**. Merchants emerge on trade volume that now exists.
*Testing: the M3 exit criterion that was WITHDRAWN rather than failed, and travelled here.*
**GRAIN does not trade**, and only grain: it is the numeraire, pinned at 1.0 both sides, so its
gap is identically zero — `TradeArbitrageSystem.cs:133-137` calls that *"structural, not
incidental"*. Livestock and fish are food goods and DO trade. The M4 milestones entry says
*"No food trade"* flatly; that is overbroad, and this brief is the measured correction.

### 7. Read the annals
Open the Annals panel; the export is written next to the exe.
*Testing: that the session produced a legible history, and that famine now renders as prose
with its cause rather than as a number.*

### 8. Save and stop — then replay
Both files autosave to `runs/`, twinned by timestamp:

```
sim replay --founded --seed S --orders runs/orders-<stamp>.bin --turns N \
  --hash-log replayed.log --report-jsonl report.jsonl --report-every 1
```

*Testing: the exit criterion that your session replays deterministically, and producing the
T3.12a replay report the criterion asks be attached.*

---

## THE MEASUREMENT HALF, ALREADY DONE — so you judge rather than measure

`m4-spec.md:385` states the intent in terms: *"M4's exit should not make the director the
measuring instrument again."* Taken literally. MEASURED on `95faa61`, Release:

| | |
| --- | --- |
| director's own M3 session log, replayed | live-run hash **=** replay hash, `fbfe9821…e6f7` |
| hash logs compared | **650 per-turn hashes, bit-identical** (not just the final value) |
| an independent second live run | reproduces run 1 **bit-for-bit** |
| empty-order control, seed 42 | run = replay, `1901dfbd…3d12` |
| second seed, seed 3, held-exit log | run = replay, `8e1cd11b…9331` |
| T3.12a replay report | 650/650 turns, `replay-report/v1`, terminal hash matches the run |
| suite | Sim.Tests 885 passed / 0 failed / 4 skipped · Sim.Ui.Tests 296 / 0 |
| gates | banned-constructs, read-isolation, readonly-proof — all PASS |

**One thing that looks wrong and is not.** `docs/t4.14-review-record.md:132` records this log
at `baf50674…`. It no longer reproduces — but neither does it reproduce on `main`, which
gives `caa05873…`. That figure was taken on a tree predating the certified merge, so it is
stale against `main` independently of anything on the candidate. The criterion asks that a
log replay hash-identically **on one build**, which it does, three ways.

---

## KNOWN-OPEN, SO YOU ARE NOT SURPRISED MID-SESSION

- **CR-016 — the disaster rate.** OPEN. The mechanism is unarmed. Your ruling.
- **G1 — the harvest-weather decade variance.** Escalated and open; the shipped decade
  multiplier is 1.5× too variable in σ. No sigma was tuned pending your ruling.
- **B5 — the density quarantine's window.** The corridor is quarantined and its recorded
  window is now breached: seed 3 measures 0.35415668759623087 against a floor of
  0.3685744951368359, **3.91 % below**. Until this audit no instrument could see it; the
  nightly now reports it (and a second breach, migration at 74.12 % below its window, which
  no instrument had ever shown either). Neither is repaired: a corridor's disposition is
  yours, and CR-002/CR-003 both forbid fitting the instrument to the artifact.
- **The five post-certification packets** — T4.17, T4.18, T4.19, T4.20, T4.21 — are on the
  candidate and are not in the certified baseline. Schema is **v25** against `main`'s v24.

---

## WHAT TO REPORT BACK

1. Where `runs/` landed in the zip.
2. Whether the bounded migration cascade reads as proportionate or as a throttle (step 4).
3. Whether the famine prose and the food tab's state-and-cause are legible (steps 3, 7).
4. Your `runs/orders-<stamp>.bin`, so the replay report can be attached to the exit record.
5. The rulings that are yours and nobody else's: CR-016, G1, B5, and the merge.
