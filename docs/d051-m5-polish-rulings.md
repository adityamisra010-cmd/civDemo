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

## 3. Forecast: expected-value, never a read of the actual draw

The player-facing "next turn" food and population forecast is computed by the authoritative
simulation, stepping a private copy of the world, with **expected weather** (and no new random shock
onset). It never reveals the actual next draw. ADR-019 §9.2/9.3 ("a forecast is a model over reality,
never a read of it") stands. Labour orders take effect one turn late, so the forecast horizon is
labelled honestly ("your changes act from turn N+2").

## 4. Tools before bronze

The Workshop must not be useless in Age I. Add an **alternative, non-bronze toolmaking path** (stone
and wood tools) available from founding. It has **lower productivity** than bronze toolmaking.
Bronze toolmaking (Age III, via tin bronze) remains the full-productivity path. The construction
queue's head-of-queue semantics are unchanged.

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

## Not changed by these rulings

Taxation (D-049/D-050), knowledge persistence (D-048), the research engine, the Age gates and the
Battle Layer deferral (M7).
