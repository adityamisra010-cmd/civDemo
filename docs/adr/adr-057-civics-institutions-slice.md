# ADR-057 — The M6 civics and institutions vertical slice: content-driven institution types, the law court, the regime-module shape

**Status: ADOPTED UNDER DELEGATION (D-066 A1, A2, A6, A10, A13; M6 closure track CIV, 2026-10-28; uncommitted; Director confirmation requested at the exit gate).** Implements the slice D-066 A2 adopted (register
D-P items 1–3 and 6–8) on the institution definition D-066 A1 adopted (register D-M option 1), with D-066 A6 (I-7
wording), A10 (forms) and A13 (criterion 8 scope). Every item is reversible; the Director is asked to confirm at the exit
gate, as D-066 §2 requires. Not in scope: government adoption and active political effects (M8, D-D / D-062), any
research.json node, entity, cost or text (TC-1, D-066 R3), the research-economy candidate (R4), movement or M7 work (R5),
D-P item 4 (the inert promotion entry — the unit-progression track's) and item 5 (recorded honestly by that track).

## 0. Questions this ADR answers

- **CR-010 ("what is an institution", never written; register D-M).** Answered by D-066 A1 and built here: an
  institution is a founded, staffed, maturing structure of a settlement or polity on the university lifecycle
  (`InstitutionRow`, Locked → Available → Active → Mature), realized by construction `founds`, its effects only through
  coefficients inside named equations; institution TYPES are content. No CR file is written: the ruling exists (D-066 A1)
  and this ADR records its implementation.
- **D-P item 8 ("no schema change if the type enum is content-driven — to be measured").** Measured: no schema change.

## 1. Civic-state vocabulary (P1; D-D RULED)

`State.CivicState` = `Unresearched | Available | Researched`, answered by `ResearchQuery.CivicStateOf` from completion
and the one eligibility predicate (I-1, I-4: no `CivicsQuery` twin). **"Adopted" is deliberately not a member** — D-D
reserves adoption for M8, so no M6 code path can return, store or display it (I-2 "Available is never read as Adopted",
enforced by the type). A researched government form is presented "Available - adoption in M8" (D-066 A10).

Defect fixed: the INSTITUTIONS lens listed completed civics under "Adopted civics" and its note and the Civics intro
said "Adopted civics …" (`Lenses.cs`, `ProgressionScreen.cs`, `ProgressionPreview.cs`). They now say "Researched".

## 2. Content-driven institution types (P3; D-066 A1)

- `sim.json institutions.types[]` (`InstitutionTypeConfig`): every type beyond research.json `universityTypes`, each
  with its own PROFILE — `maturityTauYears`, `decayTauYears`, `matureAt`, `staffShareAtMaturity`,
  `adultsPerInstitution`, `foundingFoodSurplusRatio`, `sustainingFoodSurplusRatio`. Keys share `InstitutionRow.Type` with
  the university keys and may not repeat them (validated where the files meet). A university's profile is
  `institutions.universities` (`InstitutionContent.UniversityProfile`).
- `goods.json founds` names EITHER `universityType` OR `institutionType` (exactly one; `ProjectFounds.TypeId`).
- `InstitutionContent`: `TypeKeyOf(cfg, project)`, `Info`, `Types` (content order: universities, then sim.json types),
  `Profile`, `MarketOf`. The pre-ADR-057 university-only lookups are kept (`TypeKeyOf(research, …)`, `TypeOf`).
- Per type: viability (`InstitutionViability.ToFound(…, project)`, `ToSustain(…, typeKey)`), staffing
  (`InstitutionStaffing.StaffOfInstitution(cfg, typeKey, m)`), maturation and the MATURE reading
  (`InstitutionsQuery.Stage(cfg, typeKey, m)`). **Markets:** every university type shares ONE host market (the hosted
  count is of all university buildings, as before); every other type is its own market. An unknown key (content changed
  under a save) reads the university profile and market — the pre-ADR-057 reading of every row.
- **Branch:** only university types carry a research branch; the ADR-029 §9 cost seam (`ResearchCostModifiers`) stays
  keyed by research.json university type and is untouched. The ADR-047 maturity-τ operand
  (`institutions.universities.maturityTauYears`) applies to universities only.
- Callers generalized: `InstitutionsSystem` (maturation per row profile; founding by any type), `ConstructionSystem` /
  `ConstructionQuery` (resolution gate and blocker per project), `AvailableActionsQuery.Institutions`, `InfoQuery`,
  `InstitutionsQuery`, `AiConstructionPolicy`, and the UI readers (`InstitutionMarkerSource`, `StateChronicle`,
  `SettlementComposition.PartUniverse`).
- **No schema change (measured):** `CanonicalSchema.Version` stays 39; a law-court row is the same 32-byte
  `InstitutionRow` (`InstitutionTypesSchemaTests.MixedTypes_PopulatedTable_SameLayout_LengthRoundTripAndHashExact`).
- **Bit-identity for universities:** the arithmetic is the old one (`InstitutionTypesSchemaTests.Universities_…`), and the
  four base pins are unchanged on this tree with the court ON (§6).

## 3. The law court (P4; D-066 A2, register D-P items 1, 3, 6)

- **Type** `law_court` (key 6, "Law Court") in `sim.json institutions.types`. Profile: τ 16 / decay τ 21.7 / matureAt
  0.95 and the food thresholds 1.3 / 1.1 are IDENTITY with the universities (the same lifecycle law; the Artisans
  emerge/recede pair for non-food specialists); market 1,000 adults and staff share 0.01 (≈10 adults at maturity) are
  INFERRED from Old Babylonian district courts (TUNE; reference classes in `sim.json institutions._types`).
- **Project** goods.json id 16 "law court": `founds { entity inst.law_code, institutionType law_court }`, no building
  entity (research.json has no courthouse entity, and TC-1 adds none), 240 timber + 160 stone + 32 adult-years (the
  workshop scaled to a ~200 m² hall).
- **Gate:** `inst.law_code` requires the civic `law_code` (key 1001) — the existing entity, so the project is
  `NotKnowledgeEligible` until the civic completes, through `ConstructionQuery.IsKnowledgeEligible`, the one availability
  predicate (no new availability reason, no new order kind: establishment = construction kind 4).
- **Effect:** none written for it. `Unrest.ServiceOffset` already sums every hosted institution's maturity into X
  (V = reach × (1 − e^(−X))); the court's maturity is a term of that existing equation with weight 1 — a coefficient
  inside a named equation, not a bonus — and V acts only through `Unrest.FeltBurden` under a levy.
- **D-021 brake:** its staff (0.01 × 1,000 × M) are withdrawn from every labour pool through the one labour reader, and its
  sustaining viability decays it where the host cannot feed its specialists.
- **AI parity:** `AiConstructionPolicy`'s one-founding-per-turn institution decision ranges over EVERY content type by the
  same composite key (n ascending, X ascending, type key ascending — the key tie-break is now explicit, not content
  order) and the same availability predicate and order constructor as the player. The AI's research goal for institutions
  (ADR-046) stays the universities' requirement only, so the AI's research path is unchanged (`AiResearchPolicy`).
- **The law_code node text** is NOT edited: its `desc` is in the TC-1 TEXT hash (`TreeClosureBaselineTests`), so naming
  the court there would move TC-1 TEXT. The text already says "the institution itself is established by its owning
  system once this civic is complete"; the court is named on the node's detail (§5) instead.

## 4. The regime-module data shape (P5; D-D option 1, D-066 A2 item 2, A10, A13)

- `sim.json regime` (`RegimeConfig`): `baseLabel` "chiefdom" (the label of a polity with no adopted form), `modules[]`
  (one: `codified_law` "Codified law", present while the owner holds an ACTIVE `law_court`, qualifier "law-giving"),
  `forms[]` (the 33 government-form civics of the tranche-2 `regime-forms` lane, content order).
- `State.RegimeQuery` (read-only): `Modules` — derived every read from the institution rows (no stored regime state, no
  adoption order, no Age/date/currency read); `Label` — the DERIVED government label ("chiefdom" → "law-giving chiefdom"),
  NEVER STORED and NEVER READ BY ANY MECHANISM; `Forms` — each form's `CivicState` and its line ("Available - adoption in
  M8" once researched, "Locked - needs …" or the Age-sweep reason before).
- **Source guard:** `RegimeLabelSourceGuardTests` — no Sim.Core file other than `RegimeQuery.cs` names `RegimeQuery`, and
  none other than it and the loader reads the regime section.
- **I-7 under D-066 A6:** `RegimeContent.OptionalityViolations` — a form of content Age x must not be NECESSARY for
  entering any Age y ≤ x + 1 (necessary sets: prerequisite AND = union / OR = intersection, the subtree stage, the Age
  sweep two Ages down, Research / ResearchCount / single-project Structures facts, CORE milestones plus the supporting
  pigeonhole). The six-file load refuses a violation (`SimConfigLoader.WithProgression`). Shipped content: none.

## 5. Presentation (P6)

- INSTITUTIONS lens (with the session config): "Researched civics"; "Institutions" — each founded-by-construction type's
  `InstitutionsQuery.TypeReading` line ("Law Court: Unresearched - needs Written law" → "Available - …" → "Under
  construction" → "Active - 1 founded (maturity 0.25)" → "Mature …"); "Regime modules" with the derived label;
  "Government forms" (n of 33 researched - adoption in M8).
- Progression › Civics: a civic whose unlocked entity a type founds (law_code → the court) paints an INSTITUTION heading
  with the same reading (`ProgressionScreen.InstitutionLines`).
- Action surface: "Found Law Court" is listed under Institutions exactly where `ConstructionQuery.IsProjectAvailable`
  admits project 16 (one predicate, two callers) — extended in directive test 19
  (`ResearchUnlockPipelineUiTests.UiShowsOnlyLegalActions_…`).

## 6. Goldens and the switch-off twin

- The twin is `TestConfigs.PreLawCourt` (the court project, `institutions.types` and `regime` removed — the pre-ADR-057
  content). No runtime switch: the switch-off is content.
- MEASURED on this tree (base + CIV): the founded `d5ebd0b2…`, driven `55afd498…`, first-reign `064ed6b7…` and AI-world
  `S1cTurn300 ac4554bf…` pins are UNCHANGED with the court ON (their tests pass unmodified, including every older-twin arm
  of `AiFoundingAttributionTests`). Cause: in those worlds no court is ordered by turn 300. No pin moved; no re-pin.
- MEASURED beyond the pins (canonical world, one AI Empire, 700 turns, ON vs `PreLawCourt`): seed 42 — the AI completes
  law_code at turn 506, orders a court that turn, the worlds first differ at state 507 (the delivery), the court is founded
  at 508; seed 1 — 500 / 500 / 501 / 502. Both AIs hold three courts by turn 600 (the saturation reading S(3) ≥ 0.95 stops
  a fourth) and read "law-giving chiefdom". Pinned turn-exactly for seed 42 by
  `LawCourtAiWorldAttributionTests.AiWorldSeed42_IdenticalToTheTwinUntilTheFirstCourtOrdersDelivery_…` (the attribution
  control: byte-identical before the delivery, different from it on).

## 7. Decisions taken under D-066's rules (new, recorded here)

1. Court types live in sim.json `institutions.types` (not research.json `universityTypes`): research.json is TC-1-closed
   and a court is not a university.
2. `founds.institutionType` beside `founds.universityType` (exactly one) rather than renaming the field: every existing
   goods.json and test fixture keeps loading.
3. Universities keep one shared market; each other type is its own market.
4. "Active" for the module = any founded row (Active or Mature); maturity 0 counts.
5. The base label is content ("chiefdom"); D-P item 2's example.
6. The AI's research goals are not widened to the court (the court is founded when the civic arrives by the AI's own
   civics route); widening them would move the AI's research path and every AI pin.
7. Display "Unresearched" for the LOCKED stage of an institution type (the polity reading); the per-settlement lifecycle
   enum is unchanged.
