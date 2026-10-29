# Unit progression: design note and M7 contract (M6.8; T6.12)

**Status: M6 deliverable, 2026-10-28, under D-066 R2** ("unit progression contracts (contracts and inert placeholders
only)"). The scope is D-E, RULED option 1 on 2026-10-20: "M6 specifies unit-progression semantics, content schema,
validation and the M7 contract. No serialized per-unit progression state in M6"
([decision register](m6-decision-register.md), dated note 2026-10-20). The contract is reviewed by M7 at its spec
(task register M6.8 acceptance). It does not commit, merge or tag anything (D-066 R7).

**Verified base:** detached `0bf7626` plus the M6 base patch (the uncommitted M7 pass and D-066). Every claim in §1 was
read from that tree.

**Companions:**
- [training-ap-contract.md](training-ap-contract.md): M6.10, training and Action Capacity;
- [settler-special-character-disposition.md](settler-special-character-disposition.md): M6.11;
- [m7-handoff-register.md](m7-handoff-register.md): H7-02, H7-05 and H7-06 point here.

---

## 1. What exists in the tree today

- **The formation row.** `MilitaryUnitRow(Id, Owner, Family, Identity, Location, X, Y, Experience, Army)`
  (`Sim.Core/State/WorldState.cs`, the record at `MilitaryUnitRow`).
  - `Experience` is founded at 0.
  - ADR-031 modernization preserves it.
  - ADR-055 §6 keeps it, or multiplies it by `military.reequipExperienceTransfer` (0.35), when a re-equipment completes.
  - **Nothing earns it.** The unit card says so: "Experience: 0.0 - nothing earns it yet".
- **The people of a formation** are `FormationPersonnelRow.Count` (ADR-055 §4, schema v39 §70). It is a conserved
  stock moved only by `Ledger.Transfer` (Law 1).
- **The writer split of `MilitaryUnits`.** The table is a sanctioned shared table, split by field and pipeline order
  `agetransition → military` (`Sim.Core/SystemCatalog.cs`, `AgeTransition` and `Military`).

  | Writer | What it writes | Source |
  |---|---|---|
  | worldgen (`WorldFounding.FoundInitialFormations`) | founds one row per Empire with a capital | ADR-031 |
  | `AgeTransitionSystem` | rewrites `Family` and `Identity` of existing rows at an Age transition (free modernization) | ADR-031 |
  | `MilitarySystem` (SystemId 30, the M7 pass) | appends rows (recruitment, kind 16), removes rows (disbandment, kind 18), rewrites `Family`, `Identity` and `Experience` when a re-equipment completes (kind 17) | ADR-055 §5–§6 (PROPOSED, uncommitted) |

  The XML comment on `SystemCatalog.AgeTransition` still says "SOLE OWNER of MilitaryUnits". That has been stale since
  ADR-055. It is recorded here and left as it is: the M7 pass is preserved and not extended (D-066 R5).
- **None of D-011 §3's formation fields exists:** strength, cohesion, fatigue, ammunition, facing, frontage. There is
  no battle resolver (D-011 §2), no movement, and no Action Capacity (D-052 §4).

## 2. Design: one progression model per class that actually exists

| Class | Exists as | Progression in this contract |
|---|---|---|
| Land army formations: families 1–8 (category A, D-065 R8) | `MilitaryUnits` rows | **Promotions** (this file) and **training** ([training-ap-contract.md](training-ap-contract.md)) |
| Air defence (family 14) | content only (ADR-055) | none yet. Its garrisoned state is work item GA (M7.8). |
| Mobile-agent families: naval, air, reconnaissance … | content only; no agent exists | **training only** (reconnaissance has a dormant profile). There are no promotions: D-011 §3's formation fields belong to formations. |
| Settlers, heroes, special people | an order; or nothing | none. See [the disposition note](settler-special-character-disposition.md). |

Rules this design keeps:

- **No universal XP system.** Each class progresses by its own mechanism, or not at all.
- **No Builder unit.** Construction stays city-level: settlement labour through `ConstructionSystem` and
  `RoadDevelopmentSystem`. This is unit charter §8.3: "No Civ-style builder unit with charges … would double-count
  that labour". A guard test asserts that no `Builder` or worker type exists and that no order kind trains, promotes,
  builds or moves (`UnitProgressionInertTests`).
- **Modernization is not a promotion.** ADR-031 conversion stays automatic and free at Age entry (D-047 ruling 18).
  A promotion never changes a formation's identity or family.

## 3. The contract vocabulary (what promotion content may reference)

A promotion **effect** names one **term** of one D-011 §3 **field**. The term is the coefficient of a named M7
resolution equation, and the promotion's `scale` multiplies that coefficient inside the equation (Law 2: a coefficient
inside a named equation, never a free-floating permanent buff). The loader's vocabulary is
`UnitProgressionContract.Terms`. A test pins the table below to it row for row, so the two cannot drift.

### 3.1 Fields and terms

<!-- contract:terms -->
| Field | Term | The M7 equation it scales (D-011 §1–§3) | A scale below 1 / above 1 means |
|---|---|---|---|
| `strength` | `losses_taken` | the paired-Lanchester attrition this formation receives (melee and fire). The casualties are still debited from real people by `Ledger` (D-011 §2, Law 1). | fewer / more casualties taken |
| `strength` | `melee_effect` | the coefficient of the casualties this formation inflicts in melee | weaker / stronger melee |
| `strength` | `fire_effect` | the coefficient of the casualties this formation inflicts by ranged fire | weaker / stronger fire |
| `cohesion` | `loss_rate` | cohesion lost per unit of casualty fraction | steadier / brittler |
| `cohesion` | `recovery_rate` | cohesion regained per sim-year at rest (Law 3) | slower / faster rally |
| `cohesion` | `rout_threshold` | the cohesion level below which the formation routs | routs later / sooner |
| `fatigue` | `accrual_rate` | fatigue gained per pulse of action | tires slower / faster |
| `fatigue` | `recovery_rate` | fatigue shed per sim-year at rest (Law 3) | slower / faster recovery |
| `ammunition` | `capacity` | the ammunition carried into battle, where the era makes it relevant | less / more carried |
| `ammunition` | `expenditure_rate` | ammunition spent per volley | thriftier / more wasteful |
| `facing` | `turn_time` | the time to change facing | quicker / slower to turn |
| `frontage` | `reform_time` | the time to change frontage (re-form) | quicker / slower to re-form |

### 3.2 Names a promotion may not target

The loader refuses each of these names with the reason below.

<!-- contract:refused -->
| Name | Why a promotion may not target it |
|---|---|
| `action_capacity` | Maximum Action Capacity is raised only by explicit training ([training-ap-contract.md](training-ap-contract.md); H7-02). A second source would be a second source of truth. |
| `experience` | Experience is the input to *earning* a promotion (`MilitaryUnitRow.Experience`), never a promotion's output. |
| `personnel` | A formation's people are a conserved stock (Law 1). They move only by `Ledger.Transfer`. |
| `movement` | The movement budget is M7 movement content (unit charter §6.2), not a D-011 §3 formation field. |

### 3.3 Effect semantics (binding on M7)

- **Multiplicative and inside the term.**
  - `scale` multiplies the named term's coefficient in its equation. There are no additive flat bonuses.
  - Rate terms (`recovery_rate`, and every per-year quantity) stay per sim-year and integrate with `dtYears` (Law 3).
- **Repeatable promotions compound.** A promotion taken `n` times contributes `scale^n`. The loader bounds
  `scale^maxTimes` to [0.5, 2.0] (`UnitProgressionContract.MinCompound` / `MaxCompound`), so no repeatable entry can
  grow without limit. `scale` is never 1, since a no-op is not a promotion.
- **One source per stat.** A (field, term) pair is granted by exactly one promotion in the whole file, within and
  across trees (rule R4). A family belongs to at most one tree (rule R2). So no stat can reach a formation from two
  places, and the content audit (M6.16, H7-08) can prove "no effect applied twice" from the data alone. M7 may relax R4
  only together with a written stacking rule.
- **Experience is the earning input.** How experience accrues (D-011 §2 `BattleOutcome`) and how much of it a
  promotion takes are M7's earning rule (§5.3). Content carries no threshold, so M6 invents no number nothing can
  reach.

## 4. What M6 adds: content and a validator, nothing else

| Deliverable | Where | Inert how |
|---|---|---|
| Promotion trees: three trees, twelve promotions, covering 7 land families | `Sim.Data/content/unit-progression.json` (`trees`, schema 1, status `INERT`) | no system, query, order, AI policy or save table reads it (source guard) |
| Training / Action Capacity data (M6.10) | the same file (`training`, status `DORMANT`) | the same |
| The loader and its rules | `Sim.Core/Systems/Ages/UnitProgressionContent.cs` (`UnitProgressionContentLoader`) | loads and validates. `SimConfig` carries no member of its types (test) |
| Tests | `Sim.Tests/Military/UnitProgressionContentTests.cs` (populated read-back and one mutant per rule), `UnitProgressionInertTests.cs` (source guard, doc pins, builder guard) | — |
| CI and selection | `.github/workflows/ci.yml` (named step), `scripts/validation-map.json` (category `unit-progression`) | — |
| The unit card's planned-for-M7 line (M6.12) | `Sim.Ui/ViewModel/UnitTrainingPlaceholder.cs` | text only (see the task register M6.12 and `UnitTrainingPlaceholderTests`) |

**Loader rules** (`UnitProgressionContentLoader`). Each rule has a mutant test that must be refused with that rule's
message.

| Rule | What it checks |
|---|---|
| R1 | Ids carry their prefix (`tree.`, `promo.`, `train.`) and are unique file-wide. |
| R2 | Every family exists in `unit-families.json`. A tree covers army formations only. A family is in at most one tree, and at most one training profile. |
| R3 | Every effect names a (field, term) of §3.1. The §3.2 names are refused with their reason. |
| R4 | No stat is duplicated within or across trees. |
| R5 | Prerequisites exist in the same tree, are not the promotion itself, are not listed twice, and are acyclic. |
| R6 | An exclusive group has two or more members, all in one tree. No member requires another member of its group. No promotion requires two members of one group, since it could never be earned. |
| R7 | `maxTimes` is in 1..`repeatCap`, and `repeatCap` is in 1..10. `scale` is finite, greater than 0, and not 1. The compounded coefficient stays within bounds. |
| T1–T3 | Training rules: see [training-ap-contract.md](training-ap-contract.md) §8. |

**What M6 does not add** (D-E option 1; D-066 A11, "'No per-unit state' reads as 'M6 packets add none'"):
- no state, no table and no schema version;
- no `MilitaryUnits` writer, and no new `MilitaryUnitRow` field (pinned by reflection);
- no order kind, and kind 10 stays unassigned;
- no effect, and no AI use.

v36 belongs to ADR-049. Action Points have no reserved schema version (D-062 R11; D-066 A11).

## 5. What M7 must add (single owner: M7)

1. **The D-011 §3 fields and the equations each §3.1 term names.** The AutoResolver and TacticalResolver are M7.4 in the
   unit charter §14. Each term gets exactly one call site, and that call site is where a held promotion's coefficient
   enters. A test per term shows the term moving and nothing else moving.
2. **Promotion state.**
   - **What it holds:** each formation's held promotions and how often each was taken.
   - **Where it lives:** a companion table keyed by formation id. This follows the ADR-055 `FormationPersonnelRow`
     precedent, which keeps `MilitaryUnitRow`'s v28 layout so every readable older stream stays a byte prefix (D-056).
   - **Writer:** the owning M7 system. The `MilitaryUnits` writer split is not widened for it.
   - **Schema version:** the next free version at landing ("first to land", D-H/D-062). None is reserved.
   - **Tests:** a POPULATED-table test (exact expected length, bit-exact round trip, hash equality) and a replay twin.
3. **Earning.**
   - **Experience accrual:** the rule that raises `Experience` from a `BattleOutcome` (D-011 §2).
   - **Thresholds:** the experience a promotion takes. It is a TUNE band, set by measurement at M7.
   - **The choice:** an M7 order kind, never kind 10, which is `MoveFormation`. It carries a turn-exact delivery pin.
   - **The AI's choice:** a composite (score, id) key with a tie-dense test.
4. **Application and refusal.** The static rules R5–R7 become order validation:
   - a prerequisite not held;
   - an exclusive sibling held;
   - `maxTimes` reached.

   Each is a named rejection, decided by one predicate shared by the system, the UI and the AI (the
   `RecruitmentQuery.CheckRecruit` precedent, ADR-055 §5).
5. **Lifecycle edges.**
   - Promotions are preserved across ADR-031 modernization (the same family keeps the same tree).
   - Disbandment removes them with the row.
   - Their fate on a re-equipment to another family (ADR-055 class (a)) is an **open input for M7**. The
     recommendation is to drop them, logged, in parallel with the experience transfer fraction.
6. **The functional training/promotion panel** (H7-06). It replaces the card's planned-for-M7 line.

## 6. Open inputs owed to M7 (not decided here)

- A stacking rule beyond multiplicative compounding, if R4 is ever relaxed.
- The experience thresholds and the earning rule (D-011 §2).
- Promotions on a role-change re-equipment (§5.5).
- Whether mobile-agent families (naval, air) get trees once their own fields exist.
- CR-017 §3 (Action Capacity against Law 3). It reaches training, not promotions
  ([training-ap-contract.md](training-ap-contract.md) §6).

## 7. Decisions taken in M6.8 / M6.9 (D-066 decision rules: simplest, deterministic, testable, reversible)

| Decision | Rationale |
|---|---|
| One content file `unit-progression.json` for promotions and training, with one loader | Both are D-E/D-F content with the same inert status and one source guard. It is one file to audit (M6.16). |
| Promotions target D-011 §3 fields only, and only army formations carry trees | D-011 §3 lists those fields "for formations". A field invented for mobile agents would be a modifier without a carrier (the D-035-C test). |
| "No stat duplicated" read as: a (field, term) is granted once in the whole file, and a family is in at most one tree | This is the literal reading of the task register ("no duplicate stat, no second source of truth"). It makes "no effect applied twice" provable from data. It is reversible: relax R4 with a stacking rule. |
| A multiplicative `scale`, with compounded bounds [0.5, 2.0] | It is a coefficient inside an equation (Law 2). The bounds stop a repeatable entry from becoming an unbounded buff. |
| No experience threshold in content | Nothing can earn experience before M7. A threshold now would be a number with no measured basis. |
| The loader lives in `Sim.Core/Systems/Ages` next to `UnitFamilyContent`; the UI loads it without the family cross-check | This follows the loader's precedent. The tests run the family cross-check, so the UI needs no `SimConfig` member, which keeps it inert by construction. |
| Two training profiles, land formations and reconnaissance | They show both carriers (formation and mobile agent) without inventing more. |

## References

- D-066 R2, R5, R7, A2 item 4, A11 (`../d066-m6-closure-rulings.md`).
- D-E and D-F (`m6-decision-register.md`, dated note 2026-10-20).
- D-011 §1–§3 (`../d011-battle-layer-addendum.md`).
- D-043 B1–B3 and D-047 ruling 18 (`../d043-civilization-progression-ages-and-mobile-agents.md`).
- D-052 §4.
- D-065 R7–R11 (`../d065-m7-charter-rulings.md`).
- ADR-031, ADR-055 (`../adr/adr-055-m7-recruitment-personnel-reequip.md`).
- Unit charter §6.1, §6.3, §8.3, §14 (`../m7-plan/unit-catalogue-and-progression.md`).
- Task register M6.8–M6.12 (`m6-task-register.md`).
