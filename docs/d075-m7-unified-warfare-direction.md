# D-075: M7 unified warfare direction — the Director's decisions of 2026-11-03, recorded

**Status: RULED (Director, 2026-11-03).** This record is the administrative record of the Director's decisions of 2026-11-03 on the revised M7 design. It adds a record and edits no earlier one (gov-4 §6). It authorizes no implementation, build, commit or push. **M7 packets remain unauthorized until the Director authorizes them, packet by packet.**

## Provenance

**The decision.** The Director's revision "DIRECTOR'S REVISION: UNIFIED WARFARE ARCHITECTURE AND M7.1-M7.20", sections A–J. The Director confirmed this text as their 2026-11-03 decision.
- Source file: `/tmp/claude-0/-home-user-civDemo/54b82655-610f-500e-a185-8fae64bdd108/scratchpad/m7-1/m7-spec-revised.md`, lines 374–567, cited below as **REV:n**.
- Lines 1–373 of that file are the earlier agent draft, and no line of them is a Director decision.
- The untracked, LOCAL-only `docs/m7-spec.md` now holds the whole source file: its first 567 lines are byte-identical to it (draft lines 1–373 and REV lines 374–567; checked with `cmp` on 2026-11-03). A dated note is appended after line 567. The file is on no branch until it is tracked.
- The source file lives outside the repository. §1 below quotes every decision verbatim, so this record stands on its own.

**The Director's clarification of 2026-11-03, verbatim:**
> "All decisions I made today regarding the revised M7 design are FINAL and authoritative. They supersede any earlier conflicting decisions, rulings, ADRs, specifications, frozen clauses, design constraints or milestone plans. Do not ask me to reconsider decisions I have already made today. Do not treat earlier rulings as vetoes or reasons to resist the revised direction. Your responsibility is to update the governance record to reflect my decisions. Specifically: 1. Identify every earlier ruling that conflicts with today's decisions. 2. Record it as superseded, citing the exact replacement decision. 3. Preserve the historical text. Do not silently rewrite frozen records. 4. Where a formal amendment, supersession clause or ADR is required, draft and record it as the administrative consequence of my decision, not as a request for renewed approval. 5. Where today's decisions do not resolve a detail, leave that detail open and present it for my decision. 6. Do not invent additional constraints from earlier documents. 7. Do not reinterpret my decisions to preserve the old architecture. 8. Update the M7.2 docket, M7.3 contracts and dependency plan to reflect the revised direction. 9. Correct the E2 critical path using B7-11 → B7-12 → B7-15 → B7-16 → B7-18 → B7-24 → B7-25 → B7-29. Distinguish between: Decisions already made by the Director today; Administrative documentation required to record those decisions; Technical questions genuinely not yet decided. No implementation, builds, commits or pushes are authorized by this clarification. Continue the governance-recording work only."

**The status line of the revision is overtaken.** REV:376 reads: "**Status: PROPOSED FOR REVIEW.** … It does not silently amend binding rulings. In particular, conflicts with D-011, D-012, D-013 and CR-017 require formal rulings before implementation."
- Under the clarification, REV §A–J is the decision. The "PROPOSED" label is kept as historical text.
- **D-075 is the formal ruling for every conflict that §A–J resolves.**
- Where §A–J leaves a conflict unresolved, it stays OPEN (§5).
- REV:376's own scope limit still applies: the revision "supersedes conflicting proposals in the original draft only where stated". Each supersession in §2 is therefore scoped to the words of a REV clause or of the Director's instruction 9.

**Working inputs (secondary evidence, verified against `7d5defc` before use):**
- the governance reconciliation v2 (`…/scratchpad/m7-1/m7.2-governance-reconciliation-v2.md`, cited **v2:n**), including its B7 packet table, alias table and verifier errata;
- four independent verification maps (battle, visual, military, kernel).

Only rows those maps tagged CONFIRMED, CORRECTED or ADDED are used. Every repository citation below was re-opened at HEAD `7d5defc` (`main` = tag `m6-exit`) when this record was written.

**Three categories, used throughout:**
- **DECIDED**: decided by the Director on 2026-11-03.
- **ADMIN**: documentation required to record a decision. It is drafted as the consequence of the decision, not as a request for approval.
- **OPEN**: a technical question that is genuinely not decided. Each is listed in §5.

---

## 1. Decisions (DECIDED, Director, 2026-11-03)

Each decision is quoted verbatim from REV. A trailing "(open detail: O-n)" marks a part the revision itself leaves open; that part is not decided here.

### 1.1 Scope: a standalone, reusable military simulation

| # | REV | Decision (verbatim) |
|---|---|---|
| R1 | :380 | "M7 builds a reusable military simulation environment and standalone War Sandbox, not merely a campaign battle viewer." |
| R2 | :381 | "Campaign battles and sandbox battles use the same simulation kernel and domain modules." |
| R3 | :388 | "Decisions must be inspectable. M7 is initially a developer/test environment. Real-world war experimentation is out of scope until the whole game is integrated and tested." |

### 1.2 One kernel, domain modules, combined arms

| # | REV | Decision (verbatim) |
|---|---|---|
| R4 | :408 | "**Do not build disconnected battle engines.** Build one deterministic military simulation framework with domain-specific modules and a combined-arms interaction layer." |
| R5 | :412 | "The kernel owns the common simulation clock, pulse scheduler, internal steps, deterministic event ordering, entity identity/lifecycle, formation membership, order contracts, movement/spatial queries, detection/knowledge contracts, target acquisition interfaces, effects/damage application, replay, save/load, RNG ownership, inspection and event logging." (open detail: where this kernel lives in code, O-14) |
| R6 | :414 | "It must not become one giant collection of era-specific combat conditionals. Domain modules provide explicit capabilities and rules through stable contracts." |
| R7 | :420–424 | The B2 module table: **Land warfare** "Infantry, cavalry, archers, artillery, ground vehicles, melee, projectiles, armour and land formations"; **Air warfare** "Aircraft, helicopters, flight state, air-to-air, air-to-ground, interception and air defence interactions"; **Naval warfare** "Surface vessels, maritime movement/detection, naval weapons, damage, sinking, ports and transport"; **Siege operations** "Investment, blockade, siege works, bombardment, breaches, sorties, relief, starvation, assault and surrender"; **Combined-arms interaction** "Cross-domain detection, target classification, engagement eligibility, weapons, interception, support and effects". (open detail: naval resolution mode, O-1) |
| R8 | :426 | "A domain module may contain distinct equations and mechanics. It must reuse the kernel's clock, event ordering, identity, inspection, persistence and replay contracts." |
| R9 | :430 | "Combined arms is not another battle engine. It connects domain modules through capability contracts. Examples include aircraft attacking tanks, tanks engaging helicopters, ground air-defence engaging aircraft, ships engaging aircraft, aircraft attacking ships, and artillery engaging detected/designated ground targets." |
| R10 | :432 | "A cross-domain engagement follows a testable sequence: detect or receive valid target information → identify/classify as required → establish engagement eligibility → select compatible weapon/effect → resolve interception/defence → apply damage/effects → update knowledge and inspection records." |
| R11 | :434 | "Not every entity can attack every other entity. Compatibility derives from equipment, weapons, sensors, target class, range, altitude/depth, line of sight and relevant capability state. Do not add era gates where actual capability state should govern." |
| R12 | :537 | (§H, D-065 R3 row) "Build generic contracts; implement only ratified era/capability scope." |

**Reading recorded from the revision's own text.** "Multiple specialized battle engines" means domain modules on one kernel. The authority is REV §B: the heading at :406, "one simulation kernel, multiple domain modules"; :408, which forbids disconnected engines; and :426, which allows distinct equations inside a module.

### 1.3 Entities, formations and rendering

| # | REV | Decision (verbatim) |
|---|---|---|
| R13 | :383 | "Individual soldiers and machines exist as simulation entities. The player commands formations; subordinate entities act and position themselves under formation intent." |
| R14 | :448 | "**Simulation entity:** an individual soldier, vehicle, aircraft, ship, weapon team or other simulated object with type-appropriate state." |
| R15 | :449 | "**Formation:** the player-commandable group, owning formation orders, membership and command state. Individual entities execute formation intent through deterministic local behaviours." |
| R16 | :450 | "**Render representation:** visual models, instances, grouped representations or impostors chosen according to camera distance and performance budget." |
| R17 | :452 | "The renderer may reduce visual detail, animation frequency or model count. It must not merge or delete simulation identities. Simulation outcomes must be invariant under camera position, render quality, frame rate and LOD." |
| R18 | :534 | (§H, D-011 §2 row) "Compatible if individuals are subordinate simulation entities. Rule persistence/identity semantics." (open detail: persistence and identity, O-10) |
| R19 | :538 | (§H, D-065 R8–R10 row) "Keep formation as player-commandable family; subordinate members are not new player-commandable families absent a ruling." |
| R20 | :477 | "'Fusing ten units into one representation' is permitted only as a visual batching/LOD technique. It must never merge their simulation state or identities." |

### 1.4 Presentation: the 3D battlefield

| # | REV | Decision (verbatim) |
|---|---|---|
| R21 | :382 | "Visual direction is a high-quality Total War-style 3D battlefield. Graphics work starts during M7." |
| R22 | :473 | "Graphics work starts early, with a separate renderer/simulation boundary. Required foundations include 3D terrain, elevation, slopes, water, vegetation, roads, structures, fortifications, deployment zones, individual models, instancing, batching, culling, LOD, animation throttling, camera, selection, tactical map, overlays, inspection panels and simulation-driven effects." (open detail: host engine, O-13) |

### 1.5 Animator AI and Enemy AI

| # | REV | Decision (verbatim) |
|---|---|---|
| R23 | :384 | "**Animator AI** means visual animation/presentation only. **Enemy AI** means opposing-force decision-making. These must have separate interfaces, responsibilities, logs and tests." |
| R24 | :454 | "**Animator AI** converts simulation state and formation intent into visual spacing, facing, locomotion, weapon handling, hit reactions and death animations. It must not choose targets, invent movement, decide casualties, alter morale or change combat outcomes." |
| R25 | :456 | "**Enemy AI** selects formation-level orders using only permitted knowledge (`PolityKnowledge`/battle knowledge). It does not control visual animation." |
| R26 | :389 | "No autonomous tactical personality layer. Enemy AI makes inspectable decisions based on its knowledge, objectives and legal orders." |

### 1.6 Clocks, pulses and internal steps

| # | REV | Decision (verbatim) |
|---|---|---|
| R27 | :385 | "Tactical battles use fixed-duration pulses with smaller internal time steps. Both sides' orders resolve against a common evolving state. Movement, firing, charges, target acquisition, interruption and timed reinforcements can overlap within a pulse." |
| R28 | :462 | "**Campaign turn:** existing civilization turn with era-dependent `dtYears`." |
| R29 | :463 | "**Operational war pulse:** fixed interval for strategic movement, contact, reinforcement, siege progression and campaign operations. Its configured duration may be weeks in earlier eras and days in modern eras, subject to a dimensional audit and formal ruling." (open details: duration, O-2; the mechanism by which crossing campaign-turn boundaries is reconciled with the kernel contract and the sub-step rule, O-3. Crossing itself is ruled by the Director's ledger Part I §19, which stays binding: §3.) |
| R30 | :464 | "**Tactical battle pulse:** a much shorter fixed interval for battle resolution. It must be suitable for movement, firing, charges, interception and other events during continuous resolution." (open detail: length and count, O-4) |
| R31 | :465 | "**Internal simulation step:** smaller deterministic time increments/event processing within a pulse." (open detail: definition, O-5) |
| R32 | :469 | "During tactical pulse resolution, both sides' orders act against a common evolving state. … Define deterministic event priority, tie-breaking, collisions, interruptions, target invalidation, simultaneous effects and RNG consumption. Do not impose artificial movement-then-fire phases where they contradict the time model." (open detail: the rules themselves, O-6) |
| R33 | :533 | (§H, D-011 §1, §3; D-013 row) "Separate clocks. Amend D-013 only if tactical pulse budget/duration changes." |

### 1.7 Detection, movement, editing

| # | REV | Decision (verbatim) |
|---|---|---|
| R34 | :386 | "Realistic spotting, line of sight, concealment and terrain-based hiding are required. Forests and other cover must have meaningful tactical effects." |
| R35 | :535 | (§H, D-012 row) "Continuous rendering can coexist with deterministic spatial simulation. Audit whether the proposed grid supports movement, LOS and projectiles." (open detail: the spatial model, O-7) |
| R36 | :536 | (§H, CR-017 §3 and §5 row) "Resolve before movement implementation." (open details: O-8, O-9) |
| R37 | :387 | "Full terrain, structure and deployment editing is required." |

### 1.8 Historical authenticity

| # | REV | Decision (verbatim) |
|---|---|---|
| R38 | :390 | "Historical authenticity is a design objective. Shared interfaces must not flatten materially different historical weapons, formations, movement, detection, logistics and combat." |
| R39 | :442 | "Ancient/medieval melee, archery, cavalry and siege equipment must not be forced into modern fire-control assumptions. Modern warfare must support relevant distinctions such as armour, penetration, guided weapons, sensors, aircraft, air defence and combined-arms engagement. Shared interfaces are abstractions, not identical mechanics." |

### 1.9 Siege as a persistent operation

| # | REV | Decision (verbatim) |
|---|---|---|
| R40 | :438 | "A siege advances over operational/campaign time and owns investment, blockade, supply, starvation, construction, fortification condition, bombardment, sorties, relief, surrender and capture state. Tactical assaults use the land module. Naval blockade or amphibious action uses the naval/combined-arms modules. Siege must not duplicate combat resolution." (open detail: how starvation is computed, O-21) |
| R41 | :467 | "A siege does not run as one enormous tactical battle. Investment, supply, starvation and siege works progress over operational/campaign time; an assault invokes tactical battle resolution." |
| R42 | :532 | (§H, D-011 §5 / C5 row) "Preserve siege as operation. Director must decide whether naval battles remain auto-only or gain TacticalResolver support." (The naval half is OPEN: O-1.) |

### 1.10 War Sandbox

| # | REV | Decision (verbatim) |
|---|---|---|
| R43 | :481 | "The sandbox is a standalone mode using the same kernel and domain modules as campaign battles." (open detail: which resolver path sandbox AI-vs-AI and autoplay battles use, O-37) |
| R44 | :483–487 | "**Quick Battle:** configure sides, forces, terrain, conditions and deployment." · "**Scenario Editor:** edit terrain, elevation, vegetation, roads, water, structures, fortifications, objectives, deployment zones and timed reinforcements." · "**Battle Test:** controlled experiments, seeded repetitions, autoplay and parameter sweeps." · "**Replay and Inspection:** inspect orders, events, visibility, target decisions, movement, damage, casualties and causal explanations." · "**Campaign Encounter:** later load a real campaign encounter into the same simulation without changing combat rules." (open details: O-18, O-19, O-20, O-37) |
| R45 | :489 | "The first playable slice should prove a small land scenario, individual entities, formation orders, pulse resolution, replay and inspection. Graphics, terrain and simulation can proceed as parallel lanes after contracts are established." |

### 1.11 Caps and performance

| # | REV | Decision (verbatim) |
|---|---|---|
| R46 | :391–404 | "Initial proposed per-formation caps:" (Heavy/aggregated infantry 50 soldiers; Archers and lighter infantry 60 soldiers; Tanks 5 vehicles; Helicopters 3; Ships 5; Fixed-wing aircraft 1; Snipers 2 personnel; Mortars "5 weapon systems/crews, exact counted object to be defined"). "These are proposed limits, not measured performance guarantees. M7.1/M7.6 must define precisely what is counted for each family and validate recruitment, splitting, merging, casualties and save/load against the caps." **What is decided:** per-formation caps exist, these are the initial proposed values, and M7.1/M7.6 must define and validate them. **Not decided:** the values as final limits and the counted objects (O-11). |
| R47 | :475 | "The proposed formation caps are not a maximum battle-size guarantee. Benchmark simulation and rendering separately: CPU, GPU, memory, draw calls, animation cost, simulation throughput and worst-case entity counts. Set target hardware and measurable performance budgets before promising scale." (open detail: hardware and budgets, O-12) |

### 1.12 Task decomposition, acceptance criteria and the next step

| # | REV | Decision (verbatim) |
|---|---|---|
| R48 | :497–516 | The M7.1–M7.20 packet list, with each packet's name, deliverable and dependencies exactly as they stand in REV §G. Reproduced in §4.6. |
| R49 | :520–526 | The parallel lanes: "**Graphics:** M7.4 → M7.5 → visual work in M7.10/M7.18." · "**Simulation:** M7.6 → M7.7 → M7.8 → M7.9 → M7.11." · "**Campaign:** M7.14, then M7.13 and M7.19." · "**Domains:** M7.15 and M7.16 after shared contracts; M7.17 connects them." · "**AI/testing:** M7.12 and test-laboratory work in M7.18 as interfaces stabilize." · "Parallelism does not authorize incompatible implementations. Every lane uses M7.3 contracts and the same deterministic kernel." |
| R50 | :493 | "These are proposed task packets, not implementation authorization. Each packet needs its own scope, governing rulings, dependencies, acceptance tests and explicit authorization." **What is decided:** the decomposition is the plan, and each packet is authorized separately (O-22). |
| R51 | :542–553 | M7 acceptance criteria I.1–I.12, added to M7's exit definition:<br>1. "Land, naval and air modules use the same deterministic clock, event, identity, inspection, save/load and replay contracts."<br>2. "At least one cross-domain engagement runs end-to-end through the combined-arms layer."<br>3. "A formation is commanded as one unit while its individual entities act under its intent."<br>4. "Camera, LOD, animation detail and frame rate cannot alter simulation results."<br>5. "Terrain concealment demonstrably affects detection and is inspectable."<br>6. "Events can occur during a pulse, interrupt actions and resolve deterministically under simultaneous orders."<br>7. "Siege progresses on operational/campaign time; assault invokes land combat."<br>8. "A saved sandbox scenario reopens and replays identically."<br>9. "Performance reports entity counts, formation counts, frame rate, simulation throughput, CPU/GPU memory and stress results. Do not promise a maximum battle size before measurement."<br>10. "Representative eras demonstrate that shared interfaces have not flattened historical differences."<br>11. "Material decisions can be traced to inputs, knowledge, rules/capabilities, events and state changes."<br>12. "Campaign resolvers preserve the ratified common contract, parity and Ledger conservation." (open detail: parity tolerance, O-16) |
| R52 | :557–567 | "**M7.1 is a read-only foundations and architecture audit.**" It inspects the eight items at :558–565. "M7.1 must not implement the renderer or battle simulation. It produces a measured architecture audit, coupling/dependency map, binding-conflict list and M7.2 decision docket. After review, the Director can ratify the architecture and authorize M7.2/M7.3." **The Director's clarification counts as the ratification of the architecture.** This is a labelled reading, not a REV quote: REV:567 provides that the Director "can ratify the architecture", and the clarification of 2026-11-03 states "All decisions I made today regarding the revised M7 design are FINAL and authoritative". Authorizing M7.2 and M7.3 as packets to execute is still OPEN (O-22). |

### 1.13 The Director's instruction 9: the M7 critical path

| # | Source | Decision |
|---|---|---|
| R53 | Clarification, item 9 | **The M7 critical path is B7-11 → B7-12 → B7-15 → B7-16 → B7-18 → B7-24 → B7-25 → B7-29.** It corrects v2 §D.1 (v2:325), whose path "B7-00 → B7-02 → B7-03 → B7-07 → B7-09 → B7-10 → B7-11 → B7-15 → B7-22 → B7-29" is not the longest chain (v2's verifier, error E2, v2:566–570). |
| R54 | Clarification, item 9 (consequence of the critical path, R53) | **DECIDED as a consequence of the Director's instruction 9 critical path, which places B7-24 and B7-25 on M7's path: B7-24 (military buildings and calibration; charter stage M7.7) and B7-25 (Garrisoning, GA; charter stage M7.8) are M7 packets.** Both lie on M7's critical path into B7-29, "Whole-M7 integration, optimization and exit" (v2:313). This closes the GA placement question (v2 DEC-15, v2:452) as option (a), "M7 (B7-25)". **Not decided:** GA's design. D-065 R11 calls it "an approved future feature requirement" (`docs/d065-m7-charter-rulings.md:54`); GA design remains O-36. |

---

## 2. Supersessions

**How to read this table:**
- "In part" means the clause is superseded only to the stated extent. Everything else in the clause, including the stated retained part, stays binding.
- The text of every superseded clause is unchanged. Each affected file receives an appended dated note (§4.1).
- Line numbers are at `7d5defc`. "REV:n" in the right-hand column refers to the decisions in §1.
- **Rows S4 and S31 are withdrawn** by the corrections of 2026-11-03 ("Corrections applied — 2026-11-03", at the end of this record). S4 is reclassified OPEN (O-37); S31 is not a supersession. Wherever a range such as "S1–S13" or "S1–S15" is cited, in this record or in an appended note, it excludes S4.

### 2.1 D-011 (`docs/d011-battle-layer-addendum.md`, frozen by `docs/spine-s8-governance-freeze.md:16`)

| # | Earlier record: line — quote | Extent (retained part) | Replaced by |
|---|---|---|---|
| S1 | d011:4 — "Battle play happens *inside* the turn structure." | **In part**, sandbox battles only. **Retained** for campaign battles. The time nesting is O-3/O-5. | R43 (REV:481) |
| S2 | d011:4 — "closest real-world class: Ultimate General / Unity of Command, not Starcraft." | **In part**: the Ultimate General / Unity of Command reference class. **Retained:** "No individual-soldier control", formation-level command, "Total War's command layer, delivered turn-based", "not Starcraft". | R21 (REV:382) |
| S3 | d011:12–13 — "(era-gated additions: bombard, air strike, dig-in)" | **In part**: the words "era-gated". Additions are gated by capability state. **Retained:** the verb set, constant across eras. | R11 (REV:434) |
| S4 | d011:22 — "**AutoResolver** — Lanchester-family math. Used for all AI-vs-AI battles and whenever the player delegates." and d011:23 — "**TacticalResolver** — the watchable command battle above. Player-only." | **WITHDRAWN (not a supersession); reclassified OPEN as O-37.** REV:381 and :481 say the sandbox uses the same kernel and domain modules as campaign battles, and REV:485 lists "autoplay" for Battle Test, but no REV line says which resolver sandbox AI-vs-AI or autoplay battles use. Both d011:22 and d011:23 therefore stay as written until the Director rules on O-37. **Retained** for campaign battles under every O-37 alternative. | — (O-37) |
| S5 | d011:26 — "`BattleSetup` comes entirely from the strategic sim" | **In part**, sandbox only: the sandbox configures its own setups. **Retained** for campaign battles (REV:487, :515). Sandbox legality is O-19. | R44 (REV:483) |
| S6 | d011:34 — "**Trigger:** armies contact during the military phase → prompt: *Command* (enter battle) or *Delegate*" | **In part**, sandbox only: the sandbox starts battles without campaign contact. **Retained** for campaign battles, including "battles read frozen turn-start state per the sub-step rule". | R43, R44 (REV:481, :483) |
| S7 | d011:35 — "**Battlefield:** generated from the contact cell's actual terrain" | **In part**, sandbox only: Scenario Editor terrain. **Retained** for campaign battles. The grid is O-7; campaign-encounter editing is O-20. | R44 (REV:484) |
| S8 | d011:36 — "**Formations are atomic:** warband → phalanx/cohort → tercio → line battalion → armored company → air wing. Each carries strength, cohesion, fatigue, ammo …" | **In part**: "atomic" no longer bars individually simulated members. **Retained:** the formation is the command unit, and the ladder's family names. Ladder sizes against the proposed caps, and stored versus member-derived attributes, are O-11. | R13, R14, R15, R19 (REV:383, :448–449, :538) |
| S9 | d011:41 — "Formations render as **unit tokens/blocks** …"; "each token drawn as a *cluster of tiny sprites* … well inside MonoGame + no-art-team reality"; "What v1 will **not** be: 3D soldiers and camera cinema." | **In part**: tokens and blocks as *the* battle presentation, the sprite-cluster flourish and its "no-art-team reality" premise, and "3D soldiers". **Retained:** strength and morale bars, banners, facing arrows, combat feedback, and "The drama comes from stakes". **OPEN:** "camera cinema" (O-27) and "MonoGame" (O-13). | R16, R21, R22, R24 (REV:382, :450, :454, :473) |
| S10 | d011:45 — "v1 ships ancient/classical warfare only" | **Superseded** for M7 scope. Implementation stays bounded by R12 (REV:537). | R7, R51 (REV:420–422, :516, :551) |
| S11 | d011:45 — "Later eras arrive as **data + a few new verbs**" | **In part**: domain modules may carry distinct equations and mechanics. **Retained:** later eras arrive with data. | R8, R39 (REV:426, :442) |
| S12 | d011:45 — "air as *strike missions on the battle map* — never dogfight simulation" | **Superseded**: strike-only air and "never dogfight". Air-to-air is within the air module. Implementation is bounded by R12. | R7, R12 (REV:421, :512, :537) |
| S13 | d011:45 — "Naval command battles: post-slice candidate; naval stays auto-resolve until then." | **In part**: naval as a deferred domain (the naval module is M7.15). **OPEN:** "stays auto-resolve" (O-1). | R7, R42 (REV:422, :511, :532) |
| S14 | d011:60–61 — "The battle mechanics in §1–§5 and the decisions in §8 are unchanged" | **In part**: "unchanged", to the extent of S1–S13 only. | as S1–S13 |
| S15 | d011:82 — "D-002 (MonoGame + ImGui.NET): *strengthened* — MonoGame is a genuine 2D game framework; the battle map is squarely in its lane." | **In part**: the premise that the battle map is a 2D surface. D-002 and ADR-009 as host stack are O-13. **Retained** (as claims, open on adequacy, O-14): "Kernel contract: already supports sub-stepped military phases and order-logged player input", and the M4 interface sentence. | R21, R22 (REV:382, :473) |

### 2.2 Other battle and visual records

| # | Earlier record: line — quote | Extent (retained part) | Replaced by |
|---|---|---|---|
| S16 | `docs/d039-command-fog-and-siege.md:99–100` — "C4. The visual treatment falls under D-038's object layer and the M6 symbology inheritance (D-038 E4)." | **In part**, for the M7 3D battle view. **Retained** for the campaign map. | R21, R22 (REV:382, :473) |
| S17 | d039:152–153 — "Battles that arise drop into D-011's existing pulse layer." | **In part**: the pulse layer is restructured into tactical pulses with internal steps. **Retained:** E3's campaign layer as "STRATEGIC DECISIONS BETWEEN BATTLES" (:149–151). How the operational war pulse relates to E3's intervals is O-2/O-3. | R27, R30, R31 (REV:385, :464–465) |
| S18 | d039:179 — "F1. Does not amend D-011's verbs, formations-as-tokens, or WEGO structure." | **In part**: "formations-as-tokens". **Retained:** the verbs, WEGO, and the formation as the command unit. | R15, R21, R22 (REV:382, :449, :473) |
| S19 | `docs/d038-visual-target.md:13` — "The rendered 3D world (Civilization VI proper) is REJECTED" | **In part**: only as the rejection reaches the battlefield (through E4, :64). **Retained** for the campaign map, which today does not address. | R21 (REV:382, :473) |
| S20 | d038:29 — "C1. NO ANATOMICALLY DETAILED CHARACTERS. Not now, not later, at any milestone. People never render as figures with faces, limbs or animation cycles." | **In part**, for the M7 battlefield and War Sandbox: animated individual figures are decided. Superseding the prohibition does not mandate faces. **Retained** for the campaign map. | R16, R22, R24 (REV:450, :454, :473); supporting R13 (REV:383) |
| S21 | d038:33 — "C3. WHERE PEOPLE MUST APPEAR THEY ARE SILHOUETTES OR TOKENS. This is already ruled for battles — D-011 §4 …" | **In part**, battlefield and War Sandbox (follows S9). **Retained** for the campaign map. | as S20 |
| S22 | d038:58 (2026-10-12 dated note) — "C1 (no figures) stands." | **In part**, as S20. | as S20 |
| S23 | d038:64 — "E4. … D-011 §4 already specifies how battle formations render. If the visual language exists first, M6 INHERITS it rather than inventing a second one." | **In part**: the M7 3D battlefield's visual language is not inherited from the map's token language (follows S9). | R21, R22 (REV:382, :473) |
| S24 | `docs/d052-m5-integration-audit-rulings.md:77–78` — "D-038 C1 (no figures) stands." | **In part**, as S20. **Retained:** the M6 scheduling around it (:72–77). | as S20 |
| S25 | `docs/style-bible-parchment.md:122` and `:124` (§7, restating D-038 C1 and C3) | **In part**, as S20 and S21. **Retained** for the map. | as S20 |
| S26 | `docs/m6-plan/e1-visual-acceptance-checklist.md:87` — "No anatomically detailed characters, ever" | **In part**: "ever", as to the battlefield. **Retained** as M6 E1's map check. | as S20 |
| S27 | `docs/design/recovered-decisions-architecture-invariants.md:311` — A-123: "D-002 (MonoGame + ImGui.NET): *strengthened* …" | **In part**, as S15. Data recorded, not edited: A-123 cites its source as `d011-battle-layer-addendum.md:68-70`, but §7 is at d011:82. | as S15 |
| S28 | `docs/civ-sim-architecture-v3-outline.md:52` — "**UI doctrine — the debug UI IS the game UI.**" | **In part**, for the M7 battlefield only. **Retained** for panels and data display and the campaign map. | R21, R22 (REV:382, :473, :500) |
| S29 | spine:150 — "Localization pipeline, accessibility program, premium visual direction, asset plan beyond icons+counters+map — audience of one." | **In part**, for the M7 battlefield only: "premium visual direction" and "asset plan beyond icons+counters+map". **Retained:** localization and accessibility (not addressed), and the campaign map. | R21, R22 (REV:382, :473, :500) |
| S30 | spine:90 — "Military full (ops, siege, naval) \| M9+ \| T2 \| battle module behind replaceable interface" | **In part**: the M9+ placement of operations, siege and naval. They are M7 packets (M7.13, M7.14, M7.15). **Retained:** "battle module behind replaceable interface". "T2" is not addressed. Air in M7 is new scope, not a supersession of this row. | R48 (REV:509–511) |

### 2.3 Milestone, freeze and governance records

| # | Earlier record: line — quote | Extent (retained part) | Replaced by |
|---|---|---|---|
| S31 | `docs/milestones.md:24` — "M7 \| Battle Layer \| future" | **WITHDRAWN (not a supersession).** The row states a name, a position and a status, and no scope, so R1 (REV:380) supersedes nothing in it. **Retained:** the whole row. The name against R1's wider scope is O-28. | — (O-28) |
| S32 | milestones:97–98 — "D-011's battle mechanics are unchanged — only their milestone number and their dependency on M6." | **In part**: "unchanged", to the extent of S1–S15. **Retained:** the M6 dependency and :93, "CONSUMES M6's authoritative technology/capability state" (consistent with R11). | as S1–S15 |
| S33 | `docs/spine-s8-governance-freeze.md:16` — "All CLOSED decision-log entries: … D-011 (dual-resolver battle contract)" | **In part**: D-011 remains frozen except for the extents in S1–S15. **Retained:** the dual-resolver contract and the parity rule. | as S1–S15 |
| S34 | S8:17 — "The milestone ladder order M0→M11+ and each milestone's exit-criteria *definitions*." | **In part**: M7's exit-criteria definition gains I.1–I.12. **Retained:** the ladder order; no milestone moves. Recorded through ADR-060 (§4.2). | R51 (REV:540–553) |
| S35 | `CLAUDE.md:25` — "The Spine, kernel contract, closed D-decisions, and milestone order are FROZEN." | **In part**, for the closed D-011 entry, to the extents of S1–S15. Recorded in D-075 and ADR-060 only. `CLAUDE.md` is not edited. | as S1–S15 |
| S36 | `docs/adr/cr-017-ages-surge-and-mobile-agents-vs-frozen-items.md:320–321` — "The ruling should also say explicitly whether a War Pulse is a fixed span of sim-time." | **Answered**: both the operational and the tactical pulse are fixed intervals. This holds whichever REV pulse the §19 War Pulse corresponds to. Durations are O-2/O-4. CR-017 §3 Options A–C (Action Capacity) stay O-8. | R29, R30 (REV:463, :464) |
| S37 | `docs/d065-m7-charter-rulings.md:51` (relation column) — "D-011 (formations are atomic)" | **In part**, as S8. **Retained:** R8's body (the architecture rule). | R13, R15, R19 (REV:383, :449, :538) |

### 2.4 M7 planning records (`PROPOSED` status; superseded so they are not read against today's decisions)

| # | Earlier record: line — quote | Extent (retained part) | Replaced by |
|---|---|---|---|
| S38 | `docs/m7-plan/unit-catalogue-and-progression.md:93–98` — "**Formation-level battle stays.** D-011 is unchanged: formations are atomic; …" | **In part**: "D-011 is unchanged" and "atomic", as S8/S14. **Retained:** WEGO, the shared contract, the parity rule, and "There is no individual-soldier control." Pulse count is O-4. | as S8, S14 |
| S39 | charter:136 — "There are no per-soldier stats." | **Superseded**: entities carry "type-appropriate state". **Retained** in the same cell: "Verbs stay era-constant (…)". | R14 (REV:448) |
| S40 | charter:192 — "An atomic formation inside an Army MobileAgent" | **In part**, as S8. | as S8 |
| S41 | charter:487 — "D-011 verbs: … bombard; air strike" | **In part**: the air-strike mechanism, which is now air-to-ground in the air module through combined arms. **Retained:** the formation-level order set. | R7, R9 (REV:421, :430, :513) |
| S42 | charter:848 — "Intercept (a reaction against strike missions on the battle map, D-011 §5)" | **In part**: ground air defence engages aircraft through combined arms, not only as a reaction against strike missions. | R9 (REV:430) |
| S43 | charter:1025 — "Strike missions on the battle map, never dogfight simulation (D-011 §5)." | **Superseded**, as S12. | as S12 |
| S44 | charter:1100 — "Strike: on the battle map (D-011 §5)" | **In part**: the strike mechanism, as S41. **Retained:** the structure strike as a construction-state debit. | as S41 |
| S45 | charter:1538 — "strike missions on the battle map (D-011 §5)" | **In part**, as S41. | as S41 |
| S46 | charter:1633–1640 — §14 stages M7.0–M7.7 | **Superseded as plan stages** by REV §G. Their content is redistributed through the alias table (§4.5). The M7.0 items stay OPEN (O-4, O-7, O-8, O-9; Q4 stays open). M7.3 is historical (delivered under ADR-055). The "(auto-resolve)" at :1639 is O-1. M7.7 is B7-24 (R54). | R48, R53 (REV:497–516) |
| S47 | charter:1641 — "M7 (proposed placement; the Director confirms)" | **Superseded**: GA is in M7 as B7-25. Its dependency labels "M7.2 … M7.4 … M7.7" are charter aliases, mapped to B7-09, B7-14 and B7-24 (v2:309). **Not decided:** the seven-part design (O-36). | R54 |
| S48 | charter:1803 — "the Director confirms the M7.8 placement" | **Superseded**: answered by R54. | R54 |
| S49 | `docs/m7-plan/aviation-progression.md:160` — "Light bombing on the battle map (D-011 §5 strike mission)." | **In part**, as S41. | as S41 |
| S50 | aviation:274 — "Strike mission on the battle map; intercept reaction" | **In part**, as S41/S42. | as S41 |
| S51 | aviation:282–283 — "carrier combat as its own battle mode: naval battle stays auto-resolve (D-011 §5); carrier sorties feed the ordinary strike verb;" | **In part**: only in that ship–aircraft engagements are M7 content. "naval battle stays auto-resolve" is O-1. **Retained (not addressed):** carrier combat as a battle mode and carrier sorties. | R9, R51 (REV:430, :513) |
| S52 | aviation:284 — "air-to-air 'dogfight' simulation, which is barred by D-011 §5." | **Superseded**, as S12. | as S12 |
| S53 | aviation:370–373 — "**Carrier combat is not M7 battle content.** … Naval battle stays auto-resolve … A naval command battle is a post-M7 candidate." | **In part**: only in that it would bar aircraft–ship engagements that involve carriers as ships. The naval resolution lines are O-1. **Retained:** carrier sorties, carrier battle mode, and carriers realized at M11+ (charter:1646). | R9 (REV:430, :513) |
| S54 | `docs/m6-plan/m7-handoff-register.md:3–6` — "D-011 (frozen, `spine-s8:16`) owns battle mechanics" | **In part**: D-011's ownership yields to D-075 for atomicity (S8), air-to-air (S12), air defence and combined arms (R9), and the pulse structure (R27–R33). Naval resolution is O-1. **Retained:** "CONSUMES M6's authoritative technology/capability state". Data recorded, not edited: the header cites `milestones.md:53-58`, but the quoted text is at `milestones.md:93-98`. | as S8, S12; R9, R27–R33 |
| S55 | handoff:14 (H7-04) — "Everything in D-011 §1–§3 and §8; D-012/D-013 close at the M7 spec" | **In part**, to the extents of S1–S15. D-012/D-013 are O-7/O-4. | as S1–S15 |
| S56 | handoff:24 (H7-14) — "the Director confirms the M7.8 placement (D-065 Addendum 3)" | **Superseded**: answered by R54. | R54 |
| S57 | handoff:51–54 — "It depends on M7.2 (location, orders), M7.4 (battle, fortification term), M7.7 (city buildings) and save/load. … The Director confirms the placement." | **Superseded** as to placement (R54). The stage labels are aliases (B7-09, B7-14, B7-24). | R54 |

### 2.5 The earlier agent draft (REV:1–373; LOCAL copy `docs/m7-spec.md`, untracked)

The draft was never ratified. These rows record which of its sections REV §A–J replaces, so it is not read as a competing plan.

| # | Draft: line — quote | Extent | Replaced by |
|---|---|---|---|
| S58 | REV:24 — "**What M7 is, in one paragraph.** M7 makes armies real. …" | **In part**: M7 is also a reusable military simulation environment and a standalone War Sandbox. | R1, R2 (REV:380–381) |
| S59 | REV:202–232 — "§4 TASK PACKETS — PROPOSED" (T7.1–T7.16), including the critical path at :225, "T7.1 → T7.2 → T7.5 → T7.6 → T7.8 / T7.11 → T7.16" | **Superseded**: replaced by REV §G (M7.1–M7.20) and the Director's critical path. T7 labels are aliases (§4.5). | R48, R49, R53 |
| S60 | REV:304 — "GA \| **Garrisoning placement** \| M7.8, as charted (T7.15) \| M8" | **Superseded**: GA is in M7 as B7-25. | R54 |
| S61 | REV:76 — "**ASSUMED:** naval movement and ports … are a late, separable packet that can be cut to M7.x without failing the exit (§7 N1)"; REV:305 (N1); REV:329 "naval is separable (N1)" | **Superseded**: the naval module is an M7 packet (M7.15), and I.1 requires naval to share the kernel contracts at exit. "It stays auto-resolve (C5)" at :76 is O-1. | R7, R48, R51 (REV:511, :542) |
| — | REV:50 (C1, "6–12 command pulses"), REV:54 (C5, "Naval battles always auto-resolve"), REV:299–300 (D-012, D-013 recommendations), REV:331 ("snipers as an attached capability"), REV:260–273 (§5 exit criteria, PROPOSED) | **Not superseded and not decided.** They restate D-011 or make proposals. They stay OPEN as O-4, O-1, O-7, O-32. Draft §5 stays a proposal; REV §I (R51) is decided and adds to the exit definition. | — |

### 2.6 Questions in the reconciliation v2 that today's decisions close

| v2 item | Closed as | By |
|---|---|---|
| DEC-1, "Record REV" (v2:435) | Recorded, by this D-075 | Clarification |
| DEC-2, figures and 3D (v2:436) | 3D battlefield with individual figures: S9, S20–S23. **Not closed:** the host engine (O-13) and the campaign map (unchanged) | R21, R22, R24 |
| DEC-6, air-to-air (v2:441) | Air-to-air is within the air module (S12) | R7, R12 |
| DEC-8a, sandbox setup source (v2:443) | The sandbox configures its own battles (S5) | R44 (REV:483) |
| DEC-15, GA placement (v2:452) | (a) M7, as B7-25 | R54 |
| DEC-16, siege boundary (v2:453) | Operation over operational/campaign time, with assaults through the land module. The starvation interplay is O-21 | R40, R41 |
| §D.1 critical path (v2:325) | Corrected | R53 |

---

## 3. Still binding and compatible

These records are untouched by today's decisions, or are consistent with them. Citations are at `7d5defc`.

- **D-011 §2, the dual-resolver contract**, for campaign battles: `IBattleResolver`, identical inputs and outputs, Ledger casualties, per-pulse order log, the parity rule, and delegation (`docs/d011-battle-layer-addendum.md:18–30`). It is consistent with R51 I.12 and REV:515. D-011:4 "No individual-soldier control" is consistent with R13/R15. D-011:10 simultaneous WEGO resolution is refined by R27/R32. D-011:45 "Sieges remain multi-turn strategic operations that can spawn assault battles" is consistent with R40/R41.
- **D-039** Parts A–E (`docs/d039-command-fog-and-siege.md`): delay, not degradation; reconnaissance; stale ghosts (REV:504); fortification computed (D1, which does not conflict with editor-placed structures in the sandbox); D5 starvation on the food system (its interplay with R40 is O-21).
- **D-065** R1–R4, R8 body, R9, R10, R12, R13 and R14 (`docs/d065-m7-charter-rulings.md:16–19, :51–57`). R3 is applied through R12. R10 against R13 is O-33. R13 and R14 are O-1 and O-32.
- **ADR-055 and D-067 R15–R19** (`docs/adr/adr-055-m7-recruitment-personnel-reequip.md`; `docs/d067-m6-exit-gate-director-rulings.md`), including "Soldiers remain immortal until M7.4" (`d067:88`). The label "M7.4" is an alias (§4.5); which packet ends immortality is O-35.
- **The design laws and the kernel contract** (`CLAUDE.md:16–21`; `docs/m0-kernel-spec.md`, including D-004 and the §3.1/§3.3/§3.5/§3.8 contracts for campaign `WorldState`). The sub-step rule (`docs/civ-sim-architecture-v3-outline.md:33`) holds for campaign battles; how operational pulses relate to it is O-3/O-14.
- **The milestone capability rule** (`docs/milestones.md:93–96`), consistent with R11. M7 carries movement and Action Points (`milestones.md:36`; D-052 §4).
- **ADR-019** §1.2 and §1.4 (`docs/adr/adr-019-architecture-constitution-addendum.md:45–53, :79–81`). Not superseded: §1.4 denies only "a second global clock". O-29 and O-30 decide whether any conflict arises.
- **The Director's ledger, Part I §19 "WAR PULSES", and §26 item 18** (`docs/design/director-architecture-and-decision-ledger.md:941–956` and `:1109–1111, :1130`; cited **ledger:n**; tracked at `7d5defc`). **Still binding, and compatible with today's clock decisions R29/R30 (REV §D, REV:458–469).** Not presented as open.
  - ledger:943: "War Pulses can cross strategic turn boundaries."
  - ledger:950: "players retain full operational control" (in the list "During War Pulses:", :947).
  - ledger:955–956: "Do not reintroduce a blanket rule prohibiting player operational control during War Pulses."
  - ledger:1130, §26 "PROHIBITED REINTRODUCTIONS" ("Future work must not silently reintroduce:", :1111), item 18: "Blanket player-control prohibition during War Pulses."
  - **Status.** Part I is a "RATIFIED DECISION REGISTER" (ledger:38). The repository annex marks "§19 War Pulses \| RATIFIED" (ledger:1474) and "§26 prohibited reintroductions \| RATIFIED" (:1481).
  - **Why it is compatible.** REV:463 defines the operational war pulse as a "fixed interval for strategic movement, contact, reinforcement, siege progression and campaign operations", and REV:464 the tactical battle pulse. No REV line says whether pulses nest inside a campaign turn or cross it, and no REV line restricts player operational control during pulses. Nothing in today's decisions conflicts with §19, so §19 is not superseded (clarification items 6 and 7 do not reach a ruling that does not conflict). §19's War Pulse carries military agents' Action Capacity, battles, retreats and territorial change (ledger:949–953); this record reads it as REV's operational war pulse (REV:463), whose listed content is the same kind. If the Director meant otherwise, that is part of O-3.
  - **What stays OPEN is only the mechanism.** The ledger's own annex R4.10 (ledger:1538–1544) records that D-043 B8, "War Pulses operate inside the strategic turn" (:1539), is superseded in part; that "Part I §19 … governs" (:1540–1541); and: "Still OPEN. The collision with the frozen kernel contract, the sub-step rule and D-011: CR-017 §6 together with CR-006 §1" (:1543–1544). That mechanism is O-3.
- **The Director's ledger, Part I §24 "VISUALIZATION", for the world map** (ledger:1060–1079; "§24 visualization read-only \| RATIFIED", ledger:1479). "For MobileAgents: … one reported agent -> one visual token" (ledger:1071–1074), with "Internal composition may appear as labels/counts" (:1077). **Retained for the campaign/world map.** Compatible with R16 (REV:450): render representations on the battlefield draw simulation entities inside a battle, and battlefield entities are not world-map agents.
- **The Director's ledger, Part I §17.2 "Action Capacity" and §18 "MOVEMENT"** (ledger:861–872, :910–937; "RATIFIED", ledger:1472–1473). Still binding; they are the ratified baselines under O-8 and O-9 (§5). Applying them to frozen items stays OPEN as the ledger itself records (R4.11, ledger:1546–1554; CR-017 §3 and §5).
- **ADR-009** (MonoGame + ImGui.NET, `docs/adr/adr-009-ui-stack.md`) and **D-002** (`docs/m0-kernel-spec.md:9`), pending O-13. **ADR-022** and **CR-013** (Linux reference platform), pending O-17.
- **D-069** R1–R4 (`docs/d069-originality-and-third-party-game-references.md`). "Total War-style" is a style descriptor in the Director's decision, not a copy of another game. Packet text uses genre terms.
- **D-038 for the campaign map**: A3's atlas direction, B1/B2, C1–C4 as they apply to the map, D1–D3, E6, F1–F4, G1–G4 and Part H (`docs/d038-visual-target.md`). The guard tests that draw only campaign-map tokens (`Sim.Ui.Tests/ObjectLayerTests.cs`, `WorldLens.FormationToken`; `Sim.Ui.Tests/UnitTokenDetailTests.cs:293`, `WorldLens.UnitToken`) stay valid.
- **Spine** "Spatial LOD tiers (3.7) — replaced by small-world commitment" (`docs/civ-sim-architecture-v3-outline.md:148`). Compatible: R17 and R20 confine LOD to rendering.
- **Unaddressed planning content stays as written**: the charter's verbs "stay era-constant" (charter:136), carriers realized at M11+ (charter:1646), and aviation's not-in-scope list (`docs/m7-plan/aviation-progression.md:464`).

---

## 4. Administrative consequences (ADMIN)

Everything in this section follows from §1 and §2. None of it is a request for approval. **State of the tree (corrected 2026-11-03; see "Corrections applied — 2026-11-03" at the end):** the §4.1 dated notes are appended to all 17 listed files (the `adr-055` and `d067` alias-only notes in the corrections pass); ADR-060 (§4.2), the plan of §4.6–§4.8 (`docs/m7-plan/m7-2-docket-m7-3-contracts.md`) and the `docs/m7-spec.md` note are written; `docs/current-state.md` carries a routing block. All of it is LOCAL and uncommitted. Nothing is staged, committed or pushed.

### 4.1 Dated notes to append

Each affected file gets a section at its very end titled "## Dated note — 2026-11-03 (D-075; appended, the text above is unchanged)". Each note lists that file's S-rows and points to this record. No existing line is edited.

| File | Last line at `7d5defc` | S-rows |
|---|---|---|
| `docs/d011-battle-layer-addendum.md` | 91 | S1–S15 (S4 withdrawn: O-37; a correction section is appended after the note) |
| `docs/d039-command-fog-and-siege.md` | 229 | S16–S18 |
| `docs/d038-visual-target.md` | 212 | S19–S23. States that D-038 C1/C3/E4 reach battles, and that the supersession is bounded by today's decision (battlefield and War Sandbox), not by D-038 :5. |
| `docs/d052-m5-integration-audit-rulings.md` | 258 | S24 |
| `docs/style-bible-parchment.md` | 223 | S25. One scope sentence covers the map restatements at :84, :87 and :214, which are compatible. |
| `docs/m6-plan/e1-visual-acceptance-checklist.md` | 120 | S26 |
| `docs/design/recovered-decisions-architecture-invariants.md` | 456 | S27, with the stale `:68-70` citation recorded |
| `docs/civ-sim-architecture-v3-outline.md` | 167 | S28–S30 |
| `docs/milestones.md` | 751 | S31 (withdrawn; a correction section is appended after the note), S32 |
| `docs/adr/cr-017-ages-surge-and-mobile-agents-vs-frozen-items.md` | 714 | S36. §3 and §5 remain open as O-8/O-9. |
| `docs/d065-m7-charter-rulings.md` | 57 | S37; R54 (GA placement under R11) |
| `docs/m7-plan/unit-catalogue-and-progression.md` | 1865 | S38–S48, plus the stage-label aliases at :560 ("M7.3"), :736 ("M7.4") and :1594 ("M7 slice 6") |
| `docs/m7-plan/aviation-progression.md` | 480 | S49–S53, plus the alias label "M7.5" at :379 |
| `docs/m6-plan/m7-handoff-register.md` | 114 | S54–S57, plus the alias label at :108 ("M7.4") and the stale `milestones.md:53-58` citation |
| `docs/adr/adr-055-m7-recruitment-personnel-reequip.md` | 265 | Alias only: "M7.4" at :89, :243, :261 (§4.5). No supersession. Appended in the corrections pass. |
| `docs/d067-m6-exit-gate-director-rulings.md` | 199 | Alias only: "M7.4" at :88. No supersession. Appended in the corrections pass. |
| `docs/m7-spec.md` (untracked, LOCAL only) | — (lines 1–567 are the source file) | S58–S61. A note here exists on no branch until the file is tracked. |

**Not annotated:**
- ADR-019 (not superseded; see O-29/O-30).
- ADR-056 (its label "(the units track, M7.3)" at `docs/adr/adr-056-economy-packets.md:104` is a charter-stage alias, covered by §4.5: charter M7.3 was delivered under ADR-055).
- `Sim.Data/content/unit-families.json` (data; no implementation is authorized).
- `docs/spine-s8-governance-freeze.md` (S33 and S34 are recorded through ADR-060, which is the instrument S8:13 names).

### 4.2 ADR-060 records the architecture

ADR-060 is the architecture record that follows from this ruling. The number is free at `7d5defc`. ADR-060 is written as the untracked LOCAL file `docs/adr/adr-060-unified-warfare-simulation-architecture.md`. Its open list (§4) carries the same ids and content as §5 of this record (corrected 2026-11-03; see "Corrections applied — 2026-11-03" at the end). Whatever text is filed as ADR-060 must record:
1. The architecture of R4–R45: kernel and modules, the three layers of entity, formation and render representation, the Animator AI / Enemy AI separation, the clocks, the sandbox, siege as an operation, and the renderer/simulation boundary.
2. The Contradiction-Report component of S8:13 ("change requires a Contradiction Report + director ADR"), as the five fields of S8:41: frozen items in conflict (S1–S15, S33–S35); evidence (this record's §2); options (the Director's chosen direction); blast radius (§4.1, §4.3); recommendation (as ruled). These fields are carried inside ADR-060 instead of a separate `cr-027`. That choice is an administrative step, not a Director question.
3. The override price that S8:37 requires: "what breaks, which tests and docs change, and the schedule price".
4. The change to M7's exit-criteria definition (S34, R51).

Each open detail in §5 is named in ADR-060 as open. It is not resolved there.

### 4.3 Code and tests that state superseded rules: future follow-ups, unchanged now

- **`Sim.Ui.Tests/ObjectLayerTests.cs` and `Sim.Ui.Tests/UnitTokenDetailTests.cs`.** Their comments read D-038 C1 as universal, but the tests draw only campaign-map tokens. Rescoping those comments is a follow-up for a future authorized packet. The tests stay as they are.
- **Doc comments in `Sim.Ui/World/WorldLens.cs` and `Sim.Ui/World/UnitEmblem.cs`.** Campaign map; no action.
- **New battle-simulation code.** Any such code falls under the banned-constructs scan (CLAUDE.md Law 5; `adr-009:45–46`). Which project hosts it is O-14.
- **No code, data, schema, test or golden is changed by D-075.**

### 4.4 The `CLAUDE.md` milestone line

Unchanged. It still reads "M7 Battle Layer — spec in preparation, NOT ratified; no active packets until its spec is ratified". It changes only at a milestone exit gate or on the Director's authorization of M7 packets.

### 4.5 Packet numbering and aliases

- **REV M7.1–M7.20 are the M7 milestone packets.**
- **The B7-nn ids of v2 (v2:280–313) are sub-packet ids mapped to them.** The Director's instruction 9 uses them.
- B7-24 and B7-25 are in the plan by R53/R54. Formal adoption of the B7 scheme beyond that use is O-24.
- **Old labels are superseded aliases.** They are historical and are never reused for new work.

| Old label | Source | Now |
|---|---|---|
| Charter M7.0 (rulings first) | `charter:1633` | M7.2 (B7-02) |
| Charter M7.1 (catalogue and schema) | `charter:1634` | B7-08 (v2 proposal; no REV packet) |
| Charter M7.2 (movement, AC, ZOC) | `charter:1635` | M7.7 (B7-09) |
| Charter M7.3 (recruitment) | `charter:1636` | none: delivered under ADR-055 (D-067 R15) |
| Charter M7.4 (resolvers, siege, starvation) | `charter:1637` | M7.11 (B7-15), M7.13 (B7-18), M7.19 (B7-23); v2's B7-14 is a proposal |
| Charter M7.5 (reconnaissance, generals, category B) | `charter:1638` | reconnaissance → M7.8 (B7-10). Generals and category-B allocations have no owner yet (O-34). |
| Charter M7.6 (water, ports, naval) | `charter:1639` | M7.15 (B7-19) |
| Charter M7.7 (military buildings) | `charter:1640` | B7-24 (R54) |
| Charter M7.8 GA | `charter:1641` | B7-25 (R54) |
| EC-1, EC-2, EC-3 | `charter:1642` | **none: done.** Ratified (D-067 R16–R18, `d067:99–101`, "**RATIFIED**") and delivered (commit `f7a59b8`, "economy packets EC-1..3", an ancestor of `7d5defc`). v2's B7-26, B7-27 and B7-28 (v2:310–312) are stale and are not mapped. |
| ADR-055 / D-067 "M7.4" | `adr-055:89, :243, :261`; `d067:88` | the packet that ends soldier immortality (O-35); v2 proposes B7-04 |
| `current-state.md` "M7.8" | `current-state.md:99` | B7-25 |
| Draft T7.1 … T7.16 | REV:208–223 | T7.1→M7.1; T7.2→M7.2; T7.3→B7-08; T7.4→B7-13; T7.5→M7.7; T7.6→B7-14; T7.7→B7-04; T7.8→M7.13; T7.9→M7.8; T7.10→unplaced (fog and economy routing; the Director places it); T7.11→M7.11; T7.12→M7.12; T7.13→B7-24; T7.14→M7.15; T7.15→B7-25; T7.16→M7.20 (v2:363–380) |

### 4.6 The dependency plan, revised

**DECIDED (REV §G, R48), with the B7 mapping (v2:331–350):**

| REV | Name (REV line) | REV dependencies | B7 |
|---|---|---|---|
| M7.1 | Foundations audit and architecture reconciliation (:497) | None | B7-01 |
| M7.2 | Governance and decision rulings (:498) | M7.1 | B7-02 |
| M7.3 | Unified simulation contracts (:499) | M7.1–M7.2 | B7-03 |
| M7.4 | Battlefield renderer foundation (:500) | M7.1; aligned with M7.3 | B7-05 |
| M7.5 | Terrain, structures and scenario editor (:501) | M7.3–M7.4 | B7-06 |
| M7.6 | Individual entities and formation model (:502) | M7.3 | B7-07 |
| M7.7 | Movement and spatial interaction (:503) | M7.3, M7.5–M7.6 | B7-09 |
| M7.8 | Detection, visibility and concealment (:504) | M7.5–M7.7 | B7-10 |
| M7.9 | Land warfare foundations (:505) | M7.3, M7.6–M7.8 | B7-11 |
| M7.10 | Individual combat and Animator AI (:506) | M7.4, M7.6–M7.7, M7.9 | B7-12 |
| M7.11 | Tactical pulse and WEGO resolution (:507) | M7.2–M7.10 | B7-15 |
| M7.12 | Enemy AI and battle inspection (:508) | M7.8–M7.9, M7.11 | B7-16 |
| M7.13 | Siege operations (:509) | M7.7–M7.12; campaign contract | B7-18 |
| M7.14 | Logistics and operational warfare (:510) | M7.2–M7.3, M7.6–M7.7 | B7-17 |
| M7.15 | Naval warfare module (:511) | M7.3, M7.6, M7.8, M7.11, M7.14 | B7-19 |
| M7.16 | Air warfare module (:512) | M7.3, M7.6, M7.8, M7.11, M7.14 | B7-20 |
| M7.17 | Combined-arms interaction (:513) | M7.9, M7.15–M7.16 | B7-21 |
| M7.18 | War Sandbox and test laboratory (:514) | M7.4–M7.12; integrate domain modules incrementally | B7-22 |
| M7.19 | Campaign integration and resolver parity (:515) | M7.11–M7.18 | B7-23 |
| M7.20 | Whole-M7 integration, optimization and exit (:516) | All required packets | B7-29 |
| — | Military buildings and calibration | B7-18 (v2:308) | B7-24 (R54) |
| — | Garrisoning (GA) | B7-09, B7-14, B7-24 (v2:309) | B7-25 (R54) |

**The critical path (R53, DECIDED):** B7-11 (M7.9) → B7-12 (M7.10) → B7-15 (M7.11) → B7-16 (M7.12) → B7-18 (M7.13) → B7-24 → B7-25 → B7-29 (M7.20).

Each edge was checked against v2's dependency table (v2:295–313):

| Edge | Evidence | REV equivalent |
|---|---|---|
| B7-11 → B7-12 | B7-12 depends on "B7-05, B7-07, B7-09, B7-11" (v2:296) | M7.9 → M7.10 (REV:506) |
| B7-12 → B7-15 | B7-15 depends on "B7-02–B7-12, B7-14" (v2:299) | M7.10 → M7.11 (REV:507, "M7.2-M7.10") |
| B7-15 → B7-16 | B7-16 depends on "B7-10, B7-11, B7-15" (v2:300) | M7.11 → M7.12 (REV:508) |
| B7-16 → B7-18 | B7-18 depends on "B7-09–B7-17" (v2:302) | M7.12 → M7.13 (REV:509, "M7.7-M7.12") |
| B7-18 → B7-24 | B7-24 depends on "B7-18" (v2:308) | none in REV; by R54 |
| B7-24 → B7-25 | B7-25 depends on "B7-09, B7-14, B7-24" (v2:309) | none in REV; by R54 |
| B7-25 → B7-29 | B7-29 depends on "all in scope" (v2:313) | → M7.20 (REV:516, "All required packets") |

The competing chains are shorter: B7-16 → B7-22 → B7-23 → B7-29; B7-18 → B7-23 → B7-29; B7-15 → B7-19 → B7-21 → B7-23 → B7-29.

**ADMIN derivation, not a Director decision:** the segment upstream of B7-11 is v2's own, quoted exactly from v2:325: "B7-00 → B7-02 → B7-03 → B7-07 → B7-09 → B7-10 → B7-11". v2's caveat also stands: "B7-06's terrain data contract must land before B7-09" (v2:325). B7-00, "Record the direction" (v2:284), is discharged by this D-075 and is not a packet; REV makes M7.2 (B7-02) depend on M7.1 (B7-01) (REV:498). Which predecessors are formally on the path is O-23.

**v2 proposals that are NOT decided** (REV governs until the Director rules; O-23):
- the parity battery moved from M7.19 to B7-15;
- "The renderer depends on the docket, not only on M7.1" (v2:316), against REV:500 "M7.1; aligned with M7.3";
- the added packets B7-04, B7-08, B7-13 and B7-14 (v2's B7-26–B7-28 are stale: EC-1..3 are delivered, §4.5).

### 4.7 The M7.2 docket, revised

v2's docket is at v2:386–409. Each item is now DECIDED (closed today), OPEN (§5), or ADMIN.

| v2 item | Subject | Status now |
|---|---|---|
| D-01 | Record REV with supersession clauses | **ADMIN, done**: D-075, the §4.1 notes, and ADR-060 |
| D-02 | D-038 C1/A3/C3, D-052, D-039 F1, D-011 §4; host engine | **DECIDED** for the battlefield (S9, S16–S29). Host engine **OPEN** (O-13). Campaign map unchanged. |
| D-03 | Meaning of "kernel" | **OPEN** (O-14) |
| D-04 | "Atomic" and paired Lanchester | "Atomic" **DECIDED** (S8). Paired Lanchester **OPEN** (O-15). |
| D-05 | Tactical pulse count and length; internal step | **OPEN** (O-4, O-5). Separate clocks DECIDED (R33). |
| D-06 | Operational pulses and turn crossing | The fixed interval is DECIDED (R29). Crossing turn boundaries is binding under the ledger's §19 (§3). **OPEN:** the duration (O-2) and the reconciliation mechanism (O-3). |
| D-07 | Parity tolerance | **OPEN** (O-16) |
| D-08 | Naval resolution; transport (R13) | **OPEN** (O-1). The naval module is DECIDED (R7). |
| D-09 | Air-to-air | **DECIDED** (S12) |
| D-10 | Cross-platform scope | **OPEN** (O-17) |
| D-11 | Sandbox setup; casualties; legality; editor scope | Setup **DECIDED** (S5). Casualties, legality, campaign-encounter editing and the sandbox resolver path **OPEN** (O-18, O-19, O-20, O-37). |
| D-12 | Vehicle counted object and caps | **OPEN** (O-11) |
| D-13 | Close D-012 and D-013 | **OPEN** (O-7, O-4) |
| D-14 | Entity persistence; wounded and prisoners | **OPEN** (O-10) |
| D-15 | Action Capacity against Law 3 | **OPEN** (O-8) |
| D-16 | Off-road movement against the graph | **OPEN** (O-9) |
| D-17 | Siege boundary | **DECIDED** (R40, R41). Starvation interplay **OPEN** (O-21). |
| D-18 | GA placement | **DECIDED** (R54). Design **OPEN** (O-36). |
| D-19 | Numbering | B7 ids used by R53, aliases in §4.5 (ADMIN). Formal adoption **OPEN** (O-24). |
| D-20 | D-069 genre-term wording | ADMIN drafting rule; no ruling needed |
| D-21 | S8 §4.1 four items in the M7 spec | ADMIN compliance check for the ratified M7 spec text (`spine-s8-governance-freeze.md:72–73`). Item 1 is M7.1 (R52). |
| D-22 | Category-B support as battle-transient entities | **OPEN** (O-33) |

M7.2's remaining deliverable is the Director's rulings on the open items that REV:498 names: Action Capacity, off-road movement, pulse definitions, internal step, spatial model, entity persistence and naval resolver. The "siege boundary" and the "required D-record/ADR amendments" in that list are now discharged by R40/R41 and by D-075/ADR-060.

### 4.8 The M7.3 contracts, revised

The v2 contract list (v2:411–429) is the working input. Under today's decisions:

**DECIDED invariants every contract must carry:**
- one kernel clock, event order, identity, inspection, persistence and replay for all modules (R4, R8, R51 I.1);
- the renderer never merges or deletes identities, and results are invariant under camera, render quality, frame rate and LOD (R17, R20, I.4);
- Animator AI and Enemy AI on separate interfaces and logs (R23–R25);
- the cross-domain engagement sequence (R10) with capability-state compatibility and no era gates (R11);
- per-pulse events, interruptions and simultaneity (R27, R32, I.6);
- the sandbox scenario round-trips and replays identically (I.8);
- causal traceability (I.11);
- campaign resolvers keep the common contract, parity and Ledger conservation (I.12).

**Standing repository rules carried, not new constraints:** a POPULATED-table test per serialized row type; a composite ordering key with an integer tie-break and a tie-dense test; turn-exact delivery pins (`CLAUDE.md:48–50`).

**OPEN in the contract set:**
- the owning project or namespace (v2 writes `Sim.Battle`; O-14);
- the event-key tuple `(tick, phaseClass, priority, entityId, seq)`, which is a v2 proposal (v2:573, E5; O-6);
- the entity collapse "to `long` per formation" (O-10, O-11);
- sandbox RNG stream keying and the scenario persistence and hash format, beside `RngRegistry.Get(systemId, regionId)` and the `WorldState` header (`docs/m0-kernel-spec.md:72, :86`; O-14, O-18);
- formation-membership ownership between ADR-055's campaign tables and the battle kernel (`m0-kernel-spec.md:54`; O-14).

---

## 5. Open for the Director

None of these is decided by today's decisions, and none is decided here. Each shows the alternatives as recorded (mostly v2 §E, v2:433–454), their consequences, and what the item blocks. Each alternative leaves today's decisions intact. None is a reason to revisit them.

| # | Open question (words that leave it open) | Alternatives → consequences | Blocks |
|---|---|---|---|
| O-1 | **Naval resolution, and transport under D-065 R13.** REV:532 "Director must decide whether naval battles remain auto-only or gain TacticalResolver support"; REV:511 "decided by formal ruling" | (a) Auto-only → keeps d011:45's auto-resolve, and the naval module still shares the kernel contracts (I.1). (b) TacticalResolver support → naval tactical battles; an ADR-031 amendment and save migration if transport vessels become controllable (`d065:56`). Transport: abstract lift, or controllable vessels. | M7.15 (B7-19), M7.17 |
| O-2 | **Operational pulse duration.** REV:463 "subject to a dimensional audit and formal ruling" | (a) Era-varying fixed duration (weeks early, days modern), set after the audit. (b) A single fixed duration → simpler, and dimensionally checked once. Either way it integrates with dt (Law 3). | M7.14 (B7-17), M7.13 |
| O-3 | **The MECHANISM by which War Pulses crossing campaign-turn boundaries is reconciled with the kernel contract and the sub-step rule (CR-017 §6 with CR-006 §1).** Crossing itself is not open: the Director's ledger Part I §19 (RATIFIED) rules "War Pulses can cross strategic turn boundaries" (ledger:943) and "players retain full operational control" (:950); it stays binding (§3). REV §D does not address turn boundaries. The ledger's annex R4.10 records the collision as "Still OPEN … CR-017 §6 together with CR-006 §1" (ledger:1543–1544). The frozen items are kernel §3.2 and §3.4 (`m0-kernel-spec.md:66, :70`), the sub-step rule (`civ-sim-architecture-v3-outline.md:33`) and D-011 (`d011:34`), as CR-017 §6.1 lists them (`cr-017:466–475`) | (a) CR-017 §6.4 Option B, "RULE TOGETHER WITH CR-006 §1" (`cr-017:528–529`), choosing one of CR-006 §1.4's options (`cr-006-continuous-time-and-campaign-epoch.md:66–91`): A sub-stepping → "Every golden re-pins" (`cr-006:68–75`); B date-stamped presentation → small blast radius, but "The player acts at the next boundary" (`:77–83`); C interrupt as a turn boundary → event-dependent dt recorded in the order log, and the affected goldens re-pin (`:85–91`). (b) CR-017 §6.4 Option C, Director override of kernel §3.2/§3.4 "adding a sub-turn coordinate" (`cr-017:531–532`) → frozen kernel §3.2/§3.4 and the sub-step rule amended; "every golden re-pins (CR-006 §1.4)" (`cr-017:540`). (c) **Would supersede ledger §19 (a ratified Director ruling):** no crossing. War Pulses nest inside one Step, which is CR-017 §6.4 Option A, "**Inside one Step,** pulses resolve by AI or delegated resolution" (`cr-017:520–522`; blast radius `:538`). Its player orders are "persistent directives set at the turn boundary" (`:523`). Whether such directives satisfy §19's "players retain full operational control" (ledger:950) is not stated by any source and is for the Director. | M7.14, M7.15, M7.16 |
| O-4 | **Tactical pulse length and count; D-011 §1 "6–12 pulses max" (`d011:10`) and D-013 (`d011:89`).** REV:533 "Amend D-013 only if tactical pulse budget/duration changes" | (a) Keep 6–12 command pulses, each with internal steps → no change to D-011 §1 or D-013. (b) A different count or length → D-013 amended, and the parity battery reshaped. | M7.11 (B7-15) |
| O-5 | **Definition of the internal step.** REV:498 "Resolve … internal step" | (a) Fixed integer ticks. (b) An event-driven queue with integer time. (c) A hybrid. Each needs the composite-key tie-break (`CLAUDE.md:48`). | M7.3, M7.11 |
| O-6 | **Event priority, tie-break, collision, interruption and RNG-consumption rules.** REV:469 "Define …" | Technical work for M7.3. v2's five-field key is one candidate (v2:573). A ruling is needed only if the Director wants to fix the shape. | M7.3, M7.11 |
| O-7 | **Spatial model (D-012).** REV:535 "Audit whether the proposed grid supports movement, LOS and projectiles" | (a) Coarse grid (~48×32, `d011:35`) → cheap pathing; weak LOS and projectiles. (b) Continuous fixed-point → best fit for LOS and projectiles; new spatial index. (c) Hybrid (grid for pathing, continuous for LOS) → two structures to keep consistent. | M7.3, M7.7 (B7-09), M7.8 |
| O-7a | **Numeric representation of battle positions and kinematics.** Not addressed by REV; Law 5 bans `float` (`CLAUDE.md:20`) | (a) `double` under the existing determinism regime. (b) Fixed-point integers. Interacts with O-7 and O-17. | M7.3 |
| O-8 | **Action Capacity against Law 3 (CR-017 §3).** REV:536 "Resolve before movement implementation". **Ratified baseline, binding under every alternative:** ledger Part I §17.2, "Every MobileAgent has Action Capacity." and "Actions are agent-specific." (ledger:861–865); §19, "involved military agents receive refreshed Action Capacity" during War Pulses (:949) | (a) A: per sim-year × dt → a dt-0.5 turn gets 1/20 of a dt-10 turn (`cr-017:296–297`). (b) B: an agency budget per decision interval → one exemption ADR. (c) C: override Law 3 → largest blast radius. | M7.7, M7.14 |
| O-9 | **Off-road movement against the D-009/D-010 graph, including sea (CR-017 §5).** REV:536. **Ratified baseline, binding under every alternative:** ledger Part I §18 (ledger:910–937): "Movement uses continuous x/y positions." (:912), "Road graphs are preferred where viable." (:929), "**Off-road movement is allowed.**" (:931), "Ships may move on open water/free positions." (:935), "Do not restrict all agents to roads." (:937); §26 items 16–17 (:1128–1129) | (a) A: the graph is the fast route and the supply object; off-road movement samples terrain → one ADR. (b) B: off-road as the lowest network tier → Scale Charter edge budget re-measured. (c) C: amend D-009. | M7.7, M7.15 |
| O-10 | **Entity persistence and identity; wounded; prisoners (`d011:27`); the end of ADR-055 immortality.** REV:534 "Rule persistence/identity semantics" | (a) Battle-transient entities that collapse to formation counts → no conserved-table change. (b) Persistent veterans in a conserved table → schema, migration and Ledger work. (c) A person registry → touches D-010. | M7.6 (B7-07), M7.19 |
| O-11 | **Caps and counted objects.** REV:402 "exact counted object to be defined"; REV:404 "proposed limits, not measured … must define precisely what is counted" | Covers: final cap values; the counted object for vehicles and mortars; whether vehicles are conserved stocks (V2) or transient (V1); the mismatch of identities 306–308 (60 personnel) against the 50 cap; ladder sizes against caps (`d011:36`); stored versus member-derived formation attributes. (a) V1 transient → no amendment. (b) V2 conserved → four amendments and a migration. | M7.6 |
| O-12 | **Target hardware and performance budgets.** REV:475 "Set target hardware and measurable performance budgets before promising scale" | (a) Name a reference machine and p95 budgets before M7.4/M7.6 benchmarks. (b) Measure first, then set budgets → the benchmarks report without a pass line. | M7.4, M7.20 (I.9) |
| O-13 | **Host engine and renderer technology** (D-002 `m0-kernel-spec.md:9`; ADR-009; `d038:72`). REV:558 is an audit item only | (a) A 3D path inside the current MonoGame host. (b) A new host or engine beside the simulation → ADR-009's "ONLY new packages" (`adr-009:8`) amended. The choice follows the M7.1 audit. | M7.4 (B7-05), M7.10 |
| O-14 | **Where "the kernel" (R5) lives in code.** REV:408 and :412 do not say | (a) A new battle framework project (v2: `Sim.Battle`) → no touch to frozen `Sim.Core/Kernel`; added to the banned-construct scan. (b) Extend the frozen kernel, for example a pulse coordinate in `OrderRecord` (`Sim.Core/Kernel/OrderLog.cs:5`, "shape … is fixed") → kernel sign-off, and every order-log consumer re-checked. Also covers the pulse scheduler against the fixed `SystemId[]` order (`m0-kernel-spec.md:68`) and formation-membership ownership (:54). | M7.3, M7.9, M7.11 |
| O-15 | **Paired Lanchester inside the TacticalResolver** (`d011:37`). REV is silent on resolver mathematics | (a) Keep paired Lanchester at formation level, driven by entity state. (b) Entity-level resolution aggregated to outcomes (R39 allows distinct equations per module). | M7.9, M7.11 |
| O-16 | **Parity tolerance and granularity** (`d011:29`; I.12). REV:553 "parity" without a value | (a) A median-gap band pre-committed before measurement → testable. (b) Qualitative parity → untestable. | M7.11/M7.19 |
| O-17 | **Cross-platform determinism scope.** REV:516 "Windows/CI" is undefined | (a) Keep the Linux reference (ADR-022): Windows is built and smoke-tested but not hash-equal. (b) Reopen CR-013: hash equality across platforms → rework of simulation math (transcendentals). | M7.3, M7.20 |
| O-18 | **Sandbox casualty conservation.** Not addressed | (a) Non-conserved, sandbox-only → no Ledger touch; must never reach a campaign. (b) Conserved → synthetic populations. | M7.18 (B7-22) |
| O-19 | **Sandbox force legality**, including campaign-blocked identities and sandbox siege difficulty from editor structures. Not addressed (REV:483 "configure sides, forces") | (a) Legality by computed capability (`milestones.md:93–98`). (b) Free forces → a parallel vocabulary needs its own record. | M7.5, M7.18 |
| O-20 | **Whether editing reaches campaign-encounter terrain.** REV:487 says only "without changing combat rules" | (a) Sandbox scenarios only; campaign terrain comes from the world. (b) Editing on loaded encounters → D-009 interaction. | M7.5, M7.19 |
| O-21 | **Siege-owned starvation state against D-039 D5** ("This runs on the FOOD system, not the battle system", `d039:128`). REV:438 "owns … starvation" | (a) The siege owns starvation state; the food system computes it → no conflict. (b) The siege computes starvation itself → D5 superseded in part. | M7.13 |
| O-22 | **Per-packet authorization of M7.2, M7.3 and the rest.** REV:493 "explicit authorization" | Authorize packet by packet (for example M7.1's acceptance, then M7.2/M7.3), or as a lane. | Every M7 packet |
| O-23 | **v2's resequencing, and the formal upstream path.** REV:515 governs; v2 proposals only | Adopt or reject: parity battery in B7-15; renderer after the docket; packets B7-04, B7-08, B7-13, B7-14 (v2's B7-26–B7-28 are stale, §4.5); the path segment before B7-11. | Planning |
| O-24 | **Formal adoption of B7-nn numbering.** The Director used B7 ids; adoption is not stated | (a) B7-nn as sub-packets under M7.n, aliases frozen (§4.5). (b) REV M7.n only, with B7 ids kept for R53 alone. | Planning |
| O-25 | **How battlefield assets are produced** (`d038:75` F4 "procedural, rendered, purchased or commissioned"; D-038 D1–D3 for the battlefield). REV is silent | Procedural, rendered, purchased or commissioned, with provenance per asset (D-069 R3). | M7.4, M7.10 |
| O-26 | **The campaign map against D-038 C1 and A3.** Today addresses only the battlefield, so C1 and A3 remain binding on the map. Raised only so the two visual layers are a conscious choice | (a) Keep the map as the illuminated atlas, with tokens (no change). (b) Bring figures to the map too → a separate ruling. | none in M7 |
| O-27 | **"Camera cinema"** (`d011:41`). REV requires a camera (:473, :500) but says nothing on cinematic camera work | (a) Not in M7. (b) In M7.4/M7.18. | M7.4 |
| O-28 | **The milestone name** "Battle Layer" (`milestones.md:24`; `CLAUDE.md:10`) against R1's wider scope | Keep the name, or rename at a milestone gate. | none |
| O-29 | **Whether the operational war pulse is a world-global clock.** ADR-019 §1.4: "it is not a second global clock" (`adr-019:81`) | (a) Local to wars and their participants → no conflict with ADR-019. (b) Global → ADR-019 §1.4 superseded in part. | M7.14 |
| O-30 | **Intra-turn supply, upkeep, attrition and siege supply against ADR-019 §1.2** (`adr-019:51–53` forbids "intra-turn economic causal chains of any kind"). REV:438, :510 | (a) Economic effects settle at turn granularity; pulses carry only military state → no conflict. (b) Per-pulse economic effects → ADR-019 §1.2 superseded in part. | M7.13, M7.14 |
| O-31 | **Air units in sandbox or test scenarios outside the ratified Age scope reached in play.** REV:513 tests aircraft and helicopters (an I.2 input) and REV:537 says "implement only ratified era/capability scope"; draft REV:75 "Flight I is in M7 only if its Age is reached in play" | (a) Tests may instantiate any designed family; campaign play stays capability-gated. (b) Tests use only Age-reached families → I.2 is proven with ratified-scope units. | M7.16, M7.17 |
| O-32 | **Sniper form** (D-065 R14, Q10: independent formation or attached capability; `d065:57`). The cap of 2 is in O-11 | (a) Its own formation. (b) An attached capability on line formations. | M7.6 |
| O-33 | **Category-B support (D-065 R10, "persisted quantities or allocations") against R13's entities** | (a) Quantities consumed by the battle system, with no member entities. (b) Battle-transient entities derived from the quantities (R19 keeps them non-commandable). | M7.6, M7.9 |
| O-34 | **Packet owners** for ZOC, generals as commanders, category-B allocations, the MobileAgent table design and the catalogue validator (formerly charter M7.1/M7.2/M7.5) | Assign to M7.6, M7.7, M7.12 or M7.19, or accept v2's B7-08. | M7.6, M7.7, M7.19 |
| O-35 | **Which packet ends "Soldiers remain immortal until M7.4"** (`d067:88`; `adr-055:261`) | M7.14 (attrition), M7.19 (Ledger casualties), or v2's B7-04. Bound to O-10. | M7.14, M7.19 |
| O-36 | **The GA design** (charter:1641's seven parts are a proposal; D-065 R11 "approved future feature requirement") | Ratify charter §14's seven-part breakdown as B7-25's scope, or have it designed in its packet. | B7-25 |
| O-37 | **Which resolver path sandbox AI-vs-AI and autoplay battles use; D-011 §2 "AutoResolver … Used for all AI-vs-AI battles" (`d011:22`) and "TacticalResolver … Player-only" (`d011:23`) as they apply to the sandbox.** REV:481 and :381 say the sandbox uses the same kernel and domain modules as campaign battles, and REV:485 lists "autoplay" for Battle Test, but no REV line names the resolver for sandbox AI-vs-AI or autoplay battles. Campaign battles keep both resolvers (REV:515, :553). Replaces the withdrawn S4 | (a) Sandbox AI-vs-AI and autoplay battles run through the TacticalResolver path → `d011:22` ("all AI-vs-AI") and `d011:23` ("Player-only") superseded in part, for the sandbox only. (b) Sandbox AI-vs-AI and autoplay battles use the AutoResolver, as `d011:22` says, and the TacticalResolver stays player-commanded → no supersession. (c) Either resolver selectable per Battle Test setup → `d011:22` and `d011:23` superseded in part, for the sandbox only. Under every alternative, campaign battles keep `d011:22–23` as written. | M7.18 (B7-22) |

---

## 6. Not authorized by this record

- No implementation: no code, data, schema, test, golden or `CLAUDE.md` change.
- No build or test run, and no git operation: no add, commit, push, branch, tag or merge.
- No M7 packet. M7.1 to M7.20, B7-24 and B7-25 stay unauthorized until the Director authorizes each one (O-22).
- No commit of any §4 consequence. The dated notes, ADR-060, the plan and the `docs/m7-spec.md` note are written in the working tree as administrative consequences of this ruling (item 4 of the clarification), LOCAL and uncommitted (§4). Committing them waits for the Director's instruction. The comment rescoping of §4.3 is not done; it waits for an authorized packet.
- No decision on any §5 item.

---

## Corrections applied — 2026-11-03

*Change log of the corrections pass made after the independent verification (errors E1–E12, concerns C1–C5) and the completeness critique (gaps G1, G1b, G2, G3, G4). D-075 is a new, untracked record, so the in-place changes above are corrections of a draft, not edits of a ratified record. Tracked files received appended sections only. No Director decision was added, changed or reinterpreted.*

- **G1 (ledger §19).** §3 now records the Director's ledger Part I §19 "WAR PULSES" (ledger:943, :950, :955–956) and §26 item 18 (:1130) as still binding and compatible with R29/R30 (REV §D), with the annex status R4.10 (ledger:1538–1544: §19 governs; the collision with the kernel contract, the sub-step rule and D-011 is OPEN through CR-017 §6 with CR-006 §1). O-3 is restated as the MECHANISM of reconciliation. "No crossing" appears only as alternative (c), labelled "Would supersede ledger §19 (a ratified Director ruling)". R29 and §4.7 D-06 were aligned. The same restatement is made in ADR-060 (§2.9, §3, §4), the plan (O-3) and the `docs/m7-spec.md` note (W2).
- **E10.** O-3 now cites CR-017 §6.4 correctly: Option A is the inside-one-Step (no-crossing) case (`cr-017:520–522`); Option C's "every golden re-pins" is at `cr-017:540`.
- **G1b.** O-8 cites ledger §17.2 (ledger:861–865) and §19's Action Capacity line (:949) as the ratified baseline. O-9 cites ledger §18 (ledger:910–937). §3 adds §24 "one reported agent -> one visual token" (ledger:1074) as retained for the world map, compatible with R16, and §17.2/§18 as binding baselines. The critique's ":855ff" was checked: the §17.2 heading is at ledger:861.
- **E1/E9.** The statements at the provenance list (former line 9), §4 intro, §4.2 and §6 now describe the tree: `docs/m7-spec.md` holds the source file's lines 1–567 plus an appended note; the §4.1 notes, ADR-060, the plan and the m7-spec note are written, LOCAL and uncommitted.
- **E2, E3 (ADR-060).** ADR-060 §3 cites the handoff register at `:51–54` and no longer cites a "D-065 R11 placement wording"; its §4 cites `d039:128`.
- **E4.** O-7a (numeric representation) is added to §5. ADR-060 §4 is now a copy of §5 (O-1…O-37 plus O-7a, same ids and content); ADR-019 maps to O-29 and O-30 there; ADR-060 §5 no longer says "O-1…O-28 only".
- **E5.** The upstream segment is quoted exactly from v2:325, "B7-00 → B7-02 → B7-03 → B7-07 → B7-09 → B7-10 → B7-11", in §4.6, ADR-060 §2.14 and plan §5, each noting that B7-00 is discharged. The competing-chain list in §4.6 now includes B7-21 → B7-23.
- **E6.** §4.1 now quotes ADR-056:104 as "(the units track, M7.3)".
- **E7.** Alias-only notes are appended to `docs/adr/adr-055-m7-recruitment-personnel-reequip.md` and `docs/d067-m6-exit-gate-director-rulings.md`, headed "## Dated note — 2026-11-03 (D-075; appended, the text above is unchanged)". No content is superseded.
- **E8 (ADR-060).** ADR-060 §3.1 now states the S8:37 schedule price as what changes and which packets are affected, with no duration, and its docs list names every appended file (including cr-017, the recovered-decisions register, ADR-055, D-067 and `docs/m7-spec.md`).
- **E11.** The `docs/m7-spec.md` note heading now reads "## Dated note — 2026-11-03 (D-075; appended, the text above is unchanged)".
- **E12.** §4.5 maps EC-1..EC-3 as done: ratified (`d067:99–101`, R16–R18) and delivered (commit `f7a59b8`). B7-26–B7-28 are marked stale in §4.6 and O-23.
- **C5.** "Blocks" is unified across D-075, ADR-060 and the plan: O-7 blocks M7.3, M7.7 and M7.8; O-17 blocks M7.3 and M7.20.
- **G3/C1.** S4 is withdrawn and reclassified OPEN as **O-37**, with alternatives. It covers both `d011:22` ("Used for all AI-vs-AI battles") and `d011:23` ("Player-only") as they apply to the sandbox. R43, R44, §4.7 D-11, the ADR-060 §3 table, the plan (O-37, C17, TF-7) and §4.1 were aligned. A "Correction — 2026-11-03 (D-075)" section is appended at the end of `docs/d011-battle-layer-addendum.md`.
- **C2.** S31 is withdrawn as vacuous (`milestones.md:24` states no scope). The name stays O-28. A correction section is appended at the end of `docs/milestones.md`.
- **G4/C3.** R54 is framed the same way in D-075 §1.13 and ADR-060 §2.14: DECIDED as a consequence of the Director's instruction 9 critical path, which places B7-24 and B7-25 on M7's path; GA design remains O-36. ADR-060's "the Director may correct it" hedge is removed.
- **C4.** R52 keeps its label as a reading: the Director's clarification counts as the ratification of the architecture, quoting the clarification's "FINAL and authoritative". Authorizing M7.2 and M7.3 stays O-22.
- **Line references.** The plan's `D-075:n` and `ADR-060:n` citations were re-pointed to the corrected line numbers.
- **Not changed.** `docs/current-state.md`: its routing block still says "O-1 to O-36". The tracked block sits at the top of the file and is not appended to here; O-37 and O-7a are listed in §5.

## Dated note — 2026-11-04 (D-076; appended, the text above is unchanged)

Record: `docs/d076-m7-decision-reconciliation.md` (Director decisions of 2026-11-04).
- **S83:** the following §5/§4 items are closed or delegated by the Director on 2026-11-04: O-1 (b), O-2 (a), O-3 (a, CR-006 Option A), O-4 (b, count), O-5 (c), O-6 (delegated), O-7 (c), O-7a (b), O-10 (a), O-15 (b), O-16 (delegated model), O-17 (b), O-24 (a); O-22 partially; O-8 shape (b) as a labelled reading. Residues and remaining items: D-076 §4. Garrisoning is decided M7 scope (R54) and is not a Director question. The text above is kept as the 2026-11-03 record.

## Dated note — 2026-11-04 (D-077; appended, the text above is unchanged)

Record: `docs/d077-visual-workstream-procedural-graphics-avatar-cinematics.md` (Director visual decisions of 2026-11-04).
- **S94:** O-13 → (a) MonoGame 3D path; O-27 → (b) in M7; O-25 closed (procedural, purchased, commissioned, custom-rendered, with provenance); O-12 closed as to the machine, budgets open (D-077 Q-V4).

## Dated note — 2026-11-05 (D-079; appended, the text above is unchanged)

Record: `docs/d079-m7-governance-reconciliation-2026-11-05.md` (Director decisions of 2026-11-05, cited DIR5). Each bullet quotes the superseded line, states the extent and what is retained, and cites the replacing DIR5 section and D-079 decision row (DR). Categories: DIRECTOR DECISION, ADMINISTRATIVE CONSEQUENCE, DELEGATED TECHNICAL DECISION, OPEN (needs the Director), READING (labelled interpretation).

- **S242** (`d075-m7-unified-warfare-direction.md:52`) — "(open detail: where this kernel lives in code, O-14)" — IN PART: the open detail is answered as to direction: a separate Sim.Battle project; retained: R5's kernel responsibilities; project creation unauthorized until the architecture review. **Replaced by** DIR5 §1.3 (D-079 DR4–DR6). [DIRECTOR DECISION]
- **S243** (`d075-m7-unified-warfare-direction.md:88`) — "It must not choose targets, invent movement, decide casualties, alter morale or change combat outcomes." — IN PART (clarified): cosmetic between-step animation (swings, blocks, dodges, reactions) is permitted if it never changes results, authoritative positions or health, RNG, timing or hashes; retained: every prohibition as to authoritative state. **Replaced by** DIR5 §5.4 (D-079 DR42). [DIRECTOR DECISION]
- **S244** (`d075-m7-unified-warfare-direction.md:133`) — "edit terrain, elevation, vegetation, roads, water, structures, fortifications, objectives, deployment zones and timed reinforcements." — IN PART (READING): deployment-zone editing is bounded by the thirds rule (own third; middle third undeployable before battle); campaign-encounter terrain editing barred; retained: all other editor content. **Replaced by** DIR5 §2.13, §2.14 (D-079 DR30–DR32). [READING]
- **S245** (`d075-m7-unified-warfare-direction.md:151`) — "The Director's clarification counts as the ratification of the architecture." — IN PART (scope of the READING): ratification covers the logical architecture (ADR-060); physical project structure awaits the Director's architecture review; retained: R52's audit scope. **Replaced by** DIR5 §1.3 (D-079 DR4–DR6). [DIRECTOR DECISION]
- **S246** (`d075-m7-unified-warfare-direction.md:280`) — "which packet ends immortality is O-35." — IN FULL: soldier attrition and mortality belong to M7.14; retained: "M7.4" stays a historical alias. **Replaced by** DIR5 §2.9 (D-079 DR23). [DIRECTOR DECISION]
- **S247** (`d075-m7-unified-warfare-direction.md:281`) — "how operational pulses relate to it is O-3/O-14." — IN PART: the relation is decided (sub-stepping, War Pulses cross turns: D-076 N2, DIR5 §2.4) and is not an O-14 (project) question; retained: no reads of fresh writes inside one sub-step. **Replaced by** DIR5 §2.4, §1.3 (D-079 DR4–DR6, DR11–DR14). [DIRECTOR DECISION]
- **S248** (`d075-m7-unified-warfare-direction.md:294`) — "**ADR-022** and **CR-013** (Linux reference platform), pending O-17." — in part: O-17 is decided (CR-013 reopened; Windows/Linux hash equality required) and O-13 is decided (MonoGame 3D; D-077 V6). Retained: ADR-009/D-002 as the host stack (amendment A-22 pending). **Replaced by** DIR5 §3.2, §6.1 (D-079 DR35, DR44). [DIRECTOR DECISION]
- **S249** (`d075-m7-unified-warfare-direction.md:350`) — "Which project hosts it is O-14." — IN PART: host direction = separate Sim.Battle project; creation and scan extension wait for the architecture review; retained: banned-constructs scan applies to it. **Replaced by** DIR5 §1.3 (D-079 DR4–DR6). [DIRECTOR DECISION]
- **S250** (`d075-m7-unified-warfare-direction.md:376`) — "the packet that ends soldier immortality (O-35); v2 proposes B7-04" — IN FULL: ends in M7.14; v2's B7-04 maps as a sub-packet under M7.14 (ADMIN); retained: the alias rows. **Replaced by** DIR5 §2.9, §1.2 (D-079 DR2, DR3, DR23). [DIRECTOR DECISION]
- **S251** (`d075-m7-unified-warfare-direction.md:427`) — "**v2 proposals that are NOT decided** (REV governs until the Director rules; O-23):" — IN PART: ruled: v2's resequencing is not adopted wholesale; B7 work packages map under the existing M7.n with REV §G edges; retained: REV governs. **Replaced by** DIR5 §1.2 (D-079 DR2, DR3). [DIRECTOR DECISION]
- **S252** (`d075-m7-unified-warfare-direction.md:440`) — "| D-03 | Meaning of "kernel" | **OPEN** (O-14) |" — IN PART: direction decided (Sim.Battle); creation awaits architecture review; render/presentation boundaries need a technical recommendation. **Replaced by** DIR5 §1.3 (D-079 DR4–DR6). [DIRECTOR DECISION]
- **S253** (`d075-m7-unified-warfare-direction.md:447`) — "| D-10 | Cross-platform scope | **OPEN** (O-17) |" — in full as an open item: Windows/Linux hash equality required; Windows primary; Linux a verification platform (D-076 N22 first; DIR5 controls). **Replaced by** DIR5 §3.2 (D-079 DR35). [DIRECTOR DECISION]
- **S254** (`d075-m7-unified-warfare-direction.md:448`) — "Casualties, legality, campaign-encounter editing and the sandbox resolver path **OPEN** (O-18, O-19, O-20, O-37)." — IN PART: O-19 free forces, O-20 no encounter terrain editing, O-37 AutoResolver for AI-vs-AI decided; O-18 isolation decided; retained: sandbox setup (S5). **Replaced by** DIR5 §2.13, §2.15 (D-079 DR30, DR33). [DIRECTOR DECISION]
- **S255** (`d075-m7-unified-warfare-direction.md:455`) — "Design **OPEN** (O-36)." — IN PART: GA functional scope fixed (installations, capacity, eligibility, defence bonuses, defence vs attack and siege, reinforcement/withdrawal/replacement, occupation, capture, integration); retained: mechanics designed in B7-25. **Replaced by** DIR5 §2.3 (D-079 DR10). [DIRECTOR DECISION]
- **S256** (`d075-m7-unified-warfare-direction.md:459`) — "| D-22 | Category-B support as battle-transient entities | **OPEN** (O-33) |" — IN FULL: support is panel-managed capability, not battlefield entities (O-33 -> a). **Replaced by** DIR5 §2.12 (D-079 DR29). [DIRECTOR DECISION]
- **S257** (`d075-m7-unified-warfare-direction.md:480`) — "the owning project or namespace (v2 writes `Sim.Battle`; O-14);" — IN PART: Sim.Battle is the chosen direction, not a v2 proposal; retained: creation unauthorized until architecture review. **Replaced by** DIR5 §1.3 (D-079 DR4–DR6). [DIRECTOR DECISION]
- **S258** (`d075-m7-unified-warfare-direction.md:484`) — "formation-membership ownership between ADR-055's campaign tables and the battle kernel" — IN PART: the battle kernel is outside Sim.Core (Sim.Battle direction); the ownership split becomes M7.3 contract C4 design, not O-14; retained: the question itself. **Replaced by** DIR5 §1.3 (D-079 DR4–DR6). [ADMINISTRATIVE CONSEQUENCE]
- **S259** (`d075-m7-unified-warfare-direction.md:494`) — "Transport: abstract lift, or controllable vessels." — IN FULL: controllable transport vessels are included; ADR-031 amendment and save migration follow (ADMIN). **Replaced by** DIR5 §2.1 (D-079 DR8). [DIRECTOR DECISION]
- **S260** (`d075-m7-unified-warfare-direction.md:502`) — "| O-8 | **Action Capacity against Law 3 (CR-017 §3).**" — IN PART: maximum two actions per decision window, integrated with agent-specific Action Capacity; legal combinations delegated; retained: ratified AC baseline (ledger §17.2, §19). **Replaced by** DIR5 §2.8 (D-079 DR22). [DIRECTOR DECISION]
- **S261** (`d075-m7-unified-warfare-direction.md:505`) — "| O-11 | **Caps and counted objects.**" — IN PART: the eight values are preserved as caps; CAP-1..5 to be resolved without new Director decisions; vehicles battle-transient (V1) by §2/§2.9; mortar counted object delegated. **Replaced by** DIR5 §2.10, §2.9 (D-079 DR23–DR25). [DIRECTOR DECISION]
- **S262** (`d075-m7-unified-warfare-direction.md:506`) — "| O-12 | **Target hardware and performance budgets.**" — IN FULL (procedure): alternative (b): measure on the Victus first; earlier targets are proposals; numeric budgets stay OPEN for Director review after measurement. **Replaced by** DIR5 §6.5, §7 (D-079 DR48, DR50). [DIRECTOR DECISION]
- **S263** (`d075-m7-unified-warfare-direction.md:508`) — "| O-14 | **Where "the kernel" (R5) lives in code.**" — IN PART: (a) separate project chosen as direction (Sim.Battle); (b) not chosen; creation unauthorized until architecture review; scheduler and membership become M7.3 contract items. **Replaced by** DIR5 §1.3 (D-079 DR4–DR6). [DIRECTOR DECISION]
- **S264** (`d075-m7-unified-warfare-direction.md:511`) — "| O-17 | **Cross-platform determinism scope.**" — in full: (b), strengthened to "Require Windows/Linux hash equality" (same as ADR-060:336). **Replaced by** DIR5 §3.2 (D-079 DR35). [DIRECTOR DECISION]
- **S265** (`d075-m7-unified-warfare-direction.md:512`) — "| O-18 | **Sandbox casualty conservation.**" — IN PART: sandbox isolated from campaign population and casualties; retained: internal sandbox accounting form (technical). **Replaced by** DIR5 §2.13 (D-079 DR30). [DIRECTOR DECISION]
- **S266** (`d075-m7-unified-warfare-direction.md:513`) — "| O-19 | **Sandbox force legality**" — IN FULL: (b): unrestricted forces, any designed family regardless of campaign era; campaign stays gated; the sandbox vocabulary gets its own record (ADMIN). **Replaced by** DIR5 §2.13, §2.15 (D-079 DR30, DR33). [DIRECTOR DECISION]
- **S267** (`d075-m7-unified-warfare-direction.md:514`) — "| O-20 | **Whether editing reaches campaign-encounter terrain.**" — IN FULL: (a): encounters inherit campaign terrain; no encounter terrain editing. **Replaced by** DIR5 §2.13 (D-079 DR30). [DIRECTOR DECISION]
- **S268** (`d075-m7-unified-warfare-direction.md:515`) — "| O-21 | **Siege-owned starvation state against D-039 D5**" — IN PART (READING): "do not duplicate existing resource models" excludes a siege-private food model (b); siege health is a siege-owned quantity; retained: D5 and the food system. **Replaced by** DIR5 §2.2, §2.11 (D-079 DR9, DR26–DR28). [READING]
- **S269** (`d075-m7-unified-warfare-direction.md:517`) — "| O-23 | **v2's resequencing, and the formal upstream path.**" — IN FULL (resequencing): not adopted wholesale; B7 mapped under M7.n; retained: upstream segment as an ADMIN derivation outside E2. **Replaced by** DIR5 §1.2 (D-079 DR2, DR3). [DIRECTOR DECISION]
- **S270** (`d075-m7-unified-warfare-direction.md:523`) — "| O-29 | **Whether the operational war pulse is a world-global clock.**" — IN FULL: (a): local to wars and participants, fog-respecting; ADR-019 §1.4 not superseded. **Replaced by** DIR5 §2.4 (D-079 DR11–DR14). [DIRECTOR DECISION]
- **S271** (`d075-m7-unified-warfare-direction.md:524`) — "| O-30 | **Intra-turn supply, upkeep, attrition and siege supply against ADR-019 §1.2**" — IN FULL: (b): economic effects per operational pulse; ADR-019 §1.2 superseded in part (ADMIN note owed). **Replaced by** DIR5 §2.4 (D-079 DR11–DR14). [DIRECTOR DECISION]
- **S272** (`d075-m7-unified-warfare-direction.md:525`) — "| O-31 | **Air units in sandbox or test scenarios outside the ratified Age scope reached in play.**" — IN FULL: (a): sandbox/tests may instantiate any designed family; campaign availability stays gated. **Replaced by** DIR5 §2.15, §2.13 (D-079 DR30, DR33). [DIRECTOR DECISION]
- **S273** (`d075-m7-unified-warfare-direction.md:526`) — "| O-32 | **Sniper form**" — IN FULL: (a): independent small specialist formations. **Replaced by** DIR5 §2.15 (D-079 DR33). [DIRECTOR DECISION]
- **S274** (`d075-m7-unified-warfare-direction.md:527`) — "| O-33 | **Category-B support (D-065 R10" — IN FULL: (a): panel-managed capabilities, not physical battlefield entities. **Replaced by** DIR5 §2.12 (D-079 DR29). [DIRECTOR DECISION]
- **S275** (`d075-m7-unified-warfare-direction.md:528`) — "| O-34 | **Packet owners**" — IN PART: owners are assigned by the B7 -> M7.n mapping (ADMIN); retained OPEN: generals as commanders (Q4). **Replaced by** DIR5 §1.2 (D-079 DR2, DR3). [ADMINISTRATIVE CONSEQUENCE]
- **S276** (`d075-m7-unified-warfare-direction.md:529`) — "| O-35 | **Which packet ends "Soldiers remain immortal until M7.4"**" — IN FULL: M7.14. **Replaced by** DIR5 §2.9 (D-079 DR23). [DIRECTOR DECISION]
- **S277** (`d075-m7-unified-warfare-direction.md:530`) — "| O-36 | **The GA design**" — IN PART: scope fixed by DIR5; retained: detailed mechanics designed in B7-25. **Replaced by** DIR5 §2.3 (D-079 DR10). [DIRECTOR DECISION]
- **S278** (`d075-m7-unified-warfare-direction.md:531`) — "| O-37 | **Which resolver path sandbox AI-vs-AI and autoplay battles use;" — IN PART: AI-vs-AI sandbox battles use the AutoResolver without rendering (b); retained: TacticalResolver autoplay for the parity battery (d011:29, DIR5 §3.1; READING). **Replaced by** DIR5 §2.13 (D-079 DR30). [DIRECTOR DECISION]
- **S279** (`d075-m7-unified-warfare-direction.md:540`) — "Committing them waits for the Director's instruction." — IN PART: commits and pushes to the development branch are authorized after reporting the commit scope; retained: no merge to main, tags or releases. **Replaced by** DIR5 §1.3 (D-079 DR4–DR6). [DIRECTOR DECISION]
