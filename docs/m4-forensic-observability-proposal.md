# M4 Forensic Observability — Proposal

**Status: PROPOSAL ONLY. Nothing has been implemented.**
**Candidate: `7c91cbf` on `t4.19-glass-box` (62 ahead / 0 behind `main` = `dbef61a`; merge-base == `dbef61a`).**
**This document is written on branch `m4-forensic-proposal`, cut from `7c91cbf`. It changes nothing under `Sim.*`.**

---

## Provenance of every claim in this document

Per `CLAUDE.md` "Repository freshness", a previous agent's report is **secondary evidence**. Five audit
lanes, two designs and one adversarial challenge fed this proposal. I re-verified the load-bearing claims
myself against the tree at `7c91cbf` before writing. Citations are marked:

- **[V]** — I opened the file at `7c91cbf` in this session and read the cited lines.
- **[L]** — carried from a lane, a design or the challenge. **Must be re-verified at implementation time.**

Verified by me in this session **[V]**: `Sim.Core/State/SettlementHappiness.cs` (60–242) ·
`Sim.Core/Systems/NeedsGrievance/NeedsAggregation.cs` (118–184) · `Sim.Core/Kernel/SessionManifest.cs`
(71–210) · `Sim.Core/Kernel/WorldHash.cs` (whole file) · `Sim.Core/Kernel/CanonicalSchema.cs` (95, 143) ·
`Sim.Core/Observability/Observer.cs` (56–57, 566–597) · `Sim.Core/Observability/TelemetryWriter.cs`
(29, 35, 418–430) · `Sim.Core/Observability/TurnRecord.cs` (126–170) · `Sim.Core/Kernel/TurnExecutor.cs`
(29–49, 84–121) · `Sim.Core/Systems/Consumption/ConsumptionSystem.cs` (190–230, 285) ·
`Sim.Core/Systems/ClassMobility/ClassMobilitySystem.cs` (168–200, 283–295) ·
`Sim.Core/Systems/ClassMobility/Predicate.cs` (28–46) · `Sim.Core/Systems/Migration/MigrationSystem.cs`
(254, 360, 401, 416, 455–475) · `Sim.Core/Kernel/Ledger.cs` (45–55) · `Sim.Core/Kernel/SessionTrace.cs`
(34, 78–92) · `Sim.Core/Chronicle/ChronicleCollector.cs` (1–25) · `Sim.Core/Chronicle/ChronicleProse.cs`
(26–34, 56–66) · `Sim.Core/Systems/PathBuild/PathBuildSystem.cs` (78–98) ·
`Sim.Core/Worldgen/WorldgenConfig.cs` (55) · `Sim.Core/SystemCatalog.cs` (40–62) · `Sim.Ui/UiSession.cs`
(322–327) · `Sim.Cli/Program.cs` (479–497, 543–556) · `scripts/check-read-isolation.sh` (25–68) ·
`docs/observability-architecture.md` (§0 lines 14–34, §7 lines 280–295).

Gates run by me on the candidate in this session: `check-read-isolation.sh` → **OK, exit 0**;
`check-banned-constructs.sh` → **OK, exit 0**.

**Two structural facts I established exhaustively, because everything downstream depends on them:**

1. **`WorldState` declares 39 `public Table<...>` fields, and ALL 39 are named in `CanonicalSchema.cs`.
   There is NO unserialized, non-hashed observation table anywhere in this tree.** Every "just add one
   observational long" therefore means `CanonicalSchema.Version` 24 → 25 and **every world hash in the
   repository moves** — every golden, every corridor pin, the determinism harness, the cross-process job,
   and the director's own recorded playtest traces, which would then read as `REPRODUCTION FAILED` against
   the new build. **[V]**
2. **`docs/observability-architecture.md` §0 permits exactly five field kinds** — READ, SUMMED, DIFFERENCED,
   RESIDUAL, RECOMPUTED — and defines RECOMPUTED as *"a call to a PUBLIC static simulation function on
   stored state — never a private re-implementation"*, closing with *"Nothing else is permitted. If a
   quantity would need a formula the simulation does not expose, it is a **GAP** (§8) and the record says
   so, rather than an observer-side copy of the formula that will drift."* **[V]**

### Two numbering caveats the director must read before §1 and §13

**The literal text of your §1–§15 requirement list, your §15 query list and your §18 test list was not in
my packet, and no document in `docs/` at `7c91cbf` contains them.** The section names below are exactly the
fifteen you gave. But:

- The **28-item table in §1** is built from the item names the five audit lanes cite. **Items 12, 13, 14
  and 15 were never named by any lane.** I have entered them as NOT SUPPLIED with my best reconstruction
  and marked them clearly. Please correct them.
- The **§15 query walk** and the **nineteen §18 test requirements** are reconstructed from your stated
  objective, your worked example and the constitution's standing rules. Every item is stated by *substance*
  as well as by number, so a re-numbering costs a table edit and nothing more.

---

# ⛔ STOP LIST — decisions only the director can take

**Your instruction: "IF A REQUESTED FORENSIC FIELD WOULD REQUIRE CHANGING AUTHORITATIVE GAMEPLAY STATE,
STOP AND REPORT IT rather than designing around it."** This is that report. Nothing below has been
designed around, worked around, or quietly approximated. Each is a ruling you must make.

## A. Requires NEW AUTHORITATIVE SERIALIZED STATE — schema v24 → v25, every world hash moves

| # | Field | What it would cost | What it would buy |
|---|---|---|---|
| **S1** | **Pairwise migration From→To and the amount** (`MigrationLegRow(From,To,Class,CohortIdx,Moved)`) | New serialized table; v25; **every golden, corridor pin, determinism pin and your own playtest traces invalidated** | **Your worked example.** "Where did Gapi's 163 go." Nothing else answers it. |
| **S2** | **Migration channel split** (gap-driven vs famine flight) | As S1, **plus restructuring arithmetic inside a resolution equation** — the two terms are summed inside one parenthesis at `MigrationSystem.cs:401-402, :416-417` **[L]** before any row is written | The one movement distinction the simulation actually makes, made authoritative instead of inferred |
| **S3** | **Per-settlement natural vs starvation deaths** | One `long` on `SettlementVitalsRow`; v25 | "Died of hunger" vs "died of age" per settlement. World level is already exact by ledger reason |
| **S4** | **Colony parentage / party / provisions** (`FoundingRow`) | New serialized row; v25 | Which settlement founded which colony. Today the colony carries no source id and the party moves by an unrecorded `Ledger.Transfer` |
| **S5** | **Appropriation raids** (`RaidRow(Raider,Victim,Units)`) | New serialized row; v25 | Who raided whom for how much. Today the only signal is a world-level boolean |
| **S6** | **Housing built vs decayed per settlement** | Two `long`s on `HousingRow`; v25 | Splits Δdwellings into its two causes |
| **S7** | **Per-settlement per-good sinks** (InputsConsumed / ToolWear / HousingMaterials / ConstructionMaterials) | Four `Last*` fields on `GoodStockRow`; v25 | Your §8 "consumed vs transferred vs lost" **per settlement**. World level is already exact |
| **S8** | **Per-cohort / per-class vitals, cohort aging, class-mobility flows** | New per-bucket rows; v25 | "What each population cohort experienced" as a **flow**, not only a headcount. Class mobility is a **seventh movement category your list does not name and the simulation does not record at all** |
| **S9** | **Order acceptance / rejection at the system boundary** | Consuming systems publish outcome rows; v25 | *"The simulation refused my order and did not tell me"* — precisely the forensic failure you are trying to eliminate. Five silent `continue`/return sites **[V for PathBuild:82,:94]** |
| **S10** | **Per-turn RNG draw counts** | A counter on `RngStreamRow` (canonical-stream field 4); v25 | Which system drew how often. End-state `(State, Inc)` is already free |
| **S11** | **Authoritative reason for a control change** | A `RevoltRow` / `ControlChangeRow`; v25 | The *fact* of the change needs nothing (it is a clean DIFFERENCE). Only the **category** needs state |
| **S12** | **A refusal record inside `orders-<stamp>.bin`** | `OrderLog` refuses any IoVersion but 1 and `Append` enforces nondecreasing turn order **[L]**; both the wire format and the append invariant would move, and binary replay fixtures depend on the format | Nothing that a separate artifact cannot carry. **Recommend: never do this.** |
| **S13** | **A column on `trace-<stamp>.csv`** | `SessionTrace.Parse` demands **exactly 6 columns and throws** (`SessionTrace.cs:83-87`) **[V]** — widening breaks every existing trace file *and* every reader simultaneously, including `sim inspect`'s own divergence check | Nothing. **Recommend: never do this.** Per-turn world scalars belong in JSONL. |
| **S14** | **Anything inside the canonical hash stream** (making the world hash self-identifying) | Changes the hashed bytes of every world; a change to the **FROZEN kernel contract §3.8** | Nothing an observing artifact cannot carry beside the hash for free. **Recommend: never do this.** |

## B. Requires a SIMULATION-SIDE VISIBILITY CHANGE (public pure statics) — outside an observability packet's fence

These are **not** new state and **not** behaviour changes. They are the T4.19-A2 / `NeedsGrievanceSystem`
precedent: extract existing inline arithmetic into public pure statics that `Step` itself calls, prove the
world hash unchanged, and the observer may then legally RECOMPUTE through them. **Each needs its own ruling
and its own hash-equality proof.** Until then, each is a GAP.

| # | Extraction | What it would buy |
|---|---|---|
| **S15** | `SettlementHappiness.Weights(SimConfig, Span<double>)`, a raw-aggregate accessor, `Normalize(double, AggregationTuning)` — from the inline code at `SettlementHappiness.cs:174-176, :183-184, :207-211` **[V]** | **Makes every happiness value other than exactly 0 or 100 verifiable from the artifact.** Today it is not. See §8. |
| **S16** | `MigrationSystem.Damping(travelCost, cfg)` and `Viability(prev, settlement, cfg)` | The **eligible-destination set**. Does **not** give the pairwise flow (that is S1) |
| **S17** | `ClassMobilitySystem.HasPublished` and **`PrevVariable`** — both `private static` **[V: :283, :290]** | Makes the artisan/merchant latch **result** a genuine RECOMPUTED. **Without both, it is not.** See the correction in §7 |
| **S18** | Production's tool factor / equip ratio / land-vs-labour binding side; housing's binding build cap and fill ratios; construction's capacity-vs-materials reason | The "SYSTEM CALCULATION" link of your causal chain on the production, housing and construction sides |

## C. ⚠️ NEW STOP ITEM — the granary escape hatch does NOT work, and both designs relied on it

Both designs escalate *"make `ConsumptionSystem.BoundStore` public and the observer RECOMPUTES spoilage,
overflow and granary capacity"* as **the highest-value cheap ruling available**, on lane 2's measurement
that the reconstruction is exact on 480/480 settlement-turns. **I verified the call site and it does not
work as a visibility change alone:**

```
ConsumptionSystem.cs:200-203   BoundStore(ctx, stores, settlement, _grain,
                                 annualGrainDemand: (exactDemand[IndexOfGood(basketGoods, _grain)]
                                                     + nonStapleShortfall) / Math.Max(dt, double.Epsilon),
                                 dt: dt, cfg: _cfg.Consumption);
ConsumptionSystem.cs:285      private static void BoundStore( ... double annualGrainDemand ... )
```
**[V]**

`annualGrainDemand` is **built at the call site from two transient locals** (`exactDemand[]` and
`nonStapleShortfall`). A public `Capacity(annualGrainDemand, cfg)` cannot be called without that number.
The only route to it from stored state is the `ConsumeRemainder` identity — which is **observer-side
arithmetic over a field with three writers**:

```
Sim.Core/Systems/Production/ProductionSystem.cs:262   tools.ConsumeRemainder  = wearExact - worn;
Sim.Core/Systems/Production/ProductionSystem.cs:450   inStock.ConsumeRemainder = exactIn - sunk;
Sim.Core/Systems/Consumption/ConsumptionSystem.cs:227 row.ConsumeRemainder     = want - demanded;
```
**[V]**

And `Sim.Core/SystemCatalog.cs:54-61` records the T3.3 adversarial finding verbatim **[V]**: *"no shipped
recipe consumes grain today, so the two `ConsumeRemainder` owners never meet. THE FIRST RECIPE THAT TAKES
GRAIN AS AN INPUT — a T3.5 food basket, a brewing recipe, **or a data-only goods.json edit** — puts two
systems on one accumulator."* `CLAUDE.md` states **"Tuning data files and TUNE parameters is always
allowed."**

> **S19 — THE GRANARY SPLIT IS A CLASS-A STOP ITEM, NOT A CHEAP RULING.** The reconstruction both designs
> stake the largest food hole on **can be silently invalidated by an edit the constitution explicitly
> permits, with no code review and no test touching `Sim.Core`.** Closing it honestly needs either new
> serialized state (two `long`s on the grain `GoodStockRow`, v25) **or** `ConsumptionSystem` recording the
> split itself. The magnitude at stake: lane 5 measured `StoreLosses` at **52.7% of all grain harvested**
> over 650 turns; the challenger independently measured **51.8%** over 120 turns on a different seed. More
> than half the grain leaves a settlement's store through **one number that mixes spoilage + granary
> overflow + appropriation + colony provisions.**

## D. Contract moves that need a ruling (no gameplay state involved)

| # | Item | Why it is yours to rule on |
|---|---|---|
| **S20** | **The shipped telemetry violates your own "NEVER an ambiguous NaN string" constraint.** `TelemetryWriter.cs:429-430` returns the strings `"NaN"` / `"Infinity"` / `"-Infinity"`, and `:29` documents it as deliberate **[V]** | Fixing it moves `telemetry/v2` → `v3` by the writer's own rule, and breaks the byte-identity assertions the tag guarantees. Measured blast radius: **12 occurrences** in a 120-turn file, 100% at `migration.pullAttractiveness` on turn 1 **[L]** |
| **S21** | **The ambiguous zero.** `Observer.Migration` writes `push = 0.0` when the `ConsumptionDeficitRow` is ABSENT, with no flag (`Observer.cs:571-573`) **[V]**; `Observer.Food` does the same **[L]**. `HousingSection.HasRow` is the correct pattern, applied to exactly one field | Additive repair, but still an artifact-contract change. Rule on it with S20 |
| **S22** | **A `session-manifest/v3`.** `SessionManifest.Read` is a **whitelist**: `if (schema != Schema && schema != SchemaV1) throw` (`SessionManifest.cs:183-187`) **[V]** | **A v3 manifest is REJECTED by every existing binary**, including your shipped UI artifact and any previously built `sim inspect`. This is *not* "append-safe", and one of the two designs says it is |
| **S23** | **Gate-verdict provenance in run artifacts (item 28).** No producer exists: CI's Test step is a bare `dotnet test` with no `--logger trx` and no upload; `scripts/test.sh` writes a `.trx` that is gitignored and CI does not call it **[L]** | Until a producer exists, any `gates` block honestly reads `not-recorded`. A separate small packet, no gameplay state |

## E. Not a stop item — stated so you know where the line is

`scripts/check-read-isolation.sh` **allowlists `Sim.Core/Observability/` by path prefix** (the `ALLOW`
regex), `Sim.Cli` is scanned and **not** allowlisted, and the script's own header admits *"it does NOT
distinguish sim code from reporting code or from PROSE — two of the seven lines it flagged on
ReplayReport.cs were DOC COMMENT text."* **[V]** I ran it: **OK, exit 0.**

**Consequence:** a happiness/grievance forensic record under `Sim.Core/Observability/` is **not** blocked —
`SocialSection` already reads both tables and ships green. **The line that must not be crossed** is
`Sim.Core/State/SettlementHappiness.cs` gaining such a read: that file is not allowlisted, it would trip CI
immediately, and it would be a **behaviour change**, because happiness feeds migration viability. The
briefing's "a happiness forensic record must not reach those tables" is true of `Sim.Core/State` and of
`Sim.Cli`, **not** of `Sim.Core/Observability`.

**Second consequence, and it binds the implementation:** no identifier, string **or doc comment** in
`Sim.Cli` may contain `Grievances`, `NeedSatisfactions`, `GrievanceRow` or `NeedSatisfactionRow`. The
reader and every query primitive therefore live in `Sim.Core/Observability/`; `Sim.Cli` stays a thin
flag-parsing shell.

---

# 🔎 HONEST ANSWER on the §15 query list

**Caveat repeated: your literal §15 list was not in my packet.** What follows walks (a) the twelve
sub-questions in your worked example, verbatim, and (b) the eight clauses of your stated objective. If your
real §15 contains a question I have not reconstructed, this walk does not cover it — say so and I will
extend it.

**The blunt answer first, in your words:**

> **PER-MOVER DESTINATION ATTRIBUTION DOES NOT EXIST IN THE SIMULATION.** It is not hidden, not discarded
> into a residual, not recoverable by any observer-side function. Neither proposed design delivers it, and
> **no design can**, because the realised From→To amount is the output of an ordered decision sequence, not
> a function of stored state. If your acceptance test is "which destination did Gapi's 163 choose", **this
> proposal fails that test**, and the only thing that passes is stop item **S1**.

Why, from the code I read **[V]** (`MigrationSystem.cs:455-475` and the loop above it):

1. `MigrationRemainder` is a **single accumulator per bucket row**, mutated across the whole destination
   loop. The amount sent to destination *j* depends on every *j′ < j*. Only the **final** remainder is
   serialized, and it does not decompose.
2. `WholeUnits` floors; the fractional part carries forward, entangling destinations.
3. `Ledger.Transfer(..., OverdrawPolicy.ClampToAvailable)` can silently reduce the move, and
   `actuallyMoved` is measured **after the fact** as `before - after`.
4. The "destination bucket not found" branch **restores the floored amount to the remainder**, changing
   every subsequent pair.
5. `Ledger.Transfer` **records nothing at all** — its own doc says *"Conserves by construction (no flow
   entry)"* (`Ledger.cs:45-49`) **[V]**. Only `flows[src].Outflow` and `flows[dst].Inflow` — two scalars —
   survive.

Reconstructing the split would require `push[src,dst] = damping × viability × (gapScale×gap + famine×deficit)`,
every factor a transient local. **Reconstructing them IS re-running `MigrationSystem`** — the second
simulation §0 forbids and that you explicitly banned ("do NOT add a second migration calculation").

### Your worked example, question by question

| # | Your question | Answered? | By which record | If not, why |
|---|---|---|---|---|
| 1 | Population before and after | **YES** | `move.popOpening` / `popClosing` (SUMMED), `place.cohorts[16]` | — |
| 2 | Eligible destinations | **PARTIAL** | every input: `network.legs[]` travel costs, each destination's post-EMA pull, each destination's deficit, both grain-gate inputs, each destination's happiness | The predicate is `damping>0 && viability>0` and **neither operand is stored**. Emitting the *set* means re-implementing viability — §0 violation. Needs **S16** |
| 3 | Migration pressure | **PARTIAL** | the drivers: `move.pushDeficitRatio` (+ a presence flag), pull opening and closing | The **product** `perCount × damping × viability × (…)` is transient. GAP |
| 4 | Source | **YES** | `move.outflow`, keyed to a settlement id | — |
| 5 | **Destination(s)** | **NO** | — | **Does not exist in the simulation.** Needs **S1** |
| 6 | Amount | **AGGREGATE ONLY** | `move.outflow` — netted across all partners, all cohorts, all classes, both channels | Per-destination amount needs **S1** |
| 7 | The relevant happiness | **YES, and corrected** | `place.happinessPrev` = `SettlementHappiness.Of(**prev**)` — **the value migration actually consumed** (`MigrationSystem.cs:232`) | Today the record publishes `Of(next)`, a different number. See §8 |
| 8 | Deprivation factors | **YES as the simulation has them** | `happinessFactors[2]` with **branch labels**, per-(class,need) satisfaction, per-class grievance | A per-factor *share of the deprivation* **is not a simulation quantity** — CES with ρ<0 is non-additive. Any such number would be observer-defined |
| 9 | Food state | **YES, with one hole** | existing `FoodSection` + per-good **opening** (new READ) + `fill` | `StoreLosses` stays **one number for four mechanisms** (≈52% of harvest). Needs **S19** |
| 10 | Housing state | **YES** | existing `HousingSection` + explicit "no population" state for a dead settlement | — |
| 11 | Which category of movement | **PARTIAL, and much better than today** | `categoriesAbsent: ["forcedDisplacement","war"]` — **authoritative negatives**; `colonyMovement` from the ColonistsDeparted residual; `demographicChange` from births + **unsplit** deaths; `voluntaryMigration` with `channelSplitRecorded:false` | **Forced displacement and war DO NOT EXIST at M4** — no code path relocates people by force, revolt moves nobody, appropriation moves grain. Two of six categories eliminated by evidence. The rest need **S2**, **S3**, **S4** |
| 12 | **The authoritative reason or category** | **NO** | — | **No movement of people between settlements or classes carries a reason id anywhere**, because `Ledger.Transfer` records nothing. Reason ids exist only for creation/destruction (Births 5, Deaths 6, Starvation 7). Needs **S1/S2** |

**Score: 6 fully answered, 4 partially, 2 not answerable — and both unanswerable ones fail for the same
single missing row type.**

### Your objective's eight clauses

| Clause | Answered? | By which records |
|---|---|---|
| "what existed" | **YES** | `run` (seed, config digests, pipeline, era table, registries), `site` (carried forward), `turn.polities` |
| "what changed" | **YES** | `turn.hashBefore`/`hashAfter` chained, `turn.controlChanges` (DIFFERENCED), `place` diffs |
| "what each settlement experienced" | **YES**, minus the stop items | `place` + `move` + the existing `SettlementRecord`, joined on (turn, settlement) |
| "what each polity controlled" | **YES** | `turn.polities[].controlled` (public `EmpireQuery`) + every `place.controllerAfter`. **Honest note: the M4 world is politically degenerate** — one Empire, `CommandSource.Player`, `aiEmpires` defaults to 0 **[V: WorldgenConfig.cs:55]** and is absent from `worldgen.json` **[V]** |
| "what each population cohort experienced" | **COUNTS YES, FLOWS NO** | `place.cohorts[16]` (SUMMED). Per-cohort births/deaths/aging need **S8** |
| "what orders were issued" | **ACCEPTED YES, REFUSED NO** | `order` per delivered row, decoded, with its policy effect. UI-side pre-log refusals **are** recordable (see §10). System-boundary refusals need **S9** |
| "what the simulation decided" | **PARTIAL** | `decide` (3 predicates cleanly, 1 classified, 1 blocked on a missing input, 4 refused as §0 violations) + `order.effect` + `turn.controlChanges` |
| **"and WHY major outcomes occurred"** | **PARTIAL — and this is where the largest cheap win sits** | The deepest causal machinery in the tree (`CausalChain`, `HappinessExplanation`, `MigrationExplanation`, `GrievanceExplanation`, `Levers`) is **screenshot-only today**: `grep` of `Sim.Cli/*.cs` for those types returns **0** **[V]**. Routing it to `sim inspect --explain` is pure plumbing — no new state, no new arithmetic, no mechanics |

### The one thing a reviewer most needs and cannot get today

**He cannot enumerate what the artifact set is unable to tell him.** The gaps are real, honest and
documented — in a design document he does not have. This proposal's single most valuable structural idea is
that **the artifact carries its own gap catalogue and its own §0 field dictionary**, so `sim forensic --gaps`
answers "what can this evidence not establish" in one command, instead of by discovering silence.

---
# 1. Existing artifacts audited

## 1.1 The artifact set as it stands

A **played session** (`Sim.Ui`) writes **five** files into `runs\`, all sharing one wall-clock stamp **[L]**:

| file | tag | shape | rewritten? |
|---|---|---|---|
| `session-<stamp>.json` | `session-manifest/v2` **[V: SessionManifest.cs:71]** | 12 fields, written **once at launch** | never |
| `orders-<stamp>.bin` | `CIVORDR\0` IoVersion 1 **[L]** | `{Turn, ActorId, Kind, TargetId, Amount}` | appended |
| `trace-<stamp>.csv` | header `turn,year,population,settlements,food,hash` **[V: SessionTrace.cs:34]** | one line per turn **including turn 0** | rewritten |
| `chronicle-<stamp>.txt` | none | **prose only**, one rendered sentence per line | rewritten |
| `telemetry-<stamp>.jsonl` | `telemetry/v2` **[V: TelemetryWriter.cs:35]** | `{schema, turn, settlements[]}` per turn | **rewritten in full every End Turn [V: UiSession.cs:322-327]** |

A **headless CLI run** writes **none of those five**. `sim run`/`sim replay` can emit a bare hash-log, a
snapshot, or a `replay-report/v1`; `--telemetry` exists **only** on `sim inspect`, which **requires** a
manifest and derives its turn count from the trace beside it **[L]**. `Chronicle` has **0 references in
`Sim.Cli`** **[V]**. **A headless, reproducible forensic run — the exact thing an external reviewer needs —
is not possible from the shipped CLI without hand-authoring session metadata.**

`docs/session-records.md` still says "Four files in `runs\`" **[L]** — it predates the telemetry file.

## 1.2 The 28-item table

Legend: **SER** = in the canonical snapshot (schema v24, so recoverable from a save or by replay) ·
**TEL** = in the `telemetry/v2` JSONL · **DERIV** = obtainable by READ/SUMMED/DIFFERENCED/RESIDUAL, or by
RECOMPUTED through a function that is **already public** · **N/A** = not available at any level.

| # | Item | CURRENTLY SERIALIZED | CURRENTLY TELEMETRIED | DERIVABLE | NOT AVAILABLE |
|---|---|---|---|---|---|
| 1 | World identity | seed; sizePx/settlements overrides; `schemaVersion`; buildSha/buildDate; platform (`SessionManifest.cs:150-169`) **[L]**; terrain content hash **only inside a Snapshot** (`CanonicalSchema.cs:153-158`) **[L]** | — (telemetry has **no** seed, build, platform or schema version) **[L]** | the world itself, by replay from (seed, overrides) + the build's configs; settlement names, a pure function of (seed, id) **[L]** | **Config identity.** All nine embedded resources are compiled into `Sim.Data` **[L]**; nothing records their bytes or a hash. The only pointer is `buildSha`, which falls back to `"dev"` on a local build **[L]**. **A session played from a locally-built `Sim.Ui` records NO recoverable configuration.** Terrain hash absent from all five session files |
| 2 | Settlement identity | `SettlementRow(Id, SiteCell, FoundedTurn)` **[L]** | `settlement`, `foundedTurn`, `founded`, `controller` **[L]** | founding **year** by joining FoundedTurn to that turn's Year | **Founder polity** as a fact (`SettlementRow` has no owner field); **colony parent** (no source id) |
| 3 | Settlement ownership / control | `ControlRow(Polity,Place,Strength)`, `PolityRow(Id,CommandSource)`, `CapitalRow`, `ClaimRow`, `RecognitionRow` **[L]** | `settlements[].controller` (−1 sentinel) and `turn.flows.controlLost` (**a count only**) **[L]** | full polity snapshot via **public** `EmpireQuery.*`; controller before/after as a DIFFERENCE | **Which** settlement changed hands, **from** whom **to** whom, and **why**. `Strength` is a dead slot — both writers hardcode 1.0 **[L]** |
| 4 | Population | `BucketRow(Settlement,Culture,Religion,Class,CohortIdx,Count)` **[L]** | opening/closing, children/adults/elders, notables, births, deaths, inflow, outflow, colonistsDeparted, per-class counts **[L]** | **per-cohort counts** (SUMMED) — present in `replay-report/v1` but **not** in telemetry **[L]** | per-**culture** and per-**religion** counts (exposed in no artifact) |
| 5 | Demographics | `SettlementVitalsRow(Settlement,Births,Deaths,DtYears)` **[L]** | births, deaths (**one unsplit number**) **[L]** | `vitalsDtYears` (READ, currently dropped by the Observer) | **natural vs starvation split per settlement** (summed at `DemographicsSystem.cs:265-266`) **[L]**; per-cohort/per-class vitals; **cohort aging** (a bare `Ledger.Transfer`) |
| 6 | Food | `GoodStockRow.{Amount, LastProduced, LastConsumptionDemand, LastConsumptionEaten, ConsumeRemainder}`; `ConsumptionDeficitRow` **[L/V]** | grain opening/closing, harvest, eaten, **StoreLosses (RESIDUAL)**, demandUnits, deficitRatio, per-food-good produced/demand/eaten **[L]** | per-good **fill** via public `NeedsGrievanceSystem.Fill`; per-good **opening** (READ prev) | **granary capacity, spoilage, overflow per settlement** — `BoundStore` is `private static` and its input is a **call-site transient** **[V: ConsumptionSystem.cs:200-203, :285]**. See **S19** |
| 7 | Production | `LastProducedUnits`; `SectorAllocationRow`; `HarvestWeatherRow`; `DepositRow` **[L]** | per-good produced; sector shares (RECOMPUTED via public `Sectors.Share`) **[L]** | labour pool via public `BandViews`; weather multiplier (READ, not currently telemetried) | tool factor, equip ratio, **the land-vs-labour binding constraint**, per-recipe labour split, recipe availability results — all transient **[L]**. See **S18** |
| 8 | Consumption | as item 6 | as item 6 | per-good fill | the substitution term `nonStapleShortfall` and pre-substitution demand — transient **[V: call site]** |
| 9 | Storage | `GoodStockRow.Amount`; `StructureRow` **[L]** | closing stock per good **[L]** | — | **granary capacity (S19)**; **no storage bound exists for any non-grain good** — `BoundStore` is called for grain only **[V]**, so "not applicable" must be said rather than a number printed. `StructureRow` is **inert**: no system reads it **[L]** |
| 10 | Shelter / housing | `HousingRow(Dwellings, BuildRemainder, DecayRemainder, LastMaintenanceFraction, LastLaborUsed)` **[L]** | hasRow, dwellings opening/closing, capacity, need, sufficiency (RECOMPUTED), maintenance, labour, **built/decayed as a GAP STRING** **[L]** | — | **built vs decayed per settlement**; the binding build cap (deficit/labour/timber/clay) — transient **[L]**. See **S6**, **S18** |
| 11 | Comfort | `NeedSatisfactionRow`, `GrievanceRow` **[L]** | per-(class,need) satisfaction and per-class grievance **[L]** | per-good fill for pottery/cloth | **per-need grievance accrual** — *not a missing field, a missing concept*: the CES aggregate is non-additive |
| 12 | **NOT SUPPLIED** | *(no lane names item 12)* | — | — | **Reconstruction guess: happiness.** Covered in §8; the honest answer is that every value other than exactly 0 or 100 is **unverifiable from the artifact** today |
| 13 | **NOT SUPPLIED** | *(no lane names item 13)* | — | — | **Guess: grievance / needs.** Already telemetried per (class, need); attribution by need is refused as a non-quantity |
| 14 | **NOT SUPPLIED** | *(no lane names item 14)* | — | — | **Guess: migration.** Covered in §9 |
| 15 | **NOT SUPPLIED** | *(no lane names item 15)* | — | — | **Guess: classes / social mobility.** Latch covered in §7; **flows are S8** |
| 16 | Trade | `TradeFlowRow(From,To,Good,Quantity)`, cleared each turn **[L]** | per-settlement `tradeIn`/`tradeOut`; world `tradeUnits`/`tradeFlowCount`; per-good prices **[L]** | **trade scope** (Unruled/Domestic/Foreign) via public `TradeScopes.Classify` **[L]** | price gap, deadband threshold, market-scale sensitivity, desired q*, availability scaling, pair path cost — all transient **[L]**. **Structural fact: GRAIN NEVER TRADES** (the numeraire is short-circuited) **[L]**, so no food outcome can ever be explained by imports |
| 17 | Orders | `OrderLog` (its own artifact, not `WorldState`); four kinds only **[L]** | `turn.orders[]` and `settlements[].orders[]` with `index, turn, actor, kind, targetId, settlement, sector, amount` **[L]** | `commandSource` per order via public `EmpireQuery.TryGetCommandSource`; **effect** by DIFFERENCING policy/queue; `PolicyHistory` already computes order→weight-change attribution **[V]** | **acceptance / rejection with a reason** (five silent `continue` sites, **S9**); UI-side refusals never reach the log at all. `PolicyChange`/`PolicyState` are **written to no artifact** — in-memory only, UI-only **[V]** |
| 18 | AI decisions | `CommandSource` enum; `PolityRow.Source` **[L]** | — | — | **THERE IS NO AI AT M4.** `AiEmpires` defaults to 0 **[V: WorldgenConfig.cs:55]** and `aiEmpires` is absent from `worldgen.json` **[V]**. One Empire, PolityId 1, `CommandSource.Player`. **No system emits an order.** "What the AI decided" has no subject |
| 19 | Player decisions | `OrderLog` rows | as item 17 | — | refusals (as 17); **there is no player path for `EnqueueConstruction` at all** **[L]**; **no control check on Labor/SectorAllocation at any layer** **[L]** |
| 20 | War and control changes | `ControlRow` removal by `RevoltSystem` **[L]** | `turn.flows.controlLost` (a count) **[L]** | revolt **readiness** via public `SettlementHappiness.IsRevoltReady` + public `RevoltThreshold` **[V: :70, :220]**; the control **diff** | **WAR DOES NOT EXIST AT M4** — no battle, army, siege or resolver. Which settlement revolted, and the authoritative reason (**S11**) |
| 21 | Colonies | new `SettlementRow` + inherited `ControlRow` **[L]** | `founded`, `foundedTurn`, `colonistsDeparted` (RESIDUAL with its identity), `storeLosses` **[L]** | source localisation **only when ≤1 founding occurs in that step** **[L]** | **the parent settlement id** — the colony carries none; party and provisions move by unrecorded `Ledger.Transfer` **[L]**. See **S4** |
| 22 | Taxes and governance | — | — | — | **NONE OF IT EXISTS.** No tax, treasury, tribute, currency, legitimacy, administration or admin-reach anywhere in `Sim.Core` **[L]**. M5 owns it |
| 23 | Randomness / seed / RNG state | seed; `RngStreamRow(SystemId,RegionId,State,Inc)` — **snapshots only** **[L]** | — | fully derivable by replay (streams are lazily created from `splitmix64(seed, …)`) **[L]** | **a played session writes no snapshot**, so RNG state appears in **zero** of the five files; **per-turn draw counts do not exist at all** (**S10**). Wart: the `Region` column holds **settlement** ids for harvestweather **[L]** |
| 24 | Turn boundaries | `SimClock(Turn, SimDays, DtDays)` **[L]** | `turn`, `year`, `dtYears` **[L]** | the whole boundary contract is **stated in code** — dt at turn start, clone, fixed pipeline order, **clock advances last** **[V: TurnExecutor.cs:84-121]**; order delivery `BatchFor(prev.Clock.Turn)` **[V: :100]** | the **era band NAME** in force (bands are named in `era-pacing.json`, recorded nowhere); the **pipeline order itself** is data at load and is **persisted in no artifact** |
| 25 | State hashes | one per turn in the live **trace**, turn 0 included **[V: SessionTrace.cs:34]** | **NOT AT ALL** — measured zero occurrences of `"hash"` in a telemetry file **[L]** | `replay-report/v1` carries a per-turn hash, but is never written by a played session **[L]** | **the algorithm and its version are recorded in no per-turn artifact.** And structurally: `CanonicalSchema.Write` begins at the seed, so **a world hash does not self-identify its schema** **[V: CanonicalSchema.cs:143]** |
| 26 | Events / chronicle | — (`ChronicleEvent` is never serialized) | — | detection is a pure function of the replayed worlds **[V: ChronicleCollector.cs:18-20]** | **The structured event is DISCARDED.** `ChronicleEvent(Type, Turn, Year, SettlementId, Magnitude1, Magnitude2)` exists **[V]**, and all six fields are destroyed on the way to disk: type→sentence, **turn dropped**, settlement id→**display name**, year and both magnitudes **rounded to whole numbers** **[V: ChronicleProse.cs:26-34, 56-66]**. **This violates your "stable ids, never display names" constraint in the only event artifact that exists.** Vocabulary is six types only. `Sim.Cli` cannot regenerate it (0 references) **[V]** |
| 27 | Derived metrics | — | per-settlement deficitRatio, foodSurplusRatio, artisanShare, tradeVolume, housing capacity/need/sufficiency, foodBalance **[L]** | `CalibrationAnalysis` computes growth, crude rate, migration gross, crashes, pyramid shares, density — but **only three are ever serialized, and only into the unrelated `autoplay` artifact** **[L]** | no per-settlement density exists as a simulation quantity at all; the UI's 12 series keys are exposed by **no** CLI verb **[L]** |
| 28 | Test / gate results | — | — | — | **NOTHING in any run artifact references a test run, a gate, a CI run id or a golden.** The only link is `buildSha`, which is `"dev"` locally. See **S23** |

## 1.3 Append-safety of each existing artifact, against the code

| artifact | append-safe? | evidence |
|---|---|---|
| `telemetry/v2` JSONL | **at FIELD level, yes** — but a **tag move rejects every v2 reader by design** | `TelemetryWriter.cs:19-27` **[V]** |
| `session-manifest` | **NO, at the tag.** `Read` is a **whitelist** that throws on anything but v2/v1; required fields use `GetProperty` and throw if absent | `SessionManifest.cs:183-187` **[V]** |
| `trace-<stamp>.csv` | **NO.** Exactly 6 columns or it throws with the line number | `SessionTrace.cs:83-87` **[V]** |
| `orders-<stamp>.bin` | **NO.** IoVersion 1 only; `Append` enforces nondecreasing turn order | **[L]** |
| `chronicle-<stamp>.txt` | no schema, no version, no delimiter contract | **[V]** |
| snapshot | **NO by design** — exact version equality, no migration (D-008) | **[L]** |

---
# 2. Gaps

Ordered by how much each costs your stated objective. G-numbers are this document's.

**Blocking your worked example**

- **G1 — Pairwise migration From→To does not exist.** Only two scalars survive per settlement per turn
  **[V: MigrationSystem.cs:455-475]**. Lane 3 measured **all 11 other settlements reachable and viable on
  every single outflow turn** **[L]**; the challenger measured a 217-person outflow on turn 38 where **all
  twelve** settlements gained **[L]**. The candidate set is not even a useful bound. → **S1**
- **G2 — Inflow and outflow are netted.** 458 of 2,400 settlement-turns had **both** non-zero **[L]**, so
  even the gross direction of a settlement's change is two numbers with no partner attached.
- **G3 — The two migration channels are summed before anything is recorded.** → **S2**
- **G4 — The eligibility gate is a transient product.** Inputs survive; `damping` and `viability` do not. → **S16**
- **G5 — The published pull signal is not the one the mechanism used.** **I verified this from source**:
  `Observer.Migration` reads **`prev`**`.SmoothedAttractiveness` for both `pullAttractiveness` and
  `allAttractiveness` **[V: Observer.cs:574-581]**, while `MigrationSystem` writes the updated EMA at
  `:254` and then reads **that updated array** for every gap at `:360`, `:401`, `:416` **[V]**.
  `MigrationExplanation` reads `next` and is correct, so **two T4.19 artifacts disagree about the same
  quantity.** Lane 3 measured them differing on **2,388 of 2,388** comparable records — never once equal —
  and the challenger on **1,416 of 1,416** **[L]**. This is a **one-line read-source correction**, no state,
  no mechanics. **Per ADR-015 §6 it is a FINDING awaiting a verdict, not an authorised fix, and must not be
  applied on this document's word.**

**Blocking happiness verification**

- **G6 — No weights, no sigma, no floor, no span, no raw aggregate, no normalisation in any artifact.**
  `WeightOf` is `private`, the need-id mapping is `private const`, and floor/span are **inline locals inside
  `Of`** **[V: SettlementHappiness.cs:207-209, :226-227, :234]**. **Consequence: for every happiness value
  other than exactly 0 or 100, the number is unverifiable from the record.** → **S15**
- **G7 — Per-factor deprivation contribution is not a simulation quantity.** CES with ρ<0 is non-additive.
  Not a missing field; a missing concept.
- **G8 — The absence branches are invisible, and one is an ambiguous zero.** Four distinct branches produce
  identical-looking factor values **[V: SettlementHappiness.cs:118-159]**, and `Observer` writes `0.0` for an
  absent deficit row with no flag **[V: Observer.cs:571-573]**. `HousingSection.HasRow` is the correct
  pattern, applied once. → **S21**
- **G9 — Happiness is published on the wrong world for migration forensics** (`Of(next)` published,
  `Of(prev)` consumed). Fixable as a *second* READ, not a replacement.

**Blocking movement typing and attribution**

- **G10 — Deaths are unsplit per settlement.** → **S3**
- **G11 — Class mobility is completely unrecorded.** Lane 3 measured **122,638 person-movements of class
  across 200 turns with zero recorded cause** **[L]** — a **seventh** movement category your list does not
  name. → **S8**
- **G12 — Cohort aging is completely unrecorded**, and per-bucket vitals do not exist. → **S8**

**Blocking reconstruction and provenance**

- **G13 — Configuration is not in the artifact set at all.** The largest provenance hole. Closing it is a
  pure READ of config already in memory. **No gameplay state.**
- **G14 — The system execution order is documented, not persisted.** Also a pure config READ.
- **G15 — Telemetry carries no identity and no hash.** A telemetry file found alone cannot be attributed to
  a world, and a line can be bound to a state only by a turn-number join to a trace whose association is
  itself only a filename string.
- **G16 — No cross-artifact integrity binding.** The manifest names its companions by **filename**
  **[L]**. Swapping a same-named file is undetectable, and surfaces as `REPRODUCTION FAILED`, **which reads
  as a determinism defect and is not one.**
- **G17 — The trace↔replay divergence check is POSITIONAL.** `Divergence` compares `played[i].Hash` against
  `replayed[i].Hash` and **never asserts the turn numbers agree**, although it prints the turn in the verdict
  **[V: Sim.Cli/Program.cs:485-491]**. For a layer whose authority rests on that verdict, this is the wrong
  contract.
- **G18 — A session's turn count exists only in the trace.** Lose the trace and the whole set becomes
  uninspectable **[L]**.
- **G19 — The chronicle is lossy by construction and keyed by display name** (item 26). → violates a binding
  constraint.
- **G20 — The whole causal layer is UI-only.** 0 references in `Sim.Cli` **[V]**. **This is the single
  largest mismatch between what the tree already contains and what the artifacts expose**, and closing it
  requires no new state and no new arithmetic.

**Artifact-contract gaps**

- **G21 — `"NaN"` as a string** (→ **S20**) · **G22 — sentinel integers** (`controller: -1`,
  `Actor/OrderIndex: -1`) · **G23 — the §7 bounded window is designed and NOT implemented**; `ObservationLog`
  holds an unbounded `List` **[L]** · **G24 — the UI rewrites the entire telemetry file on every End Turn**
  **[V: UiSession.cs:325-326]**, against `observability-architecture.md:283`'s claim of "streaming append"
  **[V]** · **G25 — the shipped §7 storage figures are wrong by 3–5×** (see §11) ·
  **G26 — `sim inspect` prints the raw packed order target** (`88` = settlement 11 × 8 + sector 0) although
  the public decoder exists and the telemetry uses it **[V: Program.cs:549-551 vs TurnRecord.cs:140-151]**.

---

# 3. Proposed artifact(s)

## 3.1 What I recommend, and why

**RECOMMENDATION: ONE new artifact — `runs/forensic-<stamp>.jsonl`, tag `forensic/v1` — with the ten
record kinds of Design B, the refusal discipline of Design A, and the sizing of neither.**

This is a **synthesis**, and I am explicit about what I took from where and what the challenge refuted.

**Why a new artifact and not an extension of the existing five (against Design A):**

1. **Extension of the manifest is not append-safe and Design A says it is.** `SessionManifest.Read` is a
   whitelist that throws **[V]**. **A `session-manifest/v3` is rejected by every existing binary**,
   including your shipped UI artifact. That is stop item **S22**, not a free extension.
2. **Extension of the telemetry moves a tag by the writer's own rule** **[V: TelemetryWriter.cs:19-27]**,
   breaking the byte-identity assertions the tag guarantees and two literal test assertions **[L]**.
3. **The other three cannot be extended at all** — S12, S13 and the schema-less chronicle.
4. **Design A's headline cost claim (+0.3%) does not survive measurement.** See §11.

**Why not five separate streams:** five files means five stamps to keep in step and five chances for the
filename-only binding that is already the weakest joint in the set. One file has one identity, and
`grep '"rec":"move"'` is a complete stream extractor.

**What stays untouched, and this is the point:** `CanonicalSchema` stays at **Version 24**. No world hash
moves. No golden moves. No existing artifact's tag moves. No existing reader breaks. No test goes red.

## 3.2 The record kinds

| kind | cardinality | class |
|---|---|---|
| `run` | 1, first line | launch identity, config digests, pipeline, era table, registries, field dictionary, gap catalogue |
| `close` | 1, last line | closing identity, **content hashes of every companion artifact**, gate block |
| `turn` | 1 per turn | boundary, pre/post hash, polity snapshot, control diff, reconciliation verdicts |
| `site` | 1 per settlement, at founding | static geography, founding facts |
| `catchment` | 1 per settlement per change | capacity, network revision, size tier |
| `network` | 1 per network revision | the pairwise travel-cost matrix — **O(n²·R), not O(n²·T)** |
| `place` | 1 per (turn, settlement) | the **strict complement** of the existing `SettlementRecord` |
| `move` | 1 per (turn, settlement) | movement identity, drivers, category verdicts |
| `decide` | 1 per (turn, settlement) | predicate evaluations |
| `order` | 1 per delivered order | order lifecycle and effect |
| `event` | sparse | structured `ChronicleEvent`, id-keyed, **unrounded** |
| `refusal` | sparse, **`repro:false`** | **UI-side pre-log order refusals** — see §10 |
| `phase` | **opt-in, default OFF** | intra-turn system attribution — see the warning in §15 |

**Two classes, declared on every line.** `"repro": true` = a pure function of (seed, overrides, founded,
order log, config) and therefore rebuildable by `sim inspect`. `"repro": false` = session-only (`refusal`,
and the `run` record's wall-clock fields). **A reviewer must never mistake "absent from a replayed
reconstruction" for "did not happen".**

## 3.3 What is deliberately NOT duplicated

Your constraint: *"do not duplicate data unnecessarily."*

- **No second per-turn settlement snapshot.** `telemetry/v2`'s `SettlementRecord` is one, at a measured
  ~5.4–6.2 KB per settlement-turn. `place` is defined as its **strict complement**.
- **No second food accounting model** and **no second migration calculation.** Per your explicit prohibition.
- **No restatement of the world conservation accounts.** `TurnRecord`'s `stocks[]`/`goods[]` blocks are
  already exact source-and-sink by reason; `turn` carries the **verdict** and the discrepancy list only.
- **No streamed Explain layer.** Those records are pure functions of one world and are recomputable on
  demand. They belong in the **query surface**, not in a stream.
- **Two deliberate redundancies, each earning its bytes (~55 B total):** `place.happiness.value`/`factors`
  (so the verification block is self-contained **and** a cross-artifact invariant test can assert
  forensic == telemetry every settlement-turn) and `move.popOpening`/`popClosing` (so the conservation
  identity closes without a join).
- **One duplication that needs your ruling:** `place.cohorts[16]` duplicates `replay-report/v1` — **but the
  replay report is never written by a played session**, so for your actual playtest the cohort pyramid has
  no home. **Recommendation: carry it in the forensic stream and demote `replay-report/v1` to the CLI-only
  diagnostic it already is.** The design works either way.

---
# 4. Record schemas

## 4.0 Global envelope and null policy

Every line begins with the same five keys, in this order:

```
"v"     : "forensic/v1"     the tag; moves with the emitted field set (TelemetryWriter's own rule)
"rec"   : "<kind>"          the discriminator
"run"   : "<16 lowercase hex>"  the run id (§5)
"turn"  : long              the turn described; -1 is NEVER used (the run record carries 0)
"repro" : bool              replay-reproducible
```

**NULL POLICY — your binding constraint, honoured without exception:**

1. A value the simulation **does not record** is JSON `null`, **always paired** with a sibling
   `<field>State` string carrying a token from the **gap catalogue** (§4.11). Never an ambiguous NaN string,
   never a sentinel integer, never a bare zero.
2. A **row** that is absent is `"<thing>RowPresent": false` with dependent values `null`. This generalises
   `HousingSection.HasRow`, today the only field in the settlement record that makes absence explicit.
3. **A non-finite double NEVER appears.** If a recompute yields non-finite, the field is `null` with state
   `"nonfinite"`. This is a deliberate divergence from `telemetry/v2` — see **S20**.
4. **"Not applicable" is distinct from "not recorded":** `"applicable": false` with a reason token. Example:
   non-grain goods have **no storage bound at all** **[V]**, so the record must say "no capacity modelled"
   rather than print a number or omit the field.
5. **No nondeterministic timestamp appears on any `repro:true` record.** Wall-clock lives only on the `run`
   record's `startedAtLocal` / `startedAtUtcOffset` (`repro:false`) and is **excluded from the run id**.

**IDS ONLY.** Every reference is a stable integer — `SettlementId.Value`, `PolityId.Value`, `ClassId.Value`,
`GoodId.Value`, need id, sector index, order-log index, `ReasonId`, `CohortIdx`. **No display name appears in
any record.** Registry names are emitted **once**, in `run.registries`. A reviewer who wants "Gapi"
regenerates it from `run.seed` — `NameRegistry` is a pure function of (worldSeed, settlementId) and is
correctly never serialized **[L]**.

**ORDERING.** Records within a turn are emitted in a fixed order: `turn`, then `site`/`catchment`/`network`
if any, then `place`, `move`, `decide` in **ascending settlement-table index**, then `order` in **ascending
log index**, then `event`. No `Dictionary` iteration. **There is no ordering or argmax over a double
anywhere in this design** — so `CLAUDE.md`'s composite-key rule has no subject in the writer, and the test
suite asserts that absence rather than implementing a tie-break. (The **query surface** does rank, and
there the rule applies in full — see §12 and test **T17**.)

## 4.1 `rec: "run"` — §1, §2, §12

| field | §0 class | source | null form |
|---|---|---|---|
| `runId` | DERIVED (see §5) | SHA-256 over world-defining inputs, computed **outside** the canonical stream | never null |
| `seed` | READ | `SessionManifest.Seed` / CLI `--seed` | never null |
| `sizePx`, `settlementsOverride` | READ | manifest overrides | `null` + state `"default"` |
| `founded` | READ | the CLI/UI founding flag | never null |
| `canonicalSchemaVersion` | READ | `CanonicalSchema.Version` = **24** **[V:95]** | never null |
| `hashAlgorithm` | READ (constant) | `"sha256/canonical-stream"`, naming `WorldHash.Compute` **[V]** | never null |
| `hashCoversSchemaVersion` | READ (**stated fact**) | **`false`** — `CanonicalSchema.Write` begins at the seed **[V:143]**, so a world hash does **not** self-identify its schema | never null |
| `buildSha`, `buildDate`, `buildShaRecorded` | READ | `BuildInfo`; `buildShaRecorded:false` when it is `"dev"` **[L]** | never null |
| `platformPlayed`, `platformInspected` | READ | manifest; `RuntimeInformation.RuntimeIdentifier` | `null` + `"not-recorded"` |
| `config[]` `{file, bytes, sha256}` | READ | the nine embedded resources in `Sim.Data` **[L]** | never null |
| `configDigest` | DERIVED | SHA-256 over the ordered `(file, sha256)` pairs | never null |
| `pipeline[]` `{position, name, wellKnownId}` | READ | `PipelineLoader`'s returned `SystemRegistration[]` — **`Name` and `WellKnownId` are public constants** | never null |
| `eraBands[]` `{name, startDay, endDay, dtDays}` | READ | `EraTable.Bands` (public) **[L]** | never null |
| `rngDerivation`, `rngRegionColumnNote` | READ (constants) | prose stating the stream-derivation rule and that the `Region` column holds **settlement** ids for harvestweather | never null |
| `aiEmpiresConfigured` | READ | `WorldgenConfig.AiEmpires` — **`0`** **[V:55]**. Emitted **explicitly as zero**, never omitted | never null |
| `registries` `{goods[], classes[], needs[{id,name,weight,bound}], sectors[], reasons[]}` | READ config | the **only** place names appear | never null |
| `needsTuning` `{sigma, satisfactionFloor, tierAFloor, tierAGain, tierACollapse}` | READ config | `cfg.Needs.Aggregation`, **dumped verbatim, with no assertion about how happiness uses them** (see §8) | `null` + `"needs-config-absent"` |
| `terrainContentHash` | READ | `TerrainSet.ContentHash` — today present **only inside a Snapshot** **[L]**, so nothing binds a played session to its terrain | `null` + `"terrain-absent"` |
| `fieldDictionary[]` `{path, kind, sourceWorld, sourceTable, sourceCitation}` | READ (self-describing) | **the artifact carries its own §0 classification, machine-checkable.** `kind ∈ READ\|SUMMED\|DIFFERENCED\|RESIDUAL\|RECOMPUTED\|GAP` | never null |
| `identities[]` `{token, statement}` | READ (constants) | reuses `Observer.ColonistsDepartedIdentity` and `StoreLossesIdentity` **verbatim** rather than restating them | never null |
| `gapCatalogue[]` `{token, what, whereComputed, whereDiscarded, whatWouldCloseIt}` | READ (constants) | §4.11 | never null |
| `nullPolicy` | READ (constant) | the five rules above | never null |
| `startedAtLocal`, `startedAtUtcOffset` | READ, **`repro:false`** | `Sim.Ui`'s `DateTime.Now` (ADR-009 legal there). **The offset field closes a real defect: the one human-facing timestamp in the whole artifact set carries no timezone today** | `null` + `"not-recorded"` |

## 4.2 `rec: "close"`

| field | §0 class | source | null form |
|---|---|---|---|
| `turnsReached`, `finalWorldHash` | READ | last observed step; **caller-supplied** hash (§6) | never null |
| `artifacts[]` `{role, file, bytes, sha256}` | READ | manifest, orders, trace, chronicle, telemetry, forensic. **Closes G16: the binding becomes content-addressed** | per-entry `null` + `"absent"` |
| `gates[]` `{name, verdict, evidence}` | READ | a build-time producer | **`[]` + `gatesState:"not-recorded — no gate-verdict producer exists at 7c91cbf"`.** See **S23** |

**A missing `close` line is itself the signal that the session did not close cleanly** — information the
current artifact set cannot express.

## 4.3 `rec: "turn"` — §2, §4

| field | §0 class | source | null form |
|---|---|---|---|
| `year`, `dtYears`, `dtDays`, `simDaysAtStart` | READ | `SimClock` on next / prev. **dt is chosen at TURN START and the clock advances LAST** **[V: TurnExecutor.cs:85, :108]** | never null |
| `hashBefore`, `hashAfter` | READ, **caller-supplied** | `WorldHash.ComputeHex` (§6) | never null |
| `orderDeliveryTurn` | READ | `prev.Clock.Turn` — the executor's own rule, `BatchFor(prev.Clock.Turn)` **[V: :100]** | never null |
| `polities[]` `{polity, commandSource, controlled, extinct, capital}` | RECOMPUTED | **public** `EmpireQuery.TryGetCommandSource` / `ControlledCount` / `IsExtinct` / `TryGetCapital` | `capital`: `null` + `"no-capital-row"` |
| `claimsWritten`, `recognitionsWritten` | READ | `Claims.Count`, `Recognitions.Count`. **Emitted explicitly as zero with a note that no system writes either table** — an omitted table reads as an oversight | never null |
| `statelessSettlements` | SUMMED | settlements with no `ControlRow`, via public `EmpireQuery.TryGetController` | never null |
| `controlChanges[]` `{settlement, polityBefore, polityAfter, reasonRecorded, reasonNote}` | DIFFERENCED | prev vs next `Controls`. **`reasonRecorded` is ALWAYS `false`**; `reasonNote` states "the only M4 mechanism that removes a `ControlRow` is `RevoltSystem`" | `polityBefore/After`: `null` + `"no-control-row"` |
| `policyChanges[]` `{settlement, sector, oldWeight, newWeight, actor, orderIndex}` | READ | `PolicyHistory.Observe` output — **already computed, already collected into `ObservationLog`, and written to NO artifact today** **[V]** | `actor`/`orderIndex`: `null` when unattributed (today an in-band −1) |
| `events[]` | READ | see §4.9 | `[]` when none |
| `allAccountsReconcile`, `discrepancies[]` | READ (a **verdict over** `TurnRecord`, not a copy of it) | the `Reconciles` flags and integer `Discrepancy` already in telemetry | never null / `[]` |
| `sumInflow`, `sumOutflow`, `migrationBalanced` | SUMMED, then DERIVED verdict | Σ `MigrationFlowRow`; **exact integer equality, no epsilon** | never null |
| `unattributedGrainTransfer` | READ | `TurnRecord.Flows` — an honest detector: a store-loss residual going **negative** | never null |
| `counts` `{place, move, decide, order, event}` | SUMMED | **lets a reader detect a truncated file** | never null |
| `rngStreams[]` `{systemId, region, state, inc}` | READ | `RngStreamRow` **[L]**. **OPTIONAL — see §11; this is the first field set I would drop if the line must be leaner** | `[]` when none |

**Deliberately NOT a field: `eraBandName`.** Resolving the band from `simDays` would re-implement
`EraTable.DtDaysAt`'s half-open-interval predicate. The band **table** is in `run.eraBands` and
`simDaysAtStart` is here; the reviewer resolves it from two READs. This matters: two shipped bands share
`dtYears` 0.5, so **dt alone does not identify the band**.

## 4.4 `rec: "site"` — §3 identity and geography

| field | §0 class | source | null form |
|---|---|---|---|
| `siteCell` | READ | `SettlementRow.SiteCell` | never null |
| `elevation, water, temperature, moisture, fertility, movementCost, river` | READ | `TerrainSet`'s public `ReadOnlySpan<double>` at `siteCell`. **Terrain is caller-supplied**, exactly as the hashes are — `sim inspect` already regenerates it **[L]** | `null` + `"terrain-absent"` |
| `deposits[]` `{good, abundance}` | READ | `DepositRow` | `[]` + `"no-deposit-rows"` |
| `foundedTurn` | READ | `SettlementRow.FoundedTurn` | never null |
| `foundedYear` | READ | `next.Clock` **at the founding step** — available because `site` is emitted at founding, so no join is needed | never null |
| `controllerAtFounding` | RECOMPUTED | public `EmpireQuery.TryGetController` on the founding step's next | `null` + `"no-control-row"` |
| `founderPolity` | **GAP** | — | `null` + `"gap.founderPolity"`. **`controllerAtFounding` is "the controller ON the founding turn" and is NOT "the founder". The record refuses to conflate them.** → **S4** |
| `parentSettlement` | **GAP** | — | `null` + `"gap.colonyParent"` → **S4** |

## 4.5 `rec: "catchment"` — emitted on change

| field | §0 class | source | null form |
|---|---|---|---|
| `effectiveArableKm2, nodeCount, networkRevision, lastRecomputeTurn, sizeTierStored` | READ | `CatchmentSummaryRow` | `null` + `"no-catchment-row"` |
| `sizeTierRecomputed` | RECOMPUTED | **public static** `CatchmentSystem.SizeTier(dwellings, cfg.Catchment.SizeDwellingsRef)` **[L]** | `null` + `"no-housing-row"` |
| `tierAgrees` | DERIVED verdict | stored == recomputed. A disagreement is the recompute gate lagging, and is worth seeing | never null |

**No density field.** There is **no per-settlement density in the simulation** — the only public density is
world-level and end-of-run, and §0 has no "ratio" kind. The two READs (population, arable) are emitted and
the reviewer divides. **Inventing a ratio would be a sixth field kind.**

## 4.6 `rec: "network"` — emitted per network revision

`revision` (READ) · `legs[] {f, t, cost}` (READ `SettlementDistanceRow`, walked in integer (from,to) order),
`[]` + `"no-distance-rows"`.

**The key size decision.** The travel-cost matrix is O(n²) but **static between recomputes**, so it is
emitted **per revision, not per turn** — O(n²·R), not O(n²·T). Contrast the shipped telemetry, which repeats
the full n-vector of smoothed attractiveness inside **every** settlement record; lane measurements put that
at ~8.8% of the file for information a single value per settlement per turn carries **[L]**.

## 4.7 `rec: "place"` — §3, §5 — the strict complement of the existing `SettlementRecord`

| field | §0 class | source | null form |
|---|---|---|---|
| `controllerBefore`, `controllerAfter` | RECOMPUTED | public `EmpireQuery.TryGetController` on prev / next | `null` + `"no-control-row"` (retiring the `-1` sentinel for new fields) |
| `weatherRowPresent, weatherLogDeviation, weatherMultiplier` | READ | `HarvestWeatherRow` | `null` when absent |
| `cohorts[16]` | SUMMED | Σ `BucketRow.Count` per `CohortIdx`; bands from public `BandViews` | zeros + `"no-buckets"` |
| `vitalsRowPresent`, `vitalsDtYears` | READ | `SettlementVitalsRow.DtYears` — **the settlement's own rate basis across an era transition, serialized today and dropped by the Observer** | `null` when absent |
| `deathsNatural`, `deathsStarvation` | **GAP** | — | `null` + `"gap.deathSplit"` → **S3** |
| `goodOpening[]` per good | READ | `prev.GoodStockRow.Amount`. **The single highest-value cheap field in §8**: today only GRAIN has a per-settlement opening, so for the other 13 goods **no per-settlement closure identity can be formed at all** | `null` + `"no-good-row"` |
| `goodFill[]` per good | RECOMPUTED | **public static** `NeedsGrievanceSystem.Fill(in GoodStockRow)` **[L]** | `null` + `"no-good-row"` |
| `happiness.value` | RECOMPUTED | **public** `SettlementHappiness.Of(next, id, cfg)` **[V:169]** | `null` + `"nonfinite"` |
| `happiness.valuePrev` | RECOMPUTED | **public** `SettlementHappiness.Of(**prev**, …)` — **the value migration's viability consumed** **[L: MigrationSystem.cs:232]**. Carried **beside** the existing next-world reading, never replacing it | `null` + `"nonfinite"` |
| `happiness.factors[2]`, `factorsPrev[2]` | RECOMPUTED | **public** `SettlementHappiness.Factors` **[V:95]** | `null` |
| `happiness.foodBranch` | READ (branch label) | one of `"deficit-row-present"` \| `"no-deficit-row"` \| `"deficit-nan"` — **distinguished by INSPECTING THE ROW'S PRESENCE, never by re-running the function.** Closes G8: a 1.0 from "zero deficit" and a 1.0 from "no row yet" are different claims | never null |
| `happiness.housingBranch` | READ (branch label) | `"dwellings-and-population"` \| `"no-population"` \| `"no-housing-row"`. **A dead settlement now reads `"no-population"` instead of a misleading sufficiency of 1.0** | never null |
| `happiness.comfortParticipates`, `happiness.tierAGateApplied` | READ (**stated facts**) | both `false` — `Of` calls `NeedsAggregation.Aggregate` **directly** **[V:183-184]**, not the wrapper that applies the Tier-A gate first. Emitted so a reviewer never reads happiness as the needs aggregate | never null |
| `happiness.weights`, `.sigma`, `.floor`, `.span`, `.rho`, `.rawAggregate`, `.selfCheck` | **🚫 GAP — REMOVED FROM THIS PROPOSAL** | — | `null` + `"gap.happinessDerivation"`. **See §8. Design B proposed these; the challenge refuted them and I verified the refutation.** → **S15** |
| `grainCapacityUnits`, `spoiled`, `overflowed` | **GAP** | — | `null` + `"gap.granaryCapacity"` → **S19** |
| `nonGrainCapacityModelled` | READ (**stated fact**) | `false` — `BoundStore` is called for grain only **[V]**. **"Not applicable", not "not recorded"** | never null |
| `structures[]`, `structuresInert` | READ + stated fact | `StructureRow`; `structuresInert: true` because **no system reads it** — building a granary changes nothing mechanically. Emitted so a reviewer does not infer a mechanism that does not exist **[L]** | `[]` |
| `attractivenessOpening` | READ | `prev.SmoothedAttractiveness` | `null` + `openingRecorded:false` |
| `attractivenessClosing`, `gapBasis:"closing"` | READ | **`next.SmoothedAttractiveness` — THE VALUE THE MECHANISM USED.** `MigrationSystem` writes the EMA at `:254` and reads the updated array at `:360, :401, :416` **[V]**. The shipped Observer publishes the prev value for both **[V: Observer.cs:574-581]** | `null` + `closingRecorded:false` |

## 4.8 `rec: "move"` — §9

| field | §0 class | source | null form |
|---|---|---|---|
| `popOpening`, `popClosing` | SUMMED | Σ `BucketRow.Count` + Σ `NotableRow.Count` on prev / next | never null |
| `residual` | **RESIDUAL** | `Opening + Births − Deaths + Inflow − Outflow − Closing`, the identity string reused verbatim from `Observer.ColonistsDepartedIdentity`; **absorbs colonization only** | never null |
| `identity` | READ (token) | `"pop.v1"` → `run.identities` | never null |
| `balances` | DERIVED verdict | `residual == 0` on any turn no settlement was founded. **Exact integer equality** | never null |
| `pushDeficitRatio`, `pushRowPresent` | READ | `prev.ConsumptionDeficitRow`. **`pushRowPresent` closes the ambiguous zero** — `Observer.cs:571-573` writes `0.0` for an absent row with no flag **[V]** | `null` when absent |
| `foodGate` | **GAP** | — | `null` + `"gap.foodGate"`. The predicate is a **private inline expression with no public accessor**; its two INPUTS are already recorded. **The record refuses to copy the predicate** — the existing `SettlementRecord` already documents exactly this decision **[L]**. → **S16** |
| `destinations` | **GAP** | — | `null` + `"gap.pairwise"`. **YOUR HEADLINE QUESTION.** → **S1** |
| `category` | **GAP** | — | `null` + `"gap.movementCategory"` → **S1/S2** |
| `categoriesModelled` | READ (stated fact) | `["migration","colonization","births","deaths","classMobility","cohortAging"]` | never null |
| `categoriesAbsent` | READ (**stated fact — an AUTHORITATIVE NEGATIVE**) | `["forcedDisplacement","war"]`. **No code path in `Sim.Core` forcibly relocates people; `RevoltSystem` moves nobody; `AppropriationSystem` moves GRAIN.** This lets a reviewer eliminate two of your six categories **by evidence rather than by silence** | never null |

**Deliberately absent:** births, deaths, inflow, outflow — the existing `PopulationSection` carries them.

## 4.9 `rec: "decide"` — §7, and `rec: "order"` / `"refusal"` / `"event"`

### `decide`

`on: "prev"` (READ constant) — `ClassMobilitySystem` evaluates against **prev** `VariableRow`s **[V:182]**,
flagging the one-turn lag that telemetry's economy section (read from next) silently imposes.

`ev[]`, one entry per predicate in a fixed integer order:

| predicate | result class | null / gap form |
|---|---|---|
| `revolt.ready` | **RECOMPUTED** — public `SettlementHappiness.IsRevoltReady` with public `RevoltThreshold` (0.0) **[V:70,220]** | never null |
| `control.controls` per (polity, settlement) | **RECOMPUTED** — public `EmpireQuery.ControlsSettlement` | never null |
| `trade.scope` per realised flow | **RECOMPUTED** — public `TradeScopes.Classify` → Unruled/Domestic/Foreign **[L]** | never null |
| `class.emerge` / `class.recede` **inputs** | **READ** — `VariableRow.Value` per variable id, on **prev** | `null` + `present:false` per input |
| `class.emerge` / `class.recede` **latch before/after + transition** | **READ** + **DIFFERENCED** — `ClassStateRow.Active` | `null` + `"no-class-state-row"` |
| `class.emerge` / `class.recede` **RESULT** | **🚫 GAP in this proposal** | `null` + `"gap.predicateComposition"`. **CORRECTION — both designs got this wrong and I verified it.** `Predicate.Evaluate` is public **[V: Predicate.cs:44]**, but it takes a `VariableReader` delegate that the system builds from **`private static PrevVariable`** (which carries a private missing-row convention, `return 0.0`), gated by **`private static HasPublished`**, composed as an **else-if chain** **[V: ClassMobilitySystem.cs:180-186, :283, :290]**. Calling `Evaluate` with an observer-built reader is a **copy of private logic**, not a RECOMPUTED. → **S17** |
| `colony.inherit` | **GAP** | `predicateInputRecorded:false` + `"gap.colonyParent"`. The predicate is public and recomputable **in principle**, but its INPUT (the source settlement id) is never recorded, so the record refuses to evaluate it |

`latchMeansPresence: false` (READ, stated fact). Lane 3 measured **141 of 2,400 settlement-turns with
`Active == 0` while artisans still had members** (the class drains slowly after a recede) and 3 with
`Active == 1` and zero members **[L]**. The record carries **both** the latch (READ) and the headcount
(SUMMED) so the two are never conflated.

### `order` — §6

`logIndex` · `issuedOnTurn` · `deliveredToStep` (READ — **stated as the executor's rule**
`BatchFor(prev.Clock.Turn)` **[V]**, not computed as a guess) · `actor` (READ; **it IS the issuing Empire's
PolityId**) · `actorCommandSource` (RECOMPUTED via public `EmpireQuery.TryGetCommandSource` **evaluated as of
that turn**, never assumed constant; `null` + `"no-polity-row"`) · `kindId`, `kind` · `targetRaw` ·
`targetSettlement`, `targetSector` (**RECOMPUTED via the public decoders `OrderApplied.Settlement`/`.Sector`
[V: TurnRecord.cs:140-151]** — closing G26, where `sim inspect` prints `target 88` raw **[V]**;
`null` + `"not-a-settlement-order"` for `SetRainBias`) · `amount` · `consumerPresent` (READ stated fact:
**`false` for `SetRainBias`**, because `WeatherSystem` is not in the production pipeline, so such an order is
delivered and silently ignored **[L]**) ·
`accepted` → **🚫 GAP**, `null` + `"gap.orderAcceptance"` → **S9** ·
`effect[]` → **DIFFERENCED**: for Labor/SectorAllocation the `PolicyChange` rows; for `EnqueueConstruction`
the `ConstructionQueueRow` diff. `[]` + `effectState:"no-observable-change"`.

> **The case this makes visible.** Lane 4 measured five turn-0 orders declaring **exactly** `Sectors.Default`
> and producing **zero** `PolicyChange` rows **[L]**. `effect:[]` + `"no-observable-change"` distinguishes
> *"applied, value unchanged"* from *"never applied"* — today indistinguishable.

### `refusal` — SESSION-ONLY, `repro:false`

`source:"ui"` · `reason ∈ "settlement-not-present" | "batch-invalid"` · `requestedKind`,
`requestedSettlement`, `requestedAmounts` — all READ of the **UI's own parameters**.

> **This is the one order-refusal record that is NOT blocked, and separating it from **S9** is a design
> contribution both designs missed.** `UiSession.EmitLaborOrder` returns silently and `EmitSectorOrders`
> returns `false` **before anything reaches the `OrderLog`** **[L]**. That refusal is a **UI behaviour**, not
> gameplay state and not an observation of the simulation: recording it needs no new state, breaches no §0
> rule, and **changes no order semantics — the order still does not happen.** The **system-side** drops
> remain blocked as **S9**.

### `event` — §10 (your item 26)

`type` (READ — a **stable token**, not prose) · `settlement` (READ — **a stable id**; the prose chronicle
keys by generated display **name** **[V: ChronicleProse.cs:30]**) · `turn` (READ — **the prose drops it
entirely**) · `year`, `magnitude1`, `magnitude2` (READ, **UNROUNDED**; the prose rounds all three to whole
numbers **[V: :56-66]**, destroying deficit ratios and migration shares at 1% granularity) ·
`reconstructibleFromTurn0Only: true` (READ stated fact — `ChronicleCollector`'s latches are per-settlement
mutable tracks and **a mid-game load starts them fresh** **[L]**, so an event stream is rebuildable from turn
0 and from nowhere else).

Vocabulary is **six types only**: Founding, FamineOnset, FamineEnd, Extinction, FirstArtisans,
MigrationSurge **[V]**. **Nothing about polities, control, revolt, colonization, trade or orders is an
"event" anywhere on this tree.** Control changes are carried on `turn.controlChanges` as a **DIFFERENCE**,
not invented as events.

## 4.10 `rec: "phase"` — OPT-IN, DEFAULT OFF

`system`, `position` (READ) · `populationAfter`, `goodStocksAfter[]` (SUMMED) · `ledgerCumulative[]` (READ).

Mechanism: `ITurnObserver.OnPhaseState(phase, IReadOnlyWorldState next)` already exposes the
work-in-progress world between systems; today the only implementation is a bench observer that records
ticks and bytes and never touches state **[V: TurnExecutor.cs:38-49, :111-118]**. **No kernel change.**
This is the **only** record kind that attributes a world-level delta to a **named system** — the "SYSTEM
CALCULATION" link of your causal chain.

> **⚠️ TWO HARD RULES, and they are contract-level, not advisory.**
> 1. **`OnPhase(string, long elapsedTimestampTicks, long allocatedBytes)` is NOT a default method** **[V:33]**
>    — any `ITurnObserver` must implement it and will be handed a `Stopwatch` tick delta 17× per turn
>    **[V: :112-118]**. **Neither argument may EVER reach the artifact.** One implementer writing
>    `elapsedTicks` into a record "because it was free" destroys the byte-reproducibility the whole layer
>    depends on. Asserted by test **T7**.
> 2. **No test in this repository proves that an ATTACHED `ITurnObserver` leaves the world bit-identical.**
>    The executor's own doc only claims the phases run identically **when `observer` is null** **[V: :43-47]**,
>    and the sole `ITurnObserver` in the test tree is a bench-totals collector that asserts no hash **[V]**.
>    A **new attached-observer hash-twin test (T6) must land BEFORE this stream ships.**
>
> Secondary, not a hash issue but worth stating: attaching an observer changes the next phase's allocation
> reading, so a forensic run and a bench run contaminate each other's allocation numbers.

## 4.11 The gap catalogue (emitted in `run.gapCatalogue`)

Each token carries `{what, whereComputed, whereDiscarded, whatWouldCloseIt}` — the machine-readable form of
`docs/observability-architecture.md` §8, so a reviewer can **enumerate** what the evidence cannot establish.

`gap.pairwise` · `gap.movementCategory` · `gap.deathSplit` · `gap.granaryCapacity` · `gap.colonyParent` ·
`gap.founderPolity` · `gap.raid` · `gap.builtDecayed` · `gap.goodSinks` · `gap.cohortVitals` ·
`gap.classMobilityFlow` · `gap.cohortAging` · `gap.foodGate` · `gap.orderAcceptance` · `gap.controlReason` ·
`gap.productionFactors` · `gap.tradeEligibility` · `gap.constructionResolution` ·
**`gap.predicateComposition`** (new, from the challenge) · **`gap.happinessDerivation`** (new, from the
challenge) · `gap.rngDraws` · `gap.grievanceAttribution` (**not a missing field — a missing concept**).

---
# 5. Identity strategy

## 5.1 The run id

`runId` = first 16 hex chars of SHA-256 over the canonical, length-prefixed concatenation, in this fixed
order:

```
seed (u64 LE) | sizePx (i32 or "absent") | settlementsOverride (i32 or "absent") | founded (u8)
| canonicalSchemaVersion (i32 = 24) | configDigest (32 B) | ordersDigest (32 B)
```

**Deliberately EXCLUDED, each for a stated reason:**

- **Any wall-clock value.** `startedAtLocal` is `repro:false` and outside the digest, so the run id is
  byte-reproducible. This is the one property the manifest does **not** have today: its filename stamp is a
  `DateTime.Now` read, so two otherwise-identical sessions produce different manifest bytes **[L]**.
- **`codeDigest`** (assembly MVIDs). **Excluding it is the load-bearing choice.** A correct replay on a
  *different build* must reproduce the *same* `runId`, or the id stops being a join key the moment you
  rebuild. A code mismatch is reported separately, on comparison. (And see **W6** in §15 — I have not
  verified that this repository's builds are deterministic in the MVID sense, so `codeDigest` is provenance
  only and must be measured before it is trusted.)
- **`buildSha`**, because it is `"dev"` on a locally built `Sim.Ui` **[L]** and would make every locally
  played session collide.
- **The turn count**, which is an outcome, not an input, and lives on `close`.

**Consequence, which is the point.** `sim forensic --verify` recomputes the `runId` from
(manifest + orders file + this build's configs) and compares. **A mismatch becomes a NAMED, TYPED FINDING** —
*"this orders log was not the one this session played"* or *"this build's `sim.json` differs from the one
recorded"* — instead of today's failure mode, where a swapped companion surfaces as `REPRODUCTION FAILED`,
which reads as a determinism defect and is not one (G16).

## 5.2 The composite key

Every record is addressed by **`(runId, rec, turn, subjectId)`**:

| rec | subjectId |
|---|---|
| `run` / `close` / `turn` / `network` | none |
| `site` / `catchment` / `place` / `move` / `decide` | `SettlementId.Value` |
| `order` | the **order-log index** (`OrderApplied.Index` — the row's position in the log **[V]**) |
| `event` | (settlement id, event type token) |
| `phase` | pipeline position (the integer index into `run.pipeline`) |
| `refusal` | a monotone per-session counter, `repro:false` |

**Total** (every record has one), **stable** (all integers from serialized rows), **unique** (asserted by
test **T4**). **No display name appears in any key or any value.**

## 5.3 Cross-artifact binding — by CONTENT, not by filename

```
forensic.run.runId            ← the root key
forensic.close.artifacts[]    ← {role, file, bytes, sha256} for all six files
forensic.turn.turn            ↔ telemetry line's turn.turn                (one-to-one)
forensic.turn.hashAfter       ↔ trace row's hash for the SAME turn        (byte equality; a mismatch is a finding)
forensic.place.settlement     ↔ telemetry settlements[].settlement        (one-to-one on the same turn)
forensic.order.logIndex       ↔ telemetry turn.orders[].index             (one-to-one)
forensic.event.settlement     → NameRegistry(seed, id) → the chronicle .txt line   (ONLY this direction works)
forensic.turn.hashBefore(N)   ≡ forensic.turn.hashAfter(N-1)              (asserted; self-verifying chain)
```

**The manifest is NOT modified.** A `session-manifest/v3` is rejected by every existing binary (**S22**), so
the join runs **forensic → manifest**: the `run` record carries the manifest's own path and SHA-256. I flag
as a **separate, smaller ruling** that adding a `forensicFile` field to a future manifest v3 would let
`sim inspect --manifest` discover the forensic file without a stamp convention. **The design works without
it.**

## 5.4 No transient object references, no nondeterministic timestamps

Every field is a scalar, a string, or an array of scalars. No record holds a world, a row struct, a span or
a system. This already holds today — `ObservationLog` retains neither world **[L]** — and this design does
not weaken it. Two independent `sim inspect --telemetry --report-jsonl` runs at `7c91cbf` were measured
byte-identical, with **zero** timestamp-shaped tokens in the telemetry, the replay report or the hash-log
**[L]**.

---

# 6. Turn/state reconstruction strategy

## 6.1 Four rules, and a reviewer needs no others

**RULE 1 — Per-turn records are SELF-CONTAINED.** `turn`, `place`, `move`, `decide`, `order`, `event` are
complete on their own line. No carry-forward, no delta chain, no reconstruction state. Losing one line loses
exactly one turn of one subject.

**RULE 2 — Sparse records carry forward.** `site`, `catchment`, `network` are emitted at founding and on
change; the reader's rule is *"the state of X on turn N is the most recent X record with turn ≤ N."*
**This is a SPARSE SNAPSHOT stream, not a delta stream** — every record is a full, standalone statement of
its subject, so there is no chain to break and no "my delta chain is corrupt" failure class.

**RULE 3 — The hash chain is the state anchor.** Because `hashBefore(N) ≡ hashAfter(N−1)`, the file carries
a complete, verifiable chain from turn 0 to turn T, at **one hash computed per turn**. **The forensic file
is never a source of truth**: it is a pure function of the replay, and the hash chain is what proves the
replay a reviewer ran is the session that was played.

**RULE 4 — Every join is KEYED BY TURN, never positional.** This fixes G17 by contract. `Divergence` today
compares `played[i]` against `replayed[i]` and never asserts the turns agree **[V: Program.cs:485-491]**;
the forensic reader keys on `turn` and reports a **missing turn as a missing turn**.

## 6.2 The hash is CALLER-SUPPLIED — and this is a real implementability constraint one design missed

```
Sim.Core/Kernel/WorldHash.cs:14        public static byte[] Compute(WorldState world)          ← CONCRETE
Sim.Core/Kernel/CanonicalSchema.cs:143 public static void Write(WorldState world, BinaryWriter) ← CONCRETE
Sim.Core/Observability/Observer.cs:56  Observe(IReadOnlyWorldState prev, IReadOnlyWorldState next, …) ← INTERFACE
```
**[V, all three]**

**The world hash cannot be computed from inside the observer as written.** Design A presents
`worldHash | READ | WorldHash.ComputeHex(next)` as a free field; it is not. The hashes (and the terrain)
must be **caller-supplied**, which means the forensic entry point's signature is
`Observe(prev, next, cfg, orders, terrain, hashBefore, hashAfter)`. This mirrors the discipline
`SessionManifest` already uses — it takes its timestamp from the caller rather than reading a clock inside
`Sim.Core` **[L]**.

**And the hash is free in both paths that matter:** the live UI already computes one per turn for the trace,
and `sim inspect` already computes one per turn via `SessionTrace.Line` **[L]**.

## 6.3 Two reconstruction paths, held provably equivalent

- **PATH 1 (authoritative, slow):** replay from turn 0 through the observer. Lane measurements: ~12.5 s for
  a 60-turn session, ~50 s for 650 turns — **because every query re-replays** **[L]**.
- **PATH 2 (derived, fast):** read the forensic file. Lane measured a reader proxy at **46 ms full-parse,
  1 ms with early exit** **[L]** — a 270×–12,000× improvement for the same answer.

**Test T5 asserts the equivalence**: replay-produced records must be byte-identical to the file's lines for
the same session, so Path 2 can never silently drift from Path 1.

## 6.4 One honest limit

`reconstructibleFromTurn0Only` is carried on every `event` record because `ChronicleCollector`'s latches are
per-settlement mutable tracks that a mid-game load starts fresh **[L]**. **Events are reconstructible from
turn 0 and from nowhere else.** The record says so rather than implying otherwise.

---

# 7. Causal/event strategy

## 7.1 The causal chain, link by link, against your stated principle

> BEFORE STATE → INPUT/ORDER/EXOGENOUS CHANGE → SYSTEM CALCULATION → DECISION/PREDICATE → STATE CHANGE → AFTER STATE

| link | where it lives | status |
|---|---|---|
| **BEFORE STATE** | `turn.hashBefore`, `place.*Opening`, `move.popOpening`, `attractivenessOpening`, existing telemetry openings | **COVERED** |
| **INPUT / ORDER** | `order` (decoded, with command source and effect), `refusal`, `place.weather*` (the exogenous term), `site.deposits` | **COVERED for accepted orders**; refusals partly (**S9**) |
| **SYSTEM CALCULATION** | `phase` (opt-in) at world granularity; every stored *input* of every equation | **PARTIAL.** The equation internals are transient and refused (**S18**). This is the weakest link and I do not claim otherwise |
| **DECISION / PREDICATE** | `decide` — 3 predicates cleanly, 1 classified, 1 blocked on a missing input, 4 refused, **1 corrected to a GAP by the challenge** | **PARTIAL** |
| **STATE CHANGE** | `turn.controlChanges`, `order.effect`, `event`, the ledger accounts in existing telemetry | **COVERED at world level**, partial per settlement (**S7**) |
| **AFTER STATE** | `turn.hashAfter`, `place.*Closing`, `move.popClosing`, `move.balances` | **COVERED** |

## 7.2 Events: structured, id-keyed, unrounded — and the prose file untouched

`ChronicleEvent(Type, Turn, Year, SettlementId, Magnitude1, Magnitude2)` **already exists and is already
produced every turn** **[V: ChronicleCollector.cs:18-20]**. It is then **discarded**: what reaches disk is a
rendered sentence keyed by display name, with the turn dropped and year and both magnitudes rounded to
whole numbers **[V: ChronicleProse.cs:26-34, 56-66]**.

**Proposal: emit the structured event into `turn.events[]` and leave `chronicle-<stamp>.txt` exactly as it
is.** Two audiences, not duplication — prose for a human reading the annals, ids and unrounded magnitudes
for a reviewer joining to telemetry. This closes G19 and item 26 with a **pure READ**, and it simultaneously
makes the events reconstructible headlessly (today `Chronicle` has **0 references in `Sim.Cli`** **[V]**).

## 7.3 The largest cheap win: route the existing Explain layer headlessly

`CausalChain`, `HappinessExplanation`, `MigrationExplanation`, `GrievanceExplanation` and `Levers` live in
`Sim.Core/Observability/Explain/` — inside the CI allowlist — and are consumed **only by `Sim.Ui`**
**[V: 0 references in `Sim.Cli`]**. **The deepest causal machinery in the tree is screenshot-only, which is
exactly what your objective rules out.**

**Proposal: `sim inspect --settlement S --turn N --explain happiness|grievance|migration|chain`,
recomputed on demand from a replay to turn N. NOT stored per turn.** Storing it would duplicate data your
constraints forbid duplicating and would roughly double the line. **Honest tradeoff:** stored facts are read
in milliseconds from the file; explanations cost a replay measured in seconds, and **a reviewer handed only
the file, with no build and no ability to run the CLI, gets the facts and the gap catalogue but not the
causal chains.** If your external reviewer is truly offline, that is a real limit of this design.

## 7.4 The correction the challenge forced, stated plainly

**Both designs claimed the artisan/merchant latch result is a clean RECOMPUTED through the public
`Predicate.Evaluate`. It is not, and I verified it.** The leaf call is public **[V: Predicate.cs:44]**; the
**composition** is not:

```
ClassMobilitySystem.cs:180  if (HasPublished(prev, settlement))                         ← private static  [V:283]
ClassMobilitySystem.cs:182  Predicate.VariableReader read = varId => PrevVariable(prev, settlement, varId);
                                                                     ↑ private static, private missing-row
                                                                       convention "return 0.0"             [V:290]
ClassMobilitySystem.cs:183-186  if (active == 0 && emerge) active = 1;
                                else if (active == 1 && recede) active = 0;             ← an ELSE-IF chain
```
**[V]**

An observer that builds its own reader writes the private default into itself. **The record therefore emits
the predicate INPUTS (READ), the latch before/after (READ) and the transition (DIFFERENCED), and declares
the RESULT a GAP** until **S17** is ruled. An implementer following either original contract would have
shipped the copy.

---
# 8. Happiness forensic strategy

## 8.1 What happiness actually is, verified from source

`Sim.Core/State/SettlementHappiness.cs` **[V, whole file]**. **Exactly two factors** (`FactorCount = 2`):

- **Factor 0, FOOD** — raw input `ConsumptionDeficitRow.DeficitRatio`; normalised `Clamp(1 − deficit, 0, 1)`;
  **three distinct branches**: row present, **no row at all → 1.0**, NaN deficit → 0.0.
- **Factor 1, HOUSING** — raw inputs Σ`BucketRow.Count`, `HousingRow.Dwellings`, `cfg.Housing.PersonsPerDwelling`;
  normalised `Clamp(dwellings × ppd / pop, 0, 1)`; **three branches**: normal, **pop ≤ 0 → 1.0**,
  people-but-no-row → 0.0.
- **Weights** come from `WeightOf(cfg, SustenanceNeedId, 1.0)` and `WeightOf(cfg, ShelterNeedId, 0.9)`.
- **Aggregation** is `NeedsAggregation.Aggregate(factors, weights, agg.Sigma, agg.SatisfactionFloor)` —
  **called DIRECTLY**, not through the wrapper that applies the Tier-A gate first **[V:183-184]**.
- **Normalisation** is `floor = Clamp(agg.SatisfactionFloor,0,1); span = 1 − floor;
  normalized = (aggregate − floor)/span`, then `Clamp(normalized × 100, 0, 100)` **[V:207-211]**.
- **Water / clothing / taxation are deliberately absent**, and **Comfort is not a factor**. Happiness is
  structurally a **different number** from the needs aggregate.

## 8.2 🚫 THE CHALLENGE REFUTED DESIGN B'S HAPPINESS BLOCK, AND I VERIFIED THE REFUTATION

Design B proposed emitting `weights[2]`, `sigma`, `floor`, `span`, `rho`, `rawAggregate` and a bit-exact
`selfCheck`, arguing the weights are "a CONFIG READ for ids 1 and 2" and `span` is "a config-derived
constant". **The challenger rebuilt happiness observer-side from those ingredients and matched every
sub-100 record to `|Δ| = 0.000e+00` — and correctly reported that as the proof of the violation, not a
defence**, quoting your constraint: *"An observer-side copy of private simulation arithmetic is forbidden
EVEN WHEN NUMERICALLY EXACT."*

**I verified every private member the copy needs [V]:**

| what the copy needs | status at `7c91cbf` |
|---|---|
| `WeightOf(SimConfig, int needId, double fallback)` | **`private static`**, line **234** |
| `SustenanceNeedId = 1`, `ShelterNeedId = 2` — **the factor→need MAPPING** | **`private const`**, lines **226–227**. "Ids 1 and 2" is **not a READ of config; it is a copy of a private mapping applied to config** |
| `floor = Math.Clamp(agg.SatisfactionFloor, 0, 1)` and `span = 1.0 - floor` | **inline LOCALS inside `Of`**, lines **207–208**. `1.0 - floor` is **arithmetic**; there is no way to emit `span` without computing it. A sixth field kind |
| `rho = (sigma - 1.0)/sigma` | an **inline LOCAL inside `Aggregate`**, `NeedsAggregation.cs:135`, never returned |

**`selfCheck` is the tell.** You cannot assert *"(rawAggregate − floor)/span × 100, clamped, equals the
value to the bit"* unless you have already copied the normalisation. Design B markets the self-check as the
safety feature; **it is the evidence.**

**Two further defects in that block that neither design saw, and I confirmed both:**

1. **TWO DIFFERENT FLOORS SHARE ONE NAME.** `Of` uses `Math.Clamp(f, 0, 1)` **[V:207]**; `Aggregate` uses
   `Math.Max(1e-12, Math.Clamp(f, 0, 1))` **[V: NeedsAggregation.cs:171]**. Design B emits **one** `floor`
   field. At `satisfactionFloor == 0` they differ. **The observer would be modelling arithmetic it cannot
   see all of.**
2. **A THIRD FACTOR BREAKS IT, AND THE CODE INVITES ONE.** `SettlementHappiness.cs:71-75` says the `Factor`
   enum exists *"so that adding 'water' later is an additive change"* **[V]**. And `Aggregate`'s
   accumulation loop indexes `weights[i]` for `i < satisfactions.Length` **with no length guard** — unlike
   the saturation loop above it, which **does** guard **[V: :151-155 vs :172-179]**. An observer that
   hardcodes two weights would either throw or silently drop the third factor.

## 8.3 What this proposal therefore emits

**DELIVERED (all §0-clean):**

- `happiness.value` — RECOMPUTED, **public** `Of(next)`.
- `happiness.valuePrev`, `factorsPrev[2]` — RECOMPUTED, **public** `Of(prev)` / `Factors(prev)`. **The value
  migration's viability actually consumed.** Carried **beside** the next-world reading, never replacing it.
- `happiness.factors[2]` — RECOMPUTED, **public** `Factors`.
- `happiness.foodBranch` / `housingBranch` — **READ branch labels, determined by inspecting ROW PRESENCE,
  never by re-running the function.** Closes G8. A dead settlement reads `"no-population"` instead of a
  misleading `sufficiency: 1.0`.
- `happiness.comfortParticipates: false`, `tierAGateApplied: false` — READ stated facts, so no reviewer
  reads happiness as the needs aggregate.
- `run.registries.needs[{id, name, weight, bound}]` and `run.needsTuning{sigma, satisfactionFloor, …}` —
  a **verbatim config dump**, emitted **with no assertion about how happiness uses them.**

**REFUSED, declared `gap.happinessDerivation`:** weights-as-used, `rho`, `span`, the pre-normalisation raw
aggregate, and any self-check. → **S15**.

## 8.4 The consequence, stated without softening

> **For every happiness value other than exactly 0 or exactly 100, the number is NOT VERIFIABLE from the
> artifact — before this proposal and after it.** A reviewer can see both factors, both branch labels, the
> needs registry and the aggregation tuning, and can see that the score is consistent with them. He cannot
> *prove* it without re-deriving the private mapping and the private normalisation.

The fix is cheap, safe and **already has precedent in this tree** — the T4.19-A2 treatment applied to
`NeedsGrievanceSystem`, measured zero behaviour change, hash-verified. Publishing
`SettlementHappiness.Weights(SimConfig, Span<double>)`, a raw-aggregate accessor and
`Normalize(double, AggregationTuning)` moves **no value** and changes **no behaviour**, and turns G6 from
"impossible without a formula copy" into RECOMPUTED. **It is a change to `Sim.Core/State`, so it needs your
ruling: S15.**

## 8.5 The read-isolation line

Verified: `Sim.Core/Observability/` is on the allowlist and `SocialSection` already reads `GrievanceRow` and
`NeedSatisfactionRow` and ships green **[V]**. A happiness forensic record may sit **beside** grievance and
need readings without breaching the gate. **The line that must not be crossed is
`Sim.Core/State/SettlementHappiness.cs` itself gaining such a read** — it is not allowlisted, it would trip
CI, and it would be a **behaviour change** because happiness feeds migration viability. **Nothing in this
proposal adds such a read.**

---

# 9. Migration forensic strategy

## 9.1 The decision, in execution order, and what survives it

| step | code | survives? |
|---|---|---|
| per-settlement prev signals (population, grain stock + last harvest, arable, deficit) | `MigrationSystem.cs:150-190` **[L]** | **YES** — all serialized |
| **absolute food gate** `anyFood = stock > 0 \|\| lastHarvest > 0` | `:174` **[L]** | inputs YES, **the boolean NO** (private inline expression, no public accessor) |
| **viability** `material × (1 − w + w×happiness01)` | `:225-235` **[L]** | inputs YES, **the product NO** |
| **EMA update** into the OWNED (next) table | `:244-255`, write at **`:254`** **[V]** | **YES** — `SmoothedAttractivenessRow` |
| **damping matrix** `exp(−travelCost / decayUnits)` | `:256-277` **[L]** | `TravelCost` YES, **the matrix NO** |
| unplaced-departure readout | `:289-344` **[L]** | **YES** — `BucketRow.UnplacedDeparture/Remainder` |
| **gap cap, equalizing m\*, gapScale** | `:354-382` **[L]** | **NO** |
| desired outflow per bucket | `:387-406`, reading **`smoothed[]`** at **`:360, :401, :416`** **[V]** | **NO** |
| **transfers, pinned order, remainder banked, clamp** | `:409-474` **[V: :455-475]** | **ONLY** `MigrationFlowRow.Inflow/Outflow` — two scalars |

## 9.2 🚫 Per-mover destination attribution does not exist in the simulation

**Said in those words, as instructed.** See the four structural reasons in the Honest Answer above. The
short form: the realised From→To amount depends on a **single mutating remainder accumulator**, an integer
**floor**, a **clamp measured after the fact**, and a **pinned order in which each transfer changes what the
next can draw** — and `Ledger.Transfer` **records nothing** **[V]**. It is the output of an ordered decision
sequence, not a function of stored state. **No pure function of (prev, next, cfg) reproduces it.
Reproducing it IS re-running `MigrationSystem`.** → **S1**.

## 9.3 🚫 And the plausible consolation prize is unsound — the challenge demonstrated it

Design A argued that ranking destinations by the pull the mechanism actually used recovers the causal story,
asserting of its own data that *"those top four are EXACTLY the four largest gainers that turn."* The
challenger tested that claim on a different turn — the **largest single movement in a real run, 217 people
leaving settlement 11 on turn 38**:

```
top-4 destinations by the pull the MECHANISM used:  3 (2.8746), 5 (2.8457), 10 (2.8449), 0 (2.8148)
top-4 actual gainers that turn:                     7 (+57),   4 (+42),   5 (+29),   10 (+28)
→ only 2 of 4 overlap; the top-ranked destination received 26; the largest gainer is not in the top four
```
**[L — the challenger's measurement; the structural reason below is verified [V]]**

**The reason is structural, not seed-specific:** inflow is driven by the **gap** `smoothed[dst] − smoothed[src]`
summed over **all** sources, damped by **per-pair** travel cost and scaled by the **per-pair** gap cap
**[V: :360, :401, :416]** — not by absolute attractiveness.

> **This proposal therefore DOES NOT RANK destinations and does not offer an inferred category.** Both would
> be a second migration calculation in spirit, and your constraint bans one. The record emits every stored
> **input** and declares the products, the eligible set and the destination a GAP.

## 9.4 What the design does deliver on migration

- Push driver with a **presence flag** (killing the ambiguous zero, G8/S21).
- **Both** attractiveness readings — `attractivenessOpening` (prev) and **`attractivenessClosing` (next, the
  value the mechanism used)** with `gapBasis:"closing"` — closing G5 **as an additive second READ**, not as
  a silent replacement of a shipped field.
- The full **travel-cost matrix**, per network revision.
- Every destination's food-gate inputs, deficit and happiness, so the reviewer can **bound** the candidate
  set himself.
- `UnplacedDeparture` / `UnplacedRemainder` (READ) — the simulation's own "wanted to leave, nowhere to go"
  signal.
- The **conservation identity** with its integer residual and a `balances` verdict.
- **Authoritative negatives**: forced displacement and war **do not exist at M4**.

## 9.5 The G5 finding, and the process rule that applies to it

I verified from source that the shipped Observer publishes a migration signal the mechanism did not use
**[V: Observer.cs:574-581 vs MigrationSystem.cs:254, :360, :401, :416]**, and two lanes plus the challenger
measured the divergence at **100% of comparable records** **[L]**. `MigrationExplanation` reads `next` and is
correct, so two T4.19 artifacts disagree about the same quantity.

> **Per `CLAUDE.md` and ADR-015 §6, this is a FINDING, not an authorised fix.** *"No finding is actionable
> before its verdict returns; applying a fix on a finder's word alone is a review bypass."* This proposal
> does **not** change `Observer.cs`. It adds the correct reading **beside** the existing one under a new
> name in a new artifact, which is safe in either direction. Correcting `Observer.Migration` itself should be
> its own packet, reviewed on its own evidence.

---

# 10. Order/AI/player strategy

## 10.1 There is one order pathway, and there is no AI

**One pathway, structurally.** `OrderLog` → `BatchFor(prev.Clock.Turn)` **[V: TurnExecutor.cs:100]** →
`OrderBatch` → `SimContext.Orders` → the three consuming systems. One delivery rule, one batch object; the
UI and the CLI feed the same `OrderLog` type. `CommandSource` lives on `PolityRow`, not on `OrderRecord`,
**by deliberate design** **[L]** — and is therefore **DERIVABLE per order**, legally and cheaply, via public
`EmpireQuery.TryGetCommandSource(world, order.Actor)`, **evaluated as of that turn and never assumed
constant.**

> **⚠️ THERE IS NO AI AT M4, AND ITEM 18 HAS NO SUBJECT.** `WorldgenConfig.AiEmpires` defaults to **0**
> **[V:55]** and `aiEmpires` is **absent from `worldgen.json`** **[V]**. The founded world has **one** Empire,
> PolityId 1, `CommandSource.Player`. **No system emits an order.** Every order in every order log in the
> repository is actor 1 **[L]**. **A forensic design budgeted for rich AI decision records would be building
> for state that does not exist.** Build the polity machinery because M5 will fill it — not because M4 has
> anything to put in it.

## 10.2 What the `order` record delivers

Full lifecycle for **accepted** orders: log index, issued turn, **delivered step stated as the executor's
rule**, actor, **command source recomputed for that turn**, kind, **decoded settlement and sector** (closing
G26), amount, `consumerPresent:false` for `SetRainBias` (whose consuming system is not in the production
pipeline, so the order is delivered and silently ignored **[L]**), and **`effect[]` DIFFERENCED** from the
policy rows and the construction queue.

**`PolicyChange` and `PolicyState` already exist, are already computed, and are written to NO artifact**
**[V: only `ObservationLog` + `Sim.Ui`]**. Surfacing them is pure plumbing over an existing pure function.

## 10.3 Order refusals — the split both designs missed

| refusal class | recordable? |
|---|---|
| **UI-side, pre-log** — `EmitLaborOrder` returns silently, `EmitSectorOrders` returns `false` **before the order reaches the `OrderLog`** **[L]** | **YES, as `rec:"refusal"`, `repro:false`.** It records the **UI's own action**, not an observation of the simulation. No new state, no §0 breach, **no change to order semantics — the order still does not happen** |
| **System-boundary, mid-turn** — five silent `continue`/early-return sites (`PathBuildSystem.cs:82, :94` **[V]**; `ConstructionSystem.cs:81, :93, :95` **[L]**) | **NO.** → **S9.** Recording it needs the consuming systems to publish outcome rows (new serialized state, entering the world hash) or the observer to re-implement each acceptance test (§0 violation) |
| **Fatal, whole-log** — `OrderValidation` / `OrderLog.Load` throw and the run aborts **[L]** | **NO artifact exists.** The message goes to the CLI error path. A `close`-record note is the most that can honestly be said |

**Two facts the record must state rather than let a reviewer infer:** there is **no player path for
`EnqueueConstruction` at all** (0 references in `Sim.Ui`/`Sim.Cli` **[L]**), and there is **no control check
on Labor/SectorAllocation at any layer** **[L]** — so a player can set sector policy on a settlement his
Empire does not control, reachable after a revolt. **That is a gameplay fact of the candidate, reported, not
changed.**

## 10.4 Two stale in-tree comments that will mislead an implementer

Not defects in behaviour — defects in the record, and worth a one-line correction in whichever packet
touches them **[L, both]**: `OrderValidation.cs:28-32` claims the `Polities` roster is never populated and
the actor check never fires (`WorldFounding` populates it, and lane 4 measured the check running live);
`AppropriationSystem.cs:100-101` claims colonization writes no control row and is the surviving route to
statelessness (`ColonizationSystem` now writes one; **revolt is the only route**).

---
# 11. Storage-size estimate

**Provenance note.** I did **not** re-run the size harness in this session; four independent lanes plus the
challenger measured it, on three different seeds, and **converge within ~3%**. Every figure below is marked
with its source and must be re-measured at implementation before it is quoted as a gate.

## 11.1 The shipped baseline, measured four times independently

| measurement | source | B/turn (12 settlements) | B per settlement-record |
|---|---|---|---|
| 120 turns, founded, seed 42 | challenger & Design B **[L]** | **74,108** | 5,394 (compact) / 6,176 (line ÷ 12) |
| 650 turns, founded, seed 1 | lane 5 **[L]** | **74,317** (file = **48,306,212 B**) | 5,459 |
| 40 turns, seed 12345 | lane 2 **[L]** | 72,376 | 5,397 mean |
| 40 turns, seed 42 | lane 1 **[L]** | 72,576 | 4,505–5,480 |

**Take 74,100 B/turn and ~5.4 KB per settlement-record as the working basis.**

## 11.2 ⚠️ The shipped §7 cost claim is WRONG, is cited in two documents, and must be corrected

`docs/observability-architecture.md:287-289` states a `SettlementRecord` is *"≈ 1.2 KB"* and a 12-settlement
× 1,730-turn campaign is *"≈ 25 MB in memory, ~40 MB JSONL. Trivial today."* **[V — I read the lines]**

| claim | measured | error |
|---|---|---|
| ≈ 1.2 KB per `SettlementRecord` | **~5.4 KB** | **4.5×** |
| ~40 MB JSONL per campaign | **~128 MB** | **3.2×** |
| ≈ 25 MB in memory | lane 5 measured **+132 MB RSS at 650 turns** → **~351 MB at 1,730** **[L]** | **14×** |
| "~200 MB in memory" at 100 settlements | ~**2.9 GB** on the measured basis **[L]** | same order |
| line 283: the UI does *"streaming append"* | **it does `File.Create` + `WriteAll` on every End Turn** **[V: UiSession.cs:325-326]** | **false** |

**Any proposal, packet or ADR citing 1.2 KB or 40 MB is citing a stale figure.**
`docs/observability-architecture.md` is a **T4.19 contract document** — per `CLAUDE.md`'s standing ban on
rewriting frozen documents, **the correction belongs in the ADR, not in an in-place edit**, unless you rule
the document editable.

## 11.3 The forensic stream's own cost

| | claimed by its design | **measured by the challenger** **[L]** |
|---|---|---|
| per turn (12 settlements) | 22,687 B | **27,198 B (+20% understated)** |
| 650 turns | 14.7 MB | **17.7 MB** |
| 1,730 turns | 39.2 MB | **47.1 MB** |
| **combined with telemetry, 650 turns** | — | **~66 MB** |
| **combined with telemetry, 1,730 turns** | — | **~175 MB** |

Record shares (measured): `place` ~1,020 B · `decide` ~658 B · `move` ~529 B · `turn` ~714 B ·
`network` ~5,290 B **per revision** (not per turn) · `site` ~722 B **once per settlement**.

**My synthesis strips fields** (the whole happiness derivation block, the `decide` predicate result) **and
adds a few** (per-good opening + fill, branch labels, `valuePrev`). I estimate it within ±10% of 27,198 B/turn
and **claim no better resolution than that.** Re-measure before quoting.

## 11.4 ⚠️ The refutation of Design A's headline cost claim

Design A stated **"+247 B/turn = +0.3%"** and **"+0.34% over a 1,730-turn campaign."** The challenger built
Design A's own stated field list onto 1,440 real settlement records and measured **[L]**:

```
settlement record:  v2 5,394 B  →  hoisted-only 3,918 B  →  Design A v3 6,525 B
DESIGN A CLAIMS  5,394 → 5,440  (+46 B).   MEASURED  +1,131 B  =  +21.0%
per turn: v2 73,748 → 86,700 B with the hoist approved (+17.6%);  106,302 B without it (+44%)
650 turns: 47.9 MB → 56.4 MB (hoist) or 69.1 MB (no hoist)
```

The decomposition is arithmetically obvious once stated: Design A's **`allAttractivenessUsed[12]` field alone
measured 526 B — eleven times its entire claimed per-turn delta.**

**And the framing is worse than the arithmetic.** The "+0.3%" nets a **permanent per-turn addition** against
a **one-time structural hoist** that Design A itself lists as **requiring a director ruling** (its R19 —
moving `telemetry/v2` → `v3`). **A headline cost figure must not be conditional on an unapproved contract
move.** If the ruling goes the other way the budget vanishes and the real cost is +44%.

## 11.5 Snapshot vs delta — FULL SELF-CONTAINED RECORDS, no delta

Measured input: **69.7% of leaf fields are unchanged from the previous turn** **[L]**. That is the entire
case for a delta scheme, and it is not enough:

1. **gzip beats it and is lossless.** Measured: 8,892,987 → **719,312 B = 8.1%** (12.4×) **[L]**. A
   field-level delta keeps **30.3%** — **~3.7× worse than a compressor one flag away**, while adding
   reconstruction state and an ordering dependency.
2. **A delta chain destroys the property the layer exists for.** Every turn line must be independently
   checkable against its own `worldHash`. Under a delta scheme line N is meaningless without lines 1..N−1,
   introducing a *"my delta chain is broken"* failure class **into the one artifact that must survive being
   handed to a stranger**, and making partial-file recovery impossible.
3. **The file is not information-dense, it is text-inefficient.** The full canonical **binary** snapshot of
   the entire world is ~127 KB **[L]**, against a ~74 KB JSON line for one turn. The right lever is
   **representation**, not structure.

**The lossless levers, measured, in order of preference:**

| lever | saving | forensic loss |
|---|---|---|
| hoist the four constant GAP/identity strings (each has **exactly one distinct value** across the file) | **16.1–16.7%** **[L]** | **zero** — and it makes the gap declarations a **first-class enumerable manifest** instead of hundreds of duplicated sentences |
| hoist registry names into a once-per-file header (~58,692 `"name"` occurrences) | **11.4%** **[L]** | **zero** — and it is your own "stable ids, never display names" requirement, so the saving and the correctness fix are the same change |
| gzip | **~92%** | zero, reversible, touches no record shape |

**This proposal applies both hoists to the NEW stream by construction.** Applying them to `telemetry/v2` is a
**separate small packet** carrying the tag move its own contract mandates (**S20**).

## 11.6 Runtime and write overhead — and the real binding constraint

**OBSERVATION cost is below the noise floor.** Lane 5, 11 interleaved runs each: replay-only 50,062 ms vs
replay+observe+write-48 MB 50,813 ms — **+751 ms = +1.50%** against a pooled noise floor of **2.1%** **[L]**.

**HASH cost is free at this scale, and one design over-claimed here.** Design B reported *"+343 ms over 120
turns = +2.86 ms/turn = +1.6%, hash > nohash in all four pairs."* The challenger's three interleaved pairs on
the same command gave **mean −110 ms, hash FASTER in 2 of 3**, inside a ~500 ms (2.4%) spread **[L]**. **The
honest statement is that hashing is below the resolution of this machine** — which strengthens the design,
but a design document that reports an unresolved signal as resolved should be corrected before it becomes a
cited figure. And the hash is **already paid** in both paths that matter (§6.2).

**⚠️ THE WRITE PATH IS THE BINDING CONSTRAINT, AND IT IS ALREADY BROKEN.** `UiSession.ExportTelemetry` does
`File.Create` + `TelemetryWriter.WriteAll` over the **entire in-memory history**, called from `SaveSession`
on **every `EndTurn`** **[V:322-327]**. Exact prefix-sum over real line sizes **[L]**:

| session length | final file | **bytes actually written** | amplification | cost of the LAST End Turn |
|---|---|---|---|---|
| 120 turns | 8.9 MB | 0.53 GB | 60× | — |
| **650 turns** | 48.2 MB | **15.68 GB** | **326×** | **one 48.2 MB synchronous serialize, 0.8–1.6 s** |
| 1,730 turns | 128.2 MB | **111 GB** | **866×** | one 128 MB serialize |

**This is quadratic in turn count, it sits in the interactive End Turn path, and every added field makes it
worse.** Design B called fixing it *"worth fixing in the same packet, not required by this design."*
**I disagree: it is required.**

> **RECOMMENDATION: fix `ExportTelemetry` to a true append BEFORE any forensic field list is approved.**
> The format is already append-safe JSONL; it is a one-method change in `Sim.Ui` with **no format
> implication, no gameplay implication and no schema implication.** It is the cheapest, highest-value item
> in this entire document.

## 11.7 Must forensic mode be opt-in?

| stream | default | why |
|---|---|---|
| `run`/`close`/`turn`/`site`/`catchment`/`network`/`place`/`move`/`decide`/`order`/`event`/`refusal` | **ON** | +31% on top of telemetry, observation cost below noise, and the layer is worthless if a reviewer must know in advance to ask for it. **A forensic record that has to be switched on is not evidence; it is a debugging aid.** |
| `phase` | **OPT-IN, DEFAULT OFF** | Its cost is **unmeasured** (SUMMED table scans ×17/turn); its non-interference is **untested** (T6); and if per-phase **hashes** were ever wanted, the measured upper bound is 16 × ~2.9 ms = ~46 ms/turn ≈ **+26%** **[L]** |
| `rngStreams[]` on the turn line | **ON, but first to cut** | ~540 B/turn ≈ +0.75%, answers item 23 only at end-state granularity, and its `Region` column is polysemous enough to mislead a reviewer reading it cold |

**The forensic writer is STREAMING.** Each record is built from (prev, next, cfg, orders, terrain, hashes),
written, and **dropped** — so it does **not** inherit `ObservationLog`'s unbounded growth (G23). The bounded
window the shipped design specified and never implemented is applied to the new stream **from day one**.

---
# 12. Reconstruction/query interface

## 12.1 The fact that decides this section

**THERE IS NO TELEMETRY READER ANYWHERE IN THE REPOSITORY.** The artifact is write-only. Consequently
`sim inspect` answers **every** question by replaying the entire session — measured at ~12.5 s for a 60-turn
session and ~50 s for 650 turns **[L]**. And `--telemetry` exists **only** on `sim inspect`, which **requires**
a manifest and a trace; `sim run` and `sim replay` cannot emit one, and **no CLI verb can write a manifest, a
trace or a chronicle at all** **[L, V for chronicle]**. Every lane had to **hand-author session metadata** to
measure anything.

> **Adding fields without adding a reader would add data no tool can read.** A reader proxy answering the
> identical query over the already-written file measured **46 ms full-parse / 1 ms with early exit**
> **[L]** — a 270×–12,000× improvement. This is the single largest usability win in the proposal.

## 12.2 Emission — so a forensic run is reproducible headlessly

```
sim run    … --forensic OUT.jsonl [--phases]
sim replay … --forensic OUT.jsonl [--phases]
sim inspect --manifest M --forensic OUT.jsonl
sim run/replay … --emit-session DIR      writes manifest + trace + forensic together
```

`--emit-session` closes the hole that makes headless forensics impossible today. (The chronicle stays UI-only
unless `ChronicleCollector` is wired into `Sim.Cli` — a separate decision; the **structured** events reach
the forensic stream either way.)

## 12.3 Reading — the reader the telemetry has never had

| command | answers |
|---|---|
| `sim forensic --file F --verify` | recomputes `runId` from (manifest, orders, this build's configs) and compares; checks the hash chain; checks `turn.counts` against records present; checks `close` is present; checks every gap token resolves; checks the field dictionary is **total** over emitted keys. Exit 0/1 with the **first failure named BY TURN, never by index** |
| `--rec place --settlement 11 --from 100 --to 200` | **one settlement across a turn range** — a query the current CLI cannot express at all |
| `--turn 186 --rec place,move,decide,order` | all subjects at one turn (today `--turn N` prints **world totals only**) |
| `--where 'move.outflow>100'` `--select turn,settlement,…` | predicate filter + projection. The two filters that matter most are **already computable from fields in the file**: `turn.allAccountsReconcile==false` and `turn.unattributedGrainTransfer==true` |
| `--top N --by FIELD` | ranking — **composite `(value, id)` key with a stable integer tie-break, shipping a tie-dense test (T17)** per `CLAUDE.md` |
| `--csv --rec place --field happiness.value` | tabular series output. The UI has 12 series keys; the CLI exposes **none** **[L]** |
| `--gaps` | prints `run.gapCatalogue` — **what this evidence CANNOT establish. A reviewer's first command** |
| `--fields` | prints `run.fieldDictionary` — every field with its §0 kind and source citation |
| `--events` / `--polity` / `--orders --limit N` | structured events; polity snapshot and control changes across turns; **raises or removes the hard cap of 20 order-turns** **[L]** |
| `sim inspect … --explain happiness\|grievance\|migration\|chain` | routes the existing `Sim.Core/Observability/Explain/` layer headlessly (§7.3) |

## 12.4 The CI constraint that shapes where the code lives

`scripts/check-read-isolation.sh` greps `Sim.Core`, `Sim.Data` **and `Sim.Cli`** for
`\bGrievances\b|\bNeedSatisfactions\b|\bGrievanceRow\b|\bNeedSatisfactionRow\b`, minus a **path** allowlist
that includes `Sim.Core/Observability/` and **does not include `Sim.Cli`** — and **the script's own header
states it cannot distinguish code from PROSE** **[V]**.

**Therefore: the reader and every query primitive live in `Sim.Core/Observability/`, and `Sim.Cli` stays a
thin flag-parsing shell in which no identifier, string OR DOC COMMENT may contain those four tokens.**
Verified as compatible: the existing record properties are `SocialSection.Grievance` and `.NeedSatisfaction`
(**singular**), which do not match the gate's word-boundary plural patterns **[V]**. **This must be
re-verified by RUNNING the gate, not assumed** (test **T18**).

## 12.5 Two existing defects the query surface must not inherit

- **The positional join** (G17) — `--verify` keys on `turn`.
- **The undecoded order target** (G26) — the `order` record and every print path carry the decoded settlement
  and sector.

I do **not** propose fixing `Sim.Cli/Program.cs:485-491` inside a forensic packet; it should be its own
packet, reviewed on its own evidence.

---

# 13. Test plan

> **⚠️ NUMBERING CAVEAT: your literal §18 list was not in my packet.** The nineteen requirements below are
> reconstructed from `CLAUDE.md`'s standing rules (the POPULATED-table rule and its T1.1/T1.3 precedent; the
> order-delivery turn-exact rule and its T1.9 precedent; the composite-key/tie-dense rule; the exact-equality
> rule), from the two designs, and from the challenge. **Each is stated by substance as well as by number, so
> a re-map against your real §18 costs a table edit.** If your §18 contains a requirement I have not
> reconstructed, this plan does not cover it.

| # | Requirement | Test |
|---|---|---|
| **T1** | **Determinism** | The same session, written twice, is **byte-identical**. (Already true of `telemetry/v2`; measured byte-identical across two `sim inspect` runs at `7c91cbf` **[L]**.) |
| **T2** | **No nondeterministic timestamp in a simulation record** | Zero timestamp-shaped tokens (`20\d\d-\d\d-\d\d`, `\d\d:\d\d:\d\d`) on any `repro:true` record. Asserted over a full generated file |
| **T3** | **POPULATED-record round-trip, EVERY kind** | Per `CLAUDE.md`: exact expected field set, **bit-exact round-trip** writer→reader, hash equality. **Empty-record coverage proves nothing (T1.1/T1.3 precedent)** — every kind must be exercised with real data, including `event`, `refusal`, `network` and the founding-turn `site` |
| **T4** | **Key totality and uniqueness** | Every record has a total `(runId, rec, turn, subjectId)` key; no duplicates in a generated file |
| **T5** | **Path-1 / Path-2 equivalence** | Records produced by replay are byte-identical to the file's lines for the same session, so the fast read path can never silently drift from the authoritative one |
| **T6** | **NON-INTERFERENCE with an ATTACHED observer** | World hash after N steps **bit-identical with and without a forensic observer attached**. **THIS TEST DOES NOT EXIST TODAY**: the executor claims identity only when `observer` is **null** **[V:43-47]**, and the sole `ITurnObserver` in the test tree asserts no hash **[V]**. **This test must land BEFORE the `phase` stream ships** |
| **T7** | **Timing/allocation arguments never reach the artifact** | `OnPhase`'s `elapsedTimestampTicks` and `allocatedBytes` **[V:33]** must not appear in any record. Asserted structurally and by scanning a generated file |
| **T8** | **§0 field-dictionary totality** | Every key the writer emits has a `fieldDictionary` entry with a kind ∈ READ/SUMMED/DIFFERENCED/RESIDUAL/RECOMPUTED/GAP. **A field added without one fails the build.** This turns §0 from a convention into a gate |
| **T9** | **Gap-catalogue totality** | Every `gap.*` token emitted resolves to a catalogue entry carrying what/where-computed/where-discarded/what-would-close-it |
| **T10** | **Null policy** | No non-finite double and **no `"NaN"` string** on any path; no in-band sentinel on any new field; absence is **always** `null` + a state token. Property-tested across a generated file |
| **T11** | **Order-delivery TURN-EXACT pin** | `issuedOnTurn` vs `deliveredToStep` gets its **own turn-exact test**. `CLAUDE.md` is explicit (T1.9 precedent): **replay equality proves reproducibility, not semantics**, and live-vs-replay comparison alone cannot see stamping drift |
| **T12** | **Conservation and residual identities, exact, no epsilon** | Σ per-settlement `storeLosses` == ledger spoilage+overflow **every turn** (lanes measured 120/120 and 650/650 **[L]**); `move.residual` == 0 on every turn with no founding (measured 1,440/1,440 **[L]**); Σ inflow == Σ outflow exactly |
| **T13** | **Hash chain and keyed trace join** | `hashBefore(N) == hashAfter(N−1)` for every turn; forensic `hashAfter` == the trace's hash for **the same turn number**, joined **by turn, never by index** (G17) |
| **T14** | **Cross-artifact invariants** | `forensic.place.happiness.value` == `telemetry.social.happiness` for **every** settlement-turn; `forensic.order.logIndex` joins one-to-one with `telemetry.turn.orders[].index` |
| **T15** | **Run-id reproducibility and sensitivity** | Same inputs → same `runId`; changing **any one** of seed/overrides/founded/schema version/config digest/orders digest → **different** `runId`; changing only the wall clock → **SAME** `runId` |
| **T16** | **Registry stability** | The good/class/need registries are fixed at config load and **no system adds a member mid-run**. The header hoist depends on this; if it ever becomes false the names must return to the rows |
| **T17** | **Deterministic ordering** | (a) A **tie-dense** test for every ranking in the query surface, proving the `(value, id)` composite key with a stable integer tie-break. (b) A structural assertion that **the WRITER contains no ordering or argmax over a double** — all writer ordering is by integer id or table index |
| **T18** | **Gates green** | `check-banned-constructs.sh`, `check-read-isolation.sh` (**re-run after adding the reader — `Sim.Cli` is scanned and not allowlisted, and the gate matches PROSE**), `check-readonly-proof.sh`, `dotnet build`, `dotnet test`. All green on the candidate today; I ran the first two **[V]** |
| **T19** | **Honest degradation** | Absent rows, a trimmed needs registry, a **dead settlement** (measured: 83 dwellings, 0 people, `sufficiency: 1.0` today **[L]**), and **turn 1 with no `SmoothedAttractivenessRow`** (the sole source of the 12 `"NaN"` strings **[L]**) each produce an **explicit state, never a wrong number**. A truncated file is detected via `turn.counts` and the missing `close` line |

**Additive, never a substitute.** Per `CLAUDE.md`, definition of done is the packet's stated acceptance
criteria; these tests are the floor, not the ceiling.

---
# 14. Exact files expected to change

**Nothing below has been touched. No production file was written, nothing was committed beyond this
document, nothing merged, nothing pushed to `main`, `m5-full-build` untouched.**

## 14.0 Phasing — so you can approve part of this

| phase | what | needs a STOP ruling? | why this order |
|---|---|---|---|
| **P0 — PREREQUISITE, no new fields** | Fix `UiSession.ExportTelemetry` to a **true append**. | **No** | 326× write amplification and a 48 MB synchronous serialize on the last End Turn at 650 turns is **already a defect**, and every added field makes it worse. **Cheapest, highest-value item in this document.** |
| **P1 — IDENTITY** | `run` + `close` records; config digests, pipeline order, era table, hash algorithm, registries, terrain hash, artifact content hashes, field dictionary, gap catalogue, null policy. | **No** | Closes G13/G14/G15/G16/G18/G23-provenance — **the largest provenance holes** — with **pure READs of config already in memory**. No new state, no tag move, no hash move. **If you approve nothing else, approve this.** |
| **P2 — PER-TURN RECORDS** | `turn`, `site`, `catchment`, `network`, `place`, `move`, `decide`, `order`, `refusal`, `event` — **minus every refused field**. | **No** | The forensic substance. Every field is READ/SUMMED/DIFFERENCED/RESIDUAL or a **genuinely public** RECOMPUTED. |
| **P3 — READER AND QUERY** | `ForensicReader`, `ForensicQuery`, the `sim forensic` verb, `--emit-session`, `--explain` routing. | **No** | Without it the data is unreadable except by a 50-second replay. Includes **routing the screenshot-only Explain layer headlessly** — the highest value-to-risk item in the tree. |
| **P4 — SEPARATE RULINGS, each its own packet** | **S15** (happiness statics) · **S16** (migration gates) · **S17** (class-mobility composition) · **S18** (production/housing/construction statics) · **S20/S21** (telemetry v3 null convention + the 28% constant/name hoist) · **S23** (gate-verdict producer) · the G5 `Observer.Migration` read-source correction · the G17 keyed-join fix. | **Yes, each** | Each is a simulation-side visibility change or an artifact-contract move. Each needs a hash-equality proof and a verdict. **Each converts specific GAPs into RECOMPUTED fields.** |
| **P5 — DECISION ONLY** | **S1–S14, S19, S22.** New authoritative serialized state. | **Yes** | Not implementable inside an observability packet. **S1 is the only thing that answers your worked example.** |

## 14.1 NEW files — all under `Sim.Core/Observability/Forensic/`

**This placement is load-bearing, not cosmetic:** `check-read-isolation.sh` allowlists
`Sim.Core/Observability/` **by path prefix**, so the subdirectory inherits it **[V]**. Anywhere else in
`Sim.Core`, or in `Sim.Cli`, trips the gate.

| file | phase | contents |
|---|---|---|
| `ForensicSchema.cs` | P1 | the `forensic/v1` tag; the record-kind enum; the **field dictionary**; the **identity catalogue** (reusing `Observer.ColonistsDepartedIdentity` / `StoreLossesIdentity` **verbatim**); the **gap catalogue** |
| `ForensicRecords.cs` | P1/P2 | the record types, each field carrying its §0 class in a doc comment (the existing `SettlementRecord.cs` house style) |
| `ForensicObserver.cs` | P1/P2 | the pure function `Observe(IReadOnlyWorldState prev, IReadOnlyWorldState next, SimConfig cfg, OrderApplied[] orders, TerrainSet? terrain, string hashBefore, string hashAfter)` — **hashes and terrain caller-supplied (§6.2)** |
| `ForensicWriter.cs` | P1 | JSONL writer that **enforces the null policy structurally**: one `Value(json, name, double?, stateName, state)` helper and **no path that can emit a non-finite double** |
| `ForensicReader.cs` | P3 | **the reader that does not exist anywhere in the repo today.** Tolerant of unknown keys, **strict on an unknown tag** (the `SessionManifest.Read` discipline: refuse an unknown vintage loudly) |
| `ForensicQuery.cs` | P3 | range / projection / filter / top-N with a `(value, id)` stable tie-break — **kept in `Sim.Core/Observability` so `Sim.Cli` never names the gate's four tokens** |
| `ForensicLog.cs` | P2 | the append seam + a bounded window behind an `IForensicHistory` interface mirroring `IObservationHistory`. **Streaming by default: records written and dropped** |
| `ForensicPhaseObserver.cs` | **P4 / opt-in** | an `ITurnObserver` beside the existing bench observer, using the existing `OnPhaseState` seam. **No kernel change.** **Blocked on T6** |

## 14.2 MODIFIED files

| file | phase | change |
|---|---|---|
| `Sim.Ui/UiSession.cs` | **P0** | `ExportTelemetry` → **true append** (the one-method fix). |
| `Sim.Ui/UiSession.cs` | P1/P2 | append one forensic line per turn; emit `refusal` records at the three pre-log sites; pass the trace's **already-computed** hash to the forensic writer so no second hash is taken |
| `Sim.Ui/SimUiGame.cs` | P1 | wire the forensic file into `SaveSession` and the launch path |
| `Sim.Ui/Program.cs` | P1 | record the **UTC offset** beside the existing local stamp |
| `Sim.Cli/Program.cs` | P1–P3 | `--forensic`, `--phases`, `--emit-session` on `run`/`replay`/`inspect`; the new `forensic` verb and its flags; `--explain`. **Binding constraint: no identifier, string or comment containing `Grievances` / `NeedSatisfactions` / `GrievanceRow` / `NeedSatisfactionRow`** |

**NOT MODIFIED, deliberately:** `Sim.Core/Kernel/SessionManifest.cs` · `SessionTrace.cs` · `OrderLog.cs` ·
**`CanonicalSchema.cs`** · `WorldHash.cs` · `Snapshot.cs` · `ReplayReport.cs` ·
`Sim.Core/Observability/{Observer,TurnRecord,SettlementRecord,TelemetryWriter}.cs` · **every file under
`Sim.Core/Systems` and `Sim.Core/State`** · `Sim.Data/content/*`.

> **`CanonicalSchema` stays at Version 24. No world hash moves. No golden moves. No existing artifact's tag
> moves. No existing reader breaks. No test goes red. That invariant is the first thing a reviewer of the
> implementing packet should check.**

## 14.3 TESTS — `Sim.Tests/Forensic/` (new)

T1–T19 of §13. Two of them are **prerequisites, not deliverables**: **T6** (attached-observer hash twin)
must land before the `phase` stream, and **T18** (gates, re-run) before handback.

## 14.4 DOCS

| file | change |
|---|---|
| `docs/m4-forensic-observability-proposal.md` | **this document** (the only file this packet writes) |
| `docs/adr/adr-NNN-forensic-record-layer.md` | **NEW, REQUIRED.** This extends the §0 observability contract with the **field-dictionary** and **gap-catalogue** obligations, and `CLAUDE.md` is explicit that a packet touching a contract writes an ADR. **It must also carry the corrected storage figures** (§11.2) |
| `docs/forensic-contract.md` | NEW — the record contract as the reviewable artifact |
| `docs/observability-architecture.md` | **DO NOT EDIT IN PLACE WITHOUT A RULING.** It is a T4.19 contract document and `CLAUDE.md` bans rewriting frozen documents. Lines **283** and **287–289** are measurably wrong (§11.2). The correction goes in the ADR unless you rule the document editable |
| `docs/session-records.md` | says "Four files in `runs\`"; there are five today and would be six |
| `docs/telemetry-schema.md` | only if **S20** is approved |
| `docs/queue.md` | one line for anything noticed and deliberately not done |

---

# 15. Confirmation that simulation behaviour remains untouched

**This is a proposal; nothing has been implemented, so this section states what the design GUARANTEES and how
each guarantee would be PROVEN, not what has been observed of an implementation.**

## 15.1 The five structural guarantees

1. **NO GAMEPLAY STATE IS ADDED OR CHANGED.** `CanonicalSchema` stays at **Version 24**. No `WorldState`
   table gains a field; no table is added. **All 39 `WorldState` tables are serialized, which I verified
   exhaustively [V]** — so this is not an assumption, it is a checked invariant. Every world hash is
   unchanged; every golden, corridor pin, determinism pin and **your recorded playtest traces** remain valid.
2. **NO MECHANIC, COEFFICIENT OR THRESHOLD IS TOUCHED.** No change to happiness, migration, food, density,
   Artisan thresholds, population dynamics, AI behaviour or player order semantics. No future-age needs, no
   dietary diversity, no era-dependent needs, no universal CapabilitySystem, no God object, no direct
   sibling-system call, no change to state ownership. **P1–P3 modify no file under `Sim.Core/Systems` or
   `Sim.Core/State` at all.**
3. **THE OBSERVER CANNOT WRITE.** The forensic entry point takes `IReadOnlyWorldState` on both sides, so a
   write **does not compile** — the guarantee `check-readonly-proof.sh` already enforces (it reports
   *"mutation attempts fail to compile (CS0200, CS1061)"* **[L]**). The observer runs **after** `Step`,
   outside the pipeline, and **no system consults it**.
4. **NO NEW ARITHMETIC.** Every non-READ/SUMMED/DIFFERENCED value is a call to a named **public static** the
   simulation itself calls: `SettlementHappiness.Of` / `.Factors` / `.IsRevoltReady`, `EmpireQuery.*`,
   `TradeScopes.Classify`, `CatchmentSystem.SizeTier`, `NeedsGrievanceSystem.Fill`, `BandViews.*`,
   `Sectors.Share`, `OrderApplied.Settlement`/`.Sector`. **Exactly two residuals, both pre-existing, both
   carrying their identity string.** The `run.fieldDictionary` makes this **machine-checkable** (T8).
5. **THE REFUSALS ARE THE PROOF.** Every place where a second simulation would be required — migration
   viability and the pairwise matrix, the food gate, granary capacity, trade eligibility, the production
   Leontief branch, appropriation's victim argmax, **the happiness weights and normalisation**, **the
   class-mobility predicate composition** — is a `null` plus a gap token, not a number. **An observer that
   was quietly recomputing would have no gaps.**

## 15.2 How it would be proven

- **T6** — world hash bit-identical with and without a forensic observer attached. **This test does not exist
  today and is a prerequisite, not a deliverable.**
- **T1 / T5 / T13** — determinism, path equivalence, self-verifying hash chain.
- **T8 / T9 / T10** — §0 totality, gap totality, null policy, enforced as build gates.
- **T18** — all four gate scripts plus build and test, re-run before handback. **`sim bench` is not required:
  no hot path is touched — the observer runs outside `Step`.** If the opt-in `phase` stream is ever built,
  `sim bench` **is** required, and its cost is currently **unmeasured**.

## 15.3 ⚠️ Known ways this guarantee could be broken by a careless implementation

Stated so a reviewer of the implementing packet knows exactly where to look:

1. **Writing `OnPhase`'s timing or allocation arguments into the artifact** — destroys byte-reproducibility.
   Banned by contract, asserted by **T7**.
2. **Building a `VariableReader` for `Predicate.Evaluate`** — copies `PrevVariable`'s private missing-row
   convention. Declared a GAP (§7.4); would silently become a §0 violation if an implementer "helpfully"
   fills it in.
3. **Emitting `span`, `rho` or the happiness weights** "because they are config" — they are not (§8.2).
4. **Reconstructing granary capacity through the `ConsumeRemainder` identity** — observer-side arithmetic over
   a three-writer field that a **permitted data edit** can invalidate (**S19**).
5. **Naming any of the four gate tokens in `Sim.Cli`, including in a comment** — the gate matches prose **[V]**.
6. **Adding a read of the needs tables to `Sim.Core/State/SettlementHappiness.cs`** — trips CI **and** is a
   behaviour change, because happiness feeds migration viability.

## 15.4 Honest weaknesses of this proposal

- **W1 — Your worked example still is not answered.** Destination and the authoritative category are `null`
  with gap tokens. **If that is your acceptance test, this proposal fails it**, and only **S1** passes.
- **W2 — More than half the grain stays in one unsplit residual** (≈52%), and **the cheap escape hatch both
  designs offered does not work** (**S19**).
- **W3 — Two artifacts will carry different null conventions** until **S20** is ruled. This inconsistency is
  created *by obeying you*, and it is not mine to fix in a proposal-only packet.
- **W4 — Happiness stays unverifiable** for every value other than 0 or 100 until **S15**.
- **W5 — Three size inputs are estimates, not measurements**: the `catchment` emission rate and the `network`
  revision count are **assumed** from an in-code comment that the size tier "changes a handful of times per
  campaign"; the `phase` stream's cost is **unmeasured**. `network` is O(n²) per revision, so if the network
  recomputes far more often than that comment suggests, this term grows fast at 100 settlements. **Measure
  before quoting.**
- **W6 — `codeDigest` rests on deterministic builds** being on (the .NET default). **I did not verify that
  this repository's builds are deterministic in the MVID sense.** It is deliberately excluded from the
  `runId` so a wrong answer there cannot break the join key, but it must be measured before it is trusted.
- **W7 — The line scales linearly in roster size.** Colonization can take the roster past 100 (T4.4 measured
  178 on a defective branch **[L]**), and **memory binds before disk does**. The bounded window is the
  precondition, not garnish.
- **W8 — A reviewer handed ONLY the file** — no build, no ability to run the CLI — gets the facts, the
  identity and the gap catalogue, but **not the causal chains**, which are recomputed on demand by design.
- **W9 — Roughly half my file:line citations are `[L]`**, carried from lanes, designs or the challenge. Per
  `CLAUDE.md` they are **secondary evidence and must be re-verified at implementation time.** I verified the
  load-bearing ones myself and marked them `[V]`; a packet written against this document must not treat the
  `[L]` citations as settled.
- **W10 — My §1 items 12–15, my §15 question list and my §18 test numbering are RECONSTRUCTIONS.** If your
  real lists differ, the substance still holds but the mapping needs your correction.

---

## Recommendation, in one paragraph

**Build the synthesis: one new artifact `runs/forensic-<stamp>.jsonl` (`forensic/v1`) with Design B's record
shape and blast radius, Design A's refusal discipline, and neither design's sizing.** Design B is the right
shape because it moves **no shipped tag**, and Design A's central structural claim — that the manifest is
append-safe — **is false**: `SessionManifest.Read` is a whitelist that throws, so a v3 manifest is rejected
by every existing binary. Design A's headline cost claim is wrong by ~70× and its destination-ranking
consolation is unsound. But Design A's **refusals** are the best judgement in either document, and the
challenge proved it by writing Design B's forbidden happiness copy and matching it to `|Δ| = 0`. **Approve
P0 first — the 326× write amplification is a live defect and the one-method fix costs nothing. Then approve
P1, which closes the largest provenance holes with pure config READs. P2 and P3 deliver the forensic
substance and the reader without which none of it is readable. Then rule on P4's visibility extractions,
each of which converts specific GAPs into RECOMPUTED fields. And rule separately on the P5 STOP list —
above all S1, because it is the only thing in existence that answers "where did Gapi's 163 people go."**

---

## This is a proposal. No implementation has been performed.

No file under `Sim.Core`, `Sim.Data`, `Sim.Cli`, `Sim.Ui`, `Sim.Tests` or `Sim.Ui.Tests` has been created,
modified or deleted. No part of this design has been implemented. No gameplay mechanic, coefficient,
threshold or serialized schema has been touched. `CanonicalSchema` remains at Version 24 and no world hash
has moved. Nothing has been merged; nothing has been pushed to `main`; `m5-full-build` was not touched. The
only change on branch `m4-forensic-proposal` is this document.

**Awaiting the director's ruling on the STOP LIST and on the phasing before any implementation begins.**
