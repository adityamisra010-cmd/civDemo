# D-051 — M5 polish rulings (Director, 2026-10-06/07)

**Status: RATIFIED.** The Director answered the questions raised by the read-only audits of the final
M5 polish pass. The audits are in the session scratchpad; their findings are summarised below. The
build these rulings start from is `m5-hardening` @ `81ac697`. The rulings are about what to build.
Each one is implemented on the polish branches and recorded in the M5 polish report.

These rulings supersede the ratified items named under each one, for M5 and onward. The historical
text of those records is not edited; dated pointers are added to them.

## 1. Food → population response: smooth it now

Measured: a single harvest-weather draw is applied to a whole 10-year harvest (σ_log ≈ 0.29), and
flight reads the previous turn's **nominal** one-turn deficit (ADR-025 §2.1). A 4 % shortfall causes a
~6 % one-turn exodus, and then the settlement refills.

Ruling:
- **(a)** Flight reads a **smoothed, multi-turn deficit**, not one turn's raw deficit.
- **(b)** The decade harvest uses the **time-average of the yearly weather process**. The mean is
  unchanged; the per-decade spread falls from ~0.30 to ~0.19.
- **(c)** Severe sustained deficits still produce mortality and out-migration.
- **(d)** The fed-growth band is unchanged. No surplus → fertility response is added in M5.

This supersedes ADR-025 §2.1 (flight on the nominal deficit) and closes the open G1 item. A CR records
the conflict and this ruling.

> **2026-10-10 — 1(b) implemented** on `m5p-d051-fixes` (time-averaged decade weather; 1(a) not yet). It moves two
> frozen dev-corridor teeth; escalated in `docs/adr/cr-020-d051-tools-and-weather-vs-measured-behaviour.md` §B.

> **2026-10-12 — §1(a) DEFERRED until after the M5 playtest** (`docs/d052-m5-integration-audit-rulings.md` §10); the
> ruling stays ratified. §1(b) stays ON and its two red dev-corridor teeth are ruled in CR-020 §B (D-052 §9).

## 2. Resources: common floor plus regional richness, so that expansion pays

Measured: stone rides the raw elevation channel with no floor. At 38 % of founded settlements it is
effectively zero (under 1 unit per 10-year turn), and the first granary in the playtest waited until
turn 113.

Ruling:
- **(a)** Every deposit good gets an **availability class** in `goods.json`. Common materials (timber,
  stone, clay, fibre, hides) get an abundance **floor** so that every early settlement can work them.
  Local differences remain.
- **(b)** Regional richness exists for **fertile land and livestock, good stone (quarries), copper and
  tin ores, and clay and fibre**. These are rich in some regions and scarce or absent in others, and
  geographically correlated, so a whole region is rich, not a random site.
- **(c)** The goal is that, **after roughly the first 100–150 turns**, an empire needs more land and
  more cities to get the resources it lacks. Expansion must pay. Rare and strategic goods stay rare.

> **2026-10-12 — §2 DEFERRED until after the M5 playtest** (D-052 §10); the ruling stays ratified. Disclosed for the
> playtest: stone-poor settlements, and the "now usable" shortcut outlining known land rather than deposits (D-052 §8).

## 3. Forecast: expected-value, never a read of the actual draw

The player-facing "next turn" food and population forecast is computed by the authoritative
simulation, stepping a private copy of the world, with **expected weather** (and no new random shock
onset). It never reveals the actual next draw. ADR-019 §9.2/9.3 ("a forecast is a model over reality,
never a read of it") stands. Labour orders take effect one turn late, so the forecast horizon is
labelled honestly ("your changes act from turn N+2").

> **2026-10-12 — §3 DEFERRED until after the M5 playtest** (D-052 §10); the ruling stays ratified. No next-turn
> forecast ships in the playtest build.

## 4. Tools before bronze

The Workshop must not be useless in Age I. Add an **alternative, non-bronze toolmaking path** (stone
and wood tools) available from founding. It has **lower productivity** than bronze toolmaking.
Bronze toolmaking (Age III, via tin bronze) remains the full-productivity path. The construction
queue's head-of-queue semantics are unchanged.

> **2026-10-10 — not implemented as a recipe.** A plain stone-toolmaking recipe ate every unit of stone and blocked
> all granaries (measured); the Age I trap is closed instead by making a workshop whose tools cannot be made an
> illegal order. Options in `docs/adr/cr-020-d051-tools-and-weather-vs-measured-behaviour.md` §A.

> **2026-10-12 — Director (D-052 §11):** the guard stays. The approved direction is a primitive stone-and-wood recipe
> for the existing tools good, at lower productivity than bronze, with no stone exhaustion and no granary blockage;
> its mechanism and milestone are deferred until after the playtest. §4 is NOT implemented as a recipe — the
> 2026-10-10 note under §6 ("Implemented first instead: §4 …") means the guard, not a toolmaking path.

## 5. Settlers: a player "Found settlement" order in M5

Add a player order, in the style of Civilization VI settlers. The player picks one of their
settlements and an unclaimed, reachable site. A party of colonists leaves with provisions and founds a
city that the issuing civilization controls.
- People and goods move only through the Ledger; nothing is created.
- The order uses the existing colonization founding path.
- Emergent founding (D-037 B1) continues unchanged.
- The order is legal only under stated conditions: the source has enough people and food, and the site
  is unclaimed and reachable.
- The AI uses the same order and the same predicates.

## 6. One central stockpile per civilization

> "Lossless automatic transfer. If it exists in one city, it is magically also available to the
> entire empire. Everything produced or available in a city contributes to 'central resource
> stockpile' no delays, no losses. No wastage. No need to complicate things by making per city
> stockpiles. Everything is simply central, including food." — the Director, 2026-10-07

Ruling: every good — food included — that is produced or held anywhere in a civilization forms **one
central stockpile** for that civilization. It is available to every one of its settlements instantly,
with no transport delay and no loss. There are no per-city stockpiles for a civilization's
settlements. An uncontrolled (stateless) settlement is its own single-settlement stockpile. Trade
(Age III) remains the exchange **between** civilizations.

This supersedes, for controlled settlements:
- the per-settlement store semantics in CR-015, ADR-024/025/026 (food state and migration read the
  civilization's stockpile);
- D-034 transport bulk for movements inside one civilization;
- the directive's own §5 wording that physical stores are kept per settlement.

Physical storage itself (the granary bound and spoilage) is **not transfer wastage**. It remains a
physical process, applied to the central stockpile. This reading is recorded here; the Director may
revise it.

> **2026-10-10 — Director: §6 is DEFERRED from the first M5 playtest candidate.** The literal per-civilization
> stockpile touches ~37 Sim.Core and ~68 Sim.Tests files (every `GoodStocks` reader) and supersedes ADR-024/025/026
> semantics; it is too broad for the candidate. The schema-free "intra-polity relief pooling" substitute raised in
> the read-only audit is NOT authorised either — it needs its own design decision. §6 stays ratified and open.
> Implemented first instead: §4 (pre-bronze toolmaking) and §1(b) (time-averaged decade weather).

> **2026-10-12 — §6 stays DEFERRED until after the Director's M5 playtest** (D-052 §11); no partial pooling or
> relief-pooling substitute. In the note above, "Implemented first instead: §4 (pre-bronze toolmaking)" is read as
> the Workshop guard only: no pre-bronze toolmaking path exists (D-052 §11).

> **2026-10-14 — pointer (text above unchanged).** After the playtest, the Director's 2026-10-13 instruction asked for
> two parts of §6: empire-wide access for construction and roads, and food allocated by critical need in a shortage.
> Both are implemented, UNCOMMITTED, with "no partial pooling / no relief-pooling substitute" superseded for those two
> parts only. See `docs/d053-batch2-playtest-rulings.md` §1, §2 and §5. §6 stays RATIFIED and OPEN for everything else.

## Not changed by these rulings

Taxation (D-049/D-050), knowledge persistence (D-048), the research engine, the Age gates and the
Battle Layer deferral (M7).


> **2026-10-20 — Director (closure ruling R2): destinations for the deferred items.** Scheduling, not implementation or
> acceptance; each ruling above stays RATIFIED. Mapped by content: **§1(a)** (smoothed multi-turn flight deficit; a
> migration mechanic, calibration-sensitive) → **M9 Society**. **§2** (availability classes, floor, regional richness —
> "expansion must pay") → **M6**, coordinated with fog of war (known extent) and the P9/P10 overlays. **§3** → the stores
> half is discharged now by Decision C (the RESOURCES next-turn figure uses expected weather); the population forecast
> → **M6** UI lane. **§4** (primitive stone-and-wood toolmaking) → **M6**, M6.22 realization lane, subject to the CR-020 §A
> mechanism decision. **§6-remainder** (one central stockpile for everything beyond D-053's two parts) → **M10 Integrated
> Civilization Simulation**. Dependencies: §2 needs stored known extent (M6.13) and the overlay data gaps closed; §4 needs
> the CR-020 §A option chosen and the recipe realized by an owning system; §6-remainder touches housing, recipes, trade,
> prices, migration and provisions at once and is not a knowledge item. Record: `docs/m5-exit-inventory.md` §13.

## Dated note — 2026-10-22 (D-062; appended, the text above is unchanged)

§2 (availability and richness classes, `:132-142`): D-062 R6/R7 make concealment (≈25 % of eligible geological deposits, revealed by prospecting) the first availability class in M6 (ADR-044). Richness classes stay as scheduled. Record: `docs/d062-m6-director-final-rulings.md`.

## Dated note — 2026-10-28 (D-067 R19; appended, the text above is unchanged)

§2 (the remaining parts: the §2(a) common-material floor and the §2(b) regional richness classes) and §4 (primitive
stone-and-wood toolmaking and its unresolved CR-020 §A mechanism choice) are rescheduled: the 2026-10-20 R2 destination
"→ M6" is superseded, and both are **deferred until after the Director's M6 playtest, with no milestone named**
(`docs/d067-m6-exit-gate-director-rulings.md` R19). M10 is a content-mapped future possibility, not a commitment; the M7
economy track is a coupled alternative, not an assignment. This changes scheduling only: **both rulings stay
RATIFIED.** The Workshop guard (D-052 §11; `ConstructionQuery` → `InputUnobtainable`) and deposit concealment (§2's
first availability class; D-062 R6/R7, ADR-044) are unchanged. Carried open: D-062 R6 ("randomly distributed") versus
§2(b) ("geographically correlated … not a random site").
