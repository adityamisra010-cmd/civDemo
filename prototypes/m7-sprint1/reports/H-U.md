# H-U — M7 Phase C lane U: war notifications (research and design proposal; docs only)

Date 2026-11-09. Clone `/home/user/m7w/U` (`git clone --shared` of `/home/user/civDemo`), branch `lane/U`, cut from the
development branch `claude/new-session-yavghm` at `1601c74`. The clone did not exist at the start and no
`U-wip.bundle` existed, so the lane started fresh (an earlier draft was lost with the old container; nothing of it is
reconstructed from memory).

**DOCS ONLY.** Nothing under `Sim.Core`, `Sim.Data`, `Sim.Cli`, `Sim.Tests`, `Sim.Ui` or `prototypes/m7-sprint1/Proto.*`
changed (D-079 Q5 is open). No project, build file or test was added. Every design statement is a **PROPOSAL** under
D-080 WA3-D; nothing is ratified. Labels: **MEASURED** = read or run in this clone; **INFERRED** = reasoned, not run.

Authority: D-080 WA3 (DECIDED: observers may see battle results as a side-panel notification, opened on click), WA3-D
(DELEGATED: the notification design; presentation only; reads fog-filtered published state; never writes simulation
state), WA3-R (READING, flagged: observers are polities that observe the war under fog; no war economics for
observers); D-079 DR12, DR15–DR18, DR22, DR36–DR38, DR41, R8, Q2, Q4, Q5, Q12; ADR-061 §3.6, §3.12; the 2026-11-05 plan
revision (INV-5, INV-8, INV-10; C7, C12, C19, C23, C31–C33, C35); D-060; D-039 A2, B5, C1; D-069.

---

## 0. What now exists

| Item | Where | State |
|---|---|---|
| The design proposal | [`docs/m7-plan/notification-panel-proposal.md`](../../../docs/m7-plan/notification-panel-proposal.md) | PROPOSAL, 760 lines at `e38bbcc` (MEASURED, `wc -l`); revised in verification rounds 1 (§10) and 2 (§11), line counts in §10.4 and §11.5 |
| This report | `prototypes/m7-sprint1/reports/H-U.md` | — |
| Research copies (four files read from a mod repository) | The lane's scratch area only (`scratchpad/m7c/U-research/dl1/`), identified by SHA-256 in the proposal's Annex P | Not in the repository (third-party code; reference only) |

The proposal covers every item of the brief: the notice catalogue for every event family on the branch (§6: T1's
significant list, T2 War Pulse and orders, T3 two-action windows, battles and endings including the unratified Standoff,
AI-vs-AI AutoResolver results, sieges, war outcomes, war economics, naval and ramming), with class (decision item or
informational), recipients under fog, icon, hover, click, stacking and expiry per kind; the relation between decision
windows and notices (§9); accessibility (§11); a performance budget to measure (§12); the data contract (§10); the test
plan (§13); the fit with `Sim.Ui` (§14); the Director's open points (§15); and the labelled reference research
(Annex R, Annex P). *(Verification round 1: "every event family" was not complete. U-V6: the war-outcomes catalogue
missed the captive leader's execution, `FateCause.ExecutedAsCaptive`, now N-310; U-V3: the non-war T1 kinds had no
rows, now proposal §6.9.)*

---

## 1. The design in brief (PROPOSAL; full text in the proposal)

- **Surfaces.** Decisions (a tray, only while a decision window is open for the player; the clock is held until
  **Resume**); Dispatches (a rail at the right edge of the map rect; never stops the clock); the Dispatch log (a
  filter of the ANNALS section); the Battle report (a DETAILS card in the context column: the "side panel" of WA3).
  *(Superseded in part, verification round 1, U-V1: the tray was placed "above End Turn" and the rail "at the lower
  right of the map rect", on top of existing controls. The tray is now the window's mode bar at the top of the map,
  placed by the placement bar's rule, with its own Resume; the rail stands on the LEGEND tab; proposal §3.)*
  *(Superseded again, verification round 2, U2-1 and U2-2: that mode bar stood over the Age panel. The tray is now the
  first block of the context column while a window is open, the column opening for the window and returning to its
  prior state at Resume; the rail yields to every existing control in its column, and the overlay key stops short of
  it in both of its forms; proposal §3.)*
- **Class rule.** A notice is a decision item if and only if it is a hit of the player's open decision window
  (T1 `SignificantKinds`, T2 `WindowsFor`, ADR-061 §3.6). The significant list stays the Director's (D-079 Q2): the
  catalogue marks kinds "D (if approved)", "D-candidate" or "I". A decision item never forces a choice (defaults
  Occupy and Hold exist, H-W §3.1). *(Verification round 1, U-V12: also the TacticalResolver at a battle joined, N-101;
  U-V3: four labels corrected to follow the rule, and rows added for the non-war T1 kinds. Verification round 2,
  U2-4: two more labels, N-201 and N-401, corrected; U2-5: a capture made by the taker's own order at T offers no
  choice, so N-301 has a lapsed, informational form.)*
- **Fog rules F1–F7.** One source (the player's published rows and `PlayerKnowledge`, guarded by a source guard);
  observers get battle results only (place, learned instant, known combatants, public outcome class; no numbers in
  M7); books stay with their owner (D-060); unknown combatants are "an unidentified force"; a notice's time is the
  instant learned; no sub-step pacing or counting while the clock runs; reports are stale, never wrong; no raw ids in
  player text and a per-recipient dense `LogSeq`. *(Verification round 1, U-V4: the catalogue's non-result observer
  notices, N-601/N-602, N-303 and N-309, are now marked as widenings of WA3, off unless the Director approves them,
  U-Q5 and the new U-Q10.)*
- **Order.** ~~Every list sorts by `(KnownAtMs, KindRank, SubjectId, LogSeq)`, all integers; `KindRank` = the approved
  significance priority for significant kinds, 1000 + the kind's code otherwise.~~ *(Superseded, verification round 1,
  U-V2:)* the tray has one entry per window hit, in the window's own key order (class, priority, source, sequence);
  every other list sorts by `(KnownAtMs, KindRank, SubjectId, LogSeq)`, all integers; `KindRank` is one integer per
  kind from one table, not the significance priority.
- **Expiry.** Decision item: while its window is open, then (*verification round 2, U2-7*) the form its catalogue code
  names: a dispatch, its log line only, or an ongoing notice. Dispatch: until `S(TurnOf(KnownAtMs) + 2)` (survives one
  End Turn) unless dismissed or pinned. Ongoing notice: while its condition holds. Order note: until the next stop or
  turn start (*U2-6*: an order whose boundary never comes gets a note that promises no instant, and a lapse note once
  the producer publishes a lapse row). Every notice keeps a log line.
- **Data contract.** Input: a per-recipient published event-row log from the knowledge layer (C7: M7.8, M7.12; C19's
  observation rule: M7.14; *corrected in verification round 1, U-V5: it said "M7.8; C7, C19"*), with
  observer rows as a separate row type without book fields. Output: a pure `NotificationFeed.Build(knowledge, rows,
  window, uiState, nowMs, calendar)`.

---

## 2. Research provenance

| Source | Result | Label used |
|---|---|---|
| `raw.githubusercontent.com`: a mod repository's copies of the reference game's interface scripts, branch `master` (repository, URLs and full hashes in the proposal's Annex P) | Reachable by `curl` through the session proxy. Read in full: `Assets/UI/Panels/notificationpanel.lua` (1,573 lines, SHA-256 `25701ebd…cbc90b`), `Assets/UI/actionpanel.lua` (1,189 lines, `34eaee01…5553a`), `Assets/UI/Panels/statusmessagepanel.lua` (421 lines, `91f7efb8…765a`) and `.xml` (33 lines, `feeb257f…7440`); the mod's manifest file was used to locate them. These are the mod's edited copies (some lines marked with the mod's name), not a verified release of the game. The notification panel's layout XML is not in the mod and was not read. | [CODE] — 11 of the 15 research rows (R-1 to R-11; R-1 partly [KNOW]) |
| GitHub API for the same repository | HTTP 403: "GitHub access to this repository is not enabled for this session". Not pursued (raw reads sufficed; attaching a repository was not needed). | — |
| WebFetch of result pages | Failed for two hosts, a platform's community pages and a fan forum (named in Annex P; the hosts do not resolve from this session; they would have to be allowed in the session's network settings). After the second host failed, no further hosts were tried. | — |
| WebSearch (7 queries, in this order: action-panel blocking types; a notification log or history mod; the notification layout file; player complaints; and the message settings or end-turn notices of three other strategy games, named in Annex R, R-13 and R-14) | Summaries only. The blocking-types and layout-file queries found nothing usable (the scripts answered those). Some complaint results concern the successor title; the proposal says so. | [SEARCH] — R-12 to R-14 |
| General knowledge | Alert rows for persistent conditions in other strategy games; the lower-right corner of the reference rail; news gating by diplomatic access in the reference | [KNOW] — R-15, part of R-1, one cell of R.3 |

**Originality (D-069).** Other games are named only in the proposal's Annex R and Annex P, which are labelled reference
and live in `docs/` (MEASURED after verification round 1: `grep -n -i` for the game, studio, mod and host names finds
them in the proposal only at lines 834–882, inside Annex R, which starts at line 822, and Annex P, which starts at line
874, and nowhere in this report). *(Re-measured in verification round 2 with the same `grep -n -i`, after the
proposal grew: lines 949–997 of the proposal, inside Annex R, which now starts at line 937, and Annex P, which starts at
line 989; still none in this report.)* *(Corrected in verification round 1, U-V11: the earlier statement measured only the
proposal, while this report itself named the mod, three other games, two hosts and a mod-hosting service in §2 and §8;
those names now live only in the proposal's annexes, in `docs/` as D-069 R1 asks.)* The design sections use this
game's own terms (Dispatches, Decisions, Ongoing, Resume, Battle report, Dispatch log); no player-facing string proposed
names another game. The scripts are cited by identifiers, constants and line numbers, not reproduced. Icons must be
drawn for this game with recorded provenance (`AssetManifest`).

---

## 3. Findings on the branch that shape the design (MEASURED by reading the code and reports at `1601c74`)

| # | Finding | Evidence | Consequence in the proposal |
|---|---|---|---|
| U-F1 | Only T2's world publishes per-polity knowledge (`KnowledgeEntry`, `WindowsFor`; lane E's world wraps it for pulses and windows). The siege world has no fog; lane E's `CascadeEvent` carries a single `Polity` and is not filtered by anyone's sight; lane W's records are authoritative rows with polity fields and no recipient. | `WarTheaterWorld.cs` (`KnowledgeOf`, `WindowsFor`); H-S §11 ("No fog in the siege world"); `WarEconomyWorld.cs:60`; `OutcomeRecords.cs` | WA3's observer notices need the knowledge layer (C7: M7.8, M7.12; C19's observation rule: M7.14; *corrected in verification round 1, U-V5: it said "M7.8; C7, C19"*) to publish per-recipient rows; the proposal states that input contract (§10.1) and does not invent the knowledge rule. |
| U-F2 | Lane E's `CascadeEvent.Polity` is the army's owner for most kinds and the locality's owner for `Plunder`, `LocalFamine`, `Occupation`, `OccupationEnded`; a contribution is logged under the army's owner only, though the locality's owner pays it. | `WarEconomyWorld.cs:445`, `:515`, `:520`, `:779`, `:803`, `:956` | One row per recipient in the published log, not one polity per event. |
| U-F3 | `HungerBegan` and `MovementRestricted` are emitted at every pulse start at which the condition holds, not only when it begins (`ArrearsBegan` is emitted once). | `WarEconomyWorld.cs:662–663` (in `OnPulseStart`), `:882` (in `OnPulseEnd`) | Treated as conditions (ongoing notices that log begin and end only); a naive per-event notice would repeat every pulse (90–180 pulses per turn per war at T2's proposed lengths, T2 §7). |
| U-F4 | A capture made by an order at T (lane S's immediate assault) is settled when the order completes, after the window at T has closed, so its consequences raise no decision-window hit at T (lane W pins this for the leader-fate change). *(Extended in verification round 2, U2-5, MEASURED: no `SettlementTaken` hit either, so the taker's disposition, offered only at T, has lapsed by its next stop, while an AI taker disposes at T by doctrine; §11.1.)* | H-W §2.2 last row, limitation 11; *(round 2)* `WarOutcomeWorld.cs:665`, `:698–710`, `:756` | The tray cannot show such consequences at T; they reach the player at the next stop (proposal §9 item 10; a property of the window mechanism, not changed here). *(Round 2:)* N-301's lapsed form, and U-Q11 for the Director. |
| U-F5 | Lane W's `BattleKind` has Field, Siege and Storming only; naval battles reach the cinematic selector as field battles. | `OutcomeTypes.cs` (`BattleKind`); H-W limitation 15 | The "naval outcome" distinction C35 keeps needs a domain field before a notice can show it (§15 dependency). |
| U-F6 | `CinematicSelector` already maps authoritative ends to presentation classes as a pure function (Standoff, BothEliminated, BothDisengaged → Inconclusive; siege ends → Surrender, Stormed, SiegeRaised). | `OutcomePresentation.cs` | Notices reuse the same mapping, so notices, cinematics and records cannot disagree (INV-10). |
| U-F7 | The Standoff end is unratified (P-TR1) and load-bearing for termination (its removal hung, H-TR §4.1, mutant M21); the AutoResolver is not at parity (16 of 29 scenarios, 4 of 9 held out). | H-TR §10, §8.4; H-P §5.4 | Notices show Standoff as inconclusive, flagged; the AutoResolver's result is an ordinary result (shown as "Auto" in the developer view only); nothing depends on adoption. |
| U-F8 | `Sim.Ui` has no audio subsystem; the existing End Turn toast is UI-time only (6 s, at most 4 lines); the annals are UI-side, rebuilt by replay and filtered by subject (P7) and books (D-060); `KnowledgeLeakTests` runs two lenses with positive controls and a source guard. | grep (0 files with audio types); `NoticeToast.cs:17`, `:20`, `:53`; `UiSession.cs:343`, `:356`; `KnowledgeLeakTests.cs:121`, `:806`, `:829`, `:1685` | No sound proposed; the toast stays; the log is an ANNALS filter; leak tests join the existing harness. |
| U-F9 | Two leak channels follow from the time model (INFERRED from ADR-061 §3.6 and T1 R4): every significant event splits the turn whether or not anyone can see it, so a UI that paced or counted sub-steps would reveal hidden events; a global sequence or raw battle ids in text would count other polities' events. | ADR-061 §3.6 ("a pause can never reveal an event the human cannot know"); T1 R4 | Rules F5 and F7, tests L3 and L8. |
| U-F10 | *(Added in verification round 1, U-V1.)* The map rect is not empty at its edges: the LEGEND tab holds its bottom-right corner (with the legend above it while shown), the OVERLAYS tab (with the NOW USABLE block, controls and key) its foot right of the left column, the Age panel or the formation card the left column, the placement bar and the toasts the top, and the folded column's tab the top right. *(Extended in verification round 2, U2-1 to U2-3, MEASURED: the Age panel is 480 px wide against the selection card's 320; the overlay key's form above its tab, and in the map's upper half, runs to the map's right margin; and some of these controls already collide with each other, §11.1.)* | `GameUi.cs:1212–1213`, `:1702–1709`, `:913`, `:837–845`, `:1823`, `:949`, `:1733–1745`; `GameUi.Overlay.cs:69–96`, `:129–143`; `GameUi.Placement.cs:184–186`, `:208` | The rail stands on the LEGEND tab; ~~the tray is a mode bar at the top of the map; one existing anchor changes (the overlay key's right limit)~~ *(superseded in verification round 2, U2-1, U2-2:)* the tray is the context column's first block during a window; two terms of the overlay key's anchor change; proposal §3, §14, E7. |

---

## 4. Evidence: MEASURED versus INFERRED

| Statement | Status |
|---|---|
| Prototype facts quoted in the proposal (kinds, codes, refusal reasons, emit sites, record fields, defaults) | MEASURED by reading the files at `1601c74` (file and line cited at each use) |
| `Sim.Ui` facts (layout rects, toast constants, annals, knowledge seam, test names) | MEASURED by reading (file and line cited) |
| Reference behaviour (Annex R, [CODE] rows) | MEASURED by reading the fetched copies (hashes in Annex P); that they match the shipped game is NOT verified |
| Reference criticisms and other games (Annex R, [SEARCH] and [KNOW] rows) | Search summaries and unverified knowledge; not measured |
| The design itself (surfaces, rules, order, expiry, contract) | PROPOSAL; its rationale is INFERRED |
| Performance bounds (proposal §12) | INFERRED structural bounds; nothing measured (DR48: "Measure first."; DR50: the earlier targets "are proposals, not approved targets"). *(Corrected in verification round 1, U-V8: "measure first" was attributed to DR50.)* |

---

## 5. Tests, mutants and pins

- **Tests:** none written or run. No code exists to test. The proposal's §13 is the test plan for the implementing
  packet: ~~nine leak tests with positive controls (L1–L9), a tie-dense ordering test and two order invariants (O1–O3),
  eight instant-exact expiry, stacking and delivery pins (E1–E8)~~ *(superseded in verification round 1, U-V7, U-V4:)*
  ten leak tests with positive controls (L1–L10), a tie-dense ordering test with reversed `LogSeq`/`SubjectId` pairs
  and two order invariants (O1–O3), ten instant-exact expiry, stacking, layout and delivery pins (E1–E10).
  *(Verification round 2: E2, E7, E8 and E10 extended, U2-1 to U2-7; still ten pins.)*
- **Mutants:** none run (no code). The proposal's §13.4 lists ~~eight~~ ~~*(verification round 1: twelve)*~~
  *(verification round 2: seventeen)* mutants for the implementing packet, each bounded at 10 × the clean filtered run
  and each tied to a semantic test.
- **Moved pins and goldens:** none. No code, data or golden was touched.

---

## 6. What ran, what was NOT RUN, and what is deferred

| Check | Result |
|---|---|
| Baseline builds and tests | **Not needed and NOT RUN:** this lane touches no solution (`M7Proto.slnx`, `M7Econ.slnx`, `M7DetMath.slnx` and `Sim.slnx` unchanged). No baseline counts were taken because nothing they cover changed. |
| `python3 scripts/check-doc-links.py --all` before the change (base `1601c74`) | **456 file(s) checked, 0 broken link(s)** (MEASURED, exit 0) |
| `python3 scripts/check-doc-links.py --all` after the change | **457 file(s) checked, 0 broken link(s)** (MEASURED, exit 0; the new proposal is the 457th) |
| `python3 scripts/check-doc-links.py prototypes/m7-sprint1/reports/H-U.md` (`--all` does not walk `prototypes/`) | **1 file(s) checked, 0 broken link(s)** (MEASURED, exit 0) |
| `python3 scripts/check-doc-links.py docs/m7-plan/notification-panel-proposal.md` (its links into `Sim.*`, `prototypes/` and `docs/` included) | **1 file(s) checked, 0 broken link(s)** (MEASURED, exit 0) |
| Determinism scan (`prototypes/m7-sprint1/scripts/check-proto-determinism.sh`) | **NOT RUN:** no code changed |
| Bench | **NOT RUN:** no code |

**Deferred** to the implementing packet (when authorized, after D-079 Q5, the knowledge layer, C7 in M7.8/M7.12 and
C19's observation rule in M7.14 *(corrected in verification round 1, U-V5: it said "the knowledge layer M7.8")*, and the
Q2 list): the
feed builder; the rail, tray and log painters; the Battle report card (an `InfoKind` in `Sim.Core/State/InfoQuery.cs`);
the published event-row log and its schema ADR; the §13 tests and mutants; the §12 measurements; the icons and their
provenance records.

---

## 7. Decisions for the Director (from the proposal's §15; each with a recommendation)

| # | Question | Recommendation |
|---|---|---|
| U-Q1 | Make the per-polity event log (proposal §10.1) simulation state, so the AI reads the same knowledge (Spine Law 7)? | Yes, through an ADR in the packet that builds the published log (C7 in M7.8/M7.12, or C19's observation rule in M7.14; *corrected in verification round 1, U-V5: it said "the knowledge packet (M7.8)"*); the UI-diff alternative cannot produce observer notices. |
| U-Q2 | Observer payload: outcome class, place and known combatants only; or also losses the observer saw; or authoritative casualties? | Outcome only in M7; observed strength when the knowledge layer publishes it (C7: M7.8, M7.12; *U-V5*); never authoritative casualties. |
| U-Q3 | An observed combatant without contact: "unidentified force", or drop the notice (the annals' P7 rule)? | Unidentified force. |
| U-Q4 | Tell observers when a battle begins in sight, or results only (WA3's words)? | Results only. |
| U-Q5 | Who outside the war learns of a leader's execution (N-303, and since verification round 1 N-310) and of a war's end (N-309)? Participants only is the strict reading of WA3; observers of the final capture widens it (off unless approved; *U-V4*). | Observers of the final capture (a rule for C19's observation rule, M7.14; *corrected in verification round 1, U-V5: it said "a knowledge rule for M7.8"*). |
| U-Q6 | May the player choose kinds for which the UI resumes at once at a window (presentation only)? | Yes, default off, never for a choice that lapses at the instant. |
| U-Q7 | Dispatch expiry: one End Turn, a number of simulation years, or manual only? | Survives one End Turn. |
| U-Q8 | Reading: text states an execution whatever the execution content setting (D-079 Q12 governs depiction). | Confirm. |
| U-Q9 | WA3-R's two readings (observers under fog; no war economics for observers). | Confirm. |
| U-Q10 | *(Added in verification round 1, U-V4.)* Tell an observer when a war first comes into its sight (N-601, with its log line N-602), or only of battle results (WA3's words)? | Results only, as for U-Q4; N-601 and N-602 stay in the catalogue, off. |
| U-Q11 | *(Added in verification round 2, U2-5.)* A city taken by the player's own immediate assault order at T leaves no instant at which to choose Occupy, Sack or Raze (no hit at T; lapsed by the next stop), while an AI taker on that path disposes by doctrine at T (MEASURED, §11.1). Accept it (a), let the assault order carry a disposition (b), raise a hit after an order's consequences (c, an ADR-061 change), or give the AI Occupy there too (d)? | (b), which treats the player as the AI's doctrine is treated and changes no world rule; until then (a), N-301's lapsed form. The ruling belongs to the war-outcome rules (DR37, ADR-061). |

Dependencies the design waits on (not questions): ADR-061's landing (D-079 Q5); the knowledge layer's observation rule
(C19, M7.14; *corrected in verification round 1, U-V5: it said "M7.8; C19"*); the significant list (Q2); lane W's
records with recipient-filtered views and a naval domain field; the Standoff ruling (P-TR1) and parity acceptance (Q4),
which change wording only; *(added in verification round 1, U-V12)* H-W DQ8 (whether post-battle orders count against
the two-action maximum), which changes the tray's wording; *(added in verification round 2)* a producer lapse row for
an order whose boundary never comes (U2-6; T2 writes none), and the ruling on U-Q11 (U2-5).

---

## 8. Limitations (honest list)

1. Nothing is implemented or tested; the proposal's test plan is a plan.
2. The observer rules are requirements on a knowledge layer that does not exist yet; the prototype siege, economy and
   outcome worlds have no per-recipient view (U-F1).
3. The reference research rests on a mod's copies of the scripts at the repository's `master` on 2026-11-09, not on a
   verified game release; the layout XML was not read, so the rail's exact anchor is [KNOW], not [CODE].
4. Forum and mod-hosting pages could not be opened (hosts not resolvable from this session); criticisms are search
   summaries, some about the successor title, and are indicative only. *(Reworded in verification round 1, U-V11: the
   names of games, the mod and the hosts now appear only in the proposal's Annex R and Annex P.)*
5. The comparison with other games is brief (two families) and rests on search summaries and unverified knowledge.
6. Performance numbers are structural bounds, not measurements; the reference machine was not used (DR48, DR50).
7. The tray cannot show a capture made by an order at T at that instant (U-F4); this is a property of the window
   mechanism (ADR-061), not changed here.
8. No icon was drawn and no player-facing copy was tested with players; the wording in the catalogue is illustrative.
9. The proposal's kind codes (N-101 to N-705) and `KindRank` values are design placeholders; ~~the significant priorities
   in `KindRank` follow T1's proposal and change with the Director's list.~~ *(Superseded in verification round 1, U-V2:
   `KindRank` is now one integer per kind, not the significance priority; the tray takes its order from the window,
   whose priorities change with the Director's list. The codes now run to N-706 and N-310.)*

---

## 9. Files

- Added `docs/m7-plan/notification-panel-proposal.md`.
- Added `prototypes/m7-sprint1/reports/H-U.md` (this report).
- *(Verification round 1)* Both files revised in place; §10 below appended.
- *(Verification round 2)* Both files revised in place; §11 below appended.

---

## 10. Verification round 1 (fix round, 2026-11-09)

Clone `/home/user/m7w/U`, branch `lane/U`, starting at `e38bbcc` (the verified commit). The verifier's tree,
`/home/user/m7w/verify-U-r1`, was at the same commit (MEASURED, `git rev-parse HEAD`; read only, not modified). Every
finding was first reproduced in this clone at `e38bbcc`, with the verifier's reproduce commands run here. The checks on
the reference action-panel script ran on the lane's own copy, whose SHA-256 (`34eaee01…5553a`) is the one the verifier
re-fetched. **All twelve findings reproduced; none was refuted.** Only reproduced findings were changed. Every change
is to the two documents (docs only): it is marked in place in the proposal with *(r1, U-Vn …)* and in this report with
"Corrected / Superseded / Added in verification round 1". Line numbers in the reproduction column are those of the
files at `e38bbcc`.

### 10.1 Per-finding verdicts

| Finding | Verdict | Reproduction (measured on `e38bbcc`) | Fix | Test |
|---|---|---|---|---|
| U-V1 (major) both new surfaces placed on existing on-map controls; the "free corner" reason false | **REPRODUCED** | `GameUi.cs:1212` draws the LEGEND tab "over the map's bottom-right corner" (`LegendTab`, `:1702–1709`, at `map.Right − margin − w, map.Bottom − margin − h`), with the legend above it (`:913`) and the compass rose at the window's bottom-right (`:1070`); `:1213` draws the OVERLAYS tab "over the map's bottom-left corner" (`GameUi.Overlay.cs:69–96`, right of the Age panel `:837–845` and the formation card `:1823`). `grep -i` of the proposal and this report for legend, overlay, compass, age panel, unit card, NOW USABLE: no match (exit 1). | Proposal §3: a table of every control floating over the map rect (MEASURED, file:line). The rail stands on the LEGEND tab (and on the legend while shown) and stops below the folded column's tab, the tray and the placement bar; the overlay key's right limit must stop short of the rail (the one existing anchor that changes). The tray becomes the window's mode bar at the top of the map, placed by the placement bar's rule, with its own Resume (no existing anchor changes). "Why here" rewritten, the false sentence quoted in place. §14 gains a row per control; §12's per-frame bound and Annex R.3's placement row follow; U-F10 added here. | Docs only. Test plan: E7 gains a layout row (no overlap with any control of §3's table, across window sizes and panel states) |
| U-V2 (minor) tray order and O2 contradict the canonical order; one hit can give several items | **REPRODUCED** | T1 R1 orders by class before priority (`G-time-events-t1.md:23`); significance is independent of class (ADR-061 `:189`); `KindRank` was one value per kind while §6.1 mapped N-301 to pri 0 and pri 3. Lane W schedules `SettlementTaken`, `LeaderFateChanged`, `BattleResolved` at priorities 0, 1, 2 with source = settlement, polity, battle (`WarOutcomeWorld.cs:708–709`, `:879`), lane S its significant kinds at 0 (`SiegeWorld.cs:600`, `:910`, `:1381`): kinds of one priority interleave by source. One `BattleResolved` hit per battle, the prisoner intake only a row (`:875–879`; no intake kind in `:452–454`): a captor's window has one hit and two items (N-102, N-306). | The tray has one entry per hit, in the window's key order, each entry holding its items in `(KindRank, LogSeq)` order (§0 rule 4, §8, §9 item 1, §10.2, §10.3). `KindRank` is one integer per kind and is no longer claimed equal to the significance priority. O2 rewritten with fixtures where the two orders differ and a two-item entry; a mutant added (the tray sorted by the canonical key). | Docs only. Test plan: O2 |
| U-V3 (minor) class labels contradict §4's rule; T1 rows missing | **REPRODUCED** | N-303 "I" and N-304 "D (if approved, T1 pri 4)" for the same kind, `LeaderFateChanged`, which is significant in lane W (`:452–454`, scheduled `:709`); T1 pri 4 is a polity's own ruler (`G-time-events-t1.md:82`). N-407 and N-410 "D-candidate" while citing T1 pri 7 (`:85`). `grep 'N-70[1-5]'` finds only the §6.1 table. | §4 note: prototype windows are not per recipient, so rows name the proposed recipients. N-303: D (if approved) for taker and captor, I for others. N-304: the captive's polity D (T1 pri 4), the captor D (lane W's significance). N-407 split: its deaths form D (if approved, T1 pri 7), its risk form D-candidate. N-410: D (if approved, T1 pri 7). New §6.9 rows N-701 to N-706 (N-701 no longer covers both pri 4 and pri 5; N-706 added). | Docs only |
| U-V4 (minor) "observers see battle results only" contradicted by N-601, N-303, N-309; N-601 put to no question | **REPRODUCED** | Proposal `:47`, `:194`, `:252–253`, `:339`, `:345`, `:691`; WA3 grants "battle results" (`d080:17`). | §0 rule 3 and F2: the non-result observer notices are widenings of WA3, **off unless the Director approves them**; the N-601, N-602, N-303 (seen by others) and N-309 rows say so; U-Q5 states the widening; new U-Q10 for N-601/N-602 (recommendation: results only); new leak test L10 and its mutant. | Docs only. Test plan: L10 |
| U-V5 (minor) C19 attributed to M7.8 | **REPRODUCED** | Plan revision `:107` (M7.8 is detection), `:484` (C7: M7.8, M7.12), `:567`, `:569` (C19, with the observation rule: M7.14). Proposal lines 19, 80, 197, 514, 678, 688, 689, 692; this report lines 57, 86, 134, 145, 146, 149, 156. | Every attribution corrected: C7 in M7.8 and M7.12; C19's observation rule in M7.14. | Docs only |
| U-V6 (minor) the captive leader's execution (`ExecutedAsCaptive`) missing | **REPRODUCED** | `OutcomeTypes.cs:105–106`; `WarOutcomeWorld.cs:1035–1037`; `ExecuteLeader` writes the fate row and schedules no event (`:713–723`); H-W `:126`, `:167–170`; the proposal's only execution row was N-303 (`LastCityLost`). | New N-310 row (recipients, class, icon, hover, click, expiry). Being an order, it raises no window hit at its instant (§9 item 10), so the leader's polity gets it as a dispatch at its next stop until a producer raises a hit. §6.1 pri 4 and U-Q5 updated. | Docs only |
| U-V7 (minor) no instant-exact pins for a dispatch's appearance and for the order note; the `SubjectId` mutant not sure to die | **REPRODUCED** | §13.3 pinned appearance only for decision items (E2); O3 is replay equality (`CLAUDE.md:50`; ADR-061 §3.7, `:222`). The producer appends rows in scheduler key order and lane W's source id is the subject id, so `LogSeq` follows `SubjectId` within a kind and a sort without `SubjectId` passes O1. | O1 requires hand-built pairs with `LogSeq` order reversed against `SubjectId`. New E9: a dispatch is absent at `KnownAtMs − 1` and at `EventAtMs`, present at `KnownAtMs`, and shows both dates. New E10: a window order at T, and pulse and turn orders issued at B − 1, B, B + 1 and S − 1, S, S + 1; for the pulse and turn orders the instant the note states equals the `LatchRecord`'s `EffectiveMs`, and the window order is delivered at T. Two mutants added. **Found while fixing:** N-603's source was the `LatchRecord` alone, which does not exist at T for an order that waits for a boundary (`WarTheaterWorld.cs:446–464`, `:468–482`; latched at `:343`, `:362`); the row now derives the instant by T2 §6's rule and the record confirms it. | Docs only. Test plan: O1, E9, E10 |
| U-V8 (nit) a DR48 sentence attributed to DR50 | **REPRODUCED** | `grep -n 'Proposed targets in earlier'` finds it only at `d079:108`, the DR48 row. | Proposal §12 attributes it to DR48 and quotes DR50's own words; §4 of this report likewise. | Docs only |
| U-V9 (nit) Annex R row R-9 misreads the action-panel script | **REPRODUCED** | On the lane's copy: `:25` `MAX_BLOCKER_BUTTONS` = 4 includes the big button (`:139` starts the count at 1; the small controls run 2 to 4, `:225`, `:261`); `:89` STACKED_UNITS and `:103` SPY_CHOOSE_DRAGNET_PRIORITY were left out; `:512` also requires no city ranged attack, plus the mod's own encampment clause. | R-9 rewritten: at most 3 small buttons; the two blocker types added; the automatic end of turn's conditions completed, with the mod's clause attributed to the mod. | Docs only |
| U-V10 (nit) the HitAndRun refusal text states the rule wrongly | **REPRODUCED** | T3 AE6 (`G-action-windows-t3.md:73`); `ActionWindows.cs:73` (cooldown 1), `:251` (`_serial[agent] <= d.Serial + 1`): barred in the retreat's window and the next. | The text now reads "Cannot attack a force you withdrew from, in this window or your next one, unless it attacks you or closes on you first". | Docs only |
| U-V11 (nit) two originality statements inaccurate | **REPRODUCED** | At `e38bbcc` this report named the mod, three other games, two hosts and a mod-hosting service (`:67`, `:69`, `:70`, `:168`) while `:73–75` said names appeared only in the proposal's annexes. Annex R (`:702–703`) banned any "layout position", which D-069 R1–R4 do not, and §3 contradicted it. | The names are removed from this report (they stay in the proposal's Annex P and Annex R, in `docs/`, as D-069 R1 asks); the statement is re-measured (§2). Annex R's sentence restated to D-069's rule. | Docs only |
| U-V12 (nit) N-101's Resume default unstated; the tray's wording ignores DQ8 | **REPRODUCED** | `ResolverPolicy.cs:41–45` (any player side: TacticalResolver); proposal `:44–46`, `:291`, `:479–484`; H-W DQ8 (`:373`); `grep -c DQ8` on the proposal: 0. | §0 rule 2, C3, the N-101 row and §9 item 3: Resume at N-101 hands the battle to the TacticalResolver (D-079 DR8 as applied by §1.5 R3; lane P's default). §9 item 2 tells agent actions from post-battle orders (DQ8). E8 extended; DQ8 added to the dependencies here and in the proposal. | Docs only. Test plan: E8 |

### 10.2 What ran, what was NOT RUN

| Check | Result |
|---|---|
| The twelve reproductions | **Ran** in this clone at `e38bbcc`; results in §10.1 |
| `python3 scripts/check-doc-links.py --all` before the round (at `e38bbcc`, in a temporary worktree of this clone, removed afterwards) | **457 file(s) checked, 0 broken link(s)**, exit 0 (MEASURED) |
| `python3 scripts/check-doc-links.py --all` after the round | **457 file(s) checked, 0 broken link(s)**, exit 0 (MEASURED) |
| `check-doc-links.py` on this report (`--all` does not walk `prototypes/`), before and after | **1 file(s) checked, 0 broken link(s)** both times (MEASURED; after: exit 0) |
| `check-doc-links.py` on the proposal, before and after | **1 file(s) checked, 0 broken link(s)** both times (MEASURED; after: exit 0) |
| Determinism scan (`prototypes/m7-sprint1/scripts/check-proto-determinism.sh`) | **Ran:** "scanned 92 files under Proto.M7/; pattern groups with findings: 0 … CLEAN", exit 0 (MEASURED). No code changed, so this shows only that the tree is unchanged in that respect |
| Paths changed since the base | `git diff --name-only 1601c74` lists only `docs/m7-plan/notification-panel-proposal.md` and `prototypes/m7-sprint1/reports/H-U.md` (MEASURED) |
| Builds and full suites of `M7Proto.slnx`, `M7Econ.slnx`, `M7DetMath.slnx` and `Sim.slnx`, Debug and Release | **NOT RUN:** no solution is touched (row above), so no build or test result can change. No counts are claimed |
| Mutants | **NOT RUN:** no code fix in this round. The proposal's §13.4 mutants belong to the implementing packet |
| Bench | **NOT RUN:** no code |

Deferred, unchanged from §6: everything the implementing packet would build and test, including the new E7 layout row,
E9, E10, L10 and the four mutants added in this round.

### 10.3 Limitations of this round

1. The new layout (proposal §3) is reasoned from the anchor formulas in the code, not run: no frame was drawn and no rect
   was measured. E7's layout row is how the implementing packet would check it. The 156 px figure is INFERRED at UI
   scale 1. *(Superseded in verification round 2: the anchors were then measured through the real UI, and the round-1
   tray stood over the Age panel, U2-1; §11.)*
2. Moving the tray to the top of the map is a design change made under the WA3-D delegation in answer to U-V1. It is a
   PROPOSAL like the rest of the document. *(Superseded in verification round 2, U2-1: the tray is now the context
   column's first block during a window; §11.)*
3. N-310's decision item for the leader's polity needs a producer that raises a hit for an order's consequence. No
   prototype does (proposal §9 item 10).
4. The §6.9 rows for non-war kinds are placeholders for the milestones that own those kinds.
5. No test, mutant or build exists for a docs-only lane. The new tests and mutants are plans for the implementing
   packet.

### 10.4 Files

- `docs/m7-plan/notification-panel-proposal.md`: revised in place, 887 lines (MEASURED, `wc -l`; 760 at `e38bbcc`).
  *(At `9df5ffb`; for round 2 see §11.5.)*
- `prototypes/m7-sprint1/reports/H-U.md`: this report, revised in place, with this section appended.
- Nothing else. The research copies stay in the lane's scratch area, unchanged; the verifier's tree was only read.

---

## 11. Verification round 2 (fix round, 2026-11-09)

Clone `/home/user/m7w/U`, branch `lane/U`, starting at `9df5ffb` (the verified commit; `git diff --name-only
1601c74..9df5ffb` lists only this lane's two documents, so `Sim.Ui` and the prototypes are those of `1601c74`). Every
finding was first reproduced in this lane's own tree: by reading the cited code and reports at `9df5ffb`, and by
running scratch probes in a temporary worktree of this clone at `9df5ffb` (`git worktree add --detach`, in the
session's scratch area). The probe files were written for this round, never committed, and removed with the worktree.
The verifier's tree (`/home/user/m7w/verify-U-r2`) and its probe were only read; its probe was not run. Its numbers were
re-measured with this lane's own probe (`FixUR2LayoutProbe`), which covers the verifier's states and adds the folded
column, the legend's rect and the key without the Age panel. Two more scratch probes ran on lane W's and T2's prototype
rigs (`FixUR2OrderCaptureProbe`, `FixUR2T2LapseProbe`). **All seven findings reproduced; none was refuted.** In two of
them a sub-claim needed correcting (U2-2, U2-7). Only reproduced findings were changed, and every change is to this
lane's two documents (docs only). The proposal marks each change in place with *(r2, U2-n …)*, quoting what it
replaces; this report marks its own as "verification round 2".

### 11.1 Per-finding verdicts

| Finding | Verdict | Reproduction (measured on `9df5ffb`) | Fix | Test plan |
|---|---|---|---|---|
| U2-1 (major): the tray, placed by the placement bar's rule, stands over the Age panel; §3's "no new surface overlaps" and §14's "nothing new is placed in the left column" are false | **REPRODUCED** | Probe through the real UI (`UiFrameHarness`, seed 42, UI scale 1), POLICY open, Age panel open. Selection card x 12–332, y 64–218. Age panel x 12–492, y 230–568 (1080 × 640) and 230–728 (1280 × 800). A tray placed by the placement bar's rule spans x 344–636 / 344–836 from y 64: a 148 px x-overlap. It reaches the Age panel's top when taller than 166 px, or 68 px with the selection card folded (card y 64–120, Age top 132); the placement bar's empty frame is 104.4 px tall (header 54.0, button row 30.0, spacing 8.4, margin 12). With the column folded the overlap is 148 px (1080 × 640) and 132 px (1280 × 800). Placement mode with the Age panel open: bar x 344–636, y 64–246 at 1080 × 640, so a tray one margin below it starts at y 258, 28 px inside the Age panel; bar x 344–836, y 64–226 at 1280 × 800, so the tray starts at y 238, 8 px inside. Reading: `GameUi.Placement.cs:184–186`, `:205–208`; `GameUi.cs:161`, `:837–845`; `PanelLayout.cs:110`. Every number in the finding matched. *Found while reproducing:* the overlay key's upper-half form covers the same top-right span (x 504–835.3, y 64–384 at 1280 × 800, Age panel open, controls unfolded). The Age panel is painted over the ImGui windows and takes the clicks in its rect (`GameUi.cs:941–944`, `:859`, `:869–873`) | The tray moves off the map. While a window is open it is the first block of the context column; the column opens or unfolds for the window and returns to its prior state at Resume, and its section or DETAILS card starts below the tray. Proposal §3 rewritten: a measured list of collisions among existing controls; the rule that new surfaces yield; the rail's placement (one margin clear of every existing control in its column, the lowest free stretch that holds an icon), overflow and no-room rules; the measured reasons for the tray's place. *Found while fixing* (INFERRED from the anchors, `GameUi.cs:419–434`, `:837–845`): at a UI scale of 1.25 on the 1080 × 640 window with a section open, the Age panel ends at x 501 and the map at x 540, so the rail's column meets the left column; the placement rule lists it. §0, the sketch, §7.2, §12, §14 (a new context-column row; the left-column row now holds) and Annex R.3 follow. Each superseded sentence is quoted in place | E7 rewritten: (a) the tray inside the column, and the column's prior state (closed, folded, a section, the DETAILS card) restored after Resume; (b) the rail never over an existing control in the listed states, and absent where it has no room; (d) only pairs that include a new surface asserted. Mutant: the rail standing on the LEGEND tab while the legend is shown |
| U2-2 (minor): the overlay key's second branch (`:138`) ignores the limit at `:134`, so "the one change … that the rail needs" is false | **REPRODUCED** (core); one sub-claim overstated | Reading `GameUi.Overlay.cs:129–145`. Beside the tab (`:137`) the width is bounded by `limit` (`:134`); above it (`:138`) by `map.Right − margin − tab.X`; the upper-half form takes that x-range (`:142–144`). MEASURED at 1280 × 800, Age panel open, every layer, legend shown: key x 504–835.3, at y 64–384 (controls unfolded) and y 408–668 (folded); the map's right margin is at 836. Cutting `:134` lowers `room` (`:135`), so `:138` is taken more often (INFERRED from the formula). The sub-claim "always the case with the legend shown" does not hold. At 1280 × 800 with a section open, no Age panel, one layer and the legend shown, the key took the branch beside the tab (MEASURED x 166–479.3, y 408–728). At 1080 × 640 in the same state it stood above the tab, but the 400 px cap ended it at x 404.5, short of the map's right side | Proposal §3 and §14: while the rail is drawn, two terms of one anchor stop short of its column: the limit beside the tab, and the width bound above it (which also bounds the upper-half form). The narrower key is quantified: 331.3 → about 271 px at 1280 × 800; 132 → about 72 px at 1080 × 640 (INFERRED; that state is already over-full, U2-3). The false sentences are quoted in place | E7 (c): the key stops short of the rail in both forms. Mutant: the width bound above the tab left at the map's right margin |
| U2-3 (minor): a collision that predates this lane in `Sim.Ui` at 1080 × 640; §3 does not name it; E7's premise does not cover it | **REPRODUCED** (pre-existing; lane U changes no `Sim.Ui` file) | MEASURED at 1080 × 640, POLICY open, Age panel open. OVERLAYS tab x 504–646, y 514–568; LEGEND tab x 510–636, same rows; the map's right margin is at 636, so the tab runs 10 px past it. A click on the OVERLAYS control: overlays open False → False, legend shown False → True. At 1280 × 800 the click gives False → True, with the tabs at x 504–646 and 710–836. Reading: `GameUi.Overlay.cs:75–77` (the tab is placed right of the Age panel); `GameUi.cs:1702–1709`. `grep -n -i` of `docs/queue.md` for "overlays tab", "legend tab", "age panel" and "placement bar" matches one line, `:1476`, the overlay key's height with six layers (fixed 2026-10-11), not these collisions. *Found while reproducing* (MEASURED): the key's form above its tab lies over the legend whenever both show, because `:138` ignores the legend's reach: 328.3 × 260 px at 1280 × 800 (Age panel open, every layer, controls folded), 328.3 × 18.1 px unfolded, 97.5 × 180 px at 1080 × 640 without the Age panel | Proposal §3 names three collisions among existing controls: the two in the finding and the key over the legend. It states that no rule depends on existing controls being apart, and E7 asserts only pairs that include a new surface. The queue line is proposed in §11.3 and was not appended to `docs/queue.md`, because the brief confines this lane to its two files | E7 (d) |
| U2-4 (minor): N-401 and N-201 labels contradict §4 | **REPRODUCED** | H-E §9 (`H-E.md:363–364`) lists `ArrearsBegan`, and the proposal (`:211` at `9df5ffb`) defines D-candidate, yet N-401 was "I, ongoing (H-E §9 lists ArrearsBegan as a Q2 candidate)". T1 pri 0 is "Own city captured, or own last city threatened" (`G-time-events-t1.md:78`) and §6.1 row 0 maps N-201 to it, yet N-201's defender was "D-candidate". No lane report proposes a siege's start for Q2: H-S has no candidate list, and its significant kinds are `SiegeEnded`, `AssaultResolved`, `Revolt` and the test device `Probe` (`SiegeTypes.cs:17–24`) | N-401: D-candidate, default I, ongoing. N-201 defender: D (if approved, T1 pri 0) for its last city, and I, dispatch for any other place; expiry "W, then E" / "E". §4's sentence names the round-2 corrections. A scan of every other class cell against §4 found no further break | Docs only |
| U2-5 (minor): N-301's taker form assumes a window hit that an order-made capture never raises | **REPRODUCED** (MEASURED) | Reading: `SiegeWorld.cs:1544`, `:1571` (immediate assault with no scheduler), `:1380–1382`; `WarOutcomeWorld.cs:657–665`, `:698–710`, `:756`. Probe on lane W's rig (`OutcomeRig.BuildBound`, with a resolver that always storms). An assault ordered at a siege pulse start captures at T. The only window hit at T is the probe's (kind 69); there is no 83 `SettlementTaken`, 84 `LeaderFateChanged`, 67 or 68. A Sack ordered at the next stop (T + 1 day) is refused with reason 3 (`WindowClosed`), and the city stays occupied (disposition 0, no loot). Same path with an AI taker on a Sack doctrine: sacked at T (disposition 1; loot: money 313,612, grain 2,069,848). Control: an assault executed by a world event (ordered mid-pulse) raises hits 68, 67, 83 and 84 at the capture instant, and a Sack there is accepted. Also MEASURED: a Sack issued in the same window as the immediate assault order, before any capture, is accepted when the assault takes the city (reason 0) | N-301 gains a lapsed form: I, dispatch, with its own text; a click opens the settlement card, never the disposition panel; expiry E; a same-window disposition applies instead. Proposal §9 item 10 extended with the measurements. New U-Q11 with options a–d. Recommendation (b): the assault order carries a standing disposition, as the AI's doctrine does, with (a) as the notice until then. A dependency is added | E8 extended: no tray entry at T; the lapsed form at the next stop; the world-event case as a control. Mutant: a lapsed N-301 that opens the disposition panel |
| U2-6 (minor): N-603 and E10 have no case for an order that never takes effect | **REPRODUCED** (MEASURED) | Reading `WarTheaterWorld.cs:446–465`, `:468–485`, `:367–368`, `:380–385`. Probe on T2's rig (the shipped calendar, the proposed pulse lengths). A supply-rate order issued 1 ms or 10 days into a war's last pulse: the war ends with no `LatchRecord`, no refusal, and the rate unchanged (360). At offset 0 it latches at once (control). An upkeep order issued 1 ms or 30 days into the campaign's last turn (turn 1629, 180 days): no latch, no refusal, the rate unchanged. At offset 0 it latches at once | N-603 gains the no-boundary case and its notes: in the last turn a note that the order cannot take effect; for a pulse order a lapse note at the war's end, built from a producer lapse row. §10.1 requires the lapse row (T2 writes none). A §15 dependency is added | E10 extended: no instant stated; the lapse note absent at end − 1 ms, present at the end and at end + 1 ms. Mutant: a note that names a next pulse start after the war's last pulse |
| U2-7 (minor): decision items after Resume: the "W, then E" rows contradict §8 and §9 item 3 | **REPRODUCED**; one count corrected | `grep -c 'W, then E\|W, then L'` gives 19 lines here (18 "W, then E", 1 "W, then L"), not 22. Counting also "W, then C" (2), "W or E" (1) and N-310's "W at the next stop" (1), 23 catalogue rows carry a code built on W. §8's decision-item row said "Becomes a log line" and §9 item 3 said "Decision items become log lines". The legend did not define "W, then X", and E2 pinned only "gone after Resume" | The catalogue's per-kind codes stand. The legend now defines "W, then X". §8's row and §9 item 3 say that at Resume an item takes form X (a dispatch until `S(TurnOf(T) + 2)`, its log line only, or an ongoing notice), with one log line written at T. N-310's and N-201's codes are aligned | E2 extended: N-102 is on the rail after Resume until `S(TurnOf(T) + 2)`; N-101 is on neither surface; N-306 is an ongoing notice; one log line each. Mutant: a "W, then E" item dropped at Resume |

### 11.2 What ran, what was NOT RUN

| Check | Result |
|---|---|
| The seven reproductions | **Ran** at `9df5ffb` (reading and probes); results in §11.1 |
| Probe build: `Sim.slnx`, Debug, in the temporary worktree (`nice -n 19`, `-m:1`, `-nodeReuse:false`, `DOTNET_CLI_USE_MSBUILD_SERVER=0`) | **Build succeeded, 0 warnings, 0 errors** (MEASURED). It was built only to run the probe; none of the solution's suites was run |
| `FixUR2LayoutProbe` (`dotnet test Sim.slnx --no-build -c Debug --filter FullyQualifiedName~FixUR2LayoutProbe`) | **4 probe cases ran and passed** (two window sizes × two rigs). Their output is the U2-1 to U2-3 measurements. It measures; it does not test the design |
| Probe build: `M7Proto.slnx`, Debug, in the temporary worktree | **Build succeeded, 0 warnings, 0 errors** (MEASURED) |
| `FixUR2OrderCaptureProbe` and `FixUR2T2LapseProbe` (`--filter FullyQualifiedName~FixUR2`) | **10 probe cases ran and passed**. Their output is the U2-5 and U2-6 measurements |
| `python3 scripts/check-doc-links.py --all`, before the round (at `9df5ffb`, in the worktree) | **457 file(s) checked, 0 broken link(s)**, exit 0 (MEASURED) |
| `python3 scripts/check-doc-links.py --all`, after the round | **457 file(s) checked, 0 broken link(s)**, exit 0 (MEASURED) |
| `check-doc-links.py` on this report and on the proposal, each before and after | **1 file(s) checked, 0 broken link(s)**, exit 0, all four runs (MEASURED) |
| Determinism scan (`prototypes/m7-sprint1/scripts/check-proto-determinism.sh`) on this clone | **Ran:** "scanned 92 files under Proto.M7/; pattern groups with findings: 0 … CLEAN", exit 0 (MEASURED). No prototype code changed, so this only shows that the tree is unchanged in that respect |
| Paths changed since the base | `git diff --name-only 1601c74` lists only `docs/m7-plan/notification-panel-proposal.md` and `prototypes/m7-sprint1/reports/H-U.md` (MEASURED) |
| Builds and full suites of `M7Proto.slnx`, `M7Econ.slnx`, `M7DetMath.slnx` and `Sim.slnx`, Debug and Release | **NOT RUN:** no commit of this lane touches a solution (row above), so no build or test result can change. No counts are claimed. The two probe builds are not suite runs |
| Release builds of the probes | **NOT RUN:** the probes are scratch measurements, and Debug was used |
| Mutants | **NOT RUN:** no code was fixed in this round (docs only). The proposal's §13.4 mutants, seventeen now, belong to the implementing packet |
| Bench | **NOT RUN:** no code |
| The verifier's probe | **Read, not run**; its states were re-measured with this lane's own probe |

Cleanup: the probe worktree, its build output and the three probe files were removed with `git worktree remove --force`
on the exact path, followed by `git worktree prune`. `git worktree list` then showed only the clone. Copies of the three
probe sources (SHA-256 `e085bd8b…b251` layout, `efa055dd…aa88` order capture, `64108abe…af5e` T2 lapse) and their output
logs are kept in the session's scratch area (`scratchpad/U-r2-fix-probes/`), not in the repository.

### 11.3 Proposed queue line (U2-3; not appended to `docs/queue.md`, for the orchestrator or the Director to route)

> - 2026-11-09 (M7 lane U verification round 2; MEASURED through `UiFrameHarness` at `9df5ffb`, whose `Sim.Ui` is that
>   of `1601c74`; FOUND, NOT FIXED): three collisions among existing map controls. (1) At the 1080 × 640 window with a
>   section and the Age panel open, the OVERLAYS tab (x 504–646, y 514–568) lies over the LEGEND tab (x 510–636) and
>   runs 10 px past the map's right margin; a click on the OVERLAYS control toggles the legend instead, so the overlay
>   controls cannot be unfolded by click there (at 1280 × 800 the same click unfolds them). (2) In placement mode at
>   1080 × 640 with the Age panel open, the placement bar (y 64–246) runs 16 px into the Age panel (top y 230) over
>   148 px. (3) The overlay key's form above its tab ignores the legend's reach and lies over the legend whenever both
>   show (1280 × 800, Age panel open, every layer, controls folded: 328.3 × 260 px; 1080 × 640, no Age panel, one layer:
>   97.5 × 180 px). Anchors: `GameUi.Overlay.cs:75–77`, `:136–138`; `GameUi.cs:1702–1709`, `:837–845`;
>   `GameUi.Placement.cs:184–186`. Presentation defects for the UI owner. Record:
>   `prototypes/m7-sprint1/reports/H-U.md` §11.

### 11.4 Limitations of this round

1. Moving the tray into the context column is a design change made under the WA3-D delegation in answer to U2-1. It is
   a PROPOSAL. Its cost is INFERRED and has not been tried with players: a window that opens while the column is closed
   narrows the map until Resume.
2. The rail's no-room example combines two rects measured in two sessions: the legend in a seed-42 session, the
   placement bar in the founding rig. It is INFERRED. The 48 px rail width is a placeholder. The rail's meeting with the
   left column at a UI scale of 1.25 is INFERRED from the anchors; no frame was measured at a UI scale other than 1.
3. The narrower key (U2-2's fix) is INFERRED from `:138` and was not drawn. At 1080 × 640 with the Age panel open it
   would be about 72 px wide, in a state that is already broken (U2-3).
4. U2-5's measurements use lane W's rig with a resolver that always storms. The human/AI difference and the
   same-window Sack are properties of the prototypes at `1601c74`, not of an integrated game. U-Q11's recommendation is
   a PROPOSAL for the owner of the war-outcome rules.
5. U2-6's lapse row is a requirement on a producer that does not exist. Until it does, a pulse order's note can only
   say that the order waits.
6. Nothing is implemented or tested. The additions to E2, E7, E8 and E10 and the five new mutants are plans for the
   implementing packet.

### 11.5 Files

- `docs/m7-plan/notification-panel-proposal.md`: revised in place, 1,002 lines (MEASURED, `wc -l`; 887 at
  `9df5ffb`).
- `prototypes/m7-sprint1/reports/H-U.md`: this report, revised in place, with this section appended.
- Nothing else. The probe worktree and the probe files were scratch and are gone. The verifier's tree was only read.
