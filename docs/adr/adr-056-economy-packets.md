# ADR-056 — The M7 economy packets: EC-1 metals, EC-2 mounts, EC-3 gunpowder

**Status: RATIFIED PER PACKET — three separate Director rulings (D-067 §4, 2026-10-28), each after the amendments
recorded in the amendment log at the end of this ADR:**
- **EC-1, metals and tools (§4): RATIFIED** — D-067 R16.
- **EC-2, mounts (horses) (§8): RATIFIED** — D-067 R17.
- **EC-3, gunpowder and explosives (§9): RATIFIED** — D-067 R18.

**Recorded with the rulings (D-067 R16–R18):**
- **Mostly dormant content.** Each packet is mostly dormant until its knowledge gates are reached in play (§10): in
  natural play no AI or city-state completes an EC-1 or EC-3 gate within the measured horizons (400 turns on seeds 1, 2,
  42 and 7; 700 turns on seed 42), and EC-2's `horse_domestication` is first completed at turn 614 on seed 42 (§10, §12).
  These horizons were measured on `0bf7626` and on the research integration base (§10, §12), not re-measured on the
  integrated M6 tree.
- **No research nodes are added.** The AI research-progression finding of §10 is unchanged and stays OPEN; it is not
  fixed here.
- **Known limitations, accepted as they stand:**
  - **Pre-packet snapshots (§5):** a SAVE (snapshot) written before the packets loads with NO stock row for a packet good
    at its founding settlements, so those settlements can never hold the packet's goods; colonies founded after the load
    get rows. Recorded sessions are not affected: they replay with the packets off (§5).
  - **Labour dilution (§5, §6):** crafting labour splits equally across available recipes, so a packet recipe dilutes
    the base crafts once its knowledge is met (`docs/queue.md`).

Three separately reviewed packets (D-065 R2), each behind its own content switch in `goods.json` `packets[]`, each
shipped ON. Every packet's switch-off twin is the pre-packet world byte for byte. Approval is per packet; nothing here
combines them. Not in scope: fuels, engines, aluminium, electronics, composites
(EC-4a is provisionally M10, EC-5 later), military recruitment (the units track), and any new research.json node or
entity (TC-1).

Sources: `docs/m7-plan/unit-catalogue-and-progression.md` §1 item 6 and §13 rows EC-1..EC-3;
`docs/m7-plan/building-and-infrastructure-charter.md` §5.8, §5.9, §5.11, §12.1 (B2); `docs/d065-m7-charter-rulings.md` R2.

## 1. The switch (shared; introduced with EC-1)

- `goods.json` gains `packets: [{ id, enabled }]`. A good or a recipe may name a `packet`. A good or recipe of a
  DISABLED packet is removed at load (`GoodsConfigLoader.FilterAndValidate`) and the remainder is validated exactly as a
  hand-written pre-packet file would be — a recipe of an enabled packet that needs a disabled packet's good fails at load,
  by name. An undeclared packet id is refused.
- `GoodsConfig.WithoutPacket(id)` is the same filter on a loaded config — the attribution twins (`TestConfigs.PreEc1`,
  `PreEc2`, `PreEc3`, `PreEconomyTrack`) are built with it.
- `GoodsConfig.DisabledGoodNames` lets `sim.json` name a disabled packet's goods (`discovery.concealableGoods`): the twin
  loads the same `sim.json`.
- Packet goods take ids after the fourteen base goods (15+), so every base good keeps its id, its roster position, its
  founding deposit slot (`300 + id`, a stateless hash — a new good never perturbs an existing good's roll), its research
  quantity index (`stock_<good>`), and its row order in every table. A packet adds rows; it changes no existing row.

## 2. Concealment pools (shared; introduced with EC-1)

The D-062 rule k = floor(n × s + u) is now applied per POOL: the base pool (goods with no packet: copper-ore, tin-ore)
with exactly the old offset u, and one pool per packet with an offset salted by the packet id (`PoolFraction`, FNV-1a of
the id — never `GetHashCode`). A packet's ores therefore never change how many, or which, base ores are concealed. Pinned:
`EconomyMetalsTests.Founding_BaseDepositsAndTheBaseConcealmentPool_AreUntouchedByThePacket` (seeds 1, 2, 42, 7).

## 3. Knowledge gates without new research entities (shared; introduced with EC-1)

TC-1 adds no node and no entity. A recipe, or a good, carries a goods.json `knowledge` expression over EXISTING node ids
(the ADR-033 D4 dialect: AND/OR, no NOT), parsed by `ResearchContentLoader.ParseRequirement` and evaluated by
`ResearchQuery.RequirementMet` (`State.EconomyKnowledge`). The canonical four-file load refuses an expression naming no
node (`SimConfigLoader.ValidateProjectsAgainstContent`).

- A recipe's `knowledge` joins its research entity in `CraftingQuery.IsKnown` — the one recipe predicate, so production,
  the labour surface, the action surface, construction obtainability and the AI read the same answer.
- A deposit good's `knowledge` gates WORKING the deposit (`EconomyKnowledge.IsWorkable`): without it the extraction
  sector puts no labour there (ProductionSystem joins it to the hidden mask for the split), the labour surface does not
  list it, construction does not count it obtainable, and the AI's prospect key (`AiDiscoveryPolicy.KnownEligible`) does
  not count it. The row exists and is shown — the ore was always there; the technique was not.
- **Met only by knowledge.** Unlike a base recipe's entity (R1: "everything is known" where nothing gates), a packet gate
  is not met with no research content or no knowledge mask. Packet content postdates knowledge gating and has no ungated
  past to preserve. Consequence: hand-built test worlds with no control relation run the base economy exactly
  (`PacketGates_AreMetOnlyByKnowledge_AWorldWithNoControllerRunsTheBaseEconomyExactly`).

## 4. EC-1 — metals

**Ratification: EC-1 RATIFIED by D-067 R16 (2026-10-28), after the amendments logged at the end of this ADR. A
separate ruling from EC-2 (R17) and EC-3 (R18) (D-065 R2).**

All numbers TUNE (chosen, not derived; not calibrated).

| Good | id | category | bulkPerUnit | deposit | gate (working) |
|---|---|---|---|---|---|
| iron-ore | 15 | raw | 8.0 (as copper/tin ore) | elevation, spread 0.7 (common, uneven) | `iron_bloomery` |
| coal | 16 | raw | 7.0 | elevation, spread 0.9 (coalfields are patchy) | `coke` |
| iron | 17 | processed | 2.0 (as bronze) | — | — |
| steel | 18 | processed | 2.0 | — | — |

Recipes (inputs and labour PER EXECUTION, the T3.3 declaration; all also `artisan_share > 0.05`):

| Recipe | gate | inputs | output | labour (person-years) |
|---|---|---|---|---|
| iron-smelting (bloomery) | `iron_bloomery` (A4) | 3 iron-ore, 4 timber (charcoal) | 1 iron | 0.4 |
| coke-smelting (blast furnace) | `coke` (A7) | 3 iron-ore, 2 coal | 2 iron | 0.3 |
| steel-cementation (carburized) | `steel_carburized` (A4) | 2 iron, 2 timber | 1 steel | 0.5 |
| steel-converting (Bessemer) | `bessemer` (A8; `open_hearth` requires it) | 5 iron, 2 coal | 4 steel | 0.2 |
| iron-toolmaking | `iron_farm_tools` (A4) | 1 iron, 1 timber | 2 tools | 0.2 |

- Coal's working gate is `coke`, its first consumer; iron-ore's is `iron_bloomery`. No labour is spent on an ore nothing
  can use.
- Iron toolmaking is a SUBSTITUTION, not a better tool: the same `tools` good and the same 2 tools per execution as bronze
  toolmaking (historically iron's advantage was availability, not edge). No yield coefficient changes (law 2).
- Transport: `bulkPerUnit` above, read by the trade deadband (D-034) like every good. Price and trade participation are
  automatic (every roster good is priced per settlement and arbitraged independently per good, so a packet good cannot
  perturb another good's trade).
- Consumption: intermediate demand is the recipes above (iron-ore and coal by smelting; iron by steel and tools). The
  final consumer of iron and steel is military recruitment (the units track, M7.3), not built here; until it exists, steel
  accumulates and its price falls toward the band floor — measured, not hidden.
- Unblocks (charter §13 EC-1): the material side of identities 202–204, 303–305, 307–308, 103, 105, 407–408, 506,
  602–605, 803, 805–806, 904–906 and families 14 and 17–19 (iron/steel requirements; keys 13 and 15, listed in the
  charter's EC-1 row, were withdrawn by D-065 R9 and stay unallocated); `building.blast_furnace`,
  `building.steelworks`, `infra.railway`, `building.skyscraper` and the smithy's iron mode (charter §5.8; buildings are
  not built here). B2: an extraction improvement would attach to the deposit row (none is built here).

## 5. Shared plumbing (introduced with EC-1)

- **Replay of earlier records.** A packet adds rows from turn 0, so a session recorded before it replays only without it.
  The session manifest gains an additive v2 key `economyPackets` (written only when a packet is enabled); a manifest
  without it — every session recorded before the packets — is re-founded and replayed with every packet off, by
  `sim inspect` and `UiSession.Resume` alike (`SessionManifest.RecordedWithoutEconomy`,
  `EconomyKnowledge.WithoutEconomy`). `sim run|replay|inspect --without-economy` is the explicit switch (the
  `--without-discovery` precedent), applied at founding too. No serialized table changes: the packets add no table.
  (When this section was written, on bare `0bf7626`, that meant the schema stayed v37; the shipped schema is v39 —
  ADR-054 v38, ADR-055 v39, `CanonicalSchema.Version`; §13.)
- **The pre-packet projection** (`TestUtil/EconomyProjection`): removes the packets' rows (stocks, deposits, prices, price
  terms, trade flows, holds, ledger rows) and ASSERTS each was inert — never made, worked, consumed, priced away from 1.0
  or traded — so a packet that acted can never be projected away. `SchemaProjection` applies it first, so every
  pre-packet layout pin is still reached; every historical `TestConfigs.Pre*` twin also switches the packets off
  (`PreEconomyTrack`), since every earlier state predates them.
- **A row of a good the config does not hold** (a world founded with a packet on, stepped with it off — a twin or a
  mismatched save) is no sector's: production never works a deposit of an unknown good (`ProductionSystem.InSector`).
- **Known limits, not fixed here:**
  - **Labour split** (`docs/queue.md`): crafting labour splits equally across available recipes, and extraction labour
    follows abundance whatever the demand — a packet recipe dilutes the base crafts once known.
  - **Known limitation — pre-packet snapshots (accepted with the rulings, D-067 R16–R18):** a SAVE (snapshot) written
    before the packets loads with no stock row for a packet good at its founding settlements, so those settlements cannot
    hold the packet's goods (colonies founded after the load get rows); sessions are covered above. This item has no
    `docs/queue.md` entry; it is recorded here and in D-067 §4.

## 6. Measured (EC-1)

Harness: `Sim.Tests/Kernel/EconomyPacketMeasurement` (skipped; the `sim run --founded --ai-empires 2` loop with AI orders
before every step), each seed against its switch-off twin (`WithoutPacket("ec1")`).

**Natural play, 400 turns (to ~750 BCE), seeds 1, 2, 42, 7:** no AI and no city-state completes `iron_bloomery`, `coke`,
`steel_carburized`, `iron_farm_tools` or `bessemer`. Population, harvest, food eaten, grain held, starvation, trade units
and every base good's output and price are IDENTICAL to the twin (0.00 % on every metric); the hashes differ only by the
inert packet rows. Seed 42 continued to 700 turns (~650 CE): still no packet gate; `horse_domestication` (A3) first at
turn 614. **Finding for the Director:** in the current research pacing and AI goals, EC-1 has NO effect on a normal game
within the first 700 turns — the material side of iron/steel identities is unreachable until the A4 nodes are. What would
change it: faster AI research pacing into A4 (the open research-cost ruling), or AI research goals that value the nodes
which unlock the AI's own material needs (the ADR-052/053 goal machinery). Not implemented here.

**Granted knowledge (the EC-1 gate closures completed for every holder at founding, in BOTH arms), 150 turns:**

| seed | pop end (on vs twin) | harvest | iron made | steel made (held) | tools made | cloth | pottery | copper-ore |
|---|---|---|---|---|---|---|---|---|
| 1 | 77,627 vs 77,749 (−0.16 %) | −0.17 % | 16,957 | 8,450 (8,450) | 7,767 vs 8,310 | −4.8 % | −4.8 % | −8.1 % |
| 2 | 73,890 vs 73,851 (+0.05 %) | +0.03 % | 8,467 | 4,230 (4,230) | 3,592 vs 3,758 | −3.3 % | −3.3 % | −4.3 % |
| 42 | 67,245 vs 67,369 (−0.18 %) | +0.12 % | 7,385 | 3,754 (3,754) | 4,924 vs 5,120 | −3.1 % | −3.1 % | −3.8 % |
| 7 | 71,139 vs 71,161 (−0.03 %) | −0.03 % | 9,108 | 4,552 (4,552) | 5,467 vs 5,470 | −3.6 % | −3.6 % | −4.7 % |

No starvation in either arm; trade 0 in both (the trade node is not in the granted closure). Population and food stay
within 0.2 % of the twin. The costs are the dilution noted in §5: base crafts lose 3–5 % of their labour to the packet's
recipes, and extraction moves to the ores (stone −4..−8 %, copper −4..−8 %). Steel has no consumer until recruitment (its
price falls toward the band floor, 0.10–0.35); coal accumulates where no ore lies beside it.

## 7. Moved pins (EC-1) — all CONTENT LAYOUT ALONE

> **Historical — superseded (2026-10-28, D-067).** The NEW values in this section were measured on bare `0bf7626` and
> never shipped. They are kept as the record of the measurement. The current pin values, and every move since, are in
> `docs/m6-plan/golden-attribution-register.md` (§1 and §2 rows 13–14, with its dated notes).

Each re-pinned value's pre-packet projection (`EconomyProjection.HashWithout`, rows asserted inert) is the OLD pin byte for
byte, asserted beside the pin:

| pin | OLD | NEW (EC-1) |
|---|---|---|
| `SnapshotTests.FoundedGoldenHash` (+ ci.yml `FOUNDED_GOLDEN`) | 8abc32b0d85ef6fc… | 71a6147940d2be65… |
| `DrivenGoldenTests.Golden` | af5be6a12fd1391d… | 1ca2553085eaed78… |
| `FirstReignTests.PostR1Golden` | 6b9571cd1c2f50fe… | 7f58762da03f1dc4… |
| `AiFoundingAttributionTests.S1cTurn300` | 2a6a61d3dbd26dac… | 2f3c745cedc24b72… |

These NEW values were measured on bare 0bf7626 and are void on the integrated base (research and units move them first);
the final re-pin is done there. What carries over is the control: the pre-packet projection equals the base's own pin.

Every older pin (the attribution chains) is unmoved: each is reached through a historical twin, which now switches the
packets off, or through `SchemaProjection`, which strips the inert packet rows first. The toy golden does not move (no goods).

## 8. EC-2 — mounts (horses)

**Ratification: EC-2 RATIFIED by D-067 R17 (2026-10-28), after the amendments logged at the end of this ADR. A
separate ruling from EC-1 (R16) and EC-3 (R18) (D-065 R2).**

This section, §8, is the EC-2 section (§3 is the shared knowledge gate and §5 the shared plumbing). Code comments that
cited "ADR-056 §3" or "ADR-056 §5" for EC-2 and the herd were corrected to §8 in the same 2026-10-28 pass (comment-only
edits; see the amendment log, #12).

All numbers TUNE (chosen, not derived; not calibrated). `goods.json` packet `ec2`.

| Good | id | category | bulkPerUnit | gate (capture and breeding) |
|---|---|---|---|---|
| horses | 19 | **mount** (new category: not food, not extracted, not crafted) | 4.0 (livestock's: a driven animal) | `horse_domestication` (A3) |

**A herd, not a deposit and not food** (`GoodEntry.Herd`, `ProductionSystem.Breed`). No basket names horses; no recipe may
consume a herd good (the loader refuses one: the herd row's `ConsumeRemainder` banks its fodder). Per settlement, once its
knowledge meets the gate:
- herders h = the herding sector's workers (the same who tend the flocks — no labour is taken from livestock or fish);
- pasture P = the known abundance of the `pastureGood` deposit (livestock's moisture-channel row);
- capacity K = h × `headPerHerder` (2.0 [1–5]) × P; capture c = h × `capturePerHerderPerYear` (0.2 [0.05–0.5]) × P;
- the herd follows dH/dt = (c + r·H)(1 − H/K) below K, r = `breedingRatePerYear` (0.12 [0.08–0.2]), and loses its excess
  at r above K; integrated in CLOSED FORM over dtYears (`HerdAfter`), so it is exact for any turn length and never
  overshoots K;
- fodder: `fodderPerHeadPerYear` (0.2 [0.1–0.5] grain units, a winter supplement) × the EXACT head-years of the turn
  (`HeadYears`, the closed-form ∫H dt), a Ledger sink on grain (reason `Fodder`, 23 — renumbered from 20 at the M7 merge, ADR-055 owns 19–21), clamped to the store; a shortfall
  scales K and c by the fed share and the trajectory is recomputed;
- the net change enters as whole units through `Produced` (8) or `HerdLosses` (22), the sub-unit rest banked.
Without the knowledge a held herd is inert (held and traded, never bred or fed). The Observer's grain account closes through
`Fodder`, a good account through `HerdLosses`. No serialized table changes (the packets add no table; the schema version is the one the other layers ship — v39 at the M7 merge).

Tests (`EconomyMountsTests`, 9): the roster; the switch; loader refusals; the closed form composes exactly
(H(dt) = H(dt/2)∘H(dt/2), ∫ additive) at, below and above K and with no capacity; breeding needs its knowledge and
conserves the herd and the fodder exactly (integer pins); no fodder → no breeding and the unfed herd is lost at r; ten
1-year turns against one 10-year turn (±1 head, ±2 grain); the labour surface lists the herd exactly where it breeds;
30 canonical turns with the knowledge granted reconcile every account every turn (grain through Fodder).

**Measured (EC-2):** natural play — no AI or city-state completes `horse_domestication` before turn 400 on any of seeds 1, 2,
42, 7 (EC-1's natural run above; seed 42 first completes it at turn 614), so EC-2 moves nothing but inert rows there.
Granted arm (`horse_domestication` closure for every holder at founding, both arms; 150 turns):

| seed | pop end (on = twin) | harvest, eaten, grain held | horses held (bred) | fodder drawn | livestock |
|---|---|---|---|---|---|
| 1 | 16,994 = 16,994 | identical | 2,456 (3,049) | 439,651 | identical |
| 2 | 16,785 = 16,785 | identical | 2,456 (2,923) | 440,235 | identical |
| 42 | 15,854 = 15,854 | identical | 2,292 (2,759) | 410,735 | identical |
| 7 | 16,159 = 16,159 | identical | 2,354 (2,870) | 419,953 | identical |

The fodder comes out of grain that the granary bound would otherwise have spoiled or overflowed (stores sit at capacity in
this horizon), so population, harvest, food eaten and grain held are byte-identical to the twin; livestock is untouched.
Horses have no consumer until recruitment (the units track): their price falls to the band floor. A food-short world would
feel the fodder (it is drawn before people eat — Production precedes Consumption); not seen in these runs.

**Moved pins (EC-2), CONTENT LAYOUT ALONE, NOT re-pinned here** (re-pin on the integrated base). *Historical —
superseded (2026-10-28, D-067): the values in this paragraph were measured on bare `0bf7626` and never shipped; they are
absorbed by the combined pins of `docs/m6-plan/golden-attribution-register.md` row 14 (its §4 says so), and the current
values are in that register.* The founded golden
(f87e2cf06939fddb…), the driven golden (71fc678bb11807ec…), the first reign (c6b6963c63c6a3ba…) and the AI world (not
measured). Each one's pre-packet projection is asserted equal to its 0bf7626 pin (asserted FIRST in each pin test, and
passing): the horses row is inert where `horse_domestication` is unknown.

## 9. EC-3 — gunpowder and explosives

**Ratification: EC-3 RATIFIED by D-067 R18 (2026-10-28), after the amendments logged at the end of this ADR. A
separate ruling from EC-1 (R16) and EC-2 (R17) (D-065 R2).**

All numbers TUNE (chosen, not derived; not calibrated). `goods.json` packet `ec3`.

| Good | id | category | bulkPerUnit | deposit | gate (working) |
|---|---|---|---|---|---|
| saltpetre | 20 | raw | 3.0 | elevation, spread 0.9 | `saltpetre_refining` (A6) |
| sulphur | 21 | raw | 4.0 | elevation, spread 0.95 | `saltpetre_refining` |
| gunpowder | 22 | processed | 1.5 | — | — |
| explosives | 23 | processed | 1.5 | — | — |

| Recipe | gate | inputs (per execution) | output | labour |
|---|---|---|---|---|
| gunpowder-milling | `saltpetre_refining` (requires `gunpowder`) | 3 saltpetre, 0.5 sulphur, 2 timber (charcoal) | 4 gunpowder | 0.3 |
| explosives-making | `dynamite` (A8) | 2 saltpetre (nitric acid), 2 sulphur (sulphuric acid) | 1 explosives | 0.5 |

- `saltpetre_refining` is the prerequisite of every gunpowder weapon node (`cannon_early`, `matchlock`), so powder in quantity
  arrives with it; the `gunpowder` node alone (the alchemical discovery) works no deposit. Sulphur's first consumer is the
  same recipe, so both raw goods share its gate.
- Explosives are NOT made from gunpowder. Their glycerine and kieselguhr have no good and none is invented: the two acids
  carry the cost.
- Channels: worldgen has no aridity or volcanism channel (charter B3, deferred with owner), so both use elevation with wide
  spreads — cave nitre and volcanic sulphur stand-ins; the geography is uneven, the precondition for trade.
- Concealable, in their own pool (§2); EC-1's and the base pools are untouched (`Founding_TheEarlierPoolsAreUntouchedByThePacket`).
- No dependency on EC-1 or EC-2: the charcoal is timber. Each packet switches off alone (`SwitchOff_RemovesExactlyThePacket_AndEachPacketSwitchesAlone`).
- Consumers: the recipes (saltpetre and sulphur); gunpowder and explosives await recruitment and the arsenal/powder works
  (units track; charter §5.11). Unblocks (charter §13 EC-3): the material side of identities 104–105, 204, 306–307,
  406–408, 504–505, 604, 703, 804–806, 904–906, privateers and commerce raiders (17) and 14 (key 13, listed in the
  charter's EC-3 row, was withdrawn by D-065 R9 and stays unallocated).

Tests (`EconomyGunpowderTests`, 10): the roster and book; the switch (and each packet alone); milling only with its knowledge,
consuming per execution exactly; explosives need `dynamite` and take no gunpowder; saltpetre is not worked without its
knowledge (stone equals the twin's); the earlier pools are untouched (seeds 1, 2, 42, 7); 30 canonical turns with the
knowledge granted reconcile every account every turn and make all four goods (saltpetre and sulphur endowed through the
Ledger so the late recipe meets both inputs).

**Measured (EC-3):** natural play — no holder completes `saltpetre_refining` (A6) in 400 turns (EC-1's natural run), so EC-3
moves nothing but inert rows. Granted arm (`saltpetre_refining` and `dynamite` closures for every holder at founding, both
arms; 150 turns):

| seed | pop end (on vs twin) | harvest | gunpowder made | explosives made | cloth | pottery | stone |
|---|---|---|---|---|---|---|---|
| 1 | 75,274 vs 75,378 (−0.14 %) | −0.12 % | 45,416 | 94 | −3.8 % | −3.7 % | −6.9 % |
| 2 | 70,816 vs 70,780 (+0.05 %) | +0.02 % | 20,627 | 0 | −3.9 % | −3.9 % | −5.7 % |
| 42 | 65,569 vs 65,637 (−0.10 %) | −0.09 % | 28,140 | 23 | −4.2 % | −4.1 % | −5.9 % |
| 7 | 68,842 vs 68,803 (+0.06 %) | +0.04 % | 29,096 | 0 | −3.5 % | −3.5 % | −6.7 % |

No starvation in either arm; trade 0 (not granted). The cost is again the dilution of §5. Explosives are scarce: milling,
earlier in the recipe book, draws the shared saltpetre first (recipe order is input priority, T3.3), and saltpetre and
sulphur seldom lie at one settlement. Gunpowder accumulates (no consumer yet) and its price falls (0.21–0.38).

**Moved pins (EC-3), CONTENT LAYOUT ALONE, NOT re-pinned here.** *Historical — superseded (2026-10-28, D-067): the
values in this paragraph were measured on bare `0bf7626` and never shipped; the current values are in
`docs/m6-plan/golden-attribution-register.md` (row 14 and its dated notes).* Founded (5ae5e241be8a6c53…), driven (65d092db2782decf…),
first reign (4ea9df7139082bdc…); the AI world not measured. Every pre-packet projection equals its 0bf7626 pin (asserted
first in each pin test, passing).

## 10. Finding for the Director (not a fix)

In natural play (`--ai-empires 2`, the AI's own research goals), no AI and no city-state completes a single packet gate
within the measured horizons: by turn 400 (~750 BCE) on seeds 1, 2, 42 and 7 none of `horse_domestication`,
`iron_bloomery` or `saltpetre_refining`; on seed 42 continued to turn 700 (~650 CE) only `horse_domestication` (A3, turn 614),
still no `iron_bloomery` (A4). So EC-1 (and EC-3) change nothing in a normal game for at least the first 700 turns, and
EC-2 only from turn ~600. What would change it — not implemented here: research pacing into A3/A4 (the pending
research-cost ruling), or AI research goals that value the nodes unlocking the AI's own material needs (the ADR-052/053
goal machinery), or both. Every effect measured above is from the granted-knowledge arm.

## 11. Not done here

The final re-pin of the live goldens and ci.yml `FOUNDED_GOLDEN`, the long attribution classes, and the four-seed
measurement on the integrated (research + units) base; military consumption of iron, steel, horses, gunpowder and
explosives (the units track); buildings (blast furnace, steelworks, stables, foundry, arsenal; charter §5.8–§5.11); a
price- or input-aware labour split (queue).

## 12. On the integration base (research r2: ADR-054 per-turn CP and average population, schema v38)

The three packets ported onto `int-base-r2` unchanged in substance. Twin composition: `PreEconomyTrack` touches only the
goods roster (the research switches stay as shipped); every research "pre" twin (`PrePerTurnCivics`, `PrePerTurnCivicsOnly`,
`PrePopulationAverageOnly`, the UI's `TestFog.PreAdr054`) also switches the packets off (R4); `SchemaProjection` strips the
inert packet rows before every projection, the v37 one included. The allocation CI leg and its in-process twin take
`--without-economy` beside `--without-tranche2`.

**Moved pins — CONTENT LAYOUT ALONE, MEASURED on the research+econ tree.** Each pin test asserts FIRST that the live world
with the packet rows removed (asserted inert) is research's pin byte for byte, then the new raw value:

| pin | research (control, `…PreEconomy`) | research + econ (NEW) |
|---|---|---|
| `SnapshotTests.FoundedGoldenHash`, ci.yml `FOUNDED_GOLDEN` | 1f9f3d4f8bfa1991… | bd8a343b6683e19a… |
| `DrivenGoldenTests.Golden` | f23e0dcd49b012d0… | d6862db037272aa3… |
| `FirstReignTests.PostR1Golden` | 389e05266085018b… | 58f017059925b16e… |
| `AiFoundingAttributionTests.S1cTurn300` | 02500b4afbddc2ef… | 56e8e8671880ab95… |

The values in §7–§9 were measured on bare 0bf7626 and are superseded by these.

**Natural play under the new research pacing** (seed 42, `--ai-empires 2`, 700 turns, all three packets against the
all-off twin): still only `horse_domestication` (turn 614); no `iron_bloomery`, `coke`, `steel_carburized`,
`iron_farm_tools`, `saltpetre_refining`, `bessemer` or `dynamite`. Population (3,006,250), harvest, food eaten, grain held,
starvation (29,522) and trade (309,507 units) are identical to the twin. From turn 614 the herds grow to 382,344 horses
(385,042 bred) and draw 13,708,162 grain of fodder — all of it grain the granary bound would otherwise have spoiled or
overflowed (stores at capacity), so no food figure moves. §10's finding stands on the new base: EC-1 and EC-3 do nothing in
a normal game for at least 700 turns.

## 13. The M7 merge (research + units + economy, schema v39)

The economy track sits on research (ADR-054, v38) and units (ADR-055, v39). What the merge changed:

- **Reason ids.** ADR-055 owns 19 `Recruitment`, 20 `FormationUpkeep`, 21 `Reequipment`; the herd's sinks are renumbered
  `HerdLosses` 22 and `Fodder` 23 (§8 updated). A reason id is serialized only in ledger rows, and no pinned world has a
  herd flow, so the renumbering moves no hash.
- **The Observer keeps both sets of terms.** `GrainAccount` carries `Military` (ADR-055) and `Fodder`; `GoodAccount` carries
  `Military` and `HerdLosses` (herd losses and fodder); each closes its identity through both. Telemetry writes `military`
  and `fodder` in the grain object only when non-zero, so a line of a world without either layer is byte-identical to
  before. (Since then the line's tag has moved: the writer is `telemetry/v5` — D-066 A15, ADR-059 §4,
  `TelemetryWriter.Schema` — so a line now differs from a pre-merge line in its `schema` tag; its other keys are as
  stated here.)
- **Planned goods.** unit-families.json lists the nine EC-1..EC-3 goods as `plannedGoods` under the names goods.json gives
  them. With a packet on, the loader resolves a recruitment input to the real good id; with it off at load (goods.json's
  `enabled`) the input is planned and the identity blocked, naming the good and its packet. A config whose packets are
  switched off AFTER loading (every twin, `--without-economy`, `UiSession.Resume` of a session without the
  `economyPackets` key) goes through `EconomyKnowledge.WithGoods`, which swaps the roster and re-resolves the bills against
  it (`UnitFamilyContent.ResolvedAgainst`: the loader's own rule, an identity untouched by the swap kept as the same
  instance), so a bill never names an id the roster lacks. Pinned both ways on a real identity
  (`EconomyRecruitmentTests`: `iron_infantry` blocked with EC-1 off; with it on, known and stocked, recruited and its iron
  sunk under `Recruitment` exactly). The units track's planned-good tests now run on the packets-off twin.
- **Twins compose both ways.** `PreEconomyTrack` touches only the goods roster (and the bills re-resolved against it): the
  military layer and the research switches stay as shipped. `PreMilitary` keeps the packets on. Every older twin
  (`PreM6`, `PreTranche2`, `PrePerTurnCivics*`, `PrePopulationAverageOnly`, `PreD054Sim`, … and the UI's
  `TestFog.PreAdr054`) strips both. `SchemaProjection` removes the inert packet rows first (asserted inert), then the v39
  and v38 layers; `SchemaProjection.V38KeepingPackets` is the v38 form that keeps them (the research+economy pins).
  The UI's `TestFog.Off` and `TestTranche2.Off` keep the military layer as the units track left them (a session they found
  records schema v39, so `Resume` replays it with the military layer on; their founding follows the config).
- **CLI / UI.** `--without-economy` and the military switch for old sessions both apply: a session older than v39 is
  replayed without the military layer, and one whose manifest has no `economyPackets` key without the packets — an old
  session has neither, so both are off.

**Moved pins — MEASURED on the fully combined tree.** Each live golden has two controls, both exact: (a) packets off — the
live world with the packet rows removed (asserted inert) is the units pin, asserted FIRST in the pin test
(`…PreEconomy`); (b) military off — `TestConfigs.PreMilitary`'s world is the research+economy pin through
`SchemaProjection.V38KeepingPackets` and `WorldHash.ComputeHexAsSchema(·, 38)`, and the research pin through
`SchemaProjection.V38` (`MilitaryAttributionTests`, constants `V38Econ…`).

| pin | (a) packets off = units pin | (b) military off = research+econ pin (research pin via `V38`) | COMBINED (NEW) |
|---|---|---|---|
| `SnapshotTests.FoundedGoldenHash`, ci.yml `FOUNDED_GOLDEN` | bb825342455bdbd8… | bd8a343b6683e19a… (1f9f3d4f8bfa1991…) | d5ebd0b2c642ec5c… |
| `DrivenGoldenTests.Golden` | f320440fba3da9eb… | d6862db037272aa3… (f23e0dcd49b012d0…) | 55afd498a326ab2a… |
| `FirstReignTests.PostR1Golden` | df73d4f2b022107a… | 58f017059925b16e… (389e05266085018b…) | 064ed6b76374dea4… |
| `AiFoundingAttributionTests.S1cTurn300` | 3153b7e3214ecd21… | 56e8e8671880ab95… (02500b4afbddc2ef…) | ac4554bfdcd3d5b6… |

*AI world, since superseded (2026-10-28):* the COMBINED `S1cTurn300` value `ac4554bf…` above is the M7-merge value. The
M6 integration (ADR-058 KNOW and D-066 A18 combined) moved it to `5458cb3d…` (`S1cTurn300PreEconomy` `3153b7e3…` →
`a56313fc…`); `ac4554bf…` survives as `AiFoundingAttributionTests.PreA18Turn300`. The founded, driven and first-reign
values above are the shipped ones. See `docs/m6-plan/golden-attribution-register.md`, dated notes of 2026-10-28.

Control (a) was also measured the strong way, by stepping the packets-off twin (`TestConfigs.PreEconomyTrack`) itself: its
raw hash is the units pin in all four worlds, so the projection and the twin agree. The layout-only pins (toy,
`PreTranche2Turn300`, `PreM6FoundedGolden`, `PreM6AiTurn300`) do not move: every one of those worlds is under an older twin
(packets off) or has no goods content.

## Amendment log — 2026-10-28 (D-067)

Authority: `docs/d067-m6-exit-gate-director-rulings.md` §4 (R16–R18: EC-1, EC-2, EC-3 each RATIFIED "after the ADR-056
amendments") and §7. This ADR was uncommitted and not yet ratified, so stale FACTS were corrected in place; no design
decision is changed. Every in-place change is listed here. Line numbers marked "was" are those of the staged pre-amendment
text; evidence was read from the live tree on 2026-10-28. D-067 is cited by ruling and section, not by line: it is untracked and was being amended concurrently.

| # | Where | Old | New | Evidence |
|---|---|---|---|---|
| 1 | Status line (was :3–5) | "Status: PROPOSED — three separately reviewed packets (D-065 R2), each behind its own content switch in `goods.json` `packets[]`, each shipped ON." | Three separate rulings, EC-1 RATIFIED (R16), EC-2 RATIFIED (R17), EC-3 RATIFIED (R18), each after these amendments; then "Recorded with the rulings": mostly dormant content until the knowledge gates are reached (§10); no research nodes added and the §10 AI-progression finding unchanged and OPEN; "Known limitations" bullets for the pre-packet snapshot (§5) and labour dilution. The original sentence is kept below the bullets. | D-067 §4 (R16–R18 and "Recorded with R16–R18") |
| 2 | Head of §4, §8, §9 (was :51, :153, :204) | (no ratification line) | One "Ratification: EC-n RATIFIED by D-067 R16 / R17 / R18" line per packet | D-067 §4 (R16–R18) |
| 3 | §4 "Unblocks" (was :83) | "families 13–15, 17–19" | "families 14 and 17–19 (… keys 13 and 15 … withdrawn by D-065 R9 and stay unallocated)" | `docs/d065-m7-charter-rulings.md:52` (R9: "The proposed keys 13, 15 and 16 are withdrawn"); `Sim.Data/content/unit-families.json` has keys 14 (:228), 17 (:242), 18 (:256), 19 (:270) and no key 13, 15 or 16 |
| 4 | §5 replay bullet (was :94) | "No serialized table changes: the schema stays v37." | "No serialized table changes: the packets add no table. (When this section was written, on bare `0bf7626`, that meant the schema stayed v37; the shipped schema is v39 …)" | `Sim.Core/Kernel/CanonicalSchema.cs:231` (`Version = 39`); ADR-056 §8 (:175 was) and §13 |
| 5 | §5 "Known limits" (was :102–105) | One bullet attributing both limits to `docs/queue.md` | Two sub-bullets: the labour split (`docs/queue.md`), and "Known limitation — pre-packet snapshots (accepted with the rulings, D-067 R16–R18)", text unchanged, noting it has no `docs/queue.md` entry | `docs/queue.md:1720` (the labour-split entry; no entry for the snapshot limit); D-067 §4 ("Known limitation") |
| 6 | §7 (was :135) | (pins presented without a currency marker; §7 already said the NEW values were "void on the integrated base") | Block note: "Historical — superseded", with a pointer to the register. The values are NOT deleted. | `docs/m6-plan/golden-attribution-register.md:14–20` (§1), `:41–42` (rows 13–14), `:57–58` (§4: the EC-2 moves "absorbed by row 14") |
| 7 | §8 "Moved pins (EC-2)" (was :199) | (no currency marker) | Inline "Historical — superseded" note with the register pointer; values kept | as #6 |
| 8 | §9 "Moved pins (EC-3)" (was :254) | (no currency marker) | Inline "Historical — superseded" note with the register pointer; values kept | as #6 |
| 9 | §9 consumers / unblocks (was :231) | "privateers and commerce raiders (17), 13 (A8) and 14." | "privateers and commerce raiders (17) and 14 (key 13 … withdrawn by D-065 R9 …)." | as #3 |
| 10 | §13 "The Observer keeps both sets of terms" (was :312–313) | "… a line of a world without either layer is byte-identical to before." (no tag stated) | Same sentence, plus: the writer's tag is now `telemetry/v5` (D-066 A15, ADR-059 §4), so a line differs from a pre-merge line in its `schema` tag | `Sim.Core/Observability/TelemetryWriter.cs:35` (`Schema = "telemetry/v5"`); at `0bf7626` it was `telemetry/v4` (`git show HEAD:Sim.Core/Observability/TelemetryWriter.cs`, :35) |
| 11 | §13, after the combined-pin table (was before :347) | AI world COMBINED `ac4554bf…`, `S1cTurn300PreEconomy` `3153b7e3…`, presented as current | Note: since superseded by the M6 integration (ADR-058 KNOW + D-066 A18): `5458cb3d…` / `a56313fc…`; `ac4554bf…` survives as `PreA18Turn300`. Founded, driven and first-reign values are the shipped ones. | `Sim.Tests/Kernel/AiFoundingAttributionTests.cs:129, :139, :151`; `SnapshotTests.cs:1184`; `DrivenGoldenTests.cs:118`; `FirstReignTests.cs:58`; register `:64`, `:74` |
| 12 | Head of §8 (EC-2 citation) | (none in this ADR) | A sentence stating that §8 is the EC-2 section and that code comments citing "ADR-056 §3 (EC-2, mounts)" or "ADR-056 §5" for the herd mean §8 | `Sim.Core/State/Ids.cs:248` ("ADR-056 §3 (EC-2, mounts)"); `Sim.Core/State/LabourActivities.cs:303` and `Sim.Core/Systems/Production/ProductionSystem.cs:235, :447` ("ADR-056 §5 (EC-2 …)"); `ProductionSystem.cs:525, :543` ("ADR-056 §5", the herd's `HeadYears` / `HerdAfter`). The comments were then corrected to §8 in the same pass (comment-only, no code token changed): `Ids.cs:248`, `LabourActivities.cs:303`, `ProductionSystem.cs:235, :447, :525, :543`, `Sim.Core/Systems/GoodsConfig.cs:43, :271, :457`, `Sim.Tests/Systems/EconomyMountsTests.cs:15`; and the EC-3 test header `Sim.Tests/Systems/EconomyGunpowderTests.cs:13` ("§6" → "§9"). |

**Checked and left unchanged:** every "§" cross-reference inside this ADR (§2, §5, §8, §10 and the charter's §5.8–§5.11,
§12.1, §13) and its D-065 citation (R2, separate packets: `d065-m7-charter-rulings.md:17`). No incorrect EC-2 section
citation was found inside this ADR; the incorrect ones were the code comments named in #12, corrected in this pass. The §12 values remain the
research+economy control (b) values (register row 13) and are not superseded as controls.
