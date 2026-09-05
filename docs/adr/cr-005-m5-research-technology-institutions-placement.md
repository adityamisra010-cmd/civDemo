# CR-005 — PLACING "RESEARCH, TECHNOLOGY & INSTITUTIONS" AT M5 CONFLICTS WITH THE FROZEN MILESTONE ORDER

**Status: RULED 2026-09-05 — OPTION C ACCEPTED (see §6). No frozen document has been edited.**
Raised under S8 §3 (milestone order is frozen; change requires a Contradiction
Report + director ADR). Scope record for the proposed packet:
`docs/m5-research-technology-institutions-placeholder.md`.

**Nothing was implemented. No mechanic, formula, schema, test, golden, corridor
or quarantine was touched.**

---

## §1 THE FROZEN ITEMS IN CONFLICT

The director has directed that **M5 own a major "Research, Technology &
Institutions" architecture packet**. Three ratified places currently say
otherwise, and they disagree with the direction in two different ways.

### 1.1 The Spine outline — `docs/civ-sim-architecture-v3-outline.md`

| line | frozen commitment |
|---|---|
| 109 | **M5 — The governing loop.** Taxation, budget, authority/bandwidth economy, laws-lite, legitimacy. **"It's a game now" checkpoint** |
| 110 | **M6 — Knowledge & divergence.** Domain lattice lite, diffusion, computed era labels |
| 111 | **M7 — Politics & diplomacy.** Institutions as modules, regime change, coups/revolts |
| 82–83 | State/taxation/authority **M5**; Legitimacy & opinion **M5** |
| 84 | Knowledge & diffusion **M6** |
| 85 | Politics deep (**institutions**, regime change) **M7** |

So the Spine places **knowledge at M6** and **institutions at M7**, with **M5
reserved for the governing loop** — and M5 is additionally load-bearing as the
project's stated "evaluate fun honestly here" checkpoint.

### 1.2 The M4 spec — `docs/m4-spec.md`

M4 defers work to M5 **by name**, repeatedly, always meaning the fiscal system:

- §1.1: *"Money: M5, taxes IN KIND (Option B). M4 ships no currency."*
- line 48: *"**M5 GOVERNS.** Taxes, in kind. No currency, no fiscal system, no administration at M4."*
- line 143 / 269 (T4.8): notable **purchase** is out of M4 scope because *"payment is money, M5"*.
- line 426: *"…therefore arrives at **M5 alongside the fiscal system**."*
- line 429: *"this list is the **M5 spec author's rewrite inventory**."*

**M4 has already shipped deferrals that point at an M5 defined as money and
fiscal administration.** Re-tasking M5 does not merely reorder a roadmap; it
orphans those deferrals.

### 1.3 The nature of the conflict

This is **not** the "better idea / taste change" class that S8 §3 sends to the
queue. The director's stated reason is an **ordering-dependency argument**:

> technology must be designed before the downstream systems that depend on
> technological capabilities, so that a later warfare or economic packet cannot
> independently invent technology prerequisites that belong to M5.

That is a claim about **architectural sequencing**, which is exactly what the
frozen milestone order encodes — so it belongs in a CR rather than the queue.

---

## §2 EVIDENCE

**E1 — the dependency direction is real and already visible in the tree.** M4's
own spec repeatedly defers capability questions to a later milestone rather than
inventing them locally (T4.8's purchase deferral is the worked example). The same
pressure will apply to any military or economic packet that needs to ask "can
this civilization do X yet?". Under the current order, **M5 (governing loop) and
M7 (politics/institutions) both land before or without a knowledge architecture
that M6 owns**, so institutions would be specified at M7 while the knowledge that
produces them arrives at M6 — adjacent, but the *capability* layer the director
describes has no owner at all.

**E2 — "institutions" is currently split across two milestones.** The Spine puts
*institutions as modules* at M7 and *knowledge & diffusion* at M6. The proposed
packet treats institutions as **the conversion mechanism** from population to
usable knowledge (schools, universities, academies, libraries), which is neither
of those two readings. **The word "institutions" is doing different work in the
Spine than in the direction**, and that ambiguity is itself part of the conflict.

**E3 — no empirical failure is claimed.** S8 §3 recognises *internal* (two frozen
commitments provably conflict) and *empirical* (a frozen commitment fails in
code) contradictions. **This is the internal kind, and it is a conflict between a
DIRECTOR DIRECTIVE and the frozen order, not between two frozen items.** That
distinction is stated plainly rather than dressed up: the amendment authority
here is the director's ruling, and this CR exists to make the blast radius
visible before it is exercised.

---

## §3 OPTIONS (≤3, minimal; none implemented)

**Option A — RENUMBER: M5 becomes Research/Technology/Institutions; the governing
loop and money shift to M6; knowledge/diffusion is ABSORBED into the new M5;
politics/diplomacy moves to M7+.**
Delivers the director's sequencing exactly. Blast radius is the largest:
`m4-spec.md`'s five "M5 = money" deferrals must be re-pointed to the new fiscal
milestone, and the Spine outline lines 82–88 and 105–112 re-lettered. **The "It's
a game now" checkpoint moves with the governing loop**, which the Spine flags as
a motivation-critical gate for a solo project — that consequence should be ruled
on explicitly, not inherited silently.

**Option B — INSERT: Research/Technology/Institutions becomes M5, and every
existing M5…M8 shifts up by one (money/governing loop → M6, knowledge → merged
into M5, politics → M8…).**
Same sequencing benefit, and it keeps each existing milestone's CONTENT intact
rather than re-scoping any of them. Cost: every milestone reference in the tree
that names M5–M8 by number becomes stale, including the M4 deferrals and
`docs/d038-visual-target.md`'s "visual milestone sits between M5 and M6".

**Option C — KEEP THE ORDER; give the technology architecture an early
ARCHITECTURE-ONLY packet inside M5 alongside the governing loop**, with the
mechanics landing at the existing M6.
Cheapest by far — no renumbering, no stale references, and M4's money deferrals
stay valid. It satisfies the stated sequencing rule (*design before dependants*)
without moving any milestone, because what the director asked to protect is the
**design**, not the implementation. Cost: M5 carries two large packets, and the
"institutions" split between M6 and M7 is left unresolved.

---

## §4 BLAST RADIUS

| item | A | B | C |
|---|---|---|---|
| `civ-sim-architecture-v3-outline.md` lines 82–88, 105–112 | rewrite | renumber | one added line |
| `m4-spec.md` five "M5 = money" deferrals | re-point | re-point | **unchanged** |
| `d038-visual-target.md` "between M5 and M6" | re-point | re-point | **unchanged** |
| CLAUDE.md "Current milestone" line | unchanged (M4) | unchanged | unchanged |
| "It's a game now" checkpoint | **moves** | **moves** | stays at M5 |
| code, schema, tests, goldens, corridors, quarantines | **none** | **none** | **none** |

**No option touches any code, data, schema, test, golden, band or quarantine.**
This is a documentation-only decision in every branch.

---

## §5 RECOMMENDATION

**Option C**, unless the director specifically wants the fiscal system to move.

The sequencing rule the direction states is *"technology must be DESIGNED before
downstream systems that depend on technological capabilities"* — and Option C
satisfies that in full, at the cost of one added line, while leaving M4's five
shipped deferrals valid and the motivation-critical "It's a game now" gate where
the Spine deliberately put it. Options A and B buy milestone-number tidiness and
pay for it in stale cross-references across ratified documents.

**If the director's intent is that M5 must be substantively ABOUT learning and
change — "M5 should establish the civilization's ability to learn and change" —
then Option B is the honest reading and C is a half-measure.** That is the
question this CR needs answered, and it is a director call, not an agent's.

**No option is implemented. Awaiting ruling.** The scope itself is recorded
separately and is safe to accumulate under any ruling.


---

## §6 DIRECTOR RULING (2026-09-05) — OPTION C

**Ruled by the M5 FULL BUILD packet.** The packet's instructions state Option C in
substance rather than by name, and they match this CR's own §5 recommendation:

> *"M5 is the **GOVERNING LOOP** milestone … State, Taxation, Authority / state
> capacity, Legitimacy, Opinion / grievance, Laws-lite, Basic autonomous AI."*
>
> *"Do not silently move entire future milestones into M5."*
>
> *"**DO NOT ACTIVATE M6/M7 GAMEPLAY IN M5.**"* — the progression architecture is
> commissioned as **data and specification only**.

That is Option C exactly: **the milestone order is unchanged, M5 keeps the
governing loop and money, and the technology/institutional architecture lands now
as an ARCHITECTURE-ONLY package whose mechanics arrive at their ratified
milestone.**

**Consequences, per §4's blast-radius table:**

- `civ-sim-architecture-v3-outline.md` — one added line, no re-lettering.
- `m4-spec.md`'s five by-name "M5 = money" deferrals — **remain valid**. M5 owns
  money and the fiscal system.
- `d038-visual-target.md`'s "between M5 and M6" — **unchanged**.
- The **"it's a game now" checkpoint stays at M5**, where the Spine deliberately
  put it.
- No code, schema, test, golden, band or quarantine is touched by the ruling
  itself.

### 6.1 ONE DISCREPANCY IN THE RULING, RECORDED RATHER THAN SILENTLY RESOLVED

The packet states the downstream placement as *"Knowledge & diffusion → M6"* and
*"Politics / institutions / regime change → M7"*. **Those are the PRE-D-011 §6
numbers.** `docs/d011-battle-layer-addendum.md` §6 is ratified — and was
explicitly used by D-040 on 2026-08-08 — and it inserted the Battle Layer as M6,
pushing everything down:

| ratified (D-011 §6) | content |
| --- | --- |
| **M5** | governing loop ← *this milestone; both readings agree* |
| **M6** | Battle Layer v1 |
| **M7** | knowledge & divergence *(the packet calls this M6)* |
| **M8** | politics & diplomacy *(the packet calls this M7)* |

**The substance is unaffected** — knowledge and institutions are downstream of M5
under either numbering, which is all Option C turns on. Per the packet's own
source-of-truth rule (*"resolve according to chronology and ratification
status"*), **the ratified D-011 §6 numbering is used throughout M5's documents**:
knowledge is **M7**, politics/institutions is **M8**. Recorded here for the
director to confirm or correct; nothing was renumbered in any frozen document.

### 6.2 WHAT THIS RULING DOES *NOT* SETTLE

Option C's stated cost stands: **the ownership of "institutions" is still split**
between the knowledge milestone and the politics milestone, and this ruling does
not resolve it. The civic/institutional module graph commissioned by this packet
is authored as future content architecture under that ambiguity, and says so.

**Separately and more seriously — §8 Q1 of
`docs/m5-roadmap-dependency-audit.md` remains OPEN and BLOCKING for the
progression lane:**

> *"Is an accumulating 'science' stock compatible with D-040 B3 at all? … Either
> B3 is narrowed to 'no tree EDGES' while permitting accumulation, or capability
> must emerge purely from structural preconditions with no research resource.
> **This is the load-bearing question and everything else depends on it.**"*

That audit's own implementation order makes it item 1 and says it **blocks
everything**. It is a director call and is NOT taken here. The consequence for
this packet is recorded in `docs/progression-architecture.md`: the knowledge
lattice is specified so that **both** answers remain buildable from it, and no
science stock is committed to.
