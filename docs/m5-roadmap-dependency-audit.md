# MILESTONE ROADMAP — DEPENDENCY AUDIT AND PROPOSED REORDERING

**DESIGN DOCUMENT. Nothing implemented, nothing certified, no frozen document
edited.** Companion records: `docs/m5-research-technology-institutions-placeholder.md`
(scope), `docs/m5-temporal-control-and-player-agency-placeholder.md` (temporal),
`docs/adr/cr-005-…` (M5 ownership conflict), `docs/adr/cr-006-…` (temporal/epoch
conflicts).

**Read §0 first. Two ratified findings change the premise of the reordering
request, and one of them changes what M5 can be at all.**

---

## §0 TWO FINDINGS THAT MUST LAND BEFORE ANY REORDERING IS DISCUSSED

### 0.1 The milestone order is NOT what the request assumes — it was already resequenced

`docs/d011-battle-layer-addendum.md` §6 (ratified, and explicitly *used* by D-040
on 2026-08-08) inserted the Battle Layer as M6 and pushed everything down:

| M | content | note |
|---|---|---|
| M0–M3 | kernel → skeleton → Malthus → markets | unchanged |
| **M4** | trade + **strategic war, AutoResolver only** | armies, supply, attrition on the world map |
| **M5** | governing loop | **"it's a game now" checkpoint** |
| **M6** | **Battle Layer v1** | TacticalResolver, ancient units, parity suite |
| **M7** | **knowledge & divergence** | *was M6* |
| **M8** | politics & diplomacy | *was M7* |
| **M9** | society layer | *was M8* |
| **M10** | **Ancient Vertical Slice** | **go/no-go gate for era expansion** |
| M11+ | era expansions | each adds battle-layer units/verbs as data |

**Consequences for the proposed order (M5 knowledge → M6 governance → M7
diplomacy → M8 warfare):**

- **Warfare is not downstream — it is M4, and it is already built.** `T4.8`
  (strategic war + AutoResolver + notables-as-generals) has shipped. The Spine
  states the intent plainly: *"conflict is core to DEV's stated taste; the game
  must show its teeth early."* **"M8 = Warfare" is not a reordering; it would be
  a removal of shipped work.**
- The requirement actually being asked for — *technology must precede the parts
  of warfare that depend on technological capability* — concerns **Military full
  (ops, siege, naval), which the Spine puts at M9+**, and era-expansion warfare
  at M11+. **Knowledge at M7 already precedes both.** The stated sequencing goal
  is, on this axis, *already satisfied by the ratified order.*
- The genuine question is therefore **not** "does knowledge precede warfare" but
  **"does knowledge precede the ECONOMY and GOVERNANCE that depend on it"** —
  and there the answer is **no**, which §0.2 and §2 develop.

### 0.2 D-040 B3 — "NO TECHNOLOGY UNLOCK. LAW 4 BINDS." — is already ratified, and it governs M5's SHAPE

This is the single most important thing in this document.

`docs/d040-discovery-and-control.md` B3 records a **director ruling** that the
Civ-style *"research X to unlock capability Y"* model is **REJECTED**:

> **"NO TECHNOLOGY UNLOCK. LAW 4 BINDS.** The director raised Civ's *'research
> sailing to cross to another continent'* as the reference and it is **REJECTED
> in that form**: a tech-tree node opening sea travel is **a calendar gate
> wearing a tree**."

The sanctioned shape is stated in the same ruling:

> **"The computed version: sea travel becomes possible when the conditions for
> boats exist** — a coastal settlement, timber, and craft capacity — **in the
> same shape as class emergence**, where artisans emerge on food surplus AND
> market extent rather than on a date. **A landlocked polity never develops it; a
> coastal one does; nobody schedules either.**"

The Spine's interaction matrix says the same thing twice over for this system:
**"Knowledge & diffusion | M6 | T2 | no tree; domain lattice lite."**

**What this means for the request.** The instinct in the brief — *"technology is
not merely a collection of unlockable bonuses"* — is not merely compatible with
project law; **project law already goes further.** It has rejected the *unlock*
shape itself, not just the *bonus* shape. Concretely:

- ✅ *"discoveries that can occur rapidly when the required conditions exist"* —
  **this is exactly D-040 B3's shape** and is already law.
- ✅ *"no Civ-style assumption that a eureka means waiting several turns"* — same.
- ✅ *"different civilizations reach similar capabilities through different
  historical paths"* — direct consequence of emergence-on-computed-state.
- ⚠️ *"technologies with prerequisites and dependencies"* — **admissible only as
  computed preconditions** (no boats without coast + timber + craft capacity),
  **not as tree edges** (`Bronze Working` before `Iron Working`). The second form
  is the rejected shape.
- ⚠️ *"science as a research resource"*, *"multiple research projects in
  parallel"* — a **project that completes and then grants a capability is an
  unlock with a progress bar.** Whether accumulating science is compatible with
  B3 at all is **the first question the director must answer** (§8, Q1).

**There is a shipped precedent for the sanctioned shape**, and it should be the
model: `ClassStateRow.Active` is a **hysteresis latch** — *"1 once the class's
emergence predicate has fired"* (`WorldState.cs:356`). Capability is a computed
predicate over state with a latch, not an inventory of purchased nodes.

---

## §1 CURRENT ARCHITECTURE

**Owned per milestone** (Spine Tier-2 ladder as amended by D-011 §6):

- **M0** kernel, determinism harness · **M1** worldgen, skeleton · **M2**
  cohort demography, farming, famine, chronicle-lite · **M3** 12–15 goods,
  settlements, the local-price solver.
- **M4** (in progress) region-graph trade, 3–8 AI neighbours, strategic war with
  AutoResolver, claims/control (D-037), non-state peoples, notables-as-generals.
- **M5** taxation, budget, authority/bandwidth, laws-lite, legitimacy. **Money is
  M5**: `m4-spec.md` defers currency to it **five times by name**, including
  T4.8's notable-purchase deferral (*"payment is money, M5"*).
- **M6** Battle Layer v1 · **M7** knowledge & divergence (*"no tree; domain
  lattice lite"*) · **M8** politics & diplomacy · **M9** society · **M10**
  Ancient Vertical Slice (go/no-go) · **M11+** era expansions.
- **Continuous:** chronicle, calibration battery, `TUNE` registry, UI.
- **Era-expansion (M11+):** **Finance — banking, debt, panics** — *"staged with
  the early-modern era."*

**Frozen dependencies that bind any reordering:** the Spine and milestone order
(S8 §3, change requires CR + director ADR) · the kernel contract (atomic turn) ·
Law 4 (no calendar gates) · Law 2 (mechanisms over modifiers — *"unlock a bonus"*
is the shape it bans) · D-040 B3 · D-011 §6 · ADR-002 (4000 BCE epoch).

---

## §2 DEPENDENCY AUDIT

| system | depends on | depended on by | safe before knowledge? | needs a SEAM instead? |
|---|---|---|---|---|
| **Money / currency** | goods + prices (M3 ✅) | taxation, credit, banking, FX, notable purchase | **YES** — a medium of exchange is not a technology | no |
| **Taxation / budget** | money, population, settlements | authority, institutions, research funding | **YES** | no |
| **Institutions** (schools, academies) | construction, goods, **maintenance funding** | knowledge generation | **NO** — needs the fiscal loop | — |
| **Knowledge / science generation** | population, classes (M3 ✅), **institutions**, surplus | capability emergence everywhere | — | — |
| **Capability predicates** | computed world state only | economy, governance, military, diplomacy | **YES — and this is the key finding** | **YES** |
| **Strategic war v1** | cohorts, supply | — | **already shipped (M4)** | already has one |
| **Battle Layer** | strategic war | military full | **YES (M6)** | plugs into a reserved interface |
| **Diplomacy** | polities (D-037, M4 ✅), contact | knowledge diffusion | partly | **YES — contact/stance seam** |
| **Knowledge diffusion** | **contact** (trade M4 ✅ suffices) | divergence | **YES via trade** | no |
| **Credit / banking / FX** | money, institutions, **accounting capability** | advanced economy | **NO** | — |
| **Government forms** | legitimacy, authority (M5) | which pathways are available | **NO** | — |

**The audit's central result.** Only one thing in the whole table is both
(a) required by *every* later system and (b) implementable before any of them:
**the capability predicate seam.** Everything else has a real ordering
dependency. Institutions need funding; funding needs money and taxation; research
needs institutions. **Knowledge is genuinely downstream of the governing loop,
which is why the Spine put it there** — that ordering is not an oversight.

---

## §3 PROPOSED MILESTONE ORDER

**Recommendation: keep the ratified D-011 §6 order, and add one small early
seam plus one milestone re-scope.** Three options are given because this is a
director decision.

### Option 1 — MINIMAL (recommended)

Order unchanged (M5 governing loop, M6 battle, M7 knowledge, M8 politics). Two
changes:

1. **Land the CAPABILITY SEAM as a late-M4 or early-M5 contract packet** (§4.1).
   Small, mechanism-free, and it is the entire anti-retrofit device.
2. **Re-scope M7 from "knowledge & divergence" to the capability-generation
   layer** described in the brief, keeping the *"no tree"* constraint.

*Why:* preserves the "it's a game now" checkpoint at M5 that Spine v3 called
motivation-critical (*"v2 deferred playability to M5; for a motivation-funded
solo project that is fatal"*); keeps M4's five money deferrals valid; requires no
renumbering; and satisfies the stated sequencing goal, because knowledge at M7
already precedes Military full (M9+) and era economics (M11+).

### Option 2 — SPLIT ECONOMICS (the real gap, if the brief's economy matters)

As Option 1, plus: **pull Finance forward from "era expansion" (M11+) to a
dedicated milestone after knowledge.** The brief's barter → accounting →
coinage → credit → banking → FX progression is currently scheduled at M11+,
staged with the early-modern era. **That is the largest genuine mismatch between
the brief and the plan** — larger than anything about warfare. If the director
wants that progression to be a mid-game system, Finance must move, and it must
land *after* the capability layer so its stages emerge rather than unlock.

### Option 3 — REORDER AS BRIEFED (not recommended)

M5 knowledge → M6 governance → M7 diplomacy → M8 warfare. **Rejected on
evidence:** it deletes shipped M4 warfare and the M6 Battle Layer; it orphans
five by-name money deferrals; it puts research before the fiscal system that
funds institutions, inverting a real dependency; and it moves the fun-check the
Spine deliberately placed at M5.

---

## §4 WHAT M5 SHOULD MEAN

**Under Option 1, M5 keeps the governing loop, and the capability layer is M7 —
but the layer needs a name that is not "tech tree", and a seam that lands early.**

### 4.1 THE CAPABILITY SEAM — the anti-retrofit device

The brief's real question is: *"what needs to exist before we build the advanced
economy, governance, diplomacy and warfare, so that technology and knowledge are
not retrofitted afterward?"*

**Under D-040 B3 the answer is not a research system.** It is a single uniform
way for any system to ask **"can this civilization do X?"**, answered from
computed state — modelled on the shipped `ClassStateRow.Active` hysteresis latch.

If that seam exists before M5, then taxation, credit, government forms, military
doctrines and diplomatic instruments all express their availability *through it*.
If it does not, each of those milestones invents its own gate, and **that** is the
retrofit the brief is trying to avoid. The seam is a contract, not a mechanism:
no science stock, no research projects, no domains, no constants.

### 4.2 THE CAPABILITY-GENERATION LAYER (M7 under Option 1)

Population → intellectual potential → institutions → knowledge → **capability
predicates becoming satisfiable** → changed economic, military, political
behaviour. Government determines which institutional pathways are available or
efficient; capability determines what is possible. A government change therefore
*transforms* pathways rather than deleting acquired capability — which the
latch-based shape supports naturally, because a latch records that a predicate
*has* fired.

---

## §5 PARALLELIZATION PLAN

Concurrent design tracks that do not create conflicting contracts:

- **A. Capability seam** — the predicate/latch contract. **Blocks the others;
  must be settled first.**
- **B. Knowledge generation** — population, classes, institutions → knowledge.
  Independent of A's *consumers*.
- **C. Diffusion** — contact-based transfer; rides the existing trade network.
- **D. Institutions** — lifecycle (found, fund, maintain, obsolesce, transform).
  Couples to M5's fiscal loop, not to A.
- **E. Government/civics coupling** — which pathways a regime makes available.
  Needs M5 and A; last.

A is a genuine blocker. B/C/D can proceed in parallel once A's shape is ruled on.

---

## §6 CROSS-SYSTEM DEPENDENCY MAP

```
population ─┬─► intellectual potential ─► knowledge ──┐
classes ────┘                                          │
institutions ─► knowledge generation ──────────────────┤
   ▲  (needs construction + FUNDING ⇒ needs M5 money/tax)
   │                                                   ▼
economy ──► funds institutions                CAPABILITY PREDICATES
   ▲                                                   │
   └───────────────────────────────────────────────────┤
                                                       ├─► economy (accounting → coinage → credit → banking → FX)
civics/government ─► which pathways are available ─────┤
   ▲                                                   ├─► warfare (doctrine, weapons, organisation)  [M9+ Military full]
   └── capability can reproduce what a regime lacks ───┤
                                                       ├─► agriculture · engineering · medicine
diplomacy ─► contact ─► diffusion ─────────────────────┘
   ▲
trade network (M4 ✅) also provides contact
```

**Two loops to note.** *Economy ⇄ knowledge*: the economy funds institutions that
produce knowledge that changes the economy — a real feedback loop, and the reason
knowledge cannot simply precede economics. *Civics ⇄ capability*: government
gates pathways, and capability can reproduce what a government lacks (the brief's
militia-vs-universal-training example) — which requires capability and civics to
be **mutually referable**, i.e. both expressed through the §4.1 seam.

---

## §7 PLAYER EXPERIENCE / TEMPORAL MODEL

Covered in `docs/m5-temporal-control-and-player-agency-placeholder.md`; the
kernel conflict is CR-006 and is **not** re-opened here. The distinction the brief
asks for, restated against the atomic turn:

| stage | where it can live today |
|---|---|
| research progress during a turn | inside the turn; state at boundaries only |
| a discovery becoming known | a predicate becomes satisfied during the step |
| capability state changing | the latch fires; recorded in the turn's state |
| player handed a decision | **turn boundary** — the kernel has no sub-turn coordinate |
| an order executed | next turn, via the turn-stamped `OrderLog` |

**Do not invent a mid-turn order timestamp.** CR-006's recommended Option C — a
decision event *ends the turn early*, so the interrupt **is** a boundary — is the
only option that delivers responsiveness without kernel re-entrancy.

---

## §8 OPEN DESIGN QUESTIONS

**Before M5 can be specified:**
1. **Is an accumulating "science" stock compatible with D-040 B3 at all?** A
   project that completes and grants a capability is an unlock with a progress
   bar. Either B3 is narrowed to *"no tree EDGES"* while permitting accumulation,
   or capability must emerge purely from structural preconditions with no
   research resource. **This is the load-bearing question and everything else
   depends on it.**
2. Does the capability seam land before M5 (Option 1) or not at all?
3. Does Finance move forward from M11+ (Option 2)?
4. Where does **money** live if M5 is ever re-tasked? Five M4 deferrals point at it.
5. Is "educated population" a class (D-018), a class attribute, or new state?

**Can wait until inside M5/M7:**
6. Are science and knowledge one stock or two?
7. Institution lifecycle: stock, structure, or row with a lifecycle?
8. Does knowledge decay, and if so through a Ledger sink with its own reason?
9. How many parallel domains, and are they literal trees, graphs, or predicate sets?
10. **What calibration corridor gates knowledge?** The project gates milestones on
    corridors; "science output" has no obvious historical target. A milestone that
    cannot be calibrated is a milestone that cannot pass its own exit criteria.

**Later milestones:**
11. Brain drain — must move people through the existing migration/`Ledger.Transfer`
    machinery, not a parallel mover.
12. AI research strategy — deterministic, no unordered iteration (law 5).
13. FX and exchange-rate dynamics between civilizations.
14. Espionage/scientific exchange (Spine: M10+, T3).

---

## §9 MIGRATION / DOCUMENTATION IMPACT

| document | status | impact |
|---|---|---|
| `civ-sim-architecture-v3-outline.md` | **FROZEN** | Options 2/3 rewrite the ladder — CR required |
| `d011-battle-layer-addendum.md` §6 | **RATIFIED** | the operative order; **the brief was written against the pre-D-011 table** |
| `d040-discovery-and-control.md` B3 | **RATIFIED** | governs M5's shape; Q1 may require narrowing it — CR required |
| `m4-spec.md` | ratified | five money deferrals break under Options 2/3 |
| `CLAUDE.md` | **FROZEN** | **STALE: line 10 still reads "Current milestone: M3" while M4 is in progress.** Not edited — flagged for director |
| CR-005 / CR-006 | open | unchanged; this document does not resolve them |

**No frozen document was edited.**

---

## §10 IMPLEMENTATION ORDER (after ratification)

1. Director rules Q1 (science stock vs pure emergence) — **blocks everything**.
2. Capability seam contract packet (small, mechanism-free).
3. Complete M4; exit gate.
4. M5 governing loop + money + taxation (institutions become fundable).
5. M6 Battle Layer v1 (unchanged, plugs into its reserved interface).
6. M7 capability-generation layer: knowledge generation → institutions →
   diffusion → civics coupling.
7. Finance staging (position per Q3).
8. M8 politics & diplomacy; M9 society; M10 Ancient Vertical Slice go/no-go.

---

## §11 DOES M4 ITSELF HAVE TO CHANGE?

**No — with one caveat and one stale line.**

- **No M4 packet needs rescoping.** The capability seam is additive and could be a
  late-M4 contract packet or open M5; it changes no shipped mechanism.
- **Caveat:** if Q1 is answered as *"pure emergence, no science stock"*, then M4's
  existing emergence predicates (class emergence, and D-040 B3's boats example)
  become the **template** for the whole capability layer, and it is worth auditing
  them before they are copied. `docs/food-anomaly-investigation.md` §7 already
  found one such predicate reading a post-destruction stock — evidence that these
  predicates deserve review before being made load-bearing for a milestone.
- **Stale line, not edited:** `CLAUDE.md:10` still says *"Current milestone: M3"*.
  It is frozen and prohibited from edit here; the director should correct it at
  the next gate.
