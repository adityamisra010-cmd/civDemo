# M6 playtest checklist (the Director's playtest of the M6 slice; D-066 R8)

**Status:** READY TO RUN, NOT YET HELD.
- This is the human gate for exit criterion 10 (the vertical slice).
- The visual gate (criterion 5) has its own list in [e1-visual-acceptance-checklist.md](e1-visual-acceptance-checklist.md).
- Nothing below is claimed as passed.

## 0. Get the build

| Option | How |
|---|---|
| **Windows, pre-built** | Needs **authorization to commit and push** the working tree on `claude/new-session-yavghm`. The `ui-artifact` workflow then builds `sim-ui-win-x64-<sha>` and runs its `windows-smoke` job (README "Download & Play"). Unzip, double-click `Play civ-sim.cmd`. For flags, run `app\Sim.Ui.exe` from a terminal. |
| **From source (any OS with .NET 10)** | `dotnet run --project Sim.Ui -c Release -- --ai-empires 1` |

**Recommended start:**
- `--ai-empires 1` gives an AI rival, so fog, the AI and exposure have someone to act against.
- F12 or `--dev` opens the developer panels.
- `--resume <session-dir>` continues a saved session.

**Saves:**
- Sessions from earlier builds are refused on resume: the rules changed (ADR-054/055/057/058).
- Start a new game.

## 1. What to try (about 1–2 hours), and what "good" looks like

**Acceptance set (T6.18: at most five items): rows 1, 2, 3, 5 and 10.** Rows 4 and 6–9 are exploratory. They inform
the playtest but do not gate the slice; row 9 has its own gate in the E1 checklist.

| # | Area | Do this | Expect |
|---|---|---|---|
| 1 | Research and civics points | Open Progression; pick a technology and a civic | Two lanes; RP and CP both shown per turn. CP banks when no civic target is set. Base RP equals base CP at every turn length. |
| 2 | Civic states | Look at any completed civic | It reads **Researched**, never "Adopted": adoption is M8. Government forms read "Available — adoption in M8". |
| 3 | **Vertical slice: the law court** | Research `law_code` (civics, Age III). Then, in Construction at a settlement, build the **law court**. | Before `law_code`, the court is not offered (and an injected order is refused). After it, the court is listed. It builds, is founded the step after completion, and matures over years. The INSTITUTIONS lens walks Unresearched → Available → Under construction → Active → Mature. The regime label changes "chiefdom" → "law-giving chiefdom" while you hold a court. Unrest service improves through the existing equation. |
| 4 | Eurekas | Settle near coast, river or deposits | Seven new Eurekas can fire from what you actually know (e.g. coast → salt extraction; a prospected coal deposit → coke). A concealed deposit never triggers one. |
| 5 | Explore / Prospect / fog | Actions menu: Explore, Prospect | Explore widens what you know (food cost). Prospect reveals concealed deposits. Unknown land stays unknown; remembered land stays known. You see rivals only where you can see them. |
| 6 | AI under fog | Watch the AI's first colonies | The AI values only land it knows. Its first founding comes a few turns later than before (turn ~44 on seed 42). |
| 7 | Knowledge exposure | Only after you know trade AND trade with a known foreign settlement | Exposure credit accrues toward their known nodes, capped with Eurekas at 40 % of base cost. **In ordinary play this rarely happens** (see §2). |
| 8 | Unit card | Select your warband | Composition line when formations are grouped. The inert "Training — planned for M7" line. Recruit / re-equip / disband controls (the M7 pass). No movement. |
| 9 | Map visuals (E1) | Zoom from World to Settlement band | **Judge the eight-part cap** (D-067 R3, provisional until this gate): is a settlement with every building kind legible? One light and contact shadows; building parts staged by count (1/10/20/30) with the number at high zoom; relief, forest and field marks on known land; identity silhouettes; one grouped token per place and identity. Full list: the E1 checklist. |
| 10 | Session export | Quit normally | A `session-<stamp>.xlsx` appears beside the session files. **Open it in Excel** (or LibreOffice): eight sheets, the Telemetry sheet coded per turn. Opening it in a spreadsheet program is a human check nobody has done yet. |

## 2. Known limits (judge them; don't report them as new)

- **Movement-dependent fog is deferred to M7 (D-066 R1):** ghost rows, scouts, sighting from moving units, claim/trade/road routing on the known extent.
- **The founding legality rule still reads unseen land, for you and the AI alike.** Assigned to M7 (DW-002).
- **Contact exposure is dormant in ordinary play.** Trade is research-gated at both ends; the AI learns trade around turn 540.
  - Director question: should contact also come from proximity or sighting?
- **The AI founds law courts only after about turn 500.** You may be able to reach the court earlier by prioritising `law_code`; this is NOT measured — record the turn you research `law_code` and the turn the court is founded.
- **Soldiers in formations don't die while serving.** ADR-055 §4 has no attrition until M7.4, so a starving settlement's warband outlives it.
- **The founding warband draws 20 people from your first settlement,** which moves early history.
- **Economy packets EC-1..3** (iron, horses, gunpowder) are rarely reached by the AI in normal play.
- **E1:** the frame-cost budget the E1 track set itself (2.0× baseline) is exceeded on near-empty maps (≤ 2.8 ms per frame). Large worlds are unchanged.
- **Stone-poor settlements (D-051 §2 deferred, D-067 R19).** There are no common-material floors and no regional
  richness classes yet: stone follows the raw elevation channel, and in the earlier (M5) playtest 38 % of settlements had
  effectively no stone (`m5-playtest-known-issues.md` §3). An empire's stock is shared, so a stone-poor city builds
  once the empire holds stone; a city outside an empire builds only from its own stock.
- **No Workshop before bronze (D-051 §4 deferred, D-067 R19).** The Workshop is not offered until its tools can be
  made (the `InputUnobtainable` guard; the card shows the chain to tin bronze, Age III). Primitive stone-and-wood
  toolmaking does not exist yet (`m5-playtest-known-issues.md` §6).
- **Migration is free and unrestricted** across every border in every Age, at the lower rate the Director accepted
  (D-067 R11). No border, entry/exit or treaty control exists; some research texts describe such controls (e.g.
  `frontier_fortresses`, `manorialism`) that the simulation does not have yet (M8, D-067 R13–R14).
- **Exporting a long session at quit takes time:** about 2.5 s for 68 turns, so about 25 s for 600 turns.

## 3. Record

For each row, write: OK, a defect (with a screenshot), or a design note. Attach the session folder: its `.xlsx` workbook now carries the whole record.

## Dated note — 2026-10-28 (D-067)

- §2 now carries the M6.24 disclosures (stone-poor settlements; no Workshop before bronze), required by D-067 R19, and
  the migration disclosure (D-067 R11–R14).
- Row 9 asks for the legibility verdict on the provisional eight-part cap (D-067 R3).
- The early-court claim is now a measurement to record, not a stated fact: no test or measurement supports it.
- The executable plan for this playtest (build, seeds, scenarios, measurements, pass/fail) is in
  [m6-exit-inventory.md](../m6-exit-inventory.md) §7.

## Dated note — 2026-10-29 (D-068)

The playtest continues on the build carrying the [D-068](../d068-m6-playtest-fix-rulings.md) fixes:
- Re-check row 1 (Technology `[K]` and Civics `[C]` are now separate top-bar chips).
- Re-check row 5 (roads now stop at the fog).
- **Row 10 PASS now means:**
  - the workbook appears;
  - the folder holds no `telemetry-*.jsonl`;
  - the Orders and Chronicle sheets read in codes, explained by the Codebook.
- **New observation:** advance an Age and judge whether the panel change is now obvious. The map background is unchanged by design.
