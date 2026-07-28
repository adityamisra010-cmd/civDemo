# D-037 — EMERGENT POLITIES, CLAIMS AND TERRITORIAL DISPUTE

**Director design ruling, 2026-07-26.** Decision record — exempt document class under S8 §4.
Binds the **M4 spec** (Parts A–D) and, through Part E, M5 (loyalty levers), M7 (diplomacy),
M8 (unrest, secession) and M10+ (espionage). **No M3 work changes.** This refines and binds
material already present in the master vision (Part 16.3 frozen conflicts, Part 16.7 claims and
borders) rather than adding scope. Queue-linked from the colonization/land-clearance item
(origin: CR-003 §2(a)).

════════════════════════════════════════════════
## PART A — GOVERNING PRINCIPLES
════════════════════════════════════════════════

**A1. NOTHING SPAWNS.** Every actor, settlement and hostile force must originate from population
already simulated. Civ-style barbarian spawning is permanently anti-scoped. Our population model
is what makes the honest version affordable.

**A2. A POLITY IS A CLAIM, NOT A CONTAINER.** A polity does not "own" settlements as an axiom.
Ownership is a contingent, simulated quantity that can be won, lost and contested. M4 is therefore
not "add rival civilizations" — it is "make ownership itself simulated."

**A3. CLAIM, CONTROL AND RECOGNITION ARE THREE SEPARATE QUANTITIES.** This is LOAD-BEARING and
must be in the M4 data model from day one; retrofitting it later is prohibitively expensive.

- **CLAIM:** which polities assert a right to a settlement. MULTIPLE polities may hold a claim on
  the SAME settlement simultaneously. Claims have sources and strengths (see C1) and persist
  independently of who holds the ground.
- **CONTROL:** which polity's orders the settlement actually obeys. Exactly one, or none
  (stateless). Determined by distance, communication latency, state capacity, legitimacy,
  garrison and local grievance.
- **RECOGNITION:** which claims each OTHER polity acknowledges. Bilateral and asymmetric — X may
  recognise A's claim while Y recognises B's.

A contested territory is simply these three disagreeing. No special case, no "disputed" flag, no
bespoke system.

════════════════════════════════════════════════
## PART B — THREE ORIGIN MECHANISMS (M4/M5/M8)
════════════════════════════════════════════════

**B1. COLONIZATION FROM BELOW (M4; extends the queued land-clearance item).**
Migration currently runs settlement-to-settlement, and ADR-012 rules that with no viable
destination people die at home. Extend it: groups may depart into UNCLAIMED land and found new
settlements. A settlement founded by departing population NEED NOT belong to the polity they
left — refugee foundings may be stateless. This is how the frontier fills and how new peoples
begin.

**B2. SECESSION AS WITHDRAWAL OF CLAIM (M5 control → M8 unrest ladder).**
A settlement ceasing to obey is not an event, a spawn, or a rebel faction — it is CONTROL failing
while CLAIM persists. That gap IS the dispute. The D-010 government-paralysis mechanic is this
same failure seen from the capital; secession is the provincial view. This replaces Civ-style
city-state formation with the real mechanism.

**B3. NON-STATE PEOPLES (M4 worldgen, present from turn zero — NOT spawned).**
Pastoralists, hunter-gatherers and other stateless populations occupy marginal terrain that
farming settlements do not claim. They are real population with real subsistence. RAIDING EMERGES
FROM THEIR SUBSISTENCE FAILING — no raid timer, no aggression stat. Steppe raiding historically
correlates with drought, which is exactly the T3.4b harvest-variance driver: the same bad year
that starves villages sends herders after grain. Connect these two mechanisms explicitly in the
M4 spec.

════════════════════════════════════════════════
## PART C — TERRITORIAL DISPUTE, PARTITION AND DIVISION
════════════════════════════════════════════════

The director requires that real-world territorial situations be POSSIBLE outcomes — not scripted,
not special-cased. Named as mechanism classes with brief neutral referents; the game must never
hardcode any specific historical case.

**C1. CLAIM SOURCES (mechanisms, never assignments).** Every claim must trace to one:

- prior possession (a polity that formerly controlled the settlement)
- co-ethnic/co-cultural population (irredentism — requires the culture dimension already present
  in the bucket key; becomes live when cultural plurality does, M8/M9)
- dynastic or legal inheritance (M7 institutions)
- treaty cession (M7 diplomacy)
- conquest (control converting to claim over time)
- settlement founding (a polity's own colonists)

Claims decay with time and disuse, and strengthen with recognition, population of the claiming
culture on the ground, and continuous assertion. Decay rates TUNE.

**C2. CONTESTED TERRITORY — claim exceeding control** (referent class: long-running partition
disputes such as Kashmir). A holds a claim; B holds control; neither concedes; third parties
recognise differently. Requires nothing beyond A3 plus C1. The line between them is a LINE OF
CONTROL, not a border: it is where control ends, and it need not coincide with any recognised
claim boundary. The system must represent and render this distinction.

**C3. PARTITION — one claim becoming two** (referent class: Korea, Germany, and the
India–Pakistan partition). Three legitimate origins, all mechanism-driven:

- **IMPOSED:** an external power or peace settlement divides a polity at a settlement boundary
  (M7 diplomacy: partition as a treaty clause).
- **DE FACTO:** a civil war or secession stalemates, and the front freezes into a durable line of
  control (M8, arising from B2 + no resolution machinery).
- **NEGOTIATED DISSOLUTION:** elites agree to separate without violence (referent class:
  Czechoslovakia). Requires an elite-bargain path in M8's politics — secession must be reachable
  through negotiation, not only through revolt.

In ALL THREE cases, BOTH successor polities may inherit a claim to the WHOLE. Whether they assert
it is their politics; that they CAN is the mechanism.

**C4. DIVERGENCE IS FREE — DO NOT BUILD IT.** Once separated, two halves of one people will
diverge on their own, because institutions, prices, opinion, grievance and class composition are
all computed per settlement. Do not add a divergence mechanic. The only requirement is that the
system stop treating them as one thing. Over centuries this produces two genuinely different
societies from a common origin, unscripted.

**C5. FROZEN CONFLICTS ARE A VALID STABLE STATE** (already ratified, Part 16.3). The system must
NOT force resolution of disputes. Most real disputes never resolve. There is no win condition to
serve, so a dispute persists until a mechanism changes it. Explicitly forbid any timer, event or
arbitration that exists merely to clear a dispute from the board.

**C6. SUCCESSOR CLAIMS ON COLLAPSE.** When a polity collapses (already ratified: collapse is a
chapter, not a loss screen), its claims must be inherited, not deleted. Multiple successors each
inheriting a claim to the whole is historically the origin of a great many disputes, and must be
a reachable outcome.

**C7. RECOGNITION AS DIPLOMACY (M7).** Recognising or withholding recognition of a claim is a
first-class diplomatic act with real consequences — it shapes alliance logic, casus belli
legitimacy, trade access and the legitimacy cost of assertion. Non-recognition must be a durable,
playable position.

════════════════════════════════════════════════
## PART D — WHAT THE M4 SPEC MUST DO
════════════════════════════════════════════════

**D1.** Ship claim, control and recognition as three separate quantities (A3). Even if M4 uses
only a subset, the data model must support overlap, claim-without-control, and asymmetric
recognition. This is the single instruction whose omission would make Part C unbuildable later.

**D2.** Settle the player-scope question: the director currently rules all twelve settlements
because M1/M2 had no polity concept. Under D-037 the natural design is that he controls ONE polity
holding a few settlements, with others belonging to rivals, to stateless foundings, or to no one.
This is the M4 spec's central question and must be answered there, not assumed.

**D3.** Render the distinction between a line of control and a claimed boundary (C2). Flag for
the deferred map-symbology art packet — this is exactly the kind of content that packet was
deferred to wait for.

**D4.** Include the coupling map, dimensional declarations, foundations audit and
corridor-independence statements required by the sharpened spec format (S8 §4.1).

════════════════════════════════════════════════
## PART E — DISPUTE PATHWAYS
════════════════════════════════════════════════

**Director design ruling, appended same date.** Binds the M4 spec (claims/control), M5 (loyalty
levers), M7 (diplomacy), M8 (unrest, secession) and M10+ (espionage). No M3 work changes.

**E0. THERE IS NO RESOLUTION MECHANIC.** A dispute is not "resolved" by an action with an outcome.
It ends when the underlying quantities — claim strength, control, recognition, loyalty — move far
enough that the disagreement no longer exists. Every pathway below is a way of MOVING THOSE
QUANTITIES, never a bespoke resolution system. Forbid any "resolve dispute" action, roll, or
timer.

**E1. LOYALTY IS THE PRIMARY PATHWAY, AND IT IS ALREADY HALF-BUILT.**
Loyalty of a settlement to a claiming polity is COMPUTED, never assigned, from quantities that
already exist or are scheduled: cultural/religious distance from the ruling polity's core,
accumulated grievance (T2.6), travel distance and communication latency to the capital, state
capacity, garrison presence, relative prosperity versus the core, and legitimacy.
Referent class: a distinct, aggrieved, distant, poorly-integrated province.
Low loyalty degrades CONTROL first (orders obeyed slowly, badly, or not at all), and only then
produces secession per B2.
The levers on loyalty are ORDINARY GOVERNANCE read at provincial scale, not special separatism
tools: invest, integrate institutionally, tax less, extend the franchise, garrison, repress. Each
with its own costs and backfire risk — repression must be able to CREATE the grievance it
suppresses.

**E2. DIPLOMATIC PATHWAYS (M7).** Cession by treaty; purchase; exchange of claims; third-party
arbitration where an institution exists to arbitrate; plebiscite as a treaty clause (the outcome
computed from actual population identity and loyalty, never scripted); mutual recognition; and
formal renunciation of a claim — which must be a real, costly, legitimacy-affecting act, not a
free tidy-up.

**E3. MILITARY PATHWAYS (M4 conflict → M6 battle layer).** Conquest converts CONTROL immediately
but converts CLAIM only slowly (C1: conquest is a claim source that strengthens with time and
continuous possession). Occupation without legitimacy is expensive and generates grievance.
Insurgency and counterinsurgency operate on a population-support model. A stalemated front becomes
a durable line of control per C3 — which is how de facto partitions form.

**E4. ESPIONAGE ACCELERATES; IT DOES NOT CREATE (M10+, with hooks reserved earlier).**
Foreign intelligence may AMPLIFY existing grievance, FUND AND ARM an already-organized movement,
BUY elites, and DEGRADE the target's state capacity or information quality. It may NOT manufacture
disloyalty in a contented, well-integrated population — otherwise espionage becomes a wand that
destabilizes anyone, and the loyalty model stops mattering. Ratio-of-existing-grievance, never
additive-from-zero. Counterintelligence and domestic security reduce the amplification. All
operations carry exposure risk, diplomatic-incident potential, and blowback (Part 18.3).

**E5. DEMOGRAPHIC PATHWAYS — slow, effective, and morally ugly.** Settling co-ethnic population
into a contested province erodes the rival's co-ethnic claim (C1) over generations; forced removal
does so faster and at severe legitimacy and international cost. Our bucket model already supports
this exactly, since population carries culture. The system must permit it, model its costs
honestly, and never reward it mechanically beyond its real effects. Content-policy note: handled
at population-statistics level, never depicted.

**E6. INSTITUTIONAL PATHWAYS (M8).** Autonomy, federation, devolution and power-sharing as
institutional modules that RAISE LOYALTY WITHOUT CEDING CLAIM — the middle road between repression
and separation, and historically the most common outcome. Their absence would leave the design
with only "crush it" or "lose it," which is false to history and poor as gameplay.

**E7. PATHWAYS ARE COMBINABLE AND OFTEN FAIL.** Multiple pathways may run at once, may work
against each other (repression while negotiating), and may produce outcomes nobody chose. C5
stands: nothing forces a dispute to end.
