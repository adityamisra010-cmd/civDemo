# GOV-3 — CONDITIONAL EXECUTION AND STANDING AUTONOMY

**Governance record. Docs only. HELD FOR MERGE** behind `t4.1b-spacing-derivation`,
`t4.1d-path-discrepancy` and `d040-discovery-and-control`.

**Citations verified against the tree.** A3's six cited instances were checked against the review
records rather than taken from the prompt: **two hold as stated, four are a different failure mode
than claimed.** The corrected evidence is in **§A3** and the disagreements in **PART G**. The rule
A3 states is unchanged — the evidence for it is stronger and more varied than the prompt's version,
not weaker.

---

## WHY THIS EXISTS, AND HOW MUCH IT IS EXPECTED TO RECOVER

The director reports leaving the machine and returning to find an agent stopped, waiting on a
response that was predictable and could have been pre-ruled. That is real throughput lost to a
ruling that already existed in substance.

**Be honest about the size of the problem, because the fix must not overreach.** Reviewing the last
stretch of packets, what actually consumed round trips was: the T4.9/T4.3 citation slip, the
class-id bug, D-040's F2 tree disagreement, the era-gate law-4 violation, and Q2's drift tooth
firing. **None of those were predictable branches. Every one was an unpredictable finding —
exactly the case that must still stop and return.**

**This record formalises what is already half-practised, and it is expected to recover a MINORITY
of round trips, not most of them.** A governance record that oversells itself invites someone to
stretch it. Nothing here is a licence to keep going when the ground has moved.

---

## PART A — CONDITIONAL EXECUTION

**A1.** A directed packet MAY pre-rule its own branches: *do A; if X, do B; else do C; if anything
unpredicted, STOP AND REPORT.* Where a packet does this, the agent **EXECUTES the matching branch
rather than stopping to ask**.

**A2. THIS IS NOT NEW AUTHORITY.** §7.13 already requires pre-committed readings before
measurement, and every one is already an if-X-then-B-else-C. What changes is that the agent may
**act on** the branch instead of halting at it. The thinking was already forced by writing the
pre-commitment; executing it adds no risk that writing it did not already contain.

**A3. BRANCH ON THE MEASUREMENT, NEVER ON THE INTERPRETATION.**

**Permitted** conditions are checkable without judgement: a hash changed or did not; a test is red
or green; a count is above or below a stated threshold; a file exists or does not; two paths agree
or disagree.

**Forbidden** conditions require reading: *"if it looks reasonable"*, *"if the result is
plausible"*, *"if the finding seems minor"*.

**The reason is measured. Here is the evidence, corrected against the records:**

| # | case | what actually went wrong | verdict |
| --- | --- | --- | --- |
| 1 | **T3.5b's density anchor** | population unchanged (±0.2 %); the −24 % was the DENOMINATOR. Numbers reproduced bit-exactly on independent reruns; the causal reading (geometry read as harvest) was wrong | **measurement correct, interpretation wrong** |
| 2 | **T4.1d's seed-3 over-generalisation** | *"The measurement above stands. The sentence drawn from it does not."* Seed 3 has no low mode, seed 42 does, neither had orders | **measurement correct, interpretation wrong** |
| 3 | **T3.6b's overturned premise** | a justifying premise quoted from T2.13 as LIVE, which Item 0 measured **dead**. No measurement was misread — a stale citation was falsified | **§7.12 shape: an unmeasured premise** |
| 4 | **T4.1's order-conditional claim** | *"The two measurement paths disagree for the same seed"* — the MEASUREMENT was in doubt, and the cause turned out to be the class-id bug | **the measurement itself was wrong** |
| 5 | **D-040's F2 artisan-emergence citation** | D-018 does not contain the food-surplus condition at all; the shape belongs to the shipped predicate's `_doc`. A source attributed wrongly | **citation error** |
| 6 | **The M4 spec's artisans-at-1 sampling** | eleven sampled turns of 650 cannot see an instant. *"The method could not see the quantity"* — re-measured every turn, the claim was CORROBORATED | **the instrument was wrong** |

**Two are the interpretation failure A3 names. The other four are four DIFFERENT ways a stated
condition can be false — a stale premise, a wrong measurement, a wrong citation, a wrong
instrument.** That does not weaken A3; it widens what a branch author must worry about. A branch
predicated on any of these six would have executed confidently down the wrong path, and only case
1 and case 2 would have been caught by re-reading the interpretation.

**§7.10 exists precisely because measurement and interpretation come apart**
(`docs/adr/adr-015-verification-hygiene.md:486`): *"A FINDING IS A MEASUREMENT PLUS AN
INTERPRETATION, AND THEY VERIFY SEPARATELY … In T3.5, three of four killed findings had exactly
correct numbers and wrong conclusions."* **A branch that requires interpretation is a branch that
can be wrong in a way the author never sees.**

**A4. CAP THE CHAIN AT THREE BRANCH POINTS**, then stop regardless. A wrong turn at step one
compounds silently through step five, and the director reads a report built on a premise that
failed early. **Three is the ceiling; a packet may set fewer.**

**A5. EVERY BRANCH STATES ITS ELSE EXPLICITLY.** *"If not X, stop and report"* is a complete
branch; leaving X's failure unaddressed is not. **An unaddressed branch is where an agent
improvises**, and improvisation is what the fence exists to prevent.

**A6. THE UNPREDICTED CASE ALWAYS STOPS.** If the measurement lands outside every stated branch,
the agent reports and halts. It does not pick the nearest branch, and it does not reason its way
to which branch *"really"* applies. **Landing outside the stated branches is itself the finding.**

---

## PART B — STANDING AUTONOMY

**B1.** Most stops observed are the agent **correctly halting to report something that required no
decision from the director**. These are ruled standing permissions and need no per-packet grant.

**B2. MERGE ON GREEN WHEN THE RULING WAS PRE-GIVEN.** Where the director has certified a packet and
stated the merge sequence, the agent merges, reports the SHA and both suite counts on the merged
tree, and continues to the next stated step. **It does not stop between merges to confirm.**

The measurement obligations are unchanged: a real run on the merged tree, or a fence inference
**with its premise checked rather than assumed** — the `d51327e` precedent, where the standing
phrasing had quietly stopped being true one packet after it was set.

**B3. FILE A FINDING WITHOUT ASKING.** An agent never needs permission to record a finding.
**Filing is not deciding**, and the disposition remains the director's. What the agent must not do
without a ruling is **FIX** it.

**B4. CORRECT A CITATION, ADDRESS OR REFERENCE ERROR IN ITS OWN PROMPT, AND RECORD IT.** D-040
produced six such corrections in one packet and every one was the director's error. Stopping to
confirm each would have cost six round trips for six facts the tree already settled. **The tree
wins; correct it, record it as a §7.12 instance, and continue.**

**B5. RE-RUN A MEASUREMENT WHOSE FIRST RUN WAS INVALID.** A build race, a killed run, a contended
box, a probe with a bug — **re-run it and report both**. This is not a new result requiring a
ruling; it is the same measurement, taken properly.

**B6. WHAT STILL ALWAYS STOPS. This list is not shortened by anything above:**

- any finding that is a **DEFECT** rather than an observation;
- any **red** in a pre-exit or pre-merge sweep;
- any measurement landing **outside every pre-committed branch** (A6);
- any **golden moving that the packet did not predict** would move;
- any **determinism** result;
- anything requiring **a constant to move, a band to shift, or a ratified document to be amended**;
- any case where following the packet's instruction would require **weakening a test or a guard**.

---

## PART C — WHAT THIS DOES NOT DO

**C1.** Does not weaken any fence. A packet's scope fence is unchanged by conditional execution;
**a branch that would take the agent outside the fence is not a branch, it is a stop.**
**C2.** Does not permit fixing without a ruling. B3 permits **filing**; it does not permit repair.
**C3.** Does not relax §7.13. Pre-committed readings are still written **BEFORE** measurement, with
the observable named and its composite status stated per §7.15.
**C4.** Does not make chained execution mandatory. A packet may still be written to stop at every
step, and **for adversarial-mandatory packets that is often correct.**

---

## PART D — CANDIDATE ADR-015 SECTION

Parts A and B are procedural rules of the same kind as §7.15–§7.17 and **belong in ADR-015 rather
than in a standalone record**, since `CLAUDE.md` points every agent at ADR-015 and not here.

**Filed as CANDIDATE**, with A3's corrected six-instance evidence attached. The director rules on
writing it at the next governance packet, alongside the two already queued:

1. **the git-operation pattern** — READY TO WRITE, five instances;
2. **the registry-id rule** — a probe indexing a registry by raw id prints the NAME, because a
   wrong index yields a **plausible series rather than an error**;
3. **this record's Parts A and B** — CANDIDATE.

**Three candidates now sit unqueued into sections, and a queue line does not bind an agent the way
a numbered §7.x does.** That is the same observation GOV-1 made before §7.15–§7.17 were written,
and it is made again here rather than assumed to have been heard.

---

## PART E — HANDBACK ECONOMY

**E1.** The director pays for a packet's findings **twice**: once in the review record committed to
the tree, once restated in prose in the chat handback. Handbacks have run to **800+ words** while
the record beside them says the same thing at greater length and with better provenance.

**E2. THE HANDBACK IS A POINTER, NOT A REPORT.** From here, a standard handback carries:

- the **branch name and tip SHA**;
- the **review record's path**;
- **suite counts, both projects**;
- **which pre-committed branches fired**, by name, one line each;
- **anything that STOPPED, in full** — a stop is the exception and it carries its reasoning;
- **anything requiring a director ruling, in full**.

Everything else lives in the record and the director reads it there.

**E3. WHAT MUST NOT BE COMPRESSED**, because compression is where a finding goes quiet:

- any **DEFECT**;
- any measurement landing **outside a pre-committed branch**;
- any **determinism** result;
- any **golden that moved unpredicted**;
- any **§7.12 instance**, whether against the packet or the director;
- any **disagreement between the prompt and the tree**.

**E4.** For an **adversarial-mandatory** packet the **lens reconciliation table stays in the
handback** as well as the record. It is the artifact §7.11 exists to make visible, and burying it
in a file is how a missing lens goes unnoticed.

---

## PART F — MEASUREMENT ECONOMY

**F1. FILTER DURING, FULL RUN AT HANDBACK.** `Sim.Tests` at 440+ tests has run **20+ minutes**, and
several packets ran it repeatedly during development. The convention: **filtered runs on the
affected family while working, ONE full run on the final tree before handback.** The full run is
**the measurement of record** and it is never skipped, inferred, or replaced by a filter.

Where a fence inference replaces a run — as at `d51327e` — **the premise is checked, not assumed**.
That precedent had quietly stopped being true one packet after it was set, and only re-checking
caught it.

**F2. VERIFY THE DIRECTOR'S ARITHMETIC WHERE IT IS LOAD-BEARING; TAKE HIS WORDING ON TRUST AND
CORRECT IT IF WRONG.** The tree always wins, and every citation correction filed so far has been
right to file. But:

- **A number the packet ACTS ON** — a threshold, a ratio, a measured value — **is verified before
  use.** D-040's F2 found a ratified document disagreeing with shipped code by checking; that is
  the case for verification.
- **A citation address, a section number, a name, a quoted phrase** is corrected if wrong and
  recorded (**B4**), but **does not need independent re-derivation** before the packet proceeds.

**The difference is whether being wrong changes what gets built.**

**F3. RE-MEASURING SOMETHING THE TREE ALREADY RECORDS IS A COST, NOT A VIRTUE**, unless the
packet's own conclusion rests on it. Where a prior packet's measurement is **cited and not acted
on**, cite it with its source and move on. Where it **IS acted on, verify** — the class-id bug is
what happens when a measurement is trusted without checking what it measured.

**F4. BUNDLE SMALL MEASUREMENTS IN ONE AREA; SPLIT INDEPENDENT RISK.** T4.1, T4.1b, T4.1c and T4.1d
were four packets with four manifests, four records, four handbacks and four certifications — and
**two of them were small**. The overhead was paid four times.

**The counterweight is real and is stated so this is not read as a licence to consolidate:** T3.10
was **split** precisely because it carried four packets of **independent risk** under one label,
and discovering that inside an adversarial-mandatory packet would have been expensive.

**The rule: SPLIT when the parts carry independent risk; BUNDLE when they are several small
measurements in one area under one fence.** Where a packet is bundled, **each part still carries
its own pre-committed readings and its own red proofs.**

---

## PART G — DISAGREEMENTS WITH THE PROMPT (§7.12: THE TREE WINS)

**G1. FOUR OF A3's SIX CITED INSTANCES ARE NOT THE FAILURE MODE CLAIMED.** Verified case by case
against the review records; the corrected table is in A3. Two hold exactly as stated (T3.5b's
density anchor, T4.1d's seed-3 over-generalisation). The other four fail differently: **T3.6b** was
a stale unmeasured premise (§7.12's shape, as its own record says); **T4.1's order-conditional
claim** failed on the MEASUREMENT — two paths disagreed, and the cause was the class-id bug;
**D-040's F2** is a mis-citation; **the M4 spec's artisans-at-1** is the inverse case entirely —
the measurement was the wrong instrument and the claim it attacked was later **corroborated**.

**THE OVER-GENERAL READING OCCURRED WHILE DRAFTING THE SECTION ABOUT OVER-GENERAL READINGS.**
That is not an aside; it is the whole argument for A3 in one line. The rule is not a lesson someone
learns and thereafter applies — the failure recurs at the moment of maximum attention to it, in the
sentence written to prevent it. A branch author is not exempt because they are the one writing the
branch. *(Director ruling, 2026-08-08: recorded at his instruction as the most useful sentence in
the record.)*

**This is itself an A3 instance, and it happened while writing A3.** The prompt's claim — *"at
least six cases where a measurement was correct and its interpretation was wrong"* — is a
correct-in-substance observation with an over-general reading attached to it. Six things did go
wrong; they did not go wrong the same way.

**G2. THE STALE INTERPRETATION FROM CASE 4 IS STILL UNAMENDED IN `queue.md`.** T4.1's
order-conditional framing survives at `docs/queue.md:834-845` after the claim was withdrawn.
**Filed, not fixed** — B3 permits the filing and forbids the repair. Owner: whoever takes T4.14.

**G3. §7.10 EXISTS AND IS NUMBERED.** `docs/adr/adr-015-verification-hygiene.md:486`. Worth stating
because `docs/handoff-status.md:244` records a prior session wrongly reporting it as never written.
A3 cites the live section.

---

## FENCE, AS EXECUTED

- **One new file, plus a queue entry. Nothing else.**
- **ADR-015 NOT amended.** Part D files a candidate; the director rules on writing it.
- **`CLAUDE.md` NOT amended.**
- **GOLDENS MUST NOT MOVE** — no code, no data, no test touched, so no golden can move. Reported
  explicitly as required, and confirmed by the suite.
