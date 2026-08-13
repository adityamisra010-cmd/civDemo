# D-040 — DISCOVERY, REACH, AND THE DECAY OF CONTROL

**Director design ruling.** Extends D-037 and D-039. Constrains T4.3 (the claim model) and the
transport packet. **Designs nothing.** No mechanism, no storage, no constant.

**Citations in this record were verified against the tree at `583ae1e`.** Six disagreements were
found between the dictated text and ratified documents; per §7.12 the tree wins and each is
recorded in **PART F**. The body below is written with the corrections already applied — read Part
F to see what was changed and why.

---

## PART A — WHY THIS EXISTS

**A1.** T4.1b's spacing derivation raises settlement CAPACITY on the canonical continent from a
measured median of **46** to a median of **457** (95.2 km, the spacing at which a settlement's hex
allotment equals the catchment it actually works). Founding still places twelve. The Charter's
300–800 therefore becomes something **colonization grows into across 6,000 years** rather than
something that exists at turn 1.

**A2.** That leaves a question the spacing ruling does not answer: **what stops a polity settling
all 457 slots as fast as it can walk?** Capacity is now geometric. Nothing else limits expansion.

**A3.** **DIRECTOR RULING: two things limit it, and both are mechanisms rather than caps.**
**YOU CANNOT SETTLE WHAT YOU DO NOT KNOW EXISTS** (Part B), and **WHAT YOU HOLD FAR AWAY, YOU HOLD
WEAKLY** (Part C). Together they turn expansion from filling a known grid into a story with a
frontier.

**A4.** The director's framing, recorded: *an Indus Valley civilization knows the land along its
river and has no concept of America.* That is **not a fog-of-war overlay on a known map**; it is a
world whose **extent is itself discovered**.

---

## PART B — THE MAP IS DISCOVERED, NOT REVEALED

**B1.** A polity's knowledge of the world is a **computed extent**, not a visibility flag over
pre-known terrain. Land that has never been reached is not dark — it is **absent from what the
polity can act on**. Settlement, claim and trade all operate only within known extent.

**B2. DISCOVERY IS THE SAME MECHANISM AS D-039's RECONNAISSANCE, AT A DIFFERENT SCALE.**
D-039 Part B (`docs/d039-command-fog-and-siege.md:42`) rules reconnaissance an investable
capability: **B1** — information quality *"is bought with whatever is scarce in that era, never
with money"*, the ancient cost being **food and people**; **B2** — diminishing returns; **B3** —
*"reconnaissance is pointed, not global"*; **B4** — *"distance degrades information independently
of scout count"*, with a vantage point extending effective range; **B5** — scouts are reliable.

**Those clauses govern discovery too.** Strategic discovery is that same capability walked further
and returned with a map. **Do not invent a second exploration system.**

**The one difference, and it is the whole difference:** reconnaissance reports **a lagging position
of a moving thing**; discovery reports **a permanent fact about the land**. Once known, land stays
known. D-039 B2's diminishing returns and B4's distance degradation therefore apply to *reaching*
the land, not to *retaining* the knowledge.

*(D-039 Part B carries a sixth clause, B6, which is an open question rather than a ruling; it is
not imported here. See F1.)*

**B3. NO TECHNOLOGY UNLOCK. LAW 4 BINDS.** The director raised Civ's *"research sailing to cross to
another continent"* as the reference and it is **REJECTED in that form**: a tech-tree node opening
sea travel is a calendar gate wearing a tree. Law 4: *"capability derives from computed state,
never from dates or era labels"* (`CLAUDE.md:16`). The tree already refuses this shape in three
places — *"Classes emerge, they are not unlocked"*, *"LAW 4 FORBIDS CALENDAR GATES — a tier cannot
unlock by era or date"*, *"No era gate, no date, no unlock."*

**The computed version: sea travel becomes possible when the conditions for boats exist** — a
coastal settlement, timber, and craft capacity — **in the same shape as class emergence**, where
artisans emerge on **food surplus AND market extent** rather than on a date. A landlocked polity
never develops it; a coastal one does; **nobody schedules either.**

*(That emergence-shape citation belongs to the shipped predicate and its `_doc`, not to D-018,
and "market extent" is currently implemented as raw population. See F2 — it is a finding against
the reference, not against this ruling.)*

**B4. BOATS ARE THE SAME OBJECT AS WATER ROUTES.** Q-A (rivers as network edges), Q-C (draught
animals), Q-E (route improvement through use) and sea travel are **ONE design conversation**.
`queue.md` records that Q-A, Q-C and Q-E *"are ONE design conversation and should be ruled
together"* and that they remain **OPEN, owner: the transport packet** — so this record **adds sea
travel to that conversation**; it does not report a scoping decision already taken (see F3).
**Sea travel extends the network's edge types; it is not a separate movement mode.**

**B5. NOT DESIGNED HERE:** how extent is stored, whether it is per-polity or per-settlement, what
granularity it uses, or how it interacts with the traversal lattice.

The collision the transport packet already owns is noted and left there: the traversal lattice
samples terrain at **stride 4**, so **one node is 16 × 16 km** (`kmPerPx = 4.0` ×
`TraversalLattice.Build(…, stride = 4)`), and Q-A found that *"a river is a sub-node feature"*.
Discovery granularity will meet the same wall. **Stop here.**

---

## PART C — CONTROL DECAYS WITH DISTANCE, AND IT IS NOT A NEW QUANTITY

**C1. THIS IS THE PART THAT CONSTRAINS T4.3, AND IT MUST BE READ BEFORE THAT PACKET IS BUILT.**

**C2.** D-037 already separates **claim, control and recognition** as three quantities:
*"CLAIM, CONTROL AND RECOGNITION ARE THREE SEPARATE QUANTITIES. This is LOAD-BEARING and must be in
the M4 data model from day one"* (`docs/d037-emergent-polities.md:22`, A3), and D1 requires the
data model to *"support overlap, claim-without-control, and asymmetric recognition"*, being *"the
single instruction whose omission would make Part C unbuildable later"* (`:128-130`).

**A distant settlement is claimed but weakly controlled. That IS the loyalty mechanic, and D-037
already has the shape for it.**

**C3. DIRECTOR RULING: DO NOT ADD A `loyalty` FIELD.** A double decaying with distance is exactly
the free-floating modifier law 2 forbids — *"free-floating permanent buffs are banned"*
(`CLAUDE.md:14`) — a number attached to a place rather than a consequence of anything.

**Instead: CONTROL CARRIES A DISTANCE TERM**, and grievance, unrest and the political systems
**read control**. The mechanism is **administrative reach**; **loyalty is what you observe when
reach runs out.**

**C4.** The reference class, recorded because it is what makes this historically exact: **empires
lose their periphery first.**

> **D-041 (2026-08-12) ADDS THE POPULATION-SIDE QUANTITY THIS PART LEAVES OPEN.**
> `docs/d041-attachment.md`: control is a FUNCTION computed each turn; **attachment is an
> ACCUMULATED STOCK held by a POPULATION**, slow to build and slow to lose, and it travels with
> migrants. Distance-decayed control explains why remote provinces are weakly governed; it does not
> explain why some never leave. **D-041 does not amend this record** — attachment's inputs are time
> under control and what the polity does, and distance enters only through control, never directly. Roman provinces, colonial holdings, every over-extended polity fails
at its edges before its centre. A control model that decays with administrative distance produces
that **without anyone scripting it**.

**C5. THE SECOND PRESSURE THE DIRECTOR NAMED FALLS OUT OF THE SAME MODEL.** A settlement close to a
foreign polity's settlements sits where **two claims overlap** — which D-037 requires the data
model to express (A3: *"MULTIPLE polities may hold a claim on the SAME settlement
simultaneously"*, `:25-27`, and D1). **Contested control IS a loyalty crisis. No new mechanism.**

**C6. DISTANCE MEANS TRAVEL COST, NOT EUCLIDEAN DISTANCE.** It runs over the network graph —
D-009's *"One object, three jobs (movement, trade, military logistics)"*
(`docs/d009-d010-map-population-addendum.md:17`). So route improvement (Q-E) and water routes (Q-A)
**directly extend administrative reach**, which is why roads and rivers held empires together.
Stated explicitly: **the transport packet and the control model are COUPLED**, and improving a
route strengthens the hold on what is at the end of it.

**C7. BINDING ON T4.3, AND IT MUST APPEAR IN THAT PACKET'S FENCE.** Control must be able to express
a value that **varies with distance** and is **contested where claims overlap**.

T4.1's audit already named two foreclosing implementations — *"any design that stores control as an
owner id on the place row silently forecloses overlap, and any design that stores recognition as a
flag on the polity forecloses asymmetry"* (`docs/t4.1-review-record.md:166-169`, promoted to
PROHIBITED 1/2 at `docs/m4-spec.md:220-224`).

**D-040 C3 adds a THIRD requirement: an owner-id cannot carry a decay term either.** All three
must be **relations**, with recognition keyed by **(recogniser, recognised)**.

---

## PART D — WHERE EACH PIECE LANDS

**D1. M4** — the claim model's **data shape** must support C3 and C5. That is **T4.3**
(`docs/m4-spec.md:212`), and it is **the only M4 obligation this record creates**.

**D2. The transport packet** — sea travel as an **edge type** (B3, B4), alongside water routes
(Q-A), draught animals (Q-C) and route improvement (Q-E). One conversation; still OPEN.

**D3. M7 (knowledge)** — map extent and discovery mechanics. Under the D-011 §6 resequence
(battle layer inserted as M6) knowledge is **M7** and politics **M8**
(`docs/d011-battle-layer-addendum.md:47-57`). **GOV-2 §1c** records the Spine's system inventory as
**stale by one milestone from M6 onward** (`docs/m4-pre-spec-dependencies.md:143`) — so a reader
checking the Spine will find the old numbering, and this record uses the resequenced one.

**D4. M8 (politics)** — the political consequences of weak control: secession, autonomy, the
periphery going its own way. **D-021's release valves** already carry the machinery
(`docs/d021-stability-doctrine.md:17`, *"THE RELEASE VALVES"*; see F4).

**D5. NOT SCHEDULED HERE.** This record fixes the shape and the constraints. Each milestone's spec
proposes its own packet.

---

## PART E — WHAT THIS DOES NOT DO

**E1.** Does not amend **D-037**. C2 upholds it and depends on it.
**E2.** Does not amend **D-039**. B2 extends it to a second scale and names the one difference.
**E3.** Does not amend the **spacing derivation**. A1 takes it as given.
**E4.** Does not design **storage, granularity, decay curves or any constant**. Every number is
derived by the packet that builds the mechanism, from a stated reference class.
**E5.** Does not authorise work before the milestone that owns it.

---

## PART F — CITATION FINDINGS (§7.12: THE TREE WINS)

Six disagreements between the dictated text and the ratified tree. None changes a ruling; all
change what may be cited in support of one.

**F1. D-039 Part B has SIX clauses, not five.** B1–B5 are rulings; **B6 is an open question**
(`docs/d039-command-fog-and-siege.md:71`). "D-039 B1–B5 govern both" is correct as written, and
B2 now says so explicitly rather than by implication — an open question cannot govern anything.

**F2. THE ARTISAN-EMERGENCE REFERENCE IS NOT D-018.** D-018 lists the artisan trigger as
*"craft specialization share > threshold"* (`docs/d018-classes-and-needs.md:20`) and contains **no
food-surplus and no market-extent condition anywhere**. The "food surplus AND market extent"
shape belongs to the **shipped predicate and its `_doc`** —
`"emerge": "food_surplus_ratio > 1.3 && population > 520"` (`Sim.Data/content/sim.json:158`),
documented as *"a market exists to buy from him … Smith's extent-of-the-market limit on the
division of labour"*. **Two consequences:** (a) B3's analogy is cited to the code, not to D-018;
(b) **D-018 and the shipped predicate disagree about what makes an artisan** — recorded here,
owner unassigned, and it belongs beside T4.14's emergence work.
Second-order: **"market extent" is implemented as RAW POPULATION**, which is a proxy, not an
extent — the very thing B1 says a computed model should not be. Noted, not ruled.

**F3. Q-A/Q-C/Q-E ARE *"SHOULD BE RULED TOGETHER"*, NOT ALREADY RULED.** `docs/queue.md:540-542`:
*"OPEN, owner: the transport packet — Q-A (water), Q-C (draught animals) and Q-E are ONE design
conversation and should be ruled together."* B4 and D2 are worded to **add sea travel to an open
conversation**, not to report a scoping decision that has been taken. The distinction matters: a
future reader must not treat the transport scope as settled.

**F4. D-021 SAYS "RELEASE VALVES", NOT "UNREST VALVES".** `docs/d021-stability-doctrine.md:17`.
D4 uses the tree's wording.

**F5. D-009's WORDING IS "One object, three jobs".** `docs/d009-d010-map-population-addendum.md:17`
— a comma, not "with". C6 quotes it exactly.

**F6. THE D-037 OVERLAP REQUIREMENT IS D1 AND A3, NOT C1.** D-037's **C1 is
*"CLAIM SOURCES (mechanisms, never assignments)"*** (`:70`) — a different subject entirely. The
requirement that the data model express simultaneous claims lives at **A3** (`:25-27`) and **D1**
(`:128-130`). C5 and C2 cite A3/D1. **A reader sent to D-037 C1 would have found the wrong
clause**, which is exactly the failure mode §7.12 exists to catch.

**AND ONE TENSION, REPORTED NOT RULED.** `docs/d009-d010-map-population-addendum.md:12` calls
bridges and tunnels *"expensive, **era-gated**, terrain-crossing edges"*. That is the same shape
B3 rejects for sea travel, sitting in a ratified document, on the very subject B4 folds sea travel
into. **It is not amended here** — D-040 amends nothing — but the transport packet will have to
face it, and it should not discover it late.

---

**HOLD FOR MERGE.** Docs only. No mechanism designed, no code written.
