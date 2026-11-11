# Battle entities that stand for many persons — PROPOSAL (M7 lane CB, 2026-11-11)

**Status: PROPOSAL.** Nothing here is a Director decision unless it is quoted from one. Every reading is labelled READING with
both alternatives. No data changes: `Sim.Data/content/unit-families.json` and every cap value stay as they are (D-079 §4 D10:
"Nothing changes in data now"; §6). The prototype code it describes is in `prototypes/m7-sprint1/Proto.M7/` (isolated; D-079
Q5 stays OPEN, so nothing in `Sim.*` changed). Lane report: `prototypes/m7-sprint1/reports/H-CB.md`.

**Authority.**
- [D-082](../d082-morale-caps-ties-director-answers-2026-11-10.md) **DA5** (DECIDED, answers READING C1): "A soldier cap counts
  battlefield units, not people: lane D's READING C1 (b), the battle-entity rule that lane W applies." Director's words:
  "Battlefield units."
- **DA5-R (a)** (APPLIED): "a battle entity in lane W's rule. A campaign formation of P persons fights as n entities, n ≤ the cap
  (`BattleCollapse`; `reports/H-W.md` §5). The cap bounds how many entities one formation fights as; a formation's size in
  persons is limited by the economy, not by the cap." Alternative (b): "The cap counts the formations of that family on one
  battlefield."
- **D-082 §3:** "Lane D READING C1 (a), persons, as applied in the caps prototype (`DirectorCaps.CountOf`, `FormationRoster`)
  and in the sandbox's Quick Battle split: superseded by (b) (DA5). Lane D's own consequences of (b) (caps proposal §2.2)
  follow: CAP-1 and CAP-2 dissolve; formation size is limited by the economy; and what one entity stands for (its persons, hit
  points, morale weight and fire) is undesigned and becomes required design for M7.6."
- **DA4** (DECIDED): "DR24's caps apply in the War Sandbox". **DA3** (DECIDED): DR25's numbers hold the same at every stage
  (the economic side: [economic-scale-proposal.md](economic-scale-proposal.md) §8).
- D-079 **DR23** ("Battle-transient individual entities collapse into formation counts after battle"; "No persistent individual
  veteran table"; "Casualty and Ledger integration must remain coherent"), **DR24** (the cap values), **DR25** ("Hard caps may
  protect performance, but ordinary military scale should be governed by the economy"), §4 **D10** (CAP-1..CAP-5, the mortar
  counted object), **D11** (the resolvers move together), §1.5 **R6**, **R7**.
- Lanes this design rests on: lane W's collapse (`reports/H-W.md` §5), lane D's caps prototype
  ([caps-and-counted-objects-proposal.md](caps-and-counted-objects-proposal.md)), lane N's CAP-5 treatment and domain formations
  (`reports/H-N.md` §10), lane P's AutoResolver (`reports/H-P.md`), lanes MOR and MOR2's morale
  ([morale-proposal.md](morale-proposal.md) §3.2, §4, §11).

Labels: **DD** Director decision (quoted); **READING** an interpretation with both alternatives; **PROPOSAL** this lane's design;
**MEASURED** run in this lane on its branch; **DERIVED** exact arithmetic; **INFERRED** argued, not measured.

---

## 0. Summary

| Question (D-082 §3) | Answer | State |
|---|---|---|
| How many entities does a formation of P persons fight as? | soldier class: n = min(P, cap); machine class: one per machine; uncapped: one per person (§1) | **implemented** (`DirectorCaps.EntitiesOf`), one rule for lanes W and D |
| Persons per entity and the remainder rule (Law 1) | each entity stands for the rational share P ÷ n; persons become integers once, at the collapse, by the largest remainder (§2) | **implemented** (lane W's `BattleCollapse.Apportion`, now the only integerization); MEASURED exact |
| Its hit points; its fire and melee output | a block of the persons it stands for: hit points and output scale with its living persons (§3, option E1) | **PROPOSAL, not built**: today an entity fights as one person whatever its share (MEASURED gap) |
| How casualties map back to persons | through the collapse categories (dead, wounded, routed, surrendered, fit), apportioned by the largest remainder (§2) | **implemented** (unchanged lane W rule) |
| The AutoResolver's representation | its aggregate counts become persons; its frontage stays in entities (§3.4) | **PROPOSAL, not built** |
| Its morale weight | none of its own: morale is per formation and per army; the formation's size in every morale term is persons (§4) | settlement: **unchanged**; in battle: lane MOR's "fight it won" weight **implemented** in persons, both resolvers |
| CAP-1, CAP-2 | dissolve: no data row needs a retune (§5) | DERIVED from the rule |
| Performance at large P | a battle's cost depends on its entities, which the caps bound, not on its persons (§6) | **MEASURED** |

---

## 1. The rule: how many entities (implemented)

`Proto.M7/Caps/DirectorCaps.cs` `EntitiesOf(class, persons)` is the one rule; `CountOf`, `Check`, `FormationRoster`, the War
Sandbox's force creation and its Campaign Encounter all use it (`SandboxSession.Fielded`).

| DR24 class | Cap | Entities a formation of P persons fights as | Persons the cap still bounds |
|---|---:|---|---|
| Heavy/aggregated infantry | 50 | min(P, 50) | none (economy) |
| Archers and lighter infantry | 60 | min(P, 60) | none (economy) |
| Snipers | 2 | min(P, 2) | none (economy) |
| Tanks, helicopters, ships, fixed-wing aircraft | 5, 3, 5, 1 | one per machine: ⌈P ÷ crew⌉, refused above the cap | cap × crew (one entity per machine) |
| Mortars | 5 | one per crewed tube: ⌈P ÷ crew per tube⌉, refused above 5 (lane D §2.1, kept) | 5 × crew per tube |
| No cap (CAP-5) | — | one per person (lane N's treatment, kept) | none |

No crew value is shipped (CAP-3 is a TUNE rule of M7.6): a machine class without one has no entity count and its check refuses
(`NoCrewRule`), as before.

**MEASURED (tests in `Proto.M7.Tests/LaneCB/BattleEntityRuleTests.cs`):** every soldier class at P = 0, 1, cap − 1, cap, cap + 1,
2 × cap and 10¹², the machine classes on both sides of cap × crew (TEST INPUT crews), the uncapped class at 10¹²; a soldier formation grown past its cap in persons by every roster edit (create, reinforce, split, transfer, merge) is
never refused and always counts min(P, cap); a tank formation is still refused one person past 5 × crew; for every family the
sandbox fields and every size around its cap, the sandbox's own force creation (lane D) and a Campaign Encounter (lane W's
campaign formations) create the same formation; a 3,000-person heavy-infantry formation is accepted by the check with 50
counted entities, fielded identically by both paths, and fights the same battle (equal result hash, casualty rows and session
hash).

**READING CB-1 (what a machine-class entity is).**
- **(a) APPLIED: one machine per entity.** The cap bounds the machines a formation fights as, so its persons are bounded by cap ×
  crew; a large armoured or naval force is several formations. Basis: lane D §2.1 ("one entity per tube"; the brief: "Keep the
  mortar crewed tube"), lane N's battle model (`FormationSpec.Count` = hulls or airframes, crews in the battle's personnel ledger),
  and a machine's own state (its hull, its ammunition, its crew's minimum to fire) being per machine.
- **(b) machine entities aggregate like soldier entities:** a formation of M machines fights as n ≤ cap entities, each standing
  for M ÷ n machines; its persons are then limited by the economy alone. Consequence: lane N's per-hull damage, flooding,
  boarding and ramming state, and the crewed tube's minimum crew, would all need an aggregate form; nothing in DA5's text (a
  "soldier cap") requires it. Data it would touch (cited, not changed): none directly, but the CAP-3 crew rule would then no
  longer bound the 30 machine rows lane D listed (caps proposal §3: 605 :134, 1102 :291, the vessel rows 901–906 :175–:180,
  1001–1006 :191–:196, 1701–1703 :250–:252, 1801 :265, 907 :266, 908 :279, 1901 :280, the fixed-wing rows 1101, 1105, 1106, 1103
  :209–:212 and 2301–2303 :330–:332, the drones 2101 :304 and 1104 :305).

**READING CB-2 (how many entities below the cap).**
- **(a) APPLIED: n = min(P, cap)**, the most entities the cap allows, so a formation within its cap fights one person per entity
  exactly as every battle did before D-082 (MEASURED: no resolver golden or battery pin moved; the 18 moved pins are lane D's
  roster and sandbox tests, `reports/H-CB.md` §2.2), and the share is as small as the cap permits.
- **(b) a fixed persons-per-entity k per identity** (a TUNE value: a file, a section): n = min(cap, ⌈P ÷ k⌉). Consequence: equal
  shares across formations of one identity, fewer entities for small formations (cheaper battles); a new TUNE value per identity
  (data, M7.6); every existing battle of P ≤ cap changes unless k = 1. Data it would touch (cited, not changed): every
  soldier-class identity row of `unit-families.json`, family 1 `light_infantry` 102–105 (:46–:49), family 2 `spear_anti_cavalry`
  201–204 (:61–:64), family 3 `heavy_infantry` 301, 309, 302–308 (:75–:83) and family 4 `ranged_infantry` 401–408 (:96–:103), each
  gaining a k.

**READING CB-4 (which formations lane D's `ReinforceToCaps` fills)** *(added in verification round 1, CB-R1-01: this choice was
first written as a consequence of DA5; it is this lane's reading, and DA5 does not force it)*.
- **(a) APPLIED: machine classes only.** Only formations whose cap bounds their persons take part (machine classes with a crew rule,
  up to cap × crew); a soldier formation takes no part and is reinforced by `Reinforce`, by any number. Consequence: lane D's automatic
  reinforcement of understrength soldier formations stops; three of lane D's pins moved because of this reading, not because of DA5
  (`ReinforceToCaps_TieDense_…`, `…_ExactFractionTiesAcrossClasses_…`, `…_StopsAtEveryCapAndAtTheEmptyDepot_…`; re-based on tanks,
  ships and helicopters).
- **(b) soldier formations filled up to their full complement of entities**, one person per entity (`FullStrengthPersons`, the cap),
  persons above it by `Reinforce`. Consequence: lane D's automatic reinforcement of understrength infantry and its three base pins stay
  (MEASURED in a scratchpad probe of `f454bec`: with (b) in `ReinforceToCaps` and lane D's `CapsRosterTests` of `0d2db74`, 5 of 27 cases
  fail, all of them pins DA5 itself moves, and the three `ReinforceToCaps` tests pass; with (b) and this lane's re-based tests, exactly
  those three re-based tests fail). The fill fraction of a soldier formation is then persons ÷ cap, i.e. its entities ÷ cap.
- Data either option touches: none (no cap, crew or personnel value is read differently).

---

## 2. What one entity stands for: the share and the remainder rule (implemented)

An entity of a formation of P persons in n entities stands for the **share s = P ÷ n**, a rational number (q = ⌊P ÷ n⌋ persons and
r = P mod n of them one more, on average). The battle keeps no persons per entity. **Persons become integers once, at the
battle's end, in lane W's collapse:** the formation's P persons are apportioned over its entities' end categories (fit, wounded,
routed, dead, surrendered) by the largest-remainder method, floor(P × n_c ÷ n) each and the leftover persons one each to the
largest remainders, ties to the lowest category code (`BattleCollapse.Apportion`, unchanged). Then: dead → the killed sink;
wounded → the formation's wounded account (M7.14's); captured → the captor's custody; routed → back to the colours on the side that
held the field, else the share (MoraleMax − end) ÷ MoraleMax deserts (lane W's PROPOSAL, unchanged); fit and returned stay.

- **Law 1 (MEASURED).** Every casualty row sums to the formation's persons exactly and each category is within one person of its
  quota P × n_c ÷ n, on battles with remainders everywhere (1,013 against 997 heavy infantry, three seeds); the sandbox ledger is
  conserved; at P = 10⁹ per side the collapse moves exactly 2 × 10⁷ persons per entity of every category (Int128 arithmetic).
  *Verification round 1 (CB-R1-07): "within one person of its quota" holds for any rounding, so this lane's test did not pin the
  largest-remainder method (only lane W's `CollapseTests` did). It now compares every category with an independent
  largest-remainder oracle; in each of the three seeds the 1,013-person formation's one leftover person goes to Dead (remainder 34
  of 50 against 16), not to Fit (MEASURED).*
- **Why once, at the collapse (READING CB-3).** **(a) APPLIED:** the share is rational until the collapse. **(b)** integer persons
  per entity slot (the first r slots q + 1, the rest q), mapped back entity by entity. Consequence of (b): it is exact too, but it
  biases by slot wherever entities do not die at random: the front slots meet the enemy first (INFERRED from the slot layout, not
  measured). DERIVED example: P = 75, n = 50, so slots 0–24 stand for 2 persons and 25–49 for 1; if the ten front entities 0–9 die,
  (b) kills 20 persons where the share kills 15. (a) needs no per-entity
  state, so DR23's "no persistent individual table" holds trivially. (b) is what an entity with hit points proportional to its
  persons needs (§3, option E1), and then the +1 slots must be spread (slot k takes one more iff ⌊(k + 1) r ÷ n⌋ − ⌊k r ÷ n⌋ = 1),
  not front-loaded.
- **What maps back:** only the end categories; nothing per entity survives the battle (DR23; lane W's `NoVeteranTable_…` pin).

---

## 3. The entity's hit points and output (PROPOSAL, not built)

### 3.1 The gap today (MEASURED)

Both resolvers fight an entity as one person whatever its share. A force of 1,000 against 1,000 heavy infantry fights as 50
against 50 entities, the same battle as 50 against 50 (equal result hashes, `ForceCreation_LargeForce_…_DA5`); so does 10⁹
against 10⁹ (`ABillionPersonsPerSide_…`). Only the collapse sees the persons. Until §3.2 is built, **a formation of more persons
than its cap fights no harder than one of exactly its cap**, so numbers above the cap do not count in battle. The sandbox shows
it: before D-082 a 3,000-person force was split into 60 formations of 50 and fought as 3,000 entities (on a field large enough to
hold them); now it is one formation of 50 entities. This is what D-082 §3 calls "undesigned … required design for M7.6".

### 3.2 Options for what one entity is in combat

| Option | Hit points | Output (melee, missile, domain weapon) | Death and casualties | Consequences |
|---|---|---|---|---|
| **E1 Proportional block (RECOMMENDED)** | the persons it stands for (integers per slot, CB-3 (b), spread) × the profile's per-person MaxHp | proportional to its LIVING persons a = ⌈hp ÷ MaxHp⌉: a hit's damage × a (hit chance unchanged), a volley of a shots resolved as one expected value plus one counter-RNG draw for the fraction | a person dies when the block's hit points fall a whole MaxHp; the block dies with its last person; dead, wounded and living persons per block feed the collapse | numbers count in battle; a damaged block hits less (Lanchester-like attrition, as the AutoResolver's aggregate model already is); both resolvers, the parity battery and every golden of a battle with P > cap move; a wound/death converter is needed (the AutoResolver's `WoundBufferPermille`, `KillEfficiencyPermille` shape, fitted at s = 1) |
| E2 Weighted exchange | per person (MaxHp) | a hit from a to b deals damage × s_a ÷ s_b | binary: an entity's death kills its whole share at once | equal to E1 in the mean ratio, but lumpy: a block of 60 dies all at once; wounds and partial losses disappear |
| E3 Representative sample (today) | per person | per person | binary; the share applied at the collapse | numbers above the cap do not count (§3.1); DR25's "50,000 troops difficult" would mean nothing on the battlefield |

**Why E1 (PROPOSAL).** It is the only option in which a block's strength follows the persons in it, before and during the fight:
DR25 ("ordinary military scale should be governed by the economy") then reaches the battlefield, while the caps still bound the
entities, the geometry and the cost (DR25: "Hard caps may protect performance"). At s = 1 (P ≤ cap) E1 is today's engine exactly,
so every pre-D-082 battle, golden and battery case would be unchanged and only battles with P > cap would move (INFERRED by
construction; to be pinned by the goldens when E1 is built).

### 3.3 Space and formation geometry under E1 (PROPOSAL)

A block occupies one entity's disc and slot (the battle's geometry is in entities: slots, frontage, separation, line of sight). A
formation of 3,000 heavy infantry is then 50 blocks of 60 persons in a 10 × 5 slot grid: the same frontage as 50 men, much denser.
Two choices for M7.6, neither built: keep the slot geometry (a block is a file of s men; frontage is a property of the entity
count, so a doubled formation is twice as deep, not twice as wide) or widen the slot spacing with s (frontage grows with persons;
the pathfinder and separation see larger discs). The first keeps the engine's spatial costs unchanged; the second is closer to
how a larger formation occupies ground. Either is a TacticalRules value, not data.

### 3.4 The AutoResolver's representation (PROPOSAL)

The AutoResolver (lane P's model 2) is already an aggregate model: alive counts, damage pools, `KillEfficiencyPermille`,
`WoundBufferPermille`, frontage limits per alive entity (`MaxAttackersPerTargetPermille`, `RangedPerExposedPermille` on the front
rank `min(alive, FormationColumns)`). Under E1 its counts become **persons** (alive persons, persons killed, persons surrendered;
casualties = damage ÷ the per-person MaxHp), while its **frontage limits stay in entities** (the geometry is entities in both
resolvers). Its coefficients are re-fitted on the lane P tuning battery with E1 in the TacticalResolver ("re-fit, never
hand-tune"; D-079 D11, Q4), and its `FormationResult` reports persons beside entities, so the hand-off needs no apportionment of
entity counts. Until then, the sandbox maps the AutoResolver's entity categories to persons by the largest remainder
(`SandboxSession.FromAuto` and the collapse; MEASURED exact).

---

## 4. The entity's morale weight (lanes MOR and MOR2)

An entity carries no morale of its own: in battle morale is per formation; on the campaign it is per army (lane E's stock). So
the question is whether any morale term counts entities where it should count persons.

| Term | What it counts | Under DA5 |
|---|---|---|
| Casualty-driven loss (both resolvers): ⌊k × F × 100 ÷ (start × (100 + L))⌋ | entities lost k of the start n | **unchanged, exact**: k entities of n stand for k × s of n × s persons, the same fraction (DERIVED); under E1 it becomes persons lost of P, equal to today's at s = 1 |
| Lane MOR's "fight it won" rise, ⌊c × B ÷ (B + W)⌋ (coefficient 0 by default) | the two formations' sizes | **changed (implemented):** both resolvers pass PERSONS (`EntityShare.PersonsOf`), because a broken formation of 5,000 persons in 50 entities would otherwise weigh like 50 persons. MEASURED, c = 200 (TEST INPUT), lane MOR's scenario: persons one per entity → 33 (lane MOR's number); the beaten 10 entities standing for 40 persons → 88; the winner's 50 standing for 5,000 → 0, no row; the battle otherwise identical, in both resolvers. *Verification round 1 (CB-R1-05): the 88 pairing is a resolver-level TEST INPUT the one rule never produces (40 heavy infantry fight as 40 entities; persons exceed entities only above the cap), accepted because the resolvers check P ≥ n, not n = `EntitiesOf`(class, P) (§9). Pairings the rule produces: 33 (within the cap), 28 (the winner's 50 entities standing for 60 persons; entity counts would give 33) and 0 (5,000), MEASURED in both resolvers; and the same 33, 28 and 0 in War Sandbox battles fielded by the rule, through lane TR's deployment, in both resolvers (the persons reach the battle: before round 1 no test followed them there)* |
| D-083 DB1's entry cost: in battle (`EntryMoraleCost`, default 0) and in the settlement (E = 50,000 per army whose formation entered) | per formation, once | **unchanged**: entering is a formation's act, whatever its entities |
| DR29's decrease in the settlement: ⌊max(0, (start − entry) − end − restore) × 1,000,000 ÷ MoraleMax⌋ | the formation's battle-scale morale | **unchanged** |
| DA1's rise in the settlement: ⌊R × B ÷ (B + W)⌋ | persons of the beaten and winning sides at the start (lane MOR's hand-off) | **unchanged**: already persons |
| An army of several formations (lane MOR §4.5, MOR2 §11.6, PROPOSAL for M7.15): Δ_army = Σ persons_f × (rise_f − decrease_f − entry_f) ÷ persons_army | persons | **unchanged**: weights are persons; a formation fighting as many entities weighs by its persons |
| Lane W's desertion of the routed: share (MoraleMax − end) ÷ MoraleMax | routed persons | **unchanged** |

**So:** nothing in the settlement changes when one formation fights as many entities; in battle one term changed (the local-win
sizes, now persons) and the casualty-driven loss is already exact for equal shares.

---

## 5. Consequences of DA5 (lane D's §2.2), with every data row cited (none changed)

Rows of `Sim.Data/content/unit-families.json` (blob at this branch's base `0d2db74`; the file last changed in `32861f6`):

- **CAP-1 dissolves.** 306 `musketeers` (:81), 307 `line_infantry` (:82), 308 `mechanized_infantry` (:83) have personnel 60 against
  the heavy-infantry cap of 50. Under DA5 each fights as 50 entities standing for 1.2 persons: no retune. Lane D's PROPOSAL
  "retune 306–308 to 50" (caps proposal §3) is superseded; its alternative "(B) a lighter classing" is moot.
- **CAP-2 dissolves.** 204 `pike_and_shot` (:64), personnel 60, if family 2 `spear_anti_cavalry` (:53) is classed heavy: 50
  entities of 1.2 persons. The other spear rows (201 :61 at 20, 202 :62 and 203 :63 at 40) are within 50 either way.
- **No soldier row needs a retune.** Every soldier-class row is within reach: the archers-and-lighter class rows at the cap are
  406 `arquebusiers` (:101), 407 `sharpshooters` (:102) and 408 `machine_gun_teams` (:103), personnel 60 = cap 60; family 1
  `light_infantry` 102–105 (:46–:49) at 20–40; heavy infantry 301 (:75), 309 (:76), 302 (:77) at 20, 303–305 (:78–:80) at 40.
- **CAP-3 unchanged** (READING CB-1 (a)): machine classes still need the crew-per-machine TUNE rule of M7.6; the 30 rows lane D
  listed (caps proposal §3, tanks 605 :134 to strike aviation 2303 :332) are unchanged, and none of them is affected by DA5.
- **CAP-4 narrows.** The sniper identity lane D proposed (category A, a branch of family 4, NOT created) was to carry "personnel ≤ 2"
  to fit the cap; under DA5 a sniper formation of more persons fights as 2 entities, so DR33's "independent small specialist
  formations" rests on the identity's personnel band (data, TUNE) and its cost, not on the cap. 407 `sharpshooters` (:102) remains a
  line formation (archers-and-lighter class). The mortar identity keeps the crewed tube (one entity per tube, §1).
- **CAP-5 unchanged in rule, and a measured performance limit.** Uncapped families fight one entity per person (lane N's treatment).
  The prototype engine cannot field an uncapped formation of 1,000 or more persons with generated entity ids ("generated ids need
  Count < 1000", `TacticalBattle`), and a battle holds at most 65,536 entities. D10 allows a performance cap "only if measurement
  shows the need": the cost measurement of §6 shows the tactical cost growing with entities squared. **For the Director (§8):** no cap
  value is proposed here.
- **Formation size is limited by the economy** (DA5-R (a)): the roster refuses no soldier formation for size; the economy's levers
  ([economic-scale-proposal.md](economic-scale-proposal.md) §8) decide how many persons a polity can field.
- **The War Sandbox (DA4, S1 (a) confirmed):** a force is one formation (the fewest within the cap, counted in entities); the split at
  the persons cap and its layout are superseded ([war-sandbox-proposal.md](war-sandbox-proposal.md) §4, marked in place).
- **Lane D's `ReinforceToCaps`:** fills only formations whose cap bounds their persons (machine classes); soldier formations have no
  room to fill. *Verification round 1 (CB-R1-01): this is READING CB-4 (a) (§1), this lane's choice, not a consequence of DA5;
  alternative (b) fills soldier formations up to their full complement of entities and keeps lane D's three pins.*

---

## 6. Performance at large P (MEASURED)

`dotnet Proto.M7.Bench.dll entities 50,250,500,1000,2000 10` (Release, `nice -n 19`, a shared 4-CPU host: wall times are indicative
only; raw output `…/scratchpad/m7c/CB-work/bench/entities-bench.txt`). Heavy infantry on both sides, an open 150 × 100 field,
the knowledge-only AI on both sides (TacticalResolver, timed over its first 10 pulses or to its end) and the AutoResolver.
"Persons rule" is the superseded READING C1 (a): ⌈P ÷ 50⌉ formations of one person per entity.

| Persons per side | DA5 one rule: formations, entities | TacticalResolver ms per pulse | AutoResolver ms | Persons rule: formations, entities | TacticalResolver ms per pulse | AutoResolver ms |
|---:|---|---:|---:|---|---:|---:|
| 50 | 2, 100 | 73.3 (4 pulses, ended) | 74.4 | 2, 100 | 17.9 (4 pulses, ended) | 49.4 |
| 250 | 2, 100 | 20.8 (ended) | 45.1 | 10, 500 | 335.5 | 30.5 |
| 500 | 2, 100 | 17.8 (ended) | 13.2 | 20, 1,000 | 1,363.4 | 34.3 |
| 1,000 | 2, 100 | 18.5 (ended) | 9.5 | 40, 2,000 | 4,330.7 | 49.7 |
| 2,000 | 2, 100 | 22.8 (ended) | 8.1 | 80, 4,000 | 20,332.4 | 92.8 |

At 50 persons a side the two rules build the same battle (the AutoResolver result hash is `9D4379113A7D…` for both); the first
row's 73.3 against 17.9 ms per pulse is the same battle timed twice and shows the host's noise and warm-up, not a difference. Under
DA5 the AutoResolver's result hash is `9D4379113A7D…` at EVERY size: the same battle whatever the persons (§3.1). Under the
persons rule each doubling of the entities multiplies the TacticalResolver's cost per pulse by 3.2 to 4.7 (336 → 1,363 → 4,331 →
20,332 ms).

- Under DA5 the battle's cost depends on its entities, which the caps bound: Σ over formations of min(P_f, cap_f), whatever P. A
  side of 50,000 heavy infantry in 20 formations of 2,500 persons is 1,000 entities (50 per formation, each standing for 50
  persons).
- Under the persons rule the TacticalResolver's cost grows with the square of the persons (its detection, targeting and separation
  are O(n²) per step, `reports/D-tactical.md` §7), and 50,000 a side (100,000 entities) is past the engine's 65,536-entity limit.
- The AutoResolver is cheap at every size (aggregate per formation).
- The collapse costs O(categories) per formation in Int128 arithmetic at any P (10⁹ per side MEASURED).

---

## 7. The battle entity is not a new entity (charter §4.5)

The battle entity is the battle-transient part of an existing category-A formation (D-065 R8; O-10 (a); DR23), not a new entity:
**lifecycle** created when the battle starts, gone when it collapses; **owner** its formation; **controllable** no (its formation
owns orders); **authoritative state** battle-only (position, hit points, state), no persisted row and no schema change; **consumer**
the two resolvers; **map location** the battle grid only; **how the player sees it** as one figure or block of its formation;
**no counting twice**: its persons are its formation's persons, integerized once at the collapse.

---

## 8. For the Director (only what is genuinely undecided)

1. **E1 for M7.6** (§3): confirm that an entity standing for many persons should fight as a block of them (hit points and output
   proportional to its living persons), or choose E2 or E3. This is the design D-082 §3 names as required; it is delegated work
   (D10, M7.6), flagged because it decides whether DR25's numbers count on the battlefield.
2. **READING CB-1** (machine classes one machine per entity): confirm (a) or choose (b).
3. **CAP-5 performance** (§5, §6): D10 asks for a performance cap only if measurement shows the need. The measurement shows the
   tactical cost growing with the square of the entities and a hard engine limit at 1,000 entities per generated formation; whether
   uncapped families get a cap (a value only the Director can set) or aggregate like soldier classes is the Director's.

Readings CB-2 and CB-3 are applied and need no decision unless the Director prefers their alternatives. *Verification round 1:
so is READING CB-4 (§1, `ReinforceToCaps`).*

---

## 9. Not built, and where it lands

- E1 in both resolvers, its geometry choice (§3.3), the AutoResolver's re-fit in persons (§3.4) and the parity battery's
  re-measurement: M7.6 with M7.9/M7.11 (D-079 D11; Q4 OPEN).
- The campaign → battle hand-off that fields a campaign formation by `EntitiesOf` and passes its persons: M7.15 (today the War
  Sandbox's Campaign Encounter is the only path, and lane W's tests build battles by hand).
- The crew-per-machine values (CAP-3), the sniper and mortar identities (CAP-4): M7.6, an authorized packet.
- A performance cap for CAP-5 families: the Director (§8).
- *(Verification round 1, CB-R1-05.)* The battle boundary checks only that a formation's persons can stand behind its entities
  (P ≥ n, land formations only; `EntityShare.Validate`), not that n is the one rule's count for P: the resolvers accept, for example,
  10 heavy-infantry entities standing for 40 persons, which the rule never fields. The rule holds where formations are fielded (the
  roster, both War Sandbox paths). PROPOSAL for M7.15, both options open: (a) the hand-off computes n by `EntitiesOf` and the battle
  trusts it (as now); (b) the battle refuses n ≠ `EntitiesOf`(class, P) for soldier classes, which needs the cap rule inside the
  battle and would refuse resolver-level test inputs such as the 88 case of §4.
