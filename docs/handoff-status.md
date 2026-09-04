# HANDOFF STATUS — for a director with no prior context

**Measured 2026-07-28** against working branch `t3.4c-variance-fix` at `719152a`. Every fact below
was read from git or from a test run at that commit, not recalled.

**What this project is.** A deterministic, turn-based simulation of a civilization over 6,000 years,
in C# on .NET 10. One human *director* rules on acceptance and merges; AI agents implement one
scoped *task packet* per session. Same seed must always produce the same world, forever — that
constraint drives most of the rules below. Milestones are M0, M1, M2 … ; packets inside milestone 3
are named T3.1, T3.2, and so on.

**Suite status at `719152a`: 355/355 passing (Sim.Tests) + 48/48 (Sim.Ui.Tests). All green.**

---

## 1. Branch state

`main` is accepted truth at **`ff67711`** ("Merge T3.4 — the price solver"). Agents never push to
`main`; the director merges a packet branch on acceptance. Convention: **one branch per packet.**

| branch | tip | commits ahead of `main` | status |
| --- | --- | --- | --- |
| `main` | `ff67711` | — | accepted truth |
| `t3.4c-variance-fix` | `719152a` | 28 | **IN PROGRESS** — the active packet |
| `t3.4b-harvest-variance` | `55f9870` | 16 | **UNCERTIFIED, UNMERGED** — cannot merge without T3.4c |
| `t3.5-needs-baskets` | `e41c748` | 17 | **ACCEPTED, not yet merged** |
| `art-substrate-renderer` | `d53e9b7` | 21 | **UNMERGED** — art assets + renderer, `Sim.Ui` only |
| `t3.3-production`, `t3.4-price-solver`, `cr-002-recalibration` | — | **0** | fully merged; safe to delete |

Everything else in the branch list is a spent packet branch already contained in `main`, or a
`worktree-wf_*` scratch branch created by tooling and carrying nothing.

**What is on the unmerged branches and not on `main`:**

- `t3.5-needs-baskets` — consumption baskets and needs satisfaction (decision D-035): households buy
  a basket of goods, and how well the basket is met feeds a grievance stock. Accepted by the
  director; awaiting merge. It also carries the only copy of ADR-015 §7.10 (see §7 below).
- `t3.4b-harvest-variance` — a per-settlement, per-year weather multiplier on crop yield, spatially
  and temporally correlated, so regional bad years and multi-year droughts are possible. Uncertified
  because its own review found the substrate does not deliver the variance it claims.
- `t3.4c-variance-fix` — descends from `t3.4b`. Fixes that defect, re-measures everything T3.4b
  claimed, and repairs two governance gaps. **The two branches merge together or not at all.**
- `art-substrate-renderer` — a parchment-style map renderer and its image assets. Touches only the
  UI project; its non-UI difference against `main` is provably empty, which is why M3 was allowed to
  proceed in parallel with it.

---

## 2. Milestone state

**Complete and tagged:** `m0-exit` (simulation kernel), `m1-exit` (a walking skeleton: world,
population, food, a UI), `m2-exit` (social classes, migration, grievance, chronicle).

**M4:** IN PROGRESS, and this line was three months stale — it said "not started" against a tree
carrying fourteen merged M4 packets. `docs/m4-spec.md` is written and its packet list is FINAL.
Merged: T4.1(+b–g), T4.2, T4.3, T4.4, T4.5, T4.6, T4.7, T4.8, T4.9, T4.10/T4.12, T4.14, T4.16 and
M4-A/B/C/D.

Remaining, and the distinction matters: **T4.11** (merchants) is genuinely NOT STARTED — no branch,
no document, no code, and it is blocked empirically on a trade volume that does not yet exist.
**T4.13** (comfort-as-stock) and **T4.15** (the exit artifact) are NOT absent but UNMERGED, each on
its own branch (`t4.13-comfort-as-stock` at `21e6aa5`, 25 files / ~1,732 insertions with a review
record; `t4.15-exit-inventory` at `01cb191`, docs only). Both were cut from a much older main and
neither has been re-measured against the current tree, so neither is merge-ready as it stands.

See `docs/m4-integration-audit.md` for the measured per-packet state and the M4 §6 exit-criteria
assessment. **M5 and later:** not started.

**M3 — in progress.** Packet order, including the three packets inserted mid-milestone by director
ruling (T3.2b, T3.4b/T3.4c, T3.5b):

| packet | what it is | state |
| --- | --- | --- |
| T3.1 | worldgen refresh | merged |
| T3.2 | goods & recipes | merged |
| **T3.2b** | spatial & agronomic recalibration (inserted, CR-002/CR-003) | merged |
| T3.3 | sector production; removal of M2 scaffolding | merged |
| T3.4 | the price solver | merged |
| **T3.4b** | harvest variance (inserted, CR-003 ruling) | uncertified |
| **T3.4c** | variance fix + re-measurement (inserted, T3.4b review rulings) | **ACTIVE** |
| T3.5 | consumption baskets + needs | accepted, unmerged |
| **T3.5b** | four follow-up rulings from T3.5's acceptance (inserted) | scoped, not started |
| T3.6 | trade & arbitrage | not started |
| T3.7 | merchants + mobility | not started |
| T3.8 | settlement size + housing | not started |
| T3.9 | UI: markets | not started |
| T3.10 | calibration extension | not started |
| T3.11 | harness + goldens | not started |
| T3.12 | M3 exit artifact | not started |

**Immediate order: finish T3.4c → T3.5b → T3.6.**

---

## 3. What T3.4c still owes

Done: the variance fix itself; a new test file for the weather subsystem where none existed; the
quarantine re-scope; the design-point re-measurement; the migration, correlation and corridor
re-measurements; the six quarantine verdicts filed as a document.

**Remaining: the six-lens adversarial review.** Six independent reviewing agents each attack the
packet from a named angle. Two have reported (`scope-discipline`, `re-measurement-honesty`); four are
outstanding (`variance-correctness`, `test-power`, `corridor-and-band`, `quarantine-and-guards`).
Findings so far point at errors in the packet's own *reporting*, not its code — a headline migration
figure that may not reproduce, a correlation table whose measurement conditions were never written
down, and a quarantine assertion that may be incapable of ever failing. Three verifiers are checking
those now.

**Rulings already made that govern the rest — do not re-open these:**

1. **The re-measurement reports; it does not repair.** Nothing may be re-tuned to close a gap
   discovered while re-measuring.
2. **The migration weights are not to be adjusted inside T3.4c**, even though the re-measurement
   showed the design point they were set against is missed by 2.3×–8.1×. The design point's own
   metric turned out to vary with the number of settlements, which makes it a defective target, not
   a licence to move the weights. A separate directed packet handles that.
3. **The corridor floor does not move.** A "corridor" here is an accepted band that a measured
   statistic must fall inside. If the measurement lands outside, that is reported, not fitted.
4. **The reading of both possible outcomes was fixed in advance** (see §5, pre-committed
   interpretation). One outcome — canonical world in band, development preset below floor — was
   pre-declared to mean the development preset is not a scale model of the real world. That is what
   was measured, and it is the ruling that applies.
5. **Store bounding is forbidden in this packet.** It is M4 material (§6).

---

## 4. Open rulings and blocked questions

**Ruled but not yet executed:**

- **Merge T3.5.** Accepted; the merge has not happened.
- **T3.4b + T3.4c merge as a pair**, once T3.4c's review completes and the director certifies.
- **T3.5b** is scoped and scheduled but unstarted: four fixes from T3.5's acceptance — a bad default
  that let every settlement saturate on farming, a variety-of-diet reference point to be replaced
  with a fixed nutritional standard, a grievance stock accruing to a class with no members, and two
  configuration checks to be adopted at load time.
- **A separate directed packet** for the migration-weight design point (§3 item 2).

**Awaiting a director decision:** nothing is blocked on the director right now. T3.4c is blocked only
on its own review completing.

**The art branch.** I could not find a record in the tree of "three art-branch UI defects" as a
distinct set, and I will not reconstruct them from memory — that is exactly the failure mode §5's
rules exist to stop. What the tree *does* record, as open UI-polish items, is three things:
terrain detail-on-zoom (resample the noise at view resolution so zoom never blurs), river-polyline
corner smoothing (render path only), and true river breadth from computed water flow instead of a
tuned rank falloff. **If the director means a different three, they must be re-stated; they are not
written down anywhere I can measure.** Separately, map *symbology* — settlement icons, trade-route
and border visual language, army and unrest markers, a legend — is deferred by ruling to after M4/M5,
on the reasoning that M3, M4 and M5 each change what the map contains.

---

## 5. Standing rules a new director must know

**The seven laws** (full text in `CLAUDE.md`) govern the simulation itself. The two that bite most
often: all people, money and goods move only through a ledger, and are compared with *exact* equality
in tests — no tolerance; and every rate is expressed per simulated year and integrated against the
timestep, never hardcoded per turn.

**ADR-015 is the verification constitution.** The load-bearing sections, one line each:

- **§6** — one worktree per verifying agent, never shared; and **no finding may be acted on before
  its verdict returns**.
- **§7.1** — every mutation experiment gets a stated time bound before it starts; a hang is itself a
  finding.
- **§7.2** — a test having teeth is not the same as it aiming at the right property.
- **§7.3** — a review is incomplete until *every* lens has reported.
- **§7.4** — every guard ships with a test proven to fail when the guard is removed.
- **§7.5** — never assert on a quantity already resting against its own limit.
- **§7.6** — the empirical case for requiring six lenses.
- **§7.7** — a corridor insensitive to its own control parameter is measuring something else.
- **§7.8** — a new random driver can silently destroy an existing experiment's control.
- **§7.9** — broken tooling misdirects; a review that could not run is not a review that found
  nothing.
- **§7.10** — *a finding is a measurement plus an interpretation, and they verify separately.* Before
  calling anything a defect, name the ratified rule that requires the property claimed broken. Lenses
  agreeing corroborates the *number* only. **(Currently filed only on `t3.5-needs-baskets`.)**
- **§7.11** — reconcile lenses **by name** against the written manifest, never by count; and commit
  your work before running anything that reverts files.
- **§7.12** — a property you assert about your own work is a measurement you owe, not a summary you
  may write.
- **§7.13** — pre-commit how you will read *both* outcomes before taking a measurement whose result
  could be argued about. Corollary: **a defect can mask the distance to a corridor as easily as it
  can create one.**

**The anti-fitting rules.** These exist because a simulation can always be made to produce a desired
number by choosing constants backwards.

- **CR-003 §5.1** — you may not pick a constant in order to reproduce a previously observed
  behaviour. The old behaviour was a product of a bug and has no claim to correctness.
- **Corridor independence** — a band a measurement must fall inside is derived from real-world
  reasoning, then measured against. Never widened to admit the result you got.
- **Pre-committed interpretation (§7.13)** — decide what each possible result will *mean* before you
  look.

**Process.** Six-lens adversarial review is the floor for a substantive packet, with the lens set
written down *before* implementation and reconciled by name afterward. One branch per packet, cut
from `main`. If implementation reveals a genuine conflict between two frozen decisions, work stops
and a change request is filed for director ruling — but "a better way exists" is not a conflict.

---

## 6. The findings that shape the next milestone

**The frontier result (CR-003).** The corrected world is *not* Malthusian — that is, population is
not pressed against the food supply. Earlier constants were denominated wrongly (a yield figure was
256× too coarse, and "arable land" was counted inconsistently between two consumers), and fixing them
revealed a continent with enormous unused capacity: twelve settlements claim roughly 2% of it. This
inverted the project's working picture. It is why harvest weather was inserted as a packet at all —
without some source of year-to-year risk, a frontier world has no scarcity driver except player
error, and famine cannot emerge.

**B-1 and B-1b — the development preset is not a scale model.** B-1 originally claimed settlement
spacing caps the continent at nine sites. **That claim was measured and is false**; true capacity is
roughly 33. The real finding underneath it is B-1b: the small "dev preset" world used for fast test
runs behaves qualitatively differently from the shipped world, and it is referenced at 46 call sites.
T3.4c supplied the decisive number — the migration corridor sits inside its band on the full
canonical world and below the floor across the dev preset. Any corridor calibrated on the preset is
therefore measuring the preset. M4 cannot rely on preset-derived calibration without answering this.

**B-2 — unbounded grain accumulation.** Nothing in the simulation limits how much food a settlement
can stockpile, so stores run to roughly 3,000 years of consumption. Famine is consequently impossible
regardless of how bad the weather gets: the T3.4c review measured zero starvation deaths across
twenty full-length runs, with reserves about 17× above the level at which starvation could begin.
This is why T3.4b's requirement that the chronicle report famines was *withdrawn* rather than failed —
the reporting code is proven correct, there was simply nothing to report. B-2a stages the fix
(spoilage and a granary capacity first; storage technology, moisture, vermin, seed corn later) and
B-2b sets its sequencing. **M4 introduces conflict and foreign trade, both of which assume scarcity
can bite. It cannot ship on a world where hunger is unreachable.**

---

## 7. Known false or corrected claims

The record deliberately keeps its own errors. Do not act on any of these as if still true.

**Refuted claims of fact:**

- **"Settlement spacing caps the continent at nine sites"** (B-1) — false; capacity is ~33.
- **"Thin reserves"** — a director-level premise, false by roughly 45×. Recorded in CR-003 §6 at the
  director's own instruction.
- **"Harvest variance restored the resurrection detector"** — false. Isolated by turning the weather
  off: the migration weight change did it. Weather only made it happen sooner.
- **"Artisan surplus stays above 2.0 all campaign"** — false. The measured minimum is 1.114, and the
  mechanism it describes comes within 1.2% of firing.
- **"Famine is reported elsewhere — grievance accrues 4.80"** — void. The commit two later deleted
  the rig that produced it. It was also an overstatement when written.
- **"Harvest variance opened the attractiveness gaps that restored the migration teeth"** — half
  wrong. Weather is not needed, and it *shrinks* the margin toward failure.
- **An anchor recorded as "variance moved it 70 → 81"** — neither number is a measurement. The true
  values are 54 and 76. The bound built on them still holds; its stated derivation does not.
- **T3.4b's headline claim that its own derived variance constant is what the system delivers** —
  false; realised variance was 1.10–1.41× the derived value. That defect is what T3.4c fixes.

**Corrected this session:** I reported that ADR-015 §7.10 had never been written, having measured its
absence from this branch. The measurement was right and the conclusion wrong — it was filed on
`t3.5-needs-baskets`, which this branch does not descend from. My duplicate has been reverted so the
two do not collide at merge. This is precisely the error §7.10 itself describes, committed against
§7.10. **STAMPED CURRENT (GOV-3 G3, director ruling): §7.10 IS LIVE AND
NUMBERED on `main` at `docs/adr/adr-015-verification-hygiene.md:486`** — a reader skimming the
paragraph above must not carry away the withdrawn claim.

**A prediction that was falsified, kept because the falsification is the point:** I predicted the
variance fix would move the migration corridor back toward its band. On the dev preset it moved
*away* — median −14.1% to −30.5%. Because both readings had been fixed in advance, the result could
be reported rather than argued with.
