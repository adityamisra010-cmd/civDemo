# CIVILIZATION SIMULATION — ARCHITECTURE v3 OUTLINE
### Restructured after technical-director review of Master Prompt v2

**Context reset.** This game is built by one director + AI coding agents, for an audience of exactly one player who has full source access. Every requirement that existed to serve a studio, a market, or other players is deleted. Every requirement that protects the solo build loop (determinism, testability, data-driven iteration, small verifiable modules) is strengthened.

**Core structural change.** The single 300-page waterfall GDD is dead. It is replaced by a three-tier document system: a thin **Spine** ratified before any code, **Just-in-Time System Specs** written one milestone ahead of implementation, and **generated Task Packets** for agents. Specification effort follows the dependency graph, not encyclopedia order.

---

## TIER 0 — THE SPINE (write and ratify first; ~20–30 pages total; changes require sign-off)

### S1. Player Charter (replaces v2 Part 1)
- Who the player is: DEV, and only DEV. Name the pleasures being built for, ranked. Candidate list to confirm: (a) watching a world grow and react to decisions (Manor Lords / city-builder pleasure), (b) waging and winning wars (Total War pleasure), (c) reading the world's data and history (Paradox pleasure), (d) era-spanning progress arc (Civ pleasure).
- **Open values question (must be answered before M4):** do battles need to be *fought/watched*, or only *resolved and reported*? This decides whether the battle module interface reserves a future tactical layer as a first-class replaceable component or a note.
- Anti-goals: no monetization, no localization, no accessibility beyond DEV's needs, no marketing comparables, no multiplayer.
- Design position (formerly Law 11): the player is a continuous civilization-perspective; control is mediated by current institutions; losing a regime changes your levers, not your seat.

### S2. Design Laws v3 (revised from 12 to 10; precision fixes applied)
1. **Conservation.** People, money, and goods are conserved. Every aggregation, migration, or conversion operator must conserve exactly; property-based tests enforce this in CI. Anything that cannot conserve (e.g., a lossy LOD tier) is cut, not fudged.
2. **Mechanisms over modifiers — precise form.** Effects act on real stocks and flows. Coefficients *inside* a resolution equation (terrain multiplier in a combat equation, fertility factor in a yield function) are legal. Free-floating permanent auras ("+10% happiness") are illegal. *(v2's absolute wording made Part 17 combat self-contradictory; this closes it.)*
3. **No instant transformation.** All change integrates over turns at dt-correct rates.
4. **No calendar gates.** Capability derives from computed state; era labels are descriptive output only.
5. **Computed, never assigned.** Every derived stat has a formula; unknown coefficients are tagged `TUNE` and registered.
6. **Glass box, honestly scoped.** Surfaced key variables carry causal decompositions computed by the kernel's delta-tracker. Where true attribution is infeasible (equilibrium prices), the UI shows contributing inputs and their movements — never fabricated percentages. No explanation string may exist that is not derived from state. *(v2 over-promised "every number decomposes"; general-equilibrium attribution is not tractable.)*
7. **Symmetry.** AI actors use player-identical verbs and information class. Difficulty = information and friction, never hidden resources.
8. **Data-driven content.** All content (goods, crops, doctrines, institutions, units, events, phonologies) lives in validated data files; code implements mechanisms only. Retained *not* for modders — for DEV's own iteration speed: design agents edit data without touching code agents' work.
9. **Single-machine determinism.** Same seed + same order log ⇒ identical world, on the dev machine. Standard float64 permitted under fixed evaluation order; parallelism only with deterministic reduction. Purpose: replay debugging, golden-run regression, calibration. *(v2's fixed-point-math analysis and lockstep-multiplayer provision are deleted — they served cross-machine sync for players who don't exist.)*
10. **Tiered realism.** Every system declares a fidelity tier per milestone. Realism that cannot be tested at its tier is deferred, not faked.

### S3. Simulation Kernel Contract (merges v2 Parts 3, 28.2, Appendix B)
- **State model:** world state as plain data tables, each owned by exactly one system. Systems are pure functions: `step(read_only_world_prev, my_tables_rw, orders, rng_stream, dt)`. Read/write enforcement **by construction** through the function signature and type system — not the separate contract-enforcement tooling v2 demanded (weeks of tooling replaced by an API shape agents cannot violate silently).
- **Fixed execution order** (draft; finalize in S3): environment/upkeep → agriculture/food → demographics → production → market → trade/logistics → culture/religion/opinion → politics/legitimacy → diplomacy → military (sub-stepped) → events/crises → chronicle/report. One-turn-lag is the default coupling; the interaction matrix marks the few deliberate same-turn edges.
- **Sub-step rule (fixes v2 contradiction):** sub-stepped systems (epidemics, battles, panics) read only frozen turn-start state plus their own sub-state. No mid-turn reads of other systems' fresh writes.
- **Turn semantics:** era-scaled dt + crisis zoom, retained. **dt authority rule (fixes v2 gap):** global dt is set by the world's *most advanced* polity's era band; all systems integrate rate × dt regardless (Law 3), so laggard civs simply evolve less per turn. Era-pacing table committed here; campaign budget 1,200–2,000 turns.
- **RNG:** named seeded streams per system × region; registry in kernel.
- **Numeric policy:** float64; explicit clamps and growth bounds on all exponential processes (population, money, prices) — a 6,000-year compounding sim must state its overflow discipline.
- **Units & constants:** 1 pop unit = N persons (decide N here), money-unit strategy across eras, sim-year as the universal rate basis.
- **Saves:** versioned full snapshots. **Compatibility policy for solo dev:** saves may break between milestones; seed + order-log replay is the recovery path. (v2's save-migration engineering is deleted.)
- **Headless-first:** the sim is a UI-free library with a CLI runner; this is deliverable M0. Explanation delta-tracker is a kernel service.

### S4. World & Scale Charter (fixes v2's unbounded scaling)
- **World size committed small:** default 40k cells (range 25–60k), 12–25 polities. Rationale: n=1 wants a *dense* world, not a big one; every scaling problem in v2 shrinks by an order of magnitude.
- **No spatial LOD in v1.** Full simulation everywhere at this world size. (v2's promote/demote LOD tiers were the single largest conservation and complexity hazard; a small world deletes the problem.) AI *attention* LOD (dormant/active planning) is retained — decisions aren't conserved quantities.
- **Pop bucket cardinality cap:** bucket key = location × culture × religion × class/occupation × age-band; hard cap ~150k buckets world-wide with an automatic merge-below-threshold policy. (This is the Victoria-2 explosion, pre-answered.)
- **Trade routing on a region graph** (~300–800 nodes aggregated from cells), never the cell graph. Cells keep local detail; commerce flows coarse.
- **Goods staging:** 12–15 goods (M3) → ~30 (vertical slice) → ~60 (late eras). v2's 50–90 at first spec is deleted.
- **Performance budget:** ≤5 s/turn early, ≤20 s/turn late game on DEV's machine; profiling gate in CI.

### S5. Tech Stack Decision (v2 said "don't lock language" — wrong for solo; lock it now)
- **Criteria:** AI-agent competence density, static typing (agents' silent errors get caught), performance headroom ~10–50× over Python, mature data/UI ecosystem, single-binary desktop.
- **Recommendation: C#/.NET.** Rationale: top-tier agent training coverage, fast enough for this world size, excellent data tooling, painless windows desktop delivery, and a credible UI story at every fidelity (see S5-UI). Rust if DEV prefers maximal performance and stricter compile-time enforcement at the cost of agent iteration speed. Decide once, record in Decision Log.
- **UI doctrine — the debug UI IS the game UI.** Since the only player has source access, ship developer-grade instruments as the actual interface: map render + data tables + time-series graphs + decision queue (ImGui-class immediate-mode UI, or a local web dashboard over the headless sim). v2's "premium cartographic aesthetic," bespoke UI kit, and 23.10 visual direction are deleted. This is the largest single labor cut in the project — UI is where solo strategy projects die.
- **Storage:** data content as versioned JSON/TOML with schema validation; saves as binary snapshots.

### S6. Dependency Graph & Milestone Map (see Tier 2 below — the build spine)

### S7. Living Registers
- Decision Log (every recommendation + alternatives + rationale; append-only).
- Requirement Index (`[XXX-###]`, one line each).
- `TUNE` parameter registry (owner, range, default, sensitivity).
- Open-questions register — must be empty before each milestone's task cut.

---

## TIER 1 — SYSTEM SPECS (just-in-time; one file per system; written one milestone ahead)

Template trimmed from 16 to 12 fields: Purpose & realism target · Fidelity tier per milestone · State owned (tables/fields) · Inputs read (systems + lag) · Outputs & events · Turn logic (with dt handling) · Player-facing surface & explanations · AI usage · Data schema · Acceptance tests (incl. emergence/calibration hooks) · Anti-scope · Open decisions (must be empty at task-cut).

**System inventory with milestone + v1 tier assignment:**

| System | Milestone | v1 Tier | Top risk to pre-answer |
|---|---|---|---|
| Kernel/turn pipeline | M0 | T1 | determinism harness proves itself day one |
| Worldgen & map | M1 | T2 | coherence over beauty; region graph derived here |
| Population (buckets+cohorts) | M1–M2 | T1 | bucket cap discipline |
| Agriculture & food | M2 | T1 | Malthusian loop stability under dt |
| Settlements | M3 | T2 | growth without micromanagement |
| Production & goods | M3 | T2 (12–15 goods) | recipe graph kept shallow at first |
| **Markets & prices** | M3 | T1 | **the #1 project risk — see solver mandate below** |
| Trade & logistics | M4 | T2 | region-graph routing only |
| Conflict v1 (attrition war) | M4 | T2 | manpower from real cohorts from day one |
| State, taxation, authority | M5 | T1 | the governing loop must be *fun* here |
| Legitimacy & opinion | M5 | T2 | mechanism-only, no mood auras |
| Knowledge & diffusion | M6 | T2 | no tree; domain lattice lite |
| Politics deep (institutions, regime change) | M7 | T1 | composable modules, not enum governments |
| Diplomacy | M7 | T2 | interest-computed stances; treaties as clauses |
| Religion & culture | M8 | T2 | procedural doctrine bundles |
| Health & disease | M8 | T2 | SIR on trade network; sub-step rule |
| Environment & climate | M9 | T2→T1 industrial | degradation stocks close loops |
| Military full (ops, siege, naval) | M9+ | T2 | battle module behind replaceable interface |
| Espionage/intel uncertainty | M10+ | T3 | estimates-with-error UI only at first |
| Characters/notables | M10+ | T3 | light layer; no romance sim |
| Finance (banking, debt, panics) | era exp. | T2 | staged with early-modern era |
| Media/nationalism | era exp. | T2 | reuse opinion engine |
| Chronicle engine | continuous | lite→T1 | event log with names ships at M2 |
| Calibration battery | continuous | T1 | corridors added as systems land |

**Market solver mandate (closing v2's most dangerous under-specification):** no global Walrasian equilibrium. Each regional market holds a local price per good; prices adjust by damped excess-demand steps with clamps; arbitrage flows between connected markets close gaps at transport-cost thresholds. This is Victoria-3-class local-price architecture: stable, incremental, explainable, and implementable by an agent in bounded sessions. Global-equilibrium solving is permanently anti-scoped.

---

## TIER 2 — BUILD SPINE (milestones; every one ends runnable + tested; autoplay green from M2)

- **M0 — Kernel.** Turn executor, state tables, RNG registry, determinism harness (same seed ⇒ identical hash over 1,000 turns), CLI runner, CI with conservation property tests. *No game.*
- **M1 — Walking skeleton (the big restructure).** Small generated map, one settlement, minimal pop+food loop, end-turn button, crude map + 3 numbers + 1 decision. **Playable-ugly inside weeks, not months.** v2 deferred playability to M5; for a motivation-funded solo project that is fatal.
- **M2 — Malthusian core.** Cohort demographics, farming, famine; autoplay shows boom–crash–recovery; graphs UI; chronicle-lite (named event log).
- **M3 — Production & markets.** 12–15 goods, settlements grow, the mandated local-price solver proven stable in 500-turn autoplay soak.
- **M4 — Trade + first conflict.** Region-graph trade; 3–8 dumb AI neighbors; attrition-model war drawing manpower from real cohorts. *(Pulled forward from v2's M6 — conflict is core to DEV's stated taste; the game must show its teeth early.)*
- **M5 — The governing loop.** Taxation, budget, authority/bandwidth economy, laws-lite, legitimacy. **"It's a game now" checkpoint — evaluate fun honestly here before proceeding.**
- **M6 — Knowledge & divergence.** Domain lattice lite, diffusion, computed era labels; map shows civilizations pulling apart in time.
- **M7 — Politics & diplomacy.** Institutions as modules, regime change, coups/revolts; interest-computed diplomacy, clause treaties.
- **M8 — Society layer.** Religion, culture, opinion, disease. First two crisis archetypes fully live (famine, plague).
- **M9 — Ancient Vertical Slice.** Full loop through classical antiquity; calibration corridors for antiquity pass (population, urbanization, empire sizes, war lethality); crisis archetypes: +succession war, +revolt. **Go/no-go gate for era expansion.**
- **M10+ — Era expansions.** Medieval → early-modern (finance, print) → industrial (energy, factories, demographic transition test) → modern (media, total war, nuclear-as-deterrence) → information. Each expansion = its systems at declared tiers + its calibration corridors + 2–3 crisis archetypes.
- **Continuous tracks:** UI instruments (graphs, ledger, map modes — grown, never big-banged), chronicle engine, calibration battery, `TUNE` registry.

**Agent protocol per milestone:** dependency-ordered task packets, each = (system spec fields + relevant interaction-matrix row + kernel contract + acceptance tests). Stub twins: every system ships a canned-output fake so downstream systems build against stubs. Kernel freeze after M0 — kernel API changes require director sign-off. Agent-written unit tests never suffice for acceptance; property tests + calibration metrics are the gate.

---

## DELETED / DEFERRED (from v2, with reasons)

- **Lockstep-multiplayer viability (29.3)** — no second player will ever exist.
- **Fixed-point math analysis (28.2)** — single-machine float64 determinism suffices.
- **Spatial LOD tiers (3.7)** — replaced by small-world commitment; the conservation hazard disappears.
- **Mod ecosystem** (load order, conflict detection, sandboxed scripting, save-compat for mods) — DEV is the only modder and has source access. Data-driven content stays; the modding *platform* goes.
- **Localization pipeline, accessibility program, premium visual direction, asset plan beyond icons+counters+map** — audience of one.
- **Mechanical read/write contract enforcement tooling** — replaced by enforcement-by-API-shape.
- **Save migration engineering** — replaced by break-between-milestones + replay recovery.
- **Full GDD-first waterfall** — replaced by Spine + JIT specs. The three downstream docs survive in new form: `Architecture.md` ≈ S2+S3+S5; `SimulationSpec.md` ≈ the per-system JIT files + interaction matrix; `ImplementationRoadmap.md` ≈ Tier 2 + task packets.
- **Rescoped:** espionage theory-of-mind/bluffing → T3 late; nuclear layer → modern-era expansion, deterrence-abstraction only; space era → note; 12 crisis archetypes up-front → 2 at M8, 4 at slice, rest with their eras; "every number decomposes" → honest-scope Law 6.

---

## DECISIONS THAT MUST CLOSE BEFORE M0 (Decision Log entries, one recommendation each)

1. Language/stack (recommendation on file: C#/.NET).
2. UI approach (recommendation: immediate-mode dev-UI or local web dashboard; no game-engine UI stack).
3. Market solver (mandated above: local prices + damped adjustment + arbitrage flows).
4. Map substrate + cell count (recommendation: irregular cell graph, 40k default) and region-graph derivation.
5. Pop bucket key dimensions + cap + merge policy.
6. dt table + campaign turn budget + dt-authority rule (drafted above; ratify).
7. 1 pop unit = N persons; money-unit strategy.
8. Battles: resolved-only vs watchable — DEV's values call (S1). Architecture reserves a replaceable battle-resolution interface either way.
