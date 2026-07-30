# ADR-017 — D-025 under T3.6b: siting STANDS; the endowment split moves to its reference band

Status: proposed (director certification pending) · Packet: T3.6b · Date: 2026-07-29
Evidence: docs/t3.6b-review-record.md (Item 0, 5 seeds, canonical world), rig
Sim.Tests/Systems/FoundingVariationItem0Tests.cs @ b9d4b3b.

## What D-025 said

Two clauses. SITING: "iterative top-score siting with a minimum travel-time spacing
constraint, composite key (score desc, cell id asc)." ENDOWMENT: equal-split founding
endowment, recorded as PROVISIONAL by the founding-variation queue item (raised T2.9/T2.10,
confirmed T2.13, consequence recorded at T3.6). T3.1c already modulated both in magnitude
without amending the decision text: `siting.scoreJitter` 0.35 on the score, and
`founding.endowmentJitter` 0.25 on the endowment — a de-facto partial amendment this ADR
regularises.

## What it now says

**SITING: STANDS UNAMENDED.** The Item 0 measurement shows the siting rule needs no change
for this packet's target:
- The lockstep complaint that justified touching founding at all NO LONGER REPRODUCES at
  HEAD (emergence spreads 58–85 decades across all five seeds; modal decade ≤ 2 of 12 —
  against the T2.13 baseline of 11-of-12 in one decade). T3.1c's jitters discharged it.
- Site-CELL compression is real (site fertility CV 0.00–0.04 vs land 0.38–0.54) but the
  reference class for site quality (RC-2, cross-sectional yields among SETTLED sites) is
  measured on realised output — and realised output spread is ALREADY in RC-2's band (grain
  production CV 0.27–0.41 vs justified 0.2–0.3). Amending siting to spread raw site cells
  would fit a quantity the reference class never measured (§7.10).
- The water-hugging that makes renewable deposit channels identical is the reference class's
  OWN behaviour (Neolithic settlement hugged water); "fixing" it would trade historical
  correctness for lumpy worlds — the fitted-world shape the packet forbids.

**ENDOWMENT: amplitude moves INTO its reference band — 0.25 → 0.69** (`founding.endowmentJitter`,
TUNE, data-only). Derivation: RC-1 (committed d0fc61a, before the measurement was seen)
justifies founding-population CV 0.4–0.6; the shipped 0.25 realises CV ≈ 0.14 (measured
0.115–0.161 across seeds — arithmetic amplitude/√3 confirmed). The band FLOOR is the
smallest intervention the measurement justifies: CV 0.4 ⇒ amplitude 0.4·√3 = 0.6928 ≈ 0.69.
The T3.1c structure is preserved unchanged: food scales with the REALISED population (only
the amp/3 per-capita wobble is independent), so founding food-per-capita stays stable and
the measured colony-crash failure mode that structure exists to prevent is not re-opened.
The loader's [0,1) guard still binds (0.69 < 1; a settlement cannot found empty).

Explicitly NOT amended, and why: this change was justified ONLY by the divergence target
and RC-1. P2 (pre-committed) predicts trade is UNCHANGED by it — endowment scale does not
touch bundle composition, and Item 0 (c) shows exchange is blocked by common-band-edge
pinning and by ore thresholds exceeding the entire price band; those findings are escalated
via the queue, never tuned at.

## What breaks (measured list to follow in the review record before repairs)

Founding endowments change on every founded world ⇒ every FOUNDED golden moves: FoundedGolden
(+ ci.yml FOUNDED_GOLDEN) and the FirstReign trajectory (its N = 1 world draws slot-0 jitter).
The toy golden does NOT move (no founded world, and schema is untouched). Candidate rig
fallout to be enumerated by running the full suite before any repair: calibration battery /
Malthus-corridor measurements (population totals now vary more per world), migration small-N
pins, any hand-computed founded rig, the T3.6 reading tests. Worldgen twin/determinism tests
must pass UNCHANGED (the jitter remains a pure hash of (seed, settlement, slot)).

## Schedule price

One data value + goldens re-pinned once + rig fallout repairs + a test-power pin (a
variance-floor test proven red at jitter 0, so a silent regression to lockstep cannot ship
green — closing the gap that let the discharged queue item go stale un-remeasured). Estimated
inside the packet; no downstream packet's scope changes. The structural transport findings
create M4 material but no new packet obligations here.
