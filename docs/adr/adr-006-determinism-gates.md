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
   see — hidden shared state between runs (statelessness violations), identity-hash
   or allocation-address dependence feeding state, framework RNG or wall-clock
   values reached without their greppable names. Four suites over 1,000-turn runs:
   orderless twin-run, ordered twin-run (independently built identical logs),
   replay from a reloaded order log, and a per-turn exact conservation audit — all
   per-turn `WorldHash` equality against the canonical serialization (ADR-005).

## Honest coverage limits (neither gate catches these)

- **Deterministic `Dictionary`/`HashSet` iteration in sim logic:** .NET enumerates
  these in entries-array insertion order, so a purely local dictionary iterated in
  sim code produces identical results in both in-process twins — the law-5 ban on
  such iteration is enforced by code review, not by either gate.
- **Culture-sensitive parse/format:** both twins share one process culture, so a
  culture leak cannot diverge in-process. The actual mitigation is
  `InvariantGlobalization` in Directory.Build.props (as the grep script's header
  records), not this harness.

## Teeth (proven at packet time, then reverted)

The grep-invisible injection that proved the harness: a **shared static
`Dictionary` in WeatherSystem accumulating entries across Step calls, its
iterated contents feeding a state write** — a statelessness violation invisible
to the grep. It **passed** the grep gate and **failed** the harness (twin runs
diverged at turn 2, ordered twins and replay at turn 1: the second twin observes
the first twin's accumulated entries). Adversarial review additionally confirmed
identity-hash dependence (`new object()` hash bits feeding state) fails 3 of 4
suites. This is the recorded proof that the harness covers the hidden-shared-state
gap the grep cannot, and why both gates are required on every push.

## Consequences

- Twin runs construct everything fresh (world, system instances, executor), so
  the harness also re-proves system statelessness on every push.
- Runtime budget: the harness must stay under ~3 minutes in CI; cuts to run
  length require a director decision, never a silent shortening.
