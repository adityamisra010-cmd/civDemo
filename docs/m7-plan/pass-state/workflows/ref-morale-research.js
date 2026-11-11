export const meta = {
  name: 'ref-morale-research',
  description: 'Deep labelled research of the reference game morale (army and unit level), one agent at a time, provenance-checked dossier',
  phases: [
    { title: 'Sweep', detail: 'one angle at a time: reachability + official, game data, community, targeted questions' },
    { title: 'Check', detail: 'adversarial provenance check of every claim' },
    { title: 'Dossier', detail: 'answers per question with evidence, confidence and gaps' },
  ],
}

const OUT = args.outDir

const QUESTIONS = `
Q1. Does ENTERING a battle, or simply being in one, cost morale in the reference game, at either level? (start-of-battle morale against a unit's base value; any pre-battle or deployment effect; fatigue at the start; effects of the campaign state on starting morale; anything that charges morale for taking part without casualties.)
Q2. Is there an army-wide morale, or an equivalent stock, on the campaign map? What raises and lowers it (victories, defeats, food or supplies, attrition, upkeep, leaders and heroes, events, stances), how does it carry into battle, and how does a battle change it?
Q3. Unit morale in battle: every factor that raises or lowers it, with sizes where findable (casualties total and recent, flank and rear attacks, missile fire, the general's presence or aura, a hero's death or wounding, nearby friendly routs, beating an enemy unit, the battle's overall balance, fatigue, ambush or surprise, fear, terrain, being charged), and the morale states with their thresholds.
Q4. Routing, rallying and shattering: when a routing unit rallies, how often, and what makes a rout permanent.
Q5. How a won or lost battle feeds back to the campaign (experience, replenishment, loot, captives, effect bundles, faction resources, hero motivation or mood, divine favour).
Q6. Ambush: how it is set up on the campaign map, how it is detected, what it does in battle (deployment, surrounded start, a morale penalty for the ambushed side).
Q7. Fatigue: how it builds and recovers, and its effect on morale.
Q8. Commanders and heroes: the presence aura, death, wounding, and abilities that change morale.
G.  Genre patterns: two or three other strategy or tactics games' morale models, briefly, labelled genre.`

const LABELS = `Labels (one per claim):
- CODE: values or definitions read in the game's own data or scripts, or in a faithful extraction of them (a modder's dump of the database with values); give the path or URL, and a hash where you can.
- FETCH: text you read on a fetched page; give the URL and a verbatim quote of at most 25 words.
- SEARCH: only a web-search result summary; give the result URL. This is weaker evidence: say so in notes when a claim rests on one summary.
- KNOW: your own unverified knowledge. Use it only as a hypothesis to test; an unconfirmed KNOW stays KNOW and is never evidence.
Scope (one per claim): reference (the reference game itself), sibling (another title of the same series and developer; name it in the claim; never present it as the reference's), genre (another developer's game).`

const NET = `Network (MEASURED 2026-11-10 with curl through the session's proxy, one try each): www.totalwar.com, steamcommunity.com, www.reddit.com, www.twcenter.net, www.pcgamer.com, www.pcinvasion.com and en.wikipedia.org answered "CONNECT tunnel failed, response 403" (the environment's network policy). Earlier: totalwar.fandom.com and gamepur.com were refused by policy; r2encv2.totalwar.com was refused (connect_rejected 403); the web fetch tool could not resolve primagames.com, www.pcinvasion.com, www.gamepressure.com, wiki.totalwar.com and www.iskmogul.com (cause not established). The web search tool works (it runs outside the container). raw.githubusercontent.com served one file.
Rules:
- The Director may have changed the environment's network access since then, so the FIRST sweep agent re-tests every candidate host once (list below), including those refused earlier, and writes ${OUT}/hosts.json. After that step a refused host is never retried by anyone; read hosts.json first.
- Do NOT use git (clone, ls-remote, fetch) against any repository outside this project: the session's permission check refused it as a containment escape. Do not try to work around it.
- Use the web search tool's extended mode for hard questions (Q1, Q2 and any answer that stays thin) and standard mode otherwise; send several searches in one turn.
- Never invent a mechanic or a number. Say plainly what you could not establish.`

const HOSTS = `www.totalwar.com, wiki.totalwar.com, totalwar.fandom.com, community.creative-assembly.com, forums.totalwar.com, support.sega.com, store.steampowered.com, steamcommunity.com, store.epicgames.com, www.reddit.com, old.reddit.com, www.twcenter.net, tw-modding.com, raw.githubusercontent.com, en.wikipedia.org, www.pcgamer.com, www.pcinvasion.com, www.rockpapershotgun.com, www.gamesradar.com, www.thegamer.com, gamerant.com, screenrant.com, www.dualshockers.com, www.ign.com, wccftech.com, primagames.com, gamepur.com, www.gamepressure.com, prodigygamers.com, www.youtube.com`

const CONTEXT = `You are one agent in a research workflow for the design team of a deterministic civilization simulation (repository /home/user/civDemo, read-only for you). The Director named a commercial strategy game, Total War Saga: TROY (2020), as the research REFERENCE for morale at two levels: an army's morale on the campaign map and each unit's morale in battle (docs/d082-morale-caps-ties-director-answers-2026-11-10.md, DA2). Under the project's originality rule (D-069, docs/ip-originality.md) this is reference only: our design, numbers, names and text stay our own. Your output stays in ${OUT}; write nothing in the repository.
Why this pass: the earlier pass (prototypes/m7-sprint1/reports/H-MOR.md section 1; docs/m7-plan/morale-proposal.md Annex A) read no game file and no web page except one schema file; everything else was search-result snippets. The Director then ruled that entering a battle costs morale (docs/d083-entering-battle-costs-morale-2026-11-10.md) and expected the research to have answered whether the reference does this. Find out properly, and say plainly what cannot be established. Read Annex A of the morale proposal first, so you know what was already claimed and how weakly; three of its "Pattern seen" cells were found to overstate their evidence (rout contagion, fatigue, missile fire).
${QUESTIONS}
${LABELS}
${NET}`

const SWEEP_SCHEMA = {
  type: 'object',
  properties: {
    angle: { type: 'string' },
    file: { type: 'string' },
    claims: {
      type: 'array',
      items: {
        type: 'object',
        properties: {
          id: { type: 'string' },
          questions: { type: 'array', items: { type: 'string' } },
          claim: { type: 'string' },
          label: { type: 'string', enum: ['CODE', 'FETCH', 'SEARCH', 'KNOW'] },
          scope: { type: 'string', enum: ['reference', 'sibling', 'genre'] },
          source: { type: 'string' },
          evidence: { type: 'string' },
          notes: { type: 'string' },
        },
        required: ['id', 'questions', 'claim', 'label', 'scope', 'source', 'evidence'],
      },
    },
    hosts: {
      type: 'array',
      items: {
        type: 'object',
        properties: { host: { type: 'string' }, tool: { type: 'string' }, result: { type: 'string' } },
        required: ['host', 'tool', 'result'],
      },
    },
    not_found: { type: 'array', items: { type: 'string' } },
  },
  required: ['angle', 'file', 'claims', 'hosts', 'not_found'],
}

const ANGLES = [
  { key: 'official', focus: `FIRST, the reachability step: test each candidate host once with curl -sS -o /dev/null -w '%{http_code}' --max-time 12 -I https://<host>/ (the proxy is preconfigured), and once with the web fetch tool for each host curl reached; write ${OUT}/hosts.json as [{host, tool, result}]. Candidate hosts: ${HOSTS}. THEN your angle: the developer's and publisher's own sources: the official site, blog and news posts, patch notes (also mirrored on the publisher's support site and the store pages' news), developer interviews and previews in the press, official forums, any official manual or in-game encyclopedia text; the official encyclopedias of sibling titles of the same engine generation only as scope sibling.` },
  { key: 'data', focus: `the game's own data: the database tables that hold morale constants and morale effects, with VALUES, from modding sources: an Assembly Kit for this title if one was released, modders' extracted tables, mod pages that quote vanilla values, the open-source pack-file tool's schema (names only; the earlier pass read schema_troy.ron from raw.githubusercontent.com), modding wikis and forums. CODE needs values or definitions read in the data itself or a faithful extraction; a mod page quoting a value is FETCH or SEARCH, with the mod named.` },
  { key: 'community', focus: `players' guides and discussions: store community guides, forum and Reddit threads, gaming-press guides and reviews, video titles and descriptions (no transcripts).` },
  { key: 'targeted', focus: `the questions one at a time, Q1 and Q2 first, each in many formulations and in the game's own interface terms (tooltip lines, "leadership", "morale", "battle balance", "the battle is being won", "fatigue", "surprise attack", "ambush", "routing", "shattered", "rally", "army losses"). Read the earlier angles' files first, fill their gaps, and do not repeat what they established.` },
]

phase('Sweep')
const files = []
const sweeps = []
for (const a of ANGLES) {
  const prior = files.length ? `Earlier angles' files (read them first; do not duplicate their claims; never retry a host they recorded as refused): ${files.join(', ')}.` : 'You are the first agent.'
  const r = await agent(
    `${CONTEXT}\n\nYOUR ANGLE (${a.key}): ${a.focus}\n${prior}\nCover every question Q1-Q8 from your angle and G where it fits. Prefer few strong claims over many weak ones, but record every relevant thing you found with its true label. Write your result object as JSON to ${OUT}/${a.key}.json (claim ids prefixed ${a.key}-), then return the same object.`,
    { label: `sweep:${a.key}`, phase: 'Sweep', schema: SWEEP_SCHEMA },
  )
  if (r) { sweeps.push(r); files.push(r.file || `${OUT}/${a.key}.json`) }
  else log(`sweep ${a.key} returned nothing`)
}

phase('Check')
const CHECK_SCHEMA = {
  type: 'object',
  properties: {
    file: { type: 'string' },
    verdicts: {
      type: 'array',
      items: {
        type: 'object',
        properties: {
          id: { type: 'string' },
          verdict: { type: 'string', enum: ['CONFIRMED', 'WEAK', 'UNSUPPORTED', 'CONTRADICTED'] },
          how: { type: 'string' },
          note: { type: 'string' },
        },
        required: ['id', 'verdict', 'how'],
      },
    },
    contradictions: { type: 'array', items: { type: 'string' } },
  },
  required: ['file', 'verdicts', 'contradictions'],
}
const check = await agent(
  `${CONTEXT}\n\nYOU ARE THE ADVERSARIAL PROVENANCE CHECKER. Default to skepticism. Read every claim in ${files.join(', ')} and ${OUT}/hosts.json. For each claim decide whether its source supports it, as labelled and scoped:\n- FETCH: re-fetch the URL (if its host is reachable) and confirm the quote is there and means what the claim says;\n- SEARCH: re-run a search on the claim's terms and confirm a result summary supports it; a claim resting on one summary is at best WEAK;\n- CODE: confirm the value in the file named, or say why you cannot;\n- KNOW: always UNSUPPORTED unless you find evidence (then name it);\n- check scope: a sibling title's mechanic presented as the reference's is CONTRADICTED or UNSUPPORTED.\nCheck every claim that bears on Q1 or Q2 first and hardest. Record contradictions between sources. Never retry a refused host. Write {file, verdicts, contradictions} to ${OUT}/check.json and return it.`,
  { label: 'check', phase: 'Check', schema: CHECK_SCHEMA },
)

phase('Dossier')
const DOSSIER_SCHEMA = {
  type: 'object',
  properties: {
    file: { type: 'string' },
    answers: {
      type: 'array',
      items: {
        type: 'object',
        properties: {
          question: { type: 'string' },
          answer: { type: 'string' },
          confidence: { type: 'string', enum: ['ESTABLISHED', 'LIKELY', 'UNKNOWN'] },
          evidence_ids: { type: 'array', items: { type: 'string' } },
        },
        required: ['question', 'answer', 'confidence', 'evidence_ids'],
      },
    },
    reachable_hosts: { type: 'array', items: { type: 'string' } },
    limits: { type: 'array', items: { type: 'string' } },
  },
  required: ['file', 'answers', 'reachable_hosts', 'limits'],
}
const dossier = await agent(
  `${CONTEXT}\n\nYOU WRITE THE DOSSIER. Inputs: ${files.join(', ')}, ${OUT}/hosts.json and ${OUT}/check.json. Write ${OUT}/dossier.md:\n1. Method and limits: hosts tried and reached (table), what kinds of source were and were not available.\n2. For each question Q1-Q8: the answer in plain words; confidence ESTABLISHED (two independent CONFIRMED sources, or CODE), LIKELY (one CONFIRMED source) or UNKNOWN; the evidence rows (id, claim, label, scope, source, verdict); contradictions; what remains unknown. Q1 and Q2 first and in most detail.\n3. G: genre patterns, labelled.\n4. Sibling-title evidence kept apart from the reference's.\n5. Input for our design (lane MOR2: an entry cost under D-083, the Annex A rewrite and the correction of the three overstated cells): what the evidence suggests and what it does not; no decisions.\nUse only claims whose verdict is CONFIRMED or WEAK as evidence, and say which is which; list UNSUPPORTED and CONTRADICTED claims in a separate section so they are not lost. D-069: reference only. Return {file, answers, reachable_hosts, limits}.`,
  { label: 'dossier', phase: 'Dossier', schema: DOSSIER_SCHEMA },
)

return { sweeps: sweeps.map(s => ({ angle: s.angle, claims: s.claims.length, file: s.file })), check: check && { verdicts: check.verdicts.length, contradictions: check.contradictions.length }, dossier }
