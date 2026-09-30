# ADR-019 — ARCHITECTURE CONSTITUTION ADDENDUM

**Docs only. HOLD FOR MERGE.** Cut from `main` `6d47739`. Every citation below verified against the
tree at that commit; disagreements between the directing prompt and the tree are recorded as
findings in PART F rather than silently reconciled (§7.12).

---

## WHY THIS EXISTS

Fourteen director decisions on long-range architecture were ratified. Checked against the tree,
**four are already ratified and need no change** (PART 10), **one conflicts with a frozen Spine
rule and is corrected below** (PART 1.1), and the rest are new. This ADR carries only what is new,
and cross-references the rest so a future agent does not read absence as permission.

**It amends no frozen document.** The Spine, D-009, D-037, and the M0 kernel spec are untouched.
Everything here is additive, or — in the one conflict — a correction of the DIRECTING PROMPT's
wording against the ratified Spine, not a change to the Spine itself.

---

## PART 1 — TEMPORAL

### 1.1 THE SPINE'S ERA-SCALED dt STANDS UNAMENDED — THE DIRECTING PROMPT IS CORRECTED, NOT RATIFIED

`civ-sim-architecture-v3-outline.md:34` rules era-scaled dt plus crisis zoom: **"global dt is set by
the world's most advanced polity's era band; all systems integrate rate × dt regardless (Law 3)."**
This is a FROZEN Spine commitment (S3, frozen at M0 exit per `spine-s8-governance-freeze.md`). dt is
10 years in the Neolithic band and shortens in later bands per the era-pacing table — **it is not,
and has never been, fixed at 10 years for the whole campaign.**

An earlier director formulation stated "the global simulation timestamp is 10 years." Checked
against the tree, **that formulation conflicts with the frozen dt-authority rule and is corrected,
not ratified**, to:

> **Economic systems operate at the globally authoritative strategic dt (era-scaled, per the
> Spine's dt-authority rule) and do not introduce an independent intra-turn economic timestep.**

The INTENT behind the "10 years" formulation was always "no economic sub-simulation," not a
literal fixed dt — PART 1.2 below is that intent, stated correctly. This correction requires no
Contradiction Report: the directing prompt is not a frozen document, and nothing frozen changes.

### 1.2 SUB-CALCULATION IS PERMITTED. SUB-SIMULATION IS NOT.

An internal mathematical microstep — a loop that integrates a system's OWN dynamics more finely
than the strategic turn, then flushes ONE set of Ledger flows per turn, with no other system able to
observe the intermediate state — is an implementation technique. A sub-simulation is an OBSERVABLE,
CAUSALLY INTERACTING mini-world: one where another system, or the player, can see or react to a
mid-turn state.

**FORBIDDEN:** intra-turn harvest or consumption reporting, intra-turn price updates, intra-turn
migration driven by economic change, intra-turn class response to economic change, intra-turn
economic causal chains of any kind.

**Reference implementation, verified against the tree at this commit (Sim.Core/Systems):**
`ProductionSystem.Step` and `ConsumptionSystem.Step` (including `BoundStore`'s spoilage and granary
capacity) each compute ONE dt-scaled lump per strategic turn — no internal loop, one `Ledger.Flow`
call per good per settlement per turn. `DemographicsSystem.Step` carries an internal half-year
MICRO-STEP kernel (`MicroStepYears = 0.5`, `n = dt / 0.5` identical steps) that integrates births,
deaths, starvation and aging more finely than the turn, then floors ONE set of exact totals into the
Ledger once per turn ("integer reconciliation, once per turn") — **no other system can read its
intermediate state, so this is SUB-CALCULATION and it stays**, per §1.2's rule. A future agent
should verify this against the code directly rather than trusting this citation, as the code is the
authority and this description will drift as the systems evolve.

### 1.3 dt CHANGES TAKE EFFECT IMMEDIATELY — NOT SPECIFIED IN THE TREE, RECORDED AS A GAP

When the most advanced polity enters a new era band and global dt changes, the directing prompt
states the change applies at once with an explicit player notification, with no transition period
unless the Spine specifies one. **Checked: the Spine does not specify a transition period anywhere
in the tree** — `civ-sim-architecture-v3-outline.md`'s dt-authority rule (`:34`) states the mechanism
(dt follows the most-advanced polity's band) but says nothing about transition behavior at a band
boundary, and no other document searched (`m0-kernel-spec.md`, `m3-spec.md`, `m4-spec.md`) mentions
one either. **This is recorded as an open gap, not resolved here** — "immediate, with notification"
is the director's stated preference and is consistent with the existing rule's silence, but it is a
NEW commitment, not a restatement of one already ratified. A future spec that needs this should
close it explicitly rather than infer it from this ADR's silence.

### 1.4 WAR IS THE ONLY SANCTIONED INTRA-TURN LAYER, AND IT IS THE SPINE'S EXISTING CRISIS ZOOM

A war pulse may contain player decisions and multiple battles; it is not a second global clock.
`d011-battle-layer-addendum.md:10` already rules battles as sequences of **command pulses** (orders
→ simultaneous resolution → playback → next pulse), and `:75` records the pulse-BUDGET ruling
("D-013 pulse budget and pulse-duration fiction — rec: 6–12 pulses ≈ one day of battle") — **D-013
is a decision-log entry recorded inside this same file, not a separate document; cited here as
found.** `d039-command-fog-and-siege.md` Part E ("THE CAMPAIGN LAYER", `:129`) rules the layer above
individual battles. **Cross-referenced; nothing restated, nothing added.**

---

## PART 2 — IMMEDIATE VS EMERGENT

### 2.1 A player decision resolves in one of two ways, and the distinction is architectural.

**IMMEDIATE / TRANSACTIONAL** — committed when the turn is committed: trade agreements, diplomatic
agreements, policy activation, government decisions, allocation of existing resources, construction
orders, territory sales.

**EMERGENT / ACCUMULATED** — developing across the interval: company formation, industrial
expansion, population growth, literacy, religious conversion, infrastructure progress, city
expansion, technological diffusion, economic consequences.

### 2.2 A construction order commits immediately and establishes the project's state; progress then
accumulates over the interval. **The player never observes fictional annual construction turns** —
that would be the sub-simulation PART 1.2 forbids.

---

## PART 3 — POLICY IS CAUSAL INFLUENCE, NOT DETERMINISTIC COMMAND

### 3.1 THE MOST IMPORTANT RULE IN THIS DOCUMENT, AND THE ONE MOST LIKELY TO BE VIOLATED BY A FUTURE AGENT REACHING FOR A MODIFIER.

**FORBIDDEN**, in the director's own example:

> **"Steel Policy +20 → 20% more steel plants"**

Policy changes the CONDITIONS under which emergence occurs. The outcome comes from the interaction
of population, capital, resources, demand, infrastructure, institutions, technology, entrepreneurs,
government policy, geography, competing opportunities, foreign conditions, and chance.

### 3.2 An outcome may occur AGAINST the policy environment. A steel plant can emerge where policy
does not favour steel, if the other conditions carry it. **The player influences probability and
environment, never the exact outcome.**

### 3.3 This restates law 2 (mechanisms over modifiers, `civ-sim-architecture-v3-outline.md:20`) for
the systems M5–M9 will build, and is consistent with D-018's class emergence, where a class appears
on a COMPUTED PREDICATE rather than being unlocked by a modifier. Cross-reference both; design
nothing new — the existing M2/M3 class-emergence mechanism (`ClassMobilitySystem`'s emerge/recede
predicates over published variables) is the pattern this generalizes, not a new one.

---

## PART 4 — HISTORY AS POSSIBILITY SPACE

### 4.1 History constrains what is POSSIBLE. It does not dictate the DATE of first occurrence, and
it does not dictate the outcome.

### 4.2 If a civilization has the knowledge, technology, resources, institutions and pathways, a
thing may emerge EARLIER than it did historically. **"Not historically invented here" does not mean
"impossible here."**

### 4.3 DIFFUSION IS PERMITTED where contact, trade, exploration or diplomacy provide a plausible
pathway. A polity encountering silk or tea through contact may develop adoption pathways around
them. `d040-discovery-and-control.md` Part B ("THE MAP IS DISCOVERED, NOT REVEALED", `:35`) already
rules the discovery half — cross-reference it; design nothing new here.

### 4.4 THIS IS NOT A CALENDAR UNLOCK SYSTEM. Law 4 (`civ-sim-architecture-v3-outline.md:22`,
"no calendar gates... capability derives from computed state") already forbids era gates; this adds
the positive half explicitly: availability derives from computed prerequisites, in BOTH directions
(a thing can arrive early on computed readiness, and a thing already known elsewhere can diffuse in
on a computed contact pathway) — neither direction is a date check.

---

## PART 5 — RESOURCE TYPOLOGY

### 5.1 Not everything economic is a physical stock. Five kinds, named so a future agent does not
flatten them into one generic resource table:

- **PHYSICAL STOCKS** — grain, iron, timber, coal, oil, steel. Accumulate, are consumed,
  transported, exported. **This is what exists in the tree today** (`GoodStockRow`, the Ledger's
  conserved-quantity model).
- **CAPACITY** — electricity is generation capacity, grid connectivity and demand, not a stockpile.
  A blackout is a capacity shortfall, not an empty warehouse.
- **MONEY** — an ENDOGENOUS SYSTEM, not a stock: currencies, money supply, credit, debt, banking,
  government issuance, exchange rates, inflation, depreciation, monetary crises. **Cross-reference
  `m4-pre-spec-dependencies.md` §1a ("MONEY — RULED: M5 TAXES IN KIND", `:23`): money is DEFERRED,
  M5 taxes in kind, and no money milestone currently exists in the Spine's milestone ladder.** That
  scheduling gap is open and this ADR does not close it — it only records that when money DOES
  arrive, it must be built as the endogenous system described here, not as another `long` stock
  next to grain and iron.
- **INSTITUTIONS** — represented as entities with counts (seven universities, two banking
  corporations), not as a single abstract "education" value.
- **ABSTRACT VARIABLES** — inflation behaves as a real economic variable (computed from the money
  system's own dynamics), not as an inventory quantity.

### 5.2 No implementation detail is designed here. The typology exists so a future packet does not
implement electricity as a warehouse or money as a fifth grain.

---

## PART 6 — COMPANIES

### 6.1 Companies are emergent. The player influences conditions; the simulation generates firms.
PART 3 governs the mechanism — this is Part 3 applied to one domain, not a separate rule.

### 6.2 The player receives aggregate industry information sufficient for strategic decisions —
robustness, capacity, employment, firm count, growth. **Not per-company micromanagement.**

---

## PART 7 — GOVERNMENT AND REGIME

### 7.1 State structure: Government (Executive, Legislature, Bureaucracy), political factions,
interest groups.

### 7.2 Government type is a strategic OPERATING ENVIRONMENT, not a micromanagement layer. It moves
legitimacy, policy execution, corruption, protest, suppression capability, population response,
foreign perception, trade relationships. The player retains strategic control under any form.

### 7.3 Regime change occurs through coups, revolutions, political crises and civil conflict, and
can alter territorial control. **A player with zero territories loses.** `d021-stability-doctrine.md`
Part 2 ("THE RELEASE VALVES", `:17`) and `d037-emergent-polities.md`'s claim model (Part A, `:11`)
carry the machinery — cross-reference; design nothing.

---

## PART 8 — CITIES: THE ONE CONFLICT, RULED

### 8.1 D-009 IS THE RATIFIED POSITION AND IT STANDS.

`d009-d010-map-population-addendum.md:13` — the unit of "where" is a settlement and its hinterland.
`:15` — settlement footprints are organic blobs growing along the network and terrain suitability,
consuming real farmland, and **"districts inside remain abstracted (v3 position holds)."**

### 8.2 Most of the cities decision is already D-009: expansion, suburbs, zones of influence,
organic growth. Two clauses were not, and both are ruled here:

**ABSORPTION IS A MERGE, NOT A CONTAINER.** One settlement may absorb another: the absorbed
settlement CEASES TO EXIST and its population transfers through `Ledger.Transfer`, conservation
exact under law 1 (`civ-sim-architecture-v3-outline.md:19`). It does not become a district inside
the survivor. D-009's district abstraction is untouched.

**LOCAL POLICY IS A SETTLEMENT-SCOPED ATTRIBUTE, NOT AN INTERNAL ADMINISTRATION.** A settlement may
carry policies distinct from national ones — an agricultural subsidy, a local tax rate. That is a
property OF the settlement (a row/field the settlement carries), not a structure INSIDE it (no
internal district-level simulation).

### 8.3 Both rulings preserve D-009 deliberately. **The alternative — un-abstracting districts —
was considered and REJECTED** as a large change to a ratified position with no current need behind
it.

### 8.4 The player sets strategic priorities; administration executes. Consistent with PART 3's
delegation doctrine (policy as causal influence, not command) applied to city administration.

---

## PART 9 — FORECASTING AND INFORMATION

### 9.1 The player receives forecasts BEFORE committing, and decisions RECALCULATE them dynamically
— changing a policy before End Turn changes the expected consequences shown.

### 9.2 Forecasts carry uncertainty and are not guaranteed future states. Other polities, markets,
diplomacy, intelligence and emergent events affect the outcome.

### 9.3 The chain is: **SIMULATION REALITY → AVAILABLE INFORMATION → PLAYER PERCEPTION → FORECAST.**
A forecast is a model over reality, never a read of it.

### 9.4 No takebacks after End Turn.

### 9.5 Information availability is already ruled — `d039-command-fog-and-siege.md` Part B
("RECONNAISSANCE AS AN INVESTABLE CAPABILITY", `:42`) and `d040-discovery-and-control.md` Part B
("THE MAP IS DISCOVERED, NOT REVEALED", `:35`). Cross-reference; add nothing.

### 9.6 No UI is designed here.

---

## PART 10 — WHAT IS ALREADY RATIFIED AND NEEDS NO CHANGE

**Stated explicitly so absence is not read as permission.**

**AI SYMMETRY — Spine principle 7, `civ-sim-architecture-v3-outline.md:25`:** *"AI actors use
player-identical verbs and information class. Difficulty = information and friction, never hidden
resources."* Frozen at M0 exit (`spine-s8-governance-freeze.md` §1). Information asymmetry and
decision-quality asymmetry are permitted; simulation-rule asymmetry is not. **Nothing to add.**

The director's AI-fairness decision was checked against the tree and found ALREADY RATIFIED, exactly
as quoted above, at the cited line. An earlier assessment reportedly found this a gap — checked here
and that finding does not hold against the tree; the rule already exists and needs no new document.

**TERRITORY AS CLAIM — `d037-emergent-polities.md` Part A.** A2 (`:18`): a polity is a claim, not a
container. A3 (`:22`): claim, control and recognition are three separate quantities, load-bearing.
Multiple polities may claim one settlement; recognition is bilateral and asymmetric. Irregular
influence-based borders follow from this model. `d040-discovery-and-control.md` Part C
("CONTROL DECAYS WITH DISTANCE...", `:92`) adds distance-decayed control; `d041-attachment.md` adds
population-held attachment as a fourth, distinct quantity. **Nothing to add.**

**Territory sale for money** is expressible only once money exists as the endogenous system PART 5
describes (`m4-pre-spec-dependencies.md` §1a — money deferred, no money milestone currently
scheduled). This dependency is noted; it is not designed here.

**WAR STRUCTURE** — `d011-battle-layer-addendum.md`, `d039-command-fog-and-siege.md`. **NOTHING
SPAWNS** — `d037-emergent-polities.md` A1 (`:14`): every actor, settlement and hostile force must
originate from population. Applies to war exactly as to everything else; not restated as a new rule.

---

## FENCE

- One new file (this one) plus the cross-reference lines recorded above. Nothing else.
- Amends NO frozen document. The Spine, D-009, D-037, and the M0 kernel spec are untouched — PART
  1.1 corrects the DIRECTING PROMPT's wording against an already-frozen rule; it does not touch the
  rule itself.
- Designs no mechanism, no constant, no curve.
- Creates no implementation packet.
- **GOLDENS DO NOT MOVE.** This is a docs-only change; no production code, config, or test file is
  touched. No suite run is required to prove this — the diff is one new markdown file.

---

## PART F — CITATION FINDINGS (§7.12: THE TREE WINS)

**F1.** Every line citation above (`civ-sim-architecture-v3-outline.md:19,20,22,25,34`,
`d009-d010-map-population-addendum.md:13,15`, `d011-battle-layer-addendum.md:10,75`,
`d037-emergent-polities.md:14,18,22`, `d039-command-fog-and-siege.md:42,129`,
`d040-discovery-and-control.md:35,92`, `d021-stability-doctrine.md:17`,
`m4-pre-spec-dependencies.md:23`) was verified against the tree at `6d47739` before being written
into this document, not carried from the directing prompt.

**F2. THE ONE CORRECTION.** The directing prompt's "the global simulation timestamp is 10 years" was
NOT ratified as written — it conflicts with the frozen dt-authority rule
(`civ-sim-architecture-v3-outline.md:34`), which sets dt from the most-advanced polity's era band,
shortening as eras advance. PART 1.1 records the correction: the INTENDED rule (no economic
sub-simulation) is ratified; the LITERAL "fixed at 10 years" wording is not.

**F3.** PART 1.3 (immediate dt-change transition behavior) is NOT found anywhere in the existing
tree — recorded as an open gap rather than silently treated as already covered.

**F4.** PART 10's AI-symmetry citation was checked directly against the Spine text and confirmed
ratified; the directing prompt's implication that an earlier compiler assessment found this a gap is
noted but not itself verified here (no such assessment was located in the tree to check against) —
recorded as the prompt's claim, not independently confirmed.

**F5. D-013**, cited in PART 1.4, is a decision-log entry recorded INSIDE `d011-battle-layer-addendum.md`
(line 75), not a separate document — cited as found rather than assumed to be a standalone file.
