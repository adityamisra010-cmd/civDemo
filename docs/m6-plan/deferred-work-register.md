# Deferred-work register — M5 → M6 transition

**Status: RECORDED sweep, 2026-10-20, tree `dc83a67` (read-only; no file changed by the sweep).** Every item in the
record that is explicitly deferred, parked, queued for a later milestone, or marked open/not built, with its source,
who deferred it, the destination named in the record ("none named" when none is), its category (SIM / UI / TOOL /
TEST / DOCS / GOV) and whether a Director decision is still owed. IDs `DW-NNN` are stable in document order. INFERRED
marks the sweep's inference; everything else is quoted or paraphrased from the cited line. This register seeds the M6.1
deliverable "deferred-work register with destination milestones"; destinations here are the RECORD's, and the
recommendations for unruled ones are in `m6-decision-register.md` and `../m5-exit-inventory.md` §7.

**Housekeeping — items that read as open but are closed (fix the headers in M6.1):**
- G1 (harvest-weather turn-mean variance) is closed by D-051 §1(b) (`d051:24-25`); `milestones.md:687-691` and
  `queue.md:1437` still read it as open.
- CR-018 is resolved by ADR-030 (`cr-018:60-68`; `adr-030:3` RATIFIED) but its header (`cr-018:3`) still says OPEN.
- CR-012 header (`cr-012:11`) says DRAFT; `cr-012:162` "STATUS: RULED — KEEP. CR-012 closed."
- ADR-034 header (`adr-034:3`) says PROPOSED; `adr-034:7` "RATIFIED by the Director (D-050 ruling 1)".
- ADR-019 "exists only on an unmerged branch" (`milestones.md:506, 703`) — it is on `origin/main` at `a36b94d` (INFERRED from git).
- T3.7 merchants / Comfort-as-stock carried from M3 shipped in M4 (`m4-exit-inventory.md:59, 67, 96-97`).
- "Revolt-founded polity starts at Age A1 — OPEN" (`m5-playtest-baseline.md:765`) is claimed closed by `d048:70`;
  "Research not Age-gated vs Taxation A3 tag" (`:766`) is superseded by H2's Age gate (`adr-033:300-303`) — both INFERRED stale.

## Group 1 — destination M6 named in the record

| ID | Item | Source | Deferred by / when | Destination (verbatim) | Cat | Dir. owed |
|---|---|---|---|---|---|---|
| DW-001 | Fog of war cluster: full fog; LIVE/GHOST/UNKNOWN (D-039 C1); scouts; persisted sighting memory; discovered-extent storage & visibility persistence (m5w S2a–S2c, schema now v35); known-extent restriction of settle/claim/trade (D-040 B1); closing ADR-037's channels | `d052:53-66`; `milestones.md:35`; `d040:158-161`; `m5w-map-workstream-audit-and-plan.md:1196, 1229`; `m5-playtest-known-issues.md:266-271`; `adr-037:11-14`; `d060:236-237` | Director 2026-10-12: "The following are M6 work: full fog; …; scouts …; persisted sighting memory; discovered-extent storage and visibility persistence …; the known-extent restriction of settle, claim and trade …; closing the information channels" | "M6" | SIM+UI | N (scheduled) |
| DW-002 | Which world a fogged issuer's Found Settlement order is judged against (the placement-mode verdict leaks one bit about unseen ground) | `queue.md:1473`; `adr-036:467`; `m5w:1116`; `d052:62-63` | Agent (S1d, 2026-10-11); D-052 §5 routes it to M6 | "M6" (S2b's ruling) | SIM/GOV | Y |
| DW-003 | AI reads the whole map (colonies median 953 km) — leak accepted | `d052:27-29`; `known-issues:210-211`; `m5w:393` | Director 2026-10-12: "accepted until M6's known extent" | "until M6's known extent" | SIM | N |
| DW-004 | Decision A — rival map-token size (footprint/dwelling blocks) | `d060:255-258`; `known-issues:624-627`; `current-state.md:16` | Director 2026-10-20: "DEFERRED until fog of war … not a ruling that the sizing is permanently accepted public information … Revisit … (S2a)" | "until fog of war (S2a)" (= M6 by D-052 §5) | UI | Y (at fog design) |
| DW-005 | Are scouts a CLASS or notables? (D-039 B6) | `d039:77-82`; `d052:58` | D-039: "OPEN QUESTION, NOT RULED … M6's spec owns it" | "M6's spec" | GOV | Y |
| DW-006 | Trade's mutually-known term in `TradeQuery.CanTrade` | `m5w:762`; `t4.6-foreign-trade-decision.md:116-137` | m5w plan: "if ruled (M6 per the T4.6 note)" | "M6" | SIM | Y |
| DW-007 | Visual milestone (D-038 E1): individually drawn buildings/institutions with count staging 1/10/20/30 (D-043 D2/H8); Age-specific terrain & architecture; richer unit art (authored silhouettes); m5w P5, P12b, P14; the T2.12 map-symbology packet | `d052:68-78`; `milestones.md:32-34`; `d038:51-56`; `queue.md:96-116, 1475`; `m5w:160-166, 1198-1199` | Director 2026-10-12: "the visual milestone (D-038 E1), which the Director places at M6" | "M6" | UI | N |
| DW-008 | D-038 H8 (how many sprite parts before legibility fails) and "what drives the Age style register (Age, realized state or knowledge) is unruled" | `d038:208`; `m5w:938, 1113-1114` | Open since D-038; m5w audit | "M6" (visual milestone) | UI/GOV | Y |
| DW-009 | One-file session export (`.xlsx` beside the six files) + telemetry codebook v4 (337 rows); README §5 requirements (continuation cell at ≈49 settlements, self-check, exact double text, versioning, goldens); open spec questions: where written, folder-per-session, xlsx library | `milestones.md:40-43`; `queue.md:1562-1607`; `m6-session-export/README.md:3, 54-55, 71-77`; `m5-run-recording-audit.md:50, 107-123`; `current-state.md:17-18` | Director 2026-10-19: "Merge it all into one single excel in M6. add this into future agenda"; 2026-10-20 ruling: encoding not encryption, export beside the record | "M6" | TOOL | N (ruled 2026-10-20); spec Q3–Q4 open |
| DW-010 | Review/reconcile/complete research engine (ADR-029/030), Ages (ADR-031), unlock pipeline, universities (ADR-033 D6) | `milestones.md:85-88`; `m5-playtest-baseline.md:302, 760` | Roadmap rebase 2026-10-03: "M6 owns that work … M6 is NOT done" | "M6" | SIM | Y (ADR-029 PROPOSED) |
| DW-011 | 115 deferred research entities, tiers A–G (E = M7 military); 70 class-J nodes, 219 deferred links; census/services NOT WIRED; walls/temples knowledge-only "until M6 realizes them" | `deferred-entity-realization-plan.md:3-12, 28-30, 57-63, 82-84`; `research-gameplay-unlock-audit.md` §2; `adr-033:235-236`; `baseline:756`; `d049:396-399`; `m5w:704, 723` | Director LOCKED decision 5 (priority order); "All 115 remain DEFERRED … Tier membership is INFERRED and OPEN to Director revision" | "M6 / M7 / M11+ per tier" | SIM | Y (tier revision; first slice needs a per-building capacity mechanism packet) |
| DW-012 | Full Trade content chain (node 426 implemented; rest deferred) | `baseline:55, 759` | R2a/R4a | "M6 / M11+" | SIM | N |
| DW-013 | D-045/D-046 Part D: future research modifiers formula (seams exist, no writer); Age→calendar/dt mapping; repeat/recursive mechanics (G4); 38 Technique candidates; A9 reference population; capability-ownership-matrix changes | `d045:578-593`; `d046:387-396`; `queue.md:1270-1278`; `adr-029:609` | D-045/D-046 "OPEN (Director decision required)" | none named (research = M6 by roadmap, INFERRED) | SIM/GOV | Y |
| DW-014 | D-044 Part D: T2 Spine ":84 no tree" reading (CR owed if read strictly); T4 D-021 brake owed by first university-modifier writer; T5 D-020 grammar extension "may be ruled back"; T7 starting holdings (CR-006) | `d044:843-850`; `queue.md:1296-1298` | D-044 "UNRESOLVED (owner: director)" | none named (M6 domain) | GOV | Y |
| DW-015 | Director-ledger gap audit OPEN DECISIONS feeding the M6 spec: G-04 infra realization boundaries; G-05 D-009 bridges "era-gated" vs D-040 B3 (CR-009 never written); G-08 which lens shows Civics; G-14 who owns Age milestones; G-16 dt rules bound to Ages; G-25 university relevance; G-27 MobileAgent; G-31 institution lifecycle / CR-010 never written; G-32 PathBuild banks capacity; G-33 NOT in prerequisites | `docs/design/director-ledger-gap-audit.md:220, 230-232, 288, 328, 372, 480, 555, 638-640, 655, 687-695` | Audit (2026-09/10) | none named | GOV | Y |
| DW-016 | arch-PQ knowledge-cluster questions: Q-16 accumulation fence; Q-17 latch-derived publication; Q-19 technology object; Q-21 reserve conserved?; Q-22 residence grain; Q-24 diffusion; Q-25 foreign gating; Q-26 calibration corridor for knowledge — "the only open item … that can block a MILESTONE EXIT"; Q-27 literacy/education; Q-68 AI research strategy | `docs/design/arch-PQ-conflicts-and-questions.md:790-801, 847` | Architecture phase | none named (M6) | GOV | Y |
| DW-017 | AI: "M6+: deeper military and diplomatic reasoning" | `m5-ai-constitution.md:308` | AI constitution | "M6+" | SIM | N |

## Group 2 — destination M7 or later named

| ID | Item | Source | Deferred by / when | Destination | Cat | Dir. owed |
|---|---|---|---|---|---|---|
| DW-018 | Unit move orders, Action Points/Capacity, right-click move; m5w S3a–S3c; kind 10 reserved; ~~AP state at v36~~ AP state has no reserved schema version (v36 is ADR-049's; D-062 R11, D-066 A11 — corrected 2026-10-28, M6.19); unit-card wording stale ("awaiting the Director's ruling") — ~~stale~~ corrected 2026-10-28 (M6.12: the movement line now reads "Movement: not simulated — Battle Layer (M7)"). The M6 side of AP is the contract only ([training-ap-contract.md](training-ap-contract.md), H7-02) | `d052:46-51`; `cr-017:7-12`; `milestones.md:36`; `m5w:1197, 1229-1230`; `d054:407`; `known-issues:154-155, 266` | Director 2026-10-12: "Unit move orders, Action Points / Action Capacity and right-click move are M7 Battle Layer work. CR-017 §3 … and §5 … must be ruled before any M7 movement packet" | "M7" | SIM+UI | Y (CR-017 §3/§5 substance) |
| DW-019 | CR-017's other collisions (Ages as state, transition surge, mobile agents, war pulses; seven collisions) | `cr-017:3-6, 29-32` | Raised 2026-09-29; scheduling only ruled | §3/§5 "M7"; rest none named | GOV | Y |
| DW-020 | Military realization: recruitment, headcount, armies in motion, ZOC, war pulses, conquest; Age military milestones PENDING recruitment; tier-E entities; city_wall defence effect | `milestones.md:53-58`; `adr-033:151-158`; `d052:89`; `baseline:385, 752, 758, 761`; `m5-integration-coherence-matrix.md:221-224`; deferred-entity plan `:37, 57-63` | Roadmap rebase 2026-10-03 | "M7" | SIM | N |
| DW-021 | Annexation gameplay (`MergeInto` has no caller), conquest, recovery of revolted settlements; transfer of institutions on conquest/secession "later work — open question" | `baseline:752-755`; `adr-033:258-259`; coherence `:172-183`; `institutions-universities.md:61` | R3/D-048 | "M7 (Battle Layer), M8" | SIM | Y (institution transfer) |
| DW-022 | CR-019 revolt polity vs T4.5 raider gate; options 2/3 open | `cr-019:3, 44-50`; `baseline:751` | Director 2026-10-04: "DEFERRED to the later military and political milestones" | "M7 / M8" | SIM/GOV | Y (later) |
| DW-023 | Capital succession/relocation; revolt-founded civs cannot tax until then | `d050:18-19`; `adr-033:305-306`; `baseline:754` | Director 2026-10-06: "Capital succession deferred to M8 Politics / Diplomacy" | "M8" | SIM | N |
| DW-024 | Finer population segments (faith, ethnicity, occupation) | `d050:12`; `d049:418, 622` | Director 2026-10-06: "Finer segments are deferred to M9 Society" | "M9" | SIM | N |
| DW-025 | D-039 Parts A/C/D/E (command friction, reconnaissance, stale-ghost display, siege incl. D5 starvation, campaign layer); D-011 §8 decisions; GHOST sighting memory | `d039:7-10, 59, 80, 175, 181`; `d011:84-86`; `queue.md:117-122`; `m5w:794, 810`; `adr-037:22, 43` | D-039/D-011 ("close at M6 spec" → M7 per the rebase note; GHOST display moved to M6 by D-052 §5) | "M7 Battle Layer spec" | SIM/UI | Y (D-011 §8 at the M7 spec) |
| DW-026 | Political notables & movement leadership (GOV-2 §1b); D-021 valve 5 escalation; D-021 valves 2 (fatigue) and 3 (unrest exit) DEFERRED | `docs/design/arch-N-milestone-allocation.md:169`; `m4-pre-spec-dependencies.md:100-106`; `d049:426`; coherence `:137` | GOV-2 / D-049 | "M8" / none named | SIM | N |
| DW-027 | Cultural/religious plurality & creation (Q-35); environment & climate placement UNRESOLVED (Spine M9); military full M9+; espionage M11+ | `arch-N:170, 184-185`; `arch-PQ:803, 809`; `d039:9-10` | Spine / D-011 / design audits | "M8/M9", "M9 (UNRESOLVED)", "M9+", "M11+" | GOV | Y (climate placement) |
| DW-028 | R-3 clone architecture / bucket cap / clone-size claim (ADR-020 design complete, awaiting ruling) | `queue.md:656-682`; `adr-020:344`; `milestones.md:503, 525, 702-703`; `arch-N:189` | Director T3.11/T4.16: "the largest unscheduled item in the project — it bites at M8/M9" | "bites at M8/M9" | GOV/SIM | Y |
| DW-029 | Items parked to the M10 Integrated Civilization Simulation gate: Prev read-only facade; dropped-local Conserved escape; world travel-scale/expansion-opportunity | `queue.md:1-2, 8-26, 1461` | Director (T2.9) + rebase 2026-10-03 | "M10" | SIM/TOOL | N |
| DW-030 | Disaster architecture (hazard → empirical frequency → exposure → … → recovery); CR-016 rate & options | `milestones.md:675-685`; `cr-016:541-560`; `queue.md:1448, 1456`; `baseline:769` | Director 2026-09-21: "POST-M4 and belongs to the civilization architecture phase … No arbitrary rate may be implemented to close a milestone" | "POST-M4" (no number) | SIM | Y |
| DW-031 | T4.21 lines queued "for M5" and not done in M5: pastoralist substitution CR G6(b); "stranded by capacity" founding G7(b); per-year food-balance sub-step G8(c) (CR-016 option 3) | `queue.md:1441-1443, 1140-1157`; `cr-016:532-537`; `arch-N:137` | T4.21 architecture §11 (2026-09-17) | "M5" (missed) → none now | SIM | Y (re-home) |
| DW-032 | T4.21 queue lines never implemented: disaster spatial correlation; weather-conditioned hazard; "granary loss" kind NOT authorized; exact onset renewal; kind registry; a=1/3 TUNE alternative; AppliedMultiplier field; CLI chronicle-file gap (`SessionEmitter` writes no chronicle); AutoplayMetrics starvation split & famine counters; T4.21-5 recentStress, inflowAeByChannel, ViabilityProducts strings | `queue.md:1431-1436, 1438-1440, 1444, 1451-1453`; `m5-run-recording-audit.md:11` | T4.21 (2026-09-17) "none implemented" | none named | SIM/TOOL/TEST | granary-loss authorization Y; rest N |

## Group 3 — "after the M5 playtest", no milestone named

| ID | Item | Source | Deferred by / when | Destination (verbatim) | Cat | Dir. owed |
|---|---|---|---|---|---|---|
| DW-033 | D-051 §1(a): flight reads a smoothed multi-turn deficit (1(b) time-averaged weather is ON) | `d051:18, 30-31`; `d052:127-130`; `known-issues:88-89` | Director 2026-10-12: "The rulings stay RATIFIED. Their implementation is deferred until after the Director's M5 playtest" | "until after the Director's M5 playtest" | SIM | Y (schedule — D-052 §15 exit condition) |
| DW-034 | D-051 §2: availability classes, common-material floor, regional richness ("expansion must pay"); disclosed consequences (38 % stone-poor; shortcut outlines known land; P9/P10 data gaps) | `d051:40-50`; `d052:131, 134-137`; `known-issues:68-85`; `queue.md:1470-1471`; `m5-playability-gate.md:916` | same ruling | same | SIM | Y |
| DW-035 | D-051 §3: expected-value next-turn food/population forecast (private copy, expected weather, "acts from turn N+2"). The 2026-10-20 RESOURCES "Next turn" column is a one-step stores preview on queued orders — see `../m5-exit-inventory.md` §10 (Decision C) | `d051:54-61`; `d052:132, 138`; `known-issues:90` | same ruling | same | UI/SIM | Y (Decision C) |
| DW-036 | D-051 §4: primitive stone-and-wood toolmaking (Workshop guard stays; approved direction) | `d051:70-77`; `d052:140-147`; `cr-020:3`; `known-issues:102-104` | Director 2026-10-12: "Its mechanism … and its milestone are deferred until after the playtest" | "after the playtest; milestone deferred" | SIM | Y (mechanism + milestone) |
| DW-037 | D-051 §6 central stockpile — everything beyond D-053's two parts: housing materials, recipe inputs, tool equipping, trade, prices, migration food gate, colony provisions, livestock & fish, spoilage/bound; D-042 §4.3/§10.1 supersession to confirm | `d051:98-127`; `d052:148-152`; `d053:151-168, 188`; `known-issues:121-129`; `adr-038:197-198`; `m5w:278, 948` | Director 2026-10-10/12: "DEFERRED until after the Director's M5 playtest; no partial pooling …" (two parts superseded 2026-10-13) | "after the playtest" | SIM | Y (schedule; D-053 §6 Q3) |
| DW-038 | Calibration reassessment: dev-corridor reds re-recorded by D-059 (acceptance pending); remaining: nightly drift under the cylinder (density band 16/20, migration breach 80.83 %, D-054 Q8); quarantined canonical corridors "no milestone ruled" | `d052:122-125, 192, 232-241`; `d054` §10 Q8; `d059:1-11`; `known-issues:291-296`; `baseline:757`; `milestones.md:669-673` | Director 2026-10-12: "Calibration is reassessed from measured evidence after the playtest" | "after the playtest" | TEST | Y (Q8; corridor disposition) |

## Group 4 — open Director rulings / acceptances owed

| ID | Item | Source | Who / when | Cat | Dir. owed |
|---|---|---|---|---|---|
| DW-039 | Spine M5 items budget, authority/bandwidth economy, laws-lite — not built; "The ruling names no destination milestone, and does not say whether the M5 exit waits for them" | `d052:154-158, 181, 195`; `milestones.md:37-38, 49-50`; `known-issues:270-271`; `current-state.md:318`; `arch-PQ:770` | Director 2026-10-12 | SIM/GOV | Y |
| DW-040 | D-052 §14 still open: acceptance of the 18 m5w packet branches + W0; publication; CLAUDE.md "cut from main" convention; G9 destitution-alone revolt cause; folding the status/command bars; shortcut highlighting realized deposit rows before D-051 §2 | `d052:172-184`; `current-state.md:313-316`; `queue.md:1462-1468`; `m5w:1206` | Director 2026-10-12 "Not ruled by this record" | GOV/UI | Y each |
| DW-041 | D-052 §15 exit/merge conditions outstanding (see `../m5-exit-inventory.md` §0) | `d052:80-98, 186-200` | Director 2026-10-12: "M5 is NOT closed" | GOV/UI | Y |
| DW-042 | D-053 §6: Q1 food urgency metric; Q2 granary farm bonus 5 % vs 10 %; Q3 confirm D-042 §4.3/§10.1 superseded; Q4 confirm F-138 reading | `d053:185-191`; `adr-038:199-200`; `known-issues:98` | Batch 2 record 2026-10-14 | SIM | Y |
| DW-043 | D-054 §10 unruled: Q1 refund on cancel; Q2 default split; Q3 AI structural-stall cancel; Q4 LOD on window width; Q5 whole-world overview / minimap; Q6 AI whole-cost prudence; Q7 starvation-guard deviation; Q8 cylinder calibration drift; Q9 old sessions & manifest topology; Q10 GPU seam; Q11 seam metric; Q12 RoadMaterials reason id; Q13 famine rig density; Q15 deviations to confirm; Q18 seam-movement coverage; Q19 where dated notes sit in frozen docs | `d054` §10 (`:1113-1200`); `d055:1316-1318`; `queue.md:1479, 1483, 1490-1499`; `known-issues:297-324` | Batch 3 record 2026-10-15 | SIM/UI/GOV | Y |
| DW-044 | Acceptance owed on implemented-under-authority work: D-055 (incl. O7/O9 need an ADR, O8 deferred), D-056/ADR-041, D-057/ADR-042/CR-022, D-058, D-059, D-060 (incl. the F12 and session-file scope decisions), `3c18a71` | `d055` §10–§11; `queue.md:1543-1546`; `d056:3-4`; `d057:3-9, 219-227`; `cr-022:3`; `d058:6-8`; `d059:9-11`; `d060:66-68, 85, 250-252` | Each record: "awaits the Director's acceptance" | GOV | Y |
| DW-045 | CR-005 formal disposition (status OPEN; rebase placed knowledge at M6) | `cr-005:3, 7, 163-172`; `queue.md:1191-1199`; `current-state.md:884` | Raised pre-M5 | GOV | Y (close-out) |
| DW-046 | CR-006: mid-turn hand-backs vs atomic turn; 10,000 BCE epoch vs ADR-002 / CLAUDE.md "6,000 years"; BCE/CE converter unowned; blocks the temporal-control placeholder | `cr-006:3-7`; `m5-temporal-control-and-player-agency-placeholder.md:7-11, 215-224, 229-281`; `queue.md:1200-1209`; `arch-N:32` | Raised by the placeholder | GOV/SIM | Y |
| DW-047 | CR-016 (DW-030) plus the disarmed-tree items that flip with it; CR-003 Malthus teeth re-aim NOT taken; ADR-018 §11 premise open | `queue.md:1448-1450, 1456-1457`; `cr-016:3, 541` | T4.21-4/-7 (2026-09-18) | TEST/SIM | Y |
| DW-048 | ADR-017 "director certification pending" | `adr-017:3, 12-13`; `milestones.md:502, 524, 702`; `adr-035:228` | Since T3.6b | GOV | Y |
| DW-049 | ADR-027 sign-off: `CorridorStatus.cs` under `Sim.Core/Kernel/` | `adr-027:3, 86`; `milestones.md:704` | M4 final closure | GOV | Y |
| DW-050 | ADR-029 acceptance and its golden | `adr-029:3` | Research foundation | GOV | Y (M6) |
| DW-051 | ADR-035 P-F1 and P-F0 unruled (ship in the build) | `adr-035:3, 20-24, 226-236`; `d052:174, 208` | Director 2026-10-12: "still UNRULED: P-F1 and P-F0" | SIM/GOV | Y |
| DW-052 | ADR-038/039/040 PROPOSED "for the Director's review with D-053/D-054/D-055" | `adr-038:3`; `adr-039:3`; `adr-040:3` | Batches 2–4 | GOV | Y |
| DW-053 | M4 known-open rulings travelling: T4.19-E structural set S1–S5; grain storage-bounded vs livestock/fish; density quarantine disposition; nightly gate never consults the window; six stale claims in current-state.md | `milestones.md:504-505, 526-527, 530, 564, 669-673, 700-705`; `queue.md:1401-1406`; `arch-PQ:821` | M4 closure 2026-09-21 | GOV/SIM | Y |
| DW-054 | CR-009 (era gates) and CR-010 (what is an institution) listed as open CRs but never written; D-035-C path 6 binds M5 by name; institutions carry ≥6 meanings with zero code | `current-state.md:884`; `cr-017:21`; `arch-N:196, 265`; `director-ledger-gap-audit.md:232, 639` | Design audits | GOV | Y |
| DW-055 | D-042 §14.1: D-018 per-class income column stale vs D-042 §4.4/§4.6 | `current-state.md:884`; `queue.md:1266-1268` | D-042 | DOCS/GOV | Y |
| DW-056 | ADR-015 candidate sections never written: GOV-3 "CONDITIONAL EXECUTION AND STANDING AUTONOMY" and "AN OPERATION THAT LOOKS LIKE IT SUCCEEDED IS NOT EVIDENCE THAT IT DID" | `queue.md:736-750, 844-885` | Director instruction 2026-08-06 | GOV | Y |
| DW-057 | Process: a required status check can be bypassed silently by the pushing identity; nightly failure must surface where someone reads it | `queue.md:804-809, 693-714`; `milestones.md:530` | T3.12 / T4.1e | TOOL/GOV | Y |
| DW-058 | T4.20 rulings owed: BoundStore capacity/spoil split as PUBLIC STATICS; §0 DIFFERENCED wording; grievance accrual attribution rule; policy-consequence counterfactual replay | `queue.md:1407-1411, 1419-1423`; `m4-observability-gaps.md:12, 20, 23` | T4.19/T4.20 | SIM/TOOL | Y |
| DW-059 | Transport design conversation: Q-A rivers on the stride-4 lattice; Q-C draught animals; Q-E emergent trade routes; D-040 sea travel; Escalation 1 deadband > price band; D-040 F-tension; ADR-032 §10.8 DEFERRED items | `queue.md:355-375, 472-535`; `d040:156, 208-209, 225`; `adr-032:346-350`; `milestones.md:285-299` | T3.6b–T3.8 Director rulings; ADR-032 | SIM | Y |
| DW-060 | Housing tiers in kind (Q-D) and D-017's farmland-consumption half "not built and scheduled nowhere" | `queue.md:528-535`; `milestones.md:330-345`; `m5w:1113` | T3.8 / T3.12 | SIM | Y (owner) |
| DW-061 | Spacing should derive from computed state (law 4) — "OPEN DESIGN ITEM, NO OWNER" | `queue.md:1073-1081` | T4.1b | SIM | Y |
| DW-062 | `ResearchCompletedRow` has no turn (history via replay only); a turn field is a schema bump | `queue.md:1471` | P10, 2026-10-11 | SIM | Y |
| DW-063 | Place card outside the placement mode is judged without this turn's queued foundings | `queue.md:1474`; `adr-036:525` | S1d review | UI | Y (minor) |
| DW-064 | ADR-033/R3 open: `farming.preCultivation` committed OFF; R3 item 7 STOPPED; Ledger negative-amount exception at N=100 | `adr-033:237, 255-257`; `baseline:273` | R2a/R3 2026-10-03 | SIM | Y |
| DW-065 | Director playtest coverage still owed: PT-04–PT-11; chronicle wording fix; checklist 21–22; tested build INFERRED `f80dce6` | `known-issues:606-613`; `current-state.md:7-9` | 2026-10-20 note | TEST | Y (acceptance act) |

## Group 5 — residual defects / limitations, accepted or open

| ID | Item | Source | Status | Cat | Dir. owed |
|---|---|---|---|---|---|
| DW-066 | D-060 §9: DEV panels and session files world-wide by decision; the read-isolation allowlist cannot see gated reads; "Known" is all-known until S2a | `d060:226-239` | Accepted (A/B ruled 2026-10-20) | UI | N |
| DW-067 | D-057 §14: kind-13 packing bounds; bare-log runners replay a same-schema other-build log as this build's run; no-trace resume replays to the last named turn; run-id collisions; partial emitted sessions | `d057:280-287`; `queue.md:1558` | Recorded | TOOL | N |
| DW-068 | D-055 §9 still standing: head-of-line wait; v33-under-v34 ADR-034 refusal; Controls-less worlds hold per settlement; doubly-controlled places; GPU seam headless-untested; persistence gaps across processes; +0.15 ms/turn; labels are estimates; AI does not read holds; wall-clock bench reddens under load | `d055:1227-1283`; `queue.md:1535-1542, 1551` | Recorded | SIM/TEST/UI | N |
| DW-069 | D-058 §9: no in-turn forecast of a continuation; the head line describes only the head | `d058:189-191`; `queue.md:1608`; `known-issues:550` | Recorded | UI | N |
| DW-070 | Known-issues: native incompatibility dialog untested on Windows; `--size` sessions from earlier builds stop reproducing; no GPU frame timing; overlay key's 16 km-node sampling unstated; drifted older sessions refused | `known-issues:161-163, 184-188, 240-242, 249-250, 378, 452-463` | Disclosed | UI/TEST | N |
| DW-071 | Batch-4/CI residue: `--from-save` refuses saves of worlds founded with more Empires than settlements after a revolt-born AI polity; allocation leg exercises no starvation-guarded step; two CI reference hashes recorded not pinned; FamineScenarioTests ρ report cylindrical vs frozen flat series; T1b clause shown whenever split < 100 | `queue.md:1500-1507, 1519-1534` | Recorded | TEST/TOOL | N |
| DW-072 | Batch-2/3 residue: relief recomputes polity membership per recipient; AI reserves every input; goal-sequential AI research — tax and university gates trail the Ages; famine factor rarely visible; part-held crafted input reads "needs more knowledge" | `queue.md:1460, 1477-1478`; `adr-038:201-205` | Recorded | SIM/UI | N |
| DW-073 | P9/P10/P11 presentation gaps: place-card report carries low-order bits of undiscovered ground (M6 channel); overlay data gaps; shortcut never says "discovered"; formation card scrolls at 1080×640 | `queue.md:1469-1472, 1475` | Recorded | UI | N |
| DW-074 | Stale texts: `Sim.Cli/SessionEmitter.cs:12` "writes five files"; `docs/session-records.md:3` "Four files"; `queue.md` cites a moved `SessionLogPath` line; `CLAUDE.md:10` names `m5-integration` and an "unmerged art-substrate branch" (merged at `c655a6d`, 2026-07-28); main's `CLAUDE.md` names M4; D-054 Q19 pending so batch notes sit at file ends | `m5-run-recording-audit.md:81-83`; `m4-forensic-p013-record.md:478`; `d052:19-20`; `CLAUDE.md:10` | Open | DOCS | Q19 only |
| DW-075 | AI issues no labour orders (E3), unassigned | `baseline:764`; coherence `:91` | Open | SIM | N |
| DW-076 | Older queue items still open as written, no destination (each with its queue.md lines): order-log header binding of founding parameters (`:6`); Buckets linear scans (`:7`); tool-wear needs vintaged capital (`:37-51`); lattice stride floor (`:52-64`); extent-of-market generalization (`:148-152`); under-utilisation audit (`:153-157`); recipe-chain ordering contract (`:158-161`); price solver O(S²G²) (`:162-166`); Lambda>rail watch (`:173-177`); libm on the determinism surface (`:194-205`); √(1−ρ²) and spatial-correlation tests unpinned (`:209-225`); grievance monotonicity TUNE (`:244-249`); aggregation strength thinly pinned (`:250-253`); sampler state implicit (`:283-291`); nearMean<0.90 sole mutant tooth (`:336-349`); fallout enumeration per-solution (`:580-586`); sweep capture rule (`:682-692`); migration floor on small worlds (`:715-726`); world population 1.63× unbisected (`:727-735`); T4.1 order-conditional framing (`:751-754`); discriminator-behind-hash audit (`:755-766`); minSpacingKm third coupling / ADR-018 §7 (`:810-819`); dev-migration quarantine bisect "stays owed" (`:820-844`); T4.8 notable-spawner items (`:1117-1130`); worldgen places no pastoralists (`:1131-1139`); appropriation dt seam (`:1158-1166`); founding food endowment 21 yr vs 1.5 yr cap (`:1385-1391`); no food good trades; zero herding-dominant settlement-turns; CausalChain food-category string (`:1413-1428`); T4.21-2 basin-cap residual cut (`:1447`); T4.21-8 detonator two-rig ambiguity (`:1459`) | `docs/queue.md` | Open | mixed | Y where the line names a ruling |

## ADR / CR status lines still PROPOSED / AWAITING / RAISED (verbatim)

| File | Status line |
|---|---|
| `adr-017-d025-founding-variation.md:3` | `Status: proposed (director certification pending) · Packet: T3.6b · Date: 2026-07-29` |
| `adr-020-clone-architecture-r3.md:344` | `**STATUS: DESIGN AND MEASUREMENT COMPLETE. No implementation. Awaiting director ruling …` |
| `adr-027-corridor-status-and-the-checked-checker.md:3` | `**Status: PROPOSED (director sign-off required — see §4).**` |
| `adr-029-research-engine.md:3` | `**Status:** PROPOSED. It is implemented on branch research-progression-foundation. Acceptance, and the golden …` |
| `adr-034-order-validation-deferral.md:3` | `**Status:** PROPOSED — awaits the Director's acceptance ruling.` — stale: `:7` "RATIFIED by the Director (D-050 ruling 1)" |
| `adr-035-founding-turn-harvest.md:3` | `**Status: PROPOSED — awaits the Director's acceptance ruling.**` (P-F2 accepted; P-F1/P-F0 unruled, `:20-24`) |
| `adr-038-empire-stockpile-food-relief-granaries.md:3` | `**Status: PROPOSED — uncommitted, for the Director's review with D-053.**` |
| `adr-039-schema-v33-orders-11-12-and-cylinder-topology.md:3` | `**Status: PROPOSED — uncommitted, for the Director's review with D-054.**` |
| `adr-040-schema-v34-construction-holds-pathfinder-surface-and-cli.md:3` | `**Status: PROPOSED — uncommitted, for the Director's review with D-055.**` |
| `adr-041-identity-cancel-replay-guard-same-step-edge.md:3` | `**Status: IMPLEMENTED 2026-10-18 under the Director's mandate "Fix the game. Change what needs changing." Awaiting the Director's acceptance …` |
| `adr-042-replay-identity-proof.md:3-4` | `**Status: IMPLEMENTED 2026-10-18 on claude/new-session-yavghm under the Director's delegated authority (D-057). Not merged; awaiting the Director's acceptance.**` |
| `cr-005-m5-research-technology-institutions-placement.md:7` | `**Status: OPEN — awaiting director ruling. No frozen document has been edited.**` (superseded-note at `:3`) |
| `cr-006-continuous-time-and-campaign-epoch.md:3` | `**Status: OPEN — awaiting director ruling. No frozen document has been edited and no code has been changed.**` |
| `cr-012-malthus-architecture.md:11` | `**Status: DRAFT, awaiting director ruling. No code changed.**` — stale: `:162` "STATUS: RULED — KEEP. CR-012 closed." |
| `cr-016-armed-disaster-fallout.md:3` | `**Status: OPEN — ESCALATED TO THE DIRECTOR. Nothing is fixed here.**` |
| `cr-017-ages-surge-and-mobile-agents-vs-frozen-items.md:3` | `**Status: OPEN — awaiting director ruling. No frozen document has been edited and no code has been changed.**` |
| `cr-018-research-rate-per-turn.md:3` | `**Status: OPEN — escalated to the Director.**` — stale: `:60-62` "Resolved by ADR-030" |
| `cr-019-revolt-polity-vs-t45-raider.md:3` | `**Status:** OPEN.` (`:44-46` DEFERRED to M7/M8) |
| `cr-022-identity-proof-vs-d008-replay-recovery.md:3` | `**Status: RAISED 2026-10-18 AND IMPLEMENTED UNDER DELEGATED AUTHORITY (option 2) — AWAITING THE DIRECTOR'S RULING.**` |

Also awaiting acceptance though not ADR/CR files: `d053:3` "RECORDED 2026-10-13/14, uncommitted, for the Director's
review"; `d056:3-4`, `d057:3-9`, `d058:3-8`, `d059:3-11`, `d060:3, 250-252` "IMPLEMENTED … awaits the Director's
acceptance".


## Dated note — 2026-10-21 (M6.1; appended, the text above is unchanged)

- Housekeeping items in the header: CR-012, CR-018 and ADR-034 headers now carry dated superseding notes (appended by
  M6.1); G1 and the `milestones.md`/`queue.md` stale readings, the ADR-019 branch note, the T3.7 carry and the two
  INFERRED-stale playtest lines are RECORDED here and left for the next docs pass that touches those files.
- New deferred items from T6.1 (`docs/m6-foundations-audit.md` §F): the "chosen, never derived" constants (queued in
  `docs/queue.md`), the code-literal lattice stride and AI valuation windows, the stale code comments (to T6.2).

## Dated note — 2026-10-28 (M6.19, D-066 R2; DW-018 corrected in place, struck text kept)

- **DW-018:** "AP state at v36" was stale — v36 is ADR-049's, and Action Points / Action Capacity state has no reserved
  schema version (D-062 R11; D-066 A11); it takes the next free version when M7 lands it. The unit card's stale
  "awaiting the Director's ruling" movement wording (`m5-playtest-known-issues.md`) is corrected by M6.12. The item's
  destination (M7) and its owed ruling (CR-017 §3/§5) are unchanged. M6 delivers the AP/training contract only
  ([training-ap-contract.md](training-ap-contract.md); `m7-handoff-register.md` H7-02).

## Dated note — 2026-10-28 (D-066 §2 A19 and §3; M6 closure track FOG; appended, the text above is unchanged)

The fog rows the D-066 §3 deferral table carries, restated here per row. Each "Destination" of the rows above that says
"M6" is superseded for the parts listed **M7**; the parts listed **M6** are done or completed in M6 (D-066 §3).

| ID | Part | Disposition | Reason | Dependency | Owner / milestone |
|---|---|---|---|---|---|
| DW-001 | Stored known extent, UNKNOWN / VISIBLE / REMEMBERED, settlement sighting memory, Explore / Prospect | DONE in M6, preserved with its tests (ADR-044) | — | — | M6 (Track EX) |
| DW-001 | Extent monotone across revolt and loss of control; the leak gate on the production fog; the AI's valuation of unseen terrain (closing ADR-037 §6's valuation channel) | COMPLETED in M6 (D-066 §3; A18; ADR-044 dated note 2026-10-28) | — | none | M6 (closure track FOG) |
| DW-001 | LIVE / GHOST rows and the ghost display; scouts and reconnaissance; sighting updates from moving formations | DEFERRED (D-066 R1) | A ghost is a last-seen position of something that moves; scouts and sight along a path need movable units | Movable units: order kind 10, per-step positions, Action Points | **M7** — H7-01 / H7-07 (single owner, see `m7-handoff-register.md` note of 2026-10-28) |
| DW-001 | The known-extent restriction of claim and trade routing (D-040 B1's "claim, trade") | DEFERRED (D-066 A19; slice contract E8; ADR-044 §11) | E8 ruled no new gate in the M6 slice beyond the founding arm; road and trade routing belong with the movement graph | The M7 movement / route graph these routings share | **M7** (movement, D-039 parts A/D/E) |
| DW-002 | Which world a fogged issuer's Found Settlement order is judged against (the placement-mode verdict reads unseen ground) | DEFERRED (D-066 A19) | The founding predicate's spacing, route and candidate-floor arms read the whole map; restricting them is a RULE change shared by the player's placement mode and the AI (one predicate), not an AI-only channel. A18 closed the AI's VALUATION only; the residual is pinned open by `PolityKnowledgeTests.RecordedResidual_ThePredicatesOwnArms_ReadLandTheIssuerDoesNotKnow_SharedByThePlayerAndTheAi` | The M7 movement / route graph (the route arm is a travel question) | **M7** (with DW-001's routing row) |
| DW-006 | Trade's mutually-known term in `TradeQuery.CanTrade` | DEFERRED (D-066 A19) | Trade pairing against the known extent is part of trade routing, which E8 left ungated in M6 | The M7 movement / route graph | **M7** (with DW-001's routing row) |

## Dated note — 2026-10-28 (M6 exit audit: aligned with D-066 §3)

The fog deferral rows are carried as in D-066's dated note of 2026-10-28 (one owner each):
- **LIVE/GHOST, scouts and moving sight:** H7-07.
- **Claim, trade AND road routing** (DW-001 deferred part, DW-002, DW-006): M7 route graph (H7-01).
- **Siege / line of sight:** H7-04.
- **Discovery bench:** completed, recorded in the ADR-044 dated note.

The earlier note's attribution of claim/trade routing to "D-039 A/D/E" is corrected: D-039 parts A, D and E are command
friction, siege and campaign, not the route graph.

DW-036 (D-051 §4) stays open and needs a Director ruling (D-066 dated note, M6.24).

## Dated note — 2026-10-28 (D-067 exit-gate rulings R10–R14, R19; appended, the text above is unchanged)

The Director's rulings in [D-067](../d067-m6-exit-gate-director-rulings.md), restated per row. Where a row above (or an
earlier dated note) says otherwise, this note governs for the parts listed.

**DW-034 and DW-036 (D-067 R19).** Both were swept on 2026-10-20 at `dc83a67`, before closure ruling R2 placed D-051 §2
and §4 in M6 (`../m5-exit-inventory.md` §13; `m6-decision-register.md` D-N). The Director's words (2026-10-28): *"They are
deferred until after the Director's M6 playtest, with no milestone named. M10 is a content-mapped future possibility,
not a commitment. The M7 economy track is a coupled alternative, not an assignment."* The two are deferred together.

| ID | Part | Disposition | Deferred by / when | Destination (verbatim) | Owner when scheduled | Dir. owed |
|---|---|---|---|---|---|---|
| DW-034 | D-051 §2(a) common-material floor; §2(b) regional richness classes; §2(c) the goal ("expansion must pay") | DEFERRED (D-067 R19); D-051 §2 stays RATIFIED, not built | Director 2026-10-28 (D-067 R19) | "until after the Director's M6 playtest, with no milestone named" | None assigned. M10 is a content-mapped future possibility, not a commitment; the M7 economy track is a coupled alternative, not an assignment | Y (milestone; and the contradiction below) |
| DW-034 | §2 availability class: deposit concealment | DONE in M6 (D-062 R6/R7, ADR-044); unchanged by R19 | — | — | M6 | N |
| DW-036 | D-051 §4 primitive stone-and-wood toolmaking, with its unresolved CR-020 §A mechanism choice | DEFERRED (D-067 R19); D-051 §4 stays RATIFIED; the Workshop guard stays (D-052 §11; `ConstructionQuery` → `InputUnobtainable`) | Director 2026-10-28 (D-067 R19) | "until after the Director's M6 playtest, with no milestone named" | As DW-034. The coupled M7 alternative is ADR-056 EC-1 toolmaking (see `queue.md` 2026-10-27, EC-1: crafting labour splits equally across available recipes) | Y (mechanism + milestone) |

- **Which playtest.** The 2026-10-12 phrase "until after the playtest" (D-052 §10, §11; the DW-034/DW-036 rows above)
  referred to the M5 playtest. R2 (2026-10-20) replaced it with "→ M6"; R19 supersedes R2 for these items and is a new
  deferral.
- **Carried open for whoever schedules DW-034:** D-062 R6 says deposits are "randomly distributed"; D-051 §2(b) says
  richness is "geographically correlated … not a random site". Unresolved.
- **Playtest disclosure:** stone-poor settlements and no Workshop before bronze (D-067 R19; `m6-playtest-checklist.md`
  §2).
- The earlier note's last line, "DW-036 (D-051 §4) stays open and needs a Director ruling", is answered by R19.

**DW-001, DW-002, DW-006 (D-067 R10).** The known-extent restrictions on claim, trade and road routing are deferred to
**M7**. This explicitly **supersedes DW-001's M6 assignment** (the Director's 2026-10-12 "the known-extent restriction of
settle, claim and trade" as M6 work) for those parts. The D-066 A19 rows above already read M7; their stated basis
("E8 ruled") is corrected: slice contract E8 is the coordinator's design document, not a Director ruling, and R10 is the
ruling. The founding predicate's reads of unseen land (DW-002) stay **M7**, as recorded above. R10 authorizes no
implementation now.

**DW-038, migration part only (D-067 R11, R12).** The density band, D-054 Q8 and the D-059 dev-corridor reds are
unchanged by this note.

| ID | Part | Disposition | Record | Dir. owed |
|---|---|---|---|---|
| DW-038 | `migrationGrossPerDecade` corridor: the lower observed migration baseline | **ACCEPTED as an intentional design acceptance (D-067 R11). It is NOT a causal explanation.** The band [0.001, 0.01] and quarantine window [0.0009, 0.01] stay unchanged as **historical records, not tuning targets**; no migration constant, band or window is tuned or re-cut. The corridor keeps reporting and does not gate (T3.12). | D-067 §2 | N |
| DW-038 | Causal attribution of the drop | **Optional; deferred; NOT an M6 exit gate (D-067 R12).** HYPOTHESIS (unmeasured on the corridor's canonical world): D-051 §1(b) time-averaged harvest is the leading suspect. The M4-era decline is unattributed. | D-067 §2 | N (optional work, unscheduled) |

- **MEASURED (D-067 §2): the drop happened during M5**, between the M5 hardening reading of 2026-10-05 (0.000430–0.000914,
  1/20 seeds in window; `../m5-hardening-measurements.md:227`) and the D-053/D-054 batch measurements (batch 3:
  1.7256e-4–3.5233e-4; `../d054-batch3-incremental-construction-allocation-cylinder.md:680`). The integrated M6 tree reads
  1.8083e-4–3.5084e-4 (20 seeds, 650 turns, `TestResults/validation/m6-soak/`), essentially the M5-exit envelope: the M6
  integrated tree did not cause the drop.
- **Stale owner string, left unedited:** the quarantine's `"owner": "M4 CR-002 packet (which is already re-deriving
  corridors)"` (`Sim.Data/content/corridors.json:93`) is stale. It is not edited in this documentation pass; re-point it
  when calibration is next scheduled.

**DW-077 (new, D-067 R13, R14): migration-policy design (entry/exit controls, permits, treaties).**

| ID | Item | Source | Deferred by / when | Destination (verbatim) | Cat | Dir. owed |
|---|---|---|---|---|---|---|
| DW-077 | Migration-policy design: who may cross a polity border, in which direction, under what permission or treaty. It is the policy side of D-021 valve 3, where "closing exits is a *policy choice*" (`../d021-stability-doctrine.md:28`; the valve itself is deferred to M8 in DW-026). Not to be confused with D-021's M5 "policy hooks" (`:39`, `:50`), which are class-mobility (promotion) levers and are not addressed here. Today migration is free between any two settlements across polity lines in every Age, and research effects can only raise movement (`Sim.Core/State/KnowledgeEffects.cs`; operand contract `KnowledgeOperands.cs`), so restriction needs a new mechanism and an ADR in M8. | `d021:28`; DW-026; D-067 §2 | Director 2026-10-28 (D-067 R13) | "M8 (Politics / Diplomacy) owns the future migration-policy design" | SIM/GOV | N (owner ruled; the design is M8's) |

- **Design direction, NOT implemented behaviour** (D-067 R13; M8 starts from it and may change it; content Ages):
  basic entry and exit controls from **A3** (where kingship first appears: `sacral_kingship`, `territorial_kingship`,
  `overkingship`); richer policy options **A5–A7**; entry permits around **A8**; treaty regimes between civilizations
  around **A9**.
- **Misleading node text (D-067 R14):** nodes such as `frontier_fortresses`, `manorialism`, `marches` and `refugee_law`
  describe movement controls the simulation does not have. The text is left for the M8 content/design pass; no migration
  control is implemented in this pass.
- Related row: DW-026 (D-021 valves 2 and 3, the unrest exit, → M8). DW-077 is the policy side of the same exit valve.
