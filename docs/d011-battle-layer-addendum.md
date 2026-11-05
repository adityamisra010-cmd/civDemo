# D-011 CLOSED — THE BATTLE LAYER (Architecture Addendum)
### Director's call: battles are watchable and lightly controllable — general's seat, not soldier's seat.

**Ruling recorded:** the player can enter battles, see the armies, and command at formation level — set attack paths, order assaults, entrench, commit reserves. No individual-soldier control. Armies, and in later eras tanks and air power, are commanded as formations. Battle play happens *inside* the turn structure. Reference feel: Total War's command layer, delivered turn-based — closest real-world class: Ultimate General / Unity of Command, not Starcraft.

---

## 1. Design position: Operational Command Battles (WEGO)

A battle is a sequence of **command pulses**. Each pulse: player issues orders to formations → both sides' orders resolve **simultaneously** → the clash plays back visually → next pulse. Battles run 6–12 pulses max; most end earlier by **morale collapse and rout** (historically true, mechanically merciful). This keeps the entire game turn-based, deterministic, and replayable — a battle is just sub-stepped time with the player in the loop.

**Order verbs (constant across all eras — units change, verbs don't):**
move-along-path · attack formation · hold/entrench · skirmish/screen · flank · commit reserve · withdraw · (era-gated additions: bombard, air strike, dig-in).

## 2. The dual-resolver contract (the architectural core)

```
IBattleResolver.Resolve(BattleSetup) -> BattleOutcome
```

Two implementations, **identical inputs and outputs**:
- **AutoResolver** — Lanchester-family math. Used for all AI-vs-AI battles and whenever the player delegates.
- **TacticalResolver** — the watchable command battle above. Player-only.

**Invariants:**
- `BattleSetup` comes entirely from the strategic sim: formation rosters (real manpower from real cohorts), equipment, supply state, fatigue, morale, general (notable) stats, and terrain of the world-map location.
- `BattleOutcome` returns casualties per formation (debited from actual population cohorts via `Ledger` — Law 1, exact), equipment losses, prisoners, morale deltas, ground result, retreat vectors, general experience.
- **Determinism:** player battle orders append to the order log per pulse; replay reproduces the battle blow-for-blow.
- **Parity rule:** AutoResolver is calibrated so its outcomes match the *median* of TacticalResolver autoplay distributions for the same setup — the world's history must not depend on which mode was used. (This is the Total War disease, pre-treated.) Calibration battery gets a battle-parity suite.
- **Delegate-with-agency:** skipping a battle hands it to the assigned general — their competence and traits parameterize the AutoResolver. Delegation doctrine (Spine) extends naturally to the battlefield.

## 3. Battle anatomy v1

- **Trigger:** armies contact during the military phase → prompt: *Command* (enter battle) or *Delegate* (general auto-resolves). Strategic turn pauses during command; battles read frozen turn-start state per the sub-step rule.
- **Battlefield:** generated from the contact cell's actual terrain — river lines, ridges, woods, roads. Under the hood a coarse deterministic grid (~48×32) for pathing and zones; continuous rendering on top. *(Logged as D-012, recommendation attached, finalize at battle-layer spec.)*
- **Formations are atomic:** warband → phalanx/cohort → tercio → line battalion → armored company → air wing. Each carries strength, cohesion, fatigue, ammo (where era-relevant), facing and frontage.
- **Resolution per pulse:** movement with interpenetration rules, ranged fire, melee as paired Lanchester engagements modified inside the equation by flanking, terrain, elevation, fortification, morale, and command radius (Law 2, precise form).

## 4. Presentation doctrine (expectation set honestly)

Formations render as **unit tokens/blocks with strength and morale bars**, banners, facing arrows, and combat feedback (tracer arcs, impact flashes, casualty ticks, rout animations). A cheap high-flavor flourish is sanctioned: each token drawn as a *cluster of tiny sprites* thinning as strength drops — Ultimate-General-style, well inside MonoGame + no-art-team reality. What v1 will **not** be: 3D soldiers and camera cinema. The drama comes from stakes (those are real men from real cohorts; losses scar the pyramid) and from readable, consequential command — not from polygon count.

## 5. Era scaling

v1 ships ancient/classical warfare only (matching the vertical slice). Later eras arrive as **data + a few new verbs**, with their expansions: gunpowder (ranged dominance, entrenchment value), industrial (artillery bombard, rail-fed fronts), modern (armored formations, air as *strike missions on the battle map* — never dogfight simulation). Naval command battles: post-slice candidate; naval stays auto-resolve until then. Sieges remain multi-turn strategic operations that can spawn assault battles.



> *(2026-10-03 roadmap rebase: knowledge is now M6, politics stays M8 — see §6 note.)*
> **D-040 (2026-08-08) uses this resequence.** `docs/d040-discovery-and-control.md` D3/D4 place
> map extent and discovery at **M7 (knowledge)** and the political consequences of weak control at
> **M8 (politics)**, per the table below. D-040 Part B also extends **D-039** Part B's
> reconnaissance clauses to a second scale (strategic discovery), naming the one difference:
> reconnaissance reports a lagging position of a moving thing, discovery a permanent fact about
> the land.## 6. Milestone resequence (battle layer inserted as M6)

> **Superseded 2026-10-03 (Director roadmap rebase).** This §6 ordering (M6 battle, M7 knowledge, M8 politics) is superseded. The Battle Layer is now
> **M7** and FOLLOWS **M6 Knowledge / Research / Technology**: the battle layer consumes M6's authoritative
> technology/capability state rather than shipping generic battle with tech added later. Politics &
> diplomacy remain M8, society M9, M10 Integrated Civilization Simulation, M11+ Depth & Content Expansion. The battle mechanics in §1–§5
> and the decisions in §8 are unchanged (they now close at the **M7** spec). Record: `docs/milestones.md`
> §"Roadmap rebase 2026-10-03". "Ancient Vertical Slice" and "Era expansions" are obsolete milestone names: M10 integrates and validates the whole
> model across all nine Ages A1–A9 as one simulation (ancient content is not a prerequisite for later Ages), and M11+ adds
> depth, not "later Ages". **Ages are a simulation dimension, not development milestones** — never "build A1 → validate A1 →
> build A2". §5's "matching the vertical slice" and the M10/M11+ rows below
> are read accordingly. The table below is retained as history.

| M | Content | Note |
|---|---|---|
| M0–M3 | unchanged | kernel → skeleton → Malthus → markets |
| M4 | trade + strategic war, **AutoResolver only** | armies, supply, attrition on world map |
| M5 | governing loop | "it's a game now" checkpoint |
| **M6** | **Battle Layer v1** | TacticalResolver, ancient units, parity suite |
| M7 | knowledge & divergence | was M6 |
| M8 | politics & diplomacy | was M7 |
| M9 | society layer (religion, culture, disease) | was M8 |
| M10 | **Ancient Vertical Slice** — now includes command battles | go/no-go gate |
| M11+ | era expansions | each adds its battle-layer units/verbs as data |

## 7. What does NOT change

M0 task packets T0.1–T0.9: untouched — build proceeds. Kernel contract: already supports sub-stepped military phases and order-logged player input. D-002 (MonoGame + ImGui.NET): *strengthened* — MonoGame is a genuine 2D game framework; the battle map is squarely in its lane. Strategic-layer military spec (M4): unchanged; the tactical layer plugs into the reserved interface exactly as designed.

## 8. New decisions opened (close at M6 spec, recommendations noted)

*(2026-10-03 roadmap rebase: "M6 spec" here now means the **M7 Battle Layer** spec.)*

- **D-012** battle-grid representation — rec: coarse deterministic grid + continuous render (above).
- **D-013** pulse budget and pulse-duration fiction — rec: 6–12 pulses ≈ one day of battle.
- **D-014** command friction model — orders beyond the general's command radius/staff capacity suffer delay or degradation (ties command quality to institutions and era; the realism device that makes the general's seat feel real).
  - **CLOSED by D-039** (`docs/d039-command-fog-and-siege.md`, director ruling). The line above is retained unedited — superseded, not deleted, per S8 §5's append-only audit trail. D-039 Part A rules the model: order latency is DELAY, NOT DEGRADATION (A3); the three information channels — position, strength, outcome — are separate quantities (A2); command capability grows faster than force size (A4); and nothing gates on era (A5, Law 4).

## Dated note — 2026-11-03 (D-075; appended, the text above is unchanged)

Rows S1–S15 of D-075 §2.1. Clauses not listed here stay binding, including the §2 dual-resolver contract for campaign battles.

- §1, line 4 — "Battle play happens *inside* the turn structure." — SUPERSEDED IN PART, for sandbox battles only (retained: campaign battles; time nesting left OPEN, D-075 O-3/O-5). Replaced by D-075 R43; Director's revision §F (REV:481), docs/d075-m7-unified-warfare-direction.md (S1).
- §1, line 4 — "closest real-world class: Ultimate General / Unity of Command, not Starcraft." — SUPERSEDED IN PART: the Ultimate General / Unity of Command reference class (retained: "No individual-soldier control", formation-level command, "Total War's command layer, delivered turn-based", "not Starcraft"). Replaced by D-075 R21; Director's revision §A (REV:382), docs/d075-m7-unified-warfare-direction.md (S2).
- §1, lines 12–13 — "(era-gated additions: bombard, air strike, dig-in)" — SUPERSEDED IN PART: the words "era-gated"; additions are gated by capability state (retained: the verb set, constant across eras). Replaced by D-075 R11; Director's revision §B3 (REV:434), docs/d075-m7-unified-warfare-direction.md (S3).
- §2, line 22 — "**AutoResolver** — Lanchester-family math. Used for all AI-vs-AI battles and whenever the player delegates." — SUPERSEDED IN PART, sandbox only: sandbox AI-vs-AI and autoplay battles run on the shared tactical kernel (retained: campaign battles). Replaced by D-075 R2, R43, R44; Director's revision §A and §F (REV:381, :481, :485), docs/d075-m7-unified-warfare-direction.md (S4).
- §2, line 26 — "`BattleSetup` comes entirely from the strategic sim" — SUPERSEDED IN PART, sandbox only: the sandbox configures its own setups (retained: campaign battles; sandbox legality left OPEN, D-075 O-19). Replaced by D-075 R44; Director's revision §F (REV:483), docs/d075-m7-unified-warfare-direction.md (S5).
- §3, line 34 — "**Trigger:** armies contact during the military phase → prompt: *Command* (enter battle) or *Delegate*" — SUPERSEDED IN PART, sandbox only: the sandbox starts battles without campaign contact (retained: campaign battles, including "battles read frozen turn-start state per the sub-step rule"). Replaced by D-075 R43, R44; Director's revision §F (REV:481, :483), docs/d075-m7-unified-warfare-direction.md (S6).
- §3, line 35 — "**Battlefield:** generated from the contact cell's actual terrain" — SUPERSEDED IN PART, sandbox only: Scenario Editor terrain (retained: campaign battles; the grid, D-012, left OPEN as D-075 O-7; campaign-encounter editing OPEN as O-20). Replaced by D-075 R44; Director's revision §F (REV:484), docs/d075-m7-unified-warfare-direction.md (S7).
- §3, line 36 — "**Formations are atomic:** warband → phalanx/cohort → tercio → line battalion → armored company → air wing." — SUPERSEDED IN PART: "atomic" no longer bars individually simulated member entities (retained: the formation is the command unit, and the ladder's family names; ladder sizes and stored versus member-derived attributes left OPEN, D-075 O-11). Replaced by D-075 R13, R14, R15, R19; Director's revision §A, §C and §H (REV:383, :448–449, :538), docs/d075-m7-unified-warfare-direction.md (S8).
- §4, line 41 — "Formations render as **unit tokens/blocks** …"; "each token drawn as a *cluster of tiny sprites* … well inside MonoGame + no-art-team reality"; "What v1 will **not** be: 3D soldiers and camera cinema." — SUPERSEDED IN PART: tokens/blocks as the battle presentation, the sprite-cluster flourish and its "no-art-team reality" premise, and "3D soldiers" (retained: strength and morale bars, banners, facing arrows, combat feedback, "The drama comes from stakes"; "camera cinema" left OPEN as D-075 O-27 and "MonoGame" as O-13). Replaced by D-075 R16, R21, R22, R24; Director's revision §A, §C and §E (REV:382, :450, :454, :473), docs/d075-m7-unified-warfare-direction.md (S9).
- §5, line 45 — "v1 ships ancient/classical warfare only" — SUPERSEDED for M7 scope (implementation stays bounded by D-075 R12, REV:537, "implement only ratified era/capability scope"). Replaced by D-075 R7, R51; Director's revision §B2 and §I (REV:420–422, :516, :551), docs/d075-m7-unified-warfare-direction.md (S10).
- §5, line 45 — "Later eras arrive as **data + a few new verbs**" — SUPERSEDED IN PART: domain modules may carry distinct equations and mechanics (retained: later eras arrive with data). Replaced by D-075 R8, R39; Director's revision §B2 and §B5 (REV:426, :442), docs/d075-m7-unified-warfare-direction.md (S11).
- §5, line 45 — "air as *strike missions on the battle map* — never dogfight simulation" — SUPERSEDED: strike-only air and "never dogfight"; air-to-air is within the air module (implementation bounded by D-075 R12). Replaced by D-075 R7, R12; Director's revision §B2, §G and §H (REV:421, :512, :537), docs/d075-m7-unified-warfare-direction.md (S12).
- §5, line 45 — "Naval command battles: post-slice candidate; naval stays auto-resolve until then." — SUPERSEDED IN PART: naval as a deferred domain; the naval module is M7.15 (retained/OPEN: "stays auto-resolve" is not decided, D-075 O-1). Replaced by D-075 R7, R42; Director's revision §B2, §G and §H (REV:422, :511, :532), docs/d075-m7-unified-warfare-direction.md (S13).
- Dated note, lines 60–61 — "The battle mechanics in §1–§5 and the decisions in §8 are unchanged" — SUPERSEDED IN PART: "unchanged", to the extent of the rows above only (retained: everything else). Replaced as the rows above (S14).
- §7, line 82 — "D-002 (MonoGame + ImGui.NET): *strengthened* — MonoGame is a genuine 2D game framework; the battle map is squarely in its lane." — SUPERSEDED IN PART: the premise that the battle map is a 2D surface (retained: the kernel-contract sentence and the M4 interface sentence, as claims whose adequacy is OPEN, D-075 O-14; D-002 and ADR-009 as host stack left OPEN, O-13). Replaced by D-075 R21, R22; Director's revision §A and §E (REV:382, :473), docs/d075-m7-unified-warfare-direction.md (S15).
- §8, line 89 (D-013) and §3 line 37 (paired Lanchester) are not superseded; they are OPEN for the Director (D-075 O-4, O-15).

Recorded administratively under the Director's decisions of 2026-11-03; the historical text above is preserved.

### Correction — 2026-11-03 (D-075)

*Appended after the dated note above; no line above is edited.*

- The bullet above for §2, line 22 (D-075 row S4) is **withdrawn**. Line 22 ("Used for all AI-vs-AI battles and whenever the player delegates") is **not** superseded. No line of the Director's revision says which resolver sandbox AI-vs-AI or autoplay battles use: REV:381 and :481 give the sandbox the same kernel and domain modules as campaign battles, and REV:485 lists "autoplay" for Battle Test.
- §2, line 23 ("**TacticalResolver** — the watchable command battle above. Player-only.") is treated the same way: **not** superseded.
- Both lines stay as written until the Director rules on **D-075 O-37** (which resolver path sandbox AI-vs-AI and autoplay battles use). Its alternatives are in D-075 §5. Campaign battles keep lines 22–23 under every alternative.
- Where the note above cites "the rows above" (the line 60–61 bullet, S14), the range excludes the withdrawn S4.

## Dated note — 2026-11-04 (D-076; appended, the text above is unchanged)

Record: `docs/d076-m7-decision-reconciliation.md` (Director decisions of 2026-11-04).
- **S72 (§1, "Battles run 6–12 pulses max"):** superseded. No count ceiling; termination by elimination, retreat, rout or surrender, plus a five-pulse inactivity rule for hidden inactive units (D-076 N9–N11). Command pulses, simultaneous resolution and "most end earlier by morale collapse and rout" are retained.
- **S73 (D-013, line 89):** closed and amended: no count ceiling. Pulse length is open (D-076 Q-B1).
- **S74 (§3, "melee as paired Lanchester engagements"), in part:** TacticalResolver resolution is entity-level, aggregated (D-076 N13). The AutoResolver's "Lanchester-family math" is unaffected.
- **S75 (§5, "naval stays auto-resolve until then"):** superseded. Naval battles support full tactical control; auto-resolve is optional, never the default (D-076 N8).
- **S76 (§3, D-012 grid):** D-012 closed: grid pathfinding with continuous fixed-point LOS and projectiles (D-076 N6, N7).
- **S65 (§3, "battles read frozen turn-start state per the sub-step rule"), in part:** read as sub-step-start (D-076 N2).
- §2's dual-resolver contract and parity rule are retained; D-076 N14 supplies the quantitative parity model.

## Dated note — 2026-11-04 (D-077; appended, the text above is unchanged)

Record: `docs/d077-visual-workstream-procedural-graphics-avatar-cinematics.md` (Director visual decisions of 2026-11-04).
- **S88 (§4, "What v1 will not be: 3D soldiers and camera cinema"):** superseded. Camera cinema (O-27) is decided in M7 (D-077 V5); 3D soldiers were already decided (D-075 S9). "The drama comes from stakes" is retained.

## Dated note — 2026-11-05 (D-079; appended, the text above is unchanged)

Record: `docs/d079-m7-governance-reconciliation-2026-11-05.md` (Director decisions of 2026-11-05, cited DIR5). Each bullet quotes the superseded line, states the extent and what is retained, and cites the replacing DIR5 section and D-079 decision row (DR). Categories: DIRECTOR DECISION, ADMINISTRATIVE CONSEQUENCE, DELEGATED TECHNICAL DECISION, OPEN (needs the Director), READING (labelled interpretation).

- **S197** (`d011-battle-layer-addendum.md:10`) — "Battles run 6–12 pulses max" — in full as to the ceiling (already superseded by D-076 S72; DIR5 re-decides it) [DIRECTOR DECISION]; retained: command pulses, simultaneous resolution, "most end earlier by morale collapse and rout". **Replaced by** DIR5 §2.6 (D-079 DR19, DR20). [DIRECTOR DECISION]
- **S198** (`d011-battle-layer-addendum.md:41`) — "and from readable, consequential command — not from polygon count" — in part: "— not from polygon count" (and, as already D-075 S9, the "no-art-team reality" premise) [DIRECTOR DECISION]; retained: "The drama comes from stakes (those are real men from real cohorts; losses scar the pyramid) and from readable, consequential command", strength/morale bars, banners, facing arrows, combat feedback. **Replaced by** DIR5 §6, §6.2, §6.4 (D-079 DR43, DR45, DR47). [DIRECTOR DECISION]
- **S199** (`d011-battle-layer-addendum.md:45`) — "Naval command battles: post-slice candidate; naval stays auto-resolve until then." — in full (already D-075 S13 in part, D-076 S75 in full) [DIRECTOR DECISION]; DIR5 adds controllable transport vessels, which closes the O-1 transport remainder (D-076 Q-B5); ADMIN consequence: ADR-031 amendment and save migration for controllable transports. **Replaced by** DIR5 §2.1 (D-079 DR8). [DIRECTOR DECISION; ADMINISTRATIVE CONSEQUENCE]
- **S200** (`d011-battle-layer-addendum.md:45`) — "Sieges remain multi-turn strategic operations that can spawn assault battles." — in part: the open-ended duration and the evaluation model [DIRECTOR DECISION] — a siege lasts at most three campaign-turn equivalents and is evaluated pulse by pulse through a siege-health calculation (attack lowers it; rationing lowers defence but extends endurance); equations, calibration and the gunpowder transition are DELEGATED (§2.2); retained: the siege as a persistent operation, and assaults. **Replaced by** DIR5 §2.2 (D-079 DR9). [DIRECTOR DECISION; DELEGATED TECHNICAL DECISION]
- **S201** (`d011-battle-layer-addendum.md:45`) — "v1 ships ancient/classical warfare only" — in full (already D-075 S10, and S12 for "air as strike missions … never dogfight simulation" on the same line) [DIRECTOR DECISION]; DIR5 re-decides: land, naval, air, siege and combined arms in one framework, ancient, medieval and modern not flattened; sandbox instantiates any designed family, campaign availability gated by era and technology. **Replaced by** DIR5 §2, §2.15 (D-079 DR7, DR33). [DIRECTOR DECISION]
- **S202** (`d011-battle-layer-addendum.md:82`) — "Kernel contract: already supports sub-stepped military phases and order-logged player input." — in part, as a sufficiency claim [DIRECTOR DECISION + ADMIN]: event-triggered decision windows at non-integer time, War Pulses crossing turn boundaries by sub-stepping and fractional-time carry need a sub-turn time coordinate and order stamp (proposed kernel ADR-061, D-076 A-13; kernel code still needs Director sign-off, m0-kernel-spec:118, DIR5 §10); retained: the sub-step rule within one sub-step; "M0 task packets … untouched"; the M4 interface sentence. **Replaced by** DIR5 §2.4, §2.5 (D-079 DR11–DR18). [DIRECTOR DECISION; ADMINISTRATIVE CONSEQUENCE]
- **S203** (`d011-battle-layer-addendum.md:82`) — "D-002 (MonoGame + ImGui.NET): *strengthened* — MonoGame is a genuine 2D game framework; the battle map is squarely in its lane." — in part (2D premise already D-075 S15) [DIRECTOR DECISION]: MonoGame 3D is explicitly selected (O-13 closed, already D-077 V6); Sim.Battle is the chosen simulation-project direction (O-14 → (a)); retained: MonoGame + ImGui.NET as host stack. **Replaced by** DIR5 §6.1, §1.3 (D-079 DR4–DR6, DR44). [DIRECTOR DECISION]
- **S204** (`d011-battle-layer-addendum.md:89`) — "rec: 6–12 pulses ≈ one day of battle." — in full as to the count (already D-076 S73, D-013 closed) [DIRECTOR DECISION]; tactical pulse LENGTH stays OPEN (D-076 Q-B1; DIR5 §5.4's two-second interval is an example, not a ruling). **Replaced by** DIR5 §2.6 (D-079 DR19, DR20). [DIRECTOR DECISION; OPEN residue]
- **S205** (`d011-battle-layer-addendum.md:97`) — "time nesting left OPEN, D-075 O-3/O-5" — closes the open item (already D-076 N2/N3) [DIRECTOR DECISION]: four clocks; War Pulses cross turn boundaries by sub-stepping; hybrid event-driven and integer time; retained: the S1 row itself. **Replaced by** DIR5 §2.4, §2.5 (D-079 DR11–DR18). [DIRECTOR DECISION]
- **S206** (`d011-battle-layer-addendum.md:101`) — "sandbox legality left OPEN, D-075 O-19" — closes O-19 [DIRECTOR DECISION]: unrestricted experimental force creation, including campaign-unavailable units; any designed unit family regardless of campaign era; campaign availability stays gated; sandbox siege difficulty from editor structures is DELEGATED design (DIR5 silent). **Replaced by** DIR5 §2.13, §2.15 (D-079 DR30, DR33). [DIRECTOR DECISION; DELEGATED TECHNICAL DECISION]
- **S207** (`d011-battle-layer-addendum.md:103`) — "the grid, D-012, left OPEN as D-075 O-7; campaign-encounter editing OPEN as O-20" — closes both [DIRECTOR DECISION]: O-7 (already D-076 N6/N7; DIR5 §2.7 hybrid grid pathfinding with continuous fixed-point LOS/projectiles) and O-20 → (a) (loaded campaign encounters inherit campaign terrain; no encounter terrain editing; terrain editing only in the sandbox). **Replaced by** DIR5 §2.7, §2.13 (D-079 DR21, DR30). [DIRECTOR DECISION]
- **S208** (`d011-battle-layer-addendum.md:104`) — "ladder sizes and stored versus member-derived attributes left OPEN, D-075 O-11" — in part [DIRECTOR DECISION]: the recovered caps are preserved as Director caps (50/60/5/3/5/1/2/5); entity resolution aggregates into formation outcomes and entities collapse into formation counts after battle; DELEGATED: the mortar counted object and resolution of CAP-1..CAP-5 "without inventing new Director decisions" (covers ladder sizes against caps); stored vs member-derived formation attributes stay engineering design. **Replaced by** DIR5 §2.9, §2.10 (D-079 DR23–DR25). [DIRECTOR DECISION; DELEGATED TECHNICAL DECISION]
- **S209** (`d011-battle-layer-addendum.md:105`) — ""camera cinema" left OPEN as D-075 O-27 and "MonoGame" as O-13" — closes both (already D-077 V5/V6, S88/S94) [DIRECTOR DECISION]: Total War-style cinematic camera in M7; MonoGame 3D selected. **Replaced by** DIR5 §5.2, §6.1 (D-079 DR40, DR44). [DIRECTOR DECISION]
- **S210** (`d011-battle-layer-addendum.md:109`) — "retained/OPEN: "stays auto-resolve" is not decided, D-075 O-1" — closes O-1 including its transport remainder [DIRECTOR DECISION]: (already D-076 N8) full tactical naval control, AutoResolver optional never default; controllable transport vessels included (closes D-076 Q-B5). **Replaced by** DIR5 §2.1 (D-079 DR8). [DIRECTOR DECISION; ADMINISTRATIVE CONSEQUENCE]
- **S211** (`d011-battle-layer-addendum.md:111`) — "as claims whose adequacy is OPEN, D-075 O-14; D-002 and ADR-009 as host stack left OPEN, O-13" — closes O-13 (MonoGame 3D, DIR5 §6.1; already D-077 V6) and O-14 as to direction (separate Sim.Battle project chosen; creation unauthorized until the architecture review, DIR5 §1.3) [DIRECTOR DECISION]; the kernel-contract sentence's adequacy is answered by DIR5 §2.5 (sub-turn decision windows need the kernel ADR, ADMIN); also corrects the O-14 label (O-14 is "where the kernel lives in code", D-075:508, not kernel adequacy). **Replaced by** DIR5 §1.3, §6.1, §2.5 (D-079 DR4–DR6, DR15–DR18, DR44). [DIRECTOR DECISION; ADMINISTRATIVE CONSEQUENCE]
- **S212** (`d011-battle-layer-addendum.md:112`) — "they are OPEN for the Director (D-075 O-4, O-15)." — closes both (already D-076 N9, N13) [DIRECTOR DECISION]: no pulse ceiling; entity-level resolution aggregated into formation-level outcomes; AutoResolver's Lanchester-family math unaffected. **Replaced by** DIR5 §2.6, §2.9 (D-079 DR19, DR20, DR23). [DIRECTOR DECISION]
- **S213** (`d011-battle-layer-addendum.md:122`) — "Both lines stay as written until the Director rules on **D-075 O-37**" — closes O-37 → (b) [DIRECTOR DECISION]: AI-vs-AI sandbox battles use the AutoResolver and need no rendering; "Campaign resolver rules remain unchanged"; lines 22–23 stand unsuperseded; TacticalResolver autoplay for the parity battery stays d011:29's calibration instrument (READING). **Replaced by** DIR5 §2.13 (D-079 DR30). [DIRECTOR DECISION; READING]
- **S214** (`d011-battle-layer-addendum.md:128`) — "plus a five-pulse inactivity rule for hidden inactive units (D-076 N9–N11)" — in part [DIRECTOR DECISION]: a hidden unit stationary for at least five pulses may be treated as routed or lost (answers D-076 Q-B2 in part); READING (D-079 R17): the prototype's "escaped, withdrawn intact" outcome is not one of the named consequences; whether and how the rule applies to a unit, and the inactivity-vs-concealment/defence/waiting distinction, are DELEGATED design; retained: N9–N11 as recorded. **Replaced by** DIR5 §2.6 (D-079 DR19, DR20). [DIRECTOR DECISION; DELEGATED TECHNICAL DECISION; READING]
