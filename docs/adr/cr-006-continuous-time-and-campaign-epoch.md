# CR-006 — CONTINUOUS SIMULATION AND A 10,000 BCE EPOCH CONFLICT WITH THE FROZEN KERNEL CONTRACT AND CHARTER

**Status: OPEN — awaiting director ruling. No frozen document has been edited and
no code has been changed.** Raised under S8 §3 by the M5 temporal-control design
(`docs/m5-temporal-control-and-player-agency-placeholder.md`).

**Two independent conflicts.** They can be ruled on separately.

---

## §1 CONFLICT ONE — MID-TURN HAND-BACKS vs THE FROZEN KERNEL CONTRACT

### 1.1 The frozen item

Kernel contract §3.2–3.4, frozen at M0 exit, implemented in
`Sim.Core/Kernel/TurnExecutor.cs:70-93`. The turn is **atomic**:

```
(1) dt = era-table band at the turn-START date
(2) next = prev.Clone()
(3) run every system in configured order
(4) next.Clock = (Turn + 1, SimDays + dtDays, dtDays)
```

Systems read `Prev` and write only their own `Next` tables. **There is no point
between (3) and (4) at which control can leave the executor**, and there is no
sub-turn time coordinate: state exists only at turn boundaries. The
double-buffer, the `Prev`/`Next` isolation and the ordering guarantee are what
make replay and cross-process determinism provable.

### 1.2 The design requirement that collides with it

The M5 design requires that when research completes part-way through a turn —
*"1946.3: Nuclear Fission completes"* — the technology become available **at that
point in simulation time**, and that the player be able to act on it
**immediately, without waiting for the next turn**.

Under the frozen contract this is not expressible. At dt = 10 sim-years the
world simply has no state at 1946.3; it has state at the turn boundary before and
the one after. The requirement asks the executor to **yield mid-pipeline, accept
input, and resume** — a re-entrancy the contract does not provide.

### 1.3 Evidence that this is structural, not incidental

- **`TurnExecutor.Step` returns only a completed world.** Its sole observation
  hook (`ITurnObserver`, ADR-007, extended for the food investigation) is
  explicitly **read-only** — `OnPhaseState(string, IReadOnlyWorldState)` — and
  cannot accept input.
- **Orders are turn-stamped.** `OrderLog.BatchFor(prev.Clock.Turn)` delivers
  orders keyed to an integer turn (§3.9). **An order issued at 1946.3 has no
  representable timestamp.**
- **T1.9 precedent, recorded in CLAUDE.md:** *"Every order-delivery semantic (when
  an order applies relative to when it was issued) gets its own turn-exact pin —
  live-vs-replay comparison alone cannot see stamping drift."* A mid-turn order is
  a **new delivery semantic** and would need such a pin, which cannot be written
  against a clock with no sub-turn coordinate.
- **Replay determinism.** A hand-back that depends on when a human chose to be
  interrupted is not reproducible unless the interrupt point is itself recorded
  as data. Otherwise replay diverges — the property four goldens and two CI jobs
  exist to protect.

**This is a genuine internal contradiction** in S8 §3's sense: a frozen
commitment (the atomic turn) and a required capability (mid-turn agency) cannot
both hold as stated.

### 1.4 Options (≤3, none implemented)

**Option A — SUB-STEPPING.** dt is subdivided; the executor runs finer steps and
can hand back at any sub-step boundary. Mid-turn agency becomes real and the
clock gains a sub-turn coordinate. **Blast radius: the largest possible.** Every
golden re-pins, every rate integration is revisited, per-turn cost rises by the
subdivision factor, and the T4.2/CR-001 dt-correctness work is re-opened. It also
contradicts the measured finding that **per-year behaviour is already dt-invariant**
(`docs/food-evidence-dt-experiment.md`), so this would buy resolution the physics
does not currently need.

**Option B — DEFERRED-EFFECT, DATE-STAMPED PRESENTATION.** The simulation stays
atomic. Research completion is **recorded with the sim-time at which it occurred**
(interpolated within the turn), and the *presentation* reports "1946.3: Nuclear
Fission completes". The player acts at the next boundary, but the game shows the
true date and the option is not lost. **Blast radius: small** — a stamp on a
completion record, plus presentation. **Cost: it does not literally deliver
"act immediately"; it delivers "know exactly when, act at the next checkpoint".**

**Option C — INTERRUPT AS A TURN BOUNDARY.** A decision event **ends the turn
early**: the executor completes the current step, and the next turn's dt is
shortened so the boundary lands at the event. Turns become variable-length in a
second sense (era pacing already varies dt). Mid-turn agency becomes real
*without* sub-stepping, because the interrupt simply *is* a boundary. **Blast
radius: moderate** — dt becomes event-dependent, so it must be recorded in the
order log for replay, and goldens for any world that triggers one re-pin.

### 1.5 Recommendation

**Option C, with Option B as the fallback if variable dt proves unreplayable.**

C preserves the atomic turn, the double buffer and the `Prev`/`Next` isolation —
everything determinism rests on — while delivering genuine mid-turn agency,
because an interrupt that *creates* a boundary needs no re-entrancy. The clock
already tolerates varying dt (10 → 0.5 across eras), so the machinery exists.
The open risk is replay: the shortened dt must be recorded as data, or replay
diverges. **That risk is exactly open question 6 in the design document and must
be answered before anything is built.**

Option A is not recommended: it re-opens dt-correctness across the whole
simulation to buy resolution that the measured evidence says the physics does not
need.

---

## §2 CONFLICT TWO — A 10,000 BCE EPOCH vs ADR-002 AND THE CHARTER

### 2.1 The frozen items

- **`CLAUDE.md`, line 3 — the constitution's first sentence:** *"One
  deterministic, turn-based civilization simulation **spanning 6,000 years**."*
- **ADR-002:** *"`SimClock` stores time as `long SimDays` since the campaign epoch
  (**4000 BCE = day 0**)."* Implemented at `EraTable.cs:7` and `:20-21`.
- **`Sim.Data/content/era-pacing.json`:** the first band begins at
  `startYear: -4000`; `EraTable.DtDaysAt` **throws** for any day outside
  `[CampaignStartDay, CampaignEndDay)`.

### 2.2 The requirement

The proposed campaign start is **10,000 BCE**, roughly **doubling** the span to
~12,100 years.

### 2.3 Why this is a conflict and not a tuning change

S8 §3 exempts *"all data files and every `TUNE` parameter — tuning is play, not
amendment"*, and `era-pacing.json` is a data file. **But the epoch is not tuning:**

1. It is stated in **CLAUDE.md**, the constitution every agent reads first.
2. It is fixed by **ADR-002**, a ratified decision record.
3. It is part of the **Scale Charter** (S1–S5, frozen), which sizes the whole
   simulation.
4. Doubling the span doubles the compounding horizon — and the Spine's numeric
   policy already flags that *"a 6,000-year compounding sim must state its
   overflow discipline"*. **Twelve thousand years is a different statement about
   overflow, population growth bounds and price compounding than six.**

### 2.4 Options

**Option A — extend the era table backward to −10000 and amend ADR-002 + CLAUDE.md.**
Delivers the requirement. Requires new pacing bands for the pre-agricultural
period (what dt does 10,000 BCE use?), and re-states the charter's span. Every
founded world's date labelling changes; world hashes do **not** change unless
band boundaries move within the existing span.

**Option B — keep 4000 BCE as day 0 and treat 10,000 BCE as a presentation-only
prologue**, not simulated. Zero blast radius, but it does not actually deliver a
10,000 BCE start.

**Option C — defer.** Record the requirement against the milestone that owns the
calendar and decide when that packet is specified.

### 2.5 Recommendation

**Option A if the director wants a genuine 10,000 BCE start, but it must be ruled
on explicitly**, because it edits the constitution's first sentence and a
ratified ADR. **The pre-4000-BCE pacing question is unanswered and is real**: the
existing bands run 10 → 0.5 sim-years and there is no band for a
pre-agricultural period, so extending the table is not a mechanical edit.

**Neither conflict is resolved. Nothing is implemented. Awaiting ruling.**

---

## §3 WHAT IS *NOT* IN CONFLICT — recorded so it is not re-litigated

**"Turn duration may vary by historical period" is ALREADY SATISFIED** and needs
no change: `EraTable` steps dt 10 → 5 → 3 → 2 → 1 → 0.5 sim-years by era, and
`SimClock.DtYears` is the universal rate basis under law 3.

**"No year zero" (2 BCE → 1 BCE → 1 CE → 2 CE) conflicts with nothing.** No
BCE/CE converter exists anywhere in the tree — `SimClock.WorldDateYears` is
simply years since epoch, and the calendar mapping is unowned. It can be
specified freely as a presentation-layer requirement.
