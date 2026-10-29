# Repeatable training and the Action Capacity contract (M6.10; T6.12) — the H7-02 deliverable

**Status: M6 deliverable, 2026-10-28, under D-066 R2.** The scope is D-F, RULED option 1 on 2026-10-20: "M6 defines the
training/AP contract only. No executable training state, training order or AP-resolution change in M6. The training panel
remains a labelled, non-functional placeholder" ([decision register](m6-decision-register.md)). Action Points / Action
Capacity are M7 work (D-052 §4, RATIFIED; confirmed by D-062).

The contract is the data in `Sim.Data/content/unit-progression.json` → `training` (status `DORMANT`). It is validated by
`UnitProgressionContentLoader` and read by nothing (`UnitProgressionInertTests`). It is the M6 side of
[m7-handoff-register.md](m7-handoff-register.md) H7-02.

**Terms.** This contract uses the ratified D-043 term **Action Capacity (AC)**. D-052 §4 calls it "Action Points"; the unit
charter §6.3 uses AC.

---

## 1. What exists today

Nothing that this contract governs exists:
- no AC field;
- no AC refresh;
- no training state;
- no training order: `OrderKind` holds no training, promotion or movement verb (guard test), and kind 10 stays reserved
  for M7's `MoveFormation`;
- no schema version: v36 belongs to ADR-049, and AC state has no reserved version (D-062 R11; D-066 A11).

The unit card's line "Action points: not simulated" stays true.

## 2. Dormancy

- **Definition.** A carrier is **dormant** in a refresh when its M7 action state is `idle` for the whole refresh. The
  refresh is the strategic turn, or a War Pulse in war (D-043 B8). `idle` is D-043 B1's "current action state" with no
  action; the content's `dormantState` names it, and the loader requires exactly `idle`.
- **Rule: a dormant carrier never improves.** Training has no passive component: `passiveGainPerYear` must be exactly 0
  (rule T1). Improvement is only ever the result of an explicit, ordered, completed training action. This is the
  register's "inactive units never improve accidentally".
- **In M6** nothing improves at all: there is no state to improve.

## 3. Explicit, not passive

`mode` is `explicit` (rule T1 refuses anything else). Training is an action the owner orders. It spends Action Capacity
and runs for a stated duration. A passive mode would make dormant carriers improve with time, which the rule in §2 forbids.

## 4. The data fields (per training profile)

| Field | Meaning | Validation |
|---|---|---|
| `families` | the unit families the profile covers: land army formations, or a mobile-agent family | each exists; a family is in at most one profile (R2) |
| `eligibility` | the conditions that must hold for the order to be accepted (§5) | names from the vocabulary, unique; `action_capacity_available` and `below_cap` always present (T2) |
| `cost.actionCapacityShare` | the share of the carrier's AC for this refresh that the action consumes | in (0, 1] |
| `cost.years` | the action's duration in sim-years. Progress accrues as a rate × `dtYears` (Law 3; unit charter §6.3 multi-turn actions). | > 0 |
| `increment` | the maximum AC gained when one training completes | integer ≥ 1 |
| `cap` | the number of trainings a carrier can complete (trained max AC ≤ base + cap × increment) | 1..10 |
| `diminishing.rule` | `escalating_years`: the k-th training lasts `years × factor^(k−1)` | the one rule (T2) |
| `diminishing.factor` | the escalation factor | in [1, 4] |

The shipped profiles hold illustrative bands (TUNE; no calibration).

| Profile | Covers | AC share | Years | Increment | Cap | Factor |
|---|---|---|---|---|---|---|
| `train.land_formation` "Field exercises" | land families 1–8 | 1.0 | 0.25 | 1 | 2 | 1.5 (second training 0.375 y) |
| `train.reconnaissance` "Fieldcraft" | `reconnaissance` (22, `mobile_agent`) | 1.0 | 0.1 | 1 | 1 | 1.0 |

**Base AC is not in this file.** The S3b plan derives AC max from unit-families content (unit charter §6.3, e.g. "AC 2"
for scouts). That field is M7.1's to add to `unit-families.json`. This contract owns only the trained increment above
it.

## 5. Eligibility and rejections

Every condition has exactly one rejection: the reason a refused order states (rule T3). The loader's vocabulary is
`UnitProgressionContract.TrainingConditions`. A test pins this table to it, and to the shipped rejection codes, row for row.

<!-- contract:conditions -->
| Condition | Rejection code | Holds when | What M7 reads to decide it |
|---|---|---|---|
| `owned` | `not_owner` | the order's issuer owns the carrier | the formation row's `Owner` |
| `stationed_at_controlled_settlement` | `not_stationed` | the carrier is stationed at a settlement its owner controls | `Location` and `Controls` (the ADR-055 re-equip precedent) |
| `not_reequipping` | `reequipping` | no re-equipment is pending for it | `Reequips` (ADR-055 §6) |
| `not_engaged` | `engaged` | it is in no battle and no siege | the M7 battle and siege state (M7.4) |
| `supplied` | `unsupplied` | it is fed: no unfed shortfall | `FormationPersonnelRow.UnfedUnits` (ADR-055 §4); M7.4 supply |
| `action_capacity_available` | `insufficient_action_capacity` | the stated share of this refresh's AC is unspent | the M7 AC state |
| `below_cap` | `at_cap` | it has completed fewer than `cap` trainings | the M7 training state |

## 6. Cost and Law 3: CR-017 §3 stays open

CR-017 §3 (Action Capacity against Law 3) "must be ruled before any M7 movement packet" (D-052 §4). It is not ruled. The
contract is written to hold under either recommended reading:

- **Option B (recommended by CR-017: AC is an agency budget per decision interval).** `actionCapacityShare` is a share of
  that budget. `years` is the physical duration and integrates with `dtYears`.
- **Option A (AC accrues per sim-year × dt).** The share is a share of the AC accrued for the refresh. `years` is
  unchanged.

Neither reading changes a field of this file. The refresh rule itself is M7's (§9).

## 7. The carrier (open input for M7)

The trained increment must live on a carrier:
- a formation: a companion row keyed by formation id, the `FormationPersonnelRow` precedent; or
- the D-043 B1 MobileAgent that holds the AC.

Formations live inside an Army MobileAgent (unit charter §6.1). **Recommendation:** store the increment per formation,
and derive the Army's maximum AC as the minimum over its formations, since an army acts at the pace of its least
capable formation. Mobile-agent families (reconnaissance) carry it on their own agent row. This is decided by M7 with
the AC state.

## 8. Validation rules (the loader; one mutant test each in `UnitProgressionContentTests`)

| Rule | What it checks |
|---|---|
| T1 | `mode` is `explicit`. `passiveGainPerYear` is exactly 0. `dormantState` is `idle`. |
| T2 | Each profile: eligibility names come from the vocabulary, are unique, and include `action_capacity_available` and `below_cap`. The cost share is in (0, 1]. `years` > 0. `increment` ≥ 1. `cap` is in 1..10. The rule is `escalating_years`, with a factor in [1, 4]. |
| T3 | One rejection per condition. Codes are unique. Each text is non-empty. Every condition a profile names has a rejection. |
| R1, R2 | Ids and families, shared with the promotion rules ([unit-progression-contract.md](unit-progression-contract.md) §4). |

## 9. What M7 must implement (single owner: M7; H7-02)

1. **AC state and its persistence.**
   - **Where it lives:** an agent-generic table or a companion of the formation (§7).
   - **Schema version:** the next free version at landing. None is reserved.
   - **Tests:** a POPULATED-table test, round trip and hash.
2. **The refresh rule,** after the CR-017 §3 ruling (§6).
3. **The training action:**
   - **The order:** an M7 order kind, never kind 10. It carries a **turn-exact delivery pin**: stamped t, accepted in the
     step t → t+1, progress from the step after acceptance, as in ADR-055's re-equipment.
   - **Progress:** accrues as `dtDays` / `dtYears` (Law 3).
   - **Completion:** raises the carrier's max AC by `increment` and counts toward `cap`.
4. **Rejection paths.** One predicate, shared by the system, the action surface and the AI (the
   `RecruitmentQuery.CheckRecruit` precedent), returning the §5 codes. There is an injected-order rejection test per
   code.
5. **The dormancy property as a test:** a carrier idle for N refreshes has the same max AC.
6. **Replay coverage:** a replay twin, and golden attribution with a switch-off twin.
7. **The functional training panel** (H7-06). It replaces the card's planned-for-M7 line.

## 10. Not done in M6 (and why)

The following is not built in M6, by D-F option 1, D-052 §4 and D-066 R1/R5:
- no AC field, refresh or resolution change;
- no training state, order, action or effect;
- no AI use;
- no panel controls.

The card's single text line "Training — planned for M7" is the whole M6 surface (M6.12).
