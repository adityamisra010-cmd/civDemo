# CR-022 — THE IDENTITY OF A REPLAYED ORDER'S TARGET AGAINST D-008'S REPLAY RECOVERY PATH

**Status: RAISED 2026-10-18 AND IMPLEMENTED UNDER DELEGATED AUTHORITY (option 2) — AWAITING THE DIRECTOR'S RULING.**
- **Raised under** CLAUDE.md's governance rule ("If implementation reveals a genuine conflict between frozen items …
  write `docs/adr/cr-NNN.md`") while carrying out the Director's 2026-10-18 instruction "Fix the Game: Independent Audit,
  Remediation, and Validated Handoff". That instruction delegated implementation authority ("Use engineering judgment
  within the repository's governing contracts. Where an architectural decision is genuinely required, follow the
  established governance process and document the decision and rationale.") and told the agent not to stop at a report.
- **So the recommended option is built** (D-057; contract ADR-042). Nothing here claims that the Director has approved
  it. If the Director rules for another option, the change is one kernel file (`Sim.Core/Kernel/ReplayProof.cs`) and
  its three callers.

## 1. The frozen items in conflict

- **D-008 (closed): saves may break between milestones; seed + order-log replay is the recovery path.** A session
  recorded by an older build is continued by re-founding its world from the seed and replaying its log.
- **D-052 §13 and ADR-034 S0 (the resume rules built on it):** a session recorded under another schema is resumed by
  that replay, "without the trace check", with a notice that the world may differ.
- **Against them: an order names its targets by id** (`OrderRecord.TargetId`, `ActorId`; the five-field record is
  frozen kernel surface), and an id allocated after turn 0 is the next free one (`ColonizationSystem.Found`: settlement
  max + 1; `RevoltSystem`: roster max + 1). In a replay that has drifted from the recorded game, the same id can name a
  different colony or revolt-born polity.

The recovery path therefore cannot both replay every recorded order and apply each one to the entity it was given for.

## 2. Evidence

- **Reproduced in this build's own rules** (`Sim.Ui.Tests/IdentityDriftResumeTests`, seed 42):
  - a session recorded under another build's siting floor (worldgen.json `siting.scoreFloorPercentile`, a TUNE
    number), labelled v33, its trace in the v33 form;
  - the player's first colony is refused there, so the colony from the second order is settlement 12, and a turn-5
    sector order names 12;
  - this build founds both, the first as 12; replayed without an identity check, the turn-5 order sets the sectors of
    the OTHER colony, which the player also controls, without a word.
- **On real older builds' sessions** (`sim inspect`, each run in this build):
  - a 300-turn, 2-AI v32 session recorded by `de86ace` matches this build's replay in the v32 form through turn 205 and
    differs from turn 206 on;
  - the batch-3 build's v33 fixture (`Sim.Tests/Fixtures/v33-session-q17`) matches through turn 2 and differs from turn
    3, its own documented premise (W = 3);
  - the batch-2 build's v32 fixture (`v32-session-found`, with a kind-9 colony and an order for it) reproduces through
    its last turn, 8.

  Drift between builds is real and common. Before D-057, every order after it was applied to whatever entity held its
  id.

## 3. Options (≤ 3)

1. **Keep D-008 as written.** Replay every recorded order and rely on the notice ("the world may differ"). Silent
   misapplication stays possible. No compatibility cost.
2. **Prove each target's identity, refuse what cannot be proven (RECOMMENDED; BUILT).**
   - Before each recorded batch, every settlement and polity it names must be the recorded one. That holds for ids
     allocated up to the last turn the replay matched the trace, in any readable schema form, and for the founded
     roster by construction.
   - Otherwise the resume or `inspect` is refused, before anything is applied or written, naming the order, the
     turns, and the remedy.
   - D-008 still recovers every session whose orders can be proven.
3. **Carry identity in every order** (the colony's founding cell and turn, the polity's origin) as kind 13 does for
   cancels. This needs new order kinds or a wider record: an order-log wire change. It only helps sessions recorded
   after it ships; old logs would still need option 2.

## 4. Blast radius

- **Unaffected:**
  - sessions of this schema whose trace checks turn by turn: every order is proven;
  - older sessions whose orders name only settlements and polities that existed before the replay first differs (all
    three real sessions above resume or inspect);
  - every CLI bare-log run.
- **Refused, with a diagnostic:**
  - an older session with an order naming a colony or revolt-born polity founded after the replay first differs;
  - an older session whose founding itself differs (turn 0 does not match), if any order names a settlement;
  - a session whose trace file is missing, if any order names a settlement.
- **Code:** `ReplayProof` (new kernel file); `UiSession.ReplayTo` and `Resume`; `sim inspect`. Additionally
  `Cli.LoadOrders` refuses another schema's session log in the bare-log runners (D-057 §4).
- **Hashes, the save layout and the order-log wire format:** unchanged.

## 5. Recommendation

Option 2, as built. It removes the silent misapplication, keeps D-008 for every session whose orders can be shown to
mean what they meant, and needs no wire change. Option 3 is worth adding later for colonies (a founding-cell field)
when an order-log version is next opened; it is a `docs/queue.md` item, not part of this change.
