# City-state progression — design note (R2a, written before the code)

**Status:** IMPLEMENTED on `m5i-r2a-trade-citystates` under the Director's LOCKED decisions 4 and 12
(2026-10-03). Levels: L1 = Director decision, L2 = existing architecture, L6 = minimal inference made here.

## 1. What the architecture already has (inspected)

- Knowledge is the `ResearchCompleted` relation keyed `(PolityId, ResearchNodeId)`; progress, targets, Eurekas and
  credit provenance are keyed the same way. `ResearchSystem` runs one identical loop body per key.
- Every capability predicate takes a completed-mask (`ResearchQuery.CompletedMask(world, content, polity)`).
- Settlement-scoped consumers (recipes, R1) already ask ONE seam for "the knowledge of this settlement":
  `CraftingQuery.KnowledgeOf` — the controller's mask, or (R1) the all-false mask for an uncontrolled settlement.
- `PolityId` values in use are ≥ 1 (player 1, AI 2…); `InstitutionsSystem` uses 0 for "no owner". Nothing
  validates that a research row's polity is on the roster; no negative `PolityId` is used anywhere.
- Uncontrolled settlements arise in a live world only from revolt (ControlRow removed); colonies inherit the
  parent's control (`ColonizationSystem` §3a).

## 2. The extension (smallest conforming one)

**A settlement-scoped KNOWLEDGE HOLDER key in the existing research tables (L6).** An uncontrolled settlement `s`
holds knowledge under the reserved, derived key `PolityId(-1 - s)` (`SettlementKnowledge.LocalHolder`). It is a
key of the research relation and nothing else: it is never added to `Polities`, never in `Controls`, issues no
orders, owns nothing, has no command source. It is therefore not a second ownership abstraction — the control
relation stays the only answer to "who rules this place".

- **No second research system.** `ResearchSystem` iterates its knowledge holders: the roster polities (as before),
  then — only in a world that has a control relation — every settlement with no controller, in settlement-table
  order. The SAME loop body applies: same content, same availability (prerequisites, research stage), same
  Eurekas (evaluated over the holder's settlements — the one settlement), same EffectiveCost, same completion.
- **No schema change.** The rows have the existing shape; the canonical schema (v31) is unchanged. Populated
  round-trip coverage is added for local-holder rows anyway (negative key).
- **One knowledge seam.** `SettlementKnowledge.MaskOf(world, research, settlement)` replaces R1's
  "uncontrolled = baseline only" with "uncontrolled = its own accumulated knowledge"; `CraftingQuery.KnowledgeOf`
  delegates to it, so recipes, the labour surface, the Trade gate and (when switched on) the farming yield all
  read the same thing. `ResearchQuery.Population/Adults/EurekaHolds` count a settlement toward its HOLDER
  (controller, else its local key) — bit-identical for real polities.

## 3. Autonomous target choice and pace

- **Choice (L6, reuse):** with no target (or a target no longer available), the holder targets
  `AiResearchPolicy.Cheapest` over its available nodes — the existing composite key (EffectiveCost, key) with its
  tie-dense tests. No goals (a city-state has no Age, roads or tax edict to plan for). Deterministic, no RNG.
- **Pace (L6, TUNE):** RP = `cityStatePaceFraction` × the content's own RP curve of the settlement's population
  (`research.json tuning.cityStatePaceFraction` = 0.25). Reference: Civ VI city-states do not research at all;
  the Director rules ours progress, slowly. A quarter of the organized rate, before the sublinearity penalty of
  being one settlement rather than a pooled realm, makes a city-state substantially slower than a civilization of
  the same population (tested) while still visibly accumulating over centuries.

## 4. Control changes (decision recorded)

> **R3 SUPERSEDES part of this section (Director R2-final §2, RATIFIED).** A REVOLT no longer leaves the place
> uncontrolled: it founds a new AI polity holding a complete copy of the former polity's knowledge
> (`KnowledgeTransfer`); annexation is a knowledge union. Local holders below now apply only to settlements that
> are uncontrolled for another reason (hand-built / legacy worlds). See `docs/r3-final-reconcile-record.md` §2.

- **Accumulated state persists.** Local-holder rows are never deleted. While a civilization controls the
  settlement the local holder is dormant (it is not iterated; its people count toward the controller's RP).
- **The settlement's knowledge while controlled = controller's mask ∪ its own accumulated mask.** A craft
  tradition does not vanish when a city is annexed: its smiths keep casting, its merchants keep trading.
- **The controller is NOT granted the settlement's knowledge.** The controller's `CompletedMask` (its research
  tree, its Ages, its tax edict, its AvailableActions knowledge) is unchanged. Diffusion of conquered knowledge
  into the controller would be a new mechanic (cf. the foreign-exposure seam); it is OPEN, not invented here.
- **Losing control again** resumes local research from what the settlement had accumulated.

## 5. What it does not do

No hidden resources (RP comes from its real population), no separate physics (the same ProductionSystem,
TradeArbitrageSystem and predicates), no orders, no Age for a city-state (Ages belong to civilizations, D-043).
Colonies inherit their parent's control and so are not city-states; seeding a newly uncontrolled settlement with
its former ruler's knowledge is OPEN (a revolting settlement starts its local record empty).
