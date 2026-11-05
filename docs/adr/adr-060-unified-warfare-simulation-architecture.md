# ADR-060 — Unified warfare simulation architecture (administrative record of D-075)

**Status: ACCEPTED. This ADR records the Director's decisions of 2026-11-03 (D-075). It authorizes no implementation.**
No code, data, schema, test, golden, build, commit, push or merge is authorized by this ADR or by D-075. Each M7 packet
still needs its own explicit authorization (REV:493). That authorization is open (§4, O-22).

**Nature of this record.** This ADR is an **administrative** document. It records decisions the Director has already
made. It does not ask for them to be approved again (Director's clarification of 2026-11-03, items 4 and 7). It also
serves as the "director ADR" that `docs/spine-s8-governance-freeze.md:13` requires for any change to a frozen item, and
§3.1 carries the five Contradiction Report fields that `:41` asks for. Wherever the decision leaves a detail open, the
detail is listed in §4 and is **not** decided here.

**Citation forms used below:**
- **REV:n** is a line of the Director's revision, "DIRECTOR'S REVISION: UNIFIED WARFARE ARCHITECTURE AND M7.1-M7.20",
  §A–J, lines 374–567 of the session file `m7-1/m7-spec-revised.md`. The Director confirmed on 2026-11-03 that this
  section is their decision of that day. Lines 1–373 of that file are an earlier agent draft and decide nothing; in
  particular, its C1 "6–12 command pulses" (REV:50) and C5 "Naval battles always auto-resolve" (REV:54) are not
  decisions. The LOCAL, untracked `docs/m7-spec.md` (not on MAIN at `7d5defc`) holds the whole session file: its first
  567 lines are byte-identical to it (draft lines 1–373 and REV lines 374–567), followed by an appended dated note.
- **v2:n** is a line of the session file `m7-1/m7.2-governance-reconciliation-v2.md`, the verified M7.2 governance
  reconciliation. Its findings are used here, reclassified under the Director's 2026-11-03 authority.
- Repository citations are `path:line` at `7d5defc` (main = m6-exit). Each one was opened before it was written here.

Three categories are kept apart throughout:
- **DECIDED**: decided by the Director on 2026-11-03.
- **ADMINISTRATIVE**: documentation that records a decision. It is drafted as a consequence of the decision.
- **OPEN**: a technical question that has not been decided. It is listed in §4 for the Director.

---

## §1 Context

1. **The decision.** On 2026-11-03 the Director issued the revised M7 design, REV §A–J (REV:374–567). The same day the
   Director stated that "All decisions I made today regarding the revised M7 design are FINAL and authoritative. They
   supersede any earlier conflicting decisions, rulings, ADRs, specifications, frozen clauses, design constraints or
   milestone plans." That statement and REV §A–J together are recorded as **D-075**.
2. **REV's own status line.** REV:376 reads "**Status: PROPOSED FOR REVIEW.** … conflicts with D-011, D-012, D-013 and
   CR-017 require formal rulings before implementation." The Director's confirmation overtakes that status line.
   D-075 together with this ADR is the formal ruling for every conflict that REV §A–J resolves. REV:376 is kept as
   historical text. Its own scope limit still applies: the revision supersedes the earlier draft's proposals "only where
   stated". Its "before implementation" condition also still holds, because nothing is authorized.
3. **What M7 now is (REV §A).**
   - REV:380: "M7 builds a reusable military simulation environment and standalone War Sandbox, not merely a campaign
     battle viewer."
   - REV:381: "Campaign battles and sandbox battles use the same simulation kernel and domain modules."
   - REV:382: "Visual direction is a high-quality Total War-style 3D battlefield. Graphics work starts during M7."
   - REV:383: "Individual soldiers and machines exist as simulation entities. The player commands formations;
     subordinate entities act and position themselves under formation intent."
4. **Architecture (REV §B).** One kernel with domain modules (REV:406–442).
5. **Entities, formations and rendering (REV §C).** REV:446–456.
6. **Clocks (REV §D).** REV:458–469.
7. **Graphics and performance (REV §E).** REV:471–477.
8. **The prior record this decision meets.** It meets these records:
   - D-011's battle-layer addendum: `docs/d011-battle-layer-addendum.md`, the dual-resolver contract that is frozen by
     `docs/spine-s8-governance-freeze.md:16`.
   - D-038's visual target: `docs/d038-visual-target.md` C1, C3, E4.
   - D-039 F1: `docs/d039-command-fog-and-siege.md:179`.
   - The proposed D-012 and D-013: `docs/d011-battle-layer-addendum.md:88–89`.
   - CR-017 §3 and §5.
   - D-065 R3 and R8–R10.
   - The Spine's deleted/deferred list: `docs/civ-sim-architecture-v3-outline.md:150`.
   - The M7 charter documents under `docs/m7-plan/`.

   v2 analysed these conflicts as items that "require amendment or ratification". Under the Director's 2026-11-03
   authority each one is now either **SUPERSEDED by D-075**, where REV resolves it (§3), or **OPEN**, where REV does not
   (§4).
9. **The M7 exit-criteria definition changes.** `docs/spine-s8-governance-freeze.md:17` freezes "each milestone's
   exit-criteria *definitions*". REV §I (REV:540–553) adds twelve M7 acceptance criteria. This is a **DECIDED** change
   to M7's exit definition, recorded here as that clause requires. The milestone ladder order does not change.

---

## §2 Decision as recorded (D-075)

This section records only what REV decides, quoted with its lines. It adds nothing.

### 2.1 One framework, domain modules, not disconnected engines (DECIDED)
- REV:408: "**Do not build disconnected battle engines.** Build one deterministic military simulation framework with
  domain-specific modules and a combined-arms interaction layer."
- The domain modules are listed in the REV:418–424 table:
  - Land warfare: "Infantry, cavalry, archers, artillery, ground vehicles, melee, projectiles, armour and land
    formations".
  - Air warfare: "Aircraft, helicopters, flight state, air-to-air, air-to-ground, interception and air defence
    interactions".
  - Naval warfare: "Surface vessels, maritime movement/detection, naval weapons, damage, sinking, ports and transport".
  - Siege operations: "Investment, blockade, siege works, bombardment, breaches, sorties, relief, starvation, assault
    and surrender".
  - Combined-arms interaction: "Cross-domain detection, target classification, engagement eligibility, weapons,
    interception, support and effects".
- **How the decision reads.** Where the Director speaks of "multiple specialized battle engines", the decision means
  domain modules on one kernel. This reading comes from REV itself: the §B heading at REV:406 is "one simulation kernel,
  multiple domain modules", REV:408 forbids disconnected engines, and REV:426 allows distinct equations inside a module.

### 2.2 Shared-kernel responsibilities (DECIDED; where the kernel lives is OPEN, O-14)
REV:412: "The kernel owns the common simulation clock, pulse scheduler, internal steps, deterministic event ordering,
entity identity/lifecycle, formation membership, order contracts, movement/spatial queries, detection/knowledge
contracts, target acquisition interfaces, effects/damage application, replay, save/load, RNG ownership, inspection and
event logging."

REV:414: "It must not become one giant collection of era-specific combat conditionals. Domain modules provide explicit
capabilities and rules through stable contracts."

### 2.3 Domain module contract principle (DECIDED)
REV:426: "A domain module may contain distinct equations and mechanics. It must reuse the kernel's clock, event
ordering, identity, inspection, persistence and replay contracts."

### 2.4 Combined-arms engagement sequence (DECIDED)
- REV:430: "Combined arms is not another battle engine. It connects domain modules through capability contracts." The
  examples it gives are "aircraft attacking tanks, tanks engaging helicopters, ground air-defence engaging aircraft,
  ships engaging aircraft, aircraft attacking ships, and artillery engaging detected/designated ground targets".
- REV:432: "detect or receive valid target information → identify/classify as required → establish engagement
  eligibility → select compatible weapon/effect → resolve interception/defence → apply damage/effects → update knowledge
  and inspection records."
- REV:434: "Compatibility derives from equipment, weapons, sensors, target class, range, altitude/depth, line of sight
  and relevant capability state. Do not add era gates where actual capability state should govern."
- REV:537 bounds the air scope: "Build generic contracts; implement only ratified era/capability scope."

### 2.5 Siege is a persistent operation (DECIDED)
- REV:438: "A siege advances over operational/campaign time and owns investment, blockade, supply, starvation,
  construction, fortification condition, bombardment, sorties, relief, surrender and capture state. Tactical assaults
  use the land module. Naval blockade or amphibious action uses the naval/combined-arms modules. Siege must not
  duplicate combat resolution."
- REV:467: "A siege does not run as one enormous tactical battle."
- REV:532: "Preserve siege as operation."
- REV:548 (I.7): "Siege progresses on operational/campaign time; assault invokes land combat."

How starvation is computed relative to D-039 D5 is OPEN (O-21).

### 2.6 Historical authenticity (DECIDED)
- REV:390: "Shared interfaces must not flatten materially different historical weapons, formations, movement,
  detection, logistics and combat."
- REV:442: "Ancient/medieval melee, archery, cavalry and siege equipment must not be forced into modern fire-control
  assumptions. Modern warfare must support relevant distinctions such as armour, penetration, guided weapons, sensors,
  aircraft, air defence and combined-arms engagement. Shared interfaces are abstractions, not identical mechanics."
- REV:551 (I.10) requires representative eras to demonstrate this.

### 2.7 Three layers and renderer independence (DECIDED)
- REV:448: "**Simulation entity:** an individual soldier, vehicle, aircraft, ship, weapon team or other simulated object
  with type-appropriate state."
- REV:449: "**Formation:** the player-commandable group, owning formation orders, membership and command state.
  Individual entities execute formation intent through deterministic local behaviours."
- REV:450: "**Render representation:** visual models, instances, grouped representations or impostors chosen according
  to camera distance and performance budget."
- REV:452: "The renderer may reduce visual detail, animation frequency or model count. It must not merge or delete
  simulation identities. Simulation outcomes must be invariant under camera position, render quality, frame rate and
  LOD."
- REV:477: "'Fusing ten units into one representation' is permitted only as a visual batching/LOD technique. It must
  never merge their simulation state or identities."
- REV:538: "Keep formation as player-commandable family; subordinate members are not new player-commandable families
  absent a ruling."

### 2.8 Animator AI is separate from Enemy AI (DECIDED)
- REV:384: "These must have separate interfaces, responsibilities, logs and tests."
- REV:454: Animator AI "must not choose targets, invent movement, decide casualties, alter morale or change combat
  outcomes."
- REV:456: Enemy AI "selects formation-level orders using only permitted knowledge (`PolityKnowledge`/battle
  knowledge). It does not control visual animation."
- REV:389: "No autonomous tactical personality layer."

### 2.9 Clocks (DECIDED as layers; durations and rules OPEN)
- REV:462–465 defines four time layers:
  1. Campaign turn, "existing civilization turn with era-dependent `dtYears`".
  2. Operational war pulse, a "fixed interval".
  3. Tactical battle pulse, "a much shorter fixed interval".
  4. Internal simulation step.
- REV:533: "Separate clocks."
- REV:469: "both sides' orders act against a common evolving state … Define deterministic event priority,
  tie-breaking, collisions, interruptions, target invalidation, simultaneous effects and RNG consumption. Do not impose
  artificial movement-then-fire phases where they contradict the time model."
- REV:547 (I.6): "Events can occur during a pulse, interrupt actions and resolve deterministically under simultaneous
  orders."

The following stay OPEN:
- the operational pulse duration (O-2);
- the mechanism by which War Pulses crossing campaign-turn boundaries is reconciled with the kernel contract and the
  sub-step rule, CR-017 §6 with CR-006 §1 (O-3). Crossing itself is not open: the Director's ledger Part I §19
  (RATIFIED) rules "War Pulses can cross strategic turn boundaries" (`docs/design/director-architecture-and-decision-ledger.md:943`)
  and "players retain full operational control" (`:950`), and it stays binding and compatible with REV §D (§3);
- the tactical pulse length and count (O-4);
- the definition of the internal step (O-5);
- the concrete priority, tie-break and RNG-consumption rules, which are M7.3 technical work (O-6).

### 2.10 War Sandbox (DECIDED)
- REV:481: "The sandbox is a standalone mode using the same kernel and domain modules as campaign battles."
- Its modes (REV:483–487) are Quick Battle, Scenario Editor, Battle Test, Replay and Inspection, and Campaign Encounter
  ("later … without changing combat rules").
- REV:489 sets the first playable slice: "a small land scenario, individual entities, formation orders, pulse
  resolution, replay and inspection."
- REV:549 (I.8): "A saved sandbox scenario reopens and replays identically."

### 2.11 Graphics start early; benchmarks come before scale promises (DECIDED)
- REV:473: "Graphics work starts early, with a separate renderer/simulation boundary."
- REV:475: "Benchmark simulation and rendering separately … Set target hardware and measurable performance budgets
  before promising scale."
- REV:550 (I.9): "Do not promise a maximum battle size before measurement."
- The per-formation caps (REV:391–402) are "Initial proposed". REV:404 says "These are proposed limits, not measured
  performance guarantees", so their values and counted objects are OPEN (O-11).

### 2.12 Campaign resolvers (DECIDED)
- REV:553 (I.12): "Campaign resolvers preserve the ratified common contract, parity and Ledger conservation."
- REV:515 (M7.19): "Implement AutoResolver/TacticalResolver contract and parity battery."
- The dual-resolver contract, the parity rule and Ledger-exact casualties for campaign battles
  (`docs/d011-battle-layer-addendum.md:21–29`) are therefore **retained**.
- The parity tolerance is OPEN (O-16).

### 2.13 Inspectability and scope (DECIDED)
- REV:388: "M7 is initially a developer/test environment. Real-world war experimentation is out of scope until the whole
  game is integrated and tested."
- REV:552 (I.11): "Material decisions can be traced to inputs, knowledge, rules/capabilities, events and state changes."

### 2.14 Plan (DECIDED; authorization OPEN)
- The M7.1–M7.20 decomposition, its dependencies and its parallel lanes are decided as written in REV:495–526.
- REV:526: "Every lane uses M7.3 contracts and the same deterministic kernel."
- §J (REV:557–567): M7.1 is a read-only audit that "must not implement the renderer or battle simulation".
- The **E2 critical path** is decided by the Director's instruction 9:
  **B7-11 → B7-12 → B7-15 → B7-16 → B7-18 → B7-24 → B7-25 → B7-29**.
  - It replaces the path in v2 §D.1 (v2:325). v2's verifier showed that path was not the longest chain (v2:566–570).
  - Each edge follows v2's dependency table. B7-25 (Garrisoning, GA) depends on "B7-09, B7-14, B7-24" (v2:309), and
    B7-29 is "Whole-M7 integration, optimization and exit" (v2:313).
- **ADMINISTRATIVE derivation, not a Director decision.** The upstream segment is v2's own, quoted exactly from v2:325:
  "B7-00 → B7-02 → B7-03 → B7-07 → B7-09 → B7-10 → B7-11". The verifier did not contradict it. B7-00, "Record the
  direction" (v2:284), is discharged by D-075 and is not a packet; REV makes M7.2 (B7-02) depend on M7.1 (B7-01)
  (REV:498). Its formal status is O-23.
- **R54 (D-075 §1.13): DECIDED as a consequence of the Director's instruction 9 critical path, which places B7-24 and
  B7-25 on M7's path.** B7-24 (military buildings and calibration) and B7-25 (Garrisoning, GA) are M7 packets. This
  closes v2 DEC-15 as option (a), "M7 (B7-25)" (v2:452). GA design remains O-36.

---

## §3 Supersessions (administrative; the full scoped list is D-075 §2)

Each supersession reaches only as far as the conflict. Where the earlier record is a file, the supersession is recorded
by a dated note appended to the end of that file. The historical text stays unchanged.

| Earlier record | Superseded words (extent) | Replaced by |
|---|---|---|
| D-011 §3 `d011-battle-layer-addendum.md:36` | "Formations are atomic", only insofar as it bars individually simulated members. The formation stays the command unit. | REV:383, :448–449, :538 |
| D-011 §4 `:41` | Tokens or blocks as the battle presentation; "What v1 will **not** be: 3D soldiers"; the "no-art-team reality" premise. Overlays, bars, banners and combat feedback are retained. "Camera cinema" is OPEN. | REV:382, :450, :454, :473 |
| D-011 §5 `:45` | "v1 ships ancient/classical warfare only"; "never dogfight simulation"; naval as a deferred domain. "naval stays auto-resolve" is OPEN (O-1). Sieges as multi-turn operations is retained. | REV:420–423, :512, :516, :551, bounded by :537 |
| D-011 §2 `:26` (sandbox only) | "`BattleSetup` comes entirely from the strategic sim", **for sandbox battles only**. Campaign battles are retained. D-011 §2 `:22` ("Used for all AI-vs-AI battles") and `:23` ("Player-only") are **not** superseded: which resolver sandbox AI-vs-AI and autoplay battles use is OPEN (O-37; D-075's S4 is withdrawn). | REV:483 |
| D-011 §1 `:4` (sandbox only; reference class) | "*inside* the turn structure" for the standalone sandbox; the "Ultimate General / Unity of Command" reference class. | REV:380, :382, :481 |
| D-011 §7 `:82` | The premise "the battle map is squarely in its lane" as a 2D surface. The engine choice is OPEN (O-13). | REV:382, :473 |
| D-038 C1 `d038-visual-target.md:29`, C3 `:33`, E4 `:64` | The prohibitions "not later, at any milestone" on figures and animation cycles, and the inheritance of battle tokens, **for the M7 battlefield and War Sandbox only**. The campaign map is not addressed and is untouched. | REV:382, :450, :454, :473 |
| D-039 F1 `d039-command-fog-and-siege.md:179` | The protected "formations-as-tokens". The verbs and WEGO are retained. | REV:382, :449, :507 |
| Spine `civ-sim-architecture-v3-outline.md:150` | "premium visual direction, asset plan beyond icons+counters+map", for the M7 battlefield only. Localization and accessibility are not addressed. | REV:382, :473, :500 |
| S8 `spine-s8-governance-freeze.md:17` | M7's exit-criteria definition gains REV §I. The ladder order is unchanged. | REV:540–553 |
| v2 §D.1 critical path (v2:325) | Corrected. | Director instruction 9 (§2.14) |
| v2 DEC-16 (siege boundary), DEC-8a (sandbox setup source) | Resolved. | REV:438, :467, :532; REV:483 |
| Charter `:1641`, `:1803`, handoff register `docs/m6-plan/m7-handoff-register.md:24`, `:51–54` ("the Director confirms the M7.8 placement"; "The Director confirms the placement") | Placement answered: GA is in M7 as B7-25 (R54). The "M7.8" labels are historical. D-065 R11 (`docs/d065-m7-charter-rulings.md:54`) carries no placement wording and is not superseded: GA stays "an approved future feature requirement". | Instruction 9 (§2.14) |
| M7 charter and aviation documents (`docs/m7-plan/`) | Specific clauses (no per-soldier stats; strike-only air; "never dogfight"; AA framing), to the extent listed in D-075 §2. Carrier battle mode and carrier realization are **not** addressed and stay binding. | REV:421, :430, :448, :512–513 |

**Not superseded.** These stay binding or open:
- the dual-resolver contract and parity (REV:515, :553);
- D-011 §1 "6–12 pulses" (REV:533 makes any amendment conditional; O-4);
- D-012's grid (O-7);
- CR-017 §3 and §5 (REV:536; O-8, O-9);
- ADR-019 §1.4 (`docs/adr/adr-019-architecture-constitution-addendum.md:81` denies only a "second **global** clock"; this
  binds unless O-29 resolves otherwise);
- ADR-019 §1.2 `:51–53` (intra-turn supply, upkeep, attrition and siege supply against it are O-30);
- the Director's ledger Part I §19 "WAR PULSES" and §26 item 18 (RATIFIED;
  `docs/design/director-architecture-and-decision-ledger.md:943`, "War Pulses can cross strategic turn boundaries";
  `:950`, "players retain full operational control"; `:955–956`; `:1130`), **binding and compatible** with REV §D
  (D-075 §3). Only the reconciliation mechanism is OPEN (O-3), as the ledger's annex R4.10 records (`:1538–1544`);
- the ledger's §17.2 Action Capacity and §18 movement (`:861–872`, `:910–937`), the ratified baselines of O-8 and O-9;
- the ledger's §24 "one reported agent -> one visual token" (`:1074`) for the world map, compatible with REV:450
  (battlefield entities are not world-map agents);
- the kernel freeze (`m0-kernel-spec.md:118`) and the fixed `OrderRecord` shape (`Sim.Core/Kernel/OrderLog.cs:5`), both
  part of O-14;
- D-002 / ADR-009 (O-13);
- ADR-022 and CR-013 (O-17);
- the D-065 R3 aviation staging (REV:537).

### 3.1 Contradiction Report fields (`spine-s8-governance-freeze.md:41`), recorded as administrative
1. **Frozen items in conflict.**
   - D-011's battle contract (S8:16), to the extents in the table above.
   - The M7 exit-criteria definition (S8:17).
   - The Spine's deferred visual direction (`:150`).
2. **Evidence.** This is a Director decision, not a measured defect: REV §A–J and the Director's statement of
   2026-11-03. The conflict analysis is v2 matrix B, re-verified at `7d5defc`.
3. **Options.** None are offered. The Director has ruled.
4. **Blast radius.**
   - Docs: dated notes appended to D-011 (plus a correction section), D-038, D-039, D-052, D-065, the Spine,
     `milestones.md` (plus a correction section), `docs/adr/cr-017-ages-surge-and-mobile-agents-vs-frozen-items.md`,
     `docs/design/recovered-decisions-architecture-invariants.md`, the M7 charter and aviation documents, the M7 handoff
     register, the style bible, the E1 checklist, and the alias-only notes on ADR-055 and D-067; the untracked
     `docs/m7-spec.md` (dated note); the routing block of `docs/current-state.md`; and the new records D-075, this ADR
     and `docs/m7-plan/m7-2-docket-m7-3-contracts.md`.
   - Tests: the comments in `ObjectLayerTests.cs` and `UnitTokenDetailTests.cs` overstate the C1 scope. Both tests
     exercise campaign-map tokens only (`WorldLens.FormationToken`, `WorldLens.UnitToken`). Rescoping those comments is
     future implementation work and is **not authorized** now.
   - Packets: M7.1–M7.20, plus B7-24 and B7-25 (R54).
   - **Schedule price** (`spine-s8-governance-freeze.md:37`: "a written ADR stating what breaks, which tests and docs
     change, and the schedule price"). Stated as what changes, not as a duration; no packet duration is measured.
     - M7's scope grows from "Battle Layer" (`milestones.md:24`) to twenty REV packets (REV:497–516) plus B7-24 and
       B7-25, against the unratified draft's sixteen (T7.1–T7.16, REV:208–223).
     - Work the Spine placed at M9+ moves into M7: operations, siege and naval (`civ-sim-architecture-v3-outline.md:90`;
       M7.13, M7.14, M7.15).
     - New scope enters M7: the air module (M7.16), combined arms (M7.17), the War Sandbox (M7.18), and a 3D battlefield
       renderer, terrain and editor (M7.4, M7.5, M7.10), which the Spine had deferred (`:150`).
     - M7's exit definition gains I.1–I.12 (REV:540–553), so M7 cannot exit before each is met.
     - M7.2 cannot close until the Director rules on the open items it names (REV:498), and M7.3, M7.6, M7.7 and M7.15
       wait on the blocking open items listed in §5.
     - GA (B7-25) and military buildings (B7-24) sit on the critical path into exit (R53, R54).
     - The ladder order is unchanged, and no other milestone moves.
5. **Recommendation.** Not applicable. The ruling is recorded here, and the freeze resumes over the amended items.

---

## §4 OPEN technical questions (not decided)

Nothing in this section is decided. The options are listed only to frame the Director's decision. The order is not a
recommendation. **The ids and content below are identical to D-075 §5 (O-1…O-37, plus O-7a); the table is copied from
it (corrected 2026-11-03).** In it, **ledger:n** is `docs/design/director-architecture-and-decision-ledger.md:n`, and
`cr-017`, `cr-006`, `d011`, `d038`, `d039`, `d065`, `d067`, `adr-009`, `adr-019`, `adr-055` and `charter` (the unit
charter) abbreviate the files D-075 names.

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

## §5 Consequences

- **Governance.**
  - D-075 is the controlling record for M7's architecture.
  - Earlier rulings are superseded only to the extents in §3 and D-075 §2, each by a dated note appended to the end of
    the affected file ("## Dated note — 2026-11-03 (D-075; appended, the text above is unchanged)").
  - No frozen text is rewritten.
  - `CLAUDE.md` is not edited.
- **Spec work (administrative, not authorization).** The M7.2 docket, the M7.3 contract list and the dependency plan
  are to be updated:
  - Every item marked DECIDED leaves the docket.
  - The docket keeps the open items of D-075 §5 only: O-1…O-37 and O-7a, with the same ids as §4.
  - M7.3 contracts are derived from §2.2–§2.12.
  - The dependency plan carries the §2.14 critical path.
- **Implementation.** None is authorized. That covers the renderer, the battle simulation, schemas, the guard-test
  comment rescoping, builds, commits and pushes. M7.1 remains the next packet, read-only (REV:557–567), once it is
  authorized.
- **Risk carried forward.** These open questions block downstream packets:
  - O-14 (where the kernel lives) and O-7, O-7a and O-17 (spatial model, numerics, platform) block M7.3.
  - O-8 and O-9 block M7.7 (REV:536).
  - O-1 blocks M7.15's resolver.
  - O-10 and O-11 block M7.6.
  - O-37 blocks M7.18.

  The full "Blocks" column is in §4 and is identical to D-075 §5 (for example O-7: M7.3, M7.7, M7.8; O-17: M7.3, M7.20).

  M7.2 cannot close until the Director rules on these.

## Dated note — 2026-11-04 (D-076; appended, the text above is unchanged)

Record: `docs/d076-m7-decision-reconciliation.md` (Director decisions of 2026-11-04).
- **S83:** the following §5/§4 items are closed or delegated by the Director on 2026-11-04: O-1 (b), O-2 (a), O-3 (a, CR-006 Option A), O-4 (b, count), O-5 (c), O-6 (delegated), O-7 (c), O-7a (b), O-10 (a), O-15 (b), O-16 (delegated model), O-17 (b), O-24 (a); O-22 partially; O-8 shape (b) as a labelled reading. Residues and remaining items: D-076 §4. Garrisoning is decided M7 scope (R54) and is not a Director question. The text above is kept as the 2026-11-03 record.

## Dated note — 2026-11-05 (D-079; appended, the text above is unchanged)

Record: `docs/d079-m7-governance-reconciliation-2026-11-05.md` (Director decisions of 2026-11-05, cited DIR5). Each bullet quotes the superseded line, states the extent and what is retained, and cites the replacing DIR5 section and D-079 decision row (DR). Categories: DIRECTOR DECISION, ADMINISTRATIVE CONSEQUENCE, DELEGATED TECHNICAL DECISION, OPEN (needs the Director), READING (labelled interpretation).

- **S153** (`adr-060-unified-warfare-simulation-architecture.md:5`) — "still needs its own explicit authorization (REV:493). That authorization is open (§4, O-22)." — In part: authorized now — M7.2/M7.3 docs, contracts, dependency planning; the specifically authorized foundational sim and graphics prototype work; builds/tests; commits and pushes to the development branch. Retained: ADR-060/D-075 authorize nothing; not authorized — merge to main, tags, releases, permanent projects before review, the wider programme. **Replaced by** DIR5 §1.3 (D-079 DR4–DR6). [DIRECTOR DECISION]
- **S154** (`adr-060-unified-warfare-simulation-architecture.md:94`) — "### 2.2 Shared-kernel responsibilities (DECIDED; where the kernel lives is OPEN, O-14)" — In part: O-14 decided as direction — separate Sim.Battle project; creation unauthorized until the architecture review; presentation boundaries need a technical recommendation. Retained: REV:412 responsibilities. **Replaced by** DIR5 §1.3 (D-079 DR4–DR6). [DIRECTOR DECISION]
- **S155** (`adr-060-unified-warfare-simulation-architecture.md:119`) — "REV:438: "A siege advances over operational/campaign time and owns investment, blockade, supply, starvation, …"" — In part: evaluated through battle pulses with a siege-health calculation per pulse; max three campaign-turn equivalents; rationing trade-off; named effectiveness inputs; gunpowder transition. Retained: persistent operation; ownership list; assaults via land module; no duplicate combat resolution. [OPEN] which DIR5 §2.4 clock "battle pulse" in §2.2 denotes (D-079 §5 Q14). **Replaced by** DIR5 §2.2 (D-079 DR9). [DIRECTOR DECISION; OPEN residue]
- **S156** (`adr-060-unified-warfare-simulation-architecture.md:154`) — "Animator AI "must not choose targets, invent movement, decide casualties, alter morale or change combat" — IN PART (clarified): as D-075 R24: cosmetic between-step animation permitted; retained: no effect on authoritative state, RNG, timing or hashes. **Replaced by** DIR5 §5.4 (D-079 DR42). [DIRECTOR DECISION]
- **S157** (`adr-060-unified-warfare-simulation-architecture.md:197`) — "performance guarantees", so their values and counted objects are OPEN (O-11)." — In part: values recovered and preserved (50/60/5/3/5/1/2/5) [DIR]; mortar counted object [DELEG]; CAP-1..CAP-5 [DELEG] without new Director decisions. Retained: not measured guarantees (I.9). **Replaced by** DIR5 §2.10 (D-079 DR24, DR25). [DIRECTOR DECISION; DELEGATED TECHNICAL DECISION]
- **S158** (`adr-060-unified-warfare-simulation-architecture.md:204`) — "The parity tolerance is OPEN (O-16)." — In part: 9/10 insufficient; improve before adoption; terrain must influence AutoResolver; quantitative model and repeatable battery [DELEG]. Retained [OPEN]: the acceptance criteria (Phase D recommendation). **Replaced by** DIR5 §3.1 (D-079 DR34). [DIRECTOR DECISION; DELEGATED TECHNICAL DECISION; OPEN residue]
- **S159** (`adr-060-unified-warfare-simulation-architecture.md:220`) — "ADMINISTRATIVE derivation, not a Director decision. The upstream segment is v2's own … Its formal status is O-23." — In part: v2 resequencing not adopted wholesale; the upstream segment is not part of the fixed E2 path; B7 mapped under M7.n, aliases kept. Retained: derivation as history. **Replaced by** DIR5 §1.2 (D-079 DR2, DR3). [DIRECTOR DECISION]
- **S160** (`adr-060-unified-warfare-simulation-architecture.md:226`) — "GA design remains O-36." — In part: full garrison functionality, with its listed functions, is decided M7 scope; mechanics [DELEG] in B7-25. **Replaced by** DIR5 §2.3 (D-079 DR10). [DIRECTOR DECISION; DELEGATED TECHNICAL DECISION]
- **S161** (`adr-060-unified-warfare-simulation-architecture.md:238`) — ""Camera cinema" is OPEN." — In full as open question: cinematic camera in M7, presentation-only (D-077 V5; DIR5 controls). **Replaced by** DIR5 §5.2 (D-079 DR40). [DIRECTOR DECISION]
- **S162** (`adr-060-unified-warfare-simulation-architecture.md:240`) — "which resolver sandbox AI-vs-AI and autoplay battles use is OPEN (O-37; D-075's S4 is withdrawn)." — In part: AI-vs-AI sandbox battles use AutoResolver, no rendering; d011:22–23 stay unsuperseded; campaign rules unchanged. [READING]: Battle Test "autoplay" as AI-vs-AI; the §3.1 parity battery is a harness, not a sandbox battle. **Replaced by** DIR5 §2.13 (D-079 DR30). [DIRECTOR DECISION; READING]
- **S163** (`adr-060-unified-warfare-simulation-architecture.md:242`) — "The engine choice is OPEN (O-13)." — In full as open question: MonoGame 3D; no Godot. **Replaced by** DIR5 §6.1 (D-079 DR44). [DIRECTOR DECISION]
- **S164** (`adr-060-unified-warfare-simulation-architecture.md:254`) — "D-011 §1 "6–12 pulses" (REV:533 makes any amendment conditional; O-4);" — In full: no fixed ceiling. Retained: command pulses. **Replaced by** DIR5 §2.6 (D-079 DR19, DR20). [DIRECTOR DECISION]
- **S165** (`adr-060-unified-warfare-simulation-architecture.md:256`) — "CR-017 §3 and §5 (REV:536; O-8, O-9);" — In part: §3 AC half ruled in substance. Retained: §5 / O-9 open; surge half. **Replaced by** DIR5 §2.8 (D-079 DR22). [DIRECTOR DECISION]
- **S166** (`adr-060-unified-warfare-simulation-architecture.md:257`) — "ADR-019 §1.4 (… denies only a "second global clock"; this binds unless O-29 resolves otherwise);" — In full as a condition: O-29 resolved (local); §1.4's sentence binds. **Replaced by** DIR5 §2.4 (D-079 DR11–DR14). [DIRECTOR DECISION]
- **S167** (`adr-060-unified-warfare-simulation-architecture.md:259`) — "ADR-019 §1.2 `:51–53` (intra-turn supply, upkeep, attrition and siege supply against it are O-30);" — In full (leaves the 'not superseded' list): §1.2 superseded in part; O-30 (b). **Replaced by** DIR5 §2.4 (D-079 DR11–DR14). [DIRECTOR DECISION]
- **S168** (`adr-060-unified-warfare-simulation-architecture.md:267`) — "the kernel freeze (…:118) and the fixed OrderRecord shape (…OrderLog.cs:5), both part of O-14;" — In part: battle half decided (Sim.Battle; no battle pulse coordinate enters Sim.Core/Kernel); the campaign time model still changes clock and order stamp via ADR-061 [ADMIN]. Retained: kernel freeze. **Replaced by** DIR5 §1.3, §2.5 (D-079 DR4–DR6, DR15–DR18). [DIRECTOR DECISION; ADMINISTRATIVE CONSEQUENCE]
- **S169** (`adr-060-unified-warfare-simulation-architecture.md:269`) — "D-002 / ADR-009 (O-13);" — In part: O-13 closed; A-22 [ADMIN]; permanent projects wait for review. **Replaced by** DIR5 §6.1 (D-079 DR44). [DIRECTOR DECISION; ADMINISTRATIVE CONSEQUENCE]
- **S170** (`adr-060-unified-warfare-simulation-architecture.md:270`) — "ADR-022 and CR-013 (O-17);" — In full (leaves the 'not superseded' list): reopened; equality required. **Replaced by** DIR5 §3.2 (D-079 DR35). [DIRECTOR DECISION]
- **S171** (`adr-060-unified-warfare-simulation-architecture.md:317`) — "| # | Open question (words that leave it open) | Alternatives → consequences | Blocks |" — IN PART: rows O-1 (transport), O-8, O-11, O-12, O-14, O-18–O-21, O-23, O-29–O-37 at adr-060:319–356 superseded exactly as the D-075 §5 rows (d075:494–531, offset −175); retained: other rows. **Replaced by** DIR5 §1.2–§6 (D-079 DR2–DR43). [DIRECTOR DECISION]
- **S172** (`adr-060-unified-warfare-simulation-architecture.md:319`, O-1) — "O-1 "… Transport: abstract lift, or controllable vessels."" — In full: naval TacticalResolver, direct ship control, controllable transports; AutoResolver optional, never default. ADR-031 amendment + migration [ADMIN/DELEG]. **Replaced by** DIR5 §2.1 (D-079 DR8). [DIRECTOR DECISION; ADMINISTRATIVE CONSEQUENCE; DELEGATED TECHNICAL DECISION]
- **S173** (`adr-060-unified-warfare-simulation-architecture.md:327`, O-8) — "Action Capacity against Law 3 (CR-017 §3)." — In part: max two actions per decision window [DIR], integrated with AC (ledger §17.2/§19 retained); legal combinations, anti-loop/hit-and-run rules [DELEG]; exemption ADR [ADMIN]. **Replaced by** DIR5 §2.8 (D-079 DR22). [DIRECTOR DECISION; ADMINISTRATIVE CONSEQUENCE; DELEGATED TECHNICAL DECISION]
- **S174** (`adr-060-unified-warfare-simulation-architecture.md:330`, O-11) — "Caps and counted objects." — In part: values preserved [DIR]; mortar object, CAP-1..5 [DELEG]; V1 (transient) indicated by §2.9 [READING]. **Replaced by** DIR5 §2.10, §2.9 (D-079 DR23–DR25). [DIRECTOR DECISION; DELEGATED TECHNICAL DECISION; READING]
- **S175** (`adr-060-unified-warfare-simulation-architecture.md:331`, O-12) — "(a) Name a reference machine and p95 budgets before M7.4/M7.6 benchmarks. (b) Measure first, then set budgets" — In full: Victus is the reference; measure first (b); earlier targets are proposals; software rendering is not evidence. **Replaced by** DIR5 §6.5, §7 (D-079 DR48, DR50). [DIRECTOR DECISION]
- **S176** (`adr-060-unified-warfare-simulation-architecture.md:332`, O-13) — "Host engine and renderer technology" — In full: (a) MonoGame 3D. **Replaced by** DIR5 §6.1 (D-079 DR44). [DIRECTOR DECISION]
- **S177** (`adr-060-unified-warfare-simulation-architecture.md:333`, O-14) — "Where "the kernel" (R5) lives in code." — In full as direction: (a) Sim.Battle; creation after review. **Replaced by** DIR5 §1.3 (D-079 DR4–DR6). [DIRECTOR DECISION]
- **S178** (`adr-060-unified-warfare-simulation-architecture.md:335`, O-16) — "Parity tolerance and granularity" — In part (as K55). **Replaced by** DIR5 §3.1 (D-079 DR34). [DIRECTOR DECISION; OPEN residue]
- **S179** (`adr-060-unified-warfare-simulation-architecture.md:336`, O-17) — "(a) Keep the Linux reference (ADR-022): Windows is built and smoke-tested but not hash-equal. (b) Reopen CR-013" — In full: (b), strengthened to "Require". **Replaced by** DIR5 §3.2 (D-079 DR35). [DIRECTOR DECISION]
- **S180** (`adr-060-unified-warfare-simulation-architecture.md:337`, O-18) — "Sandbox casualty conservation." — In part: isolation from campaign population/casualties decided; conservation form [DELEG] under Law 1. **Replaced by** DIR5 §2.13 (D-079 DR30). [DIRECTOR DECISION; DELEGATED TECHNICAL DECISION]
- **S181** (`adr-060-unified-warfare-simulation-architecture.md:338`, O-19) — "Sandbox force legality" — In part: (b) unrestricted forces, any designed family regardless of era. Not addressed: sandbox siege difficulty from editor structures [DELEG]. **Replaced by** DIR5 §2.13, §2.15 (D-079 DR30, DR33). [DIRECTOR DECISION; DELEGATED TECHNICAL DECISION]
- **S182** (`adr-060-unified-warfare-simulation-architecture.md:339`, O-20) — "Whether editing reaches campaign-encounter terrain." — In full: (a). **Replaced by** DIR5 §2.13 (D-079 DR30). [DIRECTOR DECISION]
- **S183** (`adr-060-unified-warfare-simulation-architecture.md:341`, O-22) — "Per-packet authorization of M7.2, M7.3 and the rest." — In part (as K51). **Replaced by** DIR5 §1.3 (D-079 DR4–DR6). [DIRECTOR DECISION]
- **S184** (`adr-060-unified-warfare-simulation-architecture.md:342`, O-23) — "v2's resequencing, and the formal upstream path." — In part: not adopted wholesale; mapping under M7.n [ADMIN]. **Replaced by** DIR5 §1.2 (D-079 DR2, DR3). [DIRECTOR DECISION; ADMINISTRATIVE CONSEQUENCE]
- **S185** (`adr-060-unified-warfare-simulation-architecture.md:344`, O-25) — "How battlefield assets are produced" — In full: mixed strategy (procedural, purchased, commissioned, custom-rendered); provenance and licensing tracked. **Replaced by** DIR5 §6.4 (D-079 DR47). [DIRECTOR DECISION]
- **S186** (`adr-060-unified-warfare-simulation-architecture.md:346`, O-27) — "Camera cinema" — In full: (b). **Replaced by** DIR5 §5.2 (D-079 DR40). [DIRECTOR DECISION]
- **S187** (`adr-060-unified-warfare-simulation-architecture.md:348`, O-29) — "Whether the operational war pulse is a world-global clock." — In full: (a) local. **Replaced by** DIR5 §2.4 (D-079 DR11–DR14). [DIRECTOR DECISION]
- **S188** (`adr-060-unified-warfare-simulation-architecture.md:349`, O-30) — "Intra-turn supply, upkeep, attrition and siege supply against ADR-019 §1.2" — In full: (b) per-pulse economic effects → ADR-019 §1.2 superseded in part (K2). **Replaced by** DIR5 §2.4 (D-079 DR11–DR14). [DIRECTOR DECISION]
- **S189** (`adr-060-unified-warfare-simulation-architecture.md:350`, O-31) — "Air units in sandbox or test scenarios outside the ratified Age scope reached in play." — In full: (a). **Replaced by** DIR5 §2.13, §2.15 (D-079 DR30, DR33). [DIRECTOR DECISION]
- **S190** (`adr-060-unified-warfare-simulation-architecture.md:351`, O-32) — "Sniper form" — In full: (a). **Replaced by** DIR5 §2.15 (D-079 DR33). [DIRECTOR DECISION]
- **S191** (`adr-060-unified-warfare-simulation-architecture.md:352`, O-33) — "Category-B support (D-065 R10, "persisted quantities or allocations") against R13's entities" — In full: (a) panel-managed capabilities, not battlefield entities; formulas [DELEG]. **Replaced by** DIR5 §2.12 (D-079 DR29). [DIRECTOR DECISION; DELEGATED TECHNICAL DECISION]
- **S192** (`adr-060-unified-warfare-simulation-architecture.md:354`, O-35) — "Which packet ends "Soldiers remain immortal until M7.4"" — In full: M7.14. **Replaced by** DIR5 §2.9 (D-079 DR23). [DIRECTOR DECISION]
- **S193** (`adr-060-unified-warfare-simulation-architecture.md:355`, O-36) — "The GA design" — In part (as K57). **Replaced by** DIR5 §2.3 (D-079 DR10). [DIRECTOR DECISION]
- **S194** (`adr-060-unified-warfare-simulation-architecture.md:356`, O-37) — "Which resolver path sandbox AI-vs-AI and autoplay battles use;" — In part (as K59). **Replaced by** DIR5 §2.13 (D-079 DR30). [DIRECTOR DECISION]
- **S195** (`adr-060-unified-warfare-simulation-architecture.md:374`) — "Implementation. None is authorized. That covers the renderer, the battle simulation, schemas, the guard-test comment rescoping, builds, commits and pushes." — In part (as K51). **Replaced by** DIR5 §1.3 (D-079 DR4–DR6). [DIRECTOR DECISION]
- **S196** (`adr-060-unified-warfare-simulation-architecture.md:378`) — "O-14 (where the kernel lives) and O-7, O-7a and O-17 (spatial model, numerics, platform) block M7.3." — IN PART: O-14 direction decided; project creation (not M7.3 contract text) is gated by the architecture review. **Replaced by** DIR5 §1.3 (D-079 DR4–DR6). [DIRECTOR DECISION]
