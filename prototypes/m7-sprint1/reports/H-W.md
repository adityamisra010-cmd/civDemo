# H-W — M7 Phase C lane W: war outcomes

Leader execution, post-battle choices, war records and the post-battle collapse, in `Proto.M7/War/Outcomes/`
(`WarOutcomeWorld.cs`, `OutcomeTypes.cs`, `OutcomeRecords.cs`, `BattleCollapse.cs`, `OutcomePresentation.cs`), with tests in
`Proto.M7.Tests/Outcomes/` and a bench mode `outcomes` in `Proto.M7.Bench/OutcomeBench.cs`.

**Provenance.** No clone, bundle or patch of lane W existed when this run started (the resume note's `/dev/shm/m7c/W` was absent;
`scratchpad/m7c/bundles/W-wip.bundle`, `W.bundle` and `wip/m7c-W.patch` were absent). Following the orchestrator's note (B), the clone
was created fresh from dev HEAD `0041f68` (`git clone --shared /home/user/m5w/dev /dev/shm/m7c/W`, branch `lane/W`). The four
`scratchpad/m7c/W-logs/*.txt` files dated 2026-11-05 belong to an earlier base (369 tests) and were left untouched. This run's logs are
in `/dev/shm/m7c/W-logs-r2/`. Nothing under `Sim.*` changed (D-079 Q5 is open).

**Status words.** MEASURED = run in this clone and shown in a log or test. PROPOSAL = my design for Director review (DIR5 §4.2
delegates the framework; it is not ratified). PLACEHOLDER = a coefficient with no source. OPEN = a question this lane does not decide.

---

## 0. What now exists (measured)

| Item | Where | Evidence |
|---|---|---|
| Leader-execution transition (DIR5 §4.1, C31): executed at the exact instant the last city is lost; simultaneous losses, captures vs razes, same-instant and later re-captures pinned; fate as a Ledger flow of the leader's person; war record and fate agree | `WarOutcomeWorld.Settle`, `ExecuteLeader` | `LeaderExecutionTests` (16 methods, 20 cases) |
| Real captures from lane S: a siege's capitulation or storm IS the capture; the garrison lane S took prisoner enters custody; immediate assault orders settle when the order completes | `WarOutcomeWorld.ObserveSiege` (wraps `SiegeWorld`, shares its ledger; lane S unchanged) | `OutcomeSiegeIntegrationTests` (4) |
| Post-battle choices (PROPOSAL): occupy / sack / raze with Ledger flows; prisoners release / conscript / enslave (institution-gated) / ransom / exchange; captive-leader release / ransom / execute (Director-level flag) | `Dispose`, `Plunder`, `Order*` | `DispositionTests` (9 methods, 20 cases), `PrisonerAndLeaderDecisionTests` (16 methods, 31 cases) |
| War records: wars, participants, battles, sides, casualties by side and cause, leader fates, settlement outcomes, prisoner movements + 3 state tables; canonical fixed-length encoding | `OutcomeRecords.cs` | `OutcomeCodecTests` (POPULATED round trip for all 11 row types) |
| Post-battle collapse: battle entities → formation counts; dead, wounded, captured, deserted as explicit flows; no veteran table | `BattleCollapse.cs`, `WarOutcomeWorld.OnBattleEnded` | `CollapseTests` (10 methods, 15 cases) on real lane TR tactical battles and lane N's amphibious combined-arms battle |
| Execution content setting (Q12 OPEN): presentation-only, no default | `OutcomePresentation.cs` | `OutcomeCinematicsTests` (5) |
| Replay equality, conservation at every instant, input-order independence | — | `ReplayOfTheOrderLog_…`, `EveryInstantConserves…`, `InputOrder_DoesNotChangeTheWorld` (3 permutations), bound-world replay; `OutcomeWorldSetupTests` (5) |

Lane W adds **105 tests**; the M7Proto suite goes from 992 to 1,097 (Release and Debug, all passing; §9). Verification round 1 (§13)
adds 27 more: 1,124, all passing in Release and Debug. Verification round 2 (§14) adds 27 more test cases: 1,151, all passing in
Release and Debug.

---

## 1. Inputs read (grepped, not read whole)

- DIR5 §4.1 "If an empire loses all its cities, its leader is executed. This is an authoritative simulation outcome, not merely a
  cinematic choice. The relevant state transition, war record, leader fate and presentation must agree." §4.2 (Troy-inspired choices,
  framework DELEGATED, "Do not present choices that are inconsistent with the actual battle outcome"), §4.3 (M7 owns wars, battles,
  participants, outcomes, casualties, leader fates; M8 owns diplomacy), §2.9 (collapse to formation counts, no veteran table,
  attrition is M7.14, Ledger coherent), §5.3 (execution shown only when the fate is executed and the content is enabled).
- D-079 DR36–DR38, DR41; Q12 (the content setting's default: OPEN, the Director's); A-47 (the war-record schema ADR is owed when the
  packet is authorized — not written here).
- Plan revision 2026-11-05, contracts C31 (leader execution), C32 (post-battle framework), C33 (records), C35 (cinematics).
- On the polity after elimination: ADR-019 §7.3 ("A player with zero territories loses"); Sim.Core `EmpireQuery.IsExtinct`
  (extinction is a DERIVED emptiness, the row persists, a polity that regains control is no longer extinct); D-037 C6 (successor claims
  inherited, M8); D-048 ruling 5 via D-049 (a ruler's final settlement never revolts away). D-075 and D-076 contain no rule on leaders or
  elimination (grep: only "elimination" as a battle-end word, D-076 N10); ADR-060 none.

---

## 2. The leader-execution transition (C31)

### 2.1 The rule as implemented

"Cities" = the settlements a polity controls that are cities: lane S `InstallationKind.City`, or an `OutcomeSettlement` with `IsCity`;
a razed settlement is no city. *Corrected in verification round 1 (W-R1-7):* C31's READING is "the settlements the polity controls";
that forts, harbours, airports and depots do NOT count is this lane's own narrower reading, not C31's and not Sim.Core's — raised as
DQ7 (§12), not decided here. A polity whose city count was ≥ 1 at
the last settled instant and is 0 now has **lost all its cities**; at that instant its leader is executed:

- fate Reigning (or Captive) → **Executed**, final;
- the leader is a person in the Ledger (one person in the polity's leader account): death is a transfer to the executed sink;
- a `LeaderFateRow` with the instant, campaign turn, war and War Pulse of that war's clock, the polity that took the last city
  (`ByPolity`), the captor that held the leader if any (`HeldBy`) and the settlement;
- the polity leaves every war it is in (`ParticipantExit.Eliminated`); a war whose whole side has left ends in `Conquest` (both sides at
  one instant: `MutualCollapse`), with `EndMs` = that instant — so the war record and the leader fate agree by construction (tested).
  *Verification round 1:* a polity whose leader is already dead (executed as a captive under `CaptiveExecutionRule.Permitted`) is
  eliminated the same way when it loses its last city, with no second fate row (W-R1-1); the fate row names the war and pulse of the
  capture that caused it, the same as that capture's battle row (W-R1-5).

### 2.2 When exactly: the settlement of an instant (my design under C31's delegation)

All control changes made by **world events** at instant T (lane S's siege endings, the capture stand-in; all of class < Resolve) are
settled together by one `ControlSettle` event at (T, Resolve). This is the event classes' existing simultaneity rule (all shots, then
all damage, then deaths resolve) applied to control:

| Case | Result (pinned) |
|---|---|
| Last city lost at T | Executed at T; alive at T − 1 ms; pulse index = that of T (B − 1 → pulse n − 1, B and B + 1 → pulse n); turn of T (S − 1 → t − 1, S → t) |
| Two cities lost at the same T (to two victors) | one execution at T; attributed to the loss processed last in event-key order (key = settlement id, not scheduling order: permuted scheduling gives the same hash) |
| Two losses in one pulse at different instants | executed at the second instant only (alive at second − 1 ms) |
| Last city lost and another taken at the same T | the polity ends T with a city: no execution (teeth: the same loss alone executes at T) |
| Two polities swap their last cities at T | neither is executed; both seats fell, so both leaders are Captive of each other |
| Both sides of a war lose their last cities at one T | both executed; the war ends in MutualCollapse (side 0 alone eliminated: Conquest credited to side 1, and vice versa) |
| Loss at T1, re-capture at T2 > T1 in the same pulse | Executed at T1 stands (a fate never reverts); at T2 the polity is no longer extinct (derived) but leaderless; a later loss executes no one (no living leader) |
| Seat taken while another city remains | Reigning → Captive (held by the taker) at that instant; a later loss of the last city executes from captivity (`HeldBy` recorded) |
| Last city captured and razed at the post-battle window (order at T, or AI doctrine at T) | Executed at T in both, identical record hash: the loss is the capture; the raze is the victor's disposition of what it holds |
| A raze of the razer's own only city | refused (`LastCity`); an AI choosing it falls back to Occupy |
| A capture made by an ORDER at T (lane S's immediate assault) | settled when that order completes (orders run after every world event at T, sequentially by log index, ADR-061); the record is complete, but no fate-changed decision-window hit can be raised at T (the window at T has already closed) — tested |

Why per instant and not per War Pulse: a pulse-level check would leave a polity with no city alive and reigning for the rest of the
pulse (up to 21 days with T2's PROPOSED early-era lengths) and would make the outcome depend on the era's pulse length; DIR5 requires
the state transition, the record and the presentation to agree at all times.

**Captures vs razes.** You raze what you hold: in lane S control passes at the siege's end, and the victor's disposition comes after,
at the same instant. So the former owner always loses the city by capture at T, whatever the victor then does; the record names the
settlement and its disposition (`SettlementOutcomeRow`). A "raze without capture" path does not exist in the prototype (lane S raids
pillage food, they do not take or destroy), and I did not invent one.

### 2.3 What happens to the polity afterwards (existing rules only)

Extinction is derived, not stored, as in Sim.Core `EmpireQuery.IsExtinct`: the identity persists and a polity that regains a city is not
extinct. *Corrected in verification round 1 (W-R1-7):* `EmpireQuery.IsExtinct` is "controls no settlement"; `WarOutcomeWorld.IsExtinct`
is "controls no city" (the lane's reading, DQ7) — it does not mirror Sim.Core: a polity holding only a fort is extinct here. ADR-019 §7.3: a player with zero territories loses. D-037 C6: its claims are inherited by
successors, not deleted (M8). D-048 ruling 5: a final settlement never revolts away, so only war triggers the rule. M8 owns the
diplomatic consequences. **No succession rule exists** in the repository (D-048 lists capital succession as DEFERRED): a polity that
regains a city after the execution stays leaderless here — flagged in §12.

---

## 3. Post-battle choices — PROPOSAL (C32)

### 3.1 The choice set by context

| Context (authoritative outcome) | Choices offered | When | Who | Default |
|---|---|---|---|---|
| A settlement taken (lane S Stormed, Capitulated, GarrisonDestroyed, CapCapitulation; the capture stand-in) | Occupy, Sack, Raze (Sack and Raze not of one's own population: L13) | only at the capture instant T (its post-battle window) | only the taker | Occupy |
| Siege Relieved, Lifted, CapLifted; a raid; a field battle without a settlement | none for a settlement (the outcome is inconsistent with a disposition) | — | — | — |

*Verification round 1 (W-R1-9):* the seven lane S endings are now exercised end to end
(`SiegeEndings_EligibilityMatrix_OnlyAPassOfControlOffersADisposition`): Capitulated, Stormed, GarrisonDestroyed and CapCapitulation
accept the besieger's Sack at the ending instant; Relieved, Lifted and CapLifted refuse it (`NotEligible`, ledger unchanged).
| Men surrendered in a battle or a garrison taken | Hold, Release (parole), Conscript, Enslave, Ransom, Exchange | at any window from the intake on | only the captor | Hold |
| A captive leader (its seat was taken) | Hold, Release, Ransom, Execute (only under `CaptiveExecutionRule.Permitted`) | at any window from the captivity on | only the captor | Hold |
| Loss of all cities | none: the leader rule is not a choice (DIR5 §4.1) | — | — | — |

AI victors use the same predicates and consequences (`aiDisposition`, decided at the capture instant; a refused choice falls back to
Occupy; pinned by an identical record hash for a human and an AI raze). AI prisoner and leader doctrines are NOT built (AI holds).

### 3.2 Consequences — mechanisms and flows (every flow is a `WarLedger` transfer; totals exact)

**Sack** over `SackDays` = 3 (DERIVED: the customary three days, e.g. Rome 410, Constantinople 1453) at an intensity
q = min(sackers, civilians) / civilians (1 for an empty town with sackers present, 0 with no sackers):

| Flow | Mechanism (reused) | Coefficient |
|---|---|---|
| Movable wealth → victor's treasury; grain → victor's granary | lane E `StockDraw.Share(stock, ForageForcedPermilleYear × q, 3 d)` — lane E's forced plunder rate (90 % in 7 days) | lane E PROPOSAL 100,910 ‰/yr |
| Civilians killed → civilians-killed sink; fled → refugees of the population's polity (lane S's "elsewhere" when bound) | lane E `HeadcountLoss` competing flows (one survival product, split pro rata) | PLACEHOLDER kill 13,452, flee 26,904 ‰/yr (30 % in 3 days at q = 1, a third dead) |
| Buildings burnt → ruins sink (new `Commodity.Structures`) | `StockDraw.Share(buildings, SackBurn × q, 3 d)` | PLACEHOLDER 12,420 ‰/yr (≈ 10 % in 3 days) |
| Occupation grievance (the INPUT of lane S's occupation unrest) | base + HarmGrievance × (killed + fled) / civilians; 0 if the taker is the population's own polity (lane S's rule: no unrest under one's own) | PLACEHOLDER 4,000 ‰/yr per whole population harmed |

Measured on the rig city (10,000 people, 1,000,000 money, 5,000,000 gm grain, 2,000 buildings), 3-day sack: at q = ½ 529 killed and
1,059 fled, 364,282 money and 1,821,413 gm taken, 100 buildings burnt, grievance 1,000 → 1,635 ‰/yr; at q = 1, 1,000 killed and 2,000
fled (the calibration), 627,226 money taken. A second sack after a re-capture draws on what is left (pinned).

**Raze** = the sack's flows, then: the rest of the money to the victor, the rest of the grain burnt (sink), every building to the ruins,
every survivor expelled to the refugees; the settlement is no longer a city (standalone: no controller). *Verification round 1
(W-R1-8):* whether the raze kills is the Director-level `RazePopulationRule` (no default; every caller passes one): `Displace` kills no
one (everyone expelled), `KillThenDisplace` applies the sack's kill rate first (the lane's first proposal). Loot and burning are the same
under both. **Occupy**: no flows; the
population is occupied (lane S's base grievance applies).

**Prisoners.** Custody is per (captor, origin) account. In custody they eat lane E's base ration (`RationBase`, 1,000 gm per head-year)
from the captor's granary, integrated exactly (T1 `RateIntegrator.IntegrateExact`) and split at every campaign-turn start (T2's
sub-stepping); what the granary cannot pay is recorded as unpaid rations; their mortality is attrition (M7.14) and is not modelled.

| Action | Flow | Eligibility (state-derived institutions of the captor, Law 4) |
|---|---|---|
| Release (parole) | custody → origin's paroled account (home, not in the ranks) | always |
| Conscript (impress) | custody → captor's impressed account (kept apart: their desertion is attrition, M7.14) | not bound by `PrisonerConvention` |
| Enslave | custody → captor's enslaved account | holds `Slavery` and not bound by `PrisonerConvention` |
| Ransom | origin's treasury → captor's treasury (n × price); custody → origin's home troops | both hold `Coinage`; the origin's standing terms ≥ price; n = min(held, treasury / price) — partial if it cannot pay for all, `CannotPay` if for none |
| Exchange | one for one: k = min(both standing offers, both holdings) each way to the home troops | both have offered |

**Captive leader.** Release (back to the polity's lowest-id city), Ransom (the polity's standing leader terms ≥ price, paid from its
treasury), Execute (cause `ExecutedAsCaptive`, a Ledger flow; the polity is NOT eliminated — it still holds a city — and the war goes
on). Execute is behind `CaptiveExecutionRule`, which every caller must pass: C32 lists "whether a victor may execute a captured leader
where C31 does not apply" as a Director-level flag; I did not decide it.

### 3.3 Legal-combination and anti-exploit rules (implemented, pinned)

| # | Rule | Why | Test |
|---|---|---|---|
| L1 | A disposition only by the taker, only of a settlement it took at THIS instant | no choice inconsistent with the outcome (DIR5 §4.2) | `Dispose_EligibilityMatrix` (7 cases) |
| L2 | Offered only at the capture instant (T − 1: NotEligible; T: applied; T + 1: WindowClosed) | no deferred plunder of a held city ("occupy, tax, then sack later") | `Dispose_IsOfferedOnlyAtTheCaptureInstant` |
| L3 | One disposition per capture (a second is refused); Occupy if none | no double dipping | `Dispose_Once_…` |
| L4 | No raze of the razer's only city | it would lose all its cities by its own choice | `RazingTheRazersOwnLastCity_…` |
| L5 | A ruin cannot be taken | it no longer exists | `TheCaptureDevice_Refuses…` |
| L6 | Re-sacking needs a new capture (a new battle) and draws on what is left | capture–release–recapture farming yields a geometric series, not a fresh stock | `ARecapture_OpensANewChoice_…` |
| L7 | Prisoner actions only on prisoners held, counts ≤ holdings, only the captor | conservation; no phantom prisoners | `PrisonerActions_Refusals` |
| L8 | Enslave / conscript gated by the captor's institutions | state-derived (Law 4) | `PrisonerActions_AreGatedBy…` (6 cases) |
| L9 | Ransom needs the payer's standing terms (two parties; terms stand from their instant, a ransom before them in the same window is refused) | no involuntary payment | `Ransom_…`, `RansomTerms_StandFromTheirInstant` |
| L10 | Exchange needs both offers; one for one | no one-sided exchange | `Exchange_NeedsBothOffers_…` |
| L11 | Leader decisions only by the captor of a captive | — | `LeaderDecisions_OnlyTheCaptor_OnlyACaptive` |
| L12 | A collapse applies once per battle; one collapse input per campaign formation (fix r1, W-R1-2: a repeated campaign formation is refused at scheduling and in `FromTactical`); *fix r2 (W-R2-2):* and one battle at a time: a campaign formation already in another field battle whose span overlaps — (start, end] spans that share more than a boundary instant, or that end at the same instant — is refused at scheduling; a battle that starts at or after another's end is fought by what that one left | no double counting of casualties or of the start strength | guarded (`OnBattleEnded`); `OneCampaignFormationFieldedAsTwoBattleFormations_IsRefused_NotMisSplit`, `FromTactical_Refuses…`, `ACampaignFormation_FightsOneBattleAtATime_…` (start at end − 1 / end / end + 1), `TwoBattlesEndingAtOneInstant_…` |
| L13 | (fix r1, W-R1-6, PROPOSAL; *fix r2, W-R2-1:* for a lane-S-bound city "whose people" is lane S's, cross-checked at construction) No sack or raze of a settlement whose population is the taker's own: a re-capture is a liberation; Occupy only (`OwnPopulation`; an AI doctrine falls back to Occupy) | lane S has no unrest under one's own polity, so the harm cost the sacker nothing (on 157130e: 1,000 killed, 2,000 fled and 627,226 money looted at grievance 0, measured) | `OnesOwnPopulation_CannotBeSackedOrRazed_ARecaptureIsALiberation` |
| L14 | (fix r2, W-R2-3, PROPOSAL) A leader is never its own polity's captive: a seat its own polity razes (it can only raze what it took at this instant, e.g. the city its released leader was just seated in) was taken by no one; the leader moves to the polity's lowest-id city (the release rule), no fate row, no captor | a captive "held" by its own polity has no order that can free it (measured on 9c5f135: `HeldBy 0`, every release refused) | `ALeaderReleasedIntoACityItsOwnPolityThenRazes_…`, `AReleasedLeader_ReturnsToItsPolitysLowestIdCity` |

### 3.4 Delivery semantics (pinned with ±1 neighbours)

Outcome orders are one-shot transactions applied AT the window instant at which they are issued (T3's rule for agent actions); standing
terms and offers take effect at their instant and stay until replaced. Pins: disposition at T − 1 / T / T + 1; prisoner release at
intake − 1 / intake / intake + 1; ransom terms at T − 1 (usable at T) and T + 1 (not usable at T); leader release at captivity − 1 /
captivity / captivity + 1; (fix r1, W-R1-11b) an exchange takes effect at the instant the second standing offer is issued: second offer
at TA − 1 / TA / TA + 1 against the first at TA.

---

## 4. War records (C33) — canonical serialization

Fixed-length little-endian rows; table = [version u8][row count i32] rows (`OutcomeCodec`, lane S's pattern; version 2 since fix r1). The world's state hash is
built from these encodings. No row is keyed by a battle entity and none carries a treaty, alliance, relation, truce, tribute, vassal or
peace field (reflection tests). `WarOutcome.NegotiatedPeace` is a reserved code for M8 to write; M7 never writes it.

| Row | Bytes | Fields |
|---|---:|---|
| WarRow | 25 | war, start, end (−1 ongoing), outcome, winner side |
| WarParticipantRow | 26 | war, polity, side, joined, left (−1), exit |
| BattleRow | 51 | battle, war, kind (field/siege/storming), mechanism (tactical/auto/siege-health/stand-in), end code (lane TR `BattleEndReason` incl. the unratified Standoff, or lane S `SiegeOutcome`), start, end, turn, pulse, settlement, winner side. *Fix r2 (W-R2-9):* a lane S siege that ends without a capture (Relieved, Lifted, CapLifted) writes one too (winner side 1, the defender) |
| BattleSideRow | 21 | battle, side, polity, formations, personnel at start |
| CasualtyRow | 18 | battle, side (2 = civilians), polity, cause (killed, wounded, captured, deserted, marched out, civilians killed, civilians fled), count |
| LeaderFateRow | 55 | seq, polity, leader, from, to, cause, instant, turn, war, pulse, by, held by, settlement |
| SettlementOutcomeRow | 81 | settlement, battle, instant, from, to, disposition, loot money, loot grain, killed, fled, burnt, grain burnt, grievance |
| PrisonerRow | 41 | seq, instant, captor, origin, action, count, money, battle |
| SettlementStateRow | 75 | current control, razed, population polity, bound installation, accounts, grievance, the latest capture, its sackers (fix r1: +8, W-R1-10) |
| PolityStateRow | 89 | leader, fate, seat, held by, cities at the last settle, latest loss (by, settlement, war — fix r1: +4, W-R1-5), institutions, standing terms, 8 accounts |
| CustodyRow | 52 | captor, origin, account, ration clock, exact carry, unpaid rations, standing exchange offer |

POPULATED round trip (one world with a captive, an execution, a sack, a raze, a field battle with prisoners both ways, release, terms,
ransom, a standing exchange offer, rations split at a turn start, a war ended in conquest): exact length, bit-exact re-encoding, hash
equality, truncated and wrong-version tables refused, decoded rows equal to the originals field by field. Row lengths pinned.

---

## 5. The post-battle collapse (DIR5 §2.9)

A campaign formation of P persons fights as n entities. At the battle's end every entity is classified: Dead; Surrendered; alive with
hit points below its maximum = **Wounded** (derived from combat state); alive, unhurt, fled in a rout = **Routed**; otherwise **Fit**.
P is apportioned over the categories by the largest-remainder method (ties to the lowest category code; exact sum; with P = n each
category gets exactly its entity count). Routed men return to the colours if their side held the field; on a side that did not, the
share (MoraleMax − end morale) / MoraleMax of them **desert** (PROPOSAL; state-derived, no free coefficient). Flows: dead → killed sink;
wounded → the formation's wounded account (handed to M7.14, which owns recovery and mortality); captured → the captor's custody;
deserted → deserted sink; fit + returned stay in the formation. Nothing per entity survives. Vessel and aircraft formations collapse from
lane N's personnel ledger (killed and lost with the vessel → dead, captured → surrendered, everyone else fit; no per-person wounds).

Measured on lane TR's CombinedArms battle (KnowledgeAi both sides), one entity per soldier — every flow equals the engine's own end
state (dead = `FormationResult.Killed`, captured = `Surrendered`):

| Seed | Formation | Entities | Dead | Wounded | Routed | Surrendered | End morale | Deserted |
|---|---|---:|---:|---:|---:|---:|---:|---:|
| 42 | 1 (side 0, HI) | 50 | 26 | 0 | 24 | 0 | 0 | 24 |
| 42 | 2 (side 0, archers) | 60 | 22 | 2 | 36 | 0 | 269 | 26 |
| 42 | 3 (side 1, HI) | 50 | 24 | 1 | 25 | 0 | 40 | 0 (held the field) |
| 42 | 4 (side 1, archers) | 60 | 19 | 2 | 0 | 0 | 370 | 0 |
| 7 | 3 (side 1, HI) | 50 | 25 | 1 | 21 | 3 | 0 | 21 |

At 100 persons per entity every number scales exactly; at 37 persons per entity (remainders everywhere) people are conserved exactly and
two runs hash identically. Lane N's amphibious battle (seed 42): the warship formation's 300 crew lose 5 dead, the transports' 100
crew none, the aircrews (2, 1, 1) none; every vessel and aircraft formation's remaining count equals its persons minus the killed, lost
with the vessel and captured of lane N's personnel ledger, and the ledger totals are unchanged.

---

## 6. The execution content setting (D-079 Q12, OPEN)

`ExecutionContent` {Unset = 0, Show, CutAway} lives in the presentation namespace; the selector refuses `Unset`, so there is no default
— whoever ships cinematics must choose (a) or (b) of Q12. The simulation never reads it: a reflection test pins that no field, property
or parameter of any `Proto.M7.War.Outcomes` type has that type. The selector (`CinematicSelector`) is a pure function of the records:
an execution cue only when the fate row says Executed (shown or cut away by the setting); Standoff, BothEliminated and BothDisengaged
are Inconclusive (never a false victory); siege Capitulated/CapCapitulation → Surrender, Stormed → Stormed, Relieved/Lifted/CapLifted →
SiegeRaised; Conquest → Victory or Defeat by viewer; NegotiatedPeace → Peace.

---

## 7. Reused mechanisms (not rebuilt)

| From | Reused | How |
|---|---|---|
| Lane S | siege endings and control transfer (`EndSiege`), the captured garrison (`PrisonersOf`), installation civilians and granary, home treasury and food, "elsewhere" refugees, occupation unrest (its grievance input) | `WarOutcomeWorld` wraps `SiegeWorld` and shares its `WarLedger`; lane S code unchanged; its captures are observed after each delegated event |
| Lane E | forced plunder rate and `StockDraw` (sack loot); `HeadcountLoss` (civilian deaths and flight as competing flows); `RationBase` (prisoner rations); `LogisticsRates` table | called directly |
| Lane TR / N | `TacticalBattle` end state (`Entities()`, `BattleResult`, `HoldsField`, `MoraleEnd`), lane N's `PersonnelReport` for vessels/aircraft, `BattleEndReason` codes incl. Standoff | `BattleCollapse.FromTactical`; end codes in `BattleRow` |
| T1 / T2 / ADR-061 | `RateIntegrator.IntegrateExact` + `ExactFraction` carry; `WarPulseSchedule` (pulse of an instant per war); turn-start sub-stepping; `Runner` decision windows, order gate and replay | — |
| Lane E's insolvency cascade and lane E's per-year desertion | NOT reused: the cascade is an army-economy process with no settlement capture in it, and its desertion is a per-year rate driven by pay and morale; a rout's stragglers are a one-off post-battle split (the collapse) — attrition of the impressed and the wounded is left to M7.14 | — |

**Shared files touched (minimal, delimited):** `War/WarLedger.cs` — one appended `Commodity.Structures = 12` (codes 0–11 unchanged);
`Proto.M7.Bench/Program.cs` — one dispatch line for `outcomes`. Nothing in `WarTheaterWorld.cs`, `Runner.cs`, `EventScheduler.cs`,
`TacticalBattle*.cs` or any lane S/E file.

---

## 8. Determinism

Integer arithmetic only (Int128 intermediates, checked); no unordered containers; every ordering by integer id or event key
(settlements by id, polities by id, captures at one instant by `SourceId` = settlement id); account numbering by id, not input order
(3 permutations hash identically); the War Pulse cursor is a cache (never a result input). Determinism scan: CLEAN (92 files).

---

## 9. Build and test counts (MEASURED, this clone)

| Solution | Config | Before (0041f68) | After (lane W) |
|---|---|---|---|
| M7Proto.slnx | Release | build 0 warnings; 992 passed, 0 failed, 0 skipped (16 s) | build 0 warnings; 1,097 passed, 0 failed, 0 skipped (14 s) |
| M7Proto.slnx | Debug | build 0 warnings; 992 passed, 0 failed, 0 skipped (30 s) | build 0 warnings; 1,097 passed, 0 failed, 0 skipped (38 s) |
| M7Proto.slnx (verification round 1, §13) | Release | 1,097 passed (14 s), re-measured on 157130e | build 0 warnings; 1,124 passed, 0 failed, 0 skipped (13 s) |
| M7Proto.slnx (verification round 1, §13) | Debug | 1,097 passed (48 s), re-measured on 157130e | build 0 warnings; 1,124 passed, 0 failed, 0 skipped (36 s) |

M7Econ.slnx and M7DetMath.slnx: NOT run. Their dependency closures (Proto.M7.Econ / Proto.DetMath and their tests, Sim.Core, Sim.Data) contain
no file this lane changed (`git diff --stat 0041f68..HEAD` on those paths is empty; Proto.M7 is not in either closure), so their inputs
are byte-identical to the base; I did not spend the shared CPUs on two Sim.Core builds. Logs: `/dev/shm/m7c/W-logs-r2/base-*`, `final2-*`.

---

## 10. Mutants

Prioritized subset: 21 distinct defects in 25 runs (the first forms of W-M2 and W-M5 made code unreachable, which the build treats as
an error, and were re-formed as W-M2b and W-M5b; W-M13 and W-M18 were re-run after the tests they exposed were added). Bound: 200 s per suite run = 10 × the clean Release suite wall time (≈ 20 s); none came near
it (14–21 s each), no hang. One full Proto.M7.Tests run per mutant (`/dev/shm/m7c/W-logs-r2/run-mutants.py`, output
`mutants-out.txt`). The source was restored after each and `git status` showed no residue.

| Id | Defect injected | Verdict | Killing semantic test(s) |
|---|---|---|---|
| W-M1 | leader rule needs ≥ 2 cities at the last settle | KILLED (14) | `LastCityLost_ExecutesAtThatInstant_PulseExact`, `TwoLossesInOnePulse_…`, siege capitulation, … |
| W-M2b | no instant settlement (every capture settled at once; no post-battle window raised) | KILLED (30) | `LastCityLostAndAnotherTakenAtTheSameInstant_TheLeaderLives`, `SwapOfLastCities…`, `BothSidesEliminated…`, every disposition-at-the-window test |
| W-M3 | a fate may be applied twice | KILLED (1) | `LossThenRecaptureLaterInThePulse_TheExecutionStands_…` |
| W-M4 | execution attributed to the first loss, not the last | KILLED (5) | `TwoCitiesLostAtTheSameInstant_OneExecution_AttributedInKeyOrder…` |
| W-M5b | a taken seat never makes the leader captive | KILLED (13) | `SeatTakenFirst_…`, `OneCityLeft_…`, `SwapOfLastCities…`, leader-decision tests |
| W-M6 | razing one's own last city allowed | KILLED (1) | `RazingTheRazersOwnLastCity_IsRefused_…` |
| W-M7 | the post-battle window never closes | KILLED (1) | `Dispose_IsOfferedOnlyAtTheCaptureInstant(offset: 1)` |
| W-M8 | sack intensity ignores the sackers | KILLED (3) | `SackIntensity_ScalesWithSackersPerInhabitant_CappedAtOne` |
| W-M9 | loot goes back to the loser | KILLED (2) | `Sack_MovesExactlyWhatLaneEsDrawsAndHeadLossFlowsGive_…` |
| W-M10 | routed men desert on the side that held the field | KILLED (4) | `ATacticalBattle_CollapsesIntoFormationCounts_WithExactFlows` |
| W-M11 | apportionment ties to the highest category | KILLED (1) | `Apportion_TieDense_EqualRemaindersGoToTheLowestCategory` |
| W-M12 | only entities below half hit points are wounded | KILLED (3) | `ATacticalBattle_…` (oracle from the engine's end state) |
| W-M13 | prisoner rations not split at turn starts | first run KILLED only by a structural assertion of the populated-table test (not a semantic kill) → new test; re-run W-M13r KILLED (2) | `Prisoners_RationsAreSettledAtEveryTurnStart_TheSnapshotShowsWhatTheyAteSoFar` |
| W-M14 | ransom accepted up to twice the terms | KILLED (1) | `Ransom_NeedsTheOriginsStandingTerms_…` |
| W-M15 | a prisoner convention does not forbid enslavement | KILLED (1) | `PrisonerActions_AreGatedByTheCaptorsInstitutions` |
| W-M16 | captive execution ignores the Director-level rule | KILLED (1) | `CaptiveExecution_IsADirectorLevelRule_RefusedUnlessPermitted` |
| W-M17 | lane S's captured garrison never enters custody | KILLED (2) | `ASiegeCapitulation_IsTheCapture_…`, `AnAssaultOrderThatStorms_…` |
| W-M18 | conquest credited to the eliminated side (side 0 eliminated) | **SURVIVED** the first run (only side 1 had ever been eliminated in a test) → new test; re-run W-M18r KILLED (1) | `WhenSideZeroIsEliminated_TheConquestIsCreditedToSideOne` |
| W-M19 | an instant at a pulse start counted in the previous pulse | KILLED (2) | `LastCityLost_ExecutesAtThatInstant_PulseExact(offset: 0)`, siege capitulation pulse |
| W-M20 | leader-fate row encodes turn and instant in swapped slots | KILLED (2) | `DecodedRows_EqualTheOriginals_FieldByField` (bit-exact re-encoding alone cannot see a symmetric swap) |
| W-M21 | both sides gone is not a mutual collapse | KILLED (1) | `BothSidesEliminatedAtOneInstant_TheWarEndsInMutualCollapse` |

Teeth and aim (ADR-015 §7.2): every killing test asserts a property the system ought to have by DIR5 or by this proposal's stated rule;
W-M13's first kill was teeth without aim and is not counted.

---

## 11. Bench

`dotnet Proto.M7.Bench.dll outcomes 64 2000 4000 3` (Release, 4 shared CPUs under `nice -n 19`, the G2 track running; wall time is
indicative only), log `/dev/shm/m7c/W-logs-r2/bench-outcomes.txt`:

| Measure | Result |
|---|---|
| 4,000 captures at distinct instants, 64 polities, 2,000 cities, every 4th victor an AI sacker | median 1,495 ms (≈ 374 µs per capture); 3 reps, identical state hash |
| Record export (11 tables after that run) | 1,086,332 bytes, 1.7 ms |
| Collapse of CombinedArms at 100 persons per entity (220 entities, 22,000 persons) incl. world flows | 0.07 ms |
| *Re-run after verification round 1* (churn captures now declared outside any war, §13; log `/dev/shm/m7c/W-fix-r1/bench-outcomes.txt`) | churn median 1,512 ms (≈ 378 µs per capture), 3 reps identical hash `A79BA69B255CE983`; export 1,102,588 bytes, 1.8 ms; collapse 0.11 ms. *Corrected in round 2 (W-R2-12): NOT like-for-like with the 1,495 ms above.* With war −1, `PulseOf` and the opposition checks return at once, so the war-attribution paths the first run exercised are skipped, and the record differs (no war or pulse in any row); the two numbers measure different paths and are not a re-measurement of one quantity |

Each settlement of an instant recounts every polity's cities (O(polities × settlements); EmpireQuery's choice: extinction is derived,
nothing stored to drift). At 64 × 2,000 that is ~0.4 ms per capture instant — negligible for captures, which are rare events. If a
future map has far more settlements, a per-polity counter maintained on control changes (with a recount check in tests) is the option.

---

## 12. Limitations and open questions

**Director-level choices (PROPOSALS, not decided here):**

| # | Question | Options and my reading |
|---|---|---|
| DQ1 (C32 flag) | May a captor execute a captive leader where DIR5 §4.1 does not apply? | `CaptiveExecutionRule` {NotPermitted, Permitted}; no default; every caller passes one |
| DQ2 (C32 flag) | Does razing kill or displace the population? | *Changed in verification round 1 (W-R1-8):* `RazePopulationRule` {Displace, KillThenDisplace}, no default, every caller passes one (as DQ1). Displace: no one killed, everyone expelled to the refugees; KillThenDisplace: the sack's killing over the customary days, then every survivor expelled. A third reading ("the raze kills everyone left") is not built; it would be one more value |
| DQ3 (D-079 Q12) | Default of the execution content setting | (a) Show with a cut-away option or (b) CutAway with a show option; implemented without a default |
| DQ4 | Succession: a polity that regains a city after its leader's execution stays leaderless here (no succession rule exists; D-048 defers capital succession) | a successor rule belongs to politics (M8) or a new Director rule |
| DQ5 | DIR5 §4.1 is unconditional: a leader held captive by A is executed when B takes the last city; a free leader with an army in the field is executed too | implemented as written; the record names `ByPolity` (the taker of the last city) and `HeldBy`; whether exile or escape should ever avert it would be a new Director rule |
| DQ6 | Enslavement and conscription of prisoners exist only where the captor's state-derived institutions allow (`Slavery`, no `PrisonerConvention`) | confirm the content is wanted; the gate is in place |
| DQ7 (fix r1, W-R1-7) | What counts as a "city" for DIR5 §4.1? C31's READING is "the settlements the polity controls"; Sim.Core `EmpireQuery.IsExtinct` is "controls no settlement" | the lane counts only cities (lane S `InstallationKind.City` / `IsCity`): a polity holding only a fort, harbour, airport or depot has lost all its cities, its leader is executed and it is extinct here. Counting every controlled installation instead is a one-line predicate change (`CityCount`); not decided here |
| DQ8 (fix r1, W-R1-11c) | Do post-battle orders (dispositions, prisoner actions, ransom terms, exchange offers, leader decisions) count against DIR5 §2.8's two-action maximum per decision window? | as built they do NOT: they go through the `Runner` order gate directly, not through T3's `ActionWindowBook` (one test issues four outcome orders in one window). Whether they are "actions" in the §2.8 sense is the Director's (the two-action maximum is a Director decision); counting them would route them through `ActionWindowBook` |
| DQ9 (fix r1, W-R1-11e) | Should killing prisoners be an option (DIR5 §4.2 "prisoners"; D-079 Q12 is about execution/death content)? | not offered (the prisoner options are release, conscript, enslave, ransom, exchange, hold); flagged as a Director content question, not built and not decided |
| DQ10 (fix r2, W-R2-11) | C24 (plan revision 2026-11-05, "Aggregation, casualties and Ledger coherence") says soldiers stay immortal until M7.14 lands (D-067 R15, alias "M7.4" = M7.14) and asks for "an immortality pin that holds until the M7.14 switch and is lifted by it". The lane brief asks for the dead as an explicit flow, and the collapse moves them to the killed sink unconditionally, with no switch. Which governs battle deaths? | (a) battle deaths are the battle's resolution, not C24's attrition and mortality: the collapse stands as built and C24 gets a clarifying note; (b) C24 governs: the dead go to the killed sink only once the M7.14 switch is on, and until then the dead share stays in the formation while the casualty record still counts it; (c) the dead go to a held "fallen" account that M7.14 owns. Blast radius: one transfer in `OnBattleEnded` and the killed-sink assertions of the collapse tests; the casualty rows are the same under all three. Not decided here (C24 is a plan contract); the earlier report did not cite C24 at all, which was an omission |

**Limitations (stated, not hidden):**

1. Sack coefficients (kill, flee, burn, harm-grievance) are PLACEHOLDERS; the 3-day duration is a cited custom; the loot rate is lane E's
   PROPOSAL forced rate. The intensity scaling q = min(sackers, people)/people is my mechanism, not calibrated against a battery.
2. The derived occupation grievance is the INPUT lane S's unrest needs, but it is not wired live into a running `SiegeWorld` (lane S
   reads it from its immutable `InstallationSpec`); the effect is demonstrated by constructing lane S worlds with the derived and the base
   input (the sacked city revolts sooner). Integration: a mutable grievance in lane S's installation state (one field, a row bump).
3. A razed lane-S-bound installation stays in lane S under the victor's control with no people (lane S has no destroyed state); this
   world marks it razed and no longer a city. Integration: a Razed flag in lane S's installation row.
4. Siege casualties by side and cause: only the captured and the marched-out are attributable; lane S's loss sinks (killed, starved,
   thirst, disease, deserted) are shared by both sides. Integration: per-side sinks in lane S.
5. The collapse needs per-entity hit points, so it takes tactical (lane TR/N) results; an AutoResolver result has no entities and no
   wounded classification here.
6. Leaders and commanders are not on battlefields (generals are O-34): no leader killed or captured in a field battle, no commander
   decisions; a leader is captured only when its seat is taken.
7. AI doctrines exist only for the settlement disposition; AI captors hold prisoners and captive leaders.
8. Prisoner mortality, the wounded's recovery and death, and the desertion of impressed men are attrition (M7.14): the accounts are the
   hand-off and nothing moves out of them here. Unpaid prisoner rations are recorded, not enforced.
9. Ransom is a flat price per head (no rank or value), in coin only (`Coinage` on both sides); exchange is one for one.
10. A polity eliminated and later back with a city is not re-entered in its war (its participant row stays `Eliminated`); wars are
    inputs (declared outside M7); peace and treaties are M8. Prisoners and captive leaders held by an eliminated polity stay held.
11. A capture made by an order at T (lane S's immediate assault) is settled when the order completes; no decision-window hit can be
    raised at T for its leader-fate change (the record is complete; the player sees it at the next window). Raising a post-order window
    at T would be an ADR-061 change — out of scope.
12. The capture test device stands in for the M7.13 capture paths other than a siege; a "raze without capture" path does not exist.
13. Records are append-only lists in memory with no compaction and no resume-from-rows (as lane S); the A-47 schema ADR is owed when
    the packet is authorized and is not written here.
14. M7Econ.slnx and M7DetMath.slnx were not run (their dependency closures are unchanged; §9).
15. (fix r1, W-R1-11d) C35 asks to preserve "naval outcomes" as a distinction; `CinematicSelector` does not distinguish them: a naval
    battle reaches it as a field battle (`BattleKind.Field`), so its cue is Victory/Defeat/Inconclusive, never a naval-specific one.
    A `BattleKind.Naval` (or a domain field on `BattleRow`) would be needed.
16. *FIXED in verification round 2 (W-R2-9, §14): raised sieges now write a battle row.* (fix r1, found while reproducing W-R1-9; NOT fixed that round) A lane S siege that ends WITHOUT a capture (Relieved, Lifted,
    CapLifted) writes no lane W `BattleRow`: `ObserveSiege` reads only lane S's captures, so those ends exist only in lane S's own
    records, and the selector's `SiegeRaised` cue has no lane W row to read. Recording them is a small addition in `ObserveSiege`
    (read `SiegeWorld.Ends` as well); it is new behaviour beyond the verifier's findings and is left for the next round.
17. (fix r1, W-R1-2; *extended in round 2, W-R2-2:* also across field battles whose spans overlap) A campaign formation fielded as
    several battle formations, or in two battles at once, is refused (`ScheduleBattle`, `FromTactical`), not supported. Supporting it would apportion the formation's people over its inputs by entity count first and then split each input.
18. (fix r1, W-R1-5) The capture test device now refuses a war in which the two parties are not current, opposed participants; a lane S
    capture takes the lowest-id ongoing war in which they are (`ActiveWarBetween`), or none. *Superseded in round 2 (W-R2-5, W-R2-9,
    §14):* a field battle is recorded in its declared war only if that war is still fought by both at its end (else outside any war),
    and a captive-leader decision names the war of the captivity (read from the captive fate row) only while both still fight it
    (else none); `WarBetween` is no longer used for any record.
19. (fix r2, W-R2-11) C33's delegated battle row lists a "location". `BattleRow` records the settlement (sieges, stormings, raised
    sieges of a bound installation); a field battle, and a siege of an UNBOUND lane S installation, has none (`SettlementId` −1). The
    stand-in hand-off (`FieldBattle`) carries no campaign position and no prototype world has one for a battle (T2's theatre world has
    no battles; lane TR's coordinates are local to the tactical map), so a location column would be filled with invented data. Proposal:
    a theatre cell (`CellCoord`, 2 × i64) on `FieldBattle` and `BattleRow` once the M7.15 hand-off exists, and the lane S installation
    id for unbound installations. Not built.
20. (fix r2, W-R2-4) A world bound to lane S refuses any lane S City installation that no settlement binds; unbound Forts, Harbours,
    Airports and Depots are accepted (they are no city under DQ7's reading). If DQ7 is ruled the other way (every controlled
    installation counts), this check must cover every installation kind.

---

## 13. Verification round 1 (fix round, 2026-11-06)

**Base and provenance.** The clone `/dev/shm/m7c/W` was present on `lane/W` at `157130e` (the commit the verifier pinned); no restore
was needed. Logs of this round: `/dev/shm/m7c/W-fix-r1/`. Baseline re-measured in this clone before any change: Release 1,097
passed (14 s), Debug 1,097 passed (48 s), 0 warnings (`base-*.txt`).

**How each finding was reproduced (on 157130e, before any change).** (a) The five surviving verifier mutants V-M1, V-M2, V-M4, V-M5
and V-M7, re-run by my own runner (one full Release suite each): all SURVIVED with 1,097 passed (`repro-mutants-out.txt`).
(b) The verifier's probes V1–V6, copied into a temporary test file together with two probes of mine (R2b: `FromTactical` with a
many-to-one map; R10: the state hash with `Sackers` changed by reflection), run against 157130e; the file was deleted afterwards
(`repro-probes.txt`). (c) Greps of the code, tests, report and governance records for the documentation and coverage findings.
No finding was acted on before it was reproduced here.

### 13.1 Verdict table

| Finding | Sev. | Verdict | Evidence on 157130e (measured) | Change | Semantic test(s) |
|---|---|---|---|---|---|
| W-R1-1 elimination after a captive execution | major | **REPRODUCED** | V1 fails: "participant 0: left -1 exit 0; war 0: outcome 0 end -1 winner -1" | `Settle` step 1: a polity that has lost all its cities and whose leader is already dead leaves its wars as Eliminated at that instant (`Eliminate`); no second fate row | `CaptiveExecuted_ThenLastCityLost_ThePolityIsEliminatedAtThatInstant_AndTheWarEnds` (t2 − 1 / t2 / t2 + 1) |
| W-R1-2 one campaign formation as two battle formations | major | **REPRODUCED** | V2 fails: "killed 3000, remaining 0, side-1 personnel at start 6000"; R2b: `FromTactical` accepted the map | refused: `ScheduleBattle` (a campaign formation in two inputs) and `FromTactical` (two tactical formations → one campaign formation) throw `ArgumentException` (limitation 17) | `OneCampaignFormationFieldedAsTwoBattleFormations_IsRefused_NotMisSplit` (and the one-to-one split: 1,500 / 1,500 / 3,000), `FromTactical_RefusesTwoTacticalFormationsForOneCampaignFormation` |
| W-R1-3 casualties by side and polity unpinned | major | **REPRODUCED** | V-M1 and V-M4 SURVIVED (1,097 / 1,097) | tests only (the code was right) | `FieldBattleCasualties_AreRecordedUnderTheSufferingSideAndPolity_ByCause_Exactly` (every row: battle, side, polity, cause, count), `ASiegeCapture_RecordsTheCapturedUnderTheDefender_AndTheBattleAndTheFateInOneWarAndPulse` |
| W-R1-4 raze flows and bound raze untested | minor | **REPRODUCED** | V-M5 and V-M7 SURVIVED | tests only | `Raze_TheExpelledAreRefugees_TheDeadAreOnlyWhatTheRuleKills_AndBothRulesTakeTheSameLoot` (2), `RazeOfABoundCity_StopsCountingIt_AndItsPeopleGoExactlyWhereTheRecordSays` (2) |
| W-R1-5 war of a siege battle unpinned; fate/battle war disagree; device accepts any war | minor | **REPRODUCED** | V-M2 SURVIVED; V5: "fate war 0 pulse 3; battle war -1 pulse -1"; V4: "battle war 0 … WarBetween(2,1) = -1" (allies recorded as fighting in war 0) | the fate row takes the war of the capture that caused it (`Pol.LastLossWar`, now in `PolityStateRow`; `seat.TakenWar` for a captive); the capture device refuses a war in which the two parties are not current participants on opposite sides of an ongoing war; a lane S capture takes `ActiveWarBetween` (lowest-id such war, or none); a field battle's polities must stand on opposite sides of its war (checked at scheduling) | `TheFateRow_NamesTheWarAndPulseOfTheCaptureThatCausedIt` (two wars with different phases; and the no-war case), `TheCaptureDevice_RefusesAWarInWhichThePartiesAreNotOpposed` (allies, ended war, a war that goes on without the eliminated polity), `ASiegeCapture_…` |
| W-R1-6 sack of one's own population costs nothing | minor | **REPRODUCED** | V3: "own sack: killed 1000, fled 2000, loot 627226, grievance 0" | PROPOSAL rule L13 (§3.3): Sack and Raze of a settlement whose population is the taker's own are refused (`OutcomeRefusal.OwnPopulation`); an AI doctrine falls back to Occupy | `OnesOwnPopulation_CannotBeSackedOrRazed_ARecaptureIsALiberation` |
| W-R1-7 "cities" narrowed and credited to C31 / Sim.Core | minor | **REPRODUCED** | `Sim.Core/State/EmpireQuery.cs:36-44` "extinct when it controls no settlement"; C31 READING (`m7-2-m7-3-revision-2026-11-05.md:671`) "the settlements the polity controls"; `WarOutcomeWorld.IsExtinct` doc said "as Sim.Core EmpireQuery.IsExtinct … controls no city"; `LeaderExecutionTests.cs:47-49` | documentation only: code comments and §2.1 / §2.3 corrected; the reading is raised as DQ7; behaviour unchanged (the lane does not decide it) | none new (no behaviour change; `LastCityLost_ExecutesAtThatInstant_PulseExact` keeps pinning the reading as built) |
| W-R1-8 DQ2 hard-coded | minor | **REPRODUCED** | `OutcomeTypes.cs` had a rule enum only for DQ1; the raze branch of `Plunder` took no parameter | `RazePopulationRule` {Displace, KillThenDisplace}: a required constructor parameter with no default (as `CaptiveExecutionRule`); test rigs pass KillThenDisplace as a TEST INPUT | `TheRazeRule_IsADirectorLevelFlag_WithNoDefault`, the two raze theories above (both rules) |
| W-R1-9 eligibility matrix over siege endings untested | minor | **REPRODUCED** | grep: Relieved, Lifted, CapLifted, CapCapitulation, GarrisonDestroyed appear only as selector rows in `OutcomeCinematicsTests` | tests only (the behaviour was right) | `SiegeEndings_EligibilityMatrix_OnlyAPassOfControlOffersADisposition` (7 endings, each produced by lane S's own recipe, the besieger's Sack issued at the ending instant) |
| W-R1-10 `Sackers` not in the state hash | nit | **REPRODUCED** | R10: the state hash with `Sackers` 5,000 equals the hash with `Sackers` 1 (`A39A428AA6081C72…`) | `SettlementStateRow.Sackers` (+8 bytes, 67 → 75; codec version 2). `OutcomeRow` checked too: derivable from the hashed outcome table (the last row of that settlement), so no hole | `TheSackIntensityInput_IsInTheRows_AndSoInTheStateHash` (reflection + bound: the row carries lane S's `AttackerPeople` at the capture, not `A0`), `DecodedRows_EqualTheOriginals_FieldByField` (now also the settlement state rows) |
| W-R1-11 (a) polities of a battle | nit | **REPRODUCED** | V6: "run: ArgumentOutOfRangeException" (thrown inside the event loop at the battle end) | `ScheduleBattle` checks both polities (range, distinct, opposed in its war) at scheduling | `ScheduleBattle_ChecksItsPolitiesAtScheduling_NotInsideTheEventLoop` |
| W-R1-11 (b) exchange delivery pin | nit | **REPRODUCED** | the exchange test uses TB and TB + 2 d only | test only | `Exchange_TakesEffectAtTheInstantTheSecondOfferStands` (second offer at TA − 1 / TA / TA + 1; nothing moves at the effect instant − 1) |
| W-R1-11 (c) two-action maximum | nit | **REPRODUCED** | no mention of DIR5 §2.8 or `ActionWindows` in lane W's code or report | stated as built (they do not count) and raised as DQ8 | — |
| W-R1-11 (d) naval outcomes | nit | **REPRODUCED** | only a code comment (`OutcomePresentation.cs:43`) | limitation 15 | — |
| W-R1-11 (e) prisoner killing | nit | **REPRODUCED** | no option, no flag | raised as DQ9 (not built, not decided) | — |

No finding was refuted.

### 13.2 Existing tests whose premise changed (stated, not hidden)

- `LeaderExecutionTests.LossThenRecaptureLaterInThePulse_…`: polity 0 is eliminated at t1 and war 0 ends in conquest; its re-capture at
  t2 and polity 1's capture at t3 were declared "in war 0" — exactly the W-R1-5 inconsistency. They are now declared outside any war
  (`ScheduleCapture(…, -1, …)`); every assertion is unchanged.
- `OutcomeCodecTests.RowLengths_ArePinned`: 67 → 75 (settlement state) and 85 → 89 (polity state); `DecodedRows_…` also checks the
  settlement state rows field by field and that `Sackers` and `LastLossWar` are populated.
- `OutcomeRig.Build` / `BuildBound`: pass `RazePopulationRule.KillThenDisplace` (a TEST INPUT) unless a test names the other rule.
- Bench churn (`OutcomeBench`): its victor sequence is not war-consistent (allies and eliminated polities take cities), which the
  capture device now refuses; its captures are declared outside any war. The bench hash therefore changed (§11).

### 13.3 Mutants (fix round)

Bound: 180 s per suite run = 10 × the clean Release suite wall time after the fixes (18 s). One full Release `Proto.M7.Tests` run
per mutant (`/dev/shm/m7c/W-fix-r1/run-mutants.py`, outputs `repro-mutants-out.txt`, `fix-mutants-out.txt`); runs took 15–21 s; no
hang. Source restored after each, `git status` clean of residue.

| Id | Defect injected | Verdict | Killing semantic test(s) |
|---|---|---|---|
| V-M1 | field-battle casualties under the other side's polity | KILLED (was SURVIVED on 157130e) | `FieldBattleCasualties_…Exactly` |
| V-M2 | lane S capture recorded outside any war | KILLED (was SURVIVED) | `ASiegeCapture_…InOneWarAndPulse`, `ASiegeCapitulation_IsTheCapture_…` |
| V-M4 | captured garrison as the taker's casualty | KILLED (was SURVIVED) | `ASiegeCapture_…InOneWarAndPulse` |
| V-M5 | a ruin still counts as a city | KILLED (was SURVIVED) | `RazeOfABoundCity_…` (2) |
| V-M7 | raze expulsions to the killed sink | KILLED (was SURVIVED) | `RazeOfABoundCity_…` (2), `Raze_TheExpelledAreRefugees_…` (2) |
| F-M1 | W-R1-1 reverted (no elimination when the leader is already dead) | KILLED (2) | `CaptiveExecuted_ThenLastCityLost_…` (offsets 0, +1) |
| F-M2 | W-R1-2 reverted in `ScheduleBattle` | KILLED | `OneCampaignFormationFieldedAsTwoBattleFormations_…` |
| F-M3 | W-R1-2 reverted in `FromTactical` | KILLED | `FromTactical_Refuses…` |
| F-M4 | execution fate row takes the lowest-id war | KILLED | `TheFateRow_NamesTheWarAndPulse…` |
| F-M5 | captive fate row takes the lowest-id war | KILLED | `TheFateRow_NamesTheWarAndPulse…` |
| F-M6 | capture device accepts any war | KILLED | `TheCaptureDevice_RefusesAWar…` |
| F-M7 | a participant that has left an ongoing war still counts as opposed | KILLED | `TheCaptureDevice_RefusesAWar…` (the war-goes-on case) |
| F-M8 | own population may be sacked | KILLED | `OnesOwnPopulation_…` |
| F-M9 | the raze rule ignored | KILLED (2) | `RazeOfABoundCity_…(Displace)`, `Raze_TheExpelledAreRefugees_…(Displace)` |
| F-M10 | `Sackers` left out of the row | KILLED (2) | `TheSackIntensityInput_…`, `DecodedRows_…` |
| F-M11 | same polity on both sides accepted | **SURVIVED** the first run: the test's same-polity battle was in war 0, so the "opposed in its war" check refused it anyway → the test now also schedules it outside any war; re-run F-M11r KILLED | `ScheduleBattle_ChecksItsPolitiesAtScheduling_…` |
| F-M12 | lane S's CapCapitulation does not pass control in lane W | KILLED | `SiegeEndings_EligibilityMatrix_…(CapCapitulation)` |
| F-M13 | the exchange recorded 1 ms after it took effect | KILLED (3) | `Exchange_TakesEffectAtTheInstantTheSecondOfferStands` (−1, 0, +1) |
| F-M14 | `LastLossWar` left out of the polity row | KILLED (2) | `TheFateRow_NamesTheWarAndPulse…`, `DecodedRows_…` |

Teeth and aim (ADR-015 §7.2): each killing test asserts a property the system ought to have (DIR5 §4.1/§4.3, C32/C33) or the lane's
stated PROPOSAL rule (L12, L13, the Director-level raze flag); none is a golden-only kill.

### 13.4 Counts, scan, bench (MEASURED, this clone, after the round)

| Solution | Config | Before (157130e) | After |
|---|---|---|---|
| M7Proto.slnx | Release | 0 warnings; 1,097 passed, 0 failed, 0 skipped | 0 warnings; 1,124 passed, 0 failed, 0 skipped (13 s) |
| M7Proto.slnx | Debug | 0 warnings; 1,097 passed, 0 failed, 0 skipped | 0 warnings; 1,124 passed, 0 failed, 0 skipped (36 s) |

M7Econ.slnx and M7DetMath.slnx: not run; no file in their dependency closures changed in this round either. Determinism scan
(`scripts/check-proto-determinism.sh`): CLEAN, 92 files. Bench: §11 (re-run row).

### 13.5 Not done / new observations

- Limitation 16 (found while reproducing W-R1-9, not a verifier finding, not fixed): siege ends without a capture write no lane W
  battle row.
- DQ7, DQ8, DQ9 and the DQ2 switch are PROPOSALS / questions for the Director; nothing here decides them.
- The many-to-one hand-off of W-R1-2 is refused, not supported (limitation 17).

---

## 14. Verification round 2 (fix round, 2026-11-06)

**Base and provenance.** The clone `/dev/shm/m7c/W` was present on `lane/W` at `9c5f135` (the commit the verifier pinned); no restore
was needed. Logs of this round: `/dev/shm/m7c/W-fix-r2/` (the earlier `W-logs-r2/` and `W-fix-r1/` directories are untouched).
Baseline re-measured in this clone before any change: Release 1,124 passed (15 s), Debug 1,124 passed (33 s), 0 warnings
(`base-*.txt`).

**How each finding was reproduced (on 9c5f135, before any change).** (a) The verifier's seven surviving mutants MV-1 … MV-7, re-run by
my own runner (one full Release `Proto.M7.Tests` suite each): all SURVIVED with 1,124 passed (`repro-mutants-out.txt`). (b) The
verifier's six probes P1–P6, copied into a temporary test file together with two probes of mine (P7: a lane S capture after the
defender was eliminated, with the war ended and with the war going on; P8: a raised siege's lane W rows, and the war a captive-leader
decision names), run against 9c5f135; the file was deleted afterwards (`repro-probes.txt`). (c) Greps and a diff for the two
documentation findings (`repro-docs.txt`). No finding was acted on before it was reproduced here.

### 14.1 Verdict table

| Finding | Sev. | Verdict | Evidence on 9c5f135 (measured) | Change | Semantic test(s) |
|---|---|---|---|---|---|
| W-R2-1 bound population polity not cross-checked | major | **REPRODUCED** | P5 with lane W −1 (lane S: population 0, controller 1): "row 1; lane S IsOccupied after capture: False; liberator's sack: reason 0; killed 514 fled 1028 loot 313612 grievance 2199; refugees to polity 1 elsewhere 1028"; MV-1 SURVIVED (1,124 / 1,124) | the constructor refuses a bound settlement whose effective population polity (`PopulationPolity`, or its controller when −1) differs from lane S's effective one, like the controller and city-kind checks | `ABoundCity_AgreesWithLaneSOnWhosePeopleLiveThere_ALiberatorMayNotSackThem` (−1 and 2 refused; 0: the liberator's sack is refused `OwnPopulation`, lane S counts the city unoccupied, the ledger equals the run without the order), `ThePopulationPolity_NotTheController_…` (standalone: own people not sackable; a sack's refugees go to the population's polity, not the controller's) |
| W-R2-2 one formation in two overlapping battles | minor | **REPRODUCED** | P1: "battles 2; side-1 personnel at start per battle: 3000, 0; killed 3000; formation 0 remaining 0"; the same men as one input: "killed 1500; remaining 1500" | `ScheduleBattle` refuses a campaign formation that is already in another field battle (pending or done) over an overlapping span: (start, end] spans sharing more than a boundary instant, or the same end instant (L12) | `ACampaignFormation_FightsOneBattleAtATime_InSequenceItFightsWhatTheLastLeft` (second battle starting at the first's end − 1 ms refused and nothing scheduled; at the end and at end + 1 ms accepted and fought by what is left: 3,000 → 1,500 → 750, killed 2,250), `TwoBattlesEndingAtOneInstant_CannotShareAFormation_TwoFormationsMayFightAtOnce` |
| W-R2-3 a leader captive of its own polity; release seat unpinned | minor | **REPRODUCED** | P2: "orders: 0, 0, 1, 11"; "leader 0: fate Captive heldBy 0 seat -1"; fate row "Reigning->Captive cause SeatTaken … by 0"; MV-5 SURVIVED | `Settle` step 2: a seat razed by its own polity (it took the city at that instant; only the taker may raze) is no captivity: the leader moves to the polity's lowest-id city (`LowestCity`, now shared with `Free`); no fate row (PROPOSAL L14) | `ALeaderReleasedIntoACityItsOwnPolityThenRazes_MovesToItsLowestIdCity_NeverItsOwnCaptive` (orders 0, 0, `NotEligible`; Reigning, held by none, seat 3; released into city 1, the lowest-id city then; no fate row by or held by its own polity), `AReleasedLeader_ReturnsToItsPolitysLowestIdCity` |
| W-R2-4 an unbound lane S city is invisible to the leader rule | minor | **REPRODUCED** | P6: "lane S city 0 (InstallationKind.City) taken by 1 from 0 …; lane W: cities of 0 = 0, fate Reigning, fates 0, battle settlement -1, war outcome Ongoing" | a world bound to lane S refuses any lane S City installation that no settlement binds; other kinds may stay unbound (limitation 20) | `ALaneSCity_MustBeBound_AFortNeedNotBe` (City refused; an unbound Fort's capture is recorded with no settlement and leaves the leader reigning) |
| W-R2-5 a field battle recorded in a war that has ended | minor | **REPRODUCED** | P4: "war 0: outcome Conquest end 171081244800000"; "field battle: war 0 end 171082540800000 pulse 3" | at the battle's end its declared war is recorded only if it is ongoing and both polities still fight it on opposite sides; otherwise no war (pulse −1); prisoners are taken either way | `AFieldBattleThatOutlastsItsWar_IsRecordedOutsideIt_PinnedAtTheWarsEnd` (end at the war's end − 1 / at it / + 1: war 0 and its pulse, war 0 and its pulse, none) |
| W-R2-6 the lane S capture's war rule unpinned | minor | **REPRODUCED** (coverage gap; the code was right) | MV-2 SURVIVED; P7 (mine) shows the path is reachable: "war 0 outcome Conquest …; battle row war -1 pulse -1; WarBetween(1,0) 0, ActiveWarBetween(1,0) -1", and the same with the war going on without polity 0 | tests only | `ALaneSCapture_IsRecordedOnlyInAWarInWhichBothStillFight` (defender not eliminated: war 0 and its pulse; eliminated with the war ended, or going on: none) |
| W-R2-7 the lane S intake's ration clock unpinned | minor | **REPRODUCED** (coverage gap) | MV-4 SURVIVED | test only | `TheGarrisonLaneSTakesPrisoner_EatsFromItsIntakeInstant` (clock at the intake instant, nothing eaten before it, exactly `IntegrateExact(P × RationBase, ts − tc)` at the next turn start, carry equal) |
| W-R2-8 three stated properties unpinned | minor | **REPRODUCED** (coverage gaps) | MV-3, MV-6, MV-7 SURVIVED | tests only (two assertions added to an existing test) | `RazeOfABoundCity_…` (+ lane W's controller equals lane S's), `TheRazeRule_LeavesASackUntouched`, `SeatAndAnotherCityLostToTwoVictorsAtOneInstant_TheLeaderIsTheSeatTakersCaptive` (2 cases) |
| W-R2-9 raised sieges unrecorded; decisions named `WarBetween` | minor | **REPRODUCED** | P8: "lane S end Lifted at 171081072000000; lane W battle rows 0"; "Reigning->Captive cause SeatTaken war 1 pulse 0", then "Captive->Reigning cause Released war 0 pulse 3" | `ObserveSiege` writes a Siege battle row for every lane S end without a capture (besieger side 0, defender side 1, winner side 1, the war chosen as for a lane S capture, the bound settlement or −1, no casualty row); a captive-leader decision names the captivity's war (that of the latest captive fate row) while both still fight it, else none | `SiegeEndings_EligibilityMatrix_…` extended (Relieved, Lifted, CapLifted: one row with its codes, war 0 and its pulse, sides, no casualties, the `SiegeRaised` cue; capture endings: one row), `ACaptiveLeaderDecision_NamesTheWarOfTheCaptivity` (Release, Execute), `ACaptiveLeaderReleasedAfterTheCaptivitysWarEnded_IsRecordedOutsideAnyWar` |
| W-R2-10 pending battle inputs outside the state hash | nit | **REPRODUCED** | P3: "at tb-1: hash equal True"; "after tb: hash equal False; killed 0 vs 3000" | `StateHashHex` includes every scheduled field battle's end instant, war, start, polities, mechanism, end code, field holder and collapse inputs (and the new raised-siege counter) | `AScheduledBattlesInputs_AreInTheStateHash` (same inputs: equal; other inputs or another end instant: different, before the battle) |
| W-R2-11 C24 not cited; no battle location | nit | **REPRODUCED** | grep of this report for `C24`, `immortal`, `R15`: no hit; `BattleRow` has no location field | documentation: DQ10 (the C24 conflict, options and blast radius; not decided), limitation 19 (location; not built: nothing in the prototype produces a field battle's campaign position) | — |
| W-R2-12 the bench re-run not like-for-like | nit | **REPRODUCED** | `git diff 157130e..9c5f135` of `OutcomeBench.cs`: the churn captures' war 0 → −1 | §11's re-run row corrected; this round's run (§14.4) states its comparability and the hash move | — |

No finding was refuted.

**The war of a record, after this round.** Every record now names war w only if, at the record's instant, w is ongoing and the
record's two polities still take part in it on opposite sides: a capture (the device refuses otherwise; lane S takes the lowest-id such
war), a field battle (its declared war, else none), a raised siege (as a lane S capture), a captive-leader decision (the captivity's
war, else none), and the fates a capture causes (the capture's war). `WarBetween` is no longer used for any record.

### 14.2 Tests: what failed on 9c5f135, what changed

The cases that pin changed behaviour, run against the 9c5f135 source with this round's tests, FAILED there (14 cases, measured,
`new-tests-on-9c5f135.txt`): the population cross-check (2), the overlap refusal (2), the self-captivity, the unbound city, the
battle after its war (offset + 1), the decision's war (3), the state hash, and the three raised-siege cases of the matrix. The coverage
cases (W-R2-6, W-R2-7, W-R2-8 and the MV-1 / MV-5 pins) passed there because the code was right; their teeth are the mutants below.
Existing tests: none failed after the changes; two were extended (`RazeOfABoundCity_…`, `SiegeEndings_EligibilityMatrix_…`), and no
assertion was removed or loosened.

### 14.3 Mutants (fix round)

Bound: 320 s per suite run = 10 × the clean Release suite wall time measured just before (32 s, the shared CPUs busy). One full
Release `Proto.M7.Tests` run per mutant (`/dev/shm/m7c/W-fix-r2/run-mutants.py`; outputs `fix-mutants-out.txt`,
`fix-mutants-out-2.txt`); runs took 22–35 s; no hang. Source restored after each, `git status` clean of residue.

| Id | Defect injected | Verdict | Killing semantic test(s) |
|---|---|---|---|
| MV-1 | `PopulationOf` ignores `PopulationPolity` | KILLED (3; SURVIVED on 9c5f135) | `ABoundCity_…`, `ThePopulationPolity_NotTheController_…` |
| MV-2 | lane S capture in `WarBetween` | KILLED (2; SURVIVED) | `ALaneSCapture_IsRecordedOnlyInAWarInWhichBothStillFight` (both eliminated cases) |
| MV-3 | a bound raze sets lane W's controller to −1 | KILLED (2; SURVIVED) | `RazeOfABoundCity_…` (both rules) |
| MV-4 | lane S intake without settling the ration clock | KILLED (1; SURVIVED) | `TheGarrisonLaneSTakesPrisoner_EatsFromItsIntakeInstant` |
| MV-5r | a freed leader to the highest-id city (re-formed on `LowestCity`: `Free` now calls it, so the verifier's pattern moved) | KILLED (3; MV-5 SURVIVED) | `AReleasedLeader_…`, `ALeaderReleasedIntoACity…`, `ACaptiveLeaderReleasedAfterTheCaptivitysWarEnded_…` |
| MV-6 | `Displace` also spares a sack's population | KILLED (1; SURVIVED) | `TheRazeRule_LeavesASackUntouched` |
| MV-7 | the captive held by the instant's last-loss taker | KILLED (2; SURVIVED) | `SeatAndAnotherCityLostToTwoVictorsAtOneInstant_…` (both cases) |
| F2-M1 | W-R2-1 reverted (no population cross-check) | KILLED (2) | `ABoundCity_…` (−1, 2) |
| F2-M2 | W-R2-4 reverted (unbound lane S city accepted) | KILLED (1) | `ALaneSCity_MustBeBound_AFortNeedNotBe(City)` |
| F2-M3a | W-R2-2 reverted, first form `oEnd == oEnd \|\| …` | BUILD-FAIL: `error CS1718: Comparison made to same variable` (warnings are errors; `F2-M3a-buildfail.txt`) → re-formed as F2-M3a2 | — |
| F2-M3a2 | W-R2-2 reverted (the overlap refusal never throws) | KILLED (2) | `ACampaignFormation_FightsOneBattleAtATime_…(−1)`, `TwoBattlesEndingAtOneInstant_…` |
| F2-M3b | boundary: a battle starting exactly at another's end is refused | KILLED (1) | `ACampaignFormation_FightsOneBattleAtATime_…(0)` |
| F2-M3c | two battles ending at one instant may share a formation | KILLED (1) | `TwoBattlesEndingAtOneInstant_…` |
| F2-M4 | W-R2-5 reverted (a battle keeps its declared war) | KILLED (1) | `AFieldBattleThatOutlastsItsWar_…(+1)` |
| F2-M5 | W-R2-3 reverted (a self-razed seat makes the leader its own captive) | KILLED (1) | `ALeaderReleasedIntoACity…` |
| F2-M6 | W-R2-9 reverted (no row for a raised siege) | KILLED (3) | `SiegeEndings_EligibilityMatrix_…` (Relieved, Lifted, CapLifted) |
| F2-M6b | a raised siege credited to the besieger | KILLED (3) | `SiegeEndings_EligibilityMatrix_…` (Relieved, Lifted, CapLifted) |
| F2-M7 | a decision names the captivity's war after it ended | KILLED (1) | `ACaptiveLeaderReleasedAfterTheCaptivitysWarEnded_…` |
| F2-M7b | W-R2-9 reverted (a decision names `WarBetween`) | KILLED (2) | `ACaptiveLeaderDecision_NamesTheWarOfTheCaptivity` (both) |
| F2-M8 | W-R2-10 reverted (pending battle inputs not hashed) | KILLED (1) | `AScheduledBattlesInputs_AreInTheStateHash` |

Teeth and aim (ADR-015 §7.2): each killing test asserts a property the system ought to have (DIR5 §4.1 and §4.3, C31–C33, the
Ledger laws) or the lane's stated PROPOSAL rule (L12, L13, L14, the Director-level raze flag); none is a golden-only kill.

### 14.4 Counts, scan, bench (MEASURED, this clone, after the round)

| Solution | Config | Before (9c5f135) | After |
|---|---|---|---|
| M7Proto.slnx | Release | 0 warnings; 1,124 passed, 0 failed, 0 skipped (15 s) | 0 warnings; 1,151 passed, 0 failed, 0 skipped (28 s) |
| M7Proto.slnx | Debug | 0 warnings; 1,124 passed, 0 failed, 0 skipped (33 s) | 0 warnings; 1,151 passed, 0 failed, 0 skipped (59 s) |

Wall times are indicative only (four CPUs shared with other tracks under `nice -n 19`). M7Econ.slnx and M7DetMath.slnx: not run;
`git diff --stat 0041f68..HEAD` over their closures (Proto.M7.Econ, Proto.DetMath, their tests, Sim.*) is empty. Determinism scan
(`scripts/check-proto-determinism.sh`): CLEAN, 92 files (`final-detscan.txt`).

Bench `outcomes 64 2000 4000 3` on the final tree (`bench-outcomes.txt`): churn median 1,545 ms (≈ 386 µs per capture), 3 reps with
an identical hash `430914157F09C5A8`; record export 11 tables, 1,102,588 bytes (byte count unchanged), 1.7 ms; collapse 0.07 ms. It
is like-for-like with the round-1 re-run (the same churn, war −1), NOT with the first run (§11). **The hash moved** from
`A79BA69B255CE983`; attribution MEASURED: the same tree with the one new state-hash term `L(_siegeEndSeen)` removed gives
`A79BA69B255CE983` again (`bench-attribution-without-siegeEndSeen.txt`); the churn has no field battle, so the other new hash terms
add nothing there. No test golden moved.

### 14.5 Not done / new observations

- DQ10 (C24 against the brief's dead flows) is the Director's; the collapse is unchanged.
- Limitation 19 (a field battle's location) and limitation 20 (only lane S Cities must be bound, under DQ7's reading) are stated,
  not built.
- L14 (a self-razed seat relocates the leader to the lowest-id city) is my PROPOSAL; refusing the raze of one's own leader's seat
  would be the alternative.
- A raised siege's row has no casualty rows: lane S's loss sinks are shared by both sides (limitation 4 still holds).
- A captive-leader decision after the captivity's war has ended is recorded with no war; the fate row still names the captor
  (`ByPolity`, `HeldBy`), so the link to the captivity is in the fate table (its captive row names the war).
