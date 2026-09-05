# PROGRESSION ARCHITECTURE — THE MASTER CLASSIFICATION

**DESIGN DOCUMENT. Nothing implemented. No mechanic, formula, schema, test,
golden, corridor or quarantine touched, and no M6/M7/M8 gameplay activated.**

This is the answer to "which of our progression systems is a tree, and which only
looks like one". It exists so that a later milestone cannot quietly invent a
Civ-style unlock ladder for its own domain.

---

## §0 THE RATIFIED CONSTRAINT THAT GOVERNS EVERYTHING BELOW

`docs/d040-discovery-and-control.md` **B3 is a ratified director ruling**:

> **"NO TECHNOLOGY UNLOCK. LAW 4 BINDS.** … a tech-tree node opening sea travel is
> **a calendar gate wearing a tree**."
>
> **"The computed version: sea travel becomes possible when the conditions for
> boats exist** — a coastal settlement, timber, and craft capacity — **in the same
> shape as class emergence** … **A landlocked polity never develops it; a coastal
> one does; nobody schedules either.**"

**The project has already rejected the *unlock shape itself*, not merely the
*bonus* shape.** The shipped template is `ClassStateRow.Active` — a hysteresis
latch that records that a predicate *has fired*, not an inventory of purchased
nodes.

Two further ratified laws bind the same way:
- **Law 2, mechanisms over modifiers** — "unlock a bonus" is the banned shape.
- **Law 8** — institutions and doctrines live in **data files**; code implements
  mechanisms only. Every graph in this document is therefore DATA, and no
  milestone should add a C# enum for any of it.

### §0.1 THE OPEN QUESTION THAT LIMITS THIS DOCUMENT

`docs/m5-roadmap-dependency-audit.md` §8 Q1 is **OPEN and director-owned**, and its
own §10 makes it item 1, blocking everything:

> *"Is an accumulating 'science' stock compatible with D-040 B3 at all? A project
> that completes and grants a capability is an unlock with a progress bar. Either
> B3 is narrowed to 'no tree EDGES' while permitting accumulation, or capability
> must emerge purely from structural preconditions with no research resource."*

**This document does not answer it and does not assume an answer.** Every
structure below is specified so that **both** rulings remain buildable from it:

- if **pure emergence** is ruled, the domains become *named predicate families*
  and the "level" is a computed reading of structural preconditions;
- if **accumulation** is ruled, the same domains gain a continuous state variable
  and the same breakthrough conditions become its thresholds.

**Nothing here commits to a science stock.** Nothing here defines a research
project, a progress bar, or a purchase.

---

## §1 THE CLASSIFICATION

Every progression family in the GDD, classified. **A** lattice · **B** module
graph · **C** continuous · **D** emergent · **E** true prerequisite tree ·
**F** should NOT be a progression tree at all.

| family | class | why | milestone (D-011 §6 order) | now |
| --- | --- | --- | --- | --- |
| **Knowledge / technology** | **A + D** | Domains are a lattice of *relationships*, but advancement within them is **emergent from computed preconditions** (D-040 B3). Never edges-you-buy. | **M7** | **data/spec only** |
| **Civic / institutional** | **B** | Institutions compose. A polity runs a bureaucracy *and* a legal code *and* a succession rule; these are not steps on a ladder. Enum governments are explicitly refused. | **M8** | **data/spec only** |
| **Economic organisation** (barter → accounting → coinage → credit → banking) | **D**, staged | Each stage has real structural preconditions (accounting needs literate administration; coinage needs metallurgy + a state that certifies weight). Emerges, never unlocks. | **M11+** (Finance), unless moved | **not started** |
| **Military doctrine** | **B** | Doctrines are composable modules constrained by capability and institutions, not a ladder. | **M9+** (Military full) | **not started** |
| **Religious doctrine** | **B + D** | Composable tenets that emerge from and feed back into society. | **M9** (society) | **not started** |
| **Legal systems** | **B** | A subset of the civic module graph, not its own tree. | **M8** | folded into civic graph |
| **Education provision** | **B** | An institutional module whose *output* feeds knowledge. Not a tree. | **M8**, consumed by M7 | folded into civic graph |
| **Social organisation / class structure** | **D** | **Already shipped as emergence** — D-018 classes emerge on published-variable predicates with a hysteresis latch. This is the project's proof the shape works. | M2–M4 ✅ | **live** |
| **Infrastructure** | **C + D** | Continuous stocks (roads, dwellings, structures) built by real labour and materials. Already partly shipped. | M4 ✅ / later | **partly live** |
| **Era labels** | **F** | **NOT a progression system at all — a computed READING.** See §3. | M7 | **design only** |
| **"Civilization advancement" as a single score** | **F** | There is no such quantity. Refused: it is the aggregate mood-score shape Law 2 bans. | — | **refused** |

**Two entries are the load-bearing ones**: knowledge is **A + D**, not E; civics
is **B**, not E. Nothing in the GDD is classified **E** — *no family in this
project is a true prerequisite tree.* That is the headline result.

---

## §2 WHY KNOWLEDGE IS A LATTICE **AND** EMERGENT

A lattice of ~20–30 domains describes *how knowledge relates to knowledge*:
metallurgy is near materials, astronomy is near mathematics and navigation. Those
relationships are real and worth writing down as data.

**But relationship is not prerequisite.** Under D-040 B3, a domain does not open
because a neighbouring domain was "completed". Advancement in a domain is a
predicate over **structural preconditions** — population, class composition,
institutions, materials, contact, geography — evaluated exactly as class emergence
is. The lattice supplies *plausibility and adjacency*; the world supplies whether
it happens.

**Concretely, the difference:**

| Civ-shaped (REFUSED) | this project (D-040 B3) |
| --- | --- |
| `Bronze Working` → `Iron Working` edge | iron working becomes possible where ore, fuel, smiths and demand coexist |
| research points accumulate, node unlocks | a predicate over computed state fires and latches |
| everyone follows the same order | a landlocked polity never develops boats; nobody schedules either |
| a node grants +10% something | capability changes what is *possible*, mechanisms do the rest |

**Discovery, diffusion and adoption stay three distinct things.** Awareness is not
adoption: a polity may know of a technique and be unable to adopt it because the
capital replacement cost, the skills, or the institutional fit are absent. Any
design that collapses these into `TechnologyUnlocked = true` is refused.

**Technology loss is therefore free.** Because a latch records a fired predicate
over *current* structural conditions, a polity that loses the conditions — its
smiths, its institutions, its trade contact — can lose the capability without any
separate "forgetting" mechanism. A purchased-node model cannot express that
without inventing one.

---

## §3 ERA LABELS ARE A READING, NOT A GATE

**Law 4 binds: no calendar gates.** `Year > X → Era Y` is refused outright, and so
is its disguised form, `TechCount > N → Era Y`.

An era label is a **computed classification of a civilization's current
structural state** — what it actually knows, has adopted, has built, and how it is
organised. It is derived, displayed, and consumed by nothing. **The same world may
legitimately contain civilizations at different computed eras at the same instant,
and a civilization may move backwards.** That is the point of the reading.

**An era label must never appear as an input to any mechanism.** If a mechanism
needs to know whether something is possible, it asks the capability predicate, not
the era.

---

## §4 THE CAPABILITY SEAM — THE ANTI-RETROFIT DEVICE

`docs/m5-roadmap-dependency-audit.md` §4.1 identifies the one thing that is both
required by every later system and implementable before any of them: **a single
uniform way for any system to ask "can this civilization do X?", answered from
computed state.**

If that seam exists, then taxation, credit, government forms, military doctrines
and diplomatic instruments all express availability *through it*. If it does not,
each milestone invents its own gate — **and that is precisely the retrofit this
whole document exists to prevent.**

**The seam is a CONTRACT, not a mechanism: no science stock, no research projects,
no domains, no constants.** The shipped D-020 predicate architecture plus
`ClassStateRow`'s hysteresis latch is already its template; the seam is the
generalisation of what M2 shipped for classes.

**Whether it lands before M5 is §8 Q2 and is open.** It is not built here.

---

## §5 WHAT IS DATA-ONLY NOW, AND WHO OWNS IT LATER

| artifact | status | future owner |
| --- | --- | --- |
| `docs/progression-architecture.md` (this file) | **specification** | standing |
| `docs/knowledge-domain-lattice.md` | **specification, data-only** | **M7** |
| `docs/civic-institutional-module-graph.md` | **specification, data-only** | **M8** |
| capability seam contract | **not written** — blocked on §8 Q2 | late-M4 / early-M5 per the audit |
| any `data/knowledge/*.json` | **NOT created** — would be premature under §0.1 | M7 |

**No simulation loop is wired. No schema changed. No M6/M7/M8 gameplay activated.**

The data files are deliberately **not** written yet: under §0.1 the shape of a
domain's state (a predicate family versus a continuous level) is exactly what the
open ruling decides, and writing the JSON now would bake in an answer the director
has not given.
