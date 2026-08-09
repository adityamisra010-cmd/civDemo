# ADR-018 — D-025's MINIMUM SPACING: 480 km → 95.2 km

**Status:** ACCEPTED (director ruling, 2026-08-08). Amends **D-025**, a frozen decision.
**Packet:** T4.1b. **Derivation:** T4.1b's review record. **Depends on:** nothing. **Depended on by:**
D-040 Part A1, which cites this capacity number.

**ADR-014's precedent binds: a freeze whose amendment procedure is skipped when amending it is
theatre.** This record exists so the amendment is procedural rather than assumed.

---

## 1. WHAT D-025 SAID

> *"Worldgen sites **N = 12** settlements (TUNE, `worldgen.json`): iterative top-score siting with a
> minimum travel-time spacing constraint, composite key (score desc, cell id asc). Dev preset N=4."*
> — `docs/m2-spec.md:9`

The constraint shipped as `minSpacingTravel = 30.0` in the pathfinder's internal cost units. T3.2b
re-denominated it — 30 cost units × 16 km per cost unit on ideal ground = **480 km** — a
re-denomination and **not** a re-tune: both factors are exact powers of two divided out, so siting
was bit-identical.

## 2. WHAT IT NOW SAYS

**`minSpacingKm = 95.2`.** Everything else in D-025 is untouched: N = 12, iterative top-score
siting, the composite key `(score desc, cell id asc)`, the dev preset at N = 4. **The only constant
that moves is `minSpacingKm`.**

## 3. THE DERIVATION

`s = r·√(π/0.866) = 50 × 1.9046 = **95.2 km**`

This is the spacing at which a settlement's **hex allotment equals the catchment area it actually
works** (`hinterlandRadiusKm` = 50). The neighbouring construction — catchments tangent, `s = 2r` —
gives 100 km at 90.7 % coverage. **Both land at 95–100 km.**

**Reference class, fixed BEFORE measuring** (T4.1b manifest): nucleated agrarian settlements in an
established farming landscape, Neolithic–Early Bronze. Sumerian cities 30–50 km; Athens–Corinth
~80 km as a later-era upper bound. **The derived 95 km sits ABOVE the whole band**, driven by the
50 km catchment radius, which is generous relative to the class. **The catchment is out of scope
and was not adjusted** — recorded, not fixed.

## 4. THE INCONSISTENCY IT RESOLVES

Habitable land measured on the canonical continent (seed 42; fertility ≥ 0.10): **5,373,616 km²**.

| quantity | at 480 km |
| --- | --- |
| hex allotment per settlement | 199,532 km² |
| catchment actually worked (π × 50²) | 7,854 km² |
| **worked ÷ allotted** | **3.94 %** |
| habitable land worked at measured saturation (~46) | **4.82 %** |

**95.2 km is the value at which `minSpacingKm` and `hinterlandRadiusKm` stop contradicting each
other.** Under the old constant ~96 % of a settlement's allotment was never touched by anyone.

## 5. THE MEASURED SATURATION, AND A CORRECTION AGAINST THE DIRECTOR

Greedy siting to saturation, 5 seeds (42, 1, 2, 3, 6):

| spacing | measured saturation (min / median / max) |
| --- | --- |
| **480 km (was)** | **40 / 46 / 52** |
| 143.8 km | 206 / 254 / 294 |
| 100 km | 366 / 431 / 549 |
| **95.2 km (now)** | **398 / 457 / 582** |
| 88.1 km | 458 / 515 / 650 |

**THE DIRECTOR'S PREMISE WAS REFUTED BY THIS MEASUREMENT AND THE CORRECTION IS RECORDED HERE
EXPLICITLY** (director ruling, 2026-08-08): **480 km is NOT a wrong constant.** It saturates at a
median of **46** against the Scale Charter's *"~50 (ancient)"* — it is **correct for the ancient
endpoint**. The packet was cut believing otherwise, and the measurement says otherwise.

**The ruling survives on a distinction the table makes sharper, and this is the paragraph a future
reader must not skip:**

> **SATURATION IS A CEILING ON GROWTH, NOT A FOUNDING COUNT.** Greedy siting fills every slot;
> **worldgen places twelve.** At 480 km the world can **NEVER exceed ~46 settlements however long
> it is played.** At 95.2 km it **CAN reach ~457 across 6,000 years**, gated by colonization's own
> mechanisms.
>
> **95.2 km does not found 457 settlements in 4000 BCE. It permits a world that can eventually hold
> them.** **THE ANCIENT-DENSITY QUESTION IS COLONIZATION'S RATE, NOT SPACING'S FLOOR**, and it
> belongs to **T4.4**.
>
> Nobody may later read 95.2 as a claim about how many villages a neolithic continent held.

**SPACING IS A FLOOR, NOT A TARGET.** And D-040 has since added two further gates on the same
growth: **undiscovered land cannot be settled** (Part B), and **distant holdings are weakly
controlled** (Part C). Capacity is geometric; what fills it is not.

## 6. A NAMED REQUIREMENT, PROMOTED FROM AN OPEN DESIGN NOTE

**NO SINGLE FIXED VALUE OF `minSpacingKm` CAN SATISFY BOTH SCALE-CHARTER ENDPOINTS.** The Charter
asks the same map for **~50 ancient AND 300–800 late**. Back-solved against measured habitable
land, those require **~352 km and ~88–144 km** — **a factor of 2.5–4 apart.** `minSpacingKm` is one
constant. **The two endpoints are not jointly reachable by any value of it.**

**Director ruling (2026-08-08): this is promoted from an open design note to a NAMED REQUIREMENT
with the measurement attached.** **Spacing must eventually DERIVE FROM COMPUTED STATE per law 4** —
population pressure, land quality — and the justification is no longer aesthetic:

> **A fixed spacing constant is PROVABLY INSUFFICIENT, not merely inelegant.** The proof is the
> table in §5 plus the back-solve above, and it is the strongest evidence yet for the
> derive-from-state item.

**Carried forward unchanged, both already filed:**
1. **A fixed spacing constant cannot be right across 6,000 years** — sparse settlement is plausible
   at 4000 BCE and absurd by 1000 CE. Now a named requirement, per the ruling above.
2. **T4.16's clone work should land BEFORE the world grows.** The price solver is O(S²·G²) and
   buckets already collide with their ratified cap at Charter scale. A world that can reach 457
   settlements is a world whose per-turn clone cost matters.

## 7. WHAT BREAKS, AND THE SCHEDULE PRICE

**Nothing in the kernel.** No law is touched: no conserved stock, no rate, no calendar gate, no
determinism construct. Siting is still `(score desc, cell id asc)`; the change is the acceptance
radius fed to it.

**Seven tests move.** FOUR are pins and re-pin cleanly. **THREE ARE NOT PINS — they are semantic
guards asserting properties that the new constant BREAKS.** See §8 and §10. **No test was re-pinned
in this commit**, because three of the seven cannot be re-pinned without deleting a guard.

**Schedule price: none for M4's packet list.** T4.4 (colonization) inherits a materially different
question — it now governs the RATE at which a 457-slot world fills, where before it governed
filling ~46. That is a change of subject, not of schedule, and it is the change the director ruled
for.

## 8. THE SEVEN, ITEMIZED — EACH WITH ITS CAUSE AND ITS KIND

**The packet directed all seven to be re-pinned in one commit with this ADR. FOUR CAN BE. THREE
CANNOT** — they assert properties, not values, and the properties are now false. §10 is the stop.

| # | test | cause |
| --- | --- | --- |
| 1 | `SnapshotTests.FoundedGolden_Seed42Turn300` | world golden: different sites → different world |
| 2 | `DrivenGoldenTests.DrivenGolden_Seed42Turn300` | driven golden: same, under the order log |
| 3 | `GoodsTests.Founding_LaysOutStocks_…_DepositsRolled` | **NOT A PIN — SEMANTIC GUARD. FAILS: *"livestock: every settlement rolled the identical deposit 1 — endowments do not differ"*** |
| 4 | `CatchmentTests.Catchment_NodeCount_And_RecomputeMs_Reported_At1024` | catchments partition differently when sites are closer |
| 5 | `MigrationTests.FamineAtOneOfTwelve_…` | **NOT A PIN. FAILS: *"attributable starvation never crossed the fraction — window too short to prove ordering"*** |
| 6 | `MigrationTests.MagnitudeCorridor_FedPhaseDrift_WithTeeth` | **NOT A PIN — A TOOTH WENT DEAD. FAILS: *"a 10× base rate has NO teeth: produced 0.84 %/decade against 0.82 % — the rate lever is dead in both directions"*** |
| 7 | `CalibrationBatteryTests` dev migration envelope | **see the history line below** |

**THE CORRIDOR ENVELOPE'S HISTORY LINE.**

**Decomposition first (§7.15 — the metric is composite: gross ÷ person-years × 10), dev preset,
1000 turns, both arms in one process overriding only `MinSpacingKm`:**

| spacing | seed | gross migration | person-years | metric |
| --- | --- | --- | --- | --- |
| 480 | 42 | 8,940 | 100,728,701 | 0.000887533 |
| **95.2** | 42 | **6,632** | 100,677,816 | 0.000658735 |
| 480 | 7 | 8,888 | 138,012,502 | 0.000644000 |
| **95.2** | 7 | **9,550** | 137,731,948 | 0.000693376 |

**Seed 42 is a NUMERATOR move: gross ×0.742, person-years ×0.9995.** Not the denominator — this is
**not** the CR-002 family. Branch 1(a) of the packet's stated branches. **And the sign is not
uniform: seed 7's gross moves the OTHER way** (×1.074); recorded, unexplained, outside scope.

**Q2 PREDICTED THIS THIN MARGIN AT T3.4c's CERTIFICATION AND IT MATERIALISED.** The tolerance
0.836 → 0.75 leaves **11 % on the must-pass side**; a legitimate substrate correction larger than
×0.836 would false-fire, and the per-seed pinned values are what make it **loud rather than
silent**. **It fired, it was loud, and the filed risk materialising is evidence the discipline
pays.**

**AND A DEFECT FOUND WHILE MEASURING THE CONTROL — FILED, NOT FIXED.** The 480 arm was validated
against the real battery on `origin/main`: main measures 0.000887533 (seed 42) and 0.000644
(seed 7), identical to the probe to nine significant figures. **Neither RECORDED constant
reproduces on main, before this packet's change:**

| seed | recorded | measured on main at 480 | ratio | tolerance |
| --- | --- | --- | --- | --- |
| 42 | 0.000931705 | 0.000887533 | ×0.953 | 0.75 |
| 7 | 0.000799951 | 0.000644000 | **×0.805** | 0.75 |

**The envelope was ALREADY STALE**, seed 7 sitting 0.055 above the tooth on main. That contradicts
the guard's own documented property (`CalibrationBatteryTests.cs:170-174`): *"self-verifying …
a stale pin cannot rot silently."* **It compares against `recorded × 0.75`, so a pin CAN rot 25 %
in silence — and both had.**

**The director ruled the packet proceeds on the numerator decomposition.** The re-pin therefore
**absorbs two causes**: the ruled spacing change (measured, ×0.742 on the numerator) and a
**pre-existing drift of unmeasured origin**. **That is stated here so the new constants are read
for what they are.** The open question — **when did the envelope drift, and from what?** — is a
bisect over the packets between T3.4c's pin and main, **filed and unowned**, and the false
self-verification claim is filed with it. Neither is fixed here; B3 permits the filing and forbids
the repair.

**No band edge moved. The T3.4c corridor-wide quarantine stands and `[0.001, 0.01]` is untouched.**
Re-pinning a quarantine envelope under a ruling is not the same act as moving a band.

## 9. CROSS-REFERENCES

- **D-040 Part A1** cites this capacity number (46 → median 457) as the premise for discovery and
  control. **Verified against this ADR's final wording: A1 says "from ~46 to a median of ~457" and
  "founding still places twelve" — both hold as written here.**
- **T4.4 (colonization)** inherits the ancient-density question per §5.
- **T4.16 (clone architecture)** should land before the world grows, per §6.


---

## 10. STEP C DID NOT COMPLETE — A B6 STOP, AND THE REASON IS A FINDING

**Branch 2 of the packet's stated branches offered (a) green with exactly seven moved, (b) an
eighth test moved, (c) any test red after re-pinning. THE OUTCOME IS NONE OF THE THREE**, and per
A6 the unpredicted case stops rather than being mapped onto the nearest branch.

**Exactly seven moved, as itemized — the count was right. THE KIND WAS NOT.** Four are value pins.
**Three assert PROPERTIES the shipped constant now falsifies:**

**1. COMPARATIVE ADVANTAGE IS GONE FOR LIVESTOCK.** `GoodsTests` asserts deposit abundances are
*"NOT all equal across settlements (the comparative-advantage precondition)"* — and at 95.2 km
**every one of the twelve rolls the identical livestock deposit, 1.** There is no value to re-pin;
the assertion is a property, and the property is false. **This is the substantive consequence of
the ruled change that nobody predicted**: settlements packed at a fifth of the old spacing sample
too little of the map's heterogeneity to differ. **ADR-017's endowment jitter is defeated by
geometry.**

**2. A MIGRATION TOOTH WENT DEAD.** `MagnitudeCorridor_FedPhaseDrift_WithTeeth` reports *"a 10×
base rate has NO teeth: produced 0.84 %/decade against 0.82 % — the rate lever is dead in both
directions."* The test's anti-vacuity check is what failed: it can no longer tell a 10× rate change
from none. **A guard that cannot fail is not a guard.**

**3. A FAMINE-ORDERING WINDOW NO LONGER PROVES ITS ORDERING.** `FamineAtOneOfTwelve` reports
*"attributable starvation never crossed the fraction — window too short to prove ordering."*

**WHY THIS STOPS RATHER THAN PROCEEDS.** B6: *"any case where following the packet's instruction
would require weakening a test or a guard."* Re-pinning these three means deleting or relaxing
three properties the project asserted deliberately — one of them the **comparative-advantage
precondition the entire trade layer rests on**. That is not a re-pin; it is a design change to the
world's economics, arriving as a side effect of a spacing constant.

**WHAT IS AND IS NOT CLAIMED.** Measured: the three failures and their exact messages, on the
parked tree with `minSpacingKm = 95.2`. **Not claimed:** that 95.2 km is wrong, that the guards are
wrong, or that comparative advantage is unrecoverable — the deposit channel's own scale may simply
need to follow the spacing, and that is a derivation, not a re-pin.

**THE QUESTION THE DIRECTOR NOW OWNS:** the ruled constant is defensible on §4's geometry and §5's
ceiling argument, **and it removes livestock comparative advantage from the founded world.** Both
are true. Which one gives is a design ruling, not a re-pin decision.

**STATE OF THE TREE: `minSpacingKm = 95.2` is committed; NOTHING was re-pinned; no guard was
touched; the suite is RED on these seven and green elsewhere.**
