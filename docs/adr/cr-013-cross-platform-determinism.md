# CR-013 — THE SAME SEED PRODUCES DIFFERENT WORLDS ON WINDOWS AND LINUX

**Status: OPEN — awaiting director ruling. No simulation code, constant,
equation, config, golden, corridor, band or quarantine was touched. This file
and the evidence below are the whole change.**

Raised under S8 §3 during T4.18. It was found by the T4.17 session trace on its
**first use against a real played session** — which is what that cross-check was
built for, and the reason the trace is written live rather than derived.

---

## §1 THE FROZEN ITEM IN CONFLICT

**CLAUDE.md law 5 — determinism.** The kernel contract's whole promise is that a
world is a pure function of its seed and its order log. Everything downstream
depends on it: golden pins, replay equivalence, the session record, and the
director's ability to hand a log to anyone and have them see what he saw.

Nothing in the repository states that the promise is scoped to one platform, and
the CI determinism jobs run on `ubuntu-latest` only, so the scope was never
tested either way.

## §2 THE EVIDENCE

The director played seed 42, canonical world, on **Windows x64**, from the CI
artifact built at commit `98f25c1`. The same commit was run on **Linux x64**.
Both were driven through the identical `UiSession` code path with **zero orders
issued before turn 19**, so turns 1–5 are pure simulation on both sides.

| turn | population | food | settlements | hash (Windows) | hash (Linux) |
| --- | --- | --- | --- | --- | --- |
| 0 | 5,140 | 82,041 | 12 | `0ba766a93454…` | `0ba766a93454…` |
| 1 | 4,330 | 5,752 | 12 | `b9d206c1fcf4…` | `b9d206c1fcf4…` |
| 2 | 4,041 | 4,822 | 12 | `545ca00bd148…` | **`28e62ffb26fb…`** |
| 3 | 3,987 | 4,508 | 12 | `605cabce825f…` | **`05a6190349d6…`** |
| 4 | 3,997 | 4,451 | 12 | `efcc28eb9d48…` | **`ec4257d61e7b…`** |
| 5 | 4,035 | 4,459 | 12 | `1ac4c45da9d0…` | **`ff62af93d533…`** |

Two facts make this precise rather than merely alarming:

1. **Every conserved integer quantity agrees exactly, on every turn.**
   Population, food and settlement count are identical to the unit at turns 0–5
   — and the population decomposition measured independently in
   `docs/m4-population-transient-investigation.md` reconciles on both. The
   divergence is confined to something double-valued elsewhere in the canonical
   stream.
2. **Turns 0 and 1 agree completely; turn 2 is the first divergence.** Turn 2 is
   also the first turn that CONSUMES `CatchmentSummaries`: `WorldFounding` writes
   no catchment rows, the table is empty at turn 0, and 12 rows appear at turn 1
   for turn 2 to read. That makes the catchment computation — travel costs and
   effective arable over the lattice, all doubles — the first suspect. **That is
   an inference from the timing, not a measurement**, because the director's
   world state was never captured; only its hashes were.

Determinism WITHIN a platform is intact and was re-verified, not assumed: on
Linux, a live `UiSession` and a headless replay of its log agree hash-for-hash at
every turn, with and without orders.

## §3 WHY IT MATTERS BEYOND THE HASH

- A session log the director records on Windows **will not reproduce on CI**.
  `sim inspect` correctly reports `REPRODUCTION FAILED at turn 2` for his real
  session — the tool is right, the world genuinely differs.
- The goldens are pinned from Linux CI. A Windows build is therefore not
  golden-clean, and nobody would have noticed, because the suite runs on Linux.
- Cross-machine playtest evidence cannot currently be pooled.

It does NOT invalidate the M4 playtest: the trajectory, the population transient
and the famine crises the director saw are all reproduced on Linux to the unit.
What differs is a quantity that has not yet moved an integer stock in the first
five turns — but "has not yet" is not "cannot".

## §4 THE LIKELY MECHANISM, NOT YET MEASURED

`double` arithmetic is IEEE-754 and platform-stable for `+ - * /`. The usual
culprits for exactly this signature are:

- **`Math.Pow` / `Math.Exp` / `Math.Log`**, which are library functions, not
  IEEE-mandated to be correctly rounded, and legitimately differ in the last
  ulp between platform math libraries.
- **FMA contraction**: a JIT fusing `a * b + c` into one instruction on one
  platform and not the other, changing the rounding of the intermediate.

Either produces a last-ulp difference that survives into the serialized stream
and hashes differently while integer stocks, which round to whole units, agree.

## §5 OPTIONS (≤3, per S8 §3)

1. **Scope the promise to one platform.** Declare Linux x64 the reference,
   document that Windows builds are for play and not for evidence, and have
   `sim inspect` say so when a manifest's build differs from the running
   platform. Cheapest; concedes cross-machine reproducibility.
2. **Find and fix the divergent expression.** Requires first MEASURING which
   table differs — capture a Windows snapshot at turn 2 and diff it against a
   Linux one, which is a `sim hash`/`Snapshot` exercise the director can run in
   ten minutes. Then eliminate the offending call (replace a `Math.Pow` with an
   exact form, or forbid FMA contraction). Correct; blast radius unknown until
   the expression is named; may move every golden.
3. **Cross-platform CI as a standing gate.** Add a Windows determinism job that
   runs the founded golden and compares against the Linux pin. Does not fix
   anything, but converts an invisible defect into a visible one and would have
   caught this at the commit that introduced it.

## §6 RECOMMENDATION

**Measure first (the diff in option 2), then rule.** Naming the divergent table
is cheap, and the choice between options 1 and 2 turns entirely on what it
finds: a cosmetic EMA is a different decision from a catchment area that feeds
production. Option 3 is worth doing regardless of that answer, and it is the
only one of the three that prevents a recurrence.

**Do not tune anything to make the hashes agree.** No constant moved for this
CR and none should.

## §7 BLAST RADIUS

Nothing yet — this file is documentation. Whatever is ruled will touch either
the kernel contract's stated scope (option 1), a simulation expression and
plausibly every golden (option 2), or CI only (option 3).
