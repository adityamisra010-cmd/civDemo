# M4 EXIT INVENTORY

**Measured against the final M4 tree.** Every status below was produced by running this tree, not
recalled. Where something is dormant, unresolved or deferred it says so plainly — an exit artifact
that reports only the green parts is worse than none.

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
- **The migration corridor cause** — unidentified, recorded, not tuned around. A per-merge bisection
  is the outstanding work; T4.1b's `minSpacingKm` 480 → 95.2 is an untested prior suspect.
- **Happiness vs D-021** — §4.
- **Structures read by nothing** — §7.
