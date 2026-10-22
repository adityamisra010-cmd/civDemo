# D-052 — M5 integration-audit rulings (Director, 2026-10-12)

**Status: RATIFIED.** The Director's decisions of 2026-10-12, given in two parts: (1) with the request for a complete
read-only integration audit of `m5w-integration` @ `f0427d2`, and (2) as consolidated rulings after reviewing that
audit's report (`civDemo-M5-integration-audit-f0427d2.md`, in the session scratchpad, not in the repository). Until this
record they existed only in the Director's messages, while `docs/current-state.md` listed most of them as open. Written
on `m5w-integration`; committed and pushed to the new branch `claude/m5-playtest-d052` for the playtest build, with the
Director's authorization (2026-10-12). `claude/new-session-yavghm` is untouched at `2dc6af2`. NOT merged to `main`.

**What this record is not.** It is not an authorization to publish, merge, tag, package or close M5. It does not change
the milestone order: `docs/milestones.md` stays the authoritative ladder. It supersedes the ratified or proposed items
named under each ruling. The historical text of those records is not rewritten; dated pointers are added to them
(GOV-4 §6). Figures marked MEASURED were measured on `f0427d2` by the audit, or by the agent writing the cited record.

## 1. Milestone ladder — re-affirmed, unchanged

M5 Governing Gameplay (current, NOT complete) · M6 Knowledge / Research / Technology · M7 Battle Layer · M8 Politics /
Diplomacy · M9 Society · M10 Integrated Civilization Simulation (whole-model validation across Ages A1–A9) · M11+ Depth &
Content Expansion. This restates `docs/milestones.md` §"Roadmap rebase 2026-10-03"; it is not a new ordering. `main`'s
`CLAUDE.md` still names M4 only because the M5 branches are unmerged; it changes when they merge, not here.

## 2. ADR-036 (the Found Settlement order) — RATIFIED as drafted

- The legality terms (§2–§3) and the §7 defaults (a)–(f), as implemented.
- **§8 (g):** the AI expansion rule as shipped: `sim.json` `colonization.aiFounding` enabled, `minSourcePopulation` 500,
  `minYearsBetweenFoundings` 350 years. Neither legality nor the AI's pace changes.
- **§8 (h):** the AI's known information-access leak is accepted until M6's known extent (§5). Its MEASURED consequence
  is accepted as evidence, not as a target: AI colonies land a median 953 km from their source (217–2,392 km; 55 orders
  on seeds 42, 1, 2, 3 and 7; 600 turns; one AI Empire; reproduced on `f0427d2`). No travel budget is added in M5.
- **§8 (i):** the AI meeting `a4_expansion` (≥ 14 settlements) at turn 358 on every measured seed is the accepted reading
  of that milestone.
- **The S1c re-pins are accepted:** `AiEmpireIntegrationTests`' moved pins, the `AiFoundingAttributionTests` constants
  (`a416ce4b…` with the switch off, `aa7a87a9…` on) and the ci.yml AI leg's kind range 3..9.
- This ratification is the Director sign-off the kernel freeze requires for ADR-036's changes to `OrderLog.cs`
  (`OrderKind.FoundSettlement = 9`) and `OrderValidation.cs`.
- Found Settlement (player and AI) keeps using the one authoritative predicate, `FoundingQuery.Check`.

## 3. ADR-037 (the player-knowledge seam) — RATIFIED as drafted

One read seam: `PolityKnowledge` in Sim.Core and `PlayerKnowledge` in Sim.Ui. Production is all-known
(`PolityKnowledge.Of`) until a stored extent exists, and `PolityKnowledge.Restricted` is a test seam only. Player-visible
information is gated by explicit knowledge queries through that seam, separate from omniscient state. The open channels
ADR-037 records stay recorded: road-panel endpoints, the developer surfaces (F12 / `--dev`), the place card's low-order
bits, and the AI's valuation of known cells. They are M6's (§5).

## 4. Movement and Action Points — deferred to M7

Unit move orders, Action Points / Action Capacity and right-click move are M7 Battle Layer work. CR-017 §3 (its Action
Capacity half) and §5 (off-road movement) are scheduled for M7 and must be ruled before any M7 movement packet. Their
substance is not ruled here, and CR-017 stays OPEN. The m5w plan's S3a–S3c packets (`OrderKind` 10, Action Points,
schema v33/v34) are M7's, not M5's.

## 5. Fog of war, scouts and visibility — deferred to M6; no M5 save-format change

The following are M6 work:
- full fog;
- the three-state LIVE / GHOST / UNKNOWN display (D-039 C1);
- scouts (D-039 B6 already gives them to M6's spec);
- persisted sighting memory;
- discovered-extent storage and visibility persistence (the m5w plan's S2a–S2c);
- the known-extent restriction of settle, claim and trade (D-040 B1);
- closing the information channels named in §3, including the placement mode's legality bit (`docs/queue.md`, the S1d
  line).

No visibility state and no save-format change ship in M5: `CanonicalSchema.Version` stays 32. This supersedes ADR-037
§1's placement of GHOST sighting memory in M7, and the matching type comment in `Sim.Core/State/PolityKnowledge.cs`.

## 6. Visual tier — M5 keeps the composed tier; the visual milestone is placed at M6

M5 ships the current composed visual tier. The following are deferred to the visual milestone (D-038 E1), which the
Director places at **M6**:
- individually drawn buildings and institutions, including count staging (1/10/20/30; D-043 D2/H8);
- detailed Age-specific terrain and architecture;
- richer unit art (authored silhouettes).

This gives D-038 E1 ("an inserted milestone after M5 and before M6") its slot: the visual work is scheduled with M6. The
order and names in `docs/milestones.md` are unchanged. The m5w plan's P5, P12b and P14 go with it. D-038 C1 (no
figures) stands.

## 7. Unit display — the closed decision and its agreed implementation reading

**Decision (closed 2026-10-12):** one representative character per unit type, with that type's count above it.
Selecting it opens a combined army detail panel showing the composition, with only real, simulation-backed statistics.

**Implementation reading (agreed):**
- "Count" = the number of formations of that type in the group. That is the only count the state holds:
  `MilitaryUnitRow` has no headcount. Under `unit-families.json` `formationsPerPolity` 1 it reads 1 in M5.
- The combined panel aggregates the co-located formations (same anchor) of one owner.
- Headcount arrives with M7 recruitment. Until then the panel says so, as the current card does.
- The existing non-figurative emblem plate is the "representative character" until the visual milestone (§6). D-038 C1
  stands.

This governs over the per-formation, no-count default that P11 shipped. Ledger §24 ("one reported agent → one visual
token"; "internal composition may appear as labels/counts") and D-043 B5/F12 (formations observed unequally must not be
merged) are read through it: grouping applies to formations the viewer observes alike, which is every formation while
production is all-known.

**Status: NOT IMPLEMENTED.** The rework is outstanding. It was not in the 2026-10-12 implementation pass.

## 8. Resource-discovery shortcut — closed, as built (P10)

**Decision (closed 2026-10-12):** temporary, for a few turns; it opens the corresponding overlay; it highlights known
deposits only; manual access remains; it never reveals unknown tiles. P10 implements this with one limitation, which is
disclosed for the playtest. Until D-051 §2 there is no per-place deposit presence, so the outline marks every known
place whose channel value is above zero. On the canonical world that is the known landmass, not deposit locations. Its
resolution follows D-051 §2 (§10).

## 9. Weather and calibration — CR-020 §B ruled

- D-051 §1(b) time-averaged decade weather stays ON (`sim.json` `harvestVariance.timeAveragedHarvest` = true).
- **Every frozen calibration reference is preserved:**
  - the `corridors.json` bands;
  - the in-test dev population envelope [71,000, 119,000];
  - the per-seed recorded dev migration values and their 0.75× drift tooth (`CalibrationBatteryTests`).

  No band is widened, no test is waived, skipped or quarantined, nothing is re-pinned, and nothing is reverted.
- **The two failing tests stay red and are disclosed for the playtest:**
  - `Dev_MalthusCorridors_AllInBand` seed 42: dev migration 8.71773E-05, below 0.75 × 0.000248624.
  - The same test, seed 7: dev population 124,455, above 119,000. Its migration, 1.0083E-04 (×0.3497 of its recorded
    2.8835E-04), is also below its floor. The population assert runs first and hides it (MEASURED by the audit; added to
    CR-020 §B).
- Calibration is reassessed from measured evidence after the playtest. This is CR-020 §B option 3, recorded as the
  ruling. CR-020's recommendation (option 1, re-record) is not adopted now.
- The reds still stop any merge to `main` and the M5 exit sweep (GOV-3 B6: "any red in a pre-exit or pre-merge sweep").
  This ruling does not waive that.

## 10. D-051 §1(a), §2 and §3 — deferred until after the playtest

The rulings stay RATIFIED. Their implementation is deferred until after the Director's M5 playtest:
- §1(a): flight reads a smoothed, multi-turn deficit;
- §2: availability classes, a common-material floor and regional richness;
- §3: the expected-value next-turn forecast.

Consequences disclosed for the playtest:
- Settlements on stone-poor ground. D-051 §2 measured 38 % of settlements with effectively zero stone, and the first
  granary at turn 113.
- The §8 shortcut limitation.
- No next-turn forecast.

## 11. Primitive toolmaking and the central stockpile — deferred

- **The Workshop guard stays:** `ConstructionQuery` → `InputUnobtainable`, `355608f`.
- **Approved direction, NOT implemented:** a primitive stone-and-wood recipe that produces the existing tools good, at
  lower productivity than bronze, with no stone exhaustion, no granary blockage, and full Bronze Age productivity
  preserved.
  - Its mechanism (CR-020 §A option 2, reservation; option 3, demand-limited; or another) and its milestone are deferred
    until after the playtest.
  - CR-020 §A is ruled by this item: guard, direction and deferral.
- **D-051 §6, the central stockpile:** deferred until after the playtest. No partial pooling and no relief-pooling
  substitute.
  > *2026-10-14 pointer (text above unchanged):* superseded for construction/roads and shortage food only by the
  > Director's 2026-10-13 instruction; see `docs/d053-batch2-playtest-rulings.md` §5 (uncommitted, for review).

## 12. The Spine's M5 items — deferred

The M5 foundation items "budget", "authority / bandwidth economy" and "laws-lite" (`docs/milestones.md` §Dependencies;
Spine outline) are not built and are deferred out of the M5 playtest build. The ruling names no destination milestone,
and does not say whether the M5 exit waits for them. Both stay open for the M5 exit criteria (§14).

## 13. Playtest configuration

- **AI launch configuration:** the packaged playtest launcher starts the game with one AI Empire (`--ai-empires 1`).
  `worldgen.json`'s default (0) and every golden are unchanged. A session's manifest and log name record the override.
- **`--size`:** a non-canonical map size must found settlements correctly for the player and the AI: deposits are read
  from the founded map's own cells, with no exception. This was fixed in the 2026-10-12 implementation pass
  (`WorldFounding.HinterlandMeans` indexes the terrain by its own size), with regression tests.
- **Old saves:** a session that does not replay identically on this build is not resumable. One recorded by the
  `2dc6af2` playtest build is such a session. The build refuses it before any window opens, with a clear, actionable
  incompatibility error: never an unhandled exception, and never a silent replay without the trace check. The
  schema-mismatch path (S0, D-008 replay-with-notice) is unchanged.

## 14. Not ruled by this record (still open)

- ADR-035 P-F1 and P-F0. They ship in the build; only P-F2 and the ADR-017 §7 amendment were ruled, on 2026-10-10.
- Acceptance of the 18 m5w packet branches and W0. They are merged into the local integration, without rulings.
- **Publication.**
  - `2dc6af2` (remote, `origin/claude/new-session-yavghm`) is the accepted publication baseline and is preserved.
  - `m5p-d051-fixes` and `m5w-integration` stay LOCAL.
  - Nothing is pushed, merged, tagged or packaged without explicit approval.
  - The CLAUDE.md "cut from main" convention is not reconciled.
- Where the Spine's M5 items (§12) belong, and whether the M5 exit waits for them.
- G9: whether destitution alone, untaxed, stays a revolt cause.
- The viewport's cover fit and folding the status and command bars (`docs/queue.md`, the P1 line).
- Whether the shortcut should highlight the realized deposit rows of the player's settlements before D-051 §2 lands.

## 15. Playtest readiness is not M5 exit

**Playtest readiness:** a build the Director can play, with its known issues disclosed in
`docs/m5-playtest-known-issues.md`.

**M5 exit and merge readiness** additionally need at least:
- CR-020 §B reassessed, and the calibration battery green or re-ruled;
- the deferred D-051 items scheduled;
- the unit-display rework (§7);
- the Spine M5 items dispositioned (§12);
- packet acceptance (§14);
- a green CI run, Windows smoke included, on the candidate;
- the Director's merge ruling.

**M5 is NOT closed.**

## Pointers added 2026-10-12 (dated notes; historical text kept)

- **Status lines and dated notes:**
  - ADR-036, ADR-037: RATIFIED.
  - CR-017: §3 / §5 scheduled for M7; the CR stays OPEN.
  - CR-020: §A and §B ruled; FPW-2 and the drift-tooth evidence added.
  - ADR-035: P-F1 and P-F0 unruled.
  - ADR-034: the same-schema divergence limitation closed by the incompatibility error.
- D-051 §1, §2, §3, §4 and §6.
- D-038 E1.
- `docs/milestones.md`.
- `docs/m5w-map-workstream-audit-and-plan.md`.
- `docs/queue.md`: the P8 `--size` line.
- `docs/current-state.md`: the routing entry.

## Pointer added 2026-10-15 (D-054; dated note, historical text kept)

- **§5: the save-format half is superseded.** "No visibility state and no save-format change ship in M5:
  `CanonicalSchema.Version` stays 32" no longer holds for the save format. The Director's 2026-10-14 instruction asks
  for incremental construction and a building/roads split, "with an explicit version migration if required". Schema
  **v33** therefore ships in M5 (ADR-039), with the first explicit migration (v32 → v33).
- **The visibility half STANDS.** There is no fog, no known extent and no explored-longitude state. The same instruction
  puts exploration gating and Satellites "EXPLICITLY OUT OF SCOPE FOR NOW".
- **§4 stands.** Movement and Action Points are M7, and order kind 10 stays reserved for MoveFormation. Batch 3 took
  kinds 11 and 12. §4's "(`OrderKind` 10, Action Points, schema v33/v34)" for S3a–S3c: v33 is now taken, so their
  state takes the next free version after S2a's.
- **The m5w plan's reservation** "v33 = the first schema bump to land" is met by batch 3, so S2a's known extent moves to
  v34.
- Record: `docs/d054-batch3-incremental-construction-allocation-cylinder.md` §5 items 6 and 13.

## Pointer added 2026-10-19 (D-059; dated note, historical text kept)

- **§9's "nothing is re-pinned" and "the two failing tests stay red" are superseded.** The Director's 2026-10-19
  instruction ("CI main test run, full authorization, resolve the issue best you can") authorises CR-020 §B option 1.
- The two per-seed recorded dev migration values and the dev population envelope are re-recorded. Both
  `Dev_MalthusCorridors_AllInBand` cases are green.
- **§9 otherwise stands:**
  - 1(b) stays ON;
  - the `corridors.json` bands and the 0.75 tooth are preserved;
  - nothing is waived, skipped or reverted.
- Record: `docs/d059-calibration-rerecord-d051-regime.md`.


> **2026-10-20 — Director (closure rulings R2–R5, R8; `docs/m5-exit-inventory.md` §13).** §7 unit-display rework: **deferred to
> M6's E1 visual milestone** by this dated ruling; §15 bullet 3 is discharged by deferral; the emblem plate and the card's
> truthful "not simulated" wording stay; nothing visual is marked complete. §12: taxation, authority-as-reach and
> legitimacy-as-reading are the delivered M5 mechanisms; laws-lite and the authority/bandwidth economy go to the M6 civic
> specification subject to architecture review; budget/treasury has no carrier under CR-008 (CLOSED: "There is NO treasury
> requirement") and is **unassigned pending a definition** — not placed in M8, which the Spine does not support. §14:
> ADR-035 P-F1 and P-F0 accepted as shipped; the 18 m5w packets and W0 accepted as a set (exceptions in §13). §15: the
> frozen exit-criterion definition (the Spine's "it's a game now" checkpoint) is answered by the Director's statement in
> §13 R8; the content items of the Spine's M5 line were moved out by §12 above and are now given destinations; no frozen
> definition is edited. The merge ruling (§15 bullet 7) is NOT given yet. M5 is NOT closed by this note.

## Dated note — 2026-10-22 (D-062; appended, the text above is unchanged)

§4 (movement and Action Points in M7) is CONFIRMED by D-062 R2. §5 (full fog, LIVE/GHOST/UNKNOWN, scouts, sighting memory in M6) is SUPERSEDED in part by D-062 R1/R11: M6 ships a minimal fog (UNKNOWN / VISIBLE / REMEMBERED, settlement sighting memory, rival formations only while visible) with Explore and Prospect orders; scouts and formation ghost rows move with movement to M7 (ADR-044). Record: `docs/d062-m6-director-final-rulings.md`.
