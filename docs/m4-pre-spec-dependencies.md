# M4 — PRE-SPEC DEPENDENCY FILING (GOV-2)

**Docs-only filing, 2026-07-30, directed packet GOV-2.** The M4 spec will be the first written
under S8 §4.1 (`docs/spine-s8-governance-freeze.md:65` — "Effective from the M4 spec onward"),
whose four mandatory items include a coupling map (§4.1 item 4, `spine-s8-governance-freeze.md:183`).
This file is the INPUT to that map: it collects, with tree citations, everything the M4 spec author
would otherwise rediscover. **It observes; it designs nothing and amends nothing.** The rulings
recorded in §1 and §4 arrive via the GOV-2 directive and are FIRST RECORDED HERE (see §5, finding
F1); the corrections they imply land later, by ADR where a frozen document is touched.

Citation convention: `file:line` as measured in the tree at `gov-2-m4-dependencies` (cut from
`main` @ d51327e). Where a figure comes from a review record, the record is cited.

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
See also finding F7 (§5): the vertical slice is described in the Spine as a "Full loop through
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

### 2d. THE LATTICE STRIDE BLOCKS TWO CONSUMERS, NOT ONE — RULE IT ONCE

Stride 4 means one traversal node is 16 × 16 km — "the RESOLUTION FLOOR on every spatial quantity
derived from it" (`docs/queue.md:52-64`). Two independent consumers are blocked:

1. **Rivers are sub-node features invisible to the traversal lattice.** The T3.6b water
   counterfactual's stage 1 finding: the stride-4 water mask cannot see rivers, so even FREE
   water never crosses the ore deadband at lattice resolution — "a RESOLUTION limit, not an
   economics answer" (`docs/t3.6b-review-record.md:235-239`); the measured hypothesis had to
   move to the pixel river mask — "since stride-4 blocks cannot represent rivers"
   (`t3.6b-review-record.md:259-260`).
2. **Village-scale catchments are unrepresentable.** "the classic 5 km site catchment is 0.3 of
   one node"; anything below ~32 km is under two nodes and the isochrone collapses
   (`queue.md:52-64`).

Moving to stride 2 "quadruples the node count and the Dijkstra work" plus a golden re-pin, not a
tuning change (`queue.md:62-64`). ONE architecture decision with TWO consumers — the M4 spec
should obtain a single ruling covering both (including whether the answer is a finer stride or a
pixel-resolution water overlay, which the T3.6b measurement already reached for), not two rulings
a milestone apart.

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

## 4. WHAT M4 IS NOW HOLDING, IN ONE LIST

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

## 5. FINDINGS — PROMPT-VS-TREE DISAGREEMENTS (§7.12) AND FIRST-RECORD RULINGS

Filed per the packet's own rule: where the directive and the tree disagree, the tree wins and the
disagreement is reported.

- **F1 — Four rulings and all Q-labels have NO TREE RECORD; this file is their first record.**
  Measured: `Q-A`/`Q-B`/`Q-C`/`Q-E`, "draught animals" and "route improvement" appear nowhere in
  the tree; no tree document records the transport packet moving into M4, T3.10 moving to M4 in
  its entirety, the money ruling (1a), the notables ruling (1b), or the M4-stays-whole ruling
  (§4). The tree's last word on T3.10 still schedules it inside M3 (`m3-spec.md:60`;
  `queue.md:116-125` "Do not close T3.10 with the ≥1 bar standing"). Of the transport packet's
  three levers, only water routes have a tree measurement behind them
  (`t3.6b-review-record.md:224-265`).
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

---

## 6. PROVENANCE (per-item, claim → source)

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
| M4 scope list | table in §4, each row cited |
| exit-gate history | `docs/milestones.md:11-21,45-68` |
