# ADR-023 — FOOD VARIETY IS REPRESENTED EXCLUSIVELY BY D-035-A

**Status: ACCEPTED** (director ruling, M4 dietary-diversity gate).

> Food variety is represented exclusively by D-035-A. A second dietary-diversity
> happiness mechanism is rejected as double counting of the same underlying signal.
> Any future change to the salience of food variety must calibrate D-035-A rather
> than introduce another channel.

---

## §1 WHAT WAS PROPOSED, AND WHAT WAS DECIDED

A director packet authorised a new M4 mechanic, **Dietary Diversity**: normalised
Simpson diversity `D = (1 − Σpᵢ²) / (1 − 1/N)` over the actually-consumed
Sustenance goods, paying `DiversityBonus = 5 × D` as **additive happiness points**
inside the existing 0–100 bound, with the invariant that total deprivation keeps
happiness at exactly 0.

That packet carried its own hard gate as §1: establish, before writing code,
whether the proposal and the existing D-035-A food-variety mechanism are the same
phenomenon. **The gate returned STOP.** The director then ruled, rejecting the new
mechanism and confirming D-035-A as the sole authoritative channel.

**Rejected by name, and not to be reintroduced under another name:** a second
diversity index; Simpson diversity; Shannon diversity; a new happiness bonus; an
additive post-aggregate happiness modifier; a multiplicative replacement for the
existing happiness architecture; a food-only deprivation guard; a new migration
term; a new demographic survival term; a new revolt term; any new food-variety
state; any new serialized diversity field.

---

## §2 THE INDEPENDENT RECONCILER FINDING

The gate was run **blind**: an earlier implementation attempt, killed mid-run by a
container restart, had left an unverified draft arguing the two mechanisms were
distinct. That draft was preserved unmerged on `m4-diversity-draft`, the working
branch was reset, and the reconciler was explicitly instructed not to read it, so
its verdict was reached independently. **The independent verdict contradicts the
draft.** The draft is superseded and carries no authority.

Three independent grounds were returned, any one sufficient.

**(a) They are one signal in two units.** Both compute the Herfindahl
concentration `H = Σ shareᵢ²` over the same eligible Sustenance goods
(`BasketBook.FoodGoods`, N = 3 on shipped data), from the same field. Over the
range the shipped world actually occupies the two are collinear: the proposal is
an affine rescaling of the existing factor. The differing zero cases, ranges and
destinations are plumbing, not phenomenon. The same player decision — importing
livestock or fish — moves both.

**(b) The same input field.** D-035-A's factor reads
`GoodStockRow.LastConsumptionEatenUnits` through `NeedsGrievanceSystem.Fill`,
which is the post-clamp eaten figure. That is precisely the field the new packet
named as its authorised input. *Verified at source for this record:*
`Fill(in GoodStockRow)` returns
`Clamp(row.LastConsumptionEatenUnits / (double)demanded, 0, 1)`.

**(c) Measured double counting.** For one cause, both channels move by comparable
magnitudes. The proposal's five-point ceiling is worth roughly 0.086 of food
sufficiency near the top of the happiness scale, against the existing term's
measured swing of ≈0.0897 of Sustenance satisfaction. That is a second mechanism
of comparable size, not a nuance beside one.

The strongest counter-argument — that grievance drives no behaviour before M5
under D-021, so the two rewards cannot compound in a simulated outcome — was
tested and rejected: the existing channel is already **displayed and explained**,
so two on-screen quantities would rise for one purchase; D-021 gives that channel
teeth one milestone later, with no packet then looking for the overlap.

---

## §3 WHY THE ADDITIVE SIMPSON FORM WAS REJECTED

Beyond the double counting, the proposed shape is **forbidden by name** by the
decision it would have sat beside. `docs/d035-needs-aggregation.md` §D-035-A,
verbatim:

> **NEVER as a bonus modifier.** This is law 2 (mechanisms over modifiers) and it
> is the whole difference between a mechanism and a buff: a variety *bonus* added
> after the fact is a free-floating modifier; a concentration term inside the
> equation changes what satisfaction *is*.

The proposal is an additive point bonus applied after the score is complete. That
is the sentence's target. Numerical convenience and determinism do not override an
existing design ruling.

A second, narrower reason is recorded so it is not rediscovered: the proposal's
`N` is the eligible Sustenance good count, which is **data-dependent** — a new
Sustenance basket line silently rescales `(1 − 1/N)`. The existing term normalises
against a **fixed** standard `H*` (`needs.json varietyStandard.shares`
`[0.70, 0.20, 0.10]`, H* = 0.54) and is immune to that.

---

## §4 WHY THE MULTIPLICATIVE ALTERNATIVE WAS NOT SELECTED

The reconciler offered, as the only law-2-clean way for variety to reach
happiness, folding the existing factor **inside** the food-sufficiency term:
`FoodSufficiency := clamp(1 − DeficitRatio, 0, 1) × VarietyFactor(...)`. That form
cannot violate the deprivation invariant, since `0 × anything = 0`, so no guard is
needed and none can be written wrongly; it calls the existing function rather than
adding a second index.

**It was not selected, and this ADR is not a deferral of it.** It is a change to
what happiness reads, and `SettlementHappiness` states in terms that what happiness
may read is a director's call because it turns on D-021 rather than on engineering
taste. The director's ruling directs any future change in the salience of food
variety to **calibrate D-035-A**, not to open a second path into happiness. Anyone
revisiting this needs a fresh director ruling and its own ADR; it is not licensed
here.

---

## §5 WHY THE FOOD-ONLY DEPRIVATION GUARD WAS REJECTED

The proposal required that total food deprivation keep happiness at exactly 0.
**There is no food-only deprivation predicate in the architecture**, and creating
one would have been a behaviour change disguised as a guard.

Measured: `food = 0.0, housing = 1.0` gives a CES aggregate of 0.090909 and
happiness **4.306220 — not zero**. Only `food = 0.0, housing = 0.0` reaches the
floor and yields exactly 0. The scale is deliberately anchored so that **total
deprivation across every factor is the only route to zero**.

A guard written against food sufficiency alone would therefore have forced a
settlement with no food but intact housing from 4.31 down to 0, newly satisfying
`IsRevoltReady` and silently rewriting D-021's revolt condition from inside a food
packet. The correct predicate, had one been needed, is the existing
`SettlementHappiness.RevoltThreshold` (verified: `0.0`) applied to the aggregate —
never a literal and never a new constant.

Noted for the record: the zeros of "food sufficiency" and "nothing eaten" coincide
today only through an undeclared coupling — `DeficitRatio` is computed over the
whole basket, not food alone — which a future non-food basket line would break
silently. An invariant that holds by coincidence is not an invariant.

---

## §6 THE CANDIDATE ANCESTRY THIS RULING WAS MADE AGAINST

The ruling was made against candidate **`79dec68`** on `t4.19-glass-box`, with
`main` at `dbef61a` and M5 at `9a79d1e`, neither touched.

`79dec68` is a descendant of `5be93f3` by exactly one merge, carrying exactly two
commits, both the previously authorised **observability-only** food-legibility
lane. Verified for this record:

| check | result |
| --- | --- |
| ancestry | `79dec68` is a descendant of `5be93f3` |
| first-parent path | one merge commit, two commits carried |
| scope | 14 files: observability, UI, tests, docs |
| forbidden paths | diff **empty** under `Sim.Core/Systems`, `Sim.Core/Kernel`, `Sim.Core/State`, `Sim.Core/Worldgen`, `Sim.Data`, `Sim.Cli`, `.github`, `scripts` |
| canonical schema | **24** at both ends, unchanged |
| golden pins | all 54 snapshot, 14 driven and 1 CI hash **identical** at both ends |

The one output-shape change in that lane is the telemetry vintage tag moving
`telemetry/v1` → `telemetry/v2`, because the emitted field set changed. That is
telemetry vintage, not canonical schema, and it is recorded here so the ancestry is
complete.

Nothing was reset, rebased, cherry-picked or discarded to reach an older SHA.

---

## §7 CONSEQUENCES

1. **D-035-A's existing destination and semantics remain authoritative.** Variety
   enters as a concentration term inside the satisfaction equation, lands in
   `NeedSatisfactions`, and feeds grievance.
2. **`NeedsAggregation.VarietyFactor` is the single authoritative food-variety
   calculation.** A second implementation of a concentration index over food
   shares is a defect, wherever it appears.
3. **Salience is a calibration question, not an architecture question.** Changing
   how much food variety matters means changing `varietyWeight` in
   `needs.json` — which CLAUDE.md permits without a ruling — measured first, never
   chosen by intuition.
4. **The `+5 happiness points` figure is void.** It belonged to the rejected
   mechanism and is not a calibration target for anything.
5. `m4-diversity-draft` is unverified, contradicted, and must not be used as an
   authority by any future packet.
