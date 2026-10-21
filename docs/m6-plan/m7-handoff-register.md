# M7 handoff register — what M6 provides, what M7 implements

**Status: PROPOSED (planning input, 2026-10-20, tree `dc83a67`).** M7 is the Battle Layer: it "CONSUMES M6's
authoritative technology/capability state. No parallel tech vocabulary inside the battle layer, and no 'generic battle
now, tech later'" (`docs/milestones.md:53-58`, RECORDED under the 2026-10-03 rebase). D-011 (frozen, `spine-s8:16`)
owns battle mechanics; D-052 §4 (RATIFIED) assigns movement and Action Points to M7. Ownership below is single: one
column says what M6 delivers, the other what M7 builds; nothing is shared.

| ID | Item | M6 must provide | M7 must implement | Governing sources | Rulings owed first |
|---|---|---|---|---|---|
| H7-01 | Formation movement (`MoveFormation`, order kind 10; m5w S3a/S3c) | Leave kind 10 reserved (`Sim.Core/Kernel/OrderLog.cs:157`); the fog seam's `Visible(node)` / `Known(node)` queries over stored extent (M6.13) that a move's legality and sight will read; no second position table | The order, its validation on PREV via the route query, the writer re-split of `MilitaryUnits` (`AgeTransitionSystem.cs:43-45`), right-click move UI | `d052:46-51`; `m5w-map-workstream-audit-and-plan.md:812-878`; `d011` §1 | CR-017 §5 (off-road vs D-009) |
| H7-02 | Action Points / Action Capacity (S3b; schema reserved v36) | The **interface contract** only: which content fields a family/identity carries for AC (plan: "AC max is derived from unit-families content"), the refresh rule as a named open input, and — if the Director takes the 2026-10-20 proposal — the *repeatable training* contract: what "dormant" means, eligibility, cost, increment, cap, diminishing returns, as data and validation with **no state and no effect in M6** | AC state and its persistence (v36 or an agent-generic `MobileAgentState` table), the refresh rule, the training action that raises max AC, its rejection paths, replay coverage | `d052:46-51`; `d055:1306-1308` (v35 S2a, v36 S3); `cr-017:289-321` (§3 options, rec. B); D-043 B2 `d043:390-405` | CR-017 §3 (Action Capacity vs Law 3); Decision D-F in `m6-decision-register.md` |
| H7-03 | Recruitment, headcount, military realization | The capability vocabulary M7 reads: which research/civic nodes gate which unit identities (the 23 `unit.*` entities stay DEFERRED, tier E), validated by the corpus audit (M6.16) | Recruitment from real cohorts via `Ledger.Transfer`, headcount on `MilitaryUnitRow` or a roster table, the unit-display count that D-052 §7 asks for, the Age military milestones in `ages.json` marked pending | `milestones.md:53-58`; `adr-033:151-158` (D7 ESCALATED); `d052:89-90`; `deferred-entity-realization-plan.md:57-63` | — |
| H7-04 | Battle resolution (`IBattleResolver`, Auto + Tactical, WEGO pulses, parity suite) | Nothing new; the technology state it consumes (equipment identities, Age forms) | Everything in D-011 §1–§3 and §8; D-012/D-013 close at the M7 spec | `d011:27-61, 84-86` | D-011 §8 decisions at the M7 spec |
| H7-05 | Experience and promotions becoming live | The promotion-tree **content schema and validator** (M6.9) if the Director scopes it into M6; `MilitaryUnitRow.Experience` stays a preserved 0.0 — nothing earns it before M7 (`UnitCardModel.cs:157-160`) | The earning rule (D-011 §2 "general experience" via `BattleOutcome`), promotion state and its writer, effects on the D-011 §3 formation fields (strength, cohesion, fatigue …) that do not exist before M7 | `d011:27, 37`; `WorldState.cs:1009-1011`; `d043:300-320` (ADR-031 modernization stays automatic and free) | Decision D-E (progression state in M6?) |
| H7-06 | Unit training panel | A **placeholder entry point** only (M6.12): a panel location that states training is planned for M7, with no buttons that mutate, no provisional logic; follows `UnitCardModel`'s "not simulated" pattern | The functional panel: unit inspection, eligibility, upgrade selection, training actions, AP integration | `UnitCardModel.cs:68-88` (pattern); 2026-10-20 instruction M6.12 | — |
| H7-07 | Command friction, reconnaissance (pointed, era-costed), stale-GHOST display, siege incl. starvation, the campaign layer | The LIVE/GHOST/UNKNOWN **display** and persisted sighting memory moved to M6 by D-052 §5 (`d052:53-66`), scouts per D-039 B6 ruling, the stored known extent | Parts A, D, E of D-039 (command friction, siege, campaign); reconnaissance as an operational verb over M6's visibility state | `d039:7-10, 48-100, 175-181`; `adr-037:22, 43` (M7 placement superseded for GHOST) | D-039 B6 (scouts: class / flag / capability score — "M6's spec owns it") |
| H7-08 | The 23 `unit.*` research entities and castle / bastion / dry_dock / military_medicine | Listed, validated and inert in the corpus (M6.16); their prerequisites inside the tree stay consistent | Their realization (`deferred-entity-realization-plan.md` §E "owned by M7 Battle Layer (do not pull forward)") | `deferred-entity-realization-plan.md:57-63` | — |
| H7-09 | CR-017's remaining collisions (Ages as state, the transition surge, mobile agents, war pulses) | Record which of them M6's Age-integration work touches (M6.3) and raise them before any M6 packet that would | §3 and §5 before any movement packet; the rest when their packets come | `cr-017:3-6, 29-32`; `d052:48-51` | CR-017 rulings |
| H7-10 | MobileAgent (D-043 B1) as the carrier for settlers, scouts, special people | A statement, in the M6 spec, of whether M6's scouts (if a class) are the first MobileAgent — or a notable (D-039 B6) | The agent-generic position/AC tables if M6 did not create them; war pulses (D-043 B8) | `d043:350-405, 548-570`; `cr-017:549-631` (§7 special people, rec. "a notable with a catalogue identity") | CR-017 §7 |
| H7-11 | Settler progression | Nothing: a settler is an instantaneous `FoundSettlement` order (`OrderLog.cs:124-128`, ADR-036) — there is no settler unit to progress. If a settler ever becomes a MobileAgent it is an M7/M8 decision | A settler agent, if ruled, with travel on M7's movement | `adr-036`; `d043:350-388` | Director scope decision (D-E) |
| H7-12 | CR-019 revolt polity vs the T4.5 raider gate | Nothing | Options 2/3 at the military/political milestones | `cr-019:3, 44-50` (DEFERRED to M7/M8) | CR-019 |
| H7-13 | Annexation gameplay, conquest, recovery of revolted settlements; transfer of institutions on conquest | Institution/civic state defined so that transfer rules can be written (M6.4–M6.6: what a civic or government row belongs to) | The conquest path (`MergeInto` has no caller), recovery, transfer | `adr-033:258-259`; `m5-integration-coherence-matrix.md:172-183` | institution-transfer ruling (M7/M8) |

**Boundary rules this register enforces**

- M6 writes no `MilitaryUnits` row field and adds no per-unit state unless the Director rules otherwise in D-E; the
  writer stays `AgeTransitionSystem` (`SystemCatalog.cs:427-429`).
- M6 does not assign order kind 10 and does not take schema v36 (reserved for S3) — see Decision D-H on version
  sequencing if M6's own tables land first.
- Anything in M6 that would *change* how AP, movement or battle resolve is out of scope by D-052 §4 and D-011; M6 may
  define data, requirements and interface contracts, nothing more.


## Dated note — 2026-10-21 (M6.1; appended, the text above is unchanged)

- Unchanged by M6.1. The spec (`docs/m6-spec.md` §3.5 H, §4 T6.12, T6.19) binds M6 to this register's boundary rules;
  D-E/D-F are ruled (option 1). H7-10: the spec recommends no scout class in M6 (D-J), so M7 creates the first MobileAgent.
