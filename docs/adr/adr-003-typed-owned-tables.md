# ADR-003 — Ownership by construction via `ISimSystem<TOwned>` + SystemCatalog

**Status:** accepted (recorded during T0.5; refines the §3.1 sketch)
**Context packet:** T0.5 (turn executor + toy systems)

## Decision

The kernel contract's `ISimSystem.Step(SimContext ctx)` sketch is realized as a
generic pair: `ISimSystem<TOwned>.Step(SimContext<TOwned> ctx)`, where `TOwned` is
a per-system readonly struct of writable table handles (e.g.
`WeatherTables(Table<RainfallRow>)`). Contexts are constructed in exactly one
place — `SystemCatalog`, the composition root — via internal constructors; the
executor consumes systems through non-generic `SystemRegistration` invokers.

## Rationale

§3.1 demands read/write enforcement **by construction**: "an agent cannot silently
write another system's state — the reference does not exist." A single non-generic
`SimContext` would need either runtime table lookup (a runtime check, not a
construction guarantee) or exposure of all tables (no guarantee at all). The
generic payload makes each system's writable surface a compile-time type; the only
code that can grant tables is the catalog, reviewable at a glance, and
`SimContext`'s internal constructor prevents systems from forging contexts.

## Consequences

- Adding a system = one table-ownership line in `SystemCatalog` + its `TOwned`
  struct; ownership changes are visible in one file's diff.
- The pipeline stays generic: executor and pipeline.json know only
  `SystemRegistration` (id, name, invoker).
- Systems remain stateless pure functions; cross-system communication remains
  Prev-reads (one-turn lag) and, later, kernel-ordered explicit handoffs.
