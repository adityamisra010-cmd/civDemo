# M5 IMPLEMENTATION INVENTORY — the governing loop

Branch `m5-full-build`. **Not merged to `main`.** Schema v25 is reported for a
ruling in `docs/m5-schema-v25-migration.md` and is not accepted until the director
says so.

---

## §1 WHAT THE LOOP IS

Four quantities, each one derived from the one before it, closing back on the
first:

```
declared rate ──> administrative reach ──> effective rate ──┬──> extraction ──> output
      ^                (from the capital, by travel cost)   │
      │                                                     └──> burden ──> happiness
      │                                                                        │
      └──────────────── AI eases the levy ◀── legitimacy ◀─────────────────────┘
```

Nothing in that chain is a stored intermediate. `TaxPolicyRow` is the only new
state in the milestone, and it holds the legislative act itself — the rate an
Empire declared — because nothing else in the world records what a government has
decided.

## §2 WHAT EXISTS, FILE BY FILE

| file | what it is |
| --- | --- |
| `Sim.Core/State/WorldState.cs` | `TaxPolicyRow(PolityId, double Rate)` — one row per polity. Absent row = untaxed. |
| `Sim.Core/Kernel/CanonicalSchema.cs` | v25: the table appended last, 12 bytes a row. |
| `Sim.Core/Kernel/OrderLog.cs` | `OrderKind.SetTaxRate = 5`; payload is a percentage in [0,100]. |
| `Sim.Core/Kernel/OrderValidation.cs` | an Empire legislates its OWN taxes — actor must equal target. |
| `Sim.Core/State/Governance.cs` | the derived readers: nominal rate, administrative reach, effective rate, extraction multiplier, legitimacy. Pure functions; no state. |
| `Sim.Core/Systems/Governance/GovernanceSystem.cs` | enacts the orders; writes `ControlRow.Strength` = administrative reach. Calls no `Ledger` method. |
| `Sim.Core/Systems/Production/ProductionSystem.cs` | the extraction multiplier on farm, deposit and crafting output. |
| `Sim.Core/State/SettlementHappiness.cs` | the burden multiplies the aggregate. |
| `Sim.Core/State/AiGovernance.cs` | D-021 valve 6 — the AI eases the levy as legitimacy falls, through the player's own order kind. |
| `Sim.Ui/ViewModel/TaxOrderFactory.cs` | the edict, and the panel's three numbers. |
| `Sim.Ui/UiSession.cs` | `EmitTaxOrder`; the AI's orders appended to the log before each step. |

## §3 THE DECISIONS WORTH ARGUING WITH

**Reach is `exp(−travelCost / decay)` from the capital.** The shape is not
invented for M5: it is migration's own distance damping, and the decay constant is
carried across by reference class (`migration.dampingDecayCostUnits = 25.0`)
rather than tuned to make an outcome come out. The capital reads exactly 1.0; a
settlement with no route reads 0.0. There is no calendar term and no era term —
capability comes from computed state (law 4).

**Effective rate = nominal × reach, and ONE number feeds both consumers.** The
same quantity is the compelled effort production reads and the burden happiness
reads, so the gain and the cost cannot be tuned apart. A frontier the collectors
cannot reach is neither taxed nor resented.

**Extraction is a coefficient, `1 + response × rate`.** With no policy the rate is
0 and the multiplier is exactly 1.0 — bit-identical output, which is what lets
every pre-M5 pinned world be recovered exactly. It is inside the resolution
equation, which law 2 permits; it is not a free-floating buff.

**The burden MULTIPLIES happiness rather than joining its CES aggregate**, and
that is a correction the suite forced rather than a preference. As a third factor,
an untaxed realm scored 1.0 on it and lifted the floor-anchored aggregate off its
floor: an unfed, unhoused, untaxed settlement read **2.31 instead of 0**, silently
disarming the revolt condition D-021 requires M5 to ship working. As a multiplier,
an untaxed realm scales by exactly 1.0, a taxed one is felt at every level of
provision, and total deprivation lands on exactly 0 at any rate. Two regression
tests pin both halves. It is emphatically not `happiness -= taxRate`.

**Legitimacy is population-weighted mean happiness over what the polity controls**,
so it is a reading of the realm rather than a stock, and an extinct polity reads
0.0. Nothing accumulates it and nothing can grant it.

**Grievance is not inert.** The chain tax → happiness → legitimacy → AI response
was checked by mutation: deleting `TaxSufficiency` fails three tests including
`LegitimacyFallsWhenTheRealmIsTaxedHarder`; deleting the extraction multiplier
fails two.

## §4 WHAT IS DELIBERATELY ABSENT

No treasury. No `TaxReceiptRow`, `TaxTransferSystem` or `EmpireInventory`. No
city-level economic ownership. No second happiness. No universal
`CapabilitySystem`, no technology tree, no M6/M7/M8 mechanism. The turn is still
ten years. The UI mutates no simulation state and every player and AI action goes
through the order pipeline.

## §5 THE HONEST LIMIT

**A founded world holds one Empire and it is the player's — measured: player=1,
ai=0.** The AI valve is implemented, tested end-to-end through the real order
pathway, and wired into the session, but there are no AI Empires for it to drive
in a played session yet. Founding them is the configurable AI-count seam the
director ruled must not be hard-coded, and it is not this packet's scope. The UI
test states which world it is running in rather than passing on an empty loop.

## §6 EVIDENCE

- `Sim.Tests` 661 passed / 6 skipped / **2 failed — the two already-quarantined
  Malthus corridor cases**, unchanged by this work and failing identically before it.
- `Sim.Ui.Tests` 160/160.
- `check-banned-constructs.sh`, `check-read-isolation.sh`, `check-readonly-proof.sh`
  all pass.
- Four goldens move; every movement is decomposed by a control arm and recorded at
  the pin. See `docs/m5-schema-v25-migration.md` §"Blast radius".
- `GovernanceTests` 25 tests; `TaxControlTests` 8.
