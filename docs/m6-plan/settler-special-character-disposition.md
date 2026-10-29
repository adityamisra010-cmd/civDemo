# Settler, hero and special-character progression: disposition (M6.11; T6.12)

**Status: M6 deliverable, 2026-10-28, under D-066 R2.** This is a design note only. It adds no class, mechanic, action,
state or content (task register M6.11: "states the gap; proposes nothing unsupported"). It is the honest
"no non-military example" of the vertical slice (D-P item 5; D-066 A2: "'no non-military example' recorded honestly").

## 1. Finding: there is no carrier to progress

| Candidate | What exists in the tree | Why it cannot progress in M6 |
|---|---|---|
| **Settler** | An **instantaneous order**: `FoundSettlement`, kind 9 (`Sim.Core/Kernel/OrderLog.cs`, ADR-036). The party is drawn from the source's buckets when the order is consumed, and the settlement row appears in the state of t+1. | No settler unit or agent exists between issue and founding. Nothing persists that could hold progression. |
| **Hero / special person** | Ratified as a design: D-043 B3, "temporary MobileAgents … predefined skills … finite lifespans". Not built. | The person-vs-catalogue conflict is **OPEN**: CR-017 §7 (D-010 notables and Law 1 against D-043 B3), recommendation option A ("a notable with a catalogue identity"), unruled. The unit charter escalates it as **Q4** to M7.0. |
| **Notable** (the population face of a special person) | `NotableRow` and `NotableLifecycle` exist. **No system writes `Notables`.** Rows enter only through snapshot load (`CanonicalSchema`); `PathBuildSystem` only reads them. | It has no writer, so it has no lifecycle that progression could attach to. |
| **Builder** | Nothing. Construction is city-level settlement labour (`ConstructionSystem`, `RoadDevelopmentSystem`). | Unit charter §8.3: "No Civ-style builder unit with charges", because it would double-count labour. A guard test asserts that no Builder or worker type exists ([unit-progression-contract.md](unit-progression-contract.md) §2). |

## 2. Where each one is owned

- **Settlers.**
  - Unit charter §8.2: one agent type for all Ages; "Its capacity, speed and range change with transport (roads, boats,
    rail) rather than by identity".
  - Charter §14 (M7.5) records "settler transit is deferred (not approved)".
  - Even as a future MobileAgent, a settler has **no progression line**: its capability improves through the transport
    network, not through experience. The H7-11 owner is M7/M8, by Director scope decision.
- **Generals and admirals** (unit charter §9.1):
  - the command role is an M7 candidate; the traits catalogue is M8+;
  - their capability grows with institutions (`prestige_leadership` → `standing_army` → `military_academies` →
    `general_staff`), not with a hero XP bar;
  - they wait on the Q4 ruling.
- **Heroes** (unit charter §9.2): design-only. They are recommended only after special people and culture are live
  (M11+).
- **Artists, writers, musicians, cultural groups** (unit charter §9.3–§9.6, §14): the M9 special-people system.

## 3. What M7/M8 must create before any of them can progress (in order)

1. **The Q4 / CR-017 §7 ruling.** It settles whether a special person is a notable from the population with a catalogue
   identity (option A) or an abstract agent (option B). It is owed at M7.0 (unit charter §14).
2. **A writer for `Notables`.** This is the extraction and return lifecycle (M4 R-1, `Ledger.Transfer`) that option A
   builds on.
3. **The D-043 B1 MobileAgent table** (agent-generic position, action state, AC). H7-10 records that M7 creates the
   first MobileAgent, since M6 has no scout class (D-J; D-062).
4. **For settlers only, if ever ruled:** a settler agent with transit on M7's movement (H7-11). Even then, no
   progression: see §2.
5. **Only then,** per class:
   - the skill or charge model of D-043 B3 / D-047 rulings 15–16 (finite charges, never a permanent buff: Law 2);
   - its persistence: the next free schema version, a populated-table test, a replay twin.

## 4. What M6 does instead

- The military class that exists, army formations, gets inert promotion content and a dormant training contract
  ([unit-progression-contract.md](unit-progression-contract.md), [training-ap-contract.md](training-ap-contract.md)).
- The vertical slice shows that promotion inert on the unit card, "earned in M7" (D-066 A2 item 4).
- No non-military progression example is invented. The slice records this gap here, as D-P item 5 requires.

## References

- D-066 R2, A2 (`../d066-m6-closure-rulings.md`).
- D-P item 5 (`m6-decision-register.md`).
- ADR-036.
- D-043 B1, B3 (`../d043-civilization-progression-ages-and-mobile-agents.md`).
- CR-017 §7 (`../adr/cr-017-ages-surge-and-mobile-agents-vs-frozen-items.md`).
- Unit charter §8.2, §8.3, §9, §14, §16.4 Q4 (`../m7-plan/unit-catalogue-and-progression.md`).
- H7-10, H7-11 (`m7-handoff-register.md`).
