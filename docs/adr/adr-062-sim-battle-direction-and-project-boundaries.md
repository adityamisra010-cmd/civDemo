# ADR-062 — `Sim.Battle` is the chosen direction for the battle framework; project creation waits for the architecture review; presentation boundaries are a recommendation

**Status: ACCEPTED as to the Director decision of 2026-11-05 (DIR5 §1.3; D-079 DR4–DR6). It creates nothing.** No project,
solution entry, CI job, scan entry, namespace or file outside `docs/` exists because of this ADR, and none is authorized
by it. Section §6 is a **technical recommendation** for the Director's architecture review; it is not a decision. State:
LOCAL and uncommitted on branch `claude/new-session-yavghm`, HEAD `7d5defc` (= tag `m6-exit` = `github/main` as of the last fetch; there is no local `main`).

**ADR number.** 062, listed by D-079 as A-32 ("ADR-062: `Sim.Battle` direction and boundaries") and by the M7.2/M7.3
plan revision of 2026-11-05 as the record of A-24's recommendation.

**Nature of this record.** Administrative (DIR5:21: an ADR update that "is administratively necessary" is done without
renewed Director approval). It records the decision, states exactly what is and is not decided, corrects the O-14
inconsistency (DIR5 §8 Phase A item 7, DIR5:614), and carries the boundary recommendation that the Director's
architecture review needs.

**Citation forms.** DIR5:n is a line of the Director's instruction of 2026-11-05 (working copy
`/tmp/claude-0/-home-user-civDemo/54b82655-610f-500e-a185-8fae64bdd108/scratchpad/m7gov/director-decisions-2026-11-05.md`).
DRn, Sn, An, Dn, Qn and Rn are rows of `docs/d079-m7-governance-reconciliation-2026-11-05.md`. Cn, OD-n and RDG-n are
items of `docs/m7-plan/m7-2-m7-3-revision-2026-11-05.md`. G1:n is a line of `prototypes/m7-gfx1/reports/g1-summary.md`.
Repository citations are `path:line` at `7d5defc`, plus the LOCAL uncommitted M7 records.

**Categories:** **[DIR]** DIRECTOR DECISION · **[ADMIN]** ADMINISTRATIVE CONSEQUENCE · **[DELEG]** DELEGATED TECHNICAL
DECISION · **[OPEN]** needs the Director · **[READING]** a labelled interpretation · **[REC]** a technical
recommendation for Director review (not a decision of any kind).

---

## §1 Context

- **What O-14 is.** D-075 defines it once: "**Where "the kernel" (R5) lives in code.**"
  (`docs/d075-m7-unified-warfare-direction.md:508`; byte-identical in
  `docs/adr/adr-060-unified-warfare-simulation-architecture.md:333`). It has two alternatives:
  - (a) "A new battle framework project (v2: `Sim.Battle`) → no touch to frozen `Sim.Core/Kernel`; added to the
    banned-construct scan."
  - (b) "Extend the frozen kernel, for example a pulse coordinate in `OrderRecord` … → kernel sign-off, and every
    order-log consumer re-checked."
- **What "the kernel" (R5) is.** It is REV:412 (`d075:51`; `adr-060:95–98`): "The kernel owns the common simulation
  clock, pulse scheduler, internal steps, deterministic event ordering, entity identity/lifecycle, formation membership,
  order contracts, movement/spatial queries, detection/knowledge contracts, target acquisition interfaces, effects/damage
  application, replay, save/load, RNG ownership, inspection and event logging." It is the battle framework's kernel, not
  the campaign kernel `Sim.Core/Kernel`.
- **Where the code is today.** All M7 code lives in isolated prototypes, outside `Sim.slnx`, CI and the scans:
  - the simulation prototypes: `prototypes/m7-sprint1/` (`Proto.M7`, `Proto.M7.Econ`, `Proto.DetMath`, their tests and
    bench, and `M7Proto.slnx`, `M7Econ.slnx`, `M7DetMath.slnx`);
  - the graphics prototypes: `prototypes/m7-gfx1/` (`Gfx.Engine`, `Gfx.Proc`, `Gfx.Units`, `Gfx.Avatar`,
    `Gfx.Cinematics`, `Gfx.Demo`, `Gfx.Tests`; `M7Gfx.slnx`);
  - `prototypes/m7-gfx2/` if it is created under D-078 S102.

  The solution `Sim.slnx` holds `Sim.Cli`, `Sim.Core`, `Sim.Data`, `Sim.Tests`, `Sim.Ui.Tests` and `Sim.Ui`, and nothing
  else. `scripts/check-banned-constructs.sh:22–23` scans `Sim.Core Sim.Data Sim.Cli` (`SIM_DIRS`) and those three plus
  `Sim.Tests` (`ALL_DIRS`).
- **Earlier records.**
  - D-077 V7 listed six proposed boundaries ("Sim.Battle, Sim.Rendering, Sim.Assets, Sim.Animation, Sim.Cinematics,
    Sim.Avatar") as "to be investigated and recommended, not created" and wrote "O-14 stays OPEN"
    (`docs/d077-visual-workstream-procedural-graphics-avatar-cinematics.md:21`).
  - D-077 A-24 called project boundaries "a recommendation, not a structural change" (`:49`).
  - G1 §4 made that recommendation (G1:117–130).

---

## §2 Director decision recorded [DIR]

| # | Decision (verbatim) | DIR5 | D-079 |
|---|---|---|---|
| P-D1 | "The separate `Sim.Battle` project is the chosen architecture direction. Record that decision." | :58 | DR6 |
| P-D2 | "However, project creation remains unauthorized until the architecture review is completed." | :58 | DR6 |
| P-D3 | "Rendering and presentation project boundaries still require a technical recommendation." | :58 | DR6; D17 |
| P-D4 | Not authorized: "Creating permanent projects from isolated prototypes before the architecture review." "Expanding the work into the entire M7 implementation programme without authorization." | :53–54 | DR5 |
| P-D5 | "Keep prototypes isolated for now. Do not convert them into permanent projects until the Director reviews the architecture recommendation." | :56 | DR5 |
| P-D6 | "Preserve the isolated prototype architecture until the architecture review." "Keep the renderer independent of authoritative simulation." | :468–469 | DR44 |
| P-D7 | "Campaign battles and sandbox battles must use the same authoritative simulation framework." | :70 | DR7 |
| P-D8 | "Do not create permanent projects before architecture review." "Do not let rendering influence simulation." | :688–689 | DR53 |
| P-D9 | Phase D recommendations include "Permanent project boundaries." | :658 | DR51 |

---

## §3 What is decided — precisely

1. **[DIR] O-14 is closed as to direction, alternative (a).** The battle framework goes in one separate project named
   `Sim.Battle`. It holds REV:412's kernel responsibilities as they apply to battles, and the domain modules (land, naval,
   air, siege, combined arms; DIR5 §2).
2. **[DIR] Alternative (b) is not chosen.** No battle-pulse coordinate, battle clock, battle entity table or battle
   scheduler enters `Sim.Core/Kernel` (D-079 §1.4, DR6).
3. **[DIR, P-D7] One framework.** Campaign battles and War Sandbox battles use the same `Sim.Battle`, not two engines.
4. **[ADMIN] The campaign time model is not O-14.** The campaign kernel's sub-turn instant, sub-step runner,
   decision-window scheduler and order stamp follow from D-076 N2/N4 and DIR5 §2.4–§2.5 whatever O-14 says. They are
   recorded in `docs/adr/adr-061-hybrid-event-time-decision-windows.md` (D-079 A-31) and belong to `Sim.Core/Kernel`.
   Their landing is OPEN (D-079 Q5, OD-1). The battle framework and the campaign kernel share one integer timeline
   (ADR-061 §3.1), and that is the only time coordinate they share.
5. **[ADMIN] M7.3 contract text is no longer blocked by O-14** (D-079 §1.4 item 2). The M7.3 contracts name `Sim.Battle`
   as the intended owning project of every framework-side contract (plan revision C1–C18 "Home"). Only the creation of
   the project waits, and the architecture review gates it.

## §4 What is NOT decided, and NOT authorized

| # | Item | Category | Gate |
|---|---|---|---|
| N1 | Creating `Sim.Battle` (a csproj), adding it to `Sim.slnx`, to CI (`.github/workflows/ci.yml`), to `scripts/check-banned-constructs.sh` (`SIM_DIRS`, `ALL_DIRS`), to the read-isolation review, or giving it a `Directory.Build.props` | Not authorized (P-D2, P-D4, P-D8) | The Director's architecture review (D-079 Q6; OD-2) |
| N2 | Turning any prototype (`Proto.M7*`, `Proto.DetMath`, `Gfx.*`) into a permanent project, or moving prototype code into an existing permanent project | Not authorized (P-D4, P-D5, P-D6) | The architecture review; for `Sim.Core/Kernel`, also D-079 Q5 |
| N3 | Rendering and presentation project boundaries (`Sim.Rendering`, `Sim.Assets`, `Sim.Animation`, `Sim.Cinematics`, `Sim.Avatar`, or any alternative) | **[OPEN]**, decided by the Director after the §6 recommendation (P-D3; D-079 D17) | The architecture review |
| N4 | The dependency direction between `Sim.Core` and `Sim.Battle`, and where the campaign resolver contract's interfaces live | **[REC]** §6 R-2, for the review | The architecture review |
| N5 | The ADR-009 amendment for MonoGame 3D outside `Sim.Ui` and the presentation packages | **[ADMIN]**, owed (D-079 A-39) | Filed separately; it does not create projects |
| N6 | The M7 implementation programme as a whole | Not authorized (P-D4) | Per-packet authorization |

Until the review, the prototypes keep their own namespaces (`Proto.M7.*`, `Gfx.*`), solutions and scan scripts
(`prototypes/m7-sprint1/scripts/check-proto-determinism.sh`, `prototypes/m7-gfx1/scripts/`). Those scans cover only
`Proto.M7/` and `Gfx.Proc/Authoritative` (Phase B audit R12). Nothing in a prototype is evidence that a project
boundary has been chosen.

---

## §5 The O-14 inconsistency, corrected (DIR5 §8 Phase A item 7) [ADMIN]

The correction is D-079 §1.4. Each line below carries a dated note appended in the same pass (D-079 §7); the text of
those records is unchanged.

| # | Inconsistency | Where | Correction |
|---|---|---|---|
| I-1 | **Option letters.** D-075 §5 and ADR-060 §4 give two alternatives. The docket's §2(b) gives three ((b) an area inside `Sim.Core`, (c) extend the kernel), and cites "O-14(c)" | `docs/m7-plan/m7-2-docket-m7-3-contracts.md:151`, `:274` | D-075's letters govern. "O-14(c)" means D-075's (b), which is not chosen |
| I-2 | **What O-14 blocks.** Some lines say M7.3, M7.9 and M7.11; others say M7.3 only | `d075:508`, `adr-060:333`, docket :184 against `adr-060:378`, docket :349, :391, revision-2026-11-04 :133 | After DR6, no contract text is blocked by O-14; only project creation is gated, by the architecture review |
| I-3 | **Conflation with the campaign kernel.** The campaign sub-turn time and the `OrderRecord` stamp are filed under O-14 | `adr-060:267–268`; docket :151, :274; revision-2026-11-04 :110; `d075:188`, `:281`, and the `d075:508` (b) example | They are ADR-061 (§3 item 4), required by DIR5 §2.4–§2.5 whatever O-14 says |
| I-4 | **Status.** `Sim.Battle` called open or "a proposal" | `d076:129`, `:134`, `:152`, `:273`; docket :248; `d077:21` | It is the Director's chosen direction (DR6); it is not created |
| I-5 | **"Kernel adequacy".** O-14 described as whether the kernel contract is adequate | `docs/d011-battle-layer-addendum.md:111`; `docs/design/recovered-decisions-architecture-invariants.md:462` | O-14 is the location question. Kernel adequacy for decision windows is answered by DIR5 §2.5 through ADR-061 |
| I-6 | **Presentation filed under O-14.** Presentation projects and project creation treated as part of O-14 | `d077:21` (V7), `:49` (A-24); G1:117–122; the consolidated report of 2026-11-04 (D-079 Annex A, FULL:130, :184) | Rendering and presentation boundaries are a separate DELEGATED recommendation (P-D3; D-079 D17), §6 below |
| I-7 | **M7.3 contract items filed under O-14.** Formation-membership ownership between ADR-055's tables and the framework, the pulse scheduler against the fixed `SystemId[]`, RNG keying | `d075:483`, `:484`, `:508`; docket :273, :280 | Contract design items C4, C1/C2 and C11 (DELEGATED), not O-14 |
| I-8 | **Scan extension** stated as if it followed from the choice | `d075:508` (a); revision-2026-11-04 :97 | Happens only after authorized creation, consistent with `d077:49` |
| I-9 | **Scope of D-075 R52's reading**, that the 2026-11-03 clarification "counts as the ratification of the architecture" | `d075:151` | Scoped to the logical architecture (ADR-060). The physical project structure waits for the architecture review (DIR5:56, :58) |

Consistent and unchanged: the prototype-isolation statements (`d076:344`; revision-2026-11-04 :11; `d077:67`; `d078:36`).

---

## §6 TECHNICAL RECOMMENDATION for the architecture review — NOT A DECISION [REC]

> **Everything in this section is a recommendation for the Director's architecture review (P-D3, P-D9; D-079 D17, Q6).
> None of it is decided, authorized or implemented. Nothing here may be acted on before the Director rules.** It draws
> on G1 §3–§4 (G1:91–130), which is itself a prototype report and secondary evidence. Where this section goes beyond
> G1, it says so.

**R-1 `Sim.Battle` (direction decided; contents recommended).**
- **Contents.**
  - The authoritative battle simulation: the REV:412 responsibilities for battles, and the domain modules.
  - Authoritative battlefield generation. G1's template is `Gfx.Proc/Authoritative`: integer-only, seeded, with
    SHA-256 canonical bytes (G1:119).
  - The prototype code it would re-implement under its own acceptance tests: Slice C spatial, Slice D tactical, Slice E
    AutoResolver and parity.
- **References.** `Sim.Core` and `Sim.Data` only. No reference to MonoGame, ImGui or any presentation project.
- **Numerics.** The authoritative path is integer and fixed-point (DIR5 §2.7, DR21). The float-free scan rules of
  `prototypes/m7-sprint1/scripts/check-proto-determinism.sh` (no `double`, `float`, `decimal`, `Half`; ordered
  containers; `unchecked` only where listed) move with it.
- **On creation:**
  - add it to `scripts/check-banned-constructs.sh` `SIM_DIRS` and `ALL_DIRS`;
  - include it in the read-isolation review;
  - give it a twin-process determinism test;
  - run it on the Windows and Linux equality legs (`docs/adr/cr-013-reopened-2026-11-05.md` §4) (G1:119–121).

**R-2 Campaign ↔ battle boundary (beyond G1; recommended here).**
- `Sim.Core` should not reference `Sim.Battle`. The campaign resolver contract — contract C16, `BattleSetup` →
  `BattleOutcome` with exact conservation — is defined in `Sim.Core` as data and interfaces. `Sim.Battle` implements it,
  and the host (`Sim.Cli`, `Sim.Ui`, the system catalogue's composition) wires them together.
- This keeps the reference graph acyclic, keeps `Sim.Core` free of battle code (no alternative-(b) creep), and leaves
  the campaign kernel's frozen surface where ADR-061 puts it.
- The shared integer-time types (ADR-061 §3.1) live in `Sim.Core/Kernel`, and `Sim.Battle` reuses them rather than
  duplicating them.
- The campaign decision-window scheduler serves non-war events (the Taj Mahal example), so it belongs to the campaign
  kernel, not to `Sim.Battle` (`prototypes/m7-sprint1/reports/B-foundation.md:129` and its D-079 S482 note).

**R-3 One presentation project, not five (G1:123–127).**
- **Shape.** One project, named `Sim.Presentation3D` or `Sim.Rendering`. Engine, procedural presentation,
  units/animation, avatar appearance and cinematics are namespaces inside it.
- **Why.** The five `Gfx.*` prototype libraries share one float/GPU/skeleton type system. Splitting them added project
  plumbing and no determinism protection. The protection is the one-way sim → presentation edge.
- **Guard.** A csproj guard test: no simulation-surface project (`Sim.Core`, `Sim.Data`, `Sim.Battle`) references a
  presentation project, MonoGame or ImGui.
- **Prerequisite.** It needs the ADR-009 amendment (D-079 A-39): today MonoGame and ImGui are "the ONLY new packages in
  the repo, both confined to `Sim.Ui`" (`docs/adr/adr-009-ui-stack.md:7–8`).
- **Alternative considered.** D-077 V7's split into `Sim.Rendering`, `Sim.Animation`, `Sim.Cinematics` and `Sim.Avatar`.
  Not recommended, for the reason above. The review may still prefer it; nothing in the simulation depends on the choice.

**R-4 No `Sim.Assets` code project (G1:128).**
- The asset catalogue and provenance records are JSON data with a validator test: key, kind, source, author, licence,
  proof, date, era and biome tags, LOD set, triangle budget and variation slots (G1:110–113).
- DIR5 makes provenance and licensing mandatory for every externally sourced or commissioned asset (DIR5 §6.4, DR47).
  That obligation is decided; the JSON form is the recommendation. The asset ADR is D-079 A-40.

**R-5 Avatar profile outside `WorldState` (G1:129–130).**
- That the avatar stays outside authoritative state, hashes and gameplay is decided (DIR5 §5.1, DR39).
- Recommended form: a versioned, hashed sibling file next to the session files (the `SessionManifest` precedent). It is
  not in `WorldHash` and not in the order log.
- Era dress is recomputed at render time from realized capability state (Law 4).

**R-6 Simulation/render threading and interpolation (G1:100–106).**
- The authoritative battle simulation steps at its fixed pulse and publishes immutable, double-buffered snapshots
  (positions in mm, states, events). The renderer reads only the latest two snapshots and never writes simulation state.
  GL stays on one thread.
- The renderer-invariance tests (hash the grid and the trace before and after rendering) carry over.
- Cosmetic animation interpolates between snapshots within DIR5 §5.4's limits: no effect on results, positions, health,
  RNG, timing or hashes (DR42).

**R-7 Host executables (open within the recommendation).**
- `Sim.Ui` keeps the ImGui panels (D-002).
- Whether the 3D battlefield is hosted inside `Sim.Ui` or in a separate executable that references the presentation
  project is left to the review. G1 does not decide it.

**What the review needs (the Phase D package):** this section; G1 §3–§4; the measured prototype facts (Phase B audit: 505
prototype tests pass in Debug and Release, determinism scans clean within their stated scope); the Victus benchmark when
it exists (DIR5 §6.5); and the open costs (the MonoGame effect-format pinning, A-39).

---

## §7 Consequences

- **Documents.** Dated notes on the O-14 lines are appended in the D-079 pass (D-079 §7); none is edited by this ADR.
  `CLAUDE.md` is not edited.
- **Code and CI.** No change. The prototypes stay where they are, outside `Sim.slnx`, CI and the repository scans. Whether a
  CI job may build or test an isolated prototype before the review is D-079 §5 Q15 (OD-7); this ADR adds none.
- **Plan.** M7.3 proceeds as contract text with `Sim.Battle` as the intended home. Every packet that would implement in
  `Sim.Battle` or in a presentation project waits for the review (OD-2).
- **The architecture review is the single gate** for N1–N4. Its outcome is recorded as an amendment to this ADR: the
  approved project list, references and scan entries.

## §8 Summary by category

| Category | Items |
|---|---|
| DIRECTOR DECISION | `Sim.Battle` is the direction (O-14 → (a)); no battle coordinate in `Sim.Core/Kernel`; one framework for campaign and sandbox; creation unauthorized until the review; prototypes stay isolated; boundaries need a recommendation |
| ADMINISTRATIVE CONSEQUENCE | This record; the O-14 corrections (§5) and their dated notes; ADR-061 holds the campaign time model; M7.3 text unblocked; the ADR-009 amendment owed (A-39) |
| DELEGATED TECHNICAL DECISION | The content of the boundary recommendation (§6), for review |
| OPEN (needs the Director) | The architecture review: project creation and registration, presentation boundaries, the `Sim.Core` ↔ `Sim.Battle` dependency direction (D-079 Q6; OD-2) |

## Amendment log

- 2026-11-05 — written (D-079 A-32; W3). Not yet reviewed by an independent verifier.
- 2026-11-05 — Phase A fix pass, after an independent verification: §7 points to D-079 §5 Q15 for CI jobs that build or
  test an isolated prototype; the `main` reference names `github/main`.
