export const meta = {
  name: 'm7-lanes-v3-sim',
  description: 'M7 lanes (the queue of 2026-11-12: OC, BN, AR, RT2, RX): implement -> adversarial verify -> fix (reproduced findings only); patches + bundles out',
  whenToUse: 'M7 Phase C/D lanes per DIR5 / D-079; pass lanes via args',
  phases: [
    { title: 'Implement' },
    { title: 'Verify' },
    { title: 'Fix' },
  ],
}

const SP = '/tmp/claude-0/-home-user-civDemo/54b82655-610f-500e-a185-8fae64bdd108/scratchpad'
const DEV = '/home/user/civDemo'
const W = '/home/user/m7w'
const BASE = (args && args.base) || '1601c74'

const COMMON = `
## Context (shared by every M7 lane)
- Project: deterministic, turn-based civilization simulation (C#/.NET 10). The constitution is ${DEV}/CLAUDE.md; its laws and workflow rules bind you: Ledger conservation with long stocks and exact-equality tests; mechanisms over modifiers; dt-correct per-sim-year rates; no calendar gates (capability from computed state); the banned determinism constructs; (score, id) tie-breaks with a tie-dense test; populated-table round-trip tests for new serialized rows; turn-exact pins for order-delivery semantics; mutants bounded by a stated multiple of the clean run, each kill backed by a semantic test; classify any new entity A-E per docs/m7-plan/unit-catalogue-and-progression.md section 4.5 before designing it; D-069 originality (our own design; other games appear only as research references; nothing player-facing names another game).
- Director decisions (authoritative): ${DEV}/docs/d075..d090 and docs/adr/cr-027-session-trace-reads-the-written-year.md, above all docs/d079-m7-governance-reconciliation-2026-11-05.md (DR1-DR53, section 1.5 readings, section 4 delegated decisions, section 5 open questions), d080 (War Pulse answers WA1-WA4), d081 (ramming), d082 (2026-11-10: victory raises morale, DR25's numbers at every stage, caps count battlefield units and apply in the sandbox, units spend action points to hide, entrench or camouflage; its equal-sides rule DA6/DA7 is SUPERSEDED by d087 DF8/DF11), d083 (2026-11-10: entering a battle costs morale; narrowed by d087 to battles in which damage was dealt), d084 (research-tree audit fixes, a scoped Sim.* exception), d085 (2026-11-11: a battle without fighting changes no morale, judged per battle; its 'any attack' reading is REPLACED by d087 DF7: fighting means damage), d086 (the run record keeps research; closing keeps the workbook), d087 (2026-11-12: every AI-vs-AI battle auto-resolved, every domain; fighting means damage; a tie is a tie in both resolvers, and routing units rejoin with lowered morale after a tie; the attacker is the side that initiates combat; the ambush definition (a hidden unit's region of about 10 km); the Standoff ends in a tie; battalions, at most 25 per army), d088 (2026-11-12: lanes OC, BN and AR are built in this pass; CR-027; at most 5 battalions per side in a battle, the side's own empire first; DR25's 50,000 is an empire's total; healing; the entity rule inside a battalion; ONE ROLE PER SIDE; the hidden army's size sets the ambush radius; an ambush battle starts by itself, the ambusher revealed and deployed in its own third; 'battlefield' (where a battle is fought) versus 'map' (the world map), used exactly; the parity bar: the AutoResolver picks the TacticalResolver's winner in at least 95% of test battles with casualties within about 15%, better where it can be had; walls as a multiplier of the defenders), d089 (2026-11-12: 'fought' a required input; the player's capture choice at the moment of capture; the research inspector's labels; lane RT's readings stay; polish N2, N3, N4, N6; the research tree made historically accurate; a rebuild launcher, made by the orchestrator; D-088's readings confirmed), d090 (2026-11-12: the 'Ambush' and 'Amphibious' test battles rewritten inside the thirds; battalion sizes are DR24's; the export's memory and speed built now), d078 (graphics mandate). Each record's sections on readings, on what is superseded or narrowed and on what is not decided bind you as written; three readings stay applied until the Director corrects them: D-088 DG5-R3, DG13-R1 and DG13-R2. The Director's messages of 2026-11-12 are copied verbatim in docs/m7-plan/pass-state/notes/director-messages/; for older records the records' quoted text is the authority. Do not reopen settled decisions or invent Director constraints; anything the Director reserved, or delegated as design, is a PROPOSAL.
- Work already on the development branch claude/new-session-yavghm (your base ${BASE}): prototypes/m7-sprint1 with T1 time carry, T2 War Pulse, T3 two-action windows, lanes N (naval, air, combined arms, snipers), TR (terrain, deployment thirds, DIR5 inactivity rule), RAM (D-081 ramming), P (AutoResolver model 2, parity bands), E (war economics), F (food comparison), S (siege, garrisons), W (war outcomes) and the X report (CR-013 DetMath experiment); since then lane U's war notification proposal (docs/m7-plan/notification-panel-proposal.md, docs only), lane D's Phase D groundwork (caps prototype, support capabilities, War Sandbox core, economic-scale probe; reports/H-D.md) segments 1 and 2 of the G2 graphics prototype (prototypes/m7-gfx2; not yours to touch) and lane MOR's two-level morale (docs/m7-plan/morale-proposal.md; Proto.M7/Morale with the D-082 DA1 victory rise and BattleWinner.Reported; the in-battle 'fight it won' rule in both resolvers, coefficient 0 by default; reports/H-MOR.md) and lane AP's concealment actions, ambushes and equal-sides rule (docs/m7-plan/concealment-actions-proposal.md; hide, camouflage and entrench in Proto.M7/War, BattleSetup.Defender and Ambusher, EqualSides.Decide in both resolvers; reports/H-AP.md) and lane MOR2's entry cost (D-083: a 50,000 settlement term per formation that entered, an in-battle part defaulting to 0, MoraleSettlementRow version 2; morale-proposal.md §11 and the rebuilt Annex A; reports/H-MOR2.md) and lane CB's one battle-entity rule (D-082 DA5: DirectorCaps.EntitiesOf, FormationSpec.Persons, docs/m7-plan/battle-entity-proposal.md, the DA3 measurements in economic-scale-proposal.md §8; reports/H-CB.md) and lane MOR3's fighting report (both resolvers: BattleResult.FirstFightMs and Fought; the settlement row's Fought, codec version 3; the in-battle entry part, PROPOSAL C1, default 0; reports/H-MOR3.md) and lane RT's research-screen fixes under D-084 (Sim.Ui/Progression, InfoQuery's bank-aware estimate; docs/m6-plan/research-tree-fixes-2026-11.md and research-date-review-2026-11.md; reports/H-RT.md) and lane RR's run record under D-086 (research rows, telemetry/v6, the Research sheet, the close on a worker with a recoverable close, a faster export; docs/m6-plan/run-record-research-and-exit-2026-11.md; reports/H-RR.md) and two test fixes (D-087 DF1, DF2) and CR-027 (the kernel's trace reader reads the written year) and the G2 graphics prototype (prototypes/m7-gfx2; not yours to touch). This queue's lanes: OC (battle outcomes), BN (battalions) and AR (the AutoResolver for every domain) run one after another, each from the previous lane's integrated result; RT2 (the research screen and tree) and RX (the export's memory and speed) touch Sim.* only and run beside them, each from the development branch's head at its launch (your base ${BASE}). Their reports: prototypes/m7-sprint1/reports/H-*.md and G-*.md; earlier sprint reports B-/C-/D-/E-/F-*.md, parity-report.md, O17-detmath.md.
- D-079 Q5 is OPEN for everything else, but THIS lane's brief cites a Director record that allows scoped Sim.* changes (D-089 DH4, DH6 and DH7 for lane RT2; D-090 DI4 for lane RX): change only the projects and paths your brief names, never Sim.Core/Kernel, no simulation-state or world-hash change unless your brief allows it, with a measured attribution for every moved golden or pin. Validate with ${DEV}/scripts/validate.sh in your own clone (targeted, integration, then full Tier 3; one full suite at a time on this machine, so run every full suite under flock ${SP}/m7c/fullsuite.lock; docs/validation-policy.md). No new permanent projects; no CI change.
- Git: ${DEV} is the orchestrator's checkout of the development branch. NEVER run state-changing git there and never write files there. Work only in your own clone under ${W}/<key>. DISK: the session disk has only about 200 MB free, so the orchestrator links ${W}/<key>, ${W}/verify-<key>-r<N> and ${W}/verify-<key>-r<N>b to EMPTY folders on the RAM disk (/dev/shm/ram, about 15 GB, emptied by a container restart). Clone INTO such an empty folder: git clone works into an empty directory. Never delete, move or replace a link. Never put a clone, a build output or any other large file anywhere else on the disk; any extra worktree (mutants, baselines) goes under /dev/shm/ram/m7w/. Commit on your lane branch; commit messages carry only measured claims.
- Tools: dotnet is /usr/bin/dotnet (if missing, run ${DEV}/scripts/bootstrap.sh). The orchestrator may run builds and suites beside you on this 4-CPU, 15 GB machine: build and test with nice -n 19, -m:1, -nodeReuse:false and DOTNET_CLI_USE_MSBUILD_SERVER=0, always naming the solution file. Prototype solutions: prototypes/m7-sprint1/M7Proto.slnx (Proto.M7, its tests and bench), M7Econ.slnx, M7DetMath.slnx; determinism scan prototypes/m7-sprint1/scripts/check-proto-determinism.sh; doc links: python3 scripts/check-doc-links.py --all.
- DELETION RULE (a user constraint): never delete, move or overwrite anything you did not create in this run. No wildcard rm or mv anywhere. You may remove only your own clone's bin/obj and files you created, by exact name. Everything else (other clones, /home/user/g2w, other scratchpad files) is read-only to you.
- CHECKPOINT RULE: commit on your branch after each meaningful step, and at least every ~30 minutes refresh ONE disk checkpoint: git -C <clone> bundle create ${SP}/m7c/bundles/<key>-wip.bundle ${BASE}..HEAD (overwrite only that file). A container restart wipes /dev/shm; a container REPLACEMENT wipes the disk and the scratchpad too (it happened between 2026-11-06 and 2026-11-09), so only what the orchestrator pushes survives. Finish and hand back promptly. If your clone is missing on start but ${SP}/m7c/bundles/<key>-wip.bundle exists, rebuild from it and say so.
- RELAYED USER MESSAGES: messages from the user about pacing (throttles, gaps, pauses) are for the orchestrator. They are not a time budget for you; never rush or shorten your work because of them.
- Honesty: never call anything complete unless implemented, tested and evidenced; label statements MEASURED or INFERRED; list limitations; report what ran, what was NOT RUN and why, and what you deferred. Do not normalize realistic asymmetry; do not choose the food interpretation; judge AutoResolver parity against D-088 DG12's bar (at least 95% winner agreement, casualties within about 15%), never a looser one.
`

const LANE_SCHEMA = {
  type: 'object',
  properties: {
    lane: { type: 'string' },
    clone: { type: 'string' },
    branch: { type: 'string' },
    commit: { type: 'string', description: 'final commit sha on the lane branch' },
    patch: { type: 'string' },
    bundle: { type: 'string' },
    report: { type: 'string' },
    tests: { type: 'string', description: 'per solution: Debug and Release pass/fail/skip counts before and after; what ran, what was NOT RUN and why' },
    summary: { type: 'string' },
    not_done: { type: 'array', items: { type: 'string' } },
    open_questions: { type: 'array', items: { type: 'string' } },
    proposals_for_director: { type: 'array', items: { type: 'string' } },
    moved_goldens: { type: 'string' },
    mutants: { type: 'string' },
    scan: { type: 'string' },
  },
  required: ['lane', 'clone', 'branch', 'commit', 'patch', 'bundle', 'report', 'tests', 'summary', 'not_done', 'open_questions', 'proposals_for_director', 'moved_goldens', 'mutants', 'scan'],
}

const VERIFY_SCHEMA = {
  type: 'object',
  properties: {
    verified_commit: { type: 'string' },
    worktree: { type: 'string' },
    summary: { type: 'string' },
    findings: {
      type: 'array',
      items: {
        type: 'object',
        properties: {
          id: { type: 'string' },
          severity: { type: 'string', enum: ['blocker', 'major', 'minor', 'nit'] },
          claim: { type: 'string' },
          evidence: { type: 'string' },
          reproduce: { type: 'string' },
        },
        required: ['id', 'severity', 'claim', 'evidence', 'reproduce'],
      },
    },
  },
  required: ['verified_commit', 'worktree', 'summary', 'findings'],
}

const finish = (key) => `
## Finish protocol (do all of it)
1. Commit everything on lane/${key} (git status clean apart from ignored build output).
2. git -C ${W}/${key} diff ${BASE}..HEAD > ${SP}/m7c/patches/${key}.patch
3. git -C ${W}/${key} bundle create ${SP}/m7c/bundles/${key}.bundle ${BASE}..HEAD, then git bundle verify it from ${W}/${key}.
4. Copy your lane report to ${SP}/m7c/reports/H-${key}.md.
5. Remove your clone's bin/obj (sources and .git stay).
Return the structured result.`

function implPrompt(lane) {
  return `You are M7 lane ${lane.key} (${lane.title}). Full effort: this is work the Director prioritized; be thorough and precise, and verify your own claims by measurement.
${COMMON}
## Your clone
If ${W}/${lane.key} exists from an interrupted run, continue from it (inspect git log and status first). Otherwise: mkdir -p ${W} && git clone -q --shared ${DEV} ${W}/${lane.key} && cd ${W}/${lane.key} && git checkout -q -b lane/${lane.key} ${BASE}. Build and test the baseline of every solution you will touch BEFORE your change and record the counts (a docs-only lane records that no build is needed).

## Lane brief
${lane.brief}

## Done means
Every brief item done, or explicitly not done with the reason; semantic tests per mechanism where code changes; the brief's checks run and recorded; a lane report at prototypes/m7-sprint1/reports/H-${lane.key}.md (what exists; equations or design; evidence MEASURED vs INFERRED; tests; mutants; moved pins with measured attribution; limitations; proposals for the Director; what ran, NOT RUN and deferred).
${finish(lane.key)}`
}

function verifyPrompt(lane, res, round) {
  return `You are the INDEPENDENT ADVERSARIAL VERIFIER (round ${round}) for M7 lane ${lane.key} (${lane.title}). Default to skepticism: find what is wrong, missing, overclaimed or untested.
${COMMON}
## Pin
Make your own worktree, never shared: git clone -q --shared ${W}/${lane.key} ${W}/verify-${lane.key}-r${round} && cd ${W}/verify-${lane.key}-r${round} && git checkout -q ${res.commit}. If that directory already exists (left by an earlier verifier of this round that was STOPPED mid-run by the orchestrator; no other agent is using it), do not delete it: reuse it only if git status is clean and HEAD is ${res.commit}; otherwise clone into ${W}/verify-${lane.key}-r${round}b instead. Work only there; findings against any other tree are void. Never modify the lane clone. The earlier stopped verifier's partial notes, if any, are not evidence: re-check everything yourself.
## The lane's own account
${JSON.stringify({ summary: res.summary, tests: res.tests, not_done: res.not_done, moved_goldens: res.moved_goldens, mutants: res.mutants, report: res.report }, null, 1)}
## Lane brief (scope)
${lane.brief}
## Check
1. Every brief item: done as claimed? Read the code, docs and report; reproduce the key numbers.
2. Tests have teeth (code lanes): write mutants of the core mechanisms, bounded at 10x the clean-suite time (a hang is itself a finding); a mutant killed only by goldens is a gap.
3. The laws and governance: conservation (exact), dt-correctness, determinism constructs, tie-breaks, no calendar gates, isolation, D-069 originality; no Director decision reopened or invented; PROPOSAL labels where the Director reserved the call.
4. Claims: re-measure every MEASURED claim you can; overclaims are findings.
Return findings with severity (blocker/major/minor/nit), concrete evidence (commands and outputs) and how to reproduce. An empty list is a valid answer. Remove your worktree's bin/obj when done (keep the worktree).`
}

function fixPrompt(lane, res, ver, round) {
  return `You are the FIX agent (round ${round}) for M7 lane ${lane.key} (${lane.title}).
${COMMON}
## Rules
- Work in the lane clone ${W}/${lane.key} on branch lane/${lane.key} (current commit ${res.commit}).
- For EACH finding below: first REPRODUCE it against the tree (a finding is actionable only once you reproduce it; a finder's word alone is not enough). Record REPRODUCED or REFUTED with evidence. Fix only reproduced findings, minimally and in scope, each with a semantic test where code is involved. A refuted finding gets no change.
- Re-run the builds and full suites of every touched solution (Debug and Release), the determinism scan and the doc-link check; run a bounded mutant per code fix; append a 'Verification round ${round}' section to the lane report with a per-finding verdict table, and mark superseded statements in place.
## Lane brief (for scope)
${lane.brief}
## Verifier findings
${JSON.stringify(ver.findings, null, 1)}
${finish(lane.key)}
In the structured result, put the per-finding verdict table (compressed) in summary and keep every other field current.`
}

// [orchestrator 2026-11-13] A fix round that ends with brief items still NOT built is followed by a completion stage, so
// that the next verification round checks the lane as its brief defines it (lane OC's fix round 1 deferred items 5-8).
// Added after impl, verify r1 and fix r1 of run wf_42ee07c9-d2f: their prompts are unchanged, so they replay from cache.
function notBuilt(res) {
  return ((res && res.not_done) || []).some(s => /not built|not implemented|not done/i.test(String(s)))
}

function completePrompt(lane, res, round) {
  return `You are M7 lane ${lane.key} (${lane.title}), COMPLETION STAGE after fix round ${round}. Full effort: this is work the Director prioritized; be thorough and precise, and verify your own claims by measurement.
${COMMON}
## Why this stage runs
The lane brief is the definition of done. Fix round ${round} ended with brief items still NOT built (not_done below). They are built now, before the next verification round, so that round checks the lane as the brief defines it. The orchestrator's session schedule is never your time budget: if you are stopped, a later run continues from your clone and loses nothing, so never shorten, skip or defer work for time.
## Your clone
Work in ${W}/${lane.key} on branch lane/${lane.key} (current commit ${res.commit}). Inspect git log and status first: if an earlier run of this stage was stopped, continue from its commits and edits. Commit in small coherent steps as you go, so that a stop loses nothing.
DISK (overrides any older figure above): the session disk has only about 200 MB free. Build outputs live on the RAM disk (/dev/shm/ram): a prototype lane's Proto.*/bin and obj are links the orchestrator made into /dev/shm/ram/oc-build, and a Sim lane's whole clone is a link into /dev/shm/ram/m7w. Never delete, move or replace those links. Put any other worktree (mutants, baselines) or large file under /dev/shm/ram/m7w/ only, never elsewhere on the disk.
## The lane's account so far
${JSON.stringify({ summary: res.summary, not_done: res.not_done, open_questions: res.open_questions, proposals_for_director: res.proposals_for_director, tests: res.tests, report: res.report }, null, 1)}
## Lane brief (scope and definition of done)
${lane.brief}
## Do
1. Build every brief item that not_done lists as not built, completely and as the brief and the Director's decisions specify. That means code, semantic tests per mechanism, bounded mutants each killed by a named semantic test, and the design and proposal sections the brief asks for. The tests include:
   - a tie-dense test for any ordering or argmax over doubles;
   - a turn-exact pin for every order-delivery semantic, beside any live-versus-replay check;
   - a POPULATED-table round-trip test for any new serialized row type.
2. Where the brief or a Director decision leaves a call open, apply the reading the brief records, label it READING or PROPOSAL, and list it for the Director. Never invent or reopen a decision. An item that is truly blocked (a conflict between frozen items, which needs a CR, or a missing Director ruling) stays not done with the exact blocker. Time is never a reason.
3. Also run what not_done lists as NOT RUN, where it is in scope and feasible.
4. Re-run the builds and full suites of every touched solution (Debug and Release), the determinism scan and the doc-link check.
5. Add a 'Completion after fix round ${round}' section to the lane report covering: what was built; equations or design; evidence MEASURED vs INFERRED; tests; mutants; moved pins with measured attribution; what ran, what was NOT RUN and what was deferred. Mark superseded statements in place.
${finish(lane.key)}
In the structured result, keep every field current; not_done lists only what is still not done, each with its reason.`
}

async function runLane(lane) {
  let res = await agent(implPrompt(lane), { label: `impl:${lane.key}`, phase: 'Implement', schema: LANE_SCHEMA })
  if (!res) return { lane: lane.key, failed: 'implement agent returned nothing' }
  const history = []
  for (let round = 1; round <= 2; round++) {
    const ver = await agent(verifyPrompt(lane, res, round), { label: `verify:${lane.key}:r${round}`, phase: 'Verify', schema: VERIFY_SCHEMA })
    if (!ver) { history.push({ round, verify: 'no result' }); break }
    const findings = ver.findings || []
    const actionable = findings.filter(f => f.severity !== 'nit' || round === 1)
    history.push({ round, findings: findings.map(f => `${f.id}[${f.severity}] ${f.claim}`.slice(0, 300)) })
    if (!actionable.length) break
    const serious = findings.filter(f => f.severity === 'blocker' || f.severity === 'major')
    const fixed = await agent(fixPrompt(lane, res, { findings: actionable }, round), { label: `fix:${lane.key}:r${round}`, phase: 'Fix', schema: LANE_SCHEMA })
    if (fixed) res = fixed
    if (round < 2 && fixed && notBuilt(fixed)) {
      const done = await agent(completePrompt(lane, res, round), { label: `complete:${lane.key}:r${round}`, phase: 'Implement', schema: LANE_SCHEMA })
      if (done) res = done
      history.push({ round, completion: done ? `completion at ${done.commit}` : 'completion agent returned nothing' })
      continue
    }
    if (!serious.length) break
  }
  log(`lane ${lane.key} done at ${res.commit}`)
  return { ...res, history }
}

const out = []
for (const lane of ((args && args.lanes) || [])) out.push(await runLane(lane))
return out
