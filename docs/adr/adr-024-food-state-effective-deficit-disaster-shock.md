# ADR-024 — FOOD STATE, EFFECTIVE DEFICIT, AND THE DISASTER SHOCK (schema v25)

**Status: accepted under CR-015; implementation T4.21-1** (state and shock plumbing; `hazardPerYear`
shipped at 0 so the packet's golden move is layout + RngStreams only) **and T4.21-4** (arming
λ = 0.01, the last golden move). Ruling: `docs/adr/cr-015-famine-is-exceptional.md` §6 (mandate
2026-09-17). Spec: `docs/t4.21-architecture.md` §3.1–§3.3, §3.7–§3.10, §6.1–§6.2.

**ADR number:** 024. `adr-023-food-variety-is-d035a-only.md` is the highest on `main`; 019 lives
only on an unmerged branch and the sequence deliberately jumps 018 → 020 (ADR-021 precedent); no
`adr-024..029` exists on any of the 87 branches (2026-09-17).

> Famine is a DERIVED state, never a stock: `FoodState.Of(prev, s)` is FAMINE iff the food balance
> shows a shortfall (`d > 0`) AND an exceptional cause is present — a famine-class disaster row
> applied to the harvest that produced the deficit, or productive abandonment in force. Below that,
> one boundary `a` separates FOOD STRESS (adaptation absorbs the cut; no starvation) from SEVERE
> FOOD STRESS (the unabsorbed remainder starves). The disaster is a PRODUCTION shock in weather's
> class — a serialized `DisasterRow` that multiplies the two food paths weather already multiplies
> — sized by derivation to be able to exhaust a settlement's effective buffer at the coarsest era
> dt alone. `CanonicalSchema` goes v24 → v25.

---

## §1 WHAT THIS ADR AMENDS (quoted), AND WHAT IT DOES NOT CHANGE

**Amends (CR-015 N1):**

- `docs/m3-spec.md:52`: *"Famine must EMERGE from bad years against thin stores: no famine
  trigger, threshold or event that fires independently of the food balance."* → the clause now
  reads "… independently of the food balance AND of an exceptional cause" (CR-015 §6.2 N1). `d > 0`
  remains NECESSARY for FAMINE; the cause qualifier changes the response FORM (no adaptation), never
  the magnitude, which is the food balance's own `d`.
- `docs/adr/cr-003.md:280-283` (§5.2(b)): *"famine emerges from the food balance, never from a
  trigger"* → CR-003 §9 (appended) records the premise amendment. "Never from a trigger" survives
  in its substance: neither the disaster row nor the abandonment row IS a famine; both are causes
  that the balance must confirm.
- `Sim.Core/Systems/Harvest/HarvestWeatherSystem.cs:21-28` doc block: *"whether that becomes famine
  is decided downstream by the food balance alone"* → "decided downstream by the food balance and,
  for the FAMINE label, by `FoodState`'s cause qualifier; this system still has no threshold, no
  event, no famine flag, and no read of population, stores or deficits" (one doc line; no code).
- `Sim.Core/Kernel/CanonicalSchema.cs:95` `Version = 24` → **25** (§5).
- `SystemCatalog`: `WellKnownId = 23` (`"disaster"`); 17 is on unmerged `t4.13`, 22 is
  `GovernanceSystem` on unmerged `m5-full-build`.

**Does NOT change:** `ConsumptionSystem` (no line; the deficit and one-directional substitution
are untouched — CR-015 N8(a), G6(a)); `HarvestWeatherSystem`'s model, σ, τ, the AR(1) state, the
spatial blend, the mean-one property (G1 escalated; weather byte-identical); the derived **26.0**
(the disaster multiplies OUTPUT, never land); the stock model, spoilage and the granary cap (T4.2);
extraction (exempt from both multipliers, as today); the absent-row-is-1.0 rule; `RngRegistry`
semantics (new streams only); the pipeline position of everything already in it.

---

## §2 MECHANISMS

### 2.1 `FoodState` — the derived four-state classification (`Sim.Core/State/FoodState.cs`, new; static, pure, non-serialized; sibling of `SettlementHappiness`)

```
enum FoodStateKind { Normal = 0, Stress = 1, Severe = 2, Famine = 3 }
enum FamineReason  { None = 0, Disaster = 1, Abandonment = 2, Both = 3 }

Of(IReadOnlyWorldState w, SettlementId s, SimConfig cfg, out FamineReason reason):
    d       = w.ConsumptionDeficits[s].DeficitRatio          (absent row → 0)
    struck  = w.Disasters[s].AppliedMultiplier < 1.0          (absent row → false)
    abandon = IsAbandoned(w, s)
    if (d > 0 && (struck || abandon)) → Famine   reason ∈ {Disaster, Abandonment, Both}
    if (d > a)                        → Severe   // adaptation exhausted: starvation begins
    if (d > 0)                        → Stress   // adaptation absorbs the cut
    else                              → Normal
SevereThreshold(cfg) = cfg.FoodState.AdaptationAbsorbableShortfall = a
IsAbandoned(w, s): row = w.SectorAllocations[s] (absent → Sectors.Default(s), exactly as
    ProductionSystem.cs:148-153); return Raw(Farming) == 0.0 && Raw(Herding) == 0.0
```

- **One boundary carries the kernel semantics:** `θ_sev := a`, so the label predicts the response —
  STRESS ⇒ starvation hazard identically 0; SEVERE ⇒ starvation on the unabsorbed remainder;
  FAMINE ⇒ starvation on the whole deficit. The kernel's birth full-stop is a boundary INSIDE
  SEVERE (nominal `d = a + (1 − a)/3 = 0.4667` under CR-015 G3(b); `1/3` inside FAMINE), a kernel
  fact, not a state.
- **Abandonment predicate** reads the RAW weights of the row IN FORCE for the step — the same row
  and fallback that make farm labour and the herding pool zero (`ProductionSystem.cs:155-158,
  :175-179`), so "deliberate abandonment" and "food labour is zero" are one fact. The all-zero row
  (hand-written logs only; `SectorOrderFactory.cs:98-109` refuses it) classifies as abandoned
  (CR-015 §6.3). Legacy `LaborAllocation` pct = 0 (`PathBuildSystem.cs:80-88`, writes Farming 0 /
  Herding 0 / Construction 1.0) IS abandonment, so `CollapseStabilityTests`, the FirstReign fixture
  and the skipped `FamineAtOneOfTwelve` rig are FAMINE rigs without re-rigging. The single-sector
  cases are NOT abandonment (director's rule); the pure pastoralist classifies SEVERE (G6(a)).

**Timing (one-turn lag, no state).** Order at turn `t` → row in state `t+1` (PathBuild is last) →
zero harvest at step `t+1→t+2` → `d(t+2) > 0` once the store is short → FAMINE at step `t+2→t+3`.
Restore at `u` → row `u+1` restored → SEVERE/STRESS (adapted) at `u+1→u+2` → NORMAL after the first
restored harvest. The disaster branch reads the multiplier APPLIED to the harvest that produced `d`
(`AppliedMultiplier`), so a strike at `t` classifies FAMINE at `t+2→t+3`, the same turn its deficit
first appears. A policy is read as IN FORCE (forward-looking); an event as APPLIED (backward-
looking); both keep `d > 0` necessary. Pinned by `F_AbandonmentTiming_TurnExact`,
`F_DisasterTiming_TurnExact`.

### 2.2 Effective deficit — adaptation

```
EffectiveDeficit(d, state, cfg):
    if (state == Famine) return d                       // no adaptation possible
    a = cfg.FoodState.AdaptationAbsorbableShortfall      // 0.20
    return d <= a ? 0.0 : (d − a) / (1.0 − a)            // dead-zone; exactly 1 at d = 1
```

Form: DEAD-ZONE (spec §3.2 gives the four reasons; the smooth forms `d²/(d+a)` and
`d²/(d + a(1−d))` were rejected — the first has `d_eff(1) < 1`, both starve 11–13 % of adults per
decade at a survivable cut). Null arm `a = 0` is EXACT (`(d − 0)/1 = d` bit-for-bit). Readers
(CR-015 G3(b)): starvation mortality AND fertility suppression outside FAMINE (ADR-026); NOT the
flight hazard or the B1 readout (ADR-025 — nominal `d`); NOT viability, happiness, appropriation,
ClassMobility, the rebound release gate (nominal `d`, byte-identical). Worked values at `a = 0.2`:
`d` 0.10 → 0; 0.20 → 0; 0.25 → 0.0625; 0.30 → 0.125; 0.40 → 0.25; 1.0 → 1.0. Adult starvation per
decade `1 − e^{−0.12·d_eff·10}`: STRESS → 0 (today 21 % at `d = 0.20`); SEVERE `d = 0.30` → 14 %
(today 30 %); FAMINE `d = 0.35` → 34 %.

### 2.3 `DisasterSystem` — the explicit famine-class production shock (`Sim.Core/Systems/Disaster/`, new, stateless; `WellKnownId = 23`; registered in `SystemCatalog.All()` and `pipeline.json` immediately after `"harvestweather"`)

**Row** (serialized, appended after `Structures`; width 4+4+8+8+8+8 = **40 bytes**):

```
record struct DisasterRow(
    SettlementId Settlement,
    int    Kind,               // 1 = crop failure; 0 reserved
    double Severity,           // fraction of FOOD output lost per ACTIVE year, [s_min, s_max]
    double RemainingYears,     // years still ahead AFTER this step's overlap; 0 when run its course
    double Multiplier,         // what Production multiplies food rates by in the step that READS this row
    double AppliedMultiplier)  // what Production multiplied by in the step that WROTE it (= prev.Multiplier, or 1.0)
```

Rows exist only where `Multiplier < 1 ∨ AppliedMultiplier < 1 ∨ RemainingYears > 0`; the table is
rebuilt every step (`TradeFlows` precedent); absent row ≡ (1.0, 1.0, 0).

**Step**, per settlement in table row order, all from prev:

```
p = 1 − exp(−λ·dt)
for each settlement s (row order):
    uH = rng(s).NextDouble(); uS = rng(s).NextDouble()    // ALWAYS both: λ = 0 leaves RngStreams bit-identical to λ > 0 with no strike
    applied = prev.Disasters[s].Multiplier (absent → 1.0)
    if (prev.RemainingYears > 0)  severity, remaining = prev's           // active event continues; no new onset while active
    else if (uH < p)              severity = s_min + (s_max − s_min)·uS; remaining = D
    else                          { if (applied < 1.0) write (s, 0, 0, 0, 1.0, applied); continue; }
    overlap = min(remaining, dt); multiplier = 1 − severity·overlap/dt; remaining −= overlap
    write (s, 1, severity, remaining, multiplier, applied)
```

**Production reads it where it reads weather:** `foodMultiplier = HarvestWeatherFor(prev, s) ×
DisasterFor(prev, s)` once per settlement; `Farm` (`ProductionSystem.cs:239`) and
`FromDeposits(foodSector: true)` (`:300-306`) take it in weather's position; extraction keeps 1.0.
`x × 1.0 == x` bit-for-bit, so every world without a strike produces identically.

**What the disaster IS and IS NOT.** It multiplies the same two food paths weather multiplies
(farming + herding/fishing — CR-015 G5); it leaves 26.0 a statement about land; it does not touch
stores (a "granary loss" kind is queued, G8(b) not authorized); it does not read population, stores
or deficits — like weather, it cannot decide famine; `FoodState` does, from the balance. Every row
of the single kind is famine-class by the derivation in §3, so `FoodState.struck` needs no per-row
physics test; a second kind with sub-class severities must add a per-row predicate
(`Severity·D·ρ_ship ≥ B_eff`) — queued with the kind registry. Spatial correlation and a
weather-conditioned hazard are DEFERRED (queue). Placement after `harvestweather` rather than in the
Spine's late "events/crises" slot is equivalent under the one-turn lag and fixes the turn-1
RngStreams creation order; the Spine slot is not moved.

---

## §3 CONSTANTS (new `foodState` and `disaster` sections of `sim.json`; all TUNE)

| constant | value | CHOSEN / DERIVED — frame | null / identity arm |
| --- | --- | --- | --- |
| `foodState.adaptationAbsorbableShortfall` `a` | 0.20 | CHOSEN — physiology: Minnesota Starvation Experiment (~50 % cut, 24 weeks, no deaths — an upper bound); WWII civilian rationing 15–25 % cuts (no famine mortality); Dutch Hunger Winter / post-war German rations 40–70 % (excess mortality). The director's 15–25 % band, midpoint. Validated `[0, 1)`. `a = 1/3` (dead zone = the band in which births still occur) recorded as the aligned TUNE alternative | `a = 0` reproduces today's linear response bit-for-bit |
| `disaster.hazardPerYear` λ | 0.01 (T4.21-1 ships **0**; T4.21-4 arms 0.01) | CHOSEN — one famine-class local crop failure per settlement per century. Reference class: England 1300–1700 ≈ 5–6 famine-class events / 400 y (≈ 1/70 y); France by région 1500–1800 (Le Roy Ladurie) 1/50–1/100 y; a single hinterland sees fewer than a région. Band 1/50–1/150; round central value. 9.5 % per settlement per decade. Validated ≥ 0 | λ = 0: no row ever; the world differs from a no-disaster world by RngStreams rows only — the attribution control |
| `disaster.durationYears` D | 5.0 | CHOSEN inside the historical band 3–7 y (1315–22 incl. the murrain; 1601–03; 1695–97; the 1690s "seven ill years"; 1845–49), at the length the derivation below requires. Validated > 0 | D → 0 is a no-op disaster |
| `disaster.severityMin` / `Max` | 0.75 / 1.0 | `s_min` DERIVED (below); `s_max` historical upper (Irish potato 1846 ≈ 25 % of normal; 1601–03 near-total locally). Uniform on the band. Validated `0 ≤ s_min ≤ s_max ≤ 1` (`s_max > 1` would hand `Ledger.Flow` a negative source) | — |

**The `s_min` derivation (against the effective buffer at the coarsest dt).** At dt = 10 the balance
pools the decade's surplus (F4), so a settlement with surplus ratio `ρ` has an effective buffer
`B_eff = G + dt·(ρ − 1)` years of demand; a loss of `s·D` production-years produces a deficit iff
`s·D > (dt_max(ρ_ship − 1) + G)/ρ_ship`. With `dt_max = 10`, `G = 1.5` and **`ρ_ship = 1.3` DECLARED**
(Libur's weather-mean grain surplus ratio in the director's playtest series, `libur-resources.txt`
rows 105–116, harvest/eaten 1.03–1.49; CR-015 G8(a)) the threshold is **3.46 production-years**;
`D = 5, s_min = 0.75` gives `s·D ∈ [3.75, 5.0]`. A DIMENSIONAL derivation — the event's magnitude
must exceed a declared buffer — not a corridor fit: no outcome is targeted and `ρ_ship` enters only
as the buffer size. Re-runnable from the playtest series by anyone (spec §3.9).

**Where the band sits relative to weather.** A yearly multiplier of 0.25 is `z = (ln 0.25 +
σ²/2)/σ = −4.57` under the shipped yearly lognormal — once per ≈ 400 000 settlement-years; "not
every extreme draw is a disaster" holds by construction at the YEAR scale. At the DECADE scale under
F1-unfixed weather a 5th-percentile decade (0.59) overlaps the band's decade multiplier
`1 − s·D/10 ∈ [0.5, 0.625]` — the CAUSE row distinguishes them there (CR-015 §6.4).

**Deficit produced by one event (weather = 1)** — dt 10: `d = ρ(sD − sD*)/10`, `sD* = (10(ρ−1) +
G)/ρ`; dt 0.5: the store lasts `G/(1 − ρ(1−s))` years, then `d = 1 − ρ(1−s)` per turn:

| `ρ` | dt | `s = 0.75` | `s = 1.0` | adult starvation over the event (FAMINE, `d_eff = d`) |
| --- | --- | --- | --- | --- |
| 1.0 | 10 | `d = 0.225` | `d = 0.35` | 24 % / 34 % |
| 1.3 | 10 | `d = 0.038` | `d = 0.20` | 4.5 % / 21 % |
| 1.5 | 10 | 0 (absorbed, NORMAL) | `d = 0.10` | 0 / 11 % |
| 2.1 | 10 | 0 | 0 | 0 — a rich granary survives; the balance decides |
| 1.3 | 0.5 | `d = 0.675` for 2.8 y | `d = 1.0` for 3.5 y | 20 % / 34 % |
| 2.1 | 0.5 | `d = 0.475` for 1.8 y | `d = 1.0` for 3.5 y | 10 % / 34 % |

The dt-10 and dt-0.5 rows of ONE physical event DIFFER — the inherited F4 artefact (CR-015 G8(a)).
`D_Classification_DtDifference_Pinned` pins the difference for `ρ = 1.3`; nothing claims they agree.

---

## §4 dt-CORRECTNESS, DETERMINISM, ISOLATION

- **dt-correctness (law 3).** `P = 1 − exp(−λ·dt)` composes exactly; `severity × overlap`
  integrates the loss against the years actually inside the turn, so a 5-year failure costs `5·s`
  years of food at dt = 10 (one turn, multiplier `1 − 0.5·s`) and at dt = 0.5 (ten turns at
  `1 − s`) alike — `D_DtExact_FiveYearFailure`. `RemainingYears` is persisted BY DIMENSION (an event
  longer than a late-era turn), not as stress history — the one place "no serialized famine-stress
  state" is deliberately not read as "no serialized disaster". **Accepted onset biases** (CR-015
  §6.3): at dt 10 at most ONE onset per turn — `P(≥ 2 onsets in 10 y) = 1 − e^{−0.1}(1 + 0.1) =
  0.47 %` of decades ≈ 4.7 % of onsets truncated; at dt 0.5 a 5-year refractory, effective rate
  `λ/(1 + λD) = 0.952 λ` ≈ 4.8 % rate bias. Both ≈ 5 %, opposite in mechanism, bounded by
  `D_OnsetProcess_BiasesWithinStatement`; the exact renewal is queued.
- **Determinism (law 5).** Per-settlement RNG streams from `RngRegistry`, state in `WorldState`
  (RngStreams gains one row per settlement); both draws taken every turn regardless of outcome;
  table-order scans; no dictionaries, no LINQ, no floats; `InvariantCulture` for the config.
  Replay equality with λ > 0: `D_Replay_Deterministic`; save/load with an active row:
  `D_PersistsAcrossSaveLoad`.
- **Isolation (law 6).** `DisasterSystem` reads only prev and writes only its table; Production
  reads the row as it reads weather; `FoodState` is a static on `IReadOnlyWorldState` that crosses
  no isolation boundary (the `SettlementHappiness.Of` pattern) and names neither `Grievances` nor
  `NeedSatisfactions` (the read-isolation gate).
- **Conservation (law 1).** The disaster changes RATES before `Ledger.Flow` credits production;
  no stock is touched directly; `s_max ≤ 1` keeps every source non-negative.

---

## §5 SCHEMA v24 → v25 — the three-edit rule and the collision note

`CanonicalSchema.cs:14-16`: *"ADDING STATE? Three edits, same file: (1) write in Write, (2) read in
Read, (3) width in ExpectedLength."* `DisasterRow` (40 bytes) is appended after `Structures`;
`Version = 25`; `WorldState` gains the table at its 8 clone/equality/serialize sites;
`PathBuildSystem.cs:355` forwards it; `WorldStates.TableEquals` and `Sim.Cli/SnapshotDiff.cs` learn
the layout; `Assert.Equal(24, Version)` ×4 and one JSON literal move to 25.
**`SchemaV25_PopulatedDisasterTable`** is the POPULATED-table test: three rows with distinct dyadic
values, exact `ExpectedLength`, bit-exact round-trip, hash equality (empty-table coverage proves
nothing — T1.1/T1.3 precedent). v24 UI saves become unloadable; no migration path is promised
(spec §9 R4).

**COLLISION NOTE.** `origin/m5-full-build` also bumps to **v25** (`TaxPolicies`) and edits
`ProductionSystem.cs:239/:306` (the exact weather-application lines this ADR touches) and deletes
`Sim.Cli/SnapshotDiff.cs`. *"There is exactly one meaning of every version number in this file"*
(`CanonicalSchema.cs:93-94`; v22/v23 precedent). **Whichever branch lands on `main` second
renumbers**: if T4.21-1 merges first, `m5-full-build` must take v26 when it rebases; textual
conflicts are expected at exactly those lines and are resolved by re-measurement on the merged
tree, never by merging golden digits.

---

## §6 BIT-IDENTITY LEDGER (this ADR's rows)

| regime | identical? | why |
| --- | --- | --- |
| production without a strike | YES | `× 1.0` |
| every snapshot | NO (layout) | v25 prefix; RngStreams +1 row per settlement — the attribution control (`IntegratedPinAttribution.HashWithoutM4` extended with a strip of the disaster streams + table) reproduces the pre-packet pins BYTE-FOR-BYTE at λ = 0 |
| `Consumption`, `Price`, `Trade`, `Housing`, `Construction`, `Appropriation`, `ClassMobility`, `Revolt`, `NeedsGrievance`, `PathBuild`, `Catchment`, `HarvestWeather` | YES | untouched code; read nominal `d` where they read it today |
| `FoodState` at `d = 0` | NORMAL, `d_eff = 0` | identically today's "no deficit" |

---

## §7 S8 §4.1 ITEMS FOR THIS PIECE

- **Foundations audit** (T4.21-1 authoritative over spec §2): `DeficitRatio`, `DemandUnits`,
  `HarvestWeatherRow.Multiplier` (F1 — escalated), `sigmaLogYield`, `correlationTimeYears`,
  `granaryYearsOfDemand` (F4), `SectorAllocationRow` raw weights, `dtYears`.
  **The measured part (F4/F5) is `docs/t4.21-1-foundations-audit.md`** (added by the T4.21-1 fix
  lane on the verifier's material finding): the per-settlement `ρ = grain LastProduced ÷ DemandUnits`
  series for seed 42 with and without the director's 15 orders and for seeds 1–3, `B_eff` and `sD*`
  per settlement, the per-decade exposure fractions, and the F5 feedable limit against the sum. Its
  headline facts: every canonical settlement's time-mean `ρ` is 1.78–2.02 (`sD*` 5.22–5.79 > the
  band's maximal 5.0), so without orders no settlement is exposed to the band in a mean-weather
  decade; Libur under the director's orders (`ρ` 1.43, `sD*` 4.06) is the one settlement exposed at
  its mean; exposure otherwise arrives through below-mean weather decades (`ρ_t ≤ 1.70` in 31–51 %
  of decades per settlement). `ρ_ship = 1.3` sits below every time-mean and at Libur-under-orders'
  median. The band is unchanged by this record; it is reported for the director's judgement (G8).
- **Dimensional declaration:** `d`, `d_eff`, `a`, `Multiplier`, `AppliedMultiplier` dimensionless;
  λ per sim-year; `D`, `RemainingYears`, `overlap`, `dt`, `G`, `B_eff` sim-years; `Severity`
  fraction of food output lost per active year; `s·D` production-years; `ρ`, `ρ_ship` dimensionless.
  Checks: `P = 1 − exp(−[1/yr]·[yr])` ✓; `Multiplier = 1 − [1]·[yr]/[yr]` ✓; `sD* = ([yr]·[1] +
  [yr])/[1] = [yr]` ✓; `min(remaining, dt)` [yr] ✓.
- **Corridor independence:** no new corridor; `a`, λ, D, `s_max` external; `s_min` derived against
  a DECLARED buffer (`ρ_ship = 1.3`), a magnitude condition on the event, never a target on any
  corridor. Corridors moved with pre-committed readings: `dev.starvationRatePer1000` RISES from 0
  iff a disaster strikes a `ρ ≤ 1.4` dev settlement (re-pinned under CR-015 N7, never fitted).
- **Coupling map:** CR-015 §4 rows 1–2.

---

## §8 TESTS THAT PIN EACH PROPERTY (spec §6.1–§6.2)

`F_Normal_ZeroDeficit_IsNormal` · `F_Stress_OrdinaryWeather_NeverFamine` (kills **M-FS-WEATHER**:
FAMINE iff `d ≥ 0.15` or `weather < 0.7`) · `F_Severe_WhereAdaptationEnds` (threshold == `a`, moves
with `a`; the birth full-stop asserted as a kernel fact inside SEVERE) · `F_Famine_DisasterApplied`
(kills **M-FS-DISASTER**; an absorbed disaster with `d = 0` is NORMAL) · `F_NotAbandoned_HerdingAlive`
/ `F_NotAbandoned_FarmingAlive` · `F_Famine_Abandonment` (all-zero row and legacy pct-0 row
abandoned; absent row not). **M-FS-ABANDON, corrected by the measured kill-record** (the earlier
text of this line claimed "predicate reads `Share` or `Farming` only" is killed by
`F_Famine_Abandonment`; that was an assertion, and it was wrong on both counts): the
`abandoned := false` variant is killed by `F_Famine_Abandonment`, `F_Famine_Both` and
`F_AbandonmentTiming_TurnExact`; the **Farming-only** variant is killed by
`F_NotAbandoned_HerdingAlive` alone (the director's single-sector rule is the property); the
**Share** variant is an EQUIVALENT mutant — `Sectors.Share` (`WorldState.cs:350-354`) guards the zero
row sum and returns 0.0 for the all-zero row, so `Share == 0 ⇔ Raw == 0` for every reachable row and
no test can kill it. `IsAbandoned` reads Raw because it is the field `ProductionSystem` reads, not
because Share is undefined ·
`F_Famine_Both` · `F_Stockpile_DecidesFamine` (full pipeline, forced strike: 0.1-y store ⇒ FAMINE;
1.5-y store at `ρ = 2` ⇒ NORMAL) · `F_AbandonmentTiming_TurnExact` · `F_DisasterTiming_TurnExact` ·
`D_HazardZero_StripControl` · `D_HazardInfinite_EveryoneStruck` ·
`D_AppliedOnce_FarmingAndHerding_NeverExtraction` (kills **M-PROD-EXTRACTION**) ·
`D_DtExact_FiveYearFailure` · `D_Classification_DtDifference_Pinned` ·
`D_OnsetProcess_BiasesWithinStatement` · `D_PersistsAcrossSaveLoad` · `D_Replay_Deterministic` ·
`D_ConfigRefusals` · `SchemaV25_PopulatedDisasterTable` · `S_WeatherOnly_NeverFamine` (λ = 0, 20
seeds × 300 turns: zero FAMINE settlement-turns; STRESS/SEVERE occur — non-vacuity) ·
`S_Disaster_TriggersFamine` · `S_Seed42_NoFamineWithoutCause`. Every mutant run is bounded at 3× the
clean-suite baseline; a hang is recorded as non-termination (ADR-015 §7.1); each kill names the
property the system ought to have (§7.2). **The measured kill-record is `docs/t4.21-1-mutants.md`**
(13 mutants, each with the diff applied, the failing tests, elapsed time against the bound, and the
property each failing test asserts), written by the agent who ran it on the merged tree.

**Suite status on the packet tree (recorded, not hidden).** The full Sim.Tests suite on `05b23e6` is
2-red: `CalibrationBatteryTests.Dev_MalthusCorridors_AllInBand(seed: 42)` and `(seed: 7)` fail with
the message "3 starvation deaths — the dev world is no longer pre-Malthusian…". Both fail with the
byte-identical message on the pre-packet tree `45046eb` (the M4 playtest build on the integration
branch) — re-measured by the fix lane in its own worktree at `45046eb` and on `05b23e6`: 2/2 fail on
each, messages byte-identical after stripping the timing suffix (sha256 `c943a28e4b4c4025…`) — so
the red is INHERITED, not caused by T4.21-0/1; CI on this branch cannot be green until it
is addressed, which is T4.21-4's work (the `Cr003Quarantine` guard restoration and the
`Dev_MalthusCorridors` message re-read, spec §4 T4.21-4).


---

## §10. THE ARMED MEASUREMENT (T4.21-4, 2026-09-18) — appended, nothing above is edited

`sim.json` `disaster.hazardPerYear` **0.0 → 0.01**, the value this ADR's §3.3 derivation and the
`disaster._doc` both name. Measured on branch `t4.21-4-arm` (cut from `claude/civdemo-work-b1z2y4`
@ `8f7f9da`) by the agent writing this section; raw rows in `docs/t4.21-evidence/t4.21-4/`.

**The onset process, against its own derivation.** Canonical founded, 20 seeds × 300 turns:
6,211 onsets over 66,000 settlement-turns = **0.941061 per settlement-century**. The once-per-turn
booking truncation at dt 10 is exact — `(1 − e^{−λ·dt})/(λ·dt) = 0.9516258`, the ≈ 4.7 % this ADR
accepts — so the truncation-corrected expectation is 0.9516258 and the binomial 99 % band on 66,000
Bernoulli trials at p = 0.0951626 is **[0.922204, 0.981047]**. The measurement sits inside it at
**z = −0.925**. The accepted bias is therefore confirmed at the value it was accepted for.

**The ladder, on whole worlds.** 1,540 FAMINE settlement-turns across those seeds, **0** with
`FamineReason.None`; 263 STRESS settlement-turns with `d_eff` **exactly 0.0** on every one; 0
starvation deaths on all 4,745 turns whose PREV world held nobody above STRESS; the one SEVERE turn
killed 23. The λ = 0 control arm produces **ZERO** FAMINE settlement-turns while still producing 352
STRESS and 1 SEVERE — weather is never famine, and the claim is not vacuous.

**The attribution.** Every layout control in `IntegratedPinAttributionTests` now runs the λ = 0 twin
and returns its v22/v23/v24 constant BYTE FOR BYTE, which is exact because `DisasterSystem` draws
both uniforms unconditionally (§4's RNG contract). The tree minus the arming is bit-identical to
`8f7f9da`, so the arming is the ENTIRE cause of the behavioural goldens' move
(`FoundedGolden` `db7c7a09…` → `a1def4df…`, `DrivenGolden` `98ee3a7a…` → `0af545ae…`, `ci.yml`
`FOUNDED_GOLDEN` with them; the founded value derived twice, in-test and by the built CLI).

**THE FALLOUT IS ESCALATED, NOT ABSORBED.** At this λ against the shipped mortality kernel the
canonical world loses roughly three quarters of its turn-300 population (median ratio 0.26 over 20
seeds), `canonical.fedGrowthPerYear` falls below its band, the CR-001 permanent dt-continuity
detonator breaks at the era gate by 2.5259 per 1000 yr (+0.4352 before, −1.8220 after), and the dev
world is extinguished (93,910 → 38 at seed 42 over 1000 turns). The mechanism is the G8/F4 dt
artefact this ADR accepted while λ = 0 made it unobservable. `docs/adr/cr-016-armed-disaster-fallout.md`
is OPEN with three options and a recommendation; no band, no constant and no derivation was moved by
T4.21-4.

**Suite status on the packet tree, recorded.** The §9 note above ("which is T4.21-4's work") is
discharged in part: the `Cr003Quarantine` guards at `PopulationTests :483` and `ChronicleTests :331`
are RESTORED (the phenomena returned and were measured), `:282` stays quarantined with its
measurement recorded (1 down-crossing, 0 up-crossings), and `Dev_MalthusCorridors` is re-read but
stays RED — its `starvedTotal == 0` / `crashes == 0` / `peak == final` teeth all assert the CR-003
premise, and re-aiming them onto a cause-attributed form is a change to the CR-003 quarantine's
substance, which is the director's. Suite: 867 passed / 6 failed / 4 skipped, all six named in
CR-016 §5.
