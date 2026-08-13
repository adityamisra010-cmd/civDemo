# M4 — NEIGHBOURS, CONFLICT, AND A WORLD THAT CAN RUN SHORT

### The first milestone spec written under S8 §4.1. Its own conformance to that section is part of the deliverable (§0).

**Status: R-1, R-2 and R-3 RULED (director, 2026-08-07). THE PACKET LIST IS FINAL.**
This spec OBSERVES and PROPOSES. It amends no frozen or ratified document.

---

## §0. S8 §4.1 CONFORMANCE, REQUIREMENT BY REQUIREMENT

| S8 §4.1 requirement | where it lives here | state |
| --- | --- | --- |
| **1. Foundations audit as packet one** | **§4, T4.1** — with its four named checks, its enumeration rule, its dispositions and its written-table acceptance | **SATISFIED as a packet definition.** The audit itself has not run; its scope is enumerated FROM CODE by the packet, not asserted here |
| **2. Dimensional declaration — §3 system notes** | **§3.2** — every new quantity declares units at introduction; each core equation checked for unit balance | **PARTIAL BY CONSTRUCTION, and stated as such.** M4's equations are not designed yet; §3.2 declares the units of the quantities the spec commits to and records that each packet completes its own declaration before implementation. Anti-self-certification binds: any unit for an EXISTING quantity is read off the producing expression and cited |
| **3. Corridor independence** | **§3.3** | **SATISFIED for the corridors M4 touches**, including the standing failure case (`densityPerArableKm2`) restated with its 20-seed measurement |
| **4. Coupling map** | **§3.4** | **SATISFIED as the spec author's PROVISIONAL reading.** S8 §4.1 makes T4.1 AUTHORITATIVE over it; a T4.1 finding that moves a map row is a §1 LIVING revision, not an amendment |

**One conformance limit stated plainly rather than papered over.** Requirement 2 asks that "every
core equation is checked for unit balance on paper before implementation". M4's core equations —
the bounding rule, the AutoResolver, the claim model — **do not exist yet**; specifying them here
would be pre-specification of what only design settles, which §4.1's own closing line excludes.
So §3.2 declares units for what is committed and makes the check performable, and each packet
carries its own declaration. If the director wants the equations in the spec instead, that is a
larger spec and a different instruction.

---

## §1. DECISIONS CLOSED

Carried from ratified records; each verified against the tree (§8 provenance, §9 findings).

| # | decision | source |
| --- | --- | --- |
| 1.1 | **Money: M5, taxes IN KIND (Option B).** M4 ships no currency | GOV-2 §1a, first recorded there |
| 1.2 | **Notables: SPLIT BY ROLE (Option B).** M4 ships GENERALS ONLY — the minimal named actor D-011 §2's delegate-with-agency requires; M6 adds battle stats and experience; M8 adds political notables | GOV-2 §1b |
| 1.3 | **M4 STAYS WHOLE** rather than splitting | GOV-2 §6, director ruling |
| 1.4 | **The transport packet's three levers are ONE design conversation** — Q-A water routes, Q-C draught animals, Q-E route improvement — to be scoped together | `queue.md` Q-E (":541" — "Q-C … and Q-E are ONE design conversation and should be ruled together") |
| 1.5 | **The comparative-advantage exit criterion was WITHDRAWN, not failed, at M3** and travels here with T3.10 | `milestones.md` M3 entry, director exit ruling |
| 1.6 | **Strategic war is AutoResolver ONLY at M4.** The battle layer proper is M6 | `d011-battle-layer-addendum.md:52` |

---

## §2. SCOPE FENCE — WHAT M4 EXPLICITLY DOES NOT DO

Named so packets do not drift across the boundary.

- **M5 GOVERNS.** Taxes, in kind (1.1). No currency, no fiscal system, no administration at M4.
- **M6 FIGHTS.** The battle layer, BattleSetup/BattleOutcome, general stats and experience.
- **THE VISUAL MILESTONE SITS BETWEEN M5 AND M6** (D-038 E1) and absorbs the deferred symbology
  packet plus D-038 Part H's composed settlement sprites. **No M4 packet authors map art.**
- **D-039 IS M6, WITH ONE EXCEPTION.** Command friction, reconnaissance and siege are ruled at M6;
  **only Part B's investment mechanism touches M4.** D-039 D5 (siege starvation) is additionally
  hard-blocked on B-2 — a siege cannot starve a city that cannot starve.
- **NOT M4:** vintaged capital (tool-wear residue, `queue.md`); the lattice-stride change itself
  (M4 must RULE it once — §4 T4.9 — but the re-stride is a worldgen/pathfinding cost question);
  cultural plurality (M8/M9), which is where R-3 bites.

---

## §3. SYSTEM NOTES

### §3.1 The shape of the milestone

M4 is the milestone where the world acquires **neighbours** and **scarcity that can bite**. Those
two are not independent: conflict and foreign trade both assume scarcity, and M3 measured that
scarcity is currently unreachable. That is why the packet order in §4 is what it is.

### §3.2 DIMENSIONAL DECLARATION (S8 §4.1 requirement 2)

**Anti-self-certification binds:** for any quantity produced by EXISTING code, the unit is READ
OFF the producing expression and CITED, never asserted from memory.

Quantities this spec commits to, with units declared at introduction:

| quantity | unit | status |
| --- | --- | --- |
| **granary capacity** (B-2 base) | units of good — the same denomination as `GoodStockRow.Amount`, a conserved `long` (`WorldState.cs`, `Conserved`) | NEW; must be DERIVED, not chosen (B-2's own ruling stages it as "base layer, derived not chosen") |
| **spoilage rate** (B-2 base) | fraction of stock per SIM-YEAR — law 3; integrated with `dtYears`, never per-turn | NEW |
| **claim / control / recognition** (D-037) | three separate quantities; units TBD by the claim packet, but structurally separate from day one | NEW |
| **general competence / traits** | dimensionless parameters into the AutoResolver | NEW |
| **bind ratio** (Q1) | dimensionless: land-capacity ÷ labour-capacity per settlement | MEASURED BY T4.1 |
| `EffectiveArableKm2` | fertility-weighted km²; read off `CatchmentSystem.BlockArableKm2` → `LatticeMap.BlockArableKm2` | EXISTING — cited, not asserted |
| `minSpacingKm` | kilometres (`worldgen.json`) | EXISTING |

**Each packet completes its own dimensional declaration before implementation**; T4.1 is
authoritative over every row above that describes existing code.

### §3.3 CORRIDOR INDEPENDENCE (S8 §4.1 requirement 3)

M4 touches three corridors. For each: what it is independent OF, and how a change could make it
fail.

**`densityPerArableKm2` — THE STANDING FAILURE CASE, now with a 20-seed measurement.**
A LITERAL band `[0.15, 0.6]` with no derivation on record, so the algebraic test passes it
trivially and proves nothing; it discharges the requirement through (ii) alone. **Its denominator's
drivers:** catchment radius, lattice stride, the travel budget, and the T3.8 size-tier bonus —
every one of which M4 can move (colonization changes settlement count; the transport packet
changes travel cost; the stride ruling changes block size). **It already failed (ii) once:** the
denominator moved ~15× at T3.2b while the numerator barely moved. **Measured at M3 exit:** 20
seeds, 1.1428–1.6501, mean 1.3952 — quarantined in `corridors.json` with window, owner and
history. **M4 must not re-derive this band without satisfying (ii) explicitly**, and CR-002 is the
packet that owns it.

**`migrationGrossPerDecade` — a NEW independence question M3 exit raised.** Band `[0.001, 0.01]`,
metric already normalised by person-years (`AutoplayMetrics.cs:139`). Measured: corr(migration,
population) **+0.737**, corr(migration, arable) **+0.815** — so the metric is **NOT independent of
world size** even after normalisation, and the floor is ABSOLUTE. One seed in twenty (seed 9, the
smallest world) falls 2 % under it. **Quarantined at M3 exit.** Whether an absolute floor is the
right shape is the open question, and it is a corridor-independence question, not a tuning one.

**Any NEW M4 corridor** (war outcomes, claim stability, trade volume) is REFUSED at spec time if
its denominator moves with the measured system. The CR-002 cancellation identity is the signature
to test against, algebraically and counterfactually.

### §3.4 COUPLING MAP (S8 §4.1 requirement 4) — **REVISED BY T4.1 UNDER THE LIVING CLAUSE**

> **REVISION RECORD (director ruling, 2026-08-07).** S8 §4.1 makes T4.1 authoritative over this
> map; a finding that moves a row is a **LIVING revision, not an amendment**. **Two rows moved:**
> `AutoResolver + generals` (R-1 ruled PERSON, and purchase ruled OUT — see T4.8) and
> `colonization` (`minSpacingKm` amended in the open by t4.1b rather than bypassed). **Nothing
> else moved:** B-2's row stands — T4.1's Q1 quantifies how far B-2 must move the world (~9–19×
> from land-binding at t650) without changing what B-2 perturbs. Claims, transport and T3.10 are
> untouched.
>
> **A CLASSIFICATION CORRECTION, recorded because §7.12 applies to classifications as well as to
> numbers.** T4.1's review record initially called two things "coupling-map revisions". Checked
> against this merged map, **neither was a map row**: **T4.14 is not in the map at all** (a
> packet-scope change), and **the inert tool bonus is INBOUND**, which this map explicitly assigns
> to T4.1's own scope — *"INBOUND … is T4.1's scope and is enumerated from code there, not here."*
> The mislabelling was found by reading the artifact before the director ruled on it. **What
> actually moved is the two rows above**, one from the audit and one from the Q4 direction ruling.

**PROVISIONAL AS AUTHORED; T4.1 IS AUTHORITATIVE**

OUTBOUND: what M4's new systems perturb.

| new system | perturbs | re-anchoring expected |
| --- | --- | --- |
| **B-2 store bounding** | every goods stock; all price series (band-edge pinning); Shelter (timber buffer); Comfort; the grain reserve; `densityPerArableKm2` indirectly via starvation becoming reachable | **ALL THREE GOLDENS + first-reign.** Re-pin once, in the same early window as T4.1's corrections |
| **colonization** | settlement count; every catchment; `EffectiveArableKm2`; the density corridor's denominator; migration destinations. **`minSpacingKm` is NO LONGER perturbed here** — it is amended in the open by t4.1b's ADR, before and independently of colonization | goldens; density + migration corridors; `CatchmentSystem` recompute triggers (D-016). **The spacing change's own golden re-pin and catchment recompute belong to t4.1b, not to this row** |
| **claim/control/recognition** | new tables only; schema version bump | goldens (schema), no behaviour |
| **AutoResolver + generals** | new tables; **bucket extraction via `Ledger.Transfer` (R-1 RULED person)**; NOT purchase — payment is money, M5 | goldens; **the law-1 conservation audit, as packet CONTENT** |
| **transport levers** | travel cost → path costs → catchments → arable → density corridor; trade deadband | goldens; density corridor; escalation 1's arithmetic |
| **T3.10 migrated work** | MalthusLite test power (BINDING restoration); calibration corridors | corridor bands; test power assertions |

INBOUND (what M4 stands on) is T4.1's scope and is enumerated from code there, not here.

---

## §4. TASK PACKETS — PROPOSED

Adversarial-mandatory flags follow the M3 convention: mechanism-bearing packets with corridor or
conservation surface get lens review.

### T4.1 — FOUNDATIONS AUDIT [PACKET ONE, MANDATORY, S8 §4.1]

**Scope:** DEPENDENCY, not perturbation, **enumerated from code**: every existing constant and
every denominated state-table field that M4's §3 equations consume, followed transitively through
each field's producer. "Constant" includes code-resident literals. A consumption site is any
reader — systems, `AutoplayMetrics`, corridor computations, view-models.

**Deliverable:** a WRITTEN TABLE, one row per in-scope quantity, answering (a) real units and
physical possibility, (b) denomination vs consumption site, (c) derived or merely chosen, (d)
visible to tuning. No unexplained omissions; "never derived" is a stated answer. Dispositions per
S8 §4.1: (b) mismatch or frozen conflict → CR; impossible magnitude under (a) → escalate before
any dependent packet builds; (c) fails but (a)(b)(d) clean → record "chosen, never derived" and
queue; "a better way exists" → queue.

**FOUR NAMED CHECKS already homed here:**

1. **Q1 — the canonical land-capacity / labour-capacity BIND RATIO, never measured.** T3.4c's rig
   used `outputPerFarmerPerYear ×1e6` — a definitely-binds value, not a threshold — which served
   its criterion but tells nobody whether the real distance between the shipped world and a
   land-capped one is 3× or 1e5×. **That distance is exactly what B-2 and colonization aim to
   close**, so T4.1 is the packet that needs the number. Measure the actual bind ratio per
   settlement on the canonical world.
2. **D-037's data model** — claim, control and recognition **structurally separate in the shipped
   schema** from day one, supporting overlap, claim-without-control and asymmetric recognition.
   D-037 D1: omission "would make Part C unbuildable later". The audit row VERIFIES separation;
   it is not satisfied by spec prose.
3. **The notables retrofit fields** — the M4 general row carries: stable id with the NAME in an
   id-keyed registry (ADR-001 bans strings in `unmanaged` rows); home bucket link; traits and
   competence; MUTABLE experience; lifecycle (death, defection, purchase, falling out). Verified
   against GOV-2 §1b's enumerated table. **This is the item whose omission makes the role split
   expensive.**
4. **`minSpacingKm` vs the Scale Charter's settlement counts** — a (b)-shaped constant-vs-consumer
   mismatch. Charter: "~50 (ancient) → 300–800 (late)". Measured greedy saturation at the shipped
   `minSpacingKm = 480`: **min 33, median 45, max 74** across seeds. Ancient target exceeds
   measured capacity by ~1.5×; late target by **~9–24×**. ADR-017 re-examined D-025's siting
   clause days before M3 closed and ruled **"SITING: STANDS UNAMENDED"** on measured grounds.
   Three closure routes, recorded as options for the director, none chosen here: colonization
   founding settlements that do not respect `minSpacingKm`; changing the spacing constraint; or a
   larger map (D-015).

**Adversarial:** NO — an audit produces findings by construction; its acceptance is the
director adjudicating the table.

### T4.2 — B-2 STORE BOUNDING [THE FIRST REAL PACKET; ADVERSARIAL-MANDATORY]

**Scope:** the base layer only — **spoilage + granary capacity, DERIVED not chosen**, per B-2's
own staging. Enrichment (B-2a/B-2b) is deliberately NOT in this packet.

**Why it is first:** M4's blocking material states conflict and foreign trade both assume scarcity
can bite, and it cannot. Measured: reserves **~1,240 years** post-T3.5b; **zero starvation across
20 full-length runs**; and T3.6 measured the interaction **unbounded in the other direction too**
— under sustained maximum drive the mechanism drains a granary to zero, so there is no bound on
either side. B-2b's own rule binds here: adding detail on top of a base known to be wrong makes
the error harder to find.

**PRE-COMMITTED READINGS — Q-B's FIVE PREDICTIONS (§7.13/§7.15, stated before implementation):**

| # | prediction | discriminating observable |
| --- | --- | --- |
| P1 | bounded stores make stocks **CYCLE** rather than ratchet | stock series sign-flips per good; a ratchet has none |
| P2 | prices **UNPEG** from band edges | count of non-grain price rows at 0.05 or 20.0 — currently 11 of 13 goods pinned |
| P3 | **Shelter decay becomes reachable** | Shelter < 1.0 within a stated horizon after a farm-100 % order (currently holds at 1.0000 for decades on the timber buffer) |
| P4 | **Comfort-as-stock becomes meaningful** | a household-goods stock that does not saturate at 1.0 forever |
| P5 | **band-edge pinning stops degrading RED PROOFS** | a price-step perturbation shows per-good effects without needing an aggregate statistic (the T3.11 P1 hazard) |

**If bounding delivers even three of five, several separately-filed problems were one problem.**
Each prediction is reported separately whatever the others do; a composite "it worked" is refused.

**Depends on:** T4.1 (the bind ratio, Q1, tells this packet how far the world is from land-capped).

### THE REST OF THE PACKET LIST — PROPOSED, NOT FIXED

| packet | one-line scope | depends on | adversarial |
| --- | --- | --- | --- |
| **T4.3 — D-037 claim model** | claim, control, recognition as three separate quantities; tables + schema only, no polity behaviour. **CARRIES TWO NAMED PROHIBITIONS — see below** | T4.1 check 2 | NO (schema) |

### T4.3 FENCE — TWO NAMED PROHIBITIONS (director ruling on T4.1 Q2, 2026-08-07)

D-037 D1 says omitting the three-quantity model *"would make Part C unbuildable later"*. T4.1's
audit found the two SPECIFIC SHAPES that would do it, and they are promoted here from findings to
**prohibitions an agent reads at packet start**, in the audit's own words:

> **PROHIBITED 1 — control as an OWNER ID ON THE PLACE ROW.** Storing control that way *"silently
> forecloses overlap"*.
>
> **PROHIBITED 2 — recognition as a FLAG ON THE POLITY.** A single `recognised: bool` per polity
> *"cannot express"* asymmetric recognition.

**Both are the NATURAL FIRST IMPLEMENTATION**, which is exactly why they are named rather than
left to be rediscovered. **All three quantities ship as RELATIONS:** claim keyed by
(polity, place), control keyed by (polity, place), recognition keyed by
**(recogniser, recognised)**.
| **T4.4 — colonization / land clearance** | founding rules, site selection, clearing cost, sprawl constraint; migration extended to depart into UNCLAIMED land; refugee foundings may be stateless (D-037 B1) | **T4.2** (land pressure needs scarcity), T4.1 check 4, T4.9 | **YES** |
| **T4.5 — non-state peoples from turn zero** | D-037 B3 | T4.3 | YES |
| **T4.6 — trade & foreign trade** | the second polity's exchange; foreign-trade rules | T4.2, T4.3, **T4.7** | **YES** |
| **T4.7 — the transport packet (Q-A + Q-C + Q-E as ONE)** | water routes, draught animals, route improvement — **ruled one design conversation**; the lever set that could make escalation 1's deadband reachable | **T4.9** (rivers cannot live on a stride-4 lattice) | **YES** |
| **T4.8 — strategic war + AutoResolver + notables-as-generals** | delegate-with-agency; general competence and traits parameterize the AutoResolver. **R-1 RESOLVED: notables are PEOPLE** — bucket extraction via `Ledger.Transfer` and the **law-1 audit are packet CONTENT, not a follow-up**. **PURCHASE IS OUT OF SCOPE** (payment is money, M5). **ACCEPTANCE MUST INCLUDE: a notable can be BORN, DIE and DEFECT with no bookkeeping outside the Ledger** — the property Option B was taken for, which T4.1's escalation established holds for three of four lifecycle events | T4.3 | **YES** |
| **T4.9 — RULE THE LATTICE STRIDE ONCE** | one architecture call serving three blocked consumers: rivers (transport), village catchments, settlement density (colonization) | — | NO (ruling + measurement) |
| **T4.10 — T3.10's migrated work** | calibration extension; **the BINDING MalthusLite power restoration**; CR-002's deferred geometry fix; the density + migration corridor quarantines with their 20-seed ranges; the withdrawn comparative-advantage criterion | **T4.2** (MalthusLite restoration needs cycling stocks) | **YES** |
| **T4.11 — T3.7 merchants** | the class that emerges on trade volume | **T4.6** (merchants emerge on a volume that must first exist) | YES |
| **T4.12 — the migration-weight packet** | T3.4c ruling 2, still unhomed: design point missed 2.3×–8.1×, metric unstable in N and seed | — | **YES** |
| **T4.13 — Comfort-as-stock** | household goods depleted by USE and replenished by crafting — a different equilibrium from housing's maintenance shape, NOT a copy | **T4.2** (unbounded stock saturates at 1.0 forever) | YES |
| **T4.14 — the three undiagnosed M3 observations** | (a) artisan EMERGENCE: why bimodal — 1 or ~26–60, never in between (§9 F4); (b) the artisan COLLAPSE to zero in half the settlements (§9 F4b) — recede arm, class mobility, or settlement decline; (c) Kunaetho's late grievance rise. **First obligation: replay the director's own session log when it is available** | T3.12a reporter | NO (diagnosis) |
| **T4.15 — M4 exit artifact** | version strings, README, sweep incl. nightly, milestones entry, session brief | all | NO |
| **T4.16 — CLONE ARCHITECTURE (R-3)** | **DESIGN AND MEASUREMENT ONLY, no implementation.** Produces an **ADR under S8 §2** — both `m0-kernel-spec` §3.2 and the kernel clone are inside the M0 freeze — stating what breaks, which tests and docs change, and the schedule price | T4.1 | **YES** |

### T4.16 — CLONE ARCHITECTURE [DESIGN AND MEASUREMENT; ADR-PRODUCING; ADVERSARIAL-MANDATORY]

**Deliverable: an ADR under S8 §2, not a code change.** It states what breaks, which tests and
docs change, and the schedule price. Implementation is scheduled by the ruling on that ADR.

**THREE NON-NEGOTIABLES — constraints, not trade-offs.**

1. **READ-ISOLATION IS WHY THE COPY EXISTS.** Systems read `Prev` and write `Next`; that is what
   makes a turn's systems independent of ordering, and CI gates on it. Any scheme must keep
   `Prev` **genuinely immutable for the whole turn**. **A scheme that buys memory by weakening
   that is REJECTED, not traded off.**
2. **DETERMINISM IS ABSOLUTE.** Same seed, same order log, same world hash, before and after.
   **Asserted, not assumed** — T3.12a's two-axis assertion is the named precedent, **including a
   vacuity guard so the assertion cannot pass trivially.**
3. **THE GOLDENS ARE THE SAFETY NET AND MUST NOT MOVE.** A clone-architecture change is a change
   of REPRESENTATION, not of behaviour. **If a golden moves, that is a finding and the packet
   STOPS** — it means the change altered what the world does.

**CANDIDATE APPROACHES — TO MEASURE, NOT TO PICK HERE.**

| approach | mechanism | expected shape |
| --- | --- | --- |
| **copy-on-write per table** | **Law 6 (system isolation) already declares which systems write which tables**, so the kernel can know STATICALLY which tables can change in a turn; unchanged tables are shared by reference | read-isolation preserved **exactly** — `Prev` is never written |
| **lazy clone on first write** | the same effect, decided **dynamically** rather than declared | fewer static guarantees, no pipeline declaration needed |
| **delta journal** | `Next = Prev` + a change list; reads consult the list | lowest memory, **highest read cost — probably wrong for a read-heavy sim, but MEASURE it rather than dismissing it** |

**SEQUENCING, WITH THE DIRECTOR'S OWN REASONING AND A PROPOSED SLOT.** Late-game slowdown is the
risk, and **today's measurement is 0.078 MiB — nothing. So this is NOT urgent by measurement.**
But **every milestone adds tables and the clone touches all of them**, so it is **cheap now and
expensive later** — the same shape as D-037's data model.

**PROPOSED SLOT: early in M4, immediately after T4.1, and NON-BLOCKING.** The reason is specific
rather than "do important things first": M4 itself adds several tables — claims/control/
recognition (T4.3), notables-as-a-conserved-stock (T4.8, now with a Ledger surface per R-1), war
state — so **measuring the clone AFTER them measures a moving target, and the ADR's "what breaks"
list grows with every table added before it is written.** Running the measurement while the table
set is still M3's gives the ADR a stable baseline and a smaller blast-radius inventory. It is
design-and-measurement only, so it **blocks nothing**; the implementation slot is the ADR's to
propose and the director's to rule.

---

## §5. SEQUENCING — WHICH PACKETS ARE GATED ON WHICH

```
T4.1  FOUNDATIONS AUDIT ── authoritative over §3.2 and §3.4
  │
  └─> T4.2  B-2 STORE BOUNDING ─────────────────────────────┐
         │                                                   │
         ├─> T4.4  colonization        (land pressure needs scarcity)
         ├─> T4.10 T3.10 migrated      (MalthusLite needs cycling stocks)
         ├─> T4.13 Comfort-as-stock    (saturates at 1.0 without bounding)
         └─> T4.6  trade & foreign trade
                    ^
T4.9  STRIDE RULING ──> T4.7 transport ──┘
T4.3  claim model ──> T4.5 non-state peoples
                 └──> T4.8 war + generals   (also gated on R-1)
T4.6 ──> T4.11 merchants
T4.1 ──> T4.16 clone architecture (design+measurement, NON-BLOCKING, runs while
                                   the table set is still M3's)
```

**The gates, stated:**

- **Everything scarcity-dependent waits for T4.2.** B-2b's rule: detail on a base known to be
  wrong makes the error harder to find.
- **D-039 D5 (siege starvation) is hard-blocked on T4.2** — and is M6 anyway.
- **T4.7 waits on T4.9**: the T3.6b water counterfactual's lattice pass could see only the SEA,
  because stride-4 majority-water blocks hide rivers. That was a resolution artifact, not
  economics. Building water transport on a lattice that cannot represent a river repeats it.
- **T4.8 waits on R-1**, because whether a general is extracted from their bucket decides whether
  the packet has a conservation surface at all.
- **T4.16 blocks nothing and is deliberately EARLY** — not by importance but because the ADR's
  blast-radius inventory grows with every table M4 adds before it is written.
- **Golden-moving order:** T4.1's corrections and T4.2 both move goldens. Per S8 §4.1's ordering
  note, audit-then-correct-then-build lands them in one early window so goldens move once.

---

## §6. EXIT CRITERIA — PROPOSED

- All packets accepted; each merged on a director ruling.
- **Scarcity can bite:** starvation reachable on the canonical world under a stated condition, with
  the five Q-B predictions reported individually.
- Determinism suites green on the M4 world; xproc; first-reign shape asserts standing.
- Goldens pinned with dated history lines; the driven golden extended to whatever M4 adds.
- **Calibration battery green across ≥20 seeds with proven teeth**, quarantined corridors reported
  with measured ranges rather than silently gating (the T3.12 mechanism).
- **The nightly has been green, and someone has read it** — the M3 CI process defect (eleven silent
  reds) closed.
- Director exit session from the CI zip, log replaying hash-identical, **with a T3.12a replay
  report attached** — M4's exit should not make the director the measuring instrument again.
- `milestones.md` M4 entry with its known-open list; `m4-exit` Release.

---

## §7. THREE RULINGS — TAKEN (director, 2026-08-07)

**All three turn on the same question: IS A THING COUNTED, OR LABELLED?** That framing is not a
presentational convenience — it is why they are presented together and why R-2 cannot be taken
before R-1.

- **R-1** asks whether a notable is COUNTED (a conserved person, moved by `Ledger.Transfer`) or
  LABELLED (an annotation on a bucket that stays counted where it is).
- **R-2** asks what to call the thing that gets counted — and one of `stock`'s three live meanings
  is *the population stock that exists only if R-1 says COUNTED*.
- **R-3** asks what it costs to COUNT things densely: rows that exist at zero population, because
  the cross-product is instantiated whether or not anyone lives there.

### R-1 — IS A NOTABLE A PERSON?

D-010: notables emerge FROM aggrieved buckets — *"the demagogue who leads the uprising emerges
from the aggrieved bucket, named, with traits"*.

| option | what it means | cost |
| --- | --- | --- |
| **A — LABEL.** The notable remains counted in their bucket | An annotation; no conservation surface | Cheap now. But a general who dies, defects or is purchased (D-021 valve 5) changes nothing conserved, so every later lifecycle mechanic has to invent its own bookkeeping |
| **B — PERSON.** The notable is EXTRACTED via `Ledger.Transfer` | Notables become a conserved population stock with births, deaths and a law-1 audit | Expensive now: a new conserved quantity, audit rows, and every notable event becomes a Ledger flow. But lifecycle, defection and purge are then conservation-exact for free |

**RULED: OPTION B — A NOTABLE IS A PERSON.** Extracted from the bucket via `Ledger.Transfer`; a
conserved population stock with births, deaths and a law-1 audit.

*Rationale, recorded:* lifecycle, defection and purge become **conservation-exact** rather than
each inventing its own bookkeeping — and D-021 valve 5 already requires all three ("leaders die,
defect, are bought, or fall out"). **The cost is accepted deliberately:** a new conserved
quantity, audit rows, and every notable event as a Ledger flow.

**CONSEQUENCE FOR T4.8, binding:** generals ship with the **conservation surface from day one**,
and the law-1 audit is **part of the packet, not a follow-up**.

**NARROWED, NOT REOPENED (director ruling, T4.1 Q3 escalation, 2026-08-07): M4 DOES NOT IMPLEMENT
PURCHASE.** T4.1 tested R-1's premise before T4.8 could build on it and found it holds for three
of four lifecycle events: **born, dies and defects are clean Ledger flows; IS BOUGHT is not** —
the person moves, but the **consideration is a SECOND flow, and payment is money, which is M5**
(GOV-2 §1a).

**R-1 STANDS.** The person half is exactly what Option B was taken for. What does not hold is the
PAYMENT half. **The notable moves via `Ledger.Transfer` whatever the reason; the consideration for
a purchase is a separate flow that cannot exist until money does.** D-021 valve 5's "bought"
therefore arrives at **M5 alongside the fiscal system**, not at M4 with generals.

**Cross-reference: GOV-2 §1a's rewrite inventory** already enumerates every ratified line that
assumes a money stock — *"this list is the M5 spec author's rewrite inventory"* — and **D-021 valve
5's "bought" is one more entry for it.** Note that the **in-kind ruling makes a purchase
expressible IN PRINCIPLE** (grain, land, office), **but designing it is M5's work and not a
workaround to add here.**

### R-2 — `stock` AND `source` NAMESPACING

CONV-1 left both PROPOSED rather than ruled, **deliberately, because both touch R-1**.

- **`stock` — three live meanings:** the goods inventory (`GoodStocks`, a serialized table), the
  housing stock (T3.8), and **a possible population stock (R-1 option B)**. CONV-1's claimant rule
  — *the domain with the mechanical dependency, not the one that used it first in prose* —
  **points cleanly at the goods inventory**, which is a schema field where the other two are prose.
  *Proposed wording:* bare `stock` = goods inventory; `dwelling stock` for housing; population
  qualified explicitly if it ever becomes one.
- **`source` — two meanings, both in ratified documents:** D-037 C1's claim source, and
  `needs.json`'s `"source": "housingStock"`. **The claimant rule points cleanly at needs.json** —
  `source` there is a SERIALIZED JSON KEY read by the needs binding; D-037's is prose in a design
  document. *Proposed wording:* bare `source` = a need satisfier's binding; claims say
  `claim origin`.

**RULED: THE PROPOSED WORDING IS TAKEN.** Bare **`stock` = the goods inventory**; housing becomes
**`dwelling stock`**; **population is qualified explicitly**, now that R-1 makes a population stock
exist. Bare **`source` = a need satisfier's binding**; claims say **`claim origin`**.

R-1 unblocked it exactly as CONV-1 predicted: the third meaning of `stock` is real, so qualifying
it is a decision about a thing that exists rather than a bet on one that might. Both moved from
PROPOSED to **RULED** in `docs/conv-1-term-namespacing.md`, and the registry updated there.

### R-3 — THE BUCKET-CAP AND CLONE-SIZE ADR

**ONE director-ruled ADR under S8 §2, not two** — both documents are inside the M0 freeze.

**Measured, not projected:**

- `WorldFounding` instantiates the **full culture × religion × class × cohort cross-product**, rows
  existing at zero population — **DENSE**. Today: **384 bucket rows**, confirmed by `sim bench`
  against a code-only prediction of exactly 384.
- **Clone size today: 82,096 B/turn = 0.078 MiB.** Scaling measured by varying settlements:
  12 → 73,906 B, 24 → 147,036 B ⇒ **≈6,094 B per settlement**.

**Projected, and labelled as arithmetic:**

| scenario | bucket rows | clone/turn |
| --- | --- | --- |
| today (M3, N=12, 2 classes) | 384 | **0.078 MiB — MEASURED** |
| Charter late game: 800 settlements × D-018's 12 class slots × 16 cohorts, ONE culture/religion | **153,600 — already past the ratified ~150k cap** | ~16 MB |
| same at 4 cultures × 4 religions | ~2.46M | ~200 MB |

**The cap's own "automatic merge-below-threshold policy" presupposes sparse or merged storage and
DOES NOT EXIST.** And `m0-kernel-spec` §3.2's *"at M0–M9 scale this is a few MB"* is **wrong at the
far end**: what it actually covers is "while buckets stay small", and buckets are exactly what
D-018's class roster and plural cultures grow.

**R-3 IS THE LARGEST UNSCHEDULED ITEM IN THE PROJECT, AND IT BITES AT M8/M9** — the same
milestones where cultural plurality arrives and D-037's co-ethnic claim source goes live. Four
faces of one scaling decision: dense founding, a ratified cap, an unbuilt merge policy, and a
clone-size claim that fails at the same scale.

**RULED: RAISE THE CEILING, AND RETHINK THE COPY ARCHITECTURE — a FIFTH option this spec did not
offer, and the difference is the point.**

**Raising the cap permits more rows. It does nothing about every row being COPIED EVERY TURN**,
and that is the actual constraint at late-game scale. The ruling therefore adds: **change how
state is carried between turns so a big world is AFFORDABLE, not merely PERMITTED.** Scheduled as
**T4.16**.

**A FRAMING ERROR IN THIS SPEC'S OWN OPTION LIST, CORRECTED.** Options (i) and (iii) were
presented as if mutually exclusive. **They are not.** Rows currently exist at ZERO POPULATION —
the cross-product is instantiated whether or not anyone lives there — so **most of the projected
153,600 is empty slots**. **Sparse founding, a raised cap, and a cheaper clone are THREE
INDEPENDENT WINS and can all be taken.** Sparse founding is COMPLEMENTARY to raising the ceiling,
never an alternative to it.

---

## §8. ALSO SURFACED, NOT DECIDED — CANDIDATE ADR-015 SECTIONS

The director rules on writing these at spec time.

- **READY TO WRITE — "an operation that looks like it succeeded is not evidence that it did."**
  Five instances: T3.9b's silent `git checkout` no-op on an untracked file; T3.11's
  `git push --delete` returning 403 for all 21 branches while the loop printed `DELETED: 21`;
  CONV-1's over-wide `git checkout --`; T3.12's `grep -c` exiting nonzero on zero matches and
  silently skipping a whole test suite via `&&`; and **instance five, which is why the status was
  upgraded** — a mid-red-proof `git checkout --` that destroyed an uncommitted baseline, **caught
  and recovered by the very remedy the entry names, in the same session it was drafted.** The
  remedy: commit a verified-green baseline before a red proof.
- **The kernel clone-size / bucket-cap ADR** — this is R-3.
- **`stock` / `source` namespacing** — this is R-2.
- **The CI process defect** — a nightly failure must surface where someone reads it. Eleven
  consecutive silent reds; owner CI, M4-era.

---

## §9. FINDINGS — PROMPT-VS-TREE DISAGREEMENTS (§7.12)

GOV-2 already carries twelve findings against its own citations. These are new, found while
verifying this prompt's carried claims. **In every case the TREE WINS.**

**F1 — the prompt's GOV-2 section numbers are wrong in two places (director-acknowledged).**
The prompt says *"Everything M4 is holding, from GOV-2 §4"*. GOV-2 **§4** is
"THREE SCALING AND SCOPE CONFLICTS"; the holding list is **§6**, "WHAT M4 IS NOW HOLDING, IN ONE
LIST". The prompt also cites *"GOV-2 §5a"* for the Scale Charter conflict; there is no §5a —
**§5** is the clone-size measurement (which the prompt cites correctly for R-3) and the Charter
conflict is **§4a**. Substance unaffected in both cases; the content is exactly as described, at
different addresses. Corrected throughout this spec.

**F2 — "Sections 5–6's scaling conflicts and the clone-size measurement" mislocates the conflicts.**
The scaling and scope conflicts are **§4**; §5 is the clone-size measurement and §6 is the holding
list. Same class as F1.

**F3 — the bronze/tools cascade filing NEVER LANDED; the packet appears not to have been sent.**
The prompt asks the spec to carry *"the artisans-at-1 finding and its bronze/tools cascade"*. The
word "cascade" appears nowhere in `queue.md`, `milestones.md` or any review record, and no record
anywhere connects artisans to bronze or tools. **Asked to say so rather than file it here: there
is no such filing in the tree, so either the packet that would have created it was never sent, or
it was sent and never committed.** Not reconstructed from the prompt — reconstructing a filing
from a one-line reference is how unmeasured claims enter the record.

**What IS measured and adjacent** (seed 3, turn 650, from the same replay reports): world bronze
stock **0** with 34 produced in the final turn, tools stock 240, against copper-ore 14,650 and
tin-ore 109,197 accumulated. Ore piles up; bronze does not persist. **F4b supplies the other end
of that chain** — with artisans extinct in half the settlements, artisan_share is zero and the
casting gate is shut regardless of whether it ever opened. Recorded as an observation with its
measurement, NOT as the missing filing.

**F4 — WITHDRAWN AND REPLACED (director ruling). THE METHOD COULD NOT SEE THE QUANTITY.**

**Withdrawn for a METHOD reason, which is not the same as refuted**, and the record should not
blur them — the same distinction the T3.4b criterion withdrawal drew.

*What F4 originally said:* the artisans-at-1 claim is contradicted, because 0 of 12 settlements
held exactly 1 artisan in seeds 3/6/9 at any of eleven sampled turns.

*Why that was the wrong instrument:* the chronicle logs *"the FIRST artisans set up their
workshops at X, N masters and hands"* — an **EMERGENCE EVENT, one instant per settlement**, not a
persisting state. Sampling eleven turns out of 650 cannot see a momentary event. If artisans
emerge at 1 and reach hundreds by turn 160, every sampled turn shows hundreds and none shows 1 —
which is exactly the result obtained. The turns-1–40 arm does not rescue it either: zero there is
consistent with emergence happening LATER in those seeds, not with emergence never being 1. **The
reports were written every turn; the sampling error was at ANALYSIS time**, one layer below where
it was looked for.

**RE-MEASURED PROPERLY — EVERY TURN SCANNED, 650 lines per seed, first non-zero turn and the count
AT THAT TURN, which is the quantity the chronicle records:**

| seed | emerged | AT EXACTLY 1 | emergence turns |
| --- | --- | --- | --- |
| 3 | 12/12 | **5** | 43–98 |
| 6 | 12/12 | **9** | 4–65 |
| 9 | 11/12 (s3 never) | **5** | 69–137 |

**THE BIMODAL SPLIT IS REPRODUCED — and it is sharper than the director's chronicle showed.**
All 35 emergences across three seeds:

```
1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 3 | 26 26 33 33 55 56 56 57 57 57 58 58 58 59 60
LOW mode (<=3): n=20, values {1, 3}      HIGH mode: n=15, range 26-60
GAP: NOTHING between 3 and 26.           AT EXACTLY 1: 19 of 35 = 54%
```

The director's chronicle showed Nuhem at 27 and Naethaehun at 36 against ten settlements at
exactly 1. **That shape is confirmed on independent seeds**: a low mode pinned at 1, a high mode
in the mid-tens-to-sixty, and an empty gap between them. **The anomaly is the SPLIT, not the
number 1** — and a mechanism that emits either one person or fifty-odd, never five, is the thing
T4.14 has to explain.

**LIMIT, stated: this is NOT the director's session.** `orders-20260807-145349.bin` **is not in
the tree** — not in `docs/`, not in `runs/`, never committed, and absent from the container. The
measurement above replays the shipped `orders-20260724-164734-held-exit.bin` on seeds 3/6/9. It
corroborates the claim's SHAPE on independent worlds; it does not replay his session, and T4.14
should still do so when that log is available.

**F4b — A LARGER FINDING FOUND WHILE CHECKING A SMALLER ONE: THE ARTISAN CLASS GOES EXTINCT
ACROSS HALF THE WORLD.** Measured, every turn, all three seeds: artisans rise to the hundreds by
turn 160 (12 of 12 settlements non-zero), peak near turn 320, and then **COLLAPSE TO ZERO in 6 of
12 settlements by turn 650** after having held hundreds. Seed 3 final counts:
`[0, 1849, 0, 0, 0, 1805, 0, 2427, 2964, 1788, 0, 1376]`.

It corroborates T3.12a's own sample line — `(Artisans, 0, 0)` at turn 650 with Comfort 0.00 — and
**it feeds the bronze chain from the other end: no artisans means artisan_share zero means the
casting gate is shut regardless of whether it ever opened.**

**THE OPEN QUESTION, STATED AND NOT ANSWERED.** Three different defects produce this shape:
(i) the emergence latch's **RECEDE arm** firing; (ii) **class mobility** moving people out of the
artisan class; or (iii) the **settlements themselves declining**, taking every class down with
them. Distinguishing them needs per-turn class flows, which the reporter already emits.
**Owner: T4.14, alongside the emergence question.**

**F5 — "artisans-at-1" exists in the tree ONLY in the milestones entry.**
It has no measurement record anywhere else — no review record, no queue entry, no test. It entered
the tree through the M3 exit ruling text. Combined with F4, the entry should be re-stated as an
OBSERVATION AWAITING REPRODUCTION rather than a measurement. **Not amended here** — `milestones.md`
records a director ruling and this packet does not amend ratified records; flagged for the
director.

---

## §10. PROVENANCE — PER-CLAIM, CLAIM → SOURCE

| claim carried | source |
| --- | --- |
| Money ruled M5, in kind | GOV-2 §1a |
| Notables split by role; M4 = generals only | GOV-2 §1b |
| Notables retrofit field list (id/registry, home bucket, traits, competence, experience, lifecycle) | GOV-2 §1b table, sourced to ADR-001, D-010, D-011 §2, D-021 valve 5 |
| "Is a notable a person?" is unanswered, owner T4.1 | GOV-2 §1b closing paragraph |
| D-037 three-quantity model, day one, named check in T4.1 | GOV-2 §2a; D-037 A3 / D1 |
| B-2 is M4's first real work; ~1,240-yr reserves; zero starvation in 20 runs; unbounded in the other direction | GOV-2 §2b, citing `m4-blocking-material.md`, `t3.4c-review-record.md:50`, `t3.6-review-record.md:53-54` |
| B-2 staged base-then-enrichment, derived not chosen | GOV-2 §2b; `m4-blocking-material.md:57-104` |
| Q-B's five predictions | `queue.md` Q-B (extended at T3.8, T3.9b and T3.11 certifications) |
| Q1 bind ratio never measured; owner T4.1 | `queue.md:220-226` |
| Scale Charter ~50 → 300–800; measured saturation min 33 / median 45 / max 74; ADR-017 "SITING: STANDS UNAMENDED"; ~9–24× gap | **GOV-2 §4a** (not §5a — F1) |
| M4 holding list (trade, war, colonization, non-state peoples, B-2, claims, transport levers, T3.10, generals) | **GOV-2 §6** (not §4 — F1) |
| Transport levers Q-A/Q-C/Q-E are ONE design conversation | `queue.md` Q-E |
| Stride blocks three consumers; rule it once | GOV-2 §2d as amended by §4b |
| Clone size 0.078 MiB; 384 rows DENSE; 6,094 B/settlement; 153,600 rows at the cap; ~200 MB at 4×4 | `t3.11-review-record.md` Item 3; GOV-2 §5 |
| `m0-kernel-spec` §3.2 "a few MB at M0–M9" is wrong at the far end | `queue.md` ADR-REQUIRED entry, T3.11 measurement |
| Density corridor 20-seed range 1.1428–1.6501, mean 1.3952, dated to T3.2b | `t3.12-breach-record.md`; `corridors.json` quarantine block |
| Migration floor small-world mis-specification; corr +0.737 / +0.815 | `t3.12b-review-record.md`; `corridors.json` |
| MalthusLite restoration is BINDING for T3.10 | `queue.md:131` |
| D-039 is M6; only Part B's investment touches M4; D5 hard-blocked on B-2 | `queue.md:119`; `d039-command-fog-and-siege.md` |
| Visual milestone sits after M5, before M6; absorbs symbology + Part H | D-038 E1; D-038 Part H |
| Comparative-advantage criterion WITHDRAWN not failed | `milestones.md` M3 entry |
| Candidate ADR-015 sections, five instances, READY TO WRITE | `queue.md` |
| **Artisans-at-1** | `milestones.md` M3 entry ONLY, no measurement behind it (F5) — original F4 **WITHDRAWN for a method reason and REPLACED**; re-measured every-turn, the claim's SHAPE is **CORROBORATED** (54 % of emergences at exactly 1; bimodal split with an empty gap between 3 and 26) |
| **Artisan collapse to zero in 6 of 12 settlements** | **NEW, F4b** — measured every turn, seeds 3/6/9; owner T4.14 |
| **Bronze/tools cascade** | **NO TREE RECORD — F3** |
