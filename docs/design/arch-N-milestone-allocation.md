# ARCH-N — MILESTONE ALLOCATION (mandate Part 19)

**DESIGN, NOT IMPLEMENTATION.** This document allocates proposed systems to milestones on paper.
It implements nothing, changes no schema, writes no M5 code, merges nothing, and edits no existing
document. **It does not renumber the ladder, resequence anything, or close CR-005.** Created under
the Director's 2026-09-19 mandate as OUTPUT N of the synthesis lane.

**Tree pinned for every citation:** branch `claude/civdemo-work-b1z2y4` @ `26d12d3`, working tree
clean; `origin/main` = `dbef61a`.

**Author authority: none.** The DIRECTOR IS ChatGPT. **The milestone ladder ORDER and each
milestone's exit-criteria DEFINITIONS are FROZEN** (`docs/spine-s8-governance-freeze.md:17`).
Nothing below moves a milestone. Every allocation here is a **PROPOSED reading of where already-
ratified material puts a system**, or an explicit statement that the tree does not say.

**Label key:** RATIFIED (cite file:line) · MEASURED (cite record + tree) · PROPOSED · INFERRED ·
DIRECTOR DECISION REQUIRED.

---

## §0 THE FENCE THIS DOCUMENT OPERATES INSIDE

| # | fence | source |
|---|---|---|
| **N-F1** | **M4 REMAINS CLOSED. Nothing is allocated to it.** *"No AutoResolver and no armies … **No money, no treasury, no taxation. No research, technology or institutions** — CR-005 places them in M5 and remains open without blocking M4."* | `docs/milestones.md:324-338` — **RATIFIED** |
| **N-F2** | **Documentation exists at most one milestone ahead of implementation. Nothing beyond n+1 is ever written.** *"`implement M(n) → exit criteria GREEN → write + ratify M(n+1) spec → cut packets`"* … *"The **only** system-spec documents in existence at any moment: the one being implemented, and — after proof — the next one."* | `docs/spine-s8-governance-freeze.md:44`, `:47`; `CLAUDE.md:28` — **RATIFIED** |
| **N-F3** | **A system proves itself in code before any later system is specified.** | `docs/spine-s8-governance-freeze.md:4-7` — **RATIFIED** |
| **N-F4** | **Do not pull a system forward because it has now been designed.** A design document is not an allocation. This lane treats every sibling lane's architecture as *material for whichever milestone already owns it*, never as a claim on M5. | **PROPOSED**, stated as this lane's own discipline, derived from N-F2/N-F3 |
| **N-F5** | **The M5 STARTUP FENCE — eight items that do not reopen at M5 without a fresh ruling.** Migration is not an M5 tuning target · the Malthus corridor stays quarantined · happiness stays derived · **D-021 stays as-is** · identity stays `PolityId` + `ControlRow` · the uncontrolled-settlement path stays legitimate · **the 10-year atomic turn stays** (*"No annual substeps, no mid-turn handbacks, no order-timing changes. CR-006 remains open"*) · do not fabricate a second polity to activate foreign trade. | `docs/m4-exit-inventory.md:302-324` (§12) — **RATIFIED** |
| **N-F6** | **Every milestone spec from M4 onward carries the four S8 §4.1 items** — FOUNDATIONS AUDIT as packet one, dimensional declaration, corridor independence, coupling map. | `docs/spine-s8-governance-freeze.md:51`, `:68-71`; ADR-014 — **RATIFIED** |

---

## §1 WHAT THE LADDER ALREADY RATIFIES

**Two ladders exist on this tree and they differ.** Both are recorded; neither is chosen here.

**Ladder A — the frozen Spine** (`docs/civ-sim-architecture-v3-outline.md:104-115`), RATIFIED and
inside the S8 §1 freeze perimeter:

> M4 Trade + first conflict · **M5 The governing loop** (*"Taxation, budget, authority/bandwidth
> economy, laws-lite, legitimacy. **'It's a game now' checkpoint — evaluate fun honestly here
> before proceeding.**"*) · **M6 Knowledge & divergence** (*"Domain lattice lite, diffusion,
> computed era labels"*) · **M7 Politics & diplomacy** (*"Institutions as modules, regime change,
> coups/revolts"*) · **M8 Society layer** (religion, culture, opinion, disease) · M9 Ancient
> Vertical Slice · M10+ Era expansions.

**Ladder B — D-011 §6's resequence** (`docs/d011-battle-layer-addendum.md:54-66`), RATIFIED and
equally inside the freeze perimeter (`docs/spine-s8-governance-freeze.md:16`):

> M4 trade + strategic war, AutoResolver only · M5 governing loop · **M6 Battle Layer v1** ·
> **M7 knowledge & divergence** (*was M6*) · **M8 politics & diplomacy** (*was M7*) · **M9 society
> layer** (*was M8*) · **M10 Ancient Vertical Slice** · M11+ era expansions.

**The Spine's per-system inventory table** (`:71-96`) additionally assigns, in Ladder-A numbering:
State/taxation/authority **M5** · Legitimacy & opinion **M5** · Knowledge & diffusion **M6** ·
Politics deep (institutions, regime change) **M7** · Diplomacy **M7** · Religion & culture **M8** ·
Health & disease **M8** · **Environment & climate M9** (*"degradation stocks close loops"*) ·
Military full **M9+** · Espionage **M10+** · Characters/notables **M10+**.

**The reconciliation of A and B was REQUIRED and has not been performed.** GOV-2 §1c verifies the
inventory table *"stale by one milestone from M6 onward"*, row by row, and records that the table
*"was never patched"*; two rows — **Environment & climate** and **Military full** — it marks
**AMBIGUOUS**, because D-011 §6 does not name them at all
(`docs/m4-pre-spec-dependencies.md:143-160`). **MEASURED** — a claim about what that filing
records; the filing's own ratification status is separately contested (arch-PQ **P-36**).
`docs/milestones.md:326-328` dissolves the conflict **in one direction only**, for one line,
invoking GOV-4 §1 rank 2. The general question is recorded unanswered as recovery-lane **C-04 /
G-07**.

**Three other ratified placements bear directly on the allocation:**

| # | placement | source |
|---|---|---|
| **N-P1** | **GOV-2 §1a, RULED:** *"M5 taxes in kind. **Money is NOT folded into M5** and does NOT get an inserted milestone before it … money then arrives as an institution that **EMERGES**"* — law 4, computed state, never a date. | `docs/m4-pre-spec-dependencies.md:23-41` — **RATIFIED** (director ruling, first recorded there) |
| **N-P2** | **CR-005 is OPEN.** The director directed that *"M5 own a major 'Research, Technology & Institutions' architecture packet"*, on an ordering-dependency argument; three ratified places say otherwise. Three options on record (RENUMBER / INSERT / an architecture-only packet inside M5); recommendation Option C; **awaiting ruling.** | `docs/adr/cr-005-m5-research-technology-institutions-placement.md:3`, `:36-79`, `:140-155` — **OPEN** |
| **N-P3** | **D-040 uses Ladder B.** *"D-040 (2026-08-08) uses this resequence … D3/D4 place map extent and discovery at **M7 (knowledge)** and the political consequences of weak control at **M8 (politics)**."* | `docs/d011-battle-layer-addendum.md:49-54` — **RATIFIED** |

### 1.1 A MEASURED correction to the recovered record, recorded because it changes what is missing

**Recovery-lane C-02 (architecture) records:** *"GOV-2 IS CITED AS A RATIFIED SOURCE AND EXISTS ON
NO REF … `docs/gov-2*` exists on **no ref**."*

**MEASURED, this lane, `26d12d3`:** the **filename** claim is correct — `git log --all
--diff-filter=A -- 'docs/gov-2*'` returns nothing, on any ref. **But the GOV-2 filing's CONTENT is
on the tree, and on `origin/main`, under a different name:** `docs/m4-pre-spec-dependencies.md`,
whose title line reads **"# M4 — PRE-SPEC DEPENDENCY FILING (GOV-2)"** (`:1`) and whose §1a, §1b,
§1c and §6 match every GOV-2 citation in `docs/m4-spec.md:35-37` clause for clause: §1a money in
kind (`:23-41`), §1b notables split by role (`:100-115`), §1c the stale inventory (`:143-160`), §6
*"Director ruling … M4 STAYS WHOLE rather than splitting"* (`:460-463`). The file **is** on
`origin/main` (verified by `git cat-file -e origin/main:docs/m4-pre-spec-dependencies.md`).

**What this settles and what it does not.** It settles that GOV-2's rulings are readable and
citable on merged truth, which removes the "rests on a document that cannot be read" half of
C-02. It does **not** settle three things, and this lane rules on none of them:

1. **GOV-1 is still unlocated.** It is cited at `docs/gov-3-execution-protocol.md:153`, in
   `docs/queue.md` at seven places, and at `docs/t3.9b-spec.md:4`. MEASURED: no file on any ref.
2. **The document calls itself unratified.** `:14` — *"This file is unmerged and unratified; the
   in-place amendment is by the extension's explicit instruction."* That sentence is **stale as to
   "unmerged"** (MEASURED: it is on `origin/main`) and **unretracted as to "unratified"** — while
   `docs/m4-spec.md:35-37` cites three of its sections as the ratified sources of three M4
   decisions. **DIRECTOR DECISION REQUIRED**, and it is carried in OUTPUT P.
3. **Whether a GOV-N record should be re-homed to its own filename** so that a citation resolves
   by search. Recovery-lane **G-04** asks this; this lane adds only the measurement.

---

## §2 THE ALLOCATION

**Reading rule for the table.** "Owner" is the milestone **the ratified record already assigns**,
cited. Where the two ladders disagree the cell shows **both** and marks it. Where the tree assigns
nothing, the cell reads **UNASSIGNED** and that is the finding, not a gap this lane fills.
**Milestone numbers below use Ladder B** (D-011 §6) wherever a number is stated, because GOV-4 §1
rank 2 puts a later accepted addendum above the older Spine and `docs/milestones.md:326-328`
invokes exactly that rule — **but that is this lane's reading convention, not a ruling, and the
underlying reconciliation remains unperformed.**

### 2.1 M4 — CLOSED

**Nothing is allocated to M4.** N-F1. The five items M4 explicitly did not deliver stay
undelivered, and the M4 known-open list (items 1–11) stays open where it is.

### 2.2 M5 — the governing loop

| system / capability | basis for M5 | status |
|---|---|---|
| **Taxation in kind, and the polity-scoped destination stock it requires** | Spine `:82`, `:109` (*"State, taxation, authority — M5"*); N-P1 | **RATIFIED placement.** Blocked on **CD-6** (no polity-scoped stock exists) — recovery **C-03/C-06** |
| **Legitimacy & opinion** | Spine `:83` (*"M5 … mechanism-only, no mood auras"*) | **RATIFIED placement; the quantity is undefined** — recovery **Q-03** |
| **Authority / bandwidth economy; laws-lite** | Spine `:109` | **RATIFIED placement; both are named once and nowhere else** — recovery **Q-04, Q-05** |
| **The D-021 unrest-lite valves 1, 2, 3, 6, 7** | *"**M5 (unrest-lite):** ships valves 1, 2, 3, 6, 7 *with* the unrest it ships — **the brakes install with the gas pedal, never after.** Ignite-and-burn-out test enters the battery at M5."* | `docs/d021-stability-doctrine.md:48` — **RATIFIED** |
| **Lifting the grievance read-isolation quarantine** | The gate's own text: *"Grievance accrues but ACTS only at M5"* | `scripts/check-read-isolation.sh:5-6` — **RATIFIED** |
| **D-035-C path 6 — Safety-vs-Liberty via an institution** | *"**M5 must build Safety-vs-Liberty this way and no other way**"* | `docs/d035-needs-aggregation.md:86` — **RATIFIED**, and **in conflict** with the Spine/D-011 placement of institutions (recovery **C-11**, arch-FGH **X-1**) |
| **G8(c) / CR-016 option 3 — the per-year food-balance sub-step** | *"It is **M5-SCALE** — the food balance is the largest of the three blast radii — and it is already queued as G8(c) in `docs/queue.md`"* | `docs/adr/cr-016-armed-disaster-fallout.md:308-313` (option 3), quoted verbatim from `:311-312`; the queue entry is `docs/queue.md:1403` (G8(c)) — **MEASURED**. The option is not adopted: CR-016 itself is **OPEN** |
| **Research / Technology / Institutions architecture packet** | The director's direction, recorded in CR-005 §1 | **CR-005 is OPEN.** Option C (an architecture-only packet inside M5) is the CR's recommendation and is **not ruled** |
| **Attachment (D-041)** | D-041 D1 places it at M5 *"the governing loop"*; D-041 D4 says it is **NOT SCHEDULED** | `docs/d041-attachment.md:76-78`, `:104` — **AMBIGUOUS**, recovery **Q-19** |

### 2.3 M6 — Battle Layer v1

| system | basis | status |
|---|---|---|
| **TacticalResolver, ancient units, parity suite** | `docs/d011-battle-layer-addendum.md:56` | **RATIFIED** |
| **D-039 command friction, reconnaissance, siege** | `docs/m4-spec.md:51` — *"**D-039 IS M6, WITH ONE EXCEPTION** … only Part B's investment mechanism touches M4"* | **RATIFIED** |
| **D-037 E3 occupation-without-legitimacy grievance** | `docs/d037-emergent-polities.md:182-186` | **RATIFIED**, and its natural brake (Endurance) is an M8 valve — arch-FGH **X-9**, cycle **C-16** |
| **The visual milestone** | *"**THE VISUAL MILESTONE SITS BETWEEN M5 AND M6** (D-038 E1)"* | `docs/m4-spec.md:50` — **RATIFIED** |

### 2.4 M7 — knowledge & divergence

| system | basis | status |
|---|---|---|
| **Knowledge: domain lattice lite, diffusion, computed era labels** | Ladder B `:62` (*"was M6"*); Ladder A `:84`, `:110` says **M6** | **CONFLICT — see §3** |
| **The CORPUS / HOLDING / PRACTICE registers** (arch-C) | follows knowledge | **PROPOSED**, allocated to whichever milestone owns knowledge |
| **Breakthrough arrival, domain roster** (arch-E) | follows knowledge | **PROPOSED** |
| **Technology / capability scope widening** (arch-D) | follows knowledge | **PROPOSED**; D-042 §8.5 explicitly defers the design (`:159-160`) |
| **Map extent and discovery (D-040 D3)** | *"D3/D4 place map extent and discovery at **M7 (knowledge)**"* | `docs/d011-battle-layer-addendum.md:51-54` — **RATIFIED**, Ladder B numbering |
| **Literacy / education as published quantities** | ratified dependants exist (D-018 `:26`, `:42`, `:48`, `:63`; D-021 `:39`, `:43`; D-039 A5) with **no variable** | **UNASSIGNED — recovery Q-09, and it is the sharpest scheduling hole in the tree** |

### 2.5 M8 — politics & diplomacy

| system | basis | status |
|---|---|---|
| **Institutions as modules, regime change, coups/revolts** | Ladder B `:63` (*"was M7"*); Ladder A `:85`, `:111` says **M7** | **CONFLICT with D-035-C path 6's M5 binding** — recovery **C-11**, arch-FGH **X-1** |
| **Diplomacy; interest-computed stances; clause treaties** | Ladder B `:63`; Ladder A `:86` | **RATIFIED placement** |
| **Political consequences of weak control (D-040 D4)** | `docs/d011-battle-layer-addendum.md:51-54` | **RATIFIED**, Ladder B |
| **D-021 valves 4 (endurance), 5 (organization decay), 8 (generational decay); movements, organization, leadership; the D-019 matrix** | `docs/d021-stability-doctrine.md:49` | **RATIFIED** |
| **Political notables** | GOV-2 §1b: *"M8 adds political notables and movement leadership"* | `docs/m4-pre-spec-dependencies.md:100-108` — **RATIFIED** |
| **Cultural plurality — and R-3 bites here** | *"cultural plurality (M8/M9), which is where R-3 bites"* | `docs/m4-spec.md:57`; R-3 `:465-508` — **RATIFIED** |

### 2.6 M9 — society layer

| system | basis | status |
|---|---|---|
| **Religion, culture, opinion** | Ladder B `:57` (*"was M8"*) | **RATIFIED placement** |
| **Health & disease (SIR on the trade network; sub-step rule)** | Ladder A `:88` → M9 under GOV-2 §1c | **RATIFIED placement.** It is the missing substrate for **CD-7** (pollution → mortality, arch-I **B3/O2**) and for arch-E domains 9/10 |
| **First two crisis archetypes fully live (famine, plague)** | Ladder A `:113` | **RATIFIED** |

### 2.7 FUTURE — and the two rows the resequence left AMBIGUOUS

| system | basis | status |
|---|---|---|
| **Environment & climate** (*"degradation stocks close loops"*) | Spine `:89` says **M9** in Ladder-A numbering; **D-011 §6 does not name it**, and GOV-2 §1c marks the row **AMBIGUOUS between M10 slice content and M11+ era expansions** | **UNRESOLVED.** arch-I **Q9** asks the same question: *"Is the M9 placement of environment & climate still the intent?"* — *"the only source is the pre-M0 Spine table, and nothing since restates it"* |
| **Military full (ops, siege, naval)** | Spine `:90` M9+; **D-011 §6 does not name it**; §5 keeps naval auto-resolve *"until post-slice"* | **AMBIGUOUS** — GOV-2 §1c, finding F3 |
| **Espionage / intel uncertainty** | Spine `:91` M10+; *"'M10+' does NOT read the same under both schemes"* | **AMBIGUOUS** — GOV-2 §1c, finding F4 |
| **Money as an institution that EMERGES** | N-P1: not M5, not an inserted milestone, *"a real milestone later in the ladder"* — **which milestone is not named** | **UNASSIGNED. DIRECTOR DECISION REQUIRED** (CR-008, which exists only on `m5-full-build`) |
| **Characters/notables residual layer · Finance · Media/nationalism** | Spine `:92-94` places them late — Characters/notables **M10+**, Finance and Media/nationalism **era expansion** | **RATIFIED** |
| **R-3 — the clone architecture** | *"R-3 IS THE LARGEST UNSCHEDULED ITEM IN THE PROJECT, AND IT BITES AT M8/M9"*; scheduled as T4.16; ADR-020 **awaiting a ruling** | `docs/m4-spec.md:465-508`; `docs/milestones.md:363` — **OPEN** |

### 2.8 Systems this lane declines to allocate at all

| system | why |
|---|---|
| **Pollution · Environmental quality · Climate substrate** | Their only ratified home is the AMBIGUOUS Spine row above. Allocating them would be picking a side in an unreconciled ladder conflict. **arch-I Q9.** |
| **Institutions** | Three ratified placements (M5 by D-035-C, M7 by the Spine, M8 by D-011) and **≥6 meanings with zero code**. Allocating a thing whose definition is unruled allocates nothing. **CR-010 was recommended and never written.** |
| **Technology, as an object** | *"nothing in the tree says what a technology IS"* (recovery **G-14**). arch-D and arch-E both write their designs **without needing the object**, which is itself evidence about the answer. |
| **Everything in the six sibling architecture documents** | N-F4. A design is not an allocation. Each design belongs to whichever milestone the ladder already gives its subject. |

---

## §3 THE RECOVERED CONFLICT ABOUT WHICH MILESTONE OWNS KNOWLEDGE — SURFACED, NOT RESOLVED

**Source A, RATIFIED and FROZEN:** *"Knowledge & diffusion | **M6**"* and *"**M6 — Knowledge &
divergence.** Domain lattice lite, diffusion, computed era labels"* —
`docs/civ-sim-architecture-v3-outline.md:84`, `:110`. Inside the S8 §1 freeze perimeter
(`docs/spine-s8-governance-freeze.md:14`, `:17`).

**Source B, RATIFIED and equally FROZEN:** *"| **M7** | knowledge & divergence | **was M6** |"* —
`docs/d011-battle-layer-addendum.md:62`. D-011 is named in the same freeze perimeter (`:16`).

**Source C, MEASURED (a recorded finding):** GOV-2 §1c calls the Spine row **"stale by one"** and
states that reconciliation **"remains REQUIRED"** — `docs/m4-pre-spec-dependencies.md:143-153`.
**MEASURED, this lane: the reconciliation has not been performed.** The Spine table is unedited at
`26d12d3`.

**Source D:** `docs/capability-architecture-decision.md:378` lists the same disagreement as MINOR
conflict #9, *"known-stale"* — **SECONDARY EVIDENCE** (a prior agent's report), and that document
carries a correction notice falsifying two of its own claims.

**What each side implies.**

- **If M6 (Ladder A):** knowledge precedes the battle layer, and D-011's own resequence is
  partially inoperative — which cannot be right, since D-011 §6 *is* the resequence.
- **If M7 (Ladder B):** knowledge follows the battle layer. **INFERRED:** this is the reading every
  document written after 2026-08-08 uses, including D-040 (N-P3), and it is the reading this
  document's §2 adopts as a convention.
- **Either way**, CR-005 sits on top of both, because it would put an architecture-only
  Research/Technology/Institutions packet **inside M5**, ahead of whichever number knowledge
  carries.

**What is blocked until it is ruled.** N-F2 limits what may be written to the current milestone
plus one. **INFERRED, and it is the operative consequence:** until the ladder question and CR-005
are both ruled, it is not determinable from the tree alone whether a knowledge spec is n+1
(writable after the M5 exit gate) or n+3 (not writable at all). **Every sibling architecture lane
records this same blocker** — arch-C **X-07/Q-C18**, arch-D, arch-E **X-E7**, arch-FGH **X-1** —
and none of them resolves it. **DIRECTOR DECISION REQUIRED.**

**This lane does not choose, and records that choosing is not a design act.** Two frozen documents
give different numbers to the same content; S8 §3's Contradiction-Report path exists for exactly
that, and `docs/milestones.md:326-328` has already used GOV-4 §1 to dissolve **one line** of it
while explicitly leaving the general question open.

---

## §4 THE MANDATE'S REAL QUESTION — THE MINIMUM ARCHITECTURE M5 NEEDS SO M7/M8 ARE NOT BOXED IN

**This is the section the Director acts on. It is deliberately short.**

**The test each item had to pass to be on this list**, stated so items can be argued off it:
*(i)* M5 needs it **for M5's own ratified content** (taxation, legitimacy, the unrest valves), or
*(ii)* not having it at M5 makes a later milestone's ratified content **more expensive than
building it now**, on the D-037 A3 precedent — *"This is LOAD-BEARING and must be in the M4 data
model from day one; **retrofitting it later is prohibitively expensive**"*
(`docs/d037-emergent-polities.md:22-23`, RATIFIED). **Everything that passes only "it would be
nice" is off the list.**

### THE LIST — five items

| # | item | why M5 cannot avoid it | which later milestone it un-boxes | label |
|---|---|---|---|---|
| **N1** | **A SCOPED PUBLISHED-VARIABLE SUBSTRATE** — a way for a system to publish a named `double` against a **non-settlement key**, and for a D-020 predicate to read it at that scope. | M5's own content needs it twice over, for reasons that have nothing to do with knowledge: **legitimacy** and **authority** are Empire quantities, and D-042 §8.3 **already ratifies** that *"Capability evaluation must be able to distinguish SCOPE — Empire-level and Settlement-level"* (`:155-156`). MEASURED: `VariableRow` is `(SettlementId, VarId, Value)` (`Sim.Core/State/WorldState.cs:394`). | **M7 knowledge is Empire-scoped by ruling** (D-042 §9.2). Without this, *"knowledge that belongs to a civilization has nowhere to live"* (arch-C §6), and **the blocker is in the READING as much as the storing**: a D-020 predicate can only read a *published* variable — no functions, no arithmetic (`Predicate.cs:10-27`). | **DIRECTOR DECISION REQUIRED.** arch-D **Q1** states the two shapes (a second polity-keyed row type vs a scope discriminant on `VariableRow`) and notes the second changes a serialization contract **inside the M0 freeze perimeter**, so it is a CR, not a packet. |
| **N2** | **A POLITY-SCOPED CONSERVED STOCK** — one destination endpoint for an in-kind `Ledger.Transfer`. | **Law 1 leaves no alternative.** *"an in-kind tax has three possible destinations, and two are absurd: a **Flow sink destroys the grain** … and **a budget you cannot spend is not a budget**. The remaining option is a `Transfer` into a **polity-scoped stock that does not exist**"* (`docs/milestone-architecture-governance.md:123-137`, SECONDARY). M5 taxes in kind by ruling (N-P1). MEASURED: `PolityRow` ships with **identity and command source only**. | Everything downstream that spends: stipends, buying off leaders, coup finance, institutional upkeep, garrison upkeep — the **17-line rewrite inventory** GOV-2 §1a enumerates (`:43-72`). | **INFERRED** — law 1 leaves no alternative, and no ruling has been made on the endpoint itself. **The design is DIRECTOR DECISION REQUIRED** — one stock or one per good, per settlement or per Empire, given D-042 §4.3 forbids a single Empire-wide inventory (`:72-74`). |
| **N3** | **A PUBLISHER FOR LITERACY / EDUCATION** — one absolute quantity and, if wanted, its share. | **Not for knowledge's sake.** For M5's own: D-021's ratified education-as-a-flow lever (`:39`, `:43`) and D-018's rising expectations (`:48`) are the **paired feedback** M5 owes on the unrest it ships, and both read literacy. MEASURED: the registry publishes four variables and **none of them is literacy, education, institution or knowledge** (`Variables.cs:93`). | D-018's **Intelligentsia** emergence (`:26`), the **Prospects** need (`:42`), mobility via education access (`:63`), D-039 A5's command capability (`:37-40`) — **four frozen mechanisms whose operands do not exist** (recovery **C-07**). It also un-blocks cycle **C-7**'s only ratified brake. | **INFERRED** that M5 is the cheapest place. The **registry law** binds the shape: *"ANY emergence predicate that needs scale sensitivity MUST publish an **absolute** quantity, not another ratio"* (`Variables.cs:24-32`, RATIFIED). |
| **N4** | **CR-010 — a ruling on what an INSTITUTION is** (not an institution system). | D-035-C path 6 binds M5 **by name**: *"**M5 must build Safety-vs-Liberty this way and no other way**"*, with an institution as the carrier (`docs/d035-needs-aggregation.md:86`, RATIFIED). M5 cannot discharge that clause without knowing what the carrier is. | **CD-2**: ≥6 meanings, zero code, and **four separate sibling lanes owe a carrier that reduces to this one noun** (arch-C OWED-3, arch-D §4.1/4.3/4.6, arch-E OWED-E3, arch-FGH OWED-5, arch-JKL OWED-K3). M8's *"institutions as modules"* is unwritable until it is ruled. | **DIRECTOR DECISION REQUIRED.** **CR-010 was recommended and has never existed on any branch** (`docs/current-state.md:13`). *A ruling, not a system* — this item is a paragraph, not a packet. |
| **N5** | **THE D-021 PAIRING DISCIPLINE, MADE CHECKABLE AT SPEC TIME** — every grievance source in the M5 spec listed with its valve, its amplitude-strengthening brake, and the milestone both land in. | *"the brakes install with the gas pedal, never after"* (`docs/d021-stability-doctrine.md:48`, RATIFIED), and M5 is the first milestone that ships unrest. The **ignite-and-burn-out** test enters the battery at M5 by the same line. | It is the only item on this list that costs **no state at all** and it is the one that prevents the largest class of later damage: **five of the cycles in `arch-M-cross-system-graph.md` §7.6 are FLAGGED for an unpaired or misaligned brake**, not for a topology fault. | **PROPOSED** as a spec-format obligation; arch-FGH §2.6 states it in the checkable form: *"a packet that adds a row to the left column and cannot fill the right two columns from mechanisms landing **in its own milestone** has violated it, and the correct response is to **defer the grievance source**, not to ship it and schedule the brake."* |

### What is deliberately NOT on the list

| not on the list | why |
|---|---|
| **Any knowledge, technology, capability, breakthrough or diffusion mechanism** | N-F4. Knowledge is M6 or M7 under an unreconciled ladder; CR-005 is open. **N1 and N3 are the only things M5 owes the knowledge milestone, and both are justified by M5's own content.** |
| **A climate substrate, pollution, environmental quality** | Their ratified home is AMBIGUOUS (§2.7) and the shipped weather is under two open rulings — **G1** and **CR-016** — plus a ruling in force that weather stays *"unclamped and unmodified"* (arch-I **X1**). Building climate at M5 would prejudge all three. |
| **Money, a treasury in currency, prices in coin** | N-P1 rules it out by name. |
| **Any change to the atomic turn, order timing, or migration tuning** | N-F5, the M5 startup fence. **CR-006 remains open.** |
| **A universal capability abstraction of any shape** | F14 / D-042 §7.3. See `arch-O-invariants.md` §II. |
| **Re-deriving or re-banding any quarantined corridor** | *"CR-002 and CR-003 both forbid fitting the instrument to the artifact"* (`docs/milestones.md:357`). |

### One sequencing observation, offered and not decided

**INFERRED.** N1 and N2 are the same shape of question asked by two different milestones for two
unrelated reasons — *knowledge needs Empire-scoped published state; taxation needs an Empire-scoped
conserved stock*. They are **not the same object** (one is a published `double`, the other a
conserved `long` behind `Ledger`), and this lane does **not** propose unifying them. arch-C §6
makes the same observation and draws the same line: *"whoever designs one should know the other is
waiting. Stated as an observation; the sequencing is the Director's."*

---

## §5 CONFLICTS THIS DOCUMENT SURFACES

Both sources named; nothing reconciled. Consolidated, de-duplicated and ordered by blast radius in
`docs/design/arch-PQ-conflicts-and-questions.md`.

- **X-N1 — Which milestone owns knowledge.** §3. Two frozen documents, a required-and-unperformed
  reconciliation, and CR-005 open on top. **DIRECTOR DECISION REQUIRED.**
- **X-N2 — Where institutions live.** D-035-C path 6 binds **M5 by name**; the Spine says **M7**;
  D-011 §6 says **M8**; CR-005 would say **M5**. Compatible only if "institution" means two
  different things in two places — which is **X-N3**. Recovery **C-04/C-11**, arch-FGH **X-1**.
- **X-N3 — What an institution is.** ≥6 recorded meanings, two *"load-bearing and mutually
  incompatible in kind"*, zero code. **CR-010 recommended, never written.**
- **X-N4 — GOV-2's status.** Its rulings are cited as RATIFIED by `docs/m4-spec.md:35-37`; the
  document carrying them calls itself *"unmerged and unratified"* (`:14`) and is **on
  `origin/main`** (MEASURED, §1.1). **DIRECTOR DECISION REQUIRED** on the document's class.
- **X-N5 — Two ladder rows the resequence left AMBIGUOUS** — Environment & climate, and Military
  full — GOV-2 §1c findings F3/F4. **Environment & climate is the home of an entire sibling
  architecture lane (arch-I), and nobody knows which milestone it is.**
- **X-N6 — M5's exit criteria.** S8 §1 freezes *"each milestone's exit-criteria definitions"*, and
  M5's is *"'It's a game now' — evaluate fun honestly here before proceeding."* **How that is
  discharged, and by whom, is recorded nowhere** — recovery **G-08**. Adjacent and sharper:
  **no calibration corridor for knowledge or diffusion is named anywhere in the tree**, and *"a
  milestone that cannot be calibrated is a milestone that cannot pass its own exit criteria"*
  (recovery knowledge-lane **G-08**).

---

## §6 CAVEATS

1. **Milestone numbers in §2 use Ladder B as a reading convention, not as a ruling.** Every cell
   that depends on the choice says so. If the Director rules Ladder A, the §2 headings shift by
   one from M6 onward and **nothing in §4 changes** — the five-item list is deliberately written
   so it does not depend on the ladder answer.
2. **No sibling architecture lane's design was allocated to M5 merely because it now exists.**
   N-F4 was applied to all six, and §2.8 records the four cases where this lane declined to
   allocate at all.
3. **This lane ran no test, build, bench or measurement beyond reading the tree at `26d12d3`.**
   Every MEASURED claim is a claim about what a file says.
4. **The five-item list in §4 is PROPOSED and is an argument, not an inventory.** Each item states
   the test it passed; an item can be argued off the list by attacking that test.

**WHAT THIS DOCUMENT DID NOT DO.** No milestone was moved, renumbered, resequenced or given new
content. No conflict was reconciled. No CR was opened, closed or answered. No status was changed.
No system was designed, scheduled or authorised. No production code, schema, data file, golden,
corridor, quarantine or test was touched. No existing document was edited.
