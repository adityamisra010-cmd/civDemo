# ADR-051 — The capped technology-lane RP reserve (switch, shipped OFF)

**Status: PROPOSED — implemented behind `research.json tuning.researchReserve`, absent from the shipped content, so OFF.**
Director's ruling of 2026-10-26: "Approve implementation behind switches, with route overflow and the three-turn reserve
enabled together only in the trial configuration. Keep the default configuration unchanged until the new four-seed pacing
baseline has been reviewed." Also: "Do not let RP pay for civics or combine RP and CP." "If no eligible goal exists, use the
capped reserve rule." "[Do not introduce] an unlimited reserve." The design and its measurements are
`docs/m6-plan/knowledge-redesign/research-cost-pass/empty-lane-reserve-proposal.md` (§3 evidence, §4 rule, §5 UI). With the
switch on, this ADR amends D-044 R20-D ("RP is per turn and never stored") for the one case it names: the technology lane of
a step that funds nothing. Switching it on in the default configuration is a separate Director decision.

## 1. The rule (switch on)

`"researchReserve": { "capTurns": 3.0, "drawTurns": 1.0 }` (validated: both finite and > 0; absent = off). `ResearchTuning.Reserve`
(`ResearchReserveRule(CapTurns, DrawTurns)`). Every quantity below is in integer milli-RP; RP → milli-RP rounds DOWN
(`ResearchQuery.ToMilliRp`). "Pool" is the holder's RP of the step (`ResearchQuery.HolderResearchPoints`: the polity pool, or
a city state's `CityStateResearchPoints`), read from PREV. All of it is in `ResearchSystem.Step`, step 3, for every holder in
the fixed holder order (roster polities, then city states).

1. **Deposit.** A step whose technology lane funds nothing (`fund[0] < 0`: no destination and no assist, or a destination
   none of whose route nodes is fundable — the waiting lane) deposits `ResearchQuery.ReserveDepositMilli` =
   min(floor(1000 × pool), floor(1000 × capTurns × pool) − reserve), or 0 if the reserve is at or above that cap. The rest of
   the step's RP is lost, as before. A reserve above the cap (the pool shrank) is not trimmed; it only stops receiving.
2. **Draw.** A step whose technology lane funds one of its OWN technology nodes — its destination or a node of its route; not
   a free-lane assist (`LaneFunding.Assisting`), never a civic (the one-pool model's civic target) — draws
   `ResearchQuery.ReserveDrawMilli` = min(reserve, floor(1000 × drawTurns × pool)). The whole draw leaves the reserve and is
   added to the step's points; they are spent by the pool's own rules: one node per step with overflow off; along the route
   with `tuning.researchOverflow` (ADR-050). The pool is spent first, the draw last.
3. **Lossy.** What the funding cannot absorb is lost — the draw is never returned to the reserve. Banked RP is therefore never
   more efficient than the step's own RP (the proposal §3 finding 2: EXACT draws reward idling).
4. **No assist, no civic.** With overflow on, if the points move on to an assist node or a civic, the unspent draw (spent
   last) is dropped at that point; the pool's remainder continues as before. The reserve never pays an assist or a civic.
5. **Banks stay separate.** The CP bank (ADR-043) is never read or written by the reserve; RP and CP never mix. The CP
   identity `Balance == Generated − Spent` holds.
6. **Everyone.** Players, AI empires and city states alike (city states: their own `CityStateResearchPoints` pool).
7. **Determinism.** Read from PREV, integer milli-RP, fixed holder order, no RNG, no dictionary iteration.

Code points: `ResearchSystem.Step` step 3 (`// ADR-051 DEPOSIT`, `// ADR-051 DRAW`, the dropped draw after `NextFunded`, which
now reports `out bool assist`), `ResearchSystem.SetReserve`, `ResearchQuery.ReserveDepositMilli / ReserveDrawMilli /
ReserveCapMilli / ReserveMilli / ToMilliRp / HolderResearchPoints`.

## 2. Schema v37, section 68

`ResearchReserveRow(PolityId Polity, long ReserveMilli)` — 12 bytes. One row per holder that has ever deposited (it stays,
possibly at 0); strictly ascending Polity (inserted by shifting, never a swap — the CivicsPoints precedent), so the stream
depends only on the set of holders. Appended after section 67 (ResearchLevels); no existing row changed width. Readable set
{32, 33, 34, 35, 36, 37} (`CanonicalSchema.IsReadable`; `BytesAppendedSince`, `WorldHash.ComputeHexAsSchema`, `ReplayGuard`,
`Snapshot.Load`, `SaveFiles.MigrationNote`, `SnapshotDiff.LayoutFor`). A v36 stream stops after section 67 and loads with an
EMPTY reserve table — exact, because no reserve existed under v36. `Snapshot.Load` refuses a reserve table no rule could
write (unsorted or duplicate Polity, a negative reserve: `ResearchQuery.ReservesInvariantViolation`).

**Goldens.** With the switch off no reserve row is ever written, so every world is its v36 stream plus one empty count prefix
(4 bytes); raw stream hashes move for the layout alone. `SchemaProjection.V36` (strip section 68, assert it empty, drop its
prefix) and `WorldHash.ComputeHexAsSchema(world, 36)` reproduce every v36 pin byte for byte —
`Sim.Tests/Kernel/ResearchReserveLayoutAttributionTests.cs`.

## 3. UI (proposal §5)

Read-only `ResearchQuery.Reserve(world, content, holder)` → `ReserveStatus(Pool, ReserveMilli, CapMilli, Action, AmountMilli)`
(null with the switch off) classifies the next step exactly as the step does (deposit / draw / none) and carries the one
wording: "reserve Y of Z RP"; for a lane funding nothing "X RP go to the reserve (Y of Z)" or "reserve full - X RP unused this
turn"; for a drawing lane "+W RP from the reserve this turn · reserve Y of Z RP". It appears on the Glass Box research card,
the action surface's research line, the status band ("… - reserve Y/Z", "+W reserve"), and the progression screen (Technology
stats "Research reserve" / "Reserve this turn", the idle capsule). RP are per turn (ADR-030), so no reserve figure depends on
the next step's dt. Civics lines are unchanged ("+X CP a turn · Y CP banked"). With the switch off every surface is unchanged.

## 4. Evidence

- `Sim.Tests/Systems/ResearchReserveTests.cs` (21 rig tests): deposit on an idle and on a waiting lane; saturation and the
  partial room; a twelve-step wait pinned at the cap; draw at most one pool a turn; LOSSY (2 Rm left, not 2.5 Rm); no draw
  for an assist or a one-pool civic; the first turns after a civic gate and after a real Age-sweep gate; repeated gates
  (the reserve refills); overflow carrying the draw along the route, and dropping it at an assist; the CP bank and civic
  progress byte-identical to the switch-off twin every step; a turn-exact pin (an order stamped turn 2 draws in the step
  2 → 3); save/load mid-run; replay from the order log; loader validation.
- `Sim.Tests/Kernel/ResearchReserveTrialTests.cs`: the trial configuration (overflow + reserve 3/1) on the founded seed-42
  world with one AI, 120 turns: per-step deposit identity for the idle player, cap respected, replay equal every turn,
  save/load at turn 60 (non-zero reserve) continues to the same hash.
- `Sim.Tests/State/ResearchReservesSchemaV37Tests.cs`: populated table (exact ExpectedLength, field offsets, bit-exact
  round trip, hash equality, snapshot re-save), the v36 → v37 migration, the readable set, the Load refusals.
- `Sim.Tests/Kernel/ResearchReserveLayoutAttributionTests.cs` (+ the heavy worlds where they already run): every re-pinned
  golden's v36 projection, and `WorldHash.ComputeHexAsSchema(world, 36)`, equal its previous pin.
- `Sim.Ui.Tests/ResearchReserveDisplayTests.cs`: each surface shows `ResearchQuery.Reserve`'s figures, and the next step is
  measured to deposit / keep / draw what the line says; with the switch off no surface carries a reserve.
- Mutants (each run bounded at 300 s, about 70 × the 4.3 s clean wall time of the rig class; none hung), every one killed
  by at least one semantic rig test: cap ignored (4 tests, e.g. `LongWait_…`); draw limit ignored (9, e.g.
  `Draw_IsAtMostOnePoolATurn_…`); EXACT draw, the unabsorbed part refunded (3, e.g. `Draw_IsLossy_…`); drawing for an assist
  (`NoDraw_WhenTheLaneAssistsACivic_…`); drawing for a civic target (the same test, one-pool arm); the overflow assist
  drop removed (`WithOverflow_TheDrawNeverPaysAnAssist_…`).

## 5. Not decided here

- Turning the switch on (with `researchOverflow`) in the default configuration, after the four-seed pacing baseline of the
  trial configuration has been reviewed.
- The sweep wait under overflow (proposal §6 decision 3): a 3-turn cap does not cover a ~20-turn wait.

## Combined trial (2026-10-26)

Measured together with the other two switches of the Director's 2026-10-26 trial configuration (overflow + capped
reserve {3, 1} + gate-aware goals), against the new default baseline (CR-025 content):
`docs/m6-plan/knowledge-redesign/research-cost-pass/combined-trial.md`.

| Measure | Default | Trial configuration |
|---|---|---|
| RP lost | 55.7 % | 49.5 % |
| Technology waiting steps | 66 | 0 |
| Casual player's lag (Age 9) | +36 turns | +2 turns |
| Deliberate idling (Age 9) | — | −58 turns (no advantage) |
| Age 9 entry | — | 76–98 turns sooner |

Replay and save/load are exact. **Status:** still PROPOSED and OFF by default. Adoption and the pacing recalibration
wait for the Director's review of the combined baseline.
