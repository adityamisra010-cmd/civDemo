# D-035 — Needs aggregation, variety, and the legal coupling paths

**Status: RATIFIED.** Director decision record, filed 2026-07-27. Exempt class under S8 §4 — a
D-decision, not a milestone spec.

**AMENDS `docs/d018-classes-and-needs.md:52`**, which gives the math shape as a WEIGHTED SUM:
`G += Σ wₙ·(expectationₙ − sₙ)⁺·dt − decay·G·dt`. D-035-B replaces the aggregation with a
CES-class form. `d018:46`'s Tier A gate override is **retained unchanged** and continues to
operate on top of the new aggregation.

**Provenance note.** These four rulings were made but never filed. The implementing agent could
not locate them and refused to proceed on them (the ADR-016 §4 standing note: an uncited ruling
is refused and queried, not reconstructed). The director confirmed them as real and unfiled, and
this document is the filing. The refusal was correct — reconstructing "non-compensatory CES with
Tier A overrides" from a one-line mention would almost certainly have produced a different model
from the one ruled.

---

## D-035-A — VARIETY AS SATISFACTION

Basket satisfaction depends on **diversity as well as quantity**. The same calories from grain
alone satisfy Sustenance **less** than the same calories spread across grain / livestock / fish.

**Implementation.** A concentration term **inside the satisfaction equation**, Herfindahl-style,
TUNE-weighted. The same shape applies to Comfort across pottery / cloth.

**NEVER as a bonus modifier.** This is law 2 (mechanisms over modifiers) and it is the whole
difference between a mechanism and a buff: a variety *bonus* added after the fact is a free-
floating modifier; a concentration term inside the equation changes what satisfaction *is*.

**Rationale (director).** Monotony is real deprivation, and it gives trade a purpose beyond
scarcity — a settlement that can feed itself entirely on grain still has a reason to import fish.

**Note for implementation:** a Herfindahl index over basket shares is `H = Σ shareᵢ²`, running
from `1/n` (perfectly even across n goods) to `1` (everything from one good). The satisfaction
equation should be *decreasing* in H at fixed quantity. State the mapping and its TUNE weight;
do not leave "Herfindahl-style" to interpretation.

---

## D-035-B — NON-COMPENSATORY AGGREGATION *(amends d018:52)*

Needs must **NOT** combine as a weighted sum, under which abundant comfort offsets absent
shelter.

**Commit to CES-class aggregation with substitution elasticity σ < 1**, plus **d018:46's Tier A
gate override retained unchanged**: below a floor, a gate need (Sustenance / Shelter / Safety)
scales **superlinearly** and collapses upper-need weights toward zero.

σ < 1 is the load-bearing constraint. At σ = 1 CES degenerates to Cobb-Douglas and at σ → ∞ to
the linear sum this ruling exists to forbid; only σ < 1 makes needs genuine complements, so that
a need at zero cannot be bought off by surplus elsewhere.

### MANDATORY ACCEPTANCE TEST

> With one need pinned at 1.0 and another at 0.0, aggregate grievance must remain **above a
> stated floor for ALL values of the first**.

A rigged "maximise one cheap need, ignore the rest" configuration **must FAIL to suppress
grievance**. This is the test that distinguishes the CES form from the sum it replaces, and it is
the one that must be proven RED against a weighted-sum implementation (ADR-015 §7.4 — prove the
red, do not assume it).

### The director's named scenario ships as a test

**Shelter 100%, heavy taxation, Dignity and Comfort at zero → high grievance.** Named, shipped,
and not merely implied by the general test above.

---

## D-035-C — THE SEVEN LEGAL COUPLING PATHS

**Exactly one thing is FORBIDDEN:** `satisfaction_A` feeding directly into `satisfaction_B` with
an invented weight. It is unfalsifiable, and it destroys glass-box explanation.

All real coupling runs through these seven instead:

| # | path | mechanism | carrier |
| --- | --- | --- | --- |
| 1 | **SHARED SATISFIER** | one good/service/institution serves several needs — cloth → Comfort *and* Shelter bedding. **Declare shared satisfiers in `needs.json`.** | a good |
| 2 | **SHARED BUDGET / SHARED INPUT** | all purchased needs draw on one goods-and-income pool; all built needs draw on one labour pool. Priced competition. **The primary household coupling.** | a purse, a labour pool |
| 3 | **DEMAND COUPLING** | one need's physical state changes the QUANTITY another requires — cold shelter raises the food requirement for warmth. Physiological. | a body |
| 4 | **COMMON CAUSE** | two needs read the same upstream world variable — density → Shelter and Health; war → Safety, Shelter, Sustenance. Correlation with **no link between the needs**. | a world variable |
| 5 | **CAPACITY FEEDBACK** | unmet Sustenance/Health degrades labour productivity and raises mortality → less output → every basket shrinks. **Through production and demography, never directly.** | production, demography |
| 6 | **INSTITUTIONAL TRADE-OFF** | one institution raises one need and lowers another — garrison buys Safety with Dignity; conscription buys Safety with Sustenance. **M5 must build Safety-vs-Liberty this way and no other way.** | a policy, an institution |
| 7 | **GRIEVANCE-LEVEL BUFFERING** | already ratified as D-021's Endurance valve: faith and community absorb grievance from unmet needs **without changing satisfaction**. Operates *after* aggregation. **Explicitly legal.** | an institution |

### THE TEST for any proposed coupling

> **Name the physical carrier — a good, a purse, a building, a policy, a body, a season.**
> If none exists, it is an invented modifier and is **refused**.

This is law 2 given an operational form. The seven paths are not a taxonomy to be extended by
analogy; a coupling that does not fit one of them has not found an eighth path, it has failed the
carrier test.

---

## D-035-D — TAXATION → DIGNITY IS DIRECT *(M5; recorded now)*

Heavy or arbitrary exaction **injures Dignity directly**, with the **tax rate / instrument** as
the input. **Not** comfort routed into dignity.

Recorded now because it constrains M5 and because it is the clearest instance of the D-035-C
carrier test: the carrier is the tax instrument itself, so the coupling is legal and direct. A
version that routed taxation through Comfort and then Comfort into Dignity would be exactly the
forbidden `satisfaction_A → satisfaction_B` link wearing an economic costume.

---

## Consequences

- `d018:52`'s weighted-sum form is **superseded** for aggregation. The rest of d018 — Tier A gate
  needs (§46), Tier B class signatures (§47), ratcheting expectations, relative deprivation —
  stands.
- T3.5 implements A, B and C. D is M5.
- Any future need-coupling proposal is checked against the seven paths and the carrier test
  before design, not after.
