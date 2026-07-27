# ADR-016 — Exact integration for the price step, and the compounding/linear rule

Status: **RATIFIED** — directed amendment to D-033 by the mandate's author, 2026-07-26
Context: T3.4 acceptance. Supersedes the Euler form of the D-033 step.

## 1. What changed

The price step becomes the closed form:

```
p *= exp(Lambda × (excess / scale) × dtYears)
```

replacing `p += Lambda × p × (excess / scale) × dtYears`.

## 2. What did NOT change

Everything else in the mandate, unchanged and re-verified after the amendment:

- **Per settlement, per good.** No cross-settlement coupling, no cross-good coupling — both
  pinned by behavioural tests asserting bit-identical prices.
- **ONE damped step per turn.** No inner loop, no residual, no tolerance, no iteration to
  convergence. `exp()` is a closed-form evaluation, not a solve.
- **No global equilibrium solve, ever.**
- **Both clamps.** The per-step rail (`MaxRelativeChangePerYear × p × dtYears`) and the absolute
  band are applied exactly as before, to the realised change.
- **Grain pinned at exactly 1.0**, written every turn.
- **The one-turn lag**, reading PREV quantities.
- **Explainability**, though its decomposition arithmetic changed — see §5.

The amendment changes **how the same rate is integrated**, not what is being solved. The
mandate's intent is untouched.

## 3. Why — the general principle

**COMPOUNDING PROCESSES REQUIRE EXACT INTEGRATION; LINEAR ONES DO NOT.**

The test is whether the integrated quantity appears on the right-hand side:

| shape | example | Euler | required |
| --- | --- | --- | --- |
| `stock += rate × dt` | tool wear, harvest, extraction | **exact** — no residue exists | Euler is fine |
| `stock *= …` or `stock += rate × stock × dt` | prices, interest, capital accumulation, epidemic spread, mortality | **under-integrates**, residue grows with dt | closed form |

For a compounding process Euler computes `p(1 + r·dt)` where the truth is `p·e^{r·dt}`, and the
gap widens with both `r` and `dt`. In a simulation whose era table deliberately shrinks dt across
the campaign, that gap is not a fixed bias — it is a systematic drift in behaviour caused by the
integrator rather than the world.

**This is why the T3.3 tool-wear refutation was correct and does not carry here.** Tool wear is
linear in the stock being worn, so Euler is exact for it and the refutation's reasoning — "no
correct implementation is exempt from discretization" — was right *for that mechanism*. Price is
not linear. The same sentence applied to a compounding process is wrong, and the difference is
exactly the right-hand-side test above. Two mechanisms, opposite rulings, one rule.

ADR-011 is this same ruling applied to demographics (exponential survival for mortality). It
should be read as the first instance of the rule this ADR now states in general.

## 4. Determinism basis — verified before implementing

The director required confirmation that this `exp()` usage shares ADR-011's determinism basis,
and that implementation stop if it does not. It does:

- **Same math path.** `System.Math.Exp`, identical to the calls already live in sim code at
  `DemographicsSystem.cs:97,204,207` (ADR-011's exponential survival) and
  `MigrationSystem.cs:206` (migration damping). No new library, no new numeric surface.
- **Same gate.** The cross-process determinism check — two separate processes, 300 turns,
  byte-identical hash logs — passes with those `exp()` calls live today, and passes with this one
  added. Replay equality likewise.
- **Same guarantee, stated honestly.** This is single-machine determinism. `Math.Exp` is not
  contractually bit-identical across CPU architectures or runtime versions, and the project has
  never claimed it is; the guarantee the harness enforces is same-binary, same-machine
  reproducibility, which is what replay and the golden pins depend on. Adopting `exp()` here
  widens nothing — that exposure was taken at ADR-011 and is unchanged.

**Citation corrected at implementation time — director confirmed, 2026-07-26.** The ruling cited
"Law 9 as amended"; the director was quoting the v3 Spine's TEN-law numbering as if it were
CLAUDE.md's SEVEN. There is no Law 8 or Law 9 in the constitution. The grounding below is the
correction, confirmed by the director as "correct and better", and it is recorded here so a
future reader does not go hunting for a law that does not exist.

**STANDING NOTE (director ruling):** if the director cites a numbered law or a document that
cannot be located in the repo, REFUSE it and ask rather than proceeding on it. Both instances in
this packet — the phantom Law 9, and the "Nenatul" evidence that greps empty because it is a
runtime-generated name from a chronicle outside the tree — were correctly refused. The second
turned out to be real evidence held outside the repo, now filed as
`docs/t3.4b-migration-evidence.md`; the first was simply wrong. Refusing both was right, and the
cost of refusing a real one is one round-trip.

**The original wording of this correction.** The ruling refers to "Law 9 as amended". There is no
Law 9, and no Law 8: `CLAUDE.md` states laws 1–7 and neither number appears anywhere in `docs/`.
The determinism basis above is therefore grounded in what exists — law 5's banned-construct list,
the cross-process CI gate, and the ADR-011 precedent — rather than in a law number that would not
resolve for a future reader. Flagged rather than silently adopted.

## 5. Explainability under a multiplicative step

The decomposition survives, but its arithmetic had to change, and one test's asserted property
changed with it.

Each measured quantity contributes a **rate** to the exponent
(`k = Lambda × dtYears / scale`; `rate_x = ±k × quantity_x`), the exponent is their sum, and the
realised change is `raw = p × (exp(exponent) − 1)`. Terms are then attributed by each rate's
**share** of the exponent, so the four still sum to `raw` exactly.

The consequence: because `exp` is not linear, moving ONE input moves the exponent and therefore
rescales EVERY term by the common share factor. The previous assertion — "perturb one input, the
other three hold exactly" — is now something the mathematics forbids, and keeping it would have
meant asserting a falsehood.

The replacement is just as discriminating: the untouched terms rescale **together**, so their
**ratios to one another are invariant**. Verified to still have teeth — an attribution swap
(`consumption ↔ inputDemand`) fails both sensitivity tests, measured on a mutated tree.

This is the anti-tautology bar holding under a changed mechanism: the sum check still reconciles
by construction and still proves nothing; the sensitivity tests still catch a permuted label.

## 6. The goldens

**Unchanged — deliberately verified, not assumed.** The founded and toy golden runs use the
all-farming default, so every non-grain good has zero production, zero demand and zero stock. The
exponent is exactly 0, `exp(0) = 1`, and the price does not move — bit-identical to Euler's
`+ 0`. Grain is pinned either way.

- toy `8287c70c…` — unchanged
- founded `aebac29c…` — unchanged
- FirstReign `b6e16c1e…` — unchanged

The amendment is therefore invisible to every pinned world, and visible only where prices
actually move. That is itself a finding worth recording: **the goldens do not exercise the price
solver at all**, which is why the driven soak and the dt tests carry the entire behavioural
weight of this packet.

## 7. Measured effect

Same fixture, same 100-sim-year horizon, dt 10 / 5 / 2.5 / 1:

| | dt 10 | dt 5 | dt 2.5 | dt 1 | spread |
| --- | --- | --- | --- | --- | --- |
| Euler (before) | 7.439 | 8.225 | 8.694 | 9.006 | **21%** |
| exact (after) | 9.227814352139522 | 9.227814352139527 | 9.227814352139545 | 9.227814352139534 | **5.8e-16** |

Fifteen significant figures of agreement, and 9.2278 is precisely the limit the Euler sequence
was climbing toward from below. The pin is now `relative spread < 1e-12` — IEEE summation noise
across different step counts, not modelling slack.

**Era-boundary continuity** is pinned separately, in the family of ADR-011's continuity test: a
run crossing the Neolithic→Bronze dt flip (10 → 5) mid-horizon lands within 1e-3 relative of a
run held at one dt throughout, so a price does not visibly change speed at a band boundary for a
reason having nothing to do with the simulated world.

## 8. Consequences

- The `queue.md` Euler-residue item is closed by this ADR.
- Any future mechanism of compounding shape (interest, capital accumulation, epidemic spread,
  compound growth of any stock) ships with the closed form from the start, and §3's table is the
  test to apply. Linear mechanisms keep Euler and need no ceremony.
