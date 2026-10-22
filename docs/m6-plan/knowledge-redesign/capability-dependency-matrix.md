# Capability–dependency matrix

**Status: PROPOSED.** One row per active proposed node (494 technology + 67 civics), generated from the canonical
JSON by `tools/render.py`:

| View | File |
|---|---|
| Main technology tree (trunk) | `matrix/capability-matrix-trunk.md` |
| 1.1 Military | `matrix/capability-matrix-military.md` |
| 1.2 Medicine | `matrix/capability-matrix-medicine.md` |
| 1.3 Engineering | `matrix/capability-matrix-engineering.md` |
| 1.4 Natural Science | `matrix/capability-matrix-natural_science.md` |
| 1.5 Agriculture | `matrix/capability-matrix-agriculture.md` |
| Civics (Tree 2) | `matrix/capability-matrix-civics.md` |

Columns: key · id · prerequisites · capability grants · inputs (resources · geography · infrastructure ·
institutions) · downstream consumers · realization owner · milestone · status · AI valuation · behaviour when inputs are
missing · behaviour when the resource is unknown · player communication · acceptance test · migration.

## 1. Capability stages (vocabulary, `design-framework.md` §5)

knowledge (completion row) → identification (recognize a resource in observed land) → access (reach, claim, traverse)
→ production (recipe or labour activity) → infrastructure (road or structure class) → institutional (institution
founding) → availability (Researched/Available) → adoption/activation (civics, M8) → realization (the owning system
builds or fields it). Grant prefixes: `knowledge:`, `identification:`, `access:`, `production:`, `building:`,
`infrastructure:`, `institution:`, `unit:`, `policy:`/`regime:` (deferred entity kinds, KD-21), `trade:`, `tax:`,
`stage:`, `age:`.

## 2. Package-wide defaults (shown as "default" in the generated rows)

| Field | Default text |
|---|---|
| missing inputs | No physical input: the knowledge is held polity-wide once complete. / Eligible but not realizable where an input is absent; the blocked action names it. |
| unknown resource | n/a / Knowledge only: no deposit is identified or targeted until the polity has located and identified it. |
| AI valuation | Valued through the goal closures and consumers it unlocks; never from unknown cells. |
| player communication | Research panel shows what it makes eligible and which system realizes it; locked actions name the missing prerequisite. |
| acceptance test | covered by the lane's packet acceptance (`implementation-roadmap.md`) |

Existing nodes whose lane did not restate the block carry a block auto-filled from the CI-checked unlock audit
(`docs/research-gameplay-unlock-audit.md` §5) — marked `autoFilled` in the JSON.

## 3. Ownership summary (MEASURED from the proposal JSON at integration)

Status: realized-today (a live consumer exists), knowledge-only (no realizing owner; justified), deferred (owner named
in a later milestone). Owners by milestone: existing systems (ProductionSystem, ConstructionSystem + InstitutionsSystem,
RoadDevelopmentSystem, TradeArbitrageSystem, GovernanceSystem, AgeTransitionSystem, ResearchSystem); M6 (fog/discovery
contract, T6.11 institutions); M7 (battle layer — units); M8 (adoption — regimes, policies, diplomacy); M9 (society —
disease, class, culture); M10+ (era expansions: energy, deterrence); M11+ (depth and content). Counts per status are in
`validation-report.md` and the generated matrix files.

## 4. Checks applied to every row (`tools/validate.py`)

Coherent prerequisites (resolved, acyclic, causal per lane review, chronology V21), explicit resource requirements in
`inputs`, world state separated from research (no grant reveals or places a deposit — reviewed by lanes D02/D11 and the
fog proposal), an assigned owner and milestone (V09), milestone fit (framework §10), invariant compatibility (trunk
closure, Age monotonicity, loader rules V18), migration notes on every changed record, AI and UI fields present.
