# ADR-006 — Determinism enforcement: grep gate + behavioral harness, division of labor

**Status:** accepted (recorded at T0.8)
**Context packet:** T0.8 (determinism harness)

## Decision

Determinism is enforced by two complementary permanent CI gates:

1. **The grep gate** (`scripts/check-banned-constructs.sh`, since T0.1): catches
   *visible* banned constructs — `System.Random`, `float`/`MathF`/`Single`,
   wall-clock reads, unordered `Parallel.*`, `GetHashCode` use, out-of-Ledger
   conserved mutation. Fast, textual, zero false negatives for what it names.
2. **The determinism harness** (`DeterminismHarnessTests`, the required
   "determinism" CI job, since T0.8): catches *behavioral* divergence no grep can
   see — hash-bucket-ordered `Dictionary`/`HashSet` iteration, allocation-address
   or identity-hash dependence, hidden shared state between runs, culture leaks.
   Four suites over 1,000-turn runs: orderless twin-run, ordered twin-run
   (independently built identical logs), replay from a reloaded order log, and a
   per-turn exact conservation audit — all per-turn `WorldHash` equality against
   the canonical serialization (ADR-005).

## Teeth (proven at packet time, then reverted)

A deliberately grep-invisible injection — a toy system writing state in the
enumeration order of a `Dictionary<object, ·>` keyed by fresh allocations, whose
bucket order varies with runtime identity hash codes — **passed** the grep gate
and **failed** the twin-run harness. This is the recorded proof that the harness
covers the gap the grep cannot, and why both gates are required on every push.

## Consequences

- Twin runs construct everything fresh (world, system instances, executor), so
  the harness also re-proves system statelessness on every push.
- Runtime budget: the harness must stay under ~3 minutes in CI; cuts to run
  length require a director decision, never a silent shortening.
