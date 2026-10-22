# Civics Points economy — sources, generation, the government-change exchange and the cross-pool gate

**Status: PROPOSED design analysis for Director review. Not ratified, not implemented, not runtime content.** Nothing
here changes `Sim.*`, content, schemas, tests or goldens. Every coefficient is a PROPOSED placeholder; no yield is
final. Labels: **MEASURED** (read from code, content or the baseline runs) · **RESEARCHED** · **INFERENCE** ·
**PROPOSED** · **OPEN DECISION** · **IMPLEMENTATION DEFERRED** · **ESTIMATE (static model)** — a figure from
`tools/cp_gate.py`, never a gameplay or simulation result (no simulation of the proposed graph or of any CP economy
exists).

Tree: branch `claude/new-session-yavghm` (package baseline `edba2b5`). Written 2026-10-22.

**Input, not a ruling.** The Director's intent of 2026-10-22 (recorded in `docs/d061-civics-points-supersession.md`,
"Director intent note — 2026-10-22", NOT RATIFIED) asks for: separate accumulated pools for RP and CP feeding distinct
trees; CP partly from population plus other source categories, defined independently and never as a fraction or split
of RP; an RP↔CP exchange only in the turn the empire changes government, with its economics left open; supersession of
the old single-pool rulings only through the append-only process; staged ratification (architecture first). This
document develops that intent into options and recommendations. The temporary happiness penalty mentioned as an
example is **excluded**: it is not developed, tuned or prototyped here.

## 0. Governing records checked (exact references)

| Record | Status | What it fixes for this document |
|---|---|---|
| `docs/d061-civics-points-supersession.md` §1–§9 + dated note 2026-10-21 | PROPOSED; §9 ruling block **empty** | CP as a second currency for Civics only (§3); D-061.2 dimension (per-turn by ADR-043 recommended); D-061.3 inputs (population term with its own exponent + institutional term; "no RP term, no fraction of RP"); D-061.8 widened to civic atoms inside Technology gates; D-061.3 and D-061.8 ruled together |
| `docs/adr/cr-023-civics-points-vs-single-pool-rulings.md` | RAISED, awaiting ruling | option 1 = supersede for Civics only; option 2 (a split of RP) "does not meet C as worded" |
| `docs/m6-spec.md` §1 row 1.17, §2, §3 I-3/I-4/I-6, §3.2, §7 G1/G2, dated note 2026-10-21 (C.1) | spec in preparation, NOT ratified | no third currency, **no general RP or CP reserve**, no manual percentage allocation, no reusable per-turn exception (§2); gate on completion, never on a currency (I-3); one engine (I-4); C.1: `legal_code_roman` paces Technology under CP |
| `docs/m6-plan/m6-ruling-docket.md` A2, B (D-061.2–8, D-S), C.1, C.5 | docket | D-061.8 recommendation "keep both, T6.3 measures reachability"; D-S: institutions feed CP only, scholars must not feed RP (double count, `Institutions.cs:72-109`) |
| `docs/d044-research-progression-rulings.md` R2, R4, R5, R9, R20-D (Part B ★ list `:782-818`) | RATIFIED | ★ subtrees open together at the university/research institutional stage (`:145`, ★ `:796`); ★ universities reduce EffectiveCost, "no flat science, no second currency" (`:162-182`, ★ `:797`); **no general bank or reserve; per-node partial progress is the only retained quantity** (`:795`, `:816-818`) |
| `docs/d046-research-foundation-gate-rulings.md` G5 (`:202-204`), F§1 (`:367`) | RATIFIED | one pool (superseded for Civics only if D-061 is ratified); the research.json content, **including the stage expression**, is ratified content |
| `docs/design/director-architecture-and-decision-ledger.md` §26 items 2–5, 10, 11, 19 | RATIFIED | no per-branch currencies, no general RP reserve, no manual percentage allocation, no RP × dtYears, no reusable per-turn exception, no repricing to hit Age duration |
| `docs/adr/adr-030-per-turn-research-points.md` §1–§3, §6 | RATIFIED | RP = 0.08 × P^0.699 per strategic turn; exception scope is generation, allocation, progress only; "a second per-turn quantity needs its own ADR" |
| `docs/m6-plan/m6-decision-register.md` dated note 2026-10-20, D-D (`:235-237`) | RULED | M6 defines civic prerequisites and government eligibility; adoption and active political effects are M8 |
| `docs/civ-sim-architecture-v3-outline.md:85`; `docs/d042-empire-and-player-control-addendum.md:90-92` | RATIFIED (frozen) | "composable modules, not enum governments"; "avoid hard-coding governments as bundles of direct bonuses" |
| `docs/d018-classes-and-needs.md` §2 | RATIFIED (closed by delegation) | 11 classes + 1 reserved slot; classes emerge by computed predicate; MEASURED: 3 implemented (`sim.json` `registries.classes`: Peasants, Artisans, Merchants) |

## 1. Source catalogue

Status key: **EXISTING** = the quantity exists and is computed in the running build; **PARTLY REPRESENTED** = the
entity, person pool or variable exists but nothing realizes it as a CP input (or it is eligibility only);
**PROPOSED** = nothing in the build. Every row's CP use is PROPOSED.

| # | Source | Status (evidence) | Responsible system · milestone | Dependencies | Double-counting risk | Open tuning questions |
|---|---|---|---|---|---|---|
| S1 | **Population** of the polity's controlled settlements | **PARTLY REPRESENTED** — `ResearchQuery.Population` (each settlement once, lowest-id controller; `ResearchQuery.cs:361-375`) EXISTS as RP's input; its use for CP is PROPOSED | ResearchSystem parameterized by (tree, pool, target) (m6-spec I-4) · **M6 (T6.3)** | D-061.1, D-061.2, D-061.3 | the base term must exclude every person counted in a specialist term (S2–S8): `P_free = P − Σ S_k`; across pools the same people also feed RP (ADR-030) — by design, both pools are population-led | exponent β_P; reference population P_ref; coefficient; aggregate-then-power (per polity) vs per-settlement sum; reading Y vs T (§2.4) |
| S1b | Population **weighted by administrative reach** (variant of S1) | **PARTLY REPRESENTED** — `ControlRow.Strength = exp(−TravelCost/25)` (`Governance.cs:94-115`) EXISTS (M5) | GovernanceSystem publishes, ResearchSystem reads (law 6) · M6 | as S1 + R4 laws-lite review | none beyond S1 | a seatless (revolt-born) polity reaches nothing → CP 0 → liveness risk; needs a floor or is rejected |
| S2 | **Social-sciences universities** | **PROPOSED** — not represented: `universityTypes` are exactly the five Technology subtree types (research.json keys 1–5); only universities are realized (`InstitutionRow`, ADR-033 D6) | InstitutionsSystem (founding, maturity, staff) + ResearchSystem · M6 only if D-M generalizes institutions (T6.11), else **M9** (education) | **CONFLICT G-3 (§5)** with D-044 R5 ★; D-M | staff are real adults withdrawn from labour (`InstitutionStaffing`, `Institutions.cs:72-109`): count them once, in the staff term, and subtract from `P_free`; never also as Intelligentsia | a CP yield per staff-year (needs a ruling) **or** an R5-shaped civic cost factor (no new currency yield); staff share; κ |
| S3 | **Colleges / academies / scribal schools** | **PARTLY REPRESENTED** — `inst.scribal_school`, `inst.archive`, `inst.library`, `building.school` exist as eligibility only (unlock class J, unrealized) | InstitutionsSystem (D-M) + ConstructionSystem · M6 (T6.11/T6.18 generalization) or M9 | D-M; the university founding shape generalized | same staff rule as S2; a library's staff cannot also be "literary scholars" (S7) | which entity kinds staff civic learning; κ per kind |
| S4 | **Law courts / legal institutions** | **PARTLY REPRESENTED** — `inst.law_code`, `inst.legal_code_roman` (eligibility only); the D-P vertical slice law_code → law court → `ServiceOffset` is PROPOSED (m6-spec §7 G4) | InstitutionsSystem + GovernanceSystem · M6 (D-P slice) | D-P, D-M | court staff vs the Bureaucrats class (S6): one precedence rule | whether a court's CP is staff-based or a per-instance term |
| S5 | **Temples / organized priesthood / Clergy** | **PROPOSED** — Clergy is a D-018 class, not implemented (3 of 11 classes exist, MEASURED); the civic `organized_priesthood` is PROPOSED | ClassMobilitySystem (emergence) + ResearchSystem · **M9** | D-018 Clergy emergence predicate; Belonging/Faith need | Clergy headcount vs temple staff (count one); D-018 says Clergy *produce legitimacy* — a CP term must not also be the legitimacy channel (M8) | per-cleric κ; recede/latch behaviour |
| S6 | **Bureaucrats / appointed officials** | **PROPOSED** — D-018 class (M9); the civic `appointed_officials` is PROPOSED | ClassMobilitySystem · M9 | D-018; R4 authority economy | overlaps S1b (reach) and S4 (courts) | κ; whether reach weighting is then dropped |
| S7 | **Literary scholars / Intelligentsia** | **PROPOSED** — D-018 Intelligentsia emerges on "literacy share + education institutions"; no literacy variable exists (MEASURED: `ResearchQuery.cs:381` lists literacy as a FUTURE input); `inst.library`, `inst.historiography`, `inst.translation_movement` exist as eligibility only | ClassMobilitySystem / InstitutionsSystem · M9 | a literacy variable; D-018 | with S2/S3 staff | κ; which institutions employ them |
| S8 | **Artists** | **PROPOSED** — no class (D-018 roster closed at 11 + 1 reserved); `NotableRow` EXISTS as a person-conserving surface (M4 T4.8, `NotableLifecycle.cs`); the Great People pattern is adopted but deferred (D-047 r15/r16; m6-spec §8) | notable lifecycle owner · **M7/M8** | a notable trigger (none ratified); D-047 | a notable is a person extracted from a bucket — counted once, never also in `P_free` | per-notable κ; a flat per-notable yield risks law 2 unless tied to the person's occupation |
| S9 | **Embassies / resident envoys** | **PROPOSED** — diplomacy is M8 (D-D; m6-spec §2); the civic `resident_embassies` is PROPOSED | Diplomacy (M8) · **M8** | contact within known extent (fog, T6.8) | envoys are people: once | **recommended mechanism: not a rate** — a contact credit on civic nodes the contacted polity knows, through the `ResearchExposureRow` pathway (`WorldState.cs:931`), capped by the shared 0.4 × BaseCost acceleration ceiling (D-047 r10) |
| S10 | **Spies** | **PROPOSED** — espionage DEFERRED to M8 (m6-spec §8) | Diplomacy/espionage (M8) · **M8** | fog information fence (I-9), metamorphic leak tests | as S9 | as S9; never reads world truth; AI symmetric |
| S11 | **Printing, newspapers, mass schooling** | **PARTLY REPRESENTED** — `printing_press` (technology), `inst.newspaper`, `inst.mass_schooling`, `building.printing_house` (eligibility only) | Institutions / Society · M9 | literacy variable (S7) | a multiplier on S1 would be a modifier (law 2) unless it enters a named equation (e.g. the share of `P` that is literate) | whether it scales specialist productivity or the literate share |
| S12 | Realized civic institutions, Σ maturity (D-061.3's "Active-institution term") | **PARTLY REPRESENTED** — maturity EXISTS for universities (`InstitutionsSystem.cs:62-70`) | InstitutionsSystem · M6 | D-M | Σ maturity counts buildings, not people; combined with a staff term it double-counts | **recommended replacement:** express the institutional term through staff (persons, S2–S4), which already scales with maturity |
| — | Merchants class (EXISTING, T4.11) | considered, **not recommended** | — | — | trade already has its own mechanisms and Age facts | — |
| — | Settlement count / urbanization | considered, **not recommended** | — | — | rewards splitting settlements; not a person count | — |

## 2. Recommended CP-generation architecture (PROPOSED)

### 2.1 Principles

1. **Independent inputs.** CP is a function of the polity's own people and institutions. RP's output never enters it,
   and CP never enters RP (ADR-030 stands unchanged).
2. **Each person counts once within CP.** A person is either in the base population term or in exactly one specialist
   term; precedence when pools overlap: institution staff → class specialists → notables; the remainder is `P_free`.
3. **Mechanisms, not modifiers (law 2).** Every term is a coefficient inside the generation equation on a quantity a
   system computes (persons, staff); no free-floating "+x % CP" buff.
4. **Gate on completion, never on CP** (m6-spec I-3). CP is never the left operand of a predicate.
5. **No bank.** "Pool" means the turn's generation directed to the civics target plus per-node partial progress — the
   only retained quantity under D-044 R20-D (★ for partial progress) and ledger §26 item 4. A bankable balance would
   need a dated Director ruling (conflict G-1, §5).

### 2.2 Candidate equations

```
G_pop(p)   = c_P · P_ref · (P_free(p) / P_ref)^β_P                      CP per sim-year   (S1; per-polity aggregate)
G_inst(p)  = Σ_i∈civic institutions of p   κ_i · r_ref · staff_i          CP per sim-year   (S2–S4; staff_i = σ·A·M_i, the InstitutionStaffing shape)
G_class(p) = Σ_c∈{Clergy, Bureaucrats, Intelligentsia}  κ_c · r_ref · (N_c − staff drawn from c)   CP per sim-year (S5–S7; M9)
P_free(p)  = P(p) − Σ staff_i − Σ (N_c − staff drawn from c) − notables held
r_ref      = c_P                                                          the reference per-capita yield (CP per person-year at P_ref)

reading Y (law 3 as written):   CP_turn(p) = dtYears × [G_pop + G_inst + G_class]
reading T (needs ADR-043):      CP_turn(p) = G'_pop + G'_inst + G'_class   (same form, coefficients per strategic turn)
exposure (S9, S10; M8):         credit(n) = w_contact × BaseCost(n),  w_contact ≤ 0.4 − w_eureka   — an event credit, not a rate
```

**Recommended baseline (one line):** `CP_turn = dtYears × [c_P·P_ref·(P_free/P_ref)^β_P + Σ_k κ_k·c_P·S_k]` with
**P_ref = 5,000**, **c_P·P_ref = 3.1 CP per sim-year** (≈ the first A1 civic, `kin_council` 310 CP, in 10 turns at
dt = 10), **β_P = 0.6**, **κ_k = 50** (a specialist yields 50× the reference per-capita CP), `S_k` the disjoint staff
and specialist pools — **all PROPOSED placeholders**, to be anchored by stated reference classes before any
implementation (S8 §4.1), with D-061.2 deciding whether the bracket is read per sim-year (above) or per turn.

In M6 only S1 (and S2–S4 if D-M generalizes institutions) can exist; S5–S11 enter with their owning milestones. The
static model in §4 therefore uses the population term alone — a lower bound on CP with respect to the specialist
terms.

### 2.3 Trade-offs of the population term

| Choice | Option | Effect | Recommendation |
|---|---|---|---|
| exponent β_P | 0.5 / 0.6 / 0.7 / 0.8 | lower β = less advantage to large empires; β ≈ 0.699 with reading T makes CP numerically proportional to RP — a degenerate corner that erases the distinction the Director asked for | **0.6** (own exponent, below RP's), sensitivity 0.5–0.8 |
| aggregation | (Σ_s P_s)^β per polity / Σ_s P_s^β per settlement | per-settlement summation rewards splitting into many small settlements | **per polity** (mirrors RP; not exploitable by splitting) |
| reach weighting (S1b) | none / `Σ_s P_s·Strength_s` | ties civic capacity to administration; zero for a seatless polity | none in the baseline; offer S1b as an OPEN DECISION with a floor |
| specialist terms | linear in persons / sublinear | linear keeps "each person counts once" exact and auditable | **linear**, κ per kind |
| normalisation | P_ref | makes `c_P` read as "CP per sim-year of a reference polity" and keeps the anchor stated in people | P_ref = 5,000 (the founded-civilization reference of `U_basis`, research.json `tuning.costModel`) |

### 2.4 dt-correctness and the D-061.2 choice (MEASURED inputs, ESTIMATE outputs)

Law 3 requires per-sim-year rates integrated with `dtYears`; RP is the single scoped exception (ADR-030 §2), and a
second per-turn quantity needs its own ADR (ADR-030 §3; ledger §26 item 11 forbids a reusable mechanism). The
equations above are written per sim-year (reading Y). The era table shortens turns from dt = 10 to dt = 0.5 (MEASURED,
`population-trace-seed42.csv`), so at equal population reading Y yields **20× less CP per turn late than early**,
while RP per turn does not fall. ESTIMATE (static model, placeholder anchor, β = 0.6, `lateGamePerTurn` in
`data/balance-cp-gate.json`):

| seed 42 | turn 300 (dt 5) | turn 700 (dt 3) | turn 1000 (dt 2) | turn 1300 (dt 0.5) | turn 1600 (dt 0.5) |
|---|---|---|---|---|---|
| non-expanding driver, reading Y — CP/turn (turns for a 10,000-CP civic) | 55 (183) | 78 (128) | 77 (130) | 23 (428) | 25 (400) |
| non-expanding driver, reading T | 110 (91) | 261 (38) | 384 (26) | 468 (21) | 500 (20) |
| AI empire, reading Y | 42 (241) | 62 (161) | 195 (51) | 116 (86) | 148 (68) |
| AI empire, reading T | 83 (120) | 207 (48) | 974 (10) | 2,325 (4) | 2,951 (3) |

Reading: under Y, the A8–A9 civics (7,000–33,450 CP) take hundreds of turns for a non-expanding polity; under T they
pace like technology. This is the evidence for D-061.2 (OPEN DECISION). Costs must not be changed to compensate
(ledger §26 item 19); the remedy, if wanted, is the dimension ruling or the coefficients.

### 2.5 Why CP must not reuse RP's output

1. **It would be a split, not a currency.** The 2026-10-20 instruction refused combining the currencies; D-061.3 and
   CR-023 option 2 record that a fraction of RP does not meet the direction.
2. **Dimension leak.** RP is per turn by a scoped exception; `CP = r × RP` would inherit that exception without its own
   ADR (ADR-030 §3; ledger §26 item 11).
3. **Hidden coupling.** Every future RP decision (KD-10 pacing, D-S inputs, the military-population weight of ADR-030
   §6) would silently move civics pacing — the corridor-independence statement (S8 §4.1 item 3) could not be made.
4. **The exchange would be empty.** With `CP ≡ r·RP`, an RP↔CP exchange is a re-labelled split.
5. **No distinct strategy.** A polity could not invest in civic capacity (institutions, specialists) separately from
   scientific capacity.

How RP works today (MEASURED): `ResearchQuery.ResearchPoints = 0.08 × P^0.699` per strategic turn (`ResearchQuery.cs:378-385`;
research.json `tuning.rpPerTurn`, anchors 100 → 2, 1,000 → 10), P = `ResearchQuery.Population`; directed to one target
(R9); overflow lost; never × dtYears (ADR-030).

### 2.6 Conservation, ledger and state

- CP is not people, money or goods, so it is **not a Ledger stock under law 1** (as RP is not); it is a research-type
  `double` quantity held as per-node progress. Two audit identities replace the one-pool pin (D-061 S-12):
  `Σ invested RP + RP lost (+ RP exchanged out) = Σ RP_turn` and
  `Σ invested CP + CP lost = Σ CP_turn (+ ρ × RP exchanged in)`, exact (no epsilon), accumulation order fixed by
  polity id then node key.
- If the Director wants a **bankable** CP or RP balance (a true stock), that needs (a) a dated ruling superseding D-044
  R20-D and ledger §26 item 4 for that pool, and (b) a type decision: a `long` fixed-point stock (e.g. milli-points)
  with ledger-style flows would make exact equality natural; a `double` balance would not be a conserved stock. OPEN
  DECISION; not recommended.
- New state (m6-spec §3.2, PROPOSED): `CivicsProgressRow(Polity, Node, Progress)`, `CivicsTargetRow(Polity, Node)`, and
  — only when M8 lands the exchange — an `ExchangeRow(Polity, Turn, From, AmountOut, Ratio, AmountIn)`. Each appended
  section ships a POPULATED-table test (ExpectedLength, bit-exact round-trip, hash equality).
- Isolation (law 6): ResearchSystem reads `Population`, institution staff and class buckets from tables; no system
  reference.

## 3. RP↔CP exchange in the government-change turn (architecture only; IMPLEMENTATION DEFERRED to M8)

**Scope.** Exchange is allowed **only in the turn the empire changes government**; no general conversion. Direction,
ratio, limits, transaction order and eligibility are **OPEN DECISIONS**. No exchange implementation is proposed for
M6: M6 has no government change at all (D-D: adoption is M8). The happiness penalty is excluded from this design.

### 3.1 What "changes government" can mean under the frozen direction

Governments are composable module sets with a derived label (Spine `:85`; D-042 §5; arch-FGH §2.1–2.2, PROPOSED). The
trigger must therefore be a **change of the polity's module set that changes its derived label**, computed from state —
never a write to, or a read of, a government enum. Candidate eligibility (OPEN DECISION): (a) a voluntary M8 adoption
act applied this turn; (b) any change of the derived label, including involuntary ones (revolt, coup, succession
crisis); (c) (a) only, with a minimum interval between exchanges. Recommendation: (a), because an involuntary change
gives the player no decision to attach an exchange to, and (b) creates an incentive to provoke crises.

### 3.2 The structural limit

There is no RP or CP bank (D-044 R20-D; ledger §26 item 4; m6-spec §2). The only quantities that could be exchanged
are therefore: (i) **the change turn's own generation** (`RP_turn`, `CP_turn` of that turn), or (ii) per-node partial
progress already invested. **Recommended structural form: (i) only** — up to one turn's generation of one pool is
redirected to the other pool's target at ratio ρ. This caps every exchange at one turn of generation without any
reserve, and cannot be repeated more often than the government changes. Form (ii) would turn invested progress into a
de facto bank and is not recommended.

### 3.3 Open economics (OPEN DECISIONS — no values proposed)

| Question | Options |
|---|---|
| direction | RP→CP only; CP→RP only; either, chosen at the change |
| ratio ρ | 1:1; a loss (ρ < 1); different per direction |
| limit | a fraction of the change turn's generation; the whole turn; an absolute cap |
| eligibility | §3.1 (a)/(b)/(c); whether a revolt-born polity's founding counts; minimum interval |
| receiving side | into the active target only (overflow lost, as today) or carried (D-R/KD-11) |
| Eureka ceiling | whether exchanged points count against the 0.4 × BaseCost acceleration ceiling (recommend: no — they are points, not credit — but OPEN) |
| amount | fixed by rule, or chosen by the player up to the limit |

### 3.4 Order within the turn (PROPOSED)

1. Orders of turn t are applied on PREV by their owners; the M8 adoption system applies the adoption and publishes a
   per-polity "government changed at t" row.
2. The exchange specification is **a field of the adoption order itself** (no separate order kind): if the adoption is
   refused, the exchange is void — no race between two orders.
3. ResearchSystem computes `RP_turn` and `CP_turn` from PREV; if the polity's change row is stamped t, it moves
   `x ≤ limit` from one turn's generation to the other pool (`out = x`, `in = ρ·x`) **before** allocation to targets.
4. Allocation, progress, completion, overflow as today.

The adoption-before-research dependency is a declared same-step edge (the D-056 precedent) pinned by a pipeline-order
test, plus a **turn-exact delivery pin**: an exchange issued with an adoption at turn t applies in step t→t+1 exactly
once (CLAUDE.md, T1.9 precedent — replay equality alone cannot see stamping drift).

### 3.5 Determinism and AI parity

- Fixed arithmetic order; a clamp on `x`; no RNG; no Dictionary/HashSet iteration; the exchange row persisted with a
  populated-table test; replay equality plus the turn-exact pin.
- AI parity: the AI uses the same adoption order, the same eligibility predicate and the same limit; its choice of
  direction and amount is an argmax over a composite key (value, integer id) with a tie-dense test; it decides from its
  own state only (information fence I-9). No AI-only bonus or discount.

### 3.6 Governance price of the exchange

D-061 §3 creates two separate currencies; a conversion between them is a new rule that needs its own dated Director
ruling (an amendment line in D-061 §9 or a successor D-record), and ADR-043 (or the ADR that rules D-061.2) must list
the exchange explicitly in its exhaustive scope — ADR-030 §2 lists generation, allocation and progress only, and ledger
§26 item 11 forbids a reusable mechanism. Ledger §26 item 5 (manual percentage allocation) is not engaged by a one-off
transfer, but the ruling should say so.

## 4. The `legal_code_roman` cross-pool gate

**MEASURED baseline (one pool):** `legal_code_roman` completes at turns 635/649/644/639 (AI polity, seeds 1/2/42/7) and
671/673/685/683 (cheapest-first driver); the research stage at 702/716/710/705 (AI) and 996/998/1016/1010 (driver);
`geometry_axiomatic` is the stage atom that binds in every run; university eligibility equals the `legal_code_roman`
turn (the library half is met at the first writing completion, 437–450 AI / 612–624 driver); A5 CORE is bound by
`coinage_electrum` (860–922 AI; 1398–1430 driver). Sources: `measurements/baseline-summary.json`,
`measurements/baseline-*-node-completions.csv`.

**Static model (ESTIMATE; `tools/cp_gate.py` → `data/balance-cp-gate.json`, 1,280 cases):** seeds 1, 2, 42, 7 ×
{AI polity, driver} × reading {Y, T} × β ∈ {0.5, 0.6, 0.7, 0.8} × placeholder scale s ∈ {0.1, 0.25, 0.5, 1, 2} (s = 1 is
the §2.2 anchor, 31 CP/turn at 5,000 people and dt = 10) × detour E ∈ {0, 5k, 10k, 30k} CP spent on other civics after
writing and before `law_code`. CP comes only from the candidate population term over the MEASURED population and dt;
taxation is bought first once its prerequisites are known; `law_code` waits for the first writing node; one completion
per turn; overflow lost. Technology timings are the MEASURED one-pool turns, optionally advanced by the RP the one-pool
run spent on civics before the stage (4,780 RP AI, 9,750 RP driver: −16 to −17 turns). Biases: no Eurekas, no foreign
exposure, no specialist CP terms (all would make CP earlier); the AI's goal order is taken from the one-pool run.

### 4.1 Options

- **Option A — keep `legal_code_roman` in the global research-stage requirement, with tested liveness and lag
  safeguards.** No content change.
- **Option B — remove it from the research-stage requirement; keep the university prerequisite.**
  `researchStage.requires` → `medicine_hippocratic AND (geometry_axiomatic OR algebra) AND stamp_seal`;
  `inst.university` keeps `library AND legal_code_roman` (research.json:29068) — the legitimate institutional
  prerequisite (a chartered corporation), which no frozen record forbids keeping.
- **Option C — an OR of civic routes** (`legal_code_roman OR civil_service_examinations`). Included because the
  analysis justifies a verdict on it, not as a candidate: it is dominated (below).

### 4.2 Key numbers (ESTIMATE, static model; worst case over the 8 seed × driver combinations, β = 0.6)

| scale s | reading | Option A stage delay vs one-pool twin, E = 0 / 5k / 10k / 30k | university eligibility delay (A and B), E = 0 / 10k | A5 CORE delay, E = 0 / 10k |
|---|---|---|---|---|
| 0.25 | Y | 0 / +213 / +404 / never within 1,630 turns | +111 / +722 | 0 / +187 |
| 0.5 | Y | 0 / 0 / +121 / never | +23 / +241 | 0 / 0 |
| 1 | Y | 0 / 0 / 0 / +199 | −27 / +98 | 0 / 0 |
| 0.25 | T | 0 / 0 / +57 / +312 | +1 / +142 | 0 / 0 |
| 0.5 | T | 0 / 0 / 0 / +109 | −28 / +49 | 0 / 0 |
| 1 | T | 0 / 0 / 0 / 0 | −43 / −3 | 0 / 0 |

- **Smallest placeholder scale passing P2 (stage ≤ twin + 10 turns on every seed and driver):** with no detour 0.1–0.5
  (reading T 0.1–0.25; reading Y 0.25–0.5, by β); with a 10,000-CP detour 0.25–1.0 (T 0.25–0.5; Y 0.5–1.0); with
  30,000 CP, T 0.5–1.0 and Y 1.0–2.0 (Y at β = 0.5 never). (`thresholds` in the JSON.)
- **Liveness:** the gate civic fails to complete within the 1,630-turn horizon in 17 of 160 grid cells (`anyNever`):
  reading Y with s = 0.1 and a detour ≥ 10,000 CP (≥ 5,000 CP at β ≤ 0.6), s = 0.25 with 30,000 CP (β ≤ 0.7; also
  10,000 CP at β = 0.5), s = 0.5 with 30,000 CP (β ≤ 0.6); reading T only at s = 0.1, β = 0.5, 30,000 CP. Non-termination is itself the finding: a slow CP economy plus a civic
  detour can lock all five subtrees for the whole game under Option A.
- **Option B:** the stage no longer depends on CP at all: 16–17 turns *earlier* than the one-pool twin in every case
  (freed RP). University eligibility stays CP-paced exactly as under A (column above).
- **Option C:** zero gain in all 1,280 cases; the examination route (taxation + appointed_officials +
  civil_service_examinations = 9,050 CP, plus `paper` at 959–1,062 AI) is never earlier than `legal_code_roman` (at
  least 17 turns later where computable).
- **Tax edict (legacy live gate):** taxation completes at 329–339 (T) / 340–351 (Y) for the AI at s = 1 vs 328–339
  MEASURED; the Age III half (A3 entry 412–425, MEASURED) still binds the operational edict, so its date does not move.
- **Gate window:** CP accumulated between the first writing node and the one-pool stage turn at s = 1, β = 0.6: AI
  ≈ 19,000 (Y) / 43,000 (T); driver ≈ 35,000 (Y) / 120,000 (T) — against a gate closure of 3,730 CP.

### 4.3 Effects by option

| Effect | Option A (keep in stage) | Option B (stage without the civic; university keeps it) | Option C (OR route) |
|---|---|---|---|
| M6/M8 boundary (D-D) | unchanged — completion of a micro civic gates knowledge; no adoption involved | unchanged | unchanged, but puts a further PROPOSED civic into a ratified gate (breaks package rule R-C2) |
| current gates | stage atom (research.json:254), `inst.university` (:29068), A5 CORE (ages.json:85) all CP-paced | stage purely technological; `inst.university` and A5 CORE remain CP-paced | as A, plus `paper` and `appointed_officials` enter the stage closure |
| Age milestones | `a5_systematic_law` stays CORE (a micro civic, never a regime form); P4 covers it | same | same |
| AI research | the AI goal closures span two pools; an AI civics lane must work gate civics first (priority 1), or the AI stalls Technology; `AiResearchPolicy.cs:165-178` university goal split per pool (T6.16) | the stage goal becomes RP-only; the university goal still needs the civics lane | as A, larger closure |
| turn pacing (ESTIMATE) | no delay at s ≥ 0.5 (T) / ≥ 1 (Y) up to a 10k-CP detour; large delays or non-termination when CP is slow and spent elsewhere | stage −16/−17 turns in every case; universities as A | no gain over A in any case |
| state / schema | none beyond D-061's CP tables | none beyond D-061 | none beyond D-061 |
| tests | `ResearchContentTests.cs:233-252` kept; new liveness/lag acceptance (P1–P4, P7) on seeds 1/2/42/7 × both drivers vs the `PreCivicsPoints` twin; a rig where CP = 0 proves the stage is never reached and that the lock names `legal_code_roman` across trees; a tie-dense AI lane test | the stage pin superseded by a dated pin; goldens move only where the civic lagged (it never did in the baseline, MEASURED) | new causal-review evidence; new pins |
| governance price | none beyond D-061.3 + D-061.8 ruled together (docket C.1); a dated note recording the tolerances as T6.3 acceptance | a **dated Director content ruling** amending the D-046 F§1 ratified stage expression, dated notes on ADR-029 §8 and the research.json:255 `_doc`; **not** a re-ask of the ★ D-044 R4 question (all five subtrees still open together at one stage), but the stage then carries no institutional atom | as B plus a D-047-style causal review of the new edge |

### 4.4 Recommendation (the choice stays with the Director)

**Option A**, with these safeguards as T6.3 acceptance: tolerances P1–P7 (`civics-tree-proposal.md` §6, P5 now stated
in CP terms and P7 added for the tax edict); an AI civics lane that funds gate civics first; the cross-tree lock named
in the UI; the remedy order of P6 (AI lane → D-061.3 inputs → Option B), never a cost change. Reasons: A changes no
ratified content; the static model shows no stage delay for any placeholder scale ≥ 0.5 (reading T) or ≥ 1 (reading
Y) with up to a 10,000-CP detour; B buys protection the model shows A does not need at those scales, at the price of a
ratified-content ruling, and leaves the university — the actual research-cost seam — CP-paced anyway. **B is the
pre-registered remedy** if P1–P4 fail in T6.3 after the AI-lane and D-061.3 remedies. **C is rejected** (dominated in
every case). If D-061.2 rules reading Y, the CP coefficient must sit at the upper end of the tested range (or above) for
A to hold with detours; that interaction is the reason D-061.2, D-061.3 and D-061.8 should be ruled together.

## 5. Governance conflicts found and resolution paths

| id | Conflict | Records / sections | Resolution path |
|---|---|---|---|
| G-1 | "Separate **accumulated** pools" vs no bank or reserve | D-044 R20-D (`:795`, `:816-818`, ★ partial progress); ledger §26 item 4; m6-spec §2 | read "pool" as generation + per-node progress (no ruling needed; recommended); a bankable balance needs a dated Director ruling superseding R20-D / item 4 for that pool, plus a type decision (§2.6) |
| G-2 | RP↔CP exchange vs two separate currencies and the per-turn scope | D-061 §3 (two currencies); ADR-030 §2–§3; ledger §26 items 5, 11; the 2026-10-20 instruction ("do not … combine the currencies without an explicit, evidence-backed design decision") | a dated Director ruling (D-061 §9 amendment line or a successor record) plus the exchange listed in ADR-043's exhaustive scope; IMPLEMENTATION DEFERRED to M8 (D-D) |
| G-3 | Social-sciences universities as a CP **yield** vs what universities do | D-044 R5 (`:162-182`), ★ R20-H (`:797`): universities reduce EffectiveCost; "no flat science"; `universityTypes` are the five Tree-1 subtrees (R3 ★) | either an R5-shaped **civic cost factor** (no new ruling beyond D-061) or staff-based CP from a civic institution kind under D-M — the latter needs a dated Director note on R5's scope |
| G-4 | Per-sim-year CP (law 3) vs D-061.2's per-turn recommendation | CLAUDE.md law 3; ADR-030 §3; ledger §26 item 11; D-061 §4.1 | an OPEN DECISION, not a defect: reading Y needs no ADR; reading T needs ADR-043; §2.4 gives the evidence |
| G-5 | CP stated as a fraction of RP in the package's own tolerance and recommendation | D-061.3 ("no fraction of RP"); CR-023 option 2; Director intent 2026-10-22 | **corrected in canonical data** (D12 meta: P5 restated in CP terms; §5 recommendation; the ratio stand-in marked SUPERSEDED); `tools/balance.py`'s `cpCoupling` block remains a superseded stand-in |
| G-6 | Option B vs ratified stage content | D-046 F§1 (`:367`); ADR-029 §8; research.json:255 `_doc`; D-044 R4 ★ (not engaged) | a dated Director content ruling; dated notes on ADR-029 §8 |
| G-7 | Artists / literary scholars as **classes** vs the closed D-018 roster | D-018 §1–§2 (11 + 1 reserved) | treat them as specialists (staff, notables), not classes; using the reserved slot needs a D-018 amendment |
| G-8 | Embassies and spies before diplomacy exists | D-D; m6-spec §2 and §8 (espionage DEFER M8) | M8 sources only, via the exposure-credit pathway, under the fog information fence |
| G-9 | Old single-pool rulings | D-044 R2/R12, D-046 G5, ledger §26 items 2–3, D-I (2026-10-20), ADR-030 §3, the one-pool test | superseded only by the D-061 §9 ruling (append-only); nothing in this package edits them |

## 6. What the Director is asked to rule (staged)

1. **Architecture (first gate):** D-061.1 (supersession for Civics only); G-1 reading of "pool"; D-061.2 (Y or T);
   the principle of §2.1 (independent inputs, each person once, no RP term).
2. **Mechanics (later gate):** D-061.3 terms and placeholders; S1b reach weighting; G-3 form of the social-sciences
   source; the exchange's economics (§3.3) and its ADR scope (G-2) — with M8.
3. **Content (later gate):** D-061.8 / KD-03 (Options A/B/C); which institutions and classes become CP sources at
   which milestone (§1).

## 7. Reproduction

```
cd docs/m6-plan/knowledge-redesign
python3 -I tools/cp_gate.py                      # -> data/balance-cp-gate.json (ESTIMATE, static model)
python3 -I tools/integrate.py && python3 -I tools/render.py
```
