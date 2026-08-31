# CAPABILITY ARCHITECTURE — DECISION RECORD

**ARCHITECTURAL DECISION PASS. Nothing implemented. No production code, schema,
golden, corridor, quarantine or frozen document touched. Nothing certified or
merged.**

> **CORRECTION NOTICE (added later; original text below is NOT rewritten).**
> The adversarial pass that §0 records as NOT HAVING RUN has since run, and it
> **falsified two claims in this document**. Both are recorded in
> `docs/milestone-architecture-governance.md` §6.2:
>
> 1. **§4.1's appeal to the shipped exemplar is falsified.** This document claims
>    the class-emergence predicate demonstrates *"the predicate conjoins a
>    structurally-falsifiable-forever term."* **It does not.** One conjunct is
>    constant-true (`Variables.cs:29-32`, measured 3.5 ± 0.1 across all twelve),
>    and the other is universal delay by explicit tuning (`sim.json:167`: *"520
>    sits above the ~350-500 jittered founding sizes, so every settlement must
>    GROW into its artisans"*). **The fence proposed in §4.1 stands as a rule; its
>    evidentiary basis does not.**
> 2. **The latch is misdescribed.** §5 and the roadmap audit say a latch *"records
>    that a predicate has fired."* It records **current satisfaction under
>    hysteresis** — *"active + recede true → Active = 0… Recede absent = never
>    recedes"* (`ClassMobilitySystem.cs:28-33`).
>
> §4's science-model recommendation remains **UNVERIFIED** in the sense that
> matters: the adversarial pass returned **SURVIVES_WITH_CONDITIONS**, not a clean
> survival, and the conditions are in the governance record.

**VERIFICATION STATUS, STATED FIRST BECAUSE IT BOUNDS EVERYTHING BELOW.** Six
source audits were commissioned; **four returned (Q1–Q4) and two did not (Q5, Q6
— session limit)**, and **the three-lens adversarial verification of the Q1
recommendation DID NOT RUN.** Q5 and Q6 below were done directly by me at lower
depth. Under ADR-015 §6 — *no finding is actionable before its verdict returns* —
**§4's recommendation is UNVERIFIED and must not be built against until it is
attacked.** Every load-bearing claim from the audits was, however,
**independently re-verified against source by me** before being recorded here;
claims I could not verify are marked.

---

## §1 EXECUTIVE CONCLUSION

Four results, in order of how much they change the plan.

1. **The capability seam already exists and ships.** It is the **D-020 predicate
   DSL** over published variables, with **two live consumers**: class emergence
   and **recipe availability**. `Sim.Data/content/goods.json:3` describes
   `requires` as *"an optional D-020 availability predicate ('requires') — **a
   knowledge gate over published variables, never a calendar date (law 4)**"*,
   and `m3-spec.md:26` calls it *"a per-recipe **era/knowledge gate** expressed
   in the D-020 DSL."* Live data ships `"requires": "artisan_share > 0.05"`.
   **The anti-retrofit device is not new work. The work is to widen its SCOPE.**
   *(This corrects `m5-roadmap-dependency-audit.md` §4.1, which proposed building
   it — see §12.)*

2. **A per-CIVILIZATION capability is not representable today — this is the real
   blocker.** `PolityId` is a bare `record struct PolityId(int Value)`
   (`Ids.cs:54`); there is **no `PolityRow` type, no `Polities` table**, and
   **nothing anywhere in `Sim.Core`/`Sim.Cli` constructs a `PolityId` outside
   deserialization**. `VariableRow` is keyed `(SettlementId, VarId, Value)`
   (`WorldState.cs:353`) — settlement-scoped only. Any capability that belongs to
   a civilization has nowhere to live.

3. **Money has no owner in the tree, and "money is M5" is a transcription
   error.** GOV-2 §1a rules the opposite: *"M5 taxes in kind. **Money is NOT
   folded into M5** and does NOT get an inserted milestone before it; it remains
   deferred … **to a real milestone later in the ladder**."* The imagined
   M5-vs-M11 clash is not the problem; **the vacuum is.**

4. **Money is already ruled to be a capability-seam citizen.** The same ruling:
   *"money then arrives as an institution that **EMERGES** — … **coinage must
   derive from computed state**, never from a date."* That is the seam's job
   description, ratified, before anyone proposed a seam.

**The answer to the framing question** — *what must exist before the advanced
economy, governance, diplomacy and warfare so technology is not retrofitted?* —
is therefore **not a research system and not a milestone reorder.** It is
**a scope widening of a mechanism that already ships**, plus **polities becoming
first-class**, because without the second the first has nothing to attach to.

---

## §2 EXISTING RATIFIED CONSTRAINTS

- **Milestone order** (D-011 §6, ratified, used by D-040): M4 trade + strategic
  war (**shipped**, T4.8) · M5 governing loop · M6 Battle Layer v1 · M7 knowledge
  & divergence · M8 politics & diplomacy · M9 society · M10 Ancient Vertical Slice.
- **Law 2** mechanisms over modifiers · **Law 4** no calendar gates · **Law 1**
  conservation · **Law 5** determinism.
- **D-040 B3** no technology unlock (§3).
- **GOV-2 §1a** M5 taxes in kind; money deferred; coinage must emerge.
- **D-005 (FROZEN, M0)** *"Money as `long` minor-units of an abstract currency"* —
  **singular**.
- **D-041** ratifies accumulated stocks whose input is time (§4).
- **D-039 A1** *"units change, verbs don't."*

---

## §3 D-040 B3 — WHAT IT ACTUALLY PROHIBITS

**Prohibited** (`d040:59-64`): the tech-tree **node** as the thing that opens a
capability — *"a tech-tree node opening sea travel is a calendar gate wearing a
tree."*

**Permitted** (`d040:66-70`): *"sea travel becomes possible when the conditions
for boats exist — a coastal settlement, timber, and craft capacity — **in the
same shape as class emergence** … **A landlocked polity never develops it; a
coastal one does; nobody schedules either.**"*

**The operative test is that last clause**: a legitimate predicate must be able to
be **false forever** for a structurally unsuited civilization.

---

## §4 SCIENCE / RESEARCH MODEL COMPARISON — **RECOMMENDATION UNVERIFIED**

### 4.1 The crux, resolved on evidence

**A threshold on an accumulating stock is NOT per se what B3 bans — because B3's
own sanctioned exemplar contains one.** The predicate B3 points at is
`"emerge": "food_surplus_ratio > 1.3 && population > 520"`
(`sim.json:165`), and `population` is an accumulation summed from conserved `long`
bucket counts (`ClassMobilitySystem.cs:148-152`).

**Two properties separate the legitimate case from the illegitimate one, and both
are visible in that exemplar:**

1. **The accumulator can FALL.** The predicate ships `"recede": "food_surplus_ratio < 1.1"`
   (`sim.json:166`), and the latch is reversible — *"1 once the class's emergence
   predicate has fired, back to 0 only via its recession predicate"*
   (`WorldState.cs:355-362`). Population is non-monotone by design (Spine: *"boom–crash–recovery"*).
2. **The predicate conjoins a structurally-falsifiable-forever term**, so some
   civilizations are excluded by circumstance rather than merely delayed.

**Therefore a threshold on a MONOTONE-IN-TIME accumulator IS a tech-tree node
wearing different clothes.** If `science` only rises, `science > K` is `year > N`
with a per-civilization rate constant, and B3's exhibited test becomes
unexhibitable. **And ordered thresholds K1 < K2 < K3 on one accumulator are tree
edges with the edges hidden in the numbers** — that is the failure mode to write
into any fence. *(This inference is not stated in any ratified document.)*

**Decisive precedent that accumulation itself is legal:** **D-041**, a director
ruling that *"Extends D-040"*, ratifies *"an **ACCUMULATED STOCK** … slow to build
and slow to lose"* whose *"inputs are **TIME UNDER CONTROL**"* — legal because it
*"feeds behaviour continuously"* and is *"a **LEVER** rather than a
**COEFFICIENT**"*, never a boolean grant.

### 4.2 The five models

| model | B3-compatible | "no tree" | hidden tree? | progress-bar? | circumstantial discovery | rapid breakthrough | parallel domains | divergent paths |
|---|---|---|---|---|---|---|---|---|
| **A** Civ: stock → project → completion → unlock | **NO** | no | is one | yes | lost | no | queue-bound | no |
| **B** science as pressure, no completion event | yes | yes | no | no | yes | yes | yes | yes |
| **C** domain knowledge + thresholds | **only if §4.1's two properties hold** | risk | **yes if monotone/ordered** | at thresholds | partial | yes | yes | yes |
| **D** pure structural emergence | **yes** | **yes** | no | no | **yes** | **yes** | yes | **yes** |
| **E** hybrid: knowledge as readiness, emergence still computed | yes | yes | no if unordered | no | yes | yes | yes | yes |

**Recommended: E, floored by D. Reject A outright. C only under §4.1's two
properties.** **UNVERIFIED — the adversarial pass did not run.**

---

## §5 THE CAPABILITY SEAM

> **CONSTRAINT ADDED BY D-042 (director ruling; this section is not rewritten).**
> D-042 §7.3 and §12 forbid *"a universal God system such as a `CapabilitySystem`
> that owns every capability or coordinates every domain."* The seam proposed
> below is conformant **only** as a **shared predicate grammar consumed
> independently by each domain system** — which is the shape D-020 already has,
> and which D-042 §8.1 ratifies as the foundation. It is **not** conformant as a
> coordinating owner, and the `CapabilityState(scope, domain, capability)`
> notation below must be read as *a way of asking*, never as *a system that
> answers for everyone*. D-042 §8.3 independently ratifies the scope point:
> capability evaluation must distinguish **Empire-level and Settlement-level**.

**It exists. Do not rebuild it.** The D-020 predicate DSL is a closed
deterministic grammar over registered variables, with two shipped consumers
(class emergence; recipe `requires`).

**But the tree ships TWO seams with contradictory answers**, and reconciling them
is the design work:

| | class emergence (`ClassStateRow`) | recipe gate (`requires`) |
|---|---|---|
| storage | **stored** latch, serialized | **pure derived**, no state |
| hysteresis | **yes**, with `recede` | **none** |
| flip cost | moves conserved people | changes availability only |

**The rule for choosing is mechanical, not aesthetic: does flipping move a
conserved stock?** If yes, hysteresis is mandatory (an oscillating input would
chatter people back and forth every turn — the reason `ClassStateRow` exists). If
no, a pure predicate is correct.

**Recommended shape: `CapabilityState(scope, domain, capability)`** —
- not `CanDo(civ, capability)`: a bare boolean is the recipe gate, correct only
  for instantaneous reversible effects;
- **scope generalized, not fixed to "civ"** — because civ does not exist (§1.2).
  The correct substrate is a **relation** `(scopeKind, scopeId, varId, value)`,
  never a field bolted onto a settlement row;
- **domain** carries no mechanism; it is what makes *"no tree; domain lattice
  lite"* expressible as parallel independent lattices rather than one graph.

**Prerequisites without a tech tree:** a prerequisite is a **conjunct in a
predicate over published variables**, not an edge to another capability. Capability
A referencing capability B's *latch* is how a tree grows back — that is the line
to hold.

---

## §6 KNOWLEDGE, EDUCATION, INSTITUTIONS

**The dependency chain in `m5-roadmap-dependency-audit.md` §2 fails at one link.**
It said institutions → funding → **money/taxation**. GOV-2 §1a rules M5 taxes
**in kind**, so institution funding never had to wait for currency.

**The shipped proof is Housing**: built from timber, consuming construction labour
in adult-years, and degrading under unmet maintenance through a named Ledger sink
— **institution-scale funding with an upkeep failure mode, already conserved,
already dt-correct, with no money anywhere near it.**

**Corrected chain:** institutions → **in-kind upkeep** → M5 governing loop → M7
knowledge. **So knowledge belongs at M7 — not earlier, not later.**

**Ratified vs new** — three ratified mechanisms already depend on literacy or
education (D-018 Intelligentsia emergence, the Prospects need, rising
expectations), **but the predicate registry ships only three variables**
(`Variables.cs`). *The dependency is ratified; the variables to satisfy it do not
exist.* That gap is real M7 work.

**Unresolved:** four documents assign **four different meanings** to
"institution" across three milestones — political module (M7→M8), a need
trade-off binding M5 by name (D-035), a settlement capability (D-038), and the
knowledge-conversion mechanism (the M5 placeholder). **This must be ruled before
anyone writes an institution packet.**

---

## §7 ECONOMIC TECHNOLOGY

**There is no M5-vs-M11 contradiction.** GOV-2 §1a itself enumerates the Finance
row precisely because *"Finance (banking, debt, panics) — era exp."* presupposes
money exists before the early-modern expansion. Money-the-medium and
banking/debt/panics are different objects at different depths, and the staging is
historically correct.

**The real conflict is different and blocking:** `m4-spec.md` says money is at M5
in **eight** places and cites GOV-2 §1a as its source, while GOV-2 §1a says the
opposite. `d039:45` reads the ruling correctly — which is how we know this is
**transcription drift, not a later amendment**. **Consequence: money has no owner
in the tree at all.**

**And a frozen-kernel obstacle nobody has named:** D-005 commits to *"Money as
`long` minor-units of an **abstract currency**"* — **singular**. The progression's
*currencies → foreign exchange → exchange rates* stages need **N conserved money
quantities, one per issuer** — a conserved-quantity-registry change **inside the
frozen M0 kernel**.

**Does money need the seam? YES, and it is already ratified** — *"coinage must
derive from computed state."* Split the object: the **medium** (grain numéraire,
shipped) is not a capability; **coinage, credit, banking, FX** are.

---

## §8 WARFARE DEPENDENCY — M6 NOT REDESIGNED

**The apparent tension resolves.** D-011 §5 says later eras arrive as *"data + a
few new verbs"*; D-039 A1 tightens it: *"D-011 §1 fixes the order verbs as
constant across all eras — **units change, verbs don't**. What changes is **WHAT
THE COMMANDER KNOWS** and **HOW FAST ORDERS ARRIVE**."*

**So era-varying warfare is already ratified as (a) unit DATA and (b) three
information quantities — POSITION, STRENGTH, OUTCOME — not new mechanisms.** That
is exactly compatible with capability-gated warfare, and it means **M6 needs
nothing from M5/M7**: ancient units are data, and the information channels are
D-039's, not technology's.

| | M6 needs? | later warfare needs from capability? | kind |
|---|---|---|---|
| weapons, armour | no (ancient data) | **yes** | data, capability-gated |
| fortifications | no | yes | data |
| logistics | partly (supply, shipped M4) | yes | mechanism |
| organization, training, doctrine | no | **yes** | capability |
| communications | **D-039's latency, not tech** | yes | quantity |
| industrial production | no | **yes** | economy |
| naval | auto-resolve until post-slice | yes | capability |
| air, strategic weapons | no | yes | era expansion |

**M6 remains valid untouched.**

---

## §9 TEMPORAL / DISCOVERY

*(Done by me; the Q6 audit did not run. CR-006 already analyses this — not
re-derived.)*

| stage | where it can live today |
|---|---|
| state evolving during a turn | inside the step; state exists only at boundaries |
| capability becoming emergent | the predicate flips during the step |
| capability becoming **known** | turn boundary |
| player receiving a decision | **turn boundary** — no sub-turn coordinate |
| player issuing an order | `OrderLog` is **turn-stamped** |
| order execution | next step |

**Do not invent a sub-turn order timestamp.** CR-006's Option C — a decision event
**ends the turn early**, so the interrupt **is** a boundary — remains the only
option needing no kernel re-entrancy. **A mitigation worth noting: dt already
falls 10 → 0.5 across eras, so the "wait seven years" problem shrinks by 20× in
exactly the later eras where discoveries cluster.** The worst case is the
Neolithic, where discoveries are rarest.

---

## §10 MILESTONE OWNERSHIP — CORRECTED

**No reorder recommended.** D-011 §6 stands.

| M | owns | prerequisite interface it must expose |
|---|---|---|
| M4 (now) | trade, strategic war, claims/control | **polity instantiation** (§1.2) |
| M5 | governing loop, **in-kind** taxation, authority, legitimacy | institution upkeep |
| M6 | Battle Layer v1 | unchanged; needs nothing from M5/M7 |
| M7 | **knowledge & capability generation** | widened seam scope |
| M8 | politics & institutions | institution definition (§6) |
| M9/M10 | society; Ancient Vertical Slice | — |
| M11+ | Finance (banking, debt, panics), era warfare | money owner (§7) |

**Money is unowned and must be assigned.**

---

## §11 PARALLEL WORK PLAN

- **A. Polity instantiation** — blocker for any per-civ capability. Independent.
- **B. Seam scope widening** — the `(scopeKind, scopeId, varId, value)` relation.
  Depends on A for its scope key.
- **C. Variable registry growth** — literacy/education variables that three
  ratified mechanisms already assume. Independent of A and B.
- **D. Institution definition ruling** (§6) — documentation; independent.
- **E. Money ownership ruling** (§7) — documentation; independent.

**A blocks B. C, D, E are independent of everything and of each other.**

---

## §12 CORRECTIONS TO MY OWN PRIOR DOCUMENTS

Recorded, not silently fixed. `m5-roadmap-dependency-audit.md` (`a3e1740`) and
`cr-005` contain three errors:

1. **"Money is M5 … five by-name deferrals."** Wrong twice: there are **eight**
   occurrences, and **GOV-2 §1a rules money is NOT at M5.** I inherited
   `m4-spec.md`'s compression of *"M5 taxes in kind"* into *"money is M5"*. The
   correct finding is that **money is unowned**.
2. **"§4.1 the capability seam — the anti-retrofit device"** proposed as new work.
   **It already ships** (D-020 DSL, two consumers). The work is scope widening.
3. **§11's caveat** said the food investigation *"found one such predicate reading
   a post-destruction stock."* **§7.3 does not say that** — it concerns
   *migration's* food attractiveness, a **reader**, not an emergence predicate.
   The class-emergence predicate reads `food_surplus_ratio` from a **flow**.
   Withdrawn.

---

## §13 CONFLICTS WITH RATIFIED / FROZEN DOCUMENTS

| # | conflict | severity |
|---|---|---|
| 1 | `m4-spec.md` ×8 "money is M5" **vs** GOV-2 §1a "money is NOT folded into M5" | **BLOCKING** |
| 2 | Per-civ capability **vs** no `PolityRow`, no table, no constructor | **BLOCKING** |
| 3 | D-009/D-010 *"expensive, **era-gated**… edges"* **vs** D-040 B3 / Law 4 — D-040 flags this against itself at :223-227 and does not resolve it | **BLOCKING** (pre-existing) |
| 4 | D-005 frozen singular currency **vs** FX/exchange rates | MAJOR |
| 5 | "institution" means four things across three milestones | MAJOR |
| 6 | M5 placeholder §7's *research-completion event* **vs** B3 (it is Model A) | MAJOR |
| 7 | M5 scope items 7–11 (projects, throughput, prerequisites) **vs** B3 | MAJOR |
| 8 | D-018 artisan trigger **vs** shipped predicate (D-040 F2, unowned) | MINOR |
| 9 | Spine "Knowledge M6" **vs** D-011 §6 "M7" — known-stale | MINOR |

**No frozen document was edited.**

---

## §14 OPEN DIRECTOR DECISIONS

**Before M5/M7 can be specified:**
1. **Q1 ruling: is an accumulating knowledge quantity permitted, and under what
   fence?** Recommended fence: the accumulator must be able to **fall**, the
   predicate must conjoin a **structurally-falsifiable-forever** term, and
   **ordered thresholds on one accumulator are banned**. *Narrowing B3 requires a
   CR.* **The adversarial pass has not run.**
2. **Who owns money?** (Conflict 1 — it is currently nobody.)
3. **Does `m4-spec.md`'s "money is M5" get corrected as transcription drift?**
4. **What is an "institution"?** (Conflict 5.)
5. **Are polities instantiated at M4, or later?** (Conflict 2 — gates the seam.)

**Can wait:** science/knowledge one stock or two · institution lifecycle · decay ·
number of domains · **what calibration corridor gates knowledge** (unanswered and
serious — the project gates milestones on corridors).

**Later:** brain drain via existing migration machinery · AI research strategy ·
FX (needs Conflict 4 resolved) · espionage.

---

## §15 RECOMMENDED NEXT PACKET

**Not a research system, and not the seam.** The next packet is the cheapest thing
that unblocks the most: **a documentation-only CR resolving Conflict 1 (money
ownership) and Conflict 3 (era-gated edges vs B3)** — both are ratified-document
contradictions that will otherwise be inherited by every downstream design, as
Conflict 1 already was by two of mine.

**Then, in parallel:** the Q1 ruling (§14.1) with its adversarial pass actually
run, and a scoping packet for **polity instantiation** — because until a
civilization is a thing in the state, "can this civilization do X?" has no
subject.
