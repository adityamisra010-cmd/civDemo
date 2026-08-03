# M4 — PRE-SPEC DEPENDENCY FILING (GOV-2)

**Docs-only filing, 2026-07-30, directed packet GOV-2.** The M4 spec will be the first written
under S8 §4.1 (`docs/spine-s8-governance-freeze.md:65` — "Effective from the M4 spec onward"),
whose four mandatory items include a coupling map (§4.1 item 4, `spine-s8-governance-freeze.md:183`).
This file is the INPUT to that map: it collects, with tree citations, everything the M4 spec author
would otherwise rediscover. **It observes; it designs nothing and amends nothing.** The rulings
recorded in §1 and §6 arrive via the GOV-2 directive and are FIRST RECORDED HERE (see §7, finding
F1); the corrections they imply land later, by ADR where a frozen document is touched.

Citation convention: `file:line` as measured in the tree at `gov-2-m4-dependencies` (cut from
`main` @ d51327e). Where a figure comes from a review record, the record is cited.

*Amended once under the GOV-2 extension (director, 2026-07-30, same session): §2d gains a third
consumer, §4 (three conflicts) and §5 (one measurement owed) are new, findings F9–F12 and their
provenance rows are added, later sections renumbered. This file is unmerged and unratified; the
in-place amendment is by the extension's explicit instruction. Nothing ratified is touched.*

---

## 1. TWO RULINGS TAKEN, ONE STALE TABLE

### 1a. MONEY — RULED: M5 TAXES IN KIND (Option B). First recorded here.

**The gap the ruling closes.** D-030 defers money: *"Money as an institution is deferred to its
own milestone"* (`docs/m3-spec.md:8`), repeated by the M3 scope fence — *"money, credit, banking
(own milestone)"* (`m3-spec.md:21`). The Spine schedules **"State, taxation, authority — M5"**
(`docs/civ-sim-architecture-v3-outline.md:82`; Tier 2: *"M5 — The governing loop. Taxation,
budget…"*, `civ-sim-architecture-v3-outline.md:109`). No money milestone exists anywhere in the
ladder (D-011 §6 table, `docs/d011-battle-layer-addendum.md:49-59`). M5 therefore taxes before
money exists.

**The ruling (director, recorded verbatim in intent):** M5 taxes in kind. Money is NOT folded
into M5 and does NOT get an inserted milestone before it; it remains deferred as D-030 states, to
a real milestone later in the ladder. Rationale, recorded: historical accuracy is the deciding
criterion — coinage dates from roughly 630 BCE in Lydia; Mesopotamia ran on barley and weighed
silver as units of account without coin; Old Kingdom Egypt was in-kind and corvée throughout.
D-030's own warrant is Mesopotamian barley accounting (`m3-spec.md:8`). A state that taxes grain,
goods and labour before it taxes silver is the honest model, and money then arrives as an
institution that EMERGES — the project's grammar (law 4, no calendar gates: coinage must derive
from computed state, never from a date; `CLAUDE.md` law 4, `civ-sim-architecture-v3-outline.md:22`).

**THE COST, ENUMERATED — every ratified line that assumes a money stock or reads as
money-denominated.** Under the ruling these become granaries, warehouses and rations at M5. This
list is the M5 spec author's rewrite inventory. Observed only; nothing is rewritten here.

*Lines that assume a treasury/money STOCK directly:*

| # | source | the line |
| --- | --- | --- |
| 1 | `docs/d009-d010-map-population-addendum.md:37` | "Overthrow propensity = f(elite grievance, military loyalty, legitimacy, **treasury health**, …)" |
| 2 | `docs/d009-d010-map-population-addendum.md:43` | paralysis remediation menu: "**buy off leaders (treasury)**" |
| 3 | `docs/m0-kernel-spec.md:12` (D-005, frozen kernel decision) | "**Money as `long` minor-units of an abstract currency**" — a ratified representation commitment for the stock itself |
| 4 | `docs/m0-kernel-spec.md:11` (D-004) + `civ-sim-architecture-v3-outline.md:19` (Law 1) + `CLAUDE.md` law 1 | money named as a conserved stock class ("people, **money**, goods") |
| 5 | `civ-sim-architecture-v3-outline.md:36` | numeric policy names money among clamped exponential processes ("population, **money**, prices") |
| 6 | `civ-sim-architecture-v3-outline.md:37` and `:143` | "**money-unit strategy across eras**" (S3 units; pre-M0 decision 7) |
| 7 | `civ-sim-architecture-v3-outline.md:93` | "Finance (banking, debt, panics) — era exp." — presupposes money exists before the early-modern expansion |

*Lines with money-denominated income/flow language that need an in-kind reading at M5:*

| # | source | the line |
| --- | --- | --- |
| 8 | `docs/d018-classes-and-needs.md:23,24,25` | Clergy "**stipend**/tithe", Soldiers "**stipend**", Bureaucrats "**stipend**" as income types |
| 9 | `d018-classes-and-needs.md:19,20,27` | Laborers "wage", Artisans "skilled wage/own-shop", Aristocracy "land rent" |
| 10 | `d018-classes-and-needs.md:21` | Merchants: "trade profit"; anger: "tax strikes, **funding opposition (treasury**, politics)" |
| 11 | `d018-classes-and-needs.md:22` | Capitalists: "capital profit"; anger: "**coup finance**" |
| 12 | `d018-classes-and-needs.md:62` | Influence = f(class **wealth share**, …) |
| 13 | `docs/d021-stability-doctrine.md:22,25` | valve 2 "Participation costs **wages**…"; valve 5 movements need "**funding**"; leaders "are **bought**" |
| 14 | `d021-stability-doctrine.md:34,37` | "institutional recruitment **budgets**"; "**wage differentials** pull migration" |
| 15 | `docs/d035-needs-aggregation.md:82` | coupling path 2: purchased needs draw on "one goods-and-income pool … carrier: **a purse**" |
| 16 | `docs/d037-emergent-polities.md:170,183-185` | E2 "**purchase**" (of claims); E4 espionage may "**FUND** AND ARM … **BUY** elites" |
| 17 | `civ-sim-architecture-v3-outline.md:109` | Tier 2 M5: "Taxation, **budget**, authority/bandwidth economy…" |

*Measured, for the M5 author's relief:* no money stock exists in code. The kernel registers no
money conservation quantity (`Sim.Core/State/Ids.cs:20` — "later people/money/goods"); the
auditor covers people and the 14 goods (`m3-spec.md:25`). Every money assumption above lives in
ratified prose, none in state.

**Forward scheduling item, recorded open, NO OWNER:** money still needs a milestone eventually —
coinage emerges well inside the 6,000-year campaign, and law 4 requires it to emerge from
computed state. The ladder's only money-adjacent row is "Finance … era exp."
(`civ-sim-architecture-v3-outline.md:93`), which presupposes money rather than introducing it.
See also finding F7 (§7): the vertical slice is described in the Spine as a "Full loop through
classical antiquity" (`civ-sim-architecture-v3-outline.md:113`), which spans the ruling's own
~630 BCE coinage date — the open item is therefore live before the slice, not after it.

### 1b. NOTABLES — RULED: SPLIT BY ROLE (Option B). First recorded here.

**What the ratified docs need, and when.** Three ratified documents require notables before the
inventory row schedules them ("Characters/notables — M10+ — light layer; no romance sim",
`civ-sim-architecture-v3-outline.md:92`):

- **M4:** D-011 §2 delegate-with-agency — *"skipping a battle hands it to the assigned general —
  their competence and traits parameterize the AutoResolver"* (`docs/d011-battle-layer-addendum.md:30`).
- **M6:** D-011 §2 — BattleSetup carries "general (notable) stats" (`d011-battle-layer-addendum.md:26`);
  BattleOutcome returns "general experience" (`:27`).
- **M8:** D-021 Part 2 valve 5 — *"Escalation beyond riot requires movements: leadership
  (notables), cohesion, funding"* (`d021-stability-doctrine.md:25`), landing at M8 per Part 4
  (`:44`); and D-010's paralysis menu — "scapegoat/purge ministers (notables)"
  (`d009-d010-map-population-addendum.md:43`).

**The ruling (director, recorded):** M4 ships GENERALS ONLY — the minimal named actor
delegate-with-agency requires. M6 extends them with battle-relevant stats and experience. M8 adds
political notables and movement leadership. The residual light-character layer stays latest, per
the inventory row's "light layer; no romance sim". Rationale, recorded: each milestone builds only
what it needs, and D-010 already states the mechanism — notables emerge FROM aggrieved buckets
(*"the demagogue who leads the uprising emerges from the aggrieved bucket, named, with traits"*,
`d009-d010-map-population-addendum.md:29`) — so a general emerging from a soldier bucket at M4 is
that same mechanism arriving early, not a new one. Anonymous general-quality (the rejected third
option) was rejected because delegating to a number is not delegating to someone; the weight of a
general you know is the point of the mechanic — and it is what D-011's delegate-with-agency would
have lost.

**THE RETROFIT HAZARD IS THE WHOLE RISK OF THIS OPTION.** Whatever M4 ships must carry the
identity and trait fields the later roles need, or this becomes a D-037-shaped problem (D-037 A3:
"retrofitting it later is prohibitively expensive", `docs/d037-emergent-polities.md:23`). The
fields, enumerated from the ratified docs:

| field | source |
| --- | --- |
| stable id; the NAME in an id-keyed registry, never in the row | ADR-001: rows are `unmanaged` structs, "Names, text, and variable-length data live outside sim tables, in id-keyed registries" (`docs/adr/adr-001-unmanaged-table-rows.md:27`); standing practice `Sim.Core/State/Ids.cs:80`; T2.9 name precedent `Sim.Core/Chronicle/NameRegistry.cs` (chronicle-lite + names, `docs/m2-spec.md:40`) |
| home bucket link | D-010: notables emerge FROM buckets (`d009-d010-map-population-addendum.md:29`) |
| traits and competence | D-011 §2 (`d011-battle-layer-addendum.md:30`) |
| mutable experience | D-011 §2: BattleOutcome returns general experience (`d011-battle-layer-addendum.md:27`) |
| lifecycle: death, defection, purchase, falling out | D-021 valve 5: "leaders die, defect, are bought, or fall out" (`d021-stability-doctrine.md:25`) |

**NAMED CHECK for T4.1** (M4 foundations audit, S8 §4.1 item 1, `spine-s8-governance-freeze.md:73`):
the M4 general row carries every field above, verified against this table — the same treatment
D-037's data model gets in §2a. This is the item whose omission makes the split expensive.

**ONE UNANSWERED DESIGN QUESTION, recorded, not answered — IS A NOTABLE A PERSON?** D-010 says
they emerge from the bucket. If a notable remains counted in their bucket, the notable is a LABEL
and there is no conservation surface. If they are extracted, that is a `Ledger.Transfer` and
notables become a conserved population stock with births, deaths and a Law 1 audit
(`CLAUDE.md` law 1). Those are different systems and the choice is expensive to reverse. Owner:
M4's foundations audit (T4.1).

**Note:** this ruling resolves what previously blocked it — notables no longer arrive wholesale
at "M10+", so the stale inventory row (1c) is no longer load-bearing for this decision. 1c's
reconciliation remains REQUIRED, but for that row its content changes: the row should describe
the residual character layer, not where notables land.

### 1c. THE SPINE'S SYSTEM INVENTORY IS STALE BY ONE MILESTONE FROM M6 ONWARD

D-011 §6 resequenced the ladder — Battle Layer inserted at M6, everything after shifted, with
explicit mappings: M7 knowledge "was M6", M8 politics & diplomacy "was M7", M9 society layer
"was M8", M10 Ancient Vertical Slice, M11+ era expansions
(`d011-battle-layer-addendum.md:49-59`). The system inventory table
(`civ-sim-architecture-v3-outline.md:71-96`) was never patched. Verified row by row:

| inventory row | inventory says | under D-011 §6 | verified |
| --- | --- | --- | --- |
| Knowledge & diffusion | M6 (`:84`) | M7 ("was M6", `d011:55`) | stale by one |
| Politics deep (institutions, regime change) | M7 (`:85`) | M8 ("politics & diplomacy, was M7", `d011:56`) | stale by one — **row omitted from the director's reading; see finding F3** |
| Diplomacy | M7 (`:86`) | M8 (`d011:56`) | stale by one |
| Religion & culture | M8 (`:87`) | M9 ("society layer", `d011:57`) | stale by one |
| Health & disease | M8 (`:88`) | M9 (disease named in the M9 row, `d011:57`) | stale by one |
| Environment & climate | M9 (`:89`) | **not named by D-011 §6** — M9 is now the society layer; ambiguous between M10 slice content and M11+ era expansions | AMBIGUOUS, stated as such |
| Military full (ops, siege, naval) | M9+ (`:90`) | **not named by D-011 §6**; §5 keeps naval auto-resolve "until post-slice" (`d011:45`); era expansions are M11+ (`d011:59`) | displaced/ambiguous — **row omitted from the director's reading; see finding F3** |
| Espionage/intel uncertainty | M10+ (`:91`) | M10 is now the slice, M11+ the expansions (`d011:58-59`) | ambiguous — under the PRE-D-011 scheme the slice sat at M9 (`civ-sim-architecture-v3-outline.md:113`) and M10+ meant era expansions, so "M10+" does NOT read the same under both schemes; **see finding F4** |
| Characters/notables | M10+ (`:92`) | same ambiguity as above; after the 1b ruling this row should describe only the residual character layer | see 1b |

Two additional measured observations:

- **The staleness is not confined to the inventory table.** The Tier 2 build-spine list carries
  the same pre-D-011 ladder — M6 Knowledge, M7 Politics & diplomacy, M8 Society, M9 Slice, M10+
  expansions (`civ-sim-architecture-v3-outline.md:110-114`).
- **The stale numbering propagates into new ratified documents.** D-037 (ratified 2026-07-26,
  months after D-011) internally mixes the two schemes: E3 cites the "M6 battle layer"
  (`d037-emergent-polities.md:176`, post-D-011 numbering) while its header, C7 and E2 cite
  "M7 (diplomacy)" (`d037:4,119,170`, pre-D-011 numbering).

**The governance nuance the tree already contains (finding F5):** S8 §5 rules *"Where addenda
amend the v3 outline (region-graph clause, **milestone renumbering**, walking-skeleton content),
**the addendum governs** — append-only audit trail, no retro-editing"*
(`spine-s8-governance-freeze.md:202`). The table is therefore governed-stale, not untracked-stale
— D-011 §6 is authoritative — but a reader of the table alone still reads wrong numbers, which is
precisely how D-037's mixed citations happened.

**§7.12 instance against the Spine, the THIRD of its shape:** (1) CLAUDE.md's merge-loop line was
false for eleven merges (*"it survived eleven merges because the document asserting it is also the
document nobody re-measures"*, `docs/adr/adr-015-verification-hygiene.md:645`); (2) ADR-015 §7.7
carried a refuted mechanism inside its own ratified text (*"the 'land heterogeneity' attribution
slipped in unmeasured, was refuted at T3.4b …, re-confirmed at T3.4c …, and still propagated to a
downstream misreading because it sat in this ratified section"*, `adr-015:361-375`); (3) now the
inventory table. All three are stale claims surviving in documents nobody re-measures because
they are the documents people read TO find out what is true. Named as a pattern.

**Correction: REQUIRED, not performed here.** The Spine is frozen (S8 §1 — the ladder order is in
the freeze perimeter, `spine-s8-governance-freeze.md:17`) and S8 §5 forbids retro-editing, so the
fix is a director ADR under S8 §2's priced override — *"it costs a written ADR stating what
breaks, which tests and docs change, and the schedule price"* (`spine-s8-governance-freeze.md:33`)
— the same procedure ADR-014 used to amend S8 itself
(`docs/adr/adr-014-spec-format-foundations-audit.md`; S8 §4.1 header and §5 correction note,
`spine-s8-governance-freeze.md:51,204`). **Owner: director.**

---

## 2. FOUR KNOWN ITEMS, WITH THE MILESTONE THEY BITE IN

### 2a. D-037's DATA MODEL IS THE HIGHEST-CONSEQUENCE M4 ITEM (M4, day one)

Claim, control and recognition ship as THREE SEPARATE QUANTITIES from day one, supporting
overlap, claim-without-control, and asymmetric recognition (D-037 A3,
`d037-emergent-polities.md:22-35` — "LOAD-BEARING … retrofitting it later is prohibitively
expensive"). D-037 D1: *"Even if M4 uses only a subset, the data model must support overlap,
claim-without-control, and asymmetric recognition. This is the single instruction whose omission
would make Part C unbuildable later"* (`d037:128-130`). **NAMED CHECK in T4.1**, not merely spec
prose — the audit's written table (S8 §4.1 item 1 acceptance, `spine-s8-governance-freeze.md:124-129`)
carries a row verifying the three quantities are structurally separate in the shipped schema.

### 2b. B-2 STORE BOUNDING IS M4's FIRST REAL WORK

- Reserves measured at **~1,240 years** of consumption post-T3.5b (B-2 addendum,
  `docs/m4-blocking-material.md:147-157`; also `docs/t3.6-spec.md:122`), after T3.5b's derived
  mix slowed accumulation −59%. Earlier state: ~2,900–3,080 years (B-2 director ruling,
  `m4-blocking-material.md:39-49`; re-measured "~3,080 years, zero starvation in 20 runs",
  `docs/t3.4c-review-record.md:50`).
- **Zero starvation in 20 full-length runs** (`t3.4c-review-record.md:50`); measured thresholds:
  ~180 years of store gives first starvation, ~65 years mature-world chronicle famine
  (`m4-blocking-material.md:47-49`).
- T3.6 measured the interaction **unbounded in the other direction too**: under sustained
  maximum drive the mechanism drains a granary to zero — no store bounding on either side
  (`docs/t3.6-review-record.md:53-54`; lens table `:14`).
- Conflict and foreign trade both assume scarcity can bite (*"M4 introduces conflict and foreign
  trade, both of which assume scarcity can bite. It cannot ship on a world where hunger is
  unreachable"*, `docs/handoff-status.md:217` — note this sentence lives in the handoff record,
  not in m4-blocking-material.md itself; see finding F2. B-2's own words: "M4 cannot ship without
  answering how stores are bounded", `m4-blocking-material.md:45-46`).
- The mechanism is already staged by ruling — base layer (spoilage + granary capacity), derived
  not chosen, then enrichment (B-2a/B-2b, `m4-blocking-material.md:57-104`).
- **The Q-B hypothesis (director, via the GOV-2 directive — NO TREE RECORD, see finding F1):**
  bounding stores may also unstick T3.6b escalation 2's common-band-edge pinning, in which case
  the two are one packet. Recorded as the director's hypothesis to TEST, not assume: the tree's
  own record currently separates them — escalation 2 is filed as "a PRICE-SOLVER question
  (D-033/T3.4 …) — different owner, different fix" (`docs/t3.6b-review-record.md:216-222`;
  `docs/queue.md:340-344`).

### 2c. THE PRICE SOLVER SCALES AS O(S²·G²) PER TURN (a known trajectory M4 accelerates)

- The T3.4 residue: *"PriceSystem is O(S^2 * G^2) per turn … 56 rows today, but ~7.8M row-visits
  per turn at 200 settlements"* (`docs/queue.md:140-144`). Re-measured at T3.6: "at today's scale
  (S = 12, G = 14): measured, negligible; no optimization" (`docs/t3.6-review-record.md:166`).
- The Scale Charter as amended by D-009 runs settlements *"~50 (ancient) → 300–800 (late)"*
  (`d009-d010-map-population-addendum.md:19`); the performance budget is ≤5 s/turn early,
  ≤20 s/turn late (`civ-sim-architecture-v3-outline.md:47`). M4 adds inter-polity trade on top of
  the existing per-pair × per-good loop (D-034, `m3-spec.md:12`; "Trade & logistics — M4",
  `civ-sim-architecture-v3-outline.md:80`).
- **The count at which it needs attention, from the cited figures (arithmetic, not a new
  measurement):** at G = 14, row-visits ≈ S²G² give ~0.5M at S = 50 (comfortably inside budget at
  today's per-visit cost), **~7.8M at S = 200 — the queue's own flagged point — and ~17.6M at the
  charter's late-game floor of S = 300**, before goods staging grows G toward ~30/~60
  (`civ-sim-architecture-v3-outline.md:46`), which enters SQUARED. Attention is needed in the low
  hundreds of settlements — i.e. inside M4's own colonization trajectory, since colonization is
  the mechanism that starts moving S upward from 12. Not an M4 defect; a trajectory M4
  accelerates.

### 2d. THE LATTICE STRIDE BLOCKS THREE CONSUMERS, NOT ONE — RULE IT ONCE
*(amended under the GOV-2 extension: this item's first commit named two consumers; a third,
stated in the SAME queue entry, was missed and is added here — see §4b)*

Stride 4 means one traversal node is 16 × 16 km — "the RESOLUTION FLOOR on every spatial quantity
derived from it" (`docs/queue.md:52-64`). Three independent consumers are blocked, in three
different milestones:

1. **Rivers are sub-node features invisible to the traversal lattice** (M4 transport). The T3.6b
   water counterfactual's stage 1 finding: the stride-4 water mask cannot see rivers, so even
   FREE water never crosses the ore deadband at lattice resolution — "a RESOLUTION limit, not an
   economics answer" (`docs/t3.6b-review-record.md:235-239`); the measured hypothesis had to
   move to the pixel river mask — "since stride-4 blocks cannot represent rivers"
   (`t3.6b-review-record.md:259-260`).
2. **Village-scale catchments are unrepresentable** (unowned — no milestone has claimed them).
   "the classic 5 km site catchment is 0.3 of one node"; anything below ~32 km is under two
   nodes and the isochrone collapses (`queue.md:52-64`).
3. **Settlement density is bounded by the stride, not only by the siting rule** (M4
   colonization, per §4a). The same queue entry: *"The same floor bounds how finely settlements
   can be spaced before their catchments alias into each other"* (`queue.md:58-60`). This is
   §4a's Charter-vs-spacing constraint seen from the engineering side: even if `minSpacingKm`
   moved, the stride would still bound how dense the Charter's 300–800 settlements can pack
   before their catchments alias.

Moving to stride 2 "quadruples the node count and the Dijkstra work" plus a golden re-pin, not a
tuning change (`queue.md:62-64`). ONE architecture decision with THREE consumers — the M4 spec
should obtain a single ruling covering all three (including whether the answer is a finer stride
or a pixel-resolution water overlay, which the T3.6b measurement already reached for), not three
rulings across three milestones.

---

## 3. ONE TIMING NOTE INHERITED FROM T3.6

T3.6 decision (b) ruled trade **instantaneous within the turn**, justified at dt = 10 — "one
Neolithic turn is TEN YEARS, and no pre-modern land journey between adjacent settlements takes a
decade" (`docs/t3.6-spec.md:26-31`). The era table shrinks dt to **0.5 by the Modern band**
(`Sim.Data/content/era-pacing.json`), where the T3.6 spec's own FORWARD NOTE (director decision 1,
2026-07-28) states the breakdown: L_crit ≈ 182.5 × v × dt km; a 4,096 km corner-to-corner haul at
pre-modern land speeds (~30 km/day; ≈136 days ≈ 0.37 yr) is then "roughly ONE FULL TURN"
(`t3.6-spec.md:33-50`; speed reference class 25 km/day loaded, 30+ unloaded,
`docs/adr/adr-013-lattice-denomination-and-agronomic-recalibration.md:129-130`).

When that line is crossed, **in-transit cargo is NEW STATE** — "a serialized stock, a schema
bump, and a new conservation surface (goods on the road are owned by no settlement and must still
sum exactly)" (`t3.6-spec.md:46-47`). M4's armies and supply march on the same graph
(`d009-d010-map-population-addendum.md:51` — "M4: armies march and supply on the network graph
(same object as trade)") and face the identical question at military timescales. The T3.6 spec's
forward note already says the durable thing: *"whichever packet first needs in-transit state
builds it once, for both"* (`t3.6-spec.md:48-50`). Cross-referenced here so the M4 spec rules it
once.

---

## 4. THREE SCALING AND SCOPE CONFLICTS FOUND IN THE PRE-SPEC SCAN

Found by reading the ratified documents against each other (GOV-2 extension; every figure
re-verified against the tree below). Each is a conflict between things that are individually
true. **Recorded, not resolved.**

### 4a. THE SCALE CHARTER'S SETTLEMENT COUNTS ARE UNREACHABLE UNDER A DECISION JUST RE-RATIFIED

The three figures, verified:

- **The Charter line:** the revised Scale Charter states settlements *"~50 (ancient) → 300–800
  (late)"* (`docs/d009-d010-map-population-addendum.md:19`).
- **The measured capacity:** greedy saturation of the canonical continent at the shipped
  `minSpacingKm = 480` (`Sim.Data/content/worldgen.json:48`, set at T3.2b) is **min 33, median
  45, max 74** across seeds — *"Saturation is real at ~33, not 9"*
  (`docs/t3.4b-review-record.md:220,237`; carried as the corrected B-1,
  `docs/m4-blocking-material.md:9-24`, superseding B-1's original false claim of nine).
- **The re-ratification:** ADR-017 (2026-07-29) re-examined D-025's siting clause — *"iterative
  top-score siting with a minimum travel-time spacing constraint"* — against the T3.6b Item 0
  measurement and ruled **"SITING: STANDS UNAMENDED"** on measured grounds
  (`docs/adr/adr-017-d025-founding-variation.md`; evidence `docs/t3.6b-review-record.md`).

The arithmetic: the ANCIENT target (~50) already exceeds measured capacity ~33 by ~1.5×, and the
LATE target exceeds it by **~9–24×** (300/33 ≈ 9.1; 800/33 ≈ 24.2 — the directive's "10–24×" is
corrected to the measured arithmetic, finding F10). Only three things close the gap: colonization
founding settlements that do not respect `minSpacingKm = 480`; a change to the spacing constraint
itself; or a larger map (D-015, `d009-d010-map-population-addendum.md:58`).

**THE CONFLICT:** colonization is M4 work (CR-003 §5.2(a) — *"A large system — founding rules,
site selection, clearing cost, what constrains sprawl"*, `docs/adr/cr-003.md:261`), and the
siting-and-spacing clause it would have to break or bypass was re-examined and left UNAMENDED
days ago, on measured grounds. Nothing in any document states that M4's colonization must also
break or bypass that constraint in order to reach the Charter's own numbers. Stated, not
resolved. **NAMED CHECK for T4.1:** this is a constant-versus-consumer mismatch of exactly the
kind S8 §4.1 item 1(b) exists to catch — *"the identical question at both ends of the pipe: what
the producer writes, what the consumer multiplies"* (`spine-s8-governance-freeze.md:99-101`) —
reaching M4 through colonization; the audit row is `minSpacingKm` vs the Charter's settlement
counts, with the three closure routes as the recorded options for the director's ruling.

### 4b. THE STRIDE'S THIRD CONSUMER — LANDED AS THE §2d AMENDMENT

The GOV-2 extension identified a third consumer of the stride-4 decision, stated in the same
queue entry as the second and missed in this file's first commit: settlement density
(`docs/queue.md:58-60`). §2d is amended in place to name all three consumers with their
milestones — rivers (M4 transport), village catchments (unowned), settlement density (M4
colonization, §4a). The cost line is unchanged. The case for ruling the stride ONCE rather than
three times, across three milestones, is now substantially stronger.

### 4c. D-037 SHIPS CLAIM SOURCES THAT NOTHING CAN EXERCISE FOR SEVERAL MILESTONES

D-037 C1 enumerates six claim sources (`docs/d037-emergent-polities.md:70-79`). Read against the
milestone ladder, verified source by source (D-037's own milestone labels use the pre-D-011
numbering in places — §1c/F8; both numberings given):

| C1 source | live at M4? | goes live |
| --- | --- | --- |
| prior possession (`d037:72`) | **live mechanism at M4, empty at turn zero** — it needs a former controller, so it populates only after control first changes hands (conquest, E3, is M4: `d037:176-181`) | M4, first exercised after the first control change |
| co-ethnic/co-cultural population — irredentism (`d037:73-74`) | **inert** — C1's own text: "becomes live when cultural plurality does, M8/M9"; cultural plurality (Religion & culture) sits at M9 under D-011 §6 (§1c) | M9 (D-011 numbering) |
| dynastic or legal inheritance (`d037:75`) | **inert** — "(M7 institutions)"; institutions are M8 under D-011 §6 (§1c) | M8 (D-011 numbering) |
| treaty cession (`d037:76`) | **inert** — "(M7 diplomacy)"; diplomacy is M8 under D-011 §6 (§1c) | M8 (D-011 numbering) |
| conquest (`d037:77`) | **live** — E3: "Conquest converts CONTROL immediately but converts CLAIM only slowly" (`d037:176-181`) | M4 |
| settlement founding (`d037:78`) | **live** — B1 colonization from below is M4 (`d037:41-46`) | M4 |

**THIS IS NOT A REASON TO CHANGE D-037.** D1 is explicit that the data model supports all six
from day one and that retrofitting is prohibitively expensive (`d037:128-130`; §2a). The conflict
is with a DIFFERENT standard: this project has repeatedly shipped unreachable code paths that
artifacts claimed were exercised, and the reviews have caught them at cost. The precedents,
cited:

- **The `sumSq == 0` fallback (T3.4c review — NOT T3.6b as the directive stated; finding F9):**
  *"the `sumSq == 0` fallback branch is unreachable dead code and three artifacts claim it is
  exercised (lens 1 and lens 3 independently; NaN-poisoning proof — bit-identical worlds)"*
  (`docs/t3.4c-review-record.md:116-117`); the fallback was deleted, the three false claims
  corrected where they stood, and the *"undischargeable manifest item struck"*
  (`t3.4c-review-record.md:197`).
- **T3.5's exact-saturation branch** was *"measured dead in shipped"* config
  (`docs/t3.5-review-record.md:73`) and had to be revived deliberately at T3.5b
  (`docs/t3.5b-spec.md:154,200` — "the exact-saturation branch demonstrably live again").

**The requirement for M4, recorded so the spec author reads it before writing the packet
(director, via the GOV-2 extension):** every claim source that is inert at M4 ships explicitly
marked inert, with a test proving it is INERT RATHER THAN BROKEN, and with the milestone where it
goes live named in the code. An inert-and-tested path is honest; an inert-and-unmarked path is
the finding this project keeps paying for.

---

## 5. ONE MEASUREMENT OWED, NOT A FILING — THE KERNEL CLONE-SIZE CLAIM

m0-kernel-spec §3.2 states: *"At turn start the kernel clones `Prev → Next` (full copy; at M0–M9
scale this is a few MB — simplicity beats cleverness, revisit only if profiling gates fail)"*
(`docs/m0-kernel-spec.md:66`). That claim covers THROUGH M9 and has never been measured beyond M3
(trivially: no later milestone has run). This is §7.12's shape — a property asserted about the
artifact, covering a range it was never tested across, in a document every agent reads at session
start — and it is a FOURTH instance of the §1c pattern (CLAUDE.md's merge-loop line, ADR-015
§7.7's refuted mechanism, the stale Spine inventory).

**What is already determinable from code (read, not run — recorded here):**

- **The Buckets table is DENSE, not sparse.** The founding site instantiates the full
  cross-product: *"the FULL culture × religion × class × cohort cross product is instantiated in
  registry order"* (`Sim.Core/Worldgen/WorldFounding.cs:50-52`), with four nested loops adding a
  row for EVERY (culture, religion, class, cohort) combination per settlement and endowing only
  class 0 (`WorldFounding.cs:76-96`); other classes found at zero rows that nonetheless EXIST.
  No other site adds bucket rows — later systems transfer among the founded rows.
- **Today's row count, statically:** 12 settlements (`Sim.Data/content/worldgen.json:46`) × 1
  culture × 1 religion × 2 registered classes (`Sim.Data/content/sim.json` registries; the
  Merchants class, D-036/T3.7, has not landed on main) × 16 cohorts
  (`Sim.Core/State/Ids.cs:140`, `Cohorts.Count = 16`) = **384 rows** — coinciding with the M2
  arithmetic (12 × 1 × 1 × 2 × 16), since no third class has landed. A `BucketRow` carries four
  id fields, a cohort index, one `Conserved` long and seven doubles
  (`Sim.Core/State/WorldState.cs:105-141`) — order ~84 bytes unpadded, so today's Buckets clone
  is tens of KB. Arithmetic from code, not a measurement.
- **The projection arithmetic that makes the measurement urgent:** DENSE instantiation at the
  Charter's late game (settlements 300–800, `d009-d010-map-population-addendum.md:19`) with
  D-018's 11+1 class slots (`docs/d018-classes-and-needs.md:10`) and 16 cohorts gives, at even a
  SINGLE culture and religion, 800 × 1 × 1 × 12 × 16 = **153,600 rows — already at the Spine's
  own ratified hard cap of "~150k buckets world-wide"** (`civ-sim-architecture-v3-outline.md:44`)
  — and every plural culture/religion (arriving M8/M9-band) multiplies it: at 4 × 4, ~2.46M rows,
  order ~200 MB at ~84 B/row — no longer "a few MB". The cap's own *"automatic merge-below-
  threshold policy"* (`civ-sim-architecture-v3-outline.md:44`) presupposes sparse-or-merged
  storage; the shipped dense founding and the ratified cap are on a collision course at scale.
  Projection arithmetic, flagged as such — not a measurement.

**THE MEASUREMENT OWED, with its method (recorded, not run — docs-only packet):** report today's
bucket row count and clone bytes from `sim bench`; state sparse-or-dense (answered above from
code: DENSE — the bench confirms); project both against the Charter's late-game settlement counts
with plural cultures and religions and the ~150k bucket cap; then either CONFIRM the §3.2 claim
or NARROW it to the milestones it actually holds for. If the kernel spec's wording needs
amendment, the mechanism is an ADR under the M0 freeze (`m0-kernel-spec.md` is in the frozen
baseline, `spine-s8-governance-freeze.md:202`; kernel contract in the freeze perimeter, `:15`).

**Interaction with ADR-008, for whoever runs it:** ~50 MB of terrain is currently EXCLUDED from
the per-turn clone — terrain layers are *"immutable after worldgen"*
(`docs/adr/adr-008-static-terrain.md:9,19`). ADR-008 names the upgrade path: *"late-era terrain
mutation … would move the mutated layers into cloned, canonically-serialized state — a
director-approved ADR at that milestone, reversing this exclusion only for the layers that gain
writers"* (`adr-008:37-41`). The Spine schedules Environment & climate at M9
(`civ-sim-architecture-v3-outline.md:89`), displaced and AMBIGUOUS under the D-011 resequence
per §1c's table (the extension's "M10" is not tree-supported — finding F11). When those layers
gain writers, part of that 50 MB re-enters the clone, on top of whatever the buckets are doing by
then. D-009's mitigation is already on record: *"Raster-wide updates (climate, vegetation) run
chunked or every-N-turns, never full-raster every turn"* (`d009-d010-map-population-addendum.md:19`).

**Owner: the next packet that runs a bench. No milestone assigned.**

---

## 6. WHAT M4 IS NOW HOLDING, IN ONE LIST

**Director ruling, recorded at the director's instruction: M4 STAYS WHOLE rather than splitting.**
This section exists so the scale is visible to the spec author, not to reopen the ruling. M4
currently carries:

| item | source |
| --- | --- |
| trade & logistics + foreign trade | `civ-sim-architecture-v3-outline.md:80`; `m3-spec.md:21` ("any second polity, conflict, or foreign trade (M4)"); `d011-battle-layer-addendum.md:52` |
| strategic war, AutoResolver only | `d011-battle-layer-addendum.md:52` ("M4 — trade + strategic war, AutoResolver only"); `civ-sim-architecture-v3-outline.md:81` |
| colonization / land clearance | director ruling CR-003 §5.2(a): "A large system — founding rules, site selection, clearing cost, what constrains sprawl" (`docs/adr/cr-003.md:261`; queue entry `docs/queue.md:27-36`), bound by D-037 Part B1 (`d037-emergent-polities.md:41-46`) |
| non-state peoples from turn zero | D-037 B3 (`d037-emergent-polities.md:54-60`) |
| B-2 store bounding (base layer) | `docs/m4-blocking-material.md:39-104` |
| D-037's three-quantity claim model | `d037-emergent-polities.md:22-35,128-130` (§2a) |
| the transport packet's three levers (water routes — Q-A; draught animals — Q-C; route improvement — Q-E) | ruling via the GOV-2 directive, first recorded here; the water-route lever is the one with tree evidence (`docs/t3.6b-review-record.md:224-265` — "The next packet is a MECHANISM ('add water routes' …)"); see finding F1 |
| all of T3.10's migrated work | ruling via the GOV-2 directive, first recorded here (see finding F1); the content: `m3-spec.md:60` (calibration extension, comparative-advantage emergence), the BINDING MalthusLite power restoration (`queue.md:116-125`), the M5-tooth ceiling item (`queue.md:314-326`), candidate ADR-015 §7.15 (`queue.md:248-256`) |
| notables as generals | §1b ruling |

**The one cost it carries, stated as a scheduling expectation, not a risk to be mitigated away:**
a milestone this wide means an exit gate covering many systems in a single play session. The
director playing exit builds is how real defects get found: the M2 exit session exposed two
defects and the exit was **HELD** on them (starvation magnetism + the resurrection cycle; ghost
grievance — `docs/milestones.md:45-68`), and M1's director-played visual gates likewise forced
two rework rounds (T1.7, T1.8 — `milestones.md:11-13`; see finding F6 on the attribution). An M4
gate spanning trade, war, colonization, claims, store bounding and generals should EXPECT the
same, in proportion to its width.

---

## 7. FINDINGS — PROMPT-VS-TREE DISAGREEMENTS (§7.12) AND FIRST-RECORD RULINGS

Filed per the packet's own rule: where the directive and the tree disagree, the tree wins and the
disagreement is reported.

- **F1 — Four rulings and all Q-labels have NO TREE RECORD; this file is their first record.**
  Measured: `Q-A`/`Q-B`/`Q-C`/`Q-E`, "draught animals" and "route improvement" appear nowhere in
  the tree; no tree document records the transport packet moving into M4, T3.10 moving to M4 in
  its entirety, the money ruling (1a), the notables ruling (1b), or the M4-stays-whole ruling
  (§4). The tree's last word on T3.10 still schedules it inside M3 (`m3-spec.md:60`;
  `queue.md:116-125` "Do not close T3.10 with the ≥1 bar standing"). Of the transport packet's
  three levers, only water routes have a tree measurement behind them
  (`t3.6b-review-record.md:224-265`). *Extended at the GOV-2 extension:* the §4c inert-marking
  requirement for M4's claim sources is likewise director-originated with no earlier tree record;
  first recorded here.
- **F2 — Misattributed sentence.** "M4's own blocking material says conflict and foreign trade
  both assume scarcity can bite" — that sentence lives in `docs/handoff-status.md:217`, not in
  `m4-blocking-material.md`, whose own words are "M4 cannot ship without answering how stores are
  bounded" (`m4-blocking-material.md:45-46`).
- **F3 — The director's 1c row mapping omits two rows that also shift.** "Politics deep
  (institutions, regime change)" (inventory M7 → D-011 M8) and "Military full (ops, siege,
  naval)" (inventory M9+ → displaced/ambiguous; D-011 §5 keeps naval auto-resolve until
  post-slice). Both verified in §1c's table.
- **F4 — "M10 is the slice gate under BOTH schemes" is false against the tree.** Under the
  pre-D-011 scheme the Ancient Vertical Slice sat at **M9** (`civ-sim-architecture-v3-outline.md:113`)
  and "M10+" meant era expansions; D-011 §6 moved the slice to M10 (`d011:58`). The two schemes'
  "M10+" rows therefore do not read the same, and the espionage/notables rows are ambiguous
  rather than possibly-intentional.
- **F5 — The tree already contains the reconciliation rule for 1c.** S8 §5: "the addendum governs
  — append-only audit trail, no retro-editing" (`spine-s8-governance-freeze.md:202`), explicitly
  naming "milestone renumbering". The table is governed-stale by design; the REQUIRED correction
  is an ADR under the S8 §2 priced override precisely because §5 forbids a plain retro-edit.
  Consistent with the directed mechanism; recorded because the file must show the tree's rule.
- **F6 — "Both the M1 and M2 exits found real defects that way": half-supported as worded.** M2:
  fully supported — the exit session exposed two defects and the exit was HELD
  (`milestones.md:45-68`). M1: the tree records no defect found at the exit session itself (the
  T1.10 gate playthrough was ruled the exit session, `milestones.md:18-21`); M1's rework rounds
  were at the mid-milestone visual gates T1.7 and T1.8 (`milestones.md:11-13`). The tree-true
  form: director-played gates found real defects in both milestones; the M2 EXIT specifically was
  held on two.
- **F7 — The 1a rationale's premise "the ancient vertical slice at M10 is squarely pre-coinage"
  disagrees with the slice's ratified description.** The Spine describes the slice as a "Full
  loop through classical antiquity" (`civ-sim-architecture-v3-outline.md:113`), which spans the
  ruling's own ~630 BCE coinage date; the era table's Bronze/Iron band runs to 500 CE
  (`era-pacing.json`). The ruling stands — rulings are the director's — but the premise as stated
  is not tree-true, and the disagreement sharpens the open scheduling item in §1a: money's
  milestone may need to land before or within the slice, not after it.
- **F8 — Supporting evidence for 1c found during verification:** D-037 internally mixes the two
  numbering schemes (`d037-emergent-polities.md:176` vs `:4,119,170`), demonstrating the stale
  table propagating into a new ratified document (§1c).

*Findings F9–F12 filed at the GOV-2 extension (2026-07-30):*

- **F9 — The `sumSq == 0` precedent is a T3.4c finding, not T3.6b.** The extension attributed it
  to "T3.6b"; the tree records it in the T3.4c review — mutant/finding M4, unreachable dead code,
  NaN-poisoning proof, three artifacts claiming exercise, undischargeable manifest item struck
  (`docs/t3.4c-review-record.md:116-117,197`; the branch itself originates in the T3.4b/c weather
  substrate, `docs/t3.4c-spec.md:19-37`). Nothing in the T3.6b record mentions `sumSq`. Cited
  correctly in §4c.
- **F10 — "roughly 10–24×" is ~9–24× by the cited figures.** 300/33 ≈ 9.1 and 800/33 ≈ 24.2;
  §4a records the measured arithmetic. Minor, filed for the transcription discipline (the
  standing summary-prose rule, `docs/queue.md:283-290`).
- **F11 — Environment & climate is not "M10 under the D-011 resequence".** The extension's
  Section 6 parenthetical asserts M10 "per 1c"; §1c's verified table records the row as
  DISPLACED AND AMBIGUOUS — D-011 §6 does not name Environment & climate at all, and M9 is now
  the society layer (`docs/d011-battle-layer-addendum.md:49-59`;
  `civ-sim-architecture-v3-outline.md:89`). §5 carries the ambiguity, not the M10 label.
- **F12 — ADR-017's status line is stale against the merge record (tree-internal, noted while
  verifying §4a).** The ADR file still reads "Status: proposed (director certification pending)"
  (`docs/adr/adr-017-d025-founding-variation.md:3`) while the merge commit on `main` records the
  packet as director-certified ("Merge t3.6b-founding-variation (director-certified)", commit
  `4938897`). §4a's premise — the siting clause stands, re-ratified on measured grounds — holds
  either way; the unflipped status line is itself a small instance of the §1c pattern.

---

## 8. PROVENANCE (per-item, claim → source)

| claim | source |
| --- | --- |
| D-030 money deferral + scope fence | `docs/m3-spec.md:8,21` |
| taxation scheduled M5 | `docs/civ-sim-architecture-v3-outline.md:82,109` |
| money-stock lines (17 enumerated) | table in §1a, each row cited |
| no money stock in code | `Sim.Core/State/Ids.cs:20`; `docs/m3-spec.md:25` |
| notables needed M4/M6/M8 | `docs/d011-battle-layer-addendum.md:26,27,30`; `docs/d021-stability-doctrine.md:25,44`; `docs/d009-d010-map-population-addendum.md:29,43` |
| notables field list | `docs/adr/adr-001-unmanaged-table-rows.md:27`; `Sim.Core/State/Ids.cs:80`; `Sim.Core/Chronicle/NameRegistry.cs`; `docs/m2-spec.md:40`; D-011/D-010/D-021 lines above |
| inventory staleness table | `docs/civ-sim-architecture-v3-outline.md:71-96,110-114`; `docs/d011-battle-layer-addendum.md:45,49-59` |
| §7.12 precedent instances | `docs/adr/adr-015-verification-hygiene.md:645` (merge loop), `:348-375` (§7.7 correction) |
| S8 freeze/override/append-only | `docs/spine-s8-governance-freeze.md:17,33,202`; §4.1 `:51-134` |
| D-037 A3/D1 | `docs/d037-emergent-polities.md:22-35,128-130` |
| B-2 figures | `docs/m4-blocking-material.md:39-104,147-157`; `docs/t3.4c-review-record.md:50`; `docs/t3.6-review-record.md:14,53-54`; `docs/handoff-status.md:217` |
| price-solver figures | `docs/queue.md:140-144`; `docs/t3.6-review-record.md:166`; `docs/d009-d010-map-population-addendum.md:19`; `docs/civ-sim-architecture-v3-outline.md:46,47` |
| lattice stride | `docs/queue.md:52-64`; `docs/t3.6b-review-record.md:235-239,259-260` |
| T3.6 timing note | `docs/t3.6-spec.md:26-50`; `Sim.Data/content/era-pacing.json`; `docs/adr/adr-013-lattice-denomination-and-agronomic-recalibration.md:129-130`; `docs/d009-d010-map-population-addendum.md:51` |
| M4 scope list | table in §6, each row cited |
| exit-gate history | `docs/milestones.md:11-21,45-68` |
| Charter-vs-spacing conflict (§4a) | `docs/d009-d010-map-population-addendum.md:19,58`; `Sim.Data/content/worldgen.json:48`; `docs/t3.4b-review-record.md:220,237`; `docs/m4-blocking-material.md:9-24`; `docs/adr/adr-017-d025-founding-variation.md`; `docs/adr/cr-003.md:261`; `docs/spine-s8-governance-freeze.md:99-101` |
| stride third consumer (§2d/§4b) | `docs/queue.md:58-60` |
| D-037 claim-source liveness (§4c) | `docs/d037-emergent-polities.md:41-46,70-79,128-130,176-181`; precedents `docs/t3.4c-review-record.md:116-117,197`, `docs/t3.4c-spec.md:19-37`, `docs/t3.5-review-record.md:73`, `docs/t3.5b-spec.md:154,200` |
| kernel clone-size claim (§5) | `docs/m0-kernel-spec.md:66`; bucket key `docs/d009-d010-map-population-addendum.md:29`; DENSE founding `Sim.Core/Worldgen/WorldFounding.cs:50-52,76-96`; counts `Sim.Data/content/worldgen.json:46`, `Sim.Data/content/sim.json` (registries), `Sim.Core/State/Ids.cs:140`; row shape `Sim.Core/State/WorldState.cs:105-141`; bucket cap `civ-sim-architecture-v3-outline.md:44`; classes ceiling `docs/d018-classes-and-needs.md:10`; ADR-008 `docs/adr/adr-008-static-terrain.md:9,19,37-41`; mitigation `d009-d010-map-population-addendum.md:19`; freeze mechanism `docs/spine-s8-governance-freeze.md:15,202` |
