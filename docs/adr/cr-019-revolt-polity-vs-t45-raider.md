# CR-019 — A revolt now founds a polity, so T4.5's raider has no source

**Status:** OPEN. The Director's ruling was implemented as written; this record describes what it costs an M4
mechanism. Written in R3 (`m5i-r3-final-reconcile`), 2026-10-03.

## 1. The frozen items in conflict

- **Director R2-final §2 (RATIFIED):** "When a settlement revolts … it becomes a NEW AI-CONTROLLED POLITY
  immediately."
- **M4 T4.5 appropriation (`AppropriationSystem`):** the raider must be **stateless**, meaning no `ControlRow` names
  it. **M4 revolt (`RevoltSystem`, RevoltTests):** revolt was the only path in a founded world that produced a
  stateless settlement. It was written for that purpose: "statelessness is now something a world can ARRIVE at".

## 2. Evidence (MEASURED)

- `RevoltTests.RevoltProducesEXACTLYTheStateT45sRaiderGateAsksFor` failed once the ruling was implemented. It has
  been replaced by `RevoltNoLongerProducesStatelessness_TheRaiderGateLosesItsOnlyProducer_CR019`, which pins the
  ruled state.
- Founded worlds control every settlement (`WorldFounding.FoundInitialEmpire`), and colonies inherit their
  parent's controller. With this change no path produces a stateless settlement in a founded world, so the raider
  gate cannot open there. Before this change the gate was already narrow (it also needs herding dominance,
  AppropriationSystem header), and R2c recorded raids as "rare".
- R2a city-states (local knowledge holders) still work in any world that contains an uncontrolled settlement
  (tests unchanged). A founded world, however, no longer creates any.

## 3. Minimal options

1. **Accept (current state).** Appropriation stays reachable only in hand-built or legacy worlds. Raids in founded
   worlds wait for the Battle Layer (M7 in the 2026-10-03 roadmap) or for politics.
2. **Let a one-settlement AI polity count as a raider:** change the T4.5 predicate from "no control row" to
   "controller holds only this place". This changes a frozen M4 rule and needs a ruling.
3. **Make only zero-happiness revolts stateless.** Uprisings would found polities and total-deprivation revolts
   would leave the place stateless. That splits §2's single rule, and needs a ruling.

## 4. Blast radius

RevoltSystem, AppropriationSystem's reachability, RevoltTests, and the driven/founded goldens wherever a revolt
fires. Option 1 changes no further code.

## 5. Recommendation

Option 1 now. The Director should rule on 2 or 3 when raids or politics are next in scope.
