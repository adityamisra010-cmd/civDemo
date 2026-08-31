# CR-007 — D-040 B3 AND ITS EMERGENCE EXEMPLAR

**Status: RESOLVED WITHOUT A NEW RULING — the alleged contradiction does not
exist as stated, and D-040 already disposes of the residue itself.**
**Documentation-only. No production code, test, schema, JSON, golden, milestone
or frozen document touched.**

> **THIS CR CORRECTS TWO OF MY OWN PRIOR RECORDS.** `milestone-architecture-governance.md`
> (`487a66d`) reported *"D-040 B3's own sanctioned exemplar fails B3's own test"*
> as a **BLOCKING** contradiction and made it the headline finding. **That
> finding is withdrawn — it rested on a misreading, identified in §4.1.** The
> underlying measurements were correct; the inference from them was not.

---

## §1 PROBLEM STATEMENT

The governance audit reported that D-040 B3 rejects date-like technology unlocks
and requires structurally falsifiable emergence, while B3's own cited exemplar —
`"emerge": "food_surplus_ratio > 1.3 && population > 520"` — behaves as a schedule
with jitter rather than a structurally contingent predicate, because no settlement
is ever permanently excluded. If true, every future capability predicate modelled
on it inherits the defect.

**The question this CR answers is narrow: is B3 internally contradictory?**

---

## §2 EVIDENCE

### 2.1 Source evidence (documents)

- **B3's prohibition** (`d040:59-64`): *"a tech-tree node opening sea travel is a
  calendar gate wearing a tree."*
- **B3's computed version** (`d040:66-70`): *"**sea travel** becomes possible when
  the conditions for boats exist — **a coastal settlement, timber, and craft
  capacity** — **in the same shape as class emergence**, where artisans emerge on
  food surplus AND market extent rather than on a date. **A landlocked polity
  never develops it**; a coastal one does; nobody schedules either."*
- **B3's own parenthetical** (`d040:72-74`): *"That emergence-shape citation
  belongs to the shipped predicate and its `_doc`… **it is a finding against the
  reference, not against this ruling.**"*
- **Part F's governing doctrine** (`d040:181-183`): *"Six disagreements between
  the dictated text and the ratified tree. **None changes a ruling; all change
  what may be cited in support of one.**"*
- **F2, second-order** (`d040:198-203`): *"'market extent' is implemented as **RAW
  POPULATION**, which is a proxy, not an extent — the very thing B1 says a
  computed model should not be. **Noted, not ruled.**"*
- **D-040's self-description** (`d040:3-4`): *"**Designs nothing.** No mechanism,
  no storage, no constant."*

### 2.2 Implementation evidence (code and data)

- `sim.json:165` — `"emerge": "food_surplus_ratio > 1.3 && population > 520"`.
- `sim.json:167` — *"TUNE: 520 sits above the ~350-500 jittered founding sizes, so
  **every settlement must GROW into its artisans**."*
- `Variables.cs:29-32` — *"a labor-limited food_surplus_ratio is adultShare ×
  output / consumption, **identical at every size (measured 3.5 ± 0.1 across all
  twelve)**."*
- `ClassMobilitySystem.cs:28-33` — the latch is **current satisfaction under
  hysteresis**: *"active + recede true → Active = 0… Recede absent = never
  recedes."*
- Coastal geography is real and **static** in worldgen (ADR-008 static terrain;
  coastal deposits and coastal-vs-inland siting in `WorldFounding.cs`,
  `SettlementSiting.cs`, `WorldgenConfig.cs`). **An inland settlement is
  permanently non-coastal.**

### 2.3 Experimental evidence

**None was generated for this CR, and none was needed.** The question is one of
document interpretation plus already-recorded measurement. No sweep was run.

---

## §3 WHAT B3 ACTUALLY REQUIRES

**B3 prohibits one thing: a capability opened by a tree node, a date, or an era
label.** That is the whole prohibition, and Law 4 is its ground.

**B3 requires that capability be a predicate over computed world state.** The
sanctioned form is a **conjunction of computed preconditions**.

**B3 does NOT require that every capability predicate exhibit permanent structural
exclusion.** The sentence *"A landlocked polity never develops it"* is offered as
**a property of the sea-travel example**, demonstrating that a computed predicate
*can* exclude permanently where a date cannot — it is **illustrative of what the
form makes possible**, not a conformance test every predicate must pass. It is
prose in support of a ruling, in a document that says of itself *"Designs
nothing."*

---

## §4 WHAT THE EXEMPLAR ACTUALLY DEMONSTRATES

### 4.1 THE MISREADING, STATED PLAINLY

**"It" in *"A landlocked polity never develops it"* refers to SEA TRAVEL, not to
the artisan class.** The subject of the sentence is the boats predicate — *coastal
settlement AND timber AND craft capacity* — whose **first conjunct is permanently
false for a landlocked polity**, and coastal geography is static terrain.

**That predicate does exhibit structural exclusion. B3's claim is true of the
thing B3 makes it about.**

The class-emergence predicate is invoked for its **SHAPE** — *"in the same shape
as class emergence"* — meaning *a conjunction of computed conditions rather than a
date*. It is not offered as a demonstration of permanent exclusion, and B3 never
claims it is.

**My prior record conflated the two.** The measurements it cited were correct;
the inference — that B3's test is failed by B3's own exemplar — was not.

### 4.2 What the exemplar proves

- **Proves:** capability can be expressed as a conjunction of computed variables
  rather than a date, and can be evaluated deterministically with hysteresis.
- **Does NOT prove:** that a predicate can permanently exclude. Both conjuncts
  fail to do so — one is constant-true, the other universal delay by explicit
  tuning.

### 4.3 The residue, which is real but is NOT a contradiction in B3

**The shipped artisan predicate is a poor template to copy.** A future author who
imitates it inherits a constant-true conjunct and a growth-delay threshold, and
gets a schedule.

**D-040 already recorded this and already declined to rule on it** — F2's
second-order finding, *"Noted, not ruled"* — and Part F states the governing
doctrine that such findings *"change what may be cited in support of"* a ruling
**without changing the ruling**. **The document already applied Option C to
itself.**

---

## §5 DECISION OPTIONS

### OPTION A — NARROW THE TEST (permit universal eventual emergence with varied timing)

- **Compatibility with the landlocked sentence:** would weaken it, but the
  sentence is about sea travel and needs no weakening (§4.1).
- **Compatibility with no-calendar-unlock:** **poor.** If universal eventual
  emergence is explicitly blessed, `k > K` on a monotone quantity becomes
  sanctioned, and that is `year > N` with a per-civ rate constant.
- **Consequence:** materially weakens the anti-tech-tree rule.
- **Verdict: REJECT.** It concedes ground B3 never lost.

### OPTION B — RETUNE THE EXEMPLAR

A conforming exemplar would need `X AND Y` where at least one conjunct is
permanently false for a valid class of polity under the same rules.

- **The architecture supports one already**: B3's own sea-travel predicate
  (coastal + timber + craft) — coastal is static terrain, hence permanently false
  inland.
- **But retuning the *class* predicate is out of scope**: it would change
  `sim.json`, move every golden, and disturb a calibration corridor — for a
  documentation problem. `population > 520` is also a **TUNE** value, and tuning
  is *"play, not amendment"* (S8 §3).
- **Verdict: REJECT as a CR action.** The defect is already owned: F2 assigns it
  *"beside T4.14's emergence work."*

### OPTION C — DECLARE THE EXEMPLAR NON-NORMATIVE

- **Can B3 stand without a conforming executable exemplar?** **Yes** — it already
  does. B3's normative content is the prohibition plus the required *form*; the
  sea-travel predicate supplies the conforming illustration.
- **Do implementers still get a reliable test?** Only if the *form* is stated
  crisply, which is §8's job.
- **Does it merely postpone the contradiction?** **No — because there is no
  contradiction to postpone.** It postpones nothing except the separate,
  already-owned question of whether the shipped artisan predicate is well tuned.
- **Verdict: ALREADY THE DOCUMENT'S OWN POSITION** (F2 + Part F doctrine).

---

## §6 RECOMMENDED RULING

> **SUPERSEDED IN PART BY D-042 (director ruling, later).** This CR's §5/§6 treat
> *"is an accumulating knowledge quantity permitted?"* as still open. **D-042 §9.4
> and §9.5 now rule it: knowledge is an allocatable flow, and unallocated
> knowledge ACCUMULATES AS A RESERVE.** So the accumulation question is
> **answered YES** and must not be re-asked. D-042 §9.7 and §12 supply the guard
> that keeps the answer compatible with B3 — no rigid technology tree, no
> one-at-a-time queue, no calendar-date unlocks. **This CR's conclusion is
> unaffected** — B3 was never contradictory, and §8's design constraints (in
> particular §8.4: a predicate whose conjuncts are all constant-true or
> monotone-in-time is a *schedule*) remain the operative guidance for keeping an
> accumulating quantity from becoming a disguised tree.

**OPTION C, recorded rather than enacted: no new ruling is required.**

D-040 B3 is **not internally contradictory**. The alleged contradiction was a
misreading of the referent of *"it"* (§4.1), compounded by treating a
shape-citation as a conformance exemplar. D-040 had already quarantined the
reference's defects at F2 under a stated doctrine that findings against a
reference do not disturb the ruling.

**The evidence is sufficient to decide this; no further measurement is needed.**

**What the director should actually rule on is narrower and is NOT a B3 question:**
whether the shipped artisan predicate should be retuned so the project has at
least one *executable* predicate demonstrating permanent exclusion. **That is
T4.14's, per F2 — not this CR's.**

---

## §7 DOWNSTREAM CONSEQUENCES

Since B3 stands unamended, nothing downstream changes **because of B3**. What
changes is the removal of a blocker I had wrongly asserted.

| system | consequence |
|---|---|
| knowledge / science | **unblocked.** §8's constraints apply; no B3 amendment needed |
| research | a *completing project that grants a capability* remains prohibited — unchanged |
| capabilities | the shipped D-020 seam remains the mechanism; scope widening remains the work |
| institutions | unaffected by this CR; six-meanings problem stands (CR-010) |
| government-dependent capability | unaffected here; still blocked by the latch semantics (§10.2) |
| military technology | **unaffected by this CR** — its problem is era gates in D-011, not B3 |
| economic technology | unaffected; money ownership stands (CR-008) |
| money | unaffected |
| diplomacy | unaffected |
| tech-tree / domain lattice | *"no tree; domain lattice lite"* stands unamended |

---

## §8 CONSTRAINTS FOR FUTURE CAPABILITY DESIGN

Following from §3 and §4 — these are what a legitimate predicate must satisfy:

1. **A capability predicate is a conjunction of computed world-state variables.**
   Never a date, era label, or tree node. *(B3, Law 4 — ratified.)*
2. **A predicate may reference published variables only.** Referencing another
   capability's latch reconstructs tree edges. *(Proposed.)*
3. **Permanent exclusion is PERMITTED and VALUABLE but is NOT REQUIRED.** B3
   demonstrates it via static geography; it does not demand it. *(This CR.)*
4. **A predicate whose conjuncts are all constant-true or monotone-in-time is a
   schedule.** It is not prohibited by B3, but it delivers no differentiation and
   should be recognised as a schedule rather than mistaken for emergence.
   *(Proposed — this is the real lesson of the artisan predicate.)*
5. **At least one conjunct should be scale-sensitive or structural** if the
   capability is meant to differentiate civilizations. `Variables.cs:29-32` states
   the shipped form of this lesson: *"ANY emergence predicate that needs scale
   sensitivity MUST publish an absolute quantity, not another ratio."*
6. **Reversibility is a data choice, not a mechanism property** — `recede` absent
   means never recedes (§10.2).

---

## §9 FOLLOW-UP CRs — STATUS REVISED

| CR | status after this ruling |
|---|---|
| **CR-008** money has no owner | **STILL REQUIRED, and now the top blocker.** Independent of B3 |
| **CR-009** era gates (D-009/D-010, D-011) vs Law 4 | **STILL REQUIRED but NARROWER than I claimed.** D-040 already reports the D-009/D-010 instance *"REPORTED NOT RULED"* and assigns it to **the transport packet**. The **D-011 military instance is not yet owned** — that is the genuinely open part |
| **CR-010** institution definition (six meanings) | **STILL REQUIRED.** Untouched by this CR |

**No new CR is required by this one.**

---

## §10 HISTORICAL CORRECTIONS — marked, not rewritten

### 10.1 `milestone-architecture-governance.md` (`487a66d`) — HEADLINE WITHDRAWN

The document's §1 and §5 record *"B3's operative test vs B3's own sanctioned
exemplar"* as **BLOCKING contradiction #1** and as the executive finding.
**Withdrawn.** *"It"* refers to sea travel; the class predicate is cited for shape;
D-040 F2 already quarantined the reference's defects. The measurements in §1 are
correct and stand; **the inference does not.**

Consequently its §3 adversarial verdicts **#1, #6 and #10** — all of which turned
on the exemplar failing B3's test — **lose their principal supporting argument.**
They may still hold on other grounds; **they are no longer supported by this one.**

### 10.2 What SURVIVES from `487a66d`, unchanged

- **#5 polity state cannot be postponed past M5** — the Law 1 two-endpoint
  `Transfer` argument is untouched by this CR and remains the strongest result in
  that pass.
- **#2 advanced military is era-gated in frozen D-011** (`:13`, `:45`, `:66`) —
  untouched; still a genuine Law 4 tension, and the D-011 instance is unowned.
- **#3 knowledge and economy are a cycle** — untouched.
- **Institutions carry six meanings with zero code** — untouched.
- **The latch misdescription correction** (`capability-architecture-decision.md`
  §6.2b) — untouched and still correct.

### 10.3 `capability-architecture-decision.md` (`1d2d56e`)

Its correction notice cites §4.1's evidentiary basis as falsified. **That notice
is now itself partly overtaken**: §4.1's *fence* was never required by B3 in the
first place, so it was never load-bearing on B3. **The fence survives as a
proposed design constraint (now §8.4), not as a B3 requirement.**

---

## §11 WHAT THIS CR DID NOT DO

No knowledge, research, capability, polity or institution system designed or
implemented. No milestone resequenced. No frozen document amended. No exemplar
retuned. No `sim.json` change. No experiment run. **No independent human reviewer
participated.**
