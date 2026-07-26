# ADR-013 — Lattice denomination, the derived yield constant, and the economic hinterland

**Status:** ACCEPTED (directed packet T3.2b, director ruling superseding the first CR-002 ruling)
**Supersedes in part:** the tuning of `farming.yieldPerFarmlandPerYear` and the code constant
`CatchmentSystem.TravelBudget`
**Relates to:** `docs/adr/cr-002.md` (the change request this resolves), ADR-005 (canonical schema),
D-009 (roads grow reach), D-016 (catchment recompute events)

---

## 1. Context — what was actually wrong

CR-002 was raised because the canonical world sat below the `canonical.densityPerArableKm2`
corridor floor. The corridor was held (a historical corridor may not be loosened because the
measured value moved), and the deviation was escalated instead. Two hypotheses were then tested by
measurement rather than argument:

1. **That the 205 km catchment radius was compensating for absent colonization** — the director's
   first diagnosis. REFUTED: correcting the radius alone left the measured density essentially
   unchanged, because population simply adapted to the smaller catchment.
2. **That the yield constant was denominated per lattice node rather than per km²** — CONFIRMED,
   and it explains both the density deviation and why the radius had to be so large.

The refutation of (1) is itself the useful finding, and it generalizes into the invariant in §4:
the equilibrium density of this model cannot be moved by geometry at all.

### 1.1 The denomination fault, precisely

`CatchmentSummaryRow.EffectiveFarmland` held

```
Σ over owned lattice nodes of  BlockFertility(node)
```

where `BlockFertility` is the **mean** agronomic-suitability index over the node's 4×4 terrain
block — a dimensionless number in [0,1]. The sum is therefore a count of *fertility-weighted
nodes*, not an area. Two consumers read that one field in two different denominations:

| consumer | what it did | denomination it assumed |
|---|---|---|
| `AutoplayMetrics` | multiplied by `blockKm2` = 256 | fertility-weighted **nodes** |
| `FarmingSystem` | multiplied by `yieldPerFarmlandPerYear` and nothing else | fertility-weighted **km²** |

Only one of them was right, and neither said which. So `yieldPerFarmlandPerYear = 28.0` was
silently *per 256 km²*: 28 ÷ 256 = 0.109 person-years of food per fertility-weighted km² per year,
i.e. about 9 km² of land to feed one person.

The fault is not the number. The fault is that a denominated quantity travelled through the state
tables under a name that did not say what it was denominated in, so the error could only be found
by someone who happened to read both consumers at once.

### 1.2 The number was not merely low — it was agronomically impossible

This check uses no disputed parameter and no reference band. At 200 kg of grain per person-year,
0.109 person-years per fertility-weighted km² is 21.9 kg of grain per km² per year, i.e. **0.219 kg
per hectare of landscape per year**. Sow 100 % of that landscape every single year — zero fallow,
zero seed retention, zero storage loss, no pasture, no woodland — and the implied sown yield is
still 0.219 kg/ha against a broadcast sowing rate of 120–200 kg/ha. The constant did not return the
seed, by a factor of roughly 700, under *any* choice of the disputed land-use parameters. As a
cross-check in the other direction, 0.109 per fertility-weighted km² is 0.058 people per real km²
at the measured in-catchment mean suitability of 0.53 — below documented temperate-forest *forager*
densities. A grain-agriculture land coefficient encoding a sub-forager density is falsified without
reference to any corridor at all.

---

## 2. Decision (a) — one denomination chokepoint

`Sim.Core/Pathing/LatticeGeometry.cs` is now the only place where lattice units and physical units
meet. It holds `BlockAreaKm2`, `ArableKm2`, `KmPerCostUnitOnIdealGround`,
`CostUnitsForIdealGroundKm` and its inverse, and its header states both unit systems once.

Three things enforce it, because a convention nobody can fail is not a fix:

1. **Names carry units.** `EffectiveArableKm2`, `YieldPerArableKm2PerYear`, `HinterlandRadiusKm`,
   `BlockMeanFertility`, `BlockArableKm2`, `TravelBudgetCostUnits`. A bare `farmland` or `budget`
   is now a review finding. `BlockFertility` was renamed to `BlockMeanFertility` specifically so
   that summing it into an area reads wrong at the call site.
2. **A grep gate** (`scripts/check-banned-constructs.sh`, alongside the determinism bans). Reading
   `TraversalLattice.KmPerNode` anywhere but `LatticeGeometry.cs` and `TraversalLattice.cs` fails
   the build — that property is the raw scale factor every conversion is built from, so a second
   reader is how a second, divergent conversion gets born. **Tests are not exempt**: a test that
   recomputes the conversion by hand agrees with a wrong implementation. A second scan fails on the
   retired identifiers (`EffectiveFarmland`, `yieldPerFarmlandPerYear`, `TravelBudget`,
   `BlockFertility`) so a bad merge cannot resurrect an ambiguous name.
3. **A physical-plausibility test** (`Denomination_CatchmentArable_IsBlockAreaTimesMeanFertility_NotNodeCount`).
   On a real founded world, `EffectiveArableKm2 ÷ (NodeCount × blockAreaKm2)` must be a mean
   fertility — in (0,1], and specifically the mean of `BlockMeanFertility` over exactly the owned
   nodes. A **missing** conversion makes that ratio ≈ 0.002; a **doubled** one makes it ≈ 135. Both
   fail loudly. The bound is asserted independently of the recomputed mean, so the test does not
   pass merely by making the same mistake as the code.

`AutoplayMetrics` no longer converts (it sums an already-denominated field).
`MigrationConfig.AttractivenessLandWeight` was divided by 256 in the same commit: a
**re-denomination, not a re-tune** — both factors are exact powers of two, so the product
`landWeight × arable` is bit-identical to its pre-T3.2b value and migration behaviour is unchanged
by this half of the packet.

---

## 3. Decision (b) — the catchment is an economic hinterland, and its radius is tuning data

`CatchmentSystem.TravelBudget = 15.0` — a code constant, in the pathfinder's internal cost units,
that no tuning pass could see — became `catchment.hinterlandRadiusKm` in `sim.json`. Two changes,
both deliberate:

- **Out of code, into TUNE data.** A constant the director cannot tune is invisible to the process
  that is supposed to catch exactly this kind of error; this one went unexamined for three
  milestones and ended up absorbing the yield fault.
- **Out of cost units, into kilometres.** "15 cost units" is not a quantity anyone can have an
  opinion about. The radius is stated as an **ideal-ground** distance and converted at the
  chokepoint, so what it actually buys is decided by geography: less through mountain and marsh,
  more along rivers and built dirt paths. That asymmetry is the mechanism by which a road network
  grows a hinterland (D-009) — it is not a modifier bolted onto one.

### 3.1 Derivation of the value

**Frame (the correction that matters most).** The catchment is the country whose produce can
profitably *flow to* the settlement. It is **not** a farmer's daily working radius. The two differ
by an order of magnitude, and conflating them is what made the old 205 km figure look absurd when
compared against the classic 5 km site-catchment radius: those are not the same quantity. Outlying
land inside a hinterland is not walked to daily from the centre; it is worked by hamlets and its
surplus is carried in.

**Assumptions, stated.**

1. *No roads at founding.* The network table is empty at turn 0 and PathBuild fills it later, so the
   founding hinterland is bounded by movement over unimproved ground.
2. *Loaded travel speed 25 km/day* for a porter or pack train on unimproved ground. (30+ km/day is
   unloaded; ox-carts on unmade ground are slower, 15–20 km/day.)
3. *The binding limit is surplus, not exhaustion.* A porter carries ~30 kg of grain and eats
   ~0.9 kg/day; a round trip to distance *d* costs 0.072·*d* kg, i.e. 0.24 % of the load per km.
   Bulk staples stop moving overland once transport eats a low-double-digit share of the load.

**Chain.** At the conventional 10–15 % edge for bulk grain, *d* = 42–63 km. Central value: **50 km**
of ideal-ground reach, i.e. two days' travel each way.

**Cross-checks, none of them an input to the chain.**

- *Von Thünen.* The intensive-grain ring around an isolated pre-railway town runs ~30–50 km.
- *Diocletian's Price Edict (301 AD).* Wagon haulage on Roman roads adds roughly 40–50 % to the
  price of wheat per 100 Roman miles (~150 km); a ~10 % addition is therefore ~35 km on *roads*. Our
  50 km is on unimproved ground, so this cross-check says the figure is generous rather than timid —
  appropriate, since the sim's own terrain cost (median passable node cost 1.11) shortens the
  realised radius to ~45 km anyway.
- *Central-place spacing.* A 50 km radius implies ~100 km between tangent centres. Bronze Age and
  early-state settlement systems space major centres 20–60 km apart, so in a *filled* landscape
  catchments will be bounded by neighbours (the T2.3 partition) rather than by budget — the correct
  behaviour. On the empty founding continent, the budget binds.

**What it buys in the instrument.** 50 km ÷ 16 km per cost unit = 3.125 cost units, against the
retired 15.0 (= 240 km ideal ground, ~205 km realised). See the lattice-stride-floor entry in
`docs/queue.md`: at 16 km per node this is ~3 nodes, coarse but not degenerate, and the lattice
cannot represent anything much smaller.

**What the sim under-models, recorded rather than patched.** Real water transport is 5–20× cheaper
than land; the sim's river corridors are cheaper but not by that margin, so riverine hinterlands are
less elongated than the historical record. That is a movement-cost question for a later packet, not
a modifier to add here.

---

## 4. Decision (c) — the equilibrium density invariant, pinned

At a land-bound, demographically stationary settlement with a stationary grain store and no trade,
harvest = arable × yield and consumption = population × meanConsumption, and the two are equal.
Therefore

```
equilibrium density  =  YieldPerArableKm2PerYear ÷ meanConsumptionPerPersonPerYear
```

people per fertility-weighted arable km². Preconditions are enumerated in the header of
`Sim.Tests/Systems/EquilibriumInvariantTests.cs` and the load-bearing one (that the land side binds)
is asserted, not assumed.

Two consequences worth stating plainly, because both are counter-intuitive and both cost this
project time:

**Geometry cannot move the density.** Catchment radius, lattice stride, block area, world size,
settlement count, fertility distribution and siting rule all change how MANY people there are;
none of them changes how DENSELY they sit. This is why correcting the travel budget alone did
nothing to the measured density, and it is the reason the density corridor is an instrument pointed
at the yield constant and at nothing else.

**In the old denomination the same identity read `yield ÷ (blockKm² × meanConsumption)`** — a
geometric constant sitting inside a purely agronomic quantity. That is the denomination bug visible
in the algebra, and nobody looked at it for three milestones.

**Which side of the Leontief min() binds is independent of the yield.** Writing *a* for the adult
share, *s* for farm share, *m* for the tool multiplier and *c* for mean consumption, labour capacity
per head is *a·s·m·*`outputPerFarmerPerYear` and realised (land-bound) harvest per head is *c*.
Raising the yield raises the equilibrium population, which raises both sides in the same proportion,
so the yield cancels: land binds whenever *a·s·m·*`outputPerFarmerPerYear` > *c*, at every yield.
With the canonical values (a ≈ 0.57, s = 1.0, m = 1.0, output = 5.0, c ≈ 0.84) the labour side has
~3.4× headroom, so the world is land-bound at equilibrium — before and after this packet alike,
which is why the denomination error never announced itself as a regime change.

---

## 4b. Decision (d) — the yield constant, derived

`farming.yieldPerFarmlandPerYear` = 28.0 (per fertility-weighted lattice NODE) became
`farming.yieldPerArableKm2PerYear` = **26.0** (per fertility-weighted km²). Note that this is not a
re-denomination of the same number: 28.0/node is 0.109/km², so the constant moved by ~240× on top
of the unit change. §1.2 shows why the old value could not stand at any denomination.

**The reference class was derived from the sim, not chosen off a shelf.** Four independent
reference-class derivations (swidden/long-fallow 16.04; temperate LBK 12.94, corrected to ~24.4 by
the units attack; Near Eastern rainfed 37.5; medieval NW-European 152.7, an explicit out-of-class
ceiling) spanned 12×, and averaging them would have been a fit dressed as a derivation. What
settled it was reading `Worldgen` step 6: `fertility = clamp(tempSuit × moisture ×
1.6-if-river-adjacent, 0, 1)` with `moisture = 1/(1 + d_water_px/40)` and 4 km pixels, so **f = 1.0
is attainable only within 8 km of a river channel and at a mean annual temperature in
[8.4, 23.6] °C**. The sim's ideal square kilometre is river-valley floor and first terrace, not
average upland, and every land-use share must be a share of that landscape.

Chain, per f = 1.0 km² = 100 ha per year:

| step | value |
|---|---|
| gross sown yield, long-run mean incl. failure years (valley bottom) | 770 kg/ha |
| less broadcast seed 120 kg/ha (seed:yield 6.4 : 1, inside the 5–10 : 1 rainfed band) | 650 kg/ha |
| × 0.90 storage, vermin and threshing loss | 585 kg edible per sown ha |
| cropped share of the territory at saturation, k (deducting channel/floodplain 12 ha, slope and terrace break 15, pasture for the traction and manure herd 25, woodland 15, settlement 5) | 0.28 |
| sown share of the rotation (medium fallow, crop 2 years in 7) | 0.2857 |
| ⇒ sown area | 8.00 ha per f = 1.0 km² |
| ⇒ edible grain | 4 680 kg |
| ÷ 180 kg clean grain per adult-year (2 200 kcal/day, cereals 75 % of calories, 3 390 kcal/kg) | **26.0 food units** |

Inverse checks, none of them an input: 0.88 arable ha per average person, against Wilkinson's and
Halstead's 0.5–1.0 for pre-mechanised rainfed (inside, mid-range); 3.14 ha of ideal land per person,
against the retired constant's 910. Sensitivity: k alone 16.7–37.1; fallow alone 14.0–45.5; all
parameters at their simultaneous endpoints 5.5–133.8.

**Three adversarial attacks, all of which refuted their own claim.** *Fitting*: no fitting
signature — the four span 11.8×, all four overshoot every reference band they were shown, the
dominant free parameter was not pushed upward in any of them, the conclusion survives every chain's
own pessimistic endpoint, and the CR-002 cancellation identity was checked for specifically and is
absent. *Units*: all four chains reproduce exactly, hectares↔km² and the per-node factor are each
applied once, fertility weighting once and never twice; one real error found and corrected (the LBK
chain used a real-land arable ratio as an ideal-fertility ratio, a 1.89× understatement).
*Labour interaction*: this one did not refute the derivations but produced the packet's most
important finding — see below.

**The world is OUT OF CORRIDOR at this value, and that is escalated rather than absorbed.** The
labour-interaction attack ran the real kernel and found that the world never reaches its food
ceiling inside the 6 000-year campaign, so no agronomically defensible yield satisfies the Malthus
corridors — they need ≤ 6.2 and realistically ≈ 1.6. That is `docs/adr/cr-003.md`, which is OPEN and
which this ADR does not pre-empt.

One caveat from the synthesis is worth repeating here verbatim in substance, because it is the same
fork CR-003 asks the director to settle: in a Leontief `min()` the land term is properly a CAPACITY
ceiling, with realised production falling out of the labour term — but because the land-vs-labour
ratio is yield-free and greater than one (§4), **the land ceiling IS the realised state in this
model**. The constant is therefore doing two incompatible jobs at once. A strict capacity reading
(k ≈ 0.40–0.55) would land at 70–120 people per fertility-weighted km²; the realised-landscape
reading taken here lands at 31.8. Which one is correct is a design decision, not an agronomic one.

## 5. Consequences

- `CatchmentSummaryRow` changed field name (not width or type), so the canonical schema hash and
  every golden move. Re-pinned once, itemized in the packet report.
- `SimConfig` gained a required `catchment` section and renamed a required `farming` key; worldgen's
  `siting.minSpacingTravel` became `siting.minSpacingKm` (behaviour-preserving: 30 cost units ×
  16 km = 480 km, both factors exact powers of two) and `migration.dampingDecayCost` became
  `dampingDecayCostUnits` — a rename only, because that one damps travel EFFORT rather than map
  distance and cost units are the right denomination for it. An old `sim.json` or `worldgen.json`
  now fails the load loudly with an actionable message, which is the intended behaviour for a
  denomination change: silently binding an old key to a new meaning would be a 256× error.
- `SystemCatalog.Catchment()` now takes `SimConfig`. The system remains stateless.
- The density corridor is unchanged at `[0.15, 0.6]` and the world's relationship to it is reported
  honestly in the packet report rather than negotiated here.
