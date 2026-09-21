# ARCH-P / ARCH-Q — CONFLICTS REQUIRING A RULING, AND QUESTIONS REQUIRING A DECISION

**DESIGN-PHASE SYNTHESIS. Nothing here is resolved, and nothing here proposes an answer.** This
document consolidates and de-duplicates every conflict and every open question surfaced by the five
decision-recovery lanes, the M4 closure audit, the density-breach verification lane, and the six
architecture lanes — and orders both by **blast radius** so the Director can triage. Created under
the Director's 2026-09-19 mandate as OUTPUTS P and Q of the synthesis lane.

**Tree pinned for every citation:** branch `claude/civdemo-work-b1z2y4` @ `26d12d3`, working tree
clean; `origin/main` = `dbef61a`.

**Author authority: none.** The DIRECTOR IS ChatGPT. Every row names both sources and takes no
side. **Part Q contains questions only** — where this lane has a design position it is in
`arch-M`, `arch-N` or `arch-O` under a PROPOSED label, never here.

**Label key:** RATIFIED · MEASURED · PROPOSED · INFERRED · DIRECTOR DECISION REQUIRED.

---

## §0 HOW THIS DOCUMENT IS ORDERED, AND THE ONE STRUCTURAL FACT THAT SETS THE ORDER

**Blast-radius scale**, PROPOSED and applied uniformly:

| BR | meaning |
|---|---|
| **BR-5** | Blocks a **milestone** from closing or starting. |
| **BR-4** | Blocks an entire architecture cluster or several packets at once. |
| **BR-3** | Blocks one packet or one mechanism. |
| **BR-2** | Blocks a design choice inside a packet; the packet can start. |
| **BR-1** | Record-level. Blocks nothing, but will mislead a reader who meets it first. |

**THE STRUCTURAL FACT, and it reorders everything below. INFERRED, from a RATIFIED source.**
S8 §4's documentation cadence is strictly sequential: *"`implement M(n) → exit criteria GREEN →
write + ratify M(n+1) spec → cut packets → implement M(n+1)`"*, and *"Spec-writing for n+1 begins
only after M(n)'s exit gate passes"* (`docs/spine-s8-governance-freeze.md:44`, `:47`;
`CLAUDE.md:28`). **Therefore everything that blocks M4's CLOSURE also blocks M5's START**, even
though none of it is about M5. Four items in Part P are in that class — **P-01, P-02, P-03,
P-04** — and they are listed first for that reason, ahead of items that are substantively about
M5 itself.

**"BLOCKS M5 START"** in the tables below means one of two things, and the row says which:
**(closure)** — it blocks M4 from closing, and M5 cannot begin until M4 closes; or **(scope)** —
it determines what the M5 spec may contain, so the spec cannot be written without it.

**De-duplication method.** Every conflict row in every `docs/design/` document, plus the M4 closure
audit's R1–R15, was collected; rows naming the same pair of sources were merged into one entry;
the entry carries a **"found by"** line listing every lane that surfaced it, so no lane's work is
lost. **Sixty-three conflict entries survive from roughly ninety raw rows.** Merges are stated,
never silent.

**Correction to this pass (label- and citation-discipline review).** Four recovery-lane conflicts had
been dropped **without the merge note the method above requires**. Three are now entries in their own
right, because each names a **source pair that no existing entry names** — **P-61**
(recovery-knowledge **C-11**), **P-62** (**C-05**) and **P-63** (**C-08**). They carry the next free
ids and sit in the section their blast radius puts them in, so **no existing id moved**. The fourth,
recovery-knowledge **C-04**, is recorded as an **explicit note on P-11**, whose subject it shares and
whose source pair it does not. MEASURED: the knowledge lane's **C-01 … C-11** are now all accounted
for in this document.

---

# PART P — CONFLICTS REQUIRING A DIRECTOR RULING

## P.1 — BR-5, BLOCKS M5 START (closure): the four that keep M4 open

### **P-01 — CR-016: the disaster rate, and whether CR-015's arming directive stands amended**
**BLOCKS M5 START (closure). BR-5.**

- **Source A, RATIFIED:** CR-015's status is **RULED** (`docs/adr/cr-015-famine-is-exceptional.md:3`),
  and it directs the arming: *"T4.21-4 arms λ = 0.01 and is the last golden move"* (`:338`), with a
  coupling map that expects golden behaviour change there (`:327`). Its famine semantics name a
  famine-class natural disaster as one of exactly two CAUSES (`:373-376`) — at λ = 0 that cause
  never fires outside a rig.
- **Source B, MEASURED:** CR-016 is **OPEN — ESCALATED TO THE DIRECTOR**
  (`docs/adr/cr-016-armed-disaster-fallout.md:3`). MEASURED at λ = 0.01: CR-001's *permanent*
  dt-continuity detonator breaks by **2.2572** (battery rig) and **2.5259** (DemographyRetune rig)
  per 1000 yr against a **0.1** bar; `canonical.fedGrowthPerYear` falls below its immovable band;
  the dev world goes **93,910 → 38** over 1000 turns.
- **Source C, MEASURED (this lane, `26d12d3`):** the tree ships `hazardPerYear` = **0.0**
  (`Sim.Data/content/sim.json:235`), and **the two append-only amendment blocks drafted for
  CR-015 and CR-016 have NOT landed.** `grep "APPEND-ONLY\|2026-09-19"` over
  `docs/adr/cr-015-famine-is-exceptional.md` returns only the T4.21-4 and T4.21-7 blocks; the
  string `2026-09-19` appears nowhere in `docs/adr/cr-016-armed-disaster-fallout.md`. **The text
  headed "DIRECTOR RULING (2026-09-19)" exists only inside `docs/design/m4-closure-audit.md` as
  PROPOSED text — §4.5's heading is literally *"PROPOSED amendment text — NOT WRITTEN INTO CR-015
  OR CR-016"* (`:463`), with the two draft blocks at `:474` and `:523`.**

**What each side implies.** **Reading A** (the closure audit §4.4 sets out both with citations):
CR-015 is *"an already-ratified requirement saying otherwise"*, so arming should proceed unless
CR-015 is amended — and withholding is a **change to a ruled decision**, which owes the amendment
**before** the code. **Reading B:** CR-015 ratified the mechanism and the derivation's frame, not
the shipping of a value — λ's own governing row reads **CHOSEN, not DERIVED**, none of the nine
amendment lines N1–N9 fixes it, and *"Tuning data files and `TUNE` parameters is always allowed"*.

**What is blocked.** M4's closing record; the disposition of three tests currently reading as
quarantines (`PopulationTests.Reconciliation_FromLedgerAlone`'s `starved > 0` guard,
`ChronicleTests.Annals_TwinIdentical`'s famine line, `MigrationTests.MagnitudeCorridor_
FedPhaseDrift_WithTeeth`'s upward rate-lever tooth); and, downstream, the **whole of the disaster/
resilience architecture**, because arch-JKL Part K sits on top of it (**DQ-K3**).
**Neither reading selects option 1, 2 or 3. The rate remains UNSET under either.**

*Found by:* m4-closure-audit §4 (R2) · recovery-climate **C-03** · recovery-food **C-03/C-04** ·
recovery-architecture **C-10** · arch-JKL **X-04**.

---

### **P-02 — G1: the harvest-weather decade variance, and the FOURTH PATH that is not on its list**
**BLOCKS M5 START (closure). BR-5.**

- **Source A, RATIFIED (frozen):** the T3.4b/T3.4c invariant — the weather state holds stationary
  variance σ² at **every** dt, *"which would be the era table altering the climate"*
  (`Sim.Core/Systems/Harvest/HarvestWeatherSystem.cs:42-45`; `docs/m3-spec.md:52-53`) — plus the
  mandate's ruling in force that *"weather is not to be modified"* / *"stochastic and unclamped"*
  (`cr-015:551`, `:581`).
- **Source B, RATIFIED (law 3):** applied to the published turn value, a turn's harvest is the
  turn-MEAN of the yearly multipliers, whose variance is `σ²·g(dt/τ)` = **0.427·σ²** at dt 10 — so
  the shipped decade multiplier is **1.5× too variable in σ** (`cr-015:524-547`).
- **CR-015 states the collision itself:** *"**Two frozen items and a law disagree.** S8 §3 records
  such a conflict and escalates it; it does not let a packet resolve it by choosing."* (`:549-555`).
  Status: **ESCALATED, awaiting its own ruling**, on four independent statements (`cr-015:3-8`;
  `:568` *"Until ruled: (a)"*; `docs/milestones.md:318-319`; `docs/t4.21-director-report.md:1156`).
- **Source C, the FOURTH PATH — DIRECTOR-STATED, relayed, UNVERIFIED as a repository document:** a
  **deterministic, bounded climate signal**; the chain *climate state → rainfall → water → soil →
  agriculture → food*; **ENSO-like multi-year structure**; and extreme weather as **climate and
  hazard STATES** rather than unbounded noise. **MEASURED (closure audit, by reading `cr-015:558-570`
  in full): none of the three options on record describes it.** All three operate on the same
  stochastic, unbounded, single-scale process; the fourth path replaces the process.

**What each side implies.** **(a) LEAVE** keeps ordinary SEVERE decades as lethal as spec §9 R1
states. **(b) APPLY `g`** moves every golden and needs its own packet with its own attribution
step. **(c)** is REFUSED (re-deriving a correct yearly σ is tuning). **The fourth path** would
answer the turn-mean variance question *by construction* rather than by a correction factor, would
**couple G1 and CR-016 harder** (*"extreme weather as climate/hazard STATES is the same
architectural move as a hazard catalogue"*), and **is an amendment to ratified weather semantics,
not an option selection inside §6.4**.

**What is blocked.** M4's closing record; the entire **arch-I climate substrate** (its §3 is
*"presently forbidden by a ruling in force"* — **X1**); and, through the same weather constants,
the reference class of `sigmaLogYield` = 0.2936 and `YieldPerArableKm2PerYear` = 26.0.
**Evidence-class caveat, stated because the report states it:** `g = 0.427`, *"1.5× too variable"*
and the 3.9 %/0.25 % pair are **algebra over shipped constants, not measurements** — *"no lane
sampled the shipped decade multiplier's variance"* (`docs/t4.21-director-report.md:1178-1183`).

*Found by:* m4-closure-audit §5 (R3) · recovery-climate **C-02** · recovery-food **Q-13** ·
arch-I **X1, X2, Q2, Q3, Q10**.

---

### **P-03 — B5: the density-quarantine window breach, and the instrument that cannot see it**
**BLOCKS M5 START (closure). BR-5.**

- **Source A, MEASURED and CONFIRMED by a dedicated verification lane on `9f6c6ae`:** canonical
  `densityPerArableKm2` at **seed 3** measures **0.35415668759623087** against the recorded
  quarantine-window floor **0.3685744951368359** — **3.9117757 % below**. **Attribution: T4.21
  CAUSED it**; on the pre-T4.21 tree `45046eb` the same instrument measures **20/20 inside the
  window**, reproducing both endpoints bit-for-bit. The mechanism is the population move, not the
  arable: seed 3's arable is bit-identical across arms, **133,750 → 128,518 people, −3.9117757009%**
  — the same number to every digit as the shortfall
  (`docs/design/density-window-verification.md` §0, §2, §3).
- **Source B, MEASURED by the same lane:** the instrument gap **is total**. The shipped nightly gate
  (`.github/workflows/ci.yml:232-233`) returns **exit 0 / PASS** on the candidate's own 20-seed
  metrics; an otherwise-identical control gate that consults `quarantine.window` returns **exit 1 /
  BREACH** on the same file. `CalibrationBatteryTests` is **7 passed / 0 failed** because its
  canonical theory runs **seeds 1 and 2 only** (`CalibrationBatteryTests.cs:426-428`), both
  comfortably inside the window.
- **Source C, RATIFIED:** *"**The shared quarantine helper asserts the precondition is still
  ABSENT**, so each family fails loudly the moment famine returns: **a tripwire, not a mute
  button**"* (`docs/adr/adr-013-…:272-274`); and *"CR-002 and CR-003 both forbid fitting the
  instrument to the artifact"* (`docs/milestones.md:357`).

**What each side implies.** Either the breach is a defect in the candidate — in which case M4 does
not close over it — or the window is not a lift condition in the sense the quarantine record
implies. **Under I-30 (`arch-O`) a corridor a shipped mechanism cannot satisfy is a finding about
the mechanism, never a reason to move the band**, so "re-pin the window" is the one disposition
that is ratified-out unless the Director rules otherwise. **The verification lane explicitly does
not decide** whether the breach is a defect, whether the window should be re-pinned, whether the
quarantine should lift, or whether M4 may close over it.

**What is blocked.** M4 closure, and **no `corridors.json` edit may precede the ruling**. It is
also **known-open item 11 firing exactly as predicted**: *"the instrument reports, but no mechanism
makes anyone read it."*

*Found by:* m4-closure-audit B5 / R4 / R13 · `density-window-verification.md` §0, §4, §7 ·
recovery-architecture **A-136** item 11.

---

### **P-04 — `docs/milestones.md`'s M4 entry is the closing artefact and is stale against this tree**
**BLOCKS M5 START (closure). BR-5.**

- **Source A, RATIFIED:** *"**M4 does not close on this entry.** It closes on the director's play
  session against the candidate and his merge ruling, as M3 did."* (`docs/milestones.md:369-370`),
  and the exit-criteria table records *"AWAITING THE DIRECTOR"* (`:340-351`).
- **Source B, MEASURED (m4-closure-audit B4):** the entry *"is stale in five places against this
  tree. It cannot be the record of a closed milestone as written"* (findings F1, F2, F3, F5, F8).
  Also MEASURED there: the **`m4-exit` tag does not exist** (`git tag` returns `m3-exit` only).
- **Related and unresolved, RATIFIED:** *"a FAILED criterion means the system did the
  wrong thing; a WITHDRAWN one means the criterion asked for evidence the world cannot produce"*
  (`docs/milestones.md:229-237`) — the disposition vocabulary a closing edit would have to use.

**What each side implies.** The closing edit is a living-document edit **on a Director ruling**; no
agent is permitted to make it unilaterally. Until it is made, the milestone record and the tree
disagree, and **GOV-4 §10 warns that a status board is a router, not evidence** —
`docs/current-state.md` is separately recorded as stale in four load-bearing claims.

**What is blocked.** M4 closure, the `m4-exit` tag, and the merge ruling for the five
post-certification packets (audit B2, R1, R15).

*Found by:* m4-closure-audit B3/B4, R1, R15 · recovery-architecture **A-135**.

---

## P.2 — BR-5, BLOCKS M5 START (scope): what M5 *is*, and what it may contain

### **P-05 — CR-005: the Director's M5 "Research, Technology & Institutions" direction versus the frozen milestone order**
**BLOCKS M5 START (scope). BR-5. Status: OPEN — awaiting director ruling.**

- **Source A:** the direction, recorded in CR-005 §1: *"M5 own a major 'Research, Technology &
  Institutions' architecture packet"*, on an ordering-dependency argument — *"technology must be
  designed before the downstream systems that depend on technological capabilities, so that a later
  warfare or economic packet cannot independently invent technology prerequisites that belong to
  M5."*
- **Source B, RATIFIED and FROZEN:** M5 is *"The governing loop. Taxation, budget,
  authority/bandwidth economy, laws-lite, legitimacy. **'It's a game now' checkpoint — evaluate fun
  honestly here before proceeding.**"* (`docs/civ-sim-architecture-v3-outline.md:109`); knowledge is
  M6, institutions M7 (`:110-111`). **And M4 has already shipped five deferrals pointing at an M5
  defined as money and fiscal administration** (`cr-005:36-42`).

**What each side implies.** CR-005 itself states the crux and refuses to answer it: *"**If the
director's intent is that M5 must be substantively ABOUT learning and change … then Option B is the
honest reading and C is a half-measure.** That is the question this CR needs answered, and it is a
director call, not an agent's."* (`:149-152`). Three options are on record — **A RENUMBER**,
**B INSERT**, **C an architecture-only packet inside M5** (recommendation: C).

**What is blocked.** **The content of the M5 spec**, and therefore whether any of the six
architecture lanes' material may be written into a spec at all under S8 §4's n+1 rule. It also
determines whether **P-06** (which milestone owns knowledge) is urgent or deferrable.

*Found by:* `docs/adr/cr-005-…:3` · recovery-architecture **C-08** · recovery-civics **C-04** ·
arch-N **§2.2, X-N1**.

---

### **P-06 — Which milestone owns KNOWLEDGE**
**BLOCKS M5 START (scope) only if CR-005 is ruled A or B; otherwise BR-4.**

- **Source A, RATIFIED and FROZEN:** *"Knowledge & diffusion | **M6**"* and *"**M6** — Knowledge &
  divergence"* — `docs/civ-sim-architecture-v3-outline.md:84`, `:110`. Inside the S8 §1 freeze
  perimeter.
- **Source B, RATIFIED and equally FROZEN:** *"| **M7** | knowledge & divergence | **was M6** |"* —
  `docs/d011-battle-layer-addendum.md:62`. D-011 is named in the same perimeter (`:16`).
- **Source C, MEASURED (a recorded finding):** GOV-2 §1c calls the Spine row **"stale by one"**,
  verifies it row by row, and states the reconciliation **"remains REQUIRED"** —
  `docs/m4-pre-spec-dependencies.md:143-160`. **MEASURED (this lane): the reconciliation has not
  been performed; the Spine table is unedited at `26d12d3`.**
- **Source D, SECONDARY EVIDENCE:** `docs/capability-architecture-decision.md:378` lists it as
  MINOR conflict #9, *"known-stale"* — and that document carries a correction notice falsifying two
  of its own claims.

**What each side implies.** Under **M6** the battle layer follows knowledge, which contradicts
D-011 §6 — the very document that *is* the resequence. Under **M7** knowledge follows the battle
layer, which is the reading every document written after 2026-08-08 uses, D-040 included. **Either
way**, `docs/milestones.md:326-328` has already used GOV-4 §1 rank 2 to dissolve the ladder
conflict **for exactly one line** and left the general question open (recovery **C-04 / G-07**).

**What is blocked.** Whether a knowledge spec is n+1 (writable after M5's exit gate) or n+3 (not
writable at all) — and therefore whether **arch-C, arch-D and arch-E** are material for the next
spec or for a spec three milestones away. **All three lanes record this same blocker and none
resolves it.**

*Found by:* recovery-knowledge **C-02** · recovery-architecture **C-04 / G-07** · arch-C **X-07 /
Q-C18** · arch-E **X-E7** · arch-N **§3**.

---

### **P-07 — What an INSTITUTION is: ≥6 meanings, zero code, and CR-010 was never written**
**BLOCKS M5 START (scope). BR-5.**

- **Source A, RATIFIED, and it binds M5 by name:** D-035-C path 6 — *"one institution raises one
  need and lowers another … **M5 must build Safety-vs-Liberty this way and no other way**"*, with
  *"a policy, an institution"* as the carrier (`docs/d035-needs-aggregation.md:86`). D-035-C path 7
  names *"an institution"* as the Endurance valve's carrier (`:87`). Law 8 names institutions as
  validated data content (`docs/civ-sim-architecture-v3-outline.md:26`).
- **Source B, MEASURED:** *"`institution` appears nowhere in `Sim.Core/`, `Sim.Data/`, `Sim.Cli/`
  or `Sim.Tests/`. **There is no mechanical institution.**"*
  (`docs/milestone-architecture-governance.md:181-198`, SECONDARY). **Corrected this pass by
  arch-FGH X-6:** it appears exactly once, in a doc comment at `Sim.Core/State/WorldState.cs:696`
  quoting D-042 §3.2. **The substantive claim stands; the phrasing has drifted.**
- **The meanings.** Four (`docs/capability-architecture-decision.md:230-234`); six
  (`milestone-architecture-governance.md:179-198`), of which two are *"load-bearing and mutually
  incompatible in kind"* — (5) and (6) make an institution a **published scalar**, (1) a
  **composable module**, (3) a **built structure**; **at least eight** once arch-FGH §3.0 adds
  *organizational actor*, splits *holder* from *converter*, and restores *need trade-off*.

**What each side implies.** A published scalar, a module and a building have different row shapes,
different owners, different milestones and different serialization costs. **Both records reach the
same consequence independently:** *"This must be ruled before anyone writes an institution
packet."*

**What is blocked.** M5's D-035-C path-6 obligation; the whole of arch-FGH Part 14; **and four
separate sibling lanes' carrier debts reduce to this one undefined noun** — arch-C **OWED-3**,
arch-D **§4.1/4.3/4.6** (*"the carrier problem in this design is not six problems. It is largely
one undefined noun, appearing three times"*), arch-E **OWED-E3**, arch-FGH **OWED-5**, arch-JKL
**OWED-K3**. **CR-010 was recommended and has never existed on any branch**
(`docs/current-state.md:13`).

*Found by:* recovery-knowledge **C-09** · recovery-civics **C-02 / Q-01** · recovery-architecture
**C-07** · arch-C **X-09 / Q-C12** · arch-D **Q7** · arch-E **OWED-E3** · arch-FGH **X-2 / Q-I1** ·
arch-N **X-N3**.

---

### **P-08 — M5 taxation under law 1 requires a two-endpoint destination that does not exist**
**BLOCKS M5 START (scope). BR-5.**

- **Source A, RATIFIED and FROZEN:** `Ledger.Transfer` is **two-endpoint**; `Ledger.Flow` is a
  world source/sink; *"Total = initial + sources − sinks holds **exactly** at every turn"*
  (`docs/m0-kernel-spec.md:74-81`). And M5 taxes **in kind** by ruling (**P-10** Source A).
- **Source B, SECONDARY EVIDENCE, logged BLOCKING:** *"an in-kind tax has three possible
  destinations, and two are absurd: a **Flow sink destroys the grain** — confiscation-as-annihilation
  — and **a budget you cannot spend is not a budget**. The remaining option is a `Transfer` into a
  **polity-scoped stock that does not exist**. **M5 taxation therefore requires a polity treasury
  endpoint. Polity state is an M4/M5 prerequisite, not an M7/M8 convenience.**"*
  (`docs/milestone-architecture-governance.md:123-137`).
- **MEASURED (this lane and two recovery lanes):** the finding's own premise *"No polity entity
  exists"* is now **FALSE** — `PolityRow` ships (`Sim.Core/State/WorldState.cs:704`). **But the row
  carries identity and command source only, and no stock**, so the two-endpoint problem stands
  unchanged. **Nobody has re-ruled it.**
- **Constraint on any answer, RATIFIED:** D-042 §4.3 forbids a single Empire-wide inventory —
  *"Local stocks stay physically local"* (`:72-74`) — while D-042 §14.2 explicitly sanctions an
  Empire treasury (*"§4.4 bans a treasury-VERSUS-population split, not a treasury"*, `:251-260`).

**What is blocked.** M5's central mechanic, and **every downstream line that spends**: the 17-item
rewrite inventory GOV-2 §1a enumerates — *"treasury health"*, *"buy off leaders (treasury)"*, the
Clergy/Soldiers/Bureaucrats **stipends**, *"funding opposition"*, *"coup finance"*, *"wage
differentials"* (`docs/m4-pre-spec-dependencies.md:43-72`).

*Found by:* recovery-architecture **C-06** · recovery-civics **C-03 / Q-02** · arch-FGH
**FGH-OWED-1** · arch-N **N2**.

---

### **P-09 — Empire-scope capability evaluation is RATIFIED; the substrate is settlement-only; and the obvious coordinator is REJECTED**
**BLOCKS M5 START (scope). BR-5.**

- **Source A, RATIFIED:** *"**Capability evaluation must be able to distinguish SCOPE — Empire-level
  and Settlement-level, with unit/action scope where required.**"* (`docs/d042-…:155-156`, §8.3);
  and *"Knowledge is ultimately **Empire-scoped**"* (`:165-166`, §9.2).
- **Source B, MEASURED:** `public record struct VariableRow(SettlementId Settlement, int VarId,
  double Value)` — `Sim.Core/State/WorldState.cs:394`. **There is no Empire-keyed, polity-keyed,
  bucket-keyed or any-other-keyed published-variable row type anywhere in `Sim.Core/State/`.**
- **Source C, RATIFIED / REJECTED:** the coordinator that would naturally supply Empire scope is the
  rejected universal `CapabilitySystem` (`docs/d042-…:141-142`).
- **The sharpening that narrows what is being asked for, from arch-C §6:** *"**The blocker is in the
  READING, not only in the STORING.**"* Even a *derived* Empire quantity is invisible to every
  predicate in the project unless it is **published**, because the D-020 grammar's operands are
  `variableName | numberLiteral` — *"No functions, no arithmetic in v1"*
  (`Sim.Core/Systems/ClassMobility/Predicate.cs:10-27`). **So the substrate is required whichever
  way the storage question is answered.**

**What each side implies.** arch-D **Q1** states the two shapes: a **second, additive polity-keyed
row type**, or **generalizing `VariableRow` with a scope discriminant** — and the second *"changes a
serialization contract inside the M0 freeze perimeter and therefore takes a Contradiction Report
rather than a packet."*

**What is blocked.** M5's own legitimacy and authority quantities (both Empire-scoped by nature);
D-042 §8.3 itself; the entire Empire half of arch-C; arch-FGH Part 15's *"civilization-wide"*
question; and carrier debt **CD-1** in `arch-M` §8. **Note it is asked for by two independent
milestones for two unrelated reasons — knowledge and taxation (P-08) — at roughly the same time.**

*Found by:* recovery-knowledge **G-01** · recovery-architecture **G-05** · recovery-civics **C-09 /
Q-14** · arch-C **§6 / Q-C15** · arch-D **§3.3 / Q1** · arch-E **OWED-E4** · arch-FGH **X-3 /
Q-H1** · arch-N **N1**.

---

### **P-10 — "Money is M5" versus GOV-2 §1a's "money is NOT folded into M5"**
**BLOCKS M5 START (scope). BR-5.**

- **Source A, RATIFIED (director ruling, first recorded there):** *"M5 taxes in kind. **Money is NOT
  folded into M5** and does NOT get an inserted milestone before it; it remains deferred as D-030
  states, to a real milestone later in the ladder … **money then arrives as an institution that
  EMERGES** — the project's grammar (law 4, no calendar gates: coinage must derive from computed
  state, never from a date)."* — `docs/m4-pre-spec-dependencies.md:23-41` (GOV-2 §1a).
- **Source B:** `docs/m4-spec.md:48` — *"**M5 GOVERNS.** Taxes, in kind … No currency, no fiscal
  system, no administration at M4"*, with *"money is M5"* reported to appear in **eight** places in
  the M4 spec (`docs/milestone-architecture-governance.md:218`, SECONDARY).
- **Status:** D-042 §15 lists *"which milestone owns money (CR-008)"* as still open (`:299`).
  **MEASURED: CR-008 exists only on `origin/m5-full-build`** (`docs/current-state.md:13`); CR-007
  calls it *"**STILL REQUIRED, and now the top blocker**"* (`:254`).

**What is blocked.** M5's scope fence; every civics line about stipends, buying off leaders, funding
opposition and coup finance (P-08); **and D-021 valve 5**, whose *"leaders are bought"* M4 pushed
to *"M5 alongside the fiscal system"* while GOV-2 says the fiscal system is not at M5
(recovery-civics **C-12**, merged here).

*Found by:* recovery-architecture **C-03** · recovery-civics **C-05, C-12** · arch-JKL **OWED-L2**.

---

## P.3 — BR-4: blocks a whole cluster

### **P-11 — Era gates in frozen material versus law 4 and D-040 B3; CR-009 was never written**
**BR-4. Rulable later, but it is inherited by every transport, military and capability packet.**

- **Source A, RATIFIED and FROZEN:** D-011 — *"(**era-gated** additions: bombard, air strike,
  dig-in)"* (`:13`), *"Later eras arrive as **data + a few new verbs** … gunpowder … industrial …
  modern"* (`:45`), *"| M11+ | era expansions | each adds its battle-layer units/verbs as data |"*
  (`:66`). D-009/D-010 — bridges and tunnels as *"expensive, **era-gated**, terrain-crossing
  edges"* (`:12`).
- **Source B, RATIFIED:** law 4 (`CLAUDE.md:19`); D-040 B3 — *"a tech-tree node opening sea travel
  is **a calendar gate wearing a tree**"* (`:59-64`); D-042 §12's anti-pattern *"calendar-date
  technology unlocks where computed predicates are intended"* (`:201`).
- **D-040 flags the D-009/D-010 instance against ITSELF and declines to rule:** *"**It is not
  amended here** — D-040 amends nothing — but the transport packet will have to face it, and it
  should not discover it late"* (`:223-227`). CR-007 §9 narrows the live part: *"The **D-011
  military instance is not yet owned** — that is the genuinely open part"* (`:255`).
- **MEASURED:** *"**CR-009 was recommended and NEVER WRITTEN** — neither has ever existed on any
  branch"* (`docs/current-state.md:13`). The prior audit asks for it to be ruled **"once,
  generally"**.

**What is blocked.** Every transport, military and capability packet built on the D-020 seam
inherits an unresolved contradiction **in its own ratified source**. And arch-C **X-06** sharpens
it: the knowledge architecture is *"the thing that would compute the labels D-011 gates on"*, so
building it makes the collision concrete rather than latent.

**ABSORBED HERE, AND RECORDED RATHER THAN MERGED, BECAUSE ITS SOURCE PAIR IS DIFFERENT —
recovery-knowledge C-04.** *Does knowledge precede advanced military?* The roadmap audit says the
ratified order already achieves it: *"**Knowledge at M7 already precedes both.** The stated
sequencing goal is, on this axis, *already satisfied by the ratified order*"*
(`docs/m5-roadmap-dependency-audit.md:41-45`). The adversarial pass **FALSIFIED** that assumption
(#2): *"the ratified gate on advanced military is **an era label, not knowledge**"*, and *"military
does **not** currently descend from capability at all — it descends from an **era label**"*
(`docs/milestone-architecture-governance.md:110-121`, `:139-153`, `:282-284`). CR-007 §10.2 leaves
the falsification standing: *"#2 advanced military is era-gated in frozen D-011 … untouched; still a
genuine Law 4 tension"* (`docs/adr/cr-007-b3-exemplar-reconciliation.md:281-282`). **The pair here is
the roadmap audit against the governance record's FALSIFIED #2 — a claim about SEQUENCING** — where
P-11's pair is frozen D-011/D-009-D-010 against law 4 and D-040 B3. Both recorded; neither
reconciled.

*Found by:* recovery-knowledge **C-03, C-04** · recovery-architecture **C-05** · recovery-civics
**C-06** · arch-C **X-06** · arch-D **§5.4 / Q10** · arch-E **X-E6** · arch-M node 19.

---

### **P-12 — Ratified mechanisms depend on literacy and education variables that do not exist**
**BR-4.**

- **Source A, RATIFIED (frozen D-018/D-021/D-039):** Intelligentsia emerges on *"literacy share +
  education institutions > threshold"* (`docs/d018-classes-and-needs.md:26`); the **Prospects** need
  is *"education access, mobility openness, growth trend"* (`:42`); rising expectations *"scales
  with the bucket's **literacy**, urbanization, and media exposure"* — and **explicitly replaced era
  tables** (`:48`); mobility runs *"→Intelligentsia via education access"* (`:63`); *"Education is a
  flow with a lag"* (`docs/d021-stability-doctrine.md:43`); D-039 A5 names *"literacy, road and
  signal infrastructure, institutions, general competence"* as command-capability inputs (`:37-40`).
- **Source B, MEASURED:** the registry publishes **four** variables — `food_surplus_ratio`,
  `artisan_share`, `population`, `trade_volume` — and **none is a literacy, education, institution
  or knowledge quantity** (`Sim.Core/State/Variables.cs:93`). **No Intelligentsia class exists in
  data** either.

**What each side implies.** *"**The dependency is ratified; the variables to satisfy it do not
exist.**"* **Six ratified mechanisms have no operands.** It also disarms the one **ratified** D-021
brake on the knowledge loop (`arch-M` cycle **C-7**), because that brake runs through literacy —
so the pairing is currently **unsatisfiable, not merely unbuilt**.

**Constraint on any answer, RATIFIED in code:** *"published variables that are all RATIOS are
SCALE-INVARIANT … **ANY emergence predicate that needs scale sensitivity MUST publish an ABSOLUTE
quantity, not another ratio**"* (`Sim.Core/State/Variables.cs:24-32`). A literacy *share* alone
cannot differentiate civilizations.

*Found by:* recovery-knowledge **C-07** · recovery-civics **Q-09** · arch-C **X-05 / OWED-2** ·
arch-D **§5.3 / 4.5** · arch-E **OWED-E5 / E14** · arch-FGH **FGH-OWED-6 / Q-H8** · arch-JKL
**OWED-L1 / DQ-L4** · arch-N **N3**.

---

### **P-13 — Where institutions live: M5 by name, M7 by the Spine, M8 by D-011, M5 by CR-005**
**BR-4. Conditional on P-07.**

- **Source A, RATIFIED:** D-035-C path 6 binds M5 **by name** (P-07 Source A).
- **Source B, RATIFIED:** the Spine places *"Politics deep (institutions, regime change)"* at **M7**
  (`:85`, `:111`); **D-011 §6** moves politics and diplomacy to **M8** (`:63`).
- **Source C:** CR-005 would place institutions at **M5** and is **OPEN**.

**What each side implies.** *"Compatible only if 'institution' means two different things in the two
places — which is exactly the unresolved P-07."* CR-005 names the ambiguity itself as part of the
conflict: *"**The word 'institutions' is doing different work in the Spine than in the
direction**"* (`:74-79`).

*Found by:* recovery-civics **C-04, C-11** · arch-FGH **X-1 / Q-I5** · arch-N **X-N2**.

---

### **P-14 — Is D-042's accumulating reserve the "research points" the mandate bans?**
**BR-4.**

- **Source A:** the mandate, Part 5 (explicit ban): *"do not invent a 'research points' abstraction
  … If you find yourself proposing a single scalar that accumulates and unlocks things, stop."*
- **Source B, RATIFIED:** *"**Knowledge generation is a resource/flow the player ALLOCATES** among
  concurrent research activities"* and *"**Unallocated knowledge ACCUMULATES AS A RESERVE** rather
  than disappearing"* — `docs/d042-…:168-171`, listed as settled at `:290-292`. CR-007 §6's
  correction notice: *"the accumulation question is **answered YES** and must not be re-asked"*.
- **Source C, MEASURED:** two earlier records **and `docs/queue.md:1213-1214`** still read the
  accumulation question as **OPEN** and were never marked (recovery-knowledge **C-06**).

**What each side implies.** arch-C §2.3 proposes a fence under which both hold — the reserve *"may
appear only as a term inside a rate equation, never as the left operand of a D-020 comparison that
gates anything"* — **but the fence is PROPOSED and unruled** (recovery **G-03**), and **two named
escapes exist**: ordered thresholds `K1 < K2 < K3` on one accumulator (proposed as *"tree edges in
disguise"* by two records, **ruled by none**), and publishing a derived 0.0/1.0 variable with no
grammar or schema change (**P-15**).

*Found by:* arch-C **X-01 / Q-C1** · arch-D **Q4** · recovery-knowledge **C-06 / G-03** · arch-O
**§II.2**.

---

### **P-15 — The latch-reference fence is enforced by an accident of the grammar, not by a rule**
**BR-4. This is the cheapest conflict in the document to act on, and the most expensive to leave.**

- **Source A, PROPOSED by three records, RULED BY NONE:** *"Capability A referencing capability B's
  **latch** is how a tree grows back — **that is the line to hold**"*
  (`docs/capability-architecture-decision.md:203-206`, SECONDARY, and that document's own
  recommendation is **UNVERIFIED**, `:34-37`); restated *"Proposed"* at
  `docs/milestone-architecture-governance.md:319-321` and `docs/adr/cr-007-…:233-234`.
  **Recovery-lane G-04: no document rules on it.**
- **Source B, MEASURED:** the D-020 grammar has **no latch operand** — operands are
  `variableName | numberLiteral` (`Sim.Core/Systems/ClassMobility/Predicate.cs:18`). **So the ban
  currently holds by the grammar's shape, not by a rule.**
- **The hole, stated precisely by arch-D Q3:** a system *can* publish a 0.0/1.0 variable derived
  from its own latch, **with no grammar change, no schema change and no review trigger**, and a
  second capability can then read it. **Is that legitimate publication, or the technology tree
  returning through the seam?**

**What is blocked.** Nothing today — which is the point. `arch-M` §7.4 records it as **FLAGGED cycle
C-8**, the one unresolved cycle *"that can be created with no schema change and no review
trigger"*. **INFERRED: this should be ruled BEFORE the D-020 grammar or the published-variable
registry is widened, not after.**

*Found by:* recovery-knowledge **G-04** · arch-C **Q-C4** · arch-D **Q3** · arch-E **X-E5 / Q-E5** ·
arch-M **§7.4**.

---

### **P-16 — Does a research activity COMPLETE, and does completion grant anything?**
**BR-4.**

- **Source A, unratified but explicit:** *"When research crosses **completion**: 1. the technology
  becomes available at that point in simulation time…"*
  (`docs/m5-temporal-control-and-player-agency-placeholder.md:154-169`); M5 scope items 7–11 —
  *"Multiple simultaneous research projects"*, *"Research capacity and throughput"*, *"Technology
  prerequisites and alternative technological pathways"*
  (`docs/m5-research-technology-institutions-placeholder.md:79-84`). **Both placeholders declare
  themselves unratified.**
- **Source B:** *"a *completing project that grants a capability* remains **prohibited** —
  unchanged"* (`docs/adr/cr-007-…:215`); logged as conflicts #6 and #7 at
  `docs/capability-architecture-decision.md:375-376`.
- **D-042 settles HALF and says nothing about completion:** parallelism **YES** (§9.3), a rigid
  one-at-a-time queue **banned** (§12).

**What is blocked.** arch-C §3(12), arch-E Part 7 and arch-O **§II.4** are all written on a
**no-completion** assumption, and all three flag that the assumption would have to be revisited if
completion is ruled in. It is also **the difference between a design with a satisfying player
moment and one without** — arch-E **Q-E1** puts the cost honestly.

*Found by:* recovery-knowledge **C-10 / G-02** · arch-C **X-08 / Q-C3** · arch-E **X-E4**.

---

### **P-17 — Does a single system owning the HOLDING relation for every domain resurrect the rejected `CapabilitySystem`?**
**BR-4.**

- **Source A, RATIFIED / REJECTED:** *"**Do not create a universal God system such as a
  `CapabilitySystem`** that owns every capability or coordinates every domain"* — `docs/d042-…:141-142`;
  anti-pattern `:200`. **The reason is law 6**, not taste: §7.3 sits inside §7, titled *"STATE-MEDIATED
  DEPENDENCY (reaffirms Law 6)"*.
- **Source B, RATIFIED disposition:** *"conformant **only** as a *shared predicate grammar consumed
  independently by each domain system* … and **not** as a coordinating owner"* (`:280-282`, §14.5).
- **Source C, the narrowing the recovery lane draws:** *"**The rejected property is therefore
  CROSS-DOMAIN COORDINATION AND OWNERSHIP, not capability evaluation as such**"* — §8.1, in the very
  next section, ratifies that capability evaluation continues.
- **Source D, MEASURED (this lane, `arch-M` X-M2):** **all four published variables are published by
  `ClassMobilitySystem`** (`:144-165`), including `trade_volume` (a trade quantity) and `population`
  (a demographic one); and the shared `Predicate` type lives inside
  `Sim.Core.Systems.ClassMobility`, reached by qualified name from `ProductionSystem`
  (`:105-108`). arch-D §5.9 calls it *"a latent conformance hazard of **location**, not of
  mechanism."*

**What each side implies.** arch-C §4.1 argues its design is on the permitted side — it publishes
variables and answers nothing for anybody. *"An adversary can reasonably reply that a system owning
**the** knowledge table for every domain is the same centre of gravity under another name."*
**And there is a contradiction candidate inside one record**: an argument that §8.3's
scope-distinguishing evaluation *requires* a central owner *"would be an argument between §8.3 and
§7.3, both inside one record, and would be a contradiction to escalate rather than a design choice
to take"* (INFERRED there; repeated as such).

*Found by:* arch-C **X-02 / Q-C11** · recovery-architecture **§4a** · arch-M **X-M2** · arch-O
**§II.1**.

---

### **P-18 — Whether a capability outlives its preconditions: D-042 §5.4 versus the shipped latch**
**BR-4.**

- **Source A, RATIFIED:** *"A government transition **preserves unrelated accumulated Empire
  state** unless a specific mechanic explicitly changes it."* — `docs/d042-…:92-93` (§5.4).
- **Source B, MEASURED and recorded as a correction in place:** *"**The shipped latch records
  CURRENT satisfaction under hysteresis, not history**: 'Inactive + emerge true → Active = 1;
  **active + recede true → Active = 0** … **Recede absent = never recedes**'. Monotonic acquisition
  exists **only in the special case of an omitted `recede` clause** — a data choice, not a property
  of the mechanism."* (`Sim.Core/Systems/ClassMobility/ClassMobilitySystem.cs:28-33`; correction at
  `docs/milestone-architecture-governance.md:242-251`).
- **Status:** *"Any design relying on capability outliving its preconditions **must say so
  explicitly and justify it**."* Design principle 10 is marked **"Open"** (`:329-330`). **The
  uncorrected version still circulates** in `docs/m5-roadmap-dependency-audit.md:220-221`.

**What each side implies.** The join between *"state is preserved"* and *"capability is not
remembered"* is unruled. **arch-D Q5 sharpens the governance consequence:** irreversibility is
currently a **data choice** — an omitted `recede` clause, one line in a data file, needing no
review. **Is an omitted `recede` a tuning decision (LIVING, no procedure) or a design decision
needing a ruling each time?**

*Found by:* recovery-knowledge **G-06** · recovery-architecture **C-12** · recovery-civics **C-10**
· arch-C **Q-C5** · arch-D **Q5** · arch-FGH **Q-G6**.

---

### **P-19 — D-018's per-class INCOME column versus D-042's ban on individual economic ownership**
**BR-4.**

- **Source A, RATIFIED and FROZEN:** D-018 lists per-class income — Laborers *"wage"*, Artisans
  *"skilled wage/own-shop"*, Clergy *"stipend/tithe"*, Soldiers *"stipend"*, Bureaucrats
  *"stipend"*, Merchants *"trade profit"*, Aristocracy *"land rent"*
  (`docs/d018-classes-and-needs.md:19-27`).
- **Source B, RATIFIED:** *"**Population does not independently own economic resources.** There is
  **no household wallet model** and **no treasury-versus-population-money model.**"*
  (`docs/d042-…:75-80`).
- **Status, RATIFIED in D-042 itself:** *"**Status: UNRESOLVED. Owner: director.** … it must be
  ruled before any class-income mechanic is specified."* (`:237-249`). **D-018 was not edited.**
  D-042 offers both readings — mechanical (conflict) and descriptive (no conflict) — and declines
  to choose.

**What is blocked.** Every civics line about stipends, buying off leaders, funding opposition and
coup finance sits on the answer, and so does arch-JKL's **OWED-L2** (*"the carrier for 'this class
now buys pottery' is **a purse**"*).

*Found by:* recovery-civics **C-01** · arch-FGH **X-4** · arch-JKL **OWED-L2**.

---

### **P-20 — Soil and land quality: the ruled dichotomy has no slot for them, and the substrate refuses them**
**BR-4.**

- **Source A, RATIFIED (director ruling, CR-003):** *"land quality is a constant of the land;
  weather is a coefficient on realised output"*; applying a multiplier to the **land** side is
  forbidden; the derived **26.0** is *"not provisional and not negotiable"* (`cr-003:297-298`).
- **Source B, RATIFIED:** ADR-008 makes terrain rasters *"**immutable after worldgen** … **excluded
  from the per-turn `Clone()`** … the terrain **content hash** … is folded into the canonical state
  stream"* (`:8-15`); the reserved route reverses the exclusion *"only for the layers that gain
  writers"*.
- **Source C, RATIFIED:** D-009/D-010 name **soil** and **vegetation** as intended raster layers,
  while **D-022 anti-scopes erosion and climate simulation**.
- **Source D:** the mandate's Part 11 progression — manure → soil restoration, industrial
  agriculture → yield up, fertilizer → nutrient limitation down — *"every item of which is a claim
  about **land quality changing**."*

**What each side implies.** arch-I §6.4 states the three shapes and what each breaks: multiply the
land side (**reopens the ruled meaning of 26.0**); multiply output only (**mis-states what soil is,
and leaves D-021 loop 1 unpaired** — `arch-M` cycle **C-12**); or promote ADR-013's land budget
(cropped share 0.28, fallow 2-in-7, pasture 25 ha, woodland 15 ha) to **state**, which **opens a
derived constant's internals**. *"**Three ratified items point three ways** and nothing reconciles
them."*

*Found by:* recovery-climate **gaps 6–9** · arch-I **X3, X5, O5, Q4** · arch-E **OWED-E7 / E11**.

---

### **P-21 — A bounded climate signal is a clamped signal, and a ruling in force forbids clamping weather**
**BR-4.**

- **Source A, RATIFIED (in force):** *"Weather unclamped and unmodified (pending G1)"*, with
  `sigmaLogYield` and `correlationTimeYears` on the no-move list (`cr-015:573-576`, `:581`).
- **Source B, DIRECTOR-STATED (relayed, UNVERIFIED as a repository document):** a **deterministic,
  BOUNDED climate signal** — the fourth path of **P-02**.
- **arch-I states the collision against itself:** *"**A bounded signal is a clamped signal by
  construction.** The whole of §3 is presently forbidden by a ruling in force."*

**What is blocked.** The entire arch-I climate substrate. **And it is coupled to P-01:** bounding
the weather *"removes the tail against which ADR-024 derived the weather/disaster separation and
changes what CR-016's three broken gates are arguing about"* (arch-I **Q3**).

*Found by:* arch-I **X1, Q3** · m4-closure-audit **§5.4** · recovery-climate **E-119**.

---

## P.4 — BR-3: blocks one packet or one mechanism

| # | conflict | source A | source B | what is blocked | found by |
|---|---|---|---|---|---|
| **P-22** | **The severity derivation reasons BACKWARD from the buffer; the Director's chain reasons FORWARD from hazard frequency.** Under A, *"improving the economy mechanically requires hazards to grow, because the event must still exhaust a larger buffer"* — and §7.2's hazard/vulnerability split is well-defined only under B. | `Sim.Data/content/sim.json:234` (RATIFIED): `s_min` derived so the event can exhaust `B_eff`; *"Dimensional, not a corridor fit."* | the Director-stated chain, *"must not invent the underlying frequency by tuning for acceptable gameplay"* (`m4-closure-audit.md:551-555`, relayed, UNVERIFIED) | the whole of arch-JKL Part K; **CR-016 option 2** (*"re-derive λ against the demographic kernel"*) points at the frequency, which the chain says must not move for outcome | arch-JKL **X-03, X-04, DQ-K1** · m4-closure-audit §4.6 |
| **P-23** | **Storage technology is the highest-value enrichment item and cannot ship without meeting the quarantined Malthus corridors.** MEASURED: the world is storage-limited on **4,800/4,800** turns and **~55 %** of every harvest is destroyed, so **any** change to T2 moves the demographic outcome directly. | the Director's own list: storage technology is *"Most valuable of the list… resilience becomes a real investment decision"* (`docs/m4-blocking-material.md:87-101`) | the corridors are QUARANTINED with bands **held immovable**: *"Do not loosen it, fit yield to it, alter consumption or demographics to satisfy it, fabricate starvation, or delete the quarantine"* | the first food-technology packet, whatever it is | arch-JKL **X-01, DQ-J1** |
| **P-24** | **The granary's carrier is named in ratified data and is read by no equation.** `StructureRow` is written by `ConstructionSystem` when a granary completes and is read by **no resolution equation anywhere in `Sim.Core`**; the constant scales with **population** instead. | `Sim.Data/content/sim.json:43`: *"a structure of finite size, which grows with the settlement because more households means more granaries."* | MEASURED, two independent lanes | any storage-technology design meets it **on day one**; and it sits inside a quarantine-adjacent subsystem | arch-E **X-E8, Q-E11** · arch-JKL **X-02** · arch-M **X-M4, S18** |
| **P-25** | **The weather's spatial coupling runs over TRAVEL COSTS** — *"improving roads shrinks the weather field's effective footprint in map terms"* — and the two constants that control it have **no semantic test** (mutants forcing k = 1 and a constant kernel both pass). | the shipped blend over `SettlementDistances`, ratified at T3.4b/T3.4c with *"SPATIAL CORRELATION REQUIRED"* behind it (`m3-spec.md:52`) | arch-I §3.3: climate correlation must be **geographic**; recovery **E-32/E-40/E-44** | changing it moves **shipped ratified behaviour and every golden**; leaving it means an edge **Transport → Climate** that nobody designed | arch-I **X4** · arch-M **X-M3** |
| **P-26** | **The interaction matrix does not exist while same-turn edges ship.** The frozen kernel names it twice as the artefact recording deliberate same-turn edges. | `docs/m0-kernel-spec.md:66`; `docs/civ-sim-architecture-v3-outline.md:32`, `:117` | MEASURED: same-turn edges ship and are documented at their own sites (`ConsumptionSystem.cs:46-48`); the shared-table sanction lives in `SystemCatalog.cs:31-60`; **no matrix artefact located** | a future packet adding a same-turn edge has **no single place to look** to see what it is joining | recovery-architecture **G-06** · arch-M **X-M1, F5–F7** |
| **P-27** | **Exit-closing is an M5 policy lever whose counterweight is an M8 valve.** D-021 rules that closing exits *"redirects pressure into Voice, with everything that implies"*, and M5 ships valve 3 — but Voice's escalation machinery (movements, organization, leadership, the D-019 matrix) is **M8**. Also MEASURED: *"exit openness"* exists today as a computed term `ω` inside the migration hazard, **not as a player policy**, so the lever does not yet exist in either form. | `docs/d021-stability-doctrine.md:28`, `:48` | `:49`; ADR-025 | whether closing exits is admissible as an M5 policy at all | arch-FGH **X-7, Q-G4** · arch-M cycle **C-16** |
| **P-28** | **A grievance source lands at M6 while its natural brake is an M8 valve.** D-037 E3 places occupation-without-legitimacy grievance at the M6 battle layer; D-035-C path 7's Endurance buffering is an M8 valve. *"A tension, not a contradiction — it becomes a contradiction only if the occupying mechanism closes exit."* | `docs/d037-emergent-polities.md:182-186` | `docs/d021-stability-doctrine.md:47-50` | whether F14's *"brakes install with the gas pedal"* means the **source** defers or the M5 valves suffice | arch-FGH **X-9, Q-G5** |
| **P-29** | **Rising expectations is a positive feedback whose ratified brake lands three milestones later.** D-018 §4's *desired* consequence is *"development raises unrest potential before satisfying it"*; the Endurance valve is M8. | `docs/d018-classes-and-needs.md:48` | `docs/d021-stability-doctrine.md:8`, `:47-50` | whichever milestone implements rising expectations owes a brake **in the same milestone** | arch-JKL **X-06, DQ-L5** · arch-M cycle **C-16** |
| **P-30** | **Two design lanes disagree about whether a traded good can carry a technique.** arch-FGH: D-035-C's list opens with *"a good"*, so a good legally carries the **existence** of a technique. arch-C: *"goods move, people do not … **A good is not a teacher.**"* | `docs/design/arch-FGH-…:§4.1` ch.2 | `docs/design/arch-C-knowledge.md:583` (OWED-1) | the diffusion channel roster; **both are PROPOSED, neither ratified, no sibling file touched** | arch-FGH **X-8, Q-H2** · arch-C **X-04** · arch-M **X-M5, CD-4** |
| **P-31** | **Two of the mandate's Part 11 progression items have no ratified milestone at all.** D-011 §6 does not name **Environment & climate** or **Military full**; GOV-2 §1c marks both rows AMBIGUOUS. | `docs/civ-sim-architecture-v3-outline.md:89`, `:90` | `docs/m4-pre-spec-dependencies.md:143-160` (findings F3, F4) | **arch-I is an entire architecture lane whose milestone is unknown** | recovery-architecture **C-04** · arch-I **Q9** · arch-N **§2.7, X-N5** |
| **P-32** | **The "M4 Happiness is a FOUNDATION, not the permanent definition of human needs" decision has no locus in the tree.** The nearest citable lines are `SettlementHappiness.cs:36-49`. | the 2026-09-19 mandate (relayed) | MEASURED absence across `docs/`, `docs/adr/` and the source file | arch-JKL Part L preserves the reading and **queries rather than reconstructs**, per the D-035 precedent that *"an uncited ruling is refused and queried, not reconstructed"* | recovery-food **C-07** · arch-JKL **X-05, DQ-L1** |
| **P-33** | **May happiness read the needs aggregate?** Named a **DIRECTOR'S CALL** in code, in the exit inventory and in ADR-023 §4. It gates the diet-quality path, the unification of the two numbers, and any richer well-being model. | `Sim.Core/State/SettlementHappiness.cs:36-38` | `docs/m4-exit-inventory.md`; ADR-023 §4 | every evolving-needs mechanism arch-JKL designs is **invisible to the player** while the answer is "no" | recovery-food **Q-17** · arch-JKL **DQ-L6** · arch-FGH **Q-G9** |
| **P-34** | **"Water" carries two incompatible readings in the tree.** `m4-exit-inventory:148` — *"**Absent, and stated rather than stubbed:** WATER is not modelled anywhere"* — is a list of **happiness factors** and means water as a *consumed human need*; the code means **geographic** water (`TerrainSet._water/_moisture/_rivers`, `transport.riverCostFactor`). | `docs/m4-exit-inventory.md:148` | `Sim.Core/Worldgen/TerrainSet.cs:21,23,26`; `sim.json:21-22` | a future reader meets the unqualified sentence without its §4 heading. Related: **is water a ninth need, a happiness factor, or neither** — two records point in different directions and neither resolves the other | recovery-climate **C-06 / T-11** · recovery-food **Q-19** |
| **P-61** | **"Education and literacy as MODIFIERS" versus law 2.** The M5 placeholder names *"5. Education and literacy as **modifiers** of knowledge production"* as one of the mechanics it sketches, and lists *"government modifiers"* among the items *"deliberately ABSENT"* until a dedicated M5 design workshop. The governance record flags the shape without ruling it: *"**Law 2 hazard flagged, not a violation:** … A free-floating permanent modifier is the banned construct. **D-035's shipped shape is the legal one** — *'one institution raises one need and lowers another'*, a two-sided mechanism."* | `docs/m5-research-technology-institutions-placeholder.md:73`, `:16` — and the placeholder **declares itself unratified** (`:3-11`) | `docs/milestone-architecture-governance.md:205-209`; law 2 at `CLAUDE.md:17` | whichever packet builds knowledge production: **the hazard is named, not ruled**, and the two-sided D-035 shape is offered as the legal alternative **but has not been ruled to apply here** | recovery-knowledge **C-11** |

---

## P.5 — BR-2 and BR-1: record-level conflicts that mislead but block nothing

| # | conflict | both sources | disposition |
|---|---|---|---|
| **P-35** | **TEN LAWS versus SEVEN.** Spine S2 has **ten** numbered laws, FROZEN; `CLAUDE.md` has **seven**, differently numbered. Spine law 3 is *"no instant transformation"*, short-form law 3 is dt-correctness. **Spine law 5 (*"computed, never assigned"*) has no short-form counterpart**; short-form law 5 is determinism, which is **Spine law 9**. Spine laws 5, 6 (glass box), 7 (symmetry), 8 (data-driven) and 10 (tiered realism) are **absent from the list every agent reads first**. Short-form law 7 (types) has no Spine counterpart. | `docs/civ-sim-architecture-v3-outline.md:19-28` vs `CLAUDE.md:15-22` | **BR-2, and rising.** *"No document on the tree states that the short form is a complete restatement, and none states that it is a subset."* **Needs a ruling on which enumeration binds and on what "law N" means in a future document** — every M5 spec, ADR and CR will cite "law N". Recovery **C-01 / G-01**; arch-O quotes both, by file, every time. |
| **P-36** | **GOV-2 is cited as a RATIFIED source; `docs/gov-2*` exists on no ref.** | `docs/m4-spec.md:35-37` cites **GOV-2 §1a/§1b/§6** for three M4 decisions; further citations in D-039 and the capability record | **MATERIALLY CORRECTED BY THIS LANE — MEASURED, `26d12d3`.** The **filename** claim is right (`git log --all --diff-filter=A -- 'docs/gov-2*'` returns nothing on any ref). **But the GOV-2 filing's CONTENT is on the tree AND on `origin/main`**, as `docs/m4-pre-spec-dependencies.md`, titled *"# M4 — PRE-SPEC DEPENDENCY FILING (GOV-2)"* (`:1`); its §1a (`:23-41`), §1b (`:100-115`), §1c (`:143-160`) and §6 (`:460-463`) match every citation clause for clause. **THREE THINGS THIS DOES NOT SETTLE, and a ruling is still required on each:** (i) **GOV-1 is still unlocated** on any ref while cited in seven places; (ii) the document **calls itself *"unmerged and unratified"*** (`:14`) — *stale* as to "unmerged" (MEASURED: it is on `origin/main`), *unretracted* as to "unratified" — while three M4 decisions cite it as ratified; (iii) whether a GOV-N record should be re-homed to its own filename so a citation resolves by search. Recovery-architecture **C-02 / G-04**; arch-N **§1.1, X-N4**. |
| **P-37** | **Two of CR-015's N1 amendment sites did not land.** N1 states the frozen text now reads *"…independently of the food balance **AND of an exceptional cause**"*, across three sites. **MEASURED (this lane, `26d12d3`):** `docs/m3-spec.md:52` still carries the **unamended** sentence; `Sim.Core/Systems/Harvest/HarvestWeatherSystem.cs:25-26` still says *"decided downstream by the food balance **alone**"*, and **`FoodState` and `CR-015` appear 0 times in that file**. The third site, `cr-003.md`, **did** land by append (`:521`). Five other N-line appends also landed and were verified. | `docs/adr/cr-015-…:403-414` vs the two sites | **BR-2.** The packet's own rule was *"Governed-but-wrong mechanisms get the amendment BEFORE the code"* (`cr-015:395-396`). **A reader arriving at either site first gets the superseded rule.** `CLAUDE.md`'s standing ban on rewriting frozen documents is one candidate explanation and **is not asserted as the cause**. Recovery-food **C-01**; recovery-climate **C-01**. |
| **P-38** | **ADR-017 reads *"director certification pending"* while the M4 spec cites its ruling as settled and ADR-018 amends the same decision.** | `docs/adr/adr-017-…:3` vs `docs/milestones.md:142`, `:362` | **BR-2. OPEN — REQUIRES DIRECTOR RULING**; *"A status contradiction only the director can resolve"* (`:384`). Audit **R8**. |
| **P-39** | **CR-007 declares itself *"RESOLVED WITHOUT A NEW RULING"*; the routing document lists it among open CRs.** A self-declared resolution by the finding's own author is not a director ruling. | `docs/adr/cr-007-…:3-4` vs `docs/current-state.md:368-370` | **BR-2.** Material because arch-D §3.6(3) and §5.6 cite CR-007 §8.4's *"schedule"* framing, which carries whatever standing CR-007 has. Recovery-knowledge **C-01**; arch-D **§5.8**. |
| **P-40** | **CR-004 says in §0 there is NO conflict between the two frozen items and in §2A that *"the conflict in §1 is real"*.** Both sentences are in the same RETAINED document, *"Retained in full, not deleted — the record of a falsified hypothesis is the point."* | `docs/adr/cr-004-…:26-28` vs `:131-133` | **BR-1.** The CR's status is WITHDRAWN and it simultaneously leaves a located conflict **at the turn length**. Recovery-food **C-02**. Adjacent: **the controlled dt experiment RAN** and its data kills the temporal-resolution hypothesis by CR-004 §5's own pre-commitment — **no document records that reading being taken** (recovery-food **Q-15**). |
| **P-41** | **The capability record's BLOCKING conflict #2 is FALSE on this tree.** *"there is **no `PolityRow` type, no `Polities` table**, and **nothing anywhere in `Sim.Core`/`Sim.Cli` constructs a `PolityId` outside deserialization**"* — **all three clauses false**: `WorldState.cs:704`, `:837`/`:980`, `CanonicalSchema.cs:91`/`:543-547`, `WorldFounding.cs:282-302`. | `docs/capability-architecture-decision.md:56-62`, `:371` vs MEASURED | **BR-1.** The document was not edited. **Whether that record's conflict #2 is now closed is DIRECTOR DECISION REQUIRED.** arch-D **§5.2**; recovery-architecture **G-102**. |
| **P-42** | **The shipped exemplar is a counter-example, not a model.** One conjunct is **constant-true** (measured 3.5 ± 0.1 across all twelve settlements, `Variables.cs:24-32`); the other is **universal delay by explicit tuning** (`sim.json:171`: *"520 sits above the ~350-500 jittered founding sizes, so every settlement must GROW into its artisans"*). | `docs/capability-architecture-decision.md:132-133` vs its own correction notice `:11-19` and `milestone-architecture-governance.md:230-238` | **BR-2.** The **shape** survives the falsification (a conjunction over computed published state, with hysteresis and a `recede` clause); **differentiation** does not. CR-007 §8.4 supplies the tree's own word for what it is instead — *"a **schedule**"*. arch-D **§5.6**. |
| **P-43** | **D-018's artisan trigger and the shipped predicate disagree about what makes an artisan.** D-018: *"craft specialization share > threshold"*, with no food-surplus and no market-extent condition. Shipped: `"emerge": "food_surplus_ratio > 1.3 && population > 520"`. | `docs/d018-classes-and-needs.md:20` vs `Sim.Data/content/sim.json:169` | **BR-2.** D-040 F2 records it and **assigns no owner**. arch-D **§5.5**. |
| **P-44** | **A dimensional declaration contradicts itself inside one `_doc` string.** *"T3.4b harvest variance (CR-003 ruling 3). **ALL TUNE, ALL CHOSEN - none derived**"* … then, later in the same string, *"**sigmaLogYield**: SD of LOG yield in one year, **DERIVED (0.2936)**"*. | `Sim.Data/content/sim.json:223` (both halves) | **BR-1.** S8 §4.1(c) requires each constant to be declared CHOSEN or DERIVED. Recorded, not corrected — this phase changes no data. Recovery-climate **C-05**. |
| **P-45** | **A queue entry's stated CAUSE was ruled wrong and its BLOCKING CONDITION discharged, and it still stands OPEN and unannotated.** Q-A: *"RIVERS CANNOT LIVE ON THE STRIDE-4 LATTICE … that was a resolution artifact … an ARCHITECTURE call."* T4.9 RULING: *"the cause … is **NOT lattice resolution** … a **downstream data-path gap**, not a resolution artifact, and stride does not fix it."* T4.7 then shipped `riverCostFactor`. | `docs/queue.md:468-477` vs `docs/m4-spec.md:353-363` | **BR-1.** MEASURED: `queue.md:468-477` carries no T4.9 or T4.7 annotation at HEAD. **Whether Q-A survives as an architecture question about river representation, or was closed, is recorded nowhere.** Recovery-climate **C-04**. |
| **P-46** | **CR-015 G5 pre-binds a director ruling that has not been made.** G5: *"Recorded so the T4.20 storage ruling, when it comes, cannot reopen ADR-024/026."* But T4.20 is fully OPEN and explicitly reserved: *"A director ruling, not an agent's judgement, is what closes it."* | `docs/adr/cr-015-…:498-501` vs `docs/t4.20-food-semantics.md:88-89` | **BR-2.** *"whether an orchestrator decision can fence a future director ruling is not this lane's to say."* Recovery-food **C-05**. |
| **P-47** | **CR-016 vs CR-015's acceptance of the G8/F4 dt artefact.** CR-015 G8(a) accepts it as **INHERITED** and queues the sub-step for M5; CR-016 §1 D lists the same acceptance as a **frozen item in the collision** and §4 option 3 says it should be **REVERSED** — *"it is the only option that removes the CAUSE."* | `cr-015:511-516` vs `cr-016:31-34`, `:144-148` | **BR-3, conditional on P-01.** CR-016 states why both can be true: *"it was accepted as an artefact at a time when λ = 0 made it unobservable."* Recovery-food **C-03**. |
| **P-48** | **`milestones.md` states M4 delivered no food-supported population cap because *"a cap would have had nothing to correct"*; ADR-026 ships a headroom growth cap.** | `docs/milestones.md:332-334` vs ADR-026 `:10-17` | **BR-1.** The supersession **is** recorded in the same file (`:321-322`, CR-015 N3), so it is documented rather than hidden. Recorded because the original measurement is never retracted and *"remains the best evidence about what the cap will and will not bind on."* Recovery-food **C-06**. |
| **P-49** | **The disaster mechanism's shipped rate contradicts its own derived value, by design, in two records.** `sim.json:235` ships **0.0**; ADR-024 §3 gives λ = **0.01** as the CHOSEN value with its reference class; CR-015's T4.21-4 block records behaviour **at 0.01**. | `Sim.Data/content/sim.json:234-235` vs `docs/adr/adr-024-…:176` | **BR-1, not a defect** — both sides are labelled. Recorded because **any number quoted from ADR-024 §10 or CR-015's T4.21-4 block describes a world that is not shipped**, and the two are easy to mistake. Recovery-food **C-04**. |
| **P-50** | **D-037 D2 assigned the player-scope question to the M4 spec; the answer was ruled elsewhere.** D-037: *"This is the M4 spec's central question and **must be answered there, not assumed**."* The M4 spec contains no D2 answer; D-042 §1.1 ruled it; CR-011 settled the id. | `docs/d037-emergent-polities.md:138-141` vs `docs/d042-…:26-27` | **BR-1.** What is unrecorded is whether D-037 D2 is thereby **discharged**, given `aiEmpires` defaults to **0** and MEASURED *"one polity controls everything founded, so every pair is Domestic or Unruled."* Recovery-civics **C-07**. |
| **P-51** | **Control cardinality was reconciled in a SHIPPED CODE HEADER, not by a ruling.** D-037 A3: *"exactly one, or none"*; D-040 C7 / T4.3: *"contested where claims overlap"*. The reconciliation is `Sim.Core/State/WorldState.cs:596-602`. | `docs/d037-…:28-30` vs `docs/d040-…:137-146` | **BR-2.** *"the authority of that reading is a packet author's, and **nothing enforces the cardinality**"* (`:604-606`). Recovery-civics **C-08**. |
| **P-52** | **The AI constitution's §23 citations do not resolve against the tree**, including rendering Spine principle 7's *"Difficulty = information and **friction**"* as *"information and **decision quality**"*. *"Not cosmetic: *friction* names a ratified, world-side, player-symmetric lever (D-039 is titled COMMAND FRICTION), whereas *decision quality* is the document's own AI-side competence concept."* Three other terms have **0 hits each**. **And its status is asserted nowhere.** | `docs/m5-ai-constitution.md:296-300`, `:5` vs `docs/queue.md:1163-1185` | **BR-3 for any M5 AI packet.** *"Whoever writes the M5 AI spec must reconcile §23 against the tree before implementing from it."* Recovery-civics **C-13, Q-17**. |
| **P-53** | **D-042 §6.2 rules a persistent directive is STATE; the M5 temporal placeholder still records *"Are policies **orders** or a **new standing-state table**?"* as an open question.** | `docs/d042-…:99-106` vs `docs/m5-temporal-control-…:243-245` | **BR-1.** D-042 is later and ratified; the placeholder is earlier and unratified; **whether §6.2 closes open question 5 is stated in neither, and the placeholder was not amended.** Recovery-civics **C-14**. |
| **P-54** | **THE DERIVATION CYCLE — new this lane.** A constant derived from a quantity that a shipped mechanism moves in response to it closes a loop **in the derivation, not in the turn pipeline**, so **neither the prev-read break nor D-021 reaches it**. The live instance is **P-22**. | `Sim.Data/content/sim.json:234` vs `docs/spine-s8-governance-freeze.md:154-182` (corridor independence, which states exactly this shape **for corridors** and does not reach derived constants) | **BR-3.** `arch-M` §7.3 proposes a third clause for the cycle rule and **does not adopt it**. **DIRECTOR DECISION REQUIRED** on whether S8 §4.1's self-referentiality test extends from corridor bands to derived constants. |
| **P-55** | **GOV-3 Parts A and B are filed as a CANDIDATE ADR-015 section, not a written one**, while the document header reads *"HELD FOR MERGE"* — and the record makes the point about itself: *"a queue line does not bind an agent the way a numbered §7.x does."* | `docs/gov-3-execution-protocol.md:3`, `:139-153` | **BR-2.** Until ruled, conditional execution and standing autonomy are **practice, not law** — including **B3's finding/fixing separation**, which is the rule every lane in this design phase operated under. Recovery-architecture **A-106–A-110**. |
| **P-56** | **ADR-019 exists only on an unmerged branch**; the ADR sequence on the candidate jumps **018 → 020**. | `docs/milestones.md:366`; MEASURED: `ls docs/adr/` | **BR-1.** Audit **R12**. A standing instance of GOV-4 §2's *"never collapse LOCAL / REPORTED / REMOTE / MAIN."* |
| **P-57** | **ADR-020 / R-3 — the clone architecture — awaits a ruling, and R-3 is *"THE LARGEST UNSCHEDULED ITEM IN THE PROJECT, AND IT BITES AT M8/M9."*** MEASURED: 384 bucket rows today; clone 82,096 B/turn; **projected 153,600 rows at charter late game, already past the ratified ~150k cap** — and the cap's *"automatic merge-below-threshold policy presupposes sparse or merged storage and **DOES NOT EXIST**."* | `docs/m4-spec.md:465-508`; ADR-020 `:8-16`; `docs/milestones.md:363` | **BR-3 now, BR-5 at M8/M9.** ADR-020 explicitly sits on top of *"a live, already-recognised contradiction"* — a frozen kernel claim (`m0-kernel-spec` §3.2's *"at M0–M9 scale this is a few MB"*) measured **wrong at the far end**. Audit **R9**. |
| **P-58** | **`docs/observability-architecture.md` is not on `origin/main`.** Its own header says *"Not merged to main"*; it exists on `t4.19-glass-box` and this branch. Four ratified invariants in `arch-O` (I-19…I-21, I-23) are quoted from it. | MEASURED (this lane and arch-FGH **X-5**) | **BR-1.** *"Anything leaning on it as RATIFIED should say which tree it is standing on."* |
| **P-59** | **The observability taxonomy as the mandate renders it (four kinds) versus as the document reads (five plus GAP).** *"READ / RECOMPUTED / DERIVED / GAP"* vs *"exactly five things"* — **READ · SUMMED · DIFFERENCED · RESIDUAL · RECOMPUTED** — plus **GAP**. **"DERIVED" is not one of the five**; it is the tree's separate word for a *reading*. | the mandate vs `docs/observability-architecture.md:19-32` | **BR-1, a correction to the brief, not a repository defect.** Recorded under GOV-3 B4. **Five sibling lanes recorded it independently** — arch-D §5.1, arch-FGH X-5, arch-I X7, arch-JKL X-07, arch-M X-M6. |
| **P-60** | **Does D-035-C's carrier list extend to a climate regime?** *"a season"* is on the list, but the list was written for household needs, and *"a multi-decadal regime is a season only by analogy"* — while D-035-C is explicit that *"the seven paths are **not a taxonomy to be extended by analogy**."* | `docs/d035-needs-aggregation.md:91-98` vs arch-I **A1/O6** | **BR-2.** If the answer is no, the climate driver's own carrier is owed. arch-I **O6, Q11**. |
| **P-62** | **Does knowledge depend on money and taxation through institution funding?** The roadmap audit's chain: *"Institutions need funding; funding needs money and taxation; research needs institutions. **Knowledge is genuinely downstream of the governing loop**, which is why the Spine put it there."* The capability record refutes the middle link: *"GOV-2 §1a rules M5 taxes **in kind**, so institution funding never had to wait for currency. **The shipped proof is Housing** … **Corrected chain:** institutions → **in-kind upkeep** → M5 governing loop → M7 knowledge."* | `docs/m5-roadmap-dependency-audit.md:125-146` (§2) vs `docs/capability-architecture-decision.md:210-222` (§6); the in-kind ruling itself at `docs/m4-pre-spec-dependencies.md:33-41` (GOV-2 §1a) | **BR-2.** Both recorded, neither reconciled — and a third record points in a third direction: *"the economy funds institutions that produce knowledge that changes the economy. **A cycle has no 'precedes.'**"* (`docs/milestone-architecture-governance.md:155-162`, #3 FALSIFIED). Material because the sequencing rationale is what a reader will lean on when placing knowledge, and the refuting record is itself **UNVERIFIED by its own header** (`:29-37`). **Distinct from P-10**, which pairs GOV-2 §1a against `m4-spec.md:48`. Recovery-knowledge **C-05**. |
| **P-63** | **How many published variables ship — three or four.** The capability record: *"the predicate registry ships only **three** variables (`Variables.cs`)"*. The file ships **four**: `food_surplus_ratio`, `artisan_share`, `population`, `trade_volume`. | `docs/capability-architecture-decision.md:224-228` vs `Sim.Core/State/Variables.cs:93`, ids `:36-37`, `:60`, `:90` — **MEASURED** | **BR-1.** Both recorded. Cause **INFERRED**: the T4.11 merchant variable registered after that document was written. Per GOV-4 the tree wins on the fact and the record's ruling is unaffected — but the same paragraph is the source **P-12** leans on, and P-12 counts four, so a reader meeting the record first gets the wrong count. Related line drift, **MEASURED** this pass: the capability record cites the artisan `emerge` at `sim.json:165` and its `recede` at `:166` (`docs/capability-architecture-decision.md:122`, `:129`), and its `_doc` at `:167` (`:16`); on this tree those three lines are `:169`, `:170` and `:171`. Recovery-knowledge **C-08**. |

---

# PART Q — QUESTIONS REQUIRING A DIRECTOR DECISION BEFORE IMPLEMENTATION

**These are questions. None carries a preferred answer, and none is a proposal in disguise.**
Questions whose substance is a Part P conflict are **not** repeated; the cross-reference is given
instead. Questions asked by several lanes are merged, with every asking lane named.

## Q.1 — BLOCKS M5 START

| # | question | why it blocks M5 | asked by |
|---|---|---|---|
| **Q-01** | **Which of CR-005's three options?** And the crux the CR itself names: **must M5 be substantively ABOUT learning and change** — in which case *"Option B is the honest reading and C is a half-measure"* — or is M5 the governing loop with an architecture-only packet inside it? | **It determines what M5 IS.** | **P-05** |
| **Q-02** | **What polity-scoped conserved stock receives an in-kind tax** — one stock or one per good, per settlement or per Empire — given D-042 §4.3 forbids a single Empire-wide inventory while §14.2 sanctions an Empire treasury? | M5's central mechanic cannot be specified without it. | **P-08**; recovery-civics **Q-02** |
| **Q-03** | **What is the substrate for an Empire-scoped published variable** — a second additive polity-keyed row type, or a scope discriminant on `VariableRow` (which changes a serialization contract **inside the M0 freeze perimeter** and takes a CR, not a packet)? **Who owns designing it, and at which milestone?** | M5's legitimacy and authority are Empire quantities. | **P-09**; arch-C **Q-C15**, arch-D **Q1**, arch-FGH **Q-H1**, recovery **G-01/G-05** |
| **Q-04** | **What IS an institution, mechanically** — a stock, a built structure, a table row with a lifecycle, a published scalar, a composable module, an organizational actor — **and is it one thing or several?** If several, how do they co-exist without one row carrying two meanings the governance record calls *"mutually incompatible in kind"*? **Who writes CR-010, and against which milestone?** | D-035-C path 6 binds M5 **by name**. | **P-07**; arch-C **Q-C12**, arch-D **Q7**, arch-FGH **Q-I1** |
| **Q-05** | **What IS legitimacy** — a derived reading, an accumulated stock held by a population, or a split pair — and what is its **scope**, Empire or settlement or both? | The Spine puts *"Legitimacy & opinion"* at M5, *"mechanism-only, no mood auras"*, and the quantity is **defined nowhere** while being named as an input in four ratified places. | recovery-civics **Q-03**; arch-FGH **Q-G1** |
| **Q-06** | **What is the "authority/bandwidth economy", and what is "laws-lite"?** What is authority, what spends it, what replenishes it, and what is its scope? Is a law a policy, an institution, a persistent directive, or a fourth thing? | **Each is named once, in the frozen Spine M5 line, and nowhere else.** | recovery-civics **Q-04, Q-05**; arch-FGH **Q-G7** |
| **Q-07** | **What are M5's exit criteria?** S8 §1 freezes *"each milestone's exit-criteria definitions"*, and M5's is *"'It's a game now' — evaluate fun honestly here before proceeding."* **How is that discharged, and by whom?** | A milestone cannot start toward an undefined gate. | recovery-architecture **G-08** |
| **Q-08** | **Which packet lifts the grievance read-isolation quarantine**, and does `SettlementHappiness` then read the needs aggregate — which the tree makes **a Director's call, not an engineering one**? | `scripts/check-read-isolation.sh` enforces that nothing reads grievance until M5. The lift is M5 work by the gate's own text. | **P-33**; arch-FGH **Q-G9**; recovery-food **Q-17** |
| **Q-09** | **Which law enumeration binds** — the Spine's ten or `CLAUDE.md`'s seven — and are Spine laws 5, 6, 7, 8 and 10 in force **as laws** or only as Spine text? **When a future document writes "law 4", which document is it citing?** | Every line of the M5 spec, every ADR and every CR will cite "law N". | **P-35**; recovery **G-01** |
| **Q-10** | **Does GOV-2's filing bind, given the document carrying it calls itself *"unratified"* while three M4 decisions cite it as their ratified source — and where is GOV-1?** Should GOV-N records be re-homed to their own filenames? | The M5 spec author's rewrite inventory **is** GOV-2 §1a's 17-line list. | **P-36**; recovery **G-04** |

## Q.2 — BLOCKS M4 CLOSURE (and therefore M5 start), but is a ruling on an existing CR

| # | question | cross-reference |
|---|---|---|
| **Q-11** | **CR-016 — the record:** does CR-015's arming directive stand as written, superseded by an append-only amendment, or read as never having ratified the value? **And the rate:** which of CR-016's three options, in which order? CR-016's own recommendation is *"3 for the CAUSE, 2 for the MAGNITUDE, and **NEITHER without the director**."* | **P-01** |
| **Q-12** | **G1 — (a) LEAVE, (b) APPLY `g`, (c) REFUSED, or the FOURTH PATH?** And, separately: **does the fourth path go onto CR-015 §6.4's list, or into its own CR?** That choice determines whether G1 is *answered* or *bypassed*. | **P-02**; arch-I **Q10** |
| **Q-13** | **B5 — is the density-window breach a defect, should the window be re-pinned, should the quarantine lift, and may M4 close over it?** No `corridors.json` edit before the ruling. **And separately: what obligation attaches to a drifting quarantine that no instrument reads** (known-open item 11)? | **P-03**; recovery **G-14** |
| **Q-14** | **The remaining M4 known-open rulings**, unchanged from the audit's R-list and not restated here: **R5** (the `MagnitudeCorridor` upward tooth, conditional on Q-11) · **R6** (ADR-018 §11's premise no longer holds — *"Is GROSS the wrong observable, or is the LEVER unreachable?"*) · **R7** (the CR-003 Malthus teeth's cause-attributed re-aim) · **R8** (ADR-017, **P-38**) · **R9** (ADR-020, **P-57**) · **R10** (the T4.19-E structural set S1–S5 — *"the Director's own list never arrived"*) · **R11** (grain is storage-bounded, livestock and fish are not — *"The repository records no intent either way"*) · **R14** (ADR-025 §2.4a's unmet RULE 2 criterion, recorded as a DEVIATION awaiting sign-off). | `m4-closure-audit.md` §1.3 |

## Q.3 — BLOCKS A CLUSTER, rulable after M5 starts

| # | question | asked by |
|---|---|---|
| **Q-15** | **Which milestone owns knowledge, and is the Spine's ladder superseded wholesale or line by line?** | **P-06**; recovery **G-07** |
| **Q-16** | **What is the FENCE on accumulation?** Must the accumulator be able to **fall**? Are **ordered thresholds K1 < K2 < K3 on one accumulator** tree edges in disguise? Is a monotone-in-time accumulator a **schedule**? **Which, if any, becomes a rule — and does narrowing B3 require a CR?** | **P-14**; recovery **G-03**; arch-C **Q-C1**; arch-D **Q4** |
| **Q-17** | **May a system publish a variable derived from its own latch?** Is that legitimate publication, or the technology tree returning through the seam? **Should the ban be ratified BEFORE the grammar is widened?** | **P-15**; arch-D **Q3**; arch-C **Q-C4**; arch-E **Q-E5** |
| **Q-18** | **Does a line of inquiry COMPLETE, and if so what does completion produce?** Is "completion" a concept in this architecture at all? | **P-16**; recovery **G-02** |
| **Q-19** | **Is there a TECHNOLOGY object at all, distinct from a capability** — a row, a predicate, a named bundle, or purely a label over a satisfied capability? **Both arch-D and arch-E were written without needing it, which is itself evidence about the answer; but the word is in the milestone's own name.** | recovery **G-14**; arch-C **Q-C10**; arch-E **Q-E12** |
| **Q-20** | **Does knowledge DECAY, and is decay a named `Ledger` sink with its own reason, as spoilage and granary overflow are for grain?** **The answer decides whether knowledge is a law-1 conserved `long` or a law-7 `double` — a typing question, not a flavour one.** | recovery **G-07**; arch-C **Q-C6**; arch-FGH **Q-H5** |
| **Q-21** | **Is the D-042 §9.5 reserve a conserved quantity?** Banked person-years and food (⇒ `long`, via `Ledger`), or a readiness ratio (⇒ `double`, not conserved)? *"The two answers have different schemas, different tests and different failure modes."* | arch-C **Q-C2** |
| **Q-22** | **At what grain does knowledge RESIDE?** Does *"produced/applied through settlements, institutions, **people** and research activities"* permit residence in a practitioner **class bucket** (finer than a settlement), or must everything below the Empire be an aggregation input rather than a holder? | arch-C **Q-C8** |
| **Q-23** | **One knowledge quantity per domain, or one global quantity allocated across domains?** How many knowledge quantities are there — one stock, two, or a stock and a rate; science vs knowledge; are domains literal trees, graphs or predicate sets? | recovery **G-05**; arch-C **Q-C14** |
| **Q-24** | **What crosses a contact edge in DIFFUSION, at what rate, in which direction, and is it conserved?** And — since the shipped trade table moves goods and not people — **does the D-035-C carrier test apply to knowledge transmission at all, or is knowledge exempt because it is not a conserved stock?** | recovery **G-09**; arch-C **Q-C13**; arch-FGH **Q-H2** (**P-30**) |
| **Q-25** | **How is foreign contribution gated?** D-042 §9.6 permits open borders and foreign institutions to contribute and sets no conditions; the placeholder expects gating by *"distance, diplomacy, language, institutional capacity, literacy and wealth"* and says those rules are not decided. **Nobody owns them.** | recovery **G-10**; arch-FGH **Q-H6** |
| **Q-26** | **WHAT CALIBRATION CORRIDOR GATES KNOWLEDGE OR DIFFUSION?** *"The project gates milestones on corridors; 'science output' has no obvious historical target. **A milestone that cannot be calibrated is a milestone that cannot pass its own exit criteria.**"* **No candidate corridor is named anywhere in the tree.** | recovery **G-08**; arch-C **Q-C16**; arch-D **Q11**; arch-FGH **Q-H7** — **the only open item in the knowledge cluster that can block a MILESTONE EXIT rather than a packet** |
| **Q-27** | **Which milestone publishes literacy, urbanization and media exposure**, and **is literacy a bucket property or a settlement-published variable**? Is *"educated population"* a class, a class attribute, or new state? **Who owns EDUCATION, and what is a school?** | **P-12**; recovery **Q-09, G-11**; arch-C **Q-C17**; arch-FGH **Q-H8**; arch-JKL **DQ-L4** |
| **Q-28** | **Is there a CLIMATE at all, distinct from weather?** *"The tree has a static temperature and moisture field and a stochastic output multiplier, and nothing between them. No document records a decision either way."* **It is the prior question to every other climate question.** | recovery-climate **gap 1**; arch-I **Q7** |
| **Q-29** | **Is the M9 placement of environment & climate still the intent?** *"the only source is the pre-M0 Spine table, and nothing since restates it — while the mandate's Part 11 progression reaches from the neolithic manure heap to industrial abatement, which is not one milestone's worth of world."* | **P-31**; arch-I **Q9** |
| **Q-30** | **What may change land quality** — multiply the land side (reopening the ruled 26.0), multiply output only (mis-stating what soil is and leaving a D-021 loop unpaired), or promote ADR-013's land budget to state (opening a derived constant's internals)? | **P-20**; arch-I **Q4** |
| **Q-31** | **Is a degradation stock a conserved `long` on the Ledger, or a rate-like `double`?** The Spine's own word is *"stocks"* and laws 1 and 7 read together point at the `long`; the shipped precedent for environmental state points at the `double`. | recovery-climate **gap 14**; arch-I **Q5** |
| **Q-32** | **Is there a ROSTER of government forms**, and if not, does the Director accept that a form is a **derived label** over a regime-module set and never an input to any mechanism? **What mechanism moves an Empire from one to another?** | recovery-civics **Q-08**; arch-FGH **Q-G3** |
| **Q-33** | **What is state capacity, and what is military loyalty a property of** — the Soldiers class bucket, a notable, an army, or the Empire? Both are inputs to the frozen overthrow propensity and neither is defined. | recovery-civics **Q-06, Q-07**; arch-FGH **Q-G2** |
| **Q-34** | **What is the franchise, mechanically** — what changes it, what an election resolves, and what publishes the **enfranchisement vector** D-018 requires of every regime module? | recovery-civics **Q-13**; arch-FGH **Q-G8** |
| **Q-35** | **How does a CULTURE or a RELIGION come into being, diverge, or end?** The bucket key carries both and **nothing rules how one is created, identified, split or merged** — while D-037 C1's co-ethnic claim source, E5's demographic pathways and E1's *"cultural/religious distance"* all depend on the answer. **And R-3 bites in the same milestone.** | recovery-civics **Q-10**; **P-57** |
| **Q-36** | **Who picks up a stateless settlement**, and can a stateless settlement found its own polity — which would interact with D-037 A1 NOTHING SPAWNS and D-042 §2.5's rebellion-creates-a-faction? `RevoltSystem` deliberately declines and names *"M5's politics and M6's war"* as the owners. | recovery-civics **Q-11**; arch-FGH **FGH-OWED-11** |
| **Q-37** | **Is there a RULER, and does succession exist as a mechanism?** `NotableRow` carries identity, settlement, allegiance, cohort and the person — **and no office** — while D-037 C1 lists *"dynastic or legal inheritance"* as a claim source. | recovery-civics **Q-12** |

## Q.4 — BLOCKS A PACKET OR A MECHANISM

| # | question | asked by |
|---|---|---|
| **Q-38** | **Does the disaster's magnitude derive from a hazard catalogue, or from the effective buffer it must exhaust?** The answer determines whether a hazard/vulnerability split is even well-defined, and **whether improving storage makes hazards larger**. | **P-22**; arch-JKL **DQ-K1** |
| **Q-39** | **May a technology reduce a settlement's EXPOSURE (its hazard rate), as opposed to its vulnerability — and if so, what bounds it away from zero?** I.e. is *"technology must not simply erase disasters"* a statement about **severity** only, or about **frequency** too? | arch-JKL **DQ-K5** |
| **Q-40** | **May there be a second disaster kind, and may a kind touch STORES rather than production?** A flood is the natural first kind and a granary loss the natural first store-touching effect; *"granary loss"* is currently **NOT AUTHORIZED**, with its price stated (a per-row physics predicate plus a kind registry). **Should disasters be spatially correlated**, so a region rather than a settlement fails? | recovery-food **Q-11, Q-12**; arch-JKL **DQ-K2** |
| **Q-41** | **Is *"Exit is the only open valve under famine"* the intended architecture, or an artefact of the missing food-trade capability?** D-021 requires at least one channel always open; **with no food trade and no institutions, Exit is the only one — a structural fact rather than a design choice anyone made.** | arch-JKL **DQ-K4** |
| **Q-42** | **Are grain, livestock and fish materially equivalent Sustenance inventories, or materially different foods? Should livestock and fish be storable, perishable or bounded at all, and by what carrier?** MEASURED: `GoodEntry` has **no** perishability, shelf-life, capacity or storability field, so the data layer cannot express the answer today. **May a food good ever be traded?** | recovery-food **Q-01, Q-02, Q-03**; arch-JKL **DQ-J3, DQ-J5**; audit **R11** |
| **Q-43** | **Should the granary's carrier be made real** — should the capacity coefficient read the `StructureRow` count the player already builds — **given that doing so moves the binding constraint of the whole food economy and meets the quarantined Malthus corridors?** And **which of the five store terms T1…T5 is the intended home of "storage technology"?** | **P-23, P-24**; arch-JKL **DQ-J1, DQ-J2**; arch-E **Q-E11** |
| **Q-44** | **Is SEED CORN a mechanism the project wants** — *"a reservation, not a loss… it makes a bad year compound into the next one"* — given it would be the **first food mechanism with memory across turns** and the first food loop needing its own D-021 brake? **Is ~55 % destruction of every harvest the intended equilibrium of a bounded store? Should the granary cap be counter-cyclical? Should the founding endowment be capped at the ceiling?** | recovery-food **Q-04, Q-05, Q-07, Q-08**; arch-JKL **DQ-J4** |
| **Q-45** | **What does the expectation baseline drift toward** — instantaneous satisfaction, a smoothed record, a class adoption fraction, or generation turnover? **Must basket composition become computed state, and if so what preserves the ratified invariant that a class's food entries sum to exactly 1.0 by construction?** | arch-JKL **DQ-L2, DQ-L3** |
| **Q-46** | **Which milestone gives needs the supply curves that habituation and rising expectations need?** Three ratified D-018 §4 mechanisms — rising expectations, the habituation ratchet, relative deprivation — **none implemented**, and the code defers habituation to *"the milestone that gives needs supply curves to habituate to"* **without naming it**. **And if rising expectations lands before M8, what discharges its D-021 obligation?** | recovery-food **Q-18**; arch-JKL **DQ-L5**; **P-29** |
| **Q-47** | **Must something continuous sit between the conditions and the predicate?** Without a holding-like quantity, a breakthrough architecture degenerates to *"a capability predicate flips when its conjuncts align"* — which is legal and is D-040 B3 exactly, but **cannot represent inquiry that was attempted and failed.** **Does the Director want attempted-and-failed inquiry to be representable?** | arch-E **Q-E1** |
| **Q-48** | **Should a breakthrough's arrival be stochastic at all?** The architecture is indifferent: a deterministic threshold crossing and an exponential arrival differ only in whether two identical worlds diverge. **And what is a breakthrough's temporal granularity under the 10-year atomic turn**, given the D-020 seam's one-turn lag and D-042 §11's ban on an ad-hoc mid-turn pause? | arch-E **Q-E2, Q-E7**; recovery **G-12**; arch-C **Q-C9** |
| **Q-49** | **What is the fence on a PRESSURE TERM, and must an arrival hazard only read pressure for constraints it can relieve?** The D-021 pairing depends on the second: *"if λ is driven by a constraint the arrival cannot relieve, the negative valve never engages and **the loop is unpaired**."* **And does publishing how hard a constraint binds hardwire the Malthusian trap that CR-003 §5.1 rules must EMERGE?** | arch-E **Q-E3, Q-E8, X-E1** |
| **Q-50** | **What is the RNG keying for a per-(holder × corpus region) draw?** MEASURED: the shipped registry key is `(SystemId, RegionId)` with **one 32-bit id channel** (`RngRegistry.cs:68-69`), and every shipped consumer keys by settlement. A two-id draw needs either **a packing convention that becomes part of the determinism contract**, or a registry change. | arch-E **Q-E4** |
| **Q-51** | **Is the latch-vs-pure-derived choice a RULE, and is *"does flipping move a conserved stock"* the criterion?** The tree ships two consumers with opposite storage answers and **no document says which a third should copy** — and the record that proposes the criterion is itself **UNVERIFIED**. | arch-D **Q8** |
| **Q-52** | **May a capability be declared irreversible, and by what authority?** The shipped mechanism makes irreversibility a **data choice** — an omitted `recede` clause, one line in a data file, needing no review. **Is that a tuning decision (LIVING, no procedure) or a design decision needing a ruling each time?** | **P-18**; arch-D **Q5** |
| **Q-53** | **Is "partial capability" a concept this project has?** If a capability may be held in degree rather than in extent, does degree enter as a **coefficient inside the domain's resolution equation**, or is a capability strictly boolean-per-place? | arch-D **Q6** |
| **Q-54** | **Does the D-020 grammar stay CLOSED as the seam takes on capability work?** It ships *"No functions, no arithmetic (v1 — queue if needed)"*, so **every quantity a capability needs must be published by the system that owns it — a real cost paid by real packets.** Does that constraint hold, or does capability work reopen D-020? | arch-D **Q9** |
| **Q-55** | **What does an Empire-scoped conjunct evaluate to for a settlement under no polity's control** — false, an error at evaluation, or is the situation to be made unreachable by construction? **Nothing rules on it, and the choice has turn-visible semantics.** | arch-D **Q2** |
| **Q-56** | **Is a holding's firmness one value or two?** With one, *"capability retained by practice, theory lost"* is **not representable**. With two, it is — at the cost of a schema decision no design lane made. | arch-C **Q-C7** |
| **Q-57** | **Which climate substrate** — a closed-form quasi-periodic driver with no state and an exact turn integral, or a Markov regime chain with serialized modes and micro-stepped aggregation? They differ in schema cost, **in how much `libm` enters the determinism surface**, and in whether climate is in principle forecastable. **And what is the turn-aggregation rule** (which is G1's answer restated)? **And what timescale do the persistent regional states have?** | arch-I **Q1, Q2, Q8** |
| **Q-58** | **May happiness carry an environmental factor the frozen needs ladder does not have?** Taking the factor route is open **without reopening D-018**, and makes happiness and the needs ladder asymmetric. | arch-I **Q6** |
| **Q-59** | **Is irrigation in scope, and does it move the world outside its own derived reference class?** MEASURED: `grep -i "irrigat"` returns **only** *"rain-fed cereal agriculture without irrigation or modern inputs"* as the σ and yield **reference class** — *"Irrigation is the reference class's exclusion, not a recorded decision about the game."* If it is in scope, **does it re-open the derivations of `sigmaLogYield` = 0.2936 and 26.0, and at which milestone does that audit run?** | recovery-climate **gap 10**; arch-E **Q-E10** |
| **Q-60** | **Is the proposed domain roster the right cut?** Five reversible choices, none ruled: **energy** and **sanitation** declined (*"neither has any constraint on this tree to relieve"*); **information/computation** not split from communications; **textiles/hides** and **ceramics/containers** added because the shipped goods imply them. | arch-E **Q-E9** |
| **Q-61** | **Is CLASS an acceptable proxy for know-how in migration?** The bucket key carries class and **nothing about knowing**; using class as the proxy risks collapsing two of the eight concepts D-042 §8.2 rules non-interchangeable. **And is IMITATION a channel in its own right, or the local response after a carrier has arrived?** | arch-FGH **Q-H3, Q-H4** |
| **Q-62** | **Does the Director accept the God-object test as a checkable conformance gate for any future institution packet**, in the form arch-FGH states it or another? **And should an institution abstraction be explicitly forbidden from absorbing an already-shipped mechanism** (markets, public works and union regulation already ship as mechanisms or policies)? **Are COURTS a subject the project intends to model at all?** | arch-FGH **Q-I2, Q-I3, Q-I4** |
| **Q-63** | **If an institution ends up being a published scalar, one variable per institution kind per settlement, or an aggregate** — bearing in mind the registry law that a scale-sensitive predicate **must** publish an absolute quantity? | arch-FGH **Q-I6** |
| **Q-64** | **Does the Director accept the CIRCUMSTANCE TEST as a rule** — three mechanical clauses: every term can fall; the zero branch is reachable or its absence is stated; the two counterfactuals — and if so, **does it bind at spec time, packet time, or verification time?** | arch-E **Q-E6** |
| **Q-65** | **Does the Director accept `arch-M`'s CYCLE RULE as a conformance obligation** — in its two-clause form, where the prev-read break explicitly does **not** discharge the D-021 obligation? **And does it need the third clause, the DERIVATION BREAK?** | **P-54**; arch-M **Q-M1, Q-M2** |
| **Q-66** | **What records same-turn edges now that the interaction matrix does not exist?** Is the `SystemCatalog` shared-table paragraph the intended artefact, and **does a new same-turn edge owe a row somewhere?** | **P-26**; arch-M **Q-M3** |
| **Q-67** | **Should the SINGLE-PUBLISHER shape of the published-variable registry be preserved, split by domain, or ruled on explicitly before the seam is widened?** MEASURED: all four published variables are written by `ClassMobilitySystem`, including a trade quantity and a demographic one. | **P-17**; arch-M **Q-M4** |
| **Q-68** | **Who owns AI research strategy, and under what determinism discipline?** The AI constitution lists *"Technological appetite"* as a personality dimension and bans hidden map knowledge; the placeholder's Q7 requires a deterministic decision procedure free of unordered iteration. **No document connects the two, and the constitution's own status is unasserted.** | recovery **G-13**; recovery-civics **Q-17** (**P-52**) |
| **Q-69** | **What is the order SCOPE field and the PERMISSION MATRIX that M4-B deferred?** *"Deliberately deferred, and still open."* A governance layer that lets a government restrict what the player may order needs both. **Who owns them?** | recovery-civics **Q-16** |
| **Q-70** | **Does the M5 governing loop OWN attachment, or merely host it as a candidate?** D-041 D1 places it at M5 *because it is a governed lever*; D-041 D4 says it is **NOT SCHEDULED** and each milestone's spec proposes its own packet; CR-005 may re-scope M5 entirely. | recovery-civics **Q-19** |
| **Q-71** | **Does recognition carry a degree, a date, or a payload?** The shipped row is two keys and nothing else, **deliberately** — while D-037 C7 makes recognition consequential for alliance logic, casus belli legitimacy, trade access and the legitimacy cost of assertion. **Is recognition binary?** | recovery-civics **Q-15** |
| **Q-72** | **What is "Part 15", and do the master-vision parts still exist as authority?** D-018 cites *"every regime module (Part 15)"* and *"(Part 19)"*; D-037 cites *"Part 16.3 frozen conflicts"*, *"Part 16.7 claims and borders"* and *"(Part 18.3)"*. **No file in this repository contains those parts.** Live authority, superseded by the Spine, or citations outside the tree? | recovery-civics **Q-18** |

## Q.5 — PROCESS AND RECORD-KEEPING QUESTIONS

| # | question | asked by |
|---|---|---|
| **Q-73** | **Is a D-record issued AFTER the M0 freeze itself frozen?** D-021, D-035, D-037, D-038, D-039, D-040, D-041 and D-042 were all ruled later, under the §4 exempt-class mechanism. **What procedure amends one, and does the S8 §3 CR path reach them at all?** **This is load-bearing on P-17: it determines what it would take to revisit the rejected `CapabilitySystem`.** | recovery **G-02** |
| **Q-74** | **What is the amendment procedure for `CLAUDE.md` itself?** It is not in the freeze perimeter, yet it carries the operative law list, the merge loop and the §4.1 pointer. ADR-014 §4 records it being edited under an ADR override; ADR-015 §7.12 records it being corrected on measurement. **Amendment, correction, or neither?** | recovery **G-03** (architecture lane) |
| **Q-75** | **Is `docs/design/` a recognised document class, and where does it sit in the GOV-4 §1 hierarchy?** Twelve documents now live there. S8 §4's exempt classes do not name it and GOV-4 §1 does not rank it. **Agent report (rank 8), or something else?** | recovery **G-13** |
| **Q-76** | **What governs an agent-to-agent handoff and a multi-lane session?** GOV-3 governs one agent and one director; ADR-015 §6 rules one worktree per verifying agent. **Nothing rules how parallel lanes on one branch coordinate, or how their outputs are reconciled when they overlap** — which is exactly what produced **P-30**, where two lanes reached opposite verdicts and neither could touch the other's file. | recovery **G-12** |
| **Q-77** | **Should GOV-3 Parts A and B be written as a numbered ADR-015 section?** They are filed as a **CANDIDATE**, and the record makes the point about itself: *"a queue line does not bind an agent the way a numbered §7.x does."* | **P-55** |
| **Q-78** | **Do the S7 living registers exist as artefacts, and is the open-questions precondition being discharged?** The frozen Spine requires a Decision Log, a Requirement Index, a `TUNE` registry, and an **Open-questions register that *"must be empty before each milestone's task cut."*** **None was located as a file** — and this document is, in effect, that register for M5. | recovery **G-10** |
| **Q-79** | **Do stub twins exist, and does the obligation bind M5+ systems?** *"every system ships a canned-output fake so downstream systems build against stubs"* is frozen Tier-2 protocol; **no stub-twin artefact was located.** | recovery **G-11** |
| **Q-80** | **Is there a rule governing PLACEHOLDER documents?** Two M5 placeholders exist and **both generated open CRs (CR-005, CR-006) simply by being written.** S8 §4 says *"Nothing beyond n+1 is ever written"*, and the exempt classes do not include placeholders. **What class are they, and may more be written?** | recovery **G-09** |

---

## §R CAVEATS ON THIS DOCUMENT

1. **De-duplication is a judgement and it can be wrong in one direction.** Two rows naming the same
   pair of sources were merged; two rows naming *different* sources for the same *subject* were
   kept apart. **A merge that should not have happened would hide a conflict.** Every merged entry
   names every lane that found it, so a reader can reconstruct the originals.
2. **The blast-radius ordering is PROPOSED.** The BR scale in §0 is this lane's, not the
   repository's. The one part of the ordering that is **not** a judgement is the M4-closure
   dependency in §0, which follows from a RATIFIED cadence rule.
3. **Part P mixes evidence classes deliberately.** Some rows are two RATIFIED documents in
   conflict (**P-06**, **P-11**); some are a ratified document against a MEASURED tree fact
   (**P-09**, **P-12**); some are a ratified document against a **relayed, unverified** director
   direction (**P-02**, **P-21**, **P-22**). **The third class is the weakest and is marked as such
   every time it appears.**
4. **Five Part P rows are corrections to prior records rather than live conflicts** — **P-36**,
   **P-41**, **P-48**, **P-49**, **P-63** — and each says so. They are kept because the
   uncorrected version still circulates and will mislead the next reader.
5. **This lane ran no build, no test, no bench and no experiment.** Every MEASURED claim is a claim
   about what a file says at `26d12d3`, verified by reading it.
6. **Part Q asks; it does not recommend.** Where a lane attached a recommendation to a question,
   the recommendation was **dropped** in transcription and only the question carried over. The
   recommendations remain readable in the originating lane's own document.

**WHAT THIS DOCUMENT DID NOT DO.** No conflict was resolved, reconciled, closed or sided with. No
question was answered. No status was upgraded or downgraded from what its source says. No CR was
opened or closed. No ruling was made, proposed as ratified, or implied. No production code, schema,
data file, golden, corridor, quarantine, test or gate script was touched. No existing document was
edited. No milestone was moved.
