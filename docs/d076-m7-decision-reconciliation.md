# D-076: M7 decision reconciliation — the Director's overriding decisions of 2026-11-04, recorded

**Status: RULED (Director, 2026-11-04).**
- This is the administrative record of the Director's decisions of 2026-11-04 on M7. It reconciles them with D-075 (2026-11-03) and ADR-060.
- It adds a record. Earlier records are not rewritten. Each one whose clause is superseded gets an appended dated note (§6). Frozen text stays unchanged (gov-4 §6).
- **Authorization it carries:**
  - documentation, contracts and dependency planning for M7.2 and M7.3;
  - the foundational coding, building and prototyping already authorized on 2026-11-04 (§1.2).
- **It does not authorize:**
  - the M7 programme as a whole;
  - any packet execution beyond the above;
  - any commit, push, merge, tag or release.

## Provenance

**Two Director instructions, both of 2026-11-04.**

1. **The brief "M7: Initial Engineering Sprint — Director-authorized implementation scope | 4 November 2026"** (cited **BRIEF:n**).
   - Verbatim copy: `/tmp/claude-0/-home-user-civDemo/54b82655-610f-500e-a185-8fae64bdd108/scratchpad/m7s1/director-brief-2026-11-04.md`, lines 1–80.
   - It holds the authoritative decision list (BRIEF:19–66), the governing instructions (BRIEF:8–17), the boundaries (BRIEF:76) and the verification rule (BRIEF:78).
2. **"LATEST OVERRIDING DIRECTOR DECISIONS — Unified Warfare Simulation | Decision Reconciliation and Execution Instructions"** (cited **OVR**). Verbatim excerpts:
   > "These decisions supersede all earlier conflicting rulings, assumptions, recommendations and documentation, regardless of when those earlier records were created. The decisions below are authoritative. Do not reopen them or push back by citing superseded documentation."
   > "[Retain the complete decision list, technical specifications, governance instructions, implementation authorization boundaries and required deliverables from the previous consolidated prompt without changing their substance.]"
   > "**Governance rule:** Preserve historical records, explicitly record supersession and cite the overriding decision. Do not silently rewrite historical records or invent new constraints."
   > "**Milestone structure:** M7.1–M7.20 remains the controlling hierarchy. B7 identifiers are sub-packets under M7.n, with historical identifiers retained as aliases."
   > "**E2 critical path:** `B7-11 → B7-12 → B7-15 → B7-16 → B7-18 → B7-24 → B7-25 → B7-29`"
   > "**Authorization:** M7.2 and M7.3 documentation, contracts and dependency planning are authorized. Previously authorized foundational coding/building may continue. This does not authorize the entire M7 programme, commits, pushes, merges, tags or releases."

**Reading (labelled; not a Director quote).**
- OVR's bracketed sentence refers to "the previous consolidated prompt". This record reads that as the 2026-11-04 brief, the only consolidated decision list the Director issued before OVR. Its decision list is therefore carried here unchanged in substance (§1).
- If the Director meant a different text, the register in §1 must be re-checked against it. Nothing else in this record depends on that reading.

**Earlier clarification, carried.** On 2026-11-04 the Director stated that garrisoning cities, forts, airports, harbours and similar installations is decided M7 scope and is not to be presented as an unresolved question. The brief (BRIEF:76) also says: "Do not decide unresolved garrison mechanics."

**Evidence base.**
- Repository citations are `path:line` at HEAD `7d5defc` (= `main` = tag `m6-exit`), plus the LOCAL, uncommitted D-075, ADR-060, `docs/m7-spec.md` and `docs/m7-plan/m7-2-docket-m7-3-contracts.md`.
- Each citation was re-opened when this record was written.
- The reconnaissance used is in the scratchpad directory `…/scratchpad/m7s1/recon/`. It is secondary evidence and was verified against the tree before use.

**Categories, used throughout:**
- **DECIDED-1103**: decided 2026-11-03 (D-075 R1–R54). Carried unchanged.
- **DECIDED-1104**: decided 2026-11-04 (this record, N1–N24).
- **DELEGATED**: the Director gave the engineering agent authority to choose. The mechanism is recorded as the agent's design, and the delegation itself is the ruling.
- **ADMIN**: documentation required to record a decision. It is a consequence, not a request for approval.
- **OPEN**: genuinely not decided. Listed in §4.
- **READING**: an interpretation this record had to make in order to write anything. It is labelled so the Director can correct it. It is never presented as a Director quote.

---

## 1. Decision register

### 1.1 Carried from 2026-11-03 (DECIDED-1103)

D-075 §1 R1–R54 stand as recorded, with the supersessions of D-075 §2 (S1–S61; S4 and S31 withdrawn). They are not restated here. OVR confirms three of them in its own words:
- R48: M7.1–M7.20 is the controlling hierarchy.
- R53: the critical path.
- R54: B7-24 and B7-25 are on the path.

### 1.2 Decided 2026-11-04 (DECIDED-1104)

Each row quotes the Director. "Closes" names the D-075 §5 item and the alternative it selects, from D-075 §5 / docket §2(b) wording.

**Time and scheduling**

| # | Decision (verbatim) | Source | Closes |
|---|---|---|---|
| N1 | "O-2: Era-varying operational War Pulse duration." | BRIEF:21 | **O-2 → (a)**, "An era-varying fixed duration, set after the audit". **Not decided:** the durations themselves, and the key the duration varies on (§4, Q-T1, Q-T2). |
| N2 | "O-3: CR-006 Option A, sub-stepping, allowing War Pulses to cross campaign-turn boundaries." | BRIEF:22 | **O-3 → (a)**: CR-017 §6.4 Option B, ruled together with CR-006 §1, choosing **CR-006 §1.4 Option A (sub-stepping)** (`docs/adr/cr-006-continuous-time-and-campaign-epoch.md:66–75`). This rules CR-006 §1 and CR-017 §6. CR-006 §2 (the 10,000 BCE epoch) is untouched. |
| N3 | "O-5: Hybrid event-driven and integer-time simulation." | BRIEF:23 | **O-5 → (c)**, hybrid. |
| N4 | "Significant events can interrupt a campaign turn and reopen player control at their actual non-integer time." Binding example: "if a wonder completes one-third of the way through a long campaign turn, the player must have an opportunity to react at that point rather than discovering its completion only at the next turn boundary." | BRIEF:24, :26 | No D-075 O-item by name. It is CR-006 §1.2's requirement (`cr-006:33–36`), delivered by N2's "can hand back at any sub-step boundary" (`cr-006:68–69`). **Not decided:** which events are "significant" (Q-T3). |
| N5 | "O-6: You have authority to select and implement the best deterministic event-priority, tie-breaking, collision, interruption and RNG-consumption mechanism." | BRIEF:25 | **O-6 → DELEGATED.** The mechanism chosen is recorded as contract C2 in the plan revision (`docs/m7-plan/m7-2-m7-3-revision-2026-11-04.md` §3). |

**Spatial simulation**

| # | Decision (verbatim) | Source | Closes |
|---|---|---|---|
| N6 | "O-7: Hybrid spatial model. Grid-based pathfinding and continuous coordinates for LOS and projectiles." | BRIEF:29 | **O-7 → (c)**; closes **D-012** (`d011:88`; a LIVING open decision, so closing it is not an amendment, S8:26). |
| N7 | "O-7a: Fixed-point integer representation." "Do not use floating-point calculations in authoritative battle simulation." | BRIEF:30–31 | **O-7a → (b)**. |

**Tactical battles**

| # | Decision (verbatim) | Source | Closes |
|---|---|---|---|
| N8 | "O-1: Naval battles must support full tactical control. Auto-resolve is optional, never the default." | BRIEF:34 | **O-1 → (b)**, TacticalResolver support. **Not decided:** the transport half of O-1 (D-065 R13, abstract lift versus controllable vessels, `d065:56`), and "never the default" for naval battles with no player participant (Q-B4, joined to O-37). |
| N9 | "O-4: No arbitrary 6-12 pulse ceiling. Battles continue while active participating forces remain, subject to termination rules." | BRIEF:35 | **O-4 → (b)** as to count: D-013 is amended. **Not decided:** the tactical pulse *length* (Q-B1). |
| N10 | "Battle termination includes elimination, retreat, routing and surrender." | BRIEF:36 | (with N9) |
| N11 | "Inactive hidden units have a five-pulse inactivity rule to prevent indefinite battles. Design a robust distinction between genuine inactivity and active concealment. Do not simply delete every stationary hidden unit." | BRIEF:37 | New rule. Its design is **DELEGATED** in the brief's words ("Design a robust distinction"). **Not decided:** the campaign consequence of an inactivity disengagement (Q-B2). |
| N12 | "O-10: Individual entities are battle-transient and collapse back into formation counts after battle." | BRIEF:38 | **O-10 → (a)**: no conserved-table change. |
| N13 | "O-15: Tactical combat resolves at individual-entity level, with outcomes aggregated as needed." | BRIEF:39 | **O-15 → (b)**. |
| N14 | "O-16: You have authority to design, implement and test a quantitative parity model for TacticalResolver and AutoResolver." | BRIEF:40 | **O-16 → DELEGATED** as to the model, which must be quantitative. **Not decided:** the tolerance *value*. BRIEF:73 says "Propose a measurable parity tolerance", so the value is a proposal for ratification (Q-B3). |

**Action economy**

| # | Decision (verbatim) | Source | Closes |
|---|---|---|---|
| N15 | "Each unit should have a strict total action budget of approximately two or three actions." "Prevent repeated movement, attack, retreat, healing and re-engagement chains within one decision interval." "Prototype the action-budget mechanism without deciding whether the final budget is two or three." "Preserve the existing ratified Action Capacity framework and identify how the new restriction integrates with it." | BRIEF:43–46 | **READING:** this fixes the **shape** of O-8 as alternative (b): a budget of whole actions per decision interval, which is CR-017 §3 Option B (`cr-017:299–304`). Alternative (a) (accrual per sim-year × dt) cannot give a strict total of 2–3, and nothing in the brief overrides Law 3 (alternative c). The brief does not name O-8 or CR-017 §3. **Not decided:** 2 versus 3; what a "decision interval" is; and how slots map to the existing AC route cost and `actionCapacityShare` (Q-A1–Q-A3). The ledger's §17.2/§18/§19 (RATIFIED) are preserved. |

**Logistics**

| # | Decision (verbatim) | Source | Closes |
|---|---|---|---|
| N16 | "Movement automatically consumes food and relevant resources." | BRIEF:49 | — |
| N17 | "Terrain affects movement cost: roads are cheaper, jungle more expensive, mountains more expensive still." | BRIEF:50 | Consistent with ledger §18 (`docs/design/director-architecture-and-decision-ledger.md:916–931`). Does **not** close O-9: the graph-versus-off-road instrument stays open. |
| N18 | "Population growth is reduced first when resources become constrained. Military movement is then restricted before allowing military consumption to cause starvation. This priority is automatic, not player-controlled." | BRIEF:51–53 | — . **Not decided:** whether this consumption settles per pulse (observable) or per turn. That is O-30, now narrowed by N2/N4. |
| N19 | "Audit the actual economy … Do not create an isolated resource model or invent resource quantities." | BRIEF:54 | Method rule. |

**Scale and unit economics**

| # | Decision (verbatim) | Source | Closes |
|---|---|---|---|
| N20 | "Ordinary gameplay should make 500-1,000 troops achievable. Maintaining 50,000 troops should be difficult. Recruitment and recurring per-turn maintenance costs must constrain force size." | BRIEF:57–59 | New design target. "Per-turn" maintenance is read under Law 3 as a per-sim-year rate × dt (**READING**: ADR-055 upkeep already has that form). |
| N21 | "Recover the previously specified numerical entity caps from the repository. Do not invent replacements. Do not silently change existing caps." | BRIEF:60–61 | Does **not** close O-11. The caps stay "initial proposed" (R46). Recovered in §5. |

**Determinism**

| # | Decision (verbatim) | Source | Closes |
|---|---|---|---|
| N22 | "O-17: Target cross-platform hash equality between Windows and Linux. Windows remains the primary gameplay platform. Investigate the existing divergence and identify the causes." | BRIEF:64–66 | **O-17 → (b)**: CR-013 is reopened and ADR-022 amended (§2). **Not decided:** the golden reference platform during the transition, and the scope order (battle first or campaign as well) (Q-D1). |

**Structure and authorization**

| # | Decision (verbatim) | Source | Closes |
|---|---|---|---|
| N23 | "M7.1–M7.20 remains the controlling hierarchy. B7 identifiers are sub-packets under M7.n, with historical identifiers retained as aliases." | OVR | **O-24 → (a)**. |
| N24 | "M7.2 and M7.3 documentation, contracts and dependency planning are authorized. Previously authorized foundational coding/building may continue. This does not authorize the entire M7 programme…" | OVR; BRIEF:5 | **O-22 → partially**: M7.2/M7.3 documentation and the foundational prototyping are authorized. Every M7.n execution packet still needs its own authorization. |

**Boundaries carried verbatim (BRIEF:76).**
- "Do not assume MonoGame or Godot."
- "Do not assume a new Sim.Battle project is authorized."
- "Do not extend the frozen kernel or modify OrderRecord without identifying the dependency and obtaining the required authorization."
- "Do not implement full 3D rendering yet."
- "Do not implement the complete naval, air or siege modules yet."
- "Do not decide unresolved garrison mechanics."
- O-12, O-13, O-14 and O-18 to O-37 stay open "except decisions already explicitly clarified".

### 1.3 Status of every D-075 §5 item after 2026-11-04

| Item | Status now | By |
|---|---|---|
| O-1 | **CLOSED (b).** The transport sub-question stays open (Q-B5). AI-vs-AI naval default goes to O-37 | N8 |
| O-2 | **CLOSED (a).** The values and their key stay open (Q-T1, Q-T2) | N1 |
| O-3 | **CLOSED (a)**, with CR-006 Option A | N2 |
| O-4 | **CLOSED (b)** as to count. The length stays open (Q-B1) | N9 |
| O-5 | **CLOSED (c)** | N3 |
| O-6 | **CLOSED: DELEGATED** | N5 |
| O-7 | **CLOSED (c)**; D-012 closed | N6 |
| O-7a | **CLOSED (b)** | N7 |
| O-8 | **Shape fixed as (b), READING.** The instrument (the scoped exemption ADR) is ADMIN. Q-A1–Q-A3 stay open | N15 |
| O-9 | OPEN | — |
| O-10 | **CLOSED (a)** | N12 |
| O-11 | OPEN; caps recovered (§5) | N21 |
| O-12, O-13, O-14 | OPEN (explicitly, BRIEF:76) | — |
| O-15 | **CLOSED (b)** | N13 |
| O-16 | **CLOSED: DELEGATED** (model). The tolerance value awaits ratification (Q-B3) | N14 |
| O-17 | **CLOSED (b).** Q-D1 stays open | N22 |
| O-18 – O-21, O-23, O-25 – O-35, O-37 | OPEN (BRIEF:76). O-29 and O-30 are narrowed (§4) | — |
| O-22 | **PARTIALLY CLOSED** | N24 |
| O-24 | **CLOSED (a)** | N23 |
| O-36 | **Not a Director question.** Garrisoning is decided M7 scope (R54 and the Director's clarification). Its mechanics are designed inside B7-25 and are not decided by implementation (BRIEF:76) | R54; clarification |
| P-1 | OPEN (docket §2(b)) | — |

---

## 2. Supersession matrix (2026-11-04)

How to read this matrix:
- Numbering continues from D-075 (S1–S61).
- "In part" means superseded only to the stated extent. Everything else stays binding.
- Superseded text is not edited. Each file gets an appended dated note (§6).
- **Kernel boundary.** Rows marked **K** supersede a frozen kernel or Spine clause *as a decision*. The code change itself still needs a director-approved kernel ADR (`docs/m0-kernel-spec.md:118`; BRIEF:76). That ADR is ADMIN (§3, A-13) and its acceptance is an authorization step, not a re-decision.

### 2.1 Time, turns and interrupts (N2, N3, N4)

| # | Earlier record: line — quote | Extent (retained part) | Replaced by |
|---|---|---|---|
| S62 **K** | `docs/m0-kernel-spec.md:66` §3.2 — "Systems read `Prev`, write `Next`. One-turn lag is therefore the default" | **In part.** A campaign turn may be executed as several sub-steps, each a full Prev→Next step over a sub-interval of the turn's dt, with a hand-back point at each sub-step boundary. **Retained:** double buffering and one-step lag *within* each sub-step. | N2, N4 |
| S63 **K** | `m0-kernel-spec.md:70` §3.4 — "`SimClock { long Turn; double WorldDateYears; double DtYears; }` — dt from the era table" | **In part:** the clock gains a sub-turn coordinate. **Retained:** dt from the era table; every rate per sim-year; integration `stock += rate * dtYears`; "A system that hardcodes per-turn amounts fails review." (Code is already `SimClock(long Turn, long SimDays, long DtDays)`, `Sim.Core/Kernel/SimClock.cs:10`, under ADR-002.) | N2, N3 |
| S64 **K** | `docs/civ-sim-architecture-v3-outline.md:33` — "sub-stepped systems … read only frozen turn-start state plus their own sub-state. No mid-turn reads of other systems' fresh writes." | **In part**: "turn-start" reads as "sub-step-start". **Retained:** no reads of fresh writes inside one sub-step. | N2 |
| S65 | `docs/d011-battle-layer-addendum.md:34` — "Strategic turn pauses during command; battles read frozen turn-start state per the sub-step rule." | **In part**: as S64. **Retained:** the turn pauses during command; *Command*/*Delegate*. | N2 |
| S66 | `docs/adr/adr-019-architecture-constitution-addendum.md:36–37` §1.1 — "do not introduce an independent intra-turn economic timestep" | **In part**: sub-stepping (N2) splits the strategic dt, so a sub-step is the strategic timestep for its sub-interval, not an independent one (**READING**). **Retained:** no system-private economic clock. | N2 |
| S67 | `adr-019:46–53` §1.2 — FORBIDDEN "sub-simulation … where another system, or the player, can see or react to a mid-turn state … intra-turn economic causal chains of any kind" | **In part**: at a sub-step boundary created by a significant event (N4) or by a war pulse (N2), the state is observable and the player may act. **Retained:** sub-calculation inside a step stays private (§1.2's permitted form). Per-pulse supply and upkeep effects stay O-30. | N2, N4 |
| S68 | `adr-019:79–81` §1.4 — "WAR IS THE ONLY SANCTIONED INTRA-TURN LAYER" | **In part**: significant-event interrupts are also sanctioned. **Retained:** "it is not a second global clock" (O-29 stays open). | N4 |
| S69 | `docs/adr/cr-006-continuous-time-and-campaign-epoch.md:93–95` §1.5 — "Option C, with Option B as the fallback" (recommendation) | **Not adopted.** CR-006 §1 is ruled Option A. **Retained:** CR-006 §1.3's replay requirement (the interrupt point is data). §2 (epoch) stays OPEN. | N2 |
| S70 | `docs/adr/cr-017-ages-surge-and-mobile-agents-vs-frozen-items.md` §6.6 — "Rule this jointly with CR-006 §1. Choose Option A if the Director did not intend per-pulse player control." | **Ruled**: §6.4 Option B, with CR-006 Option A. CR-017 §3 (O-8) and §5 (O-9) are not ruled here, except N15's shape reading. | N2 |
| S71 | `docs/d039-command-fog-and-siege.md:149–152` E3 — "THE CAMPAIGN LAYER IS … NOT A FINER-GRAINED TURN. Ruled explicitly: the player does not play out months of movement." | **In part**: the turn is finer-grained where a war pulse or a significant event splits it, and the player acts at those points (ledger §19 already gives "full operational control", `ledger:950`). **Retained:** the campaign presents decision points and resolves the intervals between them. | N2, N4 |

### 2.2 Battle (N6–N14)

| # | Earlier record: line — quote | Extent (retained part) | Replaced by |
|---|---|---|---|
| S72 | `d011:10` — "Battles run 6–12 pulses max" | **Superseded.** **Retained:** command pulses; simultaneous resolution; "most end earlier by morale collapse and rout". | N9, N10, N11 |
| S73 | `d011:89` — "D-013 pulse budget and pulse-duration fiction — rec: 6–12 pulses ≈ one day of battle." | **Closed and amended:** no count ceiling; termination rules govern. Pulse length is Q-B1. | N9 |
| S74 | `d011:37` — "melee as paired Lanchester engagements" | **In part**, TacticalResolver only: resolution is entity-level, aggregated. **Retained:** the modifiers inside the equation (Law 2). The AutoResolver's "Lanchester-family math" (`d011:22`) is unaffected. | N13 |
| S75 | `d011:45` — "Naval command battles: post-slice candidate; naval stays auto-resolve until then." | **Superseded**: naval battles support full tactical control; auto-resolve is optional and never the default. (D-075 S13 had left this part OPEN.) | N8 |
| S76 | `d011:35` — "a coarse deterministic grid (~48×32) for pathing and zones; continuous rendering on top. *(Logged as D-012 …)*" | **Closed (D-012)**: hybrid. Grid for pathing; continuous fixed-point coordinates for LOS and projectiles. "~48×32" stays a recommendation. | N6, N7 |
| S77 | `docs/m7-plan/aviation-progression.md:282–283` and `:370–373` — "naval battle stays auto-resolve (D-011 §5)" | **In part**: the naval-resolution clauses (D-075 S51/S53 had left them OPEN). **Retained:** carrier combat as a battle mode and carrier sorties (not addressed). | N8 |
| S78 | `docs/m7-plan/unit-catalogue-and-progression.md:1639` — "(auto-resolve)" in charter stage M7.6 | **Superseded** (D-075 S46 had left it O-1). | N8 |
| S79 | `docs/m7-spec.md:54` (draft C5) — "Naval battles always auto-resolve" | Never a decision (D-075 §2.5). Now answered the other way. | N8 |

### 2.3 Numerics and determinism (N7, N22)

| # | Earlier record: line — quote | Extent (retained part) | Replaced by |
|---|---|---|---|
| S80 **K** | `m0-kernel-spec.md:11` D-004 — "rates, prices, and ratios are `double`"; restated as Law 7 (`CLAUDE.md:22`) | **In part**, **authoritative battle simulation only**: positions, kinematics, rates, probabilities and ratios inside it are fixed-point integers. **Retained** for every campaign system, and the conserved-stock rule everywhere. A deterministic double→fixed conversion is defined at the campaign↔battle boundary (contract C16). `CLAUDE.md` is not edited (as D-075 S35). | N7 |
| S81 | `docs/adr/cr-013-cross-platform-determinism.md:3–4` — "CLOSED / ACCEPTED … Linux x64 reference; Windows under …" | **Reopened**: the target is Windows/Linux hash equality. The investigation findings are in the O-17 report. | N22 |
| S82 | `docs/adr/adr-022-reference-platform-linux-x64.md:37` — "The determinism promise is scoped to the reference platform: Linux x64" | **In part**: the promise is widened to Windows/Linux equality as the target, and Windows is the primary gameplay platform. **Retained until equality is measured on both platforms:** Linux x64 goldens as the test reference (Q-D1). "Do not claim cross-platform hash equality unless it has actually been tested on both platforms" (BRIEF:78). | N22 |

### 2.4 Governance and planning records

| # | Earlier record | Extent | Replaced by |
|---|---|---|---|
| S83 | D-075 §5 and ADR-060 §4: the O-items listed CLOSED or DELEGATED in §1.3 | Status only. The texts (LOCAL drafts) stay as written, with a pointer note appended | N1–N24 |
| S84 | `docs/m7-plan/m7-2-docket-m7-3-contracts.md` §2(b), §3 "OPEN inside it", §4.3 TF-10, §5 gates | Revised by `docs/m7-plan/m7-2-m7-3-revision-2026-11-04.md`. The original is retained as the 2026-11-03 baseline | N1–N24 |
| S85 | docket §1.1 — "Formal adoption of the B7 scheme beyond that use is OPEN (O-24)" | Answered: B7-nn are sub-packets under M7.n; aliases are retained | N23 |

### 2.5 Checked and NOT superseded (not re-litigated)

- **D-011 §2 dual-resolver contract and parity rule** (`d011:18–30`): retained. N14 supplies the quantitative model under `d011:29`'s median rule.
- **Ledger §17.2, §18, §19 and §26 item 11** (`ledger:861–872, :910–937, :941–956, :1123`): retained. N15's exemption must be scoped to Action Capacity, never a "reusable generic per-turn exception mechanism" (§26.11).
- **ADR-055 upkeep** (1.0 grain per person-year × dt, `adr-055:86–89`): retained. N16/N18 add consumption and priority. Any change to the ratified rule is an ADR-055 amendment (A-17).
- **"Soldiers remain immortal until M7.4"** (`d067:88`): retained; O-35 stays open.
- **Law 3**: retained. Battle-internal budgets per fixed tactical pulse are dt-invariant, because the pulse is a fixed span of sim-time (D-075 S36).
- **D-039 D5** "This runs on the FOOD system" (`d039:128`): retained; O-21.
- **D-065 R13** transport: retained, open (Q-B5).

---

## 3. Administrative consequences (ADMIN)

| # | Record | State |
|---|---|---|
| A-13 | **Kernel ADR (ADR-061, proposed): sub-turn time, sub-step executor, sub-turn order stamp.** Needed for S62–S64 to reach code. Content is drafted as contracts C1, C2, C5 and C11 in the plan revision. Touches `TurnExecutor.Step/Run`, `SimClock`, `OrderRecord` ("shape … is fixed", `Sim.Core/Kernel/OrderLog.cs:5`), `OrderLog` IO version, order validation, SessionTrace/ReplayGuard/ReplayProof, CanonicalSchema (v39 → v40), and goldens (re-pin with attribution) | **Not written as an ADR.** Content drafted. Director sign-off required (`m0-kernel-spec.md:118`) |
| A-14 | **Scoped numeric exception, battle simulation** (S80), ADR-030 pattern | Drafted as contract C6/C16 text; ADR not filed |
| A-15 | **Scoped Law 3 exemption for Action Capacity budgets** (N15; CR-017 §3 Option B, `cr-017:314`), scoped per ledger §26.11 | Not filed; waits on Q-A1–Q-A3 |
| A-16 | **CR-013 reopening and ADR-022 amendment** (S81, S82) | Dated notes appended (§6). Amendment text waits on Q-D1 |
| A-17 | **ADR-055 amendment** for movement consumption and the logistics priority (N16, N18), when integrated | Not filed; waits on O-30/O-35 and the economy calibration |
| A-18 | **Dated notes** on every file in §2 (list in §6) | Appended 2026-11-04 |
| A-19 | **Plan revision**: M7.2 docket, M7.3 contracts, dependency plan | Written: `docs/m7-plan/m7-2-m7-3-revision-2026-11-04.md` |
| A-20 | **`docs/current-state.md` routing**: point to D-076 | Pointer appended |
| A-21 | **Stale lines, recorded not edited**: D-075:567 ("O-1 to O-36"); `current-state.md:1039` (ADR-019 "absent from `origin/main`" — it is an ancestor of `m6-exit`); ADR-055 §8.1's "every A4+ identity … blocked" (`adr-055:165`, stale since EC-1..3 landed) | Recorded here only |

---

## 4. Remaining genuinely open questions

Only items that the 2026-11-03 and 2026-11-04 decisions leave undecided are listed here. None reopens a decision. Option order is not a recommendation. A recommendation is given only where the engineering evidence supports one, and it is labelled.

### 4.1 New residues of today's decisions

| # | Question | Alternatives → consequence | Blocks |
|---|---|---|---|
| Q-T1 | **Operational War Pulse durations per band** (N1) | Values set by the dimensional audit. Era-pacing turns are 3600/1800/1080/720/360/180 days (`Sim.Data/content/era-pacing.json`). A pulse length that divides each band's dt avoids a remainder pulse at turn ends, but crossing is allowed (N2), so divisibility is a convenience, not a rule. | M7.14 |
| Q-T2 | **What the duration varies on** (N1 against Law 4 / R11: no era-label gates) | (a) The same computed quantity that sets dt (the dt-authority rule, `outline:34`). (b) Participants' capability state. **Recommendation:** (a), because it keeps one authority for time. | M7.14 |
| Q-T3 | **Which events are "significant"** (N4) | (a) A closed list in data (wonder completion, research or civic completion, contact, declaration of war, …). (b) Any event above a computed importance. (c) Player-configurable from a fixed list. Also undecided: whether AI polities get the same interrupt points. Their orders are per turn today, and if they get none, AI and player agency differ. | M7.3 (C1), A-13 |
| Q-T4 | **Interrupt cost model** (engineering choice under N5; flagged because it moves goldens) | (a) Always split the turn at a significant event → every world that triggers one re-pins. (b) Compute forward and re-run from the split only if an order is actually issued at the interrupt → order-less runs stay bit-identical. Delegated to the agent; recorded for transparency. | A-13 |
| Q-B1 | **Tactical pulse length** (N9 removed only the count) | A fixed length in battle ticks. The prototype uses a parameter. | M7.11 |
| Q-B2 | **Campaign meaning of an inactivity disengagement** (N11) | The prototype withdraws the side intact as "escaped": units preserved and not deleted, per BRIEF:37. Alternatives for the campaign outcome: withdrawal, a draw, or retreat with a position change. This affects `BattleOutcome` and exact Ledger accounting (I.12). | M7.11, M7.19 |
| Q-B3 | **Parity tolerance value** (N14) | Proposed in the parity report, pre-committed before the holdout measurement. Needs ratification. | M7.19 (I.12) |
| Q-B4 | **"Never the default" for naval battles with no player participant** (N8 × O-37 × `d011:22`) | (a) AI-vs-AI naval uses the AutoResolver, as `d011:22` says. (b) The tactical path. Joined to O-37. | M7.15, M7.18 |
| Q-B5 | **Naval transport** (the O-1 remainder; D-065 R13, `d065:56`) | Abstract lift, or controllable vessels (ADR-031 amendment plus save migration). | M7.15 |
| Q-A1 | **Budget 2 or 3** (N15) | The prototype measures both. | M7.7, M7.11 |
| Q-A2 | **"Decision interval"** (N15) | (a) Tactical pulse in battle, operational pulse in campaign. (b) The interval between interrupts. **Engineering note:** (b) turns event frequency into an action multiplier, an exploit. Fixed pulses avoid it. | M7.7, M7.11, M7.14, A-15 |
| Q-A3 | **Slots against the AC route cost** (ledger §18 "least Action-Capacity-consuming viable route", `ledger:933`) **and `actionCapacityShare`** (`Sim.Core/Systems/Ages/UnitProgressionContent.cs:119`) | (a) Slots per interval, with a terrain-costed movement allowance inside a Move slot. (b) AC as a fractional pool that slots draw on. Plus a rounding rule for share → slots. | M7.7, A-15 |
| Q-L1 | **The mechanism that makes 50,000 troops difficult** (N20) | Measured: a soldier eats exactly what an adult civilian eats (`sim.json:362–364`), so the marginal food cost is ~0. The binding constraints today are lost labour and the one-year grain buffer (`RecruitmentQuery.cs:245–251`). Options: (a) a recurring non-food upkeep term (wear or wages, as charter §6.7 already designs, `unit-catalogue-and-progression.md:597`); (b) a soldier ration above the civilian ration; (c) a labour-opportunity cost through the economy. Each is an ADR-055 amendment. The calibration evidence is in the economy report. | M7.14 |
| Q-L2 | **Population-trace conflict** (current seed-42 trace 83.7M at t1630 against older 506k) | Re-measure on HEAD before any force-size calibration is ratified. The economy prototype measures it. | Q-L1 |
| Q-D1 | **Determinism transition** (N22) | (a) Battle simulation fixed-point now (cross-platform by construction); campaign transcendentals reworked later with a re-pin. (b) Both at once. Also: the golden reference platform until Windows equality is measured. Windows execution needs CI on a pushed branch, and pushing needs authorization. | M7.3, M7.20 |

### 4.2 Carried from D-075 §5, still open (not affected or only narrowed)

- **Narrowed:**
  - **O-29**, world-global pulse: N2's whole-pipeline sub-stepping refines the one clock rather than adding a second. That is a **READING** for the Director to confirm.
  - **O-30**, intra-turn supply: under N2 a sub-step is an economic step. Whether supply settles per pulse or per turn is still open.
- **Unchanged:** O-9, O-11, O-12, O-13, O-14, O-18, O-19, O-20, O-21, O-23, O-25, O-26, O-27, O-28, O-31, O-32, O-33, O-34, O-35, O-37 and P-1, with D-075 §5's alternatives.
- O-36 is not a Director question (§1.3).

---

## 5. Recovered entity caps (verbatim; values unchanged)

**Status.** Initial proposed per-formation caps. Their existence is DECIDED (R46); their values and counted objects are OPEN (O-11). They are **not in code or data**: no upper bound exists. The only bound is `Personnel >= 1` (`Sim.Core/Systems/Ages/UnitFamilyContent.cs:499`). The only primary source is REV, `docs/m7-spec.md:391–404`:

| Formation/entity type | Maximum (verbatim) | Source |
|---|---|---|
| Heavy/aggregated infantry | 50 soldiers | `m7-spec.md:395` |
| Archers and lighter infantry | 60 soldiers | `:396` |
| Tanks | 5 vehicles | `:397` |
| Helicopters | 3 | `:398` |
| Ships | 5 | `:399` |
| Fixed-wing aircraft | 1 | `:400` |
| Snipers | 2 personnel | `:401` |
| Mortars | 5 weapon systems/crews, exact counted object to be defined | `:402` |

`m7-spec.md:404`: "These are proposed limits, not measured performance guarantees. M7.1/M7.6 must define precisely what is counted for each family and validate recruitment, splitting, merging, casualties and save/load against the caps."

**Conflicts with shipped data, recorded and not resolved.** Data is unchanged, per N21.

| # | Conflict | Evidence |
|---|---|---|
| CAP-1 | 306 musketeers, 307 line infantry and 308 mechanized infantry carry 60 personnel against the 50 heavy-infantry cap. Already in O-11 | `Sim.Data/content/unit-families.json:81–83` |
| CAP-2 | 204 pike_and_shot = 60, if "heavy/aggregated infantry" includes the spear family | `unit-families.json:64` |
| CAP-3 | Vehicles, ships and aircraft are capped by count, but the data counts people only: 605 tanks = 60 personnel; 1102 helicopters = 20; 906 destroyers = 60; 1101 aircraft = 20. No vehicle-count field exists (`MilitaryUnitRow`, `Sim.Core/State/WorldState.cs:1090–1092`) | O-11 V1/V2 |
| CAP-4 | Snipers and mortars have caps but no identity | `unit-families.json:13`; no mortar identity anywhere |
| CAP-5 | Cavalry, siege, naval transport, submarines, carriers, air defence, drones, missiles and reconnaissance have no cap. Any cap for them would be invented | — |

**Related shipped values (DATA-IN-CODE, TUNE).**
- Personnel bands are 3/5/10/20/40/60.
- Upkeep is 1.0 grain per person-year × dtYears (`sim.json:364`).
- Recruitment and re-equip require free grain ≥ 1 year of upkeep (`sim.json:365`).
- REV:475 and I.9: "Do not promise a maximum battle size before measurement."

---

## 6. Dated notes appended (A-18)

Each file below receives "## Dated note — 2026-11-04 (D-076; appended, the text above is unchanged)" naming its rows. The text above each note is unchanged.

| File | Rows |
|---|---|
| `docs/m0-kernel-spec.md` | S62, S63, S80 |
| `docs/civ-sim-architecture-v3-outline.md` | S64 |
| `docs/d011-battle-layer-addendum.md` | S65, S72–S76 |
| `docs/adr/adr-019-architecture-constitution-addendum.md` | S66–S68 |
| `docs/adr/cr-006-continuous-time-and-campaign-epoch.md` | S69 |
| `docs/adr/cr-017-ages-surge-and-mobile-agents-vs-frozen-items.md` | S70; N15's shape reading |
| `docs/d039-command-fog-and-siege.md` | S71 |
| `docs/m7-plan/aviation-progression.md` | S77 |
| `docs/m7-plan/unit-catalogue-and-progression.md` | S78 |
| `docs/adr/cr-013-cross-platform-determinism.md` | S81 |
| `docs/adr/adr-022-reference-platform-linux-x64.md` | S82 |
| `docs/d075-m7-unified-warfare-direction.md`, `docs/adr/adr-060-unified-warfare-simulation-architecture.md` | S83 |
| `docs/m7-plan/m7-2-docket-m7-3-contracts.md` | S84, S85 |
| `docs/m7-spec.md` | S79 |
| `docs/current-state.md` | A-20 pointer |

`CLAUDE.md` is not edited.

## 7. Not authorized by this record

- No commit, push, merge, tag or release. Every file is LOCAL and uncommitted.
- No change to `Sim.Core`, `Sim.Data`, `Sim.Cli`, `Sim.Ui` or their tests:
  - no kernel extension;
  - no `OrderRecord` change;
  - no golden re-pin.
- Prototype code lives only under `prototypes/m7-sprint1/`. It is outside every solution and CI scan, does not decide O-14, and is removed by deleting the folder.
- No M7.n execution packet beyond N24.
- No decision on any §4 item.

## Dated note — 2026-11-04 (D-077; appended, the text above is unchanged)

Record: `docs/d077-visual-workstream-procedural-graphics-avatar-cinematics.md` (Director visual decisions of 2026-11-04).
- **S92, S93:** the carried boundary "Do not assume MonoGame or Godot" is superseded as to MonoGame (D-077 V6); "Do not implement full 3D rendering yet" is narrowed: an initial 3D slice and prototypes are authorized (D-077 V11). O-12 is closed as to the reference machine (HP Victus, RTX 4050 Laptop); O-13, O-25 and O-27 are closed (D-077 S94).

## Dated note — 2026-11-05 (D-079; appended, the text above is unchanged)

Record: `docs/d079-m7-governance-reconciliation-2026-11-05.md` (Director decisions of 2026-11-05, cited DIR5). Each bullet quotes the superseded line, states the extent and what is retained, and cites the replacing DIR5 section and D-079 decision row (DR). Categories: DIRECTOR DECISION, ADMINISTRATIVE CONSEQUENCE, DELEGATED TECHNICAL DECISION, OPEN (needs the Director), READING (labelled interpretation).

- **S280** (`d076-m7-decision-reconciliation.md:27`) — "This does not authorize the entire M7 programme, commits, pushes, merges, tags or releases." — IN PART: commits and pushes to the development branch now authorized; retained: not the entire programme; no merge to main, tags or releases. **Replaced by** DIR5 §1.3 (D-079 DR4–DR6). [DIRECTOR DECISION]
- **S281** (`d076-m7-decision-reconciliation.md:84`) — "**Not decided:** the transport half of O-1" — IN FULL: transport -> controllable vessels; no-player naval battles -> AutoResolver (sandbox explicit; campaign by "Campaign resolver rules remain unchanged" + d011:22, READING). **Replaced by** DIR5 §2.1, §2.13 (D-079 DR8, DR30). [DIRECTOR DECISION]
- **S282** (`d076-m7-decision-reconciliation.md:87`) — "**Not decided:** the campaign consequence of an inactivity disengagement (Q-B2)." — IN PART: a hidden unit stationary >= 5 pulses may be treated as routed or lost; retained: robust inactivity design (delegated). **Replaced by** DIR5 §2.6 (D-079 DR19, DR20). [DIRECTOR DECISION]
- **S283** (`d076-m7-decision-reconciliation.md:96`) — ""Each unit should have a strict total action budget of approximately two or three actions."" — IN PART: maximum two actions per decision window; retained: no repeated chains; AC preserved and integrated. **Replaced by** DIR5 §2.8 (D-079 DR22). [DIRECTOR DECISION]
- **S284** (`d076-m7-decision-reconciliation.md:104`) — "**Not decided:** whether this consumption settles per pulse (observable) or per turn." — IN FULL: per operational pulse. **Replaced by** DIR5 §2.4 (D-079 DR11–DR14). [DIRECTOR DECISION]
- **S285** (`d076-m7-decision-reconciliation.md:112`) — "Does **not** close O-11. The caps stay "initial proposed" (R46)." — IN PART: caps preserved as listed; CAP-1..5 resolution ordered; mortar counted object delegated; retained: not a battle-size guarantee (R47). **Replaced by** DIR5 §2.10 (D-079 DR24, DR25). [DIRECTOR DECISION]
- **S286** (`d076-m7-decision-reconciliation.md:118`) — "**Not decided:** the golden reference platform during the transition, and the scope order (battle first or campaign as well) (Q-D1)." — IN PART: equality required and existing (campaign) divergence must be fixed, so campaign is in scope; retained: ordering and transitional golden platform (technical). **Replaced by** DIR5 §3.2 (D-079 DR35). [DIRECTOR DECISION]
- **S287** (`d076-m7-decision-reconciliation.md:129`) — ""Do not assume a new Sim.Battle project is authorized."" — IN PART: Sim.Battle is the chosen direction; retained: creation still unauthorized until the architecture review. **Replaced by** DIR5 §1.3 (D-079 DR4–DR6). [DIRECTOR DECISION]
- **S288** (`d076-m7-decision-reconciliation.md:133`) — ""Do not decide unresolved garrison mechanics."" — IN PART: the garrison functional scope is decided by the Director; retained: detailed mechanics designed in B7-25. **Replaced by** DIR5 §2.3 (D-079 DR10). [DIRECTOR DECISION]
- **S289** (`d076-m7-decision-reconciliation.md:134`) — "O-12, O-13, O-14 and O-18 to O-37 stay open" — IN PART: (also the §1.3 status rows d076:151–156) closed now: O-12 (procedure), O-14 (direction), O-19, O-20, O-23, O-29–O-33, O-35; partly O-18, O-21, O-34, O-36, O-37; open: O-9, O-26, O-28, P-1. **Replaced by** DIR5 §1.2–§2.15, §6 (D-079 DR2–DR33, DR43). [DIRECTOR DECISION]
- **S290** (`d076-m7-decision-reconciliation.md:140`) — "The transport sub-question stays open (Q-B5). AI-vs-AI naval default goes to O-37" — IN PART: Q-B5 answered (controllable transport vessels); the AI-vs-AI naval default is answered for the sandbox (O-37 (b)), its campaign half a READING pending confirmation (D-079 R3). **Replaced by** DIR5 §2.1, §2.13 (D-079 DR8, DR30). [DIRECTOR DECISION; READING]
- **S291** (`d076-m7-decision-reconciliation.md:148`) — "Q-A1–Q-A3 stay open" — IN PART: Q-A1 -> two; Q-A2/Q-A3 delegated; A-15 is ADMIN; retained: shape (b) reading. **Replaced by** DIR5 §2.8 (D-079 DR22). [DIRECTOR DECISION]
- **S292** (`d076-m7-decision-reconciliation.md:155`) — "| O-17 | **CLOSED (b).** Q-D1 stays open | N22 |" — in part: O-17 (b) is strengthened from "target" to "Require Windows/Linux hash equality"; Q-D1 is narrowed: the end state is decided and only the transition order and the interim golden/CI pin platform remain (DELEGATED, READING). Retained: O-17 closed (b). **Replaced by** DIR5 §3.2 (D-079 DR35). [DIRECTOR DECISION]
- **S293** (`d076-m7-decision-reconciliation.md:159`) — "Its mechanics are designed inside B7-25 and are not decided by implementation (BRIEF:76)" — IN PART: functional scope now set by the Director; retained: mechanics designed inside B7-25; not a Director question. **Replaced by** DIR5 §2.3 (D-079 DR10). [DIRECTOR DECISION]
- **S294** (`d076-m7-decision-reconciliation.md:181`) — "Per-pulse supply and upkeep effects stay O-30." — IN FULL: economic effects occur per operational pulse. **Replaced by** DIR5 §2.4 (D-079 DR11–DR14). [DIRECTOR DECISION]
- **S295** (`d076-m7-decision-reconciliation.md:205`) — "**Reopened**: the target is Windows/Linux hash equality." — in part: "the target" becomes a requirement ("Require Windows/Linux hash equality"; "Investigate and fix existing divergence rather than declaring Linux unsupported"). Retained: CR-013 reopened; the O-17 report as the investigation record. **Replaced by** DIR5 §3.2 (D-079 DR35). [DIRECTOR DECISION]
- **S296** (`d076-m7-decision-reconciliation.md:206`) — "the promise is widened to Windows/Linux equality as the target, and Windows is the primary gameplay platform." — in part: equality is required, not a target; Linux is "a determinism verification platform, not a release-target commitment". Retained: Windows primary; no equality claim before measurement on both platforms; Linux x64 goldens as the CI pin instrument during the transition (DELEGATED, READING). **Replaced by** DIR5 §3.2 (D-079 DR35). [DIRECTOR DECISION]
- **S297** (`d076-m7-decision-reconciliation.md:221`) — "retained; O-35 stays open." — IN PART: O-35 -> M7.14; retained: immortality until that packet. **Replaced by** DIR5 §2.9 (D-079 DR23). [DIRECTOR DECISION]
- **S298** (`d076-m7-decision-reconciliation.md:224`) — "**D-065 R13** transport: retained, open (Q-B5)." — IN PART: transport answered (controllable vessels); retained: R13 category E commercial shipping. **Replaced by** DIR5 §2.1 (D-079 DR8). [DIRECTOR DECISION]
- **S299** (`d076-m7-decision-reconciliation.md:234`) — "Not filed; waits on Q-A1–Q-A3" — IN PART: unblocked: count decided, rules delegated; A-15 can be drafted (ADMIN). **Replaced by** DIR5 §2.8 (D-079 DR22). [ADMINISTRATIVE CONSEQUENCE]
- **S300** (`d076-m7-decision-reconciliation.md:235`) — "Amendment text waits on Q-D1" — in full as a dependency: the ADR-022 amendment (A-16) no longer waits on Q-D1; the end state is decided and writing it is ADMINISTRATIVE (no renewed approval). Retained: the dated notes already appended. **Replaced by** DIR5 §3.2, §1.1(5) (D-079 DR1, DR35). [ADMINISTRATIVE CONSEQUENCE]
- **S301** (`d076-m7-decision-reconciliation.md:236`) — "Not filed; waits on O-30/O-35 and the economy calibration" — IN PART: O-30, O-35 decided; still waits on calibration and on the Director's food-allocation decision after the numerical comparison. **Replaced by** DIR5 §2.4, §2.9, §2.11 (D-079 DR11–DR14, DR23, DR26–DR28). [ADMINISTRATIVE CONSEQUENCE]
- **S302** (`d076-m7-decision-reconciliation.md:254`) — "| Q-T3 | **Which events are "significant"** (N4)" — IN PART: agent proposes a list for Director approval (content OPEN); AI interrupt treatment falls under delegated interruption rules (READING). **Replaced by** DIR5 §2.5 (D-079 DR15–DR18). [DIRECTOR DECISION]
- **S303** (`d076-m7-decision-reconciliation.md:255`) — "| Q-T4 | **Interrupt cost model**" — IN FULL: delegated mechanism; golden re-pins expected ("Every golden replay must be re-pinned"). **Replaced by** DIR5 §2.4, §2.5 (D-079 DR11–DR18). [DELEGATED TECHNICAL DECISION]
- **S304** (`d076-m7-decision-reconciliation.md:257`) — "| Q-B2 | **Campaign meaning of an inactivity disengagement** (N11)" — IN PART: routed or lost, per unit; not a draw or intact escape by default; Ledger accounting design delegated. **Replaced by** DIR5 §2.6 (D-079 DR19, DR20). [DIRECTOR DECISION]
- **S305** (`d076-m7-decision-reconciliation.md:259`) — "| Q-B4 | **"Never the default" for naval battles with no player participant**" — IN PART: answered (a) AutoResolver for AI-vs-AI sandbox battles (DIR5 §2.13); the campaign half is a READING pending confirmation (unchanged campaign resolver rules with d011:22; D-079 R3). **Replaced by** DIR5 §2.13, §2.1 (D-079 DR8, DR30). [DIRECTOR DECISION; READING]
- **S306** (`d076-m7-decision-reconciliation.md:260`) — "| Q-B5 | **Naval transport**" — IN FULL: controllable vessels; ADR-031 amendment and save migration (ADMIN). **Replaced by** DIR5 §2.1 (D-079 DR8). [DIRECTOR DECISION]
- **S307** (`d076-m7-decision-reconciliation.md:261`) — "| Q-A1 | **Budget 2 or 3** (N15) | The prototype measures both." — IN FULL: two (maximum) per decision window. **Replaced by** DIR5 §2.8 (D-079 DR22). [DIRECTOR DECISION]
- **S308** (`d076-m7-decision-reconciliation.md:262`) — "| Q-A2 | **"Decision interval"** (N15)" — IN FULL (as a Director question): term "decision window"; legal-action and refresh rules delegated with the exploit-prevention mandate. **Replaced by** DIR5 §2.8, §2.5 (D-079 DR15–DR18, DR22). [DELEGATED TECHNICAL DECISION]
- **S309** (`d076-m7-decision-reconciliation.md:263`) — "| Q-A3 | **Slots against the AC route cost**" — IN FULL (as a Director question): delegated: integrate with agent-specific AC; agent owns rules. **Replaced by** DIR5 §2.8 (D-079 DR22). [DELEGATED TECHNICAL DECISION]
- **S310** (`d076-m7-decision-reconciliation.md:264`) — "| Q-L1 | **The mechanism that makes 50,000 troops difficult** (N20)" — IN PART: recruitment costs + recurring maintenance govern scale; calibrate; no arbitrary costs; retained: mechanism choice (technical) via ADR-055 amendment A-17. **Replaced by** DIR5 §2.10, §2.11 (D-079 DR24–DR28). [DIRECTOR DECISION]
- **S311** (`d076-m7-decision-reconciliation.md:266`) — "Windows execution needs CI on a pushed branch, and pushing needs authorization." — IN PART: pushing to the development branch is authorized; retained: no claim before measurement on both platforms. **Replaced by** DIR5 §1.3, §3.2 (D-079 DR4–DR6, DR35). [DIRECTOR DECISION]
- **S312** (`d076-m7-decision-reconciliation.md:271`) — "That is a **READING** for the Director to confirm." — IN FULL: O-29 -> (a): War Pulses local to wars and participants. **Replaced by** DIR5 §2.4 (D-079 DR11–DR14). [DIRECTOR DECISION]
- **S313** (`d076-m7-decision-reconciliation.md:272`) — "Whether supply settles per pulse or per turn is still open." — IN FULL: per operational pulse (O-30 -> b). **Replaced by** DIR5 §2.4 (D-079 DR11–DR14). [DIRECTOR DECISION]
- **S314** (`d076-m7-decision-reconciliation.md:273`) — "**Unchanged:** O-9, O-11, O-12, O-13, O-14, O-18, O-19, O-20, O-21, O-23, O-25, O-26, O-27, O-28, O-31, O-32, O-33, O-34, O-35, O-37 and P-1" — IN PART: as d076:134; still unchanged: O-9, O-26, O-28, P-1. **Replaced by** DIR5 §1.2–§6 (D-079 DR2–DR43). [DIRECTOR DECISION]
- **S315** (`d076-m7-decision-reconciliation.md:280`) — "their values and counted objects are OPEN (O-11)." — IN PART: values preserved; mortar counted object delegated; vehicles V1. **Replaced by** DIR5 §2.10 (D-079 DR24, DR25). [DIRECTOR DECISION]
- **S316** (`d076-m7-decision-reconciliation.md:295`) — "**Conflicts with shipped data, recorded and not resolved.** Data is unchanged, per N21." — IN PART: resolution now ordered "without inventing new Director decisions"; retained: no silent data change. **Replaced by** DIR5 §2.10 (D-079 DR24, DR25). [DIRECTOR DECISION]
