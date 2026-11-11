# War Sandbox — PROPOSAL (M7 lane D, 2026-11-09)

**Status: PROPOSAL** for the parts the Director delegated or left undesigned; the quoted Director decisions are fixed and are
not reopened. The prototype core is `prototypes/m7-sprint1/Proto.M7/Sandbox/` (isolated; no permanent project, D-079 DR5).

**Authority.** D-079 **DR30** (DIR5 §2.13): modes "Quick Battle, Scenario Editor, Battle Test, Replay and Inspection, later
Campaign Encounter"; "Isolated from campaign population and campaign casualties"; "Unrestricted experimental force creation,
including units unavailable in the campaign"; "Any designed unit family can be instantiated regardless of campaign era";
"Terrain editing is available in the sandbox"; "Loaded campaign encounters inherit campaign terrain. Do not allow campaign
encounter terrain editing"; "AI-vs-AI sandbox battles use AutoResolver. They do not need battlefield rendering"; "Campaign
resolver rules remain unchanged". **DR31** (deployment thirds), **DR32** (asymmetry), **DR33** (snipers; "Sandbox supports
all designed unit families regardless of campaign-era availability"; "Campaign availability remains gated by era and
technology"), **DR7** (one authoritative framework for campaign and sandbox battles), §4 **D15** (sandbox casualty accounting
form, DELEGATED, under Law 1), READINGS **R2** (era = computed Age), **R4** (Battle Test autoplay is AI-vs-AI), **R5**
(deployment zones bounded by the thirds). Lane TR's `DeploymentPhase` (`prototypes/m7-sprint1/reports/H-TR.md` §2, P-TR5),
lane P's AutoResolver (`reports/H-P.md`), lane W's collapse (`reports/H-W.md`).

Labels: **DD**, **READING** (both alternatives), **PROPOSAL**, **MEASURED**, **INFERRED**.

---

## 1. Modes (PROPOSAL, within DR30)

| Mode | What the player does | Rules |
|---|---|---|
| **Quick Battle** | picks forces and a field, deploys, fights or watches | forces from any designed family (§3); deployment through the thirds (§5); AI-vs-AI → AutoResolver |
| **Scenario Editor** | edits terrain cell by cell, places forces, saves a scenario | terrain editing allowed until the deployment phase opens; forces as in Quick Battle |
| **Battle Test** | runs a scenario many times (seeds) with no player, to test units and doctrine | READING R4: its autoplay is AI-vs-AI and so uses the AutoResolver; the resolver-parity battery that runs the TacticalResolver in autoplay is a test harness, not a sandbox battle |
| **Replay and Inspection** | re-runs any finished session from its logs; inspects orders, verdicts and casualties | not a mode with its own rules: an operation on any session (§6) |
| **Campaign Encounter** (later) | loads one battle from the campaign | inherits the campaign terrain (no terrain editing, DR30); never writes back (§2) |

The sandbox never renders an AI-vs-AI battle (DR30); a battle with a player side is the TacticalResolver's, which the graphics
lane renders like a campaign battle (DR7: one framework).

---

## 2. Isolation and the casualty accounting form (D15, PROPOSAL)

**Form proposed: a sandbox-scoped ledger.** Each session owns its own conserved-quantity ledger (the prototype's
`WarLedger` stand-in for the kernel Ledger):

- forces are **minted** by a ledger transfer from the session's own **mint** account (opening balance 10¹², a sandbox-scope
  number, never a campaign quantity) into one account per formation; *verification round 1 of lane CB (CB-R1-02): a force the
  mint cannot cover is refused (`SandboxRefusal.MintShort`), logged, before anything is minted. Under D-082 DA5 a force of any
  size is one formation, so such a force reached the transfer and threw after its formation id and account were taken, outside
  the log (reproduced; pinned by `AddForce_AForceTheMintCannotCover_…` in `Proto.M7.Tests/LaneCB/BattleEntityRuleTests.cs`);*
- a finished battle moves people through **lane W's collapse** — the same casualty mapping the campaign uses
  (`BattleCollapse.Split`, DIR5 §2.9) — into the session's own **sinks**: dead, wounded, captured, deserted; survivors stay in
  their formation;
- so every person is in exactly one account of the session's ledger, the session total equals the mint's opening at every
  stage (exact, Law 1), and **no transfer can reach a campaign account**: the session holds no reference to any campaign world.
  A Campaign Encounter copies a **value snapshot** at load (terrain copy, formation sizes) and never writes back.

Alternatives considered: (b) a "sandbox" flag on campaign accounts (one ledger, tagged entries) — rejected: a single bug in a
tag filter would leak sandbox casualties into the campaign population; (c) no accounting in the sandbox (counts only) —
rejected: the sandbox would not exercise the campaign's casualty mapping, so a sandbox test would prove less.

**MEASURED (tests in `Proto.M7.Tests/LaneD/SandboxTests.cs`):**
- `SandboxAccounting_ConservesExactly_InItsOwnLedger`: the session's people total equals the mint opening before, during and
  after a battle; every formation's casualty row sums exactly to its persons; the sinks equal the row sums.
- `SandboxAccounting_PrisonersAndDeadReachTheirOwnSinks`: a TacticalResolver battle that takes prisoners (a holding force
  whose exit edge is walled off surrenders as it routs) puts them in the captured sink, equal to the battle's surrendered count.
- `CampaignWorld_StateHashIdentical_BeforeAndAfterASandboxBattle`: lane W's campaign world (`OutcomeRig`), run to a War Pulse,
  gives a Campaign Encounter snapshot of two of its formations (3,000 and 2,500 persons); the encounter is fought to the end in
  the sandbox (people die there); the campaign world's **state hash and every ledger total are unchanged**, its formation
  still holds 3,000, and after running on to a later pulse it equals a twin campaign world that never met a sandbox.

*Superseded in part (verification round 1, D-R1-01): the tests above prove that the sandbox totals conserve and that the
campaign is untouched. They did NOT tie the casualty rows to the battle: no test compared a row with the `BattleResult`. Two
wrong mappings passed every test (verifier mutants V9, desertion charged to the side that held the field, and V10, the
routed who escaped also counted as fit, which under-counts the dead). Now pinned:*
- *`SandboxAccounting_MatchesTheBattleResult_PerFormation` (AutoResolver, three seeds with routed escapes on both sides):
  per formation, dead = the battle's killed; captured = its surrendered; fit = survivors − routed escapes; wounded = 0 (the
  AutoResolver has no wounded); deserted = ⌊routed escapes × (MoraleMax − end morale) ÷ MoraleMax⌋ on the side that did NOT
  hold the field and 0 on the side that did; returned = routed escapes − deserted. All of these are computed by hand, without
  calling the collapse.*
- *`SandboxAccounting_PrisonersAndDeadReachTheirOwnSinks` (TacticalResolver) now also checks per formation: dead = killed,
  captured = surrendered, and no deserter on the side that held the field.*

---

## 3. Forces: any designed family, any era; caps (DR30, DR33, DR24)

- **Any designed unit family regardless of campaign era (DD).** The sandbox does not consult campaign availability. The
  prototype can field only the families that have a battle profile today (`SandboxCatalogue`: warband 301, archers 402, and
  lane N's test identities riflemen 4991, sniper team 4990, gun crew 4992); a family without a profile is refused
  (`UnknownFamily`), never invented. Every designed family becomes fieldable as its battle profile is designed (M7.6 / M7.9).
  *Superseded in part (verification round 1, D-R1-09): until round 1 the sandbox fielded the gun crew (4992) WITHOUT its gun.
  Lane N's profile has Ranged 0 because "the gun is a domain weapon given on the formation" (`DomainProfiles.GunCrew`), and the
  sandbox gave none, so its gun crews could not fire (MEASURED, verifier probe P2: 12 gun crews against 60 archers, archers
  killed 0). The catalogue entry now carries lane N's `FieldGun` (the field-artillery weapon its row cites, 805), so each gun
  crew formation is created with that weapon. A formation with a domain weapon is outside the AutoResolver's model, so an
  AI-vs-AI battle with gun crews runs on the TacticalResolver (READING A, §6). MEASURED by
  `GunCrew_IsFieldedWithItsFieldGun_AndFires`: with the gun crews holding (a passive player side), the guns kill archers, and
  the same deployed battle without the guns leaves the archers unharmed. Limitations: (1) lane N's land guns fire only from a
  stationary crew, and the knowledge-only AI advances gun crews. So in an AI-vs-AI sandbox battle the guns never fire
  (MEASURED with a round-1 probe: 12 gun crews against 60 archers fired 0 shots under the AI, against 12 shots and 58 archers
  killed when the crews held). An AI that holds crew-served guns belongs to the AI lanes. (2) One entity per person (lane W's
  land rule), so n gun-crew persons field n guns; no crew-per-gun rule is applied (lane N's entity is "one weapon team").*
- **Placement of a split force (round 1, D-R1-14).** `AddForce` lays the parts out column by column from the anchor,
  `SplitSpacingCells` (4) cells apart. A column holds ⌊grid height ÷ 4⌋ parts, at rows wrapped inside the grid, and each
  further column steps 4 cells toward the side's own exit edge (West for side 0, East for side 1), away from the middle third.
  The force is refused whole (`OutOfGrid`, nothing minted) when a column would leave the field. Before round 1 only the first
  anchor was checked and the parts were stacked 4 cells apart in y. MEASURED (verifier probe P6, reproduced): a 1,000-man
  warband gave 16 of 20 anchors off the 48 × 32 field, and deployment was refused (NotPassable). Pinned by
  `ForceCreation_LargeForce_IsLaidOutInsideTheField_AndDeploys`: the same force now lands in the West third and deploys, and a
  force that cannot fit is refused with nothing minted. Capacity of one call on this field: 8 parts per column; from x = 8
  (side 0) the columns are at x = 8, 4 and 0, so 24 parts (1,200 heavy infantry).
  **[Lane CB, 2026-11-11, (D-082 DA5)]** Superseded: there is no split force any more. A force is one formation fighting as
  `DirectorCaps.EntitiesOf` entities (§4), so the column layout, `SplitSpacingCells` and the refusal of a force that "cannot fit"
  are gone (MEASURED: 1,000 heavy infantry are one formation of 50 entities at the anchor and deploy; 1,250 at (4, 16), refused
  before, are accepted). The pin is now `ForceCreation_LargeForce_IsOneFormation_AndDeploys_TheBattleDoesNotYetScaleEntities_DA5`.
- **Campaign availability stays gated by computed Age (DD; READING R2).** `CampaignAvailability.Available(computedAge, f)` =
  f's identity Age ≤ the polity's computed Age (ADR-031), so Law 4 holds; the knowledge gate of ADR-055 applies on top. MEASURED:
  the sniper team (age 8) is unavailable to a computed-Age-2 polity in the campaign and is fielded in the sandbox
  (`ForceCreation_AnyDesignedFamilyAnyEra_CampaignAvailabilityStillByComputedAge`).

## 4. Caps in the sandbox — READING S1

DR30's "Unrestricted experimental force creation" against DR24's caps:

- **(a) APPLIED:** unrestricted means any family, any era and any number of formations; each formation keeps its DR24 cap. A
  force larger than one formation is created as the fewest formations within the cap, sizes as equal as possible
  (`FormationRoster.SplitSizes`: 130 heavy infantry → 44 + 43 + 43; 5 snipers → 2 + 2 + 1; uncapped gun crews stay one
  formation). Consequence: the sandbox runs the very engine the campaign runs, whose entry check refuses over-cap formations;
  sandbox and campaign results of the same forces are comparable (DR7). MEASURED by `ForceCreation_SplitsAtTheDirectorCap`.
  *Superseded in part (verification round 1, D-R1-03): "each formation keeps its DR24 cap" holds for forces created in the
  sandbox (Quick Battle, Scenario Editor, Battle Test), where the cap counts persons. A **Campaign Encounter** does not split:
  it fields each campaign formation as ONE battle formation of min(persons, cap) entities (lane W's persons-and-entities
  rule). A formation of more persons than the cap is accepted there (MEASURED: 3,000 persons fielded as 50 entities, which
  `DirectorCaps` refuses). The two rules are READING C1 of `docs/m7-plan/caps-and-counted-objects-proposal.md` §2.2. Pinned as
  the stand-in's current behaviour by `CampaignEncounter_FieldsLaneWsFormationsByTheEntityRule_ReadingC1b`.*
- **(b)** caps lifted in the sandbox. Consequence: an override of the battle-entry check (lane N's `ProposedCaps.WithOverride`
  exists for tests), unbounded battle cost, and sandbox results not comparable with campaign battles.

Applied until the Director corrects it (also in `docs/m7-plan/caps-and-counted-objects-proposal.md` §5).

**[Lane CB, 2026-11-11, (D-082 DA4, DA5)]** DA4 confirmed (a): "caps in sandbox apply". DA5 decided what a cap counts:
"Battlefield units." (READING C1 (b)). So (a)'s "A force larger than one formation is created as the fewest formations within the
cap, sizes as equal as possible (`FormationRoster.SplitSizes`: 130 heavy infantry → 44 + 43 + 43; 5 snipers → 2 + 2 + 1 …)" is
replaced: counted in battle entities, the fewest formations within the cap is ONE formation for every class the sandbox fields.
130 heavy infantry are one formation of 50 entities standing for 130 persons; 5 snipers, 2 entities standing for 5; gun crews,
one entity per person as before. The sandbox's force creation and its Campaign Encounter now apply the same rule
(`SandboxSession.Fielded`), so the round-1 note above is resolved: an encounter's formation and a Quick Battle formation of the
same family and persons are the same formation and fight the same battle (MEASURED,
`CampaignEncounterAndForceCreation_FieldTheSameFormation_TheCapCountsBattleEntities_DA5` and
`ForceCreationAndCampaignEncounter_FieldTheSameFormation_ForEveryFamilyAndSize`). `ForceCreation_SplitsAtTheDirectorCap` is now
`ForceCreation_IsOneFormation_WithinTheCapInBattleEntities_DA5`. **Consequence until the battle-entity design is built
(`docs/m7-plan/battle-entity-proposal.md` §3):** an entity fights as one person whatever its share, so a sandbox force larger
than its cap fights no harder than one of exactly its cap (MEASURED: 1,000 against 1,000 is the battle of 50 against 50; only the
casualty rows, 20 persons per entity, differ). A user who wants every person to fight as one entity creates several formations
(one `AddForce` each).

---

## 5. Terrain and deployment

- **Terrain editing (DD):** cell class and height, in every mode except a Campaign Encounter (`TerrainLocked`); also refused
  once the deployment phase is open (the thirds are judged on a fixed field) and after the battle. The encounter's grid is a
  copy, so not even the campaign's own grid object can be reached (MEASURED: `TerrainEditing_…`).
  *Superseded in part (verification round 1, D-R1-10): the lock existed only at the API level. The public `Terrain` property
  returned the session's live grid, so "locked" terrain could be written, without a log entry, and a replay then built a
  different field (MEASURED, verifier probe P3: encounter cell (20, 16) Forest after a refused `EditTerrain`; the replay's cell
  Open). The deployed setup handed out the same grid object. Both now return copies, so the session's grid is changed only by
  logged edits. Pinned by `Terrain_IsHandedOutAsACopy_SoTheLockAndTheReplayHold`. The inspection surface still hands out the
  session's `Ledger` and its `DeploymentPhase`, which accept writes. These were not reported and are not changed here; see
  §7.*
  *Superseded (verification round 2, D-R2-06): both are now copies, and so is the battle result (§6, Replay and Inspection).*
- **Deployment (DD DR31; READING R5; lane TR P-TR5):** always through lane TR's `DeploymentPhase`. Each army deploys in its own
  third; the middle third is undeployable; edits are judged on the edited formation; `Close` requires a valid whole deployment
  and yields `Deployment = Thirds`; the battle cannot start before (`NotDeployed`). The editor's "deployment zones" are the
  thirds (R5): no other zone exists. MEASURED by `Deployment_AlwaysThroughTheThirds` (middle third and enemy third refused with
  lane TR's own codes, recorded in the session log).
- **Default objectives (PROPOSAL):** at `Close`, each side's battle objective is the integer mean anchor of the enemy's deployed
  formations, so both the knowledge-only AI and the AutoResolver doctrine advance to contact (without objectives both hold,
  and the battle ends by the inactivity rule with no fighting — MEASURED during this lane's development: the AutoResolver
  battles ended with no dead before this default existed). The Scenario Editor may later set objectives explicitly.

### 5.1 The two shipped scenarios that violate the thirds (DR32; PROPOSAL only, NOT implemented)

Lane TR found that the parity `Ambush` (its ambushers stand in the middle third) and lane N's `Amphibious` (shore formations
in the middle third) violate DIR5 §2.14 and run only because `BattleSetup.Deployment` defaults to Unrestricted
(`reports/H-TR.md` §7 "[r1, V8]", P-TR13). Proposal for offering them in the sandbox without normalizing them (DR32: "Preserve
realistic geographical and tactical asymmetries"):

1. **As authored, in Battle Test only, labelled.** Battle Test may load an authored scenario with its own start positions,
   marked "authored start, not a DIR5 deployment" in the session log and in every result row; positions are passed to the
   battle unchanged (no snapping, no mirroring). They are never offered in Quick Battle or the Scenario Editor, and never
   enter the parity battery's acceptance claims as DIR5-legal deployments.
2. **As a DIR5-legal variant, beside it.** The same terrain and forces, re-deployed inside the thirds by the scenario's
   author, kept as a SECOND scenario — the asymmetry is preserved by the terrain and the force mix (the ambush cover, the
   beach), not by an illegal start; per-side results reported for both (lane TR's D13 reporting).

Relation to lane TR's P-TR13 ("Re-anchor … inside their own thirds, or label both 'Unrestricted probe' scenarios"): option 1
is P-TR13's labelling, confined to Battle Test; option 2 is its re-anchoring, kept as a separate scenario so the authored one
is not overwritten. Both leave lane P's and lane N's pins untouched until those lanes or the integrator act.

What this proposal does not do: change either shipped scenario, or decide whether an ambush may begin inside the middle third
as a special deployment rule — that would amend DR31 and is the Director's.

---

## 6. Battles, resolvers, replay and inspection

- **AI-vs-AI (DD):** both sides AI → lane P's default policy (`ResolverPolicy.Default`): the **AutoResolver** whenever every
  formation is inside its model (lane P's **model 2, as it stands on the branch; its acceptance is OPEN, D-079 Q4**: lane P's
  battery passes 16 of 29 scenarios, `reports/H-P.md` §0); otherwise the TacticalResolver, headless — no rendering either way.
  MEASURED: the sandbox result equals a direct `AutoResolver` run of the same deployed setup, byte for byte
  (`AiVsAi_UsesTheAutoResolverModel2_Unchanged`): the sandbox changes no resolver rule (DR30).
  *Superseded in part (verification round 1, D-R1-12): the "(DD)" label covered more than the decision. DR30 says only
  "AI-vs-AI sandbox battles use AutoResolver. They do not need battlefield rendering." The fallback to the TacticalResolver for
  forces outside the AutoResolver's model is not in DR30; it is lane P's default policy, applied here as **READING A**:*
  - *(a) APPLIED: lane P's `ResolverPolicy.Default`. An AI-vs-AI battle with any formation outside the AutoResolver's model
    (domain weapons, vessels, aircraft, embarked troops) runs on the TacticalResolver, headless. Consequence: such sandbox
    battles run, but not on the resolver DR30 names. Since round 1 this fallback really runs: gun crews carry their
    `FieldGun` (§3).*
  - *(b) The literal DR30 reading: refuse an AI-vs-AI sandbox battle that the AutoResolver cannot model, until its model
    covers those forces. Consequence: gun-crew, naval and air forces could be tested in the sandbox only with a player side.*
  *Flagged for confirmation (§8).*
- **A player side:** the TacticalResolver; the player's orders arrive per pulse through the battle's order path (a
  `ScriptedController` in the prototype).
- **Replay and Inspection:** the session logs every edit, accepted or refused (`SandboxEditRow`: terrain edits, forces,
  deployment edits with lane TR's codes, start, resolver used), and the battle keeps its own order log. `SandboxSession.Replay`
  rebuilds the session from the INITIAL terrain, seed, rules and encounter snapshot, re-applies every edit in order and
  re-runs the battle with each player side's recorded orders.
  - **Inspection surface (verification round 2, D-R2-06):** every object the session hands out whose writes could reach the
    session's state or hash is a copy, so only logged edits reach the session and the replay can reproduce it (the result's lane
    N personnel report, which neither the session's state nor its hash uses, is shared): the terrain, the formations and
    the deployed setup (since round 1); since round 2 also the ledger (the same accounts in a new ledger), the deployment phase
    (rebuilt edit for edit on a detached copy of its base setup and closed where the session closed it, so a proposal tried on
    it gets lane TR's real verdict: a what-if that changes nothing) and the battle result (its formation array copied). Before
    round 2 these three were live (MEASURED, reproduced: verifier probe P_D byte for byte, an unlogged `Propose` deployed a
    formation at (50000, 90000) in the session and at (170000, 330000) in its replay; this round's probes: an unlogged ledger
    transfer of 100 persons made a formation fight with 140 in the session and 40 in the replay; a write into the result's
    array changed the session hash). Pinned by `InspectionSurface_HandsOutCopies_OfTheLedgerTheDeploymentPhaseAndTheResult` and
    `Deployment_IsHandedOutAsTheSessionsPhase_EditForEdit`.
  - **Hash pin (MEASURED):** `Replay_ReproducesAWholeSession_BitForBit` — a session with terrain edits, a refused edit, split
    forces, refused and accepted deployment edits and an AI-vs-AI battle replays to the same session hash (log, casualty rows,
    every ledger balance, the result hash); a different seed gives a different hash. An encounter session replays too.
    **[Lane CB, (D-082 DA5)]** "split forces" is now five formations created explicitly at the anchors the split gave (one
    `AddForce` each); the battle is the base's (its result hash, MEASURED on `0d2db74`, is pinned).
  - **Turn-exact order-delivery pin (MEASURED):** `PlayerBattle_OrderDeliveryPin_AndReplay` — an order submitted for pulse p
    with step offset k is delivered at StartMs + p·PulseMs + k·StepMs exactly (pulse 0 offset 0, pulse 2 offset 3), an offset
    outside the pulse is refused `BadStep` at that pulse's start, and the replay delivers every order at the same pulse and
    instant with the same verdict, trace hash and state hash.
- **Rows (MEASURED):** `SandboxEditRow` (50 bytes) and `SandboxCasualtyRow` (68), `SandboxCodec` version 1, populated-table
  round trip from a real session (`Codec_PopulatedTables_RoundTripBitExact`).
  *Superseded in part (verification round 1, D-R1-07): that session was an AutoResolver battle whose casualty rows had
  Wounded = Captured = 0 in every row (verifier probe P4), so an encoder that swapped the two fields round-tripped (verifier
  mutant V12 survived). The casualty table is now taken from a TacticalResolver battle that leaves wounded and prisoners. The
  test also asserts, for every pair of same-typed fields, that some row tells them apart.*

---

## 7. Limitations

1. Only five battle profiles exist (two data identities, three lane N test identities); most designed families cannot yet be
   fielded. Naval and air forces are not offered by the prototype's `AddForce` (lane N's modules exist; the sandbox wiring for
   them is not built).
2. The Campaign Encounter is a stand-in: lane W's campaign world has no battle terrain, so the test supplies one; a real
   encounter takes the campaign map's battle terrain (D-012 grid, M7.7).
3. Battle Test's multi-seed runs and the inspection user interface are not built (the session and its logs are).
4. Objectives are a default only; the editor cannot set them yet.
5. AutoResolver acceptance is OPEN (Q4): AI-vs-AI sandbox results inherit the AutoResolver's measured parity gaps.
6. (Round 1.) The Campaign Encounter applies lane W's entity rule, while the sandbox's own force creation applies the persons
   rule (READING C1; `docs/m7-plan/caps-and-counted-objects-proposal.md` §2.2). M7.6 must reconcile them.
   **[Lane CB, (D-082 DA5)]** Reconciled: both apply the one rule (§4). The new limitation is the battle-entity gap of §4: a
   formation above its cap fights as its cap of one-person entities until `docs/m7-plan/battle-entity-proposal.md` §3 is built.
7. (Round 1.) Gun crews field one gun per person; no crew-per-gun rule is applied (§3).
8. (Round 1, observed while fixing D-R1-10, not reported, not changed.) The session's `Ledger` and `Deployment` properties
   hand out live objects that accept writes (a ledger transfer, a `DeploymentPhase.Propose`) outside the session log. A
   replay would not reproduce such a write. A read-only inspection surface is the remedy (PROPOSAL).
   *Superseded (verification round 2, D-R2-06): fixed by copies (§6), the battle result included. Not changed: the three logs
   (`Log`, `Casualties`, `BattleOrders`) are the session's own lists handed out as `IReadOnlyList`, which has no write member;
   only a downcast to the list type could write them.*

---

## 8. For the Director

1. **READING S1** (§4): caps per formation in the sandbox (applied) or lifted. **[Lane CB: answered (a) by D-082 DA4.]**
2. **The two thirds-violating scenarios** (§5.1): whether option 1 (authored, Battle Test only, labelled) is acceptable, or only
   option 2 (a DIR5-legal variant). An ambush start inside the middle third as a rule would amend DR31 and is not proposed.
3. **READING A** (§6; added in verification round 1): an AI-vs-AI sandbox battle that the AutoResolver cannot model falls back
   to the TacticalResolver, headless (applied), or is refused until the AutoResolver covers those forces.

The accounting form (D15) is delegated and is presented here for review with M7.18.
