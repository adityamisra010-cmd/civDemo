# M5 — TEMPORAL CONTROL & PLAYER AGENCY (DESIGN PLACEHOLDER)

**NOT A SPEC. NOTHING HERE IS RATIFIED. NO MECHANIC IS IMPLEMENTED.**
No constant, threshold, timer, cooldown, RNG stream, schema change or gameplay
equation is introduced by this document, and none may be inferred from it.

**Two conflicts with frozen material are raised by this design and are NOT
resolved here** — see `docs/adr/cr-006-continuous-time-and-campaign-epoch.md`.
Milestone ownership itself is still open under
`docs/adr/cr-005-m5-research-technology-institutions-placement.md`. Companion
scope record: `docs/m5-research-technology-institutions-placeholder.md`.

---

## §0 THE GOVERNING PRINCIPLE

> **THE GAME SHOULD INTERRUPT THE PLAYER BECAUSE THEIR DECISION MATTERS, NOT
> BECAUSE THE SIMULATION WANTS THEIR ATTENTION.**

This is not a warning system and must not become one. Every rule below exists to
serve that sentence.

## §1 DEFINITIONS

**CONTINUOUS SIMULATION.** The underlying world evolves according to the
simulation's time model; population, production, consumption, stocks, prices,
research, construction, trade and diplomacy all advance. A turn is **not** a
freeze-frame in which nothing happens between decisions. *(This is the statement
that collides with the frozen kernel contract — CR-006 §1.)*

**TURN.** The normal strategic control boundary, and **retained unchanged**. The
player receives control at turn boundaries. Turns are **player-facing strategic
checkpoints over a continuously evolving state**, not the fundamental unit of
physical simulation. The existing turn architecture is not replaced and the sim
does not become real-time.

**PROJECTION.** A forward estimate of a trajectory — food reserves, depletion,
production, consumption, construction and research completion, population,
economy, military readiness. **Projections are INFORMATION, never a command.**
They must express uncertainty where the underlying process is stochastic, and
must not present a deterministic promise. The illustrative shape (values are
illustration, not specification):

```
Food reserves      2,937 units
Projected depletion  ~7.2 years
Estimated range      5.8 – 9.1 years
```

Harvest variance is already an AR(1) stochastic process, so a food projection
that quoted a single number would be false precision by construction.

**POLICY.** A standing instruction from the player telling the civilization how
to behave automatically, so the player need not re-decide each turn. **A policy
is how the player DELIBERATELY CHOOSES RISK.** The taxonomy is deliberately not
invented here; the concept and its ownership are what this document fixes.

**DECISION EVENT.** A point at which the player's agency can *materially change
the outcome*. Existence is the test — not severity, not novelty, not urgency.

**CALCULATED HAND-BACK.** The act of returning control mid-flow because a
decision event exists. "Calculated" is load-bearing: the system must have
determined that a genuine strategic option opened, not merely that a number
moved.

**PLAYER TRIGGER.** A player-authored condition that *defines* a hand-back
("wake me when nuclear research reaches 80%", "pause when this construction
completes", "do not interrupt me for minor economic developments"). A trigger is
the player delegating their own attention policy.

## §2 NON-GOALS — stated as prohibitions

The system must **NOT**:

1. nag the player;
2. require confirmation of an obvious risk — **no "are you sure you want to risk
   famine?" dialog, ever**;
3. assume the player's objective is safety;
4. automatically "correct" what it judges to be bad strategy;
5. turn a warning or a worsening forecast into a popup;
6. force every technology completion into a player decision;
7. make turns the fundamental unit of *physical* simulation;
8. punish the player for having intentionally accepted a calculated risk.

**A risky strategy is valid gameplay.** Running reserves deliberately low to
accelerate construction is a legitimate move, and the simulation's job is to
model its consequences, not to prevent it.

## §3 WHEN AN INTERRUPTION IS WARRANTED

Only when a **new strategic option exists that did not exist before**, and the
player's choice between options changes the outcome. Illustrative classes:

- a major technology becomes available;
- a new government form becomes available;
- a major policy becomes available;
- an important military capability becomes available;
- a major diplomatic event requires player agency;
- a construction or research milestone creates a materially different option;
- **a player-defined trigger fires** — which is warranted *by definition*,
  because the player already declared it worth their attention.

## §4 WHEN AN INTERRUPTION IS NOT WARRANTED

Never merely because: food declined · prices changed · a minor technology
improved an efficiency · a forecast worsened · the simulation believes the player
is making a mistake · a resource crossed a threshold.

**Warnings and projections are not decision events.** The discriminator is
agency, not magnitude: if the player learning this fact now would not change what
they can *do*, it is information and belongs in a projection, not an interrupt.

## §5 CRITERION — "MATERIALLY CHANGES AVAILABLE STRATEGIC CHOICES"

Stated qualitatively; **no numeric threshold is fixed here, by instruction.** A
development qualifies when at least one holds:

1. **New option** — an action becomes possible that was previously impossible
   (not merely cheaper or faster).
2. **Branch point** — the development opens mutually exclusive or competing
   pathways, so *not* choosing is itself a choice with consequences.
3. **Irreversibility** — the option will lapse, or committing forecloses others.
4. **Cross-domain reach** — the capability changes what is available in a
   *different* system (military, economic, diplomatic, institutional).

A development that only shifts a rate, cost or efficiency **does not qualify**
and is incorporated automatically. Nuclear Fission completing qualifies under 1,
2 and 4 (weapons vs power vs further research are competing pathways in different
domains). A minor agricultural yield improvement qualifies under none.

## §6 ARCHITECTURAL OWNERSHIP

| concern | owner | exists today? |
|---|---|---|
| turn execution, dt, clock advance | kernel `TurnExecutor` / `SimClock` / `EraTable` | **yes, frozen** |
| player input | `OrderLog` / `OrderKind`, turn-stamped | **yes** |
| narrative emission | `Chronicle` | **yes — narrative only, NOT decisions** |
| projection | **new M5 component, read-only** | no |
| policy | **new M5 component** | no |
| decision-event detection | **new M5 component** | no |
| player triggers | **new M5 component** | no |
| calendar / BCE-CE presentation | **new, presentation layer** | **no converter exists** |

**A projection component must be a pure observer** — it reads state and computes
estimates, and no system may consult it. The existing `ReplayReport` is the
precedent: it reimplements no formula and is read by nobody in the sim. A
projection that fed back into simulation would be a second implementation of
every equation it forecasts, and would drift.

**The Chronicle is not the decision-event system.** It records what happened;
decision events are about what the player can now *do*. Conflating them would
make every narrative beat an interrupt — precisely §2's prohibition.

## §7 INTERACTION WITH RESEARCH / TECHNOLOGY

Research progresses continuously; technology **does not unlock at turn boundaries
merely because turns exist**. When research crosses completion:

1. the technology becomes available **at that point in simulation time**;
2. a calculated hand-back occurs **only if** §5 is satisfied;
3. if it is, the player may exploit the capability immediately rather than
   waiting for the next turn;
4. the simulation then resumes.

Minor discoveries become available and are incorporated **with no interruption**.

*(This is the sharpest instance of the CR-006 §1 conflict: acting at 1946.3
requires the executor to yield mid-turn, which the frozen kernel contract does
not provide.)*

## §8 INTERACTION WITH CIVICS / GOVERNMENT

Government determines which institutional pathways are **available or
efficient**; research determines which capabilities are **possible**. A
government change therefore *transforms* pathways rather than deleting acquired
technologies. A new government form becoming available is a §3 decision event
(new option + branch point). Policies are player-authored standing instructions;
institutions are the civilization's own machinery — **the two must not be
conflated**, because a policy the player never set must still leave the
civilization behaving normally through its institutions.

## §9 INTERACTION WITH ECONOMIC AND MILITARY SYSTEMS

Technology changes **which levers exist**, not the magnitude of a permanent buff
— law 2 (mechanisms over modifiers) governs, and "unlock a bonus" is the shape it
bans. A capability that opens a genuinely new economic lever or military option
qualifies under §5.1/§5.4; one that improves throughput does not. **Later
military and economic packets must not invent their own technology
prerequisites** — that ownership sits with M5, which is the entire reason for the
sequencing rule.

## §10 THE WORKED SCENARIO, AS AN ACCEPTANCE NARRATIVE

Player ends a turn with +2,937 food. Three years into the next turn granaries are
invented. The player reduces food production to accelerate construction,
deliberately accepting a thinner buffer. Required behaviour:

- the technology is incorporated at the moment it completes;
- projections update, including the shortened depletion estimate **with its
  range**;
- the player may adjust allocation immediately;
- simulation continues;
- **no confirmation is demanded**;
- **no repeated warning is issued**;
- **the player's strategy is not overridden**;
- a configured food-reserve policy governs automatic institutional behaviour; if
  none is configured, the civilization still behaves through its normal emergent
  mechanisms;
- if food later approaches a genuine civilization-level crisis, a decision event
  may be generated **only if a new decision actually exists** — otherwise it
  remains a projection.

## §11 CALENDAR REQUIREMENT

1. **Turn duration varies by historical period.** **ALREADY SATISFIED** — the era
   table steps dt 10 → 5 → 3 → 2 → 1 → 0.5 sim-years and `SimClock.DtYears` is
   the universal rate basis. **No change is required or requested here.**
2. **Proposed campaign start: 10,000 BCE.** **CONFLICTS** with ADR-002 (epoch
   4000 BCE = day 0) and with CLAUDE.md's "spanning 6,000 years". Raised in
   CR-006 §2; **not applied.**
3. **No year zero:** 2 BCE → 1 BCE → 1 CE → 2 CE. **No BCE/CE converter exists in
   the tree today** — `SimClock.WorldDateYears` is years since epoch and the
   mapping is unowned. This is therefore a clean requirement to specify, with no
   existing rule to contradict, and it belongs to the presentation layer.
4. **WORLD TIME ≠ PLAYER DECISION FREQUENCY.** The world may advance through time
   continuously while the player receives control at turns and calculated
   hand-backs.

## §12 OPEN QUESTIONS — must be answered before implementation

1. **How does a mid-turn hand-back work against the frozen kernel contract?**
   The blocking question. Sub-stepping, a re-entrant executor, and
   "decide-at-next-boundary-but-date-stamp-earlier" are three different
   architectures with different determinism and replay consequences. **CR-006.**
2. Does a mid-turn hand-back create a **new order-delivery semantic**? T1.9's
   precedent is explicit that order-delivery timing needs its own turn-exact pin;
   an order issued at 1946.3 has no such semantic today.
3. Are projections **recomputed on demand** (pure, no state) or **stored**? Stored
   projections become serialized state and fall under the schema and hash rules.
4. Do projections consume RNG? If a range is derived by sampling, that is a new
   stream — and law 5 requires it be registered and deterministic. A closed-form
   interval avoids the question entirely.
5. Are policies **orders** (existing `OrderLog`) or a **new standing-state
   table**? Orders are turn-stamped events; a policy persists until changed.
6. Is a decision event **replayable**? A replay must reproduce hand-backs exactly
   or determinism breaks — this interacts with question 2.
7. Does a player trigger evaluate **continuously or at boundaries**? Continuous
   evaluation is the point of the feature and the hardest part against the kernel.
8. What happens when **several decision events coincide** — queue, merge, or
   priority? Any ordering over them must have a stable integer tie-break.
9. Multiplayer/AI civilizations do not stop for the player's hand-backs; what is
   the semantic for AI-controlled research strategy during a human's interrupt?

## §13 DEPENDENCY MAP — build order within M5

```
(0) CR-006 ruling on continuous time + epoch      ← BLOCKS EVERYTHING BELOW
        │
        ├─► (1) calendar / BCE-CE presentation        (independent, presentation only)
        │
        └─► (2) research & knowledge state            (from the M5 scope record)
                    │
                    ├─► (3) PROJECTION (pure observer; needs state to project)
                    │
                    ├─► (4) POLICY (standing state; needs something to govern)
                    │
                    └─► (5) DECISION-EVENT DETECTION
                                 │   needs (2) to have capabilities to detect,
                                 │   and (4) so a policy-governed outcome is not
                                 │   mistaken for a decision
                                 │
                                 └─► (6) PLAYER TRIGGERS
                                          (authored against (3) and (5))
```

**Reading of the map.** (1) is independent and could ship first. (3) cannot
precede (2) — there is nothing to project. (5) must not precede (4), or every
policy-handled situation would surface as a decision event, which is §2's failure
mode exactly. (6) is last because a trigger is authored against projections and
event classes that must already exist. **Nothing below (0) can be specified until
the kernel question is ruled on.**
