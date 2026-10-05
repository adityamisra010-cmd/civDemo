# D-048 — KNOWLEDGE-PERSISTENCE RULINGS (revolt, annexation)

**Status:** DIRECTOR RULINGS, **RATIFIED 2026-10-04** (M5 closure instruction, item 4).
**Number:** D-048 was checked free on every local and remote ref before it was minted here.
**Supersedes nothing by rewriting.** `docs/r3-final-reconcile-record.md` keeps its text. Its §2 list of
**INFERRED** implementation choices is superseded by this record, as stated in §3 below and in the pointer note
appended to that record.

## 1. The rulings (RATIFIED, Director 2026-10-04)

1. **Revolt copies completed research only.** The new polity receives a complete copy of the parent's
   `ResearchCompleted` rows at the instant of separation, and nothing else.
2. **Research progress remains with the parent.** In-progress RP is not copied or split.
3. **Eureka credit remains with the parent.** Fired Eurekas and credit provenance are not copied.
4. **The new civilization starts without a capital.** No capital row is invented for it.
5. **A civilization's final settlement cannot revolt away.** A settlement whose controller holds no other place
   does not revolt.
6. **The new civilization researches at the normal rate**, on its own population's RP curve. The 0.25
   city-state pace applies only to uncontrolled settlements (local knowledge holders).
7. **Completed knowledge never decays, and it is never removed by revolt or annexation.** `KnowledgeTransfer.MergeInto`
   is a union: idempotent, and it never deletes a row.

## 2. Where each ruling is implemented (unchanged code; this record changes no behaviour)

| Ruling | Implementation | Tests |
|---|---|---|
| 1, 7 | `KnowledgeTransfer.MergeInto` called by `RevoltSystem` | `KnowledgeMonotonicTests`; `UnrestTests.ARevoltedSeat_BecomesANewAiPolity_HoldingTheCompleteParentKnowledge` |
| 2, 3 | `RevoltSystem` copies `ResearchCompleted` only | `KnowledgeMonotonicTests` |
| 4 | `RevoltSystem` adds no capital row | `UnrestTests.CapitalLoss_ErasesNoKnowledge_ResetsNoResearch_AndLeavesNoTaxSource` |
| 5 | single-settlement guard in `RevoltSystem` | `KnowledgeMonotonicTests` |
| 6 | the new polity is an ordinary `CommandSource.Ai` polity | `KnowledgeMonotonicTests` |

## 3. Supersession

`docs/r3-final-reconcile-record.md` §2, "Implementation choices (INFERRED)", recorded rulings 1–6 as agent
inferences. They are now **RATIFIED** by this record. Ruling 7 restates the R3 ratified monotonic-knowledge rule
together with annexation.

Still **DEFERRED** (not decided here): conquest, full annexation gameplay, capital succession, and recovery of
revolted settlements.

## 4. Ruling 8 — annexation (appended 2026-10-04, M5 playtest baseline)

**RATIFIED (Director, 2026-10-04, M5 playtest baseline instruction §8):**

8. **Annexation, when implemented, merges knowledge by union and never removes completed knowledge.**

Ruling 7 above already says that annexation never removes knowledge. This note states the annexation rule as a
ruling of its own; it does not reword rulings 1–7.

- **Implemented:** the domain operation `KnowledgeTransfer.MergeInto`. It is a union, it is idempotent, and it
  never deletes a row. It is tested directly in `KnowledgeMonotonicTests`.
- **Not implemented:** nothing calls it for annexation. No annexation or conquest path exists in the
  simulation.
- **Annexation gameplay stays DEFERRED.**
- Revolt, which does call the operation (rulings 1–7), is implemented behaviour.

Record: `docs/m5-playtest-baseline.md` §8.

## 5. Revolt Age — pointer (appended 2026-10-05, M5 hardening)

**RATIFIED (Director, 2026-10-05, M5 hardening instruction §8):** a civilization created by revolt **inherits its
parent's CURRENT Age** (never "parent A5 → revolt → child A1 with A5 knowledge"). Rulings 1–7 stand unchanged
alongside it: completed research copied, no progress, no Eureka credit, no capital, the normal research rate; the
final settlement still cannot revolt away. §9 of that instruction restates rulings 1–8 unchanged.

- **Implemented:** `RevoltSystem.InheritAge` (H2, `ece2a7b`): the child's `AgeStateRow` carries the parent's Age
  (read on PREV, the instant of separation) and surge; its entry turn is the child's first turn; no transition is
  logged. Tests: `RevoltAgeInheritanceTests`.
- This closes the baseline's OPEN "new polity at Age A1".

Record: `docs/d049-taxation-and-revolt-model.md` §4.
