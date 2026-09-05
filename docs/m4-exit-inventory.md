# M4 EXIT INVENTORY — FINAL CERTIFICATION RECORD

**Certified 2026-09-05.** This is the authoritative M4 exit artifact (T4.15).

| | |
| --- | --- |
| M4 completion commit | **`badef96`** |
| branch | `m4-completion` |
| starting main | `e6cf705` |
| schema | **v24** (unchanged across the whole completion pass) |
| tag | `m4-exit` (repository convention: `m2-exit`, `m3-exit`) |
| status | **FEATURE-COMPLETE. QUALIFIED FOR FINAL BASELINE CERTIFICATION.** |

**Measured against this tree, not recalled.** Where something is dormant, quarantined or unresolved
it says so — an exit artifact that reports only the green parts is worse than none.

---

## 0. CERTIFICATION RESULT

| check | result |
| --- | --- |
| `Sim.Tests` | **632 passed · 2 failed · 6 skipped** |
| the 2 failures | `Dev_MalthusCorridors_AllInBand` seeds 7 and 42 — **explicitly quarantined by ruling (CR-003 §8.2)** |
| `Sim.Ui.Tests` | **151 / 151** |
| Release build | **0 warnings** |
| determinism | **PASS** |
| replay | **PASS** |
| save/load | **PASS** |
| schema | **PASS** (v24, no row type/field/table joined or left the stream) |
| `check-banned-constructs` | **PASS** |
| `check-read-isolation` | **PASS** |
| `check-readonly-proof` | **PASS** |

**THE TWO RED TESTS ARE NOT A CERTIFICATION FAILURE.** They are the expected output of CR-003's
standing ruling that the Malthus crash family stays quarantined with its bands frozen. CR-003 §7.6
anticipated the exact message they emit. Measured here: 4 and 3 starvation deaths over 1,000 turns;
`crashCount` still 0/20. Isolated starvation is not the crash cycle the quarantine lifts on.

---

## 0A. FINAL M4 AUDIT — EVERY REQUIREMENT, CLASSIFIED

Classification: **A** implemented and certified · **B** implemented but conditionally dormant ·
**C** explicitly quarantined by ruling · **D** deferred to a later milestone · **E** known
broken/incomplete dependency, explicitly deferred.

| requirement | class | evidence / condition |
| --- | --- | --- |
| Player / Empire control architecture | **A** | exactly one `CommandSource.Player` Empire at every AI count; asserted at 1, 3, 11 |
| `ControlRow` authority | **A** | sole membership source; no `EmpireId`/`CivilizationId`/`SettlementOwnerId` exists; `SettlementRow` has no owner field |
| Capital designation | **A** | `CapitalRow` is a (Polity, Place) relation; each Empire holding ground has a capital inside it |
| Colony inheritance | **A** | colony inherits the PARENT's controller; stateless parent → stateless colony; failed founding writes neither row. Mutant-verified |
| Configurable AI count | **A** | `worldgen.aiEmpires`, default 0; verified at 1 / 4 / 8 / 50; surplus Empires representable-and-extinct |
| Common order pathway | **A** | one pathway; `CommandSource` distinguishes source, not architecture; `ActorId` is a `PolityId`, never a player marker |
| Transport | **A** | T4.7 river-aware traversal live; measured cause of the density corridor's return |
| Trade (arbitrage) | **A** | live; D-034 deadband; realised flows drive merchant emergence |
| Foreign-trade classification | **B** | implemented; **structurally dormant** — one polity controls everything founded, so every pair is Domestic or Unruled. `aiEmpires > 0` is the seam that activates it |
| Merchants (T4.11) | **A** | registry class emerging on `trade_volume`; **verified to emerge in a real 650-turn canonical run** |
| Strategic war boundary | **D** | no armies; no AutoResolver; player-side auto-win is the standing boundary until the military stage |
| Notables (T4.8) | **B** | conservation surface correct and lifecycle implemented; **no production driver**, so the table is always empty |
| Claims / recognitions (T4.3) | **B** | schema-only by design; no writer, no reader |
| Colonization | **A** | live; founds settlements, moves people by `Ledger.Transfer`, transfers provisions |
| Land clearance | **A** | T4.4; leaving costs the parent settlement real food |
| Uncontrolled settlements | **A** | reachable via revolt — the pathway that did not exist before this pass |
| T4.5 appropriation | **A** | mechanism live and its raider precondition now **reachable**; asserted against the gate's own predicate |
| Happiness (T4.13) | **A** | derived 0..100, pure query, not a stock, **not serialized** |
| Revolt | **A** | zero happiness drops the control relation; **moves no golden** |
| Happiness → migration | **A** | weak (w = 0.15) multiplier on destination viability; zero-weight arm proves it is not inert |
| Migration anomaly | **C-adjacent — PROVISIONALLY ACCEPTED** | below the historical corridor on 19/20 seeds; **not a quarantine, not tuned**; cause open (§6) |
| Malthus corridor | **C** | quarantined by CR-003 §8.2; bands frozen |
| Construction / `Structures` | **E** | system live and the control rule enforced at the consumption point, but **no production emitter** issues `EnqueueConstruction` and completed `Structures` are read by nothing. Real broken chain, deliberately not repaired |
| Timestep | **A** | deterministic 10-year atomic turn, 0.5-year demographic microsteps, fixed pipeline — unchanged |
| Schema stability | **A** | v24 throughout; merchant rows are more rows of types that already existed |
| Determinism / replay / save-load | **A** | all PASS |

---

## 1. WHAT M4 BUILT

M4 is the milestone where the world acquires **neighbours, scarcity that can bite, and a reason to
stop obeying**. The mechanisms, in the order they compose:

| mechanism | packet | state |
| --- | --- | --- |
| Empire identity, control, capitals, command source | M4-A/B/C/D, D-042 | **LIVE** |
| Bounded stores (spoilage + granary capacity) | T4.2 | **LIVE** — starvation is reachable |
| Claim / control / recognition as three relations | T4.3 | **SCHEMA ONLY, by design** |
| Colonization and land clearance | T4.4 | **LIVE** |
| Non-state peoples (appropriation) | T4.5 | **LIVE, and now REACHABLE** — see §3 |
| Foreign-trade classification | T4.6 | **LIVE, structurally unreachable** — see §5 |
| River-aware traversal | T4.7 | **LIVE** |
| Notables as conserved people | T4.8 | **DORMANT by ruling** — no production driver |
| Lattice stride ruling | T4.9 | **RULED, docs only** |
| Migration attractiveness (food term removed) | T4.10/T4.12 | **LIVE** |
| Merchants | T4.11 | **LIVE — they emerge in the canonical world** |
| Derived settlement happiness 0..100 | T4.13 | **LIVE** |
| Colony control inheritance | M4 completion §10 | **LIVE** |
| Revolt at zero happiness | M4 completion §21 | **LIVE** |
| Configurable AI Empire count | M4 completion §11 | **LIVE, default 0** |

---

## 2. THE PLAYER, THE AI, AND WHO OWNS WHAT

- **Exactly one Empire is human-commanded**, at every AI count, asserted at 1, 3 and 11 rivals.
- **`PolityId` is the only strategic identity.** No `EmpireId`, `CivilizationId`,
  `SettlementOwnerId` or equivalent exists; `SettlementRow` carries no owner field.
- **`ControlRow` is authoritative** and is the only thing that says who holds a place.
  `CapitalRow` is a designation on top of it, never an identity.
- **The player controls every settlement of their Empire, including colonies.** A colony founded
  from a settlement Polity P controls is controlled by P — one appended `ControlRow`, written only
  on a successful founding.
- **AI count is configuration, not code.** `worldgen.aiEmpires` defaults to 0, which reproduces the
  shipped single-Empire world exactly; 1, 4, 8 and 50 all produce the corresponding rosters, with
  surplus Empires representable-and-extinct rather than an error.

---

## 3. THE LOOP THAT MAKES T4.5 REAL

The single most consequential thing this milestone closed, because it converted a dead mechanism
into a reachable one:

```
material conditions  ->  derived happiness (0..100)  ->  zero happiness  ->  revolt
    ->  the control row is dropped  ->  an UNCONTROLLED settlement exists
    ->  T4.5's appropriation gate can finally open
```

Before it, `WorldFounding` wrote a control row for every settlement and nothing ever removed one, so
a founded world contained **zero** stateless settlements for its entire 6,000 years — and T4.5's
raider, which must be stateless, could never exist. Statelessness is now something a world can
ARRIVE at, by governing a place so badly it stops being governed, rather than something seeded into
worldgen to make a test pass.

Colonization propagates it: a stateless parent founds a stateless colony, so D-037 B1 survives as
the general case rather than a special one.

---

## 4. HAPPINESS

Derived, bounded 0..100, **not a stock**, **not serialized**, recomputed from state on every call.

- **Factors today:** food sufficiency (1 − consumption deficit) and housing sufficiency (dwelling
  capacity over population, capped at 1) — the same expressions the needs system uses.
- **Absent, and stated rather than stubbed:** WATER is not modelled anywhere; CLOTHING/comfort
  exists only inside the needs system and is unavailable for the D-021 reason below; TAXATION does
  not exist on this tree at all (M5 owns it).
- **Aggregation is non-compensatory** (D-035-B CES, σ = 0.5): a full granary cannot buy off having
  nowhere to live.
- **Zero is reachable only at total deprivation**, because the scale is anchored to the all-zero
  aggregate. This is what makes the revolt condition a real corner of the state space rather than a
  predicate that can never fire.
- **Migration reads it WEAKLY** — it multiplies destination viability by (1 − w + w·happiness),
  w = 0.15, so it composes with and never bypasses the deficit repulsion and the absolute food gate.
  A famine destination is viability 0 whatever its happiness.
- **Nothing grants happiness for migrating.** The feedback closes only through changed conditions:
  people move, population and per-head provision change, and the next turn's reading follows.

**An architectural limitation worth the director's attention.** The natural substrate for happiness
is `NeedSatisfaction`, which already expresses this idea and already aggregates by D-035-B. It is
NOT used, because D-021 rules that needs/grievance state drives no behaviour until M5 and
`check-read-isolation.sh` enforces that — and happiness feeds migration, which is behaviour. The
duplication is deliberate and is the cheaper price. **Reusing the needs aggregate would be a real
improvement and it is a director's call, because it turns on D-021 rather than on taste.**

---

## 5. MERCHANTS

A **registry class** emerging on observed trade volume, not a bolted-on actor table — which is what
T4.11 specifies ("the class that emerges on trade volume") and what makes merchants real people:
they occupy buckets, eat, age, migrate and carry needs like anyone else.

- `trade_volume` is published per settlement from realised `TradeFlows` (absolute, not a ratio, so
  it is scale-dependent and a large entrepot crosses where a hamlet never will).
- Emergence: `trade_volume > 200 && population > 520`; recede below 50.
- **Measured, and the reason the latch matters:** per-settlement volume is EPISODIC — 114, 135, 2,
  227, 3042 at turns 100/200/300/400/650, with only 2–4 settlements trading at once. D-020's
  hysteresis latch is what converts that spiky signal into a durable merchant town; a single
  threshold would have oscillated.
- **They do emerge in the canonical world** — asserted by running the real pipeline for 650 turns,
  not by a hand-built rig.
- Merchant baskets sit one step further from the staple than artisans, so they import more and are
  hit harder when trade stops — the correct coupling for a trading class.

**EXTENSION SEAM, and what it deliberately does not do.** Long-distance shipping, foreign trade,
multiple cargo classes, specialised logistics and cold-chain all arrive as a larger NUMERATOR on the
same published variable. None of them requires replacing the merchant identity model. **None of them
is implemented here.**

**Foreign trade remains structurally unreachable** (T4.6's own finding, unchanged): one polity
controls everything founded, so every classified pair is Domestic or Unruled and `CountForeignFlows`
is identically 0. Turning on `aiEmpires` is the seam that would change this; doing so is a measured
decision, not a default.

---

## 6. CALIBRATION DISPOSITIONS

| corridor | disposition |
| --- | --- |
| `canonical.densityPerArableKm2` | **QUARANTINE LIFTED.** 20/20 seeds in band (min 0.28080, mean 0.41290, max 0.56483) against the UNTOUCHED corridor [0.15, 0.6] — the quarantine's own lift condition, met at the ≥20-seed standard §6 requires. Cause of the return: T4.7's river-aware traversal enlarging catchments. |
| `canonical.migrationGrossPerDecade` | **ACCEPTED AS MEASURED, NO LONGER A GATE.** 1/20 in band (mean 0.00057). Not tuned, not re-banded. The recorded cause is **refuted**: a single-variable arm lifting T4.2's granary cap made it WORSE (1/20 → 0/20, mean 0.00057 → 0.00036), the opposite sign to the 5.56× recovery on file. The cause is unidentified. A liveness tooth remains — migration stopping entirely would still fail. |
| `dev.crashCount` and the Malthus family | **QUARANTINED, unchanged.** CR-003 stays open and its bands stay frozen. Fed-density health is not crash emergence and the two were deliberately not conflated. |
| artisan famine / recession | **CLOSED AS UNSUPPORTED.** Both directions were asserting a prediction rather than a mechanism; the drain mechanism itself stays live and exercised by hand-built rigs. |
| demote-first ordering | **CLOSED.** Already ruled UNREPRESENTABLE at current causal resolution (2026-08-13): two systems consume the same one-turn-lagged deficit and cannot be ordered within a turn. |

---

## 7. WHAT IS DORMANT, AND WHY THAT IS HONEST

- **T4.8 notables** — the conservation surface is correct and the lifecycle is implemented, but no
  production driver creates one, so the table is always empty. Strategic war and the AutoResolver
  are M6 by ruling; the pre-army boundary holds.
- **T4.3 claims and recognitions** — schema only, by design; no writer, no reader.
- **Foreign trade** — classified but unreachable (§5).
- **Construction** — the system is live and the control rule is enforced where orders are consumed,
  but no production emitter issues `EnqueueConstruction`, and completed `Structures` are counted and
  read by nothing. **This is a real broken causal chain and it is not fixed here.**

---

## 8. DEFERRED BY RULING

Knowledge/technology (no tech tree, no diffusion, no era system), money and finance, full armies and
the AutoResolver, M5 politics. The 10-year atomic turn, the 0.5-year demographic microsteps and the
deterministic fixed pipeline are unchanged.

---

## 9. KNOWN OPEN

- **CR-003** — open; the Malthus quarantine and its frozen bands depend on it.
- **CR-005, CR-006** — open; neither blocks M4.
- **The migration corridor cause** — unidentified, recorded, not tuned around. **Investigation is
  DEFERRED by ruling; migration is not an M4 or M5 tuning target.** If it is ever taken up, a
  per-merge bisection is the shape, and T4.1b's `minSpacingKm` 480 → 95.2 is an untested prior
  suspect — recorded so the thread is not lost, not as work outstanding now.
- **Happiness vs D-021** — §4.
- **Structures read by nothing** — §7.

---

## 10. GOLDEN MOVEMENT — MEASURED, NOT ASSERTED

Three moved, one deliberately did not, and the causes were **separated by a control arm** rather
than attributed by argument.

| golden | old | new |
| --- | --- | --- |
| Founded, seed 42 turn 300 | `8759fcb8dadbc91905cdc410cb1933e9211b830f8195c829ecbab887025e4048` | `98a89d18b014fa1726ab3ee611a8662b2982bf4fbac0b10ada00718e4eebd983` |
| FirstReign turn 40 | `51ba9b1187ef48b3ae0953096b53c92e6efa6a61e667fd1c6cbaf7b4bc3854e3` | `7a9c3de745eac824c5c1b5783d527cf959ada9c558e7012423bea5f92a6361a3` |
| Driven, seed 42 turn 300 | `0b9423d6f451a313003ded645e799056c6d4b7d6a4528894f668aafd04f76272` | `01673381e5e4b18753bf19f345e42f5424046a813a8c723a7564be34186820af` |
| **`GoldenHash_Seed42Turn200`** | — | **UNMOVED — PROTECTED CONTROL** |

**THE TWO CAUSES, AND HOW THEY WERE SEPARATED.**

1. **T4.13 happiness → migration.** A **zero-weight control arm** —
   `migration.attractivenessHappinessWeight` 0.15 → 0.0, nothing else changed — reproduced the
   PREVIOUS pins **exactly** on all three worlds. That is the measurement proving **colony-control
   inheritance, the revolt system, and the happiness query itself move NO golden at all.**
2. **T4.11 merchants.** A third registry class adds one `ClassStateRow` and one `GrievanceRow` per
   settlement to the canonical stream.

**FirstReign moved for cause (2) ALONE.** At the intermediate tree — happiness live, merchants
absent — Founded and Driven had already moved while FirstReign had **not**. FirstReign is a 40-turn
director replay whose settlements stay comparably provisioned, so a 15 % viability modulation
reorders nothing in it.

**NO UNRELATED MOVEMENT.** `GoldenHash_Seed42Turn200` is unmoved, and its v22-stripped value is
still main's pre-M4 **`0f94b4ad95b8821d19b24d208d56ecc1d2be755ced2d89c539249855ebc23745`**. It is
synthetic and terrain-less, so migration cannot reach it — which is exactly why it is the control.

**NOT A SCHEMA CHANGE.** `CanonicalSchema` stays at v24. No row type, field or table joined or left
the stream; the merchant rows are more rows of types that already existed.

---

## 11. MIGRATION — PROVISIONALLY ACCEPTED, CAUSE OPEN

**This is not a quarantine.** It is behaviour accepted for M4 with the causal explanation left open.

- Long-distance permanent migration remains **below the historical corridor** `[0.001, 0.01]` on
  **19 of 20 seeds** (min 0.00029, mean 0.00057, max 0.00103). The record had one seed 2 % under.
- **The previous granary-cap attribution is REFUTED.** A single-variable arm
  (`granaryYearsOfDemand` 1.5 → 1e6, nothing else, 20 seeds) moved migration the **wrong way**:
  1/20 → 0/20 in band, mean 0.00057 → 0.00036, the opposite sign to the ×5.56 recovery on file.
- **The mechanism remains responsive** — migration teeth and the happiness arm both demonstrate it
  reacts to its inputs.
- **NO TUNING WAS PERFORMED.** No migration constant moved, no corridor bound moved, no
  migration-related golden was repinned.
- A **liveness tooth** remains: migration stopping entirely still fails loudly.

**Disposition: provisionally accepted for M4; causal explanation remains open.** Future food,
governance and related systems are expected to move this behaviour.

---

## 12. DO NOT REOPEN DURING M5 STARTUP

These are settled. Reopening any of them at M5 startup is out of scope without a fresh ruling:

- **Migration is not an M5 tuning target.** The anomaly is an open *research* question. It is not
  permission to move migration constants or corridor bounds.
- **The Malthus corridor stays quarantined.** Do not loosen it, fit yield to it, alter consumption
  or demographics to satisfy it, fabricate starvation, or delete the quarantine.
- **Happiness stays derived.** Not a stock, not serialized, no persisted row. The causal direction
  is conditions → happiness → migration pressure → movement → changed conditions → recalculated
  happiness. **Migration must never grant happiness directly.**
- **D-021 stays as-is.** Needs/grievance state drives no behaviour before M5. Happiness's deliberate
  duplication of primary-signal calculations, instead of reading `NeedSatisfaction`, is accepted for
  exactly this reason. Do not activate D-021 as a side effect of M5 startup.
- **Identity stays `PolityId` + `ControlRow`.** No `EmpireId`, `CivilizationId`,
  `SettlementOwnerId` or second ownership abstraction.
- **The uncontrolled-settlement path stays the legitimate one** — bad conditions → zero happiness →
  revolt → control dropped. Not test-only exceptions, seeded stateless settlements, or fabricated
  world state.
- **The 10-year atomic turn stays.** No annual substeps, no mid-turn handbacks, no order-timing
  changes. CR-006 remains open.
- **Do not fabricate a second polity to activate foreign trade.** The `aiEmpires` seam exists; using
  it is a measured decision.
