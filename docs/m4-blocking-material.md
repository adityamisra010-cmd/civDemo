# M4 — BLOCKING MATERIAL

Findings that the M4 spec **cannot ship without answering**. Not queue items: the spec is
incomplete until each has a stated resolution. Raised under the S8 §4.1 discipline that a
milestone spec's foundations are audited before its packets are written.

> **Cross-reference (GOV-2, 2026-07-30):** `docs/m4-pre-spec-dependencies.md` — the pre-spec
> dependency filing, input to the M4 spec's S8 §4.1 coupling map: two director rulings first
> recorded there (money: M5 taxes in kind; notables: split by role, generals at M4), the stale
> system-inventory reconciliation, four known items with the milestone they bite in (incl. B-2
> below), the T3.6 trade-instantaneity timing note, and the ruled-whole M4 scope list.

---

## B-1 — ~~Settlement spacing caps the continent at nine sites~~ **CORRECTED: the claim was FALSE** (T3.4b review, 2026-07-27)

**The original B-1 below is retained for the record and is WRONG.** Verified across 20 seeds on the
canonical production worldgen: **10 of 10 seeds place 12 settlements**, and greedy capacity at
`minSpacingKm 480` is **min 33, median 45, max 74**. Not nine. The operative sentence — *"there is
nowhere to put the tenth"* — is false; there is room for 21 to 62 more.

The spacing threshold is identical on both maps (480 km = exactly 30.0 cost units on each). The
"could only place 9 of 12" exception comes **only from the 256 px dev preset**, and the provenance
was a reasoning error that hardened into a documentation one: the measurement sentence said "measured
on the dev world", the claim sentence dropped the qualifier, and the inference was never re-tested
against the shipped map before being promoted here.

**What survives, and it is a different item:** saturation is real at **~33**, not 9. If M4
colonization is expected to reach that, CR-003's map-exhaustion argument applies unchanged — at 33.
At the shipped N=12 the spacing rule is nowhere near binding.

## B-1b — THE DEV PRESET IS NOT A SCALE MODEL OF THE SHIPPED WORLD (T3.4b review; the finding underneath B-1)

`SizePx = 256` holds `kmPerPx` at 4.0 while worldgen's noise frequencies stay in normalised
coordinates, so the same continent is compressed into **¼ the linear extent**. Measured against
canonical 1024 px: land movement cost **+37%**, site packing **3.1× denser per km² of land**, river
cells per land cell **4.4×**, mean fertility **+48%**. A 480 km exclusion buys ~166 km of realised
separation on dev against ~450 km on production.

**46 call sites across 27 test files calibrate on `DevWorldgen()`**, against 11 using the canonical
config. **Every corridor constant fitted on that preset is fitted to a world the game never
generates.** That is an S8 §4.1 foundations-audit item in its own right, broader than spacing, and
B-1 was its first evidence without recognising it.

## B-2 — UNBOUNDED GRAIN ACCUMULATION IS WHAT STANDS BETWEEN THIS WORLD AND FAMINE (director ruling, 2026-07-27)

**Same weight as B-1.** Director's words:

> UNBOUNDED GRAIN ACCUMULATION, not the closing of the land frontier, is what stands between this
> world and famine. Measured: reserves stabilise at ~2,900 years of consumption; a world at 6,000
> years still runs harvest/demand 2.29. CR-003's frontier-closure reasoning identified the wrong
> constraint. M4 cannot ship without answering how stores are bounded — spoilage, storage capacity,
> alternative uses (seed, feed, brewing, trade), or consumption that scales with wealth. Measured
> thresholds: ~180 years of store gives first starvation, ~65 years gives mature-world chronicle
> famine.

Supporting measurement is in `docs/t3.4b-review-record.md` §2 and `docs/adr/cr-003.md` §6. Note the
shape of the answer matters as much as the answer: store bounding must EMERGE from a mechanism with
a physical carrier (grain rots; a granary holds only so much; seed corn and fodder are real claims on
the harvest), never from a cap chosen to make famine appear. CR-003 §5.1's prohibition on choosing a
constant that reproduces the old crash applies to this with full force.

### B-2a — THE MECHANISM, STAGED (director ruling, 2026-07-27)

Store bounding lands in **two layers, base first**. This constrains the M4 store-bounding packet when
it is written; **nothing here is acted on in M3.**

**BASE LAYER — the M4 packet:**

1. **SPOILAGE** — a fraction of stored grain lost per sim-year, integrated dt-correctly.
   *Physical carrier:* decay. Grain rots, is eaten by vermin, germinates, moulds.
2. **GRANARY CAPACITY** — a per-settlement ceiling on what can be held, above which surplus is lost
   or not stored. *Physical carrier:* a structure of finite size.

Both **DERIVED, not chosen** (S8 §4.1): state the reference class for pre-modern grain loss rates and
for granary capacity per household, and report what reserve depth the derived values produce.

**COMPARISON TARGET, NOT A TARGET TO HIT:** historically, agrarian households carried on the order of
one to a few years of grain. **If the derived values produce something far from that, report it as a
finding rather than adjusting to reach it.** CR-003 §5.1 applies with full force — no constant chosen
to make famine appear. (This is the same fence as the T3.4c corridor prediction, and ADR-015 §7.13
applies: pre-commit the reading of both outcomes before measuring.)

**ENRICHMENT LAYER — later.** Each item must state its physical carrier and act by **MODIFYING the
base spoilage rate or capacity, never by adding an independent term** (law 2). The list is the
director's:

| item | carrier | note |
| --- | --- | --- |
| **STORAGE TECHNOLOGY** — pit, sealed jar, raised granary, silo | a built structure with a construction cost and a spoilage multiplier | **Most valuable of the list, and the strongest candidate to follow the base layer:** resilience becomes a real investment decision and a genuine technology arc across eras |
| **MOISTURE** — damp regions spoil faster | the worldgen moisture field (T1.1/T3.1) | **Nearly free — the carrier already exists** |
| **VERMIN** — rat losses scaling with settlement size and storage type | settlement density | arrives with T3.8's settlement-size work |
| **HYGIENE / STORE MAINTENANCE** — labour spent maintaining stores reduces loss | the labour pool | already simulated; couples naturally to sector allocation |
| **SEED CORN** — a claim on the harvest not available for consumption | next year's sowing | **Distinct from spoilage: a reservation, not a loss** — and it makes a bad year compound into the next one |
| **ALTERNATIVE USES** — feed, brewing, trade | competing demand from other systems | arrives naturally with livestock and the goods economy |

*Provenance: the enrichment list is the director's. **The staging — not the content — is the
ruling.***

### B-2b — THE SEQUENCING RULE

> **WHEN A BASE QUANTITY IS WRONG, ADDING DETAIL ON TOP MAKES THE ERROR HARDER TO FIND, NOT EASIER.**

Recorded because it has now been **paid for twice**: T3.2b (a yield constant denominated per lattice
node while consumed per km², with the travel budget compensating for it) and T3.4b/c (a variance
defect whose 3.2% yield lift was invisible under six interacting systems and no distributional test).

Shipping vermin, humidity and storage styles before spoilage exists would leave the world still
unable to starve, **with the cause now distributed across six systems instead of one.** Base mechanism
first, measured; then enrichment, **each item earning its place by measurably changing the base.**

---

## B-1 (original text, retained — see the correction above)


**The finding.** `minSpacingKm` is 480 (T3.2b). The dev continent admits at most **nine**
settlements: `settlement siting could only place 9 of 12 sites at minSpacingKm 480 — terrain too
small or spacing too large`. The 12-settlement world used as the pre-T3.2b migration baseline is
no longer constructible.

**Why this is blocking rather than a queue line.** M4's colonization packet means **founding new
settlements**, and there is nowhere to put the tenth. That leaves three possibilities, and the
spec must choose one:

1. **Spacing becomes colonization-aware** — `minSpacingKm` is a worldgen siting rule today; it
   would become a *founding* rule with different semantics (a daughter colony sited near its
   parent is the normal historical pattern, not a violation). This is the option that makes
   colonization mean what it says.
2. **The continent grows** — a larger terrain raster, which moves every worldgen golden and
   re-opens the T3.2b spatial calibration.
3. **Expansion saturates at nine** — and this is the one the spec must not choose by default,
   because of what it implies below.

**The reason it matters, in the terms CR-003 already settled.** If expansion saturates at nine
sites, population growth eventually presses against a land supply that cannot extend, and the
Malthusian transition arrives by **MAP EXHAUSTION rather than by land filling** — the trap
hardwired by geometry. CR-003's ruling is explicit that the trap must EMERGE when land fills and
"must never be hardwired, and it must never be restored by choosing a constant that reproduces
the old crash." A spacing constant that caps the world at nine settlements is exactly such a
constant, arriving through the back door: it would not look like a hardwired Malthus, it would
look like a full continent, and the distinction would be invisible in every metric the
calibration battery measures.

**What the spec must state.** Which of the three options is taken; if (1), the founding-rule
semantics and how they differ from siting; if (2), the golden and calibration blast radius; if
(3), an explicit argument for why saturation-by-geometry is acceptable and how it is
distinguished, in the metrics, from land genuinely filling.

**Evidence:** `docs/t3.4b-migration-evidence.md` (addendum), `docs/adr/cr-003.md`,
`Sim.Core/Worldgen/SettlementSiting.cs:180`.

### B-2 addendum (T3.5b, 2026-07-28) — the granary masks a 45% labour perturbation

Item 1b's pre-committed reading 2 fired and is escalated here as directed. The derived subsistence
mix cut farm labour 45%. Measured consequence on canonical seeds 1/2 at 650 turns: **population
±0.2%** (120,811/120,558 → 120,588/120,585 — CR-003 §2.3's yield-insensitivity confirmed at head),
starvation 0, grain accumulation slowed −59% (303M → 125M) with **~1,240 years of reserve
remaining**. A 45% cut to food labour is invisible to every demographic observable because the
unbounded granary absorbs it — the store is not merely deep, it is deep enough to hide
labour-allocation policy itself. Store bounding (B-2a) is what makes sector allocation a decision
with consequences; until it lands, no M3/M4 mechanism that routes through food scarcity can be
calibrated against the shipped world.
