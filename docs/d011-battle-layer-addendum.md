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

## 6. Milestone resequence (battle layer inserted as M6)

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

- **D-012** battle-grid representation — rec: coarse deterministic grid + continuous render (above).
- **D-013** pulse budget and pulse-duration fiction — rec: 6–12 pulses ≈ one day of battle.
- **D-014** command friction model — orders beyond the general's command radius/staff capacity suffer delay or degradation (ties command quality to institutions and era; the realism device that makes the general's seat feel real).
