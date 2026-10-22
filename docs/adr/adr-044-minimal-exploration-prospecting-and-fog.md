# ADR-044 — MINIMAL EXPLORATION, PROSPECTING AND FOG (M6 Track EX)

**Status: ACCEPTED under D-062** (the Director's final M6 decisions of 2026-10-22, recorded by the coordinator as D-062;
binding implementation contract: the M6 slice contract, Track EX, items D1–D10). This ADR records the contract **as
built**, the choices the contract left open, the measurements behind the costs, the deviations, what is deferred to M7,
and the golden moves with their attribution.

- **Touches the kernel contract** (the constitution requires an ADR): `CanonicalSchema.Version` 34 → **35** with four
  appended sections (63–66, after Track CP's section 62 — merged: the stream order is 61, 62, 63 … 66); the readable set becomes {32, 33, 34, 35};
  `Snapshot.Load` gains the v35 load refusals; two new order kinds (14, 15) on the UNCHANGED wire format (IoVersion 1);
  one new system (SystemId 29) in `pipeline.json`; two new ledger reasons (17, 18). Kernel files changed:
  `CanonicalSchema.cs`, `Snapshot.cs`, `OrderLog.cs`, `OrderValidation.cs`, `ReplayProof.cs`, `ReplayGuard.cs` (the
  readable-schema list only). `TurnExecutor.cs` is unchanged (the same-step edge is declared through the existing
  `SystemRegistration.ReadsThisStepFrom`).
- **Supersedes, to the extent of these minimal actions** (D-062): D-040 B2, D-039 B1/B3 and m6-spec 1.13 ("no second
  exploration system / discovery only by scouts"). Movement and Action Points stay M7.

## 1. What exists now

| Layer | New state / contract | Owner | Switch (shipped ON) | Attribution control |
|---|---|---|---|---|
| Concealed deposits | none stored — a derived classification (`State.DepositConcealment`) | read by every deposit consumer | `sim.json discovery.enabled` | `TestConfigs.PreDiscovery` |
| Prospect (OrderKind 15) | `SettlementSurveyRow` (section 66) | DiscoverySystem | `discovery.enabled` | `TestConfigs.PreDiscovery` |
| Fog: known extent, sight, settlement memory | `KnownExtentRow` (63), `SettlementSightingRow` (65); visible set derived | DiscoverySystem; read through `PolityKnowledge.Of` | `sim.json fog.enabled` | `TestConfigs.PreDiscovery` |
| Explore (OrderKind 14) | `ExplorationRow` (64) | DiscoverySystem | `fog.enabled` | `TestConfigs.PreDiscovery` |
| Founding gate on the known extent | `FoundingVerdict.UnknownSite` | FoundingQuery (validator, UI, AI) | `fog.enabled` | `TestConfigs.PreDiscovery` |
| AI parity | `AiDiscoveryPolicy` (Explore and Prospect lanes) | AiOrders | both switches | `TestConfigs.PreDiscovery` |
| Actions menu, fog on the map | `ActionDomain.Discovery`, `DiscoveryBlock`, `FogRun` | Sim.Ui | both switches | — |

`TestConfigs.PreDiscovery` switches both sections off; by the R4 rule every older chained twin strips it too
(`PreIncrementalConstruction`, `PreReservations`, and through them `PreD054Sim`, `PreD053` and every twin built on it).

## 2. Denominator and eligibility (D1)

The denominator is the existing `DepositRow` per (settlement, good), written at founding (`WorldFounding.AddDepositsForSite`)
and at colonization. **Nothing about deposit generation changed** — quantities, placement and the founding roll are
byte-identical (pinned: the deposit table and the whole founded world hash are equal with concealment on and off).
A row is **eligible** when its abundance is > 0 and its good is in `discovery.concealableGoods` = stone, clay, copper-ore,
tin-ore (geological). Livestock, fish, timber, fiber and hides are surface-evident and never concealed. The share is
measured over eligible rows.

## 3. Classification (D2) — derived, never stored

For a settlement with n eligible rows: k = floor(n × s + u), s = `discovery.concealedShare` = 0.25, u ∈ [0, 1) =
SplitMix64(seed ⊕ `ConcealSalt` ⊕ settlement ≪ 32) — a salt distinct from every founding salt. The concealed rows are the
k lowest by the composite key (SplitMix64-keyed row hash, goodId) — an integer key with a stable integer tie-break
(`DepositConcealment.LowestByKey`, tie-dense test). Stateless: no RngRegistry stream is drawn, nothing is written.

**Measured realized share (turn 0, canonical 1024 px worlds):** seeds 1, 2, 42 and 7 each have 12 settlements × 4
eligible rows = 48, of which 12 are concealed: **0.250 on every seed** (n = 4 everywhere, so k = floor(1 + u) = 1).
Pinned in `[0.20, 0.30]` by `DiscoveryTests.RealizedConcealedShare_AtTurnZero_IsWithin20And30Percent`.

## 4. Effect and Prospect (D3, D4)

**One predicate.** `DepositConcealment.HiddenGoods / Abundance / KnownAbundance`: for the settlement's CURRENT controller
an undiscovered concealed row reads as abundance 0. Routed through it: `ProductionSystem.FromDeposits`,
`LabourActivities.GoodsOf`, `ConstructionQuery.Producible`, `InfoQuery` (good card, `Obtainable`, the deposit read), the
site assessment's `IfFoundedNext` (the roll a settlement founded next would get, classified under the id it would get —
nobody has surveyed a settlement that does not exist; the terrain-based range is still shown), and the resource
overlay's ore markers. Developer / Explain surfaces and `sim diff` read the raw table. A settlement no polity controls has
no surveyor (its concealed rows read 0); a settlement that changes hands is undiscovered for its new controller.

**Prospect = OrderKind 15.** TargetId = a settlement the issuer controls and has not surveyed; Amount = 0 (load-checked).
Cost `discovery.prospectTools` = 4 tools, drawn through `EmpireStockpile.Draw` (the settlement first, then nearest-first),
never below construction's set-asides, SUNK under **reason 17 `Prospecting`**; refused at delivery (nothing charged,
the state is the orderless step's — pinned) when unaffordable. Stamped t → `SettlementSurveyRow(P, S, t)` in t+1; the
deposit first produces in the step t+1 → t+2 (production reads PREV) — both pinned turn-exact. Once per polity per
settlement (a second prospect, same turn or later, is refused and charges nothing — pinned). Prospecting never creates,
moves or resizes a deposit.

**The technology discount.** `discovery.prospectDiscountRequires = "copper_smelting"` (key 64, "Copper smelting"):
recognising malachite and azurite *as ore* is the knowledge copper smelting embodies, and it is the earliest node in the
current content whose capability is the use of ore (copper_native works native metal found on the surface; mining_shaft
is depth, not recognition). With it completed the cost halves, integer division (4 → 2). Parsed against research.json at
load (`ValidateDiscoveryAgainstResearch`), evaluated by the existing knowledge evaluator.

## 5. Costs — the measurement

Measured on this tree, orderless canonical worlds (no player orders), seeds 1, 2, 42, 7, per settlement:

| seed | turn 0 grain (median, min–max) | turns 5–40 grain (median) | tools, turns 0–40 |
|---|---|---|---|
| 1 | 7 227 (2 833–8 335) | 521–606 | 0 everywhere |
| 2 | 7 282 (4 133–11 623) | 507–619 | 0 everywhere |
| 42 | 6 854 (3 705–11 759) | 515–668 | 0 everywhere |
| 7 | 5 549 (3 221–10 434) | 502–665 | 0 everywhere |

And a 12-AI world (every polity AI-commanded, `AiOrders` each turn) to turn 400 (year ≈ 3 200) on seeds 42 and 1:
**tools 0 and bronze 0 in every settlement at every 50th turn** — the tool chain (copper-ore + tin-ore → bronze →
toolmaking, research-gated, artisan share > 0.05) is not reached in that horizon.

- **`fog.exploreGrain` = 60** (unchanged from the contract's start value): about 1 % of a settlement's founding store
  and about a tenth of its steady store from turn 5 on, so every settlement can afford an expedition from turn 0. In the
  12-AI runs the AI explored 134 (seed 42) and 81 (seed 1) times in 400 turns.
- **`discovery.prospectTools` = 4** (the contract's start value, kept). **No positive tools cost can meet the
  "affordable within the first few dozen turns" target**: tools are 0 at founding on all four seeds and stay 0 for
  hundreds of turns, so Prospect opens only with toolmaking. Setting the cost to 0 would make information free, which the
  contract forbids. **Recorded for the Director (OPEN):** (a) accept Prospect as a toolmaking-era action (as built); (b)
  pay it in a good every settlement holds early (grain or stone — a content change of `DepositConcealment.ProspectGood`);
  (c) endow founding settlements with a few tools. The AI's Prospect lane is correct but idle until then
  (0 prospects in the 12-AI runs).

## 6. The fog (D5)

Per polity and lattice node (the traversal lattice's 16 km node, `PolityKnowledge.Grid`; 256 × 256 on the canonical map):

- **KNOWN (E1/E2/E3)** = the stored extent (`KnownExtentRow`, monotone — a bit is never cleared, pinned) ∪ the seed disc of
  radius `fog.seedRadiusNodes` = 3 (≈ 50 km, `catchment.hinterlandRadiusKm`) around every settlement the polity controls
  NOW ∪ its formations' sight discs ∪ explored discs. DiscoverySystem stores that union every step; `PolityKnowledge.Of`
  also applies the seed and sight at query time, so turn 0, a colony, a revolt-born polity and a migrated save know their
  seed before the first store.
- **VISIBLE (E4, derived, never stored)** = the node of every settlement the polity controls ∪ discs of radius
  `fog.formationSightNodes` = 1 around its formations. **REMEMBERED** = known ∧ ¬visible; **UNKNOWN** = ¬known.
- **Settlements (E5):** `SettlementSightingRow(P, S, turn)` is written or updated when S's node is VISIBLE to P, or an
  Explore's disc covers it. `KnowsSettlement` = own, visible, or sighted; a settlement founded in remembered land stays
  unknown until seen (pinned). Internals stay the controller's (`ReadsInternals`).
- **Units (E6):** a rival formation is shown only while its node is visible.
- **E7:** `PolityKnowledge.Of(world, polity, cfg)` is the one production entry (it now takes the config: its fog
  parameters). The UI's `PlayerKnowledge` is built from it by the session; the AI reads only it. Difficulty grants nothing;
  maps are never shared.
- **E8:** a Found Settlement order's site node must be KNOWN to the issuer — `FoundingVerdict.UnknownSite`, one arm of
  `FoundingQuery.Check`, which the delivery check (ColonizationSystem), the UI's placement mode and the AI share.

**Geometry.** Integer discs dx² + dy² ≤ r² from precomputed offset tables (radii 0–32), wrap-aware in x on a cylinder,
hard edges north and south (pinned at the seam). Extent stored as sparse 64-bit words, node n → word n ≫ 6, bit n & 63.

## 7. Explore (D6) and DiscoverySystem (D7)

**Explore = OrderKind 14.** TargetId = a settlement the issuer controls; Amount = 0. New radius = current
(`ExplorationRow`, default 3) + `fog.exploreStepNodes` = 3, capped at `fog.exploreMaxRadiusNodes` = 15 (refused at the
cap). Cost `fog.exploreGrain` grain via `EmpireStockpile.Draw` from S first, never below construction's set-asides, SUNK
under **reason 18 `Expedition`** (travel provisions; no person is removed). Stamped t → in t+1: the radius, the disc in the
extent, and the sightings of every settlement in the disc. Pinned: 3 → 6 → 9 → 12 → 15 then refused; 60 grain per
admitted expedition; extent monotone; a later stamp lands later.

**DiscoverySystem** (SystemId 29, `"discovery"`, pipeline slot right after colonization). Owns sections 63–66; applies
the orders in log order (one per (polity, kind, target) per turn — a later duplicate is refused), then the extent, then
the sightings; every table strictly ascending by key (asserted at the end of the step and by `Snapshot.Load`).
**Stock draws mirror road development's:** discovery is the seventh holder of the sanctioned GoodStocks share (sinks only,
recorded in `SystemCatalog`'s ownership paragraph), it is handed construction's NEXT `ConstructionHolds` read-only and
honours the same per-purse floors (`RoadDevelopmentQuery.SetAsideWithin`), and the read is a declared same-step edge
(`ReadsThisStepFrom = construction` whenever reservations are on) that the executor enforces. The Observer's grain
identity gains `− Expedition` and each good's identity `− Discovery` (reasons 17 + 18), so every account still reconciles.

**Migration v34 → v35.** A v34 stream stops after section 61; the four tables are left EMPTY; the first step re-derives
each polity's seed extent (pinned: after one step the stored extent is exactly the seed; explored radii are gone).
`SaveFiles.MigrationNote` names it; `SnapshotDiff.LayoutFor(34)` walks it.

## 8. The UI (D9)

The POLICY surface keeps its **Building** block (Build Granary etc., unchanged) and gains a separate headed **Actions**
block: per controlled settlement "Explore surroundings (radius r → r+3 nodes, cost 60 grain)" and "Prospect for hidden
deposits (cost 4 tools)", each with the predicate's blocker ("explored to the limit (15 nodes)", "already surveyed",
"not enough grain/tools (needs N)", "already ordered this turn"). New `ActionDomain.Discovery` (15; id = settlement × 2 +
0/1), `ActionHitKind.Explore/Prospect`, `ActionCommandKind.Explore/Prospect`, `UiSession.EmitExplore/EmitProspect` guarded
by `DiscoveryQuery.CheckExplore/CheckProspect` and the once-per-turn rule; two PlayabilityGate rows. The map draws UNKNOWN
nodes as blank parchment (`MapInk.FogUnknown`, opaque, over the terrain) and REMEMBERED nodes under a darker wash
(`MapInk.FogRemembered` at 38 %), as row runs (`WorldProjection.Fog`); VISIBLE land is untouched. After a prospect the
discovered deposit appears in the place card, the good card and the overlay's ore markers. The raw-read guards (the
source guard regex and `check-read-isolation.sh`) now also match the four discovery tables.

## 9. The AI (D8)

`AiDiscoveryPolicy`, called by `AiOrders` per AI polity after the founding policy:
- **Explore** — when the founding policy is due and has a source (`WantsSites`) but issued nothing (it found no known
  candidate site): the argmin of (explored radius, settlementId) over the settlements `CheckExplore` admits.
- **Prospect** — one per turn: the argmin of (−knownEligible, settlementId) over the settlements `CheckProspect` admits.
  **Deviation from the letter of D8:** "most eligible rows" is read through the controller's knowledge — the number of
  concealable goods it can SEE there (known abundance > 0). The raw eligible count includes the concealed rows it cannot
  see, which would leak them (the metamorphic test pins non-interference).
Both read only `PolityKnowledge`, own books and own stocks; both keys are integer composite keys (tie-dense test).
ci.yml's AI kind set gains 14 (the AI explores in the 600-turn run); 15 is not issued there (no tools — §5).

## 10. Recorded deviations

1. **Emergent colonization stays unchanged** (people, not a state): D-037 B1 founding is not gated by the known extent.
2. **No ghosts before M7:** formations cannot move before M7, so a last-known marker would equal the live position;
   a rival formation is shown only while visible, and no ghost row exists.
3. **Minimal fog:** visibility is the settlement nodes and formation discs only (no line of sight, no terrain, no roads,
   no trade-route knowledge); remembered land is drawn with no stale picture of what is there; the terrain under known
   land is the live terrain (static by ADR-008).
4. **The AI's prospect key reads visible eligible goods** (§9).
5. **Prospect is unaffordable until toolmaking** (§5) — recorded as an open Director choice.
6. **`PolityKnowledge.Of` takes the config.** Its fog parameters (seed and sight radii) are content; the stored extent
   and sightings remain its only stored inputs.

## 11. Deferred to M7 (and later)

Mobile scouts and Action Points (Explore from a settlement is the stand-in; its provisions sink is replaced by the mobile
unit); ghost (last-seen) formation rows; line of sight and terrain-dependent sight; sharing maps (diplomacy, M8); trade,
roads and claims gated by the known extent (no new gate in this slice beyond the seam's existing filters — the trade
partner, road-development route and claim readers are unchanged); sight from trade routes; the remap seam for any
deposit change; depletion.

## 12. Golden moves and attribution

Schema v35 (merged with Track CP, ADR-043: section 62 CivicsPoints, then this track's 63–66) and the shipped discovery
layer move every golden world: the discovery tables are POPULATED wherever DiscoverySystem runs (seed extents and sightings
every step), concealed deposits are unread until surveyed, and the AI founds only on known sites (exploring first). Re-pinned
once, on the merged tree (MEASURED), each with a RE-PIN comment naming both causes:

| golden | v34 | Track CP (5af05ad) | merged v35 | Track EX's part |
|---|---|---|---|---|
| founded seed 42 t300 (`SnapshotTests.FoundedGoldenHash`, ci.yml `FOUNDED_GOLDEN`) | `0f29efc7…` | `5c56a1f9…` | `0a0759ef…` | tables + concealment |
| toy seed 42 t200 (`ToyGoldenHash`) | `2e6d4461…` | `1d3f5be2…` | `f97432f0…` | layout alone (four empty prefixes) |
| driven seed 42 t300 (`DrivenGoldenTests.Golden`) | `eb88663a…` | `1fb16d8b…` | `62c3345d…` | tables + concealment |
| first reign t40 (`FirstReignTests.PostR1Golden`) | `f7ca60a5…` | `3fee0f31…` | `91584395…` | tables + concealment |
| AI world seed 42 t300 (`AiFoundingAttributionTests.S1cTurn300`) | `25f4799d…` | `5623de7a…` | `8378f993…` | + known-site founding, Explore lane |

**Attribution.** `TestConfigs.PreM6` = `PreTrackCp(PreDiscovery(cfg))` switches the whole slice off; a world stepped under
it writes nothing in sections 62–66, and `SchemaProjection.V34` (which strips 62–66 and asserts them empty) returns the v34
pin byte for byte. `DiscoveryLayoutAttributionTests` proves it per golden and that the EX-alone world (`PreTrackCp`)
writes discovery rows and differs from the twin; it also pins the PreM6 raw hashes the older rigs' ON arms reach
(founded `555238a9…`, AI `d67da0a7…`). `CivicsPointsAttributionTests` now attributes the CP half on the EX-off world
(`PreDiscovery`). Per R4 every older twin chains `PreM6` (`PreIncrementalConstruction`, `PreReservations` and through them
`PreD054Sim`, `PreD053` …), and every pre-M6 attribution rig (the cylinder rigs, reservations/incremental/wrap/granary
layouts, the de86ace per-settlement rigs, the seam world, the founding rigs, the allocation scenario) runs on it.
**AI pins** (`AiEmpireIntegrationTests`): first build 2 → 4, advance 221/222 → 230/231, road 134/135 → 138/139, levy
440/441 → 467/468 (A3 440 → 467), foundings 35, 71, … 428 → 38, 78, 114, 151, 189, 225, 296, 367, 438 (nine, 15
settlements); control `OneAiEmpire_WithTheDiscoveryLayerOff_EveryPinReturnsItsTrackCpValue`.

**CI.** `FOUNDED_GOLDEN` re-pinned; the AI kind set is `{3..9, 12, 13, 14}` (MEASURED: 13 Explore orders in the 600-turn
leg, none Prospect); the `--found` leg now explores from settlement 0 at turn 0 and founds at the re-derived known sites
335063 (turn 2) and 326873 (turn 5, from colony 12), its delivery twin is the same log without the turn-2 order.
**OPEN (Director):** the D-055 allocation leg S42-ALLOC-H no longer exhibits two of its premises under the shipped
concealment — the player's empire quarries ≈ 40 stone a step instead of ≈ 64, so building work claims more than its share on
every step and road works never less (MEASURED: no Y step, `split-check` "P5: no step where building work's claim above its
share used road works' unused share"). So the scenario runs on the discovery twin: in process on `PreDiscovery` (R4) with
every D-055/D-058 literal intact, and through the CLI with a new switch, `sim run|replay --without-discovery` (sim.json
discovery and fog off; `Cli.SimCfg` and the session pipeline honour it), which the ci.yml allocation leg and the
`--from-save` rigs (`RunFromSaveCliTests`: under the fog the dev AI holds no stone for a granary until turn 68, MEASURED)
now pass. Re-deriving S42-ALLOC-H under the fog (re-timing the road phase so the route completes) is the open item.

**Other findings.** (1) Tools held are worn by farmers (ProductionSystem `ToolWear`, ≈ 136 a step at a dev settlement with
ample tools) before DiscoverySystem draws, so a prospect needs tools above the step's wear; the UI/AI predicate reads PREV
stocks and can admit an order the step then refuses (charging nothing). (2) Pre-v35 sessions are replayed WITHOUT the
discovery layer (`DiscoveryQuery.RecordedWithoutDiscovery`, mirroring `RecordedFlat`; `UiSession.Resume`, `sim inspect`), as
they were played; Track CP replays them with Civics Points on (no order is refused by CP). (3) The Actions block is compact
(one line per settlement, then its costs and blockers) and painted last on POLICY; the playability gate gained a post-End-Turn
Confirm row (a legal site is known only after an Explore), accepts an overlay channel that draws nothing because every known
hinterland touches undiscovered ground when its key says so, and clicks only known settlements' WHERE rows.
