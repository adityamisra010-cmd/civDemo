# ADR-007 — ITurnObserver: measurement-only instrumentation on TurnExecutor.Step

**Status:** accepted (director-sanctioned, recorded at M0 closure)
**Context packet:** T0.9 (CLI + bench)

## Decision

`TurnExecutor.Step` gains an overload taking an optional `ITurnObserver`, which
receives per-phase wall time (Stopwatch timestamp ticks) and allocated bytes for
the clone phase and each pipeline system, in execution order. `sim bench` is its
only consumer at M0.

## Constraints (the reason this is safe inside the frozen kernel)

- **Measurement-only:** the observer receives timing data and returns nothing; no
  sim state, context, or RNG is exposed through it, and nothing in the kernel
  reads back anything the observer does. Sim behavior cannot depend on it (a
  wall-clock value influencing state would be a law-5 violation — the observer
  interface makes that structurally impossible by giving it nothing to write to).
- **Phases identical when unobserved:** with a null observer no timestamps are
  taken and the original `Step(WorldState)` path is byte-for-byte the same
  execution; the determinism harness and golden hash pin this.

## Status post-M0

This overload is part of the **frozen kernel surface** as of `m0-exit`: changes
to `ITurnObserver` or the observed-phase set require a director-approved ADR,
like any other kernel contract change (S8 freeze perimeter).
