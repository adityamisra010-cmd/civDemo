# M4 — EXIT ARTIFACT INVENTORY (T4.15, first pass)

**Inventory only. Nothing here changes implementation, corridors, quarantines or goldens.**
Cross-checked against the tree at `main` `8586863`, not against queue summaries. Branch merge status
is `git merge-base --is-ancestor <branch> origin/main`, not memory.

---

## §1 PACKET STATUS

| packet | status | evidence |
|---|---|---|
| **T4.1** foundations audit (+ b–g sub-packets) | **DONE** | all seven branches merged |
| **T4.2** store bounding | **DONE** | `331162d8` merged |
| **T4.3** claim model | **DONE** | `c1fd78a6` merged |
| **T4.4** colonization / land clearance | **BLOCKED** | no branch; held on its own spacing item |
| **T4.5** non-state peoples | **DONE** | `0d096b62` merged |
| **T4.6** trade & foreign trade | **DIRECTOR DECISION REQUIRED** | decision inventory on `t4.6-decision-inventory` (`d8636304`), unmerged; "foreign" has no mechanical referent — no polity carrier, money deferred |
| **T4.7** transport | **DONE** | `81236bec` merged |
| **T4.8** notables (structural) | **DONE** | `aed73932` merged; AutoResolver DEFERRED TO M6 |
| **T4.9** lattice stride ruling | **DONE** (docs-only ruling) | `af398397` merged |
| **T4.10 / T4.12** migrated work | **PENDING REVIEW** | `b57e798b` unmerged; investigation complete, corridor disposition is the open director decision |
| **T4.11** merchants | **BLOCKED — precondition measured absent** | see §4 |
| **T4.13** comfort-as-stock | **READY FOR CERTIFICATION** | `4b1bd821` unmerged; `docs/t4.13-review-record.md` |
| **T4.14** M3 observations | **DONE** | `a0efbdb5` merged |
| **T4.15** exit artifact | **IN PROGRESS** | this document |
| **T4.16** clone architecture ADR-020 | **DONE** (design only) | `bd16e37c` merged; copy-on-write withdrawn |

## §2 SCHEMA AND GOLDEN STATE

- Schema on `main`: **v21**. T4.13 takes it to **v22** when certified (`HouseholdGoodsRow`).
- Behavioural goldens on `main`: driven 300, founded 300 (+ `ci.yml` `FOUNDED_GOLDEN` mirror),
  FirstReign. Control: `GoldenHash_Seed42Turn200`, synthetic `Genesis(23)`, no terrain.
- T4.13 moves the three behavioural pins twice (schema, then behaviour) with the control unmoved —
  separable by evidence, recorded OLD → NEW → CAUSE at each pin.

## §3 CERTIFIED BASELINE REDS ON `main` — 6, all intentional

| test | why |
|---|---|
| `Canonical_FedCorridors_AllInBand(seed 1)` | density quarantine reporting its own resolution |
| `Canonical_FedCorridors_AllInBand(seed 2)` | same |
| `Dev_MalthusCorridors_AllInBand(seed 42)` | obsolete `starvedTotal == 0` premise tooth |
| `Dev_MalthusCorridors_AllInBand(seed 7)` | same |
| `Artisans_EmergeInFedAutoplay_…` | CR-003 quarantine tripwire, self-reporting resolved |
| `Famine_DrainsArtisansBeforePeasantStarvationPeaks` | pre-existing baseline |

**These are not implementation regressions and must not be treated as such.** Any packet is
measured against this exact set.

## §4 T4.11 MERCHANTS — DEPENDENCY AUDIT (complete)

**What it requires** (`m4-spec.md`:272): *"the class that emerges on trade volume."*

| dependency | genuinely satisfied? | evidence |
|---|---|---|
| emergence machinery | **YES — fully general, no new mechanism needed** | D-020: `registries.classes[].emerge` is a data-driven predicate string over published `Variables`, parsed by `Predicate.Parse`, latched with hysteresis. Adding a class is a DATA entry plus a published variable |
| a carrier for trade volume | **YES** | `TradeFlowRow(From, To, Good, Quantity)`, owned by `TradeArbitrageSystem` (T3.6, D-034), merged and in the pipeline |
| a published `trade_volume` variable | **NO** | `Variables` has exactly three entries: `food_surplus_ratio`, `artisan_share`, `population`. `Variables.cs` already anticipates this and says **"Queued."** |
| **trade volume actually existing** | **NO — MEASURED ABSENT** | canonical seed 1, 650 turns: `tradeQty = 0` at turns 5, 50, 150 and 650, and **one row of quantity 5 at turn 300**. Five units of trade in a 650-turn campaign |

**Verdict: BLOCKED, and the blocker is empirical rather than a design gap.** The class machinery is
ready and the volume carrier exists; what is missing is volume. Note this is **not** specifically the
T4.6 foreign-trade dependency the spec names — *domestic* inter-settlement trade is also effectively
dead, which points at the transport deadband recorded in `docs/queue.md` (escalation 1), not at
T4.11. **T4.11 should not be opened as implementation or as a design packet until trade volume is
non-trivial**; whoever owns the trade escalations owns unblocking it.

Also load-bearing when it *is* opened — the registry's own scale-invariance law: *"ANY emergence
predicate that needs scale sensitivity MUST publish an absolute quantity, not another ratio."* A
merchant predicate must therefore read an **absolute** trade volume, not a trade ratio.

## §5 OPEN QUARANTINES (all on `main`, all intact)

| quarantine | site | owner |
|---|---|---|
| `canonical.densityPerArableKm2` | `corridors.json` + `AssertDensityKnownDeviation` | M4 CR-002 packet (= T4.10) |
| `canonical.migrationGrossPerDecade` | `corridors.json` + plain `AssertInBand` | M4 CR-002 packet (= T4.10) |
| CR-003 famine family (4 call sites) | `Cr003Quarantine` in ClassSystem/Population×2/Chronicle | CR-003, disposition T4.10 |
| dev Malthus + dev migration | `CalibrationBatteryTests` | T4.10 |

**Two are still MASKED and unmeasured on `main`:** the canonical migration assert never executes
(the density assert on the line above throws first), and the dev migration quarantine never executes
(the Malthus assert throws first). Recorded, not actioned.

## §6 DEFERRED TO M6

- T4.8 **AutoResolver**, general stats, experience, `BattleSetup`/`BattleOutcome` — explicit director
  deferral; T4.8 shipped the structural lifecycle only.
- Notable **emergence/identity/gameplay** — see `docs/notables-forward-design-inventory.md`.

## §7 GOVERNANCE FINDINGS OUTSTANDING

1. **`CLAUDE.md` line 10 says "Current milestone: M3"** while `docs/m4-spec.md` is merged, T4.x
   packets are landing and an M5 document has been admitted. The pointer `spine-s8` §204 declared
   *"can no longer go stale"* **has gone stale**. The line reserves its own amendment to a milestone
   exit gate, so it is T4.15's to raise and the director's to move. **FINDING ONLY.**
2. **`ConservationAuditor` is blind to cross-quantity accounting holes** (found in T4.13, filed in
   `queue.md`). Any future transformation between conserved quantities needs a paired-flow invariant.
3. **Four §23 citations in `docs/m5-ai-constitution.md` do not resolve** against the tree, one
   misquoting frozen Spine principle 7 ("friction" → "decision quality"). Filed in `queue.md`.

## §8 WHAT M4 EXIT STILL NEEDS

- **Director decision** on `canonical.migrationGrossPerDecade` (cause now fully measured — T4.2's
  store bounding, 6.7× on gross migration by single-variable control).
- **Director certification** of T4.13.
- **Disposition** of T4.10/T4.12 and the quarantines it owns.
- **T4.6** design ruling, or explicit deferral of foreign trade out of M4.
- **T4.4** unblocking or deferral.
- Then version strings, README, nightly sweep, `milestones.md` entry, session brief.
