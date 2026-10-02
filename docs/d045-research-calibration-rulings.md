# D-045 — Research calibration rulings (2026-10-01)

**Status:** Director rulings, recorded. Implemented on branch `research-progression-foundation` (PR #10, not merged).
**Relationship:** follows **D-044** (2026-09-30), which is NOT rewritten. Where a D-045 ruling replaces a D-044-era
provisional choice, Part C names the earlier choice and where it is recorded. ADR-029 is the implementation contract;
CR-018 records the one conflict with a frozen law found while implementing.

**Labels used in this record and in ADR-029:**
- **RATIFIED** — a Director ruling (D-044, D-045) or a frozen document.
- **PROVISIONAL CALIBRATION** — numbers chosen to meet a ruled target; replaceable without a ruling.
- **FUTURE / OPEN** — not decided; a Director decision is required.

**Clause labels:** Part A's numbered sections are cited as **D-045 §1 … §15**.

---

## PART A — THE RULINGS (verbatim)

```
DIRECTOR RULINGS

# 1. STARTING KNOWLEDGE

A newly founded civilization starts with:

    0 completed Technology/Knowledge nodes.

This is intentional.

However:

    0 completed research nodes
        !=
    0 gameplay capabilities.

Baseline capabilities exist outside the research graph.

At minimum the starting civilization can have:

    - Settlers / settlement founding
    - Scouts / exploration
    - Clubmen or equivalent basic military
    - Basic food/resource gathering
    - Basic internalized construction
    - Other minimum capabilities required for playable founding

There is NO Builder unit.

Construction is an internal civilization process:

    construction capacity
        -> project allocation
        -> construction progress

Do NOT create a Builder MobileAgent.

Do NOT create fake research nodes merely to represent:
    - settlement founding
    - basic scouting
    - basic clubmen
    - construction
    - basic food gathering

IF the current implementation already handles baseline capabilities correctly:
    preserve it.

IF it currently depends on fake completed research nodes:
    replace that dependency with the smallest explicit baseline-capability representation required.

Do not redesign unrelated starting gameplay.

# 2. RESEARCH CAPACITY

Research capacity is primarily population-derived.

It is sublinear/logarithmic in character:

    larger population
        -> more total research
        -> less research per additional person

Director calibration anchors:

    Population 100
        -> 2 RP / turn

    Population 1,000
        -> 10 RP / turn

For the current implementation, use this provisional calibration function:

    RP(P) = 2 * (P / 100)^0.69897

This gives:

    100 -> 2
    1000 -> 10

IMPORTANT:

This mathematical curve is PROVISIONAL CALIBRATION.

Do NOT present the exponent as permanently ratified architecture.

Do NOT preserve the old:

    coefficient × adults^0.5

formula.

Do NOT add literacy, education, universities, health, connectivity,
institutional maturity, etc. to the base population formula yet.

Those will become future modifiers once their systems exist.

Add tests for:

    100 -> 2
    1000 -> 10
    monotonicity
    sublinear growth

# 3. TECHNOLOGY COSTS

The existing prerequisite-depth-derived costs are NOT acceptable as
the final content model.

The corpus contains 424 Technology nodes.

Every node must receive an intentional research cost.

Do NOT simply calculate:

    cost = prerequisite depth × constant

and call the result final.

Prerequisite structure can influence cost, but cost must primarily represent
the magnitude of the actual technological advancement.

Consider:

    - conceptual novelty
    - technical difficulty
    - breadth of capability
    - historical significance
    - prerequisite complexity
    - material sophistication
    - institutional sophistication
    - whether this is incremental or a major breakthrough
    - whether this belongs to a genuine technology generation
    - whether the resulting capability is narrow or civilization-wide

Do not mechanically make every later-generation technology more expensive.

Do not make every sibling node identical unless their actual magnitude warrants it.

COST TIERS

Introduce a deterministic, inspectable cost-calibration scheme.

The corpus should be auditable so a reviewer can understand why a node
has its cost.

Use a finite set of meaningful cost bands or an equivalent explicit
calibration representation.

The exact numeric bands are yours to derive from the corpus, but they
must satisfy the pacing target below.

Do NOT invent arbitrary costs independently for 424 nodes without
recording the rationale/category that generated them.

Prefer:

    content significance
        -> calibrated cost band
        -> node cost

over:

    graph depth
        -> node cost

# 4. FINAL-AGE PACING

This is a hard gameplay calibration target.

The finite meaningful research content of the final Age must provide:

    approximately 150-250 turns

of actual research workload even for an exceptionally well-developed,
research-optimized civilization.

Use approximately:

    200 turns

as the central target.

150 turns is the lower acceptable design bound.

250 turns is the upper design envelope.

This does NOT mean every civilization must finish in 200 turns.

A poorly developed civilization should take longer.

A highly optimized civilization should approach the lower portion
of the range, but should not trivially exhaust the finite final-Age
technology corpus.

The calculation must account for:

    - population-derived research capacity
    - Eurekas
    - technology prerequisites
    - technology costs
    - future university specialization
    - future research-efficiency modifiers

Where future systems cannot yet be simulated, document the assumption
used for the calibration.

DO NOT solve the pacing problem by simply making every final-Age node
enormously expensive.

The overall Age workload matters.

CALIBRATION METHOD

Create a deterministic calibration/reporting tool or script if one does
not already exist.

It should be able to report at minimum:

    - total cost by Age
    - node count by Age
    - average/median cost by Age
    - minimum/maximum cost by Age
    - total finite research workload
    - estimated turns under specified population/RP assumptions
    - effect of Eureka credit under the current model
    - research-optimized pacing estimate

The report must make it possible to identify whether the final Age falls
inside the 150-250 target.

Do not hide calibration assumptions.

# 5. EUREKA CREDIT

Eureka credit is now:

    40% of BASE technology cost

for a fully satisfied Eureka.

This replaces the previous provisional 25%.

Example:

    Base cost = 100

    Full Eureka = 40 research credit

Eureka credit:

    - is independent of active research target
    - applies to the specific technology
    - is capped at remaining cost
    - never overflows to another technology
    - is deterministic
    - cannot be repeatedly awarded for the same satisfied condition

# 6. PARTIAL EUREKAS

A Eureka may contain multiple independently satisfiable conditions.

A fully satisfied Eureka still provides:

    40% of base cost

If there are multiple equally weighted conditions:

    total Eureka credit / number of conditions

is awarded for each newly satisfied condition.

Example:

    Cost = 100
    Eureka has 4 conditions

    condition 1 -> +10
    condition 2 -> +10
    condition 3 -> +10
    condition 4 -> +10

Therefore:

    1/4 -> 10%
    2/4 -> 20%
    3/4 -> 30%
    4/4 -> 40%

IMPORTANT:

Do NOT assume all future Eurekas are necessarily equally weighted.

The data model must support:

    condition
    weight / credit fraction

If no explicit weight exists:

    equal weighting

If explicit weights exist:

    normalize them deterministically.

Total credit for one Eureka may never exceed:

    40% of base technology cost.

IDEMPOTENCE

A satisfied Eureka condition can award its credit only once.

If the condition remains true for 20 turns:

    it does NOT award credit 20 times.

If a second condition becomes true later:

    award only that condition's previously unawarded credit.

The state/query model must allow:

    0/N
    1/N
    ...
    N/N

conditions satisfied and credited.

AMBIGUOUS PROSE

Do NOT fabricate machine-evaluable conditions from ambiguous Eureka prose.

If a Eureka cannot safely be decomposed:

    classify it as requiring authoring / future interpretation.

Preserve the original text.

# 7. EUREKA CORPUS AUDIT

Audit all 801 existing Eureka strings.

Classify each as:

    A. Machine-evaluable single condition
    B. Machine-evaluable multi-condition
    C. Requires future system
    D. Implied by prerequisites
    E. Dead/impossible
    F. Ambiguous / requires authoring

Do not silently discard category F.

Retain the reason for every non-machine-evaluable Eureka.

The existing:

    implied-by-prerequisites

classification remains authoritative.

The invariant remains:

    no evaluable node-knowledge Eureka may be implied by the
    node's own prerequisites.

Keep the existing generator/content tests and extend them for the
new classification and partial-Eureka representation.

# 8. UNIVERSITY RESEARCH EFFECT

DO NOT IMPLEMENT the University institution system in this task.

We only need the research-engine seam.

The eventual model is:

    specialized university
        -> specialized research capacity
        -> lower effective research cost for relevant technologies

Examples:

    Medical University
        -> Medicine technologies

    Engineering University
        -> Engineering technologies

    Agriculture University
        -> Agriculture technologies

etc.

The university effect should eventually be:

    civilization-wide research benefit

with physical/local institutional properties determining its strength.

It should NOT be:

    University count × flat permanent percentage.

The intended realistic model has:

    - institutional maturity
    - diminishing marginal returns
    - specialization
    - researcher/personnel capacity
    - local institutional ecosystem
    - knowledge spillovers

A newly founded university should not immediately perform like a mature
research institution.

The research engine should therefore retain a clean seam conceptually
equivalent to:

    BaseCost
        -> research modifiers
        -> EffectiveCost

But:

    DO NOT build UniversitySystem
    DO NOT create university maturation logic
    DO NOT invent institution data
    DO NOT hard-code arbitrary university bonuses

unless required solely to preserve the interface.

Document this as a future institutional modifier.

# 9. TECHNOLOGY GENERATIONS

Do not force every technology family into a fixed number of generations.

A family may have:

    one meaningful technology

or:

    multiple genuine successive advancements.

Example:

    Antibiotics I
    Antibiotics II
    ...
    Antibiotics VI

is valid if the corpus contains real successive advances.

But do NOT manufacture generations merely to create more nodes.

Generation metadata must not automatically determine cost.

# 10. FINITE VS RECURSIVE RESEARCH

Distinguish:

    finite meaningful technology nodes

from:

    future recursive/repeatable research.

The 150-250-turn final-Age target applies to the finite meaningful
research corpus.

Recursive research is the long-tail after meaningful finite research
is exhausted.

Do NOT redesign recursive research in this task unless the existing
implementation requires a minimal classification field.

The current idempotent completion behaviour should remain until the
recursive-research architecture is separately ratified.

# 11. TECHNOLOGY VS TECHNIQUE

Do not collapse Techniques into Technology nodes merely to make the
corpus easier to balance.

Maintain the architectural distinction:

    Technology
        = discovered knowledge / technological capability

    Technique
        = practical method of applying knowledge

Do not implement the Technique system yet.

However, flag obvious cases during the corpus audit where a node may
actually belong to Techniques rather than Technology.

Do not move such nodes without Director approval.

# 12. NO SILENT ARCHITECTURE DECISIONS

Do not silently resolve remaining Director-level questions.

If you encounter something that requires a new architectural ruling:

    document it as OPEN

rather than choosing a permanent implementation rule.

In particular, do not silently decide:

    - final CLP modifiers
    - university formulas
    - institutional maturity formulas
    - foreign-knowledge research bonuses
    - recursive research formulas
    - Age-transition research effects
    - milestone placement
```

Sections 13–15 of the instruction (required output, validation, Git/PR) are process instructions for the
implementing session, not rulings; they are satisfied by the PR, not recorded here.

---

## PART B — HOW EACH RULING IS IMPLEMENTED (and its label)

| § | Ruling | Implementation (ADR-029) | Label |
|---|---|---|---|
| 1 | Zero starting nodes; baseline capabilities outside the graph; no Builder | Founding writes no research row (unchanged). `research.json` gains a `baseline` list (six capabilities, each naming the system that provides it or saying none simulates it yet); `ResearchQuery.BaselineCapabilities`. No baseline dependency on research existed, so none was replaced. No Builder unit or MobileAgent was created | RATIFIED rule; the list's wording is implementer text |
| 2 | RP(P) = 2·(P/100)^0.69897, anchors 100 → 2, 1000 → 10 | Two-anchor power law in `research.json` tuning; P = the polity's total population; replaces coefficient × adults^0.5 (§6) | Anchors RATIFIED; curve PROVISIONAL CALIBRATION; per-turn reading OPEN (CR-018) |
| 3 | Intentional, auditable, significance-based costs for all 424 nodes | Bands M1–M5 per node with recorded rationale (`scripts/research-calibration/nodes.json`); cost = band weight × Age unit (§2.4) | Method RATIFIED; band weights and Age units PROVISIONAL CALIBRATION |
| 4 | Final Age ≈ 200 turns (150–250) for a research-optimized civilization | `scripts/research-calibration-report.py` → `docs/research-calibration-report.md`; the final-Age unit is derived from the target | Target RATIFIED; scenario assumptions PROVISIONAL CALIBRATION |
| 5 | Full Eureka = 40 % of BASE cost; target-independent, capped, no overflow, deterministic, once | `eurekaFullCreditFraction` 0.4 of `BaseCost`, capped at the remaining EffectiveCost (§7) | RATIFIED |
| 6 | Partial Eurekas: weighted conditions, normalized, each credited once; 0/N…N/N queryable | One Eureka per node; its strings are conditions; optional `weight`; B strings carry `parts`; one state row per credited condition; `EurekaProgressOf` | RATIFIED rule; "the node's strings are ONE Eureka" is an implementer reading (OPEN-6 below) |
| 7 | Audit all 801 strings into A–F with reasons | `category`, `status`, `reason`, `futureSystem` per string; audit counts A 93 · B 0 · C 419 · D 21 · E 0 · F 268 | RATIFIED |
| 8 | University seam only | Unchanged `ResearchCostModifiers` input table with no writer; documented as a FUTURE institutional modifier | RATIFIED (seam); formula FUTURE |
| 9 | Generations do not set cost | Bands come from significance; `generation` is display metadata only | RATIFIED |
| 10 | Finite vs recursive | The 10 repeatable-descriptor nodes are the recursive-research candidates; the final-Age target is measured on the finite corpus; they still complete once | RATIFIED; recursive formula FUTURE |
| 11 | Technology vs Technique; flag, do not move | 38 nodes flagged `techniqueCandidate` with a reason (audit section); none moved | RATIFIED |
| 12 | No silent architecture decisions | Part D | RATIFIED |

---

## PART C — EARLIER CHOICES THIS RECORD REPLACES (originals not rewritten)

| Earlier choice | Where recorded | Replaced by |
|---|---|---|
| CLP = coefficient × adults^0.5 (R-4, provisional) | ADR-029 §6, §13 R-4 (as of `82da12c`) | D-045 §2 |
| Eureka credit = 0.25 × EffectiveCost per string (R-5) | ADR-029 §7, §13 R-5 | D-045 §5–§6 |
| BaseCost = round10(1000 × 1.12^depth) (R-10) | ADR-029 §2.4, §13 R-10 | D-045 §3 |

---

## PART D — OPEN (Director decision required; nothing below was decided silently)

1. **Per turn vs per sim-year (CR-018).** The anchors are "RP per turn"; frozen law 3 requires rates per sim-year
   integrated over dt. Implemented reading: RP(P) is the yield of a 10-year reference turn (the campaign-start dt), so
   the anchors hold literally at the campaign start; a dt-0.5 final-Age turn yields RP(P)/20. Options in CR-018.
2. **Completion overflow (ADR-029 R-2).** CLP past a completion is lost, so every node costs at least one turn; the
   final Age's 116 finite nodes cannot take fewer than 116 turns at any cost. Whether overflow should carry to the
   next target is a Director question (it touches D-044 R20-D, "no general bank").
3. **Future research modifiers** (literacy, education, universities, health, connectivity, institutional maturity,
   foreign knowledge): no formula is chosen; the calibration assumes ×1.5 for a research-optimized civilization.
4. **Age → calendar / dt mapping** (architecture §12.3): the calibration's Age pacing windows are targets for the
   cost scale, never gates.
5. **Recursive research** formula and the 10 repeatable descriptors.
6. **What one corpus "Eureka" is.** Implemented: a node's corpus strings together form its one Eureka, each string a
   condition of equal weight, so a node's full 40 % needs every string satisfied; unevaluable strings hold their share.
   The alternative (each string its own 40 % Eureka) would let a node collect up to 160 %.
7. **Technique candidates:** 38 flagged nodes await the Director's decision; none was moved.
8. **Milestone placement (CR-005)** remains as recorded in D-044 Part D T1.

---

## PART E — SUPERSESSION NOTE (append-only, 2026-10-01; Parts A–D are not rewritten)

The research corpus finalization and the research-foundation gate (**D-046**; ADR-029 addendum A; ADR-030) change how
several D-045 rulings are implemented. The D-045 text stands as the record of what was ruled on 2026-10-01 morning.

| D-045 | Now |
|---|---|
| §1 zero starting nodes, baseline outside the graph | Unchanged. The `baseline` list is kept; its ownership against the corpus is classified in `docs/design/research-capability-ownership.md` (D-046 G3) |
| §2 anchors 100 → 2, 1,000 → 10 | Anchors unchanged; the per-turn reading is ruled (ADR-030): RP per turn = 0.08 × P^0.699, never × dt. Part D item 1 (CR-018) is resolved |
| §3 significance-band costs, §4 final-Age Age units | Replaced by content-derived costs, BaseCost = U × K^magnitude with per-node factors and no calibration adjustment (D-046 F§1). The banded calibration (`scripts/research-calibration/nodes.json`) and its generator are retired; the D-045 report is kept as `docs/research-calibration-report-d045.md` and its pacing is obsolete |
| §5–§6 Eureka credit, partial Eurekas | 40 % of BASE survives as the ceiling of ONE acceleration pool shared with foreign exposure; credit is weight × BaseCost per condition, once (D-046 F§2) |
| §7 audit of 801 strings | Replaced by 81 curated Eurekas on 80 nodes. The D-045 audit is kept verbatim as `docs/research-corpus-audit-d045.md`. Part D item 6 (what one corpus "Eureka" is) is moot |
| §8 university seam | Unchanged (input table, no writer); EffectiveCost gains the 20 % floor |
| §10 finite vs recursive | The ten are the named recursive set, available per subtree; repeat mechanics DEFERRED (D-046 G4) |
| §11 technique candidates | Unchanged: 38 flagged, none moved; carried forward in `docs/research-corpus-audit.md` §7 |
