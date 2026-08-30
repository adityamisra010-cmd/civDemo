# MILESTONE ARCHITECTURE GOVERNANCE — AUDIT AND CONTRADICTION RECORD

**DOCUMENTATION-ONLY. No production code, schema, JSON, golden, corridor,
quarantine or frozen document touched. Nothing certified, merged or pushed.**

**Scope note, stated because it deviates from the packet as written.** Six
workstreams were specified (A–F). **A, B and E were NOT re-run**: they map onto
audits Q1, Q4 and the roadmap audit that already landed and that I re-verified
against source, and the packet's own discipline forbids running the same
investigation twice. **C, D and F were run**, F being the adversarial pass that
died on the session limit last time and which gates everything else. All 12
agents completed. **Every load-bearing claim below was independently re-verified
by me against source before being recorded.**

---

## §1 EXECUTIVE FINDING — **WITHDRAWN BY CR-007**

> **CORRECTION NOTICE (added later; the original §1 is preserved below, not
> rewritten).** This section's headline — *"B3's own sanctioned exemplar fails
> B3's own test"*, recorded as **BLOCKING contradiction #1** — **is WITHDRAWN.**
> See `docs/adr/cr-007-b3-exemplar-reconciliation.md` §4.1.
>
> **The misreading:** in B3's *"A landlocked polity never develops it"*, **"it"
> refers to SEA TRAVEL**, not to the artisan class. The subject is the boats
> predicate — *coastal settlement AND timber AND craft capacity* — whose first
> conjunct is permanently false for a landlocked polity, coastal geography being
> static terrain. **That predicate does exhibit structural exclusion.** The
> class-emergence predicate is cited for its **SHAPE** (*"in the same shape as
> class emergence"* = a conjunction of computed conditions rather than a date),
> not as a demonstration of permanent exclusion.
>
> **And D-040 had already quarantined the residue:** Part F states *"None changes
> a ruling; all change what may be cited in support of one"*, and F2 records the
> reference's defects — including *"'market extent' is implemented as RAW
> POPULATION… **Noted, not ruled**"*.
>
> **The measurements below are correct and stand. The inference from them does
> not.** Consequently §3's verdicts **#1, #6 and #10** lose their principal
> supporting argument. **#2, #3 and #5 are untouched and still stand**, as does
> the six-meanings institution finding.
>
> Original text follows.

### §1 (original, superseded) — the exemplar that licenses the whole model fails its own test

D-040 B3 replaces the tech tree with capability emerging *"in the same shape as
class emergence"*, and stakes the ruling on one operative test: **"A landlocked
polity never develops it; a coastal one does"** — a legitimate predicate must be
able to be **false forever**.

**The only emergence predicate that ships does not exhibit that test. Both of its
conjuncts fail, and one fails by deliberate tuning.**

```
"emerge": "food_surplus_ratio > 1.3 && population > 520"      sim.json:165
```

- **Conjunct 2 is universal delay, by design.** `sim.json:167`: *"TUNE: 520 sits
  above the ~350-500 jittered founding sizes, so **every settlement must GROW
  into its artisans**."* Nobody is excluded; everyone arrives late.
- **Conjunct 1 is constant-true.** `Variables.cs:29-32`: *"a labor-limited
  food_surplus_ratio is adultShare × output / consumption, **identical at every
  size (measured 3.5 ± 0.1 across all twelve)**."*

So the sanctioned exemplar is **a schedule with jitter**, not structural
exclusion — and the accepted remedy when it was measured emerging in lockstep was
**date spread**, not exclusion.

**This falsifies part of my own `capability-architecture-decision.md` §4.1.** I
wrote that the exemplar demonstrates a predicate conjoining *"a
structurally-falsifiable-forever term."* **It does not.** The fence I proposed is
sound as a *rule*, but it **cannot be justified by pointing at the shipped
exemplar**, because the exemplar violates it. Corrected in §6, not silently.

**What actually blocks M5/M7+ is therefore narrower and harder than "pick a
science model":** the project has a ratified prohibition (B3) whose only worked
example does not satisfy the prohibition. Until that is reconciled, every
capability predicate written against the exemplar inherits the defect.

---

## §2 RATIFIED FACTS — NOT OPEN FOR DEBATE

1. Milestone order per D-011 §6: M4 trade + strategic war (**shipped**) · M5
   governing loop · M6 Battle Layer · M7 knowledge · M8 politics · M9 society ·
   M10 slice · M11+ era expansions.
2. **D-040 B3**: no technology unlock; Law 4 binds.
3. **D-041** (extends D-040): an accumulated stock whose input is *"time under
   control"* is legal **because it is a lever feeding behaviour continuously,
   never a boolean grant**.
4. **GOV-2 §1a**: M5 taxes **in kind**; money is **not** at M5; *"coinage must
   derive from computed state."*
5. **D-005 (frozen)**: *"Money as `long` minor-units of an abstract currency"* —
   **singular**.
6. **The capability seam ships**: D-020 predicate DSL, two consumers (class
   emergence; recipe `requires`). Grammar is closed — *"No functions, no
   arithmetic (v1)"*.
7. **`Ledger.Transfer(ref Conserved from, ref Conserved to, long, OverdrawPolicy)`**
   is **two-endpoint** (`Ledger.cs:50`).
8. **No polity entity exists**: no `PolityRow`, no table, no constructor outside
   deserialization; `VariableRow` is settlement-keyed.

---

## §3 ADVERSARIAL RESULT — 3 FALSIFIED, 7 CONDITIONAL, 0 CLEAN SURVIVALS

**Not one of the ten assumptions survived unconditionally.**

| # | assumption | verdict |
|---|---|---|
| 1 | science can accumulate without becoming a disguised tree | **CONDITIONAL** |
| 2 | knowledge should precede advanced military; order achieves it | **FALSIFIED** |
| 3 | knowledge should precede advanced economy | **FALSIFIED** |
| 4 | institutions designable independently of knowledge | **CONDITIONAL** |
| 5 | polity state can be postponed past M4/M5 | **FALSIFIED** |
| 6 | capabilities can stay emergent, not a tech tree | **CONDITIONAL** |
| 7 | government transforms availability without breaking Law 2 | **CONDITIONAL** |
| 8 | multiple research processes in parallel | **CONDITIONAL** |
| 9 | rapid discovery under the atomic turn | **CONDITIONAL** |
| 10 | circumstantial tech without a scripted simulator | **CONDITIONAL** |

### 3.1 #5 FALSIFIED — polity state cannot be postponed past M5 (Law 1 forces it)

The strongest result in the pass, and it is mechanical rather than aesthetic.
The Spine's M5 line is *"Taxation, **budget**"* — a stock word. GOV-2 moved the
**denomination** (in kind), not the milestone. And Law 1 offers exactly two
channels: `Transfer` needs **two endpoints**; `Flow` is *"value entering the
world (source) or leaving it (sink)."*

So an in-kind tax has three possible destinations, and two are absurd: a **Flow
sink destroys the grain** — confiscation-as-annihilation — and **a budget you
cannot spend is not a budget**. The remaining option is a `Transfer` into a
**polity-scoped stock that does not exist**.

**M5 taxation therefore requires a polity treasury endpoint. Polity state is an
M4/M5 prerequisite, not an M7/M8 convenience.**

### 3.2 #2 FALSIFIED — advanced military is era-gated in a FROZEN document

Milestone precedence is *build* order, not *causal* order. The ratified gate on
advanced military is **an era label, not knowledge**, in three places in frozen
D-011:

- `:13` *"(**era-gated** additions: bombard, air strike, dig-in)"*
- `:45` *"Later eras arrive as data + a few new verbs… gunpowder… industrial… modern…"*
- `:66` *"| M11+ | era expansions | each adds its battle-layer units/verbs as data |"*

**This is the same shape B3 rejects, in frozen material.** D-040 already flagged
one instance of this (D-009/D-010's *"expensive, **era-gated**, terrain-crossing
edges"*) against itself at `:223-227` and declined to resolve it. **With D-011 it
is no longer an isolated blemish — it is a pattern**: era-gating is embedded in
ratified infrastructure *and* military documents while Law 4 and B3 forbid it.

### 3.3 #3 FALSIFIED — economy and knowledge are a CYCLE, so neither "precedes"

*"The economy funds institutions that produce knowledge that changes the
economy."* **A cycle has no "precedes."** And the two economic capability gates
that already ship — bronze-casting and toolmaking, both
`"requires": "artisan_share > 0.05"` — decide it the other way: an **economic**
predicate gates a **technological** recipe today.

### 3.4 The conditional verdicts worth acting on

- **#1/#6/#10** all converge on §1: the fence is right, the exemplar cannot
  justify it.
- **#7** — *government transforms rather than deletes acquired capability*
  requires **monotonic acquisition**. **The shipped latch does not provide it**
  (§6.2).
- **#8** — "multiple research *projects* in parallel" is Model A wording and is
  already logged as Conflict 6/7 against B3. It survives only under a reading
  that removes the completing project.
- **#9** — the atomic turn is *"a calendar-selected quantum"*: dt is 10 years in
  the Neolithic, and the only shipped discovery chain crosses **three** turn
  boundaries. "Rapid" is meaningful in turns, not sim-years.

---

## §4 INSTITUTIONS — SIX MEANINGS, NOT FOUR, AND ZERO CODE

**`institution` appears nowhere in `Sim.Core/`, `Sim.Data/`, `Sim.Cli/` or
`Sim.Tests/`.** Every occurrence is in `docs/`. There is no mechanical
institution.

The prior count of four meanings was an **undercount — there are six**:

| # | meaning | source |
|---|---|---|
| 1 | composable political **module** | Spine `:85`, `:111` (M7→M8) |
| 2 | need **trade-off**, binds M5 by name | D-035 `:86` |
| 3 | settlement **structure** / sprite part | D-038 `:170` |
| 4 | knowledge **conversion mechanism** | M5 placeholder (unratified) |
| 5 | **argument to the control function** | D-041 `:35` |
| 6 | **predicate operand** in class emergence | D-018 `:25` etc. |

**Two are load-bearing and mutually incompatible in kind**: (5) and (6) make an
institution a *published scalar*; (1) makes it a *composable module*; (3) makes
it a *built structure*.

**The funding pattern already ships** — Housing consumes in-kind timber/clay
upkeep and degrades under unmet maintenance through a named Ledger sink.
**Institutions never needed money**, which independently confirms §6.1's
correction.

**Law 2 hazard flagged, not a violation:** the unratified placeholder uses
*"government modifiers"* and *"education and literacy as **modifiers**"*. A
free-floating permanent modifier is the banned construct. **D-035's shipped shape
is the legal one** — *"one institution raises one need and lowers another"*, a
two-sided mechanism.

---

## §5 TRUE CONTRADICTIONS

| # | contradiction | severity |
|---|---|---|
| 1 | **B3's operative test vs B3's own sanctioned exemplar** (§1) | **BLOCKING** |
| 2 | `m4-spec` ×8 "money is M5" vs GOV-2 §1a "money is NOT folded into M5" | **BLOCKING** |
| 3 | **Era gates in frozen D-011 (`:13`, `:45`, `:66`) + D-009/D-010 vs Law 4 / B3** | **BLOCKING** |
| 4 | M5 taxation (Law 1, two-endpoint) vs no polity stock to receive it | **BLOCKING** |
| 5 | "institution" = six meanings, zero code | **BLOCKING** |
| 6 | D-005 singular currency vs FX / exchange rates | MAJOR |
| 7 | M5 placeholder's research-completion event vs B3 (it is Model A) | MAJOR |
| 8 | D-018 artisan trigger vs shipped predicate (D-040 F2, unowned) | MINOR |

---

## §6 CORRECTIONS TO MY OWN PRIOR RECORDS — marked, not rewritten

### 6.1 `m5-roadmap-dependency-audit.md` (`a3e1740`) — already corrected
Three errors recorded in `capability-architecture-decision.md` §12 (money-is-M5;
seam-as-new-work; the §7.3 misattribution). **Unchanged and still correct.**

### 6.2 `capability-architecture-decision.md` (`1d2d56e`) — TWO NEW ERRORS

**(a) §4.1's appeal to the exemplar is falsified.** I claimed the shipped
predicate demonstrates *"the predicate conjoins a structurally-falsifiable-forever
term."* **It does not** — one conjunct is constant-true, the other is universal
delay by explicit tuning. **The fence stands as a proposed rule; its evidentiary
basis does not.** The exemplar is a counter-example, not a model.

**(b) The latch misdescription, inherited from the roadmap audit.** I wrote that
government-transforms-rather-than-deletes works *"because a latch records that a
predicate **has** fired."* **The shipped latch records CURRENT satisfaction under
hysteresis, not history**: *"Inactive + emerge true → Active = 1; **active +
recede true → Active = 0**… **Recede absent = never recedes**"*
(`ClassMobilitySystem.cs:28-33`). Monotonic acquisition exists **only in the
special case of an omitted `recede` clause** — a data choice, not a property of
the mechanism. Any design relying on capability outliving its preconditions must
say so explicitly and justify it.

---

## §7 DEPENDENCY GRAPH — CORRECTED BY THE AUDIT

The packet's proposed chain was Polity → Governance/Institutions → Knowledge →
Capabilities → manifestations. **The audit supports the top of it and refutes the
middle.**

```
POLITY  (blocking; forced by Law 1's two-endpoint Transfer — §3.1)
   │
   ├──► GOVERNANCE / IN-KIND TAXATION  (M5; needs no money — Housing precedent)
   │         │
   │         └──► INSTITUTIONS  (in-kind upkeep; six meanings unresolved)
   │                   │
   │        ┌──────────┴──────────┐
   │        ▼                     ▼
   │   KNOWLEDGE  ◄════════►  ECONOMY        ← A CYCLE, NOT AN ORDER (#3)
   │        │                     │
   │        └────────┬────────────┘
   │                 ▼
   │        CAPABILITY PREDICATES  (seam SHIPS; scope is settlement-only)
   │                 │
   └─────────────────┼──────────────────────────────┐
                     ▼                              ▼
              CIVILIAN MANIFESTATIONS        MILITARY MANIFESTATIONS
                                             ⚠ currently ERA-GATED in frozen
                                               D-011, not capability-gated (#2)
```

**Two edges the packet's graph got wrong:** knowledge→economy is **bidirectional**,
and military does **not** currently descend from capability at all — it descends
from an **era label**.

---

## §8 RECOMMENDED CRs — minimal, documentation-only

- **CR-007 — B3's exemplar does not satisfy B3.** The blocking one. Options:
  narrow B3's test (delay may be legitimate emergence); or re-tune the exemplar
  so exclusion is structural; or accept the exemplar as non-normative and supply
  a conforming one. **This gates every capability predicate.**
- **CR-008 — money has no owner**, and `m4-spec`'s "money is M5" is transcription
  drift against the ruling it cites.
- **CR-009 — era gates in ratified material vs Law 4 / B3.** Now two documents
  (D-011, D-009/D-010), so it is a pattern. D-040 flagged it and declined to
  rule; it should be ruled once, generally.
- **CR-010 — "institution" means six things.** Canonical definition required
  before any institution packet.

*(Numbers proposed; CR-004 withdrawn, CR-005/CR-006 open.)*

---

## §9 DESIGN PRINCIPLES FOR THE FUTURE RESEARCH SYSTEM

Each carries its evidentiary status honestly.

1. **No calendar unlocks** — Law 4. *Ratified, and currently violated by D-011
   and D-009/D-010 (CR-009).*
2. **No fixed linear tree** — B3. *Ratified.*
3. **No monotone accumulator disguised as a tree** — if it only rises,
   `k > K` is `year > N`. *Proposed; §1 shows the shipped exemplar already fails
   this, so it is a new constraint, not an existing one.*
4. **Reversible knowledge where appropriate** — D-041's stock is *"slow to build
   and slow to lose"*; the latch's `recede` is the shipped mechanism. *Ratified
   pattern.*
5. **Structural prerequisites, expressed as predicate conjuncts over published
   variables — never as edges to another capability's latch.** Referencing
   another capability's latch is how a tree grows back. *Proposed.*
6. **Multiple simultaneous efforts** — admissible only without completing
   "projects" (#8). *Conditional.*
7. **Rapid breakthroughs** — bounded by dt; meaningful in turns, not sim-years
   (#9). *Constraint, not aspiration.*
8. **Foreign contribution** — rides existing trade contact; needs polity scope.
9. **Institutions as knowledge multipliers** — must be a two-sided mechanism
   (D-035 shape), never a free-floating modifier (Law 2).
10. **Government-dependent capabilities** — requires monotonic acquisition, which
    the shipped latch does **not** give (§6.2b). *Open.*
11. **Technology affects war and civilian economy alike** — blocked for war by
    CR-009.
12. **Uneven emergence between civilizations** — the property the exemplar fails
    to deliver (§1). *This is the whole point of CR-007.*

---

## §10 NEXT PACKET — smallest thing before any implementation

**CR-007 alone.** Not the research system, not the seam, not polities.

If B3's own exemplar does not satisfy B3, then every capability predicate written
today inherits a defect the architecture already forbids, and **CR-008/009/010
are all downstream of knowing what a legitimate predicate actually is.** One
ruling, documentation-only, unblocks the rest.

**Recommended order after it:** CR-009 (era gates — same subject, ratified
material) → CR-008 (money owner) → polity substrate scoping → CR-010
(institutions) → M7 knowledge design.

---

## §11 WHAT THIS PACKET DID NOT DO

No research system. No science resource. No polity entity. No tech tree. No
milestone rewritten. No frozen document edited. No production code, schema, JSON,
golden, corridor or quarantine touched. Nothing certified, merged or pushed.
**No independent human reviewer participated.**
