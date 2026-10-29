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
A row is **eligible** when its abundance is > 0 and its good is in `discovery.concealableGoods` = **copper-ore, tin-ore**
(the ores only — the coordinator's ruling under D-062, 2026-10-23). Stone and clay are surface quarry goods and, like
livestock, fish, timber, fiber and hides, are never concealed. The share is measured over eligible rows.

**Denominator change (recorded).** The first build counted four geological goods (stone, clay, copper-ore, tin-ore): 48
eligible rows on each canonical founding world (12 settlements × 4), every settlement n = 4. With the ores only, the
denominator is **24 at turn 0** (12 × 2: every founded site holds both ores) and **38 at turn 300 of the AI world** (19
settlements, colonies included). With n = 2 per settlement, the first build's per-settlement u (an independent hash per
settlement) would give k = floor(0.5 + u) — a coin flip per settlement and a realized share anywhere from 0 to 0.5 on a
small world; the low-discrepancy u of §3 keeps it near s.

## 3. Classification (D2) — derived, never stored

For a settlement with n eligible rows: k = floor(n × s + u_s), s = `discovery.concealedShare` = 0.25, and
**u_s = frac(h(seed) + 0.6180339887498949 × settlementId)** (`DepositConcealment.Fraction`): h(seed) ∈ [0, 1) is the top
53 bits of SplitMix64(seed ⊕ `ConcealSalt`) — a salt distinct from every founding salt — and the golden-ratio step spreads
successive settlements' offsets evenly over [0, 1) (a low-discrepancy sequence), so Σ k stays within about one row of
s × Σ n however small each n is. The concealed rows are the k lowest by the composite key
(hash(seed, salt, settlement, good), goodId) — an integer key with a stable integer tie-break
(`DepositConcealment.LowestByKey`, tie-dense test `Classification_TieDense_EqualKeysOrderByGoodId_IndependentOfInputOrder`;
the offset itself is pinned by `Fraction_IsTheSeedOffsetPlusTheGoldenStepPerSettlementId_ModuloOne`). Stateless: no
RngRegistry stream is drawn, nothing is written.

**Measured realized share over ALL eligible rows (AI world: canonical 1024 px, aiEmpires 1, the AI's own orders; colonies
included), MEASURED 2026-10-23 and pinned exactly by
`DiscoveryTests.RealizedConcealedShare_AtTurnZeroAndTurn300_OfTheAiWorld_IsPinnedWithin20And30Percent`:**

| seed | turn 0 | turn 300 |
|---|---|---|
| 1 | 6 / 24 = 0.250 | 9 / 38 = 0.237 |
| 2 | 7 / 24 = 0.292 | 10 / 38 = 0.263 |
| 42 | 7 / 24 = 0.292 | 10 / 38 = 0.263 |
| 7 | 6 / 24 = 0.250 | 10 / 38 = 0.263 |

Each is in [0.20, 0.30], asserted beside the exact pin.

## 4. Effect and Prospect (D3, D4)

**One predicate.** `DepositConcealment.HiddenGoods / Abundance / KnownAbundance`: for the settlement's CURRENT controller
an undiscovered concealed row reads as abundance 0. Routed through it: `ProductionSystem.FromDeposits`,
`LabourActivities.GoodsOf`, `ConstructionQuery.Producible`, `InfoQuery` (good card, `Obtainable`, the deposit read), the
site assessment's `IfFoundedNext` (the roll a settlement founded next would get, classified under the id it would get —
nobody has surveyed a settlement that does not exist; the terrain-based range is still shown), and the resource
overlay's ore markers. Developer / Explain surfaces and `sim diff` read the raw table. A settlement no polity controls has
no surveyor (its concealed rows read 0); a settlement that changes hands is undiscovered for its new controller.

**Prospect = OrderKind 15.** TargetId = a settlement the issuer controls and has not surveyed; Amount = 0 (load-checked).
Cost `discovery.prospectGrain` = **40 grain** (the survey party's provisions; §5), drawn through `EmpireStockpile.Draw`
(the settlement first, then nearest-first), never below construction's set-asides, SUNK under **reason 17 `Prospecting`**
(the Observer's grain identity counts it with Expedition); refused at delivery (nothing charged, the state is the
orderless step's — pinned, with the one-short / exactly-the-cost edges, on DiscoverySystem alone so no harvest moves the
purse) when unaffordable. The tools parameter (`prospectTools`, `DepositConcealment.ProspectGood`) is removed. Stamped t → `SettlementSurveyRow(P, S, t)` in t+1; the
deposit first produces in the step t+1 → t+2 (production reads PREV) — both pinned turn-exact. Once per polity per
settlement (a second prospect, same turn or later, is refused and charges nothing — pinned). Prospecting never creates,
moves or resizes a deposit.

**The technology discount.** `discovery.prospectDiscountRequires = "copper_smelting"` (key 64, "Copper smelting"):
recognising malachite and azurite *as ore* is the knowledge copper smelting embodies, and it is the earliest node in the
current content whose capability is the use of ore (copper_native works native metal found on the surface; mining_shaft
is depth, not recognition). With it completed the cost halves, integer division (40 → 20; pinned). Parsed against research.json at
load (`ValidateDiscoveryAgainstResearch`), evaluated by the existing knowledge evaluator.

## 5. Costs — the measurement

Measured on this tree (2026-10-23), per settlement, seeds 1, 2, 42, 7. Orderless canonical worlds (first build):

| seed | turn 0 grain (median, min–max) | turns 5–40 grain (median) |
|---|---|---|
| 1 | 7 227 (2 833–8 335) | 521–606 |
| 2 | 7 282 (4 133–11 623) | 507–619 |
| 42 | 6 854 (3 705–11 759) | 515–668 |
| 7 | 5 549 (3 221–10 434) | 502–665 |

The AI world (aiEmpires 1, the AI's own orders, Explore and Prospect live) at turns 5, 10, 20 and 40, over every
settlement: **minimum 131–423, 10th percentile 253–470, median 502–646**. The founding store (thousands) is eaten down to
a steady store of about 500 by turn 5 and stays there.

- **`fog.exploreGrain` = 60** (unchanged): about 1 % of a founding store and about a tenth of the steady store, so every
  settlement can afford an expedition from turn 0.
- **`discovery.prospectGrain` = 40** (the coordinator's ruling: the cost in GRAIN, about 40): **under a tenth of the median
  steady store (≈ 8 %), about a sixth of the 10th percentile and under a third of the poorest settlement's store in the
  window** — affordable at every settlement from turn 0 and within the first few dozen turns without risking its food; a
  survey party is cheaper than an expedition (no 50 km travel). The AI prospects from turn 1: 13 Prospect orders in 300 turns
  on each of the four seeds (one per settlement it holds then; once per settlement).
- **The technology discount** stays on `copper_smelting` (§4): 40 → 20.

**Preview and step agree, or the refusal is explained.** The UI and the AI judge affordability on the PUBLISHED stocks
(`DiscoveryQuery.CheckProspect/CheckExplore` on PREV, the same predicate DiscoverySystem applies); the step draws from the
stocks its own harvest, meals and building left (DiscoverySystem runs after them). The two use one predicate and one cost
(`ProspectCost`, `ExploreCost`), so a preview "cost 40 grain" is what the step draws. Where the step's purse is smaller than
the preview's (a store eaten below the cost within the turn), the step refuses and charges nothing, and the UI EXPLAINS it
after the step: `UiSession.LastDelivered` keeps the delivered batch, `DiscoveryQuery.RefusedAtDelivery(prev, next, …)`
finds every delivered Explore whose radius did not grow and every Prospect that left no survey, and the Actions block
paints `DiscoveryQuery.RefusalText` ("The survey of X was refused when the turn ran: … Nothing was paid; order it again.")
under its heading — never a silent no-cost refusal (pinned: `DiscoveryActionsUiTests.AnOrderTheStepRefusesAtDelivery_…`).

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
deposits (cost 40 grain)", each with the predicate's blocker ("explored to the limit (15 nodes)", "already surveyed",
"not enough grain (needs N)", "already ordered this turn"), and — after a step that refused a delivered Explore or Prospect
— a "last turn:" line explaining the refusal (§5). New `ActionDomain.Discovery` (15; id = settlement × 2 +
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
ci.yml's AI kind set gains 14 and 15 (the AI explores and prospects in the 600-turn run; §12).

## 10. Recorded deviations

1. **Emergent colonization stays unchanged** (people, not a state): D-037 B1 founding is not gated by the known extent.
2. **No ghosts before M7:** formations cannot move before M7, so a last-known marker would equal the live position;
   a rival formation is shown only while visible, and no ghost row exists.
3. **Minimal fog:** visibility is the settlement nodes and formation discs only (no line of sight, no terrain, no roads,
   no trade-route knowledge); remembered land is drawn with no stale picture of what is there; the terrain under known
   land is the live terrain (static by ADR-008).
4. **The AI's prospect key reads visible eligible goods** (§9).
5. **Prospect is paid in grain** (the coordinator's ruling, §5) — the first build's tools cost was unaffordable until
   toolmaking.
6. **`PolityKnowledge.Of` takes the config.** Its fog parameters (seed and sight radii) are content; the stored extent
   and sightings remain its only stored inputs.

## 11. Deferred to M7 (and later)

Mobile scouts and Action Points (Explore from a settlement is the stand-in; its provisions sink is replaced by the mobile
unit); ghost (last-seen) formation rows; line of sight and terrain-dependent sight; sharing maps (diplomacy, M8); trade,
roads and claims gated by the known extent (no new gate in this slice beyond the seam's existing filters — the trade
partner, road-development route and claim readers are unchanged); sight from trade routes; the remap seam for any
deposit change; depletion.

## 12. Golden moves and attribution

Schema v35 (Track CP's section 62, then this track's 63–66), the shipped discovery layer (with the coordinator's rulings of
2026-10-23: grain-paid Prospect, ores-only concealment) and Track TREE's research-content tranche (ADR-045, merged after)
move the golden worlds: the discovery tables are POPULATED wherever DiscoverySystem runs (seed extents and sightings every
step), the concealed ores are unread until surveyed, the AI founds only on known sites (exploring first) and prospects, and
the tranche changes what the AI researches. Re-pinned ONCE, on the final M6 tree (MEASURED 2026-10-23), each with a RE-PIN
comment naming all three causes (CP, EX, TREE); the intermediate merge-time pins are superseded:

| golden | v34 (pre-M6) | Track CP (5af05ad) | CP + EX merge (b67e578) | final M6 (CP + EX rulings + TREE) | TREE's part (measured) |
|---|---|---|---|---|---|
| founded seed 42 t300 (`SnapshotTests.FoundedGoldenHash`, ci.yml `FOUNDED_GOLDEN`) | `0f29efc7…` | `5c56a1f9…` | `0a0759ef…` | `8ccbe80f…` | none (tranche off: same hash) |
| toy seed 42 t200 (`ToyGoldenHash`) | `2e6d4461…` | `1d3f5be2…` | `f97432f0…` | `f97432f0…` (unchanged) | none (no research) |
| driven seed 42 t300 (`DrivenGoldenTests.Golden`) | `eb88663a…` | `1fb16d8b…` | `62c3345d…` | `392e2806…` | none |
| first reign t40 (`FirstReignTests.PostR1Golden`) | `f7ca60a5…` | `3fee0f31…` | `91584395…` | `e3a86ce6…` | none |
| AI world seed 42 t300 (`AiFoundingAttributionTests.S1cTurn300`) | `25f4799d…` | `5623de7a…` | `8378f993…` | `776d5d0c…` | moves it (tranche off: `53649caf…`) |

**Attribution.** `TestConfigs.PreM6` switches the whole slice off: the discovery layer (`PreDiscovery`) and the research
content replaced by the PRE-M6 research.json (Track TREE's fixture `Sim.Tests/Fixtures/research-pre-adr045.json`,
byte-identical to the file at 06e0153 — before both Track CP's edits and the tranche; behaviourally the v34 content). A world
stepped under it writes nothing in sections 62–66, and `SchemaProjection.V34` (which strips 62–66 and asserts them empty)
returns the v34 pin byte for byte; the raw PreM6 hashes are unchanged by the TREE merge (founded `555238a9…`, AI
`d67da0a7…`, MEASURED). `DiscoveryLayoutAttributionTests` proves it per golden and that the EX-alone world
(`PreTrackCp ∘ PreTreeTranche`) writes discovery rows and differs from the twin. The research twins compose: `PreTreeTranche`
is a single-layer twin (the fixture with CP's two edits re-applied — the converter asserts the tranche never touches
`tuning` or `researchStage`), `PreTrackCp` maps the pre-tranche family to the fixture, so
`PreTrackCp ∘ PreTreeTranche` on the discovery-off world is `PreM6`. `CivicsPointsAttributionTests` attributes the CP part
on the EX-off, tranche-off world. Per R4 every older twin chains `PreM6` (`PreIncrementalConstruction`, `PreReservations`
and through them `PreD054Sim`, `PreD053` …), and every pre-M6 attribution rig (the cylinder rigs, reservations/incremental/
wrap/granary layouts, the de86ace per-settlement rigs, the seam world, the allocation scenario) runs on it; Track TREE's own
older-layer controls that ran on `PreTreeTranche` and compare with pre-M6 values run on `PreM6`
(`OneAiEmpire_WithTheWholeM6SliceOff_EveryPinReturnsItsBatch4Value`,
`AiFoundingAttributionTests.TheM6Slice_IsTheWholeMove_…`, `CliConstructionLifecycleTests`' in-process twin for the v33
trace), while its v33/v35 diff save is written on `PreTreeTranche ∘ PreDiscovery` (the world the `--without-discovery` CLI
wrote before the tranche, Track CP on). `MerchantTests` first active 148 (pre-M6) → 159 (CP + EX merge) → 138 (final: the
ores-only ruling; TREE moved nothing, MEASURED).
**AI pins** (`AiEmpireIntegrationTests`, pre-M6 → final): first two targets knapping_oldowan, ground_stone_early →
grinding_stone, kin_council (the civic lane's first pick on turn 0); research target 2 at 25 → 0; first build 2 (unchanged —
the CP + EX merge's 4 was the concealed-stone stall, gone with ores-only concealment); advance 221/222 → 111/112; road
134/135 → 166/167; levy 422/423 → 579/580 (A3 422 → 579); foundings 35, 71, … 357 (eight, 14 settlements) → 38, 78, 114,
151, 189, 225, 296, 367, 438, 509 (ten, 16 settlements). Controls: the whole slice off returns every batch-4 value; the
discovery layer and the tranche off return Track CP's (`OneAiEmpire_WithTheDiscoveryLayerAndTheTrancheOff_…`).

**CI.** `FOUNDED_GOLDEN` re-pinned to `8ccbe80f…`; the AI kind set is `{3..9, 12, 13, 14, 15}` (MEASURED on the final
tree: the 600-turn leg's run log holds 400 orders — 13 Explore, first at 35, and 17 Prospect, first at turn 0); the
`--found` leg (explores from settlement 0 at turn 0, founds at the known sites 335063 at turn 2 and 326873 at turn 5 from
colony 12) passes unchanged. Both legs and the allocation leg were run locally from the final tree.
**Where `--without-discovery` remains, and why (re-checked 2026-10-23 with stone no longer concealed).** The CLI switch
`sim run|replay|inspect --without-discovery` (sim.json discovery and fog off; `Cli.SimCfg` and the session pipeline honour
it — the `TestConfigs.PreDiscovery` twin, R4) is kept ONLY where a test genuinely compares against a measurement or a
recording made before M6:

1. **The D-055 allocation scenario S42-ALLOC-H** — the ci.yml allocation leg (every run and replay),
   `AllocationCiLegTests` (its expected command list), `AllocationScenarioRun` and its D-058 twin (in process on
   `PreDiscovery`) and `AllocationScenarioTests`' cross-process continuation. Its turns and literals are D-055's pre-M6
   measurement. MEASURED under the shipped rules (ores only): the purse's stone at 116 is 5 048 against D-055's 4 913, road
   works lend building work their unused share only at 128 (not 131), competing heads occur only at 117–119, and the leg's
   `split-check alloc-b.rep --expect-hold-at 131` FAILS P5 and P7 (the concealed copper and tin are unworked until surveyed,
   so extraction labour moves to the visible goods). Re-deriving S42-ALLOC-H under the shipped rules is the OPEN item.
2. **`CliConstructionLifecycleTests`** — its rigs replay and diff a session, trace and save RECORDED by the v33 build and
   premise the colony at 12. MEASURED without the switch: the replay diverges from the v33 trace at the first hash, `run`
   refuses the turn-3 order naming settlement 12 (the AI founds only on a known site, so no colony 12 by then), and the v33/v34
   diff gains LedgerFlows rows.

**Removed** where the pre-M6 measurement holds again or the test is not a pre-M6 comparison: the `--from-save` rigs
(`RunFromSaveCliTests` — with stone visible the dev AI enqueues its granaries at 7 and 14 again; the one literal that moved,
the AI colony's founding turn 72 → 73, is re-pinned with its cause), and the AI construction stall is gone
(`AiEmpireIntegrationTests`' first build returns to its pre-M6 turn 2).

**Other findings.** (1) The step draws after its own harvest, meals and building, the preview on PREV: an order the
preview admitted can be refused at delivery (charging nothing); the UI explains it after the step (§5). (2) Pre-v35 sessions are replayed WITHOUT the
discovery layer (`DiscoveryQuery.RecordedWithoutDiscovery`, mirroring `RecordedFlat`; `UiSession.Resume`, `sim inspect`), as
they were played; Track CP replays them with Civics Points on (no order is refused by CP). (3) The Actions block is compact
(one line per settlement, then its costs and blockers) and painted last on POLICY; the playability gate gained a post-End-Turn
Confirm row (a legal site is known only after an Explore), accepts an overlay channel that draws nothing because every known
hinterland touches undiscovered ground when its key says so, and clicks only known settlements' WHERE rows.

---
**Dated note, 2026-10-23 (ADR-046; the Director's M6 review instruction).** The Prospect cost of §4/§5 (grain only, halved
by `copper_smelting`) is superseded by ADR-046 §2: the survey bill (40 grain, 20 timber, 10 stone) and the assay
(20 grain, 10 timber, 5 stone, 2 bronze, behind `ore_prospecting`), selected by the Prospect order's Amount (0/1). The fog's
units are restated in km (ADR-046 §3); `formationSightNodes 1` is the node and its 4 orthogonal neighbours (the sim.json
"8-neighbourhood" wording was wrong; behaviour unchanged). The text above is kept as written.

---
**Dated note, 2026-10-28 (M6 closure, track FOG; D-066 §1 R1, §2 A18/A19, §3). The text above is kept as written.**
Every number below was MEASURED on the track's tree (the M6 closure base + this track) by the agent writing this note.

**1. F-VERIFY — the research-sight fix to the stored extent (§6; ADR-047), independently verified.**
`DiscoverySystem` builds each polity's stored union with `DiscoveryQuery.KnownBits(prev, f, polity, _cfg.Research)` — the
SAME formation-sight radius `PolityKnowledge.Of` answers (research-improved, ADR-047). Mutant: the call reverted to
`KnownBits(prev, f, polity)` (the content radius). Clean: `DiscoveryExtentResearchSightTests` 2/2 pass (≈2 s). Mutant
(bound: 10× the clean class time): KILLED by the semantic test
`TheStoredExtent_HoldsTheResearchSightDisc_SoTheLandStaysKnownAfterTheFormationLeaves` — first at its stored-extent
assertion ("word 57: a node of the radius-2 sight disc is missing from the stored extent"), and, with that assertion
disabled, independently at its player-visible assertion ("node 3702 of the radius-2 disc was forgotten when the formation
left"). Not a golden kill. The property is one the system ought to have (D-040 B2: the extent is permanent once gained;
§6: DiscoverySystem stores the union the query answers). **VERDICT: the fix is correct and its test has teeth.**
`ResearchExposureControlChangeTests` (the exposure seam across a revolt; 5 tests) pass and are not about the sight radius.

**2. F-MONO — the extent is monotone across revolt and loss of control (D-066 §3, COMPLETED).** New
`Sim.Tests/Systems/DiscoveryExtentControlChangeTests` (2 tests), the revolt corner of ResearchExposureControlChangeTests
on the dev world WITH terrain and the shipped fog, stepped by discovery then revolt (the shipped relative order): the
player explores from the settlement it will lose (radius 3 → 6, land no current control re-derives), the settlement is
made destitute and revolts into a new AI polity. Pinned turn-exactly: every stored word of the parent's extent survives
every later step and `PolityKnowledge.Of` still answers it known; the parent keeps knowing the lost place (sighting, E5);
the revolt-born polity on its FIRST world has no stored row yet its whole seed disc is known through the query, and the
seed is stored one step later; it inherits none of the parent's explored land. A CAPTURE (the control row moved to the
rival — the state an M7 conqueror leaves; no production path does it today): the loser keeps every word, the captor knows
the captured seed at once and stores it the next step. Mutants (each killed by both tests or by the named one): the stored
extent dropped from `KnownBits` (both); the query-time seed dropped from `PolityKnowledge.Of` (both); `DiscoverySystem`
storing only polities that already have a row (both).

**3. F-LEAK — the leak gate runs on the production fog (D-066 §3, COMPLETED).** `Sim.Ui.Tests` `KnowledgeLeakTests`
(the main file and its Military file) run every family on two lenses: **Fog — the gate of record**: the SHIPPED config
(fog, discovery, military and economy on) and the session's own production view, `PlayerKnowledge.Of(world, player,
cfg)`, the AI hidden by the REAL fog, no stub; **Stub — a control leg**: the fog-off world through `KnowledgeStubs`. The
all-known views are the positive controls of both. MEASURED before fixing the rig (seeds 42, 1, 2, 7; turns 0–12;
canonical 1024 px; one AI Empire): the player knows 0 of the AI's 6 settlements, 0 of its formations and not the polity on
every seed and turn (174 known lattice nodes of 65 536); the 256 px military rig likewise through turn 6. The fog rig is
seed 42 after six turns, with one player Explore at turn 0 from its settlement farthest from every AI settlement (so its
known land reaches past its own catchments — the place card's claim channel needs a known node it does not claim), and
the AI still unsighted (asserted). The one family that cannot arise under the shipped fog — a step that un-knows a
settlement (sighting memory is monotone; ghosts are M7) — keeps its stub leg and gains a fog pin that a sighted settlement
stays known across steps. A known foreign settlement, where a family needs one, is known by a planted sighting row (E5),
never by a stub. Result: 65/65 (42 → 65 tests). Fog-only mutant (the sighting arm of `KnowsSettlement` reading ANY
polity's sightings): killed by 18 fog legs, survived by every stub leg — the fog legs see what the stub cannot.
`PolityKnowledgeTests` gains fog-on legs through the production seam (the seam's answer, the AI site scan, the AI producer
against foreign books, the formation card) and its stale header ("all-known today") is corrected; its foreign-books
perturbation now includes foreign civics state (Civics Points purse, civics-tree completions, progress, target).

**4. F-CHANNEL — the AI values only known terrain (D-066 A18, COMPLETED).** `Sim.Core/State/KnownSiting.cs`: region
fertility summed directly over the KNOWN cells of BoxSum's window (unknown cells add nothing; no integral image, whose
low bits read cells above-left), water access to the nearest KNOWN water or river cell (the same 4-neighbour metric,
capped at the cutoff), the score for known land cells, the candidate floor over the known scores, and the known lattice
nodes. `AiFoundingPolicy` ranks by it, takes its candidate arm, and floods only known nodes
(`Pathfinder.ReachableWithin`, pinned equal to the uncapped Isochrone with every node admitted) — whenever the seam
restricts (fog on) and sim.json `colonization.aiFounding.knownTerrainOnly` is true (shipped; absent = true). Fog off, the
scan is the pre-A18 scan byte for byte (the seam is all-known; every fog-off golden and twin unchanged, MEASURED).
`FrontierSiting` carries its `SitingConfig` (set by `PrepareFrontier`). The quarantined tooth is inverted to equality
(`PolityKnowledgeTests.KnownCellsValuations_ReadOnlyKnownTerrain_TerrainUnderUnknownNodesLeavesThePickIdentical_D066A18`:
the valuation, the pick and the order identical; the switch-off twin moves the pick, as P7 measured). New:
the valuation identical under half-map fertility/water/river perturbations; the known-reach island test; the fog-on
production-seam test (every AI order identical under fertility 0 or 1 beneath every unknown node; the twin's founding
order moves, 15774 → 15516). Mutants: all cells treated as known (4 tests kill); ranking by the whole-map raster (2);
the whole-component flood (the island test).
**Recorded residual (not closed; not AI-only): the founding PREDICATE** (`FoundingQuery.Check`, the rule the player's
placement mode and the AI share) judges its candidate floor over the whole map and spacing and the route over the whole
lattice, so a perturbation of unknown land can still change the AI's order THROUGH THE RULE (MEASURED: a river under every
unknown cell refuses the AI's pick, its valuation unchanged). The player is told the same verdict for the same cell.
Pinned OPEN by `PolityKnowledgeTests.RecordedResidual_ThePredicatesOwnArms_ReadLandTheIssuerDoesNotKnow_SharedByThePlayerAndTheAi`;
owned with DW-002 by M7 (D-066 A19). NEW DECISION (recorded under D-066's rules — simplest, deterministic, testable,
reversible): "what a polity assumes of terrain it has not seen" = nothing (zero fertility, no water); the switch reverses
it.

**Moved pins (the golden protocol; A18 is the only cause on this tree).** The R4 rule: every older twin strips A18 too
(`TestConfigs.PreMilitary` and `PreEconomyTrack`, through which every chained twin passes), so every older attribution
control returns its recorded value unchanged (MEASURED: 11 failing controls on the first run, 0 after).

| pin | old | new | control (returns old exactly) |
|---|---|---|---|
| AI world seed 42 t300 `AiFoundingAttributionTests.S1cTurn300` | `ac4554bfdcd3d5b6…` | `7aed09b9c85948aa…` | `TestConfigs.PreAiKnownTerrain` (`AiWorldTurn300_WithTheKnownTerrainValuationOff_IsThePreA18World`) |
| its economy projection `S1cTurn300PreEconomy` | `3153b7e3214ecd21…` | `2b4d88e0b6f21ee8…` | the same twin |
| `AiEmpireIntegrationTests` canonical AI: levy / first levy | 380 / 381 | 381 / 382 | `OneAiEmpire_WithTheKnownTerrainValuationOff_EveryPinReturnsItsPreA18Value` |
| the same: foundings | 38, 78, 114, 151, 189, 225, 296, 367 | 44, 80, 116, 152, 188, 224, 295, 366 | the same |
| §3's realized concealed share, AI world seed 1, turn 300 (`DiscoveryTests.RealizedConcealedShare_…`) | 9 / 38 | 9 / 36 (0.250, one colony fewer by turn 300); seeds 2, 42, 7 unchanged | `RealizedConcealedShare_Seed1Turn300_WithTheKnownTerrainValuationOff_IsThePreA18Count` |
| founded `d5ebd0b2c642ec5c…` (ci.yml `FOUNDED_GOLDEN`), driven `55afd498a326ab2a…`, first reign `064ed6b76374dea4…` | — | unchanged (MEASURED: SnapshotTests, DrivenGoldenTests, FirstReignTests pass) | — |

Cause of the AI moves: valuing only known terrain, the AI explores longer before its first founding (Explore stamped
35–43; first founding 44, was 38). Three harness adjustments, no pin moved: the headless gate's `PlayabilityGate.LegalPlace`
(the place the gate clicks for the player) ranks the player's KNOWN legal cells with the whole-map valuation — with the AI's
known-terrain floor it found none in 12 of 13 states and `found-confirm` went uncovered (MEASURED: `--smoke --monkey 200`
24 FAIL → 0 FAIL, SMOKE PASSED); and `IdentityDriftGuardTests`' recorder used the
AI ranking as a site FINDER for the player and, under the fog, now finds no site after its two expeditions (MEASURED:
`OffMap` on the -1 it returned), so it ranks with the twin's whole-map valuation (its predicate check unchanged); and
`MapTopologyGuardTests` allowlists `KnownSiting`'s three column lines with reasons (its x steps go through
`MapTopology.NeighbourX`). ci.yml's 600-turn AI leg (one local `run` + the kind check): 349 orders (was 365),
9 Explore, 11 foundings, kind set unchanged.

**5. F-BENCH — the discovery layer's cost (D-066 §3, COMPLETED).** `sim bench` now takes the layer switches
(`--without-discovery`, `--without-tranche2`, `--without-economy`). `sim bench --founded --seed 42 --turns 300 --json`,
three runs of each arm alternated, under `/tmp/civdemo-full-suite.lock`, Release, the track's tree (load average 2–7 on
4 shared CPUs — wall times are indicative):

| arm | total ms (3 runs) | median total | `discovery` phase median | clone bytes / turn |
|---|---|---|---|---|
| discovery on (shipped) | 31 971, 30 751, 33 056 | **31 971 ms** | **113.3 ms** (0.35 % of the step) | 146 815 |
| `--without-discovery` | 34 912, 32 501, 31 833 | **32 501 ms** | 4.8 ms (the system returns at once) | 145 249 |

The layer costs ≈ 0.11 s of discovery-system time per 300 turns (≈ 0.36 ms a step) and +1 566 B of clone per turn; the
totals differ by less than their run-to-run spread. Caveats: `--without-discovery` also turns the military layer off (the
R4 rule, as the CLI documents), which the `military` phase shows (≈ 18 ms on, 2 ms off); bench issues no orders, so the
AI's A18 valuation (paid only on turns an AI founding is due) is not in it — the 600-turn AI leg above ran in 1 m 44 s.

**6. Deferred, per D-066 R1 / A19 (unchanged here):** ghost rows and display, scouts, sighting from moving formations —
M7 (H7-01, H7-07; `m7-handoff-register.md` note 2026-10-28); claim, trade and road routing against the known extent
(DW-001, DW-006) and the predicate residual (DW-002) — M7 with the movement / route graph
(`deferred-work-register.md` note 2026-10-28).
