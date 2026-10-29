# ADR-057 — The M6 civics and institutions vertical slice: content-driven institution types, the law court, the regime-module shape

**Status: ACCEPTED — the Director CONFIRMED D-066 A1, A2 and A10 (D-067 R1), A6 (D-067 R4, with its correction: the
validator checks optionality at load, it does not prove it) and A13 (D-067 R5), 2026-10-28.** First recorded as ADOPTED
UNDER DELEGATION (M6 closure track CIV, 2026-10-28; uncommitted). Stale facts are corrected in place and listed in the
amendment log at the end. Implements the slice D-066 A2 adopted (register
D-P items 1–3 and 6–8) on the institution definition D-066 A1 adopted (register D-M option 1), with D-066 A6 (I-7
wording), A10 (forms) and A13 (the government-form criterion's scope: m6-spec §5 criterion 3, per D-067 R5). Every item
is reversible; the Director confirmed them at the exit gate (D-067 R1, R4, R5), as D-066 §2 required. Not in scope: government adoption and active political effects (M8, D-D / D-062), any
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
  *On the integrated M6 tree* the AI-world pin is `5458cb3d…`, moved by ADR-058 (KNOW) and D-066 A18 together, not by
  the court (`docs/m6-plan/golden-attribution-register.md`, dated notes of 2026-10-28); `ac4554bf…` survives there as
  `AiFoundingAttributionTests.PreA18Turn300`. The founded, driven and first-reign values above are the shipped ones.
- MEASURED beyond the pins on the CIV track's own tree (base + CIV; canonical world, one AI Empire, 700 turns, ON vs
  `PreLawCourt`) — historical, superseded for seed 42 by the integrated value below: seed 42 — the AI completes
  law_code at turn 506, orders a court that turn, the worlds first differ at state 507 (the delivery), the court is founded
  at 508; seed 1 — 500 / 500 / 501 / 502. Both AIs hold three courts by turn 600 (the saturation reading S(3) ≥ 0.95 stops
  a fourth) and read "law-giving chiefdom". Pinned turn-exactly for seed 42 by
  `LawCourtAiWorldAttributionTests.AiWorldSeed42_IdenticalToTheTwinUntilTheFirstCourtOrdersDelivery_…` (the attribution
  control: byte-identical before the delivery, different from it on).
- **Current value, the integrated M6 tree (2026-10-28):** seed 42's first court order is stamped at turn **507** (CIV's
  tree: 506), so the worlds first differ at state 508 and the court is founded at 509 (stamp + 2, asserted). Cause: D-066
  A18, the AI's known-terrain valuation, which moves the AI's foundings and its research trajectory with them; with KNOW
  off the stamp is still 507 (stated in the re-pin comment, not asserted by a test). Control: with only A18 off (`TestConfigs.PreAiKnownTerrain`) the stamp is 506
  (`LawCourtAiWorldAttributionTests.FirstCourtOrder_WithTheKnownTerrainValuationOff_IsStampedAt506`). The test does not
  assert the turn law_code completes on the integrated tree, and the seed-1 figures and the turn-600 court count above
  were not re-measured there. This re-pin is recorded on the test (`LawCourtAiWorldAttributionTests.cs`), not in the
  golden-attribution register.

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

## Amendment log — 2026-10-28 (D-067)

Authority: `docs/d067-m6-exit-gate-director-rulings.md` R1 (A1, A2, A10 CONFIRMED), R4 (A6), R5 (A13) and
§7 ("status CONFIRMED, with stale facts corrected"). This ADR was uncommitted and not yet confirmed, so stale FACTS
were corrected in place; no design decision is changed. Line numbers marked "was" are those of the staged pre-amendment
text; evidence was read from the live tree on 2026-10-28. D-067 is cited by ruling and section, not by line: it is untracked and was being amended concurrently.

| # | Where | Old | New | Evidence |
|---|---|---|---|---|
| 1 | Status line (was :3) | "Status: ADOPTED UNDER DELEGATION (D-066 A1, A2, A6, A10, A13; M6 closure track CIV, 2026-10-28; uncommitted; Director confirmation requested at the exit gate)." | "Status: ACCEPTED — the Director CONFIRMED D-066 A1, A2 and A10 (D-067 R1), A6 (D-067 R4, with its correction …) and A13 (D-067 R5), 2026-10-28." The original adoption is kept as history. | D-067 §1 (R1, R4, R5) |
| 2 | Status paragraph (was :5) | "A13 (criterion 8 scope)" | "A13 (the government-form criterion's scope: m6-spec §5 criterion 3, per D-067 R5)" | D-067 §1 (R5); `docs/m6-spec.md:308` (criterion 3: "government forms are Available only (M8 adopts)"), `:317` (criterion 8: content validation) |
| 3 | Status paragraph (was :5–6) | "the Director is asked to confirm at the exit gate, as D-066 §2 requires." | "the Director confirmed them at the exit gate (D-067 R1, R4, R5), as D-066 §2 required." | as #1 |
| 4 | §6 second bullet (was :113–115) | AI-world `S1cTurn300 ac4554bf…` presented as the current pin | Kept as the CIV-tree measurement; added: on the integrated tree the AI-world pin is `5458cb3d…`, moved by ADR-058 + D-066 A18, not by the court; `ac4554bf…` survives as `PreA18Turn300` | `Sim.Tests/Kernel/AiFoundingAttributionTests.cs:129, :151`; `docs/m6-plan/golden-attribution-register.md:64, :74` |
| 5 | §6 third bullet (was :116) | "MEASURED beyond the pins (canonical world, …)" | Labelled as the CIV track's own tree (base + CIV), historical, superseded for seed 42 by the integrated value; its figures (506 / 507 / 508; seed 1 500 / 500 / 501 / 502) are kept | `Sim.Tests/Systems/LawCourtAiWorldAttributionTests.cs:13–16` (the class summary records CIV's 506) |
| 6 | §6, new bullet after the pinned-test sentence (was after :121) | AI first court order turn 506, worlds differ at 507, court founded 508 (as current) | Current integrated value: first court order stamped **507**; worlds first differ at state 508; court founded at 509 (stamp + 2). Cause D-066 A18; control `FirstCourtOrder_WithTheKnownTerrainValuationOff_IsStampedAt506` | `LawCourtAiWorldAttributionTests.cs:46–52` (`Assert.NotEqual` at `stamp + 1`; re-pin comment 506 → 507 attributing D-066 A18; `Assert.Equal(507, stamp)`), `:55` (`FoundedTurn == stamp + 2`), `:60–78` (control, `Assert.Equal(506, stamp)`) |

**Attribution note.** The 506 → 507 re-pin is attributed to D-066 A18 by the test's own re-pin comment and control
(`LawCourtAiWorldAttributionTests.cs:49–51`, `:60–78`). `docs/m6-plan/golden-attribution-register.md` does NOT record it
(no entry for the court; checked 2026-10-28), so the test file is the only record. "KNOW off still gives 507" is stated in
that comment and not asserted by a test. Not measured on the integrated tree: the turn law_code completes, the seed-1
figures and the turn-600 court count.
