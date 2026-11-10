# Support capabilities — PROPOSAL (M7 lane D, 2026-11-09)

**Status: PROPOSAL.** Every formula and every number below is lane D's design under the delegation of D-079 §4 D8 (support
part); none is a Director decision. Placement per D8: effects in M7.9, allocation and costs in M7.14. The prototype is in
`prototypes/m7-sprint1/Proto.M7/Support/` plus a default-off hook in lane E's `Proto.M7/WarEcon/WarEconomyWorld.cs`.

**Authority.**
- D-079 **DR29** (DIR5 §2.12): "Support units are primarily panel-managed capabilities, not physical battlefield entities."
  Medics and supply wagons are examples; "These figures are examples, not approved formulas." "Morale decreases when troops
  participate in combat. Support capabilities modify relevant effects." "Define stacking, allocation, costs, limits and
  formulas. Do not render support capabilities as visible battlefield entities merely because they exist in the management
  panel."
- D-079 §4 **D8** (DELEGATED): "support-capability stacking, allocation, costs, limits and formulas".
- DR14 (economic effects per operational pulse), DR26 ("Do not invent arbitrary costs or duplicate existing resource
  models"), DR23 (attrition and mortality belong to M7.14), D-065 R10 and charter §4.5 (category B, abstract military
  support; §7.11 military supply and §7.12 field medical kept "as design input for the allocation model").

**What the record does not hold.** The DIR5 verbatim working copy was lost (D-079 Provenance; the brief). D-079 quotes DR29's
sentences, not the example figures, so this proposal reconstructs no figure: every value here is lane D's own, labelled.

Labels: **DD** quoted Director decision; **READING** with both alternatives; **PROPOSAL**; **MEASURED** (a test or run in
this lane); **INFERRED**.

---

## 1. The capability set

| Capability | Status | Category (charter §4.5) | Acts on | Prototype |
|---|---|---|---|---|
| **Field medical** (DIR5 example "medics") | implemented | B (D-065 R10; withdrawn key 16) | the casualty split of a battle, the care of the wounded, part of the casualty-driven morale loss | `SupportKind.FieldMedical` |
| **Supply train** (DIR5 example "supply wagons") | implemented | B (D-065 R10; withdrawn key 15) | the army's own train in lane E's logistics: train capacity (ration endurance), supply-line throughput, march speed, fodder | `SupportKind.SupplyTrain` |
| Combat engineers | **PROPOSAL only, not implemented** | B (D-065 R10; withdrawn key 13) | would act on lane S's siege works term and on field fortification and river crossing; no formula is fixed here | — |

No further capability is proposed. Support never becomes a battle entity: it has no position, no deployment, no order and
nothing to render (DR29); the battle sees only its effects on existing terms.

---

## 2. Allocation (PROPOSAL)

### 2.1 Pool, panel and standing targets

A polity holds a **support pool** per kind: medical staff (people), and for the supply train wagons (a conserved stock),
draft animals and drivers (people). The panel issues **allocation orders** (`SupportKinds.OrderAllocate`): "army A shall have
N of kind K". An accepted order replaces that army's **standing target** for the kind (two orders do not add up). A target
of 0 returns the kind's assets.

**Allocation target: armies, not formations. This deviates from the lane brief, which asked for "allocation from a panel to
formations". The deviation was not stated before verification round 1 (D-R1-13).** Support is allocated to an ARMY because:

1. charter §4.5 row B places category-B support in "an allocation row per army or war" (`unit-catalogue-and-progression.md`
   §4.5 table);
2. the only cost and logistics path support may use is lane E's war economy (DR26: "Do not … duplicate existing resource
   models"), and lane E plans per army per War Pulse: train, supply line, pay, morale and the casualty collapse are army
   quantities there;
3. a formation-level allocation would need formation-level logistics and formation membership of armies, which lane E's
   prototype does not have (membership is an M7.3 contract item, D-079 §1.4 item 7).

Alternative (PROPOSAL, not built): **per formation**. Support would follow the formations that fight (for example medics
with the formations of one battle). There would be one allocation row per formation, costs would still settle per army (the
sum over its formations), and the design would need the army-to-formation membership of ADR-055's tables. This choice is
delegated design (D8, allocation in M7.14), so it is presented for review, not for decision.

### 2.2 Per War Pulse, not per battle — why

The target is applied only at the army's next **War Pulse start**, and the latched support holds for the whole pulse:

1. **Costs are per pulse** (DR14): the war economy plans each pulse at its start (lane E, `reports/H-E.md` §2: rations,
   fodder, pay are planned and reserved then); support that eats and is paid must be known at that instant.
2. **One asset, one place**: allocated per battle, one pool could cover every battle of a pulse in turn (the same surgeons in
   two places in one week). Latching per pulse binds an asset to one army for a pulse.
3. **The supply effect is a pulse quantity**: lane E's train capacity and supply-line fleet are computed at the pulse start
   from the army's composition; wagons attached later would not be in that plan.
4. **No mid-pulse shuffling** in the spirit of DR22's exploit prevention (no repeated move-and-return loops).

### 2.3 Delivery, contention and returns (implemented)

- **Delivery (turn-exact, MEASURED by `Allocation_DeliveryPin_ToThePulse`):** orders are delivered at a decision-window
  instant in the PlayerOrder class, after every world event of that instant (the Runner's rule). An order issued 1 ms before
  pulse k starts is in force for pulse k; an order issued AT pulse k's start instant (after k was planned) is in force from
  pulse k + 1; the war's first pulse follows the same rule.
- **Contention (MEASURED by `PoolContention_AtOneInstant_…`):** armies latching at one instant are served in lane E's own
  order, the composite key (Priority, Id); an increase is granted from the pool as far as the pool allows. Tie-dense test:
  three armies at equal priority, a pool too small for all: the lowest id first, whatever the order of the armies in the
  setup; a lower priority value first, whatever the id.
- **Limits** (§4) are applied at every latch on the men present then; an excess is returned to the pool.
- **Returns:** a lowered target returns the difference at the next latch; a disbanded army returns everything (its wounded go
  home to the civilians). MEASURED by `ReturnsAndDisbandment_ConserveEveryCommodity`.
  *Superseded in part (verification round 2, D-R2-02): that test checks conservation and that no wounded remain with the army,
  so sending a disbanded army's wounded to the dead passed it (verifier mutant S8 survived the full suite; reproduced). The
  destination is now pinned by `Disbandment_SendsTheWoundedHome_TheDeadAccountHoldsOnlyTheDead`: lane E's dead account equals
  the recorded deaths exactly (disease, the battle's dead, deaths of wounds, famine) while wounded were still in care when the
  army disbanded.*

| Refusal (`SupportRefusal`, stable codes) | When |
|---|---|
| 0 None | accepted: becomes the standing target |
| 1 UnknownArmy · 2 NotOwner · 3 UnknownKind · 4 NegativeTarget | as named |
| 5 NavalForce | no naval support capability is designed (ships' surgeons and colliers are lane E's own naval terms) |
| 6 ArmyDisbanded | the army no longer exists |

*Superseded in part (verification round 2, D-R2-04 and D-R2-02): the codes did not behave as the table says. Code 4 could not
be produced: a negative B decoded to kind 0 and was refused UnknownKind first. A raw order whose decoded kind lay outside 0..255
(any B ≤ −2^32, any B ≥ 256 × 2^32), or whose A decoded to a polity outside the int range (A ≥ 2^47, or A ≤ −2^47 − 65,536),
threw an OverflowException out of the support world instead of being refused, although an order log is replayed input
(the ranges DERIVED from the decoding; MEASURED at 7 raw B values by verifier probe P_N, reproduced byte for byte, and at 4 raw A
values by this round's probe). Codes 5 and 6 had no test (verifier mutants S6 and S7 survived the full suite). The decoding is
now total and the checks run in this order:*

| Refusal (since round 2) | When (A = Pack(polity, army), B = kind × 2^32 + target) | Recorded |
|---|---|---|
| 1 UnknownArmy | A outside 0..2^47 − 1 (Pack's range, `SupportKinds.MaxPacked`), or no such army | polity and army −1 when A is outside the range |
| 2 NotOwner | the issuing polity does not own the army | — |
| 4 NegativeTarget | B &lt; 0 (under the documented decoding its target part is negative); checked before the kind | kind byte 0, target = B |
| 3 UnknownKind | B ≥ 0 and ⌊B ÷ 2^32⌋ is neither 1 nor 2 | the kind byte when it is 0..255, else 0; target = B mod 2^32 |
| 5 NavalForce, 6 ArmyDisbanded | as in the table above | — |

*Pinned by `AllocationOrders_AnyRawValue_IsRefusedAndRecorded_TheWorldRunsOn` (16 raw orders at one window, each row
checked field by field, and a valid order after them latched) and `Allocation_RefusedForANavalForce_AndForADisbandedArmy`.*

---

## 3. Effects and formulas (PROPOSAL; every effect is a coefficient inside an existing equation)

### 3.1 Field medical

| Term | Formula | Where it acts |
|---|---|---|
| coverage | c = ⌊medical staff × 1000 ÷ men⌋ (staff per 1000 men, at the latch) | — |
| saturation (stacking) | u(c) = c ÷ (c + c₀) | every medical effect; diminishing returns, u &lt; 1 |
| battle save | saved = ⌊dead × S_max × c ÷ (c + c₀)⌋, moved from Dead to Wounded | lane W's collapse split (`BattleCollapse.Split`): exact, the parts still sum to the persons |
| care: return to duty | R = R₀ + R₁ × u(c) | the army's wounded account, per sim-year |
| care: death of wounds | D = D₀ × (1 − K × u(c)) | the same account, per sim-year |
| care flows over dt | lane E's `HeadcountLoss`: one survival product of R + D compounded per day, split pro rata to R and D; thousandth-of-a-head carries persist | recovered → the army's men; dead → the dead account |
| morale restore | restore = ⌊MoraleLoss(dead entities) × saved ÷ dead persons × ρ⌋ | the post-battle morale (§3.3), with the TacticalResolver's own loss formula `ConditionMath.MoraleLoss` |

Without staff (c = 0) the battle save is 0, so the casualty split is **lane W's exactly** (MEASURED:
`Medical_BattleSave_IsLaneWsSplitPlusAnExactDeadToWoundedMove`). The care of the wounded with c = 0 runs at the base rates:
it is M7.14 attrition (DR23) — this proposal supplies the base rates only so that the medical effect has something to modify.

*Verification round 2 (D-R2-02): three rows of the table above had no semantic test, and a mutant of each survived the full suite
(reproduced): the coverage computed from the TARGET instead of the staff granted (S1); the care settled over a fixed week
instead of the elapsed time, Law 3 in the world (S4); the thousandth-of-a-head carries discarded (S5). Now pinned:
`Medical_CoverageIsTheStaffGranted_NotTheTarget_AndTheSaveFollowsIt` (a pool of 20 against a target of 60: coverage 10, save 80
of 400 dead, not 30 and 120) and `Medical_CareSettlements_AreExactOverTheirElapsedTime_AndCarryTheThousandths` (every care row
recomputed over its own interval with the carries chained; the run contains a row that a fixed week would settle differently
and a row that the carries change).*

**What the numbers mean (derived from the PROPOSAL values of §5, exact arithmetic, not a measurement of the world):** at
c = c₀ = 10 staff per 1000 men, u = ½, the battle save is 20 % of the dead (S_max × ½), the return rate 6,000 ‰/yr and the
death rate 900 ‰/yr, so the share of the wounded who eventually die of wounds is 900 ÷ 6,900 ≈ 13 % (against 1,500 ÷ 4,500 ≈
33 % without care) and the mean time in care is about 53 days (against about 81). At c → ∞ the save tends to 40 % and the
death share to 300 ÷ 9,300 ≈ 3 %.

### 3.2 Supply train

Support wagons are **lane E's wagons** (no second supply model): for the pulse the army's composition used by lane E's
logistics is its own plus the latched support (`WarEconomyWorld.SupportedComposition`):

- **ration endurance:** train capacity = wagons × `WagonPayloadGm` + trucks × payload + the rations the army's people carry
  (lane E's `TrainCapacity`, with the support staff counted as carriers too);
- **supply-line throughput:** the fleet term carriers × payload × round trips in the pulse (lane E's
  `ArmyLogistics.Evaluate`) and the carriage mode (a road with wagons is wagon carriage, without them pack carriage);
- **march speed:** an army with wagons and draft animals marches at the ox-train pace (lane E's `BaseSpeed`, slowest
  element): 15 km a day against 20 for foot on open ground. A supply train costs speed — MEASURED and pinned exactly by
  `SupplyTrain_SlowsAFootArmyToTheOxTrainPace`;
- **ammunition endurance:** lane E's munitions are drawn from the polity's stock into the ammunition train at the pulse start
  (no carriage loss in lane E's model), so support wagons do not change ammunition supply in this prototype (limitation).

MEASURED (`SupplyTrain_RunsASupplyLine_ForAnArmyWithoutCarriers`): a halted foot army with no carriers 60 km from its depot
and no locality to forage from is delivered nothing in every pulse; with 150 support wagons lane E's own arithmetic runs a
supply line (deliveries &gt; 0) and its hunger summed over the pulses falls.

### 3.3 Morale: "Morale decreases when troops participate in combat"

**The rule implemented since verification round 2 (PROPOSAL; D-R2-01):** a battle LOWERS the army's campaign morale by its net
morale loss on the TacticalResolver's scale, start − end, less the medical restore (§3.1), never below zero, mapped to micro units
(× 1,000,000 ÷ MoraleMax, floor), and floored at 0: campaign morale after = max(0, before − ⌊max(0, start − end − restore) ×
1,000,000 ÷ MoraleMax⌋) (`SupportMath.MoraleAfterBattle`). **A battle never raises the campaign morale.** Lane E's pulse-end
recovery and penalties then run on it as before. Neither resolver is changed. MEASURED by
`Morale_ABattleNeverRaisesCampaignMorale_ArchersHoldingWithoutLossEndAboveTheirStart` (a real TacticalResolver battle: 60
archers at morale 800 holding against heavy infantry, two seeds; they kill enemies, lose nobody and end at a morale above 800,
yet the campaign morale stays 800,000, with and without medical staff) and `Morale_ABattleLowersCampaignMoraleByItsNetLossOnly`
(six stand-in battles, by hand: an end above the start gives no rise; a start above or below the campaign morale applies the
battle's own loss; a restore larger than the net loss gives no rise; the by-hand case 640,000; the floor at 0).

*Why the round-1 rule below was replaced (verification round 2, D-R2-01; MEASURED, verifier probe P_M reproduced byte for byte):
the TacticalResolver's in-battle recovery (HoldRecovery +20, RallyRecovery +60 per casualty-free pulse, capped at MoraleMax and
not at the start, `TacticalBattle.PulseEnd`) lifts a formation that holds and fights without loss ABOVE its start. With the
battle started at the army's own campaign morale (800 = 800,000), archers holding against 5–20 heavy infantry ended at 880 in 24
of 24 runs (4 seeds × 3 enemy sizes × 0 or 100 medical staff), and the round-1 rule set the campaign morale to 880,000: combat
RAISED it, against DR29's quoted text. So the round-1 note below, which put morale rises down to the unchecked START half only
and named M7.15's start-half enforcement as the remedy, was wrong: enforcing the start would not have prevented this rise; the
after-battle half itself produced it.*

**The round-1 rule (superseded by the rule above; kept as the record):** the army's campaign morale enters a battle as its start morale on the tactical scale
(campaign micro-units ÷ 1000), and after the battle the campaign morale is set to the TacticalResolver's end morale of that
battle (× 1,000,000 ÷ MoraleMax) plus the medical restore. Lane E's pulse-end recovery (600 ‰ of the scale per year when
fed and paid) and penalties (hunger, arrears) then run on it as before. This reuses the TacticalResolver's morale; neither
resolver is changed. MEASURED by `Morale_ACampaignArmyLeavesABattleAtTheTacticalEndMorale_MedicsRestorePart`, which runs a
real TacticalResolver battle (lane TR's asymmetric meeting scenario), collapses it through lane W's `FromTactical`, and pins
the campaign morale after it, with and without medical staff.
*Superseded in part (verification round 1, D-R1-16): only the AFTER-battle half of this rule is implemented. The start half
("enters a battle as its start morale") is an unchecked assumption of the stand-in hand-off: `SupportBattle.MoraleStart` is
only range-checked, never compared with the army's campaign morale, and the campaign morale is set to the battle's end morale
whatever it was before. So a stand-in battle whose end morale lies above the army's campaign morale RAISES it (MEASURED with a
round-1 probe: campaign 800,000 → 900,000 for a battle ending at 900 of 1,000). The test above drives the battle from the
army's own morale, so it does not exercise the gap. Enforcing the start half belongs to the M7.15 battle → campaign hand-off,
which knows the battle's real start (limitation 4).*
*Superseded (verification round 2, D-R2-01): the rises did not come only from the start half; the after-battle half produced
them too (see above). Under the round-2 rule the battle's start enters only through start − end, the battle's own loss, so a
mismatch between the battle's start and the campaign morale can no longer raise the campaign morale (MEASURED: start 1,000 and
end 900 against a campaign morale of 800,000 give 700,000; start 500 and end 450 give 750,000; the round-1 rule gave 900,000
and 450,000). What remains for the M7.15 hand-off is that `MoraleStart` is the battle's real start morale: the stand-in passes it
separately from the collapse, and nothing checks it (limitation 4).*
*Also superseded (verification round 1, D-R1-04): that test computed its expected restore with `SupportMath.MoraleRestore`
itself, so a wrong restore formula passed (verifier mutants V7, ρ ignored, and V8, the wounded entities' loss added, survived
the full suite). The expectation is now recomputed by hand from the TacticalResolver's formula, and
`Morale_MedicalRestore_ByHand_TenDeadOfFifty` pins one battle with literal numbers: loss ⌊10 × 2,000 × 100 ÷ (50 × 100)⌋ =
400; 80 of 400 dead saved; restore ⌊400 × 80 × 500 ÷ (400 × 1,000)⌋ = 40; campaign morale after = 640,000.*

**How support modifies it:** medical staff through the restore (§3.1); the supply train through hunger — lane E's morale
penalty is proportional to the hunger share and its recovery needs a fed army, so better supply means less loss and faster
recovery (an existing coupling, unchanged).

**READING M (both alternatives; applied: (a)):**
- **(a)** "participate in combat" is realised by the casualty-driven loss of the TacticalResolver (troops who fight take
  losses and lose morale; a formation that fights without loss loses none) carried into the campaign. No resolver change.
  *Superseded in part (verification round 2, D-R2-01): as implemented in round 1, "a formation that fights without loss loses
  none" was true and incomplete: it could GAIN, because the carry set the campaign morale to the battle's end morale, which the
  TacticalResolver's in-battle recovery lifts above the start (archers holding without loss: 800 → 880; MEASURED). Under (a) as
  implemented since round 2 the campaign carries the battle's NET loss (start − end, less the medical restore) and never a gain:
  a formation that fights without loss neither loses nor gains campaign morale. Within (a), the in-battle recovery still offsets
  part of a loss suffered earlier in the same battle (the TacticalResolver's own model). Carrying only the gross casualty-driven
  loss instead would need the TacticalResolver to report it (a resolver change, so it belongs with (b)'s costs); not proposed.*
- **(b)** participation itself costs morale even without casualties (a per-battle or per-engaged-pulse decrement). That is a
  new term in BOTH resolvers (a one-sided term would open a parity gap by construction, D-079 D11), with golden moves in the
  parity battery and the tactical tests.

---

## 4. Limits (PROPOSAL)

- Medical staff ≤ ⌊men × 50 ÷ 1000⌋ per army (5 %); support wagons ≤ ⌊men × 100 ÷ 1000⌋ (one per ten men). Applied at every
  latch on the army's men then (MEASURED: `Limits_AndRefusalCodes` checks every latch's limit against lane E's men at the
  pulse start).
- An increase never exceeds the pool; a wagon is granted only with its draft animals (2, lane E's hitch rule) and its driver.
  *Verification round 2 (D-R2-02): this rule had no test (verifier mutants S2 and S3, dropping the animal or the driver check,
  survived the full suite; reproduced). Pinned by `SupplyTrain_AWagonIsGrantedOnlyWithItsTwoDraftAnimalsAndItsDriver`: the
  animals bind (10 wagons, 4 animals: 2 granted), the drivers bind (3 drivers: 3), the wagons bind (10, with 20 animals and 10
  drivers).*
- No support for naval forces; none for a disbanded army; a polity allocates only its own pool to its own armies.

---

## 5. Costs (through lane E's per-pulse settlement) and the PROPOSAL values

Costs are not a new model: the latched support adds to lane E's own terms in its pulse plan (`WarEconomyWorld.OnPulseStart`,
the `[D]` lines), so they are settled per sub-step exactly as lane E settles the army (`RateIntegrator.IntegrateExact`,
transfers through the ledger, exact conservation):

| Cost | Term (lane E's rate) | Value (lane E PROPOSAL table) |
|---|---|---|
| support staff food | (RationBase + RationFieldExtra) per staff-year, + RationMarchExtra while marching | 1,000 + 300 gm/yr; + 600 |
| support staff pay | PayFoot per staff-year (coin) or WageInKindFoot (in kind) | 12 soldier-months/yr; 500 gm/yr |
| support draft animals | DraftHardRest + DraftRoughageAsGrain × (1000 − graze)/1000 per animal-year, + DraftHardWorkExtra while marching | 1,000 + 5,000 × (1 − graze); + 1,500 gm/yr |
| patients (the wounded in care) | RationBase per patient-year | 1,000 gm/yr |

MEASURED and pinned to the unit (`SupportCosts_AreLaneEsRationPayAndFodderTerms_Exactly`): in the first pulse the army's
standing need and pay due equal lane E's arithmetic with the support terms added, recomputed independently in the test.
*Superseded in part (verification round 1, D-R1-02): that test covers a HALTED first pulse with no battle, so it pinned only
the staff's field ration, the animals' rest feed and roughage, and the staff's pay. Four documented terms could each be
deleted with the whole suite passing (verifier mutants V2–V5): the patients' ration, the staff's and the animals' march
extras, and the staff as ration carriers in the train capacity (§3.2). Three tests now recompute each term by hand, so every
cost term in the table above is pinned exactly:*
- *`SupportCosts_MarchExtrasOfStaffAndAnimals_Exactly`: the movement need of a marching first pulse (soldiers', staff's and
  support animals' march extras over the whole pulse);*
- *`SupportCosts_PatientsEatTheBaseRation_Exactly`: the standing need of the pulse after a battle, with patients in care,
  including lane E's integration carry from the pulse before;*
- *`SupportCosts_StaffCarryRationsInTheTrainCapacity_Exactly`: the restock request of an army at its depot (no carriage
  loss), which is lane E's train capacity with the support wagons and the support staff as carriers.*

*Superseded in part (verification round 2, D-R2-02): "every cost term in the table above is pinned exactly" was not true. The
in-kind pay term, "WageInKindFoot (in kind)", had no test: every test polity had coinage (verifier mutant S10, support staff
unpaid without coinage, survived the full suite; reproduced). `SupportCosts_StaffArePaidInKind_WhereThePolityHasNoCoinage` now
pins it (the first pulse's pay due of a polity without coinage, recomputed by hand with the staff's in-kind wage).*

**Coefficients of the support layer (`SupportRates.Proposal`, all PROPOSAL / TUNE):**

| Coefficient | Value | Basis |
|---|---:|---|
| DraftPerWagon | 2 | CITED: lane E's hitch rule ("two per wagon", `ArmyLogistics.Evaluate`) |
| DriversPerWagon | 1 | PROPOSAL |
| MedicsPerThousandMax, WagonsPerThousandMax | 50, 100 | PROPOSAL limits |
| c₀ (`SaturationCoverage`) | 10 staff per 1000 men | PROPOSAL: the coverage at which half the maximum effect is reached |
| S_max (`SaveMaxPpm`) | 400,000 ppm | PROPOSAL |
| R₀, R₁ | 3,000, 6,000 ‰/yr | PROPOSAL (M7.14 attrition placeholders) |
| D₀, K | 1,500 ‰/yr, 800 ‰ | PROPOSAL (M7.14 attrition placeholders) |
| ρ (`MoraleRestorePermille`) | 500 ‰ | PROPOSAL |

Orders of magnitude (general knowledge, **unverified here**): before modern surgery a large share of the wounded died of
wounds and disease, and organised field medicine cut that share substantially; the values above reproduce that shape, not a
sourced figure. **Not costed in the prototype:** raising the pool (recruiting staff from civilians, building wagons — the pool
is an opening input); equipment wear of staff; mortality of support animals and staff (lane E's disease and desertion apply to
the army's men only). These belong to M7.14 with the costs.

---

## 6. Conservation, determinism, persistence (MEASURED)

- Every person, animal and wagon moves by a `WarLedger` transfer between accounts of lane E's ledger: pools, army
  attachments, the wounded, lane E's dead account, a captured sink, the civilians. `Commodity.Vehicles = 13` was appended for
  the wagons (lanes E, S and W appended their codes the same way). Totals of people, animals, wagons, grain, money,
  munitions and kit are equal at every observed instant (`ReturnsAndDisbandment_ConserveEveryCommodity`,
  `Medical_BattleSave_…`, `Medical_CareInTheWorld_…`).
- **Default off:** with empty pools and no orders, lane E's economy is byte-identical — the same pulse rows, events, turn
  rows and the same balance in every account lane E opened (`WithoutSupport_LaneEsEconomyIsByteIdentical`); every lane E and
  lane S test passes unchanged.
- **New serialized rows** (`SupportCodec`, version 1): `SupportOrderRow` (30 bytes), `SupportAllocationRow` (57),
  `SupportCareRow` (52), `SupportBattleRow` (96); POPULATED-table test from a real run: exact lengths, bit-exact round trip,
  hash equality (`Codec_PopulatedTables_RoundTripBitExact_WithExactLengths`).
  *Superseded in part (verification round 1, D-R1-07): the populated run had ArmyId = WarId = 0 in every allocation row, so an
  encoder that swapped the two fields round-tripped (verifier mutant V13 survived the full suite). The run now has a second
  army in the war and a second battle. The codec test also asserts, for every pair of same-typed fields of every row type, that
  some row tells them apart, so no encoder swap of two such fields can round-trip unseen.*
- **Replay:** the logged panel orders re-run on a fresh world give the same state hash and the same pulse rows
  (`Replay_FromTheOrderLog_GivesTheSameStateHash`).

---

## 7. Limitations

1. The care of the wounded and its base rates are M7.14 attrition placeholders; the field-medical design waits on M7.14's
   attrition model to become authoritative.
2. Support staff and support animals are not exposed to lane E's desertion, disease or animal mortality.
3. Ammunition endurance is not affected (lane E delivers munitions without carriage loss); a carriage model for ammunition
   would be lane E's change, not a support formula.
4. The battle stand-in: battles reach the support world as lane W collapse inputs (the M7.15 hand-off is not built); the
   morale coupling assumes the battle started at the army's campaign morale (the test drives it that way).
   *Superseded in part (verification round 2, D-R2-01): since round 2 the coupling carries the battle's net loss (start − end)
   and never a gain, so it no longer needs the battle to start at the campaign morale; it assumes that `MoraleStart` is the
   battle's real start morale, which the stand-in passes separately and nothing checks.*
5. No naval or air support capability; no combat engineers.
6. Pools are opening inputs; raising and disbanding support assets are not modelled.

---

## 8. For the Director

Only one point needs the Director, and it is a reading, applied until corrected:

- **READING M** (§3.3): does "Morale decreases when troops participate in combat" mean the casualty-driven loss the
  TacticalResolver already applies (applied), or an additional participation term in both resolvers? (b)'s consequence: a
  change to both resolvers and moved parity and tactical pins.
  *(Verification round 2, D-R2-01: (a) is now applied as the battle's net loss, never a gain; before round 2 the carry could
  raise the campaign morale through combat, which neither alternative intends. The question to the Director is unchanged.)*

Everything else here (allocation per pulse, stacking, limits, costs and formulas) is delegated (D8) and is presented for
review with M7.9 and M7.14.

## Dated note — 2026-11-10 (lane MOR, D-082 DA1; appended, the text above is unchanged)

- §3.3's rule "A battle never raises the campaign morale" is superseded for the winning side by D-082 DA1 ("Winning a battle
  should raise morale too"; D-082 §3). Since lane MOR the support world settles a battle as DR29's decrease above (the net loss,
  less the medical restore, for every participant) plus, for an army of the side the battle report names the winner, the DA1 rise
  ⌊400,000 × B ÷ (B + W)⌋ (PROPOSAL), in one clamp; the in-battle recovery's gain is still never carried. READING M is narrowed as
  D-082 says: (b), whether taking part alone costs morale, stays OPEN. Design: [morale-proposal.md](morale-proposal.md) §4;
  prototype `Proto.M7/Morale/CampaignMorale.cs`; report `prototypes/m7-sprint1/reports/H-MOR.md`.
- The test `Morale_ABattleNeverRaisesCampaignMorale_ArchersHoldingWithoutLossEndAboveTheirStart` (§3.3) is renamed
  `Morale_TheInBattleGainIsNeverCarried_OnlyDA1RaisesTheWinners_ArchersHoldingWithoutLossEndAboveTheirStart`; its no-winner case
  keeps the numbers above (800,000), and its reported-winner case adds the DA1 rise exactly.
