# ADR-031 — AGES AS COMPUTED STATE, EXPLICIT AGE ADVANCEMENT, AGE SURGE PLUMBING, UNIT FAMILIES AND MODERNIZATION

**Status:** ACCEPTED for implementation under the Director's 2026-10-02 pass (D-047 Phases 2 and 3). Contract
changes listed in §2 await the Director's acceptance ruling with PR #10; nothing is merged.

**Implements:** D-047 rulings 12 (AGE-MS-1, the thin evaluator), 13 (explicit advancement, next-turn effect, delay
allowed), 14 (surge emphasis chosen by the player — plumbing only), 18 (automatic, free modernization) and the
Part 2 / Part 3 requirements (unit-family schema, modernization, no-successor handling).
Ledger §10 (Ages), §17.4 (armies), §2.3 (no God objects), §2.4 (one order pathway), §26, §27.
D-043 A1–A5, A8, B5 (`origin/claude/civdemo-work-b1z2y4`).

**Touches the kernel contract:** schema v26 → **v28**, two systems (SystemId 25, 26), one order kind (7), five
tables. This record is the ADR the constitution requires for a touched contract.

---

## §1 — THE READING OF "AGE" THIS IMPLEMENTS (Law 3 / Law 4 conformance)

**An Age is computed state derived from milestones, never a calendar.** A polity's Age changes only when (a) its
next Age's entry requirements evaluate true over state the owning systems publish, and (b) the polity issues an
explicit AdvanceAge order. No date, era band (`era-pacing.json`), turn number or research node's `age` tag is
read by any rule in this record; the turn number appears only as a stamp on log rows.

- **Law 4 (no calendar gates).** Satisfied in the strict sense: the Age is a predicate over computed state plus a
  decision, exactly the "computed state" reading that CR-017 §1.3 lists. Nothing is gated by the Age in this pass
  except the ruled consequence in the next bullet; research availability still never reads the Age
  (D-044 R13; ledger §26 item 6).
- **The Age IS read as an input in one place: modernization.** A formation's identity changes because its owner
  entered a new Age. That is the Age-as-input collision CR-017 §1 records (frozen Law 4, Spine S2) and the
  "free modernization" collision of CR-017 §4 (Law 1, Law 3, D-011 §2 equipment). **Director rulings 12–18 of
  D-047 rule these collisions** (`docs/d047-civ6-reference-and-progression-rulings.md`, Part A rulings 12, 13,
  14, 18 and Part B's closure table: ruling 18 "nothing reopened: automatic, free, at Age transition"). This
  record implements the ruling and does not re-argue the CR. CR-017 itself is UNMERGED (branch
  `claude/civdemo-work-b1z2y4`) and is not edited here.
- **CR-017 §3 (Law 3: "several turns").** The surge's duration is the per-turn shape CR-017 §3 flags. It is NOT
  implemented: the surge is stored (emphasis + start turn) and has no numeric effect, so no per-turn quantity
  exists to conflict with Law 3. Whoever implements the surge formula owes the dt treatment (ledger §27 defers
  formula and duration; D-043 DD-14).
- **Law 3 (dt) generally.** Eligibility is a predicate over state; advancement and modernization are discrete
  events. Nothing here is a rate, so nothing integrates `dtYears`.
- **Law 1 (conservation).** A formation row carries no people and no goods. Founding a formation draws nothing
  from the buckets or stocks; modernization creates and destroys no conserved stock. Equipment in
  `unit-families.json` is declarative. A test pins that a founded world's buckets, stocks and ledger flows are
  bit-identical with and without unit-family content.
- **Law 2 (mechanisms over modifiers).** No Age-keyed coefficient, modifier table or surge multiplier exists
  (D-043 A7/DD-13; ledger §10.5, §26 item 23).
- **Law 5 (determinism).** No RNG, no dictionary iteration, no LINQ in sim code; polities in roster order,
  formations in table order, the first valid order in log order.
- **Law 6 (isolation).** The two systems never reference each other or any other system. Both call the pure
  `AgeQuery` statics (the ResearchQuery precedent), which read published tables only.
- **Ledger §10.3 (global dt).** Untouched: the Age does not drive dt in this pass. Exact Age-to-dt values stay
  deferred (§27); `EraTable` is unchanged.

## §2 — CONTRACT CHANGES

### 2.1 Schema v28 (v27 skipped on purpose)

`CanonicalSchema.Version` 26 → 28. v27 is reserved by the v26 note for the unmerged `m5-full-build` TaxPolicies
rebase, so every version number keeps exactly one meaning. Appended after `ResearchExposures`, in this order:

| # | table | row | width | owner |
|---|---|---|---|---|
| 48 | `AgeStates` | `AgeStateRow(Polity, Age, EnteredTurn, Surge, SurgeStartTurn)` | 28 | AgeTransitionSystem |
| 49 | `AgeEligibility` | `AgeEligibilityRow(Polity, NextAge, EvaluatedTurn, CoreMet, CoreTotal, SupportingMet, SupportingRequired, CategoryMask, CategoriesRequired, Eligible)` | 41 | AgeEligibilitySystem |
| 50 | `AgeTransitions` | `AgeTransitionRow(Polity, FromAge, ToAge, Surge, DecisionTurn, EffectiveTurn)` | 32 | AgeTransitionSystem |
| 51 | `MilitaryUnits` | `MilitaryUnitRow(Id, Owner, Family, Identity, Location, X, Y, Experience, Army)` | 48 | AgeTransitionSystem (founded by worldgen) |
| 52 | `UnitConversions` | `UnitConversionRow(Turn, Unit, Owner, FromFamily, FromIdentity, ToFamily, ToIdentity, FromAge, ToAge, Outcome)` | 44 | AgeTransitionSystem |

- **Absence of an `AgeStates` row is the founding Age** (A1). A row is written on a polity's first advance, so every
  existing world, fixture and order log reads correctly without backfill.
- `AgeEligibility` is rebuilt every step (roster order, polities below A9). The facts are read from PREV
  (`EvaluatedTurn` = PREV's turn); the Age evaluated against is the polity's Age at the END of the step (an advance
  applied in the same step is accounted for via the shared `AgeQuery.ResolveAdvance`).
- `MilitaryUnits.Location` is a settlement id or -1 (in the field); `X`/`Y` are terrain pixel coordinates (the
  continuous frame of ruling 17). `Army` 0 = no army. Personnel, supply, movement and Action Capacity are NOT
  modelled (D-011 battle layer and MobileAgent movement are later work).
- Every row type has a populated-table test (`Sim.Tests/State/AgeSchemaTests.cs`): exact `ExpectedLength`,
  bit-exact round trip (−0.0, denormal, NaN payload), hash equality, per-field hash sensitivity, snapshot
  save/load. `SnapshotDiff.Layout` and `WorldStates.StateEquals` carry the five tables.

### 2.2 Order kind 7 — `AdvanceAge`

`TargetId` = the Age being entered (2..9), `Amount` = the chosen surge key (whole number ≥ 1). Load-validated
ranges; eligibility, "is it current + 1" and "does the surge exist" are checked at consumption
(`AgeQuery.CheckAdvance`, the function the UI calls). The first valid AdvanceAge of a polity in log order wins;
any other changes nothing. Kind 5 stays reserved for `m5-full-build`'s SetTaxRate.

**Delivery semantics (turn-exact):** an order stamped turn *t* is consumed by the step *t → t+1*. Every other
system of that step reads PREV, so turn *t* resolves entirely under the old Age (not retroactive — pinned by a
full-pipeline test that strips the Age tables and compares the deciding step with and without the order: hash
equal, research progress/credits equal, i.e. no RP moved or overflowed). The new Age is written into NEXT, so
**state *t+1* is the first in the new Age** (`EnteredTurn = SurgeStartTurn = t+1`, `DecisionTurn = t`), which is
ruling 13's "the next turn begins under the new Age". Pinned: `AdvanceAge_StampedTurnT_…_TurnExact` (ages
observed over 7 states: 1,1,1,1,2,2,2 for an order stamped turn 3).

There is no auto-advance: eligibility never advances anyone (pinned over 12 eligible turns), and an order can
only name current + 1, so an Age never decreases (ledger §10.1).

### 2.3 Systems 25 and 26

Pipeline (`pipeline.json`) appends `ageeligibility`, `agetransition` after `research`. Both are inert without
Age content (toy worlds; configs loaded without `ages.json`). SystemIds 17, 19, 22 stay reserved.

- **AgeEligibilitySystem (25)** — the thin evaluator of ruling 12. Owns only its published summary.
- **AgeTransitionSystem (26)** — consumes AdvanceAge, writes the Age row and transition log, and in the same
  step converts the advancing polity's formations. It is the sole writer of `MilitaryUnits`. **Why one system
  owns Age and formations:** ruling 13 makes modernization part of the transition instant; a second system
  reading PREV `AgeStates` would modernize one turn late. When recruitment or movement arrives, `MilitaryUnits`
  ownership must be re-split under a reviewed record (the SystemCatalog shared-stock precedent).

Neither is a ProgressionSystem (ledger §2.3): the evaluator only combines facts that owning systems publish,
and the transition applies only the ruled consequences of an explicit decision.

### 2.4 Content and loading

- `Sim.Data/content/ages.json` — nine Ages, five categories, per-Age entry requirements (core, supporting,
  `supportingRequired`, `minCategories`), six surge emphases, all **PROVISIONAL** (ledger §27 defers final
  thresholds). Founding Age **A1**, from D-044 T7 ("No node is complete at founding — the reading of the
  Director's 'the game begins in the Stone Age'"). The loader resolves every reference (research node ids, goods,
  projects, classes, unit families) and refuses a Military Realization milestone backed by anything but real
  formations (ledger §10.4).
- Fact kinds and their owners: `research` / `research_count` (ResearchSystem), `population` (Demographics),
  `settlements` (Controls), `structures` (Construction), `good_stock` (GoodStocks), `class_active`
  (ClassMobility), `trade_volume` (TradeArbitrage), `dwellings` (Housing), `formations` (MilitaryUnits). None
  reads the needs/grievance tables (check-read-isolation stays green).
- `Sim.Data/content/unit-families.json` — the twelve families of D-047 Part 2 (light infantry/scout,
  spear/anti-cavalry, heavy/line infantry, ranged infantry, light cavalry, heavy cavalry, ranged cavalry, siege,
  naval combat, naval transport, air, strategic missile forces), each with role, historical emergence, per-Age
  identities, branches, `autoModernize`, composition (army formation vs MobileAgent), `noSuccessor` rule,
  declarative equipment, and `realizedBy` naming the research.json `unit.*` entity where one exists (validated).
  Civ VI / AoE II are cited for the pattern only; no statistics.
- Founding: `baseline.basic_military` is realized as ONE warband per Empire with a capital, stationed there
  (`WorldFounding.FoundInitialFormations`). research.json is not edited (its baseline row still says "not yet
  simulated" — true of combat; the formation is a token).
- `SimConfig` gains `Ages` and `UnitFamilies`; `SimConfigLoader.Load(sim, needs, goods, research, ages,
  unitFamilies)` and `SimConfigLoader.WithProgression(cfg, ages, unitFamilies)`. `DataFiles.OpenAges()`,
  `DataFiles.OpenUnitFamilies()`. The four-stream load is unchanged and leaves both systems inert, so a caller
  that has not switched (Sim.Ui today) behaves exactly as before.

## §3 — MODERNIZATION RULE (ruling 18; Part 3)

`MilitaryQuery.PlanFor(content, unit, toAge)` — the one function both the transition and the preview call:

1. Family does not auto-modernize → preserved (outcome 4).
2. The family's mainline realization at `toAge` is a later identity → converted (outcome 1). Several Ages at once
   jump straight to the realization at the new Age. Branch identities are never targeted; a branch unit converts
   to the next mainline identity.
3. No later realization yet (e.g. the founding warband entering A2; cuirassiers entering A8) → preserved
   (outcome 3) until one exists.
4. The family's line has ENDED (`lineEndsAfterAge`) → its `noSuccessor` rule: `preserve`, or `generic` →
   convert to the named family's realization at `toAge` (outcome 2; spear → heavy infantry at A8, ranged cavalry →
   light cavalry at A8), following the generic chain.
5. An identity unknown to the content (content changed under a save) → preserved, never destroyed.

Id, owner, location, position, experience and army membership are preserved bit-for-bit. Every outcome,
including preserved ones, is logged in `UnitConversions`. Supply/movement state do not exist yet.

## §4 — QUERY API (read-only; the UI must not write)

- `AgeQuery.CurrentAge / NextAge / StateRow / Status / Evaluate / EvaluateFor / IsEligible / Milestone / Observe /
  SurgeOptions / Transitions / CheckAdvance / AdvanceOrder / PendingAdvance / ResolveAdvance`.
- `MilitaryQuery.Units / Conversions / PlanFor / Plan / ModernizationPreview / Summarize / FamilyLineChanges`.
- AI: `AgeAdvancePolicy.Decide / OrdersForAi` — returns AdvanceAge ORDERS for AI-commanded polities (advance on the
  first eligible turn, lowest surge key: deterministic, one candidate, no argmax). The session appends them to the
  order log like the player's click; the player is never auto-advanced.

## §5 — GOLDENS

Re-pinned ONCE, measured on this tree. Cause: the v28 layout (five appended tables) and, on founded worlds, the
founding warband rows plus the per-turn `AgeEligibility` rows. **No behaviour moved**: for each golden,
`IntegratedPinAttributionTests.*AgeLayerAlone` / `*V28AgeTrailerAlone` strips the five tables, drops their count
prefixes and returns the pre-ADR-031 pin byte for byte (founded/first-reign/driven controls assert the strip is
non-vacuous). Every older control in that file now strips the Age tables first and returns its original
constant unchanged.

| golden | OLD (3e5647f) | NEW |
|---|---|---|
| toy `SnapshotTests.GoldenHash` (seed 42, 200) | `c7bb78dc…6052e8` | `498635bf3c2673b9b544582e17774381b7a785903d7ed320e2dd8e44ddfd4296` |
| founded `SnapshotTests.FoundedGolden` + `ci.yml FOUNDED_GOLDEN` | `e4279f65…fdfbb18` | `15c63d6564ff8092cae67bc90518b525655a6a38f67723beb44930aa8af83fcd` |
| `FirstReignTests` turn 40 | `4291b3e1…51d62db7` | `259c13cf27ea3bed62cd8a0018469a85858b51f546486acb8046dc3577014050` |
| `DrivenGoldenTests` seed 42, 300 | `464aac3d…5d70ae47` | `f94b01eb509853e252a399e103c8d82d7939013d3cca78419b090cc085e2083f` |

The toy value is derived twice (test harness and `sim run --seed 42 --turns 200`). `ci.yml`'s FOUNDED_GOLDEN is
updated in the same commit (CiPinAgreementTests).

## §6 — NOT DONE HERE (and not to be filled with invented constants)

Surge formula/magnitude/duration; generic per-Age effects (DD-13 hold); Age-to-dt; recruitment, movement, ZOC
(ruling 17), supply, combat statistics; special people (rulings 15–16); final milestone thresholds.
