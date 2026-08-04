# D-039 — COMMAND FRICTION, RECONNAISSANCE, AND SIEGE

Director design ruling. Decision record — exempt document class under S8 §4.
Closes D-014. Extends D-011/D-012/D-013.

## PART A — WHAT VARIES ACROSS ERAS IS KNOWLEDGE AND LATENCY, NOT VERBS

A1. D-011 §1 fixes the order verbs as constant across all eras — "units change,
    verbs don't." That stands. What changes is WHAT THE COMMANDER KNOWS and HOW FAST
    ORDERS ARRIVE. A neolithic chief and a modern general issue the same verbs
    against very different information.

A2. THE THREE INFORMATION CHANNELS ARE SEPARABLE, and only two improve much:
      POSITION  — where enemy formations are. Minutely lagged. Improves with
                  reconnaissance investment and era.
      STRENGTH  — how many they are. Minutely lagged. Same drivers.
      OUTCOME   — what happened after an order executed. IMPROVES LITTLE ACROSS
                  ERAS. Assessment lag is stubborn; modern commanders still wait to
                  learn whether an assault succeeded.
    Model them as three quantities, not one information score.

A3. ORDER LATENCY IS DELAY, NOT DEGRADATION. An order issued at pulse t executes at
    pulse t+d. Orders are never garbled, partially understood, or misexecuted.
    RATIONALE, RECORDED: degradation multiplies failure modes the player cannot
    diagnose and cannot plan against; delay is legible, plannable, and historically
    sufficient. A formation with no current order continues its last order.

A4. COMMAND CAPABILITY GROWS FASTER THAN ARMY SIZE. Force sizes scale by roughly an
    order of magnitude across the campaign — clan skirmishes in the hundreds, early
    medieval field armies in the low thousands, modern armies far larger. Command
    capability grows FASTER: staff systems, signals and doctrine outpace the growth
    in numbers. A modern army is better commanded despite being larger.
    THE MODEL MUST STATE THIS RATIO EXPLICITLY rather than leaving it implicit in
    two independently-tuned curves. Size raises command difficulty; capability
    raises the ceiling faster.

A5. LAW 4 BINDS ALL OF IT. Nothing above may gate on era, date or turn number.
    Command capability derives from computed state — literacy, road and signal
    infrastructure, institutions, general competence, force size. Era is a
    consequence, never an input.

## PART B — RECONNAISSANCE AS AN INVESTABLE CAPABILITY

B1. INFORMATION QUALITY IS BOUGHT WITH WHATEVER IS SCARCE IN THAT ERA, NEVER WITH
    MONEY. Money does not exist before its milestone (D-030; GOV-2 §1a rules M5
    taxes in kind), so the ancient cost is FOOD AND PEOPLE. A scout is a person
    drawn from a bucket, eating rations, not farming. That is a real opportunity
    cost inside the population system that already exists, and it generalises to
    every era without requiring money.

B2. DIMINISHING RETURNS. Each additional scout adds less than the last. Ten scouts
    plus one is a material improvement; a hundred plus two is not. Derive the curve
    from a reference class at M6 spec; do not pick a shape here.

B3. RECONNAISSANCE IS POINTED, NOT GLOBAL. Scouts see WHERE THEY LOOK. Investment
    raises capability; deployment decides where that capability is spent. This is
    what makes surprise attack, terrain and screening real strategy rather than
    decoration.

B4. DISTANCE DEGRADES INFORMATION INDEPENDENTLY OF SCOUT COUNT. A large network
    still knows little about the far side of a continent. BUT THE DIRECTOR HAS RULED
    PLAYABILITY OVER FIDELITY HERE: the degradation is present and mild, not
    punishing. Terrain provides the counterweight — a vantage point extends
    effective reconnaissance range, so high ground is worth taking.

B5. SCOUTS ARE RELIABLE. Report quality sits high — roughly 90-95% — at all eras.
    Reports are STALE, not WRONG. Deliberately false intelligence is not modelled
    here; if it arrives it belongs to espionage (Spine: Espionage/intel uncertainty,
    late milestone), not to reconnaissance.

B6. OPEN QUESTION, NOT RULED: whether scouts are a CLASS (D-018 reserves one of
    twelve slots), a state flag on existing military buckets, or an abstract
    per-polity capability score with a population cost and no bucket of its own.
    Each has different conservation consequences under law 1. M6's spec owns it, and
    it should be answered alongside GOV-2's open "is a notable a person?" question,
    which has the same shape.

## PART C — DISPLAY: THE STALE GHOST

C1. An enemy formation renders in one of three states:
      LIVE      — currently observed. Full colour.
      GHOST     — last known position and strength, from a report now stale.
                  Rendered darker/desaturated, visibly not current.
      UNKNOWN   — never observed, or too long since. Nothing rendered.

C2. THE GHOST IS THE PEG ON THE TABLE. It is where the commander last placed the
    marker. It may be wrong because the enemy moved, never because the report lied.

C3. A ghost that comes back into observation reverts to LIVE immediately —
    proximity restores certainty. State this so the transition is not treated as a
    timer.

C4. The visual treatment falls under D-038's object layer and the M6 symbology
    inheritance (D-038 E4). It is not designed here.

## PART D — SIEGE

D1. FORTIFICATION IS COMPUTED FROM WHAT A SETTLEMENT HAS BUILT. Not a unit type, not
    a flag. It derives from constructed works, size tier (T3.8), and terrain.
    A capital is hard to take because it is large, well built, deeply connected and
    far inside friendly territory — not because it is labelled a capital.

D2. THE DIRECTOR'S EXPECTATION, RECORDED AND CORRECTED: the picture wanted is a
    capital ringed by smaller towns and villages, hard to reach and harder to storm.
    NOTE THAT THIS PROJECT HAS ONE SETTLEMENT PER PLACE (D-009: "the unit of 'where'
    is a settlement and its hinterland"; districts remain abstracted) — it does NOT
    follow Civilization VI or Humankind in giving a settlement multiple cities. The
    wanted picture arrives instead from D-009's organic sprawl plus T3.8's size
    tiers plus the settlement network: many settlements, unequal, with the largest
    deepest inside the polity.

D3. SIEGE DIFFICULTY IS COMPUTED FROM AT LEAST: built fortification; size and
    population; depth in defender territory and what an attacker had to pass;
    besieger supply at its standing position; garrison strength; and DEFENDER WILL
    (grievance, loyalty — a city may open its gates).

D4. DIFFICULTY IS SHOWN TO THE PLAYER BEFORE THE DECISION, and it is the same
    quantity that parameterizes the quick-resolve success estimate. One number, two
    uses — a shown difficulty that does not drive the resolver is a lie.

D5. STARVATION IS A FIRST-CLASS SIEGE OUTCOME. A settlement can be starved into
    surrender with no assault. This runs on the FOOD system, not the battle system,
    and it is the historically dominant siege outcome.
    DEPENDENCY, STATED: this requires B-2 store bounding. Today reserves stand near
    twelve centuries and no settlement can starve (B-2, three costumes: grain,
    prices, timber). A siege that cannot starve a city is not a siege. M4's B-2
    packet is a hard prerequisite for D5.

## PART E — THE CAMPAIGN LAYER

E1. D-011 already sub-steps BATTLES: 6-12 command pulses, roughly one day (D-013).
    That stands unchanged.

E2. A WAR IS NOT A BATTLE, AND NEEDS ITS OWN LAYER. Wars run months to years; a
    10-year turn cannot contain one as a single resolution. This layer DOES NOT
    EXIST in the current design and is new work.

E3. THE CAMPAIGN LAYER IS STRATEGIC DECISIONS BETWEEN BATTLES, NOT A FINER-GRAINED
    TURN. Ruled explicitly: the player does not play out months of movement. The
    campaign presents decision points — march, besiege, give battle, withdraw,
    negotiate, hold — and resolves the intervals between them. Battles that arise
    drop into D-011's existing pulse layer.
    RATIONALE: playing months of movement is a second full game inside the first.

E4. THE DELEGATION DOCTRINE APPLIES AT BOTH LAYERS. D-011 §2's delegate-with-agency
    already covers battles: skipping hands it to the assigned general, whose
    competence and traits parameterize the AutoResolver. THE SAME APPLIES TO A
    CAMPAIGN — a war can be handed to a general and reported on.

E5. D-011's PARITY RULE EXTENDS TO THE CAMPAIGN LAYER UNCHANGED: auto-resolved
    outcomes calibrate to the median of played outcomes for the same setup, so the
    world's history does not depend on which mode was used. Without this, delegating
    a war is a strategy rather than a convenience.

E6. THE dt QUESTION IS ALREADY ON FILE. GOV-2 §3 records that dt falls to 0.5 by the
    Modern band, where a cross-continent march is roughly one full turn. The campaign
    layer must work at both dt = 10 and dt = 0.5. State the constraint; the M6 spec
    solves it.

## PART F — WHAT THIS DOES NOT DO

F1. Does not amend D-011's verbs, formations-as-tokens, or WEGO structure.
F2. Does not amend D-009's one-settlement-per-place ruling (see D2).
F3. Does not schedule any work. M6 owns Parts A, C, D, E; Part B's investment
    mechanism touches M4, which owns armies.
F4. Does not design espionage. B5 draws the line: reconnaissance is stale, espionage
    is uncertain, and they are different systems in different milestones.
