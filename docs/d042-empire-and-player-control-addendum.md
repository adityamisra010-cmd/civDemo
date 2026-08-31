# D-042 — EMPIRE, OWNERSHIP, GOVERNANCE AND PLAYER CONTROL (Architecture Addendum)

**Director design ruling.** Decision record — exempt document class under S8 §4.
Follows the **D-011 addendum precedent**: it binds the Spine **without editing
it**. Extends and refines **D-037** (polities, claims, control), and constrains
future M5+ work on governance, knowledge, capability and economy.

**DESIGNS NO MECHANISM.** No storage, no constant, no equation, no schema. No
production code, test, golden or frozen document was changed by this record.

---

## §0 THE CORE PRINCIPLE

> **The player controls an Empire. The simulation models the world.**

The player operates at a grand-strategy abstraction comparable to Civilization VI
/ Total War: the player issues strategic and settlement decisions, the simulation
resolves the consequences, and **the player does not directly manage individual
citizens or economic agents.**

---

## §1 EMPIRE AS THE STRATEGIC ACTOR

1. One human player controls exactly **one** playable Empire; every other Empire
   is AI-controlled.
2. **There is no separate player-controlled "polity" competing with the Empire.**
   Governance is a *domain of* the Empire, not a rival actor.
3. **Do not introduce a separate Civilization abstraction** unless a future
   explicitly ratified mechanic requires persistent identity independent of an
   Empire.
4. **Do not create `PlayerEmpire` and `AIEmpire` as separate simulation
   entities.** Player and AI are different *command sources* acting on the same
   Empire/state model.

## §2 FOUNDING AND LIFECYCLE

1. An Empire begins when its settler establishes its first settlement; that
   settlement is the initial **capital**.
2. An Empire may acquire further settlements, cities, towns, villages and future
   settlement types.
3. **Losing the original settlement does not eliminate the Empire.** Losing the
   capital produces its own consequences, but the Empire survives while it
   controls at least one settlement; the player then designates a new capital
   from the survivors.
4. **An Empire ceases to exist when it controls no surviving settlement.**
5. Rebellion/secession may create a new faction/Empire from settlements taken by
   rebels. **If an Empire fragments, the player continues with the surviving
   portion** — losing settlements to rebellion does not by itself remove the
   player from the game.
6. Civilization VI is the baseline for empire/capital/lifecycle behaviour only —
   unrelated Civ VI mechanics are **not** imported.

## §3 SETTLEMENT / EMPIRE RELATIONSHIP

1. A **settlement** is a physical/local simulation entity; an **Empire** is the
   strategic control entity spanning one or more settlements. **They are not the
   same conceptual object.**
2. **The Empire must not become a God object** containing every domain's state.
   Domain state stays in its own tables/systems, sharing a stable Empire
   identity/key.
3. The conceptual shape — *Empire: settlements · government · economy ·
   knowledge · military · institutions · relations · capabilities* — is a
   **relationship, not an instruction to build one monolithic class.**

## §4 RESOURCE OWNERSHIP

1. Resources have **both physical location and economic ownership**.
2. Resources at a settlement are owned by the Empire **controlling** that
   settlement.
3. **Shared ownership does NOT create a single Empire-wide inventory.** Local
   stocks stay physically local; moving them between settlements requires the
   appropriate transport/trade/logistics mechanism when one exists.
4. **Population does not independently own economic resources.** There is **no
   household wallet model** and **no treasury-versus-population-money model.**
5. Money, when implemented, follows the same principle: an **Empire-controlled
   economic resource, locally situated where appropriate.**
6. **Do not introduce individual citizen wealth, wages, household bank accounts
   or personal money** unless a future explicitly ratified mechanic requires it.

## §5 GOVERNANCE

1. **Governance belongs to the Empire**; government is not a separate strategic
   actor, and **a government change does not destroy or replace Empire
   identity.**
2. Government may influence available decisions, policies, capabilities,
   economic behaviour, military organisation, knowledge production and
   institutions — **through state and computed conditions.**
3. **Avoid hard-coding governments as bundles of direct bonuses.** *(This aligns
   with Law 2: free-floating permanent buffs are banned.)*
4. A government transition **preserves unrelated accumulated Empire state**
   unless a specific mechanic explicitly changes it.

## §6 PLAYER CONTROL, ORDERS, AND PLAYER/AI SYMMETRY

1. The player acts at grand-strategy abstraction and **does not manipulate
   individual people, individual economic agents, or internal calculations.**
2. Player actions enter the simulation as **orders** or **persistent directives**
   through the existing order/control architecture.
   - **Immediate orders:** declare war, move an army, rename a settlement, begin
     an explicitly ordered activity.
   - **Persistent directives:** research allocation, policy allocation, storage
     investment, recurring trade routes. **A persistent directive is state
     describing a desired ongoing configuration — it is NOT re-issued every turn
     to keep working.**
3. **Orders express intent; simulation systems compute consequences.**
4. **A button represents player intent. UI code must never directly mutate
   simulation state, nor call domain systems to perform outcomes.**
5. **Player and AI use the same downstream pathway:**

```
Human Player                      AI
     │                             │
     ▼                             ▼
Player Control            AI Decision Engine
     │                             │
     └────────► Orders / Directives ◄────────┘
                        │
                        ▼
                   Validation
                        │
                        ▼
              Simulation Systems
                        │
                        ▼
                Next World State
```

6. **The simulation must not contain separate gameplay physics for human- and
   AI-controlled Empires.** Player control is an **authority distinction, not a
   different simulation model.**

## §7 STATE-MEDIATED DEPENDENCY (reaffirms Law 6)

1. **Gameplay interdependence is allowed; direct code coupling is not.** Systems
   communicate through World State and kernel contracts, never by calling sibling
   domain systems.
2. Economy may depend *conceptually* on knowledge, government, military,
   transport and institutions — **that never justifies a sibling call.**
3. **Do not create a universal God system such as a `CapabilitySystem`** that
   owns every capability or coordinates every domain.

## §8 CAPABILITY ARCHITECTURE

1. **The existing D-020 predicate machinery is the foundation** for future
   capability evaluation.
2. Capability is **distinct from** raw state, knowledge, research activity,
   technology, policy, institution and action:

```
State → Published Variables → Predicates → Capabilities → Available Actions
```

3. **Capability evaluation must be able to distinguish SCOPE — Empire-level and
   Settlement-level, with unit/action scope where required.**
4. Capability definitions **consume computed state, never hard-coded calendar
   unlocks.**
5. **Do not implement the knowledge/technology/capability system as part of this
   record.**

## §9 KNOWLEDGE AND RESEARCH (future constraints, not M4 work)

1. **Knowledge is distinct from technology and capability.**
2. Knowledge is ultimately **Empire-scoped**, while produced/applied through
   settlements, institutions, people and research activities.
3. **Research activities proceed in PARALLEL.**
4. **Knowledge generation is a resource/flow the player ALLOCATES among
   concurrent research activities.**
5. **Unallocated knowledge ACCUMULATES AS A RESERVE rather than disappearing**,
   and the player may deliberately hold a non-optimal reserve.
6. Knowledge may arise through deliberate research, practical discovery,
   diffusion, external exchange, espionage, or other explicitly defined
   mechanisms; open borders and foreign institutions may contribute.
7. **Do not collapse these concepts into a single rigid technology tree.**

## §10 ECONOMY (future constraints)

1. Economic resources stay **physically localised** even under one owner;
   **Empire-level control does not imply instantaneous empire-wide access.**
2. Local production, storage, trade, transport, consumption and military supply
   stay meaningful **because physical location matters.**
3. Money stays conceptually simple: **one economic ownership layer** — no
   population wallets, no household wealth simulation, no treasury/population
   split. Government spending, taxation, trade and finance may later create
   transfers *within* Empire economic state **without individual wallets.**

## §11 FUTURE MID-TURN CONTROL

Long-run design may require player control when important developments occur
during a long turn. **This is not an M4 requirement.** Any future mechanism
**must preserve deterministic replay** and be an **explicit temporal/control
architecture — never an ad hoc pause inserted into individual systems.**
*(Consistent with the open CR-006; this record does not resolve it.)*

## §12 ANTI-PATTERNS — DO NOT INTRODUCE

A monolithic Empire class · separate simulation architectures for player and AI ·
individual citizen wallets · treasury-versus-household money for realism alone ·
direct sibling-system calls · a universal `CapabilitySystem` God object · a rigid
one-at-a-time research queue · calendar-date technology unlocks where computed
predicates are intended · UI code that mutates simulation state · speculative
M5–M8 systems inside M4.

---

## §13 RELATIONSHIP TO D-037 — RECONCILED, NOT OVERRIDDEN

**D-037 A2 states: *"A POLITY IS A CLAIM, NOT A CONTAINER. A polity does not
'own' settlements as an axiom. Ownership is a contingent, simulated quantity that
can be won, lost and contested."*** §3.1 of this record calls the Empire "the
strategic control entity spanning one or more settlements", which could be
misread as the container D-037 rejects.

**The reconciling reading, and it is the intended one:**

- **Empire is the strategic-actor role of D-037's polity — not a third
  abstraction.** §1.3 forbids a separate competing polity and §1.4 forbids a
  separate Civilization, so no new entity is created. `PolityId` remains the
  identity key.
- **An Empire's settlement set is DERIVED from D-037's CONTROL relation, not
  owned as an axiom.** This record keys on control throughout: §4.2 says the
  Empire *"controlling that settlement"*, and §2.4 says an Empire ends when it
  *"controls no surviving settlement."*
- **D-037's three quantities — claim, control, recognition — are untouched and
  remain load-bearing.** Contested and multiply-claimed settlements remain
  expressible; §2.5's rebellion/fragmentation rules depend on exactly that.

**Nothing in D-037 is amended.**

---

## §14 STALENESS AND CONFLICT FINDINGS — recorded for director resolution, NOT rewritten

Per the packet's §15.5/15.6. **No document below was edited.**

### 14.1 D-018's class INCOME column — the one genuine staleness finding

`docs/d018-classes-and-needs.md` lists a per-class income column: Laborers
*"wage"*, Artisans *"skilled wage/own-shop"*, Clergy *"stipend/tithe"*, Soldiers
*"stipend"*, Bureaucrats *"stipend"*, Merchants *"trade profit"*.

**If read mechanically, that is per-class economic income — the shape §4.4/§4.6
now forbid.** If read descriptively (what the class *lives on*, as flavour and as
grievance-source vocabulary), there is no conflict.

**Status: UNRESOLVED. Owner: director.** D-018 is a closed D-decision and is not
touched here. **This is the wording §15.5 asked to be found**, and it must be
ruled before any class-income mechanic is specified.

### 14.2 "Treasury" wording — NOT a conflict, already owned elsewhere

`d009-d010:37` (*"treasury health"*), `d009-d010:43` (*"buy off leaders
(treasury)"*), `d018:21` (*"funding opposition (treasury, politics)"*).

**§4.4 bans a treasury-VERSUS-population split, not a treasury.** Under §4.5
money is an Empire-controlled resource, so an Empire treasury is exactly
conformant. **These lines are already in GOV-2 §1a's rewrite inventory** for a
different reason (they assume a money stock that is deferred). **No new conflict;
no new owner needed.**

### 14.3 D-041's *"stock held by a population"* — NOT a conflict

D-041 makes attachment *"an ACCUMULATED STOCK held by a population."* §4.4 bans
population ownership of **economic** resources. **Attachment is political/
affective, not economic**, so §4.4 does not reach it. **Recorded so that a future
reader does not "correct" D-041 by mistake.**

### 14.4 Terminology drift: "polity" (code and docs) vs "Empire" (this record)

`PolityId`, `ClaimRow`, `ControlRow`, `RecognitionRow` all use *polity*. Under
§13 these are the **same object**, so **no schema change and no rename is implied
by this record.** A future packet may align the vocabulary; **this one does
not.**

### 14.5 Note against a prior audit recommendation of mine

`docs/capability-architecture-decision.md` §5 proposes a capability *seam* shaped
`CapabilityState(scope, domain, capability)`. **§7.3 and §12 forbid a universal
`CapabilitySystem` God object.** The seam is conformant **only** as a *shared
predicate grammar consumed independently by each domain system* — the shape D-020
already has — and **not** as a coordinating owner. Recorded in that document too.

---

## §15 WHAT THIS RECORD SETTLES THAT WAS PREVIOUSLY OPEN

Recorded so these are not re-asked:

| previously open | now ruled |
|---|---|
| **Is an accumulating knowledge/science quantity permitted?** (CR-007 §6 / the Q1 blocker) | **YES** — §9.4/§9.5: knowledge is an allocatable flow and **unallocated knowledge accumulates as a reserve**, with §9.7 barring a rigid tree |
| Can research proceed in parallel? | **YES** — §9.3, and §12 bans a one-at-a-time queue |
| Must capability distinguish Empire vs Settlement scope? | **YES** — §8.3 |
| Is D-020 the capability foundation? | **YES** — §8.1 |
| Who owns money *in the model*? | **The Empire** — §4.5 |
| Player/AI architecture | **One pathway** — §6.5/§6.6 |

**Still open and NOT settled here:** which milestone owns money (CR-008) · era
gates in D-011 vs Law 4 (CR-009) · the definition of "institution" (CR-010) ·
mid-turn control and the campaign epoch (CR-006) · M5/M7 ownership (CR-005) ·
D-018's income column (§14.1).
